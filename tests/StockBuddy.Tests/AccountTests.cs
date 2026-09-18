using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockBuddy.Web.Data;
using StockBuddy.Web.Services;
using Xunit;

namespace StockBuddy.Tests;

public class AccountTests
{
    private const string Password = "A test-only passphrase 928!";

    [Fact]
    public async Task AccountsArePrivateAndSurviveLogoutLoginAndRevocation()
    {
        await using var app = new TestApp();
        using var alice = app.Client();
        using var bob = app.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await alice.GetAsync("/")).StatusCode);
        var registered = await Register(alice, "alice");
        Assert.Equal(HttpStatusCode.Redirect, registered.StatusCode);
        Assert.Contains("StockBuddy.Account=", string.Join(";", registered.Headers.GetValues("Set-Cookie")));

        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.FindByNameAsync("alice");
        Assert.NotNull(user);
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.True(await users.CheckPasswordAsync(user, Password));
        var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<IdentityUser>>().CreateAsync(user);
        var session = new UserSession(new TestAuthentication(principal),
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<AccountsDbContext>>(),
            scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>());
        var inventory = new InventoryService(scope.ServiceProvider.GetRequiredService<IDbContextFactory<StockBuddyDbContext>>(), session);
        var item = await inventory.AddAsync();
        item.Name = "Alice private staplers";
        item.Count = 23;
        await inventory.SaveAsync(item);
        Assert.Contains("Alice private staplers", await alice.GetStringAsync("/"));

        Assert.Equal(HttpStatusCode.Redirect, (await Register(bob, "bob")).StatusCode);
        Assert.DoesNotContain("Alice private staplers", await bob.GetStringAsync("/"));
        var bobInventory = await InventoryFor(scope.ServiceProvider, "bob");
        Assert.Empty(await bobInventory.ListAsync());

        // A cross-site or tokenless request cannot sign a user out.
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsync("/Account/Logout", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(alice, "/Account/Logout", [])).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await alice.GetAsync("/")).StatusCode);
        // Already-open Blazor circuits validate the old security stamp on every data operation.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => inventory.ListAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => inventory.SaveAsync(item));

        var login = await Submit(alice, "/Account/Login", new()
        {
            ["Input.UserName"] = "alice", ["Input.Password"] = Password, ["Input.RememberMe"] = "true"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("expires=", string.Join(";", login.Headers.GetValues("Set-Cookie")).ToLowerInvariant());
        Assert.Contains("Alice private staplers", await alice.GetStringAsync("/"));
        Assert.DoesNotContain("Alice private staplers", await bob.GetStringAsync("/"));
    }

    [Fact]
    public async Task FormsRejectMissingCsrfWeakPasswordsMismatchesAndDuplicateNames()
    {
        await using var app = new TestApp();
        using var client = app.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Register",
            new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Input.UserName"] = "alice", ["Input.Password"] = Password, ["Input.ConfirmPassword"] = Password }))).StatusCode);
        var weak = await Register(client, "alice", "short");
        Assert.Equal(HttpStatusCode.OK, weak.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
        var mismatch = await Submit(client, "/Account/Register", new()
        {
            ["Input.UserName"] = "alice", ["Input.Password"] = Password, ["Input.ConfirmPassword"] = "Different passphrase!"
        });
        Assert.Contains("Passwords must match", await mismatch.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await Register(client, "alice")).StatusCode);
        using var other = app.Client();
        var duplicate = await Register(other, "ALICE");
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await other.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task RepeatedWrongPasswordsLockAccountAndReturnGenericError()
    {
        await using var app = new TestApp();
        using var signedIn = app.Client();
        await Register(signedIn, "alice");
        using var attacker = app.Client();
        for (var i = 0; i < 5; i++)
        {
            var response = await Submit(attacker, "/Account/Login", new()
            { ["Input.UserName"] = "alice", ["Input.Password"] = "wrong-password" });
            Assert.Contains("Unable to sign in.", await response.Content.ReadAsStringAsync());
        }
        var blocked = await Submit(attacker, "/Account/Login", new()
        { ["Input.UserName"] = "alice", ["Input.Password"] = Password });
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await attacker.GetAsync("/")).StatusCode);
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByNameAsync("alice"))!));
    }

    private static async Task<InventoryService> InventoryFor(IServiceProvider services, string name)
    {
        var user = await services.GetRequiredService<UserManager<IdentityUser>>().FindByNameAsync(name);
        var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IdentityUser>>().CreateAsync(user!);
        return new InventoryService(services.GetRequiredService<IDbContextFactory<StockBuddyDbContext>>(),
            new UserSession(new TestAuthentication(principal),
                services.GetRequiredService<IDbContextFactory<AccountsDbContext>>(),
                services.GetRequiredService<IOptions<IdentityOptions>>()));
    }

    private static Task<HttpResponseMessage> Register(HttpClient client, string name, string password = Password) =>
        Submit(client, "/Account/Register", new()
        {
            ["Input.UserName"] = name, ["Input.Password"] = password, ["Input.ConfirmPassword"] = password
        });

    private static async Task<HttpResponseMessage> Submit(HttpClient client, string path, Dictionary<string, string> fields)
    {
        var html = await client.GetStringAsync(path);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private class TestAuthentication(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
    }

    private class TestApp : WebApplicationFactory<Program>
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "stockbuddy-accounts-" + Guid.NewGuid());
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(directory);
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:StockBuddy", $"Data Source={Path.Combine(directory, "inventory.db")};Pooling=False");
            builder.UseSetting("ConnectionStrings:Accounts", $"Data Source={Path.Combine(directory, "accounts.db")};Pooling=False");
            builder.UseSetting("DataProtection:KeyPath", Path.Combine(directory, "keys"));
        }
        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}

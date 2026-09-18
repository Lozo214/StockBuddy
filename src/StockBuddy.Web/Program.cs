using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using StockBuddy.Web.Components;
using StockBuddy.Web.Data;
using StockBuddy.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddRazorPages();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsync("Too many account requests. Please wait a minute and try again.", token);
    };
    options.AddPolicy("accounts", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var connection = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("StockBuddy") ?? "Data Source=stockbuddy.db");
if (!Path.IsPathRooted(connection.DataSource))
    connection.DataSource = Path.Combine(builder.Environment.ContentRootPath, connection.DataSource);
Directory.CreateDirectory(Path.GetDirectoryName(connection.DataSource)!);
builder.Services.AddDbContextFactory<StockBuddyDbContext>(options => options.UseSqlite(connection.ToString()));
var accountsConnection = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("Accounts")
    ?? $"Data Source={Path.Combine(Path.GetDirectoryName(connection.DataSource)!, "accounts.db")}");
if (!Path.IsPathRooted(accountsConnection.DataSource))
    accountsConnection.DataSource = Path.Combine(builder.Environment.ContentRootPath, accountsConnection.DataSource);
Directory.CreateDirectory(Path.GetDirectoryName(accountsConnection.DataSource)!);
builder.Services.AddDbContextFactory<AccountsDbContext>(options => options.UseSqlite(accountsConnection.ToString()));
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.SignIn.RequireConfirmedAccount = false;
}).AddEntityFrameworkStores<AccountsDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.Cookie.Name = "StockBuddy.Account";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingAccountState>();
builder.Services.AddScoped<IUserSession, UserSession>();
var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
else if (!string.IsNullOrWhiteSpace(keyPath))
    builder.Services.AddDataProtection().SetApplicationName("StockBuddy")
        .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddScoped<InventoryService>();
var app = builder.Build();
await using (var db = await app.Services.GetRequiredService<IDbContextFactory<StockBuddyDbContext>>().CreateDbContextAsync())
{
    await InventorySchema.InitializeAsync(db);
}
await using (var accounts = await app.Services.GetRequiredService<IDbContextFactory<AccountsDbContext>>().CreateDbContextAsync())
    await accounts.Database.EnsureCreatedAsync();
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseForwardedHeaders();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
app.MapGet("/health", async (IDbContextFactory<StockBuddyDbContext> factory) =>
{
    try
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.StockItems.CountAsync();
        await using var accounts = await app.Services.GetRequiredService<IDbContextFactory<AccountsDbContext>>().CreateDbContextAsync();
        await accounts.Users.CountAsync();
        return Results.Ok(new { status = "healthy" });
    }
    catch { return Results.StatusCode(503); }
});
app.MapStaticAssets();
app.MapRazorPages();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

public partial class Program;

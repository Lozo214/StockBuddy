using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockBuddy.Web.Data;

namespace StockBuddy.Web.Services;

public interface IUserSession
{
    Task<string> GetUserIdAsync();
}

public class UserSession(AuthenticationStateProvider authentication,
    IDbContextFactory<AccountsDbContext> factory, IOptions<IdentityOptions> options) : IUserSession
{
    public async Task<string> GetUserIdAsync()
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        return await ValidateAsync(user, factory, options.Value)
            ?? throw new UnauthorizedAccessException("Please sign in again.");
    }

    internal static async Task<string?> ValidateAsync(ClaimsPrincipal principal,
        IDbContextFactory<AccountsDbContext> factory, IdentityOptions options)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        var id = principal.FindFirstValue(options.ClaimsIdentity.UserIdClaimType);
        var stamp = principal.FindFirstValue(options.ClaimsIdentity.SecurityStampClaimType);
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(stamp)) return null;
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return user is not null && user.SecurityStamp == stamp ? id : null;
    }
}

public class RevalidatingAccountState(ILoggerFactory loggerFactory,
    IDbContextFactory<AccountsDbContext> factory, IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState state, CancellationToken cancellationToken) =>
        await UserSession.ValidateAsync(state.User, factory, options.Value) is not null;
}

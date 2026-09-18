using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StockBuddy.Web.Pages.Account;

[Authorize]
public class LogoutModel(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is not null)
        {
            // Revokes existing cookies and prevents already-open circuits from saving again.
            var result = await users.UpdateSecurityStampAsync(user);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Could not end your sessions. Please try again.");
                return Page();
            }
        }
        await signIn.SignOutAsync();
        return LocalRedirect("/Account/Login");
    }
}

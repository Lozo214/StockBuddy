using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace StockBuddy.Web.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("accounts")]
public class LoginModel(SignInManager<IdentityUser> signIn) : PageModel
{
    [BindProperty] public LoginInput Input { get; set; } = new();

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? LocalRedirect("/") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var result = await signIn.PasswordSignInAsync(Input.UserName.Trim(), Input.Password,
            Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded) return LocalRedirect("/");
        ModelState.AddModelError("", "Unable to sign in. Check your username and password, or try again in 15 minutes if you have made several attempts.");
        return Page();
    }

    public class LoginInput
    {
        [Required, StringLength(40)]
        public string UserName { get; set; } = "";
        [Required, StringLength(128), DataType(DataType.Password)]
        public string Password { get; set; } = "";
        public bool RememberMe { get; set; }
    }
}

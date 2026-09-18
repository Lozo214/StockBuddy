using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace StockBuddy.Web.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("accounts")]
public class RegisterModel(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn) : PageModel
{
    [BindProperty] public RegisterInput Input { get; set; } = new();

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? LocalRedirect("/") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = new IdentityUser { UserName = Input.UserName.Trim() };
        var result = await users.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            await signIn.SignInAsync(user, isPersistent: false);
            return LocalRedirect("/");
        }
        foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        return Page();
    }

    public class RegisterInput
    {
        [Required, StringLength(40, MinimumLength = 3)]
        [RegularExpression(@"^[A-Za-z0-9_.-]+$", ErrorMessage = "Use letters, numbers, dots, underscores, or hyphens for your username.")]
        public string UserName { get; set; } = "";
        [Required, StringLength(128, MinimumLength = 12), DataType(DataType.Password)]
        public string Password { get; set; } = "";
        [Required, Compare(nameof(Password), ErrorMessage = "Passwords must match."), DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";
    }
}

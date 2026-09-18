using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace StockBuddy.Web.Data;

// Kept in a separate file so upgrading the original inventory never replaces its data.
public class AccountsDbContext(DbContextOptions<AccountsDbContext> options)
    : IdentityDbContext<IdentityUser>(options);

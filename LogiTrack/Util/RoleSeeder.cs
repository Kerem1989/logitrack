using LogiTrack.Models;
using Microsoft.AspNetCore.Identity;

public static class RoleSeeder
{
    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        if (!await roleManager.RoleExistsAsync("User"))
        {
            await roleManager.CreateAsync(new IdentityRole("User"));
        }
    }

    // Creates the initial Admin from config (SeedAdmin:UserName/Email/Password), since registration only grants "User"
    public static async Task SeedAdminAsync(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        var section = configuration.GetSection("SeedAdmin");
        var userName = section["UserName"];
        var password = section["Password"];
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
        {
            return;
        }

        var admin = await userManager.FindByNameAsync(userName);
        if (admin == null)
        {
            admin = new ApplicationUser { UserName = userName, Email = section["Email"] };
            var result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Could not seed admin: " +
                    string.Join(" ", result.Errors.Select(e => e.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
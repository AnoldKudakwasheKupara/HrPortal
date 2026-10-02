using HrPortal.Entities;
using Microsoft.AspNetCore.Identity;

namespace HrPortal.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAdministratorAsync(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<int>> roleManager)
        {
            const string roleName = "Administrator";

            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(
                    new IdentityRole<int>(roleName));

                if (!roleResult.Succeeded)
                {
                    throw new Exception(
                        "Failed to create Administrator role: " +
                        string.Join(", ",
                            roleResult.Errors.Select(e => e.Description)));
                }
            }

            const string email = "admin@hrportal.local";
            const string password = "Admin@12345";

            var existingUser = await userManager.FindByEmailAsync(email);

            if (existingUser == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = "System Administrator",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(
                    admin,
                    password);

                if (!result.Succeeded)
                {
                    throw new Exception(
                        "Failed to create Administrator account: " +
                        string.Join(", ",
                            result.Errors.Select(e => e.Description)));
                }

                await userManager.AddToRoleAsync(
                    admin,
                    roleName);
            }
        }
    }
}
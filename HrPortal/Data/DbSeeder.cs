using HrPortal.Entities;
using Microsoft.AspNetCore.Identity;

namespace HrPortal.Data
{
    public static class DbSeeder
    {
        /// <summary>
        /// The roles the portal is built around. Access is granted
        /// by role throughout, so all four must exist before any
        /// user can be given one.
        /// </summary>
        private static readonly string[] Roles =
        {
            "Administrator",
            "HR Officer",
            "MD",
            "Employee"
        };

        public static async Task SeedAdministratorAsync(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<int>> roleManager)
        {
            foreach (var role in Roles)
            {
                if (await roleManager.RoleExistsAsync(role))
                    continue;

                var roleResult = await roleManager.CreateAsync(
                    new IdentityRole<int>(role));

                if (!roleResult.Succeeded)
                {
                    throw new Exception(
                        $"Failed to create {role} role: " +
                        string.Join(", ",
                            roleResult.Errors.Select(e => e.Description)));
                }
            }

            const string roleName = "Administrator";

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

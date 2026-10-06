using HrPortal.Entities;
using HrPortal.DbContexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Data
{
    public static class DbSeeder
    {
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

        /// <summary>
        /// Employee is the baseline role, so any account that predates
        /// that rule, or lost the role some other way, is given it back
        /// on startup. The administrator account is left alone: it is
        /// not a member of staff.
        /// </summary>
        public static async Task BackfillEmployeeRoleAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            const string employeeRole = "Employee";

            var linkedUserIds = await context.Employees
                .Where(e => e.UserId != null)
                .Select(e => e.UserId!.Value)
                .ToListAsync();

            foreach (var userId in linkedUserIds)
            {
                var user = await userManager.FindByIdAsync(
                    userId.ToString());

                if (user == null)
                    continue;

                if (await userManager.IsInRoleAsync(user, employeeRole))
                    continue;

                await userManager.AddToRoleAsync(user, employeeRole);
            }
        }
    }
}

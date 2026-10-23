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

        public static async Task SeedAdministratorEmployeeAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            const string email = "admin@hrportal.local";

            var admin = await userManager.FindByEmailAsync(email);

            if (admin == null)
                return;

            var department = await FindOrCreateItDepartmentAsync(context);

            var employee = await context.Employees
                .FirstOrDefaultAsync(e =>
                    e.UserId == admin.Id ||
                    e.Email.ToLower() == email);

            if (employee == null)
            {
                context.Employees.Add(new Employee
                {
                    EmployeeNumber = await UniqueEmployeeNumberAsync(
                        context,
                        "ADMIN"),
                    FirstName = "System",
                    LastName = "Administrator",
                    Email = email,
                    DateJoined = DateTime.Today,
                    DepartmentId = department.Id,
                    UserId = admin.Id,
                    IsActive = true
                });

                await context.SaveChangesAsync();
                return;
            }

            var changed = false;

            if (employee.UserId == null)
            {
                employee.UserId = admin.Id;
                changed = true;
            }

            if (employee.DepartmentId != department.Id)
            {
                employee.DepartmentId = department.Id;
                changed = true;
            }

            if (changed)
                await context.SaveChangesAsync();
        }

        private static readonly string[] ItDepartmentNames =
        {
            "IT", "ICT", "I.T.", "I.T", "IT DEPARTMENT", "ICT DEPARTMENT",
            "INFORMATION TECHNOLOGY",
            "INFORMATION TECHNOLOGY DEPARTMENT",
            "INFORMATION AND COMMUNICATION TECHNOLOGY",
            "INFORMATION & COMMUNICATION TECHNOLOGY",
            "INFORMATION COMMUNICATION TECHNOLOGY",
            "IT SUPPORT", "IT SERVICES", "TECHNOLOGY"
        };

        private static async Task<Department> FindOrCreateItDepartmentAsync(
            ApplicationDbContext context)
        {
            var departments = await context.Departments.ToListAsync();

            var existing = departments
                .Where(d => ItDepartmentNames.Contains(
                    d.Name.Trim().ToUpperInvariant()))
                .OrderByDescending(d => d.IsActive)
                .FirstOrDefault();

            if (existing != null)
            {
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    await context.SaveChangesAsync();
                }

                return existing;
            }

            var department = new Department
            {
                Name = "IT",
                Description = "Information Technology",
                IsActive = true
            };

            context.Departments.Add(department);

            await context.SaveChangesAsync();

            return department;
        }

        private static async Task<string> UniqueEmployeeNumberAsync(
            ApplicationDbContext context,
            string preferred)
        {
            var number = preferred;
            var suffix = 1;

            while (await context.Employees
                .AnyAsync(e => e.EmployeeNumber == number))
            {
                number = preferred + "-" + (++suffix);
            }

            return number;
        }

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

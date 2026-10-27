using HrPortal.Entities;
using HrPortal.DbContexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Data
{
    /// <summary>
    /// Where the first administrator's credentials come from.
    ///
    /// They used to be written into the source, which meant anybody
    /// who could read the repository could sign in to any deployment
    /// of it. Now they are configuration: set SeedAdministrator:Email
    /// and SeedAdministrator:Password, through user secrets or
    /// environment variables rather than a file in the repository.
    ///
    /// Development keeps the old pair so a fresh clone still runs,
    /// and nowhere else does: without configuration, no account is
    /// created at all.
    /// </summary>
    public sealed record AdminSeed(string Email, string? Password)
    {
        public const string Section = "SeedAdministrator";

        public const string DevelopmentEmail = "admin@hrportal.local";

        private const string DevelopmentPassword = "Admin@12345";

        /// <summary>
        /// Null when no account should be created, which is what
        /// happens outside Development if nobody configured one.
        /// </summary>
        public bool CanCreate => !string.IsNullOrWhiteSpace(Password);

        public static AdminSeed From(
            IConfiguration configuration,
            bool isDevelopment)
        {
            var email = configuration[$"{Section}:Email"];
            var password = configuration[$"{Section}:Password"];

            if (!string.IsNullOrWhiteSpace(email) &&
                !string.IsNullOrWhiteSpace(password))
            {
                return new AdminSeed(email, password);
            }

            if (isDevelopment)
                return new AdminSeed(DevelopmentEmail, DevelopmentPassword);

            return new AdminSeed(
                string.IsNullOrWhiteSpace(email)
                    ? DevelopmentEmail
                    : email,
                null);
        }
    }

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
            RoleManager<IdentityRole<int>> roleManager,
            AdminSeed seed,
            ILogger logger)
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

            var email = seed.Email;

            var existingUser = await userManager.FindByEmailAsync(email);

            if (existingUser == null)
            {
                if (!seed.CanCreate)
                {
                    logger.LogWarning(
                        "No administrator account was created. Set " +
                        "{Section}:Email and {Section}:Password to " +
                        "create one; outside Development there is no " +
                        "default, so that nobody can sign in with " +
                        "credentials taken from the source.",
                        AdminSeed.Section,
                        AdminSeed.Section);

                    return;
                }

                var admin = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = "System Administrator",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(
                    admin,
                    seed.Password!);

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
            ApplicationDbContext context,
            string email)
        {

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

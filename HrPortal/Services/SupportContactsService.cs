using HrPortal.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>One person somebody can go to for help.</summary>
    public record SupportPerson(
        string Name,
        string? JobTitle,
        string? Email,
        string? Phone);

    /// <summary>
    /// Who to ask: Human Resources for anything people-related, and
    /// the IT department for anything the portal or a device is doing.
    /// </summary>
    public record SupportContacts(
        IReadOnlyList<SupportPerson> Hr,
        IReadOnlyList<SupportPerson> It);

    public class SupportContactsService
    {
        private readonly ApplicationDbContext _context;

        public SupportContactsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<SupportContacts> GetAsync()
        {
            return new SupportContacts(
                await HrAsync(),
                await ItAsync());
        }

        /// <summary>
        /// Everybody holding the HR Officer role.
        ///
        /// The role, not the approval setting: the setting names the
        /// one person who signs leave off, which is a different
        /// question from who an employee may ring about their pay.
        /// A company with two HR officers should see both here.
        /// </summary>
        private async Task<List<SupportPerson>> HrAsync()
        {
            const string role = Security.Roles.HrOfficer;

            return await _context.Employees
                .AsNoTracking()
                .Where(e =>
                    e.IsActive &&
                    e.UserId != null &&
                    _context.UserRoles.Any(ur =>
                        ur.UserId == e.UserId.Value &&
                        _context.Roles.Any(r =>
                            r.Id == ur.RoleId && r.Name == role)))
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Select(e => new SupportPerson(
                    e.FirstName + " " + e.LastName,
                    e.JobTitle,
                    e.Email,
                    e.PhoneNumber))
                .ToListAsync();
        }

        /// <summary>
        /// Everybody in the IT department. There is no IT role, so
        /// this one goes by department.
        /// </summary>
        private async Task<List<SupportPerson>> ItAsync()
        {
            // Matching the department by name has to happen here
            // rather than in the query: the comparison is
            // case-insensitive and trims, which SQL Server would do
            // under its own collation rather than ours.
            var candidates = await _context.Employees
                .AsNoTracking()
                .Where(e => e.IsActive && e.Department.IsActive)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Select(e => new
                {
                    e.FirstName,
                    e.LastName,
                    e.JobTitle,
                    e.Email,
                    e.PhoneNumber,
                    DepartmentName = e.Department.Name
                })
                .ToListAsync();

            return candidates
                .Where(e => ItDepartment.Matches(e.DepartmentName))
                .Select(e => new SupportPerson(
                    $"{e.FirstName} {e.LastName}".Trim(),
                    e.JobTitle,
                    e.Email,
                    e.PhoneNumber))
                .ToList();
        }
    }
}

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
        HrContact Hr,
        IReadOnlyList<SupportPerson> It);

    public class SupportContactsService
    {
        private readonly ApplicationDbContext _context;
        private readonly HrContactService _hrContact;

        public SupportContactsService(
            ApplicationDbContext context,
            HrContactService hrContact)
        {
            _context = context;
            _hrContact = hrContact;
        }

        public async Task<SupportContacts> GetAsync()
        {
            var hr = await _hrContact.GetAsync();

            // Matching the department by name has to happen here
            // rather than in the query: the comparison is
            // case-insensitive and trims, which SQL Server would do
            // under its own collation rather than ours.
            var candidates = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
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

            var it = candidates
                .Where(e => ItDepartment.Matches(e.DepartmentName))
                .Select(e => new SupportPerson(
                    $"{e.FirstName} {e.LastName}".Trim(),
                    e.JobTitle,
                    e.Email,
                    e.PhoneNumber))
                .ToList();

            return new SupportContacts(hr, it);
        }
    }
}

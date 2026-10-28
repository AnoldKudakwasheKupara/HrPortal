using HrPortal.DbContexts;
using HrPortal.Entities;
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
        private readonly ApproverDirectory _approvers;

        public SupportContactsService(
            ApplicationDbContext context,
            ApproverDirectory approvers)
        {
            _context = context;
            _approvers = approvers;
        }

        public async Task<SupportContacts> GetAsync()
        {
            return new SupportContacts(
                await HrAsync(),
                await ItAsync());
        }

        /// <summary>
        /// Everybody holding the HR Officer role, from the one place
        /// that answers who holds a role. An employee ringing about
        /// their pay should reach whoever can help, not whichever one
        /// of them a second list happened to name.
        /// </summary>
        private async Task<List<SupportPerson>> HrAsync()
        {
            var people = await _approvers.PeopleAsync(
                ApprovalRole.HROfficer);

            return people
                .Select(p => new SupportPerson(
                    p.Name, p.JobTitle, p.Email, p.Phone))
                .ToList();
        }

        /// <summary>
        /// Everybody in the IT department. There is no IT role, so
        /// this one goes by department.
        /// </summary>
        private async Task<List<SupportPerson>> ItAsync()
        {
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

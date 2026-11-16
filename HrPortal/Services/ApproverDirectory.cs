using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>One person who can act in an approval role.</summary>
    public record ApproverPerson(
        int EmployeeId,
        string Name,
        string? JobTitle,
        string? Email,
        string? Phone);

    /// <summary>
    /// Who holds an approval role.
    ///
    /// It used to be whoever an administrator had named on a
    /// settings page, which was a second list of the same fact: a
    /// company could grant somebody the HR Officer role and still
    /// have the portal send everything to whoever the setting named,
    /// or name nobody and have requests sit where nobody was told
    /// about them. The sign-in role is now the whole answer.
    ///
    /// It answers with everybody, not one person. Two HR officers is
    /// an ordinary thing for a company to have, and a request either
    /// of them may act on should reach both.
    /// </summary>
    public class ApproverDirectory
    {
        private readonly ApplicationDbContext _context;
        private readonly CompanyService _companies;

        public ApproverDirectory(
            ApplicationDbContext context,
            CompanyService companies)
        {
            _context = context;
            _companies = companies;
        }

        /// <summary>Whether anybody holds it at this portal's company.</summary>
        public async Task<bool> AnybodyHereAsync(ApprovalRole role) =>
            await AnybodyAsync(role, await _companies.CurrentIdAsync());

        /// <summary>The people who hold it at this portal's company.</summary>
        public async Task<List<ApproverPerson>> PeopleHereAsync(ApprovalRole role) =>
            await PeopleAsync(role, await _companies.CurrentIdAsync());

        /// <summary>
        /// The sign-in role that carries this approval role. The two
        /// enums are separate because one is a stage in a workflow
        /// and the other is a thing a person is granted, but this is
        /// the only place that has to know they line up.
        /// </summary>
        public static string SignInRole(ApprovalRole role) =>
            role switch
            {
                ApprovalRole.HROfficer => Roles.HrOfficer,
                ApprovalRole.MD => Roles.Md,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(role),
                    role,
                    "No sign-in role carries this approval role.")
            };

        /// <summary>
        /// Active employees with a sign-in that holds the role at the
        /// company. Somebody who has left, or who has no account to
        /// sign in with, cannot approve anything.
        /// </summary>
        private IQueryable<Employee> Holders(ApprovalRole role, int companyId) =>
            HoldersOf(SignInRole(role), companyId);

        private IQueryable<Employee> HoldersOf(string signInRole, int? companyId)
        {
            var users = _context.UserIdsWithRole([signInRole], companyId);

            return _context.Employees
                .AsNoTracking()
                .Where(e =>
                    e.IsActive &&
                    e.UserId != null &&
                    users.Contains(e.UserId.Value));
        }

        /// <summary>
        /// The people who hold the role at the company that employs
        /// somebody, so their requests reach their own approvers.
        /// </summary>
        public async Task<List<int>> EmployeeIdsForAsync(ApprovalRole role, int employeeId)
        {
            var home = await HomeCompanyOfAsync(employeeId);

            return home == null
                ? []
                : await Holders(role, home.Value).Select(e => e.Id).ToListAsync();
        }

        /// <summary>
        /// The same, for a sign-in role that is not an approval stage,
        /// such as the administrators who look after licences. A role
        /// held at one company needs the company named.
        /// </summary>
        public Task<List<int>> EmployeeIdsWithRoleAsync(string signInRole, int? companyId = null)
        {
            if (companyId == null && Roles.IsCompanyRole(signInRole))
                throw new ArgumentException(
                    $"'{signInRole}' is held at one company, so say which.",
                    nameof(companyId));

            return HoldersOf(signInRole, companyId).Select(e => e.Id).ToListAsync();
        }

        /// <summary>
        /// Whether this employee holds the role at the company that
        /// employs them -- which is how the portal knows a request
        /// belongs to an approver and should not wait for their own
        /// decision.
        /// </summary>
        public async Task<bool> HoldsAsync(ApprovalRole role, int employeeId)
        {
            var home = await HomeCompanyOfAsync(employeeId);

            return home != null &&
                   await Holders(role, home.Value).AnyAsync(e => e.Id == employeeId);
        }

        /// <summary>
        /// Whether anybody holds it at the company at all. Nobody
        /// means requests will reach that stage and sit there
        /// unannounced, which is worth warning an administrator about.
        /// </summary>
        public Task<bool> AnybodyAsync(ApprovalRole role, int companyId) =>
            Holders(role, companyId).AnyAsync();

        private Task<int?> HomeCompanyOfAsync(int employeeId) =>
            _context.Employees
                .Where(e => e.Id == employeeId)
                .Select(e => (int?)e.HomeCompanyId)
                .FirstOrDefaultAsync();

        /// <summary>
        /// Everybody who holds it at the company, with how to reach
        /// them.
        /// </summary>
        public Task<List<ApproverPerson>> PeopleAsync(ApprovalRole role, int companyId) =>
            Holders(role, companyId)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Select(e => new ApproverPerson(
                    e.Id,
                    e.FirstName + " " + e.LastName,
                    e.JobTitle,
                    e.Email,
                    e.PhoneNumber))
                .ToListAsync();
    }
}

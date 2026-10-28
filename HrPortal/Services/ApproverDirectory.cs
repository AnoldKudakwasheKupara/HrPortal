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

        public ApproverDirectory(ApplicationDbContext context)
        {
            _context = context;
        }

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
        /// Active employees with a sign-in that holds the role.
        /// Somebody who has left, or who has no account to sign in
        /// with, cannot approve anything.
        /// </summary>
        private IQueryable<Employee> Holders(ApprovalRole role)
        {
            var signInRole = SignInRole(role);

            return _context.Employees
                .AsNoTracking()
                .Where(e =>
                    e.IsActive &&
                    e.UserId != null &&
                    _context.UserRoles.Any(ur =>
                        ur.UserId == e.UserId.Value &&
                        _context.Roles.Any(r =>
                            r.Id == ur.RoleId &&
                            r.Name == signInRole)));
        }

        public Task<List<int>> EmployeeIdsAsync(ApprovalRole role) =>
            Holders(role).Select(e => e.Id).ToListAsync();

        /// <summary>
        /// Whether this employee is one of the people who hold the
        /// role -- which is how the portal knows a request belongs
        /// to an approver and should not wait for their own decision.
        /// </summary>
        public Task<bool> HoldsAsync(ApprovalRole role, int employeeId) =>
            Holders(role).AnyAsync(e => e.Id == employeeId);

        /// <summary>
        /// Whether anybody holds it at all. Nobody means requests
        /// will reach that stage and sit there unannounced, which is
        /// worth warning an administrator about.
        /// </summary>
        public Task<bool> AnybodyAsync(ApprovalRole role) =>
            Holders(role).AnyAsync();

        /// <summary>
        /// Everybody who holds it, with how to reach them.
        /// </summary>
        public Task<List<ApproverPerson>> PeopleAsync(ApprovalRole role) =>
            Holders(role)
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

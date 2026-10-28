using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// Who reports to whom, and what that is allowed to look like.
    ///
    /// Everybody has a supervisor except the Managing Director, who
    /// is identified by holding the MD approval role rather than by
    /// job title -- the title is free text and nobody signs anything
    /// because of it.
    /// </summary>
    public class SupervisorService
    {
        private readonly ApplicationDbContext _context;

        public SupervisorService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// The employee holding the MD approval role, or null where
        /// nobody does.
        /// </summary>
        public Task<int?> ManagingDirectorIdAsync() =>
            _context.ApprovalConfigurations
                .AsNoTracking()
                .Where(a =>
                    a.Role == ApprovalRole.MD &&
                    a.IsActive &&
                    a.Employee.IsActive)
                .Select(a => (int?)a.EmployeeId)
                .FirstOrDefaultAsync();

        /// <summary>
        /// Everybody needs one except whoever sits at the top.
        /// </summary>
        public async Task<bool> RequiresSupervisorAsync(int employeeId)
        {
            var md = await ManagingDirectorIdAsync();

            return md != employeeId;
        }

        /// <summary>
        /// Who may supervise this person: anybody active except
        /// themselves and anybody already beneath them, since either
        /// would close the chain into a loop.
        /// </summary>
        public async Task<List<Employee>> CandidatesAsync(int employeeId)
        {
            var people = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Where(e => e.IsActive)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();

            var beneath = Beneath(people, employeeId);

            return people
                .Where(e => e.Id != employeeId && !beneath.Contains(e.Id))
                .ToList();
        }

        /// <summary>
        /// Why this pairing is not allowed, or null when it is.
        /// </summary>
        public async Task<string?> RejectionReasonAsync(
            int employeeId,
            int? supervisorId)
        {
            if (supervisorId == null)
                return null;

            if (supervisorId == employeeId)
                return "An employee cannot supervise themselves.";

            var people = await _context.Employees
                .AsNoTracking()
                .Select(e => new Employee
                {
                    Id = e.Id,
                    SupervisorId = e.SupervisorId,
                    IsActive = e.IsActive,
                    FirstName = e.FirstName,
                    LastName = e.LastName
                })
                .ToListAsync();

            var supervisor = people
                .FirstOrDefault(e => e.Id == supervisorId);

            if (supervisor == null)
                return "That supervisor is not on the staff list.";

            if (!supervisor.IsActive)
                return "That supervisor has left the company.";

            if (Beneath(people, employeeId).Contains(supervisorId.Value))
            {
                return
                    $"{supervisor.FirstName} {supervisor.LastName} " +
                    "already reports to this employee, directly or " +
                    "through somebody else, so this would close the " +
                    "chain into a loop.";
            }

            return null;
        }

        /// <summary>
        /// Everybody under this person, however far down.
        /// </summary>
        private static HashSet<int> Beneath(
            IReadOnlyCollection<Employee> people,
            int employeeId)
        {
            var beneath = new HashSet<int>();

            var frontier = new Queue<int>();

            frontier.Enqueue(employeeId);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                foreach (var report in people.Where(e =>
                    e.SupervisorId == current))
                {
                    // Already-seen guards against a loop that somehow
                    // exists in the data from running forever.
                    if (!beneath.Add(report.Id))
                        continue;

                    frontier.Enqueue(report.Id);
                }
            }

            return beneath;
        }

        /// <summary>
        /// The supervisor a request should go to first, or null where
        /// the employee has none and the request should go straight
        /// to HR as it always did.
        /// </summary>
        public Task<Employee?> SupervisorOfAsync(int employeeId) =>
            _context.Employees
                .AsNoTracking()
                .Where(e => e.Id == employeeId && e.SupervisorId != null)
                .Select(e => e.Supervisor!)
                .Where(s => s.IsActive)
                .FirstOrDefaultAsync();

        /// <summary>
        /// The employee record behind a signed-in user, where they
        /// supervise anybody at all.
        /// </summary>
        public Task<bool> SupervisesAnyoneAsync(int employeeId) =>
            _context.Employees
                .AsNoTracking()
                .AnyAsync(e =>
                    e.SupervisorId == employeeId && e.IsActive);
    }
}

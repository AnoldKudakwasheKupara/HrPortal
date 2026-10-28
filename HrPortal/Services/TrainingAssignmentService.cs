using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// What came of an attempt to assign a course.
    /// </summary>
    public record TrainingAssignmentOutcome(
        int Assigned,
        int AlreadyAssigned)
    {
        public bool DidNothing => Assigned == 0;
    }

    /// <summary>
    /// Putting a course in front of people.
    ///
    /// The assignments page and the course form both do this, and
    /// each telling somebody they have been assigned training is
    /// one place too many for that to be forgotten.
    /// </summary>
    public class TrainingAssignmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly AuditService _audit;

        public TrainingAssignmentService(
            ApplicationDbContext context,
            NotificationService notifications,
            AuditService audit)
        {
            _context = context;
            _notifications = notifications;
            _audit = audit;
        }

        /// <summary>
        /// Everybody on the staff list as it stands. A snapshot:
        /// whoever joins afterwards is not in it.
        /// </summary>
        public Task<List<int>> EveryoneAsync() =>
            _context.Employees
                .Where(e => e.IsActive)
                .Select(e => e.Id)
                .ToListAsync();

        /// <summary>
        /// Assigns the course to those of these people who do not
        /// already have it, tells each of them, and records it.
        /// Saves, so the course has to exist first.
        /// </summary>
        public async Task<TrainingAssignmentOutcome> AssignAsync(
            TrainingCourse course,
            IEnumerable<int> employeeIds,
            DateTime? dueDate)
        {
            var wanted = employeeIds.Distinct().ToList();

            var already = await _context.TrainingAssignments
                .Where(a => a.TrainingCourseId == course.Id)
                .Select(a => a.EmployeeId)
                .ToListAsync();

            var toAssign = wanted
                .Where(id => !already.Contains(id))
                .ToList();

            if (toAssign.Count == 0)
                return new TrainingAssignmentOutcome(0, wanted.Count);

            var due = dueDate.HasValue
                ? $", due {dueDate.Value:dd MMM yyyy}."
                : ".";

            foreach (var employeeId in toAssign)
            {
                _context.TrainingAssignments.Add(
                    new TrainingAssignment
                    {
                        TrainingCourseId = course.Id,
                        EmployeeId = employeeId,
                        DueDate = dueDate,
                        IsCompleted = false,
                        AssignedAt = DateTime.UtcNow
                    });

                _notifications.Notify(
                    employeeId,
                    "Training assigned",
                    $"You have been assigned \"{course.Title}\"{due}",
                    "/MyTraining");
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.TrainingAssigned,
                nameof(TrainingCourse),
                course.Title,
                $"Assigned to {toAssign.Count} employee(s).");

            return new TrainingAssignmentOutcome(
                toAssign.Count,
                wanted.Count - toAssign.Count);
        }
    }
}

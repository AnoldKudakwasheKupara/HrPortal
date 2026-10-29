using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// What came of assigning a course.
    /// </summary>
    public record TrainingAssignmentOutcome(int Assigned)
    {
        public bool DidNothing => Assigned == 0;
    }

    /// <summary>
    /// Putting a course in front of people.
    ///
    /// A course used to be created and then assigned, on a separate
    /// page, by picking employees out of a list. Creating it is the
    /// whole of it now: everybody gets it, and the trainer is told
    /// they are delivering it.
    /// </summary>
    public class TrainingAssignmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly AuditService _audit;
        private readonly CurrentUserService _currentUser;

        public TrainingAssignmentService(
            ApplicationDbContext context,
            NotificationService notifications,
            AuditService audit,
            CurrentUserService currentUser)
        {
            _context = context;
            _notifications = notifications;
            _audit = audit;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Whether the signed-in person is the trainer on anything.
        /// The menu asks this to decide whether to offer them the
        /// material pages at all: leading a course is a relationship
        /// and almost nobody has it.
        /// </summary>
        public async Task<bool> LeadsAnyTrainingAsync()
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
                return false;

            return await _context.TrainingCourses
                .AsNoTracking()
                .AnyAsync(c =>
                    c.TrainerEmployeeId == me.Value && c.IsActive);
        }

        /// <summary>
        /// What happens when a course is created: everybody on the
        /// staff list as it stands gets it and is told so, and
        /// whoever is delivering it is told that instead.
        ///
        /// A snapshot. Somebody who joins next month is not caught
        /// by it, and takes the course when one is next created.
        ///
        /// The trainer is not given their own course to complete.
        /// They are running the session, and an outstanding training
        /// item for it would be asking them to attend themselves.
        /// </summary>
        public async Task<TrainingAssignmentOutcome>
            AssignNewCourseAsync(
                TrainingCourse course,
                DateTime? dueDate)
        {
            var everyone = await _context.Employees
                .Where(e => e.IsActive)
                .Select(e => e.Id)
                .ToListAsync();

            var scheduled = course.TrainingDate.HasValue
                ? $" on {course.TrainingDate.Value:dd MMM yyyy}"
                : string.Empty;

            if (course.TrainerEmployeeId is int trainer)
            {
                everyone.Remove(trainer);

                var post = course.MaterialsDueDate.HasValue
                    ? "Please prepare, and post the content for it " +
                      "by " +
                      $"{course.MaterialsDueDate.Value:dd MMM yyyy}."
                    : "Please prepare, and post the content for it.";

                _notifications.Notify(
                    trainer,
                    "You are leading a training session",
                    $"You are the trainer for \"{course.Title}\"" +
                    $"{scheduled}. {post} Everybody on the staff " +
                    "list has been assigned it.",
                    "/TrainingMaterial?courseId=" + course.Id,
                    EmailKind.TrainerAssigned);
            }

            var due = dueDate.HasValue
                ? $" It has to be completed by " +
                  $"{dueDate.Value:dd MMM yyyy}."
                : string.Empty;

            foreach (var employeeId in everyone)
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
                    "Training scheduled",
                    $"\"{course.Title}\" has been scheduled" +
                    $"{scheduled} and you are down to attend.{due}",
                    "/MyTraining",
                    EmailKind.TrainingScheduled);
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.TrainingAssigned,
                nameof(TrainingCourse),
                course.Title,
                $"Assigned to {everyone.Count} employee(s).");

            return new TrainingAssignmentOutcome(everyone.Count);
        }
    }
}

using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// Chases the two training dates as they come up: the date the
    /// trainer has to have posted the content by, and the day of the
    /// session itself.
    ///
    /// Sending is kept apart from the job that schedules it, so the
    /// rule can be read in one place and so what would go out today
    /// can be asked without waiting for tomorrow.
    /// </summary>
    public class TrainingReminderService
    {
        /// <summary>
        /// How far ahead the chasing starts. A reminder then goes
        /// every day until the date arrives, in the bell. Only the
        /// one three days out and the one the day before are also
        /// emailed, and for content gone overdue only the first day
        /// of it: seven emails a course is how a reminder becomes
        /// something people filter away.
        /// </summary>
        public const int LeadDays = 7;

        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly ILogger<TrainingReminderService> _logger;

        public TrainingReminderService(
            ApplicationDbContext context,
            NotificationService notifications,
            ILogger<TrainingReminderService> logger)
        {
            _context = context;
            _notifications = notifications;
            _logger = logger;
        }

        public async Task<int> SendDueAsync(DateTime today)
        {
            var day = today.Date;

            var sent = await ContentAsync(day) + await SessionAsync(day);

            if (sent > 0)
            {
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Sent {Count} training reminder(s) for {Day:d}.",
                    sent,
                    day);
            }

            return sent;
        }

        /// <summary>
        /// The trainer, while the content is still not posted.
        /// Chasing carries on past the date: a deadline gone by with
        /// nothing posted is the case that most needs saying, and it
        /// stops the day the content appears or the course is made
        /// inactive.
        /// </summary>
        private async Task<int> ContentAsync(DateTime day)
        {
            var courses = await _context.TrainingCourses
                .Include(c => c.Materials)
                .Where(c =>
                    c.IsActive &&
                    c.TrainerEmployeeId != null &&
                    c.MaterialsDueDate != null)
                .ToListAsync();

            var sent = 0;

            foreach (var course in courses)
            {
                if (course.Materials.Count > 0)
                    continue;

                var due = course.MaterialsDueDate!.Value.Date;
                var days = (due - day).Days;

                if (days > LeadDays)
                    continue;

                var trainer = course.TrainerEmployeeId!.Value;

                if (await AlreadySentAsync(
                        TrainingReminderKind.Content,
                        course.Id,
                        trainer,
                        day))
                {
                    continue;
                }

                var when = days switch
                {
                    > 1 => "is due in " + days + " days",
                    1 => "is due tomorrow",
                    0 => "is due today",
                    -1 => "was due yesterday",
                    _ => "was due " + (-days) + " days ago"
                };

                _notifications.Notify(
                    trainer,
                    days < 0
                        ? "Training content is overdue"
                        : "Training content is due",
                    $"The content for \"{course.Title}\" {when} " +
                    $"({due:dd MMM yyyy}) and nothing has been " +
                    "posted yet.",
                    "/TrainingMaterial?courseId=" + course.Id,
                    days switch
                    {
                        3 or 1 => EmailKind.ContentDue,
                        -1 => EmailKind.ContentOverdue,
                        _ => null
                    });

                Record(
                    TrainingReminderKind.Content,
                    course.Id,
                    trainer,
                    day);

                sent++;
            }

            return sent;
        }

        /// <summary>
        /// Everybody who still has the course to do, and the trainer
        /// delivering it, in the week before the day itself. Nothing
        /// goes out afterwards, because the session has happened.
        /// </summary>
        private async Task<int> SessionAsync(DateTime day)
        {
            var courses = await _context.TrainingCourses
                .Where(c => c.IsActive && c.TrainingDate != null)
                .ToListAsync();

            var sent = 0;

            foreach (var course in courses)
            {
                var date = course.TrainingDate!.Value.Date;
                var days = (date - day).Days;

                if (days > LeadDays || days < 0)
                    continue;

                var audience = await _context.TrainingAssignments
                    .Where(a =>
                        a.TrainingCourseId == course.Id &&
                        !a.IsCompleted)
                    .Select(a => a.EmployeeId)
                    .ToListAsync();

                if (course.TrainerEmployeeId is int trainer &&
                    !audience.Contains(trainer))
                {
                    audience.Add(trainer);
                }

                var when = days switch
                {
                    > 1 => "in " + days + " days",
                    1 => "tomorrow",
                    _ => "today"
                };

                foreach (var employeeId in audience)
                {
                    if (await AlreadySentAsync(
                            TrainingReminderKind.Session,
                            course.Id,
                            employeeId,
                            day))
                    {
                        continue;
                    }

                    var leading =
                        course.TrainerEmployeeId == employeeId;

                    _notifications.Notify(
                        employeeId,
                        "Training is coming up",
                        $"\"{course.Title}\" is {when} " +
                        $"({date:dd MMM yyyy})." +
                        (leading
                            ? " You are delivering it."
                            : string.Empty),
                        leading
                            ? "/TrainingMaterial?courseId=" + course.Id
                            : "/MyTraining",
                        days is 3 or 1
                            ? EmailKind.SessionReminder
                            : null);

                    Record(
                        TrainingReminderKind.Session,
                        course.Id,
                        employeeId,
                        day);

                    sent++;
                }
            }

            return sent;
        }

        /// <summary>
        /// Whether this person has already been told today. Asked of
        /// the database and of what is about to be written, since
        /// nothing is saved until the run finishes.
        /// </summary>
        private async Task<bool> AlreadySentAsync(
            TrainingReminderKind kind,
            int courseId,
            int employeeId,
            DateTime day)
        {
            var pending = _context.ChangeTracker
                .Entries<TrainingReminder>()
                .Any(e =>
                    e.State == EntityState.Added &&
                    e.Entity.Kind == kind &&
                    e.Entity.TrainingCourseId == courseId &&
                    e.Entity.EmployeeId == employeeId &&
                    e.Entity.SentOn == day);

            if (pending)
                return true;

            return await _context.TrainingReminders
                .AnyAsync(r =>
                    r.Kind == kind &&
                    r.TrainingCourseId == courseId &&
                    r.EmployeeId == employeeId &&
                    r.SentOn == day);
        }

        private void Record(
            TrainingReminderKind kind,
            int courseId,
            int employeeId,
            DateTime day)
        {
            _context.TrainingReminders.Add(
                new TrainingReminder
                {
                    Kind = kind,
                    TrainingCourseId = courseId,
                    EmployeeId = employeeId,
                    SentOn = day
                });
        }
    }
}

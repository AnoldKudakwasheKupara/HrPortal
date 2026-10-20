using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reminders
{
    public class TrainingDueRule : IReminderRule
    {
        public string Kind => ReminderKinds.TrainingDue;

        public async Task<int> RunAsync(ReminderRunContext context)
        {
            var today = context.Today;
            var horizon = context.Horizon;

            var assignments = await context.Db.TrainingAssignments
                .AsNoTracking()
                .Include(a => a.TrainingCourse)
                .Where(a =>
                    !a.IsCompleted &&
                    a.DueDate != null &&
                    a.DueDate <= horizon &&
                    a.Employee.IsActive)
                .OrderBy(a => a.DueDate)
                .ToListAsync();

            var sent = 0;

            foreach (var assignment in assignments)
            {
                var days = (assignment.DueDate!.Value.Date - today).Days;

                var when = days switch
                {
                    < 0 => $"was due {-days} day{ReminderText.Plural(-days)} ago",
                    0 => "is due today",
                    _ => $"is due in {days} day{ReminderText.Plural(days)}"
                };

                if (!context.ShouldSend(
                        Kind,
                        assignment.Id.ToString(),
                        assignment.EmployeeId,
                        today))
                {
                    continue;
                }

                context.Notify(
                    assignment.EmployeeId,
                    $"Training {(days < 0 ? "overdue" : "due")}",
                    $"{assignment.TrainingCourse.Title} {when} " +
                    $"({assignment.DueDate:d MMM yyyy}).",
                    "/MyTraining");

                sent++;
            }

            return sent;
        }
    }
}

using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reminders
{
    public class MaintenanceDueRule : IReminderRule
    {
        public string Kind => ReminderKinds.MaintenanceDue;

        public async Task<int> RunAsync(ReminderRunContext context)
        {
            var today = context.Today;
            var horizon = context.Horizon;

            var jobs = await context.Db.AssetMaintenances
                .AsNoTracking()
                .Include(m => m.Asset)
                .Where(m =>
                    m.Status == MaintenanceStatus.Scheduled &&
                    m.ScheduledFor <= horizon)
                .OrderBy(m => m.ScheduledFor)
                .ToListAsync();

            if (jobs.Count == 0)
                return 0;

            var recipients = await context.EmployeeIdsInRoleAsync(Roles.Administrator);

            var sent = 0;

            foreach (var job in jobs)
            {
                var days = (job.ScheduledFor.Date - today).Days;

                var when = days switch
                {
                    < 0 => $"was due {-days} day{ReminderText.Plural(-days)} ago",
                    0 => "is due today",
                    _ => $"is due in {days} day{ReminderText.Plural(days)}"
                };

                foreach (var employeeId in recipients)
                {
                    if (!context.ShouldSend(
                            Kind,
                            job.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    context.Notify(
                        employeeId,
                        $"Maintenance {(days < 0 ? "overdue" : "due")}",
                        $"{ReminderText.Humanise(job.Type)} on {job.Asset.DeviceModel} " +
                        $"{when} ({job.ScheduledFor:d MMM yyyy}).",
                        $"/AssetMaintenance");

                    sent++;
                }
            }

            return sent;
        }
    }
}

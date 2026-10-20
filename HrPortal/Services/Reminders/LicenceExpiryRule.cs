using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reminders
{
    public class LicenceExpiryRule : IReminderRule
    {
        public string Kind => ReminderKinds.LicenceExpiry;

        public async Task<int> RunAsync(ReminderRunContext context)
        {
            var today = context.Today;
            var horizon = context.Horizon;

            var licences = await context.Db.Licenses
                .AsNoTracking()
                .Where(l =>
                    l.Status == LicenseStatus.Active &&
                    l.ExpiryDate <= horizon)
                .OrderBy(l => l.ExpiryDate)
                .ToListAsync();

            if (licences.Count == 0)
                return 0;

            var recipients = await context.EmployeeIdsInRoleAsync(Roles.Administrator);

            var sent = 0;

            foreach (var licence in licences)
            {
                var due = ReminderText.Due(today, licence.ExpiryDate);

                foreach (var employeeId in recipients)
                {
                    if (!context.ShouldSend(
                            Kind,
                            licence.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    context.Notify(
                        employeeId,
                        $"Licence {due}",
                        $"{licence.Name} {due} " +
                        $"({licence.ExpiryDate:d MMM yyyy}).",
                        $"/License/Details/{licence.Id}");

                    sent++;
                }
            }

            return sent;
        }
    }
}

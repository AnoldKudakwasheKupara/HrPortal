using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reminders
{
    public class CertificationExpiryRule : IReminderRule
    {
        public string Kind => ReminderKinds.CertificationExpiry;

        public async Task<int> RunAsync(ReminderRunContext context)
        {
            var today = context.Today;
            var horizon = context.Horizon;

            var certifications = await context.Db.Certifications
                .AsNoTracking()
                .Include(c => c.Employee)
                .Where(c =>
                    c.ExpiryDate != null &&
                    c.ExpiryDate <= horizon &&
                    c.Employee.IsActive)
                .OrderBy(c => c.ExpiryDate)
                .ToListAsync();

            if (certifications.Count == 0)
                return 0;

            var hr = await context.EmployeeIdsInRoleAsync(Roles.HrOfficer);

            var sent = 0;

            foreach (var certification in certifications)
            {
                var due = ReminderText.Due(today, certification.ExpiryDate!.Value);

                var who =
                    $"{certification.Employee.FirstName} " +
                    $"{certification.Employee.LastName}";

                var recipients = hr
                    .Append(certification.EmployeeId)
                    .Distinct();

                foreach (var employeeId in recipients)
                {
                    if (!context.ShouldSend(
                            Kind,
                            certification.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    var mine = employeeId == certification.EmployeeId;

                    context.Notify(
                        employeeId,
                        $"Certification {due}",
                        mine
                            ? $"Your {certification.Name} {due} " +
                              $"({certification.ExpiryDate:d MMM yyyy})."
                            : $"{who}'s {certification.Name} {due} " +
                              $"({certification.ExpiryDate:d MMM yyyy}).",
                        mine
                            ? "/MyProfile"
                            : $"/Certification?employeeId={certification.EmployeeId}");

                    sent++;
                }
            }

            return sent;
        }
    }
}

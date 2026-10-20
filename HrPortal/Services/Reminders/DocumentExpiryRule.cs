using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reminders
{
    public class DocumentExpiryRule : IReminderRule
    {
        public string Kind => ReminderKinds.DocumentExpiry;

        public async Task<int> RunAsync(ReminderRunContext context)
        {
            var today = context.Today;
            var horizon = context.Horizon;

            var documents = await context.Db.EmployeeDocuments
                .AsNoTracking()
                .Include(d => d.Employee)
                .Where(d =>
                    d.ExpiryDate != null &&
                    d.ExpiryDate <= horizon &&
                    d.Employee.IsActive)
                .OrderBy(d => d.ExpiryDate)
                .ToListAsync();

            if (documents.Count == 0)
                return 0;

            var recipients = await context.EmployeeIdsInRoleAsync(Roles.HrOfficer);

            var sent = 0;

            foreach (var document in documents)
            {
                var due = ReminderText.Due(today, document.ExpiryDate!.Value);

                var who =
                    $"{document.Employee.FirstName} " +
                    $"{document.Employee.LastName}";

                foreach (var employeeId in recipients)
                {
                    if (!context.ShouldSend(
                            Kind,
                            document.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    context.Notify(
                        employeeId,
                        $"Document {due}",
                        $"{document.OriginalFileName} on {who}'s file " +
                        $"{due} ({document.ExpiryDate:d MMM yyyy}).",
                        $"/EmployeeDocument?employeeId={document.EmployeeId}");

                    sent++;
                }
            }

            return sent;
        }
    }
}

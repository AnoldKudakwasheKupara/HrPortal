using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reminders
{
    public class ContractEndingRule : IReminderRule
    {
        public string Kind => ReminderKinds.ContractEnding;

        public async Task<int> RunAsync(ReminderRunContext context)
        {
            var today = context.Today;
            var horizon = context.Horizon;

            var employees = await context.Db.Employees
                .AsNoTracking()
                .Where(e =>
                    e.IsActive &&
                    e.ContractEndDate != null &&
                    e.ContractEndDate <= horizon)
                .OrderBy(e => e.ContractEndDate)
                .ToListAsync();

            if (employees.Count == 0)
                return 0;

            var recipients = await context.EmployeeIdsInRoleAsync(Roles.HrOfficer);

            var sent = 0;

            foreach (var employee in employees)
            {
                var due = ReminderText.Due(today, employee.ContractEndDate!.Value);

                foreach (var employeeId in recipients)
                {
                    if (!context.ShouldSend(
                            Kind,
                            employee.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    context.Notify(
                        employeeId,
                        $"Contract {due}",
                        $"{employee.FirstName} {employee.LastName}'s " +
                        $"{ReminderText.Humanise(employee.EmploymentType)} contract {due} " +
                        $"({employee.ContractEndDate:d MMM yyyy}).",
                        $"/Employee/Details/{employee.Id}");

                    sent++;
                }
            }

            return sent;
        }
    }
}

using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reminders
{
    public class PolicyReviewRule : IReminderRule
    {
        public string Kind => ReminderKinds.PolicyReview;

        public async Task<int> RunAsync(ReminderRunContext context)
        {
            var today = context.Today;
            var horizon = context.Horizon;

            var policies = await context.Db.Policies
                .AsNoTracking()
                .Where(p =>
                    p.Status == PolicyStatus.Active &&
                    p.ReviewDate != null &&
                    p.ReviewDate <= horizon)
                .OrderBy(p => p.ReviewDate)
                .ToListAsync();

            if (policies.Count == 0)
                return 0;

            var recipients = await context.EmployeeIdsInRoleAsync(Roles.HrOfficer);

            var sent = 0;

            foreach (var policy in policies)
            {
                var days = (policy.ReviewDate!.Value.Date - today).Days;

                var when = days switch
                {
                    < 0 => $"was due for review {-days} day{ReminderText.Plural(-days)} ago",
                    0 => "is due for review today",
                    _ => $"is due for review in {days} day{ReminderText.Plural(days)}"
                };

                foreach (var employeeId in recipients)
                {
                    if (!context.ShouldSend(
                            Kind,
                            policy.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    context.Notify(
                        employeeId,
                        $"Policy {(days < 0 ? "overdue for review" : "review due")}",
                        $"{policy.Title} version {policy.Version} {when} " +
                        $"({policy.ReviewDate:d MMM yyyy}).",
                        $"/Policy/Details/{policy.Id}");

                    sent++;
                }
            }

            return sent;
        }
    }
}

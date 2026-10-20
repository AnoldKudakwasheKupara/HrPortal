using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reminders
{
    public class PolicyUnreadRule : IReminderRule
    {
        public string Kind => ReminderKinds.PolicyUnread;

        public async Task<int> RunAsync(ReminderRunContext context)
        {
            var today = context.Today;

            var policies = await context.Db.Policies
                .AsNoTracking()
                .Where(p =>
                    p.Status == PolicyStatus.Active &&
                    p.RequiresAcknowledgement &&
                    p.EffectiveDate <= today)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Version,
                    p.Audience,
                    Acknowledged = p.Acknowledgements
                        .Select(a => a.EmployeeId)
                        .ToList()
                })
                .ToListAsync();

            if (policies.Count == 0)
                return 0;

            var staff = await context.Db.Employees
                .AsNoTracking()
                .Where(e => e.IsActive && e.UserId != null)
                .Select(e => new { e.Id, e.UserId })
                .ToListAsync();

            var sent = 0;

            foreach (var policy in policies)
            {
                var audienceIds = await context.AudienceEmployeeIdsAsync(policy.Audience);

                foreach (var employee in staff)
                {
                    if (policy.Acknowledged.Contains(employee.Id))
                        continue;

                    if (!audienceIds.Contains(employee.Id))
                        continue;

                    if (!context.ShouldSend(
                            Kind,
                            policy.Id.ToString(),
                            employee.Id,
                            today))
                    {
                        continue;
                    }

                    context.Notify(
                        employee.Id,
                        "Policy to read",
                        $"{policy.Title} version {policy.Version} is in " +
                        "force and you have not yet confirmed you have " +
                        "read it.",
                        $"/Policy/Details/{policy.Id}");

                    sent++;
                }
            }

            return sent;
        }
    }
}

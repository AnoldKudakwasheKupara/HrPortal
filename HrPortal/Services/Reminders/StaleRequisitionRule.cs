using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reminders
{
    public class StaleRequisitionRule : IReminderRule
    {
        public string Kind => ReminderKinds.RequisitionWaiting;

        public async Task<int> RunAsync(ReminderRunContext context)
        {
            var today = context.Today;

            var cutoff = DateTime.UtcNow.AddDays(-context.Options.StaleApprovalDays);

            var pending = await context.Db.Requisitions
                .AsNoTracking()
                .Where(r =>
                    r.Status == RequisitionStatus.Submitted ||
                    r.Status == RequisitionStatus.ReviewApproved)
                .Select(r => new
                {
                    r.Id,
                    r.RequisitionNumber,
                    r.Status,
                    r.SubmittedAt,
                    r.EstimatedTotal,
                    Employee = r.Employee.FirstName + " " + r.Employee.LastName,
                    LastAction = r.History
                        .Max(h => (DateTime?)h.ActionDate)
                })
                .ToListAsync();

            var sent = 0;

            foreach (var requisition in pending)
            {
                var waitingSince =
                    requisition.LastAction
                    ?? requisition.SubmittedAt
                    ?? DateTime.UtcNow;

                if (waitingSince > cutoff)
                    continue;

                var withMd = requisition.Status ==
                    RequisitionStatus.ReviewApproved;

                var approver = await context.GetApproverEmployeeIdAsync(
                    withMd
                        ? ApprovalRole.MD
                        : ApprovalRole.RequisitionReviewer);

                if (approver == null)
                    continue;

                if (!context.ShouldSend(
                        Kind,
                        requisition.Id.ToString(),
                        approver.Value,
                        today))
                {
                    continue;
                }

                var days = (int)(DateTime.UtcNow - waitingSince).TotalDays;

                context.Notify(
                    approver.Value,
                    $"Requisition waiting {days} day{ReminderText.Plural(days)}",
                    $"{requisition.RequisitionNumber} from " +
                    $"{requisition.Employee} " +
                    $"({requisition.EstimatedTotal:N2}) has been waiting " +
                    $"{days} day{ReminderText.Plural(days)} for your decision.",
                    withMd
                        ? "/RequisitionApproval/FinalApproval"
                        : "/RequisitionApproval");

                sent++;
            }

            return sent;
        }
    }
}

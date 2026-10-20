using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reminders
{
    public class StaleLeaveRule : IReminderRule
    {
        public string Kind => ReminderKinds.LeaveWaiting;

        public async Task<int> RunAsync(ReminderRunContext context)
        {
            var today = context.Today;

            var cutoff = DateTime.UtcNow.AddDays(-context.Options.StaleApprovalDays);

            var pending = await context.Db.LeaveRequests
                .AsNoTracking()
                .Where(r =>
                    r.Status == LeaveRequestStatus.Submitted ||
                    r.Status == LeaveRequestStatus.HRReview ||
                    r.Status == LeaveRequestStatus.HRApproved ||
                    r.Status == LeaveRequestStatus.MDReview)
                .Select(r => new
                {
                    r.Id,
                    r.RequestNumber,
                    r.Status,
                    r.SubmittedAt,
                    Employee = r.Employee.FirstName + " " + r.Employee.LastName,
                    LastAction = r.History
                        .Max(h => (DateTime?)h.ActionDate)
                })
                .ToListAsync();

            var sent = 0;

            foreach (var request in pending)
            {
                var waitingSince = request.LastAction ?? request.SubmittedAt;

                if (waitingSince > cutoff)
                    continue;

                var withMd =
                    LeaveWorkflowService.IsAwaitingMd(request.Status);

                var approver = await context.GetApproverEmployeeIdAsync(
                    withMd ? ApprovalRole.MD : ApprovalRole.HROfficer);

                if (approver == null)
                    continue;

                if (!context.ShouldSend(
                        Kind,
                        request.Id.ToString(),
                        approver.Value,
                        today))
                {
                    continue;
                }

                var days = (int)(DateTime.UtcNow - waitingSince).TotalDays;

                context.Notify(
                    approver.Value,
                    $"Leave waiting {days} day{ReminderText.Plural(days)}",
                    $"{request.RequestNumber} from {request.Employee} has " +
                    $"been waiting {days} day{ReminderText.Plural(days)} for your " +
                    "decision.",
                    withMd
                        ? "/LeaveApproval/FinalApproval"
                        : "/LeaveApproval");

                sent++;
            }

            return sent;
        }
    }
}

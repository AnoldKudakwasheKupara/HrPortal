using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    public class LeaveBalanceService
    {
        private readonly ApplicationDbContext _context;

        public LeaveBalanceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string?> ValidateAgainstBalanceAsync(
            int employeeId,
            int leaveTypeId,
            int year,
            decimal numberOfDays,
            int? excludeRequestId = null)
        {
            var balance = await FindBalance(
                employeeId,
                leaveTypeId,
                year);

            // No balance row means no entitlement has been recorded,
            // which is not the same as an unlimited one. Allowing the
            // request through here was letting leave be approved with
            // nothing to deduct it from.
            if (balance == null)
            {
                var typeName = await _context.LeaveTypes
                    .AsNoTracking()
                    .Where(t => t.Id == leaveTypeId)
                    .Select(t => t.Name)
                    .FirstOrDefaultAsync();

                return
                    $"No {typeName ?? "leave"} entitlement has been set " +
                    $"for {year}, so there is nothing to take these " +
                    "day(s) from. Ask HR to set the balance first.";
            }

            var remaining = balance.RemainingDays;

            if (excludeRequestId.HasValue)
            {
                var existing = await _context.LeaveRequests
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r =>
                        r.Id == excludeRequestId.Value);

                if (existing != null &&
                    existing.LeaveTypeId == leaveTypeId &&
                    HoldsPendingDays(existing.Status))
                {
                    remaining += existing.NumberOfDays;
                }
            }

            if (numberOfDays > remaining)
            {
                return
                    $"This request is for {numberOfDays:0.##} day(s) " +
                    $"but only {remaining:0.##} day(s) remain on the " +
                    $"{balance.LeaveType.Name} balance for {year}.";
            }

            return null;
        }

        /// <summary>
        /// A missing balance row is now refused at validation, so
        /// these three only meet one on requests raised before that
        /// check existed. They stay no-ops rather than throwing, so an
        /// old request can still be approved or cancelled.
        /// </summary>
        public async Task HoldAsync(LeaveRequest request)
        {
            var balance = await FindBalance(
                request.EmployeeId,
                request.LeaveTypeId,
                request.StartDate.Year);

            if (balance == null)
                return;

            balance.PendingDays += request.NumberOfDays;

            Recalculate(balance);
        }

        public async Task ReleaseAsync(LeaveRequest request)
        {
            var balance = await FindBalance(
                request.EmployeeId,
                request.LeaveTypeId,
                request.StartDate.Year);

            if (balance == null)
                return;

            balance.PendingDays -= request.NumberOfDays;

            if (balance.PendingDays < 0)
                balance.PendingDays = 0;

            Recalculate(balance);
        }

        public async Task ConsumeAsync(LeaveRequest request)
        {
            var balance = await FindBalance(
                request.EmployeeId,
                request.LeaveTypeId,
                request.StartDate.Year);

            if (balance == null)
                return;

            balance.PendingDays -= request.NumberOfDays;

            if (balance.PendingDays < 0)
                balance.PendingDays = 0;

            balance.UsedDays += request.NumberOfDays;

            Recalculate(balance);
        }

        public static bool HoldsPendingDays(LeaveRequestStatus status)
        {
            return status == LeaveRequestStatus.Submitted
                || status == LeaveRequestStatus.HRReview
                || status == LeaveRequestStatus.HRApproved
                || status == LeaveRequestStatus.MDReview;
        }

        private static void Recalculate(LeaveBalance balance)
        {
            balance.RemainingDays =
                balance.AllocatedDays
                - balance.UsedDays
                - balance.PendingDays;
        }

        private async Task<LeaveBalance?> FindBalance(
            int employeeId,
            int leaveTypeId,
            int year)
        {
            return await _context.LeaveBalances
                .Include(b => b.LeaveType)
                .FirstOrDefaultAsync(b =>
                    b.EmployeeId == employeeId &&
                    b.LeaveTypeId == leaveTypeId &&
                    b.Year == year);
        }
    }
}

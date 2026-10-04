using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// Keeps leave balances in step with the requests that move
    /// through the approval chain.
    ///
    /// Submitting holds days in Pending. Final approval moves the
    /// hold into Used. Rejection, cancellation or a return to the
    /// employee releases it. Remaining is always
    /// Allocated - Used - Pending, so it is never typed in.
    ///
    /// A leave type with no balance row for that employee and year
    /// is treated as unrationed, which is how unpaid and
    /// compassionate leave are normally run.
    /// </summary>
    public class LeaveBalanceService
    {
        private readonly ApplicationDbContext _context;

        public LeaveBalanceService(ApplicationDbContext context)
        {
            _context = context;
        }


        // ============================================================
        // VALIDATION
        // ============================================================

        /// <summary>
        /// Returns a message when the request would exceed the
        /// employee's remaining days, or null when it is fine or
        /// the leave type is not rationed.
        /// </summary>
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

            if (balance == null)
                return null;

            var remaining = balance.RemainingDays;

            // When editing, the days this request is already
            // holding should not count against itself.
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


        // ============================================================
        // MOVEMENTS
        // ============================================================

        /// <summary>
        /// Places a hold when a request is submitted.
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

        /// <summary>
        /// Releases a hold when a request is rejected, cancelled
        /// or sent back for correction.
        /// </summary>
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

        /// <summary>
        /// Converts a hold into used days on final approval.
        /// </summary>
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


        // ============================================================
        // HELPERS
        // ============================================================

        /// <summary>
        /// True while a request in this status is holding days.
        /// </summary>
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

using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// Keeps leave balances. A leave type with a monthly rate, annual
    /// leave, has a running balance per employee: it starts from an
    /// uploaded figure, gains the rate each month, loses approved leave,
    /// may go below zero, and carries across years. There is one row per
    /// year so past years can still be looked at, but only the latest is
    /// live, whatever year a request's dates fall in. Leave types without
    /// a rate have no running balance.
    /// </summary>
    public class LeaveBalanceService
    {
        private static readonly LeaveRequestStatus[] PendingStatuses =
        {
            LeaveRequestStatus.SupervisorReview,
            LeaveRequestStatus.Submitted,
            LeaveRequestStatus.HRReview,
            LeaveRequestStatus.HRApproved,
            LeaveRequestStatus.MDReview
        };

        private readonly ApplicationDbContext _context;

        public LeaveBalanceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task HoldAsync(LeaveRequest request)
        {
            var balance = await FindBalanceAsync(
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
            var balance = await FindBalanceAsync(
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
            var balance = await FindBalanceAsync(
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

            if (balance.LeaveType.MonthlyAccrualDays > 0)
            {
                _context.LeaveBalanceEntries.Add(new LeaveBalanceEntry
                {
                    EmployeeId = balance.EmployeeId,
                    LeaveTypeId = balance.LeaveTypeId,
                    EntryDate = DateTime.Today,
                    Kind = LeaveBalanceEntryKind.Taken,
                    Days = -request.NumberOfDays,
                    BalanceAfter = Available(balance),
                    Note = $"{request.RequestNumber}: {request.StartDate:d MMM yyyy} " +
                           $"to {request.EndDate:d MMM yyyy}"
                });
            }
        }

        public static bool HoldsPendingDays(LeaveRequestStatus status) =>
            PendingStatuses.Contains(status);

        /// <summary>What the employee has, before leave awaiting approval.</summary>
        public static decimal Available(LeaveBalance balance) =>
            balance.AllocatedDays - balance.UsedDays;

        /// <summary>
        /// The balance a request draws on: the live one for a type with
        /// a monthly rate, otherwise the one for the request's year.
        /// </summary>
        public async Task<LeaveBalance?> FindBalanceAsync(
            int employeeId,
            int leaveTypeId,
            int year)
        {
            var tracked = await _context.LeaveTypes
                .AnyAsync(t => t.Id == leaveTypeId && t.MonthlyAccrualDays > 0);

            return await _context.LeaveBalances
                .Include(b => b.LeaveType)
                .Where(b =>
                    b.EmployeeId == employeeId &&
                    b.LeaveTypeId == leaveTypeId &&
                    (tracked || b.Year == year))
                .OrderByDescending(b => b.Year)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Adds every month due to every active employee's balance, and
        /// starts a balance for anyone who has just joined. It works from
        /// the month each balance last earned, so a server that was off
        /// on the 1st catches up, and running it twice changes nothing.
        /// Returns how many months were added.
        /// </summary>
        public async Task<int> EarnDueAsync(DateTime today)
        {
            var thisMonth = FirstOfMonth(today);

            var types = await _context.LeaveTypes
                .AsNoTracking()
                .Where(t => t.IsActive && t.MonthlyAccrualDays > 0)
                .Select(t => new { t.Id, t.MonthlyAccrualDays })
                .ToListAsync();

            var employees = await _context.Employees
                .AsNoTracking()
                .Select(e => new { e.Id, e.IsActive, e.DateJoined })
                .ToListAsync();

            var earned = 0;

            foreach (var type in types)
            {
                foreach (var employee in employees)
                {
                    var row = await _context.LeaveBalances
                        .Where(b => b.EmployeeId == employee.Id && b.LeaveTypeId == type.Id)
                        .OrderByDescending(b => b.Year)
                        .FirstOrDefaultAsync();

                    if (row == null)
                    {
                        if (!employee.IsActive || employee.DateJoined < thisMonth.AddMonths(-1))
                            continue;

                        row = new LeaveBalance
                        {
                            EmployeeId = employee.Id,
                            LeaveTypeId = type.Id,
                            Year = thisMonth.Year,
                            EarnedThrough = FirstOfMonth(employee.DateJoined),
                            PendingDays = await PendingDaysAsync(employee.Id, type.Id)
                        };

                        Recalculate(row);

                        _context.LeaveBalances.Add(row);
                    }

                    var added = employee.IsActive
                        ? Earn(row, type.MonthlyAccrualDays, thisMonth)
                        : Skip(row, thisMonth);

                    try
                    {
                        await _context.SaveChangesAsync();
                        earned += added;
                    }
                    catch (DbUpdateException)
                    {
                        _context.ChangeTracker.Clear();
                    }
                }
            }

            return earned;
        }

        private int Earn(LeaveBalance row, decimal rate, DateTime thisMonth)
        {
            if (row.EarnedThrough == null)
            {
                row.EarnedThrough = thisMonth;
                return 0;
            }

            var added = 0;

            for (var month = row.EarnedThrough.Value.AddMonths(1);
                 month <= thisMonth;
                 month = month.AddMonths(1))
            {
                if (month.Year > row.Year)
                    row = StartNewYear(row, month.Year);

                row.AllocatedDays += rate;
                row.EarnedThrough = month;

                Recalculate(row);

                _context.LeaveBalanceEntries.Add(new LeaveBalanceEntry
                {
                    EmployeeId = row.EmployeeId,
                    LeaveTypeId = row.LeaveTypeId,
                    EntryDate = month,
                    Kind = LeaveBalanceEntryKind.Earned,
                    Days = rate,
                    BalanceAfter = Available(row),
                    Note = $"Earned for {month:MMMM yyyy}",
                    Period = month.ToString("yyyy-MM")
                });

                added++;
            }

            return added;
        }

        /// <summary>
        /// Someone who has left earns nothing, and is not paid the
        /// months in between should they come back.
        /// </summary>
        private static int Skip(LeaveBalance row, DateTime thisMonth)
        {
            if (row.EarnedThrough == null || row.EarnedThrough < thisMonth)
                row.EarnedThrough = thisMonth;

            return 0;
        }

        /// <summary>
        /// Closes a year and opens the next with what was left. Days
        /// awaiting approval move with it, because that is the balance
        /// they will now be released from or deducted from.
        /// </summary>
        private LeaveBalance StartNewYear(LeaveBalance old, int year)
        {
            var next = new LeaveBalance
            {
                EmployeeId = old.EmployeeId,
                LeaveTypeId = old.LeaveTypeId,
                Year = year,
                AllocatedDays = Available(old),
                PendingDays = old.PendingDays,
                EarnedThrough = old.EarnedThrough
            };

            Recalculate(next);

            old.PendingDays = 0;

            Recalculate(old);

            _context.LeaveBalances.Add(next);

            return next;
        }

        /// <summary>
        /// Sets what an employee has today, from an uploaded figure. The
        /// next month's days are added on top of it. Requests already in
        /// progress are counted against it, since they were made before
        /// there was anything to hold them against. Returns false when
        /// the balance already says this.
        /// </summary>
        public async Task<bool> SetBalanceAsync(
            int employeeId,
            int leaveTypeId,
            decimal days,
            DateTime today,
            string note)
        {
            var row = await _context.LeaveBalances
                .Where(b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId)
                .OrderByDescending(b => b.Year)
                .FirstOrDefaultAsync();

            if (row != null && Available(row) == days)
                return false;

            var was = row == null ? null : (decimal?)Available(row);

            if (row == null)
            {
                row = new LeaveBalance
                {
                    EmployeeId = employeeId,
                    LeaveTypeId = leaveTypeId,
                    Year = today.Year
                };

                _context.LeaveBalances.Add(row);
            }
            else if (row.Year < today.Year)
            {
                row = StartNewYear(row, today.Year);
            }

            row.AllocatedDays = days + row.UsedDays;
            row.PendingDays = await PendingDaysAsync(employeeId, leaveTypeId);
            row.EarnedThrough = FirstOfMonth(today);

            Recalculate(row);

            _context.LeaveBalanceEntries.Add(new LeaveBalanceEntry
            {
                EmployeeId = employeeId,
                LeaveTypeId = leaveTypeId,
                EntryDate = today.Date,
                Kind = LeaveBalanceEntryKind.Opening,
                Days = days,
                BalanceAfter = days,
                Note = was == null ? note : $"{note} (it was {was:0.##})"
            });

            await _context.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Records a change HR made by hand on the balance form, so the
        /// statement still adds up, and has the balance earn from next
        /// month if it is new. Call before saving.
        /// </summary>
        public async Task RecordManualChangeAsync(
            LeaveBalance balance,
            decimal? was,
            string? who)
        {
            var rate = await _context.LeaveTypes
                .Where(t => t.Id == balance.LeaveTypeId)
                .Select(t => t.MonthlyAccrualDays)
                .FirstOrDefaultAsync();

            if (rate <= 0)
                return;

            balance.EarnedThrough ??= FirstOfMonth(DateTime.Today);

            var now = Available(balance);

            if (was == now)
                return;

            _context.LeaveBalanceEntries.Add(new LeaveBalanceEntry
            {
                EmployeeId = balance.EmployeeId,
                LeaveTypeId = balance.LeaveTypeId,
                EntryDate = DateTime.Today,
                Kind = LeaveBalanceEntryKind.Adjusted,
                Days = now - (was ?? 0),
                BalanceAfter = now,
                Note = $"Changed by hand by {who ?? "HR"}"
            });
        }

        public Task<List<LeaveBalanceEntry>> StatementAsync(
            int employeeId,
            int leaveTypeId) =>
            _context.LeaveBalanceEntries
                .AsNoTracking()
                .Where(e => e.EmployeeId == employeeId && e.LeaveTypeId == leaveTypeId)
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.Id)
                .ToListAsync();

        private Task<decimal> PendingDaysAsync(int employeeId, int leaveTypeId) =>
            _context.LeaveRequests
                .Where(r =>
                    r.EmployeeId == employeeId &&
                    r.LeaveTypeId == leaveTypeId &&
                    PendingStatuses.Contains(r.Status))
                .SumAsync(r => r.NumberOfDays);

        private static DateTime FirstOfMonth(DateTime date) =>
            new(date.Year, date.Month, 1);

        private static void Recalculate(LeaveBalance balance)
        {
            balance.RemainingDays =
                balance.AllocatedDays
                - balance.UsedDays
                - balance.PendingDays;
        }
    }
}

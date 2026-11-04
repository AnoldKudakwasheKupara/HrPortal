using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Tests.Leave
{
    public class LeaveBalanceServiceTests
    {
        private const int Annual = 1;
        private const int Sick = 2;
        private const int Staff = 10;

        private static readonly DateTime Nov4 = new(2026, 11, 4);

        private readonly ApplicationDbContext _db;
        private readonly LeaveBalanceService _balances;

        public LeaveBalanceServiceTests()
        {
            _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            _db.LeaveTypes.AddRange(
                new LeaveType { Id = Annual, Name = "Annual Leave", MonthlyAccrualDays = 2 },
                new LeaveType { Id = Sick, Name = "Sick Leave" });

            AddEmployee(Staff, new DateTime(2020, 1, 1));

            _db.SaveChanges();

            _balances = new LeaveBalanceService(_db);
        }

        private void AddEmployee(int id, DateTime joined, bool active = true) =>
            _db.Employees.Add(new Employee
            {
                Id = id,
                EmployeeNumber = $"E{id}",
                FirstName = "Test",
                LastName = $"Person{id}",
                Email = $"e{id}@test.test",
                DateJoined = joined,
                IsActive = active
            });

        private LeaveBalance Live(int employee = Staff) =>
            _db.LeaveBalances.AsEnumerable()
                .Where(b => b.EmployeeId == employee && b.LeaveTypeId == Annual)
                .OrderByDescending(b => b.Year)
                .First();

        private LeaveRequest Request(decimal days, LeaveRequestStatus status = LeaveRequestStatus.Submitted, int type = Annual) =>
            new()
            {
                RequestNumber = "LR-TEST",
                EmployeeId = Staff,
                LeaveTypeId = type,
                StartDate = new DateTime(2026, 12, 7),
                EndDate = new DateTime(2026, 12, 9),
                NumberOfDays = days,
                Status = status
            };

        [Fact]
        public async Task A_new_month_adds_the_rate_to_the_uploaded_balance()
        {
            await _balances.SetBalanceAsync(Staff, Annual, 10, Nov4, "Upload");

            Assert.Equal(0, await _balances.EarnDueAsync(Nov4));
            Assert.Equal(10, Live().RemainingDays);

            Assert.Equal(1, await _balances.EarnDueAsync(new DateTime(2026, 12, 1)));
            Assert.Equal(12, Live().RemainingDays);
        }

        [Fact]
        public async Task Running_again_in_the_same_month_adds_nothing()
        {
            await _balances.SetBalanceAsync(Staff, Annual, 10, Nov4, "Upload");

            await _balances.EarnDueAsync(new DateTime(2026, 12, 1));
            await _balances.EarnDueAsync(new DateTime(2026, 12, 1));
            await _balances.EarnDueAsync(new DateTime(2026, 12, 15));

            Assert.Equal(12, Live().RemainingDays);
            Assert.Single(_db.LeaveBalanceEntries, e => e.Kind == LeaveBalanceEntryKind.Earned);
        }

        [Fact]
        public async Task Months_missed_while_the_server_was_off_are_caught_up()
        {
            await _balances.SetBalanceAsync(Staff, Annual, 10, new DateTime(2026, 8, 20), "Upload");

            Assert.Equal(3, await _balances.EarnDueAsync(Nov4));

            Assert.Equal(16, Live().RemainingDays);
            Assert.Equal(
                new[] { "2026-09", "2026-10", "2026-11" },
                _db.LeaveBalanceEntries.Where(e => e.Period != null).OrderBy(e => e.Period).Select(e => e.Period!));
        }

        [Fact]
        public async Task Each_month_starts_from_where_the_last_left_off()
        {
            await _balances.SetBalanceAsync(Staff, Annual, 10, Nov4, "Upload");

            var request = Request(3);
            await _balances.HoldAsync(request);
            await _db.SaveChangesAsync();
            Assert.Equal(7, Live().RemainingDays);

            await _balances.ConsumeAsync(request);
            await _db.SaveChangesAsync();
            Assert.Equal(7, Live().RemainingDays);
            Assert.Equal(3, Live().UsedDays);
            Assert.Equal(0, Live().PendingDays);

            await _balances.EarnDueAsync(new DateTime(2026, 12, 1));
            Assert.Equal(9, Live().RemainingDays);

            Assert.Equal(
                new decimal[] { 10, 7, 9 },
                _db.LeaveBalanceEntries.OrderBy(e => e.Id).Select(e => e.BalanceAfter));
        }

        [Fact]
        public async Task Leave_beyond_the_balance_takes_it_below_zero_and_earning_recovers_it()
        {
            await _balances.SetBalanceAsync(Staff, Annual, 1, Nov4, "Upload");

            var request = Request(4);
            await _balances.HoldAsync(request);
            await _balances.ConsumeAsync(request);
            await _db.SaveChangesAsync();
            Assert.Equal(-3, Live().RemainingDays);

            await _balances.EarnDueAsync(new DateTime(2026, 12, 1));
            Assert.Equal(-1, Live().RemainingDays);
        }

        [Fact]
        public async Task A_declined_request_gives_its_days_back()
        {
            await _balances.SetBalanceAsync(Staff, Annual, 10, Nov4, "Upload");

            var request = Request(3);
            await _balances.HoldAsync(request);
            await _balances.ReleaseAsync(request);
            await _db.SaveChangesAsync();

            Assert.Equal(10, Live().RemainingDays);
            Assert.DoesNotContain(_db.LeaveBalanceEntries, e => e.Kind == LeaveBalanceEntryKind.Taken);
        }

        [Fact]
        public void A_request_with_the_supervisor_counts_as_holding_days() =>
            Assert.True(LeaveBalanceService.HoldsPendingDays(LeaveRequestStatus.SupervisorReview));

        [Fact]
        public async Task Everything_carries_into_the_new_year_and_the_old_year_stays_on_record()
        {
            await _balances.SetBalanceAsync(Staff, Annual, 10, Nov4, "Upload");

            var taken = Request(4);
            await _balances.HoldAsync(taken);
            await _balances.ConsumeAsync(taken);

            var awaiting = Request(1);
            await _balances.HoldAsync(awaiting);
            await _db.SaveChangesAsync();

            await _balances.EarnDueAsync(new DateTime(2027, 1, 1));

            var old = _db.LeaveBalances.Single(b => b.Year == 2026);
            var live = _db.LeaveBalances.Single(b => b.Year == 2027);

            Assert.Equal(8, old.RemainingDays);
            Assert.Equal(4, old.UsedDays);
            Assert.Equal(0, old.PendingDays);

            Assert.Equal(0, live.UsedDays);
            Assert.Equal(1, live.PendingDays);
            Assert.Equal(6 + 2 + 2 - 1, live.RemainingDays);

            await _balances.ConsumeAsync(awaiting);
            await _db.SaveChangesAsync();

            Assert.Equal(9, live.RemainingDays);
            Assert.Equal(1, live.UsedDays);
            Assert.Equal(4, old.UsedDays);
        }

        [Fact]
        public async Task Leave_types_without_a_rate_earn_nothing_and_have_no_balance()
        {
            await _balances.SetBalanceAsync(Staff, Annual, 10, Nov4, "Upload");
            await _balances.EarnDueAsync(new DateTime(2026, 12, 1));

            var sick = Request(2, type: Sick);
            await _balances.HoldAsync(sick);
            await _balances.ConsumeAsync(sick);
            await _db.SaveChangesAsync();

            Assert.DoesNotContain(_db.LeaveBalances, b => b.LeaveTypeId == Sick);
            Assert.Equal(12, Live().RemainingDays);
        }

        [Fact]
        public async Task Someone_who_has_left_earns_nothing_and_gets_no_back_pay_on_returning()
        {
            AddEmployee(11, new DateTime(2020, 1, 1), active: false);
            await _db.SaveChangesAsync();

            await _balances.SetBalanceAsync(11, Annual, 10, Nov4, "Upload");

            await _balances.EarnDueAsync(new DateTime(2027, 2, 1));
            Assert.Equal(10, Live(11).RemainingDays);

            var employee = await _db.Employees.FindAsync(11);
            employee!.IsActive = true;
            await _db.SaveChangesAsync();

            await _balances.EarnDueAsync(new DateTime(2027, 3, 1));
            Assert.Equal(12, Live(11).RemainingDays);
        }

        [Fact]
        public async Task Someone_who_just_joined_starts_at_zero_but_staff_awaiting_an_upload_do_not_start()
        {
            AddEmployee(12, new DateTime(2026, 11, 20));
            await _db.SaveChangesAsync();

            await _balances.EarnDueAsync(new DateTime(2026, 12, 1));

            Assert.Equal(2, Live(12).RemainingDays);
            Assert.DoesNotContain(_db.LeaveBalances, b => b.EmployeeId == Staff);
        }

        [Fact]
        public async Task Uploading_counts_requests_already_in_progress_and_keeps_days_used()
        {
            var inProgress = Request(2, LeaveRequestStatus.SupervisorReview);
            _db.LeaveRequests.Add(inProgress);
            _db.LeaveRequests.Add(Request(9, LeaveRequestStatus.Rejected));
            await _db.SaveChangesAsync();

            await _balances.SetBalanceAsync(Staff, Annual, 10, Nov4, "Upload");
            Assert.Equal(8, Live().RemainingDays);

            var used = Request(1);
            await _balances.ConsumeAsync(used);
            await _db.SaveChangesAsync();
            Assert.Equal(1, Live().UsedDays);

            await _balances.SetBalanceAsync(Staff, Annual, 20, Nov4, "Correction");
            Assert.Equal(20, LeaveBalanceService.Available(Live()));
            Assert.Equal(1, Live().UsedDays);
        }

        [Fact]
        public async Task Uploading_the_same_figure_changes_nothing()
        {
            Assert.True(await _balances.SetBalanceAsync(Staff, Annual, 10, Nov4, "Upload"));
            Assert.False(await _balances.SetBalanceAsync(Staff, Annual, 10, Nov4, "Upload"));

            Assert.Single(_db.LeaveBalanceEntries);
        }

        [Fact]
        public async Task A_change_by_hand_is_on_the_statement()
        {
            await _balances.SetBalanceAsync(Staff, Annual, 10, Nov4, "Upload");

            var row = Live();
            var was = LeaveBalanceService.Available(row);
            row.AllocatedDays = 12;

            await _balances.RecordManualChangeAsync(row, was, "hr@test.test");
            await _db.SaveChangesAsync();

            var entry = _db.LeaveBalanceEntries.Single(e => e.Kind == LeaveBalanceEntryKind.Adjusted);
            Assert.Equal(2, entry.Days);
            Assert.Equal(12, entry.BalanceAfter);
        }
    }
}

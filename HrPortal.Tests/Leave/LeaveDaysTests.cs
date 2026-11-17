using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Tests.Leave
{
    public class LeaveDaysTests
    {
        private const int Annual = 1;
        private const int Sick = 2;
        private const int Study = 3;
        private const int Staff = 10;
        private const int Intern = 11;

        private static readonly DateTime Today = new(2026, 11, 17);

        private readonly ApplicationDbContext _db;
        private readonly LeaveBalanceService _balances;

        public LeaveDaysTests()
        {
            _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            _db.LeaveTypes.AddRange(
                new LeaveType { Id = Annual, Name = "Annual Leave", MonthlyAccrualDays = 2 },
                new LeaveType { Id = Sick, Name = "Sick Leave" },
                new LeaveType { Id = Study, Name = "Study Leave" });

            AddEmployee(Staff, EmploymentType.Permanent);
            AddEmployee(Intern, EmploymentType.Intern);

            _db.SaveChanges();

            _balances = new LeaveBalanceService(_db);
        }

        private void AddEmployee(int id, EmploymentType type) =>
            _db.Employees.Add(new Employee
            {
                Id = id,
                EmployeeNumber = $"E{id}",
                FirstName = "Test",
                LastName = $"Person{id}",
                Email = $"e{id}@test.test",
                DateJoined = new DateTime(2020, 1, 1),
                EmploymentType = type
            });

        private void AddBalance(int type, int year, decimal allocated, decimal used = 0, decimal pending = 0) =>
            _db.LeaveBalances.Add(new LeaveBalance
            {
                EmployeeId = Staff,
                LeaveTypeId = type,
                Year = year,
                AllocatedDays = allocated,
                UsedDays = used,
                PendingDays = pending,
                RemainingDays = allocated - used - pending
            });

        [Fact]
        public async Task The_live_balance_shows_even_when_it_was_opened_in_an_earlier_year()
        {
            AddBalance(Annual, 2025, 20, used: 4, pending: 2);
            _db.SaveChanges();

            var annual = Assert.Single(await _balances.CurrentAsync(Staff, Today));

            Assert.Equal(Annual, annual.Type.Id);
            Assert.Equal(14, annual.Balance!.RemainingDays);
        }

        [Fact]
        public async Task The_latest_year_wins_for_a_type_that_earns_monthly()
        {
            AddBalance(Annual, 2025, 20);
            AddBalance(Annual, 2026, 7);
            _db.SaveChanges();

            var annual = Assert.Single(await _balances.CurrentAsync(Staff, Today));

            Assert.Equal(2026, annual.Balance!.Year);
        }

        [Fact]
        public async Task A_type_without_a_monthly_rate_shows_only_this_years_balance()
        {
            AddBalance(Annual, 2026, 10);
            AddBalance(Sick, 2025, 30);
            AddBalance(Study, 2026, 5);
            _db.SaveChanges();

            var rows = await _balances.CurrentAsync(Staff, Today);

            Assert.Equal(new[] { "Annual Leave", "Study Leave" }, rows.Select(r => r.Type.Name));
        }

        [Fact]
        public async Task Annual_leave_without_a_balance_still_gets_a_row_to_say_so()
        {
            var annual = Assert.Single(await _balances.CurrentAsync(Staff, Today));

            Assert.Equal(Annual, annual.Type.Id);
            Assert.Null(annual.Balance);
        }

        [Fact]
        public async Task An_intern_is_not_told_annual_leave_is_missing()
        {
            Assert.Empty(await _balances.CurrentAsync(Intern, Today));
        }

        [Fact]
        public async Task A_retired_leave_type_is_left_out()
        {
            AddBalance(Study, 2026, 5);
            _db.LeaveTypes.Find(Study)!.IsActive = false;
            _db.SaveChanges();

            Assert.DoesNotContain(await _balances.CurrentAsync(Staff, Today), r => r.Type.Id == Study);
        }
    }
}

using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Tests.Leave
{
    public class InternLeaveTests
    {
        private const int Vacation = 1;
        private const int Sick = 2;

        private static readonly DateTime Monday = new(2026, 12, 7);

        private readonly ApplicationDbContext _db;
        private readonly LeaveWorkflowService _workflow;

        public InternLeaveTests()
        {
            _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            _db.LeaveTypes.AddRange(
                new LeaveType { Id = Vacation, Name = "Vacation Leave", MonthlyAccrualDays = 2 },
                new LeaveType { Id = Sick, Name = "Sick Leave" });

            _db.Employees.AddRange(Person(1, EmploymentType.Intern), Person(2, EmploymentType.FixedTerm));

            _db.SaveChanges();

            _workflow = new LeaveWorkflowService(
                _db,
                new LeaveBalanceService(_db),
                null!,
                null!,
                new WorkingCalendar(_db),
                null!,
                null!);
        }

        private static Employee Person(int id, EmploymentType type) =>
            new()
            {
                Id = id,
                EmployeeNumber = $"E{id}",
                FirstName = "Test",
                LastName = $"Person{id}",
                Email = $"e{id}@test.test",
                EmploymentType = type,
                IsActive = true
            };

        private Task<LeaveValidationResult> Ask(int employee, int type) =>
            _workflow.ValidateRequestAsync(employee, type, Monday, Monday.AddDays(1), null);

        [Fact]
        public async Task An_intern_cannot_ask_for_vacation_leave()
        {
            var result = await Ask(1, Vacation);

            Assert.Contains("Interns do not have Vacation Leave days", Assert.Single(result.Errors).Message);
        }

        [Fact]
        public async Task An_intern_can_still_ask_for_sick_leave()
        {
            var result = await Ask(1, Sick);

            Assert.Empty(result.Errors);
            Assert.Equal(2, result.NumberOfDays);
        }

        [Fact]
        public async Task Someone_on_a_fixed_term_can_ask_for_vacation_leave()
        {
            Assert.Empty((await Ask(2, Vacation)).Errors);
        }
    }
}

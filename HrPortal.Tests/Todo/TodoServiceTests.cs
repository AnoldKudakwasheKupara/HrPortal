using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Tests.Todo
{
    public class TodoServiceTests
    {
        private const int Boss = 1;
        private const int Report = 2;
        private const int Other = 3;
        private const int Leaver = 4;

        private static readonly DateTime Today = new(2026, 11, 10);

        private readonly ApplicationDbContext _db;
        private readonly TodoService _todos;

        public TodoServiceTests()
        {
            _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            AddEmployee(Boss, "Bea", null);
            AddEmployee(Report, "Rudo", Boss);
            AddEmployee(Other, "Owen", null);
            AddEmployee(Leaver, "Lee", Boss, active: false);

            _db.TodoItems.AddRange(
                Item(Report, "Draft budget", TodoStatus.NotStarted, Today.AddDays(-1)),
                Item(Report, "Call client", TodoStatus.InProgress, Today.AddDays(3)),
                Item(Report, "Old task", TodoStatus.Done, Today.AddDays(-5)),
                Item(Report, "Someday", TodoStatus.NotStarted, null),
                Item(Other, "Not yours", TodoStatus.NotStarted, null));

            _db.SaveChanges();

            _todos = new TodoService(_db);
        }

        private void AddEmployee(int id, string first, int? supervisorId, bool active = true) =>
            _db.Employees.Add(new Employee
            {
                Id = id,
                EmployeeNumber = $"E{id}",
                FirstName = first,
                LastName = "Test",
                Email = $"e{id}@test.test",
                DateJoined = new DateTime(2020, 1, 1),
                SupervisorId = supervisorId,
                IsActive = active
            });

        private static TodoItem Item(int employeeId, string title, TodoStatus status, DateTime? due) =>
            new()
            {
                EmployeeId = employeeId,
                Title = title,
                Status = status,
                DueDate = due,
                CreatedAt = Today.AddDays(-10),
                CompletedAt = status == TodoStatus.Done ? Today.AddDays(-2) : null
            };

        [Fact]
        public async Task Supervisor_sees_only_their_direct_reports()
        {
            Assert.True(await _todos.SupervisesAsync(Boss, Report));
            Assert.False(await _todos.SupervisesAsync(Boss, Other));
            Assert.False(await _todos.SupervisesAsync(Report, Boss));
            Assert.False(await _todos.SupervisesAsync(Other, Report));
        }

        [Fact]
        public async Task List_holds_only_the_owners_items_open_first_soonest_due_first()
        {
            var list = await _todos.ListAsync(Report);

            Assert.Equal(
                new[] { "Draft budget", "Call client", "Someday", "Old task" },
                list.Select(t => t.Title));
        }

        [Fact]
        public async Task Team_counts_each_status_and_overdue_for_active_reports()
        {
            var team = await _todos.TeamAsync(Boss, Today);

            var rudo = Assert.Single(team);
            Assert.Equal(Report, rudo.EmployeeId);
            Assert.Equal(new TodoCounts(2, 1, 1, 1), rudo.Counts);
            Assert.Equal(3, rudo.Counts.Open);
        }

        [Fact]
        public async Task Someone_with_no_reports_has_an_empty_team()
        {
            Assert.Empty(await _todos.TeamAsync(Other, Today));
        }

        [Fact]
        public void Done_items_are_never_overdue()
        {
            var item = Item(Report, "x", TodoStatus.Done, Today.AddDays(-3));

            Assert.False(item.IsOverdue(Today));
        }

        [Fact]
        public void Marking_done_stamps_the_time_and_reopening_clears_it()
        {
            var item = Item(Report, "x", TodoStatus.InProgress, null);
            var now = Today.AddHours(9);

            TodoService.SetStatus(item, TodoStatus.Done, now);

            Assert.Equal(TodoStatus.Done, item.Status);
            Assert.Equal(now, item.CompletedAt);
            Assert.Equal(now, item.UpdatedAt);

            TodoService.SetStatus(item, TodoStatus.InProgress, now.AddHours(1));

            Assert.Null(item.CompletedAt);
        }

        [Fact]
        public void Setting_the_same_status_keeps_the_original_completion_time()
        {
            var item = Item(Report, "x", TodoStatus.Done, null);
            var completed = item.CompletedAt;

            TodoService.SetStatus(item, TodoStatus.Done, Today.AddDays(1));

            Assert.Equal(completed, item.CompletedAt);
        }
    }
}

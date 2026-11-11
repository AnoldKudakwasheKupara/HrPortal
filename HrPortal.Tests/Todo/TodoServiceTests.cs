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
            Assert.Equal(new TodoCounts(2, 1, 1, 1, 1), rudo.Counts);
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

        [Fact]
        public void A_status_shows_only_its_tasks_ten_at_a_time()
        {
            var items = Enumerable.Range(1, 23)
                .Select(i => Item(Report, $"Task {i:00}", TodoStatus.NotStarted, Today.AddDays(i)))
                .Append(Item(Report, "Busy", TodoStatus.InProgress, null))
                .ToList();

            var page = TodoService.Filter(items, TodoStatus.NotStarted, null, null, 3);

            Assert.Equal(3, page.Page);
            Assert.Equal(3, page.TotalPages);
            Assert.Equal(23, page.TotalCount);
            Assert.Equal(new[] { "Task 21", "Task 22", "Task 23" }, page.Items.Select(t => t.Title));

            Assert.Equal(10, TodoService.Filter(items, TodoStatus.NotStarted, null, null, 1).Items.Count);
            Assert.Equal("Busy", Assert.Single(TodoService.Filter(items, TodoStatus.InProgress, null, null, 1).Items).Title);
            Assert.Equal(24, TodoService.Filter(items, null, null, null, 1).TotalCount);
        }

        [Fact]
        public void A_page_past_the_end_shows_the_last_page()
        {
            var items = Enumerable.Range(1, 12)
                .Select(i => Item(Report, $"Task {i}", TodoStatus.Done, null))
                .ToList();

            Assert.Equal(2, TodoService.Filter(items, TodoStatus.Done, null, null, 9).Page);
            Assert.Equal(1, TodoService.Filter(items, TodoStatus.Done, null, null, -4).Page);
        }

        [Fact]
        public void Search_looks_in_titles_and_notes_ignoring_case()
        {
            var noted = Item(Report, "Call", TodoStatus.NotStarted, null);
            noted.Notes = "About the BUDGET";

            var items = new List<TodoItem>
            {
                Item(Report, "Draft budget", TodoStatus.Done, null),
                noted,
                Item(Report, "Lunch", TodoStatus.NotStarted, null)
            };

            Assert.Equal(new[] { "Draft budget", "Call" },
                TodoService.Filter(items, null, " budget ", null, 1).Items.Select(t => t.Title));
        }

        [Fact]
        public void Sorting_by_due_date_puts_undated_tasks_last_either_way()
        {
            var items = new List<TodoItem>
            {
                Item(Report, "Undated", TodoStatus.NotStarted, null),
                Item(Report, "Later", TodoStatus.NotStarted, Today.AddDays(5)),
                Item(Report, "Sooner", TodoStatus.NotStarted, Today.AddDays(1))
            };

            Assert.Equal(new[] { "Sooner", "Later", "Undated" },
                TodoService.Filter(items, null, null, "due", 1).Items.Select(t => t.Title));
            Assert.Equal(new[] { "Later", "Sooner", "Undated" },
                TodoService.Filter(items, null, null, "due-desc", 1).Items.Select(t => t.Title));
            Assert.Equal(new[] { "Later", "Sooner", "Undated" },
                TodoService.Filter(items, null, null, "title", 1).Items.Select(t => t.Title));
        }

        [Fact]
        public void Due_soon_counts_open_tasks_due_in_the_next_seven_days()
        {
            var items = new List<TodoItem>
            {
                Item(Report, "Today", TodoStatus.NotStarted, Today),
                Item(Report, "Week", TodoStatus.InProgress, Today.AddDays(7)),
                Item(Report, "Too far", TodoStatus.NotStarted, Today.AddDays(8)),
                Item(Report, "Late", TodoStatus.NotStarted, Today.AddDays(-1)),
                Item(Report, "Finished", TodoStatus.Done, Today.AddDays(2))
            };

            var counts = TodoService.Count(items, Today);

            Assert.Equal(2, counts.DueSoon);
            Assert.Equal(5, counts.Total);
        }
    }
}

using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    public sealed record TodoCounts(int NotStarted, int InProgress, int Done, int Overdue)
    {
        public int Open => NotStarted + InProgress;
    }

    public sealed record TeamMemberTodos(int EmployeeId, string Name, string? JobTitle, TodoCounts Counts);

    /// <summary>
    /// Everybody keeps their own to-do list. Their supervisor sees it,
    /// read only, so they know what their team is working on.
    /// </summary>
    public class TodoService
    {
        private readonly ApplicationDbContext _context;

        public TodoService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>Open items first, soonest due first, then what is done, latest first.</summary>
        public async Task<List<TodoItem>> ListAsync(int employeeId)
        {
            var items = await _context.TodoItems
                .AsNoTracking()
                .Where(t => t.EmployeeId == employeeId)
                .ToListAsync();

            return items
                .OrderBy(t => t.Status == TodoStatus.Done)
                .ThenBy(t => t.Status == TodoStatus.Done ? DateTime.MaxValue : t.DueDate ?? DateTime.MaxValue)
                .ThenByDescending(t => t.Status == TodoStatus.Done ? t.CompletedAt : null)
                .ThenBy(t => t.CreatedAt)
                .ToList();
        }

        public Task<bool> SupervisesAsync(int supervisorId, int employeeId) =>
            _context.Employees.AnyAsync(e => e.Id == employeeId && e.SupervisorId == supervisorId);

        /// <summary>Each active direct report and where their list stands.</summary>
        public async Task<List<TeamMemberTodos>> TeamAsync(int supervisorId, DateTime today)
        {
            var team = await _context.Employees
                .AsNoTracking()
                .Where(e => e.SupervisorId == supervisorId && e.IsActive)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.JobTitle })
                .ToListAsync();

            var ids = team.Select(e => e.Id).ToList();

            var items = await _context.TodoItems
                .AsNoTracking()
                .Where(t => ids.Contains(t.EmployeeId))
                .ToListAsync();

            return team
                .Select(e => new TeamMemberTodos(
                    e.Id,
                    e.Name,
                    e.JobTitle,
                    Count(items.Where(t => t.EmployeeId == e.Id), today)))
                .ToList();
        }

        public static TodoCounts Count(IEnumerable<TodoItem> items, DateTime today)
        {
            var list = items.ToList();

            return new TodoCounts(
                list.Count(t => t.Status == TodoStatus.NotStarted),
                list.Count(t => t.Status == TodoStatus.InProgress),
                list.Count(t => t.Status == TodoStatus.Done),
                list.Count(t => t.IsOverdue(today)));
        }

        public const int PageSize = 10;

        /// <summary>
        /// The items in one status group, a page at a time. With no group
        /// asked for, the first one that has anything in it.
        /// </summary>
        public static (TodoStatus Status, PagedResult<TodoItem> Page) Group(
            IReadOnlyList<TodoItem> items,
            TodoStatus? status,
            int page)
        {
            var chosen = status != null && Enum.IsDefined(status.Value)
                ? status.Value
                : Enum.GetValues<TodoStatus>()
                    .Where(s => items.Any(t => t.Status == s))
                    .DefaultIfEmpty(TodoStatus.NotStarted)
                    .First();

            var inGroup = items.Where(t => t.Status == chosen).ToList();
            var totalPages = (int)Math.Ceiling(inGroup.Count / (double)PageSize);

            page = Math.Clamp(page, 1, Math.Max(totalPages, 1));

            return (chosen, new PagedResult<TodoItem>
            {
                Items = inGroup.Skip((page - 1) * PageSize).Take(PageSize).ToList(),
                Page = page,
                PageSize = PageSize,
                TotalCount = inGroup.Count,
                TotalPages = totalPages
            });
        }

        /// <summary>Moves an item to a new status, noting when it was finished.</summary>
        public static void SetStatus(TodoItem item, TodoStatus status, DateTime now)
        {
            if (item.Status == status)
                return;

            item.Status = status;
            item.UpdatedAt = now;
            item.CompletedAt = status == TodoStatus.Done ? now : null;
        }
    }
}

using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// A personal to-do list. Only its owner can add to it or change
    /// it. Their supervisor can look at it, and nobody else.
    /// </summary>
    [Authorize]
    public class TodoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly TodoService _todos;
        private readonly CurrentUserService _currentUser;

        public TodoController(
            ApplicationDbContext context,
            TodoService todos,
            CurrentUserService currentUser)
        {
            _context = context;
            _todos = todos;
            _currentUser = currentUser;
        }

        public async Task<IActionResult> Index(TodoStatus? status, string? q, string? sort, int page = 1)
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
                return View("NoEmployeeRecord");

            var model = await ListAsync(me.Value, status, q, sort, page);
            model.CanEdit = true;
            model.New = new TodoItemViewModel();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add([Bind(Prefix = "New")] TodoItemViewModel model)
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
                return View("NoEmployeeRecord");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Give the task a title of up to 200 characters.";
                return RedirectToAction(nameof(Index));
            }

            var title = model.Title.Trim();

            _context.TodoItems.Add(new TodoItem
            {
                EmployeeId = me.Value,
                Title = title,
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                DueDate = model.DueDate?.Date,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"\"{title}\" added.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<TodoListViewModel> ListAsync(
            int employeeId, TodoStatus? status, string? search, string? sort, int page)
        {
            var items = await _todos.ListAsync(employeeId);

            if (status != null && !Enum.IsDefined(status.Value))
                status = null;

            if (sort == null || !TodoService.Sorts.ContainsKey(sort))
                sort = "due";

            return new TodoListViewModel
            {
                Counts = TodoService.Count(items, DateTime.Today),
                Status = status,
                Search = search?.Trim(),
                Sort = sort,
                Page = TodoService.Filter(items, status, search, sort, page)
            };
        }

        private async Task<TodoItem?> MineAsync(int id)
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            return me == null
                ? null
                : await _context.TodoItems.FirstOrDefaultAsync(t => t.Id == id && t.EmployeeId == me);
        }

        /// <summary>Back to the list as it was, with its tab, search and page.</summary>
        private IActionResult BackToList(string? returnUrl) =>
            returnUrl != null &&
            Url.IsLocalUrl(returnUrl) &&
            returnUrl.StartsWith("/Todo", StringComparison.OrdinalIgnoreCase)
                ? LocalRedirect(returnUrl)
                : RedirectToAction(nameof(Index));

        private static string Label(TodoStatus status) => status switch
        {
            TodoStatus.NotStarted => "Not started",
            TodoStatus.InProgress => "In progress",
            _ => "Completed"
        };

        public async Task<IActionResult> Edit(int id, string? returnUrl)
        {
            var item = await MineAsync(id);

            if (item == null)
                return NotFound();

            ViewBag.ReturnUrl = returnUrl;

            return View(new TodoItemViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Notes = item.Notes,
                DueDate = item.DueDate,
                Status = item.Status
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TodoItemViewModel model, string? returnUrl)
        {
            var item = await MineAsync(id);

            if (item == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            item.Title = model.Title.Trim();
            item.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
            item.DueDate = model.DueDate?.Date;
            item.UpdatedAt = DateTime.UtcNow;

            TodoService.SetStatus(item, model.Status, DateTime.UtcNow);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"\"{item.Title}\" saved.";

            return BackToList(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, TodoStatus status, string? returnUrl)
        {
            var item = await MineAsync(id);

            if (item == null)
                return NotFound();

            if (!ModelState.IsValid || !Enum.IsDefined(status))
                return BadRequest();

            TodoService.SetStatus(item, status, DateTime.UtcNow);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"\"{item.Title}\" moved to {Label(status)}.";

            return BackToList(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? returnUrl)
        {
            var item = await MineAsync(id);

            if (item == null)
                return NotFound();

            _context.TodoItems.Remove(item);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"\"{item.Title}\" removed.";

            return BackToList(returnUrl);
        }

        /// <summary>Starts, completes or removes the ticked tasks together.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Bulk(int[] ids, string bulkAction, string? returnUrl)
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
                return View("NoEmployeeRecord");

            var items = await _context.TodoItems
                .Where(t => ids.Contains(t.Id) && t.EmployeeId == me)
                .ToListAsync();

            if (items.Count == 0)
            {
                TempData["Error"] = "Tick at least one task first.";
                return BackToList(returnUrl);
            }

            var now = DateTime.UtcNow;
            var noun = items.Count == 1 ? "task" : "tasks";

            switch (bulkAction)
            {
                case "start":
                    foreach (var item in items.Where(t => t.Status != TodoStatus.InProgress))
                        TodoService.SetStatus(item, TodoStatus.InProgress, now);
                    TempData["Success"] = $"{items.Count} {noun} moved to In progress.";
                    break;

                case "done":
                    foreach (var item in items)
                        TodoService.SetStatus(item, TodoStatus.Done, now);
                    TempData["Success"] = $"{items.Count} {noun} moved to Completed.";
                    break;

                case "delete":
                    _context.TodoItems.RemoveRange(items);
                    TempData["Success"] = $"{items.Count} {noun} removed.";
                    break;

                default:
                    return BadRequest();
            }

            await _context.SaveChangesAsync();

            return BackToList(returnUrl);
        }

        /// <summary>A supervisor's view of the people who report to them.</summary>
        public async Task<IActionResult> Team()
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
                return View("NoEmployeeRecord");

            return View(await _todos.TeamAsync(me.Value, DateTime.Today));
        }

        /// <summary>One team member's list, read only, for their supervisor.</summary>
        public async Task<IActionResult> Member(int id, TodoStatus? status, string? q, string? sort, int page = 1)
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null || !await _todos.SupervisesAsync(me.Value, id))
                return NotFound();

            var person = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Id == id)
                .Select(e => new { Name = e.FirstName + " " + e.LastName, e.JobTitle })
                .FirstAsync();

            ViewBag.PersonName = person.Name;
            ViewBag.JobTitle = person.JobTitle;

            return View(await ListAsync(id, status, q, sort, page));
        }
    }
}

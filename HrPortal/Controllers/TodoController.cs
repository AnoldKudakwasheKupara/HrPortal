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

        public async Task<IActionResult> Index()
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
                return View("NoEmployeeRecord");

            return View(new TodoListViewModel
            {
                Items = await _todos.ListAsync(me.Value),
                CanEdit = true,
                New = new TodoItemViewModel()
            });
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
                TempData["Error"] = "Give the item a title of up to 200 characters.";
                return RedirectToAction(nameof(Index));
            }

            _context.TodoItems.Add(new TodoItem
            {
                EmployeeId = me.Value,
                Title = model.Title.Trim(),
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                DueDate = model.DueDate?.Date,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task<TodoItem?> MineAsync(int id)
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            return me == null
                ? null
                : await _context.TodoItems.FirstOrDefaultAsync(t => t.Id == id && t.EmployeeId == me);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var item = await MineAsync(id);

            if (item == null)
                return NotFound();

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
        public async Task<IActionResult> Edit(int id, TodoItemViewModel model)
        {
            var item = await MineAsync(id);

            if (item == null)
                return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            item.Title = model.Title.Trim();
            item.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
            item.DueDate = model.DueDate?.Date;
            item.UpdatedAt = DateTime.UtcNow;

            TodoService.SetStatus(item, model.Status, DateTime.UtcNow);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Saved.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, TodoStatus status)
        {
            var item = await MineAsync(id);

            if (item == null)
                return NotFound();

            if (!ModelState.IsValid || !Enum.IsDefined(status))
                return BadRequest();

            TodoService.SetStatus(item, status, DateTime.UtcNow);

            await _context.SaveChangesAsync();

            return Redirect(Url.Action(nameof(Index)) + $"#todo-{id}");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await MineAsync(id);

            if (item == null)
                return NotFound();

            _context.TodoItems.Remove(item);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"\"{item.Title}\" removed.";

            return RedirectToAction(nameof(Index));
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
        public async Task<IActionResult> Member(int id)
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

            return View(new TodoListViewModel
            {
                Items = await _todos.ListAsync(id),
                CanEdit = false
            });
        }
    }
}

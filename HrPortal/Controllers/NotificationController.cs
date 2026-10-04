using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationController(ApplicationDbContext context)
        {
            _context = context;
        }


        // ============================================================
        // INDEX
        // ============================================================

        public async Task<IActionResult> Index(
            int? employeeId,
            bool? isRead)
        {
            var query = _context.Notifications
                .Include(n => n.Employee)
                .AsQueryable();

            if (employeeId.HasValue)
            {
                query = query.Where(n =>
                    n.EmployeeId == employeeId.Value);
            }

            if (isRead.HasValue)
                query = query.Where(n => n.IsRead == isRead.Value);

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            await LoadEmployees();

            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.SelectedIsRead = isRead;

            return View(notifications);
        }


        // ============================================================
        // DETAILS
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var notification = await _context.Notifications
                .Include(n => n.Employee)
                    .ThenInclude(e => e.Department)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (notification == null)
                return NotFound();

            return View(notification);
        }


        // ============================================================
        // CREATE - GET
        // ============================================================

        public async Task<IActionResult> Create()
        {
            await LoadEmployees();

            return View(new NotificationCreateViewModel());
        }


        // ============================================================
        // CREATE - POST
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            NotificationCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadEmployees();
                return View(model);
            }

            var notification = new Notification
            {
                EmployeeId = model.EmployeeId,
                Title = model.Title.Trim(),
                Message = model.Message.Trim(),
                Link = model.Link?.Trim(),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Notification sent successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // EDIT - GET
        // ============================================================

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var notification = await _context.Notifications
                .FindAsync(id);

            if (notification == null)
                return NotFound();

            var model = new NotificationEditViewModel
            {
                Id = notification.Id,
                EmployeeId = notification.EmployeeId,
                Title = notification.Title,
                Message = notification.Message,
                Link = notification.Link,
                IsRead = notification.IsRead
            };

            await LoadEmployees();

            return View(model);
        }


        // ============================================================
        // EDIT - POST
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            NotificationEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadEmployees();
                return View(model);
            }

            var notification = await _context.Notifications
                .FindAsync(id);

            if (notification == null)
                return NotFound();

            notification.EmployeeId = model.EmployeeId;
            notification.Title = model.Title.Trim();
            notification.Message = model.Message.Trim();
            notification.Link = model.Link?.Trim();
            notification.IsRead = model.IsRead;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Notification updated successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // TOGGLE READ
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRead(int id)
        {
            var notification = await _context.Notifications
                .FindAsync(id);

            if (notification == null)
                return NotFound();

            notification.IsRead = !notification.IsRead;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                notification.IsRead
                    ? "Notification marked as read."
                    : "Notification marked as unread.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // DELETE - GET
        // ============================================================

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var notification = await _context.Notifications
                .Include(n => n.Employee)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (notification == null)
                return NotFound();

            return View(notification);
        }


        // ============================================================
        // DELETE - POST
        // ============================================================

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var notification = await _context.Notifications
                .FindAsync(id);

            if (notification == null)
                return NotFound();

            _context.Notifications.Remove(notification);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Notification deleted successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private async Task LoadEmployees()
        {
            var employees = await _context.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Select(e => new
                {
                    e.Id,
                    FullName = e.FirstName + " " + e.LastName
                })
                .ToListAsync();

            ViewBag.Employees =
                new SelectList(
                    employees,
                    "Id",
                    "FullName");
        }
    }
}

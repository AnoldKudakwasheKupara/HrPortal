using HrPortal.DbContexts;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize]
    public class MyNotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CurrentUserService _currentUser;

        public MyNotificationsController(
            ApplicationDbContext context,
            CurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<IActionResult> Index()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(n => n.EmployeeId == employeeId.Value)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }

        public async Task<IActionResult> Open(int id)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.Id == id &&
                    n.EmployeeId == employeeId.Value);

            if (notification == null)
                return NotFound();

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            if (!string.IsNullOrWhiteSpace(notification.Link) &&
                Url.IsLocalUrl(notification.Link))
            {
                return Redirect(notification.Link);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.Id == id &&
                    n.EmployeeId == employeeId.Value);

            if (notification == null)
                return NotFound();

            notification.IsRead = true;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var unread = await _context.Notifications
                .Where(n =>
                    n.EmployeeId == employeeId.Value &&
                    !n.IsRead)
                .ToListAsync();

            if (unread.Count == 0)
                return RedirectToAction(nameof(Index));

            foreach (var notification in unread)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{unread.Count} notification(s) marked as read.";

            return RedirectToAction(nameof(Index));
        }
    }
}

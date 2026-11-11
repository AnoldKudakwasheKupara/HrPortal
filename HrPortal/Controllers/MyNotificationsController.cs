using HrPortal.DbContexts;
using HrPortal.Services;
using HrPortal.ViewModels;
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

        public async Task<IActionResult> Index(string? show, int page = 1)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var mine = _context.Notifications
                .AsNoTracking()
                .Where(n => n.EmployeeId == employeeId.Value);

            var group = NotificationsIndexViewModel.Normalise(show);
            var weekAgo = DateTime.UtcNow.AddDays(-7);

            var shown = group switch
            {
                NotificationsIndexViewModel.Unread => mine.Where(n => !n.IsRead),
                NotificationsIndexViewModel.Read => mine.Where(n => n.IsRead),
                _ => mine
            };

            return View(new NotificationsIndexViewModel
            {
                Show = group,
                Total = await mine.CountAsync(),
                UnreadCount = await mine.CountAsync(n => !n.IsRead),
                ThisWeek = await mine.CountAsync(n => n.CreatedAt >= weekAgo),
                Page = await shown
                    .OrderByDescending(n => n.CreatedAt)
                    .ThenByDescending(n => n.Id)
                    .ToPagedResultAsync(page, PagedList.DefaultPageSize)
            });
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

using HrPortal.DbContexts;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// An employee's own employment information, the documents
    /// held on their file, and their notifications.
    ///
    /// Read only. Changes to employment information go through HR
    /// so the audit trail records who made them.
    /// </summary>
    [Authorize]
    public class MyProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FileStorageService _fileStorage;
        private readonly CurrentUserService _currentUser;

        public MyProfileController(
            ApplicationDbContext context,
            FileStorageService fileStorage,
            CurrentUserService currentUser)
        {
            _context = context;
            _fileStorage = fileStorage;
            _currentUser = currentUser;
        }


        // ============================================================
        // INDEX
        // ============================================================

        public async Task<IActionResult> Index()
        {
            var employee = await _currentUser.GetEmployeeAsync();

            if (employee == null)
                return View("NoEmployeeRecord");

            var model = new MyProfileViewModel
            {
                Employee = employee,

                Documents = await _context.EmployeeDocuments
                    .Include(d => d.DocumentCategory)
                    .Where(d => d.EmployeeId == employee.Id)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync(),

                Notifications = await _context.Notifications
                    .Where(n => n.EmployeeId == employee.Id)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(20)
                    .ToListAsync()
            };

            return View(model);
        }


        // ============================================================
        // DOWNLOAD OWN DOCUMENT
        // ============================================================

        public async Task<IActionResult> Document(int? id)
        {
            if (id == null)
                return NotFound();

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            // Scoped to the employee, so changing the id in the
            // address bar cannot reach someone else's file.
            var document = await _context.EmployeeDocuments
                .FirstOrDefaultAsync(d =>
                    d.Id == id &&
                    d.EmployeeId == employeeId.Value);

            if (document == null)
                return NotFound();

            var fullPath =
                _fileStorage.ResolveExistingPath(document.FilePath);

            if (fullPath == null)
            {
                TempData["Error"] =
                    "That file is no longer on the server.";

                return RedirectToAction(nameof(Index));
            }

            new FileExtensionContentTypeProvider()
                .TryGetContentType(fullPath, out var contentType);

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);

            return File(
                bytes,
                contentType ?? "application/octet-stream",
                document.OriginalFileName);
        }


        // ============================================================
        // MARK NOTIFICATION READ
        // ============================================================

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
    }
}

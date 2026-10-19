using HrPortal.DbContexts;
using HrPortal.Services;
using HrPortal.ViewModels;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize]
    public class MyProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FileStorageService _fileStorage;
        private readonly CurrentUserService _currentUser;
        private readonly IAuthorizationService _authorization;

        public MyProfileController(
            ApplicationDbContext context,
            FileStorageService fileStorage,
            CurrentUserService currentUser,
            IAuthorizationService authorization)
        {
            _context = context;
            _fileStorage = fileStorage;
            _currentUser = currentUser;
            _authorization = authorization;
        }

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
                    .Where(d =>
                        d.EmployeeId == employee.Id &&
                        !d.DocumentCategory.IsRestricted)
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

        public async Task<IActionResult> Document(int? id)
        {
            if (id == null)
                return NotFound();

            var document = await _context.EmployeeDocuments
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
                return NotFound();

            var allowed = await _authorization.AuthorizeAsync(
                User,
                document,
                Operations.View);

            if (!allowed.Succeeded)
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

            return PhysicalFile(
                fullPath,
                contentType ?? "application/octet-stream",
                document.OriginalFileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id);

            if (notification == null)
                return NotFound();

            var allowed = await _authorization.AuthorizeAsync(
                User,
                notification,
                Operations.Manage);

            if (!allowed.Succeeded)
                return NotFound();

            notification.IsRead = true;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}

using HrPortal.Data;
using HrPortal.Security;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Lets an administrator download a complete backup. Because the
    /// file holds everybody's personal data, taking one asks for the
    /// administrator's password again and goes in the audit log.
    /// </summary>
    [Authorize(Roles = Roles.AdminOnly)]
    public class BackupController : Controller
    {
        private readonly BackupService _backups;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _audit;
        private readonly ILogger<BackupController> _logger;

        public BackupController(
            BackupService backups,
            UserManager<ApplicationUser> userManager,
            AuditService audit,
            ILogger<BackupController> logger)
        {
            _backups = backups;
            _userManager = userManager;
            _audit = audit;
            _logger = logger;
        }

        public IActionResult Index()
        {
            ViewBag.Available = _backups.PgDumpPath() != null;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Download(string? password)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null ||
                string.IsNullOrEmpty(password) ||
                !await _userManager.CheckPasswordAsync(user, password))
            {
                TempData["Error"] =
                    "That password is not right, so no backup was made.";

                return RedirectToAction(nameof(Index));
            }

            FileStream backup;

            try
            {
                backup = await _backups.CreateAsync(HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The backup could not be made.");

                TempData["Error"] =
                    "The backup could not be made. The details are in the " +
                    "server log.";

                return RedirectToAction(nameof(Index));
            }

            await _audit.LogAsync(
                AuditService.BackupDownloaded,
                "Backup",
                null,
                $"Full backup downloaded ({backup.Length / 1024:N0} KB).");

            return File(
                backup,
                "application/zip",
                BackupService.FileName(DateTime.Now));
        }
    }
}

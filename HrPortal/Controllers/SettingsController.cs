using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrPortal.Controllers
{
    /// <summary>
    /// The settings an administrator can change without a deployment.
    ///
    /// They are written as configuration keys, so everything that reads
    /// them through IOptions keeps working unchanged, and the database
    /// simply layers over appsettings.json.
    /// </summary>
    [Authorize(Roles = Roles.AdminOnly)]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOptionsSnapshot<RequisitionOptions> _requisitions;
        private readonly IOptionsSnapshot<LeaveOptions> _leave;
        private readonly IOptionsSnapshot<ReminderOptions> _reminders;

        public SettingsController(
            ApplicationDbContext context,
            AuditService audit,
            UserManager<ApplicationUser> userManager,
            IOptionsSnapshot<RequisitionOptions> requisitions,
            IOptionsSnapshot<LeaveOptions> leave,
            IOptionsSnapshot<ReminderOptions> reminders)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
            _requisitions = requisitions;
            _leave = leave;
            _reminders = reminders;
        }

        public async Task<IActionResult> Index()
        {
            await LoadOverridesAsync();

            return View(Current());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadOverridesAsync();
                return View(model);
            }

            var by = await CurrentUserNameAsync();

            var changes = new List<string>();

            await SetAsync(
                "Requisitions:MdApprovalThreshold",
                model.MdApprovalThreshold.ToString("0.##"),
                by,
                changes);

            await SetAsync(
                "Requisitions:CurrencyCode",
                model.CurrencyCode.Trim(),
                by,
                changes);

            await SetAsync(
                "Leave:BackdateGraceDays",
                model.BackdateGraceDays.ToString(),
                by,
                changes);

            await SetAsync(
                "Reminders:Enabled",
                model.RemindersEnabled ? "true" : "false",
                by,
                changes);

            await SetAsync(
                "Reminders:RunAtHour",
                model.RunAtHour.ToString(),
                by,
                changes);

            await SetAsync(
                "Reminders:ExpiryWindowDays",
                model.ExpiryWindowDays.ToString(),
                by,
                changes);

            await SetAsync(
                "Reminders:RepeatEveryDays",
                model.RepeatEveryDays.ToString(),
                by,
                changes);

            await SetAsync(
                "Reminders:StaleApprovalDays",
                model.StaleApprovalDays.ToString(),
                by,
                changes);

            await _context.SaveChangesAsync();

            // Tell the configuration pipeline to re-read, so the new
            // values are in force on the next request rather than after
            // the next restart.
            DatabaseConfigurationProvider.ReloadAll();

            if (changes.Count > 0)
            {
                await _audit.LogAsync(
                    AuditService.Updated,
                    "Settings",
                    null,
                    string.Join("; ", changes));

                TempData["Success"] =
                    $"{changes.Count} setting(s) changed and in force now.";
            }
            else
            {
                TempData["Success"] = "Nothing was changed.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reset()
        {
            var stored = await _context.AppSettings.ToListAsync();

            if (stored.Count == 0)
            {
                TempData["Success"] =
                    "Nothing to reset: every setting already comes from " +
                    "the configuration file.";

                return RedirectToAction(nameof(Index));
            }

            _context.AppSettings.RemoveRange(stored);

            await _context.SaveChangesAsync();

            DatabaseConfigurationProvider.ReloadAll();

            await _audit.LogAsync(
                AuditService.Deleted,
                "Settings",
                null,
                $"{stored.Count} setting override(s) removed, falling " +
                "back to the configuration file.");

            TempData["Success"] =
                $"{stored.Count} override(s) removed. The settings in " +
                "appsettings.json apply again.";

            return RedirectToAction(nameof(Index));
        }

        private SettingsViewModel Current()
        {
            var requisitions = _requisitions.Value;
            var leave = _leave.Value;
            var reminders = _reminders.Value;

            return new SettingsViewModel
            {
                MdApprovalThreshold = requisitions.MdApprovalThreshold,
                CurrencyCode = requisitions.CurrencyCode,
                BackdateGraceDays = leave.BackdateGraceDays,
                RemindersEnabled = reminders.Enabled,
                RunAtHour = reminders.RunAtHour,
                ExpiryWindowDays = reminders.ExpiryWindowDays,
                RepeatEveryDays = reminders.RepeatEveryDays,
                StaleApprovalDays = reminders.StaleApprovalDays
            };
        }

        /// <summary>
        /// Writes a setting, or removes the override when the value
        /// matches nothing worth storing. Records what changed so the
        /// audit entry says more than "settings updated".
        /// </summary>
        private async Task SetAsync(
            string key,
            string value,
            string? by,
            List<string> changes)
        {
            var existing = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            var before = existing?.Value;

            if (before == value)
                return;

            if (existing == null)
            {
                _context.AppSettings.Add(new AppSetting
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = by
                });
            }
            else
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = by;
            }

            changes.Add(
                before == null
                    ? $"{key} set to {value}"
                    : $"{key} changed from {before} to {value}");
        }

        private async Task LoadOverridesAsync()
        {
            ViewBag.Overrides = await _context.AppSettings
                .AsNoTracking()
                .OrderBy(s => s.Key)
                .ToListAsync();
        }

        private async Task<string?> CurrentUserNameAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            return user?.FullName ?? user?.Email ?? User.Identity?.Name;
        }
    }
}

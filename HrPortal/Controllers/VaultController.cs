using System.Security.Claims;
using System.Security.Cryptography;
using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// The IT team's shared passwords. Administrators only, behind a
    /// second password check, with every reveal, change and removal
    /// written to the audit log. The passwords themselves are never
    /// logged and never sent to the browser until somebody asks to
    /// see one.
    /// </summary>
    [Authorize(Roles = Roles.AdminOnly)]
    public class VaultController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly VaultCipher _cipher;
        private readonly VaultLock _lock;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _audit;
        private readonly CurrentUserService _currentUser;

        public VaultController(
            ApplicationDbContext context,
            VaultCipher cipher,
            VaultLock vaultLock,
            UserManager<ApplicationUser> userManager,
            AuditService audit,
            CurrentUserService currentUser)
        {
            _context = context;
            _cipher = cipher;
            _lock = vaultLock;
            _userManager = userManager;
            _audit = audit;
            _currentUser = currentUser;
        }

        private int UserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>
        /// Every page but the unlock page needs the key set up and
        /// the vault open. Nothing from the vault is ever cached.
        /// </summary>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            Response.Headers.CacheControl = "no-store";

            var action = context.ActionDescriptor.RouteValues["action"];

            if (!_cipher.IsReady)
            {
                context.Result = View("NotSetUp");
                return;
            }

            if (action is nameof(Unlock) or nameof(Lock))
                return;

            if (!_lock.IsOpen(UserId))
            {
                context.Result = action == nameof(Reveal)
                    ? new StatusCodeResult(StatusCodes.Status401Unauthorized)
                    : RedirectToAction(nameof(Unlock), new
                    {
                        returnUrl = Request.Path + Request.QueryString
                    });
            }
        }

        public IActionResult Unlock(string? returnUrl)
        {
            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName(nameof(Unlock))]
        public async Task<IActionResult> UnlockPost(
            string? password,
            string? returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null ||
                string.IsNullOrEmpty(password) ||
                !await _userManager.CheckPasswordAsync(user, password))
            {
                await _audit.LogAsync(
                    AuditService.VaultUnlockFailed,
                    nameof(VaultEntry),
                    null,
                    "Wrong password given to open the password vault.");

                ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
                ViewBag.Error = "That password is not right.";

                return View();
            }

            _lock.Open(UserId);

            await _audit.LogAsync(
                AuditService.VaultOpened,
                nameof(VaultEntry),
                null,
                "Password vault opened.");

            if (Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Lock()
        {
            _lock.Close(UserId);

            TempData["Success"] = "The password vault is locked.";

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Index(string? search, string? category)
        {
            var entries = _context.VaultEntries.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";

                entries = entries.Where(e =>
                    EF.Functions.ILike(e.Name, term) ||
                    (e.Username != null && EF.Functions.ILike(e.Username, term)) ||
                    (e.Url != null && EF.Functions.ILike(e.Url, term)));
            }

            if (!string.IsNullOrWhiteSpace(category))
                entries = entries.Where(e => e.Category == category);

            ViewBag.Search = search;
            ViewBag.CategoryFilter = category;
            ViewBag.Categories = await Categories();

            return View(await entries
                .OrderBy(e => e.Category)
                .ThenBy(e => e.Name)
                .ToListAsync());
        }

        /// <summary>
        /// The one way a password leaves the server: asked for by a
        /// button on the page, answered only to an open vault, and
        /// logged each time.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reveal(int id)
        {
            var entry = await _context.VaultEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null)
                return NotFound();

            await _audit.LogAsync(
                AuditService.VaultPasswordViewed,
                nameof(VaultEntry),
                entry.Id.ToString(),
                $"Password for {entry.Name} revealed.");

            try
            {
                return Json(new
                {
                    password = _cipher.Decrypt(entry.EncryptedPassword),
                    notes = entry.EncryptedNotes == null
                        ? null
                        : _cipher.Decrypt(entry.EncryptedNotes)
                });
            }
            catch (CryptographicException)
            {
                return StatusCode(
                    StatusCodes.Status409Conflict,
                    WrongKey(entry));
            }
        }

        /// <summary>
        /// What to say when a stored value will not decrypt, which
        /// only happens when Vault:Key has been changed since it was
        /// saved.
        /// </summary>
        private static string WrongKey(VaultEntry entry) =>
            $"{entry.Name} was saved under a different vault key and " +
            "cannot be read with the current one. Put the original " +
            "Vault__Key back, or store the password again.";

        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await Categories();

            return View(new VaultEntryViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VaultEntryViewModel model)
        {
            if (string.IsNullOrEmpty(model.Password))
            {
                ModelState.AddModelError(
                    nameof(model.Password),
                    "Enter the password to store.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await Categories();
                return View(model);
            }

            var entry = new VaultEntry
            {
                Name = model.Name.Trim(),
                Category = Tidy(model.Category),
                Username = Tidy(model.Username),
                Url = Tidy(model.Url),
                EncryptedPassword = _cipher.Encrypt(model.Password!),
                EncryptedNotes = string.IsNullOrWhiteSpace(model.Notes)
                    ? null
                    : _cipher.Encrypt(model.Notes.Trim()),
                PasswordChangedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedByName = await _currentUser.GetDisplayNameAsync()
            };

            _context.VaultEntries.Add(entry);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(VaultEntry),
                entry.Id.ToString(),
                $"{entry.Name} added to the password vault.");

            TempData["Success"] = $"{entry.Name} added to the vault.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var entry = await _context.VaultEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null)
                return NotFound();

            if (entry.EncryptedNotes != null)
            {
                await _audit.LogAsync(
                    AuditService.VaultPasswordViewed,
                    nameof(VaultEntry),
                    entry.Id.ToString(),
                    $"Notes for {entry.Name} shown for editing.");
            }

            string? notes;

            try
            {
                notes = entry.EncryptedNotes == null
                    ? null
                    : _cipher.Decrypt(entry.EncryptedNotes);
            }
            catch (CryptographicException)
            {
                TempData["Error"] = WrongKey(entry);

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = await Categories();
            ViewBag.PasswordChangedAt = entry.PasswordChangedAt;

            return View(new VaultEntryViewModel
            {
                Id = entry.Id,
                Name = entry.Name,
                Category = entry.Category,
                Username = entry.Username,
                Url = entry.Url,
                Notes = notes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, VaultEntryViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            var entry = await _context.VaultEntries.FindAsync(id);

            if (entry == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await Categories();
                ViewBag.PasswordChangedAt = entry.PasswordChangedAt;
                return View(model);
            }

            var passwordChanged = !string.IsNullOrEmpty(model.Password);

            entry.Name = model.Name.Trim();
            entry.Category = Tidy(model.Category);
            entry.Username = Tidy(model.Username);
            entry.Url = Tidy(model.Url);
            entry.EncryptedNotes = string.IsNullOrWhiteSpace(model.Notes)
                ? null
                : _cipher.Encrypt(model.Notes.Trim());

            if (passwordChanged)
            {
                entry.EncryptedPassword = _cipher.Encrypt(model.Password!);
                entry.PasswordChangedAt = DateTime.UtcNow;
            }

            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedByName = await _currentUser.GetDisplayNameAsync();

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(VaultEntry),
                entry.Id.ToString(),
                $"{entry.Name} updated in the password vault" +
                (passwordChanged ? ", with a new password." : "."));

            TempData["Success"] = $"{entry.Name} updated.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var entry = await _context.VaultEntries.FindAsync(id);

            if (entry == null)
                return NotFound();

            _context.VaultEntries.Remove(entry);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(VaultEntry),
                entry.Id.ToString(),
                $"{entry.Name} removed from the password vault.");

            TempData["Success"] = $"{entry.Name} removed from the vault.";

            return RedirectToAction(nameof(Index));
        }

        private Task<List<string>> Categories() =>
            _context.VaultEntries
                .AsNoTracking()
                .Where(e => e.Category != null)
                .Select(e => e.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

        private static string? Tidy(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

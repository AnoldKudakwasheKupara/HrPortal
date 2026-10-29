using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = AudienceRules.Curators)]
    public class RepositoryFolderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;

        public RepositoryFolderController(
            ApplicationDbContext context,
            AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index()
        {
            var folders = await _context.RepositoryFolders
                .AsNoTracking()
                .OrderBy(f => f.Name)
                .Select(f => new RepositoryFolderViewModel
                {
                    Id = f.Id,
                    Name = f.Name,
                    Description = f.Description,
                    Audience = f.Audience,
                    DocumentCount = f.Documents.Count
                })
                .ToListAsync();

            return View(folders);
        }

        public IActionResult Create() =>
            View(new RepositoryFolderEditViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            RepositoryFolderEditViewModel model)
        {
            await ValidateAsync(model, null);

            if (!ModelState.IsValid)
                return View(model);

            var folder = new RepositoryFolder
            {
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                Audience = model.Audience,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.RepositoryFolders.Add(folder);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(RepositoryFolder),
                folder.Id.ToString(),
                $"Folder {folder.Name} created, visible to " +
                $"{folder.Audience}.");

            TempData["Success"] = "Folder created.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var folder = await _context.RepositoryFolders.FindAsync(id);

            if (folder == null)
                return NotFound();

            return View(new RepositoryFolderEditViewModel
            {
                Id = folder.Id,
                Name = folder.Name,
                Description = folder.Description,
                Audience = folder.Audience,
                IsActive = folder.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            RepositoryFolderEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            await ValidateAsync(model, id);

            if (!ModelState.IsValid)
                return View(model);

            var folder = await _context.RepositoryFolders.FindAsync(id);

            if (folder == null)
                return NotFound();

            var audienceWas = folder.Audience;

            folder.Name = model.Name.Trim();
            folder.Description = model.Description?.Trim();
            folder.Audience = model.Audience;
            folder.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            var note = audienceWas == model.Audience
                ? $"Folder {folder.Name} updated."
                : $"Folder {folder.Name} moved from {audienceWas} to " +
                  $"{model.Audience}, changing who can see everything " +
                  "in it.";

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(RepositoryFolder),
                folder.Id.ToString(),
                note);

            TempData["Success"] = "Folder updated.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var folder = await _context.RepositoryFolders
                .Include(f => f.Documents)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (folder == null)
                return NotFound();

            if (folder.Documents.Count > 0)
            {
                TempData["Error"] =
                    $"{folder.Documents.Count} document(s) are in this " +
                    "folder, so it cannot be deleted. Empty it first, or " +
                    "take it out of use.";

                return RedirectToAction(nameof(Index));
            }

            _context.RepositoryFolders.Remove(folder);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(RepositoryFolder),
                folder.Id.ToString(),
                $"Folder {folder.Name} deleted.");

            TempData["Success"] = "Folder deleted.";

            return RedirectToAction(nameof(Index));
        }

        private async Task ValidateAsync(
            RepositoryFolderEditViewModel model,
            int? excludeId)
        {
            var name = model.Name.Trim();

            var clash = await _context.RepositoryFolders
                .AnyAsync(f =>
                    f.Name.ToLower() == name.ToLower() &&
                    (excludeId == null || f.Id != excludeId.Value));

            if (clash)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "There is already a folder with that name.");
            }
        }
    }
}

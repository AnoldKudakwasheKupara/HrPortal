using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize]
    public class RepositoryController : Controller
    {
        private const string UploadFolder = "repository";

        private readonly ApplicationDbContext _context;
        private readonly FileStorageService _fileStorage;
        private readonly AuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;

        public RepositoryController(
            ApplicationDbContext context,
            FileStorageService fileStorage,
            AuditService audit,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _fileStorage = fileStorage;
            _audit = audit;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int? folderId, string? search)
        {
            var audiences = AudienceRules.Visible(User);

            var folders = await _context.RepositoryFolders
                .AsNoTracking()
                .Where(f => f.IsActive && audiences.Contains(f.Audience))
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

            var documents = _context.RepositoryDocuments
                .AsNoTracking()
                .Include(d => d.Folder)
                .Where(d => audiences.Contains(d.Folder.Audience) &&
                            d.Folder.IsActive);

            if (folderId.HasValue)
            {
                documents = documents
                    .Where(d => d.RepositoryFolderId == folderId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                documents = documents.Where(d =>
                    d.Title.Contains(term) ||
                    d.OriginalFileName.Contains(term) ||
                    (d.Description != null && d.Description.Contains(term)));
            }

            ViewBag.Folders = folders;
            ViewBag.FolderFilter = folderId;
            ViewBag.Search = search;
            ViewBag.CanCurate = User.IsInRole(Roles.Administrator) ||
                                User.IsInRole(Roles.HrOfficer);

            var rows = await documents
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            return View(rows);
        }

        public async Task<IActionResult> Download(int? id)
        {
            var document = await Readable(id);

            if (document == null)
                return NotFound();

            var fullPath = _fileStorage.ResolveExistingPath(document.FilePath);

            if (fullPath == null)
            {
                TempData["Error"] = "That file is no longer on the server.";
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

        public async Task<IActionResult> Preview(int? id)
        {
            var document = await Readable(id);

            if (document == null)
                return NotFound();

            if (!FileStorageService.CanPreview(document.OriginalFileName))
                return RedirectToAction(nameof(Download), new { id });

            var fullPath = _fileStorage.ResolveExistingPath(document.FilePath);

            if (fullPath == null)
            {
                TempData["Error"] = "That file is no longer on the server.";
                return RedirectToAction(nameof(Index));
            }

            new FileExtensionContentTypeProvider()
                .TryGetContentType(fullPath, out var contentType);

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);

            Response.Headers.ContentDisposition =
                "inline; filename=\"" +
                Path.GetFileName(document.OriginalFileName) + "\"";

            return File(bytes, contentType ?? "application/octet-stream");
        }

        [Authorize(Roles = AudienceRules.Curators)]
        public async Task<IActionResult> Upload(int? folderId)
        {
            await LoadFolders();

            return View(new RepositoryUploadViewModel
            {
                RepositoryFolderId = folderId ?? 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AudienceRules.Curators)]
        public async Task<IActionResult> Upload(RepositoryUploadViewModel model)
        {
            var fileError = FileStorageService.Validate(model.File);

            if (fileError != null)
                ModelState.AddModelError(nameof(model.File), fileError);

            if (!await _context.RepositoryFolders
                    .AnyAsync(f => f.Id == model.RepositoryFolderId))
            {
                ModelState.AddModelError(
                    nameof(model.RepositoryFolderId),
                    "Choose a folder to put it in.");
            }

            if (!ModelState.IsValid)
            {
                await LoadFolders();
                return View(model);
            }

            var (fileName, filePath) =
                await _fileStorage.SaveAsync(model.File!, UploadFolder);

            var document = new RepositoryDocument
            {
                RepositoryFolderId = model.RepositoryFolderId,
                Title = model.Title.Trim(),
                Description = model.Description?.Trim(),
                FileName = fileName,
                OriginalFileName = Path.GetFileName(model.File!.FileName),
                FilePath = filePath,
                FileSizeBytes = model.File.Length,
                ExpiryDate = model.ExpiryDate,
                UploadedAt = DateTime.UtcNow,
                UploadedByName = await CurrentUserNameAsync()
            };

            _context.RepositoryDocuments.Add(document);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.DocumentUploaded,
                nameof(RepositoryDocument),
                document.Id.ToString(),
                $"{document.Title} added to the document repository.");

            TempData["Success"] = "Document added.";

            return RedirectToAction(
                nameof(Index),
                new { folderId = model.RepositoryFolderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AudienceRules.Curators)]
        public async Task<IActionResult> Delete(int id)
        {
            var document = await _context.RepositoryDocuments
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
                return NotFound();

            var folderId = document.RepositoryFolderId;
            var filePath = document.FilePath;

            _context.RepositoryDocuments.Remove(document);

            await _context.SaveChangesAsync();

            _fileStorage.Delete(filePath);

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(RepositoryDocument),
                document.Id.ToString(),
                $"{document.Title} removed from the document repository.");

            TempData["Success"] = "Document removed.";

            return RedirectToAction(nameof(Index), new { folderId });
        }

        private async Task<RepositoryDocument?> Readable(int? id)
        {
            if (id == null)
                return null;

            var document = await _context.RepositoryDocuments
                .AsNoTracking()
                .Include(d => d.Folder)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
                return null;

            if (!document.Folder.IsActive)
                return null;

            return AudienceRules.CanSee(User, document.Folder.Audience)
                ? document
                : null;
        }

        private async Task LoadFolders()
        {
            ViewBag.FolderOptions = await _context.RepositoryFolders
                .AsNoTracking()
                .Where(f => f.IsActive)
                .OrderBy(f => f.Name)
                .ToListAsync();
        }

        private async Task<string?> CurrentUserNameAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            return user?.FullName ?? user?.Email ?? User.Identity?.Name;
        }
    }
}

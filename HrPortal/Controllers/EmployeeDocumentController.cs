using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.HrOnly)]
    public class EmployeeDocumentController : Controller
    {
        private const string UploadFolder = "employee-documents";

        private const int PageSize = 50;

        private readonly ApplicationDbContext _context;
        private readonly FileStorageService _fileStorage;
        private readonly CurrentUserService _currentUser;
        private readonly AuditService _audit;
        private readonly RecordScope _scope;

        public EmployeeDocumentController(
            ApplicationDbContext context,
            FileStorageService fileStorage,
            CurrentUserService currentUser,
            AuditService audit,
            RecordScope scope)
        {
            _context = context;
            _fileStorage = fileStorage;
            _currentUser = currentUser;
            _audit = audit;
            _scope = scope;
        }

        public async Task<IActionResult> Index(
            int? employeeId,
            int? categoryId,
            int page = 1)
        {
            var query = await _scope.VisibleToAsync(
                _context.EmployeeDocuments
                    .AsNoTracking()
                    .Include(d => d.Employee)
                    .Include(d => d.DocumentCategory)
                    .Include(d => d.UploadedByEmployee),
                User);

            if (employeeId.HasValue)
            {
                query = query.Where(d =>
                    d.EmployeeId == employeeId.Value);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(d =>
                    d.DocumentCategoryId == categoryId.Value);
            }

            var documents = await query
                .OrderByDescending(d => d.UploadedAt)
                .ToPagedResultAsync(page, PageSize);

            await LoadLookups();

            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.SelectedCategoryId = categoryId;

            return View(documents);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var document = await _context.EmployeeDocuments
                .AsNoTracking()
                .Include(d => d.Employee)
                    .ThenInclude(e => e.Department)
                .Include(d => d.DocumentCategory)
                .Include(d => d.UploadedByEmployee)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
                return NotFound();

            return View(document);
        }

        public async Task<IActionResult> Download(int? id)
        {
            if (id == null)
                return NotFound();

            var document = await _context.EmployeeDocuments
                .FindAsync(id);

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

            return PhysicalFile(
                fullPath,
                contentType ?? "application/octet-stream",
                document.OriginalFileName);
        }

        public async Task<IActionResult> Preview(int? id)
        {
            if (id == null)
                return NotFound();

            var document = await _context.EmployeeDocuments
                .FindAsync(id);

            if (document == null)
                return NotFound();

            if (!FileStorageService.CanPreview(document.OriginalFileName))
                return RedirectToAction(nameof(Download), new { id });

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

            Response.Headers.ContentDisposition =
                "inline; filename=\"" +
                Path.GetFileName(document.OriginalFileName) +
                "\"";

            return PhysicalFile(
                fullPath,
                contentType ?? "application/octet-stream");
        }

        public async Task<IActionResult> Create(int? employeeId)
        {
            await LoadLookups();

            return View(new EmployeeDocumentCreateViewModel
            {
                EmployeeId = employeeId ?? 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            EmployeeDocumentCreateViewModel model)
        {
            var fileError = FileStorageService.Validate(model.File);

            if (fileError != null)
                ModelState.AddModelError("File", fileError);

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var (fileName, filePath) =
                await _fileStorage.SaveAsync(
                    model.File!,
                    UploadFolder);

            var document = new EmployeeDocument
            {
                EmployeeId = model.EmployeeId,
                DocumentCategoryId = model.DocumentCategoryId,
                FileName = fileName,
                OriginalFileName =
                    Path.GetFileName(model.File!.FileName),
                FilePath = filePath,
                Description = model.Description?.Trim(),
                ExpiryDate = model.ExpiryDate,
                UploadedAt = DateTime.UtcNow,
                UploadedByEmployeeId = await _currentUser.GetEmployeeIdAsync(),
                UploadedByName = await _currentUser.GetDisplayNameAsync()
            };

            _context.EmployeeDocuments.Add(document);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.DocumentUploaded,
                nameof(EmployeeDocument),
                document.Id.ToString(),
                $"{document.OriginalFileName} filed against " +
                $"employee {document.EmployeeId}.");

            TempData["Success"] =
                "Document uploaded successfully.";

            return RedirectToAction(
                nameof(Index),
                new { employeeId = model.EmployeeId });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var document = await _context.EmployeeDocuments
                .FindAsync(id);

            if (document == null)
                return NotFound();

            var model = new EmployeeDocumentEditViewModel
            {
                Id = document.Id,
                EmployeeId = document.EmployeeId,
                DocumentCategoryId = document.DocumentCategoryId,
                CurrentFileName = document.OriginalFileName,
                Description = document.Description,
                ExpiryDate = document.ExpiryDate
            };

            await LoadLookups();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            EmployeeDocumentEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (model.File != null)
            {
                var fileError = FileStorageService.Validate(model.File);

                if (fileError != null)
                    ModelState.AddModelError("File", fileError);
            }

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var document = await _context.EmployeeDocuments
                .FindAsync(id);

            if (document == null)
                return NotFound();

            if (model.File != null)
            {
                var oldFilePath = document.FilePath;

                var (fileName, filePath) =
                    await _fileStorage.SaveAsync(
                        model.File,
                        UploadFolder);

                document.FileName = fileName;
                document.OriginalFileName =
                    Path.GetFileName(model.File.FileName);
                document.FilePath = filePath;
                document.UploadedAt = DateTime.UtcNow;
                document.UploadedByEmployeeId =
                    await _currentUser.GetEmployeeIdAsync();
                document.UploadedByName =
                    await _currentUser.GetDisplayNameAsync();

                _fileStorage.Delete(oldFilePath);
            }

            document.EmployeeId = model.EmployeeId;
            document.DocumentCategoryId = model.DocumentCategoryId;
            document.Description = model.Description?.Trim();
            document.ExpiryDate = model.ExpiryDate;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Document updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var document = await _context.EmployeeDocuments
                .AsNoTracking()
                .Include(d => d.Employee)
                .Include(d => d.DocumentCategory)
                .Include(d => d.UploadedByEmployee)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
                return NotFound();

            return View(document);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var document = await _context.EmployeeDocuments
                .FindAsync(id);

            if (document == null)
                return NotFound();

            var filePath = document.FilePath;

            _context.EmployeeDocuments.Remove(document);

            await _context.SaveChangesAsync();

            _fileStorage.Delete(filePath);

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(EmployeeDocument),
                document.Id.ToString(),
                $"{document.OriginalFileName} removed.");

            TempData["Success"] =
                "Document deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadLookups()
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

            var categories = await _context.DocumentCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories =
                new SelectList(
                    categories,
                    "Id",
                    "Name");
        }
    }
}

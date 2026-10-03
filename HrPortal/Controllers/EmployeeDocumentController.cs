using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class EmployeeDocumentController : Controller
    {
        private const string UploadFolder = "employee-documents";

        private readonly ApplicationDbContext _context;
        private readonly FileStorageService _fileStorage;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeeDocumentController(
            ApplicationDbContext context,
            FileStorageService fileStorage,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _fileStorage = fileStorage;
            _userManager = userManager;
        }


        // ============================================================
        // INDEX
        // ============================================================

        public async Task<IActionResult> Index(
            int? employeeId,
            int? categoryId)
        {
            var query = _context.EmployeeDocuments
                .Include(d => d.Employee)
                .Include(d => d.DocumentCategory)
                .Include(d => d.UploadedByEmployee)
                .AsQueryable();

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
                .ToListAsync();

            await LoadLookups();

            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.SelectedCategoryId = categoryId;

            return View(documents);
        }


        // ============================================================
        // DETAILS
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var document = await _context.EmployeeDocuments
                .Include(d => d.Employee)
                    .ThenInclude(e => e.Department)
                .Include(d => d.DocumentCategory)
                .Include(d => d.UploadedByEmployee)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
                return NotFound();

            return View(document);
        }


        // ============================================================
        // DOWNLOAD
        // ============================================================

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

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);

            return File(
                bytes,
                contentType ?? "application/octet-stream",
                document.OriginalFileName);
        }


        // ============================================================
        // CREATE - GET
        // ============================================================

        public async Task<IActionResult> Create(int? employeeId)
        {
            await LoadLookups();

            return View(new EmployeeDocumentCreateViewModel
            {
                EmployeeId = employeeId ?? 0,
                UploadedByEmployeeId =
                    await GetCurrentEmployeeId() ?? 0
            });
        }


        // ============================================================
        // CREATE - POST
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            EmployeeDocumentCreateViewModel model)
        {
            var fileError = FileStorageService.Validate(model.File);

            if (fileError != null)
                ModelState.AddModelError("File", fileError);

            if (model.ExpiryDate.HasValue &&
                model.ExpiryDate.Value.Date < DateTime.Today)
            {
                ModelState.AddModelError(
                    "ExpiryDate",
                    "The expiry date cannot be in the past.");
            }

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
                UploadedAt = DateTime.UtcNow,
                UploadedByEmployeeId = model.UploadedByEmployeeId,
                ExpiryDate = model.ExpiryDate
            };

            _context.EmployeeDocuments.Add(document);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Document uploaded successfully.";

            return RedirectToAction(
                nameof(Index),
                new { employeeId = model.EmployeeId });
        }


        // ============================================================
        // EDIT - GET
        // ============================================================

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
                ExpiryDate = document.ExpiryDate,
                UploadedByEmployeeId = document.UploadedByEmployeeId
            };

            await LoadLookups();

            return View(model);
        }


        // ============================================================
        // EDIT - POST
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            EmployeeDocumentEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            // Replacing the file is optional on edit.
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

                // Only drop the old file once the new one is stored.
                _fileStorage.Delete(oldFilePath);
            }

            document.EmployeeId = model.EmployeeId;
            document.DocumentCategoryId = model.DocumentCategoryId;
            document.Description = model.Description?.Trim();
            document.ExpiryDate = model.ExpiryDate;
            document.UploadedByEmployeeId = model.UploadedByEmployeeId;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Document updated successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // DELETE - GET
        // ============================================================

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var document = await _context.EmployeeDocuments
                .Include(d => d.Employee)
                .Include(d => d.DocumentCategory)
                .Include(d => d.UploadedByEmployee)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
                return NotFound();

            return View(document);
        }


        // ============================================================
        // DELETE - POST
        // ============================================================

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

            // Remove the file only after the row is gone, so a
            // failed save never leaves a row pointing at nothing.
            _fileStorage.Delete(filePath);

            TempData["Success"] =
                "Document deleted successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // HELPERS
        // ============================================================

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

        private async Task<int?> GetCurrentEmployeeId()
        {
            var userId = _userManager.GetUserId(User);

            if (!int.TryParse(userId, out var parsedUserId))
                return null;

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == parsedUserId);

            return employee?.Id;
        }
    }
}

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
    [Authorize(Roles = Roles.AdminOrHr)]
    public class TrainingMaterialController : Controller
    {
        private const string UploadFolder = "training-materials";

        private readonly ApplicationDbContext _context;
        private readonly FileStorageService _fileStorage;
        private readonly UserManager<ApplicationUser> _userManager;

        public TrainingMaterialController(
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

        public async Task<IActionResult> Index(int? courseId)
        {
            var query = _context.TrainingMaterials
                .Include(m => m.TrainingCourse)
                .Include(m => m.UploadedByEmployee)
                .AsQueryable();

            if (courseId.HasValue)
            {
                query = query.Where(m =>
                    m.TrainingCourseId == courseId.Value);
            }

            var materials = await query
                .OrderByDescending(m => m.UploadedAt)
                .ToListAsync();

            await LoadCourses();

            ViewBag.SelectedCourseId = courseId;

            return View(materials);
        }


        // ============================================================
        // DETAILS
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var material = await _context.TrainingMaterials
                .Include(m => m.TrainingCourse)
                .Include(m => m.UploadedByEmployee)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null)
                return NotFound();

            return View(material);
        }


        // ============================================================
        // DOWNLOAD
        // ============================================================

        public async Task<IActionResult> Download(int? id)
        {
            if (id == null)
                return NotFound();

            var material = await _context.TrainingMaterials
                .FindAsync(id);

            if (material == null)
                return NotFound();

            var fullPath =
                _fileStorage.ResolveExistingPath(material.FilePath);

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
                material.OriginalFileName);
        }


        // ============================================================
        // CREATE - GET
        // ============================================================

        public async Task<IActionResult> Create(int? courseId)
        {
            await LoadCourses();
            await LoadEmployees();

            return View(new TrainingMaterialCreateViewModel
            {
                TrainingCourseId = courseId ?? 0,
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
            TrainingMaterialCreateViewModel model)
        {
            var fileError = FileStorageService.Validate(model.File);

            if (fileError != null)
                ModelState.AddModelError("File", fileError);

            if (!ModelState.IsValid)
            {
                await LoadCourses();
                await LoadEmployees();
                return View(model);
            }

            var (fileName, filePath) =
                await _fileStorage.SaveAsync(
                    model.File!,
                    UploadFolder);

            var material = new TrainingMaterial
            {
                TrainingCourseId = model.TrainingCourseId,
                FileName = fileName,
                OriginalFileName =
                    Path.GetFileName(model.File!.FileName),
                FilePath = filePath,
                Description = model.Description?.Trim(),
                UploadedAt = DateTime.UtcNow,
                UploadedByEmployeeId = model.UploadedByEmployeeId
            };

            _context.TrainingMaterials.Add(material);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Training material uploaded successfully.";

            return RedirectToAction(
                nameof(Index),
                new { courseId = model.TrainingCourseId });
        }


        // ============================================================
        // EDIT - GET
        // ============================================================

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var material = await _context.TrainingMaterials
                .FindAsync(id);

            if (material == null)
                return NotFound();

            var model = new TrainingMaterialEditViewModel
            {
                Id = material.Id,
                TrainingCourseId = material.TrainingCourseId,
                CurrentFileName = material.OriginalFileName,
                Description = material.Description,
                UploadedByEmployeeId = material.UploadedByEmployeeId
            };

            await LoadCourses();
            await LoadEmployees();

            return View(model);
        }


        // ============================================================
        // EDIT - POST
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            TrainingMaterialEditViewModel model)
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
                await LoadCourses();
                await LoadEmployees();
                return View(model);
            }

            var material = await _context.TrainingMaterials
                .FindAsync(id);

            if (material == null)
                return NotFound();

            if (model.File != null)
            {
                var oldFilePath = material.FilePath;

                var (fileName, filePath) =
                    await _fileStorage.SaveAsync(
                        model.File,
                        UploadFolder);

                material.FileName = fileName;
                material.OriginalFileName =
                    Path.GetFileName(model.File.FileName);
                material.FilePath = filePath;
                material.UploadedAt = DateTime.UtcNow;

                // Only drop the old file once the new one is stored.
                _fileStorage.Delete(oldFilePath);
            }

            material.TrainingCourseId = model.TrainingCourseId;
            material.Description = model.Description?.Trim();
            material.UploadedByEmployeeId = model.UploadedByEmployeeId;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Training material updated successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // DELETE - GET
        // ============================================================

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var material = await _context.TrainingMaterials
                .Include(m => m.TrainingCourse)
                .Include(m => m.UploadedByEmployee)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null)
                return NotFound();

            return View(material);
        }


        // ============================================================
        // DELETE - POST
        // ============================================================

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var material = await _context.TrainingMaterials
                .FindAsync(id);

            if (material == null)
                return NotFound();

            var filePath = material.FilePath;

            _context.TrainingMaterials.Remove(material);

            await _context.SaveChangesAsync();

            // Remove the file only after the row is gone, so a
            // failed save never leaves a row pointing at nothing.
            _fileStorage.Delete(filePath);

            TempData["Success"] =
                "Training material deleted successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private async Task LoadCourses()
        {
            var courses = await _context.TrainingCourses
                .OrderBy(c => c.Title)
                .ToListAsync();

            ViewBag.Courses =
                new SelectList(
                    courses,
                    "Id",
                    "Title");
        }

        private async Task LoadEmployees()
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

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
    /// <summary>
    /// Leading a course is a relationship, not a role: any employee
    /// may be the trainer on one, and the trainer is the person
    /// asked to post the material for it. The class guard asks only
    /// for a signed-in user, and every action settles for itself
    /// whether the person asking is HR or the trainer on the course
    /// in question.
    /// </summary>
    [Authorize]
    public class TrainingMaterialController : Controller
    {
        private const string UploadFolder = "training-materials";

        private readonly ApplicationDbContext _context;
        private readonly FileStorageService _fileStorage;
        private readonly CurrentUserService _currentUser;

        private readonly RecordDocumentPdfService _pdf;

        public TrainingMaterialController(
            ApplicationDbContext context,
            FileStorageService fileStorage,
            CurrentUserService currentUser,
            RecordDocumentPdfService pdf)
        {
            _context = context;
            _fileStorage = fileStorage;
            _currentUser = currentUser;
            _pdf = pdf;
        }

        private bool IsHr => User.IsInRole(Roles.HrOfficer);

        /// <summary>
        /// The courses the signed-in person is the trainer on.
        /// Empty for almost everybody, which is what keeps this
        /// page to HR and the few people leading something.
        /// </summary>
        private async Task<List<int>> CoursesILeadAsync()
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
                return new List<int>();

            return await _context.TrainingCourses
                .AsNoTracking()
                .Where(c => c.TrainerEmployeeId == me.Value)
                .Select(c => c.Id)
                .ToListAsync();
        }

        /// <summary>
        /// Whether the person asking has business with this course:
        /// HR, or the trainer leading it.
        /// </summary>
        private async Task<bool> MayPostToAsync(int courseId)
        {
            if (IsHr)
                return true;

            return (await CoursesILeadAsync()).Contains(courseId);
        }

        /// <summary>
        /// What is known about the material, as a printable record.
        /// This is the card, not the file -- Download still fetches
        /// the material itself.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var material = await _context.TrainingMaterials
                .AsNoTracking()
                .Include(m => m.TrainingCourse)
                .Include(m => m.UploadedByEmployee)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null ||
                !await MayPostToAsync(material.TrainingCourseId))
            {
                return NotFound();
            }

            var uploader = material.UploadedByEmployee;

            var record = new RecordDocument("Training Material")
            {
                Headline = material.OriginalFileName,
                Caption = material.TrainingCourse.Title
            };

            record.Section("Material", "The file held against the course")
                .Add("File name", material.OriginalFileName)
                .Add("Stored as", material.FileName);

            record.Section("Course", "What it belongs to")
                .Add("Title", material.TrainingCourse.Title)
                .Add("Trainer", material.TrainingCourse.TrainerName);

            record.Section("Provenance", "Who put it there, and when")
                .Add(
                    "Uploaded by",
                    uploader == null
                        ? null
                        : $"{uploader.FirstName} {uploader.LastName}")
                .AddStamp("Uploaded", material.UploadedAt);

            record.Note(material.Description);

            return File(
                _pdf.Render(record),
                "application/pdf",
                record.FileName);
        }

        public async Task<IActionResult> Index(int? courseId)
        {
            var mine = IsHr ? null : await CoursesILeadAsync();

            if (mine is { Count: 0 })
                return Forbid();

            var query = _context.TrainingMaterials
                .AsNoTracking()
                .Include(m => m.TrainingCourse)
                .Include(m => m.UploadedByEmployee)
                .AsQueryable();

            if (mine != null)
            {
                query = query.Where(m =>
                    mine.Contains(m.TrainingCourseId));
            }

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
            ViewBag.IsTrainer = mine != null;

            if (mine != null)
            {
                // The courses themselves, not just what has been
                // posted to them. A course with nothing posted yet
                // is the one the trainer most needs to see, and it
                // has no material to appear under.
                ViewBag.MyCourses = await _context.TrainingCourses
                    .AsNoTracking()
                    .Include(c => c.Materials)
                    .Where(c => mine.Contains(c.Id))
                    .OrderBy(c => c.TrainingDate ?? DateTime.MaxValue)
                    .ThenBy(c => c.Title)
                    .ToListAsync();
            }

            return View(materials);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var material = await _context.TrainingMaterials
                .AsNoTracking()
                .Include(m => m.TrainingCourse)
                .Include(m => m.UploadedByEmployee)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null ||
                !await MayPostToAsync(material.TrainingCourseId))
            {
                return NotFound();
            }

            return View(material);
        }

        public async Task<IActionResult> Download(int? id)
        {
            if (id == null)
                return NotFound();

            var material = await _context.TrainingMaterials
                .FindAsync(id);

            if (material == null ||
                !await MayPostToAsync(material.TrainingCourseId))
            {
                return NotFound();
            }

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

            return PhysicalFile(
                fullPath,
                contentType ?? "application/octet-stream",
                material.OriginalFileName);
        }

        public async Task<IActionResult> Create(int? courseId)
        {
            if (!IsHr && (await CoursesILeadAsync()).Count == 0)
                return Forbid();

            await LoadCourses();
            await LoadEmployees();

            return View(new TrainingMaterialCreateViewModel
            {
                TrainingCourseId = courseId ?? 0,
                UploadedByEmployeeId =
                    await _currentUser.GetEmployeeIdAsync() ?? 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            TrainingMaterialCreateViewModel model)
        {
            if (!await MayPostToAsync(model.TrainingCourseId))
                return Forbid();

            if (!IsHr)
            {
                // The trainer posts as themselves. The form offers
                // the choice to HR, who files material on somebody
                // else's behalf.
                model.UploadedByEmployeeId =
                    await _currentUser.GetEmployeeIdAsync() ?? 0;
            }

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

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var material = await _context.TrainingMaterials
                .FindAsync(id);

            if (material == null ||
                !await MayPostToAsync(material.TrainingCourseId))
            {
                return NotFound();
            }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            TrainingMaterialEditViewModel model)
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
                await LoadCourses();
                await LoadEmployees();
                return View(model);
            }

            var material = await _context.TrainingMaterials
                .FindAsync(id);

            if (material == null ||
                !await MayPostToAsync(material.TrainingCourseId))
            {
                return NotFound();
            }

            // Where it is being moved to is asked about as well as
            // where it is: otherwise a trainer could post material
            // onto somebody else's course by editing their own.
            if (!await MayPostToAsync(model.TrainingCourseId))
                return Forbid();

            if (!IsHr)
            {
                model.UploadedByEmployeeId =
                    material.UploadedByEmployeeId;
            }

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

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var material = await _context.TrainingMaterials
                .AsNoTracking()
                .Include(m => m.TrainingCourse)
                .Include(m => m.UploadedByEmployee)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null ||
                !await MayPostToAsync(material.TrainingCourseId))
            {
                return NotFound();
            }

            return View(material);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var material = await _context.TrainingMaterials
                .FindAsync(id);

            if (material == null ||
                !await MayPostToAsync(material.TrainingCourseId))
            {
                return NotFound();
            }

            var filePath = material.FilePath;

            _context.TrainingMaterials.Remove(material);

            await _context.SaveChangesAsync();

            _fileStorage.Delete(filePath);

            TempData["Success"] =
                "Training material deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadCourses()
        {
            var mine = IsHr ? null : await CoursesILeadAsync();

            var query = _context.TrainingCourses.AsQueryable();

            if (mine != null)
                query = query.Where(c => mine.Contains(c.Id));

            var courses = await query
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
    }
}

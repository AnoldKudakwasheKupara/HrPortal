using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.ViewModels;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.AdminOrHr)]
    public class DocumentCategoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DocumentCategoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _context.DocumentCategories
                .Include(c => c.Documents)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(categories);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var category = await _context.DocumentCategories
                .Include(c => c.Documents)
                    .ThenInclude(d => d.Employee)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return NotFound();

            return View(category);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            DocumentCategoryCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var name = model.Name.Trim();

            var exists = await _context.DocumentCategories
                .AnyAsync(c => c.Name.ToLower() == name.ToLower());

            if (exists)
            {
                ModelState.AddModelError(
                    "Name",
                    "A document category with this name already exists.");

                return View(model);
            }

            var category = new DocumentCategory
            {
                Name = name,
                Description = model.Description?.Trim(),
                IsRestricted = model.IsRestricted,
                IsActive = true
            };

            _context.DocumentCategories.Add(category);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Document category created successfully.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var category = await _context.DocumentCategories
                .FindAsync(id);

            if (category == null)
                return NotFound();

            var model = new DocumentCategoryEditViewModel
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                IsRestricted = category.IsRestricted,
                IsActive = category.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            DocumentCategoryEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            var category = await _context.DocumentCategories
                .FindAsync(id);

            if (category == null)
                return NotFound();

            var name = model.Name.Trim();

            var duplicateExists = await _context.DocumentCategories
                .AnyAsync(c =>
                    c.Id != id &&
                    c.Name.ToLower() == name.ToLower());

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    "Name",
                    "Another document category already uses this name.");

                return View(model);
            }

            category.Name = name;
            category.Description = model.Description?.Trim();
            category.IsRestricted = model.IsRestricted;
            category.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Document category updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var category = await _context.DocumentCategories
                .FindAsync(id);

            if (category == null)
                return NotFound();

            category.IsActive = !category.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                category.IsActive
                    ? "Document category activated successfully."
                    : "Document category deactivated successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}

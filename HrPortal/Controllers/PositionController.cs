using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// The register of posts. HR keeps it, since it is HR who decide
    /// what the establishment is and who sits in it.
    /// </summary>
    [Authorize(Roles = Roles.AdminOrHr)]
    public class PositionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;

        public PositionController(
            ApplicationDbContext context,
            AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index(bool includeRetired)
        {
            var query = _context.Positions
                .AsNoTracking()
                .Include(p => p.Department)
                .AsQueryable();

            if (!includeRetired)
                query = query.Where(p => p.IsActive);

            var positions = await query
                .Select(p => new PositionListViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Department = p.Department != null
                        ? p.Department.Name
                        : null,
                    Establishment = p.Establishment,
                    Filled = p.Employees.Count(e => e.IsActive),
                    IsActive = p.IsActive
                })
                .OrderBy(p => p.Department)
                .ThenBy(p => p.Title)
                .ToListAsync();

            ViewBag.IncludeRetired = includeRetired;

            ViewBag.TotalFilled = positions.Sum(p => p.Filled);

            ViewBag.TotalEstablishment = positions
                .Where(p => p.Establishment != null)
                .Sum(p => p.Establishment!.Value);

            ViewBag.Vacancies = positions.Sum(p => p.Vacancies);

            return View(positions);
        }

        public async Task<IActionResult> Create()
        {
            await LoadDepartments();

            return View(new PositionViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PositionViewModel model)
        {
            await ValidateAsync(model, null);

            if (!ModelState.IsValid)
            {
                await LoadDepartments();
                return View(model);
            }

            var position = new Position
            {
                Title = model.Title.Trim(),
                Description = model.Description?.Trim(),
                DepartmentId = model.DepartmentId,
                Establishment = model.Establishment,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.Positions.Add(position);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(Position),
                position.Id.ToString(),
                $"{position.Title} added to the position register.");

            TempData["Success"] = "Position added.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var position = await _context.Positions.FindAsync(id);

            if (position == null)
                return NotFound();

            await LoadDepartments();

            return View(new PositionViewModel
            {
                Id = position.Id,
                Title = position.Title,
                Description = position.Description,
                DepartmentId = position.DepartmentId,
                Establishment = position.Establishment,
                IsActive = position.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PositionViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            await ValidateAsync(model, id);

            if (!ModelState.IsValid)
            {
                await LoadDepartments();
                return View(model);
            }

            var position = await _context.Positions.FindAsync(id);

            if (position == null)
                return NotFound();

            position.Title = model.Title.Trim();
            position.Description = model.Description?.Trim();
            position.DepartmentId = model.DepartmentId;
            position.Establishment = model.Establishment;
            position.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(Position),
                position.Id.ToString(),
                $"{position.Title} updated.");

            TempData["Success"] = "Position updated.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var position = await _context.Positions
                .Include(p => p.Employees)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (position == null)
                return NotFound();

            // Somebody holds it, so removing it would quietly blank
            // their job title. Retiring keeps the history.
            if (position.Employees.Count > 0)
            {
                TempData["Error"] =
                    $"{position.Employees.Count} employee(s) hold this " +
                    "position, so it cannot be deleted. Take it out of " +
                    "use instead.";

                return RedirectToAction(nameof(Index));
            }

            _context.Positions.Remove(position);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(Position),
                position.Id.ToString(),
                $"{position.Title} removed from the position register.");

            TempData["Success"] = "Position deleted.";

            return RedirectToAction(nameof(Index));
        }

        private async Task ValidateAsync(PositionViewModel model, int? excludeId)
        {
            var title = model.Title.Trim();

            var clash = await _context.Positions
                .AnyAsync(p =>
                    p.Title == title &&
                    (excludeId == null || p.Id != excludeId.Value));

            if (clash)
            {
                ModelState.AddModelError(
                    nameof(model.Title),
                    "There is already a position with that title.");
            }
        }

        private async Task LoadDepartments()
        {
            ViewBag.Departments = await _context.Departments
                .AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new SelectListItem(d.Name, d.Id.ToString()))
                .ToListAsync();
        }
    }
}

using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.HrOnly)]
    public class LeaveTypeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RecordDocumentPdfService _pdf;

        public LeaveTypeController(
            ApplicationDbContext context,
            RecordDocumentPdfService pdf)
        {
            _context = context;
            _pdf = pdf;
        }

        /// <summary>
        /// The leave type and what it is currently costing the
        /// company, as a printable record.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var type = await _context.LeaveTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (type == null)
                return NotFound();

            var year = DateTime.Today.Year;

            var balances = await _context.LeaveBalances
                .AsNoTracking()
                .Where(b => b.LeaveTypeId == type.Id && b.Year == year)
                .ToListAsync();

            var record = new RecordDocument("Leave Type")
            {
                Headline = type.Name,
                Chip = type.IsActive ? "Active" : "Inactive"
            };

            record.Section("Leave type", "How the type is set up")
                .Add("Name", type.Name)
                .Add("Active", type.IsActive);

            record.Section($"Balances for {year}", "Across all staff")
                .Add("Employees with a balance", balances.Count)
                .Add(
                    "Days allocated",
                    balances.Sum(b => b.AllocatedDays))
                .Add("Days used", balances.Sum(b => b.UsedDays))
                .Add("Days remaining", balances.Sum(b => b.RemainingDays));

            record.Note(type.Description);

            return File(
                _pdf.Render(record),
                "application/pdf",
                record.FileName);
        }

        public async Task<IActionResult> Index()
        {
            var leaveTypes = await _context.LeaveTypes
                .AsNoTracking()
                .Include(l => l.LeaveRequests)
                .OrderBy(l => l.Name)
                .ToListAsync();

            return View(leaveTypes);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var leaveType = await _context.LeaveTypes
                .AsNoTracking()
                .Include(l => l.LeaveRequests)
                    .ThenInclude(r => r.Employee)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (leaveType == null)
                return NotFound();

            return View(leaveType);
        }

        public IActionResult Create()
        {
            return View(new LeaveTypeCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            LeaveTypeCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var name = model.Name.Trim();

            var exists = await _context.LeaveTypes
                .AnyAsync(l => l.Name.ToLower() == name.ToLower());

            if (exists)
            {
                ModelState.AddModelError(
                    "Name",
                    "A leave type with this name already exists.");

                return View(model);
            }

            var leaveType = new LeaveType
            {
                Name = name,
                Description = model.Description?.Trim(),
                IsActive = true
            };

            _context.LeaveTypes.Add(leaveType);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Leave type created successfully.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var leaveType = await _context.LeaveTypes
                .FindAsync(id);

            if (leaveType == null)
                return NotFound();

            var model = new LeaveTypeEditViewModel
            {
                Id = leaveType.Id,
                Name = leaveType.Name,
                Description = leaveType.Description,
                IsActive = leaveType.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            LeaveTypeEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            var leaveType = await _context.LeaveTypes
                .FindAsync(id);

            if (leaveType == null)
                return NotFound();

            var name = model.Name.Trim();

            var duplicateExists = await _context.LeaveTypes
                .AnyAsync(l =>
                    l.Id != id &&
                    l.Name.ToLower() == name.ToLower());

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    "Name",
                    "Another leave type already uses this name.");

                return View(model);
            }

            leaveType.Name = name;
            leaveType.Description = model.Description?.Trim();
            leaveType.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Leave type updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var leaveType = await _context.LeaveTypes
                .FindAsync(id);

            if (leaveType == null)
                return NotFound();

            if (leaveType.IsActive)
            {
                var hasOpenRequests = await _context.LeaveRequests
                    .AnyAsync(r =>
                        r.LeaveTypeId == id &&
                        r.Status != LeaveRequestStatus.Approved &&
                        r.Status != LeaveRequestStatus.Rejected &&
                        r.Status != LeaveRequestStatus.Cancelled);

                if (hasOpenRequests)
                {
                    TempData["Error"] =
                        "This leave type cannot be deactivated " +
                        "because it still has leave requests in " +
                        "progress. Complete or cancel them first.";

                    return RedirectToAction(nameof(Index));
                }
            }

            leaveType.IsActive = !leaveType.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                leaveType.IsActive
                    ? "Leave type activated successfully."
                    : "Leave type deactivated successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}

using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.ViewModels;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.HrOnly)]
    public class LeaveBalanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RecordScope _scope;

        public LeaveBalanceController(
            ApplicationDbContext context,
            RecordScope scope)
        {
            _context = context;
            _scope = scope;
        }

        public async Task<IActionResult> Index(int? year)
        {
            var selectedYear = year ?? DateTime.Today.Year;

            var visible = await _scope.VisibleToAsync(
                _context.LeaveBalances
                    .AsNoTracking()
                    .Include(b => b.Employee)
                    .Include(b => b.LeaveType),
                User);

            var balances = await visible
                .Where(b => b.Year == selectedYear)
                .OrderBy(b => b.Employee.FirstName)
                .ThenBy(b => b.Employee.LastName)
                .ThenBy(b => b.LeaveType.Name)
                .ToListAsync();

            var years = await _context.LeaveBalances
                .Select(b => b.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            if (!years.Contains(selectedYear))
                years.Add(selectedYear);

            ViewBag.Years = years.OrderByDescending(y => y).ToList();
            ViewBag.SelectedYear = selectedYear;

            return View(balances);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var balance = await _context.LeaveBalances
                .Include(b => b.Employee)
                    .ThenInclude(e => e.Department)
                .Include(b => b.LeaveType)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (balance == null)
                return NotFound();

            return View(balance);
        }

        public async Task<IActionResult> Create()
        {
            await LoadLookups();

            return View(new LeaveBalanceCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            LeaveBalanceCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var exists = await _context.LeaveBalances
                .AnyAsync(b =>
                    b.EmployeeId == model.EmployeeId &&
                    b.LeaveTypeId == model.LeaveTypeId &&
                    b.Year == model.Year);

            if (exists)
            {
                ModelState.AddModelError(
                    "",
                    "This employee already has a balance for that " +
                    "leave type and year. Edit the existing one instead.");

                await LoadLookups();
                return View(model);
            }

            var balance = new LeaveBalance
            {
                EmployeeId = model.EmployeeId,
                LeaveTypeId = model.LeaveTypeId,
                Year = model.Year,
                AllocatedDays = model.AllocatedDays,
                UsedDays = model.UsedDays,
                PendingDays = model.PendingDays,
                RemainingDays =
                    model.AllocatedDays
                    - model.UsedDays
                    - model.PendingDays
            };

            _context.LeaveBalances.Add(balance);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Leave balance created successfully.";

            return RedirectToAction(
                nameof(Index),
                new { year = balance.Year });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var balance = await _context.LeaveBalances
                .FindAsync(id);

            if (balance == null)
                return NotFound();

            var model = new LeaveBalanceEditViewModel
            {
                Id = balance.Id,
                EmployeeId = balance.EmployeeId,
                LeaveTypeId = balance.LeaveTypeId,
                Year = balance.Year,
                AllocatedDays = balance.AllocatedDays,
                UsedDays = balance.UsedDays,
                PendingDays = balance.PendingDays
            };

            await LoadLookups();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            LeaveBalanceEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var balance = await _context.LeaveBalances
                .FindAsync(id);

            if (balance == null)
                return NotFound();

            var duplicateExists = await _context.LeaveBalances
                .AnyAsync(b =>
                    b.Id != id &&
                    b.EmployeeId == model.EmployeeId &&
                    b.LeaveTypeId == model.LeaveTypeId &&
                    b.Year == model.Year);

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    "",
                    "This employee already has another balance for " +
                    "that leave type and year.");

                await LoadLookups();
                return View(model);
            }

            balance.EmployeeId = model.EmployeeId;
            balance.LeaveTypeId = model.LeaveTypeId;
            balance.Year = model.Year;
            balance.AllocatedDays = model.AllocatedDays;
            balance.UsedDays = model.UsedDays;
            balance.PendingDays = model.PendingDays;

            balance.RemainingDays =
                model.AllocatedDays
                - model.UsedDays
                - model.PendingDays;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Leave balance updated successfully.";

            return RedirectToAction(
                nameof(Index),
                new { year = balance.Year });
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var balance = await _context.LeaveBalances
                .Include(b => b.Employee)
                .Include(b => b.LeaveType)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (balance == null)
                return NotFound();

            return View(balance);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var balance = await _context.LeaveBalances
                .FindAsync(id);

            if (balance == null)
                return NotFound();

            var year = balance.Year;

            _context.LeaveBalances.Remove(balance);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Leave balance deleted successfully.";

            return RedirectToAction(
                nameof(Index),
                new { year });
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

            var leaveTypes = await _context.LeaveTypes
                .Where(l => l.IsActive)
                .OrderBy(l => l.Name)
                .ToListAsync();

            ViewBag.LeaveTypes =
                new SelectList(
                    leaveTypes,
                    "Id",
                    "Name");
        }
    }
}

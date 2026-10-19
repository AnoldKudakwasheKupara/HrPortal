using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize]
    public class MyRequisitionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RequisitionWorkflowService _workflow;
        private readonly CurrentUserService _currentUser;

        public MyRequisitionsController(
            ApplicationDbContext context,
            RequisitionWorkflowService workflow,
            CurrentUserService currentUser)
        {
            _context = context;
            _workflow = workflow;
            _currentUser = currentUser;
        }

        public async Task<IActionResult> Index()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var requisitions = await _context.Requisitions
                .Include(r => r.Vendor)
                .Include(r => r.Lines)
                .Where(r => r.EmployeeId == employeeId.Value)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.Threshold = _workflow.Threshold;
            ViewBag.Currency = _workflow.Currency;

            ViewBag.Awaiting = requisitions.Count(r =>
                !RequisitionWorkflowService.IsFinal(r.Status) &&
                !RequisitionWorkflowService.IsWithRequester(r.Status));

            ViewBag.WithMe = requisitions.Count(r =>
                RequisitionWorkflowService.IsWithRequester(r.Status));

            ViewBag.ApprovedTotal = requisitions
                .Where(r => r.Status == RequisitionStatus.Approved)
                .Sum(r => r.EstimatedTotal);

            return View(requisitions);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var requisition = await LoadOwnAsync(id.Value);

            if (requisition == null)
                return NotFound();

            ViewBag.Threshold = _workflow.Threshold;
            ViewBag.Currency = _workflow.Currency;

            return View(requisition);
        }

        public async Task<IActionResult> Create()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            await LoadLookups();

            var model = new RequisitionEditViewModel
            {
                Status = RequisitionStatus.Draft,
                Lines = new List<RequisitionLineViewModel>
                {
                    new(),
                    new(),
                    new()
                }
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            RequisitionEditViewModel model,
            string? submit)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var lines = RequisitionWorkflowService.UsableLines(model);

            foreach (var error in RequisitionWorkflowService.ValidateLines(lines))
                ModelState.AddModelError(string.Empty, error);

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var requisition = new Requisition
            {
                RequisitionNumber =
                    await _workflow.GenerateRequisitionNumberAsync(),
                EmployeeId = employeeId.Value,
                Status = RequisitionStatus.Draft,
                Title = model.Title.Trim(),
                Justification = model.Justification?.Trim(),
                NeededBy = model.NeededBy,
                VendorId = model.VendorId,
                CreatedAt = DateTime.UtcNow,
                Lines = lines.Select(RequisitionWorkflowService.ToLine).ToList()
            };

            requisition.EstimatedTotal =
                RequisitionWorkflowService.TotalOf(requisition);

            _context.Requisitions.Add(requisition);

            await _context.SaveChangesAsync();

            if (submit == "true")
            {
                await _workflow.SubmitAsync(
                    requisition,
                    employeeId.Value,
                    null,
                    isResubmission: false);

                TempData["Success"] =
                    $"{requisition.RequisitionNumber} submitted.";
            }
            else
            {
                TempData["Success"] =
                    $"{requisition.RequisitionNumber} saved as a draft.";
            }

            return RedirectToAction(nameof(Details), new { id = requisition.Id });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var requisition = await LoadOwnAsync(id.Value);

            if (requisition == null)
                return NotFound();

            if (!RequisitionWorkflowService.IsWithRequester(requisition.Status))
            {
                TempData["Error"] =
                    "This requisition is no longer with you, so it cannot " +
                    "be edited.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await LoadLookups();

            return View(ToEditModel(requisition));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            RequisitionEditViewModel model,
            string? submit)
        {
            if (id != model.Id)
                return NotFound();

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var requisition = await LoadOwnAsync(id);

            if (requisition == null)
                return NotFound();

            if (!RequisitionWorkflowService.IsWithRequester(requisition.Status))
            {
                TempData["Error"] =
                    "This requisition is no longer with you, so it cannot " +
                    "be edited.";

                return RedirectToAction(nameof(Details), new { id });
            }

            var lines = RequisitionWorkflowService.UsableLines(model);

            foreach (var error in RequisitionWorkflowService.ValidateLines(lines))
                ModelState.AddModelError(string.Empty, error);

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            requisition.Title = model.Title.Trim();
            requisition.Justification = model.Justification?.Trim();
            requisition.NeededBy = model.NeededBy;
            requisition.VendorId = model.VendorId;

            _context.RequisitionLines.RemoveRange(requisition.Lines);

            requisition.Lines = lines.Select(RequisitionWorkflowService.ToLine).ToList();

            requisition.EstimatedTotal =
                RequisitionWorkflowService.TotalOf(requisition);

            await _context.SaveChangesAsync();

            if (submit == "true")
            {
                var isResubmission =
                    requisition.Status == RequisitionStatus.ReturnedToRequester;

                await _workflow.SubmitAsync(
                    requisition,
                    employeeId.Value,
                    null,
                    isResubmission);

                TempData["Success"] =
                    $"{requisition.RequisitionNumber} submitted.";
            }
            else
            {
                TempData["Success"] = "Draft saved.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? comment)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var requisition = await LoadOwnAsync(id);

            if (requisition == null)
                return NotFound();

            if (RequisitionWorkflowService.IsFinal(requisition.Status))
            {
                TempData["Error"] =
                    "This requisition has already been closed.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await _workflow.CancelAsync(
                requisition,
                employeeId.Value,
                comment);

            TempData["Success"] =
                $"{requisition.RequisitionNumber} withdrawn.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var requisition = await LoadOwnAsync(id);

            if (requisition == null)
                return NotFound();

            if (requisition.Status != RequisitionStatus.Draft)
            {
                TempData["Error"] =
                    "Only a draft can be deleted. Withdraw it instead.";

                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Requisitions.Remove(requisition);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Draft deleted.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<Requisition?> LoadOwnAsync(int id)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return null;

            return await _context.Requisitions
                .Include(r => r.Employee)
                .Include(r => r.Vendor)
                .Include(r => r.Lines)
                .Include(r => r.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(r =>
                    r.Id == id &&
                    r.EmployeeId == employeeId.Value);
        }

        private static RequisitionEditViewModel ToEditModel(
            Requisition requisition)
        {
            var model = new RequisitionEditViewModel
            {
                Id = requisition.Id,
                RequisitionNumber = requisition.RequisitionNumber,
                Status = requisition.Status,
                Title = requisition.Title,
                Justification = requisition.Justification,
                NeededBy = requisition.NeededBy,
                VendorId = requisition.VendorId,

                Lines = requisition.Lines
                    .Select(l => new RequisitionLineViewModel
                    {
                        Id = l.Id,
                        Description = l.Description,
                        Quantity = l.Quantity,
                        UnitOfMeasure = l.UnitOfMeasure,
                        EstimatedUnitCost = l.EstimatedUnitCost,
                        Notes = l.Notes
                    })
                    .ToList()
            };

            model.Lines.Add(new RequisitionLineViewModel());
            model.Lines.Add(new RequisitionLineViewModel());

            return model;
        }

        private async Task LoadLookups()
        {
            ViewBag.Vendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.Name)
                .Select(v => new SelectListItem(v.Name, v.Id.ToString()))
                .ToListAsync();

            ViewBag.Threshold = _workflow.Threshold;
            ViewBag.Currency = _workflow.Currency;
        }
    }
}

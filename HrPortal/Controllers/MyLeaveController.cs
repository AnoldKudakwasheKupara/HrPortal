using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize]
    public class MyLeaveController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LeaveWorkflowService _workflow;
        private readonly LeaveBalanceService _balances;
        private readonly CurrentUserService _currentUser;
        private readonly IAuthorizationService _authorization;

        public MyLeaveController(
            ApplicationDbContext context,
            LeaveWorkflowService workflow,
            LeaveBalanceService balances,
            CurrentUserService currentUser,
            IAuthorizationService authorization)
        {
            _context = context;
            _workflow = workflow;
            _balances = balances;
            _currentUser = currentUser;
            _authorization = authorization;
        }

        public async Task<IActionResult> Index()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var model = new MyLeaveIndexViewModel
            {
                Requests = await _context.LeaveRequests
                    .Include(r => r.LeaveType)
                    .Where(r => r.EmployeeId == employeeId.Value)
                    .OrderByDescending(r => r.SubmittedAt)
                    .ToListAsync(),

                Balances = await _context.LeaveBalances
                    .Include(b => b.LeaveType)
                    .Where(b =>
                        b.EmployeeId == employeeId.Value &&
                        b.Year == DateTime.Today.Year)
                    .OrderBy(b => b.LeaveType.Name)
                    .ToListAsync()
            };

            return View(model);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await LoadAuthorizedRequest(
                id.Value,
                Operations.View);

            if (request == null)
                return NotFound();

            return View(request);
        }

        public async Task<IActionResult> Create()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            await LoadLeaveTypes();

            return View(new MyLeaveRequestViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            MyLeaveRequestViewModel model)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var numberOfDays = await ValidateRequest(
                model,
                employeeId.Value,
                null);

            if (!ModelState.IsValid)
            {
                await LoadLeaveTypes();
                return View(model);
            }

            var request = new LeaveRequest
            {
                RequestNumber =
                    await _workflow.GenerateRequestNumberAsync(),
                EmployeeId = employeeId.Value,
                LeaveTypeId = model.LeaveTypeId,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                NumberOfDays = numberOfDays,
                Reason = model.Reason?.Trim(),
                Status = LeaveRequestStatus.Draft,
                SubmittedAt = DateTime.UtcNow
            };

            _context.LeaveRequests.Add(request);

            await _context.SaveChangesAsync();

            await _workflow.SubmitAsync(
                request,
                employeeId.Value,
                model.Reason,
                isResubmission: false);

            TempData["Success"] =
                $"Leave request {request.RequestNumber} submitted " +
                "for HR review.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await LoadAuthorizedRequest(
                id.Value,
                Operations.Manage);

            if (request == null)
                return NotFound();

            if (!LeaveWorkflowService.IsWithEmployee(request.Status))
            {
                TempData["Error"] =
                    "This request is with the approvers and cannot " +
                    "be changed.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            await LoadLeaveTypes();

            return View(new MyLeaveRequestViewModel
            {
                Id = request.Id,
                RequestNumber = request.RequestNumber,
                LeaveTypeId = request.LeaveTypeId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Reason = request.Reason
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            MyLeaveRequestViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var request = await LoadAuthorizedRequest(
                id,
                Operations.Manage);

            if (request == null)
                return NotFound();

            if (!LeaveWorkflowService.IsWithEmployee(request.Status))
            {
                TempData["Error"] =
                    "This request is with the approvers and cannot " +
                    "be changed.";

                return RedirectToAction(nameof(Details), new { id });
            }

            var numberOfDays = await ValidateRequest(
                model,
                employeeId.Value,
                id);

            if (!ModelState.IsValid)
            {
                await LoadLeaveTypes();
                return View(model);
            }

            request.LeaveTypeId = model.LeaveTypeId;
            request.StartDate = model.StartDate;
            request.EndDate = model.EndDate;
            request.NumberOfDays = numberOfDays;
            request.Reason = model.Reason?.Trim();

            await _workflow.SubmitAsync(
                request,
                employeeId.Value,
                model.Reason,
                isResubmission: true);

            TempData["Success"] =
                $"Leave request {request.RequestNumber} sent back " +
                "to HR for review.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var request = await LoadAuthorizedRequest(
                id,
                Operations.Manage);

            if (request == null)
                return NotFound();

            if (LeaveWorkflowService.IsFinal(request.Status))
            {
                TempData["Error"] =
                    "This request has already been decided and " +
                    "cannot be cancelled.";

                return RedirectToAction(nameof(Index));
            }

            await _workflow.CancelAsync(
                request,
                employeeId.Value,
                "Cancelled by the employee.");

            TempData["Success"] =
                "Leave request cancelled.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<decimal> ValidateRequest(
            MyLeaveRequestViewModel model,
            int employeeId,
            int? excludeRequestId)
        {
            if (model.EndDate.Date < model.StartDate.Date)
            {
                ModelState.AddModelError(
                    nameof(model.EndDate),
                    "The end date cannot be before the start date.");

                return 0;
            }

            var numberOfDays =
                LeaveWorkflowService.CountWorkingDays(
                    model.StartDate,
                    model.EndDate);

            if (numberOfDays == 0)
            {
                ModelState.AddModelError(
                    nameof(model.EndDate),
                    "That range contains no working days.");

                return 0;
            }

            var overlaps = await _context.LeaveRequests
                .AnyAsync(r =>
                    r.EmployeeId == employeeId &&
                    (excludeRequestId == null ||
                     r.Id != excludeRequestId.Value) &&
                    r.Status != LeaveRequestStatus.Rejected &&
                    r.Status != LeaveRequestStatus.HRRejected &&
                    r.Status != LeaveRequestStatus.Cancelled &&
                    r.StartDate <= model.EndDate &&
                    r.EndDate >= model.StartDate);

            if (overlaps)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "You already have leave booked that overlaps " +
                    "these dates.");

                return numberOfDays;
            }

            var balanceError =
                await _balances.ValidateAgainstBalanceAsync(
                    employeeId,
                    model.LeaveTypeId,
                    model.StartDate.Year,
                    numberOfDays,
                    excludeRequestId);

            if (balanceError != null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    balanceError);
            }

            return numberOfDays;
        }

        private async Task<LeaveRequest?> LoadAuthorizedRequest(
            int id,
            OperationAuthorizationRequirement operation)
        {
            var request = await _context.LeaveRequests
                .Include(r => r.LeaveType)
                .Include(r => r.Employee)
                .Include(r => r.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return null;

            var result = await _authorization.AuthorizeAsync(
                User,
                request,
                operation);

            return result.Succeeded ? request : null;
        }

        private async Task LoadLeaveTypes()
        {
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

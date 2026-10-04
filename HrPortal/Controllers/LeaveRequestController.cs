using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.ViewModels;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.AdminOrHrOrMd)]
    public class LeaveRequestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public LeaveRequestController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        // ============================================================
        // INDEX
        // ============================================================

        public async Task<IActionResult> Index(
            LeaveRequestStatus? status)
        {
            var query = _context.LeaveRequests
                .Include(r => r.Employee)
                .Include(r => r.LeaveType)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            var requests = await query
                .OrderByDescending(r => r.SubmittedAt)
                .ToListAsync();

            ViewBag.SelectedStatus = status;

            return View(requests);
        }


        // ============================================================
        // DETAILS
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await _context.LeaveRequests
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Department)
                .Include(r => r.LeaveType)
                .Include(r => r.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            return View(request);
        }


        // ============================================================
        // CREATE - GET
        // ============================================================

        public async Task<IActionResult> Create()
        {
            await LoadLookups();

            return View(new LeaveRequestCreateViewModel());
        }


        // ============================================================
        // CREATE - POST
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            LeaveRequestCreateViewModel model)
        {
            if (model.EndDate.Date < model.StartDate.Date)
            {
                ModelState.AddModelError(
                    "EndDate",
                    "The end date cannot be before the start date.");
            }

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var numberOfDays = CountWorkingDays(
                model.StartDate,
                model.EndDate);

            if (numberOfDays == 0)
            {
                ModelState.AddModelError(
                    "EndDate",
                    "That range contains no working days.");

                await LoadLookups();
                return View(model);
            }

            // Overlapping leave for the same employee is almost
            // always a mistake, so block it rather than record it.
            var overlaps = await _context.LeaveRequests
                .AnyAsync(r =>
                    r.EmployeeId == model.EmployeeId &&
                    r.Status != LeaveRequestStatus.Rejected &&
                    r.Status != LeaveRequestStatus.Cancelled &&
                    r.StartDate <= model.EndDate &&
                    r.EndDate >= model.StartDate);

            if (overlaps)
            {
                ModelState.AddModelError(
                    "",
                    "This employee already has leave booked that " +
                    "overlaps these dates.");

                await LoadLookups();
                return View(model);
            }

            var request = new LeaveRequest
            {
                RequestNumber = await GenerateRequestNumber(),
                EmployeeId = model.EmployeeId,
                LeaveTypeId = model.LeaveTypeId,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                NumberOfDays = numberOfDays,
                Reason = model.Reason?.Trim(),
                Status = LeaveRequestStatus.Submitted,
                SubmittedAt = DateTime.UtcNow
            };

            _context.LeaveRequests.Add(request);

            await _context.SaveChangesAsync();

            // The employee is the one submitting, so they are the
            // actor on the opening history entry.
            _context.LeaveRequestHistories.Add(
                new LeaveRequestHistory
                {
                    LeaveRequestId = request.Id,
                    ActionedByEmployeeId = request.EmployeeId,
                    Action = LeaveRequestAction.Submitted,
                    Comment = model.Reason?.Trim(),
                    ActionDate = DateTime.UtcNow
                });

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Leave request {request.RequestNumber} created successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // EDIT - GET
        // ============================================================

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await _context.LeaveRequests
                .FindAsync(id);

            if (request == null)
                return NotFound();

            var model = new LeaveRequestEditViewModel
            {
                Id = request.Id,
                RequestNumber = request.RequestNumber,
                EmployeeId = request.EmployeeId,
                LeaveTypeId = request.LeaveTypeId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Reason = request.Reason,
                Status = request.Status
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
            LeaveRequestEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (model.EndDate.Date < model.StartDate.Date)
            {
                ModelState.AddModelError(
                    "EndDate",
                    "The end date cannot be before the start date.");
            }

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var request = await _context.LeaveRequests
                .FindAsync(id);

            if (request == null)
                return NotFound();

            var numberOfDays = CountWorkingDays(
                model.StartDate,
                model.EndDate);

            if (numberOfDays == 0)
            {
                ModelState.AddModelError(
                    "EndDate",
                    "That range contains no working days.");

                await LoadLookups();
                return View(model);
            }

            var overlaps = await _context.LeaveRequests
                .AnyAsync(r =>
                    r.Id != id &&
                    r.EmployeeId == model.EmployeeId &&
                    r.Status != LeaveRequestStatus.Rejected &&
                    r.Status != LeaveRequestStatus.Cancelled &&
                    r.StartDate <= model.EndDate &&
                    r.EndDate >= model.StartDate);

            if (overlaps)
            {
                ModelState.AddModelError(
                    "",
                    "This employee already has other leave booked " +
                    "that overlaps these dates.");

                await LoadLookups();
                return View(model);
            }

            var previousStatus = request.Status;

            request.EmployeeId = model.EmployeeId;
            request.LeaveTypeId = model.LeaveTypeId;
            request.StartDate = model.StartDate;
            request.EndDate = model.EndDate;
            request.NumberOfDays = numberOfDays;
            request.Reason = model.Reason?.Trim();
            request.Status = model.Status;

            // A request is complete once it reaches a final state.
            request.CompletedAt =
                IsFinalStatus(model.Status)
                    ? request.CompletedAt ?? DateTime.UtcNow
                    : null;

            // Only a real status change is worth a history entry.
            if (previousStatus != model.Status)
            {
                _context.LeaveRequestHistories.Add(
                    new LeaveRequestHistory
                    {
                        LeaveRequestId = request.Id,
                        ActionedByEmployeeId =
                            await GetCurrentEmployeeId()
                            ?? request.EmployeeId,
                        Action = MapStatusToAction(model.Status),
                        Comment = model.Comment?.Trim(),
                        ActionDate = DateTime.UtcNow
                    });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Leave request updated successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // CANCEL
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var request = await _context.LeaveRequests
                .FindAsync(id);

            if (request == null)
                return NotFound();

            if (IsFinalStatus(request.Status))
            {
                TempData["Error"] =
                    "This request has already been completed and " +
                    "cannot be cancelled.";

                return RedirectToAction(nameof(Index));
            }

            request.Status = LeaveRequestStatus.Cancelled;
            request.CompletedAt = DateTime.UtcNow;

            _context.LeaveRequestHistories.Add(
                new LeaveRequestHistory
                {
                    LeaveRequestId = request.Id,
                    ActionedByEmployeeId =
                        await GetCurrentEmployeeId()
                        ?? request.EmployeeId,
                    Action = LeaveRequestAction.Cancelled,
                    ActionDate = DateTime.UtcNow
                });

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Leave request cancelled.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // DELETE - GET
        // ============================================================

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await _context.LeaveRequests
                .Include(r => r.Employee)
                .Include(r => r.LeaveType)
                .Include(r => r.History)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            return View(request);
        }


        // ============================================================
        // DELETE - POST
        // ============================================================

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var request = await _context.LeaveRequests
                .FindAsync(id);

            if (request == null)
                return NotFound();

            // History is cascade deleted with the request.
            _context.LeaveRequests.Remove(request);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Leave request deleted successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // HELPERS
        // ============================================================

        /// <summary>
        /// Counts the working days in a range, both ends included,
        /// skipping weekends.
        /// </summary>
        private static decimal CountWorkingDays(
            DateTime startDate,
            DateTime endDate)
        {
            var days = 0;

            for (var date = startDate.Date;
                 date <= endDate.Date;
                 date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday &&
                    date.DayOfWeek != DayOfWeek.Sunday)
                {
                    days++;
                }
            }

            return days;
        }

        private static bool IsFinalStatus(LeaveRequestStatus status)
        {
            return status == LeaveRequestStatus.Approved
                || status == LeaveRequestStatus.Rejected
                || status == LeaveRequestStatus.Cancelled;
        }

        private static LeaveRequestAction MapStatusToAction(
            LeaveRequestStatus status)
        {
            return status switch
            {
                LeaveRequestStatus.Submitted =>
                    LeaveRequestAction.Resubmitted,

                LeaveRequestStatus.HRReview =>
                    LeaveRequestAction.Submitted,

                LeaveRequestStatus.ReturnedToEmployee =>
                    LeaveRequestAction.ReturnedByHR,

                LeaveRequestStatus.HRApproved =>
                    LeaveRequestAction.ApprovedByHR,

                LeaveRequestStatus.HRRejected =>
                    LeaveRequestAction.RejectedByHR,

                LeaveRequestStatus.MDReview =>
                    LeaveRequestAction.ApprovedByHR,

                LeaveRequestStatus.Approved =>
                    LeaveRequestAction.ApprovedByMD,

                LeaveRequestStatus.Rejected =>
                    LeaveRequestAction.RejectedByMD,

                LeaveRequestStatus.Cancelled =>
                    LeaveRequestAction.Cancelled,

                _ => LeaveRequestAction.Submitted
            };
        }

        /// <summary>
        /// Builds the next reference for the current year, in the
        /// form LR-2026-0001.
        /// </summary>
        private async Task<string> GenerateRequestNumber()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"LR-{year}-";

            var lastNumber = await _context.LeaveRequests
                .Where(r => r.RequestNumber.StartsWith(prefix))
                .OrderByDescending(r => r.RequestNumber)
                .Select(r => r.RequestNumber)
                .FirstOrDefaultAsync();

            var next = 1;

            if (lastNumber != null &&
                int.TryParse(
                    lastNumber.Substring(prefix.Length),
                    out var parsed))
            {
                next = parsed + 1;
            }

            return $"{prefix}{next:D4}";
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

using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Loans are sanctioned by the Managing Director, so this is the
    /// only place a loan decision is taken.
    /// </summary>
    [Authorize(Roles = Roles.MdOnly)]
    public class LoanApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LoanWorkflowService _workflow;
        private readonly CurrentUserService _currentUser;
        private readonly FileStorageService _fileStorage;

        private readonly LoanApplicationPdfService _pdf;

        public LoanApprovalController(
            ApplicationDbContext context,
            LoanWorkflowService workflow,
            CurrentUserService currentUser,
            FileStorageService fileStorage,
            LoanApplicationPdfService pdf)
        {
            _context = context;
            _workflow = workflow;
            _currentUser = currentUser;
            _fileStorage = fileStorage;
            _pdf = pdf;
        }

        public async Task<IActionResult> Index(bool settled = false)
        {
            var query = _context.LoanApplications
                .AsNoTracking()
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .AsQueryable();

            query = settled
                ? query.Where(l =>
                    l.Status != LoanApplicationStatus.Submitted)
                : query.Where(l =>
                    l.Status == LoanApplicationStatus.Submitted);

            ViewBag.Settled = settled;

            var applications = await query
                .OrderBy(l => l.SubmittedAt ?? l.CreatedAt)
                .ToListAsync();

            return View(applications);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var application = await LoadAsync(id.Value);

            if (application == null)
                return NotFound();

            ViewBag.Decision = new LoanDecisionViewModel
            {
                Id = application.Id,
                AmountAuthorised = application.AmountRequested,
                RepaymentStartDate = StartOfNextMonth()
            };

            return View(application);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(
            int id,
            LoanDecisionViewModel model)
        {
            var application = await LoadAsync(id);

            if (application == null)
                return NotFound();

            var actedBy = await _currentUser.GetEmployeeIdAsync();

            if (actedBy == null)
            {
                TempData["Error"] =
                    "Your sign-in is not linked to an employee " +
                    "record, so the decision cannot be recorded.";

                return RedirectToAction(nameof(Details), new { id });
            }

            if (!LoanWorkflowService.CanAct(
                    LoanApplicationAction.Approved,
                    application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status} and cannot be sanctioned.";

                return RedirectToAction(nameof(Details), new { id });
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Decision = model;
                return View(nameof(Details), application);
            }

            await _workflow.ApproveAsync(
                application,
                actedBy.Value,
                model.AmountAuthorised,
                model.RepaymentStartDate,
                model.Comment);

            TempData["Success"] =
                $"{application.ApplicationNumber} sanctioned for " +
                $"{model.AmountAuthorised:N2}.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? comment)
        {
            return await DecideAsync(
                id,
                comment,
                LoanApplicationAction.Rejected,
                "declined");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(int id, string? comment)
        {
            return await DecideAsync(
                id,
                comment,
                LoanApplicationAction.Returned,
                "sent back to the applicant");
        }


        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var application = await LoadAsync(id.Value);

            if (application == null)
                return NotFound();

            return File(
                _pdf.Render(application),
                "application/pdf",
                $"Loan application - " +
                $"{application.ApplicationNumber}.pdf");
        }
        public async Task<IActionResult> Attachment(
            int id,
            int attachmentId)
        {
            var application = await LoadAsync(id);

            if (application == null)
                return NotFound();

            var attachment = application.Attachments
                .FirstOrDefault(a => a.Id == attachmentId);

            if (attachment == null)
                return NotFound();

            var fullPath =
                _fileStorage.ResolveExistingPath(attachment.FilePath);

            if (fullPath == null)
            {
                TempData["Error"] =
                    "That file is no longer on the server.";

                return RedirectToAction(nameof(Details), new { id });
            }

            new FileExtensionContentTypeProvider()
                .TryGetContentType(fullPath, out var contentType);

            return PhysicalFile(
                fullPath,
                contentType ?? "application/octet-stream",
                attachment.OriginalFileName);
        }

        private async Task<IActionResult> DecideAsync(
            int id,
            string? comment,
            LoanApplicationAction action,
            string described)
        {
            var application = await LoadAsync(id);

            if (application == null)
                return NotFound();

            var actedBy = await _currentUser.GetEmployeeIdAsync();

            if (actedBy == null)
            {
                TempData["Error"] =
                    "Your sign-in is not linked to an employee " +
                    "record, so the decision cannot be recorded.";

                return RedirectToAction(nameof(Details), new { id });
            }

            if (!LoanWorkflowService.CanAct(action, application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status}, so it cannot be " +
                    $"{described}.";

                return RedirectToAction(nameof(Details), new { id });
            }

            if (action == LoanApplicationAction.Rejected)
            {
                await _workflow.RejectAsync(
                    application,
                    actedBy.Value,
                    comment);
            }
            else
            {
                await _workflow.ReturnAsync(
                    application,
                    actedBy.Value,
                    comment);
            }

            TempData["Success"] =
                $"{application.ApplicationNumber} {described}.";

            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Deductions normally begin with the next payroll run, so the
        /// form opens on the first of next month.
        /// </summary>
        private static DateTime StartOfNextMonth()
        {
            var today = DateTime.Today;

            return new DateTime(today.Year, today.Month, 1)
                .AddMonths(1);
        }

        private async Task<LoanApplication?> LoadAsync(int id)
        {
            return await _context.LoanApplications
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .Include(l => l.Attachments)
                .Include(l => l.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(l => l.Id == id);
        }
    }
}

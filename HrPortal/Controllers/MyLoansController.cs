using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize]
    public class MyLoansController : Controller
    {
        private const string UploadFolder = "loans";

        private readonly ApplicationDbContext _context;
        private readonly LoanWorkflowService _workflow;
        private readonly CurrentUserService _currentUser;
        private readonly IAuthorizationService _authorization;
        private readonly FileStorageService _fileStorage;

        public MyLoansController(
            ApplicationDbContext context,
            LoanWorkflowService workflow,
            CurrentUserService currentUser,
            IAuthorizationService authorization,
            FileStorageService fileStorage)
        {
            _context = context;
            _workflow = workflow;
            _currentUser = currentUser;
            _authorization = authorization;
            _fileStorage = fileStorage;
        }

        public async Task<IActionResult> Index()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var applications = await _context.LoanApplications
                .AsNoTracking()
                .Where(l => l.EmployeeId == employeeId.Value)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            return View(applications);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var application = await LoadAuthorizedAsync(
                id.Value,
                Operations.View);

            if (application == null)
                return NotFound();

            return View(application);
        }

        public async Task<IActionResult> Create()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            await LoadApplicantAsync();

            return View(new MyLoanViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MyLoanViewModel model)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            if (!ModelState.IsValid)
            {
                await LoadApplicantAsync();
                return View(model);
            }

            var application = new LoanApplication
            {
                ApplicationNumber =
                    await _workflow.GenerateApplicationNumberAsync(),
                EmployeeId = employeeId.Value,
                Status = LoanApplicationStatus.Draft,
                AmountRequested = model.AmountRequested,
                RepaymentMonths = model.RepaymentMonths,
                Purpose = model.Purpose.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.LoanApplications.Add(application);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{application.ApplicationNumber} saved as a draft. " +
                "Attach any receipts or quotations, then send it.";

            return RedirectToAction(
                nameof(Details),
                new { id = application.Id });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var application = await LoadAuthorizedAsync(
                id.Value,
                Operations.Manage);

            if (application == null)
                return NotFound();

            if (!LoanWorkflowService.IsWithApplicant(application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status} and cannot be changed.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await LoadApplicantAsync();

            return View(new MyLoanViewModel
            {
                Id = application.Id,
                ApplicationNumber = application.ApplicationNumber,
                AmountRequested = application.AmountRequested,
                RepaymentMonths = application.RepaymentMonths,
                Purpose = application.Purpose
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MyLoanViewModel model)
        {
            var application = await LoadAuthorizedAsync(
                id,
                Operations.Manage);

            if (application == null)
                return NotFound();

            if (!LoanWorkflowService.IsWithApplicant(application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status} and cannot be changed.";

                return RedirectToAction(nameof(Details), new { id });
            }

            if (!ModelState.IsValid)
            {
                await LoadApplicantAsync();
                return View(model);
            }

            application.AmountRequested = model.AmountRequested;
            application.RepaymentMonths = model.RepaymentMonths;
            application.Purpose = model.Purpose.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{application.ApplicationNumber} updated.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int id)
        {
            var application = await LoadAuthorizedAsync(
                id,
                Operations.Manage);

            if (application == null)
                return NotFound();

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            if (!LoanWorkflowService.CanAct(
                    LoanApplicationAction.Submitted,
                    application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status} and cannot be sent.";

                return RedirectToAction(nameof(Details), new { id });
            }

            var isResubmission =
                application.Status ==
                    LoanApplicationStatus.ReturnedToApplicant;

            await _workflow.SubmitAsync(
                application,
                employeeId.Value,
                isResubmission);

            TempData["Success"] =
                $"{application.ApplicationNumber} has gone to the " +
                "Managing Director.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var application = await LoadAuthorizedAsync(
                id,
                Operations.Manage);

            if (application == null)
                return NotFound();

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            if (!LoanWorkflowService.CanAct(
                    LoanApplicationAction.Cancelled,
                    application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status} and cannot be cancelled.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await _workflow.CancelAsync(
                application,
                employeeId.Value,
                null);

            TempData["Success"] =
                $"{application.ApplicationNumber} cancelled.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Attach(int id, IFormFile? file)
        {
            var application = await LoadAuthorizedAsync(
                id,
                Operations.Manage);

            if (application == null)
                return NotFound();

            if (!LoanWorkflowService.IsWithApplicant(application.Status))
            {
                TempData["Error"] =
                    "Attachments can only be added while the " +
                    "application is with you.";

                return RedirectToAction(nameof(Details), new { id });
            }

            var problem = FileStorageService.Validate(file);

            if (problem != null)
            {
                TempData["Error"] = problem;
                return RedirectToAction(nameof(Details), new { id });
            }

            var (fileName, filePath) =
                await _fileStorage.SaveAsync(file!, UploadFolder);

            _context.LoanApplicationAttachments.Add(
                new LoanApplicationAttachment
                {
                    LoanApplicationId = application.Id,
                    FileName = fileName,
                    OriginalFileName =
                        Path.GetFileName(file!.FileName),
                    FilePath = filePath,
                    SizeInBytes = file.Length,
                    UploadedAt = DateTime.UtcNow,
                    UploadedByEmployeeId =
                        await _currentUser.GetEmployeeIdAsync()
                });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Attachment added.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Detach(int id, int attachmentId)
        {
            var application = await LoadAuthorizedAsync(
                id,
                Operations.Manage);

            if (application == null)
                return NotFound();

            if (!LoanWorkflowService.IsWithApplicant(application.Status))
            {
                TempData["Error"] =
                    "Attachments can only be removed while the " +
                    "application is with you.";

                return RedirectToAction(nameof(Details), new { id });
            }

            var attachment = application.Attachments
                .FirstOrDefault(a => a.Id == attachmentId);

            if (attachment == null)
                return NotFound();

            _fileStorage.Delete(attachment.FilePath);

            _context.LoanApplicationAttachments.Remove(attachment);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Attachment removed.";

            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> Attachment(
            int id,
            int attachmentId)
        {
            var application = await LoadAuthorizedAsync(
                id,
                Operations.View);

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

        private async Task LoadApplicantAsync()
        {
            ViewBag.Applicant = await _currentUser.GetEmployeeAsync();
        }

        private async Task<LoanApplication?> LoadAuthorizedAsync(
            int id,
            OperationAuthorizationRequirement operation)
        {
            var application = await _context.LoanApplications
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .Include(l => l.Attachments)
                .Include(l => l.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (application == null)
                return null;

            var result = await _authorization.AuthorizeAsync(
                User,
                application,
                operation);

            return result.Succeeded ? application : null;
        }
    }
}

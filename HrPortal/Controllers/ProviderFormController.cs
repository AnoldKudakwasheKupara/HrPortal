using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.Services.ProviderForms;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Where HR checks the providers' forms, fills in the employer's
    /// part and keeps track of what has gone. They carry salaries and
    /// family details, so HR sees only the forms of the people their
    /// own company employs, and each time one is opened it is recorded.
    /// </summary>
    [Authorize(Roles = Roles.HrOnly)]
    public class ProviderFormController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CurrentUserService _currentUser;
        private readonly ProviderFormService _forms;
        private readonly ProviderFormPdfService _pdf;
        private readonly AuditService _audit;

        public ProviderFormController(
            ApplicationDbContext context,
            CurrentUserService currentUser,
            ProviderFormService forms,
            ProviderFormPdfService pdf,
            AuditService audit)
        {
            _context = context;
            _currentUser = currentUser;
            _forms = forms;
            _pdf = pdf;
            _audit = audit;
        }

        public async Task<IActionResult> Index()
        {
            return View(await Ours()
                .AsNoTracking()
                .OrderByDescending(f => f.SubmittedAt ?? f.CreatedAt)
                .ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var form = await LoadAsync(id);

            if (form == null)
                return NotFound();

            await LogViewAsync(form);

            return View(await ReviewAsync(form));
        }

        public async Task<IActionResult> Form(int id, bool download = false)
        {
            var form = await LoadAsync(id);

            if (form == null)
                return NotFound();

            await LogViewAsync(form);

            return download
                ? File(_pdf.Render(form), "application/pdf", ProviderFormPdfService.FileName(form))
                : File(_pdf.Render(form), "application/pdf");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, ProviderFormEmployer employer, string? signature)
        {
            var form = await LoadAsync(id);

            if (form == null)
                return NotFound();

            if (!ProviderFormRules.WithHr(form.Status))
                return Locked(form);

            var me = await _currentUser.GetEmployeeAsync();

            if (me == null)
            {
                TempData["Error"] = "Your sign-in is not linked to an employee record, so this cannot be recorded.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ProviderFormRules.Clean(employer);

            foreach (var problem in ProviderFormRules.EmployerProblems(form.Kind, employer))
                ModelState.AddModelError(string.Empty, problem);

            byte[]? signed = null;

            if (form.Kind == ProviderFormKind.Minerva)
            {
                signed = SignatureImage.Read(signature) ?? form.EmployerSignature;

                if (signed == null)
                    ModelState.AddModelError(string.Empty, "Sign for the employer in the box.");
            }

            if (!ModelState.IsValid)
            {
                return View(nameof(Details), new ProviderFormReviewViewModel
                {
                    Form = form,
                    Employer = employer
                });
            }

            await _forms.CompleteAsync(form, employer, signed, me.Id);

            await _audit.LogAsync(
                AuditService.ProviderFormCompleted,
                nameof(ProviderForm),
                form.Reference,
                $"{ProviderFormRules.Title(form.Kind)} of {Who(form)} completed by HR.");

            TempData["Success"] = $"{form.Reference} is complete. Download it and send it to {ProviderFormRules.Provider(form.Kind)}.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(int id, string? comment)
        {
            var form = await LoadAsync(id);

            if (form == null)
                return NotFound();

            if (!ProviderFormRules.WithHr(form.Status))
                return Locked(form);

            comment = comment?.Trim();

            if (string.IsNullOrEmpty(comment) || comment.Length > 500)
            {
                TempData["Error"] = "Say what needs to be changed, in up to 500 characters.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await _forms.ReturnAsync(form, comment);

            await _audit.LogAsync(
                AuditService.ProviderFormReturned,
                nameof(ProviderForm),
                form.Reference,
                $"{ProviderFormRules.Title(form.Kind)} of {Who(form)} sent back.");

            TempData["Success"] = $"{form.Reference} has been sent back to {Who(form)}.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sent(int id)
        {
            var form = await LoadAsync(id);

            if (form == null)
                return NotFound();

            if (form.Status != ProviderFormStatus.Completed)
                return Locked(form);

            await _forms.MarkSentAsync(form);

            await _audit.LogAsync(
                AuditService.ProviderFormSent,
                nameof(ProviderForm),
                form.Reference,
                $"{ProviderFormRules.Title(form.Kind)} of {Who(form)} marked as sent to the provider.");

            TempData["Success"] = $"{form.Reference} is marked as sent.";

            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Forms that have reached HR, of the people this company
        /// employs. A form with the employee is not HR's to see yet,
        /// unless it was sent back and HR is waiting for it.
        /// </summary>
        private IQueryable<ProviderForm> Ours()
        {
            var here = _context.CompanyFilter;

            return _context.ProviderForms
                .Include(f => f.Employee)
                    .ThenInclude(e => e.Department)
                .Include(f => f.CompletedByEmployee)
                .Where(f =>
                    f.Status != ProviderFormStatus.Draft &&
                    (here == null || f.Employee.HomeCompanyId == here));
        }

        private Task<ProviderForm?> LoadAsync(int id) =>
            Ours().FirstOrDefaultAsync(f => f.Id == id && f.Status != ProviderFormStatus.Returned);

        private async Task<ProviderFormReviewViewModel> ReviewAsync(ProviderForm form)
        {
            var me = await _currentUser.GetEmployeeAsync();

            return new ProviderFormReviewViewModel
            {
                Form = form,
                Employer = me == null ? form.Employer : await _forms.SuggestEmployerAsync(form, me)
            };
        }

        private Task LogViewAsync(ProviderForm form) =>
            _audit.LogAsync(
                AuditService.PersonalDetailsViewed,
                nameof(ProviderForm),
                form.Reference,
                $"{ProviderFormRules.Title(form.Kind)} of {Who(form)} viewed.");

        private IActionResult Locked(ProviderForm form)
        {
            TempData["Error"] = $"{form.Reference} is already past that step.";

            return RedirectToAction(nameof(Details), new { id = form.Id });
        }

        private static string Who(ProviderForm form) =>
            $"{form.Employee.FirstName} {form.Employee.LastName}";
    }
}

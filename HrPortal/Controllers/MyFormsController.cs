using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.Services.ProviderForms;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Where staff fill in and sign the outside providers' forms. Every
    /// page here is only about the person signed in.
    /// </summary>
    [Authorize]
    public class MyFormsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CurrentUserService _currentUser;
        private readonly ProviderFormService _forms;
        private readonly ProviderFormPdfService _pdf;
        private readonly AuditService _audit;

        public MyFormsController(
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
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            return View(await _context.ProviderForms
                .AsNoTracking()
                .Where(f => f.EmployeeId == employeeId.Value)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(ProviderFormKind kind)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            if (!ModelState.IsValid || !Enum.IsDefined(kind))
                return NotFound();

            var form = await _forms.StartAsync(employeeId.Value, kind);

            return RedirectToAction(nameof(Edit), new { id = form.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var form = await MineAsync(id);

            _context.ChangeTracker.Clear();

            if (form == null)
                return NotFound();

            if (!ProviderFormRules.WithEmployee(form.Status))
                return Locked(form);

            return View(Editing(form, form.Member));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProviderFormEditViewModel model)
        {
            var form = await MineAsync(id);

            if (form == null)
                return NotFound();

            if (!ProviderFormRules.WithEmployee(form.Status))
                return Locked(form);

            var member = model.Member ?? new ProviderFormMember();

            ProviderFormRules.Clean(form.Kind, member);

            foreach (var problem in ProviderFormRules.SaveProblems(member))
                ModelState.AddModelError(string.Empty, problem);

            if (!ModelState.IsValid)
            {
                return View(Editing(form, member));
            }

            form.Member = member;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{form.Reference} saved. Check it, then sign and send it.";

            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var form = await MineAsync(id);

            if (form == null)
                return NotFound();

            ViewBag.Problems = ProviderFormRules.MemberProblems(form.Kind, form.Member, DateTime.Today);

            return View(form);
        }

        public async Task<IActionResult> Form(int id, bool download = false)
        {
            var form = await MineAsync(id);

            if (form == null)
                return NotFound();

            return download
                ? File(_pdf.Render(form), "application/pdf", ProviderFormPdfService.FileName(form))
                : File(_pdf.Render(form), "application/pdf");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int id, string? signature, string? witness)
        {
            var form = await MineAsync(id);

            if (form == null)
                return NotFound();

            if (!ProviderFormRules.WithEmployee(form.Status))
                return Locked(form);

            if (ProviderFormRules.MemberProblems(form.Kind, form.Member, DateTime.Today).Count > 0)
            {
                TempData["Error"] = "Finish the form before you sign it. What is missing is listed below.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var signed = SignatureImage.Read(signature);
            var witnessed = form.Kind == ProviderFormKind.Minerva ? SignatureImage.Read(witness) : null;

            if (signed == null)
            {
                TempData["Error"] = "Sign the form in the box before you send it.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (form.Kind == ProviderFormKind.Minerva && witnessed == null)
            {
                TempData["Error"] = "The nomination page needs a witness to sign too.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await _forms.SubmitAsync(form, signed, witnessed);

            await _audit.LogAsync(
                AuditService.ProviderFormSubmitted,
                nameof(ProviderForm),
                form.Reference,
                $"{ProviderFormRules.Title(form.Kind)} signed and sent to HR.");

            TempData["Success"] = $"{form.Reference} has been signed and sent to HR.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var form = await MineAsync(id);

            if (form == null)
                return NotFound();

            if (form.Status != ProviderFormStatus.Draft)
                return Locked(form);

            _context.ProviderForms.Remove(form);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{form.Reference} has been deleted.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<ProviderForm?> MineAsync(int id)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            return employeeId == null
                ? null
                : await _context.ProviderForms
                    .Include(f => f.Employee)
                    .FirstOrDefaultAsync(f => f.Id == id && f.EmployeeId == employeeId.Value);
        }

        private IActionResult Locked(ProviderForm form)
        {
            TempData["Error"] = $"{form.Reference} has been sent, so it cannot be changed here.";

            return RedirectToAction(nameof(Details), new { id = form.Id });
        }

        /// <summary>
        /// The answers with blank lines added, so the page has every
        /// line the form does.
        /// </summary>
        private static ProviderFormEditViewModel Editing(ProviderForm form, ProviderFormMember member)
        {
            while (member.Dependants.Count < ProviderFormRules.MaxDependants)
                member.Dependants.Add(new ProviderFormPerson());

            while (member.Children.Count < ProviderFormRules.MaxChildren)
                member.Children.Add(new ProviderFormName());

            while (member.Nominees.Count < ProviderFormRules.MaxNominees)
                member.Nominees.Add(new ProviderFormName());

            return new ProviderFormEditViewModel
            {
                Id = form.Id,
                Kind = form.Kind,
                Reference = form.Reference,
                ReturnComment = form.ReturnComment,
                Member = member
            };
        }
    }
}

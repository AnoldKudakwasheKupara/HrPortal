using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize]
    public class PolicyController : Controller
    {
        private const string UploadFolder = "policies";

        private readonly ApplicationDbContext _context;
        private readonly FileStorageService _fileStorage;
        private readonly AuditService _audit;
        private readonly CurrentUserService _currentUser;
        private readonly PolicyService _policies;
        private readonly RecordDocumentPdfService _pdf;

        public PolicyController(
            ApplicationDbContext context,
            FileStorageService fileStorage,
            AuditService audit,
            CurrentUserService currentUser,
            PolicyService policies,
            RecordDocumentPdfService pdf)
        {
            _context = context;
            _fileStorage = fileStorage;
            _audit = audit;
            _currentUser = currentUser;
            _policies = policies;
            _pdf = pdf;
        }

        /// <summary>
        /// What is known about the policy, as a printable record.
        /// This is the cover sheet, not the policy itself -- Download
        /// still fetches the document.
        ///
        /// Reach goes through the same ReadableAsync the details page
        /// uses, so a policy somebody may not see is not one they can
        /// print the particulars of either.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            var policy = await _policies.ReadableAsync(id, User);

            if (policy == null)
                return NotFound();

            var record = new RecordDocument(
                "Policy Record",
                policy.Reference)
            {
                Headline = policy.Title,
                Caption = policy.Category,
                Chip = $"{policy.Status} · v{policy.Version}"
            };

            record.Section("Policy", "What it is and where it stands")
                .Add("Title", policy.Title)
                .Add("Reference", policy.Reference)
                .Add("Category", policy.Category)
                .Add("Version", policy.Version)
                .Add("Status", policy.Status.ToString())
                .Add("Audience", policy.Audience.ToString())
                .Add("Owner", policy.Owner);

            record.Section("Dates", "When it applies and when it is due")
                .Add("Effective date", policy.EffectiveDate)
                .Add("Review date", policy.ReviewDate)
                .Add(
                    "Acknowledgement required",
                    policy.RequiresAcknowledgement);

            record.Section("Document", "The file staff are given")
                .Add("File name", policy.OriginalFileName);

            record.Note(policy.Summary);

            return File(
                _pdf.Render(record),
                "application/pdf",
                record.FileName);
        }

        public async Task<IActionResult> Index(string? category, string? search)
        {
            var audiences = AudienceRules.Visible(User);
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            var query = _context.Policies
                .AsNoTracking()
                .Where(p =>
                    p.Status == PolicyStatus.Active &&
                    audiences.Contains(p.Audience));

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category == category);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();

                query = query.Where(p =>
                    p.Title.ToLower().Contains(term) ||
                    (p.Summary != null && p.Summary.ToLower().Contains(term)) ||
                    (p.Reference != null && p.Reference.ToLower().Contains(term)));
            }

            var policies = await query
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Title)
                .Select(p => new PolicyListViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Reference = p.Reference,
                    Category = p.Category,
                    Version = p.Version,
                    EffectiveDate = p.EffectiveDate,
                    ReviewDate = p.ReviewDate,
                    Audience = p.Audience,
                    RequiresAcknowledgement = p.RequiresAcknowledgement,
                    Acknowledged =
                        employeeId != null &&
                        p.Acknowledgements.Any(a => a.EmployeeId == employeeId),
                    AcknowledgedCount = p.Acknowledgements.Count
                })
                .ToListAsync();

            ViewBag.Categories = await _context.Policies
                .AsNoTracking()
                .Where(p =>
                    p.Status == PolicyStatus.Active &&
                    p.Category != null &&
                    p.Category != "")
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.CategoryFilter = category;
            ViewBag.Search = search;
            ViewBag.CanManage = PolicyService.CanManage(User);

            ViewBag.Outstanding = policies.Count(p =>
                p.RequiresAcknowledgement && !p.Acknowledged);

            return View(policies);
        }

        public async Task<IActionResult> Details(int? id)
        {
            var policy = await _policies.ReadableAsync(id, User);

            if (policy == null)
                return NotFound();

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            var model = new PolicyDetailsViewModel
            {
                Policy = policy,
                Acknowledged =
                    employeeId != null &&
                    policy.Acknowledgements
                        .Any(a => a.EmployeeId == employeeId),
                CanManage = PolicyService.CanManage(User)
            };

            if (model.CanManage)
            {
                var acknowledgedIds = policy.Acknowledgements
                    .Select(a => a.EmployeeId)
                    .ToList();

                model.Outstanding = await _context.Employees
                    .AsNoTracking()
                    .Where(e =>
                        e.IsActive &&
                        !acknowledgedIds.Contains(e.Id))
                    .OrderBy(e => e.FirstName)
                    .Select(e => e.FirstName + " " + e.LastName)
                    .ToListAsync();

                model.ActiveStaff = await _context.Employees
                    .CountAsync(e => e.IsActive);
            }

            return View(model);
        }

        public async Task<IActionResult> Download(int? id)
        {
            var policy = await _policies.ReadableAsync(id, User);

            if (policy == null)
                return NotFound();

            var fullPath = _fileStorage.ResolveExistingPath(policy.FilePath);

            if (fullPath == null)
            {
                TempData["Error"] = "That file is no longer on the server.";
                return RedirectToAction(nameof(Index));
            }

            new FileExtensionContentTypeProvider()
                .TryGetContentType(fullPath, out var contentType);

            return PhysicalFile(
                fullPath,
                contentType ?? "application/octet-stream",
                policy.OriginalFileName);
        }

        public async Task<IActionResult> Read(int? id)
        {
            var policy = await _policies.ReadableAsync(id, User);

            if (policy == null)
                return NotFound();

            if (!FileStorageService.CanPreview(policy.OriginalFileName))
                return RedirectToAction(nameof(Download), new { id });

            var fullPath = _fileStorage.ResolveExistingPath(policy.FilePath);

            if (fullPath == null)
            {
                TempData["Error"] = "That file is no longer on the server.";
                return RedirectToAction(nameof(Index));
            }

            new FileExtensionContentTypeProvider()
                .TryGetContentType(fullPath, out var contentType);

            Response.Headers.ContentDisposition =
                "inline; filename=\"" +
                Path.GetFileName(policy.OriginalFileName) + "\"";

            return PhysicalFile(fullPath, contentType ?? "application/octet-stream");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Acknowledge(int id)
        {
            var result = await _policies.AcknowledgeAsync(id, User);

            if (!result.Found)
                return NotFound();

            if (result.Succeeded)
                TempData["Success"] = result.Message;
            else
                TempData["Error"] = result.Message;

            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = AudienceRules.Curators)]
        public async Task<IActionResult> Manage(PolicyStatus? status)
        {
            var query = _context.Policies.AsNoTracking().AsQueryable();

            if (status.HasValue)
                query = query.Where(p => p.Status == status.Value);

            var policies = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PolicyListViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Reference = p.Reference,
                    Category = p.Category,
                    Version = p.Version,
                    Status = p.Status,
                    EffectiveDate = p.EffectiveDate,
                    ReviewDate = p.ReviewDate,
                    Audience = p.Audience,
                    RequiresAcknowledgement = p.RequiresAcknowledgement,
                    AcknowledgedCount = p.Acknowledgements.Count
                })
                .ToListAsync();

            ViewBag.StatusFilter = status;
            ViewBag.ActiveStaff = await _context.Employees
                .CountAsync(e => e.IsActive);

            return View(policies);
        }

        [Authorize(Roles = AudienceRules.Curators)]
        public async Task<IActionResult> Create(int? supersedes)
        {
            var model = new PolicyEditViewModel
            {
                EffectiveDate = DateTime.Today,
                ReviewDate = DateTime.Today.AddYears(1)
            };

            if (supersedes.HasValue)
            {
                var previous = await _context.Policies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == supersedes.Value);

                if (previous != null)
                {
                    model.Title = previous.Title;
                    model.Reference = previous.Reference;
                    model.Category = previous.Category;
                    model.Owner = previous.Owner;
                    model.Summary = previous.Summary;
                    model.Audience = previous.Audience;
                    model.RequiresAcknowledgement =
                        previous.RequiresAcknowledgement;
                    model.SupersedesPolicyId = previous.Id;
                    model.Version = PolicyService.NextVersion(previous.Version);
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AudienceRules.Curators)]
        public async Task<IActionResult> Create(PolicyEditViewModel model)
        {
            var fileError = FileStorageService.Validate(model.File);

            if (fileError != null)
                ModelState.AddModelError(nameof(model.File), fileError);

            var dateError = PolicyService.ValidateDates(
                model.EffectiveDate,
                model.ReviewDate);

            if (dateError != null)
            {
                ModelState.AddModelError(
                    PolicyService.ReviewDateField,
                    dateError);
            }

            if (!ModelState.IsValid)
                return View(model);

            var (fileName, filePath) =
                await _fileStorage.SaveAsync(model.File!, UploadFolder);

            var policy = new Policy
            {
                Title = model.Title.Trim(),
                Reference = model.Reference?.Trim(),
                Category = model.Category?.Trim(),
                Version = model.Version.Trim(),
                Status = PolicyStatus.Draft,
                Audience = model.Audience,
                Owner = model.Owner?.Trim(),
                Summary = model.Summary?.Trim(),
                EffectiveDate = model.EffectiveDate,
                ReviewDate = model.ReviewDate,
                RequiresAcknowledgement = model.RequiresAcknowledgement,
                FileName = fileName,
                OriginalFileName = Path.GetFileName(model.File!.FileName),
                FilePath = filePath,
                SupersedesPolicyId = model.SupersedesPolicyId,
                CreatedAt = DateTime.UtcNow,
                CreatedByName = await _currentUser.GetDisplayNameAsync()
            };

            _context.Policies.Add(policy);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(Policy),
                policy.Id.ToString(),
                $"{policy.Title} version {policy.Version} drafted.");

            TempData["Success"] =
                "Policy drafted. It is not visible to staff until it is " +
                "published.";

            return RedirectToAction(nameof(Manage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AudienceRules.Curators)]
        public async Task<IActionResult> Publish(int id)
        {
            var result = await _policies.PublishAsync(id);

            if (!result.Found)
                return NotFound();

            if (result.Succeeded)
                TempData["Success"] = result.Message;
            else
                TempData["Error"] = result.Message;

            return RedirectToAction(nameof(Manage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AudienceRules.Curators)]
        public async Task<IActionResult> Withdraw(int id)
        {
            var result = await _policies.WithdrawAsync(id);

            if (!result.Found)
                return NotFound();

            TempData["Success"] = result.Message;

            return RedirectToAction(nameof(Manage));
        }
    }
}

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
        private readonly UserManager<ApplicationUser> _userManager;

        public PolicyController(
            ApplicationDbContext context,
            FileStorageService fileStorage,
            AuditService audit,
            CurrentUserService currentUser,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _fileStorage = fileStorage;
            _audit = audit;
            _currentUser = currentUser;
            _userManager = userManager;
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
                var term = search.Trim();

                query = query.Where(p =>
                    p.Title.Contains(term) ||
                    (p.Summary != null && p.Summary.Contains(term)) ||
                    (p.Reference != null && p.Reference.Contains(term)));
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
            ViewBag.CanManage = CanManage();

            ViewBag.Outstanding = policies.Count(p =>
                p.RequiresAcknowledgement && !p.Acknowledged);

            return View(policies);
        }

        public async Task<IActionResult> Details(int? id)
        {
            var policy = await Readable(id);

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
                CanManage = CanManage()
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
            var policy = await Readable(id);

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
            var policy = await Readable(id);

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
            var policy = await Readable(id);

            if (policy == null)
                return NotFound();

            if (policy.Status != PolicyStatus.Active)
            {
                TempData["Error"] =
                    "That policy is not in force, so there is nothing " +
                    "to acknowledge.";

                return RedirectToAction(nameof(Details), new { id });
            }

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
            {
                TempData["Error"] =
                    "Your sign-in is not linked to an employee record, " +
                    "so an acknowledgement cannot be recorded against it.";

                return RedirectToAction(nameof(Details), new { id });
            }

            var already = await _context.PolicyAcknowledgements
                .AnyAsync(a =>
                    a.PolicyId == id &&
                    a.EmployeeId == employeeId.Value);

            if (already)
            {
                TempData["Success"] = "You had already acknowledged this.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.PolicyAcknowledgements.Add(new PolicyAcknowledgement
            {
                PolicyId = id,
                EmployeeId = employeeId.Value,
                AcknowledgedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(PolicyAcknowledgement),
                id.ToString(),
                $"{policy.Title} version {policy.Version} acknowledged.");

            TempData["Success"] =
                $"Thank you. Your reading of {policy.Title} " +
                $"version {policy.Version} has been recorded.";

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
                    model.Version = NextVersion(previous.Version);
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

            Validate(model);

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
                CreatedByName = await CurrentUserNameAsync()
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
            var policy = await _context.Policies.FindAsync(id);

            if (policy == null)
                return NotFound();

            if (policy.Status == PolicyStatus.Active)
            {
                TempData["Error"] = "That policy is already in force.";
                return RedirectToAction(nameof(Manage));
            }

            policy.Status = PolicyStatus.Active;
            policy.PublishedAt = DateTime.UtcNow;

            var replaced = string.Empty;

            if (policy.SupersedesPolicyId.HasValue)
            {
                var previous = await _context.Policies
                    .FindAsync(policy.SupersedesPolicyId.Value);

                if (previous != null &&
                    previous.Status == PolicyStatus.Active)
                {
                    previous.Status = PolicyStatus.Superseded;

                    replaced =
                        $" It replaces version {previous.Version}, which " +
                        "is now marked as replaced.";
                }
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(Policy),
                policy.Id.ToString(),
                $"{policy.Title} version {policy.Version} published." +
                replaced);

            TempData["Success"] =
                $"{policy.Title} version {policy.Version} is now in " +
                "force." + replaced;

            return RedirectToAction(nameof(Manage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AudienceRules.Curators)]
        public async Task<IActionResult> Withdraw(int id)
        {
            var policy = await _context.Policies.FindAsync(id);

            if (policy == null)
                return NotFound();

            policy.Status = PolicyStatus.Withdrawn;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(Policy),
                policy.Id.ToString(),
                $"{policy.Title} version {policy.Version} withdrawn.");

            TempData["Success"] = "Policy withdrawn.";

            return RedirectToAction(nameof(Manage));
        }

        private void Validate(PolicyEditViewModel model)
        {
            if (model.ReviewDate.HasValue &&
                model.ReviewDate.Value.Date <= model.EffectiveDate.Date)
            {
                ModelState.AddModelError(
                    nameof(model.ReviewDate),
                    "The review date must be after the date it takes " +
                    "effect.");
            }
        }

        private static string NextVersion(string version)
        {
            var parts = version.Split('.');

            if (int.TryParse(parts[0], out var major))
            {
                return parts.Length > 1
                    ? $"{major + 1}.0"
                    : (major + 1).ToString();
            }

            return version;
        }

        private bool CanManage() =>
            User.IsInRole(Roles.Administrator) || User.IsInRole(Roles.HrOfficer);

        private async Task<Policy?> Readable(int? id)
        {
            if (id == null)
                return null;

            var policy = await _context.Policies
                .AsNoTracking()
                .Include(p => p.Supersedes)
                .Include(p => p.Acknowledgements)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (policy == null)
                return null;

            if (!AudienceRules.CanSee(User, policy.Audience))
                return null;

            if (policy.Status != PolicyStatus.Active && !CanManage())
                return null;

            return policy;
        }

        private async Task<string?> CurrentUserNameAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            return user?.FullName ?? user?.Email ?? User.Identity?.Name;
        }
    }
}

using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// The register of complaints from clients, for a company whose
    /// brand keeps one. Everybody who works there can read it, record a
    /// complaint they received and say what was done about it.
    /// </summary>
    [Authorize]
    public class ComplaintController : Controller
    {
        private const int PageSize = 10;

        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;
        private readonly CurrentUserService _currentUser;

        public ComplaintController(
            ApplicationDbContext context,
            AuditService audit,
            CurrentUserService currentUser)
        {
            _context = context;
            _audit = audit;
            _currentUser = currentUser;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!Brand.Current.ComplaintsRegister)
                context.Result = NotFound();
        }

        public async Task<IActionResult> Index(
            string? q,
            ComplaintStatus? status,
            int page = 1)
        {
            var query = _context.Complaints.AsNoTracking();

            if (status.HasValue)
                query = query.Where(c => c.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";

                query = query.Where(c =>
                    EF.Functions.ILike(c.ClientName, term) ||
                    EF.Functions.ILike(c.ClientEmail ?? "", term) ||
                    EF.Functions.ILike(c.ClientPhone ?? "", term) ||
                    EF.Functions.ILike(c.Nature ?? "", term) ||
                    EF.Functions.ILike(c.StaffInvolved ?? "", term));
            }

            var complaints = await query
                .OrderBy(c => c.Status)
                .ThenByDescending(c => c.ReceivedOn ?? c.RecordedAt)
                .ThenByDescending(c => c.Id)
                .ToPagedResultAsync(page, PageSize);

            var all = await _context.Complaints
                .AsNoTracking()
                .Select(c => new { c.Status, c.ReceivedOn, c.ResolvedOn })
                .ToListAsync();

            var timed = all
                .Where(c => c.ReceivedOn != null && c.ResolvedOn != null && c.ResolvedOn >= c.ReceivedOn)
                .Select(c => (c.ResolvedOn!.Value - c.ReceivedOn!.Value).TotalDays)
                .ToList();

            ViewBag.Search = q;
            ViewBag.StatusFilter = status;
            ViewBag.Total = all.Count;
            ViewBag.Pending = all.Count(c => c.Status == ComplaintStatus.Pending);
            ViewBag.Resolved = all.Count(c => c.Status == ComplaintStatus.Resolved);
            ViewBag.ThisYear = all.Count(c => c.ReceivedOn?.Year == DateTime.Today.Year);
            ViewBag.AverageDays = timed.Count == 0 ? (double?)null : timed.Average();

            return View(complaints);
        }

        public async Task<IActionResult> Details(int id)
        {
            var complaint = await _context.Complaints
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            return complaint == null ? NotFound() : View(complaint);
        }

        public IActionResult Create() =>
            View(new ComplaintFormViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ComplaintFormViewModel model)
        {
            Validate(model);

            if (!ModelState.IsValid)
                return View(model);

            var complaint = new Complaint
            {
                RecordedAt = DateTime.UtcNow,
                RecordedBy = await _currentUser.GetDisplayNameAsync()
            };

            Copy(model, complaint);

            _context.Complaints.Add(complaint);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(Complaint),
                complaint.Id.ToString(),
                $"Complaint from {complaint.ClientName} recorded.");

            TempData["Success"] = "Complaint recorded.";

            return RedirectToAction(nameof(Details), new { id = complaint.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var c = await _context.Complaints.FirstOrDefaultAsync(x => x.Id == id);

            if (c == null)
                return NotFound();

            return View(new ComplaintFormViewModel
            {
                Id = c.Id,
                ReceivedOn = c.ReceivedOn,
                ClientName = c.ClientName,
                ClientPhone = c.ClientPhone,
                ClientEmail = c.ClientEmail,
                Nature = c.Nature,
                StaffInvolved = c.StaffInvolved,
                ActionTaken = c.ActionTaken,
                Status = c.Status,
                ResolvedOn = c.ResolvedOn,
                Remarks = c.Remarks,
                RecordedBy = c.RecordedBy,
                RecordedAt = c.RecordedAt
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ComplaintFormViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            var complaint = await _context.Complaints.FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
                return NotFound();

            Validate(model);

            if (!ModelState.IsValid)
            {
                model.RecordedBy = complaint.RecordedBy;
                model.RecordedAt = complaint.RecordedAt;
                return View(model);
            }

            var wasResolved = complaint.Status == ComplaintStatus.Resolved;

            Copy(model, complaint);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(Complaint),
                complaint.Id.ToString(),
                !wasResolved && complaint.Status == ComplaintStatus.Resolved
                    ? $"Complaint from {complaint.ClientName} resolved."
                    : $"Complaint from {complaint.ClientName} updated.");

            TempData["Success"] = "Complaint updated.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var complaint = await _context.Complaints.FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
                return NotFound();

            _context.Complaints.Remove(complaint);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(Complaint),
                id.ToString(),
                $"Complaint from {complaint.ClientName} deleted.");

            TempData["Success"] = $"The complaint from {complaint.ClientName} was deleted.";

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Import() => View();

        /// <summary>
        /// Loads a register kept in a spreadsheet. Complaints already in
        /// the portal are left alone, so the same sheet can be loaded
        /// again once more rows have been added to it.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Import(IFormFile? file)
        {
            var extension = Path.GetExtension(file?.FileName ?? string.Empty).ToLowerInvariant();

            if (file == null || file.Length == 0 || extension is not (".xlsx" or ".csv"))
            {
                ViewBag.Error = "Choose the register as an Excel workbook (.xlsx) or a CSV file.";
                return View();
            }

            ComplaintImport read;

            try
            {
                await using var stream = file.OpenReadStream();
                read = ComplaintImporter.Read(stream, file.FileName);
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
            {
                ViewBag.Error = "That file could not be read as a spreadsheet. Save it again as .xlsx or .csv and try once more.";
                return View();
            }

            var existing = (await _context.Complaints.AsNoTracking().ToListAsync())
                .Select(ComplaintImporter.Key)
                .ToHashSet();

            var by = await _currentUser.GetDisplayNameAsync();
            var added = 0;
            var skipped = 0;

            foreach (var complaint in read.Complaints)
            {
                if (!existing.Add(ComplaintImporter.Key(complaint)))
                {
                    skipped++;
                    continue;
                }

                complaint.RecordedAt = DateTime.UtcNow;
                complaint.RecordedBy = by;

                _context.Complaints.Add(complaint);
                added++;
            }

            await _context.SaveChangesAsync();

            if (added > 0)
            {
                await _audit.LogAsync(
                    AuditService.Created,
                    nameof(Complaint),
                    file.FileName,
                    $"{added} complaint(s) loaded from {file.FileName}.");
            }

            TempData["Success"] =
                $"{added} complaint{(added == 1 ? "" : "s")} added" +
                (skipped > 0 ? $"; {skipped} already in the register, so left alone." : ".");

            if (read.Problems.Count > 0)
                TempData["Error"] = string.Join(" ", read.Problems);

            return RedirectToAction(nameof(Index));
        }

        private void Validate(ComplaintFormViewModel model)
        {
            if (model.ReceivedOn > DateTime.Today)
                ModelState.AddModelError(nameof(model.ReceivedOn), "The date received cannot be in the future.");

            if (model.Status == ComplaintStatus.Resolved && model.ResolvedOn > DateTime.Today)
                ModelState.AddModelError(nameof(model.ResolvedOn), "The resolution date cannot be in the future.");

            if (model.Status == ComplaintStatus.Resolved &&
                model.ResolvedOn != null && model.ReceivedOn != null &&
                model.ResolvedOn < model.ReceivedOn)
            {
                ModelState.AddModelError(nameof(model.ResolvedOn), "It cannot be resolved before it was received.");
            }
        }

        private static void Copy(ComplaintFormViewModel model, Complaint complaint)
        {
            complaint.ReceivedOn = model.ReceivedOn;
            complaint.ClientName = model.ClientName.Trim();
            complaint.ClientPhone = Clean(model.ClientPhone);
            complaint.ClientEmail = Clean(model.ClientEmail);
            complaint.Nature = Clean(model.Nature);
            complaint.StaffInvolved = Clean(model.StaffInvolved);
            complaint.ActionTaken = Clean(model.ActionTaken);
            complaint.Status = model.Status;
            complaint.ResolvedOn = model.Status == ComplaintStatus.Resolved
                ? model.ResolvedOn ?? DateTime.Today
                : null;
            complaint.Remarks = Clean(model.Remarks);
        }

        private static string? Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

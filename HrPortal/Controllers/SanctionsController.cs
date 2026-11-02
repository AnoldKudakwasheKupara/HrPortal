using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.Services.Sanctions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Sanctions screening, for compliance officers only. Nobody else
    /// can open any of it, administrators included, unless they are
    /// given the Compliance Officer role.
    /// </summary>
    [Authorize(Roles = Roles.ComplianceOnly)]
    public class SanctionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SanctionsService _sanctions;
        private readonly SanctionsReportPdfService _reports;
        private readonly FileStorageService _files;
        private readonly ILogger<SanctionsController> _logger;

        public SanctionsController(
            ApplicationDbContext context,
            SanctionsService sanctions,
            SanctionsReportPdfService reports,
            FileStorageService files,
            ILogger<SanctionsController> logger)
        {
            _context = context;
            _sanctions = sanctions;
            _reports = reports;
            _files = files;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.RegisterSize = await _context.SanctionsRegister.CountAsync(r => r.IsActive);

            ViewBag.ClientList = await _context.SanctionsClientLists
                .AsNoTracking()
                .Include(c => c.Clients)
                .OrderByDescending(c => c.UploadedAt)
                .FirstOrDefaultAsync();

            ViewBag.OpenMatters = await _context.SanctionsConcerns
                .CountAsync(c => c.Status == SanctionsConcernStatus.Open);

            return View(await _context.SanctionsLists
                .AsNoTracking()
                .Include(l => l.Changes)
                .Include(l => l.Concerns)
                .OrderByDescending(l => l.UploadedAt)
                .ToListAsync());
        }

        public IActionResult Upload() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(FileStorageService.MaxFileSizeBytes + 1024 * 1024)]
        public async Task<IActionResult> Upload(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                ViewBag.Error = "Choose the PDF the FIU sent.";
                return View();
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = "Upload the document as a PDF.";
                return View();
            }

            if (file.Length > FileStorageService.MaxFileSizeBytes)
            {
                ViewBag.Error = "The file is larger than 10 MB.";
                return View();
            }

            try
            {
                var list = await _sanctions.UploadAsync(file);

                return RedirectToAction(nameof(Review), new { id = list.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A sanctions document could not be read.");

                ViewBag.Error =
                    "The PDF could not be read. It may be damaged, protected or a " +
                    "scanned image without text.";

                return View();
            }
        }

        public async Task<IActionResult> Review(int id)
        {
            var list = await _context.SanctionsLists
                .AsNoTracking()
                .Include(l => l.Entries)
                .Include(l => l.Changes)
                .Include(l => l.Concerns)
                .Include(l => l.ClientList)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (list == null)
                return NotFound();

            ViewBag.Others = await _context.SanctionsLists
                .AsNoTracking()
                .Where(l => l.Id != id)
                .OrderByDescending(l => l.UploadedAt)
                .Select(l => new { l.Id, l.ReviewNumber, l.Title, l.UploadedAt })
                .ToListAsync();

            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(int id, SanctionsListKind kind, int[]? removed)
        {
            try
            {
                await _sanctions.ApplyAsync(id, kind, (removed ?? Array.Empty<int>()).ToHashSet());

                TempData["Success"] =
                    "Applied to the register and screened. Resolve each matter, then sign off.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Review), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rescreen(int id)
        {
            var list = await _context.SanctionsLists.FindAsync(id);

            if (list == null)
                return NotFound();

            if (list.Status != SanctionsReviewStatus.InReview)
            {
                TempData["Error"] = "Only a review in progress can be screened again.";
                return RedirectToAction(nameof(Review), new { id });
            }

            var raised = await _sanctions.ScreenAsync(list);

            TempData["Success"] = raised == 0
                ? "Screened again. Nothing new was found."
                : $"Screened again. {raised} new matter(s) raised.";

            return RedirectToAction(nameof(Review), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(
            int id,
            int concernId,
            SanctionsConcernStatus outcome,
            string? resolution)
        {
            if (outcome == SanctionsConcernStatus.Open || string.IsNullOrWhiteSpace(resolution))
            {
                TempData["Error"] = "Choose an outcome and write down what was found.";
                return RedirectToAction(nameof(Review), new { id });
            }

            try
            {
                await _sanctions.ResolveAsync(concernId, outcome, resolution);
                TempData["Success"] = "Matter recorded.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Review), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignOff(int id, string? note)
        {
            try
            {
                await _sanctions.SignOffAsync(id, note);
                TempData["Success"] = "Signed off. The report has been made and kept.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Review), new { id });
        }

        /// <summary>
        /// The kept report once signed off; until then a preview,
        /// marked as such by its file name and not kept.
        /// </summary>
        public async Task<IActionResult> Report(int id)
        {
            var list = await _context.SanctionsLists.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);

            if (list == null)
                return NotFound();

            if (list.Status == SanctionsReviewStatus.SignedOff && list.ReportPath != null)
            {
                var path = _files.ResolveExistingPath(list.ReportPath);

                if (path == null)
                    return NotFound();

                return PhysicalFile(path, "application/pdf", $"{list.ReviewNumber} Sanctions Screening Report.pdf");
            }

            var full = await _sanctions.LoadForReportAsync(id);

            return File(
                _reports.Render(full),
                "application/pdf",
                $"{list.ReviewNumber} Sanctions Screening Report PREVIEW.pdf");
        }

        public async Task<IActionResult> Document(int id)
        {
            var list = await _context.SanctionsLists.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);

            var path = list == null ? null : _files.ResolveExistingPath(list.FilePath);

            if (path == null)
                return NotFound();

            return PhysicalFile(path, "application/pdf", list!.OriginalFileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var list = await _context.SanctionsLists.FindAsync(id);

            if (list == null)
                return NotFound();

            if (list.Status != SanctionsReviewStatus.Pending)
            {
                TempData["Error"] =
                    "Only a document not yet applied can be deleted. Applied documents are " +
                    "part of the register's history.";

                return RedirectToAction(nameof(Review), new { id });
            }

            _context.SanctionsLists.Remove(list);
            await _context.SaveChangesAsync();
            _files.Delete(list.FilePath);

            TempData["Success"] = $"{list.ReviewNumber} deleted.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Register(string? search, bool removed = false)
        {
            var entries = _context.SanctionsRegister.AsNoTracking().Where(r => r.IsActive != removed);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";

                entries = entries.Where(r =>
                    EF.Functions.ILike(r.Reference, term) ||
                    EF.Functions.ILike(r.Name, term) ||
                    (r.Aliases != null && EF.Functions.ILike(r.Aliases, term)) ||
                    (r.Identifiers != null && EF.Functions.ILike(r.Identifiers, term)));
            }

            ViewBag.Search = search;
            ViewBag.Removed = removed;

            return View(await entries.OrderBy(r => r.Reference).Take(500).ToListAsync());
        }

        public async Task<IActionResult> Compare(int? a, int? b)
        {
            var lists = await _context.SanctionsLists
                .AsNoTracking()
                .OrderByDescending(l => l.UploadedAt)
                .ToListAsync();

            ViewBag.Lists = lists;
            ViewBag.A = a;
            ViewBag.B = b;

            if (a == null || b == null || a == b)
                return View(new List<SanctionsComparisonRow>());

            return View(await _sanctions.CompareAsync(a.Value, b.Value));
        }

        public async Task<IActionResult> Clients()
        {
            return View(await _context.SanctionsClientLists
                .AsNoTracking()
                .Include(c => c.Clients)
                .OrderByDescending(c => c.UploadedAt)
                .ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clients(IFormFile? file)
        {
            var extension = Path.GetExtension(file?.FileName ?? string.Empty).ToLowerInvariant();

            if (file == null || file.Length == 0 || extension is not (".xlsx" or ".csv"))
            {
                TempData["Error"] = "Choose the client list as an Excel workbook (.xlsx) or a CSV file.";
                return RedirectToAction(nameof(Clients));
            }

            try
            {
                var list = await _sanctions.UploadClientsAsync(file);

                TempData["Success"] = list.Clients.Count == 0
                    ? "The file was kept, but no clients could be read from it. Check it has a Name column."
                    : $"{list.Clients.Count} clients read. They will be screened from the next review, " +
                      "or use Screen again on a review in progress.";
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or IOException)
            {
                TempData["Error"] = "The file could not be read. " + ex.Message;
            }

            return RedirectToAction(nameof(Clients));
        }

        public IActionResult Verify() => View();

        /// <summary>
        /// Checks a report somebody has been shown against the ones the
        /// portal made. A copy that has been altered in any way will
        /// not match.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                ViewBag.Error = "Choose the report to check.";
                return View();
            }

            using var memory = new MemoryStream();
            await file.CopyToAsync(memory);

            var hash = SanctionsService.Sha256Of(memory.ToArray());

            ViewBag.Hash = hash;
            ViewBag.Match = await _context.SanctionsLists
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.ReportSha256 == hash);

            return View();
        }
    }
}

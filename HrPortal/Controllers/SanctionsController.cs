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
    ///
    /// The start page runs a review as three steps the officer watches
    /// one after another: upload and read, compare, check people. Each
    /// is its own request so the page can show what each one found.
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
            ViewBag.LastDocument = await _context.SanctionsDocuments
                .AsNoTracking()
                .OrderByDescending(d => d.Id)
                .FirstOrDefaultAsync();

            ViewBag.ClientList = await _context.SanctionsClientLists
                .AsNoTracking()
                .Include(c => c.Clients)
                .OrderByDescending(c => c.UploadedAt)
                .FirstOrDefaultAsync();

            return View(await _context.SanctionsReviews
                .AsNoTracking()
                .Include(r => r.NewDocument)
                .Include(r => r.PreviousDocument)
                .Include(r => r.Changes)
                .Include(r => r.Concerns)
                .OrderByDescending(r => r.Id)
                .Take(50)
                .ToListAsync());
        }

        /// <summary>Step one: keep and read a document.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(FileStorageService.MaxFileSizeBytes + 1024 * 1024)]
        public async Task<IActionResult> UploadDocument(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Choose the PDF the FIU sent." });

            if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = $"{file.FileName} is not a PDF." });

            if (file.Length > FileStorageService.MaxFileSizeBytes)
                return BadRequest(new { error = $"{file.FileName} is larger than 10 MB." });

            try
            {
                var document = await _sanctions.UploadAsync(file);

                return Json(new
                {
                    id = document.Id,
                    label = document.Label,
                    title = document.Title,
                    entries = document.Entries.Count,
                    individuals = document.Entries.Count(e => !e.IsEntity),
                    entities = document.Entries.Count(e => e.IsEntity),
                    warnings = SanctionsService.Lines(document.ReadingWarnings)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A sanctions document could not be read.");

                return BadRequest(new
                {
                    error = $"{file.FileName} could not be read. It may be damaged, protected " +
                            "or a scanned image without text."
                });
            }
        }

        /// <summary>
        /// Step two: compare. Without a previous document given, the
        /// one uploaded just before the new one is used.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Compare(int newDocumentId, int? previousDocumentId)
        {
            if (!await _context.SanctionsDocuments.AnyAsync(d => d.Id == newDocumentId))
                return BadRequest(new { error = "The new document was not found." });

            previousDocumentId ??= (await _sanctions.PreviousOfAsync(newDocumentId))?.Id;

            var review = await _sanctions.CompareAsync(newDocumentId, previousDocumentId);

            var previous = previousDocumentId == null
                ? null
                : await _context.SanctionsDocuments.AsNoTracking().FirstAsync(d => d.Id == previousDocumentId);

            return Json(new
            {
                reviewId = review.Id,
                reviewNumber = review.ReviewNumber,
                previous = previous == null ? null : new { previous.Label, entries = await _context.SanctionsEntries.CountAsync(e => e.SanctionsDocumentId == previous.Id) },
                added = review.Changes.Where(c => c.Type == SanctionsChangeType.Added).Select(c => $"{c.Reference} {c.Name}"),
                removed = review.Changes.Where(c => c.Type == SanctionsChangeType.Removed).Select(c => $"{c.Reference} {c.Name}"),
                changed = review.Changes.Where(c => c.Type == SanctionsChangeType.Changed).Select(c => $"{c.Reference} {c.Name}"),
                discrepancies = review.Concerns
                    .Where(c => c.Category == SanctionsConcernCategory.Discrepancy)
                    .Select(c => c.Reason)
            });
        }

        /// <summary>Step three: check staff, vendors and clients.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Screen(int reviewId)
        {
            var review = await _sanctions.ScreenAsync(reviewId);

            var matches = review.Concerns
                .Where(c => c.Category == SanctionsConcernCategory.PossibleMatch)
                .ToList();

            return Json(new
            {
                staff = review.ScreenedStaff,
                vendors = review.ScreenedVendors,
                clients = review.ScreenedClients,
                matches = matches.Select(c => $"{c.SubjectType} {c.SubjectName} resembles {c.Reference} {c.ListedName}"),
                carriedForward = matches.Count(c => c.ResolvedByName == SanctionsService.CarriedForwardBy),
                findings = SanctionsFindings.Sentences(await _sanctions.LoadAsync(reviewId))
            });
        }

        public async Task<IActionResult> Result(int id)
        {
            if (!await _context.SanctionsReviews.AnyAsync(r => r.Id == id))
                return NotFound();

            return View(await _sanctions.LoadAsync(id));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckAgain(int id)
        {
            var review = await _context.SanctionsReviews.FindAsync(id);

            if (review == null)
                return NotFound();

            if (review.FinalisedAt != null)
            {
                TempData["Error"] = "This review has been finalised.";
                return RedirectToAction(nameof(Result), new { id });
            }

            await _sanctions.ScreenAsync(id);

            TempData["Success"] = "Staff, vendors and clients checked again.";

            return RedirectToAction(nameof(Result), new { id });
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
                return RedirectToAction(nameof(Result), new { id });
            }

            try
            {
                await _sanctions.ResolveAsync(concernId, outcome, resolution);
                TempData["Success"] = "Recorded.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Result), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finalise(int id, string? note)
        {
            try
            {
                await _sanctions.FinaliseAsync(id, note);
                TempData["Success"] = "Finalised. The report has been made and kept.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Result), new { id });
        }

        /// <summary>
        /// The kept report once finalised; until then a preview, named
        /// as such and not kept.
        /// </summary>
        public async Task<IActionResult> Report(int id)
        {
            var review = await _context.SanctionsReviews.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);

            if (review == null)
                return NotFound();

            if (review.FinalisedAt != null && review.ReportPath != null)
            {
                var path = _files.ResolveExistingPath(review.ReportPath);

                return path == null
                    ? NotFound()
                    : PhysicalFile(path, "application/pdf", $"{review.ReviewNumber} Sanctions Screening Report.pdf");
            }

            return File(
                _reports.Render(await _sanctions.LoadAsync(id)),
                "application/pdf",
                $"{review.ReviewNumber} Sanctions Screening Report PREVIEW.pdf");
        }

        public async Task<IActionResult> Document(int id)
        {
            var document = await _context.SanctionsDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);

            var path = document == null ? null : _files.ResolveExistingPath(document.FilePath);

            return path == null
                ? NotFound()
                : PhysicalFile(path, "application/pdf", document!.OriginalFileName);
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
                    : $"{list.Clients.Count} clients read. They will be checked from the next comparison.";
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
            ViewBag.Match = await _context.SanctionsReviews
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReportSha256 == hash);

            return View();
        }
    }
}

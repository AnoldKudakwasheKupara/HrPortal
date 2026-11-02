using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.Services.Sanctions;
using HrPortal.ViewModels;
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
                var (document, alreadyUploaded) = await _sanctions.UploadAsync(file);

                return Json(new
                {
                    id = document.Id,
                    label = document.Label,
                    title = document.Title,
                    kind = SanctionsText.Kind(document.Kind),
                    listName = document.ListName,
                    pages = document.PageCount,
                    entries = document.Entries.Count,
                    individuals = document.Entries.Count(e => !e.IsEntity),
                    entities = document.Entries.Count(e => e.IsEntity),
                    marked = document.Entries.Count(e => e.AddedText != null || e.RemovedText != null),
                    alreadyUploaded = alreadyUploaded
                        ? $"This file was already uploaded on {document.UploadedAt.ToLocalTime():d MMM yyyy HH:mm}; the copy kept then is used."
                        : null,
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

            if (previousDocumentId == newDocumentId)
                return BadRequest(new { error = "Both uploads are the same file. Choose two different documents." });

            var review = await _sanctions.CompareAsync(newDocumentId, previousDocumentId);

            var current = await _context.SanctionsDocuments.AsNoTracking().FirstAsync(d => d.Id == newDocumentId);

            var previous = review.PreviousDocumentId == null
                ? null
                : await _context.SanctionsDocuments.AsNoTracking().FirstAsync(d => d.Id == review.PreviousDocumentId);

            return Json(new
            {
                reviewId = review.Id,
                reviewNumber = review.ReviewNumber,
                isNotice = current.Kind != SanctionsDocumentKind.FullList,
                previous = previous == null ? null : new { previous.Label, entries = await _context.SanctionsEntries.CountAsync(e => e.SanctionsDocumentId == previous.Id) },
                added = review.Changes.Where(c => c.Type == SanctionsChangeType.Added).Select(c => $"{c.Reference} {c.Name}"),
                removed = review.Changes.Where(c => c.Type == SanctionsChangeType.Removed).Select(c => $"{c.Reference} {c.Name}"),
                changed = review.Changes.Where(c => c.Type == SanctionsChangeType.Changed).Select(c => $"{c.Reference} {c.Name}"),
                unchanged = review.UnchangedCount,
                notCompared = review.NotComparedCount,
                inconclusive = review.Inconclusive,
                notes = SanctionsService.Lines(review.ComparisonNote),
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
            if (!await _context.SanctionsReviews.AnyAsync(r => r.Id == reviewId && r.FinalisedAt == null))
                return BadRequest(new { error = "This comparison was not found or has been finalised." });

            var review = await _sanctions.ScreenAsync(reviewId);

            var matches = review.Concerns
                .Where(c => c.Category == SanctionsConcernCategory.PossibleMatch)
                .ToList();

            object Group(params string[] types)
            {
                var found = matches.Where(c => types.Contains(c.SubjectType)).ToList();
                var raised = found.Where(c => c.Strength != SanctionsMatchStrength.SetAside).ToList();

                return new
                {
                    matches = raised
                        .OrderBy(c => c.Strength)
                        .ThenByDescending(c => c.Score)
                        .Select(c => $"{(c.Strength == SanctionsMatchStrength.Exact ? "Exact match" : "Potential match")}: " +
                                     $"{c.SubjectName} and {c.Reference} {c.ListedName}" +
                                     (c.ResolvedByName == SanctionsService.CarriedForwardBy ? " (cleared before, unchanged)" : "")),
                    setAside = found.Count - raised.Count
                };
            }

            return Json(new
            {
                staff = review.ScreenedStaff,
                vendors = review.ScreenedVendors,
                clients = review.ScreenedClients,
                hasClientList = review.ClientListId != null,
                staffResult = Group(SanctionsService.Staff),
                vendorResult = Group(SanctionsService.Vendor, SanctionsService.VendorContact),
                clientResult = Group(SanctionsService.Client),
                findings = SanctionsFindings.Sentences(await _sanctions.LoadAsync(reviewId))
            });
        }

        public async Task<IActionResult> Result(int id)
        {
            if (!await _context.SanctionsReviews.AnyAsync(r => r.Id == id))
                return NotFound();

            var review = await _sanctions.LoadAsync(id);

            var references = review.Concerns
                .Where(c => c.Reference != null)
                .Select(c => c.Reference!)
                .Distinct()
                .ToList();

            var model = new SanctionsResultViewModel
            {
                Review = review,
                FinaliseBlockers = SanctionsService.FinaliseBlockers(review),
                Entries = (await _context.SanctionsEntries
                        .AsNoTracking()
                        .Where(e => e.SanctionsDocumentId == review.NewDocumentId && references.Contains(e.Reference))
                        .ToListAsync())
                    .GroupBy(e => e.Reference)
                    .ToDictionary(g => g.Key, g => g.First())
            };

            List<int> Ids(params string[] types) => review.Concerns
                .Where(c => c.SubjectId != null && types.Contains(c.SubjectType))
                .Select(c => c.SubjectId!.Value)
                .Distinct()
                .ToList();

            static string Date(DateTime? d) => d?.ToString("d MMM yyyy") ?? "Not recorded";

            var staffIds = Ids(SanctionsService.Staff);

            foreach (var e in await _context.Employees
                         .AsNoTracking()
                         .Include(e => e.Department)
                         .Where(e => staffIds.Contains(e.Id))
                         .ToListAsync())
            {
                model.Subjects[SanctionsResultViewModel.SubjectKey(SanctionsService.Staff, e.Id)] = new()
                {
                    ("Name", $"{e.FirstName} {e.LastName}"),
                    ("Employee number", e.EmployeeNumber),
                    ("Job title", e.JobTitle ?? "Not recorded"),
                    ("Department", e.Department.Name),
                    ("National ID", e.NationalId ?? "Not recorded"),
                    ("Date of birth", Date(e.DateOfBirth))
                };
            }

            var vendorIds = Ids(SanctionsService.Vendor, SanctionsService.VendorContact);

            foreach (var v in await _context.Vendors
                         .AsNoTracking()
                         .Where(v => vendorIds.Contains(v.Id))
                         .ToListAsync())
            {
                var details = new List<(string, string)>
                {
                    ("Vendor", v.Name),
                    ("Supplies", v.ProductOrService),
                    ("Contact person", v.ContactPerson ?? "Not recorded"),
                    ("Address", v.Address ?? "Not recorded")
                };

                model.Subjects[SanctionsResultViewModel.SubjectKey(SanctionsService.Vendor, v.Id)] = details;
                model.Subjects[SanctionsResultViewModel.SubjectKey(SanctionsService.VendorContact, v.Id)] = details;
            }

            var clientIds = Ids(SanctionsService.Client);

            foreach (var c in await _context.SanctionsClients
                         .AsNoTracking()
                         .Where(c => clientIds.Contains(c.Id))
                         .ToListAsync())
            {
                model.Subjects[SanctionsResultViewModel.SubjectKey(SanctionsService.Client, c.Id)] = SanctionsService.ClientDetails(c);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(FileStorageService.MaxFileSizeBytes + 1024 * 1024)]
        public async Task<IActionResult> AddNote(int id, int concernId, string? text, IFormFile? file)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                TempData["Error"] = "Write the note.";
                return Redirect(Url.Action(nameof(Result), new { id }) + $"#matter-{concernId}");
            }

            if (!await _context.SanctionsConcerns.AnyAsync(c => c.Id == concernId && c.SanctionsReviewId == id))
                return NotFound();

            try
            {
                await _sanctions.AddNoteAsync(concernId, text, file);
                TempData["Success"] = "Note added.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return Redirect(Url.Action(nameof(Result), new { id }) + $"#matter-{concernId}");
        }

        public async Task<IActionResult> Evidence(int id)
        {
            var note = await _context.SanctionsConcernNotes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);

            var path = note?.FilePath == null ? null : _files.ResolveExistingPath(note.FilePath);

            return path == null
                ? NotFound()
                : PhysicalFile(path, "application/octet-stream", note!.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reopen(int id, int concernId)
        {
            if (!await _context.SanctionsConcerns.AnyAsync(c => c.Id == concernId && c.SanctionsReviewId == id))
                return NotFound();

            try
            {
                await _sanctions.ReopenAsync(concernId);
                TempData["Success"] = "Reopened. It is now with the matters that need a decision.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return Redirect(Url.Action(nameof(Result), new { id }) + $"#matter-{concernId}");
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
            string? resolution,
            string? escalatedTo)
        {
            if (!await _context.SanctionsConcerns.AnyAsync(c => c.Id == concernId && c.SanctionsReviewId == id))
                return NotFound();

            try
            {
                await _sanctions.ResolveAsync(concernId, outcome, resolution ?? string.Empty, escalatedTo);
                TempData["Success"] = "Recorded.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return Redirect(Url.Action(nameof(Result), new { id }) + $"#matter-{concernId}");
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

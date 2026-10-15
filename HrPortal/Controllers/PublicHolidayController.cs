using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// The holiday calendar leave is counted against. HR keeps it,
    /// since it is the same people who set leave types and balances.
    /// </summary>
    [Authorize(Roles = Roles.HrOnly)]
    public class PublicHolidayController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;

        public PublicHolidayController(
            ApplicationDbContext context,
            AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index(int? year)
        {
            var chosen = year ?? DateTime.Today.Year;

            var holidays = await _context.PublicHolidays
                .AsNoTracking()
                .Where(h => h.Date.Year == chosen)
                .OrderBy(h => h.Date)
                .ToListAsync();

            ViewBag.Year = chosen;

            ViewBag.Years = await _context.PublicHolidays
                .AsNoTracking()
                .Select(h => h.Date.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            // Only weekday holidays take a day off a leave request; one
            // that falls at a weekend costs nobody anything.
            ViewBag.OnWeekdays = holidays.Count(h =>
                h.Date.DayOfWeek != DayOfWeek.Saturday &&
                h.Date.DayOfWeek != DayOfWeek.Sunday);

            return View(holidays);
        }

        public IActionResult Create(int? year)
        {
            var target = year ?? DateTime.Today.Year;

            return View(new PublicHolidayViewModel
            {
                Date = new DateTime(target, 1, 1)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PublicHolidayViewModel model)
        {
            await ValidateAsync(model, null);

            if (!ModelState.IsValid)
                return View(model);

            var holiday = new PublicHoliday
            {
                Name = model.Name.Trim(),
                Date = model.Date.Date,
                Notes = model.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.PublicHolidays.Add(holiday);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(PublicHoliday),
                holiday.Id.ToString(),
                $"{holiday.Name} on {holiday.Date:dd MMM yyyy} added to " +
                "the holiday calendar.");

            TempData["Success"] = "Holiday added.";

            return RedirectToAction(
                nameof(Index),
                new { year = holiday.Date.Year });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var holiday = await _context.PublicHolidays.FindAsync(id);

            if (holiday == null)
                return NotFound();

            return View(new PublicHolidayViewModel
            {
                Id = holiday.Id,
                Name = holiday.Name,
                Date = holiday.Date,
                Notes = holiday.Notes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            PublicHolidayViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            await ValidateAsync(model, id);

            if (!ModelState.IsValid)
                return View(model);

            var holiday = await _context.PublicHolidays.FindAsync(id);

            if (holiday == null)
                return NotFound();

            holiday.Name = model.Name.Trim();
            holiday.Date = model.Date.Date;
            holiday.Notes = model.Notes?.Trim();

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(PublicHoliday),
                holiday.Id.ToString(),
                $"{holiday.Name} updated.");

            TempData["Success"] = "Holiday updated.";

            return RedirectToAction(
                nameof(Index),
                new { year = holiday.Date.Year });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var holiday = await _context.PublicHolidays.FindAsync(id);

            if (holiday == null)
                return NotFound();

            var year = holiday.Date.Year;

            _context.PublicHolidays.Remove(holiday);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(PublicHoliday),
                holiday.Id.ToString(),
                $"{holiday.Name} on {holiday.Date:dd MMM yyyy} removed " +
                "from the holiday calendar.");

            TempData["Success"] = "Holiday removed.";

            return RedirectToAction(nameof(Index), new { year });
        }

        /// <summary>
        /// Copies a year's holidays into the next one on the same month
        /// and day, because most of them are fixed dates: Christmas is
        /// always the 25th whatever weekday it lands on. The ones that
        /// move, Easter and anything pinned to the second Monday of a
        /// month among them, are corrected afterwards by hand, which is
        /// what the message says.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CopyToNextYear(int year)
        {
            var source = await _context.PublicHolidays
                .AsNoTracking()
                .Where(h => h.Date.Year == year)
                .OrderBy(h => h.Date)
                .ToListAsync();

            if (source.Count == 0)
            {
                TempData["Error"] =
                    $"There are no holidays recorded for {year} to copy.";

                return RedirectToAction(nameof(Index), new { year });
            }

            var target = year + 1;

            var existing = await _context.PublicHolidays
                .Where(h => h.Date.Year == target)
                .Select(h => h.Date)
                .ToListAsync();

            var taken = existing.Select(d => d.Date).ToHashSet();

            var added = 0;

            foreach (var holiday in source)
            {
                var from = holiday.Date.Date;

                // 29 February has no counterpart in a common year.
                if (from.Month == 2 && from.Day == 29 &&
                    !DateTime.IsLeapYear(target))
                {
                    continue;
                }

                var date = new DateTime(target, from.Month, from.Day);

                if (taken.Contains(date))
                    continue;

                _context.PublicHolidays.Add(new PublicHoliday
                {
                    Name = holiday.Name,
                    Date = date,
                    Notes = holiday.Notes,
                    CreatedAt = DateTime.UtcNow
                });

                taken.Add(date);
                added++;
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(PublicHoliday),
                target.ToString(),
                $"{added} holiday(s) copied from {year} into {target}.");

            TempData["Success"] = added == 0
                ? $"Nothing to copy: {target} already has those dates."
                : $"{added} holiday(s) copied into {target}. Check the " +
                  "dates, since not every holiday falls the same way " +
                  "each year.";

            return RedirectToAction(nameof(Index), new { year = target });
        }

        private async Task ValidateAsync(
            PublicHolidayViewModel model,
            int? excludeId)
        {
            var date = model.Date.Date;

            var clash = await _context.PublicHolidays
                .AnyAsync(h =>
                    h.Date == date &&
                    (excludeId == null || h.Id != excludeId.Value));

            if (clash)
            {
                ModelState.AddModelError(
                    nameof(model.Date),
                    "There is already a holiday recorded on that date.");
            }
        }
    }
}

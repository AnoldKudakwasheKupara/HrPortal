using HrPortal.DbContexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Audit logs are deliberately read-only. A trail that can be
    /// edited or deleted from the portal is no longer evidence of
    /// anything, so this controller offers no create, edit or
    /// delete actions. Entries are written by the code that
    /// performs the audited action.
    /// </summary>
    [Authorize(Roles = "Administrator")]
    public class AuditLogController : Controller
    {
        private const int PageSize = 50;

        private readonly ApplicationDbContext _context;

        public AuditLogController(ApplicationDbContext context)
        {
            _context = context;
        }


        // ============================================================
        // INDEX
        // ============================================================

        public async Task<IActionResult> Index(
            int? employeeId,
            string? entityName,
            DateTime? fromDate,
            DateTime? toDate,
            int page = 1)
        {
            var query = _context.AuditLogs
                .Include(a => a.Employee)
                .AsQueryable();

            if (employeeId.HasValue)
            {
                query = query.Where(a =>
                    a.EmployeeId == employeeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(entityName))
            {
                query = query.Where(a =>
                    a.EntityName == entityName);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(a =>
                    a.Timestamp >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var inclusiveTo = toDate.Value.Date.AddDays(1);

                query = query.Where(a =>
                    a.Timestamp < inclusiveTo);
            }

            var totalCount = await query.CountAsync();

            var totalPages =
                (int)Math.Ceiling(totalCount / (double)PageSize);

            if (page < 1)
                page = 1;

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            await LoadFilters();

            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.SelectedEntityName = entityName;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(logs);
        }


        // ============================================================
        // DETAILS
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var log = await _context.AuditLogs
                .Include(a => a.Employee)
                    .ThenInclude(e => e!.Department)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (log == null)
                return NotFound();

            return View(log);
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private async Task LoadFilters()
        {
            var employees = await _context.Employees
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Select(e => new
                {
                    e.Id,
                    FullName = e.FirstName + " " + e.LastName
                })
                .ToListAsync();

            ViewBag.Employees =
                new SelectList(
                    employees,
                    "Id",
                    "FullName");

            // Only offer entity names that actually appear in the
            // log, so the filter never has dead options.
            var entityNames = await _context.AuditLogs
                .Select(a => a.EntityName)
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync();

            ViewBag.EntityNames = new SelectList(entityNames);
        }
    }
}

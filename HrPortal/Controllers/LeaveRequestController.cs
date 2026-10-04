using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// A read-only view of every leave request, for HR and
    /// management oversight and reporting.
    ///
    /// Deliberately has no create, edit or delete. Requests are
    /// raised by employees through MyLeave and decided by the
    /// approvers through LeaveApproval, which is where the
    /// balance movements, history, notifications and audit
    /// entries are produced. Editing a request's status here
    /// would quietly skip all of that, and leave records are
    /// historical HR information that should not be removed.
    /// </summary>
    [Authorize(Roles = Roles.AdminOrHrOrMd)]
    public class LeaveRequestController : Controller
    {
        private const int PageSize = 50;

        private readonly ApplicationDbContext _context;

        public LeaveRequestController(ApplicationDbContext context)
        {
            _context = context;
        }


        // ============================================================
        // INDEX
        // ============================================================

        public async Task<IActionResult> Index(
            LeaveRequestStatus? status,
            int? employeeId,
            int page = 1)
        {
            var query = _context.LeaveRequests
                .Include(r => r.Employee)
                .Include(r => r.LeaveType)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            if (employeeId.HasValue)
            {
                query = query.Where(r =>
                    r.EmployeeId == employeeId.Value);
            }

            var totalCount = await query.CountAsync();

            var totalPages =
                (int)Math.Ceiling(totalCount / (double)PageSize);

            if (page < 1)
                page = 1;

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var requests = await query
                .OrderByDescending(r => r.SubmittedAt)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            await LoadEmployees();

            ViewBag.SelectedStatus = status;
            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(requests);
        }


        // ============================================================
        // DETAILS
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await _context.LeaveRequests
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Department)
                .Include(r => r.LeaveType)
                .Include(r => r.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            return View(request);
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private async Task LoadEmployees()
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
                new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                    employees,
                    "Id",
                    "FullName");
        }
    }
}

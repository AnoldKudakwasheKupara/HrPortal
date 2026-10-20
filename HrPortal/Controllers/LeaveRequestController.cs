using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.HrOrMd)]
    public class LeaveRequestController : Controller
    {
        private const int PageSize = 50;

        private readonly ApplicationDbContext _context;
        private readonly RecordScope _scope;

        public LeaveRequestController(
            ApplicationDbContext context,
            RecordScope scope)
        {
            _context = context;
            _scope = scope;
        }

        public async Task<IActionResult> Index(
            LeaveRequestStatus? status,
            int? employeeId,
            int page = 1)
        {
            var query = await _scope.VisibleToAsync(
                _context.LeaveRequests
                    .AsNoTracking()
                    .Include(r => r.Employee)
                    .Include(r => r.LeaveType),
                User);

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

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await _context.LeaveRequests
                .AsNoTracking()
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

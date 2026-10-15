using HrPortal.DbContexts;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// The staff directory: who works here, what they do and how to
    /// reach them at work. Open to anyone signed in, which is the whole
    /// point of it.
    ///
    /// It carries work contact details only. Date of birth, home
    /// address, national ID, emergency contacts and anything about
    /// someone's employment terms stay on the employee record, which is
    /// HR and administrator only.
    /// </summary>
    [Authorize]
    public class DirectoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DirectoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            string? search,
            int? departmentId)
        {
            var query = _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Where(e => e.IsActive);

            if (departmentId.HasValue)
                query = query.Where(e => e.DepartmentId == departmentId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                query = query.Where(e =>
                    e.FirstName.Contains(term) ||
                    e.LastName.Contains(term) ||
                    (e.JobTitle != null && e.JobTitle.Contains(term)) ||
                    e.Email.Contains(term));
            }

            var people = await query
                .OrderBy(e => e.Department.Name)
                .ThenBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Select(e => new DirectoryEntryViewModel
                {
                    Name = e.FirstName + " " + e.LastName,
                    JobTitle = e.JobTitle,
                    Department = e.Department.Name,
                    Email = e.Email,
                    PhoneNumber = e.PhoneNumber
                })
                .ToListAsync();

            ViewBag.Departments = await _context.Departments
                .AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new SelectListItem(d.Name, d.Id.ToString()))
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.DepartmentFilter = departmentId;

            ViewBag.TotalActive = await _context.Employees
                .CountAsync(e => e.IsActive);

            return View(people);
        }
    }
}

using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.AdminOnly)]
    public class TimesheetController : Controller
    {
        private readonly TimesheetRegisterService _register;

        public TimesheetController(TimesheetRegisterService register)
        {
            _register = register;
        }

        public async Task<IActionResult> Index(
            int? employeeId,
            WorkCategory? category,
            DateTime? from,
            DateTime? to,
            int page = 1)
        {
            var model = await _register.BuildAsync(
                employeeId,
                category,
                from,
                to,
                page);

            ViewBag.Employees = await _register.LoggingEmployeesAsync();

            return View(model);
        }

        public async Task<IActionResult> Export(
            int? employeeId,
            WorkCategory? category,
            DateTime? from,
            DateTime? to)
        {
            var (content, fileName) = await _register.ExportAsync(
                employeeId,
                category,
                from,
                to);

            return File(content, "text/csv", fileName);
        }
    }
}

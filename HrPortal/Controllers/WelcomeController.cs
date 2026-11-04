using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrPortal.Controllers
{
    [Authorize]
    public class WelcomeController : Controller
    {
        private readonly CurrentUserService _currentUser;
        private readonly ApproverDirectory _approvers;
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;

        public WelcomeController(
            CurrentUserService currentUser,
            ApproverDirectory approvers,
            ApplicationDbContext context,
            AuditService audit)
        {
            _currentUser = currentUser;
            _approvers = approvers;
            _context = context;
            _audit = audit;
        }

        /// <summary>
        /// Only for somebody still being onboarded; for anyone else the
        /// page no longer exists and the dashboard takes its place.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var employee = await _currentUser.GetEmployeeAsync();

            if (employee == null || employee.OnboardingCompletedAt != null)
                return RedirectToAction("Index", "Home");

            return View(new WelcomeViewModel
            {
                FirstName = employee?.FirstName,
                HrContacts = await _approvers.PeopleAsync(
                    ApprovalRole.HROfficer)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finish()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            var employee = employeeId == null
                ? null
                : await _context.Employees.FindAsync(employeeId.Value);

            if (employee != null && employee.OnboardingCompletedAt == null)
            {
                employee.OnboardingCompletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await _audit.LogAsync(
                    AuditService.Updated,
                    nameof(Employee),
                    employee.EmployeeNumber,
                    "Finished onboarding.");
            }

            return RedirectToAction("Index", "Home");
        }
    }
}

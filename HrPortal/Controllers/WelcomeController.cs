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

        public WelcomeController(
            CurrentUserService currentUser,
            ApproverDirectory approvers)
        {
            _currentUser = currentUser;
            _approvers = approvers;
        }

        public async Task<IActionResult> Index()
        {
            var employee = await _currentUser.GetEmployeeAsync();

            return View(new WelcomeViewModel
            {
                FirstName = employee?.FirstName,
                HrContacts = await _approvers.PeopleAsync(
                    ApprovalRole.HROfficer)
            });
        }
    }
}

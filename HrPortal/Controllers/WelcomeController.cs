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
        private readonly HrContactService _hrContact;

        public WelcomeController(
            CurrentUserService currentUser,
            HrContactService hrContact)
        {
            _currentUser = currentUser;
            _hrContact = hrContact;
        }

        public async Task<IActionResult> Index()
        {
            var employee = await _currentUser.GetEmployeeAsync();

            return View(new WelcomeViewModel
            {
                FirstName = employee?.FirstName,
                HrContact = await _hrContact.GetAsync()
            });
        }
    }
}

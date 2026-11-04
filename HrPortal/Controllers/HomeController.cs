using HrPortal.Models;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace HrPortal.Controllers
{
    public class HomeController : Controller
    {
        private readonly DashboardService _dashboard;
        private readonly PrivacyNoticeService _privacy;
        private readonly CurrentUserService _currentUser;

        public HomeController(
            DashboardService dashboard,
            PrivacyNoticeService privacy,
            CurrentUserService currentUser)
        {
            _dashboard = dashboard;
            _privacy = privacy;
            _currentUser = currentUser;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated != true)
                return View("Landing");

            if ((await _currentUser.GetEmployeeAsync()) is { OnboardingCompletedAt: null })
                return RedirectToAction("Index", "Welcome");

            return View(await _dashboard.BuildAsync(User));
        }

        /// <summary>
        /// Reachable without signing in: the sign-in page links to
        /// it, and somebody deciding whether to use the portal
        /// should be able to read it first.
        /// </summary>
        [AllowAnonymous]
        public async Task<IActionResult> Privacy(string? returnUrl)
        {
            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (int.TryParse(id, out var userId))
                ViewBag.AcknowledgedAt = await _privacy.AcknowledgedAtAsync(userId);

            return View();
        }

        [AllowAnonymous]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId =
                    Activity.Current?.Id
                    ?? HttpContext.TraceIdentifier
            });
        }
    }
}

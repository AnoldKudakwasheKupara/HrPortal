using HrPortal.Models;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace HrPortal.Controllers
{
    public class HomeController : Controller
    {
        private readonly DashboardService _dashboard;

        public HomeController(DashboardService dashboard)
        {
            _dashboard = dashboard;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated != true)
                return View("Landing");

            return View(await _dashboard.BuildAsync(User));
        }

        /// <summary>
        /// Reachable without signing in: the sign-in page links to
        /// it, and somebody deciding whether to use the portal
        /// should be able to read it first.
        /// </summary>
        [AllowAnonymous]
        public IActionResult Privacy()
        {
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

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

        public async Task<IActionResult> Index()
        {
            return View(await _dashboard.BuildAsync(User));
        }

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

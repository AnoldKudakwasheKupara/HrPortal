using HrPortal.DbContexts;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize]
    public class MyAssetsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CurrentUserService _currentUser;

        public MyAssetsController(
            ApplicationDbContext context,
            CurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<IActionResult> Index()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var assets = await _context.Assets
                .AsNoTracking()
                .Where(a => a.AssignedToEmployeeId == employeeId.Value)
                .OrderByDescending(a => a.IsInUse)
                .ThenBy(a => a.DeviceModel)
                .ToListAsync();

            return View(assets);
        }
    }
}

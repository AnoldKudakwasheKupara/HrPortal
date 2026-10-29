using System.Security.Claims;
using HrPortal.Security;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrPortal.Controllers
{
    [Authorize]
    public class PrivacyController : Controller
    {
        private readonly PrivacyNoticeService _privacy;

        public PrivacyController(PrivacyNoticeService privacy)
        {
            _privacy = privacy;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Acknowledge(string? returnUrl)
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(id, out var userId))
                return Forbid();

            await _privacy.AcknowledgeAsync(
                userId,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Who has read the current notice and who has not, for HR to
        /// chase and to show if anyone asks.
        /// </summary>
        [Authorize(Roles = Roles.AdminOrHr)]
        public async Task<IActionResult> Readers()
        {
            return View(await _privacy.ReadersAsync());
        }
    }
}

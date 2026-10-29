
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using HrPortal.Data;
using HrPortal.Services;

namespace HrPortal.Areas.Identity.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<LogoutModel> _logger;
    private readonly VaultLock _vault;

    public LogoutModel(
        SignInManager<ApplicationUser> signInManager,
        ILogger<LogoutModel> logger,
        VaultLock vault)
    {
        _signInManager = signInManager;
        _logger = logger;
        _vault = vault;
    }

    public async Task<IActionResult> OnPost(string? returnUrl = null)
    {
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            _vault.Close(userId);

        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        if (returnUrl != null)
        {
            return LocalRedirect(returnUrl);
        }
        else
        {
            return RedirectToPage();
        }
    }
}

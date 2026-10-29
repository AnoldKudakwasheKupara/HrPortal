using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Services;
using HrPortal.Services.Notifications;

namespace HrPortal.Areas.Identity.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly EmailService _email;
    private readonly EmailOptions _options;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        EmailService email,
        IOptions<EmailOptions> options,
        ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager;
        _context = context;
        _email = email;
        _options = options.Value;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = default!;
    }

    /// <summary>
    /// Sends the reset link in the portal's own email, straight to
    /// the address that asked rather than through the bell, since
    /// the person asking cannot sign in to see a bell.
    ///
    /// The page says the same thing whether or not the address
    /// belongs to anybody, so it cannot be used to find out who has
    /// an account.
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);

        if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            return RedirectToPage("./ForgotPasswordConfirmation");

        var code = await _userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var link = Url.Page(
            "/Account/ResetPassword",
            pageHandler: null,
            values: new { area = "Identity", code })!;

        if (string.IsNullOrWhiteSpace(_options.PortalBaseUrl))
            link = $"{Request.Scheme}://{Request.Host}{link}";

        var employee = user.EmployeeId == null
            ? null
            : await _context.Employees
                .AsNoTracking()
                .Where(e => e.Id == user.EmployeeId.Value)
                .Select(e => new { e.Id, e.FirstName })
                .FirstOrDefaultAsync();

        var sent = await _email.SendNotificationAsync(
            new OutboundNotification(
                employee?.Id ?? 0,
                user.Email!,
                user.FullName,
                employee?.FirstName,
                "Reset your My Intellego password",
                "Somebody asked to reset the password for your My " +
                "Intellego account. If it was you, choose a new password " +
                "using the link below. The link works for 24 hours and " +
                "only once. If you did not ask, you can ignore this " +
                "email and your password stays as it is.",
                link,
                EmailKind.PasswordReset));

        if (!sent)
        {
            _logger.LogWarning(
                "A password reset was asked for user {UserId} but the " +
                "email could not be sent.",
                user.Id);
        }

        return RedirectToPage("./ForgotPasswordConfirmation");
    }
}

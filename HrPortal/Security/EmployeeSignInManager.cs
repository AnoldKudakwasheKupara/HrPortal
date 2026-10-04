using HrPortal.Data;
using HrPortal.DbContexts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrPortal.Security
{
    /// <summary>
    /// Refuses sign-in to users whose employee record has been
    /// deactivated, as required by the security rules.
    ///
    /// This sits on CanSignInAsync rather than on the login page
    /// so that every sign-in path is covered, including two
    /// factor, recovery codes and external logins. A user
    /// deactivated mid-session is removed by the security stamp
    /// being changed at the same time, which invalidates the
    /// cookie on its next validation.
    /// </summary>
    public class EmployeeSignInManager : SignInManager<ApplicationUser>
    {
        private readonly ApplicationDbContext _context;

        public EmployeeSignInManager(
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor contextAccessor,
            IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
            IOptions<IdentityOptions> optionsAccessor,
            ILogger<SignInManager<ApplicationUser>> logger,
            IAuthenticationSchemeProvider schemes,
            IUserConfirmation<ApplicationUser> confirmation,
            ApplicationDbContext context)
            : base(
                userManager,
                contextAccessor,
                claimsFactory,
                optionsAccessor,
                logger,
                schemes,
                confirmation)
        {
            _context = context;
        }


        public override async Task<bool> CanSignInAsync(
            ApplicationUser user)
        {
            if (!await base.CanSignInAsync(user))
                return false;

            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.UserId == user.Id);

            // A user with no employee record is an administrative
            // account such as the seeded administrator, and is not
            // governed by employment status.
            if (employee == null)
                return true;

            if (!employee.IsActive)
            {
                Logger.LogWarning(
                    "Sign-in refused for user {UserId}: the linked " +
                    "employee record is inactive.",
                    user.Id);

                return false;
            }

            return true;
        }
    }
}

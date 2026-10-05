using HrPortal.Data;
using HrPortal.DbContexts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrPortal.Security
{
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

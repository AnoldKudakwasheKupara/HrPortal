using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrPortal.Security
{
    public class EmployeeSignInManager : SignInManager<ApplicationUser>
    {
        private readonly ApplicationDbContext _context;
        private readonly CompanyService _companies;

        public EmployeeSignInManager(
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor contextAccessor,
            IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
            IOptions<IdentityOptions> optionsAccessor,
            ILogger<SignInManager<ApplicationUser>> logger,
            IAuthenticationSchemeProvider schemes,
            IUserConfirmation<ApplicationUser> confirmation,
            ApplicationDbContext context,
            CompanyService companies)
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
            _companies = companies;
        }

        /// <summary>
        /// The company somebody works for when they tried a portal that
        /// is not theirs with the right password, so the sign-in page
        /// can send them to the right one. Null otherwise.
        /// </summary>
        public Company? RefusedHomeCompany { get; private set; }

        /// <summary>
        /// Somebody who has passed the password check is still turned
        /// away from a company's portal they do not work for. This is
        /// checked after the password, so the answer tells nobody who
        /// does not already know it whether an address has an account.
        /// The administrator looks after every company, so is let in
        /// everywhere.
        /// </summary>
        protected override async Task<SignInResult> SignInOrTwoFactorAsync(
            ApplicationUser user,
            bool isPersistent,
            string? loginProvider = null,
            bool bypassTwoFactor = false)
        {
            RefusedHomeCompany = await HomeCompanyIfNotHereAsync(user);

            if (RefusedHomeCompany != null)
            {
                Logger.LogWarning(
                    "Sign-in refused for user {UserId}: they do not work " +
                    "for {Company}.",
                    user.Id,
                    Brand.Current.Key);

                return SignInResult.NotAllowed;
            }

            return await base.SignInOrTwoFactorAsync(
                user,
                isPersistent,
                loginProvider,
                bypassTwoFactor);
        }

        private async Task<Company?> HomeCompanyIfNotHereAsync(ApplicationUser user)
        {
            if (await UserManager.IsInRoleAsync(user, Roles.Administrator))
                return null;

            var employee = await _context.Employees
                .AsNoTracking()
                .Include(e => e.HomeCompany)
                .FirstOrDefaultAsync(e => e.UserId == user.Id);

            if (employee == null || await _companies.WorksHereAsync(employee.Id))
                return null;

            return employee.HomeCompany;
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

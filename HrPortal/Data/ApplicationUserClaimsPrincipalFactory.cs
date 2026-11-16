using HrPortal.DbContexts;
using HrPortal.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace HrPortal.Data
{
    public class ApplicationUserClaimsPrincipalFactory
        : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<int>>
    {
        public const string FullNameClaim = "full_name";

        private readonly ApplicationDbContext _context;
        private readonly CompanyService _companies;

        public ApplicationUserClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            IOptions<IdentityOptions> options,
            ApplicationDbContext context,
            CompanyService companies)
            : base(userManager, roleManager, options)
        {
            _context = context;
            _companies = companies;
        }

        /// <summary>
        /// Adds the roles the person holds at the company whose portal
        /// they signed in to, alongside the ones that hold everywhere.
        /// Each portal keeps its own sign-in, so somebody who is HR at
        /// one company is an ordinary employee on the other's portal.
        /// The sign-in is rebuilt every few minutes, which is how a
        /// change of role reaches somebody already signed in.
        /// </summary>
        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(
            ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                identity.AddClaim(
                    new Claim(FullNameClaim, user.FullName));
            }

            var here = await _companies.CurrentIdAsync();

            var roles = await _context.CompanyRoles
                .AsNoTracking()
                .Where(cr => cr.UserId == user.Id && cr.CompanyId == here)
                .Join(
                    _context.Roles,
                    cr => cr.RoleId,
                    r => r.Id,
                    (cr, r) => r.Name!)
                .ToListAsync();

            foreach (var role in roles.Where(r => !identity.HasClaim(Options.ClaimsIdentity.RoleClaimType, r)))
            {
                identity.AddClaim(
                    new Claim(Options.ClaimsIdentity.RoleClaimType, role));
            }

            return identity;
        }
    }
}

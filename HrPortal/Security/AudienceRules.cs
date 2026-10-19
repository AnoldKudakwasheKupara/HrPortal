using System.Security.Claims;
using HrPortal.Entities;

namespace HrPortal.Security
{
    public static class AudienceRules
    {
        public static bool CanSee(ClaimsPrincipal user, DocumentAudience audience)
        {
            return audience switch
            {
                DocumentAudience.Everyone => true,

                DocumentAudience.Managers =>
                    user.IsInRole(Roles.Administrator) ||
                    user.IsInRole(Roles.HrOfficer) ||
                    user.IsInRole(Roles.Md),

                DocumentAudience.HrAndAdministrators =>
                    user.IsInRole(Roles.Administrator) ||
                    user.IsInRole(Roles.HrOfficer),

                _ => false
            };
        }

        public static List<DocumentAudience> Visible(ClaimsPrincipal user)
        {
            return Enum.GetValues<DocumentAudience>()
                .Where(a => CanSee(user, a))
                .ToList();
        }

        public const string Curators = Roles.AdminOrHr;
    }
}

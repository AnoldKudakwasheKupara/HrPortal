using System.Security.Claims;
using HrPortal.Entities;

namespace HrPortal.Security
{
    public static class AudienceRules
    {
        /// <summary>
        /// The sign-in roles that may see something meant for this
        /// audience, or null when everybody may. Both the check on a
        /// person and the list of who to tell come from here, so the
        /// two cannot drift apart.
        /// </summary>
        public static string[]? RolesFor(DocumentAudience audience) =>
            audience switch
            {
                DocumentAudience.Everyone => null,

                DocumentAudience.Managers =>
                    new[] { Roles.Administrator, Roles.HrOfficer, Roles.Md },

                DocumentAudience.HrAndAdministrators =>
                    new[] { Roles.Administrator, Roles.HrOfficer },

                _ => Array.Empty<string>()
            };

        public static bool CanSee(ClaimsPrincipal user, DocumentAudience audience)
        {
            var roles = RolesFor(audience);

            return roles == null || roles.Any(user.IsInRole);
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

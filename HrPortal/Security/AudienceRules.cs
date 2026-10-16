using System.Security.Claims;
using HrPortal.Entities;

namespace HrPortal.Security
{
    /// <summary>
    /// Who can see which audience. One place, so the browse list and
    /// the download action cannot drift apart and leave a file listed
    /// but unopenable, or worse, openable but unlisted.
    /// </summary>
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

        /// <summary>
        /// The audiences this user may see, for filtering a query in
        /// the database rather than in memory.
        /// </summary>
        public static List<DocumentAudience> Visible(ClaimsPrincipal user)
        {
            return Enum.GetValues<DocumentAudience>()
                .Where(a => CanSee(user, a))
                .ToList();
        }

        /// <summary>
        /// Who may put things into the repository and take them out.
        /// </summary>
        public const string Curators = Roles.AdminOrHr;
    }
}

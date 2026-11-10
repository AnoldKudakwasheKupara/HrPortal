using System.Security.Claims;
using HrPortal.Services;

namespace HrPortal.Security
{
    /// <summary>
    /// Who may record course requests that were approved on paper before
    /// the portal: HR Officers and everyone in the IT department.
    ///
    /// The department is looked up on each request rather than kept in the
    /// sign-in cookie, so moving someone out of IT takes effect at once.
    /// </summary>
    public class CourseCaptureAccess
    {
        private readonly CurrentUserService _currentUser;

        public CourseCaptureAccess(CurrentUserService currentUser) => _currentUser = currentUser;

        public async Task<bool> CanCaptureAsync(ClaimsPrincipal user)
        {
            if (user.IsInRole(Roles.HrOfficer))
                return true;

            var employee = await _currentUser.GetEmployeeAsync();

            return ItDepartment.Matches(employee?.Department?.Name);
        }
    }
}

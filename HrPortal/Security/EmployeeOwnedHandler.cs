using HrPortal.Entities;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace HrPortal.Security
{
    public class EmployeeOwnedHandler
        : AuthorizationHandler<
            OperationAuthorizationRequirement,
            IEmployeeOwned>
    {
        private readonly CurrentUserService _currentUser;

        public EmployeeOwnedHandler(CurrentUserService currentUser)
        {
            _currentUser = currentUser;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            OperationAuthorizationRequirement requirement,
            IEmployeeOwned resource)
        {
            if (context.User.IsInRole(Roles.Administrator))
            {
                context.Succeed(requirement);
                return;
            }

            if (ElevatedRoleMay(context.User, requirement, resource))
            {
                context.Succeed(requirement);
                return;
            }

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId != null &&
                employeeId.Value == resource.EmployeeId)
            {
                context.Succeed(requirement);
            }
        }

        private static bool ElevatedRoleMay(
            System.Security.Claims.ClaimsPrincipal user,
            OperationAuthorizationRequirement requirement,
            IEmployeeOwned resource)
        {
            var isHr = user.IsInRole(Roles.HrOfficer);
            var isMd = user.IsInRole(Roles.Md);

            var viewing = requirement.Name == Operations.View.Name;

            return resource switch
            {
                LeaveRequest => isHr || (isMd && viewing),

                LeaveBalance => isHr,

                EmployeeDocument => isHr,
                TrainingAssignment => isHr,

                Notification => false,

                _ => false
            };
        }
    }
}

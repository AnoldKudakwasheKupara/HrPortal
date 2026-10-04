using HrPortal.Entities;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace HrPortal.Security
{
    /// <summary>
    /// Decides who may see and change a record that belongs to an
    /// employee.
    ///
    /// This is the single statement of the rule. Controllers ask
    /// it rather than writing their own ownership test, so a new
    /// action cannot quietly forget one. Its counterpart for
    /// lists is <see cref="RecordScope"/>, which applies the same
    /// rule to a query; the two are kept side by side so they
    /// cannot drift.
    ///
    /// Only ownership and role are decided here. Whether an
    /// action is valid at this point in a workflow is a separate
    /// question and stays with the workflow.
    /// </summary>
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
            // An administrator runs the system and sees all of it.
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

            // Otherwise it has to be your own record.
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId != null &&
                employeeId.Value == resource.EmployeeId)
            {
                context.Succeed(requirement);
            }
        }


        /// <summary>
        /// What a role may do to someone else's record.
        ///
        /// Anything not granted here falls through to the
        /// ownership test, so the default is that a record is
        /// private to the employee it belongs to.
        /// </summary>
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
                // HR administers leave. The MD needs to read a
                // request to decide it, but never edits one.
                LeaveRequest => isHr || (isMd && viewing),

                LeaveBalance => isHr,

                // HR keeps the employee files and the training
                // records.
                EmployeeDocument => isHr,
                TrainingAssignment => isHr,

                // A notification is a personal message. Nobody
                // reads someone else's, including HR; the
                // administrator exception above is there to keep
                // the system supportable.
                Notification => false,

                _ => false
            };
        }
    }
}

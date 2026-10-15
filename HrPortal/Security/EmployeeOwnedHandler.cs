using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Security
{
    public class EmployeeOwnedHandler
        : AuthorizationHandler<
            OperationAuthorizationRequirement,
            IEmployeeOwned>
    {
        private readonly CurrentUserService _currentUser;
        private readonly ApplicationDbContext _context;

        public EmployeeOwnedHandler(
            CurrentUserService currentUser,
            ApplicationDbContext context)
        {
            _currentUser = currentUser;
            _context = context;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            OperationAuthorizationRequirement requirement,
            IEmployeeOwned resource)
        {
            if (ElevatedRoleMay(context.User, requirement, resource))
            {
                context.Succeed(requirement);
                return;
            }

            // Owning the record is normally enough. A document in a
            // restricted category is the exception: it is HR's record
            // about the employee, not the employee's own, so ownership
            // grants nothing.
            if (resource is EmployeeDocument document &&
                await IsRestrictedAsync(document))
            {
                return;
            }

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId != null &&
                employeeId.Value == resource.EmployeeId)
            {
                context.Succeed(requirement);
            }
        }

        /// <summary>
        /// Read from the database rather than the resource's navigation
        /// property, which a caller may not have loaded. Guessing from
        /// a null navigation would fail open.
        /// </summary>
        private async Task<bool> IsRestrictedAsync(EmployeeDocument document)
        {
            if (document.DocumentCategory != null)
                return document.DocumentCategory.IsRestricted;

            return await _context.EmployeeDocuments
                .AsNoTracking()
                .Where(d => d.Id == document.Id)
                .Select(d => d.DocumentCategory.IsRestricted)
                .FirstOrDefaultAsync();
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

                // Timesheets are the owner's own record. The
                // administrator reads them through the register, not
                // through this handler.
                TimesheetEntry => false,

                _ => false
            };
        }
    }
}

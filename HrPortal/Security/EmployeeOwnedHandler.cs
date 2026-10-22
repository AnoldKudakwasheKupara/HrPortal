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

                // Loans are sanctioned by the Managing Director and
                // are nobody else's business, so HR has no standing
                // view of them the way it does for leave.
                LoanApplication => isMd,

                LeaveBalance => isHr,

                EmployeeDocument => isHr,
                TrainingAssignment => isHr,

                Notification => false,

                TimesheetEntry => false,

                _ => false
            };
        }
    }
}

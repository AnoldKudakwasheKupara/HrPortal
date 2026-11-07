using System.Security.Claims;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;

namespace HrPortal.Security
{
    /// <summary>
    /// Who may open pension fund reporting: everyone in the Consultancy or IT
    /// department, and anyone else given the Investment Analyst role.
    ///
    /// The department is looked up on each request rather than kept in the
    /// sign-in cookie, so moving someone between departments takes effect at
    /// once, in both directions.
    /// </summary>
    public class FundReportingAccess
    {
        public const string Policy = "FundReporting";

        private readonly CurrentUserService _currentUser;

        public FundReportingAccess(CurrentUserService currentUser) => _currentUser = currentUser;

        public async Task<bool> CanOpenAsync(ClaimsPrincipal user)
        {
            if (user.IsInRole(Roles.InvestmentAnalyst))
                return true;

            var employee = await _currentUser.GetEmployeeAsync();

            return DepartmentMayOpen(employee?.Department?.Name);
        }

        /// <summary>
        /// A department has no flag saying what it is, only its name. Any name
        /// with "consult" in it counts, so Consulting and Investment Consultancy
        /// are not locked out by their spelling.
        /// </summary>
        public static bool DepartmentMayOpen(string? departmentName) =>
            ItDepartment.Matches(departmentName) ||
            (departmentName?.Contains("consult", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    public class FundReportingRequirement : IAuthorizationRequirement
    {
    }

    public class FundReportingHandler : AuthorizationHandler<FundReportingRequirement>
    {
        private readonly FundReportingAccess _access;

        public FundReportingHandler(FundReportingAccess access) => _access = access;

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            FundReportingRequirement requirement)
        {
            if (await _access.CanOpenAsync(context.User))
                context.Succeed(requirement);
        }
    }
}

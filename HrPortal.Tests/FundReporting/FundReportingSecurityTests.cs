using System.Reflection;
using HrPortal.Controllers;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrPortal.Tests.FundReporting
{
    public class FundReportingSecurityTests
    {
        private static IEnumerable<MethodInfo> Actions =>
            typeof(FundReportsController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName);

        [Fact]
        public void Every_part_of_it_goes_through_the_access_rule()
        {
            var authorize = typeof(FundReportsController).GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authorize);
            Assert.Equal(FundReportingAccess.Policy, authorize!.Policy);
            Assert.Null(authorize.Roles);
        }

        [Theory]
        [InlineData("Consultancy")]
        [InlineData("CONSULTING")]
        [InlineData("Investment Consultancy")]
        [InlineData("Consultants")]
        [InlineData("IT")]
        [InlineData("ict department")]
        [InlineData("Information Technology")]
        public void Consultancy_and_it_departments_may_open_it(string department) =>
            Assert.True(FundReportingAccess.DepartmentMayOpen(department));

        [Theory]
        [InlineData("Finance")]
        [InlineData("Human Resources")]
        [InlineData("Audit")]
        [InlineData("")]
        [InlineData(null)]
        public void Other_departments_may_not(string? department) =>
            Assert.False(FundReportingAccess.DepartmentMayOpen(department));

        [Fact]
        public void No_action_lets_anybody_else_in()
        {
            Assert.DoesNotContain(Actions, a => a.GetCustomAttribute<AllowAnonymousAttribute>() != null);
        }

        [Fact]
        public void Every_change_is_protected_against_forged_requests()
        {
            var unprotected = Actions
                .Where(a => a.GetCustomAttribute<HttpPostAttribute>() != null &&
                            a.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() == null)
                .Select(a => a.Name)
                .ToList();

            Assert.Empty(unprotected);
        }
    }
}

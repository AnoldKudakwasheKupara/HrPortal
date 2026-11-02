using System.Reflection;
using HrPortal.Controllers;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrPortal.Tests.Sanctions
{
    public class SanctionsSecurityTests
    {
        private static IEnumerable<MethodInfo> Actions =>
            typeof(SanctionsController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName);

        [Fact]
        public void Only_compliance_officers_can_reach_any_part_of_it()
        {
            var authorize = typeof(SanctionsController).GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authorize);
            Assert.Equal(Roles.ComplianceOnly, authorize!.Roles);
            Assert.Equal("Compliance Officer", Roles.ComplianceOnly);
        }

        [Fact]
        public void No_action_lets_anybody_else_in()
        {
            Assert.DoesNotContain(Actions, a =>
                a.GetCustomAttribute<AllowAnonymousAttribute>() != null ||
                a.GetCustomAttributes<AuthorizeAttribute>().Any(x => x.Roles != null && x.Roles != Roles.ComplianceOnly));
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

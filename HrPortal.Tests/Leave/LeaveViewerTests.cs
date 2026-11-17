using System.Security.Claims;
using HrPortal.Entities;
using HrPortal.Security;

namespace HrPortal.Tests.Leave
{
    public class LeaveViewerTests
    {
        private static ClaimsPrincipal User(params string[] roles) =>
            new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "Test"));

        [Fact]
        public void A_leave_viewer_sees_every_leave_request_and_nothing_else()
        {
            var viewer = User(Roles.LeaveViewer);

            Assert.True(RecordScope.SeesEveryone<LeaveRequest>(viewer));
            Assert.False(RecordScope.SeesEveryone<LeaveBalance>(viewer));
            Assert.False(RecordScope.SeesEveryone<EmployeeDocument>(viewer));
            Assert.False(RecordScope.SeesEveryone<LeaveRequest>(User(Roles.Employee)));
        }

        [Fact]
        public void The_leave_viewer_role_is_held_per_company()
        {
            Assert.True(Roles.IsCompanyRole(Roles.LeaveViewer));
            Assert.Contains(Roles.LeaveViewer, Roles.LeaveRecords.Split(','));
        }
    }
}

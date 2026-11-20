using System.Reflection;
using System.Security.Claims;
using HrPortal.Controllers;
using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;

namespace HrPortal.Tests.Leave
{
    public class RecordsViewerTests
    {
        private static ClaimsPrincipal User(params string[] roles) =>
            new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "Test"));

        private static string[] RolesFor(Type controller, string action)
        {
            var method = controller.GetMethods().First(m => m.Name == action);

            return method.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(controller.GetCustomAttributes<AuthorizeAttribute>())
                .Where(a => a.Roles != null)
                .Select(a => a.Roles!)
                .ToArray();
        }

        private static bool Allows(Type controller, string action, string role) =>
            RolesFor(controller, action).All(roles => roles.Split(',').Contains(role));

        [Fact]
        public void A_records_viewer_sees_every_leave_request_and_balance_but_not_documents()
        {
            var viewer = User(Roles.RecordsViewer);

            Assert.True(RecordScope.SeesEveryone<LeaveRequest>(viewer));
            Assert.True(RecordScope.SeesEveryone<LeaveBalance>(viewer));
            Assert.False(RecordScope.SeesEveryone<EmployeeDocument>(viewer));
            Assert.False(RecordScope.SeesEveryone<TrainingAssignment>(viewer));
        }

        [Fact]
        public void The_records_viewer_role_is_held_per_company()
        {
            Assert.True(Roles.IsCompanyRole(Roles.RecordsViewer));
            Assert.Contains(Roles.RecordsViewer, Roles.LeaveRecords.Split(','));
        }

        [Theory]
        [InlineData(nameof(LeaveBalanceController.Index))]
        [InlineData(nameof(LeaveBalanceController.Details))]
        [InlineData(nameof(LeaveBalanceController.Form))]
        public void A_records_viewer_can_open_the_balances(string action)
        {
            Assert.True(Allows(typeof(LeaveBalanceController), action, Roles.RecordsViewer));
            Assert.True(Allows(typeof(LeaveBalanceController), action, Roles.HrOfficer));
        }

        [Theory]
        [InlineData(nameof(LeaveBalanceController.Upload))]
        [InlineData(nameof(LeaveBalanceController.Template))]
        [InlineData(nameof(LeaveBalanceController.Create))]
        [InlineData(nameof(LeaveBalanceController.Edit))]
        [InlineData(nameof(LeaveBalanceController.Delete))]
        [InlineData(nameof(LeaveBalanceController.DeleteConfirmed))]
        public void A_records_viewer_cannot_change_a_balance(string action)
        {
            foreach (var method in typeof(LeaveBalanceController).GetMethods().Where(m => m.Name == action))
            {
                var roles = method.GetCustomAttributes<AuthorizeAttribute>()
                    .Concat(typeof(LeaveBalanceController).GetCustomAttributes<AuthorizeAttribute>())
                    .Select(a => a.Roles!.Split(','));

                Assert.False(roles.All(r => r.Contains(Roles.RecordsViewer)), $"{action} lets a records viewer in");
                Assert.True(roles.All(r => r.Contains(Roles.HrOfficer)), $"{action} keeps HR out");
            }
        }

        [Fact]
        public void The_full_loan_and_course_lists_are_for_records_viewers()
        {
            Assert.True(Allows(typeof(LoanApprovalController), nameof(LoanApprovalController.All), Roles.RecordsViewer));
            Assert.True(Allows(typeof(CourseApprovalController), nameof(CourseApprovalController.All), Roles.RecordsViewer));
            Assert.False(Allows(typeof(LoanApprovalController), nameof(LoanApprovalController.All), Roles.Employee));
        }

        [Theory]
        [InlineData(nameof(LoanApprovalController.Approve))]
        [InlineData(nameof(LoanApprovalController.Reject))]
        [InlineData(nameof(LoanApprovalController.Return))]
        public void A_records_viewer_cannot_decide_a_loan(string action) =>
            Assert.False(Allows(typeof(LoanApprovalController), action, Roles.RecordsViewer));

        [Theory]
        [InlineData(nameof(CourseApprovalController.HrApprove))]
        [InlineData(nameof(CourseApprovalController.HrReject))]
        [InlineData(nameof(CourseApprovalController.Approve))]
        [InlineData(nameof(CourseApprovalController.Reject))]
        public void A_records_viewer_cannot_decide_a_course(string action) =>
            Assert.False(Allows(typeof(CourseApprovalController), action, Roles.RecordsViewer));
    }
}

using System.Security.Claims;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Tests.Policies
{
    public class PolicyServiceTests
    {
        private const int UserId = 100;
        private const int Staff = 10;

        private readonly ApplicationDbContext _db;
        private readonly PolicyService _policies;
        private readonly ClaimsPrincipal _user;

        public PolicyServiceTests()
        {
            _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            _db.Employees.Add(new Employee
            {
                Id = Staff,
                UserId = UserId,
                EmployeeNumber = "E1",
                FirstName = "Rudo",
                LastName = "Nyandoro",
                Email = "rudo@test.test",
                IsActive = true
            });

            _db.SaveChanges();

            _user = User();

            var accessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = _user }
            };

            var currentUser = new CurrentUserService(_db, accessor);

            _policies = new PolicyService(
                _db,
                currentUser,
                new AuditService(_db, accessor),
                null!);
        }

        private static ClaimsPrincipal User(params string[] roles) =>
            new(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, UserId.ToString()) }
                    .Concat(roles.Select(r => new Claim(ClaimTypes.Role, r))),
                "test"));

        private Policy Add(
            string title,
            PolicyStatus status = PolicyStatus.Active,
            bool requires = true,
            DocumentAudience audience = DocumentAudience.Everyone)
        {
            var policy = new Policy
            {
                Title = title,
                Status = status,
                RequiresAcknowledgement = requires,
                Audience = audience,
                FileName = "f",
                OriginalFileName = "f.pdf",
                FilePath = "/uploads/policies/f.pdf"
            };

            _db.Policies.Add(policy);
            _db.SaveChanges();

            return policy;
        }

        [Fact]
        public async Task Every_policy_in_force_that_asks_for_it_is_outstanding()
        {
            Add("In force");
            Add("Draft", PolicyStatus.Draft);
            Add("Withdrawn", PolicyStatus.Withdrawn);
            Add("Replaced", PolicyStatus.Superseded);
            Add("Just for information", requires: false);
            Add("Managers only", audience: DocumentAudience.Managers);

            var outstanding = await _policies.OutstandingForAsync(_user);

            Assert.Equal("In force", Assert.Single(outstanding).Title);
        }

        [Fact]
        public async Task A_policy_cannot_be_confirmed_before_it_is_opened()
        {
            var policy = Add("Conduct");

            var result = await _policies.AcknowledgeAsync(policy.Id, _user);

            Assert.False(result.Succeeded);
            Assert.Contains("open the document", result.Message);
            Assert.Empty(_db.PolicyAcknowledgements);
            Assert.Single(await _policies.OutstandingForAsync(_user));
        }

        [Fact]
        public async Task Once_opened_and_confirmed_the_reminder_goes_and_a_new_version_brings_it_back()
        {
            var policy = Add("Conduct");

            await _policies.RecordOpenedAsync(policy.Id);
            await _policies.RecordOpenedAsync(policy.Id);

            Assert.Single(_db.PolicyReadings);

            var result = await _policies.AcknowledgeAsync(policy.Id, _user);

            Assert.True(result.Succeeded);
            Assert.Empty(await _policies.OutstandingForAsync(_user));

            Add("Conduct v2");

            Assert.Equal("Conduct v2", Assert.Single(await _policies.OutstandingForAsync(_user)).Title);
        }

        [Fact]
        public async Task Somebody_with_no_employee_record_has_nothing_outstanding()
        {
            Add("Conduct");

            var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            var nobody = new PolicyService(_db, new CurrentUserService(_db, accessor), new AuditService(_db, accessor), null!);

            Assert.Empty(await nobody.OutstandingForAsync(new ClaimsPrincipal()));
        }

        [Theory]
        [InlineData(Roles.HrOfficer, true)]
        [InlineData(Roles.ComplianceOfficer, true)]
        [InlineData(Roles.Administrator, true)]
        [InlineData(Roles.Employee, false)]
        [InlineData(Roles.Md, false)]
        public void Hr_and_compliance_officers_manage_policies(string role, bool can)
        {
            Assert.Equal(can, PolicyService.CanManage(User(role)));
            Assert.Equal(can, AudienceRules.Curators.Split(',').Contains(role));
        }

        [Fact]
        public async Task A_compliance_officer_can_see_a_draft_they_manage_but_staff_cannot()
        {
            var draft = Add("Draft", PolicyStatus.Draft, audience: DocumentAudience.HrAndAdministrators);

            Assert.NotNull(await _policies.ReadableAsync(draft.Id, User(Roles.ComplianceOfficer)));
            Assert.Null(await _policies.ReadableAsync(draft.Id, User(Roles.Employee)));
        }
    }
}

using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Tests.Companies
{
    public class CompanyRoleTests : IDisposable
    {
        public void Dispose()
        {
            CompanyScope.Enter(null);
            Brand.Enter(Brand.Main);
        }

        private const int HrRole = 10;
        private const int MdRole = 11;
        private const int AdminRole = 12;

        private const int Tamara = 1;
        private const int Hope = 2;
        private const int Ivy = 3;
        private const int Mike = 4;
        private const int Admin = 5;

        private readonly ApplicationDbContext _db;
        private readonly ApproverDirectory _approvers;

        public CompanyRoleTests()
        {
            _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            _db.Database.EnsureCreated();

            _db.Roles.AddRange(
                new IdentityRole<int> { Id = HrRole, Name = Roles.HrOfficer },
                new IdentityRole<int> { Id = MdRole, Name = Roles.Md },
                new IdentityRole<int> { Id = AdminRole, Name = Roles.Administrator });

            AddPerson(Tamara, Company.MmcId, Company.IntellegoId, Company.MmcId);
            AddPerson(Hope, Company.IntellegoId, Company.IntellegoId);
            AddPerson(Ivy, Company.IntellegoId, Company.IntellegoId);
            AddPerson(Mike, Company.MmcId, Company.MmcId);
            AddPerson(Admin, Company.IntellegoId, Company.IntellegoId);

            _db.CompanyRoles.AddRange(
                new CompanyRole { UserId = 100 + Tamara, CompanyId = Company.MmcId, RoleId = HrRole },
                new CompanyRole { UserId = 100 + Hope, CompanyId = Company.IntellegoId, RoleId = HrRole });

            _db.UserRoles.Add(new IdentityUserRole<int> { UserId = 100 + Admin, RoleId = AdminRole });

            _db.SaveChanges();

            Brand.Enter(new Brand { Key = "intellego" });

            _approvers = new ApproverDirectory(_db, new CompanyService(_db));
        }

        private void AddPerson(int id, int home, params int[] companies)
        {
            _db.Users.Add(new ApplicationUser { Id = 100 + id, UserName = $"u{id}" });

            _db.Employees.Add(new Employee
            {
                Id = id,
                EmployeeNumber = $"E{id}",
                FirstName = $"Person{id}",
                LastName = "Test",
                Email = $"e{id}@test.test",
                DepartmentId = 1,
                UserId = 100 + id,
                HomeCompanyId = home,
                Companies = companies
                    .Select(c => new EmployeeCompany { CompanyId = c })
                    .ToList()
            });
        }

        [Theory]
        [InlineData(Ivy, Hope)]
        [InlineData(Hope, Hope)]
        [InlineData(Mike, Tamara)]
        [InlineData(Tamara, Tamara)]
        public async Task A_request_goes_to_HR_at_the_company_that_employs_the_person(
            int requester,
            int hr)
        {
            Assert.Equal(
                new[] { hr },
                await _approvers.EmployeeIdsForAsync(ApprovalRole.HROfficer, requester));
        }

        [Fact]
        public async Task HR_at_one_company_is_not_HR_at_the_other()
        {
            Assert.True(await _approvers.HoldsAsync(ApprovalRole.HROfficer, Tamara));
            Assert.True(await _approvers.HoldsAsync(ApprovalRole.HROfficer, Hope));
            Assert.False(await _approvers.HoldsAsync(ApprovalRole.HROfficer, Ivy));

            Assert.Equal(
                new[] { Hope },
                (await _approvers.PeopleAsync(ApprovalRole.HROfficer, Company.IntellegoId))
                    .Select(p => p.EmployeeId));

            Assert.Equal(
                new[] { Tamara },
                (await _approvers.PeopleAsync(ApprovalRole.HROfficer, Company.MmcId))
                    .Select(p => p.EmployeeId));
        }

        [Fact]
        public async Task A_company_with_nobody_in_a_role_says_so()
        {
            Assert.False(await _approvers.AnybodyAsync(ApprovalRole.MD, Company.MmcId));
            Assert.True(await _approvers.AnybodyAsync(ApprovalRole.HROfficer, Company.MmcId));
        }

        [Fact]
        public async Task The_administrator_holds_everywhere_and_a_company_role_needs_a_company()
        {
            Assert.Equal(
                new[] { Admin },
                await _approvers.EmployeeIdsWithRoleAsync(Roles.Administrator));

            await Assert.ThrowsAsync<ArgumentException>(
                () => _approvers.EmployeeIdsWithRoleAsync(Roles.HrOfficer));
        }

        [Theory]
        [InlineData(Roles.Administrator, false)]
        [InlineData(Roles.Employee, false)]
        [InlineData(Roles.HrOfficer, true)]
        [InlineData(Roles.Md, true)]
        [InlineData(Roles.ComplianceOfficer, true)]
        public void Only_the_administrator_and_employee_roles_hold_everywhere(string role, bool atOneCompany) =>
            Assert.Equal(atOneCompany, Roles.IsCompanyRole(role));
    }
}

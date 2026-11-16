using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Tests.Companies
{
    public class CompanyScopeTests : IDisposable
    {
        public void Dispose()
        {
            CompanyScope.Enter(null);
            Brand.Enter(Brand.Main);
        }

        private const int Ivy = 1;
        private const int Tamara = 2;
        private const int Mike = 3;

        private readonly ApplicationDbContext _db;

        public CompanyScopeTests()
        {
            _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            _db.Database.EnsureCreated();

            AddPerson(Ivy, Company.IntellegoId);
            AddPerson(Tamara, Company.IntellegoId, Company.MmcId);
            AddPerson(Mike, Company.MmcId);

            _db.SaveChanges();
            _db.ChangeTracker.Clear();
        }

        private void AddPerson(int id, params int[] companies)
        {
            _db.Employees.Add(new Employee
            {
                Id = id,
                EmployeeNumber = $"E{id}",
                FirstName = $"Person{id}",
                LastName = "Test",
                Email = $"e{id}@test.test",
                DepartmentId = 1,
                HomeCompanyId = companies[0],
                Companies = companies
                    .Select(c => new EmployeeCompany { CompanyId = c })
                    .ToList()
            });

            _db.LeaveRequests.Add(new LeaveRequest
            {
                Id = id,
                EmployeeId = id,
                LeaveTypeId = 1,
                RequestNumber = $"LR-{id}"
            });

            _db.LoanApplications.Add(new LoanApplication
            {
                Id = id,
                EmployeeId = id,
                ApplicationNumber = $"LN-{id}"
            });
        }

        private List<int> StaffAt(int? company)
        {
            CompanyScope.Enter(company);

            return _db.StaffHere.Select(e => e.Id).OrderBy(i => i).ToList();
        }

        private List<int> LeaveAt(int? company)
        {
            CompanyScope.Enter(company);

            return _db.LeaveRequests.Select(r => r.EmployeeId).OrderBy(i => i).ToList();
        }

        [Fact]
        public void Each_portal_lists_only_the_people_who_work_there()
        {
            Assert.Equal(new[] { Ivy, Tamara }, StaffAt(Company.IntellegoId));
            Assert.Equal(new[] { Tamara, Mike }, StaffAt(Company.MmcId));
        }

        [Fact]
        public void Records_follow_the_person_so_a_shared_employee_is_seen_in_both()
        {
            Assert.Equal(new[] { Ivy, Tamara }, LeaveAt(Company.IntellegoId));
            Assert.Equal(new[] { Tamara, Mike }, LeaveAt(Company.MmcId));

            CompanyScope.Enter(Company.MmcId);

            Assert.Null(_db.LoanApplications.FirstOrDefault(l => l.EmployeeId == Ivy));
        }

        [Fact]
        public void Work_done_outside_a_request_sees_every_company()
        {
            Assert.Equal(new[] { Ivy, Tamara, Mike }, StaffAt(null));
            Assert.Equal(new[] { Ivy, Tamara, Mike }, LeaveAt(null));
        }

        [Fact]
        public void A_company_the_portal_could_not_find_sees_nothing()
        {
            Assert.Empty(StaffAt(-1));
            Assert.Empty(LeaveAt(-1));
        }
    }
}

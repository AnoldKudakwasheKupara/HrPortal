using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Tests.Companies
{
    public class CompanyServiceTests
    {
        private const int IntellegoOnly = 1;
        private const int Shared = 2;
        private const int MmcOnly = 3;

        private readonly ApplicationDbContext _db;

        public CompanyServiceTests()
        {
            _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            _db.Database.EnsureCreated();

            AddEmployee(IntellegoOnly, Company.IntellegoId, Company.IntellegoId);
            AddEmployee(Shared, Company.IntellegoId, Company.IntellegoId, Company.MmcId);
            AddEmployee(MmcOnly, Company.MmcId, Company.MmcId);

            _db.SaveChanges();
        }

        private void AddEmployee(int id, int home, params int[] companies) =>
            _db.Employees.Add(new Employee
            {
                Id = id,
                EmployeeNumber = $"E{id}",
                FirstName = $"Person{id}",
                LastName = "Test",
                Email = $"e{id}@test.test",
                DepartmentId = 1,
                HomeCompanyId = home,
                Companies = companies
                    .Select(c => new EmployeeCompany { CompanyId = c })
                    .ToList()
            });

        private CompanyService At(string key)
        {
            Brand.Enter(new Brand { Key = key });

            return new CompanyService(_db);
        }

        [Fact]
        public async Task Both_companies_are_there_from_the_start_and_the_address_picks_one()
        {
            Assert.Equal(
                new[] { "intellego", "mmc" },
                (await At("intellego").AllAsync()).Select(c => c.Key));

            Assert.Equal(Company.IntellegoId, await At("intellego").CurrentIdAsync());
            Assert.Equal(Company.MmcId, await At("mmc").CurrentIdAsync());
        }

        [Fact]
        public async Task A_brand_with_no_company_is_an_error_not_somebody_else_s_data()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => At("nobody").CurrentAsync());
        }

        [Theory]
        [InlineData("intellego", IntellegoOnly, true)]
        [InlineData("mmc", IntellegoOnly, false)]
        [InlineData("intellego", Shared, true)]
        [InlineData("mmc", Shared, true)]
        [InlineData("intellego", MmcOnly, false)]
        [InlineData("mmc", MmcOnly, true)]
        public async Task People_work_only_where_they_are_members(
            string portal,
            int employeeId,
            bool expected)
        {
            Assert.Equal(expected, await At(portal).WorksHereAsync(employeeId));
        }

        [Fact]
        public async Task A_new_brand_gets_its_own_company_once()
        {
            var brands = new[]
            {
                new Brand { Key = "intellego" },
                new Brand { Key = "mmc" },
                new Brand { Key = "example", FullName = "Example Holdings" }
            };

            await CompanyService.EnsureForBrandsAsync(_db, brands);
            await CompanyService.EnsureForBrandsAsync(_db, brands);

            var companies = await _db.Companies.OrderBy(c => c.Id).ToListAsync();

            Assert.Equal(new[] { "intellego", "mmc", "example" }, companies.Select(c => c.Key));
            Assert.Equal(3, companies[2].Id);
            Assert.Equal("Example Holdings", companies[2].Name);
        }
    }
}

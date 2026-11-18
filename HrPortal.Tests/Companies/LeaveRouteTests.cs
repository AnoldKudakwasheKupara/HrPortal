using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HrPortal.Tests.Companies
{
    [Collection("Brand list")]
    public sealed class LeaveRouteTests : IDisposable
    {
        private const int AtIntellego = 1;
        private const int AtMmc = 2;

        private static readonly string PortalFolder = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HrPortal"));

        private readonly List<Brand> _brandsBefore = Brand.All.ToList();
        private readonly string _mainBefore = Brand.Main.Key;
        private readonly ApplicationDbContext _db;

        public LeaveRouteTests()
        {
            var (brands, main) = Brand.LoadAll(new ConfigurationBuilder().Build(), PortalFolder);

            Brand.UseAll(brands, main);

            _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            _db.Database.EnsureCreated();

            _db.Employees.AddRange(Person(AtIntellego, Company.IntellegoId), Person(AtMmc, Company.MmcId));
            _db.SaveChanges();
        }

        public void Dispose() => Brand.UseAll(_brandsBefore, _mainBefore);

        private static Employee Person(int id, int home) =>
            new()
            {
                Id = id,
                EmployeeNumber = $"E{id}",
                FirstName = "Test",
                LastName = $"Person{id}",
                Email = $"e{id}@test.test",
                DepartmentId = 1,
                HomeCompanyId = home
            };

        private LeaveWorkflowService Workflow() =>
            new(_db, new LeaveBalanceService(_db), null!, null!, new WorkingCalendar(_db), null!, null!);

        [Fact]
        public void MMC_names_its_approvers_its_own_way_and_HR_decides_its_leave()
        {
            var mmc = Brand.ForKey("mmc")!;
            var intellego = Brand.ForKey("intellego")!;

            Assert.True(mmc.LeaveEndsWithHr);
            Assert.Equal("HR Manager", mmc.HrTitle);
            Assert.Equal("a director", mmc.TheDirector);
            Assert.Equal("a director", mmc.TheCourseApprover);

            Assert.False(intellego.LeaveEndsWithHr);
            Assert.Equal("the Managing Consultant", intellego.TheCourseApprover);
        }

        [Fact]
        public async Task HR_has_the_last_word_on_leave_for_the_people_MMC_employs_only()
        {
            Assert.True(await Workflow().HrDecidesAsync(AtMmc));
            Assert.False(await Workflow().HrDecidesAsync(AtIntellego));
        }
    }
}

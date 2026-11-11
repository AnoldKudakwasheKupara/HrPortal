using HrPortal.Entities;
using HrPortal.ViewModels;

namespace HrPortal.Tests.Common
{
    public class RequestGroupsTests
    {
        private static readonly DateTime Today = new(2026, 11, 11);

        [Fact]
        public void A_page_of_a_list_in_memory_stays_in_range()
        {
            var items = Enumerable.Range(1, 23).ToList();

            Assert.Equal(new[] { 21, 22, 23 }, PagedList.Of(items, 3).Items);
            Assert.Equal(3, PagedList.Of(items, 99).Page);
            Assert.Equal(1, PagedList.Of(items, 0).Page);
            Assert.Equal(1, PagedList.Of(new List<int>(), 4).Page);
            Assert.Equal(0, PagedList.Of(new List<int>(), 4).TotalPages);
        }

        [Fact]
        public void Tabs_count_each_group()
        {
            var items = new (bool Final, bool Ok)[] { (false, false), (true, true), (true, true), (true, false) };

            var tabs = RequestGroups.Tabs(items, i => i.Final, i => i.Ok, "All");

            Assert.Equal(new[] { 4, 1, 2, 1 }, tabs.Select(t => t.Count));
            Assert.Equal(new[] { "all", "open", "approved", "closed" }, tabs.Select(t => t.Key));
        }

        [Fact]
        public void My_courses_counts_and_finds_the_next_exam()
        {
            CourseApplication App(CourseApplicationStatus status, DateTime? exam) =>
                new() { Status = status, TargetExamDate = exam, ProgramIntended = status.ToString() };

            var applications = new List<CourseApplication>
            {
                App(CourseApplicationStatus.Approved, Today.AddDays(40)),
                App(CourseApplicationStatus.Submitted, Today.AddDays(10)),
                App(CourseApplicationStatus.Rejected, Today.AddDays(2)),
                App(CourseApplicationStatus.Approved, Today.AddDays(-5)),
                App(CourseApplicationStatus.Cancelled, null)
            };

            var model = MyCoursesIndexViewModel.Build(applications, "approved", 1, Today);

            Assert.Equal(1, model.InProgress);
            Assert.Equal(2, model.Approved);
            Assert.Equal(2, model.Page.TotalCount);
            Assert.Equal("Submitted", model.NextExam?.ProgramIntended);
            Assert.Equal(new[] { 5, 1, 2, 2 }, model.Tabs.Select(t => t.Count));
            Assert.Equal("all", MyCoursesIndexViewModel.Build(applications, "nonsense", 1, Today).Show);
        }
    }
}

using HrPortal.Entities;
using HrPortal.ViewModels;

namespace HrPortal.Tests.Leave
{
    public class MyLeaveListTests
    {
        private static readonly DateTime Today = new(2026, 11, 11);

        private static LeaveRequest Request(LeaveRequestStatus status, DateTime start, decimal days = 1) => new()
        {
            Status = status,
            StartDate = start,
            EndDate = start.AddDays((double)days - 1),
            NumberOfDays = days
        };

        [Theory]
        [InlineData(LeaveRequestStatus.Draft, "open")]
        [InlineData(LeaveRequestStatus.SupervisorReview, "open")]
        [InlineData(LeaveRequestStatus.MDReview, "open")]
        [InlineData(LeaveRequestStatus.Approved, "approved")]
        [InlineData(LeaveRequestStatus.Rejected, "closed")]
        [InlineData(LeaveRequestStatus.SupervisorRejected, "closed")]
        [InlineData(LeaveRequestStatus.Cancelled, "closed")]
        public void Each_status_falls_in_one_group_and_in_all(LeaveRequestStatus status, string group)
        {
            foreach (var g in new[] { MyLeaveList.Open, MyLeaveList.Approved, MyLeaveList.Closed })
                Assert.Equal(g == group, MyLeaveList.InGroup(status, g));

            Assert.True(MyLeaveList.InGroup(status, MyLeaveList.All));
        }

        [Fact]
        public void Unknown_groups_show_everything()
        {
            Assert.Equal(MyLeaveList.All, MyLeaveList.Normalise("bogus"));
            Assert.Equal(MyLeaveList.All, MyLeaveList.Normalise(null));
            Assert.Equal(MyLeaveList.Closed, MyLeaveList.Normalise("closed"));
        }

        [Fact]
        public void Requests_come_ten_a_page_within_their_group()
        {
            var requests = Enumerable.Range(0, 12)
                .Select(i => Request(LeaveRequestStatus.Approved, Today.AddDays(i)))
                .Append(Request(LeaveRequestStatus.Cancelled, Today))
                .ToList();

            var second = MyLeaveList.Page(requests, MyLeaveList.Approved, 2);

            Assert.Equal(2, second.Items.Count);
            Assert.Equal(12, second.TotalCount);
            Assert.Equal(2, second.TotalPages);
            Assert.Equal(13, MyLeaveList.Page(requests, MyLeaveList.All, 1).TotalCount);
            Assert.Equal(1, MyLeaveList.Page(requests, MyLeaveList.Closed, 7).Page);
        }

        [Fact]
        public void Days_taken_count_only_approved_leave_starting_that_year()
        {
            var requests = new[]
            {
                Request(LeaveRequestStatus.Approved, new DateTime(2026, 3, 2), 3),
                Request(LeaveRequestStatus.Approved, new DateTime(2026, 12, 21), 2.5m),
                Request(LeaveRequestStatus.Approved, new DateTime(2025, 12, 29), 4),
                Request(LeaveRequestStatus.Rejected, new DateTime(2026, 5, 4), 5),
                Request(LeaveRequestStatus.MDReview, new DateTime(2026, 6, 1), 1)
            };

            Assert.Equal(5.5m, MyLeaveList.DaysTaken(requests, 2026));
        }

        [Fact]
        public void Next_leave_is_the_soonest_approved_leave_not_yet_over()
        {
            var current = Request(LeaveRequestStatus.Approved, Today.AddDays(-1), 3);
            var later = Request(LeaveRequestStatus.Approved, Today.AddDays(20));

            var requests = new[]
            {
                Request(LeaveRequestStatus.Approved, Today.AddDays(-10)),
                later,
                Request(LeaveRequestStatus.MDReview, Today.AddDays(2)),
                current
            };

            Assert.Same(current, MyLeaveList.Next(requests, Today));
            Assert.Null(MyLeaveList.Next(new[] { Request(LeaveRequestStatus.Cancelled, Today.AddDays(5)) }, Today));
        }
    }
}

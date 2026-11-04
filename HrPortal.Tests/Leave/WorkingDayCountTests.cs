using HrPortal.Services;

namespace HrPortal.Tests.Leave
{
    public class WorkingDayCountTests
    {
        [Fact]
        public void Leave_running_through_a_weekend_does_not_count_the_saturday_and_sunday() =>
            Assert.Equal(5, WorkingCalendar.CountWorkingDays(new DateTime(2026, 12, 3), new DateTime(2026, 12, 9)));

        [Fact]
        public void A_whole_two_weeks_is_ten_days() =>
            Assert.Equal(10, WorkingCalendar.CountWorkingDays(new DateTime(2026, 11, 9), new DateTime(2026, 11, 22)));

        [Fact]
        public void A_weekend_alone_is_no_days() =>
            Assert.Equal(0, WorkingCalendar.CountWorkingDays(new DateTime(2026, 11, 7), new DateTime(2026, 11, 8)));

        [Fact]
        public void A_public_holiday_does_not_count_either() =>
            Assert.Equal(
                4,
                WorkingCalendar.CountWorkingDays(
                    new DateTime(2026, 12, 3),
                    new DateTime(2026, 12, 9),
                    new HashSet<DateTime> { new(2026, 12, 8) }));
    }
}

using HrPortal.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    public class WorkingCalendar
    {
        private readonly ApplicationDbContext _context;

        public WorkingCalendar(ApplicationDbContext context)
        {
            _context = context;
        }

        public static decimal CountWorkingDays(
            DateTime startDate,
            DateTime endDate,
            IReadOnlySet<DateTime>? holidays = null)
        {
            var days = 0;

            for (var date = startDate.Date;
                 date <= endDate.Date;
                 date = date.AddDays(1))
            {
                if (date.DayOfWeek == DayOfWeek.Saturday ||
                    date.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                if (holidays != null && holidays.Contains(date))
                    continue;

                days++;
            }

            return days;
        }

        public async Task<IReadOnlySet<DateTime>> HolidaysAsync(
            DateTime startDate,
            DateTime endDate)
        {
            var dates = await _context.PublicHolidays
                .AsNoTracking()
                .Where(h =>
                    h.Date >= startDate.Date &&
                    h.Date <= endDate.Date)
                .Select(h => h.Date)
                .ToListAsync();

            return dates.Select(d => d.Date).ToHashSet();
        }

        public async Task<decimal> CountWorkingDaysAsync(
            DateTime startDate,
            DateTime endDate)
        {
            var holidays = await HolidaysAsync(startDate, endDate);

            return CountWorkingDays(startDate, endDate, holidays);
        }
    }
}

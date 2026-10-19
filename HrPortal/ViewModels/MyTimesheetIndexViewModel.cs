using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class MyTimesheetIndexViewModel
    {
        public DateTime WeekStart { get; set; }

        public DateTime WeekEnd => WeekStart.AddDays(6);

        public bool IsCurrentWeek { get; set; }

        public string DepartmentName { get; set; } = string.Empty;

        public List<TimesheetEntry> Entries { get; set; } = new();

        public int MinutesThisWeek { get; set; }

        public int MinutesThisMonth { get; set; }

        public int EntriesThisMonth { get; set; }

        public Dictionary<DateTime, int> DailyMinutes { get; set; } = new();

        public int DaysLoggedThisWeek =>
            DailyMinutes.Count(d => d.Value > 0);
    }
}

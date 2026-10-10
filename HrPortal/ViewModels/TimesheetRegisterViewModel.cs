using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    /// <summary>
    /// The administrator's view across everyone's timesheets for a
    /// date range.
    /// </summary>
    public class TimesheetRegisterViewModel
    {
        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public int? EmployeeId { get; set; }

        public WorkCategory? Category { get; set; }

        public List<TimesheetEntry> Entries { get; set; } = new();

        public int TotalMinutes { get; set; }

        public int People { get; set; }

        public int WorkingDays { get; set; }

        public List<TimesheetPersonSummary> ByPerson { get; set; } = new();

        public List<TimesheetCategorySummary> ByCategory { get; set; } = new();
    }

    public class TimesheetPersonSummary
    {
        public int EmployeeId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public int Entries { get; set; }

        public int Minutes { get; set; }

        public int DaysLogged { get; set; }
    }

    public class TimesheetCategorySummary
    {
        public WorkCategory Category { get; set; }

        public int Entries { get; set; }

        public int Minutes { get; set; }
    }
}

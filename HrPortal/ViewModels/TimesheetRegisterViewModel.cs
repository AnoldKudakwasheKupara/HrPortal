using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class TimesheetRegisterViewModel
    {
        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public int? EmployeeId { get; set; }

        public WorkCategory? Category { get; set; }

        public List<TimesheetRowViewModel> Entries { get; set; } = new();

        public int TotalMinutes { get; set; }

        public int People { get; set; }

        public int WorkingDays { get; set; }

        public int TotalEntries { get; set; }

        public int Page { get; set; } = 1;

        public int TotalPages { get; set; }

        public List<TimesheetPersonSummary> ByPerson { get; set; } = new();

        public List<TimesheetCategorySummary> ByCategory { get; set; } = new();
    }

    public class TimesheetRowViewModel
    {
        public int Id { get; set; }

        public DateTime WorkDate { get; set; }

        public string EmployeeName { get; set; } = string.Empty;

        public string DepartmentName { get; set; } = string.Empty;

        public WorkCategory Category { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Details { get; set; }

        public string? Reference { get; set; }

        public string? AssetModel { get; set; }

        public string? AssetSerialNumber { get; set; }

        public TimeOnly? StartTime { get; set; }

        public TimeOnly? EndTime { get; set; }

        public int DurationMinutes { get; set; }
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

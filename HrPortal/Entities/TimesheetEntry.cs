using HrPortal.Security;
using System.ComponentModel.DataAnnotations.Schema;

namespace HrPortal.Entities
{
    public class TimesheetEntry : IEmployeeOwned
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public DateTime WorkDate { get; set; }

        public TimeOnly? StartTime { get; set; }

        public TimeOnly? EndTime { get; set; }

        public int DurationMinutes { get; set; }

        public WorkCategory Category { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Details { get; set; }

        public string? Reference { get; set; }

        public int? AssetId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public Employee Employee { get; set; } = null!;

        public Asset? Asset { get; set; }

        [NotMapped]
        public decimal Hours => DurationMinutes / 60m;

        public static string FormatDuration(int minutes)
        {
            var hours = minutes / 60;
            var rest = minutes % 60;

            if (hours == 0)
                return rest + "m";

            return rest == 0
                ? hours + "h"
                : hours + "h " + rest.ToString("00") + "m";
        }
    }
}

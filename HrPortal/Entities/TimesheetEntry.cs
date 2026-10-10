using HrPortal.Security;
using System.ComponentModel.DataAnnotations.Schema;

namespace HrPortal.Entities
{
    /// <summary>
    /// One piece of work logged by a member of staff whose department
    /// keeps timesheets: what was done, for how long, and on what.
    ///
    /// Time is stored as whole minutes. Start and end are optional,
    /// for people who clock a job rather than estimate it; when both
    /// are given the duration is derived from them.
    /// </summary>
    public class TimesheetEntry : IEmployeeOwned
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public DateTime WorkDate { get; set; }

        public TimeOnly? StartTime { get; set; }

        public TimeOnly? EndTime { get; set; }

        public int DurationMinutes { get; set; }

        public WorkCategory Category { get; set; }

        /// <summary>A one-line summary of the task.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>What was done, found, or left outstanding.</summary>
        public string? Details { get; set; }

        /// <summary>A ticket, request or job number, if there is one.</summary>
        public string? Reference { get; set; }

        /// <summary>The machine on the register the work was on, if any.</summary>
        public int? AssetId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public Employee Employee { get; set; } = null!;

        public Asset? Asset { get; set; }

        [NotMapped]
        public decimal Hours => DurationMinutes / 60m;

        /// <summary>Renders minutes as 2h 15m, 45m, 3h and so on.</summary>
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

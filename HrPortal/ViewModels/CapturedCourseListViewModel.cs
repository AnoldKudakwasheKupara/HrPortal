using HrPortal.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HrPortal.ViewModels
{
    public class CapturedCourseRow
    {
        public int Id { get; set; }

        public string ApplicationNumber { get; set; } = string.Empty;

        public string EmployeeName { get; set; } = string.Empty;

        public string Programme { get; set; } = string.Empty;

        public string Justification { get; set; } = string.Empty;

        public DateTime CapturedAt { get; set; }

        public string CapturedBy { get; set; } = string.Empty;

        public DateTime? ApprovedOn { get; set; }

        public string? ApprovedBy { get; set; }

        public CourseApplicationStatus Status { get; set; }
    }

    /// <summary>Another course application by the same employee.</summary>
    public sealed record RelatedCourse(
        int Id,
        string ApplicationNumber,
        string Programme,
        CourseApplicationStatus Status,
        DateTime Date,
        bool Captured);

    /// <summary>The captured course approvals, filtered and a page at a time.</summary>
    public class CapturedCourseListViewModel
    {
        public PagedResult<CapturedCourseRow> Page { get; set; } = new();

        public int Total { get; set; }

        public int Employees { get; set; }

        public int ThisMonth { get; set; }

        public int ByYou { get; set; }

        public string? Search { get; set; }

        public int? EmployeeId { get; set; }

        /// <summary>Approved on or after.</summary>
        public DateTime? From { get; set; }

        /// <summary>Approved on or before.</summary>
        public DateTime? To { get; set; }

        public bool Filtered =>
            !string.IsNullOrWhiteSpace(Search) || EmployeeId != null || From != null || To != null;

        public SelectList EmployeeOptions { get; set; } = new(Array.Empty<object>());
    }
}

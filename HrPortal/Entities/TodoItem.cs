using System.ComponentModel.DataAnnotations;
using HrPortal.Security;

namespace HrPortal.Entities
{
    public enum TodoStatus
    {
        [Display(Name = "Not started")]
        NotStarted,

        [Display(Name = "In progress")]
        InProgress,

        [Display(Name = "Done")]
        Done
    }

    /// <summary>
    /// One thing on somebody's to-do list. Only they can change it;
    /// their supervisor can see it and where it stands.
    /// </summary>
    public class TodoItem : IEmployeeOwned
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public DateTime? DueDate { get; set; }

        public TodoStatus Status { get; set; } = TodoStatus.NotStarted;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public Employee Employee { get; set; } = null!;

        public bool IsOverdue(DateTime today) =>
            Status != TodoStatus.Done && DueDate != null && DueDate.Value.Date < today.Date;
    }
}

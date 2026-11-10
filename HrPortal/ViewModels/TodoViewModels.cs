using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;
using HrPortal.Services;

namespace HrPortal.ViewModels
{
    public class TodoItemViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Say what needs doing.")]
        [StringLength(200)]
        [Display(Name = "What needs doing")]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Notes { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Due")]
        public DateTime? DueDate { get; set; }

        public TodoStatus Status { get; set; } = TodoStatus.NotStarted;
    }

    public class TodoListViewModel
    {
        public TodoCounts Counts { get; set; } = new(0, 0, 0, 0);

        /// <summary>The status group being shown.</summary>
        public TodoStatus Status { get; set; }

        public PagedResult<TodoItem> Page { get; set; } = new();

        /// <summary>False when a supervisor is looking at somebody else's list.</summary>
        public bool CanEdit { get; set; }

        public TodoItemViewModel? New { get; set; }
    }
}

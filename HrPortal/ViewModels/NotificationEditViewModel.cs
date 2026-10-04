using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class NotificationEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Required]
        [Display(Name = "Title")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Message")]
        [StringLength(2000)]
        public string Message { get; set; } = string.Empty;

        [Display(Name = "Link")]
        [StringLength(500)]
        public string? Link { get; set; }

        [Display(Name = "Read")]
        public bool IsRead { get; set; }
    }
}

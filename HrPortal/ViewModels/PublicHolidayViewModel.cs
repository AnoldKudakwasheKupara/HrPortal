using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class PublicHolidayViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Holiday")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }
    }
}

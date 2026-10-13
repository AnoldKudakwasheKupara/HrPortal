using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class ItActivityEditViewModel : ItActivityCreateViewModel
    {
        [Required]
        public int Id { get; set; }

        [Display(Name = "Logged by")]
        public string? LoggedBy { get; set; }
    }
}

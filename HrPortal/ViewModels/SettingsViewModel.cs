using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class SettingsViewModel
    {
        [Display(Name = "Leave backdating allowed")]
        [Range(0, 365)]
        public int BackdateGraceDays { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    /// <summary>
    /// The settings worth changing without a deployment. Mail settings
    /// are deliberately absent: they include a password, and a password
    /// in a database table that any administrator can read is worse
    /// than one in a protected configuration file.
    /// </summary>
    public class SettingsViewModel
    {
        [Display(Name = "MD approval threshold")]
        [Range(0, 100000000)]
        public decimal MdApprovalThreshold { get; set; }

        [Required]
        [Display(Name = "Currency")]
        [StringLength(10)]
        public string CurrencyCode { get; set; } = "USD";

        [Display(Name = "Leave backdating allowed")]
        [Range(0, 365)]
        public int BackdateGraceDays { get; set; }

        [Display(Name = "Reminders switched on")]
        public bool RemindersEnabled { get; set; }

        [Display(Name = "Reminder run hour")]
        [Range(0, 23)]
        public int RunAtHour { get; set; }

        [Display(Name = "Warn before expiry")]
        [Range(1, 365)]
        public int ExpiryWindowDays { get; set; }

        [Display(Name = "Repeat a reminder after")]
        [Range(1, 365)]
        public int RepeatEveryDays { get; set; }

        [Display(Name = "Chase an approver after")]
        [Range(1, 60)]
        public int StaleApprovalDays { get; set; }
    }
}

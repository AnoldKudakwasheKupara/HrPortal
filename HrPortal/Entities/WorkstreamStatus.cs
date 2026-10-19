using System.ComponentModel.DataAnnotations;

namespace HrPortal.Entities
{
    public enum WorkstreamStatus
    {
        [Display(Name = "None")]
        None,

        [Display(Name = "Active")]
        Active,

        [Display(Name = "Ongoing")]
        Ongoing,

        [Display(Name = "Monitoring Ongoing")]
        MonitoringOngoing,

        [Display(Name = "Active / Monitoring Ongoing")]
        ActiveMonitoringOngoing,

        [Display(Name = "Continuous")]
        Continuous,

        [Display(Name = "In Progress")]
        InProgress,

        [Display(Name = "Completed")]
        Completed,

        [Display(Name = "On Hold")]
        OnHold
    }
}

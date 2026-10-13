using System.ComponentModel.DataAnnotations;

namespace HrPortal.Entities
{
    /// <summary>
    /// The standing vocabulary used against each workstream in the
    /// weekly report, kept to a fixed set so the wording stays
    /// consistent from one week to the next.
    /// </summary>
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

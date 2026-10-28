using System.ComponentModel.DataAnnotations;

namespace HrPortal.Entities
{
    /// <summary>
    /// A stage a request has to clear, named after the role that
    /// clears it.
    ///
    /// Separate from the sign-in role on purpose: one is a point in
    /// a workflow, the other is something a person is granted.
    /// ApproverDirectory is the only place that has to know which
    /// sign-in role carries which stage.
    /// </summary>
    public enum ApprovalRole
    {
        [Display(Name = "HR Officer")]
        HROfficer,

        [Display(Name = "Managing Director")]
        MD
    }
}

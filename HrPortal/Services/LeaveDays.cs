using HrPortal.Entities;

namespace HrPortal.Services
{
    /// <summary>
    /// One leave type and the balance held for it, if any, as shown
    /// beside a leave application.
    /// </summary>
    public sealed record LeaveDays(LeaveType Type, LeaveBalance? Balance);

    /// <summary>
    /// The leave days panel: whose days they are, and which type the
    /// application in front of the reader is for, so it stands out.
    /// </summary>
    public sealed record LeaveDaysPanel(
        string Heading,
        IReadOnlyList<LeaveDays> Rows,
        int? AppliedFor = null);
}

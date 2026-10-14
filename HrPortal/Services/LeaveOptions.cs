namespace HrPortal.Services
{
    /// <summary>
    /// Rules for leave that an organisation may reasonably want to set
    /// differently, kept in configuration rather than in code.
    /// </summary>
    public class LeaveOptions
    {
        public const string Section = "Leave";

        /// <summary>
        /// How far back a request may start. All leave in the portal is
        /// raised by the employee, so some backdating has to be allowed
        /// or sick leave could never be recorded after the fact. Set it
        /// to 0 to refuse any start date before today.
        /// </summary>
        public int BackdateGraceDays { get; set; } = 7;
    }
}

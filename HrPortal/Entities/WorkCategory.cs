namespace HrPortal.Entities
{
    /// <summary>
    /// The kind of work a timesheet entry records. Broad on purpose:
    /// the detail lives in the entry itself.
    /// </summary>
    public enum WorkCategory
    {
        Support,
        Maintenance,
        Installation,
        Development,
        Infrastructure,
        Security,
        Meeting,
        Training,
        Administration,
        Other
    }
}

namespace HrPortal.Entities
{
    /// <summary>
    /// A day the organisation does not work, beyond the weekend.
    ///
    /// Held as one row per actual date rather than a rule, because the
    /// dates that matter are a mix of fixed ones such as Christmas and
    /// movable ones such as Easter or a holiday shifted when it falls
    /// on a Sunday. A calendar HR fills in each year is both simpler
    /// and more honest than trying to compute them.
    /// </summary>
    public class PublicHoliday
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

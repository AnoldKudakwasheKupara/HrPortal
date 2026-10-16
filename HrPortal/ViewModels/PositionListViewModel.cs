namespace HrPortal.ViewModels
{
    /// <summary>
    /// A row of the position register, with how many people hold it.
    /// </summary>
    public class PositionListViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Department { get; set; }

        public int? Establishment { get; set; }

        public int Filled { get; set; }

        public bool IsActive { get; set; }

        /// <summary>
        /// Only meaningful where a budgeted number has been set. A post
        /// with no establishment is never reported as having vacancies.
        /// </summary>
        public int Vacancies =>
            Establishment == null
                ? 0
                : Math.Max(0, Establishment.Value - Filled);

        public bool IsOverFilled =>
            Establishment != null && Filled > Establishment.Value;
    }
}

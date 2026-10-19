namespace HrPortal.ViewModels
{
    public class PositionListViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Department { get; set; }

        public int? Establishment { get; set; }

        public int Filled { get; set; }

        public bool IsActive { get; set; }

        public int Vacancies =>
            Establishment == null
                ? 0
                : Math.Max(0, Establishment.Value - Filled);

        public bool IsOverFilled =>
            Establishment != null && Filled > Establishment.Value;
    }
}

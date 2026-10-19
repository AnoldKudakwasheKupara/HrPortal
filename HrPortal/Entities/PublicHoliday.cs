namespace HrPortal.Entities
{
    public class PublicHoliday
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

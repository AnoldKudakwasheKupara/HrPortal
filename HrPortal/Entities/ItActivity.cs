namespace HrPortal.Entities
{
    public class ItActivity
    {
        public int Id { get; set; }

        public DateTime LoggedOn { get; set; }

        public ItActivityCategory Category { get; set; }

        public ItActivityStatus Status { get; set; }
            = ItActivityStatus.Open;

        public string Summary { get; set; } = string.Empty;

        public string? ActionTaken { get; set; }

        public int? RaisedByEmployeeId { get; set; }

        public string? RaisedByOther { get; set; }

        public int? AssetId { get; set; }

        public DateTime? ResolvedOn { get; set; }

        public bool IsNotable { get; set; }

        public string? LoggedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public Employee? RaisedByEmployee { get; set; }

        public Asset? Asset { get; set; }
    }
}

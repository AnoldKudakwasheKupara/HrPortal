namespace HrPortal.Entities
{
    public class Asset
    {
        public int Id { get; set; }

        public string DeviceModel { get; set; } = string.Empty;

        public string? Company { get; set; }

        public string? Processor { get; set; }

        public string? SerialNumber { get; set; }

        public DateTime? ProcuredOn { get; set; }

        public AssetCondition Condition { get; set; }

        public string? Notes { get; set; }

        public bool IsInUse { get; set; } = true;

        public int? AssignedToEmployeeId { get; set; }

        public DateTime? AssignedOn { get; set; }

        public Employee? AssignedToEmployee { get; set; }
    }
}

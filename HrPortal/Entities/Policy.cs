namespace HrPortal.Entities
{
    public class Policy
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Reference { get; set; }

        public string? Category { get; set; }

        public string Version { get; set; } = "1.0";

        public PolicyStatus Status { get; set; } = PolicyStatus.Draft;

        public DocumentAudience Audience { get; set; }
            = DocumentAudience.Everyone;

        public string? Owner { get; set; }

        public string? Summary { get; set; }

        public DateTime EffectiveDate { get; set; }

        public DateTime? ReviewDate { get; set; }

        public bool RequiresAcknowledgement { get; set; } = true;

        public string FileName { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public int? SupersedesPolicyId { get; set; }

        public DateTime? PublishedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? CreatedByName { get; set; }

        public Policy? Supersedes { get; set; }

        public ICollection<PolicyAcknowledgement> Acknowledgements { get; set; }
            = new List<PolicyAcknowledgement>();
    }
}

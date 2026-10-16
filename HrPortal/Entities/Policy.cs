namespace HrPortal.Entities
{
    /// <summary>
    /// An organisation policy: the document, which version it is, when
    /// it took effect and when it should next be looked at.
    ///
    /// Kept apart from the document repository because a policy is not
    /// just a file on a shelf. It has a version, a date it came into
    /// force, an owner who is answerable for it, and in most cases a
    /// record of who has read it.
    /// </summary>
    public class Policy
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>A policy number, where the organisation uses them.</summary>
        public string? Reference { get; set; }

        public string? Category { get; set; }

        /// <summary>Free text, since organisations number them differently.</summary>
        public string Version { get; set; } = "1.0";

        public PolicyStatus Status { get; set; } = PolicyStatus.Draft;

        public DocumentAudience Audience { get; set; }
            = DocumentAudience.Everyone;

        /// <summary>Who is answerable for keeping it current.</summary>
        public string? Owner { get; set; }

        public string? Summary { get; set; }

        public DateTime EffectiveDate { get; set; }

        /// <summary>
        /// When it should next be reviewed. The reminder run warns
        /// before this passes, so policies do not quietly go stale.
        /// </summary>
        public DateTime? ReviewDate { get; set; }

        /// <summary>
        /// Whether staff have to confirm they have read it. Not every
        /// policy warrants it, and asking for everything trains people
        /// to click through without reading.
        /// </summary>
        public bool RequiresAcknowledgement { get; set; } = true;

        public string FileName { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        /// <summary>The version this one replaced, where it replaced one.</summary>
        public int? SupersedesPolicyId { get; set; }

        public DateTime? PublishedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? CreatedByName { get; set; }

        public Policy? Supersedes { get; set; }

        public ICollection<PolicyAcknowledgement> Acknowledgements { get; set; }
            = new List<PolicyAcknowledgement>();
    }
}

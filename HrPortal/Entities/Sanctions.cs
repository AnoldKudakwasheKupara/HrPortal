using HrPortal.Services.Sanctions;

namespace HrPortal.Entities
{
    public enum SanctionsReviewStatus
    {
        /// <summary>Read, not yet applied to the register.</summary>
        Pending,

        /// <summary>Applied and screened; matters may be open.</summary>
        InReview,

        /// <summary>Every matter resolved and the review signed off.</summary>
        SignedOff
    }

    /// <summary>
    /// One document received from the FIU, kept exactly as it came
    /// with a SHA-256 of the file, so any report can be tied to the
    /// very document it was based on.
    /// </summary>
    public class SanctionsList
    {
        public int Id { get; set; }

        /// <summary>The portal's own reference for the review, SAN-2026-0001.</summary>
        public string ReviewNumber { get; set; } = string.Empty;

        public string? Title { get; set; }

        /// <summary>The document's own reference, such as SC/16494.</summary>
        public string? DocumentReference { get; set; }

        public DateTime? IssuedOn { get; set; }

        public SanctionsListKind Kind { get; set; }

        public SanctionsReviewStatus Status { get; set; }

        public string OriginalFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string Sha256 { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; }

        public string? UploadedByName { get; set; }

        public DateTime? AppliedAt { get; set; }

        public string? AppliedByName { get; set; }

        /// <summary>How many people and companies were screened, by source.</summary>
        public int ScreenedStaff { get; set; }

        public int ScreenedVendors { get; set; }

        public int ScreenedClients { get; set; }

        public int? ClientListId { get; set; }

        public int RegisterSizeAfter { get; set; }

        public DateTime? SignedOffAt { get; set; }

        public string? SignedOffByName { get; set; }

        public string? SignOffNote { get; set; }

        public string? ReportPath { get; set; }

        public string? ReportSha256 { get; set; }

        public ICollection<SanctionsListEntry> Entries { get; set; } = new List<SanctionsListEntry>();

        public ICollection<SanctionsChange> Changes { get; set; } = new List<SanctionsChange>();

        public ICollection<SanctionsConcern> Concerns { get; set; } = new List<SanctionsConcern>();

        public SanctionsClientList? ClientList { get; set; }
    }

    /// <summary>What one entry looked like in one document.</summary>
    public class SanctionsListEntry
    {
        public int Id { get; set; }

        public int SanctionsListId { get; set; }

        public string Reference { get; set; } = string.Empty;

        public bool IsEntity { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Aliases { get; set; }

        public string? DateOfBirth { get; set; }

        public string? Nationality { get; set; }

        public string? Identifiers { get; set; }

        public string? Address { get; set; }

        public string? Designation { get; set; }

        public string? Title { get; set; }

        public string? PlaceOfBirth { get; set; }

        public string? ListedOn { get; set; }

        public string? OtherInformation { get; set; }

        public string Details { get; set; } = string.Empty;

        public string Fingerprint { get; set; } = string.Empty;

        /// <summary>
        /// Marked by the compliance officer on a notice that takes
        /// some entries off and changes others.
        /// </summary>
        public bool MarkedRemoved { get; set; }

        public SanctionsList List { get; set; } = null!;
    }

    /// <summary>
    /// Everybody currently sanctioned, as far as the documents
    /// received so far say. Each upload brings it up to date, and
    /// screening is always against the whole of it.
    /// </summary>
    public class SanctionsRegisterEntry
    {
        public int Id { get; set; }

        public string Reference { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public bool IsEntity { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Aliases { get; set; }

        public string? DateOfBirth { get; set; }

        public string? Nationality { get; set; }

        public string? Identifiers { get; set; }

        public string? Address { get; set; }

        public string? Designation { get; set; }

        public string? Title { get; set; }

        public string? PlaceOfBirth { get; set; }

        public string? ListedOn { get; set; }

        public string? OtherInformation { get; set; }

        public string Details { get; set; } = string.Empty;

        public string Fingerprint { get; set; } = string.Empty;

        public int FirstListId { get; set; }

        public int LastListId { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public enum SanctionsChangeType
    {
        Added,
        Amended,
        Removed
    }

    /// <summary>What an upload changed in the register.</summary>
    public class SanctionsChange
    {
        public int Id { get; set; }

        public int SanctionsListId { get; set; }

        public SanctionsChangeType Type { get; set; }

        public string Reference { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>For an amendment: field by field, "Field: before -> after", one per line.</summary>
        public string? Differences { get; set; }

        public SanctionsList List { get; set; } = null!;
    }

    public enum SanctionsConcernCategory
    {
        /// <summary>Somebody the company deals with resembles a listed entry.</summary>
        PossibleMatch,

        /// <summary>Something in the document does not add up.</summary>
        Discrepancy
    }

    public enum SanctionsConcernStatus
    {
        Open,

        /// <summary>Looked into and found not to be the listed party.</summary>
        Cleared,

        /// <summary>Confirmed, and escalated.</summary>
        Confirmed,

        /// <summary>A discrepancy that has been dealt with.</summary>
        Resolved
    }

    /// <summary>
    /// A matter the compliance officer must look into before the
    /// review can be signed off.
    /// </summary>
    public class SanctionsConcern
    {
        public int Id { get; set; }

        public int SanctionsListId { get; set; }

        public SanctionsConcernCategory Category { get; set; }

        public SanctionsConcernStatus Status { get; set; }

        public string? Reference { get; set; }

        public string? ListedName { get; set; }

        /// <summary>Staff, Vendor or Client.</summary>
        public string? SubjectType { get; set; }

        public int? SubjectId { get; set; }

        public string? SubjectName { get; set; }

        /// <summary>Why it was raised, in words.</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>0 to 100: how close the match is.</summary>
        public int Score { get; set; }

        public DateTime RaisedAt { get; set; }

        public string? Resolution { get; set; }

        public DateTime? ResolvedAt { get; set; }

        public string? ResolvedByName { get; set; }

        public SanctionsList List { get; set; } = null!;
    }

    /// <summary>
    /// The company's clients and counterparties, as uploaded by the
    /// compliance officer. The latest one is screened.
    /// </summary>
    public class SanctionsClientList
    {
        public int Id { get; set; }

        public string OriginalFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string Sha256 { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; }

        public string? UploadedByName { get; set; }

        public ICollection<SanctionsClient> Clients { get; set; } = new List<SanctionsClient>();
    }

    public class SanctionsClient
    {
        public int Id { get; set; }

        public int SanctionsClientListId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? IdNumber { get; set; }

        public string? ClientReference { get; set; }

        public SanctionsClientList ClientList { get; set; } = null!;
    }
}

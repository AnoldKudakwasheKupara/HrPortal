namespace HrPortal.Entities
{
    /// <summary>What a sanctions document is: the whole list, or a notice of changes to it.</summary>
    public enum SanctionsDocumentKind
    {
        FullList,

        /// <summary>A notice that adds names.</summary>
        Additions,

        /// <summary>A notice that amends existing entries.</summary>
        Amendments,

        /// <summary>A notice that removes names.</summary>
        Removals,

        /// <summary>A notice of more than one kind of change.</summary>
        Notice
    }

    /// <summary>
    /// One document received from the FIU, kept exactly as it came
    /// with a SHA-256 of the file, and every entry read from it.
    /// </summary>
    public class SanctionsDocument
    {
        public int Id { get; set; }

        public string? Title { get; set; }

        public SanctionsDocumentKind Kind { get; set; }

        /// <summary>Which list it belongs to, such as the 1988 Sanctions List.</summary>
        public string? ListName { get; set; }

        /// <summary>Who issued it, as far as the document says.</summary>
        public string? Source { get; set; }

        public int PageCount { get; set; }

        public long FileSize { get; set; }

        /// <summary>The document's own reference, such as SC/16494.</summary>
        public string? DocumentReference { get; set; }

        public DateTime? IssuedOn { get; set; }

        public string OriginalFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string Sha256 { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; }

        public string? UploadedByName { get; set; }

        /// <summary>The title announces additions, amendments or removals rather than the whole list.</summary>
        public bool LooksLikeNotice { get; set; }

        /// <summary>Anything the reader could not make sense of, one per line.</summary>
        public string? ReadingWarnings { get; set; }

        public ICollection<SanctionsEntry> Entries { get; set; } = new List<SanctionsEntry>();

        /// <summary>How the document is named on screen and in reports.</summary>
        public string Label =>
            (DocumentReference ?? OriginalFileName) +
            (IssuedOn is DateTime d ? $" of {d:d MMM yyyy}" : $", uploaded {UploadedAt.ToLocalTime():d MMM yyyy}");
    }

    /// <summary>One listed party as one document gives it.</summary>
    public class SanctionsEntry
    {
        public int Id { get; set; }

        public int SanctionsDocumentId { get; set; }

        public string Reference { get; set; } = string.Empty;

        public bool IsEntity { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? OriginalScriptName { get; set; }

        public string? Aliases { get; set; }

        public string? Title { get; set; }

        public string? Designation { get; set; }

        public string? DateOfBirth { get; set; }

        public string? PlaceOfBirth { get; set; }

        public string? Nationality { get; set; }

        /// <summary>Passport and identity numbers together, one per line, for screening.</summary>
        public string? Identifiers { get; set; }

        public string? Passports { get; set; }

        public string? NationalIds { get; set; }

        public string? Address { get; set; }

        public string? ListedOn { get; set; }

        public string? OtherInformation { get; set; }

        /// <summary>The page of the document the entry starts on.</summary>
        public int? Page { get; set; }

        /// <summary>What the document underlines in the entry, one passage per line.</summary>
        public string? AddedText { get; set; }

        /// <summary>What the document strikes through in the entry, one passage per line.</summary>
        public string? RemovedText { get; set; }

        /// <summary>The entry as it now reads, struck-through text left out.</summary>
        public string Details { get; set; } = string.Empty;

        /// <summary>A hash of the compared fields, to tell at a glance whether an entry changed.</summary>
        public string Fingerprint { get; set; } = string.Empty;

        public SanctionsDocument Document { get; set; } = null!;
    }

    /// <summary>
    /// One comparison: a new document against the one before it (or
    /// against a second document uploaded with it), what differs,
    /// anything that did not add up, and who among staff, vendors and
    /// clients resembles anybody on the new document.
    /// </summary>
    public class SanctionsReview
    {
        public int Id { get; set; }

        /// <summary>The portal's own reference, SAN-2026-0001.</summary>
        public string ReviewNumber { get; set; } = string.Empty;

        public int NewDocumentId { get; set; }

        /// <summary>None when the new document is the very first.</summary>
        public int? PreviousDocumentId { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? CreatedByName { get; set; }

        public bool Screened { get; set; }

        public int ScreenedStaff { get; set; }

        public int ScreenedVendors { get; set; }

        public int ScreenedClients { get; set; }

        public int? ClientListId { get; set; }

        public DateTime? FinalisedAt { get; set; }

        public string? FinalisedByName { get; set; }

        public string? Note { get; set; }

        public string? ReportPath { get; set; }

        public string? ReportSha256 { get; set; }

        public SanctionsDocument NewDocument { get; set; } = null!;

        public SanctionsDocument? PreviousDocument { get; set; }

        public SanctionsClientList? ClientList { get; set; }

        public ICollection<SanctionsChange> Changes { get; set; } = new List<SanctionsChange>();

        public ICollection<SanctionsConcern> Concerns { get; set; } = new List<SanctionsConcern>();
    }

    public enum SanctionsChangeType
    {
        Added,
        Changed,
        Removed
    }

    /// <summary>One difference between the two documents.</summary>
    public class SanctionsChange
    {
        public int Id { get; set; }

        public int SanctionsReviewId { get; set; }

        public SanctionsChangeType Type { get; set; }

        public string Reference { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>For a change: field by field, "Field: before → after", one per line.</summary>
        public string? Differences { get; set; }

        public SanctionsReview Review { get; set; } = null!;
    }

    public enum SanctionsConcernCategory
    {
        /// <summary>Somebody the company deals with resembles a listed party.</summary>
        PossibleMatch,

        /// <summary>Something in the documents does not add up.</summary>
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

    /// <summary>A matter to look into before the review is finalised.</summary>
    public class SanctionsConcern
    {
        public int Id { get; set; }

        public int SanctionsReviewId { get; set; }

        public SanctionsConcernCategory Category { get; set; }

        public SanctionsConcernStatus Status { get; set; }

        public string? Reference { get; set; }

        public string? ListedName { get; set; }

        /// <summary>The listed entry's fingerprint when the matter was raised.</summary>
        public string? EntryFingerprint { get; set; }

        /// <summary>Staff, Vendor, Vendor contact or Client.</summary>
        public string? SubjectType { get; set; }

        public int? SubjectId { get; set; }

        public string? SubjectName { get; set; }

        public string Reason { get; set; } = string.Empty;

        /// <summary>0 to 100: how close the match is.</summary>
        public int Score { get; set; }

        public DateTime RaisedAt { get; set; }

        public string? Resolution { get; set; }

        public DateTime? ResolvedAt { get; set; }

        public string? ResolvedByName { get; set; }

        public SanctionsReview Review { get; set; } = null!;
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

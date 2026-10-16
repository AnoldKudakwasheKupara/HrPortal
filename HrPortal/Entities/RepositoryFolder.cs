namespace HrPortal.Entities
{
    /// <summary>
    /// A shelf in the document repository: forms, templates, handbooks,
    /// procedures. Organisation-wide documents, as opposed to the ones
    /// on a person's file.
    /// </summary>
    public class RepositoryFolder
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DocumentAudience Audience { get; set; }
            = DocumentAudience.Everyone;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public ICollection<RepositoryDocument> Documents { get; set; }
            = new List<RepositoryDocument>();
    }
}

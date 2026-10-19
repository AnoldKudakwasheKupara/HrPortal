namespace HrPortal.Entities
{
    public class RepositoryDocument
    {
        public int Id { get; set; }

        public int RepositoryFolderId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public DateTime UploadedAt { get; set; }

        public string? UploadedByName { get; set; }

        public RepositoryFolder Folder { get; set; } = null!;
    }
}

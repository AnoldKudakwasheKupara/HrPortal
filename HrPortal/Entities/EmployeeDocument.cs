using HrPortal.Security;

namespace HrPortal.Entities
{
    public class EmployeeDocument : IEmployeeOwned
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public int DocumentCategoryId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime UploadedAt { get; set; }

        /// <summary>
        /// When the document stops being valid, for the ones
        /// that do: a contract, a work permit, a certificate.
        /// Null for anything that does not expire.
        /// </summary>
        public DateTime? ExpiryDate { get; set; }

        public int? UploadedByEmployeeId { get; set; }

        public string? UploadedByName { get; set; }

        public Employee Employee { get; set; } = null!;

        public DocumentCategory DocumentCategory { get; set; } = null!;

        public Employee? UploadedByEmployee { get; set; }
    }
}

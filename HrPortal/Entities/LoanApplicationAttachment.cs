namespace HrPortal.Entities
{
    /// <summary>
    /// A receipt or quotation supporting a loan application, which the
    /// paper form asks the applicant to attach.
    /// </summary>
    public class LoanApplicationAttachment
    {
        public int Id { get; set; }

        public int LoanApplicationId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public long SizeInBytes { get; set; }

        public DateTime UploadedAt { get; set; }

        public int? UploadedByEmployeeId { get; set; }

        public LoanApplication LoanApplication { get; set; } = null!;

        public Employee? UploadedByEmployee { get; set; }
    }
}

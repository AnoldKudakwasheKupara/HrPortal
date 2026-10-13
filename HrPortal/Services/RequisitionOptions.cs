namespace HrPortal.Services
{
    /// <summary>
    /// The spending rules for requisitions, kept in configuration so
    /// the threshold can be moved without a code change.
    /// </summary>
    public class RequisitionOptions
    {
        public const string Section = "Requisitions";

        /// <summary>
        /// At or above this estimated total, a requisition goes on to
        /// the Managing Director after review. Below it, review is the
        /// final approval.
        /// </summary>
        public decimal MdApprovalThreshold { get; set; } = 500m;

        public string CurrencyCode { get; set; } = "USD";
    }
}

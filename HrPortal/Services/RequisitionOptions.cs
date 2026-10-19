namespace HrPortal.Services
{
    public class RequisitionOptions
    {
        public const string Section = "Requisitions";

        public decimal MdApprovalThreshold { get; set; } = 500m;

        public string CurrencyCode { get; set; } = "USD";
    }
}

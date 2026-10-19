namespace HrPortal.Entities
{
    public class RequisitionLine
    {
        public int Id { get; set; }

        public int RequisitionId { get; set; }

        public string Description { get; set; } = string.Empty;

        public decimal Quantity { get; set; } = 1;

        public string? UnitOfMeasure { get; set; }

        public decimal? EstimatedUnitCost { get; set; }

        public string? Notes { get; set; }

        public Requisition Requisition { get; set; } = null!;

        public decimal LineTotal =>
            Quantity * (EstimatedUnitCost ?? 0m);
    }
}

using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class AssetMaintenanceEditViewModel
        : AssetMaintenanceCreateViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Status")]
        public MaintenanceStatus Status { get; set; }

        [Display(Name = "Completed on")]
        [DataType(DataType.Date)]
        public DateTime? CompletedOn { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class LicenseEditViewModel : LicenseCreateViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Status")]
        public LicenseStatus Status { get; set; }
    }
}

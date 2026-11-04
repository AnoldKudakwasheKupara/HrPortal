using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class VaultEntryViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        [Display(Name = "Service")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Username")]
        public string? Username { get; set; }

        /// <summary>
        /// Required when adding. When editing, left blank keeps the
        /// password that is stored.
        /// </summary>
        [StringLength(500)]
        [DataType(DataType.Password)]
        public string? Password { get; set; }
    }
}

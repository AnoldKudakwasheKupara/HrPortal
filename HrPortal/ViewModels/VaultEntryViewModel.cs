using System.ComponentModel.DataAnnotations;

namespace HrPortal.ViewModels
{
    public class VaultEntryViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        [Display(Name = "What it is for")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Category { get; set; }

        [StringLength(200)]
        [Display(Name = "Username")]
        public string? Username { get; set; }

        [StringLength(500)]
        [Display(Name = "Web address")]
        public string? Url { get; set; }

        /// <summary>
        /// Required when adding. When editing, left blank keeps the
        /// password that is stored.
        /// </summary>
        [StringLength(500)]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [StringLength(4000)]
        public string? Notes { get; set; }
    }
}

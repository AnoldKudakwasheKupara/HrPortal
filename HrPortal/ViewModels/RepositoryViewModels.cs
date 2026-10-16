using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class RepositoryFolderViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DocumentAudience Audience { get; set; }

        public int DocumentCount { get; set; }
    }

    public class RepositoryUploadViewModel
    {
        [Required]
        [Display(Name = "Folder")]
        public int RepositoryFolderId { get; set; }

        [Required]
        [Display(Name = "Title")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [StringLength(1000)]
        public string? Description { get; set; }

        [Display(Name = "File")]
        public IFormFile? File { get; set; }

        [Display(Name = "Out of date after")]
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }
    }

    public class RepositoryFolderEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Folder Name")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Who can see it")]
        public DocumentAudience Audience { get; set; }

        [Display(Name = "In use")]
        public bool IsActive { get; set; } = true;
    }
}

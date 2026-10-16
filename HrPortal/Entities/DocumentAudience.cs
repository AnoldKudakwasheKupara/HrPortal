using System.ComponentModel.DataAnnotations;

namespace HrPortal.Entities
{
    /// <summary>
    /// Who may see what is in a folder.
    ///
    /// Held on the folder rather than the file, so a document cannot be
    /// filed somewhere more open than whoever uploaded it intended. To
    /// change who sees something you move it, which is a visible act.
    /// </summary>
    public enum DocumentAudience
    {
        [Display(Name = "Everyone")]
        Everyone,

        [Display(Name = "Managers only")]
        Managers,

        [Display(Name = "HR and administrators only")]
        HrAndAdministrators
    }
}

using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class MyCourseApplicationViewModel
    {
        public int Id { get; set; }

        public string? ApplicationNumber { get; set; }

        [Required(ErrorMessage = "Say which programme you intend to take.")]
        [StringLength(200)]
        [Display(Name = "Programme intended")]
        public string ProgramIntended { get; set; } = string.Empty;

        [Required(ErrorMessage =
            "Explain why the programme is relevant to your work.")]
        [StringLength(2000)]
        [Display(Name = "Justification and relevance")]
        public string Justification { get; set; } = string.Empty;

        [Display(Name = "Have you sat for this programme before?")]
        public bool HasSatBefore { get; set; }

        [Display(Name = "If you have")]
        public CourseSitting Sitting { get; set; }
            = CourseSitting.FirstAttempt;

        [DataType(DataType.Date)]
        [Display(Name = "Target date to sit the exam")]
        public DateTime? TargetExamDate { get; set; }

        [Display(Name =
            "I confirm that I will dedicate the requisite hours and " +
            "effort towards the selected studies, and will abide by " +
            "the Intellego Educational Policy")]
        public bool Pledged { get; set; }
    }
}

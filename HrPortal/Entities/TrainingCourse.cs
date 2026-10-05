namespace HrPortal.Entities
{
    public class TrainingCourse
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime? TrainingDate { get; set; }

        public string? Trainer { get; set; }

        public int CreatedByEmployeeId { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsActive { get; set; } = true;

        public Employee CreatedByEmployee { get; set; } = null!;

        public ICollection<TrainingMaterial> Materials { get; set; }
            = new List<TrainingMaterial>();

        public ICollection<TrainingAssignment> Assignments { get; set; }
            = new List<TrainingAssignment>();
    }
}

namespace HrPortal.Entities
{
    public class TrainingCourse
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime? TrainingDate { get; set; }

        /// <summary>
        /// An outside provider, by name. Used where the course is
        /// not delivered by somebody on the staff list.
        /// </summary>
        public string? Trainer { get; set; }

        /// <summary>
        /// The member of staff delivering it, where one is.
        /// </summary>
        public int? TrainerEmployeeId { get; set; }

        public int CreatedByEmployeeId { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsActive { get; set; } = true;

        public Employee CreatedByEmployee { get; set; } = null!;

        public Employee? TrainerEmployee { get; set; }

        /// <summary>
        /// Who is delivering it, named: the member of staff where
        /// one is set, the outside provider otherwise.
        ///
        /// Reads TrainerEmployee, so a query that shows this has to
        /// have included it. A course with a staff trainer and the
        /// navigation left out would otherwise read as having no
        /// trainer at all.
        /// </summary>
        public string? TrainerName =>
            TrainerEmployee == null
                ? Trainer
                : $"{TrainerEmployee.FirstName} " +
                  $"{TrainerEmployee.LastName}";

        public ICollection<TrainingMaterial> Materials { get; set; }
            = new List<TrainingMaterial>();

        public ICollection<TrainingAssignment> Assignments { get; set; }
            = new List<TrainingAssignment>();
    }
}

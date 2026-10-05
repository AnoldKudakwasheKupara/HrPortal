namespace HrPortal.Entities
{
    public class TrainingFeedback
    {
        public int Id { get; set; }

        public int TrainingAssignmentId { get; set; }

        public int Rating { get; set; }

        public string? Comments { get; set; }

        public DateTime SubmittedAt { get; set; }

        public TrainingAssignment TrainingAssignment { get; set; } = null!;
    }
}

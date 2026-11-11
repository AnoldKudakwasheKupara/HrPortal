using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class MyTrainingIndexViewModel
    {
        public const string All = "all";
        public const string Outstanding = "outstanding";
        public const string Completed = "completed";
        public const string FeedbackDue = "feedback";

        public List<TrainingAssignment> Assignments { get; set; } = new();

        public string Show { get; set; } = All;

        public PagedResult<TrainingAssignment> Page { get; set; } = new();

        public List<ListTab> Tabs { get; set; } = new();

        public int OutstandingCount { get; set; }

        public int Overdue { get; set; }

        public int CompletedCount { get; set; }

        public int FeedbackDueCount { get; set; }

        public static bool IsOverdue(TrainingAssignment a, DateTime today) =>
            !a.IsCompleted && a.DueDate != null && a.DueDate.Value.Date < today.Date;

        public static bool In(string show, TrainingAssignment a) => show switch
        {
            Outstanding => !a.IsCompleted,
            Completed => a.IsCompleted,
            FeedbackDue => a.IsCompleted && a.Feedback == null,
            _ => true
        };

        public static MyTrainingIndexViewModel Build(
            List<TrainingAssignment> assignments, string? show, int page, DateTime today)
        {
            var group = show is Outstanding or Completed or FeedbackDue ? show : All;
            int Count(string g) => assignments.Count(a => In(g, a));

            return new MyTrainingIndexViewModel
            {
                Assignments = assignments,
                Show = group,
                Page = PagedList.Of(assignments.Where(a => In(group, a)).ToList(), page),
                Tabs = new List<ListTab>
                {
                    new(All, "All Training", "fa-list-ul", "is-teal", assignments.Count),
                    new(Outstanding, "Outstanding", "fa-hourglass-half", "is-blue", Count(Outstanding)),
                    new(Completed, "Completed", "fa-circle-check", "is-green", Count(Completed)),
                    new(FeedbackDue, "Feedback Due", "fa-comment-dots", "is-amber", Count(FeedbackDue))
                },
                OutstandingCount = Count(Outstanding),
                Overdue = assignments.Count(a => IsOverdue(a, today)),
                CompletedCount = Count(Completed),
                FeedbackDueCount = Count(FeedbackDue)
            };
        }
    }
}

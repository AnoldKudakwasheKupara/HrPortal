using HrPortal.Entities;
using HrPortal.Services;

namespace HrPortal.ViewModels
{
    public class MyCoursesIndexViewModel
    {
        public List<CourseApplication> Applications { get; set; } = new();

        public string Show { get; set; } = RequestGroups.All;

        public PagedResult<CourseApplication> Page { get; set; } = new();

        public List<ListTab> Tabs { get; set; } = new();

        public int InProgress { get; set; }

        public int Approved { get; set; }

        /// <summary>The soonest exam still ahead on an approved or open application.</summary>
        public CourseApplication? NextExam { get; set; }

        public static MyCoursesIndexViewModel Build(
            List<CourseApplication> applications, string? show, int page, DateTime today)
        {
            static bool Final(CourseApplication c) => CourseApplicationWorkflowService.IsFinal(c.Status);
            static bool IsApproved(CourseApplication c) => c.Status == CourseApplicationStatus.Approved;

            var group = RequestGroups.Normalise(show);

            return new MyCoursesIndexViewModel
            {
                Applications = applications,
                Show = group,
                Page = RequestGroups.Page(applications, Final, IsApproved, group, page),
                Tabs = RequestGroups.Tabs(applications, Final, IsApproved, "All Applications"),
                InProgress = applications.Count(c => !Final(c)),
                Approved = applications.Count(IsApproved),
                NextExam = applications
                    .Where(c => (IsApproved(c) || !Final(c)) && c.TargetExamDate?.Date >= today.Date)
                    .OrderBy(c => c.TargetExamDate)
                    .FirstOrDefault()
            };
        }
    }
}

using System.Text.RegularExpressions;
using HrPortal.Entities;

namespace HrPortal.Services
{
    /// <summary>One step in a request's history, ready to draw.</summary>
    public sealed record TimelineEntry(
        string Text, string Who, DateTime When, string? Comment, string Tone, string Icon);

    /// <summary>
    /// Turns the history of a leave, loan or course request into the same
    /// timeline everywhere, so every request reads the same way.
    /// </summary>
    public static class Timeline
    {
        private static readonly Dictionary<string, string> Named = new()
        {
            ["CapturedAsApproved"] = "Captured as approved",
            ["CaptureCorrected"] = "Details corrected",
            ["Commented"] = "Comment",
            ["ApprovedByManagingConsultant"] = "Approved by the Managing Consultant",
            ["RejectedByManagingConsultant"] = "Rejected by the Managing Consultant"
        };

        /// <summary>"ApprovedByHR" reads "Approved by HR", "ReturnedBySupervisor" "Returned by the supervisor".</summary>
        public static string Describe(string action)
        {
            if (Named.TryGetValue(action, out var named))
                return named;

            var words = Regex.Split(action, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")
                .Select((w, i) => w switch
                {
                    "HR" or "Hr" => "HR",
                    "MD" or "Md" => "MD",
                    _ => i == 0 ? w : w.ToLowerInvariant()
                })
                .ToList();

            var text = string.Join(' ', words);

            return text.Replace(" by supervisor", " by the supervisor");
        }

        public static (string Tone, string Icon) Look(string action) => action switch
        {
            _ when action.StartsWith("Approved") || action == "CapturedAsApproved" => ("is-green", "fa-check"),
            _ when action.StartsWith("Rejected") => ("is-red", "fa-xmark"),
            _ when action.StartsWith("Returned") => ("is-amber", "fa-rotate-left"),
            _ when action == "Cancelled" => ("is-gray", "fa-ban"),
            _ when action == "Commented" => ("is-teal", "fa-comment"),
            _ when action == "CaptureCorrected" => ("is-blue", "fa-pen"),
            _ => ("is-blue", "fa-paper-plane")
        };

        public static List<TimelineEntry> Of<T>(
            IEnumerable<T> history,
            Func<T, string> action,
            Func<T, Employee?> who,
            Func<T, DateTime> when,
            Func<T, string?> comment) =>
            history
                .OrderByDescending(when)
                .Select(h =>
                {
                    var a = action(h);
                    var (tone, icon) = Look(a);
                    var person = who(h);

                    return new TimelineEntry(
                        Describe(a),
                        person == null ? "--" : $"{person.FirstName} {person.LastName}",
                        when(h),
                        string.IsNullOrWhiteSpace(comment(h)) ? null : comment(h),
                        tone,
                        icon);
                })
                .ToList();

        public static List<TimelineEntry> Of(IEnumerable<CourseApplicationHistory> history) =>
            Of(history, h => h.Action.ToString(), h => h.ActionedByEmployee, h => h.ActionDate, h => h.Comment);

        public static List<TimelineEntry> Of(IEnumerable<LoanApplicationHistory> history) =>
            Of(history, h => h.Action.ToString(), h => h.ActionedByEmployee, h => h.ActionDate, h => h.Comment);

        public static List<TimelineEntry> Of(IEnumerable<LeaveRequestHistory> history) =>
            Of(history, h => h.Action.ToString(), h => h.ActionedByEmployee, h => h.ActionDate, h => h.Comment);
    }
}

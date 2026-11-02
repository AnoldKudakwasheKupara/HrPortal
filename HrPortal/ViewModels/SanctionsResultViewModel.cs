using HrPortal.Entities;
using HrPortal.Services.Sanctions;

namespace HrPortal.ViewModels
{
    /// <summary>
    /// A review as the compliance officer works through it: what still
    /// needs a decision first, then what has been dealt with, then what
    /// the portal set aside by itself.
    /// </summary>
    public class SanctionsResultViewModel
    {
        public SanctionsReview Review { get; set; } = null!;

        /// <summary>The listed entries the matters refer to, by reference.</summary>
        public Dictionary<string, SanctionsEntry> Entries { get; set; } = new();

        /// <summary>What the portal holds about each person or company checked, keyed "Type|Id".</summary>
        public Dictionary<string, List<(string Label, string Value)>> Subjects { get; set; } = new();

        public List<string> FinaliseBlockers { get; set; } = new();

        public static string SubjectKey(string? type, int? id) => $"{type}|{id}";

        public List<SanctionsConcern> Outstanding => Review.Concerns
            .Where(SanctionsService.IsOutstanding)
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Strength)
            .ThenByDescending(c => c.Score)
            .ToList();

        public List<SanctionsConcern> DealtWith => Review.Concerns
            .Where(c => !SanctionsService.IsOutstanding(c) && c.ResolvedByName != SanctionsService.SetAsideBy)
            .OrderBy(c => c.Category)
            .ThenByDescending(c => c.ResolvedAt)
            .ToList();

        public List<SanctionsConcern> SetAside => Review.Concerns
            .Where(c => c.ResolvedByName == SanctionsService.SetAsideBy && c.Status == SanctionsConcernStatus.Cleared)
            .OrderByDescending(c => c.Score)
            .ToList();
    }

    /// <summary>One matter as shown on the results page.</summary>
    public sealed record SanctionsMatterView(
        SanctionsConcern Concern,
        SanctionsEntry? Entry,
        List<(string Label, string Value)>? Subject,
        bool CanEdit,
        int ReviewId,
        int DocumentId);
}

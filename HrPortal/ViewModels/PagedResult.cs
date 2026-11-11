using Microsoft.EntityFrameworkCore;

namespace HrPortal.ViewModels
{
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

        public int Page { get; init; } = 1;

        public int PageSize { get; init; }

        public int TotalCount { get; init; }

        public int TotalPages { get; init; }

        public bool HasPrevious => Page > 1;

        public bool HasNext => Page < TotalPages;

        public bool IsPaged => TotalPages > 1;
    }

    /// <summary>One tab in a row of filter pills, with how many it holds.</summary>
    public sealed record ListTab(string Key, string Text, string Icon, string Tint, int Count);

    /// <summary>A row of filter pills; the chosen key goes in the query string as <see cref="Param"/>.</summary>
    public sealed record ListTabsModel(IReadOnlyList<ListTab> Tabs, string Current, string Param = "show", string AllKey = "all");

    /// <summary>Filter pills and a search box above a list; either may be left out.</summary>
    public sealed record ListToolbarModel(ListTabsModel? Tabs, string? Search, string Placeholder = "Search...");

    /// <summary>What the numbered pager needs, whatever the list holds.</summary>
    public sealed record PagerSummary(int Page, int PageSize, int TotalCount, int TotalPages, string Noun)
    {
        public static PagerSummary From<T>(PagedResult<T> paged, string noun) =>
            new(paged.Page, paged.PageSize, paged.TotalCount, paged.TotalPages, noun);

        public static PagerSummary From<T>(PagedResult<T> paged, string one, string many) =>
            From(paged, paged.TotalCount == 1 ? one : many);
    }

    public static class PagedList
    {
        public const int DefaultPageSize = 10;

        /// <summary>The page asked for in the query string, or the first.</summary>
        public static int PageFrom(Microsoft.AspNetCore.Http.HttpRequest request) =>
            int.TryParse(request.Query["page"], out var page) ? page : 1;

        /// <summary>The trimmed search text in the query string, or null.</summary>
        public static string? SearchFrom(Microsoft.AspNetCore.Http.HttpRequest request) =>
            string.IsNullOrWhiteSpace(request.Query["q"]) ? null : request.Query["q"].ToString().Trim();

        /// <summary>True when any of the values contains the search text, ignoring case.</summary>
        public static bool Matches(string? search, params string?[] values) =>
            search == null || values.Any(v => v != null && v.Contains(search, StringComparison.OrdinalIgnoreCase));

        /// <summary>A page of a list already in memory. Out of range pages show the nearest page.</summary>
        public static PagedResult<T> Of<T>(IReadOnlyList<T> items, int page, int pageSize = DefaultPageSize)
        {
            var totalPages = (int)Math.Ceiling(items.Count / (double)pageSize);

            page = Math.Clamp(page, 1, Math.Max(totalPages, 1));

            return new PagedResult<T>
            {
                Items = items.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = items.Count,
                TotalPages = totalPages
            };
        }
    }

    public static class PagedQueryExtensions
    {
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
            this IQueryable<T> query,
            int page,
            int pageSize)
        {
            if (pageSize < 1)
                pageSize = 1;

            var totalCount = await query.CountAsync();

            var totalPages =
                (int)Math.Ceiling(totalCount / (double)pageSize);

            if (page < 1)
                page = 1;

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };
        }
    }
}

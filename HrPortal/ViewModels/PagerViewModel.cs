using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace HrPortal.ViewModels
{
    public class PagerViewModel
    {
        public int Page { get; init; } = 1;

        public int TotalPages { get; init; }

        public int TotalCount { get; init; }

        public string ItemNoun { get; init; } = "items";

        private string Path { get; init; } = string.Empty;

        private Dictionary<string, string?> Query { get; init; } = new();

        public static PagerViewModel From<T>(
            HttpRequest request,
            PagedResult<T> paged,
            string itemNoun)
        {
            var query = new Dictionary<string, string?>();

            foreach (var pair in request.Query)
            {
                if (string.Equals(pair.Key, "page",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                query[pair.Key] = pair.Value.ToString();
            }

            return new PagerViewModel
            {
                Page = paged.Page,
                TotalPages = paged.TotalPages,
                TotalCount = paged.TotalCount,
                ItemNoun = itemNoun,
                Path = request.Path,
                Query = query
            };
        }

        public string UrlForPage(int page)
        {
            var query = new Dictionary<string, string?>(Query)
            {
                ["page"] = page.ToString()
            };

            return QueryHelpers.AddQueryString(Path, query);
        }
    }
}

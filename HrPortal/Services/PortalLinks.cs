namespace HrPortal.Services
{
    /// <summary>
    /// Links that have to open on a particular company's portal, such
    /// as a notification about a request that company decides.
    /// </summary>
    public static class PortalLinks
    {
        /// <summary>
        /// Where a company's portal lives: its own address, or the
        /// portal address in the Email settings for the main company.
        /// Null when neither is known.
        /// </summary>
        public static string? BaseFor(string? companyKey, string? mainBase)
        {
            if (string.IsNullOrWhiteSpace(companyKey))
                return null;

            var brand = Brand.ForKey(companyKey);

            if (brand == null)
                return null;

            if (!string.IsNullOrWhiteSpace(brand.PortalUrl))
                return brand.PortalUrl.TrimEnd('/');

            return brand.Key == Brand.Main.Key && !string.IsNullOrWhiteSpace(mainBase)
                ? mainBase.TrimEnd('/')
                : null;
        }

        /// <summary>
        /// The link on the company's own portal when its address is
        /// known; otherwise the link as it was, to open wherever the
        /// reader is.
        /// </summary>
        public static string? For(string? companyKey, string? link, string? mainBase)
        {
            if (string.IsNullOrWhiteSpace(link) || !link.StartsWith('/') || link.StartsWith("//"))
                return link;

            var root = BaseFor(companyKey, mainBase);

            return root == null ? link : root + link;
        }
    }
}

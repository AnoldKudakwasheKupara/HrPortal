namespace HrPortal.Services
{
    /// <summary>
    /// The company artwork the printed forms carry. Read from disk
    /// once; a missing file is not fatal, the forms simply fall back
    /// to the company name in text.
    /// </summary>
    public class BrandAssets
    {
        private readonly Lazy<byte[]?> _logo;

        public BrandAssets(
            IWebHostEnvironment environment,
            ILogger<BrandAssets> logger)
        {
            _logo = new Lazy<byte[]?>(() =>
            {
                var path = Path.Combine(
                    environment.WebRootPath ?? string.Empty,
                    "images",
                    "intellego-logo.png");

                try
                {
                    if (File.Exists(path))
                        return File.ReadAllBytes(path);

                    logger.LogWarning(
                        "No logo was found at {Path}, so printed " +
                        "forms will show the company name as text " +
                        "instead.",
                        path);

                    return null;
                }
                catch (IOException ex)
                {
                    logger.LogWarning(
                        ex,
                        "The logo at {Path} could not be read, so " +
                        "printed forms will show the company name as " +
                        "text instead.",
                        path);

                    return null;
                }
            });
        }

        /// <summary>
        /// The full wordmark, or null when it could not be read.
        /// </summary>
        public byte[]? Logo => _logo.Value;

        public bool HasLogo => Logo != null;
    }
}

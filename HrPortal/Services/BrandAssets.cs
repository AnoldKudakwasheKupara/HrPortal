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

        private readonly Lazy<byte[]?> _markOnDark;

        public BrandAssets(
            IWebHostEnvironment environment,
            ILogger<BrandAssets> logger)
        {
            Lazy<byte[]?> Read(string file) => new(() =>
            {
                var path = Path.Combine(
                    environment.WebRootPath ?? string.Empty,
                    "images",
                    file);

                try
                {
                    if (File.Exists(path))
                        return File.ReadAllBytes(path);

                    logger.LogWarning(
                        "No artwork was found at {Path}, so printed " +
                        "documents will show the company name as " +
                        "text instead.",
                        path);

                    return null;
                }
                catch (IOException ex)
                {
                    logger.LogWarning(
                        ex,
                        "The artwork at {Path} could not be read, so " +
                        "printed documents will show the company " +
                        "name as text instead.",
                        path);

                    return null;
                }
            });

            _logo = Read("intellego-logo.png");
            _markOnDark = Read("intellego-logo-on-dark.png");
        }

        /// <summary>
        /// The full wordmark, or null when it could not be read.
        /// </summary>
        public byte[]? Logo => _logo.Value;

        public bool HasLogo => Logo != null;

        /// <summary>
        /// The same mark recoloured for the green banner, or null
        /// when it could not be read.
        /// </summary>
        public byte[]? MarkOnDark => _markOnDark.Value;

        public bool HasMarkOnDark => MarkOnDark != null;
    }
}

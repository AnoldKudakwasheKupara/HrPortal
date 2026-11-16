using System.Collections.Concurrent;

namespace HrPortal.Services
{
    /// <summary>
    /// The company artwork the printed forms and emails carry, for the
    /// brand being served. Each file is read from disk once per company;
    /// a missing file is not fatal, the forms simply fall back to the
    /// company name in text.
    /// </summary>
    public class BrandAssets
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<BrandAssets> _logger;
        private readonly ConcurrentDictionary<(string Key, string File), byte[]?> _files = new();

        public BrandAssets(
            IWebHostEnvironment environment,
            ILogger<BrandAssets> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        private byte[]? Read(string file) =>
            _files.GetOrAdd((Brand.Current.Key, file), key =>
            {
                var path = Path.Combine(
                    _environment.WebRootPath ?? string.Empty,
                    "brands",
                    key.Key,
                    key.File);

                try
                {
                    if (File.Exists(path))
                        return File.ReadAllBytes(path);

                    _logger.LogWarning(
                        "No artwork was found at {Path}, so printed " +
                        "documents will show the company name as " +
                        "text instead.",
                        path);

                    return null;
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "The artwork at {Path} could not be read, so " +
                        "printed documents will show the company " +
                        "name as text instead.",
                        path);

                    return null;
                }
            });

        /// <summary>
        /// The full wordmark, or null when it could not be read.
        /// </summary>
        public byte[]? Logo => Read("logo.png");

        public bool HasLogo => Logo != null;

        /// <summary>
        /// The same mark recoloured for the dark banner, or null
        /// when it could not be read.
        /// </summary>
        public byte[]? MarkOnDark => Read("logo-on-dark.png");

        public bool HasMarkOnDark => MarkOnDark != null;

        /// <summary>
        /// The mark for the dark banner, sized for an email. Every
        /// message carries its own copy, so it is the small one.
        /// </summary>
        public byte[]? EmailMark => Read("logo-email.png");
    }
}

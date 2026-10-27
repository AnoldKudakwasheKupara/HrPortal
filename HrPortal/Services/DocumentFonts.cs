using QuestPDF.Drawing;

namespace HrPortal.Services
{
    /// <summary>
    /// Hands Montserrat to QuestPDF so a printed form reads as the
    /// same company as the website and the portal. Registered once at
    /// startup; a missing file is not fatal, QuestPDF simply falls
    /// back to the Lato it carries internally.
    /// </summary>
    public static class DocumentFonts
    {
        /// <summary>
        /// The family name the PDF services ask for. Matches the name
        /// inside the font files, which is what QuestPDF resolves on.
        /// </summary>
        public const string Family = "Montserrat";

        private static readonly string[] Weights =
        {
            "Montserrat-Regular.ttf",
            "Montserrat-SemiBold.ttf",
            "Montserrat-Bold.ttf"
        };

        public static void Register(
            IWebHostEnvironment environment,
            ILogger logger)
        {
            var folder = Path.Combine(
                environment.WebRootPath ?? string.Empty,
                "fonts");

            var registered = 0;

            foreach (var weight in Weights)
            {
                var path = Path.Combine(folder, weight);

                try
                {
                    if (!File.Exists(path))
                    {
                        logger.LogWarning(
                            "The font {Path} is missing, so printed " +
                            "documents will fall back to Lato.",
                            path);

                        continue;
                    }

                    using var stream = File.OpenRead(path);
                    FontManager.RegisterFontFromStream(stream);
                    registered++;
                }
                catch (IOException ex)
                {
                    logger.LogWarning(
                        ex,
                        "The font {Path} could not be read, so " +
                        "printed documents will fall back to Lato.",
                        path);
                }
            }

            if (registered == Weights.Length)
                return;

            // Partial registration is worse than none: QuestPDF would
            // substitute for the weights it did not get, and the
            // forms would print in two different typefaces.
            logger.LogWarning(
                "Only {Registered} of {Total} Montserrat weights " +
                "were registered. Printed documents may mix " +
                "typefaces.",
                registered,
                Weights.Length);
        }
    }
}

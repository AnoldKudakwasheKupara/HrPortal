namespace HrPortal.Services
{
    public class FileStorageService
    {
        private readonly IWebHostEnvironment _environment;

        public FileStorageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        /// <summary>
        /// Where uploads live: outside the web root, so static file
        /// serving cannot hand them out. Everything reaches them
        /// through a controller action that checks who is asking.
        /// </summary>
        public string UploadRoot => Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads");

        public static readonly string[] AllowedExtensions =
        {
            ".pdf",
            ".doc",
            ".docx",
            ".xls",
            ".xlsx",
            ".ppt",
            ".pptx",
            ".txt",
            ".csv",
            ".png",
            ".jpg",
            ".jpeg"
        };

        public static readonly string[] PreviewableExtensions =
        {
            ".pdf",
            ".png",
            ".jpg",
            ".jpeg",
            ".txt",
            ".csv"
        };

        public const long MaxFileSizeBytes = 10 * 1024 * 1024;

        public static bool CanPreview(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path
                .GetExtension(fileName)
                .ToLowerInvariant();

            return PreviewableExtensions.Contains(extension);
        }

        public static string? Validate(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return "Select a file to upload.";

            if (file.Length > MaxFileSizeBytes)
                return "The file must be 10 MB or smaller.";

            var extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                return
                    "That file type is not allowed. Permitted types: " +
                    string.Join(", ", AllowedExtensions) + ".";
            }

            return null;
        }

        public async Task<(string FileName, string FilePath)> SaveAsync(
            IFormFile file,
            string folder)
        {
            var extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            var fileName = $"{Guid.NewGuid():N}{extension}";

            var folderPath = Path.Combine(UploadRoot, folder);

            Directory.CreateDirectory(folderPath);

            var fullPath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(
                fullPath,
                FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return (fileName, $"/uploads/{folder}/{fileName}");
        }

        public void Delete(string filePath)
        {
            var fullPath = ResolveExistingPath(filePath);

            if (fullPath != null)
                File.Delete(fullPath);
        }

        public string? ResolveExistingPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            // The stored path is a key of the form
            // /uploads/<folder>/<file>, kept as it was so no existing
            // record has to be rewritten. Only the root it resolves
            // against has moved.
            var relative = filePath
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);

            const string prefix = "uploads";

            if (relative.StartsWith(
                    prefix + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                relative = relative[(prefix.Length + 1)..];
            }

            var fullPath = Path.GetFullPath(
                Path.Combine(UploadRoot, relative));

            // A stored path should never climb out of the upload root,
            // but checking costs nothing and a crafted value would
            // otherwise reach the rest of the disk.
            if (!fullPath.StartsWith(
                    Path.GetFullPath(UploadRoot),
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return File.Exists(fullPath) ? fullPath : null;
        }

        /// <summary>
        /// Moves anything left under wwwroot/uploads into the new root.
        ///
        /// Files used to be written inside the web root, where static
        /// file serving handed them to anyone with the URL and no
        /// sign-in at all. Leaving them there would keep that hole open
        /// for every document uploaded before this change, so they are
        /// moved once at startup and the old folder removed.
        /// </summary>
        public int MoveLegacyUploads(ILogger logger)
        {
            var legacyRoot = Path.Combine(
                _environment.WebRootPath ?? string.Empty,
                "uploads");

            if (string.IsNullOrEmpty(_environment.WebRootPath) ||
                !Directory.Exists(legacyRoot))
            {
                return 0;
            }

            var moved = 0;

            foreach (var source in Directory.EnumerateFiles(
                         legacyRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(legacyRoot, source);
                var destination = Path.Combine(UploadRoot, relative);

                try
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(destination)!);

                    if (File.Exists(destination))
                    {
                        File.Delete(source);
                    }
                    else
                    {
                        File.Move(source, destination);
                        moved++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Could not move {File} out of the web root. It " +
                        "stays publicly reachable until it is moved.",
                        source);
                }
            }

            try
            {
                if (!Directory.EnumerateFileSystemEntries(legacyRoot).Any())
                    Directory.Delete(legacyRoot, true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Moved the uploads but could not remove {Folder}.",
                    legacyRoot);
            }

            if (moved > 0)
            {
                logger.LogWarning(
                    "Moved {Count} uploaded file(s) out of the web root " +
                    "into {Root}. They are no longer served without a " +
                    "sign-in.",
                    moved,
                    UploadRoot);
            }

            return moved;
        }
    }
}

namespace HrPortal.Services
{
    public class FileStorageService
    {
        private readonly IWebHostEnvironment _environment;

        public FileStorageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

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

            if (!LooksLike(file, extension))
            {
                return
                    $"{Path.GetFileName(file.FileName)} does not look like a {extension.TrimStart('.').ToUpperInvariant()} " +
                    "file. Save it again from the program that made it and upload that copy.";
            }

            return null;
        }

        private static readonly byte[] ZipStart = { 0x50, 0x4B, 0x03, 0x04 };

        private static readonly byte[] OldOfficeStart = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

        private static readonly byte[] PngStart = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static readonly byte[] JpegStart = { 0xFF, 0xD8, 0xFF };

        /// <summary>
        /// Whether the first bytes of the file match its extension, so a
        /// program or a page renamed to .pdf or .docx is not kept and
        /// handed back to whoever downloads it.
        /// </summary>
        private static bool LooksLike(IFormFile file, string extension)
        {
            var head = new byte[1024];
            int read;

            using (var stream = file.OpenReadStream())
            {
                read = 0;
                int got;

                while (read < head.Length && (got = stream.Read(head, read, head.Length - read)) > 0)
                    read += got;
            }

            var start = head.AsSpan(0, read);

            return extension switch
            {
                ".pdf" => start.IndexOf("%PDF-"u8) >= 0,
                ".docx" or ".xlsx" or ".pptx" => start.StartsWith(ZipStart),
                ".doc" or ".xls" or ".ppt" => start.StartsWith(OldOfficeStart) || start.StartsWith(ZipStart) || IsText(start),
                ".png" => start.StartsWith(PngStart),
                ".jpg" or ".jpeg" => start.StartsWith(JpegStart),
                ".txt" or ".csv" => IsText(start),
                _ => false
            };
        }

        private static bool IsText(ReadOnlySpan<byte> start)
        {
            if (start.StartsWith(new byte[] { 0xFF, 0xFE }) || start.StartsWith(new byte[] { 0xFE, 0xFF }))
                return true;

            return start.IndexOf((byte)0) < 0 && !start.StartsWith("MZ"u8);
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

            if (!fullPath.StartsWith(
                    Path.GetFullPath(UploadRoot),
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return File.Exists(fullPath) ? fullPath : null;
        }

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

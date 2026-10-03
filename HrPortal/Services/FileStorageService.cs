namespace HrPortal.Services
{
    /// <summary>
    /// Saves uploaded files under wwwroot and removes them again.
    /// Stored names are generated so that two uploads of the same
    /// file never collide, while the name the user chose is kept
    /// separately on the entity for display.
    /// </summary>
    public class FileStorageService
    {
        private readonly IWebHostEnvironment _environment;

        public FileStorageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }


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

        public const long MaxFileSizeBytes = 10 * 1024 * 1024;


        // ============================================================
        // VALIDATE
        // ============================================================

        /// <summary>
        /// Returns an error message when the file is not acceptable,
        /// or null when it is.
        /// </summary>
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


        // ============================================================
        // SAVE
        // ============================================================

        /// <summary>
        /// Writes the file into wwwroot/uploads/{folder} and returns
        /// the stored name along with the web path to reach it.
        /// </summary>
        public async Task<(string FileName, string FilePath)> SaveAsync(
            IFormFile file,
            string folder)
        {
            var extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            var fileName = $"{Guid.NewGuid():N}{extension}";

            var folderPath = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                folder);

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


        // ============================================================
        // DELETE
        // ============================================================

        /// <summary>
        /// Removes a stored file. A missing file is not an error:
        /// the database row is what matters.
        /// </summary>
        public void Delete(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            var relativePath = filePath
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);

            var fullPath = Path.Combine(
                _environment.WebRootPath,
                relativePath);

            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }


        // ============================================================
        // READ
        // ============================================================

        /// <summary>
        /// Resolves a stored path to a full path on disk, or null
        /// when the file is no longer there.
        /// </summary>
        public string? ResolveExistingPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            var relativePath = filePath
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);

            var fullPath = Path.Combine(
                _environment.WebRootPath,
                relativePath);

            return File.Exists(fullPath) ? fullPath : null;
        }
    }
}

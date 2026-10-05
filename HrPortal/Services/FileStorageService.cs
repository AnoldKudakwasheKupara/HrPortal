namespace HrPortal.Services
{
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

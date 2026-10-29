using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using HrPortal.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HrPortal.Services
{
    /// <summary>
    /// A complete copy of the portal that an administrator can take
    /// away: the database, in PostgreSQL's own format so it restores
    /// exactly, and every file staff and HR have uploaded.
    ///
    /// The keys that protect sign-ins are left out. A backup is a
    /// file that leaves the server, and nothing in it should let
    /// whoever holds it pass for a signed-in user.
    /// </summary>
    public class BackupService
    {
        public const string Section = "Backup";

        private readonly ApplicationDbContext _context;
        private readonly FileStorageService _files;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BackupService> _logger;

        public BackupService(
            ApplicationDbContext context,
            FileStorageService files,
            IConfiguration configuration,
            ILogger<BackupService> logger)
        {
            _context = context;
            _files = files;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// pg_dump, from Backup:PgDumpPath when it is set and from the
        /// path otherwise. Null when it cannot be found, which the
        /// backups page says plainly rather than failing at download.
        /// </summary>
        public string? PgDumpPath()
        {
            var configured = _configuration[$"{Section}:PgDumpPath"];

            if (!string.IsNullOrWhiteSpace(configured))
                return File.Exists(configured) ? configured : null;

            var name = OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump";

            return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(dir => Path.Combine(dir, name))
                .FirstOrDefault(File.Exists);
        }

        public static string FileName(DateTime when) =>
            $"My Intellego backup {when:yyyy-MM-dd HHmm}.zip";

        /// <summary>
        /// Builds the backup in a temporary file and returns it open
        /// for reading. The file deletes itself when the stream is
        /// closed, so nothing is left behind on the server once the
        /// download ends.
        /// </summary>
        public async Task<FileStream> CreateAsync(CancellationToken cancellationToken)
        {
            var pgDump = PgDumpPath()
                ?? throw new InvalidOperationException(
                    "pg_dump was not found, so the database cannot be " +
                    "backed up from here.");

            var path = Path.Combine(
                Path.GetTempPath(),
                $"hrportal-backup-{Guid.NewGuid():N}.zip");

            try
            {
                await using (var zipFile = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous))
                using (var zip = new ZipArchive(zipFile, ZipArchiveMode.Create))
                {
                    await DumpDatabaseAsync(pgDump, zip, cancellationToken);

                    var fileCount = await AddUploadsAsync(zip, cancellationToken);

                    await WriteTextAsync(
                        zip,
                        "README.txt",
                        Readme(DateTime.Now, fileCount),
                        cancellationToken);
                }

                return new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            }
            catch
            {
                File.Delete(path);
                throw;
            }
        }

        /// <summary>
        /// Runs pg_dump in custom format straight into the archive.
        /// The connection details go to it through the environment,
        /// so the password never appears on a command line.
        /// </summary>
        private async Task DumpDatabaseAsync(
            string pgDump,
            ZipArchive zip,
            CancellationToken cancellationToken)
        {
            var connection = new NpgsqlConnectionStringBuilder(
                _context.Database.GetConnectionString());

            var start = new ProcessStartInfo(pgDump)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            foreach (var argument in new[]
                     {
                         "--format=custom",
                         "--no-owner",
                         "--no-privileges"
                     })
            {
                start.ArgumentList.Add(argument);
            }

            start.Environment["PGHOST"] = connection.Host;
            start.Environment["PGPORT"] = connection.Port.ToString();
            start.Environment["PGDATABASE"] = connection.Database;
            start.Environment["PGUSER"] = connection.Username;
            start.Environment["PGPASSWORD"] = connection.Password;
            start.Environment["PGSSLMODE"] = connection.SslMode switch
            {
                SslMode.Disable => "disable",
                SslMode.Allow => "allow",
                SslMode.Require => "require",
                SslMode.VerifyCA => "verify-ca",
                SslMode.VerifyFull => "verify-full",
                _ => "prefer"
            };

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("pg_dump did not start.");

            var errors = process.StandardError.ReadToEndAsync(cancellationToken);

            var entry = zip.CreateEntry("database.dump", CompressionLevel.Fastest);

            await using (var target = entry.Open())
            {
                await process.StandardOutput.BaseStream
                    .CopyToAsync(target, cancellationToken);
            }

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var detail = await errors;

                _logger.LogError(
                    "pg_dump stopped with code {Code}: {Detail}",
                    process.ExitCode,
                    detail);

                throw new InvalidOperationException(
                    $"pg_dump stopped with code {process.ExitCode}.");
            }
        }

        private async Task<int> AddUploadsAsync(
            ZipArchive zip,
            CancellationToken cancellationToken)
        {
            var root = _files.UploadRoot;

            if (!Directory.Exists(root))
                return 0;

            var count = 0;

            foreach (var file in Directory.EnumerateFiles(
                         root,
                         "*",
                         SearchOption.AllDirectories))
            {
                var name = "files/" + Path.GetRelativePath(root, file)
                    .Replace(Path.DirectorySeparatorChar, '/');

                var entry = zip.CreateEntry(name, CompressionLevel.Fastest);

                await using var source = File.OpenRead(file);
                await using var target = entry.Open();

                await source.CopyToAsync(target, cancellationToken);

                count++;
            }

            return count;
        }

        private static async Task WriteTextAsync(
            ZipArchive zip,
            string name,
            string text,
            CancellationToken cancellationToken)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Fastest);

            await using var target = entry.Open();

            await target.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
        }

        private static string Readme(DateTime when, int fileCount) =>
            $"""
            My Intellego backup, taken {when:d MMMM yyyy 'at' HH:mm}.

            This holds the personal data of everyone in the portal. Keep it
            somewhere only authorised people can reach, such as a restricted
            company OneDrive or SharePoint folder, and delete stray copies.

            What is in it
              database.dump   the whole database, in PostgreSQL custom format
              files/          the {fileCount} uploaded file(s): documents, policies,
                              training materials and loan attachments

            Restoring
              1. Restore the database into an empty PostgreSQL 18 database:
                   pg_restore --no-owner --no-privileges --dbname "<connection>" database.dump
              2. Copy the contents of files/ into App_Data/uploads/ beside
                 the portal, keeping the folder structure.
              3. Start the portal. Everyone signs in again, because the keys
                 that protect sign-ins are not part of a backup.
            """;
    }
}

using Microsoft.Data.SqlClient;

namespace HrPortal.Services
{
    /// <summary>
    /// Lets settings live in the database while still arriving through
    /// the normal configuration pipeline.
    ///
    /// Everything that reads a setting does so through IOptions against
    /// a configuration section. Rather than rewrite all of that to ask
    /// a service, the database is added as another configuration
    /// source, layered over appsettings.json. A value in the database
    /// wins; anything not set there falls back to the file, so a fresh
    /// clone behaves exactly as before.
    /// </summary>
    public class DatabaseConfigurationSource : IConfigurationSource
    {
        private readonly string _connectionString;

        public DatabaseConfigurationSource(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new DatabaseConfigurationProvider(_connectionString);
        }
    }

    public class DatabaseConfigurationProvider : ConfigurationProvider
    {
        private readonly string _connectionString;

        /// <summary>
        /// The live providers, so saving a setting can tell them to
        /// re-read. Without this a change would need a restart.
        /// </summary>
        private static readonly List<DatabaseConfigurationProvider> Instances
            = new();

        private static readonly object Gate = new();

        public DatabaseConfigurationProvider(string connectionString)
        {
            _connectionString = connectionString;

            lock (Gate)
            {
                Instances.Add(this);
            }
        }

        public override void Load()
        {
            var data = new Dictionary<string, string?>(
                StringComparer.OrdinalIgnoreCase);

            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                // Queried directly rather than through EF: this runs
                // while the application is still being built, before
                // any DbContext has been registered. It also has to
                // survive the table not existing yet, on a first run
                // before migrations have been applied.
                using var command = connection.CreateCommand();

                command.CommandText =
                    @"IF OBJECT_ID('dbo.AppSettings', 'U') IS NOT NULL
                          SELECT [Key], [Value] FROM dbo.AppSettings;";

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var key = reader.GetString(0);

                    data[key] = reader.IsDBNull(1)
                        ? null
                        : reader.GetString(1);
                }
            }
            catch (SqlException)
            {
                // No database yet, or it cannot be reached. The file
                // settings still apply, and startup reports the
                // connection problem on its own.
            }

            Data = data;
        }

        /// <summary>
        /// Re-reads every live provider and tells anything watching
        /// that configuration has changed, so IOptionsSnapshot and
        /// IOptionsMonitor consumers see the new value without a
        /// restart.
        /// </summary>
        public static void ReloadAll()
        {
            List<DatabaseConfigurationProvider> providers;

            lock (Gate)
            {
                providers = Instances.ToList();
            }

            foreach (var provider in providers)
            {
                provider.Load();
                provider.OnReload();
            }
        }
    }

    public static class DatabaseConfigurationExtensions
    {
        public static IConfigurationBuilder AddDatabaseSettings(
            this IConfigurationBuilder builder,
            string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return builder;

            return builder.Add(
                new DatabaseConfigurationSource(connectionString));
        }
    }
}

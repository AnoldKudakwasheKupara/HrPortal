using Npgsql;

namespace HrPortal.Services
{
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

        private static readonly List<DatabaseConfigurationProvider> Instances
            = new();

        private static readonly object Gate = new();

        public static NpgsqlException? LastLoadError { get; private set; }

        public static bool SettingsAreLive => LastLoadError == null;

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
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var exists = connection.CreateCommand();

                exists.CommandText =
                    "SELECT to_regclass('public.\"AppSettings\"') IS NOT NULL";

                if (exists.ExecuteScalar() is true)
                {
                    using var command = connection.CreateCommand();

                    command.CommandText =
                        "SELECT \"Key\", \"Value\" FROM \"AppSettings\"";

                    using var reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        var key = reader.GetString(0);

                        data[key] = reader.IsDBNull(1)
                            ? null
                            : reader.GetString(1);
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                LastLoadError = ex;

                Data = Data.Count > 0 ? Data : data;

                return;
            }

            LastLoadError = null;

            Data = data;
        }

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

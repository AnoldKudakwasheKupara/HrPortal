using Microsoft.Data.SqlClient;

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
            }

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

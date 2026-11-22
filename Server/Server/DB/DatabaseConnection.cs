using System;
using Microsoft.Data.SqlClient;
using Server.Configuration;

namespace Server.DB
{
    public static class DatabaseConnection
    {
        const string ProtectedFile = "ECHODIA_DB_CONNECTION_DPAPI_FILE";
        static readonly Lazy<string> ProtectedConnection = new(() =>
        {
            string connection = MachineProtectedSecret.Read(Environment.GetEnvironmentVariable(ProtectedFile));
            var options = new SqlConnectionStringBuilder(connection);
            if (options.TrustServerCertificate || options.Encrypt == SqlConnectionEncryptOption.Optional)
                throw new InvalidOperationException("The remote SQL connection requires verified TLS.");
            return connection;
        });

        public static string Configured()
        {
            if (BenchmarkDatabase.Connection is { } benchmark)
                return benchmark;
            string connection = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ProtectedFile)))
                return connection;
            if (!string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("Configure exactly one SQL connection source.");
            return ProtectedConnection.Value;
        }
    }
}

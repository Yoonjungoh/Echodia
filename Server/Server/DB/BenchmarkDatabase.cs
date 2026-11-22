using System;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Server.Configuration;

namespace Server.DB;

public static class BenchmarkDatabase
{
    public static string Connection { get; private set; }

    // Enabled only by the separate benchmark entry point before any loader or session runs.
    public static string Activate()
    {
        string run = Environment.GetEnvironmentVariable("ECHODIA_QA_RUN");
        string path = Environment.GetEnvironmentVariable("ECHODIA_DB_CONNECTION_DPAPI_FILE");
        if (run == null || !Regex.IsMatch(run, @"\A[A-Za-z0-9]{1,24}\z") || string.IsNullOrWhiteSpace(path) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")))
            throw new InvalidOperationException("The benchmark requires its own run identifier and protected SQL file.");
        string connection = MachineProtectedSecret.Read(path);
        var options = new SqlConnectionStringBuilder(connection);
        if (!string.Equals(options.InitialCatalog, "EchodiaPerf_" + run, StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(options.AttachDBFilename) || !string.IsNullOrEmpty(options.FailoverPartner))
            throw new InvalidOperationException("The benchmark requires its dedicated fixture database.");
        bool localSharedMemory = string.Equals(options.DataSource, @"lpc:(local)\SQLEXPRESS", StringComparison.OrdinalIgnoreCase);
        if (!localSharedMemory && (options.TrustServerCertificate || options.Encrypt == SqlConnectionEncryptOption.Optional))
            throw new InvalidOperationException("Networked benchmark SQL requires a verified TLS connection.");
        Connection = connection;
        return "EchodiaPerf:" + run + ":loggedIn";
    }
}

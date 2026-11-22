using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Google.Protobuf.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Server.Authentication;
using Server.DB;
using Xunit;

public sealed class PrivateLoginTests
{
    [Fact]
    public void PrivateLoginRequiresHttps()
    {
        using var ca = MakeIssuer();
        using var client = MakeLeaf(ca, "client", LoginTls.ClientAuth);
        Assert.Throws<ArgumentException>(() => new LoginAuthority(new Uri("http://localhost:8443"), client, ca));
    }

    [LocalDbFact]
    public async Task OnlyTheConfiguredGameIdentityCanAuthenticateAccounts()
    {
        string database = "EchodiaLoginCheck_" + Guid.NewGuid().ToString("N");
        string? previous = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        Environment.SetEnvironmentVariable("DB_CONNECTION_STRING",
            $@"Server=(localdb)\MSSQLLocalDB;Database={database};Integrated Security=true;Encrypt=false");
        using var db = new GameDbContext();
        try
        {
            await db.Database.EnsureCreatedAsync();
            using var issuer = MakeIssuer();
            using var server = MakeLeaf(issuer, "localhost", LoginTls.ServerAuth);
            using var game = MakeLeaf(issuer, "test-game", LoginTls.ClientAuth);
            using var other = MakeLeaf(issuer, "other-game", LoginTls.ClientAuth);
            var bind = new IPEndPoint(IPAddress.Loopback, 0);
            await using var host = PrivateLoginHost.Build(WebApplication.CreateSlimBuilder(), bind, server, issuer,
                game.GetCertHash(HashAlgorithmName.SHA256));
            await host.StartAsync();
            try
            {
                var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                var endpoint = new Uri(address);
                using var approved = new LoginAuthority(endpoint, new X509Certificate2(game), new X509Certificate2(issuer));
                using var unapproved = new LoginAuthority(endpoint, new X509Certificate2(other), new X509Certificate2(issuer));
                var request = new C_Login { Id = "canary", Password = Guid.NewGuid().ToString("N") };
                await Assert.ThrowsAsync<HttpRequestException>(() => unapproved.AuthenticateAsync(request));
                Assert.Empty(await db.Accounts.ToListAsync());

                Assert.Equal(new CredentialDecision(LoginStatus.SignUpSuccess, 0), await approved.AuthenticateAsync(request));
                var saved = await db.Accounts.AsNoTracking().SingleAsync();
                Assert.Null(saved.AccountPassword);
                Assert.Equal(48, saved.PasswordHashV1.Length);
                Assert.Equal(new CredentialDecision(LoginStatus.PasswordWrong, 0),
                    await approved.AuthenticateAsync(new C_Login { Id = request.Id, Password = "incorrect" }));
                Assert.Equal(new CredentialDecision(LoginStatus.Success, saved.AccountDbId), await approved.AuthenticateAsync(request));
                Assert.Single(await db.Accounts.ToListAsync());
            }
            finally { await host.StopAsync(); }
        }
        finally
        {
            try { await db.Database.EnsureDeletedAsync(); }
            finally { Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", previous); }
        }
    }

    static X509Certificate2 MakeIssuer()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Echodia fixture authority", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
    }

    static X509Certificate2 MakeLeaf(X509Certificate2 issuer, string name, string role)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=" + name, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(role) }, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(name);
        names.AddIpAddress(IPAddress.Loopback);
        req.CertificateExtensions.Add(names.Build());
        using var cert = req.Create(issuer, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), RandomNumberGenerator.GetBytes(16));
        using var withKey = cert.CopyWithPrivateKey(rsa);
        return X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pfx), null);
    }

    sealed class LocalDbFactAttribute : FactAttribute
    {
        public LocalDbFactAttribute()
        {
            if (!OperatingSystem.IsWindows() || Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions") is not { } installed)
                Skip = "Requires SQL Server LocalDB on Windows.";
            else
                installed.Dispose();
        }
    }
}

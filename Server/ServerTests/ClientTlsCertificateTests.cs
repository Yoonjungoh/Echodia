using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ServerCore;

namespace ServerTests;

public class ClientTlsCertificateTests
{
    private const string PinVariable = "ECHODIA_LOCAL_CERT_SHA256";
    private const string ServerAuth = "1.3.6.1.5.5.7.3.1";
    private const string ClientAuth = "1.3.6.1.5.5.7.3.2";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const bool IsDevelopmentBuild = true;
#else
    private const bool IsDevelopmentBuild = false;
#endif

    [Theory]
    [InlineData("localhost", true, "none", -1, 1, true, true)]
    [InlineData("other.invalid", true, "none", -1, 1, false, false)]
    [InlineData("localhost", false, "none", -1, 1, false, false)]
    [InlineData("localhost", true, "none", -3, -1, false, false)]
    [InlineData("localhost", false, "matching", -1, 1, true, false)]
    [InlineData("other.invalid", false, "matching", -1, 1, false, false)]
    [InlineData("localhost", false, "wrong", -1, 1, false, false)]
    [InlineData("localhost", true, "wrong", -1, 1, false, true)]
    [InlineData("localhost", false, "matching", -3, -1, false, false)]
    [InlineData("localhost", false, "matching", 1, 3, false, false)]
    public async Task LoopbackHandshakeVerifiesPeer(string dnsName, bool trustIssuer, string pinKind,
        int validFromDay, int validToDay, bool developmentAccepts, bool playerAccepts)
    {
        var now = DateTimeOffset.UtcNow;
        using var issuerKey = RSA.Create(2048);
        var request = new CertificateRequest("CN=Echodia local test root", issuerKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var issuer = request.CreateSelfSigned(now.AddDays(-5), now.AddDays(5));
        using var leaf = IssueTestLeaf(dnsName, ServerAuth, now.AddDays(validFromDay), now.AddDays(validToDay), issuer);
        string? pin = pinKind switch
        {
            "matching" => leaf.GetCertHashString(HashAlgorithmName.SHA256),
            "wrong" => new string('0', 64),
            _ => null
        };
        var validator = ForHostWithPin("localhost", pin);
        bool expected = IsDevelopmentBuild ? developmentAccepts : playerAccepts;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        var accept = listener.AcceptTcpClientAsync();
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
        using var peer = await accept;
        using var serverTls = new SslStream(peer.GetStream());
        using var clientTls = new SslStream(client.GetStream(), false, validator);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var serverHandshake = serverTls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = leaf,
            EnabledSslProtocols = SslProtocols.Tls12
        }, timeout.Token);
        var trust = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = X509RevocationMode.NoCheck,
            DisableCertificateDownloads = true
        };
        trust.ExtraStore.Add(issuer);
        if (trustIssuer)
            trust.CustomTrustStore.Add(issuer);
        var options = new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            CertificateChainPolicy = trust,
            EnabledSslProtocols = SslProtocols.Tls12
        };

        try
        {
            if (expected)
            {
                await clientTls.AuthenticateAsClientAsync(options, timeout.Token);
                Assert.True(clientTls.IsAuthenticated && clientTls.IsEncrypted);
            }
            else
                await Assert.ThrowsAsync<AuthenticationException>(() => clientTls.AuthenticateAsClientAsync(options, timeout.Token));
        }
        finally
        {
            // A rejected certificate can cause the peer to see the alert or simply EOF.
            client.Close();
            try { await serverHandshake; }
            catch (Exception error) when (!expected && error is AuthenticationException or IOException) { }
        }
    }

    [Theory]
    [InlineData("game.example.invalid", "0011")]
    [InlineData("127.0.0.1", "0011")]
    [InlineData("localhost", null)]
    public void OrdinaryConnectionsUsePlatformValidation(string host, string? pin)
        => Assert.Null(ForHostWithPin(host, pin));

    [Fact]
    public void PinnedSelfSignedLeafStillRequiresServerAuthenticationUsage()
    {
        var now = DateTimeOffset.UtcNow;
        using var leaf = IssueTestLeaf("localhost", ClientAuth, now.AddDays(-1), now.AddDays(1));
        var validator = ForHostWithPin("localhost", leaf.GetCertHashString(HashAlgorithmName.SHA256));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(ServerAuth));
        Assert.False(chain.Build(leaf));
        Assert.Contains(chain.ChainStatus, item => item.Status.HasFlag(X509ChainStatusFlags.NotValidForUsage));
        Assert.False(validator!(this, leaf, chain, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(validator(this, null, null, SslPolicyErrors.RemoteCertificateNotAvailable));
#else
        Assert.Null(validator);
#endif
    }

    private static RemoteCertificateValidationCallback? ForHostWithPin(string host, string? pin)
    {
        string? original = Environment.GetEnvironmentVariable(PinVariable);
        try
        {
            Environment.SetEnvironmentVariable(PinVariable, pin);
            return GameServerCertificatePolicy.ForHost(host);
        }
        finally
        {
            Environment.SetEnvironmentVariable(PinVariable, original);
        }
    }

    private static X509Certificate2 IssueTestLeaf(string dnsName, string eku, DateTimeOffset from,
        DateTimeOffset to, X509Certificate2? issuer = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=" + dnsName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(eku) }, false));
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        if (issuer == null)
            return request.CreateSelfSigned(from, to);
        using var issued = request.Create(issuer, from, to, RandomNumberGenerator.GetBytes(16));
        using var keyed = issued.CopyWithPrivateKey(key);
        return X509CertificateLoader.LoadPkcs12(keyed.Export(X509ContentType.Pkcs12), null);
    }
}

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server.DB;

namespace Server.Authentication;

public sealed record CredentialDecision(LoginStatus Status, int AccountId)
{
    public static CredentialDecision CheckDatabase(C_Login credentials)
    {
        using var db = new GameDbContext();
        var (status, account) = AccountCredentials.Authenticate(db, credentials.Id, credentials.Password);
        // The game admits a session only after a successful password check.
        return new CredentialDecision(status, status == LoginStatus.Success ? account : 0);
    }
}

public sealed class LoginAuthority : IDisposable
{
    public const string Route = "/internal/login";
    public const string MediaType = "application/x-protobuf";
    readonly HttpClient _client;
    readonly X509Certificate2 _identity;
    readonly X509Certificate2 _issuer;

    public static LoginAuthority FromEnvironment()
    {
        string mode = Environment.GetEnvironmentVariable("ECHODIA_LOGIN_MODE");
        if (string.IsNullOrWhiteSpace(mode) || mode.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ECHODIA_LOGIN_URL")))
                throw new InvalidOperationException("Configure ECHODIA_LOGIN_MODE=remote with ECHODIA_LOGIN_URL.");
            return new LoginAuthority();
        }
        if (!mode.Equals("remote", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unknown ECHODIA_LOGIN_MODE.");
        return new LoginAuthority(new Uri(LoginTls.Required("ECHODIA_LOGIN_URL")),
            LoginTls.LoadIdentity(), LoginTls.LoadIssuer());
    }

    LoginAuthority() { }

    // Ownership of both certificates transfers to this long-lived connection pool.
    public LoginAuthority(Uri origin, X509Certificate2 identity, X509Certificate2 issuer)
    {
        if (!origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps || origin.UserInfo.Length > 0 ||
            origin.AbsolutePath != "/" || origin.Query.Length > 0 || origin.Fragment.Length > 0)
            throw new ArgumentException("The login origin must consist of an HTTPS host and optional port.");
        _identity = identity;
        _issuer = issuer;
        _client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            SslOptions = new SslClientAuthenticationOptions
            {
                ClientCertificates = new X509CertificateCollection { identity },
                CertificateChainPolicy = LoginTls.PeerPolicy(issuer, LoginTls.ServerAuth)
            }
        }) { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 4096 };
    }

    public async Task<CredentialDecision> AuthenticateAsync(C_Login request)
    {
        if (_client == null)
            return CredentialDecision.CheckDatabase(request);

        byte[] payload = request.ToByteArray();
        try
        {
            using var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue(MediaType);
            using var response = await _client.PostAsync(Route, content).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                return new CredentialDecision(LoginStatus.None, 0);
            response.EnsureSuccessStatusCode();
            var decision = await response.Content.ReadFromJsonAsync<CredentialDecision>().ConfigureAwait(false);
            if (decision == null || (decision.Status == LoginStatus.Success ? decision.AccountId <= 0 :
                decision.AccountId != 0 || decision.Status is not (LoginStatus.SignUpSuccess or LoginStatus.PasswordWrong or LoginStatus.None)))
                throw new HttpRequestException("Invalid login authority response.");
            return decision;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _identity?.Dispose();
        _issuer?.Dispose();
    }
}

public static class LoginTls
{
    public const string ServerAuth = "1.3.6.1.5.5.7.3.1";
    public const string ClientAuth = "1.3.6.1.5.5.7.3.2";

    public static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value : throw new InvalidOperationException($"Set {name} for the private login connection.");

    public static X509Certificate2 LoadIdentity()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);
        var certs = store.Certificates.Find(X509FindType.FindByThumbprint, Required("ECHODIA_LOGIN_CERT_THUMBPRINT"), false);
        if (certs.Count != 1 || !certs[0].HasPrivateKey || DateTime.Now < certs[0].NotBefore || DateTime.Now >= certs[0].NotAfter)
            throw new InvalidOperationException("Login identity must be valid and have an accessible private key.");
        return certs[0];
    }

    public static X509Certificate2 LoadIssuer() => new X509Certificate2(
        System.IO.File.ReadAllBytes(Required("ECHODIA_LOGIN_CA_FILE")));

    public static X509ChainPolicy PeerPolicy(X509Certificate2 issuer, string purpose)
    {
        // This deployment's private offline CA has no CRL. Use bounded leaf validity;
        // reissue the private CA if compromised and remove the previous trust anchor.
        var policy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = X509RevocationMode.NoCheck,
            DisableCertificateDownloads = true
        };
        policy.CustomTrustStore.Add(issuer);
        policy.ApplicationPolicy.Add(new Oid(purpose));
        return policy;
    }
}

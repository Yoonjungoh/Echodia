using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Server.Authentication;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "EchodiaLogin");
_ = LoginTls.Required("DB_CONNECTION_STRING");
var address = IPAddress.Parse(LoginTls.Required("ECHODIA_LOGIN_BIND_ADDRESS"));
using var identity = LoginTls.LoadIdentity();
using var issuer = LoginTls.LoadIssuer();
byte[] allowedClient = Convert.FromHexString(LoginTls.Required("ECHODIA_LOGIN_CLIENT_SHA256"));
if (allowedClient.Length != SHA256.HashSizeInBytes) throw new FormatException("Expected a SHA-256 fingerprint.");
using var app = PrivateLoginHost.Build(builder, new IPEndPoint(address, 8443), identity, issuer, allowedClient);
await app.RunAsync();

public static class PrivateLoginHost
{
    const int MaxFramePayload = ushort.MaxValue - 4;

    public static WebApplication Build(WebApplicationBuilder builder, IPEndPoint bind, X509Certificate2 identity,
        X509Certificate2 issuer, byte[] allowedClient)
    {
        if (bind.Address.Equals(IPAddress.Any) || bind.Address.Equals(IPAddress.IPv6Any))
            throw new ArgumentException("Bind the login service to its private IP address.");

        bool Authorized(X509Certificate2 certificate) =>
            CryptographicOperations.FixedTimeEquals(certificate.GetCertHash(HashAlgorithmName.SHA256), allowedClient);

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = MaxFramePayload;
            options.Listen(bind, listener => listener.UseHttps(https =>
            {
                https.ServerCertificate = identity;
                https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                https.ClientCertificateValidation = (certificate, _, _) =>
                {
                    if (!Authorized(certificate)) return false;
                    using var chain = new X509Chain { ChainPolicy = LoginTls.PeerPolicy(issuer, LoginTls.ClientAuth) };
                    return chain.Build(certificate);
                };
            }));
        });

        var app = builder.Build();
        // Only the private listener may perform a password check, including when a host
        // administrator has defined additional ASP.NET endpoints in external configuration.
        var capacity = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount / 2));
        app.Lifetime.ApplicationStopped.Register(capacity.Dispose);
        app.MapPost(LoginAuthority.Route, async (HttpContext ctx) =>
        {
            if (!ctx.Request.IsHttps || ctx.Connection.ClientCertificate is not { } client || !Authorized(client))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!string.Equals(ctx.Request.ContentType, LoginAuthority.MediaType, StringComparison.OrdinalIgnoreCase))
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            if (ctx.Request.ContentLength is not (> 0 and <= MaxFramePayload))
                return Results.BadRequest();
            if (!capacity.Wait(0))
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            byte[] payload = new byte[(int)ctx.Request.ContentLength.Value];
            try
            {
                await ctx.Request.Body.ReadExactlyAsync(payload, ctx.RequestAborted);
                return Results.Json(CredentialDecision.CheckDatabase(C_Login.Parser.ParseFrom(payload)));
            }
            catch (InvalidProtocolBufferException) { return Results.BadRequest(); }
            finally
            {
                CryptographicOperations.ZeroMemory(payload);
                capacity.Release();
            }
        });
        return app;
    }
}

#nullable enable
using System;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ServerCore
{
    internal static class GameServerCertificatePolicy
    {
        internal static RemoteCertificateValidationCallback? ForHost(string host)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (host == "localhost")
            {
                string? pin = Environment.GetEnvironmentVariable("ECHODIA_LOCAL_CERT_SHA256");
                if (pin != null && pin.Length != 0)
                    return (sender, cert, chain, errors) => ValidateLocalLeaf(cert, chain, errors, pin);
            }
#endif
            return null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static bool ValidateLocalLeaf(X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors, string pin)
        {
            if (cert == null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0)
                return false;

            if ((errors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
            {
                // The pin supplies missing trust. Other chain failures still apply.
                var statuses = chain?.ChainStatus;
                if (statuses == null || statuses.Length == 0)
                    return false;
                const X509ChainStatusFlags missingTrust = X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.PartialChain;
                foreach (var status in statuses)
                    if (status.Status == X509ChainStatusFlags.NoError || (status.Status & ~missingTrust) != 0)
                        return false;
            }

            using (var leaf = new X509Certificate2(cert))
            using (var sha = SHA256.Create())
            {
                DateTime now = DateTime.UtcNow;
                string actual = BitConverter.ToString(sha.ComputeHash(leaf.RawData)).Replace("-", "");
                return now >= leaf.NotBefore.ToUniversalTime() && now < leaf.NotAfter.ToUniversalTime()
                    && string.Equals(actual, pin, StringComparison.OrdinalIgnoreCase);
            }
        }
#endif
    }
}

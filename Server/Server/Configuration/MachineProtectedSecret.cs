using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Server.Configuration
{
    public static class MachineProtectedSecret
    {
        // The host ACL grants only the service, SYSTEM and administrators access to this blob.
        public static string Read(string path)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("Machine-protected credentials require Windows.");
            if (!Path.IsPathFullyQualified(path))
                throw new InvalidOperationException("The protected credential path must be absolute.");
            byte[] clear = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.LocalMachine);
            try
            {
                return new UTF8Encoding(false, true).GetString(clear);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(clear);
            }
        }
    }
}

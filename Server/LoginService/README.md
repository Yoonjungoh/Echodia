# Echodia private login service

Unity keeps the existing TLS game connection and the same `C_Login` and `S_Login` packets. In remote mode the game forwards the credential check to a private ASP.NET Core endpoint. The login process uses the existing EF Core account mapping, SQL Server unique index and PBKDF2 password verifier. The game still owns the player connection and its Redis session claim. Character and world state continue through the game process.

## Windows deployment

1. Restore the verified SQL Server backup on the private database host and test encrypted SQL connectivity. Keep account IDs, character relationships and password-verifier rows intact. Grant the login service identity the privileges needed to read and create accounts and convert legacy verifier rows. Use the game SQL identity for game data access.
2. Issue two certificates from a dedicated private CA. The login certificate needs server-authentication EKU and the login DNS name or private IP in its SAN. The game certificate needs client-authentication EKU. Install each endpoint's own certificate in `LocalMachine\My`, grant its service account private-key access and copy only the CA's public certificate to both nodes. The login node also needs the SHA-256 fingerprint of the approved game certificate (hash of DER encoding). The lookup thumbprint on each node is the certificate store's ordinary thumbprint.
3. Configure service-specific environment variables using the table below. Allow TCP 8443 to the login EC2 only from the game EC2 security group and mirror that restriction in Windows Firewall. Allow the SQL port inbound only from the game and login hosts. Both database connections should validate the SQL Server certificate with `Encrypt=True;TrustServerCertificate=False`. Keep connection strings and private keys off Git.
4. Publish with the .NET 10 SDK and install the executable as the Windows service `EchodiaLogin`. For example, `dotnet publish Server/LoginService/LoginService.csproj -c Release -r win-x64 --self-contained true`. The service can also run as a console process. Start SQL and the login service, verify availability, then switch the game to remote mode and restart it. The prior game release and the original database backup provide rollback points. After a data cutover, account writes require a coordinated rollback to avoid data loss.

| Variable | Game | Login |
|---|---|---|
| `ECHODIA_LOGIN_MODE` | `remote`, or `local` in the original single-process setup | |
| `ECHODIA_LOGIN_URL` | HTTPS origin including port 8443 | |
| `ECHODIA_LOGIN_CERT_THUMBPRINT` | Game client certificate | Login server certificate |
| `ECHODIA_LOGIN_CA_FILE` | Absolute path to public CA certificate | Absolute path to public CA certificate |
| `ECHODIA_LOGIN_BIND_ADDRESS` | | Private IP bound by Kestrel |
| `ECHODIA_LOGIN_CLIENT_SHA256` | | 64 hex digits identifying the game leaf |
| `DB_CONNECTION_STRING` | Game database credentials | Account database credentials |

The private channel validates the server name, chain, certificate purpose and validity. The login service verifies the client chain and allows only the configured game leaf. Private CA trust is scoped to this connection. This offline CA configuration does not perform CRL lookup. Use short-lived leaves, protect the offline issuer and provision replacements before expiry. For a game certificate rotation, change the fingerprint and client identity in a coordinated maintenance window. The public Unity certificate retains its separate renewal process.

The game pools HTTPS connections and bounds in-flight login checks. When the configured login authority is unavailable, a login returns the existing retry status and no local credential check runs. Successful credentials are followed by the existing atomic Redis session claim. The game process still receives the password through its public TLS connection and accesses the account table for character operations; separate SQL permissions and projections are additional work if the game must have zero access to verifier columns.

## Verification

Run `dotnet test Server/LoginService.Tests/LoginService.Tests.csproj -c Release`. On Windows with SQL Server LocalDB, the test starts a real private HTTPS listener, requires the exact game client certificate, exercises account creation and password verification, and removes its unique temporary database. The test creates synthetic certificates and credentials for its own process and database.

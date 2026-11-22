# Redis on a Windows EC2 for Echodia

The EC2 host runs Windows Server 2025 Core. The Redis process runs as the unprivileged `redis` user inside a dedicated Ubuntu 24.04 LTS WSL2 distribution. Redis publishes Linux packages. This arrangement uses its signed package and Windows administration tools. The extra VM, private port proxy and two operating systems add memory use and patching work. The game uses the same TCP/TLS and StackExchange.Redis protocol regardless of the cache host OS. This role handles login membership. Measure gameplay and authentication on the deployed configuration before attributing performance changes to Windows.

## Provisioning order

1. Complete the previous production rollout and its Unity verification. Create the dedicated Windows cache host with private connectivity and nested virtualization explicitly enabled on a supported instance type such as M7i-flex. A smaller Windows machine without nested virtualization cannot run this image. Permit inbound TCP 6380 in the cache security group only from the game host's security group. The boot task also scopes the Windows firewall to the game's private IPv4 address. Keep the private addresses stable. Allow initial HTTPS access to Windows/WSL release endpoints, Ubuntu and packages.redis.io, and Ubuntu archive access required by the image. The cache does not need public inbound access.
2. On the **game host**, run the following from an elevated Windows PowerShell in the deployment package directory. Use its actual game service identity. The generated 256-bit random credential stays on the game host, in a machine-DPAPI ciphertext with a SYSTEM, Administrators and game-service ACL. The output contains a SHA-256 verifier and the protected file path. Use a private administrative channel to supply the verifier to the cache operator.

   ```powershell
   .\New-GameRedisCredential.ps1 -RedisPrivateIp <cache-private-ip> -GameServiceAccount '<service-identity>'
   ```

3. On the **cache host**, use an elevated Windows PowerShell to install its startup task. `-RestartIfNeeded` allows a cache-host reboot to enable WSL and the Virtual Machine Platform. Run this while the role is still offline. A dedicated Windows standard user owns the distro and runs its foreground task at startup. Task Scheduler stores its random batch-logon password. WSL explicitly rejects LocalSystem sessions. A second task running as LocalSystem controls only the Windows firewall and private port proxy. The standard account can update its own state directory and read the administrator-owned scripts, role configuration and public certificates. [Microsoft WSL session creation](https://github.com/microsoft/WSL/blob/master/src/windows/service/exe/LxssUserSessionFactory.cpp#L181-L185)

   ```powershell
   .\Install-RedisHost.ps1 -RedisPrivateIp <cache-private-ip> -GamePrivateIp <game-private-ip> -RedisAclSha256 <verifier> -RestartIfNeeded
   Get-Content C:\ProgramData\EchodiaRedis\state\status.txt
   ```

   The installer verifies the Microsoft WSL machine MSI digest and Authenticode publisher on a fresh host. The unprivileged owner task checks the downloaded Canonical image against the pinned SHA-256 from its signed 24.04.5 manifest. Before adopting a newer image, verify Canonical's manifest against its complete image-signing fingerprint `843938DF228D22F7B3742BC0D94AA3F0EFE21092` and update the URL and digest together. Ubuntu APT verifies Redis's archive metadata with its scoped vendor key. The boot script pins that key's primary fingerprint and records the installed package version in `/etc/echodia-redis/package-version`. For upgrades use a maintenance window, repeat package signature validation and test the resulting version before admitting traffic. Boot normally reuses the installed version.

4. The cache task creates a 3072-bit RSA private key in the Linux filesystem and requests a certificate using the cache private IP as its SAN. Copy **only** `C:\ProgramData\EchodiaRedis\state\server.csr` to the authorized private CA. Inspect the request with `openssl req -in server.csr -noout -text -verify`. Match the requested IP against the cache ENI you provisioned. Issue a server certificate for that IP with `extendedKeyUsage=serverAuth`, `basicConstraints=CA:FALSE`, `keyUsage=digitalSignature,keyEncipherment` and a bounded lifetime. For a dedicated one-tier private CA, the following OpenSSL commands run on the **CA host**. The extension file supplies the authorized IP explicitly, so unreviewed CSR extensions are ignored.

   ```ini
   # redis-leaf.cnf, replace the example with the actual private cache IP
   basicConstraints = critical,CA:FALSE
   keyUsage = critical,digitalSignature,keyEncipherment
   extendedKeyUsage = serverAuth
   subjectAltName = IP:10.0.1.42
   ```

   ```sh
   openssl x509 -req -in server.csr -CA private-ca.pem -CAkey private-ca.key -CAcreateserial -days 90 -sha256 -extfile redis-leaf.cnf -out server.pem
   openssl verify -purpose sslserver -verify_ip 10.0.1.42 -CAfile private-ca.pem server.pem
   ```

   After verifying the expected CA fingerprint through the deployment channel, install the public root into the game host's LocalMachine trusted roots and copy that root as `C:\ProgramData\EchodiaRedis\ca.pem` plus the signed leaf as `server.pem` into the same protected cache directory. The single-tier example assumes a leaf signed directly by the trusted root. Keep the CA key with its issuer. Start the cache task. On renewal replace the public PEMs and restart the cache role in a drained maintenance window.

   ```powershell
   Start-ScheduledTask EchodiaRedis
   Get-Content C:\ProgramData\EchodiaRedis\proxy-status.txt
   Get-Content C:\ProgramData\EchodiaRedis\state\status.txt
   ```

5. On the game host set **only** `ECHODIA_REDIS_CONNECTION_DPAPI_FILE` to the generated file path in the game service configuration. Clear any previous `ECHODIA_REDIS_CONNECTION` setting. The credential is bound to this Windows installation. Moving the role to another Windows machine requires rotating the credential. Validate in the game's execution environment before restarting it.

   ```powershell
   $env:ECHODIA_REDIS_CONNECTION_DPAPI_FILE = 'C:\ProgramData\EchodiaRedisGameCredential\connection.dpapi'
   .\Server.exe --check-redis
   ```

   The check uses the game binary's normal Redis client, an authenticated ping and a read of set membership. Check its exit code. Verify a normal Unity login and disconnect as well, since the read-only check leaves membership untouched. Reboot the cache once during staging and repeat both checks. This demonstrates unattended two-task startup, the current WSL address binding, certificate validity, connectivity and game login on the selected host.

## Operation and cutover

The Linux listener accepts TLS 1.2 or 1.3 on 6380, with the default account off. The named Redis account can add, remove and inspect members of `loggedIn`. The ACL also permits the connection setup and liveness commands used by StackExchange.Redis. The game trusts the cache certificate and supplies its private scoped credential inside TLS. A guest health check signals the privileged Windows task to open the private port proxy only after an anonymous TLS request receives the expected authentication rejection, the certificate's IP and issuing CA match, and the plaintext Redis port is closed inside the guest. Startup rebinds the proxy to the current WSL IP. The proxy accepts only an on-link WSL NAT address with a fresh heartbeat from the current boot. Logs are `state\redis.log`, `state\redis.err`, `state\status.txt` and `proxy-status.txt` under the cache’s protected ProgramData directory.

Drain game logins and disconnect active sessions before switching endpoints. An account left in the old `loggedIn` set can survive an unclean game shutdown. Verify the previous cache’s authenticated session count with an operations identity and reconcile with actual live sessions. If it stores keys other than login state, inventory and migrate them with the proper Redis tooling before cutover. Preserve a snapshot and the prior game endpoint until validation passes. On failure keep new logins drained, restore the previous endpoint and retest. AOF with fsync on every write preserves membership through guest restarts at a possible latency cost per change. Measure that cost with actual login traffic. Stop all four EC2 instances when the deployment window ends per the project runbook.

[Redis signed APT packages](https://redis.io/docs/latest/operate/oss_and_stack/install/install-stack/apt/) · [Canonical image verification](https://documentation.ubuntu.com/security/software-integrity/image-verification/) · [Ubuntu 24.04.5 signed release manifest](https://releases.ubuntu.com/24.04.5/SHA256SUMS.gpg) · [Microsoft WSL on Server](https://learn.microsoft.com/en-us/windows/wsl/install-on-server) · [WSL NAT networking](https://learn.microsoft.com/en-us/windows/wsl/networking) · [AWS nested virtualization](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/amazon-ec2-nested-virtualization.html)

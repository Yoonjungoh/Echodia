using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore
{
    public class Connector
    {
        public async Task ConnectAsync(string host, int port, Func<Session> sessionFactory)
        {
            TcpClient client = null;
            SslStream tls = null;
            try
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var addresses = await Dns.GetHostAddressesAsync(host);
                    SocketException last = null;
                    foreach (var address in addresses)
                    {
                        timeout.Token.ThrowIfCancellationRequested();
                        client = new TcpClient(address.AddressFamily);
                        try
                        {
                            using (timeout.Token.Register(client.Close))
                                await client.ConnectAsync(address, port);
                            break;
                        }
                        catch (SocketException error)
                        {
                            client.Close();
                            client = null;
                            last = error;
                        }
                    }
                    if (client == null)
                        throw last ?? new SocketException((int)SocketError.HostNotFound);
                    using (timeout.Token.Register(client.Close))
                    {
                        tls = new SslStream(client.GetStream(), false, GameServerCertificatePolicy.ForHost(host));
                        await tls.AuthenticateAsClientAsync(host, null, SslProtocols.None, true);
                    }
                }
                sessionFactory().Start(tls, client.Client.RemoteEndPoint);
            }
            catch
            {
                tls?.Dispose();
                client?.Close();
                throw;
            }
        }
    }
}

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
            var client = new TcpClient(AddressFamily.InterNetworkV6);
            client.Client.DualMode = true;
            SslStream tls = null;
            try
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                using (timeout.Token.Register(client.Close))
                {
                    await client.ConnectAsync(host, port);
                    tls = new SslStream(client.GetStream(), false, GameServerCertificatePolicy.ForHost(host));
                    await tls.AuthenticateAsClientAsync(host, null, SslProtocols.None, true);
                }
                sessionFactory().Start(tls, client.Client.RemoteEndPoint);
            }
            catch
            {
                tls?.Dispose();
                client.Close();
                throw;
            }
        }
    }
}

using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore
{
    public class Listener
    {
        public void Init(IPEndPoint endPoint, Func<Session> sessionFactory,
            SslStreamCertificateContext certificate, int register = 10, int backlog = 100)
        {
            var listener = new TcpListener(endPoint);
            listener.Start(backlog);
            var options = new SslServerAuthenticationOptions
            {
                ServerCertificateContext = certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            };
            // Each accept loop admits at most one handshake at a time.
            for (int i = 0; i < register; i++)
                _ = AcceptAsync(listener, sessionFactory, options);
        }

        static async Task AcceptAsync(TcpListener listener, Func<Session> sessionFactory,
            SslServerAuthenticationOptions options)
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (SocketException error)
                {
                    ConsoleLogManager.Instance.Log("Accept failed: " + error.SocketErrorCode);
                    await Task.Delay(100).ConfigureAwait(false);
                    continue;
                }
                SslStream tls = null;
                try
                {
                    tls = new SslStream(client.GetStream());
                    using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                        await tls.AuthenticateAsServerAsync(options, timeout.Token).ConfigureAwait(false);
                    sessionFactory().Start(tls, client.Client.RemoteEndPoint);
                }
                catch (Exception error)
                {
                    tls?.Dispose();
                    client.Dispose();
                    ConsoleLogManager.Instance.Log("TLS connection closed: " + error.GetType().Name);
                }
            }
        }
    }
}

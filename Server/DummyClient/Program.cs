using DummyClient.Session;
using ServerCore;

namespace DummyClient
{
    public class Program
    {
        static int DummyClientCount { get; } = 10;

        static void Main(string[] args)
        {
            Thread.Sleep(3000);

            // ── 더미 동작 플래그 ────────────────────────────────────
            ServerSession.EnableMovement = true;   // 랜덤 이동
            ServerSession.EnableShooting = true;   // 10초마다 투사체 발사
            // ────────────────────────────────────────────────────────

            Connector connector = new Connector();

            string targetHost = Environment.GetEnvironmentVariable("ECHODIA_GAME_HOST") ?? "localhost";
            Task.WaitAll(Enumerable.Range(0, DummyClientCount)
                .Select(_ => connector.ConnectAsync(targetHost, 7777, SessionManager.Instance.Generate)).ToArray());

            while (true)
            {
                Thread.Sleep(10000);
            }
        }
    }
}

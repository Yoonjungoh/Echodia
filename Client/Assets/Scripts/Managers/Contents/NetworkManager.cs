using Cysharp.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Newtonsoft.Json;
using ServerCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Networking;

public class NetworkManager
{
    ServerSession _session = new ServerSession();
    private bool _connecting;

    // 서버와의 offset 저장
    public double ServerOffsetMs { get; set; } // 서버 기준 시각과 내 로컬 시각의 차이 (초 단위)
    public double RTTMs { get; set; }
    public double LatencyMs { get; set; }
    public bool IsInitialized => _session.IsConnected;

    private long _nextLatencyCheckMs = 0;
    private const long LatencyCheckIntervalMs = 5000;

    #region 서버와의 시간 차이 계산
    // t1: 내가 패킷을 보낸 시각 (클라 로컬)
    // t2: 서버가 패킷을 받은 시각 (서버 기준)
    // t3: 내가 서버 응답을 받은 시각 (클라 로컬)
    #endregion
    public void CalculateTimeOffset(double clientSendTimeMs, double serverReceiveTimeMs)
    {
        long clientReceiveTimeMs = Util.GetTimestampMs(); // 현재 클라 로컬 시각
        RTTMs = clientReceiveTimeMs - clientSendTimeMs;
        LatencyMs = RTTMs / 2.0;
        ServerOffsetMs = (serverReceiveTimeMs + LatencyMs) - clientReceiveTimeMs;
    }

    // 서버 기준 현재 시각 반환
    public double GetServerNowMs()
    {
        return Util.GetTimestampMs() + ServerOffsetMs;
    }

    public void RequestServerTimeSync()
    {
        // 서버와의 시간 offset 계산
        C_Timestamp timestampPacket = new C_Timestamp();
        timestampPacket.ClientSendTime = Util.GetTimestampMs();
        Managers.Network.Send(timestampPacket);
    }

    public void Send(IMessage packet)
    {
        _session.Send(packet); 
    }

    public class URLData
    {
        public string url;
    }

    public async UniTask CoDownloadServerURL(Action callBack = null)
    {
        if (_connecting)
            return;
        _connecting = true;
        try
        {
            string host = Environment.GetEnvironmentVariable("ECHODIA_GAME_HOST");
            if (string.IsNullOrWhiteSpace(host))
            {
                using (var request = UnityWebRequest.Get(Managers.URL.Ec2Url))
                {
                    request.timeout = 5;
                    try
                    {
                        await request.SendWebRequest();
                        host = JsonConvert.DeserializeObject<URLData>(request.downloadHandler.text)?.url;
                    }
                    catch (Exception error)
                    {
                        Debug.LogWarning("Endpoint discovery failed: " + error.GetType().Name);
                    }
                }
                if (string.IsNullOrWhiteSpace(host))
                    host = "54.116.85.22";
            }

            RemoteCertificateValidationCallback validate = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string pin = Environment.GetEnvironmentVariable("ECHODIA_LOCAL_CERT_SHA256");
            if (host == "localhost" && !string.IsNullOrEmpty(pin))
                validate = (sender, cert, chain, errors) => ValidateLocalCertificate(cert, errors, pin);
#endif
            ServerSession session = new ServerSession();
            await new Connector().ConnectAsync(host, 7777, () => _session = session, validate);
        }
        catch (Exception error)
        {
            Debug.LogWarning("Game TLS connection failed: " + error.GetBaseException().Message);
            Managers.UI.ShowToastPopup("게임 서버에 안전하게 연결할 수 없습니다. 로그인 버튼으로 다시 시도해 주세요.");
        }
        finally
        {
            _connecting = false;
            callBack?.Invoke();
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // The local development player trusts one explicitly selected localhost leaf.
    static bool ValidateLocalCertificate(X509Certificate cert, SslPolicyErrors errors, string pin)
    {
        if (cert == null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0)
            return false;
        using (var leaf = new X509Certificate2(cert))
        using (var sha = SHA256.Create())
        {
            string actual = BitConverter.ToString(sha.ComputeHash(leaf.RawData)).Replace("-", "");
            return DateTime.Now >= leaf.NotBefore && DateTime.Now < leaf.NotAfter
                && string.Equals(actual, pin, StringComparison.OrdinalIgnoreCase);
        }
    }
#endif

    public void OnUpdate()
    {
        List<PacketMessage> list = PacketQueue.Instance.PopAll();
        foreach (PacketMessage packet in list)
        {
            Action<PacketSession, IMessage> handler = PacketManager.Instance.GetPacketHandler(packet.Id);
            if (handler != null)
                handler.Invoke(_session, packet.Message);
        }

        if (IsInitialized)
        {
            long now = Util.GetTimestampMs();
            if (now >= _nextLatencyCheckMs)
            {
                _nextLatencyCheckMs = now + LatencyCheckIntervalMs;
                RequestServerTimeSync();
            }
        }
    }
}
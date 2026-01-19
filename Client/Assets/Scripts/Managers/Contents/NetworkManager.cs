using Google.Protobuf;
using Google.Protobuf.Protocol;
using Newtonsoft.Json;
using ServerCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Networking;

public class NetworkManager
{
    ServerSession _session = new ServerSession();
    private string urlValue;

    // 서버와의 offset 저장
    public double ServerOffsetMs { get; set; } // 서버 기준 시각과 내 로컬 시각의 차이 (초 단위)
    public double RTTMs { get; set; }
    public double LatencyMs { get; set; }
    public bool IsInitialized { get; set; } = false;

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

    public IEnumerator CoDownloadServerURL(Action callBack = null)
    {
        UnityWebRequest www = UnityWebRequest.Get(Managers.URL.Ec2Url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Error: " + www.error);
            Init(callBack); // 실패해도 로컬 접속
        }
        else
        {
            URLData urlData = JsonConvert.DeserializeObject<URLData>(www.downloadHandler.text);
            urlValue = urlData.url;
            Init(callBack);
        }
    }

    public void Init(Action callBack = null)
    {
        // DNS (Domain Name System)
        string host = Dns.GetHostName();
        IPHostEntry ipHost = Dns.GetHostEntry(host);
        //IPAddress ipAddr = IPAddress.Parse(urlValue); // for ec2
        IPAddress ipAddr = ipHost.AddressList[0]; // for local test
        //IPAddress ipAddr = ipHost.AddressList[1]; // for local test
        IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);

        Connector connector = new Connector();

        connector.Connect(endPoint,
           () => { return _session; },
           1);

        callBack?.Invoke();

        IsInitialized = true;
    }

    public void OnUpdate()
    {
        List<PacketMessage> list = PacketQueue.Instance.PopAll();
        foreach (PacketMessage packet in list)
        {
            Action<PacketSession, IMessage> handler = PacketManager.Instance.GetPacketHandler(packet.Id);
            if (handler != null)
                handler.Invoke(_session, packet.Message);
        }
    }
}
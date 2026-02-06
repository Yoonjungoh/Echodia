using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

public class ServerSession : PacketSession
{
    public int DummyId { get; set; }
    public int ServerId { get; set; }
    public int ChannelId { get; set; }
    public int PlayerId { get; set; }   // 선택된 플레이어 아이디

    public void Send(IMessage packet)
    {
        string msgName = packet.Descriptor.Name.Replace("_", string.Empty);
        MsgId msgId = (MsgId)Enum.Parse(typeof(MsgId), msgName);
        ushort size = (ushort)packet.CalculateSize();
        byte[] sendBuffer = new byte[size + 4];
        Array.Copy(BitConverter.GetBytes((ushort)(size + 4)), 0, sendBuffer, 0, sizeof(ushort));
        Array.Copy(BitConverter.GetBytes((ushort)msgId), 0, sendBuffer, 2, sizeof(ushort));
        Array.Copy(packet.ToByteArray(), 0, sendBuffer, 4, size);
        Send(new ArraySegment<byte>(sendBuffer));
    }

    public override void OnConnected(EndPoint endPoint)
    {
        Console.WriteLine($"OnConnected : {endPoint}");
    }

    public override void OnDisconnected(EndPoint endPoint)
    {
        Console.WriteLine($"OnDisconnected : {endPoint}, DummyId: {DummyId}");
    }

    public override void OnRecvPacket(ArraySegment<byte> buffer)
    {
        PacketManager.Instance.OnRecvPacket(this, buffer);
    }

    public override void OnSend(int numOfBytes)
    {
        //Console.WriteLine($"Transferred bytes: {numOfBytes}");
    }
}
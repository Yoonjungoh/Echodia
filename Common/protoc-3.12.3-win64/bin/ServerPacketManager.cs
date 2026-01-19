using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.Collections.Generic;

class PacketManager
{
	#region Singleton
	static PacketManager _instance = new PacketManager();
	public static PacketManager Instance { get { return _instance; } }
	#endregion

	PacketManager()
	{
		Register();
	}

	Dictionary<ushort, Action<PacketSession, ArraySegment<byte>, ushort>> _onRecv = new Dictionary<ushort, Action<PacketSession, ArraySegment<byte>, ushort>>();
	Dictionary<ushort, Action<PacketSession, IMessage>> _handler = new Dictionary<ushort, Action<PacketSession, IMessage>>();
		
	public Action<PacketSession, IMessage, ushort> CustomHandler { get; set; }

	public void Register()
	{		
		_onRecv.Add((ushort)MsgId.CAssignUserId, MakePacket<C_AssignUserId>);
		_handler.Add((ushort)MsgId.CAssignUserId, PacketHandler.C_AssignUserIdHandler);		
		_onRecv.Add((ushort)MsgId.CEnterGame, MakePacket<C_EnterGame>);
		_handler.Add((ushort)MsgId.CEnterGame, PacketHandler.C_EnterGameHandler);		
		_onRecv.Add((ushort)MsgId.CAttack, MakePacket<C_Attack>);
		_handler.Add((ushort)MsgId.CAttack, PacketHandler.C_AttackHandler);		
		_onRecv.Add((ushort)MsgId.CMove, MakePacket<C_Move>);
		_handler.Add((ushort)MsgId.CMove, PacketHandler.C_MoveHandler);		
		_onRecv.Add((ushort)MsgId.CSpawnProjectile, MakePacket<C_SpawnProjectile>);
		_handler.Add((ushort)MsgId.CSpawnProjectile, PacketHandler.C_SpawnProjectileHandler);		
		_onRecv.Add((ushort)MsgId.CTimestamp, MakePacket<C_Timestamp>);
		_handler.Add((ushort)MsgId.CTimestamp, PacketHandler.C_TimestampHandler);		
		_onRecv.Add((ushort)MsgId.CChangeCreatureState, MakePacket<C_ChangeCreatureState>);
		_handler.Add((ushort)MsgId.CChangeCreatureState, PacketHandler.C_ChangeCreatureStateHandler);		
		_onRecv.Add((ushort)MsgId.CLogin, MakePacket<C_Login>);
		_handler.Add((ushort)MsgId.CLogin, PacketHandler.C_LoginHandler);		
		_onRecv.Add((ushort)MsgId.CRequestPlayerList, MakePacket<C_RequestPlayerList>);
		_handler.Add((ushort)MsgId.CRequestPlayerList, PacketHandler.C_RequestPlayerListHandler);		
		_onRecv.Add((ushort)MsgId.CCreatePlayer, MakePacket<C_CreatePlayer>);
		_handler.Add((ushort)MsgId.CCreatePlayer, PacketHandler.C_CreatePlayerHandler);		
		_onRecv.Add((ushort)MsgId.CDeletePlayer, MakePacket<C_DeletePlayer>);
		_handler.Add((ushort)MsgId.CDeletePlayer, PacketHandler.C_DeletePlayerHandler);		
		_onRecv.Add((ushort)MsgId.CUpdateCurrencyData, MakePacket<C_UpdateCurrencyData>);
		_handler.Add((ushort)MsgId.CUpdateCurrencyData, PacketHandler.C_UpdateCurrencyDataHandler);		
		_onRecv.Add((ushort)MsgId.CUpdateCurrencyDataAll, MakePacket<C_UpdateCurrencyDataAll>);
		_handler.Add((ushort)MsgId.CUpdateCurrencyDataAll, PacketHandler.C_UpdateCurrencyDataAllHandler);		
		_onRecv.Add((ushort)MsgId.CRequestServerList, MakePacket<C_RequestServerList>);
		_handler.Add((ushort)MsgId.CRequestServerList, PacketHandler.C_RequestServerListHandler);		
		_onRecv.Add((ushort)MsgId.CRequestServerSummaryList, MakePacket<C_RequestServerSummaryList>);
		_handler.Add((ushort)MsgId.CRequestServerSummaryList, PacketHandler.C_RequestServerSummaryListHandler);
	}

	public void OnRecvPacket(PacketSession session, ArraySegment<byte> buffer)
	{
		ushort count = 0;

		ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
		count += 2;
		ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
		count += 2;

		Action<PacketSession, ArraySegment<byte>, ushort> action = null;
		if (_onRecv.TryGetValue(id, out action))
			action.Invoke(session, buffer, id);
	}

	void MakePacket<T>(PacketSession session, ArraySegment<byte> buffer, ushort id) where T : IMessage, new()
	{
		T pkt = new T();
		pkt.MergeFrom(buffer.Array, buffer.Offset + 4, buffer.Count - 4);

		if (CustomHandler != null)
        {
			CustomHandler.Invoke(session, pkt, id);	
		}
        else
		{
			Action<PacketSession, IMessage> action = null;
			if (_handler.TryGetValue(id, out action))
				action.Invoke(session, pkt);
		}
	}

	public Action<PacketSession, IMessage> GetPacketHandler(ushort id)
	{
		Action<PacketSession, IMessage> action = null;
		if (_handler.TryGetValue(id, out action))
			return action;
		return null;
	}
}
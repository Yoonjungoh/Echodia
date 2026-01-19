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
		_onRecv.Add((ushort)MsgId.SAssignUserId, MakePacket<S_AssignUserId>);
		_handler.Add((ushort)MsgId.SAssignUserId, PacketHandler.S_AssignUserIdHandler);		
		_onRecv.Add((ushort)MsgId.SEnterGame, MakePacket<S_EnterGame>);
		_handler.Add((ushort)MsgId.SEnterGame, PacketHandler.S_EnterGameHandler);		
		_onRecv.Add((ushort)MsgId.SAttack, MakePacket<S_Attack>);
		_handler.Add((ushort)MsgId.SAttack, PacketHandler.S_AttackHandler);		
		_onRecv.Add((ushort)MsgId.SLeaveGame, MakePacket<S_LeaveGame>);
		_handler.Add((ushort)MsgId.SLeaveGame, PacketHandler.S_LeaveGameHandler);		
		_onRecv.Add((ushort)MsgId.SSpawn, MakePacket<S_Spawn>);
		_handler.Add((ushort)MsgId.SSpawn, PacketHandler.S_SpawnHandler);		
		_onRecv.Add((ushort)MsgId.SDespawn, MakePacket<S_Despawn>);
		_handler.Add((ushort)MsgId.SDespawn, PacketHandler.S_DespawnHandler);		
		_onRecv.Add((ushort)MsgId.SMove, MakePacket<S_Move>);
		_handler.Add((ushort)MsgId.SMove, PacketHandler.S_MoveHandler);		
		_onRecv.Add((ushort)MsgId.SDie, MakePacket<S_Die>);
		_handler.Add((ushort)MsgId.SDie, PacketHandler.S_DieHandler);		
		_onRecv.Add((ushort)MsgId.SEnterServerSelectScene, MakePacket<S_EnterServerSelectScene>);
		_handler.Add((ushort)MsgId.SEnterServerSelectScene, PacketHandler.S_EnterServerSelectSceneHandler);		
		_onRecv.Add((ushort)MsgId.STimestamp, MakePacket<S_Timestamp>);
		_handler.Add((ushort)MsgId.STimestamp, PacketHandler.S_TimestampHandler);		
		_onRecv.Add((ushort)MsgId.SChangeCreatureState, MakePacket<S_ChangeCreatureState>);
		_handler.Add((ushort)MsgId.SChangeCreatureState, PacketHandler.S_ChangeCreatureStateHandler);		
		_onRecv.Add((ushort)MsgId.SConnected, MakePacket<S_Connected>);
		_handler.Add((ushort)MsgId.SConnected, PacketHandler.S_ConnectedHandler);		
		_onRecv.Add((ushort)MsgId.SLogin, MakePacket<S_Login>);
		_handler.Add((ushort)MsgId.SLogin, PacketHandler.S_LoginHandler);		
		_onRecv.Add((ushort)MsgId.SRequestPlayerList, MakePacket<S_RequestPlayerList>);
		_handler.Add((ushort)MsgId.SRequestPlayerList, PacketHandler.S_RequestPlayerListHandler);		
		_onRecv.Add((ushort)MsgId.SCreatePlayer, MakePacket<S_CreatePlayer>);
		_handler.Add((ushort)MsgId.SCreatePlayer, PacketHandler.S_CreatePlayerHandler);		
		_onRecv.Add((ushort)MsgId.SDeletePlayer, MakePacket<S_DeletePlayer>);
		_handler.Add((ushort)MsgId.SDeletePlayer, PacketHandler.S_DeletePlayerHandler);		
		_onRecv.Add((ushort)MsgId.SUpdateCurrencyData, MakePacket<S_UpdateCurrencyData>);
		_handler.Add((ushort)MsgId.SUpdateCurrencyData, PacketHandler.S_UpdateCurrencyDataHandler);		
		_onRecv.Add((ushort)MsgId.SUpdateCurrencyDataAll, MakePacket<S_UpdateCurrencyDataAll>);
		_handler.Add((ushort)MsgId.SUpdateCurrencyDataAll, PacketHandler.S_UpdateCurrencyDataAllHandler);		
		_onRecv.Add((ushort)MsgId.SRequestServerList, MakePacket<S_RequestServerList>);
		_handler.Add((ushort)MsgId.SRequestServerList, PacketHandler.S_RequestServerListHandler);		
		_onRecv.Add((ushort)MsgId.SRequestServerSummaryList, MakePacket<S_RequestServerSummaryList>);
		_handler.Add((ushort)MsgId.SRequestServerSummaryList, PacketHandler.S_RequestServerSummaryListHandler);
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
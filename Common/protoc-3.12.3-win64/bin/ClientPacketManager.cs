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
		_onRecv.Add((ushort)MsgId.SEnterLobby, MakePacket<S_EnterLobby>);
		_handler.Add((ushort)MsgId.SEnterLobby, PacketHandler.S_EnterLobbyHandler);		
		_onRecv.Add((ushort)MsgId.SLeaveLobby, MakePacket<S_LeaveLobby>);
		_handler.Add((ushort)MsgId.SLeaveLobby, PacketHandler.S_LeaveLobbyHandler);		
		_onRecv.Add((ushort)MsgId.SEnterWaitingRoom, MakePacket<S_EnterWaitingRoom>);
		_handler.Add((ushort)MsgId.SEnterWaitingRoom, PacketHandler.S_EnterWaitingRoomHandler);		
		_onRecv.Add((ushort)MsgId.SAddRoom, MakePacket<S_AddRoom>);
		_handler.Add((ushort)MsgId.SAddRoom, PacketHandler.S_AddRoomHandler);		
		_onRecv.Add((ushort)MsgId.SRemoveRoom, MakePacket<S_RemoveRoom>);
		_handler.Add((ushort)MsgId.SRemoveRoom, PacketHandler.S_RemoveRoomHandler);		
		_onRecv.Add((ushort)MsgId.SExitRoom, MakePacket<S_ExitRoom>);
		_handler.Add((ushort)MsgId.SExitRoom, PacketHandler.S_ExitRoomHandler);		
		_onRecv.Add((ushort)MsgId.SUpdateWaitingRoomInfo, MakePacket<S_UpdateWaitingRoomInfo>);
		_handler.Add((ushort)MsgId.SUpdateWaitingRoomInfo, PacketHandler.S_UpdateWaitingRoomInfoHandler);		
		_onRecv.Add((ushort)MsgId.SStartGame, MakePacket<S_StartGame>);
		_handler.Add((ushort)MsgId.SStartGame, PacketHandler.S_StartGameHandler);		
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
		_onRecv.Add((ushort)MsgId.SStartCountdown, MakePacket<S_StartCountdown>);
		_handler.Add((ushort)MsgId.SStartCountdown, PacketHandler.S_StartCountdownHandler);		
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
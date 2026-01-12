using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;

class PacketHandler
{
    public static void S_AssignUserIdHandler(PacketSession session, IMessage packet)
    {
        // UI 찾는게 더 무겁고 패킷 캐스팅이 더 가벼우니 패킷 먼저 체크
        S_AssignUserId assignUserIdPacket = packet as S_AssignUserId;
        if (assignUserIdPacket == null)
        {
            Debug.Log("S_AssignUserId 패킷이 null입니다");
            return;
        }
        Managers.WaitingRoomObject.UserId = assignUserIdPacket.UserId;
    }

    // 로비에 입장했을 때
    public static void S_EnterLobbyHandler(PacketSession session, IMessage packet)
    {
        // UI 찾는게 더 무겁고 패킷 캐스팅이 더 가벼우니 패킷 먼저 체크
        S_EnterLobby enterLobbyPacket = packet as S_EnterLobby;
        if (enterLobbyPacket == null || enterLobbyPacket.UserIdList == null)
        {
            Debug.Log("S_EnterLobby 패킷이 null입니다");
            return;
        }

        UI_Lobby lobbyUI = Managers.UI.CurrentScene.GetComponent<UI_Lobby>();
        if (lobbyUI == null)
        {
            Debug.Log("현재 로비가 아닌데 로비에 입장하려고 합니다.");
            return;
        }

        lobbyUI.EnterLobby(enterLobbyPacket.UserIdList, enterLobbyPacket.UserNameList);
        lobbyUI.AddRoom(enterLobbyPacket.RoomInfoList); // 기존 방 목록 추가
    }

    // 로비에서 누군가 퇴장했을 때
    public static void S_LeaveLobbyHandler(PacketSession session, IMessage packet)
    {
        // UI 찾는게 더 무겁고 패킷 캐스팅이 더 가벼우니 패킷 먼저 체크
        S_LeaveLobby leaveLobbyPacket = packet as S_LeaveLobby;
        if (leaveLobbyPacket == null)
        {
            Debug.Log("S_LeaveLobby 패킷이 null입니다");
            return;
        }

        UI_Lobby lobbyUI = Managers.UI.CurrentScene.GetComponent<UI_Lobby>();
        if (Managers.UI.CurrentScene == null)
        {
            Managers.UI.ShowToastPopup("다시 시도해주세요.");
        }
        if (lobbyUI == null)
        {
            Debug.Log("현재 로비가 아닌데 로비에 입장하려고 합니다.");
            return;
        }
        lobbyUI.LeaveLobby(leaveLobbyPacket.UserId);
    }

    public static void S_EnterWaitingRoomHandler(PacketSession session, IMessage packet)
    {
        S_EnterWaitingRoom enterWaitingRoomPacket = packet as S_EnterWaitingRoom;
        if (enterWaitingRoomPacket == null)
        {
            Debug.Log("S_EnterWaitingRoom 패킷이 null입니다");
            return;
        }

        Managers.WaitingRoomObject.Add(enterWaitingRoomPacket.ObjectState, isMyPlayer: true);
    }

    public static void S_AddRoomHandler(PacketSession session, IMessage packet)
    {
        // UI 찾는게 더 무겁고 패킷 캐스팅이 더 가벼우니 패킷 먼저 체크
        S_AddRoom addRoomPacket = packet as S_AddRoom;
        if (addRoomPacket == null)
        {
            Debug.Log("S_AddRoom 패킷이 null입니다");
            return;
        }

        UI_Lobby lobbyUI = Managers.UI.CurrentScene.GetComponent<UI_Lobby>();
        if (lobbyUI == null)
        {
            Debug.Log("현재 로비가 아닌데 로비에 입장하려고 합니다.");
            return;
        }
        RepeatedField<RoomInfo> roomInfoList = new RepeatedField<RoomInfo>();
        roomInfoList.Add(addRoomPacket.RoomInfo);
        lobbyUI.AddRoom(roomInfoList);

        int roomInfoListCount = roomInfoList.Count;
        // 내가 방 주인인지 확인 후 바로 입장
        if (roomInfoListCount == 1 && roomInfoList[0].RoomOwnerId == Managers.WaitingRoomObject.UserId)
        {
            Managers.WaitingRoom.EnterRoom(roomInfoList[0]);
        }

    }

    public static void S_RemoveRoomHandler(PacketSession session, IMessage packet)
    {
        // UI 찾는게 더 무겁고 패킷 캐스팅이 더 가벼우니 패킷 먼저 체크
        S_RemoveRoom removeRoomPacket = packet as S_RemoveRoom;
        if (removeRoomPacket == null)
        {
            Debug.Log("S_RemoveRoom 패킷이 null입니다");
            return;
        }

        UI_Lobby lobbyUI = Managers.UI.CurrentScene.GetComponent<UI_Lobby>();
        if (lobbyUI == null)
        {
            Debug.Log("현재 로비가 아닌데 로비에 입장하려고 합니다.");
            return;
        }

        lobbyUI.RemoveRoom(removeRoomPacket.RoomId);
    }

    public static void S_ExitRoomHandler(PacketSession session, IMessage packet)
    {
        S_ExitRoom exitRoomPacket = packet as S_ExitRoom;
        if (exitRoomPacket == null)
        {
            Debug.Log("S_ExitRoom 패킷이 null입니다");
            return;
        }

        Managers.WaitingRoom.ExitRoom();
    }

    // 유저가 로비에 있으면 방 정보 갱신
    // 유저가 대기방에 있으면 본인 방의 정보 갱신
    public static void S_UpdateWaitingRoomInfoHandler(PacketSession session, IMessage packet)
    {
        S_UpdateWaitingRoomInfo updateWatingRoomInfoPacket = packet as S_UpdateWaitingRoomInfo;
        if (updateWatingRoomInfoPacket == null)
        {
            Debug.Log("S_UpdateWaitingRoomInfo 패킷이 null입니다");
            return;
        }

        if (Managers.Scene.CurrentScene == Define.Scene.Lobby)
        {
            UI_Lobby lobbyUI = Managers.UI.CurrentScene.GetComponent<UI_Lobby>();
            if (lobbyUI == null)
            {
                Debug.Log("현재 로비가 아닌데 로비에 입장하려고 합니다.");
                return;
            }
            lobbyUI.UpdateRoomInfo(updateWatingRoomInfoPacket.RoomInfo);
        }
        else if (Managers.Scene.CurrentScene == Define.Scene.WaitingRoom)
        {
            Managers.WaitingRoom.RoomInfo = updateWatingRoomInfoPacket.RoomInfo;
        }
    }

    public static void S_StartGameHandler(PacketSession session, IMessage packet)
    {
        S_StartGame startGamePacket = packet as S_StartGame;
        if (startGamePacket == null)
        {
            Debug.Log("S_StartGame 패킷이 null입니다");
            return;
        }
        if (Managers.Scene.CurrentScene != Define.Scene.WaitingRoom)
        {
            Managers.UI.ShowToastPopup("현재 대기방에 있지 않아 게임에 입장할 수 없습니다.");
            return;
        }
        
        // 대기방의 정보 그대로 넘겨주고 서버에서 받은 유저 아이디 리스트 넘겨줌
        Managers.GameRoom.EnterGame(Managers.WaitingRoom.RoomInfo, startGamePacket.PlayerIdList);
    }

    // 내가 게임에 입장할 때 패킷
    public static void S_EnterGameHandler(PacketSession session, IMessage packet)
    {
        S_EnterGame enterGamePacket = packet as S_EnterGame;
        if (enterGamePacket == null)
        {
            Debug.Log("S_EnterGame 패킷이 null입니다");
            return;
        }
        
        // 처음 입장했다는 걸 서버에게 알려서 카운트다운 패킷 받을 준비 하기
        Managers.GameRoomObject.Add(enterGamePacket.ObjectState, isMyPlayer: true);
        C_StartCountdown startContdownPacket = new C_StartCountdown();
        Managers.Network.Send(startContdownPacket);
    }

    public static void S_AttackHandler(PacketSession session, IMessage packet)
    {
        S_Attack attackPacket = packet as S_Attack;
        if (attackPacket == null)
        {
            Debug.Log("S_Attack 패킷이 null입니다");
            return;
        }

        foreach (DamagedInfo damagedInfo in attackPacket.DamagedObjectList)
        {
            GameObject go = Managers.GameRoomObject.FindById(damagedInfo.ObjectId);
            if (go == null)
                continue;
            
            CreatureController cc = go.GetComponent<CreatureController>();
            if (cc == null)
                continue;

            cc.OnDamaged(damagedInfo.RemainHp);
        }
    }

    // 게임에서 죽었을 때
    public static void S_LeaveGameHandler(PacketSession session, IMessage packet)
    {
        S_LeaveGame leaveGamePacket = packet as S_LeaveGame;
        
        // 커서 잠금 풀기
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Managers.UI.ShowCountdown(
            Managers.GameRoom.GameOverPopupDelayTime, 
            () => Managers.GameRoom.OnShowGameOverPopup(leaveGamePacket.RoomExitReason),
            isHideCountdownText: false);
    }

    public static void S_SpawnHandler(PacketSession session, IMessage packet)
    {
        S_Spawn spawnPacket = packet as S_Spawn;

        if (Managers.Scene.CurrentScene == Define.Scene.WaitingRoom)
        {
            foreach (ObjectState objectState in spawnPacket.ObjectStateList)
            {
                Managers.WaitingRoomObject.Add(objectState, isMyPlayer: false);
            }
        }
        else if (Managers.Scene.CurrentScene == Define.Scene.GameRoom)
        {
            foreach (ObjectState objectState in spawnPacket.ObjectStateList)
            {
                Managers.GameRoomObject.Add(objectState, isMyPlayer: false);
            }
        }
    }

    public static void S_DespawnHandler(PacketSession session, IMessage packet)
    {
        S_Despawn despawnPacket = packet as S_Despawn;

        if (Managers.Scene.CurrentScene == Define.Scene.WaitingRoom)
        {
            foreach (int id in despawnPacket.ObjectIdList)
            {
                Managers.WaitingRoomObject.Remove(id);
            }
        }
        else if (Managers.Scene.CurrentScene == Define.Scene.GameRoom)
        {
            foreach (int id in despawnPacket.ObjectIdList)
            {
                Managers.GameRoomObject.Remove(id, isDead: false);
            }
        }
    }

    public static void S_MoveHandler(PacketSession session, IMessage packet)
    {
        S_Move movePacket = packet as S_Move;

        // 움직임 동기화 시킬 오브젝트 찾기
        GameObject go = null;
        if (Managers.Scene.CurrentScene == Define.Scene.WaitingRoom)
        {
            go = Managers.WaitingRoomObject.FindById(movePacket.ObjectState.ObjectId);
        }
        else if (Managers.Scene.CurrentScene == Define.Scene.GameRoom)
        {
            go = Managers.GameRoomObject.FindById(movePacket.ObjectState.ObjectId);
        }
        if (go == null)
        {
            Debug.Log($"Cant find GameObject {movePacket.ObjectState.ObjectId}");
            return;
        }

        CreatureController cc = go.GetComponent<CreatureController>();
        if (cc == null)
        {
            Debug.Log("Cant find CreatureController");
            return;
        }

        cc.ObjectState.Position = movePacket.ObjectState.Position;
        cc.ObjectState.Rotation = movePacket.ObjectState.Rotation;
        cc.ObjectState.Velocity = movePacket.ObjectState.Velocity;
        cc.ObjectState.CreatureState = movePacket.ObjectState.CreatureState;

        GameObjectType type = GameObjectType.None;
        if (Managers.Scene.CurrentScene == Define.Scene.WaitingRoom)
        {
            type = Managers.WaitingRoomObject.GetObjectTypeById(cc.Id);
        }
        else if (Managers.Scene.CurrentScene == Define.Scene.GameRoom)
        {
            type = Managers.GameRoomObject.GetObjectTypeById(cc.Id);
        }

        if (type == GameObjectType.Player)
        {
            OtherPlayerController otherPlayer = go.GetComponent<OtherPlayerController>();
            if (otherPlayer != null)
            {
                otherPlayer.SetServerState(
                    movePacket.ObjectState.Position,
                    movePacket.ObjectState.Rotation,
                    movePacket.ObjectState.Velocity,
                    movePacket.ObjectState.ServerReceivedTime
                );
            }
        }
        else if (type == GameObjectType.Monster)
        {
            MonsterController monster = go.GetComponent<MonsterController>();
            if (monster != null)
            {
                monster.SetServerState(
                    movePacket.ObjectState.Position,
                    movePacket.ObjectState.Rotation,
                    movePacket.ObjectState.Velocity,
                    movePacket.ObjectState.ServerReceivedTime
                );
            }
        }
    }

    public static void S_DieHandler(PacketSession session, IMessage packet)
    {
        S_Die diePacket = packet as S_Die;
        // 죽은 오브젝트 상태 변경
        GameObject go = Managers.GameRoomObject.FindById(diePacket.DamagedObjectId);
        if (go == null)
        {
            Debug.Log($"Id: {diePacket.DamagedObjectId}가 존재하지 않음");
            return;
        }
        CreatureController cc = go.GetComponent<CreatureController>();
        if (cc == null)
        {
            Debug.Log($"Id: {diePacket.DamagedObjectId}의 CreatureController가 존재하지 않음");
            return;
        }
        cc.CreatureState = diePacket.CreatureState;
        Managers.GameRoomObject.Remove(diePacket.DamagedObjectId, isDead: true);
        cc.OnDead();
    }

    public static void S_StartCountdownHandler(PacketSession session, IMessage packet)
    {
        S_StartCountdown startCountdownPacket = packet as S_StartCountdown;
        Managers.GameRoom.StartGame(startCountdownPacket.GameStartCountdownTime);
    }

    public static void S_TimestampHandler(PacketSession session, IMessage packet)
    {
        S_Timestamp sereverTimestamp = packet as S_Timestamp;
        Managers.Network.CalculateTimeOffset(sereverTimestamp.ClientSendTime, sereverTimestamp.ServerReceivedTime);
    }

    public static void S_ChangeCreatureStateHandler(PacketSession session, IMessage packet)
    {
        S_ChangeCreatureState changeCreatureStatePacket = packet as S_ChangeCreatureState;
        Managers.GameRoomObject.HandleChangeCreatureState(changeCreatureStatePacket.ObjectId, changeCreatureStatePacket.CreatureState);
    }

    public static void S_ConnectedHandler(PacketSession session, IMessage packet)
    {
        S_Connected connectedPacket = packet as S_Connected;
        // TODO - 연결 성공 처리
        //Managers.UI.ShowToastPopup("서버와 연결되었습니다.");
    }

    public static void S_LoginHandler(PacketSession session, IMessage packet)
    {
        S_Login loginPacket = packet as S_Login;
        if (loginPacket == null)
        {
            Debug.Log("S_Login 패킷이 null입니다");
            return;
        }

        UI_Login loginUI = Managers.UI.CurrentScene.GetComponent<UI_Login>();
        if (loginUI == null)
        {
            Debug.Log("현재 로그인 화면이 아닌데 로그인을 시도하려고 합니다.");
            return;
        }

        loginUI.HandleLogin(loginPacket.LoginStatus);
    }

    public static void S_RequestPlayerListHandler(PacketSession session, IMessage packet)
    {
        S_RequestPlayerList requestPlayerListPacket = packet as S_RequestPlayerList;
        if (requestPlayerListPacket == null)
        {
            Debug.Log("S_RequestPlayerList 패킷이 null입니다");
            return;
        }

        UI_PlayerSelect playerSelectUI = Managers.UI.CurrentScene.GetComponent<UI_PlayerSelect>();
        if (playerSelectUI == null)
        {
            Debug.Log("현재 캐릭터 선택창이 아닌데 캐릭터 선택을 하려고 합니다.");
            return;
        }

        playerSelectUI.UpdatePlayerInfos(requestPlayerListPacket.PlayerInfoList);
    }

    public static void S_CreatePlayerHandler(PacketSession session, IMessage packet)
    {
        S_CreatePlayer createPlaerPacket = packet as S_CreatePlayer;
        if (createPlaerPacket == null)
        {
            Debug.Log("S_CreatePlayer 패킷이 null입니다");
            return;
        }

        UI_PlayerSelect playerSelectUI = Managers.UI.CurrentScene.GetComponent<UI_PlayerSelect>();
        if (playerSelectUI == null)
        {
            Debug.Log("현재 캐릭터 선택창이 아닌데 캐릭터 선택을 하려고 합니다.");
            return;
        }

        if (createPlaerPacket.CanCreate == false)
        {
            Managers.UI.ShowToastPopup("중복 닉네임이 존재하여 캐릭터를 생성할 수 없습니다");
            return;
        }

        playerSelectUI.UpdatePlayerInfos(createPlaerPacket.PlayerInfoList);
    }

    public static void S_DeletePlayerHandler(PacketSession session, IMessage packet)
    {
        S_DeletePlayer deletePlaerPacket = packet as S_DeletePlayer;
        if (deletePlaerPacket == null)
        {
            Debug.Log("S_DeletePlayer 패킷이 null입니다");
            return;
        }

        UI_PlayerSelect playerSelectUI = Managers.UI.CurrentScene.GetComponent<UI_PlayerSelect>();
        if (playerSelectUI == null)
        {
            Debug.Log("현재 캐릭터 선택창이 아닌데 캐릭터 삭제를 하려고 합니다.");
            return;
        }

        if (deletePlaerPacket.CanDelete == false)
        {
            Managers.UI.ShowToastPopup("캐릭터 삭제에 실패했습니다. 다시 시도해주세요.");
            return;
        }

        Managers.UI.ShowToastPopup("캐릭터가 삭제되었습니다.");
        playerSelectUI.UpdatePlayerInfos(deletePlaerPacket.PlayerInfoList);
    }

    public static void S_UpdateCurrencyDataHandler(PacketSession session, IMessage packet)
    {
        S_UpdateCurrencyData updateCurrencyDataPacket = packet as S_UpdateCurrencyData;
        if (updateCurrencyDataPacket == null)
        {
            Debug.Log("S_UpdateCurrencyData 패킷이 null입니다");
            return;
        }

        Managers.Currency.UpdateCurrencyData(updateCurrencyDataPacket.CurrencyType, updateCurrencyDataPacket.Amount);
    }

    public static void S_UpdateCurrencyDataAllHandler(PacketSession session, IMessage packet)
    {
        S_UpdateCurrencyDataAll updateCurrencyDataAllPacket = packet as S_UpdateCurrencyDataAll;
        if (updateCurrencyDataAllPacket == null || updateCurrencyDataAllPacket.CurrencyData == null)
        {
            Debug.Log("S_UpdateCurrencyDataAll 패킷이 null입니다");
            return;
        }

        Managers.Currency.UpdateCurrencyDataAll(updateCurrencyDataAllPacket.CurrencyData);
    }
}
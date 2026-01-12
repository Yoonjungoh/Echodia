using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Lobby : UI_Scene
{
    enum Buttons
    {
        AddRoomButton,
    }

    private GameObject _roomScrollView;
    private GameObject _userScrollView;
    Dictionary<int, Lobby_RoomSubItem> _roomSubItemDict = new Dictionary<int, Lobby_RoomSubItem>();
    Dictionary<int, Lobby_UserSubItem> _userSubItemDict = new Dictionary<int, Lobby_UserSubItem>();

    public override void Init()
    {
        base.Init();
        
        Bind<Button>(typeof(Buttons));
        GetButton((int)Buttons.AddRoomButton).onClick.AddListener(OnClickAddRoomButton);

        _roomScrollView = Util.FindChild(gameObject, "RoomContent", recursive: true);
        _userScrollView = Util.FindChild(gameObject, "UserContent", recursive: true);

        C_EnterLobby enterLobbyPacket = new C_EnterLobby();
        enterLobbyPacket.PlayerId = Managers.Lobby.MyPlayer.PlayerId;
        Managers.Network.Send(enterLobbyPacket);

        //// 로비에 재진입 시 갱신 패킷 전송
        //if (Managers.Lobby.IsEnterFirst == true)
        //{
        //    C_EnterLobby enterLobbyPacket = new C_EnterLobby();
        //    enterLobbyPacket.PlayerId = Managers.Lobby.MyPlayer.PlayerId;
        //    Managers.Network.Send(enterLobbyPacket);
        //}

        //Managers.Lobby.IsEnterFirst = true; // 로비 UI 초기화 시에 처음 들어왔다고 판단
    }

    public void EnterLobby(RepeatedField<int> userIdList, RepeatedField<string> userNameList)
    {
        // 유저 아이디 리스트와 유저 이름 리스트의 개수가 다르면 잘못된 패킷이므로 처리하지 않음
        if (userIdList.Count != userNameList.Count)
            return;

        int userIdListCount = userIdList.Count;
        for (int i = 0; i < userIdListCount; i++)
        {
            if (_userSubItemDict.ContainsKey(userIdList[i])) continue;

            Lobby_UserSubItem lobbyUserSubItem = Managers.UI.MakeSubItem<Lobby_UserSubItem>(_userScrollView.transform);
            lobbyUserSubItem.SetData(new LobbyUserSubItemData
            {
                UserId = userIdList[i],
                UserName = userNameList[i]
            });
            _userSubItemDict.TryAdd(userIdList[i], lobbyUserSubItem);
        }
    }

    public void LeaveLobby(int userId)
    {
        if (_userSubItemDict.ContainsKey(userId) == false)
        {
            Debug.Log($"UserId: {userId}가 로비에 존재하지 않습니다.");
            return;
        }
        Lobby_UserSubItem lobbyUserSubItem = null;
        _userSubItemDict.TryGetValue(userId, out lobbyUserSubItem);
        if (lobbyUserSubItem == null)
        {
            Debug.Log($"UserId: {userId}의 SubItem이 로비에 존재하지 않습니다.");
            return;
        }
        Destroy(lobbyUserSubItem.gameObject);
        _userSubItemDict.Remove(userId);
    }

    public void OnClickAddRoomButton()
    {
        UI_AddRoom addRoomUI = Managers.UI.ShowPopupUI<UI_AddRoom>();
        addRoomUI.SetData(new AddRoomPopupData
        {
            RoomName = string.Empty
        });
    }

    public void AddRoom(RepeatedField<RoomInfo> roomInfoList)
    {
        int roomInfoListCount = roomInfoList.Count;

        for (int i = 0; i < roomInfoListCount; i++)
        {
            if (_roomSubItemDict.ContainsKey(roomInfoList[i].RoomId))
            {
                Debug.Log($"같은 RoomId가 이미 존재합니다. RoomId: {roomInfoList[i].RoomId}, RoomName: {roomInfoList[i].RoomName}");
                return;
            }
            
            Lobby_RoomSubItem lobbyRoomSubItem = Managers.UI.MakeSubItem<Lobby_RoomSubItem>(_roomScrollView.transform);
            lobbyRoomSubItem.SetData(new LobbyRoomSubItemData
            {
                RoomId = roomInfoList[i].RoomId,
                RoomName = roomInfoList[i].RoomName,
                RoomOwnerId = roomInfoList[i].RoomOwnerId,
                CurrentPlayerCount = roomInfoList[i].CurrentPlayerCount,
                MaxPlayerCount = roomInfoList[i].MaxPlayerCount,
            });
            _roomSubItemDict.TryAdd(roomInfoList[i].RoomId, lobbyRoomSubItem);
        }
    }

    public void RemoveRoom(int roomId)
    {
        if (_roomSubItemDict.ContainsKey(roomId) == false)
            return;

        _roomSubItemDict.TryGetValue(roomId, out Lobby_RoomSubItem room);
        Destroy(room.gameObject);
        _roomSubItemDict.Remove(roomId);
    }
    
    public void UpdateRoomInfo(RoomInfo roomInfo)
    {
        if (_roomSubItemDict.ContainsKey(roomInfo.RoomId) == false)
            return;
        
        _roomSubItemDict.TryGetValue(roomInfo.RoomId, out Lobby_RoomSubItem room);
        room.SetData(new LobbyRoomSubItemData
        {
            RoomId = roomInfo.RoomId,
            RoomName = roomInfo.RoomName,
            CurrentPlayerCount = roomInfo.CurrentPlayerCount,
            MaxPlayerCount = roomInfo.MaxPlayerCount,
        });
    }
}

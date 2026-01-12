using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct LobbyRoomSubItemData
{
    public int RoomId;
    public string RoomName;
    public int RoomOwnerId;
    public int CurrentPlayerCount;
    public int MaxPlayerCount;
}

public class Lobby_RoomSubItem : UI_SubItem<LobbyRoomSubItemData>
{
    enum Buttons
    {
        EnterRoomButton,
    }

    enum Texts
    {
        RoomNameText,
        PlayerCountText,
    }

    public override void Init()
    {
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));
        
        GetButton((int)Buttons.EnterRoomButton).onClick.AddListener(OnClickEnterRoom);
    }

    private void OnClickEnterRoom()
    {
        // 방 입장 가능 인원 초과
        bool canEnterWaitingRoom = (_data.CurrentPlayerCount < _data.MaxPlayerCount);
        if (canEnterWaitingRoom == false)
        {
            Managers.UI.ShowToastPopup("현재 방에 입장 가능 인원을 초과했습니다.");
            return;
        }

        UI_Confirm confirmUI = Managers.UI.ShowPopupUI<UI_Confirm>();
        confirmUI.SetData(new ConfirmPopupData
        {
            RoomId = _data.RoomId,
            RoomName = _data.RoomName,
            RoomOwnerId = _data.RoomOwnerId,
            CurrentPlayerCount = _data.CurrentPlayerCount,
            MaxPlayerCount = _data.MaxPlayerCount,
        });
    }
    
    public override void SetData(LobbyRoomSubItemData data)
    {
        base.SetData(data);
        UpdateUI();
    }

    protected override void UpdateUI()
    {
        GetTextMeshProUGUI((int)Texts.RoomNameText).text = _data.RoomName;
        GetTextMeshProUGUI((int)Texts.PlayerCountText).text = $"{_data.CurrentPlayerCount} / {_data.MaxPlayerCount}";
    }
}

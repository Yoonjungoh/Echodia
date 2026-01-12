using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct ConfirmPopupData
{
    public int RoomId;
    public string RoomName;
    public int RoomOwnerId;
    public int CurrentPlayerCount;
    public int MaxPlayerCount;
}

public class UI_Confirm : UI_Popup<ConfirmPopupData>
{
    enum Buttons
    {
        ConfirmButton,
        CancelButton,
    }

    enum Texts
    {
        RoomNameText,
    }

    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetButton((int)Buttons.ConfirmButton).onClick.AddListener(OnClickConfirmButton);
        GetButton((int)Buttons.CancelButton).onClick.AddListener(OnClickCancelButton);
    }

    private void OnClickConfirmButton()
    {
        RoomInfo roomInfo = new RoomInfo();
        roomInfo.RoomId = _data.RoomId;
        roomInfo.RoomName = _data.RoomName;
        roomInfo.RoomOwnerId = _data.RoomOwnerId;
        roomInfo.CurrentPlayerCount = _data.CurrentPlayerCount;
        roomInfo.MaxPlayerCount = _data.MaxPlayerCount;
        
        Managers.WaitingRoom.EnterRoom(roomInfo);
        ClosePopupUI();
    }

    private void OnClickCancelButton()
    {
        ClosePopupUI();
    }

    public override void SetData(ConfirmPopupData data)
    {
        base.SetData(data);
        UpdateUI();
    }

    protected override void UpdateUI()
    {
        GetTextMeshProUGUI((int)Texts.RoomNameText).text = $"{_data.RoomName}에 입장하시겠습니까?";
    }
}

using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct AddRoomPopupData
{
    public string RoomName;
}

public class UI_AddRoom : UI_Popup<AddRoomPopupData>
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
        _data.RoomName = GetTextMeshProUGUI((int)Texts.RoomNameText).text;
        Debug.Log($"방 이름: {_data.RoomName}");

        // 서버에 방 생성 패킷 전송
        C_AddRoom addRoomPacket = new C_AddRoom();
        addRoomPacket.RoomName = _data.RoomName;
        Managers.Network.Send(addRoomPacket);
        
        ClosePopupUI();
    }

    private void OnClickCancelButton()
    {
        ClosePopupUI();
    }
    
    public override void SetData(AddRoomPopupData data)
    {
        base.SetData(data);
        UpdateUI();
    }
    
    protected override void UpdateUI()
    {

    }
}

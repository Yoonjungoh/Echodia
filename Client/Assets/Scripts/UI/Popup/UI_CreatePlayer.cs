using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_CreatePlayer : UI_Popup
{
    enum Buttons
    {
        ConfirmButton,
        CancelButton,
    }

    enum Texts
    {
        PlayerNameText,
    }

    private TextMeshProUGUI _playerNameText;

    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetButton((int)Buttons.ConfirmButton).onClick.AddListener(OnClickConfirmButton);
        GetButton((int)Buttons.CancelButton).onClick.AddListener(OnClickCancelButton);

        _playerNameText = GetTextMeshProUGUI((int)Texts.PlayerNameText);
    }

    private void OnClickConfirmButton()
    {
        // 서버에 방 생성 패킷 전송
        C_CreatePlayer createPlayerPacket = new C_CreatePlayer();
        createPlayerPacket.Name = _playerNameText.text;
        Managers.Network.Send(createPlayerPacket);
        
        ClosePopupUI();
    }

    private void OnClickCancelButton()
    {
        ClosePopupUI();
    }
}

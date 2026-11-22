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

    enum InputFields
    {
        PlayerNameInputField,
    }

    private TMP_InputField _nameInputField;

    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));
        Bind<TMP_InputField>(typeof(InputFields));

        GetButton((int)Buttons.ConfirmButton).onClick.AddListener(OnClickConfirmButton);
        GetButton((int)Buttons.CancelButton).onClick.AddListener(OnClickCancelButton);

        _nameInputField = Get<TMP_InputField>((int)InputFields.PlayerNameInputField);
    }

    private void OnClickConfirmButton()
    {
        // 서버에 방 생성 패킷 전송
        C_CreatePlayer createPlayerPacket = new C_CreatePlayer();
        createPlayerPacket.Name = _nameInputField.text;
        Managers.Network.Send(createPlayerPacket);
        
        ClosePopupUI();
    }

    private void OnClickCancelButton()
    {
        ClosePopupUI();
    }
}

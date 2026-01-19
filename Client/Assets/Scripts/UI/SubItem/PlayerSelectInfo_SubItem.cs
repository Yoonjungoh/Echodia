using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSelectInfo_SubItem : UI_SubItem<PlayerSelectInfo>
{
    enum Texts
    {
        PlayerIdText,
        PlayerNameText,
        JewelText,
        GoldText,
    }

    enum Buttons
    {
        SelectButton,
        DeleteButton,
    }

    public override void Init()
    {
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Button>(typeof(Buttons));

        GetButton((int)Buttons.SelectButton).onClick.AddListener(OnClickSelectButton);
        GetButton((int)Buttons.DeleteButton).onClick.AddListener(OnClickDeleteButton);
    }

    private void OnClickSelectButton()
    {
        if (_data == null)
        {
            Managers.UI.ShowToastPopup("플레이어 정보를 불러올 수 없습니다");
            return;
        }
        
        Managers.Scene.LoadScene(Define.Scene.GameRoom);
    }

    private void OnClickDeleteButton()
    {
        // 해당 캐릭터 삭제 요청
        C_DeletePlayer deletePlayerPacket = new C_DeletePlayer();
        deletePlayerPacket.PlayerId = _data.PlayerId;
        Managers.Network.Send(deletePlayerPacket);
    }

    public override void SetData(PlayerSelectInfo data)
    {
        base.SetData(data);
        UpdateUI();
    }

    protected override void UpdateUI()
    {
        GetTextMeshProUGUI((int)Texts.PlayerIdText).text = $"UId: {_data.PlayerId}";
        GetTextMeshProUGUI((int)Texts.PlayerNameText).text = $"닉네임: {_data.Name}";
        GetTextMeshProUGUI((int)Texts.JewelText).text = $"보석: {_data.CurrencyData.Jewel}";
        GetTextMeshProUGUI((int)Texts.GoldText).text = $"골드: {_data.CurrencyData.Gold}";
    }
}

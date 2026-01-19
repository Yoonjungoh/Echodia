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

    private bool _isSelected = false;

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

        if (_isSelected)
        {
            Managers.UI.ShowToastPopup("서버에서 입장 처리 중입니다");
            return;
        }
        
        _isSelected = true;
        // 교차 검증을 위해 서버, 채널 정보도 함께 보냄
        C_SelectPlayer selectPlayerPacket = new C_SelectPlayer()
        {
            PlayerId = _data.PlayerId,
            ServerId = Managers.GameRoom.ServerId,
            ChannelId = Managers.GameRoom.ChannelId,
        };
        Managers.Network.Send(selectPlayerPacket);
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

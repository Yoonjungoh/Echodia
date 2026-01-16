using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ServerChannel_SubItem : UI_SubItem<ServerInfo>
{
    enum Texts
    {
        ChannelIdText,
        PlayerCountText,
    }

    enum Buttons
    {
        SelectButton,
    }

    public override void Init()
    {
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Button>(typeof(Buttons));

        GetButton((int)Buttons.SelectButton).onClick.AddListener(OnClickSelectButton);
    }

    private void OnClickSelectButton()
    {
        // 해당 플레이어로 세팅 후, 로비로 이동
        //Managers.Lobby.SetSelectedPlayerInfo(_data);
        if (_data == null)
        {
            Managers.UI.ShowToastPopup("플레이어 정보를 불러올 수 없습니다");
            return;
        }
        
        Managers.Scene.LoadScene(Define.Scene.PlayerSelect);
    }

    public override void SetData(ServerInfo data)
    {
        base.SetData(data);
        UpdateUI();
    }

    protected override void UpdateUI()
    {
        GetTextMeshProUGUI((int)Texts.ChannelIdText).text = $"{_data.ServerName}.{_data.ChannelId}";
        GetTextMeshProUGUI((int)Texts.PlayerCountText).text = $"{_data.CurrentPlayerCount} / {_data.MaxPlayerCount}";
    }
}

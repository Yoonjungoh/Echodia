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
        if (_data == null)
        {
            Managers.UI.ShowToastPopup("플레이어 정보를 불러올 수 없습니다");
            return;
        }
        // 선택한 서버랑, 채널 Id 저장
        // TODO - 후에 혼잡도에 따라서 못 들어오게 해야할듯
        Managers.GameRoom.SetMapData(_data.ServerId, _data.ChannelId);
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

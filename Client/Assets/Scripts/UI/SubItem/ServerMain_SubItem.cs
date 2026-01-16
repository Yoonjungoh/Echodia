using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public struct ServerMainSubItemData
{
    public int ServerId;
    public string ServerName;
}

public class ServerMain_SubItem : UI_SubItem<ServerMainSubItemData>
{
    public Action OnClickSelectButtonAction;

    enum Texts
    {
        ServerNameText,
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
        OnClickSelectButtonAction.Invoke();
    }

    public override void SetData(ServerMainSubItemData data)
    {
        base.SetData(data);
        UpdateUI();
    }

    protected override void UpdateUI()
    {
        GetTextMeshProUGUI((int)Texts.ServerNameText).text = _data.ServerName;
    }
}

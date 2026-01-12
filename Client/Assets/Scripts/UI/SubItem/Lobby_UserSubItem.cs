using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct LobbyUserSubItemData
{
    public int UserId;
    public string UserName;
}

public class Lobby_UserSubItem : UI_SubItem<LobbyUserSubItemData>
{
    enum Texts
    {
        UserNameText,
    }

    public override void Init()
    {
        Bind<TextMeshProUGUI>(typeof(Texts));
    }


    public override void SetData(LobbyUserSubItemData data)
    {
        base.SetData(data);
        UpdateUI();
    }

    protected override void UpdateUI()
    {
        GetTextMeshProUGUI((int)Texts.UserNameText).text = _data.UserName;
    }
}

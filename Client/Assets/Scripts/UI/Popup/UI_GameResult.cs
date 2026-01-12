using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct GameResultPopupData
{
    public string GameResultText;
}

public class UI_GameResult : UI_Popup<GameResultPopupData>
{
    enum Buttons
    {
        CloseButton,
    }

    enum Texts
    {
        GameResultText,
    }

    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));
        
        GetButton((int)Buttons.CloseButton).onClick.AddListener(OnClickCloseButton);
    }

    private void OnClickCloseButton()
    {
        Managers.GameRoom.ExitGame();
        ClosePopupUI();
    }


    public override void SetData(GameResultPopupData data)
    {
        base.SetData(data);
        UpdateUI();
    }

    protected override void UpdateUI()
    {
        GetTextMeshProUGUI((int)Texts.GameResultText).text = _data.GameResultText;
    }
}

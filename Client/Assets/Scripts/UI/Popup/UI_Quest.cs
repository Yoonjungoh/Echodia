using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Quest : UI_Popup
{
    enum Buttons
    {
        ExitButton,
        BackgroundButton,
    }

    enum Texts
    {
        
    }
    
    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        InitQuestData();
    }

    private void InitQuestData()
    {
        
    }

    private void OnClickExitButton()
    {
        ClosePopupUI();    
    }

    private void OnClickBackgroundButton()
    {
        ClosePopupUI();    
    }

}

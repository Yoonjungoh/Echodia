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
        QuestMainTitleText,
        QuestSubTitleText,
        QuestDescriptionText,

    }

    enum Transforms
    {
        QuestContent,
        QuestRewardContent,
    }

    private Transform _questContent;
    private Transform _questRewardContent;
    
    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Transform>(typeof(Transforms));


        _questContent = Get<Transform>((int)Transforms.QuestContent);
        _questRewardContent = Get<Transform>((int)Transforms.QuestRewardContent);
        
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

using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Quest_SubItem : UI_SubItem<QuestDefinitionMetaData>
{
    public Action OnClickSelectButtonAction;

    enum Images
    {
        QuestImage,
    }

    enum Texts
    {
        QuestTitleText,
    }
    
    enum Buttons
    {
        SelectButton,
    }

    

    public override void Init()
    {
        Bind<Image>(typeof(Images));
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Button>(typeof(Buttons));

        GetButton((int)Buttons.SelectButton).onClick.AddListener(OnClickSelectButton);
    }

    private void OnClickSelectButton()
    {
        OnClickSelectButtonAction.Invoke();
    }

    public override void SetData(QuestDefinitionMetaData data)
    {
        base.SetData(data);
        UpdateUI();
    }

    protected override void UpdateUI()
    {
        GetImage((int)Images.QuestImage) = Managers.Image.GetQuestImage()
    }
}

using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Quest_SubItem : UI_SubItem<QuestInfo>
{

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

    public Action OnClickSelectButtonAction;
    private QuestDefinitionMetaData _mainQuestData;

    public override void Init()
    {
        Bind<Image>(typeof(Images));
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Button>(typeof(Buttons));

        GetButton((int)Buttons.SelectButton).onClick.AddListener(OnClickSelectButton);
    }

    private void OnClickSelectButton()
    {
        // UI_Quest에게 이벤트 전달
        OnClickSelectButtonAction.Invoke();
    }

    public override void SetData(QuestInfo data)
    {
        base.SetData(data);
        _mainQuestData = Managers.SpecData.GetQuestDefinition(_data.MainQuestId);
        
        UpdateUI();
    }

    protected override void UpdateUI()
    {
        GetImage((int)Images.QuestImage).sprite = Managers.Image.GetQuestImage(_data.MainQuestId, _data.SubQuestId);
        GetTextMeshProUGUI((int)Texts.QuestTitleText).text = $"{_mainQuestData.MainQuestId}-{_data.SubQuestId}: {_mainQuestData.Title}";
    }
}

using System.Collections;
using System.Collections.Generic;
using Google.Protobuf.Protocol;
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
    private QuestObjectiveDefinitionMetaData _selectedQuest;

    private TextMeshProUGUI _questMainTitleText;
    private TextMeshProUGUI _questSubTitleText;
    private TextMeshProUGUI _questDescriptionText;

    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Transform>(typeof(Transforms));

        _questContent = Get<Transform>((int)Transforms.QuestContent);
        _questRewardContent = Get<Transform>((int)Transforms.QuestRewardContent);

        InitQuestData();
        UpdateUI();
    }

    // 갖고 있는 퀘스트가 최신화 될 때마다 호출해줘야 함
    public void UpdateUI()
    {
        if (_selectedQuest == null)
            return;

        QuestDefinitionMetaData mainQuestData = Managers.SpecData.GetQuestDefinition(_selectedQuest.MainQuestId);
        _questMainTitleText.text = $"{mainQuestData.Title} {_selectedQuest.MainQuestId}-{_selectedQuest.SubQuestId}";
        _questSubTitleText.text = $"Lv.{mainQuestData.ReqLevel} Quest";
        _questDescriptionText.text = _selectedQuest.Description;
    }

    private void InitQuestData()
    {
        // TODO - DB에 갖고 있는 모든 퀘스트 요청하기
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

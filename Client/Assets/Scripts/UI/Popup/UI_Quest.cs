using System;
using System.Collections;
using System.Collections.Generic;
using Google.Protobuf.Collections;
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
        AcceptButton,
        CompleteButton,
        AbandonButton,
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
    // key = mainQuestId, subQuestId
    private Dictionary<ValueTuple<int, int>, QuestInfo> _userQuestDataDict = new Dictionary<(int, int), QuestInfo>();

    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Transform>(typeof(Transforms));

        GetButton((int)Buttons.AcceptButton).onClick.AddListener(OnClickAcceptButton);
        GetButton((int)Buttons.CompleteButton).onClick.AddListener(OnClickCompleteButton);
        GetButton((int)Buttons.AbandonButton).onClick.AddListener(OnClickAbandonButton);
        GetButton((int)Buttons.ExitButton).onClick.AddListener(OnClickExitButton);
        GetButton((int)Buttons.BackgroundButton).onClick.AddListener(OnClickBackgroundButton);

        _questContent = Get<Transform>((int)Transforms.QuestContent);
        _questRewardContent = Get<Transform>((int)Transforms.QuestRewardContent);

        C_RequestQuestData requestQuestData = new C_RequestQuestData();
        Managers.Network.Send(requestQuestData);    // DB 데이터 요청
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

    public void InitQuestData(RepeatedField<QuestInfo> questInfoList)
    {
        int count = questInfoList.Count;
        for (int i = 0; i < count; ++i)
        {
            _userQuestDataDict.TryAdd((questInfoList[i].MainQuestId, questInfoList[i].SubQuestId), questInfoList[i]);
            Debug.Log($"{questInfoList[i].MainQuestId}, {questInfoList[i].SubQuestId}, {questInfoList[i].QuestStatus}, {questInfoList[i].RequiredCount}");
        }

        UpdateUI();
    }

    // 새로운 퀘스트가 와서 업데이트 해야 할 때
    public void AddQuestData(int mainQuestId, int subQuestId)
    {
        QuestObjectiveDefinitionMetaData questData = Managers.SpecData.GetQuestObjectiveDefinition(mainQuestId, subQuestId);
        QuestInfo questInfo = new QuestInfo
        {
            MainQuestId = questData.MainQuestId,
            SubQuestId = questData.SubQuestId,
            RequiredCount = 0,  // 방금 생성 됐으니 0
            QuestStatus = QuestStatus.NotAccepted,
        };
        _userQuestDataDict.TryAdd((mainQuestId, subQuestId), questInfo);

        UpdateUI();
    }

    // 퀘스트 포기, 완료돼서 업데이트 해야 할 때
    public void RemoveQuestData(int mainQuestId, int subQuestId)
    {
        _userQuestDataDict.Remove((mainQuestId, subQuestId));

        UpdateUI();
    }

    private void OnClickAcceptButton()
    {

    }

    private void OnClickCompleteButton()
    {

    }

    private void OnClickAbandonButton()
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

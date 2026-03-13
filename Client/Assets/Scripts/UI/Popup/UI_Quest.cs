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
        ProceedingButton,
        CompleteButton,
        CloseButton,
    }


    enum Texts
    {
        QuestMainTitleText,
        QuestSubTitleText,
        QuestDescriptionText,
        QuestProgressText,
    }

    enum Transforms
    {
        QuestContent,
        QuestRewardContent,
    }

    enum GameObjects
    {
        QuestInfoPanel,
    }

    private Button _acceptButton;
    private Button _proceedingButton;
    private Button _completeButton;

    private Transform _questContent;    // 좌측에 퀘스트 목록 나열
    private Transform _questRewardContent;  // 우측에 퀘스트 보상 나열

    private QuestInfo _selectedQuest;   // 선택된 퀘스트의 유저 정보
    private QuestObjectiveDefinitionMetaData _selectedQuestMetaData;

    private TextMeshProUGUI _questMainTitleText;
    private TextMeshProUGUI _questSubTitleText;
    private TextMeshProUGUI _questDescriptionText;
    private TextMeshProUGUI _questProgressText;
    private GameObject _questInfoPanel;
    // key = mainQuestId, subQuestId
    // 퀘스트는 무조건 메인 퀘스트 기준 오름차순 정렬임
    private SortedDictionary<ValueTuple<int, int>, QuestInfo> _userQuestDataDict = new SortedDictionary<(int, int), QuestInfo>();
    private Dictionary<ValueTuple<int, int>, Quest_SubItem> _questItemDict = new Dictionary<(int, int), Quest_SubItem>();

    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Transform>(typeof(Transforms));
        Bind<GameObject>(typeof(GameObjects));

        _acceptButton = GetButton((int)Buttons.AcceptButton);
        _proceedingButton = GetButton((int)Buttons.ProceedingButton);
        _completeButton = GetButton((int)Buttons.CompleteButton);

        _acceptButton.onClick.AddListener(OnClickAcceptButton);
        _completeButton.onClick.AddListener(OnClickCompleteButton);

        GetButton((int)Buttons.CloseButton).onClick.AddListener(OnClickCloseButton);
        GetButton((int)Buttons.ExitButton).onClick.AddListener(OnClickExitButton);
        GetButton((int)Buttons.BackgroundButton).onClick.AddListener(OnClickBackgroundButton);

        _questContent = Get<Transform>((int)Transforms.QuestContent);
        _questRewardContent = Get<Transform>((int)Transforms.QuestRewardContent);

        _questInfoPanel = Get<GameObject>((int)GameObjects.QuestInfoPanel);

        _questMainTitleText = GetTextMeshProUGUI((int)Texts.QuestMainTitleText);
        _questSubTitleText = GetTextMeshProUGUI((int)Texts.QuestSubTitleText);
        _questDescriptionText = GetTextMeshProUGUI((int)Texts.QuestDescriptionText);
        _questProgressText = GetTextMeshProUGUI((int)Texts.QuestProgressText);

        C_RequestQuestData requestQuestData = new C_RequestQuestData();
        Managers.Network.Send(requestQuestData);    // DB 데이터 요청
    }

    // 갖고 있는 퀘스트가 최신화 될 때마다 호출해줘야 함
    public void UpdateAllUI()
    {
        // 좌측에 퀘스트 목록 나열 처리
        UpdateQuestList();
        // 우측에 퀘스트 정보 처리
        UpdateQuestInfo();
    }

    public void UpdateQuestList()
    {
        // TODO - 서브 아이템 최적화
        foreach (Transform child in _questContent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }
        _questItemDict.Clear();

        foreach (var keyValuePair in _userQuestDataDict)
        {
            ValueTuple<int, int> key = keyValuePair.Key;
            QuestInfo questInfo = keyValuePair.Value;
            Quest_SubItem questSubItem = Managers.UI.MakeSubItem<Quest_SubItem>(_questContent);
            questSubItem.SetData(questInfo);
            questSubItem.OnClickSelectButtonAction += () =>
            {
                ChangeSelectedQuest(questInfo);
                UpdateQuestInfo();
            };

            _questItemDict.TryAdd(key, questSubItem);
        }
    }

    private void ChangeSelectedQuest(QuestInfo questInfo)
    {
        _selectedQuest = questInfo;
        _selectedQuestMetaData = Managers.SpecData.GetQuestObjectiveDefinition(_selectedQuest.MainQuestId, _selectedQuest.SubQuestId);
    }

    public void UpdateQuestInfo()
    {
        // 선택된 퀘스트가 없으면 퀘스트 정보 패널 비활성화
        _questInfoPanel.SetActive(_selectedQuest != null);

        if (_selectedQuest == null)
            return;

        QuestDefinitionMetaData mainQuestData = Managers.SpecData.GetQuestDefinition(_selectedQuest.MainQuestId);
        _questMainTitleText.text = $"{mainQuestData.Title} {_selectedQuest.MainQuestId}-{_selectedQuest.SubQuestId}";
        _questSubTitleText.text = $"Lv.{mainQuestData.ReqLevel} Quest";
        _questDescriptionText.text = _selectedQuestMetaData.Description;
        _questProgressText.text = $"({_selectedQuest.RequiredCount}/{_selectedQuestMetaData.RequiredCount})";

        // TODO - 퀘스트 보상 시각화

        // 퀘스트 상태에 따른 버튼 활성화
        _acceptButton.gameObject.SetActive(_selectedQuest.QuestStatus == QuestStatus.NotAccepted);
        _proceedingButton.gameObject.SetActive(_selectedQuest.QuestStatus == QuestStatus.Proceeding);
        _completeButton.gameObject.SetActive(_selectedQuest.QuestStatus == QuestStatus.Completed);
    }

    public void InitQuestData(RepeatedField<QuestInfo> questInfoList)
    {
        int count = questInfoList.Count;
        for (int i = 0; i < count; ++i)
        {
            _userQuestDataDict.TryAdd((questInfoList[i].MainQuestId, questInfoList[i].SubQuestId), questInfoList[i]);
        }

        UpdateAllUI();
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

        UpdateQuestList();
    }

    // 퀘스트 포기, 완료돼서 업데이트 해야 할 때
    public void RemoveQuestData(int mainQuestId, int subQuestId)
    {
        _userQuestDataDict.Remove((mainQuestId, subQuestId));
        if (_selectedQuest != null && _selectedQuest.MainQuestId == mainQuestId && _selectedQuest.SubQuestId == subQuestId)
        {
            _selectedQuest = null;
            UpdateQuestInfo();
        }

        UpdateQuestList();
    }

    public void ChangeQuestStatus(int mainQuestId, int subQuestId, QuestStatus questStatus)
    {
        _userQuestDataDict[(mainQuestId, subQuestId)].QuestStatus = questStatus;
        _questItemDict[(mainQuestId, subQuestId)].ChangeQuestStatus(questStatus);
    }

    private void OnClickAcceptButton()
    {
        if (_selectedQuest == null)
            return;

        C_AcceptQuest acceptQuestPacket = new C_AcceptQuest();
        acceptQuestPacket.MainQuestId = _selectedQuest.MainQuestId;
        acceptQuestPacket.SubQuestId = _selectedQuest.SubQuestId;
        Managers.Network.Send(acceptQuestPacket);
    }

    private void OnClickCompleteButton()
    {
        if (_selectedQuest == null)
            return;

        C_CompleteQuest completeQuestPacket = new C_CompleteQuest();
        completeQuestPacket.MainQuestId = _selectedQuest.MainQuestId;
        completeQuestPacket.SubQuestId = _selectedQuest.SubQuestId;
        Managers.Network.Send(completeQuestPacket);
    }

    private void OnClickCloseButton()
    {
        ClosePopupUI();
    }

    private void OnClickExitButton()
    {
        ClosePopupUI();
    }

    private void OnClickBackgroundButton()
    {
        ClosePopupUI();
    }

    private void OnEnable()
    {
        // 커서 잠금 풀기
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

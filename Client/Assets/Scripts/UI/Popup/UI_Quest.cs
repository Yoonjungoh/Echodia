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

    private Transform _questContent;        // 좌측에 퀘스트 목록 나열
    private Transform _questRewardContent;  // 우측에 퀘스트 보상 나열

    private QuestInfo _selectedQuest;       // 선택된 퀘스트의 유저 정보
    private QuestObjectiveDefinitionMetaData _selectedQuestMetaData;

    private TextMeshProUGUI _questMainTitleText;
    private TextMeshProUGUI _questSubTitleText;
    private TextMeshProUGUI _questDescriptionText;
    private TextMeshProUGUI _questProgressText;
    private GameObject _questInfoPanel;

    private Dictionary<(int, int), Quest_SubItem> _questItemDict = new Dictionary<(int, int), Quest_SubItem>();

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

        // 이벤트 구독
        Managers.Quest.OnQuestListChanged -= UpdateAllUI;
        Managers.Quest.OnQuestListChanged += UpdateAllUI;

        Managers.Quest.OnQuestStatusChanged -= OnQuestStatusChangedHandler;
        Managers.Quest.OnQuestStatusChanged += OnQuestStatusChangedHandler;

        Managers.Quest.OnQuestProgressUpdated -= OnQuestProgressUpdatedHandler;
        Managers.Quest.OnQuestProgressUpdated += OnQuestProgressUpdatedHandler;

        // 이미 데이터가 있으면 바로 표시, 없으면 서버에 요청
        if (Managers.Quest.UserQuestDataDict.Count > 0)
        {
            UpdateAllUI();
        }
        else
        {
            Managers.Network.Send(new C_RequestQuestData());
        }
    }

    private void OnDestroy()
    {
        Managers.Quest.OnQuestListChanged -= UpdateAllUI;
        Managers.Quest.OnQuestStatusChanged -= OnQuestStatusChangedHandler;
        Managers.Quest.OnQuestProgressUpdated -= OnQuestProgressUpdatedHandler;
    }

    // 갖고 있는 퀘스트가 최신화 될 때마다 호출
    public void UpdateAllUI()
    {
        UpdateQuestList();
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

        // 선택된 퀘스트 참조 최신화
        if (_selectedQuest != null)
        {
            if (Managers.Quest.UserQuestDataDict.TryGetValue((_selectedQuest.MainQuestId, _selectedQuest.SubQuestId), out QuestInfo updated))
            {
                _selectedQuest = updated;
            }
            else
            {
                _selectedQuest = null;
            }
        }

        foreach (var kvp in Managers.Quest.UserQuestDataDict)
        {
            (int, int) key = kvp.Key;
            QuestInfo questInfo = kvp.Value;
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
        _questInfoPanel.SetActive(_selectedQuest != null);

        if (_selectedQuest == null)
            return;

        QuestDefinitionMetaData mainQuestData = Managers.SpecData.GetQuestDefinition(_selectedQuest.MainQuestId);
        _questMainTitleText.text = $"{mainQuestData.Title}";
        _questSubTitleText.text = $"Lv.{mainQuestData.ReqLevel} Quest";
        _questDescriptionText.text = _selectedQuestMetaData.Description;
        _questProgressText.text = $"({_selectedQuest.RequiredCount}/{_selectedQuestMetaData.RequiredCount})";

        // TODO - 퀘스트 보상 시각화
        foreach (Transform child in _questRewardContent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }

        int count = _selectedQuestMetaData.RewardIdList.Count;
        for (int i = 0; i < count; ++i)
        {
            Reward_SubItem rewardSubItem = Managers.UI.MakeSubItem<Reward_SubItem>(_questRewardContent);
            RewardItem rewardItem = new RewardItem()
            {
                RewardId = _selectedQuestMetaData.RewardIdList[i],
                RewardAmount = _selectedQuestMetaData.RewardAmountList[i]
            };
            rewardSubItem.SetData(rewardItem);
        }

        _acceptButton.gameObject.SetActive(_selectedQuest.QuestStatus == QuestStatus.NotAccepted);
        _proceedingButton.gameObject.SetActive(_selectedQuest.QuestStatus == QuestStatus.Proceeding);
        _completeButton.gameObject.SetActive(_selectedQuest.QuestStatus == QuestStatus.Completed);
    }

    private void OnQuestStatusChangedHandler(int mainQuestId, int subQuestId, QuestStatus questStatus)
    {
        if (_questItemDict.TryGetValue((mainQuestId, subQuestId), out Quest_SubItem subItem))
        {
            subItem.ChangeQuestStatus(questStatus);
        }

        if (_selectedQuest != null && _selectedQuest.MainQuestId == mainQuestId && _selectedQuest.SubQuestId == subQuestId)
        {
            UpdateQuestInfo();
        }
    }

    private void OnQuestProgressUpdatedHandler(int mainQuestId, int subQuestId, int currentCount)
    {
        if (_selectedQuest != null && _selectedQuest.MainQuestId == mainQuestId && _selectedQuest.SubQuestId == subQuestId)
        {
            UpdateQuestInfo();
        }
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

    private void OnDisable()
    {
        // 커서 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        // 커서 해금
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Managers.RedDot.Set(RedDotType.Quest, false);
    }
}

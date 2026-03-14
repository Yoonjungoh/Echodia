
using System;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using UnityEngine;

public class QuestManager
{
    // 서버에서 받은 유저 퀘스트 데이터
    // key = (mainQuestId, subQuestId), 오름차순 정렬
    private SortedDictionary<(int, int), QuestInfo> _userQuestDataDict = new SortedDictionary<(int, int), QuestInfo>();
    public IReadOnlyDictionary<(int, int), QuestInfo> UserQuestDataDict => _userQuestDataDict;

    // 퀘스트 데이터 변경 이벤트 (UI에서 구독)
    public Action OnQuestListChanged;
    public Action<int, int, QuestStatus> OnQuestStatusChanged;
    public Action<int, int, int> OnQuestProgressUpdated;

    public void Init() { }

    public void InitQuestData(RepeatedField<QuestInfo> questInfoList)
    {
        int count = questInfoList.Count;
        for (int i = 0; i < count; ++i)
        {
            _userQuestDataDict.TryAdd(
                (questInfoList[i].MainQuestId, questInfoList[i].SubQuestId),
                questInfoList[i]);
        }
        OnQuestListChanged?.Invoke();
    }

    // 새로운 퀘스트가 생성됐을 때
    public void AddQuestData(int mainQuestId, int subQuestId)
    {
        QuestObjectiveDefinitionMetaData questData = Managers.SpecData.GetQuestObjectiveDefinition(mainQuestId, subQuestId);
        QuestInfo questInfo = new QuestInfo
        {
            MainQuestId = questData.MainQuestId,
            SubQuestId = questData.SubQuestId,
            RequiredCount = 0,
            QuestStatus = QuestStatus.NotAccepted,
        };
        _userQuestDataDict.TryAdd((mainQuestId, subQuestId), questInfo);
        Managers.RedDot.Set(RedDotType.Quest, true);
        OnQuestListChanged?.Invoke();
    }

    // 퀘스트 포기, 완료돼서 제거해야 할 때
    public void RemoveQuestData(int mainQuestId, int subQuestId)
    {
        _userQuestDataDict.Remove((mainQuestId, subQuestId));
        OnQuestListChanged?.Invoke();
    }

    public void ChangeQuestStatus(int mainQuestId, int subQuestId, QuestStatus questStatus)
    {
        if (!_userQuestDataDict.TryGetValue((mainQuestId, subQuestId), out QuestInfo questInfo))
            return;

        questInfo.QuestStatus = questStatus;

        // 보상 수령 완료 → 퀘스트 목록에서 제거
        if (questStatus == QuestStatus.RewardClaimed)
        {
            RemoveQuestData(mainQuestId, subQuestId);
            return;
        }

        OnQuestStatusChanged?.Invoke(mainQuestId, subQuestId, questStatus);
    }

    public void UpdateQuestProgress(int mainQuestId, int subQuestId, int currentCount)
    {
        if (!_userQuestDataDict.TryGetValue((mainQuestId, subQuestId), out QuestInfo questInfo))
            return;

        questInfo.RequiredCount = currentCount;
        Managers.RedDot.Set(RedDotType.Quest, true);
        OnQuestProgressUpdated?.Invoke(mainQuestId, subQuestId, currentCount);
    }
}

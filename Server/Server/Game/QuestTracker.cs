using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Game
{
    public class QuestTracker
    {
        // 킬 퀘스트 진행 항목: 스펙 시트의 목표 킬 수를 캐싱
        private class KillQuestEntry
        {
            public QuestDb Quest { get; set; }
            public int TargetCount { get; set; } // 스펙 시트의 RequiredCount (목표 킬 수)
        }

        private readonly Player _owner;

        // key: 몬스터 TemplateId -> 해당 몬스터를 목표로 하는 킬 퀘스트 목록
        private readonly Dictionary<int, List<KillQuestEntry>> _killQuestByTargetId = new();

        // 메모리에서만 변경되고 DB에 아직 반영 안 된 퀘스트 ID 목록 (로그아웃 시 일괄 저장)
        private readonly HashSet<int> _dirtyQuestIds = new();

        public QuestTracker(Player owner)
        {
            _owner = owner;
        }

        // 로그인 시 진행 중인 킬 퀘스트를 메모리에 로드하고 옵저버 인덱스 구성
        public void Load()
        {
            _killQuestByTargetId.Clear();
            _dirtyQuestIds.Clear();

            if (_owner.PlayerId == 0)
                return;

            using GameDbContext db = new GameDbContext();
            PlayerDb playerDb = db.Players
                .Include(p => p.Quests)
                .AsNoTracking()
                .FirstOrDefault(p => p.PlayerDbId == _owner.PlayerId);

            if (playerDb == null)
                return;

            List<QuestObjectiveDefinitionMetaData> allObjectives = SpecDataManager.Instance.GetAllQuestObjectiveDefinition();

            foreach (QuestDb quest in playerDb.Quests)
            {
                if (quest.Status != QuestStatus.Proceeding)
                    continue;

                QuestObjectiveDefinitionMetaData objective = allObjectives.FirstOrDefault(o =>
                    o.MainQuestId == quest.MainQuestId &&
                    o.SubQuestId == quest.SubQuestId &&
                    o.QuestObjectiveType == QuestObjectiveType.Kill);

                if (objective == null)
                    continue;

                RegisterKillQuest(quest, objective.TargetId, objective.RequiredCount);
            }
        }

        // 퀘스트 수락 시 킬 퀘스트 인덱스에 등록
        public void RegisterKillQuest(QuestDb quest, int targetId, int targetCount)
        {
            if (!_killQuestByTargetId.TryGetValue(targetId, out List<KillQuestEntry> list))
            {
                list = new List<KillQuestEntry>();
                _killQuestByTargetId[targetId] = list;
            }
            list.Add(new KillQuestEntry { Quest = quest, TargetCount = targetCount });
        }

        // 킬 이벤트 수신 = 진행도 업데이트 + 완료 여부 판단
        // 완료 판단까지만 담당하고, 완료 처리(Status/Date/DB/패킷)는 QuestManager에 위임
        public void OnMonsterKilled(int monsterTemplateId)
        {
            if (!_killQuestByTargetId.TryGetValue(monsterTemplateId, out List<KillQuestEntry> entries))
                return;

            List<KillQuestEntry> completedEntries = null;

            foreach (KillQuestEntry entry in entries)
            {
                entry.Quest.RequiredCount++;
                _dirtyQuestIds.Add(entry.Quest.QuestDbId);

                // 진행도 변경을 클라이언트에 알림 (QuestTracker 책임)
                SendQuestProgressUpdate(entry.Quest, entry.TargetCount);

                if (entry.Quest.RequiredCount >= entry.TargetCount)
                {
                    if (completedEntries == null)
                    {
                         completedEntries = new List<KillQuestEntry>();
                    }
                    completedEntries.Add(entry);
                }
            }

            if (completedEntries == null)
                return;

            foreach (KillQuestEntry entry in completedEntries)
            {
                entries.Remove(entry);
                _dirtyQuestIds.Remove(entry.Quest.QuestDbId); // QuestManager가 즉시 저장하므로 dirty 제거
                // 완료 처리(Status/Date 변경, DB 저장, S_QuestCompleted 패킷)는 QuestManager 책임
                QuestManager.Instance.CompleteQuest(_owner, entry.Quest);
            }
        }

        // 로그아웃 시 메모리에서만 변경된 진행도를 DB에 일괄 저장
        public void SaveDirtyQuests()
        {
            if (_dirtyQuestIds.Count == 0)
                return;

            List<QuestDb> dirtyQuests = _killQuestByTargetId.Values
                .SelectMany(list => list)
                .Where(e => _dirtyQuestIds.Contains(e.Quest.QuestDbId))
                .Select(e => e.Quest)
                .ToList();

            _dirtyQuestIds.Clear();
            QuestManager.Instance.SaveQuestProgressAsync(dirtyQuests);
        }

        // 진행도 변경 시 클라이언트에 알림
        // QuestTracker가 담당하는 이유 = 진행 상태의 주인이므로 변경 시점을 정확히 앎
        private void SendQuestProgressUpdate(QuestDb quest, int targetCount)
        {
            S_QuestProgressUpdate packet = new S_QuestProgressUpdate();
            packet.MainQuestId = quest.MainQuestId;
            packet.SubQuestId = quest.SubQuestId;
            packet.CurrentCount = quest.RequiredCount;
            _owner.Session?.Send(packet);
        }

        public void RemoveKillQuestTarget(int targetId)
        {
            _killQuestByTargetId.Remove(targetId);
        }
    }
}

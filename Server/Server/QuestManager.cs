using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.DB;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Server
{
    // TODO - JSON 파싱
    public class QuestManager
    {
        public static QuestManager Instance { get; } = new QuestManager();

        // 퀘스트 생성 조건 달성했는지 확인 후, 패킷 전송
        // 퀘스트 관련 패킷 유저에게 전송
        // 퀘스트 생성 조건 (레벨업, 퀘스트 클리어)
        public void UpdateAvailableQuests(Player player)
        {

        }

        public void UpdateQuestObjective(QuestObjectiveType questObjectiveType)
        {
            switch (questObjectiveType)
            {
                case QuestObjectiveType.Kill:
                    // 킬 관련 퀘스트 진행도 확인
                    break;
                default:
                    break;
            }
        }

        // 몬스터 처치 시 진입 -> 크레딧 받을 플레이어를 결정하고 각자의 퀘스트 진행도 업데이트
        public void OnMonsterKilled(GameRoom room, Player killer, int monsterTemplateId)
        {
            List<Player> creditedPlayers = GetCreditedPlayers(room, killer);
            foreach (Player player in creditedPlayers)
            {
                player.OnMonsterKilled(monsterTemplateId);
            }
        }

        // 처치 크레딧을 받을 플레이어 목록 반환
        // 현재: 직접 킬한 플레이어만 (파티 시스템 추가 시 여기서 파티원 포함)
        private List<Player> GetCreditedPlayers(GameRoom room, Player killer)
        {
            // TODO: 파티 시스템 구현 시 파티원 중 같은 맵에 있는 플레이어 포함
            List<Player> players = new List<Player>();
            players.Add(killer);

            return players;
        }

        // 퀘스트 완료 시 즉시 DB에 반영
        public void CompleteQuestsAsync(List<QuestDb> quests)
        {
            Task.Run(() =>
            {
                using GameDbContext db = new GameDbContext();
                foreach (QuestDb quest in quests)
                {
                    QuestDb dbQuest = db.Quests.Find(quest.QuestDbId);
                    if (dbQuest == null) 
                        continue;

                    dbQuest.RequiredCount = quest.RequiredCount;
                    dbQuest.Status = quest.Status;
                    dbQuest.ClearedDate = quest.ClearedDate;
                }
                db.SaveChanges();
            });
        }

        // 로그아웃 시 메모리의 진행도를 DB에 일괄 저장
        public void SaveQuestProgressAsync(List<QuestDb> quests)
        {
            if (quests.Count == 0) return;

            Task.Run(() =>
            {
                using GameDbContext db = new GameDbContext();
                foreach (QuestDb quest in quests)
                {
                    QuestDb dbQuest = db.Quests.Find(quest.QuestDbId);
                    if (dbQuest == null) continue;
                    dbQuest.RequiredCount = quest.RequiredCount;
                }
                db.SaveChanges();
            });
        }

        public bool CanClear(int playerDbId, int mainQuestId, int subQuestId)
        {
            // 1. 시트에 있는 데이터 바탕으로 목표치를 채웠나 확인하기 위해 데이터 가져오기
            var quest = SpecDataManager.Instance.GetQuestObjectiveDefinition(mainQuestId, subQuestId);
            using (GameDbContext db = new GameDbContext())
            {
                // 2. DB에서 플레이어의 퀘스트 진행 이력 가져오기
                PlayerDb playerDb = db.Players
                    .AsNoTracking()
                    .Where(p => p.PlayerDbId == playerDbId)
                    .FirstOrDefault();

                if (playerDb == null)
                    return false;

                // 3. 가져온 데이터랑 시트에 정의된 데이터를 비교해서 클리어 가능한 진행도인지 확인
                var quests = playerDb.Quests;
                foreach (QuestDb q in quests)
                {
                    if (q.MainQuestId == quest.MainQuestId &&
                        q.SubQuestId == quest.SubQuestId &&
                        q.RequiredCount == quest.RequiredCount)
                    {
                        return true;
                    }
                }

            }
            return false;
        }

        public ICollection<QuestDb> GetPlayerQuestDb(int playerDbId)
        {
            using (GameDbContext db = new GameDbContext())
            {
                // 1. DB에서 플레이어의 퀘스트 진행 이력 가져오기
                PlayerDb playerDb = db.Players
                    .AsNoTracking()
                    .Where(p => p.PlayerDbId == playerDbId)
                    .FirstOrDefault();

                if (playerDb == null)
                    return null;

                // 2. 가져온 데이터랑 시트에 정의된 데이터를 비교해서 클리어 가능한 진행도인지 확인
                return playerDb.Quests;
            }
        }

        // 항상 메인 퀘스트의 마지막 서브 퀘스트를 클리어하면 서브 퀘스트 마지막 보상과 같이 수령함
        private void GetMainQuestReward(Player player, int mainQuestId, int SubQuestId)
        {

        }

        private void GetSubQuestReward(Player player, int mainQuestId, int SubQuestId)
        {

        }

        public void CreateQuest(PlayerDb playerDb, int mainQuestId, int subQuestId)
        {
            // playerDbId에게 퀘스트 생성해서 Db에 저장
            QuestDb quest = new QuestDb()
            {
                PlayerDbId = playerDb.PlayerDbId,
                MainQuestId = mainQuestId,
                SubQuestId = subQuestId,
                RequiredCount = 0,
                Status = QuestStatus.Proceeding,   // TODO - 플레이어가 클라에서 Accept 해야 Proceeding으로 가는 거임
                StartedDate = DateTime.UtcNow,
            };
            playerDb.Quests.Add(quest);
        }
    }
}

// 킬 퀘스트 진행 항목: 스펙 시트의 목표 킬 수를 캐싱
public class KillQuestEntry
{
    public QuestDb Quest { get; set; }
    public int TargetCount { get; set; } // 스펙 시트의 RequiredCount (목표 킬 수)
}
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.DB;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;

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
                Status = QuestStatus.NotAccepted,
                StartedDate = DateTime.UtcNow,
            };
            playerDb.Quests.Add(quest);
        }
    }
}
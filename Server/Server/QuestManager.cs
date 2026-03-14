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

        // 몬스터 처치 시 진입점 -> 크레딧 받을 플레이어를 결정하고 각자의 QuestTracker에 통지
        public void OnMonsterKilled(GameRoom room, Player killer, int monsterTemplateId)
        {
            List<Player> creditedPlayers = GetCreditedPlayers(room, killer);
            foreach (Player player in creditedPlayers)
            {
                player.QuestTracker.OnMonsterKilled(monsterTemplateId);
            }
        }

        // 처치 크레딧을 받을 플레이어 목록 반환
        // 현재 -> 직접 킬한 플레이어만 (파티 시스템 추가 시 여기서 파티원 포함)
        private List<Player> GetCreditedPlayers(GameRoom room, Player killer)
        {
            // TODO - 파티 시스템 구현 시 파티원 중 같은 맵에 있는 플레이어 포함
            return new List<Player> { killer };
        }

        // 퀘스트 완료 처리 -> Status/Date 변경, DB 즉시 저장 및 퀘스트 완료 패킷 전송
        // QuestTracker가 완료 조건을 감지한 뒤 여기에서 처리해줌
        public void CompleteQuest(Player player, QuestDb quest)
        {
            quest.Status = QuestStatus.Completed;
            quest.ClearedDate = DateTime.UtcNow;

            DbTransaction.SaveQuestComplete(quest, () =>
            {
                SendQuestCompleted(player, quest);
                player.QuestTracker.RemoveKillQuestTarget(quest.TargetId);   // 퀘스트 완료 됐으니 다시 로드
            });
        }

        // 로그아웃 시 메모리의 진행도를 DB에 일괄 저장
        public void SaveQuestProgressAsync(List<QuestDb> quests)
        {
            if (quests.Count == 0)
                return;

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
        
        // 항상 메인 퀘스트의 마지막 서브 퀘스트를 클리어하면 서브 퀘스트 마지막 보상과 같이 수령함
        private void GetMainQuestReward(Player player, int mainQuestId, int SubQuestId)
        {

        }

        private void GetSubQuestReward(Player player, int mainQuestId, int SubQuestId)
        {

        }

        // 퀘스트 생성하고 DB에 저장하고 패킷도 클라에게 전송
        public void CreateQuest(GameDbContext db, PlayerDb playerDb, ClientSession session, int mainQuestId, int subQuestId)
        {
            // playerDbId에게 퀘스트 생성해서 Db에 저장
            QuestDb quest = new QuestDb()
            {
                PlayerDbId = playerDb.PlayerDbId,
                MainQuestId = mainQuestId,
                SubQuestId = subQuestId,
                RequiredCount = 0,
                Status = QuestStatus.NotAccepted, // 플레이어가 클라에서 Accept 해야 Proceeding으로 가는 거임
                StartedDate = DateTime.UtcNow,
            };
            playerDb.Quests.Add(quest);

            bool success = db.SaveChangesEx();
            if (success == false)
                return;

            S_CreateQuest createQuestPacket = new S_CreateQuest();
            createQuestPacket.MainQuestId = mainQuestId;
            createQuestPacket.SubQuestId = subQuestId;
            session.Send(createQuestPacket);

            session.MyPlayer.QuestTracker.Load();   // 퀘스트 생성 됐으니 다시 로드
        }

        // 퀘스트 완료 알림을 클라이언트에 전송
        // QuestManager가 담당하는 이유 = 완료 비즈니스 로직(Status/Date/DB) 주인이므로 알림도 여기서
        private void SendQuestCompleted(Player player, QuestDb quest)
        {
            S_ChangeQuestStatus changeQuestStatusPacket = new S_ChangeQuestStatus();
            changeQuestStatusPacket.MainQuestId = quest.MainQuestId;
            changeQuestStatusPacket.SubQuestId = quest.SubQuestId;
            changeQuestStatusPacket.QuestStatus = QuestStatus.Completed;
            player.Session?.Send(changeQuestStatusPacket);
        }
    }
}

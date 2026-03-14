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
        
        // 클라이언트가 Complete 버튼을 눌러 보상 수령 요청 시 호출
        // 검증 -> DB 업데이트(RewardClaimed) -> 재화 지급 -> 패킷 전송
        public void ClaimReward(Player player, int mainQuestId, int subQuestId)
        {
            List<QuestObjectiveDefinitionMetaData> allObjectives = SpecDataManager.Instance.GetAllQuestObjectiveDefinition();
            QuestObjectiveDefinitionMetaData objective = allObjectives.FirstOrDefault(o =>
                o.MainQuestId == mainQuestId && o.SubQuestId == subQuestId);

            if (objective == null)
            {
                Console.WriteLine($"[Quest] ClaimReward: objective not found ({mainQuestId}-{subQuestId})");
                return;
            }

            CurrencyMetaData currency = SpecDataManager.Instance.GetCurrency(objective.RewardId);
            if (currency == null)
            {
                Console.WriteLine($"[Quest] ClaimReward: currency not found (RewardId={objective.RewardId})");
                return;
            }

            DbTransaction.GiveQuestReward(
                player, mainQuestId, subQuestId,
                objective.RewardAmount, currency.CurrencyType,
                (newAmount) =>
                {
                    // 퀘스트 상태 변경 패킷
                    S_ChangeQuestStatus changeStatusPacket = new S_ChangeQuestStatus()
                    {
                        MainQuestId = mainQuestId,
                        SubQuestId = subQuestId,
                        QuestStatus = QuestStatus.RewardClaimed,
                    };
                    player.Session?.Send(changeStatusPacket);

                    // 보상 수령 패킷
                    S_GiveReward giveRewardPacket = new S_GiveReward();
                    giveRewardPacket.RewardItemList.Add(new RewardItem
                    {
                        RewardId = objective.RewardId,
                        RewardAmount = objective.RewardAmount,
                    });
                    player.Session?.Send(giveRewardPacket);
                    // S_UpdateCurrencyData는 GiveQuestReward_Db 내부에서 자동 전송됨
                });
        }

        // 퀘스트 생성하고 DB에 저장하고 패킷도 클라에게 전송
        public void CreateQuest(GameDbContext db, PlayerDb playerDb, ClientSession session, int mainQuestId, int subQuestId)
        {
            List<QuestObjectiveDefinitionMetaData> allObjectives = SpecDataManager.Instance.GetAllQuestObjectiveDefinition();
            QuestObjectiveDefinitionMetaData objective = allObjectives.FirstOrDefault(o =>
                o.MainQuestId == mainQuestId && o.SubQuestId == subQuestId);

            // playerDbId에게 퀘스트 생성해서 Db에 저장
            QuestDb quest = new QuestDb()
            {
                PlayerDbId = playerDb.PlayerDbId,
                MainQuestId = mainQuestId,
                SubQuestId = subQuestId,
                TargetId = objective?.TargetId ?? 0,
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

using Google.Protobuf;
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

        // KillRewardManager에서 호출 — 크레딧 대상 목록을 직접 받아 처리
        public void OnMonsterKilled(GameRoom room, List<Player> creditedPlayers, int monsterTemplateId)
        {
            foreach (Player player in creditedPlayers)
            {
                player.QuestTracker.OnMonsterKilled(monsterTemplateId);
            }
        }

        // 하위 호환용 — 솔로킬 경로에서 직접 호출 시 (현재는 KillRewardManager가 위 오버로드를 사용)
        public void OnMonsterKilled(GameRoom room, Player killer, int monsterTemplateId)
        {
            List<Player> creditedPlayers = GetCreditedPlayers(room, killer);
            OnMonsterKilled(room, creditedPlayers, monsterTemplateId);
        }

        // 처치 크레딧을 받을 플레이어 목록 반환 (하위 호환용 스텁)
        private List<Player> GetCreditedPlayers(GameRoom room, Player killer)
        {
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
                player.GameRoom?.Push(() =>
                {
                    player.QuestTracker.Completed(quest.MainQuestId, quest.SubQuestId);
                    SendQuestCompleted(player, quest);
                });
            });
        }

        // 로그아웃 시 메모리의 진행도를 DB에 일괄 저장
        public void SaveQuestProgressAsync(List<QuestDb> quests)
        {
            if (quests.Count == 0)
                return;

            Task.Run(async () =>
            {
                using GameDbContext db = new GameDbContext();
                foreach (QuestDb quest in quests)
                {
                    QuestDb dbQuest = db.Quests.Find(quest.QuestDbId);
                    if (dbQuest == null) continue;
                    dbQuest.RequiredCount = quest.RequiredCount;
                }
                await db.SaveChangesExAsync();
            });
        }

        public void ClaimReward(Player player, int mainQuestId, int subQuestId)
        {
            var room = player?.GameRoom;
            var objective = SpecDataManager.Instance.GetQuestObjectiveDefinition(mainQuestId, subQuestId);
            if (room == null || objective == null) return;
            player.Economy.EnqueueClient(player, () =>
            {
                if (!player.QuestTracker.IsClaimable(mainQuestId, subQuestId)) return;
                var batch = player.Economy.Capture(player);
                var grant = new QuestGrant(mainQuestId, subQuestId);
                if (!PlanReward(player, batch, grant, objective)) return;
                AddNextQuests(grant);
                var session = player.Session;
                SubmitReward(player, batch, grant, objective, message =>
                {
                    if (session.IsConnected && session.MyPlayer == player) session.Send(message);
                });
            });
        }

        internal static void SubmitReward(Player player, EconomyBatch batch, QuestGrant grant,
            QuestObjectiveDefinitionMetaData objective, Action<IMessage> notify)
        {
            player.Economy.Submit(player, batch, grant, () =>
            {
                player.QuestTracker.Claimed(grant.MainId, grant.SubId);
                foreach (var item in grant.AwardedSlots.Values)
                    notify(new S_UpdateItemData { ItemInfo = item.ToItemInfo() });
                foreach (var (type, balance) in grant.Balances)
                    notify(new S_UpdateCurrencyData { CurrencyType = type, Amount = balance });
                notify(new S_ChangeQuestStatus
                {
                    MainQuestId = grant.MainId, SubQuestId = grant.SubId, QuestStatus = QuestStatus.RewardClaimed
                });
                var response = new S_GiveReward();
                for (int i = 0; i < objective.RewardIdList.Count; i++)
                    response.RewardItemList.Add(new RewardItem { RewardId = objective.RewardIdList[i], RewardAmount = objective.RewardAmountList[i] });
                notify(response);
                foreach (var next in grant.Created)
                    notify(new S_CreateQuest { MainQuestId = next.MainId, SubQuestId = next.SubId });
            });
        }

        private static bool PlanReward(Player player, EconomyBatch batch, QuestGrant grant, QuestObjectiveDefinitionMetaData objective)
        {
            var ids = objective.RewardIdList;
            var amounts = objective.RewardAmountList;
            if (ids == null || amounts == null || ids.Count == 0 || ids.Count != amounts.Count) return false;
            for (int i = 0; i < ids.Count; i++)
            {
                if (amounts[i] <= 0) return false;
                var currency = SpecDataManager.Instance.GetCurrency(ids[i]);
                if (currency != null)
                {
                    int current = grant.Balances.TryGetValue(currency.CurrencyType, out int projected) ?
                        projected : player.CurrencyTracker.Get(currency.CurrencyType);
                    int balance = checked(current + amounts[i]);
                    grant.Balances[currency.CurrencyType] = balance;
                    batch.Currency[currency.CurrencyType] = balance;
                    continue;
                }
                var meta = SpecDataManager.Instance.GetItem(ids[i]);
                if (meta == null || meta.MaxStack <= 0 ||
                    !AllocateItems(player.Items, player.InventoryTracker.AmbiguousSlots, batch, grant, meta, amounts[i], player.GetInventorySize(meta.ItemType)))
                    return false;
            }
            return true;
        }

        internal static bool AllocateItems(IReadOnlyDictionary<(ItemType, int), PlayerItemDb> bag,
            ISet<(ItemType, int)> ambiguous, EconomyBatch batch, QuestGrant grant, ItemMetaData meta, int amount, int capacity)
        {
            if (amount <= 0 || meta.MaxStack <= 0) return false;
            // Existing stacks first, then free slots. A plan that cannot fit is discarded in its entirety.
            for (int pass = 0; pass < 2; pass++)
                for (int slot = 0; slot < capacity && amount > 0; slot++)
                {
                    var key = (meta.ItemType, slot);
                    if (ambiguous.Contains(key)) continue;
                    if (!grant.AwardedSlots.TryGetValue(key, out var live)) bag.TryGetValue(key, out live);
                    if (pass == 0 && (live == null || live.ItemId != meta.Id)) continue;
                    if (pass == 1 && live != null) continue;
                    live ??= new PlayerItemDb { PlayerDbId = batch.PlayerId, ItemId = meta.Id, SlotIndex = slot };
                    if (!batch.Items.TryGetValue(live, out var updated)) updated = live.Snapshot();
                    if (updated.Count < 0 || updated.Count >= meta.MaxStack) continue;
                    int add = Math.Min(amount, meta.MaxStack - updated.Count);
                    updated.Count += add;
                    batch.Items[live] = updated;
                    grant.AwardedSlots[key] = live;
                    amount -= add;
                }
            return amount == 0;
        }

        private static void AddNextQuests(QuestGrant grant)
        {
            var spec = SpecDataManager.Instance;
            var nextSub = spec.GetQuestObjectiveDefinition(grant.MainId, grant.SubId + 1);
            if (nextSub != null)
            {
                grant.Next.Add(new NextQuest(grant.MainId, nextSub.SubQuestId, nextSub.TargetId, 0));
                return;
            }
            var ids = spec.GetQuestDefinition(grant.MainId)?.NextQuestIdList;
            if (ids == null) return;
            foreach (var id in ids.Distinct())
            {
                var first = spec.GetQuestObjectiveDefinition(id, 1);
                if (first != null)
                    grant.Next.Add(new NextQuest(id, 1, first.TargetId, spec.GetQuestDefinition(id)?.PrereqQuestId ?? 0));
            }
        }

        // 퀘스트 생성하고 DB에 저장하고 패킷도 클라에게 전송
        public void CreateQuest(GameDbContext db, PlayerDb playerDb, ClientSession session, int mainQuestId, int subQuestId)
        {
            QuestObjectiveDefinitionMetaData objective = SpecDataManager.Instance.GetQuestObjectiveDefinition(mainQuestId, subQuestId);
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

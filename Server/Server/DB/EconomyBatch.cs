using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;

namespace Server.DB
{
    internal enum EconomyWrite { Saved, IneligibleQuest, StaleOwner }

    // Values are copied on the room thread. Only the generated identity of an inserted
    // snapshot is read back after commit; the DB worker never edits room-owned objects.
    internal sealed class EconomyBatch
    {
        internal int PlayerId { get; }
        internal int AccountId { get; }
        internal long Revision { get; }
        internal Dictionary<CurrencyType, int> Currency { get; } = new();
        internal Dictionary<PlayerItemDb, PlayerItemDb> Items { get; } = new();
        internal List<PlayerItemDb> Deleted { get; } = new();

        internal EconomyBatch(int playerId, int accountId, long revision)
        {
            PlayerId = playerId;
            AccountId = accountId;
            Revision = revision;
        }

        internal EconomyWrite Save(GameDbContext db, QuestGrant grant = null)
        {
            using var tx = db.Database.BeginTransaction();
            int changed = db.Players.Where(p => p.PlayerDbId == PlayerId && p.AccountDbId == AccountId
                && p.EconomyRevision == Revision).ExecuteUpdate(s => s.SetProperty(p => p.EconomyRevision, p => p.EconomyRevision + 1));
            if (changed != 1)
                return EconomyWrite.StaleOwner;
            if (grant != null)
            {
                int claimed = db.Quests.Where(q => q.PlayerDbId == PlayerId && q.MainQuestId == grant.MainId &&
                    q.SubQuestId == grant.SubId && q.Status == QuestStatus.Completed)
                    .ExecuteUpdate(s => s.SetProperty(q => q.Status, QuestStatus.RewardClaimed));
                if (claimed != 1)
                    return EconomyWrite.IneligibleQuest;
            }

            if (Currency.Count != 0)
            {
                var owner = db.Players.Single(p => p.PlayerDbId == PlayerId);
                foreach (var (type, balance) in Currency)
                    DbTransaction.SetCurrencyValue(owner, type, balance);
            }

            var deletedIds = Deleted.Select(i => i.PlayerItemDbId).Where(id => id > 0).ToArray();
            if (deletedIds.Length > 0 &&
                db.PlayerItems.Where(i => i.PlayerDbId == PlayerId && deletedIds.Contains(i.PlayerItemDbId)).ExecuteDelete() != deletedIds.Length)
                throw new DbUpdateConcurrencyException("An inventory row disappeared while saving.");
            var existingIds = Items.Values.Where(i => i.PlayerItemDbId > 0).Select(i => i.PlayerItemDbId).ToArray();
            var existing = existingIds.Length == 0 ? new Dictionary<int, PlayerItemDb>() :
                db.PlayerItems.Where(i => i.PlayerDbId == PlayerId && existingIds.Contains(i.PlayerItemDbId)).ToDictionary(i => i.PlayerItemDbId);
            foreach (var item in Items.Values)
            {
                if (item.PlayerDbId != PlayerId) throw new InvalidOperationException("Item owner mismatch.");
                if (item.PlayerItemDbId == 0)
                    db.PlayerItems.Add(item);
                else if (existing.TryGetValue(item.PlayerItemDbId, out var row))
                    db.Entry(row).CurrentValues.SetValues(item);
                else
                    throw new DbUpdateConcurrencyException("An inventory row does not belong to the checkpoint.");
            }
            if (grant != null)
                foreach (var next in grant.Next)
                {
                    if (db.Quests.Any(q => q.PlayerDbId == PlayerId && q.MainQuestId == next.MainId && q.SubQuestId == next.SubId))
                        continue;
                    if (next.Prerequisite != 0 && !db.Quests.Any(q => q.PlayerDbId == PlayerId &&
                        q.MainQuestId == next.Prerequisite && q.Status == QuestStatus.RewardClaimed))
                        continue;
                    db.Quests.Add(new QuestDb { PlayerDbId = PlayerId, MainQuestId = next.MainId, SubQuestId = next.SubId,
                        TargetId = next.TargetId, Status = QuestStatus.NotAccepted });
                    grant.Created.Add(next);
                }
            db.SaveChanges();
            tx.Commit();
            return EconomyWrite.Saved;
        }
    }

    internal record NextQuest(int MainId, int SubId, int TargetId, int Prerequisite);

    internal sealed class QuestGrant
    {
        internal int MainId { get; }
        internal int SubId { get; }
        internal List<NextQuest> Next { get; } = new();
        internal List<NextQuest> Created { get; } = new();
        internal Dictionary<CurrencyType, int> Balances { get; } = new();
        internal Dictionary<(ItemType, int), PlayerItemDb> AwardedSlots { get; } = new();

        internal QuestGrant(int mainId, int subId)
        {
            MainId = mainId;
            SubId = subId;
        }
    }
}

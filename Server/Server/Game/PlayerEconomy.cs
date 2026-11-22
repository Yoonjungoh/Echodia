using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.DB;

namespace Server.Game
{
    // One checkpoint in flight per character. Ordinary autosave I/O runs on the DB
    // worker; a reward or final logout may need to finish that checkpoint first.
    internal sealed class PlayerEconomy
    {
        long _revision;
        EconomyBatch _pending;
        Task<EconomyWrite> _write;
        bool _stale;

        internal void Load(Player player)
        {
            using var db = new GameDbContext();
            using var tx = db.Database.BeginTransaction(IsolationLevel.RepeatableRead);
            var data = db.Players.AsNoTracking().Include(p => p.Items).Single(p => p.PlayerDbId == player.PlayerId);
            _revision = data.EconomyRevision;
            player.InventoryTracker.Load(data.Items);
            player.CurrencyTracker.Load(data);
            tx.Commit();
        }

        internal EconomyBatch Capture(Player player)
        {
            var batch = new EconomyBatch(player.PlayerId, player.Session.AccountId, _revision);
            player.InventoryTracker.Snapshot(batch);
            player.CurrencyTracker.Snapshot(batch.Currency);
            return batch;
        }

        internal void Flush(Player player, bool leaving)
        {
            if (!FinishPending(player, leaving) || (!player.InventoryTracker.HasChanges && !player.CurrencyTracker.HasChanges))
                return;
            var batch = Capture(player);
            if (leaving)
            {
                try
                {
                    using var db = new GameDbContext();
                    Acknowledge(player, batch, batch.Save(db));
                }
                catch (Exception e) { ConsoleLogManager.Instance.Log(e); }
                return;
            }
            var completion = new TaskCompletionSource<EconomyWrite>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = batch;
            _write = completion.Task;
            DbTransaction.Instance.Push(() =>
            {
                try
                {
                    using var db = new GameDbContext();
                    completion.SetResult(batch.Save(db));
                }
                catch (Exception error) { completion.SetException(error); }
            });
        }

        internal bool FinishPending(Player player, bool wait)
        {
            if (_write == null) return !_stale;
            if (!wait && !_write.IsCompleted) return false;
            try
            {
                Acknowledge(player, _pending, _write.GetAwaiter().GetResult());
            }
            catch (Exception e)
            {
                // Dirty markers were retained. A new snapshot can retry safely.
                ConsoleLogManager.Instance.Log(e);
            }
            finally
            {
                _write = null;
                _pending = null;
            }
            return !_stale;
        }

        internal void Acknowledge(Player player, EconomyBatch batch, EconomyWrite outcome)
        {
            if (outcome == EconomyWrite.StaleOwner)
            {
                _stale = true;
                player.Session?.Disconnect();
                return;
            }
            if (outcome != EconomyWrite.Saved) return;
            _revision++;
            player.InventoryTracker.Acknowledge(batch);
            foreach (var (type, value) in batch.Currency)
                player.CurrencyTracker.Acknowledge(type, value);
        }
    }
}

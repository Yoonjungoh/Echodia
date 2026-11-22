using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Server.DB;

namespace Server.Game
{
    // All state below belongs to the room. SQL works on snapshots and posts its
    // result back here. Only this character's economic actions wait for a commit.
    internal sealed class PlayerEconomy
    {
        const int MaxQueuedClientActions = 64;
        readonly Func<GameDbContext> _open;
        Queue<Action> _waiting;
        int _accountId;
        long _revision;
        bool _writing;
        bool _invalid;
        int _retryDelayMs = 1000;

        internal PlayerEconomy() : this(() => new GameDbContext()) { }
        internal PlayerEconomy(Func<GameDbContext> open) => _open = open;

        internal void Load(Player player, int accountId)
        {
            using var db = _open();
            using var tx = db.Database.BeginTransaction(IsolationLevel.RepeatableRead);
            var data = db.OwnedPlayer(accountId, player.PlayerId).AsNoTracking().Include(p => p.Items).Single();
            _accountId = accountId;
            _revision = data.EconomyRevision;
            player.InventoryTracker.Load(data.Items);
            player.CurrencyTracker.Load(data);
            tx.Commit();
        }

        internal EconomyBatch Capture(Player player)
        {
            var batch = new EconomyBatch(player.PlayerId, _accountId, _revision);
            player.InventoryTracker.Snapshot(batch);
            player.CurrencyTracker.Snapshot(batch.Currency);
            return batch;
        }

        internal void EnqueueClient(Player player, Action action)
        {
            var room = player.GameRoom;
            room?.Push(() => RunInRoom(player, room, action, client: true));
        }

        internal void RunInRoom(Player player, GameRoom room, Action action, bool client = false)
        {
            if (_invalid || player.GameRoom != room || player.Session?.IsConnected == false || (client &&
                player.Session?.MyPlayer != player)) return;
            if (!_writing)
            {
                action();
                return;
            }
            // Bound client input even if the DB stalls. Trusted room events and the
            // final logout barrier must still drain, including after disconnection.
            if (client && _waiting?.Count >= MaxQueuedClientActions)
            {
                player.Session.Disconnect();
                return;
            }
            Wait(() => RunInRoom(player, room, action, client));
        }

        void Wait(Action action) => (_waiting ??= new Queue<Action>()).Enqueue(action);

        internal bool PrepareLeave(Player player, Action retry)
        {
            if (!_writing) Flush(player);
            if (!_writing) return false;
            Wait(retry);
            return true;
        }

        internal void Flush(Player player)
        {
            if (_invalid || _writing || (!player.InventoryTracker.HasChanges && !player.CurrencyTracker.HasChanges)) return;
            Submit(player, Capture(player), null, null);
        }

        internal void Submit(Player player, EconomyBatch batch, QuestGrant grant, Action confirmed)
        {
            var room = player.GameRoom;
            _writing = true;
            DbTransaction.Instance.Push(() =>
            {
                EconomyWrite outcome;
                try
                {
                    using var db = _open();
                    outcome = batch.Save(db, grant);
                }
                catch (Exception e)
                {
                    ConsoleLogManager.Instance.Log(e);
                    room.Push(() => Recover(player, room));
                    return;
                }
                room.Push(() =>
                {
                    try
                    {
                        _retryDelayMs = 1000;
                        if (outcome == EconomyWrite.StaleOwner)
                        {
                            _invalid = true;
                            player.Session?.Disconnect();
                        }
                        else if (outcome == EconomyWrite.Saved)
                        {
                            _revision++;
                            if (grant != null)
                            {
                                foreach (var (key, item) in grant.AwardedSlots)
                                {
                                    item.Count = batch.Items[item].Count;
                                    player.Items[key] = item;
                                }
                                foreach (var (type, balance) in grant.Balances)
                                    player.CurrencyTracker.SetCommitted(type, balance);
                            }
                            player.InventoryTracker.Acknowledge(batch);
                            foreach (var (type, value) in batch.Currency)
                                player.CurrencyTracker.Acknowledge(type, value);
                            confirmed?.Invoke();
                        }
                    }
                    finally { Drain(); }
                });
            });
        }

        void Recover(Player player, GameRoom room)
        {
            // Commit may have reached SQL despite a lost acknowledgement. A fresh
            // login reads the durable outcome. Preserve previously dirty gameplay
            // by checkpointing it with the original revision; CAS refuses a stale
            // write if the ambiguous commit already advanced that revision.
            try { player.Session?.Disconnect(); }
            catch (Exception e) { ConsoleLogManager.Instance.Log(e); }
            if (!player.InventoryTracker.HasChanges && !player.CurrencyTracker.HasChanges)
            {
                _invalid = true;
                Drain();
                return;
            }
            int delay = _retryDelayMs;
            _retryDelayMs = Math.Min(30000, delay * 2);
            room.PushAfter(() => Submit(player, Capture(player), null, null), delay);
        }

        void Drain()
        {
            _writing = false;
            while (!_writing && _waiting?.Count > 0)
            {
                var next = _waiting.Dequeue();
                try { next(); }
                catch (Exception e) { ConsoleLogManager.Instance.Log(e); }
            }
        }
    }
}

using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.DB;
using System.Collections.Generic;
using System.Linq;

namespace Server.Game
{
    // QuestTracker와 동일한 더티 마킹 전략.
    // 아이템 변경은 인메모리만 수행하고, 로그아웃 시 일괄 DB 반영.
    public class InventoryTracker
    {
        private readonly Player _owner;

        // 변경된 아이템 (UPDATE 또는 INSERT)
        private readonly HashSet<PlayerItemDb> _dirtyItems = new();

        // 삭제된 기존 아이템의 DB ID (DELETE)
        private readonly HashSet<PlayerItemDb> _deletedItems = new();

        public InventoryTracker(Player owner)
        {
            _owner = owner;
        }

        // 로그인 시 DB에서 아이템 목록을 로드해 인메모리 동기화
        internal void Load(IEnumerable<PlayerItemDb> items)
        {
            AmbiguousSlots.Clear();
            _owner.Items.Clear();
            _owner.EquippedItems.Clear();
            _dirtyItems.Clear();
            _deletedItems.Clear();
            foreach (PlayerItemDb item in items)
            {
                ItemMetaData meta = SpecDataManager.Instance.GetItem(item.ItemId);
                if (meta == null)
                    continue;

                if (item.IsEquipped)
                {
                    EquipmentMetaData equipMeta = SpecDataManager.Instance.GetEquipment(item.ItemId);
                    if (equipMeta != null)
                        _owner.EquippedItems[equipMeta.EquipmentSlotType] = item;
                }
                else
                {
                    var key = (meta.ItemType, item.SlotIndex);
                    if (_owner.Items.ContainsKey(key)) AmbiguousSlots.Add(key);
                    _owner.Items[key] = item;
                }
            }
        }

        // 아이템 수량 변경(추가/수정) 시 호출
        public void MarkDirty(PlayerItemDb item)
        {
            if (!_deletedItems.Contains(item))
                _dirtyItems.Add(item);
        }

        public void MarkDeleted(PlayerItemDb item)
        {
            _dirtyItems.Remove(item);
            _deletedItems.Add(item);
        }

        internal HashSet<(ItemType, int)> AmbiguousSlots { get; } = new();

        internal bool HasChanges => _dirtyItems.Count != 0 || _deletedItems.Count != 0;

        internal void Snapshot(EconomyBatch batch)
        {
            foreach (var item in _dirtyItems)
                batch.Items.Add(item, item.Snapshot());
            batch.Deleted.AddRange(_deletedItems);
        }

        internal void Acknowledge(EconomyBatch batch)
        {
            foreach (var (live, saved) in batch.Items)
            {
                live.PlayerItemDbId = saved.PlayerItemDbId;
                if (live.Count == saved.Count && live.SlotIndex == saved.SlotIndex &&
                    live.IsEquipped == saved.IsEquipped && live.EnchantLevel == saved.EnchantLevel &&
                    live.RestEnchantCount == saved.RestEnchantCount)
                    _dirtyItems.Remove(live);
            }
            foreach (var item in batch.Deleted)
                _deletedItems.Remove(item);
        }
    }
}

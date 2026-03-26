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
        private readonly HashSet<int> _deletedDbIds = new();

        public InventoryTracker(Player owner)
        {
            _owner = owner;
        }

        // 로그인 시 DB에서 아이템 목록을 로드해 인메모리 동기화
        public void Load()
        {
            _owner.Items.Clear();
            _dirtyItems.Clear();
            _deletedDbIds.Clear();

            if (_owner.PlayerId == 0)
                return;

            using GameDbContext db = new GameDbContext();
            List<PlayerItemDb> items = db.PlayerItems
                .AsNoTracking()
                .Where(i => i.PlayerDbId == _owner.PlayerId)
                .ToList();

            foreach (PlayerItemDb item in items)
            {
                ItemMetaData meta = SpecDataManager.Instance.GetItem(item.ItemId);
                if (meta == null)
                    continue;
                _owner.Items[(meta.ItemType, item.SlotIndex)] = item;
            }
        }

        // 아이템 수량 변경(추가/수정) 시 호출
        public void MarkDirty(PlayerItemDb item)
        {
            // 이미 삭제 예정인 아이템은 무시
            if (item.PlayerItemDbId > 0 && _deletedDbIds.Contains(item.PlayerItemDbId))
                return;

            _dirtyItems.Add(item);
        }

        // 아이템이 인메모리에서 제거될 때 호출
        public void MarkDeleted(PlayerItemDb item)
        {
            _dirtyItems.Remove(item);

            // DB에 존재하는 아이템만 DELETE 대상에 추가
            if (item.PlayerItemDbId > 0)
                _deletedDbIds.Add(item.PlayerItemDbId);
            // PlayerItemDbId == 0 이면 아직 DB에 없으므로 아무것도 안 해도 됨
        }

        // 로그아웃 시 호출 — 변경 사항을 DB에 일괄 저장
        public void SaveDirtyItems()
        {
            if (_dirtyItems.Count == 0 && _deletedDbIds.Count == 0)
                return;

            List<PlayerItemDb> toSave = _dirtyItems.ToList();
            List<int> toDelete = _deletedDbIds.ToList();

            _dirtyItems.Clear();
            _deletedDbIds.Clear();

            DbTransaction.SaveInventoryAsync(_owner, toSave, toDelete);
        }
    }
}

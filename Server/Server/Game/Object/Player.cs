using Google.Protobuf.Protocol;
using Microsoft.Identity.Client;
using Server.Data;
using Server.DB;
using Server.Game.Room;
using System.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server.Game
{
    public class Player : GameObject
    {
        public int PlayerId { get; set; }   // DB에 저장된 플레이어 고유 Id
        public ClientSession Session { get; set; }
        public AOIController AOI { get; set; }
        public QuestTracker QuestTracker { get; private set; }
        public CooldownTracker CooldownTracker { get; private set; }
        public InventoryTracker InventoryTracker { get; private set; }
        public CurrencyTracker CurrencyTracker { get; private set; }
        // (ItemType, SlotIndex) -> PlayerItemDb (타입별로 슬롯 번호 독립)
        public Dictionary<(ItemType, int), PlayerItemDb> Items { get; private set; } = new();

        public Player()
        {
            Init();
        }

        // 플레이어 정보 초기화
        public void Init()
        {
            ObjectType = GameObjectType.Player;
            Name = $"NameNull_Player_{ObjectState.ObjectId}";
            ObjectState.CreatureState = CreatureState.Idle;
            AOI = new AOIController(this);

            InitDbData();
        }

        public void Init(int playerId, string name)
        {
            ObjectType = GameObjectType.Player;
            Name = name;
            PlayerId = playerId;
            ObjectState.CreatureState = CreatureState.Idle;

            InitDbData();

            QuestTracker = new QuestTracker(this);
            QuestTracker.Load();

            CooldownTracker = new CooldownTracker(this);
            CooldownTracker.Load();

            InventoryTracker = new InventoryTracker(this);
            InventoryTracker.Load();

            CurrencyTracker = new CurrencyTracker(this);
            CurrencyTracker.Load();
        }

        public void InitDbData()
        {
            if (ObjectState.Stat == null)
                ObjectState.Stat = new Stat();

            using (GameDbContext db = new GameDbContext())
            {
                PlayerDb player = db.Players
                    .Where(p => p.PlayerDbId == PlayerId)
                    .FirstOrDefault();

                if (player == null)
                    return;

                Level = player.Level;
                Exp = player.Exp;

                if (!player.IsStatInitialized)
                {
                    // TODO - 현재는 직업이 기사만 있다고 가정
                    PlayerMetaData spec = SpecDataManager.Instance.GetPlayer((int)PlayerJobType.Knight);
                    if (spec != null)
                    {
                        player.StatMaxHp = spec.MaxHp;
                        player.StatCommonAttackDamage = spec.CommonAttackDamage;
                        player.StatCommonAttackCoolTime = spec.CommonAttackCoolTime;
                        player.StatAttackRange = spec.AttackRange;
                        player.StatDefense = spec.Defense;
                        player.StatMoveSpeed = spec.MoveSpeed;
                        player.StatAttackHalfAngleDeg = spec.AttackHalfAngleDeg;
                        player.StatAttackHeight = spec.AttackHeight;
                        player.IsStatInitialized = true;
                        db.SaveChangesEx();
                    }
                }

                ObjectState.Stat.MaxHp = player.StatMaxHp;
                ObjectState.Stat.Hp = player.StatMaxHp;
                ObjectState.Stat.CommonAttackDamage = player.StatCommonAttackDamage;
                ObjectState.Stat.CommonAttackCoolTime = player.StatCommonAttackCoolTime;
                ObjectState.Stat.AttackRange = player.StatAttackRange;
                ObjectState.Stat.Defense = player.StatDefense;
                ObjectState.Stat.MoveSpeed = player.StatMoveSpeed;
                ObjectState.Stat.AttackHalfAngleDeg = player.StatAttackHalfAngleDeg;
                ObjectState.Stat.AttackHeight = player.StatAttackHeight;
            }
        }

        public void HandlePickUpDropItem(int objectId)
        {
            // 1. 드롭 아이템 존재 확인
            DropItem dropItem = GameRoom.FindDropItem(objectId);
            if (dropItem == null)
            {
                Console.WriteLine($"[Player] DropItem not found: {objectId}");
                return;
            }

            // 2. 거리 확인
            float pickupRadius = ConfigManager.Instance.GetFloat(ConfigType.DropItemPickupRadius);
            float dist = Vector3.Distance(CurrentPosition, dropItem.CurrentPosition);
            if (dist > pickupRadius)
            {
                Console.WriteLine($"[Player] Too far from drop item: dist={dist:F1}, radius={pickupRadius}");
                return;
            }

            // 3. 아이템 메타데이터 확인
            ItemMetaData itemMeta = SpecDataManager.Instance.GetItem(dropItem.ItemId);
            if (itemMeta == null)
            {
                Console.WriteLine($"[Player] Invalid item id: {dropItem.ItemId}");
                return;
            }

            S_PickUpDropItem pickUpDropItemPacket = new S_PickUpDropItem();
            pickUpDropItemPacket.ItemId = dropItem.ItemId;

            // 4. 인벤토리 공간 확인
            ItemType itemType = itemMeta.ItemType;
            PlayerItemDb existingItem = Items.Values.FirstOrDefault(i => i.ItemId == dropItem.ItemId);
            if (existingItem != null)
            {
                // 기존 슬롯에 스택 가능한지 확인
                if (existingItem.Count + dropItem.Count > itemMeta.MaxStack)
                {
                    Console.WriteLine($"[Player] Item stack full: {dropItem.ItemId}");
                    pickUpDropItemPacket.PickUpDropItemResult = PickUpDropItemResult.InventoryFull;
                    Session?.Send(pickUpDropItemPacket);
                    return;
                }
            }
            else
            {
                // 새 슬롯 필요  인벤토리 여유 확인
                int usedSlots = Items.Count(i => i.Key.Item1 == itemType);
                if (usedSlots >= GetInventorySize(itemType))
                {
                    Console.WriteLine($"[Player] Inventory full: {itemType}");
                    pickUpDropItemPacket.PickUpDropItemResult = PickUpDropItemResult.InventoryFull;
                    Session?.Send(pickUpDropItemPacket);
                    return;
                }
            }

            // 5. 아이템 지급 (인메모리 업데이트 + S_UpdateItemData 전송)
            GrantItemsInMemory(new List<(int, int)> { (dropItem.ItemId, dropItem.Count) });

            // 6. 드롭 아이템 게임룸에서 제거
            GameRoom.LeaveGame(objectId);

            // 7. 줍기 성공 알림
            pickUpDropItemPacket.PickUpDropItemResult = PickUpDropItemResult.Success;
            Session?.Send(pickUpDropItemPacket);
        }

        public void HandleUseItem(ItemType itemType, int slotIndex)
        {
            if (!Items.TryGetValue((itemType, slotIndex), out PlayerItemDb playerItem))
            {
                Console.WriteLine($"[Player] Invalid inventory slot: {itemType} {slotIndex}");
                return;
            }
            Item item = ItemFactory.Create(playerItem.ItemId);

            if (item.CanUse(this, slotIndex, true) == false)
                return;

            item.Use(this, slotIndex);

            // TODO - 쿨타임 시작 (스킬이나 무기는 후에 생각)
            ItemMetaData meta = SpecDataManager.Instance.GetItem(playerItem.ItemId);
            if (meta.ItemType == ItemType.Consumable)
            {
                ConsumableMetaData consumableMeta = SpecDataManager.Instance.GetConsumable(playerItem.ItemId);
                CooldownTracker.StartCooldown(playerItem.ItemId, consumableMeta.CoolTime);

                // 수량 소비
                --playerItem.Count;
                if (playerItem.Count <= 0)
                {
                    Items.Remove((itemType, slotIndex));
                    InventoryTracker.MarkDeleted(playerItem);
                }
                else
                {
                    InventoryTracker.MarkDirty(playerItem);
                }

                // 소비된 수량 클라에게 알리기
                S_UpdateItemData updateItemDataPacket = new S_UpdateItemData
                {
                    ItemInfo = new ItemInfo
                    {
                        ItemId = playerItem.ItemId,
                        Count = playerItem.Count,
                        SlotIndex = playerItem.SlotIndex,
                        IsEquipped = playerItem.IsEquipped,
                        EnchantLevel = playerItem.EnchantLevel,
                    }
                };
                Session?.Send(updateItemDataPacket);
            }

            S_UseItem useItemPacket = new S_UseItem
            {
                SlotIndex = slotIndex,
                ItemType = itemType,
                ItemId = playerItem.ItemId,
                UseItemResult = UseItemResult.Success
            };
            Session?.Send(useItemPacket);
        }

        // DB 접근이 아닌 인메모리 활용하기 위한 용도
        // 슬롯 번호는 ItemType별로 독립적 (Equipment 0번 ≠ Consumable 0번)
        // 변경된 아이템은 InventoryTracker에 더티 마킹 → 로그아웃 시 일괄 DB 반영
        public void GrantItemsInMemory(List<(int itemId, int amount)> itemRewards)
        {
            // 타입별 사용 중인 슬롯 목록 구성 (루프 중 새로 추가되는 슬롯도 반영)
            var usedSlotsByType = new Dictionary<ItemType, HashSet<int>>();
            foreach (PlayerItemDb pi in Items.Values)
            {
                ItemType t = SpecDataManager.Instance.GetItem(pi.ItemId).ItemType;
                if (!usedSlotsByType.ContainsKey(t))
                    usedSlotsByType[t] = new HashSet<int>();
                usedSlotsByType[t].Add(pi.SlotIndex);
            }

            foreach (var (itemId, amount) in itemRewards)
            {
                PlayerItemDb memItem = Items.Values.FirstOrDefault(i => i.ItemId == itemId);
                if (memItem != null)
                {
                    memItem.Count += amount;
                }
                else
                {
                    ItemType itemType = SpecDataManager.Instance.GetItem(itemId).ItemType;
                    if (!usedSlotsByType.TryGetValue(itemType, out HashSet<int> usedSlots))
                    {
                        usedSlots = new HashSet<int>();
                        usedSlotsByType[itemType] = usedSlots;
                    }
                    int nextSlot = FindNextAvailableSlot(usedSlots);
                    usedSlots.Add(nextSlot);

                    memItem = new PlayerItemDb
                    {
                        PlayerDbId = PlayerId,
                        ItemId = itemId,
                        Count = amount,
                        SlotIndex = nextSlot
                    };
                    Items.Add((itemType, memItem.SlotIndex), memItem);
                }

                InventoryTracker.MarkDirty(memItem);

                Session?.Send(new S_UpdateItemData
                {
                    ItemInfo = new ItemInfo
                    {
                        ItemId = memItem.ItemId,
                        Count = memItem.Count,
                        SlotIndex = memItem.SlotIndex,
                        IsEquipped = memItem.IsEquipped,
                        EnchantLevel = memItem.EnchantLevel,
                    }
                });
            }
        }

        private static int FindNextAvailableSlot(HashSet<int> usedSlots)
        {
            for (int slot = 0; ; ++slot)
            {
                if (!usedSlots.Contains(slot))
                    return slot;
            }
        }

        public override void OnDamaged(GameObject instigator, float damage)
        {
            base.OnDamaged(instigator, damage);
        }

        public void OnLeaveGame()
        {
            QuestTracker.SaveDirtyQuests();
            InventoryTracker.SaveDirtyItems();
            CurrencyTracker.SaveDirtyCurrencies();
            DbTransaction.SavePlayerStatsAsync(this);
        }

        
        public int GetInventorySize(ItemType itemType)
        {
            // 플레이어가 가진 가방 사이즈 (TODO - 늘릴 수 있으니 수정하기)    

            switch (itemType)
            {
                case ItemType.Equipment:
                    return ConfigManager.Instance.GetInt(ConfigType.DefaultEquimentInventorySize);
                case ItemType.Consumable:
                    return ConfigManager.Instance.GetInt(ConfigType.DefaultConsumableInventorySize);
                case ItemType.Misc:
                    return ConfigManager.Instance.GetInt(ConfigType.DefaultMiscInventorySize);
            }

            return 0;
        }
    }
}

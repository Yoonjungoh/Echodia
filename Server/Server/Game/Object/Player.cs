using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Server.Data;
using Server.DB;
using Server.Game.Room;
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
        public List<PlayerItemDb> Items { get; private set; } = new();

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
        }

        // TODO JSON - PlayerType에 따른 stat 변경
        public void InitDbData()
        {
            if (ObjectState.Stat == null)
                ObjectState.Stat = new Stat();

            using (GameDbContext db = new GameDbContext())
            {
                PlayerDb player = db.Players
                    .Include(p => p.Items)
                    .AsNoTracking()
                    .Where(p => p.PlayerDbId == PlayerId)
                    .FirstOrDefault();

                if (player == null)
                    return;

                Level = player.Level;
                Exp = player.Exp;
                Items = player.Items?.ToList() ?? new();

                // TODO - DB에서 가져오기
                ObjectState.Stat.MaxHp = 100.0f;
                ObjectState.Stat.Hp = ObjectState.Stat.MaxHp;
                ObjectState.Stat.CommonAttackDamage = 30.0f;
                ObjectState.Stat.Defense = 0.0f;
                ObjectState.Stat.MoveSpeed = 7.0f;
                ObjectState.Stat.CommonAttackCoolTime = 2.0f;
                ObjectState.Stat.AttackRange = 10.0f;
                ObjectState.Stat.AttackHalfAngleDeg = 30.0f;
                ObjectState.Stat.AttackHeight = 10.0f;
            }

            QuestTracker = new QuestTracker(this);
            QuestTracker.Load();
        }

        // DB 접근이 아닌 인메모리 활용하기 위한 용도
        // ItemType별 슬롯을 player.Items 기반으로 계산하고 인메모리 상태를 즉시 업데이트
        // 반환값: DB에 저장할 PlayerItemDb 목록 (PlayerItemDbId > 0이면 UPDATE, == 0이면 INSERT)
        public List<PlayerItemDb> GrantItemsInMemory(List<(int itemId, int amount)> itemRewards)
        {
            var usedSlotsByType = new Dictionary<ItemType, HashSet<int>>();
            foreach (PlayerItemDb pi in Items)
            {
                ItemType t = SpecDataManager.Instance.GetItem(pi.ItemId)?.ItemType ?? ItemType.None;
                if (!usedSlotsByType.ContainsKey(t))
                    usedSlotsByType[t] = new HashSet<int>();

                usedSlotsByType[t].Add(pi.SlotIndex);
            }

            var toSave = new List<PlayerItemDb>();
            foreach (var (itemId, amount) in itemRewards)
            {
                PlayerItemDb memItem = Items.FirstOrDefault(i => i.ItemId == itemId);
                if (memItem != null)
                {
                    memItem.Count += amount;
                }
                else
                {
                    ItemType itemType = SpecDataManager.Instance.GetItem(itemId)?.ItemType ?? ItemType.None;
                    if (!usedSlotsByType.TryGetValue(itemType, out HashSet<int> usedSlots))
                    {
                        usedSlots = new HashSet<int>();
                        usedSlotsByType[itemType] = usedSlots;
                    }
                    int nextSlot = FindNextAvailableSlot(usedSlots);
                    usedSlots.Add(nextSlot);

                    memItem = new PlayerItemDb { PlayerDbId = PlayerId, ItemId = itemId, Count = amount, SlotIndex = nextSlot };
                    Items.Add(memItem);
                }

                toSave.Add(memItem);

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
            return toSave;
        }

        private static int FindNextAvailableSlot(HashSet<int> usedSlots)
        {
            for (int slot = 0; ; slot++)
                if (!usedSlots.Contains(slot)) return slot;
        }

        public override void OnDamaged(GameObject instigator, float damage)
        {
            base.OnDamaged(instigator, damage);
        }

        public void OnLeaveGame()
        {
            QuestTracker.SaveDirtyQuests();
        }
    }
}

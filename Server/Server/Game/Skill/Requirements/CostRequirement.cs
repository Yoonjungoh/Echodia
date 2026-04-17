using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Game.Skill.Requirements
{
    /// <summary>
    /// 스킬 사용 비용(Mp, Hp, Gold, Jewel, Item)을 검사하고 소비한다.
    /// </summary>
    public class CostRequirement : ISkillRequirement
    {
        private readonly List<SkillCostMetaData> _costs;

        public CostRequirement(List<SkillCostMetaData> costs)
        {
            _costs = costs;
        }

        public bool Check(Player player)
        {
            foreach (SkillCostMetaData cost in _costs)
            {
                switch (cost.CostType)
                {
                    case SkillCostType.Mp:
                        if (player.Stat.Mp < cost.Amount)
                        {
                            return false;
                        }
                        break;
                    case SkillCostType.Hp:
                        // Hp 비용으로 인해 사망하는 상황은 허용하지 않음
                        if (player.Stat.Hp <= cost.Amount)
                        {
                            return false;
                        }
                        break;
                    case SkillCostType.Gold:
                        if (player.CurrencyTracker.Get(CurrencyType.Gold) < cost.Amount)
                        {
                            return false;
                        }
                        break;
                    case SkillCostType.Jewel:
                        if (player.CurrencyTracker.Get(CurrencyType.Jewel) < cost.Amount)
                        {
                            return false;
                        }
                        break;
                    case SkillCostType.Item:
                        if (!HasItem(player, cost.ItemId, cost.Amount))
                        {
                            return false;
                        }
                        break;
                }
            }
            return true;
        }

        public void Consume(Player player)
        {
            foreach (SkillCostMetaData cost in _costs)
            {
                switch (cost.CostType)
                {
                    case SkillCostType.Mp:
                        player.ConsumeMp(cost.Amount);
                        break;
                    case SkillCostType.Hp:
                        player.SetHp(player.Stat.Hp - cost.Amount);
                        break;
                    case SkillCostType.Gold:
                        player.CurrencyTracker.Set(
                            CurrencyType.Gold,
                            player.CurrencyTracker.Get(CurrencyType.Gold) - cost.Amount);
                        break;
                    case SkillCostType.Jewel:
                        player.CurrencyTracker.Set(
                            CurrencyType.Jewel,
                            player.CurrencyTracker.Get(CurrencyType.Jewel) - cost.Amount);
                        break;
                    case SkillCostType.Item:
                        ConsumeItem(player, cost.ItemId, cost.Amount);
                        break;
                }
            }
        }

        private bool HasItem(Player player, int itemId, int requiredAmount)
        {
            int total = player.Items.Values
                .Where(i => i.ItemId == itemId)
                .Sum(i => i.Count);
            return total >= requiredAmount;
        }

        private void ConsumeItem(Player player, int itemId, int amount)
        {
            int remaining = amount;
            var slots = player.Items
                .Where(kv => kv.Value.ItemId == itemId)
                .OrderBy(kv => kv.Value.Count)
                .ToList();

            foreach (var kv in slots)
            {
                if (remaining <= 0)
                {
                    break;
                }

                int consume = Math.Min(kv.Value.Count, remaining);
                kv.Value.Count -= consume;
                remaining -= consume;

                if (kv.Value.Count <= 0)
                {
                    player.Items.Remove(kv.Key);
                }

                player.InventoryTracker.MarkDirty(kv.Value);
            }
        }
    }
}

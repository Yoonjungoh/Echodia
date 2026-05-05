using Google.Protobuf.Protocol;
using Server.Currency;
using Server.Game;
using System;
using System.Collections.Generic;

namespace Server
{
    public class KillRewardManager
    {
        public static KillRewardManager Instance { get; } = new KillRewardManager();

        // Monster.OnDead에서 호출하는 진입점
        public void OnMonsterKilled(
            GameRoom room,
            Player killer,
            Dictionary<int, int> damageContributions,   // playerId → 입힌 데미지
            int totalExp,
            int totalGold,
            int monsterTemplateId)
        {
            List<(Player player, int exp, int gold)> shares =
                ComputeShares(room, killer, damageContributions, totalExp, totalGold);

            foreach ((Player player, int exp, int gold) in shares)
            {
                if (exp > 0)
                {
                    GrantExp(player, exp);
                }

                if (gold > 0)
                {
                    GrantGold(player, gold);
                }
            }

            // 퀘스트 크레딧은 보상 받은 플레이어 전체에게 동일하게 적용
            var credited = new List<Player>(shares.Count);
            foreach ((Player player, int _, int __) in shares)
                credited.Add(player);

            QuestManager.Instance.OnMonsterKilled(room, credited, monsterTemplateId);
        }

        // 보상 받을 플레이어 목록과 각자 지분 계산
        private List<(Player player, int exp, int gold)> ComputeShares(
            GameRoom room,
            Player killer,
            Dictionary<int, int> damageContributions,
            int totalExp,
            int totalGold)
        {
            Party killerParty = PartyManager.Instance.GetPartyOf(killer.PlayerId);

            var eligible = new List<(Player player, int damage)>();
            int totalPartyDamage = 0;

            foreach (KeyValuePair<int, int> kv in damageContributions)
            {
                int playerId = kv.Key;
                int damage = kv.Value;

                Player p = room.FindPlayerByPlayerId(playerId);
                if (p == null)
                    continue;   // 방 이탈

                if (killerParty == null)
                {
                    // 솔로킬 → 킬러만
                    if (p.PlayerId != killer.PlayerId)
                        continue;
                }
                else
                {
                    // 같은 파티원만
                    if (!killerParty.Contains(p.PlayerId))
                        continue;
                }

                eligible.Add((p, damage));
                totalPartyDamage += damage;
            }

            // 폴백: 아무도 기여 기록이 없으면 킬러에게 전부
            if (eligible.Count == 0)
            {
                return new List<(Player, int, int)> { (killer, totalExp, totalGold) };
            }

            // Math.Floor 비례 분배, 나머지는 최고 기여자에게
            var shares = new List<(Player player, int exp, int gold)>(eligible.Count);
            int expRemaining = totalExp;
            int goldRemaining = totalGold;
            Player topPlayer = null;
            int topDamage = -1;

            foreach ((Player p, int damage) in eligible)
            {
                float ratio = (float)damage / totalPartyDamage;
                int pExp = (int)Math.Floor(totalExp * ratio);
                int pGold = (int)Math.Floor(totalGold * ratio);
                expRemaining -= pExp;
                goldRemaining -= pGold;
                shares.Add((p, pExp, pGold));

                if (damage > topDamage)
                {
                    topDamage = damage;
                    topPlayer = p;
                }
            }

            // 반올림 오차 잔량을 최고 기여자에게
            if ((expRemaining > 0 || goldRemaining > 0) && topPlayer != null)
            {
                for (int i = 0; i < shares.Count; i++)
                {
                    if (shares[i].player == topPlayer)
                    {
                        shares[i] = (topPlayer, shares[i].exp + expRemaining, shares[i].gold + goldRemaining);
                        break;
                    }
                }
            }

            return shares;
        }

        // 기존 GameObject.OnDead EXP 로직 그대로 이관
        private void GrantExp(Player player, int amount)
        {
            int newTotal = player.Exp + amount;
            int levelUpCount = player.SetExp(newTotal, needLevelUp: true);

            CurrencyManager.Instance.SetCurrency(player, CurrencyType.Exp, player.Exp);
            ConsoleLogManager.Instance.Log($"[Reward] {player.Name} +{amount} EXP (total: {player.Exp})");

            if (levelUpCount > 0)
            {
                CurrencyManager.Instance.AddCurrency(player, CurrencyType.Level, levelUpCount);
                ConsoleLogManager.Instance.Log($"[Reward] {player.Name} leveled up! Level: {player.Level}");
            }
        }

        private void GrantGold(Player player, int amount)
        {
            CurrencyManager.Instance.AddCurrency(player, CurrencyType.Gold, amount);
            ConsoleLogManager.Instance.Log($"[Reward] {player.Name} +{amount} Gold");
        }
    }
}

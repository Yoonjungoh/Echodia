using Google.Protobuf.Protocol;
using Server.Data;
using Server.DB;
using System;
using System.Collections.Generic;

namespace Server.Game
{    
    /// <summary>
    /// 직업 -> 주/부 스탯 매핑
    ///   Warrior(Knight) : 주 STR, 부 DEX
    ///   Archer          : 주 DEX, 부 STR
    ///   Thief           : 주 LUK, 부 DEX
    ///   Mage            : 주 INT, 부 LUK
    /// </summary>
    public class PlayerStatCalculator
    {
        private readonly Player _player;
        private static readonly Random _rand = new Random();

        public PlayerStatCalculator(Player player)
        {
            _player = player;
        }

        // GetTotalXXX() 메서드들은 기본 스탯 + 장착 장비의 보너스까지 포함한 최종 스탯을 계산해서 반환
        public int GetTotalSTR()
        {
            int total = _player.Stat.Str;
            foreach (EquipmentMetaData eq in GetEquippedEquipments())
            {
                total += eq.BaseSTR;
            }
            return total;
        }

        public int GetTotalDEX()
        {
            int total = _player.Stat.Dex;
            foreach (EquipmentMetaData eq in GetEquippedEquipments())
            {
                total += eq.BaseDEX;
            }
            return total;
        }

        public int GetTotalINT()
        {
            int total = _player.Stat.StatInt;
            foreach (EquipmentMetaData eq in GetEquippedEquipments())
            {
                total += eq.BaseINT;
            }
            return total;
        }

        public int GetTotalLUK()
        {
            int total = _player.Stat.Luk;
            foreach (EquipmentMetaData eq in GetEquippedEquipments())
            {
                total += eq.BaseLUK;
            }
            return total;
        }

        public int GetTotalPhysicalDamage()
        {
            int total = _player.Stat.PhysicalDamage;
            foreach (EquipmentMetaData eq in GetEquippedEquipments())
            {
                total += eq.PhysicalDamage;
            }
            return total;
        }

        public int GetTotalMagicDamage()
        {
            int total = _player.Stat.MagicDamage;
            foreach (EquipmentMetaData eq in GetEquippedEquipments())
            {
                total += eq.MagicDamage;
            }
            return total;
        }

        // 최종 데미지와 크리티컬 여부를 함께 반환
        // forceCritical = true이면 크리티컬 확정
        public (int damage, bool isCritical) GetFinalDamage(bool forceCritical = false)
        {
            float base_ = CalculateBaseDamage(_player.Stat.JobType);
            // CriticalRate는 만분율 (5000 = 50%). Next(10000)으로 0.01% 단위 정밀도 확보
            bool isCritical = forceCritical || (_rand.Next(10000) < _player.Stat.CriticalRate);

            if (isCritical)
                base_ *= _player.Stat.CriticalDamage;

            return (Math.Max(0, (int)base_), isCritical);
        }

        private float CalculateBaseDamage(PlayerJobType jobType)
        {
            return jobType switch
            {
                // 물리 직업 ──────────────────────────────────────────────
                PlayerJobType.Warrior =>    // Knight: 주 STR, 부 DEX
                    (GetTotalSTR() * 4 + GetTotalDEX()) + (GetTotalPhysicalDamage()),

                PlayerJobType.Archer =>     // 주 DEX, 부 STR
                    (GetTotalDEX() * 4 + GetTotalSTR()) + (GetTotalPhysicalDamage()),

                PlayerJobType.Thief =>      // 주 LUK, 부 DEX
                    (GetTotalLUK() * 4 + GetTotalDEX()) + (GetTotalPhysicalDamage()),

                // 마법 직업 ──────────────────────────────────────────────
                PlayerJobType.Mage =>       // 주 INT, 부 LUK
                    (GetTotalINT() * 4 + GetTotalLUK()) + (GetTotalMagicDamage()),

                _ => 0f,
            };
        }

        private List<EquipmentMetaData> GetEquippedEquipments()
        {
            List<EquipmentMetaData> equippedEquipments = new List<EquipmentMetaData>();
            foreach (var kvp in _player.Items)
            {
                PlayerItemDb item = kvp.Value;
                if (!item.IsEquipped)
                    continue;

                EquipmentMetaData meta = SpecDataManager.Instance.GetEquipment(item.ItemId);
                if (meta != null)
                {
                    equippedEquipments.Add(meta);
                }
            }
            return equippedEquipments;
        }
    }
}

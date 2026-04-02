using Google.Protobuf.Protocol;
using Server.Data;
using Server.DB;
using System;
using System.Collections.Generic;

namespace Server.Game
{
    /// <summary>
    /// 메이플스토리 방식의 플레이어 데미지 계산기.
    /// 베이스 스탯(DB 저장) + 장착 장비 보너스를 합산하여 최종 데미지를 반환한다.
    ///
    /// 공식:
    ///   물리 직업 : (주스탯 * 4 + 부스탯) * (총PhysicalDamage / 100)
    ///   마법 직업 : (INT   * 4 + LUK  ) * (총MagicDamage   / 100)
    ///
    /// 직업 -> 주/부 스탯 매핑
    ///   Warrior(Knight) : 주 STR, 부 DEX
    ///   Archer          : 주 DEX, 부 STR
    ///   Thief           : 주 LUK, 부 DEX
    ///   Mage            : 주 INT, 부 LUK
    /// </summary>
    public class PlayerStatCalculator
    {
        private readonly Player _player;
        private static readonly Random _rng = new Random();

        public PlayerStatCalculator(Player player)
        {
            _player = player;
        }

        // ── 합산 스탯 ──────────────────────────────────────────────────

        /// <summary>베이스 STR + 장착 장비의 BaseSTR 합계</summary>
        public int GetTotalSTR()
        {
            int total = _player.Stat.Str;
            foreach (EquipmentMetaData eq in GetEquippedEquipments())
            {
                total += eq.BaseSTR;
            }
            return total;
        }

        /// <summary>베이스 DEX + 장착 장비의 BaseDEX 합계</summary>
        public int GetTotalDEX()
        {
            int total = _player.Stat.Dex;
            foreach (EquipmentMetaData eq in GetEquippedEquipments())
            {
                total += eq.BaseDEX;
            }
            return total;
        }

        /// <summary>베이스 INT + 장착 장비의 BaseINT 합계</summary>
        public int GetTotalINT()
        {
            int total = _player.Stat.StatInt;
            foreach (EquipmentMetaData eq in GetEquippedEquipments())
            {
                total += eq.BaseINT;
            }
            return total;
        }

        /// <summary>베이스 LUK + 장착 장비의 BaseLUK 합계</summary>
        public int GetTotalLUK()
        {
            int total = _player.Stat.Luk;
            foreach (EquipmentMetaData eq in GetEquippedEquipments())
            {
                total += eq.BaseLUK;
            }
            return total;
        }

        /// <summary>베이스 PhysicalDamage + 장착 장비의 PhysicalDamage 합계</summary>
        public int GetTotalPhysicalDamage()
        {
            int total = _player.Stat.PhysicalDamage;
            foreach (EquipmentMetaData eq in GetEquippedEquipments())
            {
                total += eq.PhysicalDamage;
            }
            return total;
        }

        /// <summary>베이스 MagicDamage + 장착 장비의 MagicDamage 합계</summary>
        public int GetTotalMagicDamage()
        {
            int total = _player.Stat.MagicDamage;
            foreach (EquipmentMetaData eq in GetEquippedEquipments())
            {
                total += eq.MagicDamage;
            }
            return total;
        }

        // ── 최종 데미지 ────────────────────────────────────────────────

        /// <summary>
        /// 최종 출력 데미지를 계산한다. 방어율은 포함하지 않는다.
        /// </summary>
        /// <param name="isCritical">크리티컬 여부 (호출 측에서 CriticalRate 확률 판정 후 전달)</param>
        public int GetFinalDamage(bool isCritical)
        {
            float base_ = CalculateBaseDamage(_player.Stat.JobType);

            if (isCritical)
            {
                base_ *= _player.Stat.CriticalDamage;
            }

            return Math.Max(0, (int)base_);
        }

        /// <summary>
        /// CriticalRate 확률을 내부에서 판정하고 최종 데미지를 반환한다.
        /// 크리티컬 여부는 out 파라미터로 확인할 수 있다.
        /// </summary>
        public int GetFinalDamage(out bool isCritical)
        {
            isCritical = (_rng.NextDouble() < _player.Stat.CriticalRate);
            return GetFinalDamage(isCritical);
        }

        // ── 내부 헬퍼 ─────────────────────────────────────────────────

        private float CalculateBaseDamage(PlayerJobType jobType)
        {
            return jobType switch
            {
                // 물리 직업 ──────────────────────────────────────────────
                PlayerJobType.Warrior =>    // Knight: 주 STR, 부 DEX
                    (GetTotalSTR() * 4 + GetTotalDEX()) * (GetTotalPhysicalDamage() / 100f),

                PlayerJobType.Archer =>     // 주 DEX, 부 STR
                    (GetTotalDEX() * 4 + GetTotalSTR()) * (GetTotalPhysicalDamage() / 100f),

                PlayerJobType.Thief =>      // 주 LUK, 부 DEX
                    (GetTotalLUK() * 4 + GetTotalDEX()) * (GetTotalPhysicalDamage() / 100f),

                // 마법 직업 ──────────────────────────────────────────────
                PlayerJobType.Mage =>       // 주 INT, 부 LUK
                    (GetTotalINT() * 4 + GetTotalLUK()) * (GetTotalMagicDamage() / 100f),

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

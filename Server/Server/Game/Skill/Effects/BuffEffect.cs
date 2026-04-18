using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;

namespace Server.Game.Skill.Effects
{
    public class BuffEffect : ISkillEffect
    {
        private readonly SkillBuffDetailMetaData _detail;

        public BuffEffect(SkillBuffDetailMetaData detail)
        {
            _detail = detail;
        }

        public IReadOnlyList<DamagedInfo> Execute(Player caster, List<GameObject> targets, float chargeMultiplier = 1.0f)
        {
            List<GameObject> buffTargets = targets.Count > 0 ? targets : new List<GameObject> { caster };
            foreach (GameObject target in buffTargets)
                ApplyBuff(target);

            return Array.Empty<DamagedInfo>();
        }

        private void ApplyBuff(GameObject target)
        {
            int delta = CalculateDelta(target);
            ModifyStat(target, delta);
            // TODO: Duration 이후 원복 처리 (JobSerializer 기반 지연 실행으로 구현 예정)
        }

        private int CalculateDelta(GameObject target)
        {
            float baseValue = GetStatValue(target);
            if (_detail.ValueType == BuffValueType.Percent)
                return (int)(baseValue * _detail.Value);
            else
                return (int)_detail.Value;
        }

        private float GetStatValue(GameObject target)
        {
            return _detail.BuffTargetStat switch
            {
                StatType.Str            => target.Stat.Str,
                StatType.Dex            => target.Stat.Dex,
                StatType.Int            => target.Stat.StatInt,
                StatType.Luk            => target.Stat.Luk,
                StatType.Hp             => target.Stat.Hp,
                StatType.Mp             => target.Stat.Mp,
                StatType.PhysicalDamage => target.Stat.PhysicalDamage,
                StatType.MagicDamage    => target.Stat.MagicDamage,
                StatType.Defense        => target.Stat.Defense,
                StatType.MoveSpeed      => target.Stat.MoveSpeed,
                StatType.CriticalRate   => target.Stat.CriticalRate,
                StatType.CriticalDamage => target.Stat.CriticalDamage,
                _                       => 0f,
            };
        }

        private void ModifyStat(GameObject target, int delta)
        {
            switch (_detail.BuffTargetStat)
            {
                case StatType.Str:            target.Stat.Str            += delta; break;
                case StatType.Dex:            target.Stat.Dex            += delta; break;
                case StatType.Int:            target.Stat.StatInt        += delta; break;
                case StatType.Luk:            target.Stat.Luk            += delta; break;
                case StatType.PhysicalDamage: target.Stat.PhysicalDamage += delta; break;
                case StatType.MagicDamage:    target.Stat.MagicDamage    += delta; break;
                case StatType.Defense:        target.Stat.Defense        += delta; break;
                case StatType.MoveSpeed:      target.Stat.MoveSpeed      += delta; break;
                case StatType.CriticalRate:   target.Stat.CriticalRate   += delta; break;
                case StatType.Hp: target.HealHp(delta); return;
                case StatType.Mp:
                    if (target is Player p) p.HealMp(delta);
                    return;
            }
        }
    }
}

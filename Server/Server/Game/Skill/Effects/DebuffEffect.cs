using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;

namespace Server.Game.Skill.Effects
{
    public class DebuffEffect : ISkillEffect
    {
        private readonly SkillDebuffDetailMetaData _detail;

        public DebuffEffect(SkillDebuffDetailMetaData detail)
        {
            _detail = detail;
        }

        public IReadOnlyList<DamagedInfo> Execute(Player caster, List<GameObject> targets, float chargeMultiplier = 1.0f)
        {
            foreach (GameObject target in targets)
                ApplyDebuff(caster, target);

            return Array.Empty<DamagedInfo>();
        }

        private void ApplyDebuff(Player caster, GameObject target)
        {
            switch (_detail.DebuffType)
            {
                case DebuffType.Stun:
                    // TODO: target.StatusEffects.AddStun(_detail.Duration)
                    break;
                case DebuffType.Slow:
                    // TODO: target.StatusEffects.AddSlow(_detail.Value, _detail.Duration)
                    break;
                case DebuffType.Poison:
                    // TODO: target.StatusEffects.AddPoison(_detail.DamagePerTick, _detail.Duration)
                    break;
                case DebuffType.Bleed:
                    // TODO: target.StatusEffects.AddBleed(_detail.DamagePerTick, _detail.Duration)
                    break;
            }
        }
    }
}

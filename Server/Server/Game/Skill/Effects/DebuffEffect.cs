using Server.Data;
using System.Collections.Generic;

namespace Server.Game.Skill.Effects
{
    /// <summary>
    /// 대상에게 상태이상(Stun, Slow, Poison, Bleed)을 부여한다.
    /// TODO: 실제 상태이상 시스템(StatusEffectTracker)과 연결 예정.
    /// </summary>
    public class DebuffEffect : ISkillEffect
    {
        private readonly SkillDebuffDetailMetaData _detail;

        public DebuffEffect(SkillDebuffDetailMetaData detail)
        {
            _detail = detail;
        }

        public void Execute(Player caster, List<GameObject> targets)
        {
            foreach (GameObject target in targets)
            {
                ApplyDebuff(caster, target);
            }
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
                    // TODO: target.StatusEffects.AddPoison(_detail.Value, _detail.Duration)
                    break;
                case DebuffType.Bleed:
                    // TODO: target.StatusEffects.AddBleed(_detail.Value, _detail.Duration)
                    break;
            }
        }
    }
}

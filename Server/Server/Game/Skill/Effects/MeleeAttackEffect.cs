using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;

namespace Server.Game.Skill.Effects
{
    public class MeleeAttackEffect : ISkillEffect
    {
        private readonly SkillAttackDetailMetaData _detail;

        public MeleeAttackEffect(SkillAttackDetailMetaData detail)
        {
            _detail = detail;
        }

        public IReadOnlyList<DamagedInfo> Execute(Player caster, List<GameObject> targets, float chargeMultiplier = 1.0f)
        {
            if (targets.Count == 0)
                return Array.Empty<DamagedInfo>();

            var damagedList = new List<DamagedInfo>(targets.Count);
            foreach (GameObject target in targets)
            {
                (int baseDamage, bool isCritical) = caster.StatCalculator.GetFinalDamage();
                int damage = (int)(baseDamage * _detail.DamageCoefficient * chargeMultiplier);

                target.OnDamaged(caster, damage);

                damagedList.Add(new DamagedInfo
                {
                    ObjectId   = target.Id,
                    RemainHp   = target.Stat.Hp,
                    IsCritical = isCritical,
                });
            }
            return damagedList;
        }
    }
}

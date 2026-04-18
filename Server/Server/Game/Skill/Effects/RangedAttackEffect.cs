using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;

namespace Server.Game.Skill.Effects
{
    public class RangedAttackEffect : ISkillEffect
    {
        private readonly SkillAttackDetailMetaData _detail;

        public RangedAttackEffect(SkillAttackDetailMetaData detail)
        {
            _detail = detail;
        }

        public IReadOnlyList<DamagedInfo> Execute(Player caster, List<GameObject> targets, float chargeMultiplier = 1.0f)
        {
            ProjectileType projectileType = (ProjectileType)_detail.ProjectileTypeId;
            float coefficient = _detail.DamageCoefficient * chargeMultiplier;
            caster.GameRoom.Push(caster.GameRoom.SpawnProjectile, caster.Id, projectileType, coefficient);
            return Array.Empty<DamagedInfo>();
        }
    }
}

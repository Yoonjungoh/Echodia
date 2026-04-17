using Google.Protobuf.Protocol;
using Server.Data;
using System.Collections.Generic;

namespace Server.Game.Skill.Effects
{
    /// <summary>
    /// 투사체를 소환한다. 데미지 처리는 투사체 충돌 시 기존 HandleProjectileAttack 흐름을 따른다.
    /// MagicMissile처럼 기존 Projectile 시스템과 자연스럽게 연결된다.
    /// </summary>
    public class RangedAttackEffect : ISkillEffect
    {
        private readonly SkillAttackDetailMetaData _detail;

        public RangedAttackEffect(SkillAttackDetailMetaData detail)
        {
            _detail = detail;
        }

        public void Execute(Player caster, List<GameObject> targets)
        {
            // targets는 원거리 공격에서 사용하지 않음 (투사체가 직접 충돌 감지)
            ProjectileType projectileType = (ProjectileType)_detail.ProjectileTypeId;
            caster.GameRoom.Push(caster.GameRoom.SpawnProjectile, caster.Id, projectileType);
        }
    }
}

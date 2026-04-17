using Google.Protobuf.Protocol;
using Server.Data;
using System.Collections.Generic;

namespace Server.Game.Skill.Effects
{
    /// <summary>
    /// 범위 내 대상에게 직접 근접 데미지를 입힌다.
    /// DamageCoefficient 배율이 플레이어 기본 데미지에 곱해진다.
    /// </summary>
    public class MeleeAttackEffect : ISkillEffect
    {
        private readonly SkillAttackDetailMetaData _detail;

        public MeleeAttackEffect(SkillAttackDetailMetaData detail)
        {
            _detail = detail;
        }

        public void Execute(Player caster, List<GameObject> targets)
        {
            if (targets.Count == 0)
            {
                return;
            }

            S_UseSkill packet = new S_UseSkill
            {
                CasterId = caster.Id,
                SkillId = 0, // SkillExecutor에서 채워 넣음
                Result = UseSkillResult.Success,
            };

            foreach (GameObject target in targets)
            {
                (int baseDamage, bool isCritical) = caster.StatCalculator.GetFinalDamage();
                int damage = (int)(baseDamage * _detail.DamageCoefficient);

                target.OnDamaged(caster, damage);

                packet.DamagedList.Add(new DamagedInfo
                {
                    ObjectId = target.Id,
                    RemainHp = target.Stat.Hp,
                    IsCritical = isCritical,
                });
            }

            caster.GameRoom.Broadcast(caster.CurrentPosition, packet);
        }
    }
}

using Google.Protobuf.Protocol;
using Server.Data;
using Server.Game.Skill.Effects;
using Server.Game.Skill.Requirements;
using System.Collections.Generic;
using System.Linq;

namespace Server.Game.Skill
{
    /// <summary>
    /// SkillMetaData를 읽어 ISkillRequirement[]와 ISkillEffect[] 배열을 조합한다.
    /// SkillExecutor가 스킬별로 캐싱하여 재사용한다.
    /// </summary>
    public static class SkillFactory
    {
        public static ISkillRequirement[] BuildRequirements(SkillMetaData skill)
        {
            var list = new List<ISkillRequirement>();

            if (skill.RequiredLevel > 0)
            {
                list.Add(new LevelRequirement(skill.RequiredLevel));
            }

            List<Google.Protobuf.Protocol.PlayerJobType> allowedJobs = skill.RequiredJobs;
            if (allowedJobs.Count > 0)
            {
                list.Add(new JobRequirement(allowedJobs));
            }

            List<SkillCostMetaData> costs = SpecDataManager.Instance
                .GetAllSkillCost()
                .Where(c => c.SkillId == skill.Id)
                .ToList();

            if (costs.Count > 0)
            {
                list.Add(new CostRequirement(costs));
            }

            return list.ToArray();
        }

        public static ISkillEffect[] BuildEffects(SkillMetaData skill)
        {
            var list = new List<ISkillEffect>();

            List<SkillActionMetaData> actions = SpecDataManager.Instance
                .GetAllSkillAction()
                .Where(a => a.SkillId == skill.Id)
                .OrderBy(a => a.DelayMs)
                .ToList();

            foreach (SkillActionMetaData action in actions)
            {
                ISkillEffect effect = BuildEffect(action);
                if (effect != null)
                {
                    list.Add(effect);
                }
            }

            return list.ToArray();
        }

        private static ISkillEffect BuildEffect(SkillActionMetaData action)
        {
            switch (action.ActionType)
            {
                case SkillActionType.Damage:
                case SkillActionType.SpawnProjectile:
                {
                    SkillAttackDetailMetaData detail = SpecDataManager.Instance.GetSkillAttackDetail(action.SkillDetailId);
                    if (detail == null)
                    {
                        return null;
                    }

                    return action.ActionType == SkillActionType.SpawnProjectile || detail.AttackKind == SkillAttackKind.Ranged
                        ? new RangedAttackEffect(detail)
                        : new MeleeAttackEffect(detail);
                }
                case SkillActionType.Buff:
                {
                    SkillBuffDetailMetaData detail = SpecDataManager.Instance.GetSkillBuffDetail(action.SkillDetailId);
                    if (detail == null)
                    {
                        return null;
                    }
                    return new BuffEffect(detail);
                }
                case SkillActionType.Debuff:
                {
                    SkillDebuffDetailMetaData detail = SpecDataManager.Instance.GetSkillDebuffDetail(action.SkillDetailId);
                    if (detail == null)
                    {
                        return null;
                    }
                    return new DebuffEffect(detail);
                }
                default:
                    return null;
            }
        }
    }
}

using Google.Protobuf.Protocol;
using Server.Data;
using Server.Game.Skill.Effects;
using Server.Game.Skill.Requirements;
using Server.Game.Skill.Targeting;
using System.Collections.Generic;

namespace Server.Game.Skill
{
    /// <summary>
    /// 플레이어에 붙는 스킬 실행 컴포넌트.
    /// SkillFactory가 MetaData로부터 빌드한 Requirement/Effect 배열을 캐싱하여 재사용한다.
    /// </summary>
    public class SkillExecutor
    {
        private readonly Player _owner;

        // skillId → (requirements, effects, skillMeta) 캐시
        private readonly Dictionary<int, (ISkillRequirement[] reqs, ISkillEffect[] effects, SkillMetaData meta)> _cache
            = new Dictionary<int, (ISkillRequirement[], ISkillEffect[], SkillMetaData)>();

        public SkillExecutor(Player owner)
        {
            _owner = owner;
        }

        // ────────────────────────────────────────────────────────
        // 공개 API
        // ────────────────────────────────────────────────────────

        /// <summary>현재 스킬 상태를 반환한다.</summary>
        public SkillState GetState(int skillId)
        {
            if (_owner.CooldownTracker.IsOnCooldown(skillId))
            {
                return SkillState.Cooldown;
            }
            return SkillState.Ready;
        }

        /// <summary>스킬 사용 가능 여부를 검사한다 (쿨타임 + 모든 Requirement).</summary>
        public UseSkillResult CanUse(int skillId)
        {
            if (GetState(skillId) == SkillState.Cooldown)
            {
                return UseSkillResult.Cooldown;
            }

            var (reqs, _, _) = GetOrBuildCache(skillId);
            if (reqs == null)
            {
                return UseSkillResult.WrongState;
            }

            foreach (ISkillRequirement req in reqs)
            {
                if (!req.Check(_owner))
                {
                    return RequirementToResult(req);
                }
            }

            return UseSkillResult.Success;
        }

        /// <summary>타겟을 계산하여 반환한다.</summary>
        public List<GameObject> GetTargets(int skillId)
        {
            var (_, _, meta) = GetOrBuildCache(skillId);
            if (meta == null)
            {
                return new List<GameObject>();
            }

            return SkillTargetSelector.GetTargets(_owner, meta);
        }

        /// <summary>
        /// 스킬을 실행한다.
        /// 호출 전에 반드시 CanUse()로 검사할 것.
        /// </summary>
        public void Use(int skillId)
        {
            var (reqs, effects, meta) = GetOrBuildCache(skillId);
            if (meta == null)
            {
                return;
            }

            // 1. 자원 소비
            foreach (ISkillRequirement req in reqs)
            {
                req.Consume(_owner);
            }

            // 2. 타겟 계산
            List<GameObject> targets = SkillTargetSelector.GetTargets(_owner, meta);

            // 3. 모든 효과 실행
            foreach (ISkillEffect effect in effects)
            {
                effect.Execute(_owner, targets);
            }

            // 4. 쿨타임 시작 (ID 대역이 40001+ 이므로 아이템 쿨타임과 자동 분리)
            _owner.CooldownTracker.StartCooldown(skillId, meta.CoolTime);
        }

        // ────────────────────────────────────────────────────────
        // 내부 헬퍼
        // ────────────────────────────────────────────────────────

        private (ISkillRequirement[] reqs, ISkillEffect[] effects, SkillMetaData meta) GetOrBuildCache(int skillId)
        {
            if (_cache.TryGetValue(skillId, out var cached))
            {
                return cached;
            }

            SkillMetaData meta = SpecDataManager.Instance.GetSkill(skillId);
            if (meta == null)
            {
                return (null, null, null);
            }

            ISkillRequirement[] reqs    = SkillFactory.BuildRequirements(meta);
            ISkillEffect[]      effects = SkillFactory.BuildEffects(meta);

            var entry = (reqs, effects, meta);
            _cache[skillId] = entry;
            return entry;
        }

        private static UseSkillResult RequirementToResult(ISkillRequirement req)
        {
            return req switch
            {
                LevelRequirement   => UseSkillResult.NotEnoughLevel,
                JobRequirement     => UseSkillResult.WrongJob,
                CostRequirement    => UseSkillResult.NotEnoughResource,
                _                  => UseSkillResult.WrongState,
            };
        }
    }
}

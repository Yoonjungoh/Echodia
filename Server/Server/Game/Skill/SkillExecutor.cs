using Google.Protobuf.Protocol;
using Server.Data;
using Server.Game.Skill.Effects;
using Server.Game.Skill.Requirements;
using Server.Game.Skill.Targeting;
using System.Collections.Generic;

namespace Server.Game.Skill
{
    public class SkillExecutor
    {
        private readonly Player _owner;

        private readonly Dictionary<int, (ISkillRequirement[] reqs, ISkillEffect[] effects, SkillMetaData meta)> _cache
            = new Dictionary<int, (ISkillRequirement[], ISkillEffect[], SkillMetaData)>();

        private readonly HashSet<int> _channelingSkills = new HashSet<int>();

        public SkillExecutor(Player owner)
        {
            _owner = owner;
        }

        // ────────────────────────────────────────────────────────
        // 공개 API
        // ────────────────────────────────────────────────────────

        public SkillState GetState(int skillId)
        {
            if (_channelingSkills.Contains(skillId))
                return SkillState.Active;

            if (_owner.CooldownTracker.IsOnCooldown(skillId))
                return SkillState.Cooldown;

            return SkillState.Ready;
        }

        public UseSkillResult CanUse(int skillId)
        {
            SkillState state = GetState(skillId);
            if (state == SkillState.Cooldown || state == SkillState.Active)
                return UseSkillResult.Cooldown;

            var (reqs, _, _) = GetOrBuildCache(skillId);
            if (reqs == null)
                return UseSkillResult.WrongState;

            foreach (ISkillRequirement req in reqs)
            {
                if (!req.Check(_owner))
                    return RequirementToResult(req);
            }

            return UseSkillResult.Success;
        }

        public List<GameObject> GetTargets(int skillId)
        {
            var (_, _, meta) = GetOrBuildCache(skillId);
            return meta == null ? new List<GameObject>() : SkillTargetSelector.GetTargets(_owner, meta);
        }

        /// <summary>호출 전에 반드시 CanUse()로 검사할 것.</summary>
        public void Use(int skillId)
        {
            var (reqs, effects, meta) = GetOrBuildCache(skillId);
            if (meta == null)
                return;

            foreach (ISkillRequirement req in reqs)
                req.Consume(_owner);

            if (meta.SkillActivationType == SkillActivationType.Channeling)
                StartChanneling(skillId, effects, meta);
            else
                ExecuteInstant(skillId, effects, meta);
        }

        /// <summary>채널링 중 외부에서 강제 중단 (피격, 이동 등).</summary>
        public void InterruptChannel(int skillId)
        {
            if (!_channelingSkills.Contains(skillId))
                return;

            EndChanneling(skillId, interrupted: true);
        }

        // ────────────────────────────────────────────────────────
        // Instant
        // ────────────────────────────────────────────────────────

        private void ExecuteInstant(int skillId, ISkillEffect[] effects, SkillMetaData meta)
        {
            List<GameObject> targets = SkillTargetSelector.GetTargets(_owner, meta);

            var allDamaged = new List<DamagedInfo>();
            foreach (ISkillEffect effect in effects)
                allDamaged.AddRange(effect.Execute(_owner, targets));

            _owner.CooldownTracker.StartCooldown(skillId, meta.CoolTime);

            S_UseSkill packet = new S_UseSkill
            {
                CasterId       = _owner.Id,
                SkillId        = skillId,
                Result         = UseSkillResult.Success,
                ActivationType = SkillActivationType.Instant,
            };
            packet.DamagedList.AddRange(allDamaged);
            _owner.GameRoom.Broadcast(_owner.CurrentPosition, packet);
        }

        // ────────────────────────────────────────────────────────
        // Channeling (차지 후 1회 공격)
        // ────────────────────────────────────────────────────────

        private void StartChanneling(int skillId, ISkillEffect[] effects, SkillMetaData meta)
        {
            _channelingSkills.Add(skillId);

            S_UseSkill startPacket = new S_UseSkill
            {
                CasterId       = _owner.Id,
                SkillId        = skillId,
                Result         = UseSkillResult.Success,
                ActivationType = SkillActivationType.Channeling,
                CastTimeMs     = (int)(meta.CastTime * 1000),
            };
            _owner.GameRoom.Broadcast(startPacket);

            int totalMs   = (int)(meta.CastTime * 1000);
            int tickMs    = meta.ChannelTickIntervalMs > 0 ? meta.ChannelTickIntervalMs : totalMs;
            int tickCount = tickMs > 0 ? totalMs / tickMs : 1;

            ScheduleChannelTick(skillId, effects, meta, tickCount, tickMs, completedTicks: 0);
        }

        private void ScheduleChannelTick(int skillId, ISkillEffect[] effects, SkillMetaData meta,
            int remainingTicks, int tickMs, int completedTicks)
        {
            _owner.GameRoom.ScheduleDelayedAction(tickMs, () =>
            {
                if (!_channelingSkills.Contains(skillId))
                    return;

                int newCompleted = completedTicks + 1;

                if (remainingTicks > 1)
                {
                    // 아직 차지 중 — 틱 카운트만 증가, 데미지 없음
                    ScheduleChannelTick(skillId, effects, meta, remainingTicks - 1, tickMs, newCompleted);
                }
                else
                {
                    // 마지막 틱 — 차지 완료, 1회 공격 후 종료
                    EndChanneling(skillId, interrupted: false, effects, meta, chargeMultiplier: newCompleted);
                }
            });
        }

        private void EndChanneling(int skillId, bool interrupted,
            ISkillEffect[] effects = null, SkillMetaData meta = null, float chargeMultiplier = 1.0f)
        {
            _channelingSkills.Remove(skillId);

            if (!interrupted && effects != null && meta != null)
            {
                List<GameObject> targets = SkillTargetSelector.GetTargets(_owner, meta);

                var allDamaged = new List<DamagedInfo>();
                foreach (ISkillEffect effect in effects)
                    allDamaged.AddRange(effect.Execute(_owner, targets, chargeMultiplier));

                // Melee는 여기서 데미지 결과 브로드캐스트
                // Ranged는 RangedAttackEffect가 SpawnProjectile → S_Spawn 경로로 처리
                if (allDamaged.Count > 0)
                {
                    S_UseSkill hitPacket = new S_UseSkill
                    {
                        CasterId       = _owner.Id,
                        SkillId        = skillId,
                        Result         = UseSkillResult.Success,
                        ActivationType = SkillActivationType.Channeling,
                    };
                    hitPacket.DamagedList.AddRange(allDamaged);
                    _owner.GameRoom.Broadcast(_owner.CurrentPosition, hitPacket);
                }
            }

            var (_, _, cachedMeta) = GetOrBuildCache(skillId);
            if (cachedMeta != null)
                _owner.CooldownTracker.StartCooldown(skillId, cachedMeta.CoolTime);

            S_ChannelEnd endPacket = new S_ChannelEnd
            {
                CasterId    = _owner.Id,
                SkillId     = skillId,
                Interrupted = interrupted,
            };
            _owner.GameRoom.Broadcast(endPacket);
        }

        // ────────────────────────────────────────────────────────
        // 내부 헬퍼
        // ────────────────────────────────────────────────────────

        private (ISkillRequirement[] reqs, ISkillEffect[] effects, SkillMetaData meta) GetOrBuildCache(int skillId)
        {
            if (_cache.TryGetValue(skillId, out var cached))
                return cached;

            SkillMetaData meta = SpecDataManager.Instance.GetSkill(skillId);
            if (meta == null)
                return (null, null, null);

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
                LevelRequirement => UseSkillResult.NotEnoughLevel,
                JobRequirement   => UseSkillResult.WrongJob,
                CostRequirement  => UseSkillResult.NotEnoughResource,
                _                => UseSkillResult.WrongState,
            };
        }
    }
}

using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Server.Game.Skill.Targeting
{
    /// <summary>
    /// CastDirections, CastRange, MaxTargets, TargetingType을 기반으로
    /// 스킬 대상을 선정한다. 거리순으로 정렬 후 최대 수 만큼 반환.
    /// </summary>
    public static class SkillTargetSelector
    {
        public static List<GameObject> GetTargets(Player caster, SkillMetaData skill)
        {
            Vector3 casterPos = caster.CurrentPosition;
            Vector3 forward = MovementHelper.ForwardFrom(caster.ObjectState.Rotation);

            // XZ 평면에서의 방향 벡터
            Vector3 forwardXZ = NormalizeXZ(forward);
            // right = forward 방향에서 시계방향 90° 회전 (XZ 평면)
            Vector3 rightXZ = new Vector3(forwardXZ.Z, 0f, -forwardXZ.X);

            float range = skill.CastRange;
            bool isSphere = skill.CastDirections.Contains(CastDirectionType.Sphere);

            var candidates = new List<(GameObject obj, float distSq)>();

            IEnumerable<GameObject> pool = range > 0f
                ? caster.GameRoom.GetObjectsInRange(casterPos, range)
                : caster.GameRoom.GetAllObjects();

            foreach (GameObject obj in pool)
            {
                if (obj == null || obj.Id == caster.Id)
                {
                    continue;
                }

                // TargetingType에 따라 필터링
                if (!IsValidTarget(caster, obj, skill.TargetingType))
                {
                    continue;
                }

                Vector3 toTarget = obj.CurrentPosition - casterPos;
                float distSq = toTarget.LengthSquared();

                // 범위 검사 (Sphere == 0이면 범위 제한 없음)
                if (range > 0f && distSq > range * range)
                {
                    continue;
                }

                // 방향 검사 (Sphere 포함 시 무조건 통과)
                if (!isSphere && !IsInDirections(toTarget, forwardXZ, rightXZ, skill.CastDirections, skill.CastHalfAngles))
                {
                    continue;
                }

                candidates.Add((obj, distSq));
            }

            // 거리 가까운 순 정렬
            candidates.Sort((a, b) => a.distSq.CompareTo(b.distSq));

            var result = new List<GameObject>();
            int limit = skill.MaxTargets > 0 ? skill.MaxTargets : candidates.Count;
            for (int i = 0; i < Math.Min(limit, candidates.Count); i++)
            {
                result.Add(candidates[i].obj);
            }

            return result;
        }

        private static bool IsValidTarget(Player caster, GameObject obj, SkillTargetingType targetingType)
        {
            switch (targetingType)
            {
                case SkillTargetingType.Enemy:
                    // 플레이어→몬스터 (또는 몬스터→플레이어) 공격 대상
                    return obj is Monster;
                case SkillTargetingType.Ally:
                    return obj is Player;
                case SkillTargetingType.Self:
                    return obj.Id == caster.Id;
                default:
                    return false;
            }
        }

        // halfAngles[i]는 directions[i]에 대응하는 반각(degree). null이거나 인덱스 초과 시 45도 기본값 사용.
        private static bool IsInDirections(
            Vector3 toTarget,
            Vector3 forwardXZ,
            Vector3 rightXZ,
            List<CastDirectionType> directions,
            List<float> halfAngles)
        {
            if (toTarget == Vector3.Zero)
            {
                return true;
            }

            Vector3 dirXZ = NormalizeXZ(toTarget);

            for (int i = 0; i < directions.Count; i++)
            {
                CastDirectionType dir = directions[i];
                float halfDeg = (halfAngles != null && i < halfAngles.Count) ? halfAngles[i] : 45f;
                float threshold = (float)Math.Cos(halfDeg * Math.PI / 180.0);

                switch (dir)
                {
                    case CastDirectionType.Front:
                        if (Vector3.Dot(dirXZ, forwardXZ) >= threshold)
                            return true;
                        break;
                    case CastDirectionType.Back:
                        if (Vector3.Dot(dirXZ, -forwardXZ) >= threshold)
                            return true;
                        break;
                    case CastDirectionType.Right:
                        if (Vector3.Dot(dirXZ, rightXZ) >= threshold)
                            return true;
                        break;
                    case CastDirectionType.Left:
                        if (Vector3.Dot(dirXZ, -rightXZ) >= threshold)
                            return true;
                        break;
                    case CastDirectionType.Sphere:
                        return true;
                }
            }
            return false;
        }

        private static Vector3 NormalizeXZ(Vector3 v)
        {
            var xz = new Vector3(v.X, 0f, v.Z);
            float len = xz.Length();
            return len > 0.0001f ? xz / len : Vector3.UnitZ;
        }
    }
}

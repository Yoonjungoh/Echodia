using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Server
{
    public static class CollisionHelper
    {
        // 수평 성분(유니티 기준 XZ)만으로 콜리젼 판단
        public static bool IsCollision(Vector3 origin, Vector3 forward, Vector3 target,
                                         float radius, float cosLimit, float height)
        {
            Vector3 vec = target - origin;

            // 높이 체크 (y축)
            if (MathF.Abs(vec.Y) > height * 0.5f) 
            {
                return false; 
            }

            // 수평 성분만 비교 (해당 부분을 0으로 안 해주면 원뿔 모양으로 콜리젼 체크하게 됨)
            vec.Y = 0;
            forward.Y = 0;
            float sqrDist = vec.LengthSquared();

            // 거리 체크 (반지름 넘나 확인)
            if (sqrDist > radius * radius) 
                return false;

            // 거의같은 위치면 포함
            if (sqrDist < 1e-6f) 
                return true; 

            // 각도 체크
            Vector3 vecNormal = Vector3.Normalize(vec);
            Vector3 forwardNormal = Vector3.Normalize(forward);
            float cosTheta = Vector3.Dot(vecNormal, forwardNormal);
            return cosTheta >= cosLimit;
        }
    }
}

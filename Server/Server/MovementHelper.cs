using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Server
{
    public static class MovementHelper
    {
        public static Vector3 ProtoVec3ToVec3(ProtoVector3 v) { return new Vector3(v.X, v.Y, v.Z); }
        public static ProtoVector3 Vec3ToProtoVec3(Vector3 v) { return new ProtoVector3 { X = v.X, Y = v.Y, Z = v.Z }; }

        public static Vector3 ForwardFrom(ProtoQuaternion q)
        {
            // 행렬 곱셈
            Quaternion quat = new Quaternion(q.X, q.Y, q.Z, q.W);
            return Vector3.Transform(Vector3.UnitZ, quat); // (0,0,1) 회전 적용
        }

        public static Vector3 PredictPosition(Vector3 pos, Vector3 vel, long lastServerTime, long now)
        {
            float deltaSec = MathF.Max(0f, (now - lastServerTime) / 1000f);
            return pos + vel * deltaSec;
        }
        
        // Y축 기준으로 바라보는 회전 쿼터니언 생성
        public static ProtoQuaternion LookAt(Vector3 lookDir)
        {
            // 방향이 거의 0이면 회전 안 한 상태로
            if (lookDir.LengthSquared() < 1e-6f)
            {
                return new ProtoQuaternion
                {
                    X = 0,
                    Y = 0,
                    Z = 0,
                    W = 1
                };
            }
            lookDir = Vector3.Normalize(lookDir);

            float yaw = MathF.Atan2(lookDir.X, lookDir.Z);
            Quaternion q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
            return new ProtoQuaternion
            {
                X = q.X,
                Y = q.Y,
                Z = q.Z,
                W = q.W
            };
        }
    }
}

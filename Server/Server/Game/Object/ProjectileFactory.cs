using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game.Object
{
    internal class ProjectileFactory
    {
        private static readonly Dictionary<ProjectileType, Func<Projectile>> _factory
        = new Dictionary<ProjectileType, Func<Projectile>>
        {
            { ProjectileType.MagicMissile, () => ObjectManager.Instance.Add<MagicMissile>() },
        };

        public static Projectile Create(ProjectileType type)
        {
            if (_factory.TryGetValue(type, out var creator))
                return creator();

            // 예외 처리
            throw new Exception($"Unknown ProjectileType {type}");
        }
    }
}

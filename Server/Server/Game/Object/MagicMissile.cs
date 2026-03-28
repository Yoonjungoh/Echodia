using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf.Protocol;
using Server.Data;

namespace Server.Game.Object
{
    public class MagicMissile : Projectile
    {
        public MagicMissile()
        {
            ProjectileType = ProjectileType.MagicMissile;

            ProjectileMetaData spec = SpecDataManager.Instance.GetProjectile(ProjectileType.MagicMissile);
            if (spec != null)
            {
                Stat.MaxHp = spec.MaxHp;
                Stat.Hp = spec.MaxHp;
                Stat.MagicMissileAttakDamage = spec.CommonAttackDamage;
                Stat.MagicMissileAttackCoolTime = spec.CommonAttackCoolTime;
                Stat.MoveSpeed = spec.MoveSpeed;
                LifeTime = spec.LifeTime;
                MaxHitCount = spec.MaxHitCount;
            }
            else
            {
                Console.WriteLine("[MagicMissile] ProjectileMetaData not found");
            }
        }

        public override void Reset()
        {
            base.Reset();

            ProjectileMetaData spec = SpecDataManager.Instance.GetProjectile(ProjectileType.MagicMissile);
            if (spec != null)
                LifeTime = spec.LifeTime;
        }
    }
}

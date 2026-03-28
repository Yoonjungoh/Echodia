using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf.Protocol;

namespace Server.Game.Object
{
    public class MagicMissile : Projectile
    {
        public MagicMissile()
        {
            ProjectileType = ProjectileType.MagicMissile;

            Stat.MaxHp = 1;
            Stat.Hp = Stat.MaxHp;
            Stat.MagicMissileAttakDamage = 80;
            Stat.MagicMissileAttackCoolTime = 3;
            Stat.MoveSpeed = 5;
            LifeTime = 3000;
        }

        public override void Reset()
        {
            base.Reset();

            LifeTime = 3000;
        }
    }
}

using Google.Protobuf.Protocol;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Server.Game
{
    public class Projectile : GameObject, IPoolable
    {
        public int HitCount { get; set; } = 0;
        public int MaxHitCount { get; set; } = 0; // SpecData(ProjectileMetaData)에서 ProjectileType별로 세팅
        public long SpawnTime { get; set; }
        public int LifeTime { get; set; } // Ms
        public Vector3 PreviousPosition { get; set; }

        public Projectile()
        {
            ObjectType = GameObjectType.Projectile;

            CreatureState = CreatureState.Move;
        }

        public virtual void Reset()
        {
            HitCount = 0;
            SpawnTime = 0;
            PreviousPosition = Vector3.Zero;
            GameRoom = null;
        }

        public override void Update()
        {
            base.Update();
            
            // Zone에 적용
            Zone previousZone = GameRoom.GetZone(PreviousPosition);
            Zone currentZone = GameRoom.GetZone(CurrentPosition);

            if (previousZone != currentZone)
            {
                if (previousZone != null)
                {
                    previousZone.Remove(this);
                }
                if (currentZone != null)
                {
                    currentZone.Add(this);
                }
            }
            
            PreviousPosition = CurrentPosition;
        }
    }
}

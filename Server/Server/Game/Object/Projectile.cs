using Google.Protobuf.Protocol;
using Server.DB;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Server.Game
{
    public class Projectile : GameObject
    {
        public bool hasDealtDamage { get; set; } = false;
        public long SpawnTime { get; set; }
        public float LifeTime { get; set; } = 3000.0f; // Ms
        public Vector3 PreviousPosition { get; set; }

        public Projectile()
        {
            ObjectType = GameObjectType.Projectile;

            CreatureState = CreatureState.Move;
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

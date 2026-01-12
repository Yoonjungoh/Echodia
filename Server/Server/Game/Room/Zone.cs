using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Game
{
    public class Zone
    {
        public int IndexX { get; private set; }
        
        public int IndexZ { get; private set; }

        public HashSet<Player> Players { get; private set; } = new HashSet<Player>();
        public HashSet<Monster> Monsters { get; private set; } = new HashSet<Monster>();
        public HashSet<Projectile> Projectiles { get; private set; } = new HashSet<Projectile>();

        public Zone(int x, int z) 
        {
            IndexX = x;
            IndexZ = z;
        }

        public void Add(GameObject gameObject)
        {
            if (gameObject.ObjectType == GameObjectType.Player)
            {
                Players.Add(gameObject as Player);
            }
            else if (gameObject.ObjectType == GameObjectType.Monster)
            {
                Monsters.Add(gameObject as Monster);
            }
            else if (gameObject.ObjectType == GameObjectType.Projectile)
            {
                Projectiles.Add(gameObject as Projectile);
            }
        }

        public bool Remove(GameObject gameObject)
        {
            if (gameObject.ObjectType == GameObjectType.Player)
            {
                return Players.Remove(gameObject as Player);
            }
            else if (gameObject.ObjectType == GameObjectType.Monster)
            {
                return Monsters.Remove(gameObject as Monster);
            }
            else if (gameObject.ObjectType == GameObjectType.Projectile)
            {
                return Projectiles.Remove(gameObject as Projectile);
            }

            return false;
        }
    }
}

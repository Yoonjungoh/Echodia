using Google.Protobuf.Protocol;
using Server.DB;
using Server.Game.Object;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Server.Game
{
	public class ObjectManager
	{
		public static ObjectManager Instance { get; } = new ObjectManager();

		private object _lock = new object();
        private Dictionary<int, Player> _players = new Dictionary<int, Player>();
        private Dictionary<int, Monster> _monsters = new Dictionary<int, Monster>();
        private Dictionary<int, Projectile> _projectiles = new Dictionary<int, Projectile>();

        private readonly GameObjectPool<MagicMissile> _magicMissilePool = new(100);
        private readonly GameObjectPool<DropItem> _dropItemPool = new(100);

        // [UNUSED(1)][TYPE(7)][ID(24)]
        private int _counter = 0;
		public T Add<T>() where T : GameObject, new()
		{
			T gameObject = new T();

			lock (_lock)
			{
                try
                {
                    gameObject.Id = GenerateId(gameObject.ObjectType);

                    if (gameObject.ObjectType == GameObjectType.Player)
                    {
                        _players.Add(gameObject.Id, gameObject as Player);
                    }
                    else if (gameObject.ObjectType == GameObjectType.Monster)
                    {
                        _monsters.Add(gameObject.Id, gameObject as Monster);
                    }
                    else if (gameObject.ObjectType == GameObjectType.Projectile)
                    {
                        _projectiles.Add(gameObject.Id, gameObject as Projectile);
                    }

                }
				catch (Exception e)
                {
                    ConsoleLogManager.Instance.Log(e);
                    ConsoleLogManager.Instance.Log("Dictionary Key-Value Problem");	
                }
			}

			return gameObject;
		}

        public MagicMissile RentMagicMissile()
        {
            MagicMissile obj;
            lock (_lock)
            {
                obj = _magicMissilePool.Rent();
                obj.Id = GenerateId(obj.ObjectType);
                _projectiles[obj.Id] = obj;
            }
            return obj;
        }

        public DropItem RentDropItem()
        {
            DropItem obj;
            lock (_lock)
            {
                obj = _dropItemPool.Rent();
                obj.Id = GenerateId(obj.ObjectType);
            }
            return obj;
        }

        public void Return(Projectile projectile)
        {
            // Reset을 먼저: Push 이전에 완료해야 Rent한 스레드가 오염된 상태를 보지 않음
            projectile.Reset();
            lock (_lock)
            {
                _projectiles.Remove(projectile.Id);
                if (projectile is MagicMissile mm)
                    _magicMissilePool.TryReturn(mm);
            }
        }

        public void Return(DropItem dropItem)
        {
            dropItem.Reset();
            lock (_lock)
            {
                _dropItemPool.TryReturn(dropItem);
            }
        }

		public int GenerateId(GameObjectType type)
		{
			lock (_lock)
			{
				return ((int)type << 24) | (_counter++);
			}
		}

		public GameObjectType GetObjectTypeById(int id)
		{
			int type = (id >> 24) & 0x7F;
			return (GameObjectType)type;
		}

		public bool Remove(int objectId)
		{
			GameObjectType objectType = GetObjectTypeById(objectId);

			lock (_lock)
			{
				if (objectType == GameObjectType.Player)
					return _players.Remove(objectId);
                else if (objectType == GameObjectType.Monster)
                    return _monsters.Remove(objectId);
            }

			return false;
		}

		public T Find<T>(int objectId) where T : GameObject
		{
			GameObjectType objectType = GetObjectTypeById(objectId);

			lock (_lock)
			{
				if (objectType == GameObjectType.Player)
				{
					Player player = null;
					if (_players.TryGetValue(objectId, out player))
						return player as T;
                }
                else if (objectType == GameObjectType.Monster)
                {
                    Monster monster = null;
                    if (_monsters.TryGetValue(objectId, out monster))
                        return monster as T;
                }
            }

			return null;
		}

		public Vector3 GetPlayerLastLogoutPos(int playerId)
        {
			using (GameDbContext db = new GameDbContext())
			{
				PlayerDb playerDb = db.Players.Find(playerId);
                if (playerDb != null)
                {
					return playerDb.LastLogoutPosition;
                }
                else
                {
                    ConsoleLogManager.Instance.Log($"Player with ID {playerId} not found in database.");
                    return Vector3.Zero;
                }
            }
        }

        // 마지막으로 접속했던 맵 Id 반환 (DB 없으면 DefaultStartMapId)
        public int GetPlayerLastLogoutMapId(int playerId)
        {
            using (GameDbContext db = new GameDbContext())
            {
                PlayerDb playerDb = db.Players.Find(playerId);
                if (playerDb != null)
                    return playerDb.LastMapId > 0 ? playerDb.LastMapId : DataManager.Instance.DefaultStartMapId;

                return DataManager.Instance.DefaultStartMapId;
            }
        }
	}
}

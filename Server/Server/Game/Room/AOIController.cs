using Google.Protobuf.Protocol;
using Server.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Server.Game.Room
{
    // 플레이어마다 하나씩 들고 있을 예정
    public class AOIController
    {
        public Player Owner { get; private set; }
        public HashSet<GameObject> PreviousGameObjects { get; private set; } = new HashSet<GameObject>();
        
        public AOIController(Player owner)
        {
            Owner = owner;
        }

        // 주변의 오브젝트 가져오기
        public HashSet<GameObject> GatherGameObjects()
        {
            if (Owner == null || Owner.GameRoom == null)
                return null;

            HashSet<GameObject> gameObjects = new HashSet<GameObject>();

            // 1. 플레이어의 현재 위치 기준으로 인접한 존들 가져오기
            Vector3 ownerPos = Owner.CurrentPosition;
            List<Zone> zones = Owner.GameRoom.GetAdjacentZones(ownerPos);

            // 2. 각 존들에 있는 오브젝트들 가져오기
            // 2-1. 존에 있지만 시야각 안에도 있는지 확인
            foreach (Zone zone in zones)
            {
                foreach (Player player in zone.Players)
                {
                    // 2-2. diff 떠서 확인 하기
                    float dx = player.CurrentPosition.X - ownerPos.X;
                    float dz = player.CurrentPosition.Z - ownerPos.Z;

                    if (MathF.Abs(dx) > DataManager.Instance.AOICells)
                        continue;

                    if (MathF.Abs(dz) > DataManager.Instance.AOICells)
                        continue;

                    // 2-3. 시야각 안에 있다는 의미니 추가
                    gameObjects.Add(player);
                }

                foreach (Monster monster in zone.Monsters)
                {
                    // 2-2. diff 떠서 확인 하기
                    float dx = monster.CurrentPosition.X - ownerPos.X;
                    float dz = monster.CurrentPosition.Z - ownerPos.Z;

                    if (MathF.Abs(dx) > DataManager.Instance.AOICells)
                        continue;

                    if (MathF.Abs(dz) > DataManager.Instance.AOICells)
                        continue;

                    // 2-3. 시야각 안에 있다는 의미니 추가
                    gameObjects.Add(monster);
                }

                foreach (Projectile projectile in zone.Projectiles)
                {
                    // 2-2. diff 떠서 확인 하기
                    float dx = projectile.CurrentPosition.X - ownerPos.X;
                    float dz = projectile.CurrentPosition.Z - ownerPos.Z;

                    if (MathF.Abs(dx) > DataManager.Instance.AOICells)
                        continue;

                    if (MathF.Abs(dz) > DataManager.Instance.AOICells)
                        continue;
                    
                    // 2-3. 시야각 안에 있다는 의미니 추가
                    gameObjects.Add(projectile);
                }
            }

            return gameObjects;
        }

        public void Update()
        {
            if (Owner == null || Owner.GameRoom == null)
                return;
            
            HashSet<GameObject> currentGameObjects = GatherGameObjects();
            if (currentGameObjects == null)
                return;

            // 기존엔 없었는데 생기면 Spawn 처리
            List<GameObject> addedList = currentGameObjects.Except(PreviousGameObjects).ToList();
            if (addedList.Count > 0)
            {
                S_Spawn spawnPacket = new S_Spawn();
                long serverReceivedTime = Util.GetTimestampMs();

                foreach (GameObject go in addedList)
                {
                    if (go == null)
                        continue;

                    go.ObjectState.ServerReceivedTime = serverReceivedTime;
                    // 참조로 넣기 불안하니까 복사 한 번 하기
                    ObjectState objectState = new ObjectState();
                    objectState.MergeFrom(go.ObjectState);
                    spawnPacket.ObjectStateList.Add(objectState);

                }
                Owner.Session.Send(spawnPacket);
            }

            // 기존엔 있었는데 지금은 없으면 Despawn 처리
            List<GameObject> removedList = PreviousGameObjects.Except(currentGameObjects).ToList();
            if (removedList.Count > 0)
            {
                S_Despawn despawnPacket = new S_Despawn();
                long serverReceivedTime = Util.GetTimestampMs();

                foreach (GameObject go in removedList)
                {
                    if (go == null)
                        continue;

                    despawnPacket.ObjectIdList.Add(go.Id);
                }
                Owner.Session.Send(despawnPacket);
            }

            PreviousGameObjects = currentGameObjects;

            Owner.GameRoom.PushAfter(Update, 500);
        }
    }
}

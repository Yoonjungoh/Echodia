using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server.Currency;
using Server.DB;
using Server.Game.Object;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using static Server.Define;

namespace Server.Game
{
    public class GameRoom : JobSerializer
    {
        public int ServerId { get; set; }
        public int ChannelId { get; set; }
        public int MapId { get; set; }
        public string MapName { get; set; }
        public Map Map { get; set; } = new Map();
        public Zone[,] Zones { get; private set; }  // x, z
        public int ZoneCells { get; private set; }

        private Dictionary<int, GameObject> _gameObjects = new Dictionary<int, GameObject>();
        private Dictionary<int, Player> _players = new Dictionary<int, Player>();
        private Dictionary<int, Monster> _monsters = new Dictionary<int, Monster>();
        private Dictionary<int, Projectile> _projectiles = new Dictionary<int, Projectile>();

        public bool IsRoomFull { get { return _players.Count == DataManager.Instance.MaxRoomPlayerCount; } }

        public event Action OnPlayerInfoChanged;  // 방 정보 바뀌었을 때 알림 (roomId)

        public GameRoom(int serverId, int channelId, int mapId)
        {
            ServerId = serverId;
            ChannelId = channelId;
            MapId = mapId;
            MapName = DataManager.Instance.GetMapName(mapId);
        }

        public void Init(int zoneCells)
        {
            //TestTimer();
            Map.MapData = MapManager.Instance.CreateCopy(MapId);
            OnPlayerInfoChanged -= PlayerInfoChanged;
            OnPlayerInfoChanged += PlayerInfoChanged;

            // Zone 초기화
            ZoneCells = zoneCells;
            int countX = (Map.MapData.SizeX / zoneCells) + 1;
            int countZ = (Map.MapData.SizeZ / zoneCells) + 1;
            Zones = new Zone[countX, countZ];
            for (int x = 0; x < countX; x++)
            {
                for (int z = 0; z < countZ; z++)
                {
                    Zones[x, z] = new Zone(x, z);
                }
            }

            // Monster 초반 Spawn
            InitMonsters();
        }

        public Zone GetZone(Vector3 pos)
        {
            // 1. 월드 좌표를 0 기반 좌표로 변경
            int worldX = (int)(pos.X - Map.MapData.MinX);
            int worldZ = (int)(pos.Z - Map.MapData.MinZ);

            // 2. Zone 인덱스 계산
            int x = worldX / ZoneCells;
            int z = worldZ / ZoneCells;

            // 3. 범위 체크
            if (x < 0 || x >= Zones.GetLength(0))
                return null;

            if (z < 0 || z >= Zones.GetLength(1))
                return null;

            return Zones[x, z];
        }

        // 어디선가 주기적으로 호출해줘야 함
        public void Update()
        {
            Flush();
            UpdateMonsters();
            UpdateProjectiles();
        }

        private void UpdateMonsters()
        {
            if (_monsters == null || _monsters.Count == 0)
                return;

            Monster[] monsters = _monsters.Values.ToArray();
            foreach (Monster monster in monsters)
            {
                if (monster == null)
                    continue;

                monster.Update();
            }
        }

        private void UpdateProjectiles()
        {
            if (_projectiles == null || _projectiles.Count == 0)
                return;

            long now = Util.GetTimestampMs();

            List<int> removeList = new List<int>();

            Projectile[] projectiles = _projectiles.Values.ToArray();
            foreach (Projectile projectile in projectiles)
            {
                if (now - projectile.SpawnTime >= projectile.LifeTime)
                {
                    removeList.Add(projectile.Id);
                }
            }

            foreach (int id in removeList)
            {
                LeaveGame(id);
            }
        }
        
        public void SpawnMonster(MonsterType monsterType, Vector3 spawnPos)
        {
            Monster monster = MonsterFactory.Create(monsterType);

            monster.MonsterType = monsterType;
            monster.Name = $"{monsterType}_{monster.ObjectState.ObjectId}";
            monster.Position = MovementHelper.Vec3ToProtoVec3(spawnPos);

            Push(EnterGame, monster);
        }

        public void SpawnProjectile(int ownerId, ProjectileType projectileType)
        {
            Projectile projectile = ProjectileFactory.Create(projectileType);
            // 주인이 존재하지 않는 오브젝트거나 똑같은 투사체 존재하면 스폰 안 함  
            if (_gameObjects.ContainsKey(ownerId) == false || _projectiles.ContainsKey(projectile.Id))
            {
                ConsoleLogManager.Instance.Log($"[Warning] Cannot spawn projectile. OwnerId: {ownerId}, ProjectileId: {projectile.Id}");
                return;
            }

            // 주인 추가해주기
            projectile.OwnerId = ownerId;
            var owner = _gameObjects[ownerId];

            // 먼저 회전부터 세팅
            projectile.Rotation = owner.Rotation;

            // 회전에서 forward 뽑기
            Vector3 forward = MovementHelper.ForwardFrom(projectile.Rotation);

            // 정규화
            if (forward.LengthSquared() > 1e-6f)
            {
                forward = Vector3.Normalize(forward);
            }

            // 스폰 위치 = 플레이어 위치 + forward * 오프셋
            Vector3 ownerPos = MovementHelper.ProtoVec3ToVec3(owner.Position);
            Vector3 spawnPos = ownerPos + (forward * owner.ProjectileSpawnOffset) + Vector3.UnitY;   // 살짝 위에

            // 세팅
            projectile.Position = MovementHelper.Vec3ToProtoVec3(spawnPos);
            projectile.Velocity = MovementHelper.Vec3ToProtoVec3(forward * projectile.Stat.MoveSpeed);
            projectile.SpawnTime = Util.GetTimestampMs();

            Push(EnterGame, projectile);
        }

        public void HandleAttack(int InstigatorId, int damagedObjectId, AttackType attackType)
        {
            switch (attackType)
            {
                case AttackType.CommonAttack:
                    HandleCommonAttack(InstigatorId);
                    break;
                case AttackType.RangedAttack:
                    HandleProjectileAttack(InstigatorId, damagedObjectId);
                    break;
                default:
                    ConsoleLogManager.Instance.Log($"Unknown AttackType: {attackType}");
                    break;
            }
        }

        private void HandleProjectileAttack(int instigatorId, int damagedObjectId)
        {
            _gameObjects.TryGetValue(instigatorId, out GameObject instigator);
            if (instigator == null)
                return;

            // 이미 데미지 입힌 투사체면 return
            Projectile projectile = instigator as Projectile;
            if (projectile == null || projectile.hasDealtDamage == true)
                return;

            _gameObjects.TryGetValue(damagedObjectId, out GameObject damagedObject);
            if (damagedObject == null)
                return;

            // 서버에서 예측한 투사체 위치랑 적 위치 비교해서 오차 심하지 않으면 데미지 허용
            Vector3 projectilePos = projectile.CurrentPosition;
            Vector3 damagedObjectPos = damagedObject.CurrentPosition;
            //float dist = Vector3.Distance(projectilePos, damagedObjectPos);
            //if (dist > DataManager.Instance.ProjectileDistanceErrorThreshold)
            //{
            //    // 너무 멀리 떨어져 있음
            //    ConsoleLogManager.Instance.Log($"[Warning] Projectile attack distance too far: {dist}");
            //    return;
            //}

            // 데미지 처리
            S_Attack attackPacket = new S_Attack();
            damagedObject.OnDamaged(projectile, projectile.ObjectState.Stat.MagicMissileAttakDamage);
            projectile.hasDealtDamage = true;   // 데미지 한 번 입혔으니 다시 요청들어 오면 거부

            DamagedInfo damagedInfo = new DamagedInfo();
            damagedInfo.ObjectId = damagedObjectId;
            damagedInfo.RemainHp = damagedObject.ObjectState.Stat.Hp;
            attackPacket.DamagedObjectList.Add(damagedInfo);

            // 디스폰도 같이 처리해줘야 함
            LeaveGame(projectile.Id);

            Broadcast(projectilePos, attackPacket);
        }

        private void HandleCommonAttack(int instigatorId)
        {
            _gameObjects.TryGetValue(instigatorId, out GameObject instigator);
            if (instigator == null)
                return;

            // 서버 기준 공격 시간 (플레이어 위치, 방향 예상하기 위함)
            long attackTimeMs = Util.GetTimestampMs();

            // 1. 공격자 위치 구하기
            // instigatorId.ObjectState.ServerReceivedTime 자주 갱신하면 더 정확해지더라 (당연한 말 -> HandleMove에서 업뎃 중임)
            Vector3 attackPos = instigator.CurrentPosition;

            // 2. 공격자 방향 구하기
            Vector3 attackForward = MovementHelper.ForwardFrom(instigator.ObjectState.Rotation);
            attackForward = Vector3.Normalize(attackForward);

            // 3. 공격 범위 알아내기
            float radius = instigator.ObjectState.Stat.AttackRange;
            float halfDeg = instigator.ObjectState.Stat.AttackHalfAngleDeg;
            float height = instigator.ObjectState.Stat.AttackHeight;

            // 3-1. 각도 안에 있는지 확인할 cos 구하기
            float cosLimit = (float)MathF.Cos(halfDeg * (MathF.PI / 180f));

            // 4. 후보 전부 검사하기
            List<int> damagedObjectList = new List<int>();

            foreach (GameObject target in _gameObjects.Values)
            {
                if (target == null) continue;
                if (target.Id == instigator.Id) continue;

                // 4-1. 대상 위치 예측하기
                Vector3 targetPos = target.CurrentPosition;

                // 4-2. 충돌 판정
                if (CollisionHelper.IsCollision(attackPos, attackForward, targetPos, radius, cosLimit, height))
                {
                    damagedObjectList.Add(target.Id);
                }
            }

            // 5. 데미지 처리
            S_Attack attackPacket = new S_Attack();
            foreach (int objectId in damagedObjectList)
            {
                _gameObjects.TryGetValue(objectId, out GameObject damagedObject);
                if (damagedObject == null)
                    continue;

                damagedObject.OnDamaged(instigator, instigator.ObjectState.Stat.CommonAttackDamage);

                DamagedInfo damagedInfo = new DamagedInfo();
                damagedInfo.ObjectId = objectId;
                damagedInfo.RemainHp = damagedObject.ObjectState.Stat.Hp;
                attackPacket.DamagedObjectList.Add(damagedInfo);
            }

            // 6. 브로드캐스트
            Broadcast(instigator.CurrentPosition, attackPacket);
        }

        public void EnterGame(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            GameObjectType objectType = gameObject.ObjectType;

            gameObject.GameRoom = this;

            S_EnterGame enteGamePacket = new S_EnterGame();
            enteGamePacket.ObjectState = new ObjectState();
            enteGamePacket.ObjectState.Position = new ProtoVector3();
            enteGamePacket.ObjectState.Velocity = new ProtoVector3();
            enteGamePacket.ObjectState.Rotation = new ProtoQuaternion();
            enteGamePacket.ObjectState.Stat = new Stat();

            // objectId 초기화
            enteGamePacket.ObjectState.ObjectId = gameObject.Id;

            // objectType 초기화
            enteGamePacket.ObjectState.ObjectType = objectType;

            // creatureState 초기화
            gameObject.CreatureState = CreatureState.Idle;
            enteGamePacket.ObjectState.CreatureState = CreatureState.Idle;

            // Type 관련 분기 초기화
            Zone zone = GetZone(gameObject.CurrentPosition);
            if (zone != null)
            {
                zone.Add(gameObject);
            }

            if (zone != null)
            {
                // TODO
            }
            if (objectType == GameObjectType.Player)
            {
            }
            else if (objectType == GameObjectType.Monster)
            {
                enteGamePacket.ObjectState.MonsterType = gameObject.MonsterType;
            }
            else if (objectType == GameObjectType.Projectile)
            {
                enteGamePacket.ObjectState.ProjectileType = gameObject.ProjectileType;
                enteGamePacket.ObjectState.OwnerId = gameObject.OwnerId;
                // 투사체는 Move로 변경해주기
                gameObject.CreatureState = CreatureState.Move;
                enteGamePacket.ObjectState.CreatureState = CreatureState.Move;
            }

            // name 초기화
            enteGamePacket.ObjectState.Name = gameObject.Name;

            // position 초기화
            Vector3 startPos = Vector3.Zero;

            // 플레이어 이외는 다른 곳에서 위치 미리 받고 옴
            if (objectType == GameObjectType.Player)
            {
                int spawnIndex = _players.Count % DataManager.Instance.MaxRoomPlayerCount;
                startPos = DataManager.Instance.GetStartPosition(RoomType.GameRoom, spawnIndex);
            }
            else
            {
                startPos = MovementHelper.ProtoVec3ToVec3(gameObject.Position);
            }
            gameObject.Position.X = startPos.X;
            gameObject.Position.Y = startPos.Y;
            gameObject.Position.Z = startPos.Z;

            enteGamePacket.ObjectState.Position.X = gameObject.ObjectState.Position.X;
            enteGamePacket.ObjectState.Position.Y = gameObject.ObjectState.Position.Y;
            enteGamePacket.ObjectState.Position.Z = gameObject.ObjectState.Position.Z;

            // stat 초기화
            enteGamePacket.ObjectState.Stat = gameObject.Stat;

            // 플레이어면 본인 입장 패킷 전송
            if (objectType == GameObjectType.Player)
            {
                Player player = gameObject as Player;
                if (player.Session != null)
                {
                    player.Session.Send(enteGamePacket);
                }
            }

            AddObject(gameObject);

            long serverReceivedTime = Util.GetTimestampMs();
            if (objectType == GameObjectType.Player)
            {
                Player player = gameObject as Player;
                // 본인한테 맵안의 플레이어 정보 전송
                player.AOI.Update();
            }

            // 다른 플레이어에게 게임 오브젝트가 접속한 걸 알려주기
            foreach (Player p in _players.Values)
            {
                if (p == null || p.Session == null || gameObject.Id == p.Id)
                    continue;

                p.ObjectState.ServerReceivedTime = serverReceivedTime;
                ConsoleLogManager.Instance.Log($"[GameRoom Update] Player {p.Id} Pos({p.Position.X}, {p.Position.Y}, {p.Position.Z})");
            }
            
            S_Spawn spawnToOthersPacket = new S_Spawn();
            spawnToOthersPacket.ObjectStateList.Add(gameObject.ObjectState);
            Broadcast(gameObject.CurrentPosition, spawnToOthersPacket);
        }

        public void LeaveGame(int objectId)
        {
            GameObjectType type = ObjectManager.Instance.GetObjectTypeById(objectId);

            Vector3 pos = Vector3.Zero;

            if (type == GameObjectType.Player)
            {
                Player player = null;
                if (_players.TryGetValue(objectId, out player) == false)
                    return;

                pos = player.CurrentPosition;

                Zone zone = GetZone(player.CurrentPosition);
                if (zone != null)
                {
                    zone.Remove(player);
                }

                player.OnLeaveGame();
                player.GameRoom = null;

                // 본인한테 정보 전송
                {
                    S_LeaveGame leavePacket = new S_LeaveGame();
                    leavePacket.RoomExitReason = RoomExitReason.GameLose;
                    player.Session.Send(leavePacket);
                }
            }
            else if (type == GameObjectType.Monster)
            {
                Monster monster = null;
                if (_monsters.TryGetValue(objectId, out monster) == false)
                    return;

                pos = monster.CurrentPosition;

                Zone zone = GetZone(monster.CurrentPosition);
                if (zone != null)
                {
                    zone.Remove(monster);
                }
            }
            else if (type == GameObjectType.Projectile)
            {
                Projectile projectile = null;
                if (_projectiles.TryGetValue(objectId, out projectile) == false)
                    return;

                pos = projectile.CurrentPosition;

                Zone zone = GetZone(projectile.CurrentPosition);
                if (zone != null)
                {
                    zone.Remove(projectile);
                }
            }

            RemoveObject(objectId);

            // 타인한테 정보 전송
            {
                S_Despawn despawnPacket = new S_Despawn();
                despawnPacket.ObjectIdList.Add(objectId);
                despawnPacket.PlayerCount = _players.Count;
                Broadcast(pos, despawnPacket);
            }
        }

        public void HandleMove(Player player, C_Move movePacket)
        {
            if (player == null || movePacket == null)
                return;

            // 서버에서 상태 업데이트
            player.ObjectState = movePacket.ObjectState;

            // 하드스냅 (일정 거리 이상 순간이동 방지)
            Vector3 serverPos = new Vector3(player.ObjectState.Position.X, player.ObjectState.Position.Y, player.ObjectState.Position.Z);
            Vector3 clientPos = new Vector3(movePacket.ObjectState.Position.X, movePacket.ObjectState.Position.Y, movePacket.ObjectState.Position.Z);

            float dist = Vector3.Distance(serverPos, clientPos);
            if (dist > 1.0f)
            {
                Console.WriteLine($"[Warning] Player {player.Id} position correction ({dist})");
                movePacket.ObjectState.Position = player.ObjectState.Position;
                movePacket.ObjectState.Velocity = new ProtoVector3 { X = 0, Y = 0, Z = 0 };
            }
            else
            {
                if (Map.CanGo(clientPos.X, clientPos.Z))
                {
                    // Zone 이동 확인
                    Zone nowZone = GetZone(player.CurrentPosition);
                    Zone afterZone = GetZone(clientPos);

                    if (nowZone != afterZone)
                    {
                        if (nowZone != null)
                        {
                            nowZone.Remove(player);
                        }
                        if (afterZone != null)
                        {
                            afterZone.Add(player);
                        }
                    }

                    // 이동 가능 
                    player.ObjectState.Position = movePacket.ObjectState.Position;
                }
                else
                {
                    // 이동 불가 (원래 서버 위치로 되돌림)
                    movePacket.ObjectState.Position = player.ObjectState.Position;
                    movePacket.ObjectState.Velocity = new ProtoVector3 { X = 0, Y = 0, Z = 0 };
                }
            }

            // 다른 유저들에게 브로드캐스트
            S_Move resMovePacket = new S_Move();
            resMovePacket.ObjectState = movePacket.ObjectState;
            resMovePacket.ObjectState.ServerReceivedTime = Util.GetTimestampMs();
            Broadcast(player.CurrentPosition, resMovePacket, player.Id);
        }

        private void PlayerInfoChanged()
        {

        }

        public void HandleChangeCreatureState(int objectId, CreatureState creatureState)
        {
            GameObjectType type = ObjectManager.Instance.GetObjectTypeById(objectId);
            GameObject gameObject = null;
            if (type == GameObjectType.Player)
            {
                _players.TryGetValue(objectId, out Player player);
                gameObject = player;
            }
            else if (type == GameObjectType.Monster)
            {
                _monsters.TryGetValue(objectId, out Monster monster);
                gameObject = monster;
            }
            else if (type == GameObjectType.Projectile)
            {
                _projectiles.TryGetValue(objectId, out Projectile projectile);
                gameObject = projectile;
            }

            if (gameObject == null)
                return;

            S_ChangeCreatureState changeCreatureStatePacket = new S_ChangeCreatureState();
            changeCreatureStatePacket.ObjectId = objectId;
            changeCreatureStatePacket.CreatureState = creatureState;
            Broadcast(gameObject.CurrentPosition, changeCreatureStatePacket, objectId);
        }

        public Player FindPlayer(Func<GameObject, bool> condition)
        {
            foreach (Player player in _players.Values)
            {
                if (condition.Invoke(player))
                    return player;
            }

            return null;
        }

        // AOI 기반 브로드캐스트
        public void Broadcast(Vector3 pos, IMessage packet)
        {
            List<Zone> adjacentZones = GetAdjacentZones(pos);
            foreach (Zone zone in adjacentZones)
            {
                foreach (Player p in zone.Players)
                {
                    if (p == null || p.Session == null)
                        continue;

                    // 인접한 존에 있다고 무조건 브로드캐스트 대상은
                    // 아닐 수 있으니 거리 확인
                    float dx = p.CurrentPosition.X - pos.X;
                    float dz = p.CurrentPosition.Z - pos.Z;

                    if (MathF.Abs(dx) > DataManager.Instance.AOICells)
                        continue;

                    if (MathF.Abs(dz) > DataManager.Instance.AOICells)
                        continue;

                    p.Session.Send(packet);
                }
            }
        }

        // AOI 기반 브로드캐스트 (제외자 있음)
        public void Broadcast(Vector3 pos, IMessage packet, int exceptId)
        {
            List<Zone> adjacentZones = GetAdjacentZones(pos);
            foreach (Zone zone in adjacentZones)
            {
                foreach (Player p in zone.Players)
                {
                    if (p == null || p.Session == null)
                        continue;

                    if (p.Id == exceptId)
                        continue;

                    // 인접한 존에 있다고 무조건 브로드캐스트 대상은
                    // 아닐 수 있으니 거리 확인
                    float dx = p.CurrentPosition.X - pos.X;
                    float dz = p.CurrentPosition.Z - pos.Z;

                    if (MathF.Abs(dx) > DataManager.Instance.AOICells)
                        continue;

                    if (MathF.Abs(dz) > DataManager.Instance.AOICells)
                        continue;

                    p.Session.Send(packet);
                }
            }
        }

        // 전체 브로드캐스트
        public void Broadcast(IMessage packet)
        {
            foreach (Player p in _players.Values)
            {
                if (p.Session == null)
                    continue;

                p.Session.Send(packet);
            }
        }

        // 전체 브로드캐스트 (제외자 있음)
        public void Broadcast(IMessage packet, int exceptId)
        {
            foreach (Player p in _players.Values)
            {
                if (p == null || p.Session == null)
                    continue;

                if (p.Id == exceptId)
                    continue;

                p.Session.Send(packet);
            }
        }

        private void AddObject(GameObject gameObject)
        {
            // 모든 오브젝트 관리하는 딕셔너리에 추가
            _gameObjects.Add(gameObject.Id, gameObject);

            // 분기별로 추가
            if (gameObject.ObjectType == GameObjectType.Player)
            {
                Player player = (Player)gameObject;
                _players.Add(player.Id, player);
            }
            else if (gameObject.ObjectType == GameObjectType.Monster)
            {
                Monster monster = (Monster)gameObject;
                _monsters.Add(monster.Id, monster);
            }
            else if (gameObject.ObjectType == GameObjectType.Projectile)
            {
                Projectile projectile = (Projectile)gameObject;
                _projectiles.Add(projectile.Id, projectile);
            }
        }

        private bool RemoveObject(int id)
        {
            if (_gameObjects.ContainsKey(id))
            {
                GameObjectType gameObjectType = ObjectManager.Instance.GetObjectTypeById(id);
                _gameObjects.Remove(id);
                if (gameObjectType == GameObjectType.Player)
                {
                    _players.Remove(id);
                    OnPlayerInfoChanged?.Invoke();
                }
                else if (gameObjectType == GameObjectType.Monster)
                {
                    _monsters.Remove(id);
                }
                else if (gameObjectType == GameObjectType.Projectile)
                {
                    _projectiles.Remove(id);
                }
                return true;
            }

            return false;
        }

        public List<Zone> GetAdjacentZones(Vector3 pos)
        {
            HashSet<Zone> zones = new HashSet<Zone>();
            // cells -> 현재 위치(pos)를 기준으로,
            // 얼마나 떨어진 좌표까지 검사해서 Zone을 가져올 것인가를 의미
            int cells = DataManager.Instance.AdjacentZonesCells;
            int[] delta = new int[2] { -cells, +cells };
            foreach (int dx in delta)
            {
                foreach (int dz in delta)
                {
                    int x = (int)pos.X + dx;
                    int z = (int)pos.Z + dz;
                    Zone zone = GetZone(new Vector3(x, 0, z));
                    if (zone == null)
                        continue;

                    zones.Add(zone);
                }
            }

            return zones.ToList();
        }

        private void InitMonsters()
        {
            //// TODO
            //for (int i = 0; i < 50; i++)
            //{
            //    SpawnMonster(MonsterType.Bear, new Vector3(100, -26, 527 + (i * 2)));
            //}
            SpawnMonster(MonsterType.Bear, new Vector3(100, -26, 527));
            SpawnMonster(MonsterType.Bear, new Vector3(80, -27, 500));
            SpawnMonster(MonsterType.Bear, new Vector3(100, -26, 420));
            SpawnMonster(MonsterType.Bear, new Vector3(100, -26, 480));
        }
    }
}
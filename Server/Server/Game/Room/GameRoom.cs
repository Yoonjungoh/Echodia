using Google.Protobuf;
using Google.Protobuf.Protocol;
using Microsoft.Extensions.Logging.Console;
using Server.Currency;
using Server.Data;
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
using System.Runtime.Serialization;
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
        private Dictionary<int, DropItem> _dropItems = new Dictionary<int, DropItem>();

        public bool IsRoomFull { get { return _players.Count == DataManager.Instance.MaxRoomPlayerCount; } }

        public event Action OnPlayerInfoChanged;  // 방 정보 바뀌었을 때 알림 (roomId)

        private PriorityQueue<int, DateTime> _respawnQueue = new PriorityQueue<int, DateTime>();
        private PriorityQueue<Action, DateTime> _delayedActions = new PriorityQueue<Action, DateTime>();

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

        /// <summary>GameRoom 스레드에서만 호출할 것. 지정된 ms 후에 action을 실행한다.</summary>
        public void ScheduleDelayedAction(int delayMs, Action action)
        {
            _delayedActions.Enqueue(action, DateTime.UtcNow.AddMilliseconds(delayMs));
        }

        // 어디선가 주기적으로 호출해줘야 함
        public void Update()
        {
            Flush();
            UpdateMonsters();
            UpdateProjectiles();
            UpdateRespawn();
            UpdateDelayedActions();
            UpdateAutoSave();
        }

        private long _nextAutoSaveTick = 0;

        private void UpdateAutoSave()
        {
            long now = Environment.TickCount64;
            if (now < _nextAutoSaveTick)
                return;

            int interval = ConfigManager.Instance.GetInt(ConfigType.AutoSaveDBIntervalMs);
            _nextAutoSaveTick = now + interval;

            foreach (Player player in _players.Values)
            {
                player.FlushDirtyState();
            }
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

        private void UpdateDelayedActions()
        {
            while (_delayedActions.TryPeek(out _, out DateTime executeAt))
            {
                if (executeAt > DateTime.UtcNow)
                    break;

                _delayedActions.Dequeue()();
            }
        }

        private void UpdateRespawn()
        {
            while (_respawnQueue.TryPeek(out int monsterId, out DateTime respawnTime))
            {
                // 아직 부활 시간이 안 됐으면 중단 (정렬되어 있으므로 뒤는 볼 필요 없음)
                if (respawnTime > DateTime.UtcNow)
                    break;

                // 시간 됐으면 큐에서 빼고 리스폰 처리
                _respawnQueue.Dequeue();
                ExecuteRespawn(monsterId);
            }
        }

        private void ExecuteRespawn(int monsterId)
        {
            if (_monsters.TryGetValue(monsterId, out Monster monster))
            {
                ConsoleLogManager.Instance.Log($"Respawn Monster: {monsterId}");
                monster.Position = monster.SpawnPosition;
                monster.ObjectState.Stat.Hp = monster.ObjectState.Stat.MaxHp;
                monster.CreatureState = CreatureState.Idle;
                monster.OnRespawn();
                EnterGame(monster);
                return;
            }

            ConsoleLogManager.Instance.Log($"Dont Exist Monster: {monsterId} in {ServerId}-{ChannelId}");
        }

        public void SpawnMonster(MonsterType monsterType, Vector3 spawnPos, float respawnSeconds, float spawnRadius = 0f)
        {
            Monster monster = MonsterFactory.Create(monsterType);

            Vector3 finalPos = spawnRadius > 0f
                ? FindValidSpawnPosition(spawnPos, spawnRadius)
                : spawnPos;

            monster.MonsterType = monsterType;
            monster.Name = $"{monsterType}_{monster.ObjectState.ObjectId}";
            monster.Position = MovementHelper.Vec3ToProtoVec3(finalPos);
            monster.SpawnPosition = new ProtoVector3 { X = finalPos.X, Y = finalPos.Y, Z = finalPos.Z };
            monster.RespawnTime = respawnSeconds;

            Push(EnterGame, monster);
        }

        // radius 내에서 CanGo이고 기존 몬스터와 겹치지 않는 위치를 탐색한다.
        // 30번 시도 후에도 못 찾으면 center를 반환한다.
        private static readonly Random _spawnRng = new Random();
        private const float MonsterMinSeparation = 1.5f;

        private Vector3 FindValidSpawnPosition(Vector3 center, float radius, int maxAttempts = 30)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // 원 안에서 균등 분포 랜덤 좌표 (rejection sampling 대신 sqrt로 반경 보정)
                float angle = (float)(_spawnRng.NextDouble() * Math.PI * 2.0);
                float dist = (float)(Math.Sqrt(_spawnRng.NextDouble()) * radius);

                float x = center.X + (float)Math.Cos(angle) * dist;
                float z = center.Z + (float)Math.Sin(angle) * dist;

                // 구조물 충돌 검사
                if (!Map.CanGo(x, z))
                    continue;

                // 이미 스폰된 몬스터와 최소 거리 검사
                bool tooClose = false;
                foreach (Monster m in _monsters.Values)
                {
                    float dx = m.Position.X - x;
                    float dz = m.Position.Z - z;
                    if (dx * dx + dz * dz < MonsterMinSeparation * MonsterMinSeparation)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                    continue;

                float y = Map.GetHeight(new Vector3(x, 0, z));
                return new Vector3(x, y, z);
            }

            ConsoleLogManager.Instance.Log($"[SpawnMonster] Valid position not found in radius={radius}, fallback to center");
            return center;
        }

        public void SpawnProjectile(int ownerId, ProjectileType projectileType)
            => SpawnProjectile(ownerId, projectileType, 1.0f);

        public void SpawnProjectile(int ownerId, ProjectileType projectileType, float damageCoefficient)
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
            projectile.DamageCoefficient = damageCoefficient;
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

            Console.WriteLine($"Owner: ({owner.Position.X},{owner.Position.Y},{owner.Position.Z})");
            Console.WriteLine($"Projectile: ({projectile.Position.X},{projectile.Position.Y},{projectile.Position.Z})");

            Push(EnterGame, projectile);
        }

        /// <summary>SkillTargetSelector가 탐색에 사용하는 전체 게임 오브젝트 열거.</summary>
        public IEnumerable<GameObject> GetAllObjects() => _gameObjects.Values;

        /// <summary>
        /// center 기준 range 반경 내 Zone들에 있는 Player/Monster를 열거한다.
        /// Zone AABB 기반 pre-filter이므로 호출부에서 정확한 distSq 체크를 추가로 수행해야 함.
        /// </summary>
        public IEnumerable<GameObject> GetObjectsInRange(Vector3 center, float range)
        {
            int minX = Math.Max(0, (int)((center.X - range - Map.MapData.MinX) / ZoneCells));
            int maxX = Math.Min(Zones.GetLength(0) - 1, (int)((center.X + range - Map.MapData.MinX) / ZoneCells));
            int minZ = Math.Max(0, (int)((center.Z - range - Map.MapData.MinZ) / ZoneCells));
            int maxZ = Math.Min(Zones.GetLength(1) - 1, (int)((center.Z + range - Map.MapData.MinZ) / ZoneCells));

            for (int x = minX; x <= maxX; ++x)
            {
                for (int z = minZ; z <= maxZ; ++z)
                {
                    Zone zone = Zones[x, z];
                    if (zone == null) 
                        continue;

                    foreach (Player p in zone.Players) 
                        yield return p;
                        
                    foreach (Monster m in zone.Monsters) 
                        yield return m;
                }
            }
        }

        public void HandleUseSkill(int playerId, int skillId)
        {
            _players.TryGetValue(playerId, out Player player);
            if (player == null || player.SkillExecutor == null)
            {
                return;
            }

            UseSkillResult result = player.SkillExecutor.CanUse(skillId);

            if (result != UseSkillResult.Success)
            {
                S_UseSkill failPacket = new S_UseSkill
                {
                    CasterId = playerId,
                    SkillId = skillId,
                    Result = result,
                };
                player.Session?.Send(failPacket);
                return;
            }

            player.SkillExecutor.Use(skillId);
            // S_UseSkill Success 브로드캐스트는 SkillExecutor.Use() 내부에서 담당
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
            if (projectile == null || projectile.HitCount >= projectile.MaxHitCount)
                return;

            _gameObjects.TryGetValue(damagedObjectId, out GameObject damagedObject);
            if (damagedObject == null)
                return;

            // 서버에서 예측한 투사체 위치랑 적 위치 비교해서 오차 심하지 않으면 데미지 허용
            Vector3 damagedObjectPos = damagedObject.CurrentPosition;
            //float dist = Vector3.Distance(projectile.CurrentPosition, damagedObjectPos);
            //if (dist > DataManager.Instance.ProjectileDistanceErrorThreshold)
            //{
            //    // 너무 멀리 떨어져 있음
            //    ConsoleLogManager.Instance.Log($"[Warning] Projectile attack distance too far: {dist}");
            //    return;
            //}

            // 데미지 처리
            S_Attack attackPacket = new S_Attack();

            int damage;
            bool isCritical = false;
            _gameObjects.TryGetValue(projectile.ObjectState.OwnerId, out GameObject ownerObj);
            Player ownerPlayer = ownerObj is Player p ? p : null;

            if (ownerPlayer != null)
            {
                (int baseDamage, bool isCrit) = ownerPlayer.StatCalculator.GetFinalDamage();
                damage = (int)(baseDamage * projectile.DamageCoefficient);
                isCritical = isCrit;
            }
            else
            {
                damage = (int)(projectile.ObjectState.Stat.MagicMissileAttakDamage * projectile.DamageCoefficient);
            }

            damagedObject.OnDamaged(projectile, damage);
            projectile.HitCount++;   // 히트 카운트 증가, MaxHitCount 도달 시 이후 요청 거부

            DamagedInfo damagedInfo = new DamagedInfo();
            damagedInfo.ObjectId = damagedObjectId;
            damagedInfo.RemainHp = damagedObject.ObjectState.Stat.Hp;
            damagedInfo.IsCritical = isCritical;
            attackPacket.DamagedObjectList.Add(damagedInfo);

            // 디스폰도 같이 처리해줘야 함
            LeaveGame(projectile.Id);

            // 투사체 위치는 맵 경계 밖일 수 있으므로 항상 유효한 존 안에 있는 피격 오브젝트 위치로 브로드캐스트
            Broadcast(damagedObjectPos, attackPacket);
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
                if (target == null || target.Id == instigator.Id)
                    continue;

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
            Player attackerPlayer = instigator is Player ap ? ap : null;
            foreach (int objectId in damagedObjectList)
            {
                _gameObjects.TryGetValue(objectId, out GameObject damagedObject);
                if (damagedObject == null)
                    continue;

                int damage = -1;
                bool isCritical = false;
                // 플레이어 이외에는 일반 공격 데미지만 적용
                if (attackerPlayer != null)
                {
                    (damage, isCritical) = attackerPlayer.StatCalculator.GetFinalDamage();
                }
                else
                {
                    damage = instigator.ObjectState.Stat.CommonAttackDamage;
                }

                damagedObject.OnDamaged(instigator, damage);

                DamagedInfo damagedInfo = new DamagedInfo();
                damagedInfo.ObjectId = objectId;
                damagedInfo.RemainHp = damagedObject.ObjectState.Stat.Hp;
                damagedInfo.IsCritical = isCritical;
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

            // 레벨, exp 초기화
            enteGamePacket.ObjectState.Level = gameObject.Level;
            enteGamePacket.ObjectState.Exp = gameObject.Exp;

            // position 초기화 - zone.Add 전에 올바른 위치를 먼저 설정해야 정확한 존에 등록됨
            Vector3 startPos = Vector3.Zero;
            if (objectType == GameObjectType.Player)
            {
                Player player = gameObject as Player;

                // 맵 이동(Transfer) 시 PendingTransferPosition 우선 사용
                if (player.PendingTransferPosition.HasValue)
                {
                    startPos = player.PendingTransferPosition.Value;
                    player.PendingTransferPosition = null;
                }
                else
                {
                    Vector3 lastLogoutPos = ObjectManager.Instance.GetPlayerLastLogoutPos(player.PlayerId);
                    if (lastLogoutPos.X == int.MinValue && lastLogoutPos.Y == int.MinValue && lastLogoutPos.Z == int.MinValue)
                    {
                        // 새 플레이어일 경우 맵의 EnterPoint 우선
                        SpawnPointData enterPoint = Map.MapData?.EnterPoint;
                        startPos = enterPoint.Position;
                    }
                    else
                    {
                        startPos = lastLogoutPos;
                    }
                }
                float groundY = Map.GetHeight(startPos);
                if (groundY > Map.NO_HEIGHT_VALUE)
                    startPos.Y = groundY;
            }
            else
            {
                startPos = MovementHelper.ProtoVec3ToVec3(gameObject.Position);
                // 몬스터/투사체 등도 지형 높이로 Y 보정 (보정 안 하면 클라이언트 물리와 충돌해 시각적으로 튀는 현상 발생)
                if (objectType == GameObjectType.Monster)
                {
                    float groundY = Map.GetHeight(startPos);
                    if (groundY > Map.NO_HEIGHT_VALUE)
                        startPos.Y = groundY;
                }
            }
            gameObject.Position.X = startPos.X;
            gameObject.Position.Y = startPos.Y;
            gameObject.Position.Z = startPos.Z;

            // Type 관련 분기 초기화
            Zone zone = GetZone(gameObject.CurrentPosition);
            if (zone != null)
            {
                zone.Add(gameObject);
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
            else if (objectType == GameObjectType.DropItem)
            {
                DropItem dropItem = gameObject as DropItem;
                // OwnerId 필드에 SpecData 아이템 ID, Level 필드에 수량을 저장 (클라이언트 렌더링용 컨벤션)
                gameObject.ObjectState.OwnerId = dropItem.ItemId;
                gameObject.ObjectState.Level = dropItem.Count;
            }

            // name 초기화
            enteGamePacket.ObjectState.Name = gameObject.Name;

            enteGamePacket.ObjectState.Position.X = gameObject.ObjectState.Position.X;
            enteGamePacket.ObjectState.Position.Y = gameObject.ObjectState.Position.Y;
            enteGamePacket.ObjectState.Position.Z = gameObject.ObjectState.Position.Z;

            // stat 초기화
            enteGamePacket.ObjectState.Stat = gameObject.Stat;

            // 맵 Id 세팅 (클라이언트가 올바른 맵 바이너리를 로드하도록)
            enteGamePacket.MapId = MapId;

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
            Broadcast(MovementHelper.ProtoVec3ToVec3(gameObject.Position), spawnToOthersPacket);
            //Broadcast(spawnToOthersPacket);   // 더미 클라 테스트 용
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
                ObjectManager.Instance.Return(projectile);
            }
            else if (type == GameObjectType.DropItem)
            {
                DropItem dropItem = null;
                if (_dropItems.TryGetValue(objectId, out dropItem) == false)
                    return;

                pos = dropItem.CurrentPosition;

                Zone zone = GetZone(dropItem.CurrentPosition);
                if (zone != null)
                {
                    zone.Remove(dropItem);
                }
                ObjectManager.Instance.Return(dropItem);
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

        // 맵 이동 함수
        // 현재 룸에서 플레이어를 제거하고 새 룸으로 입장시킴
        // S_LeaveGame으로 처리 안 하고, 클라이언트는 S_MapTransfer로 맵 전환을 준비함
        public void TransferPlayer(int playerId, int newMapId, Vector3 spawnPos)
        {
            if (!_players.TryGetValue(playerId, out Player player))
                return;

            // 1. 현재 룸에서 제거 (S_LeaveGame 없이 S_Despawn만 전송)
            Vector3 curPos = player.CurrentPosition;

            Zone zone = GetZone(curPos);
            zone?.Remove(player);

            player.OnLeaveGame();   // DB 저장 (퀘스트, 인벤토리, 스탯)
            player.AOI.PreviousGameObjects.Clear();  // 이전 룸 오브젝트 참조 해제
            player.GameRoom = null;

            RemoveObject(playerId);

            S_Despawn despawnPacket = new S_Despawn();
            despawnPacket.ObjectIdList.Add(playerId);
            despawnPacket.PlayerCount = _players.Count;
            Broadcast(curPos, despawnPacket);

            // 2. 클라에게 맵 전환 알림 (이후 S_EnterGame이 옴)
            S_MapTransfer mapTransferPacket = new S_MapTransfer { MapId = newMapId };
            player.Session?.Send(mapTransferPacket);

            // 3. 새 룸에 스폰 위치를 심어두고 입장
            player.PendingTransferPosition = spawnPos;

            ServerChannel channel = ServerManager.Instance.FindChannel(ServerId, ChannelId);
            GameRoom newRoom = channel?.GameRoomManager.Find(newMapId);
            if (newRoom == null)
            {
                ConsoleLogManager.Instance.Log($"[TransferPlayer] New room not found: MapId={newMapId}");
                return;
            }

            newRoom.Push(newRoom.EnterGame, player);
        }

        public void HandleMove(Player player, C_Move movePacket)
        {
            if (player == null || movePacket == null)
                return;

            // ObjectState 전체를 교체하면 Stat 등 패킷에 없는 필드가 null이 되므로 위치/속도/회전만 개별 업데이트
            Vector3 clientPos = new Vector3(movePacket.ObjectState.Position.X, movePacket.ObjectState.Position.Y, movePacket.ObjectState.Position.Z);

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

                // Y축 지형 높이 보정 (더미클라 등 Y가 잘못된 경우 서버에서 교정)
                float groundY = Map.GetHeight(clientPos);
                if (groundY > Map.NO_HEIGHT_VALUE)
                {
                    clientPos.Y = groundY;
                    movePacket.ObjectState.Position.Y = groundY;
                }

                player.ObjectState.Position.X = movePacket.ObjectState.Position.X;
                player.ObjectState.Position.Y = movePacket.ObjectState.Position.Y;
                player.ObjectState.Position.Z = movePacket.ObjectState.Position.Z;
            }
            else
            {
                // 이동 불가: 클라이언트에 서버 위치 되돌려주기
                movePacket.ObjectState.Position.X = player.ObjectState.Position.X;
                movePacket.ObjectState.Position.Y = player.ObjectState.Position.Y;
                movePacket.ObjectState.Position.Z = player.ObjectState.Position.Z;
                movePacket.ObjectState.Velocity = new ProtoVector3 { X = 0, Y = 0, Z = 0 };
            }

            // Stat 등 중요 필드를 덮어쓰지 않도록 velocity/rotation/state만 개별 업데이트
            player.ObjectState.Velocity.X = movePacket.ObjectState.Velocity.X;
            player.ObjectState.Velocity.Y = movePacket.ObjectState.Velocity.Y;
            player.ObjectState.Velocity.Z = movePacket.ObjectState.Velocity.Z;
            player.ObjectState.Rotation.X = movePacket.ObjectState.Rotation.X;
            player.ObjectState.Rotation.Y = movePacket.ObjectState.Rotation.Y;
            player.ObjectState.Rotation.Z = movePacket.ObjectState.Rotation.Z;
            player.ObjectState.Rotation.W = movePacket.ObjectState.Rotation.W;
            player.ObjectState.CreatureState = movePacket.ObjectState.CreatureState;
            // CurrentPosition 계산에 사용되므로 반드시 갱신
            player.ObjectState.ServerReceivedTime = Util.GetTimestampMs();

            // 다른 유저들에게 브로드캐스트
            S_Move resMovePacket = new S_Move();
            resMovePacket.ObjectState = new ObjectState();
            resMovePacket.ObjectState.MergeFrom(player.ObjectState);
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

        public Player Find(int id)
        {
            if (_players.ContainsKey(id))
            {
                return _players[id];
            }

            return null;
        }

        // DB PlayerId로 플레이어 검색 (KillRewardManager에서 사용)
        public Player FindPlayerByPlayerId(int playerId)
        {
            foreach (Player p in _players.Values)
            {
                if (p.PlayerId == playerId)
                    return p;
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
            if (gameObject == null)
                return;

            // 모든 오브젝트 관리하는 딕셔너리에 추가
            _gameObjects.Add(gameObject.Id, gameObject);

            // 분기별로 추가
            if (gameObject.ObjectType == GameObjectType.Player &&
                _players.ContainsKey(gameObject.Id) == false)
            {
                Player player = (Player)gameObject;
                _players.Add(player.Id, player);
            }
            else if (gameObject.ObjectType == GameObjectType.Monster &&
                _monsters.ContainsKey(gameObject.Id) == false)
            {
                Monster monster = (Monster)gameObject;
                _monsters.Add(monster.Id, monster);
            }
            else if (gameObject.ObjectType == GameObjectType.Projectile &&
                _projectiles.ContainsKey(gameObject.Id) == false)
            {
                Projectile projectile = (Projectile)gameObject;
                _projectiles.Add(projectile.Id, projectile);
            }
            else if (gameObject.ObjectType == GameObjectType.DropItem &&
                _dropItems.ContainsKey(gameObject.Id) == false)
            {
                DropItem dropItem = (DropItem)gameObject;
                _dropItems.Add(dropItem.Id, dropItem);
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
                    // _monsters.Remove(id);
                }
                else if (gameObjectType == GameObjectType.Projectile)
                {
                    _projectiles.Remove(id);
                }
                else if (gameObjectType == GameObjectType.DropItem)
                {
                    _dropItems.Remove(id);
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
            int[] delta = new int[3] { -cells, 0, +cells };
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

        public void ReserveRespawn(int monsterId, ProtoVector3 respawnPosition, float respawnTime)
        {
            _respawnQueue.Enqueue(monsterId, DateTime.UtcNow.AddSeconds(respawnTime));
        }

        public DropItem FindDropItem(int objectId)
        {
            _dropItems.TryGetValue(objectId, out DropItem dropItem);
            return dropItem;
        }

        // 몬스터 사망 시 드롭 테이블 기반으로 아이템 스폰
        public void SpawnDropItems(int monsterTemplateId, Vector3 deathPos)
        {
            MonsterMetaData monsterMeta = SpecDataManager.Instance.GetMonster(monsterTemplateId);
            if (monsterMeta == null || monsterMeta.DropItemGroupId == 0)
                return;

            List<DropItemMetaData> dropTable = SpecDataManager.Instance.GetAllDropItem()
                .FindAll(d => d.DropItemGroupId == monsterMeta.DropItemGroupId);

            if (dropTable.Count == 0)
                return;

            Random rng = new Random();
            foreach (DropItemMetaData drop in dropTable)
            {
                // Probability는 0~10000 기준 (10000 = 100%)
                int roll = rng.Next(0, 10000);
                if (roll >= drop.Probability)
                    continue;

                int count = rng.Next(drop.MinCount, drop.MaxCount + 1);
                if (count <= 0)
                    continue;

                // 사망 위치 주변 랜덤 위치에 드롭 (반경 2.5f)
                float scatterRadius = 2.5f;
                float angle = (float)(rng.NextDouble() * 2 * Math.PI);
                float r = (float)(rng.NextDouble() * scatterRadius);
                float dropX = deathPos.X + r * MathF.Cos(angle);
                float dropZ = deathPos.Z + r * MathF.Sin(angle);

                // 맵 높이에 맞게 Y 설정
                float dropY = Map.GetHeight(new Vector3(dropX, deathPos.Y, dropZ));
                if (dropY < -9000f)  // Map.NO_HEIGHT_VALUE = -9999f
                    dropY = deathPos.Y;

                SpawnDropItem(drop.ItemId, count, new Vector3(dropX, dropY, dropZ));
            }
        }

        private void SpawnDropItem(int itemId, int count, Vector3 pos)
        {
            DropItem dropItem = ObjectManager.Instance.RentDropItem();
            dropItem.ItemId = itemId;
            dropItem.Count = count;
            dropItem.Position = MovementHelper.Vec3ToProtoVec3(pos);
            dropItem.SpawnPosition = dropItem.Position;

            Push(EnterGame, dropItem);
        }

        private void InitMonsters()
        {
            var spawners = Map.MapData?.MonsterSpawners;
            if (spawners == null || spawners.Count == 0)
                return;

            foreach (MonsterSpawnerData spawner in spawners)
            {
                MonsterType monsterType = (MonsterType)spawner.MonsterTypeId;
                Vector3 spawnPos = spawner.Position;
                for (int i = 0; i < spawner.Count; i++)
                {
                    SpawnMonster(monsterType, spawnPos, spawner.RespawnSeconds, spawner.SpawnRadius);
                }
            }
        }

        // ─────────────────────────────────────────
        // 파티 패킷 핸들러 (GameRoom 잡 큐 안에서 실행)
        // ─────────────────────────────────────────

        public void HandleCreateParty(int playerObjectId)
        {
            Player player = Find(playerObjectId);
            if (player == null)
                return;
            PartyManager.Instance.CreateParty(this, player);
        }

        public void HandlePartyInvite(int inviterObjectId, int targetObjectId)
        {
            Player inviter = Find(inviterObjectId);
            if (inviter == null)
                return;
            PartyManager.Instance.SendInvite(this, inviter, targetObjectId);
        }

        public void HandlePartyInviteResponse(
            int inviterObjectId,
            int responderObjectId,
            PartyInviteResponseType response)
        {
            Player responder = Find(responderObjectId);
            if (responder == null)
                return;
            PartyManager.Instance.HandleInviteResponse(this, responder, inviterObjectId, response);
        }

        public void HandlePartyLeave(int playerObjectId)
        {
            PartyManager.Instance.LeaveParty(this, playerObjectId);
        }

        public void HandlePartyKick(int kickerObjectId, int targetObjectId)
        {
            PartyManager.Instance.KickMember(this, kickerObjectId, targetObjectId);
        }

        public void HandleRequestRoomPlayerList(int requesterObjectId)
        {
            Player requester = Find(requesterObjectId);
            if (requester == null)
                return;

            Party myParty = PartyManager.Instance.GetPartyOf(requesterObjectId);

            var packet = new S_RequestRoomPlayerList();
            foreach (Player p in _players.Values)
            {
                if (p.Id == requesterObjectId)
                    continue;

                bool inParty   = PartyManager.Instance.IsInParty(p.Id);
                bool inMyParty = myParty != null && myParty.Contains(p.Id);

                packet.PlayerList.Add(new RoomPlayerInfo
                {
                    ObjectId    = p.Id,
                    Name        = p.Name,
                    Level       = p.Level,
                    JobType     = p.Stat.JobType,
                    IsInParty   = inParty,
                    IsInMyParty = inMyParty,
                });
            }

            requester.Session?.Send(packet);
        }
    }
}
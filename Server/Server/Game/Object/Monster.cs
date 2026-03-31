using Google.Protobuf.Protocol;
using Server.Currency;
using Server.DB;
using Server.Game;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Server.Game
{
    public class Monster : GameObject
    {
        protected float _searchRange;
        protected Player _target;
        protected int _gold;
        protected float _respawnTime;
        public Monster()
        {
            ObjectType = GameObjectType.Monster;

            CreatureState = CreatureState.Idle;
        }

        public override void Update()
        {
            switch (CreatureState)
            {
                case CreatureState.Idle:
                    UpdateIdle();
                    break;
                case CreatureState.Move:
                    UpdateMove();
                    break;
                case CreatureState.Attack:
                    UpdateAttack();
                    break;
            }
        }

        long _nextSearchTick = 0;
        int _searchTick = 500;

        public virtual void UpdateIdle()
        {
            if (_nextSearchTick > Environment.TickCount64)
                return;

            _nextSearchTick = Environment.TickCount64 + _searchTick;

            Player target = GameRoom.FindPlayer(p =>
            {
                if (p == null)
                    return false;

                Vector3 playerPos = p.CurrentPosition;
                Vector3 dir = playerPos - CurrentPosition;
                float cellDist = Math.Abs(dir.X) + Math.Abs(dir.Y);
                return cellDist <= _searchRange;
            });

            if (target == null)
                return;

            _target = target;
            CreatureState = CreatureState.Move;
            BroadCastCurrentState();
        }

        // 이동 관련 변수들
        private long _nextMoveTick = 0;
        private long _nextPathTick = 0;
        private int _pathInterval = 500;
        private List<Vector3> _cachedPath = null;

        private Vector3 _lastDir = Vector3.Zero;

        protected virtual void UpdateMove()
        {
            if (_target == null || _target.GameRoom == null)
            {
                _cachedPath = null;
                CreatureState = CreatureState.Idle;
                return;
            }

            Vector3 diff = _target.CurrentPosition - CurrentPosition;
            float dist = diff.Length();

            // 공격 범위 체크를 이동 쿨타임보다 먼저 실행
            // (_nextMoveTick 중에 플레이어가 범위 안에 들어와도 즉시 Attack 전환)
            if (dist <= Stat.AttackRange)
            {
                CreatureState = CreatureState.Attack;
                BroadCastCurrentState();
                return;
            }

            if (_nextMoveTick > Environment.TickCount64)
                return;

            long moveTick = (long)(400 / 2f);
            _nextMoveTick = Environment.TickCount64 + moveTick;

            bool pathRecalculated = false;
            if (_cachedPath == null || Environment.TickCount64 >= _nextPathTick)
            {
                _cachedPath = GameRoom.Map.FindPath(CurrentPosition, _target.CurrentPosition);
                _nextPathTick = Environment.TickCount64 + _pathInterval;
                pathRecalculated = true;
            }

            if (_cachedPath == null || _cachedPath.Count < 2)
                return;

            // 웨이포인트 전진: 충분히 가까워졌거나, 지나쳤을 때(overshoot) 다음으로 이동
            // overshoot 판정: (현재→wp) 방향이 (prev→wp) 방향과 반대이면 이미 지나침
            const float waypointReachDist = 0.5f;
            while (_cachedPath.Count >= 2)
            {
                Vector3 toWp = _cachedPath[1] - CurrentPosition;
                toWp.Y = 0;
                bool reachedClose = toWp.LengthSquared() < waypointReachDist * waypointReachDist;

                bool overshot = false;
                if (!reachedClose)
                {
                    Vector3 pathSeg = _cachedPath[1] - _cachedPath[0];
                    pathSeg.Y = 0;
                    // 경로 진행 방향과 현재→wp 방향이 반대 → 이미 지나침
                    if (pathSeg.LengthSquared() > 0.0001f)
                        overshot = Vector3.Dot(toWp, pathSeg) < 0f;
                }

                if (reachedClose || overshot)
                    _cachedPath.RemoveAt(0);
                else
                    break;
            }

            if (_cachedPath.Count < 2)
                return;

            Vector3 nextPos = _cachedPath[1];
            Vector3 toNextPos = nextPos - CurrentPosition;
            toNextPos.Y = 0;
            if (toNextPos.LengthSquared() < 0.0001f)
                return;

            Vector3 moveDir = Vector3.Normalize(toNextPos);

            // 경로 재계산 시 EWMA 방향 초기화 (이전 방향이 새 경로와 충돌하여 뒤돌아보는 현상 방지)
            if (pathRecalculated)
                _lastDir = Vector3.Zero;

            Vector3 finalDir = SmoothDirection(moveDir);
            MoveByDirection(finalDir, moveTick);
        }

        private Vector3 SmoothDirection(Vector3 newDir)
        {
            if (_lastDir == Vector3.Zero)
                _lastDir = newDir;

            // 관성 비율을 낮춰 A* 경로를 더 빠르게 따라가도록 수정
            // (0.8/0.2 비율은 방향 전환이 너무 느려 경로 재계산 시 우회로가 발생했음)
            _lastDir = Vector3.Normalize(_lastDir * 0.3f + newDir * 0.7f);
            return _lastDir;
        }

        private void MoveByDirection(Vector3 dir, long moveTick)
        {
            float deltaSec = moveTick / 1000f;
            float moveDist = Stat.MoveSpeed * deltaSec;

            Vector3 newPos = CurrentPosition + dir * moveDist;

            // 지형 높이 읽기
            float groundY = GameRoom.Map.GetHeight(newPos);
            if (groundY == -9999)
                return;

            float currentY = CurrentPosition.Y;
            float diff = groundY - currentY;

            // 1. 경사면 미세한 차이는 그대로 따라가기
            if (Math.Abs(diff) <= 0.3f)
            {
                newPos.Y = groundY;
            }
            else
            {
                // 2. 급경사 / 튐 방지: 변화량 제한
                if (diff > 1f) diff = 1f;
                if (diff < -1f) diff = -1f;

                // 3. 부드럽게 보간하여 높이 이동
                newPos.Y = currentY + diff * 0.35f;
            }

            // Zone에 적용
            Zone nowZone = GameRoom.GetZone(CurrentPosition);
            Zone afterZone = GameRoom.GetZone(newPos);

            if (nowZone != afterZone)
            {
                if (nowZone != null)
                {
                    nowZone.Remove(this);
                }
                if (afterZone != null)
                {
                    afterZone.Add(this);
                }
            }

            ObjectState.Position.X = newPos.X;
            ObjectState.Position.Y = newPos.Y;
            ObjectState.Position.Z = newPos.Z;

            ObjectState.Rotation = MovementHelper.LookAt(dir);


            S_Move move = new S_Move();
            move.ObjectState = ObjectState;
            move.ObjectState.ServerReceivedTime = Util.GetTimestampMs();
            GameRoom.Broadcast(CurrentPosition, move);
        }


        long _nextAttackTick = 0;

        protected virtual void UpdateAttack()
        {
            if (_target == null || _target.GameRoom == null || _target.CreatureState == CreatureState.Die)
            {
                CreatureState = CreatureState.Idle;
                BroadCastCurrentState();
                return;
            }

            // 쿨타임 중에는 범위 밖이어도 Move로 전환하지 않음 (공격 딜레이 동안 대기)
            if (_nextAttackTick > 0 && _nextAttackTick > Environment.TickCount64)
                return;

            Vector3 diff = _target.CurrentPosition - CurrentPosition;
            float dist = diff.Length();

            // 쿨타임이 끝난 후에만 범위 체크 → 범위 밖이면 추격 시작
            if (dist > Stat.AttackRange)
            {
                CreatureState = CreatureState.Move;
                BroadCastCurrentState();
                return;
            }

            // 쿨타임 막 끝난 경우 0으로 리셋해서 다음 프레임에 공격 실행
            if (_nextAttackTick > 0)
            {
                _nextAttackTick = 0;
                return;
            }

            // _nextAttackTick == 0 → 공격 실행
            // 같은 GameRoom의 job 컨텍스트 안에서 실행 중이므로 직접 호출 (Push하면 브로드캐스트 시점에 HP가 갱신 안 됨)
            _target.OnDamaged(this, Stat.CommonAttackDamage);

            // 상태 먼저 브로드캐스트
            BroadCastCurrentState();

            // 공격 데미지 반영 브로드캐스트
            S_Attack attackPacket = new S_Attack();
            attackPacket.AttackType = AttackType.CommonAttack;
            attackPacket.InstigatorId = Id;

            DamagedInfo damagedInfo = new DamagedInfo();
            damagedInfo.ObjectId = _target.Id;
            damagedInfo.RemainHp = _target.Stat.Hp;  // OnDamaged 이후이므로 올바른 HP

            attackPacket.DamagedObjectList.Add(damagedInfo);
            GameRoom.Broadcast(CurrentPosition, attackPacket);

            _nextAttackTick = Environment.TickCount64 + (long)(Stat.CommonAttackCoolTime * 1000);
        }

        public virtual void OnRespawn()
        {
            _target = null;
            _cachedPath = null;
            _nextSearchTick = 0;
            _nextMoveTick = 0;
            _nextAttackTick = 0;
            _lastDir = Vector3.Zero;
        }

        public override void OnDead(GameObject instigator)
        {
            // 리스폰 큐에 넣기
            GameRoom.Push(GameRoom.ReserveRespawn, Id, SpawnPosition, _respawnTime);

            Player killer = null;
            GameObjectType gameObjectType = ObjectManager.Instance.GetObjectTypeById(instigator.Id);
            if (gameObjectType == GameObjectType.Player)
            {
                killer = instigator as Player;
            }
            else if (gameObjectType == GameObjectType.Projectile)
            {
                Projectile projectile = instigator as Projectile;
                if (projectile != null)
                {
                    killer = GameRoom.Find(projectile.OwnerId);
                }
            }

            if (killer != null)
            {
                CurrencyManager.Instance.AddCurrency(killer, CurrencyType.Gold, _gold);
                QuestManager.Instance.OnMonsterKilled(GameRoom, killer, TemplateId);
            }

            // 드롭 아이템 스폰 (사망 위치 주변에 랜덤 배치)
            Vector3 deathPos = CurrentPosition;
            GameRoom.Push(GameRoom.SpawnDropItems, TemplateId, deathPos);

            base.OnDead(instigator);
        }

        protected void BroadCastCurrentState()
        {
            S_ChangeCreatureState changeCreatureStatePacket = new S_ChangeCreatureState();
            changeCreatureStatePacket.ObjectId = Id;
            changeCreatureStatePacket.CreatureState = CreatureState;
            GameRoom.Broadcast(CurrentPosition, changeCreatureStatePacket);
        }
    }
}

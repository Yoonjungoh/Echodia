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
        protected float _searchRange = 7.0f;
        protected Player _target;
        protected int _gold;
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
            if (_nextMoveTick > Environment.TickCount64)
                return;

            long moveTick = (long)(400 / 2f);
            _nextMoveTick = Environment.TickCount64 + moveTick;

            if (_target == null || _target.GameRoom == null)
            {
                _cachedPath = null;
                CreatureState = CreatureState.Idle;
                return;
            }

            Vector3 diff = _target.CurrentPosition - CurrentPosition;
            float dist = diff.Length();

            // 범위 내에 오면 공격
            if (dist <= Stat.AttackRange)
            {
                // 즉시 바라보게 하기
                Vector3 dir = Vector3.Normalize(diff);
                MoveByDirection(dir, moveTick);
                CreatureState = CreatureState.Attack;
                return;
            }

            if (_cachedPath == null || Environment.TickCount64 >= _nextPathTick)
            {
                _cachedPath = GameRoom.Map.FindPath(CurrentPosition, _target.CurrentPosition);
                _nextPathTick = Environment.TickCount64 + _pathInterval;
            }

            if (_cachedPath == null || _cachedPath.Count < 2)
                return;

            Vector3 nextPos = _cachedPath[1];
            Vector3 moveDir = Vector3.Normalize(nextPos - CurrentPosition);

            Vector3 finalDir = SmoothDirection(moveDir);
            MoveByDirection(finalDir, moveTick);
        }

        private Vector3 SmoothDirection(Vector3 newDir)
        {
            if (_lastDir == Vector3.Zero)
                _lastDir = newDir;

            _lastDir = Vector3.Normalize(_lastDir * 0.8f + newDir * 0.2f);
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

            Vector3 diff = _target.CurrentPosition - CurrentPosition;
            float dist = diff.Length();

            // 공격 거리 밖이면 다시 추격 시작
            if (dist > Stat.AttackRange)
            {
                CreatureState = CreatureState.Move;
                BroadCastCurrentState();
                return;
            }

            if (_nextAttackTick == 0)
            {
                // 우선 단일 타깃
                if (GameRoom != null)
                {
                    //_target.OnDamaged(this, Stat.CommonAttackDamage);
                    GameRoom.Push(_target.OnDamaged, this, Stat.CommonAttackDamage);
                }

                // 상태 먼저 브로드캐스트
                BroadCastCurrentState();

                // 공격 데미지 반영 브로드캐스트
                S_Attack attackPacket = new S_Attack();
                attackPacket.AttackType = AttackType.CommonAttack;
                attackPacket.InstigatorId = Id;

                DamagedInfo damagedInfo = new DamagedInfo();
                damagedInfo.ObjectId = _target.Id;
                damagedInfo.RemainHp = _target.Stat.Hp;

                attackPacket.DamagedObjectList.Add(damagedInfo);
                GameRoom.Broadcast(CurrentPosition, attackPacket);

                _nextAttackTick = Environment.TickCount64 + (long)(Stat.CommonAttackCoolTime * 1000);
                return;
            }

            if (_nextAttackTick > Environment.TickCount64)
                return;

            _nextAttackTick = 0;
        }

        public override void OnDead(GameObject instigator)
        {
            GameObjectType gameObjectType = ObjectManager.Instance.GetObjectTypeById(instigator.Id);
            if (gameObjectType == GameObjectType.Player)
            {
                Player player = instigator as Player;
                if (player != null)
                {
                    CurrencyManager.Instance.AddCurrency(player.PlayerId, CurrencyType.Gold, _gold, () =>
                    {
                        player.Session.HandleUpdateCurrencyData(CurrencyType.Gold);
                    });
                }
            }
            else if (gameObjectType == GameObjectType.Projectile)
            {
                Projectile projectile = instigator as Projectile;
                if (projectile != null)
                {
                    Player player = GameRoom.Find(projectile.OwnerId);
                    if (player != null)
                    {
                        CurrencyManager.Instance.AddCurrency(player.PlayerId, CurrencyType.Gold, _gold, () =>
                        {
                            player.Session.HandleUpdateCurrencyData(CurrencyType.Gold);
                        });
                    }
                }
            }
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

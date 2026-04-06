using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using UnityEngine;

public class MyPlayerController : PlayerController
{
    private const float ROT_THRESHOLD = 2.0f;
    private const float MOVE_THRESHOLD = 0.05f;
    private const float FALL_SPEED_THRESHOLD = 1.0f;

    [SerializeField] private float _rotateSpeed = 10.0f;
    public float RotateSpeed { get { return _rotateSpeed; } }

    private Vector3 _moveDir = Vector3.zero;
    private Vector3 _prevVelocity;
    private Quaternion _prevRotation;
    private Transform _cameraTransform;

    private float _lastAttackTime = -999f;

    private readonly C_Move _movePacket = new C_Move();
    private ObjectState _moveState = new ObjectState();
    private readonly ProtoVector3 _movePos = new ProtoVector3();
    private readonly ProtoVector3 _moveVel = new ProtoVector3();
    private readonly ProtoQuaternion _moveRot = new ProtoQuaternion();

    private Dictionary<AttackType, float> _attackCoolTimeDict;
    private ProjectileType _projectileType = ProjectileType.None;

    private Action<int> OnLevelChanged;
    private Action<int, int> OnExpChanged;
    public Action<float, float> OnHpChanged;
    public Action<int, int> OnDetectedDropItem;

    private LayerMask _dropItemLayer;
    private float _dropItemPickupRadius;
    private Collider[] _hitResults = new Collider[10];

    private UI_DropItem _closestDropItem;
    private const float PROXIMITY_CHECK_INTERVAL = 0.1f;
    private float _lastProximityCheckTime;

    // 맵 이동 포인트 캐시
    private const float MAP_TRANSFER_RADIUS = 3f;
    private Vector3? _mapEnterPointPos = null;
    private Vector3? _mapLeavePointPos = null;
    private bool _isTransferring = false;

    public override void Init()
    {
        base.Init();
        _cameraTransform = Camera.main.transform;

        _prevRotation = transform.rotation;
        _prevVelocity = Vector3.zero;

        _projectileType = ProjectileType.MagicMissile;
        _attackCoolTimeDict = new Dictionary<AttackType, float>()
           {
                { AttackType.CommonAttack, Stat.CommonAttackCoolTime },
                { AttackType.RangedAttack, Stat.MagicMissileAttackCoolTime }
           };

        // 커서 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _meleeAttackType = AttackType.CommonAttack;
        _rangedAttackType = AttackType.RangedAttack;

        _dropItemLayer = LayerMask.GetMask("DropItem");
        _dropItemPickupRadius = Managers.Config.GetFloat(ConfigType.DropItemPickupRadius);

        CacheMapTransferPoints();

        OnStartGame();
    }

    public void OnStartGame()
    {
        if (Managers.Scene.CurrentScene != Define.Scene.GameRoom)
            return;

        Managers.Input.RegisterMouseAction(
            Define.MouseEvent.LeftClick,
            Managers.GameRoomObject.MyPlayer.OnMeleeAttackInput
        );

        Managers.Input.RegisterKeyAction(
            KeySettings.SpawnProjectile,
            Managers.GameRoomObject.MyPlayer.OnProjectileSpawnInput
        );

        Managers.Input.RegisterKeyAction(
            KeySettings.PickupDropItem,
            TryPickupClosestItem
        );

        Managers.Input.RegisterKeyAction(
            KeySettings.MapTransfer,
            TryRequestMapTransfer
        );

        // TODO - 우선 타이밍 이슈로 어쩔 수 없이 여기서 초기화
        _commonAttackAnimSpeedTime = 2.0f;
        _commonAttackAnimLength = _anim.GetAnimationClipLength(_commonAttackanimName) / _commonAttackAnimSpeedTime;
    }

    private void ProjectileSpawn(AttackType attackType)
    {
        if (Managers.Scene.CurrentScene != Define.Scene.GameRoom)
            return;

        if (CreatureState != CreatureState.Idle)
            return;

        if (Time.time - _lastAttackTime < _attackCoolTimeDict[attackType])
            return;

        _lastAttackTime = Time.time;
        CreatureState = CreatureState.Attack;

        C_SpawnProjectile spawnProjectilePacket = new C_SpawnProjectile();
        spawnProjectilePacket.OwnerId = Id;
        spawnProjectilePacket.ProjectileType = _projectileType;
        Managers.Network.Send(spawnProjectilePacket);

        CoReturnToIdleAfterAttack((int)(_commonAttackAnimLength * 1000)).Forget();
    }

    private void MeleeAttack(AttackType attackType)
    {
        if (CanAttack() == false)
            return;

        _lastAttackTime = Time.time;
        CreatureState = CreatureState.Attack;

        C_Attack attackPacket = new C_Attack();
        attackPacket.InstigatorId = Id;
        attackPacket.AttackType = attackType;
        Managers.Network.Send(attackPacket);

        CoReturnToIdleAfterAttack((int)(_commonAttackAnimLength * 1000)).Forget();
    }

    // Scene, CreatureState, 쿨타임 체크
    private bool CanAttack()
    {
        if (Managers.Scene.CurrentScene != Define.Scene.GameRoom)
            return false;
        if (CreatureState != CreatureState.Idle)
            return false;
        if (Time.time - _lastAttackTime < _attackCoolTimeDict[AttackType.CommonAttack])
            return false;

        return true;
    }

    private void Update()
    {
        base.OnUpdate();
        HandleInput();
        CheckDropItemProximity();
    }

    private void FixedUpdate()
    {
        ApplyMovement();
        CheckMovePacket();
    }

    private void HandleInput()
    {
        if (Managers.Scene.CurrentScene != Define.Scene.GameRoom)
            return;

        if (CreatureState == CreatureState.Die || CreatureState == CreatureState.Attack)
            return;

        _moveDir = Vector3.zero;

        Vector3 f = _cameraTransform.forward;
        Vector3 r = _cameraTransform.right;
        f.y = 0;
        r.y = 0;
        f.Normalize();
        r.Normalize();

        if (Input.GetKey(KeyCode.W)) _moveDir += f;
        if (Input.GetKey(KeyCode.S)) _moveDir -= f;
        if (Input.GetKey(KeyCode.A)) _moveDir -= r;
        if (Input.GetKey(KeyCode.D)) _moveDir += r;

        _moveDir.Normalize();

        if (_moveDir.sqrMagnitude < MOVE_THRESHOLD)
        {
            if (CreatureState != CreatureState.Idle)
            {
                CreatureState = CreatureState.Idle;
                SendMovePacket(Vector3.zero);
            }
            return;
        }

        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(_moveDir), Time.deltaTime * _rotateSpeed);

        CreatureState = CreatureState.Move;
    }

    private void ApplyMovement()
    {
        if (_rb == null)
            return;

        if (_moveDir.sqrMagnitude < MOVE_THRESHOLD)
            return;

        Vector3 newPos = _rb.position + _moveDir * Stat.MoveSpeed * Time.fixedDeltaTime;

        if (Managers.Map.CanGo(newPos.x, newPos.z))
            _rb.MovePosition(newPos);

        Quaternion targetRot = Quaternion.LookRotation(_moveDir);
        _rb.MoveRotation(Quaternion.Lerp(_rb.rotation, targetRot, _rotateSpeed * Time.fixedDeltaTime));

        CreatureState = CreatureState.Move;
    }

    private void CheckMovePacket()
    {
        Quaternion curRot = _rb.rotation;
        Vector3 physicsVelocity = _rb.velocity;

        bool isFalling = physicsVelocity.y < -FALL_SPEED_THRESHOLD;

        if (isFalling)
        {
            // 낙하 중이면 물리 속도 그대로 패킷 전송
            SendMovePacket(physicsVelocity);
            _prevRotation = curRot;
            _prevVelocity = physicsVelocity;
            return;
        }

        Vector3 curVelocity = (_moveDir.sqrMagnitude < MOVE_THRESHOLD) ? Vector3.zero : _moveDir * Stat.MoveSpeed;

        bool rotChanged = Quaternion.Angle(curRot, _prevRotation) > ROT_THRESHOLD;
        bool velChanged = (curVelocity - _prevVelocity).sqrMagnitude > MOVE_THRESHOLD;

        if (rotChanged || velChanged)
        {
            SendMovePacket(curVelocity);
            _prevRotation = curRot;
            _prevVelocity = curVelocity;
        }
    }

    private void SendMovePacket(Vector3 velocity)
    {
        Vector3 pos = _rb.position;
        Quaternion rot = _rb.rotation;

        _movePos.X = pos.x; _movePos.Y = pos.y; _movePos.Z = pos.z;
        _moveVel.X = velocity.x; _moveVel.Y = velocity.y; _moveVel.Z = velocity.z;

        _moveRot.X = rot.x; _moveRot.Y = rot.y; _moveRot.Z = rot.z; _moveRot.W = rot.w;
        _moveState = ObjectState.Clone();
        _moveState.ObjectId = Id;
        _moveState.Name = Name;
        _moveState.ClientSendTime = Util.GetTimestampMs();
        _moveState.Position = _movePos;
        _moveState.Velocity = _moveVel;
        _moveState.Rotation = _moveRot;
        _moveState.CreatureState = CreatureState;
        _moveState.Stat = Stat;

        _movePacket.ObjectState = _moveState;
        Managers.Network.Send(_movePacket);
    }

    protected override void ResetPoolState()
    {
        base.ResetPoolState();
        _moveDir = Vector3.zero;
        _prevVelocity = Vector3.zero;
        _prevRotation = transform.rotation;
        _lastAttackTime = -999f;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    public void SetGameRoomUI()
    {
        // UI 적용
        UI_GameRoom gameRoomUI = Managers.UI.CurrentScene.GetComponent<UI_GameRoom>();
        if (gameRoomUI == null)
            return;

        OnLevelChanged -= gameRoomUI.SetLevel;
        OnLevelChanged += gameRoomUI.SetLevel;

        OnExpChanged -= gameRoomUI.SetExp;
        OnExpChanged += gameRoomUI.SetExp;

        OnHpChanged -= gameRoomUI.SetHp;
        OnHpChanged += gameRoomUI.SetHp;

        OnDetectedDropItem -= gameRoomUI.ShowDropItemTooltip;
        OnDetectedDropItem += gameRoomUI.ShowDropItemTooltip;

        OnLevelChanged?.Invoke(Level);
        OnExpChanged?.Invoke(Exp, Managers.Data.GetMaxExpForLevelUp(Level));
        OnHpChanged?.Invoke(Stat.Hp, Stat.MaxHp);
        OnDetectedDropItem?.Invoke(0, 0); // 초기에는 아이템 없음 상태로 툴팁 숨김
    }

    public override void SetExp(int exp, int maxExp)
    {
        base.SetExp(exp, maxExp);
        OnExpChanged?.Invoke(exp, maxExp); // 등록된 UI 함수 실행
    }

    public override void SetLevel(int level)
    {
        base.SetLevel(level);
        OnLevelChanged?.Invoke(level); // 등록된 UI 함수 실행
    }

    public override void SetHp(int hp, int maxHp)
    {
        base.SetHp(hp, maxHp);
        OnHpChanged?.Invoke(hp, maxHp);
    }

    private void OnMeleeAttackInput()
    {
        MeleeAttack(_meleeAttackType);
    }

    private void OnProjectileSpawnInput()
    {
        ProjectileSpawn(_rangedAttackType);
    }

    private void CheckDropItemProximity()
    {
        if (Time.time - _lastProximityCheckTime < PROXIMITY_CHECK_INTERVAL)
            return;

        _lastProximityCheckTime = Time.time;

        int count = Physics.OverlapSphereNonAlloc(transform.position, _dropItemPickupRadius, _hitResults, _dropItemLayer);

        // 범위 내 가장 가까운 아이템 탐색
        UI_DropItem closest = null;
        float closestDistSqr = float.MaxValue;
        for (int i = 0; i < count; ++i)
        {
            UI_DropItem item = _hitResults[i].GetComponent<UI_DropItem>();
            if (item == null)
                continue;

            float distSqr = (transform.position - _hitResults[i].transform.position).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closest = item;
            }
        }

        // 이전 closest와 달라진 경우에만 업데이트
        if (closest == _closestDropItem)
            return;

        _closestDropItem = closest;

        if (_closestDropItem != null)
        {
            OnDetectedDropItem?.Invoke(_closestDropItem.SpecItemId, _closestDropItem.Count); // 아이템 정보 전달
        }
        else
        {
            OnDetectedDropItem?.Invoke(0, 0); // 아이템 없음 상태 전달
        }
    }

    private void TryPickupClosestItem()
    {
        // 1. 주변 아이템 스캔 (할당 없이 물리 연산)
        int count = Physics.OverlapSphereNonAlloc(transform.position, _dropItemPickupRadius, _hitResults, _dropItemLayer);

        if (count <= 0)
            return;

        UI_DropItem closestDropItem = null;
        float closestDistanceSqr = float.MaxValue; // 제곱 거리로 비교 (성능 최적화)

        for (int i = 0; i < count; i++)
        {
            UI_DropItem item = _hitResults[i].GetComponent<UI_DropItem>();
            if (item == null)
                continue;

            // 2. 거리 계산 (Vector3.Distance 대신 sqrMagnitude 사용 - 루트 연산 제거)
            float distSqr = (transform.position - _hitResults[i].transform.position).sqrMagnitude;

            if (distSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distSqr;
                closestDropItem = item;
            }
        }

        // 3. 가장 가까운 하나만 습득
        if (closestDropItem != null)
        {
            closestDropItem.RequestPickUpDropItem();
        }
    }

    // EnterPoint / LeavePoint 씬 배치 오브젝트 위치 캐시
    private void CacheMapTransferPoints()
    {
        MapEnterPoint enter = UnityEngine.Object.FindObjectOfType<MapEnterPoint>();
        if (enter != null)
            _mapEnterPointPos = enter.transform.position;

        MapLeavePoint leave = UnityEngine.Object.FindObjectOfType<MapLeavePoint>();
        if (leave != null)
            _mapLeavePointPos = leave.transform.position;
    }

    // ` 키 입력 시 근처 이동 포인트에 따라 맵 전환 요청
    private void TryRequestMapTransfer()
    {
        if (_isTransferring)
            return;

        if (Managers.Scene.CurrentScene != Define.Scene.GameRoom)
            return;

        Vector3 myPos = transform.position;

        if (_mapEnterPointPos.HasValue &&
            Vector3.Distance(myPos, _mapEnterPointPos.Value) <= MAP_TRANSFER_RADIUS)
        {
            _isTransferring = true;
            C_RequestMapTransfer packet = new C_RequestMapTransfer
            {
                TransferPoint = MapTransferPoint.MapTransferEnterPoint
            };
            Managers.Network.Send(packet);
            return;
        }

        if (_mapLeavePointPos.HasValue &&
            Vector3.Distance(myPos, _mapLeavePointPos.Value) <= MAP_TRANSFER_RADIUS)
        {
            _isTransferring = true;
            C_RequestMapTransfer packet = new C_RequestMapTransfer
            {
                TransferPoint = MapTransferPoint.MapTransferLeavePoint
            };
            Managers.Network.Send(packet);
        }
    }

    #region Gizmos 코드
    private void OnDrawGizmos()
    {
        if (Stat == null)
            return;
        Color gizmoColor = new Color(1f, 0.3f, 0f, 0.25f);
        Gizmos.color = gizmoColor;

        float range = Stat.AttackRange;
        float halfAngle = Stat.AttackHalfAngleDeg;
        float height = Stat.AttackHeight;

        if (range <= 0f)
            return;

        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;

        DrawCommonAttackCollision(origin, forward, range, halfAngle, height);
    }

    private void DrawCommonAttackCollision(Vector3 origin, Vector3 forward, float radius, float halfAngle, float height)
    {
        int segments = 30;
        float step = halfAngle * 2f / segments;
        Quaternion leftRot = Quaternion.AngleAxis(-halfAngle, Vector3.up);
        Vector3 prev = origin + leftRot * forward * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = -halfAngle + step * i;
            Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 next = origin + rot * forward * radius;
            Gizmos.DrawLine(origin, next);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        Gizmos.DrawLine(origin + Vector3.up * (height * 0.5f), origin - Vector3.up * (height * 0.5f));
    }
    #endregion
}

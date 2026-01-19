using Google.Protobuf.Protocol;
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
    private readonly ObjectState _moveState = new ObjectState();
    private readonly ProtoVector3 _movePos = new ProtoVector3();
    private readonly ProtoVector3 _moveVel = new ProtoVector3();
    private readonly ProtoQuaternion _moveRot = new ProtoQuaternion();

    private Dictionary<AttackType, float> _attackCoolTimeDict;
    private ProjectileType _projectileType = ProjectileType.None;

    private void OnMeleeAttackInput() => MeleeAttack(_meleeAttackType);

    private void OnProjectileSpawnInput() => ProjectileSpawn(_rangedAttackType);

    public override void Init()
    {
        base.Init();
        _cameraTransform = Camera.main.transform;

        _prevRotation = transform.rotation;
        _prevVelocity = Vector3.zero;

        if (Managers.Scene.CurrentScene == Define.Scene.GameRoom)
        {
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
        }
    }

    private void Start()
    {
        Init();
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
            KeyCode.F,
            Managers.GameRoomObject.MyPlayer.OnProjectileSpawnInput
        );

        _commonAttackAnimSpeedTime = 2.0f;
        _commonAttackAnimLength = _anim.GetAnimationClipLength(_commonAttackanimName) / _commonAttackAnimSpeedTime;

        _waitCommonAttackReturn = new WaitForSeconds(_commonAttackAnimLength);
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

        StartCoroutine(CoReturnToIdleAfterAttack(_waitCommonAttackReturn));
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

        StartCoroutine(CoReturnToIdleAfterAttack(_waitCommonAttackReturn));
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

    protected override void OnDestroy()
    {
        base.OnDestroy();
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

using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.Net;

public class ServerSession : PacketSession
{
    // ── 플래그: Program.cs에서 연결 전에 설정 ──────────────────────────
    public static bool EnableMovement = true;   // 랜덤 이동 on/off
    public static bool EnableShooting = true;   // 투사체 발사 on/off
    // ───────────────────────────────────────────────────────────────────

    public int DummyId { get; set; }
    public int ServerId { get; set; }
    public int ChannelId { get; set; }
    public int PlayerId { get; set; }   // 선택된 플레이어 아이디
    public int ObjectId { get; set; }   // 인게임 오브젝트 아이디 (S_EnterGame에서 저장)

    // 현재 위치 (서버 기준 초기값으로 시작, 이후 로컬에서 추적)
    private float _posX;
    private float _posY;
    private float _posZ;

    private readonly Random _random = new Random();
    private Timer _moveTimer;
    private Timer _shootTimer;

    private const int MoveIntervalMs = 500;      // 이동 방향 변경 주기 (ms)
    private const int ShootIntervalMs = 10000;  // 투사체 발사 주기 (ms)
    private const float MoveSpeed = 3f;         // 이동 속도 (유닛/초)

    private volatile bool _isAttacking = false;

    public void StartBehavior(float startX, float startY, float startZ)
    {
        _posX = startX;
        _posY = startY;
        _posZ = startZ;

        if (EnableMovement)
        {
            _moveTimer = new Timer(OnMoveTimer, null, MoveIntervalMs, MoveIntervalMs);
        }

        if (EnableShooting)
        {
            // 첫 발사는 10초 후부터
            _shootTimer = new Timer(OnShootTimer, null, ShootIntervalMs, ShootIntervalMs);
        }
    }

    private void OnMoveTimer(object state)
    {
        if (!EnableMovement)
            return;

        // 공격 애니메이션 중에는 이동 패킷을 보내지 않음
        if (_isAttacking)
            return;

        // XZ 평면에서 무작위 방향
        float angle = (float)(_random.NextDouble() * Math.PI * 2);
        float vx = (float)Math.Cos(angle) * MoveSpeed;
        float vz = (float)Math.Sin(angle) * MoveSpeed;

        // 위치 갱신 (delta = MoveIntervalMs / 1000초)
        float dt = MoveIntervalMs / 1000f;
        _posX += vx * dt;
        _posZ += vz * dt;

        // Y축 회전 쿼터니언 (방향으로 얼굴 향하기)
        float halfAngle = angle / 2f;
        var rotation = new ProtoQuaternion
        {
            X = 0f,
            Y = (float)Math.Sin(halfAngle),
            Z = 0f,
            W = (float)Math.Cos(halfAngle),
        };

        var movePacket = new C_Move
        {
            ObjectState = new ObjectState
            {
                ObjectId = ObjectId,
                Position = new ProtoVector3 { X = _posX, Y = _posY, Z = _posZ },
                Velocity = new ProtoVector3 { X = vx, Y = 0f, Z = vz },
                Rotation = rotation,
                CreatureState = CreatureState.Move,  // 이동 애니메이션 재생
            }
        };

        Send(movePacket);
    }

    private const int AttackAnimDurationMs = 1500; // 공격 애니메이션 지속 시간 (ms)

    private void OnShootTimer(object state)
    {
        if (!EnableShooting)
            return;

        _isAttacking = true;

        // 공격 상태로 전환 (다른 클라이언트에서 공격 애니메이션 재생)
        var attackStatePacket = new C_ChangeCreatureState
        {
            CreatureState = CreatureState.Attack,
        };
        Send(attackStatePacket);

        var projectilePacket = new C_SpawnProjectile
        {
            OwnerId = ObjectId,
            ProjectileType = ProjectileType.MagicMissile,
        };
        Send(projectilePacket);

        // 애니메이션 재생 후 Idle로 복귀
        _ = new Timer(_ =>
        {
            _isAttacking = false;

            var idleStatePacket = new C_ChangeCreatureState
            {
                CreatureState = CreatureState.Idle,
            };
            Send(idleStatePacket);
        }, null, AttackAnimDurationMs, Timeout.Infinite);
    }

    public void Send(IMessage packet)
    {
        string msgName = packet.Descriptor.Name.Replace("_", string.Empty);
        MsgId msgId = (MsgId)Enum.Parse(typeof(MsgId), msgName);
        ushort size = (ushort)packet.CalculateSize();
        byte[] sendBuffer = new byte[size + 4];
        Array.Copy(BitConverter.GetBytes((ushort)(size + 4)), 0, sendBuffer, 0, sizeof(ushort));
        Array.Copy(BitConverter.GetBytes((ushort)msgId), 0, sendBuffer, 2, sizeof(ushort));
        Array.Copy(packet.ToByteArray(), 0, sendBuffer, 4, size);
        Send(new ArraySegment<byte>(sendBuffer));
    }

    public override void OnConnected(EndPoint endPoint)
    {
        Console.WriteLine($"OnConnected : {endPoint}");
    }

    public override void OnDisconnected(EndPoint endPoint)
    {
        _moveTimer?.Dispose();
        _shootTimer?.Dispose();
        Console.WriteLine($"OnDisconnected : {endPoint}, DummyId: {DummyId}");
    }

    public override void OnRecvPacket(ArraySegment<byte> buffer)
    {
        PacketManager.Instance.OnRecvPacket(this, buffer);
    }

    public override void OnSend(int numOfBytes)
    {
        //Console.WriteLine($"Transferred bytes: {numOfBytes}");
    }
}

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

    private const int MoveIntervalMs = 100;       // 이동 패킷 전송 주기 (ms) — 짧을수록 부드러움
    private const int ShootIntervalMs = 10000;  // 투사체 발사 주기 (ms)
    private const float MoveSpeed = 3f;         // 이동 속도 (유닛/초)
    private const int DirChangeTicks = 30;       // 방향 유지 틱 수 (30 * 100ms = 3초)

    private float _currentAngle = 0f;
    private float _vx = 0f;
    private float _vz = 0f;
    private int _dirTicksLeft = 0;               // 현재 방향 유지 남은 틱

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

        // 일정 틱 동안 같은 방향 유지 → 매 틱 랜덤 방향 변경으로 인한 슬라이딩 방지
        if (_dirTicksLeft <= 0)
        {
            _currentAngle = (float)(_random.NextDouble() * Math.PI * 2);
            _vx = (float)Math.Cos(_currentAngle) * MoveSpeed;
            _vz = (float)Math.Sin(_currentAngle) * MoveSpeed;
            _dirTicksLeft = DirChangeTicks;
        }
        _dirTicksLeft--;

        // 위치 갱신 (delta = MoveIntervalMs / 1000초)
        float dt = MoveIntervalMs / 1000f;
        _posX += _vx * dt;
        _posZ += _vz * dt;

        // Y축 회전 쿼터니언 (방향으로 얼굴 향하기)
        // Unity는 +Z가 forward이므로 atan2(vx, vz) 사용 (수학 좌표계 atan2(vz,vx)와 다름)
        float rotAngle = (float)Math.Atan2(_vx, _vz);
        float halfAngle = rotAngle / 2f;
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
                Velocity = new ProtoVector3 { X = _vx, Y = 0f, Z = _vz },
                Rotation = rotation,
                CreatureState = CreatureState.Move,
            }
        };

        Send(movePacket);
    }

    // 서버 보정 위치 반영 (순간이동 방지)
    public void SyncPosition(float x, float y, float z)
    {
        _posX = x;
        _posY = y;
        _posZ = z;
    }

    // velocity=0 패킷을 보내 클라이언트의 위치 예측을 멈춤
    private void SendStopPacket(CreatureState stopState)
    {
        var movePacket = new C_Move
        {
            ObjectState = new ObjectState
            {
                ObjectId = ObjectId,
                Position = new ProtoVector3 { X = _posX, Y = _posY, Z = _posZ },
                Velocity = new ProtoVector3 { X = 0f, Y = 0f, Z = 0f },
                CreatureState = stopState,
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

        // ATTACK 전환 시 velocity=0 전송 → 클라이언트가 위치 예측을 멈춤
        SendStopPacket(CreatureState.Attack);

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
            _dirTicksLeft = 0;  // 다음 이동 틱에 새 방향 선택

            // IDLE 전환 시에도 velocity=0 전송 → 정지 상태 명시
            SendStopPacket(CreatureState.Idle);
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

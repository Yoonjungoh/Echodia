using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseController : MonoBehaviour
{
    // 서버 데이터
    protected float _lerpSpeed = 10f; // 데드 레커닝 보간 속도 조절
    protected Vector3 _serverPosition;
    protected Quaternion _serverRotation;
    protected Vector3 _serverVelocity;
    protected double _serverReceivedTimeMs = 0.0;  // 서버에서 패킷을 보낸 시간
    
    public ObjectState ObjectState { get; set; } = new ObjectState();
    public string Name { get { return ObjectState.Name; } set { ObjectState.Name = value; } }
    public int Id { get { return ObjectState.ObjectId; } set { ObjectState.ObjectId = value; } }
    public CreatureState CreatureState
    {
        get { return ObjectState.CreatureState; }
        set
        {
            if (ObjectState.CreatureState == value)
                return;

            ObjectState.CreatureState = value;
        }
    }
    public GameObjectType GameObjectType { get; set; } = GameObjectType.None;

    protected ProtoVector3 _position = new ProtoVector3();
    public ProtoVector3 Position
    {
        get
        {
            _position.X = transform.position.x;
            _position.Y = transform.position.y;
            _position.Z = transform.position.z;
            return _position;
        }
        set
        {
            _position = value;
            transform.position = new Vector3(_position.X, _position.Y, _position.Z);
        }
    }

    protected ProtoVector3 _velocity = new ProtoVector3();
    public ProtoVector3 Velocity
    {
        get
        {
            _velocity.X = _serverVelocity.x;
            _velocity.Y = _serverVelocity.y;
            _velocity.Z = _serverVelocity.z;
            return _velocity;
        }
        set
        {
            _velocity = value;
            _serverVelocity = new Vector3(_velocity.X, _velocity.Y, _velocity.Z);
        }
    }

    protected ProtoQuaternion _rotation = new ProtoQuaternion();
    public ProtoQuaternion Rotation
    {
        get
        {
            _rotation.X = transform.rotation.x;
            _rotation.Y = transform.rotation.y;
            _rotation.Z = transform.rotation.z;
            _rotation.W = transform.rotation.w;
            return _rotation;
        }
        set
        {
            _rotation = value;
            transform.rotation = new Quaternion(_rotation.X, _rotation.Y, _rotation.Z, _rotation.W);
        }
    }
    public Stat Stat { get { return ObjectState.Stat; } set { ObjectState.Stat = value; } }

    protected virtual void OnUpdate()
    {
        switch (CreatureState)
        {
            case CreatureState.Move:
                UpdateMove();
                break;
            case CreatureState.Idle:
                UpdateIdle();
                break;
            case CreatureState.Attack:
                UpdateAttack();
                break;
        }
    }

    public virtual void Init() { }

    protected virtual void UpdateMove() { }
    protected virtual void UpdateIdle() { }
    protected virtual void UpdateAttack() { }

    public virtual void SetServerState(ProtoVector3 pos, ProtoQuaternion rot, ProtoVector3 vel, long serverReceivedTime)
    {
        _serverPosition = new Vector3(pos.X, pos.Y, pos.Z);
        _serverRotation = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);
        _serverVelocity = new Vector3(vel.X, vel.Y, vel.Z);
        _serverReceivedTimeMs = serverReceivedTime;
    }

    protected virtual void OnDestroy()
    {
        StopAllCoroutines();
    }

    public virtual void OnDamaged(float remainHp) { }

    protected virtual bool IsDead()
    {
        return Stat.Hp <= 0.0f;
    }

    // 게임룸에서 사라지면 호출되는 함수
    public abstract void OnDead();
}

using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreatureController : BaseController
{
    protected Animator _anim;
    protected Rigidbody _rb;
    protected Collider _collider;
    protected string _commonAttackanimName;
    protected UI_HpBar _hpBar;
    protected Vector3 _hpBarPosOffset;

    protected UI_NameBar _nameBar;
    protected Vector3 _nameBarPosOffset;

    protected float _commonAttackAnimLength;
    protected float _commonAttackAnimSpeedTime = 1.0f;
    protected string _commonAttackHitEffectName;
    protected Vector3 _commonAttackHitEffectOffset;

    protected string _dieEffectName;
    protected Vector3 _dieEffectOffset;
    
    protected AttackType _meleeAttackType = AttackType.None;
    protected AttackType _rangedAttackType = AttackType.None;

    protected WaitForSeconds _waitCommonAttackReturn;

    protected override void OnUpdate()
    {
        base.OnUpdate();
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

    public override void Init()
    {
        base.Init();

        _anim = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();

        // 애니메이션 관련 초기화
        _commonAttackanimName = $"Common_Attack";
        _commonAttackAnimLength = _anim.GetAnimationClipLength(_commonAttackanimName) / _commonAttackAnimSpeedTime;
        _waitCommonAttackReturn ??= new WaitForSeconds(_commonAttackAnimLength);

        // 체력바 소환 (투사체는 이후에 조절)
        _hpBar = Managers.UI.MakeWorldSpaceUI<UI_HpBar>(transform, worldPositionStays: false);
        _hpBarPosOffset = Vector3.up * _collider.bounds.size.y;
        _hpBar.SetData(_hpBarPosOffset);
        _hpBar.UpdateHpBar(Stat.Hp, Stat.MaxHp);

        // 이름바 소환
        _nameBar = Managers.UI.MakeWorldSpaceUI<UI_NameBar>(transform, worldPositionStays: false);
        _nameBarPosOffset = Vector3.up * (_collider.bounds.size.y + 0.5f);
        _nameBar.SetData(Name, _nameBarPosOffset);

        // 이펙트 관련 초기화
        _commonAttackHitEffectName = $"{_commonAttackanimName}HitEffect";
        _commonAttackHitEffectOffset = new Vector3(0, _collider.bounds.size.y / 2, 0);

        _dieEffectName = $"{CreatureState.Die}Effect";
        _dieEffectOffset = new Vector3(0, _collider.bounds.size.y / 2, 0);
    }

    protected override void UpdateMove() 
    { 
        base.UpdateMove();
    }

    protected override void UpdateIdle()
    {
        base.UpdateIdle();
    }

    protected override void UpdateAttack()
    {
        base.UpdateAttack();
    }

    protected virtual void UpdateDeadReckoning()
    {
        double serverNowMs = Managers.Network.GetServerNowMs();
        double deltaSec = Mathf.Max(0f, (float)((serverNowMs - _serverReceivedTimeMs) / 1000.0));

        // XZ만 예측
        Vector3 predicted = _serverPosition;
        predicted.x += _serverVelocity.x * (float)deltaSec;
        predicted.z += _serverVelocity.z * (float)deltaSec;
        predicted.y = _serverPosition.y; // Y는 서버 포지션 고정

        transform.position = Vector3.Lerp(transform.position, predicted, Time.deltaTime * _lerpSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, _serverRotation, Time.deltaTime * _lerpSpeed);
    }
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    public override void OnDamaged(float remainHp)
    {
        base.OnDamaged(remainHp);

        // 히트 이펙트
        ParticleSystem particleSystem = Managers.Resource.SpawnEffect(
            _commonAttackHitEffectName,
            _commonAttackHitEffectOffset,
            new Quaternion(0, 0, 0, 0),
            worldPositionStays: false,
            transform).GetComponent<ParticleSystem>();

        float duration = particleSystem.main.duration + particleSystem.main.startLifetime.constantMax;
        Managers.Resource.Destroy(particleSystem.gameObject, duration);

        // 데미지 계산
        float damage = Stat.Hp - remainHp;
        Stat.Hp -= damage;
        
        // 체력바 갱신
        _hpBar.UpdateHpBar(Stat.Hp, Stat.MaxHp);
    }

    protected override bool IsDead()
    {
        return base.IsDead();
    }

    public override void OnDead()
    {
        // 혹시 모르니 체력 0으로 맞춤
        _hpBar.UpdateHpBar(0, Stat.MaxHp);

        // 죽는 이펙트
        ParticleSystem particleSystem = Managers.Resource.SpawnEffect(
            _dieEffectName,
            _dieEffectOffset,
            new Quaternion(0, 0, 0, 0),
            worldPositionStays: false,
            transform).GetComponent<ParticleSystem>();

        float duration = particleSystem.main.duration + particleSystem.main.startLifetime.constantMax;
        Managers.Resource.Destroy(particleSystem.gameObject, duration);

        // 죽은 오브젝트가 방해 안하게 하기
        _collider.isTrigger = true;
        _rb.isKinematic = true;
    }

    public override void SetServerState(ProtoVector3 pos, ProtoQuaternion rot, ProtoVector3 vel, long serverReceivedTime)
    {
        base.SetServerState(pos, rot, vel, serverReceivedTime);
    }

    protected IEnumerator CoReturnToIdleAfterAttack(WaitForSeconds waitAttackReturn)
    {
        if (this == null || _anim == null)
            yield break;

        yield return waitAttackReturn;

        if (this == null || _anim == null)
            yield break;

        CreatureState = CreatureState.Idle;

        // 상태 변화 패킷 전송
        C_ChangeCreatureState changeCreatureStatePacket = new C_ChangeCreatureState();
        changeCreatureStatePacket.CreatureState = CreatureState;
        Managers.Network.Send(changeCreatureStatePacket);
    }
}
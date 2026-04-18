using Cysharp.Threading.Tasks;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class CreatureController : BaseController
{
    protected Animator _anim;
    protected Rigidbody _rb;
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

    protected string _channelingEffectName;
    protected string _channelingEndEffectName;
    protected Vector3 _channelingEffectOffset;
    private ParticleSystem _channelingParticle;

    protected AttackType _meleeAttackType = AttackType.None;
    protected AttackType _rangedAttackType = AttackType.None;

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

        // 체력바 소환 (풀링 재사용 시 중복 생성 방지)
        if (_hpBar == null)
        {
            _hpBar = Managers.UI.MakeWorldSpaceUI<UI_HpBar>(transform, worldPositionStays: false);
        }
        _hpBarPosOffset = Vector3.up * _collider.bounds.size.y;
        _hpBar.SetData(_hpBarPosOffset);
        _hpBar.UpdateHpBar(Stat.Hp, Stat.MaxHp);

        // 이름바 소환 (풀링 재사용 시 중복 생성 방지)
        if (_nameBar == null)
        {
            _nameBar = Managers.UI.MakeWorldSpaceUI<UI_NameBar>(transform, worldPositionStays: false);
        }
        _nameBarPosOffset = Vector3.up * (_collider.bounds.size.y + 0.5f);
        _nameBar.SetData(Name, _nameBarPosOffset);

        // 이펙트 관련 초기화
        _commonAttackHitEffectName = $"{_commonAttackanimName}HitEffect";
        _commonAttackHitEffectOffset = new Vector3(0, _collider.bounds.size.y / 2, 0);

        _dieEffectName = $"{CreatureState.Die}Effect";
        _dieEffectOffset = new Vector3(0, _collider.bounds.size.y / 2, 0);

        _channelingEffectName    = "ChannelingEffect";
        _channelingEndEffectName = "ChannelingEndEffect";
        _channelingEffectOffset  = new Vector3(0, _collider.bounds.size.y / 2, 0);

        ChannelingStarted += HandleChannelingStarted;
        ChannelingEnded   += HandleChannelingEnded;
    }

    // 풀에서 꺼낼 때 상태 초기화 (IPoolable.Reset() → BearController.Reset() 에서 호출)
    protected virtual void ResetPoolState()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        CreatureState = CreatureState.Idle;

        if (_collider != null)
            _collider.isTrigger = false;

        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.velocity = Vector3.zero;
        }
    }

    // ObjectState 세팅 후 GameRoomObjectManager에서 호출 → HP바/이름바 갱신
    public virtual void OnSpawnSetup()
    {
        _hpBar?.UpdateHpBar(Stat.Hp, Stat.MaxHp);
        _nameBar?.SetData(Name, _nameBarPosOffset);
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

    public override void OnDamaged(int remainHp, bool isCritical)
    {
        base.OnDamaged(remainHp, isCritical);

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
        int damage = Stat.Hp - remainHp;
        SetHp(Stat.Hp - damage, Stat.MaxHp);

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

    protected async UniTaskVoid CoReturnToIdleAfterAttack(int waitAttackReturn)
    {
        if (this == null || _anim == null)
            return;

        await UniTask.Delay(waitAttackReturn, cancellationToken: _cts.Token);

        if (this == null || _anim == null)
            return;

        CreatureState = CreatureState.Idle;

        // 상태 변화 패킷 전송
        C_ChangeCreatureState changeCreatureStatePacket = new C_ChangeCreatureState();
        changeCreatureStatePacket.CreatureState = CreatureState;
        Managers.Network.Send(changeCreatureStatePacket);
    }

    public virtual void SetExp(int exp, int maxExp)
    {
        ObjectState.Exp = exp;
        ObjectState.MaxExp = maxExp;
    }

    public virtual void SetLevel(int level)
    {
        ObjectState.Level = level;
    }

    public virtual void SetHp(int hp, int maxHp)
    {
        ObjectState.Stat.Hp = hp;
        ObjectState.Stat.MaxHp = maxHp;
        if (_hpBar != null)
        {
            _hpBar.UpdateHpBar(hp, maxHp);
        }
    }

    // 마나 갱신 — 서버 패킷 수신 시 또는 내부 직접 호출
    public virtual void SetMp(int mp, int maxMp)
    {
        ObjectState.Stat.Mp = mp;
        ObjectState.Stat.MaxMp = maxMp;
    }

    // ── 스킬 애니메이션 ────────────────────────────────────────────────────────

    /// <summary>
    /// 스킬 타입 이름에 해당하는 애니메이션을 재생한다.
    /// returnToIdle=true 이면 클립 길이만큼 대기 후 Idle로 복귀 (Instant 스킬용).
    /// returnToIdle=false 이면 외부에서 복귀를 관리한다 (Channeling 스킬은 OnChannelingEnd에서 처리).
    /// </summary>
    public void PlaySkillAnimation(string animName, bool returnToIdle = true)
    {
        if (string.IsNullOrEmpty(animName))
            return;

        CreatureState = CreatureState.Attack;
        _anim.CrossFade(animName, 0.1f);

        if (returnToIdle)
        {
            float clipLength = _anim.GetAnimationClipLength(animName);
            if (clipLength > 0f)
                CoReturnToIdleAfterAttack((int)(clipLength * 1000)).Forget();
        }
    }

    // ── 채널링 ────────────────────────────────────────────────────────────────

    public event Action<int> ChannelingStarted;
    public event Action<bool> ChannelingEnded;

    public virtual void OnStartChanneling(int castTimeMs)
    {
        CreatureState = CreatureState.Attack;
        ChannelingStarted?.Invoke(castTimeMs);
    }

    public virtual void OnChannelingEnd(bool interrupted)
    {
        CreatureState = CreatureState.Idle;
        ChannelingEnded?.Invoke(interrupted);
    }

    private void HandleChannelingStarted(int castTimeMs)
    {
        GameObject fx = Managers.Resource.SpawnEffect(
            _channelingEffectName,
            _channelingEffectOffset,
            Quaternion.identity,
            worldPositionStays: false,
            transform);

        if (fx == null)
            return;

        _channelingParticle = fx.GetComponent<ParticleSystem>();
    }

    private void HandleChannelingEnded(bool interrupted)
    {
        // 채널링 루프 이펙트 정지
        if (_channelingParticle != null)
        {
            _channelingParticle.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmitting);
            float remaining = _channelingParticle.main.startLifetime.constantMax;
            Managers.Resource.Destroy(_channelingParticle.gameObject, remaining);
            _channelingParticle = null;
        }

        // 채널링 종료 이펙트 (완료/중단 무관하게 재생)
        GameObject endFx = Managers.Resource.SpawnEffect(
            _channelingEndEffectName,
            _channelingEffectOffset,
            Quaternion.identity,
            worldPositionStays: false,
            transform);

        if (endFx != null)
        {
            ParticleSystem endPs = endFx.GetComponent<ParticleSystem>();
            if (endPs != null)
            {
                float duration = endPs.main.duration + endPs.main.startLifetime.constantMax;
                Managers.Resource.Destroy(endFx, duration);
            }
        }
    }
}
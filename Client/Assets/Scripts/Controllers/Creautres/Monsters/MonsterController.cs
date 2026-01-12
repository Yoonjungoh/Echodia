using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterController : CreatureController
{
    [SerializeField] protected float _transitionTime = 0.1f; // 애니메이션 전환 시간
    protected CreatureState _lastAnimState = CreatureState.Idle;
    protected MonsterType MonsterType { get { return ObjectState.MonsterType; } }

    public override void Init()
    {
        base.Init();
    }

    // 매 프레임 CrossFade 호출 방지 용도
    protected override void UpdateIdle()
    {
        base.UpdateIdle();
        if (_lastAnimState != CreatureState.Idle)
        {
            _anim.CrossFade("Idle", _transitionTime);
            _lastAnimState = CreatureState.Idle;
        }
    }

    protected override void UpdateMove()
    {
        base.UpdateMove();
        if (_lastAnimState != CreatureState.Move)
        {
            _anim.CrossFade("Move", _transitionTime);
            _lastAnimState = CreatureState.Move;
        }
    }

    protected override void UpdateAttack()
    {
        base.UpdateAttack();
        if (_lastAnimState != CreatureState.Attack)
        {
            _anim.CrossFade(_commonAttackanimName, _transitionTime);
            StartCoroutine(CoReturnToIdleAfterAttack(_waitCommonAttackReturn));
            _lastAnimState = CreatureState.Attack;
        }
    }

    public override void OnDead()
    {
        base.OnDead();

        _lastAnimState = CreatureState.Die;
        float dieLength = _anim.GetAnimationClipLength("Die");
        _anim.CrossFade("Die", _transitionTime);
        Managers.Resource.Destroy(gameObject, dieLength);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}

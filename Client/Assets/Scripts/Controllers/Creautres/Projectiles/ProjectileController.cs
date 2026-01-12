using Google.Protobuf.Protocol;
using Google.Protobuf.WellKnownTypes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileController : BaseController
{
    public override void Init()
    {
        base.Init();
    }

    protected override void OnUpdate()
    {
        base.OnUpdate();
    }

    protected override void UpdateMove()
    {
        base.UpdateMove();

        // 서버에서 받은 Velocity로 이동
        Vector3 moveVelocity = new Vector3(Velocity.X, Velocity.Y, Velocity.Z);
        transform.position += moveVelocity * Time.deltaTime;
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        int layer = other.gameObject.layer;

        // 1. 무시해야 하는 레이어면 바로 탈출 (가장 빠른 필터)
        if (Managers.GameRoomObject.IsLayerIgnoredByProjectile(layer))
            return;

        // 2. 데미지를 입을 수 없는 레이어면 바로 탈출
        if (!Managers.GameRoomObject.IsDamageable(layer))
        {
            // 데미지 불가 -> 그냥 삭제
            Managers.GameRoomObject.Remove(Id, isDead: false);
            return;
        }

        // 3. 여기까지 왔다는 건 '데미지 가능한 레이어'
        // 이제서야 GetComponent 호출 (최소 비용)
        CreatureController creature = other.gameObject.GetComponent<CreatureController>();
        if (creature == null)
        {
            Managers.GameRoomObject.Remove(Id, isDead: false);
            return;
        }

        // 4. 주인 ID 체크
        int ownerId = Managers.GameRoomObject.GetProjectileOwnerId(Id);
        if (ownerId == -1 || creature.Id == ownerId)
            return;

        // 5. 데미지 요청 전송
        C_Attack attackPacket = new C_Attack()
        {
            AttackType = AttackType.RangedAttack,
            InstigatorId = Id,
            DamagedObjectId = creature.Id
        };

        Managers.Network.Send(attackPacket);
    }


    public override void OnDead()
    {
        Managers.Resource.Destroy(gameObject);
    }
}

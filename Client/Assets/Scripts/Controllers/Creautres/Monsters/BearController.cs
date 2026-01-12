using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BearController : MonsterController
{
    public override void Init()
    {
        base.Init();
        _hpBarPosOffset = Vector3.up * 2 * _collider.bounds.size.y;
        _hpBar.SetData(_hpBarPosOffset);
    }

    void Start()
    {
        Init();
    }

    private void FixedUpdate()
    {
        base.OnUpdate();
        base.UpdateDeadReckoning();
    }
}

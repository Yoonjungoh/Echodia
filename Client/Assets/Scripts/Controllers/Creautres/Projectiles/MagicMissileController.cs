using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MagicMissileController : ProjectileController
{
    public override void Init()
    {
        base.Init();
    }

    void Start()
    {
        Init();
    }
    
    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);
    }

    private void FixedUpdate()
    {
        base.OnUpdate();
    }
}

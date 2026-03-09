using UnityEngine;

public class OtherPlayerController : PlayerController
{
    public override void Init()
    {
        base.Init();

        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }
    }

    // OtherPlayer는 서버가 물리 제어 → 항상 kinematic
    protected override void ResetPoolState()
    {
        base.ResetPoolState();
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }
    }

    private void Start()
    {
        Init();
    }

    private void FixedUpdate()
    {
        base.OnUpdate();
        base.UpdateDeadReckoning();
    }
}

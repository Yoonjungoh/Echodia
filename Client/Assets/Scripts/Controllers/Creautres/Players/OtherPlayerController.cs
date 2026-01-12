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

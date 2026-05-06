using UnityEngine;

public class OtherPlayerController : PlayerController
{
    public int PlayerId { get; set; }
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

    // 파티원 여부에 따라 이름바 색 설정 (PartyManager.RefreshAllNameBarColors에서 호출)
    public void SetPartyNameColor(bool isPartyMember)
    {
        _nameBar?.SetPartyColor(isPartyMember);
    }

    private void Start()
    {
        Init();
        // 스폰 시점에 현재 파티 상태 반영
        bool isPartyMember = Managers.Party.IsPartyMember(PlayerId);
        _nameBar?.SetPartyColor(isPartyMember);
    }

    private void FixedUpdate()
    {
        base.OnUpdate();
        base.UpdateDeadReckoning();
    }
}

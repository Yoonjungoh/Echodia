using Google.Protobuf.Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartyPlayer_SubItem : UI_SubItem<RoomPlayerInfo>
{
    enum Texts
    {
        NameText,
        LevelText,
        JobText,
        PartyStatusText,
    }

    enum Buttons
    {
        InviteButton,
        KickButton,
    }

    protected override void UpdateUI()
    {
        if (_data == null)
            return;

        Get<TextMeshProUGUI>((int)Texts.NameText).text       = _data.Name;
        Get<TextMeshProUGUI>((int)Texts.LevelText).text      = $"Lv.{_data.Level}";
        Get<TextMeshProUGUI>((int)Texts.JobText).text        = GetJobDisplayName(_data.JobType);
        Get<TextMeshProUGUI>((int)Texts.PartyStatusText).text = _data.IsInParty ? "파티 중" : "솔로";

        RefreshButtons();
    }

    public override void SetData(RoomPlayerInfo data)
    {
        base.SetData(data);

        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Button>(typeof(Buttons));

        GetButton((int)Buttons.InviteButton).onClick.AddListener(OnClickInvite);
        GetButton((int)Buttons.KickButton).onClick.AddListener(OnClickKick);

        UpdateUI();
    }

    // 파티 상태 변경 시 외부에서 호출
    public void RefreshButtons()
    {
        if (_data == null)
            return;

        // 초대 버튼: 내가 파티장이고 대상이 솔로일 때만 활성
        bool canInvite = Managers.Party.IsLeader && !_data.IsInParty;
        GetButton((int)Buttons.InviteButton).interactable = canInvite;

        // 추방 버튼: 내가 파티장이고 대상이 내 파티원이며 본인이 아닐 때만 표시
        bool canKick = Managers.Party.IsLeader && _data.IsInMyParty && _data.PlayerId != Managers.GameRoomObject.PlayerId;
        GetButton((int)Buttons.KickButton).gameObject.SetActive(canKick);
    }

    private void OnClickInvite()
    {
        Managers.Network.Send(new C_PartyInvite { TargetPlayerId = _data.PlayerId });
    }

    private void OnClickKick()
    {
        Managers.Network.Send(new C_PartyKick { TargetPlayerId = _data.PlayerId });
    }

    private string GetJobDisplayName(PlayerJobType jobType)
    {
        return jobType switch
        {
            PlayerJobType.Warrior => "전사",
            PlayerJobType.Mage    => "마법사",
            PlayerJobType.Archer  => "궁수",
            PlayerJobType.Thief   => "도적",
            _                     => "없음",
        };
    }

    public override void Init()
    {
        throw new System.NotImplementedException();
    }

}

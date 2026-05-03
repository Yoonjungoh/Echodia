using Google.Protobuf.Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_PartyInviteNotification : UI_Popup
{
    enum Texts
    {
        InviteMessageText,
    }

    enum Buttons
    {
        AcceptButton,
        DeclineButton,
    }

    private int _inviterObjectId;
    private float _remainTime;
    private const float AutoDismissSeconds = 15f;

    public override void Init()
    {
        base.Init();

        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Button>(typeof(Buttons));

        GetButton((int)Buttons.AcceptButton).onClick.AddListener(OnClickAccept);
        GetButton((int)Buttons.DeclineButton).onClick.AddListener(OnClickDecline);
    }

    public void SetData(string inviterName, int inviterObjectId)
    {
        _inviterObjectId = inviterObjectId;
        _remainTime      = AutoDismissSeconds;

        Get<TextMeshProUGUI>((int)Texts.InviteMessageText).text =
            $"{inviterName}님이 파티에 초대하셨습니다.";
    }

    private void Update()
    {
        _remainTime -= UnityEngine.Time.deltaTime;
        if (_remainTime <= 0f)
            CloseSelf();
    }

    private void OnClickAccept()
    {
        Managers.Network.Send(new C_PartyInviteResponse
        {
            Response        = PartyInviteResponseType.Accept,
            InviterObjectId = _inviterObjectId,
        });
        CloseSelf();
    }

    private void OnClickDecline()
    {
        Managers.Network.Send(new C_PartyInviteResponse
        {
            Response        = PartyInviteResponseType.Decline,
            InviterObjectId = _inviterObjectId,
        });
        CloseSelf();
    }

    private void CloseSelf()
    {
        Managers.UI.ClosePopupUI(this);
    }
}

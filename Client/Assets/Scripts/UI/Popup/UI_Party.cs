using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_Party : UI_Popup
{
    enum Buttons
    {
        CloseButton,
        CreatePartyButton,
        LeavePartyButton,
    }

    enum Transforms
    {
        PlayerListContent,
    }

    private Transform _playerListContent;
    private readonly List<PartyPlayer_SubItem> _rows = new List<PartyPlayer_SubItem>();

    public override void Init()
    {
        base.Init();

        Bind<Button>(typeof(Buttons));
        Bind<Transform>(typeof(Transforms));

        _playerListContent = Get<Transform>((int)Transforms.PlayerListContent);

        GetButton((int)Buttons.CloseButton).onClick.AddListener(OnClickClose);
        GetButton((int)Buttons.CreatePartyButton).onClick.AddListener(OnClickCreateParty);
        GetButton((int)Buttons.LeavePartyButton).onClick.AddListener(OnClickLeaveParty);

        Managers.Party.OnPartyChanged += RefreshButtons;
        RefreshButtons();
        RequestRoomPlayerList();
    }

    private void OnDestroy()
    {
        Managers.Party.OnPartyChanged -= RefreshButtons;
    }

    // 파티창이 열릴 때마다 같은 방 플레이어 목록 갱신 요청
    public void RequestRoomPlayerList()
    {
        Managers.Network.Send(new C_RequestRoomPlayerList());
    }

    // S_RequestRoomPlayerListHandler에서 호출
    public void Refresh(RepeatedField<RoomPlayerInfo> playerList)
    {
        // 기존 행 제거
        foreach (PartyPlayer_SubItem row in _rows)
        {
            if (row != null)
                Managers.Resource.Destroy(row.gameObject);
        }
        _rows.Clear();

        foreach (RoomPlayerInfo info in playerList)
        {
            GameObject go = Managers.Resource.Instantiate("UI/SubItem/PartyPlayer_SubItem", _playerListContent);
            PartyPlayer_SubItem row = go.GetComponent<PartyPlayer_SubItem>();
            row.SetData(info);
            _rows.Add(row);
        }

        RefreshButtons();
    }

    // 파티 상태 변경 시 버튼 활성/비활성 갱신
    private void RefreshButtons()
    {
        bool inParty = Managers.Party.IsInParty;
        GetButton((int)Buttons.CreatePartyButton).interactable = !inParty;
        GetButton((int)Buttons.LeavePartyButton).interactable  = inParty;

        // 행 버튼 상태도 갱신
        foreach (PartyPlayer_SubItem row in _rows)
            row?.RefreshButtons();
    }

    private void OnClickClose()
    {
        Managers.UI.CloseSpecificPopup<UI_Party>();
    }

    private void OnClickCreateParty()
    {
        Managers.Network.Send(new C_CreateParty());
    }

    private void OnClickLeaveParty()
    {
        Managers.Network.Send(new C_PartyLeave());
    }
    
    private void OnDisable()
    {
        // 커서 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        // 커서 해금
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Managers.RedDot.Set(RedDotType.Quest, false);
    }
}

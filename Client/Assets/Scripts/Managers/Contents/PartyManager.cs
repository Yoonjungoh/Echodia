using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PartyManager
{
    public int PartyId { get; private set; }
    public bool IsInParty => PartyId != 0;
    public List<PartyMemberInfo> Members { get; private set; } = new List<PartyMemberInfo>();
    public bool IsLeader
    {
        get
        {
            int myId = Managers.GameRoomObject.MyPlayer?.Id ?? 0;
            return Members.Any(m => m.ObjectId == myId && m.IsLeader);
        }
    }

    public event Action OnPartyChanged;

    // ─────────────────────────────────────────
    // 서버 패킷 수신 처리
    // ─────────────────────────────────────────

    public void OnPartyUpdate(S_PartyUpdate packet)
    {
        if (packet.Members.Count == 0)
        {
            PartyId = 0;
            Members.Clear();
        }
        else
        {
            PartyId = packet.PartyId;
            Members = packet.Members.ToList();
        }

        OnPartyChanged?.Invoke();
        RefreshAllNameBarColors();
    }

    public void OnPartyLeft(S_PartyLeft packet)
    {
        PartyId = 0;
        Members.Clear();

        OnPartyChanged?.Invoke();
        RefreshAllNameBarColors();

        switch (packet.Reason)
        {
            case PartyLeftReason.Kicked:
                Managers.UI.ShowToastPopup("파티에서 추방되었습니다.");
                break;
            case PartyLeftReason.Disbanded:
                Managers.UI.ShowToastPopup("파티가 해산되었습니다.");
                break;
        }
    }

    // ─────────────────────────────────────────
    // 이름바 색상 갱신
    // ─────────────────────────────────────────

    public void RefreshAllNameBarColors()
    {
        foreach (GameObject go in Managers.GameRoomObject.Objects.Values)
        {
            if (go == null)
                continue;

            OtherPlayerController opc = go.GetComponent<OtherPlayerController>();
            if (opc == null)
                continue;

            bool isPartyMember = Members.Any(m => m.ObjectId == opc.Id);
            opc.SetPartyNameColor(isPartyMember);
        }
    }

    // 특정 오브젝트 ID가 파티원인지 확인
    public bool IsPartyMember(int objectId)
    {
        return Members.Any(m => m.ObjectId == objectId);
    }
}

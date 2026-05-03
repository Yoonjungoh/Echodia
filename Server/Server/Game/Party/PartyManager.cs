using Google.Protobuf.Protocol;
using Server.Game;
using System.Collections.Generic;

namespace Server
{
    public class PartyManager
    {
        public static PartyManager Instance { get; } = new PartyManager();

        private readonly object _lock = new object();

        // partyId → Party
        private readonly Dictionary<int, Party> _parties = new Dictionary<int, Party>();

        // objectId → partyId
        private readonly Dictionary<int, int> _objectToParty = new Dictionary<int, int>();

        // 대기 중인 초대: targetObjectId → inviterObjectId
        private readonly Dictionary<int, int> _pendingInvites = new Dictionary<int, int>();

        private int _nextPartyId = 1;

        // ─────────────────────────────────────────
        // 조회
        // ─────────────────────────────────────────

        public Party GetPartyOf(int objectId)
        {
            lock (_lock)
            {
                if (_objectToParty.TryGetValue(objectId, out int partyId) &&
                    _parties.TryGetValue(partyId, out Party party))
                    return party;
                return null;
            }
        }

        public bool IsInParty(int objectId)
        {
            lock (_lock)
            {
                return _objectToParty.ContainsKey(objectId);
            }
        }

        public bool AreInSameParty(int a, int b)
        {
            lock (_lock)
            {
                return _objectToParty.TryGetValue(a, out int pid) &&
                       _objectToParty.TryGetValue(b, out int pid2) &&
                       pid == pid2;
            }
        }

        // ─────────────────────────────────────────
        // 파티 생성 (C_CreateParty)
        // ─────────────────────────────────────────

        public bool CreateParty(GameRoom room, Player leader)
        {
            lock (_lock)
            {
                if (_objectToParty.ContainsKey(leader.Id))
                    return false;   // 이미 파티에 속해 있음

                int pid   = _nextPartyId++;
                var party = new Party(pid, leader.Id);
                _parties[pid]              = party;
                _objectToParty[leader.Id]  = pid;
            }

            leader.Session?.Send(new S_CreateParty { Success = true });
            BroadcastPartyUpdateToPlayer(room, leader);
            return true;
        }

        // ─────────────────────────────────────────
        // 초대 (C_PartyInvite)
        // ─────────────────────────────────────────

        public void SendInvite(GameRoom room, Player inviter, int targetObjectId)
        {
            PartyInviteResult result;
            Player target = room.Find(targetObjectId);

            lock (_lock)
            {
                if (target == null)
                {
                    result = PartyInviteResult.TargetNotFound;
                }
                else if (!_objectToParty.TryGetValue(inviter.Id, out int partyId))
                {
                    result = PartyInviteResult.NotInParty;
                }
                else if (_parties[partyId].IsFull())
                {
                    result = PartyInviteResult.PartyFull;
                }
                else if (_objectToParty.ContainsKey(targetObjectId))
                {
                    result = PartyInviteResult.TargetInParty;
                }
                else
                {
                    result = PartyInviteResult.Success;
                    _pendingInvites[targetObjectId] = inviter.Id;
                }
            }

            inviter.Session?.Send(new S_PartyInvite { Result = result });

            if (result == PartyInviteResult.Success)
            {
                target.Session?.Send(new S_PartyInviteNotify
                {
                    InviterObjectId = inviter.Id,
                    InviterName     = inviter.Name,
                });

                // 15초 후 초대 자동 만료
                room.ScheduleDelayedAction(15000, () =>
                {
                    lock (_lock)
                    {
                        if (_pendingInvites.TryGetValue(targetObjectId, out int inv) && inv == inviter.Id)
                            _pendingInvites.Remove(targetObjectId);
                    }
                });
            }
        }

        // ─────────────────────────────────────────
        // 초대 응답 (C_PartyInviteResponse)
        // ─────────────────────────────────────────

        public void HandleInviteResponse(
            GameRoom room,
            Player responder,
            int inviterObjectId,
            PartyInviteResponseType response)
        {
            Player inviter = room.Find(inviterObjectId);
            bool   valid   = false;

            lock (_lock)
            {
                if (_pendingInvites.TryGetValue(responder.Id, out int storedInviterId) &&
                    storedInviterId == inviterObjectId)
                {
                    _pendingInvites.Remove(responder.Id);
                    valid = true;
                }
            }

            if (!valid)
                return;

            if (response == PartyInviteResponseType.Accept && inviter != null)
            {
                bool added = false;
                lock (_lock)
                {
                    if (_objectToParty.TryGetValue(inviterObjectId, out int partyId) &&
                        _parties.TryGetValue(partyId, out Party party) &&
                        !_objectToParty.ContainsKey(responder.Id))
                    {
                        added = party.TryAddMember(responder.Id);
                        if (added)
                            _objectToParty[responder.Id] = partyId;
                    }
                }

                if (added)
                {
                    BroadcastPartyUpdate(room, GetPartyOf(responder.Id));
                }
            }

            // 초대자에게 결과 알림
            inviter?.Session?.Send(new S_PartyInviteResponse
            {
                Response      = response,
                ResponderName = responder.Name,
            });
        }

        // ─────────────────────────────────────────
        // 탈퇴 (C_PartyLeave, 방 이탈 시 자동 호출)
        // ─────────────────────────────────────────

        public void LeaveParty(GameRoom room, int objectId)
        {
            Party party;
            bool  wasLeader;
            bool  partyRemains;

            lock (_lock)
            {
                if (!_objectToParty.TryGetValue(objectId, out int partyId))
                    return;

                _parties.TryGetValue(partyId, out party);
                if (party == null)
                    return;

                wasLeader    = party.IsLeader(objectId);
                partyRemains = party.RemoveMember(objectId);
                _objectToParty.Remove(objectId);

                if (!partyRemains)
                {
                    _parties.Remove(partyId);
                    party = null;
                }
                else if (wasLeader)
                {
                    party.TransferLeadership();
                }
            }

            // 탈퇴자에게 알림
            Player leaver = room.Find(objectId);
            leaver?.Session?.Send(new S_PartyLeft { Reason = PartyLeftReason.SelfLeave });

            if (party != null)
                BroadcastPartyUpdate(room, party);
        }

        // ─────────────────────────────────────────
        // 추방 (C_PartyKick)
        // ─────────────────────────────────────────

        public void KickMember(GameRoom room, int kickerObjectId, int targetObjectId)
        {
            Party party;
            bool  partyRemains;

            lock (_lock)
            {
                if (!_objectToParty.TryGetValue(kickerObjectId, out int partyId))
                    return;

                _parties.TryGetValue(partyId, out party);
                if (party == null || !party.IsLeader(kickerObjectId))
                    return;
                if (!party.Contains(targetObjectId))
                    return;

                partyRemains = party.RemoveMember(targetObjectId);
                _objectToParty.Remove(targetObjectId);

                if (!partyRemains)
                {
                    _parties.Remove(partyId);
                    party = null;
                }
            }

            Player kicked = room.Find(targetObjectId);
            kicked?.Session?.Send(new S_PartyLeft { Reason = PartyLeftReason.Kicked });

            if (party != null)
                BroadcastPartyUpdate(room, party);
        }

        // ─────────────────────────────────────────
        // 내부 헬퍼
        // ─────────────────────────────────────────

        // 파티 전체 멤버에게 최신 스냅샷 전송
        private void BroadcastPartyUpdate(GameRoom room, Party party)
        {
            if (party == null)
                return;

            S_PartyUpdate packet = BuildPartyUpdatePacket(room, party);

            IReadOnlyList<int> members;
            lock (_lock)
            {
                members = new List<int>(party.MemberObjectIds);
            }

            foreach (int oid in members)
            {
                Player p = room.Find(oid);
                p?.Session?.Send(packet);
            }
        }

        // 단일 플레이어에게만 자신의 파티 스냅샷 전송 (파티 생성 직후 등)
        private void BroadcastPartyUpdateToPlayer(GameRoom room, Player player)
        {
            Party party = GetPartyOf(player.Id);
            if (party == null)
                return;

            player.Session?.Send(BuildPartyUpdatePacket(room, party));
        }

        private S_PartyUpdate BuildPartyUpdatePacket(GameRoom room, Party party)
        {
            var packet = new S_PartyUpdate { PartyId = party.PartyId };

            IReadOnlyList<int> members;
            lock (_lock)
            {
                members = new List<int>(party.MemberObjectIds);
            }

            foreach (int oid in members)
            {
                Player p = room.Find(oid);
                if (p == null)
                    continue;

                packet.Members.Add(new PartyMemberInfo
                {
                    ObjectId = p.Id,
                    PlayerId = p.PlayerId,
                    Name     = p.Name,
                    Level    = p.Level,
                    JobType  = p.Stat.JobType,
                    IsLeader = party.IsLeader(p.Id),
                });
            }

            return packet;
        }
    }
}

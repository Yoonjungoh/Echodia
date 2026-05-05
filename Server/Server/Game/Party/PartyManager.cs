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

        // playerId → partyId
        private readonly Dictionary<int, int> _playerIdToParty = new Dictionary<int, int>();

        // 대기 중인 초대: targetPlayerId → inviterPlayerId
        private readonly Dictionary<int, int> _pendingInvites = new Dictionary<int, int>();

        private int _nextPartyId = 1;

        // ─────────────────────────────────────────
        // 조회
        // ─────────────────────────────────────────

        public Party GetPartyOf(int playerId)
        {
            lock (_lock)
            {
                if (_playerIdToParty.TryGetValue(playerId, out int partyId) &&
                    _parties.TryGetValue(partyId, out Party party))
                {
                    return party;
                }
                return null;
            }
        }

        public bool IsInParty(int playerId)
        {
            lock (_lock)
            {
                return _playerIdToParty.ContainsKey(playerId);
            }
        }

        public bool AreInSameParty(int playerIdA, int playerIdB)
        {
            lock (_lock)
            {
                return _playerIdToParty.TryGetValue(playerIdA, out int pid) &&
                       _playerIdToParty.TryGetValue(playerIdB, out int pid2) &&
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
                if (_playerIdToParty.ContainsKey(leader.PlayerId)) { return false; }

                int pid = _nextPartyId++;
                var party = new Party(pid, leader.PlayerId, leader.Name, leader.Level, leader.Stat.JobType);
                _parties[pid] = party;
                _playerIdToParty[leader.PlayerId] = pid;
            }

            leader.Session?.Send(new S_CreateParty { Success = true });
            BroadcastPartyUpdateToPlayer(room, leader);
            return true;
        }

        // ─────────────────────────────────────────
        // 초대 (C_PartyInvite)
        // ─────────────────────────────────────────

        public void SendInvite(GameRoom room, Player inviter, int targetPlayerId)
        {
            PartyInviteResult result;
            Player target = room.FindPlayerByPlayerId(targetPlayerId);

            lock (_lock)
            {
                if (target == null)
                {
                    result = PartyInviteResult.TargetNotFound;
                }
                else if (!_playerIdToParty.TryGetValue(inviter.PlayerId, out int partyId))
                {
                    result = PartyInviteResult.NotInParty;
                }
                else if (_parties[partyId].IsFull())
                {
                    result = PartyInviteResult.PartyFull;
                }
                else if (_playerIdToParty.ContainsKey(targetPlayerId))
                {
                    result = PartyInviteResult.TargetInParty;
                }
                else
                {
                    result = PartyInviteResult.Success;
                    _pendingInvites[targetPlayerId] = inviter.PlayerId;
                }
            }

            inviter.Session?.Send(new S_PartyInvite { Result = result });

            if (result == PartyInviteResult.Success)
            {
                target.Session?.Send(new S_PartyInviteNotify
                {
                    InviterPlayerId = inviter.PlayerId,
                    InviterName = inviter.Name,
                });

                // 15초 후 초대 자동 만료
                room.ScheduleDelayedAction(15000, () =>
                {
                    lock (_lock)
                    {
                        if (_pendingInvites.TryGetValue(targetPlayerId, out int inv) && inv == inviter.PlayerId)
                        {
                            _pendingInvites.Remove(targetPlayerId);
                        }
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
            int inviterPlayerId,
            PartyInviteResponseType response)
        {
            Player inviter = room.FindPlayerByPlayerId(inviterPlayerId);
            bool valid = false;

            lock (_lock)
            {
                if (_pendingInvites.TryGetValue(responder.PlayerId, out int storedInviterId) &&
                    storedInviterId == inviterPlayerId)
                {
                    _pendingInvites.Remove(responder.PlayerId);
                    valid = true;
                }
            }

            if (!valid) { return; }

            if (response == PartyInviteResponseType.Accept && inviter != null)
            {
                bool added = false;
                lock (_lock)
                {
                    if (_playerIdToParty.TryGetValue(inviterPlayerId, out int partyId) &&
                        _parties.TryGetValue(partyId, out Party party) &&
                        !_playerIdToParty.ContainsKey(responder.PlayerId))
                    {
                        added = party.TryAddMember(responder.PlayerId, responder.Name, responder.Level, responder.Stat.JobType);
                        if (added)
                        {
                            _playerIdToParty[responder.PlayerId] = partyId;
                        }
                    }
                }

                if (added)
                {
                    BroadcastPartyUpdate(room, GetPartyOf(responder.PlayerId));
                }
            }

            // 초대자에게 결과 알림
            inviter?.Session?.Send(new S_PartyInviteResponse
            {
                Response = response,
                ResponderName = responder.Name,
            });
        }

        // ─────────────────────────────────────────
        // 탈퇴 (C_PartyLeave, 명시적 요청 시에만 호출)
        // ─────────────────────────────────────────

        public void LeaveParty(GameRoom room, int playerId)
        {
            Party party;
            bool wasLeader;
            bool partyRemains;

            lock (_lock)
            {
                if (!_playerIdToParty.TryGetValue(playerId, out int partyId)) { return; }

                _parties.TryGetValue(partyId, out party);
                if (party == null) { return; }

                wasLeader = party.IsLeader(playerId);
                partyRemains = party.RemoveMember(playerId);
                _playerIdToParty.Remove(playerId);

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
            Player leaver = room.FindPlayerByPlayerId(playerId);
            leaver?.Session?.Send(new S_PartyLeft { Reason = PartyLeftReason.SelfLeave });

            if (party != null)
            {
                BroadcastPartyUpdate(room, party);
            }
        }

        // ─────────────────────────────────────────
        // 추방 (C_PartyKick)
        // ─────────────────────────────────────────

        public void KickMember(GameRoom room, int kickerPlayerId, int targetPlayerId)
        {
            Party party;
            bool partyRemains;

            lock (_lock)
            {
                if (!_playerIdToParty.TryGetValue(kickerPlayerId, out int partyId)) { return; }

                _parties.TryGetValue(partyId, out party);
                if (party == null || !party.IsLeader(kickerPlayerId)) { return; }
                if (!party.Contains(targetPlayerId)) { return; }

                partyRemains = party.RemoveMember(targetPlayerId);
                _playerIdToParty.Remove(targetPlayerId);

                if (!partyRemains)
                {
                    _parties.Remove(partyId);
                    party = null;
                }
            }

            Player kicked = room.FindPlayerByPlayerId(targetPlayerId);
            kicked?.Session?.Send(new S_PartyLeft { Reason = PartyLeftReason.Kicked });

            if (party != null)
            {
                BroadcastPartyUpdate(room, party);
            }
        }

        // ─────────────────────────────────────────
        // 재접속 처리
        // ─────────────────────────────────────────

        // GameRoom.EnterGame 마지막에 호출: 재접속 시 파티 상태 복원
        public void OnPlayerEnterGame(GameRoom room, Player player)
        {
            int playerId = player.PlayerId;
            lock (_lock)
            {
                if (!_playerIdToParty.ContainsKey(playerId)) { return; }
                if (_playerIdToParty.TryGetValue(playerId, out int pid) &&
                    _parties.TryGetValue(pid, out Party party))
                {
                    party.UpdateMemberCache(playerId, player.Name, player.Level, player.Stat.JobType);
                }
            }

            Party myParty = GetPartyOf(playerId);
            if (myParty == null) { return; }

            // 자신에게 현재 파티 상태 전송
            player.Session?.Send(BuildPartyUpdatePacket(room, myParty));
            // 온라인 파티원에게도 재접속 알림
            BroadcastPartyUpdate(room, myParty);
        }

        // ─────────────────────────────────────────
        // 내부 헬퍼
        // ─────────────────────────────────────────

        // 파티 전체 멤버에게 최신 스냅샷 전송
        private void BroadcastPartyUpdate(GameRoom room, Party party)
        {
            if (party == null) { return; }

            S_PartyUpdate packet = BuildPartyUpdatePacket(room, party);

            IReadOnlyList<Party.PartyMember> members;
            lock (_lock)
            {
                members = new List<Party.PartyMember>(party.Members);
            }

            foreach (Party.PartyMember m in members)
            {
                Player p = room.FindPlayerByPlayerId(m.PlayerId);
                p?.Session?.Send(packet);
            }
        }

        // 단일 플레이어에게만 자신의 파티 스냅샷 전송 (파티 생성 직후 등)
        private void BroadcastPartyUpdateToPlayer(GameRoom room, Player player)
        {
            Party party = GetPartyOf(player.PlayerId);
            if (party == null) { return; }
            player.Session?.Send(BuildPartyUpdatePacket(room, party));
        }

        private S_PartyUpdate BuildPartyUpdatePacket(GameRoom room, Party party)
        {
            var packet = new S_PartyUpdate { PartyId = party.PartyId };

            IReadOnlyList<Party.PartyMember> members;
            lock (_lock)
            {
                members = new List<Party.PartyMember>(party.Members);
            }

            foreach (Party.PartyMember m in members)
            {
                Player p = room.FindPlayerByPlayerId(m.PlayerId);
                bool online = p != null;
                packet.Members.Add(new PartyMemberInfo
                {
                    PlayerId = m.PlayerId,
                    Name = online ? p.Name : m.Name,
                    Level = online ? p.Level : m.Level,
                    JobType = online ? p.Stat.JobType : m.JobType,
                    IsLeader = party.IsLeader(m.PlayerId),
                    IsOnline = online,
                });
            }

            return packet;
        }
    }
}

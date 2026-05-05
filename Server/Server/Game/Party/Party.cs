using Google.Protobuf.Protocol;
using System.Collections.Generic;
using System.Linq;

namespace Server.Game
{
    public class Party
    {
        // 멤버 캐시: 오프라인 멤버 정보 보존용
        public class PartyMember
        {
            public int PlayerId;
            public string Name;
            public int Level;
            public PlayerJobType JobType;
        }

        public const int MaxMembers = 3;

        public int PartyId { get; }
        public int LeaderPlayerId { get; private set; }

        private readonly List<PartyMember> _members = new List<PartyMember>();
        public IReadOnlyList<PartyMember> Members => _members;

        public Party(int partyId, int leaderPlayerId, string name, int level, PlayerJobType jobType)
        {
            PartyId = partyId;
            LeaderPlayerId = leaderPlayerId;
            _members.Add(new PartyMember { PlayerId = leaderPlayerId, Name = name, Level = level, JobType = jobType });
        }

        public bool TryAddMember(int playerId, string name, int level, PlayerJobType jobType)
        {
            if (_members.Count >= MaxMembers) { return false; }
            if (_members.Any(m => m.PlayerId == playerId)) { return false; }
            _members.Add(new PartyMember { PlayerId = playerId, Name = name, Level = level, JobType = jobType });
            return true;
        }

        // 멤버 제거. 파티에 멤버가 남아 있으면 true 반환
        public bool RemoveMember(int playerId)
        {
            _members.RemoveAll(m => m.PlayerId == playerId);
            return _members.Count > 0;
        }

        // 리더가 제거된 후 첫 번째 남은 멤버를 리더로 승계
        public void TransferLeadership()
        {
            if (_members.Count > 0)
            {
                LeaderPlayerId = _members[0].PlayerId;
            }
        }

        // 재접속 시 오프라인 캐시 갱신
        public void UpdateMemberCache(int playerId, string name, int level, PlayerJobType jobType)
        {
            PartyMember m = _members.FirstOrDefault(x => x.PlayerId == playerId);
            if (m == null) { return; }
            m.Name = name;
            m.Level = level;
            m.JobType = jobType;
        }

        public bool IsLeader(int playerId) => playerId == LeaderPlayerId;
        public bool IsFull() => _members.Count >= MaxMembers;
        public bool Contains(int playerId) => _members.Any(m => m.PlayerId == playerId);
    }
}

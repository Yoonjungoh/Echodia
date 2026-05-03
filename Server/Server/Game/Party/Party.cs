using System.Collections.Generic;

namespace Server.Game
{
    public class Party
    {
        public const int MaxMembers = 5;

        public int PartyId         { get; }
        public int LeaderObjectId  { get; private set; }

        private readonly List<int> _memberObjectIds = new List<int>();
        public IReadOnlyList<int> MemberObjectIds => _memberObjectIds;

        public Party(int partyId, int leaderObjectId)
        {
            PartyId        = partyId;
            LeaderObjectId = leaderObjectId;
            _memberObjectIds.Add(leaderObjectId);
        }

        public bool TryAddMember(int objectId)
        {
            if (_memberObjectIds.Count >= MaxMembers)
                return false;
            if (_memberObjectIds.Contains(objectId))
                return false;
            _memberObjectIds.Add(objectId);
            return true;
        }

        // 멤버 제거. 파티에 멤버가 남아 있으면 true 반환
        public bool RemoveMember(int objectId)
        {
            _memberObjectIds.Remove(objectId);
            return _memberObjectIds.Count > 0;
        }

        // 리더가 제거된 후 첫 번째 남은 멤버를 리더로 승계
        public void TransferLeadership()
        {
            if (_memberObjectIds.Count > 0)
                LeaderObjectId = _memberObjectIds[0];
        }

        public bool IsLeader(int objectId)  => objectId == LeaderObjectId;
        public bool IsFull()                => _memberObjectIds.Count >= MaxMembers;
        public bool Contains(int objectId)  => _memberObjectIds.Contains(objectId);
    }
}

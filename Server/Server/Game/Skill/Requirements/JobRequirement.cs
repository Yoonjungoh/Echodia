using Google.Protobuf.Protocol;
using System.Collections.Generic;

namespace Server.Game.Skill.Requirements
{
    public class JobRequirement : ISkillRequirement
    {
        private readonly List<PlayerJobType> _allowedJobs;

        public JobRequirement(List<PlayerJobType> allowedJobs)
        {
            _allowedJobs = allowedJobs;
        }

        public bool Check(Player player)
        {
            // 허용 직업 목록이 비어 있으면 모든 직업 가능
            return _allowedJobs.Count == 0 || _allowedJobs.Contains(player.Stat.JobType);
        }

        public void Consume(Player player) { }
    }
}

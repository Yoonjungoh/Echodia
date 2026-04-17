using Google.Protobuf.Protocol;

namespace Server.Game.Skill.Requirements
{
    public class LevelRequirement : ISkillRequirement
    {
        private readonly int _requiredLevel;

        public LevelRequirement(int requiredLevel)
        {
            _requiredLevel = requiredLevel;
        }

        public bool Check(Player player)
        {
            return player.CurrencyTracker.Get(CurrencyType.Level) >= _requiredLevel;
        }

        public void Consume(Player player) { }
    }
}

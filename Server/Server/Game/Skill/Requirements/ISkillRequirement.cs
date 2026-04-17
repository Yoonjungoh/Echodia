namespace Server.Game.Skill.Requirements
{
    public interface ISkillRequirement
    {
        /// <summary>조건을 충족하는지 검사. 충족하면 true.</summary>
        bool Check(Player player);

        /// <summary>조건에 해당하는 자원을 소비. Check() 통과 후에만 호출.</summary>
        void Consume(Player player);
    }
}

namespace Server.Game.Skill
{
    public enum SkillState
    {
        Ready,    // 사용 가능
        Casting,  // 시전 중 (선딜 / 모으기)
        Active,   // 효과 발동 중 (채널링)
        Cooldown, // 쿨타임 중
    }
}

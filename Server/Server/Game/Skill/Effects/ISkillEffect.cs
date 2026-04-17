using System.Collections.Generic;

namespace Server.Game.Skill.Effects
{
    public interface ISkillEffect
    {
        /// <summary>
        /// 스킬 효과를 실행한다.
        /// </summary>
        /// <param name="caster">시전자</param>
        /// <param name="targets">SkillTargetSelector가 계산한 대상 목록</param>
        void Execute(Player caster, List<GameObject> targets);
    }
}

using Google.Protobuf.Protocol;
using System.Collections.Generic;

namespace Server.Game.Skill.Effects
{
    public interface ISkillEffect
    {
        // chargeMultiplier: Instant = 1.0f, Channeling = completedTicks
        IReadOnlyList<DamagedInfo> Execute(Player caster, List<GameObject> targets, float chargeMultiplier = 1.0f);
    }
}

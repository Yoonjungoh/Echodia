
using System;
using System.Collections.Generic;

public class ItemCooldownTracker
{
    private Dictionary<int, DateTime> cooldowns = new Dictionary<int, DateTime>();

    public bool IsOnCooldown(int itemId)
    {
        if (cooldowns.TryGetValue(itemId, out DateTime cooldownEnd))
        {
            return DateTime.Now < cooldownEnd;
        }
        return false;
    }

    public void StartCooldown(int itemId, TimeSpan cooldownDuration)
    {
        cooldowns[itemId] = DateTime.Now.Add(cooldownDuration);
    }

    public TimeSpan GetRemainingCooldown(int itemId)
    {
        if (cooldowns.TryGetValue(itemId, out DateTime cooldownEnd))
        {
            return cooldownEnd - DateTime.Now;
        }
        return TimeSpan.Zero;
    }
}
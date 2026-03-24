
using System;
using System.Collections.Generic;
using Server.Game;

public class CooldownTracker
{
    private Player _owner;
    private Dictionary<int, DateTime> _cooldowns = new Dictionary<int, DateTime>();

    public CooldownTracker(Player owner)
    {
        _owner = owner;
    }

    // TODO - 로그인 시 DB에서 쿨다운 정보 로드 (현재는 DB 연동 안 함)
    public void Load()
    {
        
    }

    public bool IsOnCooldown(int itemId)
    {
        if (_cooldowns.TryGetValue(itemId, out DateTime cooldownEnd))
        {
            return DateTime.Now < cooldownEnd;
        }
        return false;
    }

    public void StartCooldown(int itemId, TimeSpan cooldownDuration)
    {
        _cooldowns[itemId] = DateTime.Now.Add(cooldownDuration);
    }

    public TimeSpan GetRemainingCooldown(int itemId)
    {
        if (_cooldowns.TryGetValue(itemId, out DateTime cooldownEnd))
        {
            return cooldownEnd - DateTime.Now;
        }
        return TimeSpan.Zero;
    }
}
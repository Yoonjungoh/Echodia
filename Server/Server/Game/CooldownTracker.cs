
using System;
using System.Collections.Generic;
using Server.Game;

public class CooldownTracker
{
    private Player _owner;
    private Dictionary<int, long> _cooldowns = new Dictionary<int, long>();

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
        if (_cooldowns.TryGetValue(itemId, out long cooldownEnd))
        {
            return Environment.TickCount64 < cooldownEnd;
        }
        return false;
    }

    public void StartCooldown(int itemId, float cooldownSeconds)
    {
        _cooldowns[itemId] = Environment.TickCount64 + (long)(cooldownSeconds * 1000);
    }

    public long GetRemainingCooldownMs(int itemId)
    {
        if (_cooldowns.TryGetValue(itemId, out long cooldownEnd))
        {
            return Math.Max(0, cooldownEnd - Environment.TickCount64);
        }
        return 0;
    }
}
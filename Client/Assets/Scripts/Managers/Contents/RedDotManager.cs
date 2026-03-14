using System;
using System.Collections.Generic;

public enum RedDotType
{
    Quest,
}

public class RedDotManager
{
    private HashSet<RedDotType> _activeRedDots = new HashSet<RedDotType>();

    // 레드닷 상태가 바뀔 때마다 발행 (type, isActive)
    public Action<RedDotType, bool> OnRedDotChanged;

    public void Set(RedDotType type, bool isActive)
    {
        bool changed = isActive
            ? _activeRedDots.Add(type)
            : _activeRedDots.Remove(type);

        if (changed)
        {
            OnRedDotChanged?.Invoke(type, isActive);
        }
    }

    public bool IsActive(RedDotType type)
    {
        return _activeRedDots.Contains(type);
    }
}

using Google.Protobuf.Protocol;
using Server.Game;
using Server.Game.Object;
using System;
using System.Collections.Generic;

public static class MonsterFactory
{
    private static readonly Dictionary<MonsterType, Func<Monster>> _factory
        = new Dictionary<MonsterType, Func<Monster>>
    {
        { MonsterType.Bear, () => ObjectManager.Instance.Add<Bear>() },
    };

    public static Monster Create(MonsterType type)
    {
        if (_factory.TryGetValue(type, out var creator))
            return creator();

        // 예외 처리
        throw new Exception($"Unknown MonsterType {type}");
    }
}

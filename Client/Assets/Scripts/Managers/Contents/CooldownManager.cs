
using System;
using System.Collections.Generic;
using Google.Protobuf.Protocol;
using UnityEngine;
using UnityEngine.Diagnostics;

public class CooldownManager
{
    private Dictionary<int, float> _cooldowns = new();

    public void StartCooldown(int itemId)
    {
        ItemType itemType = Util.GetItemType(itemId);
        float cooldownDuration = 0f;
        switch (itemType)
        {
            case ItemType.Consumable:
                cooldownDuration = Managers.SpecData.GetConsumable(itemId).CoolTime;
                break;
            default:
                return;
        }
        _cooldowns[itemId] = Time.time + cooldownDuration;
    }

    /// <summary>스킬처럼 SpecData 타입 분기 없이 쿨타임을 직접 지정할 때 사용한다.</summary>
    public void StartCooldown(int id, float duration)
    {
        if (duration <= 0f)
            return;

        _cooldowns[id] = Time.time + duration;
    }

    public float GetRemainingCooldownSeconds(int itemId)
    {
        if (_cooldowns.TryGetValue(itemId, out float endTime))
        {
            return Math.Max(0, endTime - Time.time);
        }
        return 0f;
    }

    public bool IsOnCooldown(int itemId)
    {
        if (_cooldowns.TryGetValue(itemId, out float endTime))
        {
            return Time.time < endTime;
        }
        return false;
    }

    public void RequestUseItem(int slotIndex, ItemType itemType)
    {
        C_UseItem useItemPacket = new C_UseItem 
        { 
            SlotIndex = slotIndex,
            ItemType = itemType,
        };
        Managers.Network.Send(useItemPacket);
    }

}
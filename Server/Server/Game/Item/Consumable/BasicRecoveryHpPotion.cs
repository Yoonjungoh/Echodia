
using System;
using Google.Protobuf.Protocol;
using Server.Data;
using Server.Game;

public class BasicRecoveryHpPotion : Consumable
{
    public ConsumableType ConsumableType { get; set; } = ConsumableType.BasicRecoveryHpPotion;
    private ConsumableMetaData Data;

    public override bool CanUse(Player player, int slotIndex, bool needSendPacket = false)
    {
        if (Data == null)
        {
            Data = SpecDataManager.Instance.GetConsumable((int)ConsumableType);
        }

        // 레벨 요구사항 확인
        bool isLevelRequirementMet = player.Level >= Data.RequiredLevel;
        if (isLevelRequirementMet == false && needSendPacket)
        {
            S_UseItem useItemPacket = new S_UseItem
            {
                SlotIndex = slotIndex,
                UseItemResult = UseItemResult.NotEnoughLevel
            };
            player.Session?.Send(useItemPacket);
            return false;
        }
        
        if (isLevelRequirementMet == false)
            return false;

        // 쿨타임이 끝났는지 확인
        bool isOnCooldown = player.CooldownTracker.IsOnCooldown((int)ConsumableType) == false;
        if (isOnCooldown == false && needSendPacket)
        {
            S_UseItem useItemPacket = new S_UseItem
            {
                SlotIndex = slotIndex,
                UseItemResult = UseItemResult.Cooldown
            };
            player.Session?.Send(useItemPacket);
            return false;
        }

        if (isOnCooldown == false)
            return false;

        return true;
    }

    public override void Use(Player player, int slotIndex)
    {
        if (Data == null)
        {
            Data = SpecDataManager.Instance.GetConsumable((int)ConsumableType);
        }
        int healAmount = Data.EffectValue;
        // HP 회복
        player.HealHp(healAmount);
    }
}
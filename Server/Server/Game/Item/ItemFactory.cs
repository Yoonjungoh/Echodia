using Google.Protobuf.Protocol;
using Server.Data;

public static class ItemFactory
{
    public static Item Create(int itemId)
    {
        ItemMetaData meta = SpecDataManager.Instance.GetItem(itemId);
        switch (meta.ItemType)
        {
            case ItemType.Consumable:
                ConsumableMetaData consumableMeta = SpecDataManager.Instance.GetConsumable(itemId);
                return CreateConsumable(consumableMeta);
            case ItemType.Equipment:
                EquipmentMetaData equipmentMeta = SpecDataManager.Instance.GetEquipment(itemId);
                return CreateEquipment(equipmentMeta);
            default:
                return new Item();
        }
    }

    static Consumable CreateConsumable(ConsumableMetaData meta)
    {
        switch (meta.ConsumableType)
        {
            case ConsumableType.BasicRecoveryHpPotion: return new BasicRecoveryHpPotion();
            // 이후 타입 추가
            default: return new Consumable();
        }
    }

    static Equipment CreateEquipment(EquipmentMetaData meta)
    {
        switch (meta.EquipmentType)
        {
            // case EquipmentType.BasicSword: return new BasicSword();
            // 이후 타입 추가
            default: return new Equipment();
        }
    }


}
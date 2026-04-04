using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;

public class InventoryManager
{

    // ItemType -> (slotIndex -> ItemInfo) : 카테고리별로 슬롯 인덱스가 독립적으로 관리됨
    private Dictionary<ItemType, Dictionary<int, ItemInfo>> _items = new Dictionary<ItemType, Dictionary<int, ItemInfo>>
    {
        { ItemType.Equipment, new Dictionary<int, ItemInfo>() },
        { ItemType.Consumable, new Dictionary<int, ItemInfo>() },
        { ItemType.Misc, new Dictionary<int, ItemInfo>() },
    };

    public event Action OnInventoryChanged;

    public IReadOnlyDictionary<int, ItemInfo> GetItems(ItemType itemType)
    {
        if (_items.TryGetValue(itemType, out var dict))
            return dict;

        return new Dictionary<int, ItemInfo>();
    }

    public void UpdateItemData(ItemInfo itemInfo)
    {
        ItemType type = GetItemType(itemInfo);
        if (type == ItemType.None)
            return;

        if (itemInfo.Count <= 0)
        {
            _items[type].Remove(itemInfo.SlotIndex);
            OnInventoryChanged?.Invoke();
            return;
        }
        
        _items[type][itemInfo.SlotIndex] = itemInfo;
        Managers.RedDot.Set(RedDotType.Inventory, true);
        OnInventoryChanged?.Invoke();
    }

    public void UpdateItemDataAll(RepeatedField<ItemInfo> itemInfoList)
    {
        foreach (var dict in _items.Values)
            dict.Clear();

        foreach (ItemInfo itemInfo in itemInfoList)
        {
            ItemType type = GetItemType(itemInfo);
            if (type == ItemType.None)
                continue;

            _items[type][itemInfo.SlotIndex] = itemInfo;
        }

        OnInventoryChanged?.Invoke();
    }

    // EquipmentSlotType별로 현재 장착된 아이템을 반환 (없으면 null)
    public ItemInfo GetEquippedItem(EquipmentSlotType slotType)
    {
        if (!_items.TryGetValue(ItemType.Equipment, out var equipDict))
            return null;

        foreach (ItemInfo itemInfo in equipDict.Values)
        {
            if (!itemInfo.IsEquipped)
                continue;

            EquipmentMetaData meta = Managers.SpecData.GetEquipment(itemInfo.ItemId);
            if (meta != null && meta.EquipmentSlotType == slotType)
                return itemInfo;
        }
        return null;
    }

    public void Clear()
    {
        foreach (var dict in _items.Values)
            dict.Clear();
    }

    private ItemType GetItemType(ItemInfo itemInfo)
    {
        ItemMetaData metaData = Managers.SpecData.GetItem(itemInfo.ItemId);
        return metaData?.ItemType ?? ItemType.None;
    }
}

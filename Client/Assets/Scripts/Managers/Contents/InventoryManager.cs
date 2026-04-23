using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;

public class InventoryManager
{
    // ItemType -> (slotIndex -> ItemInfo) : 장착되지 않은 아이템만 관리
    private Dictionary<ItemType, Dictionary<int, ItemInfo>> _items = new Dictionary<ItemType, Dictionary<int, ItemInfo>>
    {
        { ItemType.Equipment, new Dictionary<int, ItemInfo>() },
        { ItemType.Consumable, new Dictionary<int, ItemInfo>() },
        { ItemType.Misc, new Dictionary<int, ItemInfo>() },
    };

    // EquipmentSlotType -> ItemInfo : 장착 중인 장비 전용
    private Dictionary<EquipmentSlotType, ItemInfo> _equippedItems = new Dictionary<EquipmentSlotType, ItemInfo>();

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

        if (itemInfo.Count <= 0)
        {
            if (type != ItemType.None)
                _items[type].Remove(itemInfo.SlotIndex);
            OnInventoryChanged?.Invoke();
            return;
        }

        if (type == ItemType.None)
            return;

        if (itemInfo.IsEquipped)
        {
            _items[type].Remove(itemInfo.SlotIndex);
            EquipmentMetaData meta = Managers.SpecData.GetEquipment(itemInfo.ItemId);
            if (meta != null)
                _equippedItems[meta.EquipmentSlotType] = itemInfo;
        }
        else
        {
            // 해제된 아이템: _equippedItems에서 제거 후 인벤에 추가
            var toRemove = _equippedItems.FirstOrDefault(kv => kv.Value.ItemId == itemInfo.ItemId);
            if (toRemove.Value != null)
                _equippedItems.Remove(toRemove.Key);

            _items[type][itemInfo.SlotIndex] = itemInfo;
            Managers.RedDot.Set(RedDotType.Inventory, true);
        }

        OnInventoryChanged?.Invoke();
    }

    public void UpdateItemDataAll(RepeatedField<ItemInfo> itemInfoList)
    {
        foreach (var dict in _items.Values)
            dict.Clear();
        _equippedItems.Clear();

        foreach (ItemInfo itemInfo in itemInfoList)
        {
            if (itemInfo.IsEquipped)
            {
                EquipmentMetaData meta = Managers.SpecData.GetEquipment(itemInfo.ItemId);
                if (meta != null)
                    _equippedItems[meta.EquipmentSlotType] = itemInfo;
            }
            else
            {
                ItemType type = GetItemType(itemInfo);
                if (type == ItemType.None)
                    continue;
                _items[type][itemInfo.SlotIndex] = itemInfo;
            }
        }

        OnInventoryChanged?.Invoke();
    }

    // EquipmentSlotType별로 현재 장착된 아이템을 반환 (없으면 null)
    public ItemInfo GetEquippedItem(EquipmentSlotType slotType)
    {
        _equippedItems.TryGetValue(slotType, out ItemInfo item);
        return item;
    }

    public void Clear()
    {
        foreach (var dict in _items.Values)
            dict.Clear();
        _equippedItems.Clear();
    }

    private ItemType GetItemType(ItemInfo itemInfo)
    {
        ItemMetaData metaData = Managers.SpecData.GetItem(itemInfo.ItemId);
        return metaData?.ItemType ?? ItemType.None;
    }
}

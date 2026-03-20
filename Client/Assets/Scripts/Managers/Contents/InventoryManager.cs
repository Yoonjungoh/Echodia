using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;

public class InventoryManager
{
    // slotIndex → ItemInfo
    private Dictionary<int, ItemInfo> _items = new Dictionary<int, ItemInfo>();
    public IReadOnlyDictionary<int, ItemInfo> Items { get { return _items; } }

    public event Action OnInventoryChanged;

    public void UpdateItemData(ItemInfo itemInfo)
    {
        _items[itemInfo.SlotIndex] = itemInfo;
        OnInventoryChanged?.Invoke();
    }

    public void UpdateItemDataAll(RepeatedField<ItemInfo> itemInfoList)
    {
        _items.Clear();
        foreach (ItemInfo itemInfo in itemInfoList)
        {
            _items[itemInfo.SlotIndex] = itemInfo;
        }
        OnInventoryChanged?.Invoke();
    }

    public void Clear()
    {
        _items.Clear();
    }
}

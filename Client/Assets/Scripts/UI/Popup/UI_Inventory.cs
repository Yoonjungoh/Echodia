using Google.Protobuf.Protocol;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Inventory : UI_Popup
{
    enum Buttons
    {
        CloseButton,
        BackgroundButton,
        EquipmentTabButton,
        ConsumableTabButton,
        MiscTabButton,
    }

    enum Transforms
    {
        ItemContent,
    }

    enum GameObjects
    {
        EquipmentTabHighlight,
        ConsumableTabHighlight,
        MiscTabHighlight,
    }

    private Transform _itemContent;

    private ItemType _currentTab = ItemType.Equipment;

    public override void Init()
    {
        base.Init();

        Bind<Button>(typeof(Buttons));
        Bind<Transform>(typeof(Transforms));
        Bind<GameObject>(typeof(GameObjects));

        _itemContent = Get<Transform>((int)Transforms.ItemContent);

        GetButton((int)Buttons.CloseButton).onClick.AddListener(OnClickCloseButton);
        GetButton((int)Buttons.BackgroundButton).onClick.AddListener(OnClickCloseButton);
        GetButton((int)Buttons.EquipmentTabButton).onClick.AddListener(() => OnClickTab(ItemType.Equipment));
        GetButton((int)Buttons.ConsumableTabButton).onClick.AddListener(() => OnClickTab(ItemType.Consumable));
        GetButton((int)Buttons.MiscTabButton).onClick.AddListener(() => OnClickTab(ItemType.Misc));

        Managers.Inventory.OnInventoryChanged -= UpdateUI;
        Managers.Inventory.OnInventoryChanged += UpdateUI;

        UpdateUI();
    }

    private void OnDestroy()
    {
        Managers.Inventory.OnInventoryChanged -= UpdateUI;
    }

    private void OnClickTab(ItemType tabType)
    {
        _currentTab = tabType;
        UpdateTabHighlight();
        RefreshItemList();
    }

    private void UpdateTabHighlight()
    {
        Get<GameObject>((int)GameObjects.EquipmentTabHighlight).SetActive(_currentTab == ItemType.Equipment);
        Get<GameObject>((int)GameObjects.ConsumableTabHighlight).SetActive(_currentTab == ItemType.Consumable);
        Get<GameObject>((int)GameObjects.MiscTabHighlight).SetActive(_currentTab == ItemType.Misc);
    }

    private void UpdateUI()
    {
        UpdateTabHighlight();
        RefreshItemList();
    }

    private void RefreshItemList()
    {
        foreach (Transform child in _itemContent)
        {
            Managers.Resource.Destroy(child.gameObject);
        }

        foreach (var kvp in Managers.Inventory.Items)
        {
            ItemInfo itemInfo = kvp.Value;
            ItemMetaData metaData = Managers.SpecData.GetItem(itemInfo.ItemId);
            if (metaData == null || metaData.ItemType != _currentTab)
                continue;

            Inventory_SlotItem slotItem = Managers.UI.MakeSubItem<Inventory_SlotItem>(_itemContent);
            slotItem.SetData(itemInfo);
        }
    }

    private void OnClickCloseButton()
    {
        ClosePopupUI();
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}

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
        EquipmentTab,
        ConsumableTab,
        MiscTab,
    }

    private Transform _itemContent;

    private ItemType _currentTab = ItemType.Equipment;

    // 활성화 여부 담당
    private GameObject _equipmentTab;
    private GameObject _consumableTab;
    private GameObject _miscTab;

    private int _defaultEquimentInventorySize;
    private int _defaultConsumableInventorySize;
    private int _defaultMiscInventorySize;

    private List<InventorySlot_SubItem> _slots = new List<InventorySlot_SubItem>();

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

        _equipmentTab = Get<GameObject>((int)GameObjects.EquipmentTab);
        _consumableTab = Get<GameObject>((int)GameObjects.ConsumableTab);
        _miscTab = Get<GameObject>((int)GameObjects.MiscTab);

        // UI에 빈 슬롯 미리 채워놓기
        // TODO - 후에 인벤토리 칸 확장 고려
        _defaultEquimentInventorySize = Managers.Config.GetInt(ConfigType.DefaultEquimentInventorySize);
        _defaultConsumableInventorySize = Managers.Config.GetInt(ConfigType.DefaultConsumableInventorySize);
        _defaultMiscInventorySize = Managers.Config.GetInt(ConfigType.DefaultMiscInventorySize);

        InitInventorySlotItems();

        Managers.Inventory.OnInventoryChanged -= UpdateUI;
        Managers.Inventory.OnInventoryChanged += UpdateUI;

        UpdateUI();
    }

    private void InitInventorySlotItems()
    {
        int maxSlotCount = Mathf.Max(
            _defaultEquimentInventorySize,
            _defaultConsumableInventorySize,
            _defaultMiscInventorySize
        );

        for (int i = 0; i < maxSlotCount; ++i)
        {
            InventorySlot_SubItem slot = Managers.UI.MakeSubItem<InventorySlot_SubItem>(_itemContent);
            slot.gameObject.SetActive(false);
            _slots.Add(slot);
        }
    }

    private void OnClickTab(ItemType tabType)
    {
        _currentTab = tabType;
        UpdateUI();
    }

    private void UpdateTab()
    {
        _equipmentTab.SetActive(_currentTab == ItemType.Equipment);
        _consumableTab.SetActive(_currentTab == ItemType.Consumable);
        _miscTab.SetActive(_currentTab == ItemType.Misc);
    }

    private void UpdateUI()
    {
        UpdateTab();
        RefreshItemList();
    }

    private void RefreshItemList()
    {
        int tabSize = GetCurrentTabSize();

        // 카테고리별로 딕셔너리가 분리되어 있으므로 현재 탭 것만 바로 사용
        var tabItems = Managers.Inventory.GetItems(_currentTab);

        for (int i = 0; i < _slots.Count; ++i)
        {
            bool isActive = i < tabSize;
            _slots[i].gameObject.SetActive(isActive);

            if (!isActive)
                continue;

            tabItems.TryGetValue(i, out ItemInfo foundItem);
            // 장착 중인 장비는 장비창에서 표시하므로 인벤토리에서는 숨김
            if (foundItem != null && foundItem.IsEquipped)
            {
                foundItem = null;
            }
            _slots[i].SetData(foundItem);
        }
    }

    private int GetCurrentTabSize()
    {
        switch (_currentTab)
        {
            case ItemType.Equipment:
                return _defaultEquimentInventorySize;
            case ItemType.Consumable:
                return _defaultConsumableInventorySize;
            case ItemType.Misc:
                return _defaultMiscInventorySize;
        }

        return 0;
    }

    private void OnClickCloseButton()
    {
        ClosePopupUI();
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Managers.RedDot.Set(RedDotType.Inventory, false);
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 툴팁도 끄기
        Managers.UI.HideItemTooltip();
    }

    private void OnDestroy()
    {
        Managers.Inventory.OnInventoryChanged -= UpdateUI;
    }
}

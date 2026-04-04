using Google.Protobuf.Protocol;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Equipment : UI_Popup
{
    enum EquipmentSlot_SubItems
    {
        MainWeaponSlot,
        SubWeaponSlot,
        HelmetSlot,
        ChestSlot,
        PantsSlot,
        BootsSlot,
    }

    enum Buttons
    {
        CloseButton,
        BackgroundButton,
    }

    enum Texts
    {
        StatSTRText,
        StatDEXText,
        StatINTText,
        StatLUKText,
        StatPhysicalDamageText,
        StatMagicDamageText,
        StatDefenseText,
    }

    private static readonly Dictionary<EquipmentSlotType, EquipmentSlot_SubItems> _slotMap = new Dictionary<EquipmentSlotType, EquipmentSlot_SubItems>
    {
        { EquipmentSlotType.MainWeapon, EquipmentSlot_SubItems.MainWeaponSlot },
        { EquipmentSlotType.SubWeapon, EquipmentSlot_SubItems.SubWeaponSlot },
        { EquipmentSlotType.Helmet, EquipmentSlot_SubItems.HelmetSlot },
        { EquipmentSlotType.Chest, EquipmentSlot_SubItems.ChestSlot },
        { EquipmentSlotType.Pants, EquipmentSlot_SubItems.PantsSlot },
        { EquipmentSlotType.Boots, EquipmentSlot_SubItems.BootsSlot },
    };

    public override void Init()
    {
        base.Init();

        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetButton((int)Buttons.CloseButton).onClick.AddListener(OnClickCloseButton);
        GetButton((int)Buttons.BackgroundButton).onClick.AddListener(OnClickCloseButton);

        Bind<EquipmentSlot_SubItem>(typeof(EquipmentSlot_SubItems));

        foreach (var kvp in _slotMap)
        {
            Get<EquipmentSlot_SubItem>((int)kvp.Value).Init();
            Get<EquipmentSlot_SubItem>((int)kvp.Value).SetData(kvp.Key);
        }

        Managers.Inventory.OnInventoryChanged -= UpdateStatUI;
        Managers.Inventory.OnInventoryChanged += UpdateStatUI;

        UpdateStatUI();
    }

    // TODO - 스탯 UI 추가되면 업데이트
    private void UpdateStatUI()
    {
        int str = 0, dex = 0, intStat = 0, luk = 0;
        int physDmg = 0, magDmg = 0, defense = 0;

        foreach (ItemInfo item in Managers.Inventory.GetItems(ItemType.Equipment).Values)
        {
            if (!item.IsEquipped)
                continue;

            EquipmentMetaData meta = Managers.SpecData.GetEquipment(item.ItemId);
            if (meta == null)
                continue;

            str += meta.BaseSTR;
            dex += meta.BaseDEX;
            intStat += meta.BaseINT;
            luk += meta.BaseLUK;
            physDmg += meta.PhysicalDamage;
            magDmg += meta.MagicDamage;
            defense += meta.Defense;
        }

        GetTextMeshProUGUI((int)Texts.StatSTRText).text = "STR:" + str.ToString();
        GetTextMeshProUGUI((int)Texts.StatDEXText).text = "DEX:" + dex.ToString();
        GetTextMeshProUGUI((int)Texts.StatINTText).text = "INT:" + intStat.ToString();
        GetTextMeshProUGUI((int)Texts.StatLUKText).text = "LUK:" + luk.ToString();
        // GetTextMeshProUGUI((int)Texts.StatPhysicalDamageText).text = physDmg.ToString();
        // GetTextMeshProUGUI((int)Texts.StatMagicDamageText).text = magDmg.ToString();
        // GetTextMeshProUGUI((int)Texts.StatDefenseText).text = defense.ToString();
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
        Managers.UI.HideItemTooltip();
    }

    private void OnDestroy()
    {
        Managers.Inventory.OnInventoryChanged -= UpdateStatUI;
    }
}

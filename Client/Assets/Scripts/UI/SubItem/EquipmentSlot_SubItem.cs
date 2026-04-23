using Google.Protobuf.Protocol;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EquipmentSlot_SubItem : UI_Base, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    enum Images
    {
        ItemImage,
    }

    private EquipmentSlotType _slotType;
    private Image _itemImage;

    public override void Init()
    {
        Bind<Image>(typeof(Images));
        _itemImage = Get<Image>((int)Images.ItemImage);

        Managers.Inventory.OnInventoryChanged -= UpdateUI;
        Managers.Inventory.OnInventoryChanged += UpdateUI;

        UpdateUI();
    }

    public void SetData(EquipmentSlotType slotType)
    {
        _slotType = slotType;
        UpdateUI();
    }

    private void UpdateUI()
    {
        ItemInfo equipped = Managers.Inventory.GetEquippedItem(_slotType);
        if (equipped != null)
        {
            _itemImage.sprite = Managers.Image.GetAssetImage(equipped.ItemId);
            _itemImage.gameObject.SetActive(true);
        }
        else
        {
            _itemImage.gameObject.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Managers.UI.IsDraggingItem)
        {
            if (eventData.clickCount == 1)
                HandleDrop();
            return;
        }

        if (eventData.clickCount != 2)
            return;

        ItemInfo equipped = Managers.Inventory.GetEquippedItem(_slotType);
        if (equipped == null)
            return;

        C_UnequipItem packet = new C_UnequipItem { SlotType = _slotType };
        Managers.Network.Send(packet);
    }

    private void HandleDrop()
    {
        ItemInfo dragged = Managers.UI.DraggingItem;

        Managers.UI.EndItemDrag();

        // Equipment 타입만 장착 가능
        if (Util.GetItemType(dragged.ItemId) != ItemType.Equipment)
            return;

        // 슬롯 타입 일치 확인
        EquipmentMetaData meta = Managers.SpecData.GetEquipment(dragged.ItemId);
        if (meta == null || meta.EquipmentSlotType != _slotType)
            return;

        // 직업 조건 확인
        if (!Managers.Equipment.CanEquipByJob(dragged.ItemId))
        {
            Managers.UI.ShowToastPopup("직업 제한으로 장비를 착용할 수 없습니다.");
            return;
        }

        C_EquipItem packet = new C_EquipItem { SlotIndex = dragged.SlotIndex };
        Managers.Network.Send(packet);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Managers.UI.IsDraggingItem)
            return;

        ItemInfo equipped = Managers.Inventory.GetEquippedItem(_slotType);
        if (equipped == null)
            return;

        Managers.UI.ShowItemTooltip(equipped);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Managers.UI.HideItemTooltip();
    }

    private void OnDestroy()
    {
        Managers.Inventory.OnInventoryChanged -= UpdateUI;
    }
}

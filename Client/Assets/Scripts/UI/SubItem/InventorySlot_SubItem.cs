using Google.Protobuf.Protocol;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlot_SubItem : UI_SubItem<ItemInfo>, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    // 설정하면 기본 더블클릭 동작 대신 이 콜백이 호출됨 (UI_Equipment 슬롯 등에서 사용)
    public Action<ItemInfo> OnDoubleClickOverride { get; set; }
    enum Texts
    {
        CountText,
        EnchantLevelText,
    }

    enum Images
    {
        ItemImage,
        CooltimeImage,
        GradeImage,
    }

    enum GameObjects
    {
        CountBadge,
        EnchantBadge,
    }
    private RectTransform _rectTransform;
    private bool _isHovered;

    private Image _itemImage;
    private Image _cooltimeImage;
    private Image _gradeImage;
    private GameObject _countBadge;
    private TextMeshProUGUI _countText;
    private GameObject _enchantBadge;
    private TextMeshProUGUI _enchantLevelText;

    private int _equipmentStartId;
    private int _consumableStartId;
    private float _totalCooldown;

    // 드래그&드롭용 슬롯 컨텍스트
    private ItemType _slotItemType;
    private int _slotIndex;

    public override void Init()
    {
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Image>(typeof(Images));
        Bind<GameObject>(typeof(GameObjects));

        _rectTransform = GetComponent<RectTransform>();

        _itemImage = GetImage((int)Images.ItemImage);
        _cooltimeImage = GetImage((int)Images.CooltimeImage);
        _gradeImage = GetImage((int)Images.GradeImage);
        _cooltimeImage.fillAmount = 0f;
        _cooltimeImage.gameObject.SetActive(false);

        _countBadge = Get<GameObject>((int)GameObjects.CountBadge);
        _countText = GetTextMeshProUGUI((int)Texts.CountText);

        _enchantBadge = Get<GameObject>((int)GameObjects.EnchantBadge);
        _enchantLevelText = GetTextMeshProUGUI((int)Texts.EnchantLevelText);

        _equipmentStartId = Managers.Config.GetInt(ConfigType.EquipmentStartId);
        _consumableStartId = Managers.Config.GetInt(ConfigType.ConsumableStartId);
    }

    public void SetSlotContext(ItemType itemType, int slotIndex)
    {
        _slotItemType = itemType;
        _slotIndex = slotIndex;
    }

    public override void SetData(ItemInfo data)
    {
        base.SetData(data);
        if (_data != null)
        {
            ConsumableMetaData consumable = Managers.SpecData.GetConsumable(_data.ItemId);
            _totalCooldown = (consumable != null ? consumable.CoolTime : 0f);
        }
        UpdateUI();
    }

    protected override void UpdateUI()
    {
        if (_data == null)
        {
            if (_isHovered)
                Managers.UI.HideItemTooltip();
            _itemImage.sprite = null;
            _gradeImage.color = Color.clear;
            _countBadge.SetActive(false);
            _enchantBadge.SetActive(false);
            return;
        }

        _itemImage.sprite = Managers.Image.GetAssetImage(_data.ItemId);

        ItemMetaData meta = Managers.SpecData.GetItem(_data.ItemId);
        if (meta != null)
        {
            _gradeImage.color = Managers.Color.GetGradeColor(meta.ItemGrade);
        }

        bool showCount = (_data.Count >= 1);
        _countBadge.SetActive(showCount);
        if (showCount)
        {
            _countText.text = _data.Count.ToString();
        }

        // 장비만 강화 단계 표시
        bool showEnchant = (_equipmentStartId <= _data.ItemId) && (_data.ItemId < _consumableStartId);
        _enchantBadge.SetActive(showEnchant);
        if (showEnchant)
        {
            _enchantLevelText.text = $"+{_data.EnchantLevel}";
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;

        if (_data == null || Managers.UI.IsDraggingItem)
            return;

        Managers.UI.ShowItemTooltip(_data);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        Managers.UI.HideItemTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 1)
        {
            if (Managers.UI.IsDraggingItem)
            {
                HandleDrop();
                return;
            }

            if (_data == null)
                return;

            Sprite icon = Managers.Image.GetAssetImage(_data.ItemId);
            Managers.UI.StartItemDrag(_data, _slotItemType, icon);
            return;
        }

        if (eventData.clickCount == 2)
        {
            // 같은 슬롯에서 드래그 시작 후 빠른 더블클릭 → 드래그 취소 후 장착/사용 처리
            if (Managers.UI.IsDraggingItem)
            {
                if (Managers.UI.DraggingItem == _data)
                    Managers.UI.EndItemDrag();
                else
                    return;
            }

            if (_data == null)
                return;

            if (OnDoubleClickOverride != null)
            {
                OnDoubleClickOverride.Invoke(_data);
                return;
            }

            ItemType itemType = Util.GetItemType(_data.ItemId);

            if (itemType == ItemType.Equipment)
            {
                if (!Managers.Equipment.CanEquipByJob(_data.ItemId))
                {
                    Managers.UI.ShowToastPopup("직업 제한으로 장비를 착용할 수 없습니다.");
                    return;
                }

                C_EquipItem equipPacket = new C_EquipItem { SlotIndex = _data.SlotIndex };
                Managers.Network.Send(equipPacket);
            }
            else
            {
                if (Managers.Cooldown.IsOnCooldown(_data.ItemId))
                {
                    Managers.UI.ShowToastPopup("아직 쿨타임이 남았습니다.");
                    return;
                }
                Managers.Cooldown.RequestUseItem(_data.SlotIndex, itemType);
            }
        }
    }

    private void HandleDrop()
    {
        ItemInfo dragged = Managers.UI.DraggingItem;
        ItemType draggedType = Managers.UI.DraggingItemType;

        Managers.UI.EndItemDrag();

        // 카테고리 불일치 → 취소
        if (draggedType != _slotItemType)
            return;

        // 같은 슬롯에 내려놓기 → 취소
        if (dragged.SlotIndex == _slotIndex && _data != null)
            return;

        int toSlot = (_data != null) ? _data.SlotIndex : _slotIndex;

        C_MoveItem packet = new C_MoveItem
        {
            FromSlotIndex = dragged.SlotIndex,
            ToSlotIndex = toSlot,
            ItemType = draggedType,
        };
        Managers.Network.Send(packet);
    }

    private void Update()
    {
        // 쿨타임 UI 업데이트
        if (_data == null)
            return;

        // 먼저 쿨타임 중인지 확인 (데이터 기반)
        float remain = Managers.Cooldown.GetRemainingCooldownSeconds(_data.ItemId);

        if (remain > 0f)
        {
            if (!_cooltimeImage.gameObject.activeSelf)
            {
                _cooltimeImage.gameObject.SetActive(true);
            }

            // 남은 시간 비율 업데이트
            _cooltimeImage.fillAmount = remain / _totalCooldown;
        }
        else
        {
            if (_cooltimeImage.gameObject.activeSelf)
            {
                _cooltimeImage.gameObject.SetActive(false);
            }
        }
    }
}

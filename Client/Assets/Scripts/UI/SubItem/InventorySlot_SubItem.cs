using Google.Protobuf.Protocol;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlot_SubItem : UI_SubItem<ItemInfo>
{
    enum Texts
    {
        CountText,
        EnchantLevelText,
    }

    enum Images
    {
        ItemImage,
    }

    enum GameObjects
    {
        CountBadge,
        EnchantBadge,
    }

    public Action<ItemInfo> OnClickSlotAction;
    private Image _itemImage;
    private GameObject _countBadge;
    private TextMeshProUGUI _countText;
    private GameObject _enchantBadge;
    private TextMeshProUGUI _enchantLevelText;

    private int _equipmentStartId;
    private int _consumableStartId;

    public override void Init()
    {
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Image>(typeof(Images));
        Bind<GameObject>(typeof(GameObjects));

        // TODO - 툴팁 추가
        BindEvent(gameObject, _ => OnClickSlotAction?.Invoke(_data));

        _itemImage = GetImage((int)Images.ItemImage);

        _countBadge = Get<GameObject>((int)GameObjects.CountBadge);
        _countText = GetTextMeshProUGUI((int)Texts.CountText);

        _enchantBadge = Get<GameObject>((int)GameObjects.EnchantBadge);
        _enchantLevelText = GetTextMeshProUGUI((int)Texts.EnchantLevelText);

        _equipmentStartId = Managers.Config.GetInt(ConfigType.EquipmentStartId);
        _consumableStartId = Managers.Config.GetInt(ConfigType.ConsumableStartId);
    }

    public override void SetData(ItemInfo data)
    {
        base.SetData(data);
        UpdateUI();
    }

    protected override void UpdateUI()
    {
        if (_data == null)
        {
            _itemImage.sprite = null;
            _countBadge.SetActive(false);
            _enchantBadge.SetActive(false);
            return;
        }
        _itemImage.sprite = Managers.Image.GetAssetImage(_data.ItemId);

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
}

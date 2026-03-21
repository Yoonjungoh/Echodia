using Google.Protobuf.Protocol;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Inventory_SubItem : UI_SubItem<ItemInfo>
{
    enum Texts
    {
        CountText,
        EnchantText,
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

    public override void Init()
    {
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Image>(typeof(Images));
        Bind<GameObject>(typeof(GameObjects));

        BindEvent(gameObject, _ => OnClickSlotAction?.Invoke(_data));
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
            GetImage((int)Images.ItemImage).sprite = null;
            Get<GameObject>((int)GameObjects.CountBadge).SetActive(false);
            Get<GameObject>((int)GameObjects.EnchantBadge).SetActive(false);
            return;
        }

        GetImage((int)Images.ItemImage).sprite = Managers.Image.GetAssetImage(_data.ItemId);

        bool showCount = _data.Count > 1;
        Get<GameObject>((int)GameObjects.CountBadge).SetActive(showCount);
        if (showCount)
            GetTextMeshProUGUI((int)Texts.CountText).text = _data.Count.ToString();

        bool showEnchant = _data.EnchantLevel > 0;
        Get<GameObject>((int)GameObjects.EnchantBadge).SetActive(showEnchant);
        if (showEnchant)
            GetTextMeshProUGUI((int)Texts.EnchantText).text = $"+{_data.EnchantLevel}";
    }
}

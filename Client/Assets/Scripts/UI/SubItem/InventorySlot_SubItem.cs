using Google.Protobuf.Protocol;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlot_SubItem : UI_SubItem<ItemInfo>, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
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
            _itemImage.sprite = null;
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
        if (_data == null)
            return;
            
        Managers.UI.ShowItemTooltip(_data);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Managers.UI.HideItemTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_data == null)
            return;

        if (eventData.clickCount == 2)
        {
            // 쿨타임 체크 후 패킷 전송
            if (Managers.Cooldown.IsOnCooldown(_data.ItemId))
            {
                Managers.UI.ShowToastPopup("아직 쿨타임이 남았습니다.");
                return;
            }
            Managers.Cooldown.RequestUseItem(_data.SlotIndex, Util.GetItemType(_data.ItemId));
        }
        // TODO - 클릭 시 아이템 정보 툴팁 등을 띄운다면 1일 때 처리
        else if (eventData.clickCount == 1)
        {

        }
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

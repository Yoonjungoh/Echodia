using Google.Protobuf.Protocol;
using TMPro;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.UI;

public class UI_ItemTooltip : UI_Popup<ItemInfo>
{
    enum Texts
    {
        ItemNameText,
        ItemGradeText,
        ItemTypeText,
        ItemDescText,
    }

    enum Images
    {
        ItemGradeImage,
        ItemImage,
    }

    enum GameObjects
    {
        ItemTooltipPanel,
    }

    private RectTransform _panelRect;

    public override void Init()
    {
        base.Init();
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Image>(typeof(Images));
        Bind<GameObject>(typeof(GameObjects));

        _panelRect = Get<GameObject>((int)GameObjects.ItemTooltipPanel).GetComponent<RectTransform>();

        gameObject.SetActive(false);
    }

    // UIManager가 직접 호출
    public override void ClosePopupUI()
    {
        HideTooltip();
    }

    public void ShowTooltip(ItemInfo data)
    {
        _data = data;
        gameObject.SetActive(true);
        UpdateUI();
        // ContentSizeFitter 등 레이아웃이 반영된 실제 크기를 즉시 확정하는 함수
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
        ApplyPosition(Input.mousePosition);
    }

    private void ApplyPosition(Vector2 mouseScreen)
    {
        float w = _panelRect.rect.width;
        float h = _panelRect.rect.height;

        // 커서 오른쪽 위에 붙이는 것을 기본으로, 화면 경계 시 반대편으로 전환
        float topLeftX = mouseScreen.x + 16f;
        float topLeftY = mouseScreen.y + 16f + h;

        if (topLeftX + w > Screen.width)
            topLeftX = mouseScreen.x - w - 16f;
        if (topLeftY > Screen.height)
            topLeftY = Screen.height;
        if (topLeftY - h < 0f)
            topLeftY = h;

        // RectTransform.position 은 pivot 위치를 world 좌표로 지정한다.
        Vector2 pivot = _panelRect.pivot;
        _panelRect.position = new Vector3(
            topLeftX + w * pivot.x,
            topLeftY - h * (1f - pivot.y),
            0f
        );
    }

    private void Update()
    {
        if (!gameObject.activeSelf)
            return;

        ApplyPosition(Input.mousePosition);
    }

    public void HideTooltip()
    {
        gameObject.SetActive(false);
    }

    protected override void UpdateUI()
    {
        if (_data == null)
        {
            HideTooltip();
            return;
        }

        ItemMetaData meta = Managers.SpecData.GetItem(_data.ItemId);
        if (meta == null)
        {
            HideTooltip();
            return;
        }

        Color gradeColor = Managers.Color.GetGradeColor(meta.ItemGrade);

        // 아이템 아이콘 + 등급 테두리 색상
        GetImage((int)Images.ItemImage).sprite = Managers.Image.GetAssetImage(_data.ItemId);
        GetImage((int)Images.ItemGradeImage).color = gradeColor;

        // 아이템 이름 (등급 색상 적용)
        TextMeshProUGUI nameText = GetTextMeshProUGUI((int)Texts.ItemNameText);
        nameText.text = meta.ItemName;
        nameText.color = gradeColor;

        // 등급 텍스트
        TextMeshProUGUI gradeText = GetTextMeshProUGUI((int)Texts.ItemGradeText);
        gradeText.text = GetGradeString(meta.ItemGrade);
        gradeText.color = gradeColor;

        // 타입 + 상세 스탯
        GetTextMeshProUGUI((int)Texts.ItemTypeText).text = BuildTypeText(meta);

        // 설명 + 판매 정보
        GetTextMeshProUGUI((int)Texts.ItemDescText).text = BuildDescText(meta);
    }

    // TODO - LanguageManager 생기면 토큰으로 빼기
    private string BuildTypeText(ItemMetaData meta)
    {
        switch (meta.ItemType)
        {
            case ItemType.Equipment:
            {
                EquipmentMetaData equip = Managers.SpecData.GetEquipment(_data.ItemId);
                if (equip == null) return "장비";

                string slot = GetEquipSlotString(equip.EquipmentSlotType);
                string result = $"[장비] {slot}";
                if (_data.EnchantLevel > 0)
                    result += $"\n강화 단계  +{_data.EnchantLevel}";
                if (equip.PhysicalDamage > 0)
                    result += $"\n물리 공격력  {equip.PhysicalDamage}";
                if (equip.MagicDamage > 0)
                    result += $"\n마법 공격력  {equip.MagicDamage}";
                if (equip.Defense > 0)
                    result += $"\n방어력  {equip.Defense}";
                if (equip.RequiredLevel > 0)
                    result += $"\n필요 레벨  Lv.{equip.RequiredLevel}";
                return result;
            }
            case ItemType.Consumable:
            {
                ConsumableMetaData cons = Managers.SpecData.GetConsumable(_data.ItemId);
                if (cons == null) return "소비";

                string type = GetConsumableTypeString(cons.ConsumableType);
                string result = $"[소비] {type}";
                if (cons.EffectValue > 0)
                    result += $"\n효과량  {cons.EffectValue}";
                if (cons.Duration > 0)
                    result += $"\n지속 시간  {cons.Duration}초";
                if (cons.CoolTime > 0)
                    result += $"\n쿨타임  {cons.CoolTime}초";
                if (cons.RequiredLevel > 0)
                    result += $"\n필요 레벨  Lv.{cons.RequiredLevel}";
                return result;
            }
            case ItemType.Misc:
            {
                MiscMetaData misc = Managers.SpecData.GetMisc(_data.ItemId);
                string type = misc != null ? GetMiscTypeString(misc.MiscType) : "";
                return $"[기타] {type}";
            }
            default:
                return string.Empty;
        }
    }

    private string BuildDescText(ItemMetaData meta)
    {
        string desc = string.IsNullOrEmpty(meta.Description) ? string.Empty : meta.Description;
        string sellLine = meta.CanSell ? $"판매가  {meta.SellPrice} G" : "판매 불가";
        return string.IsNullOrEmpty(desc) ? sellLine : $"{desc}\n\n{sellLine}";
    }

    private string GetGradeString(ItemGrade grade)
    {
        return grade.ToString();
    }

    private string GetEquipSlotString(EquipmentSlotType slot)
    {
        return slot.ToString();
    }

    private string GetConsumableTypeString(ConsumableType type)
    {
        return type.ToString();
    }

    private string GetMiscTypeString(MiscType type)
    {
        return type.ToString();
    }
}

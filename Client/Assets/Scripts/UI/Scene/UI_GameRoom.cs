using Google.Protobuf.Protocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_GameRoom : UI_Scene
{
    enum Texts
    {
        LevelText,
        ExpText,
        HpText,
        DropItemNameText,
        DropItemDescText,
        DropItemCountText,
    }

    enum Sliders
    {
        ExpSlider,
        HpBarSlider,
    }

    enum Buttons
    {
        QuestPopupButton,
        InventoryPopupButton,
    }

    enum GameObjects
    {
        QuestRedDot,
        DropItemTooltipPanel,
    }

    enum Images
    {
        DropItemImage,
    }

    private int _level = -1;
    private int _exp = -1;
    private int _maxExp = -1;

    private TextMeshProUGUI _levelText;
    private TextMeshProUGUI _expText;
    private TextMeshProUGUI _hpText;
    private Slider _hpBarSlider;
    private Slider _expSlider;
    private GameObject _questRedDot;

    private GameObject _dropItemTooltipPanel;
    private TextMeshProUGUI _dropItemNameText;
    private TextMeshProUGUI _dropItemDescText;
    private TextMeshProUGUI _dropItemCountText;
    private Image _dropItemImage;

    public override void Init()
    {
        base.Init();

        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Slider>(typeof(Sliders));
        Bind<Button>(typeof(Buttons));
        Bind<GameObject>(typeof(GameObjects));
        Bind<Image>(typeof(Images));

        _levelText = GetTextMeshProUGUI((int)Texts.LevelText);
        _expText = GetTextMeshProUGUI((int)Texts.ExpText);
        _hpText = GetTextMeshProUGUI((int)Texts.HpText);
        _expSlider = Get<Slider>((int)Sliders.ExpSlider);
        _hpBarSlider = Get<Slider>((int)Sliders.HpBarSlider);
        _questRedDot = Get<GameObject>((int)GameObjects.QuestRedDot);

        _dropItemTooltipPanel = GetObject((int)GameObjects.DropItemTooltipPanel);
        _dropItemNameText = GetTextMeshProUGUI((int)Texts.DropItemNameText);
        _dropItemDescText = GetTextMeshProUGUI((int)Texts.DropItemDescText);
        _dropItemCountText = GetTextMeshProUGUI((int)Texts.DropItemCountText);
        _dropItemImage = GetImage((int)Images.DropItemImage);
        _dropItemTooltipPanel.SetActive(false);

        GetButton((int)Buttons.QuestPopupButton).onClick.AddListener(OnClickQuestPopupInput);
        GetButton((int)Buttons.InventoryPopupButton).onClick.AddListener(OnClickInventoryPopupInput);

        Managers.Input.RegisterKeyAction(KeySettings.ActivationQuestPopup, OnClickQuestPopupInput);
        Managers.Input.RegisterKeyAction(KeySettings.ActivationInventoryPopup, OnClickInventoryPopupInput);
        Managers.Input.RegisterKeyAction(KeySettings.CloseRecentPopup, Managers.UI.ClosePopupUI);

        Managers.RedDot.OnRedDotChanged -= OnRedDotChanged;
        Managers.RedDot.OnRedDotChanged += OnRedDotChanged;
        _questRedDot.SetActive(Managers.RedDot.IsActive(RedDotType.Quest));

        RequestInitGameRoomData();
    }

    private void OnDestroy()
    {
        Managers.RedDot.OnRedDotChanged -= OnRedDotChanged;
    }

    private void OnRedDotChanged(RedDotType type, bool isActive)
    {
        if (type == RedDotType.Quest)
            _questRedDot.SetActive(isActive);
    }

    private void OnClickQuestPopupInput()
    {
        if (Managers.UI.IsPopupActive<UI_Quest>())
        {
            Managers.UI.CloseSpecificPopup<UI_Quest>();
        }
        else
        {
            Managers.UI.ShowPopupUI<UI_Quest>();
        }
    }

    private void OnClickInventoryPopupInput()
    {
        if (Managers.UI.IsPopupActive<UI_Inventory>())
        {
            Managers.UI.CloseSpecificPopup<UI_Inventory>();
        }
        else
        {
            Managers.UI.ShowPopupUI<UI_Inventory>();
        }
    }

    public void RequestInitGameRoomData()
    {
        C_RequestInitGameRoomData requestInitGameRoomDataPacket = new C_RequestInitGameRoomData();
        Managers.Network.Send(requestInitGameRoomDataPacket);
    }

    public void SetData(InitGameRoomData initGameRoomData)
    {
        _level = initGameRoomData.Level;
        _exp = initGameRoomData.Exp;
        _maxExp = initGameRoomData.MaxExp;

        UpdateUI();
    }

    public void SetExp(int exp, int maxExp)
    {
        _exp = exp;
        _maxExp = maxExp;

        UpdateUI();
    }

    public void SetLevel(int level)
    {
        _level = level;

        UpdateUI();
    }

    public void SetHp(float hp, float maxHp)
    {
        _hpText.text = $"Hp: {hp}/{maxHp}";
        _hpBarSlider.value = hp / maxHp;
    }

    public void ShowDropItemTooltip(int specItemId, int count)
    {
        // 0으로 들어오면 숨기기
        if (specItemId == 0 && count == 0)
        {
            HideDropItemTooltip();
            return;
        }

        ItemMetaData meta = Managers.SpecData.GetItem(specItemId);
        if (meta == null)
            return;

        _dropItemNameText.text = meta.ItemName;
        _dropItemDescText.text = meta.Description;
        _dropItemCountText.text = count >= 1 ? $"x{count}" : "";
        _dropItemImage.sprite = Managers.Image.GetAssetImage(specItemId);
        _dropItemTooltipPanel.SetActive(true);
    }

    private void HideDropItemTooltip()
    {
        _dropItemTooltipPanel.SetActive(false);
    }

    private void UpdateUI()
    {
        float expRate = (float)_exp / _maxExp;
        _levelText.text = $"Level.{_level}";
        // 소수점 2자리까지 표현하기 위해 100을 곱한 후 소수점 2자리로 포맷팅
        _expText.text = $"{_exp}/{_maxExp}({expRate * 100:F2}%)";

        _expSlider.value = expRate;
    }
}

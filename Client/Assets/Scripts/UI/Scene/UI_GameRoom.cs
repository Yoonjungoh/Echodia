using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

    public override void Init()
    {
        base.Init();

        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Slider>(typeof(Sliders));
        Bind<Button>(typeof(Buttons));
        Bind<GameObject>(typeof(GameObjects));

        _levelText = GetTextMeshProUGUI((int)Texts.LevelText);
        _expText = GetTextMeshProUGUI((int)Texts.ExpText);
        _hpText = GetTextMeshProUGUI((int)Texts.HpText);
        _expSlider = Get<Slider>((int)Sliders.ExpSlider);
        _hpBarSlider = Get<Slider>((int)Sliders.HpBarSlider);
        _questRedDot = Get<GameObject>((int)GameObjects.QuestRedDot);

        GetButton((int)Buttons.QuestPopupButton).onClick.AddListener(OnClickQuestPopupInput);
        GetButton((int)Buttons.InventoryPopupButton).onClick.AddListener(OnClickInventoryPopupInput);

        Managers.Input.RegisterKeyAction(KeyCode.Q, OnClickQuestPopupInput);
        Managers.Input.RegisterKeyAction(KeyCode.I, OnClickInventoryPopupInput);

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

    private void UpdateUI()
    {
        float expRate = (float)_exp / _maxExp;
        _levelText.text = $"Level.{_level}";
        // 소수점 2자리까지 표현하기 위해 100을 곱한 후 소수점 2자리로 포맷팅
        _expText.text = $"{_exp}/{_maxExp}({expRate * 100:F2}%)";

        _expSlider.value = expRate;
    }
}

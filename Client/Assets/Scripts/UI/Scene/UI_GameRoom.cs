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
    }

    enum Sliders
    {
        ExpSlider,
    }

    private int _level = -1;
    private int _exp = -1;
    private int _maxExp = -1;

    public override void Init()
    {
        base.Init();

        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Slider>(typeof(Sliders));
        RequestInitGameRoomData();
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

    private void UpdateUI()
    {
        float expRate = (float)_exp / _maxExp;
        GetTextMeshProUGUI((int)Texts.LevelText).text = $"Level.{_level}";
        // 소수점 2자리까지 표현하기 위해 100을 곱한 후 소수점 2자리로 포맷팅
        GetTextMeshProUGUI((int)Texts.ExpText).text = $"{_exp}/{_maxExp}({expRate * 100:F2}%)";

        GetSlider((int)Sliders.ExpSlider).value = expRate;
    }
}

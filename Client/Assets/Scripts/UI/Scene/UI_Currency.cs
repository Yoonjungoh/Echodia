using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Currency : UI_Scene
{
    enum Texts
    {
        JewelText,
        GoldText,
    }

    // 재화 UI에 필요한 UI 텍스트 컴포넌트
    private Dictionary<CurrencyType, TextMeshProUGUI> _currencyTextDictionary;

    // 재화 UI에 필요한 재화 데이터
    private Dictionary<CurrencyType, int> _currencyDataDictionary = new Dictionary<CurrencyType, int>();

    public void SetData(CurrencyData currencyData)
    {
        // 재화 텍스트 컴포넌트 초기화
        if (_currencyTextDictionary == null)
        {
            _currencyTextDictionary = new Dictionary<CurrencyType, TextMeshProUGUI>();

            foreach (CurrencyType currencyType in Enum.GetValues(typeof(CurrencyType)))
            {
                if (currencyType == CurrencyType.None)
                    continue;

                string textName = currencyType + "Text";

                if (Enum.TryParse<Texts>(textName, out var textEnum) == false)
                {
                    Debug.LogError($"[UI_Currency] Texts enum에 {textName} 없음");
                    continue;
                }

                TextMeshProUGUI currencyText = GetTextMeshProUGUI((int)textEnum);
                if (currencyText != null)
                    _currencyTextDictionary[currencyType] = currencyText;
            }
        }

        // 재화 데이터 초기화
        foreach (CurrencyType currencyType in Enum.GetValues(typeof(CurrencyType)))
        {
            if (currencyType == CurrencyType.None)
                continue;

            string currencyName = currencyType.ToString();

            PropertyInfo property = typeof(CurrencyData).GetProperty(currencyName);

            if (property != null)
            {
                int value = (int)property.GetValue(currencyData);
                _currencyDataDictionary[currencyType] = value;
                _currencyTextDictionary[currencyType].text = value.ToString();
            }
            else
            {
                Debug.LogError($"CurrencyData에 {currencyName} 속성이 없습니다.");
            }
        }
    }
    
    // 특정 재화 타입과 정보를 설정 (반드시 _currencyText, _currencyData 가 초기화 된 상태에서만 설정)
    public void SetData(CurrencyType currencyType, int amount)
    {
        if (_currencyTextDictionary == null || _currencyDataDictionary.ContainsKey(currencyType) == false)
        {
            Debug.LogError($"UI_Currency에 {currencyType} 텍스트 컴포넌트가 없습니다.");
            return;
        }

        _currencyDataDictionary[currencyType] = amount;
        _currencyTextDictionary[currencyType].text = amount.ToString();
    }

    public override void Init()
    {
        base.Init();

        Bind<TextMeshProUGUI>(typeof(Texts));

        // 본인을 해당 Scene의 Currency UI로 설정
        Managers.UI.CurrencyUI = this;

        // 최초 UI 초기화 시 재화 데이터를 서버에 요청
        RequestCurrencyDataAll();
    }

    // 서버에 모든 최신 재화 데이터 요청 
    public void RequestCurrencyDataAll()
    {
        C_UpdateCurrencyDataAll updateCurrencyDataAllPacket = new C_UpdateCurrencyDataAll();
        updateCurrencyDataAllPacket.PlayerId = Managers.GameRoomObject.PlayerId;
        Managers.Network.Send(updateCurrencyDataAllPacket);
    }

    // 서버에 특정 최신 재화 데이터 요청 
    public void RequestCurrencyData(CurrencyType currencyType)
    {
        C_UpdateCurrencyData updateCurrencyDataPacket = new C_UpdateCurrencyData();
        updateCurrencyDataPacket.CurrencyType = currencyType;
        updateCurrencyDataPacket.PlayerId = Managers.GameRoomObject.PlayerId;
        Managers.Network.Send(updateCurrencyDataPacket);
    }
}

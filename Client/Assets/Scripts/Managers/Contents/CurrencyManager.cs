using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CurrencyManager
{
    public void UpdateCurrencyData(CurrencyType currencyType, int amount)
    {
        Managers.UI.CurrencyUI.SetData(currencyType, amount);
    }

    public void UpdateCurrencyDataAll(CurrencyData currencyData)
    {
        Managers.UI.CurrencyUI.SetData(currencyData);
    }


    // 서버에 모든 최신 재화 데이터 요청 
    public void RequestCurrencyDataAll()
    {
        C_UpdateCurrencyDataAll updateCurrencyDataAllPacket = new C_UpdateCurrencyDataAll();
        Managers.Network.Send(updateCurrencyDataAllPacket);
    }

    // 서버에 특정 최신 재화 데이터 요청 
    public void RequestCurrencyData(CurrencyType currencyType)
    {
        C_UpdateCurrencyData updateCurrencyDataPacket = new C_UpdateCurrencyData();
        updateCurrencyDataPacket.CurrencyType = currencyType;
        Managers.Network.Send(updateCurrencyDataPacket);
    }
}
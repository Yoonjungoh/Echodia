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
}
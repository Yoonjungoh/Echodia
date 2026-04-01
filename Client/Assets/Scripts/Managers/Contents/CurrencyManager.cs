using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CurrencyManager
{
    // 인메모리 재화 캐시 (InventoryManager의 _items와 동일한 전략)
    // 서버로부터 S_UpdateCurrencyData / S_UpdateCurrencyDataAll 수신 시 갱신됨
    private readonly Dictionary<CurrencyType, int> _currencies = new Dictionary<CurrencyType, int>();

    public event Action OnCurrencyChanged;

    // 특정 재화 현재 잔액 조회 (서버 요청 없이 즉시 반환)
    public int Get(CurrencyType currencyType)
    {
        return _currencies.TryGetValue(currencyType, out int value) ? value : 0;
    }

    // S_UpdateCurrencyData 수신 시 호출 - 단건 갱신
    public void UpdateCurrencyData(CurrencyType currencyType, int amount)
    {
        _currencies[currencyType] = amount;
        if (Managers.UI.CurrencyUI != null)
        {
            Managers.UI.CurrencyUI.SetData(currencyType, amount);
        }

        OnCurrencyChanged?.Invoke();
    }

    // S_UpdateCurrencyDataAll 수신 시 호출 - 전체 갱신
    public void UpdateCurrencyDataAll(CurrencyData currencyData)
    {
        _currencies[CurrencyType.Jewel] = currencyData.Jewel;
        _currencies[CurrencyType.Gold] = currencyData.Gold;
        _currencies[CurrencyType.Exp] = currencyData.Exp;
        _currencies[CurrencyType.Level] = currencyData.Level;
        if (Managers.UI.CurrencyUI != null)
        {
            Managers.UI.CurrencyUI.SetData(currencyData);
        }
        OnCurrencyChanged?.Invoke();
    }

    // 로그아웃/씬 전환 시 캐시 초기화
    public void Clear()
    {
        _currencies.Clear();
    }

    // 서버에 모든 최신 재화 데이터 요청 (재접속/재동기화 용도)
    public void RequestCurrencyDataAll()
    {
        C_UpdateCurrencyDataAll updateCurrencyDataAllPacket = new C_UpdateCurrencyDataAll();
        Managers.Network.Send(updateCurrencyDataAllPacket);
    }

    // 서버에 특정 최신 재화 데이터 요청 (재동기화 용도)
    public void RequestCurrencyData(CurrencyType currencyType)
    {
        C_UpdateCurrencyData updateCurrencyDataPacket = new C_UpdateCurrencyData();
        updateCurrencyDataPacket.CurrencyType = currencyType;
        Managers.Network.Send(updateCurrencyDataPacket);
    }
}

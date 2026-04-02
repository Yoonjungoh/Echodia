
using System;
using System.Collections.Generic;
using Google.Protobuf.Protocol;
using UnityEngine;

public class ImageManager
{
    private const string CurrencyBasePath = "Prefabs/Sprites/Currency";
    private const string EquipmentBasePath = "Prefabs/Sprites/Item/Equipment";
    private const string ConsumableBasePath = "Prefabs/Sprites/Item/Consumable";
    private const string MiscBasePath = "Prefabs/Sprites/Item/Misc";

    private Dictionary<CurrencyType, Sprite> _currencySprites = new();
    private Dictionary<EquipmentType, Sprite> _equipmentSprites = new();
    private Dictionary<ConsumableType, Sprite> _consumableSprites = new();
    private Dictionary<MiscType, Sprite> _miscSprites = new();
    private int _equipmentStartId;
    private int _consumableStartId;
    private int _miscStartId;

    public void Init()
    {
        _equipmentStartId = Managers.Config.GetInt(ConfigType.EquipmentStartId);
        _consumableStartId = Managers.Config.GetInt(ConfigType.ConsumableStartId);
        _miscStartId = Managers.Config.GetInt(ConfigType.MiscStartId);

        PreloadCurrencySprites();
        PreloadEquipmentSprites();
        PreloadConsumableSprites();
        PreloadMiscSprites();
    }

    private void PreloadCurrencySprites()
    {
        foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
        {
            if (type == CurrencyType.None)
                continue;

            string path = $"{CurrencyBasePath}/{type}";
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                _currencySprites[type] = sprite;
            }
            else
            {
                Debug.LogWarning($"[ImageManager] Currency 스프라이트 없음: {path}");
            }
        }
    }

    private void PreloadEquipmentSprites()
    {
        // Equipment/{EquipmentSlotType}/{EquipmentType} 구조이므로
        // 두 Enum의 모든 조합을 시도하고 존재하는 것만 캐싱합니다.
        foreach (EquipmentSlotType slotType in Enum.GetValues(typeof(EquipmentSlotType)))
        {
            if (slotType == EquipmentSlotType.None)
                continue;

            foreach (EquipmentType equipType in Enum.GetValues(typeof(EquipmentType)))
            {
                if (equipType == EquipmentType.None)
                    continue;

                string path = $"{EquipmentBasePath}/{slotType}/{equipType}";
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite != null)
                {
                    _equipmentSprites[equipType] = sprite;
                }
            }
        }
    }

    private void PreloadConsumableSprites()
    {
        foreach (ConsumableType type in Enum.GetValues(typeof(ConsumableType)))
        {
            if (type == ConsumableType.None)
                continue;

            string path = $"{ConsumableBasePath}/{type}";
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                _consumableSprites[type] = sprite;
            }
            else
            {
                Debug.LogWarning($"[ImageManager] Consumable 스프라이트 없음: {path}");
            }
        }
    }

    private void PreloadMiscSprites()
    {
        foreach (MiscType type in Enum.GetValues(typeof(MiscType)))
        {
            if (type == MiscType.None)
                continue;

            string path = $"{MiscBasePath}/{type}";
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                _miscSprites[type] = sprite;
            }
            else
            {
                Debug.LogWarning($"[ImageManager] Misc 스프라이트 없음: {path}");
            }
        }
    }

    public Sprite GetCurrencySprite(CurrencyType currencyType)
    {
        if (_currencySprites.TryGetValue(currencyType, out Sprite sprite))
            return sprite;

        Debug.LogWarning($"[ImageManager] Currency 스프라이트 캐시 없음: {currencyType}");
        return null;
    }

    public Sprite GetEquipmentSprite(EquipmentType equipmentType)
    {
        if (_equipmentSprites.TryGetValue(equipmentType, out Sprite sprite))
            return sprite;

        Debug.LogWarning($"[ImageManager] Equipment 스프라이트 캐시 없음: {equipmentType}");
        return null;
    }

    public Sprite GetConsumableSprite(ConsumableType consumableType)
    {
        if (_consumableSprites.TryGetValue(consumableType, out Sprite sprite))
            return sprite;

        Debug.LogWarning($"[ImageManager] Consumable 스프라이트 캐시 없음: {consumableType}");
        return null;
    }

    public Sprite GetMiscSprite(MiscType miscType)
    {
        if (_miscSprites.TryGetValue(miscType, out Sprite sprite))
            return sprite;

        Debug.LogWarning($"[ImageManager] Misc 스프라이트 캐시 없음: {miscType}");
        return null;
    }

    // ID 범위: Currency 1~99999 / Equipment 100000~199999 / Consumable 200000~299999 / Misc 300000~
    public Sprite GetAssetImage(int assetId)
    {
        if (assetId <= 0)
            return null;

        if (assetId < _equipmentStartId)
        {
            CurrencyMetaData meta = Managers.SpecData.GetCurrency(assetId);
            if (meta == null)
            {
                Debug.LogWarning($"[ImageManager] CurrencyMetaData 없음. id={assetId}");
                return null;
            }
            return GetCurrencySprite(meta.CurrencyType);
        }
        else if (assetId < _consumableStartId)
        {
            EquipmentMetaData meta = Managers.SpecData.GetEquipment(assetId);
            if (meta == null)
            {
                Debug.LogWarning($"[ImageManager] EquipmentMetaData 없음. id={assetId}");
                return null;
            }
            return GetEquipmentSprite(meta.EquipmentType);
        }
        else if (assetId < _miscStartId)
        {
            ConsumableMetaData meta = Managers.SpecData.GetConsumable(assetId);
            if (meta == null)
            {
                Debug.LogWarning($"[ImageManager] ConsumableMetaData 없음. id={assetId}");
                return null;
            }
            return GetConsumableSprite(meta.ConsumableType);
        }
        else
        {
            MiscMetaData meta = Managers.SpecData.GetMisc(assetId);
            if (meta == null)
            {
                Debug.LogWarning($"[ImageManager] MiscMetaData 없음. id={assetId}");
                return null;
            }
            return GetMiscSprite(meta.MiscType);
        }
    }

    public Sprite GetCurrencyImage(int currencyId)
    {
        CurrencyMetaData meta = Managers.SpecData.GetCurrency(currencyId);
        if (meta == null)
        {
            Debug.LogWarning($"[ImageManager] CurrencyMetaData 없음. id={currencyId}");
            return null;
        }
        return GetCurrencySprite(meta.CurrencyType);
    }

    public Sprite GetCurrenyImage(CurrencyType currencyType)
    {
        return GetCurrencySprite(currencyType);
    }

    public Sprite GetQuestImage(int mainQuestId, int subQuestId)
    {
        // TODO - 퀘스트 이미지 연동
        return null;
    }
}

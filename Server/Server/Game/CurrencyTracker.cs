using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Game
{
    // QuestTracker/InventoryTracker와 동일한 더티 마킹 전략.
    // 재화 변경은 인메모리만 수행하고, 로그아웃 시 일괄 DB 반영.
    // 새 재화 추가 시 PlayerDb에 같은 이름의 속성만 추가하면 Load()가 자동으로 로드함.
    public class CurrencyTracker
    {
        private readonly Player _owner;

        // CurrencyType -> 현재 잔액 (인메모리 원본 데이터)
        private readonly Dictionary<CurrencyType, int> _currencies = new();

        // 인메모리에서만 변경되고 DB에 아직 반영 안 된 재화 타입 목록
        private readonly HashSet<CurrencyType> _dirtyCurrencies = new();

        public CurrencyTracker(Player owner)
        {
            _owner = owner;
        }

        // 로그인 시 DB에서 재화 목록을 로드해 인메모리 동기화
        public void Load()
        {
            _currencies.Clear();
            _dirtyCurrencies.Clear();

            if (_owner.PlayerId == 0)
                return;

            using GameDbContext db = new GameDbContext();
            PlayerDb playerDb = db.Players
                .AsNoTracking()
                .FirstOrDefault(p => p.PlayerDbId == _owner.PlayerId);

            if (playerDb == null)
                return;

            // DbTransaction의 expression tree 캐시를 활용해 PlayerDb 속성을 자동으로 로드
            // 명명 규칙: CurrencyType 이름 == PlayerDb 속성 이름
            foreach (CurrencyType currencyType in Enum.GetValues(typeof(CurrencyType)))
            {
                if (currencyType == CurrencyType.None)
                    continue;

                try
                {
                    int value = DbTransaction.GetCurrencyValue(playerDb, currencyType);
                    _currencies[currencyType] = value;
                }
                catch (InvalidOperationException ex)
                {
                    ConsoleLogManager.Instance.Log($"Failed to load currency {currencyType} for player {playerDb.PlayerDbId}: {ex.Message}");
                }
            }
        }

        // 인메모리 재화 조회
        public int Get(CurrencyType currencyType)
        {
            return _currencies.TryGetValue(currencyType, out int value) ? value : 0;
        }

        // 인메모리 재화 변경 + dirty 마킹
        public void Set(CurrencyType currencyType, int amount)
        {
            _currencies[currencyType] = amount;
            _dirtyCurrencies.Add(currencyType);
        }

        // 로그아웃 시 호출 - 변경 사항을 DB에 일괄 저장
        public void SaveDirtyCurrencies()
        {
            if (_dirtyCurrencies.Count == 0)
                return;

            // 메모리에서 변경된 재화들만 골라내어, 현재의 최신 값과 묶어서 리스트로 만드는 작업
            List<(CurrencyType, int)> toSave = _dirtyCurrencies
                .Select(ct => (ct, _currencies.TryGetValue(ct, out int v) ? v : 0))
                .ToList();

            _dirtyCurrencies.Clear();

            DbTransaction.SavePlayerCurrenciesAsync(_owner, toSave);
        }
    }
}

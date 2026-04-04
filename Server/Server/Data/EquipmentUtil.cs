using Google.Protobuf.Protocol;
using System.Collections.Generic;

namespace Server.Data
{
    // MetaData 관련 범용 유틸리티 메서드 모음
    public static class EquipmentUtil
    {
        // 비트 레이아웃: 1 << (int)PlayerJobType
        private static readonly Dictionary<int, int> _equipmentJobBitmask = new Dictionary<int, int>();

        // SpecDataManager.LoadData()에서 장비 데이터 로드 직후 한 번 호출
        public static void Initialize()
        {
            List<EquipmentMetaData> equipments = SpecDataManager.Instance.GetAllEquipment();
            _equipmentJobBitmask.Clear();
            foreach (EquipmentMetaData equip in equipments)
            {
                int bitmask = 0;
                if (equip.RequiredPlayerJob != null)
                {
                    foreach (PlayerJobType job in equip.RequiredPlayerJob)
                    {
                        bitmask |= 1 << (int)job;
                    }
                }
                _equipmentJobBitmask[equip.Id] = bitmask;
            }
        }

        // itemId 기준 O(1) 직업 조건 검사
        public static bool HasRequiredJob(int itemId, PlayerJobType jobType)
        {
            if (!_equipmentJobBitmask.TryGetValue(itemId, out int bitmask) || bitmask == 0)
                return true;

            return (bitmask & (1 << (int)jobType)) != 0;
        }
    }
}

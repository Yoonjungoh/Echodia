using Google.Protobuf.Protocol;
using System.Collections.Generic;

public class EquipmentManager
{
    // equipmentId -> 직업 조건 비트마스크 (0 = 제한 없음)
    private readonly Dictionary<int, int> _equipmentJobBitmask = new Dictionary<int, int>();
    public void Init()
    {
        _equipmentJobBitmask.Clear();

        foreach (EquipmentMetaData equip in Managers.SpecData.GetAllEquipment())
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

    public bool CanEquip(int equipmentId)
    {
        EquipmentMetaData equipmentMetaData = Managers.SpecData.GetEquipment(equipmentId);
        if (equipmentMetaData == null)
            return false;

        return CanEquipByLevel(equipmentMetaData.RequiredLevel) && CanEquipByJob(equipmentId);
    }

    public bool CanEquipByLevel(int requiredLevel)
    {
        return Managers.GameRoomObject.MyPlayer.Level >= requiredLevel;
    }

    public bool CanEquipByJob(int equipmentId)
    {
        if (!_equipmentJobBitmask.TryGetValue(equipmentId, out int bitmask) || bitmask == 0)
            return true;

        return (bitmask & (1 << (int)Managers.GameRoomObject.MyPlayer.Stat.JobType)) != 0;
    }
}

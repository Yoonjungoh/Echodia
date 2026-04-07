using System.Collections.Generic;
using System.Numerics;

namespace Server
{
    /// <summary>
    /// Common/MapData/{mapName}_meta.json 을 역직렬화한 데이터.
    /// MapManager가 바이너리 맵 로드 시 같이 읽는다.
    /// </summary>
    public class MapMetaData
    {
        public SpawnPointData EnterPoint { get; set; }
        public SpawnPointData LeavePoint { get; set; }
        public List<MonsterSpawnerData> MonsterSpawners { get; set; } = new();
    }

    /// <summary>
    /// 스폰 위치 + Y축 회전값.
    /// </summary>
    public class SpawnPointData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float RotY { get; set; }  // Y축 오일러 각도 (도 단위)

        public Vector3 Position => new Vector3(X, Y, Z);
    }

    /// <summary>
    /// 씬에 배치된 MonsterSpawner 컴포넌트 하나에 대응.
    /// </summary>
    public class MonsterSpawnerData
    {
        public int MonsterTypeId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float RotY { get; set; }
        public int Count { get; set; }
        public float RespawnSeconds { get; set; }
        public float SpawnRadius { get; set; }

        public Vector3 Position => new Vector3(X, Y, Z);
    }
}

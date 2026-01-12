using System;
using System.IO;
using System.Numerics;

namespace Server
{
    public class MapData
    {
        public float CellSize;
        public Vector3 Origin;
        public int SizeX;
        public int SizeZ;

        public float[,] Height;  // float 로 유지
        public bool[,] CanGo;
        
        public int MinX { get { return -(SizeX / 2); } }

        public int MaxX { get { return (SizeX / 2); } }

        public int MinZ { get { return -(SizeZ / 2); } }

        public int MaxZ { get { return (SizeZ / 2); } }
    }

    public class MapManager
    {
        private MapData _globalMap;
        public static MapManager Instance { get; } = new MapManager();

        public void Init()
        {
            string path = GetMapPath();
            if (!File.Exists(path))
            {
                Console.WriteLine("[MapManager] Map file not found.");
                return;
            }

            _globalMap = Load(path);
            Console.WriteLine($"[MapManager] Loaded map: {_globalMap.SizeX} x {_globalMap.SizeZ}");
        }

        public MapData CreateCopy()
        {
            if (_globalMap == null)
                return null;

            MapData copy = new MapData();
            copy.CellSize = _globalMap.CellSize;
            copy.Origin = _globalMap.Origin;
            copy.SizeX = _globalMap.SizeX;
            copy.SizeZ = _globalMap.SizeZ;

            copy.Height = (float[,])_globalMap.Height.Clone();
            copy.CanGo = (bool[,])_globalMap.CanGo.Clone();

            return copy;
        }

        private string GetMapPath()
        {
            string exeDir = AppContext.BaseDirectory;
            string root = Path.GetFullPath(Path.Combine(exeDir, @"..\..\..\..\.."));
            return Path.Combine(root, "Common", "MapData", "MapData_001.bytes");
        }

        private MapData Load(string filePath)
        {
            MapData map = new MapData();

            using (BinaryReader br = new BinaryReader(File.OpenRead(filePath)))
            {
                map.CellSize = br.ReadSingle();
                float ox = br.ReadSingle();
                float oy = br.ReadSingle();
                float oz = br.ReadSingle();
                map.Origin = new Vector3(ox, oy, oz);

                map.SizeX = br.ReadInt32();
                map.SizeZ = br.ReadInt32();

                int sx = map.SizeX;
                int sz = map.SizeZ;
                int total = sx * sz;

                map.Height = new float[sx, sz];
                map.CanGo = new bool[sx, sz];

                // Height 디코딩 (ushort -> float)
                for (int x = 0; x < sx; x++)
                    for (int z = 0; z < sz; z++)
                    {
                        ushort encoded = br.ReadUInt16();

                        if (encoded == 0)
                            map.Height[x, z] = -9999;
                        else
                            map.Height[x, z] = encoded / 100f - 100f;
                    }

                // CanGo 비트 언패킹
                int byteCount = (total + 7) / 8;
                byte[] packed = br.ReadBytes(byteCount);

                int idx = 0;
                for (int x = 0; x < sx; x++)
                    for (int z = 0; z < sz; z++)
                    {
                        int byteIndex = idx >> 3;
                        int bitIndex = idx & 7;

                        bool canGo = ((packed[byteIndex] >> bitIndex) & 1) == 1;
                        map.CanGo[x, z] = canGo;

                        idx++;
                    }
            }

            return map;
        }
    }
}

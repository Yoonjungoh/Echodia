using ServerCore;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Server.Game.Room
{
    public struct Cell
    {
        public int X;
        public int Y;
        public Cell(int x, int y) { X = x; Y = y; }
    }

    public struct Pos
    {
        public int Y;
        public int X;
        public Pos(int y, int x) { Y = y; X = x; }
    }

    public struct PQNode : IComparable<PQNode>
    {
        public int F;
        public int G;
        public int Y;
        public int X;

        public int CompareTo(PQNode other)
        {
            if (F == other.F) return 0;
            return F < other.F ? 1 : -1;
        }
    }

    public class Map
    {
        public MapData MapData { get; set; }
        public const float NO_HEIGHT_VALUE = -9999f;

        // 8방향
        int[] _deltaY = new int[] { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] _deltaX = new int[] { 0, 0, -1, 1, 1, -1, 1, -1 };

        // 4방향 = 10, 대각선 = 14
        int[] _cost = new int[] { 10, 10, 10, 10, 14, 14, 14, 14 };

        public int SizeX => MapData.SizeX;
        public int SizeZ => MapData.SizeZ;

        public Cell WorldToCell(Vector3 w)
        {
            int x = (int)((w.X - MapData.Origin.X) / MapData.CellSize);
            int y = (int)((w.Z - MapData.Origin.Z) / MapData.CellSize);
            return new Cell(x, y);
        }

        public Vector3 CellToWorld(Cell c)
        {
            float wx = MapData.Origin.X + c.X * MapData.CellSize + MapData.CellSize * 0.5f;
            float wz = MapData.Origin.Z + c.Y * MapData.CellSize + MapData.CellSize * 0.5f;
            float wy = GetCellHeight(c);
            return new Vector3(wx, wy, wz);
        }

        public bool IsValidCell(Cell c)
        {
            return (c.X >= 0 && c.Y >= 0 && c.X < MapData.SizeX && c.Y < MapData.SizeZ);
        }

        public float GetCellHeight(Cell c)
        {
            if (!IsValidCell(c))
                return NO_HEIGHT_VALUE;

            return MapData.Height[c.X, c.Y];
        }

        public bool CanGo(Cell c)
        {
            if (!IsValidCell(c))
                return false;
            return MapData.CanGo[c.X, c.Y];
        }

        public bool CanGo(float worldX, float worldZ)
        {
            Cell c = WorldToCell(new Vector3(worldX, 0, worldZ));
            return CanGo(c);
        }

        public float GetHeight(Vector3 worldPos)
        {
            int x = (int)((worldPos.X - MapData.Origin.X) / MapData.CellSize);
            int z = (int)((worldPos.Z - MapData.Origin.Z) / MapData.CellSize);

            if (x < 0 || x >= SizeX || z < 0 || z >= SizeZ)
                return NO_HEIGHT_VALUE;

            return MapData.Height[x, z];
        }

        public List<Vector3> FindPath(Vector3 startWorld, Vector3 destWorld)
        {
            Cell start = WorldToCell(startWorld);
            Cell dest = WorldToCell(destWorld);

            return ConvertToWorldPath(InternalFindPath(start, dest));
        }

        private List<Pos> InternalFindPath(Cell start, Cell dest, int maxDist = 50)
        {
            HashSet<Pos> closeList = new HashSet<Pos>();
            Dictionary<Pos, int> openList = new Dictionary<Pos, int>();
            Dictionary<Pos, Pos> parent = new Dictionary<Pos, Pos>();
            PriorityQueue<PQNode> pq = new PriorityQueue<PQNode>();

            Pos pos = new Pos(start.Y, start.X);
            Pos des = new Pos(dest.Y, dest.X);

            int hStart = 10 * (Math.Abs(des.Y - pos.Y) + Math.Abs(des.X - pos.X));
            openList[pos] = hStart;

            pq.Push(new PQNode() { F = hStart, G = 0, Y = pos.Y, X = pos.X });
            parent[pos] = pos;

            while (pq.Count > 0)
            {
                PQNode node = pq.Pop();
                Pos cur = new Pos(node.Y, node.X);

                if (closeList.Contains(cur))
                    continue;

                closeList.Add(cur);

                if (cur.Y == des.Y && cur.X == des.X)
                    break;

                for (int i = 0; i < 8; i++)
                {
                    int ny = cur.Y + _deltaY[i];
                    int nx = cur.X + _deltaX[i];

                    Pos next = new Pos(ny, nx);
                    Cell nextCell = new Cell(nx, ny);

                    // 너무 멀면 스킵
                    if (MathF.Abs(pos.X - next.X) + MathF.Abs(pos.Y - next.Y) > maxDist)
                        continue;

                    if (!IsValidCell(nextCell))
                        continue;

                    if (!CanGo(nextCell))
                        continue;

                    if (closeList.Contains(next))
                        continue;

                    int g = node.G + _cost[i];
                    int h = 10 * (Math.Abs(des.Y - ny) + Math.Abs(des.X - nx));
                    int f = g + h;

                    if (openList.TryGetValue(next, out int oldF) && oldF <= f)
                        continue;

                    openList[next] = f;
                    parent[next] = cur;

                    pq.Push(new PQNode() { F = f, G = g, Y = ny, X = nx });
                }
            }

            return BuildPath(parent, pos, des);
        }

        private List<Pos> BuildPath(Dictionary<Pos, Pos> parent, Pos start, Pos dest)
        {
            List<Pos> path = new List<Pos>();
            Pos cur = dest;

            while (!(cur.X == start.X && cur.Y == start.Y))
            {
                path.Add(cur);

                if (!parent.TryGetValue(cur, out Pos p))
                    break;

                if (p.X == cur.X && p.Y == cur.Y)
                    break;

                cur = p;
            }

            path.Add(start);
            path.Reverse();
            return path;
        }

        private List<Vector3> ConvertToWorldPath(List<Pos> raw)
        {
            List<Vector3> result = new List<Vector3>();
            foreach (Pos p in raw)
            {
                Cell c = new Cell(p.X, p.Y);
                result.Add(CellToWorld(c));
            }
            return result;
        }
    }
}

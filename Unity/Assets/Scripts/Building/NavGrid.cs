using System;
using System.Collections.Generic;

namespace Vacancy
{
    public sealed class NavGrid
    {
        public string FloorId;
        public int Level;
        public float Tile;
        public float OriginX;
        public float OriginY;
        public int Cols;
        public int Rows;
        public byte[] Blocked;
        public string[] Owner;
        public float[] Cost;
        public float[] Clearance;
    }

    public struct Cell
    {
        public int Col;
        public int Row;

        public Cell(int col, int row)
        {
            Col = col;
            Row = row;
        }
    }

    public static class Navigation
    {
        const float StraightCost = 10f;
        const float DiagonalCost = 14f;

        public static NavGrid Build(BuiltFloor floor, int level = 0, Rect? bounds = null)
        {
            float tile = floor.Tile;
            var used = bounds ?? floor.Bounds;
            if (used.W <= 0f || used.H <= 0f) used = floor.Bounds;
            int cols = (int)Math.Ceiling(used.W / tile);
            int rows = (int)Math.Ceiling(used.H / tile);
            var grid = new NavGrid
            {
                FloorId = level < 0 ? "basement" : floor.Id,
                Level = level,
                Tile = tile,
                OriginX = used.X,
                OriginY = used.Y,
                Cols = cols,
                Rows = rows,
                Blocked = new byte[cols * rows],
                Owner = new string[cols * rows],
                Cost = new float[cols * rows]
            };

            for (int i = 0; i < grid.Blocked.Length; i++)
            {
                grid.Blocked[i] = 1;
                grid.Cost[i] = 1f;
            }

            foreach (var area in floor.Areas)
            {
                if (area.Level != level) continue;
                StampArea(grid, area);
            }

            foreach (var area in floor.Areas)
            {
                if (area.Level != level) continue;
                if (area.Doors == null) continue;
                foreach (var door in area.Doors) CarveDoor(grid, area, door);
            }

            if (level >= 0) SealEnvelope(grid, floor);

            grid.Clearance = ComputeClearance(grid);
            return grid;
        }

        static float[] ComputeClearance(NavGrid grid)
        {
            int cols = grid.Cols;
            int rows = grid.Rows;
            var dist = new float[cols * rows];
            const float orth = 1f;
            float diag = (float)Math.Sqrt(2);

            for (int i = 0; i < dist.Length; i++)
            {
                dist[i] = grid.Blocked[i] != 0 ? 0f : float.PositiveInfinity;
            }

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int i = row * cols + col;
                    if (dist[i] == 0f) continue;
                    float best = dist[i];
                    if (col > 0) best = Math.Min(best, dist[i - 1] + orth);
                    if (row > 0) best = Math.Min(best, dist[i - cols] + orth);
                    if (row > 0 && col > 0) best = Math.Min(best, dist[i - cols - 1] + diag);
                    if (row > 0 && col < cols - 1) best = Math.Min(best, dist[i - cols + 1] + diag);
                    dist[i] = best;
                }
            }

            for (int row = rows - 1; row >= 0; row--)
            {
                for (int col = cols - 1; col >= 0; col--)
                {
                    int i = row * cols + col;
                    if (dist[i] == 0f) continue;
                    float best = dist[i];
                    if (col < cols - 1) best = Math.Min(best, dist[i + 1] + orth);
                    if (row < rows - 1) best = Math.Min(best, dist[i + cols] + orth);
                    if (row < rows - 1 && col < cols - 1) best = Math.Min(best, dist[i + cols + 1] + diag);
                    if (row < rows - 1 && col > 0) best = Math.Min(best, dist[i + cols - 1] + diag);
                    dist[i] = best;
                }
            }

            return dist;
        }

        public static float CellClearancePx(NavGrid grid, int col, int row)
        {
            if (grid.Clearance == null) return float.PositiveInfinity;
            float d = grid.Clearance[row * grid.Cols + col];
            if (float.IsInfinity(d)) return float.PositiveInfinity;
            return (d - 0.5f) * grid.Tile;
        }

        static void AreaCellRange(NavGrid grid, Rect rect, out int c0, out int r0, out int c1, out int r1)
        {
            c0 = (int)Math.Floor((rect.X - grid.OriginX) / grid.Tile);
            r0 = (int)Math.Floor((rect.Y - grid.OriginY) / grid.Tile);
            c1 = (int)Math.Ceiling((rect.X + rect.W - grid.OriginX) / grid.Tile) - 1;
            r1 = (int)Math.Ceiling((rect.Y + rect.H - grid.OriginY) / grid.Tile) - 1;
        }

        static void StampArea(NavGrid grid, FloorArea area)
        {
            AreaCellRange(grid, area.Rect, out int c0, out int r0, out int c1, out int r1);
            for (int row = r0; row <= r1; row++)
            {
                for (int col = c0; col <= c1; col++)
                {
                    if (col < 0 || row < 0 || col >= grid.Cols || row >= grid.Rows) continue;
                    int i = row * grid.Cols + col;
                    bool onRing = area.Walls && (col == c0 || col == c1 || row == r0 || row == r1);
                    if (onRing)
                    {
                        grid.Blocked[i] = 1;
                        grid.Owner[i] = null;
                        grid.Cost[i] = 1f;
                    }
                    else
                    {
                        grid.Blocked[i] = 0;
                        grid.Owner[i] = area.Token;
                        grid.Cost[i] = KindCost(area.Kind);
                    }
                }
            }
        }

        static float KindCost(string kind)
        {
            if (kind == AreaKind.Parking) return 1.4f;
            return 1f;
        }

        static float StepCost(NavGrid grid, int col, int row, bool diagonal)
        {
            float baseCost = diagonal ? DiagonalCost : StraightCost;
            if (grid.Cost == null) return baseCost;
            return baseCost * grid.Cost[row * grid.Cols + col];
        }

        static void SealEnvelope(NavGrid grid, BuiltFloor floor)
        {
            if (floor.OutdoorCourt)
            {
                SealMotorCourtHinterland(grid, floor);
                return;
            }

            var content = floor.Content;
            if (content.W <= 0f || content.H <= 0f) return;

            AreaCellRange(grid, content, out int c0, out int r0, out int c1, out int r1);

            void Block(int col, int row)
            {
                BlockCell(grid, col, row);
            }

            // North, west, and east walls turn the room-wing halls into indoor
            // space. Leave the south open so the lot still meets the service
            // corridor and lobby doors.
            for (int col = c0; col <= c1; col++) Block(col, r0);
            for (int row = r0; row <= r1; row++)
            {
                Block(c0, row);
                Block(c1, row);
            }
        }

        static void BlockCell(NavGrid grid, int col, int row)
        {
            if (col < 0 || row < 0 || col >= grid.Cols || row >= grid.Rows) return;
            int i = row * grid.Cols + col;
            grid.Blocked[i] = 1;
            grid.Owner[i] = null;
            if (grid.Cost != null) grid.Cost[i] = 1f;
        }

        static void SealMotorCourtHinterland(NavGrid grid, BuiltFloor floor)
        {
            // Dirt wrapping the outer L is inside the nav bounds (lobby grew
            // west; the lot slab is larger still). Leave it blocked so A* cannot
            // send guests around the back of the west/north wings.
            float westFace = floor.CornerMass.W > 0f ? floor.CornerMass.X : floor.Lobby.X;
            float northFace = floor.CornerMass.W > 0f ? floor.CornerMass.Y : floor.Lobby.Y;
            if (floor.Rooms != null)
            {
                foreach (var room in floor.Rooms)
                {
                    if (room.DoorSide == "east") westFace = Math.Min(westFace, room.Rect.X);
                    if (room.DoorSide == "south") northFace = Math.Min(northFace, room.Rect.Y);
                }
            }

            float lobbyNorth = floor.Lobby.Y;
            for (int row = 0; row < grid.Rows; row++)
            {
                for (int col = 0; col < grid.Cols; col++)
                {
                    var center = CellCenter(grid, col, row);
                    if (floor.CornerMass.W > 0f && floor.CornerMass.Contains(center.X, center.Y))
                    {
                        BlockCell(grid, col, row);
                        continue;
                    }

                    bool behindWest = center.X < westFace && center.Y < lobbyNorth;
                    bool behindNorth = center.Y < northFace;
                    if (behindWest || behindNorth) BlockCell(grid, col, row);
                }
            }
        }

        public static bool PointBehindWings(BuiltFloor floor, float x, float y)
        {
            if (floor == null || !floor.OutdoorCourt) return false;
            float westFace = floor.CornerMass.W > 0f ? floor.CornerMass.X : floor.Lobby.X;
            float northFace = floor.CornerMass.W > 0f ? floor.CornerMass.Y : floor.Lobby.Y;
            if (floor.Rooms != null)
            {
                foreach (var room in floor.Rooms)
                {
                    if (room.DoorSide == "east") westFace = Math.Min(westFace, room.Rect.X);
                    if (room.DoorSide == "south") northFace = Math.Min(northFace, room.Rect.Y);
                }
            }

            if (y < northFace) return true;
            return x < westFace && y < floor.Lobby.Y;
        }

        static void CarveDoor(NavGrid grid, FloorArea area, Door door)
        {
            AreaCellRange(grid, area.Rect, out int c0, out int r0, out int c1, out int r1);
            float half = door.Width / 2f;

            void Open(int col, int row)
            {
                if (col < 0 || row < 0 || col >= grid.Cols || row >= grid.Rows) return;
                int i = row * grid.Cols + col;
                grid.Blocked[i] = 0;
                grid.Owner[i] = null;
                if (grid.Cost != null) grid.Cost[i] = 1f;
            }

            if (door.Side == "north" || door.Side == "south")
            {
                int row = door.Side == "north" ? r0 : r1;
                int from = (int)Math.Floor((door.Center.X - half - grid.OriginX) / grid.Tile);
                int to = (int)Math.Ceiling((door.Center.X + half - grid.OriginX) / grid.Tile) - 1;
                for (int col = from; col <= to; col++) Open(col, row);
                return;
            }

            int doorCol = door.Side == "west" ? c0 : c1;
            int fromY = (int)Math.Floor((door.Center.Y - half - grid.OriginY) / grid.Tile);
            int toY = (int)Math.Ceiling((door.Center.Y + half - grid.OriginY) / grid.Tile) - 1;
            for (int row = fromY; row <= toY; row++) Open(doorCol, row);
        }

        public static Cell WorldToCell(NavGrid grid, float x, float y)
        {
            return new Cell(
                (int)Math.Floor((x - grid.OriginX) / grid.Tile),
                (int)Math.Floor((y - grid.OriginY) / grid.Tile));
        }

        public static Point CellCenter(NavGrid grid, int col, int row)
        {
            return new Point(
                grid.OriginX + (col + 0.5f) * grid.Tile,
                grid.OriginY + (row + 0.5f) * grid.Tile);
        }

        public static bool IsCellOpen(NavGrid grid, int col, int row, HashSet<string> permits)
        {
            if (col < 0 || row < 0 || col >= grid.Cols || row >= grid.Rows) return false;
            int i = row * grid.Cols + col;
            if (grid.Blocked[i] != 0) return false;
            string token = grid.Owner[i];
            return token == null || (permits != null && permits.Contains(token));
        }

        public static bool IsCircleBlocked(NavGrid grid, float x, float y, float radius, HashSet<string> permits)
        {
            int minCol = (int)Math.Floor((x - radius - grid.OriginX) / grid.Tile);
            int maxCol = (int)Math.Floor((x + radius - grid.OriginX) / grid.Tile);
            int minRow = (int)Math.Floor((y - radius - grid.OriginY) / grid.Tile);
            int maxRow = (int)Math.Floor((y + radius - grid.OriginY) / grid.Tile);

            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    if (IsCellOpen(grid, col, row, permits)) continue;
                    float cellX = grid.OriginX + col * grid.Tile;
                    float cellY = grid.OriginY + row * grid.Tile;
                    float nearestX = Geometry.Clamp(x, cellX, cellX + grid.Tile);
                    float nearestY = Geometry.Clamp(y, cellY, cellY + grid.Tile);
                    float dx = x - nearestX;
                    float dy = y - nearestY;
                    if (dx * dx + dy * dy < radius * radius) return true;
                }
            }

            return false;
        }

        public static bool IsCellRoutable(NavGrid grid, int col, int row, HashSet<string> permits, float radius)
        {
            if (!IsCellOpen(grid, col, row, permits)) return false;
            if (!(radius > 0f)) return true;
            return CellClearancePx(grid, col, row) >= radius;
        }

        public static Cell? NearestOpenCell(NavGrid grid, float x, float y, HashSet<string> permits, float radius = 0f, int maxRings = 40)
        {
            var start = WorldToCell(grid, x, y);
            if (IsCellRoutable(grid, start.Col, start.Row, permits, radius)) return start;

            for (int ring = 1; ring <= maxRings; ring++)
            {
                Cell? best = null;
                float bestDist = float.PositiveInfinity;
                for (int dRow = -ring; dRow <= ring; dRow++)
                {
                    for (int dCol = -ring; dCol <= ring; dCol++)
                    {
                        if (Math.Abs(dRow) != ring && Math.Abs(dCol) != ring) continue;
                        int col = start.Col + dCol;
                        int row = start.Row + dRow;
                        if (!IsCellRoutable(grid, col, row, permits, radius)) continue;
                        var center = CellCenter(grid, col, row);
                        float dist = (center.X - x) * (center.X - x) + (center.Y - y) * (center.Y - y);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = new Cell(col, row);
                        }
                    }
                }

                if (best.HasValue) return best;
            }

            return null;
        }

        public static List<Cell> FindCellPath(NavGrid grid, Point from, Point to, HashSet<string> permits, float radius = 0f)
        {
            var startN = NearestOpenCell(grid, from.X, from.Y, permits, radius);
            var goalN = NearestOpenCell(grid, to.X, to.Y, permits, radius);
            if (!startN.HasValue || !goalN.HasValue) return null;

            var start = startN.Value;
            var goal = goalN.Value;
            int startIdx = start.Row * grid.Cols + start.Col;
            int goalIdx = goal.Row * grid.Cols + goal.Col;
            if (startIdx == goalIdx) return new List<Cell> { start };

            int total = grid.Cols * grid.Rows;
            var gScore = new double[total];
            var cameFrom = new int[total];
            var closed = new byte[total];
            for (int i = 0; i < total; i++)
            {
                gScore[i] = double.PositiveInfinity;
                cameFrom[i] = -1;
            }

            float Heuristic(int col, int row)
            {
                int dCol = Math.Abs(col - goal.Col);
                int dRow = Math.Abs(row - goal.Row);
                int diag = Math.Min(dCol, dRow);
                return DiagonalCost * diag + StraightCost * (dCol + dRow - 2 * diag);
            }

            var open = new MinHeap();
            gScore[startIdx] = 0;
            open.Push(startIdx, Heuristic(start.Col, start.Row));

            while (open.Size > 0)
            {
                int current = open.Pop();
                if (current == goalIdx) break;
                if (closed[current] != 0) continue;
                closed[current] = 1;

                int col = current % grid.Cols;
                int row = (current - col) / grid.Cols;

                for (int dRow = -1; dRow <= 1; dRow++)
                {
                    for (int dCol = -1; dCol <= 1; dCol++)
                    {
                        if (dCol == 0 && dRow == 0) continue;
                        int nCol = col + dCol;
                        int nRow = row + dRow;
                        if (!IsCellRoutable(grid, nCol, nRow, permits, radius)) continue;
                        if (dCol != 0 && dRow != 0)
                        {
                            if (!IsCellRoutable(grid, col + dCol, row, permits, radius)) continue;
                            if (!IsCellRoutable(grid, col, row + dRow, permits, radius)) continue;
                        }

                        int nIdx = nRow * grid.Cols + nCol;
                        if (closed[nIdx] != 0) continue;
                        double step = StepCost(grid, nCol, nRow, dCol != 0 && dRow != 0);
                        double tentative = gScore[current] + step;
                        if (tentative >= gScore[nIdx]) continue;
                        gScore[nIdx] = tentative;
                        cameFrom[nIdx] = current;
                        open.Push(nIdx, (float)(tentative + Heuristic(nCol, nRow)));
                    }
                }
            }

            if (cameFrom[goalIdx] == -1 && startIdx != goalIdx) return null;

            var cells = new List<Cell>();
            int at = goalIdx;
            int guard = 0;
            while (at != -1 && guard++ <= total)
            {
                int c = at % grid.Cols;
                cells.Add(new Cell(c, (at - c) / grid.Cols));
                if (at == startIdx) break;
                at = cameFrom[at];
            }

            cells.Reverse();
            return cells;
        }

        public static List<Point> FindRoute(NavGrid grid, Point from, Point to, HashSet<string> permits, float radius = 10f)
        {
            var cells = FindCellPath(grid, from, to, permits, radius);
            if (cells == null) return null;

            var points = new List<Point>();
            foreach (var cell in cells) points.Add(CellCenter(grid, cell.Col, cell.Row));
            if (!IsCircleBlocked(grid, to.X, to.Y, radius, permits))
            {
                points.Add(to);
            }

            return Straighten(grid, points, permits, radius);
        }

        static List<Point> Straighten(NavGrid grid, List<Point> points, HashSet<string> permits, float radius)
        {
            if (points.Count <= 2) return points;
            var result = new List<Point> { points[0] };
            int anchor = 0;
            for (int i = 2; i < points.Count; i++)
            {
                if (SegmentIsClear(grid, points[anchor], points[i], permits, radius)) continue;
                result.Add(points[i - 1]);
                anchor = i - 1;
            }

            result.Add(points[points.Count - 1]);
            return result;
        }

        public static bool SegmentIsClear(NavGrid grid, Point a, Point b, HashSet<string> permits, float radius)
        {
            float dist = Geometry.Dist(a, b);
            int steps = Math.Max(2, (int)Math.Ceiling(dist / (grid.Tile * 0.5f)));
            float probe = Math.Max(2f, radius);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float x = a.X + (b.X - a.X) * t;
                float y = a.Y + (b.Y - a.Y) * t;
                if (IsCircleBlocked(grid, x, y, probe, permits)) return false;
            }

            return true;
        }

        public static List<string> ValidateFloor(NavGrid grid, BuiltFloor floor, Point from)
        {
            var problems = new List<string>();
            foreach (var room in floor.Rooms)
            {
                if (IsCircleBlocked(grid, room.Approach.X, room.Approach.Y, 8f, null))
                {
                    problems.Add($"Room {room.Id} doorway is walled in");
                    continue;
                }

                if (FindRoute(grid, from, room.Approach, null, 12f) == null)
                {
                    problems.Add($"Room {room.Id} door is unreachable from the lobby");
                    continue;
                }

                var toDesk = FindRoute(grid, room.Approach, from, null, 12f);
                if (toDesk != null)
                {
                    foreach (var point in toDesk)
                    {
                        if (!PointBehindWings(floor, point.X, point.Y)) continue;
                        problems.Add($"Room {room.Id} checkout path goes behind the building");
                        break;
                    }
                }

                var permits = new HashSet<string> { $"room:{room.Id}" };
                if (FindRoute(grid, room.Approach, room.Center, permits, 12f) == null)
                {
                    problems.Add($"Room {room.Id} interior is unreachable from its door");
                }
            }

            if (floor.Office != null && FindRoute(grid, from, floor.Office.Approach, null, 12f) == null)
            {
                problems.Add("Office door is unreachable");
            }

            foreach (var dept in floor.Departments.Values)
            {
                if (DepartmentIsBasement(floor, dept)) continue;
                if (FindRoute(grid, from, new Point(dept.X, dept.Y), null, 12f) == null)
                {
                    problems.Add($"{dept.Label} is unreachable");
                }
            }

            return problems;
        }

        static bool DepartmentIsBasement(BuiltFloor floor, DepartmentSpot dept)
        {
            foreach (var area in floor.Areas)
            {
                if (area.DepartmentId == dept.Id) return area.Level < 0;
            }

            return false;
        }

        sealed class MinHeap
        {
            readonly List<int> items = new List<int>();
            readonly List<float> priorities = new List<float>();

            public int Size => items.Count;

            public void Push(int item, float priority)
            {
                items.Add(item);
                priorities.Add(priority);
                int i = items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (priorities[parent] <= priorities[i]) break;
                    Swap(parent, i);
                    i = parent;
                }
            }

            public int Pop()
            {
                int top = items[0];
                int lastItem = items[items.Count - 1];
                float lastPriority = priorities[priorities.Count - 1];
                items.RemoveAt(items.Count - 1);
                priorities.RemoveAt(priorities.Count - 1);
                if (items.Count > 0)
                {
                    items[0] = lastItem;
                    priorities[0] = lastPriority;
                    int i = 0;
                    while (true)
                    {
                        int left = i * 2 + 1;
                        int right = left + 1;
                        int smallest = i;
                        if (left < items.Count && priorities[left] < priorities[smallest]) smallest = left;
                        if (right < items.Count && priorities[right] < priorities[smallest]) smallest = right;
                        if (smallest == i) break;
                        Swap(i, smallest);
                        i = smallest;
                    }
                }

                return top;
            }

            void Swap(int a, int b)
            {
                int item = items[a];
                items[a] = items[b];
                items[b] = item;
                float p = priorities[a];
                priorities[a] = priorities[b];
                priorities[b] = p;
            }
        }
    }
}

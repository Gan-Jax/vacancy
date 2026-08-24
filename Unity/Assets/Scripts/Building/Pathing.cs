using System.Collections.Generic;

namespace Vacancy
{
    public static class Pathing
    {
        const float StallLimitSeconds = 0.5f;

        public static Rect GetRoomRect(Room room, HotelLayout layout)
        {
            if (layout.Rooms != null && room.Id >= 1 && room.Id <= layout.Rooms.Count)
            {
                return layout.Rooms[room.Id - 1].Rect;
            }

            var center = layout.RoomCenters[room.Id - 1];
            return new Rect(
                center.X - GameConfig.RoomWidth / 2f,
                center.Y - GameConfig.RoomHeight / 2f,
                GameConfig.RoomWidth,
                GameConfig.RoomHeight);
        }

        public static bool IsRoomBlocking(Room room)
        {
            return !room.Unlocked || room.Status == "occupied";
        }

        public static bool MayEnterRoom(Room room, object allowRoomId)
        {
            if (allowRoomId is string s && s == "player") return !IsRoomBlocking(room);
            if (allowRoomId is int roomId && room.Id == roomId) return true;
            return false;
        }

        public static HashSet<string> BuildPermits(List<Room> rooms, object allowRoomId)
        {
            var permits = new HashSet<string> { "office" };
            if (allowRoomId is string s && s == "player")
            {
                permits.Add("office");
                foreach (var room in rooms)
                {
                    if (!IsRoomBlocking(room)) permits.Add($"room:{room.Id}");
                }

                return permits;
            }

            if (allowRoomId is string office && office == "office")
            {
                permits.Add("office");
                return permits;
            }

            if (allowRoomId is int roomId)
            {
                permits.Add($"room:{roomId}");
            }

            return permits;
        }

        public static bool CollidesWithRooms(float x, float y, float radius, List<Room> rooms, HotelLayout layout, object allowRoomId, int floorLevel = 0)
        {
            var grid = layout?.GridFor(floorLevel);
            if (grid == null) return false;
            return Navigation.IsCircleBlocked(grid, x, y, radius, BuildPermits(rooms, allowRoomId));
        }

        public static void ResolveRoomCollision(IMover entity, List<Room> rooms, HotelLayout layout, object allowRoomId)
        {
            var grid = layout?.GridFor(entity.FloorLevel);
            if (grid == null) return;
            var permits = BuildPermits(rooms, allowRoomId);
            if (!Navigation.IsCircleBlocked(grid, entity.X, entity.Y, entity.Radius, permits)) return;

            var cell = Navigation.NearestOpenCell(grid, entity.X, entity.Y, permits, entity.Radius);
            if (!cell.HasValue) return;
            var target = Navigation.CellCenter(grid, cell.Value.Col, cell.Value.Row);
            entity.X = target.X;
            entity.Y = target.Y;
        }

        public static List<Point> FindPath(HotelLayout layout, float fromX, float fromY, Point? goal, PathOptions options = null)
        {
            if (goal == null) return new List<Point>();
            options = options ?? new PathOptions();
            var permits = options.Permits ?? BuildPermits(options.Rooms ?? new List<Room>(), options.AllowRoomId);
            float radius = options.Radius ?? 11f;
            int fromFloor = options.FromFloor ?? 0;
            int toFloor = options.ToFloor ?? layout.GuessFloor(goal.Value);

            var route = FindRouteAcross(layout, new Point(fromX, fromY), goal.Value, fromFloor, toFloor, permits, radius);
            if (route == null || route.Count == 0) return new List<Point> { goal.Value };
            return route;
        }

        static List<Point> FindRouteAcross(
            HotelLayout layout,
            Point from,
            Point to,
            int fromFloor,
            int toFloor,
            HashSet<string> permits,
            float radius)
        {
            var fromGrid = layout.GridFor(fromFloor);
            var toGrid = layout.GridFor(toFloor);
            if (fromGrid == null) return new List<Point> { to };
            if (fromFloor == toFloor || fromGrid == toGrid)
            {
                return Navigation.FindRoute(fromGrid, from, to, permits, radius);
            }

            Point fromPortal = fromFloor < 0 ? layout.StairsBottom : layout.StairsTop;
            Point toPortal = toFloor < 0 ? layout.StairsBottom : layout.StairsTop;
            var first = Navigation.FindRoute(fromGrid, from, fromPortal, permits, radius);
            var second = Navigation.FindRoute(toGrid, toPortal, to, permits, radius);
            if (first == null || second == null) return null;

            var combined = new List<Point>(first.Count + second.Count);
            combined.AddRange(first);
            combined.AddRange(second);
            return combined;
        }

        public static List<Point> PathToRoomDoor(HotelLayout layout, float fromX, float fromY, int roomId, PathOptions options = null)
        {
            return FindPath(layout, fromX, fromY, layout.RoomDoor(roomId), options);
        }

        public static List<Point> PathToDeskHall(HotelLayout layout, float fromX, float fromY, PathOptions options = null)
        {
            options = options ?? new PathOptions();
            if (options.ToFloor == null) options.ToFloor = 0;
            return FindPath(layout, fromX, fromY, layout.DeskApproach(), options);
        }

        public static bool SteerTo(IMover entity, float tx, float ty, float dt, List<Room> rooms, HotelLayout layout, object allowRoomId, float speed)
        {
            float dx = tx - entity.X;
            float dy = ty - entity.Y;
            float dist = (float)System.Math.Sqrt(dx * dx + dy * dy);
            if (dist < 3f)
            {
                entity.X = tx;
                entity.Y = ty;
                layout?.UpdateElevation(entity);
                return true;
            }

            var grid = layout?.GridFor(entity.FloorLevel);
            var permits = BuildPermits(rooms, allowRoomId);
            float len = dist > 0 ? dist : 1f;
            float travel = System.Math.Min(speed * dt, dist);
            float dirX = (dx / len) * travel;
            float dirY = (dy / len) * travel;

            var attempts = new[]
            {
                new Point(dirX, dirY),
                new Point(dirX, 0),
                new Point(0, dirY)
            };

            foreach (var move in attempts)
            {
                if (move.X == 0 && move.Y == 0) continue;
                float nx = entity.X + move.X;
                float ny = entity.Y + move.Y;
                if (grid != null && Navigation.IsCircleBlocked(grid, nx, ny, entity.Radius, permits)) continue;
                entity.X = nx;
                entity.Y = ny;
                ClampToBuilding(entity, layout);
                layout?.UpdateElevation(entity);
                return Geometry.Dist(entity.X, entity.Y, tx, ty) < 3f;
            }

            ResolveRoomCollision(entity, rooms, layout, allowRoomId);
            layout?.UpdateElevation(entity);
            return false;
        }

        public static bool FollowPath(IMover entity, float dt, List<Room> rooms, HotelLayout layout, object allowRoomId, float speed)
        {
            if (entity.Path == null || entity.Path.Count == 0) return true;

            var target = entity.Path[0];
            float before = Geometry.Dist(entity.X, entity.Y, target.X, target.Y);
            SteerTo(entity, target.X, target.Y, dt, rooms, layout, allowRoomId, speed);
            float after = Geometry.Dist(entity.X, entity.Y, target.X, target.Y);

            if (after < before - 0.05f)
            {
                entity.StallSeconds = 0;
            }
            else
            {
                entity.StallSeconds += dt;
                if (entity.StallSeconds >= StallLimitSeconds)
                {
                    entity.StallSeconds = 0;
                    var goal = entity.Path[entity.Path.Count - 1];
                    entity.Path = FindPath(layout, entity.X, entity.Y, goal, new PathOptions
                    {
                        Rooms = rooms,
                        AllowRoomId = allowRoomId,
                        Radius = entity.Radius,
                        FromFloor = entity.FloorLevel
                    });
                    return false;
                }
            }

            float reach = entity.Path.Count == 1 ? 6f : 12f;
            if (Geometry.Dist(entity.X, entity.Y, target.X, target.Y) < reach)
            {
                entity.Path.RemoveAt(0);
                if (entity.Path.Count == 0) return true;
            }

            return false;
        }

        static void ClampToBuilding(IMover entity, HotelLayout layout)
        {
            var b = layout.WalkRect(entity.FloorLevel);
            entity.X = Geometry.Clamp(entity.X, b.X + 4, b.X + b.W - 4);
            entity.Y = Geometry.Clamp(entity.Y, b.Y + 4, b.Y + b.H - 4);
        }
    }

    public interface IMover
    {
        float X { get; set; }
        float Y { get; set; }
        float Radius { get; }
        List<Point> Path { get; set; }
        float StallSeconds { get; set; }
        int FloorLevel { get; set; }
        float FootY { get; set; }
    }

    public sealed class PathOptions
    {
        public HashSet<string> Permits;
        public List<Room> Rooms;
        public object AllowRoomId;
        public float? Radius;
        public int? FromFloor;
        public int? ToFloor;
    }
}

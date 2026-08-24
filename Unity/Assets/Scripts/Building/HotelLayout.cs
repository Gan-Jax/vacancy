using System.Collections.Generic;

namespace Vacancy
{
    public sealed class HotelLayout
    {
        public float Width;
        public float Height;
        public Rect Building;
        public Rect Parking;
        public Rect WalkBounds;
        public BuiltFloor Floor;
        public NavGrid NavGrid;
        public float Tile;
        public List<PlannedRoom> Rooms;
        public int RoomCount;
        public List<Point> RoomCenters;
        public int Cols;
        public Rect Lobby;
        public Rect Basement;
        public Rect Stairs;
        public OfficeSpot Office;
        public DeskSpot FrontDesk;
        public Dictionary<string, DepartmentSpot> Departments;
        public DeskSpot LobbyRadio;
        public DeskSpot Newspaper;
        public DeskSpot DeskPhone;
        public DeskSpot DeskPc;
        public DeskSpot VacancySign;
        public Point Spawn;
        public Point StairsTop;
        public Point StairsBottom;
        public NavGrid BasementGrid;

        readonly Dictionary<string, DepartmentSpot> deptForStaff = new Dictionary<string, DepartmentSpot>();

        public static HotelLayout Create(float width = 1360f, float height = 1100f)
        {
            var building = Floorplan.InnBuildingRect(Floorplan.FlagshipGround, width, height);
            var floor = Floorplan.CreateFloor(Floorplan.FlagshipGround, building);
            var parking = Floorplan.AttachParking(floor, building, height);
            var navGrid = Navigation.Build(floor);
            var basementGrid = Navigation.Build(floor, -1, floor.Basement.W > 0 ? floor.Basement : floor.Lobby);
            var desk = floor.FrontDesk;
            var stairs = floor.Stairs;
            var layout = new HotelLayout
            {
                Width = width,
                Height = height,
                Building = building,
                Parking = parking,
                WalkBounds = floor.Bounds,
                Floor = floor,
                NavGrid = navGrid,
                BasementGrid = basementGrid,
                Tile = floor.Tile,
                Rooms = floor.Rooms,
                RoomCount = floor.Rooms.Count,
                RoomCenters = new List<Point>(),
                Cols = floor.RoomsPerRow,
                Lobby = floor.Lobby,
                Basement = floor.Basement,
                Stairs = stairs,
                Office = floor.Office,
                FrontDesk = desk,
                Departments = floor.Departments,
                LobbyRadio = new DeskSpot { X = desk.X - desk.W / 2f - 26f, Y = desk.Y, W = 36, H = 28 },
                Newspaper = new DeskSpot { X = desk.X, Y = desk.Y + 2f, W = 28, H = 18 },
                DeskPhone = new DeskSpot { X = desk.X - desk.W / 2f + 20f, Y = desk.Y - 2f, W = 18, H = 16 },
                DeskPc = new DeskSpot { X = desk.X + desk.W / 2f - 22f, Y = desk.Y - 2f, W = 28, H = 18 },
                VacancySign = new DeskSpot
                {
                    X = parking.Center.X - 80f,
                    Y = parking.Y + parking.H - 48f,
                    W = 140,
                    H = 36
                },
                Spawn = new Point(desk.X + 50f, desk.Y + 70f),
                StairsTop = stairs.W > 0
                    ? new Point(stairs.X + stairs.W - floor.Tile * 2.5f, stairs.Y + stairs.H * 0.55f)
                    : new Point(desk.X, desk.Y),
                StairsBottom = stairs.W > 0
                    ? new Point(stairs.X + floor.Tile * 2.5f, stairs.Center.Y)
                    : new Point(desk.X, desk.Y)
            };

            foreach (var room in floor.Rooms) layout.RoomCenters.Add(room.Center);

            if (floor.Departments.TryGetValue("housekeeping", out var housekeeping))
            {
                layout.deptForStaff["mary"] = housekeeping;
                layout.deptForStaff["housekeeping"] = housekeeping;
            }

            if (floor.Departments.TryGetValue("maintenance", out var maintenance))
            {
                layout.deptForStaff["bob"] = maintenance;
                layout.deptForStaff["maintenance"] = maintenance;
            }

            return layout;
        }

        public Point RoomDoor(int roomId)
        {
            if (roomId < 1 || roomId > Rooms.Count) return DeskApproach();
            return Rooms[roomId - 1].Approach;
        }

        public Point RoomInterior(int roomId)
        {
            if (roomId < 1 || roomId > RoomCenters.Count) return DeskApproach();
            return RoomCenters[roomId - 1];
        }

        public Rect? RoomRect(int roomId)
        {
            if (roomId < 1 || roomId > Rooms.Count) return null;
            return Rooms[roomId - 1].Rect;
        }

        public Point DeskApproach()
        {
            return new Point(FrontDesk.X, FrontDesk.Y + FrontDesk.H / 2f + 36f);
        }

        public Point StaffHome(string key)
        {
            if (key != null && deptForStaff.TryGetValue(key, out var dept))
            {
                return new Point(dept.X, dept.Y);
            }

            if (Departments.TryGetValue("maintenance", out var maintenance))
            {
                return new Point(maintenance.X, maintenance.Y);
            }

            return DeskApproach();
        }

        public Point CheckInLineSlot(int index)
        {
            int col = index % 3;
            int row = index / 3;
            return new Point(
                FrontDesk.X - 30 + col * 30,
                FrontDesk.Y + FrontDesk.H / 2f + 32 + row * 28);
        }

        public Point CheckoutLineSlot(int index)
        {
            const int perRow = 8;
            int col = index % perRow;
            int row = System.Math.Min(1, index / perRow);
            return new Point(
                FrontDesk.X - FrontDesk.W / 2f - 36 - col * 38,
                FrontDesk.Y + 20 + row * 26);
        }

        public Point StaffPaySlot(string staffId)
        {
            float offset = staffId == "mary" ? 46 : 0;
            return new Point(FrontDesk.X - 30 + offset, FrontDesk.Y - FrontDesk.H / 2f - 22);
        }

        public NavGrid GridFor(int floorLevel)
        {
            if (floorLevel < 0 && BasementGrid != null) return BasementGrid;
            return NavGrid;
        }

        public Rect WalkRect(int floorLevel)
        {
            if (floorLevel < 0 && Basement.W > 0) return Basement;
            return WalkBounds.W > 0 ? WalkBounds : Building;
        }

        public bool InStairs(float x, float y)
        {
            return Stairs.W > 0 && Stairs.Contains(x, y, Tile * 0.2f);
        }

        public float StairFootY(float x, float y)
        {
            if (!InStairs(x, y) || Stairs.W <= Tile) return 0f;
            float t = Geometry.Clamp((x - Stairs.X) / Stairs.W, 0f, 1f);
            return -(1f - t) * WorldScale.FloorDepth;
        }

        public int GuessFloor(Point point)
        {
            if (Departments != null)
            {
                foreach (var dept in Departments.Values)
                {
                    if (dept.Rect.Contains(point.X, point.Y)) return -1;
                }
            }

            if (Floor != null)
            {
                foreach (var area in Floor.Areas)
                {
                    if (area.Level >= 0) continue;
                    if (area.Kind == AreaKind.Basement) continue;
                    if (area.Rect.Contains(point.X, point.Y)) return -1;
                }
            }

            return 0;
        }

        public void UpdateElevation(IMover entity)
        {
            if (entity == null) return;
            if (InStairs(entity.X, entity.Y))
            {
                entity.FootY = StairFootY(entity.X, entity.Y);
                entity.FloorLevel = entity.FootY < -WorldScale.FloorDepth * 0.45f ? -1 : 0;
                return;
            }

            entity.FootY = entity.FloorLevel < 0 ? -WorldScale.FloorDepth : 0f;
        }

        public string AreaLabelAt(float x, float y, int floorLevel)
        {
            string best = floorLevel < 0 ? "basement" : "lot";
            float bestArea = float.PositiveInfinity;

            void Consider(string label, Rect rect)
            {
                if (rect.W <= 0 || !rect.Contains(x, y)) return;
                float area = rect.W * rect.H;
                if (area >= bestArea) return;
                bestArea = area;
                best = label;
            }

            if (floorLevel < 0)
            {
                Consider("basement", Basement.W > 0 ? Basement : Lobby);
            }

            if (Floor != null)
            {
                foreach (var area in Floor.Areas)
                {
                    if (area.Level != floorLevel) continue;
                    Consider(LabelForArea(area), area.Rect);
                }
            }

            Consider("stairs", Stairs);
            if (floorLevel >= 0)
            {
                if (Office != null) Consider("office", Office.Rect);
                Consider("desk", new Rect(FrontDesk.X - FrontDesk.W / 2f, FrontDesk.Y - FrontDesk.H / 2f, FrontDesk.W, FrontDesk.H));
                if (LobbyRadio != null)
                {
                    Consider("radio", new Rect(LobbyRadio.X - LobbyRadio.W / 2f, LobbyRadio.Y - LobbyRadio.H / 2f, LobbyRadio.W, LobbyRadio.H));
                }

                if (DeskPhone != null)
                {
                    Consider("phone", new Rect(DeskPhone.X - DeskPhone.W / 2f, DeskPhone.Y - DeskPhone.H / 2f, DeskPhone.W, DeskPhone.H));
                }

                if (DeskPc != null)
                {
                    Consider("desk PC", new Rect(DeskPc.X - DeskPc.W / 2f, DeskPc.Y - DeskPc.H / 2f, DeskPc.W, DeskPc.H));
                }

                if (VacancySign != null)
                {
                    Consider("vacancy sign", new Rect(VacancySign.X - VacancySign.W / 2f, VacancySign.Y - VacancySign.H / 2f, VacancySign.W, VacancySign.H));
                }

                Consider("parking", Parking);
                Consider("lobby", Lobby);
            }

            return best;
        }

        static string LabelForArea(FloorArea area)
        {
            if (!string.IsNullOrEmpty(area.Label)) return area.Label.ToLowerInvariant();
            if (area.RoomId != null) return $"room {area.RoomId}";
            return area.Kind ?? "area";
        }

        public static string FormatPin(string label, float x, float y)
        {
            return $"PIN {label} ({System.Math.Round(x)}, {System.Math.Round(y)}) world ({WorldScale.Meters(x):0.0}, {WorldScale.Meters(y):0.0})";
        }

        public List<string> Validate()
        {
            var problems = Navigation.ValidateFloor(NavGrid, Floor, DeskApproach());
            if (BasementGrid == null || Stairs.W <= 0) return problems;

            var permits = new HashSet<string>();
            if (Navigation.FindRoute(NavGrid, DeskApproach(), StairsTop, permits, 12f) == null)
            {
                problems.Add("Basement stairs are unreachable from the lobby");
            }

            if (Navigation.FindRoute(BasementGrid, StairsBottom, StairsBottom, permits, 12f) == null)
            {
                problems.Add("Basement stair landing is blocked");
            }

            foreach (var dept in Departments.Values)
            {
                var home = new Point(dept.X, dept.Y);
                if (Navigation.FindRoute(BasementGrid, StairsBottom, home, permits, 12f) == null)
                {
                    problems.Add($"{dept.Label} is unreachable from the basement stairs");
                }
            }

            return problems;
        }
    }
}

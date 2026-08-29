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
        public Rect UpperStairs;
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
        public Point UpperStairsGround;
        public Point UpperStairsUpper;
        public NavGrid BasementGrid;
        public NavGrid UpperGrid;
        public Rect UpperWalk;
        public Rect DriveSouth;
        public Rect PorteCochere;

        public const int StallCount = 12;
        public const float ParkingDriveWidth = 90f;
        public const float ParkingStallDepth = 56f;
        public const float ParkingStallHeight = 54f;
        public const float ParkingStallGap = 10f;
        public const float ParkedCarWidth = 46f;
        public const float ParkedCarHeight = 22f;

        readonly Dictionary<string, DepartmentSpot> deptForStaff = new Dictionary<string, DepartmentSpot>();
        StallPose[] stalls;

        public static HotelLayout Create(float width = 0f, float height = 0f)
        {
            var floor = Floorplan.CreateMotorCourt();
            var parking = floor.Parking;
            if (width <= 0f) width = floor.Bounds.X + floor.Bounds.W + 120f;
            if (height <= 0f) height = floor.Bounds.Y + floor.Bounds.H + 100f;
            var building = floor.Bounds;
            var navGrid = Navigation.Build(floor);
            var basementGrid = Navigation.Build(floor, -1, floor.Basement.W > 0 ? floor.Basement : floor.Lobby);
            var upperGrid = Navigation.Build(floor, 1);
            var desk = floor.FrontDesk;
            var stairs = floor.Stairs;
            var upperStairs = floor.UpperStairs;
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
                UpperGrid = upperGrid,
                Tile = floor.Tile,
                Rooms = floor.Rooms,
                RoomCount = floor.Rooms.Count,
                RoomCenters = new List<Point>(),
                Cols = floor.RoomsPerRow,
                Lobby = floor.Lobby,
                Basement = floor.Basement,
                Stairs = stairs,
                UpperStairs = upperStairs,
                Office = floor.Office,
                FrontDesk = desk,
                Departments = floor.Departments,
                LobbyRadio = new DeskSpot { X = desk.X - desk.W / 2f - 26f, Y = desk.Y, W = 36, H = 28 },
                Newspaper = PlaceNewspaperBox(floor, navGrid),
                DeskPhone = new DeskSpot { X = desk.X - desk.W / 2f + 20f, Y = desk.Y - 2f, W = 18, H = 16 },
                DeskPc = new DeskSpot { X = desk.X + desk.W / 2f - 22f, Y = desk.Y - 2f, W = 28, H = 18 },
                DriveSouth = floor.DriveSouth,
                PorteCochere = floor.PorteCochere,
                VacancySign = PlaceVacancySign(floor, parking),
                Spawn = new Point(desk.X + 50f, desk.Y + 46f),
                StairsTop = stairs.W > 0
                    ? new Point(stairs.X + stairs.W - floor.Tile * 2.5f, stairs.Y + stairs.H * 0.55f)
                    : new Point(desk.X, desk.Y),
                StairsBottom = stairs.W > 0
                    ? new Point(stairs.X + floor.Tile * 2.5f, stairs.Center.Y)
                    : new Point(desk.X, desk.Y),
                UpperStairsGround = upperStairs.W > 0
                    ? new Point(upperStairs.Center.X, upperStairs.Y + upperStairs.H + floor.Tile * 2f)
                    : new Point(desk.X, desk.Y),
                UpperStairsUpper = upperStairs.W > 0
                    ? new Point(upperStairs.Center.X, upperStairs.Y - floor.Tile * 2f)
                    : new Point(desk.X, desk.Y)
            };

            foreach (var room in floor.Rooms) layout.RoomCenters.Add(room.Center);
            layout.UpperWalk = UpperWalkBounds(floor, upperStairs);
            layout.BuildStalls();

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

        public int RoomFloor(int roomId)
        {
            if (roomId < 1 || roomId > Rooms.Count) return 0;
            return Rooms[roomId - 1].Level;
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

        public Point NewspaperApproach()
        {
            if (Newspaper == null) return DeskApproach();
            return new Point(Newspaper.X, Newspaper.Y + Newspaper.H / 2f + 18f);
        }

        public Point WalkaboutSpot(int index)
        {
            int n = ((index % 3) + 3) % 3;
            if (n == 0) return NewspaperApproach();
            if (n == 1) return DeskApproach();
            if (Floor != null && Floor.WalkWest.W > 0f) return Floor.WalkWest.Center;
            if (Floor != null && Floor.WalkNorth.W > 0f) return Floor.WalkNorth.Center;
            return FrontEntrance;
        }

        static DeskSpot PlaceVacancySign(BuiltFloor floor, Rect parking)
        {
            const float size = 16f;
            if (floor == null || floor.DriveSouth.W <= 0f)
            {
                return new DeskSpot
                {
                    X = parking.Center.X - 80f,
                    Y = parking.Y + parking.H - 48f,
                    W = size,
                    H = size
                };
            }

            var drive = floor.DriveSouth;
            // East shoulder of the south access drive, at the highway frontage —
            // past the 90-unit lane and not under the porte-cochère.
            float laneRight = drive.X + ParkingDriveWidth + 18f;
            float x = System.Math.Max(laneRight, drive.X + drive.W - 22f);
            float y = drive.Y + drive.H - 22f;
            return new DeskSpot { X = x, Y = y, W = size, H = size };
        }

        static DeskSpot PlaceNewspaperBox(BuiltFloor floor, NavGrid grid)
        {
            const float w = 16f;
            const float h = 14f;
            float x = 12f * WorldScale.UnitsPerMeter;
            float y = 65f * WorldScale.UnitsPerMeter;

            if (!OnLotOrWalk(floor, x, y) && grid != null)
            {
                var cell = Navigation.NearestOpenCell(grid, x, y, null, 10f, 12);
                if (cell.HasValue)
                {
                    var center = Navigation.CellCenter(grid, cell.Value.Col, cell.Value.Row);
                    x = center.X;
                    y = center.Y;
                }
            }

            return new DeskSpot { X = x, Y = y, W = w, H = h };
        }

        static bool OnLotOrWalk(BuiltFloor floor, float x, float y)
        {
            if (floor == null) return false;
            if (floor.PorteCochere.W > 0 && floor.PorteCochere.Contains(x, y)) return true;
            if (floor.Parking.W > 0 && floor.Parking.Contains(x, y)) return true;
            if (floor.WalkWest.W > 0 && floor.WalkWest.Contains(x, y)) return true;
            if (floor.WalkNorth.W > 0 && floor.WalkNorth.Contains(x, y)) return true;
            if (floor.DriveSouth.W > 0 && floor.DriveSouth.Contains(x, y, 8f)) return true;
            return floor.Lobby.W > 0 && floor.Lobby.Contains(x, y, 18f);
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

        public float DriveCenterX
        {
            get
            {
                float fromLobby = Lobby.W > 0
                    ? Lobby.X + Lobby.W + ParkingDriveWidth / 2f + 12f
                    : Parking.X + Parking.W / 2f;
                float maxX = Parking.X + Parking.W - ParkingDriveWidth / 2f - 8f;
                if (DriveSouth.W > 0)
                {
                    maxX = System.Math.Min(maxX, DriveSouth.X + DriveSouth.W - ParkingDriveWidth / 2f - 8f);
                }

                return System.Math.Min(fromLobby, maxX);
            }
        }

        public float WestAisleX => Parking.X + ParkingStallDepth + 12f + ParkingDriveWidth / 2f;

        public float NorthAisleY => Parking.Y + ParkingStallDepth + 18f;

        public Point HighwayEntry
        {
            get
            {
                float y = DriveSouth.W > 0
                    ? DriveSouth.Y + DriveSouth.H + 28f
                    : Parking.Y + Parking.H + 28f;
                return new Point(DriveCenterX, y);
            }
        }

        public Point FrontEntrance
        {
            get
            {
                if (Floor != null && (Floor.LotEntrance.X != 0f || Floor.LotEntrance.Y != 0f))
                {
                    return Floor.LotEntrance;
                }

                return new Point(Lobby.X + Lobby.W / 2f, Lobby.Y - 18f);
            }
        }

        public Point EastEntrance
        {
            get
            {
                if (Floor != null && (Floor.LotEntranceEast.X != 0f || Floor.LotEntranceEast.Y != 0f))
                {
                    return Floor.LotEntranceEast;
                }

                return new Point(Lobby.X + Lobby.W + 20f, Lobby.Y + Lobby.H * 0.6f);
            }
        }

        public Point SouthEntrance
        {
            get
            {
                if (Floor != null && (Floor.LotEntranceSouth.X != 0f || Floor.LotEntranceSouth.Y != 0f))
                {
                    return Floor.LotEntranceSouth;
                }

                return new Point(Lobby.X + Lobby.W / 2f, Lobby.Y + Lobby.H + 20f);
            }
        }

        public MotorZone MotorZoneAt(float x, float y)
        {
            if (Lobby.W > 0 && Lobby.Contains(x, y, 4f)) return MotorZone.Lobby;
            if (DriveSouth.W > 0 && x >= Lobby.X + Lobby.W - 4f && DriveSouth.Contains(x, y, 12f))
            {
                return MotorZone.Drive;
            }

            if ((PorteCochere.W > 0 && PorteCochere.Contains(x, y, 8f)) ||
                (Lobby.W > 0 && y >= Lobby.Y + Lobby.H - 4f && x <= Lobby.X + Lobby.W + 8f))
            {
                return MotorZone.Canopy;
            }

            return MotorZone.Court;
        }

        public Point LobbyGateFor(float x, float y)
        {
            var zone = MotorZoneAt(x, y);
            if (zone == MotorZone.Drive) return EastEntrance;
            if (zone == MotorZone.Canopy) return SouthEntrance;
            return FrontEntrance;
        }

        public Point DriveLaneCorner(float carY)
        {
            return DrivePoint(DriveCenterX, carY);
        }

        public Point DrivePoint(float centerX, float carY)
        {
            return new Point(centerX - ParkedCarWidth / 2f, carY);
        }

        public StallPose StallPose(int index)
        {
            if (stalls == null || stalls.Length == 0)
            {
                return default;
            }

            if (index < 0) index = 0;
            if (index >= stalls.Length) index = stalls.Length - 1;
            return stalls[index];
        }

        public List<Point> StallDriveIn(int index)
        {
            var pose = StallPose(index);
            var path = new List<Point>();
            if (IsNorthFacing(pose))
            {
                float aisleY = NorthAisleY;
                path.Add(DriveLaneCorner(aisleY));
                path.Add(new Point(pose.Car.X, aisleY));
            }
            else if (pose.Car.Y < NorthAisleY)
            {
                float aisleY = NorthAisleY;
                path.Add(DriveLaneCorner(aisleY));
                path.Add(DrivePoint(WestAisleX, aisleY));
                path.Add(DrivePoint(WestAisleX, pose.Car.Y));
            }
            else
            {
                path.Add(DriveLaneCorner(pose.Car.Y));
            }

            path.Add(pose.Car);
            return path;
        }

        public List<Point> StallDriveOut(int index)
        {
            var pose = StallPose(index);
            var path = new List<Point>();
            if (IsNorthFacing(pose))
            {
                float aisleY = NorthAisleY;
                path.Add(new Point(pose.Car.X, aisleY));
                path.Add(DriveLaneCorner(aisleY));
            }
            else if (pose.Car.Y < NorthAisleY)
            {
                float aisleY = NorthAisleY;
                path.Add(DrivePoint(WestAisleX, pose.Car.Y));
                path.Add(DrivePoint(WestAisleX, aisleY));
                path.Add(DriveLaneCorner(aisleY));
            }
            else
            {
                path.Add(DriveLaneCorner(pose.Car.Y));
            }

            path.Add(DriveLaneCorner(HighwayEntry.Y));
            return path;
        }

        public static bool IsNorthFacing(StallPose pose)
        {
            return System.Math.Abs(pose.Yaw) > 45f;
        }

        public static void CarFootprint(StallPose pose, out float x, out float y, out float w, out float h)
        {
            if (IsNorthFacing(pose))
            {
                w = ParkedCarHeight;
                h = ParkedCarWidth;
            }
            else
            {
                w = ParkedCarWidth;
                h = ParkedCarHeight;
            }

            x = pose.Car.X;
            y = pose.Car.Y;
        }

        void BuildStalls()
        {
            var list = new List<StallPose>();
            if (Floor != null && Floor.Rooms != null)
            {
                foreach (var room in Floor.Rooms)
                {
                    if (room.DoorSide == "east") list.Add(MakeWestStall(room));
                }

                foreach (var room in Floor.Rooms)
                {
                    if (room.DoorSide == "south") list.Add(MakeNorthStall(room));
                }
            }

            NudgeNorthStalls(list);
            stalls = list.Count > 0 ? list.ToArray() : new StallPose[0];
        }

        StallPose MakeWestStall(PlannedRoom room)
        {
            float walkEdge = Floor.WalkWest.W > 0f ? Floor.WalkWest.X + Floor.WalkWest.W : Parking.X;
            float walkX = Floor.WalkWest.W > 0f
                ? Floor.WalkWest.X + Floor.WalkWest.W * 0.55f
                : walkEdge - 16f;
            return new StallPose
            {
                Car = new Point(walkEdge + 10f, room.Center.Y - ParkedCarHeight / 2f),
                WalkOut = new Point(walkX, room.Center.Y),
                Yaw = 0f
            };
        }

        StallPose MakeNorthStall(PlannedRoom room)
        {
            float walkSouth = Floor.WalkNorth.H > 0f ? Floor.WalkNorth.Y + Floor.WalkNorth.H : Parking.Y;
            float walkY = Floor.WalkNorth.H > 0f
                ? Floor.WalkNorth.Y + Floor.WalkNorth.H * 0.55f
                : walkSouth - 16f;
            return new StallPose
            {
                Car = new Point(room.Center.X - ParkedCarHeight / 2f, walkSouth + 10f),
                WalkOut = new Point(room.Center.X, walkY),
                Yaw = 90f
            };
        }

        static void NudgeNorthStalls(List<StallPose> list)
        {
            const float pad = 10f;
            for (int i = 0; i < list.Count; i++)
            {
                var pose = list[i];
                if (!IsNorthFacing(pose)) continue;
                CarFootprint(pose, out float nx, out float ny, out float nw, out float nh);
                bool moved;
                do
                {
                    moved = false;
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (j == i || IsNorthFacing(list[j])) continue;
                        CarFootprint(list[j], out float wx, out float wy, out float ww, out float wh);
                        if (nx + nw + pad <= wx || wx + ww + pad <= nx || ny + nh + pad <= wy || wy + wh + pad <= ny)
                        {
                            continue;
                        }

                        nx = wx + ww + pad;
                        pose.Car = new Point(nx, pose.Car.Y);
                        pose.WalkOut = new Point(nx + nw / 2f, pose.WalkOut.Y);
                        list[i] = pose;
                        moved = true;
                        break;
                    }
                } while (moved);
            }
        }

        public Point CheckoutLineSlot(int index)
        {
            const int perRow = 4;
            int col = index % perRow;
            int row = index / perRow;
            float x = FrontDesk.X + 10f + col * 28f;
            float y = FrontDesk.Y + FrontDesk.H / 2f + 40f + row * 26f;
            if (Lobby.W > 0)
            {
                float pad = Tile > 0 ? Tile * 2.4f : 24f;
                x = Geometry.Clamp(x, Lobby.X + pad, Lobby.X + Lobby.W - pad);
                float deskFront = FrontDesk.Y + FrontDesk.H / 2f + 28f;
                y = Geometry.Clamp(y, deskFront, Lobby.Y + Lobby.H - pad);
            }

            return new Point(x, y);
        }

        public Point StaffPaySlot(string staffId)
        {
            float offset = staffId == "mary" ? 46 : 0;
            return new Point(FrontDesk.X - 30 + offset, FrontDesk.Y - FrontDesk.H / 2f - 22);
        }

        public NavGrid GridFor(int floorLevel)
        {
            if (floorLevel > 0 && UpperGrid != null) return UpperGrid;
            if (floorLevel < 0 && BasementGrid != null) return BasementGrid;
            return NavGrid;
        }

        public Rect WalkRect(int floorLevel)
        {
            if (floorLevel > 0 && UpperWalk.W > 0) return UpperWalk;
            if (floorLevel < 0 && Basement.W > 0) return Basement;
            return WalkBounds.W > 0 ? WalkBounds : Building;
        }

        public bool InStairs(float x, float y)
        {
            return Stairs.W > 0 && Stairs.Contains(x, y, Tile * 0.2f);
        }

        public bool InUpperStairs(float x, float y)
        {
            return UpperStairs.W > 0 && UpperStairs.Contains(x, y, Tile * 0.2f);
        }

        public float StairFootY(float x, float y)
        {
            if (!InStairs(x, y) || Stairs.W <= Tile) return 0f;
            float t = Geometry.Clamp((x - Stairs.X) / Stairs.W, 0f, 1f);
            return -(1f - t) * WorldScale.FloorDepth;
        }

        public float UpperStairFootY(float x, float y)
        {
            if (!InUpperStairs(x, y) || UpperStairs.H <= Tile) return 0f;
            float t = Geometry.Clamp((y - UpperStairs.Y) / UpperStairs.H, 0f, 1f);
            return (1f - t) * WorldScale.UpperFloorY;
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
                    if (area.Level < 0)
                    {
                        if (area.Kind == AreaKind.Basement) continue;
                        if (area.Rect.Contains(point.X, point.Y)) return -1;
                    }
                    else if (area.Level > 0 && area.Kind == AreaKind.GuestRoom &&
                             area.Rect.Contains(point.X, point.Y))
                    {
                        return 1;
                    }
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

            if (InUpperStairs(entity.X, entity.Y) &&
                (entity.FloorLevel >= 1 || entity.GoalFloor >= 1 || entity is PlayerActor))
            {
                entity.FootY = UpperStairFootY(entity.X, entity.Y);
                entity.FloorLevel = entity.FootY > WorldScale.UpperFloorY * 0.45f ? 1 : 0;
                return;
            }

            if (entity.FloorLevel > 0) entity.FootY = WorldScale.UpperFloorY;
            else if (entity.FloorLevel < 0) entity.FootY = -WorldScale.FloorDepth;
            else entity.FootY = 0f;
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
            Consider("upper stairs", UpperStairs);
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

                if (Newspaper != null)
                {
                    Consider("newspaper", new Rect(Newspaper.X - Newspaper.W / 2f, Newspaper.Y - Newspaper.H / 2f, Newspaper.W, Newspaper.H));
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
            for (int i = 0; i < 8; i++)
            {
                var slot = CheckoutLineSlot(i);
                if (Lobby.W > 0 && !Lobby.Contains(slot.X, slot.Y, 12f))
                {
                    problems.Add($"Checkout slot {i} is outside the lobby");
                }
            }

            if (UpperGrid != null && UpperStairs.W > 0)
            {
                problems.AddRange(Navigation.ValidateFloor(UpperGrid, Floor, UpperStairsUpper));
                if (Navigation.FindRoute(UpperGrid, UpperStairsUpper, UpperStairsUpper, null, 12f) == null)
                {
                    problems.Add("Upper stair landing is blocked");
                }
            }

            if (BasementGrid == null || Stairs.W <= 0) return problems;

            var permits = new HashSet<string> { "office" };
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

        static Rect UpperWalkBounds(BuiltFloor floor, Rect stairs)
        {
            float x0 = stairs.W > 0 ? stairs.X : 0f;
            float y0 = stairs.W > 0 ? stairs.Y : 0f;
            float x1 = stairs.W > 0 ? stairs.X + stairs.W : 0f;
            float y1 = stairs.W > 0 ? stairs.Y + stairs.H : 0f;
            bool any = stairs.W > 0;
            if (floor?.Areas != null)
            {
                foreach (var area in floor.Areas)
                {
                    if (area.Level != 1 || area.Rect.W <= 0f) continue;
                    if (!any)
                    {
                        x0 = area.Rect.X;
                        y0 = area.Rect.Y;
                        x1 = area.Rect.X + area.Rect.W;
                        y1 = area.Rect.Y + area.Rect.H;
                        any = true;
                        continue;
                    }

                    x0 = System.Math.Min(x0, area.Rect.X);
                    y0 = System.Math.Min(y0, area.Rect.Y);
                    x1 = System.Math.Max(x1, area.Rect.X + area.Rect.W);
                    y1 = System.Math.Max(y1, area.Rect.Y + area.Rect.H);
                }
            }

            if (!any) return default;
            return new Rect(x0 - 8f, y0 - 8f, x1 - x0 + 16f, y1 - y0 + 16f);
        }
    }

    public enum MotorZone
    {
        Lobby,
        Drive,
        Canopy,
        Court
    }

    public struct StallPose
    {
        public Point Car;
        public Point WalkOut;
        public float Yaw;
    }
}

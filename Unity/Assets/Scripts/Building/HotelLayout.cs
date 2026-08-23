using System.Collections.Generic;

namespace Vacancy
{
    public sealed class HotelLayout
    {
        public float Width;
        public float Height;
        public Rect Building;
        public BuiltFloor Floor;
        public NavGrid NavGrid;
        public float Tile;
        public List<PlannedRoom> Rooms;
        public int RoomCount;
        public List<Point> RoomCenters;
        public int Cols;
        public Rect Lobby;
        public OfficeSpot Office;
        public DeskSpot FrontDesk;
        public Dictionary<string, DepartmentSpot> Departments;
        public DeskSpot LobbyRadio;
        public DeskSpot Newspaper;
        public DeskSpot VacancySign;
        public Point Spawn;

        readonly Dictionary<string, DepartmentSpot> deptForStaff = new Dictionary<string, DepartmentSpot>();

        public static HotelLayout Create(float width = 1360f, float height = 820f)
        {
            var building = new Rect(48, 40, width - 96, height - 108);
            var floor = Floorplan.CreateFloor(Floorplan.FlagshipGround, building);
            var navGrid = Navigation.Build(floor);
            var desk = floor.FrontDesk;
            var layout = new HotelLayout
            {
                Width = width,
                Height = height,
                Building = building,
                Floor = floor,
                NavGrid = navGrid,
                Tile = floor.Tile,
                Rooms = floor.Rooms,
                RoomCount = floor.Rooms.Count,
                RoomCenters = new List<Point>(),
                Cols = floor.RoomsPerRow,
                Lobby = floor.Lobby,
                Office = floor.Office,
                FrontDesk = desk,
                Departments = floor.Departments,
                LobbyRadio = new DeskSpot { X = desk.X - 110f, Y = desk.Y - 8f, W = 36, H = 28 },
                Newspaper = new DeskSpot { X = desk.X - 38f, Y = desk.Y + 4f, W = 28, H = 18 },
                VacancySign = new DeskSpot { X = width / 2f, Y = height - 28f, W = 140, H = 36 },
                Spawn = new Point(desk.X, desk.Y + 40)
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
            return new Point(FrontDesk.X, FrontDesk.Y + 46);
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
            return new Point(FrontDesk.X + 96 + index * 40, FrontDesk.Y + 30);
        }

        public Point CheckoutLineSlot(int index)
        {
            const int perRow = 8;
            int col = index % perRow;
            int row = System.Math.Min(1, index / perRow);
            return new Point(FrontDesk.X - 96 - col * 38, FrontDesk.Y + 24 + row * 26);
        }

        public Point StaffPaySlot(string staffId)
        {
            float offset = staffId == "mary" ? 46 : 0;
            return new Point(FrontDesk.X - 30 + offset, FrontDesk.Y - 40);
        }

        public List<string> Validate()
        {
            return Navigation.ValidateFloor(NavGrid, Floor, DeskApproach());
        }
    }
}

using System.Collections.Generic;

namespace Vacancy
{
    public static class AreaKind
    {
        public const string Corridor = "corridor";
        public const string Lobby = "lobby";
        public const string GuestRoom = "guestRoom";
        public const string Office = "office";
        public const string Department = "department";
        public const string Parking = "parking";
        public const string Stairs = "stairs";
        public const string Basement = "basement";
        public const string Storage = "storage";
        public const string Walkway = "walkway";
    }

    public sealed class BandSpec
    {
        public string Kind;
        public string DoorSide;
        public float Height;
        public bool Grow;
    }

    public sealed class FloorSpec
    {
        public string Id;
        public string Name;
        public int Level;
        public float Tile;
        public float Edge;
        public float SideCorridor;
        public float DoorWidth;
        public Point RoomSize;
        public int MaxRoomsPerRow;
        public List<BandSpec> Bands;
    }

    public sealed class FloorArea
    {
        public string Id;
        public string Kind;
        public string Token;
        public string Label;
        public string Accent;
        public Rect Rect;
        public bool Walls;
        public List<Door> Doors = new List<Door>();
        public int? RoomId;
        public string DepartmentId;
        public int Level;
    }

    public sealed class PlannedRoom
    {
        public int Id;
        public Rect Rect;
        public Point Center;
        public string DoorSide;
        public Point Door;
        public Door DoorOpening;
        public Point Approach;
        public int Level;
    }

    public sealed class DepartmentSpot
    {
        public string Id;
        public string Label;
        public string Accent;
        public Rect Rect;
        public float X;
        public float Y;
        public float W;
        public float H;
    }

    public sealed class OfficeSpot
    {
        public float X;
        public float Y;
        public float W;
        public float H;
        public Rect Rect;
        public Point Door;
        public Point Approach;
    }

    public sealed class DeskSpot
    {
        public float X;
        public float Y;
        public float W;
        public float H;
    }

    public sealed class BuiltFloor
    {
        public string Id;
        public string Name;
        public int Level;
        public float Tile;
        public FloorSpec Spec;
        public Rect Bounds;
        public Rect Content;
        public List<FloorArea> Areas = new List<FloorArea>();
        public List<PlannedRoom> Rooms = new List<PlannedRoom>();
        public int RoomsPerRow;
        public Point RoomSize;
        public Rect Lobby;
        public Rect Parking;
        public Rect Basement;
        public Rect Stairs;
        public Rect UpperStairs;
        public OfficeSpot Office;
        public DeskSpot FrontDesk;
        public Dictionary<string, DepartmentSpot> Departments = new Dictionary<string, DepartmentSpot>();
        public Rect WalkWest;
        public Rect WalkNorth;
        public Rect PorteCochere;
        public Rect DriveSouth;
        public Rect CornerMass;
        public Point LotEntrance;
        public Point LotEntranceEast;
        public Point LotEntranceSouth;
        public bool OutdoorCourt;
    }

    public static class Floorplan
    {
        public static readonly FloorSpec FlagshipGround = new FloorSpec
        {
            Id = "ground",
            Name = "Inn",
            Level = 0,
            Tile = 10,
            Edge = 20,
            SideCorridor = 100,
            DoorWidth = 40,
            RoomSize = new Point(GameConfig.RoomWidth, GameConfig.RoomHeight),
            MaxRoomsPerRow = 8,
            Bands = new List<BandSpec>()
        };

        static readonly HashSet<string> PublicKinds = new HashSet<string>
        {
            AreaKind.Corridor, AreaKind.Lobby, AreaKind.Department, AreaKind.Parking,
            AreaKind.Stairs, AreaKind.Basement, AreaKind.Storage, AreaKind.Walkway
        };

        static readonly DepartmentDef[] Departments =
        {
            new DepartmentDef("housekeeping", "Mary's room", "left", "#e8a0bf"),
            new DepartmentDef("maintenance", "Bob's closet", "right", "#ffb347")
        };

        struct DepartmentDef
        {
            public readonly string Id;
            public readonly string Label;
            public readonly string Side;
            public readonly string Accent;

            public DepartmentDef(string id, string label, string side, string accent)
            {
                Id = id;
                Label = label;
                Side = side;
                Accent = accent;
            }
        }

        public static BuiltFloor CreateMotorCourt(FloorSpec spec = null)
        {
            spec = spec ?? FlagshipGround;
            float tile = spec.Tile;
            float Down(float value) => (float)(System.Math.Floor(value / tile) * tile);

            // Facade along the courtyard walk; depth back from the door.
            float roomAlong = Down(spec.RoomSize.X);
            float roomDepth = Down(spec.RoomSize.Y);
            float walkW = Down(spec.SideCorridor > 0 ? spec.SideCorridor : 100f);
            float originX = Down(40);
            float originY = Down(40);
            const int westCount = 8;
            const int northCount = 4;
            // Leftover bays beside the office were ~30 units; grow the lobby
            // into unused dirt (west) and the drive (east) so both sides fill.
            float lobbySide = Down(40);
            float lobbyW = Down(280) + lobbySide * 2f;
            float lobbyH = Down(280);
            float canopyH = Down(80);

            var corner = new Rect(originX, originY, roomDepth, roomDepth);
            var northWing = new Rect(originX + roomDepth, originY, northCount * roomAlong, roomDepth);
            var westWing = new Rect(originX, originY + roomDepth, roomDepth, westCount * roomAlong);
            var walkNorth = new Rect(northWing.X, northWing.Y + northWing.H, northWing.W, walkW);
            float walkWestY = walkNorth.Y + walkNorth.H;
            var walkWest = new Rect(
                westWing.X + westWing.W,
                walkWestY,
                walkW,
                westWing.Y + westWing.H - walkWestY);
            var lobbyRect = new Rect(originX - lobbySide, westWing.Y + westWing.H, lobbyW, lobbyH);
            var parking = new Rect(
                walkNorth.X + walkW,
                walkNorth.Y + walkNorth.H,
                northWing.X + northWing.W - (walkNorth.X + walkW),
                lobbyRect.Y - (walkNorth.Y + walkNorth.H));
            // Pull-up canopy over the lobby south door — match lobby width so
            // the roof and pillars stay off the east drive / highway.
            var canopy = new Rect(lobbyRect.X, lobbyRect.Y + lobbyRect.H, lobbyRect.W, canopyH);
            var driveSouth = new Rect(
                lobbyRect.X + lobbyRect.W,
                lobbyRect.Y,
                northWing.X + northWing.W - (lobbyRect.X + lobbyRect.W),
                lobbyRect.H + canopyH);

            float west = System.Math.Min(originX, lobbyRect.X);
            float east = northWing.X + northWing.W;
            float south = canopy.Y + canopy.H;
            var bounds = new Rect(west, originY, Down(east - west), Down(south - originY));

            var floor = new BuiltFloor
            {
                Id = spec.Id,
                Name = spec.Name,
                Level = spec.Level,
                Tile = tile,
                Spec = spec,
                Bounds = bounds,
                Content = lobbyRect,
                RoomsPerRow = westCount,
                RoomSize = new Point(roomAlong, roomDepth),
                OutdoorCourt = true,
                WalkWest = walkWest,
                WalkNorth = walkNorth,
                PorteCochere = canopy,
                DriveSouth = driveSouth,
                CornerMass = corner,
                Lobby = lobbyRect,
                Parking = parking
            };

            AddOpenArea(floor, "walk-north", AreaKind.Walkway, "Covered walk", walkNorth);
            AddOpenArea(floor, "walk-west", AreaKind.Walkway, "Covered walk", walkWest);
            AddOpenArea(floor, "walk-north-up", AreaKind.Walkway, "Upper walk", walkNorth, 1);
            AddOpenArea(floor, "walk-west-up", AreaKind.Walkway, "Upper walk", walkWest, 1);
            AddOpenArea(floor, "parking", AreaKind.Parking, "Parking lot", parking);
            AddOpenArea(floor, "parking-drive", AreaKind.Parking, "Drive", driveSouth);
            AddOpenArea(floor, "porte-cochere", AreaKind.Walkway, "Porte-cochère", canopy);

            // Long west wing, rooms nearest the office first, doors facing the lot.
            for (int i = 0; i < westCount; i++)
            {
                var roomRect = new Rect(
                    westWing.X,
                    westWing.Y + westWing.H - (i + 1) * roomAlong,
                    roomDepth,
                    roomAlong);
                AddGuestRoom(floor, spec, roomRect, "east", tile);
            }

            for (int i = 0; i < northCount; i++)
            {
                var roomRect = new Rect(northWing.X + i * roomAlong, northWing.Y, roomAlong, roomDepth);
                AddGuestRoom(floor, spec, roomRect, "south", tile);
            }

            int groundRooms = floor.Rooms.Count;
            for (int i = 0; i < groundRooms; i++)
            {
                var ground = floor.Rooms[i];
                AddGuestRoom(floor, spec, ground.Rect, ground.DoorSide, tile, 1);
            }

            // West half of the walk is the stair tower; east half stays a
            // ground-level lobby door so going to a first-floor room does not
            // force a climb.
            var lobbyStairGap = MakeDoor(lobbyRect, "north", spec.DoorWidth, 0.70f, tile);
            var northDoor = MakeDoor(lobbyRect, "north", spec.DoorWidth, 0.85f, tile);
            var southDoor = MakeDoor(lobbyRect, "south", spec.DoorWidth * 2f, 0.5f, tile);
            // Wide east opening centered on drive traffic so radius-12
            // guests are not sent along the solid wall to the north courtyard door.
            var eastDoor = MakeDoor(lobbyRect, "east", spec.DoorWidth * 2f, 0.65f, tile);
            floor.Areas.Add(new FloorArea
            {
                Id = "lobby",
                Kind = AreaKind.Lobby,
                Label = "Lobby",
                Rect = lobbyRect,
                Walls = true,
                Doors = new List<Door> { lobbyStairGap, northDoor, southDoor, eastDoor }
            });
            floor.LotEntrance = OutsidePoint(northDoor, tile);
            floor.LotEntranceEast = OutsidePoint(eastDoor, tile);
            floor.LotEntranceSouth = OutsidePoint(southDoor, tile);

            float stairH = Down(80);
            float stairFlightW = Down(System.Math.Min(50f, walkW * 0.5f));
            var upperStairs = new Rect(walkWest.X, lobbyRect.Y, stairFlightW, stairH);
            var upDoorSouth = MakeDoor(upperStairs, "south", spec.DoorWidth, 0.5f, tile);
            var upDoorNorth = MakeDoor(upperStairs, "north", spec.DoorWidth, 0.5f, tile);
            floor.Areas.Add(new FloorArea
            {
                Id = "stairs-up",
                Kind = AreaKind.Stairs,
                Label = "Upper stairs",
                Rect = upperStairs,
                Walls = true,
                Doors = new List<Door> { upDoorSouth, upDoorNorth }
            });
            floor.Areas.Add(new FloorArea
            {
                Id = "stairs-up-landing",
                Kind = AreaKind.Stairs,
                Label = "Upper stairs",
                Rect = upperStairs,
                Walls = true,
                Doors = new List<Door> { upDoorSouth, upDoorNorth },
                Level = 1
            });
            floor.UpperStairs = upperStairs;

            float officeH = Down(90);
            float stairW = Down(80);
            float officeLeft = lobbyRect.X + Down(16);
            float officeW = Down(walkWest.X - tile - officeLeft);
            var backBlock = new Rect(
                officeLeft,
                lobbyRect.Y + Down(16),
                officeW,
                officeH);
            var stairsRect = new Rect(backBlock.X, backBlock.Y, stairW, officeH);
            var officeRect = new Rect(
                backBlock.X + stairW,
                backBlock.Y,
                officeW - stairW,
                officeH);
            floor.Areas.Add(new FloorArea
            {
                Id = "stairs",
                Kind = AreaKind.Stairs,
                Label = "Basement stairs",
                Rect = stairsRect,
                Walls = true,
                Doors = new List<Door> { MakeDoor(stairsRect, "east", spec.DoorWidth, 0.5f, tile) }
            });
            floor.Stairs = stairsRect;

            float deskH = Down(40);
            float staffAlley = Down(50);
            floor.FrontDesk = new DeskSpot
            {
                X = backBlock.X + backBlock.W / 2f,
                Y = backBlock.Y + backBlock.H + staffAlley + deskH / 2f,
                W = Down(160),
                H = deskH
            };

            float officeDoorAlong = Geometry.Clamp(
                (floor.FrontDesk.X - officeRect.X) / officeRect.W,
                0.2f,
                0.8f);
            var officeDoor = MakeDoor(officeRect, "south", spec.DoorWidth, officeDoorAlong, tile);
            var officeWest = MakeDoor(officeRect, "west", spec.DoorWidth, 0.5f, tile);
            floor.Areas.Add(new FloorArea
            {
                Id = "office",
                Kind = AreaKind.Office,
                Token = "office",
                Label = "Office",
                Rect = officeRect,
                Walls = true,
                Doors = new List<Door> { officeDoor, officeWest }
            });
            var officeCenter = officeRect.Center;
            floor.Office = new OfficeSpot
            {
                X = officeCenter.X,
                Y = officeCenter.Y,
                W = officeRect.W,
                H = officeRect.H,
                Rect = officeRect,
                Door = officeDoor.Center,
                Approach = OutsidePoint(officeDoor, tile)
            };

            AttachBasement(floor, spec.DoorWidth, tile);
            PruneUnusableDoors(floor.Areas, tile);
            return floor;
        }

        public static BuiltFloor CreateFloor(FloorSpec spec, Rect bounds)
        {
            _ = bounds;
            return CreateMotorCourt(spec);
        }

        public static Rect InnBuildingRect(
            FloorSpec spec,
            float lotWidth,
            float lotHeight,
            float marginX = 48f,
            float marginY = 40f,
            float parkingDepth = 280f)
        {
            _ = lotWidth;
            _ = lotHeight;
            _ = marginX;
            _ = marginY;
            _ = parkingDepth;
            return CreateMotorCourt(spec).Bounds;
        }

        public static Rect AttachParking(BuiltFloor floor, Rect building, float lotHeight)
        {
            _ = building;
            _ = lotHeight;
            return floor.Parking;
        }

        static void AddOpenArea(BuiltFloor floor, string id, string kind, string label, Rect rect, int level = 0)
        {
            if (rect.W <= 0f || rect.H <= 0f) return;
            floor.Areas.Add(new FloorArea
            {
                Id = id,
                Kind = kind,
                Label = label,
                Rect = rect,
                Level = level
            });
        }

        static void AddGuestRoom(BuiltFloor floor, FloorSpec spec, Rect roomRect, string doorSide, float tile, int level = 0)
        {
            int id = floor.Rooms.Count + 1;
            var door = MakeDoor(roomRect, doorSide, spec.DoorWidth, 0.5f, tile);
            floor.Areas.Add(new FloorArea
            {
                Id = $"room-{id}",
                Kind = AreaKind.GuestRoom,
                Token = $"room:{id}",
                Label = $"Room {id}",
                Rect = roomRect,
                Walls = true,
                Doors = new List<Door> { door },
                RoomId = id,
                Level = level
            });
            floor.Rooms.Add(new PlannedRoom
            {
                Id = id,
                Rect = roomRect,
                Center = roomRect.Center,
                DoorSide = door.Side,
                Door = door.Center,
                DoorOpening = door,
                Approach = OutsidePoint(door, tile),
                Level = level
            });
        }

        static void AttachBasement(BuiltFloor floor, float doorWidth, float tile)
        {
            var lobby = floor.Lobby;
            if (lobby.W <= 0f || lobby.H <= 0f) return;

            float Down(float value) => (float)(System.Math.Floor(value / tile) * tile);
            floor.Basement = lobby;
            floor.Areas.Add(new FloorArea
            {
                Id = "basement",
                Kind = AreaKind.Basement,
                Label = "Basement",
                Rect = lobby,
                Walls = true,
                Level = -1
            });

            float storeW = Down(140);
            float storeH = Down(70);
            var storeRect = new Rect(
                lobby.X + Down((lobby.W - storeW) / 2f),
                lobby.Y + Down(24),
                storeW,
                storeH);
            floor.Areas.Add(new FloorArea
            {
                Id = "storage",
                Kind = AreaKind.Storage,
                Label = "Storage",
                Rect = storeRect,
                Level = -1
            });

            foreach (var dept in Departments)
            {
                float deptW = Down(System.Math.Min(170, (lobby.W - tile * 8f) / 2f));
                float deptH = Down(System.Math.Min(80, lobby.H * 0.35f));
                var deptRect = new Rect(
                    dept.Side == "left"
                        ? lobby.X + tile * 2f
                        : lobby.X + lobby.W - deptW - tile * 2f,
                    lobby.Y + lobby.H - deptH - tile * 2f,
                    deptW,
                    deptH);
                var door = MakeDoor(deptRect, dept.Side == "left" ? "east" : "west", doorWidth, 0.5f, tile);
                floor.Areas.Add(new FloorArea
                {
                    Id = dept.Id,
                    Kind = AreaKind.Department,
                    Label = dept.Label,
                    Accent = dept.Accent,
                    Rect = deptRect,
                    Walls = true,
                    Doors = new List<Door> { door },
                    DepartmentId = dept.Id,
                    Level = -1
                });
                var center = deptRect.Center;
                floor.Departments[dept.Id] = new DepartmentSpot
                {
                    Id = dept.Id,
                    Label = dept.Label,
                    Accent = dept.Accent,
                    Rect = deptRect,
                    X = center.X,
                    Y = center.Y,
                    W = deptRect.W,
                    H = deptRect.H
                };
            }
        }

        static Door MakeDoor(Rect rect, string side, float width, float along, float tile)
        {
            string normalized = NormalizeSide(side);
            float doorWidth = (float)(System.Math.Floor(width / tile) * tile);
            bool horizontal = normalized == "north" || normalized == "south";
            float span = (horizontal ? rect.W : rect.H) - doorWidth;
            float offset = (float)(System.Math.Floor((span * along) / tile) * tile);

            if (horizontal)
            {
                return new Door
                {
                    Side = normalized,
                    Width = doorWidth,
                    Center = new Point(
                        rect.X + offset + doorWidth / 2f,
                        normalized == "north" ? rect.Y : rect.Y + rect.H),
                    Normal = new Point(0, normalized == "north" ? -1 : 1)
                };
            }

            return new Door
            {
                Side = normalized,
                Width = doorWidth,
                Center = new Point(
                    normalized == "west" ? rect.X : rect.X + rect.W,
                    rect.Y + offset + doorWidth / 2f),
                Normal = new Point(normalized == "west" ? -1 : 1, 0)
            };
        }

        static string NormalizeSide(string side)
        {
            if (side == "n") return "north";
            if (side == "s") return "south";
            if (side == "e") return "east";
            if (side == "w") return "west";
            return side;
        }

        static Point OutsidePoint(Door door, float tile)
        {
            float reach = tile * 2f;
            return new Point(
                door.Center.X + door.Normal.X * reach,
                door.Center.Y + door.Normal.Y * reach);
        }

        static void PruneUnusableDoors(List<FloorArea> areas, float tile)
        {
            foreach (var area in areas)
            {
                if (area.Doors == null || area.Doors.Count == 0) continue;
                area.Doors.RemoveAll(door =>
                {
                    var probe = new Point(
                        door.Center.X + door.Normal.X * tile * 1.5f,
                        door.Center.Y + door.Normal.Y * tile * 1.5f);
                    foreach (var other in areas)
                    {
                        if (other == area) continue;
                        if (IsOpenSpace(other, probe, tile)) return false;
                    }

                    return true;
                });
            }
        }

        static bool IsOpenSpace(FloorArea area, Point point, float tile)
        {
            if (!PublicKinds.Contains(area.Kind) && area.Kind != AreaKind.Office) return false;
            var r = area.Rect;
            float pad = area.Walls ? tile : 0f;
            return r.Contains(point, pad);
        }
    }
}

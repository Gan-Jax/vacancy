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
        public OfficeSpot Office;
        public DeskSpot FrontDesk;
        public Dictionary<string, DepartmentSpot> Departments = new Dictionary<string, DepartmentSpot>();
    }

    public static class Floorplan
    {
        public static readonly FloorSpec FlagshipGround = new FloorSpec
        {
            Id = "ground",
            Name = "Ground floor",
            Level = 0,
            Tile = 10,
            Edge = 20,
            SideCorridor = 50,
            DoorWidth = 40,
            RoomSize = new Point(GameConfig.RoomWidth, GameConfig.RoomHeight),
            MaxRoomsPerRow = 6,
            Bands = new List<BandSpec>
            {
                new BandSpec { Kind = "rooms", DoorSide = "south" },
                new BandSpec { Kind = "corridor", Height = 40 },
                new BandSpec { Kind = "rooms", DoorSide = "north" },
                new BandSpec { Kind = "corridor", Height = 40 },
                new BandSpec { Kind = "lobby", Height = 280, Grow = true },
                new BandSpec { Kind = "service", Height = 90 }
            }
        };

        static readonly HashSet<string> PublicKinds = new HashSet<string>
        {
            AreaKind.Corridor, AreaKind.Lobby, AreaKind.Department
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

        public static Rect InnBuildingRect(FloorSpec spec, float lotWidth, float lotHeight, float marginX = 48f, float marginY = 40f)
        {
            float tile = spec.Tile;
            float Down(float value) => (float)(System.Math.Floor(value / tile) * tile);
            float roomW = Down(spec.RoomSize.X);
            int cols = spec.MaxRoomsPerRow > 0 ? spec.MaxRoomsPerRow : 6;
            float innW = cols * roomW + Down(spec.SideCorridor) * 2f + Down(spec.Edge) * 2f;
            float innH = Down(lotHeight - marginY - 68f);
            return new Rect(Down((lotWidth - innW) / 2f), marginY, innW, innH);
        }

        public static BuiltFloor CreateFloor(FloorSpec spec, Rect bounds)
        {
            float tile = spec.Tile;
            float Down(float value) => (float)(System.Math.Floor(value / tile) * tile);

            float edge = Down(spec.Edge);
            var content = new Rect(
                bounds.X + edge,
                bounds.Y + edge,
                Down(bounds.W - edge * 2f),
                Down(bounds.H - edge * 2f));

            var roomSize = new Point(Down(spec.RoomSize.X), Down(spec.RoomSize.Y));
            int fit = System.Math.Max(
                1,
                (int)System.Math.Floor((content.W - Down(spec.SideCorridor) * 2f) / roomSize.X));
            int roomsPerRow = spec.MaxRoomsPerRow > 0 ? System.Math.Min(fit, spec.MaxRoomsPerRow) : fit;
            float roomsBlockW = roomsPerRow * roomSize.X;
            float roomsBlockX = content.X + Down((content.W - roomsBlockW) / 2f);

            float[] bandHeights = ResolveBandHeights(spec.Bands, content.H, roomSize.Y, tile);

            var floor = new BuiltFloor
            {
                Id = spec.Id,
                Name = spec.Name,
                Level = spec.Level,
                Tile = tile,
                Spec = spec,
                Bounds = bounds,
                Content = content,
                RoomsPerRow = roomsPerRow,
                RoomSize = roomSize
            };

            float cursorY = content.Y;
            for (int bandIndex = 0; bandIndex < spec.Bands.Count; bandIndex++)
            {
                var band = spec.Bands[bandIndex];
                float height = bandHeights[bandIndex];
                var bandRect = new Rect(content.X, cursorY, content.W, height);

                if (band.Kind == "rooms")
                {
                    for (int col = 0; col < roomsPerRow; col++)
                    {
                        var roomRect = new Rect(
                            roomsBlockX + col * roomSize.X,
                            cursorY,
                            roomSize.X,
                            height);
                        int id = floor.Rooms.Count + 1;
                        var door = MakeDoor(roomRect, band.DoorSide, spec.DoorWidth, 0.5f, tile);
                        floor.Areas.Add(new FloorArea
                        {
                            Id = $"room-{id}",
                            Kind = AreaKind.GuestRoom,
                            Token = $"room:{id}",
                            Label = $"Room {id}",
                            Rect = roomRect,
                            Walls = true,
                            Doors = new List<Door> { door },
                            RoomId = id
                        });
                        floor.Rooms.Add(new PlannedRoom
                        {
                            Id = id,
                            Rect = roomRect,
                            Center = roomRect.Center,
                            DoorSide = door.Side,
                            Door = door.Center,
                            DoorOpening = door,
                            Approach = OutsidePoint(door, tile)
                        });
                    }

                    PushSideCorridors(floor.Areas, content, bandRect, roomsBlockX, roomsBlockW);
                }
                else if (band.Kind == "lobby")
                {
                    var lobbyRect = new Rect(roomsBlockX, cursorY, roomsBlockW, height);
                    floor.Lobby = lobbyRect;
                    // Reception reads south→north the way a guest walks in from
                    // the lot: double doors, waiting chairs, front desk, then
                    // the office (PC behind the counter). Flanking passages
                    // lead to the guest-room corridors.
                    floor.Areas.Add(new FloorArea
                    {
                        Id = "lobby",
                        Kind = AreaKind.Lobby,
                        Label = "Lobby",
                        Rect = lobbyRect,
                        Walls = true,
                        Doors = new List<Door>
                        {
                            MakeDoor(lobbyRect, "north", spec.DoorWidth, 0.14f, tile),
                            MakeDoor(lobbyRect, "north", spec.DoorWidth, 0.86f, tile),
                            MakeDoor(lobbyRect, "south", spec.DoorWidth * 2f, 0.5f, tile),
                            MakeDoor(lobbyRect, "west", spec.DoorWidth, 0.72f, tile),
                            MakeDoor(lobbyRect, "east", spec.DoorWidth, 0.72f, tile)
                        }
                    });

                    float officeW = Down(300);
                    float officeH = Down(100);
                    var officeRect = new Rect(
                        lobbyRect.X + Down((lobbyRect.W - officeW) / 2f),
                        lobbyRect.Y + Down(20),
                        officeW,
                        officeH);
                    var officeDoor = MakeDoor(officeRect, "south", spec.DoorWidth, 0.5f, tile);
                    floor.Areas.Add(new FloorArea
                    {
                        Id = "office",
                        Kind = AreaKind.Office,
                        Token = "office",
                        Label = "Office",
                        Rect = officeRect,
                        Walls = true,
                        Doors = new List<Door> { officeDoor }
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

                    float deskH = Down(40);
                    float staffAlley = Down(60);
                    floor.FrontDesk = new DeskSpot
                    {
                        X = officeRect.X + officeRect.W / 2f,
                        Y = officeRect.Y + officeRect.H + staffAlley + deskH / 2f,
                        W = Down(320),
                        H = deskH
                    };

                    PushSideCorridors(floor.Areas, content, bandRect, roomsBlockX, roomsBlockW);
                }
                else if (band.Kind == "service")
                {
                    floor.Areas.Add(new FloorArea
                    {
                        Id = $"service-{bandIndex}",
                        Kind = AreaKind.Corridor,
                        Label = "Service corridor",
                        Rect = bandRect
                    });

                    foreach (var dept in Departments)
                    {
                        float deptW = Down(170);
                        var deptRect = new Rect(
                            dept.Side == "left"
                                ? bandRect.X + tile
                                : bandRect.X + bandRect.W - deptW - tile,
                            bandRect.Y + tile,
                            deptW,
                            bandRect.H - tile * 2f);
                        floor.Areas.Add(new FloorArea
                        {
                            Id = dept.Id,
                            Kind = AreaKind.Department,
                            Label = dept.Label,
                            Accent = dept.Accent,
                            Rect = deptRect,
                            DepartmentId = dept.Id
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
                else
                {
                    floor.Areas.Add(new FloorArea
                    {
                        Id = $"corridor-{bandIndex}",
                        Kind = AreaKind.Corridor,
                        Label = "Corridor",
                        Rect = bandRect
                    });
                }

                cursorY += height;
            }

            PruneUnusableDoors(floor.Areas, tile);
            return floor;
        }

        static float[] ResolveBandHeights(List<BandSpec> bands, float totalHeight, float roomHeight, float tile)
        {
            var heights = new float[bands.Count];
            var growIndexes = new List<int>();
            float used = 0f;
            for (int i = 0; i < bands.Count; i++)
            {
                heights[i] = bands[i].Kind == "rooms"
                    ? roomHeight
                    : (float)(System.Math.Floor((bands[i].Height > 0 ? bands[i].Height : tile * 6f) / tile) * tile);
                used += heights[i];
                if (bands[i].Grow) growIndexes.Add(i);
            }

            float slack = totalHeight - used;
            if (slack > 0 && growIndexes.Count > 0)
            {
                float share = (float)(System.Math.Floor(slack / growIndexes.Count / tile) * tile);
                foreach (int i in growIndexes) heights[i] += share;
            }

            return heights;
        }

        static void PushSideCorridors(List<FloorArea> areas, Rect content, Rect bandRect, float blockX, float blockW)
        {
            var left = new Rect(content.X, bandRect.Y, System.Math.Max(0, blockX - content.X), bandRect.H);
            var right = new Rect(blockX + blockW, bandRect.Y, System.Math.Max(0, content.X + content.W - (blockX + blockW)), bandRect.H);
            if (left.W > 0)
            {
                areas.Add(new FloorArea
                {
                    Id = $"side-left-{System.Math.Round(bandRect.Y)}",
                    Kind = AreaKind.Corridor,
                    Label = "Corridor",
                    Rect = left
                });
            }

            if (right.W > 0)
            {
                areas.Add(new FloorArea
                {
                    Id = $"side-right-{System.Math.Round(bandRect.Y)}",
                    Kind = AreaKind.Corridor,
                    Label = "Corridor",
                    Rect = right
                });
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
            if (!PublicKinds.Contains(area.Kind)) return false;
            var r = area.Rect;
            float pad = area.Walls ? tile : 0f;
            return point.X >= r.X + pad &&
                   point.X <= r.X + r.W - pad &&
                   point.Y >= r.Y + pad &&
                   point.Y <= r.Y + r.H - pad;
        }
    }
}

using System.Collections.Generic;

namespace Vacancy
{
    public sealed class StaffProfile
    {
        public string Id;
        public string Name;
        public string Role;
        public string Color;
        public string Department;
    }

    public sealed class StaffNpc : IMover
    {
        public string Id;
        public string Name;
        public string Role;
        public string Color;
        public string Department;
        public float X { get; set; }
        public float Y { get; set; }
        public float Radius { get; set; } = 12f;
        public ActiveTask ActiveTask;
        public Room TargetRoom;
        public int? ExitRoomId;
        public List<Point> Path { get; set; } = new List<Point>();
        public string Phase = "idle";
        public int WagesOwed;
        public int DaysWorkedInPeriod;
        public int PeriodDays;
        public bool WorkedToday;
        public bool PaydayDue;
        public float StallSeconds { get; set; }
        public int FloorLevel { get; set; }
        public float FootY { get; set; }

        public StaffNpc(StaffProfile profile, Point home)
        {
            Id = profile.Id;
            Name = profile.Name;
            Role = profile.Role;
            Color = profile.Color;
            Department = profile.Department;
            X = home.X;
            Y = home.Y;
        }

        public static StaffNpc SpawnAtHome(HotelLayout layout, StaffProfile profile)
        {
            var staff = new StaffNpc(profile, layout.StaffHome(profile.Department ?? profile.Id));
            staff.FloorLevel = -1;
            staff.FootY = -WorldScale.FloorDepth;
            return staff;
        }

        public Point HomePoint(HotelLayout layout)
        {
            return layout.StaffHome(Department ?? Id);
        }

        PathOptions HomePathOptions()
        {
            return new PathOptions { Radius = Radius, FromFloor = FloorLevel, ToFloor = -1 };
        }

        PathOptions GroundPathOptions()
        {
            return new PathOptions { Radius = Radius, FromFloor = FloorLevel, ToFloor = 0 };
        }

        public List<Point> PathHome(HotelLayout layout)
        {
            return Pathing.FindPath(layout, X, Y, HomePoint(layout), HomePathOptions());
        }

        public Point PaySlot(HotelLayout layout)
        {
            return layout.StaffPaySlot(Id);
        }

        public void OnNewDay(GameState state)
        {
            if (WorkedToday)
            {
                WagesOwed += GameConfig.StaffDailyWage;
                DaysWorkedInPeriod += 1;
                WorkedToday = false;
            }

            PeriodDays += 1;
            if (PeriodDays >= GameConfig.StaffPayPeriodDays && !PaydayDue)
            {
                PaydayDue = true;
                string plural = DaysWorkedInPeriod == 1 ? "" : "s";
                state.AddLog(
                    $"{Name} is due for payday (${WagesOwed} for {DaysWorkedInPeriod} work day{plural}). Heading to the front desk.");
            }
        }

        public int CollectPaycheck(GameState state, HotelLayout layout)
        {
            int paid = WagesOwed;
            WagesOwed = 0;
            DaysWorkedInPeriod = 0;
            PeriodDays = 0;
            PaydayDue = false;
            TargetRoom = null;
            Phase = "to_closet";
            Path = layout != null ? PathHome(layout) : new List<Point>();
            state.AddLog($"Paid {Name} ${paid} wages. Next payday in {GameConfig.StaffPayPeriodDays} days.");
            return paid;
        }

        public void BeginPaydayTrip(HotelLayout layout)
        {
            TargetRoom = null;
            Path = Pathing.PathToDeskHall(layout, X, Y, GroundPathOptions());
            Phase = "to_desk";
        }

        public void Update(float dt, GameState state, HotelLayout layout)
        {
            object allowId = GetAllowedRoomId();
            float speed = GameConfig.NpcMoveSpeed;
            Pathing.ResolveRoomCollision(this, state.Rooms, layout, allowId);

            if (ActiveTask != null)
            {
                Phase = "working";
                ActiveTask.Progress += GameConfig.HoursPerSecond * dt;
                ActiveTask.ApplyRoomProgress();
                var center = layout.RoomCenters[ActiveTask.Room.Id - 1];
                Pathing.SteerTo(this, center.X, center.Y, dt, state.Rooms, layout, allowId, speed);

                if (ActiveTask.Progress >= ActiveTask.Duration)
                {
                    var type = ActiveTask.Type;
                    var room = ActiveTask.Room;
                    ExitRoomId = room.Id;
                    ActiveTask = null;
                    TargetRoom = null;
                    room.Worker = null;
                    WorkedToday = true;
                    if (type == "inspect") Economy.FinishInspection(state, room, Name);
                    if (type == "clean") Economy.FinishCleaning(state, room, Name);
                    if (type == "repair") Economy.FinishRepair(state, room, Name);
                    Phase = "exit_room";
                    Path.Clear();
                }

                return;
            }

            if (Phase == "waiting_pay")
            {
                var slot = PaySlot(layout);
                X = slot.X;
                Y = slot.Y;
                FloorLevel = 0;
                FootY = 0f;
                return;
            }

            if (Phase == "to_desk")
            {
                if (Path.Count == 0)
                {
                    Path = Pathing.PathToDeskHall(layout, X, Y, GroundPathOptions());
                }

                bool atHall = Pathing.FollowPath(this, dt, state.Rooms, layout, null, speed);
                var slot = PaySlot(layout);
                if (atHall)
                {
                    Pathing.SteerTo(this, slot.X, slot.Y, dt, state.Rooms, layout, null, speed);
                    if (Geometry.Dist(X, Y, slot.X, slot.Y) < 18)
                    {
                        ArriveForPay(state, slot);
                    }
                }
                else if (Geometry.Dist(X, Y, slot.X, slot.Y) < 40)
                {
                    Path.Clear();
                    Pathing.SteerTo(this, slot.X, slot.Y, dt, state.Rooms, layout, null, speed);
                    if (Geometry.Dist(X, Y, slot.X, slot.Y) < 18) ArriveForPay(state, slot);
                }

                return;
            }

            if (Phase == "exit_room" && ExitRoomId != null)
            {
                var door = layout.RoomDoor(ExitRoomId.Value);
                Pathing.SteerTo(this, door.X, door.Y, dt, state.Rooms, layout, ExitRoomId.Value, speed);
                if (Geometry.Dist(X, Y, door.X, door.Y) < 16)
                {
                    ExitRoomId = null;
                    if (PaydayDue) BeginPaydayTrip(layout);
                    else
                    {
                        Phase = "to_closet";
                        Path = PathHome(layout);
                    }
                }

                return;
            }

            if (PaydayDue)
            {
                TargetRoom = null;
                if (Phase != "to_desk" && Phase != "waiting_pay") BeginPaydayTrip(layout);
                return;
            }

            if (!IsValidJobForRole(TargetRoom, Role, Id, state))
            {
                TargetRoom = PickJobRoom(state.Rooms, Role, state);
                if (TargetRoom != null)
                {
                    Phase = "to_door";
                    Path = Pathing.PathToRoomDoor(layout, X, Y, TargetRoom.Id, GroundPathOptions());
                }
                else if (Phase != "idle" && Phase != "to_closet")
                {
                    Phase = "to_closet";
                    Path = PathHome(layout);
                }
            }

            if (TargetRoom == null)
            {
                var home = HomePoint(layout);
                if (Geometry.Dist(X, Y, home.X, home.Y) > 14)
                {
                    if (Path.Count == 0 || Phase != "to_closet")
                    {
                        Phase = "to_closet";
                        Path = PathHome(layout);
                    }

                    Pathing.FollowPath(this, dt, state.Rooms, layout, null, speed);
                }
                else
                {
                    Phase = "idle";
                    Path.Clear();
                    X += (home.X - X) * 0.25f;
                    Y += (home.Y - Y) * 0.25f;
                }

                return;
            }

            if (Phase == "to_door")
            {
                if (Path.Count == 0)
                {
                    Path = Pathing.PathToRoomDoor(layout, X, Y, TargetRoom.Id, GroundPathOptions());
                }

                if (Pathing.FollowPath(this, dt, state.Rooms, layout, null, speed))
                {
                    Phase = "enter_room";
                    Path.Clear();
                }

                return;
            }

            if (Phase == "enter_room")
            {
                if (!IsValidJobForRole(TargetRoom, Role, Id, state))
                {
                    GoHome(layout);
                    return;
                }

                var center = layout.RoomCenters[TargetRoom.Id - 1];
                Pathing.SteerTo(this, center.X, center.Y, dt, state.Rooms, layout, TargetRoom.Id, speed);
                if (Geometry.Dist(X, Y, center.X, center.Y) < 26)
                {
                    var room = TargetRoom;
                    if (room.Worker != null)
                    {
                        GoHome(layout);
                        return;
                    }

                    string type = JobTypeForRoom(room, Role);
                    if (type == null)
                    {
                        GoHome(layout);
                        return;
                    }

                    if (type == "repair")
                    {
                        int cost = Economy.GetRepairCost(state, room.RepairLevel);
                        int? paid = Economy.BeginRepairPayment(state, room);
                        if (paid == null)
                        {
                            state.AddLog($"{Name} can't start Room {room.Id} repair — need ${cost}.");
                            GoHome(layout);
                            return;
                        }

                        if (paid > 0)
                        {
                            state.AddLog($"{Name} bought ${paid} in parts for Room {room.Id} ({room.RepairLevel}).");
                        }
                    }

                    ActiveTask = new ActiveTask
                    {
                        Type = type,
                        Room = room,
                        Progress = 0,
                        Duration = GameConfig.GetTaskHours(type, room, true)
                    };
                    room.Worker = Id;
                    ActiveTask.ApplyRoomProgress();
                    Phase = "working";
                }

                return;
            }

            if (Phase == "to_closet")
            {
                if (Path.Count == 0) Path = PathHome(layout);
                if (Pathing.FollowPath(this, dt, state.Rooms, layout, null, speed))
                {
                    Phase = "idle";
                    Path.Clear();
                }
            }
        }

        void ArriveForPay(GameState state, Point slot)
        {
            X = slot.X;
            Y = slot.Y;
            FloorLevel = 0;
            FootY = 0f;
            Phase = "waiting_pay";
            Path.Clear();
            state.AddLog($"{Name} is at the desk for payday (${WagesOwed}). Press E.");
        }

        void GoHome(HotelLayout layout)
        {
            TargetRoom = null;
            Phase = "to_closet";
            Path = PathHome(layout);
        }

        object GetAllowedRoomId()
        {
            if (ActiveTask?.Room != null) return ActiveTask.Room.Id;
            if (Phase == "enter_room" && TargetRoom != null) return TargetRoom.Id;
            if (Phase == "exit_room" && ExitRoomId != null) return ExitRoomId.Value;
            if (Phase == "working" && TargetRoom != null) return TargetRoom.Id;
            return null;
        }

        static string JobTypeForRoom(Room room, string role)
        {
            if (room == null) return null;
            if (role == "repair") return room.Status == "needs_repair" ? "repair" : null;
            if (role == "housekeeping" || role == "clean")
            {
                if (room.Status == "needs_inspection") return "inspect";
                if (room.Status == "dirty") return "clean";
            }

            return null;
        }

        static bool IsValidJobForRole(Room room, string role, string workerId, GameState state)
        {
            if (room == null || !room.Unlocked) return false;
            if (room.Worker != null && room.Worker != workerId) return false;
            if (JobTypeForRoom(room, role) == null) return false;
            if (role == "repair" && state != null && !room.RepairPaid && !Economy.CanAffordRepair(state, room))
            {
                return false;
            }

            return true;
        }

        static Room PickJobRoom(List<Room> rooms, string role, GameState state)
        {
            if (role == "repair")
            {
                foreach (var room in rooms)
                {
                    if (room.Unlocked && room.Status == "needs_repair" && room.Worker == null &&
                        (state == null || Economy.CanAffordRepair(state, room) || room.RepairPaid))
                    {
                        return room;
                    }
                }

                return null;
            }

            if (role == "housekeeping" || role == "clean")
            {
                foreach (var room in rooms)
                {
                    if (room.Unlocked && room.Status == "needs_inspection" && room.Worker == null) return room;
                }

                foreach (var room in rooms)
                {
                    if (room.Unlocked && room.Status == "dirty" && room.Worker == null) return room;
                }
            }

            return null;
        }
    }
}

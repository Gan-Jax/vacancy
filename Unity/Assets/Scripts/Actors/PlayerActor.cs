using System.Collections.Generic;

namespace Vacancy
{
    public sealed class ActiveTask
    {
        public string Type;
        public Room Room;
        public float Progress;
        public float Duration;

        public float Normalized
        {
            get
            {
                if (Duration <= 0f) return 1f;
                float t = Progress / Duration;
                if (t < 0f) return 0f;
                if (t > 1f) return 1f;
                return t;
            }
        }

        public void ApplyRoomProgress()
        {
            if (Room == null) return;
            float t = Normalized;
            if (Type == "inspect") Room.InspectProgress = t;
            else if (Type == "clean") Room.CleanProgress = t;
            else if (Type == "repair") Room.RepairProgress = t;
        }
    }

    public sealed class InteractTarget
    {
        public string Kind;
        public Room Room;
    }

    public sealed class PlayerActor : IMover
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Radius { get; set; } = 14f;
        public float Yaw;
        public float Pitch;
        public string Facing = "down";
        public ActiveTask ActiveTask;
        public List<Point> Path { get; set; } = new List<Point>();
        public float StallSeconds { get; set; }
        public int FloorLevel { get; set; }
        public float FootY { get; set; }

        public PlayerActor(float x, float y)
        {
            X = x;
            Y = y;
        }

        public ActiveTask Update(GameInput input, float dt, HotelLayout layout, List<Room> rooms)
        {
            if (input.LookEnabled)
            {
                float lookY = PlayerSettings.InvertY ? -input.LookY : input.LookY;
                Yaw += input.LookX * PlayerSettings.LookSensitivity;
                Pitch = Geometry.Clamp(Pitch - lookY * PlayerSettings.LookSensitivity, -80f, 80f);
            }

            if (ActiveTask != null)
            {
                ActiveTask.Progress += GameConfig.HoursPerSecond * dt;
                ActiveTask.ApplyRoomProgress();
                if (ActiveTask.Progress >= ActiveTask.Duration)
                {
                    var done = ActiveTask;
                    ActiveTask = null;
                    return done;
                }

                return null;
            }

            double yawRad = Yaw * System.Math.PI / 180.0;
            float sin = (float)System.Math.Sin(yawRad);
            float cos = (float)System.Math.Cos(yawRad);

            float dx = 0f;
            float dy = 0f;
            if (input.Up)
            {
                dx += sin;
                dy += cos;
            }

            if (input.Down)
            {
                dx -= sin;
                dy -= cos;
            }

            if (input.Right)
            {
                dx += cos;
                dy -= sin;
            }

            if (input.Left)
            {
                dx -= cos;
                dy += sin;
            }

            if (dx != 0 || dy != 0)
            {
                float len = (float)System.Math.Sqrt(dx * dx + dy * dy);
                dx /= len;
                dy /= len;
                Facing = System.Math.Abs(dx) > System.Math.Abs(dy)
                    ? (dx > 0 ? "right" : "left")
                    : (dy > 0 ? "down" : "up");
            }

            float speed = GameConfig.PlayerSpeed * dt;
            float nextX = X + dx * speed;
            float nextY = Y + dy * speed;
            const string allow = "player";

            if (!Pathing.CollidesWithRooms(nextX, Y, Radius, rooms, layout, allow, FloorLevel)) X = nextX;
            if (!Pathing.CollidesWithRooms(X, nextY, Radius, rooms, layout, allow, FloorLevel)) Y = nextY;

            var walk = layout.WalkRect(FloorLevel);
            X = Geometry.Clamp(X, walk.X + 20, walk.X + walk.W - 20);
            Y = Geometry.Clamp(Y, walk.Y + 24, walk.Y + walk.H - 16);
            Pathing.ResolveRoomCollision(this, rooms, layout, allow);
            layout.UpdateElevation(this);
            return null;
        }

        public void StartTask(string type, Room room)
        {
            ActiveTask = new ActiveTask
            {
                Type = type,
                Room = room,
                Progress = 0,
                Duration = GameConfig.GetTaskHours(type, room, false)
            };
            room.Worker = "player";
            ActiveTask.ApplyRoomProgress();
        }

        public bool CanInteractWith(string kind, Room room, HotelLayout layout, List<StaffNpc> staffList)
        {
            if (string.IsNullOrEmpty(kind) || layout == null) return false;
            switch (kind)
            {
                case "radio": return NearRadio(layout);
                case "phone": return NearPhone(layout);
                case "deskpc": return NearDeskPc(layout);
                case "newspaper": return NearNewspaper(layout);
                case "desk": return NearDesk(layout, staffList);
                case "office": return NearOffice(layout);
                case "sign": return NearSign(layout);
                case "room": return NearRoom(room, layout);
                default: return false;
            }
        }

        public InteractTarget GetInteractTarget(List<Room> rooms, HotelLayout layout, List<StaffNpc> staffList)
        {
            if (NearRadio(layout)) return new InteractTarget { Kind = "radio" };
            if (NearDeskPc(layout)) return new InteractTarget { Kind = "deskpc" };
            if (NearPhone(layout)) return new InteractTarget { Kind = "phone" };
            if (NearNewspaper(layout)) return new InteractTarget { Kind = "newspaper" };
            if (NearDesk(layout, staffList)) return new InteractTarget { Kind = "desk" };

            if (NearOffice(layout)) return new InteractTarget { Kind = "office" };
            if (NearSign(layout)) return new InteractTarget { Kind = "sign" };

            Room best = null;
            float bestDist = float.PositiveInfinity;
            if (rooms != null)
            {
                foreach (var room in rooms)
                {
                    if (!NearRoom(room, layout)) continue;
                    var center = layout.RoomCenters[room.Id - 1];
                    float dist = Geometry.Dist(X, Y, center.X, center.Y);
                    if (dist < bestDist)
                    {
                        best = room;
                        bestDist = dist;
                    }
                }
            }

            return best != null ? new InteractTarget { Kind = "room", Room = best } : null;
        }

        bool NearRadio(HotelLayout layout)
        {
            var radio = layout.LobbyRadio;
            return radio != null && Geometry.Dist(X, Y, radio.X, radio.Y) < 44;
        }

        bool NearPhone(HotelLayout layout)
        {
            var phone = layout.DeskPhone;
            return phone != null && Geometry.Dist(X, Y, phone.X, phone.Y) < 42;
        }

        bool NearDeskPc(HotelLayout layout)
        {
            var deskPc = layout.DeskPc;
            return deskPc != null && Geometry.Dist(X, Y, deskPc.X, deskPc.Y) < 42;
        }

        bool NearNewspaper(HotelLayout layout)
        {
            var paper = layout.Newspaper;
            return paper != null && Geometry.Dist(X, Y, paper.X, paper.Y) < 36;
        }

        bool NearDesk(HotelLayout layout, List<StaffNpc> staffList)
        {
            var desk = layout.FrontDesk;
            float deskDist = Geometry.Dist(X, Y, desk.X, desk.Y);
            bool deskBusy = false;
            if (staffList != null)
            {
                foreach (var staff in staffList)
                {
                    if (staff != null && (staff.Phase == "waiting_pay" || staff.Phase == "to_desk"))
                    {
                        deskBusy = true;
                        break;
                    }
                }
            }

            float deskRange = deskBusy ? 90f : 70f;
            if (deskDist < deskRange) return true;

            if (staffList == null) return false;
            foreach (var staff in staffList)
            {
                if (staff == null) continue;
                if (staff.Phase != "waiting_pay" && staff.Phase != "to_desk") continue;
                if (Geometry.Dist(X, Y, staff.X, staff.Y) < 56) return true;
            }

            return false;
        }

        bool NearOffice(HotelLayout layout)
        {
            var office = layout.Office;
            if (office == null) return false;
            bool inside = System.Math.Abs(X - office.X) < office.W / 2f - 4f &&
                          System.Math.Abs(Y - office.Y) < office.H / 2f - 4f;
            float doorDist = Geometry.Dist(X, Y, office.Door.X, office.Door.Y);
            return inside || doorDist < 40;
        }

        bool NearSign(HotelLayout layout)
        {
            var sign = layout.VacancySign;
            return sign != null && Geometry.Dist(X, Y, sign.X, sign.Y) < 72;
        }

        bool NearRoom(Room room, HotelLayout layout)
        {
            if (room == null || layout?.RoomCenters == null) return false;
            if (room.Id < 1 || room.Id > layout.RoomCenters.Count) return false;
            var center = layout.RoomCenters[room.Id - 1];
            return Geometry.Dist(X, Y, center.X, center.Y) < 88f;
        }
    }
}

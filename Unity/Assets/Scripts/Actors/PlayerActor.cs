using System.Collections.Generic;

namespace Vacancy
{
    public sealed class ActiveTask
    {
        public string Type;
        public Room Room;
        public float Progress;
        public float Duration;
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

        public PlayerActor(float x, float y)
        {
            X = x;
            Y = y;
        }

        public ActiveTask Update(GameInput input, float dt, HotelLayout layout, List<Room> rooms)
        {
            if (input.LookEnabled)
            {
                Yaw += input.LookX * WorldScale.LookSensitivity;
                Pitch = Geometry.Clamp(Pitch - input.LookY * WorldScale.LookSensitivity, -80f, 80f);
            }

            if (ActiveTask != null)
            {
                ActiveTask.Progress += GameConfig.HoursPerSecond * dt;
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

            if (!Pathing.CollidesWithRooms(nextX, Y, Radius, rooms, layout, allow)) X = nextX;
            if (!Pathing.CollidesWithRooms(X, nextY, Radius, rooms, layout, allow)) Y = nextY;

            var b = layout.Building;
            X = Geometry.Clamp(X, b.X + 20, b.X + b.W - 20);
            Y = Geometry.Clamp(Y, b.Y + 24, b.Y + b.H - 20);
            Pathing.ResolveRoomCollision(this, rooms, layout, allow);
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
        }

        public InteractTarget GetInteractTarget(List<Room> rooms, HotelLayout layout, List<StaffNpc> staffList, bool deskQueue = false)
        {
            const float interactRange = 88f;

            var radio = layout.LobbyRadio;
            if (radio != null && Geometry.Dist(X, Y, radio.X, radio.Y) < 44)
            {
                return new InteractTarget { Kind = "radio" };
            }

            var desk = layout.FrontDesk;
            float deskDist = Geometry.Dist(X, Y, desk.X, desk.Y);
            bool deskBusy = false;
            foreach (var staff in staffList)
            {
                if (staff != null && (staff.Phase == "waiting_pay" || staff.Phase == "to_desk"))
                {
                    deskBusy = true;
                    break;
                }
            }

            float deskRange = deskBusy ? 90f : 70f;
            if (deskDist < deskRange && (deskBusy || deskQueue))
            {
                return new InteractTarget { Kind = "desk" };
            }

            var paper = layout.Newspaper;
            if (paper != null && Geometry.Dist(X, Y, paper.X, paper.Y) < 46)
            {
                return new InteractTarget { Kind = "newspaper" };
            }

            if (deskDist < deskRange) return new InteractTarget { Kind = "desk" };

            foreach (var staff in staffList)
            {
                if (staff == null) continue;
                if (staff.Phase != "waiting_pay" && staff.Phase != "to_desk") continue;
                if (Geometry.Dist(X, Y, staff.X, staff.Y) < 56)
                {
                    return new InteractTarget { Kind = "desk" };
                }
            }

            var office = layout.Office;
            if (office != null)
            {
                bool inside = System.Math.Abs(X - office.X) < office.W / 2f - 4f &&
                              System.Math.Abs(Y - office.Y) < office.H / 2f - 4f;
                float doorDist = Geometry.Dist(X, Y, office.Door.X, office.Door.Y);
                if (inside || doorDist < 40) return new InteractTarget { Kind = "office" };
            }

            var sign = layout.VacancySign;
            if (sign != null && Geometry.Dist(X, Y, sign.X, sign.Y) < 55)
            {
                return new InteractTarget { Kind = "sign" };
            }

            Room best = null;
            float bestDist = float.PositiveInfinity;
            foreach (var room in rooms)
            {
                var center = layout.RoomCenters[room.Id - 1];
                float dist = Geometry.Dist(X, Y, center.X, center.Y);
                if (dist < interactRange && dist < bestDist)
                {
                    best = room;
                    bestDist = dist;
                }
            }

            return best != null ? new InteractTarget { Kind = "room", Room = best } : null;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Vacancy
{
    public static class Economy
    {
        public const string DeskReview = "review";

        static readonly string[] GuestNames =
        {
            "Alex", "Sam", "Jordan", "Riley", "Casey", "Morgan", "Taylor", "Quinn", "Jamie", "Drew"
        };

        static string PickGuestName()
        {
            return GuestNames[GameRng.NextInt(0, GuestNames.Length - 1)];
        }

        public static int GetDayRate(GameState state)
        {
            float bonus = Math.Max(0, state.Reputation - 50) * GameConfig.ReputationRateBonus;
            return (int)Math.Round(GameConfig.BaseRoomRate + bonus);
        }

        public static int GetRepairCost(GameState state, string repairLevel)
        {
            float fraction = GameConfig.RepairCostDayFractions.TryGetValue(repairLevel ?? "", out var value)
                ? value
                : GameConfig.RepairCostDayFractions["medium"];
            return Math.Max(1, (int)Math.Round(GetDayRate(state) * fraction));
        }

        public static bool CanAffordRepair(GameState state, Room room)
        {
            if (room?.RepairLevel == null) return false;
            return state.Money >= GetRepairCost(state, room.RepairLevel);
        }

        public static int? BeginRepairPayment(GameState state, Room room)
        {
            if (room.Status != "needs_repair" || room.RepairLevel == null) return null;
            if (room.RepairPaid) return 0;

            int cost = GetRepairCost(state, room.RepairLevel);
            if (state.Money < cost) return null;

            state.Money -= cost;
            room.RepairPaid = true;
            room.RepairCost = cost;
            return cost;
        }

        public static float GetArrivalChancePerSecond(GameState state)
        {
            float t = Geometry.Clamp(state.Reputation / 100f, 0f, 1f);
            float mult = GameConfig.ArrivalRepMinMult +
                         t * (GameConfig.ArrivalRepMaxMult - GameConfig.ArrivalRepMinMult);
            return GameConfig.ArrivalChancePerSecond * mult;
        }

        static string LevelLabel(string level)
        {
            if (string.IsNullOrEmpty(level)) return "unknown";
            return char.ToUpperInvariant(level[0]) + level.Substring(1);
        }

        static readonly string[] CarColors =
        {
            "#c45c2a", "#4a6a8a", "#6a3a38", "#3a4a38", "#5b8def", "#8a7355", "#2f6b3a", "#7a5a2a"
        };

        public static bool IsAtDesk(WaitingGuest guest)
        {
            return guest != null && (string.IsNullOrEmpty(guest.ArrivePhase) || guest.ArrivePhase == "waiting");
        }

        public static WaitingGuest FirstAtDesk(GameState state)
        {
            foreach (var guest in state.WaitingGuests)
            {
                if (IsAtDesk(guest)) return guest;
            }

            return null;
        }

        public static int CountAtDesk(GameState state)
        {
            int n = 0;
            foreach (var guest in state.WaitingGuests)
            {
                if (IsAtDesk(guest)) n++;
            }

            return n;
        }

        public static Guest FirstWaitingCheckout(GameState state)
        {
            foreach (var guest in state.ActiveGuests)
            {
                if (guest.Phase == "waiting_checkout") return guest;
            }

            return null;
        }

        public static int CountWaitingCheckout(GameState state)
        {
            int n = 0;
            foreach (var guest in state.ActiveGuests)
            {
                if (guest.Phase == "waiting_checkout") n++;
            }

            return n;
        }

        public static void BuyNewspaper(GameState state, string name)
        {
            int price = GameConfig.NewspaperPrice;
            state.Money += price;
            state.AddLog($"{name} bought a newspaper (−${price} / +${price} till).");
        }

        public static int CountInboundWaiting(GameState state)
        {
            int n = 0;
            foreach (var guest in state.WaitingGuests)
            {
                string phase = ArrivePhaseOf(guest);
                if (phase == "driving" || phase == "walking_in" || phase == "waiting") n++;
            }

            return n;
        }

        static string ArrivePhaseOf(WaitingGuest guest)
        {
            if (guest == null || string.IsNullOrEmpty(guest.ArrivePhase)) return "waiting";
            return guest.ArrivePhase;
        }

        static int FindFreeStall(GameState state)
        {
            var used = new bool[HotelLayout.StallCount];
            void Occupy(int index)
            {
                if (index >= 0 && index < used.Length) used[index] = true;
            }

            foreach (var guest in state.WaitingGuests) Occupy(guest.StallIndex);
            foreach (var guest in state.ActiveGuests) Occupy(guest.StallIndex);
            foreach (var car in state.Cars) Occupy(car.StallIndex);

            for (int i = 0; i < used.Length; i++)
            {
                if (!used[i]) return i;
            }

            return -1;
        }

        static GuestCar FindCar(GameState state, WaitingGuest guest)
        {
            if (guest == null) return null;
            foreach (var car in state.Cars)
            {
                if (guest.StallIndex >= 0 && car.StallIndex == guest.StallIndex) return car;
            }

            if (string.IsNullOrEmpty(guest.Name)) return null;
            foreach (var car in state.Cars)
            {
                if (car.Owner == guest.Name) return car;
            }

            return null;
        }

        static void RemoveCar(GameState state, WaitingGuest guest)
        {
            for (int i = state.Cars.Count - 1; i >= 0; i--)
            {
                var car = state.Cars[i];
                bool match = guest != null && guest.StallIndex >= 0 && car.StallIndex == guest.StallIndex;
                if (!match && guest != null && !string.IsNullOrEmpty(guest.Name) && car.Owner == guest.Name)
                {
                    match = true;
                }

                if (match) state.Cars.RemoveAt(i);
            }
        }

        static PathOptions LobbyWalkOptions(GameState state, WaitingGuest guest)
        {
            return new PathOptions
            {
                Rooms = state.Rooms,
                Radius = guest != null ? guest.Radius : 11f,
                FromFloor = guest?.FloorLevel ?? 0,
                ToFloor = 0
            };
        }

        static List<Point> PathViaEntrance(GameState state, HotelLayout layout, WaitingGuest guest, Point dest)
        {
            return Pathing.PathAlongCourt(layout, guest.X, guest.Y, dest, LobbyWalkOptions(state, guest));
        }

        static bool FollowDrive(GuestCar car, float dt, float speed)
        {
            if (car?.Path == null || car.Path.Count == 0) return true;
            if (car.Waypoint >= car.Path.Count) return true;

            var target = car.Path[car.Waypoint];
            float dist = Geometry.Dist(car.X, car.Y, target.X, target.Y);
            if (dist < 4f)
            {
                car.X = target.X;
                car.Y = target.Y;
                car.Waypoint++;
                return car.Waypoint >= car.Path.Count;
            }

            float travel = Math.Min(speed * dt, dist);
            car.X += (target.X - car.X) / dist * travel;
            car.Y += (target.Y - car.Y) / dist * travel;
            return false;
        }

        public static void BeginWalkOut(GameState state, HotelLayout layout, WaitingGuest guest)
        {
            if (guest == null) return;
            guest.ArrivePhase = "walking_out";
            guest.Path = new List<Point>();
            if (layout == null || guest.StallIndex < 0) return;
            guest.Path = PathViaEntrance(state, layout, guest, layout.StallPose(guest.StallIndex).WalkOut);
        }

        static void LogDeskArrival(GameState state, WaitingGuest guest)
        {
            state.AddLog($"{guest.Name} is at the desk. {guest.Claim}");
            foreach (var sign in Arrivals.RevealedSigns(guest))
            {
                state.AddLog(sign.Text);
            }
        }

        public static bool SpawnArrival(GameState state, HotelLayout layout)
        {
            if (!state.VacancyOpen) return false;
            if (layout == null || layout.Parking.W <= 0f) return false;
            if (CountInboundWaiting(state) >= GameConfig.MaxWaitingGuests) return false;

            int cleanRooms = 0;
            foreach (var room in state.Rooms)
            {
                if (room.Unlocked && room.Status == "clean") cleanRooms++;
            }

            if (cleanRooms <= CountInboundWaiting(state)) return false;

            int stall = FindFreeStall(state);
            if (stall < 0) return false;

            var highway = layout.HighwayEntry;
            var start = layout.DriveLaneCorner(highway.Y);
            string color = CarColors[GameRng.NextInt(0, CarColors.Length - 1)];

            var guest = Arrivals.CreateArrival(state, PickGuestName());
            guest.ArrivePhase = "driving";
            guest.StallIndex = stall;
            guest.CarColor = color;
            guest.FloorLevel = 0;
            guest.X = start.X;
            guest.Y = start.Y;
            guest.Path = new List<Point>();
            state.WaitingGuests.Add(guest);
            state.Cars.Add(new GuestCar
            {
                Owner = guest.Name,
                StallIndex = stall,
                X = start.X,
                Y = start.Y,
                Color = color,
                Stage = "inbound",
                Waypoint = 0,
                Path = layout.StallDriveIn(stall)
            });
            state.AddLog($"{guest.Name} is driving in from the highway.");
            return true;
        }

        public static void UpdateArrivals(GameState state, float dt, HotelLayout layout)
        {
            if (layout == null) return;
            float speed = GameConfig.GuestMoveSpeed;
            float carSpeed = speed * 2.2f;
            var leaving = new List<WaitingGuest>();
            int deskIndex = 0;

            foreach (var guest in state.WaitingGuests)
            {
                string phase = ArrivePhaseOf(guest);
                var car = FindCar(state, guest);

                if (phase == "driving")
                {
                    bool parked = car == null || FollowDrive(car, dt, carSpeed);
                    if (parked)
                    {
                        var pose = layout.StallPose(guest.StallIndex);
                        if (car != null)
                        {
                            car.Stage = "parked";
                            car.X = pose.Car.X;
                            car.Y = pose.Car.Y;
                            car.Path.Clear();
                            car.Waypoint = 0;
                        }

                        guest.ArrivePhase = "walking_in";
                        guest.X = pose.WalkOut.X;
                        guest.Y = pose.WalkOut.Y;
                        guest.FloorLevel = 0;
                        var slot = layout.CheckInLineSlot(CountAtDesk(state));
                        guest.Path = PathViaEntrance(state, layout, guest, slot);
                    }
                    else
                    {
                        guest.X = car.X;
                        guest.Y = car.Y;
                    }

                    continue;
                }

                if (phase == "walking_in")
                {
                    if (guest.Path == null || guest.Path.Count == 0)
                    {
                        var slot = layout.CheckInLineSlot(CountAtDesk(state));
                        guest.Path = PathViaEntrance(state, layout, guest, slot);
                    }

                    if (Pathing.FollowPath(guest, dt, state.Rooms, layout, null, speed))
                    {
                        guest.PaperOffered = true;
                        if (!guest.BoughtPaper && GameRng.NextFloat() < GameConfig.GuestPaperChance)
                        {
                            guest.ArrivePhase = "buying_paper";
                            guest.Path = Pathing.PathToNewspaper(layout, guest.X, guest.Y, LobbyWalkOptions(state, guest));
                        }
                        else
                        {
                            guest.ArrivePhase = "waiting";
                            guest.Path.Clear();
                            LogDeskArrival(state, guest);
                        }
                    }

                    continue;
                }

                if (phase == "buying_paper")
                {
                    if (guest.Path == null || guest.Path.Count == 0)
                    {
                        guest.Path = Pathing.PathToNewspaper(layout, guest.X, guest.Y, LobbyWalkOptions(state, guest));
                    }

                    if (guest.Path == null || guest.Path.Count == 0 ||
                        Pathing.FollowPath(guest, dt, state.Rooms, layout, null, speed) ||
                        Geometry.Dist(guest.X, guest.Y, layout.NewspaperApproach().X, layout.NewspaperApproach().Y) < 14f)
                    {
                        BuyNewspaper(state, guest.Name);
                        guest.BoughtPaper = true;
                        guest.ArrivePhase = "returning_from_paper";
                        var slot = layout.CheckInLineSlot(CountAtDesk(state));
                        guest.Path = Pathing.FindPath(layout, guest.X, guest.Y, slot, LobbyWalkOptions(state, guest));
                    }

                    continue;
                }

                if (phase == "returning_from_paper")
                {
                    var slot = layout.CheckInLineSlot(CountAtDesk(state));
                    if (guest.Path == null || guest.Path.Count == 0)
                    {
                        guest.Path = Pathing.FindPath(layout, guest.X, guest.Y, slot, LobbyWalkOptions(state, guest));
                    }

                    if (Pathing.FollowPath(guest, dt, state.Rooms, layout, null, speed) ||
                        Geometry.Dist(guest.X, guest.Y, slot.X, slot.Y) < 12f)
                    {
                        guest.ArrivePhase = "waiting";
                        guest.Path.Clear();
                        LogDeskArrival(state, guest);
                    }

                    continue;
                }

                if (phase == "waiting")
                {
                    var slot = layout.CheckInLineSlot(deskIndex++);
                    guest.X = slot.X;
                    guest.Y = slot.Y;
                    continue;
                }

                if (phase == "walking_out")
                {
                    if (guest.StallIndex < 0)
                    {
                        leaving.Add(guest);
                        continue;
                    }

                    var walk = layout.StallPose(guest.StallIndex).WalkOut;
                    if (guest.Path == null || guest.Path.Count == 0)
                    {
                        guest.Path = PathViaEntrance(state, layout, guest, walk);
                    }

                    bool arrived = Pathing.FollowPath(guest, dt, state.Rooms, layout, null, speed)
                                   || Geometry.Dist(guest.X, guest.Y, walk.X, walk.Y) < 12f;
                    if (arrived)
                    {
                        guest.ArrivePhase = "driving_away";
                        guest.X = walk.X;
                        guest.Y = walk.Y;
                        guest.Path.Clear();
                        var pose = layout.StallPose(guest.StallIndex);
                        if (car != null)
                        {
                            car.Stage = "outbound";
                            car.X = pose.Car.X;
                            car.Y = pose.Car.Y;
                            car.Waypoint = 0;
                            car.Path = layout.StallDriveOut(guest.StallIndex);
                        }
                    }

                    continue;
                }

                if (phase == "driving_away")
                {
                    bool gone = car == null || FollowDrive(car, dt, carSpeed);
                    if (!gone && car != null && car.Y >= layout.HighwayEntry.Y - 4f) gone = true;
                    if (gone)
                    {
                        leaving.Add(guest);
                    }
                    else
                    {
                        guest.X = car.X;
                        guest.Y = car.Y;
                    }
                }
            }

            foreach (var guest in leaving)
            {
                state.WaitingGuests.Remove(guest);
                RemoveCar(state, guest);
            }
        }

        public static void ProcessWaitingGuests(GameState state, float hoursPassed)
        {
            // Arrivals stay at the desk until the player admits or turns them away.
        }

        public static bool ToggleVacancy(GameState state)
        {
            state.VacancyOpen = !state.VacancyOpen;
            if (state.VacancyOpen)
            {
                state.AddLog("Sign flipped to VACANCY — travelers can arrive again.");
            }
            else
            {
                state.AddLog(
                    "Sign flipped to NO VACANCY — new guests will stop showing up. Anyone already waiting can still check in.");
            }

            Stage.Mark(state, "vacancySign");
            return state.VacancyOpen;
        }

        public static bool CheckInAtDesk(GameState state, HotelLayout layout, WaitingGuest chosenGuest = null)
        {
            if (chosenGuest != null && !state.WaitingGuests.Contains(chosenGuest)) return false;
            var waiting = chosenGuest ?? FirstAtDesk(state);
            if (waiting == null || !IsAtDesk(waiting))
            {
                state.AddLog("Nobody is waiting to check in.");
                return false;
            }

            Room cleanRoom = null;
            foreach (var room in state.Rooms)
            {
                if (room.Unlocked && room.Status == "clean")
                {
                    cleanRoom = room;
                    break;
                }
            }

            if (cleanRoom == null)
            {
                state.AddLog("No clean rooms available. Clean a room first!");
                return false;
            }

            if (!InventorySystem.CanStockRoom(state, cleanRoom, out var missing))
            {
                string label = GameConfig.InventoryItems.TryGetValue(missing, out var def) ? def.Label : missing;
                state.AddLog($"Can't check in — out of {label}. Order more on the office PC.");
                return false;
            }

            int stayDays = GameConfig.MinStayDays +
                           GameRng.NextInt(0, GameConfig.MaxStayDays - GameConfig.MinStayDays);
            state.WaitingGuests.Remove(waiting);
            var spawn = new Point(waiting.X, waiting.Y);
            var dest = layout.RoomInterior(cleanRoom.Id);

            InventorySystem.ConsumeCheckInSupplies(state, cleanRoom);

            cleanRoom.Status = "occupied";
            cleanRoom.GuestName = waiting.Name;
            cleanRoom.StayDays = stayDays;
            cleanRoom.StayRemainingHours = stayDays * GameConfig.StayIntervalHours;
            cleanRoom.PaymentsLeft = stayDays - 1;
            cleanRoom.NextIntervalPaymentIn = GameConfig.StayIntervalHours;
            cleanRoom.HasHiddenDamage = GameRng.NextFloat() < GameConfig.DamageChance;
            cleanRoom.DamageFound = false;
            cleanRoom.DirtLevel = null;
            cleanRoom.RepairLevel = null;
            cleanRoom.TpDayCounter = 0;

            state.ActiveGuests.Add(new Guest
            {
                Name = waiting.Name,
                Kind = waiting.Kind ?? GuestKind.Traveler,
                Marked = waiting.Marked,
                Phase = "walking_to_room",
                Nav = "to_door",
                RoomId = cleanRoom.Id,
                X = spawn.X,
                Y = spawn.Y,
                Radius = 11,
                Path = Pathing.PathToRoomDoor(layout, spawn.X, spawn.Y, cleanRoom.Id, new PathOptions { Radius = 11 }),
                TargetX = dest.X,
                TargetY = dest.Y,
                StayDays = stayDays,
                StayRemainingHours = stayDays * GameConfig.StayIntervalHours,
                PaymentsLeft = stayDays - 1,
                NextIntervalPaymentIn = GameConfig.StayIntervalHours,
                HasHiddenDamage = cleanRoom.HasHiddenDamage,
                StallIndex = waiting.StallIndex,
                CarColor = waiting.CarColor,
                BoughtPaper = waiting.BoughtPaper,
                PaperOffered = waiting.PaperOffered
            });

            string dayWord = stayDays == 1 ? "day" : "days";
            if (waiting.Kind == GuestKind.Survivor)
            {
                cleanRoom.PaymentsLeft = 0;
                state.AddLog($"{waiting.Name} is in Room {cleanRoom.Id}. No money changed hands.");
                if (state.Story != null)
                {
                    state.Story.Humanity = Math.Min(100, state.Story.Humanity + 3);
                }
            }
            else
            {
                int rate = GetDayRate(state);
                state.Money += rate;
                state.Reputation = Math.Min(100, state.Reputation + 1);
                state.AddLog(
                    $"{waiting.Name} checked in for Room {cleanRoom.Id} ({stayDays} {dayWord}, +${rate}). Walking to the room...");
            }

            Arrivals.ArmAdmittedThreat(state, waiting);
            Story.Hook(state, "checkIn", waiting, null, cleanRoom);
            Stage.Mark(state, "checkIn");
            return true;
        }

        public static bool CheckOutAtDesk(GameState state, HotelLayout layout)
        {
            Guest guest = null;
            foreach (var g in state.ActiveGuests)
            {
                if (g.Phase == "waiting_checkout")
                {
                    guest = g;
                    break;
                }
            }

            if (guest == null)
            {
                state.AddLog("Nobody is waiting to check out.");
                return false;
            }

            int bonus = guest.ReputationBonus ?? GameConfig.CheckoutReputationBonus;
            if (guest.UpsetCheckout)
            {
                bonus = Math.Max(0, bonus - 1);
                state.Reputation = Math.Min(100, state.Reputation + bonus);
                state.AddLog(
                    $"{guest.Name} checked out annoyed after a long wait. (+{bonus} reputation, −1 for the delay)");
            }
            else
            {
                state.Reputation = Math.Min(100, state.Reputation + bonus);
                state.AddLog($"{guest.Name} checked out happily. (+{bonus} reputation)");
            }

            var departing = new WaitingGuest
            {
                Name = guest.Name,
                Kind = guest.Kind ?? GuestKind.Traveler,
                StallIndex = guest.StallIndex,
                CarColor = guest.CarColor,
                ArrivePhase = "walking_out",
                X = guest.X,
                Y = guest.Y,
                Radius = guest.Radius,
                FloorLevel = guest.FloorLevel,
                FootY = guest.FootY
            };
            state.WaitingGuests.Add(departing);
            BeginWalkOut(state, layout, departing);
            state.ActiveGuests.Remove(guest);
            Story.Hook(state, "checkOut", null, guest);
            return true;
        }

        public static bool PayStaffAtDesk(GameState state, HotelLayout layout, List<StaffNpc> staffList)
        {
            var waiting = new List<StaffNpc>();
            foreach (var staff in staffList)
            {
                if (staff != null && (staff.Phase == "waiting_pay" || staff.Phase == "to_desk"))
                {
                    waiting.Add(staff);
                }
            }

            if (waiting.Count == 0) return false;

            waiting.Sort((a, b) =>
            {
                int aw = a.Phase == "waiting_pay" ? 0 : 1;
                int bw = b.Phase == "waiting_pay" ? 0 : 1;
                if (aw != bw) return aw - bw;
                return a.WagesOwed.CompareTo(b.WagesOwed);
            });

            var staffMember = waiting[0];
            int amount = staffMember.WagesOwed;
            if (amount > 0 && state.Money < amount)
            {
                state.AddLog($"Need ${amount} to pay {staffMember.Name}.");
                return false;
            }

            if (amount > 0) state.Money -= amount;
            staffMember.CollectPaycheck(state, layout);
            return true;
        }

        public static string HandleDeskAction(GameState state, HotelLayout layout, List<StaffNpc> staffList)
        {
            foreach (var staff in staffList)
            {
                if (staff != null && (staff.Phase == "waiting_pay" || staff.Phase == "to_desk"))
                {
                    PayStaffAtDesk(state, layout, staffList);
                    return "payday";
                }
            }

            if (FirstWaitingCheckout(state) != null || FirstAtDesk(state) != null)
            {
                state.AddLog("Use the desk PC to check guests in and out.");
                return "deskpc";
            }

            state.AddLog("Desk is clear. Flip the vacancy sign at the lot (V or E).");
            return null;
        }

        public static void ProcessStayBilling(GameState state, Room room, float hoursPassed)
        {
            if (room.Status != "occupied") return;
            if (room.PaymentsLeft == null || room.PaymentsLeft <= 0 || room.NextIntervalPaymentIn == null) return;

            room.NextIntervalPaymentIn -= hoursPassed;
            int billGuard = 0;
            while (room.NextIntervalPaymentIn <= 0 && room.PaymentsLeft > 0 && billGuard++ < 8)
            {
                int rate = GetDayRate(state);
                state.Money += rate;
                room.PaymentsLeft -= 1;
                room.NextIntervalPaymentIn += GameConfig.StayIntervalHours;
                state.AddLog(
                    $"{room.GuestName} in Room {room.Id} paid +${rate} for another {GameConfig.StayIntervalHours}h.");
            }

            foreach (var guest in state.ActiveGuests)
            {
                if (guest.RoomId == room.Id && (guest.Phase == "in_room" || guest.Phase == "walking_to_room"))
                {
                    guest.PaymentsLeft = room.PaymentsLeft ?? 0;
                    guest.NextIntervalPaymentIn = room.NextIntervalPaymentIn ?? 0;
                }
            }
        }

        static void BeginDeparture(GameState state, HotelLayout layout, Guest guest, Room room)
        {
            int stayDays = guest.StayDays > 0 ? guest.StayDays : room.StayDays ?? 1;
            guest.ReputationBonus = GameConfig.CheckoutReputationBonus + Math.Max(0, stayDays - 1);
            guest.UpsetCheckout = false;
            guest.WaitRemainingHours = null;
            guest.Phase = "walking_to_checkout";
            guest.Nav = "exit_room";
            guest.Path.Clear();

            int queueIndex = -1;
            foreach (var g in state.ActiveGuests)
            {
                if (g.Phase == "waiting_checkout" || g.Phase == "walking_to_checkout") queueIndex++;
            }

            var slot = layout.CheckoutLineSlot(Math.Max(0, queueIndex));
            guest.TargetX = slot.X;
            guest.TargetY = slot.Y;

            room.Status = "needs_inspection";
            room.GuestName = null;
            room.StayRemainingHours = null;
            room.StayDays = null;
            room.NextIntervalPaymentIn = null;
            room.PaymentsLeft = null;
            room.DirtLevel = GameConfig.PickWeightedLevel(GameConfig.DirtWeights);
            room.RepairLevel = null;
            room.HasHiddenDamage = guest.HasHiddenDamage;

            state.AddLog(
                $"{guest.Name} left Room {room.Id} and is heading to the front desk to check out. Inspect the room.");
        }

        public static void UpdateGuests(GameState state, float dt, HotelLayout layout)
        {
            float hoursPassed = GameConfig.HoursPerSecond * dt;
            int checkoutSlot = 0;
            float speed = GameConfig.GuestMoveSpeed;

            foreach (var guest in state.ActiveGuests)
            {
                object allow = GuestAllowRoom(guest);

                if (guest.Phase == "walking_to_room")
                {
                    if (guest.Nav == null) guest.Nav = "to_door";
                    Pathing.ResolveRoomCollision(guest, state.Rooms, layout, allow);

                    if (guest.Nav == "to_door")
                    {
                        if (guest.Path == null || guest.Path.Count == 0)
                        {
                            guest.Path = Pathing.PathToRoomDoor(layout, guest.X, guest.Y, guest.RoomId);
                        }

                        if (Pathing.FollowPath(guest, dt, state.Rooms, layout, null, speed))
                        {
                            var door = layout.RoomDoor(guest.RoomId);
                            guest.X = door.X;
                            guest.Y = door.Y;
                            guest.Nav = "enter_room";
                            guest.Path.Clear();
                        }
                    }
                    else
                    {
                        var dest = layout.RoomInterior(guest.RoomId);
                        guest.TargetX = dest.X;
                        guest.TargetY = dest.Y;
                        Pathing.SteerTo(guest, dest.X, dest.Y, dt, state.Rooms, layout, guest.RoomId, speed);
                        if (Geometry.Dist(guest.X, guest.Y, dest.X, dest.Y) < 22)
                        {
                            guest.X = dest.X;
                            guest.Y = dest.Y;
                            guest.Phase = "in_room";
                            guest.Nav = null;
                            guest.Path.Clear();
                            state.AddLog($"{guest.Name} arrived at Room {guest.RoomId}.");
                            MaybeQueueRoomPaperTrip(guest);
                        }
                    }

                    continue;
                }

                if (guest.Phase == "in_room")
                {
                    if (guest.RoomId < 1 || guest.RoomId > state.Rooms.Count) continue;
                    var room = state.Rooms[guest.RoomId - 1];
                    if (room.Status != "occupied") continue;

                    guest.StayRemainingHours -= hoursPassed;
                    room.StayRemainingHours = guest.StayRemainingHours;
                    ProcessStayBilling(state, room, hoursPassed);
                    if (guest.StayRemainingHours <= 0)
                    {
                        BeginDeparture(state, layout, guest, room);
                        continue;
                    }

                    if (guest.PaperTripIn != null && guest.PaperTripIn > 0 && !guest.BoughtPaper)
                    {
                        guest.PaperTripIn -= hoursPassed;
                        if (guest.PaperTripIn <= 0 && guest.StayRemainingHours > 2f)
                        {
                            BeginGuestPaperTrip(guest);
                        }
                    }

                    continue;
                }

                if (guest.Phase == "buying_paper")
                {
                    UpdateGuestPaperTrip(state, layout, guest, dt, speed);
                    continue;
                }

                if (guest.Phase == "walking_to_checkout")
                {
                    if (guest.Nav == null) guest.Nav = "exit_room";
                    var slot = layout.CheckoutLineSlot(checkoutSlot);
                    guest.TargetX = slot.X;
                    guest.TargetY = slot.Y;
                    Pathing.ResolveRoomCollision(guest, state.Rooms, layout, allow);

                    if (guest.Nav == "exit_room")
                    {
                        var door = layout.RoomDoor(guest.RoomId);
                        Pathing.SteerTo(guest, door.X, door.Y, dt, state.Rooms, layout, guest.RoomId, speed);
                        if (Geometry.Dist(guest.X, guest.Y, door.X, door.Y) < 16)
                        {
                            guest.Nav = "to_desk";
                            guest.Path = Pathing.PathGuestToDesk(layout, guest.X, guest.Y);
                        }
                    }
                    else if (guest.Nav == "to_desk")
                    {
                        if (guest.Path == null || guest.Path.Count == 0)
                        {
                            guest.Path = Pathing.PathGuestToDesk(layout, guest.X, guest.Y);
                        }

                        if (Pathing.FollowPath(guest, dt, state.Rooms, layout, null, speed))
                        {
                            guest.Nav = "to_slot";
                            guest.Path.Clear();
                        }
                    }
                    else
                    {
                        Pathing.SteerTo(guest, slot.X, slot.Y, dt, state.Rooms, layout, null, speed);
                        if (Geometry.Dist(guest.X, guest.Y, slot.X, slot.Y) < 10)
                        {
                            guest.Phase = "waiting_checkout";
                            guest.Nav = null;
                            guest.Path.Clear();
                            guest.WaitRemainingHours = GameConfig.WaitPatienceHours;
                            state.AddLog(
                                $"{guest.Name} is at the desk ready to check out ({GameConfig.WaitPatienceHours}h patience).");
                        }
                    }

                    checkoutSlot += 1;
                    continue;
                }

                if (guest.Phase == "waiting_checkout")
                {
                    var slot = layout.CheckoutLineSlot(checkoutSlot);
                    guest.X = slot.X;
                    guest.Y = slot.Y;
                    guest.WaitRemainingHours = (guest.WaitRemainingHours ?? 0) - hoursPassed;
                    if (!guest.UpsetCheckout && guest.WaitRemainingHours <= 0)
                    {
                        guest.UpsetCheckout = true;
                        guest.WaitRemainingHours = 0;
                        state.AddLog(
                            $"{guest.Name} is upset about the checkout wait — will give 1 less reputation.");
                    }

                    checkoutSlot += 1;
                }
            }
        }

        static object GuestAllowRoom(Guest guest)
        {
            if (guest.Nav == "enter_room" || guest.Nav == "exit_room") return guest.RoomId;
            return null;
        }

        static void MaybeQueueRoomPaperTrip(Guest guest)
        {
            if (guest == null || guest.BoughtPaper || guest.PaperTripIn != null) return;
            if (GameRng.NextFloat() < GameConfig.GuestPaperInRoomChance)
            {
                guest.PaperTripIn = 0.4f + GameRng.NextFloat() * 1.6f;
            }
            else
            {
                guest.PaperTripIn = 0f;
            }
        }

        static void BeginGuestPaperTrip(Guest guest)
        {
            guest.Phase = "buying_paper";
            guest.Nav = "exit_room";
            guest.Path.Clear();
            guest.PaperTripIn = 0f;
        }

        static void UpdateGuestPaperTrip(GameState state, HotelLayout layout, Guest guest, float dt, float speed)
        {
            object allow = GuestAllowRoom(guest);
            Pathing.ResolveRoomCollision(guest, state.Rooms, layout, allow);
            var box = layout.NewspaperApproach();

            if (guest.Nav == "exit_room")
            {
                var door = layout.RoomDoor(guest.RoomId);
                Pathing.SteerTo(guest, door.X, door.Y, dt, state.Rooms, layout, guest.RoomId, speed);
                if (Geometry.Dist(guest.X, guest.Y, door.X, door.Y) < 16)
                {
                    guest.Nav = "to_box";
                    guest.Path = Pathing.PathToNewspaper(layout, guest.X, guest.Y);
                }

                return;
            }

            if (guest.Nav == "to_box")
            {
                if (guest.Path == null || guest.Path.Count == 0)
                {
                    guest.Path = Pathing.PathToNewspaper(layout, guest.X, guest.Y);
                }

                if (guest.Path == null || guest.Path.Count == 0 ||
                    Pathing.FollowPath(guest, dt, state.Rooms, layout, null, speed) ||
                    Geometry.Dist(guest.X, guest.Y, box.X, box.Y) < 14f)
                {
                    BuyNewspaper(state, guest.Name);
                    guest.BoughtPaper = true;
                    guest.Nav = "to_door";
                    guest.Path = Pathing.PathToRoomDoor(layout, guest.X, guest.Y, guest.RoomId);
                }

                return;
            }

            if (guest.Nav == "to_door")
            {
                if (guest.Path == null || guest.Path.Count == 0)
                {
                    guest.Path = Pathing.PathToRoomDoor(layout, guest.X, guest.Y, guest.RoomId);
                }

                if (Pathing.FollowPath(guest, dt, state.Rooms, layout, null, speed))
                {
                    guest.Nav = "enter_room";
                    guest.Path.Clear();
                }

                return;
            }

            var dest = layout.RoomInterior(guest.RoomId);
            guest.TargetX = dest.X;
            guest.TargetY = dest.Y;
            Pathing.SteerTo(guest, dest.X, dest.Y, dt, state.Rooms, layout, guest.RoomId, speed);
            if (Geometry.Dist(guest.X, guest.Y, dest.X, dest.Y) < 22)
            {
                guest.X = dest.X;
                guest.Y = dest.Y;
                guest.Phase = "in_room";
                guest.Nav = null;
                guest.Path.Clear();
            }
        }

        public static void FinishInspection(GameState state, Room room, string byNpc = null)
        {
            if (room.Status != "needs_inspection") return;
            room.InspectProgress = 0;
            room.Worker = null;

            string dirt = room.DirtLevel ?? "medium";
            float cleanHrs = GameConfig.GetCleanHours(dirt);
            string who = string.IsNullOrEmpty(byNpc) ? "" : $" ({byNpc})";
            room.Status = "dirty";

            if (room.HasHiddenDamage)
            {
                room.DamageFound = true;
                room.RepairLevel = GameConfig.PickWeightedLevel(GameConfig.RepairWeights);
                state.Reputation = Math.Max(0, state.Reputation - 2);
                int repairCost = GetRepairCost(state, room.RepairLevel);
                state.AddLog(
                    $"Room {room.Id}: {LevelLabel(dirt)} dirt ({cleanHrs}h), then {LevelLabel(room.RepairLevel)} repair ({GameConfig.GetRepairHours(room.RepairLevel)}h) will cost ${repairCost}.{who}");
            }
            else
            {
                room.DamageFound = false;
                room.RepairLevel = null;
                state.AddLog(
                    $"Room {room.Id}: no damage. {LevelLabel(dirt)} dirt — clean takes {cleanHrs}h.{who}");
            }

            room.HasHiddenDamage = false;
            Stage.Mark(state, "roomWork");
        }

        public static void FinishCleaning(GameState state, Room room, string byNpc = null)
        {
            if (room.Status != "dirty") return;
            string level = room.DirtLevel;
            room.CleanProgress = 0;
            room.DirtLevel = null;
            room.Worker = null;
            string who = string.IsNullOrEmpty(byNpc) ? "" : $" ({byNpc})";

            if (room.DamageFound && !string.IsNullOrEmpty(room.RepairLevel))
            {
                room.Status = "needs_repair";
                room.RepairPaid = false;
                int repairCost = GetRepairCost(state, room.RepairLevel);
                state.AddLog(
                    $"Room {room.Id} cleaned ({LevelLabel(level)}). Needs {LevelLabel(room.RepairLevel)} repair ({GameConfig.GetRepairHours(room.RepairLevel)}h, ${repairCost}).{who}");
                Stage.Mark(state, "roomWork");
                return;
            }

            room.Status = "clean";
            room.RepairLevel = null;
            room.DamageFound = false;
            state.Reputation = Math.Min(100, state.Reputation + 1);
            state.AddLog($"Room {room.Id} is ready again ({LevelLabel(level)} clean done).{who}");
            Stage.Mark(state, "roomWork");
        }

        public static void FinishRepair(GameState state, Room room, string byNpc = null)
        {
            if (room.Status != "needs_repair") return;
            string level = room.RepairLevel;
            int cost = room.RepairCost ?? 0;
            if (!room.RepairPaid)
            {
                cost = GetRepairCost(state, level);
                state.Money -= cost;
                room.RepairPaid = true;
                room.RepairCost = cost;
            }

            room.Status = "clean";
            room.RepairLevel = null;
            room.RepairProgress = 0;
            room.DirtLevel = null;
            room.Worker = null;
            room.DamageFound = false;
            room.RepairPaid = false;
            room.RepairCost = null;
            state.Reputation = Math.Min(100, state.Reputation + 1);
            string who = string.IsNullOrEmpty(byNpc) ? "" : $" ({byNpc})";
            state.AddLog($"Room {room.Id} repaired ({LevelLabel(level)}). Parts −${cost}.{who}");
            Story.Hook(state, "repair", null, null, room);
            Stage.Mark(state, "roomWork");
        }

        public static void AdvanceTime(GameState state, float dt, HotelLayout layout, List<StaffNpc> staffList)
        {
            if (state.Paused) return;
            float hoursPassed = GameConfig.HoursPerSecond * dt;
            state.Hour += hoursPassed;

            while (state.Hour >= 24f)
            {
                state.Hour -= 24f;
                state.Day += 1;
                state.AddLog($"--- Day {state.Day} ---");
                InventorySystem.ProcessDailyInventory(state);
                Shelter.ProcessDailyShelter(state);
                Arrivals.ResolveConsequences(state);
                foreach (var staff in staffList)
                {
                    staff?.OnNewDay(state);
                }
            }

            Story.Update(state, hoursPassed);
            Media.Update(state, hoursPassed);
            InventorySystem.UpdateOrders(state, hoursPassed);
            UpdateGuests(state, dt, layout);
            ProcessWaitingGuests(state, hoursPassed);
            UpdateArrivals(state, dt, layout);

            if (GameRng.NextFloat() < GetArrivalChancePerSecond(state) * dt)
            {
                SpawnArrival(state, layout);
            }
        }

        public static bool UnlockRoom(GameState state)
        {
            int cost = state.RoomUnlockCost();
            Room room = null;
            foreach (var r in state.Rooms)
            {
                if (!r.Unlocked)
                {
                    room = r;
                    break;
                }
            }

            if (room == null)
            {
                state.AddLog("All rooms are already unlocked.");
                return false;
            }

            if (state.Money < cost)
            {
                state.AddLog($"Need ${cost} to unlock Room {room.Id}.");
                return false;
            }

            state.Money -= cost;
            room.Unlocked = true;
            room.Status = "clean";
            state.AddLog($"Unlocked Room {room.Id} for ${cost}.");
            Story.Hook(state, "unlock", null, null, room);
            Stage.MaybeAdvance(state);
            return true;
        }

        public static bool HireBob(GameState state)
        {
            if (state.BobHired || state.Money < GameConfig.HireBobCost) return false;
            state.Money -= GameConfig.HireBobCost;
            state.BobHired = true;
            state.AddLog(
                $"Hired Bob — repairs rooms. ${GameConfig.StaffDailyWage}/work day, payday every {GameConfig.StaffPayPeriodDays} days.");
            Stage.Mark(state, "hireStaff");
            return true;
        }

        public static bool HireMary(GameState state)
        {
            if (state.MaryHired || state.Money < GameConfig.HireMaryCost) return false;
            state.Money -= GameConfig.HireMaryCost;
            state.MaryHired = true;
            state.AddLog(
                $"Hired Mary — inspects & cleans. ${GameConfig.StaffDailyWage}/work day, payday every {GameConfig.StaffPayPeriodDays} days.");
            Stage.Mark(state, "hireStaff");
            return true;
        }
    }
}

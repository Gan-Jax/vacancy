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

        public static bool SpawnArrival(GameState state)
        {
            if (!state.VacancyOpen) return false;
            if (state.WaitingGuests.Count >= GameConfig.MaxWaitingGuests) return false;

            int cleanRooms = 0;
            foreach (var room in state.Rooms)
            {
                if (room.Unlocked && room.Status == "clean") cleanRooms++;
            }

            if (cleanRooms <= state.WaitingGuests.Count) return false;

            var guest = Arrivals.CreateArrival(state, PickGuestName());
            state.WaitingGuests.Add(guest);
            state.AddLog($"{guest.Name} is at the desk. {guest.Claim}");
            foreach (var sign in Arrivals.RevealedSigns(guest))
            {
                state.AddLog(sign.Text);
            }

            return true;
        }

        public static void ProcessWaitingGuests(GameState state, float hoursPassed)
        {
            if (state.WaitingGuests.Count == 0) return;
            var stillWaiting = new List<WaitingGuest>();
            foreach (var guest in state.WaitingGuests)
            {
                guest.WaitRemainingHours -= hoursPassed;
                if (guest.WaitRemainingHours > 0)
                {
                    stillWaiting.Add(guest);
                    continue;
                }

                if (guest.Kind == GuestKind.Survivor)
                {
                    if (state.Story != null)
                    {
                        state.Story.Humanity = Math.Max(0, state.Story.Humanity - 6);
                    }

                    state.AddLog(
                        $"{guest.Name} waited {GameConfig.WaitPatienceHours}h at the desk, then walked back out to the road alone.");
                }
                else if (guest.Kind == GuestKind.Wrong)
                {
                    state.AddLog($"{guest.Name} was gone from the lobby. Nobody saw them leave.");
                }
                else
                {
                    state.Reputation = Math.Max(0, state.Reputation - 3);
                    state.AddLog(
                        $"{guest.Name} left angry — waited over {GameConfig.WaitPatienceHours}h with no room. (−3 reputation)");
                }

                Story.Hook(state, "turnAway", guest);
            }

            state.WaitingGuests.Clear();
            state.WaitingGuests.AddRange(stillWaiting);
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

            return state.VacancyOpen;
        }

        public static bool CheckInAtDesk(GameState state, HotelLayout layout, WaitingGuest chosenGuest = null)
        {
            if (state.WaitingGuests.Count == 0)
            {
                state.AddLog("Nobody is waiting to check in.");
                return false;
            }

            if (chosenGuest != null && !state.WaitingGuests.Contains(chosenGuest)) return false;

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
            var waiting = chosenGuest ?? state.WaitingGuests[0];
            state.WaitingGuests.Remove(waiting);
            var spawn = layout.CheckInLineSlot(0);
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
                HasHiddenDamage = cleanRoom.HasHiddenDamage
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
            return true;
        }

        public static bool CheckOutAtDesk(GameState state)
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
            foreach (var guest in state.ActiveGuests)
            {
                if (guest.Phase == "waiting_checkout")
                {
                    CheckOutAtDesk(state);
                    return "checkout";
                }
            }

            foreach (var staff in staffList)
            {
                if (staff != null && (staff.Phase == "waiting_pay" || staff.Phase == "to_desk"))
                {
                    PayStaffAtDesk(state, layout, staffList);
                    return "payday";
                }
            }

            if (state.WaitingGuests.Count > 0) return DeskReview;

            state.AddLog("Desk is clear. Flip the vacancy sign at the bottom (V or E).");
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
                    if (guest.StayRemainingHours <= 0) BeginDeparture(state, layout, guest, room);
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
                            guest.Path = Pathing.PathToDeskHall(layout, guest.X, guest.Y);
                        }
                    }
                    else if (guest.Nav == "to_desk")
                    {
                        if (guest.Path == null || guest.Path.Count == 0)
                        {
                            guest.Path = Pathing.PathToDeskHall(layout, guest.X, guest.Y);
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
                return;
            }

            room.Status = "clean";
            room.RepairLevel = null;
            room.DamageFound = false;
            state.Reputation = Math.Min(100, state.Reputation + 1);
            state.AddLog($"Room {room.Id} is ready again ({LevelLabel(level)} clean done).{who}");
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

            if (GameRng.NextFloat() < GetArrivalChancePerSecond(state) * dt)
            {
                SpawnArrival(state);
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
            return true;
        }

        public static bool HireBob(GameState state)
        {
            if (state.BobHired || state.Money < GameConfig.HireBobCost) return false;
            state.Money -= GameConfig.HireBobCost;
            state.BobHired = true;
            state.AddLog(
                $"Hired Bob — repairs rooms. ${GameConfig.StaffDailyWage}/work day, payday every {GameConfig.StaffPayPeriodDays} days.");
            return true;
        }

        public static bool HireMary(GameState state)
        {
            if (state.MaryHired || state.Money < GameConfig.HireMaryCost) return false;
            state.Money -= GameConfig.HireMaryCost;
            state.MaryHired = true;
            state.AddLog(
                $"Hired Mary — inspects & cleans. ${GameConfig.StaffDailyWage}/work day, payday every {GameConfig.StaffPayPeriodDays} days.");
            return true;
        }
    }
}

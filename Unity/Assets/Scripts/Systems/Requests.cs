using System;
using System.Collections.Generic;

namespace Vacancy
{
    public static class Requests
    {
        static readonly string[] Kinds = { "towels", "soap", "toiletPaper", "ice", "noise", "late" };

        public static void MaybeQueue(Guest guest)
        {
            if (guest == null || guest.HasRequested || guest.RequestRollIn != null) return;
            if (GameRng.NextFloat() < GameConfig.GuestRequestChance)
            {
                guest.RequestRollIn = GameConfig.GuestRequestMinHours
                    + GameRng.NextFloat() * (GameConfig.GuestRequestMaxHours - GameConfig.GuestRequestMinHours);
            }
            else
            {
                guest.RequestRollIn = 0f;
            }
        }

        public static void Tick(GameState state, float hoursPassed)
        {
            if (state?.Requests == null) return;

            foreach (var guest in state.ActiveGuests)
            {
                if (guest.Phase == "walking_to_checkout" || guest.Phase == "waiting_checkout") continue;
                if (guest.HasRequested || guest.RequestRollIn == null || guest.RequestRollIn <= 0f) continue;
                if (guest.StayRemainingHours < 2f) continue;
                guest.RequestRollIn -= hoursPassed;
                if (guest.RequestRollIn > 0f) continue;
                Enqueue(state, guest);
            }

            for (int i = state.Requests.Count - 1; i >= 0; i--)
            {
                var req = state.Requests[i];
                req.HoursLeft -= hoursPassed;
                if (req.HoursLeft > 0f) continue;
                state.Requests.RemoveAt(i);
                state.Reputation = Math.Max(0, state.Reputation - GameConfig.GuestRequestRepPenalty);
                state.AddLog(
                    $"Missed {req.GuestName}'s call from Room {req.RoomId} ({req.Label.ToLowerInvariant()}). (−{GameConfig.GuestRequestRepPenalty} reputation)");
            }
        }

        public static GuestRequest OpenForRoom(GameState state, int roomId)
        {
            if (state?.Requests == null) return null;
            foreach (var req in state.Requests)
            {
                if (req.RoomId == roomId) return req;
            }

            return null;
        }

        public static bool TryFulfillAtRoom(GameState state, Room room)
        {
            var req = OpenForRoom(state, room?.Id ?? 0);
            if (req == null) return false;
            return Fulfill(state, req, "You");
        }

        public static void DropForRoom(GameState state, int roomId)
        {
            if (state?.Requests == null) return;
            for (int i = state.Requests.Count - 1; i >= 0; i--)
            {
                if (state.Requests[i].RoomId == roomId) state.Requests.RemoveAt(i);
            }
        }

        public static bool SendMary(GameState state)
        {
            if (state == null || state.Requests.Count == 0) return false;
            if (!state.MaryHired)
            {
                state.AddLog("Mary is not hired. Handle the room yourself, or hire her on the office PC.");
                return false;
            }

            return Fulfill(state, state.Requests[0], "Mary");
        }

        public static string PhoneBody(GameState state)
        {
            var lines = new List<string>();
            if (state?.Requests != null && state.Requests.Count > 0)
            {
                foreach (var req in state.Requests)
                {
                    int left = Math.Max(1, (int)Math.Ceiling(req.HoursLeft));
                    lines.Add($"Room {req.RoomId} — {req.GuestName}: {req.Label} ({left}h)");
                }

                lines.Add("");
                lines.Add("E on that room to handle it, or send Mary if she is hired.");
            }
            else
            {
                lines.Add("No guest calls waiting.");
            }

            var chores = HousekeepingLines(state);
            if (chores.Count > 0)
            {
                lines.Add("");
                lines.Add("Housekeeping");
                lines.AddRange(chores);
            }

            return string.Join("\n", lines);
        }

        public static List<string> HousekeepingLines(GameState state)
        {
            var lines = new List<string>();
            if (state?.Rooms == null) return lines;
            foreach (var room in state.Rooms)
            {
                if (!room.Unlocked) continue;
                if (room.Status == "needs_inspection") lines.Add($"Room {room.Id} needs inspection.");
                else if (room.Status == "dirty") lines.Add($"Room {room.Id} needs cleaning ({room.DirtLevel}).");
                else if (room.Status == "needs_repair") lines.Add($"Room {room.Id} needs repair ({room.RepairLevel}).");
            }

            return lines;
        }

        static void Enqueue(GameState state, Guest guest)
        {
            guest.HasRequested = true;
            guest.RequestRollIn = 0f;
            string kind = Kinds[GameRng.NextInt(0, Kinds.Length - 1)];
            var req = Build(state, guest, kind);
            if (req == null) return;
            state.Requests.Add(req);
            state.AddLog($"Phone: Room {req.RoomId} — {req.GuestName} wants {req.Label.ToLowerInvariant()}.");
        }

        static GuestRequest Build(GameState state, Guest guest, string kind)
        {
            string label;
            string supply = null;
            switch (kind)
            {
                case "towels":
                    label = "Extra towels";
                    supply = "towels";
                    break;
                case "soap":
                    label = "More soap";
                    supply = "soap";
                    break;
                case "toiletPaper":
                    label = "Toilet paper";
                    supply = "toiletPaper";
                    break;
                case "ice":
                    label = "Ice";
                    break;
                case "noise":
                    label = "The AC is too loud";
                    break;
                default:
                    label = "A later checkout";
                    break;
            }

            return new GuestRequest
            {
                Id = "req-" + state.NextRequestId++,
                RoomId = guest.RoomId,
                GuestName = guest.Name,
                Kind = kind,
                Label = label,
                SupplyId = supply,
                HoursLeft = GameConfig.GuestRequestExpireHours
            };
        }

        static bool Fulfill(GameState state, GuestRequest req, string by)
        {
            if (req == null) return false;
            if (!string.IsNullOrEmpty(req.SupplyId))
            {
                if (!InventorySystem.TrySpend(state, req.SupplyId, 1))
                {
                    string label = GameConfig.InventoryItems.TryGetValue(req.SupplyId, out var def)
                        ? def.Label
                        : req.SupplyId;
                    state.AddLog($"Can't fill Room {req.RoomId}'s request — out of {label}. Order more on the office PC.");
                    return false;
                }
            }

            if (req.Kind == "late")
            {
                foreach (var guest in state.ActiveGuests)
                {
                    if (guest.RoomId != req.RoomId) continue;
                    guest.StayRemainingHours += 4f;
                    if (guest.RoomId >= 1 && guest.RoomId <= state.Rooms.Count)
                    {
                        state.Rooms[guest.RoomId - 1].StayRemainingHours = guest.StayRemainingHours;
                    }

                    break;
                }
            }

            state.Requests.Remove(req);
            state.AddLog($"{by} handled Room {req.RoomId}: {req.Label.ToLowerInvariant()}.");
            return true;
        }
    }
}

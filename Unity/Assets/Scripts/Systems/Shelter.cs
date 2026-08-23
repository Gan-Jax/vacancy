using System;
using System.Collections.Generic;
using System.Text;

namespace Vacancy
{
    public sealed class ShelterState
    {
        public bool Unlocked;
        public bool DefenseActive;
        public readonly Dictionary<string, int> Stock = new Dictionary<string, int>();
        public float Integrity = 100f;
        public bool Powered = true;
        public int DaysHeld;
        public string LastShortage;
    }

    public static class Shelter
    {
        public static ShelterState Create()
        {
            var state = new ShelterState();
            foreach (var pair in GameConfig.ShelterItems)
            {
                state.Stock[pair.Key] = pair.Value.StartingStock;
            }

            return state;
        }

        public static List<string> ItemIds()
        {
            var ids = new List<string>();
            foreach (var pair in GameConfig.ShelterItems) ids.Add(pair.Key);
            return ids;
        }

        public static int GetStock(GameState state, string itemId)
        {
            if (state.Shelter == null) return 0;
            return state.Shelter.Stock.TryGetValue(itemId, out var qty) ? qty : 0;
        }

        public static void UnlockShelterSystems(GameState state)
        {
            if (state.Shelter == null || state.Shelter.Unlocked) return;
            state.Shelter.Unlocked = true;
            state.AddLog(
                "You inventory what the hotel actually has: water, food, fuel, medicine, and lumber.");
        }

        public static void ActivateDefense(GameState state)
        {
            if (state.Shelter == null || state.Shelter.DefenseActive) return;
            state.Shelter.DefenseActive = true;
            state.AddLog("Barricades go up over the ground-floor windows.");
        }

        public static int CountOccupants(GameState state)
        {
            int inRooms = 0;
            foreach (var room in state.Rooms)
            {
                if (room.Status == "occupied") inRooms++;
            }

            int staff = (state.BobHired ? 1 : 0) + (state.MaryHired ? 1 : 0);
            return inRooms + staff + 1;
        }

        static bool Spend(GameState state, string itemId, int qty)
        {
            int have = GetStock(state, itemId);
            int used = Math.Min(have, qty);
            state.Shelter.Stock[itemId] = have - used;
            return used == qty;
        }

        public static void ProcessDailyShelter(GameState state)
        {
            var shelter = state.Shelter;
            if (shelter == null || !shelter.Unlocked) return;

            int occupants = CountOccupants(state);
            var shortages = new List<string>();

            if (!Spend(state, "water", occupants * (int)GameConfig.WaterPerPersonPerDay))
            {
                shortages.Add("water");
            }

            if (!Spend(state, "food", occupants * (int)GameConfig.FoodPerPersonPerDay))
            {
                shortages.Add("food");
            }

            if (!Spend(state, "fuel", (int)GameConfig.FuelPerDay))
            {
                shortages.Add("fuel");
                shelter.Powered = false;
            }
            else
            {
                shelter.Powered = true;
            }

            if (shortages.Count > 0)
            {
                shelter.LastShortage = string.Join(", ", shortages);
                state.Reputation = Math.Max(0, state.Reputation - shortages.Count * 2);
                state.AddLog($"Ran short on {shelter.LastShortage} for {occupants} people. Morale drops.");
            }
            else
            {
                shelter.LastShortage = null;
                shelter.DaysHeld += 1;
            }

            if (shelter.DefenseActive)
            {
                shelter.Integrity = Math.Max(0, shelter.Integrity - GameConfig.NightlyIntegrityLoss);
                if (shelter.Integrity <= 0)
                {
                    state.Reputation = Math.Max(0, state.Reputation - 6);
                    state.AddLog("The barricades failed overnight. Something got in.");
                    shelter.Integrity = 10;
                }
                else if (shelter.Integrity < GameConfig.DangerIntegrity)
                {
                    state.AddLog(
                        $"Barricades down to {Math.Round(shelter.Integrity)}%. Reinforce them with lumber.");
                }
            }
        }

        public static bool ReinforceBarricades(GameState state, int lumber = 1)
        {
            var shelter = state.Shelter;
            if (shelter == null || !shelter.Unlocked) return false;
            if (shelter.Integrity >= 100)
            {
                state.AddLog("The barricades are already solid.");
                return false;
            }

            int available = Math.Min(lumber, GetStock(state, "lumber"));
            if (available <= 0)
            {
                state.AddLog("No lumber left. Order more before nightfall.");
                return false;
            }

            state.Shelter.Stock["lumber"] -= available;
            float gain = available * GameConfig.IntegrityPerLumber;
            shelter.Integrity = Math.Min(100, shelter.Integrity + gain);
            state.AddLog(
                $"Reinforced the barricades with {available} lumber ({Math.Round(shelter.Integrity)}%).");
            return true;
        }

        public static string HudSummary(GameState state)
        {
            var shelter = state.Shelter;
            if (shelter == null || !shelter.Unlocked) return "";

            var sb = new StringBuilder();
            bool first = true;
            foreach (var pair in GameConfig.ShelterItems)
            {
                if (!first) sb.Append(" · ");
                first = false;
                sb.Append(pair.Value.Label).Append(": ").Append(GetStock(state, pair.Key));
            }

            sb.Append(" · Barricades: ").Append(Math.Round(shelter.Integrity)).Append("%");
            if (!shelter.Powered) sb.Append(" · GENERATOR DOWN");
            return sb.ToString();
        }
    }
}

using System.Collections.Generic;

namespace Vacancy
{
    /// <summary>
    /// Balance knobs — same numbers as js/config.js.
    /// </summary>
    public static class GameConfig
    {
        public const int StartingMoney = 120;
        public const int StartingReputation = 50;
        public const float BaseRoomRate = 35f;
        public const float ReputationRateBonus = 0.5f;
        public const float DamageChance = 0.22f;
        public const int DamageChargeMin = 25;
        public const int DamageChargeMax = 80;

        public static readonly Dictionary<string, float> DirtHours = new Dictionary<string, float>
        {
            { "light", 1f }, { "medium", 2f }, { "heavy", 3f }
        };

        public static readonly Dictionary<string, float> RepairHours = new Dictionary<string, float>
        {
            { "light", 2f }, { "medium", 4f }, { "heavy", 8f }
        };

        public static readonly Dictionary<string, float> RepairCostDayFractions = new Dictionary<string, float>
        {
            { "light", 1f / 3f }, { "medium", 2f / 3f }, { "heavy", 1f }
        };

        public const float InspectHours = 0.5f;

        public static readonly Dictionary<string, int> DirtWeights = new Dictionary<string, int>
        {
            { "light", 50 }, { "medium", 35 }, { "heavy", 15 }
        };

        public static readonly Dictionary<string, int> RepairWeights = new Dictionary<string, int>
        {
            { "light", 50 }, { "medium", 35 }, { "heavy", 15 }
        };

        public const float NpcWorkMultiplier = 3f;
        public const float NpcMoveSpeed = 160f;
        public const float GuestMoveSpeed = 120f;
        public const float PlayerSpeed = 220f;
        public const float ArrivalChancePerSecond = 0.12f;
        public const float ArrivalRepMinMult = 0.45f;
        public const float ArrivalRepMaxMult = 1.85f;
        public const int CheckoutReputationBonus = 3;
        public const int MaxWaitingGuests = 3;
        public const int NewspaperPrice = 1;
        public const float GuestPaperChance = 0.22f;
        public const float GuestPaperInRoomChance = 0.14f;
        public const float WaitPatienceHours = 4f;
        public const float StayIntervalHours = 12f;
        public const int MinStayDays = 1;
        public const int MaxStayDays = 3;
        public const float HoursPerSecond = 1f / 3f;
        public const int RoomUnlockBaseCost = 150;
        public const int RoomUnlockCostStep = 100;
        public const int HireBobCost = 150;
        public const int HireMaryCost = 120;
        public const int StaffDailyWage = 10;
        public const int StaffPayPeriodDays = 7;
        public const int MaxRooms = 12;
        public const int StartingUnlockedRooms = 3;
        public const float RoomWidth = 118f;
        public const float RoomHeight = 92f;
        public const float InventoryDeliveryHours = 24f;

        public static readonly Dictionary<string, InventoryItemDef> InventoryItems =
            new Dictionary<string, InventoryItemDef>
            {
                { "towels", new InventoryItemDef("Towels", 10, 20, 10, InventoryReplace.EveryStays(10)) },
                { "soap", new InventoryItemDef("Soap", 1, 50, 25, InventoryReplace.EveryStay) },
                { "shampoo", new InventoryItemDef("Shampoo", 1, 50, 25, InventoryReplace.EveryStay) },
                { "conditioner", new InventoryItemDef("Conditioner", 1, 50, 25, InventoryReplace.EveryStay) },
                { "toiletPaper", new InventoryItemDef("Toilet paper", 2, 30, 12, InventoryReplace.EveryDays(3)) }
            };

        public static readonly Dictionary<string, float> HintIntervalByAct = new Dictionary<string, float>
        {
            { "normalcy", 22f },
            { "unease", 15f },
            { "disruption", 11f },
            { "collapse", 9f },
            { "shelter", 9f }
        };

        public static readonly Dictionary<string, float> MarkedChanceByAct = new Dictionary<string, float>
        {
            { "normalcy", 0.08f },
            { "unease", 0.2f },
            { "disruption", 0.34f },
            { "collapse", 0.5f },
            { "shelter", 0.55f }
        };

        public static readonly Dictionary<string, ShelterItemDef> ShelterItems =
            new Dictionary<string, ShelterItemDef>
            {
                { "water", new ShelterItemDef("Water", 3, 0, 20) },
                { "food", new ShelterItemDef("Food", 4, 0, 20) },
                { "fuel", new ShelterItemDef("Fuel", 6, 0, 10) },
                { "medicine", new ShelterItemDef("Medicine", 12, 0, 5) },
                { "lumber", new ShelterItemDef("Lumber", 8, 0, 10) }
            };

        public const float WaterPerPersonPerDay = 1f;
        public const float FoodPerPersonPerDay = 1f;
        public const float FuelPerDay = 2f;
        public const float NightlyIntegrityLoss = 6f;
        public const float IntegrityPerLumber = 8f;
        public const float DangerIntegrity = 40f;

        public static string PickWeightedLevel(Dictionary<string, int> weights)
        {
            int total = 0;
            foreach (var pair in weights) total += pair.Value;
            float roll = GameRng.NextFloat() * total;
            foreach (var pair in weights)
            {
                roll -= pair.Value;
                if (roll <= 0f) return pair.Key;
            }

            foreach (var pair in weights) return pair.Key;
            return "medium";
        }

        public static float GetCleanHours(string dirtLevel)
        {
            return DirtHours.TryGetValue(dirtLevel ?? "", out var hours) ? hours : DirtHours["medium"];
        }

        public static float GetRepairHours(string repairLevel)
        {
            return RepairHours.TryGetValue(repairLevel ?? "", out var hours) ? hours : RepairHours["medium"];
        }

        public static float GetTaskHours(string type, Room room, bool forNpc)
        {
            float hours = InspectHours;
            if (type == "clean") hours = GetCleanHours(room.DirtLevel);
            if (type == "repair") hours = GetRepairHours(room.RepairLevel);
            if (forNpc) hours *= NpcWorkMultiplier;
            return hours;
        }
    }

    public readonly struct InventoryReplace
    {
        public readonly string Kind;
        public readonly int Count;

        InventoryReplace(string kind, int count)
        {
            Kind = kind;
            Count = count;
        }

        public static InventoryReplace EveryStay => new InventoryReplace("stay", 1);
        public static InventoryReplace EveryStays(int n) => new InventoryReplace("stays", n);
        public static InventoryReplace EveryDays(int n) => new InventoryReplace("days", n);
    }

    public sealed class InventoryItemDef
    {
        public readonly string Label;
        public readonly int UnitCost;
        public readonly int StartingStock;
        public readonly int OrderPack;
        public readonly InventoryReplace ReplaceEvery;

        public InventoryItemDef(string label, int unitCost, int startingStock, int orderPack, InventoryReplace replaceEvery)
        {
            Label = label;
            UnitCost = unitCost;
            StartingStock = startingStock;
            OrderPack = orderPack;
            ReplaceEvery = replaceEvery;
        }
    }

    public sealed class ShelterItemDef
    {
        public readonly string Label;
        public readonly int UnitCost;
        public readonly int StartingStock;
        public readonly int OrderPack;

        public ShelterItemDef(string label, int unitCost, int startingStock, int orderPack)
        {
            Label = label;
            UnitCost = unitCost;
            StartingStock = startingStock;
            OrderPack = orderPack;
        }
    }

    public sealed class OrderableEntry
    {
        public string Kind;
        public string Label;
        public int UnitCost;
        public int StartingStock;
        public int OrderPack;
    }

    public static class GameRng
    {
        static readonly System.Random Rng = new System.Random();

        public static float NextFloat() => (float)Rng.NextDouble();

        public static int NextInt(int minInclusive, int maxInclusive)
        {
            return Rng.Next(minInclusive, maxInclusive + 1);
        }
    }
}

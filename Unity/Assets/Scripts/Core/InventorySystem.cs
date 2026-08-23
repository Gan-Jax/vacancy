using System.Collections.Generic;
using System.Text;

namespace Vacancy
{
    public sealed class PendingOrder
    {
        public string Id;
        public Dictionary<string, int> Items;
        public int Cost;
        public float HoursLeft;
    }

    public sealed class InventoryState
    {
        public readonly Dictionary<string, int> Stock = new Dictionary<string, int>();
        public readonly List<PendingOrder> PendingOrders = new List<PendingOrder>();
        public int NextOrderId = 1;
    }

    public static class InventorySystem
    {
        public static InventoryState Create()
        {
            var state = new InventoryState();
            foreach (var pair in GameConfig.InventoryItems)
            {
                state.Stock[pair.Key] = pair.Value.StartingStock;
            }

            return state;
        }

        public static List<string> ItemIds()
        {
            var ids = new List<string>();
            foreach (var pair in GameConfig.InventoryItems) ids.Add(pair.Key);
            return ids;
        }

        public static OrderableEntry LookupOrderable(GameState state, string itemId)
        {
            if (GameConfig.InventoryItems.TryGetValue(itemId, out var inv))
            {
                return new OrderableEntry
                {
                    Kind = "inventory",
                    Label = inv.Label,
                    UnitCost = inv.UnitCost,
                    StartingStock = inv.StartingStock,
                    OrderPack = inv.OrderPack
                };
            }

            if (state.Shelter != null && state.Shelter.Unlocked &&
                GameConfig.ShelterItems.TryGetValue(itemId, out var shelter))
            {
                return new OrderableEntry
                {
                    Kind = "shelter",
                    Label = shelter.Label,
                    UnitCost = shelter.UnitCost,
                    StartingStock = shelter.StartingStock,
                    OrderPack = shelter.OrderPack
                };
            }

            return null;
        }

        public static List<string> OrderableItemIds(GameState state)
        {
            var ids = ItemIds();
            if (state.Shelter != null && state.Shelter.Unlocked)
            {
                foreach (var pair in GameConfig.ShelterItems) ids.Add(pair.Key);
            }

            return ids;
        }

        static void DepositOrderItem(GameState state, string itemId, int qty)
        {
            if (GameConfig.InventoryItems.ContainsKey(itemId))
            {
                int current = GetStock(state, itemId);
                state.Inventory.Stock[itemId] = current + qty;
                return;
            }

            if (GameConfig.ShelterItems.ContainsKey(itemId) && state.Shelter != null)
            {
                int current = Shelter.GetStock(state, itemId);
                state.Shelter.Stock[itemId] = current + qty;
            }
        }

        static string OrderItemLabel(string itemId)
        {
            if (GameConfig.InventoryItems.TryGetValue(itemId, out var inv)) return inv.Label;
            if (GameConfig.ShelterItems.TryGetValue(itemId, out var shelter)) return shelter.Label;
            return itemId;
        }

        public static int GetStock(GameState state, string itemId)
        {
            return state.Inventory.Stock.TryGetValue(itemId, out var qty) ? qty : 0;
        }

        public static Dictionary<string, int> SuppliesNeededForCheckIn(Room room)
        {
            var needed = new Dictionary<string, int>
            {
                { "soap", 1 },
                { "shampoo", 1 },
                { "conditioner", 1 }
            };

            int sinceTowel = room.StaysSinceTowel + 1;
            if (sinceTowel >= 10) needed["towels"] = 1;
            return needed;
        }

        public static bool CanStockRoom(GameState state, Room room, out string missing)
        {
            foreach (var pair in SuppliesNeededForCheckIn(room))
            {
                if (GetStock(state, pair.Key) < pair.Value)
                {
                    missing = pair.Key;
                    return false;
                }
            }

            missing = null;
            return true;
        }

        public static bool ConsumeCheckInSupplies(GameState state, Room room)
        {
            if (!CanStockRoom(state, room, out _)) return false;

            var needed = SuppliesNeededForCheckIn(room);
            foreach (var pair in needed)
            {
                state.Inventory.Stock[pair.Key] -= pair.Value;
            }

            room.StayCount += 1;
            room.StaysSinceTowel = needed.ContainsKey("towels") ? 0 : room.StaysSinceTowel + 1;
            return true;
        }

        public static void ProcessDailyInventory(GameState state)
        {
            foreach (var room in state.Rooms)
            {
                if (!room.Unlocked) continue;
                if (room.Status != "occupied")
                {
                    room.TpDayCounter = 0;
                    continue;
                }

                room.TpDayCounter += 1;
                if (room.TpDayCounter < 3) continue;
                room.TpDayCounter = 0;

                if (GetStock(state, "toiletPaper") > 0)
                {
                    state.Inventory.Stock["toiletPaper"] -= 1;
                    state.AddLog(
                        $"Room {room.Id} restocked toilet paper (−$2 supply). {GetStock(state, "toiletPaper")} left.");
                }
                else
                {
                    state.Reputation = System.Math.Max(0, state.Reputation - 1);
                    state.AddLog(
                        "Room " + room.Id +
                        " is out of toilet paper — guest annoyed (−1 reputation). Order more at the office PC.");
                }
            }
        }

        public static void UpdateOrders(GameState state, float hoursPassed)
        {
            if (state.Inventory.PendingOrders.Count == 0) return;

            var stillPending = new List<PendingOrder>();
            foreach (var order in state.Inventory.PendingOrders)
            {
                order.HoursLeft -= hoursPassed;
                if (order.HoursLeft > 0)
                {
                    stillPending.Add(order);
                    continue;
                }

                foreach (var pair in order.Items)
                {
                    DepositOrderItem(state, pair.Key, pair.Value);
                }

                var parts = new List<string>();
                foreach (var pair in order.Items)
                {
                    if (pair.Value <= 0) continue;
                    parts.Add($"{pair.Value} {OrderItemLabel(pair.Key)}");
                }

                state.AddLog($"Supply delivery arrived: {string.Join(", ", parts)}.");
            }

            state.Inventory.PendingOrders.Clear();
            state.Inventory.PendingOrders.AddRange(stillPending);
        }

        public static bool PlaceOrder(GameState state, Dictionary<string, int> quantities)
        {
            var items = new Dictionary<string, int>();
            int cost = 0;
            bool any = false;

            foreach (var pair in quantities)
            {
                var entry = LookupOrderable(state, pair.Key);
                if (entry == null) continue;
                int qty = pair.Value < 0 ? 0 : pair.Value;
                if (qty <= 0) continue;
                items[pair.Key] = qty;
                cost += qty * entry.UnitCost;
                any = true;
            }

            if (!any)
            {
                state.AddLog("Select at least one item to order.");
                return false;
            }

            if (state.Money < cost)
            {
                state.AddLog($"Need ${cost} for that supply order.");
                return false;
            }

            state.Money -= cost;
            state.Inventory.PendingOrders.Add(new PendingOrder
            {
                Id = $"ord-{state.Inventory.NextOrderId++}",
                Items = items,
                Cost = cost,
                HoursLeft = GameConfig.InventoryDeliveryHours
            });

            var parts = new List<string>();
            foreach (var pair in items)
            {
                parts.Add($"{pair.Value}× {OrderItemLabel(pair.Key)}");
            }

            state.AddLog(
                $"Ordered supplies (−${cost}): {string.Join(", ", parts)}. Delivery in {GameConfig.InventoryDeliveryHours}h.");
            Story.Hook(state, "order");
            return true;
        }

        public static string HudSummary(GameState state)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (var pair in GameConfig.InventoryItems)
            {
                if (!first) sb.Append(" · ");
                first = false;
                sb.Append(pair.Value.Label).Append(": ").Append(GetStock(state, pair.Key));
            }

            return sb.ToString();
        }
    }
}

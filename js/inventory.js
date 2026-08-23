import { CONFIG } from "./config.js";
import { addLog } from "./state.js";
import { storyHook } from "./story.js";

export function createInventoryState() {
  const stock = {};
  for (const [id, item] of Object.entries(CONFIG.inventoryItems)) {
    stock[id] = item.startingStock;
  }
  return {
    stock,
    /** @type {{ id: string, items: Record<string, number>, cost: number, hoursLeft: number }[]} */
    pendingOrders: [],
    nextOrderId: 1,
  };
}

export function inventoryItemIds() {
  return Object.keys(CONFIG.inventoryItems);
}

/**
 * Orders cover hotel supplies plus, once the story unlocks them, shelter
 * resources. Both flow through the same PC panel and delivery timer.
 */
export function lookupOrderable(state, itemId) {
  if (CONFIG.inventoryItems[itemId]) {
    return { def: CONFIG.inventoryItems[itemId], kind: "inventory" };
  }
  if (CONFIG.shelterItems[itemId] && state.shelter?.unlocked) {
    return { def: CONFIG.shelterItems[itemId], kind: "shelter" };
  }
  return null;
}

export function orderableItemIds(state) {
  const ids = inventoryItemIds();
  if (state.shelter?.unlocked) {
    return ids.concat(Object.keys(CONFIG.shelterItems));
  }
  return ids;
}

function depositOrderItem(state, itemId, qty) {
  if (CONFIG.inventoryItems[itemId]) {
    state.inventory.stock[itemId] = (state.inventory.stock[itemId] || 0) + qty;
    return;
  }
  if (CONFIG.shelterItems[itemId] && state.shelter) {
    state.shelter.stock[itemId] = (state.shelter.stock[itemId] || 0) + qty;
  }
}

function orderItemLabel(itemId) {
  return (
    CONFIG.inventoryItems[itemId]?.label ||
    CONFIG.shelterItems[itemId]?.label ||
    itemId
  );
}

export function getStock(state, itemId) {
  return state.inventory.stock[itemId] ?? 0;
}

/** What must be in stock to prepare a room for check-in. */
export function suppliesNeededForCheckIn(room) {
  const needed = { soap: 1, shampoo: 1, conditioner: 1 };
  // Towels last 10 stays; replace on the stay that hits the limit
  const sinceTowel = (room.staysSinceTowel || 0) + 1;
  if (sinceTowel >= 10) needed.towels = 1;
  return needed;
}

export function canStockRoom(state, room) {
  const needed = suppliesNeededForCheckIn(room);
  for (const [id, qty] of Object.entries(needed)) {
    if (getStock(state, id) < qty) {
      return { ok: false, missing: id, needed };
    }
  }
  return { ok: true, needed };
}

/** Consume check-in supplies. Returns false if stock was insufficient. */
export function consumeCheckInSupplies(state, room) {
  const check = canStockRoom(state, room);
  if (!check.ok) return false;

  for (const [id, qty] of Object.entries(check.needed)) {
    state.inventory.stock[id] -= qty;
  }
  room.stayCount = (room.stayCount || 0) + 1;
  if (check.needed.towels) {
    room.staysSinceTowel = 0;
  } else {
    room.staysSinceTowel = (room.staysSinceTowel || 0) + 1;
  }
  return true;
}

/**
 * Toilet paper: every 3 calendar days while a room is occupied, burn 1 roll.
 * Called once per day rollover.
 */
export function processDailyInventory(state) {
  for (const room of state.rooms) {
    if (!room.unlocked) continue;
    if (room.status !== "occupied") {
      room.tpDayCounter = 0;
      continue;
    }

    room.tpDayCounter = (room.tpDayCounter || 0) + 1;
    if (room.tpDayCounter < 3) continue;
    room.tpDayCounter = 0;

    if (getStock(state, "toiletPaper") > 0) {
      state.inventory.stock.toiletPaper -= 1;
      addLog(
        state,
        `Room ${room.id} restocked toilet paper (−$2 supply). ${getStock(state, "toiletPaper")} left.`
      );
    } else {
      state.reputation = Math.max(0, state.reputation - 1);
      addLog(
        state,
        `Room ${room.id} is out of toilet paper — guest annoyed (−1 reputation). Order more at the office PC.`
      );
    }
  }
}

/** Tick pending deliveries. hoursPassed is in game hours. */
export function updateInventoryOrders(state, hoursPassed) {
  if (!state.inventory.pendingOrders.length) return;

  const stillPending = [];
  for (const order of state.inventory.pendingOrders) {
    order.hoursLeft -= hoursPassed;
    if (order.hoursLeft > 0) {
      stillPending.push(order);
      continue;
    }

    for (const [id, qty] of Object.entries(order.items)) {
      depositOrderItem(state, id, qty);
    }
    const parts = Object.entries(order.items)
      .filter(([, q]) => q > 0)
      .map(([id, q]) => `${q} ${orderItemLabel(id)}`)
      .join(", ");
    addLog(state, `Supply delivery arrived: ${parts}.`);
  }
  state.inventory.pendingOrders = stillPending;
}

/**
 * Place an order from the office PC. Pays immediately; arrives in 24h.
 * quantities: { soap: 25, towels: 10, ... }
 */
export function placeInventoryOrder(state, quantities) {
  const items = {};
  let cost = 0;
  let any = false;

  for (const [id, rawQty] of Object.entries(quantities)) {
    const entry = lookupOrderable(state, id);
    if (!entry) continue;
    const qty = Math.max(0, Math.floor(Number(rawQty) || 0));
    if (qty <= 0) continue;
    items[id] = qty;
    cost += qty * entry.def.unitCost;
    any = true;
  }

  if (!any) {
    addLog(state, "Select at least one item to order.");
    return false;
  }
  if (state.money < cost) {
    addLog(state, `Need $${cost} for that supply order.`);
    return false;
  }

  state.money -= cost;
  const order = {
    id: `ord-${state.inventory.nextOrderId++}`,
    items,
    cost,
    hoursLeft: CONFIG.inventoryDeliveryHours,
  };
  state.inventory.pendingOrders.push(order);

  const parts = Object.entries(items)
    .map(([id, q]) => `${q}× ${orderItemLabel(id)}`)
    .join(", ");
  addLog(
    state,
    `Ordered supplies (−$${cost}): ${parts}. Delivery in ${CONFIG.inventoryDeliveryHours}h.`
  );
  storyHook(state, "order", { items, cost });
  return true;
}

export function inventoryHudSummary(state) {
  return inventoryItemIds()
    .map((id) => {
      const def = CONFIG.inventoryItems[id];
      return `${def.label}: ${getStock(state, id)}`;
    })
    .join(" · ");
}

import { CONFIG } from "./config.js";
import { addLog } from "./state.js";

/**
 * Shelter management — the late-game evolution of the supply closet.
 *
 * Dormant until the story unlocks it, so early play is untouched. After that,
 * headcount burns water and food, the generator burns fuel, and the barricades
 * lose integrity every night you do not maintain them.
 */

export function createShelterState() {
  const stock = {};
  for (const id of Object.keys(CONFIG.shelterItems)) {
    stock[id] = CONFIG.shelterItems[id].startingStock;
  }
  return {
    /** Ordering and tracking becomes available when the city goes dark. */
    unlocked: false,
    /** Nightly attrition and threat checks start at collapse. */
    defenseActive: false,
    stock,
    integrity: 100,
    powered: true,
    /** Days survived with everyone fed and the doors holding. */
    daysHeld: 0,
    lastShortage: null,
  };
}

export function shelterItemIds() {
  return Object.keys(CONFIG.shelterItems);
}

export function getShelterStock(state, itemId) {
  return state.shelter?.stock?.[itemId] ?? 0;
}

export function unlockShelterSystems(state) {
  if (!state.shelter || state.shelter.unlocked) return;
  state.shelter.unlocked = true;
  addLog(
    state,
    "You inventory what the hotel actually has: water, food, fuel, medicine, lumber."
  );
}

export function activateDefense(state) {
  if (!state.shelter || state.shelter.defenseActive) return;
  state.shelter.defenseActive = true;
  addLog(state, "Barricades go up over the ground-floor windows.");
}

/** Everyone the shelter is currently keeping alive. */
export function countOccupants(state) {
  const inRooms = state.rooms.filter((r) => r.status === "occupied").length;
  const staff = (state.bobHired ? 1 : 0) + (state.maryHired ? 1 : 0);
  return inRooms + staff + 1;
}

function spend(state, itemId, qty) {
  const have = getShelterStock(state, itemId);
  const used = Math.min(have, qty);
  state.shelter.stock[itemId] = have - used;
  return used === qty;
}

/** Called once per day rollover. */
export function processDailyShelter(state) {
  const shelter = state.shelter;
  if (!shelter?.unlocked) return;

  const occupants = countOccupants(state);
  const use = CONFIG.shelterUse;
  const shortages = [];

  if (!spend(state, "water", occupants * use.waterPerPersonPerDay)) {
    shortages.push("water");
  }
  if (!spend(state, "food", occupants * use.foodPerPersonPerDay)) {
    shortages.push("food");
  }
  if (!spend(state, "fuel", use.fuelPerDay)) {
    shortages.push("fuel");
    shelter.powered = false;
  } else {
    shelter.powered = true;
  }

  if (shortages.length) {
    shelter.lastShortage = shortages.join(", ");
    state.reputation = Math.max(0, state.reputation - shortages.length * 2);
    addLog(
      state,
      `Ran short on ${shelter.lastShortage} for ${occupants} people. Morale drops.`
    );
  } else {
    shelter.lastShortage = null;
    shelter.daysHeld += 1;
  }

  if (shelter.defenseActive) {
    const loss = CONFIG.shelterDefense.nightlyIntegrityLoss;
    shelter.integrity = Math.max(0, shelter.integrity - loss);
    if (shelter.integrity <= 0) {
      state.reputation = Math.max(0, state.reputation - 6);
      addLog(state, "The barricades failed overnight. Something got in.");
      shelter.integrity = 10;
    } else if (shelter.integrity < CONFIG.shelterDefense.dangerIntegrity) {
      addLog(
        state,
        `Barricades down to ${Math.round(shelter.integrity)}%. Reinforce them with lumber.`
      );
    }
  }
}

/** Spend lumber to bring the barricades back up. Returns true if anything changed. */
export function reinforceBarricades(state, lumber = 1) {
  const shelter = state.shelter;
  if (!shelter?.unlocked) return false;
  if (shelter.integrity >= 100) {
    addLog(state, "The barricades are already solid.");
    return false;
  }
  const available = Math.min(lumber, getShelterStock(state, "lumber"));
  if (available <= 0) {
    addLog(state, "No lumber left. Order more before nightfall.");
    return false;
  }

  state.shelter.stock.lumber -= available;
  const gain = available * CONFIG.shelterDefense.integrityPerLumber;
  shelter.integrity = Math.min(100, shelter.integrity + gain);
  addLog(
    state,
    `Reinforced the barricades with ${available} lumber (${Math.round(shelter.integrity)}%).`
  );
  return true;
}

export function shelterHudSummary(state) {
  const shelter = state.shelter;
  if (!shelter?.unlocked) return "";
  const parts = shelterItemIds().map(
    (id) => `${CONFIG.shelterItems[id].label}: ${getShelterStock(state, id)}`
  );
  parts.push(`Barricades: ${Math.round(shelter.integrity)}%`);
  if (!shelter.powered) parts.push("GENERATOR DOWN");
  return parts.join(" · ");
}

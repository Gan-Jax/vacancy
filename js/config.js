/** Game balance numbers — tweak these to change difficulty and pacing. */
export const CONFIG = {
  startingMoney: 120,
  startingReputation: 50,

  /** Room nightly rate before reputation bonus. */
  baseRoomRate: 35,

  /** Extra cash per reputation point above 50. */
  reputationRateBonus: 0.5,

  /** Chance a guest causes damage on checkout (0–1). */
  damageChance: 0.22,

  /** Damage charge range. */
  damageChargeMin: 25,
  damageChargeMax: 80,

  /**
   * Cleaning time in game hours (1 hour = 1 real second).
   * light / medium / heavy
   */
  dirtHours: {
    light: 1,
    medium: 2,
    heavy: 3,
  },

  /**
   * Repair time in game hours after damage is found.
   * light / medium / heavy
   */
  repairHours: {
    light: 2,
    medium: 4,
    heavy: 8,
  },

  /**
   * Repair parts cost as a fraction of one day's stay rate.
   * light 1/3, medium 2/3, heavy full day.
   */
  repairCostDayFractions: {
    light: 1 / 3,
    medium: 2 / 3,
    heavy: 1,
  },

  /** Quick walkthrough to discover dirt/repair severity. */
  inspectHours: 0.5,

  /** How dirt a room is after checkout (weights). */
  dirtWeights: {
    light: 50,
    medium: 35,
    heavy: 15,
  },

  /** How bad repairs are when damage is found (weights). */
  repairWeights: {
    light: 50,
    medium: 35,
    heavy: 15,
  },

  /** Staff NPCs take this much longer than the player. */
  npcWorkMultiplier: 3,

  /** Staff walk speed (pixels per second). Still slower than the player. */
  npcMoveSpeed: 160,

  /** Guest walk speed (pixels per second). */
  guestMoveSpeed: 120,

  /** Player movement speed (pixels per second). */
  playerSpeed: 220,

  /** How often travelers arrive at the front desk at 50 reputation (chance per second). */
  arrivalChancePerSecond: 0.12,

  /**
   * Arrival rate scales with reputation.
   * At 0 rep → base * minMult; at 100 → base * maxMult.
   */
  arrivalRepMinMult: 0.45,
  arrivalRepMaxMult: 1.85,

  /** Reputation gained when a guest finishes their stay and checks out. */
  checkoutReputationBonus: 3,

  /** Max guests that can wait at the desk at once. */
  maxWaitingGuests: 3,

  /** How long a guest will wait at the desk before leaving (game hours). */
  waitPatienceHours: 4,

  /** One billed "day" of stay is this many in-game hours. */
  stayIntervalHours: 12,

  /** Guests book a random stay in this day range (each day = one 12h interval). */
  minStayDays: 1,
  maxStayDays: 3,

  /** In-game hours per real second while unpaused. 1/3 ≈ one hour every 3 seconds. */
  hoursPerSecond: 1 / 3,

  /** Unlock costs scale per room. */
  roomUnlockBaseCost: 150,
  roomUnlockCostStep: 100,

  /** Bob repairs. Mary inspects + cleans. */
  hireBobCost: 150,
  hireMaryCost: 120,

  /** Staff earn this much for each calendar day they complete work. */
  staffDailyWage: 10,

  /** After this many calendar days, staff come to the desk for payday. */
  staffPayPeriodDays: 7,

  maxRooms: 12,
  startingUnlockedRooms: 3,

  /** Room footprint used for drawing and collision. */
  roomWidth: 118,
  roomHeight: 92,

  /** Hours until a PC supply order arrives. */
  inventoryDeliveryHours: 24,

  /**
   * Inn inventory — stocked in the office, consumed by rooms.
   * replaceEvery: "stay" | { stays: N } | { days: N }
   */
  inventoryItems: {
    towels: {
      label: "Towels",
      unitCost: 10,
      startingStock: 20,
      orderPack: 10,
      replaceEvery: { stays: 10 },
    },
    soap: {
      label: "Soap",
      unitCost: 1,
      startingStock: 50,
      orderPack: 25,
      replaceEvery: "stay",
    },
    shampoo: {
      label: "Shampoo",
      unitCost: 1,
      startingStock: 50,
      orderPack: 25,
      replaceEvery: "stay",
    },
    conditioner: {
      label: "Conditioner",
      unitCost: 1,
      startingStock: 50,
      orderPack: 25,
      replaceEvery: "stay",
    },
    toiletPaper: {
      label: "Toilet paper",
      unitCost: 2,
      startingStock: 30,
      orderPack: 12,
      replaceEvery: { days: 3 },
    },
  },

  /**
   * Story pacing. Acts advance on keystone events that need BOTH a day
   * threshold and player progress, so the fiction tracks how you actually play.
   */
  story: {
    /** Game hours between ambient hint lines, per act (randomized ±40%). */
    hintIntervalByAct: {
      normalcy: 22,
      unease: 15,
      disruption: 11,
      collapse: 9,
      shelter: 9,
    },
    /** Chance an arrival carries story weight, per act. */
    markedChanceByAct: {
      normalcy: 0.08,
      unease: 0.2,
      disruption: 0.34,
      collapse: 0.5,
      shelter: 0.55,
    },
  },

  /**
   * Shelter-era resources. Hidden from the office PC until the story unlocks
   * them, then they become the thing keeping everyone alive.
   */
  shelterItems: {
    water: {
      label: "Water",
      unitCost: 3,
      startingStock: 0,
      orderPack: 20,
    },
    food: {
      label: "Food",
      unitCost: 4,
      startingStock: 0,
      orderPack: 20,
    },
    fuel: {
      label: "Fuel",
      unitCost: 6,
      startingStock: 0,
      orderPack: 10,
    },
    medicine: {
      label: "Medicine",
      unitCost: 12,
      startingStock: 0,
      orderPack: 5,
    },
    lumber: {
      label: "Lumber",
      unitCost: 8,
      startingStock: 0,
      orderPack: 10,
    },
  },

  /** Per-occupant daily draw once the shelter is running. */
  shelterUse: {
    waterPerPersonPerDay: 1,
    foodPerPersonPerDay: 1,
    /** Generator burn per day regardless of headcount. */
    fuelPerDay: 2,
  },

  shelterDefense: {
    /** Barricade integrity lost per night once things are bad. */
    nightlyIntegrityLoss: 6,
    /** Integrity restored per lumber spent on repairs. */
    integrityPerLumber: 8,
    /** Below this, the night gets dangerous. */
    dangerIntegrity: 40,
  },
};

export function pickWeightedLevel(weights) {
  const entries = Object.entries(weights);
  const total = entries.reduce((sum, [, w]) => sum + w, 0);
  let roll = Math.random() * total;
  for (const [level, weight] of entries) {
    roll -= weight;
    if (roll <= 0) return level;
  }
  return entries[0][0];
}

export function getCleanHours(dirtLevel) {
  return CONFIG.dirtHours[dirtLevel] ?? CONFIG.dirtHours.medium;
}

export function getRepairHours(repairLevel) {
  return CONFIG.repairHours[repairLevel] ?? CONFIG.repairHours.medium;
}

export function getTaskHours(type, room, forNpc = false) {
  let hours = CONFIG.inspectHours;
  if (type === "clean") hours = getCleanHours(room.dirtLevel);
  if (type === "repair") hours = getRepairHours(room.repairLevel);
  if (forNpc) hours *= CONFIG.npcWorkMultiplier;
  return hours;
}

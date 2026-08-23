import {
  CONFIG,
  pickWeightedLevel,
  getCleanHours,
  getRepairHours,
} from "./config.js";
import { addLog } from "./state.js";
import {
  followPath,
  pathToDeskHall,
  pathToRoomDoor,
  resolveRoomCollision,
  steerTo,
} from "./pathing.js";
import {
  canStockRoom,
  consumeCheckInSupplies,
  processDailyInventory,
  updateInventoryOrders,
} from "./inventory.js";
import { storyHook, updateStory } from "./story.js";
import { updateMedia } from "./media.js";
import { processDailyShelter } from "./shelter.js";
import {
  armAdmittedThreat,
  createArrival,
  KIND,
  resolveArrivalConsequences,
  revealedSigns,
} from "./arrivals.js";

const GUEST_NAMES = [
  "Alex",
  "Sam",
  "Jordan",
  "Riley",
  "Casey",
  "Morgan",
  "Taylor",
  "Quinn",
  "Jamie",
  "Drew",
];

function pickGuestName() {
  return GUEST_NAMES[Math.floor(Math.random() * GUEST_NAMES.length)];
}

/** Current one-day stay rate (used for guest bills and repair costs). */
export function getDayRate(state) {
  const bonus = Math.max(0, state.reputation - 50) * CONFIG.reputationRateBonus;
  return Math.round(CONFIG.baseRoomRate + bonus);
}

function roomRate(state) {
  return getDayRate(state);
}

/** Parts cost to fix a repair of the given severity. */
export function getRepairCost(state, repairLevel) {
  const fraction =
    CONFIG.repairCostDayFractions[repairLevel] ??
    CONFIG.repairCostDayFractions.medium;
  return Math.max(1, Math.round(getDayRate(state) * fraction));
}

export function canAffordRepair(state, room) {
  if (!room?.repairLevel) return false;
  return state.money >= getRepairCost(state, room.repairLevel);
}

/**
 * Pay repair parts and mark the room as paid for this job.
 * Returns the amount charged, or null if unaffordable / already paid.
 */
export function beginRepairPayment(state, room) {
  if (room.status !== "needs_repair" || !room.repairLevel) return null;
  if (room.repairPaid) return 0;

  const cost = getRepairCost(state, room.repairLevel);
  if (state.money < cost) return null;

  state.money -= cost;
  room.repairPaid = true;
  room.repairCost = cost;
  return cost;
}

/** Higher reputation → guests show up faster. */
export function getArrivalChancePerSecond(state) {
  const t = Math.max(0, Math.min(1, state.reputation / 100));
  const mult =
    CONFIG.arrivalRepMinMult +
    t * (CONFIG.arrivalRepMaxMult - CONFIG.arrivalRepMinMult);
  return CONFIG.arrivalChancePerSecond * mult;
}

function levelLabel(level) {
  if (!level) return "unknown";
  return level.charAt(0).toUpperCase() + level.slice(1);
}

function deskSpawn(layout) {
  // Guests leave from the front of the check-in line
  return layout.checkInLineSlot(0);
}

function roomCenter(layout, roomId) {
  return layout.roomCenters[roomId - 1];
}

function checkoutWaitSlot(layout, index) {
  return layout.checkoutLineSlot(index);
}

function guestSpeed() {
  return CONFIG.guestMoveSpeed;
}

function guestAllowRoom(guest) {
  if (guest.nav === "enter_room" || guest.nav === "exit_room") {
    return guest.roomId;
  }
  return null;
}

/** A traveler shows up and waits at the front desk. */
export function spawnArrival(state) {
  if (!state.vacancyOpen) return false;
  if (state.waitingGuests.length >= CONFIG.maxWaitingGuests) return false;

  // No new arrivals when every clean room is already spoken for
  const cleanRooms = state.rooms.filter(
    (r) => r.unlocked && r.status === "clean"
  ).length;
  if (cleanRooms <= state.waitingGuests.length) return false;

  const guest = createArrival(state, pickGuestName());
  state.waitingGuests.push(guest);
  addLog(state, `${guest.name} is at the desk. ${guest.claim}`);
  for (const sign of revealedSigns(guest)) {
    addLog(state, sign.text);
  }
  return true;
}

/** Guests leave the desk if not checked in before their patience runs out. */
export function processWaitingGuests(state, hoursPassed) {
  if (!state.waitingGuests.length) return;

  const stillWaiting = [];
  for (const guest of state.waitingGuests) {
    guest.waitRemainingHours -= hoursPassed;
    if (guest.waitRemainingHours > 0) {
      stillWaiting.push(guest);
      continue;
    }

    // Letting someone stand there until they give up is its own answer.
    if (guest.kind === KIND.survivor) {
      if (state.story) {
        state.story.humanity = Math.max(0, state.story.humanity - 6);
      }
      addLog(
        state,
        `${guest.name} waited ${CONFIG.waitPatienceHours}h at the desk, then walked back out to the road alone.`
      );
    } else if (guest.kind === KIND.wrong) {
      addLog(state, `${guest.name} was gone from the lobby. Nobody saw them leave.`);
    } else {
      state.reputation = Math.max(0, state.reputation - 3);
      addLog(
        state,
        `${guest.name} left angry — waited over ${CONFIG.waitPatienceHours}h with no room. (−3 reputation)`
      );
    }
    storyHook(state, "turnAway", { guest });
  }
  state.waitingGuests = stillWaiting;
}

/** Flip the roadside Vacancy / No Vacancy sign. */
export function toggleVacancy(state) {
  state.vacancyOpen = !state.vacancyOpen;
  if (state.vacancyOpen) {
    addLog(state, "Sign flipped to VACANCY — travelers can arrive again.");
  } else {
    addLog(
      state,
      "Sign flipped to NO VACANCY — new guests will stop showing up. Anyone already waiting can still check in."
    );
  }
  return state.vacancyOpen;
}

/**
 * Check in: take payment, assign room, guest walks to the room.
 */
export function checkInAtDesk(state, layout, chosenGuest = null) {
  if (state.waitingGuests.length === 0) {
    addLog(state, "Nobody is waiting to check in.");
    return false;
  }
  if (chosenGuest && !state.waitingGuests.includes(chosenGuest)) {
    return false;
  }

  const cleanRoom = state.rooms.find((r) => r.unlocked && r.status === "clean");
  if (!cleanRoom) {
    addLog(state, "No clean rooms available. Clean a room first!");
    return false;
  }

  const stockCheck = canStockRoom(state, cleanRoom);
  if (!stockCheck.ok) {
    const label = CONFIG.inventoryItems[stockCheck.missing]?.label || stockCheck.missing;
    addLog(
      state,
      `Can't check in — out of ${label}. Order more on the office PC.`
    );
    return false;
  }

  const stayDays =
    CONFIG.minStayDays +
    Math.floor(Math.random() * (CONFIG.maxStayDays - CONFIG.minStayDays + 1));

  const waiting = chosenGuest ?? state.waitingGuests[0];
  state.waitingGuests = state.waitingGuests.filter((g) => g !== waiting);
  const spawn = deskSpawn(layout);
  const dest = roomCenter(layout, cleanRoom.id);

  consumeCheckInSupplies(state, cleanRoom);

  cleanRoom.status = "occupied";
  cleanRoom.guestName = waiting.name;
  cleanRoom.stayDays = stayDays;
  cleanRoom.stayRemainingHours = stayDays * CONFIG.stayIntervalHours;
  cleanRoom.paymentsLeft = stayDays - 1;
  cleanRoom.nextIntervalPaymentIn = CONFIG.stayIntervalHours;
  cleanRoom.hasHiddenDamage = Math.random() < CONFIG.damageChance;
  cleanRoom.damageFound = false;
  cleanRoom.dirtLevel = null;
  cleanRoom.repairLevel = null;
  cleanRoom.tpDayCounter = 0;

  state.activeGuests.push({
    name: waiting.name,
    kind: waiting.kind ?? KIND.traveler,
    marked: Boolean(waiting.marked),
    phase: "walking_to_room",
    nav: "to_door",
    roomId: cleanRoom.id,
    x: spawn.x,
    y: spawn.y,
    radius: 11,
    path: pathToRoomDoor(layout, spawn.x, spawn.y, cleanRoom.id),
    targetX: dest.x,
    targetY: dest.y,
    stayDays,
    stayRemainingHours: stayDays * CONFIG.stayIntervalHours,
    paymentsLeft: stayDays - 1,
    nextIntervalPaymentIn: CONFIG.stayIntervalHours,
    hasHiddenDamage: cleanRoom.hasHiddenDamage,
    waitRemainingHours: null,
    reputationBonus: null,
    upsetCheckout: false,
  });

  const dayWord = stayDays === 1 ? "day" : "days";
  if (waiting.kind === KIND.survivor) {
    // Survivors have nothing to pay with. You take them in or you do not.
    cleanRoom.paymentsLeft = 0;
    addLog(
      state,
      `${waiting.name} is in Room ${cleanRoom.id}. No money changed hands.`
    );
    if (state.story) {
      state.story.humanity = Math.min(100, state.story.humanity + 3);
    }
  } else {
    const rate = roomRate(state);
    state.money += rate;
    state.reputation = Math.min(100, state.reputation + 1);
    addLog(
      state,
      `${waiting.name} checked in for Room ${cleanRoom.id} (${stayDays} ${dayWord}, +$${rate}). Walking to the room...`
    );
  }

  armAdmittedThreat(state, waiting);
  storyHook(state, "checkIn", { guest: waiting, room: cleanRoom });
  return true;
}

/** Finish checkout at the desk for the first guest waiting to leave. */
export function checkOutAtDesk(state) {
  const guest = state.activeGuests.find((g) => g.phase === "waiting_checkout");
  if (!guest) {
    addLog(state, "Nobody is waiting to check out.");
    return false;
  }

  let bonus = guest.reputationBonus ?? CONFIG.checkoutReputationBonus;
  if (guest.upsetCheckout) {
    bonus = Math.max(0, bonus - 1);
    state.reputation = Math.min(100, state.reputation + bonus);
    addLog(
      state,
      `${guest.name} checked out annoyed after a long wait. (+${bonus} reputation, −1 for the delay)`
    );
  } else {
    state.reputation = Math.min(100, state.reputation + bonus);
    addLog(state, `${guest.name} checked out happily. (+${bonus} reputation)`);
  }

  state.activeGuests = state.activeGuests.filter((g) => g !== guest);
  storyHook(state, "checkOut", { guest });
  return true;
}

/** Pay the first staff member waiting at the desk for wages. */
export function payStaffAtDesk(state, layout, staffList = []) {
  const waiting = staffList.filter(
    (s) => s && (s.phase === "waiting_pay" || s.phase === "to_desk")
  );
  if (!waiting.length) return false;

  // Prefer whoever is already waiting; otherwise pay the closest to the desk
  waiting.sort((a, b) => {
    const aw = a.phase === "waiting_pay" ? 0 : 1;
    const bw = b.phase === "waiting_pay" ? 0 : 1;
    if (aw !== bw) return aw - bw;
    return a.wagesOwed - b.wagesOwed;
  });

  const staff = waiting[0];
  const amount = staff.wagesOwed;
  if (amount > 0 && state.money < amount) {
    addLog(state, `Need $${amount} to pay ${staff.name}.`);
    return false;
  }

  if (amount > 0) state.money -= amount;
  staff.collectPaycheck(state, layout);
  return true;
}

/**
 * Desk action: checkout → staff payday → review whoever is at the door.
 * Returns "review" when the UI should open the admit/refuse panel.
 */
export function handleDeskAction(state, layout, staffList = []) {
  if (state.activeGuests.some((g) => g.phase === "waiting_checkout")) {
    return checkOutAtDesk(state);
  }
  if (
    staffList.some((s) => s && (s.phase === "waiting_pay" || s.phase === "to_desk"))
  ) {
    return payStaffAtDesk(state, layout, staffList);
  }
  if (state.waitingGuests.length > 0) {
    return "review";
  }
  addLog(state, "Desk is clear. Flip the vacancy sign at the bottom (V or E).");
  return false;
}

/** Charge the room rate at the start of each extra 12h day. */
export function processStayBilling(state, room, hoursPassed) {
  if (room.status !== "occupied") return;
  if (!room.paymentsLeft || room.nextIntervalPaymentIn == null) return;

  room.nextIntervalPaymentIn -= hoursPassed;

  let billGuard = 0;
  while (
    room.nextIntervalPaymentIn <= 0 &&
    room.paymentsLeft > 0 &&
    billGuard++ < 8
  ) {
    const rate = roomRate(state);
    state.money += rate;
    room.paymentsLeft -= 1;
    room.nextIntervalPaymentIn += CONFIG.stayIntervalHours || 12;
    addLog(
      state,
      `${room.guestName} in Room ${room.id} paid +$${rate} for another ${CONFIG.stayIntervalHours}h.`
    );
  }

  const guest = state.activeGuests.find(
    (g) =>
      g.roomId === room.id &&
      (g.phase === "in_room" || g.phase === "walking_to_room")
  );
  if (guest) {
    guest.paymentsLeft = room.paymentsLeft;
    guest.nextIntervalPaymentIn = room.nextIntervalPaymentIn;
  }
}

/** When stay ends: guest walks to desk; room becomes ready to inspect. */
function beginDeparture(state, layout, guest, room) {
  const stayDays = guest.stayDays || room.stayDays || 1;
  guest.reputationBonus =
    CONFIG.checkoutReputationBonus + Math.max(0, stayDays - 1);
  guest.upsetCheckout = false;
  guest.waitRemainingHours = null;
  guest.phase = "walking_to_checkout";
  guest.nav = "exit_room";
  guest.path = [];

  const queueIndex =
    state.activeGuests.filter(
      (g) => g.phase === "waiting_checkout" || g.phase === "walking_to_checkout"
    ).length - 1;
  const slot = checkoutWaitSlot(layout, Math.max(0, queueIndex));
  guest.targetX = slot.x;
  guest.targetY = slot.y;

  room.status = "needs_inspection";
  room.guestName = null;
  room.stayRemainingHours = null;
  room.stayDays = null;
  room.nextIntervalPaymentIn = null;
  room.paymentsLeft = null;
  room.dirtLevel = pickWeightedLevel(CONFIG.dirtWeights);
  room.repairLevel = null;
  room.hasHiddenDamage = guest.hasHiddenDamage;

  addLog(
    state,
    `${guest.name} left Room ${room.id} and is heading to the front desk to check out. Inspect the room.`
  );
}

/** Move guests, run stays, and manage checkout patience. */
export function updateGuests(state, dt, layout) {
  const hoursPassed = CONFIG.hoursPerSecond * dt;
  let checkoutSlot = 0;
  const rooms = state.rooms;
  const speed = guestSpeed();

  for (const guest of state.activeGuests) {
    if (guest.radius == null) guest.radius = 11;

    if (guest.phase === "walking_to_room") {
      if (!guest.nav) guest.nav = "to_door";
      resolveRoomCollision(guest, rooms, layout, guestAllowRoom(guest));

      if (guest.nav === "to_door") {
        if (!guest.path?.length) {
          guest.path = pathToRoomDoor(layout, guest.x, guest.y, guest.roomId);
        }
        const atDoor = followPath(guest, dt, rooms, layout, null, speed);
        if (atDoor) {
          const door =
            layout.waypoints.points[layout.waypoints.doorIdx[guest.roomId - 1]];
          guest.x = door.x;
          guest.y = door.y;
          guest.nav = "enter_room";
          guest.path = [];
        }
      } else {
        const dest = roomCenter(layout, guest.roomId);
        guest.targetX = dest.x;
        guest.targetY = dest.y;
        steerTo(
          guest,
          dest.x,
          dest.y,
          dt,
          rooms,
          layout,
          guest.roomId,
          speed
        );
        const dist = Math.hypot(guest.x - dest.x, guest.y - dest.y);
        if (dist < 22) {
          guest.x = dest.x;
          guest.y = dest.y;
          guest.phase = "in_room";
          guest.nav = null;
          guest.path = [];
          addLog(state, `${guest.name} arrived at Room ${guest.roomId}.`);
        }
      }
      continue;
    }

    if (guest.phase === "in_room") {
      const room = state.rooms[guest.roomId - 1];
      if (!room || room.status !== "occupied") continue;

      guest.stayRemainingHours -= hoursPassed;
      room.stayRemainingHours = guest.stayRemainingHours;
      processStayBilling(state, room, hoursPassed);

      if (guest.stayRemainingHours <= 0) {
        beginDeparture(state, layout, guest, room);
      }
      continue;
    }

    if (guest.phase === "walking_to_checkout") {
      if (!guest.nav) guest.nav = "exit_room";
      const slot = checkoutWaitSlot(layout, checkoutSlot);
      guest.targetX = slot.x;
      guest.targetY = slot.y;
      resolveRoomCollision(guest, rooms, layout, guestAllowRoom(guest));

      if (guest.nav === "exit_room") {
        const door =
          layout.waypoints.points[layout.waypoints.doorIdx[guest.roomId - 1]];
        const dist = Math.hypot(guest.x - door.x, guest.y - door.y);
        steerTo(guest, door.x, door.y, dt, rooms, layout, guest.roomId, speed);
        if (dist < 16) {
          guest.nav = "to_desk";
          guest.path = pathToDeskHall(layout, guest.x, guest.y);
        }
      } else if (guest.nav === "to_desk") {
        if (!guest.path?.length) {
          guest.path = pathToDeskHall(layout, guest.x, guest.y);
        }
        const atHall = followPath(guest, dt, rooms, layout, null, speed);
        if (atHall) {
          guest.nav = "to_slot";
          guest.path = [];
        }
      } else {
        const dist = Math.hypot(guest.x - slot.x, guest.y - slot.y);
        steerTo(guest, slot.x, slot.y, dt, rooms, layout, null, speed);
        if (dist < 10) {
          guest.phase = "waiting_checkout";
          guest.nav = null;
          guest.path = [];
          guest.waitRemainingHours = CONFIG.waitPatienceHours;
          addLog(
            state,
            `${guest.name} is at the desk ready to check out (${CONFIG.waitPatienceHours}h patience).`
          );
        }
      }
      checkoutSlot += 1;
      continue;
    }

    if (guest.phase === "waiting_checkout") {
      const slot = checkoutWaitSlot(layout, checkoutSlot);
      guest.x = slot.x;
      guest.y = slot.y;
      guest.waitRemainingHours -= hoursPassed;

      if (!guest.upsetCheckout && guest.waitRemainingHours <= 0) {
        guest.upsetCheckout = true;
        guest.waitRemainingHours = 0;
        addLog(
          state,
          `${guest.name} is upset about the checkout wait — will give 1 less reputation.`
        );
      }
      checkoutSlot += 1;
    }
  }
}

export function finishInspection(state, room, byNpc = false) {
  if (room.status !== "needs_inspection") return;

  room.inspectProgress = 0;
  room.worker = null;

  const dirt = room.dirtLevel || "medium";
  const cleanHrs = getCleanHours(dirt);
  const who = byNpc ? ` (${byNpc})` : "";

  // Flow: Inspect → Clean → Repair (if damage)
  room.status = "dirty";

  if (room.hasHiddenDamage) {
    room.damageFound = true;
    room.repairLevel = pickWeightedLevel(CONFIG.repairWeights);
    state.reputation = Math.max(0, state.reputation - 2);
    const repairCost = getRepairCost(state, room.repairLevel);
    addLog(
      state,
      `Room ${room.id}: ${levelLabel(dirt)} dirt (${cleanHrs}h), then ${levelLabel(room.repairLevel)} repair (${getRepairHours(room.repairLevel)}h) will cost $${repairCost}.${who}`
    );
  } else {
    room.damageFound = false;
    room.repairLevel = null;
    addLog(
      state,
      `Room ${room.id}: no damage. ${levelLabel(dirt)} dirt — clean takes ${cleanHrs}h.${who}`
    );
  }

  room.hasHiddenDamage = false;
}

export function finishCleaning(state, room, byNpc = false) {
  if (room.status !== "dirty") return;

  const level = room.dirtLevel;
  room.cleanProgress = 0;
  room.dirtLevel = null;
  room.worker = null;
  const who = byNpc ? ` (${byNpc})` : "";

  if (room.damageFound && room.repairLevel) {
    room.status = "needs_repair";
    room.repairPaid = false;
    const repairCost = getRepairCost(state, room.repairLevel);
    addLog(
      state,
      `Room ${room.id} cleaned (${levelLabel(level)}). Needs ${levelLabel(room.repairLevel)} repair (${getRepairHours(room.repairLevel)}h, $${repairCost}).${who}`
    );
    return;
  }

  room.status = "clean";
  room.repairLevel = null;
  room.damageFound = false;
  state.reputation = Math.min(100, state.reputation + 1);
  addLog(
    state,
    `Room ${room.id} is ready again (${levelLabel(level)} clean done).${who}`
  );
}

export function finishRepair(state, room, byNpc = false) {
  if (room.status !== "needs_repair") return;

  const level = room.repairLevel;
  // Always ensure parts are paid — never credit money for repairs
  let cost = room.repairCost || 0;
  if (!room.repairPaid) {
    cost = getRepairCost(state, level);
    state.money -= cost;
    room.repairPaid = true;
    room.repairCost = cost;
  }

  room.status = "clean";
  room.repairLevel = null;
  room.repairProgress = 0;
  room.dirtLevel = null;
  room.worker = null;
  room.damageFound = false;
  room.repairPaid = false;
  room.repairCost = null;
  state.reputation = Math.min(100, state.reputation + 1);
  const who = byNpc ? ` (${byNpc})` : "";
  addLog(
    state,
    `Room ${room.id} repaired (${levelLabel(level)}). Parts −$${cost}.${who}`
  );
  storyHook(state, "repair", { room });
}

export function advanceTime(state, dt, layout, staffList = []) {
  if (state.paused) return;

  const hoursPassed = CONFIG.hoursPerSecond * dt;
  state.hour += hoursPassed;

  while (state.hour >= 24) {
    state.hour -= 24;
    state.day += 1;
    addLog(state, `--- Day ${state.day} ---`);
    processDailyInventory(state);
    processDailyShelter(state);
    resolveArrivalConsequences(state);
    for (const staff of staffList) {
      if (staff) staff.onNewDay(state);
    }
  }

  updateStory(state, hoursPassed);
  updateMedia(state, hoursPassed);
  updateInventoryOrders(state, hoursPassed);
  updateGuests(state, dt, layout);
  processWaitingGuests(state, hoursPassed);

  if (Math.random() < getArrivalChancePerSecond(state) * dt) {
    spawnArrival(state);
  }
}

export function unlockRoom(state) {
  const cost = getUnlockCost(state);
  const room = state.rooms.find((r) => !r.unlocked);
  if (!room) {
    addLog(state, "All rooms are already unlocked.");
    return false;
  }
  if (state.money < cost) {
    addLog(state, `Need $${cost} to unlock Room ${room.id}.`);
    return false;
  }

  state.money -= cost;
  room.unlocked = true;
  room.status = "clean";
  addLog(state, `Unlocked Room ${room.id} for $${cost}.`);
  storyHook(state, "unlock", { room });
  return true;
}

export function hireBob(state) {
  if (state.bobHired || state.money < CONFIG.hireBobCost) return false;
  state.money -= CONFIG.hireBobCost;
  state.bobHired = true;
  addLog(
    state,
    `Hired Bob — repairs rooms. $${CONFIG.staffDailyWage}/work day, payday every ${CONFIG.staffPayPeriodDays} days.`
  );
  return true;
}

export function hireMary(state) {
  if (state.maryHired || state.money < CONFIG.hireMaryCost) return false;
  state.money -= CONFIG.hireMaryCost;
  state.maryHired = true;
  addLog(
    state,
    `Hired Mary — inspects & cleans. $${CONFIG.staffDailyWage}/work day, payday every ${CONFIG.staffPayPeriodDays} days.`
  );
  return true;
}

function getUnlockCost(state) {
  const unlockedCount = state.rooms.filter((r) => r.unlocked).length;
  return CONFIG.roomUnlockBaseCost + unlockedCount * CONFIG.roomUnlockCostStep;
}

export { getUnlockCost as getRoomUnlockCost };

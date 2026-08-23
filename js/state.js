import { CONFIG } from "./config.js";
import { createInventoryState } from "./inventory.js";
import { createStoryState } from "./story.js";
import { createShelterState } from "./shelter.js";

/** Central game state — money, time, rooms, guests, staff. */
export function createInitialState() {
  const rooms = [];
  for (let i = 0; i < CONFIG.maxRooms; i++) {
    rooms.push({
      id: i + 1,
      unlocked: i < CONFIG.startingUnlockedRooms,
      status: "clean", // clean | dirty | occupied | needs_inspection | needs_repair
      guestName: null,
      stayRemainingHours: null,
      stayDays: null,
      paymentsLeft: null,
      nextIntervalPaymentIn: null,
      hasHiddenDamage: false,
      damageFound: false,
      dirtLevel: null, // light | medium | heavy
      repairLevel: null, // light | medium | heavy
      repairPaid: false,
      repairCost: null,
      cleanProgress: 0,
      inspectProgress: 0,
      repairProgress: 0,
      worker: null, // "player" | "npc" | null
      stayCount: 0,
      staysSinceTowel: 0,
      tpDayCounter: 0,
    });
  }

  return {
    money: CONFIG.startingMoney,
    day: 1,
    hour: 8,
    reputation: CONFIG.startingReputation,
    waitingGuests: [],
    /** Guests walking / staying / waiting to check out. */
    activeGuests: [],
    /** When false, the No Vacancy sign is up — new travelers won't arrive. */
    vacancyOpen: true,
    paused: false,
    bobHired: false,
    maryHired: false,
    inventory: createInventoryState(),
    /** Narrative act, keystone events, and ambient dispatches. */
    story: createStoryState(),
    /** Late-game shelter resources and barricades. Dormant at first. */
    shelter: createShelterState(),
    /** Office PC order panel open. */
    pcOpen: false,
    /** Arrival currently being reviewed at the desk (admit / turn away). */
    deskGuest: null,
    /** "radio" | "paper" | null */
    mediaOpen: null,
    rooms,
    messages: [
      "Welcome to the roadside inn. Meet guests at the front desk (E) to check them in.",
      "Use the office PC to order towels, soap, and supplies (24h delivery).",
    ],
  };
}

export function addLog(state, text) {
  state.messages.unshift(text);
  if (state.messages.length > 30) {
    state.messages.length = 30;
  }
}

/** Format in-game hour (0–24 float) as a readable clock, e.g. "2:30 PM". */
export function formatClock(hour) {
  let totalMinutes = Math.floor(((hour % 24) + 24) % 24 * 60);
  const h24 = Math.floor(totalMinutes / 60) % 24;
  const minutes = totalMinutes % 60;
  const period = h24 >= 12 ? "PM" : "AM";
  const h12 = h24 % 12 || 12;
  return `${h12}:${String(minutes).padStart(2, "0")} ${period}`;
}

/** Rough time-of-day label for the HUD. */
export function getTimeOfDayLabel(hour) {
  const h = ((hour % 24) + 24) % 24;
  if (h >= 5 && h < 12) return "Morning";
  if (h >= 12 && h < 17) return "Afternoon";
  if (h >= 17 && h < 21) return "Evening";
  return "Night";
}

export function getRoomUnlockCost(state) {
  const unlockedCount = state.rooms.filter((r) => r.unlocked).length;
  return CONFIG.roomUnlockBaseCost + unlockedCount * CONFIG.roomUnlockCostStep;
}

export function getAvailableCleanRooms(state) {
  return state.rooms.filter((r) => r.unlocked && r.status === "clean");
}

export function getDirtyOrInspectionRooms(state) {
  return state.rooms.filter(
    (r) =>
      r.unlocked &&
      (r.status === "dirty" ||
        r.status === "needs_inspection" ||
        (r.status === "dirty" && r.cleanProgress > 0) ||
        (r.status === "needs_inspection" && r.inspectProgress > 0))
  );
}

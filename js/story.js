import { CONFIG } from "./config.js";
import { addLog } from "./state.js";
import { activateDefense, unlockShelterSystems } from "./shelter.js";

/**
 * Narrative layer.
 *
 * Act 1 plays exactly like the plain hotel sim. Everything after that is
 * driven by keystone events, which need a day threshold AND player progress,
 * so the collapse tracks how the player actually runs the place.
 */

export const ACT_ORDER = [
  "normalcy",
  "unease",
  "disruption",
  "collapse",
  "shelter",
];

export const ACT_LABELS = {
  normalcy: "Quiet season",
  unease: "Something off",
  disruption: "The city goes dark",
  collapse: "No one is coming",
  shelter: "Shelter",
};

export function createStoryState() {
  return {
    act: "normalcy",
    /** Keystone id -> day it fired. */
    fired: {},
    /** Freeform markers set by events and hooks. */
    flags: {},
    /** Hidden pressure meter, nudged by how the player treats people. */
    tension: 0,
    /** Falls when you turn away people who needed help. */
    humanity: 100,
    /** Consequences waiting to land on a future day. */
    pendingThreats: [],
    pendingVindication: [],
    threatsRefused: 0,
    /** Countdown to the next ambient hint, in game hours. */
    hintIn: CONFIG.story.hintIntervalByAct.normalcy,
    /** Radio/news fragments, newest first. */
    dispatches: [],
    /** Radio log + newspaper issues. See js/media.js. */
    media: {
      radioLog: [],
      papers: [],
      airedIds: [],
      printedIds: [],
      radioIn: 10,
      lastPaperDay: 0,
    },
    /** Latest unshown keystone, picked up by the UI for the banner. */
    banner: null,
    stats: {
      checkIns: 0,
      checkOuts: 0,
      turnedAway: 0,
      markedServed: 0,
      markedRefused: 0,
      repairs: 0,
      orders: 0,
    },
  };
}

export function currentAct(state) {
  return state.story?.act ?? "normalcy";
}

export function actIndex(state) {
  return Math.max(0, ACT_ORDER.indexOf(currentAct(state)));
}

/** True once the world has tipped and the hotel is really a shelter. */
export function isShelterEra(state) {
  return actIndex(state) >= ACT_ORDER.indexOf("collapse");
}

export function hasFlag(state, flag) {
  return Boolean(state.story?.flags?.[flag]);
}

export function setFlag(state, flag, value = true) {
  if (!state.story) return;
  state.story.flags[flag] = value;
}

function addDispatch(state, text) {
  state.story.dispatches.unshift({ day: state.day, text });
  if (state.story.dispatches.length > 24) state.story.dispatches.length = 24;
}

/** Ambient flavor. Never mechanical — just wrongness accumulating. */
const HINTS = {
  normalcy: [
    "A trucker says the interstate was backed up for two hours over nothing.",
    "The ice machine hums all night. You have started noticing it.",
    "A guest asks whether the city is always that bright at 3 AM.",
    "Someone left a road atlas on the counter with three towns circled.",
    "The dog at the lot next door barked until dawn, then stopped.",
    "Long distance call comes in for a room that checked out last week.",
  ],
  unease: [
    "Radio bulletin: three counties ask residents to limit non-essential travel.",
    "A guest checks in with no luggage and pays for two nights in cash.",
    "Supply truck is late again. The dispatcher does not pick up.",
    "The glow over the city looks wrong tonight — orange where it should be white.",
    "A family asks if the hotel has a basement. They do not explain.",
    "Someone has been filling water jugs from the outdoor spigot at night.",
    "Two rooms cancel within an hour of each other. Same reason: 'roads'.",
  ],
  disruption: [
    "Emergency broadcast repeats a phrase and then cuts to static.",
    "Power browns out for nine seconds. Every clock in the building disagrees now.",
    "A guest returns from the city with the windows of their car taped over.",
    "No sirens tonight. That is new, and it is worse.",
    "Mary asks whether her family can stay in an empty room. She is not joking.",
    "The vending machine is empty and nobody is coming to refill it.",
    "Someone scratched a symbol into the door of Room 7. It was not there yesterday.",
  ],
  collapse: [
    "Headlights on the access road slow, then keep going. They saw the sign.",
    "The city has been dark for so long you stopped looking that direction.",
    "A voice on the radio reads names for an hour, then apologizes and stops.",
    "Something moved along the fence line. It did not move like a person.",
    "A knock at 4 AM. By the time you reach the door, there is only a bag on the step.",
    "The tap runs brown for a minute before it clears. Ration it anyway.",
  ],
  shelter: [
    "Someone wrote the day count on the lobby wall. The number is wrong by two.",
    "A child asks when the guests are coming back. Nobody answers.",
    "Fuel gauge on the generator drops faster than the math says it should.",
    "Two people argue over a bunk assignment. It ends when the lights flicker.",
    "You catch yourself checking the barricades instead of the front desk.",
  ],
};

/**
 * Keystones move the story forward. Each needs a day floor plus something the
 * player did, so the world reacts to play rather than to the calendar alone.
 */
const KEYSTONES = [
  {
    id: "first-strange-guest",
    act: "normalcy",
    when: (state, s) => state.day >= 3 && s.checkIns >= 3,
    title: "A guest who does not sleep",
    body:
      "Room 2 kept the light on all night and left before dawn. The bed was " +
      "made. The key was on the pillow, still cold.",
    onFire: (state) => {
      setFlag(state, "sawStrangeGuest");
      addDispatch(state, "Local radio: 'minor outage' in the north districts.");
    },
  },
  {
    id: "advance-unease",
    act: "normalcy",
    advanceTo: "unease",
    when: (state, s) =>
      state.day >= 5 && s.checkIns >= 5 && hasFlag(state, "sawStrangeGuest"),
    title: "The roads get quieter",
    body:
      "Fewer headlights on the access road. The ones that come through do not " +
      "ask about rates — they ask how far the next town is.",
    onFire: (state) => {
      addDispatch(state, "Advisory: avoid travel into the city until further notice.");
    },
  },
  {
    id: "delivery-slips",
    act: "unease",
    when: (state, s) => s.orders >= 2 && state.day >= 7,
    title: "Your supplier stops answering",
    body:
      "The order goes through. The confirmation does not. Whoever normally " +
      "drives out here has not been heard from in two days.",
    onFire: (state) => {
      setFlag(state, "supplyUnreliable");
      addDispatch(state, "Freight lines suspended on the western corridor.");
    },
  },
  {
    id: "advance-disruption",
    act: "unease",
    advanceTo: "disruption",
    when: (state, s) => state.day >= 10 && (s.checkIns >= 8 || s.turnedAway >= 2),
    title: "The city goes dark",
    body:
      "At 11:40 the glow on the horizon goes out, block by block, like someone " +
      "walking a hallway turning off lights. Then the phones stop working.",
    onFire: (state) => {
      unlockShelterSystems(state);
      setFlag(state, "shelterUnlocked");
      addDispatch(state, "…stay indoors… do not approach… repeat, do not app—");
      addLog(
        state,
        "The office PC now lists water, food, fuel, medicine, and lumber."
      );
    },
  },
  {
    id: "first-survivor",
    act: "disruption",
    when: (state) => state.day >= 12,
    title: "Not a guest",
    body:
      "A woman walks up the access road with no car and no bag. She does not " +
      "ask the rate. She asks if the doors lock from the inside.",
    onFire: (state) => {
      setFlag(state, "firstSurvivor");
      state.story.tension += 8;
    },
  },
  {
    id: "advance-collapse",
    act: "disruption",
    advanceTo: "collapse",
    when: (state) => state.day >= 15 && hasFlag(state, "firstSurvivor"),
    title: "No one is coming",
    body:
      "No broadcast tonight. No traffic. Whatever is happening out there has " +
      "finished happening to the city, and it is working its way outward.",
    onFire: (state) => {
      setFlag(state, "defenseMatters");
      activateDefense(state);
      addLog(
        state,
        "Money is worth less than lumber now. Keep the barricades up (R) and the generator fed."
      );
    },
  },
  {
    id: "advance-shelter",
    act: "collapse",
    advanceTo: "shelter",
    when: (state) => state.day >= 19,
    title: "This is a shelter now",
    body:
      "Somebody moved the front desk against the door and nobody moved it back. " +
      "Rooms are bunks. Guests are survivors. Your job is that they stay alive.",
    onFire: (state) => {
      setFlag(state, "shelterDeclared");
    },
  },
];

function hintInterval(state) {
  const base =
    CONFIG.story.hintIntervalByAct[currentAct(state)] ??
    CONFIG.story.hintIntervalByAct.normalcy;
  return base * (0.6 + Math.random() * 0.8);
}

function fireHint(state) {
  const pool = HINTS[currentAct(state)] ?? HINTS.normalcy;
  const unseen = pool.filter((line) => !state.story.flags[`hint:${line}`]);
  const options = unseen.length ? unseen : pool;
  const line = options[Math.floor(Math.random() * options.length)];
  state.story.flags[`hint:${line}`] = true;
  addLog(state, line);
  addDispatch(state, line);
}

function fireKeystone(state, keystone) {
  state.story.fired[keystone.id] = state.day;

  if (keystone.advanceTo) {
    state.story.act = keystone.advanceTo;
    state.story.hintIn = hintInterval(state);
  }

  state.story.banner = {
    title: keystone.title,
    body: keystone.body,
    act: currentAct(state),
  };

  addLog(state, `${keystone.title} — ${keystone.body}`);
  keystone.onFire?.(state);
}

function checkKeystones(state) {
  for (const keystone of KEYSTONES) {
    if (state.story.fired[keystone.id]) continue;
    if (keystone.act && keystone.act !== currentAct(state)) continue;
    if (!keystone.when(state, state.story.stats)) continue;
    fireKeystone(state, keystone);
    // One beat at a time so the player can read it.
    return true;
  }
  return false;
}

/** Called every tick from advanceTime. */
export function updateStory(state, hoursPassed) {
  if (!state.story) return;

  state.story.hintIn -= hoursPassed;
  if (state.story.hintIn <= 0) {
    state.story.hintIn = hintInterval(state);
    fireHint(state);
  }

  checkKeystones(state);
}

/**
 * Story reacts to what the player does.
 * hook: "checkIn" | "checkOut" | "turnAway" | "unlock" | "order" | "repair" | "payday"
 */
export function storyHook(state, hook, payload = {}) {
  if (!state.story) return;
  const stats = state.story.stats;

  switch (hook) {
    case "checkIn":
      stats.checkIns += 1;
      if (payload.guest?.marked) {
        stats.markedServed += 1;
        state.story.tension += 4;
        addLog(
          state,
          `${payload.guest.name} signs the register slowly, like the name is unfamiliar.`
        );
      }
      break;
    case "checkOut":
      stats.checkOuts += 1;
      break;
    case "turnAway":
      stats.turnedAway += 1;
      if (payload.guest?.marked) stats.markedRefused += 1;
      if (isShelterEra(state)) state.story.tension += 3;
      break;
    case "unlock":
      if (isShelterEra(state)) {
        addLog(state, "Another room opened up. Another four people off the road.");
      }
      break;
    case "order":
      stats.orders += 1;
      break;
    case "repair":
      stats.repairs += 1;
      break;
    default:
      break;
  }

  checkKeystones(state);
}

/**
 * Some arrivals carry story weight. In later acts they also carry a "tell" —
 * a small wrong detail worth catching before you hand over a key.
 */
const TELLS = [
  "Their reflection in the lobby glass lags a half-second behind them.",
  "They sign the register with the wrong year and do not correct it.",
  "They are dressed for winter. It is not winter.",
  "They repeat your last words back to you, quietly, before answering.",
  "Their bag is empty. You can tell by the way it hangs.",
  "They do not blink while you explain the checkout time.",
  "They ask for a room facing away from the road.",
];

export function maybeMarkArrival(state, guest) {
  if (!state.story) return guest;
  const chance =
    CONFIG.story.markedChanceByAct[currentAct(state)] ??
    CONFIG.story.markedChanceByAct.normalcy;
  if (Math.random() >= chance) return guest;

  guest.marked = true;
  if (actIndex(state) >= ACT_ORDER.indexOf("unease")) {
    guest.tell = TELLS[Math.floor(Math.random() * TELLS.length)];
  }
  return guest;
}

/** Shown at the desk so the player can read a guest before admitting them. */
export function describeArrival(guest) {
  if (!guest) return "";
  if (guest.tell) return `${guest.name} — ${guest.tell}`;
  return guest.name;
}

/** Short HUD line — latest radio headline once anything has aired. */
export function storySignalText(state) {
  const latest = state.story?.media?.radioLog?.[0];
  if (latest) {
    const text = latest.headline;
    return text.length > 64 ? `${text.slice(0, 61)}...` : text;
  }
  if (currentAct(state) === "normalcy") return "Local AM — weather, roads";
  return ACT_LABELS[currentAct(state)];
}

/** UI pulls the pending keystone banner exactly once. */
export function takeStoryBanner(state) {
  const banner = state.story?.banner;
  if (!banner) return null;
  state.story.banner = null;
  return banner;
}

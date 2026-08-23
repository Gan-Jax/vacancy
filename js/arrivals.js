import { CONFIG } from "./config.js";
import { addLog } from "./state.js";
import { actIndex, ACT_ORDER, storyHook } from "./story.js";
import { countOccupants, getShelterStock } from "./shelter.js";
import {
  answerFor,
  availableQuestions,
  pickTiedStory,
} from "./media.js";

/**
 * The desk decision.
 *
 * Turning someone away only means something if the player has reasons, so every
 * arrival is weighed on three axes that all pull against each other:
 *
 *   capacity  — is there a bed at all
 *   sustain   — one more mouth costs measurable days of water and food
 *   threat    — evidence that this person is not what they claim
 *
 * And refusing is never free: sending away someone genuine costs humanity.
 * Evidence is deliberately probabilistic. Honest people have odd habits and
 * dangerous ones can look ordinary, so this stays a judgement call instead of a
 * lookup table.
 */

/** What the person actually is. The player never sees this directly. */
export const KIND = {
  traveler: "traveler",
  survivor: "survivor",
  wrong: "wrong",
};

const CLAIMS = {
  traveler: [
    "Driving through. Needs a bed and an early checkout.",
    "Says the next motel is another two hours and they are done driving.",
    "Here for work in the city. Paying by the night.",
    "Wants the quietest room you have. Does not say why.",
  ],
  survivor: [
    "Walked here. Asks how many people are already inside.",
    "Came from the east side. Will not talk about what they saw.",
    "Has a child with them. Asks only for somewhere with a door.",
    "Offers to work for a bunk. No money left.",
  ],
  wrong: [
    "Says they have a reservation. There is no reservation.",
    "Asks how many people are inside. Asks again, differently.",
    "Says they came from the city. The roads from the city are closed.",
    "Wants a room on the ground floor, near the back.",
  ],
};

/** Signs that genuinely indicate something is wrong. */
const DAMNING_SIGNS = [
  "Their reflection in the lobby glass lags a half-second behind them.",
  "They sign the register with the wrong year and do not correct it.",
  "They repeat your last words back to you, quietly, before answering.",
  "They do not blink while you explain the checkout time.",
  "Their bag is empty. You can tell by the way it hangs.",
  "They know Mary's name. Mary has not said it.",
  "They are not breathing when they think you are not looking.",
];

/** Signs that mean nothing at all. Honest people are strange too. */
const INNOCUOUS_SIGNS = [
  "They are dressed for winter. It is not winter.",
  "They ask for a room facing away from the road.",
  "They pay entirely in coins.",
  "They flinch at the ice machine.",
  "They will not put their bag down.",
  "They ask twice whether the doors lock from the inside.",
  "They keep checking the window behind you.",
  "They have not slept. It shows.",
];

const GENERIC_QUESTIONS = [
  {
    id: "where-from",
    prompt: "Where did you come from?",
    answers: {
      traveler: "Down the access road. I have been driving since morning.",
      survivor: "On foot. I do not want to say the last town out loud.",
      wrong: "The city. Everyone comes from the city.",
    },
  },
  {
    id: "how-long",
    prompt: "How long do you plan to stay?",
    answers: {
      traveler: "One night. Maybe two if the roads are still bad.",
      survivor: "Until it is safe. I do not know when that is.",
      wrong: "As long as you will let me. I can stay in any room.",
    },
  },
];

/** Hours burned by pressing an arrival for more information. */
const QUESTION_HOURS = 0.75;

function pick(list) {
  return list[Math.floor(Math.random() * list.length)];
}

function pickKind(state) {
  const act = actIndex(state);
  const roll = Math.random();

  // Act 1-2: paying travelers, with a rare wrong one to seed the doubt.
  if (act <= ACT_ORDER.indexOf("unease")) {
    if (roll < (act === 0 ? 0.05 : 0.14)) return KIND.wrong;
    return KIND.traveler;
  }
  // Act 3: money still works, but people are starting to just need shelter.
  if (act === ACT_ORDER.indexOf("disruption")) {
    if (roll < 0.24) return KIND.wrong;
    if (roll < 0.62) return KIND.survivor;
    return KIND.traveler;
  }
  // Act 4+: nobody is travelling for fun any more.
  if (roll < 0.3) return KIND.wrong;
  return KIND.survivor;
}

function buildSigns(kind) {
  const signs = [];
  const damningPool = [...DAMNING_SIGNS];
  const innocuousPool = [...INNOCUOUS_SIGNS];

  const take = (pool, damning) => {
    if (!pool.length) return;
    const index = Math.floor(Math.random() * pool.length);
    const [text] = pool.splice(index, 1);
    signs.push({ text, damning, revealed: false });
  };

  if (kind === KIND.wrong) {
    // Mostly real tells, plus noise so the read is never trivial.
    const damningCount = 1 + Math.floor(Math.random() * 3);
    for (let i = 0; i < damningCount; i++) take(damningPool, true);
    if (Math.random() < 0.5) take(innocuousPool, false);
  } else {
    const innocuousCount = Math.floor(Math.random() * 3);
    for (let i = 0; i < innocuousCount; i++) take(innocuousPool, false);
    // False positives: honest people occasionally look damning.
    if (Math.random() < 0.16) take(damningPool, true);
  }

  // Shuffle so damning signs are not always revealed first.
  signs.sort(() => Math.random() - 0.5);
  if (signs.length) signs[0].revealed = true;
  return signs;
}

export function createArrival(state, name) {
  const kind = pickKind(state);
  const story = pickTiedStory(state, kind);
  return {
    name,
    kind,
    storyId: story?.id ?? null,
    claim: pick(CLAIMS[kind]),
    signs: buildSigns(kind),
    questionsAsked: 0,
    maxQuestions: 2,
    askedQuestionIds: [],
    waitRemainingHours: CONFIG.waitPatienceHours,
    /** Kept for story hooks that only care that something was off. */
    marked: kind !== KIND.traveler,
  };
}

/** Questions the player can still put to this arrival. */
export function deskQuestions(state, guest) {
  const mediaQs = availableQuestions(state, guest);
  const asked = new Set(guest?.askedQuestionIds ?? []);
  const generic = GENERIC_QUESTIONS.filter((q) => !asked.has(q.id));
  const unusedMedia = mediaQs.filter((q) => !asked.has(`${q.storyId}:${q.id}`));
  return [...unusedMedia, ...generic];
}

export function revealedSigns(guest) {
  return (guest?.signs ?? []).filter((s) => s.revealed);
}

export function hiddenSignCount(guest) {
  return (guest?.signs ?? []).filter((s) => !s.revealed).length;
}

/** True if the player has been shown at least one genuine tell. */
export function hasVisibleTell(guest) {
  return revealedSigns(guest).some((s) => s.damning);
}

/**
 * The "why" panel: everything the player weighs before choosing.
 * Numbers only appear once the resource actually matters.
 */
export function assessArrival(state, guest) {
  const bunksFree = state.rooms.filter(
    (r) => r.unlocked && r.status === "clean"
  ).length;
  const bunksTotal = state.rooms.filter((r) => r.unlocked).length;

  const assessment = {
    bunksFree,
    bunksTotal,
    occupants: countOccupants(state),
    signsRevealed: revealedSigns(guest).length,
    questionsLeft: Math.max(0, guest.maxQuestions - guest.questionsAsked),
    shelter: null,
    humanity: state.story?.humanity ?? 100,
    paysRent: guest.kind === KIND.traveler,
  };

  if (state.shelter?.unlocked) {
    const use = CONFIG.shelterUse;
    const now = assessment.occupants;
    const after = now + 1;
    const days = (stock, perPerson) =>
      perPerson > 0 ? Math.floor(stock / (perPerson * Math.max(1, now))) : 0;
    const daysAfter = (stock, perPerson) =>
      perPerson > 0 ? Math.floor(stock / (perPerson * Math.max(1, after))) : 0;

    assessment.shelter = {
      waterDays: days(getShelterStock(state, "water"), use.waterPerPersonPerDay),
      waterDaysAfter: daysAfter(
        getShelterStock(state, "water"),
        use.waterPerPersonPerDay
      ),
      foodDays: days(getShelterStock(state, "food"), use.foodPerPersonPerDay),
      foodDaysAfter: daysAfter(
        getShelterStock(state, "food"),
        use.foodPerPersonPerDay
      ),
      integrity: Math.round(state.shelter.integrity),
      powered: state.shelter.powered,
    };
  }

  return assessment;
}

/**
 * Press for more information. Costs time.
 * If a media question is chosen, their answer is the tell — radio answers
 * are public knowledge, paper answers are not.
 */
export function askArrivalQuestion(state, guest, question = null) {
  if (!guest) return false;
  if (guest.questionsAsked >= guest.maxQuestions) {
    addLog(state, `${guest.name} stops answering questions.`);
    return false;
  }

  const options = deskQuestions(state, guest);
  const chosen = question ?? options[0] ?? GENERIC_QUESTIONS[0];
  guest.questionsAsked += 1;
  guest.waitRemainingHours -= QUESTION_HOURS;
  state.hour += QUESTION_HOURS;
  guest.askedQuestionIds = guest.askedQuestionIds || [];
  guest.askedQuestionIds.push(
    chosen.storyId ? `${chosen.storyId}:${chosen.id}` : chosen.id
  );

  const spoken = chosen.answers
    ? answerFor(guest, chosen)
    : `${guest.name} answers, and nothing stands out.`;

  guest.replies = guest.replies || [];
  guest.replies.push({
    prompt: chosen.prompt,
    spoken,
    source: chosen.source || "generic",
  });

  addLog(state, `You: "${chosen.prompt}"`);
  addLog(state, `${guest.name}: "${spoken}"`);

  if (chosen.source === "radio") {
    addLog(
      state,
      "They have heard the radio. Everyone has heard the radio."
    );
  }

  const hidden = (guest.signs ?? []).filter((s) => !s.revealed);
  if (hidden.length) {
    const sign = hidden[Math.floor(Math.random() * hidden.length)];
    sign.revealed = true;
  }
  return true;
}

/** Turn someone away. Never free. */
export function refuseArrival(state, guest) {
  if (!guest) return false;
  state.waitingGuests = state.waitingGuests.filter((g) => g !== guest);

  const story = state.story;
  if (guest.kind === KIND.traveler) {
    state.reputation = Math.max(0, state.reputation - 4);
    addLog(
      state,
      `Turned away ${guest.name}. They had money and somewhere else to be. (−4 reputation)`
    );
  } else if (guest.kind === KIND.survivor) {
    if (story) story.humanity = Math.max(0, story.humanity - 8);
    state.reputation = Math.max(0, state.reputation - 2);
    addLog(
      state,
      `Turned away ${guest.name}. They did not argue. They just walked back toward the road.`
    );
  } else {
    // The player does not get told they were right. Not yet.
    if (story) story.humanity = Math.max(0, story.humanity - 2);
    if (story) story.threatsRefused = (story.threatsRefused || 0) + 1;
    addLog(state, `Turned away ${guest.name}. They did not ask why.`);
    // Delayed confirmation so the call can be learned from later.
    if (story) {
      story.pendingVindication = story.pendingVindication || [];
      story.pendingVindication.push({ name: guest.name, day: state.day });
    }
  }

  storyHook(state, "turnAway", { guest });
  return true;
}

/**
 * Admitting a wrong one does not fail immediately — it fails at night, so the
 * player connects the consequence to the choice after the fact.
 */
export function armAdmittedThreat(state, guest) {
  if (guest?.kind !== KIND.wrong) return;
  if (!state.story) return;
  state.story.pendingThreats = state.story.pendingThreats || [];
  state.story.pendingThreats.push({
    name: guest.name,
    admittedDay: state.day,
    fireOnDay: state.day + 1,
  });
}

/** Called on day rollover. Resolves what admitting or refusing actually meant. */
export function resolveArrivalConsequences(state) {
  const story = state.story;
  if (!story) return;

  const shelterEra = actIndex(state) >= ACT_ORDER.indexOf("collapse");
  const stillPending = [];

  for (const threat of story.pendingThreats || []) {
    if (state.day < threat.fireOnDay) {
      stillPending.push(threat);
      continue;
    }

    const options = [];

    options.push(() => {
      const target = state.rooms.find((r) => r.status === "occupied");
      if (target) {
        target.status = "needs_inspection";
        target.guestName = null;
        target.dirtLevel = "heavy";
        target.hasHiddenDamage = true;
        addLog(
          state,
          `Room ${target.id} is empty this morning. The bed was not slept in and the window is open from the outside.`
        );
      } else {
        addLog(state, "Something moved through the halls last night. Nothing is missing that you can name.");
      }
    });

    if (state.shelter?.unlocked) {
      options.push(() => {
        const taken = Math.min(getShelterStock(state, "food"), 6);
        state.shelter.stock.food -= taken;
        const water = Math.min(getShelterStock(state, "water"), 5);
        state.shelter.stock.water -= water;
        addLog(
          state,
          `Stores were opened overnight. ${taken} food and ${water} water gone. The lock was not forced.`
        );
      });
    }

    if (state.shelter?.defenseActive) {
      options.push(() => {
        state.shelter.integrity = Math.max(0, state.shelter.integrity - 30);
        addLog(
          state,
          `A barricade was dismantled from the inside overnight (${Math.round(state.shelter.integrity)}%).`
        );
      });
    }

    pick(options)();
    addLog(state, `You think about ${threat.name}, and when you checked them in.`);
    if (shelterEra) story.humanity = Math.max(0, story.humanity - 4);
    story.tension += 10;
  }
  story.pendingThreats = stillPending;

  // Late confirmation that a refusal was the right call.
  const stillWaitingProof = [];
  for (const proof of story.pendingVindication || []) {
    if (state.day < proof.day + 2) {
      stillWaitingProof.push(proof);
      continue;
    }
    addLog(
      state,
      `Someone found what was left of ${proof.name} out past the treeline. It had not been a person for a while.`
    );
    story.humanity = Math.min(100, story.humanity + 3);
  }
  story.pendingVindication = stillWaitingProof;
}

import { CONFIG } from "./config.js";
import { createInitialState, addLog, formatClock, getTimeOfDayLabel } from "./state.js";
import {
  handleDeskAction,
  checkInAtDesk,
  advanceTime,
  unlockRoom,
  hireBob,
  hireMary,
  finishInspection,
  finishRepair,
  finishCleaning,
  getRoomUnlockCost,
  toggleVacancy,
  beginRepairPayment,
  getRepairCost,
} from "./economy.js";
import {
  inventoryHudSummary,
  lookupOrderable,
  orderableItemIds,
  placeInventoryOrder,
} from "./inventory.js";
import {
  ACT_LABELS,
  storySignalText,
  takeStoryBanner,
} from "./story.js";
import { reinforceBarricades, shelterHudSummary } from "./shelter.js";
import {
  askArrivalQuestion,
  assessArrival,
  deskQuestions,
  refuseArrival,
  revealedSigns,
} from "./arrivals.js";
import { markPaperRead } from "./media.js";
import {
  isStageOne,
  markTutorial,
  paperReadLog,
  showShelterHud,
  tutorialHudLines,
  tutorialHudNote,
  tutorialHudSummary,
  STAGE_ROOM_GATE,
  unlockedRoomCount,
} from "./stage.js";
import { Player, StaffNPC } from "./entities.js";
import {
  createInput,
  createLayout,
  drawWorld,
  drawInspectOverlay,
  getInspectTargets,
  hitInspectTarget,
} from "./render.js";

const canvas = document.getElementById("game");
const ctx = canvas.getContext("2d");

const moneyEl = document.getElementById("money");
const addCashBtn = document.getElementById("add-cash");
const skipDayBtn = document.getElementById("skip-day");
const dayEl = document.getElementById("day");
const clockEl = document.getElementById("clock");
const todEl = document.getElementById("tod");
const queueEl = document.getElementById("queue");
const reputationEl = document.getElementById("reputation");
const vacancyStatusEl = document.getElementById("vacancy-status");
const vacancyStatEl = document.getElementById("vacancy-stat");
const inventoryHudEl = document.getElementById("inventory-hud");
const tutorialHudEl = document.getElementById("tutorial-hud");
const tutorialSummaryEl = document.getElementById("tutorial-summary");
const tutorialListEl = document.getElementById("tutorial-list");
const shelterHudEl = document.getElementById("shelter-hud");
const deskAskHeadingEl = document.getElementById("desk-ask-heading");
const deskAskCopyEl = document.getElementById("desk-ask-copy");
const radioSubEl = document.getElementById("radio-sub");
const paperSubEl = document.getElementById("paper-sub");
const signalEl = document.getElementById("signal");
const storyBannerEl = document.getElementById("story-banner");
const storyActEl = document.getElementById("story-act");
const storyTitleEl = document.getElementById("story-title");
const storyBodyEl = document.getElementById("story-body");
const storyDismissBtn = document.getElementById("story-dismiss");
const deskModal = document.getElementById("desk-modal");
const deskNameEl = document.getElementById("desk-name");
const deskClaimEl = document.getElementById("desk-claim");
const deskSignsEl = document.getElementById("desk-signs");
const deskWhyEl = document.getElementById("desk-why");
const deskQuestionsEl = document.getElementById("desk-questions");
const deskReplyEl = document.getElementById("desk-reply");
const deskRefuseBtn = document.getElementById("desk-refuse");
const signalStatEl = document.getElementById("signal-stat");
const radioModal = document.getElementById("radio-modal");
const radioLogEl = document.getElementById("radio-log");
const radioCloseBtn = document.getElementById("radio-close");
const paperModal = document.getElementById("paper-modal");
const paperLogEl = document.getElementById("paper-log");
const paperCloseBtn = document.getElementById("paper-close");
const deskAdmitBtn = document.getElementById("desk-admit");
const deskCloseBtn = document.getElementById("desk-close");
const logEl = document.getElementById("log");
const hireBobBtn = document.getElementById("hire-bob");
const hireMaryBtn = document.getElementById("hire-mary");
const unlockBtn = document.getElementById("unlock-room");
const vacancyBtn = document.getElementById("toggle-vacancy");
const pcModal = document.getElementById("pc-modal");
const pcCloseBtn = document.getElementById("pc-close");
const pcStockEl = document.getElementById("pc-stock");
const pcOrderRows = document.getElementById("pc-order-rows");
const pcPendingEl = document.getElementById("pc-pending");
const pcOrderTotalEl = document.getElementById("pc-order-total");
const pcPlaceOrderBtn = document.getElementById("pc-place-order");

const layout = createLayout(canvas);
const state = createInitialState(layout.roomCount);
const input = createInput();
const player = new Player(layout.spawn.x + 40, layout.spawn.y);
let bob = null;
let mary = null;

let lastTime = performance.now();
let inspectMode = false;
let mouseCanvas = null;
let bannerOpen = false;

function canvasMousePos(e) {
  const rect = canvas.getBoundingClientRect();
  const scaleX = canvas.width / rect.width;
  const scaleY = canvas.height / rect.height;
  return {
    x: (e.clientX - rect.left) * scaleX,
    y: (e.clientY - rect.top) * scaleY,
  };
}

canvas.addEventListener("mousemove", (e) => {
  mouseCanvas = canvasMousePos(e);
});
canvas.addEventListener("mouseleave", () => {
  mouseCanvas = null;
});
canvas.addEventListener("click", (e) => {
  if (!inspectMode) return;
  const pos = canvasMousePos(e);
  mouseCanvas = pos;
  const hit = hitInspectTarget(getInspectTargets(layout, state), pos.x, pos.y);
  const x = Math.round(pos.x);
  const y = Math.round(pos.y);
  const line = hit
    ? `[inspect] ${hit.label} @ cursor (${x}, ${y}) | center (${hit.cx}, ${hit.cy}) | box ${Math.round(hit.x)},${Math.round(hit.y)} ${Math.round(hit.w)}x${Math.round(hit.h)}${hit.extra ? ` | ${hit.extra}` : ""}`
    : `[inspect] cursor (${x}, ${y}) — no labeled area`;
  addLog(state, line);
  console.log(line);
  markUIDirty();
  refreshUI(true);
});

addCashBtn.addEventListener("click", () => {
  state.money += 500;
  addLog(state, "Debug: +$500 cash.");
  markUIDirty();
  refreshUI(true);
});

/** Debug: run the sim forward a full day so later story acts are reachable. */
function skipDay() {
  const startDay = state.day;
  const wasPaused = state.paused;
  state.paused = false;
  const staffList = [bob, mary].filter(Boolean);
  for (let i = 0; i < 4000 && state.day === startDay; i++) {
    advanceTime(state, 0.05, layout, staffList);
  }
  state.paused = wasPaused;
  markUIDirty();
  refreshUI(true);
}

skipDayBtn.addEventListener("click", skipDay);

hireBobBtn.addEventListener("click", () => {
  if (hireBob(state)) {
    bob = StaffNPC.spawnAtHome(layout, {
      id: "bob",
      name: "Bob",
      role: "repair",
      color: "#ffb347",
      department: "maintenance",
    });
  }
  markUIDirty();
  refreshUI(true);
});

hireMaryBtn.addEventListener("click", () => {
  if (hireMary(state)) {
    mary = StaffNPC.spawnAtHome(layout, {
      id: "mary",
      name: "Mary",
      role: "housekeeping",
      color: "#e8a0bf",
      department: "housekeeping",
    });
  }
  markUIDirty();
  refreshUI(true);
});

unlockBtn.addEventListener("click", () => {
  unlockRoom(state);
  markUIDirty();
  refreshUI(true);
});

vacancyBtn.addEventListener("click", () => {
  toggleVacancy(state);
  markUIDirty();
  refreshUI(true);
});

let lastLogHead = "";
let uiDirty = true;

function markUIDirty() {
  uiDirty = true;
}

function refreshUI(force = false) {
  if (!force && !uiDirty) {
    // Clock still needs a light touch every frame
    clockEl.textContent = formatClock(state.hour);
    todEl.textContent = getTimeOfDayLabel(state.hour);
    return;
  }
  uiDirty = false;

  moneyEl.textContent = `$${state.money}`;
  dayEl.textContent = String(state.day);
  clockEl.textContent = formatClock(state.hour);
  todEl.textContent = getTimeOfDayLabel(state.hour);
  queueEl.textContent = String(state.waitingGuests.length);
  reputationEl.textContent = String(state.reputation);

  vacancyStatusEl.textContent = state.vacancyOpen ? "VACANCY" : "NO VACANCY";
  vacancyStatEl.classList.toggle("no-vacancy", !state.vacancyOpen);
  vacancyBtn.textContent = state.vacancyOpen
    ? "Set: NO VACANCY"
    : "Set: VACANCY";
  vacancyBtn.classList.toggle("vacancy-closed", !state.vacancyOpen);

  const logHead = state.messages[0] || "";
  if (force || logHead !== lastLogHead) {
    lastLogHead = logHead;
    logEl.innerHTML = state.messages.map((m) => `<li>${m}</li>`).join("");
  }

  hireBobBtn.disabled = state.bobHired || state.money < CONFIG.hireBobCost;
  hireBobBtn.textContent = state.bobHired
    ? "Bob hired (repairs)"
    : `Hire Bob (repairs) — $${CONFIG.hireBobCost}`;

  hireMaryBtn.disabled = state.maryHired || state.money < CONFIG.hireMaryCost;
  hireMaryBtn.textContent = state.maryHired
    ? "Mary hired (inspect + clean)"
    : `Hire Mary (inspect + clean) — $${CONFIG.hireMaryCost}`;

  const nextRoom = state.rooms.some((r) => !r.unlocked);
  const unlockCost = getRoomUnlockCost(state);
  unlockBtn.disabled = !nextRoom || state.money < unlockCost;
  unlockBtn.textContent = nextRoom
    ? `Unlock room — $${unlockCost}`
    : "All rooms unlocked";

  if (inventoryHudEl) {
    inventoryHudEl.textContent = inventoryHudSummary(state);
  }

  if (tutorialHudEl) {
    const summary = tutorialHudSummary(state);
    tutorialHudEl.classList.toggle("hidden", !summary);
    if (summary && tutorialSummaryEl && tutorialListEl) {
      tutorialSummaryEl.textContent = summary;
      const lines = tutorialHudLines(state);
      const note = tutorialHudNote(state);
      const rooms = unlockedRoomCount(state);
      tutorialListEl.innerHTML =
        lines
          .map(
            (item) =>
              `<li class="${item.done ? "done" : ""}">${item.done ? "✓" : "○"} ${item.label}</li>`
          )
          .join("") +
        `<li class="gate">Rooms open: ${rooms} / ${STAGE_ROOM_GATE}</li>` +
        (note ? `<li class="note">${note}</li>` : "");
    }
  }

  if (signalEl) {
    signalEl.textContent = storySignalText(state);
  }

  if (shelterHudEl) {
    let summary = "";
    if (showShelterHud(state)) {
      summary = shelterHudSummary(state);
      if (summary && state.story) {
        summary += ` · Humanity: ${state.story.humanity}%`;
      }
    }
    shelterHudEl.textContent = summary;
    shelterHudEl.classList.toggle("hidden", !summary);
  }

  if (state.pcOpen) refreshPcPanel();
}

/** Keystone events pause the game behind a banner so they land. */
function showStoryBanner(banner) {
  if (!storyBannerEl) return;
  storyActEl.textContent = ACT_LABELS[banner.act] ?? "";
  storyTitleEl.textContent = banner.title;
  storyBodyEl.textContent = banner.body;
  storyBannerEl.classList.remove("hidden");
  storyBannerEl.setAttribute("aria-hidden", "false");
  bannerOpen = true;
}

function dismissStoryBanner() {
  if (!storyBannerEl) return;
  storyBannerEl.classList.add("hidden");
  storyBannerEl.setAttribute("aria-hidden", "true");
  bannerOpen = false;
  markUIDirty();
}

function openRadioLog() {
  const entries = state.story?.media?.radioLog ?? [];
  radioLogEl.innerHTML = entries.length
    ? entries
        .map(
          (e) => `<article class="media-item">
            <span class="media-meta">Day ${e.day} · on the air</span>
            <h3>${e.headline}</h3>
            <p>${e.body}</p>
          </article>`
        )
        .join("")
    : `<p class="media-empty">Nothing but weather and road reports so far.</p>`;
  if (radioSubEl) {
    radioSubEl.textContent = isStageOne(state)
      ? "Local AM — weather, road work, and inn ads."
      : "KCLR and whatever is still on the air. Everyone in the lobby hears this.";
  }
  state.mediaOpen = "radio";
  state.paused = true;
  radioModal.classList.remove("hidden");
  radioModal.setAttribute("aria-hidden", "false");
}

function closeRadioLog() {
  state.mediaOpen = null;
  if (!state.pcOpen && !state.deskGuest) state.paused = false;
  radioModal.classList.add("hidden");
  radioModal.setAttribute("aria-hidden", "true");
  markUIDirty();
}

function openPaperLog() {
  const papers = state.story?.media?.papers ?? [];
  if (papers[0] && !papers[0].read) {
    markPaperRead(state);
    addLog(state, paperReadLog(state));
  }
  paperLogEl.innerHTML = papers.length
    ? papers
        .map(
          (e) => `<article class="media-item">
            <span class="media-meta">Day ${e.day} · ${e.read ? "read" : "unread"}</span>
            <h3>${e.headline}</h3>
            <p>${e.body}</p>
          </article>`
        )
        .join("")
    : `<p class="media-empty">No paper today. The stack is empty.</p>`;
  if (paperSubEl) {
    paperSubEl.textContent = isStageOne(state)
      ? "The morning Gazette — weather, roads, and weekend rates."
      : "What the broadcast left out. Reading an issue unlocks questions they have not all rehearsed.";
  }
  state.mediaOpen = "paper";
  state.paused = true;
  paperModal.classList.remove("hidden");
  paperModal.setAttribute("aria-hidden", "false");
  markUIDirty();
  refreshUI(true);
}

function closePaperLog() {
  state.mediaOpen = null;
  if (!state.pcOpen && !state.deskGuest) state.paused = false;
  paperModal.classList.add("hidden");
  paperModal.setAttribute("aria-hidden", "true");
  markUIDirty();
}

function openPc() {
  markTutorial(state, "officePc");
  state.pcOpen = true;
  state.paused = true;
  pcModal.classList.remove("hidden");
  pcModal.setAttribute("aria-hidden", "false");
  buildPcOrderRows();
  refreshPcPanel();
  markUIDirty();
}

function closePc() {
  state.pcOpen = false;
  state.paused = false;
  pcModal.classList.add("hidden");
  pcModal.setAttribute("aria-hidden", "true");
  markUIDirty();
}

function buildPcOrderRows() {
  pcOrderRows.innerHTML = orderableItemIds(state)
    .map((id) => {
      const def = lookupOrderable(state, id).def;
      return `<div class="pc-row">
        <label for="order-${id}">${def.label} ($${def.unitCost})</label>
        <input id="order-${id}" data-item="${id}" type="number" min="0" step="${def.orderPack}" value="0" />
        <span class="pack">pack ${def.orderPack}</span>
      </div>`;
    })
    .join("");

  pcOrderRows.querySelectorAll("input").forEach((input) => {
    input.addEventListener("input", refreshPcPanel);
  });
}

function readPcOrderQuantities() {
  const quantities = {};
  pcOrderRows.querySelectorAll("input[data-item]").forEach((input) => {
    quantities[input.dataset.item] = Number(input.value) || 0;
  });
  return quantities;
}

function refreshPcPanel() {
  const stockParts = orderableItemIds(state).map((id) => {
    const entry = lookupOrderable(state, id);
    const onHand =
      entry.kind === "shelter"
        ? state.shelter.stock[id]
        : state.inventory.stock[id];
    return `${entry.def.label}: ${onHand}`;
  });
  pcStockEl.textContent = `On hand — ${stockParts.join(" · ")}`;

  const quantities = readPcOrderQuantities();
  let total = 0;
  for (const [id, qty] of Object.entries(quantities)) {
    const entry = lookupOrderable(state, id);
    if (entry) total += qty * entry.def.unitCost;
  }
  pcOrderTotalEl.textContent = `Total: $${total}`;

  if (!state.inventory.pendingOrders.length) {
    pcPendingEl.textContent = "No deliveries in transit.";
  } else {
    pcPendingEl.textContent = state.inventory.pendingOrders
      .map((o) => {
        const parts = Object.entries(o.items)
          .map(([id, q]) => `${q} ${lookupOrderable(state, id)?.def.label ?? id}`)
          .join(", ");
        return `In transit (${Math.ceil(o.hoursLeft)}h): ${parts}`;
      })
      .join(" | ");
  }
}

/* ---------- Desk review: admit or turn away ---------- */

function openDeskReview() {
  const guest = state.waitingGuests[0];
  if (!guest) return;
  state.deskGuest = guest;
  state.paused = true;
  deskModal.classList.remove("hidden");
  deskModal.setAttribute("aria-hidden", "false");
  refreshDeskReview();
  markUIDirty();
}

function closeDeskReview() {
  state.deskGuest = null;
  state.paused = false;
  deskModal.classList.add("hidden");
  deskModal.setAttribute("aria-hidden", "true");
  markUIDirty();
  refreshUI(true);
}

function whyRow(label, value, warn = false) {
  return `<div class="desk-why-row${warn ? " warn" : ""}"><span>${label}</span><span>${value}</span></div>`;
}

function refreshDeskReview() {
  const guest = state.deskGuest;
  if (!guest) return;

  deskNameEl.textContent = guest.name;
  deskClaimEl.textContent = guest.claim;

  const replies = guest.replies ?? [];
  if (deskReplyEl) {
    deskReplyEl.innerHTML = replies.length
      ? replies
          .map((r) => {
            const cls = r.source === "paper" || r.source === "radio" ? r.source : "";
            return `<div class="desk-exchange ${cls}">
              <p class="you">You: ${r.prompt}</p>
              <p class="them">${guest.name}: “${r.spoken}”</p>
            </div>`;
          })
          .join("")
      : `<p class="desk-hint">Ask something. Their answer stays here — not just in the log.</p>`;
  }

  const signs = revealedSigns(guest);
  deskSignsEl.innerHTML = signs.length
    ? signs.map((s) => `<li>${s.text}</li>`).join("")
    : `<li class="none">Nothing stands out yet.</li>`;

  if (deskAskHeadingEl) {
    deskAskHeadingEl.textContent = isStageOne(state)
      ? "Check-in questions"
      : "Ask from what you have heard";
  }
  if (deskAskCopyEl) {
    deskAskCopyEl.textContent = isStageOne(state)
      ? "Ask the usual check-in questions — name, stay length, and payment."
      : "Radio questions are public. Paper questions only appear after you read the issue.";
  }

  const why = assessArrival(state, guest);
  const rows = [];

  rows.push(
    whyRow("Rooms ready", `${why.bunksFree} of ${why.bunksTotal}`, why.bunksFree === 0)
  );
  rows.push(whyRow("Guests staying", String(why.occupants)));

  if (why.shelter && !isStageOne(state)) {
    const w = why.shelter;
    rows.push(
      whyRow(
        "Water",
        `${w.waterDays}d → ${w.waterDaysAfter}d if admitted`,
        w.waterDaysAfter <= 2
      )
    );
    rows.push(
      whyRow(
        "Food",
        `${w.foodDays}d → ${w.foodDaysAfter}d if admitted`,
        w.foodDaysAfter <= 2
      )
    );
    rows.push(
      whyRow("Barricades", `${w.integrity}%`, w.integrity < 40)
    );
    rows.push(whyRow("Humanity", `${why.humanity}%`, why.humanity < 50));
  } else {
    rows.push(
      whyRow("Pays for the room", why.paysRent ? "Yes" : "No", !why.paysRent)
    );
  }

  // Deliberately no count of what is still hidden — the player has to decide
  // whether they have seen enough, which is the whole point of the mechanic.
  deskWhyEl.innerHTML = rows.join("");

  const canAsk = why.questionsLeft > 0;
  const questions = deskQuestions(state, guest);
  if (!questions.length) {
    deskQuestionsEl.innerHTML =
      `<p class="desk-hint">${canAsk ? "Nothing left to ask." : "They stop answering."}</p>`;
  } else {
    deskQuestionsEl.innerHTML = questions
      .map((q, i) => {
        const src = q.source === "paper" ? "paper" : q.source === "radio" ? "radio" : "generic";
        const label =
          src === "paper" ? "Paper" : src === "radio" ? "Radio — they have heard this too" : "Basic";
        return `<button type="button" class="desk-q ${src}" data-q="${i}" ${canAsk ? "" : "disabled"}>
          <span class="src">${label}</span>${q.prompt}
        </button>`;
      })
      .join("");
    deskQuestionsEl.querySelectorAll("button[data-q]").forEach((btn) => {
      const question = questions[Number(btn.dataset.q)];
      btn.addEventListener("click", () => {
        if (!state.deskGuest || !question) return;
        askArrivalQuestion(state, state.deskGuest, question);
        refreshDeskReview();
        markUIDirty();
        refreshUI(true);
      });
    });
  }

  deskAdmitBtn.disabled = why.bunksFree === 0;
  deskAdmitBtn.textContent = why.bunksFree === 0 ? "No room ready" : "Admit";
}

deskRefuseBtn.addEventListener("click", () => {
  const guest = state.deskGuest;
  if (!guest) return;
  refuseArrival(state, guest);
  closeDeskReview();
});

deskAdmitBtn.addEventListener("click", () => {
  const guest = state.deskGuest;
  if (!guest) return;
  checkInAtDesk(state, layout, guest);
  closeDeskReview();
});

deskCloseBtn.addEventListener("click", closeDeskReview);
deskModal.addEventListener("click", (e) => {
  if (e.target === deskModal) closeDeskReview();
});

storyDismissBtn.addEventListener("click", dismissStoryBanner);

pcCloseBtn.addEventListener("click", closePc);
pcModal.addEventListener("click", (e) => {
  if (e.target === pcModal) closePc();
});
pcPlaceOrderBtn.addEventListener("click", () => {
  if (placeInventoryOrder(state, readPcOrderQuantities())) {
    buildPcOrderRows();
    refreshPcPanel();
    markUIDirty();
    refreshUI(true);
  } else {
    refreshPcPanel();
    markUIDirty();
    refreshUI(true);
  }
});
signalStatEl.addEventListener("click", openRadioLog);
radioCloseBtn.addEventListener("click", closeRadioLog);
radioModal.addEventListener("click", (e) => {
  if (e.target === radioModal) closeRadioLog();
});
paperCloseBtn.addEventListener("click", closePaperLog);
paperModal.addEventListener("click", (e) => {
  if (e.target === paperModal) closePaperLog();
});

window.addEventListener("keydown", (e) => {
  if (e.key !== "Escape") return;
  if (state.pcOpen) closePc();
  else if (state.mediaOpen === "radio") closeRadioLog();
  else if (state.mediaOpen === "paper") closePaperLog();
  else if (state.deskGuest) closeDeskReview();
});

function handleInteract() {
  if (state.pcOpen) return;
  if (player.activeTask) return;

  const deskQueue =
    state.waitingGuests.length > 0 ||
    state.activeGuests.some((g) => g.phase === "waiting_checkout");
  const target = player.getInteractTarget(
    state.rooms,
    layout,
    [bob, mary].filter(Boolean),
    deskQueue
  );
  if (!target) return;

  if (target.kind === "radio") {
    openRadioLog();
    return;
  }

  if (target.kind === "newspaper") {
    openPaperLog();
    return;
  }

  if (target.kind === "desk") {
    const result = handleDeskAction(state, layout, [bob, mary].filter(Boolean));
    if (result === "review") openDeskReview();
    markUIDirty();
    return;
  }

  if (target.kind === "office") {
    openPc();
    return;
  }

  if (target.kind === "sign") {
    toggleVacancy(state);
    markUIDirty();
    return;
  }

  const room = target.room;
  if (!room.unlocked) {
    const nextLocked = state.rooms.find((r) => !r.unlocked);
    if (nextLocked && room.id !== nextLocked.id) {
      addLog(state, `Unlock Room ${nextLocked.id} first.`);
    } else {
      unlockRoom(state);
    }
    markUIDirty();
    return;
  }

  if (room.worker && room.worker !== "player") return;

  if (room.status === "needs_inspection") {
    player.startTask("inspect", room);
    addLog(state, `Inspecting Room ${room.id}...`);
  } else if (room.status === "dirty") {
    player.startTask("clean", room);
    addLog(
      state,
      `Cleaning Room ${room.id} (${room.dirtLevel}, ${CONFIG.dirtHours[room.dirtLevel]}h)...`
    );
  } else if (room.status === "needs_repair") {
    const cost = getRepairCost(state, room.repairLevel);
    const paid = beginRepairPayment(state, room);
    if (paid == null) {
      addLog(
        state,
        `Need $${cost} for ${room.repairLevel} repair parts on Room ${room.id}.`
      );
      markUIDirty();
      return;
    }
    player.startTask("repair", room);
    addLog(
      state,
      `Repairing Room ${room.id} (${room.repairLevel}, ${CONFIG.repairHours[room.repairLevel]}h, −$${cost})...`
    );
  } else if (room.status === "clean") {
    addLog(state, `Room ${room.id} is ready for guests.`);
  } else if (room.status === "occupied") {
    addLog(state, `${room.guestName} is still staying in Room ${room.id}.`);
  }
  markUIDirty();
}

function gameLoop(now) {
  const dt = Math.min(0.05, (now - lastTime) / 1000);
  lastTime = now;

  try {
    if (input.consumePause()) {
      state.paused = !state.paused;
      addLog(state, state.paused ? "Game paused." : "Game resumed.");
      markUIDirty();
    }

    if (input.consumeVacancyToggle()) {
      toggleVacancy(state);
      markUIDirty();
    }

    if (input.consumeInspectToggle()) {
      inspectMode = !inspectMode;
      addLog(
        state,
        inspectMode
          ? "Inspect mode ON — hover for info, click to log a copy-paste line (X to exit)."
          : "Inspect mode OFF."
      );
      markUIDirty();
    }

    if (!bannerOpen) {
      const pending = takeStoryBanner(state);
      if (pending) {
        showStoryBanner(pending);
        markUIDirty();
      }
    }

    if (input.consumeReinforce() && state.shelter?.unlocked) {
      reinforceBarricades(state, 1);
      markUIDirty();
    }

    if (
      !state.paused &&
      !state.pcOpen &&
      !inspectMode &&
      !bannerOpen &&
      !state.deskGuest &&
      !state.mediaOpen
    ) {
      const logBefore = state.messages[0];
      const moneyBefore = state.money;
      const queueBefore = state.waitingGuests.length;

      const staffList = [bob, mary].filter(Boolean);
      advanceTime(state, dt, layout, staffList);

      const result = player.update(input, dt, layout, state.rooms);
      if (result?.completed === "inspect") finishInspection(state, result.room);
      if (result?.completed === "repair") finishRepair(state, result.room);
      if (result?.completed === "clean") finishCleaning(state, result.room);
      if (result) markUIDirty();

      if (bob) bob.update(dt, state, layout);
      if (mary) mary.update(dt, state, layout);

      if (input.consumeInteract()) handleInteract();

      if (
        state.messages[0] !== logBefore ||
        state.money !== moneyBefore ||
        state.waitingGuests.length !== queueBefore
      ) {
        markUIDirty();
      }
    } else if (state.pcOpen && input.consumeInteract()) {
      // ignore E while PC is open
    }

    drawWorld(ctx, state, layout, player, [bob, mary].filter(Boolean));
    drawInspectOverlay(ctx, layout, state, mouseCanvas, inspectMode);
    refreshUI();
  } catch (err) {
    console.error("Game loop error:", err);
    markUIDirty();
  }

  requestAnimationFrame(gameLoop);
}

// Debug handle so story/shelter/arrival state can be inspected from the console.
window.game = {
  state,
  layout,
  player,
  skipDay,
  openDeskReview,
  get staff() {
    return [bob, mary].filter(Boolean);
  },
};

const layoutProblems = layout.validate();
if (layoutProblems.length) {
  console.warn("[floorplan] unwalkable areas:", layoutProblems);
  for (const problem of layoutProblems.slice(0, 5)) {
    addLog(state, `Layout problem: ${problem}`);
  }
} else {
  addLog(
    state,
    `${layout.floor.name}: ${layout.roomCount} rooms, every door reachable.`
  );
}

refreshUI(true);
requestAnimationFrame(gameLoop);

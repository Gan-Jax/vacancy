import { addLog } from "./state.js";

/**
 * Stage 1 is a normal roadside hotel. Stage 2 (unease, highway stories,
 * mixed arrivals) waits until the tutorial is done AND the 7th room is open.
 * Day 7 is not the gate.
 */

export const STAGE_ROOM_GATE = 7;

export const TUTORIAL_OBJECTIVES = [
  { id: "checkIn", label: "Check in a guest" },
  { id: "vacancySign", label: "Flip the vacancy sign" },
  { id: "roomWork", label: "Inspect or clean a room" },
  { id: "hireStaff", label: "Hire Bob or Mary" },
  { id: "officePc", label: "Open the office PC" },
];

export function isStageOne(state) {
  return (state.stage ?? 1) < 2;
}

export function tutorialDone(state) {
  const tutorial = state.tutorial;
  if (!tutorial) return false;
  return TUTORIAL_OBJECTIVES.every((item) => tutorial[item.id]);
}

export function unlockedRoomCount(state) {
  return (state.rooms ?? []).filter((room) => room.unlocked).length;
}

export function seventhRoomUnlocked(state) {
  return unlockedRoomCount(state) >= STAGE_ROOM_GATE;
}

export function tutorialCompletedCount(state) {
  const tutorial = state.tutorial;
  if (!tutorial) return 0;
  return TUTORIAL_OBJECTIVES.filter((item) => tutorial[item.id]).length;
}

export function showShelterHud(state) {
  return !isStageOne(state) && Boolean(state.shelter?.unlocked);
}

export function markTutorial(state, id) {
  if (!state.tutorial) {
    maybeAdvanceStage(state);
    return false;
  }
  if (!TUTORIAL_OBJECTIVES.some((item) => item.id === id)) return false;
  const already = Boolean(state.tutorial[id]);
  if (!already) state.tutorial[id] = true;
  maybeAdvanceStage(state);
  return !already;
}

export function maybeAdvanceStage(state) {
  if (!isStageOne(state)) return false;
  if (!tutorialDone(state) || !seventhRoomUnlocked(state)) return false;

  state.stage = 2;
  addLog(state, "This is no longer only a hotel.");
  if (state.story) {
    state.story.banner = {
      title: "This is no longer only a hotel",
      body: "The roadside inn is still taking guests. The road ahead will not stay this simple.",
      act: state.story.act || "normalcy",
    };
  }
  return true;
}

export function tutorialHudSummary(state) {
  if (!isStageOne(state)) return "";
  const done = tutorialCompletedCount(state);
  const rooms = unlockedRoomCount(state);
  return `Today's tasks ${done}/${TUTORIAL_OBJECTIVES.length} · rooms ${rooms}/${STAGE_ROOM_GATE}`;
}

export function tutorialHudLines(state) {
  if (!isStageOne(state)) return [];
  const tutorial = state.tutorial ?? {};
  const lines = TUTORIAL_OBJECTIVES.map((item) => ({
    id: item.id,
    label: item.label,
    done: Boolean(tutorial[item.id]),
  }));
  return lines;
}

export function tutorialHudNote(state) {
  if (!isStageOne(state)) return "";
  if (tutorialDone(state) && !seventhRoomUnlocked(state)) {
    return "Tasks done. Unlock Room 7.";
  }
  if (!tutorialDone(state) && seventhRoomUnlocked(state)) {
    return "Room 7 is open. Finish today's tasks.";
  }
  return "";
}

export function paperReadLog(state) {
  if (isStageOne(state)) return "You read today's paper.";
  return "You read today's paper. New questions are available at the desk.";
}

import { getRepairCost, getRoomUnlockCost } from "./economy.js";
import { CONFIG } from "./config.js";
import { AREA, FLAGSHIP_GROUND, createFloor, innBuildingRect } from "./floorplan.js";
import { buildNavGrid, validateFloor } from "./nav.js";

/** Keyboard input helper. */
export function createInput() {
  const keys = new Set();

  window.addEventListener("keydown", (e) => {
    keys.add(e.key.toLowerCase());
    if (["arrowup", "arrowdown", "arrowleft", "arrowright", " "].includes(e.key.toLowerCase())) {
      e.preventDefault();
    }
  });

  window.addEventListener("keyup", (e) => {
    keys.delete(e.key.toLowerCase());
  });

  return {
    up: () => keys.has("w") || keys.has("arrowup"),
    down: () => keys.has("s") || keys.has("arrowdown"),
    left: () => keys.has("a") || keys.has("arrowleft"),
    right: () => keys.has("d") || keys.has("arrowright"),
    interact: () => keys.has("e") || keys.has(" "),
    pause: () => keys.has("p"),
    consumePause() {
      if (keys.has("p")) {
        keys.delete("p");
        return true;
      }
      return false;
    },
    consumeVacancyToggle() {
      if (keys.has("v")) {
        keys.delete("v");
        return true;
      }
      return false;
    },
    consumeInteract() {
      if (keys.has("e") || keys.has(" ")) {
        keys.delete("e");
        keys.delete(" ");
        return true;
      }
      return false;
    },
    consumeInspectToggle() {
      if (keys.has("x")) {
        keys.delete("x");
        return true;
      }
      return false;
    },
    consumeReinforce() {
      if (keys.has("r")) {
        keys.delete("r");
        return true;
      }
      return false;
    },
  };
}

/**
 * The layout is a thin view over the generated floorplan: it exposes named
 * spots the rest of the game asks for, so nothing else needs to know how the
 * building is shaped.
 */
export function createLayout(canvas) {
  const width = canvas.width;
  const height = canvas.height;
  const building = innBuildingRect(FLAGSHIP_GROUND, width, height);

  const floor = createFloor(FLAGSHIP_GROUND, building);
  const navGrid = buildNavGrid(floor);

  const desk = floor.frontDesk;
  const housekeeping = floor.departments.housekeeping;
  const maintenance = floor.departments.maintenance;
  const deptForStaff = {
    mary: housekeeping,
    bob: maintenance,
    housekeeping,
    maintenance,
  };

  const lobbyRadio = {
    x: desk.x - 110,
    y: desk.y - 8,
    w: 36,
    h: 28,
  };
  const newspaper = {
    x: desk.x - 38,
    y: desk.y + 4,
    w: 28,
    h: 18,
  };

  return {
    width,
    height,
    building,
    floor,
    navGrid,
    tile: floor.tile,
    rooms: floor.rooms,
    roomCount: floor.rooms.length,
    roomCenters: floor.rooms.map((room) => room.center),
    cols: floor.roomsPerRow,
    lobby: floor.lobby,
    office: floor.office,
    frontDesk: desk,
    departments: floor.departments,
    maidRoom: housekeeping,
    handymanCloset: maintenance,
    lobbyRadio,
    newspaper,
    vacancySign: { x: width / 2, y: height - 28, w: 140, h: 36 },
    spawn: { x: desk.x, y: desk.y + 40 },

    /** Public spot just outside a guest room's door. */
    roomDoor(roomId) {
      return floor.rooms[roomId - 1]?.approach ?? this.deskApproach();
    },
    roomInterior(roomId) {
      return floor.rooms[roomId - 1]?.center ?? this.deskApproach();
    },
    roomRect(roomId) {
      return floor.rooms[roomId - 1]?.rect ?? null;
    },
    officeDoor() {
      return floor.office.approach;
    },
    /** Where characters queue up to reach the desk. */
    deskApproach() {
      return { x: desk.x, y: desk.y + 46 };
    },
    staffHome(key) {
      const dept = deptForStaff[key] ?? maintenance;
      return { x: dept.x, y: dept.y };
    },
    checkInLineSlot(index) {
      return { x: desk.x + 96 + index * 40, y: desk.y + 30 };
    },
    checkoutLineSlot(index) {
      // Wrap the queue into two rows so a full house never spills into the
      // office or through the lobby's south wall.
      const perRow = 8;
      const col = index % perRow;
      const row = Math.min(1, Math.floor(index / perRow));
      return { x: desk.x - 96 - col * 38, y: desk.y + 24 + row * 26 };
    },
    staffPaySlot(staffId) {
      const offset = staffId === "mary" ? 46 : 0;
      return { x: desk.x - 30 + offset, y: desk.y - 40 };
    },
    /** Startup self-check that the generated floor is actually walkable. */
    validate() {
      return validateFloor(navGrid, floor, this.deskApproach());
    },
  };
}

/** Build labeled hit targets for the layout inspector (press X). */
export function getInspectTargets(layout, state) {
  const targets = [];
  const push = (label, rect, extra = "") => {
    targets.push({
      label,
      x: rect.x,
      y: rect.y,
      w: rect.w,
      h: rect.h,
      cx: Math.round(rect.x + rect.w / 2),
      cy: Math.round(rect.y + rect.h / 2),
      extra,
    });
  };

  push("Building", layout.building);

  for (const area of layout.floor.areas) {
    const room = area.roomId ? state.rooms[area.roomId - 1] : null;
    const extra = room
      ? `status: ${room.status}${room.unlocked ? "" : " (locked)"}`
      : area.token
        ? `permit: ${area.token}`
        : "";
    push(area.label, area.rect, extra);
  }

  for (const room of layout.floor.rooms) {
    push(
      `Room ${room.id} doorway`,
      { x: room.approach.x - 12, y: room.approach.y - 12, w: 24, h: 24 },
      `approach (${Math.round(room.approach.x)}, ${Math.round(room.approach.y)})`
    );
  }

  const desk = layout.frontDesk;
  push("Front desk", {
    x: desk.x - desk.w / 2,
    y: desk.y - desk.h / 2,
    w: desk.w,
    h: desk.h,
  });
  const deskApproach = layout.deskApproach();
  push(
    "Desk approach",
    { x: deskApproach.x - 12, y: deskApproach.y - 12, w: 24, h: 24 },
    `(${Math.round(deskApproach.x)}, ${Math.round(deskApproach.y)})`
  );

  if (layout.lobbyRadio) {
    const r = layout.lobbyRadio;
    push("Lobby radio", {
      x: r.x - r.w / 2,
      y: r.y - r.h / 2,
      w: r.w,
      h: r.h,
    });
  }
  if (layout.newspaper) {
    const n = layout.newspaper;
    push("Newspaper stack", {
      x: n.x - n.w / 2,
      y: n.y - n.h / 2,
      w: n.w,
      h: n.h,
    });
  }

  const sign = layout.vacancySign;
  push("Vacancy sign", {
    x: sign.x - sign.w / 2,
    y: sign.y - sign.h / 2,
    w: sign.w,
    h: sign.h,
  });

  // Smallest first so hovering prefers doorways over the rooms behind them.
  targets.sort((a, b) => a.w * a.h - b.w * b.h);
  return targets;
}

export function hitInspectTarget(targets, mx, my) {
  for (const t of targets) {
    if (mx >= t.x && mx <= t.x + t.w && my >= t.y && my <= t.y + t.h) {
      return t;
    }
  }
  return null;
}

/** Draw hover inspector overlay while X-mode is on. */
export function drawInspectOverlay(ctx, layout, state, mouse, enabled) {
  if (!enabled) return;

  const targets = getInspectTargets(layout, state);
  const hit = mouse ? hitInspectTarget(targets, mouse.x, mouse.y) : null;

  ctx.save();
  ctx.fillStyle = "rgba(8, 12, 24, 0.2)";
  ctx.fillRect(0, 0, layout.width, layout.height);

  for (const t of targets) {
    ctx.strokeStyle = "rgba(255, 209, 102, 0.22)";
    ctx.lineWidth = 1;
    ctx.strokeRect(t.x, t.y, t.w, t.h);
  }

  if (hit) {
    ctx.strokeStyle = "#ffd166";
    ctx.lineWidth = 2;
    ctx.strokeRect(hit.x, hit.y, hit.w, hit.h);
    ctx.fillStyle = "rgba(255, 209, 102, 0.12)";
    ctx.fillRect(hit.x, hit.y, hit.w, hit.h);

    const lines = [
      hit.label,
      `pos: (${hit.cx}, ${hit.cy})`,
      `box: ${Math.round(hit.x)}, ${Math.round(hit.y)}  ${Math.round(hit.w)}×${Math.round(hit.h)}`,
    ];
    if (hit.extra) lines.push(hit.extra);
    if (mouse) lines.push(`cursor: (${Math.round(mouse.x)}, ${Math.round(mouse.y)})`);

    const pad = 8;
    const lineH = 16;
    const boxW = 280;
    const boxH = pad * 2 + lines.length * lineH;
    let bx = (mouse?.x ?? hit.cx) + 14;
    let by = (mouse?.y ?? hit.cy) + 14;
    if (bx + boxW > layout.width - 8) bx = layout.width - boxW - 8;
    if (by + boxH > layout.height - 8) by = layout.height - boxH - 8;

    ctx.fillStyle = "rgba(16, 21, 32, 0.92)";
    ctx.fillRect(bx, by, boxW, boxH);
    ctx.strokeStyle = "#ffd166";
    ctx.lineWidth = 1;
    ctx.strokeRect(bx, by, boxW, boxH);
    ctx.fillStyle = "#ffd166";
    ctx.font = "bold 13px Segoe UI";
    ctx.fillText(lines[0], bx + pad, by + pad + 12);
    ctx.fillStyle = "#e8edf5";
    ctx.font = "12px Segoe UI";
    for (let i = 1; i < lines.length; i++) {
      ctx.fillText(lines[i], bx + pad, by + pad + 12 + i * lineH);
    }
  } else if (mouse) {
    ctx.fillStyle = "rgba(16, 21, 32, 0.85)";
    ctx.fillRect(mouse.x + 12, mouse.y + 12, 150, 36);
    ctx.fillStyle = "#9aa8c0";
    ctx.font = "12px Segoe UI";
    ctx.fillText(`cursor: (${Math.round(mouse.x)}, ${Math.round(mouse.y)})`, mouse.x + 20, mouse.y + 34);
  }

  ctx.fillStyle = "#ffd166";
  ctx.font = "bold 12px Segoe UI";
  ctx.fillText("INSPECT (X) — hover an area, click to log it", 12, 22);
  ctx.restore();
}

const COLORS = {
  wall: "#39445c",
  corridor: "#4f5d78",
  lobbyFloor: "#6a5a48",
  lobbyWall: "#8a7355",
  doorway: "#c4a574",
  officeFloor: "#3d4a63",
  officeWall: "#9eb6e0",
};

export function drawWorld(ctx, state, layout, player, staffList = []) {
  ctx.clearRect(0, 0, layout.width, layout.height);

  const { building, floor } = layout;
  const tile = floor.tile;

  ctx.fillStyle = getFloorColor(state.hour);
  ctx.fillRect(0, 0, layout.width, layout.height);

  ctx.fillStyle = COLORS.wall;
  ctx.fillRect(building.x, building.y, building.w, building.h);
  ctx.strokeStyle = "#243049";
  ctx.lineWidth = 3;
  ctx.strokeRect(building.x, building.y, building.w, building.h);

  // Circulation first, then the walled spaces that sit inside it.
  for (const area of floor.areas) {
    if (area.kind !== AREA.CORRIDOR) continue;
    ctx.fillStyle = COLORS.corridor;
    ctx.fillRect(area.rect.x, area.rect.y, area.rect.w, area.rect.h);
  }

  for (const area of floor.areas) {
    if (area.kind === AREA.LOBBY) {
      drawWalledArea(ctx, area, tile, COLORS.lobbyWall, COLORS.lobbyFloor);
      ctx.fillStyle = "#dbc5a2";
      ctx.font = "bold 14px Segoe UI";
      ctx.fillText("Lobby", area.rect.x + 16, area.rect.y + 26);
      drawLobbySeating(ctx, area.rect);
    } else if (area.kind === AREA.OFFICE) {
      drawWalledArea(ctx, area, tile, COLORS.officeWall, COLORS.officeFloor);
      drawOfficeFittings(ctx, area);
    } else if (area.kind === AREA.DEPARTMENT) {
      drawDepartment(ctx, area);
    }
  }

  ctx.fillStyle = "rgba(219, 197, 162, 0.3)";
  for (let i = 0; i < CONFIG.maxWaitingGuests; i++) {
    const slot = layout.checkInLineSlot(i);
    ctx.beginPath();
    ctx.arc(slot.x, slot.y, 14, 0, Math.PI * 2);
    ctx.fill();
  }

  const nextLocked = state.rooms.find((r) => !r.unlocked);
  const nextPrice = nextLocked ? getRoomUnlockCost(state) : null;
  for (const planned of floor.rooms) {
    const room = state.rooms[planned.id - 1];
    if (!room) continue;
    const priceLabel =
      nextLocked && nextLocked.id === room.id ? nextPrice : null;
    drawRoom(ctx, room, planned, tile, priceLabel, state);
  }

  drawFrontDesk(ctx, layout.frontDesk);
  drawLobbyRadio(ctx, layout.lobbyRadio);
  drawNewspaper(ctx, layout.newspaper);
  drawVacancySign(ctx, layout.vacancySign, state.vacancyOpen);

  for (let i = 0; i < state.waitingGuests.length; i++) {
    const guest = state.waitingGuests[i];
    const slot = layout.checkInLineSlot(i);
    const tag = i === 0 ? `${guest.name} ★` : guest.name;
    drawCharacter(ctx, slot.x, slot.y, 13, "#e8a0bf", tag);
  }

  for (const guest of state.activeGuests) {
    if (guest.phase === "in_room") continue;
    let label = guest.name;
    let color = "#d4a574";
    if (guest.phase === "walking_to_room") {
      label = `${guest.name} →${guest.roomId}`;
      color = "#7ec8e3";
    } else if (guest.phase === "walking_to_checkout") {
      label = `${guest.name} →desk`;
      color = "#e6b422";
    } else if (guest.phase === "waiting_checkout") {
      const waitLeft = Math.max(0, Math.ceil(guest.waitRemainingHours ?? 0));
      label = guest.upsetCheckout
        ? `${guest.name} upset`
        : `${guest.name} out ${waitLeft}h`;
      color = guest.upsetCheckout ? "#ff8f8f" : "#ffd166";
    }
    drawCharacter(ctx, guest.x, guest.y, 11, color, label);
  }

  drawCharacter(ctx, player.x, player.y, player.radius, "#6ecbff", "You");
  for (const staff of staffList) {
    if (!staff) continue;
    let label = staff.name;
    if (staff.phase === "waiting_pay") label = `${staff.name} pay $${staff.wagesOwed}`;
    else if (staff.phase === "to_desk") label = `${staff.name} →pay`;
    else if (staff.paydayDue) label = `${staff.name} payday`;
    drawCharacter(ctx, staff.x, staff.y, staff.radius, staff.color, label);
  }

  if (player.activeTask) {
    const taskColor = {
      clean: "#7dffb2",
      repair: "#ff9f6b",
      inspect: "#ffd166",
    };
    drawProgressBar(
      ctx,
      player.x - 30,
      player.y - 28,
      60,
      player.activeTask.progress / player.activeTask.duration,
      taskColor[player.activeTask.type] || "#ffd166"
    );
  }

  drawHintLine(ctx, state, layout, staffList);

  const nightAlpha = getNightOverlay(state.hour);
  if (nightAlpha > 0) {
    ctx.fillStyle = `rgba(8, 12, 28, ${nightAlpha})`;
    ctx.beginPath();
    ctx.rect(0, 0, layout.width, layout.height);
    ctx.rect(building.x, building.y, building.w, building.h);
    ctx.fill("evenodd");
  }

  if (state.paused) {
    ctx.fillStyle = "rgba(0,0,0,0.45)";
    ctx.fillRect(0, 0, layout.width, layout.height);
    ctx.fillStyle = "#fff";
    ctx.font = "bold 28px Segoe UI";
    ctx.fillText("PAUSED", layout.width / 2 - 60, layout.height / 2);
  }
}

/**
 * Walls are drawn exactly where the nav grid puts them: a one-tile ring with
 * the door openings cut back out, so what you see is what characters path on.
 */
function drawWalledArea(ctx, area, tile, wallColor, floorColor) {
  const r = area.rect;
  ctx.fillStyle = wallColor;
  ctx.fillRect(r.x, r.y, r.w, r.h);
  ctx.fillStyle = floorColor;
  ctx.fillRect(r.x + tile, r.y + tile, r.w - tile * 2, r.h - tile * 2);
  for (const door of area.doors ?? []) {
    drawDoorGap(ctx, area, door, tile);
  }
}

function drawDoorGap(ctx, area, door, tile) {
  const r = area.rect;
  ctx.fillStyle = COLORS.doorway;
  if (door.side === "north" || door.side === "south") {
    const x = door.center.x - door.width / 2;
    const y = door.side === "north" ? r.y : r.y + r.h - tile;
    ctx.fillRect(x, y, door.width, tile);
    return;
  }
  const y = door.center.y - door.width / 2;
  const x = door.side === "west" ? r.x : r.x + r.w - tile;
  ctx.fillRect(x, y, tile, door.width);
}

function drawOfficeFittings(ctx, area) {
  const center = {
    x: area.rect.x + area.rect.w / 2,
    y: area.rect.y + area.rect.h / 2,
  };
  ctx.fillStyle = "#1a2030";
  ctx.fillRect(center.x - 24, center.y - 20, 48, 32);
  ctx.fillStyle = "#7dffb2";
  ctx.fillRect(center.x - 20, center.y - 16, 40, 24);
  ctx.fillStyle = "#dbc5a2";
  ctx.font = "12px Segoe UI";
  ctx.fillText("Office", area.rect.x + 16, area.rect.y + 26);
  ctx.font = "10px Segoe UI";
  ctx.fillText("PC", center.x - 7, center.y + 26);
}

function drawDepartment(ctx, area) {
  const r = area.rect;
  ctx.fillStyle = "#4a3f52";
  ctx.fillRect(r.x, r.y, r.w, r.h);
  ctx.strokeStyle = area.accent ?? "#9aa8c0";
  ctx.lineWidth = 2;
  ctx.strokeRect(r.x, r.y, r.w, r.h);
  ctx.fillStyle = area.accent ?? "#e8edf5";
  ctx.font = "bold 11px Segoe UI";
  ctx.fillText(area.label, r.x + 10, r.y + 20);
}

function drawLobbySeating(ctx, lobby) {
  const sitX = lobby.x + lobby.w / 2 + 20;
  const sitY = lobby.y + lobby.h * 0.62;
  ctx.fillStyle = "#4a2f2a";
  ctx.fillRect(sitX - 130, sitY - 70, 260, 140);
  ctx.fillStyle = "#5c4a3a";
  ctx.fillRect(sitX - 80, sitY + 36, 160, 32);
  ctx.fillRect(sitX - 80, sitY - 64, 160, 32);
  ctx.fillStyle = "#3a2a20";
  ctx.fillRect(sitX - 36, sitY - 18, 72, 36);
}

function drawFrontDesk(ctx, desk) {
  const x = desk.x - desk.w / 2;
  const y = desk.y - desk.h / 2;
  ctx.fillStyle = "#5a4030";
  ctx.fillRect(x, y, desk.w, desk.h);
  ctx.strokeStyle = "#3a2818";
  ctx.lineWidth = 2;
  ctx.strokeRect(x, y, desk.w, desk.h);
  ctx.fillStyle = "#dbc5a2";
  ctx.font = "13px Segoe UI";
  ctx.fillText("Front desk", desk.x - 34, y - 8);
}

function drawLobbyRadio(ctx, radio) {
  if (!radio) return;
  ctx.fillStyle = "#2a2f3a";
  ctx.fillRect(radio.x - radio.w / 2, radio.y - radio.h / 2, radio.w, radio.h);
  ctx.strokeStyle = "#8d9bb5";
  ctx.lineWidth = 1;
  ctx.strokeRect(radio.x - radio.w / 2, radio.y - radio.h / 2, radio.w, radio.h);
  ctx.fillStyle = "#c96545";
  ctx.fillRect(radio.x - 10, radio.y - 4, 8, 5);
  ctx.fillStyle = "#dbc5a2";
  ctx.font = "9px Segoe UI";
  ctx.fillText("Radio", radio.x - 14, radio.y + radio.h / 2 + 10);
}

function drawNewspaper(ctx, paper) {
  if (!paper) return;
  ctx.fillStyle = "#d8c9a3";
  ctx.fillRect(paper.x - paper.w / 2, paper.y - paper.h / 2, paper.w, paper.h);
  ctx.strokeStyle = "#6a5a40";
  ctx.strokeRect(paper.x - paper.w / 2, paper.y - paper.h / 2, paper.w, paper.h);
  ctx.fillStyle = "#3a2818";
  ctx.font = "8px Segoe UI";
  ctx.fillText("Paper", paper.x - 12, paper.y + 3);
}

function drawVacancySign(ctx, sign, open) {
  const x = sign.x - sign.w / 2;
  const y = sign.y - sign.h / 2;
  ctx.fillStyle = open ? "#2f6b3a" : "#7a2e2e";
  ctx.fillRect(x, y, sign.w, sign.h);
  ctx.strokeStyle = "#101520";
  ctx.lineWidth = 2;
  ctx.strokeRect(x, y, sign.w, sign.h);
  ctx.fillStyle = "#fff";
  ctx.font = "bold 14px Segoe UI";
  const label = open ? "VACANCY" : "NO VACANCY";
  const labelW = ctx.measureText(label).width;
  ctx.fillText(label, sign.x - labelW / 2, sign.y + 5);
}

function drawHintLine(ctx, state, layout, staffList) {
  const checkoutWaiting = state.activeGuests.some(
    (g) => g.phase === "waiting_checkout"
  );
  const staffPayday = staffList.find(
    (s) => s && (s.phase === "waiting_pay" || s.phase === "to_desk")
  );

  ctx.font = "12px Segoe UI";
  if (checkoutWaiting) {
    ctx.fillStyle = "#ffd166";
    ctx.fillText("Press E to check out guests", 16, layout.height - 12);
  } else if (staffPayday) {
    ctx.fillStyle = "#ffd166";
    ctx.fillText(
      `Press E to pay ${staffPayday.name} $${staffPayday.wagesOwed}`,
      16,
      layout.height - 12
    );
  } else if (state.waitingGuests.length > 0) {
    ctx.fillStyle = "#ffd166";
    ctx.fillText("Press E to review the arrival at the desk", 16, layout.height - 12);
  } else {
    ctx.fillStyle = "#9aa8c0";
    ctx.font = "11px Segoe UI";
    ctx.fillText("E desk · E radio / paper · E office PC · V sign · X inspect · R barricade", 16, layout.height - 12);
  }
}

function drawRoom(ctx, room, planned, tile, unlockCost = null, state = null) {
  const r = planned.rect;
  const inner = {
    x: r.x + tile,
    y: r.y + tile,
    w: r.w - tile * 2,
    h: r.h - tile * 2,
  };

  ctx.fillStyle = COLORS.wall;
  ctx.fillRect(r.x, r.y, r.w, r.h);

  if (!room.unlocked) {
    ctx.fillStyle = "#2a3142";
    ctx.fillRect(inner.x, inner.y, inner.w, inner.h);
    ctx.fillStyle = "#6a738a";
    ctx.font = "11px Segoe UI";
    ctx.fillText(`Room ${room.id}`, inner.x + 6, inner.y + 22);
    ctx.fillText("LOCKED", inner.x + 6, inner.y + 38);
    if (unlockCost != null) {
      ctx.fillStyle = "#ffd166";
      ctx.fillText(`$${unlockCost}`, inner.x + 6, inner.y + 54);
    }
    return;
  }

  const colors = {
    clean: "#5cb85c",
    dirty: dirtColor(room.dirtLevel),
    occupied: "#5b8def",
    needs_inspection: "#e6b422",
    needs_repair: "#c45c2a",
  };
  ctx.fillStyle = colors[room.status] || "#888";
  ctx.fillRect(inner.x, inner.y, inner.w, inner.h);

  drawDoorGap(ctx, { rect: r }, planned.doorOpening, tile);

  ctx.fillStyle = "#101520";
  ctx.font = "bold 12px Segoe UI";
  ctx.fillText(`Room ${room.id}`, inner.x + 6, inner.y + 18);

  ctx.font = "9px Segoe UI";
  ctx.fillText(roomStatusLabel(room, state), inner.x + 6, inner.y + 34);

  if (room.status === "occupied") {
    ctx.fillStyle = "#ff8f8f";
    ctx.font = "8px Segoe UI";
    ctx.fillText("DO NOT DISTURB", inner.x + 6, inner.y + 48);
  }

  if (room.worker) {
    ctx.fillStyle = "#fff";
    ctx.font = "9px Segoe UI";
    ctx.fillText(`Working: ${room.worker}`, inner.x + 6, inner.y + inner.h - 14);
  }

  const progress =
    room.status === "dirty"
      ? { value: room.cleanProgress, color: "#aaf0c8" }
      : room.status === "needs_inspection"
        ? { value: room.inspectProgress, color: "#ffe08a" }
        : room.status === "needs_repair"
          ? { value: room.repairProgress, color: "#ffb080" }
          : null;
  if (progress && progress.value > 0) {
    drawProgressBar(
      ctx,
      inner.x + 4,
      inner.y + inner.h - 8,
      inner.w - 8,
      progress.value,
      progress.color
    );
  }
}

function dirtColor(level) {
  if (level === "light") return "#d4925a";
  if (level === "heavy") return "#8f3b2a";
  return "#c96545";
}

function roomStatusLabel(room, state = null) {
  if (room.status === "clean") return "Vacant";
  if (room.status === "needs_inspection") return "Inspect";
  if (room.status === "needs_repair") {
    const hrs = CONFIG.repairHours[room.repairLevel];
    const cost =
      room.repairCost || (state ? getRepairCost(state, room.repairLevel) : null);
    const costLabel = cost != null ? ` $${cost}` : "";
    return `Fix ${room.repairLevel} (${hrs}h${costLabel})`;
  }
  if (room.status === "dirty") {
    const dirt = `Dirt ${room.dirtLevel} (${CONFIG.dirtHours[room.dirtLevel]}h)`;
    return room.damageFound && room.repairLevel ? `${dirt} →fix` : dirt;
  }
  if (room.status === "occupied") {
    const hoursLeft = Math.max(0, room.stayRemainingHours || 0);
    const daysLeft = Math.max(0, Math.ceil(hoursLeft / CONFIG.stayIntervalHours));
    return room.guestName ? `${room.guestName} ${daysLeft}d` : "Occupied";
  }
  return room.status;
}

function drawCharacter(ctx, x, y, radius, color, label) {
  ctx.beginPath();
  ctx.fillStyle = color;
  ctx.arc(x, y, radius, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = "#101520";
  ctx.beginPath();
  ctx.arc(x - 4, y - 2, 2, 0, Math.PI * 2);
  ctx.arc(x + 4, y - 2, 2, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = "#e8edf5";
  ctx.font = "11px Segoe UI";
  ctx.fillText(label, x - 18, y - radius - 6);
}

function drawProgressBar(ctx, x, y, width, progress, color) {
  ctx.fillStyle = "#1a2030";
  ctx.fillRect(x, y, width, 6);
  ctx.fillStyle = color;
  ctx.fillRect(x, y, width * Math.min(1, Math.max(0, progress)), 6);
}

function getFloorColor(hour) {
  const h = ((hour % 24) + 24) % 24;
  if (h >= 5 && h < 8) return "#3a4258";
  if (h >= 8 && h < 17) return "#2f384c";
  if (h >= 17 && h < 21) return "#33334f";
  return "#1b2133";
}

function getNightOverlay(hour) {
  const h = ((hour % 24) + 24) % 24;
  if (h >= 21 || h < 5) return 0.35;
  if (h >= 19) return 0.18;
  return 0;
}

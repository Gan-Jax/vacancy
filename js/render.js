import { getRepairCost, getRoomUnlockCost } from "./economy.js";
import { CONFIG } from "./config.js";

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

/** Build labeled hit targets for the layout inspector (press X). */
export function getInspectTargets(layout, state) {
  const targets = [];

  const pushRect = (label, x, y, w, h, extra = "") => {
    targets.push({
      label,
      x,
      y,
      w,
      h,
      cx: Math.round(x + w / 2),
      cy: Math.round(y + h / 2),
      extra,
    });
  };

  const b = layout.building;
  pushRect("Building", b.x, b.y, b.w, b.h);

  const lobby = layout.lobby;
  pushRect("Lobby", lobby.x, lobby.y, lobby.w, lobby.h);

  const office = layout.office;
  pushRect(
    "Office",
    office.x - office.w / 2,
    office.y - office.h / 2,
    office.w,
    office.h,
    `door (${Math.round(office.door.x)}, ${Math.round(office.door.y)})`
  );

  const desk = layout.frontDesk;
  pushRect(
    "Front desk",
    desk.x - desk.w / 2,
    desk.y - desk.h / 2,
    desk.w,
    desk.h
  );

  if (layout.lobbyRadio) {
    const r = layout.lobbyRadio;
    pushRect("Lobby radio", r.x - r.w / 2, r.y - r.h / 2, r.w, r.h);
  }
  if (layout.newspaper) {
    const n = layout.newspaper;
    pushRect("Newspaper stack", n.x - n.w / 2, n.y - n.h / 2, n.w, n.h);
  }

  const sign = layout.vacancySign;
  pushRect(
    "Vacancy sign",
    sign.x - sign.w / 2,
    sign.y - sign.h / 2,
    sign.w,
    sign.h
  );

  const bob = layout.handymanCloset;
  pushRect("Bob's Closet", bob.x - bob.w / 2, bob.y - bob.h / 2, bob.w, bob.h);

  const mary = layout.maidRoom;
  pushRect("Mary's Maid Room", mary.x - mary.w / 2, mary.y - mary.h / 2, mary.w, mary.h);

  for (let i = 0; i < layout.roomCenters.length; i++) {
    const c = layout.roomCenters[i];
    const room = state.rooms[i];
    pushRect(
      `Room ${i + 1}`,
      c.x - CONFIG.roomWidth / 2,
      c.y - CONFIG.roomHeight / 2,
      CONFIG.roomWidth,
      CONFIG.roomHeight,
      room ? `status: ${room.status}` : ""
    );
  }

  const wp = layout.waypoints;
  const named = [
    ["Lobby north-right door", wp.lobbyNorthDoor],
    ["Lobby north-left door", wp.lobbyNorthLeftDoor],
    ["Lobby west door", wp.lobbyWestDoor],
    ["Lobby south-right door", wp.lobbySouthDoor],
    ["Lobby south-left door", wp.lobbySouthLeftDoor],
    ["Desk hall waypoint", wp.deskHallIdx],
    ["Office door waypoint", wp.officeDoorIdx],
    ["Bob home waypoint", wp.bobHomeIdx],
    ["Mary home waypoint", wp.maryHomeIdx],
  ];
  for (const [label, idx] of named) {
    if (idx == null || !wp.points[idx]) continue;
    const p = wp.points[idx];
    pushRect(label, p.x - 12, p.y - 12, 24, 24, `wp (${Math.round(p.x)}, ${Math.round(p.y)})`);
  }

  for (let i = 0; i < wp.doorIdx.length; i++) {
    const p = wp.points[wp.doorIdx[i]];
    if (!p) continue;
    pushRect(
      `Room ${i + 1} door`,
      p.x - 12,
      p.y - 12,
      24,
      24,
      `wp (${Math.round(p.x)}, ${Math.round(p.y)})`
    );
  }

  // Smaller targets first so hover prefers doors/waypoints over big rooms
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

  // Light outlines for all targets
  for (const t of targets) {
    ctx.strokeStyle = "rgba(255, 209, 102, 0.25)";
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
    const boxW = 260;
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
  ctx.fillText("INSPECT (X) — hover an area", 12, 22);
  ctx.restore();
}

/**
 * Layout: upper/lower room wings, center lobby, office as its own room.
 * Everything is padded inside the building so rooms don't clip walls.
 */
export function createLayout(canvas) {
  const width = canvas.width;
  const height = canvas.height;

  const building = {
    x: 48,
    y: 40,
    w: width - 96,
    h: height - 108,
  };

  const cols = 4;
  const hallGap = 56;
  const lobbyHeight = 148;
  const inset = 28;

  // Staff closets sit on the bottom corners — rooms can use the full width.
  const contentLeft = building.x + inset + 36;
  const contentRight = building.x + building.w - inset - 36;
  const gapX = (contentRight - contentLeft) / (cols - 1);

  const roomH = CONFIG.roomHeight;
  const gapY = roomH + hallGap;

  // Vertical stack inside building: upper 2 rows → hall → lobby → hall → lower row
  const upperStartY = building.y + inset + roomH / 2;
  const upperBottom = upperStartY + gapY + roomH / 2;
  const lobbyTop = upperBottom + hallGap;
  const lobby = {
    x: building.x + inset,
    y: lobbyTop,
    w: building.w - inset * 2,
    h: lobbyHeight,
  };
  const lowerCenterY = lobby.y + lobby.h + hallGap + roomH / 2;

  const roomCenters = [];
  for (let i = 0; i < CONFIG.maxRooms; i++) {
    const col = i % cols;
    const row = Math.floor(i / cols);
    const x = contentLeft + col * gapX;
    if (row < 2) {
      roomCenters.push({ x, y: upperStartY + row * gapY });
    } else {
      roomCenters.push({ x, y: lowerCenterY });
    }
  }

  // Bottom corners (inspect picks): Mary left, Bob right
  const handymanCloset = {
    x: 1251,
    y: 708,
    w: 84,
    h: 56,
  };

  const maidRoom = {
    x: 96,
    y: 717,
    w: 84,
    h: 56,
  };

  // Office is a real room on the left of the lobby (enter via east door only)
  const office = {
    x: lobby.x + 78,
    y: lobby.y + lobby.h / 2,
    w: 108,
    h: lobby.h - 28,
  };
  office.door = {
    x: office.x + office.w / 2 + 2,
    y: office.y,
  };

  const frontDesk = {
    x: lobby.x + lobby.w / 2 + 20,
    y: lobby.y + lobby.h / 2,
    w: 120,
    h: 52,
  };

  const waypoints = buildHallwayWaypoints(
    roomCenters,
    handymanCloset,
    maidRoom,
    frontDesk,
    lobby,
    office
  );

  return {
    width,
    height,
    roomCenters,
    cols,
    building,
    lobby,
    office,
    frontDesk,
    spawn: { x: frontDesk.x, y: frontDesk.y + 50 },
    vacancySign: { x: width / 2, y: height - 28, w: 140, h: 36 },
    lobbyRadio: {
      x: frontDesk.x - 110,
      y: frontDesk.y - 8,
      w: 36,
      h: 28,
    },
    newspaper: {
      x: frontDesk.x - 38,
      y: frontDesk.y + 4,
      w: 28,
      h: 18,
    },
    handymanCloset,
    maidRoom,
    waypoints,
    checkInLineSlot(index) {
      return {
        x: frontDesk.x + 85 + index * 40,
        y: frontDesk.y + 34,
      };
    },
    checkoutLineSlot(index) {
      return {
        x: frontDesk.x - 55,
        y: frontDesk.y + 52 + index * 28,
      };
    },
    staffPaySlot(staffId) {
      const offset = staffId === "mary" ? 48 : 18;
      return {
        x: frontDesk.x - 24 + offset,
        y: frontDesk.y - 26,
      };
    },
  };
}

function buildHallwayWaypoints(
  roomCenters,
  bobCloset,
  maidRoom,
  frontDesk,
  lobby,
  office
) {
  const points = [];
  const add = (id, x, y) => {
    points.push({ id, x, y, links: [] });
    return points.length - 1;
  };

  const cols = 4;
  const rows = Math.ceil(roomCenters.length / cols);
  const doorClearance = CONFIG.roomHeight / 2 + 22;
  const lobbyIn = 28; // step inside lobby before turning — avoids sliding on perimeter walls

  const doorIdx = [];
  const hallRowY = [];
  for (let row = 0; row < rows; row++) {
    // Upper rooms open south into the hall; lower rooms open north toward lobby
    if (row >= 2) {
      hallRowY[row] = roomCenters[row * cols].y - doorClearance;
    } else {
      hallRowY[row] = roomCenters[row * cols].y + doorClearance;
    }
  }

  for (let i = 0; i < roomCenters.length; i++) {
    const c = roomCenters[i];
    const row = Math.floor(i / cols);
    doorIdx.push(add(`door-${i + 1}`, c.x, hallRowY[row]));
  }

  // Hall rows: walk door-to-door along the corridor (direct route to assigned room)
  for (let row = 0; row < rows; row++) {
    for (let col = 0; col < cols - 1; col++) {
      link(points, doorIdx[row * cols + col], doorIdx[row * cols + col + 1]);
    }
  }

  // Spines in column gaps only — never outside the building or through room solids
  const leftDoorX = lobby.x + 240;
  const leftX = leftDoorX;
  const rightX = (roomCenters[cols - 2].x + roomCenters[cols - 1].x) / 2;
  const leftSpine = [];
  const rightSpine = [];
  for (let row = 0; row < rows; row++) {
    leftSpine.push(add(`left-${row}`, leftX, hallRowY[row]));
    rightSpine.push(add(`right-${row}`, rightX, hallRowY[row]));
    // Link both neighboring columns to the gap spine
    link(points, doorIdx[row * cols], leftSpine[row]);
    link(points, doorIdx[row * cols + 1], leftSpine[row]);
    link(points, doorIdx[row * cols + (cols - 2)], rightSpine[row]);
    link(points, doorIdx[row * cols + (cols - 1)], rightSpine[row]);
  }
  // Upper wing vertical only (row0↔row1). Never bridge across the lobby body.
  link(points, leftSpine[0], leftSpine[1]);
  link(points, rightSpine[0], rightSpine[1]);

  const last = rows - 1;

  // Lobby perimeter doors — hall traffic only touches these, never mid-wall
  const lobbyNorthDoor = add("lobby-n", rightX, lobby.y);
  const lobbyNorthLeftDoor = add("lobby-n-left", leftDoorX, lobby.y);
  const lobbyWestDoor = add(
    "lobby-w",
    lobby.x + lobby.w - 6,
    lobby.y + lobby.h / 2
  );
  const lobbySouthDoor = add("lobby-s", rightX, lobby.y + lobby.h);
  const lobbySouthLeftDoor = add("lobby-s-left", leftDoorX, lobby.y + lobby.h);

  // Outside: hall spine → door on the perimeter
  link(points, rightSpine[1], lobbyNorthDoor);
  link(points, leftSpine[1], lobbyNorthLeftDoor);
  link(points, rightSpine[last], lobbySouthDoor);
  link(points, leftSpine[last], lobbySouthLeftDoor);

  // Inside pads: must step into the lobby before turning toward the desk.
  // Direct door→desk edges let axis-first steering slide along the north wall (~675,365).
  const northInR = add("lobby-n-in", rightX, lobby.y + lobbyIn);
  const northInL = add("lobby-n-left-in", leftDoorX, lobby.y + lobbyIn);
  const southInR = add("lobby-s-in", rightX, lobby.y + lobby.h - lobbyIn);
  const southInL = add("lobby-s-left-in", leftDoorX, lobby.y + lobby.h - lobbyIn);
  const westIn = add(
    "lobby-w-in",
    lobby.x + lobby.w - 6 - lobbyIn,
    lobby.y + lobby.h / 2
  );

  link(points, lobbyNorthDoor, northInR);
  link(points, lobbyNorthLeftDoor, northInL);
  link(points, lobbySouthDoor, southInR);
  link(points, lobbySouthLeftDoor, southInL);
  link(points, lobbyWestDoor, westIn);
  // West door is inside-only. An outside "east-hall" spur used to sit in the
  // few-pixel gap between lobby and building wall (~x 1302) and trapped staff.

  const deskHallIdx = add("desk-hall", frontDesk.x, frontDesk.y + 36);
  // Desk only connects to INSIDE pads — never to perimeter door nodes
  link(points, northInR, deskHallIdx);
  link(points, northInL, deskHallIdx);
  link(points, southInR, deskHallIdx);
  link(points, southInL, deskHallIdx);
  link(points, westIn, deskHallIdx);
  // Inside circulation (all fully inside the lobby band)
  link(points, northInL, northInR);
  link(points, southInL, southInR);
  link(points, northInR, westIn);
  link(points, southInR, westIn);
  link(points, northInL, southInL);

  // Staff homes at bottom corners — column-gap drop south of lower rooms, then
  // along the south hall to a stand point just inside the building (not in the wall).
  const belowLower =
    roomCenters[last * cols].y + CONFIG.roomHeight / 2 + 22;
  const rightBelow = add("right-below", rightX, belowLower);
  const leftBelow = add("left-below", leftX, belowLower);
  link(points, rightSpine[last], rightBelow);
  link(points, leftSpine[last], leftBelow);
  // Also reach south hall from the south lobby doors (short L, no wall gap)
  link(points, lobbySouthDoor, rightBelow);
  link(points, lobbySouthLeftDoor, leftBelow);

  const bobStandX = Math.min(bobCloset.x - 36, roomCenters[cols - 1].x + 40);
  const bobHomeIdx = add("bob-home", bobStandX, belowLower);
  link(points, rightBelow, bobHomeIdx);

  const maryStandX = Math.max(maidRoom.x + 36, leftX - 80);
  const maryHomeIdx = add("mary-home", maryStandX, belowLower);
  link(points, leftBelow, maryHomeIdx);

  // Office door is only off the desk spur — not on the room↔lobby through-route
  const officeDoorIdx = add("office-door", office.door.x + 16, office.door.y);
  link(points, deskHallIdx, officeDoorIdx);

  return {
    points,
    doorIdx,
    bobHomeIdx,
    maryHomeIdx,
    deskHallIdx,
    officeDoorIdx,
    lobbyNorthDoor,
    lobbyNorthLeftDoor,
    lobbyWestDoor,
    lobbySouthDoor,
    lobbySouthLeftDoor,
    hallRowY,
  };
}

function link(points, a, b) {
  if (a == null || b == null) return;
  if (!points[a].links.includes(b)) points[a].links.push(b);
  if (!points[b].links.includes(a)) points[b].links.push(a);
}

export function drawWorld(ctx, state, layout, player, staffList = []) {
  ctx.clearRect(0, 0, layout.width, layout.height);

  const building = layout.building;
  const lobby = layout.lobby;
  ctx.fillStyle = getFloorColor(state.hour);
  ctx.fillRect(0, 0, layout.width, layout.height);

  ctx.fillStyle = "#3a455c";
  ctx.fillRect(building.x, building.y, building.w, building.h);
  ctx.strokeStyle = "#243049";
  ctx.lineWidth = 3;
  ctx.strokeRect(building.x, building.y, building.w, building.h);

  // Upper + lower hall carpets (everything except lobby band)
  ctx.fillStyle = "#4f5d78";
  const hallPad = 10;
  ctx.fillRect(
    building.x + hallPad,
    building.y + hallPad,
    building.w - hallPad * 2,
    Math.max(8, lobby.y - building.y - hallPad)
  );
  const lowerHallY = lobby.y + lobby.h;
  ctx.fillRect(
    building.x + hallPad,
    lowerHallY,
    building.w - hallPad * 2,
    Math.max(8, building.y + building.h - lowerHallY - hallPad)
  );

  // Center lobby
  ctx.fillStyle = "#6a5a48";
  ctx.fillRect(lobby.x, lobby.y, lobby.w, lobby.h);
  ctx.strokeStyle = "#8a7355";
  ctx.lineWidth = 1;
  ctx.strokeRect(lobby.x, lobby.y, lobby.w, lobby.h);
  ctx.fillStyle = "#dbc5a2";
  ctx.font = "bold 14px Segoe UI";
  ctx.fillText("Lobby", lobby.x + lobby.w / 2 - 20, lobby.y + 20);

  ctx.fillStyle = "rgba(219, 197, 162, 0.35)";
  for (let i = 0; i < CONFIG.maxWaitingGuests; i++) {
    const slot = layout.checkInLineSlot(i);
    ctx.beginPath();
    ctx.arc(slot.x, slot.y, 14, 0, Math.PI * 2);
    ctx.fill();
  }

  drawHandymanCloset(ctx, layout.handymanCloset);
  drawMaidRoom(ctx, layout.maidRoom);

  // Office room (solid) + east door into lobby
  const office = layout.office;
  const ox = office.x - office.w / 2;
  const oy = office.y - office.h / 2;
  ctx.fillStyle = "#3d4a63";
  ctx.fillRect(ox, oy, office.w, office.h);
  ctx.strokeStyle = "#9eb6e0";
  ctx.lineWidth = 2;
  ctx.strokeRect(ox, oy, office.w, office.h);
  // Door opening on the east wall
  ctx.fillStyle = "#6a5a48";
  ctx.fillRect(ox + office.w - 3, office.y - 16, 6, 32);
  ctx.fillStyle = "#c4a574";
  ctx.fillRect(ox + office.w - 5, office.y - 14, 4, 28);
  ctx.fillStyle = "#1a2030";
  ctx.fillRect(office.x - 22, office.y - 22, 44, 30);
  ctx.fillStyle = "#7dffb2";
  ctx.fillRect(office.x - 18, office.y - 18, 36, 22);
  ctx.fillStyle = "#dbc5a2";
  ctx.font = "12px Segoe UI";
  ctx.fillText("Office", office.x - 20, oy + 16);
  ctx.fillText("PC", office.x - 10, office.y + 28);

  // Lobby doors: north/south left+right, west-facing entry from right hall
  drawLobbyDoorway(ctx, layout.waypoints.points[layout.waypoints.lobbyNorthDoor], "n");
  drawLobbyDoorway(ctx, layout.waypoints.points[layout.waypoints.lobbyNorthLeftDoor], "n");
  drawLobbyDoorway(ctx, layout.waypoints.points[layout.waypoints.lobbyWestDoor], "w");
  drawLobbyDoorway(ctx, layout.waypoints.points[layout.waypoints.lobbySouthDoor], "s");
  drawLobbyDoorway(ctx, layout.waypoints.points[layout.waypoints.lobbySouthLeftDoor], "s");

  // Front desk — center of lobby
  const desk = layout.frontDesk;
  ctx.fillStyle = "#5a4030";
  ctx.fillRect(desk.x - desk.w / 2, desk.y - desk.h / 2, desk.w, desk.h);
  ctx.strokeStyle = "#3a2818";
  ctx.lineWidth = 2;
  ctx.strokeRect(desk.x - desk.w / 2, desk.y - desk.h / 2, desk.w, desk.h);
  ctx.fillStyle = "#dbc5a2";
  ctx.font = "13px Segoe UI";
  ctx.fillText("Front desk", desk.x - 34, desk.y - desk.h / 2 - 8);

  const radio = layout.lobbyRadio;
  if (radio) {
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

  const paper = layout.newspaper;
  if (paper) {
    ctx.fillStyle = "#d8c9a3";
    ctx.fillRect(paper.x - paper.w / 2, paper.y - paper.h / 2, paper.w, paper.h);
    ctx.strokeStyle = "#6a5a40";
    ctx.strokeRect(paper.x - paper.w / 2, paper.y - paper.h / 2, paper.w, paper.h);
    ctx.fillStyle = "#3a2818";
    ctx.font = "8px Segoe UI";
    ctx.fillText("Paper", paper.x - 12, paper.y + 3);
  }

  // Vacancy sign outside
  const sign = layout.vacancySign;
  const signOpen = state.vacancyOpen;
  const signX = sign.x - sign.w / 2;
  const signY = sign.y - sign.h / 2;
  ctx.fillStyle = signOpen ? "#2f6b3a" : "#7a2e2e";
  ctx.fillRect(signX, signY, sign.w, sign.h);
  ctx.strokeStyle = "#101520";
  ctx.lineWidth = 2;
  ctx.strokeRect(signX, signY, sign.w, sign.h);
  ctx.fillStyle = "#fff";
  ctx.font = "bold 14px Segoe UI";
  const signLabel = signOpen ? "VACANCY" : "NO VACANCY";
  const labelW = ctx.measureText(signLabel).width;
  ctx.fillText(signLabel, sign.x - labelW / 2, sign.y + 5);

  for (let i = 0; i < state.waitingGuests.length; i++) {
    const guest = state.waitingGuests[i];
    const slot = layout.checkInLineSlot(i);
    const waitLeft = Math.max(0, Math.ceil(guest.waitRemainingHours ?? 0));
    const tag = i === 0 ? `${guest.name} ★${waitLeft}h` : `${guest.name} ${waitLeft}h`;
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

  const checkoutWaiting = state.activeGuests.some(
    (g) => g.phase === "waiting_checkout"
  );
  const staffPayday = staffList.find(
    (s) => s && (s.phase === "waiting_pay" || s.phase === "to_desk")
  );
  if (checkoutWaiting) {
    ctx.fillStyle = "#ffd166";
    ctx.font = "12px Segoe UI";
    ctx.fillText("Press E to check out guests", 16, layout.height - 12);
  } else if (staffPayday) {
    ctx.fillStyle = "#ffd166";
    ctx.font = "12px Segoe UI";
    ctx.fillText(
      `Press E to pay ${staffPayday.name} $${staffPayday.wagesOwed}`,
      16,
      layout.height - 12
    );
  } else if (state.waitingGuests.length > 0) {
    ctx.fillStyle = "#ffd166";
    ctx.font = "12px Segoe UI";
    ctx.fillText("Press E to check in (they leave after 4h)", 16, layout.height - 12);
  } else {
    ctx.fillStyle = "#9aa8c0";
    ctx.font = "11px Segoe UI";
    ctx.fillText("E desk · E office PC · V sign", 16, layout.height - 12);
  }

  const nextLocked = state.rooms.find((r) => !r.unlocked);
  const nextPrice = nextLocked ? getRoomUnlockCost(state) : null;

  for (let i = 0; i < state.rooms.length; i++) {
    const room = state.rooms[i];
    const center = layout.roomCenters[i];
    const priceLabel =
      nextLocked && nextLocked.id === room.id ? nextPrice : null;
    drawRoom(ctx, room, center, priceLabel, state);
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

function drawRoom(ctx, room, center, unlockCost = null, state = null) {
  const w = CONFIG.roomWidth;
  const h = CONFIG.roomHeight;
  const x = center.x - w / 2;
  const y = center.y - h / 2;

  if (!room.unlocked) {
    ctx.fillStyle = "#2a3142";
    ctx.fillRect(x, y, w, h);
    ctx.strokeStyle = "#1f2635";
    ctx.strokeRect(x, y, w, h);
    ctx.fillStyle = "#6a738a";
    ctx.font = "11px Segoe UI";
    ctx.fillText(`Room ${room.id}`, x + 28, center.y - 4);
    ctx.fillText("LOCKED", x + 30, center.y + 12);
    if (unlockCost != null) {
      ctx.fillStyle = "#ffd166";
      ctx.fillText(`$${unlockCost}`, x + 34, center.y + 28);
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
  ctx.fillRect(x, y, w, h);
  ctx.strokeStyle = room.status === "occupied" ? "#1a2030" : "#c8d6ea";
  ctx.lineWidth = room.status === "occupied" ? 3 : 2;
  ctx.strokeRect(x, y, w, h);

  // Door on the hall-facing side (south for upper wing, north for lower wing)
  const doorSouth = room.id <= 8;
  drawRoomDoor(ctx, x, y, w, h, doorSouth);

  if (room.status === "occupied") {
    ctx.fillStyle = "#ff8f8f";
    ctx.font = "9px Segoe UI";
    ctx.fillText("DO NOT DISTURB", x + 10, doorSouth ? y + 50 : y + h - 14);
  }

  ctx.fillStyle = "#101520";
  ctx.font = "bold 12px Segoe UI";
  ctx.fillText(`Room ${room.id}`, x + 26, y + 18);

  ctx.font = "10px Segoe UI";
  ctx.fillText(roomStatusLabel(room, state), x + 8, y + 36);

  if (room.worker) {
    ctx.fillStyle = "#fff";
    ctx.fillText(`Working: ${room.worker}`, x + 8, y + 50);
  }

  if (room.status === "dirty" && room.cleanProgress > 0) {
    drawProgressBar(ctx, x + 8, y + h - 10, w - 16, room.cleanProgress, "#aaf0c8");
  }
  if (room.status === "needs_inspection" && room.inspectProgress > 0) {
    drawProgressBar(ctx, x + 8, y + h - 10, w - 16, room.inspectProgress, "#ffe08a");
  }
  if (room.status === "needs_repair" && room.repairProgress > 0) {
    drawProgressBar(ctx, x + 8, y + h - 10, w - 16, room.repairProgress, "#ffb080");
  }
}

function dirtColor(level) {
  if (level === "light") return "#d4925a";
  if (level === "heavy") return "#8f3b2a";
  return "#c96545";
}

function roomStatusLabel(room, state = null) {
  if (room.status === "clean") return "Vacant";
  if (room.status === "needs_inspection") return "Inspect checkout";
  if (room.status === "needs_repair") {
    const hrs = CONFIG.repairHours[room.repairLevel];
    const cost =
      room.repairCost ||
      (state ? getRepairCost(state, room.repairLevel) : null);
    const costLabel = cost != null ? `, $${cost}` : "";
    return `Repair: ${room.repairLevel} (${hrs}h${costLabel})`;
  }
  if (room.status === "dirty") {
    const dirt = `Dirt: ${room.dirtLevel} (${CONFIG.dirtHours[room.dirtLevel]}h)`;
    if (room.damageFound && room.repairLevel) {
      return `${dirt} → repair`;
    }
    return dirt;
  }
  if (room.status === "occupied") {
    const hoursLeft = Math.max(0, room.stayRemainingHours || 0);
    const daysLeft = Math.max(
      0,
      Math.ceil(hoursLeft / CONFIG.stayIntervalHours)
    );
    return room.guestName
      ? `${room.guestName} (${daysLeft}d/${Math.ceil(hoursLeft)}h)`
      : "Occupied";
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

function drawRoomDoor(ctx, x, y, w, h, facesSouth) {
  const doorW = 28;
  const doorH = 8;
  const dx = x + w / 2 - doorW / 2;
  const dy = facesSouth ? y + h - doorH : y;
  // Cut a doorway into the wall color
  ctx.fillStyle = "#4f5d78";
  ctx.fillRect(dx, dy, doorW, doorH);
  ctx.fillStyle = "#c4a574";
  ctx.fillRect(dx + 2, dy + 1, doorW - 4, doorH - 2);
  ctx.strokeStyle = "#3a2818";
  ctx.lineWidth = 1;
  ctx.strokeRect(dx + 2, dy + 1, doorW - 4, doorH - 2);
}

function drawLobbyDoorway(ctx, point, facing) {
  if (!point) return;
  if (facing === "w") {
    const w = 10;
    const h = 36;
    const x = point.x - w + 2;
    const y = point.y - h / 2;
    ctx.fillStyle = "#4f5d78";
    ctx.fillRect(x, y, w, h);
    ctx.fillStyle = "#dbc5a2";
    ctx.fillRect(x + 2, y + 3, w - 4, h - 6);
    ctx.strokeStyle = "#3a2818";
    ctx.strokeRect(x + 2, y + 3, w - 4, h - 6);
    return;
  }
  const w = 36;
  const h = 10;
  const x = point.x - w / 2;
  const y = facing === "n" ? point.y - 2 : point.y - h + 2;
  ctx.fillStyle = "#4f5d78";
  ctx.fillRect(x, y, w, h);
  ctx.fillStyle = "#dbc5a2";
  ctx.fillRect(x + 3, y + 2, w - 6, h - 4);
  ctx.strokeStyle = "#3a2818";
  ctx.strokeRect(x + 3, y + 2, w - 6, h - 4);
}

function drawHandymanCloset(ctx, closet) {
  const x = closet.x - closet.w / 2;
  const y = closet.y - closet.h / 2;
  ctx.fillStyle = "#5a4a3a";
  ctx.fillRect(x, y, closet.w, closet.h);
  ctx.strokeStyle = "#ffb347";
  ctx.lineWidth = 2;
  ctx.strokeRect(x, y, closet.w, closet.h);
  ctx.fillStyle = "#ffd9a0";
  ctx.font = "10px Segoe UI";
  ctx.fillText("Bob's", x + 22, y + 24);
  ctx.fillText("Closet", x + 18, y + 38);
}

function drawMaidRoom(ctx, room) {
  const x = room.x - room.w / 2;
  const y = room.y - room.h / 2;
  ctx.fillStyle = "#4a3a4a";
  ctx.fillRect(x, y, room.w, room.h);
  ctx.strokeStyle = "#e8a0bf";
  ctx.lineWidth = 2;
  ctx.strokeRect(x, y, room.w, room.h);
  ctx.fillStyle = "#f5c6d8";
  ctx.font = "10px Segoe UI";
  ctx.fillText("Mary's", x + 18, y + 24);
  ctx.fillText("Maid Rm", x + 12, y + 38);
}

function getFloorColor(hour) {
  const h = ((hour % 24) + 24) % 24;
  if (h >= 5 && h < 8) return "#3a4258";
  if (h >= 8 && h < 17) return "#2f384c";
  if (h >= 17 && h < 20) return "#2a3148";
  return "#1e2538";
}

function getNightOverlay(hour) {
  const h = ((hour % 24) + 24) % 24;
  if (h >= 21 || h < 5) return 0.45;
  if (h >= 20 || h < 6) return 0.25;
  return 0;
}

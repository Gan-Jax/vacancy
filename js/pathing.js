import { CONFIG } from "./config.js";

/**
 * Shared pathing for player, staff, and guests.
 *
 * Rules:
 * - Rooms are solid walls unless the mover is allowed into that room.
 * - Player may enter unlocked rooms that are not occupied.
 * - Staff/guests may enter only their explicitly allowed room id.
 * - Hall travel uses the layout waypoint graph (never cut through rooms).
 */

export function getRoomRect(room, layout) {
  const center = layout.roomCenters[room.id - 1];
  const w = CONFIG.roomWidth;
  const h = CONFIG.roomHeight;
  return {
    x: center.x - w / 2,
    y: center.y - h / 2,
    w,
    h,
  };
}

/** Player / legacy helper: locked + occupied block entry. */
export function isRoomBlocking(room) {
  return !room.unlocked || room.status === "occupied";
}

/** Whether this mover may stand inside the room. */
export function mayEnterRoom(room, allowRoomId) {
  if (allowRoomId === "player") {
    return !isRoomBlocking(room);
  }
  if (allowRoomId != null && room.id === allowRoomId) return true;
  return false;
}

function circleHitsRect(cx, cy, radius, rect) {
  const nearestX = clamp(cx, rect.x, rect.x + rect.w);
  const nearestY = clamp(cy, rect.y, rect.y + rect.h);
  const dx = cx - nearestX;
  const dy = cy - nearestY;
  return dx * dx + dy * dy < radius * radius;
}

export function getOfficeRect(layout) {
  const o = layout.office;
  if (!o) return null;
  return {
    x: o.x - o.w / 2,
    y: o.y - o.h / 2,
    w: o.w,
    h: o.h,
  };
}

function mayEnterOffice(allowRoomId) {
  return allowRoomId === "player" || allowRoomId === "office";
}

export function collidesWithRooms(x, y, radius, rooms, layout, allowRoomId) {
  for (const room of rooms) {
    if (mayEnterRoom(room, allowRoomId)) continue;
    if (circleHitsRect(x, y, radius, getRoomRect(room, layout))) {
      return true;
    }
  }

  const officeRect = getOfficeRect(layout);
  if (officeRect && !mayEnterOffice(allowRoomId)) {
    if (circleHitsRect(x, y, radius, officeRect)) return true;
  }

  return false;
}

export function resolveRoomCollision(entity, rooms, layout, allowRoomId) {
  for (let pass = 0; pass < 6; pass++) {
    let hitKind = null;
    let hitRoom = null;
    let hitRect = null;

    for (const room of rooms) {
      if (mayEnterRoom(room, allowRoomId)) continue;
      const rect = getRoomRect(room, layout);
      if (circleHitsRect(entity.x, entity.y, entity.radius, rect)) {
        hitKind = "room";
        hitRoom = room;
        hitRect = rect;
        break;
      }
    }

    if (!hitRect) {
      const officeRect = getOfficeRect(layout);
      if (
        officeRect &&
        !mayEnterOffice(allowRoomId) &&
        circleHitsRect(entity.x, entity.y, entity.radius, officeRect)
      ) {
        hitKind = "office";
        hitRect = officeRect;
      }
    }

    if (!hitRect) return;

    if (allowRoomId === "player") {
      pushOutOfRect(entity, hitRect);
    } else if (hitKind === "office") {
      const door = layout.waypoints.points[layout.waypoints.officeDoorIdx];
      if (door) {
        entity.x = door.x;
        entity.y = door.y;
      } else {
        pushOutOfRect(entity, hitRect);
      }
    } else {
      ejectThroughDoor(entity, hitRoom, layout);
    }
  }
}

function ejectThroughDoor(entity, room, layout) {
  const door = layout.waypoints.points[layout.waypoints.doorIdx[room.id - 1]];
  entity.x = door.x;
  entity.y = door.y;
}

function pushOutOfRect(entity, rect) {
  const cx = rect.x + rect.w / 2;
  const cy = rect.y + rect.h / 2;
  const dx = entity.x - cx;
  const dy = entity.y - cy;
  if (Math.abs(dx) * rect.h > Math.abs(dy) * rect.w) {
    entity.x = dx > 0 ? rect.x + rect.w + entity.radius : rect.x - entity.radius;
  } else {
    entity.y = dy > 0 ? rect.y + rect.h + entity.radius : rect.y - entity.radius;
  }
}

export function nearestDoorIndex(layout, x, y) {
  const { points, doorIdx } = layout.waypoints;
  let best = doorIdx[0];
  let bestDist = Infinity;
  for (const i of doorIdx) {
    const d = Math.hypot(points[i].x - x, points[i].y - y);
    if (d < bestDist) {
      bestDist = d;
      best = i;
    }
  }
  return best;
}

export function nearestWaypointIndex(layout, x, y) {
  const points = layout.waypoints.points;
  let best = 0;
  let bestDist = Infinity;
  for (let i = 0; i < points.length; i++) {
    const d = Math.hypot(points[i].x - x, points[i].y - y);
    if (d < bestDist) {
      bestDist = d;
      best = i;
    }
  }
  return best;
}

/** BFS shortest path on the hallway graph. Returns list of {x,y} points. */
export function findPath(layout, fromX, fromY, goalIdx) {
  const points = layout.waypoints.points;
  const start = nearestWaypointIndex(layout, fromX, fromY);
  if (start === goalIdx) {
    return [{ x: points[goalIdx].x, y: points[goalIdx].y }];
  }

  const prev = new Map([[start, -1]]);
  const queue = [start];

  while (queue.length) {
    const cur = queue.shift();
    if (cur === goalIdx) break;
    for (const next of points[cur].links) {
      if (prev.has(next)) continue;
      prev.set(next, cur);
      queue.push(next);
    }
  }

  if (!prev.has(goalIdx) || goalIdx == null || !points[goalIdx]) {
    const fallback = points[goalIdx] || points[start];
    return [{ x: fallback.x, y: fallback.y }];
  }

  const chain = [];
  let at = goalIdx;
  let guard = 0;
  while (at !== -1 && at != null && prev.has(at) && guard++ < points.length + 2) {
    chain.push(at);
    at = prev.get(at);
  }
  if (!chain.length) {
    return [{ x: points[goalIdx].x, y: points[goalIdx].y }];
  }
  chain.reverse();
  return chain.map((i) => ({ x: points[i].x, y: points[i].y }));
}

export function pathToRoomDoor(layout, fromX, fromY, roomId) {
  const doorIdx = layout.waypoints.doorIdx[roomId - 1];
  return findPath(layout, fromX, fromY, doorIdx);
}

export function pathToDeskHall(layout, fromX, fromY) {
  return findPath(layout, fromX, fromY, layout.waypoints.deskHallIdx);
}

/**
 * Step toward a point without entering blocked rooms.
 * Returns true if already at the target.
 */
export function steerTo(entity, tx, ty, dt, rooms, layout, allowRoomId, speed) {
  const dx = tx - entity.x;
  const dy = ty - entity.y;
  const dist = Math.hypot(dx, dy);
  if (dist < 3) {
    entity.x = tx;
    entity.y = ty;
    return true;
  }

  const len = dist || 1;
  const step = (speed ?? CONFIG.npcMoveSpeed) * dt;
  // Never overshoot the target — overshooting a door lands inside the room solid.
  const travel = Math.min(step, dist);
  const ax = Math.min(travel, Math.abs(dx));
  const ay = Math.min(travel, Math.abs(dy));
  const preferAxisFirst = Math.abs(dx) > 4 && Math.abs(dy) > 4;
  const dirX = (dx / len) * travel;
  const dirY = (dy / len) * travel;

  const attempts = preferAxisFirst
    ? [
        [Math.sign(dx) * ax, 0],
        [0, Math.sign(dy) * ay],
        [dirX, dirY],
        [-Math.sign(dy) * ay, 0],
        [0, -Math.sign(dx) * ax],
      ]
    : [
        [dirX, dirY],
        [dirX, 0],
        [0, dirY],
        [-dirY, dirX],
        [dirY, -dirX],
      ];

  for (const [mx, my] of attempts) {
    if (mx === 0 && my === 0) continue;
    const nx = entity.x + mx;
    const ny = entity.y + my;
    if (!collidesWithRooms(nx, ny, entity.radius, rooms, layout, allowRoomId)) {
      const b = layout.building;
      if (b) {
        entity.x = clamp(nx, b.x + 18, b.x + b.w - 18);
        entity.y = clamp(ny, b.y + 18, b.y + b.h - 18);
      } else {
        entity.x = clamp(nx, 30, layout.width - 30);
        entity.y = clamp(ny, 30, layout.height - 30);
      }
      resolveRoomCollision(entity, rooms, layout, allowRoomId);
      return Math.hypot(entity.x - tx, entity.y - ty) < 3;
    }
  }

  resolveRoomCollision(entity, rooms, layout, allowRoomId);
  return false;
}

/** Follow waypoint list. Returns true when the final point is reached. */
export function followPath(entity, dt, rooms, layout, allowRoomId, speed) {
  if (!entity.path?.length) return true;

  const target = entity.path[0];
  steerTo(entity, target.x, target.y, dt, rooms, layout, allowRoomId, speed);
  const dist = Math.hypot(entity.x - target.x, entity.y - target.y);

  if (dist < 14) {
    entity.x = target.x;
    entity.y = target.y;
    entity.path.shift();
    if (!entity.path.length) return true;
  }
  return false;
}

export function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

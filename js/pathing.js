import { CONFIG } from "./config.js";
import {
  findRoute,
  isCircleBlocked,
  nearestOpenCell,
  cellCenter,
} from "./nav.js";

/**
 * Shared movement for the player, staff and guests.
 *
 * All geometry lives in the floor's nav grid (js/nav.js): walls are cells, and
 * every private space carries a permit token. A character's permits are derived
 * from what it is currently allowed to enter, so one grid enforces "guests only
 * in their own room", "staff only in the room they are servicing", and "the
 * player may walk into any vacant unlocked room".
 */

export function getRoomRect(room, layout) {
  const planned = layout.rooms?.[room.id - 1];
  if (planned) return planned.rect;
  const center = layout.roomCenters[room.id - 1];
  return {
    x: center.x - CONFIG.roomWidth / 2,
    y: center.y - CONFIG.roomHeight / 2,
    w: CONFIG.roomWidth,
    h: CONFIG.roomHeight,
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

/**
 * Permit tokens for a mover. `allowRoomId` is a room id, "player", "office",
 * or null for a character with public access only.
 */
export function buildPermits(rooms, allowRoomId) {
  const permits = new Set();
  if (allowRoomId === "player") {
    permits.add("office");
    for (const room of rooms) {
      if (!isRoomBlocking(room)) permits.add(`room:${room.id}`);
    }
    return permits;
  }
  if (allowRoomId === "office") {
    permits.add("office");
    return permits;
  }
  if (allowRoomId != null) permits.add(`room:${allowRoomId}`);
  return permits;
}

export function collidesWithRooms(x, y, radius, rooms, layout, allowRoomId) {
  const grid = layout.navGrid;
  if (!grid) return false;
  const permits = buildPermits(rooms, allowRoomId);
  return isCircleBlocked(grid, x, y, radius, permits);
}

/** Nudge a character that ended up somewhere it may not stand. */
export function resolveRoomCollision(entity, rooms, layout, allowRoomId) {
  const grid = layout.navGrid;
  if (!grid) return;
  const permits = buildPermits(rooms, allowRoomId);
  if (!isCircleBlocked(grid, entity.x, entity.y, entity.radius, permits)) {
    return;
  }

  const cell = nearestOpenCell(grid, entity.x, entity.y, permits, entity.radius);
  if (!cell) return;
  const target = cellCenter(grid, cell.col, cell.row);
  entity.x = target.x;
  entity.y = target.y;
}

function routeOptions(entity, rooms, allowRoomId) {
  return {
    permits: buildPermits(rooms, allowRoomId),
    radius: entity?.radius ?? 11,
  };
}

/**
 * Route to a world point. Returns waypoints, or a single-point fallback so
 * callers always have something to steer at.
 */
export function findPath(layout, fromX, fromY, goal, options = {}) {
  const grid = layout.navGrid;
  if (!grid || !goal) return goal ? [{ x: goal.x, y: goal.y }] : [];

  const permits =
    options.permits ?? buildPermits(options.rooms ?? [], options.allowRoomId);
  const radius = options.radius ?? 11;
  const route = findRoute(grid, { x: fromX, y: fromY }, goal, permits, radius);
  if (!route || !route.length) return [{ x: goal.x, y: goal.y }];
  return route;
}

/** Walk to the public spot just outside a room's door. */
export function pathToRoomDoor(layout, fromX, fromY, roomId, options = {}) {
  return findPath(layout, fromX, fromY, layout.roomDoor(roomId), options);
}

/** Walk to the front desk approach inside the lobby. */
export function pathToDeskHall(layout, fromX, fromY, options = {}) {
  return findPath(layout, fromX, fromY, layout.deskApproach(), options);
}

/**
 * Step toward a point, sliding along anything in the way.
 * Returns true once the target is reached.
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

  const grid = layout.navGrid;
  const { permits } = routeOptions(entity, rooms, allowRoomId);
  const len = dist || 1;
  const step = (speed ?? CONFIG.npcMoveSpeed) * dt;
  // Never overshoot: sailing past a doorway lands inside a wall.
  const travel = Math.min(step, dist);
  const dirX = (dx / len) * travel;
  const dirY = (dy / len) * travel;

  const attempts = [
    [dirX, dirY],
    [dirX, 0],
    [0, dirY],
  ];

  for (const [mx, my] of attempts) {
    if (mx === 0 && my === 0) continue;
    const nx = entity.x + mx;
    const ny = entity.y + my;
    if (grid && isCircleBlocked(grid, nx, ny, entity.radius, permits)) continue;
    entity.x = nx;
    entity.y = ny;
    clampToBuilding(entity, layout);
    return Math.hypot(entity.x - tx, entity.y - ty) < 3;
  }

  resolveRoomCollision(entity, rooms, layout, allowRoomId);
  return false;
}

function clampToBuilding(entity, layout) {
  const b = layout.building;
  if (!b) return;
  entity.x = clamp(entity.x, b.x + 4, b.x + b.w - 4);
  entity.y = clamp(entity.y, b.y + 4, b.y + b.h - 4);
}

/** How long a character may make no headway before it re-routes. */
const STALL_LIMIT_SECONDS = 0.5;

/** Follow a waypoint list. Returns true when the final point is reached. */
export function followPath(entity, dt, rooms, layout, allowRoomId, speed) {
  if (!entity.path?.length) return true;

  const target = entity.path[0];
  const before = Math.hypot(entity.x - target.x, entity.y - target.y);
  steerTo(entity, target.x, target.y, dt, rooms, layout, allowRoomId, speed);
  const after = Math.hypot(entity.x - target.x, entity.y - target.y);

  // Nothing should ever be able to wedge itself permanently: if a character
  // stops making headway, re-route to the same destination from where it is.
  if (after < before - 0.05) {
    entity.stallSeconds = 0;
  } else {
    entity.stallSeconds = (entity.stallSeconds ?? 0) + dt;
    if (entity.stallSeconds >= STALL_LIMIT_SECONDS) {
      entity.stallSeconds = 0;
      const goal = entity.path[entity.path.length - 1];
      entity.path = findPath(layout, entity.x, entity.y, goal, {
        rooms,
        allowRoomId,
        radius: entity.radius,
      });
      return false;
    }
  }

  const reach = entity.path.length === 1 ? 6 : 12;
  if (Math.hypot(entity.x - target.x, entity.y - target.y) < reach) {
    entity.path.shift();
    if (!entity.path.length) return true;
  }
  return false;
}

export function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

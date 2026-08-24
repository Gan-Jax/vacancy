/**
 * Floorplans are data, not hand-placed geometry.
 *
 * A floor is a vertical stack of bands: rows of guest rooms, corridors, the
 * lobby, and a service band for departments. Each area carries its own walls
 * and door openings. js/nav.js bakes those areas into a walkable grid, so room
 * counts, wings, departments and extra floors are all just numbers here —
 * nothing downstream needs a hand-wired waypoint graph.
 *
 * Every dimension is a multiple of the nav tile, so walls and doors land on
 * exact grid cells instead of half-covering them.
 */

import { CONFIG } from "./config.js";

export const AREA = {
  CORRIDOR: "corridor",
  LOBBY: "lobby",
  GUEST_ROOM: "guestRoom",
  OFFICE: "office",
  DEPARTMENT: "department",
};

/**
 * Ground floor of the flagship hotel. Room rows sit against shared corridors
 * (double-loaded), and vertical side corridors run the full height so every
 * band stays connected without special-case links.
 */
export const FLAGSHIP_GROUND = {
  id: "ground",
  name: "Ground floor",
  level: 0,
  /** Navigation grid resolution in pixels. */
  tile: 10,
  /** Gap between the building shell and any usable space. */
  edge: 20,
  /** Minimum width of the vertical corridors on the far left and right. */
  sideCorridor: 50,
  /** Door openings are this wide — comfortably clear of body radii. */
  doorWidth: 40,
  /** Keep the lobby square: six rooms across, not a ten-room bowling alley. */
  maxRoomsPerRow: 6,
  roomSize: { w: CONFIG.roomWidth, h: CONFIG.roomHeight },
  bands: [
    { kind: "rooms", doorSide: "south" },
    { kind: "corridor", height: 40 },
    { kind: "rooms", doorSide: "north" },
    { kind: "corridor", height: 40 },
    { kind: "lobby", height: 280, grow: true },
    { kind: "service", height: 90 },
  ],
};

/** Areas any character may cross without a permit. */
const PUBLIC_KINDS = new Set([AREA.CORRIDOR, AREA.LOBBY, AREA.DEPARTMENT]);

const DEPARTMENTS = [
  { id: "housekeeping", label: "Mary's room", side: "left", accent: "#e8a0bf" },
  { id: "maintenance", label: "Bob's closet", side: "right", accent: "#ffb347" },
];

/** Tight building shell for a capped room row, centered on the lot. */
export function innBuildingRect(spec, lotWidth, lotHeight, marginX = 48, marginY = 40) {
  const tile = spec.tile;
  const down = (value) => Math.floor(value / tile) * tile;
  const roomW = down(spec.roomSize.w);
  const cols = spec.maxRoomsPerRow ?? 6;
  const innW = cols * roomW + down(spec.sideCorridor) * 2 + down(spec.edge) * 2;
  const innH = lotHeight - marginY - 68;
  return {
    x: down((lotWidth - innW) / 2),
    y: marginY,
    w: innW,
    h: down(innH),
  };
}

/**
 * Build one floor from a spec.
 *
 * @param spec   band/room definition, e.g. FLAGSHIP_GROUND
 * @param bounds building shell rect in pixels
 */
export function createFloor(spec, bounds) {
  const tile = spec.tile;
  const down = (value) => Math.floor(value / tile) * tile;

  const edge = down(spec.edge);
  const content = {
    x: bounds.x + edge,
    y: bounds.y + edge,
    w: down(bounds.w - edge * 2),
    h: down(bounds.h - edge * 2),
  };

  const roomSize = {
    w: down(spec.roomSize.w),
    h: down(spec.roomSize.h),
  };

  // Guest rooms sit flush in a centered block; leftover space widens the
  // vertical side corridors. Room count is therefore a function of space.
  const fit = Math.max(
    1,
    Math.floor((content.w - down(spec.sideCorridor) * 2) / roomSize.w)
  );
  const roomsPerRow = spec.maxRoomsPerRow
    ? Math.min(fit, spec.maxRoomsPerRow)
    : fit;
  const roomsBlockW = roomsPerRow * roomSize.w;
  const roomsBlockX = content.x + down((content.w - roomsBlockW) / 2);

  const bandHeights = resolveBandHeights(spec.bands, content.h, roomSize.h, tile);

  const areas = [];
  const rooms = [];
  let lobby = null;
  let office = null;
  let frontDesk = null;
  const departments = {};

  let cursorY = content.y;
  spec.bands.forEach((band, bandIndex) => {
    const height = bandHeights[bandIndex];
    const bandRect = { x: content.x, y: cursorY, w: content.w, h: height };

    if (band.kind === "rooms") {
      for (let col = 0; col < roomsPerRow; col++) {
        const roomRect = {
          x: roomsBlockX + col * roomSize.w,
          y: cursorY,
          w: roomSize.w,
          h: height,
        };
        const id = rooms.length + 1;
        const door = makeDoor(roomRect, band.doorSide, spec.doorWidth, 0.5, tile);
        areas.push({
          id: `room-${id}`,
          kind: AREA.GUEST_ROOM,
          token: `room:${id}`,
          label: `Room ${id}`,
          rect: roomRect,
          walls: true,
          doors: [door],
          roomId: id,
        });
        rooms.push({
          id,
          rect: roomRect,
          center: rectCenter(roomRect),
          doorSide: door.side,
          /** The opening itself, in the wall. */
          door: door.center,
          doorOpening: door,
          /** Public standing spot just outside the door. */
          approach: outsidePoint(door, tile),
        });
      }
      pushSideCorridors(areas, content, bandRect, roomsBlockX, roomsBlockW);
    } else if (band.kind === "lobby") {
      const lobbyRect = {
        x: roomsBlockX,
        y: cursorY,
        w: roomsBlockW,
        h: height,
      };
      lobby = lobbyRect;

      // Reception reads south→north the way a guest walks in from the lot:
      // double doors, waiting chairs, front desk, then the office (PC behind
      // the counter). Passages on both flanks lead to the guest-room corridors.
      areas.push({
        id: "lobby",
        kind: AREA.LOBBY,
        token: null,
        label: "Lobby",
        rect: lobbyRect,
        walls: true,
        doors: [
          makeDoor(lobbyRect, "north", spec.doorWidth, 0.14, tile),
          makeDoor(lobbyRect, "north", spec.doorWidth, 0.86, tile),
          makeDoor(lobbyRect, "south", spec.doorWidth * 2, 0.5, tile),
          makeDoor(lobbyRect, "west", spec.doorWidth, 0.72, tile),
          makeDoor(lobbyRect, "east", spec.doorWidth, 0.72, tile),
        ],
      });

      const officeW = down(300);
      const officeH = down(100);
      const officeRect = {
        x: lobbyRect.x + down((lobbyRect.w - officeW) / 2),
        y: lobbyRect.y + down(20),
        w: officeW,
        h: officeH,
      };
      const officeDoor = makeDoor(officeRect, "south", spec.doorWidth, 0.5, tile);
      areas.push({
        id: "office",
        kind: AREA.OFFICE,
        token: "office",
        label: "Office",
        rect: officeRect,
        walls: true,
        doors: [officeDoor],
      });
      office = {
        ...rectCenter(officeRect),
        w: officeRect.w,
        h: officeRect.h,
        rect: officeRect,
        door: officeDoor.center,
        approach: outsidePoint(officeDoor, tile),
      };

      const deskH = down(40);
      const staffAlley = down(60);
      frontDesk = {
        x: officeRect.x + officeRect.w / 2,
        y: officeRect.y + officeRect.h + staffAlley + deskH / 2,
        w: down(320),
        h: deskH,
      };

      pushSideCorridors(areas, content, bandRect, roomsBlockX, roomsBlockW);
    } else if (band.kind === "service") {
      areas.push({
        id: `service-${bandIndex}`,
        kind: AREA.CORRIDOR,
        token: null,
        label: "Service corridor",
        rect: bandRect,
        walls: false,
        doors: [],
      });
      for (const dept of DEPARTMENTS) {
        const deptW = down(170);
        const deptRect = {
          x:
            dept.side === "left"
              ? bandRect.x + tile
              : bandRect.x + bandRect.w - deptW - tile,
          y: bandRect.y + tile,
          w: deptW,
          h: bandRect.h - tile * 2,
        };
        areas.push({
          id: dept.id,
          kind: AREA.DEPARTMENT,
          token: null,
          label: dept.label,
          accent: dept.accent,
          rect: deptRect,
          walls: false,
          doors: [],
          departmentId: dept.id,
        });
        departments[dept.id] = {
          id: dept.id,
          label: dept.label,
          accent: dept.accent,
          rect: deptRect,
          ...rectCenter(deptRect),
          w: deptRect.w,
          h: deptRect.h,
        };
      }
    } else {
      areas.push({
        id: `corridor-${bandIndex}`,
        kind: AREA.CORRIDOR,
        token: null,
        label: "Corridor",
        rect: bandRect,
        walls: false,
        doors: [],
      });
    }

    cursorY += height;
  });

  pruneUnusableDoors(areas, tile);

  return {
    id: spec.id,
    name: spec.name,
    level: spec.level ?? 0,
    tile,
    spec,
    bounds,
    content,
    areas,
    rooms,
    roomsPerRow,
    roomSize,
    lobby,
    office,
    frontDesk,
    departments,
  };
}

/** Fixed band heights first; bands marked `grow` share what is left over. */
function resolveBandHeights(bands, totalHeight, roomHeight, tile) {
  const heights = bands.map((band) =>
    band.kind === "rooms"
      ? roomHeight
      : Math.floor((band.height ?? tile * 6) / tile) * tile
  );
  const growIndexes = bands
    .map((band, i) => (band.grow ? i : -1))
    .filter((i) => i >= 0);

  const slack = totalHeight - heights.reduce((sum, h) => sum + h, 0);
  if (slack > 0 && growIndexes.length) {
    const share = Math.floor(slack / growIndexes.length / tile) * tile;
    for (const i of growIndexes) heights[i] += share;
  }
  return heights;
}

/** The strips beside a room block / the lobby are vertical circulation. */
function pushSideCorridors(areas, content, bandRect, blockX, blockW) {
  const left = {
    x: content.x,
    y: bandRect.y,
    w: Math.max(0, blockX - content.x),
    h: bandRect.h,
  };
  const right = {
    x: blockX + blockW,
    y: bandRect.y,
    w: Math.max(0, content.x + content.w - (blockX + blockW)),
    h: bandRect.h,
  };
  for (const [side, rect] of [
    ["left", left],
    ["right", right],
  ]) {
    if (rect.w <= 0) continue;
    areas.push({
      id: `side-${side}-${Math.round(bandRect.y)}`,
      kind: AREA.CORRIDOR,
      token: null,
      label: "Corridor",
      rect,
      walls: false,
      doors: [],
    });
  }
}

/**
 * A door opening on one side of a rect. `along` slides it across that side
 * (0 = start, 1 = end); 0.5 centers it. Snapped to the tile grid.
 */
function makeDoor(rect, side, width, along, tile) {
  const normalized = normalizeSide(side);
  const doorWidth = Math.floor(width / tile) * tile;
  const horizontal = normalized === "north" || normalized === "south";
  const span = (horizontal ? rect.w : rect.h) - doorWidth;
  const offset = Math.floor((span * along) / tile) * tile;

  if (horizontal) {
    return {
      side: normalized,
      width: doorWidth,
      center: {
        x: rect.x + offset + doorWidth / 2,
        y: normalized === "north" ? rect.y : rect.y + rect.h,
      },
      normal: { x: 0, y: normalized === "north" ? -1 : 1 },
    };
  }
  return {
    side: normalized,
    width: doorWidth,
    center: {
      x: normalized === "west" ? rect.x : rect.x + rect.w,
      y: rect.y + offset + doorWidth / 2,
    },
    normal: { x: normalized === "west" ? -1 : 1, y: 0 },
  };
}

function normalizeSide(side) {
  if (side === "n") return "north";
  if (side === "s") return "south";
  if (side === "e") return "east";
  if (side === "w") return "west";
  return side;
}

/**
 * Standing spot just outside a door. Kept close to its own wall so two rooms
 * facing the same corridor do not claim the same spot.
 */
function outsidePoint(door, tile) {
  const reach = tile * 2;
  return {
    x: door.center.x + door.normal.x * reach,
    y: door.center.y + door.normal.y * reach,
  };
}

/**
 * Drop any door that would open into a wall or another private room. Bands can
 * be reordered freely without leaving doors that lead nowhere.
 */
function pruneUnusableDoors(areas, tile) {
  for (const area of areas) {
    if (!area.doors?.length) continue;
    area.doors = area.doors.filter((door) => {
      const probe = {
        x: door.center.x + door.normal.x * tile * 1.5,
        y: door.center.y + door.normal.y * tile * 1.5,
      };
      return areas.some(
        (other) => other !== area && isOpenSpace(other, probe, tile)
      );
    });
  }
}

function isOpenSpace(area, point, tile) {
  if (!PUBLIC_KINDS.has(area.kind)) return false;
  const r = area.rect;
  const pad = area.walls ? tile : 0;
  return (
    point.x >= r.x + pad &&
    point.x <= r.x + r.w - pad &&
    point.y >= r.y + pad &&
    point.y <= r.y + r.h - pad
  );
}

export function rectCenter(rect) {
  return { x: rect.x + rect.w / 2, y: rect.y + rect.h / 2 };
}

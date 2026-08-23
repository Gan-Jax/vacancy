/**
 * Navigation is derived, never hand-wired.
 *
 * A floor's areas are baked into a tile grid where each cell is either a wall
 * or walkable-with-an-owner. Owners are permit tokens ("room:7", "office"), so
 * the same grid answers "can this character stand here?" for guests, staff and
 * the player. Routes come from A* over that grid, then get straightened with a
 * line-of-sight pass so movement reads as walking, not tile-stepping.
 *
 * Because walls are cells, nothing can path through one. Adding rooms, wings,
 * departments or floors only changes the floorplan data.
 */

/** Build the walkable grid for one floor. */
export function buildNavGrid(floor) {
  const tile = floor.tile;
  const originX = floor.bounds.x;
  const originY = floor.bounds.y;
  const cols = Math.ceil(floor.bounds.w / tile);
  const rows = Math.ceil(floor.bounds.h / tile);

  const grid = {
    floorId: floor.id,
    level: floor.level,
    tile,
    originX,
    originY,
    cols,
    rows,
    // Everything starts solid; areas carve out the usable space, which leaves
    // the building shell as a wall no character can cross.
    blocked: new Uint8Array(cols * rows).fill(1),
    owner: new Array(cols * rows).fill(null),
  };

  for (const area of floor.areas) {
    stampArea(grid, area);
  }
  // Doors are carved after every area is stamped so a neighbouring wall can
  // never paint over an opening.
  for (const area of floor.areas) {
    for (const door of area.doors ?? []) {
      carveDoor(grid, area, door);
    }
  }

  grid.clearance = computeClearance(grid);
  return grid;
}

/**
 * Distance (in cells) from each cell to the nearest wall, via a two-pass
 * chamfer transform.
 *
 * Routing needs this because A* works on cell centres and has no idea how wide
 * a body is: without it, a path happily hugs the cell right next to a wall,
 * and anything with a radius then jams against that wall and never arrives.
 */
function computeClearance(grid) {
  const { cols, rows, blocked } = grid;
  const dist = new Float32Array(cols * rows);
  const ORTH = 1;
  const DIAG = Math.SQRT2;

  for (let i = 0; i < dist.length; i++) {
    dist[i] = blocked[i] ? 0 : Infinity;
  }

  for (let row = 0; row < rows; row++) {
    for (let col = 0; col < cols; col++) {
      const i = row * cols + col;
      if (dist[i] === 0) continue;
      let best = dist[i];
      if (col > 0) best = Math.min(best, dist[i - 1] + ORTH);
      if (row > 0) best = Math.min(best, dist[i - cols] + ORTH);
      if (row > 0 && col > 0) best = Math.min(best, dist[i - cols - 1] + DIAG);
      if (row > 0 && col < cols - 1) {
        best = Math.min(best, dist[i - cols + 1] + DIAG);
      }
      dist[i] = best;
    }
  }

  for (let row = rows - 1; row >= 0; row--) {
    for (let col = cols - 1; col >= 0; col--) {
      const i = row * cols + col;
      if (dist[i] === 0) continue;
      let best = dist[i];
      if (col < cols - 1) best = Math.min(best, dist[i + 1] + ORTH);
      if (row < rows - 1) best = Math.min(best, dist[i + cols] + ORTH);
      if (row < rows - 1 && col < cols - 1) {
        best = Math.min(best, dist[i + cols + 1] + DIAG);
      }
      if (row < rows - 1 && col > 0) {
        best = Math.min(best, dist[i + cols - 1] + DIAG);
      }
      dist[i] = best;
    }
  }

  return dist;
}

/** Free space between this cell's centre and the nearest wall, in pixels. */
export function cellClearancePx(grid, col, row) {
  if (!grid.clearance) return Infinity;
  const d = grid.clearance[row * grid.cols + col];
  if (!Number.isFinite(d)) return Infinity;
  // A wall in the neighbouring cell leaves half a tile of usable space.
  return (d - 0.5) * grid.tile;
}

function areaCellRange(grid, rect) {
  return {
    c0: Math.floor((rect.x - grid.originX) / grid.tile),
    r0: Math.floor((rect.y - grid.originY) / grid.tile),
    c1: Math.ceil((rect.x + rect.w - grid.originX) / grid.tile) - 1,
    r1: Math.ceil((rect.y + rect.h - grid.originY) / grid.tile) - 1,
  };
}

function stampArea(grid, area) {
  const { c0, r0, c1, r1 } = areaCellRange(grid, area.rect);
  for (let row = r0; row <= r1; row++) {
    for (let col = c0; col <= c1; col++) {
      if (col < 0 || row < 0 || col >= grid.cols || row >= grid.rows) continue;
      const i = row * grid.cols + col;
      const onRing =
        area.walls && (col === c0 || col === c1 || row === r0 || row === r1);
      if (onRing) {
        grid.blocked[i] = 1;
        grid.owner[i] = null;
      } else {
        grid.blocked[i] = 0;
        grid.owner[i] = area.token ?? null;
      }
    }
  }
}

/** Open a public gap through an area's wall ring. */
function carveDoor(grid, area, door) {
  const { c0, r0, c1, r1 } = areaCellRange(grid, area.rect);
  const half = door.width / 2;

  const open = (col, row) => {
    if (col < 0 || row < 0 || col >= grid.cols || row >= grid.rows) return;
    const i = row * grid.cols + col;
    grid.blocked[i] = 0;
    grid.owner[i] = null;
  };

  if (door.side === "north" || door.side === "south") {
    const row = door.side === "north" ? r0 : r1;
    const from = Math.floor((door.center.x - half - grid.originX) / grid.tile);
    const to = Math.ceil((door.center.x + half - grid.originX) / grid.tile) - 1;
    for (let col = from; col <= to; col++) open(col, row);
    return;
  }

  const col = door.side === "west" ? c0 : c1;
  const from = Math.floor((door.center.y - half - grid.originY) / grid.tile);
  const to = Math.ceil((door.center.y + half - grid.originY) / grid.tile) - 1;
  for (let row = from; row <= to; row++) open(col, row);
}

export function worldToCell(grid, x, y) {
  return {
    col: Math.floor((x - grid.originX) / grid.tile),
    row: Math.floor((y - grid.originY) / grid.tile),
  };
}

export function cellCenter(grid, col, row) {
  return {
    x: grid.originX + (col + 0.5) * grid.tile,
    y: grid.originY + (row + 0.5) * grid.tile,
  };
}

/** Can a character holding `permits` stand in this cell? */
export function isCellOpen(grid, col, row, permits) {
  if (col < 0 || row < 0 || col >= grid.cols || row >= grid.rows) return false;
  const i = row * grid.cols + col;
  if (grid.blocked[i]) return false;
  const token = grid.owner[i];
  return token === null || (permits != null && permits.has(token));
}

/** Does a body of this radius overlap anything it may not stand in? */
export function isCircleBlocked(grid, x, y, radius, permits) {
  const minCol = Math.floor((x - radius - grid.originX) / grid.tile);
  const maxCol = Math.floor((x + radius - grid.originX) / grid.tile);
  const minRow = Math.floor((y - radius - grid.originY) / grid.tile);
  const maxRow = Math.floor((y + radius - grid.originY) / grid.tile);

  for (let row = minRow; row <= maxRow; row++) {
    for (let col = minCol; col <= maxCol; col++) {
      if (isCellOpen(grid, col, row, permits)) continue;
      // Only count cells the body actually overlaps, not the bounding box.
      const cellX = grid.originX + col * grid.tile;
      const cellY = grid.originY + row * grid.tile;
      const nearestX = clampNumber(x, cellX, cellX + grid.tile);
      const nearestY = clampNumber(y, cellY, cellY + grid.tile);
      const dx = x - nearestX;
      const dy = y - nearestY;
      if (dx * dx + dy * dy < radius * radius) return true;
    }
  }
  return false;
}

/**
 * Can a body of this radius be routed through the cell? Combines permission
 * with having enough physical room to stand there.
 */
export function isCellRoutable(grid, col, row, permits, radius) {
  if (!isCellOpen(grid, col, row, permits)) return false;
  if (!(radius > 0)) return true;
  return cellClearancePx(grid, col, row) >= radius;
}

/** Closest cell this character may stand in — used when nudged into a wall. */
export function nearestOpenCell(grid, x, y, permits, radius = 0, maxRings = 40) {
  const start = worldToCell(grid, x, y);
  if (isCellRoutable(grid, start.col, start.row, permits, radius)) return start;

  for (let ring = 1; ring <= maxRings; ring++) {
    let best = null;
    let bestDist = Infinity;
    for (let dRow = -ring; dRow <= ring; dRow++) {
      for (let dCol = -ring; dCol <= ring; dCol++) {
        // Only the outer edge of each ring is new.
        if (Math.abs(dRow) !== ring && Math.abs(dCol) !== ring) continue;
        const col = start.col + dCol;
        const row = start.row + dRow;
        if (!isCellRoutable(grid, col, row, permits, radius)) continue;
        const center = cellCenter(grid, col, row);
        const dist = (center.x - x) ** 2 + (center.y - y) ** 2;
        if (dist < bestDist) {
          bestDist = dist;
          best = { col, row };
        }
      }
    }
    if (best) return best;
  }
  return null;
}

const STRAIGHT_COST = 10;
const DIAGONAL_COST = 14;

/** A* across the grid. Returns cells from start to goal, or null. */
export function findCellPath(grid, from, to, permits, radius = 0) {
  const start = nearestOpenCell(grid, from.x, from.y, permits, radius);
  const goal = nearestOpenCell(grid, to.x, to.y, permits, radius);
  if (!start || !goal) return null;

  const startIdx = start.row * grid.cols + start.col;
  const goalIdx = goal.row * grid.cols + goal.col;
  if (startIdx === goalIdx) return [start];

  const total = grid.cols * grid.rows;
  const gScore = new Float64Array(total).fill(Infinity);
  const cameFrom = new Int32Array(total).fill(-1);
  const closed = new Uint8Array(total);

  const heuristic = (col, row) => {
    const dCol = Math.abs(col - goal.col);
    const dRow = Math.abs(row - goal.row);
    const diag = Math.min(dCol, dRow);
    return DIAGONAL_COST * diag + STRAIGHT_COST * (dCol + dRow - 2 * diag);
  };

  const open = new MinHeap();
  gScore[startIdx] = 0;
  open.push(startIdx, heuristic(start.col, start.row));

  while (open.size) {
    const current = open.pop();
    if (current === goalIdx) break;
    if (closed[current]) continue;
    closed[current] = 1;

    const col = current % grid.cols;
    const row = (current - col) / grid.cols;

    for (let dRow = -1; dRow <= 1; dRow++) {
      for (let dCol = -1; dCol <= 1; dCol++) {
        if (dCol === 0 && dRow === 0) continue;
        const nCol = col + dCol;
        const nRow = row + dRow;
        if (!isCellRoutable(grid, nCol, nRow, permits, radius)) continue;
        // No slipping diagonally past a wall corner.
        if (dCol !== 0 && dRow !== 0) {
          if (!isCellRoutable(grid, col + dCol, row, permits, radius)) continue;
          if (!isCellRoutable(grid, col, row + dRow, permits, radius)) continue;
        }
        const nIdx = nRow * grid.cols + nCol;
        if (closed[nIdx]) continue;
        const step = dCol !== 0 && dRow !== 0 ? DIAGONAL_COST : STRAIGHT_COST;
        const tentative = gScore[current] + step;
        if (tentative >= gScore[nIdx]) continue;
        gScore[nIdx] = tentative;
        cameFrom[nIdx] = current;
        open.push(nIdx, tentative + heuristic(nCol, nRow));
      }
    }
  }

  if (cameFrom[goalIdx] === -1 && startIdx !== goalIdx) return null;

  const cells = [];
  let at = goalIdx;
  let guard = 0;
  while (at !== -1 && guard++ <= total) {
    const col = at % grid.cols;
    cells.push({ col, row: (at - col) / grid.cols });
    if (at === startIdx) break;
    at = cameFrom[at];
  }
  cells.reverse();
  return cells;
}

/**
 * Route between two world points. Returns straightened waypoints, ending on
 * the exact goal so callers can still test arrival precisely.
 */
export function findRoute(grid, from, to, permits, radius = 10) {
  const cells = findCellPath(grid, from, to, permits, radius);
  if (!cells) return null;

  const points = cells.map((cell) => cellCenter(grid, cell.col, cell.row));
  if (isCircleBlocked(grid, to.x, to.y, radius, permits)) {
    // Goal is tight (deep in a corner); stop at the last good cell.
  } else {
    points.push({ x: to.x, y: to.y });
  }
  return straighten(grid, points, permits, radius);
}

/** Drop waypoints we can already see past, so paths look walked not tiled. */
function straighten(grid, points, permits, radius) {
  if (points.length <= 2) return points;

  const result = [points[0]];
  let anchor = 0;
  for (let i = 2; i < points.length; i++) {
    if (segmentIsClear(grid, points[anchor], points[i], permits, radius)) {
      continue;
    }
    result.push(points[i - 1]);
    anchor = i - 1;
  }
  result.push(points[points.length - 1]);
  return result;
}

/**
 * Sample a straight line for anything the body could not pass through.
 *
 * This must use the *same* radius that movement collides with. Probing with a
 * smaller body lets smoothing cut a diagonal through a doorway that the real
 * body cannot fit, and the character then wedges on the door jamb forever.
 */
export function segmentIsClear(grid, a, b, permits, radius) {
  const dist = Math.hypot(b.x - a.x, b.y - a.y);
  const steps = Math.max(2, Math.ceil(dist / (grid.tile * 0.5)));
  const probe = Math.max(2, radius);
  for (let i = 0; i <= steps; i++) {
    const t = i / steps;
    const x = a.x + (b.x - a.x) * t;
    const y = a.y + (b.y - a.y) * t;
    if (isCircleBlocked(grid, x, y, probe, permits)) return false;
  }
  return true;
}

/**
 * Startup self-check: can a public visitor reach every room's door, and does
 * every room open onto walkable space? Returns a list of problems.
 */
export function validateFloor(grid, floor, from) {
  const problems = [];
  const origin = from ?? floor.frontDesk ?? floor.content;

  for (const room of floor.rooms) {
    if (isCircleBlocked(grid, room.approach.x, room.approach.y, 8, null)) {
      problems.push(`Room ${room.id} doorway is walled in`);
      continue;
    }
    const publicRoute = findRoute(grid, origin, room.approach, null, 12);
    if (!publicRoute) {
      problems.push(`Room ${room.id} door is unreachable from the lobby`);
      continue;
    }
    const permits = new Set([`room:${room.id}`]);
    const insideRoute = findRoute(grid, room.approach, room.center, permits, 12);
    if (!insideRoute) {
      problems.push(`Room ${room.id} interior is unreachable from its door`);
    }
  }

  if (floor.office) {
    const officeRoute = findRoute(grid, origin, floor.office.approach, null, 12);
    if (!officeRoute) problems.push("Office door is unreachable");
  }

  for (const dept of Object.values(floor.departments ?? {})) {
    const route = findRoute(grid, origin, dept, null, 12);
    if (!route) problems.push(`${dept.label} is unreachable`);
  }

  return problems;
}

function clampNumber(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

/** Small binary heap so A* stays quick on big floors. */
class MinHeap {
  constructor() {
    this.items = [];
    this.priorities = [];
  }

  get size() {
    return this.items.length;
  }

  push(item, priority) {
    this.items.push(item);
    this.priorities.push(priority);
    let i = this.items.length - 1;
    while (i > 0) {
      const parent = (i - 1) >> 1;
      if (this.priorities[parent] <= this.priorities[i]) break;
      this.swap(parent, i);
      i = parent;
    }
  }

  pop() {
    const top = this.items[0];
    const lastItem = this.items.pop();
    const lastPriority = this.priorities.pop();
    if (this.items.length) {
      this.items[0] = lastItem;
      this.priorities[0] = lastPriority;
      let i = 0;
      for (;;) {
        const left = i * 2 + 1;
        const right = left + 1;
        let smallest = i;
        if (
          left < this.items.length &&
          this.priorities[left] < this.priorities[smallest]
        ) {
          smallest = left;
        }
        if (
          right < this.items.length &&
          this.priorities[right] < this.priorities[smallest]
        ) {
          smallest = right;
        }
        if (smallest === i) break;
        this.swap(i, smallest);
        i = smallest;
      }
    }
    return top;
  }

  swap(a, b) {
    [this.items[a], this.items[b]] = [this.items[b], this.items[a]];
    [this.priorities[a], this.priorities[b]] = [
      this.priorities[b],
      this.priorities[a],
    ];
  }
}

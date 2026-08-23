import { CONFIG, getTaskHours } from "./config.js";
import {
  beginRepairPayment,
  canAffordRepair,
  finishCleaning,
  finishInspection,
  finishRepair,
  getRepairCost,
} from "./economy.js";
import { addLog } from "./state.js";
import {
  clamp,
  collidesWithRooms,
  findPath,
  followPath,
  getRoomRect,
  isRoomBlocking,
  pathToDeskHall,
  resolveRoomCollision,
  steerTo,
} from "./pathing.js";

export { getRoomRect, isRoomBlocking };

/** Player entity — you walk around and interact with rooms. */
export class Player {
  constructor(x, y) {
    this.x = x;
    this.y = y;
    this.radius = 14;
    this.facing = "down";
    this.interactCooldown = 0;
    this.activeTask = null;
  }

  update(input, dt, layout, rooms) {
    this.interactCooldown = Math.max(0, this.interactCooldown - dt);

    if (this.activeTask) {
      this.activeTask.progress += CONFIG.hoursPerSecond * dt;
      if (this.activeTask.progress >= this.activeTask.duration) {
        const { type, room } = this.activeTask;
        this.activeTask = null;
        return { completed: type, room };
      }
      return null;
    }

    let dx = 0;
    let dy = 0;
    if (input.up()) dy -= 1;
    if (input.down()) dy += 1;
    if (input.left()) dx -= 1;
    if (input.right()) dx += 1;

    if (dx !== 0 || dy !== 0) {
      const len = Math.hypot(dx, dy) || 1;
      dx /= len;
      dy /= len;
      if (Math.abs(dx) > Math.abs(dy)) {
        this.facing = dx > 0 ? "right" : "left";
      } else {
        this.facing = dy > 0 ? "down" : "up";
      }
    }

    const speed = CONFIG.playerSpeed * dt;
    const nextX = this.x + dx * speed;
    const nextY = this.y + dy * speed;
    const allow = "player";

    if (!collidesWithRooms(nextX, this.y, this.radius, rooms, layout, allow)) {
      this.x = nextX;
    }
    if (!collidesWithRooms(this.x, nextY, this.radius, rooms, layout, allow)) {
      this.y = nextY;
    }

    const b = layout.building;
    this.x = clamp(this.x, b.x + 20, b.x + b.w - 20);
    this.y = clamp(this.y, b.y + 24, b.y + b.h - 20);
    resolveRoomCollision(this, rooms, layout, allow);

    return null;
  }

  startTask(type, room) {
    const duration = getTaskHours(type, room, false);
    this.activeTask = { type, room, progress: 0, duration };
    room.worker = "player";
  }

  getInteractTarget(rooms, layout, staffList = [], deskQueue = false) {
    let best = null;
    let bestDist = Infinity;
    const interactRange = 88;

    const radio = layout.lobbyRadio;
    if (radio) {
      const radioDist = Math.hypot(this.x - radio.x, this.y - radio.y);
      if (radioDist < 44) return { kind: "radio" };
    }

    // Prefer desk when staff are waiting for payday
    const desk = layout.frontDesk;
    const deskDist = Math.hypot(this.x - desk.x, this.y - desk.y);
    const deskBusy = staffList.some(
      (s) => s && (s.phase === "waiting_pay" || s.phase === "to_desk")
    );
    const deskRange = deskBusy ? 90 : 70;
    if (deskDist < deskRange && (deskBusy || deskQueue)) {
      return { kind: "desk" };
    }

    const paper = layout.newspaper;
    if (paper) {
      const paperDist = Math.hypot(this.x - paper.x, this.y - paper.y);
      if (paperDist < 46) return { kind: "newspaper" };
    }

    if (deskDist < deskRange) {
      return { kind: "desk" };
    }

    // Also allow paying a staff member by standing next to them
    for (const staff of staffList) {
      if (!staff) continue;
      if (staff.phase !== "waiting_pay" && staff.phase !== "to_desk") continue;
      const dist = Math.hypot(this.x - staff.x, this.y - staff.y);
      if (dist < 56) {
        return { kind: "desk" };
      }
    }

    // Office PC — use from inside the office room or at its door
    const office = layout.office;
    if (office) {
      const inside =
        Math.abs(this.x - office.x) < office.w / 2 - 4 &&
        Math.abs(this.y - office.y) < office.h / 2 - 4;
      const doorDist = Math.hypot(
        this.x - office.door.x,
        this.y - office.door.y
      );
      if (inside || doorDist < 40) {
        return { kind: "office" };
      }
    }

    // Vacancy sign (bottom center) — separate from desk
    const sign = layout.vacancySign;
    if (sign) {
      const signDist = Math.hypot(this.x - sign.x, this.y - sign.y);
      if (signDist < 55) {
        return { kind: "sign" };
      }
    }

    for (const room of rooms) {
      const center = layout.roomCenters[room.id - 1];
      const dist = Math.hypot(this.x - center.x, this.y - center.y);
      if (dist < interactRange && dist < bestDist) {
        best = room;
        bestDist = dist;
      }
    }

    if (best) return { kind: "room", room: best };
    return null;
  }
}

/** Staff NPC — hallway pathfinding + personal home base + weekly payday. */
export class StaffNPC {
  constructor(profile, home) {
    this.id = profile.id;
    this.name = profile.name;
    this.role = profile.role;
    this.color = profile.color;
    this.department = profile.department ?? null;
    this.x = home.x;
    this.y = home.y;
    this.radius = 12;
    this.activeTask = null;
    this.targetRoom = null;
    this.exitRoomId = null;
    this.path = [];
    this.phase = "idle";

    /** Payroll */
    this.wagesOwed = 0;
    this.daysWorkedInPeriod = 0;
    this.periodDays = 0;
    this.workedToday = false;
    this.paydayDue = false;
  }

  static spawnAtHome(layout, profile) {
    return new StaffNPC(profile, layout.staffHome(profile.department ?? profile.id));
  }

  homePoint(layout) {
    return layout.staffHome(this.department ?? this.id);
  }

  pathHome(layout) {
    return findPath(layout, this.x, this.y, this.homePoint(layout), {
      radius: this.radius,
    });
  }

  paySlot(layout) {
    return layout.staffPaySlot(this.id);
  }

  /** Called once per calendar day rollover while hired. */
  onNewDay(state) {
    if (this.workedToday) {
      this.wagesOwed += CONFIG.staffDailyWage;
      this.daysWorkedInPeriod += 1;
      this.workedToday = false;
    }

    this.periodDays += 1;
    if (this.periodDays >= CONFIG.staffPayPeriodDays && !this.paydayDue) {
      this.paydayDue = true;
      addLog(
        state,
        `${this.name} is due for payday ($${this.wagesOwed} for ${this.daysWorkedInPeriod} work day${this.daysWorkedInPeriod === 1 ? "" : "s"}). Heading to the front desk.`
      );
    }
  }

  collectPaycheck(state, layout = null) {
    const paid = this.wagesOwed;
    this.wagesOwed = 0;
    this.daysWorkedInPeriod = 0;
    this.periodDays = 0;
    this.paydayDue = false;
    this.targetRoom = null;
    this.phase = "to_closet";
    this.path = layout ? this.pathHome(layout) : [];
    addLog(
      state,
      `Paid ${this.name} $${paid} wages. Next payday in ${CONFIG.staffPayPeriodDays} days.`
    );
    return paid;
  }

  beginPaydayTrip(layout) {
    this.targetRoom = null;
    this.path = pathToDeskHall(layout, this.x, this.y, { radius: this.radius });
    this.phase = "to_desk";
  }

  update(dt, state, layout) {
    const allowId = this.getAllowedRoomId();
    const rooms = state.rooms;
    const speed = CONFIG.npcMoveSpeed;

    resolveRoomCollision(this, rooms, layout, allowId);

    if (this.activeTask) {
      this.phase = "working";
      this.activeTask.progress += CONFIG.hoursPerSecond * dt;
      const center = layout.roomCenters[this.activeTask.room.id - 1];
      steerTo(this, center.x, center.y, dt, rooms, layout, allowId, speed);

      if (this.activeTask.progress >= this.activeTask.duration) {
        const { type, room } = this.activeTask;
        this.exitRoomId = room.id;
        this.activeTask = null;
        this.targetRoom = null;
        room.worker = null;
        this.workedToday = true;
        if (type === "inspect") finishInspection(state, room, this.name);
        if (type === "clean") finishCleaning(state, room, this.name);
        if (type === "repair") finishRepair(state, room, this.name);
        this.phase = "exit_room";
        this.path = [];
      }
      return;
    }

    if (this.phase === "waiting_pay") {
      const slot = this.paySlot(layout);
      this.x = slot.x;
      this.y = slot.y;
      return;
    }

    if (this.phase === "to_desk") {
      if (!this.path.length) {
        this.path = pathToDeskHall(layout, this.x, this.y, {
          radius: this.radius,
        });
      }
      const atHall = followPath(this, dt, rooms, layout, null, speed);
      const slot = this.paySlot(layout);
      if (atHall) {
        const dist = Math.hypot(this.x - slot.x, this.y - slot.y);
        steerTo(this, slot.x, slot.y, dt, rooms, layout, null, speed);
        if (dist < 18) {
          this.x = slot.x;
          this.y = slot.y;
          this.phase = "waiting_pay";
          this.path = [];
          addLog(
            state,
            `${this.name} is at the desk for payday ($${this.wagesOwed}). Press E.`
          );
        }
      } else {
        // If already near the pay slot (desk spur), don't stay stuck on hall path
        const dist = Math.hypot(this.x - slot.x, this.y - slot.y);
        if (dist < 40) {
          this.path = [];
          steerTo(this, slot.x, slot.y, dt, rooms, layout, null, speed);
          if (dist < 18) {
            this.x = slot.x;
            this.y = slot.y;
            this.phase = "waiting_pay";
            this.path = [];
            addLog(
              state,
              `${this.name} is at the desk for payday ($${this.wagesOwed}). Press E.`
            );
          }
        }
      }
      return;
    }

    if (this.phase === "exit_room" && this.exitRoomId != null) {
      const door = layout.roomDoor(this.exitRoomId);
      const dist = Math.hypot(this.x - door.x, this.y - door.y);
      steerTo(this, door.x, door.y, dt, rooms, layout, this.exitRoomId, speed);
      if (dist < 16) {
        this.exitRoomId = null;
        if (this.paydayDue) {
          this.beginPaydayTrip(layout);
        } else {
          this.phase = "to_closet";
          this.path = this.pathHome(layout);
        }
      }
      return;
    }

    // Payday interrupts new job seeking (finish current work first).
    if (this.paydayDue) {
      this.targetRoom = null;
      if (this.phase !== "to_desk" && this.phase !== "waiting_pay") {
        this.beginPaydayTrip(layout);
      }
      return;
    }

    if (!isValidJobForRole(this.targetRoom, this.role, this.id, state)) {
      this.targetRoom = pickJobRoom(state.rooms, this.role, state);
      if (this.targetRoom) {
        this.phase = "to_door";
        this.path = findPath(
          layout,
          this.x,
          this.y,
          layout.roomDoor(this.targetRoom.id),
          { radius: this.radius }
        );
      } else if (this.phase !== "idle" && this.phase !== "to_closet") {
        this.phase = "to_closet";
        this.path = this.pathHome(layout);
      }
    }

    if (!this.targetRoom) {
      const home = this.homePoint(layout);
      const distHome = Math.hypot(this.x - home.x, this.y - home.y);
      if (distHome > 14) {
        if (!this.path.length || this.phase !== "to_closet") {
          this.phase = "to_closet";
          this.path = this.pathHome(layout);
        }
        followPath(this, dt, rooms, layout, null, speed);
      } else {
        this.phase = "idle";
        this.path = [];
        this.x += (home.x - this.x) * 0.25;
        this.y += (home.y - this.y) * 0.25;
      }
      return;
    }

    if (this.phase === "to_door") {
      if (!this.path.length) {
        this.path = findPath(
          layout,
          this.x,
          this.y,
          layout.roomDoor(this.targetRoom.id),
          { radius: this.radius }
        );
      }
      const atDoor = followPath(this, dt, rooms, layout, null, speed);
      if (atDoor) {
        this.phase = "enter_room";
        this.path = [];
      }
      return;
    }

    if (this.phase === "enter_room") {
      if (!isValidJobForRole(this.targetRoom, this.role, this.id, state)) {
        this.targetRoom = null;
        this.phase = "to_closet";
        this.path = this.pathHome(layout);
        return;
      }

      const center = layout.roomCenters[this.targetRoom.id - 1];
      const dist = Math.hypot(this.x - center.x, this.y - center.y);
      steerTo(
        this,
        center.x,
        center.y,
        dt,
        rooms,
        layout,
        this.targetRoom.id,
        speed
      );

      if (dist < 26) {
        const room = this.targetRoom;
        if (room.worker) {
          this.targetRoom = null;
          this.phase = "to_closet";
          this.path = this.pathHome(layout);
          return;
        }
        const type = jobTypeForRoom(room, this.role);
        if (!type) {
          this.targetRoom = null;
          this.phase = "to_closet";
          this.path = this.pathHome(layout);
          return;
        }
        if (type === "repair") {
          const cost = getRepairCost(state, room.repairLevel);
          const paid = beginRepairPayment(state, room);
          if (paid == null) {
            addLog(
              state,
              `${this.name} can't start Room ${room.id} repair — need $${cost}.`
            );
            this.targetRoom = null;
            this.phase = "to_closet";
            this.path = this.pathHome(layout);
            return;
          }
          if (paid > 0) {
            addLog(
              state,
              `${this.name} bought $${paid} in parts for Room ${room.id} (${room.repairLevel}).`
            );
          }
        }
        this.activeTask = {
          type,
          room,
          progress: 0,
          duration: getTaskHours(type, room, true),
        };
        room.worker = this.id;
        this.phase = "working";
      }
      return;
    }

    if (this.phase === "to_closet") {
      if (!this.path.length) {
        this.path = this.pathHome(layout);
      }
      const arrived = followPath(this, dt, rooms, layout, null, speed);
      if (arrived) {
        this.phase = "idle";
        this.path = [];
      }
    }
  }

  getAllowedRoomId() {
    if (this.activeTask?.room) return this.activeTask.room.id;
    if (this.phase === "enter_room" && this.targetRoom) return this.targetRoom.id;
    if (this.phase === "exit_room" && this.exitRoomId != null) return this.exitRoomId;
    if (this.phase === "working" && this.targetRoom) return this.targetRoom.id;
    return null;
  }
}

/** @deprecated use StaffNPC */
export const Handyman = StaffNPC;

/** Mary: inspect + clean. Bob: repair. */
function jobTypeForRoom(room, role) {
  if (!room) return null;
  if (role === "repair") {
    return room.status === "needs_repair" ? "repair" : null;
  }
  if (role === "housekeeping" || role === "clean") {
    if (room.status === "needs_inspection") return "inspect";
    if (room.status === "dirty") return "clean";
  }
  return null;
}

function isValidJobForRole(room, role, workerId, state = null) {
  if (!room || !room.unlocked) return false;
  if (room.worker && room.worker !== workerId) return false;
  if (jobTypeForRoom(room, role) == null) return false;
  if (
    role === "repair" &&
    state &&
    !room.repairPaid &&
    !canAffordRepair(state, room)
  ) {
    return false;
  }
  return true;
}

function pickJobRoom(rooms, role, state = null) {
  if (role === "repair") {
    return (
      rooms.find(
        (r) =>
          r.unlocked &&
          r.status === "needs_repair" &&
          !r.worker &&
          (!state || canAffordRepair(state, r) || r.repairPaid)
      ) || null
    );
  }
  if (role === "housekeeping" || role === "clean") {
    // Prefer inspect first so the flow stays Inspect → Clean → Repair
    return (
      rooms.find(
        (r) => r.unlocked && r.status === "needs_inspection" && !r.worker
      ) ||
      rooms.find((r) => r.unlocked && r.status === "dirty" && !r.worker) ||
      null
    );
  }
  return null;
}

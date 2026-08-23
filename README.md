# Vacancy

A browser prototype that **presents itself as a roadside hotel simulator** and
slowly stops being one.

You run a small inn on the outskirts of a mid-sized city. For the first several
days that is the whole game: check guests in, inspect, clean, repair, order
supplies, hire staff. Then the details start not adding up — and the resource
management you already learned becomes shelter management, because people are
going to die if you get it wrong.

Tonal reference: *No, I'm Not a Human*.

> Forked from the plain hotel sim in `../hotel-simulator`, which is left
> untouched as the "pure simulator" branch of the idea.

## How to run

The game uses JS modules, so it needs a local server (not `file://`).

```powershell
# from this folder
python -m http.server 8080   # then open http://localhost:8080
```

Or use the Live Server extension in Cursor: right-click `index.html` →
**Open with Live Server**.

## Controls

| Key | Action |
|-----|--------|
| WASD / arrows | Move |
| E / Space | Interact — desk, office PC, unlock, inspect → clean → repair |
| V | Flip the Vacancy / No Vacancy sign |
| R | Reinforce barricades with lumber (shelter era) |
| P | Pause |
| X | Layout inspector — hover for name/position, click to log coordinates |

Debug buttons next to Cash: **+$500** and **+1 day** (the day skip is how you
reach the later acts without waiting).

## The five acts

Acts advance on **keystone events**. Each keystone needs a day threshold *and*
something the player actually did, so the collapse tracks how you have been
running the place rather than the calendar alone.

| Act | Label | What changes |
|-----|-------|--------------|
| 1 | Quiet season | Plays exactly like the plain hotel sim. Ambient hints only. |
| 2 | Something off | Radio dispatches begin. Guests with no luggage. Deliveries slip. |
| 3 | The city goes dark | Shelter resources unlock on the office PC. Utilities flicker. |
| 4 | No one is coming | Barricades start decaying nightly. Arrivals are survivors, not guests. |
| 5 | Shelter | Rooms are bunks. Your job is that everyone stays alive. |

Ambient hints are deliberately non-mechanical — a dog that stops barking, a
guest asking whether the hotel has a basement, the glow over the city being the
wrong color. They accumulate instead of announcing themselves.

## The desk decision

Pressing **E** at the front desk opens the arrival review instead of checking
someone in automatically. Every arrival is one of three things, and the player
never sees which:

| Kind | Pays | Notes |
|------|------|-------|
| `traveler` | Yes | An ordinary guest. Common early, gone by Act 4. |
| `survivor` | No | Genuine, needs shelter. Appears from Act 3. |
| `wrong` | — | Not what they claim. Rare early, common late. |

### Why the decision has a "why"

Turning someone away only means something if refusing costs something too, so
the panel lays out three pressures that pull against each other:

- **Capacity** — is there a clean room at all
- **Sustain** — admitting one more person converts directly into *fewer days of
  water and food*, shown as `24d → 12d if admitted`
- **Threat** — the signs you have noticed about this person

And refusing is never free:

- Refuse a **traveler** → lost income and reputation
- Refuse a **survivor** → **humanity** drops, and the log does not let you off
- Refuse a **wrong** one → you are not told you were right, until two days later
  when someone finds what is left of them past the treeline

Letting an arrival stand at the desk until their patience runs out counts as
refusing them. Inaction is an answer.

### Evidence is deliberately unreliable

Signs come from two pools. Damning signs (their reflection lags, they know
Mary's name and Mary has not said it, they are not breathing when they think you
are not looking) genuinely indicate a `wrong` arrival. Innocuous signs (they pay
in coins, they flinch at the ice machine, they will not put their bag down) mean
nothing — honest people are strange too.

A `wrong` arrival draws mostly damning signs. An honest one draws mostly
innocuous ones, but has a **16% chance of a false positive**. You can **ask up
to two questions** to reveal more, and each question burns **45 minutes** of
their patience. The panel never tells you how many signs are still hidden,
because knowing when to stop looking is the decision.

Admitting a `wrong` arrival does not fail immediately. It fails the following
night — stores opened without the lock being forced, a barricade dismantled from
the inside, a room whose bed was not slept in and whose window is open from the
outside — so the consequence lands after the choice, not with it.

## Radio and newspaper

The **lobby radio** is public. Every arrival has heard it — including the ones
that are not people. Ask a radio question and a `wrong` guest will recite the
broadcast almost word for word. That is the trap.

The **newspaper** on the desk prints what the radio left out: a name, a blue
shutter, a boy who asks about a dog. Those extra facts unlock **paper
questions** at the desk, and those are the ones a copy has not rehearsed.

Open the radio with **E** on the set (or click the Radio HUD). Read the paper
with **E** on the stack when the desk is otherwise clear. Until you read an
issue, the useful questions do not appear.

## Resource → shelter evolution

Early game is hotel supplies: towels, soap, shampoo, conditioner, toilet paper.

Once the city goes dark, the same office PC also sells **water, food, fuel,
medicine, and lumber**. From then on:

- Water and food are consumed **per occupant per day**
- Fuel runs the generator; run out and the power goes down
- Barricades lose integrity every night; lumber repairs them (**R**)
- Shortages cost morale instead of just reputation

## Files

| File | Purpose |
|------|---------|
| `index.html` | Page layout, HUD, story banner, office PC modal |
| `css/style.css` | Colors and UI styling |
| `js/config.js` | **Balance knobs** — rates, story pacing, shelter consumption |
| `js/state.js` | Central game state |
| `js/story.js` | **Acts, keystone events, ambient hints** |
| `js/arrivals.js` | **Arrival dossiers, evidence, admit/refuse consequences** |
| `js/media.js` | **Radio broadcasts, newspaper issues, desk questions** |
| `js/shelter.js` | **Shelter resources, generator, barricade integrity** |
| `js/economy.js` | Check-in/out, billing, guest movement, daily tick |
| `js/inventory.js` | Hotel supplies + shelter orders through the office PC |
| `js/entities.js` | Player and staff NPC behavior |
| `js/pathing.js` | Shared waypoint pathfinding and collision |
| `js/render.js` | Canvas drawing, layout, layout inspector |
| `js/main.js` | Wires everything together |

`window.game` is exposed in the console (`state`, `layout`, `skipDay()`) for
poking at story and shelter state directly.

## Next steps

- [ ] Night threat encounters gated on barricade integrity
- [ ] Survivors who contribute (repair, clean) so admitting has upside
- [ ] Humanity gating story branches and dialogue
- [ ] Occupants who can die from shortages, not just lose morale
- [ ] Endings based on how many people you kept alive

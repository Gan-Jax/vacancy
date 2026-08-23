# Vacancy — Unity

Same story-driven inn as the browser prototype, now running in Unity as a top-down 2D game.

The hotel is **generated from data** (rooms, corridors, lobby, nav grid, A*). There is no hand-placed level yet. Press Play and the ground floor builds itself. Story, desk admit/refuse, radio/paper, and shelter ride on top of that building.

## Open it

1. Install [Unity Hub](https://unity.com/download).
2. Install **Unity 6** (you have **6000.5.9f1** at `C:\Program Files\Unity\Hub\Editor\6000.5.9f1`).
3. In Hub: **Add** → pick this folder:
   `C:\Users\Jorge\Projects\vacancy\Unity`
4. Open the project. First import can take a few minutes.
5. Open `Assets/Scenes/Vacancy.unity` if it is not already open.
6. Press **Play**.

The game starts itself even in an empty scene (`VacancyGame` auto-boots).

If Unity opens in **Safe Mode** from the first import, click **Ignore** / **Exit Safe Mode**. A name clash in `GameState` is already fixed; scripts compile.

## Controls

| Key | Action |
|-----|--------|
| WASD / arrows | Move |
| E / Space | Desk, radio, paper, office PC, unlock, inspect → clean → repair |
| V | Vacancy sign |
| R | Reinforce barricades with lumber (shelter era) |
| P | Pause |
| Esc | Close office PC / radio / paper / desk review |

Shop buttons on the right hire Bob / Mary, unlock rooms, and flip the sign. `+$500` and `+1 day` are debug buttons.

At the desk, **E** opens the arrival review. Ask from the radio or paper, then admit or turn away. Paper questions only unlock after you read the issue.

## What was ported

| Browser file | Unity script |
|--------------|--------------|
| `js/config.js` | `Assets/Scripts/Core/GameConfig.cs` |
| `js/state.js` | `Assets/Scripts/Core/GameState.cs` |
| `js/inventory.js` | `Assets/Scripts/Core/InventorySystem.cs` |
| `js/floorplan.js` | `Assets/Scripts/Building/Floorplan.cs` |
| `js/nav.js` | `Assets/Scripts/Building/NavGrid.cs` |
| `js/pathing.js` | `Assets/Scripts/Building/Pathing.cs` |
| `js/render.js` (`createLayout`) | `Assets/Scripts/Building/HotelLayout.cs` |
| `js/economy.js` | `Assets/Scripts/Systems/Economy.cs` |
| `js/story.js` | `Assets/Scripts/Systems/Story.cs` |
| `js/media.js` | `Assets/Scripts/Systems/Media.cs` |
| `js/arrivals.js` | `Assets/Scripts/Systems/Arrivals.cs` |
| `js/shelter.js` | `Assets/Scripts/Systems/Shelter.cs` |
| `js/entities.js` | `Assets/Scripts/Actors/` |
| `js/main.js` | `Assets/Scripts/VacancyGame.cs` |
| canvas drawing | `Assets/Scripts/Presentation/HotelView.cs` |
| HTML HUD | `Assets/Scripts/Presentation/HudView.cs` |

Balance numbers are the same. Tweak `GameConfig.cs` the way you used to tweak `config.js`.

This is **not** Project RM. Namespace is `Vacancy`. Product name is Vacancy. The building data matches RM's generated hotel; the game on top of it (story, arrivals, radio, shelter) is Vacancy only.

## Look

This first pass keeps the prototype look on purpose: colored rooms, circle people, Segoe UI labels, time-of-day background. That is the scaffold for real art later — sprites and furniture can replace the generated quads without touching check-in, pathing, or staff.

## Next (when you are ready)

- Swap generated rooms for tiles / sprites
- Sprite characters instead of circles
- Layout inspect (X) in Unity
- Save / load

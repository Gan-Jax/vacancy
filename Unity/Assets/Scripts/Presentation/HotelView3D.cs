using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vacancy
{
    public sealed class HotelView3D
    {
        readonly HotelLayout layout;
        readonly Transform root;
        readonly Camera playerCamera;
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly Dictionary<int, Renderer> roomFloors = new Dictionary<int, Renderer>();
        Renderer vacancySign;
        readonly List<CharacterView> characters = new List<CharacterView>();
        readonly List<CarView> carViews = new List<CarView>();
        CharacterModel playerBody;
        readonly Text hint;
        readonly Text inspectBanner;
        readonly Text pinReadout;
        readonly Transform pinMarker;
        readonly Font font;
        readonly Mesh cubeMesh;
        readonly Mesh cylinderMesh;

        public HotelView3D(HotelLayout layout, Transform parent, Camera playerCamera)
        {
            this.layout = layout;
            this.playerCamera = playerCamera;
            root = new GameObject("HotelView3D").transform;
            root.SetParent(parent, false);
            cubeMesh = BuildCube();
            cylinderMesh = BuildCylinder(12);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Helvetica" }, 16)
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            BuildGround();
            BuildInteriors();
            BuildFurniture();
            BuildLights();
            playerBody = CharacterModel.BuildFirstPerson(root, playerCamera, Mat);
            playerBody.Recolor(Palette.Player);
            hint = BuildHint(parent);
            inspectBanner = BuildInspectBanner(hint.transform.parent);
            pinReadout = BuildPinReadout(hint.transform.parent);
            pinMarker = BuildPinMarker();
        }

        public void SyncPlayer(PlayerActor player, float dt)
        {
            playerBody?.SyncFirstPerson(player, dt);
        }

        public void Refresh(GameState state, PlayerActor player, List<StaffNpc> staff)
        {
            RenderSettings.ambientLight = Palette.FloorColor(state.Hour) * 1.15f;
            if (playerCamera != null) playerCamera.backgroundColor = Palette.FloorColor(state.Hour);

            foreach (var room in state.Rooms)
            {
                if (roomFloors.TryGetValue(room.Id, out var floor))
                {
                    floor.sharedMaterial = Mat(Palette.RoomColor(room));
                }
            }

            vacancySign.sharedMaterial = Mat(state.VacancyOpen ? Palette.Hex("#2f6b3a") : Palette.Hex("#7a2e2e"));

            int used = 0;
            int deskIndex = 0;
            for (int i = 0; i < state.WaitingGuests.Count; i++)
            {
                var guest = state.WaitingGuests[i];
                string phase = string.IsNullOrEmpty(guest.ArrivePhase) ? "waiting" : guest.ArrivePhase;
                if (phase == "driving" || phase == "driving_away") continue;

                float x = guest.X;
                float y = guest.Y;
                float lookX = float.NaN;
                float lookY = float.NaN;
                string tag = guest.Name;
                if (phase == "waiting")
                {
                    var slot = layout.CheckInLineSlot(deskIndex++);
                    x = slot.X;
                    y = slot.Y;
                    lookX = layout.FrontDesk.X;
                    lookY = layout.FrontDesk.Y;
                    tag = deskIndex == 1 ? $"{guest.Name} ★" : guest.Name;
                }
                else
                {
                    PathLook(guest, ref lookX, ref lookY);
                }

                PlaceCharacter(used++, x, y, Palette.Hex("#e8a0bf"), tag, lookX, lookY, guest.FootY);
            }

            foreach (var guest in state.ActiveGuests)
            {
                if (guest.Phase == "in_room") continue;
                Color color = Palette.Hex("#d4a574");
                string label = guest.Name;
                if (guest.Phase == "walking_to_room")
                {
                    label = $"{guest.Name} ->{guest.RoomId}";
                    color = Palette.Hex("#7ec8e3");
                }
                else if (guest.Phase == "walking_to_checkout")
                {
                    label = $"{guest.Name} ->desk";
                    color = Palette.Hex("#e6b422");
                }
                else if (guest.Phase == "waiting_checkout")
                {
                    int waitLeft = Mathf.Max(0, Mathf.CeilToInt(guest.WaitRemainingHours ?? 0));
                    label = guest.UpsetCheckout ? $"{guest.Name} upset" : $"{guest.Name} out {waitLeft}h";
                    color = guest.UpsetCheckout ? Palette.Hex("#ff8f8f") : Palette.Accent;
                }

                float lookX = guest.Phase == "waiting_checkout" ? layout.FrontDesk.X : float.NaN;
                float lookY = guest.Phase == "waiting_checkout" ? layout.FrontDesk.Y : float.NaN;
                if (guest.Phase != "waiting_checkout") PathLook(guest, ref lookX, ref lookY);
                PlaceCharacter(used++, guest.X, guest.Y, color, label, lookX, lookY, guest.FootY);
            }

            foreach (var person in staff)
            {
                if (person == null) continue;
                string label = person.Name;
                if (person.Phase == "waiting_pay") label = $"{person.Name} pay ${person.WagesOwed}";
                else if (person.Phase == "to_desk") label = $"{person.Name} ->pay";
                else if (person.PaydayDue) label = $"{person.Name} payday";

                float lookX = float.NaN;
                float lookY = float.NaN;
                if (person.Phase == "waiting_pay")
                {
                    lookX = layout.FrontDesk.X;
                    lookY = layout.FrontDesk.Y;
                }
                else
                {
                    PathLook(person, ref lookX, ref lookY);
                    if (float.IsNaN(lookX)) StaffRoomLook(person, ref lookX, ref lookY);
                }

                PlaceCharacter(used++, person.X, person.Y, Palette.Hex(person.Color), label, lookX, lookY, person.FootY);
            }

            for (int i = used; i < characters.Count; i++) characters[i].Model.GameObject.SetActive(false);

            int carUsed = 0;
            foreach (var car in state.Cars)
            {
                PlaceCar(carUsed++, car);
            }

            for (int i = carUsed; i < carViews.Count; i++) carViews[i].Root.gameObject.SetActive(false);

            if (hint != null) hint.text = HintText(state, staff);
        }

        public void SetInspect(bool enabled, string hoverLine, Vector3? worldPoint)
        {
            if (inspectBanner != null)
            {
                inspectBanner.gameObject.SetActive(enabled);
                inspectBanner.text = enabled
                    ? (string.IsNullOrEmpty(hoverLine)
                        ? "INSPECT (X) — click a spot (cursor unlocked) to pin it"
                        : hoverLine)
                    : "";
            }

            if (pinReadout != null)
            {
                pinReadout.gameObject.SetActive(enabled && !string.IsNullOrEmpty(hoverLine));
                pinReadout.text = hoverLine ?? "";
            }

            if (pinMarker != null)
            {
                pinMarker.gameObject.SetActive(enabled && worldPoint.HasValue);
                if (worldPoint.HasValue) pinMarker.position = worldPoint.Value;
            }
        }

        void BuildGround()
        {
            var lotRect = new Rect(-80, -80, layout.Width + 160, layout.Height + 160);
            // Keep the dirt lot outside the inn. Drawing it under the building
            // filled the stair well with a solid slab, so the steps vanished.
            SlabWithHole("Lot", lotRect, layout.Building, 0.02f, 0.04f, Palette.FloorColor(8), false);
            var well = Pad(layout.Stairs, 4f);
            var indoor = layout.Floor.Content;
            // Edge strip stays outside the hallway walls as a foundation/eave.
            SlabWithHole("Foundation", layout.Building, indoor, 0.025f, 0.05f, Palette.Hex("#3a3d42"), false);
            SlabWithHole("BuildingFloor", indoor, well, 0.03f, 0.06f, Palette.Corridor);
            SlabWithHole("Ceiling", layout.Building, well, WorldScale.CeilingY, 0.08f, Palette.Hex("#1a2030"));
            if (layout.Basement.W > 0)
            {
                SlabWithHole(
                    "BasementCeiling",
                    layout.Basement,
                    well,
                    WorldScale.BasementFloorY + WorldScale.WallHeight + 0.12f,
                    0.06f,
                    Palette.Hex("#1a2030"));
            }
        }

        void BuildInteriors()
        {
            foreach (var area in layout.Floor.Areas)
            {
                if (area.Level < 0) continue;
                if (area.Kind == AreaKind.Corridor)
                {
                    Box(area.Id, area.Rect, 0.05f, 0.08f, Palette.Corridor);
                    HallRunner(area);
                }
                else if (area.Kind == AreaKind.Lobby)
                {
                    SlabWithHole(
                        area.Id + "-floor",
                        Inset(area.Rect, layout.Tile),
                        Pad(layout.Stairs, 2f),
                        0.05f,
                        0.08f,
                        Palette.LobbyFloor);
                    Walls(area.Id, area.Rect, area.Doors, Palette.LobbyWall);
                }
                else if (area.Kind == AreaKind.Office)
                {
                    Box(area.Id + "-floor", Inset(area.Rect, layout.Tile), 0.11f, 0.06f, Palette.OfficeFloor);
                    Walls(area.Id, area.Rect, area.Doors, Palette.OfficeWall);
                    HangOfficeDoor(area);
                }
                else if (area.Kind == AreaKind.Stairs)
                {
                    BuildStairwell(area);
                }
                else if (area.Kind == AreaKind.Department)
                {
                    Box(area.Id + "-floor", Inset(area.Rect, layout.Tile), 0.05f, 0.08f, Palette.Hex("#4a3f52"));
                    Walls(area.Id, area.Rect, area.Doors, Palette.Wall);
                }
                else if (area.Kind == AreaKind.Parking)
                {
                    BuildParkingLot(area.Rect);
                }
            }

            foreach (var planned in layout.Floor.Rooms)
            {
                var inner = Inset(planned.Rect, layout.Tile);
                roomFloors[planned.Id] = Box($"RoomFloor-{planned.Id}", inner, 0.05f, 0.08f, Palette.Locked);
                Walls($"Room-{planned.Id}", planned.Rect, DoorList(planned.DoorOpening), Palette.Wall);
                Box(
                    $"Bed-{planned.Id}",
                    new Rect(inner.X + inner.W * 0.2f, inner.Y + inner.H * 0.25f, inner.W * 0.55f, inner.H * 0.4f),
                    0.28f,
                    0.4f,
                    Palette.Hex("#3a455c"));
            }

            BuildHallwayEnvelope();
            BuildBasement();
        }

        void BuildBasement()
        {
            float floorY = WorldScale.BasementFloorY;
            foreach (var area in layout.Floor.Areas)
            {
                if (area.Level >= 0) continue;
                if (area.Kind == AreaKind.Basement)
                {
                    Box(area.Id + "-floor", Inset(area.Rect, layout.Tile), floorY + 0.05f, 0.08f, Palette.Hex("#3a342c"));
                    Walls(area.Id, area.Rect, area.Doors, Palette.Hex("#2a2620"), floorY);
                }
                else if (area.Kind == AreaKind.Department)
                {
                    Box(area.Id + "-floor", Inset(area.Rect, layout.Tile), floorY + 0.05f, 0.08f, Palette.Hex("#4a3f52"));
                    Walls(area.Id, area.Rect, area.Doors, Palette.Wall, floorY);
                }
                else if (area.Kind == AreaKind.Storage)
                {
                    Box(area.Id + "-floor", Inset(area.Rect, 4f), floorY + 0.06f, 0.04f, Palette.Hex("#4a4034"));
                }
            }

            if (layout.Departments != null)
            {
                foreach (var dept in layout.Departments.Values)
                {
                    var r = dept.Rect;
                    Box(
                        dept.Id + "-bench",
                        new Rect(r.X + 16f, r.Y + 16f, r.W * 0.45f, 28f),
                        floorY + 0.45f,
                        0.7f,
                        Palette.Hex(dept.Accent ?? "#6a5a48"));
                    Box(
                        dept.Id + "-crate",
                        new Rect(r.X + r.W * 0.55f, r.Y + r.H * 0.35f, 36f, 28f),
                        floorY + 0.32f,
                        0.5f,
                        Palette.Hex("#5a4030"));
                }
            }

            var store = layout.Basement.W > 0 ? layout.Basement : layout.Lobby;
            Box("StoreShelfA", new Rect(store.X + 40f, store.Y + 40f, 90f, 22f), floorY + 0.55f, 1f, Palette.Hex("#4a3a28"));
            Box("StoreShelfB", new Rect(store.X + store.W - 130f, store.Y + 40f, 90f, 22f), floorY + 0.55f, 1f, Palette.Hex("#4a3a28"));
            Box("StoreCrateA", new Rect(store.Center.X - 70f, store.Center.Y - 20f, 36f, 28f), floorY + 0.28f, 0.46f, Palette.Hex("#6a5038"));
            Box("StoreCrateB", new Rect(store.Center.X + 20f, store.Center.Y + 10f, 40f, 30f), floorY + 0.34f, 0.58f, Palette.Hex("#5a4030"));
        }

        void BuildStairwell(FloorArea area)
        {
            // Office already owns the shared east wall. Drawing it twice made the
            // stairwell flicker. Keep north/south/west only, and keep steps inside
            // the wall thickness.
            Walls(area.Id, area.Rect, area.Doors, Palette.OfficeWall, 0f, "east");
            int steps = 10;
            float pad = layout.Tile;
            float innerX = area.Rect.X + pad;
            float innerY = area.Rect.Y + pad;
            float innerW = area.Rect.W - pad * 2f;
            float innerH = area.Rect.H - pad * 2f;
            float stepW = innerW / steps;
            for (int i = 0; i < steps; i++)
            {
                float t = 1f - (i + 0.5f) / steps;
                float y = -t * WorldScale.FloorDepth;
                Box(
                    $"Stair-{i}",
                    new Rect(innerX + i * stepW, innerY, stepW, innerH),
                    y + 0.08f,
                    0.16f,
                    Palette.Hex(i % 2 == 0 ? "#5a5048" : "#4a443c"));
            }
        }

        void BuildFurniture()
        {
            var desk = layout.FrontDesk;
            Box(
                "Desk",
                new Rect(desk.X - desk.W / 2f, desk.Y - desk.H / 2f, desk.W, desk.H),
                0.5f,
                1f,
                Palette.Hex("#5a4030"));

            var radio = layout.LobbyRadio;
            Box(
                "Radio",
                new Rect(radio.X - radio.W / 2f, radio.Y - radio.H / 2f, radio.W, radio.H),
                1.05f,
                0.28f,
                Palette.RadioBody);

            var paper = layout.Newspaper;
            Box(
                "Paper",
                new Rect(paper.X - paper.W / 2f, paper.Y - paper.H / 2f, paper.W, paper.H),
                1.02f,
                0.04f,
                Palette.Paper);

            var phone = layout.DeskPhone;
            if (phone != null)
            {
                Box(
                    "PhoneBase",
                    new Rect(phone.X - phone.W / 2f, phone.Y - phone.H / 2f, phone.W, phone.H),
                    1.02f,
                    0.08f,
                    Palette.Hex("#1d2430"));
                Box(
                    "PhoneHandset",
                    new Rect(phone.X - 10f, phone.Y - 4f, 20f, 8f),
                    1.12f,
                    0.06f,
                    Palette.Hex("#c45c2a"));
            }

            var deskPc = layout.DeskPc;
            if (deskPc != null)
            {
                Box(
                    "DeskPc",
                    new Rect(deskPc.X - deskPc.W / 2f, deskPc.Y - deskPc.H / 2f, deskPc.W, deskPc.H),
                    1.05f,
                    0.32f,
                    Palette.Hex("#1a2030"));
                Box(
                    "DeskPcScreen",
                    new Rect(deskPc.X - 10f, deskPc.Y - 4f, 20f, 8f),
                    1.28f,
                    0.18f,
                    Palette.Hex("#7dffb2"));
            }

            var lobby = layout.Lobby;
            float sitX = desk.X;
            float sitY = desk.Y + 100f;
            Box("Rug", new Rect(sitX - 90, sitY - 50, 180, 100), 0.055f, 0.02f, Palette.Hex("#4a2f2a"));
            Box("ChairWest", new Rect(sitX - 72, sitY - 28, 28, 56), 0.28f, 0.42f, Palette.Hex("#5c4a3a"));
            Box("ChairEast", new Rect(sitX + 44, sitY - 28, 28, 56), 0.28f, 0.42f, Palette.Hex("#5c4a3a"));
            Box("CoffeeTable", new Rect(sitX - 18, sitY - 18, 36, 36), 0.32f, 0.28f, Palette.Hex("#3a2a20"));
            Box("PlantSW", new Rect(lobby.X + 24, lobby.Y + lobby.H - 56, 22, 22), 0.55f, 1f, Palette.Hex("#2f5a3a"));
            Box("PlantSE", new Rect(lobby.X + lobby.W - 46, lobby.Y + lobby.H - 56, 22, 22), 0.55f, 1f, Palette.Hex("#2f5a3a"));

            if (layout.Office != null)
            {
                var office = layout.Office.Rect;
                float pcX = office.X + office.W * 0.72f;
                float pcY = office.Center.Y;
                Box(
                    "PcDesk",
                    new Rect(pcX - 28, pcY - 18, 56, 36),
                    0.45f,
                    0.9f,
                    Palette.Hex("#2a3142"));
                Box(
                    "Pc",
                    new Rect(pcX - 16, pcY - 8, 32, 16),
                    1.05f,
                    0.35f,
                    Palette.Hex("#1a2030"));
                Box(
                    "PcScreen",
                    new Rect(pcX - 12, pcY - 5, 24, 6),
                    1.28f,
                    0.22f,
                    Palette.Hex("#7dffb2"));
            }

            var sign = layout.VacancySign;
            vacancySign = Box(
                "Sign",
                new Rect(sign.X - sign.W / 2f, sign.Y - 8f, sign.W, 16f),
                1.2f,
                1.8f,
                Palette.Hex("#2f6b3a"));
            Box(
                "SignPost",
                new Rect(sign.X - 4f, sign.Y - 4f, 8f, 8f),
                0.55f,
                1.1f,
                Palette.Hex("#2a2430"));
        }

        void BuildLights()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Palette.Hex("#2a3148");
            RenderSettings.fog = true;
            RenderSettings.fogColor = Palette.Hex("#141820");
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 8f;
            RenderSettings.fogEndDistance = 42f;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root, false);
            sunGo.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.28f;
            sun.color = new Color(0.75f, 0.82f, 1f);
            sun.shadows = LightShadows.None;

            PointLight("LobbyLight", layout.Lobby.Center.X, layout.Lobby.Center.Y, 2.4f, 14f, 1.5f, new Color(1f, 0.86f, 0.7f));
            PointLight("SeatingLight", layout.FrontDesk.X, layout.FrontDesk.Y + 100f, 2.2f, 10f, 1.1f, new Color(1f, 0.82f, 0.62f));
            PointLight("DeskLight", layout.FrontDesk.X, layout.FrontDesk.Y, 2.2f, 9f, 1.2f, new Color(1f, 0.9f, 0.75f));
            if (layout.Office != null)
            {
                PointLight("OfficeLight", layout.Office.X, layout.Office.Y, 2.1f, 8f, 1.05f, new Color(0.7f, 0.95f, 0.85f));
            }

            if (layout.Stairs.W > 0)
            {
                PointLight(
                    "StairLight",
                    layout.Stairs.Center.X,
                    layout.Stairs.Center.Y,
                    1.4f,
                    7f,
                    0.9f,
                    new Color(0.95f, 0.82f, 0.55f));
            }

            if (layout.Basement.W > 0)
            {
                PointLight(
                    "BasementLight",
                    layout.Basement.Center.X,
                    layout.Basement.Center.Y,
                    WorldScale.BasementFloorY + 2.2f,
                    14f,
                    1.15f,
                    new Color(0.85f, 0.72f, 0.5f));
            }

            if (layout.Parking.W > 0)
            {
                PointLight(
                    "ParkingLight",
                    layout.Parking.Center.X,
                    layout.Parking.Center.Y,
                    3.2f,
                    16f,
                    1.15f,
                    new Color(0.85f, 0.88f, 1f));
            }

            int corridorLights = 0;
            foreach (var area in layout.Floor.Areas)
            {
                if (area.Kind != AreaKind.Corridor || area.Level < 0) continue;
                if (area.Rect.W < 80f && area.Rect.H < 80f) continue;
                bool eastWest = area.Rect.W >= area.Rect.H * 1.5f;
                int lamps = eastWest
                    ? Mathf.Clamp(Mathf.RoundToInt(area.Rect.W / 220f), 1, 4)
                    : 1;
                for (int i = 0; i < lamps && corridorLights < 14; i++)
                {
                    float t = lamps == 1 ? 0.5f : (i + 1f) / (lamps + 1f);
                    float lx = eastWest
                        ? area.Rect.X + area.Rect.W * t
                        : area.Rect.Center.X;
                    float ly = eastWest
                        ? area.Rect.Center.Y
                        : area.Rect.Y + area.Rect.H * t;
                    PointLight(
                        $"CorridorLight-{corridorLights}",
                        lx,
                        ly,
                        2.35f,
                        11f,
                        1.05f,
                        new Color(0.75f, 0.85f, 1f));
                    corridorLights++;
                }
            }
        }

        Text BuildHint(Transform parent)
        {
            var canvasGo = new GameObject("LookOverlay", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);

            var cross = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            cross.transform.SetParent(canvasGo.transform, false);
            var crossRt = cross.GetComponent<RectTransform>();
            crossRt.anchorMin = crossRt.anchorMax = new Vector2(0.5f, 0.5f);
            crossRt.sizeDelta = new Vector2(4, 4);
            cross.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.55f);

            var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            hintGo.transform.SetParent(canvasGo.transform, false);
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0, 28);
            hintRt.sizeDelta = new Vector2(1200, 36);
            var text = hintGo.GetComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Palette.Text;
            text.text = "";
            return text;
        }

        Text BuildInspectBanner(Transform parent)
        {
            var go = new GameObject("InspectBanner", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -18);
            rt.sizeDelta = new Vector2(1400, 40);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.alignment = TextAnchor.UpperCenter;
            text.color = Palette.Accent;
            text.text = "";
            go.SetActive(false);
            return text;
        }

        Text BuildPinReadout(Transform parent)
        {
            var go = new GameObject("PinReadout", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -18);
            rt.sizeDelta = new Vector2(1100, 48);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = 16;
            text.alignment = TextAnchor.UpperCenter;
            text.color = Palette.Accent;
            text.text = "";
            go.SetActive(false);
            return text;
        }

        Transform BuildPinMarker()
        {
            var go = MeshObject(
                "PinMarker",
                root,
                cubeMesh,
                Vector3.zero,
                new Vector3(0.22f, 0.08f, 0.22f),
                Palette.Accent,
                false);
            go.SetActive(false);
            return go.transform;
        }

        const float MoveYawThresholdSq = 0.04f;
        const float TeleportSq = 400f;
        const float AimAheadSq = 36f;
        const float TurnDegreesPerSecond = 360f;

        void PlaceCharacter(int index, float x, float y, Color color, string label, float lookX = float.NaN, float lookY = float.NaN, float footY = 0f)
        {
            while (characters.Count <= index)
            {
                var model = CharacterModel.BuildNpc(root, $"Char{characters.Count}", Mat, characters.Count % 2 == 1);
                var tm = new GameObject("Label").AddComponent<TextMesh>();
                tm.transform.SetParent(model.Root, false);
                tm.transform.localPosition = new Vector3(0f, 1.85f, 0f);
                tm.font = font;
                tm.fontSize = 48;
                tm.characterSize = 0.055f;
                tm.anchor = TextAnchor.LowerCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = Palette.Text;
                characters.Add(new CharacterView
                {
                    Model = model,
                    LastX = x,
                    LastY = y,
                    Yaw = 0f,
                    HasFacing = false,
                    Label = tm
                });
            }

            var view = characters[index];
            bool wasHidden = !view.Model.GameObject.activeSelf;
            view.Model.GameObject.SetActive(true);
            view.Model.Recolor(color);
            view.Label.text = label;

            float dx = x - view.LastX;
            float dy = y - view.LastY;
            float movedSq = dx * dx + dy * dy;
            bool teleport = wasHidden || movedSq > TeleportSq;
            bool moving = !teleport && movedSq > MoveYawThresholdSq;

            float targetYaw = view.Yaw;
            bool haveTarget = false;
            bool useLook = TryLayoutYaw(lookX - x, lookY - y, out var lookYaw);
            if (useLook && moving && LookSq(lookX - x, lookY - y) <= AimAheadSq) useLook = false;
            if (useLook)
            {
                targetYaw = lookYaw;
                haveTarget = true;
            }
            else if (moving && TryLayoutYaw(dx, dy, out var moveYaw))
            {
                targetYaw = moveYaw;
                haveTarget = true;
            }

            if (haveTarget)
            {
                view.Yaw = !view.HasFacing || teleport
                    ? targetYaw
                    : Mathf.MoveTowardsAngle(view.Yaw, targetYaw, TurnDegreesPerSecond * Time.deltaTime);
                view.HasFacing = true;
            }

            view.LastX = x;
            view.LastY = y;
            view.Model.Place(x, y, view.Yaw, Time.deltaTime, footY);

            if (playerCamera != null)
            {
                var toCam = view.Label.transform.position - playerCamera.transform.position;
                if (toCam.sqrMagnitude > 0.01f)
                {
                    view.Label.transform.rotation = Quaternion.LookRotation(toCam);
                }
            }
        }

        static void PathLook(IMover mover, ref float lookX, ref float lookY)
        {
            if (!float.IsNaN(lookX) || mover?.Path == null || mover.Path.Count == 0) return;
            lookX = mover.Path[0].X;
            lookY = mover.Path[0].Y;
        }

        void StaffRoomLook(StaffNpc person, ref float lookX, ref float lookY)
        {
            if (!float.IsNaN(lookX) || person == null) return;
            Room room = person.ActiveTask?.Room ?? person.TargetRoom;
            if (room == null || layout.RoomCenters == null || room.Id < 1 || room.Id > layout.RoomCenters.Count)
            {
                return;
            }

            if (person.Phase != "working" && person.Phase != "enter_room") return;
            var center = layout.RoomCenters[room.Id - 1];
            lookX = center.X;
            lookY = center.Y;
        }

        static float LookSq(float dx, float dy) => dx * dx + dy * dy;

        static bool TryLayoutYaw(float dx, float dy, out float yaw)
        {
            yaw = 0f;
            if (float.IsNaN(dx) || float.IsNaN(dy)) return false;
            if (dx * dx + dy * dy < 0.01f) return false;
            yaw = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
            return true;
        }

        void PlaceCar(int index, GuestCar car)
        {
            while (carViews.Count <= index)
            {
                carViews.Add(BuildGuestCar());
            }

            var view = carViews[index];
            view.Root.gameObject.SetActive(true);
            Color color = Palette.Hex(string.IsNullOrEmpty(car.Color) ? "#4a5a6a" : car.Color);
            if (view.Body != null) view.Body.sharedMaterial = Mat(color);

            float cx = car.X + HotelLayout.ParkedCarWidth / 2f;
            float cy = car.Y + HotelLayout.ParkedCarHeight / 2f;
            view.Root.position = WorldScale.ToWorld(cx, cy, 0f);

            if (float.IsNaN(view.LastX))
            {
                view.LastX = car.X;
                view.LastY = car.Y;
                view.Yaw = car.Stage == "parked" ? 0f : 90f;
            }

            float dx = car.X - view.LastX;
            float dy = car.Y - view.LastY;
            if (car.Stage == "parked")
            {
                view.Yaw = 0f;
            }
            else if (dx * dx + dy * dy > 0.4f)
            {
                view.Yaw = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg + 90f;
            }

            view.LastX = car.X;
            view.LastY = car.Y;
            view.Root.rotation = Quaternion.Euler(0f, view.Yaw, 0f);
        }

        CarView BuildGuestCar()
        {
            var go = new GameObject("GuestCar");
            go.transform.SetParent(root, false);
            var body = MeshObject(
                "Body",
                go.transform,
                cubeMesh,
                go.transform.position,
                WorldScale.Size(HotelLayout.ParkedCarWidth, 0.7f, HotelLayout.ParkedCarHeight),
                Palette.Hex("#4a5a6a"),
                false);
            body.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            var cabin = MeshObject(
                "Cabin",
                go.transform,
                cubeMesh,
                go.transform.position,
                WorldScale.Size(22f, 0.45f, 18f),
                Palette.Hex("#1a2030"),
                false);
            cabin.transform.localPosition = new Vector3(-2f / WorldScale.UnitsPerMeter, 0.95f, 0f);
            return new CarView
            {
                Root = go.transform,
                Body = body.GetComponent<Renderer>(),
                LastX = float.NaN,
                LastY = float.NaN,
                Yaw = 90f
            };
        }

        void HallRunner(FloorArea area)
        {
            var rect = area.Rect;
            if (rect.W < 80f || rect.H < 36f) return;
            if (rect.H > 120f && rect.H > rect.W) return;
            float padX = Mathf.Min(16f, rect.W * 0.08f);
            float padY = rect.H * 0.28f;
            Box(
                area.Id + "-runner",
                new Rect(rect.X + padX, rect.Y + padY, rect.W - padX * 2f, rect.H - padY * 2f),
                0.062f,
                0.02f,
                Palette.Hex("#3a455c"));
        }

        void BuildHallwayEnvelope()
        {
            var content = layout.Floor.Content;
            if (content.W <= 0f || content.H <= 0f) return;
            var color = Palette.Hex("#2c3348");
            AddSide("Envelope-N", "north", content, null, color, 0f);
            AddSide("Envelope-W", "west", content, null, color, 0f);
            AddSide("Envelope-E", "east", content, null, color, 0f);
        }

        void Walls(string name, Rect rect, List<Door> doors, Color color, float yBottom = 0f, string skipSide = null)
        {
            if (skipSide != "north") AddSide(name + "-N", "north", rect, doors, color, yBottom);
            if (skipSide != "south") AddSide(name + "-S", "south", rect, doors, color, yBottom);
            if (skipSide != "west") AddSide(name + "-W", "west", rect, doors, color, yBottom);
            if (skipSide != "east") AddSide(name + "-E", "east", rect, doors, color, yBottom);
        }

        void AddSide(string name, string side, Rect rect, List<Door> doors, Color color, float yBottom)
        {
            var gaps = new List<Door>();
            if (doors != null)
            {
                foreach (var candidate in doors)
                {
                    if (candidate != null && candidate.Side == side) gaps.Add(candidate);
                }
            }

            bool horiz = side == "north" || side == "south";
            float start = horiz ? rect.X : rect.Y;
            float end = horiz ? rect.X + rect.W : rect.Y + rect.H;
            if (gaps.Count == 0)
            {
                WallSpan(name, side, rect, start, end, color, yBottom);
                return;
            }

            gaps.Sort((a, b) =>
            {
                float ac = horiz ? a.Center.X : a.Center.Y;
                float bc = horiz ? b.Center.X : b.Center.Y;
                return ac.CompareTo(bc);
            });

            float cursor = start;
            for (int i = 0; i < gaps.Count; i++)
            {
                var door = gaps[i];
                float along = horiz ? door.Center.X : door.Center.Y;
                float gap0 = along - door.Width / 2f;
                float gap1 = gap0 + door.Width;
                if (gap0 > cursor + 2f) WallSpan(name + i + "a", side, rect, cursor, gap0, color, yBottom);
                FrameOpening(name + i + "door", door, side, rect, color, yBottom);
                if (gap1 > cursor) cursor = gap1;
            }

            if (cursor < end - 2f) WallSpan(name + "z", side, rect, cursor, end, color, yBottom);
        }

        void WallSpan(string name, string side, Rect rect, float from, float to, Color color, float yBottom)
        {
            float len = to - from;
            if (len < 2f) return;
            float thick = layout.Tile;
            Rect wallRect;
            if (side == "north") wallRect = new Rect(from, rect.Y, len, thick);
            else if (side == "south") wallRect = new Rect(from, rect.Y + rect.H - thick, len, thick);
            else if (side == "west") wallRect = new Rect(rect.X, from, thick, len);
            else wallRect = new Rect(rect.X + rect.W - thick, from, thick, len);

            Box(name, wallRect, yBottom + WorldScale.WallHeight * 0.5f, WorldScale.WallHeight, color);
        }

        void FrameOpening(string name, Door door, string side, Rect rect, Color color, float yBottom)
        {
            float thick = layout.Tile;
            bool horiz = side == "north" || side == "south";
            float along = horiz ? door.Center.X : door.Center.Y;
            float gap0 = along - door.Width / 2f;
            Rect opening;
            if (side == "north") opening = new Rect(gap0, rect.Y, door.Width, thick);
            else if (side == "south") opening = new Rect(gap0, rect.Y + rect.H - thick, door.Width, thick);
            else if (side == "west") opening = new Rect(rect.X, gap0, thick, door.Width);
            else opening = new Rect(rect.X + rect.W - thick, gap0, thick, door.Width);

            float lintelH = 0.55f;
            Box(
                name + "-lintel",
                opening,
                yBottom + WorldScale.WallHeight - lintelH * 0.5f,
                lintelH,
                color);
            Box(name + "-sill", opening, yBottom + 0.055f, 0.05f, Palette.Doorway);
        }

        void HangOfficeDoor(FloorArea office)
        {
            if (office?.Doors == null) return;
            Door south = null;
            foreach (var door in office.Doors)
            {
                if (door != null && door.Side == "south") south = door;
            }

            if (south == null) return;

            float thick = 5f;
            float leaf = south.Width * 0.9f;
            float hingeX = south.Center.X - south.Width / 2f + 2f;
            float y0 = south.Center.Y - leaf;
            Box(
                "OfficeDoor",
                new Rect(hingeX, y0, thick, leaf),
                1.15f,
                2.2f,
                Palette.Hex("#5a4030"));
            Box(
                "OfficeDoorKnob",
                new Rect(hingeX + thick, y0 + leaf * 0.45f, 3f, 4f),
                1.05f,
                0.08f,
                Palette.Hex("#c4a574"));
        }

        void BuildParkingLot(Rect lot)
        {
            Box(lotId("Asphalt"), lot, 0.04f, 0.05f, Palette.Hex("#2a2c30"));
            float driveW = HotelLayout.ParkingDriveWidth;
            float driveX = lot.X + (lot.W - driveW) / 2f;
            Box(lotId("Drive"), new Rect(driveX, lot.Y, driveW, lot.H), 0.055f, 0.03f, Palette.Hex("#3a3d42"));

            float stallH = HotelLayout.ParkingStallHeight;
            float gap = HotelLayout.ParkingStallGap;
            float westW = driveX - lot.X - 14f;
            float eastX = driveX + driveW + 8f;
            float eastW = lot.X + lot.W - eastX - 8f;
            int stalls = 4;
            for (int i = 0; i < stalls; i++)
            {
                float y = lot.Y + 16f + i * (stallH + gap);
                Box(lotId($"LineW-{i}"), new Rect(lot.X + 8f, y, westW, 2f), 0.07f, 0.02f, Palette.Hex("#c4c0b0"));
                Box(lotId($"LineE-{i}"), new Rect(eastX, y, eastW, 2f), 0.07f, 0.02f, Palette.Hex("#c4c0b0"));
            }
        }

        static string lotId(string name) => "Parking-" + name;

        static Rect Pad(Rect rect, float pad)
        {
            if (rect.W <= 0f || rect.H <= 0f) return rect;
            return new Rect(rect.X - pad, rect.Y - pad, rect.W + pad * 2f, rect.H + pad * 2f);
        }

        void SlabWithHole(string name, Rect outer, Rect hole, float yCenter, float height, Color color, bool collider = true)
        {
            if (hole.W <= 0f || hole.H <= 0f ||
                hole.X >= outer.X + outer.W || hole.X + hole.W <= outer.X ||
                hole.Y >= outer.Y + outer.H || hole.Y + hole.H <= outer.Y)
            {
                Box(name, outer, yCenter, height, color, collider);
                return;
            }

            float holeX0 = Mathf.Max(outer.X, hole.X);
            float holeX1 = Mathf.Min(outer.X + outer.W, hole.X + hole.W);
            float holeY0 = Mathf.Max(outer.Y, hole.Y);
            float holeY1 = Mathf.Min(outer.Y + outer.H, hole.Y + hole.H);
            if (holeY0 > outer.Y + 2f)
            {
                Box(name + "-N", new Rect(outer.X, outer.Y, outer.W, holeY0 - outer.Y), yCenter, height, color, collider);
            }

            if (holeY1 < outer.Y + outer.H - 2f)
            {
                Box(name + "-S", new Rect(outer.X, holeY1, outer.W, outer.Y + outer.H - holeY1), yCenter, height, color, collider);
            }

            if (holeX0 > outer.X + 2f)
            {
                Box(name + "-W", new Rect(outer.X, holeY0, holeX0 - outer.X, holeY1 - holeY0), yCenter, height, color, collider);
            }

            if (holeX1 < outer.X + outer.W - 2f)
            {
                Box(name + "-E", new Rect(holeX1, holeY0, outer.X + outer.W - holeX1, holeY1 - holeY0), yCenter, height, color, collider);
            }
        }

        Renderer Box(string name, Rect rect, float yCenter, float height, Color color, bool collider = true)
        {
            var go = MeshObject(
                name,
                root,
                cubeMesh,
                WorldScale.ToWorld(rect.X + rect.W / 2f, rect.Y + rect.H / 2f, yCenter),
                WorldScale.Size(rect.W, height, rect.H),
                color,
                collider);
            return go.GetComponent<Renderer>();
        }

        void PointLight(string name, float layoutX, float layoutY, float height, float range, float intensity, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = WorldScale.ToWorld(layoutX, layoutY, height);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
        }

        GameObject MeshObject(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Color color, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mat(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        Material Mat(Color color)
        {
            if (materials.TryGetValue(color, out var existing)) return existing;
            var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.12f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            materials[color] = material;
            return material;
        }

        static List<Door> DoorList(Door door)
        {
            var list = new List<Door>();
            if (door != null) list.Add(door);
            return list;
        }

        static Rect Union(Rect a, Rect b)
        {
            if (a.W <= 0f || a.H <= 0f) return b;
            if (b.W <= 0f || b.H <= 0f) return a;
            float x0 = Mathf.Min(a.X, b.X);
            float y0 = Mathf.Min(a.Y, b.Y);
            float x1 = Mathf.Max(a.X + a.W, b.X + b.W);
            float y1 = Mathf.Max(a.Y + a.H, b.Y + b.H);
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        static Rect Inset(Rect rect, float tile)
        {
            return new Rect(rect.X + tile, rect.Y + tile, rect.W - tile * 2f, rect.H - tile * 2f);
        }

        static string HintText(GameState state, List<StaffNpc> staff)
        {
            foreach (var guest in state.ActiveGuests)
            {
                if (guest.Phase == "waiting_checkout") return "Press E to check out guests";
            }

            foreach (var person in staff)
            {
                if (person != null && (person.Phase == "waiting_pay" || person.Phase == "to_desk"))
                {
                    return $"Press E to pay {person.Name} ${person.WagesOwed}";
                }
            }

            if (Economy.FirstAtDesk(state) != null) return "Press E on the phone to check them in";
            return "Hold RMB to look · WASD walk · E interact · X pin · Esc pause · office door behind the desk to the basement";
        }

        static Mesh BuildCube()
        {
            var mesh = new Mesh { name = "VacancyCube" };
            var v = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f),
                new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(-0.5f, -0.5f,  0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3( 0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f,  0.5f)
            };
            var n = new Vector3[]
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                Vector3.down, Vector3.down, Vector3.down, Vector3.down,
                Vector3.left, Vector3.left, Vector3.left, Vector3.left,
                Vector3.right, Vector3.right, Vector3.right, Vector3.right
            };
            var tris = new List<int>
            {
                0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7, 8, 9, 10, 8, 10, 11,
                12, 13, 14, 12, 14, 15, 16, 17, 18, 16, 18, 19, 20, 21, 22, 20, 22, 23
            };
            int count = tris.Count;
            for (int i = 0; i < count; i += 3)
            {
                tris.Add(tris[i]);
                tris.Add(tris[i + 2]);
                tris.Add(tris[i + 1]);
            }

            mesh.vertices = v;
            mesh.normals = n;
            mesh.SetTriangles(tris, 0);
            return mesh;
        }

        static Mesh BuildCylinder(int sides)
        {
            var mesh = new Mesh { name = "VacancyCylinder" };
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = (i % sides) / (float)sides * Mathf.PI * 2f;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(new Vector3(n.x * 0.5f, -0.5f, n.z * 0.5f));
                verts.Add(new Vector3(n.x * 0.5f, 0.5f, n.z * 0.5f));
                norms.Add(n);
                norms.Add(n);
            }

            for (int i = 0; i < sides; i++)
            {
                int i0 = i * 2;
                tris.Add(i0);
                tris.Add(i0 + 1);
                tris.Add(i0 + 2);
                tris.Add(i0 + 1);
                tris.Add(i0 + 3);
                tris.Add(i0 + 2);
            }

            int bottom = verts.Count;
            verts.Add(new Vector3(0f, -0.5f, 0f));
            norms.Add(Vector3.down);
            int top = verts.Count;
            verts.Add(new Vector3(0f, 0.5f, 0f));
            norms.Add(Vector3.up);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                int b0 = verts.Count;
                verts.Add(new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f));
                norms.Add(Vector3.down);
                int b1 = verts.Count;
                verts.Add(new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f));
                norms.Add(Vector3.down);
                tris.Add(bottom);
                tris.Add(b1);
                tris.Add(b0);

                int t0 = verts.Count;
                verts.Add(new Vector3(Mathf.Cos(a0) * 0.5f, 0.5f, Mathf.Sin(a0) * 0.5f));
                norms.Add(Vector3.up);
                int t1 = verts.Count;
                verts.Add(new Vector3(Mathf.Cos(a1) * 0.5f, 0.5f, Mathf.Sin(a1) * 0.5f));
                norms.Add(Vector3.up);
                tris.Add(top);
                tris.Add(t0);
                tris.Add(t1);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            return mesh;
        }

        sealed class CharacterView
        {
            public CharacterModel Model;
            public TextMesh Label;
            public float LastX;
            public float LastY;
            public float Yaw;
            public bool HasFacing;
        }

        sealed class CarView
        {
            public Transform Root;
            public Renderer Body;
            public float LastX;
            public float LastY;
            public float Yaw;
        }
    }
}

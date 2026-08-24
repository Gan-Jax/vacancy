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
        CharacterModel playerBody;
        readonly Text hint;
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
            for (int i = 0; i < state.WaitingGuests.Count; i++)
            {
                var guest = state.WaitingGuests[i];
                var slot = layout.CheckInLineSlot(i);
                string tag = i == 0 ? $"{guest.Name} ★" : guest.Name;
                PlaceCharacter(used++, slot.X, slot.Y, Palette.Hex("#e8a0bf"), tag, layout.FrontDesk.X, layout.FrontDesk.Y);
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
                PlaceCharacter(used++, guest.X, guest.Y, color, label, lookX, lookY);
            }

            foreach (var person in staff)
            {
                if (person == null) continue;
                string label = person.Name;
                if (person.Phase == "waiting_pay") label = $"{person.Name} pay ${person.WagesOwed}";
                else if (person.Phase == "to_desk") label = $"{person.Name} ->pay";
                else if (person.PaydayDue) label = $"{person.Name} payday";
                PlaceCharacter(used++, person.X, person.Y, Palette.Hex(person.Color), label);
            }

            for (int i = used; i < characters.Count; i++) characters[i].Model.GameObject.SetActive(false);

            if (hint != null) hint.text = HintText(state, staff);
        }

        void BuildGround()
        {
            Box(
                "Lot",
                new Rect(-80, -80, layout.Width + 160, layout.Height + 160),
                0.02f,
                0.04f,
                Palette.FloorColor(8));
            Box("BuildingFloor", layout.Building, 0.03f, 0.06f, Palette.Corridor);
            Box("Ceiling", layout.Building, WorldScale.CeilingY, 0.08f, Palette.Hex("#1a2030"));
        }

        void BuildInteriors()
        {
            foreach (var area in layout.Floor.Areas)
            {
                if (area.Kind == AreaKind.Corridor)
                {
                    Box(area.Id, area.Rect, 0.05f, 0.08f, Palette.Corridor);
                }
                else if (area.Kind == AreaKind.Lobby)
                {
                    Box(area.Id + "-floor", Inset(area.Rect, layout.Tile), 0.05f, 0.08f, Palette.LobbyFloor);
                    Walls(area.Id, area.Rect, area.Doors, Palette.LobbyWall);
                }
                else if (area.Kind == AreaKind.Office)
                {
                    Box(area.Id + "-floor", Inset(area.Rect, layout.Tile), 0.05f, 0.08f, Palette.OfficeFloor);
                    Walls(area.Id, area.Rect, area.Doors, Palette.OfficeWall);
                }
                else if (area.Kind == AreaKind.Department)
                {
                    Box(area.Id + "-floor", Inset(area.Rect, layout.Tile), 0.05f, 0.08f, Palette.Hex("#4a3f52"));
                    Walls(area.Id, area.Rect, area.Doors, Palette.Wall);
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

            int corridorLights = 0;
            foreach (var area in layout.Floor.Areas)
            {
                if (area.Kind != AreaKind.Corridor) continue;
                if (corridorLights >= 6) break;
                PointLight(
                    $"CorridorLight-{corridorLights}",
                    area.Rect.Center.X,
                    area.Rect.Center.Y,
                    2.35f,
                    11f,
                    1.05f,
                    new Color(0.75f, 0.85f, 1f));
                corridorLights++;
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

        void PlaceCharacter(int index, float x, float y, Color color, string label, float lookX = float.NaN, float lookY = float.NaN)
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
                    Label = tm
                });
            }

            var view = characters[index];
            view.Model.GameObject.SetActive(true);
            view.Model.Recolor(color);
            view.Label.text = label;

            float dx = x - view.LastX;
            float dy = y - view.LastY;
            if (dx * dx + dy * dy > 0.4f)
            {
                view.Yaw = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
            }
            else if (!float.IsNaN(lookX))
            {
                view.Yaw = Mathf.Atan2(lookX - x, lookY - y) * Mathf.Rad2Deg;
            }

            view.LastX = x;
            view.LastY = y;
            view.Model.Place(x, y, view.Yaw, Time.deltaTime);

            if (playerCamera != null)
            {
                var toCam = view.Label.transform.position - playerCamera.transform.position;
                if (toCam.sqrMagnitude > 0.01f)
                {
                    view.Label.transform.rotation = Quaternion.LookRotation(toCam);
                }
            }
        }

        void Walls(string name, Rect rect, List<Door> doors, Color color)
        {
            AddSide(name + "-N", "north", rect, doors, color);
            AddSide(name + "-S", "south", rect, doors, color);
            AddSide(name + "-W", "west", rect, doors, color);
            AddSide(name + "-E", "east", rect, doors, color);
        }

        void AddSide(string name, string side, Rect rect, List<Door> doors, Color color)
        {
            Door door = null;
            if (doors != null)
            {
                foreach (var candidate in doors)
                {
                    if (candidate != null && candidate.Side == side) door = candidate;
                }
            }

            bool horiz = side == "north" || side == "south";
            float start = horiz ? rect.X : rect.Y;
            float end = horiz ? rect.X + rect.W : rect.Y + rect.H;
            if (door == null)
            {
                WallSpan(name, side, rect, start, end, color);
                return;
            }

            float gap0 = (horiz ? door.Center.X : door.Center.Y) - door.Width / 2f;
            float gap1 = gap0 + door.Width;
            if (gap0 > start + 2f) WallSpan(name + "a", side, rect, start, gap0, color);
            if (gap1 < end - 2f) WallSpan(name + "b", side, rect, gap1, end, color);
        }

        void WallSpan(string name, string side, Rect rect, float from, float to, Color color)
        {
            float len = to - from;
            if (len < 2f) return;
            float thick = layout.Tile;
            Rect wallRect;
            if (side == "north") wallRect = new Rect(from, rect.Y, len, thick);
            else if (side == "south") wallRect = new Rect(from, rect.Y + rect.H - thick, len, thick);
            else if (side == "west") wallRect = new Rect(rect.X, from, thick, len);
            else wallRect = new Rect(rect.X + rect.W - thick, from, thick, len);

            Box(name, wallRect, WorldScale.WallHeight * 0.5f, WorldScale.WallHeight, color);
        }

        Renderer Box(string name, Rect rect, float yCenter, float height, Color color)
        {
            var go = MeshObject(
                name,
                root,
                cubeMesh,
                WorldScale.ToWorld(rect.X + rect.W / 2f, rect.Y + rect.H / 2f, yCenter),
                WorldScale.Size(rect.W, height, rect.H),
                color);
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

        GameObject MeshObject(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Color color)
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

            if (state.WaitingGuests.Count > 0) return "Press E on the phone to check them in";
            return "Hold RMB to look · WASD walk · E interact · Esc pause";
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
        }
    }
}

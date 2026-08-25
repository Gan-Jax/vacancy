using System.Collections.Generic;
using UnityEngine;

namespace Vacancy
{
    public sealed class HotelView
    {
        readonly HotelLayout layout;
        readonly Transform root;
        readonly Sprite square;
        readonly Sprite circle;
        readonly Font font;
        readonly Dictionary<int, SpriteRenderer> roomFills = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, TextMesh> roomLabels = new Dictionary<int, TextMesh>();
        readonly List<CharacterView> characters = new List<CharacterView>();
        readonly SpriteRenderer vacancyFill;
        readonly TextMesh vacancyLabel;
        readonly SpriteRenderer outside;
        readonly TextMesh hint;
        readonly TextMesh pausedLabel;

        public HotelView(HotelLayout layout, Transform parent)
        {
            this.layout = layout;
            root = new GameObject("HotelView").transform;
            root.SetParent(parent, false);
            square = MakeSquare();
            circle = MakeCircle();
            font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Helvetica" }, 16)
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            outside = Quad("Outside", new Rect(0, 0, layout.Width, layout.Height), Palette.FloorColor(8), -20);
            Quad("Shell", layout.Building, Palette.Wall, -10);

            foreach (var area in layout.Floor.Areas)
            {
                if (area.Kind == AreaKind.Corridor)
                {
                    Quad(area.Id, area.Rect, Palette.Corridor, 0);
                }
                else if (area.Kind == AreaKind.Walkway)
                {
                    Quad(area.Id, area.Rect, Palette.Hex("#5a6170"), 0);
                }
            }

            foreach (var area in layout.Floor.Areas)
            {
                if (area.Kind == AreaKind.Lobby)
                {
                    DrawWalled(area, Palette.LobbyWall, Palette.LobbyFloor);
                    Label("Lobby", area.Rect.X + 16, area.Rect.Y + 14, Palette.Hex("#dbc5a2"), 14);
                }
                else if (area.Kind == AreaKind.Office)
                {
                    DrawWalled(area, Palette.OfficeWall, Palette.OfficeFloor);
                    float pcX = area.Rect.X + area.Rect.W * 0.72f;
                    float pcY = area.Rect.Center.Y;
                    Quad("Pc", new Rect(pcX - 24, pcY - 20, 48, 32), Palette.Hex("#1a2030"), 3);
                    Quad("PcScreen", new Rect(pcX - 20, pcY - 16, 40, 24), Palette.Hex("#7dffb2"), 4);
                    Label("Office", area.Rect.X + 16, area.Rect.Y + 14, Palette.Hex("#dbc5a2"), 12);
                }
                else if (area.Kind == AreaKind.Department)
                {
                    Quad(area.Id, area.Rect, Palette.Hex("#4a3f52"), 1);
                    Label(area.Label, area.Rect.X + 10, area.Rect.Y + 8, Palette.Hex(area.Accent ?? "#e8edf5"), 12);
                }
                else if (area.Kind == AreaKind.Parking)
                {
                    Quad(area.Id, area.Rect, Palette.Hex("#2a2c30"), 0);
                    Label("Parking", area.Rect.X + 16, area.Rect.Y + 14, Palette.Hex("#c4c0b0"), 13);
                }
            }

            foreach (var planned in layout.Floor.Rooms)
            {
                Quad($"RoomWall-{planned.Id}", planned.Rect, Palette.Wall, 1);
                var inner = Inset(planned.Rect, layout.Tile);
                roomFills[planned.Id] = Quad($"RoomFill-{planned.Id}", inner, Palette.Locked, 2);
                DrawDoor(planned.Rect, planned.DoorOpening);
                roomLabels[planned.Id] = Label($"Room {planned.Id}", inner.X + 6, inner.Y + 6, Palette.Hex("#101520"), 12);
            }

            var desk = layout.FrontDesk;
            Quad("Desk", new Rect(desk.X - desk.W / 2f, desk.Y - desk.H / 2f, desk.W, desk.H), Palette.Hex("#5a4030"), 5);
            Label("Front desk", desk.X - 34, desk.Y - desk.H / 2f - 16, Palette.Hex("#dbc5a2"), 13);

            var radio = layout.LobbyRadio;
            Quad("Radio", new Rect(radio.X - radio.W / 2f, radio.Y - radio.H / 2f, radio.W, radio.H), Palette.RadioBody, 6);
            Quad("RadioKnob", new Rect(radio.X - 10, radio.Y - 4, 8, 5), Palette.RadioKnob, 7);
            Label("Radio", radio.X - 14, radio.Y + radio.H / 2f + 2, Palette.Hex("#dbc5a2"), 9);

            var paper = layout.Newspaper;
            Quad("Paper", new Rect(paper.X - paper.W / 2f, paper.Y - paper.H / 2f, paper.W, paper.H), Palette.Hex("#7a2e2e"), 6);
            Label("Newspaper", paper.X - 28, paper.Y + paper.H / 2f + 2, Palette.Hex("#dbc5a2"), 9);

            var phone = layout.DeskPhone;
            if (phone != null)
            {
                Quad("Phone", new Rect(phone.X - phone.W / 2f, phone.Y - phone.H / 2f, phone.W, phone.H), Palette.Hex("#c45c2a"), 7);
                Label("Phone", phone.X - 16, phone.Y + phone.H / 2f + 2, Palette.Hex("#dbc5a2"), 9);
            }

            var deskPc = layout.DeskPc;
            if (deskPc != null)
            {
                Quad("DeskPc", new Rect(deskPc.X - deskPc.W / 2f, deskPc.Y - deskPc.H / 2f, deskPc.W, deskPc.H), Palette.Hex("#1a2030"), 7);
                Label("PC", deskPc.X - 8, deskPc.Y + deskPc.H / 2f + 2, Palette.Hex("#7dffb2"), 9);
            }

            var sign = layout.VacancySign;
            vacancyFill = Quad("Sign", new Rect(sign.X - 24f, sign.Y - 8f, 48f, 16f), Palette.Hex("#7a2e2e"), 5);
            vacancyLabel = Label("NO VACANCY", sign.X - 36, sign.Y - 8, Color.white, 11);

            hint = Label("", 16, layout.Height - 22, Palette.Muted, 12);
            pausedLabel = Label("PAUSED", layout.Width / 2f - 60, layout.Height / 2f - 16, Color.white, 28);
            pausedLabel.gameObject.SetActive(false);
        }

        public void Refresh(GameState state, PlayerActor player, List<StaffNpc> staff)
        {
            outside.color = Palette.FloorColor(state.Hour);

            foreach (var room in state.Rooms)
            {
                if (!roomFills.TryGetValue(room.Id, out var fill)) continue;
                fill.color = Palette.RoomColor(room);
                if (roomLabels.TryGetValue(room.Id, out var label))
                {
                    label.text = $"Room {room.Id}\n{RoomStatusLabel(room, state)}";
                    label.color = room.Unlocked ? Palette.Hex("#101520") : Palette.Hex("#6a738a");
                }
            }

            vacancyFill.color = state.VacancyOpen ? Palette.Hex("#2f6b3a") : Palette.Hex("#7a2e2e");
            vacancyLabel.text = state.VacancyOpen ? "VACANCY" : "NO VACANCY";

            int used = 0;
            for (int i = 0; i < state.WaitingGuests.Count; i++)
            {
                var guest = state.WaitingGuests[i];
                var slot = layout.CheckInLineSlot(i);
                string tag = i == 0 ? $"{guest.Name} *" : guest.Name;
                PlaceCharacter(used++, slot.X, slot.Y, 13, Palette.Hex("#e8a0bf"), tag);
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
                else if (guest.Phase == "buying_paper")
                {
                    label = $"{guest.Name} paper";
                    color = Palette.Paper;
                }
                else if (guest.Phase == "walkabout")
                {
                    label = $"{guest.Name} out";
                    color = Palette.Hex("#c4b08a");
                }

                PlaceCharacter(used++, guest.X, guest.Y, guest.Radius, color, label);
            }

            PlaceCharacter(used++, player.X, player.Y, player.Radius, Palette.Player, "You");
            foreach (var person in staff)
            {
                if (person == null) continue;
                string label = person.Name;
                if (person.Phase == "waiting_pay") label = $"{person.Name} pay ${person.WagesOwed}";
                else if (person.Phase == "to_desk") label = $"{person.Name} ->pay";
                else if (person.PaydayDue) label = $"{person.Name} payday";
                PlaceCharacter(used++, person.X, person.Y, person.Radius, Palette.Hex(person.Color), label);
            }

            for (int i = used; i < characters.Count; i++) characters[i].Root.SetActive(false);

            hint.text = HintText(state, staff);
            pausedLabel.gameObject.SetActive(state.Paused);
        }

        void PlaceCharacter(int index, float x, float y, float radius, Color color, string label)
        {
            while (characters.Count <= index)
            {
                var holder = new GameObject($"Char{characters.Count}").transform;
                holder.SetParent(root, false);
                var body = holder.gameObject.AddComponent<SpriteRenderer>();
                body.sprite = circle;
                body.sortingOrder = 20;
                var text = Label("", 0, 0, Palette.Text, 11);
                text.transform.SetParent(holder, false);
                characters.Add(new CharacterView { Root = holder.gameObject, Body = body, Label = text });
            }

            var view = characters[index];
            view.Root.SetActive(true);
            view.Root.transform.position = World(x, y, 0);
            view.Root.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            view.Body.color = color;
            view.Label.text = label;
            view.Label.transform.localPosition = new Vector3(-0.6f, 0.9f, 0);
            view.Label.transform.localScale = new Vector3(1f / (radius * 2f), 1f / (radius * 2f), 1f);
            view.Label.anchor = TextAnchor.LowerCenter;
        }

        void DrawWalled(FloorArea area, Color wall, Color floor)
        {
            Quad(area.Id + "-wall", area.Rect, wall, 1);
            Quad(area.Id + "-floor", Inset(area.Rect, layout.Tile), floor, 2);
            if (area.Doors == null) return;
            foreach (var door in area.Doors) DrawDoor(area.Rect, door);
        }

        void DrawDoor(Rect rect, Door door)
        {
            if (door == null) return;
            Rect gap;
            if (door.Side == "north" || door.Side == "south")
            {
                gap = new Rect(
                    door.Center.X - door.Width / 2f,
                    door.Side == "north" ? rect.Y : rect.Y + rect.H - layout.Tile,
                    door.Width,
                    layout.Tile);
            }
            else
            {
                gap = new Rect(
                    door.Side == "west" ? rect.X : rect.X + rect.W - layout.Tile,
                    door.Center.Y - door.Width / 2f,
                    layout.Tile,
                    door.Width);
            }

            Quad($"Door-{door.Side}-{door.Center.X:0}-{door.Center.Y:0}", gap, Palette.Doorway, 3);
        }

        SpriteRenderer Quad(string name, Rect rect, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = World(rect.X + rect.W / 2f, rect.Y + rect.H / 2f, 0);
            go.transform.localScale = new Vector3(rect.W, rect.H, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        TextMesh Label(string text, float x, float y, Color color, int size)
        {
            var go = new GameObject(text == "" ? "Label" : text);
            go.transform.SetParent(root, false);
            go.transform.position = World(x, y, -0.1f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = font;
            tm.fontSize = size * 4;
            tm.characterSize = 0.28f;
            tm.anchor = TextAnchor.UpperLeft;
            tm.alignment = TextAlignment.Left;
            tm.color = color;
            tm.GetComponent<MeshRenderer>().sortingOrder = 30;
            return tm;
        }

        static Rect Inset(Rect rect, float tile)
        {
            return new Rect(rect.X + tile, rect.Y + tile, rect.W - tile * 2f, rect.H - tile * 2f);
        }

        public static Vector3 World(float x, float y, float z)
        {
            return new Vector3(x, -y, z);
        }

        static string RoomStatusLabel(Room room, GameState state)
        {
            if (!room.Unlocked) return "LOCKED";
            if (room.Status == "clean") return "Vacant";
            if (room.Status == "needs_inspection") return "Inspect";
            if (room.Status == "needs_repair")
            {
                float hrs = GameConfig.GetRepairHours(room.RepairLevel);
                int cost = room.RepairCost ?? Economy.GetRepairCost(state, room.RepairLevel);
                return $"Fix {room.RepairLevel} ({hrs}h ${cost})";
            }

            if (room.Status == "dirty")
            {
                return $"Dirt {room.DirtLevel} ({GameConfig.GetCleanHours(room.DirtLevel)}h)";
            }

            if (room.Status == "occupied")
            {
                float hoursLeft = Mathf.Max(0, room.StayRemainingHours ?? 0);
                int daysLeft = Mathf.Max(0, Mathf.CeilToInt(hoursLeft / GameConfig.StayIntervalHours));
                return room.GuestName != null ? $"{room.GuestName} {daysLeft}d" : "Occupied";
            }

            return room.Status;
        }

        static string HintText(GameState state, List<StaffNpc> staff)
        {
            foreach (var guest in state.ActiveGuests)
            {
                if (guest.Phase == "waiting_checkout") return "Press E on the desk PC to check guests out";
            }

            foreach (var person in staff)
            {
                if (person != null && (person.Phase == "waiting_pay" || person.Phase == "to_desk"))
                {
                    return $"Press E to pay {person.Name} ${person.WagesOwed}";
                }
            }

            if (Economy.FirstAtDesk(state) != null) return "Press E on the desk PC to check them in";
            return "E desk PC · E radio / newspaper box · E office PC (supplies / hire) · V sign · Esc pause";
        }

        static Sprite MakeSquare()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new UnityEngine.Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        static Sprite MakeCircle()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(1, 1, 1, 0);
            float r = size * 0.5f - 1f;
            var center = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) <= r
                        ? Color.white
                        : clear);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        sealed class CharacterView
        {
            public GameObject Root;
            public SpriteRenderer Body;
            public TextMesh Label;
        }
    }
}

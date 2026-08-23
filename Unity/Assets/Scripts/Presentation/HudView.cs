using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vacancy
{
    public sealed class HudView
    {
        readonly GameState state;
        readonly VacancyGame game;
        readonly Font font;
        readonly Text money;
        readonly Text clock;
        readonly Text tod;
        readonly Text day;
        readonly Text queue;
        readonly Text reputation;
        readonly Text vacancy;
        readonly Text radio;
        readonly Text inventory;
        readonly Text shelter;
        readonly Text log;
        readonly Button hireBob;
        readonly Button hireMary;
        readonly Button unlock;
        readonly Button vacancyBtn;
        readonly GameObject pcPanel;
        readonly Text pcStock;
        readonly Text pcPending;
        readonly Text pcTotal;
        readonly Transform pcRows;
        readonly Dictionary<string, InputField> orderFields = new Dictionary<string, InputField>();
        bool pcShelterRows;

        readonly GameObject deskPanel;
        readonly Text deskName;
        readonly Text deskClaim;
        readonly Text deskSigns;
        readonly Text deskWhy;
        readonly Text deskReply;
        readonly Transform deskQuestionsRoot;
        readonly Button deskAdmit;
        readonly List<GameObject> questionButtons = new List<GameObject>();
        WaitingGuest deskQuestionGuest;
        int deskQuestionStamp = -1;

        readonly GameObject radioPanel;
        readonly Text radioLog;
        readonly GameObject paperPanel;
        readonly Text paperLog;
        readonly GameObject bannerPanel;
        readonly Text bannerAct;
        readonly Text bannerTitle;
        readonly Text bannerBody;

        public HudView(GameState state, VacancyGame game, Transform parent)
        {
            this.state = state;
            this.game = game;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 16)
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);

            var eventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }

            var top = Bar("TopBar", canvasGo.transform, new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(1560, 96));
            money = Stat(top.transform, "Cash $0", -700, 16);
            ButtonOn(top.transform, "+$500", new Vector2(-580, 16), () => game.AddDebugCash(), 80, 28);
            ButtonOn(top.transform, "+1 day", new Vector2(-490, 16), () => game.SkipDay(), 80, 28);
            clock = Stat(top.transform, "8:00 AM", -380, 16);
            tod = Stat(top.transform, "Morning", -250, 16);
            day = Stat(top.transform, "Day 1", -130, 16);
            queue = Stat(top.transform, "Waiting 0", -10, 16);
            reputation = Stat(top.transform, "Rep 50", 110, 16);
            vacancy = Stat(top.transform, "VACANCY", 250, 16);
            radio = MakeText(top.transform, "Local AM — weather, roads", new Vector2(520, 18), 13, Palette.Accent, 460);
            inventory = MakeText(top.transform, "", new Vector2(200, -22), 12, Palette.Muted, 900);
            shelter = MakeText(top.transform, "", new Vector2(200, -42), 12, Palette.Accent, 900);

            var shop = Panel("Shop", canvasGo.transform, new Vector2(1f, 0.5f), new Vector2(-150, -20), new Vector2(280, 420));
            MakeText(shop.transform, "Front desk", new Vector2(0, 180), 18, Palette.Accent, 240);
            vacancyBtn = ButtonOn(shop.transform, "Set: NO VACANCY", new Vector2(0, 130), () => game.ToggleVacancy());
            hireBob = ButtonOn(shop.transform, $"Hire Bob — ${GameConfig.HireBobCost}", new Vector2(0, 70), () => game.HireBob());
            hireMary = ButtonOn(shop.transform, $"Hire Mary — ${GameConfig.HireMaryCost}", new Vector2(0, 20), () => game.HireMary());
            unlock = ButtonOn(shop.transform, "Unlock room", new Vector2(0, -30), () => game.UnlockNextRoom());
            MakeText(shop.transform, "Inspect → Clean → Repair", new Vector2(0, -90), 13, Palette.Muted, 240);

            var logPanel = Panel("Log", canvasGo.transform, new Vector2(0f, 0f), new Vector2(280, 110), new Vector2(540, 200));
            MakeText(logPanel.transform, "Activity log", new Vector2(0, 80), 16, Palette.Accent, 500);
            log = MakeText(logPanel.transform, "", new Vector2(0, -10), 13, Palette.Text, 500);
            log.alignment = TextAnchor.UpperLeft;

            pcPanel = Panel("OfficePc", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 560));
            MakeText(pcPanel.transform, "Office PC — Supply Orders", new Vector2(0, 250), 20, Palette.Accent, 520);
            pcStock = MakeText(pcPanel.transform, "", new Vector2(0, 210), 13, Palette.Text, 520);
            pcRows = new GameObject("OrderRows", typeof(RectTransform)).transform;
            pcRows.SetParent(pcPanel.transform, false);
            BuildPcRows();
            pcPending = MakeText(pcPanel.transform, "No deliveries in transit.", new Vector2(0, -170), 13, Palette.Muted, 520);
            pcTotal = MakeText(pcPanel.transform, "Total: $0", new Vector2(-120, -210), 16, Palette.Accent, 200);
            ButtonOn(pcPanel.transform, "Place order", new Vector2(80, -210), () => game.PlacePcOrder(ReadOrderQuantities()), 140, 32);
            ButtonOn(pcPanel.transform, "Close", new Vector2(0, -250), () => game.ClosePc(), 140, 32);
            pcPanel.SetActive(false);

            deskPanel = Panel("DeskReview", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 620));
            MakeText(deskPanel.transform, "Desk review", new Vector2(0, 280), 18, Palette.Accent, 580);
            deskName = MakeText(deskPanel.transform, "", new Vector2(0, 248), 20, Palette.Text, 580);
            deskClaim = MakeText(deskPanel.transform, "", new Vector2(0, 218), 14, Palette.Muted, 580);
            deskSigns = MakeText(deskPanel.transform, "", new Vector2(0, 160), 13, Palette.Accent, 580);
            deskSigns.alignment = TextAnchor.UpperLeft;
            deskSigns.rectTransform.sizeDelta = new Vector2(580, 70);
            deskWhy = MakeText(deskPanel.transform, "", new Vector2(0, 70), 13, Palette.Text, 580);
            deskWhy.alignment = TextAnchor.UpperLeft;
            deskWhy.rectTransform.sizeDelta = new Vector2(580, 80);
            deskReply = MakeText(deskPanel.transform, "Ask something. Their answer stays here.", new Vector2(0, -20), 13, Palette.Text, 580);
            deskReply.alignment = TextAnchor.UpperLeft;
            deskReply.rectTransform.sizeDelta = new Vector2(580, 90);
            deskQuestionsRoot = new GameObject("Questions", typeof(RectTransform)).transform;
            deskQuestionsRoot.SetParent(deskPanel.transform, false);
            var qRt = deskQuestionsRoot.GetComponent<RectTransform>();
            qRt.anchorMin = qRt.anchorMax = new Vector2(0.5f, 0.5f);
            qRt.anchoredPosition = new Vector2(0, -130);
            qRt.sizeDelta = new Vector2(580, 160);
            deskAdmit = ButtonOn(deskPanel.transform, "Admit", new Vector2(-160, -270), () => game.AdmitDeskGuest(), 150, 34);
            ButtonOn(deskPanel.transform, "Refuse", new Vector2(0, -270), () => game.RefuseDeskGuest(), 150, 34);
            ButtonOn(deskPanel.transform, "Close", new Vector2(160, -270), () => game.CloseDeskReview(), 150, 34);
            deskPanel.SetActive(false);

            radioPanel = Panel("RadioLog", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 480));
            MakeText(radioPanel.transform, "Lobby radio", new Vector2(0, 210), 20, Palette.Accent, 520);
            radioLog = MakeText(radioPanel.transform, "", new Vector2(0, 10), 14, Palette.Text, 520);
            radioLog.alignment = TextAnchor.UpperLeft;
            radioLog.rectTransform.sizeDelta = new Vector2(520, 360);
            ButtonOn(radioPanel.transform, "Close", new Vector2(0, -200), () => game.CloseRadio(), 140, 32);
            radioPanel.SetActive(false);

            paperPanel = Panel("PaperLog", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 480));
            MakeText(paperPanel.transform, "Newspaper", new Vector2(0, 210), 20, Palette.Accent, 520);
            paperLog = MakeText(paperPanel.transform, "", new Vector2(0, 10), 14, Palette.Text, 520);
            paperLog.alignment = TextAnchor.UpperLeft;
            paperLog.rectTransform.sizeDelta = new Vector2(520, 360);
            ButtonOn(paperPanel.transform, "Close", new Vector2(0, -200), () => game.ClosePaper(), 140, 32);
            paperPanel.SetActive(false);

            bannerPanel = Panel("StoryBanner", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 280));
            bannerAct = MakeText(bannerPanel.transform, "", new Vector2(0, 100), 14, Palette.Accent, 560);
            bannerTitle = MakeText(bannerPanel.transform, "", new Vector2(0, 60), 22, Palette.Text, 560);
            bannerBody = MakeText(bannerPanel.transform, "", new Vector2(0, -10), 15, Palette.Muted, 560);
            bannerBody.alignment = TextAnchor.UpperCenter;
            bannerBody.rectTransform.sizeDelta = new Vector2(560, 120);
            ButtonOn(bannerPanel.transform, "Continue", new Vector2(0, -100), () => game.DismissBanner(), 160, 34);
            bannerPanel.SetActive(false);

            Refresh(true);
        }

        public void Refresh(bool force = false)
        {
            money.text = $"Cash ${state.Money}";
            clock.text = GameState.FormatClock(state.Hour);
            tod.text = GameState.TimeOfDayLabel(state.Hour);
            day.text = $"Day {state.Day}";
            queue.text = $"Waiting {state.WaitingGuests.Count}";
            reputation.text = $"Rep {state.Reputation}";
            vacancy.text = state.VacancyOpen ? "VACANCY" : "NO VACANCY";
            radio.text = Media.RadioHudText(state);
            inventory.text = InventorySystem.HudSummary(state);

            string shelterLine = Shelter.HudSummary(state);
            if (!string.IsNullOrEmpty(shelterLine) && state.Story != null)
            {
                shelterLine += $" · Humanity: {state.Story.Humanity}%";
            }

            shelter.text = shelterLine;
            shelter.gameObject.SetActive(!string.IsNullOrEmpty(shelterLine));

            hireBob.interactable = !state.BobHired && state.Money >= GameConfig.HireBobCost;
            hireBob.GetComponentInChildren<Text>().text = state.BobHired
                ? "Bob hired (repairs)"
                : $"Hire Bob — ${GameConfig.HireBobCost}";

            hireMary.interactable = !state.MaryHired && state.Money >= GameConfig.HireMaryCost;
            hireMary.GetComponentInChildren<Text>().text = state.MaryHired
                ? "Mary hired (inspect + clean)"
                : $"Hire Mary — ${GameConfig.HireMaryCost}";

            bool nextRoom = false;
            foreach (var room in state.Rooms)
            {
                if (!room.Unlocked) nextRoom = true;
            }

            int cost = state.RoomUnlockCost();
            unlock.interactable = nextRoom && state.Money >= cost;
            unlock.GetComponentInChildren<Text>().text = nextRoom ? $"Unlock room — ${cost}" : "All rooms unlocked";
            vacancyBtn.GetComponentInChildren<Text>().text = state.VacancyOpen ? "Set: NO VACANCY" : "Set: VACANCY";

            int take = Mathf.Min(8, state.Messages.Count);
            var lines = new List<string>();
            for (int i = 0; i < take; i++) lines.Add("• " + state.Messages[i]);
            log.text = string.Join("\n", lines);

            if (state.Shelter != null && state.Shelter.Unlocked && !pcShelterRows)
            {
                BuildPcRows();
            }

            if (state.PcOpen) RefreshPc();
            pcPanel.SetActive(state.PcOpen);

            if (state.DeskGuest != null) RefreshDesk();
            deskPanel.SetActive(state.DeskGuest != null);

            radioPanel.SetActive(state.MediaOpen == "radio");
            paperPanel.SetActive(state.MediaOpen == "paper");
        }

        public void ShowBanner(StoryBanner banner)
        {
            bannerAct.text = Story.ActLabels.TryGetValue(banner.Act, out var label) ? label : banner.Act;
            bannerTitle.text = banner.Title;
            bannerBody.text = banner.Body;
            bannerPanel.SetActive(true);
        }

        public void HideBanner()
        {
            bannerPanel.SetActive(false);
        }

        public void ShowRadio()
        {
            var entries = state.Story?.Media?.RadioLog;
            if (entries == null || entries.Count == 0)
            {
                radioLog.text = "Nothing but weather and road reports so far.";
            }
            else
            {
                var lines = new List<string>();
                int take = Mathf.Min(6, entries.Count);
                for (int i = 0; i < take; i++)
                {
                    var e = entries[i];
                    lines.Add($"Day {e.Day} · {e.Headline}\n{e.Body}\n");
                }

                radioLog.text = string.Join("\n", lines);
            }

            radioPanel.SetActive(true);
        }

        public void HideRadio()
        {
            radioPanel.SetActive(false);
        }

        public void ShowPaper()
        {
            var papers = state.Story?.Media?.Papers;
            if (papers == null || papers.Count == 0)
            {
                paperLog.text = "No paper today. The stack is empty.";
            }
            else
            {
                var lines = new List<string>();
                int take = Mathf.Min(6, papers.Count);
                for (int i = 0; i < take; i++)
                {
                    var e = papers[i];
                    lines.Add($"Day {e.Day} · {(e.Read ? "read" : "unread")}\n{e.Headline}\n{e.Body}\n");
                }

                paperLog.text = string.Join("\n", lines);
            }

            paperPanel.SetActive(true);
        }

        public void HidePaper()
        {
            paperPanel.SetActive(false);
        }

        void RefreshDesk()
        {
            var guest = state.DeskGuest;
            if (guest == null) return;

            deskName.text = guest.Name;
            deskClaim.text = guest.Claim;

            if (guest.Replies.Count == 0)
            {
                deskReply.text = "Ask something. Their answer stays here — not just in the log.";
            }
            else
            {
                var lines = new List<string>();
                foreach (var reply in guest.Replies)
                {
                    lines.Add($"You: {reply.Prompt}\n{guest.Name}: “{reply.Spoken}”");
                }

                deskReply.text = string.Join("\n\n", lines);
            }

            var signs = Arrivals.RevealedSigns(guest);
            if (signs.Count == 0)
            {
                deskSigns.text = "Nothing stands out yet.";
            }
            else
            {
                var lines = new List<string>();
                foreach (var sign in signs) lines.Add("• " + sign.Text);
                deskSigns.text = string.Join("\n", lines);
            }

            var why = Arrivals.AssessArrival(state, guest);
            var rows = new List<string>
            {
                $"Rooms ready: {why.BunksFree} of {why.BunksTotal}",
                $"People inside: {why.Occupants}"
            };
            if (why.Shelter != null)
            {
                rows.Add($"Water: {why.Shelter.WaterDays}d → {why.Shelter.WaterDaysAfter}d if admitted");
                rows.Add($"Food: {why.Shelter.FoodDays}d → {why.Shelter.FoodDaysAfter}d if admitted");
                rows.Add($"Barricades: {why.Shelter.Integrity}%");
                rows.Add($"Humanity: {why.Humanity}%");
            }
            else
            {
                rows.Add($"Pays for the room: {(why.PaysRent ? "Yes" : "No")}");
            }

            rows.Add($"They will wait: {Mathf.Max(0, Mathf.FloorToInt(guest.WaitRemainingHours))}h more");
            deskWhy.text = string.Join("\n", rows);

            deskAdmit.interactable = why.BunksFree > 0;
            deskAdmit.GetComponentInChildren<Text>().text = why.BunksFree == 0 ? "No room ready" : "Admit";

            int stamp = guest.QuestionsAsked * 17 + guest.AskedQuestionIds.Count;
            if (deskQuestionGuest != guest || deskQuestionStamp != stamp)
            {
                deskQuestionGuest = guest;
                deskQuestionStamp = stamp;
                RebuildDeskQuestions(guest, why.QuestionsLeft > 0);
            }
        }

        void RebuildDeskQuestions(WaitingGuest guest, bool canAsk)
        {
            foreach (var go in questionButtons) Object.Destroy(go);
            questionButtons.Clear();

            var questions = Arrivals.DeskQuestions(state, guest);
            if (questions.Count == 0)
            {
                var hint = MakeText(deskQuestionsRoot, canAsk ? "Nothing left to ask." : "They stop answering.",
                    Vector2.zero, 13, Palette.Muted, 540);
                questionButtons.Add(hint.gameObject);
                return;
            }

            float y = 50f;
            int shown = Mathf.Min(4, questions.Count);
            for (int i = 0; i < shown; i++)
            {
                var q = questions[i];
                string src = q.Source == "paper" ? "Paper" : q.Source == "radio" ? "Radio" : "Basic";
                var captured = q;
                var button = ButtonOn(deskQuestionsRoot, $"{src}: {q.Prompt}", new Vector2(0, y), () =>
                {
                    if (state.DeskGuest == null) return;
                    game.AskDeskQuestion(captured);
                }, 560, 32);
                button.interactable = canAsk;
                questionButtons.Add(button.gameObject);
                y -= 36f;
            }
        }

        void BuildPcRows()
        {
            for (int i = pcRows.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(pcRows.GetChild(i).gameObject);
            }

            orderFields.Clear();
            var ids = InventorySystem.OrderableItemIds(state);
            float y = 160f;
            foreach (var id in ids)
            {
                var entry = InventorySystem.LookupOrderable(state, id);
                if (entry == null) continue;
                MakeText(pcRows, $"{entry.Label} (${entry.UnitCost})", new Vector2(-150, y), 14, Palette.Text, 220);
                orderFields[id] = NumberField(pcRows, new Vector2(80, y), entry.OrderPack);
                y -= 32f;
            }

            pcShelterRows = state.Shelter != null && state.Shelter.Unlocked;
        }

        void RefreshPc()
        {
            var parts = new List<string>();
            foreach (var id in InventorySystem.OrderableItemIds(state))
            {
                var entry = InventorySystem.LookupOrderable(state, id);
                if (entry == null) continue;
                int onHand = entry.Kind == "shelter"
                    ? Shelter.GetStock(state, id)
                    : InventorySystem.GetStock(state, id);
                parts.Add($"{entry.Label}: {onHand}");
            }

            pcStock.text = "On hand — " + string.Join(" · ", parts);

            int total = 0;
            foreach (var pair in ReadOrderQuantities())
            {
                var entry = InventorySystem.LookupOrderable(state, pair.Key);
                if (entry != null) total += pair.Value * entry.UnitCost;
            }

            pcTotal.text = $"Total: ${total}";

            if (state.Inventory.PendingOrders.Count == 0)
            {
                pcPending.text = "No deliveries in transit.";
            }
            else
            {
                var pending = new List<string>();
                foreach (var order in state.Inventory.PendingOrders)
                {
                    var items = new List<string>();
                    foreach (var item in order.Items)
                    {
                        var entry = InventorySystem.LookupOrderable(state, item.Key);
                        items.Add($"{item.Value} {entry?.Label ?? item.Key}");
                    }

                    pending.Add($"In transit ({Mathf.CeilToInt(order.HoursLeft)}h): {string.Join(", ", items)}");
                }

                pcPending.text = string.Join(" | ", pending);
            }
        }

        public Dictionary<string, int> ReadOrderQuantities()
        {
            var quantities = new Dictionary<string, int>();
            foreach (var pair in orderFields)
            {
                int.TryParse(pair.Value.text, out int qty);
                quantities[pair.Key] = qty;
            }

            return quantities;
        }

        GameObject Bar(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            return Panel(name, parent, anchor, pos, size);
        }

        GameObject Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(Palette.HudPanel.r, Palette.HudPanel.g, Palette.HudPanel.b, 0.94f);
            return go;
        }

        Text Stat(Transform parent, string value, float x, float y = 0)
        {
            return MakeText(parent, value, new Vector2(x, y), 16, Palette.Text, 140);
        }

        Button ButtonOn(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction action, float width = 230, float height = 36)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, height);
            go.GetComponent<Image>().color = new Color(0.22f, 0.29f, 0.4f, 1f);
            var text = MakeText(go.transform, label, Vector2.zero, 13, Palette.Text, width - 10);
            text.alignment = TextAnchor.MiddleCenter;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            return button;
        }

        InputField NumberField(Transform parent, Vector2 pos, int step)
        {
            var go = new GameObject("Field", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(90, 28);
            go.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.22f, 1f);
            var text = MakeText(go.transform, "0", Vector2.zero, 14, Palette.Text, 80);
            text.alignment = TextAnchor.MiddleCenter;
            var field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.text = "0";
            return field;
        }

        Text MakeText(Transform parent, string value, Vector2 pos, int size, Color color, float width)
        {
            var go = new GameObject(value == "" ? "Text" : value, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 40);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.text = value;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}

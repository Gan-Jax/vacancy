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
        readonly Foldout shopFold;
        readonly Foldout logFold;
        readonly Foldout taskFold;
        readonly Text taskLines;
        readonly ScrollBox logBox;
        readonly Button hireBob;
        readonly Button hireMary;
        readonly Button unlock;
        readonly Button vacancyBtn;
        readonly GameObject pcPanel;
        readonly GameObject pcMenuPage;
        readonly GameObject pcSuppliesPage;
        readonly GameObject pcHirePage;
        readonly Text pcStock;
        readonly Text pcPending;
        readonly Text pcTotal;
        readonly Transform pcRows;
        readonly Dictionary<string, InputField> orderFields = new Dictionary<string, InputField>();
        bool pcShelterRows;
        string officePage = "menu";

        readonly GameObject deskPanel;
        readonly Text deskName;
        readonly Text deskClaim;
        readonly Text deskSigns;
        readonly Text deskWhy;
        readonly ScrollBox deskReplyBox;
        readonly RectTransform deskQuestionsRoot;
        readonly Text deskAskHint;
        readonly Button deskAdmit;
        readonly List<GameObject> questionButtons = new List<GameObject>();
        WaitingGuest deskQuestionGuest;
        int deskQuestionStamp = -1;

        readonly GameObject radioPanel;
        readonly Text radioLog;
        readonly GameObject paperPanel;
        readonly Text paperLog;
        readonly GameObject phonePanel;
        readonly Text phoneRequests;
        readonly GameObject deskPcPanel;
        readonly GameObject deskPcHomePage;
        readonly GameObject deskPcCheckoutPage;
        readonly GameObject deskPcBookPage;
        readonly Button deskPcCheckInBtn;
        readonly Button deskPcCheckOutBtn;
        readonly Text deskPcCheckoutInfo;
        readonly Button deskPcCheckoutAction;
        readonly Text deskPcLog;
        string deskPcPage = "home";
        readonly GameObject bannerPanel;
        readonly Text bannerAct;
        readonly Text bannerTitle;
        readonly Text bannerBody;

        readonly GameObject pauseOverlay;
        readonly GameObject pauseRootPage;
        readonly GameObject pauseJournalPage;
        readonly GameObject pauseSettingsPage;
        readonly GameObject pauseQuitPage;
        readonly Text pauseJournalBody;
        readonly Text pauseSensitivity;
        readonly Text pauseInvert;
        string pausePage = "root";

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
            canvas.sortingOrder = 50;
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

            taskFold = MakeFoldout(
                "Tasks",
                canvasGo.transform,
                new Vector2(0f, 1f),
                new Vector2(168, -140),
                new Vector2(312, 40),
                new Vector2(312, 220),
                "Today's tasks",
                new Vector2(0.5f, 0.5f));
            taskLines = MakeText(taskFold.Body, "Tasks", Vector2.zero, 13, Palette.Text, 276);
            taskLines.alignment = TextAnchor.UpperLeft;
            taskLines.rectTransform.sizeDelta = new Vector2(276, 150);
            taskLines.verticalOverflow = VerticalWrapMode.Overflow;

            shopFold = MakeFoldout(
                "Shop",
                canvasGo.transform,
                new Vector2(1f, 1f),
                new Vector2(-148, -140),
                new Vector2(268, 40),
                new Vector2(268, 220),
                "Front desk",
                new Vector2(0.5f, 0.5f));
            vacancyBtn = ButtonOn(shopFold.Body, "Set: NO VACANCY", new Vector2(0, 48), () => game.ToggleVacancy());
            unlock = ButtonOn(shopFold.Body, "Unlock room", new Vector2(0, 0), () => game.UnlockNextRoom());
            MakeText(shopFold.Body, "Inspect → Clean → Repair", new Vector2(0, -56), 13, Palette.Muted, 240);

            logFold = MakeFoldout(
                "Log",
                canvasGo.transform,
                new Vector2(0f, 0f),
                new Vector2(16, 16),
                new Vector2(420, 40),
                new Vector2(540, 280),
                "Activity log",
                new Vector2(0f, 0f));
            logBox = MakeScrollBox(logFold.Body, Vector2.zero, new Vector2(508, 216));
            Stretch(logBox.Root.GetComponent<RectTransform>(), 0, 0, 0, 0);

            pcPanel = Panel("OfficePc", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 560));
            pcMenuPage = PageOn(pcPanel.transform, "Menu");
            MakeText(pcMenuPage.transform, "Office PC", new Vector2(0, 200), 22, Palette.Accent, 520);
            MakeText(pcMenuPage.transform, "Two things this computer does.", new Vector2(0, 150), 14, Palette.Muted, 520);
            ButtonOn(pcMenuPage.transform, "1. Order supplies", new Vector2(0, 40), () => ShowOfficePage("supplies"), 280, 44);
            ButtonOn(pcMenuPage.transform, "2. Hire help", new Vector2(0, -24), () => ShowOfficePage("hire"), 280, 44);
            ButtonOn(pcMenuPage.transform, "Close", new Vector2(0, -220), () => game.ClosePc(), 140, 32);

            pcSuppliesPage = PageOn(pcPanel.transform, "Supplies");
            MakeText(pcSuppliesPage.transform, "Order supplies", new Vector2(0, 250), 20, Palette.Accent, 520);
            pcStock = MakeText(pcSuppliesPage.transform, "", new Vector2(0, 210), 13, Palette.Text, 520);
            pcRows = new GameObject("OrderRows", typeof(RectTransform)).transform;
            pcRows.SetParent(pcSuppliesPage.transform, false);
            BuildPcRows();
            pcPending = MakeText(pcSuppliesPage.transform, "No deliveries in transit.", new Vector2(0, -170), 13, Palette.Muted, 520);
            pcTotal = MakeText(pcSuppliesPage.transform, "Total: $0", new Vector2(-120, -210), 16, Palette.Accent, 200);
            ButtonOn(pcSuppliesPage.transform, "Place order", new Vector2(80, -210), () => game.PlacePcOrder(ReadOrderQuantities()), 140, 32);
            ButtonOn(pcSuppliesPage.transform, "Back", new Vector2(0, -250), () => ShowOfficePage("menu"), 140, 32);

            pcHirePage = PageOn(pcPanel.transform, "Hire");
            MakeText(pcHirePage.transform, "Hire help", new Vector2(0, 200), 20, Palette.Accent, 520);
            MakeText(
                pcHirePage.transform,
                "Bob repairs rooms. Mary inspects and cleans. They come out of the till.",
                new Vector2(0, 140),
                14,
                Palette.Muted,
                500);
            hireBob = ButtonOn(
                pcHirePage.transform,
                $"Hire Bob — ${GameConfig.HireBobCost}",
                new Vector2(0, 40),
                () => game.HireBob(),
                280,
                44);
            hireMary = ButtonOn(
                pcHirePage.transform,
                $"Hire Mary — ${GameConfig.HireMaryCost}",
                new Vector2(0, -24),
                () => game.HireMary(),
                280,
                44);
            ButtonOn(pcHirePage.transform, "Back", new Vector2(0, -220), () => ShowOfficePage("menu"), 140, 32);
            pcPanel.SetActive(false);
            ShowOfficePage("menu");

            deskPanel = Panel("DeskReview", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 640));
            deskPanel.AddComponent<RectMask2D>();
            MakeText(deskPanel.transform, "Arrival review", new Vector2(0, 292), 18, Palette.Accent, 580);
            deskName = MakeText(deskPanel.transform, "", new Vector2(0, 260), 20, Palette.Text, 580);
            deskClaim = MakeText(deskPanel.transform, "", new Vector2(0, 230), 14, Palette.Muted, 580);
            deskSigns = MakeText(deskPanel.transform, "", new Vector2(0, 178), 13, Palette.Accent, 580);
            deskSigns.alignment = TextAnchor.UpperLeft;
            deskSigns.rectTransform.sizeDelta = new Vector2(580, 56);
            deskWhy = MakeText(deskPanel.transform, "", new Vector2(0, 112), 13, Palette.Text, 580);
            deskWhy.alignment = TextAnchor.UpperLeft;
            deskWhy.rectTransform.sizeDelta = new Vector2(580, 64);
            MakeText(deskPanel.transform, "What they said", new Vector2(0, 68), 13, Palette.Accent, 580);
            deskReplyBox = MakeScrollBox(deskPanel.transform, new Vector2(0, -10), new Vector2(580, 120));
            deskQuestionsRoot = new GameObject("Questions", typeof(RectTransform)).GetComponent<RectTransform>();
            deskQuestionsRoot.SetParent(deskPanel.transform, false);
            deskQuestionsRoot.anchorMin = deskQuestionsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            deskQuestionsRoot.anchoredPosition = new Vector2(0, -148);
            deskQuestionsRoot.sizeDelta = new Vector2(580, 120);
            deskAskHint = MakeText(deskPanel.transform, "", new Vector2(0, -228), 13, Palette.Muted, 580);
            deskAskHint.alignment = TextAnchor.MiddleCenter;
            deskAdmit = ButtonOn(deskPanel.transform, "Admit", new Vector2(-160, -278), () => game.AdmitDeskGuest(), 150, 34);
            ButtonOn(deskPanel.transform, "Refuse", new Vector2(0, -278), () => game.RefuseDeskGuest(), 150, 34);
            ButtonOn(deskPanel.transform, "Close", new Vector2(160, -278), () => game.CloseDeskReview(), 150, 34);
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

            phonePanel = Panel("DeskPhone", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 420));
            MakeText(phonePanel.transform, "Front desk phone", new Vector2(0, 160), 20, Palette.Accent, 520);
            MakeText(phonePanel.transform, "Requests", new Vector2(0, 110), 14, Palette.Muted, 520);
            phoneRequests = MakeText(
                phonePanel.transform,
                "No guest requests yet.\nUse the desk PC to check guests in and out.",
                new Vector2(0, 20),
                15,
                Palette.Text,
                520);
            phoneRequests.alignment = TextAnchor.UpperCenter;
            phoneRequests.rectTransform.sizeDelta = new Vector2(520, 160);
            ButtonOn(phonePanel.transform, "Close", new Vector2(0, -160), () => game.ClosePhone(), 140, 32);
            phonePanel.SetActive(false);

            deskPcPanel = Panel("DeskPc", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 500));
            deskPcHomePage = PageOn(deskPcPanel.transform, "Home");
            MakeText(deskPcHomePage.transform, "Front desk PC", new Vector2(0, 200), 20, Palette.Accent, 520);
            MakeText(deskPcHomePage.transform, "Check guests in and out. Guest book is read-only.", new Vector2(0, 156), 14, Palette.Muted, 520);
            deskPcCheckInBtn = ButtonOn(deskPcHomePage.transform, "Check in (0)", new Vector2(0, 70), () => game.ReviewArrivalFromDeskPc(), 280, 44);
            deskPcCheckOutBtn = ButtonOn(deskPcHomePage.transform, "Check out (0)", new Vector2(0, 10), () => ShowDeskPcPage("checkout"), 280, 44);
            ButtonOn(deskPcHomePage.transform, "Guest book", new Vector2(0, -50), () => ShowDeskPcPage("book"), 280, 44);
            ButtonOn(deskPcHomePage.transform, "Close", new Vector2(0, -200), () => game.CloseDeskPc(), 140, 32);

            deskPcCheckoutPage = PageOn(deskPcPanel.transform, "Checkout");
            MakeText(deskPcCheckoutPage.transform, "Check out", new Vector2(0, 200), 20, Palette.Accent, 520);
            deskPcCheckoutInfo = MakeText(deskPcCheckoutPage.transform, "", new Vector2(0, 60), 15, Palette.Text, 520);
            deskPcCheckoutInfo.alignment = TextAnchor.UpperCenter;
            deskPcCheckoutInfo.rectTransform.sizeDelta = new Vector2(520, 160);
            deskPcCheckoutAction = ButtonOn(
                deskPcCheckoutPage.transform,
                "Process checkout",
                new Vector2(0, -60),
                () => game.ProcessDeskPcCheckout(),
                280,
                44);
            ButtonOn(deskPcCheckoutPage.transform, "Back", new Vector2(0, -200), () => ShowDeskPcPage("home"), 140, 32);

            deskPcBookPage = PageOn(deskPcPanel.transform, "Book");
            MakeText(deskPcBookPage.transform, "Guest book", new Vector2(0, 200), 20, Palette.Accent, 520);
            deskPcLog = MakeText(deskPcBookPage.transform, "", new Vector2(0, 10), 14, Palette.Text, 520);
            deskPcLog.alignment = TextAnchor.UpperLeft;
            deskPcLog.rectTransform.sizeDelta = new Vector2(520, 300);
            ButtonOn(deskPcBookPage.transform, "Back", new Vector2(0, -200), () => ShowDeskPcPage("home"), 140, 32);
            deskPcPanel.SetActive(false);
            ShowDeskPcPage("home");

            bannerPanel = Panel("StoryBanner", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 280));
            bannerAct = MakeText(bannerPanel.transform, "", new Vector2(0, 100), 14, Palette.Accent, 560);
            bannerTitle = MakeText(bannerPanel.transform, "", new Vector2(0, 60), 22, Palette.Text, 560);
            bannerBody = MakeText(bannerPanel.transform, "", new Vector2(0, -10), 15, Palette.Muted, 560);
            bannerBody.alignment = TextAnchor.UpperCenter;
            bannerBody.rectTransform.sizeDelta = new Vector2(560, 120);
            ButtonOn(bannerPanel.transform, "Continue", new Vector2(0, -100), () => game.DismissBanner(), 160, 34);
            bannerPanel.SetActive(false);

            pauseOverlay = new GameObject("PauseOverlay", typeof(RectTransform), typeof(Image));
            pauseOverlay.transform.SetParent(canvasGo.transform, false);
            Stretch(pauseOverlay.GetComponent<RectTransform>(), 0, 0, 0, 0);
            pauseOverlay.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.72f);

            var pausePanel = Panel("PauseMenu", pauseOverlay.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 500));
            MakeText(pausePanel.transform, "Paused", new Vector2(0, 214), 28, Palette.Accent, 400).alignment = TextAnchor.MiddleCenter;

            pauseRootPage = new GameObject("Root", typeof(RectTransform));
            pauseRootPage.transform.SetParent(pausePanel.transform, false);
            Stretch(pauseRootPage.GetComponent<RectTransform>(), 20, 20, 20, 70);
            ButtonOn(pauseRootPage.transform, "Resume", new Vector2(0, 110), () => game.ResumeFromMenu(), 280, 40);
            ButtonOn(pauseRootPage.transform, "Journal", new Vector2(0, 54), () => ShowPausePage("journal"), 280, 40);
            ButtonOn(pauseRootPage.transform, "Settings", new Vector2(0, -2), () => ShowPausePage("settings"), 280, 40);
            ButtonOn(pauseRootPage.transform, "Quit game", new Vector2(0, -58), () => ShowPausePage("quit"), 280, 40);
            MakeText(pauseRootPage.transform, "Esc resumes · hold RMB to look", new Vector2(0, -130), 13, Palette.Muted, 380).alignment = TextAnchor.MiddleCenter;

            pauseJournalPage = new GameObject("Journal", typeof(RectTransform));
            pauseJournalPage.transform.SetParent(pausePanel.transform, false);
            Stretch(pauseJournalPage.GetComponent<RectTransform>(), 20, 20, 20, 70);
            MakeText(pauseJournalPage.transform, "Journal", new Vector2(0, 150), 20, Palette.Text, 380).alignment = TextAnchor.MiddleCenter;
            pauseJournalBody = MakeText(pauseJournalPage.transform, "", new Vector2(0, 10), 15, Palette.Text, 380);
            pauseJournalBody.alignment = TextAnchor.UpperLeft;
            pauseJournalBody.rectTransform.sizeDelta = new Vector2(380, 260);
            pauseJournalBody.verticalOverflow = VerticalWrapMode.Overflow;
            ButtonOn(pauseJournalPage.transform, "Back", new Vector2(0, -180), () => ShowPausePage("root"), 200, 36);

            pauseSettingsPage = new GameObject("Settings", typeof(RectTransform));
            pauseSettingsPage.transform.SetParent(pausePanel.transform, false);
            Stretch(pauseSettingsPage.GetComponent<RectTransform>(), 20, 20, 20, 70);
            MakeText(pauseSettingsPage.transform, "Settings", new Vector2(0, 150), 20, Palette.Text, 380).alignment = TextAnchor.MiddleCenter;
            MakeText(pauseSettingsPage.transform, "Look sensitivity", new Vector2(0, 86), 14, Palette.Muted, 380).alignment = TextAnchor.MiddleCenter;
            ButtonOn(pauseSettingsPage.transform, "−", new Vector2(-90, 44), () =>
            {
                PlayerSettings.NudgeLookSensitivity(-PlayerSettings.LookSensitivityStep);
                RefreshPauseSettings();
            }, 44, 36);
            pauseSensitivity = MakeText(pauseSettingsPage.transform, PlayerSettings.LookSensitivityLabel(), new Vector2(0, 44), 20, Palette.Text, 80);
            pauseSensitivity.alignment = TextAnchor.MiddleCenter;
            ButtonOn(pauseSettingsPage.transform, "+", new Vector2(90, 44), () =>
            {
                PlayerSettings.NudgeLookSensitivity(PlayerSettings.LookSensitivityStep);
                RefreshPauseSettings();
            }, 44, 36);
            pauseInvert = MakeText(pauseSettingsPage.transform, "", new Vector2(0, -20), 14, Palette.Text, 380);
            pauseInvert.alignment = TextAnchor.MiddleCenter;
            ButtonOn(pauseSettingsPage.transform, "Invert look Y", new Vector2(0, -64), () =>
            {
                PlayerSettings.InvertY = !PlayerSettings.InvertY;
                RefreshPauseSettings();
            }, 220, 36);
            ButtonOn(pauseSettingsPage.transform, "Back", new Vector2(0, -180), () => ShowPausePage("root"), 200, 36);

            pauseQuitPage = new GameObject("Quit", typeof(RectTransform));
            pauseQuitPage.transform.SetParent(pausePanel.transform, false);
            Stretch(pauseQuitPage.GetComponent<RectTransform>(), 20, 20, 20, 70);
            MakeText(pauseQuitPage.transform, "Leave the inn?", new Vector2(0, 80), 20, Palette.Text, 380).alignment = TextAnchor.MiddleCenter;
            MakeText(pauseQuitPage.transform, "Unsaved progress is lost.", new Vector2(0, 36), 14, Palette.Muted, 380).alignment = TextAnchor.MiddleCenter;
            ButtonOn(pauseQuitPage.transform, "Quit game", new Vector2(0, -30), () => game.QuitGame(), 220, 40);
            ButtonOn(pauseQuitPage.transform, "Cancel", new Vector2(0, -86), () => ShowPausePage("root"), 200, 36);

            pauseOverlay.SetActive(false);
            RefreshPauseSettings();

            Refresh(true);
        }

        public bool PointerOverHud()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = Input.mousePosition };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            foreach (var hit in hits)
            {
                if (hit.gameObject == null) continue;
                var canvas = hit.gameObject.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.sortingOrder >= 50) return true;
            }

            return false;
        }

        public void Refresh(bool force = false)
        {
            money.text = $"Cash ${state.Money}";
            clock.text = GameState.FormatClock(state.Hour);
            tod.text = GameState.TimeOfDayLabel(state.Hour);
            day.text = $"Day {state.Day}";
            queue.text = $"In {Economy.CountAtDesk(state)} · Out {Economy.CountWaitingCheckout(state)}";
            reputation.text = $"Rep {state.Reputation}";
            vacancy.text = state.VacancyOpen ? "VACANCY" : "NO VACANCY";
            radio.text = Media.RadioHudText(state);
            inventory.text = InventorySystem.HudSummary(state);

            string shelterLine = "";
            if (Stage.ShowShelterHud(state))
            {
                shelterLine = Shelter.HudSummary(state);
                if (!string.IsNullOrEmpty(shelterLine) && state.Story != null)
                {
                    shelterLine += $" · Humanity: {state.Story.Humanity}%";
                }
            }

            shelter.text = shelterLine;
            shelter.gameObject.SetActive(!string.IsNullOrEmpty(shelterLine));

            bool showTasks = Stage.IsStageOne(state);
            taskFold.SetVisible(showTasks);
            if (showTasks)
            {
                taskFold.SetSubtitle(Stage.HudSummary(state));
                taskLines.text = Stage.HudBody(state);
            }

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
            shopFold.SetSubtitle(state.VacancyOpen ? "VACANCY" : "NO VACANCY");

            var lines = new List<string>();
            foreach (var message in state.Messages) lines.Add("• " + message);
            logBox.SetText(lines.Count == 0 ? "No activity yet." : string.Join("\n", lines));
            logFold.SetSubtitle(state.Messages.Count == 0 ? "" : TrimPreview(state.Messages[0], 42));

            if (state.Shelter != null && state.Shelter.Unlocked && !pcShelterRows)
            {
                BuildPcRows();
            }

            if (state.PcOpen && officePage == "supplies") RefreshPc();
            pcPanel.SetActive(state.PcOpen);

            if (state.DeskGuest != null) RefreshDesk();
            else
            {
                deskQuestionGuest = null;
                deskQuestionStamp = -1;
            }

            deskPanel.SetActive(state.DeskGuest != null);

            radioPanel.SetActive(state.MediaOpen == "radio");
            paperPanel.SetActive(state.MediaOpen == "paper");
            if (state.MediaOpen == "phone") RefreshPhone();
            phonePanel.SetActive(state.MediaOpen == "phone");
            if (state.MediaOpen == "deskpc") RefreshDeskPc();
            deskPcPanel.SetActive(state.MediaOpen == "deskpc");

            if (state.PauseMenuOpen)
            {
                pauseJournalBody.text = Stage.JournalBody(state);
                pauseOverlay.SetActive(true);
            }
            else
            {
                pauseOverlay.SetActive(false);
                pausePage = "root";
            }
        }

        public void ShowPauseMenu()
        {
            ShowPausePage("root");
            pauseJournalBody.text = Stage.JournalBody(state);
            pauseOverlay.SetActive(true);
        }

        public void HidePauseMenu()
        {
            pauseOverlay.SetActive(false);
            pausePage = "root";
        }

        public bool HandlePauseEscape()
        {
            if (pausePage != "root")
            {
                ShowPausePage("root");
                return true;
            }

            return false;
        }

        void ShowPausePage(string page)
        {
            pausePage = page;
            pauseRootPage.SetActive(page == "root");
            pauseJournalPage.SetActive(page == "journal");
            pauseSettingsPage.SetActive(page == "settings");
            pauseQuitPage.SetActive(page == "quit");
            if (page == "journal") pauseJournalBody.text = Stage.JournalBody(state);
            if (page == "settings") RefreshPauseSettings();
        }

        void RefreshPauseSettings()
        {
            if (pauseSensitivity != null) pauseSensitivity.text = PlayerSettings.LookSensitivityLabel();
            if (pauseInvert != null)
            {
                pauseInvert.text = PlayerSettings.InvertY
                    ? "Vertical look: inverted"
                    : "Vertical look: normal";
            }
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

        public void ShowPhone()
        {
            RefreshPhone();
            phonePanel.SetActive(true);
        }

        public void HidePhone()
        {
            phonePanel.SetActive(false);
        }

        void RefreshPhone()
        {
            phoneRequests.text = "No guest requests yet.\nUse the desk PC to check guests in and out.";
        }

        public void ShowDeskPc()
        {
            ShowDeskPcPage("home");
            RefreshDeskPc();
            deskPcPanel.SetActive(true);
        }

        public void HideDeskPc()
        {
            deskPcPanel.SetActive(false);
            deskPcPage = "home";
        }

        public void ShowDeskPcPage(string page)
        {
            deskPcPage = page ?? "home";
            if (deskPcHomePage != null) deskPcHomePage.SetActive(deskPcPage == "home");
            if (deskPcCheckoutPage != null) deskPcCheckoutPage.SetActive(deskPcPage == "checkout");
            if (deskPcBookPage != null) deskPcBookPage.SetActive(deskPcPage == "book");
            RefreshDeskPc();
        }

        void RefreshDeskPc()
        {
            int checkIn = Economy.CountAtDesk(state);
            int checkOut = Economy.CountWaitingCheckout(state);
            if (deskPcCheckInBtn != null)
            {
                deskPcCheckInBtn.interactable = checkIn > 0;
                deskPcCheckInBtn.GetComponentInChildren<Text>().text = $"Check in ({checkIn})";
            }

            if (deskPcCheckOutBtn != null)
            {
                deskPcCheckOutBtn.interactable = checkOut > 0;
                deskPcCheckOutBtn.GetComponentInChildren<Text>().text = $"Check out ({checkOut})";
            }

            var leaving = Economy.FirstWaitingCheckout(state);
            if (deskPcCheckoutInfo != null)
            {
                if (leaving == null)
                {
                    deskPcCheckoutInfo.text = "Nobody is waiting to check out.";
                    if (deskPcCheckoutAction != null) deskPcCheckoutAction.interactable = false;
                }
                else
                {
                    deskPcCheckoutInfo.text =
                        $"{leaving.Name} is at the desk. Process payment and complete their stay — they will walk to their car.";
                    if (deskPcCheckoutAction != null) deskPcCheckoutAction.interactable = true;
                }
            }

            var lines = new List<string> { "Occupied rooms." };
            int staying = 0;
            foreach (var room in state.Rooms)
            {
                if (!room.Unlocked || room.Status != "occupied") continue;
                staying++;
                string hours = room.StayRemainingHours != null
                    ? $"{room.StayRemainingHours.Value:0}h left"
                    : "staying";
                lines.Add($"Room {room.Id}: {room.GuestName} ({hours})");
            }

            if (staying == 0) lines.Add("No guests staying.");
            lines.Add("");
            lines.Add("Supplies and hiring still go through the office PC.");
            if (deskPcLog != null) deskPcLog.text = string.Join("\n", lines);
        }

        void RefreshDesk()
        {
            var guest = state.DeskGuest;
            if (guest == null) return;

            deskName.text = guest.Name;
            deskClaim.text = guest.Claim;

            if (guest.Replies.Count == 0)
            {
                deskReplyBox.SetText("Ask something. Their answer stays here — not just in the log.");
            }
            else
            {
                var lines = new List<string>();
                foreach (var reply in guest.Replies)
                {
                    lines.Add($"You: {reply.Prompt}\n{guest.Name}: “{reply.Spoken}”");
                }

                deskReplyBox.SetText(string.Join("\n\n", lines));
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
                $"Guests staying: {why.Occupants}"
            };
            if (why.Shelter != null && !Stage.IsStageOne(state))
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

            deskWhy.text = string.Join("\n", rows);

            deskAdmit.interactable = why.BunksFree > 0;
            deskAdmit.GetComponentInChildren<Text>().text = why.BunksFree == 0 ? "No room ready" : "Admit";

            int stamp = guest.QuestionsAsked * 17 + guest.AskedQuestionIds.Count + (why.QuestionsLeft > 0 ? 1 : 0);
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
            bool showQuestions = canAsk && questions.Count > 0;
            LayoutDeskConversation(showQuestions);

            if (!showQuestions)
            {
                deskAskHint.text = canAsk ? "Nothing left to ask." : "They stop answering.";
                return;
            }

            deskAskHint.text = "";
            float y = 36f;
            int shown = Mathf.Min(3, questions.Count);
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
                questionButtons.Add(button.gameObject);
                y -= 38f;
            }
        }

        void LayoutDeskConversation(bool showQuestions)
        {
            deskQuestionsRoot.gameObject.SetActive(showQuestions);
            var replyRt = deskReplyBox.Root.GetComponent<RectTransform>();
            if (showQuestions)
            {
                replyRt.anchoredPosition = new Vector2(0f, 2f);
                replyRt.sizeDelta = new Vector2(580f, 110f);
            }
            else
            {
                replyRt.anchoredPosition = new Vector2(0f, -70f);
                replyRt.sizeDelta = new Vector2(580f, 220f);
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

        static GameObject PageOn(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), 0, 0, 0, 0);
            return go;
        }

        public void ResetOfficePc()
        {
            ShowOfficePage("menu");
        }

        void ShowOfficePage(string page)
        {
            officePage = page;
            if (pcMenuPage != null) pcMenuPage.SetActive(page == "menu");
            if (pcSuppliesPage != null) pcSuppliesPage.SetActive(page == "supplies");
            if (pcHirePage != null) pcHirePage.SetActive(page == "hire");
        }

        Foldout MakeFoldout(
            string name,
            Transform parent,
            Vector2 anchor,
            Vector2 pos,
            Vector2 closedSize,
            Vector2 openSize,
            string title,
            Vector2 pivot)
        {
            var root = Panel(name, parent, anchor, pos, closedSize);
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.pivot = pivot;
            rootRt.anchoredPosition = pos;
            root.AddComponent<RectMask2D>();
            var header = ButtonOn(root.transform, title + "  ▼", Vector2.zero, () => { }, closedSize.x - 12f, 32f);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.5f, 1f);
            headerRt.anchorMax = new Vector2(0.5f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -4f);

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(root.transform, false);
            var bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(8f, 8f);
            bodyRt.offsetMax = new Vector2(-8f, -44f);
            body.SetActive(false);

            var fold = new Foldout(root, body.transform, header.GetComponentInChildren<Text>(), title, closedSize, openSize);
            header.onClick.AddListener(fold.Toggle);
            fold.SetOpen(false);
            return fold;
        }

        ScrollBox MakeScrollBox(Transform parent, Vector2 pos, Vector2 size)
        {
            var root = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            root.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.7f);

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(root.transform, false);
            Stretch(viewport.GetComponent<RectTransform>(), 6, 6, 6, 6);

            var content = new GameObject("Content", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 20f);
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = content.GetComponent<Text>();
            text.font = font;
            text.fontSize = 13;
            text.color = Palette.Text;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var scroll = root.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return new ScrollBox(root, text, contentRt);
        }

        static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static string TrimPreview(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
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
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        sealed class Foldout
        {
            readonly GameObject root;
            readonly Transform body;
            readonly Text header;
            readonly string title;
            readonly Vector2 closedSize;
            readonly Vector2 openSize;
            string subtitle = "";

            public Transform Body => body;
            public bool Open { get; private set; }

            public void SetVisible(bool visible)
            {
                root.SetActive(visible);
            }

            public Foldout(GameObject root, Transform body, Text header, string title, Vector2 closedSize, Vector2 openSize)
            {
                this.root = root;
                this.body = body;
                this.header = header;
                this.title = title;
                this.closedSize = closedSize;
                this.openSize = openSize;
            }

            public void Toggle() => SetOpen(!Open);

            public void SetOpen(bool open)
            {
                Open = open;
                body.gameObject.SetActive(open);
                root.GetComponent<RectTransform>().sizeDelta = open ? openSize : closedSize;
                PaintHeader();
            }

            public void SetSubtitle(string value)
            {
                subtitle = value ?? "";
                PaintHeader();
            }

            void PaintHeader()
            {
                string arrow = Open ? "▲" : "▼";
                if (Open || string.IsNullOrEmpty(subtitle)) header.text = $"{title}  {arrow}";
                else header.text = $"{title}  {arrow}   {subtitle}";
            }
        }

        sealed class ScrollBox
        {
            public readonly GameObject Root;
            readonly Text text;
            readonly RectTransform content;

            public ScrollBox(GameObject root, Text text, RectTransform content)
            {
                Root = root;
                this.text = text;
                this.content = content;
            }

            public void SetText(string value)
            {
                text.text = value ?? "";
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }
        }
    }
}

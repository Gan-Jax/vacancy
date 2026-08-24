using System.Collections.Generic;
using UnityEngine;

namespace Vacancy
{
    public sealed class VacancyGame : MonoBehaviour
    {
        HotelLayout layout;
        GameState state;
        PlayerActor player;
        StaffNpc bob;
        StaffNpc mary;
        HotelView3D view;
        HudView hud;
        Camera playerCam;
        readonly GameInput input = new GameInput();
        bool bannerOpen;
        bool inspectMode;
        string lastPin;
        Vector3? lastPinWorld;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (Object.FindAnyObjectByType<VacancyGame>() != null) return;
            var go = new GameObject("Vacancy");
            go.AddComponent<VacancyGame>();
        }

        void Start()
        {
            layout = HotelLayout.Create();
            state = GameState.Create(layout.RoomCount);
            player = new PlayerActor(layout.Spawn.X + 40, layout.Spawn.Y);
            player.Yaw = 180f;

            var problems = layout.Validate();
            if (problems.Count > 0)
            {
                foreach (var problem in problems)
                {
                    Debug.LogWarning("[floorplan] " + problem);
                    state.AddLog("Layout problem: " + problem);
                }
            }
            else
            {
                state.AddLog($"{layout.Floor.Name}: {layout.RoomCount} rooms, every door reachable.");
            }

            playerCam = BuildCamera();
            view = new HotelView3D(layout, transform, playerCam);
            hud = new HudView(state, this, transform);
        }

        void Update()
        {
            if (state == null) return;
            bool look = !state.Paused && !AnyModalOpen() && !bannerOpen && Input.GetMouseButton(1);
            input.Poll(look);
            ApplyCursor(look);
            float dt = Mathf.Min(0.05f, Time.deltaTime);

            if (input.EscapePressed)
            {
                if (state.PauseMenuOpen)
                {
                    if (!hud.HandlePauseEscape()) ResumeFromMenu();
                }
                else if (state.PcOpen) ClosePc();
                else if (state.MediaOpen == "radio") CloseRadio();
                else if (state.MediaOpen == "paper") ClosePaper();
                else if (state.MediaOpen == "phone") ClosePhone();
                else if (state.MediaOpen == "deskpc") CloseDeskPc();
                else if (state.DeskGuest != null) CloseDeskReview();
                else OpenPauseMenu();
            }

            if (input.VacancyPressed && !AnyModalOpen()) ToggleVacancy();

            if (input.InspectPressed && !AnyModalOpen() && !state.PauseMenuOpen && !bannerOpen)
            {
                inspectMode = !inspectMode;
                state.AddLog(
                    inspectMode
                        ? "Inspect mode ON — click a spot (cursor unlocked) to pin it. X to exit."
                        : "Inspect mode OFF.");
            }

            if (!bannerOpen)
            {
                var pending = Story.TakeBanner(state);
                if (pending != null)
                {
                    hud.ShowBanner(pending);
                    bannerOpen = true;
                }
            }

            if (input.ReinforcePressed && state.Shelter != null && state.Shelter.Unlocked)
            {
                Shelter.ReinforceBarricades(state, 1);
            }

            if (!state.Paused && !AnyModalOpen() && !bannerOpen)
            {
                var staff = StaffList();
                Economy.AdvanceTime(state, dt, layout, staff);

                var result = player.Update(input, dt, layout, state.Rooms);
                if (result != null)
                {
                    if (result.Type == "inspect") Economy.FinishInspection(state, result.Room);
                    if (result.Type == "repair") Economy.FinishRepair(state, result.Room);
                    if (result.Type == "clean") Economy.FinishCleaning(state, result.Room);
                    result.Room.Worker = null;
                }

                bob?.Update(dt, state, layout);
                mary?.Update(dt, state, layout);

                if (input.InteractPressed) HandleInteract();
            }

            view.Refresh(state, player, StaffList());
            if (inspectMode && !AnyModalOpen() && !state.Paused) UpdateInspectPin(input.ClickPressed);
            else view?.SetInspect(false, null, null);
            hud.Refresh();
        }

        void LateUpdate()
        {
            if (player == null || playerCam == null) return;
            playerCam.transform.position = WorldScale.ToWorld(player.X, player.Y, WorldScale.EyeHeight + player.FootY);
            playerCam.transform.rotation = Quaternion.Euler(player.Pitch, player.Yaw, 0f);
            view?.SyncPlayer(player, Time.deltaTime);
            UpdateInteractHover();
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        bool AnyModalOpen()
        {
            return state.PcOpen || state.DeskGuest != null || !string.IsNullOrEmpty(state.MediaOpen);
        }

        void UpdateInteractHover()
        {
            if (view == null || player == null || playerCam == null || state == null)
            {
                return;
            }

            if (state.Paused || state.PauseMenuOpen || AnyModalOpen() || bannerOpen || inspectMode)
            {
                view.SetInteractHover(null);
                return;
            }

            var ray = playerCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var marker = PickInteractHover(ray);
            if (marker == null)
            {
                view.SetInteractHover(null);
                return;
            }

            WorldScale.FromWorld(marker.Anchor, out float hx, out float hy);
            if (Geometry.Dist(player.X, player.Y, hx, hy) > 120f)
            {
                view.SetInteractHover(null);
                return;
            }

            view.SetInteractHover(marker);
        }

        InteractHover PickInteractHover(Ray ray)
        {
            InteractHover best = null;
            float bestDist = float.MaxValue;
            int bestPri = int.MaxValue;
            ConsiderHits(Physics.RaycastAll(ray, 24f), ref best, ref bestDist, ref bestPri);
            ConsiderHits(Physics.SphereCastAll(ray.origin, 0.18f, ray.direction, 24f), ref best, ref bestDist, ref bestPri);
            return best;
        }

        void ConsiderHits(RaycastHit[] hits, ref InteractHover best, ref float bestDist, ref int bestPri)
        {
            if (hits == null) return;
            foreach (var hit in hits)
            {
                if (hit.collider == null || IsCharacterHit(hit.collider)) continue;
                var marker = hit.collider.GetComponent<InteractHover>()
                    ?? hit.collider.GetComponentInParent<InteractHover>();
                if (marker == null) continue;

                int pri = HoverPriority(marker.Kind);
                float d = hit.distance;
                bool closer = d + 0.001f < bestDist - 0.4f;
                bool similar = Mathf.Abs(d - bestDist) <= 0.4f;
                if (best == null || closer || (similar && pri < bestPri) || (similar && pri == bestPri && d < bestDist))
                {
                    best = marker;
                    bestDist = d;
                    bestPri = pri;
                }
            }
        }

        static int HoverPriority(string kind)
        {
            switch (kind)
            {
                case "radio":
                case "newspaper":
                case "phone":
                case "deskpc":
                case "sign":
                    return 0;
                case "office":
                    return 1;
                case "desk":
                    return 2;
                case "room":
                    return 3;
                default:
                    return 4;
            }
        }

        void HandleInteract()
        {
            if (state.PcOpen || player.ActiveTask != null) return;

            var target = player.GetInteractTarget(state.Rooms, layout, StaffList());
            if (target == null) return;

            if (target.Kind == "radio")
            {
                OpenRadio();
                return;
            }

            if (target.Kind == "newspaper")
            {
                OpenPaper();
                return;
            }

            if (target.Kind == "phone")
            {
                OpenPhone();
                return;
            }

            if (target.Kind == "deskpc")
            {
                OpenDeskPc();
                return;
            }

            if (target.Kind == "desk")
            {
                Economy.HandleDeskAction(state, layout, StaffList());
                return;
            }

            if (target.Kind == "office")
            {
                OpenPc();
                return;
            }

            if (target.Kind == "sign")
            {
                ToggleVacancy();
                return;
            }

            var room = target.Room;
            if (!room.Unlocked)
            {
                Room nextLocked = null;
                foreach (var r in state.Rooms)
                {
                    if (!r.Unlocked)
                    {
                        nextLocked = r;
                        break;
                    }
                }

                if (nextLocked != null && room.Id != nextLocked.Id)
                {
                    state.AddLog($"Unlock Room {nextLocked.Id} first.");
                }
                else
                {
                    UnlockNextRoom();
                }

                return;
            }

            if (room.Worker != null && room.Worker != "player") return;

            if (room.Status == "needs_inspection")
            {
                player.StartTask("inspect", room);
                state.AddLog($"Inspecting Room {room.Id}...");
            }
            else if (room.Status == "dirty")
            {
                player.StartTask("clean", room);
                state.AddLog(
                    $"Cleaning Room {room.Id} ({room.DirtLevel}, {GameConfig.GetCleanHours(room.DirtLevel)}h)...");
            }
            else if (room.Status == "needs_repair")
            {
                int cost = Economy.GetRepairCost(state, room.RepairLevel);
                int? paid = Economy.BeginRepairPayment(state, room);
                if (paid == null)
                {
                    state.AddLog($"Need ${cost} for {room.RepairLevel} repair parts on Room {room.Id}.");
                    return;
                }

                player.StartTask("repair", room);
                state.AddLog(
                    $"Repairing Room {room.Id} ({room.RepairLevel}, {GameConfig.GetRepairHours(room.RepairLevel)}h, −${cost})...");
            }
            else if (room.Status == "clean")
            {
                state.AddLog($"Room {room.Id} is ready for guests.");
            }
            else if (room.Status == "occupied")
            {
                state.AddLog($"{room.GuestName} is still staying in Room {room.Id}.");
            }
        }

        public void OpenPauseMenu()
        {
            if (state.PauseMenuOpen || AnyModalOpen() || bannerOpen) return;
            state.PauseMenuOpen = true;
            state.Paused = true;
            Time.timeScale = 0f;
            hud.ShowPauseMenu();
        }

        public void ResumeFromMenu()
        {
            state.PauseMenuOpen = false;
            Time.timeScale = 1f;
            if (!AnyModalOpen()) state.Paused = false;
            hud.HidePauseMenu();
        }

        public void QuitGame()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void AddDebugCash()
        {
            state.Money += 500;
            state.AddLog("Debug: +$500 cash.");
        }

        public void SkipDay()
        {
            int startDay = state.Day;
            bool wasPaused = state.Paused;
            float scale = Time.timeScale;
            state.Paused = false;
            Time.timeScale = 1f;
            var staff = StaffList();
            for (int i = 0; i < 4000 && state.Day == startDay; i++)
            {
                Economy.AdvanceTime(state, 0.05f, layout, staff);
            }

            state.Paused = wasPaused;
            Time.timeScale = scale;
        }

        public void ToggleVacancy()
        {
            Economy.ToggleVacancy(state);
        }

        public void UnlockNextRoom()
        {
            Economy.UnlockRoom(state);
        }

        public void HireBob()
        {
            if (!Economy.HireBob(state)) return;
            bob = StaffNpc.SpawnAtHome(layout, new StaffProfile
            {
                Id = "bob",
                Name = "Bob",
                Role = "repair",
                Color = "#ffb347",
                Department = "maintenance"
            });
        }

        public void HireMary()
        {
            if (!Economy.HireMary(state)) return;
            mary = StaffNpc.SpawnAtHome(layout, new StaffProfile
            {
                Id = "mary",
                Name = "Mary",
                Role = "housekeeping",
                Color = "#e8a0bf",
                Department = "housekeeping"
            });
        }

        public void OpenPc()
        {
            Stage.Mark(state, "officePc");
            state.PcOpen = true;
            state.Paused = true;
            hud.ResetOfficePc();
        }

        public void ClosePc()
        {
            state.PcOpen = false;
            hud.ResetOfficePc();
            if (!state.PauseMenuOpen && state.DeskGuest == null && string.IsNullOrEmpty(state.MediaOpen)) state.Paused = false;
        }

        public void PlacePcOrder(Dictionary<string, int> quantities)
        {
            InventorySystem.PlaceOrder(state, quantities);
        }

        public void OpenRadio()
        {
            state.MediaOpen = "radio";
            state.Paused = true;
            hud.ShowRadio();
        }

        public void CloseRadio()
        {
            state.MediaOpen = null;
            if (!state.PauseMenuOpen && !state.PcOpen && state.DeskGuest == null) state.Paused = false;
            hud.HideRadio();
        }

        public void OpenPaper()
        {
            var papers = state.Story?.Media?.Papers;
            if (papers != null && papers.Count > 0 && !papers[0].Read)
            {
                Media.MarkPaperRead(state);
                state.AddLog(Stage.PaperReadLog(state));
            }

            state.MediaOpen = "paper";
            state.Paused = true;
            hud.ShowPaper();
        }

        public void ClosePaper()
        {
            state.MediaOpen = null;
            if (!state.PauseMenuOpen && !state.PcOpen && state.DeskGuest == null) state.Paused = false;
            hud.HidePaper();
        }

        public void OpenPhone()
        {
            state.MediaOpen = "phone";
            state.Paused = true;
            hud.ShowPhone();
        }

        public void ClosePhone()
        {
            state.MediaOpen = null;
            hud.HidePhone();
            if (!state.PauseMenuOpen && !state.PcOpen && state.DeskGuest == null) state.Paused = false;
        }

        public void ReviewArrivalFromDeskPc()
        {
            if (Economy.FirstAtDesk(state) == null) return;
            state.MediaOpen = null;
            hud.HideDeskPc();
            OpenDeskReview();
        }

        public void ProcessDeskPcCheckout()
        {
            Economy.CheckOutAtDesk(state, layout);
            if (Economy.CountWaitingCheckout(state) == 0) hud.ShowDeskPcPage("home");
        }

        public void OpenDeskPc()
        {
            state.MediaOpen = "deskpc";
            state.Paused = true;
            hud.ShowDeskPc();
        }

        public void CloseDeskPc()
        {
            state.MediaOpen = null;
            hud.HideDeskPc();
            if (!state.PauseMenuOpen && !state.PcOpen && state.DeskGuest == null) state.Paused = false;
        }

        public void OpenDeskReview()
        {
            var atDesk = Economy.FirstAtDesk(state);
            if (atDesk == null) return;
            state.DeskGuest = atDesk;
            state.Paused = true;
        }

        public void CloseDeskReview()
        {
            state.DeskGuest = null;
            if (!state.PauseMenuOpen && !state.PcOpen && string.IsNullOrEmpty(state.MediaOpen)) state.Paused = false;
        }

        public void AdmitDeskGuest()
        {
            var guest = state.DeskGuest;
            if (guest == null) return;
            Economy.CheckInAtDesk(state, layout, guest);
            CloseDeskReview();
        }

        public void RefuseDeskGuest()
        {
            var guest = state.DeskGuest;
            if (guest == null) return;
            Arrivals.RefuseArrival(state, guest, layout);
            Economy.BeginWalkOut(state, layout, guest);
            CloseDeskReview();
        }

        public void AskDeskQuestion(MediaQuestion question)
        {
            if (state.DeskGuest == null) return;
            Arrivals.AskQuestion(state, state.DeskGuest, question);
        }

        public void DismissBanner()
        {
            hud.HideBanner();
            bannerOpen = false;
        }

        List<StaffNpc> StaffList()
        {
            var list = new List<StaffNpc>();
            if (bob != null) list.Add(bob);
            if (mary != null) list.Add(mary);
            return list;
        }

        Camera BuildCamera()
        {
            var existing = Camera.main;
            var cam = existing != null ? existing : new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = false;
            cam.fieldOfView = 72f;
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 120f;
            cam.backgroundColor = Palette.HudBg;
            cam.clearFlags = CameraClearFlags.SolidColor;
            if (cam.GetComponent<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }

            return cam;
        }

        void UpdateInspectPin(bool clicked)
        {
            if (playerCam == null || view == null || layout == null) return;
            var ray = playerCam.ScreenPointToRay(Input.mousePosition);
            string hover = lastPin;
            Vector3? world = lastPinWorld;
            var hits = Physics.RaycastAll(ray, 90f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (IsCharacterHit(hit.collider)) continue;
                WorldScale.FromWorld(hit.point, out float x, out float y);
                int floor = hit.point.y < -WorldScale.FloorDepth * 0.45f ? -1 : 0;
                hover = HotelLayout.FormatPin(layout.AreaLabelAt(x, y, floor), x, y);
                world = hit.point + Vector3.up * 0.04f;
                if (clicked && (hud == null || !hud.PointerOverHud()))
                {
                    lastPin = hover;
                    lastPinWorld = world;
                    state.AddLog(hover);
                }

                break;
            }

            view.SetInspect(true, hover, world);
        }

        static bool IsCharacterHit(Collider collider)
        {
            if (collider == null) return false;
            var t = collider.transform;
            while (t != null)
            {
                string n = t.name;
                if (n == "Player" || n.StartsWith("Char")) return true;
                t = t.parent;
            }

            return false;
        }

        static void ApplyCursor(bool look)
        {
            Cursor.lockState = look ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !look;
        }
    }
}

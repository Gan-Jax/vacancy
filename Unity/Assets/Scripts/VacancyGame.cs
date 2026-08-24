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
            bool look = !state.Paused && !AnyModalOpen() && !bannerOpen;
            input.Poll(look);
            ApplyCursor(look);
            float dt = Mathf.Min(0.05f, Time.deltaTime);

            if (input.EscapePressed)
            {
                if (state.PcOpen) ClosePc();
                else if (state.MediaOpen == "radio") CloseRadio();
                else if (state.MediaOpen == "paper") ClosePaper();
                else if (state.DeskGuest != null) CloseDeskReview();
            }

            if (input.PausePressed && !AnyModalOpen())
            {
                state.Paused = !state.Paused;
                state.AddLog(state.Paused ? "Game paused." : "Game resumed.");
            }

            if (input.VacancyPressed && !AnyModalOpen()) ToggleVacancy();

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
            hud.Refresh();
        }

        void LateUpdate()
        {
            if (player == null || playerCam == null) return;
            playerCam.transform.position = WorldScale.ToWorld(player.X, player.Y, WorldScale.EyeHeight);
            playerCam.transform.rotation = Quaternion.Euler(player.Pitch, player.Yaw, 0f);
        }

        void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        bool AnyModalOpen()
        {
            return state.PcOpen || state.DeskGuest != null || !string.IsNullOrEmpty(state.MediaOpen);
        }

        void HandleInteract()
        {
            if (state.PcOpen || player.ActiveTask != null) return;
            bool deskQueue = state.WaitingGuests.Count > 0;
            foreach (var guest in state.ActiveGuests)
            {
                if (guest.Phase == "waiting_checkout")
                {
                    deskQueue = true;
                    break;
                }
            }

            var target = player.GetInteractTarget(state.Rooms, layout, StaffList(), deskQueue);
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

            if (target.Kind == "desk")
            {
                string result = Economy.HandleDeskAction(state, layout, StaffList());
                if (result == Economy.DeskReview) OpenDeskReview();
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

        public void AddDebugCash()
        {
            state.Money += 500;
            state.AddLog("Debug: +$500 cash.");
        }

        public void SkipDay()
        {
            int startDay = state.Day;
            bool wasPaused = state.Paused;
            state.Paused = false;
            var staff = StaffList();
            for (int i = 0; i < 4000 && state.Day == startDay; i++)
            {
                Economy.AdvanceTime(state, 0.05f, layout, staff);
            }

            state.Paused = wasPaused;
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
            state.PcOpen = true;
            state.Paused = true;
        }

        public void ClosePc()
        {
            state.PcOpen = false;
            if (state.DeskGuest == null && string.IsNullOrEmpty(state.MediaOpen)) state.Paused = false;
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
            if (!state.PcOpen && state.DeskGuest == null) state.Paused = false;
            hud.HideRadio();
        }

        public void OpenPaper()
        {
            var papers = state.Story?.Media?.Papers;
            if (papers != null && papers.Count > 0 && !papers[0].Read)
            {
                Media.MarkPaperRead(state);
                state.AddLog("You read today's paper. New questions are available at the desk.");
            }

            state.MediaOpen = "paper";
            state.Paused = true;
            hud.ShowPaper();
        }

        public void ClosePaper()
        {
            state.MediaOpen = null;
            if (!state.PcOpen && state.DeskGuest == null) state.Paused = false;
            hud.HidePaper();
        }

        public void OpenDeskReview()
        {
            if (state.WaitingGuests.Count == 0) return;
            state.DeskGuest = state.WaitingGuests[0];
            state.Paused = true;
        }

        public void CloseDeskReview()
        {
            state.DeskGuest = null;
            if (!state.PcOpen && string.IsNullOrEmpty(state.MediaOpen)) state.Paused = false;
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
            Arrivals.RefuseArrival(state, guest);
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

        static void ApplyCursor(bool look)
        {
            Cursor.lockState = look ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !look;
        }
    }
}

using System.Collections.Generic;

namespace Vacancy
{
    /// <summary>
    /// Stage 1 is a normal roadside hotel. Stage 2 waits until the tutorial
    /// is done AND the 7th room is unlocked. Day 7 is not the gate.
    /// Mirrors js/stage.js.
    /// </summary>
    public sealed class TutorialProgress
    {
        public bool CheckIn;
        public bool VacancySign;
        public bool RoomWork;
        public bool HireStaff;
        public bool OfficePc;
    }

    public static class Stage
    {
        public const int RoomGate = 7;

        public static readonly (string Id, string Label)[] Objectives =
        {
            ("checkIn", "Check in a guest"),
            ("vacancySign", "Flip the vacancy sign"),
            ("roomWork", "Inspect or clean a room"),
            ("hireStaff", "Hire Bob or Mary"),
            ("officePc", "Open the office PC")
        };

        public static bool IsStageOne(GameState state)
        {
            return (state?.Stage ?? 1) < 2;
        }

        public static bool TutorialComplete(GameState state)
        {
            if (state?.Tutorial == null) return false;
            return state.Tutorial.CheckIn &&
                   state.Tutorial.VacancySign &&
                   state.Tutorial.RoomWork &&
                   state.Tutorial.HireStaff &&
                   state.Tutorial.OfficePc;
        }

        public static int UnlockedRoomCount(GameState state)
        {
            if (state?.Rooms == null) return 0;
            int count = 0;
            foreach (var room in state.Rooms)
            {
                if (room.Unlocked) count++;
            }

            return count;
        }

        public static bool SeventhRoomUnlocked(GameState state)
        {
            return UnlockedRoomCount(state) >= RoomGate;
        }

        public static int TutorialCompletedCount(GameState state)
        {
            if (state?.Tutorial == null) return 0;
            int count = 0;
            if (state.Tutorial.CheckIn) count++;
            if (state.Tutorial.VacancySign) count++;
            if (state.Tutorial.RoomWork) count++;
            if (state.Tutorial.HireStaff) count++;
            if (state.Tutorial.OfficePc) count++;
            return count;
        }

        public static bool ShowShelterHud(GameState state)
        {
            return !IsStageOne(state) && state.Shelter != null && state.Shelter.Unlocked;
        }

        public static bool Mark(GameState state, string id)
        {
            if (state?.Tutorial == null)
            {
                MaybeAdvance(state);
                return false;
            }

            if (!TryGet(state.Tutorial, id, out bool already)) return false;
            if (!already) Set(state.Tutorial, id, true);
            MaybeAdvance(state);
            return !already;
        }

        public static bool MaybeAdvance(GameState state)
        {
            if (state == null || !IsStageOne(state)) return false;
            if (!TutorialComplete(state) || !SeventhRoomUnlocked(state)) return false;

            state.Stage = 2;
            state.AddLog("This is no longer only a hotel.");
            if (state.Story != null)
            {
                state.Story.Banner = new StoryBanner
                {
                    Title = "This is no longer only a hotel",
                    Body = "The roadside inn is still taking guests. The road ahead will not stay this simple.",
                    Act = string.IsNullOrEmpty(state.Story.Act) ? "normalcy" : state.Story.Act
                };
            }

            return true;
        }

        public static string HudSummary(GameState state)
        {
            if (!IsStageOne(state)) return "";
            return $"Today's tasks {TutorialCompletedCount(state)}/{Objectives.Length} · rooms {UnlockedRoomCount(state)}/{RoomGate}";
        }

        public static string HudBody(GameState state)
        {
            if (!IsStageOne(state) || state.Tutorial == null) return "";
            return ObjectiveLines(state, true);
        }

        public static string JournalBody(GameState state)
        {
            var lines = new List<string>();
            lines.Add(IsStageOne(state) ? "Today's work at the inn." : "The inn is running.");
            lines.Add("");
            lines.Add(ObjectiveLines(state, IsStageOne(state)));
            return string.Join("\n", lines);
        }

        static string ObjectiveLines(GameState state, bool includeNote)
        {
            var lines = new List<string>();
            if (state?.Tutorial != null)
            {
                foreach (var item in Objectives)
                {
                    TryGet(state.Tutorial, item.Id, out bool done);
                    lines.Add((done ? "[x] " : "[ ] ") + item.Label);
                }
            }

            int rooms = state?.Rooms?.Count ?? RoomGate;
            lines.Add($"Rooms open: {UnlockedRoomCount(state)} / {(IsStageOne(state) ? RoomGate : rooms)}");
            if (includeNote)
            {
                string note = HudNote(state);
                if (!string.IsNullOrEmpty(note)) lines.Add(note);
            }

            return string.Join("\n", lines);
        }

        public static string HudNote(GameState state)
        {
            if (!IsStageOne(state)) return "";
            if (TutorialComplete(state) && !SeventhRoomUnlocked(state))
            {
                return "Tasks done. Unlock Room 7.";
            }

            if (!TutorialComplete(state) && SeventhRoomUnlocked(state))
            {
                return "Room 7 is open. Finish today's tasks.";
            }

            return "";
        }

        public static string PaperReadLog(GameState state)
        {
            return IsStageOne(state)
                ? "You read today's paper."
                : "You read today's paper. New questions are available at the desk.";
        }

        static bool TryGet(TutorialProgress tutorial, string id, out bool value)
        {
            switch (id)
            {
                case "checkIn":
                    value = tutorial.CheckIn;
                    return true;
                case "vacancySign":
                    value = tutorial.VacancySign;
                    return true;
                case "roomWork":
                    value = tutorial.RoomWork;
                    return true;
                case "hireStaff":
                    value = tutorial.HireStaff;
                    return true;
                case "officePc":
                    value = tutorial.OfficePc;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        static void Set(TutorialProgress tutorial, string id, bool value)
        {
            switch (id)
            {
                case "checkIn":
                    tutorial.CheckIn = value;
                    break;
                case "vacancySign":
                    tutorial.VacancySign = value;
                    break;
                case "roomWork":
                    tutorial.RoomWork = value;
                    break;
                case "hireStaff":
                    tutorial.HireStaff = value;
                    break;
                case "officePc":
                    tutorial.OfficePc = value;
                    break;
            }
        }
    }
}

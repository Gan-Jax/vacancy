using System.Collections.Generic;

namespace Vacancy
{
    public sealed class Room
    {
        public int Id;
        public bool Unlocked;
        public string Status = "clean";
        public string GuestName;
        public float? StayRemainingHours;
        public int? StayDays;
        public int? PaymentsLeft;
        public float? NextIntervalPaymentIn;
        public bool HasHiddenDamage;
        public bool DamageFound;
        public string DirtLevel;
        public string RepairLevel;
        public bool RepairPaid;
        public int? RepairCost;
        public float CleanProgress;
        public float InspectProgress;
        public float RepairProgress;
        public string Worker;
        public int StayCount;
        public int StaysSinceTowel;
        public int TpDayCounter;
    }

    public sealed class GuestSign
    {
        public string Text;
        public bool Damning;
        public bool Revealed;
    }

    public sealed class GuestReply
    {
        public string Prompt;
        public string Spoken;
        public string Source;
    }

    public sealed class WaitingGuest : IMover
    {
        public string Name;
        public string Kind = GuestKind.Traveler;
        public string StoryId;
        public string Claim;
        public readonly List<GuestSign> Signs = new List<GuestSign>();
        public int QuestionsAsked;
        public int MaxQuestions = 2;
        public readonly List<string> AskedQuestionIds = new List<string>();
        public float WaitRemainingHours;
        public bool Marked;
        public string Tell;
        public readonly List<GuestReply> Replies = new List<GuestReply>();
        public float X { get; set; }
        public float Y { get; set; }
        public float Radius { get; set; } = 11f;
        public List<Point> Path { get; set; } = new List<Point>();
        public float StallSeconds { get; set; }
        public int FloorLevel { get; set; }
        public float FootY { get; set; }
        public string ArrivePhase;
        public int StallIndex = -1;
        public string CarColor;
        public bool BoughtPaper;
        public bool PaperOffered;
    }

    public sealed class GuestCar
    {
        public string Owner;
        public int StallIndex;
        public float X;
        public float Y;
        public string Color;
        public string Stage = "inbound";
        public int Waypoint;
        public List<Point> Path = new List<Point>();
    }

    public sealed class Guest : IMover
    {
        public string Name;
        public string Kind = GuestKind.Traveler;
        public bool Marked;
        public string Phase;
        public string Nav;
        public int RoomId;
        public float X { get; set; }
        public float Y { get; set; }
        public float Radius { get; set; } = 11f;
        public List<Point> Path { get; set; } = new List<Point>();
        public float TargetX;
        public float TargetY;
        public int StayDays;
        public float StayRemainingHours;
        public int PaymentsLeft;
        public float NextIntervalPaymentIn;
        public bool HasHiddenDamage;
        public float? WaitRemainingHours;
        public int? ReputationBonus;
        public bool UpsetCheckout;
        public float StallSeconds { get; set; }
        public int FloorLevel { get; set; }
        public float FootY { get; set; }
        public int StallIndex = -1;
        public string CarColor;
        public bool BoughtPaper;
        public bool PaperOffered;
        public float? PaperTripIn;
        public float? RequestRollIn;
        public bool HasRequested;
        public float? WalkaboutIn;
        public bool DidWalkabout;
        public float WalkLingerSeconds;
    }

    public sealed class GuestRequest
    {
        public string Id;
        public int RoomId;
        public string GuestName;
        public string Kind;
        public string Label;
        public string SupplyId;
        public float HoursLeft;
    }

    public sealed class GameState
    {
        public int Money;
        public int Day = 1;
        public float Hour = 8f;
        public int Reputation;
        public readonly List<WaitingGuest> WaitingGuests = new List<WaitingGuest>();
        public readonly List<Guest> ActiveGuests = new List<Guest>();
        public readonly List<GuestCar> Cars = new List<GuestCar>();
        public bool VacancyOpen = false;
        public bool Paused;
        public bool PauseMenuOpen;
        public bool BobHired;
        public bool MaryHired;
        public InventoryState Inventory;
        public StoryState Story;
        public ShelterState Shelter;
        public int Stage = 1;
        public TutorialProgress Tutorial;
        public bool PcOpen;
        public WaitingGuest DeskGuest;
        public string MediaOpen;
        public readonly List<Room> Rooms = new List<Room>();
        public readonly List<string> Messages = new List<string>();
        public readonly List<GuestRequest> Requests = new List<GuestRequest>();
        public int NextRequestId = 1;

        public static GameState Create(int roomCount)
        {
            var state = new GameState
            {
                Money = GameConfig.StartingMoney,
                Reputation = GameConfig.StartingReputation,
                Inventory = InventorySystem.Create(),
                Story = global::Vacancy.Story.Create(),
                Shelter = global::Vacancy.Shelter.Create(),
                Stage = 1,
                Tutorial = new TutorialProgress(),
                VacancyOpen = false
            };

            for (int i = 0; i < roomCount; i++)
            {
                state.Rooms.Add(new Room
                {
                    Id = i + 1,
                    Unlocked = i < GameConfig.StartingUnlockedRooms,
                    Status = "clean"
                });
            }

            state.AddLog("Welcome to the roadside inn. Check guests in and out at the desk PC.");
            state.AddLog("Use the office PC to order supplies or hire help.");
            return state;
        }

        public void AddLog(string text)
        {
            Messages.Insert(0, text);
            if (Messages.Count > 30) Messages.RemoveRange(30, Messages.Count - 30);
        }

        public static string FormatClock(float hour)
        {
            int totalMinutes = (int)((((hour % 24f) + 24f) % 24f) * 60f);
            int h24 = (totalMinutes / 60) % 24;
            int minutes = totalMinutes % 60;
            string period = h24 >= 12 ? "PM" : "AM";
            int h12 = h24 % 12;
            if (h12 == 0) h12 = 12;
            return $"{h12}:{minutes:00} {period}";
        }

        public static string TimeOfDayLabel(float hour)
        {
            float h = ((hour % 24f) + 24f) % 24f;
            if (h >= 5 && h < 12) return "Morning";
            if (h >= 12 && h < 17) return "Afternoon";
            if (h >= 17 && h < 21) return "Evening";
            return "Night";
        }

        public int RoomUnlockCost()
        {
            int unlocked = 0;
            foreach (var room in Rooms)
            {
                if (room.Unlocked) unlocked++;
            }

            return GameConfig.RoomUnlockBaseCost + unlocked * GameConfig.RoomUnlockCostStep;
        }
    }
}

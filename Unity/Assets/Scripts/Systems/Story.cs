using System;
using System.Collections.Generic;

namespace Vacancy
{
    public sealed class StoryStats
    {
        public int CheckIns;
        public int CheckOuts;
        public int TurnedAway;
        public int MarkedServed;
        public int MarkedRefused;
        public int Repairs;
        public int Orders;
    }

    public sealed class StoryBanner
    {
        public string Title;
        public string Body;
        public string Act;
    }

    public sealed class StoryDispatch
    {
        public int Day;
        public string Text;
    }

    public sealed class PendingThreat
    {
        public string Name;
        public int AdmittedDay;
        public int FireOnDay;
    }

    public sealed class PendingVindication
    {
        public string Name;
        public int Day;
    }

    public sealed class StoryState
    {
        public string Act = "normalcy";
        public readonly Dictionary<string, int> Fired = new Dictionary<string, int>();
        public readonly Dictionary<string, bool> Flags = new Dictionary<string, bool>();
        public float Tension;
        public int Humanity = 100;
        public readonly List<PendingThreat> PendingThreats = new List<PendingThreat>();
        public readonly List<PendingVindication> PendingVindication = new List<PendingVindication>();
        public int ThreatsRefused;
        public float HintIn = GameConfig.HintIntervalByAct["normalcy"];
        public readonly List<StoryDispatch> Dispatches = new List<StoryDispatch>();
        public MediaState Media;
        public StoryBanner Banner;
        public readonly StoryStats Stats = new StoryStats();
    }

    public static class Story
    {
        public static readonly string[] ActOrder =
        {
            "normalcy", "unease", "disruption", "collapse", "shelter"
        };

        public static readonly Dictionary<string, string> ActLabels = new Dictionary<string, string>
        {
            { "normalcy", "Quiet season" },
            { "unease", "Something off" },
            { "disruption", "The city goes dark" },
            { "collapse", "No one is coming" },
            { "shelter", "Shelter" }
        };

        static readonly Dictionary<string, string[]> Hints = new Dictionary<string, string[]>
        {
            {
                "normalcy",
                new[]
                {
                    "A trucker says the interstate was backed up for two hours over nothing.",
                    "The ice machine hums all night. You have started noticing it.",
                    "A guest asks whether the city is always that bright at 3 AM.",
                    "Someone left a road atlas on the counter with three towns circled.",
                    "The dog at the lot next door barked until dawn, then stopped.",
                    "Long distance call comes in for a room that checked out last week."
                }
            },
            {
                "unease",
                new[]
                {
                    "Radio bulletin: three counties ask residents to limit non-essential travel.",
                    "A guest checks in with no luggage and pays for two nights in cash.",
                    "Supply truck is late again. The dispatcher does not pick up.",
                    "The glow over the city looks wrong tonight — orange where it should be white.",
                    "A family asks if the hotel has a basement. They do not explain.",
                    "Someone has been filling water jugs from the outdoor spigot at night.",
                    "Two rooms cancel within an hour of each other. Same reason: 'roads'."
                }
            },
            {
                "disruption",
                new[]
                {
                    "Emergency broadcast repeats a phrase and then cuts to static.",
                    "Power browns out for nine seconds. Every clock in the building disagrees now.",
                    "A guest returns from the city with the windows of their car taped over.",
                    "No sirens tonight. That is new, and it is worse.",
                    "Mary asks whether her family can stay in an empty room. She is not joking.",
                    "The vending machine is empty and nobody is coming to refill it.",
                    "Someone scratched a symbol into the door of Room 7. It was not there yesterday."
                }
            },
            {
                "collapse",
                new[]
                {
                    "Headlights on the access road slow, then keep going. They saw the sign.",
                    "The city has been dark for so long you stopped looking that direction.",
                    "A voice on the radio reads names for an hour, then apologizes and stops.",
                    "Something moved along the fence line. It did not move like a person.",
                    "A knock at 4 AM. By the time you reach the door, there is only a bag on the step.",
                    "The tap runs brown for a minute before it clears. Ration it anyway."
                }
            },
            {
                "shelter",
                new[]
                {
                    "Someone wrote the day count on the lobby wall. The number is wrong by two.",
                    "A child asks when the guests are coming back. Nobody answers.",
                    "Fuel gauge on the generator drops faster than the math says it should.",
                    "Two people argue over a bunk assignment. It ends when the lights flicker.",
                    "You catch yourself checking the barricades instead of the front desk."
                }
            }
        };

        static readonly string[] Tells =
        {
            "Their reflection in the lobby glass lags a half-second behind them.",
            "They sign the register with the wrong year and do not correct it.",
            "They are dressed for winter. It is not winter.",
            "They repeat your last words back to you, quietly, before answering.",
            "Their bag is empty. You can tell by the way it hangs.",
            "They do not blink while you explain the checkout time.",
            "They ask for a room facing away from the road."
        };

        public static StoryState Create()
        {
            return new StoryState
            {
                Media = Media.Create()
            };
        }

        public static string CurrentAct(GameState state)
        {
            return state.Story != null ? state.Story.Act : "normalcy";
        }

        public static int ActIndex(GameState state)
        {
            int index = Array.IndexOf(ActOrder, CurrentAct(state));
            return Math.Max(0, index);
        }

        public static bool IsShelterEra(GameState state)
        {
            return ActIndex(state) >= Array.IndexOf(ActOrder, "collapse");
        }

        public static bool HasFlag(GameState state, string flag)
        {
            return state.Story != null &&
                   state.Story.Flags.TryGetValue(flag, out var value) &&
                   value;
        }

        public static void SetFlag(GameState state, string flag, bool value = true)
        {
            if (state.Story == null) return;
            state.Story.Flags[flag] = value;
        }

        static void AddDispatch(GameState state, string text)
        {
            state.Story.Dispatches.Insert(0, new StoryDispatch { Day = state.Day, Text = text });
            if (state.Story.Dispatches.Count > 24)
            {
                state.Story.Dispatches.RemoveRange(24, state.Story.Dispatches.Count - 24);
            }
        }

        static float HintInterval(GameState state)
        {
            float baseHours = GameConfig.HintIntervalByAct.TryGetValue(CurrentAct(state), out var value)
                ? value
                : GameConfig.HintIntervalByAct["normalcy"];
            return baseHours * (0.6f + GameRng.NextFloat() * 0.8f);
        }

        static void FireHint(GameState state)
        {
            string act = CurrentAct(state);
            if (!Hints.TryGetValue(act, out var pool)) pool = Hints["normalcy"];
            var unseen = new List<string>();
            foreach (var line in pool)
            {
                if (!HasFlag(state, "hint:" + line)) unseen.Add(line);
            }

            var options = unseen.Count > 0 ? unseen : new List<string>(pool);
            string chosen = options[GameRng.NextInt(0, options.Count - 1)];
            SetFlag(state, "hint:" + chosen);
            state.AddLog(chosen);
            AddDispatch(state, chosen);
        }

        static void FireKeystone(GameState state, Keystone keystone)
        {
            state.Story.Fired[keystone.Id] = state.Day;
            if (keystone.AdvanceTo != null)
            {
                state.Story.Act = keystone.AdvanceTo;
                state.Story.HintIn = HintInterval(state);
            }

            state.Story.Banner = new StoryBanner
            {
                Title = keystone.Title,
                Body = keystone.Body,
                Act = CurrentAct(state)
            };

            state.AddLog($"{keystone.Title} — {keystone.Body}");
            keystone.OnFire?.Invoke(state);
        }

        static bool CheckKeystones(GameState state)
        {
            foreach (var keystone in Keystones)
            {
                if (state.Story.Fired.ContainsKey(keystone.Id)) continue;
                if (keystone.Act != null && keystone.Act != CurrentAct(state)) continue;
                if (!keystone.When(state, state.Story.Stats)) continue;
                FireKeystone(state, keystone);
                return true;
            }

            return false;
        }

        public static void Update(GameState state, float hoursPassed)
        {
            if (state.Story == null) return;

            state.Story.HintIn -= hoursPassed;
            if (state.Story.HintIn <= 0)
            {
                state.Story.HintIn = HintInterval(state);
                FireHint(state);
            }

            CheckKeystones(state);
        }

        public static void Hook(GameState state, string hook, WaitingGuest waiting = null, Guest guest = null, Room room = null)
        {
            if (state.Story == null) return;
            var stats = state.Story.Stats;

            switch (hook)
            {
                case "checkIn":
                    stats.CheckIns += 1;
                    if (waiting != null && waiting.Marked)
                    {
                        stats.MarkedServed += 1;
                        state.Story.Tension += 4;
                        state.AddLog(
                            $"{waiting.Name} signs the register slowly, like the name is unfamiliar.");
                    }

                    break;
                case "checkOut":
                    stats.CheckOuts += 1;
                    break;
                case "turnAway":
                    stats.TurnedAway += 1;
                    if (waiting != null && waiting.Marked) stats.MarkedRefused += 1;
                    if (IsShelterEra(state)) state.Story.Tension += 3;
                    break;
                case "unlock":
                    if (IsShelterEra(state))
                    {
                        state.AddLog("Another room opened up. Another four people off the road.");
                    }

                    break;
                case "order":
                    stats.Orders += 1;
                    break;
                case "repair":
                    stats.Repairs += 1;
                    break;
            }

            CheckKeystones(state);
        }

        public static WaitingGuest MaybeMarkArrival(GameState state, WaitingGuest guest)
        {
            if (state.Story == null) return guest;
            float chance = GameConfig.MarkedChanceByAct.TryGetValue(CurrentAct(state), out var value)
                ? value
                : GameConfig.MarkedChanceByAct["normalcy"];
            if (GameRng.NextFloat() >= chance) return guest;

            guest.Marked = true;
            if (ActIndex(state) >= Array.IndexOf(ActOrder, "unease"))
            {
                guest.Tell = Tells[GameRng.NextInt(0, Tells.Length - 1)];
            }

            return guest;
        }

        public static string DescribeArrival(WaitingGuest guest)
        {
            if (guest == null) return "";
            if (!string.IsNullOrEmpty(guest.Tell)) return $"{guest.Name} — {guest.Tell}";
            return guest.Name;
        }

        public static string SignalText(GameState state)
        {
            var latest = state.Story?.Media?.RadioLog.Count > 0 ? state.Story.Media.RadioLog[0] : null;
            if (latest != null)
            {
                string text = latest.Headline;
                return text.Length > 64 ? text.Substring(0, 61) + "..." : text;
            }

            if (CurrentAct(state) == "normalcy") return "Local AM — weather, roads";
            return ActLabels.TryGetValue(CurrentAct(state), out var label) ? label : CurrentAct(state);
        }

        public static StoryBanner TakeBanner(GameState state)
        {
            var banner = state.Story?.Banner;
            if (banner == null) return null;
            state.Story.Banner = null;
            return banner;
        }

        sealed class Keystone
        {
            public string Id;
            public string Act;
            public string AdvanceTo;
            public Func<GameState, StoryStats, bool> When;
            public string Title;
            public string Body;
            public Action<GameState> OnFire;
        }

        static readonly Keystone[] Keystones =
        {
            new Keystone
            {
                Id = "first-strange-guest",
                Act = "normalcy",
                When = (state, s) => state.Day >= 3 && s.CheckIns >= 3,
                Title = "A guest who does not sleep",
                Body = "Room 2 kept the light on all night and left before dawn. The bed was made. The key was on the pillow, still cold.",
                OnFire = state =>
                {
                    SetFlag(state, "sawStrangeGuest");
                    AddDispatch(state, "Local radio: 'minor outage' in the north districts.");
                }
            },
            new Keystone
            {
                Id = "advance-unease",
                Act = "normalcy",
                AdvanceTo = "unease",
                When = (state, s) => state.Day >= 5 && s.CheckIns >= 5 && HasFlag(state, "sawStrangeGuest"),
                Title = "The roads get quieter",
                Body = "Fewer headlights on the access road. The ones that come through do not ask about rates — they ask how far the next town is.",
                OnFire = state =>
                {
                    AddDispatch(state, "Advisory: avoid travel into the city until further notice.");
                }
            },
            new Keystone
            {
                Id = "delivery-slips",
                Act = "unease",
                When = (state, s) => s.Orders >= 2 && state.Day >= 7,
                Title = "Your supplier stops answering",
                Body = "The order goes through. The confirmation does not. Whoever normally drives out here has not been heard from in two days.",
                OnFire = state =>
                {
                    SetFlag(state, "supplyUnreliable");
                    AddDispatch(state, "Freight lines suspended on the western corridor.");
                }
            },
            new Keystone
            {
                Id = "advance-disruption",
                Act = "unease",
                AdvanceTo = "disruption",
                When = (state, s) => state.Day >= 10 && (s.CheckIns >= 8 || s.TurnedAway >= 2),
                Title = "The city goes dark",
                Body = "At 11:40 the glow on the horizon goes out, block by block, like someone walking a hallway turning off lights. Then the phones stop working.",
                OnFire = state =>
                {
                    Shelter.UnlockShelterSystems(state);
                    SetFlag(state, "shelterUnlocked");
                    AddDispatch(state, "…stay indoors… do not approach… repeat, do not app—");
                    state.AddLog("The office PC now lists water, food, fuel, medicine, and lumber.");
                }
            },
            new Keystone
            {
                Id = "first-survivor",
                Act = "disruption",
                When = (state, s) => state.Day >= 12,
                Title = "Not a guest",
                Body = "A woman walks up the access road with no car and no bag. She does not ask the rate. She asks if the doors lock from the inside.",
                OnFire = state =>
                {
                    SetFlag(state, "firstSurvivor");
                    state.Story.Tension += 8;
                }
            },
            new Keystone
            {
                Id = "advance-collapse",
                Act = "disruption",
                AdvanceTo = "collapse",
                When = (state, s) => state.Day >= 15 && HasFlag(state, "firstSurvivor"),
                Title = "No one is coming",
                Body = "No broadcast tonight. No traffic. Whatever is happening out there has finished happening to the city, and it is working its way outward.",
                OnFire = state =>
                {
                    SetFlag(state, "defenseMatters");
                    Shelter.ActivateDefense(state);
                    state.AddLog(
                        "Money is worth less than lumber now. Keep the barricades up (R) and the generator fed.");
                }
            },
            new Keystone
            {
                Id = "advance-shelter",
                Act = "collapse",
                AdvanceTo = "shelter",
                When = (state, s) => state.Day >= 19,
                Title = "This is a shelter now",
                Body = "Somebody moved the front desk against the door and nobody moved it back. Rooms are bunks. Guests are survivors. Your job is that they stay alive.",
                OnFire = state => { SetFlag(state, "shelterDeclared"); }
            }
        };
    }
}

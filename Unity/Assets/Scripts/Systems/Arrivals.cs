using System;
using System.Collections.Generic;

namespace Vacancy
{
    public static class GuestKind
    {
        public const string Traveler = "traveler";
        public const string Survivor = "survivor";
        public const string Wrong = "wrong";
    }

    public sealed class ArrivalAssessment
    {
        public int BunksFree;
        public int BunksTotal;
        public int Occupants;
        public int SignsRevealed;
        public int QuestionsLeft;
        public ShelterAssessment Shelter;
        public int Humanity;
        public bool PaysRent;
    }

    public sealed class ShelterAssessment
    {
        public int WaterDays;
        public int WaterDaysAfter;
        public int FoodDays;
        public int FoodDaysAfter;
        public int Integrity;
        public bool Powered;
    }

    public static class Arrivals
    {
        const float QuestionHours = 0.75f;

        static readonly string[] Stage1Claims =
        {
            "Driving through. Needs a bed and an early checkout.",
            "Says the next motel is another two hours and they are done driving.",
            "Here for work in the city. Paying by the night.",
            "Asked about the weekly rate, then decided on one night."
        };

        static readonly string[] Stage1Signs =
        {
            "They set a suitcase down and keep a hand on the handle.",
            "They count the cash twice before they speak.",
            "They ask what time the office opens in the morning."
        };

        static readonly Dictionary<string, string[]> Claims = new Dictionary<string, string[]>
        {
            {
                GuestKind.Traveler,
                new[]
                {
                    "Driving through. Needs a bed and an early checkout.",
                    "Says the next motel is another two hours and they are done driving.",
                    "Here for work in the city. Paying by the night.",
                    "Wants the quietest room you have. Does not say why."
                }
            },
            {
                GuestKind.Survivor,
                new[]
                {
                    "Walked here. Asks how many people are already inside.",
                    "Came from the east side. Will not talk about what they saw.",
                    "Has a child with them. Asks only for somewhere with a door.",
                    "Offers to work for a bunk. No money left."
                }
            },
            {
                GuestKind.Wrong,
                new[]
                {
                    "Says they have a reservation. There is no reservation.",
                    "Asks how many people are inside. Asks again, differently.",
                    "Says they came from the city. The roads from the city are closed.",
                    "Wants a room on the ground floor, near the back."
                }
            }
        };

        static readonly string[] DamningSigns =
        {
            "Their reflection in the lobby glass lags a half-second behind them.",
            "They sign the register with the wrong year and do not correct it.",
            "They repeat your last words back to you, quietly, before answering.",
            "They do not blink while you explain the checkout time.",
            "Their bag is empty. You can tell by the way it hangs.",
            "They know Mary's name. Mary has not said it.",
            "They are not breathing when they think you are not looking."
        };

        static readonly string[] InnocuousSigns =
        {
            "They are dressed for winter. It is not winter.",
            "They ask for a room facing away from the road.",
            "They pay entirely in coins.",
            "They flinch at the ice machine.",
            "They will not put their bag down.",
            "They ask twice whether the doors lock from the inside.",
            "They keep checking the window behind you.",
            "They have not slept. It shows."
        };

        public static readonly MediaQuestion[] GenericQuestions =
        {
            new MediaQuestion
            {
                Id = "register-name",
                Source = "generic",
                Prompt = "What name should I put on the register?",
                Answers = new Dictionary<string, string>
                {
                    { GuestKind.Traveler, "The name on my license is fine." },
                    { GuestKind.Survivor, "Just my first name. I do not want it written down twice." },
                    { GuestKind.Wrong, "Any name. Names are for registers." }
                }
            },
            new MediaQuestion
            {
                Id = "how-long",
                Source = "generic",
                Prompt = "How long do you plan to stay?",
                Answers = new Dictionary<string, string>
                {
                    { GuestKind.Traveler, "One night. Maybe two if the roads are still bad." },
                    { GuestKind.Survivor, "Until it is safe. I do not know when that is." },
                    { GuestKind.Wrong, "As long as you will let me. I can stay in any room." }
                }
            },
            new MediaQuestion
            {
                Id = "how-pay",
                Source = "generic",
                Prompt = "How are you paying?",
                Answers = new Dictionary<string, string>
                {
                    { GuestKind.Traveler, "Cash. One night up front, same as the last place." },
                    { GuestKind.Survivor, "I do not have money. I can work if you need hands." },
                    { GuestKind.Wrong, "I can pay. I have what people pay with." }
                }
            },
            new MediaQuestion
            {
                Id = "where-from",
                Source = "generic",
                Prompt = "Where did you come from?",
                Answers = new Dictionary<string, string>
                {
                    { GuestKind.Traveler, "Down the access road. I have been driving since morning." },
                    { GuestKind.Survivor, "On foot. I do not want to say the last town out loud." },
                    { GuestKind.Wrong, "The city. Everyone comes from the city." }
                }
            }
        };

        static T Pick<T>(IList<T> list)
        {
            return list[GameRng.NextInt(0, list.Count - 1)];
        }

        static string PickKind(GameState state)
        {
            if (Stage.IsStageOne(state)) return GuestKind.Traveler;
            int act = Story.ActIndex(state);
            float roll = GameRng.NextFloat();
            int unease = Array.IndexOf(Story.ActOrder, "unease");
            int disruption = Array.IndexOf(Story.ActOrder, "disruption");

            if (act <= unease)
            {
                if (roll < (act == 0 ? 0.05f : 0.14f)) return GuestKind.Wrong;
                return GuestKind.Traveler;
            }

            if (act == disruption)
            {
                if (roll < 0.24f) return GuestKind.Wrong;
                if (roll < 0.62f) return GuestKind.Survivor;
                return GuestKind.Traveler;
            }

            if (roll < 0.3f) return GuestKind.Wrong;
            return GuestKind.Survivor;
        }

        static List<GuestSign> BuildSigns(string kind)
        {
            var signs = new List<GuestSign>();
            var damningPool = new List<string>(DamningSigns);
            var innocuousPool = new List<string>(InnocuousSigns);

            void Take(List<string> pool, bool damning)
            {
                if (pool.Count == 0) return;
                int index = GameRng.NextInt(0, pool.Count - 1);
                string text = pool[index];
                pool.RemoveAt(index);
                signs.Add(new GuestSign { Text = text, Damning = damning, Revealed = false });
            }

            if (kind == GuestKind.Wrong)
            {
                int damningCount = 1 + GameRng.NextInt(0, 2);
                for (int i = 0; i < damningCount; i++) Take(damningPool, true);
                if (GameRng.NextFloat() < 0.5f) Take(innocuousPool, false);
            }
            else
            {
                int innocuousCount = GameRng.NextInt(0, 2);
                for (int i = 0; i < innocuousCount; i++) Take(innocuousPool, false);
                if (GameRng.NextFloat() < 0.16f) Take(damningPool, true);
            }

            for (int i = signs.Count - 1; i > 0; i--)
            {
                int j = GameRng.NextInt(0, i);
                var tmp = signs[i];
                signs[i] = signs[j];
                signs[j] = tmp;
            }

            if (signs.Count > 0) signs[0].Revealed = true;
            return signs;
        }

        static List<GuestSign> BuildStage1Signs()
        {
            var signs = new List<GuestSign>();
            if (GameRng.NextFloat() >= 0.45f) return signs;
            signs.Add(new GuestSign
            {
                Text = Pick(Stage1Signs),
                Damning = false,
                Revealed = true
            });
            return signs;
        }

        public static WaitingGuest CreateArrival(GameState state, string name)
        {
            if (Stage.IsStageOne(state))
            {
                var ordinary = new WaitingGuest
                {
                    Name = name,
                    Kind = GuestKind.Traveler,
                    StoryId = null,
                    Claim = Pick(Stage1Claims),
                    WaitRemainingHours = GameConfig.WaitPatienceHours,
                    Marked = false
                };
                ordinary.Signs.AddRange(BuildStage1Signs());
                return ordinary;
            }

            string kind = PickKind(state);
            var story = Media.PickTiedStory(state, kind);
            var guest = new WaitingGuest
            {
                Name = name,
                Kind = kind,
                StoryId = story?.Id,
                Claim = Pick(Claims[kind]),
                WaitRemainingHours = GameConfig.WaitPatienceHours,
                Marked = kind != GuestKind.Traveler
            };
            guest.Signs.AddRange(BuildSigns(kind));
            return guest;
        }

        public static List<MediaQuestion> DeskQuestions(GameState state, WaitingGuest guest)
        {
            if (Stage.IsStageOne(state))
            {
                var askedIds = new HashSet<string>(guest?.AskedQuestionIds ?? new List<string>());
                var basics = new List<MediaQuestion>();
                foreach (var q in GenericQuestions)
                {
                    if (!askedIds.Contains(q.Id)) basics.Add(q);
                }

                return basics;
            }

            var mediaQs = Media.AvailableQuestions(state, guest);
            var asked = new HashSet<string>(guest?.AskedQuestionIds ?? new List<string>());
            var list = new List<MediaQuestion>();
            foreach (var q in mediaQs)
            {
                string key = q.StoryId != null ? $"{q.StoryId}:{q.Id}" : q.Id;
                if (!asked.Contains(key)) list.Add(q);
            }

            foreach (var q in GenericQuestions)
            {
                if (!asked.Contains(q.Id)) list.Add(q);
            }

            return list;
        }

        public static List<GuestSign> RevealedSigns(WaitingGuest guest)
        {
            var list = new List<GuestSign>();
            if (guest?.Signs == null) return list;
            foreach (var sign in guest.Signs)
            {
                if (sign.Revealed) list.Add(sign);
            }

            return list;
        }

        public static int HiddenSignCount(WaitingGuest guest)
        {
            int count = 0;
            if (guest?.Signs == null) return 0;
            foreach (var sign in guest.Signs)
            {
                if (!sign.Revealed) count++;
            }

            return count;
        }

        public static bool HasVisibleTell(WaitingGuest guest)
        {
            foreach (var sign in RevealedSigns(guest))
            {
                if (sign.Damning) return true;
            }

            return false;
        }

        public static ArrivalAssessment AssessArrival(GameState state, WaitingGuest guest)
        {
            int bunksFree = 0;
            int bunksTotal = 0;
            foreach (var room in state.Rooms)
            {
                if (!room.Unlocked) continue;
                bunksTotal++;
                if (room.Status == "clean") bunksFree++;
            }

            var assessment = new ArrivalAssessment
            {
                BunksFree = bunksFree,
                BunksTotal = bunksTotal,
                Occupants = Shelter.CountOccupants(state),
                SignsRevealed = RevealedSigns(guest).Count,
                QuestionsLeft = Math.Max(0, guest.MaxQuestions - guest.QuestionsAsked),
                Humanity = state.Story?.Humanity ?? 100,
                PaysRent = guest.Kind == GuestKind.Traveler
            };

            if (state.Shelter != null && state.Shelter.Unlocked && !Stage.IsStageOne(state))
            {
                int now = assessment.Occupants;
                int after = now + 1;
                int Days(int stock, float perPerson)
                {
                    return perPerson > 0 ? (int)Math.Floor(stock / (perPerson * Math.Max(1, now))) : 0;
                }

                int DaysAfter(int stock, float perPerson)
                {
                    return perPerson > 0 ? (int)Math.Floor(stock / (perPerson * Math.Max(1, after))) : 0;
                }

                assessment.Shelter = new ShelterAssessment
                {
                    WaterDays = Days(Shelter.GetStock(state, "water"), GameConfig.WaterPerPersonPerDay),
                    WaterDaysAfter = DaysAfter(Shelter.GetStock(state, "water"), GameConfig.WaterPerPersonPerDay),
                    FoodDays = Days(Shelter.GetStock(state, "food"), GameConfig.FoodPerPersonPerDay),
                    FoodDaysAfter = DaysAfter(Shelter.GetStock(state, "food"), GameConfig.FoodPerPersonPerDay),
                    Integrity = (int)Math.Round(state.Shelter.Integrity),
                    Powered = state.Shelter.Powered
                };
            }

            return assessment;
        }

        public static bool AskQuestion(GameState state, WaitingGuest guest, MediaQuestion question = null)
        {
            if (guest == null) return false;
            if (guest.QuestionsAsked >= guest.MaxQuestions)
            {
                state.AddLog($"{guest.Name} stops answering questions.");
                return false;
            }

            var options = DeskQuestions(state, guest);
            var chosen = question ?? (options.Count > 0 ? options[0] : GenericQuestions[0]);
            guest.QuestionsAsked += 1;
            guest.WaitRemainingHours -= QuestionHours;
            state.Hour += QuestionHours;
            guest.AskedQuestionIds.Add(chosen.StoryId != null ? $"{chosen.StoryId}:{chosen.Id}" : chosen.Id);

            string spoken = chosen.Answers != null
                ? Media.AnswerFor(guest, chosen)
                : $"{guest.Name} answers, and nothing stands out.";

            guest.Replies.Add(new GuestReply
            {
                Prompt = chosen.Prompt,
                Spoken = spoken,
                Source = chosen.Source ?? "generic"
            });

            state.AddLog($"You: \"{chosen.Prompt}\"");
            state.AddLog($"{guest.Name}: \"{spoken}\"");

            if (chosen.Source == "radio")
            {
                state.AddLog("They have heard the radio. Everyone has heard the radio.");
            }

            var hidden = new List<GuestSign>();
            foreach (var sign in guest.Signs)
            {
                if (!sign.Revealed) hidden.Add(sign);
            }

            if (hidden.Count > 0)
            {
                hidden[GameRng.NextInt(0, hidden.Count - 1)].Revealed = true;
            }

            return true;
        }

        public static bool RefuseArrival(GameState state, WaitingGuest guest)
        {
            if (guest == null) return false;
            state.WaitingGuests.Remove(guest);

            var story = state.Story;
            if (guest.Kind == GuestKind.Traveler)
            {
                state.Reputation = Math.Max(0, state.Reputation - 4);
                state.AddLog(
                    $"Turned away {guest.Name}. They had money and somewhere else to be. (−4 reputation)");
            }
            else if (guest.Kind == GuestKind.Survivor)
            {
                if (story != null) story.Humanity = Math.Max(0, story.Humanity - 8);
                state.Reputation = Math.Max(0, state.Reputation - 2);
                state.AddLog(
                    $"Turned away {guest.Name}. They did not argue. They just walked back toward the road.");
            }
            else
            {
                if (story != null)
                {
                    story.Humanity = Math.Max(0, story.Humanity - 2);
                    story.ThreatsRefused += 1;
                    story.PendingVindication.Add(new PendingVindication { Name = guest.Name, Day = state.Day });
                }

                state.AddLog($"Turned away {guest.Name}. They did not ask why.");
            }

            Story.Hook(state, "turnAway", guest);
            return true;
        }

        public static void ArmAdmittedThreat(GameState state, WaitingGuest guest)
        {
            if (guest?.Kind != GuestKind.Wrong) return;
            if (state.Story == null) return;
            state.Story.PendingThreats.Add(new PendingThreat
            {
                Name = guest.Name,
                AdmittedDay = state.Day,
                FireOnDay = state.Day + 1
            });
        }

        public static void ResolveConsequences(GameState state)
        {
            var story = state.Story;
            if (story == null) return;

            bool shelterEra = Story.IsShelterEra(state);
            var stillPending = new List<PendingThreat>();

            foreach (var threat in story.PendingThreats)
            {
                if (state.Day < threat.FireOnDay)
                {
                    stillPending.Add(threat);
                    continue;
                }

                var options = new List<Action>();
                options.Add(() =>
                {
                    Room target = null;
                    foreach (var room in state.Rooms)
                    {
                        if (room.Status == "occupied")
                        {
                            target = room;
                            break;
                        }
                    }

                    if (target != null)
                    {
                        target.Status = "needs_inspection";
                        target.GuestName = null;
                        target.DirtLevel = "heavy";
                        target.HasHiddenDamage = true;
                        state.AddLog(
                            $"Room {target.Id} is empty this morning. The bed was not slept in and the window is open from the outside.");
                    }
                    else
                    {
                        state.AddLog("Something moved through the halls last night. Nothing is missing that you can name.");
                    }
                });

                if (state.Shelter != null && state.Shelter.Unlocked)
                {
                    options.Add(() =>
                    {
                        int taken = Math.Min(Shelter.GetStock(state, "food"), 6);
                        state.Shelter.Stock["food"] -= taken;
                        int water = Math.Min(Shelter.GetStock(state, "water"), 5);
                        state.Shelter.Stock["water"] -= water;
                        state.AddLog(
                            $"Stores were opened overnight. {taken} food and {water} water gone. The lock was not forced.");
                    });
                }

                if (state.Shelter != null && state.Shelter.DefenseActive)
                {
                    options.Add(() =>
                    {
                        state.Shelter.Integrity = Math.Max(0, state.Shelter.Integrity - 30);
                        state.AddLog(
                            $"A barricade was dismantled from the inside overnight ({Math.Round(state.Shelter.Integrity)}%).");
                    });
                }

                Pick(options)();
                state.AddLog($"You think about {threat.Name}, and when you checked them in.");
                if (shelterEra) story.Humanity = Math.Max(0, story.Humanity - 4);
                story.Tension += 10;
            }

            story.PendingThreats.Clear();
            story.PendingThreats.AddRange(stillPending);

            var stillWaitingProof = new List<PendingVindication>();
            foreach (var proof in story.PendingVindication)
            {
                if (state.Day < proof.Day + 2)
                {
                    stillWaitingProof.Add(proof);
                    continue;
                }

                state.AddLog(
                    $"Someone found what was left of {proof.Name} out past the treeline. It had not been a person for a while.");
                story.Humanity = Math.Min(100, story.Humanity + 3);
            }

            story.PendingVindication.Clear();
            story.PendingVindication.AddRange(stillWaitingProof);
        }
    }
}

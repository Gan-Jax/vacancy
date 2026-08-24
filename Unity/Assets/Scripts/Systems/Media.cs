using System.Collections.Generic;

namespace Vacancy
{
    public sealed class RadioEntry
    {
        public string Id;
        public int Day;
        public string Headline;
        public string Body;
        public string Kind;
    }

    public sealed class PaperIssue
    {
        public string Id;
        public int Day;
        public string Headline;
        public string Body;
        public string Kind;
        public bool Read;
    }

    public sealed class MediaQuestion
    {
        public string Id;
        public string Source;
        public string Prompt;
        public Dictionary<string, string> Answers;
        public string StoryId;
        public string StoryKind;
        public bool Tied;
    }

    public sealed class MediaStory
    {
        public string Id;
        public string MinAct;
        public string Kind;
        public RadioEntry Radio;
        public PaperIssue Paper;
        public List<MediaQuestion> Questions;
    }

    public sealed class MediaState
    {
        public readonly List<RadioEntry> RadioLog = new List<RadioEntry>();
        public readonly List<PaperIssue> Papers = new List<PaperIssue>();
        public readonly List<string> AiredIds = new List<string>();
        public readonly List<string> PrintedIds = new List<string>();
        public float RadioIn = 10f;
        public int LastPaperDay;
    }

    public static class Media
    {
        public static MediaState Create()
        {
            return new MediaState();
        }

        static int ActRank(string name)
        {
            int i = System.Array.IndexOf(Story.ActOrder, name);
            return i < 0 ? 0 : i;
        }

        static List<MediaStory> AvailableStories(GameState state)
        {
            if (Stage.IsStageOne(state)) return new List<MediaStory>(Stage1Stories);
            int rank = Story.ActIndex(state);
            var list = new List<MediaStory>();
            foreach (var story in Stories)
            {
                if (ActRank(story.MinAct) <= rank) list.Add(story);
            }

            return list;
        }

        static List<MediaStory> Unpublished(GameState state, string field)
        {
            var used = new HashSet<string>(
                field == "airedIds" ? state.Story.Media.AiredIds : state.Story.Media.PrintedIds);
            var list = new List<MediaStory>();
            foreach (var story in AvailableStories(state))
            {
                if (!used.Contains(story.Id)) list.Add(story);
            }

            return list;
        }

        static float IntervalHours(GameState state)
        {
            var byAct = new Dictionary<string, float>
            {
                { "normalcy", 26f },
                { "unease", 16f },
                { "disruption", 12f },
                { "collapse", 10f },
                { "shelter", 9f }
            };
            float baseHours = byAct.TryGetValue(Story.CurrentAct(state), out var value) ? value : 16f;
            return baseHours * (0.7f + GameRng.NextFloat() * 0.6f);
        }

        static void AirRadio(GameState state, MediaStory story)
        {
            var media = state.Story.Media;
            media.AiredIds.Add(story.Id);
            media.RadioLog.Insert(0, new RadioEntry
            {
                Id = story.Id,
                Day = state.Day,
                Headline = story.Radio.Headline,
                Body = story.Radio.Body,
                Kind = story.Kind
            });
            if (media.RadioLog.Count > 16) media.RadioLog.RemoveRange(16, media.RadioLog.Count - 16);
            state.AddLog($"Radio: {story.Radio.Headline}");
            state.Story.Dispatches.Insert(0, new StoryDispatch { Day = state.Day, Text = story.Radio.Headline });
            if (state.Story.Dispatches.Count > 24)
            {
                state.Story.Dispatches.RemoveRange(24, state.Story.Dispatches.Count - 24);
            }
        }

        static void PrintPaper(GameState state, MediaStory story)
        {
            var media = state.Story.Media;
            media.PrintedIds.Add(story.Id);
            media.Papers.Insert(0, new PaperIssue
            {
                Id = story.Id,
                Day = state.Day,
                Headline = story.Paper.Headline,
                Body = story.Paper.Body,
                Kind = story.Kind,
                Read = false
            });
            if (media.Papers.Count > 12) media.Papers.RemoveRange(12, media.Papers.Count - 12);
            media.LastPaperDay = state.Day;
            state.AddLog($"Today's paper is in the newspaper box. {story.Paper.Headline}");
        }

        public static void Update(GameState state, float hoursPassed)
        {
            if (state.Story?.Media == null) return;
            var media = state.Story.Media;

            media.RadioIn -= hoursPassed;
            if (media.RadioIn <= 0)
            {
                media.RadioIn = IntervalHours(state);
                var unpublished = Unpublished(state, "airedIds");
                if (unpublished.Count > 0) AirRadio(state, unpublished[0]);
            }

            if (state.Day > media.LastPaperDay)
            {
                var unpublished = Unpublished(state, "printedIds");
                if (unpublished.Count > 0) PrintPaper(state, unpublished[0]);
            }
        }

        public static RadioEntry LatestRadio(GameState state)
        {
            return state.Story?.Media?.RadioLog.Count > 0 ? state.Story.Media.RadioLog[0] : null;
        }

        public static PaperIssue LatestPaper(GameState state)
        {
            return state.Story?.Media?.Papers.Count > 0 ? state.Story.Media.Papers[0] : null;
        }

        public static PaperIssue MarkPaperRead(GameState state, string paperId = null)
        {
            var papers = state.Story?.Media?.Papers;
            if (papers == null || papers.Count == 0) return null;
            PaperIssue paper = null;
            if (paperId != null)
            {
                foreach (var item in papers)
                {
                    if (item.Id == paperId)
                    {
                        paper = item;
                        break;
                    }
                }
            }
            else
            {
                paper = papers[0];
            }

            if (paper == null) return null;
            paper.Read = true;
            return paper;
        }

        public static bool HasReadPaper(GameState state, string storyId)
        {
            var papers = state.Story?.Media?.Papers;
            if (papers == null) return false;
            foreach (var paper in papers)
            {
                if (paper.Id == storyId && paper.Read) return true;
            }

            return false;
        }

        public static bool HasHeardRadio(GameState state, string storyId)
        {
            return state.Story?.Media != null && state.Story.Media.AiredIds.Contains(storyId);
        }

        public static MediaStory GetStoryById(string id)
        {
            foreach (var story in Stories)
            {
                if (story.Id == id) return story;
            }

            return null;
        }

        public static List<MediaStory> KnownStories(GameState state)
        {
            var list = new List<MediaStory>();
            foreach (var story in AvailableStories(state))
            {
                if (HasHeardRadio(state, story.Id) || HasReadPaper(state, story.Id)) list.Add(story);
            }

            return list;
        }

        public static List<MediaQuestion> AvailableQuestions(GameState state, WaitingGuest guest)
        {
            if (Stage.IsStageOne(state)) return new List<MediaQuestion>();
            var list = new List<MediaQuestion>();
            foreach (var story in KnownStories(state))
            {
                bool heard = HasHeardRadio(state, story.Id);
                bool read = HasReadPaper(state, story.Id);
                foreach (var q in story.Questions)
                {
                    if (q.Source == "radio" && !heard) continue;
                    if (q.Source == "paper" && !read) continue;
                    list.Add(new MediaQuestion
                    {
                        Id = q.Id,
                        Source = q.Source,
                        Prompt = q.Prompt,
                        Answers = q.Answers,
                        StoryId = story.Id,
                        StoryKind = story.Kind,
                        Tied = guest != null && guest.StoryId == story.Id
                    });
                }
            }

            return list;
        }

        public static string AnswerFor(WaitingGuest guest, MediaQuestion question)
        {
            string kind = guest?.Kind ?? GuestKind.Traveler;
            if (question.Answers.TryGetValue(kind, out var spoken)) return spoken;
            return question.Answers.TryGetValue(GuestKind.Traveler, out var fallback) ? fallback : "";
        }

        public static MediaStory PickTiedStory(GameState state, string kind)
        {
            var recent = new List<MediaStory>();
            var log = state.Story?.Media?.RadioLog;
            if (log == null) return null;
            int take = System.Math.Min(3, log.Count);
            for (int i = 0; i < take; i++)
            {
                var story = GetStoryById(log[i].Id);
                if (story != null && story.Kind == kind) recent.Add(story);
            }

            if (recent.Count == 0) return null;
            if (GameRng.NextFloat() > 0.55f) return null;
            return recent[0];
        }

        public static string RadioHudText(GameState state)
        {
            var latest = LatestRadio(state);
            if (latest == null)
            {
                if (Story.ActIndex(state) == 0) return "Local weather. Road conditions. Ads.";
                return "Static.";
            }

            string text = latest.Headline;
            return text.Length > 72 ? text.Substring(0, 69) + "..." : text;
        }

        static MediaQuestion Q(string id, string source, string prompt, string traveler, string survivor, string wrong)
        {
            return new MediaQuestion
            {
                Id = id,
                Source = source,
                Prompt = prompt,
                Answers = new Dictionary<string, string>
                {
                    { GuestKind.Traveler, traveler },
                    { GuestKind.Survivor, survivor },
                    { GuestKind.Wrong, wrong }
                }
            };
        }

        static readonly MediaStory[] Stage1Stories =
        {
            new MediaStory
            {
                Id = "weather-ridge",
                MinAct = "normalcy",
                Kind = GuestKind.Traveler,
                Radio = new RadioEntry
                {
                    Headline = "Clear through Thursday — light wind on the ridge",
                    Body = "The county forecast calls for dry nights and a light ridge wind. No storm warnings. Overnight lows stay in the fifties."
                },
                Paper = new PaperIssue
                {
                    Headline = "Forecast holds: dry weekend, good for the access road",
                    Body = "Travel weather looks ordinary. The Gazette notes a few inns still have weekend rooms if you are driving the western corridor."
                },
                Questions = new List<MediaQuestion>()
            },
            new MediaStory
            {
                Id = "county-patch",
                MinAct = "normalcy",
                Kind = GuestKind.Traveler,
                Radio = new RadioEntry
                {
                    Headline = "County crews patching potholes on the access road tonight",
                    Body = "Expect single-lane delays after dusk. Crews say the work should be done before Friday traffic."
                },
                Paper = new PaperIssue
                {
                    Headline = "Road work expected to finish before Friday traffic",
                    Body = "The county posted a short notice: cones on the access road, then a swept lane by morning. No detour unless the weather turns."
                },
                Questions = new List<MediaQuestion>()
            },
            new MediaStory
            {
                Id = "weekend-rates",
                MinAct = "normalcy",
                Kind = GuestKind.Traveler,
                Radio = new RadioEntry
                {
                    Headline = "Inns along the corridor advertising weekend rates",
                    Body = "A stretch of roadside places is running the usual weekend special. Stations are reading the spots between weather and farm prices."
                },
                Paper = new PaperIssue
                {
                    Headline = "Travel section: roadside stays still cheaper than the city",
                    Body = "The Gazette lists midweek rates at inns between here and Pell. Nothing fancy. Clean rooms and an early checkout if you ask."
                },
                Questions = new List<MediaQuestion>()
            },
            new MediaStory
            {
                Id = "harvest-fair",
                MinAct = "normalcy",
                Kind = GuestKind.Traveler,
                Radio = new RadioEntry
                {
                    Headline = "Pell harvest fair Saturday — extra traffic after noon",
                    Body = "Fair parking fills early. If you are only passing through, give yourself an extra half hour on the county road."
                },
                Paper = new PaperIssue
                {
                    Headline = "Fair weekend: a few walk-in rooms left at the corridor inns",
                    Body = "Local desks report the usual Saturday bump. Most travelers are in and out by Sunday morning."
                },
                Questions = new List<MediaQuestion>()
            }
        };

        static readonly MediaStory[] Stories =
        {
            new MediaStory
            {
                Id = "interstate-backup",
                MinAct = "normalcy",
                Kind = GuestKind.Traveler,
                Radio = new RadioEntry
                {
                    Headline = "Interstate backed up two hours — officials blame 'nothing'",
                    Body = "Traffic on the western corridor sat still this morning. Highway patrol says there was no accident and no weather. Drivers reported the cars ahead simply stopped, then started again."
                },
                Paper = new PaperIssue
                {
                    Headline = "Motorists describe a man walking the median, smiling",
                    Body = "Several drivers told the Gazette a man in a light coat walked the median offering 'tips on who is real' in exchange for a ride. He never took the ride. He asked how many people were in the car, then waved them on. The patrol has no such person on file."
                },
                Questions = new List<MediaQuestion>
                {
                    Q("backup-why", "radio",
                        "The radio said the interstate stopped for no reason. What did you hear?",
                        "I was in it. Nobody knew why. I just wanted a bed before I tried again.",
                        "I heard that. I was not on that road. I came the back way.",
                        "Traffic on the western corridor sat still. Highway patrol says there was no accident and no weather."),
                    Q("smiling-man", "paper",
                        "The paper mentioned a smiling man on the median. Did you see him?",
                        "That was me, I guess. I tell people how to tell the difference. I do not stay. I am happier than I should be. I know how that looks.",
                        "I read that. I did not go near him. Anyone that cheerful right now is selling something.",
                        "Yes. A man in a light coat. He offered tips. Very helpful. I can be helpful too.")
                }
            },
            new MediaStory
            {
                Id = "carrington-fire",
                MinAct = "unease",
                Kind = GuestKind.Wrong,
                Radio = new RadioEntry
                {
                    Headline = "House fire on the edge of Carrington — one survivor reported",
                    Body = "A home burned down last night on the eastern edge of Carrington. A neighbor told KCLR he saw a young girl walk out of the fire, crying. Emergency crews have not confirmed a survivor. The family name has not been released."
                },
                Paper = new PaperIssue
                {
                    Headline = "Neighbor: the girl's face never matched the crying",
                    Body = "Ellis Ward, who lives two doors down, says the child walked out of the fire crying, 'but the sadness never really sat in her face — like it was copying the last thing it had seen.' He says as she walked away she seemed to grow taller, and on a second look he thought it might have been the mother. The paper is not printing the family's name at the sheriff's request. Ward asked us to mention the blue shutter that was still hanging after the roof came down."
                },
                Questions = new List<MediaQuestion>
                {
                    Q("fire-radio", "radio",
                        "Did you hear about the fire in Carrington?",
                        "On the radio, yes. A girl walked out crying. That is all they said.",
                        "I heard. I was not in Carrington. I keep thinking about the neighbor having to watch that.",
                        "A home burned down on the eastern edge of Carrington. A neighbor saw a young girl walk out of the fire, crying."),
                    Q("face-never-matched", "paper",
                        "The paper said her face never matched the crying. What do you make of that?",
                        "That is the kind of detail I listen for. Copies get the event. They do not get the face.",
                        "If that is true, it was not a child. I would not let something like that through a door.",
                        "She was crying. Very sad. Children cry when houses burn. That is what crying is for."),
                    Q("blue-shutter", "paper",
                        "Ward mentioned a blue shutter still hanging. Were you there?",
                        "I read it. I was not there. The shutter is the kind of thing a copy would not bother inventing.",
                        "I know Ellis. If he said a blue shutter, there was a blue shutter. I came from further east than that.",
                        "Yes. I walked out. I was crying. There was fire. I do not remember a shutter. Why would I remember a shutter?")
                }
            },
            new MediaStory
            {
                Id = "odd-informant",
                MinAct = "unease",
                Kind = GuestKind.Traveler,
                Radio = new RadioEntry
                {
                    Headline = "Man offering 'how to tell them apart' for a few nights' stay",
                    Body = "Listeners have called in about a traveler on the access roads who trades identification tips for a room. Stations are not endorsing his advice. He is described as unusually cheerful."
                },
                Paper = new PaperIssue
                {
                    Headline = "The informant never stays past breakfast — and will not say why he is smiling",
                    Body = "Three innkeepers between here and Pell say the same man ate, talked, slept four hours, and left before the coffee finished. He told one of them: 'They listen to the radio too. Do not ask what everyone already heard.' He paid in mixed bills and asked to be called Reed, which is probably not his name."
                },
                Questions = new List<MediaQuestion>
                {
                    Q("tips-for-a-room", "radio",
                        "The radio mentioned a man trading tips for a room. Is that you?",
                        "Sounds like me. I can tell you a few things. I will not be here long. I know that bothers people.",
                        "I heard that. I do not trust anyone selling certainty right now.",
                        "A traveler trades identification tips for a room. He is described as unusually cheerful. I can be cheerful."),
                    Q("dont-ask-the-radio", "paper",
                        "The paper said he warned not to ask what everyone already heard. Why?",
                        "Because they are listening. You ask a radio question, they recite the radio. Ask what the paper printed and watch their face.",
                        "That is the first useful thing I have heard all week. I wish I had read it sooner.",
                        "Do not ask what everyone already heard. That is good advice. I already heard everything. You can ask me anything on the radio.")
                }
            },
            new MediaStory
            {
                Id = "williams-carrington",
                MinAct = "disruption",
                Kind = GuestKind.Survivor,
                Radio = new RadioEntry
                {
                    Headline = "Carrington family displaced after overnight attack",
                    Body = "Authorities say a family left their home in Carrington after an attack. Details are limited. Listeners are asked to offer rooms if they can spare them. The broadcast did not name the family."
                },
                Paper = new PaperIssue
                {
                    Headline = "The Williams left Carrington with the windows blown out",
                    Body = "Mara and Cal Williams, and their boy Ned, evacuated after local bandits hit the east row. Neighbors say the doors and windows were blown out by the end of it. Mara's sister in Pell has not heard from them. It is a shame the world has come to this. If they come through, Ned will ask whether you have a dog. He always asks."
                },
                Questions = new List<MediaQuestion>
                {
                    Q("displaced-family", "radio",
                        "The radio said a Carrington family was displaced. Was that you?",
                        "I heard it. I am not them. I am just passing through and I have money.",
                        "They did not say our name on the radio. We left Carrington last night. We need a door that still closes.",
                        "A family left their home in Carrington after an attack. Listeners are asked to offer rooms if they can spare them. I am that family."),
                    Q("blown-windows", "paper",
                        "The paper named the Williams — windows blown out. What happened to the house?",
                        "I read that this morning. Bandits, it said. I did not go look.",
                        "The windows went first, then the front door. Cal would not let Ned look back. We did not take anything but the coats.",
                        "The house was attacked. The family left. Windows… yes. Windows can break. That is what windows do."),
                    Q("ned-dog", "paper",
                        "If you are who the paper says, Ned asks about a dog. Does he?",
                        "That is in the paper, yes. I would not pretend to be their kid.",
                        "He asked me on the walk here. Twice. We had to leave the dog. I have not figured out how to tell him.",
                        "Ned. Yes. A boy. Boys like dogs. I can ask about a dog if you want.")
                }
            },
            new MediaStory
            {
                Id = "names-hour",
                MinAct = "collapse",
                Kind = GuestKind.Wrong,
                Radio = new RadioEntry
                {
                    Headline = "A voice reads names for an hour, then apologizes and stops",
                    Body = "KCLR ran a list tonight — first names only, no towns. The reader broke off, said 'I am sorry,' and the carrier went to tone. We do not know who compiled the list."
                },
                Paper = new PaperIssue
                {
                    Headline = "The list included people who answered the door last week",
                    Body = "A Pell typesetter who still has ink says three names on last night's broadcast match guests who checked into roadside places and were never seen in the morning. He will not print the names. He says the reader's apology was the only human part of the hour."
                },
                Questions = new List<MediaQuestion>
                {
                    Q("heard-the-names", "radio",
                        "Did you hear the radio reading names last night?",
                        "I turned it off. First names, no towns. That is not information. That is a dare.",
                        "I listened for people I knew. I did not hear them. I am not sure that is better.",
                        "KCLR ran a list. First names only, no towns. The reader said I am sorry. Then tone."),
                    Q("apology-human", "paper",
                        "The paper said the apology was the only human part. Why would they write that?",
                        "Because the rest of it sounded like it had been practiced. I have heard that voice before, I think, in a different mouth.",
                        "Because whoever read those names was not the one who was sorry. I know how that sounds.",
                        "I am sorry. That is what you say. I can say I am sorry. I am sorry.")
                }
            },
            new MediaStory
            {
                Id = "pell-clinic",
                MinAct = "collapse",
                Kind = GuestKind.Survivor,
                Radio = new RadioEntry
                {
                    Headline = "Pell clinic asking for beds — 'do not send the ones who will not blink'",
                    Body = "A volunteer at the Pell clinic came on at dawn. She asked for spare rooms south of the river. She said do not send anyone who will not blink. Then someone took the microphone away."
                },
                Paper = new PaperIssue
                {
                    Headline = "Clinic volunteer named two families still walking the river road",
                    Body = "The Gazette got a note under the door: the Haros and the Venns left Pell on foot after the clinic locked its front. Rosa Haro has a burn on her left wrist she covers with a scarf. If someone arrives claiming to be from the clinic and both wrists are clean, that is not Rosa."
                },
                Questions = new List<MediaQuestion>
                {
                    Q("dont-send-blink", "radio",
                        "The clinic said not to send the ones who will not blink. What do you think that means?",
                        "It means watch the eyes. I have been saying that. They copy expressions. Blinking is easy to forget.",
                        "It means they have already let one in. I came from that road. I blink. I know how that sounds.",
                        "Do not send anyone who will not blink. I can blink. See."),
                    Q("rosa-scarf", "paper",
                        "The paper mentioned Rosa Haro and a scarf on the left wrist. Is that you?",
                        "I read it. I am not Rosa. If someone says they are, ask to see the wrist.",
                        "I am not Rosa. We passed her yesterday. The scarf was slipping and she would not let anyone touch it.",
                        "Rosa Haro. Scarf. Left wrist. Yes. I cover things. I can cover my wrist.")
                }
            },
            new MediaStory
            {
                Id = "day-count-wall",
                MinAct = "shelter",
                Kind = GuestKind.Wrong,
                Radio = new RadioEntry
                {
                    Headline = "No stations. A recording loops: stay inside, do not approach",
                    Body = "If you can still hear this, it is a recording. Stay inside. Do not approach anyone you did not let in yourself. Repeat. Stay inside."
                },
                Paper = new PaperIssue
                {
                    Headline = "Handwritten: the day count on the lobby wall is wrong by two",
                    Body = "Someone has been moving the tally. Two days vanish, then come back. If a guest knows today's number without looking at your wall, they did not sleep here. If they insist the number on the wall is correct, they are reading it the way a copy reads a face — from the last thing they saw."
                },
                Questions = new List<MediaQuestion>
                {
                    Q("recording", "radio",
                        "The radio is just a recording now. Did you hear it on the road?",
                        "Stay inside. Do not approach. I heard it from three different poles. It does not help.",
                        "I heard it. I came anyway. I know that is the opposite of the instruction.",
                        "Stay inside. Do not approach anyone you did not let in yourself. Repeat. Stay inside."),
                    Q("wall-number", "paper",
                        "Without looking — what day is it, on our wall?",
                        "I have not looked at your wall. That is the point of the note, right?",
                        "I do not know your count. I have been walking. I would not pretend I slept here.",
                        "It is the number on the wall. The number is correct. Numbers are for counting. I can count.")
                }
            }
        };
    }
}

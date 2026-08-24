import { addLog } from "./state.js";
import { ACT_ORDER, currentAct, actIndex } from "./story.js";
import { isStageOne } from "./stage.js";

/**
 * Radio is public. Everyone in the lobby has heard it — including the things
 * that are not people. Ask a radio question and a Wrong arrival will recite
 * the broadcast almost word for word. That is the trap.
 *
 * The newspaper prints what the radio left out. Those extra facts are the
 * questions that actually distinguish a traveler, a survivor, and a copy.
 * You only get those questions after you have read the issue.
 */

export function createMediaState() {
  return {
    radioLog: [],
    papers: [],
    airedIds: [],
    printedIds: [],
    radioIn: 10,
    lastPaperDay: 0,
  };
}

/** Weather, roads, and inn ads. Stage 1 only — no highway-man story. */
const STAGE1_STORIES = [
  {
    id: "weather-ridge",
    minAct: "normalcy",
    kind: "traveler",
    radio: {
      headline: "Clear through Thursday — light wind on the ridge",
      body:
        "The county forecast calls for dry nights and a light ridge wind. " +
        "No storm warnings. Overnight lows stay in the fifties.",
    },
    paper: {
      headline: "Forecast holds: dry weekend, good for the access road",
      body:
        "Travel weather looks ordinary. The Gazette notes a few inns still " +
        "have weekend rooms if you are driving the western corridor.",
    },
    questions: [],
  },
  {
    id: "county-patch",
    minAct: "normalcy",
    kind: "traveler",
    radio: {
      headline: "County crews patching potholes on the access road tonight",
      body:
        "Expect single-lane delays after dusk. Crews say the work should be " +
        "done before Friday traffic.",
    },
    paper: {
      headline: "Road work expected to finish before Friday traffic",
      body:
        "The county posted a short notice: cones on the access road, then " +
        "a swept lane by morning. No detour unless the weather turns.",
    },
    questions: [],
  },
  {
    id: "weekend-rates",
    minAct: "normalcy",
    kind: "traveler",
    radio: {
      headline: "Inns along the corridor advertising weekend rates",
      body:
        "A stretch of roadside places is running the usual weekend special. " +
        "Stations are reading the spots between weather and farm prices.",
    },
    paper: {
      headline: "Travel section: roadside stays still cheaper than the city",
      body:
        "The Gazette lists midweek rates at inns between here and Pell. " +
        "Nothing fancy. Clean rooms and an early checkout if you ask.",
    },
    questions: [],
  },
  {
    id: "harvest-fair",
    minAct: "normalcy",
    kind: "traveler",
    radio: {
      headline: "Pell harvest fair Saturday — extra traffic after noon",
      body:
        "Fair parking fills early. If you are only passing through, give " +
        "yourself an extra half hour on the county road.",
    },
    paper: {
      headline: "Fair weekend: a few walk-in rooms left at the corridor inns",
      body:
        "Local desks report the usual Saturday bump. Most travelers are in " +
        "and out by Sunday morning.",
    },
    questions: [],
  },
];

const STORIES = [
  {
    id: "interstate-backup",
    minAct: "normalcy",
    kind: "traveler",
    radio: {
      headline: "Interstate backed up two hours — officials blame 'nothing'",
      body:
        "Traffic on the western corridor sat still this morning. Highway patrol " +
        "says there was no accident and no weather. Drivers reported the cars " +
        "ahead simply stopped, then started again.",
    },
    paper: {
      headline: "Motorists describe a man walking the median, smiling",
      body:
        "Several drivers told the Gazette a man in a light coat walked the " +
        "median offering 'tips on who is real' in exchange for a ride. He " +
        "never took the ride. He asked how many people were in the car, then " +
        "waved them on. The patrol has no such person on file.",
    },
    questions: [
      {
        id: "backup-why",
        source: "radio",
        prompt: "The radio said the interstate stopped for no reason. What did you hear?",
        answers: {
          traveler:
            "I was in it. Nobody knew why. I just wanted a bed before I tried again.",
          survivor:
            "I heard that. I was not on that road. I came the back way.",
          wrong:
            "Traffic on the western corridor sat still. Highway patrol says there was no accident and no weather.",
        },
      },
      {
        id: "smiling-man",
        source: "paper",
        prompt: "The paper mentioned a smiling man on the median. Did you see him?",
        answers: {
          traveler:
            "That was me, I guess. I tell people how to tell the difference. I do not stay. I am happier than I should be. I know how that looks.",
          survivor:
            "I read that. I did not go near him. Anyone that cheerful right now is selling something.",
          wrong:
            "Yes. A man in a light coat. He offered tips. Very helpful. I can be helpful too.",
        },
      },
    ],
  },
  {
    id: "carrington-fire",
    minAct: "unease",
    kind: "wrong",
    radio: {
      headline: "House fire on the edge of Carrington — one survivor reported",
      body:
        "A home burned down last night on the eastern edge of Carrington. A " +
        "neighbor told KCLR he saw a young girl walk out of the fire, crying. " +
        "Emergency crews have not confirmed a survivor. The family name has " +
        "not been released.",
    },
    paper: {
      headline: "Neighbor: the girl's face never matched the crying",
      body:
        "Ellis Ward, who lives two doors down, says the child walked out of " +
        "the fire crying, 'but the sadness never really sat in her face — like " +
        "it was copying the last thing it had seen.' He says as she walked " +
        "away she seemed to grow taller, and on a second look he thought it " +
        "might have been the mother. The paper is not printing the family's " +
        "name at the sheriff's request. Ward asked us to mention the blue " +
        "shutter that was still hanging after the roof came down.",
    },
    questions: [
      {
        id: "fire-radio",
        source: "radio",
        prompt: "Did you hear about the fire in Carrington?",
        answers: {
          traveler:
            "On the radio, yes. A girl walked out crying. That is all they said.",
          survivor:
            "I heard. I was not in Carrington. I keep thinking about the neighbor having to watch that.",
          wrong:
            "A home burned down on the eastern edge of Carrington. A neighbor saw a young girl walk out of the fire, crying.",
        },
      },
      {
        id: "face-never-matched",
        source: "paper",
        prompt: "The paper said her face never matched the crying. What do you make of that?",
        answers: {
          traveler:
            "That is the kind of detail I listen for. Copies get the event. They do not get the face.",
          survivor:
            "If that is true, it was not a child. I would not let something like that through a door.",
          wrong:
            "She was crying. Very sad. Children cry when houses burn. That is what crying is for.",
        },
      },
      {
        id: "blue-shutter",
        source: "paper",
        prompt: "Ward mentioned a blue shutter still hanging. Were you there?",
        answers: {
          traveler:
            "I read it. I was not there. The shutter is the kind of thing a copy would not bother inventing.",
          survivor:
            "I know Ellis. If he said a blue shutter, there was a blue shutter. I came from further east than that.",
          wrong:
            "Yes. I walked out. I was crying. There was fire. I do not remember a shutter. Why would I remember a shutter?",
        },
      },
    ],
  },
  {
    id: "odd-informant",
    minAct: "unease",
    kind: "traveler",
    radio: {
      headline: "Man offering 'how to tell them apart' for a few nights' stay",
      body:
        "Listeners have called in about a traveler on the access roads who " +
        "trades identification tips for a room. Stations are not endorsing " +
        "his advice. He is described as unusually cheerful.",
    },
    paper: {
      headline: "The informant never stays past breakfast — and will not say why he is smiling",
      body:
        "Three innkeepers between here and Pell say the same man ate, talked, " +
        "slept four hours, and left before the coffee finished. He told one " +
        "of them: 'They listen to the radio too. Do not ask what everyone " +
        "already heard.' He paid in mixed bills and asked to be called Reed, " +
        "which is probably not his name.",
    },
    questions: [
      {
        id: "tips-for-a-room",
        source: "radio",
        prompt: "The radio mentioned a man trading tips for a room. Is that you?",
        answers: {
          traveler:
            "Sounds like me. I can tell you a few things. I will not be here long. I know that bothers people.",
          survivor:
            "I heard that. I do not trust anyone selling certainty right now.",
          wrong:
            "A traveler trades identification tips for a room. He is described as unusually cheerful. I can be cheerful.",
        },
      },
      {
        id: "dont-ask-the-radio",
        source: "paper",
        prompt: "The paper said he warned not to ask what everyone already heard. Why?",
        answers: {
          traveler:
            "Because they are listening. You ask a radio question, they recite the radio. Ask what the paper printed and watch their face.",
          survivor:
            "That is the first useful thing I have heard all week. I wish I had read it sooner.",
          wrong:
            "Do not ask what everyone already heard. That is good advice. I already heard everything. You can ask me anything on the radio.",
        },
      },
    ],
  },
  {
    id: "williams-carrington",
    minAct: "disruption",
    kind: "survivor",
    radio: {
      headline: "Carrington family displaced after overnight attack",
      body:
        "Authorities say a family left their home in Carrington after an " +
        "attack. Details are limited. Listeners are asked to offer rooms if " +
        "they can spare them. The broadcast did not name the family.",
    },
    paper: {
      headline: "The Williams left Carrington with the windows blown out",
      body:
        "Mara and Cal Williams, and their boy Ned, evacuated after local " +
        "bandits hit the east row. Neighbors say the doors and windows were " +
        "blown out by the end of it. Mara's sister in Pell has not heard from " +
        "them. It is a shame the world has come to this. If they come through, " +
        "Ned will ask whether you have a dog. He always asks.",
    },
    questions: [
      {
        id: "displaced-family",
        source: "radio",
        prompt: "The radio said a Carrington family was displaced. Was that you?",
        answers: {
          traveler:
            "I heard it. I am not them. I am just passing through and I have money.",
          survivor:
            "They did not say our name on the radio. We left Carrington last night. We need a door that still closes.",
          wrong:
            "A family left their home in Carrington after an attack. Listeners are asked to offer rooms if they can spare them. I am that family.",
        },
      },
      {
        id: "blown-windows",
        source: "paper",
        prompt: "The paper named the Williams — windows blown out. What happened to the house?",
        answers: {
          traveler:
            "I read that this morning. Bandits, it said. I did not go look.",
          survivor:
            "The windows went first, then the front door. Cal would not let Ned look back. We did not take anything but the coats.",
          wrong:
            "The house was attacked. The family left. Windows… yes. Windows can break. That is what windows do.",
        },
      },
      {
        id: "ned-dog",
        source: "paper",
        prompt: "If you are who the paper says, Ned asks about a dog. Does he?",
        answers: {
          traveler:
            "That is in the paper, yes. I would not pretend to be their kid.",
          survivor:
            "He asked me on the walk here. Twice. We had to leave the dog. I have not figured out how to tell him.",
          wrong:
            "Ned. Yes. A boy. Boys like dogs. I can ask about a dog if you want.",
        },
      },
    ],
  },
  {
    id: "names-hour",
    minAct: "collapse",
    kind: "wrong",
    radio: {
      headline: "A voice reads names for an hour, then apologizes and stops",
      body:
        "KCLR ran a list tonight — first names only, no towns. The reader " +
        "broke off, said 'I am sorry,' and the carrier went to tone. We do " +
        "not know who compiled the list.",
    },
    paper: {
      headline: "The list included people who answered the door last week",
      body:
        "A Pell typesetter who still has ink says three names on last night's " +
        "broadcast match guests who checked into roadside places and were " +
        "never seen in the morning. He will not print the names. He says the " +
        "reader's apology was the only human part of the hour.",
    },
    questions: [
      {
        id: "heard-the-names",
        source: "radio",
        prompt: "Did you hear the radio reading names last night?",
        answers: {
          traveler:
            "I turned it off. First names, no towns. That is not information. That is a dare.",
          survivor:
            "I listened for people I knew. I did not hear them. I am not sure that is better.",
          wrong:
            "KCLR ran a list. First names only, no towns. The reader said I am sorry. Then tone.",
        },
      },
      {
        id: "apology-human",
        source: "paper",
        prompt: "The paper said the apology was the only human part. Why would they write that?",
        answers: {
          traveler:
            "Because the rest of it sounded like it had been practiced. I have heard that voice before, I think, in a different mouth.",
          survivor:
            "Because whoever read those names was not the one who was sorry. I know how that sounds.",
          wrong:
            "I am sorry. That is what you say. I can say I am sorry. I am sorry.",
        },
      },
    ],
  },
  {
    id: "pell-clinic",
    minAct: "collapse",
    kind: "survivor",
    radio: {
      headline: "Pell clinic asking for beds — 'do not send the ones who will not blink'",
      body:
        "A volunteer at the Pell clinic came on at dawn. She asked for spare " +
        "rooms south of the river. She said do not send anyone who will not " +
        "blink. Then someone took the microphone away.",
    },
    paper: {
      headline: "Clinic volunteer named two families still walking the river road",
      body:
        "The Gazette got a note under the door: the Haros and the Venns left " +
        "Pell on foot after the clinic locked its front. Rosa Haro has a burn " +
        "on her left wrist she covers with a scarf. If someone arrives claiming " +
        "to be from the clinic and both wrists are clean, that is not Rosa.",
    },
    questions: [
      {
        id: "dont-send-blink",
        source: "radio",
        prompt: "The clinic said not to send the ones who will not blink. What do you think that means?",
        answers: {
          traveler:
            "It means watch the eyes. I have been saying that. They copy expressions. Blinking is easy to forget.",
          survivor:
            "It means they have already let one in. I came from that road. I blink. I know how that sounds.",
          wrong:
            "Do not send anyone who will not blink. I can blink. See.",
        },
      },
      {
        id: "rosa-scarf",
        source: "paper",
        prompt: "The paper mentioned Rosa Haro and a scarf on the left wrist. Is that you?",
        answers: {
          traveler:
            "I read it. I am not Rosa. If someone says they are, ask to see the wrist.",
          survivor:
            "I am not Rosa. We passed her yesterday. The scarf was slipping and she would not let anyone touch it.",
          wrong:
            "Rosa Haro. Scarf. Left wrist. Yes. I cover things. I can cover my wrist.",
        },
      },
    ],
  },
  {
    id: "day-count-wall",
    minAct: "shelter",
    kind: "wrong",
    radio: {
      headline: "No stations. A recording loops: stay inside, do not approach",
      body:
        "If you can still hear this, it is a recording. Stay inside. Do not " +
        "approach anyone you did not let in yourself. Repeat. Stay inside.",
    },
    paper: {
      headline: "Handwritten: the day count on the lobby wall is wrong by two",
      body:
        "Someone has been moving the tally. Two days vanish, then come back. " +
        "If a guest knows today's number without looking at your wall, they " +
        "did not sleep here. If they insist the number on the wall is correct, " +
        "they are reading it the way a copy reads a face — from the last thing " +
        "they saw.",
    },
    questions: [
      {
        id: "recording",
        source: "radio",
        prompt: "The radio is just a recording now. Did you hear it on the road?",
        answers: {
          traveler:
            "Stay inside. Do not approach. I heard it from three different poles. It does not help.",
          survivor:
            "I heard it. I came anyway. I know that is the opposite of the instruction.",
          wrong:
            "Stay inside. Do not approach anyone you did not let in yourself. Repeat. Stay inside.",
        },
      },
      {
        id: "wall-number",
        source: "paper",
        prompt: "Without looking — what day is it, on our wall?",
        answers: {
          traveler:
            "I have not looked at your wall. That is the point of the note, right?",
          survivor:
            "I do not know your count. I have been walking. I would not pretend I slept here.",
          wrong:
            "It is the number on the wall. The number is correct. Numbers are for counting. I can count.",
        },
      },
    ],
  },
];

function actRank(name) {
  const i = ACT_ORDER.indexOf(name);
  return i < 0 ? 0 : i;
}

function availableStories(state) {
  if (isStageOne(state)) return STAGE1_STORIES;
  const rank = actIndex(state);
  return STORIES.filter((story) => actRank(story.minAct) <= rank);
}

function unpublished(state, field) {
  const used = new Set(state.story.media[field]);
  return availableStories(state).filter((story) => !used.has(story.id));
}

function intervalHours(state) {
  const byAct = {
    normalcy: 26,
    unease: 16,
    disruption: 12,
    collapse: 10,
    shelter: 9,
  };
  const base = byAct[currentAct(state)] ?? 16;
  return base * (0.7 + Math.random() * 0.6);
}

function airRadio(state, story) {
  const media = state.story.media;
  media.airedIds.push(story.id);
  media.radioLog.unshift({
    id: story.id,
    day: state.day,
    headline: story.radio.headline,
    body: story.radio.body,
    kind: story.kind,
  });
  if (media.radioLog.length > 16) media.radioLog.length = 16;
  addLog(state, `Radio: ${story.radio.headline}`);
  state.story.dispatches.unshift({
    day: state.day,
    text: story.radio.headline,
  });
  if (state.story.dispatches.length > 24) state.story.dispatches.length = 24;
}

function printPaper(state, story) {
  const media = state.story.media;
  media.printedIds.push(story.id);
  media.papers.unshift({
    id: story.id,
    day: state.day,
    headline: story.paper.headline,
    body: story.paper.body,
    kind: story.kind,
    read: false,
  });
  if (media.papers.length > 12) media.papers.length = 12;
  media.lastPaperDay = state.day;
  addLog(state, `Today's paper is on the desk. ${story.paper.headline}`);
}

export function updateMedia(state, hoursPassed) {
  if (!state.story?.media) return;
  const media = state.story.media;

  media.radioIn -= hoursPassed;
  if (media.radioIn <= 0) {
    media.radioIn = intervalHours(state);
    const next = unpublished(state, "airedIds")[0];
    if (next) airRadio(state, next);
  }

  if (state.day > media.lastPaperDay) {
    const next = unpublished(state, "printedIds")[0];
    if (next) printPaper(state, next);
  }
}

export function latestRadio(state) {
  return state.story?.media?.radioLog?.[0] ?? null;
}

export function latestPaper(state) {
  return state.story?.media?.papers?.[0] ?? null;
}

export function markPaperRead(state, paperId = null) {
  const papers = state.story?.media?.papers ?? [];
  const paper = paperId
    ? papers.find((p) => p.id === paperId)
    : papers[0];
  if (!paper) return null;
  paper.read = true;
  return paper;
}

export function hasReadPaper(state, storyId) {
  return Boolean(
    state.story?.media?.papers?.find((p) => p.id === storyId && p.read)
  );
}

export function hasHeardRadio(state, storyId) {
  return Boolean(state.story?.media?.airedIds?.includes(storyId));
}

export function getStoryById(id) {
  return STORIES.find((s) => s.id === id) ?? null;
}

/** Stories the player can actually ask about right now. */
export function knownStories(state) {
  return availableStories(state).filter(
    (story) =>
      hasHeardRadio(state, story.id) || hasReadPaper(state, story.id)
  );
}

/**
 * Questions the player has earned the right to ask.
 * Radio questions unlock when the story has aired.
 * Paper questions unlock only after the issue has been read.
 */
export function availableQuestions(state, guest) {
  if (isStageOne(state)) return [];
  const list = [];
  for (const story of knownStories(state)) {
    for (const q of story.questions) {
      const heard = hasHeardRadio(state, story.id);
      const read = hasReadPaper(state, story.id);
      if (q.source === "radio" && !heard) continue;
      if (q.source === "paper" && !read) continue;
      list.push({
        ...q,
        storyId: story.id,
        storyKind: story.kind,
        tied: guest?.storyId === story.id,
      });
    }
  }
  return list;
}

export function answerFor(guest, question) {
  const kind = guest?.kind ?? "traveler";
  return question.answers[kind] ?? question.answers.traveler;
}

/**
 * When an arrival's kind matches a recently aired story, sometimes they *are*
 * (or claim to be) the person in that story.
 */
export function pickTiedStory(state, kind) {
  const recent = (state.story?.media?.radioLog ?? [])
    .slice(0, 3)
    .map((entry) => getStoryById(entry.id))
    .filter((story) => story && story.kind === kind);
  if (!recent.length) return null;
  if (Math.random() > 0.55) return null;
  return recent[0];
}

export function radioHudText(state) {
  const latest = latestRadio(state);
  if (!latest) {
    if (actIndex(state) === 0) return "Local weather. Road conditions. Ads.";
    return "Static.";
  }
  const text = latest.headline;
  return text.length > 72 ? `${text.slice(0, 69)}...` : text;
}

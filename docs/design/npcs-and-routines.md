# Hidden Harbours — NPCs & Routines

> Module doc. Subordinate to `../vision-and-pillars.md` (CANON). The named core cast, their
> routine system, and the procedural "extras" all serve **P3 (A Living Working Coast)** above
> all, with strong ties to **P1 (The Sea Has Moods)** because routines react to the same tide,
> weather, and clock that drive the world. If this doc contradicts canon, canon wins.
>
> Sibling docs referenced: `world-and-regions.md` (where NPCs live),
> `time-tides-weather.md` (the clock/tide/weather these routines react to),
> `economy-and-business.md` (**hireable STAFF live there, not here**),
> `progression-and-housing.md`, `fish-and-content.md`, `boats-and-navigation.md`,
> and the ADRs `../adr/0002-procedural-vs-handcrafted.md`, `../adr/0003-data-driven-content.md`.

---

## 1. Purpose & the one hard boundary

This doc owns the **people of the coast**: the handcrafted named cast who give the world its
heart and its quests, the routine system that makes them *live* on a 24-hour clock, the
procedural background townsfolk who make the place feel populated, and the relationship/reputation
system that ties the player to them.

**The one hard boundary — NPCs vs STAFF.** There are two completely different concepts that both
look like "a person who works for you," and they must not bleed together:

| | **NPCs** (this doc) | **STAFF** (`economy-and-business.md`) |
|---|---|---|
| What they are | The inhabitants of the world: townsfolk, services, friends, rivals, family. | Employees you *hire and manage* to automate your operation (deckhands at scale, processing-plant workers, fleet captains). |
| Why they exist | P3 — the world feels alive and reactive. | P4 — "earn it, then automate it"; you delegate work you've mastered. |
| Owned by | **This doc** (routines, relationships, dialogue, the core cast). | **`economy-and-business.md`** (hiring, wages, productivity, management UI). |
| Overlap | A *named* NPC can become your *first* hireable deckhand as a story/relationship hook — but the moment hiring/management mechanics are involved, those mechanics are defined in the economy doc. | Generic crew you hire in bulk are STAFF, generated similarly to "extras" but owned by the economy doc. |

This doc may *reference* staff and *introduce* the first hireable character as a relationship
hook, but it does **not** define hiring, wages, or productivity. When in doubt, mechanics of
employment → economy doc; personality, routine, and friendship → here.

---

## 2. The routine system (P3 + P1)

### 2.1 Design goals

- **Everyone keeps a 24-hour schedule** tied to the same in-game clock that drives time, tide,
  and weather (`time-tides-weather.md`). The town visibly runs on its own rhythm whether or not
  the player is watching — shops open and close, the auction fills and empties, the tavern lights
  up at night, fishers leave at dawn and land their catch in the afternoon (P3).
- **Routines react to the world, not just the clock.** The same NPC behaves differently in a gale
  than in calm, at a spring low tide than a neap high, in winter than in summer, on a market day
  than a Sunday (P1). This reactivity is what separates a *living* coast from a diorama of people
  walking fixed loops.
- **Legible, not simulationist.** This is Stardew-grade readability, not a full agent AI. Players
  should be able to *learn* an NPC's habits ("the auctioneer is always at the market mid-morning;
  the shipwright drinks at the tavern after dark") and use that knowledge. Predictability is a
  feature.
- **Cheap on mobile.** Hundreds of potential characters across regions, but only a handful
  simulated in detail at any moment. See §7 (Implementation).

### 2.2 Core concepts

- **Anchors.** Every NPC has a small set of named **location anchors** — `home`, `work`, and a few
  social/utility spots (`tavern`, `market`, `wharf`, `church`, `keeper_lamp`, etc.). Anchors are
  authored points in region scenes (`world-and-regions.md`).
- **Schedule blocks.** A day is a list of **time blocks**, each saying *go to anchor X, do
  activity Y, between times T1–T2*. Activities are lightweight tags (`work`, `sell`, `eat`,
  `drink`, `sleep`, `walk`, `pray`, `mend_nets`, `tend_lamp`) that drive animation, interactability,
  and dialogue context — not a needs simulation.
- **Schedule selection by world-state.** An NPC doesn't have *one* schedule; it has a **prioritised
  list of conditional schedules**. Each tick of the day-planner, the game picks the
  **highest-priority schedule whose condition matches the current world-state** (weather, tide
  phase, season, day-of-week, story flags, relationship). This is the reactivity engine.
- **Day-of-week & calendar.** The canon has four seasons and a ~28-day moon (tide cycle). We layer
  a simple weekday cycle (for market days, a day of rest, tavern nights) on top — owned in spirit by
  `time-tides-weather.md`'s calendar; consumed here. **Market day** and **rest day** are the two
  weekdays that most reshape routines.
- **Needs/activities (light).** We model *activities*, not *needs*. An NPC "eats" because the
  schedule says lunch at noon, not because a hunger meter emptied. This is deliberate: it keeps
  behaviour authored, debuggable, and readable, and it keeps the CPU budget tiny.

### 2.3 Reactivity rules (how the world bends routines)

These are the conditions schedules switch on (P1 made social):

- **Weather.** In a **gale / storm / heavy sea**, fishers *don't put to sea* — they stay ashore,
  mend gear at the wharf, or crowd the tavern; the harbourmaster posts warnings; the lighthouse
  keeper is most active. In **fog / The Smother conditions**, similar. Calm fair weather = normal
  working day. (Weather states from `time-tides-weather.md`.)
- **Tide.** Tide-dependent work follows the water: a **clam-digger works the Drownded Lands only at
  low water** and retreats with the flood; fishers time departures/landings to the tide and the
  Fundy Rips slack windows; the harbourmaster's traffic advice shifts with the tide. Spring vs neap
  changes *who goes where* (spring lows draw diggers far out).
- **Season.** Different fisheries are in season (`fish-and-content.md`), so working NPCs target
  different grounds; winter shortens daylight and drives more tavern time; some routines (festivals,
  certain services) are seasonal.
- **Day-of-week.** **Market day** packs Port Greywick — the auctioneer runs a full auction, the
  town is busy, more "extras" spawn. **Rest day** quiets the working waterfront and fills the
  church/tavern. Tavern nights draw the regulars.
- **Story / relationship.** Story flags can override routines (an NPC is "away" during a quest, or
  appears at a new anchor once unlocked). High relationship can add personal blocks (an NPC invites
  you fishing, keeps a stall slot for you).

A schedule's **condition** is any combination of the above; the planner resolves them by priority.

### 2.4 Pathfinding & movement

- NPCs **path between anchors** along the region's walkable navmesh / tile grid using A* (land) or,
  for working boats, simple waypoint routes on the water. Movement is point-to-point between
  anchors, not free wandering — cheaper and more readable.
- **Off-screen NPCs teleport-resolve.** When the player isn't in the region (or the NPC is far
  off-screen), we don't actually path them — we **snap them to wherever their current schedule block
  says they should be** when they next become visible. The simulation is *positional truth on
  demand*, not continuous. This is the key mobile optimisation (§7).
- **Doors, berths, and tide.** Pathing respects opening hours (a shop NPC can't path inside a closed
  building they don't own) and the tide layer (a foot route across the Drownded Lands flats is only
  passable at low water — the same rule that governs the player, `world-and-regions.md` §7).

### 2.5 Data-shape sketch (schedule as data)

Schedules are **authored data** (ScriptableObjects), not code, per
`../adr/0003-data-driven-content.md`. A sketch of the shape (illustrative JSON-ish; real form is a
ScriptableObject):

```jsonc
// NpcDefinition (ScriptableObject)
{
  "id": "auctioneer_marguerite",
  "displayNameKey": "npc.marguerite.name",        // localization key, not a literal string
  "homeRegion": "port_greywick",
  "anchors": {
    "home":   { "region": "port_greywick", "point": "marguerite_house" },
    "work":   { "region": "port_greywick", "point": "auction_house_podium" },
    "tavern": { "region": "port_greywick", "point": "tavern_corner_table" },
    "church": { "region": "port_greywick", "point": "church_pew_3" }
  },
  "schedules": [
    // Highest priority first; planner picks the first whose `when` matches world-state.
    {
      "id": "storm_day",
      "priority": 100,
      "when": { "weather": ["gale", "storm"] },         // reactivity: no auction in a blow
      "blocks": [
        { "from": "06:00", "to": "09:00", "anchor": "home",   "activity": "idle" },
        { "from": "09:00", "to": "16:00", "anchor": "tavern", "activity": "drink" },
        { "from": "16:00", "to": "22:00", "anchor": "home",   "activity": "idle" },
        { "from": "22:00", "to": "06:00", "anchor": "home",   "activity": "sleep" }
      ]
    },
    {
      "id": "market_day",
      "priority": 80,
      "when": { "dayOfWeek": ["market"], "weather": ["calm", "fair", "breezy"] },
      "blocks": [
        { "from": "05:30", "to": "08:00", "anchor": "work",   "activity": "open_market" },
        { "from": "08:00", "to": "13:00", "anchor": "work",   "activity": "run_auction" }, // service window
        { "from": "13:00", "to": "14:00", "anchor": "tavern", "activity": "eat" },
        { "from": "14:00", "to": "17:00", "anchor": "work",   "activity": "settle_books" },
        { "from": "17:00", "to": "21:00", "anchor": "tavern", "activity": "drink" },
        { "from": "21:00", "to": "05:30", "anchor": "home",   "activity": "sleep" }
      ]
    },
    {
      "id": "rest_day",
      "priority": 70,
      "when": { "dayOfWeek": ["rest"] },
      "blocks": [
        { "from": "07:00", "to": "10:00", "anchor": "home",   "activity": "idle" },
        { "from": "10:00", "to": "12:00", "anchor": "church", "activity": "pray" },
        { "from": "12:00", "to": "22:00", "anchor": "tavern", "activity": "drink" },
        { "from": "22:00", "to": "07:00", "anchor": "home",   "activity": "sleep" }
      ]
    },
    {
      "id": "default",
      "priority": 0,                                    // fallback: always matches
      "when": {},
      "blocks": [
        { "from": "06:00", "to": "08:00", "anchor": "work",   "activity": "open_market" },
        { "from": "08:00", "to": "12:00", "anchor": "work",   "activity": "run_auction" },
        { "from": "12:00", "to": "13:00", "anchor": "tavern", "activity": "eat" },
        { "from": "13:00", "to": "17:00", "anchor": "work",   "activity": "settle_books" },
        { "from": "17:00", "to": "20:00", "anchor": "tavern", "activity": "drink" },
        { "from": "20:00", "to": "06:00", "anchor": "home",   "activity": "sleep" }
      ]
    }
  ]
}
```

And the per-frame planner, in pseudocode:

```
function UpdateNpc(npc, world):
    schedule = SelectSchedule(npc, world)          // highest-priority matching `when`
    block    = schedule.BlockAt(world.timeOfDay)   // which time-block is current
    target   = npc.anchors[block.anchor]

    if npc.region == world.activeRegion and npc.isOnScreenOrNearby:
        # Fully simulated: path them there and play the activity.
        if not npc.AtOrPathingTo(target):
            npc.PathTo(target, respecting = [openingHours, tideLayer, navmesh])
        npc.PlayActivity(block.activity)            // anim, interactable state, dialogue context
    else:
        # Off-screen: don't path. Just record where they "are" for when we need them.
        npc.virtualPosition = target
        npc.currentActivity = block.activity

function SelectSchedule(npc, world):
    for s in npc.schedules ordered by priority desc:
        if Matches(s.when, world):                  // weather, tide, season, dayOfWeek, flags, rel
            return s
    return npc.defaultSchedule
```

The **service window** (e.g., `run_auction`, or a shop's `open` activity) is what the rest of the
game queries to know "is this service available right now?" — so opening hours emerge naturally from
the schedule rather than being a separate system. A player who shows up at the shipwright's at
midnight finds it dark because the shipwright is asleep at `home`, by the same data that drives the
animation.

---

## 3. The handcrafted core cast

Fourteen named characters, sized and placed to match `world-and-regions.md` (the bulk live and work
in **Port Greywick**; the **uncle** anchors the opening in **Coddle Cove**; the **lighthouse keeper**
holds the lonely end of the world at **Ironbound**). They are warm, weathered, North-Atlantic
working people — not quirky cartoons. Each entry gives **role · personality · daily-routine sketch ·
relationship to the player · questline/service hook.**

> Note on the uncle: the canon logline is that you *inherit* the uncle's dory and cottage. The cast
> below treats the uncle as the **departed figure who anchors the opening** — present at the very
> start (the tutorial / handover), then lost, with his memory and unfinished business threaded
> through the early game. This matches "Inherit your uncle's dory and his cottage" while still
> letting him anchor the opening as required.

### 3.1 Uncle Ned Coddle — *the one who left you everything*

- **Role:** Your late uncle; the man whose dory and cottage you inherit. The emotional origin of
  the game. Present in the opening hours (the handover / tutorial), then gone.
- **Personality:** Gruff, dry-humoured, endlessly capable; loved this coast more than he ever said.
  Taught by doing, not telling. The kind of man the whole harbour quietly respected.
- **Daily routine (opening only):** Up before dawn, out in the dory on the cove, in by afternoon to
  mend gear on the wharf, an early evening pipe on the cottage step, asleep with the gulls. His
  rhythm *becomes the rhythm the game teaches you*.
- **Relationship to player:** Family and mentor. The reason you're here. His absence is felt at the
  cottage, in his half-finished logbook, in neighbours' stories.
- **Hook:** The framing questline — **"Ned's Unfinished Lines."** His annotated charts, debts,
  half-mended traps, and a promise or two he didn't keep become a thread of small quests that
  gently introduce every system and every named NPC ("your uncle would've wanted…", "Ned owed me a
  favour…"). His logbook doubles as the player's early tutorial journal.

### 3.2 Marguerite Faye — *the auctioneer*

- **Role:** Runs the **fish auction** at Port Greywick — the human face of the market
  (`economy-and-business.md`).
- **Personality:** Quick, sharp-tongued, fair to a fault, misses nothing. Has called every catch on
  this wharf for thirty years and can read a hold at a glance.
- **Daily routine:** Opens the market before dawn, runs the auction through the morning (full
  auction on **market day**), settles the books midday, a drink at the tavern at dusk, church on
  rest day. No auction in a gale.
- **Relationship to player:** Your gatekeeper to good prices. Starts brisk and transactional; warms
  into a shrewd ally who tips you off to demand swings as your standing grows.
- **Hook (service):** Selling at auction; later, **better fees, market intel, and priority slots**
  as relationship rises. Questline: **"Reading the Room"** — she teaches you to time the market
  (ties to supply/demand in `economy-and-business.md`).

### 3.3 Captain Reuben Stout — *the harbourmaster*

- **Role:** **Harbourmaster** of Port Greywick — berths, port rules, forecasts, safety.
- **Personality:** Steady, weather-beaten, paternal; a former offshore skipper who's seen men not
  come back and takes the sea's danger seriously (P5). Slow to spook, impossible to bluff.
- **Daily routine:** Office at dawn, walks the wharf assigning berths and checking lines, posts the
  **forecast / storm warnings** (`time-tides-weather.md`), watches the harbour mouth in bad weather,
  tavern for one quiet pint at night. Most active and most stern when a blow is coming.
- **Relationship to player:** Your safety net and reality check. Issues warnings you ignore at your
  peril. Becomes a gruff protector who'll send a tow if you ground (ties to rescue,
  `boats-and-navigation.md`).
- **Hook (service):** **Forecasts, tide/berth info, and tow/rescue** when you're stranded. Questline:
  **"The Watch"** — earning his trust unlocks better forecasting (barometer/radio access) and,
  eventually, his old offshore charts.

### 3.4 Aunt Ginny Coddle — *the keeper of the home*

- **Role:** Ned's sister (your aunt) in **Coddle Cove**; the warm hearth of the home harbour and the
  player's softest landing.
- **Personality:** Kind, practical, fierce when she needs to be; feeds you whether you want it or
  not; the keeper of family memory and the one person who'll tell you the truth gently.
- **Daily routine:** Garden and kitchen at dawn, tends the cove cottages and neighbours through the
  day, on the step at dusk, early to bed. Rarely leaves the cove — she *is* home.
- **Relationship to player:** Family. Your emotional home base; cooks, mends, encourages, grieves Ned
  with you. The person you return to.
- **Hook (service):** Home comforts — **cooking/restoring, storage at the cottage, and the early
  tutorial of the home base** (`progression-and-housing.md`). Questline: **"Ned's Memory"** — she
  parcels out his story and his keepsakes as you prove yourself, the warm counterpart to his
  practical logbook.

### 3.5 Silas Boyne — *the shipwright*

- **Role:** Runs the **shipwright's yard** in Port Greywick — boat purchase, repair, and upgrades
  (`boats-and-navigation.md`); the gatekeeper of the boat ladder (P2).
- **Personality:** Taciturn, perfectionist, talks more easily to hulls than to people; deeply proud,
  a little lonely. Will not sell you a boat you can't handle yet, and says so plainly.
- **Daily routine:** In the yard from early, planking and caulking all day (hammering you can hear
  across the harbour), a slow pint at the tavern after dark where he finally loosens up.
- **Relationship to player:** Your enabler of *tangible growth* — every rung up the boat ladder
  passes through Silas. Starts gruffly skeptical; becomes a craftsman who takes pride in outfitting
  you. The first big purchase from him is a P2 milestone.
- **Hook (service):** **Buying/upgrading/repairing boats** up the canon ladder (Dory → Punt → Cape
  Islander → … → Tanker). Questline: **"A Boat Worth Her Salt"** — restoring a derelict hull with him
  (a possible discounted/unique boat) teaches the upgrade system and earns a friend.

### 3.6 Odette Tranchemontagne — *the gear-shop owner / chandler*

- **Role:** Runs the **chandlery / gear shop** in Port Greywick — rods, lines, traps, nets, bait,
  charts, and small equipment.
- **Personality:** Bright, chatty, mercantile in the friendliest way; knows what everyone bought and
  why; a magpie for new gear and gossip. The town's unofficial information exchange.
- **Daily routine:** Opens the shop mid-morning, works the counter through the day, restocks at dusk,
  tavern with the regulars. Busiest on market day; opens late on rest day.
- **Relationship to player:** Your outfitter and the warm, low-stakes face of progression — new gear
  unlocks new fishing. Easy to befriend; a reliable source of rumours and leads.
- **Hook (service):** **Gear/bait/trap/chart purchases and upgrades** (ties to `fish-and-content.md`
  methods and the chart layer in `world-and-regions.md` §4.1). Questline: **"Word on the Wharf"** —
  her gossip seeds many other NPCs' quests; she's the hub that points you outward.

### 3.7 Wallace "Wally" Pike — *the rival skipper*

- **Role:** A successful, ambitious **rival fisher** working the same grounds — the competitive
  needle of the living coast (P3, P5).
- **Personality:** Cocky, charismatic, not actually a villain — a sharp operator who got here first
  and lets you know it. Big laugh, sharp elbows, grudging respect underneath.
- **Daily routine:** Out early to the best marks (often beating you to them), lands a fat catch in
  the afternoon, holds court at the tavern at night. In a gale, he's the loud one daring others to
  go out — and the one who sometimes shouldn't.
- **Relationship to player:** Friendly rivalry that the player can steer toward respect or
  resentment. He benchmarks your progress (he's always a rung ahead early, then you catch him). A
  redemption/respect arc, not an enemy to defeat.
- **Hook (questline):** **"The Same Water"** — a running series of informal contests (biggest catch,
  fastest Rips transit, first to the new grounds) that pace the player against a peer, with a
  late beat where the rivalry turns to partnership or hard-won respect. Can foreshadow regions
  (he's been to the Banks before you).

### 3.8 Bram Tiller — *the tavern keeper*

- **Role:** Keeps the **tavern** in Port Greywick — the town's social hub and the night-time heart of
  the living coast (P3).
- **Personality:** Warm, unflappable, a good listener with a long memory; everyone's confessor.
  Keeps the peace, knows everyone's troubles, never repeats them to the wrong person.
- **Daily routine:** Preps in the afternoon, runs the tavern from dusk till late (the one place that
  *fills* at night and in storms), sleeps in, light mornings. Busiest in foul weather, when the
  fleet stays ashore.
- **Relationship to player:** Your social anchor in town — where you meet NPCs, hear rumours, and
  decompress. Befriending Bram opens the social map of the whole cast.
- **Hook (service):** The tavern as **social/quest hub** — meeting point, rumour board, hot meals,
  morale. Questline: **"Last Call"** — helping Bram keep the tavern afloat (a community-stakes story)
  and using it as the board where many other NPCs' quests surface.

### 3.9 Mother Edie Vance — *the lighthouse keeper*

- **Role:** **Lighthouse keeper** at **Ironbound** — the lone human presence at the dangerous end of
  the world (`world-and-regions.md` §6.7).
- **Personality:** Weathered hermit, sparing with words, encyclopedic about the outer waters; half
  myth to the townsfolk. Not unfriendly — just long alone, and worth the effort to know.
- **Daily routine:** Tends the **lamp** at dusk and through the night (her most important hours),
  sleeps by day, keeps the light against the storms, watches the weather no one else dares. Her
  routine inverts the town's — awake when the sea is most dangerous.
- **Relationship to player:** A late-game mentor for the deadliest waters. Reaching her at all is an
  achievement (you need a weather-capable boat). Earns you survival knowledge of Ironbound and the
  Smother.
- **Hook (service/quest):** **"The Lamp and the Lost"** — gaining her trust yields her
  **hand-annotated charts of Ironbound and a route into The Smother** (the relationship-gated charts
  named in `world-and-regions.md` §4.1), plus tips on legendary fish and surviving the storm coast.

### 3.10 Harlan Boudreau — *the processing-plant owner*

- **Role:** Owns the **marine-supply / fish-processing plant** in Port Greywick — the value-add /
  refining gateway (`economy-and-business.md`); the canon's "marine-supply/processing-plant owner."
- **Personality:** Shrewd, smooth, ambitious; a legitimate businessman who plays hardball and respects
  anyone who plays it well. Not crooked — just relentlessly commercial.
- **Daily routine:** At the plant early overseeing the line, lunches with buyers, works contracts in
  the afternoon, dines well. Busiest after the fleet lands; market day is his big day.
- **Relationship to player:** Your bridge from *catching* to *business* (P4) — buys raw catch in
  bulk, processes for value-add, and later contracts with your operation. Starts as a buyer; becomes
  a business peer (or a tough negotiator) as you scale.
- **Hook (service/quest):** **Processing/refining catch and bulk contracts** (the value-add economy,
  `economy-and-business.md`). Questline: **"Value Added"** — learning the refining chain and securing
  supply contracts; the on-ramp toward the freight/commerce endgame and the Shipping Lanes.

### 3.11 Pearl Tobin — *the first deckhand (hireable)*

- **Role:** A young, eager local from near Coddle Cove — the **first crew member the player can take
  on** as a relationship-and-story hook. *(Hiring/management mechanics: `economy-and-business.md`;
  her personality, routine, and bond: here.)*
- **Personality:** Keen, capable, a touch reckless; desperate to prove herself and get off the wharf
  and onto the water. Knew and idolised Ned. Loyal once you earn it.
- **Daily routine (before hire):** Hangs around the Coddle Cove / Greywick wharves looking for a
  site, helps mend gear for coppers, watches the boats come in, tavern's quiet corner at night. After
  hire, her routine merges into your operation.
- **Relationship to player:** Your **first step from laborer toward owner** (P4) — the first person
  who works *with* you, then *for* you. A warm mentor-becomes-mentee arc that personalises the move
  into management.
- **Hook (quest/service):** **"A Site on Your Boat"** — a small questline to earn her trust and take
  her on as crew (introducing the *concept* of crew before the full STAFF system in
  `economy-and-business.md`). She can grow into a skipper of one of your boats in the fleet endgame.

### 3.12 Father Tomas Le Bris — *the parson / community keeper*

- **Role:** Keeps the **church/meeting house** in Port Greywick and tends the town's spiritual and
  communal life — weddings, funerals, festivals, the rest-day gathering.
- **Personality:** Gentle, wise, quietly funny; carries the town's griefs (he buried Ned). A
  steadying presence who knows everyone's history and judges no one.
- **Daily routine:** Morning prayers, visits the sick and lonely through the day, leads the rest-day
  service, presides over **seasonal festivals**, evening reflection. His calendar marks the town's
  ceremonial rhythm.
- **Relationship to player:** The keeper of community and continuity; officiates the milestones and
  connects you to the town's heart. A source of comfort and of the town's collective memory of Ned.
- **Hook (quest/service):** **Festivals and community events** (seasonal town-life beats that make
  the coast feel inhabited, P3) and a grief/closure thread around Ned. Possible long-term:
  relationships/marriage and town-celebration arcs run through him.

### 3.13 Iris Halloran — *the buoy-tender / coast-tech* (instruments & the Smother)

- **Role:** Tends the **buoys, range markers, and navigation instruments** of the Banks, and is the
  town's expert on **sounders, radar, and fog navigation** — the technical key to the late-game
  instrument regions (`world-and-regions.md` §6.8, `boats-and-navigation.md`).
- **Personality:** Precise, curious, a tinkerer who trusts instruments over superstition; fascinated
  by **The Smother** where others are frightened of it. Dry wit, deep competence.
- **Daily routine:** Out servicing buoys and markers on fair days (a working-boat waypoint route),
  in her workshop calibrating gear on foul ones, late nights chasing fog data. Her routine tracks the
  weather closely (she's busiest before and after storms).
- **Relationship to player:** Your gateway to **instrument navigation** and thus to the Smother (the
  late-game instruments gate, canon). Befriending her demystifies the fog and unlocks the tech to
  brave it.
- **Hook (service/quest):** **Sells/installs sounder & radar-class instruments** and the
  **chart/route into The Smother** (complementing the keeper's hand-annotated route). Questline:
  **"Voices in the Fog"** — calibrating an instrument run into the Smother; the uncanny capstone's
  human on-ramp.

### 3.14 Old Joachim Furey — *the retired master & lorekeeper*

- **Role:** A retired offshore **master fisherman** who's worked every ground from the Sunkers to
  Ironbound — the living memory and informal mentor of the fishery; keeper of the coast's lore and
  legends (ties to legendary fish, `fish-and-content.md`).
- **Personality:** Garrulous, mischievous, prone to tall tales that turn out to be *mostly* true;
  proud, generous with hard-won knowledge to anyone who'll sit and listen. Knew Ned since they were
  boys.
- **Daily routine:** Morning walk along the wharf watching the fleet leave, whittling/mending on a
  favourite bench, holding court at the tavern by afternoon, early home. A fixture of the harbour's
  daily rhythm (P3).
- **Relationship to player:** The warm mentor-at-large — fills in the world's history, points you to
  legendary catches and hidden spots, and shares the unwritten lore the shops can't sell. A bridge
  to Ned's generation.
- **Hook (quest/service):** **Lore, tips, and legendary-fish leads** — his stories seed
  "white-whale" quests for rare/legendary species across the Banks, Ironbound, and the Smother.
  Questline: **"Tall Tales"** — chasing down whether his impossible fish stories are real (often the
  bread-crumb trail to legendaries in `fish-and-content.md`).

### 3.15 Core-cast summary table

| # | Name | Role | Home region | Primary hook |
|---|------|------|-------------|--------------|
| 1 | Uncle Ned Coddle | Departed uncle / origin | Coddle Cove (opening) | "Ned's Unfinished Lines" (framing) |
| 2 | Marguerite Faye | Auctioneer | Port Greywick | Selling + market intel |
| 3 | Capt. Reuben Stout | Harbourmaster | Port Greywick | Forecasts, berths, rescue |
| 4 | Aunt Ginny Coddle | Home keeper (family) | Coddle Cove | Home base + Ned's memory |
| 5 | Silas Boyne | Shipwright | Port Greywick | Boat ladder (buy/upgrade) |
| 6 | Odette Tranchemontagne | Chandler / gear shop | Port Greywick | Gear/bait/charts + rumours |
| 7 | Wallace "Wally" Pike | Rival skipper | Port Greywick / at sea | "The Same Water" rivalry |
| 8 | Bram Tiller | Tavern keeper | Port Greywick | Social/quest hub |
| 9 | Mother Edie Vance | Lighthouse keeper | Ironbound | Outer-water charts (Ironbound/Smother) |
| 10 | Harlan Boudreau | Processing-plant owner | Port Greywick | Value-add + contracts |
| 11 | Pearl Tobin | First deckhand (hireable) | Coddle Cove | First crew (→ economy doc) |
| 12 | Father Tomas Le Bris | Parson / community | Port Greywick | Festivals + closure on Ned |
| 13 | Iris Halloran | Buoy-tender / coast-tech | Port Greywick / Banks | Instruments + route to Smother |
| 14 | Old Joachim Furey | Retired master / lorekeeper | Port Greywick | Legendary-fish leads |

That is **14 named NPCs**: family (Ned, Ginny, plus Pearl as near-family), the town services
(auctioneer, harbourmaster, shipwright, chandler, tavern, parson, processor, coast-tech), the social
spice (rival), and the two lonely outer-edge mentors (lighthouse keeper, retired master). The mix
covers every service the game needs while keeping the town feeling like a real small community.

---

## 4. The procedural "EXTRAS" generator (P3)

Named NPCs give the world a heart; **extras** give it a *crowd*. The goal is a coast that feels
**populated and working** without authoring a hundred people — a busy market, dock workers shifting
crates, other skippers' boats coming and going, churchgoers on rest day, drinkers in the tavern at
night (P3). Extras are **flavour, not function**: they fill space, react to the world, and are
mostly non-interactive or shallowly interactive.

> **Extras vs STAFF — do not confuse.** Extras are *ambient background population* owned here. The
> *generic hireable crew* you employ in bulk (deckhands, plant workers) are **STAFF**, owned by
> `economy-and-business.md`. They may be generated with the *same techniques* described below (name
> pools, appearance variation), but a STAFF member is a managed employee with productivity and wages;
> an extra is set dressing. When an extra is "promoted" into something you hire, it crosses into the
> economy doc.

### 4.1 What the generator produces

- **Background townsfolk** wandering Port Greywick (shoppers, gossipers, kids, churchgoers).
- **Dock/wharf workers** loading, unloading, mending nets, pushing barrows on the public wharf.
- **Other skippers & working boats** — ambient boat traffic on the water (entering/leaving harbour,
  working the grounds, transiting the Rips at slack) that makes the *sea* feel inhabited too, not
  just the town.
- **Auction/market crowd** on market day; **tavern regulars** at night; **clam-diggers** on the
  Drownded Lands at spring low.

### 4.2 How they're generated

- **Name pools.** Procedural names drawn from authored **Maritime/North-Atlantic name pools**
  (given-name + surname lists with the right regional flavour — Acadian-French, Irish, Scots,
  English Newfoundland blends, matching the canon vernacular). Generates plausible names like
  "Cyril Mercer," "Brigid Aucoin," "Ezra Crewe" without anyone authoring each. Name pools are
  localizable data assets.
- **Appearance variation.** A **layered paper-doll** system: a body/skin base + interchangeable
  sprite layers (hair, hat/sou'wester, oilskins/sweater/apron, boots, beard, a few palette swaps),
  combined randomly within **role-appropriate sets** (a dock worker gets oilskins and boots; a
  market shopper gets a coat and basket). A handful of layers combinatorially yields plenty of
  distinct-looking people on a tiny art budget (mobile-friendly, `art-and-audio-bible.md`).
- **Schedule templates.** Extras don't get bespoke schedules; they're assigned a **role-based
  schedule template** (`dock_worker`, `market_shopper`, `tavern_regular`, `churchgoer`,
  `clam_digger`, `ambient_skipper`) — the same conditional-schedule shape as named NPCs but shared
  across many instances, with light per-instance randomisation (start time jitter, which anchor in a
  set). Templates obey the same **reactivity rules** (§2.3): dock workers thin out in a storm,
  market crowds swell on market day, clam-diggers appear only at low water, ambient skippers stay in
  harbour in a gale. This is what makes the *crowd* feel like it reads the sea, not just the named
  cast.
- **Spawn budget & pooling.** Extras are spawned to a **per-region population budget** (e.g., N
  visible at once, scaled by region + time + weather + market day) from an **object pool**, and
  despawned/recycled as the player moves or the scene changes. They are ephemeral — not persisted as
  individuals between sessions (a dock worker today need not be the same one tomorrow), which keeps
  save data and memory tiny.
- **Shallow interactivity.** Most extras offer **ambient barks** (short, flavourful, context-aware
  one-liners pulled from pools — "Mind the tide's turning," "Wally's hold was full again, the
  show-off") rather than real dialogue trees. A few can be **lightly transactional or quest-relevant
  on demand** (a dock worker who'll sell you info, a digger who marks a clam bed) — but anything with
  persistent state or employment is promoted to a named NPC or to STAFF.

### 4.3 The authored↔procedural seam for people

Mirroring the world doc's seam (`world-and-regions.md` §3): **regions author "population anchors and
budgets"** (where crowds gather, how many, which templates), and the **extras generator fills them**.
A market square authors "up to 20 `market_shopper`/`tavern_regular` extras, peaking on market day,
none in a storm"; the generator pours plausible people into that bucket. Identity (named cast) is
authored; scale and variety (the crowd) is procedural — exactly the canon principle.

---

## 5. Relationship & reputation system

Two linked tracks, kept deliberately modest in scope and tied to pillars (no sprawling sim):

### 5.1 Personal relationships (with named NPCs)

- **A relationship/friendship level per named NPC** (a small points/heart-style scale, Stardew-legible
  — canon borrows Stardew's NPC-relationship skeleton, §4). Raised through **interaction**: chatting
  daily, completing their questlines, **helping with their work** (a tide-time favour, a needed
  catch, a delivery), and **gifts** (NPCs have liked/disliked items — fish, gear, processed goods —
  giving the catch economy a social outlet).
- **What it unlocks (kept concrete and pillar-serving):**
  - **Service improvements** — better auction fees/intel (Marguerite), better forecasts and rescue
    priority (Reuben), discounts/unique boat work (Silas), gear deals and leads (Odette), processing
    terms (Harlan).
  - **Gated content** — the relationship-locked charts (Edie's Ironbound/Smother charts; Iris's
    Smother route), legendary-fish leads (Joachim), the first crew member (Pearl).
  - **Story & warmth** — personal questlines, deeper dialogue, the Ned-memory threads (Ginny, Tomas),
    and town belonging.
  - **Long-term life beats** — festivals, and optionally companionship/marriage arcs via Father Tomas
    (scope-flagged; confirm with `progression-and-housing.md`).
- **Decay is gentle or absent.** Per the cozy promise (P5 / anti-pillar "not a grim survival sim"),
  relationships should not punish absence harshly — at most a soft, slow drift, never a grind to
  maintain. Friendship is a reward, not a chore.

### 5.2 Town reputation / standing (with the community as a whole)

- **A town-standing track** representing your reputation across the Sablewick Banks as a whole,
  separate from individual friendships. Raised by **landing good catches, fulfilling contracts,
  helping in community events/crises (a storm rescue, saving the tavern, a festival), and generally
  being a reliable presence** — and dented by failures that matter to the community (a botched
  contract, leaving someone in trouble).
- **What it unlocks:** **access and opportunity** — better contracts (`economy-and-business.md`), the
  right to buy certain property (`progression-and-housing.md`), respect from the rival (Wally),
  invitations to community events, and a general sense that the coast has come to *count on you*. It
  is the macro-expression of "earning your place" that the whole game is about (P2, P3).
- **Reputation as P4 enabler.** Standing is also a soft prerequisite for the **business/commerce
  endgame** (the Shipping Lanes' "business unlocks" gate, `world-and-regions.md` §6.9) — the town has
  to trust you before it lets you run its freight. This ties reputation directly into "earn it, then
  automate it."

### 5.3 Mechanisms summary

| Mechanism | Affects | Pillar | Owned/defined where |
|-----------|---------|--------|---------------------|
| Daily chat / dialogue | Personal relationship | P3 | Here |
| Gifts (liked/disliked items) | Personal relationship | P3 | Here (item likes); items in `fish-and-content.md` / `economy-and-business.md` |
| Helping with NPC work / favours | Personal relationship | P3, P4 | Here (hook); mechanics in relevant doc |
| Completing questlines | Personal + town | P2, P3 | Here (hooks); systems in relevant docs |
| Landing catches / fulfilling contracts | Town standing | P2, P4 | Town effect here; contracts in `economy-and-business.md` |
| Community events / crises (rescue, festival) | Town standing | P3, P5 | Here (events) + relevant docs |

Keep it tight: **two tracks, a handful of inputs, concrete unlocks, gentle decay.** Resist the urge
to build a relationship sim; build a relationship *layer* that makes the living coast feel like it
knows you.

---

## 6. Dialogue approach (high level)

- **Data-driven & localizable from day one.** All dialogue is **data, not hard-coded strings**, keyed
  by localization IDs (`../adr/0003-data-driven-content.md`); the cast's display names and every line
  reference keys (see the `displayNameKey` in §2.5). Mobile ships in multiple languages; English is
  one locale, not the source of truth baked into logic.
- **Context-aware line selection.** An NPC's current line is chosen from a **pool filtered by
  context** — who they are, their **current activity** (the schedule block), **world-state** (weather,
  tide, season, market day), **relationship level**, and **story flags**. The harbourmaster says
  different things in a gale than in calm; the auctioneer greets a stranger differently than a trusted
  regular; everyone reacts to a spring low or a nor'easter. This is dialogue as another expression of
  the living, reactive coast (P1, P3) — the same world-state that bends routines also bends what people
  *say*.
- **Layered content depth.** Three tiers: **ambient barks** (one-liners, especially for extras and for
  named NPCs in passing), **conversational dialogue** (greetings, relationship-gated chat, reactive
  remarks), and **scripted questline dialogue** (authored beats for the core-cast hooks in §3).
- **Lightweight structure, room to grow.** Start with **simple branching / pooled lines** (Stardew-grade),
  authored as data assets per NPC, with the schema able to grow toward conditions, variables, and
  small choice trees without re-architecting. A node/graph dialogue tool can sit on top of the data
  later; the *format* is the commitment now, not a heavy engine.
- **Voice & tone.** Warm, weathered, grounded, lightly salted with the canon Maritime/Newfoundland
  vernacular (sunkers, rips, drownded, nor'easter, the banks) — *flavour, never caricature* (canon
  §5.1). Each named NPC has a consistent voice (Marguerite clipped and sharp; Joachim rambling and
  tall-tale; Reuben terse and steady) defined alongside their data.

---

## 7. Implementation notes

(Engineering shape only; defers to ADRs and sibling docs for specifics. Mobile-first per canon §5.6.)

- **Everything is a ScriptableObject.** `NpcDefinition`, `ScheduleDefinition`, `ScheduleTemplate`
  (for extras), `DialoguePool`/`DialogueSet`, name pools, and appearance-layer sets are all authored
  data assets (`../adr/0003-data-driven-content.md`). Designers add/edit NPCs and schedules without
  touching code.
- **Schedule = data, planner = code.** The conditional-schedule structure in §2.5 is pure data; a
  small **`RoutinePlanner`** system evaluates `SelectSchedule → BlockAt → target anchor` each tick
  (or less often — see below) and drives movement/animation/interactability. World-state inputs
  (time, tide phase, weather, season, day-of-week) come from the time/tide/weather sim
  (`time-tides-weather.md`) via a shared read-only world-state struct.
- **Tiered simulation for mobile.** Three tiers keep CPU/memory tiny:
  1. **Active (on-screen / current region):** fully simulated — pathfound movement, animation,
     interactable, dialogue.
  2. **Nearby (current region, off-screen):** schedule evaluated but **no pathfinding** — snapped to
     the current block's anchor (`virtualPosition`), ready to "pop in" correctly when seen.
  3. **Dormant (other regions):** not ticked at all; their position is *computed on demand* from
     schedule + clock the moment the player enters their region. NPCs are **positional truth on
     demand**, not a continuously running crowd. (§2.4)
- **Pathfinding.** A* on the region navmesh/tile grid for land NPCs; **waypoint/spline routes** for
  ambient working boats (cheaper than full nav on water). Pathing is **point-to-point between
  anchors** (not free roam), respects **opening hours and the tide layer** (`world-and-regions.md`
  §7), and is only ever run for **Active**-tier NPCs. Cap concurrent pathfinding queries; stagger
  re-paths; use a low planner tick rate (e.g., re-evaluate schedules a few times a game-hour, not
  every frame).
- **Extras = pooled & ephemeral.** Extras spawn from an **object pool** to a per-region/time/weather
  **population budget**, use **shared role templates** and the **layered paper-doll** for variety,
  emit **pooled barks**, and are **not persisted** between sessions. (§4)
- **Save data is light.** Persist per **named** NPC: relationship level, story/quest flags, and any
  unlocked-state. Do **not** persist extras or moment-to-moment positions (positions are recomputed
  from schedule + clock on load). Town standing is a few scalars. This keeps the save small and the
  load fast on mobile.
- **Localization-first text.** All names/dialogue via localization keys; no literal player-facing
  strings in code or logic (§6).
- **Anchors authored in scenes.** Location anchors (`home`, `work`, `tavern`, `keeper_lamp`, etc.)
  are placed in the region scenes (`world-and-regions.md` §9) and referenced by id from NPC data, so
  the same NPC data is scene-portable and designer-editable.

---

## 8. Open questions / decisions deferred

- **Relationship depth & marriage/companionship.** Do we ship Stardew-style romance/marriage (via
  Father Tomas, §3.12), and with whom of the cast? Scope-sensitive; confirm with
  `progression-and-housing.md` and the roadmap before committing art/dialogue. (§5.1)
- **How interactive should extras be?** Pure flavour (barks only) vs occasionally transactional
  (sell info, mark a spot)? Risk: making extras too useful blurs the line with named NPCs and STAFF.
  Recommend starting barks-only and promoting specific needs to named NPCs. (§4.2)
- **Pearl's promotion path into STAFF.** Exactly where the hand-off happens between "named friend you
  crew with" (here) and "managed employee/skipper" (`economy-and-business.md`) needs a clean seam so
  she doesn't exist in two systems at once. Co-design with the economy doc. (§3.11, §1)
- **Reactivity richness vs authoring cost.** How many conditional schedule variants per named NPC is
  worth it? (Storm + market + rest + season + festival + relationship could explode the authoring
  matrix for 14 NPCs.) Need a sensible default set + only the high-value overrides. (§2.3)
- **Planner tick rate & path budget tuning.** Exact off-screen tick cadence, concurrent-path caps,
  and extras population budgets per region are **profiling-driven** on target mobile hardware; the
  tiers (§7) are the architecture, the numbers are TBD. 
- **Day-of-week / calendar ownership.** This doc assumes a weekday cycle (market day, rest day) layered
  on the canon seasons + moon. Confirm whether the calendar (and which day is market/rest) lives in
  `time-tides-weather.md` and is merely consumed here. (§2.2)
- **Cross-region NPC travel.** A couple of NPCs (Iris servicing Banks buoys; the rival at sea) move
  between regions. Confirm whether any *named* NPC needs to be simulated *across* a region boundary
  at once, or whether dormant-tier snapping is always sufficient. (§7)
- **Festival/event system home.** Father Tomas's seasonal festivals (§3.12) and community crises imply
  a town-event system. Does it live here (as NPC-driven routines/events) or in a dedicated
  events/progression doc? Flag for the roadmap. (§5.2)
- **Final name/voice pass.** Surnames and voices above are a first authored pass; run a consistency +
  localization + cultural-sensitivity review with the narrative pass (and reconcile with the
  named-place inventory in `world-and-regions.md` §10).

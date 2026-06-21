# Hidden Harbours — UX & Mobile Controls

> Design module. Subordinate to `../vision-and-pillars.md` (CANON). Specifies the control schemes
> (now **PC-first** per ADR 0005 — see the note below; the touch design is retained as the
> mobile-port spec), the HUD, the on-screen menus, session design, cross-platform input/layout, and
> accessibility. Serves every pillar but is the **front line of P1 (The Sea Has Moods)** — making
> tide and wind *legible* is treated here as a first-class UI problem.
>
> Sibling docs: `../architecture/tech-architecture.md` (input abstraction, platform scale-up),
> `time-tides-weather.md` (the tide/wind/clock data the HUD shows), `boats-and-navigation.md`
> (boat handling the sailing controls drive), `economy-and-business.md` (market/business screens'
> content), `progression-and-housing.md` (skills/housing screens, session-banking, offline stance),
> `art-and-audio-bible.md` (UI *art* direction; this doc owns *layout & behaviour*).

> ⚓ **PC-FIRST NOTE (ADR 0005, 2026-06-20) — read this before the rest.** The project's **primary
> target is now landscape desktop with keyboard/mouse + gamepad** (Windows/desktop). This doc was
> written mobile-first, and **all of that touch / one-thumb / portrait design is kept on purpose** —
> but it is now **future-mobile-port scope**, not the primary build. For the PC-first build, read the
> touch sections as: *the same HUD, screens, and interactions, with the bindings retargeted to
> KB/mouse/gamepad and the layout in **landscape**.* Crucially, **the input *intent* architecture
> (VS-02 — `Move/SetThrottle/SetHeading/Interact/Haul/OpenMap/Confirm/Cancel/Zoom`) is UNCHANGED**:
> only the device→intent **bindings** retarget (see §9, §10). Nothing about the gameplay, the HUD's
> information set, or the screens changes — only which device drives the intents and how the layout
> reflows. The touch design here is the **port spec** when mobile is built.

---

## 1. Design goals

1. **One thumb should be enough** for the core loops (walk, sail, fish) so the game is genuinely
   playable on a phone, one-handed, in short bursts.
2. **The sea is always legible.** Time, tide, wind, sea state, season/weather and heading are
   *always glanceable* without opening a menu (P1). If a player runs aground, it should be because
   they misjudged a clearly-shown tide, not because the UI hid it.
3. **Sailing feels like seamanship, not a driving game.** Steering against wind and current is the
   skill fantasy (P1/P2); the controls must make wind/current *felt* through touch, with assists so
   it's approachable but masterable.
4. **Complexity folds down.** The business/management depth (Big Ambitions DNA) must collapse into a
   summary-first, card-based UI a thumb can manage — depth on demand, never a spreadsheet thrown at a
   phone.
5. **Respect mobile time & input later.** Short and long sessions both feel complete; the whole
   scheme maps cleanly to mouse/keyboard and gamepad with no rewrite (input abstraction).

---

## 2. Orientation & one-handed stance

**Decision (PC-first, ADR 0005): landscape-primary on desktop. Portrait-primary is retained as the
future-mobile-port stance** (the original decision, below, governs the mobile port).

> On PC the game runs **landscape**, using the wider sightlines (open water, big ships) and a
> spread-out top read-only band; the thumb-zone / one-handed reasoning below applies to the mobile
> port, not the desktop build.

**Original mobile-port decision: portrait-primary, landscape-supported.**

- **Portrait is the canonical, one-handed mode.** It suits the cozy, glanceable, "pick up for a
  quick run" fantasy and one-thumb reach; the HUD and controls are designed portrait-first.
- **Landscape is fully supported** for players who want a wider view (especially out on open water /
  piloting big ships, where horizontal sightlines help) and for tablets. Layout is **responsive**
  (§9), repositioning HUD/controls rather than redrawing.
- **Reachability:** interactive controls live in the **lower third / thumb arcs**; glanceable
  read-only info lives **top** (out of the thumbs' way). Right- and left-handed layouts mirror
  (accessibility, §8).

---

## 3. Core controls

A shared principle: **a context-sensitive Action Button** (bottom-right thumb zone) whose label and
icon change with context — *Fish / Haul / Board / Dock / Talk / Pick up / Use / Cast Off*. One
learned button, many verbs. A secondary radial or small cluster appears only when multiple actions
are available.

### 3.1 Walking on land

- **Movement:** a **floating virtual joystick** (left thumb) — appears where the thumb first
  touches the lower-left zone (not fixed, so it fits any hand). 8-way feel, free analog direction;
  character animates to nearest of 4 ¾ facings (`art-and-audio-bible.md` §3.4).
  - *Alternative supported:* **tap-to-move** (tap a destination, character pathfinds) for relaxed
    one-thumb play. Both on; player can lean on either. Tap-to-move is also the accessibility-friendly
    default.
- **Interact:** the context Action Button, or simply walk into / tap an interactable (NPC, door,
  trap, shop). A subtle highlight shows the current interactable.
- **No combat controls** — there is no combat (anti-pillar). Frees the whole scheme for traversal &
  work verbs.

### 3.2 Sailing (the hard one)

The goal: steering a boat against **wind** and **tidal current** is the seamanship skill (P1), and
touch must make those forces *felt* — but remain learnable in 30 seconds and masterable over hours.

**Chosen scheme: "Throttle + Heading," assisted.** (Rationale after.)

- **Throttle (left thumb): a vertical slider / lever** pinned to the lower-left (diegetic brass
  throttle, `art-and-audio-bible.md` §7). Drag up = ahead (more power = more speed & more authority
  over wind/current), center detent = neutral, down = astern/reverse. Tiny boats (dory) also offer an
  **oar mode** (tap-pulse) at the low end. The throttle position is *held* (set-and-forget) so it's
  one-thumb friendly — you're not pinning it every frame.
- **Heading (right thumb): tap/drag-to-steer.** Tap a point or drag a heading vector on the water and
  the boat **turns toward it at a rate limited by its handling** (small craft turn quickly; a tanker
  turns ponderously — `boats-and-navigation.md`). Hold-drag for continuous steering; tap for "make
  for that point." This keeps both the intimacy of pointing where you want to go and the *weight* of a
  slow-turning hull.
- **The forces you fight (the P1 core):**
  - **Wind** pushes the boat (leeway) and, for the dory/skiff, dominates at low power — you must
    *crab* into it, carry more throttle upwind, and mind being blown onto a lee shore (P5).
    Wind is shown as a vector you can read against your heading.
  - **Tidal current** sets you sideways/along — critical in **Fundy Rips** and around **The
    Sunkers**, where you must *time the tide* and angle for the set. Current is shown as flow on the
    water + a HUD readout.
  - The boat's **handling** stat governs how vulnerable it is; bigger/heavier hulls resist wind but
    are sluggish to turn — a different kind of difficulty (anticipation, not reflex).
- **Assists (approachable → masterable), each toggleable:**
  - **Heading-hold / autopilot-to-point:** by default the boat *holds the commanded heading*,
    auto-compensating *some* leeway/set so casual players aren't fighting drift every second; the
    *residual* drift still matters (you feel the sea), and turning off the assist gives experts full
    manual seamanship (and a small skill/reputation flavour of "real sailing").
  - **Set-&-drift predictor:** a faint **ghost track / drift arrow** shows where you'll actually go
    given current heading + wind + current — teaches the forces visually (great onboarding, stays on
    by default, can hide for challenge).
  - **Grounding warning:** as you approach water too shallow for your **draught** at the current
    **tide** (P1's grounding rule), the depth shades red and a haptic + audio warns (P5 telegraph,
    not ambush). Optional "hard stop / refuse to ground" beginner assist.
  - **Docking assist:** near a berth, a "Dock" Action Button auto-aligns the final approach (parking a
    big hull by hand on a phone is no fun); experts can dock manually for flavour.

> **Why throttle + tap-heading over a single virtual stick?** A twin-stick "drive" scheme fights the
> fantasy: boats don't strafe, they make way and turn slowly, and *throttle is itself a tactical
> choice* (power vs fuel vs control of drift). Splitting **power (held slider)** from **intent
> (tap/drag heading)** is one-thumb-each, set-and-forget friendly for long passages, and lets the
> assists layer cleanly. A pure virtual stick remains available as an **accessibility/"arcade"
> alternate** (§8) for players who prefer it.

### 3.3 Fishing (simple touch timing/tension)

Fishing is the heart of the early game and must be *satisfying in seconds* on a phone, with depth
that scales as techniques unlock (`progression-and-housing.md` §2.1 Fishing proficiency).

- **Cast:** tap the Action Button (**Fish**) — or a short flick to aim/distance for techniques that
  want it. Bite indicated by line tug + haptic + audio (a gull cry, a splash).
- **Hook:** a brief tap on the bite (a light timing beat — not twitchy; a forgiving window).
- **Haul (the tension interaction):** a **tension meter / line-stress** mini-interaction:
  - **Hold to reel** (tap-and-hold the Action Button, or drag down a small reel control) pulls the
    fish in but **raises line tension**; a tension bar fills toward a red **snap** zone.
  - **Release** lets the fish run and tension fall. The skill is *pulsing* — reel when the fish tires,
    ease when it surges — to land it without snapping the line (clean lands feed the Fishing skill).
  - Bigger/stronger fish = more surges, tighter tension band, longer fight; legendary fish are a real
    (but never twitch-reflex) test. The whole thing works **one-thumb**.
- **Technique evolution:** the *same core interaction* dresses up as techniques unlock — handline
  jigging → longline (set/haul many hooks, a light management beat) → traps (set & check on a timer)
  → nets/dragging (the offshore boats; more about positioning your vessel than per-fish tension).
  Consistency keeps it learnable while progression adds variety (P2/P4).
- **Feedback:** strong, juicy feedback on a land — the fish flips on deck, a stat/coin popup, a
  little music sting (`art-and-audio-bible.md` §8.3). The *first* cod sold is the canon triumph beat.

---

## 4. The HUD (first-class P1 problem)

The player must **always** be able to read the sea at a glance. The HUD is compact, lives mostly in
the **top band** (out of thumb zones), and is **glanceable in under a second** — this is the most
important UI in the game (P1).

### 4.1 What must always be visible

| Element | What it shows | Form |
|---|---|---|
| **Clock** | Current 24h time + day/season | Small digital + a day-arc; tap to expand calendar |
| **Tide** | **State (rising/falling), height, and time-to-next-turn** | A dedicated **tide gauge** (see §4.2) — the most legible widget |
| **Wind** | **Direction (relative to you) + strength** | A **wind arrow / windsock** (see §4.2) |
| **Sea state** | Calm → gale (1–5ish) | Icon + colour, often fused with wind/weather cluster |
| **Weather** | Now + a short forecast hint | Weather icon; tap for forecast (barometer) |
| **Compass / heading** | Cardinal heading + N reference | A **compass ribbon or rose** (see §4.2) |
| **Money** | Cash on hand | Small, top corner |
| **Context/skills** | Current vessel, hold fullness when fishing/hauling | Contextual, fades when irrelevant |

Everything else (full ledgers, charts, business) lives in **menus**, one tap away — the HUD only
carries what you must read *while acting*.

### 4.2 The tide & wind widgets (treat as a core feature)

Because tide/wind legibility *is* a pillar, these get bespoke, redundant-coded design (shape + colour
+ motion + text, never colour alone — see accessibility §8):

- **Tide gauge:** a small **vertical gauge** (a marked piling/tide-staff motif) showing current water
  level on a min–max range, an **arrow up/down** for rising/falling (shape, not just colour), the
  numeric height, and a tiny **"⤣ in 1:42"** to the next turn. Spring/neap is hinted by the range
  width or a moon glyph. A glance answers the only questions that matter: *is it coming or going, how
  high now, how long till it turns* — the inputs to every grounding decision (P1/P5). Tapping opens
  the full **tide table** (the diegetic almanac).
- **Wind widget:** a **compass-anchored arrow** showing wind **direction relative to the screen/your
  heading**, with **strength** encoded by **arrow length + barb count + colour band + a label
  (kts/Beaufort)** — redundant coding so it reads for everyone. It visibly **animates/strengthens**
  before weather turns (matching the rising-wind audio tell, `art-and-audio-bible.md` §8.4). When
  sailing, it pairs with the **set-&-drift predictor** (§3.2) so the player connects "wind from
  there" to "I'll drift to here."
- **Compass:** a thin **heading ribbon** (N/E/S/W ticks scrolling) or a small **rose**; at sea it
  also marks bearing to known waypoints/home. Essential in fog (**The Smother**) where you navigate by
  instrument (P1).
- **Fusion:** wind + sea-state + weather can share one **"conditions" cluster** to save space, with
  the **tide gauge kept visually distinct** (it's the highest-stakes read). Glanceability beats
  completeness — secondary detail is a tap away.

### 4.3 Layout (portrait)

```
┌─────────────────────────────────────────────┐
│ [clock/day]      [conditions: wind|sea|wx]  $│  ← top band: glanceable, read-only
│ [compass ribbon ────────────────]   [tide ▲]│
│                                              │
│                                              │
│                 GAME WORLD                   │  ← ¾ view, follow-cam
│                 (¾ top-down)                 │
│                                              │
│                                              │
│  ╭──────╮                          ╭──────╮  │
│  │throttl│  (only when sailing)    │ACTION│  │  ← bottom band: thumb controls
│  │ /stick│   joystick when ashore  │ btn  │  │
│  ╰──────╯                          ╰──────╯  │
│  [≡ menu]                          [map/chart]│
└─────────────────────────────────────────────┘
```

- Top band is **never** occluded by thumbs. Bottom band holds movement/throttle (left) and the
  context Action Button (right), with menu and map shortcuts at the corners.
- The HUD **auto-simplifies by context:** ashore, the tide/wind shrink to small icons (still
  present — P1) and the throttle is replaced by the walk joystick; at sea they expand to full
  widgets. Dialogue/menus dim the world and pause real-time where appropriate (§7).
- A **reduce-clutter toggle** can collapse non-essential HUD to just clock+tide+wind+compass.

---

## 5. Screens & menus (small-screen patterns)

Global pattern: **summary-first, card-based, progressive disclosure.** Each screen opens on a
glanceable summary; tap a card to drill in. Bottom-or-side **tab bar** for top-level destinations.
Big touch targets, vertical scroll, minimal nesting (aim ≤ 2 taps to anything common).

### 5.1 Inventory / Hold

- **The boat Hold** and **personal inventory** as scrollable **grids** with category filters (fish /
  cargo / gear / supplies). Each fish card: sprite, name, weight/quality, value-now.
- **Hold fullness bar** prominent (you must know when you're full at sea). Quick actions: stow, dump
  (with a spoilage/reputation warning — P3/P5), transfer to warehouse at dock.
- Long-press a stack for details/actions; drag to rearrange where relevant (home/warehouse layout
  ties to `progression-and-housing.md`).

### 5.2 Market / Auction

- **Summary-first:** a **chalkboard** of current prices with **up/down trend arrows** and your
  hold's sellable value at a glance (`economy-and-business.md` for the supply/demand model).
- **Sell flow:** tap an item → slider/stepper for quantity → confirm; "sell all of type" and "sell
  all" shortcuts for fast mobile trading. Show price impact if you flood the market (teaches the
  economy).
- **Auction UI:** time-boxed lots shown as cards with current bid, your max-bid (set-and-forget
  proxy bidding so you don't babysit), and a watch list. Notifications on outbid/win.
- **Storage-to-time-sales:** clear UI to send a catch to a warehouse to sell later when prices rise
  (the core econ skill) — surfaced right from the market and the hold.

### 5.3 Business / Management (the complexity test)

The Big-Ambitions-style depth (staff, facilities, logistics) **must** fold onto a phone. Approach:

- **Dashboard-first:** one screen of **summary cards** — *Today's net*, *Properties (n) status*,
  *Staff (n) — any issues?*, *Active contracts/routes*, *Alerts*. Green/amber/red status dots so the
  player sees what needs attention in one glance. No spreadsheets up front.
- **Drill into a card → a focused sub-screen:**
  - **Properties:** list of berths/warehouses/plants/shops as cards (status, throughput, upgrade
    button) → tap one for its detail/upgrade/customize (mechanics in `progression-and-housing.md`;
    economics in `economy-and-business.md`).
  - **Staff:** roster cards (name, role, assigned-to, morale/efficiency) → assign/reassign via a
    simple picker; hire from a candidates list. Avoid micromanagement — assignment, not scheduling
    minutiae.
  - **Logistics / routes:** freight routes as cards (origin→dest, cargo, status, profit/run) → a
    simple route editor (pick endpoints on the chart, pick cargo, assign a hauler). Automation is
    *configured* here, then runs itself (P4).
  - **Contracts:** available/active contracts as cards (reward, deadline, reputation) → accept/track.
- **Principle:** the player **manages by exception** — the dashboard tells them where to look; depth
  is reachable but never forced. This is what makes a logistics empire thumb-playable.

### 5.4 Map / Charts

- A **nautical chart** (diegetic, `art-and-audio-bible.md` §7): pan/pinch; shows known regions,
  hazards (**sunkers**, **rips**), your boat, home, waypoints, weather overlay, and **tide-state
  shading** where relevant. Set waypoints (feeds the compass/heading). Locked regions shown as
  fog/uncharted until unlocked (`progression-and-housing.md` gates).
- Doubles as **fast-travel** UI *if* we allow it (open question — must not trivialize the
  sailing/tide fantasy; coordinate with `boats-and-navigation.md`).

### 5.5 Boat / Upgrade

- Current vessel card (sprite at honest scale — the dynasty read, `art-and-audio-bible.md` §3.3) with
  stats: length, **draught** (tie to tide!), hold, crew, range, seaworthiness, handling.
- **Shipwright** browse-and-buy: tier cards with stats, price, **build-time (days)** and gate
  requirements (`progression-and-housing.md`). Refit/repair options. Owned fleet list with
  assign-skipper actions (late game).

### 5.6 Housing / Decor

- **Residence picker** (your homes) → per-home **decor mode**: a tile-grid placement editor
  (PPU=32 footprints) — drag furniture from a tray, rotate, recolor/paint, set wallpaper/flooring,
  place trophies. **Comfort** meter updates live (`progression-and-housing.md` §4.3).
- Touch-first: tap-to-place, drag-to-move, pinch to pan the room; a clean "edit vs play" toggle.
- Commercial customization (paint/signage/harbour-mark/layout) reuses the same editor in a lighter
  form.

### 5.7 Dialogue

- Bottom **dialogue panel** with portrait, name, text; tap to advance; choices as big tappable
  buttons. Keep text short and skimmable on a phone; optional auto-advance and history scroll.
  NPC routines/relationships per `npcs-and-routines.md`. Dim/soft-pause world during conversation.

### 5.8 Skills / Progression panel

- A thin **skills card** (the four proficiencies as labeled band-bars), license wallet, reputation
  per faction, and a **"What's next"** summary (next milestone/boat/license, active build timers) —
  the one-tap "what should I do?" from `progression-and-housing.md` §6. Band-ups celebrated with a
  small non-blocking toast.

---

## 6. Session design (mobile)

The game must reward both a **3-minute** stop and a **30-minute** sitting (P1/P2 arc made of many
short runs; `progression-and-housing.md` §6).

- **Short session ("a quick run"):** cast off, fish a nearby spot for a few minutes, sell or stow,
  done — always *some* money + proficiency banked. The Coddle Cove loop is tuned to be complete in
  minutes. Quick-resume drops you exactly where you were.
- **Longer session:** a real voyage (transit the rips, work the banks, weather a blow), market
  trading, business management, decorating — depth for when the player has time.
- **Pause & save anywhere:** the game **auto-saves frequently** and on background/exit; the player
  can stop *mid-run, mid-sail, mid-fight-with-a-fish* and resume. No "you'll lose progress if you
  quit now." Real-time (tide/weather) advances on the world clock, but the player is never punished
  for closing the app mid-action (a graceful "secure the boat" on background where needed).
- **Glanceable on-ramp:** opening the app shows the HUD's sea-state immediately and the "What's
  next" card, so a returning player knows in a second whether it's a good tide/weather to go out and
  what to aim for.

### 6.1 Offline / while-away behaviour (proposed stance)

The question: does the business run while the app is closed? **Proposed stance — "delegated
operations accrue offline, within bounds; the *fun* parts don't auto-play."**

- **Things that progress while away (because you *delegated* them — P4):**
  - **Build/refit/upgrade timers** complete on the world clock (`progression-and-housing.md` §5/§7).
  - **Staff-run facilities** (warehouses processing, plants refining, shops selling) accrue output/
    income at a **reduced "unattended" efficiency**, capped to an **accrual window** (e.g. up to
    ~8–12 in-game-hours / a tunable real-time cap) so you can't fully idle to victory, and so logging
    in still matters. Tuning lives with `economy-and-business.md`.
  - **Automated freight routes** complete runs and bank profit, same accrual cap.
- **Things that do NOT auto-play (because mastery must be earned — P4, anti-idle pillar):**
  - **Hand-fishing, sailing, exploring, decorating, trading decisions** — the *player's* verbs. You
    must be present for the parts that are the actual game. The sea is not auto-sailed.
- **On return:** a brief **"While you were away"** summary (income accrued, runs completed, builds
  finished, any alerts — a staff issue, a missed contract) so absence feels *productive but bounded*,
  never punishing and never a substitute for playing.
- **Rationale:** honours **P4** (delegate the tedium, do the craft by hand) and the **anti-pillar**
  "not a pure idle game," while respecting mobile reality (people close the app). Final caps/rates are
  an economy-balance decision (`economy-and-business.md`); this doc fixes the *stance*.

---

## 7. Pausing & time model in UI

- **Real-time world** (tide/weather/clock) runs during active play (P1).
- **Menus/dialogue:** most management menus and dialogue **soft-pause** the world (or at least the
  *threat* clock) so a player isn't grounded by a tide while deep in a sub-menu — pausing/saving
  anywhere is a stated goal. Sailing/fishing are real-time (that's the game).
- **Explicit pause** available; backgrounding the app auto-saves and effectively pauses player
  presence (offline accrual per §6.1 handles delegated systems).

---

## 8. Accessibility

Accessibility is a first-class requirement, doubly so because **P1's tide/wind reads must work for
everyone** (colourblind, low-vision, motor, motion-sensitive players).

- **Colourblind-safe sea indicators (critical):** tide/wind/sea-state/depth-warning use **redundant
  coding — shape + icon + motion + text/number**, never colour alone. Provide **colourblind palettes**
  (deuter/prot/tritanope) validated against the master ramp (`art-and-audio-bible.md` §4.1). The
  grounding-danger cue is multi-channel (red shade **+** hatch pattern **+** haptic **+** audio).
- **Text size:** scalable UI text (at least S/M/L/XL), high-contrast text option (scrim behind text on
  the weathered UI, `art-and-audio-bible.md` §7), and a clean legible-font toggle over the pixel font.
- **Reduce motion (water):** a **reduce-motion** option that calms water animation, parallax, screen
  shake, and weather particles (`art-and-audio-bible.md` §6) for motion-sensitive players — without
  removing the *information* (tide/wind still read via the widgets).
- **Simplified controls:** options for **tap-to-move** (already default-available), the **virtual-stick
  "arcade" sailing** alternate (§3.2), **stronger assists** (heading-hold, auto-dock, refuse-to-ground),
  one-handed/left-handed layouts, and adjustable touch-target size. A "casual seamanship" preset turns
  all assists on for players who want the cozy without the teeth.
- **Audio accessibility:** since rising wind is a danger tell (P1/P5), provide **visual equivalents**
  (the wind widget animating, a weather-warning toast) and **subtitles/captions** for important
  diegetic cues (foghorn, hails). Independent volume sliders (music/ambience/SFX/UI).
- **Haptics:** used for bites, grounding warnings, and confirmations; **fully toggleable**.

---

## 9. Scaling up to desktop / console

> **PC-first reframe (ADR 0005):** desktop with KB/mouse/gamepad is now the **primary** target, so
> this section describes the *primary* bindings (and the touch mappings below become the
> **mobile-port** direction). The key invariant is unchanged and load-bearing: **the input *intent*
> set (VS-02) does not change — only the device→intent bindings retarget.** Gameplay reads intents,
> never raw input, so PC-first is a binding/layout swap, not a rewrite, and the mobile port stays a
> binding/layout swap too.

Everything above is built on an **input abstraction** so the same UX maps to mouse/keyboard and
gamepad **without a rewrite** (per `../architecture/tech-architecture.md`).

- **Abstract *intents*, not inputs.** Game code consumes intents — `Move(vector)`, `SetThrottle(f)`,
  `SetHeading(point/dir)`, `Interact`, `Haul(hold/release)`, `OpenMap`, `Confirm/Cancel`, `Zoom` —
  emitted by a device layer. Touch, mouse+keyboard, and gamepad each map their raw input to these
  intents. Use Unity's **Input System** (action maps per device, runtime rebinding).
- **Touch → mouse/keyboard:** virtual joystick → WASD; throttle slider → W/S or scroll; tap-heading →
  mouse-click/point; Action Button → E/F/Space (context); pinch → scroll-zoom; HUD widgets are
  identical (just mouse-hover tooltips added).
- **Touch → gamepad:** left stick → move/throttle-axis; right stick → heading; face button →
  context Action; bumpers/triggers → throttle or secondary; d-pad → menu nav. Assists carry over;
  the set-&-drift predictor and tide/wind HUD are platform-agnostic.
- **Responsive UI for big screens:** the same **summary-first card** screens reflow to multi-column
  on desktop/console (more cards visible at once), and the HUD spreads out — but the *information set*
  and the *interaction model* are unchanged. No separate desktop UI codebase.
- **Why this matters now:** designing the scheme **as intents from day one** is exactly what lets
  the **PC-first** pivot (ADR 0005) happen as a binding/layout retarget rather than a rebuild — and
  what keeps the **mobile port** equally cheap to add later (the canon platform commitment).

---

## 10. Implementation notes

- **Input abstraction layer:** Unity **Input System** with per-device action maps emitting the shared
  **intents** (§9). A single `InputService` exposes intents to gameplay; gameplay never reads raw
  touch. Runtime rebinding + control-scheme switching (incl. on-the-fly touch↔controller on mobile
  with a gamepad). Detail/ownership in `../architecture/tech-architecture.md`.
- **UI tech — recommendation: UI Toolkit for menus/HUD, with pragmatic uGUI exceptions.** For a
  **data-driven, responsive, many-screens** mobile UI, **UI Toolkit** (UXML/USS) is the recommended
  default — its fl/grid layout makes the summary-first card screens reflow cleanly portrait↔landscape
  and phone↔desktop (§9), and styling is centralized for the diegetic-weathered look. **uGUI may be
  used where it's pragmatically better** — e.g. world-space/diegetic elements that must sort in the
  ¾ scene, or any case where UI Toolkit's world-space/runtime story is weaker for our needs. Lock the
  final split with `../architecture/tech-architecture.md` before building the HUD.
  - *Caveat:* validate UI Toolkit runtime performance and input on target phones early (it's matured,
    but profile it for the HUD which renders every frame).
  - *Pragmatic note (VS-17, ui-ux):* the **HUD v0** (clock/tide/wind/sea/money) is built in **uGUI**
    (ScreenSpaceOverlay Canvas + `UnityEngine.UI.Text`, code-driven, no prefab) — the always-on,
    no-alloc overlay is uGUI's sweet spot and it builds headless without a TMP-essentials import step.
    This does **not** lock the project-wide uGUI-vs-UI-Toolkit split (open question #6) — that stays a
    `lead-architect`/`tech-architecture.md` call; confirm before building the card screens.
- **Responsive system:** define **breakpoints/anchored zones** (top read-only band, bottom thumb
  band, safe-area insets for notches/home-indicator). Lay out by **thumb-reach zones**, not absolute
  pixels. Honor device safe areas.
- **HUD as data binding:** bind tide/wind/clock/weather widgets directly to the
  `time-tides-weather` simulation state via an observable model so they're always truthful (P1) and
  cheap to update; avoid per-frame layout churn.
- **Performance:** the HUD updates every frame — keep it lightweight (no allocations per frame, pooled
  elements). Profile on a mid-range phone alongside the water/lighting budget
  (`art-and-audio-bible.md` §6).
- **Settings surface:** all accessibility toggles (§8), assist toggles (§3.2), control scheme,
  orientation, haptics, and HUD-clutter level live in an easily-reached settings screen, with sensible
  cozy defaults (assists on-ish, tide/wind always shown).

---

## 11. Open questions

1. **Sailing scheme validation.** "Throttle + tap-heading" needs a **prototype playtest** against the
   virtual-stick alternate on a real phone — does steering against wind/current *feel* like seamanship
   and stay one-thumb-comfortable on a long passage? This is the riskiest UX bet; prototype first.
2. **How much assist by default?** Where's the line between "approachable" and "the sea loses its
   teeth" (P5)? Need to tune default heading-hold/leeway-compensation so casual players don't fight
   drift but the sea still bites. Coordinate with `time-tides-weather.md` and `boats-and-navigation.md`.
3. **Fast travel?** Do owned homes / charts allow fast travel, and if so how do we keep it from
   trivializing the tide/wind/sailing fantasy (P1/P2)? Decide with `boats-and-navigation.md` and
   `progression-and-housing.md`.
4. **Offline accrual caps & rates.** §6.1 fixes the *stance*; the exact accrual window, unattended
   efficiency, and per-facility rates are an **economy-balance** decision in `economy-and-business.md`.
   Also: do we ever notify (push) on a finished build / won auction / staff issue while away?
5. **Business UI ceiling.** How deep can the management UI go before it stops being phone-friendly?
   Define the hard cap on simultaneously-managed properties/staff/routes for mobile readability
   (with `economy-and-business.md`).
6. **UI Toolkit vs uGUI final split.** Confirm with `../architecture/tech-architecture.md` (esp. for
   world-space diegetic UI and HUD frame-cost) before building the HUD and the card screens.
7. **Portrait vs landscape default per context.** Is portrait truly best even when piloting a 110 m
   ship (where horizontal sightlines help)? Consider a gentle prompt/auto-suggest to rotate for
   offshore/big-ship play, or a context-aware layout that makes portrait work everywhere.
8. **Tide/wind widget final form.** §4.2 proposes the gauge/arrow/compass; needs a legibility pass at
   the smallest target phone size and across the colourblind palettes (§8) — the highest-stakes
   readability test in the game (P1).

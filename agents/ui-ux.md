# UI & UX — Charter

**Mission.** Make Hidden Harbours genuinely playable one-thumb on a phone: the always-glanceable
tide/wind/time HUD, the assisted touch sailing scheme, the satisfying fishing interaction, and the
summary-first card UIs that fold a logistics empire onto a 6-inch screen — all on an input
abstraction that scales to desktop/console without a rewrite.

**Pillars you most serve.** **P1 The Sea Has Moods** — making tide and wind *legible* is treated here
as a first-class UI problem; if a player grounds, it must be because they misjudged a clearly-shown
tide, not because the UI hid it. Supporting **P4** (manage-by-exception dashboards) and **P5** (the
grounding telegraph is multi-channel).

**You own.**
- `Assets/_Project/Code/UI/` — the HUD, all menus/screens (inventory/hold, market/auction,
  business/management dashboard, map/charts, boat/upgrade, housing/decor, dialogue, skills), and the
  on-screen control widgets (throttle/joystick, context Action Button).
- The **HUD's tide gauge, wind widget, and compass** — bespoke, redundant-coded (shape + colour +
  motion + text), bound to environment state so they're always truthful and cheap.
- **Input mapping** at the UI layer: the touch scheme ("Throttle + tap-Heading," assists), and the
  per-device mapping of raw input → the shared **intents** (`Move`, `SetThrottle`, `SetHeading`,
  `Interact`, `Haul`, `OpenMap`, `Confirm/Cancel`, `Zoom`). *(The `InputService` interface itself
  lives in Core — lead-architect; you map onto it.)*

**You do NOT own / hand off.**
- The boat handling the controls drive → **gameplay-systems** (you emit intents; they apply forces).
- The tide/wind/clock *data* → **gameplay-systems** (you display it). Market/business *logic* and *what
  the screens decide* → **economy-sim** (you build the panels; they own the numbers).
- UI *art direction* (the weathered-diegetic look, materials, palette) → **art-pipeline** (you own
  *layout & behaviour*; they own *look* — keep the tide/wind widgets both legible *and* on-style).
- Region/quest/dialogue *content* → **world-content** (you render the dialogue panel they author).
  Localization *tables* are shared with world-content.

**Read first.** `../docs/design/ux-and-mobile-controls.md` (the whole doc — HUD §4, sailing §3.2,
fishing §3.3, screens §5, accessibility §8, platform scale-up §9) ·
`../docs/design/time-tides-weather.md` §3.6 (the tide table the HUD widget summarizes) ·
`../docs/design/art-and-audio-bible.md` §7 (UI art direction to stay in sync with) · `coordination.md`
(§1, §7) · `../docs/architecture/tech-architecture.md` §9 (input intents, multi-platform readiness).

**Core responsibilities.**
- Build the top-band HUD that is glanceable in under a second: clock, tide gauge (rising/falling +
  height + time-to-turn), wind arrow (direction + strength, animating before weather turns), sea
  state, weather, compass, money, and contextual hold fullness.
- Build the assisted sailing controls (held throttle slider + tap/drag heading) with toggleable
  assists: heading-hold, set-&-drift predictor (ghost track), grounding warning, docking assist.
- Build the one-thumb fishing interaction (cast → hook → pulse the tension meter to land) with juicy
  feedback on a land.
- Build summary-first, card-based, progressive-disclosure screens for market, business, map, hold,
  boat, housing, and dialogue — manage by exception, ≤2 taps to anything common.

**Definition of Done — domain specifics** (beyond `coordination.md` §3).
- **Tide/wind/sea-state/depth cues are colourblind-safe:** redundant coding (shape + icon + motion +
  text/number), never colour alone; the grounding cue is multi-channel (red shade + hatch + haptic +
  audio). Validate against the colourblind palettes.
- The HUD updates **every frame with no per-frame allocation** (pooled elements, observable data
  binding) and is profiled on a mid-range phone alongside the water/lighting budget.
- All player-facing strings go through **localization tables from M0** — no inline strings; UI scales
  S/M/L/XL with a high-contrast option and a reduce-clutter toggle.
- Controls are built **as intents** so the same UX maps to mouse/keyboard and gamepad with no rewrite;
  safe-area insets honored; portrait-primary, landscape-responsive.

**Collaboration & handoffs.** You depend on gameplay-systems for the environment/heading state you
display and the intents you emit into, on economy-sim for screen data, and on world-content for
dialogue/quest content. You depend on art-pipeline for UI art (placeholder/greybox now, file a backlog
item) — keep the tide/wind widgets in lockstep with their art direction. lead-architect owns the
`InputService` contract and the UI-Toolkit-vs-uGUI call.

**Per-phase focus.**
- **M0:** HUD v0 — clock + **tide gauge** (rising/falling, height, time-to-turn) + money + hold
  fullness; the fishing interaction UI (tension band + landing gauge + strain bar) with gameplay-systems;
  basic touch movement.
- **M1:** HUD v1 — bespoke **tide gauge + wind widget + compass**, conditions cluster, market
  chalkboard, sell screen (marginal-price slider); the sailing controls for the dory force model; the
  Ned onboarding flow UI with world-content.
- **M2+:** map/chart UI + fog-of-war reveal + tide-table tiers; the management dashboards (manage by
  exception); then (M3) logistics/route/staff UIs and (M4) fleet-command UX + the desktop/console input
  maps and responsive reflow.

**Guardrails.**
- The HUD only carries what you must read *while acting* — everything else is a menu one tap away.
  Glanceability beats completeness.
- A management empire must never mean more tapping — it means **policies over clicks**. Don't throw a
  spreadsheet at a phone.
- Don't read raw touch in gameplay-bound code — emit intents, so desktop/console "just works" later.
- Don't invent tide/market values in the UI — bind to the real simulation state (P1 truth).
- Sailing is *seamanship, not a driving game* — keep the throttle held/set-and-forget and let the
  assists (not a twin-stick) carry approachability.

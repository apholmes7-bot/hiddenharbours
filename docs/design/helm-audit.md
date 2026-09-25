# Hidden Harbours — The Helm Audit (helms you can read, switches that work, a helm for every hull)

> **Status: RULED 2026-09-25 (D1–D6). Two questions are still open: D2 (a) and (b) (§3.4).** This doc is the
> design of record for the helms pass. It holds what the 2026-09-24 audit found (ui-ux, arithmetic over the code
> and Data at `3fa9b606`, no Unity) and the owner's rulings on it. **Every build it names is chartered separately,
> later; this doc builds nothing.**
>
> It is subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md) (CANON). Sibling docs:
> [`diegetic-instruments-and-consoles.md`](diegetic-instruments-and-consoles.md) (the helm consoles: the first
> build and its why), [`ux-and-mobile-controls.md`](ux-and-mobile-controls.md) (the HUD and the controls),
> [`sail-controls.md`](sail-controls.md) (the sloops: D6), and the brief to Claude Design,
> [`../art/briefs/helms-pass.md`](../art/briefs/helms-pass.md) (D4).
>
> **Pillars served:**
> - **P1:** the sea you're about to hit stays in view.
> - **P2:** every rung of the fleet gets its own helm.
> - **P3:** a working coast has working switches.
> - **P4:** a switch is earned, then trusted.
> - **P5:** a light you forgot can matter.

The helm is the card at the bottom of the screen while you pilot. It is one of these:
- a dash: the Cape Islander, the Novi lobster boat, the console skiff or the sport skiff;
- a tiller;
- nothing: the dory and the sloops.

The owner asked for a review of three things: what the cards hide, how well they read, and which of their drawn
switches should do something. The audit found four problems:
- **The card hides the sea you steer into.**
- **It can't be read at the size the game shows it.**
- **It draws switches that do nothing.**
- **Seven hulls borrow a helm that isn't theirs.**

---

## 0. The rulings (2026-09-25)

| # | The owner's ruling | What it means here |
|---|---|---|
| **D1** | *"O4, the strip (360x72 on dashes, 320x72 on consoles, whole-number scale, bottom centre), plus O5 (U or a tap of pad View cycles the size, holding View hides all, the grip mark, the first-time hint, 24 px buttons). The size is not remembered. O7 stays in reserve."* | §2. The small card becomes a purpose-drawn strip, and the window controls become findable. There is no save data and no camera change. |
| **D2** | *"P1 is the first build, as you proposed, with the L bug (spotlight plus every ice-box lid) fixed in it. Its Core interface needs lead-architect's approval. P2 and P3 come later. (a) Nav lights: ____. (b) The dory's searchlight: ____. Write this into the design doc; the build is its own charter later."* | §3. P1 wires what already exists and caps the decor. **(a) and (b) were left blank: they're open** (§3.4). |
| **D3** | *"your milestone order: wheelhouse for the dragger and trawlers (M3), ship's bridge (M4), RIB console with a twin option, flybridge, the heavier punt tiller, Sloop 88. The dory stays bare."* | §4. Six new helms in that order. The Sloop 30 gets nothing in this pass. |
| **D4** | *"revise, don't redesign. Finish the brief in the PR; I send it to Claude Design myself."* | §5. The brief is [`../art/briefs/helms-pass.md`](../art/briefs/helms-pass.md). The owner sends it. |
| **D5** | *"the docs-only draft PR."* | §6. Three new docs. The evidence stays out of the repo. |
| **D6** | *"helm trim first, sails drawn, auto-trim on by default; the mast second, in its own charter later. Log the wind-arrow and sloop-fuel bugs in the doc."* | §7 and [`sail-controls.md`](sail-controls.md), which logs both bugs (its §K). |

**Who builds what, once chartered:**

| Lane | Work |
|---|---|
| ui-ux | the strip host (§2.3), the P1 wiring on the UI side (§3.2), the C# port of the revised rigs, and the wind needle's fix ([`sail-controls.md`](sail-controls.md) §K1) |
| lead-architect | the Core switch seam (§3.2); the ADR 0043 ledger rows for M, U, View and the pad |
| art-director and Claude Design | the rigs, through the brief |
| gameplay-systems | the sail work ([`sail-controls.md`](sail-controls.md)) |

---

## 1. How to read the numbers (the frame key)

Every number names its frame.

- **Source**
  - *arith*: computed from the cited C# and Data at `3fa9b606`.
  - *px*: counted in an image the audit rendered. The dashes went through a Python port of the C# Point-filtered resample. The tiller and the lever went through their own rigs in node.
  - **No number here is a Unity capture.** Those wait for Phase C (§10).
- **Screens** (all desktop, safe-area inset 0)
  - 720p = 1280×720, 800p = 1280×800, 1080p = 1920×1080, 1440p = 2560×1440, UW = 3440×1440, 4K = 3840×2160.
- **Cards**
  - *cape* is `helm.cape_islander`, *novi* is `helm.novi`, *console* is `helm.console_skiff` and *sport* is `helm.sport_skiff`.
  - *tiller* is any engine hull with no `Helm` def.
  - *lever* is the lone lever card; no hull uses it.
- **Window states**
  - *small*: as shipped. Dashes ×0.5 and the tiller ×1 (`GameConfig.cs` :1028-1029).
  - *focused*: dashes ×1.5, clamped under the top HUD band; the tiller and lever ×2.
  - *Compact*: ×0.55. *floor*: the 0.35 `MinScale`. *Bar*: the 18 px title strip alone.
- **Measures**
  - *run*: the share of the sea below the boat that the card covers. That's the screen column from the boat's centre down to the bottom edge, with the boat at rest at screen centre.
  - *S*: the seconds of clear sea ahead of the bow before the card's edge. The boat heads south, toward the card, at her derived top speed in a calm sea.
  - *k*: the card's screen scale.
  - *c = s·k*: one glyph cell in screen px, where s is the rig's font size (1, 2 or 3).
  - *contrast*: the WCAG ratio, from the rig's own colours. Text needs 4.5 and non-text 3.

---

## 2. D1 · The view: a strip, and window controls you can find

### 2.1 What the audit found

- **The card hides the sea you steer into** (arith, cape, small, CapeIslander).

  | | 720p | 1080p |
  |---|---|---|
  | Sea below covered | 76% | 51% |
  | Warning at full ahead | 0.25 s | 1.13 s |

  Every species' visible shoal (1.2–4.0 m) fits wholly under it. So do her own wake and her anchor line.
- **The M1 card is the tiller** (arith, tiller, small, DoryOutboard). In M1 the player pilots only tillers.
  - It covers 68% of the sea below at 720p (0.86 s of warning) and 45% at 1080p (1.67 s).
  - The HUD's nav cluster draws over it at every resolution: the compass dial, its needle, the ribbon and set/drift.
- **Bigger hulls are hit harder.** The camera frames by the hull, so a bigger hull puts fewer pixels on each metre.
  - At 720p the small card overlaps the Skybridge's own stern by 212 px.
  - Steaming south at full ahead, the Skybridge and the tanker get 0.00 s of warning at 720p.
- **Its labels break at that size** (px, the glyph law in §2.4). Only 6 of the Cape's 35 labels stay intact when small, and none at Compact, the floor or Bar.
- **Focusing fixes the labels but not the sea.** Focused at 1080p, all 35 labels read (k 1.339), but the card fills 68% of the screen height.
- **The window controls are hard to find.**
  - They show only on mouse hover, and every target is under 24 px: the strip is 18 px, the buttons 22×18 and the grip 14×14 (`BoatUiWindowChrome.cs` :12-16, `GameConfig.cs` :1132-1134).
  - M (hide all) has no hint, no pad path and no row in the ADR 0043 ledger.
  - On a gamepad, only the throttle, neutral and steer do anything.

### 2.2 The options, measured (arith, cape, CapeIslander; labels intact out of 35)

| Option | 720p run · S | 1080p run · S | Labels intact | Ruling it touches |
|---|---|---|---|---|
| O0 as shipped, small (k 0.5) | 76.1% · 0.25 s | 50.7% · 1.13 s | 6 | — |
| O1b Compact | 41.9% · 0.81 s | 27.9% · 1.50 s | 0 | — |
| O1c Bar | 5.0% · 1.33 s | 3.3% · 1.85 s | 0 | — |
| O1d a corner | 0% | 0% | 6 | overrides the 08-03 bottom-centre ruling |
| O3 Bar while steering | 5.0% · 1.33 s | 3.3% · 1.85 s | 0 while steering | — (the pad has no hover) |
| **O4 strip, 72 tall (ruled)** | **20.0% · 2.09 s** | **13.3% · 2.36 s** | **all** | — |
| O4 strip, two rows (96 tall; not ruled) | 26.7% · 1.87 s | 17.8% · 2.21 s | all | — |
| O5 as an always-on chrome strip | 82.8% · 0.03 s | 55.2% · 0.98 s | 6 | — |
| O6 a saved layout | as O0 | as O0 | 6 | new save data |
| O7 the camera shift | in reserve (ruled) | — | — | — |

- At 4K the ruled strip is drawn ×2: 13.3% · 2.40 s.
- The strip overlaps nothing at any resolution: no toast, popup or quest note.

### 2.3 The ruling, as a spec for the build

**O4, the strip.** When the card isn't focused, the host draws a strip in place of the whole dash.
- **Sizes:** 360×72 rig px on the pilot dashes (Cape, Novi) and 320×72 on the skiff consoles (console, sport). Each new helm in §4 gets one too; the tillers don't.
- **Scale:** whole-number only, k = max(1, H div 1080). That's ×1 on every screen from 1280×720 to 3440×1440, and ×2 at 4K.
- **Place:** bottom centre, as ruled 2026-08-03 (`GameConfig.cs` :998, :1024).
- **One row, 72 tall.** If one row can't hold a strip, the designer says so. There is no two-row fallback without a ruling.
- **What it carries:**
  - the drive (the throttle detent and the gear, as a letter with a position cue);
  - the tach and fuel;
  - depth on the pilot strips, and a heading on the skiff strips (their dome compass hides the HUD compass, `UI/HelmHudSuppression.cs` :71-78);
  - the live switches as lit and unlit pips, never colour alone;
  - a night state.

  The art is the brief's §2.1.
- **The focus** shows the full dash, at a whole-number scale where one fits: ×1 at 1080p, which is 51% of the height with 35 of 35 labels intact.
- **The tiller** keeps its ×1 card.

**O5, window controls you can find.**
- **U**, or a tap of the pad's **View**, cycles the helm card's size. The exact cycle with the strip in place is settled in the build's charter; the lean is strip → focus → Bar.
- **Holding View hides all**, like M.
- **A grip mark** is drawn into the strip's art. The rig exports it as `GRIP`.
- **A one-line hint** shows on the first hover of each launch: "drag to move · U to resize · M to hide".
- **24 px targets** when the chrome shows: `TitleBarPx` 18 → 24, `ChromeButtonPx` 22 → 24, and `GripPx` 14 → 24 as a hit area (`GameConfig.cs` :1132-1134). These are Inspector values (rule 6).

**The size is not remembered.**
- No save data and no PlayerPrefs.
- The layout stays transient, as ruled at `GameConfig.cs` :1094-1096. It survives region and save loads within a launch, and resets at the next launch.

**O7 (moving the boat up the screen) stays in reserve.** With O4 there's nothing for it to fix: at 720p the strip's top is 88 px from the bottom.

**Who builds it:**

| Lane | Work |
|---|---|
| art-director and Claude Design | the strips and their hit geometry (the brief §2.1) |
| ui-ux | the small-state renderer, U and View, the hint and the 24 px chrome |
| lead-architect | the ADR 0043 rows for U, View and M |

There's no Core seam.

**Open (not blocking): the 720p focus.**
- At 720p the full dash can't be both whole-number and under the top band, so the focus keeps ×0.883, and 6 of 35 labels.
- A ×1 focus would cover the band's bottom 64 px, and the band's deepest label ends at 196 of its 220 px (`UI/HudBandLayout.cs` :43-45).
- The brief's s2 floor holds at either scale.

### 2.4 Legibility: the glyph law, and what the revision must meet

**The glyph law** (arith). Every letter on the cards is one 3×5 pixel font, drawn at a whole-number scale s. On screen one font cell is c = s·k.
- A glyph keeps every stroke at every sub-pixel phase only when c ≥ 1.
- Below 0.8, no phase keeps all five rows. Below 0.667, no phase keeps all three columns.

**Labels intact at every phase** (px):

| Card | Labels | small | Compact, floor | focused 1080p | focused 720p |
|---|---|---|---|---|---|
| cape | 35 | 6 | 0 | 35 (k 1.339) | 6 (k 0.883) |
| novi | 35 | 3 | 0 | 35 | 3 |
| console | 19 | 4 | 0 | 19 (k 1.439) | 4 (k 0.949) |
| sport | 19 | 4 | 0 | 19 | 4 |

- **Only s2 lettering survives when small.** Every switch label, gear letter and tach numeral is s1.
- **Contrast** (arith; text needs 4.5):
  - Every switch label on every dash fails, ANCH included: Cape 3.01, Novi 2.55, console 2.83, sport 3.10. Only the lit skiff ANCH passes (8.46).
  - The gear letters are effectively invisible: the Cape's F is 1.13, and the sport's F, N and R are 1.03–1.27.
  - At night, contrast *falls* on the sport's readouts (to 2.2–4.1). The switch banks are never lit.
- **Colour alone:** several states differ only by colour:

  | Control | Contrast between its states |
  |---|---|
  | The Novi's rocker paddle | 1.03 |
  | The tiller's shift halves | 1.91 |
  | The console's R | 1.51 |
  | The key's RUN angle | 1.13 (Cape), 1.07 (Novi) |

  The Cape's tilting rocker, which has a shape cue, is the model to copy.
- **Hit targets** (focused at 720p): the pilot ANCH is 49.5×15.9 px and the skiff ANCH 20.9×39.9 px. Both fail 24 px.

**What the revision must meet** (the brief's §1, the legibility floor):
1. Text at s2 or larger, and live strip readouts at s3. At ×1, s2 is 10 px and s3 is 15 px, which is 16.6′ and 25.0′ of arc on a 14" 720p laptop at 50 cm.
2. 4.5:1 for text and 3:1 between a control's states, by day and by night.
3. Never colour alone.
4. Targets of 24 screen px or more at the smallest scale the control is used at: 28 rig px on the Cape and Novi, 26 on the console and sport, 24 on the tiller and the strips.
5. Live looks live. A switch that is fitted but not wired yet is drawn as a blank `cap`.
6. The banks light at night.

---

## 3. D2 · The switches

### 3.1 What the audit found (arith, from the code)

- **Cape and Novi.** 11 of the 12 bank rockers do nothing; only ANCH works. The port pins the key to "running" and the fuel gauge to full (`HelmDashController.cs` :152-153).
- **Console and sport.** The DECK and SPOT bats are hard-coded off (`ConsoleDashRender.cs` :214-219; `SportDashRender.cs` :147-150).
- **Tiller.** The button, the shift rocker and the kill cord do nothing, and the telltales are always lit. Its two hit boxes are declared and read nowhere (`TillerRigRender.cs` :26-27).
- **Lever.** The trigger is decor.
- **The rigs already draw most of the missing states**: the fuel gauge and its blink, the DECK and SPOT washes, and the key's OFF and RUN. It's the C# port that pins them. Wiring them is ui-ux work, not new drawing.
- **One route, one user.** A drawn switch reaches a verb through `IHelmControl` (`Core/Boats/HelmControl.cs`) → `HelmControlRelay`, and only the anchor uses it.
  - The spotlight and the lamps live in the Art assembly, which neither UI nor Boats can reference.
  - So wiring a light switch needs **one Core interface** (rule 4).
- **The L bug** (§3.3).
- **Input gaps.** M (hide all) is missing from the ADR 0043 ledger, and the ledger's Gamepad scheme has no bindings.

### 3.2 The ruling: P1 is the first build

**P1 wires what already exists.** It needs no new system, no new save data and no new key.

| # | Switch | It drives | Keys (keyboard · pad) | Rule 5 |
|---|---|---|---|---|
| 1 | **SPOT** on all four dashes | the spotlight (`BoatSpotlight.SetBeam` / `ToggleBeam`), through the Core seam | L · Y | transient, seeded off |
| 2 | **The fuel gauge** on all four dashes, and **the tiller's warn triangle** at 0.13 | the fuel burn (`IFuelVessel`), through a new read-only `IHelmControl.Fuel01` | — | fuel is already saved (`HullFuel`, v14) |
| 3 | **NAV** on the Cape and Novi | the lamps (`BoatLamps.LampsOn`), through the Core seam | click · the pad's switch cursor | transient, default on; the lighting regime stays recomputed |
| 4 | **INST** on the Cape and Novi | the fitted instruments' Night prefs | click (K stays the instrument cycle) | a saved pref (v14) |
| 5 | **The tiller's shift rocker**, with a drawn N | the throttle's step verbs (`HelmControl.cs` :151-157) | W/S and Z, already bound | input |

- **ANCH stays as it is.** It's already live on Q (the pad's X is proposed); not saved.
- **The L bug is fixed in P1** (§3.3).

**The Core seam needs lead-architect's approval.** The audit's sketch for the co-sign:
- `IHullSwitch { HullSwitchKind Kind; bool On; void Set(bool on); }`, one per owning Art component;
- `IHelmControl` grows `HasSwitch`, `SwitchOn` and `ToggleSwitch(kind)`, mirroring the anchor's three members;
- plus the read-only `IHelmControl.Fuel01`.

The precedent is `IVesselWay` (`Core/Boats/IVesselWay.cs` :18), which `BoatLamps` already reads off the hull root (`Art/BoatLamps.cs` :202-211). The shape is lead-architect's to settle.

**P1 also stops drawing decor that looks live.** The game passes the rigs' `cap` for these:
- **Capped** (no gameplay planned): WIPE, ACC (it folds into the key) and the Cape's PUMP (a washdown).
- **Capped until their verbs land:** BILGE, HORN, VHF, DECK and CABIN on the Cape and Novi, and the Novi's PUMP (the lobster-tank circulation).
- **The skiffs' DECK bats:** these hulls carry no deck lamp, so the bat shows its cap unless one is authored.
- **These stay as drawn until the engine run state lands (P2):** the ignition keys, the tiller's START/STOP button and its telltales. Until then "running = manned" is true of the fuel burn.

The rigs need new art for the caps and the new states (the brief §2.2). The P1 wiring can land before the revised rigs; it wires the states the rigs already draw.

**P2 and P3 come later**, each with its own charter:

| | Items, in order |
|---|---|
| **P2** | 1. the engine run state (M2-04), with the pull-cord and choke on the small outboards; 2. the anchor light; 3. HORN (M2-07); 4. the CABIN and DECK switches; 5. the dory's lantern aboard, her hand horn and her hand compass; 6. the INST override of the automatic panel light; 7. radio music (audio) |
| **P3** | BILGE (M2-03), VHF (M2-09), the Novi's tank PUMP, trim and tilt, the windlass, the diesels' glow plugs, the battery, and the autopilot (M3; rule 8) |

**Keys proposed for P2** (keyboard · pad; none bound yet):

| Switch | Keyboard | Pad |
|---|---|---|
| HORN | B: hold for a long blast, tap for a short one | L3, held |
| Engine (the key or the pull-cord) | C: tap to run or stop, hold to crank | R3, held |

- On the pad, the d-pad ←/→ moves a switch cursor on the focused card, and A flips the switch.
- LT and RT are reserved for the sloops' sheets ([`sail-controls.md`](sail-controls.md) §7).
- Every pad button is a proposal until ADR 0043 PR 2 binds the Gamepad scheme.

### 3.3 The L bug (fixed in P1)

- **Today, at the helm, L toggles the spotlight and also the lid of every ice box in play.**
  - The ice box's dev keys default on, so every ice box in play answers its lid key, L (`ShipHold.cs` :32-33 adds it; `Boats/DeckIceBox.cs` :43/:45, `Update` :76/:86).
  - Separately, the spotlight still reads the raw L key (`BoatSpotlight.cs` :536-542), though ADR 0043 :117 says it moved to the ledger (PR 1).
- **What P1's fix must give:**
  - At the helm, L (the pad's Y) toggles the spotlight and nothing else. No ice-box lid answers it.
  - The spotlight reads its ledger action, not the raw key.
  - The build's charter picks how the lids stop answering: their dev keys default off, or they ignore input at the helm.
  - On foot, L stays the headlamp (`WalkerLights.cs` :66, :176-179).
  - A guard test pins it: at the helm, one press of L changes the beam and no lid.

### 3.4 Open: two questions the ruling left blank

**(a) Nav lights with teeth.**
- Today the lighting regime lights the lamps automatically, so a NAV switch that defaults on changes nothing for the player.
- The P5 version: on the player's own hull the lamps obey the switch alone, and running dark costs something, such as NPC traffic not seeing you (M3-06), or a word from the harbour master.
- That's a ruling, not wiring.
- **Until it's ruled, P1 builds the plain switch:** default on, and while on, the lamps follow the regime.
- It changes no art: NAV is drawn with its states either way.

**(b) The dory's searchlight.**
- By the code, L lights a beam on the rowing dory today (`BoatSpotlight.cs` :386-398; Phase C confirms it).
- The owner's earlier "the rowboat should not have one currently" was answered by defaulting the beam off, not by removing it. Keep it, or take it off?
- **Until it's ruled, P1 doesn't touch it:** L still lights her beam, default off.
- The dory has no card, so this changes no art either.

### 3.5 Rule 5 (what's saved)

| State | Saved? |
|---|---|
| Fuel | saved (`HullFuel`, v14) |
| INST (its route (a)) | a saved pref (v14) |
| SPOT, NAV, DECK, CABIN | transient: SPOT is seeded off, NAV defaults on |
| The anchor | not saved |
| The engine run state (P2) | transient, off on load |

### 3.6 The full switch table (the build charters' reference)

"Exists" means the verb the switch would drive exists today.

| Switch | It should drive | Exists? | Priority | Keys | Rule 5 |
|---|---|---|---|---|---|
| SPOT (four dashes) | `BoatSpotlight.SetBeam` / `ToggleBeam`, through the Core seam | yes | P1 | L · Y | transient, seeded off |
| Fuel gauge (four dashes); tiller warn at 0.13 | `IFuelVessel`, through `IHelmControl.Fuel01` | yes: it burns and is saved | P1 | — | saved |
| NAV (Cape, Novi) | `BoatLamps.LampsOn`, through the Core seam | yes | P1 | click · pad cursor | transient, default on |
| INST (Cape, Novi) | the instruments' Night prefs; later a `HelmPanelLight` override | yes | P1 (the prefs) · P2 (the override) | click | a saved pref; the override transient |
| Tiller shift | the throttle step verbs, with a drawn N | yes | P1 | W/S and Z | input |
| Lever trigger | the neutral interlock | yes | once a hull uses the lever card | rides W/S | input |
| ANCH | the anchor | already live | — | Q · X | not saved |
| ENGINE: the key (dashes), START/STOP (tiller) | a new Core engine run state (Off / Acc / Run, Start momentary) feeding the fuel burn | no (M2-04) | P2 | C · R3 held | transient, off on load |
| Pull-cord and choke (DoryOutboard, Punt, PuntUpgraded) | ENGINE's start gesture | no | P2 | C held · R3 held | transient |
| Anchor light | `IVesselWay` Moored while the anchor is down | partly | P2 | rides Q | recomputed |
| HORN | the sound and a `HornSounded` event | no (M2-07) | P2 | B · L3 held | momentary |
| CABIN | `CabinGlow`, a per-kind lamp mask | yes | P2 | click | transient |
| DECK | a new deck-flood lamp kind (the enum stops at `RangeLight = 7`) | no | P2 | click · pad cursor | transient |
| Radio | music (audio) | no | P2 (audio) | click | a pref, later |
| Glow plugs (the diesels) | a heat beat before the crank | no | P3 | rides C | transient |
| BILGE | M2-03 | no | P3 | click | water saved per hull (new) |
| VHF | M2-09 | no | P3 | click | power transient |
| PUMP (Novi) | the lobster tank | no | P3 | click | transient |
| Windlass and rode | `BoatAnchor.RodeMeters` (:96) | the data yes, the verb no | P3 | Q held · X held | not saved |
| Trim and tilt | outboard trim and tilt-up | no | P3 | click | transient |
| Battery | nothing: there's no electrical model | no | P3 | — | — |
| Autopilot | M3-02 | no | P3, not now (rule 8) | — | transient |
| WIPE, ACC, the Cape's PUMP | — | — | capped | — | — |
| Kill cord (tiller) | why she stops when you let go | — | honest decor | — | — |

**The dory, by hand.** She has no card, only things held:

| Item | Today | It should | Priority |
|---|---|---|---|
| Lantern | lit on foot, dark aboard (`WalkerLights.cs` :186) | stay lit aboard a hull with no lamps | P2 |
| Searchlight | L lights a beam | D2 (b), open | — |
| Hand horn or bell | none | use the HORN event | P2 |
| Bailer | none | come with M2-03 | P3 |
| Hand compass | none | be a held item | P2 |

---

## 4. D3 · New helms: in milestone order

### 4.1 The census (arith; 39 hulls under `Data/Boats/`, plus `StPetersAmbientFleet`, which isn't one)

| Card | Hulls | Fits | Stand-in or near-fit |
|---|---|---|---|
| novi | 19 | LobsterBoat and 18 variants | none: "Open" names only the aft roof, and all 19 keep the wheelhouse |
| tiller | 8 | DoryOutboard; Punt and PuntUpgraded roughly | stand-in on SideDragger, SternTrawler, SternTrawlerMk2 (M3), CoastalPacket and Tanker (M4) |
| cape | 3 | CapeIslander | stand-in on SportFisherConvertible and SportFisherSkybridge |
| console | 3 | ConsoleSkiff | near-fit on ZodiacFrc and ZodiacHurricane (the Hurricane has twin outboards) |
| sport | 3 | SportSkiff, SportSkiffMk2, SportSkiffTwin | none |
| none | 3 | Dory (oars); Sloop30, Sloop88 (sail) | — |
| lever | 0 | — | no hull uses it (`HelmOverlayHost.cs` :268-290 carries it) |

M1 pilots only tiller cards (DoryOutboard, Punt, PuntUpgraded). No dash is seen in M1 play except from the dev picker.

### 4.2 The ruling: the order

| # | New helm | For | Milestone |
|---|---|---|---|
| 1 | Wheelhouse | SideDragger, SternTrawler, SternTrawlerMk2 | M3 |
| 2 | Ship's bridge | CoastalPacket, Tanker | M4 |
| 3 | RIB console, with a twin-engine option | ZodiacFrc, ZodiacHurricane | none yet |
| 4 | Flybridge / tower helm | SportFisherConvertible, SportFisherSkybridge | none yet |
| 5 | The heavier punt tiller | Punt, PuntUpgraded | M1 (a refinement of a helm that fits) |
| 6 | The Sloop 88's helm | Sloop88 | with the sail work ([`sail-controls.md`](sail-controls.md)) |

- **The dory stays bare.** She gets held things, not a card (§3.6).
- **The Sloop 30 gets nothing in this pass.** Her small engine panel waits on the engine run state (P2).
- **No lobster "open helm".** The Novi fits all 19.
- Each new helm is a full card plus a strip (§2.3), except the punt tiller, which keeps the tiller's ×1 card.
- The file names, globals and canvases are pinned in the brief (§2.3, §3.9), so no two jobs edit the same file. The punt tiller is a new rig beside the tiller, not an edit of it.

---

## 5. D4 · The brief

- The brief is [`../art/briefs/helms-pass.md`](../art/briefs/helms-pass.md). It's finished in the same PR as this doc, and the owner sends it.
- **A revision, not a redesign.** Each rig keeps its file name, global, exported names, canvas line and anchors. The sport fisher v3 came back as a redesign and couldn't be taken in.
- **It travels as eight zips, one per job** (at most 500 entries each). Each carries the brief, this doc, its rigs, a README and a node checker:
  1. the strips;
  2. the revision of the five helms;
  3. to 8. the six new helms, in D3's order.
- **The checker** loads each rig in a `vm` with a recording 2D-context shim. It checks three things, the first two by value over the drawing trace:
  - the contract: what a revision must keep;
  - the job's targets: hit shapes, sizes, states and params;
  - the scope: which files the job may change.

  It needs only `fs`, `path` and `vm`, so it runs in Claude Design's emulated Node 18. We verify returns with the same checker on real node.
- **After Claude Design:** an intake charter (the art lane) lands each job as its own PR. The C# port is a ui-ux PR after that.

---

## 6. D5 · This PR, and where the evidence is

- **This is a docs-only draft PR** with three new files: this doc, [`sail-controls.md`](sail-controls.md) and the brief.
  - It touches no code, Data or scene, so it moves no test.
- **The audit's evidence is not in the repo.** That's the Phase A report, the tables (`a1-*` … `a6-*`), the mock-ups, the resample sheets and the scripts that made them. It stays with the audit.
  - The owner's package carries what each job needs.
  - The tables and images named in the brief ship in those zips.
- **No Unity-family process ran for the audit or this PR.** Nothing was written in the owner's project folder.

---

## 7. D6 · The sloops

**The ruling:**
- helm trim first, with the sails drawn and auto-trim on by default;
- the mast (the stations) second, in its own charter later;
- the wind-arrow and sloop-fuel bugs logged.

The design of record is [`sail-controls.md`](sail-controls.md):
- **§R** holds the ruling and what it leaves as standing proposals;
- **§K** holds the two bugs;
- the scope follows.

The Sloop 88's helm card is sixth in D3's order (§4.2).

---

## 8. What else the audit measured (the full tables are in the evidence)

- **The card's rect** (arith; cape and novi 600×548 rig px, console and sport 600×510, tiller 120×244):

  | Card | small at 720p | small at 1080p | focused |
  |---|---|---|---|
  | cape, novi | 300×274 px (38% of the height) | the same (25%) | 530×484 at 720p (k 0.883); 804×734 at 1080p (k 1.339) |
  | console, sport | 300×255 px (35%) | the same (24%) | 569×484 at 720p (k 0.949); 864×734 at 1080p (k 1.439) |
  | tiller | 120×244 px (34%) | the same (23%) | ×2, 240×488 |

  The tiller's ×2 focus reaches 4 px into the HUD band at 720p. The lever's reaches 204 px into it.
- **What draws over the card.** Every overlay draws on top.
  - The small cape and novi cards: toast lines 1–2 at 720p and 800p, and nothing from 1080p up.
  - The tiller: the nav cluster at every resolution.
  - Collapsing the tiller doesn't clear it.
- **What reads under the card** (§2.1):
  - the fish schools the finder and the bite use (`Fishing/FishSchoolPresenter.cs`); in M1 the eye is the only fish finder, because the tillers carry no instruments;
  - the rocks (she grounds when her draught exceeds the seabed plus the tide, `Boats/BoatController.cs` :581-591);
  - her wake (13–22 m astern at the Cape's 8.2 kn);
  - her anchor line (`Boats/BoatAnchor.cs` :316-323).
- **Input paths** (arith):
  - Only the throttle, neutral and steer have a pad path.
  - The anchor, the spotlight, focus, the brow, every window verb and rowing have none.
  - There's no mouse steer on the tiller card.

---

## 9. Corrections the audit found

None of these stopped the audit. Each thing still exists a few lines from where it was cited, or the comment is stale.

| # | Where | What it says | What's true |
|---|---|---|---|
| C1 | the audit's charter | the Open lobster variants are stand-ins | They fit: "Open" is the aft roof, and all 19 keep the wheelhouse. |
| C2 | the audit's charter | the Zodiacs are stand-ins | A near-fit. A RIB is steered at a centre console; only the twin engine is missing. |
| C3 | the audit's charter | the 600×548 canvas is at `HelmDashGeometry.cs` :18 | It's at :87 (`PilotW`, `PilotH`). |
| C4 | the audit's charter; [`diegetic-instruments-and-consoles.md`](diegetic-instruments-and-consoles.md) §5 :225-227 | four bottom-centre hint overlays | Only DevToast is left (`DevToast.cs` :39, :155-160). |
| C5 | `HelmHudSuppression.cs` :46-52 | the tiller card is "corner-parked" | It's bottom-centre, under the nav cluster (:71-78). |
| C6 | `HelmDashGeometry.cs` :294-295 | the wheel hub's doc | It disagrees with the code (:299-306). |
| C7 | `BoatUiWindowInput.cs` :13-18, `DevBoatInput.cs` :47-50 | their key sweeps | They're dated: both predate the anchor's move to Q. |
| C8 | ADR 0043 | the ledger holds every declared key | M (hide all) is missing. |
| A4-1 | `WalkerLights.cs` :24 | "the dev key ledger is exhausted" | B, C, J, P, U and the digits are free. |
| A4-2 | `HelmDashController.cs` :153 | "no fuel sim yet" | Stale: fuel burns (`BoatFuelTank`) and is saved in v14. |
| A4-3 | the audit's charter | the steaming light is at `HullLamp.cs` :19 | `Masthead = 3` is at :32 in `Core/Boats/HullLamp.cs`. |
| A4-4 | ADR 0043 :117 | the searchlight moved to the ledger (PR 1) | The code still reads the raw key (`BoatSpotlight.cs` :536-542). P1 fixes it (§3.3). |
| A5-1 | `GameConfig.cs` :1098-1101 | "no font law is in play at any size" | The resample is where the font law bites: 6 of 35 Cape labels survive at ×0.5. |
| A5-2 | the audit's charter | a saved layout needs a version bump | PlayerPrefs (`GameSettings`, :5-19) needs none. D1 ruled no saved layout either way. |
| B1 | the dead dash states | — | The rigs already draw them; the port pins them (fuel full, `running = true`). |

The sail corrections (A6-1 to A6-8) are in [`sail-controls.md`](sail-controls.md) §12.

---

## 10. Phase C: what only Unity can answer

These run on the owner's slot. None of them is guessed above.

1. **The view:**
   - the pixel phase the small card actually lands on;
   - captures at 720p, 1080p and 1440p, small and focused, by day and by night;
   - the flush brow;
   - the speed pull-back and camera shake under the card;
   - the wind readout's clipped rect (`HudController.cs` :1000-1001);
   - that a reload keeps the layout within a launch and resets it on relaunch;
   - the strip's pixels at each screen, once it's drawn.
2. **The switches:**
   - which lamps draw when she's anchored at night;
   - whether L lights a beam on the dory (D2 (b));
   - `BoatLamps` after a re-skin;
   - whether leaving the helm parks her.
3. **Play:**
   - whether an M1 player fishes from the helm with the card up;
   - how the anchor line really draws under the card.
4. **The sloops:** [`sail-controls.md`](sail-controls.md) §12.

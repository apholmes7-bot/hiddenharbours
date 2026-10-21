# ART BRIEF — the helms pass: helms you can read and trust, a band along the foot of the screen, and the helms the big hulls lack

**To:** Claude Design, through the art-director lane (`agents/art-director.md`; the helm rigs live under `docs/art/rigs/ui/`)
**From:** ui-ux, on the owner's 2026-09-25 rulings on the helm audit
**Status:** READY. The owner sends it, one job at a time (§4). **D4 ruled: revise, don't redesign** (§3.2).
**Design of record:** [`../../design/helm-audit.md`](../../design/helm-audit.md) (the audit and the rulings D1–D6) ·
[`../../design/sail-controls.md`](../../design/sail-controls.md) (the sloops, for job 8). Canon:
`docs/art/rigs/ui/README.md` (the UI rig conventions), ADR 0025 (the rigs are ported to live C#), ADR 0030 (per-hull
instruments), ADR 0031 (no keyline), ADR 0043 (the input table). The audit's tables and images named below ship in
each job's `evidence/` folder.

> **The order: the lighting pass on each hull comes first.** In it, Claude Design adds spotlights, deck lights,
> navigation lights, underwater lights and cabin lights; when a cabin's lights are on, its windows glow. The helms
> are reconfigured after it, to reflect each hull's latest design and features (the owner, 2026-09-25;
> `helm-audit.md` §3.4).

The helm is the card at the bottom of the screen while you pilot. It is a dash (Cape Islander, Novi lobster boat,
console skiff, sport skiff), a tiller, or nothing (the dory, the sloops). The owner asked for a review of what the
cards hide, how well they read, and which of their drawn switches should do something. The audit found three
things your art can fix:

- **They can't be read at the size the game shows them** (§1).
- **They draw switches that do nothing.** On the Cape and Novi, 11 of the 12 bank rockers are dead. The console and
  sport dashes draw DECK and SPOT bats that are hard-coded off.
- **Seven hulls borrow a helm that isn't theirs, and two more make do.** A small outboard's tiller steers five
  big hulls, up to the 110 m tanker; the Cape's pilothouse dash steers two sport fishers; and the two Zodiac RIBs
  use the console skiff's dash, which draws one engine's gauges (the Hurricane has two).

This pass adds a band for each dash, revises the five helm rigs you already made, and draws six new helms.
It travels as **eight jobs, one zip each** (§4). **Do them in the order in §2, one job per return.**

**A revision is not a redesign** (§3.2). Keep each rig's file name, global, exported names, canvas line and anchors.
The sport fisher v3 came back as a redesign: a new file name, a new global, a new sidecar format and an anchor line
that no longer matched. It could not be taken in.

---

## 1. ⭐ The one requirement that is gameplay, not decoration

**A helm is read and used at the size the game shows it. Today it can't be.**

Today the card sits at bottom-centre and shows **small** until you click it: ×0.5 for a dash, ×1 for the tiller.
Then it shows **focused** (the record: today's card), as large as fits under the HUD band: the Cape and Novi at ×1.339 at 1080p and ×0.883 at
720p; the console and sport at ×1.439 and ×0.949; the tiller at ×2. The texture is resampled nearest-neighbour to
that size (`DrawSurface.cs` :311).

**What the rulings change:** the dash becomes a **band** along the foot of the screen, at ×1, or ×2 at 4K (§2.1), so
the floor below holds everywhere. D1's strip and focused dash (2026-09-25) are the record: the band replaced them.

| What | Today | Frame |
|---|---|---|
| Labels intact at every sub-pixel position | Cape **6 of 35** · Novi **3 of 35** · console and sport **4 of 19** | small, ×0.5; the same at every resolution |
| … when focused at 720p (the record: today's card) | the same: 6 · 3 · 4 | ×0.883 (pilot), ×0.949 (skiff) |
| … when focused at 1080p (the record: today's card) | all of them | ×1.339, ×1.439 |
| Switch labels at 4.5:1 or better | **none**, by day or by night: the Cape bank 3.01, the Novi bank 2.55, console DECK/SPOT 2.83, sport 3.10. Only the lit skiff ANCH (8.46) passes | the C# proofs |
| Other text under 4.5:1 | the key's OFF/RUN (Cape 2.20/2.49, Novi 2.56/2.75; console and sport OFF 2.83/3.10); the Cape's RPM and FUEL 3.90; the Novi's HARBOUR MARINE 2.88 and NOVI 4.02; the Cape's X100 2.99, 95% hidden under the needle | by day |
| Gear letters | the Cape's F 1.13 and N 1.36; the sport's F/N/R 1.03–1.27 | by day |
| Readouts at night | contrast **falls** on three dashes: the sport's tach, RPM, E, F and FUEL to 2.2–4.1; the Cape's red zone, RPM, E and FUEL to 3.34–3.95; the console's red zone and E to 3.48–3.93 | the night proofs, `*-dash-night-csharp.png` |
| The switch banks at night | never lit. Only the gauges brighten: Cape ×1.19, Novi ×1.32, console ×1.60, sport ×1.01 | mean luminance, night ÷ day, by panel region |
| The one live switch, ANCH, on screen | pilot rocker 56×18 rig px → **49.5×15.9 px**; skiff bat 22×42 → **20.9×39.9 px** | focused at 720p (the record: today's card); a 24 px target fails |
| Unfitted plates | BLANKING PLATE 1.80 (Cape); NO DISPLAY FITTED 1.37 and the RADAR/GPS plates 1.51 (Novi) | by day |

The window's own controls (22×18, 14×14 and 18 px tall) fail 24 px too. They're host code, not your art, and the
host fixes them (D1).

**Why the text breaks: the glyph law.** Every letter on these cards is one 3×5 pixel font, drawn at a whole-number
scale s. On screen, one font cell is c = s × k pixels, where k is the card's scale.
- A glyph keeps every stroke at every sub-pixel position only when c ≥ 1.
- Below c 0.8, no position keeps all five rows. Below 0.667, no position keeps all three columns.
- So an s1 label is safe only at k ≥ 1, and an s2 label is safe down to k 0.5.

Today every switch label, the gear letters and the tach numerals are s1. Only RPM, E, F, FUEL and the RADAR/GPS
plates are s2. That's why they're the only ones that survive.

**So, for every piece in §2 (the legibility floor):**

1. **Text a player must read is s2 or larger; live readouts on the band are s3.** At s2 a label survives ×0.5
   (c 1.0) and every focused scale (c ≥ 1.77 at 720p; the record: today's card). On the band at ×1, s2 is 10 px tall and s3 is 15 px:
   16.6′ and 25.0′ of arc on a 14" 720p laptop at 50 cm, 15.9′ and 23.8′ on a 24" 1080p at 60 cm. The minimum for
   text is 16′ (ANSI/HFES 100-2007). s3 clears it on every screen we measured (17.1′ at the smallest); s2 clears it
   only on the laptop (11.4′–15.9′ on the rest). So s2 is the floor for labels, and anything read under way is s3.
2. **Contrast ≥ 4.5:1 for text and ≥ 3:1 between a control's states, by day and by night** (WCAG 1.4.3 and 1.4.11,
   used as yardsticks). Night must raise contrast, never lower it.
3. **Never colour alone.** Every state also has a shape or position cue, not just light.
   - The Cape rocker is the model: it tilts, so its lit half moves from top to bottom. Its colour steps only 2.52;
     the tilt carries it.
   - Avoid the Novi rocker: its lens and label brighten (4.05, 6.57), but the paddle doesn't move.
   - The tiller's shift rocker: its lit half steps only 1.91, and only the brightening F/R letter (8.34) carries it.
   - The console's gear letters: R steps only 1.51 between lit and unlit.
4. **Targets ≥ 24 screen px at the smallest scale the control is used at.** The target is the hit shape you export
   (§3.4). It may be bigger than the drawn part, as long as it doesn't overlap its neighbour's. In rig px:

   | Card | Smallest scale | Floor |
   |---|---|---|
   | Cape, Novi, and any new helm 548 tall | ×0.883 (focused at 720p; the record: today's card) | **28** |
   | console, sport, and any new helm 510 tall | ×0.949 | **26** |
   | a new helm of another height H | 484 ÷ H | ceil(24·H ÷ 484) |
   | the tillers and every band | ×1 | **24** |

5. **Live controls look live, and decor looks like decor.** A switch that does nothing reads as a bug. A switch that
   is fitted but not wired yet is drawn **unfitted: a blank cap**, until its verb lands (§2.2).
6. **The banks light at night.** Every live control's legend is lit, not just the gauges.

> *"In-world UI is only ever a real object that carries it."* (`docs/art/rigs/ui/README.md`, the diegetic rule)
> None of this adds a HUD. The band is the same dash, laid along the foot of the screen, and a blank cap is a real part.

---

## 2. The pieces, in order

### 2.1 THE BANDS (job 1) — ruled (D1, 2026-09-28). They give the player the sea back.

On every helm with a wheel, the game draws the dash as one **band** fixed along the bottom edge of the screen, the
full width: the top of the wheel in the middle, and the instruments in one row. Nothing is dragged, resized,
collapsed or cycled, and the dash never grows on a click. M, or holding the pad's View, hides it all. A click on a
brow instrument raises that instrument's card above the band, one at a time. The host code that builds the band,
places its pieces and raises the cards is ui-ux's, not yours.

The owner ruled it on 2026-09-28 (`helm-audit.md` §0, D1): more width to show things, no resizing to get in the
player's way, and a helm that stays diegetic.
It replaced D1's strip of 2026-09-25, which stays in the audit as the record.

**The envelope:**
- **144 rig px tall, one row.** If one row can't hold a band, say so in its README. Don't draw a taller one: that
  needs a new ruling.
- **Drawn at a whole-number scale only:** ×1 on every screen from 1280×720 to 3440×1440, ×2 at 4K. That's why s2 is
  enough.
- **Any width from 1280 to 3440 rig px, from fixed pieces:** the two end caps, the wheel's top arc in the middle,
  the instrument housings, and the row's groups of controls and readouts. Between them runs a **plain panel** that
  the host repeats. A detail is never stretched: a wider screen gets more panel, and nothing else.
- **Everything the band carries fits in 1280 rig px.**
- **No grip and no title bar.** Nothing is dragged.
- **§1 holds at ×1:** s2 text, s3 live readouts, 4.5:1 and 3:1, never colour alone, 24 px targets, a night state,
  and the banks lit at night.

**What goes on it:** the dash's own controls and readouts, drawn to fit the band; nothing new (D4: revise, don't
redesign).
- **The wheel's top arc, in the middle.** It steers as the wheel does today. The hub may sit below the band's
  bottom edge.
- **The drive:** the throttle detent and the gear. The gear is a letter (F/N/R, s3) with a position cue.
- **The readouts a helmsman checks under way:** the tach and the fuel gauge on all four bands, with fuel's low blink.
  - The pilot bands (Cape, Novi) also carry **depth**, repeated from the fitted sounder, or a plate when none is
    fitted.
  - The console and sport bands also carry a **heading**. Their dome compass hides the HUD compass, so without it
    the player has no heading at all.
- **The live switches:** the dash's own rockers or bats, lit and unlit, each with its s2 label and a shape cue.
  These are P1's live set (§2.2): **ANCH, SPOT, NAV and INST** on the pilot bands, and **ANCH and SPOT** on the skiff
  bands. Job 2 adds the rest of each dash's controls (§2.2).
- **A housing for each brow instrument the dash can mount:** the pilot dashes' `SLOTS`, and the skiffs' fish
  finder. The fitted instrument's face sits flush in it, drawn as the dash draws it, by the instrument's own rig,
  and a click on it raises that instrument's card. Draw each housing with its unfitted plate. If a face can't fit
  the band's height at ×1, say so in the README: its housing then shows a plate with the instrument's name, and a
  click still raises its card. Never scale a face.
- **A night state.**
- Decor stays in the dash rig.

**Hit shapes** for every control and every housing: at least 24×24 rig px.

**How a band is built, so the host can build it at any width:**
- `render(opts)` draws the whole band at `opts.width` (1280 to 3440; default 1280), 144 tall, laid out as you mean
  the host to lay it out: the end caps at the ends, the wheel's arc centred, the other pieces in order on each side,
  and the panel in every gap. Say the rule in the README.
- **`PIECES`:** every fixed piece by name → `{ w, side }` (`side` is `'left'`, `'centre'` or `'right'`), in order
  from left to right. Every piece is the band's full 144 tall. `paintPiece(ctx, name, opts)` draws one piece at its
  own origin.
- **`PANEL`:** the plain panel's tile → `{ w }`, 144 tall, drawn by `paintPiece(ctx, 'panel', opts)`. It repeats
  without a seam.
- **`layout(width)`** → each piece's x at that width. `render` uses it, so the host and the preview agree.
- **Every hit shape is piece-local:** it names its piece (`{ piece, x, y, w, h }`, or a circle) and lies inside it.
  The host adds the piece's x.
- **A band also exports what the host drives a dash by** (§3.9's list, with the dash's meanings). It may re-export
  its dash rig's.

**Files (pinned):** each band lives in its dash's folder, beside the dash rig:
- `cape-islander-helm/Art/capeBandRig.js` → `CapeBandRig`;
- `novi-helm/Art/noviBandRig.js` → `NoviBandRig`;
- `console-helm/Art/consoleBandRig.js` → `ConsoleBandRig`;
- `sport-skiff-helm/Art/sportBandRig.js` → `SportBandRig`.

Each has its own preview (`<Dash name> Band.dc.html`, with a width slider from 1280 to 3440) and its own README
(`README-band.md`). A band loads after its dash rig and may call its exports, so a part is drawn once. Job 1 only
adds files: the dash's own files are job 2's.

**The new wheel helms are bands too** (jobs 3–6 and 8, §2.3), in the same envelope. The wheelhouse, the bridge and
the flybridge carry the pilot bands' readouts, the RIB console the skiff bands', and the 88 its own (§2.3.6). The
tillers get none; they keep their card at ×1.

**Serves:** P1 (the sea you're about to hit stays in view). The band is 144 tall, but while it shows, the camera
eases up by half of it.
So the clear sea below the boat is what D1's strip left (the record: `helm-audit.md` §2.2, as measured).
Heading south at full ahead, the warning goes from **0.25 s** under today's small Cape card to **2.09 s** at 720p,
and from **1.13 s** to **2.36 s** at 1080p. P5 (the teeth are the sea's, not the UI's).

### 2.2 REVISE THE FIVE HELMS YOU HAVE (job 2) — the Cape, Novi, console and sport dashes, and the tiller

Apply §1 to each: text s2 or larger, contrast, a shape cue on every state, targets, lit banks.

**Each dash's layout becomes its band's** (D4, reopened for the layout only on 2026-09-28: the dashes' parts are
revised as before, and their layout becomes the band). Revise the parts below in the dash rig. It keeps its
contract (§3.2) and its `SW` at §3.9's floors, and it stays the source of the parts, though the game no longer
shows it whole. Then lay the rest of the dash's controls into its band (§2.1), keeping what job 1 made: its files,
its global and its names. The live and fitted controls go on with their states and `cap`; the capped ones go on
as blank caps, or stay off. The band takes the dash's new params and hit keys from §3.9, at the band's floor of
24, and everything on it still fits in 1280.

**Sort every control into one of three kinds, and make each kind look like itself.** D2 ruled that **P1** is the
first build: it wires what already exists. P2 and P3 come later.

| Kind | What the game does with it | How you draw it |
|---|---|---|
| **Live** | works today, or in P1 | every state, lit and unlit, by day and by night; a hit shape |
| **Fitted, wired later** | a real switch whose verb lands in P2 or P3 | the same: every state, drawn now. Also a **`cap`** option (a blank cap), which the game shows until the verb lands |
| **Capped** | no gameplay planned | a blank cap, or not drawn at all |

A later build then costs no new art: the game stops passing `cap`. The per-switch detail, with the code lines that
drive each switch today, is `a4-switches.md` §1–§2 (in the package), and the ruled table is `helm-audit.md` §3.6.

**Some states are already in your rigs, and the port doesn't pass them yet.** Wiring them is ui-ux's work, not
yours. Keep them, and bring them up to §1:
- the fuel gauge and its low-fuel blink on all four dashes (`fuel`, `blink`; the port pins the fuel full);
- DECK and SPOT with their wash on the Cape and Novi (`capeRig.js` :427-429, `noviRig.js` :428-430);
- SPOT with the spot-can lens and the wash on the console and sport (`consoleRig.js` :355-356, :462;
  `sportRig.js` :322-323, :404);
- the key's OFF and RUN (`running`; the port always draws RUN);
- the tiller's START/STOP button (running or stopped), its telltales (lit while running) and its warn triangle,
  blinking with `warn` (`tillerRig.js` :91-100, :168-170).

**The Cape dash** (CapeIslander; a stand-in on the two sport fishers until the flybridge helm, §2.3.4):
- **Live today:** the wheel, the lever, the tach, ANCH (on Q) and the brow instrument mounts.
- **Live in P1:** SPOT, NAV, INST and the fuel gauge.
- **Fitted, wired later:** the key (P2), DECK and CABIN (P2), HORN (P2), VHF and BILGE (P3).
- **Capped:** WIPE; ACC (it folds into the key); PUMP (a washdown, with no gameplay).
- **New art:**
  - states and a `cap` for every rocker the rig draws fixed today: NAV, INST, CABIN and VHF, and ANCH (below);
  - **HORN, redrawn as a push button**, since a rocker is the wrong control for a horn;
  - **BILGE as a 3-position switch** (off / auto / on);
  - the key with **OFF / ACC / RUN / START** detents (the rig has OFF and RUN), and a glow-plug lamp beside it
    (the Cape is a diesel);
  - the gear letters (F 1.13, N 1.36) and the BLANKING PLATE (1.80), per §1.
- **Mirror back into the rig** what the C# port added. The port lights ANCH from an `anchorDown` flag
  (`CapeDashRender.cs` :99, :501), and it finds ANCH by a hard-coded row 3, column 0 (`HelmDashGeometry.cs` :116).
  Make ANCH a param with a hit shape, so the port finds every switch by its name.
- **The bank's space.** Today there are 12 rockers of 56×18 rig px on a 27 px row pitch (`ROCK`, `capeRig.js` :139).
  In the dash rig, a 28 rig px target needs a 28 px pitch; six rows at 28 is 168, inside the bank's 178. The other
  route is fewer rockers, dropping the capped ones. On the band, the bank's rows fit inside its 144, at a pitch of
  24 or more. Your call; say which in the README.

**The Novi dash** (all 19 lobster hulls; it fits every one, Open variants included):
- The same bank and the same plan as the Cape, with these differences.
  - **Its ANCH is "the windlass switch"** (`NoviDashRender.cs` :548-549). Keep the name and the meaning.
  - **Its PUMP is the lobster-tank circulation** (fitted, wired later: P3), not a washdown.
  - **DECK is the Novi's strongest case:** hauling pots at dusk.
  - The lobster boats are diesel too, so the glow-plug lamp stays.
- **The paddle must move** between on and off (§1, rule 3).
- **Fix the RADAR and GPS plates** (1.51) and NO DISPLAY FITTED (1.37).

**The console dash** (ConsoleSkiff; a near-fit on the two Zodiacs until the RIB console, §2.3.3) **and the sport dash**
(SportSkiff, Mk2, Twin):
- **Live today:** the wheel, the throttle, the dome compass, the tach, and the ANCH bat where one is fitted.
- **Live in P1:** SPOT (the lens and the wash are already in your rigs) and the fuel gauge.
- **Fitted, wired later:** the key (P2); a horn button (P2), a trim/tilt rocker (P3) and a kill cord, none of them
  drawn yet.
- **The DECK bat:** these hulls carry no deck lamp, so the game shows its **`cap`**. Draw the cap, and keep the bat's
  states for the day a lamp is authored on the hull (§5).
- **New art:**
  - **the ANCH bat, mirrored back.** The port derives it (`HelmDashGeometry.cs` :71-75): x 87, y 362, 22×42, its
    lamp at y 416, midway between DECK and SPOT. Put it in the rig, and in `SW` as `anch`;
  - bigger targets: the bats are 22 wide, and the target needs 26;
  - the key with OFF / ACC / RUN / START detents (the rigs have OFF and RUN);
  - the kill cord, the trim/tilt rocker and the horn button, each with its states and a `cap`;
  - **the sport's night face** raises contrast instead of lowering it (§1);
  - the gear letters: the sport's F/N/R (1.03–1.27) and the console's R (§1, rule 3).

**The tiller** (DoryOutboard, Punt, PuntUpgraded; a stand-in on the five big hulls until the wheelhouse and the
bridge, §2.3.1–2):
- **It stays a card** (no band, §2.1), fixed at bottom centre at ×1 (×2 at 4K).
- **Live today:** the twist grip, the throttle band and the arm swing.
- **Live in P1:** the shift, with neutral; the warn triangle at low fuel.
- **Fitted, wired later:** the START/STOP button and the telltales (P2, already drawn with their states), a
  pull-cord and choke (P2), and the trim rocker (P3).
- **New art:**
  - **the shift rocker gets N:** F / N / R, three positions, with a position cue. Today neutral draws as F
    (`tillerRig.js` :134, `forward = o.gear !== 'R'`). Its hit (`hit.shift`, 22×34) grows to at least 24 wide;
  - **the trim rocker** (:176-179) gets states and a `cap`. Today it has none;
  - a pull-cord and choke, with their states and a `cap`.
- **Keep:** the kill-cord clip (:155-156). It's honest decor.

**The lever** (composited into all four dashes): no change in this pass. Its red neutral-lock trigger is the one bit
of decor that looks like a control. It becomes the neutral interlock only once a hull uses the lone lever card, and
none does.

**Not in this pass (Rule 8, stay in phase):** the windlass rode counter, the radio set and the autopilot head. They
belong to later milestones.

**Serves:** P3 (the working coast has working switches) · P4 (a switch is earned, then trusted) · P5 (a light you
forgot can matter).

### 2.3 NEW HELMS (jobs 3–8) — ruled (D3), in this order

Each new helm with a wheel is a band (§2.1): one rig, which draws the band, with no full card beside it. The punt
tiller is a card. Each is its own standalone folder, with the file names and globals pinned below, so no two jobs
touch the same file. Each keeps the contract in §3; a band takes the band's canvas line (§3.3).

1. **The wheelhouse helm (M3) — job 3.** `docs/art/rigs/ui/wheelhouse-helm/`: `Art/wheelhouseRig.js` →
   `WheelhouseRig`, a band.
   For the SideDragger, SternTrawler and SternTrawlerMk2 (25–38 m, diesel, 320–750 L), which steer with the small
   outboard's tiller today.
   - The dragger's house is aft, a "glassy wheelhouse looking over the deck"
     (`boat-interiors-kit/hull-rigs/sideDraggerIsoRig.js` :8).
   - The stern trawler's house is forward, a glassy wheelhouse with aft windows that watch the trawl
     (`boat-cutaway-kit-5/hull-rigs/sternTrawlerIsoRig.js` :4-5).
   - Draw, in the band: the wheel's top arc; the engine controls (throttle and gear); the key; P1's switches (ANCH,
     SPOT, NAV, INST); the pilot bands' readouts (§2.1); housings for the sounder and plotter (the Cape's `SLOTS`
     convention); the trawl-winch controls, drawn with a `cap` until M3 gives them verbs.
2. **The ship's bridge (M4) — job 4.** `bridge-helm/`: `Art/bridgeRig.js` → `BridgeRig`, a band.
   For the CoastalPacket (60 m, diesel, 2000 L: a three-level house aft, topped by a glassy wheelhouse with full-beam
   bridge wings; `boat-cutaway-kit-5/hull-rigs/coastalPacketIsoRig.js` :4) and the Tanker (110 m, diesel, 9000 L: a
   raked-glass wheelhouse with full-beam bridge wings; `boat-cutaway-kit-2/hull-rigs/tankerIsoRig.js` :7).
   - Draw, in the band: the wheel's top arc, or a small wheel's arc and a joystick; an engine telegraph or
     combinator levers; the key; P1's switches; the pilot bands' readouts; the bridge-wing repeaters; housings for
     the radar and plotter.
3. **The RIB console, with a twin-engine option — job 5.** `rib-console-helm/`: `Art/ribConsoleRig.js` →
   `RibConsoleRig`, a band.
   For ZodiacFrc (6.66 m, one outboard, 55 L) and ZodiacHurricane (7.28 m, **twin outboards**, 75 L;
   `zodiacIsoRig.js` :3-5, :114). They're a near-fit on the console dash today, but it draws one engine's gauges.
   - Draw, in the band: the wheel's top arc and a jockey-seat console's controls. **`twin` draws the twin-engine
     option:** two tachs, and two throttles or a twin binnacle.
   - Both carry a dome compass and fish-finder support, so the band carries the skiff bands' readouts, heading
     included, and a housing for the finder. Draw ANCH, SPOT, the key, the trim/tilt rocker and the kill cord, as
     on the console dash.
4. **The flybridge / tower helm — job 6.** `flybridge-helm/`: `Art/flybridgeRig.js` → `FlybridgeRig`, a band.
   For SportFisherConvertible (16.2 m, an open flybridge under a pipe tower, 320 L) and SportFisherSkybridge
   (27.4 m, an enclosed skylounge with a tuna tower, 900 L; `boat-interiors-kit/hull-rigs/sportFisherIsoRig2.js`
   :4-5, and its `helmSeat` and `towerStation` anchors at :34). They're steered with the Cape's pilothouse dash
   today.
   - **`variant` picks the hull:** `'convertible'` or `'skybridge'`.
   - Keep the brow mounts, as housings in the band: both carry a sounder, plus fish-finder, radar and GPS support.
     Draw the wheel's top arc, the key, P1's switches and the pilot bands' readouts.
5. **The heavier punt tiller (M1) — job 7.** `punt-tiller/`: `Art/puntTillerRig.js` → `PuntTillerRig`. A new rig
   beside the tiller, not an edit of it. **No band:** the tillers keep their card, at ×1 (×2 at 4K).
   For Punt and PuntUpgraded (25 L; 650 and 825 engine power against the outboard dory's 640). It has the tiller's
   controls, sized for a larger outboard, including the revised shift (F / N / R), trim, pull-cord and choke.
   - **It keeps the tiller's contract:** 120×244, the pivot at (60, 214), `hit`, `angle`, and the tiller's IIFE and
     export form (§3.2).
6. **The Sloop 88's helm — job 8.** `sloop88-helm/`: `Art/sloop88HelmRig.js` → `Sloop88HelmRig`, a band.
   Today neither sloop has a card, and neither rig draws an engine panel, a throttle, a staysail switch, a windlass
   remote or nav lights ([`sail-controls.md`](../../design/sail-controls.md) §11).
   - **Her two wheels share one helm** (`sail-controls.md` §2), so the band shows one wheel's top arc.
   - **Lay the 88's two instrument pods along the band,** each with its compass (`sloop88IsoRig.js` :1303-1304). In
     the pods: the engine panel (the key or a start button, the glow-plug lamp, oil and temperature alarms, a tach),
     the throttle and shift (one lever, on the starboard pedestal), the wind instrument (apparent) and the log, the
     staysail furler switch, and the windlass remote.
   - **The band carries** the apparent wind, the two sheets' state (main and jib), auto-trim (on by default), the
     engine run state and fuel.
   - How she's sailed: `sail-controls.md` §R (the ruling), §7 (the keys) and §11 (the helm).
   - The owner's words for the sailboats: *"engaging and somewhat sim-like. Not too demanding of clicks but also
     more engaged than the powerboats."*

**Not drawn:**
- **A lobster "open helm".** Every lobster variant keeps its wheelhouse, and the Novi fits all 19.
- **The dory.** It stays bare: "A bare dory shows nothing" (the diegetic rule).
- **The Sloop 30.** Nothing in this pass. Her small engine panel waits on the engine run state (P2).

**Serves:** P2 (dory to dynasty: every rung gets its own helm) · P3.

---

## 3. Technical

### 3.1 The UI rig conventions (`docs/art/rigs/ui/README.md`), unchanged

- `render(opts) → HTMLCanvasElement`, **stateless**: the same opts always draw the same sprite. The game owns the state.
- No anti-aliasing. The KTC master palette. Procedural art only: no image assets, and **no text API**. Every letter
  is the rigs' 3×5 pixel font; no rig calls `fillText` today.
- **Night is a parameter, not a separate rig.**
- Preview state goes to `localStorage['hh.*']`, for the preview only, never inside `render`.
- Offline-safe.
- Each folder is standalone: a README, a `*.dc.html` preview, `support.js` and `Art/*.js`. A helm carries the shared
  instruments it composites, loaded before the helm rig.

### 3.2 On a revision: what stays

| Rig | File → global | Canvas line (keep it as text) | Anchors |
|---|---|---|---|
| Cape | `capeRig.js` → `CapeRig` | `const W = 600, TOPPAD = 54, H = 494 + TOPPAD;` (600×548) | the export line `root.CapeRig = {`; the lever composite point `DRIVE` (px 507, pivotY 456) |
| Novi | `noviRig.js` → `NoviRig` | the same as the Cape | `root.NoviRig = {` |
| Console | `consoleRig.js` → `ConsoleRig` | `const W = 600, TOPPAD = 40, H = 470 + TOPPAD;` (600×510) | `root.ConsoleRig = {` |
| Sport | `sportRig.js` → `SportRig` | the same as the console | `root.SportRig = {` |
| Tiller | `tillerRig.js` → `TillerRig` | `const W = 120, H = 244, PVX = 60, PVY = 214;` | `window.TillerRig = {`; the pivot (60, 214); the caller rotates about it |

- **Keep the IIFE shape and the export line as text.** The checker finds them by text.
  - The dashes (and every new dash and band): `(function (root) {` … `root.XRig = {` …
    `})(typeof globalThis!=='undefined'?globalThis:window);`
  - The tiller (and the punt tiller): `(function () {` … `window.XRig = {` … `})();`
- **Every exported name keeps its meaning.** Values may change; a bigger rocker changes `ROCK`. Names and meanings
  may not. Add new names beside the old ones. The exports today:
  - **Cape:** `W, H, TOPPAD, DEG, dir, maxSteer, wheelTurn, ANG, driveHandle, driveFromPoint, driveThrottle,
    driveGear, PANEL, CORKF, WHEEL, RPM, FUEL, NAME, SLOTS, SLOTW, SLOTY, SLOTH, BROWB, DEFAULT_LAYOUT, BANK, ROCK,
    IGN, BINN, DRIVE, COMPASS, SW, slotBox, paint, render, paintRadar, paintGps`.
  - **Novi:** the Cape's names, with `SHELL` and `DASH` in place of `PANEL` and `CORKF`.
  - **Console and sport:** `…, CONSOLE, WHEEL, RPM, FUEL, SWPANEL, SW, BINN, DRIVE, SPOTCAN, COMPASS, paint,
    render`. The console also exports `facePaint, PAINT, TRIM, COVE, schemes, schemeIds, defaultScheme`.
  - **Tiller:** `W, H, pivot, maxSteer, hit, paint, render, angle`.
- **Every `render(opts)` param keeps its meaning:**
  - the Cape and Novi: `running, drive, steer, fuel, rpm, deck, spot, night, blink, radar, gps, compass, heading,
    layout, finder, fish, phase`;
  - the console and sport: the same without `radar, gps, layout`; the console adds `scheme, paint`;
  - the tiller: `throttle, running, press, warn, gear, blink`.

  Add new params beside them (§3.9), and give each a default that draws today's picture.

### 3.3 The canvas and `TOPPAD`

`TOPPAD` is headroom above the dash for the brow instruments: 54 on the pilot dashes, "so the portrait sonar reaches
above the dash" (`capeRig.js` :16), and 40 on the skiff dashes (`consoleRig.js` :18). Hit geometry is **rig-local**:
"subtract `TOPPAD` from pointer Y" (`cape-islander-helm/README.md` :50, `console-helm/README.md` :46).

- **A new helm is 600 wide and uses one of the two dash canvas lines:** `const W = 600, TOPPAD = 54, H = 494 +
  TOPPAD;` (548 tall) or `const W = 600, TOPPAD = 40, H = 470 + TOPPAD;` (510 tall). The game lays the card out by
  its canvas, so a third height is host work. The checker marks it CHANGED; say why in the README.
- **The bands:** `const H = 144, WMIN = 1280, WMAX = 3440;`, with no `TOPPAD`; `render` draws `opts.width` wide (§2.1). A new wheel helm is a band, so it takes this line, not a card line above.
- **The punt tiller:** the tiller's line, `const W = 120, H = 244, PVX = 60, PVY = 214;`.

### 3.4 Hit geometry for every control

Every control a player can click has its hit shape exported in rig-local px: a rect `{x, y, w, h}`, or a circle
`{cx, cy, r}` (a circle may also be written `{x, y, r}`, as the tiller's button is). The sizes are §1 rule 4.

- **Today:**
  - the dashes export `SW.start` / `SW.deck` / `SW.spot`, `WHEEL`, `driveHandle` with `DRIVE.hitR`, and
    `slotBox(i, portrait)`;
  - the tiller exports `hit.button` and `hit.shift`, and the game reads neither yet.
- **The port hand-copies everything else** (`HelmDashGeometry.cs` :6, "lifted verbatim from the immutable rig
  sources"). ADR 0025 wants it the other way round: hit geometry and anchors "come OUT of the rig as data", never
  hand-duplicated into the port (:106-108).
- **Add each new control to the table the rig already has:** `SW` on a dash, `hit` on the tiller. Key it by the
  control's name in lower case (`anch`, `nav`, `inst`, `horn`, `bilge`, …). A new dash or band exports `SW`; the
  punt tiller exports `hit`.
- **Every shape lies inside the card:** x from 0 to W, and y from −`TOPPAD` to H − `TOPPAD` (rig-local).
- **No two shapes in the table overlap.**

### 3.5 Night

`night` exists today. It brightens the gauges (Cape ×1.19, Novi ×1.32, console ×1.60), barely changes the sport
(×1.01) while its readouts' contrast falls, and never lights the banks (A3 §4–§5).

**The revision lights every live legend and raises contrast** (§1 rules 2 and 6).

The game sets `night` automatically, 19:00–06:00 (`HelmPanelLight.cs` :27-31). The INST switch drives the fitted
instruments' night prefs in P1, and becomes the panel light's override in P2. (The README says night "follows the
NIGHT PANEL switch"; in the game today, it follows the clock.)

### 3.6 The C# port path (ADR 0025, accepted 2026-08-03)

Each rig is redrawn live by a hand-written C# renderer (`CapeDashRender.cs`, `NoviDashRender.cs`,
`ConsoleDashRender.cs`, `SportDashRender.cs`, `TillerRigRender.cs`), with no JS in the build. So:

- **Draw only with what the C# layer has** (ADR 0025 :68): rects, paths with clip, linear and radial gradients,
  nearest-neighbour `drawImage`, the transform stack, and `'lighter'`. Nothing else: no filters, no shadows, no text.
- **Keep geometry in named, exported constants**, not as bare numbers inside the paint code. The port pins
  "numeric geometry goldens" read from your source (ADR 0025, the addendum).
- **Whole-pixel geometry.**
- The proofs in `docs/art/proofs/*-csharp.png` show what the port draws today, which is what the player sees. Match
  them where you don't mean to change something.

### 3.7 Wordmarks

**Every maker's name on a helm is invented** (the UI README). Keep the ones you have. A new helm gets a new invented
wordmark, never a real maker's.

### 3.8 Accessibility

**Redundant coding: shape + position + text, never colour alone** (§1 rule 3). The states that fail it today are
listed in `a3-legibility.md` §9 (in the package), and in §1 above.

### 3.9 The pinned additions: params, hit keys and caps

**Every rig this pass touches or makes exports two new tables**, so the game and the checker read its controls as
data:

- **`PARAMS`:** every `render` param, old and new → `{ default, values: [...] }`, or `{ default, range: [lo, hi] }`
  for a number.
- **`STATES`:** every key in the hit table → `{ param, values: [...] }`: the param that draws that control's state,
  and its values. **Each value must draw differently.**

**`cap`** is an array of control names drawn as blank caps; its default is `[]`. Each name in the rig's cap list
below must change the picture.

| Rig (job) | New params | Hit keys the table must hold (floor, rig px) | Cap list |
|---|---|---|---|
| `CapeRig` (2) | `anch`, `nav`, `inst`, `cabin`, `vhf`, `horn` (pressed), `bilge` (`'off'`/`'auto'`/`'on'`), `key` (`'off'`/`'acc'`/`'run'`/`'start'`; its default follows `running`), `glow`, `cap` | `anch`, `spot`, `nav`, `inst`, `deck`, `cabin`, `vhf`, `horn`, `bilge`, `start` (28) | `deck`, `cabin`, `vhf`, `horn`, `bilge` |
| `NoviRig` (2) | the Cape's, plus `pump` | the Cape's, plus `pump` (28) | the Cape's, plus `pump` |
| `ConsoleRig`, `SportRig` (2) | `anch`, `key`, `kill` (the cord pulled), `trim` (−1/0/1), `horn`, `cap` | `anch`, `spot`, `deck`, `start`, `horn`, `trim`, `kill` (26) | `deck`, `horn`, `trim`, `kill` |
| `TillerRig` (2) | `gear` gains `'N'` (so `'F'`/`'N'`/`'R'`), `trim` (−1/0/1), `choke`, `cord` (0..1, how far it's pulled), `cap` | `button`, `shift`, `trim`, `choke`, `cord` (24) | `trim`, `choke`, `cord` |
| `CapeBandRig`, `NoviBandRig` (1) | `width` (1280–3440; default 1280), `running`, `drive`, `steer`, `rpm`, `fuel`, `blink`, `night`, `anch`, `spot`, `nav`, `inst`, `depth` (metres; `null` draws the plate), and the dash rig's params for its brow instruments | `anch`, `spot`, `nav`, `inst`, and `slot0`, `slot1`, … one per housing (24) | — |
| `ConsoleBandRig`, `SportBandRig` (1) | the same, with `heading` (degrees) in place of `depth` and only `anch` and `spot` as switches; the console band also takes `scheme` and `paint` | `anch`, `spot`, and `finder` for the finder's housing (24) | — |
| `WheelhouseRig` (3), `BridgeRig` (4), `FlybridgeRig` (6) | `running`, `drive`, `steer`, `fuel`, `rpm`, `night`, `blink`, `anch`, `spot`, `nav`, `inst`, `key`, `cap`; the wheelhouse adds `winch` (−1/0/1); the flybridge adds `variant` | `anch`, `spot`, `nav`, `inst`, `start` (by height, §1 rule 4); the wheelhouse adds `winch` | the wheelhouse: `winch` |
| `RibConsoleRig` (5) | `running`, `drive`, `steer`, `fuel`, `rpm`, `night`, `blink`, `heading`, `anch`, `spot`, `key`, `trim`, `kill`, `twin`, `rpm2` (the second tach), `cap` | `anch`, `spot`, `start`, `trim`, `kill` (by height) | `trim`, `kill` |
| `PuntTillerRig` (7) | the tiller's params, with `gear` `'F'`/`'N'`/`'R'`, plus `trim`, `choke`, `cord`, `cap` | `button`, `shift`, `trim`, `choke`, `cord` (24) | `trim`, `choke`, `cord` |
| `Sloop88HelmRig` (8) | `running`, `drive`, `steer`, `fuel`, `rpm`, `night`, `blink`, `heading`, `awa` (apparent wind angle, degrees off the bow, + starboard), `aws` (knots), `sog` (knots), `staysail` (set), `anch`, `key` | `start`, `anch`, `staysail` (by height) | — |
| `Sloop88HelmRig` (8): the band adds | `width`, `main` and `jib` (the sheets, 0 eased … 1 hard in), `autotrim` (default `true`) | `autotrim` (24) | — |
| `WheelhouseRig`, `BridgeRig`, `FlybridgeRig` (3, 4, 6): the band adds | `width`, `depth` and the brow instruments' params, as the pilot bands | `slot0`, `slot1`, … one per housing (24) | — |
| `RibConsoleRig` (5): the band adds | `width`, and the finder's params, as the skiff bands | `finder`, for its housing (24) | — |

- **The new dashes also export what the host drives a dash by:** `WHEEL`, `DRIVE`, `driveHandle`,
  `driveFromPoint`, `driveThrottle`, `driveGear`, `ANG`, `maxSteer` and `wheelTurn`, with the Cape's meanings. The
  wheelhouse, the bridge and the flybridge also export `SLOTS` and `slotBox` for their brow mounts.
- **The punt tiller exports the tiller's names:** `W, H, pivot, maxSteer, hit, paint, render, angle`.
- **Booleans default to `false`**, except where the table says otherwise. A new param's default draws today's
  picture on a revised rig.

---

## 4. Deliverables, and how they travel

**Eight jobs, one zip each.** The owner sends them one at a time. **One job per return; don't bundle.**

| Job | Zip | What you make |
|---|---|---|
| 1 | `HH-helms-job1-bands-2026-09-28.zip` | the four bands (§2.1) |
| 2 | `HH-helms-job2-revision-2026-09-28.zip` | the five revised helms, and the rest of each dash's controls in its band (§2.2) |
| 3 | `HH-helms-job3-wheelhouse-2026-09-28.zip` | the wheelhouse helm, a band (§2.3.1) |
| 4 | `HH-helms-job4-bridge-2026-09-28.zip` | the ship's bridge, a band (§2.3.2) |
| 5 | `HH-helms-job5-rib-console-2026-09-28.zip` | the RIB console, a band (§2.3.3) |
| 6 | `HH-helms-job6-flybridge-2026-09-28.zip` | the flybridge helm, a band (§2.3.4) |
| 7 | `HH-helms-job7-punt-tiller-2026-09-28.zip` | the punt tiller, a card (§2.3.5) |
| 8 | `HH-helms-job8-sloop88-helm-2026-09-28.zip` | the Sloop 88's helm, a band (§2.3.6) |

**Each zip holds one folder** with at most 500 entries, folders included. Inside it:
- `README.md`: the job, what's in the zip, what to make, how to run the checker, and what to return;
- the files you work on and from, at their repo paths (`docs/…`): the rigs and the shared instruments they composite,
  their READMEs and previews, `docs/art/rigs/ui/README.md`, this brief and `helm-audit.md`;
- the C# proofs for those helms (`docs/art/proofs/*-csharp.png`) and, once the game draws its greybox band, plates
  of it at 1280, 1920 and 3440 wide;
- for a new helm, the hull rigs and gameplay sidecars of the hulls it's for; for job 8, `sail-controls.md`, the sail
  kit's READMEs and the 88's sidecars;
- `evidence/`: the audit's material for the job, such as the resample sheets (`a3-resample-*-csharp.png`: each dash
  as the game shows it, at every scale), `a3-legibility.md`, `a4-switches.md` and the view mock-ups;
- `check/`: the checker, the job's spec (`job.json`), the as-shipped copies of the rigs it compares against
  (`baseline/`, in jobs 2 and 7; job 2's holds the bands as job 1 landed them), the package manifest
  (`package.sha256`) and the checker's output on the package as shipped (`today.txt`);
- `SHA256SUMS`.

**The checker** is `check/check-helms.cjs`. Run it from the zip's folder: `node check/check-helms.cjs`.
- **It needs only `fs`, `path` and `vm`**, so it runs in your emulated Node 18. There's no npm, no canvas library and
  no zlib.
- **It draws nothing.** It loads each rig in a `vm` context, after the instruments it composites, with a small 2D
  context that records every call and every property set as a drawing trace. It compares traces by value: a param is
  read when setting it changes the trace, and a render is deterministic when two renders give the same trace.
  - A revised rig loads after the instruments its `.dc.html` loads before it. A band (job 1) loads after its dash's
    instruments and the dash rig, so it may call the dash rig's exports.
  - A new rig loads after the scripts its folder's `.dc.html` lists before it (`support.js` excluded). A new rig that
    no preview loads is a CONTRACT FAIL.
- **It checks three things,** one line each:
  - **CONTRACT:** what a revision must keep (§3.2), and what every rig must be (§3.1, §3.6): the file, the global,
    the IIFE and export lines, the canvas line, the exported names and their types, the size, every old param still
    read, determinism, and no text, filters, shadows, image assets, randomness or clock reads while rendering.
  - **TARGET:** what the job asks for (§2.1, §3.9): the new rigs, the new params, the hit keys and their floors, no
    overlaps, `PARAMS` and `STATES`, and the caps. For a band, also `PIECES`, `PANEL` and `layout`; renders at
    1280, 1920 and 3440 wide, each 144 tall; its pieces together no wider than 1280; and every hit shape inside
    its piece.
  - **SCOPE:** which files you may touch. Each job may change or add files only in its own folders (the job's
    README names them). Every other file must come back as it went. A line-ending change alone isn't a change.
- **Each line is OK, CHANGED or FAIL.** CHANGED is allowed, but say why in CHANGES.md: a new name or param, a new
  picture, a third card height. A `NOTE` line lists the less common canvas calls a rig uses (`getImageData`, for
  one), for the C# port.
- **It exits 0** when everything is met, **2** when only targets are unmet (the package as shipped: see
  `check/today.txt`), **1** on any CONTRACT or SCOPE FAIL, and **3** if it can't run at all.
- **What it can't see is the picture.** It doesn't judge colour, contrast or legibility (§1), or whether a band
  stretches a detail; those are judged by eye on intake, against the proofs and the resample sheets.

**What comes back, per job:**
1. the changed and new files, at their repo paths inside the job's folder;
2. `CHANGES.md` at the folder's root: what you changed and why, per file; what you couldn't do, and why; your
   questions (§5);
3. the checker's output: `node check/check-helms.cjs --out check/result.txt`;
4. `SHA256SUMS` over every file you return: `node check/check-helms.cjs --sums` writes it;
5. **a README per rig**, in the house format: coordinates first, then the parts, then the param table. Two tables are
   new: **the hit table** (every control, its shape in rig-local px, and the `TOPPAD` rule) and **the state table**
   (every control's states, lit and unlit, by day and by night);
6. **a STATE SHEET export per rig:** every control in every state, by day and by night. The helm previews already
   export one (stop/run × astern/neutral/ahead, `cape-islander-helm/README.md` :68); extend it to the new states.
   A band's sheet also shows it at 1280, 1920 and 3440 wide.

**We verify your return on real Node with the same checker.** No byte or pixel match is expected between your
environment and ours; the checker compares drawing traces within one run. A return with a CONTRACT or SCOPE FAIL goes
back to you. It isn't patched on intake.

**After you:** an intake charter (the art lane) lands each job as its own PR, `art/<short-desc>`, one concern per PR.
The C# port is a ui-ux PR after that.

---

## 5. Open questions, and what they gate

**D1–D6 are ruled** (`helm-audit.md` §0), and nothing below blocks drawing:

1. **Nav lights (D2 (a), answered in `helm-audit.md` §3.4).** They come with Claude Design's lighting pass on each
   hull, which comes first, and the helms are reconfigured after it.
2. **The dory's searchlight (D2 (b), answered in `helm-audit.md` §3.4).** It comes with Claude Design's lighting pass
   on each hull, which comes first, and the helms are reconfigured after it. The dory has no card, so this changes no
   art.
3. **The 720p focus** (the record: D1's focused dash, which the band replaced). It showed at ×0.883 there, because ×1 would cover the bottom 64 px of the HUD band.
   §1's s2 floor holds at either scale.
4. **The console and sport DECK bats.** These hulls carry no deck lamp. Draw the `cap`, and keep the bat's states.
   Whether a lamp is ever authored on the hull is hull art, a separate job.

**Ask, don't guess.** A question goes in CHANGES.md with your best answer drawn, and the owner rules it on intake.

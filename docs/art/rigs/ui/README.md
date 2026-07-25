# The art director's UI rigs — the diegetic boat instruments (imported 2026-07-24)

These are the art director's parametric **UI rigs**: the in-world instruments and helm consoles the
boats carry — the start-of-game watch, the outboard tiller, the single-lever throttle, the depth &
fish sounders, the compass, and the four helm dashes that assemble them. Imported here **verbatim** as
the single source of truth, the same discipline the iso boat/kit rigs follow under
[ADR 0021](../../../adr/0021-in-engine-js-rig-baking.md).

⚠️ **Do not edit these files.** They are the art director's source. Fixes belong upstream, or the next
drop silently reverts them. Anything the engine needs that a rig doesn't provide belongs in *our* host
code, never in their file. (Same rule as `../README.md`.)

> **These are NOT turntable rigs.** Unlike the sibling iso rigs one folder up (dory, punt, character,
> fishing kit), these are **screen-facing 2D instruments** — there is no heading facing, no 8/32-way
> `order`, no `ROCK`, no azimuth convention. **They must NOT go through the boat baker's
> `DirForCell` / `FacingsAreCounterClockwise` / azimuth-probe path.** The "azimuth split" warning in
> `../README.md` does not apply to anything in this folder.

---

## What was imported (and what wasn't)

**Committed here** — the 10 rig `.js` sources, de-duped to one canonical copy each. The five helm
folders each shipped their own copy of the shared instruments (`leverRig`, `depthRig`, `fishRig`,
`compassRig`); those copies were confirmed **md5-identical** across every folder before collapsing to
one file, so there is a single source per rig.

| File | Global | Kind | Role |
|---|---|---|---|
| `watchRig.js` | `WatchRig` | instrument | resin digital watch — the game clock/date (replaces the on-screen clock) |
| `tillerRig.js` | `TillerRig` | instrument | the one control a motorised dory shows; steering is a 1:1 rotation the caller applies |
| `leverRig.js` | `LeverRig` | instrument | single-lever throttle + F/N/R shift; **the moving part every helm reuses** |
| `depthRig.js` | `DepthRig` | instrument | flush-mount depth read + shallow alarm (the basic brow sounder) |
| `fishRig.js` | `FishRig` | instrument | colour sonar upgrade; same cutout as the depth sounder |
| `compassRig.js` | `CompassRig` | instrument | heading; dome bracket or flush Ritchie |
| `consoleRig.js` | `ConsoleRig` | helm | centre-console skiff dash |
| `sportRig.js` | `SportRig` | helm | polished sister of the console skiff |
| `noviRig.js` | `NoviRig` | helm | modern downeast pilothouse (brow: sounder + radar + gps slots) |
| `capeRig.js` | `CapeRig` | helm | 1982 old-school wheelhouse |

**Not committed** — the `*.dc.html` interactive previews and their `support.js` preview runtime. Per
the established contract (`../README.md`, the fishing-kit section), demo pages live in the art
director's design workspace, not the repo. Their per-rig **READMEs** (API reference — params, helpers,
hit-geometry) are kept under [`readmes/`](readmes/) because the C# integration reads them.

---

## The diegetic rule (why these exist)

There is no floating HUD in Hidden Harbours. **In-world UI is only ever a real object that carries
it** — a clock, a gauge, a dial, a switch, a screen bolted to a dash. A bare dory shows nothing; a
motor earns one tiller; a rigged console earns the whole dash. This is the direction ratified in
[`../../../design/diegetic-ui-and-inventory.md`](../../../design/diegetic-ui-and-inventory.md) §3 —
*information is an earned instrument* — and these rigs are its physical form. Every brand wordmark on
them is original.

---

## Shared conventions (from the drop)

- **`render(opts) → HTMLCanvasElement`** on every rig. The rig is **stateless**: the same opts always
  draw the same sprite. **The game owns the state; the rig only draws.** (This is the seam our C#
  integration binds to — game state → opts → pixels.)
- Pixel art: no anti-aliasing, KTC master palette, procedural 7-segment / needle / card art — **no
  external image assets**, no network, no DOM. (Verified: the rigs touch only `Math.*`, typed arrays,
  `globalThis`, and Canvas2D drawing calls.)
- **Night** is a parameter, not a separate rig; on a helm it follows the NIGHT PANEL switch.
- A helm draws in two passes: the whole static dash (`ConsoleRig.render(...)` etc., **including** its
  fitted sounder + compass) then the **one** moving part it leaves out — the binnacle lever —
  composited on top at `DRIVE.px / DRIVE.pivotY (+ TOPPAD)`.

### Signal glossary (the params our game state maps onto)

| Signal | Range | Meaning | Game source |
|---|---|---|---|
| `drive` | −1 … +1 | single lever: −1 astern · 0 neutral · +1 ahead (throttle **and** F/N/R in one) | `ThrottleDetentModel.Drive` |
| `steer` | −1 … +1 | wheel/tiller: −1 port · 0 amidships · +1 stbd | boat helm steer |
| `rpm` | 0 … 1 | tachometer sweep | derived from drive/speed |
| `fuel` | 0 … 1 | fuel gauge; < 0.13 blinks the telltale | boat fuel state |
| `heading` | 0 … 359 | compass card | `IGameClock`/boat heading |
| `finder` | `depth` \| `fish` | which sounder is fitted | boat equipment (`SounderKind`) |
| `compass` | `none` \| `dome` \| `flush` | compass unit fitted | boat equipment (`CompassMount`) |
| `radar`,`gps` | bool | Novi/Cape brow screens (placeholders until their own rigs ship) | boat equipment |
| `night` | bool | night panel backlight | NIGHT PANEL switch |
| `blink` | 0 / 1 | shared alarm/telltale blink (~2–3 Hz) | game timer |
| `phase` | seconds | free-running time for the sonar scroll & idle | game time |

Watch face params (`h, m, s, use24, dow, date, season, year, market, night, light`) map 1:1 onto
`IGameClock` — see `Assets/_Project/Code/Core/UI/WatchFaceState.cs` (`FromClock`).

---

## How these render in the game — the OPEN architectural question

**These rigs are Canvas2D _painters_** (they return a drawn `HTMLCanvasElement`), whereas the iso boat
rigs return a raw RGBA byte buffer. That difference is the crux of how they reach Unity, and it is
**not settled** — it is the subject of **[ADR 0025](../../../adr/0025-ui-rig-runtime-rendering.md)
(Proposed)**. The hard constraints that bound the choice:

- **No JavaScript may run in the player build.** ADR 0021's editor-only V8 fence is load-bearing for
  the LGPL-2.1 licence basis and for rule 7 (60 fps, no per-frame software rasteriser). A rig's *pixels*
  may be produced by running its `.js` **in the editor**; its *behaviour* in the shipped game must be
  pure C#.
- The instruments show **continuous** live values (heading 0–359, depth, rpm, a scrolling sonar, the
  ticking clock), so — unlike the 8/32 finite boat facings — they **cannot be fully pre-baked**.

ADR 0025 weighs the live-C#-renderer, bake-the-parts-and-composite, and (rejected) run-JS-live options.
Until it is accepted, **treat the render path as undecided** — but every rig's `render(opts)` seam and
the hit-geometry it exposes (below) are stable regardless of which path wins.

## Interactivity — the rigs expose their own hit-geometry

Each rig publishes rig-local hit targets so the panel is clickable (turn the key, flip switches, swing
the lever, tap the sounder). Examples: `ConsoleRig.DRIVE {px, pivotY, hitR}` + `driveFromPoint`,
`WHEEL`, `SW.start/deck/spot`, `TOPPAD`; `LeverRig.sigFromOffset(dx,dy)` (drag → drive) and
`handleOffset`; `TillerRig.pivot / maxSteer / hit.button / hit.shift`; `DepthRig.layout(x,y,w,h)`;
`FishRig.layout(...)` + `fishGeom`; `CompassRig.cardinal8 / fmtDeg`; `NoviRig.slotBox(i, portrait)`.
The C# side transforms the pointer into rig space (normalise by the displayed rect → native canvas →
flip Y → subtract `TOPPAD`) and runs the ported hit-tests. **These geometry constants come OUT of the
rig as baked data — never hand-duplicated back into it** (the ADR 0021 "his file is the truth" rule).

---

## Rig → hull map (net-new data — see `HelmConsoleDef`)

| Helm rig | Hull |
|---|---|
| `ConsoleRig` | `boat.console_skiff` |
| `SportRig` | `boat.sport_skiff` / `boat.sport_skiff_twin` |
| `NoviRig` | `boat.lobster_boat` |
| `CapeRig` | `boat.cape_islander` |
| `TillerRig` | any motorised dory (the outboard's one control) |

> **Open ruling:** `boat.fishing_skiff` is powered but the drop supplies no console rig for it — flag
> for the owner (minimal tiller-only helm, share a console, or no readout until upgraded?). Recorded in
> `../../../design/diegetic-ui-and-inventory.md` (console handoff section).
>
> **Naming trap:** the iso hull rig `consoleIsoRig` (one folder up) is the *boat's hull sprite*; the UI
> `ConsoleRig` here is its *dashboard*. Different rigs, similar names.

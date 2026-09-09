# Audio asset manifest — adaptive audio scaffold (VS-27/28)

The `AudioDirector` (self-installing, `Code/Audio/AudioDirector.cs`) plays everything below. Every slot
still has a **procedural** placeholder generated at boot (`ProceduralAudio.cs`), and that is what plays
whenever the slot is empty — so the adaptive mix stays audible and testable end-to-end even with nothing
recorded.

**To swap in a real clip (no code — rule 2):** drop the file in `Ambient/` or `SFX/`, assign it to its
slot on **`Resources/AudioClipSet.asset`** (`AudioClipSetDef`, one `AudioClip` field per row below), and
add its row to **`LICENSES.md`**. Each player loads that asset in `Awake` and copies every non-null slot
over its own field *before* it builds its sources; a null slot keeps its placeholder. Nothing here is a
serialized reference on a component any more, so slotting a sound never touches a scene.

> Authoring standard (match the art lock spirit): **mono, 44,100 Hz**. `.ogg` (Vorbis ~q6) where it
> saves real bytes, 16-bit PCM `.wav` where a loop needs a sample-exact join (see `LICENSES.md`).
> Loops seamless — the step across the wrap no bigger than the median step inside the loop, and that is
> a *test*, not a hope. Peaks: **−12 dBFS** for beds, **−3 dBFS** for one-shots. Keep beds quiet — they
> sit *under* gameplay, and loudness is mixed at runtime.
>
> **Sources must be dry, even and eventless.** No baked reverb or stereo width, no gust inside a wind
> bed, no rev inside an engine loop, no ritardando inside a tick loop: the mix rides gain and pitch, and
> a clip that performs those moves itself fights it.

## Buses (independent player volumes)

| Bus | Director field | Notes |
|---|---|---|
| Ambience | `_ambienceVolume` | calm bed, gulls, aboard boat bed (oar/water **or** outboard engine), wind tell |
| SFX | `_sfxVolume` | one-shot cues (sting, warmth) |
| Music | `_musicVolume` | reserved — no music cue wired yet (future) |

Cues **duck** the ambience/music beds under them; the calm bed also **thins** as the wind tell rises.

### "Made it home" warmth is **earned** (P5)
The ashore exhale (`_homeWarmth` on coming ashore) fires **only when the sea had become a worry that
trip** — the rising-wind tell must have peaked past `AudioDirectorLogic.HomeWarmthTellThreshold` (a small
0..1 value) while aboard. A flat-calm hop to the next beach ends **quietly**; coming in from a building
blow lands the warmth ("the sea warned me → I made it"). The peak tell is tracked aboard from the 4 Hz
wind poll and reset each time you board, so the gate is **per-trip**. (Charter guardrail: "warmth is
earned, not constant"; bible §8.3 "the home exhale".) A **sale** (`CatchSold`) still warms
unconditionally — see the flag below.

### Aboard boat bed — Dory oars vs Punt engine
The aboard boat bed is **propulsion-aware**: a hand-rowed hull plays the oar-stroke/water bed (`_hullRow`),
an engine boat plays the looping outboard bed (`_outboardEngine`), and the two **crossfade** on a swap
(`AudioDirectorLogic.BoatLayerCrossfadePerSec`). The engine is **speed-reactive** — its volume and pitch
ride the active boat's speed over ground, read through the Core `IActiveBoatService` seam (ADR 0007), so it
idles when moored and revs underway.

> **Flag (lead-architect / gameplay-systems):** the director picks oars-vs-engine from the active hull's
> stable **id** (`ActiveBoatChanged.BoatId`; the dory rows, every other hull runs an engine), because that
> signal carries the id but **not** a `PropulsionType` — and `PropulsionType` lives in the Boats module, which
> the Audio lane must not reference (asmdef is Core-only). The robust fix is a small **Core propulsion field on
> `ActiveBoatChanged`** (or on the `IActiveBoatService` snapshot), populated by the Boats/Player publisher.
> That's a Boats/Player + Core change, out of the audio lane — flagged here rather than reaching across lanes.

## Clips needed (placeholder → real)

| Director field | Committed clip | Loop? | Bus | Role / trigger | Now playing |
|---|---|---|---|---|---|
| `_calmBed`    | `Ambient/calm_sea_bed.ogg`  | yes | Ambience | always-on calm-sea wash | **real** — LICENSES.md row 1 |
| `_gulls`      | `Ambient/gulls.ogg`         | yes | Ambience | sparse gull calls over the bed | **real** — LICENSES.md row 2 |
| `_hullRow`    | — | yes | Ambience | oar-stroke / water bed — **aboard a rowed hull (the dory)**; crossfades with the engine bed on a swap | `ProceduralAudio.HullRow` — **slot held**: no CC0 rowing recording found, owner shopping list in LICENSES.md |
| `_outboardEngine` | `Ambient/outboard_engine.wav` | yes | Ambience | looping outboard-engine bed — **aboard an engine boat (the punt and up)**; pitch + volume rise with speed over ground | **real** — LICENSES.md row 3 |
| `_windTell`   | `Ambient/wind_tell.wav`     | yes | Ambience | **the SACRED rising-wind tell** — loudness driven by wind strength, audible *before* trouble (P1) | **real** — LICENSES.md row 4 |
| `_catchSting` | — | no  | SFX | bright sting on `FishCaught` | `ProceduralAudio.CatchSting` — **slot held**: musical, waits for the score (foley guide §9) |
| `_homeWarmth` | — | no  | SFX | "made it home" warmth on `CatchSold` / coming ashore | `ProceduralAudio.HomeWarmth` — **slot held**, as above |
| `_landingHit` | — | no  | SFX | **the landing frame** — `JuiceMomentCue(Landing)`, the frame the fish leaves the water (with the hit-stop and the splash; juice charter §4.1/§4.5) | **slot held** (juice PR 3): NO placeholder — silent until a file lands in `AudioClipSet.LandingHit`; owner shopping list in LICENSES.md |
| `_saleChime`  | — | no  | SFX | **the sale's reward beat** on `CatchSold` (with the coins flying in the notebook; §4.2) | **slot held** (juice PR 3): plays `_homeWarmth` in its place until a file lands in `AudioClipSet.SaleChime` |
| `_digStrike`  | — | no  | SFX | **the shovel's strike** — `JuiceMomentCue(DigStrike)`, with the sand chunks (§4.3) | **slot held** (juice PR 3): NO placeholder — silent until a file lands in `AudioClipSet.DigStrike` |
| `_castEntry`  | — | no  | SFX | **the line touches down** — `JuiceMomentCue(CastEntry)`, with the rings (§4.3) | **slot held** (juice PR 3): NO placeholder — silent until a file lands in `AudioClipSet.CastEntry` |

## Rod-fight sound layer (Rod Fishing v2 — `FishingAudio`)

A second self-installing player, **`FishingAudio`** (`Code/Audio/FishingAudio.cs`), voices the whole
rod-fishing arc **diegetically** (design `rod-fishing-v2-brainstorm.md` §2–3, §7 — the rod is the
instrument, no HUD). It consumes ONLY the Core `FishingStateChanged` snapshot (rule 4) and all its
decisions are the pure, EditMode-tested `FishingAudioLogic`. As above, every clip has a procedural
placeholder and takes its real recording from the shared `AudioClipSet.asset` — same swap flow, no code
changes.

**How the owner tunes it (no code):** select the `[FishingAudio]` object at runtime (or a slotted
prefab later) — every layer has a tooltip'd 0..1 level (`_fishingVolume` master, `_creakLevel`,
`_payoutLevel`, `_strainLevel`, `_reelLevel`, `_thrashLevel`, `_cueLevel`).

| Director field | Committed clip | Loop? | Role / trigger | Now playing |
|---|---|---|---|---|
| `_rodCreakLoop` | `SFX/rod_creak.ogg` | yes | wind-back draw (`WindBack`) — deepens as the rod loads (`RodBend01`) | **real** — LICENSES.md row 5 |
| `_castWhoosh` | `SFX/cast_whoosh.wav` | no | the flick released (enter `Cast`) — whip + line whistle | **real** — LICENSES.md row 10 |
| `_splashDown` | `SFX/splash_down.wav` | no | the line lands (exit `Cast`) — pairs with the art lane's `SplashBurst` | **real** — LICENSES.md row 11 |
| `_payoutTickLoop` | `SFX/payout_tick.ogg` | yes | the depth drop (`Sinking`) — its **pitch slows** as `Depth01` → 1 (the no-gauge depth read, §2.3) | **real** — LICENSES.md row 6 |
| `_bottomSettle` | `SFX/bottom_settle.wav` | no | the slack **bottom tell** opens pre-bite — "you felt bottom" | **real** — LICENSES.md row 14 |
| `_bobberPlop` | `SFX/bobber_plop.wav` | no | the **cast-path** bite tell (`Bite` with `Depth01 = 0`) | **real** — LICENSES.md row 12 |
| `_rodKnock` | `SFX/rod_knock.wav` | no | the **depth-path** bite tell (`Bite` with `Depth01 > 0`) — the deep rod-tip knock, in the rod, not the UI | **real** — LICENSES.md row 13 |
| `_strainGroanLoop` | `SFX/strain_groan.wav` | yes | the continuous line-strain groan — gain rides `Tension01^1.6` (the "ease off!" voice), pitch tightens with tension; services the **legacy `Fighting`** phase too | **real** — LICENSES.md row 7. The **weakest fit** in the set: an even low drone, not a rope under load. First to re-source. |
| `_reelClickLoop` | `SFX/reel_clicks.ogg` | yes | reel clicks **only while gaining** (`Landing01` rising) | **real** — LICENSES.md row 8 |
| `_slackRelease` | `SFX/slack_release.wav` | no | the mid-fight slack window opens — the diegetic "PULL now" (§3) | **real** — LICENSES.md row 15 |
| `_surfaceThrashLoop` | `SFX/surface_thrash.ogg` | yes | she's up (`FightSurface`) — swells with `RodBend01` + her dart speed, **pans on her offset** | **real** — LICENSES.md row 9 |
| `_snapSting` | `SFX/snap_sting.wav` | no | threw the hook (`Snapped`) — a **cozy** sting, never a punishment sound (§7) | **real** — LICENSES.md row 16 |
| `_landedFlourish` | `SFX/landed_flourish.wav` | no | landed (`Landed`) — warm flourish + the wet slap on the boards; layers under the `AudioDirector`'s musical `_catchSting` (diegetic vs reward) | **real** — LICENSES.md row 17 |

> **Flags:** (a) `FishingAudio` keeps its own master level rather than reaching into the
> `AudioDirector`'s private bus fields — folding both players onto shared buses (an AudioMixer) is a
> small in-lane follow-up. (b) Fishing cues do not yet duck the ambient beds; wire that when the
> shared bus lands.

## Missing Core signals (flagged for a follow-up — NOT added this round)
The Audio lane subscribes to **existing** Core signals only (`FishCaught`, `CatchSold`,
`ControlModeChanged`, `ActiveBoatChanged`) and polls the deterministic `IEnvironmentService` /
`IActiveBoatService`. Two cues would read truer with signals Core does not yet carry — flagged here for
the owning lanes rather than reached across:

- **A "reached safe harbour" signal** (world-content / Core). The home-exhale wants to fire on **arriving
  at the wharf/safe harbour**, but Core has **no harbour/wharf/safe-zone concept** and no such event. As a
  faithful v1 we proxy it: coming ashore (`ControlModeChanged` on-boat→OnFoot; Build 5 split "on the
  boat" into OnDeck + Aboard-at-the-helm, and a helm⇄deck hop is neither boarding nor coming ashore —
  see `AudioDirectorLogic.IsOnBoat`) **after the sea had become a
  worry** (peak wind tell past threshold). The robust fix is a small Core signal like
  `EnteredSafeHarbour` (or a "safe zone" flag on the disembark) published by world-content when the player
  docks at a harbour — then the warmth keys off *actually being home*, not a wind proxy.
- **A `PropulsionType` on `ActiveBoatChanged`** (Boats/Player + Core). The aboard boat bed picks oars-vs-
  engine from the hull **id** because the signal carries no propulsion type and the Audio asmdef is
  Core-only (see the boat-bed flag above). A Core propulsion field would remove the id heuristic.
- **RESOLVED (juice PR 3) — a distinct `CatchSold` reward cue.** `_saleChime` is the sale's slot now
  (`SFX/sale_chime.wav`); until its file lands the director plays `_homeWarmth` in its place, so nothing
  the player hears changed. The home-warmth stays the rarer, earned arrival cue. The other three moment
  slots (`_landingHit`, `_digStrike`, `_castEntry`) have NO placeholder by the charter's rule — audio is
  a purchase — so each is silent until its file is dropped on the director.

## Wishlist (future, not wired this round)
- A light **music** stem for the harbour / title (would slot onto the Music bus).
- Per-sea-state ambience variants (Glass → Storm) layered with the wind tell.
- A distinct **grounding** alarm on `BoatGrounded` (P5) — a follow-up once the warning palette exists.

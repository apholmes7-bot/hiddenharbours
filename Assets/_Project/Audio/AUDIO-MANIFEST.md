# Audio asset manifest — adaptive audio scaffold (VS-27/28)

The `AudioDirector` (self-installing, `Code/Audio/AudioDirector.cs`) plays everything below. Until the
owner's real SFX exist, each clip is generated **procedurally** at boot (`ProceduralAudio.cs`) so the
adaptive mix is audible/testable end-to-end. **To swap in a real clip:** drop the `.wav` in the folder
below and assign it to the matching serialized field on the `AudioDirector` component — the procedural
placeholder is only used when the field is empty, so no code changes.

> Authoring standard (match the art lock spirit): mono unless noted, 44.1 kHz, `.wav` (PCM) for SFX,
> seamless loops for beds. Keep beds quiet — they sit *under* gameplay. Loudness is mixed at runtime.

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

| Director field | Real asset (place here) | Loop? | Bus | Role / trigger | Placeholder |
|---|---|---|---|---|---|
| `_calmBed`    | `Ambient/calm_sea_bed.wav`  | yes | Ambience | always-on calm-sea wash | `ProceduralAudio.CalmSeaBed` |
| `_gulls`      | `Ambient/gulls.wav`         | yes | Ambience | sparse gull calls over the bed | `GullCalls` |
| `_hullRow`    | `Ambient/hull_row.wav`      | yes | Ambience | oar-stroke / water bed — **aboard a rowed hull (the dory)**; crossfades with the engine bed on a swap | `HullRow` |
| `_outboardEngine` | `Ambient/outboard_engine.wav` | yes | Ambience | looping outboard-engine bed — **aboard an engine boat (the punt and up)**; pitch + volume rise with speed over ground | `OutboardEngine` |
| `_windTell`   | `Ambient/wind_tell.wav`     | yes | Ambience | **the SACRED rising-wind tell** — loudness driven by wind strength, audible *before* trouble (P1) | `WindTell` |
| `_catchSting` | `SFX/catch_sting.wav`       | no  | SFX | bright sting on `FishCaught` | `CatchSting` |
| `_homeWarmth` | `SFX/home_warmth.wav`       | no  | SFX | "made it home" warmth on `CatchSold` / coming ashore | `HomeWarmth` |

## Rod-fight sound layer (Rod Fishing v2 — `FishingAudio`)

A second self-installing player, **`FishingAudio`** (`Code/Audio/FishingAudio.cs`), voices the whole
rod-fishing arc **diegetically** (design `rod-fishing-v2-brainstorm.md` §2–3, §7 — the rod is the
instrument, no HUD). It consumes ONLY the Core `FishingStateChanged` snapshot (rule 4) and all its
decisions are the pure, EditMode-tested `FishingAudioLogic`. As above, every clip has a procedural
placeholder until real SFX slot into the serialized fields — same swap flow, no code changes.

**How the owner tunes it (no code):** select the `[FishingAudio]` object at runtime (or a slotted
prefab later) — every layer has a tooltip'd 0..1 level (`_fishingVolume` master, `_creakLevel`,
`_payoutLevel`, `_strainLevel`, `_reelLevel`, `_thrashLevel`, `_cueLevel`).

| Director field | Real asset (place here) | Loop? | Role / trigger | Placeholder |
|---|---|---|---|---|
| `_rodCreakLoop` | `SFX/rod_creak.wav` | yes | wind-back draw (`WindBack`) — deepens as the rod loads (`RodBend01`) | `ProceduralAudio.RodCreak` |
| `_castWhoosh` | `SFX/cast_whoosh.wav` | no | the flick released (enter `Cast`) — whip + line whistle | `CastWhoosh` |
| `_splashDown` | `SFX/splash_down.wav` | no | the line lands (exit `Cast`) — pairs with the art lane's `SplashBurst` | `SplashDown` |
| `_payoutTickLoop` | `SFX/payout_tick.wav` | yes | the depth drop (`Sinking`) — its **pitch slows** as `Depth01` → 1 (the no-gauge depth read, §2.3) | `PayoutTick` |
| `_bottomSettle` | `SFX/bottom_settle.wav` | no | the slack **bottom tell** opens pre-bite — "you felt bottom" | `BottomSettle` |
| `_bobberPlop` | `SFX/bobber_plop.wav` | no | the **cast-path** bite tell (`Bite` with `Depth01 = 0`) | `BobberPlop` |
| `_rodKnock` | `SFX/rod_knock.wav` | no | the **depth-path** bite tell (`Bite` with `Depth01 > 0`) — the deep rod-tip knock, in the rod, not the UI | `RodKnock` |
| `_strainGroanLoop` | `SFX/strain_groan.wav` | yes | the continuous line-strain groan — gain rides `Tension01^1.6` (the "ease off!" voice), pitch tightens with tension; services the **legacy `Fighting`** phase too | `StrainGroan` |
| `_reelClickLoop` | `SFX/reel_clicks.wav` | yes | reel clicks **only while gaining** (`Landing01` rising) | `ReelClicks` |
| `_slackRelease` | `SFX/slack_release.wav` | no | the mid-fight slack window opens — the diegetic "PULL now" (§3) | `SlackRelease` |
| `_surfaceThrashLoop` | `SFX/surface_thrash.wav` | yes | she's up (`FightSurface`) — swells with `RodBend01` + her dart speed, **pans on her offset** | `SurfaceThrash` |
| `_snapSting` | `SFX/snap_sting.wav` | no | threw the hook (`Snapped`) — a **cozy** sting, never a punishment sound (§7) | `SnapSting` |
| `_landedFlourish` | `SFX/landed_flourish.wav` | no | landed (`Landed`) — warm flourish + the wet slap on the boards; layers under the `AudioDirector`'s musical `_catchSting` (diegetic vs reward) | `LandedFlourish` |

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
- **A distinct `CatchSold` reward cue.** Today a sale reuses `_homeWarmth`. A sale is a *reward* beat, not
  the *home-exhale* — they should diverge (e.g. a `_saleChime`), so the home-warmth can stay the rarer,
  earned arrival cue. Small, in-lane follow-up once a sale-chime asset exists.

## Wishlist (future, not wired this round)
- A light **music** stem for the harbour / title (would slot onto the Music bus).
- Per-sea-state ambience variants (Glass → Storm) layered with the wind tell.
- A distinct **grounding** alarm on `BoatGrounded` (P5) — a follow-up once the warning palette exists.

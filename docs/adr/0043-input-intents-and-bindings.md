# ADR 0043 — Input: intents in Core, bindings as data

- **Status:** Accepted — the eight rulings below were made by `lead-architect` in the lane charter
  of 2026-09-02 (in advance, so the lane did not wait); ratified on merge of PR 0
  (`feat/input-seam-0`). **Rolling out:** PR 1 (the helm and the verbs), PR 2 (the gamepad; this ADR
  is amended with the pad table and the owner's feel ruling).
- **Date:** 2026-09-02
- **Decision owner:** `lead-architect` (the Core contract); built by `ui-ux`.
- **Serves:** **P5 (Cozy but with Teeth)** — a control that answers the same way every time, on the
  device in the player's hand — and the PC-first target itself (ADR 0005: *"KB/mouse/gamepad become
  the primary bindings, emitted through the same intent architecture"*).
- **Related:** `0005-pc-first-target.md` (the promise), `0025-*` S1 (the helm's owner directives of
  2026-08-03 — the oars table and the stepped-and-held throttle — which this ADR does NOT redesign),
  `0035-vehicles-module.md` amendment 2026-09-02 (`IDriveInputSource` — the seam this ADR
  generalises), `../architecture/tech-architecture.md` §3 `InputService`,
  `../design/ux-and-mobile-controls.md` §9–§10.

## Context

The intent layer was promised in writing three times — `tech-architecture.md` (an `InputService`
translating raw input → intents; *"gameplay reacts to intents"*), UX §9–§10 (*"Abstract intents, not
inputs … Unity's Input System, action maps per device, runtime rebinding"*), ADR 0005 (*"keep the
intent abstraction strict — gameplay never reads raw input"*) — and never landed. **Measured
2026-09-02: forty-one production files poll `Keyboard.current` / `Mouse.current` directly**, and
`Assets/InputSystem_Actions.inputactions` was Unity's untouched template (Player/UI: Move, Look,
Attack, Jump, Crouch…) that nothing read; code comments cited it only to find free keys.

One seam existed: the driveable charter's PR 0 (#701) put the vehicle wheel behind
`IDriveInputSource` (Core) + `KeyboardDriveInputSource` (Player) + `HeldDriveInput`, because a
headless journey that set full throttle measured 0.00 m — the inline keyboard read zeroed the demand
every frame no key was held. That is the shape. This ADR generalises it before more control modes
accrete (the vehicle charter's §5 fence deliberately left the socket for it).

## Decision (the eight rulings)

1. **Core gets structs and interfaces only.** No `Unity.InputSystem` reference in `Core.asmdef`,
   ever. Intents are POCO readonly structs; **an intent never carries a device.** Pinned by
   `ControlIntentSourceTests.CoreReferencesNoInputSystem_Ever`.
2. **The seam is one read per mode per frame, in the component that owns the mode** — the walk
   controller for `WalkIntents`, the deck walk and the arrival's cabin walk for `DeckIntents`, the
   switcher for `DriveDemand`, `DevBoatInput` for `HelmIntents` (PR 1). No component polls a device
   after its mode is seamed. The two gates that already existed — `MoveActionClaim` (who owns the
   move axis) and `ShellFlow.WorldInputBlocked` (title / pause) — are applied **inside the
   device-backed source**, through one wrapper (`ControlIntentGates`), never in each consumer.
3. **Byte-identical first, then gamepad.** PR 0 and PR 1 change no shipped key: every keyboard
   mapping is pinned by an EditMode test written from the OLD code before the old read is deleted.
   The owner's helm directives (ADR 0025 S1) are not redesigned by an input lane.
4. **Latency is the honest shape.** An `Update`-read intent lands one frame after a scripted `Held`
   set, and a frame runs its physics steps before its Update; fixtures `yield return null` before they
   count. Said once, in `IControlIntentSource<T>`'s doc.
5. **`DevBoatInput` keeps its file** and gets a doc line that it is the shipped helm reading
   `HelmIntents` (PR 1). Renaming files is a cosmetic PR nobody asked for.
6. **Dev rigs stay raw** (§"out of the seam" below). They are tools, not the game.
7. **`InputSystem_Actions.inputactions` is deleted** (PR 0) and replaced by
   `Assets/_Project/Data/Input/HiddenHarbours.inputactions`; the five code comments that cited it for
   free keys now cite the new asset (`CameraZoomInput`, `BoatSpotlight`, `AnchorInput`,
   `DevInstrumentCycle`, `BoatUiWindowInput`).
8. **This ADR** records the contract, the mode list, the gate placement and the dev-rig exemption.
   `tech-architecture.md` §3/§9 and UX §10's first bullet carry a dated pointer here. Nothing else in
   the docs moves.

## The contract

```
Core/Input/ControlIntents.cs        WalkIntents { Move, Sprint, Interact, Cancel }
                                    DeckIntents { Move, Interact }
                                    IControlIntentSource<TIntents> { TIntents Read(); }
                                    HeldIntents<T> → HeldWalkIntents, HeldDeckIntents  (Reads counted)
Core/Input/ControlIntentGates.cs    Apply(raw, worldStopped, moveClaimed) — pure; live overload reads the gates
Core/Input/ActiveControlDevice.cs   ControlDevice { KeyboardMouse, Gamepad }; Current; Report(); the
                                    ActiveControlDeviceChanged signal (published on a change only)
Core/Vehicles/IDriveInputSource.cs  DriveDemand + IDriveInputSource + HeldDriveInput (#701; re-homed
                                    beside its siblings in PR 1 — a one-line move, byte-identical)

Player/Input/InputBindings.cs       the asset (InputSystem.actions — project-wide), the map names,
                                    DeviceOf(control), ReportDevice(action)
Player/Input/DeviceWalkIntentSource.cs   Walk map → WalkIntents, gated; static Map(...) is pure
Player/Input/DeviceDeckIntentSource.cs   Deck map → DeckIntents, gated; static Map(...) is pure
```

- **A consumer takes its source through one `Configure…(IControlIntentSource<T>)`** (the
  `ConfigureDriveInput` shape) and defaults to the device-backed source, made lazily. Null restores
  the default. A source is code, never scene data.
- **Polling, not callbacks.** The device sources read their actions with `ReadValue` /
  `IsPressed` / `WasPressedThisFrame` in the one read; the Input System's callback plumbing is not
  used for the polled modes. No allocation in a `Read()` (rule 7): actions are resolved once at
  construction.
- **Rule 5:** a source holds no state beyond the last read; the held sources are deterministic.
- **The device signal.** A device-backed source reports the device of any control actuated this
  frame (`InputAction.activeControl`); a held source reports nothing (a test is not a hand on a pad).
  `ActiveControlDevice.Current` starts on the keyboard — a box with nothing plugged in shows keyboard
  glyphs from the first frame — and publishes `ActiveControlDeviceChanged` on a genuine change only.
  Customers (PR 2): the interact affordance's glyph, the HUD hints, the settings sheet.

## The bindings asset — `HiddenHarbours.inputactions` (the Def for bindings, rule 6)

The project-wide actions asset (Project Settings ▸ Input System Package ▸ Project-wide Actions; the
reference is `com.unity.input.settings.actions` in `ProjectSettings/EditorBuildSettings.asset`, and it
ships as a preloaded asset). **One map per control mode**, two control schemes: `KeyboardMouse`
(complete) and `Gamepad` (declared, EMPTY until PR 2 — pinned as empty by
`TheGamepadSchemeIsPresentButEmpty_UntilPr2FillsIt`, which becomes the pad's pin when PR 2 flips it).

| Map | Action | Type | KeyboardMouse (PR 0) | Read by |
|---|---|---|---|---|
| **Walk** | Move | Value/Vector2 | `2DVector(mode=1)`: up = W ∨ ↑, down = S ∨ ↓, left = A ∨ ←, right = D ∨ → | `PlayerWalkController` ✅ PR 0 |
| | Sprint | Button | LeftShift ∨ RightShift | `PlayerWalkController` ✅ PR 0 |
| | Interact | Button | E | `ControlSwitcher` (PR 1) |
| | Cancel | Button | Esc | (PR 1/2) |
| | Mooring | Button | Q | `ControlSwitcher.ToggleMooring` (PR 1) |
| **Deck** | Move | Value/Vector2 | the same Digital composite | `DeckWalkController`, `ArrivalOpening` ✅ PR 0 |
| | Interact | Button | E | `ControlSwitcher` (PR 1) |
| **Helm** | AheadDetent / AsternDetent | Button | W ∨ ↑ / S ∨ ↓ (edge = a detent, held = the oars) | `DevBoatInput` (PR 1) |
| | Neutral | Button | Z | `DevBoatInput` (PR 1) |
| | Port / Starboard | Button | A ∨ ← / D ∨ → (the oars table needs each side; the engine steer is Starboard − Port, the helm's own sense) | `DevBoatInput` (PR 1) |
| | Brace | Button | Space | `DevBoatInput` (PR 1) |
| | Anchor | Button | Q | `AnchorInput` (PR 1) |
| | Searchlight | Button | L | `BoatSpotlight` (PR 1) |
| | InstrumentCycle | Button | K (Shift steps the pilot deck) | `DevInstrumentCycle` (PR 1) |
| | Interact | Button | E | `ControlSwitcher` (PR 1) |
| **Drive** | Throttle | Value/Axis | `1DAxis(whichSideWins=0)`: − = S ∨ ↓, + = W ∨ ↑ | `KeyboardDriveInputSource` (PR 1 re-homes it; byte-identical) |
| | Steer | Value/Axis | `1DAxis`: **+ = A ∨ ←** (LEFT is +1, the rig's own sense, ADR 0035 §5), − = D ∨ → | as above |
| | Brake | Button | Space | as above |
| **UI** | Navigate, Submit, Cancel, Point, Click, RightClick, MiddleClick, ScrollWheel | as Unity's UI map | the template's KeyboardMouse bindings, carried over verbatim | the `EventSystem` (`SellScreen` builds one with no explicit asset, so the module falls back to THIS map's `UI`); the tiered `Zoom` intent joins it in PR 2 |

**Why Digital (`mode=1`) and not the default DigitalNormalized.** The old read summed the keys and left
a diagonal at (±1, ±1) for `PlayerWalkController.VelocityFor` to clamp. Mode 1 is that sum. (After the
clamp the two modes coincide for the walk's velocity — but the asset is the transcription of the old
read, not of its downstream consequence, and a later consumer of the raw vector must not find a
different function of the same keys.)

**Why the Helm map declares Port/Starboard as buttons rather than a steer axis.** The oars table
(owner directive 2026-08-03: W+A = port oar only ahead, A alone = a stationary pivot…) is a function
of the four booleans; an axis cannot tell "both" from "neither". PR 1 pins that table. The Helm and
Drive maps are DECLARED in PR 0 (so the asset is the whole ledger) and READ from PR 1; the keys the
PR 1 audit finds beyond this table (`MooringController`'s R/Shift/Space + mouse hold,
`CatchDumpInput`'s X, `DeckIceBox`'s I/L, the fishing hold-to-haul) join the asset in PR 1.

## The gates (§2), and what is NOT byte-identical

`ControlIntentGates.Apply`: a stopped world (`ShellFlow.WorldInputBlocked`) takes EVERYTHING — the
controls are parked, not merely deaf; a claimed move axis (`MoveActionClaim.IsClaimed`) takes the
MOVE ONLY — the picker that raised it steers on the axis and confirms on Interact, so the press still
arrives. Pinned pure in `ControlIntentSourceTests`.

Applying both gates uniformly inside the source is the charter's instruction and it has **two
observable consequences the old code did not have**, both in the direction the gates' own docs
describe, both called out here rather than smuggled:

1. **The pause menu now holds a deck she is standing on, and the arrival's cabin.** Before: the
   switcher parked `PlayerWalkController` and `DevBoatInput` on `WorldInputBlocked`, but the deck walk
   and the cabin walk polled the keys straight through a pause (pause freezes the game clock, not
   `Time.deltaTime`), so W walked her on deck under the menu.
2. **The notebook (or any `MoveActionClaim` claimant) open on deck no longer both scrolls the page
   and walks her.** Before: only `PlayerWalkController.ReadInput` honoured the claim — by design, as a
   deliberate minimum with one reader; the seam is where "every movement reader honouring a claim"
   was always going to land.

Every KEY does exactly what it did; these are the two places where a key that was being read by two
things at once is now read by one.

## Out of the seam (§6): the dev rigs

`DevBoatPicker`, `GrassDevWalker`, `BoatRotationTestRig`, `Spike/DeckCharacterMeshSpikeRig`,
`DisplacedWaterSurface`'s debug key, `DevTrapInput`. Tools, not the game; they keep polling
`Keyboard.current`. The pointer paths of the overlays (chartplotter, radar, sounder, fish finder, tide
panel, notebook, catalogue, wardrobe, shell) also stay as they are — they are the notebook's world,
mouse-first by ruling — and gain `UiIntents` for confirm/cancel/navigate in PR 2 so a pad can drive
them; the pointer path is untouched.

## Rollout

- **PR 0 — the seam, proved on the walk** (this ADR): Core structs + interfaces + held sources + the
  device signal; the asset with the KeyboardMouse scheme for every map; `DeviceWalkIntentSource` /
  `DeviceDeckIntentSource`; `PlayerWalkController`, `DeckWalkController`, `ArrivalOpening` seamed;
  the gates in the source; the template deleted. PlayMode proof: `WalkIntentJourneyPlayTests` (she
  covers ground at the declared speed on a `HeldWalkIntents`, through the real controller) and the
  intro cabin walk through `HeldDeckIntents` via the arrival's own `Update`
  (`IntroCabinPassagePlayTests`). EditMode proof: the asset pinned structurally against the old truth
  table; `Map` pure; the gates; the held sources; the defaults.
- **PR 1 — the helm and the verbs:** `HelmIntents` with the oars table and the stepped throttle
  pinned as truth tables; the helm's seven readers; E/Q and the interact/haul verbs; `IDriveInputSource`
  re-homed. PlayMode: `BoatCabinJourneyPlayTests` and one helm journey through `HeldHelmIntents`.
- **PR 2 — the gamepad:** the Gamepad scheme filled; the device signal drives the affordance glyph
  and the HUD hints; `UiIntents`; the settings sheet shows the active scheme. **Owner-gated on feel.**
  The table lands here as an amendment. Note for it: `DevBoatInput` already reads a pad's d-pad
  up/down as ±detent and `buttonEast` as neutral — the charter's own proposal, already the owner's
  habit; the asset will make it data.
- **PR 3 (optional):** runtime rebinding UI; touch bindings as the mobile-port scheme.

## Consequences

- `HiddenHarbours.Core` grows a `Core/Input/` folder and no assembly reference. `HiddenHarbours.Player`
  grows `Player/Input/` (the device layer); `App` reaches it through `Player`, as it already did.
- `grep -rl "Keyboard.current\|Mouse.current" Code/` (live reads, not comments) is the lane's burn-down:
  41 files → 38 after PR 0. Each PR body carries the count.
- The asset is the one place a key lives. A comment that says "K is free" is not evidence; the asset is.
- An EditMode test can no longer press a key to prove a mapping (it never honestly could on this box);
  it reads the asset. A PlayMode test can now drive every seamed mode without a keypress.

# Hidden Harbours — Diegetic Instruments & Helm Consoles (the boat's dash is the UI)

> **Status: BUILD HANDOFF — owner-directed, in progress (2026-07-24).** The concrete implementation of the
> diegetic-instrument direction for the art director's new UI-rig drop (`docs/art/rigs/ui/`): the analog
> throttle, the watch-gated clock, and the per-boat helm consoles with upgradable equipment. Subordinate
> to [`../vision-and-pillars.md`](../vision-and-pillars.md) (CANON) and to
> [`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) (the ratified *why* — this doc is the
> *how* for one slice of it). Where this doc and the vision doc's **phasing** disagree, see §0: the owner
> has pulled this work **into M1**.
>
> Sibling docs: [`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) (the keystone rule —
> information is an earned instrument), [`boats-and-navigation.md`](boats-and-navigation.md) (the helm,
> throttle, and which instruments are boat-fitted), [`time-tides-weather.md`](time-tides-weather.md) (the
> clock the watch reads), [`ux-and-mobile-controls.md`](ux-and-mobile-controls.md) (input intents, HUD).
> ADRs: [`../adr/0025-ui-rig-runtime-rendering.md`](../adr/0025-ui-rig-runtime-rendering.md) (how the rigs
> draw), [`../adr/0021-in-engine-js-rig-baking.md`](../adr/0021-in-engine-js-rig-baking.md) (the rig-source
> contract).

---

## 0. Phasing — owner-directed into M1 (a deliberate re-scope)

[`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) §7 assigns the instrument-gated HUD and the
consoles to **M2/M3**, with the watch-gated clock as the *first* diegetic proof done in the early-M2 St
Peters opening. **The owner has re-scoped this work into M1** (2026-07-24), eyes open to the tradeoff:
adding it grows M1, so the "is this game worth making?" gate (roadmap §6) moves later. This doc records
that decision; it supersedes the M2/M3 assignment **for these specific features only** — the rest of the
diegetic-UI direction (physical inventory, container grid, merchant conversation) stays M2/M3.

The work is ordered so it lands in M1 **incrementally**, not all at once — the piece that improves the
existing slice with no art (the analog throttle) ships first, and each later piece stands on the one
before it. See §6 for the order.

### Owner decisions on record (2026-07-24)

| Question | Ruling |
|---|---|
| Where to start | **Foundation first, no Unity** — the pure-C# models, tests, data, tunables, ADRs, and the committed rig sources land before any editor/runtime wiring. |
| How the rigs render | **Live rigs** — too many variables to pre-draw whole screens. Realized as *live C# renderers* inside the no-JS fence — see [ADR 0025](../adr/0025-ui-rig-runtime-rendering.md). |
| Console window behaviour | **Free-drag & resize anywhere, remembered per boat.** |
| Carried vs boat-fitted instruments | **Only the watch is carried** (reads anywhere). Every other instrument — sounder, radar, GPS, and *any* compass — is per-boat equipment fitted to that hull. |

---

## 1. The five pieces

1. **Analog throttle** — a held, notched single-lever throttle+shift replacing the on/off keys. (§2)
2. **The watch** — the game clock/date, readable only once you own a watch. (§3)
3. **Per-boat consoles + upgradable equipment** — each engine hull's dash, with slots you upgrade. (§4)
4. **Resizable / free-drag / minimizable console windows** — the dash as an on-screen window. (§5)
5. **The render pipeline** — how the rigs draw in-game (ADR 0025); underpins 2–4.

---

## 2. The analog throttle (ships first — in-phase M1 control feel)

**The mechanic.** A keyboard key is on-or-off, so today's throttle can only be full-ahead / neutral /
full-astern. The single lever the art director drew (`LeverRig`, `drive ∈ [-1..+1]`) wants *held*
positions. So Up/Down each **step a held detent** — the player can sit at quarter, half, three-quarter
throttle. The detent value **is** the lever's `drive`, so the same number feeds the physics and draws the
lever when the console lands (zero rework).

**Grounded current state.** `DevBoatInput.ReadEngine` computes the binary `throttle = (W?1:0)-(S?1:0)` and
calls `BoatController.SetControl(throttle, steer)` — which **already** clamps a continuous ±1 and applies
the weaker astern factor (`EngineThrust`). A held `+0.5` already produces half-ahead thrust today. **The
fix is entirely input-side; no controller or physics change.**

**What landed now (foundation):**
- `ThrottleDetentModel` (pure Core POCO, no `UnityEngine`) — `StepAhead/StepAstern/ToNeutral/Reset/
  SnapToDrive`, a held `Notch`, `Drive ∈ [-1..+1]` normalized so both ends hit ±1 even with fewer astern
  notches; pure statics `Clamp/DriveFor/NearestNotch`. Unit-tested headless (`ThrottleDetentModelTests`).
- `GameConfig.HelmThrottle` (`HelmThrottleSettings`: `AheadNotches`, `AsternNotches`, `HoldRepeatPerSec`) —
  owner-tunable feel, no magic numbers (rule 6). Default 4 ahead / 2 astern / edge-only.

**What needs your Unity machine (held out of the foundation):**
- Rewrite `DevBoatInput.ReadEngine` to read key **edges** (`wasPressedThisFrame`) → step the model, and add
  `OnDisable → Reset()` so re-taking the helm never surges from a stale drive (mirrors `BoatController.Stop()`
  that `ControlSwitcher.LeaveHelm/Disembark` already call). Oar hulls (`ReadOars`) and `steer` are untouched.
- **Owner feel-check:** this changes how the Punt drives (the roadmap's #1 risk is boat feel). Play it before
  it becomes the sole engine-helm scheme. A `SnapToDrive`/hold-repeat variant is a config away.

> **Seam note (rule 4):** `ThrottleDetentModel` lives in **Core** (not Boats) so a future `InputService`/
> `ThrottleIntent` layer (`ux-and-mobile-controls.md` VS-02) and a UI-hosted lever can step the *same* held
> throttle without a Boats dependency — both keyboard edges and a lever drag write one `Drive`, last input
> wins.

---

## 3. The watch-gated clock (the cheapest first diegetic proof)

The keystone rule made concrete (`diegetic-ui-and-inventory.md` §3.3): **at the start there is no clock on
screen; you buy a watch and the time & date appear.**

**The binding.** Every `WatchRig` param maps 1:1 onto a member `IGameClock` already exposes — no calendar
math to reimplement (do **not** re-derive the weekday/market from an absolute-day index; `clock.Weekday`
and `clock.IsMarketDay` already are that). Time canon verified against the code: 4 seasons × 28 days,
Mon-first week, Friday market, day = 1200 in-game s, new game starts Early Spring · Day 1 · 06:00.

**What landed now:**
- `WatchFaceState.FromClock(IGameClock, dayStart=6, nightStart=19)` (pure Core mapper) — the 9 clock-derived
  fields; hour/minute **truncate** (a watch shows elapsed time; `HudFormat.ClockHHMM` rounds and would tick
  early); `night = hour<6 || hour>=19` as its own read, thresholds as named params (owner-tunable dusk).
  `use24`/`light` are presenter-supplied, not clock-derived. Unit-tested (`WatchFaceStateTests`).
- `PlayerGear.WatchId = "gear.watch"` + `HasWatch()` — the gate, mirroring the existing bucket/rod gating.
  **No save-schema change** (rides the existing `SaveData.OwnedGear`).

**What needs your Unity machine / later milestone:**
- An authored `gear.watch` `GearOffer` asset + a shop grant (content-validation asserts the constant↔asset
  match), else `HasWatch()` is always false.
- **The watch is a Player-side placeable presenter, not a HUD gate.** `HudController` lives in the Core-only
  `HiddenHarbours.UI` assembly and *cannot* call `PlayerGear` (Player). So the watch presenter (Player lane,
  where the gate is reachable) is what *hides* `HudController._clockLabel` at the St Peters start. The
  dormant plumbing (mapper + gate) is safe now; **flipping the M1 clock off is the St Peters task** — do not
  tear down the always-on HUD before then.

---

## 4. Per-boat consoles & upgradable equipment

Each engine hull earns a full dash (`ConsoleRig`/`SportRig`/`NoviRig`/`CapeRig`), with brow slots the player
upgrades: the sounder swaps **depth ↔ fish**, a compass fits **dome/flush**, and the Novi/Cape add **radar**
and **GPS** slots.

**Carried vs fitted (owner ruling): only the watch is carried.** The watch reads anywhere. Every other
instrument is **per-boat equipment** owned against a specific hull — buy a fish-finder for your Novi and it's
fitted to *that* Novi. This makes the dash a visible, per-boat capability (P2) and keeps the save sparse.

**What landed now (the data model — dormant, nothing gated yet):**
- Core enums `SounderKind {None,Depth,Fish}`, `CompassMount {None,Dome,Flush}`, `ConsoleRigKind
  {None,Console,Sport,Novi,Cape}`, and the `HelmFit` value struct (all in Core so Boats *and* UI read them
  without a cross-module ref, rule 4).
- `HelmConsoleDef` ScriptableObject (`Data/Boats/Helms`) — names the renderer + the default fit + which slots
  the helm supports. **No baked `Sprite[]`** (continuous-state; ADR 0025), unlike `BoatVisualDef`.
- Append-only `BoatHullDef.Helm` pointer (mirrors the existing `Visual`/`DeckContainer` pointers). Null = no
  console (the dory).
- `BoatEquipment.EffectiveFit(console, ownedForHull)` — the pure reader = the hull default with owned
  upgrades layered on, only where the slot is supported (`PotLocker` recompute-don't-store discipline).
  Instrument ids (`instrument.fish_finder/radar/gps/compass_dome/compass_flush`). Unit-tested
  (`BoatEquipmentTests`).

**Rig → hull map** (net-new data to author): `ConsoleRig`→`boat.console_skiff`, `SportRig`→`boat.sport_skiff
(_twin)`, `NoviRig`→`boat.lobster_boat`, `CapeRig`→`boat.cape_islander`, `TillerRig`→any motorised dory.

**What needs a later milestone / owner ruling:**
- **Save state v4→v5** (its own ADR on the ADR-0020 template): a sparse `List<BoatInstrumentDto{HullId,
  InstrumentId}>` (absent = default fit). Held throttle and the resolved fit are **never** saved (rule 5).
- **Equipment purchase** as `GearOffer`-style priced assets (economy) → the owned-id the reader consumes.
- **Open ruling — the powered `boat.fishing_skiff` has no console rig** in the drop (only the four bigger
  consoles + the tiller). Minimal tiller-only helm? Share a console? No readout until upgraded? (§7)

---

## 5. Resizable / free-drag / minimizable console windows

The console takes screen space, so — per the owner — it's a **window you drag and resize freely, remembered
per boat**, that you can **minimize** and reopen, clickable to work the controls (turn the key, flip
switches, swing the lever, tap the sounder to swap depth↔fish).

**Design (M2/M3, ui-ux):**
- **Host:** a screen-space uGUI window with a `RawImage` over a C#-filled `Texture2D` (`FilterMode.Point`),
  following the interactive **`BuyScreen`** recipe (its `EnsureEventSystem`), **not** the read-only
  `HudController`. Resizing scales the RawImage rect only — the native-res texture is not re-rendered, so
  resize is free. **Lock the window aspect to the rig canvas** (e.g. 600:510) or the hit-mapping skews.
- **Pointer → rig space** (a pure, unit-testable function): screen point → normalize by the displayed rect →
  × native canvas → **flip Y** → **subtract `TOPPAD`** → run the rig's own hit-tests
  (`driveFromPoint`/`sigFromOffset`/`wheelTurn`, `SW`, `slotBox`). The geometry constants come **out of the
  rig as data**, never hand-copied (ADR 0021 rule).
- **Coexistence (highest risk):** the console is **not** modal — it must **not** set
  `InteractionGate.IsBlocked` (you drive while it's up) and must **not** use a click-eating full-screen
  backdrop (eat only what it covers). But `DevFishingInput`/`PotDeckWorkController`/`TrapHaulController`
  read `Mouse.leftButton` **directly**, bypassing the EventSystem — a click on a lever would also cast/haul.
  They must consult `EventSystem.IsPointerOverGameObject()` (or a shared "pointer captured by a diegetic
  panel" flag) — a cross-lane Fishing change.
- **Persistence:** window position (as a **screen-fraction**), size, and minimized state are **per-boat UI
  preference**, kept in a UI-prefs store keyed by hull id — **not** in `SaveData` (which is sim/economy
  state; putting UI layout there bloats the save like the "don't save tide/weather" anti-pattern). Escalate
  to save state only if the owner later wants per-save-slot boat setups.

---

## 6. Build order (how it lands in M1)

1. **Foundation (done, no Unity):** the throttle model + tunables, the watch mapper + gate, the
   console/equipment data model, all unit-tested; the committed rig sources; ADR 0025 + this doc.
2. **Analog throttle live (your Unity machine):** wire `DevBoatInput` to the model; owner feel-check.
3. **The watch renders (ADR 0025 accepted):** the first live C# rig (or baked digits) + `gear.watch` asset;
   still shown always-on in M1 until the St Peters clock-hide task.
4. **One console end-to-end:** the fish-finder live renderer (the hardest render case — proves ADR 0025),
   then a full helm (e.g. `ConsoleRig`) as a draggable window with the lever wired to the throttle.
5. **Equipment + save v5:** the upgrade shop, the per-hull owned-instrument save (its own ADR), the reader
   gating each readout.

---

## 7. Open questions / rulings needed

1. **`boat.fishing_skiff` console** — no rig supplied. Tiller-only, shared console, or no readout until
   upgraded? (§4)
2. **Throttle notch counts** — default 4 ahead / 2 astern; confirm on the feel-check, and whether Up/Down
   should hold-repeat (`HoldRepeatPerSec > 0`) or stay one-detent-per-press.
3. **Dusk hour** — the watch's night flips at 19:00 by default; the global day-night `sunset` is 20:00. Keep
   them separate (the watch is a physical backlight, not the sky), or unify? A tuning question.
4. **Render approach (ADR 0025)** — approve live-C#-renderers (owner steer), and decide how much of a console
   is one live renderer vs. composited baked parts (prototype the fish-finder first).
5. **Compass as carried vs fitted** — the ruling is "only the watch is carried", so *all* compasses are
   boat-fitted. Confirm there is no hand-compass that reads heading off-boat (some fishing games let you
   carry one); if there is, it joins the watch as carried gear.
6. **Window persistence scope** — per-boat UI-prefs (recommended) vs. per-save-slot (its own ADR).

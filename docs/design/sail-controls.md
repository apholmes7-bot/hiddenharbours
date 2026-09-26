# Hidden Harbours — Sail Controls (the sloops: helm, sheets, stations and the motor)

> **Status: RULED 2026-09-25 (D6):**
> - helm trim first, with the sails drawn and auto-trim on by default;
> - the mast (the stations) second, in its own charter later;
> - the wind-arrow and sloop-fuel bugs logged here (§K).
>
> This doc builds nothing. Each step is chartered separately (§10).
>
> The design of record for how the Sloop 30 and the Sloop 88 are sailed. It's subordinate to
> [`../vision-and-pillars.md`](../vision-and-pillars.md) (CANON). Siblings:
> - [`helm-audit.md`](helm-audit.md): the helm audit, whose step A6 this is, and D1–D6;
> - [`boats-and-navigation.md`](boats-and-navigation.md): the boats and their handling;
> - [`fuel-and-refuelling.md`](fuel-and-refuelling.md): the fuel burn;
> - [`../art/briefs/helms-pass.md`](../art/briefs/helms-pass.md): the brief, whose §2.3.6 is the Sloop 88's helm.
>
> **Pillars served:**
> - **P1:** the wind is read in the sails, not in a number.
> - **P4:** auto-trim first, hand trim as the skill path.
> - **P5:** a mis-trimmed or over-canvassed boat pays for it.

**Who owns what:**
- the sail code: gameplay-systems;
- the rigs: art-director;
- the bindings table: lead-architect;
- the wind needle: ui-ux.

Every re-ruling is the owner's.

---

## R. The owner's ruling (2026-09-25), and what it leaves standing

**Ruled:**

| Question | The answer |
|---|---|
| The first slice (§10, step 4) | **Helm trim first:** the helm sheet rows, the trim term, the sails drawn (S2 is in the slice), the needle fix, and the guards. **Auto-trim is on by default.** |
| The stations: hoist and furl at the mast (§3.2; §10, steps 5–6) | **Second, in their own charter, later.** It waits on the sloops' deck promotion (step 5: world-content and art-pipeline). It carries the set term, what leaving the helm does (§3.3), and the re-ruling of (M) that stations need (§5.2). |
| The two bugs | **Logged** (§K). |
| A sloop helm card (§11) | **Ruled by D3:** the Sloop 88's helm is sixth in the new-helm order. The Sloop 30 gets nothing in this pass. Her small engine panel waits on the engine run state. |

**Not named in the ruling.** These stand as the scope below proposes. Each step's charter confirms them, or the owner re-rules:

| Question | The standing proposal | Where |
|---|---|---|
| How much auto-trim gives away | the rig's own 0–4% of speed (0–3.5% on the 30, 0–3.9% on the 88); nothing invented | §4.1 |
| The keys and the pad | 1 or `[` / 2 or `]` eases and hardens the main; 3 or `;` / 4 or `'` the jib; 5 or `\` toggles auto-trim; J is the 88's staysail. Pad: RT/LT the main, the right stick Y the jib, LT+RT held 0.5 s for auto-trim. A touch on a sheet key turns auto-trim off. | §7 |
| A tack's cost in trim | none: a hand trim survives a tack | §3.1, §10 |
| Starting the motor | C: tap to run or stop, hold to crank (pad R3), on the engine run state | §5.1 |
| Motor-sailing | not in the first slice | §5.4 |
| (M′): the throttle runs only the engine | deferred to the mast charter, with its re-ruling of (M) | §5.2 |
| Leaving the helm under sail | she rounds up into the wind (today she stops dead); with the mast charter | §3.3 |
| Reefing's teeth | control teeth (weather helm, then a knockdown), not speed | §4.3, §10 step 7 |
| The lights | automatic, no switch. The sloops first need nav-light mounts in `HullMeshDef.Lamps`. D2 (a) is answered ([`helm-audit.md`](helm-audit.md) §3.4): the sloops' nav lights come with the lighting pass on each hull, like every hull's. | §5.6 |
| Save | nothing new. The sails are derived `stored` whenever she's anchored or moored, and PR 3 carries that. | §5.3, §9 |

---

## K. Known bugs (logged 2026-09-25)

Neither is fixed here. Each is fixed in the charter named.

### K1 · The wind needle shows the wrong wind

- **Where:** `UI/ApparentWindReadout.cs` :61-65, fed from `HudController.cs` :410 and :529 (the VS-19 needle).
- **What it does:** it's labelled apparent wind, but it shows the *relative true* wind: the true wind turned into the boat's frame, with her own speed left out.
  - The sails use the apparent wind, so on a sloop the needle and the sails disagree.
  - The nav cluster isn't gated by hull (`HudController.cs` :407-410), so a powerboat's needle shows the relative true wind too.
- **How far off** (arith, at 12 kn true; the audit swept every angle in 0.5° steps):

  | Hull | True wind angle | Apparent wind angle | Gap |
  |---|---|---|---|
  | the 30 | 45° | 32° | 13° |
  | the 30 | 90° | 62° | 28° |
  | the 30 | 119° (the widest) | 86.3° | **32.7°** |
  | the 88 | 90° | 46.5° | 43.5° |
  | the 88 | 123° (the widest) | 70.9° (the same from 8 to 14 kn true) | **52.1°** |

- **The fix:**
  - The needle reads the wind the sails see, `BoatController.LastSailWind.ApparentAngleDeg`, through a Core seam (rule 4).
  - Whether a powerboat's needle should also show apparent wind is settled in the same charter.
- **Owner:** ui-ux (the needle), with lead-architect's co-sign on the seam.
- **When:** at the latest, as the first slice's item 4 (§10).
- **The guard:** on each sloop, the needle's angle equals `LastSailWind`'s at several true wind angles.

### K2 · The sloops' fuel never burns

- **Where:** `Boats/BoatFuelTank.cs` :456-462, `EngineIsTurning` = `Propulsion == Engine && isActiveAndEnabled`. Both sloops are `Propulsion: 2` (Sail), so it's never true for them.
- **What that costs:** both sloops carry a diesel and an authored tank (`Sloop30.asset`, `Sloop88.asset` :43-45), and the level is saved per hull (`SaveData.HullFuel`, v14).

  | | Tank | Burn at full throttle | Endurance |
  |---|---|---|---|
  | the 30 | 60 L | 3.5 L/h | 17 h |
  | the 88 | 1200 L | 45 L/h | 26.7 h |

  - **Today it's latent,** because the sloops can't motor: `DriveSailHelm` writes throttle 0 every frame.
  - **Once PR 3's auxiliary (M) lands, a sloop would motor on a tank that never drains.**
- **The fix:** PR 3 feeds its LIVE predicate into the burn, or the engine run state ([`helm-audit.md`](helm-audit.md) §3.6) replaces the predicate.
- **Owner:** gameplay-systems, in PR 3. **PR 3 must carry it** (§10, step 2).
- **The guard:** a sloop under power drains her tank at the authored rate, and a sloop under sail burns nothing.

---

## Provenance: where the numbers come from

- **The audit.** This is the helm audit's step A6 (ui-ux, 2026-09-24, at `3fa9b606`, no Unity). Its model is `a6_sail.py`, with `a6_gap_sweep.py` for the needle.
  - Its tables are the `a6-*.csv` and `a6-*.txt` files named below.
  - They're the audit's evidence, kept out of the repo. They ship with the Sloop 88 job of the helms package.
- **What the model ports, and how it was checked:**
  - **The wind** is a bit-exact port of `WeatherModel.SampleWind`.
    - It reproduces the `WindUncapReachTests` AFTER line exactly: CoddleCove, 9 seeds, Glass 6.3 / Calm 24.9 / Light 40.0 / Moderate 12.5 / Lively 5.7 / Rough 6.1 / Gale 4.3 / Storm 0.3.
    - The region figures are over 200 seeds (1000–1199), a week each.
  - **The two rigs' pose laws** (fill, stall, heel) are ported from the sail kit.
    - They reproduce each rig's `HEEL.table` to 0.05° and its `TRIM_TABLE` to 0.05, once the sheets are rounded to 0.01 as the kit rounds them.
    - The game's `SailDrive.AutoTrim` is unrounded, so the game sits up to 0.33° of heel off the tables. That's noted, not a finding.
- **PROPOSED** marks anything that doesn't exist in the game: the trim term, the set term, the overpower term, the hold times and the keys. Nothing marked PROPOSED is a claim about the game.
- **"A4"** below is the audit's switch step, now [`helm-audit.md`](helm-audit.md) §3. Its engine run state is P2 there.
- **The scope below is the audit's, kept as it was written.**
  - "The charter" below means the audit's own charter. "PR 2" and "PR 3" are the sail helm's irons and auxiliary PRs, ruled 09-17.
  - Where it calls something "D6's" or the owner's to re-rule, §R gives the 09-25 answer, or names the charter the question passes to.
  - Numbers that need Unity are in §12, not guessed.

---

## 0. The short answer (as put to D6)

- **The 09-17 rulings leave room for the first slice.** Irons (c) and the auxiliary (M) both stand.
  Hand trim at the helm fits inside them: it is the trim term that ruling left open.
- **The first slice** is the sails drawn (S2), the two sheets as held keys at the helm, auto-trim on by
  default, and a trim term. That makes "tightening and loosening sails" real without reopening anything.
- **Hoisting and furling at a station** needs three things:
  - the sloops' deck promotion (they have no deck walk today);
  - a change to how leaving the helm works (today she stops dead);
  - a re-ruling of (M): a throttle that strikes the sails cannot live beside sails the player set by hand.
  So stations are the second step, and a D6 question.
- **Auto-trim should give away "a few per cent."** The rig's own `AUTO_TRIM`, read through the rig's own
  fill law, already gives away 0–4% of speed. No invented number is needed. The larger stake is getting it
  wrong: sheets eased on a beat cost the 30 44% of her speed and the 88 65%.
- **Overpowering is real, but speed is the wrong currency for it.**
  - Upwind, the 30 passes her drawn heel limit (24°) from 15¼ kn of true wind. That happens 5.7–9.5% of
    the time in the three regions.
  - But the polar saturates near hull speed, so an over-canvassed boat is at most 4% slower. Easing never
    pays in speed.
  - If reefing is to have teeth (P5), they must be control teeth: she rounds up, or is knocked down.
- **The click budget:**

  | Scheme | Inputs per passage |
  |---|---|
  | powerboat | 8 |
  | the sloop under (M) with auto-trim | 9 |
  | (M) plus hand trim | 14 |
  | stations with auto-trim | 21 |
  | stations plus hand trim | 26 |

  The owner's "more than the powerboat, not a chore" lands between 14 and 21.

---

## 1. Where the 09-17 rulings leave us

The rulings (the seat's copy `seat/v27-helm/ref/HANDOFF-2026-09-17-sail-helm.md`: the AMENDMENT of 09-17 16:26Z at
:167, rulings (1) and (2) at :173 and :176; the seat's ledger `hidden-harbours-pr-workflow.md` :2008-2034):

| Ruling | What it says | What the owner's new wish needs beyond it |
|---|---|---|
| **Irons = (c)** (PR 2) | A slow pay-off: `IronsPayOffTorque` on `BoatHullDef`, a pure static in Core `SailDrive`, only while she is drawn in irons. The auxiliary is the fast way out. | Nothing. Stations make irons more common, because an unmanned boat should round up (§3.3). Backing the jib is a possible later third way out (§6). |
| **The auxiliary = (M)** (PR 3) | The throttle off neutral strikes the sails and hands her to `ApplyEngineDrive`. Z returns her to sail. No new key. One LIVE predicate replaces `UsesEngineHelm(PropulsionType)` at the instance sites. | Hand trim fits. Stations do not: a hoist the player ground up by hand cannot vanish when W is pressed (§5.2). (M) also has no state for lying at anchor with the sails down (§5.3). |
| **Sheets by hand are OUT of PR 2** | Out until S2 draws the sails, or the owner rules a trim term. | This scope is that trim term (§4.1). S2 is still needed for the picture. |
| **(H) rejected** | The physics reading hoist and furl. It needed a binding, S2's picture and a reefing ruling. | Stations *are* (H)'s binding (§3.2). §4.2 prices the set term. §4.3 is the reefing evidence. |
| **(S) rejected** | Sails only: no astern, and the 88 could never leave a berth. | Nothing. Every scheme here keeps the motor. |
| PR 1 (the rudder) | Merged as #860. | Nothing. |

---

## 2. What exists: the rigs' contract against the code

The sailing sidecars are `docs/art/rigs/gameplay/sail/sloopIsoRig.sailing.json` and `sloop88IsoRig.sailing.json`
(CONTROLS, STATES, TRANSITIONS). The gameplay sidecars (`*.gameplay.json`) hold INTERACT.

**What the code does with a sloop today (3fa9b606):**

- **Steering is live.** A/D/←/→ and the left stick X set `_steer`, which becomes `RudderTorque` (`BoatController` :726) through
  `DevBoatInput.DriveSailHelm` (:319-329).
- **The sheets are held state that nothing reads.**
  - `_autoTrim = true`, sheets 1, `_hoist = 1`, `_furl = 0` (:106-108). The comment at :101-105 says so:
    "nothing in the physics reads them yet".
  - `SetSheets` (:322-327, which clears `_autoTrim`), `SetAutoTrim` (:330) and `SetSailSet` (:338-342) have
    **no callers**.
  - Auto-trim writes the sheets every step (:720), and nothing reads them.
- **The drive is the polar alone.**
  - `ApplySailDrive` (:697-727) reads `SailDrive.TargetSpeedKn` off the true wind and pushes against the
    hull's two-term linear resistance.
  - It never reads the throttle, the sheets, the hoist or the furl.
  - The propulsion branch (:604-609) is exclusive: sail, *or* engine, *or* oars.
- **The sloops cannot motor, stop or trim from the keys.**
  - `DriveSailHelm` writes throttle 0 every frame. W, S, Z and Space do nothing (`DevBoatInput` doc :27-32).
  - `StPetersBuilder.cs` :155-156 still says "cannot yet be steered, trimmed or stopped". Since #860,
    "steered" is stale (A6-6).
- **Nothing draws a sail or a heel.**
  - Both sloops land bare-poled: no boom, no sails (`StPetersBuilder.cs` :155).
  - The sails are the SailRigDef's `dynamicFaces` per pose (`HullMeshFleet.cs` :332-335). That is S2.
  - Heel is a render parameter in the sidecar (`ANIMATION.heel`), and nothing reads it.
- **They have no deck walk.**
  - `Visuals/SloopIso.asset` has `Deck: {fileID: 0}`.
  - With no deck walk to ask, `ControlSwitcher` falls back to the boat's origin (:381) and finds no rail
    (:1873). The helm as a station you walk up to (OnDeck → `TakesTheHelmOnThisPress` :564 → `TakeHelm`
    :1190, at :904-909) is a deck path, so none of the rigs' stations can be walked to yet.
- **Leaving the helm stops her dead.**
  - `LeaveHelm` (`ControlSwitcher` :1214-1216) disables the `BoatController` and calls `Stop()`.
  - `Stop()` (`BoatController` :468-480) zeroes her velocity and spin.
  - That is right for a lobster boat hauling at rest. It is wrong for a sloop whose player walks to the
    halyard (§3.3).

**Every contract entry: what drives it today, and what it would drive:**

| Contract entry | Rig | Drives it today | Would drive (tier, §3) |
|---|---|---|---|
| CONTROLS `enter_helm` [-1, 1] | both | The rudder: `_steer` → `RudderTorque` (:726) via `DriveSailHelm` (`DevBoatInput` :319). Entered by E through the generic path. | Unchanged. Helm tier. |
| CONTROLS `trim_main` [0, 1] | both | **Nothing reads it.** Auto-trim writes `_mainSheet` (:720). `SetSheets` has no caller. | Helm/MainSheet, a held axis. Feeds the trim term (§4.1) and the sail picture (S2). |
| CONTROLS `trim_jib` [0, 1] | both | **Nothing reads it.** Auto-trim writes `_jibSheet` (:720). | Helm/JibSheet, a held axis, always the leeward sheet. Same as above. |
| CONTROLS `hoist_main` [0, 1] | both | **Nothing reads it.** `_hoist = 1` (:108). `SetSailSet` has no caller. | The halyard-winch station (hold). Feeds the set term (§4.2). |
| CONTROLS `furl_jib` [0, 1] | both | **Nothing reads it.** `_furl = 0` (:108). | The furler station (hold). Feeds the set term. |
| CONTROLS `anchor` | 88 | Q at the helm (`AnchorInput`, ADR 0043 Helm/Anchor). The same verb works on every hull. | Q stays at the helm (§3.1). The windlass station is an optional walk-up. |
| CONTROLS `fold_platform` [0, 1] | 88 | Nothing. | The platform station (tap). A berth and anchorage verb, not a sailing control. |
| INTERACT `helm` / `helm_port`, `helm_stbd` | 30 / 88 | Taking the helm, as above. The sloops have no deck, so the reach points are not walkable. | Unchanged. The 88's two wheels share one helm. |
| INTERACT `mainsheet`, `mainsheet_port/stbd` | 30 / 88 | Nothing. | Played, not walked: the sheet keys work from the wheel. On the 88 the rig's `grind: main` turns the leeward mainsheet winch while the key is held. The 30 has no mainsheet winch (a block and a pedestal cleat), so nothing turns. |
| INTERACT `jib_winch_port/stbd`, `primary_port/stbd` | 30 / 88 | Nothing. | The same. The rig's `grind: jib` turns the leeward primary, picked from awa, while the key is held. |
| INTERACT `halyard_winch` | both | Nothing. | The deck station: hoist and lower (§3.2), plus the cover at hoist 0. |
| INTERACT `furler` ("Furling line") | both | Nothing. | The deck station: furl and unfurl. On the 30 it is a cockpit station 2.9 m from the wheel (§3.2). |
| INTERACT `windlass` | 88 | Nothing. The anchor is Q. | An optional walk-up "Drop / weigh anchor" (19.3 m from the wheel). |
| INTERACT `platform` | 88 | Nothing. | Lower and raise, at rest. |
| INTERACT `stove`, `locker`, `bunk` / `dining_table`, `chart_table`, `stove`, `bunk` | 30 / 88 | Nothing on the sloops (no deck, no interior walk). | Not sail controls. They belong to the interiors lane (the bunk's "Sleep · save" is the save point, §9). |
| STATES `sailing` (full sail) | both | The only state the physics knows: the polar is stated at full hoist. | The working state. The set term's 100%. |
| STATES `main_only`, `headsail_only` | both | Nothing. | Derived from hoist and furl. Drive scales with canvas (§4.2). |
| STATES `main_and_staysail`, `heavy_weather` | 88 | Nothing. | Derived from hoist, genoa furl and staysail. `heavy_weather` = main at 0.6 hoist + genoa furled + staysail set, and it is the 88's only shortened-main state. |
| STATES `motoring` | both | Nothing. Under (M), PR 3 would make it the state while the throttle is off neutral. | (M): off neutral. Stations: "sails stowed, engine running", derived. |
| STATES `stored` (cover on) | both | Nothing. | Derived at rest: moored or anchored (§5.3, §9). |
| STATES `platform_up` | 88 | Nothing. | The platform station. Independent of the sails. |
| TRANSITIONS `hoist_main` 0→1 | both | Nothing. | The halyard winch, held. The hold time is the cost (PROPOSED 6 s up, 4 s down). A partial hoist is the 30's reef. |
| TRANSITIONS `furl_headsail` 0→1 | both | Nothing. | The furler, held (PROPOSED 3 s each way). |
| TRANSITIONS `cover` false→true | both | Nothing. "Only at hoist 0 … suggest the halyard_winch station." | A tap at the halyard winch at hoist 0 (PROPOSED 2 s), or derived when stored. |
| TRANSITIONS `furl_staysail` 1→0 | 88 | Nothing. An "electric furler … no deck station modelled." | Helm/StaysailFurl, a switch at the wheel (§3.1). |
| TRANSITIONS `fold_platform` 1→0 | 88 | Nothing. | The platform station. |
| TRANSITIONS `door` 0→1 | both | Nothing on the sloops (the companionway `THRESHOLD`). | The interiors lane (ADR 0036), not sailing. |

---

## 3. Two tiers of control

The dividing line, in the owner's terms:
- **The helm tier** holds everything you do *continuously while you sail*: steering, the sheets, the motor.
  These are held keys, never a click per notch, so being engaged never costs a walk.
- **The station tier** holds everything that changes *which sails are up*: hoist, furl, cover, and the
  88's anchor and platform. These are rare, deliberate acts done a few times a passage, and the time spent
  away from the wheel is their cost. That time is where the teeth are (§3.3).

### 3.1 At the helm (held, continuous; no walking)

| Control | Binding (PROPOSED, §7) | Behaviour | Why here |
|---|---|---|---|
| **Steer** | Exists: A/D/←/→, left stick X | Live since #860. | — |
| **Main sheet** | Held axis: 1 ∨ `[` ease, 2 ∨ `]` harden · RT harden / LT ease | Rate-driven: a full throw takes about 2.5 s (a tunable). The trigger depth sets the rate. | Trimming is the continuous engagement. Walking to a winch for every puff would be the chore. |
| **Jib sheet** | Held axis: 3 ∨ `;` ease, 4 ∨ `'` harden · right stick Y | Always works the **leeward** sheet. The sidecar's `trim_jib` is side-free, and the rig picks the leeward winch from awa (`ANIMATION.grind`). | So a tack is steering alone. |
| **Auto-trim** | 5 ∨ `\` · pad: LT+RT held together for 0.5 s | **On by default.** Touching either sheet takes it off (`SetSheets` :322-327 already does this). One press hands both sheets back. | A player who never trims still sails well (§4.1). |
| **The motor** | (M): W/S detents off neutral, Z back to sail · A4's **C** start/stop (tap to run or stop, hold to crank) · pad R3 | See §5. Both sloops are diesel (`FuelGrade: diesel`, `Sloop30.asset` :44 and `Sloop88.asset` :44), so A4's glow-plug beat applies. | P2: a start is a felt step. |
| **Staysail furler (88)** | **J** · pad: d-pad ←/→ cursor + A | A switch that runs the electric furler. The rig models no deck station for it (TRANSITIONS `furl_staysail`). | P4: the 88 automates what the 30 makes you walk for. |
| **Anchor** | Q (exists) · pad X (A4) | Unchanged on both sloops. | See below. |

**Does a hand trim carry across a tack?** Yes, by construction.
- `BoatController` holds one `_jibSheet` and one `_mainSheet`, not a port and a starboard value.
- `SailDrive.AutoTrim` and the rig laws read |awa|.
- So a trim set on port tack is the same trim on starboard. The leeward winch changes in the picture; the
  sheet value does not.

That is the "not a chore" choice: a real crew re-trims the new leeward sheet on every tack, and this
model does it for them. If the owner wants a tack to cost a re-trim, the change is small: a per-side jib
value that resets to auto on the tack. That is D6's.

**How the player gets auto-trim back:**
- the AutoTrim key (5 ∨ `\`) or the pad chord;
- automatically when she is stored (at rest, §9);
- on load (the sheets are not saved).

Nothing else turns it back on behind the player's back.

**Q stays the anchor on both sloops.**
- The 30's rig has no windlass. A walk to the bow with a hand-hauled rode is the chore the owner ruled
  out.
- The 88's windlass is a foredeck station 19.3 m from the wheel, a 7.7 s walk each way. The helm key *is*
  its remote.
- The walk-up stays available as the same verb for a player who wants it.

### 3.2 At a deck station (walk there, hold interact; the time is the cost)

Distances are straight-line lower bounds from the nearer wheel (`a6-stations.csv`). Walking speed is 2.5 m/s
(`DeckWalkController` :63). "Hold" means hold E, or the pad's A, which the trap haul already reads as a
hold (`TrapHaulController` :259). All hold times are PROPOSED tunables.

| Station | 30 | 88 | Hold | Sets | Notes |
|---|---|---|---|---|---|
| **Halyard winch** (coachroof) | 3.28 m, 1.3 s, up 0.45 m | 5.34 m, 2.1 s, up 1.98 m | 6 s hoist, 4 s lower | `hoist` 0..1 | Releasing part-way leaves a partial hoist: **the 30's reef** (the rig has no reef points; 0.7 hoist = 85% canvas). On the 88, 0.6 hoist is the `heavy_weather` main. On the 30 the rig's `grind: main` turns this winch's handle (`hoist_main.grind_handle`); the 88's hoist has no grind handle in her rig. |
| **Furler** ("Furling line", cockpit sole) | 2.92 m, 1.2 s | 2.10 m, 0.8 s | 3 s each way | `furl` 0..1 | The 30's furling line runs to the port coaming cleat. **It is a station, but a cockpit one**: about a second from the wheel, and the helmsman never leaves the cockpit. It stays a station because the furl changes the sail plan (the same tier as the hoist), not the trim. |
| **Cover** (at the halyard winch) | as the halyard | as the halyard | 2 s tap | `cover` | Only at hoist 0 (the rig's own rule). Derived "on" when she is stored at rest (§9). |
| **Windlass** (foredeck) | none | 19.30 m, 7.7 s, up 1.27 m | as Q | the anchor | An optional walk-up. Q at the helm stays. |
| **Platform** (aft deck) | none | 4.29 m, 1.7 s | tap | `fold_platform` | At rest only. Not a sailing control. |
| *heavy_weather* (88) | — | — | — | — | Not a station: a composed STATE (halyard to 0.6 + furler + the staysail switch). |

**A trip helm → halyard → furler → helm:** 2.84 s of walking on the 30, 4.69 s on the 88. Halyard to furler
is 0.90 m on the 30 and 4.28 m on the 88.
- A passage makes two such trips (hoist + unfurl out, furl + lower + cover in). With the proposed holds
  (18 s), that is **about 24 s away from the wheel on the 30 and about 27 s on the 88**.
- These are the rigs' geometry. The sloops have no deck walk yet, so this is not a measured route (§12).

### 3.3 The teeth of leaving the helm

**Today:**
- `LeaveHelm` disables the controller and zeroes her velocity (§2). A sloop whose player steps away stops
  dead, which a sailing boat cannot do.
- The unmanned gate in `DevBoatInput` (:323-327: `RowingStationManned` false, so the rudder is written to
  centre) never gets a chance, because the controller is already off.

**What stations need** (gameplay-systems', PROPOSED):
- For a hull with a sail plan, leaving the wheel keeps the `BoatController` running with the rudder
  centred.
- The powerboats keep today's `Stop()`: hauling at rest depends on it.

**What she then does** is a design choice, because nothing models weather helm:

| Behaviour when unmanned | What the player comes back to | Verdict |
|---|---|---|
| **Sail on** (a centred rudder and today's drive) | She holds her heading at 5–7 kn for 24–27 s: 70–85 m, and possibly the rocks. | Harsh near a coast, and not what a real sloop does. |
| **Round up** (PROPOSED: a small torque toward the wind, growing with the drive or heel, only while unmanned) | Irons: the luff flutters, the boom wanders (both in the rig's `ANIMATION.loop`) and she loses way. She comes out by the ruled slow pay-off (c) or the motor (M). | **Recommended.** It is what a sloop with weather helm does. It makes a hoist easier (a head-to-wind main does not fill). It is P5 with a cozy floor: you lose ground, not the boat. |
| **Bear away** | She runs off downwind and gathers speed. | Wrong for a sloop, and the most dangerous. |

- **Lashing the helm** (hold the current rudder instead of centring) earns a place only if the owner wants
  sails worked while she keeps sailing: reefing mid-passage without rounding up. With the round-up, a
  reef is done head-to-wind, which is how it is really done. Not in the first slices.
- **Heaving to** (jib backed, helm lashed to windward: a parked boat) belongs with backing the jib (§6).
  Later.
- **Crew who trim or steer for you** are P4, later. The 88's automation (staysail switch, windlass remote)
  is the first rung of that ladder.

---

## 4. Make trim and set pay

### 4.1 Trim: the open term

**The PROPOSED term**, from the sidecars' own laws, with no invented curve:

- Each sail's **fill** = clamp01((aoa − 5)/13) · clamp01(aws/6).
- Its **stall** = clamp01((aoa − 40)/40).
- Its angle of attack comes from awa and its sheet, as the rigs pose it.
- **Drive** D = Σ areaᵢ · fillᵢ · (1 − 0.5·stallᵢ), over the full sail area.
- Speed comes from the polar at an equivalent wind of tws·√(D/D_ref), where D_ref is auto-trim at full sail.
  So a player who never touches a sheet sails **exactly today's polar**.

**What auto-trim gives away** (`a6-trim.csv`, `a6-trim-speed.csv`):

| | The 30 | The 88 |
|---|---|---|
| Drive given away, by sail area (by the rig's heel weights) | up to 7.46% (4.04%) | up to 7.30% (5.54%) |
| Where | awa 26–102 only | awa 26–102 only |
| Best hand trim beats auto, in speed | 0 to 3.5% | 0 to 3.9% |
| The same at 12 kn true | +1.1% at twa 45–110, 0 downwind | +0.2% (twa 45) to +3.9% (twa 110–135), 0 at 150+ |

- **The cause is one line of the rig.** `AUTO_TRIM` sets the headsail at an angle of attack of 16°, where the
  fill law gives 0.846, not the full fill `AUTO_TRIM.meaning` claims (A6-5). The main loses nothing.
- **Mis-trim is the real stake** (at 12 kn true, `a6-mistrim.csv`):

| Case | The 30 | The 88 |
|---|---|---|
| Both sheets eased right out, beat (twa 45) | −44% | −65% |
| Both eased, beam reach (twa 90) | −44% | −67% |
| Both hardened right in, beat | +1.0% | +0.2% |
| Both hardened, beam reach | −2.8% | +1.1% |
| Both hardened, broad reach (twa 135) | −13% | −27% |
| Both eased, broad reach | 0% | −16% |

  The fill and stall law has a wide plateau (aoa 18–40°). Over-sheeting is forgiving until the wind is
  aft; easing too far is punished hard. That fits "cozy but with teeth": a player who hand-trims must
  watch the luff.

**The three answers the charter asks for:**

| Option | What it means | Verdict |
|---|---|---|
| Nothing given away (feel only) | Auto equals the best trim. | Hand trim only risks loss, so nobody touches a sheet twice. |
| **A few per cent (attention pays)** | Auto as the rig authored it: 0–4%, from the rig's own law. | **Recommended.** Nothing is invented. If the owner wants attention to pay more, auto's headsail angle becomes a Def tunable (Rule 6), not a code change. |
| No auto-trim (a sim) | Every sheet is the player's. | Every passage becomes −44% until trimmed. A chore for most players. Could be an "expert" option later. |

### 4.2 Set: drive scaled by canvas

**The PROPOSED set term:** multiply D by the canvas each STATE sets (`SAIL_PLAN` areas; the 30's reef is a
partial hoist). Speed follows through the same equivalent wind (`a6-set.csv`):

| Boat | State | Canvas | Speed vs today: twa 52 / 90 / 135 at 12 kn, then twa 52 at 20 kn |
|---|---|---|---|
| 30 | full sail | 100% | 100 / 100 / 100 / 100 |
| 30 | reef: hoist 0.7 | 84.5% | 97 / 97 / 97 / 99 |
| 30 | reef: hoist 0.7 + furl 0.3 | 70% | 94 / 94 / 94 / 97 |
| 30 | deep reef: hoist 0.5 + furl 0.5 | 50% | 87 / 87 / 87 / 94 |
| 30 | main_only | 51.5% | 89 / 89 / 88 / 95 |
| 30 | headsail_only | 48.5% | 85 / 85 / 86 / 92 |
| 88 | full sail | 100% | 100 / 100 / 100 / 100 |
| 88 | main_and_staysail | 71.7% | 87 / 86 / 88 / 99 |
| 88 | heavy_weather | 50.7% | 73 / 72 / 70 / 97 |
| 88 | main_only | 52.6% | 77 / 75 / 75 / 98 |
| 88 | headsail_only | 47.4% | 67 / 66 / 69 / 96 |
| 88 | all three set (not a STATE) | 119.1% | 101 / 102 / 106 / 100 |

- Short canvas costs real speed in a moderate breeze, and almost nothing in a strong one. That is the
  sailor's intuition.
- "All three" on the 88 (genoa + staysail + main) is not in the rig's STATES. The staysail sits in the
  genoa's shadow, and the rig only draws it with the genoa furled. The game should not allow it.

**(H)'s three, against this scope:**

| What sank (H) on 09-17 | Where it stands |
|---|---|
| A binding | **Answered by the stations.** Interact exists, so the only new key is the 88's staysail switch. But the stations need the deck promotion first (§10). |
| S2's picture | **Still needed.** Nothing draws a sail today. |
| A reefing ruling | **The evidence is §4.3.** The owner rules what a reef buys: control, not speed. |

### 4.3 Overpowered (P5): the regions do blow that hard

**Wind by region** (200 seeds × one week; `a6-wind.csv`, `a6-wind-thresholds.csv`):

| Region | Glass / Calm / Light / Moderate / Lively / Rough / Gale / Storm (% of time) | Mean / p90 / p99 / max (kn) | ≥12 kn | ≥15 | ≥18 | ≥20 | ≥25 |
|---|---|---|---|---|---|---|---|
| CoddleCove | 5.1 / 26.7 / 41.4 / 13.7 / 5.9 / 5.0 / 2.1 / 0.3 | 6.6 / 13.5 / 24.2 / 32.2 | 12.6% | 8.0% | 4.7% | 3.2% | 0.8% |
| NineMileCreek | 5.6 / 28.8 / 42.1 / 12.5 / 5.7 / 4.2 / 1.1 / 0.0 | 6.1 / 12.2 / 21.6 / 29.0 | 10.5% | 6.0% | 3.1% | 1.8% | 0.2% |
| StPeters | 5.0 / 24.5 / 40.2 / 15.2 / 6.0 / 5.5 / 2.7 / 0.9 | 7.1 / 14.8 / 26.9 / 35.5 | 14.4% | 9.8% | 6.3% | 4.6% | 1.7% |

**Heel demand at auto-trim, full sail, sailing at the polar** (`a6-heel.csv`). The rig's `HEEL` law is
evaluated in the apparent wind she makes. The drawn limit (`HEEL_MAX`) is 24° on the 30 and 20° on the 88.

| Boat | Point of sail | Passes the drawn limit from (true wind) | CoddleCove | NineMileCreek | StPeters |
|---|---|---|---|---|---|
| 30 (24°) | twa 45, close-hauled | 15.25 kn | 7.7% | 5.7% | 9.5% |
| 30 | twa 60 | 15.55 kn | 7.3% | 5.4% | 9.1% |
| 30 | twa 90, beam reach | 18.05 kn | 4.6% | 3.0% | 6.3% |
| 30 | twa 120 | 22.0 kn | 2.0% | 0.9% | 3.2% |
| 88 (20°) | twa 45 | 15.3 kn | 7.6% | 5.6% | 9.4% |
| 88 | twa 60 | 15.5 kn | 7.4% | 5.4% | 9.2% |
| 88 | twa 90 | 19.4 kn | 3.6% | 2.1% | 5.0% |
| 88 | twa 120 | 26.6 kn | 0.4% | 0.1% | 1.1% |

**The wind to ease or reef:**
- **The 30:** from about 13–15 kn true upwind. She reaches 15° at 11.5 kn, 20° at 13.7 kn and her 24° limit at
  15.3 kn. On a beam reach it is about 18 kn, and broad about 22 kn.
- **The 88:** about 15 kn upwind (15° at 12.4 kn, 20° at 15.3 kn), 19.4 kn on a beam reach, and 26.6 kn broad.
- **So reefing is not decoration by exposure.** Upwind, the full sail is past its drawn heel 6–10% of the
  time, and St Peters blows a gale or worse 3.6% of the time.

**But not in speed** (the PROPOSED penalty: drive × (1 − 0.5·clamp01(heel demand / HEEL_MAX − 1)),
`a6-overpower.csv`):
- An over-canvassed 30 on auto-trim is at most **4.0%** slower than today's polar (twa 45 at 25 kn). The 88
  is at most 1.6% slower.
- Easing or reefing **never** makes either boat faster. The polar saturates near hull speed (7.41 kn and
  12.6 kn), so there is no speed to win back.
- Auto-trim at full sail is never more than 1.1% behind the best that any set and any trim could do
  (`cost`, `a6_sail.py` :588).

**Recommendation (D6):** give the reef control teeth, not speed teeth. All PROPOSED, all gameplay-systems':
1. **Weather helm past the limit.** The heel demand over `HEEL_MAX` adds a turning torque toward the wind.
   The player must hold more rudder, and in a gust she rounds up into irons (the ruled (c) machinery). This
   reuses PR 2's irons handling and is felt without a number.
2. **A knockdown in a gust.** Past a second threshold she is laid over: way lost, gear shifted, the hold
   spilled. P5 at its sharpest. Needs S2's heel picture, and the fishing hold's rules.
3. **Gear damage** (a blown sail, a broken fitting) is later, with the economy lane (repair costs).

Easing a sheet or reefing removes the torque. The reward is a boat that goes where you point her. The
feel of this is Phase C (§12).

### 4.4 Feedback that reads without a number

| Cue | Source | State today | What it tells the player |
|---|---|---|---|
| **Cloth flutter at the luff** | Sidecar `ANIMATION.loop`: "cloth flutter at the luff, the in-irons boom wander" | Nothing draws a sail (S2) | Eased too far (fill → 0), or in irons. Motion, not colour. There are no telltales in the rig yet; they would be an art-director add. |
| **Sail depth** (fill and stall) | The rigs' pose laws | Needs S2 | Full and drawing, or flat and stalled. |
| **Heel** | Sidecar `HEEL` law; `ANIMATION.heel` "a render param … follows aws/awa" | Nothing draws it (the hull is a static bake) | Overpowered: the deck tilts. Shape, not colour. Needs the presenter or S2. |
| **The winch handle** | `ANIMATION.grind`: the jib turns the leeward primary on both; the main turns the 88's leeward mainsheet winch and the 30's halyard winch (a hoist) | Nothing | Which sheet you are working, or that the main is going up. |
| **The HUD's wind needle** (VS-19) | `UI/ApparentWindReadout.cs` | **Shows the relative TRUE wind, not apparent** (:61-65; `HudController` :410, :529; A6-7) | At 12 kn true the sails see a different wind: the 30 at twa 45 → awa 32 (a 13° gap), twa 90 → awa 62 (28°); the 88 at twa 90 → awa 46.5 (43.5°). The widest gap is on a broad reach: **52.1°** on the 88 at twa 123 (awa 70.9, the same from 8 to 14 kn true), and 32.7° on the 30 at twa 119 (`a6-gap-sweep.txt`, every angle in 0.5° steps). The needle must read the wind the sails see: `BoatController.LastSailWind.ApparentAngleDeg`, through a Core seam (Rule 4). That is ui-ux's own item and needs a Core interface. |
| **Sound** | none | No sail audio exists | Luffing canvas, winch pawls, the halyard's rattle, the water's rush rising with speed. The audio lane's. |

Every cue is motion, shape, angle or sound. None relies on colour alone.

---

## 5. The motor

### 5.1 Start and run state

- **Take A4's engine run state.** A Core run flag (A4: `IEngineRun`: Off / Acc / Run, with Start momentary)
  and **C** to start and stop (tap to run or stop, hold to crank). Both sloops are diesel, so A4's glow-plug
  beat applies.
- **Under (M), W or S off neutral with the engine stopped does nothing** except prompt the start. It should
  not strike the sails into a dead engine and leave her drifting.
- **Fuel is authored and saved, but a sloop never burns it:**
  - Her tank is authored: the 30 carries 60 L and burns 3.5 L/h at full throttle, 17 hours of motoring
    (`Sloop30.asset` :43-45). The 88 carries 1200 L at 45 L/h, 26.7 hours (same lines). The levels are
    saved per hull (`SaveData.HullFuel`, v14).
  - `BoatFuelTank.EngineIsTurning` (:456-462) is `Propulsion == Engine && isActiveAndEnabled`. The sloops are
    `Propulsion: 2` (Sail), so **under (M) as ruled, a sloop would motor on a tank that never drains**.
  - **PR 3 must feed its LIVE predicate into this burn**, or A4's run flag replaces it (A4 already proposes
    that). This is a finding for PR 3's Phase A, not a fix here.

### 5.2 What becomes of (M)'s instant strike if the sails are set at stations

(M) ties the sail set to the throttle: off neutral means struck, neutral means set. Stations tie it to the
player's hands. The two cannot both be true:

| Option | What happens | Verdict |
|---|---|---|
| Keep the strike beside stations | W drops sails the player spent 9 s hoisting, and Z raises them with nobody at the winch. | Incoherent. |
| **(M′): the throttle runs only the engine; the sails stay as set** | Hoisting under power, motoring with the main up, and furling in the lee all fall out naturally. | **Recommended with stations.** But it means motor-sailing exists by default, so it must be priced and capped (§5.4). This is the re-ruling D6 is asked for. |
| Refuse the throttle while any sail is up | "Lower the sails first." | A punishment, not a choice. |

- **The path that keeps (M) first:** the first slice (helm sheets and the trim term) changes nothing about
  (M).
- (M′) arrives only with the stations, and only if the owner re-rules.

### 5.3 A gap in (M) as ruled: she cannot lie at anchor with the sails down

- Under (M), neutral (Z) means "back to sail". So a sloop that motors into an anchorage and goes to neutral
  has her sails set again, flogging at anchor. The rig's `stored` state is unreachable.
- The click count (`a6-clicks.csv`) had to anchor her *without* Z: the throttle left in gear and the
  engine stopped with C. That is the wrong habit to teach.
- **The smallest fix, inside (M):** she is `stored` whenever she is anchored or moored. This is derived
  from the anchor and mooring state, and nothing is saved. PR 3 should carry it.

### 5.4 Motor-sailing

- **It does not exist today.** The drives are exclusive (:607-612).
- **A naive sum over-speeds her.** Both drives are a constant thrust against the same linear resistance
  (the engine :662-663; the sail :712-715), so summing the thrusts adds their speeds:
  - the 30 on a beam reach in 12 kn of true wind: 6.4 kn of sail (her polar) plus about 5.5 kn of engine
    = about 11.9 kn, against a 7.41 kn hull speed;
  - so motor-sailing needs a cap at hull speed (the polar's `HullSpeedKn`), or a wave-making term.
- **It gives no pointing gain in this model.** The sail drive is zero inside the no-go (twa < 45°), so the
  engine alone does the windward work, sails or not.
- **Worth it?** Only as a consequence of (M′): the picture of a sloop motoring out with her main up, and no
  need to strike for a short stretch of power.
- **Its cost:** the summed branch, the cap, and fuel burned with the sails up.
- Not in the first slice. With stations and (M′) it comes for free and must be capped.

### 5.5 Speed under power

This is PR 3's to price; noted, not priced. By the drive law the 30 motors at about 5.5 kn and the 88 at
about 10 kn. The 88's polar gives 6–9.5 kn in 10 kn of true wind, so she motors faster than she sails
(the ruling's own note).

### 5.6 The lights

- **Recommendation: automatic, no switch.** Lamps follow the drive state:
  - **masthead** (steaming) light on while the engine drives at night;
  - **anchor light** on at anchor;
  - sidelights and stern under way.
- The mechanism exists: `HullLampKind.Masthead` (`Core/Boats/HullLamp.cs` :32; the enum is at :19).
- Why automatic: a switch the player must remember at dusk is a chore with no decision in it. It would
  also contradict A4's rule of no new key for NAV.
- **The prerequisite:** the sloops need their nav-light mounts authored in `HullMeshDef.Lamps` (:293). The
  rigs draw no nav lights. An art-director and gameplay-systems item.

---

## 6. In irons (ruled: (c)); how it meets the new controls

- **(c) stands.** A slow pay-off (PR 2's `IronsPayOffTorque`), and the motor is the fast way out.
- **Steady-state irons does not happen.** No polar cell has awa < 25° at twa ≥ 45° (the 30 has 4 cells under
  25°, the 88 has 14, all inside the no-go). The rigs draw irons only in transients: tacks, round-ups, a
  player who turns into the wind.
- **Backing the jib** (holding the jib to windward to push her bow off) is a real third way out.
  - PROPOSED: a multiplier on PR 2's pay-off torque while the windward jib sheet is held.
  - It needs S2's picture (a backed jib drawn) and a verb (hardening the jib while in irons could be the
    verb).
  - Worth adding after S2. It does not reopen (c), it sits on top of it.
- **What a player who left the wheel comes back to:**
  - with the round-up (§3.3), irons: sails flogging, way off, the boom wandering (all in the rig's loop);
  - then out by the pay-off, the backed jib (later), or the motor.
  - That is the lesson the sea teaches: leave the helm and she heads up and waits.

---

## 7. Keys and gamepad

**Today, and under (M):**

| | Keyboard | Pad (read today) |
|---|---|---|
| Steer | A/D/←/→ (eased), `DevBoatInput` :27-32 | left stick X (analog) |
| Under sail | W, S, Z, Space do nothing | d-pad ↑/↓ (detents) and B (neutral) do nothing |
| Under (M) (PR 3) | W/S off neutral: motor · Z: back to sail | d-pad ↑/↓ · B |
| Anchor / interact / searchlight / instruments | Q / E / L / K (ADR 0043 :112-119) | A4 proposes X / A (hold) / Y / — |
| Camera zoom | the mouse wheel | LB/RB (`CameraZoomInput` :86-87) |
| Shell | Esc | Start (`ShellPresenter` :98) |

**Free, checked by grep on this box:**
- The digit row, `[`, `]`, `;`, `'`, `\` and J are bound nowhere in `Code/` or any `.inputactions`.
- The pad's triggers and right stick are read nowhere.
- A4 already reserved LT/RT for the sheets.

**The proposed rows for ADR 0043's table.** A proposal for lead-architect: `HelmIntents` has not landed on
main, so these are table rows, not code.

| Map | Action | Type | KeyboardMouse | Read by |
|---|---|---|---|---|
| **Helm** | MainSheet | Value/Axis | `1DAxis`: − (ease) = 1 ∨ `[`, + (harden) = 2 ∨ `]` | the sail helm → `BoatController.SetSheets` |
| | JibSheet | Value/Axis | `1DAxis`: − = 3 ∨ `;`, + = 4 ∨ `'` | as above (always the leeward sheet) |
| | AutoTrim | Button | 5 ∨ `\` | → `BoatController.SetAutoTrim(true)` |
| | Engine | Button (tap / hold) | C: tap to run or stop, hold to crank (A4's row) | A4's run state |
| | StaysailFurl | Button | J (the 88 only; nothing on the 30) | the 88's furler switch |

- **Two clusters, one per steering hand, in the table's own ∨ style** (like W ∨ ↑). A player steering
  with A/D trims with the right hand on `[ ] ; '`. A player steering with the arrows trims with the left hand
  on 1–4.
- The digits are the obvious home for a future hotbar. If one comes, the bracket cluster alone is enough.

**Pad (for ADR 0043 PR 2, the pad column):**

| Action | Pad | Clash check |
|---|---|---|
| MainSheet | RT harden, LT ease (analog: the trigger depth sets the rate) | Free. A4 reserved them. |
| JibSheet | right stick Y (up hardens) | Free: no right-stick read anywhere. |
| AutoTrim | LT + RT held together for 0.5 s | The main's net rate is zero while both are held, so nothing moves before the chord fires. |
| Engine | R3 (hold) | A4's. |
| StaysailFurl (88) | d-pad ←/→ switch cursor + A | A4's cursor pattern. |
| Stations | A (hold) | The haul pattern (`TrapHaulController` :259). |

**The maps by drive state:**

| | Under sail | Under power | Motor-sailing (only with (M′)) |
|---|---|---|---|
| W/S (d-pad ↑/↓) | (M): leave neutral = strike and motor. (M′): the throttle | the throttle detents | the throttle |
| Z (B) | (M): nothing (already sailing). (M′): neutral | (M): back to sail. (M′): neutral | neutral |
| Sheets (1–4 / `[ ] ; '` · RT/LT, right stick) | live | inert (the sails are struck) | live |
| AutoTrim (5 / `\` · LT+RT) | live | inert | live |
| C (R3) | start or stop the auxiliary | start or stop | start or stop |
| J (88) | staysail | inert | staysail |
| Q, E, L, K | unchanged | unchanged | unchanged |

**One alternative, only on an (M) re-ruling:** under (M′) the throttle keys only matter while the engine
runs. W/S could then trim *both* sheets together when the engine is off: one "power" axis, the fewest
inputs of any trim scheme. It is mode-dependent, so it is harder to learn. Offered for D6, not
recommended first.

---

## 8. The click budget

The passage, as the charter sets it: leave the berth under power, hoist, unfurl, a beat with two tacks, a
reach, furl, lower, motor in, anchor. Steering, walking and mooring lines are not counted. The steps are
in `a6-clicks.csv`.

| Scheme | Leave | Hoist | Unfurl | Beat (2 tacks) | Reach | Furl | Lower + cover | Motor in | Anchor | **Total** |
|---|---|---|---|---|---|---|---|---|---|---|
| Powerboat (A4 switches) | 3 | – | – | 0 | 0 | – | – | 1 | 4 | **8** |
| Sloop today (no sail verbs) | 1 | – | – | 0 | 0 | – | – | 0 | 2 | **3** |
| Sloop, (M) as ruled, auto-trim | 3 | 1 | 0 | 0 | 0 | 1 | 0 | 1 | 3 | **9** |
| Sloop, (M), hand trim (derived: the (M) row plus the hand-trim deltas) | 3 | 1 | 0 | 2 | 2 | 1 | 0 | 1 | 4 | **14** |
| Sloop, stations + (M′), auto-trim | 3 | 3 | 3 | 0 | 0 | 2 | 3 | 3 | 4 | **21** |
| Sloop, stations + (M′), hand trim | 3 | 3 | 3 | 2 | 2 | 2 | 3 | 3 | 5 | **26** |

**What each row presses:**
- **Powerboat:** E, C, W · S · Z, Q, C, E.
- **Sloop today:** E. She cannot motor, so she *sails* off the berth, or sits in irons if the berth faces
  the wind. Then Q, E.
- **(M):** E, C, W (off neutral strikes) · Z (sets everything at once) · W strikes · S · Q, C, E. Not Z at
  anchor: §5.3.
- **Stations:**
  - leave: E, C, W;
  - hoist: Z, E (leave the wheel), hold E at the halyard;
  - unfurl: hold E at the furler, E (take the wheel), C (engine off);
  - furl: E, hold E at the furler;
  - lower: hold E at the halyard, E for the cover, E (take the wheel);
  - motor in: C, W, S;
  - anchor: Z, Q, C, E.
- **Hand trim:** harden both sheets once for the beat (a hand trim carries across both tacks), ease both
  for the reach, and one press to hand them back to auto. That last press is 0 if auto returns on its own
  at rest (§3.1). A gust adds 2 (ease both in the puff). That is not in the totals.

**Where the models land against the owner's target** (more than the powerboat's 8, not a chore):
- **(M) with auto-trim (9) is barely more** than the powerboat. It is the floor, and it is what PR 3 alone
  delivers.
- **(M) + hand trim (14)** is the first slice's ceiling. Every extra input is optional and pays (§4.1).
- **Stations with auto-trim (21)** is the full ritual: about 2.6 times the powerboat, plus about 24–27 s away
  from the wheel per passage. Every added input is a sail-handling act a sailor would recognise, so it is
  engaged, not busywork, *if* the holds are short and leaving the wheel costs ground, not the boat (§3.3).
- **Stations + hand trim (26, plus 2 a gust)** is the "sim" end, for a player who wants it. Auto-trim keeps
  it optional.

---

## 9. Rule 5: what is saved and what is derived

| State | Saved? | Where and how |
|---|---|---|
| Main and jib sheet | **No, transient** | Auto-trim on load (the default at :106-107). |
| Auto-trim flag | **No, transient** | On at load. |
| Hoist, headsail furl, staysail furl | **Derived** | `stored` whenever she is anchored or moored. The only save point aboard is the bunk ("Sleep · save"), so she is at rest when saved. |
| Cover | **Derived** | On iff stored. |
| Engine run state | **No, transient** | Off on load (A4's run flag is transient too). |
| Fuel | **Already saved** | `SaveData.HullFuel` (v14), unchanged. |
| Platform (88) | **No, transient** | Down on load: 1, where the rig's `fold_platform` transition starts ("1 down, 0 up"). |

- **If the owner wants a set to survive mid-passage** (for example a save and quit while under way, if
  that is ever allowed), the precedent is `HullFuel`:
  - a per-hull `HullSailSet` list;
  - a bump `SaveMigration.CurrentVersion` 14 → 15;
  - a lead-architect co-sign.
- Nothing in the first slice or the stations needs it.
- Tide, wind and weather stay recomputed (Rule 5 as written).

---

## 10. Order, dependencies and the first slice

| # | Step | Owner | Waits on | Gives the owner |
|---|---|---|---|---|
| 1 | **PR 2: irons (c)**, `IronsPayOffTorque` | gameplay-systems | nothing (chartered 09-17) | she can come out of irons |
| 2 | **PR 3: the auxiliary (M)**, the LIVE predicate, **plus the fuel burn (§5.1) and "stored at anchor" (§5.3)** | gameplay-systems (with its own gauntlet) | PR 2 · A4's run state (or C lands with it) | she motors, stops and anchors |
| 3 | **S2: the sails drawn** (SailRigDef `dynamicFaces` per pose, the heel param) | art-director + art-pipeline | the rig kit (landed) | trim and set have a picture; the stale-polar debt (A6-8) is re-baked here |
| 4 | **The first slice: "tightening and loosening sails"** (below) | gameplay-systems (the term), lead-architect (the rows), ui-ux (the needle) | 2 and 3 | the owner's wish, inside the 09-17 rulings |
| 5 | **The sloops' deck promotion** (the sidecars up to deck Defs, `VisualsBySidecar` rows, a walkable deck) | world-content + art-pipeline | the gameplay sidecars (exist) | the stations become places |
| 6 | **Stations + the set term + (M′)** (and `LeaveHelm` kept running for sail hulls, with the round-up) | gameplay-systems | 5 · **a D6 re-ruling of (M)** | hoist and furl by hand; reefing |
| 7 | **Overpower teeth** (weather helm past the limit, then the knockdown) | gameplay-systems | 3 and 6 · a D6 reefing ruling | P5 in the wind |
| 8 | **A sloop helm card** (§11) | art-director via the brief | D6 · D3's order | the diegetic panel for the auxiliary and the instruments |

**The smallest first slice** (step 4), everything inside (c) and (M):
1. Helm/MainSheet, Helm/JibSheet and Helm/AutoTrim rows, read by the sail helm into the existing `SetSheets`
   / `SetAutoTrim`. The setters exist; they only lack callers.
2. The trim term (§4.1) in Core `SailDrive`, as a pure static like PR 2's (one function of awa, aws, the two
   sheets and the rig's areas). Auto-trim at full sail reproduces today's polar, so nothing else moves.
3. S2's sails posed from the live sheets. The flutter and the depth are the feedback.
4. The VS-19 needle reading the apparent wind the sails see (§4.4).
5. Guards (qa-test): auto-trim at full sail equals today's polar in every cell; the mis-trim table
   reproduces; a hand trim survives a tack.

If S2 slips, items 1, 2, 4 and 5 can land first. But trim with no picture only reads through the
speedometer and the HUD, which the 09-17 ruling rightly refused. **Keep S2 in the slice.**

---

## 11. A sloop helm for the brief (D3 ruled the 88's sixth)

**What the rigs bolt to each helm today:**
- **The 30:** a single pedestal wheel with a compass binnacle on top (`sloopIsoRig.js` :1240, :1312; INTERACT
  `helm`, label "Wheel"). The mainsheet block and a pedestal cleat are within reach.
- **The 88:** twin pedestal wheels, each with an instrument pod and a compass on top (`sloop88IsoRig.js`
  :1303-1304; header :7). A plotter at the chart table faces aft (:1366, :1425).
- **Neither rig draws** an engine panel, a throttle or shift, a staysail switch, a windlass remote, or nav
  lights.

**Candidates for a helm card** (the diegetic rule: a device you can see and touch, never a HUD panel):

| Piece | 30 | 88 | Drives |
|---|---|---|---|
| Compass | on the binnacle (exists) | on each pod (exists) | the heading (exists) |
| Engine panel: key or start button, glow-plug lamp, oil and temperature alarms, tach | new (a small cockpit panel) | new (in the pod) | A4's run state |
| Throttle and shift (one lever) | new (pedestal side) | new (starboard pedestal) | (M)'s detents |
| Wind instrument (apparent) and log (speed) | optional | in the pods (the rig's "instrument pod") | `LastSailWind`, speed |
| Staysail furler switch | — | new (a pod button) | Helm/StaysailFurl |
| Windlass remote (up / down) | — | new (a pod button, or a handheld) | the anchor (Q) |
| Station prompts | the halyard winch, the furler | as the 30, plus the windlass and platform | the verbs already in INTERACT: "Hoist / lower the main", "Furl / unfurl the jib" (the 88: "the genoa"), "Drop / weigh anchor", "Lower / raise the platform" |

- The station prompts are world-space, at the fitting, as the trap haul's are. They are not a helm panel.
- **Whether a sloop helm card is needed at all** is D6's question. The 30's "helm" is a wheel and a compass,
  and a card for it would mostly show an engine panel the rig does not draw. My lean:
  - the 88 earns one (the pods exist, and her automation lives there);
  - the 30 gets only the engine panel, drawn small, when A4's engine state lands.

---

## 12. Corrections found, and what is left for Phase C

**Corrections** (small drifts logged, not stopped for):

| # | Where | What |
|---|---|---|
| A6-1 | `docs/art/rigs/gameplay/sail/README.md` | The "blocked upstream" bake paragraph is stale: the bake stopped refusing in S0 (`HullMeshFleet.cs` :337). |
| A6-2 | the charter cites `DevBoatInput` :27-33 | The sail doc is :27-32. |
| A6-3 | — | Withdrawn: the charter's `SetSheets` :318 is inside its doc comment (:316-327). Accurate. |
| A6-4 | the 09-17-era memory | Its τ ≈ 20 s predates the `linearDamping` term. The two-term law gives τ = m/k of 4.15 s (the 30) and 4.76 s (the 88). |
| A6-5 | the sailing sidecars' `AUTO_TRIM.meaning` | Says "full fill". The headsail sits at aoa 16°, fill 0.846. That is the whole of auto-trim's give-away (§4.1). |
| A6-6 | `StPetersBuilder.cs` :155-156 | "cannot yet be steered" is stale since #860. "trimmed or stopped" is still true. |
| A6-7 | VS-19 `UI/ApparentWindReadout.cs` :61-65 (`HudController` :410, :529) | Its "apparent wind" is the relative TRUE wind. The sails use apparent: up to 52.1° apart (the 88 at twa 123, 12 kn true; 32.7° on the 30 at twa 119). *Self-corrected in this session:* the first draft said "up to 43.5° at twa 90", the widest of the four angles `a6_sail.py` samples; `a6_gap_sweep.py` sweeps every angle. |
| A6-8 | `SailPolarDef` assets' HeelDeg column | From an older rig: up to 2.0° off the current sidecar on the 30 (twa 165, 25 kn: 12.7 vs 10.7; `DerivedFromRigSha256` b0cdd16b vs cfb2297c), and 1.2° on the 88 (twa 100, 20 kn). Speed, awa and aws match exactly, and nothing reads HeelDeg. This is the SailRigDef/S2 stale-polar debt: noted, not stopped for. |
| note | `SailDrive.AutoTrim` vs the kit | The game's auto-trim is unrounded, and the kit's tables use 0.01 sheets. At most 0.33° of heel apart. |

**Phase C** (needs Unity, on a granted slot; none of these are guessed above):
1. **Re-acceleration:** does she feel like τ ≈ 4–5 s after a tack or a round-up? The law says so. Only the
   editor says whether it feels right.
2. **The unmanned drift:** with the controller kept running and the rudder centred, what does she do in
   24–27 s near the St Peters breakwater? This decides between "sail on" and "round up" (§3.3).
3. **Heel and the round-up torque:** the size of the weather-helm torque that is felt but fair.
4. **Speed under power:** the 30 at about 5.5 kn and the 88 at about 10 kn by the law. PR 3 prices it; Phase C
   confirms it.
5. **The 88 in transient irons:** how long she hangs head-to-wind after a slow tack. She has 14 polar cells
   under 25° awa.
6. **The stations' real routes:** walking times on a promoted deck, against §3.2's straight-line lower
   bounds.

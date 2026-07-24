# Hidden Harbours — The Plan to M1

> **Status:** Working plan. Written 2026-07-24 against `main` @ `9df75c6`.
> **Canon:** [`../docs/vision-and-pillars.md`](../docs/vision-and-pillars.md) wins on any conflict, then
> [`../CLAUDE.md`](../CLAUDE.md), then this file.
> **What this is:** an honest audit of how much of M1 is already built, the reconciliation of the M1 spec with
> what the project actually became, and the sequenced, owner-decision-gated route to calling the vertical slice
> **Done**.
> **What this is not:** new design. Everything here phases work already specced in
> [`milestone-1-vertical-slice.md`](milestone-1-vertical-slice.md) and [`backlog.md`](backlog.md).

---

## 1. The headline

**M1 is roughly 80% built, and the 20% that remains is mostly not code.** The riskiest item in the whole
milestone — the dory force model (VS-09, flagged "the #1 risk, prototype first") — shipped and has been
owner-playtested repeatedly. What is left is the *finish*: two region scenes the owner must author, an audio
track that has no source assets at all, a HUD that reads as programmer text instead of a widget, a
localization seam that was never wired, and an acceptance pass nobody has run.

Three things block progress that no agent can unblock. They are listed in §5 and they should be answered
before the next wave of work starts.

---

## 2. Where we actually are (audited, not assumed)

Evidence: 617 C# files, 254 test files, 24 ADRs, 341 art PNGs, CI green on `main` (run 2026-07-24T14:08Z),
save schema at v4 with four migration steps.

### 2.1 Done — the M0 loop and most of M1's systems

| Item | State | Evidence |
|---|---|---|
| VS-01/02/03 bootstrap, intents, versioned save | **Done** | asmdef modules under `Code/`, `SaveData` v4, `SaveMigration` with 4 guarded steps |
| VS-04 clock + deterministic semidiurnal tide | **Done** | `Environment/TideModel.cs`, `GameClock.cs`, determinism tests |
| VS-05 EnvironmentService v1 — wind, current, sea-state, FORCES sample | **Done** | `EnvironmentSample`, `WindProfile`, `CurrentModel`, `WeatherModel` |
| VS-07 dory v0 + follow-cam | **Done** (superseded by VS-09) | `CameraFollow`, `CameraZoomPolicy` |
| **VS-09 dory force model v1 — the #1 risk** | **Done** | `BoatController` (thrust, anisotropic fore/aft drag, `WindExposure`, speed-scaled `RudderAuthority`), `SeakeepingForcesMath` |
| VS-10/12 FishSpecies schema + catch resolver | **Done** | `FishSpeciesDef`, `CatchResolver`, `CatchResolverTests` |
| VS-13/14 fishing interaction + polish | **Done** (rod-fishing v2 landing) | `RodFightSim`, `RodFightPresenter`, owner playtests 2026-07-23/24 (#277, #281, #283) |
| VS-15 buyer + supply/demand price | **Done** | `Market`, `MarketMath`, `SellPricing`, Cove + Greywick `MarketId` |
| VS-18 sell screen (functional) | **Done** (M0 bar) | `SellScreen`, `SellService` |
| VS-17 HUD v0 + wind/sea/compass/set-&-drift readouts | **Done** (functional) | `HudController`, `WindReadout`, `CompassReadout.SetAndDrift` |
| VS-20 Coddle Cove | **Done + committed** | `Greybox.unity`, ADR 0011 logic/visual split, `Refresh Cove Logic` |
| VS-23 art convention + import lock | **Done** | PPU=32/Point/no-compression, sheet-slice tests |
| VS-24 cove art pass | **Done, and then some** | displaced 3D water (ADR 0023), tide-aware shoreline, day/night grade, wet-shore seam |
| VS-25/26 character, boat & fish art | **Done, and then some** | in-engine JS rig baking (ADR 0021), 11 mesh hulls (ADR 0022), deck-character mesh (ADR 0024) |
| VS-27 ambient bed v0 | **Done** (procedural) | `ProceduralAudio`, `AudioDirector` |
| VS-29 tools | **Done** | `TideScrubberWindow`, `TideTableWindow`, `FishSpeciesDefEditor`, `TerrainPaintTool`, `RegionValidatorWindow` |
| VS-08 boats as data | **Done in spirit** | `BoatHullDef` carries mass/draught/hold/engine/drag/windage; no boat is special-cased by name; hold read from data. The literal Hull/Engine/Hold/Gear **component split is not built** — see §4.8 |
| CI green gate | **Working** | Buildalon runner, EditMode + PlayMode on every PR |

### 2.2 The M0 go/no-go was answered by playing, not by a document

VS-30 asked for a written M0 verdict. What happened instead is better: the owner has been playtesting the loop
directly and driving fixes into it (see the last ~10 commits, all `owner playtest` tagged). Treat M0 as
**passed by demonstration**. Do not spend a wave re-running it; fold the surviving value — the documented
**core-loop smoke test** — into the M1 acceptance pass (§4.7).

---

## 3. What changed since the M1 spec was written (read before planning)

The M1 spec in [`milestone-1-vertical-slice.md`](milestone-1-vertical-slice.md) describes a game that is no
longer exactly the game being built. Three drifts, all of them ratified or defensible, none of them yet
reflected in the M1 Definition of Done:

**3.1 The opening was replaced.** The DoD still says *"You inherit the dory from Uncle Ned."* Canon §5.8 and
the owner-ratified St Peters batch (M2-31/31b/31c) replaced that with **buy-and-repair**: dig clams on the
bared flats → walk the tide-gated sandbar to Greywick → buy a cod licence and a rod → buy a **damaged** dory at
the shipwright and pay to repair her → sail her home to Coddle Cove. `OnboardingDirector` implements exactly
this today. The cottage and Ned's memory stay inherited; the dory is earned (P4). **The DoD line is obsolete
and must be rewritten**, not quietly ignored.

**3.2 M2 work landed inside M1.** Traps and pots, the licence system, the clam dig, the St Peters region, the
lobster boat and ten other hulls above the Punt — all built. Most of it is the owner-ratified 2026 batch, so
it is not rogue work, but CLAUDE.md rule 8 ("stay in your phase") has not been holding, and every hour spent
on an M3 hull is an hour not spent closing M1. **Recommendation: freeze new M2/M3 feature work until M1 is
signed off.** Bugfixes to already-built M2 systems are fine; new ones are not.

**3.3 The six species drifted.** Spec: `cod, haddock, pollock, mackerel, rock-crab, blue-mussel`. Built:
`AtlanticCod, Haddock, Mackerel, RockCrab, SoftShellClam, AmericanLobster`. Pollock and blue-mussel are
absent; clam (the opening beat) and lobster (M2 trap gear) took their slots. Mussels are now owned by M3-16
(aquaculture) anyway. See decision **D2**.

---

## 4. The gaps — eight workstreams

Ordered by what blocks what. Owners are the `agents/` roles.

### 4.1 · Region scenes: adopt and commit Greywick + St Peters — `owner` + `tools-editor` — **the schedule risk**
`Greybox.unity` (Coddle Cove) is the only committed scene. Greywick and St Peters exist **only as editor
builders** (`GreywickBuilder`, `StPetersBuilder`) that generate from zero on every run; ADR 0019 phase 1 —
generalizing the CREATE/REFRESH split to every region and adopting them one by one — is still *Proposed,
awaiting owner sign-off*. Until this lands, **VS-22 cannot be verified**, the whole opening arc lives on the
owner's machine rather than in the repo, and any builder re-run can eat hand-authored work.

- Ratify ADR 0019 (or reject it and say what replaces it).
- `tools-editor`: extend the `RegionLogicRoot` + `Refresh <region> Logic` pattern to `GreywickBuilder` and
  `StPetersBuilder`; make full rebuild warn-and-confirm the way the cove's does.
- **Owner:** run each builder once, author the visual layer, commit the `.unity`. Headless agents cannot
  author a valid scene — this act is irreducibly the owner's.
- **Exit:** three committed region scenes; the cove↔Greywick↔St Peters hops load additively without breaking
  the persistent core; `RegionValidatorWindow` clean on all three.

### 4.2 · Audio v1 — `audio` + `owner` — **the largest true content gap**
There are **zero audio files in the repo**. Everything you hear today is synthesized at runtime by
`ProceduralAudio`. The music bus exists in `AudioDirector` and ducks correctly, but it has no stem to play.
VS-28 — the warm Coddle Cove theme, the **rising-wind tell** (canon calls it sacred, P1/P5's early warning),
the catch sting, the "made it home" warmth — is essentially unbuilt.

- Blocked on decision **D4** (where audio comes from).
- `audio`: sea-state/wind/time-layered beds; adaptive music v1 with the wind tell wired to the same
  `EnvironmentSample` the HUD reads, so sound and widget never disagree; catch sting on `FishCaught`;
  home-warmth resolve on wharf arrival.
- **Exit:** rising wind is *audible before* anything dangerous could happen and mirrors the HUD; beds
  cross-fade with no cuts; per-bus volume sliders respected; no load errors on any region.

### 4.3 · HUD v1 as widgets, and the diegetic sell screen — `ui-ux`
The reads all exist and are correct; they render as **uGUI text labels built in code**. VS-19 asks for a wind
widget with arrow/barbs, a compass ribbon, and a **set-&-drift ghost track** — a faint predicted course line on
the water, not the `"COG 042 · set 12°R"` string `CompassReadout` returns today. M1-06 asks for the sell
screen's chalkboard skin.

- Wind widget (direction relative to heading, strength by arrow length + barbs + label — redundant coding).
- Compass ribbon/rose.
- Set-&-drift predictor as a **world-space ghost track**; keep the text read as the accessible fallback.
- Sell screen chalkboard skin; keep the live marginal-price/total behaviour exactly as-is.
- **Exit:** all sea-reads legible at a glance and validated against colourblind palettes; zero per-frame
  allocation (the current HUD's discipline must survive the reskin).

### 4.4 · Runtime tide-table panel (VS-06) — `gameplay-systems` + `ui-ux`
`TideTableWindow` is **editor-only**. The player-facing "Uncle's booklet" — today + tomorrow's highs and lows
for the region, a now-marker, a simple curve, and time frozen (`timeFlowMultiplier = 0`) while you read it —
does not exist in the build. The extrema-finding logic is already written and tested inside the editor
window; this is mostly a lift into runtime plus a panel.

- **Exit:** listed highs/lows match the live tide within rounding; the HUD gauge's time-to-turn agrees with
  the table; opening pauses and closing resumes.

### 4.5 · Localization tables — `lead-architect`
A hard DoD line — *"all player-facing strings go through localization tables"* — with nothing behind it.
`HudStrings` and `WorldStrings` are honest, well-documented **seams**, and `NpcDef`/`DialogueDef` carry the
same note, but no localization package is in `Packages/manifest.json` and no table exists. Cheap now; brutal
after M2 triples the string count. See decision **D5**.

- **Exit:** Unity Localization wired, one English table, every user-facing string routed through it, no call
  site rewritten (the seams pay off).

### 4.6 · The first-boat beat and the species set — `economy-sim` + `world-content`
The DoD promises *"buy the Punt at the Greywick Shipwright — the 'real fisher now' beat."* The shipwright's
catalogue is `DamagedDoryOffer`, `CrabPotOffer`, `LobsterPotOffer`. **The Punt is not purchasable**, though its
hull (`PuntUpgraded.asset`), art, and sheet-slice tests all exist. See decisions **D1** and **D2**.

- **Exit (assuming D1 recommendation):** the repaired dory is the *earned* opening beat; the Punt is the M1
  *aspirational* purchase at Greywick (~1,800 ₲), reachable in a few sessions of good fishing, deducting coin,
  switching the active boat, and persisting across save/load. Insufficient funds blocks gracefully.

### 4.7 · Performance pass + M1 acceptance + external playtest — `qa-test`
M1-16 (profiling) shows no evidence of having been run, and VS-31 — the acceptance pass and the closed
Steam/itch.io playtest that produces the **soft-launch-readiness verdict** — gates the entire milestone.

- Profile against the ADR 0005 **desktop baseline** (60fps, typical desktop/laptop GPU): displaced water,
  2D lights, HUD, physics, one active boat. Confirm no per-frame GC in the hot path.
- Document and automate the **core-loop smoke test** (board → sail → fish → return → sell → sleep → reload).
- Save-migration test: a save written by an older build loads without loss (schema is at v4 — exercise
  v1→v4, not just v3→v4).
- Run the external playtest; deliver the written verdict with a **GO / POLISH / PIVOT** recommendation.
- **Exit:** the reconciled DoD (§6) is green and the verdict is in the owner's hands.

### 4.8 · Deferred on purpose — log, don't build
- **Boat composable components (VS-08 literal form).** `BoatHullDef` is one SO carrying hull + engine + hold
  stats. It satisfies every M1 acceptance criterion — data-driven, swappable, no name special-casing — so
  **do not refactor it for M1**. M2-17 (component swaps at the shipwright) is the item that actually needs the
  split; do it there, with a save migration, when there is a reason.
- **Pollock/blue-mussel** — pending D2.
- **Everything M2+.** See §3.2's freeze.

---

## 5. Owner decisions needed (these block work)

| # | Decision | Recommendation |
|---|---|---|
| **D1** | The "real fisher now" beat: is it the repaired dory, the Punt, or both? | **Both.** The repaired dory is the *earned* opening (already built, playtested, canon §5.8). Add the **Punt** as M1's aspirational Greywick purchase — the DoD promises a boat you save up for, the art and hull already exist, and it costs one `ShipwrightOffer` asset plus price tuning. Cheapest possible way to keep the promise. |
| **D2** | The six species: add pollock and blue-mussel, or ratify the built set? | **Add pollock only** (one asset — it gives the rod pool real depth and it is the third of the three handline groundfish). **Drop blue-mussel from M1** — M3-16 aquaculture already owns mussels. Ratify clam and lobster as built. Then **update the spec to match reality** rather than carrying a lie in the DoD. |
| **D3** | Ratify ADR 0019 (hand-authored scenes as source of truth) and commit Greywick + St Peters? | **Yes, and soon.** This is the schedule's critical path: agents can build the tooling but cannot author a `.unity`. Until you run the builders and commit the scenes, two thirds of the game's regions live only on your machine and VS-22 cannot be signed off. |
| **D4** | Where does audio come from — commissioned, licensed, or procedural-only? | **Licensed/commissioned for the music bed and the wind tell; keep procedural for ambience.** The rising-wind tell is canon-sacred and a synthesized approximation will undersell it. This is the one M1 gap with a real-world lead time — decide it first even though the work happens last. |
| **D5** | Wire Unity Localization now, or ship M1 English-only against the seams? | **Wire it now.** The seams (`HudStrings`, `WorldStrings`, `NpcDef`, `DialogueDef`) were built for exactly this and mean no call site changes. It is a day's work today and a multi-week retrofit after M2. |
| **D6** | Freeze new M2/M3 feature work until M1 signs off? | **Yes.** Bugfixes and polish on shipped systems continue; new M2/M3 features wait. This is CLAUDE.md rule 8, and it is the difference between finishing the slice and drifting past it. |

---

## 6. The route (four waves)

Waves, not dates — they express dependency, and tracks inside a wave run in parallel.

**Wave 0 — unblock (owner, days not weeks).**
Answer D1–D6. Ratify ADR 0019. Kick off audio sourcing (longest lead time, so start it first even though it
lands last).

**Wave 1 — the things nothing else can proceed without.**
- `tools-editor`: generalize CREATE/REFRESH to Greywick + St Peters (§4.1).
- `owner`: author and commit the two region scenes.
- `lead-architect`: wire localization + the English table (§4.5).
- `economy-sim`: the Punt shipwright offer + price tuning; `world-content`: the pollock asset (§4.6).
- *Runs in parallel, gated only by Wave 0.*

**Wave 2 — make it read like a finished game.**
- `ui-ux`: wind widget, compass ribbon, set-&-drift ghost track, chalkboard sell screen (§4.3).
- `gameplay-systems` + `ui-ux`: runtime tide-table panel (§4.4).
- `audio`: beds, adaptive music v1, rising-wind tell, catch sting, home warmth (§4.2) — as assets arrive.
- *Needs Wave 1's committed scenes to be authored against.*

**Wave 3 — prove it.**
- `qa-test`: desktop-baseline profiling pass; automated core-loop smoke test; v1→v4 save-migration test.
- `qa-test`: external closed playtest (Steam/itch.io) + the written soft-launch-readiness verdict.
- `lead-architect`: update the M1 DoD in `milestone-1-vertical-slice.md` to §7 below, and mark the obsolete
  inherited-dory line resolved in canon §5.8 and `npcs-and-routines.md` §3.1 (M2-31c's outstanding
  documentation debt).

**The gate:** M1 is Done when §7 is green and the owner has made the go/no-go call.

---

## 7. The reconciled M1 Definition of Done

Supersedes §6 of [`milestone-1-vertical-slice.md`](milestone-1-vertical-slice.md) once ratified. Changes from
the original are marked **[amended]**. Bars are the ADR 0005 **desktop baseline** — 60fps on a typical
desktop/laptop GPU, KB/mouse + gamepad comfort; the touch/one-thumb pass moves to the mobile port.

**The loop**
- [ ] **[amended]** You **earn** the dory — clam-dig, sandbar, licence, rod, buy-and-repair at the shipwright,
      sail her home — through a guided onboarding that teaches the full loop. (Was: "inherit from Uncle Ned."
      The cottage and Ned's memory stay inherited; the boat is earned — canon §5.8, P4.)
- [ ] You read the **tide** — HUD gauge **and the in-game tide table** — and it is a real, deterministic force
      you can plan around.
- [ ] You sail with the **force model** — wind pushes, tide sets, the boat carries way — and it feels like
      seamanship. ✅ *already true*
- [ ] You **fish** the rod interaction and land any of the region's species; the first cod feels like a
      triumph. ✅ *already true*
- [ ] You **sell** to a buyer whose price moves as you sell and recovers over days; the sell screen shows the
      marginal price. ✅ *already true (skin pending)*
- [ ] **[amended]** You earn a stake and **buy the Punt** at the Greywick Shipwright — the aspirational
      upgrade above the earned dory.
- [ ] You **sleep** to advance the day and **save/resume anywhere**; an older save migrates without loss.

**The feel (P1/P5 + cozy)**
- [ ] Coddle Cove looks and sounds like the canon home harbour — tide-aware moving shoreline, animated water,
      day-night grade ✅, **plus** gulls, hull slap, adaptive music, and the **rising-wind tell**.
- [ ] The opening is warm and hopeful, not grim.
- [ ] The sea reads at a glance — tide gauge, **wind widget, compass, set-&-drift ghost track** — with
      redundant coding that works on colourblind palettes.

**The craft (production health)**
- [ ] PPU=32 / true metric scale holds everywhere. ✅
- [ ] Content is data-driven; environment is a pure function of `(seed, gameTime)`. ✅
- [ ] All player-facing strings go through **localization tables**.
- [ ] **[amended]** All three region scenes (**Coddle Cove, Port Greywick, St Peters**) are **committed** and
      load additively without breaking the persistent core.
- [ ] The build hits the **desktop frame budget** (profiled) and the **core-loop smoke test** passes in CI.

**The verdict**
- [ ] An external playtest completed the slice and came back for repeat sessions; `qa-test` delivered a
      written **soft-launch-readiness verdict** with a GO / POLISH / PIVOT recommendation.

---

## 8. Risks

| Risk | Why it bites | Mitigation |
|---|---|---|
| **Scene authoring is owner-serialized** | Agents can generate builders but cannot author a valid `.unity`. Two of three regions are uncommitted. Everything in Wave 2 wants to be built against them. | Ratify ADR 0019 and commit the scenes **first**. This is the one place where owner time is genuinely the bottleneck. |
| **Audio has a real-world lead time** | Zero assets exist; a music bed and a canon-sacred wind tell cannot be conjured in a sprint. | Start sourcing in Wave 0, land in Wave 2. Ship the *layering and cue logic* against placeholder stems so only the audio swaps in. |
| **Scope keeps drifting past M1** | Ten hulls above the Punt and a full trap economy already exist. The pull toward M3 is strong and the slice is 80% done — the classic way a vertical slice never ships. | D6's freeze. Log every good idea to `backlog.md`; build none of it until the verdict is in. |
| **The DoD no longer describes the game** | Acceptance against a stale checklist produces a false pass — or a real one that is quietly wrong about what shipped. | §7. Ratify it before running acceptance, not after. |
| **Playtest logistics are underestimated** | A closed Steam/itch.io playtest needs a build pipeline, a page, keys, and testers — none of which is code. | Treat it as a Wave 1 side-task, not a Wave 3 discovery. |

---

## 9. What M1 signs off into

Nothing here changes M2. The St Peters batch (M2-31 through M2-39) is already partly built and stays queued
behind the verdict. If the verdict is **GO**, M2's first job is the documentation debt M2-31c named — the
inherited-dory framing in canon §5.8 and `npcs-and-routines.md` §3.1 — plus grounding and rescue (M2-A), the
teeth that the cove has deliberately been withholding.

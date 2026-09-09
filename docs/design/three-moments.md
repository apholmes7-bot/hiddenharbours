# The three moments — hit-stop, splash, coins, action timing, and the silent dig (juice PR 3)

> Juice lane, PR 3 of 3 (owner ruling 2026-09-09, "yes to 4, write the juice charter"). PR 1 is
> the grade (`lighting-and-daynight.md` §8); PR 2 is the camera (`camera-feel.md`). Pillars
> **P5 Cozy but with Teeth** (a landing HITS) and **P4 Earn It** (a sale is a reward beat you can
> see). Closes the *feedback* half of #803 (the silent dig). Companion to `notebook-main-ui.md`
> (the register page the readouts live on) and `AUDIO-MANIFEST.md` (the four slots the moments
> feed).

## 1. What it is

Three moments the player should feel in the hands, each built from things that already exist,
every number a `GameConfig.Juice` dial or an `ActionTiming` Def:

| Moment | Signal (EventBus, Core) | What happens |
|---|---|---|
| **The landing frame** | `JuiceMomentCue(Landing, at, kg)` from the fishing controller on the frame the fish leaves the water | `Time.timeScale` dips to `LandingHitStopScale` for `LandingHitStopSeconds` (`HitStopHost`); a pooled splash of `LandingSplashDropsMin + LandingSplashDropsPerKg × kg` drops (capped at `LandingSplashDropsMax`) for `LandingSplashSeconds` (`MomentBurstEmitter`); the notebook's weight readout counts up over `WeightCountUpSeconds` and pops the last digit by `CountUpPopScale` for `CountUpPopSeconds`. |
| **The sale** | `MoneyChanged` then `CatchSold` (the order `SellService` already publishes) | In the notebook's ledger page `CoinFlyCount` pooled coins fly crate → purse, `CoinFlyStaggerSeconds` apart, on a `CoinFlyArcPixels` arc over `CoinFlySeconds`; the purse counts up from the old balance over `SaleCountUpSeconds` from the first coin's landing and pops on the last. |
| **Action timing** | an `ActionTimingDef` per action (`Data/Resources/ActionTiming/*`) | Four beats — pre-hold, strike, follow-through, settle — drive the existing frame players: the cast release holds frame 0 through the pre-hold and sweeps the strike (`PlayerFishingAnimator`); the haul holds its last frame for follow-through + settle before handing the walk sprite back (`PlayerHaulAnimator`, **wired-only**, see §4); the dig's lift line lands on the strike beat (`LiftFeedback`). A `DigStrike` cue throws `SandChunkCount` sand chunks at the hole; a `CastEntry` cue rings the water `CastRingCount` times. |

And the **silent dig** (#803): on `CatchLanded` the lift is now visible — the hand already drew
the clam (the defect was feedback, not a sprite), so this PR adds the one consequence that was
missing: a short line in the notebook's register ("Lifted out a soft-shell clam — it's in your
hand.") on the dig Def's strike beat, the register's weight readout counting up to the clam's
weight, and the sand chunks at the hole. The pail's own "1/20" line is unchanged.

## 2. The five laws

1. **One moment, one cue.** Every moment publishes exactly one `JuiceMomentCue` from the system
   that owns it (`FishingController` for the landing and the cast entry, `ClamDig` for the dig
   strike). Presenters subscribe; nothing in a presenter names a Fishing, Boats or Economy class
   (rule 4). The sale rides the two events the economy already publishes and adds none.
2. **Feel timers run on unscaled time.** The hit-stop scales `Time.timeScale`; every feedback
   timer here ticks on `Time.unscaledDeltaTime` (a source guard pins the five files), so a
   hit-stop can never freeze its own splash.
3. **`CatchLanded` has exactly one on-screen consequence, and the hand shows the clam.** The
   #803 guard, pinned by a test on the same rig as `CatchInHandTests` (a fixture that left
   `GameServices.CatchHands` null would take the pail path and be green about the wrong game).
4. **Pooled, sized, dead on time.** The splash, chunks and rings share one fixed pool
   (`MomentBurstConfig.PoolSize`, 64), recycle the oldest when full, and die at their lifetime.
   Coins are `CoinFlyCount` pre-built sprites parked inactive. The count-ups write through one
   pre-sized `StringBuilder`. Nothing allocates after construction (rule 7).
5. **The world clock reads wall time.** `Time.timeScale` is a feel channel. `GameClock.Advance`
   integrates `Time.unscaledDeltaTime` at its own `TimeScale`, so a 90 ms hit-stop costs the tide
   90 ms of wall time and not 4.5 ms; `IsPaused` stays the only pause (rule 5; `ShellPause`: "there
   is no second clock"). Pinned by `GameClockReadsWallTimeTests`. `LandingHitStopSeconds = 0` is the
   documented off-switch for the dip itself.

## 3. The pieces

| Piece | Where | What it is |
|---|---|---|
| `JuiceMomentCue`, `JuiceMoment` | `Core/Events/JuiceSignals.cs` | The one moment event: kind, world position, strength (kg for a landing or a dig, 0 for a cast, the amount for a sale). |
| `HitStop`, `CountUp`, `CountUpMath`, `LiftLine` | `Core/Juice/` | POCOs. `HitStop` keeps the longer remaining and the deeper dip on overlap and returns **exactly** 1 on expiry. `CountUpMath.ValueAt` is ease-out and never shows the target before t = 1 (the pop must land on a new number). `LiftLine.For` builds the register line ("Lifted out" for shellfish, "Landed" otherwise). |
| `ActionTimingDef`, `ActionTiming`, `ActionTimingMath` | `Core/Juice/ActionTimingDef.cs` | The Def and its math: `BeatAt`, `FrameFor` (0 through the pre-hold, a monotone sweep across the strike, the last frame held after; a zero strike is a hard cut), `Strike01`, `IsDone`. |
| `CastTiming`, `HaulTiming`, `DigTiming` | `Data/Resources/ActionTiming/` | The three Defs (`timing.cast` 0.06/0.12/0.2/0.15, `timing.haul` 0.08/0.1/0.25/0.15, `timing.dig` 0.1/0.08/0.25/0.15). |
| `HitStopHost` | `Code/App/` | Self-installing; on a landing captures `Time.timeScale`, multiplies in the dip, ticks unscaled, writes back **exactly** what it found. Disabling mid-stop releases. |
| `MomentBurstEmitter` | `Code/Art/` | Self-installing; the one pool for drops, chunks and rings; look tunables in a `MomentBurstConfig` (the `WadeSplashConfig` precedent); counts and lifetimes from `GameConfig.Juice`. |
| `LiftFeedback` | `Code/Player/` | Self-installing; on `CatchLanded` builds the lift line once and publishes it as a `DevNotice` on the dig Def's pre-hold + strike. |
| `PlayerFishingAnimator`, `PlayerHaulAnimator` | `Code/Player/` | The cast release on its Def (serialized field, else `Resources/ActionTiming/CastTiming`, else the fps sweep that shipped); the haul tail on its Def (serialized field only — §4). |
| `NotebookPresenter` | `Code/World/` | The register line, the weight count-up, the coins and the purse count-up on the ledger page; ticks at `MomentTickHz` on unscaled time and only while something is live. |
| `AudioDirector` / `AudioDirectorLogic` | `Code/Audio/` | Hears `JuiceMomentCue`; four new cues (`LandingHit`, `SaleChime`, `DigStrike`, `CastEntry`) and four new slots. No files: `_saleChime` falls back to `_homeWarmth` (nothing the player hears changed), the other three are silent until the owner's purchase lands. |

## 4. Trades and findings (for the slot and the seat)

- **The haul tail is wired-only.** Four EditMode suites and one PlayMode suite pin the *immediate*
  hand-back of the walk sprite when a haul ends, and a heave lingering under a walk that already
  started would slide her. So `PlayerHaulAnimator` reads its Def from the serialized field only —
  no Resources fallback — and `HaulTiming.asset` is the Def to drag on when the slot judges the
  hold worth it. The behaviour is tested both ways.
- **No `FisherDig` presenter exists.** The charter names one; the shovel's frames are played by
  the tool arc's own player and no dig animator lives in `Player`. The dig Def drives the lift
  line's beat only. Wiring the shovel's strike frames to `DigTiming` is one `FrameFor` call in
  whichever player owns them.
- **The hit-stop no longer slows the world (fixed here, lead-architect ruling 2026-09-09).** `GameClock`
  integrated scaled `Time.deltaTime`, so a 90 ms hit-stop at 0.05 cost ~85 ms of game time per
  landing. Law 5: the clock integrates `Time.unscaledDeltaTime` in `GameClock.Advance`; the ~thirty
  sim files that read `Time.deltaTime` for MOTION still slow under the dip, which is the feel.
- **The hand draws the clam.** `CarriableCatch.Create` sets the renderer's sprite from the
  catch art or the icon registry; the #803 defect was feedback only. Pinned.
- **The haul cannot be pre-held.** The pull is position-driven (the frame is the line's
  progress, not a clock), so only its follow-through and settle apply.
- **One small string per count-up tick.** `Text.text` is compared before assignment, but a
  changed value still allocates the label's string (Unity's `Text` takes a string). At
  `MomentTickHz` for under a second per moment; the slot reports the number.
- **The register's leaf is narrow.** The lift line may overrun the weight readout on the
  smallest notebook; the slot judges.
- **"Prefabs" are runtime-built.** The charter says two pooled particle prefabs; the drops,
  chunks and rings are runtime-built dots (the `AmbientGlobals` precedent) so no scene or prefab
  changes. St Peters is not rebuilt.

## 5. The dials

All in `GameConfig.Juice` (struct, `Default`, and **hand-added to `GameConfig.asset`** —
`GameConfigAssetCoverageTests` guards; an absent field reads ZERO):

`MomentsEnabled` 1 · `LandingHitStopScale` 0.05 · `LandingHitStopSeconds` 0.09 ·
`LandingSplashDropsMin` 6 · `LandingSplashDropsPerKg` 3 · `LandingSplashDropsMax` 24 ·
`LandingSplashSeconds` 0.55 · `WeightCountUpSeconds` 0.6 · `CountUpPopScale` 1.35 ·
`CountUpPopSeconds` 0.18 · `CoinFlyCount` 8 · `CoinFlySeconds` 0.45 · `CoinFlyStaggerSeconds`
0.05 · `CoinFlyArcPixels` 14 · `SaleCountUpSeconds` 0.7 · `SandChunkCount` 7 ·
`SandChunkSeconds` 0.45 · `CastRingCount` 3 · `CastRingSeconds` 0.6 · `MomentTickHz` 30.

Every 0 is a documented off-switch: `MomentsEnabled = 0` hands every presenter back untouched;
a 0 duration skips that piece.

## 6. Acceptance

EditMode (`JuiceMomentsTests`, `JuiceMomentAudioTests`): every curve (`FrameFor`, `BeatAt`,
`Strike01`, the hit-stop overlap and exact-one expiry, the count-up's never-early and one-tick
landing, the pop's edges and peak, the splash sizing and ring stagger), every presenter through
its public hook (the host dips and restores exactly; the emitter spawns the charter's counts and
kills them on time; the lift line fires once on the strike beat; the cast release and the haul
tail on their Defs; the notebook's register line, weight count-up and sale), the #803 guard on the
real dig rig, the three Defs loading from Resources, and the events: every moment's cue from its
owner, every feel timer unscaled, every audio slot named in the manifest and mapped to its own cue.

What the slot adds (the one Unity run of this PR): `unity test -platform EditMode -filter
JuiceMoment`, then the five-moment checklist — **dig a clam** (the line, the readout, the chunks),
**cast** (the flick, the rings), **hook and land a cod** (the hit-stop at 0.09 s, the splash, the
count-up), **sell it** (the coins, the purse), **run aground** (PR 2's shake still sound under
this PR's clock writes) — measuring the splash and coin pools' frame cost, the hit-stop's feel,
and the count-ups' readability.

## 7. Not done here

No audio files (a purchase); no new art frames; no new systems; no scene rebuild; no fix to the
world clock under hit-stop (reported); no `FisherDig` (none exists); no Resources fallback for the
haul tail (wired-only by the four suites that pin the hand-back).

# The camera speaks — `CameraFeel` inside the one `CameraFollow` (juice PR 2)

> Juice lane, PR 2 of 3 (owner ruling 2026-09-09, "yes to 4, write the juice charter"). PR 1 is
> the grade (`lighting-and-daynight.md` §8); PR 3 is the three moments. Pillars **P5 Cozy but
> with Teeth** (the sea hits back) and **P1 The Sea Has Moods** (the view opens up at speed in a
> swell). Companion to `scene-sizing-and-world-scale.md` (the zoom ladder and the clamp rectangle,
> neither of which this touches).

## 1. What it is

Three small camera motions, all of them tunables, layered inside the **one** follower:

| Moment | Signal (EventBus, Core) | What the camera does |
|---|---|---|
| A catch lands in her hands | `CatchLanded` (`Item.WeightKg`) | **Push-in** by `CatchPushInFraction` of the framing height over `CatchPushInSeconds` (ease-out), released over `CatchPushOutSeconds` (smooth-step). Scaled by weight class: full at `CatchPushInHeavyKg`, down linearly to the `CatchPushInLightScale` floor. |
| The hull touches bottom | `BoatGrounded` (`Severity`) | **Shake** of `ImpactShakeMeters × Severity` world metres at `ImpactShakeHz`, squared decay over `ImpactShakeSeconds`. A second hit takes the larger amplitude and restarts the clock. |
| She drives fast on open water | the follow's own target speed, plus `EnvironmentSample.SeaState01` | **Pull-back** by `SpeedPullBackFraction` (smooth-step from `SpeedPullBackStartMps` to `SpeedPullBackFullMps`) plus `SpeedPullBackSeaStateFraction × SeaState01`, **slew-limited** to `SpeedPullBackSlewSeconds` per full range so the sea never breathes in a swell. |

There is no impact or collision event in Core besides `BoatGrounded`; the shake keys on that one.
Ashore the grounding is ignored (the boat that grounded may not be hers).

## 2. Where it lives, and the four laws it keeps

- **One follower.** `CameraFollow` is still the only `CameraFollow` (memory law: two fight). The
  feel is a POCO, `CameraFeel` (`Assets/_Project/Code/App/CameraFeel.cs`), that owns the curves
  and nothing Unity; `CameraFollow` feeds it three signals and unscaled time and applies its two
  outputs (a zoom multiplier and a world offset) in `TickFeel`, which runs in the frame order
  **zoom policy → framing tween → feel → pixel snap → clamp**.
- **It multiplies, it does not set.** The framing (the zoom ladder, the tween, the hard set)
  writes `_framingOrtho`; the feel writes `_framingOrtho × ZoomMultiplier` to the camera and never
  remembers a camera value. A tier change during a push-in lands on the new tier, not on the tier
  times the push. At rest the camera is handed back **byte-for-byte**, including the
  `PixelPerfectCamera`'s enabled state, which the framing owns (`_ppcWantedByFraming`) and the
  feel only borrows.
- **The clamp is still the region rect.** The shake is added before the snap and the clamp, so it
  is quantised to the pixel grid and held inside `GameServices.CurrentRegionBounds` exactly as the
  follow is. The clamp's re-sync of the smoothing filter subtracts the frame's shake, so the filter
  never integrates from a shaken spot; `FollowTarget` clears the shake it wrote before writing the
  new position, so nothing accumulates.
- **Unscaled time.** `LateUpdate` passes `Time.unscaledDeltaTime`; PR 3's hit-stop cannot freeze a
  shake mid-frame. A source guard in `CameraFeelTests` pins both this and the one-follower law.

**The mode gate** (charter §3: sail-past framing, deck and interior untouched): the layer is live
**on foot** and **at the helm** (`ControlModeChanged`); **on deck** only by `FeelOnDeckEnabled`
(ships OFF per the charter — but the intro fishes from the deck, owner ruling 2026-09-06, so it is a
dial, not a rule); **in a cabin** (`CabinEntered` … `CabinLeft`) and **driving** never. The
pull-back is the helm's alone; walking fast is not open water. A gate that closes on a live effect
lets it decay out rather than cutting it.

## 3. The PixelPerfectCamera trade (what the slot must look at)

URP's `PixelPerfectCamera` re-imposes its integer zoom on every render, so any continuous zoom has
to go around it — the feel pauses it while an effect is live, exactly as the framing tween already
does. For the push-in and the shake that is a few non-integer frames, the tween's own cost. The
**pull-back holds for as long as she drives fast**, and while it holds the pixel grid is not
integer: sprites read a shade softer at speed. That is a look decision for the owner on the slot.
`SpeedPullBackFraction = 0` (with `SpeedPullBackSeaStateFraction = 0`) turns it off outright and
the PPC is then never paused for it.

## 4. Tunables (rule 6 — every key is in `GameConfig.asset`, `Juice` block)

| Field | Ships | What it does |
|---|---|---|
| `FeelEnabled` | on | Master switch; OFF hands `CameraFollow` back untouched. |
| `FeelOnDeckEnabled` | off | The push-in and shake on deck (never the pull-back). See the mode gate. |
| `CatchPushInFraction` | 0.08 | Push-in as a fraction of framing height, heavy fish. 0 = none. |
| `CatchPushInSeconds` / `CatchPushOutSeconds` | 0.12 / 0.45 | Rise and release times. 0 = instant. |
| `CatchPushInHeavyKg` | 6 | Weight at which the push-in is full. 0 = every catch is heavy. |
| `CatchPushInLightScale` | 0.35 | Floor of the weight scale (a smelt still lands). |
| `ImpactShakeMeters` | 0.18 | Amplitude at severity 1, world metres (32 px/m on the asset grid). 0 = none. |
| `ImpactShakeSeconds` / `ImpactShakeHz` | 0.3 / 18 | Decay time and frequency. |
| `SpeedPullBackFraction` | 0.10 | Pull-back at full speed. 0 = none, PPC never paused for it. |
| `SpeedPullBackStartMps` / `FullMps` | 2.5 / 7 | Speed band of the smooth-step. |
| `SpeedPullBackSeaStateFraction` | 0.05 | Extra pull-back in a full sea, scaled by the same speed term. |
| `SpeedPullBackSlewSeconds` | 2 | Seconds to cross the whole range either way — the anti-breathing limit. 0 = no limit. |
| `FeelSeaStateRefreshHz` | 4 | Slow-tick rate of the sea-state sample (rule 7). |

## 5. Budget (rule 7)

Zero allocations after construction: `CameraFeel` is one object with value fields, the follow's
speed term is one vector length it already computed, and the sea state is sampled on a slow tick.
When nothing has fired and nothing is asking, `TickFeel` returns before touching anything (the
byte-for-byte path). The per-frame cost with an effect live is a handful of `Mathf` calls; the
slot measures it (profiler ms of `CameraFollow.LateUpdate`, before / after) alongside the look.

## 6. Acceptance (`CameraFeelTests`, EditMode, no scene)

The curves: envelope shape, monotone rise and fall, exact zero at rest, weight scale, shake inside
its amplitude and exactly zero after its duration, the slew limit, backwards thresholds never
divide by zero. Driven: a synthetic catch hits `1 − CatchPushInFraction` at the in-time and is
**exactly** at rest after in + out; a grounding shakes inside its amplitude and returns exactly to
zero; a pull-back is slew-limited, reaches its fraction, never breathes in a modulated sea, and
comes back to exactly nothing. On the component: every mode gate, the master switch, a tier change
under a live push-in, a whole frame with nothing fired leaving the camera byte-for-byte, and a
shake against the region rect never carrying the view over the edge.

What the slot adds (the one Unity run of this PR): `unity test -platform EditMode -filter
CameraFeel`, then land a cod, run aground at the cape, cross the approach at speed — the shake
amplitude by eye against the 0.18 m dial, the pull-back's softness at speed, and the profiler line.

## 7. Not done here

No audio (a purchase); no wind cap change; no aboard defects; no hit-stop, splash, or count-up
(PR 3 — `three-moments.md`); no new event in Core (the shake rides `BoatGrounded`); no scene edit — St Peters is not
rebuilt.

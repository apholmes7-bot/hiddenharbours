# Boat lights PR 2c — a glow stays in its space, and a lit room is drawn as its WINDOWS

Eyeball pack for ADR 0016's PR 2c amendment, and the owner's look gate. His ruling, given at #716's
merge in answer to "the masthead and cabin glows read large and blobby at that zoom":

> The glows should be constrained to their space, if its interior it should be confined to the cabin
> with the glow only coming through the windows.

---

## Framing and exposure

**The hull.** The Cape Islander — the hull the owner reviews at — from her committed
`CapeIslanderIsoHullMesh.asset`, built through the full `IsoFacetHullPresentationService.Install`
path, so what is photographed is what the game builds and not a fixture's idea of it. Her cell is
**456 × 420 px at 32 px/m**; the camera is orthographic, framed on the cell over the rig origin,
`clearFlags = SolidColor` on **transparent** — so the boat is isolated against nothing.

**The night.** `_DayNightTint` is set as a shader global to **`(0.100, 0.120, 0.200)`**, luminance
**0.123** — a deep-night frame. That is above the additive shader's own gate threshold by a wide
margin (it reads the cycle as ACTIVE at luma > 0.02, and darkness ≈ 0.88 sits well past its full-on
band of 0.12 + 0.35), so every lamp in these plates is burning at its full shipped intensity rather
than part-way up a ramp.

**The clock is stopped and the flicker is frozen AND RE-TICKED.** `Time.timeScale = 0`, every
`SceneLight.FlickerAmount` driven to 0, then the master switch cycled so that `OnEnable` pushes the
frozen value — because with the clock stopped a light's throttled `Update` never fires again on its
own and the freeze alone is a no-op. That is the lesson #697 paid five false reds for.

**⚠️ These are shot from a SECOND camera, and PR 2a's README warns against exactly that.** Its
warning is real and stands for an in-scene shot: the day/night overlay and every additive quad are
pinned to the active camera, so a fresh camera dropped into a running world photographs a scene with
no night in it and no lamps. It does not bite here for two reasons, and both are load-bearing:
this fixture's camera is **the only camera in the scene** (so `SceneLight.ActiveCamera()` resolves to
it and the quads pin to it), and the night is **set by hand as a global** rather than published by a
`DayNightController` that is not running. It is the same arrangement `MeshInteriorLampsPlayTests`
uses, and the `(null)` column of the measurement — the same arm rendered twice, **0 px** at every
heading — is the standing proof that the rig is stable.

**⚠️ And they are BARE-HULL plates, not in-world ones.** PR 2a's pack was shot in St Peters and at
Nine Mile Creek, with water, a wharf and neighbours. These are one boat on transparency. That is
deliberate for an A/B — nothing but the thing under review changes between the two arms — but it
means they say nothing about how she reads **against water at that zoom**, which is the owner's
actual question. The in-world shot is owed; see "What is NOT here" below.

**Both arms are ONE BUILD.** `GameConfig.BoatLegacyCabinGlow` is the passthrough: ON restores
yesterday's 1.5 m amber disc and the old lamp pool radii and draws no windows at all. So `1-legacy-disc`
and `2-windows` differ by exactly the dial, not by two checkouts.

Regenerate any plate with:

```
Unity.exe -runTests -batchmode -projectPath <worktree> -testPlatform PlayMode \
  -testFilter HiddenHarbours.Tests.PlayMode.BoatWindowGlowPlayTests
```

(no `-nographics` — it needs a real device, and skips itself on CI, which has none). They land in
`%TEMP%/DefaultCompany/My project/boat-glow-confinement/`. These were regenerated from the merged
head so the images provably come from the committed code.

---

## The plates

### `running-{000,090,180,270}-{1-legacy-disc,2-windows}.png` — the owner's frame

**The cape UNDER WAY at night, her whole light show going** — cabin, both sidelights, stern light and
masthead — in both arms at four headings. This is the pair to judge the look on.

- **Beam-on (090)** is the clearest. In `1-legacy-disc` the wheelhouse roof is a blown-out white
  slab and the masthead a large ball; you cannot read the structure of the boat. In `2-windows` the
  house reads as a **box with two lit windows in its near side**, the deck around it dark, and a
  short warm wash under each window. That is the ruling in one frame.
- **Bow-on (180)** is the other useful one: the roof stops being a blob, the mast and its cross
  become legible again, and the three-pane windscreen reads as three warm rectangles.
- **Stern-on (000)** shows the smallest signature — one light in her aft wall, outboard of the
  sliding door. Correct: from dead astern that is all of her glass you can see.

### `cape-{000,090,180,270}-{0-dark,1-legacy-disc,2-windows}.png` — the measurement

The same four headings **MOORED**, which is what lets the measurement isolate the cabin. A hull lying
still shows one anchor light and, by `BoatLamps.ShowsWhen`, a lit cabin only while somebody is
aboard — so `0-dark` (nobody below) against `1-legacy-disc` / `2-windows` (skipper below) is the same
frame twice with **exactly one thing different**. Each arm differences its own pair, so the anchor
light — which is *not* the same size in the two arms, having shrunk 0.75 → 0.34 m — cancels out.

Footprint = pixels whose summed rgb rose by more than 12/765 between the dark shot and the lit one.
It counts **area**, not brightness, because "blobby" is a statement about area.

| heading | disc (px) | windows (px) | ratio | walls washing |
|---|---|---|---|---|
| 0 | 7026 | 902 | 0.128 | 1 |
| 90 | 6992 | 1776 | 0.254 | 1 |
| 180 | 6333 | 2730 | 0.431 | 2 |
| 270 | 6987 | 1747 | 0.250 | 1 |
| **total** | **27338** | **7155** | **0.262** | |

The `(null)` column — the same arm rendered twice, one frame apart — is **0 px at every heading**.
That is checked *before* any number above it is believed: a footprint metric that counts something on
two identical frames is measuring its own noise.

### ⭐ What a plate caught

**The first tune of this feature was WRONG, and the average said it was fine.** At a wash of 2× a
window's width and a 55° cone, the four-heading mean came out at **0.723** of the area the disc
covered — comfortably inside the guard, apparently a win. But at heading **180**, the one heading
where *two* of her walls face the viewer at once, the two washes together covered **1.23×**: more
deck than the blob the ruling retired.

A mean is the wrong statistic for "constrained to its space", because the failure lives in the extreme
by construction. Retuned to **1.4× / 45°**, and the fixture now asserts the **worst heading** (< 0.85)
as well as the aggregate (< 0.60).

### `boat-window-panes.txt` — the derivation

Every window in the fleet as `BoatWindowProbe` derived it from each rig's own published HOUSE
glazing: position, size and the way it faces, per hull. **218 lit panes over 25 hulls**, plus 72
bridge panes measured and deliberately left dark. Re-run it from
`Hidden Harbours/Rig Baking/Probe: boat windows (print the table)`.

The tail carries the number the refusal rests on: **worst corner KEPT 0.203 m (the lobster boat),
best corner REFUSED 0.453 m (the sport fisher convertible)**, against a 0.30 m tolerance. Re-read
those before anybody moves it.

---

## What is NOT here

- **The NMC wall at 02:00 with one skipper aboard.** A scene-level shot, not built in this lane. The
  claim it would show — his windows lit, his neighbours dark — is `ShowsWhen(CabinGlow, Moored,
  occupied)` and is covered by `BoatLampRegimePlayTests`; and the moored triplets above are that same
  logic on one hull. **Owed**, and the right way to answer "how does she read against water".
- **The two sport fishers.** Refused their windows (their rigs publish a flat half-width for a side
  that curves in plan) and falling back to the disc they already wear, so there is nothing to A/B.
- **The bridges.** 72 measured panes on the ships' wheelhouses, currently dark by design: this lane
  confines an existing glow and lights no new room. An owner call.

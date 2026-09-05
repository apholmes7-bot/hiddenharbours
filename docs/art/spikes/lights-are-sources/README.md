# Lights are SOURCES — the plates behind the 2026-09-04 ruling

**The owner, playing the St Peters arrival at 06:13–06:40:** *"spotlight doesnt read on water or
enviroement its just a flat white, dock lights are just a round glow, it should glow from within the lamp
reasilitcally."*

Both halves of that sentence are about the same mechanism. ADR 0016's additive quad is a light SOURCE'S OWN
BLOOM — it is added to the frame, so it cannot darken, cannot be occluded, and cannot tell a plank from the
sea. It was being drawn at the size of the POOL the lamp is supposed to light. A lamp post has no 3.6 m
glowing part; it has a 0.40 m lantern and a 3.6 m patch of ground the lantern makes brighter, and drawing
the second as though it were the first gives you a flat cream disc with the pier inside it.

Every plate here is shot by `LightsAreSourcesPlatePlayTests` (PlayMode), in the **real St Peters scene**,
with the lamps placed by the **builder's own code path**, through the **game's own camera** at the game's
own framing — so the day/night multiply, the additive glows and the lamp shadows composite exactly as a
player sees them. Re-shoot them with:

```bash
unity test . --mode PlayMode --filter HiddenHarbours.Tests.PlayMode.LightsAreSourcesPlatePlayTests
```

**One build, one dial, two arms.** The BEFORE arm is not a git checkout — it is the same frame with the
bloom put back to `LightPresets.ReachMetres`, which is exactly what shipped. Same scene, same clock, same
camera, same night; the arms differ by the thing under review and by nothing else.

| plate | what it is |
|---|---|
| `00-pier-0200-dark.png` | the 02:00 pier with the lamps switched off — what "lit" is measured against |
| `01-pier-0200-pool-BEFORE.png` | the bloom drawn at the 3.6 m pool: the picture the owner refused |
| `02-pier-0200-fitting-AFTER.png` | the bloom drawn at the lantern's own 0.14 m glazing |
| `03-pier-noon-control.png` | the same lamps by day |
| `04-beam-0200-fullquad-BEFORE.png` | the searchlight with `_quadGlowScale = 1`: a flat cream wedge |
| `05-beam-0200-sourceglow-AFTER.png` | the same beam at `0.3`: a glow at the lamp, the water carrying the throw |

## What the plates measure

All figures at 1200 × 900, tint (0.118, 0.134, 0.177) at 02:00, with the sea held still (see below).

**The pier lantern (01 → 02).**

| | BEFORE (pool) | AFTER (fitting) |
|---|---|---|
| pixels the lamps light | 166,484 (15.42 % of the frame) | **254 (0.02 %)** |
| relative local contrast inside the disc's own footprint | 0.0118 | **0.2142** (18.1×) |

The second row is the one that answers *"it should glow from within the lamp"*. Relative local contrast —
mean neighbour-to-neighbour luminance step over mean luminance — is what "washed out" means as a number: a
big smooth disc has enormous variance and no structure, so plain variance says the disc is the more
detailed picture. Inside the region the disc covered, the frame comes back to **0.2142** against the
**unlit pier's 0.2105** — the planks, the bollards, the post and the moored dory are back to within 1.8 %
of how they read with the lamp off, because a bloom on the lantern is not a pool on the planks.

⚠️ **Which is also the honest half-picture, and plate 02 shows it plainly: the deck under the lantern is
DARK.** PR A moves what is drawn at the lamp and puts no light on the ground at all. Making a lamp light
the ground the way the sun lights a tree — the lit-decor path, with `ReachMetres` as its falloff — is the
illumination PR. The owner has already ruled the disc worse than the dark.

**The searchlight (04 → 05).** Relative local contrast inside the stretch of sea the dial moves
(138,245 px): **0.0155 → 0.0341, a 2.20× rise.** #691 lights the sea by N·L against the wave field's own
normal, so crests catch the beam and troughs fall into shadow; a full-length quad then lays the same light
over the top of it with no relief in it, and the flat copy is the brighter one. Plate 04 is that sentence
as a picture. In 05 the quad is a glow at the lamp and the water's own term carries the throw — one
illumination, on the surface that owns it.

**Noon (03).** The two bloom arms differ by **0 px**: shrinking a bloom is exactly invisible by day. (The
gate's own arithmetic is pinned bit-exactly in `LightMathTests`; what a whole-frame control adds is that
the ruling cannot be seen at the hour nobody should see a lamp.)

## Three fixture traps, each of which produced a convincing wrong plate

1. **The game clock holds the sun; it does not hold the SEA.** The water animates on engine time, and
   639,757 pixels of a 1,080,000-pixel plate changed between two *identical* frames — a noise floor that
   swallows anything a lamp does. Engine time has to stop too, and the fixture measures the floor rather
   than assuming it.
2. **`DayNightController` follows a MOVING clock.** Seeking to an hour and stopping the clock in the same
   breath leaves the frame wearing the hour the scene loaded at: a 02:00 beam plate came back over a bright
   noon sea, and a night-gated lamp over a daylit sea emits nothing at all. The fixture now waits for the
   tint to settle *and* for the lights' own night gate (asked of `LightMath`) to be in the state the hour
   calls for.
3. **The overlay is fitted to the camera's `orthographicSize × aspect` — and attaching a render texture
   CHANGES a camera's aspect.** Attaching it inside the capture, after time was frozen, left the night as a
   rectangle inset in a daylit sea: the same picture a broken gate would give, from a fixture that broke
   it. The camera is handed its plate target *before* the world stops, and the zoom is allowed to settle.

A control arm that still contains the thing under test earns a fourth mention: disabling the
`BoatSpotlight` script leaves the `SceneLight` it configured drawing happily, so the "dark" frame still had
the beam in it and the fixture reported, with great confidence, that the beam lit **zero** pixels.

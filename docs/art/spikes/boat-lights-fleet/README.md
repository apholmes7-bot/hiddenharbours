# Boat lights PR 2a — the fleet's lamps, and the rule of the road

Eyeball pack for ADR 0016's PR 2a amendment. **Every plate is shot at the SHIPPED exposure** — the
day/night profile's own tint at the hour named on the plate, `_BeamLitStrength` 1.6 in on `Water.mat`,
and the shipped `BoatSpotlight` (intensity 1.5, range 9 m, cone 26°, lamp height 2.5 m). That is the
2026-09-01 correction on lights PR A honoured: #691's four plates were shot at a brighter, bigger lamp
and a night ~4× lighter in blue, so "it reads correctly" there said nothing about this exposure. The
02:00 tint these were shot at is `(0.118, 0.134, 0.177)` — the post-#709 moonlit night, which is the
night a lamp now has to read against.

Shot from a live editor with the game clock **seeked and frozen** (`TimeScale 0`), reading back
`Camera.main` — never a second camera, because the day/night overlay and every additive light quad are
pinned to the main camera's frustum and depth, so a fresh camera photographs a world with no night in
it and no lamps.

---

## The plates

### `01-arrival-0613-she-runs-in-lit.png` — the owner's ruling, in the running game
St Peters, 06:13, Armand's Cape Islander coming in. All three phrases of the 2026-08-27 ruling are in
this frame: **cabin light on** (the amber spill out of her wheelhouse), **navigation lights on** (red
to port and green to starboard on the bow, white at her masthead and white at her transom), **spotlight
working** (the cone thrown forward off her bow, lighting the sea by wave relief). Her red and green are
the right way round for the heading she is on — and that is the one mistake in this feature that could
actually mislead somebody, so it is also pinned by test at all eight facings.

⚠️ **This plate is the reason PR 2a is not just data.** Measured live before the fix, she was showing a
cabin glow and an **anchor light**, with her beam dark: her hull is built with a `MooredBoat`, which is
the game's *drawer* and not a claim about her state, and the regime believed it. See §"what a plate
caught" below.

### `02-nmc-wharf-0200-moored-anchor-lights-only.png` — a fleet asleep
Nine Mile Creek, 02:00, five lamped hulls made fast along the wharf wall. Each shows **one all-round
white anchor light and nothing else**. Their wheelhouses are **dark**, because nobody has gone below
them — the skippers are standing on deck, and seven identical lit wheelhouses along a wall is a row of
lanterns rather than a harbour.

The hulls themselves are almost invisible, and that is the shipped night doing what the owner asked of
it ("dark enough at night that the player feels the need to use radar and the lighting"). The wharf
wall, its ladders, bollards and tyres give the scale.

### `03-nmc-wharf-0200-the-same-boats-under-way.png` — the same frame, one word changed
The identical camera, the identical hour, the identical boats — with the regime told they are **under
way** (through `MooredBoat.SetWay`, the same seam the arrival itself calls). Red-and-green pairs,
mastheads, stern lights, cabin glows and three searchlights raking the wharf face.

⚠️ **This is not a shipped picture** — nothing in the game lets twenty-five boats go at a wharf at once.
It is the *contrast*, and the contrast is the feature. Read plate 02 as the harbour and this one as the
proof that plate 02 is a decision rather than an absence.

### `04-aspect-under-way-facing{0,2,4,6}.png` — her aspect, four ways round
One hull, under way, swung through four of her eight facings. A sidelight's whole job is to tell a
lookout which way she is pointing; these are the four views that show it. The positions behind them are
held to the rig at 0.1 px at every facing by `BoatLampAnchorTests`.

### `05-noon-control-nothing-burns.png` — the gate
The same wharf at noon. **125 light quads are still enabled and drawing** — the night gate is
in-shader, not a switch — and what reaches the frame is a faint red/green speck and a warm smudge. A
lamp that burned at midday would be the loudest possible bug and it is worth one plate to show it does
not.

---

## `boat-lamp-anchors.txt` — the measurement, re-runnable

Every lamp on every one of the 27 hulls, as `BoatLampAnchorProbe` derives it from that hull's own rig,
with the inversion residual beside each station and the two numbers the presets are bounded by. Re-run
it yourself: **`Hidden Harbours ▸ Rig Baking ▸ Probe: boat lamp anchors`**. It prints; it never writes.

---

## What the plates cannot show, and where it is proved instead

**That the Cape Islander is unchanged.** Two live runs of the same frame are *not* comparable at the
pixel: her cabin glow flickers deterministically from `(seed, Time.time)`, and `Time.time` differs
between two editor sessions — so a before/after plate pair would differ by a fraction of an LSB inside
the glow's radius and prove nothing either way. Pixel identity is measured instead by
`BoatLampRegimePlayTests.TheCapeUnderWay_DrawsExactlyWhatSheDrewBeforeHerAnchorLightWasAdded`, which
holds one host, freezes time, freezes **and re-ticks** the flicker, switches the searchlight off, and
asserts **0 px** — against a noise floor also asserted 0 — at four headings. (The recipe, and the five
false reds that produced it, are #697/#702's.)

**That the lamps are where the rigs put them.** `BoatLampAnchorTests` pushes every shipped triple
through the runtime's projection and demands it land on the pixels the rig's own `navMounts` reports:
**27 hulls, 872 lamp/facing joins, worst disagreement 5.8e-5 px.**

---

## ⭐ What a plate caught that no test had

Two defects, both found by looking at the running game rather than at a number:

1. **The arrival hull read as moored** (see plate 01). `ArrivalOpening` builds her with a `MooredBoat`
   because that component is the game's drawer — its own comment says "She is not moored yet" — and a
   regime that read the component as a claim put the intro's whole light show out. `MooredBoat.Way` is
   now a *state* with a berth's default, and the arrival declares her under way when it builds her and
   moored when her lines go fast. Pinned by
   `ArrivalOpeningPlayTests.SheRunsInUnderHerNavigationLights_AndDousesThemWhenHerLinesGoFast`.

2. **A destroyed way-source read as "moored", not as "gone".** Letting twenty-five hulls go at Nine
   Mile Creek flipped all twenty-five sets of lamps and left all twenty-five searchlights out: an
   **interface** reference does not get Unity's fake-null operator, so each beam was still asking a
   component that no longer existed and believing the answer. The lamps were fine because they
   re-resolve through `GetComponent`, which does honour it. Pinned by
   `BoatLampRegimePlayTests.AWaySourceDESTROYEDUnderHer_ReadsAsGone_NotAsMoored`.

## Budget, measured at Nine Mile Creek (rule 7)

| | enabled light quads | searchlights burning | lamp-shadow pairs |
|---|---|---|---|
| **Moored** — the shipped state | **25** (one anchor light per lamped hull) | 0 of 24 | 0 active of pool 24 |
| **Under way** — all 25 let go | **125** | 24 of 24 | 24 active (pool saturates) |

25 hulls carry `BoatLamps`, declaring 174 lamp rows and building 150 `SceneLight`s (a `Spotlight` row
builds none — `BoatSpotlight` owns that lamp). A lamp the regime forbids costs **no quad at all**, not
a dark one, because `SceneLight` pools its quad across an enable cycle — which is what makes the
regime a saving of 100 quads rather than a change of colour.

**An honest follow-up, not taken here:** the shadow scan is now 25 lights × 592 casters at 10 Hz while
the fleet lies moored, and it produces **zero** shadows — an anchor light reaches 0.75 m and a sidelight
0.28 m, so neither can reach a caster's feet. Turning `CastsShadows` off for the small nav lamps would
retire that scan for nothing lost. It is left alone deliberately: it is a preset change, and the Cape
Islander's shipped look is this PR's control.

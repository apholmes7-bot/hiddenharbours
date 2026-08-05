# Hidden Harbours — grass species

10 STATIC sprites, five species, for the same wind / footstep vertex shader. No frames, no
pose sheets. Companion to `grass-tuft-kit` — the tuft set is one plant varied; this is five
different plants.

Individual PNGs: `GrassTuft_<Species>.png`. Atlas alternative: `GrassSpecies.png` (192×144)
+ `GrassSpecies.json` (rect, species, class, climb, stiffness per frame, plus every ramp).

## Contract with the shader

Unchanged from the tuft set:

- Every plant is rooted ON the bottom row of its canvas; blades grow upward.
- **climb** = how far the blades rise from that edge, so it is also how much bend the sprite
  takes (bend ramps 0 at the canvas bottom → max at the canvas top).
- Every lit pixel is 8-connected down to the bottom edge — nothing shears off when bent.

One addition: **stiffness**, a per-frame multiplier in the manifest for how much of the
shader's bend the sprite should take (1 = a normal grass tuft). A cattail stem is a rigid
pole and a saltmeadow mat is already laid over; both look wrong flapping at full amplitude.
One number, same shader, no opt-outs.

## Colour — one ramp per species, tinted in parallel

A species ramp is never invented. It is the base grass ramp with the hue rotated and the
saturation scaled, **lightness untouched**:

| species | hue | sat | ramp |
| --- | --- | --- | --- |
| base grass | ±0° | 1.00× | `#283a22 #3a542a #567834 #7ca248 #aac660` |
| cattail | +30° | 0.72× | `#25372a #304e33 #3e6e3e #5a9555 #7fb86e` |
| soft rush | +38° | 0.88× | `#23392c #2d5136 #387440 #4d9d52 #6ec066` |
| tussock sedge | −9° | 1.05× | `#2b3b21 #405529 #617a32 #8ba446 #bbc95d` |
| saltmeadow hay | −24° | 0.72× | `#313725 #474e30 #6a6e3e #949555 #b8af6e` |
| timothy | +9° | 0.60× | `#283527 #384c32 #506a42 #71905a #98b274` |
| *cattail spike* | −72° | 0.95× | `#392f23 #533c2b #764936 #a05d4a #c36a63` |

Every species therefore shares one value structure — the thing that carries readability at
32 px — and the runtime lush → straw knob multiplies over all of them identically. Sedge
stays yellower than rush at every point on the knob.

The cattail spike is the one deliberate off-ramp accent in the set. It is built the same way,
so the tint knob still moves it with everything else instead of leaving a brown island in a
straw field.

## Guarantees, asserted at bake time on every file

- alpha is 0 or 255, never between; no anti-aliasing
- zero pixels off the species' own five colours (spike excepted, above)
- no ground, soil, shadow or outline pixels
- widths 32 / 64, heights 32 / 48 / 64

| variant | size | species | class | climb | stiff | rect (x, y, w, h) | note |
| --- | --- | --- | --- | --- | --- | --- | --- |
| GrassTuft_Cattail | 32×64 | *Typha latifolia* | emergent | 58 | 0.35 | 0, 0, 32, 64 | four sword leaves, one spike |
| GrassTuft_CattailPair | 32×64 | *Typha latifolia* | emergent | 57 | 0.35 | 32, 0, 32, 64 | two spikes, leaves raked right |
| GrassTuft_Rush | 32×48 | *Juncus effusus* | rush | 42 | 0.5 | 0, 64, 32, 48 | stiff leafless spray |
| GrassTuft_RushLean | 32×48 | *Juncus effusus* | rush | 35 | 0.5 | 32, 64, 32, 48 | whole spray raked, shorter |
| GrassTuft_Sedge | 32×48 | *Carex stricta* | tussock | 38 | 0.85 | 64, 64, 32, 48 | fountain, blades arch over |
| GrassTuft_Timothy | 32×48 | *Phleum pratense* | culm | 45 | 0.6 | 96, 64, 32, 48 | bare culms, cylindrical heads |
| GrassTuft_TimothyLean | 32×48 | *Phleum pratense* | culm | 40 | 0.6 | 128, 64, 32, 48 | three culms nodding downwind |
| GrassTuft_SedgeLow | 32×32 | *Carex stricta* | tussock | 21 | 0.85 | 0, 112, 32, 32 | same fountain, half height |
| GrassTuft_Saltmeadow | 64×32 | *Spartina patens* | mat | 18 | 0.3 | 32, 112, 64, 32 | the cowlick, two whorls |
| GrassTuft_SaltmeadowSwirl | 64×32 | *Spartina patens* | mat | 19 | 0.3 | 96, 112, 64, 32 | one whorl, all combed left |

## Suggested biome mixes

- **Freshwater marsh** — cattail, soft rush, low sedge, fringe tuft
- **Salt marsh** — saltmeadow hay mats, rush at the creek edge, short tuft
- **Wet meadow** — sedge tussocks, timothy, medium tufts
- **Hay meadow** — timothy over the existing short and medium tufts, seed heads
- **Dune edge** — marram, saltmeadow mats in the hollows, fringe

## Regenerating

`Art/grassSpeciesRig.js` is the generator. A blade here is a cubic Bezier walked at sub-pixel
steps, with thickness stamped across the local tangent — horizontally where the blade is
steep, vertically where it has laid over — so a 2 px blade stays 2 px wide all the way round
an arch instead of fattening as it turns. That is what lets sedge turn over and saltmeadow lie
down. Change a seed for a new arrangement in the same style; add an entry to `VARIANTS` for a
new one.

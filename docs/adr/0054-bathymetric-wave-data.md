# ADR 0054 â€” Bathymetric wave data

- Status: **Proposed**. Stage D part a supplies the bake; part b supplies its consumers.
- Date: 2026-10-22.
- Steward: lead-architect, gameplay-systems, art-pipeline, tools-editor and qa-test.
- Serves P1 and P5. Related: ADRs 0014, 0018, 0040, 0046 and 0047.

## Decision and scope

Use an offline eikonal solve over each painted seabed, with one first arrival per incoming
direction and water level. Store its travel direction, shoaling/shadow height factor, and
continuous optical distance. Refraction turns crests toward the shallows; transport reduces
energy where the first front spreads behind land. A single arrival cannot represent two
crossing arrivals. This is a bounded approximation, with the costs and gaps below.

W4-6a adds Core's `WaveBathymetry`, `WaveBathymetryData`, the editor source intake, importer,
loader and four recipes under `Assets/_Project/Art/Terrain/Bathymetry`. Nothing samples this
data in a scene, service, shader or player build. No existing wave test changes. W4-6b will
amend ADRs 0018 and 0040 when the runtime work lands; this proposal changes neither ADR now.

## Pure transform and numerical contract

`Core/Environment/WaveBathymetry.cs` takes a bottom-up elevation grid in millimetres above
chart datum, its world rectangle, cell size, still levels, water levels and Q15 incoming
unit vectors. It uses signed 64-bit integer arithmetic, integer square roots rounded down,
and a priority heap ordered by (optical distance, row-major cell index). Divisions truncate
toward zero. There are no floating transcendental calls, engine calls, clocks or random state.

The speed ratio is n = sqrt(entryDepth / max(depth, minimumDepth)). Entry depth is the deepest
eligible water in that slice, bounded by the recipe's offshore depth cap. Using this depth
also at the incident boundary avoids an invented refraction at the rectangle of a flat bed.
Dry cells and still water standing above the tide are excluded. Enclosed water without a
wet incoming boundary has no arrival. When the tide rises through a still level it can be
part of the sea only if a wet path reaches it.

The four-axis monotone eikonal update solves |grad D| = n. Only wet boundary cells whose
inward normal faces the incoming vector are seeded. Their incident D is dot(position, bin).
The upwind gradient supplies direction. A finite-volume approximation transports H^2*c
along the accepted characteristic graph; divergence spreads energy, convergence focuses it.
The shoaling amplitude is sqrt(n) times the transported flux amplitude, limited by the cap.
This transport is a grid approximation, not a calibrated surf or diffraction model.

Path values use 1/1024 optical metre; direction components use signed 1/32767; height uses
1/4096. The source intake bilinearly samples raw PNG codes with decimal arithmetic, using
the texel-centre law of `PaintedHeightField`, then rounds millimetres halfway away from zero.
Eight-bit codes divide by 255 and sixteen-bit codes by 65535. Rows flip exactly once.
The millimetre quantization differs from the game's float elevations by at most 0.51 mm
on the production sampling check. This tolerance is a test requirement, awaiting Unity.

The output is little-endian linear RGBAFloat: (direction x, direction y, height factor,
optical metres), 16 bytes per cell. Slice = waterLevelIndex * binCount + binIndex. Rows are
bottom-up, x fastest. Output floats come only from quantized integers; binary fractions for
height and path are exact; optical distance beyond +/-16384 m is refused before precision
would be lost. Q15 component rounding leaves
a small unit-length error; consumers normalize the decoded vector. For a frequency omega,
S = omega * D / sqrt(g * entryDepth), so grad S is the local wavenumber in the long-wave
limit. Entry depths are retained per level. Phase is unwrapped; it is never an angle modulo
2*pi in the asset. Attenuated wet cells retain phase even when energy rounds to zero;
excluded/unreached cells have zero pixels and must be masked before interpolation.

The continuity guard is h * sqrt(offshoreDepthCap / minimumDepth) + two path quanta:
40.001953 optical metres for a 4 m cell with these recipes. This is a spatial Lipschitz
bound, not a claim that a shallow wave has fewer than one crest per cell. First arrivals
can meet at a continuous phase with a kink in its gradient.

## Imported under ADR 0047

Commit the four JSON `.hhbathymetry` recipes and stable metadata. Importer version **1**
owns stable subobject identifiers `BathymetryCpu` and `BathymetryGpu`. The CPU object holds
layout and a byte array; the GPU object is a linear RGBAFloat array without mips. GPU staging
bytes are checked against the CPU payload before applying and discarding texture readability.
There is exactly one retained CPU pixel copy. Both objects live in Library import artifacts;
there is no generated pixel file under Assets, no build-time writer and no committed bake.

Dependencies include the recipe automatically, its seabed asset, its height PNG, its optional
still PNG, and these nine code sources:

- `Core/Environment/WaveBathymetry.cs` and `WaveBathymetryData.cs`;
- `Art/Editor/WaveBathymetrySource.cs`, `WaveBathymetryImporter.cs`, `WaveBathymetryAssets.cs`;
- `World/PaintedHeightMap.cs`, `World/PaintedHeightField.cs`;
- `Core/Environment/SeabedBakeMath.cs`, `Core/Environment/IStillWater.cs`.

Paths above are under `Assets/_Project/Code` except the editor files, under `Assets/_Project`.
Declare source dependencies before reading their bytes. Increment the importer version for
decode, solve or layout changes. Intake accepts only materialized, non-interlaced greyscale
8/16-bit PNGs with IHDR/IDAT/IEND, valid CRCs, zlib checksum and exact inflated length.
Other chunks, formats, malformed asset fields and unsupported project paths fail by path.
It changes no source import setting. Nothing is published until all slices and both objects
succeed. The required loader checks the import error log before serving retained objects;
ordinary loads never cause a reimport. The menu is an explicit rebuild action.

CI must run the independent cardinal planar bytes, prescribed 1D optical distances/height,
independent PNG filter/code/checksum fixtures, determinism, production phase bounds, raw
PNG versus painted-field checks, production import/Core bytes, dependency and stable-object
checks. This supplies an independent expectation in addition to comparing importer and Core.
No missing input is skipped. Unity import and Windows/Linux CI remain validation steps.

## Recipe values

All balance controls are recipe data. Bounds below are validated; omitted values are zero
and refused unless the table explicitly gives zero a meaning. Format limits such as integer
precision, heap ordering, PNG filters and maximum array extents are versioned code contracts.

| Field | Unit and range | Current value; zero meaning; reason |
|---|---|---|
| schemaVersion, id, seabedPath | schema 1; stable bathymetry id; project asset path | Each region has its own id and authored source; zero/empty refused. |
| cellSizeMm | mm, 1000..32000; whole cells in rectangle | 4000; zero refused; 8000 is the mobile fallback. |
| directionX, directionY | signed Q15 unit pairs, 1..64 bins | 16 bins at 22.5 degree intervals, clockwise from north; a zero component is cardinal, a zero vector is refused. |
| waterLevelsMm | mm above datum, -100000..100000, 1..17 strictly increasing levels | Range -2200..2200; zero is mean tide; counts below. |
| offshoreDepthMm | mm, 100..100000 | 10000 maximum entry depth; zero refused; prevents unbounded optical scale. |
| minimumDepthMm | mm, 10..offshore cap | 100; zero refused; bounds the shallow speed singularity. |
| dryDepthMm | mm, 0..minimum depth | 50; zero admits every positive depth; avoids transporting sea through tiny wet slivers. |
| heightCapMilli | thousandths, 1000..8000 | 2500 (2.5x); zero refused; limits grid focusing. |
| transportFloorMilli | thousandths, 100..1000 | 500; zero refused; bounds each convergent transport denominator. |
| openEdges | bit mask 1..15: west 1, east 2, south 4, north 8 | 15; zero refused; only submerged cells on incident-facing edges seed waves. |

No GameConfig value is needed until W4-6b has a runtime control to tune.

## Wavelength choice

Keep one long-wave field per bin and level for this bake. A simple depth/wavelength direction
blend is insufficient for the shortest trains. On a planar beach at 60 degrees to shore-normal,
compare w = clamp(1 - 2*d/lambda, 0, 1), normalized vector blending, against a separate Core
eikonal solve using finite-depth dispersion omega^2 = g*k*tanh(k*d). Independent conserved
tangential-wavenumber traces agree with the numerical results. Direction errors in degrees:

| Offshore wavelength | 0.5 m depth | 2 m depth | 5 m depth |
|---|---:|---:|---:|
| 5 m | 17.55 | 5.99 | 0.00 |
| 8.343 m | 13.33 | 12.71 | 0.07 |
| 30 m | 3.81 | 4.85 | 3.66 |
| 40 m | 2.62 | 3.19 | 2.35 |

Five metres is the base ladder's shortest rung; 8.343 m is the nominal shortest surviving
wind rung after the two short slots become swell; its interior stratum also jitters by seed.
Forty metres is the swell's longest rung. The long/swell approximation is useful, but W4-6b
must improve the short-wave blend using dispersion and must derive direction/slopes from a
consistent blended phase. Blending a depth-varying scalar phase also differentiates its blend
weight; merely rotating vectors while retaining a straight phase is insufficient. Frequency
layers require a further cost decision, not an implicit promise of the simple blend.

## Tide choice and costs

Use the fewest of 2, 3 and 5 levels meeting a **10 degree p95** nearshore direction budget
on this sampled sweep. Compare a direct Core solve every 0.275 m over -2.2..2.2 m with normalized
linear direction blending of the enclosing levels, across all 16 bins. The nearshore band is
0.5..2 m depth. Statistics condition on nonzero energy in the direct and both enclosing fields.
This is a sampled statistical budget, not a uniform continuous-tide guarantee. Drying/support
changes and switching arrivals can reach 180 degrees; neither is hidden by the p95 statistic.

Each memory value below is decimal MB for **each** of GPU and CPU. Their sum is twice the value.

| Region | 2 levels: p95 / MB | 3 levels: p95 / MB | 5 levels: p95 / MB | Chosen |
|---|---:|---:|---:|---:|
| StPeters | 20.30 deg / 12.646 | 16.63 deg / 18.970 | 9.37 deg / 31.616 | 5 |
| NineMileCreek | 24.98 deg / 13.619 | 16.81 deg / 20.429 | 8.24 deg / 34.048 | 5 |
| RicePoint | 18.07 deg / 8.755 | 7.86 deg / 13.133 | 3.22 deg / 21.888 | 3 |
| Cumberland | 14.59 deg / 13.619 | 6.33 deg / 20.429 | 2.69 deg / 34.048 | 3 |

Chosen levels are -2.2, -1.1, 0, 1.1, 2.2 m for St Peters and Nine Mile Creek; -2.2, 0, 2.2 m
for Rice Point and Cumberland. Nonzero support coverage of the chosen interpolation is
98.3%, 95.0%, 76.5% and 88.1% respectively. The corresponding all-grid support disagreement
is 1.6%, 1.7%, 4.3% and 0.7%. W4-6b must query depth/still water and handle changing support;
these recipes do not authorize treating missing or dry endpoints as ordinary phase values.

The four height inputs are 1520x1040 16-bit, 1520x1120 8-bit, 1520x720 16-bit and 1520x1120
16-bit respectively. World rectangles are 760x520, 760x560, 760x360 and 760x560 metres, all
centred at (0,0) in their region frames. Elevation ranges are -4..12, -6..6, -6..6 and -6..6 m.
Coddle Cove has no painted height input, so it receives no recipe; a colour texture cannot
substitute for seabed elevation. Its eventual flat/no-bake fallback belongs to W4-6b.

| Region | 4 m cells | Bins / levels | GPU MB | CPU MB | Decode + solve/serialize seconds | 8 m cells / MB each / solve seconds |
|---|---:|---:|---:|---:|---:|---:|
| StPeters | 190x130 = 24700 | 16 / 5 | 31.616 | 31.616 | 0.050 + 0.450 | 6175 / 7.904 / 0.094 |
| NineMileCreek | 190x140 = 26600 | 16 / 5 | 34.048 | 34.048 | 0.016 + 0.295 | 6650 / 8.512 / 0.049 |
| RicePoint | 190x90 = 17100 | 16 / 3 | 13.133 | 13.133 | 0.048 + 0.107 | 4275 / 3.283 / 0.014 |
| Cumberland | 190x140 = 26600 | 16 / 3 | 20.429 | 20.429 | 0.122 + 0.127 | 6650 / 5.107 / 0.007 |

Per level the GPU and CPU costs are respectively 6.323, 6.810, 4.378 and 6.810 MB **each**.
Only loaded regions need retention. The 8 m fallback rebuilds from the height inputs with
one quarter of the pixels; it is a cost option, with no look acceptance implied. Timings are
net10 headless CPU intake/solve/serialization, including output hashing at 4 m. Unity artifact
publication, texture creation and cold editor import overhead are unmeasured until Phase B.

## Region entries and seams

Each direction enters through submerged incident-facing cells on eligible rectangle edges.
This is a per-region boundary condition. It is not the held ADR 0049 domain across regions.
At the Nine Mile Creek/Cumberland bay seam at mean tide, 502 joint nonzero samples across
16 bins have direction p50/p95/max 0.85/46.92/56.78 degrees. After the known frame translation,
optical-distance mismatch is 2.84/70.31/100.31 m; at a 40 m optical wavelength the wrapped
phase mismatch is 25.51/156.22/178.42 degrees. A matching bed does not provide a matching
incoming phase history. A single scalar phase offset cannot repair all bins and positions.

At the Nine Mile Creek/Rice Point sea band there are **zero joint wet samples** at mean tide:
the committed creek height input is +6 m at the west edge; Rice Point's east edge is
-3.321..-2.242 m over the corresponding 4 m sample centres. The bed gap is 8.242..9.321 m.
Direction and phase mismatch there are undefined, not zero. The frame relation is creek =
Rice Point + (-760,-100) m; Cumberland = creek + (0,-560) m. No source asset is changed here.
Resolve the source seam and the phase-domain decision before claiming a seamless runtime sea.

## First arrival, shadow and competing arrivals

Do not add a second production layer. A diagnostic seeds the two incident-facing edges
separately at bearings 22.5, 45, 135, 225 and 315 degrees. Count cells with two nonzero arrivals,
at least 30 degrees apart and at most 40 optical metres apart. This is an incoming-edge
competition probe, not a complete census of two paths from the same edge around an island.
It can miss such paths and cannot establish the full energy of a second physical arrival.
Its measured water/energy support is a lower-scope estimate for pricing that later decision:

| Region | Maximum competing water share | Second energy in competing cells | Second energy over the whole sampled sea |
|---|---:|---:|---:|
| StPeters | 6.46% | 9.32% | 0.388% |
| NineMileCreek | 5.18% | 10.86% | 0.493% |
| RicePoint | 5.60% | 24.15% | 1.189% |
| Cumberland | 12.87% | 9.13% | 1.030% |

A second layer doubles pixel memory: add the chosen GPU and CPU costs in the table above.
The first layer's shadow is deliberate; it does not manufacture outgoing full-height crests
at a lee waterline. Diffraction, reflected energy and two-branch interference remain outside
this first-arrival approximation. The probe is not sufficient evidence to call a second
arrival negligible merely because its whole-domain energy share is small.

## Shore evidence and acceptance limits

Headless maps use mean tide at incoming travel bearings 40 (St Peters) or 50 (the other
regions), 325 (the swell), and their perpendiculars 130/140 and 55 degrees. For shore shares,
march the zero-elevation contour on the 4 m lattice, weight by contour length, exclude
elevated still water, and sample one cell seaward. A crest is face-on within 45 degrees of
the shoreward height gradient. These are measurement conventions, not production controls.
North/east/south/west below describe the **shoreward normal**, not a region quadrant.
The straight-field column evaluates today's directions on the same bed and contour.
The bake column includes any positive energy; energetic arrivals additionally need a height
factor of at least 0.25. Shadow is factor below 0.25, including no arrival.

| Region, prevailing direction | Shoreward normal | Contour m | Straight face-on | Baked face-on | Energetic face-on | Shadow |
|---|---|---:|---:|---:|---:|---:|
| StPeters, 40 deg | north | 595.8 | 68.2% | 80.6% | 75.9% | 8.6% |
| StPeters, 40 deg | east | 187.9 | 19.3% | 65.0% | 41.0% | 40.8% |
| StPeters, 40 deg | south | 571.8 | 0.0% | 66.3% | 15.9% | 76.9% |
| StPeters, 40 deg | west | 190.1 | 0.0% | 50.8% | 15.2% | 72.8% |
| NineMileCreek, 50 deg | north | 681.9 | 38.9% | 38.4% | 38.4% | 61.6% |
| NineMileCreek, 50 deg | east | 148.8 | 83.3% | 1.4% | 1.4% | 91.0% |
| NineMileCreek, 50 deg | south | 702.6 | 0.0% | 25.8% | 2.0% | 95.0% |
| NineMileCreek, 50 deg | west | 685.6 | 0.0% | 12.0% | 1.6% | 78.9% |
| RicePoint, 50 deg | north | 788.2 | 58.6% | 96.1% | 91.4% | 5.6% |
| RicePoint, 50 deg | east | 42.6 | 77.7% | 42.7% | 42.7% | 0.0% |
| RicePoint, 50 deg | south | 39.9 | 0.0% | 0.0% | 0.0% | 57.1% |
| RicePoint, 50 deg | west | 97.4 | 0.0% | 69.7% | 55.3% | 18.6% |
| Cumberland, 50 deg | north | 329.9 | 20.5% | 88.3% | 61.8% | 32.1% |
| Cumberland, 50 deg | east | 88.3 | 87.6% | 63.0% | 16.6% | 71.9% |
| Cumberland, 50 deg | south | 80.1 | 0.0% | 59.0% | 3.3% | 88.0% |
| Cumberland, 50 deg | west | 222.9 | 0.0% | 50.7% | 47.6% | 14.7% |

St Peters' windward shore turns in and the lee receives a weaker first front around both
sides. Nine Mile Creek retains a large shadow, especially its east/south/west normal
segments; this bake does not establish arrival at every creek beach. Rice Point's long
northward-normal shore improves strongly; its southward-normal segments remain unserved
for the prevailing bin. Cumberland improves most normal groups, with weak southward-normal
arrivals. Thus the direction maps support refraction, not full Stage D visual acceptance.
The earlier water report used different input geometry and sampling; its leaving-shore
shares are not interchangeable with this contour-length face-on measure.

Maximum adjacent optical steps over the sampled tide sweep are 30.676, 35.180, 35.621 and
35.605 m respectively, within the 40.002 m theoretical guard. Dry/unsupported cells are not
given an invented connecting phase. Evidence maps, full four-bearing shore tables, hashes,
timings, tide sweeps and seam probes are local untracked evidence, outside the source tree.

## W4-6b plan

1. Settle the short-wave approximation, support transitions and the creek source-seam gap.
   Revisit independent region entries before promising phase continuity during travel.
2. Define the Core sampling seam and scene lifetime for the imported CPU/GPU payloads.
   Keep deterministic simulation reads on (seed, game time, position); save no wave state.
3. Move `WaveMath.Sample`, the shader train loop and `WaveFieldBridge` together. Preserve
   CPU/GPU phase, height and slope agreement, including bin and tide interpolation derivatives.
4. Move dominant-train direction reads in breakers, shore fade, shoreward steering, spray,
   boat motion/forces, buoy, trap, vehicle, reflection and foam consumers to that same seam.
   Wind fetch and swell shelter must compose with transport without suppressing energy twice.
5. Amend ADRs 0018 and 0040, then validate all shores, tide extremes, calm/swell, bin crossings,
   transition support, region travel, GPU cost and the 8 m mobile option in granted editor runs.

Phase A uses no Unity process and makes no player-visible change. Phase B must cold-import
all four recipes, run `WaveBathymetryTests`, `WaveBathymetryImporterTests`,
`TerrainArrayImporterTests`, `TerrainArrayReferenceTests`, and verify that the play-mode
build loads no bathymetry. Full import lifecycle, GPU allocation and CI parity remain pending.

## Shores that lose an incident sea, and the W4-6b decision

The bake carries a sea entering the region rectangle. Today's wind-fetch field can grow a
sea inside that rectangle. Those are different sources of energy. At mean tide, every
contour-length face-on regression in the four-bearing probe is listed below; shadow means
height factor below 0.25, including no arrival. Normals name the shoreward direction.

| Region | Travel bearing | Shoreward normal | Today | Bake | Shadow |
|---|---:|---|---:|---:|---:|
| StPeters | 325 deg | north | 89.3% | 88.4% | 2.6% |
| NineMileCreek | 50 deg | north | 38.9% | 38.4% | 61.6% |
| NineMileCreek | 50 deg | east | 83.3% | 1.4% | 91.0% |
| NineMileCreek | 325 deg | north | 82.9% | 63.8% | 45.3% |
| NineMileCreek | 140 deg | east | 16.7% | 0.0% | 74.5% |
| NineMileCreek | 140 deg | south | 56.7% | 47.7% | 81.3% |
| NineMileCreek | 55 deg | east | 88.6% | 1.4% | 91.0% |
| RicePoint | 50 deg | east | 77.7% | 42.7% | 0.0% |
| RicePoint | 140 deg | south | 91.4% | 0.0% | 100.0% |
| RicePoint | 55 deg | east | 77.7% | 42.7% | 2.0% |
| Cumberland | 50 deg | east | 87.6% | 63.0% | 71.9% |
| Cumberland | 325 deg | west | 75.9% | 74.3% | 5.3% |
| Cumberland | 140 deg | east | 12.4% | 0.0% | 100.0% |
| Cumberland | 140 deg | south | 79.7% | 0.0% | 100.0% |
| Cumberland | 55 deg | east | 87.6% | 63.0% | 71.9% |

The source geometry explains the principal losses. Nine Mile Creek at 50 degrees has zero
wet incident west-edge cells and 83 south-edge cells; the sea enters behind the spit and
spreads into the creek/basin. Its eastward-normal shore falls from 83.3% to 1.4%, with 91.0%
shadow. At 325 degrees both the east and south edges have water, but the spit and enclosed
basin still limit the arriving front; the northward-normal share falls from 82.9% to 63.8%.
Rice Point has 30 wet west-edge cells and 190 south-edge cells at 50 degrees. The point and
inlet rotate the arriving front: its eastward-normal share falls from 77.7% to 42.7% despite
0% shadow there, so this loss is directional, rather than missing energy. At 140 degrees
its north edge is dry and only 30 west-edge cells seed the front; its southward-normal
shore is fully shadowed. Cumberland at 50 degrees has zero wet west-edge cells and 82
wet south-edge cells; its eastern indentation/spit weakens the eastward-normal shore. At 140
degrees its incident west and north edges both have zero wet cells: there is no seed, no
arrival, and 100% shadow on every shore normal. St Peters' smaller losses occur where the
bar/island obstructs or diverts an incident front, rather than a sea growing on that shore.
The edge counts use the recipe's 50 mm dry threshold on the 4 m grid, excluding elevated
still water. The bake is unchanged by this diagnosis.

W4-6b must decide how the wind sea grown inside a region uses refraction when the incoming
field has no arrival. Lean: retain the existing fetch-grown wind sea where no arrival
exists, preserving its straight phase and energy; use a consistent refracted phase only
where an incoming characteristic supports it. Keep swell tied to incoming transport.
Any transition must preserve continuous phase/height and avoid applying shelter twice.
This fallback needs an explicit sampling contract and tests; first-arrival shadow alone
must not erase wind energy generated inside the region. A later design can add distributed
sources or a larger sea domain; neither is added in W4-6a.

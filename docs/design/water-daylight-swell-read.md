# W1: bounded swell contrast, default off

Phase B, approved by the owner on 2026-09-29 after the seat's Phase A check.
Base: `f0d88ebe36882f9adc746f85061ffdf436a72c75`. Serves P1 (read the sea the
hull rides) and P5 (rough water can remain dark). **No look setting is chosen here.**
The implementation is GPT (Codex)'s; Phase C needs a separate editor grant.

## Law and default contract

`HiddenHarboursWater.shader` keeps the existing signed wave signal, the
0.10/0.45 calm smoothstep, the strength/gate > .001 guard and the optional
posterizer. After quantization, `[ToggleUI] _SwellReadRelative`, default **0**,
selects one of two operations. It introduces no shader keyword or variant.

* Off: the original `col.rgb += readBand * _SwellReadStrength * swellReadGate * 0.25`,
  with the same arithmetic order. There is no multiply-by-one replacement.
* On: `C + max(C,0) * (clamp(readBand,-1,1) * saturate(strength) * gate)`.
  `C` is the incoming RGB at this read site, after stock swell and before face
  shading, the envelope and the water palette grade.

For each nonnegative channel, the new read is bounded by `(1 ± S*g)*C`.
At strength .35 a trough removes at most 35% of that channel; at .20, at most 20%.
Negative channels are left alone. There is no positive floor: exact black cannot
gain a visible crest from this layer. The helper does not change depth, clipping,
deep tint, water level, wave sampling, slope or simulation.

The switch is held still across weather, like existing read strength. It is owner
look policy and is deliberately absent from `WaterSurface.MoodFloatNames`. No
material or preset is edited; all inherit the shader's off default. Values below
.5 select off and values at/above .5 select on; there is no intermediate blend.

Two zero contracts must remain distinct: **adoption off keeps today's law**;
**read strength zero bypasses the read**, and does not reproduce the shipped .35
picture. The C# reference and helper early-return on inactive strength/gate.

## Why the change is an option, not a brightness fix

The current read normalizes with `x=saturate(height/totalAmplitude)` then remaps
`2*x-1`. Every nonpositive height is maximally negative, and positive heights
below half the total amplitude are still negative. This is not a centered height
signal. The physical field and this read signal stay unchanged.

Today's full-gate swing at .35 is ±.0875, regardless of the sea below it. A
hypothetical neutral .04 input can therefore become -.0475. Under the relative
read its endpoints are .026/.054. These are pre-grade arithmetic examples, not
frame measurements. The palette grade is a soft rail; later blending, grading and
LDR clipping can still hide contrast. Stock swell, face shade and envelope remain
independent contributors.

In the Phase A single-train, full-fetch, uniform-phase model with the shipped
field sharpening 2.6, physical trough occupies 67.85% of phase, the negative read
79.02%, and mean signed read is about -.59734. **These are not the multi-train
blow's pixel areas or a layer's measured share.** Real areas await matched plates.

Relative contrast still darkens a negative-biased no-read body, but often raises
its mean compared with today's larger absolute subtraction. That mean change is
the owner's decision. No automatic compensation is added to enforce a target.
Historical September 10 #829 / `624124c7` figures (.0098 shipped wet luma, .0431
read-off, 37% band-contrast loss when off) are not measurements of this base.

## Options to price

Fixture sea states glass/light/blow/gale are 0/.25/.55/.95; their read gates are
0/.393586/1/1. The read has no new day/night gate. Noon and golden differ through
the existing sun-side and downstream layers; night still uses the existing grade.

| Arm | Glass | Light airs | Blow and gale | Night | Source-level cost |
|---|---|---|---|---|---|
| d35: existing .35 | Unchanged | Absolute swing ±.03444 | ±.0875 | Same read law before grade | Existing work |
| d20 / d10 | Unchanged | ±.01968 / ±.00984 | ±.05 / ±.025; both darkening and crest lift shrink | May alter mean/contrast | No added ALU |
| d00 | Unchanged | This read absent | Other face/envelope terms remain | Read absent | Existing guard can skip the read |
| r35 / r20: relative on | Unchanged | At most 13.78% / 7.87% of positive input | At most 35% / 20%; may raise mean versus d35 | No new night light/floor | Roughly 10–16 simple scalar ALU in active helper plus uniform selection; no new samples/RTs |
| a: **near-off (fade 64)**, with each arm | Same chop response | More mirror | Chop factor near 1, still subject to master and wind dim | Can affect night reflections | Existing MPB override; may activate more existing reflection work |

`_ReflectionFadeChop=0` suppresses reflections on non-glass seas; it does **not**
remove the fade. The approved (a) experiment uses 64 via MPB, labelled **near-off
(fade 64)**. For chop in [0,1], its remaining chop factor is at least .999275.
It is not exact off. No reflection helper, property, twin overload or reflection
test is added. An exact bypass is a later implementation only if the owner picks
(a), and would need its own default-preservation tests.

The optional posterizer uses a Float band count. Integer counts preserve the
signed range; 1.6 can produce 1.5 at a crest. Only the relative helper clamps that
overshoot. Off retains the old posterizer/add exactly. This is intentional and
tested; no global posterization fix is hidden in W1.

## Proof and CI forecast

Production helper: `Assets/_Project/Art/Shaders/Include/DaylightSwellRead.hlsl`.
Arithmetic reference: `HiddenHarbours.Art.DaylightSwellRead.Apply` and `Relative`.
The reference owns no wave state and allocates nothing per call.

`DaylightSwellReadTests` uses the production C# reference, inequality bounds and
hand-worked endpoints rather than a test-only expected-value implementation.
It also checks actual imported material defaults/preset copying and pins the
shader's call, original quantizer, gate, unchanged legacy add and pre-grade
placement. Those source checks are wiring evidence, not proof of GPU arithmetic.

`DaylightSwellReadConformanceTests` executes the production HLSL include through
the test shader and compares floating signed/HDR results with the C# twin. The
test shader retains f0d88ebe's original quantizer/add as its legacy oracle; the
candidate formula is **not duplicated** in it. Off versus legacy is bit-compared
on the same backend. This small float target does not certify the full water
shader's half precision, final look or pass cost; those still need Phase C.

| New test (prefix `DaylightSwellRead_`) | Cases | Expected on Null-device CI |
|---|---:|---|
| HasBoundedSignedContrast | 4 | Passed |
| ZeroStrengthPreservesBaseline | 2 | Passed |
| PreservesCalmGate | 4 | Passed |
| DefaultDialsAndShaderWiringPreserveLegacy | 1 | Passed |
| PosterizedEndpointCannotEscapeItsBound | 1 | Passed |
| NegativeInputDoesNotCreateLight | 1 | Passed |
| SwitchHasNoIntermediateLaw | 1 | Passed |
| ShaderAndTwinAgree | 1 | Skipped, explicitly **NOT VERIFIED** |

No existing test is moved, retired or weakened. Existing onset, face, sun-side,
bore, reflection-ladder and sweep subjects stay unchanged. Against the seat's
f0d88ebe baseline (run 36504563216), forecast EditMode **13389/13136/253/0** and
unchanged PlayMode **1007/923/84/0** (total/passed/skipped/failed). The extra skip
is named above; headless CPU/source tests cannot substitute for it.

Phase B does not run Unity locally. CI compiles/runs headless checks. Shader and
full-water visual acceptance remain unverified until the named owner slot.

## Phase C: trimmed series, then re-estimate at the grant

The owner's A1–A4 amendments supersede the larger Phase A schedule. Nothing is
reserved now. New GPU plate/timing harness work belongs to the separately granted
Phase C; the conformance test is already prepared in Phase B.

Start by rerunning `TheBlowIsWhereTheSeaGoesOut_MeasuredKnobByKnob` at default-off
adoption: 56 baseline/ablation shots on this base. Also enumerate current shader
weights at the unpinned open-water blow/noon cell, including face shade separately,
stock swell and envelope; at this base 54 weights + face + baseline + residual =
57 shots. Report conditional ablation deltas, not additive shares summing to 100%.
Label the reference as settled-main look on the PR's opt-out path with both SHAs.

Never change the editor process working directory. The existing fixture may
write to this box's git-ignored `artifacts/`; afterwards move only its named
output into this box's `Evidence~/w1-plates/<run>/`. Check both resolved paths
before moving. Do not change the #905 sweep file: its terrain arrays now load
through `TerrainArrayAssets.LoadDetailRequired()` rather than rebuilding.

Main arms: `{d35,d20,d10,d00,r35,r20}` crossed with `{f06,f64}` = 12.
Every f64 caption says **near-off (fade 64)**. Relative strength zero aliases d00.
Frame names: `{view}-{sea}-mean-{hour}-{master}-p{phase}-{read}-{fade}.png`.
Use the existing ww-open camera and Nine Mile Creek sand camera, same mean tide,
same seed and matched field/time within each comparison. Golden is profile-derived
(historically ~17:00); noon is 12:00, night is 02:00/new moon.

| Group | Cartesian product / named subset | Count |
|---|---|---:|
| Charter anchor frames | 2 views × 4 seas × noon/golden × pinned masters .70/.15 × 12 arms | 384 |
| Rough sequences | Same but blow/gale only, two additional phases at dominant period /3 and 2/3 | 384 |
| Live blend subset | ww-open, blow/gale, noon/golden, phase0, 12 arms | 48 |
| Night subset | ww-open, glass/blow/gale, both pinned masters, phase0, 12 arms | 72 |
| Noise repeats | 42 phase0 contexts above, d35-f06 repeated twice | 84 |
| Posterized controls | ww-open blow noon, both pinned masters, d35/r35, f06/f64, bands 1/4/1.6 | 24 |
| Clamp controls | ww-open blow noon, d35, both masters, fade1 and repeat fade64 | 4 |
| Existing knob instrument + full weight baseline | Counts above, re-enumerated at settled base | 113 |
| **Total at f0d88ebe** | 888 candidate frames plus 225 controls/diagnostics | **1113** |

Hash every PNG and floating readback; manifest includes shader/material/config
hashes, both SHAs, clock, seed, camera, API, GPU, effective MPB and actual renderer.
Write the treatment block after the production push and restore it between arms.
Verify the actual flat/displaced renderer consumes the switch. Identical positive
control hashes invalidate a claimed effect; glass identity is expected. Quantized
PNG equality alone is insufficient, and time-driven hash differences prove nothing.
Keep fade1/fade64 and pinned-master discriminators. Compare arms only within a run.

At 960x960 preserve existing wet-luma and full-frame row-mean standard deviation
(the old 37% metric). Report crest/trough and sunward/lee contrast, h<0/b<0/b>0
area fractions and empty-population validity, using the actual field/coordinates;
displaced-screen pixels need displaced-coordinate mapping. Keep a fixed dark-body
mask per context, report its coverage, and show all-wet metrics as well. Retain the
water palette grade and existing day/night multiply. The fixture omits MoodGrade;
owner acceptance also needs the finalists through the gameplay volume. No historic
luma target is silently adopted. Save REVIEW.html with named side-by-side arms.

GPU pricing is **endpoint-only**, at 1920x1080: d35-f06, d00-f06, r20-f06, r35-f06,
d35-f64 and r35-f64; both masters, ww-open blow/gale at noon/golden (48 conditions),
plus ww-open glass/noon controls (12). Sixty warmup frames and three 120-frame
windows per condition have a theoretical ~7-minute floor at 60 fps. Record median,
p95, paired baseline deltas and noise from isolated **water GPU events**, not CPU
stopwatches or the aggregate `HH IsoFacet Hulls` marker. If isolated GPU timestamps
are unavailable, report NOT VERIFIED and leave performance acceptance open.

Planning estimate: **60–90 minutes**, to be recalibrated at Phase C's turn for
imports, capture/export, profiling support and cleanup. This is not a slot request
or grant. Respect editor exclusivity, D3D11, free disk/commit headroom and shared-save
SHA/size/WorldSeed checks. Return exact editor PIDs closed, unchanged save seed and
lowest commit headroom. The known main GPU sun-side red is reported, not repaired.
No defaults ship until the owner rules from plates; Phase D material changes also
wait for #909's merge confirmation.

## Phase D: owner's d20 (2026-10-07)

The owner ruled, verbatim, "ok lets go d20" after reviewing Phase C. The chosen
arm is **d20-f06**: the existing absolute read, `_SwellReadRelative = 0`, at
`_SwellReadStrength = 0.20`; `_ReflectionFadeChop` stays **0.6**. Part A changes
only the strength in `Water.mat` and the shader Properties default, both
**0.35 -> 0.20**, plus their comments, the default/copy test and this record.
The branch starts at main `032f6239310707ff22dab20852351fcc16103226`.

All eight `WaterPresets/*.mat` and their eight `.preset` twins omit
`_SwellReadStrength` and use the water shader. The editor's "Apply to live Water"
uses `WaterPresetMenu.ApplyVariant`'s `CopyPropertiesFromMaterial(variant)`;
changing only `Water.mat` would let an unapplied shader default of .35 return.
`DisplacedWaterSurface` constructs from the live material and re-copies it in
`SyncUniforms`. `WaterSurface` reads mood keys individually; its line 292 copy
reference is a comment about the editor menu, not another runtime copy. The
strength is absent from `MoodFloatNames` and stays so. The default-dials test now
pins .20 on a bare material and on a scratch copy of the live asset and every
preset, after deliberately seeding that scratch with the superseded .35.

**GPU position:** Phase C priced **0/60: NOT VERIFIED**. Source inspection of
`HiddenHarboursWater.shader` confirms that .35 and .20 both take the same
`_SwellReadStrength > 0.001` path, with relative mode still zero; the absolute
expression remains `readBand * _SwellReadStrength * swellReadGate * 0.25`.
Only the uniform value changes: no added ALU, sample, keyword or instruction path.
Therefore d20 needs no new incremental GPU pricing; this is a source conclusion,
not measured timing. The 0/60 pricing gap remains open for any relative arm;
none ships.

Part A launches no Unity. Part B needs a new owner-granted editor slot: re-shoot
d20 anchors and gameplay MoodGrade plates through the shipped material with no
override, compare against Phase C d20 at
`b9e31bcf8c74472527dc2ca9f91155608a77f5f2`, and run the GPU water classes named in
the PR. Old .35-era measured bars and historical 77%/37% diagnostic numbers stay
unchanged pending those measurements; the PR names each affected premise. The
known main sun-side GPU failure (~0.27% against 0.5%) is still open. No tests are
retired and no default other than the owner's strength changes.

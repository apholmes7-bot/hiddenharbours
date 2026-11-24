# Fishing tunables audit

Snapshot: `f1b4ab2f59f417eda06f2239f7dee25d4c52ad23`. Scope: every C# file in
`Assets/_Project/Code/Fishing` (99 files, 18,171 source lines). This is a source
audit and a proposed data-home map. It changes no code, asset, config value, save
format or visible result. Priorities serve P1, P4 and P5: readable sea conditions,
manual fishing that rewards skill, and forgiving but consequential risk.

## How to read this

The first table ranks 25 **groups of related tuning choices**, not 25 arbitrary
tokens. The complete inventory follows, including lower-priority and presentation
findings. Each source link identifies the exact line at the snapshot; literal
spellings are preserved, and repeated occurrences have separate locations.

**Hard-coded** means a constant or inline calculation. **Component default** means
an existing serialized component field: it is already Inspector-editable, but has
no Def/config home. Those literals are C# defaults, **not verified scene values**.
**Fallback** means a missing-service or missing-data path. **Policy floor** records
a clamp that can override an authored choice; it need not become another gameplay
dial. All proposed new block/field names are proposals, not existing APIs.

Presentation and motion entries are recorded because they control feel, timing or
distance. Their later implementation needs the appropriate art review; this audit
does not authorize changing animation, appearance, projection or any baseline.
No scene/prefab override survey, Unity run, playtest, plate comparison, build or
EditMode/PlayMode execution was performed. Source evidence cannot establish which
fallbacks or legacy species are active in a shipped scene.

## What is already data-driven

Catch size is already `FishSpeciesDef.MinWeightKg / MaxWeightKg`
([definition](../../Assets/_Project/Code/Fishing/FishSpeciesDef.cs#L166));
`CatchResolver.RollWeight` reads those bounds and `GameConfig.SeaFishing` supplies
the sea size bias. There is no missing hard-coded catch-size range to rank.
Likewise, spawn weights, bait/lure affinities, school bite multipliers and depth
bands already have Def/config fields; the inventory flags their residual floors.

`BiteDef` owns nibble count/gaps, the take window, return chance/delay and pass count.
`RodFightDef` owns run/slack cadence, strength, tension rates, landing rate, steering
relief and give-back. Their serialized default initializers are not missing knobs.
`FishingController.BeginFight` selects that Def for a non-hand-gathered species
when present; otherwise it uses the legacy table below
([branch](../../Assets/_Project/Code/Fishing/FishingController.cs#L677)). Similarly,
the 1.2-second auto-hook window applies only when a species has no `BiteDef`.

`TrapDef`, `DeckWorkDef`, `LonglineDef`, the NPC day Defs, habitat/rotation Defs and
trawl gear/handling/session definitions already supply their main capacities,
soak times, size rules, rates and probabilities. Their field defaults are omitted.
The newer school/benthic/trawl plain classes take explicit profiles/settings;
mathematical constants in those algorithms are not content tuning.

## The 25 priorities

Order favours bite access and timing, fight duration/strain, hand skill and catch
yield. Legacy values rank highly for consequence **if that path is used**, not as
a claim that every authored fish still uses it. Motion changes are coupled to the
steering target and require coordinated review.

| Rank | Finding | Current literal(s) | Player consequence / scope |
|---|---|---|---|
| 1 | [F001](#f001) [FishingController.cs:80](../../Assets/_Project/Code/Fishing/FishingController.cs#L80) | 80: `1.5f`; 82: `4.0f` | Minimum and maximum real-second wait for a bite, before sea, school and trait scaling. |
| 2 | [F002](#f002) [PlacedTrapCatch.cs:37](../../Assets/_Project/Code/Fishing/PlacedTrapCatch.cs#L37) | 37: `3` | Favoured bait species receive three copies in the trap selection pool. This changes species odds, not pot count. Longline already has its own Def multiplier. |
| 3 | [F003](#f003) [SeaFightMath.cs:95](../../Assets/_Project/Code/Fishing/SeaFightMath.cs#L95) | 95: `0.15f` | Minimum sea-induced bite-delay multiplier; the sea alone cannot reduce a wait below 15% of its base. |
| 4 | [F004](#f004) [FishFight.cs:66](../../Assets/_Project/Code/Fishing/FishFight.cs#L66) | 66: `0.40f, 0.22f` | Landing progress per second at low/high legacy strength; governs fight length when no rod-fight Def is assigned. |
| 5 | [F005](#f005) [FishFight.cs:64](../../Assets/_Project/Code/Fishing/FishFight.cs#L64) | 64: `0.60f, 1.00f` | Reeling tension per second at low/high legacy strength. The 1.00 is a rate, not an identity. |
| 6 | [F006](#f006) [FishFight.cs:65](../../Assets/_Project/Code/Fishing/FishFight.cs#L65) | 65: `0.70f, 0.50f` | Tension relief per second while easing at low/high legacy strength. |
| 7 | [F007](#f007) [FishFight.cs:69](../../Assets/_Project/Code/Fishing/FishFight.cs#L69) | 69: `0.15f, 0.40f` | Extra tension per second during a legacy surge at low/high strength. |
| 8 | [F008](#f008) [FishFight.cs:67](../../Assets/_Project/Code/Fishing/FishFight.cs#L67) | 67: `3.0f, 1.6f`; 162: `0.7f, 0.6f` | Base surge delay in seconds, then random multiplier 0.7 + draw * 0.6 (0.7 to below 1.3). |
| 9 | [F009](#f009) [FishFight.cs:68](../../Assets/_Project/Code/Fishing/FishFight.cs#L68) | 68: `0.8f` | Duration in seconds of every legacy surge. |
| 10 | [F010](#f010) [FishFight.cs:59](../../Assets/_Project/Code/Fishing/FishFight.cs#L59) | 59: `0.5f, 0.5f`; 79: `0.40f`; 80: `0.40f`; 81: `0.55f`; 82: `0.70f`; 83: `0.85f`; 84: `1.00f`; 85: `0.50f` | Equal category/relative-weight contribution to strength; category bases for inshore, estuary, pelagic, deepwater, storm, legendary and fallback. Legendary 1.00 is a chosen category strength. |
| 11 | [F011](#f011) [JigMath.cs:100](../../Assets/_Project/Code/Fishing/JigMath.cs#L100) | 100: `2.2f` | Width of the logarithmic error band used to score jig tempo and stroke length; multiplies the existing tolerance. |
| 12 | [F012](#f012) [JigWork.cs:51](../../Assets/_Project/Code/Fishing/JigWork.cs#L51) | 51: `0.12f` | Minimum world-metre stroke that counts; rejects small hand movements. |
| 13 | [F013](#f013) [JigWork.cs:68](../../Assets/_Project/Code/Fishing/JigWork.cs#L68) | 68: `3f` | Longest reversal gap in seconds still treated as working the lure. |
| 14 | [F014](#f014) [JigWork.cs:55](../../Assets/_Project/Code/Fishing/JigWork.cs#L55) | 55: `1.6f` | Exponential fade per second of measured work after stopping or losing the pointer. |
| 15 | [F015](#f015) [JigWork.cs:139](../../Assets/_Project/Code/Fishing/JigWork.cs#L139) | 139: `0.5f`; 140: `0.5f` | Half of each later measured stroke replaces the previous tempo and length; affects responsiveness. |
| 16 | [F016](#f016) [FishingController.cs:95](../../Assets/_Project/Code/Fishing/FishingController.cs#L95) | 95: `0.3f` | Pointer distance in metres below which steer input is neutral. |
| 17 | [F017](#f017) [RodFightMotionMath.cs:47](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L47) | 47: `0.7f` | Seconds between darter direction changes, affecting both the movement tell and counter-steer target. |
| 18 | [F018](#f018) [RodFightMotionMath.cs:54](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L54) | 54: `2.4f` | Seconds between bulldog direction changes. |
| 19 | [F019](#f019) [RodFightMotionMath.cs:64](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L64) | 64: `6f` | Seconds per circler lap, also the rotation rate of its counter-steer target. |
| 20 | [F020](#f020) [RodFightMotionMath.cs:67](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L67) | 67: `0.9f` | Seconds per left-right-left swing and corresponding steering reversals. |
| 21 | [F021](#f021) [TrapHaulController.cs:82](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L82) | 82: `0.55f` | Line-progress fraction per second while holding in calm water. |
| 22 | [F022](#f022) [TrapHaulController.cs:86](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L86) | 86: `0.95f` | Signed swell-driven line progress per second, positive on lift and negative on fall. |
| 23 | [F023](#f023) [TrapHaulController.cs:90](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L90) | 90: `0.85f` | How strongly sea state blends the haul rate toward swell timing. |
| 24 | [F024](#f024) [ClamHoleVisual.cs:69](../../Assets/_Project/Code/Fishing/ClamHoleVisual.cs#L69) | 69: `7.0f`; 73: `4f` | Linger within seven metres for four real seconds and the clam is consumed for this tide. This is yield-affecting despite the cosmetic header. |
| 25 | [F025](#f025) [TrapFill.cs:99](../../Assets/_Project/Code/Fishing/TrapFill.cs#L99) | 99: `1 (comparison and return)`; 102: `1`; 103: `1 (initial slot)` | A ready pot guarantees one animal before further slots roll; one is an economic floor here, not an identity or ordinary array index. The loop start implements that same guarantee. |

## Complete inventory

The destination column names an existing Def/config block where possible. An
explicit new block or new field is required where no appropriate home exists.
No extraction or value change is part of this document.

| ID / status | Source and literal(s) | What it controls | Proposed home |
|---|---|---|---|
| <a id="f001"></a>F001 — Component default | [FishingController.cs:80](../../Assets/_Project/Code/Fishing/FishingController.cs#L80): `1.5f`<br>[FishingController.cs:82](../../Assets/_Project/Code/Fishing/FishingController.cs#L82): `4.0f` | Minimum and maximum real-second wait for a bite, before sea, school and trait scaling. | GameConfig.FishingInteraction (new block).MinBiteDelaySeconds / MaxBiteDelaySeconds |
| <a id="f002"></a>F002 — Hard-coded | [PlacedTrapCatch.cs:37](../../Assets/_Project/Code/Fishing/PlacedTrapCatch.cs#L37): `3` | Favoured bait species receive three copies in the trap selection pool. This changes species odds, not pot count. Longline already has its own Def multiplier. | GameConfig.BaitTackle.TrapBaitFavourMultiplier (new integer field) |
| <a id="f003"></a>F003 — Hard-coded | [SeaFightMath.cs:95](../../Assets/_Project/Code/Fishing/SeaFightMath.cs#L95): `0.15f` | Minimum sea-induced bite-delay multiplier; the sea alone cannot reduce a wait below 15% of its base. | GameConfig.SeaFishing.MinBiteDelayScale (new field) |
| <a id="f004"></a>F004 — Hard-coded | [FishFight.cs:66](../../Assets/_Project/Code/Fishing/FishFight.cs#L66): `0.40f, 0.22f` | Landing progress per second at low/high legacy strength; governs fight length when no rod-fight Def is assigned. | GameConfig.LegacyFight (new block; keep this separate from RodFightDef).LandingFillMinStrength / LandingFillMaxStrength |
| <a id="f005"></a>F005 — Hard-coded | [FishFight.cs:64](../../Assets/_Project/Code/Fishing/FishFight.cs#L64): `0.60f, 1.00f` | Reeling tension per second at low/high legacy strength. The 1.00 is a rate, not an identity. | GameConfig.LegacyFight (new block; keep this separate from RodFightDef).TensionRiseMinStrength / TensionRiseMaxStrength |
| <a id="f006"></a>F006 — Hard-coded | [FishFight.cs:65](../../Assets/_Project/Code/Fishing/FishFight.cs#L65): `0.70f, 0.50f` | Tension relief per second while easing at low/high legacy strength. | GameConfig.LegacyFight (new block; keep this separate from RodFightDef).TensionFallMinStrength / TensionFallMaxStrength |
| <a id="f007"></a>F007 — Hard-coded | [FishFight.cs:69](../../Assets/_Project/Code/Fishing/FishFight.cs#L69): `0.15f, 0.40f` | Extra tension per second during a legacy surge at low/high strength. | GameConfig.LegacyFight (new block; keep this separate from RodFightDef).SurgePressureMinStrength / SurgePressureMaxStrength |
| <a id="f008"></a>F008 — Hard-coded | [FishFight.cs:67](../../Assets/_Project/Code/Fishing/FishFight.cs#L67): `3.0f, 1.6f`<br>[FishFight.cs:162](../../Assets/_Project/Code/Fishing/FishFight.cs#L162): `0.7f, 0.6f` | Base surge delay in seconds, then random multiplier 0.7 + draw * 0.6 (0.7 to below 1.3). | GameConfig.LegacyFight (new block; keep this separate from RodFightDef).SurgeIntervalMinStrength / SurgeIntervalMaxStrength / SurgeDelayScaleMin / SurgeDelayScaleSpan |
| <a id="f009"></a>F009 — Hard-coded | [FishFight.cs:68](../../Assets/_Project/Code/Fishing/FishFight.cs#L68): `0.8f` | Duration in seconds of every legacy surge. | GameConfig.LegacyFight (new block; keep this separate from RodFightDef).SurgeDurationSeconds |
| <a id="f010"></a>F010 — Hard-coded | [FishFight.cs:59](../../Assets/_Project/Code/Fishing/FishFight.cs#L59): `0.5f, 0.5f`<br>[FishFight.cs:79](../../Assets/_Project/Code/Fishing/FishFight.cs#L79): `0.40f`<br>[FishFight.cs:80](../../Assets/_Project/Code/Fishing/FishFight.cs#L80): `0.40f`<br>[FishFight.cs:81](../../Assets/_Project/Code/Fishing/FishFight.cs#L81): `0.55f`<br>[FishFight.cs:82](../../Assets/_Project/Code/Fishing/FishFight.cs#L82): `0.70f`<br>[FishFight.cs:83](../../Assets/_Project/Code/Fishing/FishFight.cs#L83): `0.85f`<br>[FishFight.cs:84](../../Assets/_Project/Code/Fishing/FishFight.cs#L84): `1.00f`<br>[FishFight.cs:85](../../Assets/_Project/Code/Fishing/FishFight.cs#L85): `0.50f` | Equal category/relative-weight contribution to strength; category bases for inshore, estuary, pelagic, deepwater, storm, legendary and fallback. Legendary 1.00 is a chosen category strength. | GameConfig.LegacyFight (new block; keep this separate from RodFightDef).CategoryContribution / WeightContribution / CategoryStrengths / FallbackStrength |
| <a id="f011"></a>F011 — Hard-coded | [JigMath.cs:100](../../Assets/_Project/Code/Fishing/JigMath.cs#L100): `2.2f` | Width of the logarithmic error band used to score jig tempo and stroke length; multiplies the existing tolerance. | GameConfig.Jigging (existing block, new fields).LogErrorWidthScale |
| <a id="f012"></a>F012 — Hard-coded | [JigWork.cs:51](../../Assets/_Project/Code/Fishing/JigWork.cs#L51): `0.12f` | Minimum world-metre stroke that counts; rejects small hand movements. | GameConfig.Jigging (existing block, new fields).MinStrokeMetres |
| <a id="f013"></a>F013 — Hard-coded | [JigWork.cs:68](../../Assets/_Project/Code/Fishing/JigWork.cs#L68): `3f` | Longest reversal gap in seconds still treated as working the lure. | GameConfig.Jigging (existing block, new fields).StallSeconds; retain the slowest-style compatibility rule |
| <a id="f014"></a>F014 — Hard-coded | [JigWork.cs:55](../../Assets/_Project/Code/Fishing/JigWork.cs#L55): `1.6f` | Exponential fade per second of measured work after stopping or losing the pointer. | GameConfig.Jigging (existing block, new fields).WorkDecayPerSecond |
| <a id="f015"></a>F015 — Hard-coded | [JigWork.cs:139](../../Assets/_Project/Code/Fishing/JigWork.cs#L139): `0.5f`<br>[JigWork.cs:140](../../Assets/_Project/Code/Fishing/JigWork.cs#L140): `0.5f` | Half of each later measured stroke replaces the previous tempo and length; affects responsiveness. | GameConfig.Jigging (existing block, new fields).StrokeSmoothingWeight |
| <a id="f016"></a>F016 — Component default | [FishingController.cs:95](../../Assets/_Project/Code/Fishing/FishingController.cs#L95): `0.3f` | Pointer distance in metres below which steer input is neutral. | GameConfig.RodFight.SteerDeadzoneMetres (new field) |
| <a id="f017"></a>F017 — Motion and control | [RodFightMotionMath.cs:47](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L47): `0.7f` | Seconds between darter direction changes, affecting both the movement tell and counter-steer target. | RodFightDef.Motion (new fields).DarterSegmentSeconds |
| <a id="f018"></a>F018 — Motion and control | [RodFightMotionMath.cs:54](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L54): `2.4f` | Seconds between bulldog direction changes. | RodFightDef.Motion (new fields).BulldogSegmentSeconds |
| <a id="f019"></a>F019 — Motion and control | [RodFightMotionMath.cs:64](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L64): `6f` | Seconds per circler lap, also the rotation rate of its counter-steer target. | RodFightDef.Motion (new fields).CirclerRevolutionSeconds |
| <a id="f020"></a>F020 — Motion and control | [RodFightMotionMath.cs:67](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L67): `0.9f` | Seconds per left-right-left swing and corresponding steering reversals. | RodFightDef.Motion (new fields).ThrasherPeriodSeconds |
| <a id="f021"></a>F021 — Component default | [TrapHaulController.cs:82](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L82): `0.55f` | Line-progress fraction per second while holding in calm water. | TrapDef.Haul (new settings fields).CalmHaulRate |
| <a id="f022"></a>F022 — Component default | [TrapHaulController.cs:86](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L86): `0.95f` | Signed swell-driven line progress per second, positive on lift and negative on fall. | TrapDef.Haul (new settings fields).SwellTakeRate |
| <a id="f023"></a>F023 — Component default | [TrapHaulController.cs:90](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L90): `0.85f` | How strongly sea state blends the haul rate toward swell timing. | TrapDef.Haul (new settings fields).SwellCoupling |
| <a id="f024"></a>F024 — Component default; yield and presentation | [ClamHoleVisual.cs:69](../../Assets/_Project/Code/Fishing/ClamHoleVisual.cs#L69): `7.0f`<br>[ClamHoleVisual.cs:73](../../Assets/_Project/Code/Fishing/ClamHoleVisual.cs#L73): `4f` | Linger within seven metres for four real seconds and the clam is consumed for this tide. This is yield-affecting despite the cosmetic header. | GameConfig.ClamInteraction (new block).EscapeRadiusMetres / EscapeSeconds |
| <a id="f025"></a>F025 — Hard-coded | [TrapFill.cs:99](../../Assets/_Project/Code/Fishing/TrapFill.cs#L99): `1 (comparison and return)`<br>[TrapFill.cs:102](../../Assets/_Project/Code/Fishing/TrapFill.cs#L102): `1`<br>[TrapFill.cs:103](../../Assets/_Project/Code/Fishing/TrapFill.cs#L103): `1 (initial slot)` | A ready pot guarantees one animal before further slots roll; one is an economic floor here, not an identity or ordinary array index. The loop start implements that same guarantee. | TrapDef.MinimumReadyCatch (new field); preserve this rule unless explicitly changed |
| <a id="f026"></a>F026 — Hard-coded | [FishFight.cs:54](../../Assets/_Project/Code/Fishing/FishFight.cs#L54): `0.4f, 0.7f` | Hand-gather tending tension relief and landing progress per second. The snap/surge-disabled zeros are mode identities and omitted. | GameConfig.LegacyFight (new block; keep this separate from RodFightDef).TendTensionFallPerSecond / TendLandingFillPerSecond |
| <a id="f027"></a>F027 — Component default | [FishingController.cs:84](../../Assets/_Project/Code/Fishing/FishingController.cs#L84): `1.2f` | Real-second hook window and auto-hook delay only for species with no BiteDef. | GameConfig.FishingInteraction (new block).FallbackHookWindowSeconds |
| <a id="f028"></a>F028 — Component default | [FishingController.cs:86](../../Assets/_Project/Code/Fishing/FishingController.cs#L86): `1.5f` | Seconds showing the landed, snapped or no-bite result before another attempt. | GameConfig.FishingInteraction (new block).ResultDisplaySeconds |
| <a id="f029"></a>F029 — Component default | [FishingController.cs:1194](../../Assets/_Project/Code/Fishing/FishingController.cs#L1194): `0.05f` | Default rig mass in kilograms used by weighted-rig selection and sink speed. Tackle has no mass field today. | TackleDef.RigWeightKg (new field), with GameConfig.DepthDrop fallback for no tackle |
| <a id="f030"></a>F030 — Component default; presentation | [FishingController.cs:92](../../Assets/_Project/Code/Fishing/FishingController.cs#L92): `2.5f` | Base visual roam radius in world metres; steering uses direction rather than this distance. | GameConfig.RodFight.RoamRadiusMetres (new field) |
| <a id="f031"></a>F031 — Component default; presentation | [FishingController.cs:99](../../Assets/_Project/Code/Fishing/FishingController.cs#L99): `1.1f` | Distance in metres from the angler at which a fully landed fish is drawn. | GameConfig.RodFight.AlongsideMetres (new field) |
| <a id="f032"></a>F032 — Component default; presentation | [FishingController.cs:104](../../Assets/_Project/Code/Fishing/FishingController.cs#L104): `0.45f` | Fraction of surface roam shown by the entry point while the fish is deep. | GameConfig.RodFight.DeepEntryRoamFraction (new field) |
| <a id="f033"></a>F033 — Presentation | [RodFightMotionMath.cs:51](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L51): `0.35f` | Fraction of each darter segment spent crossing rather than hovering. | RodFightDef.Motion (new fields).DarterMoveFraction |
| <a id="f034"></a>F034 — Presentation | [RodFightMotionMath.cs:58](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L58): `0.45f` | Multiplier shrinking the bulldog roam disc. | RodFightDef.Motion (new fields).BulldogRadiusFraction |
| <a id="f035"></a>F035 — Presentation | [RodFightMotionMath.cs:61](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L61): `0.25f` | Fraction of a bulldog segment spent lunging. | RodFightDef.Motion (new fields).BulldogMoveFraction |
| <a id="f036"></a>F036 — Motion and control | [RodFightMotionMath.cs:71](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L71): `0.5f` | Vertical foreshortening of motion; also affects the circler tangent used for steering. | RodFightDef.Motion (new fields).VerticalScale; keep the view convention consistent |
| <a id="f037"></a>F037 — Presentation | [RodFightMotionMath.cs:76](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L76): `1.5f` | Seconds for the roam radius to grow when surfacing; RodFightSim also consumes it. | RodFightDef.Motion (new fields).SurfaceRampSeconds |
| <a id="f038"></a>F038 — Motion and control | [RodFightMotionMath.cs:199](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs#L199): `0.5f` | Equal chance of circling in either direction for a seeded fish. | RodFightDef.Motion (new fields).NegativeHandednessChance01 |
| <a id="f039"></a>F039 — Hard-coded | [RodFightPhases.cs:50](../../Assets/_Project/Code/Fishing/RodFightPhases.cs#L50): `0.5f` | Strength at which run pressure is unchanged; other strengths divide by this reference, and NaN falls back to it. | GameConfig.RodFight.NeutralStrength01 (new field); coordinate with RodFightDef.Strength |
| <a id="f040"></a>F040 — Hard-coded | [SeaFightMath.cs:46](../../Assets/_Project/Code/Fishing/SeaFightMath.cs#L46): `0.5f` | Sea-state ceiling used by the cozy tuning guarantee and SeaPromise feeding-sea cue. It is not a clamp on live tension. | GameConfig.SeaFishing.CozySeaCeiling01 (new field); move validation and cue together |
| <a id="f041"></a>F041 — Component default | [TrapHaulController.cs:76](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L76): `4f` | Maximum distance in metres for starting a buoy haul. | TrapDef.Haul (new settings fields).ReachMetres |
| <a id="f042"></a>F042 — Component default | [TrapHaulController.cs:95](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L95): `2.0f` | Normalized surface rise rate treated as full lift; changes the timing signal passed to the haul. | TrapDef.Haul (new settings fields).LiftReferenceRate |
| <a id="f043"></a>F043 — Component default; presentation | [TrapHaulController.cs:101](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L101): `0.5f` | Fraction of ambient rope strain relieved by taking all line in. | TrapDef.Haul (new settings fields).LineRelief |
| <a id="f044"></a>F044 — Component default; presentation | [TrapHaulController.cs:104](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L104): `0.15f`<br>[TrapHaulController.cs:107](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L107): `0.4f` | Seconds for displayed fight strain to build and ease. | TrapDef.Haul (new settings fields).StrainBuildSeconds / StrainEaseSeconds |
| <a id="f045"></a>F045 — Feedback | [TrapHaulController.cs:145](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L145): `0.15f` | Lift threshold for the clean-pull cue when the tick gained line. | TrapDef.Haul (new settings fields).GoodTakeLiftThreshold |
| <a id="f046"></a>F046 — Component default | [ClamDig.cs:80](../../Assets/_Project/Code/Fishing/ClamDig.cs#L80): `1.25f` | Shovel reach in world metres for a hole. | GameConfig.ClamInteraction (new block).DigReachMetres |
| <a id="f047"></a>F047 — Component default | [StartingBait.cs:31](../../Assets/_Project/Code/Fishing/StartingBait.cs#L31): `6` | Herring bait granted by the default one-time starter list. | GameConfig.StarterBaitKit (new list analogous to existing StarterPotKit) |
| <a id="f048"></a>F048 — Development component default | [DevTrapInput.cs:54](../../Assets/_Project/Code/Fishing/DevTrapInput.cs#L54): `5` | Bait units per development supply key press; not a normal acquisition path. | GameConfig.DevelopmentFishing.BaitGrantCount (new field) |
| <a id="f049"></a>F049 — Fallback | [FishingController.cs:1331](../../Assets/_Project/Code/Fishing/FishingController.cs#L1331): `12f` | No-clock fallback selects noon, affecting time-gated species eligibility. | GameConfig.FishingInteraction (new block).FallbackHourOfDay; keep as an explicit missing-service policy |
| <a id="f050"></a>F050 — Fallback | [TrapHaulController.cs:546](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L546): `12f` | Same noon fallback for trap catch context when the clock is missing. | GameConfig.FishingInteraction (new block).FallbackHourOfDay (share with rod path) |
| <a id="f051"></a>F051 — Sampling policy | [FishingController.cs:1360](../../Assets/_Project/Code/Fishing/FishingController.cs#L1360): `0.1` | Tenth-of-an-in-game-hour total central-difference interval used for moving-water eligibility. | GameConfig.FishSchools.TideRateSampleHours (new shared field) |
| <a id="f052"></a>F052 — Sampling policy | [IFishSchoolWorld.cs:134](../../Assets/_Project/Code/Fishing/IFishSchoolWorld.cs#L134): `0.1` | Same total central-difference interval for school time/tide eligibility; sample points are half this interval either side. | GameConfig.FishSchools.TideRateSampleHours (same proposed field) |
| <a id="f053"></a>F053 — Policy floor | [CatchResolver.cs:252](../../Assets/_Project/Code/Fishing/CatchResolver.cs#L252): `0.0001f` | Minimum positive spawn weight even when a species asks for zero. | FishSpeciesDef.SpawnWeight validation policy, or new GameConfig.FishingInteraction.MinSpawnWeight |
| <a id="f054"></a>F054 — Policy floor | [DepthDropMath.cs:211](../../Assets/_Project/Code/Fishing/DepthDropMath.cs#L211): `0.01f`<br>[DepthDropMath.cs:217](../../Assets/_Project/Code/Fishing/DepthDropMath.cs#L217): `0.0001f` | Minimum off-band affinity and final affinity; off-band fish cannot be fully disabled through those inputs. | GameConfig.DepthDrop.MinOffBandAffinity / MinEffectiveAffinity (new policy fields) |
| <a id="f055"></a>F055 — Policy floor | [SchoolInfluence.cs:94](../../Assets/_Project/Code/Fishing/SchoolInfluence.cs#L94): `0.0001f` | Minimum off-school species affinity even if the configured damp is zero. | GameConfig.FishSchools.MinOffSpeciesAffinity (new policy field) |
| <a id="f056"></a>F056 — Policy floor | [JigMath.cs:61](../../Assets/_Project/Code/Fishing/JigMath.cs#L61): `0.001f` | Floor for dead-lure fishing fraction, limiting the inverse-delay penalty to 1000 times. Config inspector already declares a different 0.01 minimum. | GameConfig.Jigging.DeadLureFraction01 validation policy (reconcile the floor, do not add a second independent tuning dial) |
| <a id="f057"></a>F057 — Policy floor | [JigMath.cs:99](../../Assets/_Project/Code/Fishing/JigMath.cs#L99): `0.05f` | Minimum accepted jig tolerance; matches the config inspector minimum. | GameConfig.Jigging.Tolerance01 validation policy |
| <a id="f058"></a>F058 — Policy floor | [BiteDef.cs:31](../../Assets/_Project/Code/Fishing/BiteDef.cs#L31): `0.05f`<br>[BiteDef.cs:33](../../Assets/_Project/Code/Fishing/BiteDef.cs#L33): `0.05f`<br>[BiteDef.cs:34](../../Assets/_Project/Code/Fishing/BiteDef.cs#L34): `0.05f` | Runtime floors for nibble gap, nibble dip and take window, even if authored smaller. | BiteDef timing validation policy; existing fields NibbleGapMinSeconds / NibbleDipSeconds / TakeWindowSeconds |
| <a id="f059"></a>F059 — Policy floor | [BiteSequenceSim.cs:170](../../Assets/_Project/Code/Fishing/BiteSequenceSim.cs#L170): `0.01f` | Minimum phase length, including a zero return delay; prevents an immediate zero-duration phase. | BiteDef timing validation policy; keep a named positive safety floor |
| <a id="f060"></a>F060 — Policy floor | [RodFightRhythm.cs:28](../../Assets/_Project/Code/Fishing/RodFightRhythm.cs#L28): `0.05f` | Minimum jittered run or slack duration prevents a zero-length phase loop. Explicitly a guard, not a recommended feel control. | RodFightDef.StaminaCadence validation policy; retain a positive named guard |
| <a id="f061"></a>F061 — Policy floor | [StrikeGesture.cs:67](../../Assets/_Project/Code/Fishing/StrikeGesture.cs#L67): `0.01f`<br>[StrikeGesture.cs:71](../../Assets/_Project/Code/Fishing/StrikeGesture.cs#L71): `0.01f` | Minimum yank speed in m/s and travel in metres; values below these cannot be authored effectively. | GameConfig.Strike.YankMinSpeedMps / YankMinMetres validation policy |
| <a id="f062"></a>F062 — Presentation policy floor | [FishSchoolMath.cs:443](../../Assets/_Project/Code/Fishing/FishSchoolMath.cs#L443): `0.05f` | Minimum sanitized global shoal spread in metres. | GameConfig.FishSchools.ShoalSpreadMetres validation policy |
| <a id="f063"></a>F063 — Presentation policy floor | [FishSchoolPresenter.cs:444](../../Assets/_Project/Code/Fishing/FishSchoolPresenter.cs#L444): `0.05f` | Same minimum for the presenter global spread fallback. | GameConfig.FishSchools.ShoalSpreadMetres validation policy (share the floor) |
| <a id="f064"></a>F064 — Policy floor | [NpcTrawlDay.cs:118](../../Assets/_Project/Code/Fishing/NpcTrawlDay.cs#L118): `1f` | Rejects work tracks shorter than one metre, beyond merely nonzero geometry. | NpcTrawlDayDef.MinWorkTrackLengthMetres (new field) or a documented authoring minimum |
| <a id="f065"></a>F065 — Component default; presentation | [ClamHoleVisual.cs:63](../../Assets/_Project/Code/Fishing/ClamHoleVisual.cs#L63): `0.5f` | Local vertical placement offset for the spurt overlay. | new ClamTellDef (presentation data; art review required).SquirtVerticalOffset |
| <a id="f066"></a>F066 — Presentation | [ClamHoleVisual.cs:77](../../Assets/_Project/Code/Fishing/ClamHoleVisual.cs#L77): `0.6f`<br>[ClamHoleVisual.cs:242](../../Assets/_Project/Code/Fishing/ClamHoleVisual.cs#L242): `0.05f`<br>[ClamHoleVisual.cs:249](../../Assets/_Project/Code/Fishing/ClamHoleVisual.cs#L249): `0.2f` | Sink animation seconds, minimum span and final fraction of original hole scale. | new ClamTellDef (presentation data; art review required).SinkSeconds / minimum-span policy / SinkEndScale |
| <a id="f067"></a>F067 — Component default; presentation cadence | [ClamHoleVisual.cs:81](../../Assets/_Project/Code/Fishing/ClamHoleVisual.cs#L81): `10f` | Refresh rate in Hz of exposed-hole show/hide; escape and sinking still tick every frame. | new ClamTellDef (presentation data; art review required).RefreshHz |
| <a id="f068"></a>F068 — Presentation | [ClamSpurtMath.cs:42](../../Assets/_Project/Code/Fishing/ClamSpurtMath.cs#L42): `420f` | Duration in milliseconds of the spurt tell. | new ClamTellDef (presentation data; art review required).SpurtMilliseconds; currently matches the authored source |
| <a id="f069"></a>F069 — Presentation | [ClamSpurtMath.cs:46](../../Assets/_Project/Code/Fishing/ClamSpurtMath.cs#L46): `2600f`<br>[ClamSpurtMath.cs:49](../../Assets/_Project/Code/Fishing/ClamSpurtMath.cs#L49): `7800f` | Minimum/maximum milliseconds between spurt starts. | new ClamTellDef (presentation data; art review required).MinPeriodMilliseconds / MaxPeriodMilliseconds |
| <a id="f070"></a>F070 — Presentation | [ClamSpurtMath.cs:52](../../Assets/_Project/Code/Fishing/ClamSpurtMath.cs#L52): `9000f` | Random phase span in milliseconds used to stagger hole tells. | new ClamTellDef (presentation data; art review required).PhaseSpanMilliseconds |
| <a id="f071"></a>F071 — Presentation | [ClamSpurtMath.cs:56](../../Assets/_Project/Code/Fishing/ClamSpurtMath.cs#L56): `4`<br>[ClamSpurtMath.cs:59](../../Assets/_Project/Code/Fishing/ClamSpurtMath.cs#L59): `0.45f` | Jet height in pixels at full rise, and normalized spurt point after which the droplet appears. | new ClamTellDef (presentation data; art review required).JetPixels / DropletAfterFraction |
| <a id="f072"></a>F072 — Component default; presentation cadence | [FishSchoolPresenter.cs:54](../../Assets/_Project/Code/Fishing/FishSchoolPresenter.cs#L54): `10` | School queries and swimmer pose refreshes per second. | GameConfig.FishSchools (existing block, new presentation fields).QueriesPerSecond |
| <a id="f073"></a>F073 — Component default; presentation | [FishSchoolPresenter.cs:58](../../Assets/_Project/Code/Fishing/FishSchoolPresenter.cs#L58): `20f` | Seconds per school event slot for a jump, roll or thrash. | GameConfig.FishSchools (existing block, new presentation fields).EventPeriodSeconds |
| <a id="f074"></a>F074 — Presentation | [FishSchoolPresenter.cs:381](../../Assets/_Project/Code/Fishing/FishSchoolPresenter.cs#L381): `0.65f` | Opacity multiplier for the inshore dim swimmer class. | GameConfig.FishSchools (existing block, new presentation fields).DimAlpha |
| <a id="f075"></a>F075 — Presentation fallback | [FishSwimSpriteLibrary.cs:194](../../Assets/_Project/Code/Fishing/FishSwimSpriteLibrary.cs#L194): `0.5f` | Metre length used when a kind lacks a positive authored length. KindLength.LengthMetres already exists; its initializer is not a missing data field. | FishSwimSpriteLibrary.FallbackLengthMetres (new field on the existing asset) |
| <a id="f076"></a>F076 — Presentation contract | [ShoalMath.cs:37](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L37): `6`<br>[ShoalMath.cs:40](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L40): `120.0` | Swim loop frame count and milliseconds per frame, copied from the authored animation contract. | FishSwimSpriteLibrary.AnimEntry.Frames (existing) plus FrameMilliseconds (new); art-coordinated binding |
| <a id="f077"></a>F077 — Presentation fallback | [ShoalMath.cs:46](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L46): `1.2f` | Fallback metre radius for nonpositive radius inputs; normal presenter already supplies species/config spread. | GameConfig.FishSchools.ShoalSpreadMetres (existing), passed explicitly to fallback callers |
| <a id="f078"></a>F078 — Presentation | [ShoalMath.cs:51](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L51): `0.35f` | Metres per second for shoal motion, explicitly passed by the presenter and also used as a fallback. | new ShoalMotionDef (presentation data; preserve the authored motion contract).SpeedMetresPerSecond |
| <a id="f079"></a>F079 — Presentation | [ShoalMath.cs:55](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L55): `-0.15f` | Relative shoal height supplied by the presenter. | new ShoalMotionDef (presentation data; preserve the authored motion contract).RelativeHeightMetres |
| <a id="f080"></a>F080 — Presentation | [ShoalMath.cs:220](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L220): `0.55, 2.0`<br>[ShoalMath.cs:222](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L222): `1.1, 2.0` | Leader figure-eight vertical amplitude and frequency ratio; 1.1 is the derivative product of 0.55 and 2.0, so derive it rather than expose an independent value. | new ShoalMotionDef (presentation data; preserve the authored motion contract).LoopVerticalScale / LoopVerticalFrequency; derive velocity from the same fields |
| <a id="f081"></a>F081 — Presentation | [ShoalMath.cs:231](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L231): `0.3, 1.6, 1.5` | Minimum/spread of trailing distance in body lengths and its growth toward the back of the school. | new ShoalMotionDef (presentation data; preserve the authored motion contract).TrailMinLengths / TrailSpanLengths / TailExpansion |
| <a id="f082"></a>F082 — Presentation | [ShoalMath.cs:232](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L232): `3.2, 0.35` | Lateral formation width in body lengths and minimum width toward the lead fish; 0.5 recentres a random draw and is mathematical. | new ShoalMotionDef (presentation data; preserve the authored motion contract).FormationWidthLengths / LeadingWidthFraction |
| <a id="f083"></a>F083 — Presentation | [ShoalMath.cs:233](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L233): `0.08` | Total random vertical-height span in metres (centred around zero). | new ShoalMotionDef (presentation data; preserve the authored motion contract).HeightJitterSpanMetres |
| <a id="f084"></a>F084 — Presentation | [ShoalMath.cs:235](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L235): `0.6, 0.8` | Minimum and random span of each swimmer motion/animation rate multiplier. | new ShoalMotionDef (presentation data; preserve the authored motion contract).RateMin / RateSpan |
| <a id="f085"></a>F085 — Presentation | [ShoalMath.cs:236](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L236): `0.85, 0.3` | Minimum and random span of specimen draw scale. | new ShoalMotionDef (presentation data; preserve the authored motion contract).ScaleMin / ScaleSpan |
| <a id="f086"></a>F086 — Presentation | [ShoalMath.cs:238](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L238): `0.7, 0.15` | Forward wobble frequency multiplier and amplitude in body lengths. | new ShoalMotionDef (presentation data; preserve the authored motion contract).ForwardWobbleFrequency / ForwardWobbleLengths |
| <a id="f087"></a>F087 — Presentation | [ShoalMath.cs:239](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L239): `0.25` | Lateral wobble amplitude in body lengths. | new ShoalMotionDef (presentation data; preserve the authored motion contract).LateralWobbleLengths |
| <a id="f088"></a>F088 — Presentation | [ShoalMath.cs:240](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L240): `1.3, 0.18` | Heading wobble frequency multiplier and amplitude in radians. | new ShoalMotionDef (presentation data; preserve the authored motion contract).HeadingWobbleFrequency / HeadingWobbleRadians |
| <a id="f089"></a>F089 — Presentation | [ShoalMath.cs:244](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L244): `0.01` | Vertical bob amplitude in metres. | new ShoalMotionDef (presentation data; preserve the authored motion contract).BobMetres |
| <a id="f090"></a>F090 — Presentation | [ShoalMath.cs:246](../../Assets/_Project/Code/Fishing/ShoalMath.cs#L246): `2.0` | Multiplier converting each random phase to a swim-frame offset; affects asynchronous animation. | new ShoalMotionDef (presentation data; preserve the authored motion contract).FramePhaseScale |
| <a id="f091"></a>F091 — Presentation contract | [ShoalEventMath.cs:59](../../Assets/_Project/Code/Fishing/ShoalEventMath.cs#L59): `4`<br>[ShoalEventMath.cs:60](../../Assets/_Project/Code/Fishing/ShoalEventMath.cs#L60): `130.0` | Roll frame count and milliseconds per frame. | FishSwimSpriteLibrary.AnimEntry.Frames plus new FrameMilliseconds; preserve the authored contract |
| <a id="f092"></a>F092 — Presentation contract | [ShoalEventMath.cs:63](../../Assets/_Project/Code/Fishing/ShoalEventMath.cs#L63): `4`<br>[ShoalEventMath.cs:64](../../Assets/_Project/Code/Fishing/ShoalEventMath.cs#L64): `110.0` | Thrash frame count and milliseconds per frame. | FishSwimSpriteLibrary.AnimEntry.Frames plus new FrameMilliseconds |
| <a id="f093"></a>F093 — Presentation contract | [ShoalEventMath.cs:67](../../Assets/_Project/Code/Fishing/ShoalEventMath.cs#L67): `3`<br>[ShoalEventMath.cs:68](../../Assets/_Project/Code/Fishing/ShoalEventMath.cs#L68): `80.0` | Dart frame count and milliseconds per frame. | FishSwimSpriteLibrary.AnimEntry.Frames plus new FrameMilliseconds |
| <a id="f094"></a>F094 — Presentation | [ShoalEventMath.cs:284](../../Assets/_Project/Code/Fishing/ShoalEventMath.cs#L284): `3u` | Three equal event buckets: jumping species get jump/roll/thrash; non-jumpers get roll twice and thrash once. This modulo encodes odds, not just an index. | GameConfig.FishSchools (existing block, new presentation fields).EventWeights (new fields for jump/roll/thrash with the species gate) |
| <a id="f095"></a>F095 — Presentation | [PotDeckWorkController.cs:96](../../Assets/_Project/Code/Fishing/PotDeckWorkController.cs#L96): `0.35f` | Metre arc height when returning an animal over the side. | DeckWorkDef.SplashArcHeightMeters (new field) |
| <a id="f096"></a>F096 — Presentation fallback | [PotDeckWorkController.cs:782](../../Assets/_Project/Code/Fishing/PotDeckWorkController.cs#L782): `0.05f, 0.7f`<br>[PotDeckWorkController.cs:783](../../Assets/_Project/Code/Fishing/PotDeckWorkController.cs#L783): `1.6f` | Minimum splash span plus missing-Def fallback seconds and travel metres. Normal values already come from DeckWorkDef. | DeckWorkDef.SplashOutSeconds / SplashOutDistanceMeters (existing); one shared default policy |
| <a id="f097"></a>F097 — Component default; presentation | [TrapHaulController.cs:109](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L109): `0.1f` | Metre width of the drawn haul rope. | TrapDef.Haul (new settings fields).RopeWidthMetres |
| <a id="f098"></a>F098 — Component default; presentation | [TrapHaulController.cs:115](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L115): `0.7f` | Maximum drawn sag in metres. | TrapDef.Haul (new settings fields).SlackSagMetres |
| <a id="f099"></a>F099 — Presentation | [TrapHaulController.cs:118](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L118): `0.06f`<br>[TrapHaulController.cs:120](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L120): `20f`<br>[TrapHaulController.cs:665](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L665): `1.7f` | Shudder amplitude in metres, frequency in Hz, and phase advance in radians per rope vertex. | TrapDef.Haul (new settings fields).ShudderMetres / ShudderHz / ShudderPhasePerVertex |
| <a id="f100"></a>F100 — Component default; presentation | [TrapHaulController.cs:131](../../Assets/_Project/Code/Fishing/TrapHaulController.cs#L131): `0.35f, 0.9f` | Hand-to-rope offset in world metres (horizontal, vertical). | TrapDef.Haul (new settings fields).HandOffsetMetres |
| <a id="f101"></a>F101 — Presentation | [TrapSplashFx.cs:38](../../Assets/_Project/Code/Fishing/TrapSplashFx.cs#L38): `0.25f` | Camera-relative Z offset placing the splash in front of the water. | TrapDef.SplashCameraOffsetMetres (new presentation field) |

## Exclusions and boundary decisions

- Array positions, enum/flag encodings, seed/hash constants, stream selectors,
  collection capacities, missing-value sentinels (including TrapPlacement's -9999),
  and 0/1 used as identities are excluded. The pot-ready
  minimum is included because its `1` is a catch-yield promise; the event `% 3`
  is included because it chooses a probability distribution.
- Unit conversions, angle wrap/full-turn constants, midpoint/interpolation
  coefficients, probability-domain bounds and the derivative factor in a
  normalized parabola are excluded. Examples: `SpeciesCatchItemFactory`'s `0.5`
  averages the Def weight bounds; `SchoolField`'s `0.5` centres its depth waveform;
  `ShoalMath`'s `6.28` approximates a full turn, not a pacing knob. The `1.1` in
  shoal velocity is documented only as a derived companion to the tuned shape.
- Tiny nonzero geometry/time denominators and numerical tolerances are excluded
  where their job is finite arithmetic or avoiding a degenerate direction/segment:
  for example DeckAngleMath, FishSchoolMath cell/slot epsilons, NpcPotDay and
  NpcPotLanding speed guards, and TrapHaulMath's glass-amplitude epsilon. The bite,
  rhythm, strike, affinity and tolerance floors are shown separately because they
  constrain the stated authoring range or prevent an intended zero setting.
- Inspector `Min`/`Range` annotations on existing data fields are authoring
  contracts, not additional runtime tuning. Component defaults still appear in
  the inventory; their annotations are not counted as duplicate choices.
- Pure rendering implementation details (sorting integers, vertex counts, sprite
  pivots, fallback RGB palettes and pixel-map data) are not balance/feel numbers.
  This excludes `PotDeckWorkController`'s fallback pixel-art palette/24 PPU,
  `TrapHaulController`'s rope colours/14 segments/2 cap vertices and the school
  sorting order. Numeric distances, timing, motion and opacity are included.
- Bounded work/allocation constants are not owner balance: cast probes 32,
  gesture samples 256, school 9/25-cell searches, splash ring/pool capacities,
  maximum 4096 NPC work legs, swimmer ceiling 32, and haul notification buckets
  10/32. They may merit a performance audit but are not priority tuning fields.
- `ShoalEventMath` forwards jump timing/travel from Core `FishJumpArc`; those are
  not numeric literal declarations in this module. They require an outside-module
  follow-up if the owner wants all presentation constants centralized.
- No test code is included in the inventory. No global data-only field initializer
  is labelled a magic number merely because its default appears in C#.

## Review coverage

Every file below was read for the source inventory. A dash means no additional
in-scope literal after the exclusions; it does not claim the file is bug-free.
Only source declarations and call paths were examined, not runtime coverage.

| File | Source lines | Inventory findings |
|---|---|---|
| [BenthicMovement.cs](../../Assets/_Project/Code/Fishing/BenthicMovement.cs) | 171 | — (data, supplied settings, wiring, or excluded constants only) |
| [BiteDef.cs](../../Assets/_Project/Code/Fishing/BiteDef.cs) | 125 | F058 |
| [BitePhases.cs](../../Assets/_Project/Code/Fishing/BitePhases.cs) | 34 | — (data, supplied settings, wiring, or excluded constants only) |
| [BiteSequenceSim.cs](../../Assets/_Project/Code/Fishing/BiteSequenceSim.cs) | 178 | F059 |
| [CastWaterMath.cs](../../Assets/_Project/Code/Fishing/CastWaterMath.cs) | 61 | — (data, supplied settings, wiring, or excluded constants only) |
| [CatchReleaseRule.cs](../../Assets/_Project/Code/Fishing/CatchReleaseRule.cs) | 28 | — (data, supplied settings, wiring, or excluded constants only) |
| [CatchResolver.cs](../../Assets/_Project/Code/Fishing/CatchResolver.cs) | 317 | F053 |
| [ClamDig.cs](../../Assets/_Project/Code/Fishing/ClamDig.cs) | 669 | F046 |
| [ClamDigger.cs](../../Assets/_Project/Code/Fishing/ClamDigger.cs) | 120 | — (data, supplied settings, wiring, or excluded constants only) |
| [ClamHoleLedger.cs](../../Assets/_Project/Code/Fishing/ClamHoleLedger.cs) | 73 | — (data, supplied settings, wiring, or excluded constants only) |
| [ClamHoleVisual.cs](../../Assets/_Project/Code/Fishing/ClamHoleVisual.cs) | 315 | F024, F065, F066, F067 |
| [ClamSpurtMath.cs](../../Assets/_Project/Code/Fishing/ClamSpurtMath.cs) | 150 | F068, F069, F070, F071 |
| [DeckAngleMath.cs](../../Assets/_Project/Code/Fishing/DeckAngleMath.cs) | 114 | — (data, supplied settings, wiring, or excluded constants only) |
| [DeckPot.cs](../../Assets/_Project/Code/Fishing/DeckPot.cs) | 364 | — (data, supplied settings, wiring, or excluded constants only) |
| [DeckWork.cs](../../Assets/_Project/Code/Fishing/DeckWork.cs) | 93 | — (data, supplied settings, wiring, or excluded constants only) |
| [DeckWorkDef.cs](../../Assets/_Project/Code/Fishing/DeckWorkDef.cs) | 208 | — (data, supplied settings, wiring, or excluded constants only) |
| [DepthDropMath.cs](../../Assets/_Project/Code/Fishing/DepthDropMath.cs) | 226 | F054 |
| [DevFishingInput.cs](../../Assets/_Project/Code/Fishing/DevFishingInput.cs) | 302 | — (data, supplied settings, wiring, or excluded constants only) |
| [DevTrapInput.cs](../../Assets/_Project/Code/Fishing/DevTrapInput.cs) | 215 | F048 |
| [FishEcologyDef.cs](../../Assets/_Project/Code/Fishing/FishEcologyDef.cs) | 52 | — (data, supplied settings, wiring, or excluded constants only) |
| [FishEcologyValidation.cs](../../Assets/_Project/Code/Fishing/FishEcologyValidation.cs) | 74 | — (data, supplied settings, wiring, or excluded constants only) |
| [FishFight.cs](../../Assets/_Project/Code/Fishing/FishFight.cs) | 164 | F004, F005, F006, F007, F008, F009, F010, F026 |
| [FishingController.cs](../../Assets/_Project/Code/Fishing/FishingController.cs) | 1537 | F001, F016, F027, F028, F029, F030, F031, F032, F049, F051 |
| [FishingGroundDef.cs](../../Assets/_Project/Code/Fishing/FishingGroundDef.cs) | 57 | — (data, supplied settings, wiring, or excluded constants only) |
| [FishingSeasonDef.cs](../../Assets/_Project/Code/Fishing/FishingSeasonDef.cs) | 49 | — (data, supplied settings, wiring, or excluded constants only) |
| [FishingSeasonMath.cs](../../Assets/_Project/Code/Fishing/FishingSeasonMath.cs) | 94 | — (data, supplied settings, wiring, or excluded constants only) |
| [FishingStrings.cs](../../Assets/_Project/Code/Fishing/FishingStrings.cs) | 115 | — (data, supplied settings, wiring, or excluded constants only) |
| [FishSchoolMath.cs](../../Assets/_Project/Code/Fishing/FishSchoolMath.cs) | 469 | F062 |
| [FishSchoolModel.cs](../../Assets/_Project/Code/Fishing/FishSchoolModel.cs) | 594 | — (data, supplied settings, wiring, or excluded constants only) |
| [FishSchoolPresenter.cs](../../Assets/_Project/Code/Fishing/FishSchoolPresenter.cs) | 479 | F063, F072, F073, F074 |
| [FishSpeciesDef.cs](../../Assets/_Project/Code/Fishing/FishSpeciesDef.cs) | 253 | — (data, supplied settings, wiring, or excluded constants only) |
| [FishSpeciesLibrary.cs](../../Assets/_Project/Code/Fishing/FishSpeciesLibrary.cs) | 40 | — (data, supplied settings, wiring, or excluded constants only) |
| [FishSpeciesRegistrar.cs](../../Assets/_Project/Code/Fishing/FishSpeciesRegistrar.cs) | 36 | — (data, supplied settings, wiring, or excluded constants only) |
| [FishSpeciesRegistry.cs](../../Assets/_Project/Code/Fishing/FishSpeciesRegistry.cs) | 147 | — (data, supplied settings, wiring, or excluded constants only) |
| [FishSwimSpriteLibrary.cs](../../Assets/_Project/Code/Fishing/FishSwimSpriteLibrary.cs) | 214 | F075 |
| [FlipbookMath.cs](../../Assets/_Project/Code/Fishing/FlipbookMath.cs) | 49 | — (data, supplied settings, wiring, or excluded constants only) |
| [Gear.cs](../../Assets/_Project/Code/Fishing/Gear.cs) | 138 | — (data, supplied settings, wiring, or excluded constants only) |
| [GullSplashLog.cs](../../Assets/_Project/Code/Fishing/GullSplashLog.cs) | 151 | — (data, supplied settings, wiring, or excluded constants only) |
| [HabitatTrapCatch.cs](../../Assets/_Project/Code/Fishing/HabitatTrapCatch.cs) | 58 | — (data, supplied settings, wiring, or excluded constants only) |
| [IFishSchoolWorld.cs](../../Assets/_Project/Code/Fishing/IFishSchoolWorld.cs) | 197 | F052 |
| [JigMath.cs](../../Assets/_Project/Code/Fishing/JigMath.cs) | 106 | F011, F056, F057 |
| [JigWork.cs](../../Assets/_Project/Code/Fishing/JigWork.cs) | 162 | F012, F013, F014, F015 |
| [LivingFishBehaviour.cs](../../Assets/_Project/Code/Fishing/LivingFishBehaviour.cs) | 64 | — (data, supplied settings, wiring, or excluded constants only) |
| [LobsterPotComposition.cs](../../Assets/_Project/Code/Fishing/LobsterPotComposition.cs) | 185 | — (data, supplied settings, wiring, or excluded constants only) |
| [LonglineCatch.cs](../../Assets/_Project/Code/Fishing/LonglineCatch.cs) | 91 | — (data, supplied settings, wiring, or excluded constants only) |
| [LonglineDef.cs](../../Assets/_Project/Code/Fishing/LonglineDef.cs) | 35 | — (data, supplied settings, wiring, or excluded constants only) |
| [LonglineGeometry.cs](../../Assets/_Project/Code/Fishing/LonglineGeometry.cs) | 56 | — (data, supplied settings, wiring, or excluded constants only) |
| [LonglinePlacement.cs](../../Assets/_Project/Code/Fishing/LonglinePlacement.cs) | 42 | — (data, supplied settings, wiring, or excluded constants only) |
| [NpcPotDay.cs](../../Assets/_Project/Code/Fishing/NpcPotDay.cs) | 395 | — (data, supplied settings, wiring, or excluded constants only) |
| [NpcPotDayCatch.cs](../../Assets/_Project/Code/Fishing/NpcPotDayCatch.cs) | 239 | — (data, supplied settings, wiring, or excluded constants only) |
| [NpcPotDayDef.cs](../../Assets/_Project/Code/Fishing/NpcPotDayDef.cs) | 251 | — (data, supplied settings, wiring, or excluded constants only) |
| [NpcPotDayLibrary.cs](../../Assets/_Project/Code/Fishing/NpcPotDayLibrary.cs) | 19 | — (data, supplied settings, wiring, or excluded constants only) |
| [NpcPotLanding.cs](../../Assets/_Project/Code/Fishing/NpcPotLanding.cs) | 234 | — (data, supplied settings, wiring, or excluded constants only) |
| [NpcTrawlDay.cs](../../Assets/_Project/Code/Fishing/NpcTrawlDay.cs) | 314 | F064 |
| [NpcTrawlDayDef.cs](../../Assets/_Project/Code/Fishing/NpcTrawlDayDef.cs) | 86 | — (data, supplied settings, wiring, or excluded constants only) |
| [PlacedTrap.cs](../../Assets/_Project/Code/Fishing/PlacedTrap.cs) | 248 | — (data, supplied settings, wiring, or excluded constants only) |
| [PlacedTrapCatch.cs](../../Assets/_Project/Code/Fishing/PlacedTrapCatch.cs) | 158 | F002 |
| [PlacedTrapService.cs](../../Assets/_Project/Code/Fishing/PlacedTrapService.cs) | 434 | — (data, supplied settings, wiring, or excluded constants only) |
| [PotDeckWorkController.cs](../../Assets/_Project/Code/Fishing/PotDeckWorkController.cs) | 914 | F095, F096 |
| [PotRopeMath.cs](../../Assets/_Project/Code/Fishing/PotRopeMath.cs) | 60 | — (data, supplied settings, wiring, or excluded constants only) |
| [RodFightDef.cs](../../Assets/_Project/Code/Fishing/RodFightDef.cs) | 159 | — (data, supplied settings, wiring, or excluded constants only) |
| [RodFightMath.cs](../../Assets/_Project/Code/Fishing/RodFightMath.cs) | 366 | — (data, supplied settings, wiring, or excluded constants only) |
| [RodFightMotionMath.cs](../../Assets/_Project/Code/Fishing/RodFightMotionMath.cs) | 225 | F017, F018, F019, F020, F033, F034, F035, F036, F037, F038 |
| [RodFightPhases.cs](../../Assets/_Project/Code/Fishing/RodFightPhases.cs) | 67 | F039 |
| [RodFightRhythm.cs](../../Assets/_Project/Code/Fishing/RodFightRhythm.cs) | 84 | F060 |
| [RodFightSim.cs](../../Assets/_Project/Code/Fishing/RodFightSim.cs) | 209 | — (data, supplied settings, wiring, or excluded constants only) |
| [SchoolField.cs](../../Assets/_Project/Code/Fishing/SchoolField.cs) | 223 | — (data, supplied settings, wiring, or excluded constants only) |
| [SchoolInfluence.cs](../../Assets/_Project/Code/Fishing/SchoolInfluence.cs) | 173 | F055 |
| [SeaFightMath.cs](../../Assets/_Project/Code/Fishing/SeaFightMath.cs) | 161 | F003, F040 |
| [SeaPromise.cs](../../Assets/_Project/Code/Fishing/SeaPromise.cs) | 75 | — (data, supplied settings, wiring, or excluded constants only) |
| [ShoalEventMath.cs](../../Assets/_Project/Code/Fishing/ShoalEventMath.cs) | 309 | F091, F092, F093, F094 |
| [ShoalMath.cs](../../Assets/_Project/Code/Fishing/ShoalMath.cs) | 262 | F076, F077, F078, F079, F080, F081, F082, F083, F084, F085, F086, F087, F088, F089, F090 |
| [SpeciesCatchItemFactory.cs](../../Assets/_Project/Code/Fishing/SpeciesCatchItemFactory.cs) | 32 | — (data, supplied settings, wiring, or excluded constants only) |
| [StableHash.cs](../../Assets/_Project/Code/Fishing/StableHash.cs) | 110 | — (data, supplied settings, wiring, or excluded constants only) |
| [StartingBait.cs](../../Assets/_Project/Code/Fishing/StartingBait.cs) | 93 | F047 |
| [StrikeGesture.cs](../../Assets/_Project/Code/Fishing/StrikeGesture.cs) | 84 | F061 |
| [SwimmerVisibility.cs](../../Assets/_Project/Code/Fishing/SwimmerVisibility.cs) | 84 | — (data, supplied settings, wiring, or excluded constants only) |
| [TackleDef.cs](../../Assets/_Project/Code/Fishing/TackleDef.cs) | 57 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrapDef.cs](../../Assets/_Project/Code/Fishing/TrapDef.cs) | 105 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrapFill.cs](../../Assets/_Project/Code/Fishing/TrapFill.cs) | 111 | F025 |
| [TrapHabitat.cs](../../Assets/_Project/Code/Fishing/TrapHabitat.cs) | 42 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrapHabitatDef.cs](../../Assets/_Project/Code/Fishing/TrapHabitatDef.cs) | 35 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrapHaulController.cs](../../Assets/_Project/Code/Fishing/TrapHaulController.cs) | 696 | F021, F022, F023, F041, F042, F043, F044, F045, F050, F097, F098, F099, F100 |
| [TrapHaulMath.cs](../../Assets/_Project/Code/Fishing/TrapHaulMath.cs) | 186 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrapPlacement.cs](../../Assets/_Project/Code/Fishing/TrapPlacement.cs) | 59 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrapRotationDef.cs](../../Assets/_Project/Code/Fishing/TrapRotationDef.cs) | 27 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrapRotationPlanner.cs](../../Assets/_Project/Code/Fishing/TrapRotationPlanner.cs) | 88 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrapSoak.cs](../../Assets/_Project/Code/Fishing/TrapSoak.cs) | 56 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrapSplashFx.cs](../../Assets/_Project/Code/Fishing/TrapSplashFx.cs) | 146 | F101 |
| [TrawlDeckBatch.cs](../../Assets/_Project/Code/Fishing/TrawlDeckBatch.cs) | 50 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrawlDefinitions.cs](../../Assets/_Project/Code/Fishing/TrawlDefinitions.cs) | 63 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrawlGearAssetDef.cs](../../Assets/_Project/Code/Fishing/TrawlGearAssetDef.cs) | 22 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrawlHandlingAssetDef.cs](../../Assets/_Project/Code/Fishing/TrawlHandlingAssetDef.cs) | 22 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrawlOperation.cs](../../Assets/_Project/Code/Fishing/TrawlOperation.cs) | 63 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrawlSession.cs](../../Assets/_Project/Code/Fishing/TrawlSession.cs) | 319 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrawlSessionDef.cs](../../Assets/_Project/Code/Fishing/TrawlSessionDef.cs) | 33 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrawlSessionGround.cs](../../Assets/_Project/Code/Fishing/TrawlSessionGround.cs) | 65 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrawlSurvey.cs](../../Assets/_Project/Code/Fishing/TrawlSurvey.cs) | 190 | — (data, supplied settings, wiring, or excluded constants only) |
| [TrawlTowSolver.cs](../../Assets/_Project/Code/Fishing/TrawlTowSolver.cs) | 197 | — (data, supplied settings, wiring, or excluded constants only) |

## Existing proof to extend in an implementation

The pre-existing [fishing behaviour/test map](../verification/fishing-behaviour-coverage.md)
was found before considering new checks. This audit adds no test or validator.
Relevant existing EditMode fixtures include `FishFightTests.cs`,
`RodFightMathTests.cs`, `RodFightMotionMathTests.cs`, `RodFightSimTests.cs`,
`RodFightDataTests.cs`, `BiteSequenceSimTests.cs`, `BiteSequenceControllerTests.cs`,
`JiggingTests.cs`, `SeaFightMathTests.cs`, `TrapFillTests.cs`,
`PlacedTrapCatchTests.cs`, `TrapHaulMathTests.cs`, `TrapHaulControllerTests.cs`,
`ClamDigTests.cs`, `ClamRefinementsTests.cs`, `ClamSpurtMathTests.cs` and
`StartingBaitOnceTests.cs`, all under `Assets/Tests/EditMode`.

A later extraction should extend those fixtures, preserve current values and
random-draw order, and prove equivalent results for identical seed/time/input.
The guaranteed ready catch, all-pull snap rule, run/slack safety, and slow-jig
recognition are rules to preserve or explicitly migrate, not weaken. Presentation
entries need their own authorized visual proof. Existing assertions were not run
for this documentation change.

# Wake stern, heading and speed response — v24

This change attaches new wake to the mesh's authored transom through turns, including the intro Cape. The visual child intentionally stays at identity rotation; it is not the boat's heading. `FoamInjector` previously read that child's `up`, placing its root northward regardless of the boat's yaw. Restoring the old injector makes the independent pose guard fail. The mesh projection invariant remains intact.

The three conspicuous bands come from `BoatWakeEmitter`'s two **crest** streams and central **sternRoll** stream. They are pooled sprite renderers drawing the wake-wave crest/trough artwork through **Universal Render Pipeline/2D/Sprite-Lit-Default**, confirmed from the live renderers. Per-family GPU captures distinguish them from deposited foam, sheet foam and bubbles. The retired `plume` and `bowSpray` paths contribute zero changed pixels with the shipped switches. In particular, the old photograph fixture's “ALL drawn” measured only sheet/deposits/crests; the new `complete.png` also includes stern roll and every other enabled family.

Birth opacity and width now use a continuous drive factor: onset × speed / (speed + configured `SpeedRefMax`). The existing reference speed is the half-response knee once onset is complete, rather than an early saturation ceiling. Hull size/mass grading remains. Crest extent uses birth strength and age spread; existing lifetime/fade still removes history. Speed is relative to the sampled current. Wind drift remains **0.30**, and turning never rotates historical deposits.

## GPU evidence

![Cape before and after](cape-before-after.png)

![Speed and age response](cape-speed-age.png)

![Fleet turns](fleet-turns.png)

![Authored intro route](intro-route.png)

![Continuous acceleration and release](cape-ramp.png)

These are real GPU captures from Unity **6000.5.0f1**, D3D11, the game's camera/post processing, WorldSeed **12345**, and fixed 1/30-second simulation frames. The NMC clock is held at **2625 s / 11:00**. Cardinal/turn legs run at 6 m/s for four seconds; turns are ±22°/s. Live wave pitch remains active, rather than claiming zero pitch. The west Cape camera has orthographic size 20.75 m; the clockwise turn is 23.38 m; original frames are 2133×1600. Speed plates use **0.044538 m/pixel**. Contact sheets are resized for reading; tolerances refer to original images, not these thumbnails.

Baseline source is `bc947ddd7e65c97c98c8d1dcf5069552fb7327cf` for both original wake consumers. The additive pose provider was present but unused by those consumers. Fixed captures use this PR's implementation. Full SHA-256 values and source paths are in [image-provenance.json](image-provenance.json). Ambient birds/clouds differ between runs despite the controlled clock and trajectory: whole-image hash differences alone are **not** the proof. Same-frame bare-water/family isolation captures and measured renderer values supply the controls.

| Water-relative speed | Peak crest alpha | Maximum crest length | Wake-family changed pixels |
|---:|---:|---:|---:|
| 0.5 m/s | 0 | 0.010 m | 135330 |
| 2 m/s | 0.088497 | 1.312909 m | 153298 |
| 6 m/s | 0.352120 | 2.610624 m | 218940 |
| 12 m/s | 0.455816 | 3.049103 m | 403354 |

Changed pixels include all enabled wake families; they are not a claim that crests emit below their gate. After release to zero water-relative speed, 24 crests remain at three seconds and none at twelve seconds. No abrupt deletion of old wake is used.

The additional continuous ramp takes one Cape from 0 to 12 m/s over six seconds, back to zero over six seconds, then holds zero water-relative speed for twelve seconds. At frames 90/180/270/360/720, the live crest counts are 48/72/71/41/0. Its maximum birth error is 0.000389 m (0.00722 final pixels). The large white vessels in that wider plate are stationary authored region boats; the moving brown Cape is the subject. The steady-speed and ramp fixtures set velocity directly to isolate wake response; they do not test controller throttle/drag tuning.

## Independent attachment oracle and coverage

The oracle transforms the authored transom `(0, -WakeSternOffsetMeters, RestingDraftMeters)` through the **actual `IsoFacetHullRenderer.PosedMesh` transform**. It does not call the projection helper under test. Existing `WakeSternOffsetRigTests` independently verify the authored stern station against original rig loft data. This is a verified authored design-waterline point, not a dynamically solved intersection of triangles with the instantaneous ocean surface.

The fixed tolerance is **0.0625 m**, two authored pixels at 32 pixels/metre. Across **39 hull definitions / 2340 poses**, the maximum source error is **0.000034 m** and maximum foam-birth error is **0.000748 m**: respectively **0.00136 and 0.02992 final pixels** at the measured 40 pixels/metre camera. Coverage includes ten headings, pitch −6/0/+6°, roll 2°, heave 0.25 m, tides −0.7/+0.9 m, and repeated hull swaps on one host. The deliberately wrong identity-child heading is rejected in **234** cases. The old-consumer control fails with maximum birth error about **75.29 m** across the fleet; this maximum belongs to large hulls, not specifically the Cape.

Cape, dory, punt and lobster have GPU north/east/south/west and continuous clockwise/counter-clockwise captures. All other mesh hulls have pose/swap coverage only; their complete inventory is in `fleet-pose-inventory.txt`. The authored St Peters dev-key roster has 24 entries; all are pose-verified, with per-entry GPU coverage listed in `dev-roster-coverage.json`. No claim of a full GPU sweep for the other hulls is made. The final oracle exercises **`DevBoatPicker.Show`** itself for every hull, not literal key injection.

The authored St Peters intro traverses its four route marks and both turns, with **1152 completed-frame samples**, **82.224°** heading sweep, maximum source error **0.000036 m**, and birth error **0.000160 m**. At orthographic size 21 m / 1600 pixels that is **0.00137 / 0.00610 pixels**. Speeds captured at the marks are 4.975, 3.869, 3.569 and 1.339 m/s. The independent passenger integration test also passes through step-ashore. This wake fixture stops at docking after observing both turns; it does not itself assert the full mooring/walking sequence.

## Pose contract for the separate trim follow-up

`Core.IHullWakePoseSource.TryGetWakePose` supplies `HullWakePose`:

- `DrawnStern`: current authored design-waterline transom in screen-world metres, after mesh yaw, bake elevation, applied roll/pitch/heave and the visual's tide translation.
- `Heading`: normalized physical plan-view bow direction from the boat root, **not** the identity-rotated mesh visual.
- `TideRise`: the visual's vertical translation relative to the boat root. Consumers remove this and invert displaced-water drawing lift at birth; drawing adds the current local water lift back once.

`BoatWaveMotion` runs at −120, `MeshHullDriver` at −110, the sprite emitter at +20 and foam injector at +30. Readers must use a completed pose. The old tide fixture now samples pose and foam together after these late updates; its two-pixel stripe assertion and tolerances are unchanged. Legacy non-mesh hosts retain the old projected stern fallback.

Trim should alter the presenter's applied pitch/heave channels, then consume this same contract. Do not add a second stern offset in a controller or rotate a historical wake root. History stays in water/world coordinates; only new births follow the hull. Existing configured astern nudges still offset particular sprite streams from the central authored point. No speed/deceleration trim or turning-force model is implemented here.

## Validation and limits

Focused EditMode: **249 passed, 0 failed, 0 skipped** across grading, particles, trail/wave/projection math, rig stern station, one-root, mesh presenter, foam ageing/lifecycle, wake lift, tide ride, bubbles and dispersal. Original deposit guards, both original tide guards, intro route, and passenger integration pass. New GPU tests require a graphics device and intentionally skip headless/null-device CI; CI alone cannot validate these images.

The initial combined intro/punt run skipped punt because the following region had no registered clock; isolated punt rerun passed all six views. Two intermediate fixture compile errors and earlier preparation skips are retained in the local evidence log, not concealed as successful runs. Scene exports do not apply: no scene, prefab, Data asset or builder source is changed. No standalone player build or new controller pivot diagnosis is claimed. Owner visual review remains required before taking this draft out of draft.

The corrected speed guard is red with the original emitter: measured crest lengths at 2 and 6 m/s are 3.545259 and 3.534802 m, failing monotonic growth. The final fixed rerun passes with the table's alpha/length values. That negative run wrote complete failing XML, then exited with Windows status 0xC0000005 during shutdown; it is not counted as a clean process exit. A later combined ramp/response run stalled loading its second scene and was terminated; separate ramp and response reruns both completed with exit 0. Those harness limitations remain documented; no failed combined run is presented as green.

Full editor launch/environment records, XML, unresized GPU captures, negative controls and slot/save accounting are in the checkout's `Evidence~/wake-resumed-v24/REPORT-v24.md` and sibling files. All requested paths remain inside the box. The report separately records Unity-managed licensing and default Test Runner output written outside it.

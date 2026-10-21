# Hull drag and the water frame

Status: draft for gameplay-systems and lead-architect review; native integration gate passed.

The tide carries an untended hull. Hull drag and the body's linear damping oppose velocity
through the water, `u = velocity - EnvironmentSample.CurrentVector`, for oars, motors, sails,
the unattended deck and a disembarked hull on its painter. This serves P1, P4 and P5: maintaining
a ground track or holding station needs the helm, and a rope restrains real drift.

Keep `Rigidbody2D.linearDamping` and add `mass * linearDamping * current` in native force units
(no ForceFeelScale). For force-then-implicit-damping, `v' = (v + h*a)/(1+h*d)`, this gives
`v' - c = (v - c + h*a)/(1+h*d)`. Still water adds no force and keeps the old step exactly.
Setting damping to zero and adding explicit `-mass*d*u` instead changes still-water integration,
so it is not the selected draft.

The controller owns the correction during its manned/unmanned force pass; BoatMooring owns it
while the controller is disabled and a line is out. The common pure helper reads the live body
damping, preserving deliberately undamped bodies. A kinematic body ignores AddForce.

Hull trim's damping acceleration uses along-keel water speed. SailDrive.LinearResistance stays
the continuous steady resistance identity; sails and held trim speed use that water-frame balance.
GroundedSlowdownForce already acts through the water and remains a forgiving drag, not a pin to
the seabed. The correction therefore still sets a grounded hull with the stream. ShallowsHoldForce
stays ground-relative: it arrests approach to fixed terrain. The rope's ground-relative damping,
inextensible clamp and load/slip rules remain. Seakeeping's small ground-relative linear/angular
damping remains a separate sea-response term. Angular body damping is unchanged.

The native constant-force Rigidbody2D case measured 2.6144557 m/s against 2.61445618 m/s
for force-then-damping and 2.79879332 m/s for damping-then-force. This supports the selected
correction. Native five-minute drift measured 0.00359981 kn for Sloop 30 and 0.000271159 kn
for Sloop 88, both below the unchanged 0.5 kn through-water bar. The full-helm native
head-to-wind crossing took 12.9601068 s for the 30 and 11.0200624 s for the 88. The 30's
margin under the unchanged 13 s bar is only 0.0398932 s; no acceptance threshold was changed.

Phase B2 recorded 365 executions: 363 passed, one failed and one skipped, including one
isolated recheck. The skip is the existing Tanker art block. The failure remains OPEN:
IntroCabinPassagePlayTests.BelowDecks_AHeldIntentWalksHer_ThroughTheArrivalsOwnUpdate
logged an IOException from File.Replace into the run's Temp/TestRunSaves save. Its isolated
recheck passed unchanged; CI must run it again. All 199 focused EditMode cases, seven new
PlayMode gates, twelve native sloop controller cases, and the completed cabin/fleet,
rope/anchor/deck/sail/wake/buoy and Nine Mile Creek batches have results recorded.

The eight-class St Peters batch is UNCONFIRMED: its ground-plate capture stalled and the
stopped launch produced no results XML. Fourteen further real-region classes were NOT RUN.
The owner's real-current motion plate is NOT RUN. CI is next for cases needing no GPU;
rendered acceptance and owner motion review remain pending. The alternate-camera
nmc-fleet-paint.png shows no hulls, and nothing is claimed from it.

The slot ran at f54f70dd; main is now 09e74633, with no rebase of this branch. Main's
push run 39660798183 is settled at EditMode 14962/14695/267/0 and PlayMode 1133/981/152/0
(total/passed/skipped/failed). The seven new EditMode cases, one existing case unskipped
and seven new PlayMode cases forecast EditMode 14969/14703/266/0 and PlayMode 1140/988/152/0,
conditional on CI. Main's nineteen additional cases since 941d5b5a were NOT RUN natively:
NineMileCreekCrossingDoorTests (7), StPetersCrossingDoorTests (7), ShallowsFloorShaderTests (1),
ShallowsFloorTests (1), ShallowsPropsTests (2), NineMileCreekCrossingDoorPlayTests (1).

Rule 6 debt: HullLinearDamping remains the existing 0.2 C# constant to isolate this measured
change. A separate data change should expose it in BoatHullDef or GameConfig, with asset and
calibration review. No new tunable, save field, randomness or per-step allocation is introduced.

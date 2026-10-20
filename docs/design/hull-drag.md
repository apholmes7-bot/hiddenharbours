# Hull drag and the water frame

Status: draft for gameplay-systems and lead-architect review; native integration gate pending.

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

The native integration order is not established by managed bindings. If Unity instead damps
before adding force, the proposed correction oversets an otherwise free body by `h*d*c`
(0.4% at h=0.02, d=0.2). The Phase B constant-force Rigidbody2D case must decide the order;
do not call the draft exact in-engine before that gate. Revisit the correction if it rejects
force-then-damping rather than silently weakening the settling bar.

Rule 6 debt: HullLinearDamping remains the existing 0.2 C# constant to isolate this measured
change. A separate data change should expose it in BoatHullDef or GameConfig, with asset and
calibration review. No new tunable, save field, randomness or per-step allocation is introduced.

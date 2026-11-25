# Nine Mile Creek fish plant: truck court

Status: paper design, audited at `00b030cf8d286bc9d3b1464acd55c025e5a26a32`.
Site A is selected. **Keep the 26 m court and the 48 x 28 m building.** An
8 x 2.5 m planning truck can enter from the wharf approach, reverse to either
intake position and leave forward. One more truck can wait on the access spur
clear of Route 91. This requires a **12 m clear gate**, a widened spur, and
exclusive use of the manoeuvring area. It is a geometric feasibility result,
not a claim that today's scheduled NPC driver can perform it.

No scene, ground, vehicle, art, traffic or economy setting changes here. The
[scene brief](nmc-fish-plant-key-scene-brief.md) carries the building programme.
The [siting study](nmc-fish-plant-siting.md#roads-and-connections) supplies the
road and neighbour reservations; the new coordinates below are design, not
committed road nodes.

## 1. What main actually supplies

Paths in this table are relative to `Assets/_Project/`. Values are metres,
degrees and metres/second unless stated otherwise. Length and width below are
**closed-body collider envelopes**, not loaded vehicle or mirror measurements.

| Source / fields | Main value and consequence |
|---|---|
| `Data/FishSupplyChain/nmc_to_plant_run.asset`, `Spec.VehicleId`, `ExistingTripId`, `ExistingRouteId` | `vehicle.dually_3500`, `trip.nmc_fish_buyer_run`, empty. The chain references a pickup and a precedent timetable, not an operating refrigerated plant delivery. |
| Other eight `Data/FishSupplyChain/*_run.asset` files, same fields | All use `vehicle.dually_3500`; trip and route references are empty. No dedicated reefer, industrial driver or plant path is assigned. |
| `Code/App/Editor/NineMileCreekTruckPark.cs`, `DuallyDefPath`; `NineMileCreekTrips.cs`, `FishBuyerAssetName`, `Place`, `OutboundRoute`, `ReturnRoute` | The existing buyer drives the park's dually to the wharf. `FishBuyerAssetName` resolves the existing buyer NPC (`npc.wendell_arsenault`), not a new factory worker. Preserve that selling routine. |
| `Data/Vehicles/Trips/FishBuyerRun.asset` | `OutboundDepartureHour: 4.75` (04:45), `ReturnDepartureHour: 20.5` (20:30), `CruiseMetresPerSecond: 7`, `WalkMetresPerSecond: 1.4`. These are the buyer's hours, not the plant's proposed slots. |
| `Data/Vehicles/Dually3500.asset` | `Id: vehicle.dually_3500`; `MaxSpeedMetersPerSecond: 11`; `MaxReverseSpeedMetersPerSecond: 4`; `SteerRateFullLocksPerSecond: 2`; `SteerReturnFullLocksPerSecond: 3`; `SteerFalloffStartMetersPerSecond: 4`; `SteerFalloffHalfSpeedMetersPerSecond: 9`. |
| `Data/Vehicles/CaboverBox.asset` | Candidate small straight truck, `Id: vehicle.cabover_box`; same fields in the preceding order: **10, 3.4, 1.7, 2.5, 4, 8.31**. Not assigned to the fish chain. |
| `Data/Vehicles/ConvBox.asset` | Larger rigid alternative, `Id: vehicle.conv_box`; same fields: **10.5, 3.2, 1.5, 2.2, 4, 9.73**. Not assigned to the fish chain. |

Geometry comes from each Def's `Mesh` reference, in
`Data/Vehicles/Meshes/`. `VehicleDef` deliberately puts these measurements on
`VehicleMeshDef`, not on handling tunables.

| Mesh asset | `WheelbaseMeters` / `FrontTrackMeters` | `FrontAxleY` / `RearAxleY` | `MaxInnerSteerDegrees` / `MaxOuterSteerDegrees` | `ColliderMinMeters` / `ColliderMaxMeters` (x,y,z) | Derived length x width |
|---|---|---|---|---|---|
| `Dually3500VehicleMesh.asset` | 4.30 / 1.80 | 2.18 / -2.12 | 30 / 24.94 | (-1.24,-3.34,0) / (1.24,3.30,2.05) | 6.64 x 2.48 |
| `CaboverBoxVehicleMesh.asset` | 4.12 / 1.56 | 2.62 / -1.50 | 33 / 27.53 | (-1.07,-3.30,0) / (1.07,3.42,3.08) | 6.72 x 2.14 |
| `ConvBoxVehicleMesh.asset` | 6.10 / 1.72 | 3.20 / -2.90 | 35 / 30.32 | (-1.25,-5.30,0) / (1.25,4.30,3.72) | 9.60 x 2.50 |

There is no authored turning-radius field. Core's
[`VehicleSteeringMath`](../../Assets/_Project/Code/Core/Vehicles/VehicleSteeringMath.cs)
derives rear-axle radius from Ackermann steering:

`R = wheelbase / tan(inner lock) + front track / 2`.

This gives **8.348 m dually, 7.124 m cabover, 9.572 m conventional** at full
low-speed lock. The controller recomputes the outer angle; the rounded asset
outer angles are not a second independent constraint. Speed above the 4 m/s
falloff start widens turns. These are axle radii, not outside-body radii.

Missing: a selected refrigerated body; its loaded/mirror envelope, opening-door
and liftgate clearance; physical cargo layout; a plant driver; a plant route;
gate width; yard speed and dock choreography. An asset's cell size is not a
truck length. Nothing here converts the existing pickup into a reefer.

### ASSUMED planning truck

Use **8.00 m overall, 2.50 m operating width including a mirror allowance,
4.47 m wheelbase, 1.53 m front overhang, 2.00 m rear overhang**, and a **9.00 m
rear-axle path radius**. All are ASSUMED for this layout, not new Def values.
The manufacturer’s [NPR-HD diesel chassis sheet, standard-cab table](https://www.isuzucv.com/en/app/site/pdf?file=npr-hd_diesel_specs.pdf)
provides a real small-straight-truck scale: 176 in wheelbase = 4.4704 m,
267.5 in chassis length = 6.7945 m, suitable for an 18–20 ft body. The 8 m
finished reefer envelope, 2.5 m width, overhang allocation and 9 m radius are
our conservative design assumptions; they are **not manufacturer specifications**.
Refrigeration and body fit still need selection. A rear-axle bicycle angle
`atan(4.47/9) = 26.41 degrees` defines this assumed turn.

Relative to the rear axle, the swept rectangle is longitudinal **[-2,6]**,
transverse **[-1.25,1.25]**. It contains the measured dually and cabover
rectangles in that frame, and its chosen path is wider than their minimum
axle circles. It does **not** contain the conventional truck: that vehicle's
front reach is 7.20 m and rear overhang 2.40 m. Larger rigids and articulated
export combinations are PARKED for another envelope study. Export here uses
the same small straight-truck envelope.

Assume manoeuvring at no more than **1 m/s**, stopped steering changes where
curvature changes, a **0.5 m obstacle margin**, and a spotter with the court
closed to pedestrians during movement. The operating width includes mirrors
only by assumption; open doors/liftgate deploy only after parking in a separately
cleared work apron. No animation or support structure is authorized here.

## 2. Frame and reproducible swept path

Use local **u = world x + 241**, **v = world y + 40**. East and north are
positive; 1 unit = 1 m. Thus:

| Reservation | Local coordinates | World coordinates |
|---|---|---|
| Site | u [-54,26], v [-30,30] | x [-295,-215], y [-70,-10] |
| Building | u [-48,0], v [-14,14] | x [-289,-241], y [-54,-26] |
| Court | u [0,26], v [-30,30] | east facade to east boundary |
| Gate centre / road junction datum | (26,0) / (51,0) | (-215,-40) / (-190,-40) |
| Proposed clear gate opening | u 26, v [-6,6] | x -215, y [-46,-34] |
| Intake I1 / I2 rear bumper | (0.5,0) / (0.5,8) | (-240.5,-40) / (-240.5,-32) |
| Waiting truck rear axle, facing west | (32,4) | (-209,-36) |
| Protected public/staff path | v [26,28], across court | y [-14,-12] |

Every path coordinate below is the **rear axle**, not the truck root or nose.
For heading `h` measured counterclockwise from east, every body point is

`Q = P + s(cos h, sin h) + t(-sin h, cos h)`,
`-2 <= s <= 6`, `-1.25 <= t <= 1.25`.

For an arc, `P = C + 9(cos a, sin a)`; forward heading is the tangent,
reverse heading is the opposite tangent. Checking all four corners over the
continuous angle interval gives its sweep. A useful conservative circle bound is
`sqrt((9+1.25)^2 + 6^2) = 11.876974 m`; including margin, diameter is
**24.753948 m**. This is the minimum clear diameter for this chosen radius and
rectangular envelope, not a proof of a globally minimum reversing court.

An S-curve with east/west initial and final headings, lateral displacement d,
and two opposite radius-9 arcs has
`theta = acos(1-|d|/18)` and longitudinal travel
`D = 18 sin(theta) = 2 sqrt(9|d|-d^2/4)`.
Thus D(4) = **11.313708 m**, D(8) = **14.966630 m**, D(18) = **18 m**.
These equations define curves, not straight chords between the listed points.

### Rank 1: two face positions, one manoeuvre at a time

**Road approach.** Use the wharf approach from the north, along Route 91's
(-181,10) to (-190,-40) segment. Its angle to the westbound spur is 79.796°.
A radius-9 fillet at the junction has tangent setback
`9 tan(79.796/2) = 7.524638 m`:

- Road tangent start **(52.333012,7.405623)**, heading southwest along Route 91.
- Circle centre **(43.475362,9)**, clockwise radial angles -10.204° to -90°.
- Spur tangent end **(43.475362,0)**, heading west. Continue to **(23,0)**.

This replaces the impossible sharp corner at (51,0). The truck passes the
gate at v = 0. Reserve the junction sweep, including 0.5 m margin, within
**u [36.9,55.9], v [-3.4,14.1]** and check it against the actual road/verge.
This is a clearance reservation, not a command to pave the whole rectangle.
The 5.8 m carriageway must be clear while turning; this document proves no
opposing-traffic pass or southern approach. Inter-region arrivals stay blocked
until their routes and junction directions are supplied.

| Leg | Rear-axle coordinates / curve | Nose / gear |
|---|---|---|
| Set up | From (23,0), centre (23,9), radial -90° to -180°; end (14,9) | west to north, forward |
| Choose bay b | Straight (14,9) to (14,9+b), where b = 0 or 8 | north, forward; zero length for I1 |
| Back round | Centre (5,9+b), radial 0° to -90°; (14,9+b) to (5,b) | north to east, reverse |
| Back straight | (5,b) to (2.5,b) | east, reverse; rear bumper stops at u 0.5 |
| Leave I1 | (2.5,0) east to (43.475362,0) | forward through original gate |
| Leave I2 | (2.5,8) to (9,8); S-curve down 8 m to (23.966630,0); continue to (43.475362,0) | east, forward throughout |
| Rejoin road | Centre (43.475362,9), radial -90° to -10.204° | east to northeast, forward onto the north leg |

At either dock the body is **u [0.5,8.5], v [b-1.25,b+1.25]**. The remaining
17.5 m between nose and boundary is not another parking row: it is manoeuvring
space. Reserve the 0.5 m rear gap for a supported transfer connection. Workers
carry goods between supported truck storage and intake; no truck floats, and
no inventory moves because it merely reached a coordinate.

**The second truck waits off the road.** After the same road turn, use an
S-curve from (43.475362,0) to (32.161654,4), then creep to **(32,4)** facing
west. Its entire stationary body is **u [26,34], v [2.75,5.25]**. The road's
west edge at the junction datum is about u 48.1: the waiting tail is **14.1 m
clear of it**, and the entry turn has already finished. Eastbound departures
on v 0 have 1.5 m body-to-body separation from that waiting rectangle.

Once the active truck has left, the queued truck takes an S-curve from (32,4)
to **(20.686292,0)**. Use that point instead of (23,0) in the I1 construction:
setup centre (20.686292,9), end (11.686292,9), reverse centre (2.686292,9),
end (2.686292,0), then reverse straight to (2.5,0). It therefore needs no
reverse movement toward the road. A queued truck is admitted to **I1**; I2
uses the direct approach. Only one truck occupies the court, even though two
intake positions are drawn. A third truck waits at its origin, never on Route 91.

Reserve the access spur outside the parcel, **u [26,43.5], v [-3.5,7.5]**,
blending into the junction reservation. This is wider than a nominal 6 m
driveway. The queue-in sweep reaches v 6.877, so a narrow straight strip fails.
The queue's nose can touch the property line; the gate remains fully open and
the holding marker refers to the rear axle. Staff enter by the northern path,
not through this queue.

### Numbers that establish fit

These are full-body bounds before the separate 0.5 m margin, rounded outward
to millimetres. The equations above are the reproducible paper specification.
An independent arithmetic check sampled each arc in 5,000 intervals and used
rectangle separating axes for building/queue intersections; all **8** primary
path groups passed, with **130,023 poses** in total. No Unity tests ran.
The largest adjacent corner motion in the listed primitives is under 0.009 m;
rounding alone is not used as a collision margin.

| Sweep | u range | v range |
|---|---|---|
| Direct approach after setup begins, I1 | [0.500,25.000] | [-1.444,15.000] |
| Same, I2 | [0.500,25.000] | [-1.444,23.000] |
| Queue-in S and final creep | [26.000,45.476] | [-1.444,6.877] |
| Queue release and I1 docking | [0.500,34.000] | [-2.877,15.000] |
| Road entry turn | [37.475,53.919] | [-2.877,9.596] |
| Road exit turn | [41.475,55.353] | [-1.444,13.533] |
| Actual intersections with gate line u 26, all primary legs | — | [-2.702,5.250] |

The building's east wall is u 0; every dock movement remains at or east of
u 0.5. Court sweeps stay south of v 23, leaving 3 m to the protected path at
v 26. Gate clearance is at least **0.75 m** at its tighter northern jamb.
Both forward exits clear the stationary queue. With the stated clearance
reservation, **26 m is sufficient**: no building move, farm-boundary extension,
or encroachment into the estate reservation is needed. The 80 x 60 m site
remains 4,800 m²; its 1,344 m² building is unchanged. The cost is the spur
widening and controlled single occupancy, not extra industrial land.

## 3. Today's NPC limitation and the forward-only alternative

**Today's NPC paths do not reverse.**
[`ScheduledTrip.ApplyMachine`](../../Assets/_Project/Code/Vehicles/ScheduledTrip.cs)
sets position and heading from the clock. Core's
[`VehicleTripPlan.SampleAt`](../../Assets/_Project/Code/Core/Vehicles/VehicleTripPlan.cs)
explicitly says a posed body cannot back out: its nose follows its travel
direction. An untowed trip pivots about its centre during boarding; that is
not a three-point turn. Reversing the waypoint array means a return journey,
not reverse gear. Towed plans reject geometry requiring that pivot.

The separate live [`RouteDriver.Read`](../../Assets/_Project/Code/Vehicles/RouteDriver.cs)
calls `RouteFollowMath.Toward`; it requests positive cruise/turn throttle,
with no reverse-leg flag. Its normal tuning is 3 m waypoint reach, 12 m
lookahead, 0.6 cruise throttle, 0.15 turn throttle, 12° slow threshold and
20° steering gain (`VehicleDef.RouteFollow`, `RouteFollowTuning.Measured/Sane`).
It is not a precision docking controller. Player-controlled
`VehicleController` does support reverse. It applies velocity to the rigidbody
root while deriving yaw from axle geometry; the rear-axle paper path is not a
claim of byte-for-byte runtime motion. Parking the truck in the editor or
letting a scheduled trip pivot cannot validate this study.

### Rank 2: forward-only horseshoe, hand service alongside

If reverse choreography is still unavailable, reserve this alternative; do
not make a key-scene truck pivot invisibly. Same building and parcel, original
entry gate plus a **second exit centred (26,-18)** = world **(-215,-58)**,
12 m clear opening v [-24,-12].

1. Enter on v 0 to rear axle **(14,0)**, facing west.
2. Follow centre **(14,-9)**, radius 9, counterclockwise radial 90° through
   180° to 270°: **(5,-9)** facing south is the service stop; **(14,-18)**
   faces east at departure. Stop and restart on the same tangent.
3. Continue east to **(25.475362,-18)**; take a two-arc S up 18 m, midpoint
   **(34.475362,-9)** facing north, end **(43.475362,0)** facing east.
4. Use the same northbound road-exit fillet. All travel is forward. At the
   second gate the body crosses v **[-19.430,-16.732]**, inside its opening.

The horseshoe body sweeps **u [2.123,20.000], v [-20.877,1.444]**.
An entire circle of this envelope needs **24.754 m** clear diameter with
margin; a **25 x 25 m** reservation fits the court if centred at u 13. The
specified horseshoe instead uses only the western half about u 14, with
the smaller bounds above. The exit needs a second spur
reservation south of the first: **u [26,43.5], v [-20,0]**, blending into the
same junction. This stays north of the parcel's southern edge v -30 and the
estate reservation v -37, but converts more of the access-side farm/verge.
No neighbour is silently taken and neither regional road endpoint moves.

At the side stop the body is u [3.75,6.25], v [-15,-7]. Reserve a supported
ground-level hand route along the facade, **u [0.5,3.0]**, linking the intake
and dispatch doors to the truck's rear at (5,-7). Close it during motion;
deploy the transfer equipment only while stopped. This costs longer hand
carrying and serial loading/unloading, and uses no backed-in dock. The loop
and exit arithmetic also clear the building and the waiting truck. The
forward-only route is geometrically available, but its timed stops, congestion
handling, support and cargo receipts still require integration; existing
waypoint following alone does not install them.

**Ranking:** 1 retains useful rear-facing intake positions with one gate and
the smaller access footprint, but needs reverse-leg work. 2 can avoid reverse
legs at the price of a second exit, more access land and slower hand service.
Neither accepts the 9.6 m conventional truck or a semi-trailer. If that becomes
the required vehicle, PARK the selection and rederive the envelope before
claiming a smallest expanded court; it is not necessary for this small-truck
brief.

## The one editor-only acceptance

An authorized scene/vehicle run must settle **whether the selected truck can
execute this continuous, loaded manoeuvre on the actual Site A ground with
its full collider and all wheels supported, clearing the dock, gate, waiting
truck and protected footpath**. Observe the complete approach–dock–depart
sequence, not parked endpoint screenshots. That is the remaining physical
acceptance; this paper proof accepts no new look or plate baseline.

# Working helm instrument logic

Logic half of the boat-electronics request, serving P1, P2 and P5. Reviewed against main
`12bc04b19a2efb33e352b4eea58104f7a5599be1`. No scene, HUD, rig, hull Def, save or visual activation.

## Existing main: reuse before adding

| Existing owner | Available behavior and limitation |
|---|---|
| Core `IHelmControl`, `HelmSlot`; Boats `HelmControlRelay` | One occupied hull owns drive/steer intents and the glass. Anchor control already has a Core switch seam. |
| Core `IHelmInstruments`, `HelmFit`, `SounderPrefs` | Effective per-hull console equipment, throttled water-column depth, position and persisted sounder preferences. Water-column depth is not under-keel clearance. |
| Boats `HelmConsoleDef`, `BoatEquipment`; Core `InstrumentLocker` | Console defaults and purchased per-hull instrument ids resolve once through the existing slot/capability rules. No second shop, ownership list or save schema is needed. |
| Core `DepthSounder`, `NavMath`, `BoatKinematics`, `SailDrive.ResolveWind` | Existing depth/alarm, compass/range, SOG/heading and true/apparent wind primitives. Reuse their conventions and arithmetic. |
| Core `IFuelVessel`, `FuelBurn`, `FuelSettings`; Boats `BoatFuelTank` | Actual remaining fuel, one burn curve, continuous sea state and hold load. Main passes zero slip; no new speed-based burn curve is justified. |
| UI `HelmOverlayHost`, `HelmBandController`, `HelmBandLayout`, `HelmInstrumentExpansion`, `HelmHudSuppression` | Fixed bottom bands, raised instruments and suppression of duplicate HUD reads already exist. The current band still uses a full-fuel placeholder and a drive-based tach. This draft changes none of their pixels. |
| UI depth/finder/radar/chartplotter hosts, `NavData`, chart preferences and track recorder | Existing instrument glass and user marks/tracks remain authoritative for presentation and saved preferences. The new catalogue only projects authored seam exits/berths; it does not replace the plotter. |
| Lamp/spotlight, anchor controls; `helm-audit.md` switch table | Lamp and anchor verbs exist. There is no general accessory/electrical switch model, engine ignition model, battery charge simulation or bilge-water model. A drawn switch is not proof of simulated state. |
| World `RegionPassage`, `BerthDef` | Source-region trigger positions and existing berth geometry/ids are available. A passage has no stable id field, so its composition caller must supply an explicit stable catalogue key. |

The prior design rulings remain in [helm-audit.md](helm-audit.md),
[diegetic-instruments-and-consoles.md](diegetic-instruments-and-consoles.md) and
[ADR 0030](../adr/0030-per-hull-instrument-ownership.md). This draft does not advance their deferred
ignition, bilge, electrical, horn or autopilot phases.

## Added model and input contract

`WorkingHelmReader` is an explicitly constructed, engine-light Core reader. It consumes the existing
`IHelmInstruments`, the owning hull's `IWorkingHullInstruments`, the existing `IEnvironmentService`,
and an `IHelmWaypoints` catalogue. It creates no GameObject and registers no global service.
`WorkingHelmHullProbe` is Boats' read-only adapter over the real controller, injected tank and hold;
it reads the actual hull id, draught, bow, velocity, drive and fuel. The controller must be active.
The caller binds those inputs to the same hull/region and supplies that region id when composing or
changing regions. Hull/profile/glass identity mismatches suppress readings instead of leaking the
previous hull's numbers. The probe adds no physics, resource integration or saved state.

Every instrument reports `NotFitted`, `Off`, `Unavailable` or `Ready`. The reader starts with all
power inputs off. `SetPower(mask, on)` is an explicit transient input for the later accessory-switch
adapter, not a battery drain model; existing unrelated light/anchor switches remain untouched.
No numeric zero is presented as a healthy reading when its source is absent. Off/unavailable reads
clear prior numbers. Units live in field names; no formatting or strings are created on tick.

## Readings

| Instrument | Rule |
|---|---|
| Depth sounder | Read the existing water column once; signed under-keel clearance is `water - actual draught`. Keep a negative result aground. The armed shallow alarm uses the larger of the existing surface-depth preference and `draught + profile clearance`; equality alarms. Thus it never lowers an existing skipper set-point or changes persisted preference units. |
| Speed/heading | Read actual `BoatKinematics.SpeedOverGround` in m/s and compass heading, 0 north, clockwise, wrapped into `[0,360)`. Ground speed is not claimed as through-water speed. |
| Wind | Call the same `SailDrive.ResolveWind` as sailing: world wind points downwind, apparent wind subtracts actual world hull velocity, signed source angles are positive to starboard, speeds are knots. Calm speed zero makes its angle meaningless. |
| Chart plotter | Own position comes from existing glass. Project region passages at the source trigger and berths at the stern/bow midpoint. Select a stable id; range/bearing use `NavMath`. Only same-region targets are valid. At the destination range is zero and bearing is explicitly absent. Losing a target clears it. Markers do not assert that a tide/access gate is open or a berth is safe for this hull. |
| Engine panel | Fuel is the injected real tank, never a second stored quantity. RPM is explicitly `EstimatedRpm`, a data-calibrated drive indication while turning, because main has no mechanical RPM simulation. Burn uses the existing full-throttle rating, drive magnitude, hold fraction, zero slip, continuous sea state and `FuelSettings`. Hours remaining are fuel/current burn at this same duty, in the burn model's hours; this is not a route forecast or a promise at another throttle. Stopped/unknown burn gives no range, never infinity. |
| Bilge/battery | Main's probe reports unavailable for both. The Core value boundary can read future authoritative litres/charge without integrating, draining, pumping, saving or inventing state. The trawler can have the instrument fitted while its reading remains unavailable. |

## Per-hull data and fit resolution

One `WorkingHelmFitDef` per hull, with stable `helm_fit.*` id and `boat.*` key. New assets carry no
art, prices or hull tuning. Depth and GPS are deliberately forbidden as extra flags: those slots
still resolve through `BoatEquipment` and `HelmFit`. The profile adds motion/wind/engine/auxiliary
readings and authored clearance/RPM indication calibration. `DefaultEquipment()` uses the existing
console and existing instrument ids. For live equipment, pass the existing glass's `Fit` to
`Resolve`; a proposed factory fit must never grant an unowned live upgrade.

| Profile | Declared logic fit |
|---|---|
| Dory | None; no console, no extra readings or power capability. |
| Lobster boat | Existing Novi depth equipment plus motion, wind and engine readings. |
| Stern trawler | All seven requested readings. Its proposed factory fit reuses the existing Novi resolver with finder/GPS/radar/flush-compass ids, plus the working readings. |

These are explicitly composed logic profiles, not installed equipment. The trawler's existing hull
Def has no console; loading this profile does not add one, grant owned instruments or enable a HUD.
The profile's clearances and RPM calibrations are new editable indication defaults, not measured
engine specifications and not physics inputs. Future mounting and its look require the look lane.
Only the append-only identity ledger gains this Def type and its three stable asset identities.

`HelmRegionWaypoints` builds and validates a sorted, unique immutable catalogue outside the tick.
The World adapter projects the existing objects; callers choose which sea passages belong in the
chart, supply stable passage keys and rebuild on region/content changes. It never discovers a scene,
activates travel or treats target-region arrival coordinates as source-region positions.

## Verification and limits

Named EditMode fixtures: `WorkingHelmInstrumentTests` covers known reads for every instrument,
different-draught alarms, inclusive/disarmed alarms, stale and invalid data, region/hull identity,
independent power, deterministic repeats and exactly zero managed bytes across 10,000 warmed ticks.
`WorkingHelmFitTests` covers all three authored fits, existing-equipment precedence, profile validation,
berth/seam projection and unique stable catalogue ordering. Existing depth, sail-wind, fuel and
module-boundary checks remain unchanged. The managed runner executes pure logic without a Unity
editor; Unity-target compilation and offline YAML/GUID/ledger checks are separate proofs. Native
asset loading and GameObject lifecycle tests require CI: no editor slot is granted to this charter.

Local no-editor evidence: 68 managed cases passed (31 reader cases, three actual-YAML fit checks,
two managed catalogue/projection checks, seven existing depth cases, 21 existing fuel cases and
four module-boundary guards). The allocation case measured exactly zero bytes over 10,000 warmed
ticks. Fresh Core/Boats/World compilation and the selected native EditMode fixtures passed against
Unity managed assemblies; existing serialized-field warnings are outside the added files. The
offline ledger check preserved all 1,947 prior identities and added one type/three rows. This is
not an editor run, native asset-import proof, PlayMode result or CI-green claim.

Tick storage consists of value readings, retained references and caller-provided strings/catalogue.
Setup allocates the reader and catalogue once. Fit resolution, component discovery, asset loading,
catalogue rebuilding, string formatting and first-time tank priming belong outside the measured
tick. There is no wall clock, RNG, EventBus traffic, save mutation, scene activation or visual proof.

## What the look lane needs

Consume these Core statuses/values alongside the existing occupied helm and glass. Keep the fixed
band, equipment ownership, raised-card layout, night preferences and HUD suppression rules. Label
the RPM indication as estimated until a real engine model supplies revs; distinguish unavailable
bilge/battery from empty/full and stopped/unknown endurance from an infinite range. Wire accessory
power explicitly when that switch model is chartered. Mount the trawler's proposed fit only after
its hull-specific helm and equipment decision. Choose actual scene sea-passage keys and berth
catalogues at composition, and rebind on region/hull changes. Validate the later integration's live
probe allocation and native lifecycle, then provide its own visual/contact/readability evidence.

# Fuel burn by speed

> **Status, 2026-11-13: LIVE.** The owner ruled on 11-12 that fuel burn rises with speed, with this model. The live meter (`BoatFuelTank`) now reads it; see **Live wiring** at the end. The sections between here and there are the original candidate write-up and are kept as written: where they say the live game keeps its throttle curve, that no caller exists, or that a future integrator must supply the speed, read them as the state before the wiring.

This is isolated logic and new data, based on main `c9c9ef73dee59ee9b6e8c072ea98487992f82c2e`. It does not replace `FuelBurn`, call `BoatFuelTank`, change hull stats, register a service or touch a save. The live game keeps its existing throttle curve. `IFuelBurnBySpeed` is the Core contract; `FuelBurnBySpeed` is an immutable plain class in Boats. Construction may allocate; valid warmed tick calls allocate zero managed bytes.

## Calibration without choosing new hull speeds

Each of the 46 existing hulls has one new `FuelSpeedProfileDef` pointing to that hull and a class-curve Def. Existing hull YAML remains unchanged. `CreateModel` requires a **supplied measured calm/light cruise speed through water**. Main has no measured cruise/top fields, so this proposal neither invents those numbers nor infers them from engine thrust, mass, drag or real-boat specifications. Tests use several arbitrary supplied cruise speeds to prove that calibration holds independently of that choice. A future integrator must measure and supply the speed; the factory does not certify the provenance of its float argument.

The fleet at refreshed main `f814d34d789ab4a12ee7362266dbf5e5e6ae22c0` includes `boat.jet_ski`. The original 45-hull coverage gate correctly rejected the expanded fleet in CI before the arithmetic and imported-reference checks could run. The new separate jetski profile uses the existing candidate planing curve and the hull's unchanged 40-litre capacity and 30-litre/hour full-throttle rate. Both fleet-count assertions now require exactly 46 hulls; the planing count requires exactly 14. Every hull still requires one unique binding, with no skipped hull or relaxed assertion.

The existing fuel design names 0.7 throttle as an example cruise duty. Both new class Defs explicitly carry that reference duty. Read the caller's existing `FuelSettings` and the hull's authored full-throttle rate `R`, with hold/slip/sea inputs zero:

```
B = FuelBurn.LitresPerHour(R, 0,   0, 0, 0, settings)  // existing idle rate
A = FuelBurn.LitresPerHour(R, 0.7, 0, 0, 0, settings)  // existing reference cruise rate
x = abs(engine-driven speed through water) / supplied cruise speed
new hourly burn = B + (A - B) * g(x) / g(1)
```

At the serialized settings on main, `B = 0.08 R` and `A = 0.5308 R`. At zero speed the running engine keeps its old idle cost; at cruise it reproduces the old rate exactly. Engine stopped always returns zero. Negative through-water speed uses magnitude, making astern burn positive. This is a **steady engine-driven operating-point model**, not fuel inferred from GPS speed. Being swept along by current, coasting or sailing cannot supply the engine-running flag. Acceleration, propeller slip, towing and station keeping need an operating-load/throttle policy before integration; speed alone cannot price them correctly. Load and sea surcharges are outside this calm/light candidate, and must not silently disappear in any later live adapter.

The rowed dory has no fuel engine and returns zero even if a caller asks for running burn. The two sloops are calibrated for their authored auxiliaries, and can return zero when under sail. The current live meter accepts only `PropulsionType.Engine`, so these new profiles **do not fix or enable auxiliary metering** for `Sail` hulls.

## Curves and authored classes

Two hydrodynamic classes have one Def each, with all constants serialized:

| Def id | Family | Displacement exponent n | Hump/cruise h | High-speed exponent p | Reference cruise throttle |
|---|---|---:|---:|---:|---:|
| `fuelcurve.displacement` | Displacement | 3 | 0.6 (unused) | 1.5 (unused) | 0.7 |
| `fuelcurve.planing` | Planing | 3 | 0.6 | 1.5 | 0.7 |

Displacement uses `g(x) = x^n`, approximating a cubic power/burn rise above idle. Planing uses the same steep rise below the hump and a flatter power law above it:

```
x <= h: g(x) = x^n
x >  h: g(x) = h^n + (n/p) * h^(n-p) * (x^p - h^p)
```

Value and first derivative meet at the hump. The hourly burn continues rising; it does not become cheaper per hour to go faster. The hump is the transition out of the steep displacement rise, not a local maximum in hourly fuel consumption. These are game design curves, not fitted naval-architecture trial curves. Construction validates positive cruise speed, finite values, `n > p > 1`, `0 < h < 1`, and `0 <= B < A` (or both rates zero for a disabled model). Output saturates at the finite float maximum for extreme speeds/time steps. Runtime NaN/infinity is rejected; nonpositive elapsed time spends zero fuel. There is no wall clock, random draw or mutable simulation state.

The initial **candidate** class assignments are conservative and explicit in the new profiles:

| Class | Hulls |
|---|---|
| Displacement (32) | Rowed and outboard dory; both punts; Cape Islander; base lobster boat plus all 18 lobster variants; side dragger; both stern trawlers; coastal packet; tanker; expedition yacht; both sloops' auxiliaries |
| Planing (14) | Console skiff; three sport skiffs; bowrider; express cruiser; go-fast; motor yacht; convertible and skybridge sportfishers; yacht tender; both Zodiac hulls; jetski |

There are **46 bindings total**. Regional lobster traditions are not a measured hydrodynamic classification. A semi-displacement or planing working-boat fit can get its own curve after measurement/review without editing a hull's authored numbers. No hull is reclassified in the live game by this proposal.

## What switching it on would mean

At the reference cruise and at idle, the same boat spends the same litres as today. Between those points and above cruise, fuel responds to speed through water and the chosen hull class. A displacement boat pays a steeper price for pushing hard. A planing boat passes through a steep rise, then its hourly burn rises more gently after the hump. Easing off buys more distance from the same tank, while taking longer to get home. Tank sizes, maximum speeds and the price per litre do not change here.

For a **comparison scenario** where full speed is cruise speed divided by 0.7, the authored curves give these calm/light rates. These are ratios to `R`, not measurements of the game's top speeds:

| Curve | Half-full speed burn / R | Full speed burn / R | Half/full distance per tank |
|---|---:|---:|---:|
| Displacement | 0.24429 | 1.39429 | 2.854 |
| Planing | 0.29805 | 0.94627 | 1.587 |

`range = capacity / hourly burn * speed * 3600`, in metres with speed m/s. Every one of the 45 fuel-engine/auxiliary profiles must have **strictly** longer range at half-full speed in this scenario. The rowed dory is explicitly tested as the sole zero-capacity/no-fuel case; both distances are unbounded by fuel, and infinity cannot strictly exceed itself. No balance rule is changed. Extremely slow idle-dominated travel need not improve range further, and the range claim is not made for arbitrary unreviewed curve tuning.

Fuel ticks use real seconds (`3600` per hour), preserving the existing fuel clock. No game-calendar conversion enters the model. A later integration must retain fuel availability, hand refuelling, load/sea cost, engine state, measured speed provenance and current behavior when disabled. That is separate reviewed composition work.

## Named proof and limits

- `Reproduction_MatchesAuthoredCruiseBurnForEveryHullDef`: 46 hull cases, three supplied cruise speeds, forward/astern, engine-off and preserved idle; reads the actual hull/curve/profile YAML and serialized fuel settings.
- `BurnIsMonotonicInSpeedForEveryHullDef`: 46 cases, 1000 increasing speed samples each; strict rise for 45 fueled profiles and exact zero for the rowed dory.
- `RangeAtHalfSpeedExceedsRangeAtFullSpeedForEveryHullDef`: 46 cases, strict comparison for all 45 fuel budgets and the explicit rowed-dory invariant.
- `ZeroAllocationsPerTick_AcrossEveryHullAndCurveBranch`: warmed Core-interface dispatch over 20,000 busy ticks, all 46 models, engine-off/idle/astern/both curve branches, with a known allocation as a mandatory counter/recorder positive control.
- Additional cases pin the cubic law, hump continuity/flatter high-speed behavior, repeatable real-hour integration and invalid-input handling.
- `ImportedProfiles_ResolveEveryHullAndFactoryReproducesCruise`: Unity-only import/factory validation of the 46 actual Def references. The source compiles headlessly; executing it and Unity's allocation recorder requires CI. No editor lease is granted for this charter.

Native NUnit execution runs the real pure production sources and the file-based fleet cases. It is not a Unity physics, asset-import, player-feel or graphics test. A managed-assembly compilation checks full Core/Boats source and the conditional editor test code. The model remains unregistered and no runtime caller is added.

## Live wiring (2026-11-13, the owner's 11-12 ruling)

`BoatFuelTank` drains by this model. Each fixed step, with the engine turning:

```
drivenSpeed   = |throttle| * stillWaterTopSpeed(hull)     // FuelSpeedMeter
litresPerHour = curve(drivenSpeed) * loadTerm * seaTerm   // IFuelBurnBySpeed * FuelBurn.DutySurcharge
```

**Which speed goes in.** The speed her throttle is driving her at, not the speed over the ground or through the water at that instant. This is the operating-load policy the candidate asked for before integration. The model is a steady operating point: priced on measured speed, an engine flat out against a wharf, a tow or a grounding would be charged as if idling, and a hull coasting with the throttle shut would be charged as if driving. `stillWaterTopSpeed = EnginePower / (ForwardDrag + bodyDamping)` is the balance `BoatController` integrates, read from its own named constants and from the hull Def as it is now. Thrust is linear in throttle and resistance is linear in speed, so half throttle is half speed. Astern is charged on the absolute throttle, as before.

**Which speed it is calibrated at.** A new field on each profile, `FuelSpeedProfileDef.ReferenceCruiseSpeedMps`: the speed at which the hull burns her old cruise rate. It is authored once, as `0.7 x` her still-water top speed on main `ce70abd495` (before the 11-12 speed table), and it is a fixed number. It is deliberately not derived from `EnginePower`. If it were, the two would cancel: a hull given a bigger engine would burn the same litres per hour flat out and simply go further on a tank, which is the opposite of the ruling. With a fixed calibration, a faster hull asks her curve for a higher speed and burns more. The owner can move the number per hull: higher makes her cheaper at every speed, lower makes her thirstier. The rowed dory is 0 (nothing to calibrate).

**What did not change.** Idle (zero throttle, engine turning) costs exactly what it did; no idle rule was added. At her calibrated cruise speed a hull burns her old cruise rate. A full hold and a rough sea add the same surcharges as before, now from `FuelBurn.DutySurcharge`. Fuel is still metered on the fixed step against real seconds, only while the player is at the helm of an engine hull. Tank sizes, prices, `FullThrottleLitresPerHour` and `GameConfig.Fuel` are untouched. The sloops' auxiliaries are calibrated but still not metered (the live meter accepts only `PropulsionType.Engine`).

**A hull with no calibrated profile keeps the throttle curve.** The tank finds a hull's profile by hull id through `Resources/FuelSpeedProfileLibrary` (append-only: a new hull is a new profile asset plus one entry). No entry, no curve, a zero reference speed, or numbers a model cannot be built from all fall back to `FuelBurn.LitresPerHour`, so a new or test hull still meters and the fixed step never throws over a Def value.

**The helm gauge reads the same meter.** `WorkingHullState` carries the tank's metered rate, so litres per hour and hours remaining at the helm are what the tank is actually charging.

**What it does to a tank, flat out.** At the speed she was calibrated for (today's Defs), a displacement hull flat out burns 1.394 x her old flat-out rate and a planing hull 0.946 x (the comparison table above). So today, before the speed table lands, a planing hull flat out goes about 5.7% further than under the throttle meter; this is the approved curve, not a wiring choice, and it is not retuned here. From about 1.11 x her calibrated speed upward a planing hull flat out covers no more ground per tank than she used to, and every hull the speed table moves (1.25 x to 2.27 x) is past that.

Proof: `FuelMeterBySpeedTests` (EditMode) drives the real `BoatFuelTank` over the shipped Defs.

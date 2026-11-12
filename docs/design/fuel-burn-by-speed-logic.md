# Candidate fuel burn by speed

This is isolated logic and new data, based on main `c9c9ef73dee59ee9b6e8c072ea98487992f82c2e`. It does not replace `FuelBurn`, call `BoatFuelTank`, change hull stats, register a service or touch a save. The live game keeps its existing throttle curve. `IFuelBurnBySpeed` is the Core contract; `FuelBurnBySpeed` is an immutable plain class in Boats. Construction may allocate; valid warmed tick calls allocate zero managed bytes.

## Calibration without choosing new hull speeds

Each of the 45 existing hulls has one new `FuelSpeedProfileDef` pointing to that hull and a class-curve Def. Existing hull YAML remains unchanged. `CreateModel` requires a **supplied measured calm/light cruise speed through water**. Main has no measured cruise/top fields, so this proposal neither invents those numbers nor infers them from engine thrust, mass, drag or real-boat specifications. Tests use several arbitrary supplied cruise speeds to prove that calibration holds independently of that choice. A future integrator must measure and supply the speed; the factory does not certify the provenance of its float argument.

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
| Planing (13) | Console skiff; three sport skiffs; bowrider; express cruiser; go-fast; motor yacht; convertible and skybridge sportfishers; yacht tender; both Zodiac hulls |

There are **45 bindings total**. Regional lobster traditions are not a measured hydrodynamic classification. A semi-displacement or planing working-boat fit can get its own curve after measurement/review without editing a hull's authored numbers. No hull is reclassified in the live game by this proposal.

## What switching it on would mean

At the reference cruise and at idle, the same boat spends the same litres as today. Between those points and above cruise, fuel responds to speed through water and the chosen hull class. A displacement boat pays a steeper price for pushing hard. A planing boat passes through a steep rise, then its hourly burn rises more gently after the hump. Easing off buys more distance from the same tank, while taking longer to get home. Tank sizes, maximum speeds and the price per litre do not change here.

For a **comparison scenario** where full speed is cruise speed divided by 0.7, the authored curves give these calm/light rates. These are ratios to `R`, not measurements of the game's top speeds:

| Curve | Half-full speed burn / R | Full speed burn / R | Half/full distance per tank |
|---|---:|---:|---:|
| Displacement | 0.24429 | 1.39429 | 2.854 |
| Planing | 0.29805 | 0.94627 | 1.587 |

`range = capacity / hourly burn * speed * 3600`, in metres with speed m/s. Every one of the 44 fuel-engine/auxiliary profiles must have **strictly** longer range at half-full speed in this scenario. The rowed dory is explicitly tested as the sole zero-capacity/no-fuel case; both distances are unbounded by fuel, and infinity cannot strictly exceed itself. This is not an exception that weakens a previous test: no existing test or balance rule is changed. Extremely slow idle-dominated travel need not improve range further, and the range claim is not made for arbitrary unreviewed curve tuning.

Fuel ticks use real seconds (`3600` per hour), preserving the existing fuel clock. No game-calendar conversion enters the model. A later integration must retain fuel availability, hand refuelling, load/sea cost, engine state, measured speed provenance and current behavior when disabled. That is separate reviewed composition work.

## Named proof and limits

- `Reproduction_MatchesAuthoredCruiseBurnForEveryHullDef`: 45 hull cases, three supplied cruise speeds, forward/astern, engine-off and preserved idle; reads the actual hull/curve/profile YAML and serialized fuel settings.
- `BurnIsMonotonicInSpeedForEveryHullDef`: 45 cases, 1000 increasing speed samples each; strict rise for 44 fueled profiles and exact zero for the rowed dory.
- `RangeAtHalfSpeedExceedsRangeAtFullSpeedForEveryHullDef`: 45 cases, strict comparison for all 44 fuel budgets and the explicit rowed-dory invariant.
- `ZeroAllocationsPerTick_AcrossEveryHullAndCurveBranch`: warmed Core-interface dispatch over 20,000 busy ticks, all 45 models, engine-off/idle/astern/both curve branches, with a known allocation as a mandatory counter/recorder positive control.
- Additional cases pin the cubic law, hump continuity/flatter high-speed behavior, repeatable real-hour integration and invalid-input handling.
- `ImportedProfiles_ResolveEveryHullAndFactoryReproducesCruise`: Unity-only import/factory validation of the 45 actual Def references. The source compiles headlessly; executing it and Unity's allocation recorder requires CI. No editor lease is granted for this charter.

Native NUnit execution runs the real pure production sources and the file-based fleet cases. It is not a Unity physics, asset-import, player-feel or graphics test. A managed-assembly compilation checks full Core/Boats source and the conditional editor test code. The model remains unregistered and no runtime caller is added.

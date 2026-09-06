# The hull has WEIGHT — the ride strip (water fidelity PR 10)

**Owner playtest 2026-09-05, verbatim:** *"the boats seem to have too much hangtime after a big wave
and bob up and down too fast and jerky as if they have no weight."*

Charter: `HANDOFF-2026-09-05-hull-has-weight.md`. Base: `f2c7105b`.

## What is in here

| file | what it is |
|---|---|
| `strip-before-f2c7105b.md` | the ride measured on `f2c7105b`, unmodified |
| `strip-after.md` | the same measurements after the change |
| `ridestrip.cs.txt` / `ridestrip.csproj.txt` | the harness that produced both, verbatim |

The harness is a `net8.0` console that **`<Compile Include>`s the repository's own source files**
(`WaveMath`, `WaveFieldAnimator`, `ShoreFadeMath`, `StormRockMath`, `HullHeaveResponseMath`) and
references `UnityEngine.CoreModule.dll` — nothing in it is a transcription of the game's maths, and
every constant is parsed out of the shipped `GameConfig.asset`, `Water.mat` and `Data/Boats/*.asset`
rather than typed. See memory `run-compiled-unity-assemblies-headless`. To re-run:

```bash
dotnet build -c Release -p:HHROOT=<repo> && HHROOT=<repo> ./bin/Release/net8.0/ridestrip.exe after
```

The `before` arm is `BoatWaveMotion.Tick` as `f2c7105b` ran it (one `WaveMath.Sample` at the hull's
origin, through `ShoreFadeMath.DisplacedHeight`, through `StepHeaveWeight` at the storm engage — which
is exactly 0, an exact passthrough, below `StormStartSeaState01` 0.4). The `after` arm is the new path
line for line; only the MonoBehaviour plumbing, which the harness has no editor for, is restated.

## 1. The transfer function — the measurement that names the defect

Steady-state ride amplitude ÷ wave amplitude, one pure train, swept over wavelength.

**BEFORE**

| hull | L m | λ2 | λ4 | λ8 | λ16 | λ32 | λ64 |
|---|---|---|---|---|---|---|---|
| Dory | 4.5 | 1.000 | 1.000 | 1.000 | 1.000 | 1.000 | 1.000 |
| CapeIslander | 12.9 | 1.000 | 1.000 | 1.000 | 1.000 | 1.000 | 1.000 |
| Tanker | 110.0 | 1.000 | 1.000 | 1.000 | 1.000 | 1.000 | 1.000 |

**1.000 at every wavelength, on every hull.** A 110 m ship followed a 2 m ripple at full amplitude,
in lockstep, with no lag — two orders of magnitude of hull, one response. That is a cork.

**AFTER**

| hull | L m | λ2 | λ4 | λ8 | λ16 | λ32 | λ64 |
|---|---|---|---|---|---|---|---|
| Dory | 4.5 | 0.314 | 0.234 | 0.792 | 1.033 | 1.048 | 1.031 |
| CapeIslander | 12.9 | 0.268 | 0.081 | 0.236 | 0.303 | 0.951 | 1.080 |
| Tanker | 110.0 | 0.269 | 0.018 | 0.018 | 0.018 | 0.097 | 0.168 |

Each hull now has a corner at her own length: the dory is up at 1.0 by λ12, the cape by λ32, and the
tanker is still at 0.17 by λ64 because 64 m is *shorter than she is*. The residual at λ2 is the
sampling alias at exactly one wavelength per sample spacing (2 m) — the honest limit of any point
sampling, and the reason the settings cap the SPACING rather than the count.

## 2. The strip — 30 s of the shipped four-train field, hull lying to

`held-at-g %` is the fraction of frames on which the ride's realized downward acceleration is pinned
**at the free-fall cap** — i.e. the hull is unweighted and being held up by gravity rather than by
water. That is the hangtime, measured as its cause. `lag` and `excursion` are against the water she
is actually chasing.

**Sea state 0.30 (wind 2.2 m/s)**

| hull | ride/surface | reversals/30 s | peak rate m/s | lag s | held-at-g % |
|---|---|---|---|---|---|
| Dory — before | 1.000 | 37 | 1.124 | 0.000 | 0.00 |
| Dory — after | 0.911 | 25 | **0.712** | 0.033 | 0.00 |
| Cape — before | 1.000 | 37 | 1.124 | 0.000 | 0.00 |
| Cape — after | 0.710 | 25 | **0.217** | 0.167 | 0.00 |
| Tanker — before | 1.000 | 37 | 1.124 | 0.000 | 0.00 |
| Tanker — after | 0.689 | 34 | **0.015** | 0.000 | 0.00 |

**Sea state 0.75 (wind 8.8 m/s)**

| hull | ride/surface | reversals/30 s | peak rate m/s | lag s | held-at-g % | excursion m |
|---|---|---|---|---|---|---|
| Dory — before | 1.017 | 22 | 3.231 | 0.000 | **0.22** | 0.065 |
| Dory — after | 1.029 | 24 | 3.138 | 0.017 | **0.00** | 0.292 |
| Cape — before | 1.032 | 22 | 3.250 | 0.033 | **0.06** | 0.160 |
| Cape — after | 0.753 | 17 | **1.124** | 0.217 | **0.00** | 0.191 |
| Tanker — before | 1.032 | 22 | 3.250 | 0.033 | **0.06** | 0.160 |
| Tanker — after | 0.667 | 20 | **0.104** | 0.000 | **0.00** | 0.000 |

Three things to read off it:

1. **"bobs up and down too fast"** is the peak rate, and it falls by 3× on the dory, 5× on the cape
   and 75× on the tanker in a light sea; by 3× on the cape at a blow.
2. **"as if they have no weight"** is the before column where *the cape and the tanker are the same
   number*. They are not the same number any more.
3. **"too much hangtime after a big wave"** is `held-at-g`, and it goes to **0.00 % everywhere**. The
   hangtime was never gravity misbehaving: the free-fall cap (ADR 0018 B2.5, the owner's own
   "it must obey gravity") fires when the surface under the hull falls faster than g, and only a
   POINT sample of a crest-sharpened field is ever that steep. Averaged over a hull's waterline the
   water never outruns gravity, so she never unweights and never hangs.

The cape's residual 0.19 m excursion is not a hold — it is her 0.22 s phase lag, which is the visible
weight the charter asked for. The dory's 0.29 m is her resonance: at 4.5 m she is short enough to be
thrown by a steep crest, and being thrown is what a dory does.

## 3. The fleet's heave character

Every number from the hull's own asset and the shipped `GameConfig.HullWeight` block.
`T(MassKg)` is what the charter's literal instruction — read `BoatHullDef.MassKg` as her displacement
— would have produced.

| hull | L m | draught m | displacement t | MassKg t | N | spacing m | **T s** | T(MassKg) s | ζ |
|---|---|---|---|---|---|---|---|---|---|
| Dory | 4.5 | 0.30 | 1.3 | 0.4 | 3 | 1.50 | **1.25** | 0.70 | 0.35 |
| FishingSkiff | 4.0 | 0.35 | 1.2 | 0.5 | 3 | 1.33 | **1.35** | 0.84 | 0.35 |
| Punt | 5.2 | 0.50 | 2.8 | 0.7 | 3 | 1.73 | **1.61** | 0.80 | 0.35 |
| ConsoleSkiff | 7.0 | 0.55 | 5.6 | 1.2 | 5 | 1.40 | **1.69** | 0.78 | 0.50 |
| LobsterInshore | 8.6 | 1.15 | 17.7 | 2.5 | 5 | 1.72 | **2.45** | 0.91 | 0.55 |
| LobsterBoat | 12.0 | 1.30 | 39.0 | 6.8 | 7 | 1.71 | **2.60** | 1.09 | 0.65 |
| CapeIslander | 12.9 | 1.40 | 48.6 | 6.0 | 7 | 1.84 | **2.70** | 0.95 | 0.65 |
| SportFisher | 16.2 | 1.75 | 95.8 | 15.0 | 9 | 1.80 | **3.02** | 1.19 | 0.68 |
| SideDragger | 25.0 | 2.90 | 378.1 | 90.0 | 13 | 1.92 | **3.89** | 1.90 | 0.75 |
| SternTrawler | 38.0 | 4.20 | 1265.0 | 316.0 | 19 | 2.00 | **4.68** | 2.34 | 0.80 |
| CoastalPacket | 60.0 | 5.00 | 3754.6 | 1244.0 | 31 | 1.94 | **5.10** | 2.94 | 0.85 |
| Tanker | 110.0 | 6.50 | 16405.4 | 7668.0 | 55 | 2.00 | **5.82** | 3.98 | 0.90 |

The charter's own targets were *"a lobster boat lands near 2–3 s, a dory nearer 1 s"*. The
displacement route hits them (2.60 / 1.25); `MassKg` misses them low **and** flattens the fleet — the
dory-to-cape spread is 2.16× on displacement and 1.36× on `MassKg`. `MassKg` is the 2D physics body's
mass for the helm model, not what she displaces (the cape carries 6 000 kg against a ~49 t block
estimate), which is why it cannot be the mass in `T = 2π√(m/ρgA_wp)`.

## 4. Cost (rule 7)

One `WaveMath.Sample` over the shipped field's 8 live trains measures **460 ns** on this box.
Nine Mile Creek's 30 moored hulls plus the player take ~217 samples/frame = **0.100 ms** of a 16.7 ms
frame; they were 31 samples = 0.014 ms as point samples. The longest hull in the game asks for 55
samples = 0.025 ms on her own.

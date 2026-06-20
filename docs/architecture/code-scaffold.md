# Code Scaffold — What's Built & How to Extend It

> A map of the C# that already exists in `Assets/_Project/Code/` and `Assets/Tests/`. This is the
> running skeleton of the **M0 greybox** fishing→sell loop. Pair with `tech-architecture.md`
> (the design) and `project-structure.md` (the layout). To stand it up in Unity, see `../../SETUP-UNITY.md`.

## Assemblies (modules)

| Assembly | Folder | What's in it | Key files |
|----------|--------|--------------|-----------|
| `HiddenHarbours.Core` | `Code/Core` | Shared contracts & types — no deps | `EventBus`, `GameServices`, `IGameClock`, `IEnvironmentService`, `IHold`, `IWallet`, `EnvironmentSample`, `CatchItem`, `GameConfig`, enums |
| `HiddenHarbours.Environment` | `Code/Environment` | Deterministic sea (P1) | `GameClock`, `TideModel`, `WeatherModel`, `EnvironmentService` |
| `HiddenHarbours.Boats` | `Code/Boats` | The dory + hold | `BoatHullDef`, `BoatController`, `ShipHold`, `DevBoatInput` |
| `HiddenHarbours.Fishing` | `Code/Fishing` | Catch | `FishSpeciesDef`, `Gear`, `CatchResolver`, `FishingController`, `DevFishingInput` |
| `HiddenHarbours.Economy` | `Code/Economy` | Market | `MarketMath`, `Market`, `FishBuyer` |
| `HiddenHarbours.Player` | `Code/Player` | The player | `PlayerWallet` |
| `HiddenHarbours.App` | `Code/App` | Composition root | `GameRoot` |
| `HiddenHarbours.Tests.EditMode` | `Tests/EditMode` | Tests | tide, weather, catch, market |

Dependency rule holds: every feature module references **only Core**; `App` references everything
(it wires them). Nothing references another feature module's internals. (`project-structure.md` §5)

## The catch → sell loop (data flow)

```
        ┌─────────────┐   reads    ┌──────────────────────┐
        │  GameClock  │──────────▶ │  EnvironmentService  │  (tide, wind, sea state, fog)
        └─────────────┘            └──────────┬───────────┘
                                              │ EnvironmentSample
                                              ▼
   DevFishingInput ▶ FishingController ▶ CatchResolver ▶ CatchItem ▶ ShipHold (IHold)
                                                                          │
                                                              FishBuyer.SellAll(hold, wallet)
                                                                          │ prices via Market/MarketMath
                                                                          ▼
                                                            PlayerWallet (IWallet)  +  MoneyChanged event
```

Everything talks through **Core interfaces + the EventBus**, so e.g. Fishing never references the
Boats class — it writes to an `IHold`. Tide and weather are **recomputed from (seed, time)**, never
saved (`tech-architecture.md` §6).

## What is DATA (you create these assets, no code)

Create via **Assets > Create > Hidden Harbours > …** and save under `Assets/_Project/Data/`:
- **Game Config** → `Data/Config` (clock/tide/weather tunables).
- **Boat Hull** → `Data/Boats` (the Dory ships by default).
- **Fish Species** → `Data/Fish` (one asset per fish — this is how the 100 fish get made).

## Wire the loop in a scene (greybox)

1. **Bootstrap** scene: `GameRoot` + `GameClock` + `EnvironmentService` (+ optional `PlayerWallet`),
   wired per `../../SETUP-UNITY.md` §5.
2. **The dory** (a GameObject): `Rigidbody2D` + `BoatController` (assign the Boat Hull) +
   `ShipHold` (assign the same Boat Hull) + `DevBoatInput` + `FishingController` (assign the dory's
   `GameObject` as the *Hold Provider*, and drop a few **Fish Species** assets into *Region Fish*) +
   `DevFishingInput`.
3. **The wharf** (a GameObject): `Market` (assign Game Config) + `FishBuyer` (assign the Market).
4. To sell, call `FishBuyer.SellAll(theShipHold, thePlayerWallet)` — hook it to a key or a trigger
   near the wharf for now; a real NPC/UI interaction lands in **VS-22**.

Controls in play mode: **W/Up** throttle, **A/D** steer, **Space** cast. Watch the Console for
catches and sales.

## How to add a fish (the common content task)

Create a new **Fish Species** asset, set its `Id` (`fish.snake_case`, append-only), category,
rarity, the **gating** (regions, tide window, time window, seasons, gear), size range, base value,
elasticity, and spawn weight. Add it to a region's *Region Fish* list. The validation/tests will
catch missing ids. No code changes needed (ADR 0003).

## What is NOT built yet (next backlog items)

- **Scenes** (Bootstrap, CoddleCove) and **sprites** — must be authored in-editor (`world-content`, `art-pipeline`).
- **HUD** surfacing tide/wind/time (`ui-ux`, VS-13) and the **real touch input** (replaces the Dev*Input scripts).
- **Additive region loading** (VS-02), **save/load**, **NPCs**, and the **fuller market sim** (NPC fleet, contracts — M2).
- A real **seabed/depth map** (grounding currently uses a placeholder local depth).

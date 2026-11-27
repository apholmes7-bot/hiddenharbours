# Shore picking at low water

This slice supplies the rules and content for picking blue mussels and American oysters by hand on St Peters. It adds no scene interaction or presentation. A later interaction adapter can call the rule without copying the tide, stock or licence decision.

Hand-picking is the slow way to gather shellfish: one hand action takes one shellfish, followed by carrying and stowing it by hand. Faster gathering, industrial processes and commercial farming elsewhere are later work, with no dates promised. This slice does not add those methods or establish an animation duration.

`PickingBedDef` describes one bed: stable id, region, position in region coordinates, species, capacity, regrowth days, exposure height and reach. `PickingBedSession` is a plain class. Its caller supplies the Core environment service for the active region and the configured seconds per game day. The caller must keep one instance per bed for the whole world session, shared by all two to four players, including region exits, and discard it when starting or loading a different world. It must never construct one bed session per player or client: constructing another instance resets that bed to full.

The world authority dispatches attempts in its accepted order on the game thread, using its game time. This follows the existing hands and EventBus contracts; this class is not a networking or worker-thread API. Both `Check` and `TryPick` take the requesting player's `ILicenseService`; `TryPick` also takes that player's `ICatchHands`. No global player wallet or hands are consulted. The licence catalog is shared content, while licence ownership and hand delivery remain per player. Transport, authenticated player-to-service binding and per-client presentation of the existing release/voice signals belong to a later interaction adapter; they are not installed here.

## Taking one shellfish

`Check` reads the water level at the requested game time through `IEnvironmentService.WaterLevelAt`. Water must be strictly below the bed's exposure height. The player must be in that region, within the authored reach, with some stock left. The result includes a plain reason. Missing tide or licence services fail closed. A time before the last kept pick is refused; forecasts do not move the session clock or change its state.

An unlicensed player can still make the picking attempt. The preview distinguishes `CanPick` from `CanKeep`; `TryPick` calls the existing `UnlicensedCatch.ReleaseAndSay` rule. It produces the same release signal and species-naming line as the clam dig, with no catch record or hand delivery. The shellfish goes straight back into this bed, so it causes no removal. This differs from a dug clam hole being spent: the bed tracks animals taken, rather than holes disturbed.

A licensed pick offers exactly one fresh `CatchItem` to the requesting player's `ICatchHands`. If those hands cannot take it, stock is unchanged and another player can take it. The hands publish `CatchLanded`; the picking rule never publishes `FishCaught` or transfers anything into a container. Putting the shellfish into a pail remains a separate hand action. A callback during tide checks, licence checks or delivery cannot start a second pick on the same session. A preview reserves nothing: if two players see the last shellfish, the first successful attempt gets it and the second finds the bed empty. A refused hand delivery or unlicensed release leaves it for the next picker.

## Depletion and regrowth

Beds begin at their authored capacity. Every kept shellfish subtracts one until its own removal time plus `RegrowDays * secondsPerDay`. Regrowth is derived on demand, including after a sleep or large clock jump. Midnight and the next flood do not reset a bed. There is no random stock roll: the capacity is guaranteed, and all players' removals reduce the same count. A regrown shellfish is available to any picker, not reserved for the player who took it before. Catch weights use the existing stable hash and weight resolver, keyed by world seed, bed identity, action time and the number currently removed. Reading more often consumes no randomness and changes no outcome.

Only outstanding removal timestamps need to remain in memory. Successful picks prune completed regrowth, so retained history never exceeds the bed's capacity. `SnapshotAt` exposes a detached, read-only list of outstanding removals, bed identity, world seed, observation time, last pick time and remaining stock. It is a session inspection type, with no save integration.

## Initial content

| Bed | Existing location | Capacity | Regrowth | Uncovered below |
| --- | --- | ---: | ---: | ---: |
| `pickingbed.st_peters_mussels` | St Peters bar-head beach, north flat toe: (-108, 3.5) | 24 | 2 game days | 0.11 m |
| `pickingbed.st_peters_oysters` | Same beach, low south flat toe: (-108, -5) | 12 | 4 game days | -0.44 m |

The coordinates come from `StPetersBarHead` flat transects 1 and 3. The thresholds match the existing species and beach-activity data. Both use a 1.25 m reach and the existing `license.clam` coverage. Capacity and regrowth are initial balance values authored only on these new beds. No terrain height, art, existing configuration or species value changes. The content tests pin this provenance; they do not establish visual placement or walkability in the rendered scene.

## Saving later

The pinned main `f808af6ae5d5a0041f3d2147d74b1ddef7dce609` uses save version 26, unchanged by this slice. Its store-purchase, special-order and shopping-bag rows do not persist picking-bed depletion. Restarting or loading resets these sessions, even when already-kept catches have been saved by the existing hands or containers; reload persistence is deliberately absent from this slice. A future save step needs an approved schema migration for shared per-bed outstanding removal timestamps and the last accepted pick time, keyed by the stable bed id once per world, not per player. World seed and game time already belong to the world save. Restore must validate ids and finite ordered times against loaded game time, reject duplicate or over-capacity rows, and define how changed bed tuning affects old removals. It should recompute tide and remaining stock, rather than save a water level or redundant count.

## Verification

`PickingBedTests` covers the real tide model through Core over seven days for each shipped bed, strict exposure and inclusive reach boundaries, exhaustion, separate regrowth deadlines, sleep jumps, day-length conversion, deterministic replay, rejected hand delivery, detached snapshots, bounded history and invalid inputs. It also checks two pickers previewing the last shellfish in both attempt orders, two to four players sharing depletion and regrowth, independent wallets despite a conflicting global wallet, refusal leaving the last shellfish to another player, and reentrant or throwing tide, licence and hand callbacks. Existing `UnlicensedCatchReleaseTests` exercises both species with and without their shipped licence, including release events and subsequent licence acquisition. Existing `ShellfishSpeciesContentTests` checks both bed assets, their references, ids and location provenance. The frozen identity ledger is unchanged; the new type and two assets have individual addition registrations.

No editor or tests were run locally for this slice. Compilation, EditMode execution, content validation and identity discovery are delegated to the normal CI run. Live interaction, rendered placement and balance feel remain unverified.

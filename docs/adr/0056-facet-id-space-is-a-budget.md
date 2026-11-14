# ADR 0056 — The facet-id space is a budget: lazy fore blocks and a reserved player figure

- **Status:** Accepted (2026-11-12). The owner ruled on PR 877's design note, verbatim: **"Yes, fix it"**. Implemented in the PR that adds this ADR.
- **Date:** 2026-11-12
- **Decision owner:** owner; technical steward lead-architect. Review: art-pipeline (the facet pass), gameplay-systems (deck riders), qa-test.
- **Serves:** P3 Living Working Coast: a harbour's boats, trucks and people all draw correctly at once, and the player is never drawn as a stale sprite beside them. Also rule 6 (the reserve is a `GameConfig` tunable) and rule 7 (no new render target, no per-frame cost).
- **Related:** ADR 0022 (the hull id in the screen texture), ADR 0044 §7.3 and §8.3 (figure ids ashore). Supersedes PR 877, whose note is carried forward and re-measured as [`docs/architecture/facet-id-budget.md`](../architecture/facet-id-budget.md). Amends ADR 0044 §8.3's "no reserve" for the player's figure only.

## Context

The owner asked: *"why do we see old character sprites sometimes? when loading st peters, or in the coop sometimes i think i see old character sprites"*.

The facet pass marks every pixel it draws with an id carried in the alpha of an 8-bit target: ids 1 to 255, 0 meaning none. Three kinds of taker share that one space:

- **a hull** (every mesh boat and mesh truck) takes one id;
- **its fore block**, twelve contiguous ids (`DeckOccupantSlots`, mirrored by the shader literal `HH_DECK_OCCUPANT_SLOTS`) that hide the people standing on its deck behind its cabin;
- **a figure ashore** (the player, and the villagers since ADR 0044 §8) takes one id.

Until this decision every hull took its id **and** its block when it was enabled, thirteen ids, whether or not anyone stood on its deck. The space ran out at the nineteenth hull. At exhaustion hulls share id 255; a figure never shares it and is **refused**. The player's presenter (`DeckRiderMeshPresenter`) asks for her figure id only at an arrival and latches a refusal until her next one, so a refusal at load showed her **old sprite** until she next boarded and left a boat. That is the owner's symptom.

PR 877 counted the takers on 2026-09-23 and recommended §4.1, lazy fore blocks. The fleet has grown since (NPC pot and trawl boats, the cast ashore), so this ADR re-measures on main. See *The measurement* below.

**A correction to PR 877 §2.1.** That note found regions are never unloaded. They are not, but `RegionTravelCoordinator.OnActiveSceneChanged` **deactivates the region left behind** (`SetSceneRootsActive(previous, false)`), so its hulls run `OnDisable` and give their ids back at the scene switch. The peak is therefore **during the additive load**, while both regions hold ids, not after it.

## Decision

1. **A hull holds one id; its fore block is held only while somebody stands on its deck.**
   - `OnEnable` takes the hull's single id, nothing else.
   - The **first** deck-slot claim (`DeckOccupants.Claim`, through `ClaimSlot`) takes the block; every later claim shares it.
   - The **last** release gives it back; `OnDisable` gives it back as before.
   - A claim made while the hull is disabled is honoured when it is enabled again.
   - **A hull refused a block retries quietly at its next claim**, and only when `IsoFacetHullRegistry.CanSpareForeBlock` says one is free, so a full pool warns once per refusal, never once per frame. This closes PR 877 §2.2's "never asks again".
2. **The player's figure has a reserve the fleet can never take.**
   - The top `R` ids below the overflow id 255 are reserved, where `R` is `GameConfig.PlayerFigureIdReserve` (default **2**: the player and one co-op friend; inspector range 0 to 8). With `R = 2` the reserve is ids 253 and 254, and hulls, blocks and the cast are handed ids up to 252.
   - The player's figure (`IsoCharacterFigureRenderer.EnterAshore`, through `IsoFacetHullRegistry.RegisterPlayerFigure`) takes from the reserve first and falls back to the general pool only when the reserve is spent. A freed reserved id goes back to the reserve, never to the fleet.
   - `R` is read once, when the pool is first built (`GameServices.PlayerFigureIdReserve`, clamped; the shipped default when no config is wired). A change applies from the next play session.
3. **The cast keeps the general pool and the loud refusal.** `CharacterFigurePresenter` marks its figures (`IsCastFigure`), which take `RegisterFigure` as before: refused at exhaustion with one warning, drawn as their sprite. Their policy is still ADR 0044 §8.3's.
4. **Exhaustion after this change.** **(c) is not needed.** After lazy blocks the fresh count's worst case in play wants 123 ids of 252 and the in-frame peak of a load wants 112; on both routes no hull shares 255, no boarded hull is without its block, and no figure, the player's or the cast's, is refused. The note's next-best option, §4.2 (ids held only by hulls in or near the view), stays the documented next step for a harbour whose boarded hulls alone spend the space, with the no-GPU budget guard of the note's §5 item 4 as the warning before it does. If the space is ever spent again, behaviour is as before for hulls (shared 255) and the cast (refused, loud), and the player's figure is still served from the reserve.
5. **Nothing outside Art's registry and renderer changes behaviour.** No shader, render target, scene, art asset or save changes. Every shader reader already treats a fore id of 0 as "no block": the overlay (`_HullIdFore <= 0`), the lamp shadow (`_HullIds.y <= 0`), the sun shadow (`_HullIds.y > 0`) and the deck-occluded sprite (`_HHDeckOccluderId > 0`). The facet shader's occupant band is gated on `_DeckOccupantCount`, which counts only while a block is held. Core gains one tunable (`GameConfig.PlayerFigureIdReserve`) and one accessor (`GameServices.PlayerFigureIdReserve`), both additive. `DeckRiderMeshPresenter` and `EnterAshore`'s signature are unchanged.

## The measurement

Measured on main `00b030cf` and on this branch by `FacetIdBudgetPlayTests`, which plays the owner's two ways to St Peters, each in one PlayMode domain from a fresh pool, through the game's own `RegionSceneLoader.Travel`:

- **Route A, St Peters first** (a new game): a cold St Peters, Nine Mile Creek, St Peters, Rice Point, South Water, St Peters.
- **Route B, a save that loaded Nine Mile Creek first**: a cold Nine Mile Creek, St Peters, East Water (the new game's sea), St Peters.

At each stage it takes a census of every live hull, claimed deck slot, figure and the pool. It samples her presenter every frame, and it catches every figure refusal where the registry warns, so a refusal that lasts less than a frame is still counted.

| Stage | Takers: hulls / boarded / figures | Ids wanted: eager → lazy | Main `00b030cf`: held · hulls on 255 · boarded with no block · her figure | This branch: held · on 255 · boarded with no block · her figure |
|---|---|---|---|---|
| A. St Peters, cold | 4 / 0 / 7 | 59 → 11 | 59 · 0 · 0 · id 40 | 11 · 0 · 0 · id 253 |
| A. Nine Mile Creek from St Peters | 23 / 7 / 3 | 302 → 110 (worst in play 303 → 123) | 212, space spent · **7** · **3** · id 40 | 110 · 0 · 0 · id 253 |
| A. St Peters after NMC | 4 / 0 / 7 | 59 → 11 | 59 · 0 · 0 · id 40 | 11 · 0 · 0 · id 253 |
| A. Rice Point | 2 / 0 / 1 | 27 → 3 | 27 · 0 · 0 · id 40 | 3 · 0 · 0 · id 253 |
| A. South Water | 2 / 0 / 1 | 27 → 3 | 27 · 0 · 0 · id 40 | 3 · 0 · 0 · id 253 |
| B. Nine Mile Creek, cold | 22 / 7 / 2 | 288 → 108 (worst in play 289 → 121) | 252, space spent · 0 · **2** · none yet | 108 · 0 · 0 · none yet |
| B. St Peters loading while NMC still holds its ids (the peak) | 25 / 7 / 2, and her ask | 328 → 112 | space spent · **her figure REFUSED** | high-water 112 · her figure takes reserved id 253 |
| B. St Peters after a cold NMC | 4 / 0 / 6 | 58 → 10 | 22 · **1** · 0 (St Peters' 3 vehicles hold no block) · id 131 | 10 · 0 · 0 · id 253 |
| B. East Water | 1 / 0 / 1 | 14 → 2 | 14 · 0 · 0 · id 131 | 2 · 0 · 0 · id 253 |
| B. St Peters after East Water | 4 / 0 / 7 | 59 → 11 | 59 · 0 · 0 · id 131 | 11 · 0 · 0 · id 253 |

**Refusals over both routes.** Main: her figure refused once (route B, while St Peters loaded under a cold NMC: 25 hulls, the pool's next id 256, no free single). This branch: none. The cast: 0 refused on either build (0 of 6 at every St Peters stage, 0 of 2 at NMC).

**The peak is inside one frame.** Route B's peak, while St Peters' hulls enable and NMC's have not yet been deactivated, does not survive to any sampled frame; it is read from the registry's own warning on main (25 hulls at the refusal) and from the pool's high-water mark on this branch (next id 109 at the cold NMC, 112 after St Peters' three vehicles took singles beside it). The busiest *sampled* frame of every stage equals its census.

**Worst case in play.** Nine Mile Creek reached from St Peters with the player aboard her own hull wants **123** ids lazily, against **252** general ids; her figure's id comes from the reserve on top. On main the same stage wants 303 eager ids of 255.

Ids **wanted** counts the space a stage would need with no sharing: eager is 13 per hull plus figures; lazy is 1 per hull, 12 per boarded hull, plus figures. The worst case in play adds the player aboard her own hull (lazy: one more block if she is not already aboard) and her figure.

## Consequences

- **The player's figure is never refused for want of an id**, whatever the harbour holds, while the reserve is above 0. That is the owner's symptom, and `FacetIdBudgetPlayTests` asserts it on his route.
- **Headroom now depends on deck traffic, not on hull count.** A moored boat with a skipper aboard still costs 13; an empty review hull or a parked truck costs 1. A future harbour of twenty crewed boats would spend 260 again. The [budget note](../architecture/facet-id-budget.md) §4.2 (ids only in view) is the next step if one does; §4.4 (a 16-bit id target) is a shader and render-target change with a per-frame mobile cost and stays rejected for now.
- **Cost.** A claim or release is an O(1) stack operation on a path that already ran per claim; nothing new runs per frame and nothing allocates per frame. The reserve spends two ids the fleet could otherwise use.
- **Tests.** `FacetIdLazyForeBlockTests` (the block's books balance over hundreds of board/leave cycles, a disabled hull's claim is honoured, a refused hull retries quietly) and `FacetIdHerFigureTests` (her figure gets an id at a full harbour) compile on main and fail there by behaviour. `IsoFacetFigureIdTests` holds the reserve at the pool and registry level, `IsoCharacterFigureRendererTests` and `CharacterFigurePresenterTests` hold it at the figure, and `FacetIdBudgetPlayTests` is the census and the plate.

## Alternatives considered

PR 877's note weighed them; the [budget note](../architecture/facet-id-budget.md) §4 keeps that analysis with fresh numbers.

- **Ids only for hulls in or near the view (§4.2).** More moving parts (a visibility pass per frame, hysteresis, re-claims against a block that changed hands, many cameras), and unneeded while lazy blocks leave the headroom measured above. Kept as the next step.
- **Fewer slots per block (§4.3).** Breaks the measured ten-on-deck stern beat and changes a shader literal. Rejected.
- **A 16-bit id channel (§4.4).** A second render target, every decode rewritten, about 8 MB at 1080p and tile memory on mobile, paid every frame to seat hulls nobody stands on. Rejected for now (rule 7).
- **No reserve; lazy blocks alone.** Leaves the player's figure competing with the cast and the fleet for the last ids. A crowded co-op harbour could still show her sprite. The owner's question named exactly that case.

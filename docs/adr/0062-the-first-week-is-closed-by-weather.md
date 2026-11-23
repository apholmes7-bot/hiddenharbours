# ADR 0062 — The first week is closed by weather: St Peters alone until the island gate

- **Status:** Proposed (owner rulings 2026-11-23; the canon is amended in the same PR — the owner accepts it by merging)
- **Owner:** lead-architect
- **Touches:** canon `vision-and-pillars.md` §5.3/§5.4/§5.8; the campaign barrier (`World/Campaign/CampaignBarrierDef`,
  `CampaignBarrierDirector`, Core `LocalWeather`); the saved progress flags; the onboarding steps; a later demo build.
  No code changes in this ADR's PR.

## Context

The canon sent the player across the low-tide bar to Nine Mile Creek in his first days, to buy the cod licence and a
rod, and to buy and repair the damaged dory at the town's yard (rulings 2026-07-25 and 2026-08-09). On 2026-11-23 the
owner ruled that the first week is lived on St Peters alone, that this week is also the game's demo, and that the
other regions are closed "with wind/waves" until the player has progressed far enough on the island. The broken dory
is sold on the island by Basil Samson and mended there by hand.

Main already has one tool for this: the campaign barrier (`docs/design/intro-storm.md`). A barrier is one data file per
edge; it turns a boat back with wind, rain and a rising sea, shades only what the player sees and hears through
`LocalWeather`, and is lifted by one saved flag. It pushes boats only; it does nothing to a walker on the bar.

## Decision

1. **The island is closed by weather only, never by a drawn wall or a message box** (consistent with the 2026-11-12
   ruling on the intro storm). Each St Peters edge that meets another region gets a campaign barrier; pushing on is turned back gently — force only, no damage, no capsize, nothing written to position or speed.
2. **The bar on foot is closed by surf.** Strong wind raises surf over the bar crest that turns a walker back. This is
   new behaviour for the barrier director (it pushes boats today). When the gate opens and the surf clears, he walks
   across.
3. **One gate lifts all of it.** Its facts: the dory mended (the saved repaired list), the cod licence held (the saved
   licences list), a fish landed from the dory, shellfish sold, and day 7 or later (the saved clock). When all hold,
   Ginny's next talk gives her word; finishing it sets one saved progress flag, and that flag lifts the barriers and
   the surf. The next morning the weather has turned.
4. **Determinism (rule 5).** Whether a barrier or the surf stands is a pure function of **world seed + game time +
   saved progress flags** and where the player is. The barrier and the surf never read hidden randomness, never change
   the world's wind, tide or sea, and are never saved; the HUD, gameplay and the save keep seeing the simulated weather.
   "The weather has turned" is a look on top of the sim, not a change to it.
5. **No new save version.** The new facts are keys in the existing saved flag list; everything else is already saved.
6. **The demo** ends at Ginny's word with a closing beat; he can keep fishing from the dory after. A demo build holds
   the barriers up for good. The boat trailer stays locked until after he can reach Nine Mile Creek.

## Consequences

- The canon's opening arc changes (St Peters week → gate → Nine Mile Creek → Coddle Cove). Design docs and backlog rows
  that still send the first dory to Nine Mile Creek carry a dated pointer to the canon.
- The NMC yard's dory offer and repair-by-payment on main become the wrong route for the first dory; moving the offer
  to an island seller and replacing payment with hand jobs is backlog work, not this PR.
- The barrier director gains a walker case; it must stay as cheap as today (no per-frame allocations) and must stand
  down during the arrival opening for the new walls too.
- Fishing already lets an unlicensed fish bite and slip back; the ruling adds a spoken line and no Notebook entry.

## Open (not ruled)

- Where on the island the cod licence is sold (proposal: Rose's post office).
- The exact repair materials and jobs, and all prices.
- The gate flag's name and the two new fact flags' names (set by the slice that builds them).
- Whether the north and south edges, which meet no region and today hold a boat at the edge, also get a storm band
  (proposed, so the island is closed by weather everywhere rather than by an unseen stop).

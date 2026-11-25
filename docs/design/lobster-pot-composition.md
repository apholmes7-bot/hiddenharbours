# Lobster pot composition: isolated arithmetic

This is the pure composition slice requested for NPC lobster boats on 2026-11-11. It serves P1
(seasonal catch) and P3 (a working coast). `LobsterPotComposition` has no gameplay callers. It
returns a statistical pot sample; it does not claim to capture living occupants or to implement
NPC operations. See [the living capture phases](living-fish-capture-phases.md) before integration.

## Inputs and output

`TryCompose(seed, gameTimeSeconds, ground, soakGameSeconds, settings, buffer, out summary)` uses
absolute Core clock seconds at haul. The seed identifies this pot sample; a future caller must
derive a distinct stable seed per pot through Core. The ground carries a stable id, pot capacity,
integer carapace size bands, a minimum berried size and four seasonal rows. Settings explicitly
carry the clock day length, season length, starting calendar day and soak saturation duration.
There are no production defaults or authored ground assets in this change. A reviewed Def adapter
must supply these values before use; no existing owner tuning changes here.

The four rows follow Core's EarlySpring, HighSummer, TheTurn and HardWinter order. Calendar
phase includes the configured starting day, matching the elapsed-day/offset convention of
`GameCalendar`; it does not assume a civil day or a spring start. The arithmetic reduces the
year before applying the offset to keep long-time and extreme finite inputs bounded. Season
is evaluated at haul, including when a soak crosses a season boundary.

Each row supplies full-soak occupancy probability, the share of **all pot occupants** below the
retention gauge and the share of **all pot occupants** that are berried females. These last two
attributes are sampled independently and may overlap. Short means below the configured gauge,
not necessarily immature. Berried short sizes are drawn from the ground's minimum berried size
through gauge minus one; other short sizes start at the ground minimum. Nonshort sizes run
from gauge through the ground maximum, inclusively. No total-body-length or weight conversion
is inferred from carapace length.

The caller owns an array of at least capacity elements. Success writes exactly `summary.Count`
compact entries, leaving the suffix unchanged. Each entry has carapace millimetres, `IsShort` and
`IsBerried`. Summary counts preserve the overlap, so `SizeAndEggEligible` subtracts exclusions
once. It is not a legal retention decision: season openings, licences, maximum/window gauges,
v-notches and other protections require separate reviewed rules. Failure returns a zero summary
without changing storage. Missing ground identity, nonfinite/out-of-range tuning, invalid bands,
negative time/soak, soak longer than world age and insufficient storage fail closed.

## Sampling rule and limits

For each of capacity slots, occupancy probability is:

`season.FullSoakOccupancy01 * min(1, soakGameSeconds / settings.FullSoakGameSeconds)`.

StableHash keys the ground id, seed and exact haul-time bits; separate tagged draws select slot
presence, short flag, berried flag and size. At a fixed haul instant, increasing soak adds slots
without resizing earlier animals and saturates at full soak. Expected count is capacity times
occupancy; proportions are ensemble probabilities, not guaranteed quotas in one small pot.
Sizes are uniformly distributed within each authored integer band. This bounded binomial/linear
curve is a testable prototype, not a calibrated catch-per-unit-effort or trap escape model.

Changing haul time changes the sample stream. Therefore this function must **not** be repeatedly
queried as persistent stock, used to reroll a player's trap, or substituted for occupant identity,
reservation, depletion or the historical competing-pot solver. Sleep/unloaded-world equivalence
is not claimed for this separate statistical slice. Wiring remains a later reviewed task. Any
eventual catch still needs physical hand loading, transport and unloading.

## Biology and calibration boundary

DFO describes external egg carrying for 9–12 months and Canadian hatching between June and
September. Berried presence therefore cannot be represented as a summer-only flag. The four
seasonal shares stay authored rather than being inferred from hatch dates.
([A lobster's life on the bottom](https://www.dfo-mpo.gc.ca/species-especes/publications/lobsterlife-viehomard/index-eng.html))

DFO's Area 17 plan describes temperature-sensitive catch rates and protection of egg-bearing
females. Those facts support variable catch composition and a separate berried exclusion; they
do not establish percentages for the fictional grounds. No regional percentage, legal minimum
or saturation duration is asserted here.
([Area 17 management plan](https://www.dfo-mpo.gc.ca/fisheries-peches/ifmp-gmp/lobster-homard/2024/index-eng.html))

Before integration, content review must supply ground-specific observed size distributions,
seasonal short/berried shares among pot occupants, maturity-compatible berried sizes and the
soak response. Test fixture numbers are synthetic, not suggested balance or biological estimates.

## Proof and cost

The EditMode fixture checks every animal across 1,000 seeds and six long-time instants,
query-order independence, exact endpoint shares, overlapping exclusions, seasonal boundaries
with calendar offset, year wrap, size endpoints, storage preservation, invalid inputs, soak
saturation and ensemble proportions across 10,000 seeds per season. The allocation test first
requires a positive allocation probe; a stub counter skips instead of reporting false zero proof.

Work is O(capacity + id length), storage is caller-owned and sampling allocates zero managed
bytes. Capacity remains an explicit caller budget. This change adds no tick, controller, content,
save field, scene, renderer or runtime asset; it cannot change the current picture or catch loop.

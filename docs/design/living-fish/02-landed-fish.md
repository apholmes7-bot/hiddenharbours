# Living fish: landing and handling

Proposal, not a save-schema or animation change. Current catch freshness and physical container fill are described in [the audit](00-today.md). This design serves cozy but consequential fishing and the by-hand goods ruling.

## State and the player's hands

Track **location** separately from **vitality**. Locations are Hooked, InHand, Deck, Container and Released. Vitality is Alive, Flopping or Still. Flopping is an active alive presentation, not a second kind of inventory item. Hooked and landed individuals are explicit state, unlike the background field. On landing reserve the actual fish on the line, move it to the available hand or supported deck position, and let the player place it. A full hand or tote leaves an honest fish at the landing point; it never silently enters a remote hold or disappears.

| Transition | Player action and sprite direction |
|---|---|
| Free → Hooked/Alive | Reserve the individual's school/member key; show the existing line fight and species jump permission. No gore or visible hook wound. |
| Hooked → InHand/Alive | Fish crosses the gunwale/shore edge into the grasp anchor. A small restrained tail movement, no forced close-up. |
| InHand → Deck/Flopping | Player sets the fish on a valid supported deck collider. A short low-amplitude tail/body flop within its footprint; the body always returns onto the deck, never hangs above it. |
| Deck/InHand → Container/Alive | Pick up, carry and lower into bucket/tote; transfer inventory only once physical placement succeeds. The existing fill system shows the contents. Finfish may have brief restrained motion; shellfish use leg/shell motion, never a fish flop. |
| Any alive location → Still | Movement fades to a laid pose. No blood, sounds of distress, death spectacle or reward for waiting. Still is irreversible. Freshness continues. |
| InHand/Alive → Released | Carry to reachable water, lower or gently toss along a short visible arc; validate depth, remove owned item after successful release, then restore a free visual. No teleport from a distant storage menu. |

A supported deck point is mandatory. Boat-local coordinates and support-plane height follow the moving hull. Never place catch using a world z offset that makes it levitate when the boat rolls. Container placement uses its actual opening and available capacity. Picking up the last fish clears that visible fill slot; holding it in hand does not also leave a duplicate in the tote.

## Duration and freshness

Suggested **gameplay tuning**, not biological survival claims. Author all values per species in a later handling Def. Use the existing Core clock: one displayed minute is `SecondsPerDay/1440`, one displayed hour is `SecondsPerDay/24`. Freeze these conversions into the same context used by freshness; do not mix real animation seconds and the compressed day clock.

| Kind | Active presentation after an unprotected landing | Provisional unprotected alive budget |
|---|---|---|
| Finfish | First displayed minute: occasional small flops. Next four minutes: rare tail/gill movement. Then a quiet pose until the alive budget ends. | 20 displayed minutes |
| Lobster/crab | Up to two displayed minutes of restrained leg/antenna motion; no struggling loop thereafter. | Four displayed hours in dry, unsuitable storage |
| Clam/mussel/oyster | One shell-close response, then quiet; no finfish gill/flop animation. | Twelve displayed hours in dry, unsuitable storage |

Tie vitality to the same elapsed intervals freshness settles: bank unprotected exposure on transfer, sleep/skip and save, not per-frame countdowns. The future alive budget is the smaller of the species exposure budget and the time to accrue **0.05 ambient spoil** (`0.05*SecondsPerDay/SpoilPerDay`, omitted if spoil/day is zero). These thresholds are proposed Def fields, not production literals. This links handling urgency to existing perishability without pretending the sell-refusal threshold of 0.9 is a death threshold. High-quality fish can be still; alive does not mean unspoiled.

Keep existing freshness exactly: `spoil = clamp01(banked + elapsed/SecondsPerDay * SpoilPerDay * storageMultiplier)`. Storage changes settle the old interval first. Cooling never resets spoil. Freeze/ice already have multiplier zero under the default policy; retain finite ice protection and melting behaviour. See `Core/Economy/Freshness.cs:140`, `:167`, `:192` and `Boats/DeckIceBox.cs:182`. Vitality requires its own monotone exposure bank because LastSettleGameSeconds is not a recoverable landing timestamp.

## Storage and choices

- **Bucket:** a wet bucket is an existing live-storage mode for shellfish; it is not automatically a finfish livewell. Finfish need an authored suitable water/oxygen/capacity rule before protected alive time is allowed. An ordinary bucket can still carry fish with the unprotected budget. No new livewell system is built here.
- **Tote/deck:** ambient freshness and unprotected alive exposure run together. The player carries fish into the opening or onto the deck. Capacity and crowding are explicit, with the actual fill visible.
- **Ice:** carry ice to the ice box, then place catch. Use the existing protection-until-melt clock. Ice protects food quality, not the alive-exposure budget; finfish do not gain livewell time from it. For cozy presentation, go directly to the quiet laid pose on ice; do not show a prolonged struggle. Ice protection never revives a Still fish or automatically grants a live-sale premium.
- **Sea:** a still fish is not catch-and-release. An alive release is available from the hand beside suitable reachable water. There is no mandatory release minigame or moral penalty for keeping a legal catch. A later disposal verb for spoiled catch must remain distinct from release.

Save the hooked/landed identity, location, vitality, banked exposure and settle instant only when that handling slice is authorized, with a migration and old-save test owned by lead-architect. Default migrated catches to Still with their existing freshness untouched; never invent an unknown living history. Background school positions remain unsaved. This charter's code does not change the save format.

## Rating and tone

My understanding, **not a rating certificate**: non-graphic catching and ordinary handling can fit all-ages fishing games; realistic suffering, deliberate cruelty, detailed injuries or gore are the elements likely to raise concern. The whole game's presentation and context determine a board's decision. ESRB distinguishes mild violence from more intense violence and blood/gore in its [ratings guide](https://www.esrb.org/ratings-guide/). PEGI explicitly distinguishes non-detailed/non-realistic violence from increasingly realistic depictions in its [label explanations](https://pegi.info/what-do-the-labels-mean). Neither guide certifies this proposed fish sequence as suitable for a particular age.

Keep the camera at normal play distance, make movement short and matter-of-fact, offer an immediate quiet handling presentation as an accessibility/tone choice, and never require watching a fish gasp to progress. No blood, gutting, injury zooms or realistic distress audio in this scope. Art-pipeline and audio must review the actual sequence later; this design approves no new look.

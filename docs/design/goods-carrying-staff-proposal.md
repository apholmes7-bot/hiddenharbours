# Hired hands carrying proposal

**M3 design proposal.** A hired worker takes over the same pickup, supported carry and set-down that the player learned by hand. The player sees a person moving the real load and eventually manages exceptions instead of repeating trips. Hiring creates paid capacity, with wages and an efficiency cost; it never grants remote access to a boat hold or a teleporting supply chain.

Authority: [P4](../vision-and-pillars.md#3-the-five-pillars), [economy staff and policies](economy-and-business.md), and the [M3 roadmap](../roadmap.md#m3--offshore--enterprise). The roadmap places staff in M3 even where older progression examples suggest an earlier first hire. This adds no staff AI, pathfinding, UI, scene, art or save implementation.

## From manual work to standing orders

| Step | Player action and earned prerequisite | What the player sees | What changes underneath |
|---|---|---|---|
| Learn the route by hand | Catch or acquire goods; pick up, carry, place and physically present a first sale | A fish leaves the boat, occupies hands, reaches the counter, is weighed/quoted and is sold after confirmation; an empty pail stays theirs | Record completion of the relevant manual work, not a generic experience threshold |
| Improve manual capacity in M2 | Buy compatible containers and a wharf cart; load/unload them personally | More fish travel together because a real loaded container sits on a supported cart bed | Capacity follows measured fit, gross load and stack rules; no free load/unload shortcut |
| Hire the first deckhand in M3 | Have the crew slot, hire contract and payroll to support help; keep skippering | The deckhand repeats demonstrated tending/icing/carrying work aboard the same boat while the player directs the trip | Assist one proven job; no unattended skipper or free shore hauler is bundled into the hire |
| Delegate a complete job | Follow the existing deckhand → skipper/processor/seller progression as facilities and staff become available | A crewed second boat or staffed facility does work the player already understands | These roles retain their own prerequisites and wages; a seller can only sell delivered stock |
| Hire a hauler for a known route | Own/access both endpoint supports, the route and any cart/vehicle; demonstrate its loading and delivery task | The worker walks to the source, takes one legal load, travels, places it, and returns with the empty reusable container if ordered | One local standing order replaces repeated trips; it does not unlock every site on the map |
| Add machinery after carts and trucks work | Provide the proper operator, equipment, access and unloading support | A full tub moves on compatible forks or lifting gear, then rests on the truck/store support | A tractor or forklift supports the weight throughout; a hauler cannot hand-lift an industrial tub |
| Delegate site logistics to a manager | Have working staffed routes, payroll and authored storage/receiving capacity | Staff continue their visible work; the player hears about blocked routes, low ice or unmet policy instead of assigning each tote | A manager chooses among the same legal jobs; later multi-site/freight scope remains in its own phase |

Use the [RULED 2026-11-11 goods choices](../../backlog/proposed-goods-handling.md#ruled-handling-choices--2026-11-11): normal puts refuse when full, the first sale presents one fish on the counter, the first cart is a four-wheel low wharf cart, and powered machines follow working carts/trucks. A large fish uses both hands after setting down the rod, within its authored envelope. No carry limit, wage, unlock count or speed is hard-coded by this proposal. Each job's manual version remains usable after hiring.

## The first hauler order

The player selects a source storage interaction, a destination receiving/storage interaction, allowed goods, an available carrier and a work schedule. They choose whether the route delivers for inspection or includes an already-authorized seller policy. A price floor and channel belong to selling policy; a hauling order alone never authorizes a sale.

The route setup checks access rights, a usable path, loading and unloading supports, allowed container types, mass/fit, hand needs and safe staging. Missing infrastructure gives a concrete refusal such as “The cart cannot reach that door.” A whole container moves only if its permitted children and gross load fit the carrier. The order does not promise a route that would need an unbuilt lift, an unowned machine or a second worker.

The worker routine is the manual routine, with durable commit points:

1. **Choose a job.** Among policy-eligible stock, prioritize time to buyer refusal, then order creation sequence and entity id. Read the existing freshness clock; do not change quality to satisfy the order. Confirm the shift, wages/access policy, route and destination capacity.
2. **Reserve.** Reserve the exact source entity and planned destination placement against their revisions. Another worker or the player cannot claim the same fish. A reservation is permission to attempt a transfer, not ownership or a second copy.
3. **Approach and pick up.** Walk to the source and revalidate. Commit source → worker hand/device through the shared transfer service. A two-hand tote consumes both hands; the worker cannot also use a tool. A machine load uses compatible grip/fork/support contracts.
4. **Carry.** Traverse the actual route using the shared movement and carry limits. Cargo remains attached to its real support/carrier. Stops, travel time, ice use and spoilage continue on game time. A closed tide route or unsafe weather can delay the trip.
5. **Place or wait.** Revalidate destination access and fit on arrival, then commit hand/device → destination. A destination that filled while travelling causes a visible pause; the load stays with the worker or on an already authorized safe staging support. It never snaps back to source or appears inside a full tote.
6. **Inspect and settle.** A buyer inspects only the delivered goods. If the order includes authorized selling, invoke the same quote/refusal/settlement path as the player. Otherwise leave a player-owned, retrievable delivery. An empty receiving pail/tote remains owned, and returns only through another physical carry leg.
7. **Release and repeat.** Release reservations, record the completed step and choose the next legal job. Completed work and empty-return trips count toward paid time. Idle workers do not manufacture stock or remove wage obligations outside the approved wage policy.

## Interruptions the player can understand

| Interruption | Visible behaviour and player choice | State that must survive |
|---|---|---|
| No eligible stock | Worker waits at the route's existing start interaction; player can change the threshold/order | Job remains idle; source stock is untouched |
| Source changed or item taken | Pickup refuses before commit and the worker selects again | No withdrawal, hand item or charge from the failed pickup |
| Hands occupied, overweight or no fit | Worker explains the specific limit; player unloads, selects a smaller container or supplies the right device | Original physical location retained |
| Closed sandbar, obstructed door or unsafe route | Worker waits before the blocked segment, using an existing safe support if available; player clears the route or changes the order | Held/staged goods and their clocks persist; no crossing a closed tide gate |
| Full destination or buyer closed | Worker holds/stages the load and asks for space or another authorized endpoint | Delivered/held goods stay player-owned; no remote sale or silent reroute |
| Ice runs out or buyer refuses grade | Freshness keeps advancing; worker retains the load and raises a plain exception | Existing spoil/life history and refusal; no re-icing reset or automatic disposal |
| Shift ends, worker removed, order cancelled | Finish only the currently safe set-down if permitted; otherwise remain at a safe stop for handover | No item disappears with the worker; a return journey is a real job |
| Player takes over | Pause at a stable boundary, release reservations and use a normal legal handover | Exactly one owner/location, with free-hand and load checks for the player |

A worker never invents a staging surface or levitates a load. If no legal set-down exists, the supported carrier keeps it and the order reports the obstruction. Disposal and changing the destination require explicit policy authority; “sell” does not mean “dump whatever is refused.” Worker absence, death/rescue and inaccessible carriers need recovery rules before those hazards ship.

## Data and service boundaries

| Proposed data | Authoritative owner | Required content checks |
|---|---|---|
| Job capability and role Def | economy-sim; shared mechanics through Core | References an existing hand-operable job; explicit wage and efficiency policy |
| Carry rules and carrier compatibility | gameplay-systems, using Def/config data | Same mass, opening, stacking, support and hands rules as the player |
| Route/order policy | economy-sim with world access through Core | Valid endpoints, goods filters, schedule, legal staging and transport scope |
| Receiving and seller policy | economy-sim | Physical delivery, grade/refusal, reusable-container ownership, explicit sell authority |
| Staff presentation | art/world/UI lanes | Worker, carried contents and supported set-down stay legible; no new art is accepted here |

Both player and worker call one Core transfer contract and one economy settlement contract. Staff work routines belong to economy-sim; shared character identity and social routines remain world-content work. Avoid cross-module concrete-type references and parallel staff-only versions of fit, freshness or sale logic.

The [future goods save contract](goods-persistence-proposal.md) must exist before persistent jobs. M3 adds stable worker/order/job ids, current committed stage, reservations/revisions, cargo carrier and route progress needed to resume. Save before/after a commit captures a complete state; loading or retrying cannot pick up or sell twice. Item location remains in the goods registry rather than a second staff inventory.

Offscreen work may advance through deterministic route events with durations derived from the same route and authored speeds. It still consumes travel time, carrier capacity, ice, wages and destination space. Resume must settle at every relevant boundary; it may not place cargo at the destination merely because the region unloaded. Any wall-clock offline accrual needs the separate capped M3 architecture policy.

## Acceptance before activation

Demonstrate a hand-played delivery, then the same route with the first worker, including a visible pickup, supported travel, set-down and empty return. Prove one fish cannot be reserved twice; destination changes leave cargo in the carrier; exact container membership and cold history survive save/load in every committed stage; and a tide gate stops both actors under the same conditions.

Compare skilled hand-play and delegated work using identical assets, route, goods and game-time span. Include wages, return trips, spoilage and the authored efficiency/quality tax. Delegation buys the player's time and scale; it must not strictly beat mastery with the same assets. Tune the tax in data without awarding quality above the input catch. The owner can still complete the entire route manually after unassigning staff.

Implementation order is the approved manual loop and durable goods first, M2 containers/carts second, then M3 paid assistance and the existing role ladder, then haulers and managers. Each phase must pass its own gameplay and art acceptance. This design does not pull hires, machines or freight into the current shipped loop.

# Proposal: a fixture press must not cross a wall

**PROPOSAL, nobody has accepted this.** Design only, against main
`f1b4ab2f59f417eda06f2239f7dee25d4c52ad23`. No code, data, prefab, scene,
collision shape, rendering, save format or configuration value changes here.
The [fixture audit](fixture-interaction-audit.md) records today's behaviour and
the exact source lines. This proposal fixes the reach decision; it does not add
missing fridge/seat verbs or give cooking furniture a solid footprint.

## Proposed player rule

A fixture is reachable only when the existing context, availability, inclusive
distance and optional facing rules pass **and** a straight segment from the
player's interaction position to the fixture's approach point crosses no active
wall, partition or closed doorway barrier on the applicable floor. An open door
gap is passable. A doorway around a corner does not permit reaching around that
corner: this is a press, not pathfinding.

Check while selecting/offering and again immediately before dispatch. A blocked
fixture must not win over an unobstructed neighbour. A chooser left open while a
door closes must not execute its old selection. A blocked press must never
publish `InteractPerformed`, start a wash/cook/rest, open a wardrobe, or move
goods. Preserve existing ownership, occupied-bed, needs, modal and action-claim
refusals; do not solve this by loosening the current assertions.

The wall check concerns physical access, not whether the camera draws a wall.
A cutaway wall still blocks; an inactive rendering child is not evidence of an
open doorway. Different storeys or different vessel interiors do not become
reachable merely because their projected XY positions overlap.

## Where the decision would live

Add a plain, non-`MonoBehaviour` class `FixtureReachPolicy` in
`Assets/_Project/Code/Core/Interaction/`. It receives immutable values and a
read-only snapshot through a Core interface, tentatively `IInteractionBarrierSource`.
The class must be runnable in EditMode without a scene, physics world,
`GameObject.Find`, input devices, renderer or frame clock. Keep the existing
`InteractResolver` context/availability/facing/ranking rule; insert the new
spatial policy after cheap eligibility checks and before candidates compete.
Keep **one** spatial predicate shared by resolution and dispatch.

Use a Core `IFixtureReachTarget` descriptor contract to carry scope and approach
point beside `IInteractable`, and Core snapshot values for obstacle facts. World owns
the adapter for buildings; Boats owns the adapter for cabin obstructions; Player
publishes the actor's position and scope through Core; App composes those
providers. Core must never reference `BuildingInterior`, `BoatInterior`,
`PlayerWalkController`, or Economy's `StallReach`. A test supplies an explicit
empty snapshot for an open environment. A registered room fixture with a missing
provider/scope is **Unknown**, which refuses interaction; it is not silently
treated as an empty room.

Avoid a universal `Physics2D.Linecast` against all solids. It would hit the
fixture itself, the player, other furniture, the outhouse shell, and appliance
stops intentionally shifted 0.7 m below their displayed ground contact. Cabins
also use logical hull-local obstruction polygons instead of individual furniture
colliders. A raycast cannot invent the architectural meaning or floor of those
hits. The recommended implementation uses explicit architectural barriers and
plain segment/polygon tests; a future physics adapter could feed the same
contract only after it proves identical semantics.

## Facts the policy needs

| Input | Producer / meaning |
|---|---|
| Actor position and facing | Fresh authoritative interaction/foot position, in the same frame as the target; retain current facing semantics |
| Actor context and spatial scope | Core value containing region/interior instance, level and coordinate frame; outside and room scopes are compatible only across a real open doorway on the same floor |
| Fixture id, approach point and scope | Stable interaction id and a point the player can physically approach; use current `WorldPosition` where valid, the outhouse's existing door offer offset, and bunk `ReachPoint` in its cabin frame |
| Reach and eligibility | Existing fixture reach, context, availability, optional facing and occupation/needs rules; keep their current meanings |
| Architectural barrier polygons | Active exterior wall segments, interior partitions and closed door leaves/plugs for the relevant level; include barriers between a just-outside actor and an inside fixture |
| Transform/frame information | A coherent snapshot transforming both endpoints into house floor or hull coordinates; no comparison between arbitrary projected XY and unprojected geometry |
| Barrier identity and revision | Stable ids; a revision advances when a door, level, placement or lifetime changes, so a cache cannot outlive the world it describes |
| Contact tolerance | Explicit non-negative metric tolerance from a future settings contract; this document selects no new tuning value |

The result is a value such as `Allowed`, `OutOfReach`, `WrongScope`, `Blocked`
or `Unknown`, optionally with the barrier id for diagnostics. No save fields are
needed; snapshots and actor scopes are transient. No hidden time or randomness
belongs in the predicate. Identical ordered inputs must give identical results;
when several barriers block, choose a diagnostic id by ordinal order rather than
depending on discovery order.

### Getting trustworthy walls and endpoints

For buildings, adapt the actual enabled architectural wall/partition geometry
that `BuildingInteriorStander.BuildWalls` and `StPetersInteriors.BuildUpperWalls`
create, together with their owning `BuildingInterior` and level. Include any
ground-floor partitions when authored; the current `InteriorFixturePlacement`
pilot explicitly leaves its enclosing partition for a later pass. Register the
geometry at construction/loading and remove it on unload/disable. Existing scenes
must be supported by reading their existing wall objects; do not require a scene
rebuild just to populate the query. A missing or ambiguous ownership mapping
must be reported as Unknown in verification, not repaired by changing a scene.

The adapter must distinguish physical wall activation from art hiding and from
the intentional activation of only the current storey. Rebuild or revise the
snapshot on those changes and on door state changes. For an outside player,
include the target building's exterior barriers; simply requiring "same room"
would wrongly forbid using a nearby fixture through an open door, while querying
only the actor's empty outdoor scope would allow pressing through a closed wall.

For boats, adapt current room outlines, partitions/closed doors and their level
in hull coordinates through a Boats implementation of the Core contract. Do not
classify every `BoatInteriorObstruction.Treatment == wall` as architecture:
`BoatCabinWalkMath` uses that word for furniture too (for example a helm console).
The provider needs an explicit architectural classification; ambiguous cabin
barriers remain Unknown until classified in a separately reviewed implementation.
Bunks must keep their existing current-level and berth-occupation gates. The
new design must not pretend a house Collider2D query fixes cabin bulkheads.

The source audit identifies no offered settee/fridge interaction to migrate.
Future implementations must supply approach/scope facts when they register;
writing an action string into a Def still does not create an interaction.

### Geometry boundary rules

- Keep current reach inclusive: squared distance equal to squared reach passes.
  Preserve the current clamp of negative reach to zero, including its zero-distance
  edge; changing that existing rule is outside this proposal.
- Ignore triggers, player shapes and non-architectural furniture. Ignore a target's
  ordinary body, **not** an entire building hierarchy that may contain a real wall.
- The segment may terminate on a designated approach surface owned by the target,
  provided it has not crossed barrier interior first. A tolerance must not erase
  a thin wall or create a gap at a partition corner. A start inside a wall refuses;
  a zero-length press inside a wall also refuses.
- A segment that enters a wall, overlaps its boundary for a nonzero interval, or
  touches a blocking corner refuses conservatively. Author approach points on the
  accessible side; do not choose a different side merely to get a passing result.
- Use an explicit entry approach point for a bath, toilet or outhouse. Do not aim
  at an inaccessible back-wall pivot or ignore all outhouse walls just because it
  is the target. A door action may target its near surface; entering it still
  requires the door to become open.
- A clear geometric segment does not authorize walking, teleporting or transferring
  inventory by itself. Existing action code remains responsible for its own job.

## Every existing caller that would change

Paths below are relative to `Assets/_Project/Code/`. This is the call-site plan,
not a claim that any method has been modified. Keep pure transaction/model APIs
usable without inventing a player position: spatial authorization belongs at the
player-facing entry point immediately before calling them.

| File and method(s) | Required change |
|---|---|
| `Core/Interaction/InteractResolver.cs`: `Qualifies`, `TryResolve`, `TryResolveNow` | Thread an explicit reach query/snapshot through the candidate scan; apply it before ranking, retain the current priority/distance/id ordering |
| `Core/Interaction/InteractChoiceList.cs`: `Build`, `TryResolveDefault`, `FreeMateFor` | Pass the same query on all three paths; do not substitute an occupied bunk with a free one behind a wall; `BuildActionsOf` receives an already checked target or checks before opening an unregistered list |
| `Core/Interaction/InteractVerb.cs`: `BuildFor`, `TryPress`, `TryPerform`, `PublishCandidate`, `Perform` | Share selection/preview policy; obtain fresh scope/geometry for final dispatch; have dispatch return a result so rejection cannot be reported as performed |
| `Core/Interaction/InteractChooser.cs`: `Open`, `OpenUnregistered`/unregistered-list route, `Confirm`, `StillWorkable` | Check before opening and obtain a fresh actor/snapshot at confirm, including unregistered lists; close safely on failure and never execute a stale action |
| `Player/ControlSwitcher.cs`: `ActorNow`, `TryInteractCandidate`, `Update` | Publish current actor scope and supply the query for press/preview; preserve all existing interaction-claim ordering and input mapping |
| `Core/Interaction/InteractActor.cs` and `InteractActorProbe.cs` | Carry the spatial scope through the Core value/probe without a feature-module dependency; update constructor/`For` producers and test fixtures explicitly |
| `World/InteriorBed.cs`, `InteriorWardrobe.cs`, `InteriorStair.cs` | Supply approach point and building/level scope alongside their existing `WorldPosition`/`ReachMeters`; stairs are targeted on their departure floor |
| `World/Washrooms/WashStationPoint.cs`, `SitPoint.cs` | Supply approach/scope for bath, basin, sink, shower, toilet and outhouse; expose an explicit cancellation action so Stop washing/Get up can always release an existing session even if a barrier changes |
| `Player/CookingStationFixture.cs`, `StowageRack.cs`, `CamperFreezer.cs` | Supply approach/scope through the Core contract; keep `Use`, storage operations and `Exchange` as non-input APIs called only after the checked player-facing dispatch |
| `Player/GinnyFreezer.cs`: `Update` | After the existing `StallReach` gate, invoke the Core policy using the live player and freezer position before `Exchange`; preserve the current F key for this focused fix |
| `Boats/BoatBunk.cs`, `BoatInteriorInstaller.cs`: `BuildBunks` | Supply vessel/level/approach metadata and register cabin barrier provider; preserve bunk reach, level and occupation logic |
| `World/WorldInteractor.cs`: `FindNearest`, `BeginInteract`, `CurrentActor`, `TryOfferTalkOrGive`, `Offer`, `TalkFromChoice`, `GiveFromChoice` | Use the same architectural reach policy for active conversation targets, including counter merchants; pass scope into its alternate actor constructor and unregistered chooser; recheck before starting dialogue/giving so the conversation route cannot bypass a blocked press |
| `World/ConversationChoice.cs`: `Bind` and target descriptor | Supply the bound target's approach and scope, so the unregistered Talk/Give chooser receives the same wall check as the direct conversation |
| `UI/WardrobePicker.cs`: `Open`, `PlayerHasLeft` | Validate late/direct wardrobe requests and close safely if access becomes blocked; keep closing/cancellation available |
| `World/BuildingInterior.cs`: `Configure`, `ConfigureUpperLevel`, `ConfigureDwelling`, `TryGoToLevel` and lifetime handling; `App/Editor/World/BuildingInteriorStander.cs`: `BuildWalls`; `App/Editor/StPetersInteriors.cs`: `BuildUpperWalls` | Feed current building barrier/scoping facts through the World adapter/Core interface; support existing serialized geometry without rebuilding scenes |
| `Boats/BoatInteriorInstaller.cs`: `Build`, `Rebuild`, `TearDown`; `Boats/BoatInterior.cs`: `TryEnter`, `LateUpdate`, `OnDestroy`; `Boats/BoatCabinDoor.cs` door-state changes | Register/unregister the cabin provider and revise it when the vessel moves, changes level or a door changes state; connect a new App composition adapter through Core |

These names were checked against the pinned main. Repeat the caller search if
main changes before implementation. Preview currently reaches `PublishCandidate`
via the control switcher's `Update`, and both registered and unregistered chooser
flows must remain covered if their names are refactored.

`Economy/StallReach.CanInteract` and `StallGate` need **no global semantic change**:
they also serve `HomeDoor`, and turning their distance predicate into a global
wall-aware service would silently change another feature. The freezer adds its
Core check at its own caller. Merchant `TryBuy`, kitchen/storage models,
`FoodEater`, and rest/save APIs remain non-spatial model operations.
Other registered interactables (carried items, vehicle/boat doors, deck work,
clam digging) need an explicit applicability classification in the provider:
only audited fixed room fixtures, cabin bunks and merchant conversation targets
are in this rollout. Existing unknown room fixtures refuse; deliberately
out-of-scope actions retain their previous policy. Do not silently expand the
scope by testing every registry member against house walls.

For already-running washing/seating sessions, cancellation/get-up is a local
release action, not a new reach across a wall. Preserve it independently of target
selection so a new door closure cannot trap the player. A timed cooking job may
finish without another press; guarding its start does not require polling the
wall throughout the cook. Do not add a spatial check inside every model mutation.

## Tests to extend before implementation is accepted

The audit searched existing suites first. The paths below are under
`Assets/Tests/`; extend those suites instead of creating another parallel
resolver, chooser or reach harness. Only a new pure geometry class without an
existing appropriate home warrants a small new fixture for its polygon cases.

| Existing suite | Cases to add while retaining existing assertions |
|---|---|
| `EditMode/InteractVerbTests.cs` | Clear same-floor segment allows; wall/partition refuses; blocked high-priority/near candidate loses to an accessible candidate; all blocked returns no target; no action/event on blocked direct dispatch; unchanged inclusive/negative reach, context and id tie-break tests |
| `EditMode/InteractArcTests.cs` | Facing never substitutes for a wall check; unknown facing still respects walls; behind-player targets retain existing arc rule |
| `EditMode/InteractChooserTests.cs` | Blocked rows absent; clear alternative survives; door closes or target/player moves between open and confirm; unregistered-list route rechecks; free bunk behind bulkhead never substitutes; no duplicate dispatch or event |
| `EditMode/InteractOfferTests.cs` | Preview and next press agree for the same snapshot; moving/closing barriers invalidate offers; cancellation is still available |
| `EditMode/Economy/StallGateTests.cs`, `EditMode/CamperFreezerTests.cs` (already covers `GinnyFreezer`), `EditMode/ColdChainTests.cs` | Preserve the existing pure range contract; add caller-level freezer coverage, proving F within 4 m behind a wall does nothing and a clear F still exchanges |
| `EditMode/Washrooms/WashStationPointTests.cs`, `SitPointTests.cs` | Block new start across a partition, retain ownership/availability semantics; stop/get-up works after a barrier changes or target unregisters |
| `EditMode/BedBringsTheMorningTests.cs`, `CamperFreezerTests.cs`, `StowageRackTests.cs`, existing cooking fixture/content tests | Checked player-facing paths never start rest, transfer goods or cook through a wall; same-side success/refusal remains unchanged; avoid adding reach to pure model APIs |
| `EditMode/InteriorFixturePlacementTests.cs`, `StPetersInteriorsTests.cs` | Existing wall/partition ownership and level can be exported without a scene rewrite; open door gap remains open; no obstacle geometry or dimensions change |
| `EditMode/BoatInteriorRuntimeTests.cs`, `EditMode/BoatBunkTests.cs`, `EditMode/RigBaking/BoatInteriorKitchenTests.cs` | Same boat/level transforms, moving hull, closed bulkhead, open passage and bunk approach; furniture Treatment values do not get misclassified as architecture |
| `EditMode/World/WorldInteractorModalGateTests.cs` | Wall filtering and final dialogue recheck preserve existing modal/action-claim rules for the counter route |
| `PlayMode/InteractVerbPlayTests.cs`, `InteractPopupPlayTests.cs` | A small actual wall/door fixture proves offer, chooser and press agree; legacy freezer F; disabled/unloaded geometry; no rendered plate baseline changes |

Pure geometric cases must cover horizontal, vertical and rotated walls; a thin
wall; start/end inside; corner tangency; collinear overlap; zero-length segment;
open gap and gap smaller than tolerance; different storeys at identical XY;
outside-to-inside across an open versus closed door; two overlapping interiors;
fixture self geometry; missing provider; and stable answers under barrier-order
permutation. Include a target touching a wall, a bath entry point, the outhouse
door offset and a berth whose visible mattress lies beyond its approach.
Test finite input rejection; a NaN cannot become an allowed press.

No test has been written or run for this proposal. The geometry, registration
lifecycle, actual input paths and player experience are therefore **unverified**.
Acceptance requires named executed test counts, not merely a green filter result.

## Cost per press and ongoing cost

Use the existing active-interactable scan. Let **N** be candidates surviving
context/availability/range/facing, and **E** the architectural polygon edges in
the relevant scopes after bounds rejection. A straightforward implementation is
**O(N x E)** for selection and **O(E)** for one fresh final target check. Building
chooser rows does not multiply geometry queries by the number of actions on the
same fixture; cache per candidate within that single immutable snapshot. A
chooser confirmation rechecks one target with a fresh revision. The legacy
freezer adds one **O(E)** check after its current range gate. Merchant selection
has the same candidate-by-edge form with its own target count.

For an illustrative room with 8 eligible fixtures and 24 rectangular barriers,
there are at most 8 x 96 = 768 edge tests for the scan, plus 96 for dispatch,
before early-outs/bounds culling. This is an operation estimate, **not measured
milliseconds or a claim about the current room counts**. Use reusable arrays and
value structs: the target is zero managed allocations per press after registration,
no scene-wide search, no physics query and no pathfinder. Snapshot rebuild costs
are O(E) when geometry changes; they must not occur once per candidate.

Preview already runs each frame. Adding the same barrier predicate costs up to
O(N x E) there too; a moving actor/hull or changing door invalidates it. Cache by
actor position/facing/context/scope, target position/availability and barrier
revision only when all are unchanged. A press must take a fresh snapshot even
if its highlight was cached. Do not sell the design as "one ray per press" while
hiding an unbounded per-frame scan. Measure typical and busiest furnished rooms,
chooser confirmation and cabin movement; record candidate/edge counts, allocations
and CPU time before making a performance claim.

The implementation cost is the plain predicate, Core contracts, World/Boats
adapters, registration and final-dispatch integration plus the tests above. The
largest uncertainty is correctly identifying architectural boundaries in existing
scenes/cabins without conflating them with furniture. Resolve that mapping with
static data and tests first; if it requires changing authored look/geometry or
new save data, split that dependency into a separately reviewed task. This
proposal alone authorizes none of those changes.

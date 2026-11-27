# Who is buying in the first week

Status: Core seam only; adoption below is future work. No interactable, purchase,
save format, scene or presentation changes in this slice. Solo play retains its
current behavior. This does not make the campaign playable in co-op.

The first week and demo take place on St Peters. Two players come first, with a
shared purse, shared licences and one shared dory. Hands, bags and held things
belong to a player. A guest may join at any time, work somewhere other than the
host, leave with his held things reserved, and return to those same things.
Only the host saves and pauses. These settled rules supersede the older
[campaign proposal](campaign-coop-plan.md)'s one-region, personal-licence and
restricted-join assumptions. The [first-week ruling](../adr/0062-the-first-week-is-closed-by-weather.md)
sets the island scope; the demo ends at the evening word, not a saved sleep.

## Existing code and the seam

Reviewed base: `98f9f3c55a1bfb3dda2e2c52a035138b0d9fab7a`.
Existing contracts are [InteractActor](../../Assets/_Project/Code/Core/Interaction/InteractActor.cs),
`ICarrier` in [ICarriable.cs](../../Assets/_Project/Code/Core/Interaction/ICarriable.cs),
[IShoppingBagHands](../../Assets/_Project/Code/Core/Interaction/IShoppingBagHands.cs)
and [IWallet](../../Assets/_Project/Code/Core/Economy/IWallet.cs). Reuse them;
there is no second hands, bag or money implementation here.

`InteractActor.PlayerKey` is an immutable, ordinal session-supplied string.
The existing three-argument constructor, three-argument `For` factory, and
`default(InteractActor)` report `InteractActor.LocalPlayerKey` (`player.local`).
New overloads take `string playerKey` last. Explicit null, empty and whitespace
keys throw; they must never silently buy for the solo player. Identity does not
come from position, region, frame order, a connection id, the clock or randomness.
The composition root must reuse an established key across movement and reconnects,
and never give a departed player's key to someone else.

The Core contract is [IActorServices](../../Assets/_Project/Code/Core/Interaction/IActorServices.cs):

```csharp
bool TryResolve(in InteractActor actor, out ActorServices services);
```

`ActorServices` exposes only `ICarrier Hands`, `IShoppingBagHands BagHands` and
`IWallet Purse`. Its getters reject destroyed component references. It is a view
of services, not a copy of goods or money and not a persistence snapshot.

- [SinglePlayerActorServices](../../Assets/_Project/Code/Core/Interaction/SinglePlayerActorServices.cs)
  reads today's `GameServices.Hands`, `BagHands` and `Wallet` on every lookup for
  `player.local`. It preserves null services during startup/teardown and refuses
  every other identity. No cached boot-time hands survive a subsequent lookup.
- [ActorServicesByPlayer](../../Assets/_Project/Code/Core/Interaction/ActorServicesByPlayer.cs)
  belongs to one session. Construct it with the shared `IWallet`, then
  `Bind(playerKey, hands, bagHands)` for each player. Both players resolve to the
  same purse and their own hands and bag interfaces. Bind order cannot select
  whose hands are returned. A replacement updates only that key.
- `TryResolve` returning false supplies an empty result. Callers must refuse or
  defer the request; they must not fall back to globals. A successful lookup can
  still contain a missing hands/bag provider, whose meaning the action must check.
  A co-op purchase must refuse missing required providers before any debit.
- `Unbind` and `Clear` remove live bindings only. They do not destroy or transfer
  held things. The future session owner retains a disconnected guest's inventory
  and rebinds the same state on return. Separate session objects share no registry.

Future composition injects `IActorServices` into interaction adapters; it does
not temporarily swap global hands while processing a guest request. No new slot
is added to `GameServices`, which other open work is changing. The solo adapter
is the compatibility path; the multiplayer map has no fallback to it. All current
call sites remain unchanged until their owning slices adopt the interface.

An actor key is an address, not permission. The eventual host request handler
must derive it from an authenticated participant, recheck reach and availability,
and serialize shared mutations. A guest cannot nominate another player's key.
The lookup supplies no networking, transaction ordering or host authority.

## Adoption order, highest risk first

Each row names the player action and the one-line mechanical change; the risk
column identifies additional work needed before that action is safe with two players.
The eight first-week action families include their alternative entry points.

| Order / action | Adoption change | Risk and acceptance proof |
|---|---|---|
| 1. Bed: `InteriorBed.Interact`, then `SleepFlow` / `RestSaveResponder` | Carry `actor.PlayerKey` through the rest request, resolve only his hands/bag, and address his rest position instead of global player state. | Sleep changes the shared clock and writes a rest anchor; host alone commits time/save, and setting his bag down must leave the other player's bag untouched. Local sleep presentation cannot imply authority to pause. The three-service bundle does not yet supply per-player body/rest state. |
| 2. Shelf: `ShelfSection.Interact(actor, index)` → `Buy` → `ShelfGood.Press` | Thread the actor through both purchase overloads and resolve hands, bag and purse once at commit. | Shared stock, debit and delivery must commit once, using the buyer's bag; two buyers racing for the last item get exactly one purchase. Check capacity before payment, preserve rollback/refusal semantics, and move `GameServices.Save` deferral to the host transaction. `IShelfTill` and its buyer-specific delivery also need a Core request contract; replacing only the three global reads is insufficient. |
| 3. Counter: clerk choice → `WharfSellPoint.SellOverTheCounter` | Pass the actor from the choice to `CarriedHold.From(services.Hands)` and use `services.Purse`. | Sell only the catch physically delivered in his bucket, once; keep the empty bucket in his hands. The other player's catch is untouched. Existing wired-hold overloads need explicit caller ownership, and the host orders competing sale requests. |
| 4. Mending station: `DoryMendingStation.Interact` / `Press` / held-work tick | Keep the actor key with the work session, resolve his bag, and use his position and region for every continuation check. | One shared dory and one material reservation: two people cannot consume the same materials or complete/launch twice. `ShoppingBagMendingCarry` still reads the one saved bag, so passing a second hands interface alone is insufficient. Its per-player contents and the host completion check are prerequisites. |
| 5. Rod: `CarriableTool.Interact`, `DevFishingInput` → `FishingController` | Address tool pickup, cast input, fitted parts and fight ownership by the actor key, resolving his held rod through `services.Hands`. | Ongoing casts and landed-catch signals need actor attribution; add a Core catch-hands view before landing into either player's hands. Rod skill/parts work remains with its drafts. Shared licences release unlicensed catches without blocking a cast; no automatic delivery to a distant hold. |
| 6. Clam dig: `ClamDig.Interact` → `TryDig`, including the `ClamDigger` input route | Pass the actor through digging, use his held shovel/bucket and position, and address the landing and effort checks to him. | One spent hole must yield once under competing requests. Catch-hands, effort and the fallback player hold need per-player contracts beyond this bundle; preserve deterministic tides and the release of unlicensed catch. Hand-picked shellfish need the same actor-addressed landing when their draft is adopted. |
| 7. Galley: `CookingStationFixture.Interact` → `Use` → `RecipeRunner` | Pass the actor key into starting and collecting a cook job, resolving his bag/hand delivery instead of global player state. | Ingredients, station occupancy and collection require host ordering; eating the result updates that player's needs. Shared station state is not a personal singleton; the current recipe/save rows need a separate ownership design. |
| 8. Licence vendor: counter choice → `LicenseVendor.TryBuy` | Pass the actor through the choice and supply `services.Purse` to the existing explicit-wallet overload, keeping the shared `ILicenseService`. | Licences stay shared; concurrent purchases charge once, and the one-time shellfish shortfall recovery cannot charge twice. The actor identifies who requested the purchase and receives the response, not a second licence wallet. |

The shelf-section source was reviewed at PR 1562 head
`5978d81d25886ae5d8178d2d00c55f0e9f95cd6d`: it currently drops the actor at
`Buy(index, now)`, before calling `ShelfGood.Press(now)`. The mending route was
reviewed at PR 1587 head `bbd06280846bfdd77563ca864765a60df7e29d36`: its
`Interact` uses global transform/bag hands and the material carrier uses saved
bag contents. These are adoption targets, not changes made to those drafts.

The surrounding first-week interactions also preserve identity end to end:
talk/choice adapters must retain the initiating actor; pickup, put-down and
bag storage use his hands; doors and boarding use his transform/region; the dory
sale uses the shared purse and host-owned story state. They are not made safe
by changing a shelf's reads. `InteractActorProbe`, `InteractChooser`, input
claims, offers and player-local responses remain single-view services on this
base. Their per-player routing must precede simultaneous local interactions;
no chooser may replace a stored actor with another player's current probe.

## Persistence and proof

Nothing in this slice requires saving: keys are supplied, registrations are
derived live references, and there are no new goods, counters or saved fields.
The default key matches the inspected PR 1569 `SavePlayerKeys.Local` spelling
without importing that pending type. The future save step must establish durable
player keys/reconnect membership, store each player's held items and bag contents,
and restore their bindings alongside one shared purse/licence/dory state. Save
v27's proposed clothing/skill/parts keys alone do not persist held goods. This
slice claims no restart or disconnected-inventory persistence support.

The existing `InteractVerbTests` fixture is extended for default/explicit keys,
dispatch preservation, live solo-service replacement, unknown-key refusal,
two distinct players in both bind orders, a shared purse, ordinal lookup,
rebind/leave/return and separate-session isolation. `ModuleBoundaryTests` retains
its existing assembly rules and adds checks that this seam's public types and
service properties stay in Core. Its recorded Fishing-to-Economy exception is
unchanged; this seam adds no feature dependency.

No Unity, EditMode, PlayMode or build ran locally. CI must compile and execute
the fixtures and report their actual test counts; source inspection is not a
passing runtime result. Interactable adoption and two-player play remain untested.

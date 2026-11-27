# One-player assumptions

This is an executable census of the first-week seams at `98f9f3c55a1bfb3dda2e2c52a035138b0d9fab7a`.
The entries below are the ratchet itself: `OnePlayerAssumptionTests` reads this document,
then compares exact occurrence counts by rule, runtime file and declaring member. Paths
are relative to `Assets/_Project/Code/`. No gameplay behavior changes here.

## Ownership decisions

Two players come first; the design allows four. The purse, licences, dory, shelf stock,
world containers and island story facts are shared. Hands, held equipment, worn things,
skill, needs, position and last beds belong to a player. One player rows the shared dory;
another aboard can fish. A guest may join at any time and be in a different region.
Leaving retains his held things with his player record. Only the host saves and pauses.
The demo ends at the evening word, under `st_peters.ginny_word_completed`, and has its own
save file; this census neither implements nor changes that ending.

These decisions follow the co-op plan in [#1563](https://github.com/apholmes7-bot/hiddenharbours/pull/1563)
and the first-week canon in [ADR 0062](../adr/0062-the-first-week-is-closed-by-weather.md).
The earlier ownership audit in [#1521](https://github.com/apholmes7-bot/hiddenharbours/pull/1521)
informed this census. Its files are not copied or changed. On this source baseline,
`ModuleBoundaryTests.cs` checks assembly boundaries and `PlayerTransformRelayTests.cs`
checks relay lifetime; neither performs a member census. The proposed lexical audit
in #1521 remains separate; this adds reflection, locator consumers, exact member counts,
shared-state classifications and stale-entry rejection.

`PER PLAYER` identifies a personal binding or selection that must become explicit.
`SHARED` includes legitimate world registries and immutable resources retained by the
conservative scan; it does not mean every use of that registry already supports co-op.
`UNSURE` records an unresolved lifetime or rule rather than deciding it silently.
Each entry gives its smallest next change. A local HUD can remain one per machine, but
must obtain its data and action identity from that machine's local player.

## How the ratchet works

All runtime C# folders below the code root are included, including presentation and audio
readers. `Editor` directories and `Tools` are excluded. Source is read once per fixture;
tokens retain source offsets, one token buffer is reused between files, and reflection reads
metadata without invoking getters or creating services, scenes or objects.

- Static fields/properties of known personal types, player-named types, conventional
  self-typed singleton members and all stored service-locator slots are inventoried.
  Reflection independently checks loaded runtime assemblies, including inherited personal
  contracts, private members, and namespace-qualified nested declaring types.
- Personal locator reads/writes are counted by member. Shared purse and licence reads
  are deliberately allowed. A new stored locator slot still requires a classification,
  even if its type has an unfamiliar name.
- Global object searches are conservatively counted, including generic, type-based,
  dynamic-type, plural, name and tag forms. A plural world-object enumeration is often
  legitimate; it is classified rather than hidden with a folder exclusion.
- Fixed relationship/sleeper identities and the equipment/bed accessors over the global
  save are included. Comments and ordinary string text are ignored; interpolation
  expressions, aliases, static imports and inactive conditional branches are examined.

Adding a new key, moving a lookup to another member or increasing its count fails with
instructions to pass the acting player through Core. Removing a finding also fails until
its single line below is deleted. Reducing repeated occurrences requires reducing that
line's count. Do not increase allowances to accommodate new personal globals. Shared
additions need an explicit ownership decision. No generated baseline acceptance is built
into the tests.

This is a bounded lexical and reflection ratchet, not a proof of all possible C# semantics.
It does not trace arbitrary wrapper calls, reflection-created references, object-typed
fields with unrelated names, event payloads without player keys, or all accesses through
local aliases of a personal service. Reflection fails on an unmapped sensitive member
rather than silently ignoring it. Future syntax needs a recognition test before use.

## Remaining design and persistence work

The existing save is version 26; this change adds no saved field, snapshot, migration or
runtime state. A later save step must map today's personal rows to the initial player and
serialize player-keyed body, clothing, skill, equipment custody, caught notes, region and
both last-bed references. It must preserve a leaving guest's held instances without
duplicating them. The separate version 27 proposal is not modified or treated as a promise
that every co-op persistence requirement is already covered.

The bed flags in `World/InteriorBed.cs` and the flat rest rows in `Core/Save/SaveData.cs`
remain semantic debt: a bed must be claimable by a particular sleeper, with a per-player
last land bed and last boat bed. These instance/data declarations are outside the static
reference patterns; `RestQuorum`, sleep identity uses and `RestLocker` below guard their
current global entry points. Skill and fitted rod parts are proposed separately and are
not present at this baseline; new global personal services will fail this ratchet.

Friendship edges need the acting player's identity even if the village graph is shared.
The daily gift cap's scope is still uncertain. The cooking runner's station/actor lifetime
and machine-local presenter bindings also need verification. None is silently approved
as shared merely because it is a singleton today.

No editor, scene, gameplay integration or full EditMode run was performed for this change.
Standalone lexical cases and an independent source-syntax comparison exercise the census;
the runtime-assembly reflection case and project compilation await CI. A passing census
does not establish playable co-op or certify scene placement.

## Pinned census by system

### Shared purse, licences and commerce

- `lookup|Economy/SellScreen.cs|SellScreen.Bind|FindAnyObjectByType<Market>` (1) -- SHARED -- Keep market state shared; resolve the market by region instead of the first loaded scene.
- `static|Core/Services/GameServices.cs|GameServices.Licenses` (1) -- SHARED -- One licence set is decided; keep it shared and check the catch against that set.
- `static|Core/Services/GameServices.cs|GameServices.Wallet` (1) -- SHARED -- One purse is decided; keep it shared and serialize debits and item handoffs on the host.
- `static|Economy/Island/IslandShelfTill.cs|LiveRows.Instance` (1) -- SHARED -- Keep licences or shelf counts shared; the host resolves each sale once and sends goods to the shopper's bag.
- `static|Economy/Island/IslandShelvesHost.cs|IslandShelvesHost._instance` (1) -- SHARED -- Keep licences or shelf counts shared; the host resolves each sale once and sends goods to the shopper's bag.
- `static|Economy/LicenseService.cs|LicenseService._instance` (1) -- SHARED -- Keep licences or shelf counts shared; the host resolves each sale once and sends goods to the shopper's bag.

### Hands, carried goods and equipment

- `accessor|Player/StowageRack.cs|StowageRack.PublishedHands` (1) -- PER PLAYER -- Give the stowage interaction its actor's hands instead of asking for published global hands.
- `lookup|Player/CamperFreezer.cs|CamperFreezer.Exchange|FindAnyObjectByType<ClamBucket>` (1) -- PER PLAYER -- Pass the interacting player's hands or carried bucket; resolve loose equipment by item identity and custody.
- `lookup|Player/CarriableBucket.cs|CarriableBucket.ResolveHands|FindAnyObjectByType<CarryHands>` (1) -- PER PLAYER -- Pass the interacting player's hands or carried bucket; resolve loose equipment by item identity and custody.
- `lookup|Player/CarriableFuelContainer.cs|CarriableFuelContainer.ResolveHands|FindAnyObjectByType<CarryHands>` (1) -- PER PLAYER -- Pass the interacting player's hands or carried bucket; resolve loose equipment by item identity and custody.
- `lookup|Player/CarriableTool.cs|CarriableTool.ResolveHands|FindAnyObjectByType<CarryHands>` (1) -- PER PLAYER -- Pass the interacting player's hands or carried bucket; resolve loose equipment by item identity and custody.
- `lookup|Player/ClamBucket.cs|ClamBucket.Awake|FindAnyObjectByType<CatchDumpInput>` (1) -- PER PLAYER -- Pass the interacting player's hands or carried bucket; resolve loose equipment by item identity and custody.
- `lookup|Player/GinnyFreezer.cs|GinnyFreezer.Exchange|FindAnyObjectByType<ClamBucket>` (1) -- PER PLAYER -- Pass the interacting player's hands or carried bucket; resolve loose equipment by item identity and custody.
- `lookup|Player/StowageRack.cs|StowageRack.FindSceneTool|FindObjectsByType<CarriableTool>` (1) -- PER PLAYER -- Pass the interacting player's hands or carried bucket; resolve loose equipment by item identity and custody.
- `lookup|Player/StowageRack.cs|StowageRack.ResolveHands|FindAnyObjectByType<CarryHands>` (1) -- PER PLAYER -- Pass the interacting player's hands or carried bucket; resolve loose equipment by item identity and custody.
- `lookup|Player/WetBucketPoint.cs|WetBucketPoint.Toggle|FindAnyObjectByType<ClamBucket>` (1) -- PER PLAYER -- Pass the interacting player's hands or carried bucket; resolve loose equipment by item identity and custody.
- `personal-save|Player/PlayerGear.cs|PlayerGear.CanDig` (1) -- PER PLAYER -- Resolve the acting player's equipment and custody; a shared purchase record must not grant a second physical rod.
- `personal-save|Player/PlayerGear.cs|PlayerGear.CanRodFish` (1) -- PER PLAYER -- Resolve the acting player's equipment and custody; a shared purchase record must not grant a second physical rod.
- `personal-save|Player/PlayerGear.cs|PlayerGear.HasBucket` (1) -- PER PLAYER -- Resolve the acting player's equipment and custody; a shared purchase record must not grant a second physical rod.
- `personal-save|Player/PlayerGear.cs|PlayerGear.HasHeadlamp` (1) -- PER PLAYER -- Resolve the acting player's equipment and custody; a shared purchase record must not grant a second physical rod.
- `personal-save|Player/PlayerGear.cs|PlayerGear.HasWatch` (1) -- PER PLAYER -- Resolve the acting player's equipment and custody; a shared purchase record must not grant a second physical rod.
- `personal-save|Player/PlayerGear.cs|PlayerGear.Owns` (1) -- PER PLAYER -- Resolve the acting player's equipment and custody; a shared purchase record must not grant a second physical rod.
- `service-use|Boats/BoatFuelTank.cs|BoatFuelTank.HeldVessel|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Core/Body/BodyService.cs|BodyService.BodyService|GameServices.CarriedLoad` (1) -- PER PLAYER -- Measure the acting player's carried load, not a process-wide carrier.
- `service-use|Core/Food/FoodEater.cs|FoodEater.FoodEater|GameServices.CatchHands` (1) -- PER PLAYER -- Pass the acting player's catch hands; transfer the physical catch once.
- `service-use|Core/Food/RecipeRunner.cs|RecipeRunner.RecipeRunner|GameServices.CatchHands` (1) -- PER PLAYER -- Pass the acting player's catch hands; transfer the physical catch once.
- `service-use|Core/Interaction/ICarriable.cs|CarriedItem.InHand|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Economy/FuelPump.cs|FuelPump.HeldVessel|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Economy/WharfSellPoint.cs|WharfSellPoint.SellOverTheCounter|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Fishing/ClamDig.cs|ClamDig.CatchHandsAvailable|GameServices.CatchHands` (1) -- PER PLAYER -- Pass the acting player's catch hands; transfer the physical catch once.
- `service-use|Fishing/ClamDig.cs|ClamDig.EnsureBucket|GameServices.PlayerHold` (1) -- PER PLAYER -- Select the acting player's carried bucket or explicitly named storage.
- `service-use|Fishing/ClamDig.cs|ClamDig.HandsAlreadyFull|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Fishing/ClamDig.cs|ClamDig.TryLandInHand|GameServices.CatchHands` (1) -- PER PLAYER -- Pass the acting player's catch hands; transfer the physical catch once.
- `service-use|Fishing/FishingController.cs|FishingController.TryLandInHand|GameServices.CatchHands` (1) -- PER PLAYER -- Pass the acting player's catch hands; transfer the physical catch once.
- `service-use|Player/CarriableBucket.cs|CarriableBucket.ResolveHands|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Player/CarriableFuelContainer.cs|CarriableFuelContainer.ResolveHands|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Player/CarriableShoppingBag.cs|CarriableShoppingBag.PickUp|GameServices.BagHands` (1) -- PER PLAYER -- Pass the shopper's bag hands; the purchase must enter his actual carried bag.
- `service-use|Player/CarriableShoppingBag.cs|CarriableShoppingBag.PutAway|GameServices.BagHands` (1) -- PER PLAYER -- Pass the shopper's bag hands; the purchase must enter his actual carried bag.
- `service-use|Player/CarriableTool.cs|CarriableTool.ResolveHands|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Player/CarryHands.cs|CarryHands.OnDestroy|GameServices.BagHands` (2) -- PER PLAYER -- Pass the shopper's bag hands; the purchase must enter his actual carried bag.
- `service-use|Player/CarryHands.cs|CarryHands.OnDestroy|GameServices.CatchHands` (2) -- PER PLAYER -- Pass the acting player's catch hands; transfer the physical catch once.
- `service-use|Player/CarryHands.cs|CarryHands.OnDestroy|GameServices.Hands` (2) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Player/CarryHands.cs|CarryHands.OnEnable|GameServices.BagHands` (1) -- PER PLAYER -- Pass the shopper's bag hands; the purchase must enter his actual carried bag.
- `service-use|Player/CarryHands.cs|CarryHands.OnEnable|GameServices.CatchHands` (1) -- PER PLAYER -- Pass the acting player's catch hands; transfer the physical catch once.
- `service-use|Player/CarryHands.cs|CarryHands.OnEnable|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Player/ClamBucket.cs|ClamBucket.CarryingAPail|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Player/ClamBucket.cs|ClamBucket.HeldCatch|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Player/ClamBucket.cs|ClamBucket.Interact|GameServices.CatchHands` (1) -- PER PLAYER -- Pass the acting player's catch hands; transfer the physical catch once.
- `service-use|Player/ClamBucket.cs|ClamBucket.OnDestroy|GameServices.PlayerHold` (2) -- PER PLAYER -- Select the acting player's carried bucket or explicitly named storage.
- `service-use|Player/ClamBucket.cs|ClamBucket.OnEnable|GameServices.PlayerHold` (1) -- PER PLAYER -- Select the acting player's carried bucket or explicitly named storage.
- `service-use|Player/DeckRiderMeshPresenter.cs|DeckRiderMeshPresenter.CarryNow|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Player/DigImpactRelay.cs|DigImpactRelay.Reader|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|Player/StowageRack.cs|StowageRack.PublishedHands|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|UI/HudController.cs|HudController.UpdateHelmFishingHint|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|World/Shops/ShelfGood.cs|ShelfGood.HasFreeHand|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|World/Shops/ShelfGood.cs|ShelfGood.Press|GameServices.BagHands` (1) -- PER PLAYER -- Pass the shopper's bag hands; the purchase must enter his actual carried bag.
- `service-use|World/WorldInteractor.cs|WorldInteractor.GatherGifts|GameServices.CatchHands` (1) -- PER PLAYER -- Pass the acting player's catch hands; transfer the physical catch once.
- `service-use|World/WorldInteractor.cs|WorldInteractor.GatherGifts|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `service-use|World/WorldInteractor.cs|WorldInteractor.GatherGifts|GameServices.PlayerHold` (1) -- PER PLAYER -- Select the acting player's carried bucket or explicitly named storage.
- `static|Core/Economy/PlayerStorage.cs|PlayerStorage._stores` (1) -- SHARED -- This is a set of world storage containers, not one carried inventory; retain stable container keys and enforce actor custody on transfers.
- `static|Core/Services/GameServices.cs|GameServices.BagHands` (1) -- PER PLAYER -- Pass the shopper's bag hands; the purchase must enter his actual carried bag.
- `static|Core/Services/GameServices.cs|GameServices.CarriedLoad` (1) -- PER PLAYER -- Measure the acting player's carried load, not a process-wide carrier.
- `static|Core/Services/GameServices.cs|GameServices.CatchHands` (1) -- PER PLAYER -- Pass the acting player's catch hands; transfer the physical catch once.
- `static|Core/Services/GameServices.cs|GameServices.Cooking` (1) -- UNSURE -- The runner binds a cooking station; decide station versus actor lifetime and pass the acting player when ingredients or hands change.
- `static|Core/Services/GameServices.cs|GameServices.Hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `static|Core/Services/GameServices.cs|GameServices.PlayerHold` (1) -- PER PLAYER -- Select the acting player's carried bucket or explicitly named storage.
- `static|Core/Services/GameServices.cs|GameServices._bagHands` (1) -- PER PLAYER -- Pass the shopper's bag hands; the purchase must enter his actual carried bag.
- `static|Core/Services/GameServices.cs|GameServices._carriedLoad` (1) -- PER PLAYER -- Measure the acting player's carried load, not a process-wide carrier.
- `static|Core/Services/GameServices.cs|GameServices._catchHands` (1) -- PER PLAYER -- Pass the acting player's catch hands; transfer the physical catch once.
- `static|Core/Services/GameServices.cs|GameServices._cooking` (1) -- UNSURE -- The runner binds a cooking station; decide station versus actor lifetime and pass the acting player when ingredients or hands change.
- `static|Core/Services/GameServices.cs|GameServices._hands` (1) -- PER PLAYER -- Pass the acting player's carrier through Core; retain custody with that player on leaving.
- `static|Core/Services/GameServices.cs|GameServices._playerHold` (1) -- PER PLAYER -- Select the acting player's carried bucket or explicitly named storage.

### Body, clothing and skill

- `service-use|Core/Body/ActivitySignalListener.cs|ActivitySignalListener.ActivitySignalListener|GameServices.Body` (1) -- PER PLAYER -- Pass the acting player's body into the operation.
- `service-use|Core/Body/BodyService.cs|BodyService.BodyService|GameServices.Needs` (1) -- PER PLAYER -- Resolve hunger, fatigue and recovery from the acting player's needs.
- `service-use|Core/Cleanliness/CleanlinessResponder.cs|CleanlinessResponder.OnFishCaught|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `service-use|Core/Cleanliness/CleanlinessResponder.cs|CleanlinessResponder.OnTrapHaul|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `service-use|Core/Cleanliness/CleanlinessResponder.cs|CleanlinessResponder.OnWater|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `service-use|Core/Food/FoodEater.cs|FoodEater.FoodEater|GameServices.Body` (1) -- PER PLAYER -- Pass the acting player's body into the operation.
- `service-use|Core/Food/FoodEater.cs|FoodEater.FoodEater|GameServices.Needs` (1) -- PER PLAYER -- Resolve hunger, fatigue and recovery from the acting player's needs.
- `service-use|Core/Kitchen/SeatSession.cs|SeatSession.Needs|GameServices.Needs` (1) -- PER PLAYER -- Resolve hunger, fatigue and recovery from the acting player's needs.
- `service-use|Core/Needs/CharacterFactors.cs|CharacterFactors.Player|GameServices.Body` (1) -- PER PLAYER -- Pass the acting player's body into the operation.
- `service-use|Core/Needs/CharacterFactors.cs|CharacterFactors.Player|GameServices.Needs` (1) -- PER PLAYER -- Resolve hunger, fatigue and recovery from the acting player's needs.
- `service-use|Core/Needs/CharacterFactors.cs|CharacterFactors.Player|GameServices.Traits` (1) -- PER PLAYER -- Resolve the acting player's traits before applying action modifiers.
- `service-use|Core/Needs/EffortGate.cs|EffortGate.Check|GameServices.Needs` (1) -- PER PLAYER -- Resolve hunger, fatigue and recovery from the acting player's needs.
- `service-use|Core/Needs/NeedsGate.cs|NeedsGate.JudgeNow|GameServices.Needs` (1) -- PER PLAYER -- Resolve hunger, fatigue and recovery from the acting player's needs.
- `service-use|Core/Needs/NeedsNotice.cs|NeedsNotice.ToneNow|GameServices.Needs` (1) -- PER PLAYER -- Resolve hunger, fatigue and recovery from the acting player's needs.
- `service-use|Core/Needs/NeedsService.cs|NeedsService.NeedsService|GameServices.Body` (1) -- PER PLAYER -- Pass the acting player's body into the operation.
- `service-use|Core/Needs/NeedsService.cs|NeedsService.NeedsService|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `service-use|Core/Needs/NeedsService.cs|NeedsService.NeedsService|GameServices.Traits` (1) -- PER PLAYER -- Resolve the acting player's traits before applying action modifiers.
- `service-use|Core/Save/RestSaveResponder.cs|RestSaveResponder.Fulfil|GameServices.Needs` (1) -- PER PLAYER -- Resolve hunger, fatigue and recovery from the acting player's needs.
- `service-use|Core/Save/SaveService.cs|SaveService.SnapshotLiveState|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `service-use|Core/Save/SleepFlow.cs|SleepFlow.Wake|GameServices.Needs` (1) -- PER PLAYER -- Resolve hunger, fatigue and recovery from the acting player's needs.
- `service-use|Environment/GameClock.cs|GameClock.TickNeeds|GameServices.Needs` (1) -- PER PLAYER -- Resolve hunger, fatigue and recovery from the acting player's needs.
- `service-use|Fishing/FishingController.cs|FishingController.WithBiteWaitTrait|GameServices.Traits` (1) -- PER PLAYER -- Resolve the acting player's traits before applying action modifiers.
- `service-use|Player/DeckRiderMeshPresenter.cs|DeckRiderMeshPresenter.OnEnable|GameServices.Appearance` (1) -- PER PLAYER -- Bind appearance to the acting player; keep worn choices in his record.
- `service-use|Player/StinkLines.cs|StinkLinesPresenter.LateUpdate|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `service-use|UI/CharacterCreatorModel.cs|CharacterCreatorModel.CharacterCreatorModel|GameServices.Appearance` (1) -- PER PLAYER -- Bind appearance to the acting player; keep worn choices in his record.
- `service-use|UI/CharacterCreatorModel.cs|CharacterCreatorModel.CharacterCreatorModel|GameServices.Traits` (1) -- PER PLAYER -- Resolve the acting player's traits before applying action modifiers.
- `service-use|UI/CharacterCreatorOpener.cs|CharacterCreatorOpener.OnRequested|GameServices.Appearance` (1) -- PER PLAYER -- Bind appearance to the acting player; keep worn choices in his record.
- `service-use|World/Washrooms/SitPoint.cs|SitPoint.Def|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `service-use|World/Washrooms/WashStationPoint.cs|WashStationPoint.Def|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `service-use|World/Washrooms/WashStationPoint.cs|WashStationPoint.IsWashingHere|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `service-use|World/Washrooms/WashStationPoint.cs|WashStationPoint.Stop|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `service-use|World/Washrooms/WashStationPoint.cs|WashStationPoint.Toggle|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `service-use|World/Washrooms/WashStationPoint.cs|WashStationPoint.Watch|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `static|Core/Services/GameServices.cs|GameServices.Appearance` (1) -- PER PLAYER -- Bind appearance to the acting player; keep worn choices in his record.
- `static|Core/Services/GameServices.cs|GameServices.Body` (1) -- PER PLAYER -- Pass the acting player's body into the operation.
- `static|Core/Services/GameServices.cs|GameServices.Cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `static|Core/Services/GameServices.cs|GameServices.Food` (1) -- PER PLAYER -- Give the eating action its player and his held item; debit that item once.
- `static|Core/Services/GameServices.cs|GameServices.Needs` (1) -- PER PLAYER -- Resolve hunger, fatigue and recovery from the acting player's needs.
- `static|Core/Services/GameServices.cs|GameServices.PlayerStrength` (1) -- PER PLAYER -- Read the acting player's strength when computing work.
- `static|Core/Services/GameServices.cs|GameServices.Traits` (1) -- PER PLAYER -- Resolve the acting player's traits before applying action modifiers.
- `static|Core/Services/GameServices.cs|GameServices._appearance` (1) -- PER PLAYER -- Bind appearance to the acting player; keep worn choices in his record.
- `static|Core/Services/GameServices.cs|GameServices._body` (1) -- PER PLAYER -- Pass the acting player's body into the operation.
- `static|Core/Services/GameServices.cs|GameServices._cleanliness` (1) -- PER PLAYER -- Resolve dirt and washing against the acting player's cleanliness.
- `static|Core/Services/GameServices.cs|GameServices._food` (1) -- PER PLAYER -- Give the eating action its player and his held item; debit that item once.
- `static|Core/Services/GameServices.cs|GameServices._needs` (1) -- PER PLAYER -- Resolve hunger, fatigue and recovery from the acting player's needs.
- `static|Core/Services/GameServices.cs|GameServices._playerStrength` (1) -- PER PLAYER -- Read the acting player's strength when computing work.
- `static|Core/Services/GameServices.cs|GameServices._traits` (1) -- PER PLAYER -- Resolve the acting player's traits before applying action modifiers.

### Dory, boarding and helm

- `lookup|App/PersistentHoldProxy.cs|PersistentHoldProxy.Resolve|FindAnyObjectByType<ShipHold>` (1) -- PER PLAYER -- Select the shared hull by stable hull identity or this player's boarding claim; never take an arbitrary boat.
- `lookup|Boats/AmbientFleetPresenter.cs|AmbientFleetPresenter.EvaluateGate|FindAnyObjectByType<BoatController>` (1) -- PER PLAYER -- Select the shared hull by stable hull identity or this player's boarding claim; never take an arbitrary boat.
- `service-use|Art/BoatSpotlight.cs|BoatSpotlight.PlayerSwitchesThisBeam|GameServices.Helm` (1) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `service-use|Audio/AudioDirector.cs|AudioDirector.SampleBoat|GameServices.ActiveBoat` (1) -- PER PLAYER -- Resolve which hull this player is aboard; the dory itself stays shared.
- `service-use|Boats/ActiveBoatProbe.cs|ActiveBoatProbe.OnDisable|GameServices.ActiveBoat` (2) -- PER PLAYER -- Resolve which hull this player is aboard; the dory itself stays shared.
- `service-use|Boats/ActiveBoatProbe.cs|ActiveBoatProbe.OnEnable|GameServices.ActiveBoat` (1) -- PER PLAYER -- Resolve which hull this player is aboard; the dory itself stays shared.
- `service-use|Boats/AnchorInput.cs|AnchorInput.IsThePlayersBoat|GameServices.Helm` (1) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `service-use|Boats/BoatBunk.cs|BoatBunk.IsPlayersBoat|GameServices.Helm` (1) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `service-use|Boats/BoatCutaway.cs|BoatCutaway.PlayerIsAtThisHelm|GameServices.Helm` (1) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `service-use|Boats/BoatFuelTank.cs|BoatFuelTank.OnDisable|GameServices.ActiveBoatFuel` (2) -- PER PLAYER -- Select the hull through the acting player; fuel remains shared hull state.
- `service-use|Boats/BoatFuelTank.cs|BoatFuelTank.OnEnable|GameServices.ActiveBoatFuel` (1) -- PER PLAYER -- Select the hull through the acting player; fuel remains shared hull state.
- `service-use|Boats/DevBoatInput.cs|DevBoatInput.RowingStationManned|GameServices.Helm` (1) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `service-use|Boats/HelmControlRelay.cs|HelmControlRelay.IsPlayerHelm|GameServices.Helm` (1) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `service-use|Boats/HelmControlRelay.cs|HelmControlRelay.IsPlayersBoat|GameServices.Helm` (1) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `service-use|Boats/HelmControlRelay.cs|HelmControlRelay.OnDisable|GameServices.Helm` (1) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `service-use|Boats/HelmControlRelay.cs|HelmControlRelay.OnEnable|GameServices.Helm` (1) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `service-use|Core/Nav/NavTrackRecorder.cs|NavTrackRecorder.Tick|GameServices.HelmInstruments` (1) -- PER PLAYER -- Select the named hull's instruments through this player's view or helm claim.
- `service-use|Economy/FuelPump.cs|FuelPump.TargetVessel|GameServices.ActiveBoatFuel` (1) -- PER PLAYER -- Select the hull through the acting player; fuel remains shared hull state.
- `service-use|Economy/StallReach.cs|StallReach.Enable|GameServices.ActiveBoat` (1) -- PER PLAYER -- Resolve which hull this player is aboard; the dory itself stays shared.
- `service-use|Player/ControlSwitcher.cs|ControlSwitcher.OnDestroy|GameServices.Helm` (4) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `service-use|Player/ControlSwitcher.cs|ControlSwitcher.PublishBoatOwnership|GameServices.Helm` (2) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `service-use|UI/ChartplotterOverlayHost.cs|ChartplotterOverlayHost.BuildState|GameServices.ActiveBoat` (1) -- PER PLAYER -- Resolve which hull this player is aboard; the dory itself stays shared.
- `service-use|UI/ChartplotterOverlayHost.cs|ChartplotterOverlayHost.Update|GameServices.HelmInstruments` (1) -- PER PLAYER -- Select the named hull's instruments through this player's view or helm claim.
- `service-use|UI/FishFinderOverlayHost.cs|FishFinderOverlayHost.Update|GameServices.HelmInstruments` (1) -- PER PLAYER -- Select the named hull's instruments through this player's view or helm claim.
- `service-use|UI/HelmOverlayHost.cs|HelmOverlayHost.Glance|GameServices.HelmInstruments` (1) -- PER PLAYER -- Select the named hull's instruments through this player's view or helm claim.
- `service-use|UI/HelmOverlayHost.cs|HelmOverlayHost.SampleHeadingDegrees|GameServices.ActiveBoat` (1) -- PER PLAYER -- Resolve which hull this player is aboard; the dory itself stays shared.
- `service-use|UI/HelmOverlayHost.cs|HelmOverlayHost.Update|GameServices.HelmControl` (1) -- PER PLAYER -- Bind input to the player holding the hull's exclusive helm claim.
- `service-use|UI/HelmOverlayHost.cs|HelmOverlayHost.Update|GameServices.HelmInstruments` (1) -- PER PLAYER -- Select the named hull's instruments through this player's view or helm claim.
- `service-use|UI/HudController.cs|HudController.UpdateNavReads|GameServices.ActiveBoat` (1) -- PER PLAYER -- Resolve which hull this player is aboard; the dory itself stays shared.
- `service-use|UI/HudController.cs|HudController.UpdateNavReads|GameServices.HelmControl` (1) -- PER PLAYER -- Bind input to the player holding the hull's exclusive helm claim.
- `service-use|UI/RadarOverlayHost.cs|RadarOverlayHost.BuildState|GameServices.ActiveBoat` (1) -- PER PLAYER -- Resolve which hull this player is aboard; the dory itself stays shared.
- `service-use|UI/RadarOverlayHost.cs|RadarOverlayHost.Update|GameServices.HelmInstruments` (1) -- PER PLAYER -- Select the named hull's instruments through this player's view or helm claim.
- `service-use|UI/SounderOverlayHost.cs|SounderOverlayHost.Update|GameServices.HelmInstruments` (1) -- PER PLAYER -- Select the named hull's instruments through this player's view or helm claim.
- `service-use|World/Campaign/CampaignBarrierDirector.cs|CampaignBarrierDirector.Step|GameServices.Helm` (2) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `static|Core/Services/GameServices.cs|GameServices.ActiveBoat` (1) -- PER PLAYER -- Resolve which hull this player is aboard; the dory itself stays shared.
- `static|Core/Services/GameServices.cs|GameServices.ActiveBoatFuel` (1) -- PER PLAYER -- Select the hull through the acting player; fuel remains shared hull state.
- `static|Core/Services/GameServices.cs|GameServices.Helm` (1) -- PER PLAYER -- Use one exclusive helm claim per shared hull plus a separate occupant set.
- `static|Core/Services/GameServices.cs|GameServices.HelmControl` (1) -- PER PLAYER -- Bind input to the player holding the hull's exclusive helm claim.
- `static|Core/Services/GameServices.cs|GameServices.HelmInstruments` (1) -- PER PLAYER -- Select the named hull's instruments through this player's view or helm claim.
- `static|Player/ControlSwitcher.cs|ControlSwitcher._helmFallbackLogged` (1) -- UNSURE -- This static flag suppresses a fallback warning, not a helm claim; it may stay diagnostic-only, but must never select a player.

### Position, regions and arrival

- `lookup|App/ArrivalOpening.Sea.cs|ArrivalOpening.PlaceLensOnPassenger|FindFirstObjectByType<CameraFollow>` (1) -- PER PLAYER -- Bind the intended player or local camera explicitly instead of choosing the first scene object or its name.
- `lookup|App/LandPassageAccess.cs|LandPassageAccess.CanTravel|FindFirstObjectByType<ControlSwitcher>` (1) -- PER PLAYER -- Bind the intended player or local camera explicitly instead of choosing the first scene object or its name.
- `lookup|App/RegionTravelCoordinator.cs|RegionTravelCoordinator.ArriveAcrossTheSeam|FindFirstObjectByType<CameraFollow>` (1) -- PER PLAYER -- Bind the intended player or local camera explicitly instead of choosing the first scene object or its name.
- `lookup|App/RegionTravelCoordinator.cs|RegionTravelCoordinator.Loader|FindAnyObjectByType<RegionSceneLoader>` (1) -- SHARED -- Keep a session scene loader, but request loads and arrivals with a player key and explicit region.
- `lookup|Art/PlayerOutfitVisual.cs|PlayerOutfitInstaller.Update|Find` (1) -- PER PLAYER -- Bind the intended player or local camera explicitly instead of choosing the first scene object or its name.
- `lookup|Art/PlayerShadowInstaller.cs|PlayerShadowInstaller.Update|Find` (1) -- PER PLAYER -- Bind the intended player or local camera explicitly instead of choosing the first scene object or its name.
- `lookup|Art/PlayerSilhouetteInstaller.cs|PlayerSilhouetteInstaller.Update|Find` (1) -- PER PLAYER -- Bind the intended player or local camera explicitly instead of choosing the first scene object or its name.
- `lookup|Art/PlayerSubmergeVisual.cs|PlayerSubmergeInstaller.Update|Find` (1) -- PER PLAYER -- Bind the intended player or local camera explicitly instead of choosing the first scene object or its name.
- `lookup|Art/WadeSplashEmitter.cs|WadeSplashEmitter.ResolvePlayer|Find` (1) -- PER PLAYER -- Bind the intended player or local camera explicitly instead of choosing the first scene object or its name.
- `lookup|Art/WalkerLights.cs|WalkerLights.FindWalker|Find` (1) -- PER PLAYER -- Bind the intended player or local camera explicitly instead of choosing the first scene object or its name.
- `lookup|Economy/StallReach.cs|StallReach.ResolvePlayer|Find` (1) -- PER PLAYER -- Bind the intended player or local camera explicitly instead of choosing the first scene object or its name.
- `service-use|App/ArrivalOpening.Sea.cs|ArrivalOpening.BeginSeaHop|GameServices.PendingArrivalKey` (2) -- PER PLAYER -- Keep the destination entry key on the travelling player's transition.
- `service-use|App/ArrivalOpening.cs|ArrivalOpening.Spawn|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|App/CameraFollow.cs|CameraFollow.Bounds|GameServices.CurrentRegionBounds` (1) -- PER PLAYER -- Resolve bounds for the queried player's region; bind the local view separately.
- `service-use|App/RegionAnchor.cs|RegionAnchor.ConfigureExtent|GameServices.CurrentRegionBounds` (1) -- PER PLAYER -- Resolve bounds for the queried player's region; bind the local view separately.
- `service-use|App/RegionAnchor.cs|RegionAnchor.OnDisable|GameServices.CurrentRegionBounds` (2) -- PER PLAYER -- Resolve bounds for the queried player's region; bind the local view separately.
- `service-use|App/RegionAnchor.cs|RegionAnchor.OnDisable|GameServices.CurrentRegionId` (2) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|App/RegionAnchor.cs|RegionAnchor.OnEnable|GameServices.CurrentRegionBounds` (1) -- PER PLAYER -- Resolve bounds for the queried player's region; bind the local view separately.
- `service-use|App/RegionAnchor.cs|RegionAnchor.OnEnable|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|App/RegionTravelCoordinator.cs|RegionTravelCoordinator.BeginCrossing|GameServices.PendingArrivalKey` (1) -- PER PLAYER -- Keep the destination entry key on the travelling player's transition.
- `service-use|App/RegionTravelCoordinator.cs|RegionTravelCoordinator.FixedUpdate|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|App/RegionTravelCoordinator.cs|RegionTravelCoordinator.OnActiveSceneChanged|GameServices.ConsumePendingArrivalKey` (1) -- PER PLAYER -- Consume only the travelling player's pending entry key.
- `service-use|App/RegionTravelCoordinator.cs|RegionTravelCoordinator.OnDestroy|GameServices.PlayerTransform` (2) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|App/RegionTravelCoordinator.cs|RegionTravelCoordinator.OnDestroy|GameServices.Whereabouts` (2) -- PER PLAYER -- Query whereabouts by player key so the guest can occupy another region.
- `service-use|App/RegionTravelCoordinator.cs|RegionTravelCoordinator.OnEnable|GameServices.Whereabouts` (1) -- PER PLAYER -- Query whereabouts by player key so the guest can occupy another region.
- `service-use|App/RegionTravelCoordinator.cs|RegionTravelCoordinator.Player|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|App/RegionTravelCoordinator.cs|RegionTravelCoordinator.PublishPlayer|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|App/RegionTravelCoordinator.cs|RegionTravelCoordinator.RestoreBoat|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Art/GroundPrintRenderer.cs|GroundPrintRenderer.Build|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Art/GroundPrintRenderer.cs|GroundPrintRenderer.CheckRegion|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Art/GullFlock.cs|GullFlock.RefreshSchedule|GameServices.CurrentRegionBounds` (1) -- PER PLAYER -- Resolve bounds for the queried player's region; bind the local view separately.
- `service-use|Art/GullFlock.cs|GullFlock.RefreshSchedule|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Art/InteractAffordancePresenter.cs|InteractAffordancePresenter.ResolveTarget|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|Art/MoodGradeDirector.cs|MoodGradeDirector.Tick|GameServices.CurrentRegionId` (2) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Art/WalkerLights.cs|WalkerLights.FindWalker|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|Boats/DeckIceBox.cs|DeckIceBox.IsAvailable|GameServices.PlayerTransform` (2) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|Core/Cleanliness/CleanlinessResponder.cs|CleanlinessResponder.SayNoSoap|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|Core/Food/FoodEater.cs|FoodEater.FoodEater|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Core/Food/FoodEater.cs|FoodEater.PlayerPosition|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|Core/Iso/CharacterFigureLookTargets.cs|PlayerLookTarget.TryGetLookTarget|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|Core/Nav/NavTrackRecorder.cs|NavTrackRecorder.Tick|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Core/Needs/EffortGate.cs|EffortGate.Say|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|Core/Save/RestSaveResponder.cs|RestSaveResponder.Write|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Core/Save/RestWakeRestorer.cs|RestWakeRestorer.Wake|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Core/Save/RestWakeRestorer.cs|RestWakeRestorer.Wake|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|Environment/DugHolePresenter.cs|DugHolePresenter.OnMoment|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Environment/DugHolePresenter.cs|DugHolePresenter.Update|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Fishing/FishingController.cs|FishingController.EffectiveRegionId|GameServices.CurrentRegionId` (2) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Fishing/IFishSchoolWorld.cs|LiveFishSchoolWorld.RegionId|GameServices.CurrentRegionId` (2) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Player/CarriableShoppingBag.cs|CarriableShoppingBag.StandsHere|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Player/CarryHands.ShoppingBag.cs|CarryHands.RestoreBag|GameServices.CurrentRegionId` (2) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Player/CarryHands.ShoppingBag.cs|CarryHands.SetBagDown|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|Player/ChestFreezerArt.cs|ChestFreezerArt.LateUpdate|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|Player/CookingStationFixture.cs|CookingStationFixture.Say|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|Player/FreezerContentsPresenter.cs|FreezerContentsPresenter.LateUpdate|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|UI/CharacterCreatorPanel.cs|CharacterCreatorPanel.CaptureWorld|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|UI/CharacterCreatorPanel.cs|CharacterCreatorPanel.LateUpdate|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|UI/ChartplotterOverlayHost.cs|ChartplotterOverlayHost.CommitName|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|UI/ChartplotterOverlayHost.cs|ChartplotterOverlayHost.Update|GameServices.CurrentRegionBounds` (1) -- PER PLAYER -- Resolve bounds for the queried player's region; bind the local view separately.
- `service-use|UI/ChartplotterOverlayHost.cs|ChartplotterOverlayHost.Update|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|UI/RadarOverlayHost.cs|RadarOverlayHost.Update|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|UI/TidePanel.cs|TidePanel.BuildHeader|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|UI/WardrobePicker.cs|WardrobePicker.AnchorFallback|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|UI/WardrobePicker.cs|WardrobePicker.PlayerHasLeft|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|World/BuildingInterior.cs|BuildingInterior.ResolveOccupant|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|World/Campaign/CampaignBarrierDirector.cs|CampaignBarrierDirector.FindStanding|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|World/Campaign/CampaignBarrierDirector.cs|CampaignBarrierDirector.Step|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|World/Campaign/CampaignBarrierDirector.cs|CampaignBarrierDirector.Step|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|World/Conversations/NpcConversationDirector.cs|NpcConversationDirector.AnyAuthoredForCurrentRegion|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|World/Conversations/NpcConversationDirector.cs|NpcConversationDirector.TryStartOne|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|World/InnerVoiceDirector.cs|InnerVoiceDirector.TrySpeak|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|World/InnerVoiceDirector.cs|InnerVoiceDirector.Update|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|World/InnerVoiceDirector.cs|InnerVoiceDirector.Update|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|World/RegionPassage.cs|RegionPassage.Activate|GameServices.PendingArrivalKey` (2) -- PER PLAYER -- Keep the destination entry key on the travelling player's transition.
- `service-use|World/RegionPassage.cs|RegionPassage.OnTriggerEnter2D|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `service-use|World/Washrooms/SitPoint.cs|SitPoint.Update|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|World/Washrooms/WashStationPoint.cs|WashStationPoint.Update|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `service-use|World/WorldInteractor.cs|WorldInteractor.ResolvePlayer|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `static|Core/Iso/CharacterFigureLookTargets.cs|PlayerLookTarget.Instance` (1) -- PER PLAYER -- Supply an explicit target or player selection policy to each figure instead of one default player transform.
- `static|Core/Services/GameServices.cs|GameServices.CurrentRegionBounds` (1) -- PER PLAYER -- Resolve bounds for the queried player's region; bind the local view separately.
- `static|Core/Services/GameServices.cs|GameServices.CurrentRegionId` (1) -- PER PLAYER -- Pass the queried player's region, or an explicit region for world simulation.
- `static|Core/Services/GameServices.cs|GameServices.PendingArrivalKey` (1) -- PER PLAYER -- Keep the destination entry key on the travelling player's transition.
- `static|Core/Services/GameServices.cs|GameServices.PlayerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `static|Core/Services/GameServices.cs|GameServices.Whereabouts` (1) -- PER PLAYER -- Query whereabouts by player key so the guest can occupy another region.
- `static|Core/Services/GameServices.cs|GameServices._playerTransform` (1) -- PER PLAYER -- Pass the intended player transform; keep a separate local-view binding where needed.
- `static|Core/World/PlayerStorey.cs|PlayerStorey.Level` (1) -- PER PLAYER -- Store building and floor per player; query the interacting or viewed player's storey.
- `static|Core/World/PlayerStorey.cs|PlayerStorey._building` (1) -- PER PLAYER -- Store building and floor per player; query the interacting or viewed player's storey.

### Beds, sleep and host authority

- `identity-use|Core/Save/SleepFlow.cs|SleepFlow.Begin|RestQuorum.LocalSleeper` (1) -- PER PLAYER -- Replace the fixed sleeper number with the acting player key; retain exclusive bed claims.
- `identity-use|Core/Save/SleepFlow.cs|SleepFlow.Release|RestQuorum.LocalSleeper` (1) -- PER PLAYER -- Replace the fixed sleeper number with the acting player key; retain exclusive bed claims.
- `identity-use|Core/Save/SleepFlow.cs|SleepFlow.Wake|RestQuorum.LocalSleeper` (1) -- PER PLAYER -- Replace the fixed sleeper number with the acting player key; retain exclusive bed claims.
- `identity|Core/Save/RestQuorum.cs|RestQuorum.LocalSleeper` (1) -- PER PLAYER -- Replace the fixed sleeper number with the acting player key; retain exclusive bed claims.
- `lookup|App/ShellRestart.cs|ShellRestart.DestroyPersistentCore|FindObjectsByType<PersistentObject>` (1) -- SHARED -- Session teardown may enumerate persistent objects; only host session shutdown should end every player's world.
- `personal-save|Core/Save/RestLocker.cs|RestLocker.Anchor` (1) -- PER PLAYER -- Take the player record explicitly; retain his last land bed and last boat bed separately.
- `personal-save|Core/Save/RestLocker.cs|RestLocker.Place` (1) -- PER PLAYER -- Take the player record explicitly; retain his last land bed and last boat bed separately.
- `static|App/ShellRestart.cs|ShellRestart._instance` (1) -- SHARED -- Keep session save and shutdown authority on the host; a guest leaving must retain his held things in his record.
- `static|Core/Save/RestQuorum.cs|RestQuorum.Occupants` (1) -- SHARED -- The bed-to-sleeper claim map is correctly shared; use real player keys and a session roster for quorum.
- `static|Core/Save/RestQuorum.cs|RestQuorum.Players` (1) -- PER PLAYER -- Replace the literal player count with the present-player roster; require each player in a separate bed.
- `static|Core/Save/SaveService.cs|SaveService._instance` (1) -- SHARED -- Keep session save and shutdown authority on the host; a guest leaving must retain his held things in his record.
- `static|Core/Services/GameServices.cs|GameServices.Clock` (1) -- SHARED -- World time is shared; restrict pause and authoritative clock changes to the host.
- `static|Core/Services/GameServices.cs|GameServices.Save` (1) -- SHARED -- Only the host writes the session save; later persistence must serialize player-keyed records.
- `static|Core/Shell/ShellPause.cs|ShellPause.IsPaused` (1) -- SHARED -- World pause is shared but host-only; route guest menus to local input suppression instead.
- `static|Core/Shell/ShellPause.cs|ShellPause._clockWasPaused` (1) -- SHARED -- World pause is shared but host-only; route guest menus to local input suppression instead.
- `static|Core/Shell/ShellPause.cs|ShellPause._frozeClock` (1) -- SHARED -- World pause is shared but host-only; route guest menus to local input suppression instead.

### Notebook, gifts and relationships

- `identity-use|World/WorldInteractor.cs|WorldInteractor.Give|RelationshipGraph.PlayerId` (1) -- PER PLAYER -- Pass the acting player's relationship identity; the graph may remain a shared registry of separately keyed edges.
- `identity|Core/Relationships/RelationshipGraph.cs|RelationshipGraph.PlayerId` (1) -- PER PLAYER -- Pass the acting player's relationship identity; the graph may remain a shared registry of separately keyed edges.
- `static|Core/Fishing/CatchRecord.cs|CatchRecords.Log` (1) -- PER PLAYER -- Keep a catch log per player; carry the acting player identity on catch events and record it once.
- `static|Core/Services/GameServices.cs|GameServices.Relationships` (1) -- SHARED -- The graph can be shared because its edges carry identities; replace fixed player identity at callers, not the whole graph.
- `static|Core/Services/GameServices.cs|GameServices._relationships` (1) -- SHARED -- The graph can be shared because its edges carry identities; replace fixed player identity at callers, not the whole graph.
- `static|World/Gifts/GiftLedger.cs|GiftLedger.Shared` (1) -- UNSURE -- The ledger keys the session and recipient only. Decide whether the daily gift cap is per giver or per recipient before adding a player key.

### Local presentation and menus

- `lookup|Audio/AudioDirector.cs|AudioDirector.ListenerPosition|FindObjectsByType<AudioListener>` (1) -- UNSURE -- Likely a local-view capture or listener; explicitly scope it to the local player and verify that remote bodies are excluded.
- `lookup|UI/CharacterCreatorPanel.cs|CharacterCreatorPanel.CaptureWorld|FindObjectsByType<Canvas>` (1) -- UNSURE -- Likely a local-view capture or listener; explicitly scope it to the local player and verify that remote bodies are excluded.
- `lookup|UI/CharacterCreatorPanel.cs|CharacterCreatorPanel.LateUpdate|FindObjectsByType<Renderer>` (1) -- UNSURE -- Likely a local-view capture or listener; explicitly scope it to the local player and verify that remote bodies are excluded.
- `lookup|World/AmbientSpeechPresenter.cs|AmbientSpeechPresenter.ResolveModal|FindAnyObjectByType<DialoguePresenter>` (1) -- PER PLAYER -- Bind speech and dialogue to the addressed player's view; avoid a global first presenter.
- `lookup|World/ArrivalLineSpeaker.cs|ArrivalLineSpeaker.OnArrived|FindAnyObjectByType<DialoguePresenter>` (1) -- PER PLAYER -- Bind speech and dialogue to the addressed player's view; avoid a global first presenter.
- `static|Art/InteractAffordancePresenter.cs|InteractAffordancePresenter.Instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|Art/InteractAffordancePresenter.cs|InteractAffordancePresenter._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|Art/MoodGradeDirector.cs|MoodGradeDirector.Instance` (1) -- UNSURE -- This singleton may be a machine-local presenter; verify its listener, region and player binding before treating its mutable state as shared.
- `static|Art/PlayerOutfitVisual.cs|PlayerOutfitInstaller._installed` (1) -- PER PLAYER -- The installation flag represents one body; track installed bodies separately while retaining shared immutable resources.
- `static|Art/PlayerShadowInstaller.cs|PlayerShadowInstaller._installed` (1) -- PER PLAYER -- The installation flag represents one body; track installed bodies separately while retaining shared immutable resources.
- `static|Art/PlayerSilhouetteInstaller.cs|PlayerSilhouetteInstaller._installed` (1) -- PER PLAYER -- The installation flag represents one body; track installed bodies separately while retaining shared immutable resources.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual._installed` (1) -- PER PLAYER -- The installation flag represents one body; track installed bodies separately while retaining shared immutable resources.
- `static|Art/SquallVeil.cs|SquallVeil.Instance` (1) -- UNSURE -- This singleton may be a machine-local presenter; verify its listener, region and player binding before treating its mutable state as shared.
- `static|Audio/AudioDirector.cs|AudioDirector._instance` (1) -- UNSURE -- This singleton may be a machine-local presenter; verify its listener, region and player binding before treating its mutable state as shared.
- `static|Audio/FishingAudio.cs|FishingAudio._instance` (1) -- UNSURE -- This singleton may be a machine-local presenter; verify its listener, region and player binding before treating its mutable state as shared.
- `static|Core/Services/GameServices.cs|GameServices.OpeningCinematicRunning` (1) -- PER PLAYER -- Bind this view or transition state to the local player; it must not drive another player's arrival or input.
- `static|Core/Services/GameServices.cs|GameServices.SeamCoverAlpha` (1) -- PER PLAYER -- Bind this view or transition state to the local player; it must not drive another player's arrival or input.
- `static|Core/Services/GameServices.cs|GameServices.WorldUnitsPerRenderedPixel` (1) -- PER PLAYER -- Bind this view or transition state to the local player; it must not drive another player's arrival or input.
- `static|Economy/CatalogBookPresenter.cs|CatalogBookPresenter._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|Economy/SellScreen.cs|SellScreen._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/BoatUiWindowInput.cs|BoatUiWindowInput._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/CharacterCreatorOpener.cs|CharacterCreatorOpener._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/ChartplotterOverlayHost.cs|ChartplotterOverlayHost.Instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/ChartplotterOverlayHost.cs|ChartplotterOverlayHost._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/FishFinderOverlayHost.cs|FishFinderOverlayHost.Instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/FishFinderOverlayHost.cs|FishFinderOverlayHost._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/HelmOverlayHost.cs|HelmOverlayHost.Instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/HelmOverlayHost.cs|HelmOverlayHost._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/InteractChooserView.cs|InteractChooserView.Instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/InteractChooserView.cs|InteractChooserView._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/InteractPopup.cs|InteractPopup.Instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/InteractPopup.cs|InteractPopup._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/NavTrackRecorderHost.cs|NavTrackRecorderHost._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/RadarOverlayHost.cs|RadarOverlayHost.Instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/RadarOverlayHost.cs|RadarOverlayHost._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/RegionFadeOverlay.cs|RegionFadeOverlay._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/Shell/PauseMenu.cs|PauseMenu._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/Shell/SettingsSheet.cs|SettingsSheet._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/Shell/ShellPresenter.cs|ShellPresenter._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/Shell/TitleScreen.cs|TitleScreen._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/SounderOverlayHost.cs|SounderOverlayHost.Instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/SounderOverlayHost.cs|SounderOverlayHost._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/TidePanel.cs|TidePanel._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/WardrobePicker.cs|WardrobePicker.Instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|UI/WardrobePicker.cs|WardrobePicker._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|World/AmbientSpeechPresenter.cs|AmbientSpeechPresenter.Instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|World/NotebookPresenter.cs|NotebookPresenter.Instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.
- `static|World/NotebookPresenter.cs|NotebookPresenter._instance` (1) -- PER PLAYER -- Keep a local view instance bound to an explicit player; all actions and data reads must use that binding.

### Shared resources and world registries

- `lookup|App/NpcPotBoats.cs|NpcPotBoats.BindFromLibrary|FindObjectsByType<MooredBoat>` (1) -- SHARED -- This locates world moorings; scope the enumeration to the requested region when two regions are live.
- `lookup|Art/GrassField.cs|GrassField.GatherAnchors|FindObjectsByType<SpriteRenderer>` (1) -- SHARED -- These are world objects, not one person; preserve the set and pass a region or interaction origin when selecting from it.
- `lookup|Art/TerrainShoreLineBinding.cs|TerrainShoreLineBinding.CaptureFallback|FindObjectsByType<CliffWallSurface>` (1) -- SHARED -- These are world objects, not one person; preserve the set and pass a region or interaction origin when selecting from it.
- `lookup|Art/TerrainShoreLineBinding.cs|TerrainShoreLineBinding.CaptureWalls|FindObjectsByType<CliffWallSurface>` (1) -- SHARED -- These are world objects, not one person; preserve the set and pass a region or interaction origin when selecting from it.
- `lookup|Art/WaterSurface.cs|WaterSurface.ResolveLandTilemaps|FindObjectsByType<Tilemap>` (1) -- SHARED -- These are world objects, not one person; preserve the set and pass a region or interaction origin when selecting from it.
- `lookup|Art/WaterSurface.cs|WaterSurface.ResolveShoreFence|FindObjectsByType<EdgeCollider2D>` (1) -- SHARED -- These are world objects, not one person; preserve the set and pass a region or interaction origin when selecting from it.
- `lookup|Boats/BoatWakeEmitter.cs|BoatWakeEmitter.BuildTierSprites|FindObjectsByType<BoatController>` (1) -- SHARED -- This enumerates hulls to prepare shared foam resources; keep enumeration, not first-player selection.
- `lookup|Economy/BuyCatalog.cs|BuyCatalog.Find|FindObjectsByType<T>` (1) -- UNSURE -- A generic scene enumeration can contain personal objects; require an explicit region and actor or custody predicate before selecting one.
- `lookup|Economy/CounterSellDesk.cs|CounterSellDesk.BuyOnly|FindObjectsByType<WharfSellPoint>` (1) -- SHARED -- These are world objects, not one person; preserve the set and pass a region or interaction origin when selecting from it.
- `lookup|Fishing/ClamDigger.cs|ClamDigger.NearestDiggable|FindObjectsByType<ClamDig>` (1) -- SHARED -- These are world objects, not one person; preserve the set and pass a region or interaction origin when selecting from it.
- `lookup|Player/CatchDumpInput.cs|CatchDumpInput.DumpSpoiledEverywhere|FindObjectsByType<MonoBehaviour>` (1) -- UNSURE -- A generic scene enumeration can contain personal objects; require an explicit region and actor or custody predicate before selecting one.
- `lookup|World/Beach/BeachLifeDirector.Gatherings.cs|BeachLifeDirector.RefreshBodies|FindObjectsByType<VillagerRoutine>` (1) -- SHARED -- These are world objects, not one person; preserve the set and pass a region or interaction origin when selecting from it.
- `lookup|World/Beach/BeachLifeDirector.cs|BeachLifeDirector.RankOf|FindObjectsByType<VillagerRoutine>` (1) -- SHARED -- These are world objects, not one person; preserve the set and pass a region or interaction origin when selecting from it.
- `lookup|World/Conversations/NpcConversationDirector.cs|NpcConversationDirector.RebuildRoster|FindObjectsByType<RoutineStations>` (1) -- SHARED -- These are world objects, not one person; preserve the set and pass a region or interaction origin when selecting from it.
- `lookup|World/Conversations/NpcConversationDirector.cs|NpcConversationDirector.RebuildRoster|FindObjectsByType<VillagerRoutine>` (1) -- SHARED -- These are world objects, not one person; preserve the set and pass a region or interaction origin when selecting from it.
- `lookup|World/Shops/SoldOutSignsDirector.cs|SoldOutSignsDirector.HangAll|FindObjectsByType<BuildingInterior>` (1) -- SHARED -- These are world objects, not one person; preserve the set and pass a region or interaction origin when selecting from it.
- `static|App/CameraFollow.cs|CameraFollow.ZoomSettings` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|App/NpcPotBoats.cs|NpcPotBoats.Instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|App/NpcPotBoats.cs|NpcPotBoats._instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|App/NpcTrawlBoats.cs|NpcTrawlBoats.Instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|App/NpcTrawlBoats.cs|NpcTrawlBoats._instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|Art/BoatLampLook.cs|BoatLampLook.Current` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/BoatLampLook.cs|BoatLampLook.s_Current` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/GroundPrintRenderer.cs|GroundPrintRenderer.Instance` (1) -- SHARED -- This owns rendering resources or effects for a set of world objects; retain shared resources and explicit object or region inputs.
- `static|Art/GroundPrintRenderer.cs|GroundPrintRenderer.s_instance` (1) -- SHARED -- This owns rendering resources or effects for a set of world objects; retain shared resources and explicit object or region inputs.
- `static|Art/HullMeshReflection.cs|PacketOrder.Instance` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/LampPoolSystem.cs|LampPoolSystem.Instance` (1) -- SHARED -- This owns rendering resources or effects for a set of world objects; retain shared resources and explicit object or region inputs.
- `static|Art/LampPoolSystem.cs|LampPoolSystem.s_Instance` (1) -- SHARED -- This owns rendering resources or effects for a set of world objects; retain shared resources and explicit object or region inputs.
- `static|Art/LampShadowSystem.cs|LampShadowSystem.Instance` (1) -- SHARED -- This owns rendering resources or effects for a set of world objects; retain shared resources and explicit object or region inputs.
- `static|Art/LampShadowSystem.cs|LampShadowSystem.s_Instance` (1) -- SHARED -- This owns rendering resources or effects for a set of world objects; retain shared resources and explicit object or region inputs.
- `static|Art/PlayerOutfitVisual.cs|PlayerOutfitVisual.IdPaletteCount` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerOutfitVisual.cs|PlayerOutfitVisual.IdPaletteEpsSq` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerOutfitVisual.cs|PlayerOutfitVisual.IdPaletteFrom` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerOutfitVisual.cs|PlayerOutfitVisual.IdPaletteTo` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerOutfitVisual.cs|PlayerOutfitVisual.NoColour` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual.IdMainTex` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual.IdPixelsPerUnit` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual.IdRefractAmount` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual.IdRefractFrequency` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual.IdRefractSpeed` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual.IdSpriteHeightPx` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual.IdSubmergeDim` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual.IdSubmergeTint` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual.IdSubmergeTintAmount` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual.IdWaterlineFoam` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual.IdWaterlineFoamWidth` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/PlayerSubmergeVisual.cs|PlayerSubmergeVisual.IdWaterlineFrac` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Art/SeaweedPresenter.cs|SeaweedPresenter._instance` (1) -- SHARED -- This owns rendering resources or effects for a set of world objects; retain shared resources and explicit object or region inputs.
- `static|Art/ShorePlantTideDriver.cs|ShorePlantTideDriver.s_instance` (1) -- SHARED -- This owns rendering resources or effects for a set of world objects; retain shared resources and explicit object or region inputs.
- `static|Art/SunCasterInstaller.cs|SunCasterInstaller.Instance` (1) -- SHARED -- This owns rendering resources or effects for a set of world objects; retain shared resources and explicit object or region inputs.
- `static|Art/SunCasterInstaller.cs|SunCasterInstaller._instance` (1) -- SHARED -- This owns rendering resources or effects for a set of world objects; retain shared resources and explicit object or region inputs.
- `static|Boats/AmbientFleetPresenter.cs|AmbientFleetPresenter.Instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|Boats/AmbientFleetPresenter.cs|AmbientFleetPresenter._instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|Boats/BoatWakeEmitter.cs|BoatWakeEmitter._shared` (1) -- SHARED -- This owns rendering resources or effects for a set of world objects; retain shared resources and explicit object or region inputs.
- `static|Boats/FuelSpeedProfileLibrary.cs|FuelSpeedProfileLibrary.Shared` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Boats/FuelSpeedProfileLibrary.cs|FuelSpeedProfileLibrary.s_shared` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Boats/RadarContactSource.cs|RadarContactSource.Instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|Boats/RadarContactSource.cs|RadarContactSource._instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|Boats/TrapBuoyPresenter.cs|TrapBuoyPresenter._instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|Core/Boats/DeckOccupantSlots.cs|NoDeckOccupantSlots.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Core/Body/IPlayerStrength.cs|NeutralStrength.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Core/Body/IPlayerStrength.cs|NoCarriedLoad.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Core/Cleanliness/StinkLinesSettings.cs|StinkLinesSettings.Current` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Core/Cleanliness/WashVisualsSettings.cs|WashVisualsSettings.Current` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Core/Config/GameConfig.cs|PlayerZoomSettings.Default` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Core/Crew/VoyageSpec.cs|EmptyVoyageCatalog.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Core/Environment/IStillWater.cs|EmptyStillWater.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Core/Fishing/ClamShowSettings.cs|ClamShowSettings.Current` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Core/Fishing/IFishBehaviourFacts.cs|EmptyFishBehaviourFacts.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Core/Fishing/IFishSchools.cs|EmptyFishSchools.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Core/GroundPrints/DugHoleSettings.cs|DugHoleSettings.Current` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Core/GroundPrints/GroundPrintSettings.cs|GroundPrintSettings.Current` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Core/Kitchen/KitchenSeams.cs|AlwaysOnKitchenPower.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Core/Kitchen/KitchenSeams.cs|NothingSpoilsPerishability.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Core/Nav/IRadarContacts.cs|EmptyRadarSea.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Core/Save/ISaveService.cs|ImmediateSaveScope.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Core/Services/GameServices.cs|GameServices.AudioMix` (1) -- SHARED -- Configuration, definitions or local output settings carry no personal ownership; retain the shared provider.
- `static|Core/Services/GameServices.cs|GameServices.BirdPresence` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices.CatchFactory` (1) -- SHARED -- Configuration, definitions or local output settings carry no personal ownership; retain the shared provider.
- `static|Core/Services/GameServices.cs|GameServices.Config` (1) -- SHARED -- Configuration, definitions or local output settings carry no personal ownership; retain the shared provider.
- `static|Core/Services/GameServices.cs|GameServices.Crew` (1) -- SHARED -- World work or facilities remain shared; give operations an explicit facility, region and actor when necessary.
- `static|Core/Services/GameServices.cs|GameServices.CrossingGate` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices.Environment` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices.Farm` (1) -- SHARED -- World work or facilities remain shared; give operations an explicit facility, region and actor when necessary.
- `static|Core/Services/GameServices.cs|GameServices.FishBehaviour` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices.FishConservation` (1) -- SHARED -- Configuration, definitions or local output settings carry no personal ownership; retain the shared provider.
- `static|Core/Services/GameServices.cs|GameServices.FishSchools` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices.FishSizes` (1) -- SHARED -- Configuration, definitions or local output settings carry no personal ownership; retain the shared provider.
- `static|Core/Services/GameServices.cs|GameServices.FoodPerishability` (1) -- SHARED -- Configuration, definitions or local output settings carry no personal ownership; retain the shared provider.
- `static|Core/Services/GameServices.cs|GameServices.GiftableFacts` (1) -- SHARED -- Configuration, definitions or local output settings carry no personal ownership; retain the shared provider.
- `static|Core/Services/GameServices.cs|GameServices.KitchenPower` (1) -- SHARED -- World work or facilities remain shared; give operations an explicit facility, region and actor when necessary.
- `static|Core/Services/GameServices.cs|GameServices.Messes` (1) -- SHARED -- World work or facilities remain shared; give operations an explicit facility, region and actor when necessary.
- `static|Core/Services/GameServices.cs|GameServices.RadarContacts` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices.StillWater` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices.StillWaterChanged` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices.SwellClimates` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices.TidalTerrain` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices.TidalTerrainChanged` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices.Voyages` (1) -- SHARED -- World work or facilities remain shared; give operations an explicit facility, region and actor when necessary.
- `static|Core/Services/GameServices.cs|GameServices.WorldFrame` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices._birdPresence` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices._crew` (1) -- SHARED -- World work or facilities remain shared; give operations an explicit facility, region and actor when necessary.
- `static|Core/Services/GameServices.cs|GameServices._crossingGate` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices._farm` (1) -- SHARED -- World work or facilities remain shared; give operations an explicit facility, region and actor when necessary.
- `static|Core/Services/GameServices.cs|GameServices._fishBehaviour` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices._fishSchools` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices._foodPerishability` (1) -- SHARED -- Configuration, definitions or local output settings carry no personal ownership; retain the shared provider.
- `static|Core/Services/GameServices.cs|GameServices._kitchenPower` (1) -- SHARED -- World work or facilities remain shared; give operations an explicit facility, region and actor when necessary.
- `static|Core/Services/GameServices.cs|GameServices._messes` (1) -- SHARED -- World work or facilities remain shared; give operations an explicit facility, region and actor when necessary.
- `static|Core/Services/GameServices.cs|GameServices._radarContacts` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices._stillWater` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices._tidalTerrain` (1) -- SHARED -- Keep world facts shared; query or register by region so one player's travel cannot replace another region's facts.
- `static|Core/Services/GameServices.cs|GameServices._voyages` (1) -- SHARED -- World work or facilities remain shared; give operations an explicit facility, region and actor when necessary.
- `static|Core/Services/IBirdPresence.cs|EmptyBirdPresence.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Core/Time/WatchFaceState.cs|DaylightHours.Shared` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Core/Time/WatchFaceState.cs|DaylightHours._shared` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Core/Traits/TraitService.cs|ById.Instance` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Core/World/ICrossingGate.cs|AlwaysOpenCrossingGate.Instance` (1) -- SHARED -- This is a stateless fallback provider; retain one instance and keep real personal state outside it.
- `static|Environment/DugHolePresenter.cs|DugHolePresenter.Instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|Fishing/FishSchoolPresenter.cs|FishSchoolPresenter.Instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|Fishing/TrapSplashFx.cs|TrapSplashFx._instance` (1) -- SHARED -- This owns rendering resources or effects for a set of world objects; retain shared resources and explicit object or region inputs.
- `static|Player/FishArtLibrary.cs|FishArtLibrary.Shared` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Player/PlayerFrothPresenter.cs|PlayerFrothPresenter._glyph` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|Player/PlayerWalkController.cs|PlayerWalkController.WalkCycle` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|UI/ChooserAxisRepeat.cs|ChooserAxisTuning.Current` (1) -- SHARED -- This is immutable metadata, tuning, a comparer or a shared rendering resource; no player-state split is needed.
- `static|World/Beach/BeachLifeDirector.cs|BeachLifeDirector.Instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|World/Campaign/CampaignBarrierDirector.cs|CampaignBarrierDirector.Instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|World/Conversations/NpcConversationDirector.cs|NpcConversationDirector.Instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.
- `static|World/Shops/SoldOutSignsDirector.cs|SoldOutSignsDirector._instance` (1) -- SHARED -- This owns world objects or facts; use a session or region registry when both players occupy different places.

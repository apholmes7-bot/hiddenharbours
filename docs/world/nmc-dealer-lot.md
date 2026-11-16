# Nine Mile Creek dealer lot

This is the scene handoff for the existing vehicle-ownership slice. It serves P2,
P3 and P4. The data and purchase logic are ready for integration; neither seller
has a placed counter, vehicle handover, or a listed wares-book entry. No scene,
prefab, ground, art, or save schema is changed by this slice.

## What the counters sell

The existing `SellerDef` assets identify `seller.nmc_vehicle_dealer` and
`seller.nmc_industrial_yard`, both in `trade.vehicles`. Their keeper ids remain
empty until the NPC lane authors them. Do not infer a person from a shop name.
Each `VehicleOffer` in `Data/Resources/Catalog/Vehicles` is one machine, with
its own stable offer id, price and model id. Its deed is keyed by offer id.

| Counter | Authored stock | Price in currency units |
| --- | --- | --- |
| Vehicle dealer | Trike 200; Enduro 250; Utility Quad 4x4 | 800; 900; 1,200 |
| Vehicle dealer | Modern 350; Hightop Van; Modern 2500 | 3,500; 4,000; 5,500 |
| Vehicle dealer | Modern 3500; Dually 3500; Otter 8x8 | 7,000; 7,500; 9,000 |
| Industrial yard | Used Cabover Box Truck; Conventional Box Truck | 5,000; 12,000 |
| Industrial yard | Classic Long-Nose Semi; Aero Sleeper Semi | 25,000 each |

The used cabover needs the repair already priced in its offer before it runs.
The Otter offer fills the sole gap among the thirteen current vehicle models;
9,000 is an initial balance value in that asset, open to tuning. No tractor or
forklift definition exists in this catalog, so neither is offered speculatively.

Heavy-goods stock is **blocked on existing work**, not implemented here.
PR #1224 owns `IslandGoodDef` and island supply goods; PR #1202 owns `ShopDef`
and stock rules. After integration, reference their fuel drums, lumber, feed
and building goods by their actual stable ids, where present. Keep the same
industrial seller identity. Do not create a second goods vocabulary or use a
count-only purchase to deliver a heavy object into a distant inventory.

## Space and actors for the scene lane

- Reserve approximately **36 m by 28 m** for the dealer and **48 m by 36 m** for
  the industrial yard as planning envelopes, subject to the scene lane's ground
  and turning checks. These are not approved coordinates or a terrain change.
- Display the nine dealer machines and four industrial machines in distinct
  parked positions associated with their offer ids. Allow a roughly 6 m dealer
  aisle; size the heavy-yard turning area from the actual trucks' steering and
  swept paths. Keep an unobstructed road entrance and pedestrian route to each
  counter. Reduce or stage the display if the accepted footprint cannot fit it.
- Put the seller beside the office entrance, outside the vehicle path and
  within conversation reach from the pedestrian route. The scene lane places
  the actor; the NPC lane supplies identity, dialogue and working routine.
- Put the dealer sign at the road-facing entrance, readable from the approach;
  give the industrial yard its own sign and loading area. Art and wording need
  the appropriate content lanes. This plan does not prescribe new art.
- Every wheel must rest on the accepted ground. Reserve handover and repair
  bays where machines remain supported. Future heavy goods need physical
  storage, lifting access and supported load/unload positions. The player
  carries, loads, transports and unloads purchases; a sale never teleports them.

## Integration contract and visibility blockers

Bind the counter's `SellerDef.Counterparty` and its supplied `IWallet` to a
Core `VehicleSellerAccount`. Pass that account, the buyer's wallet and the
buyer's save service into `VehiclePurchase.TryBuy` / `TryRepair`. `VehicleOffer`
implements Core `IVehicleSale`; the seller seam is Core `IVehicleSeller`.
The account must match the offer seller. Missing accounts, unaffordable prices,
duplicate deeds and balances that would overflow are refused without a sale.
Successful payment debits the buyer and credits the seller before publishing
the attributed purchase or repair signal. `CustomLedger` counts one completed
vehicle sale or repair, not the currency value.

The account is supplied by the caller; this slice does not create or persist a
merchant economy. A runtime counter must get its account lifecycle from the
economy/save integration before being enabled. Purchase completion grants the
existing buyer deed; it does not spawn a machine or save its parked position.

Keep all offers `Catalog.Listed = false` until the scene lane places the
counter, supplies that account, binds the wares interaction and implements
physical vehicle handover/restoration. The Vehicles consumer must unlock the
matching displayed machine and respect damaged/repaired state. Parked-position
persistence belongs to the existing vehicle integration follow-up. Enable
listing with those consumers and prove the sold machine remains on the ground,
is owned after reload and cannot be bought twice. The scene lane supplies the
visual proof; this data-and-logic slice makes no claim about appearance.

# Electronics offer proposals

The six missing products use the existing `InstrumentOffer` item identity and
`CatalogListing.Sellers` model. Prices below are proposals awaiting review.

| Item | Price | Rule from existing prices |
|---|---:|---|
| Autopilot | 6000 | Expensive upgrade: 34.48% of the 17400 Cape Islander and 29.70% of the 20200 lobster boat. |
| Dome compass | 70 | Half the 140 depth sounder: a basic magnetic heading instrument. |
| Flush compass | 140 | Equal to the depth sounder; twice the dome for the fitted dash unit. |
| Radar | 3600 | Twice the 1800 chartplotter for the scanner and display. |
| Wind dial | 140 | Equal to the depth sounder for a single-purpose analogue instrument. |
| Wind display | 650 | Equal to the fish finder for the digital instrument tier. |

All six name the existing boat and gear wholesaler. Their catalogue tags remain
unlisted: the committed scene has individual instrument vendors for the fish
finder, chartplotter and Loran only. The keeper's browse dialogue opens this
seller's wares book, and `BuyCatalog` can pass an offer to an existing
`InstrumentShop`, but `StoreWiringTests` requires each listed ware to have its own
committed vendor. These six therefore remain dormant until the separate counter
and fitting work is ready. No counter, dialogue, UI, scene or prefab changes here.

The depth sounder remains listed at the island store for 140. Existing electronics,
both wholesalers and the boatyard retain their identities and prices. No stock
quantities or shelf-stock model are introduced.

`InstrumentOffer` itself holds the instrument's stable item id; there is no second
item Def to duplicate. The identity contract now discovers that type as well as
Def types. Addition files register all ten instrument offers, including the four
existing identities, so the new coverage starts complete. The frozen ledger is
unchanged. Content tests require exactly nine boat-wholesaler electronics on file,
one offer per new id, real seller identities, separate trades, dormant new tags
and the retained island sounder.

The autopilot offer does not grant heading hold or persist a fitment. The later
fitting and save integration must validate hull eligibility before any charge.

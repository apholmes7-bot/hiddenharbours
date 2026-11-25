# Interior fixture readability audit

Base: `1abca5974ce3d16a3257777797bb2547c9ad8d53`. Weakest first, judging each complete sheet and its front three-quarter view. Dimensions are PNG / sprite cell, in pixels. The first three are the in-place redraw targets.

The audit covers all 34 standalone fixture, appliance and interior furniture PNGs, including eight facings and every appliance state. No washer, dryer or dishwasher sprite was found in the art tree. Whole room and boat-interior sheets are environments, not standalone appliance sprites.

Prefab links below are direct serialized sprite-GUID references. A dash means no prefab reference was found, not proof of non-use in scenes: the older strips and room furniture can be placed by builders. Current washroom and kitchen prefabs use the newer `Appliance_*` sheets; replacing the older top three does not establish a visible change in those prefabs. Animated sheets also resolve through the appliance resource loader.

| Rank | Sprite file | PNG / cell px; frames | Prefab using sprite | Readability |
|---:|---|---|---|---|
| 1 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_bath.png` | 480x57 / 60x57; 8 | — | Solid white top has no cut-out tub; reads as a bed or block. |
| 2 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_bathroomSink.png` | 208x21 / 26x21; 8 | — | Solid diamond has neither bowl recess nor a readable tap. |
| 3 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_kitchenSink.png` | 368x53 / 46x53; 8 | — | Tap sits over a flat metal slab; the missing bowl makes it a counter. |
| 4 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_microwave.png` | 224x45 / 28x45; 8 | — | Tiny upper box on a tall cupboard reads as a wall cabinet. |
| 5 | `Assets/_Project/Art/Resources/Appliances/Appliance_microwave.png` | 224x42 / 28x42; 8 | `Assets/_Project/Prefabs/Props/Kitchen/Microwave.prefab` | Dark window helps, but the integrated base still reads as a tall cabinet. |
| 6 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_shower.png` | 400x81 / 50x81; 8 | — | Tall opaque enclosure reads as a cubicle and masks the wall behind. |
| 7 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_washstand.png` | 272x46 / 34x46; 8 | — | White bowl is almost filled by highlights; basin depth is weak. |
| 8 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_fridge.png` | 288x53 / 36x53; 8 | — | Small handles and faint door seam leave a generic white box. |
| 9 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_prepCounter.png` | 336x49 / 42x49; 8 | — | Featureless top gives few preparation cues. |
| 10 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_stove.png` | 320x65 / 40x65; 8 | — | Burner marks and oven door are low contrast against the dark body. |
| 11 | `Assets/_Project/Art/Resources/Appliances/Appliance_prepCounter.png` | 336x53 / 42x53; 8 | `Assets/_Project/Prefabs/Props/Kitchen/PrepCounter.prefab` | Tap and inset pale square compete with the intended preparation surface. |
| 12 | `Assets/_Project/Art/Resources/Appliances/Appliance_shower.png` | 432x567 / 54x81; 56 | `Assets/_Project/Prefabs/Washrooms/Shower.prefab` | Tray and curtain identify it, but the full-height frame dominates. |
| 13 | `Assets/_Project/Art/Resources/Appliances/Appliance_chestFreezerLarge.png` | 608x595 / 76x85; 56 | `Assets/_Project/Prefabs/Props/Kitchen/CamperFreezer.prefab` | Lid seam and vent help; broad white body still resembles storage. |
| 14 | `Assets/_Project/Art/Resources/Appliances/Appliance_chestFreezer.png` | 512x546 / 64x78; 56 | `Assets/_Project/Prefabs/Props/Kitchen/GinnyFreezer.prefab` | Lid and vent distinguish a freezer only on close inspection. |
| 15 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_toilet.png` | 256x59 / 32x59; 8 | — | High tank and pipe read, though the tiny bowl opening is weak. |
| 16 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_toiletModern.png` | 240x37 / 30x37; 8 | — | Tank and bowl silhouette read; seat rim is very thin. |
| 17 | `Assets/_Project/Art/Resources/Appliances/Appliance_basin.png` | 240x315 / 30x45; 56 | `Assets/_Project/Prefabs/Washrooms/WashStand.prefab` | Tap and oval opening read, though the pale rim crowds the recess. |
| 18 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_fridgeModern.png` | 288x69 / 36x69; 8 | — | Separate doors and dispenser improve recognition; handle contrast is modest. |
| 19 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_iceCooler.png` | 240x30 / 30x30; 8 | — | White lid and red insulated body read; closure is subdued. |
| 20 | `Assets/_Project/Art/Resources/Appliances/Appliance_cookStove.png` | 304x74 / 38x74; 8 | `Assets/_Project/Prefabs/Props/Kitchen/Stove.prefab` | Flue, burners and oven door read; burner contrast is still modest. |
| 21 | `Assets/_Project/Art/Resources/Appliances/Appliance_toilet.png` | 256x160 / 32x40; 32 | `Assets/_Project/Prefabs/Washrooms/Toilet.prefab` | Seat ring, water recess and tank give a clear toilet silhouette. |
| 22 | `Assets/_Project/Art/Resources/Appliances/Appliance_kitchenSink.png` | 352x399 / 44x57; 56 | `Assets/_Project/Prefabs/Props/Kitchen/KitchenSink.prefab` | Bowl, tap and draining board distinguish it from its cabinet. |
| 23 | `Assets/_Project/Art/Resources/Appliances/Appliance_fridge.png` | 624x344 / 78x86; 32 | `Assets/_Project/Prefabs/Props/Kitchen/Fridge.prefab` | Door divisions, handles and vent read; open states expose storage. |
| 24 | `Assets/_Project/Art/Resources/Appliances/Appliance_bath.png` | 512x793 / 64x61; 104 | `Assets/_Project/Prefabs/Washrooms/Bath.prefab` | Deep cut-out, rim and taps read as a bath; water states reinforce depth. |
| 25 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_wharfShower.png` | 368x80 / 46x80; 8 | — | Raised head and slatted base read clearly as an outdoor shower. |
| 26 | `Assets/_Project/Art/Resources/Appliances/Appliance_wharfShower.png` | 368x328 / 46x82; 32 | `Assets/_Project/Prefabs/Washrooms/WharfShower.prefab` | Head, upright and tray read; running states clarify use. |
| 27 | `Assets/_Project/Art/Sprites/Fixtures/Fixture_outhouse.png` | 544x95 / 68x95; 8 | — | Roof, timber walls and door identify the enclosure. |
| 28 | `Assets/_Project/Art/Resources/Appliances/Appliance_outhouse.png` | 656x318 / 82x106; 24 | `Assets/_Project/Prefabs/Washrooms/Outhouse.prefab` | Door hardware and open states make the enclosure legible. |
| 29 | `Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_seaChest.png` | 288x35 / 36x35; 8 | — | Lid and corner bands read as a chest. |
| 30 | `Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_dresser.png` | 320x49 / 40x49; 8 | — | Drawer divisions and handles read as a dresser. |
| 31 | `Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_chair.png` | 192x41 / 24x41; 8 | — | Back slats, seat and legs form a clear chair. |
| 32 | `Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_table.png` | 432x53 / 54x53; 8 | — | Boarded top and legs read as a table. |
| 33 | `Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_bed.png` | 672x79 / 84x79; 8 | — | Pillow, blanket and bed frame give immediate recognition. |
| 34 | `Assets/_Project/Art/Sprites/Buildings/Shops/Fixtures/ShopFixture_counter_generalStore.png` | 760x93 / 95x93; 8 | — | Register and goods distinguish a staffed shop counter. |

The review contact sheet shows every atlas cell at 2x nearest-neighbour on `#815a39`, sampled from the accepted cream saltbox floor. It is retained outside version control. These are source-art observations; room lighting, overlap and game-camera readability were not rendered.

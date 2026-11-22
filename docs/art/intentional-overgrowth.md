# Intentional growth on buildings

Text audit of **f1b4ab2f59f417eda06f2239f7dee25d4c52ad23**. St Peters comes first. This is a preservation list and a source-based triage, not permission to remove plants or an acceptance of the picture. No editor, render, image inspection, rebake, scene edit, asset edit or save change was performed.

## INTENTIONAL — leave these alone

- **The derelict cannery at St Peters (170, 16): keep the weeds and moss that belong to the building drawing.** The lifecycle materials are `lcWeed` and `lcMoss`; there are **no separate plant IDs** for those painted details. The building is explicitly `collapsing`. This protection does not include separate field plants rooted inside it.

- **The St Peters general store (2.5, 35.422): keep its two built-in planters.** They are part of the building sprite, have **no separate plant IDs**, and the source places exactly two. Also keep the two deliberately placed forecourt pots, `prop.stp_vl_general_store_barrel_planter` and `prop.stp_vl_general_store_barrel_planter_2`; these are yard decorations, not roof growth.

- **The village sage cottage and white gable: keep the deliberately placed dory planter and vegetable rows**, `prop.stp_vl_sage_cottage_dory_planter` and `prop.stp_vl_white_gable_veg_rows`. They stand beside the houses. Do not treat all vegetation near a house as accidental.

## UNSURE — leave these alone pending a picture check

- **Possible roof moss on the school, red saltbox, white farmhouse, sage cottage, harbour gable, cream saltbox and white gable at St Peters; the red saltbox and sage cottage at Nine Mile Creek; and the unused village general-store prefab.** The classic house renderer explicitly authors moss. The current renderer can bypass that pass, so its presence in the committed sprites is unverified. There are **no separate plant IDs** to remove.

- **St Peters cannery at (170, 16): preserve these named field plants outside the tested footprint** while checking whether their pictures actually cover the building: `plant.stp_east_end_` suffixes **263, 264, 265, 266, 276, 277, 278, 279, 280, 281, 282, 284, 292, 293, 294, 295, 296, 297, 310, 311, 321, 322, 328, 329, 330, 331, 332, 338, 339, 340, 341, 347, 348, 352, 354, 355, 356, 357, 361, 362, 363, 364, 365, 366, 367, 368, 373, 374, 375, 377, 378, 379, 380, 383, 384, 385, 387, 388, 389, 396, 397, 398, 399, 400, 410, 411, 412, 413, 419, 420, 423, 424, 430, 431, 432, 433, 437, 438, 439, 440, 441, 450, 452, 453, 457, 460, 461, 462, 463, 468, 469, 474, 476, 477, 478, 484, 485, 486, 490, 491, 492, 493, 494, 495, 499, 500, 501, 507, 511, 512, 513, 517, 518, 519, 520, 523, 524, 525, 526, 527, 528, 529, 532, 533, 534, 536, 537, 538, 539, 540, 541, 545, 546, 547, 551, 552, 553, 554, 555, 556, 557, 558, 560, 561, 562, 563, 564, 565, 566, 567, 568, 569, 570, 571, 578, 579, 580, 581, 584, 585, 586, 587, 588, 589, 590, 591, 595, 596, 597, 598, 599, 600, 602, 603, 604, 605, 606, 607, 608, 609, 610, 615, 616, 617**. Prefix plus suffix is the complete plant ID; this is an exact set, not a numeric range.

- **St Peters net loft at (183.2, 22.8): preserve these named field plants outside the tested footprint** while checking whether their pictures actually cover the building: `plant.stp_east_end_` suffixes **467, 476, 477, 478, 485, 486, 487, 493, 494, 495, 496, 497, 501, 502, 503, 508, 513, 514, 515, 516, 529, 530, 535, 542, 543, 548, 558, 570, 571, 572, 573, 582, 583, 591, 592, 593, 597, 598, 599, 600, 608, 609, 610, 611, 615, 616, 617, 618, 626, 627, 628, 629, 630, 631, 632, 639, 641, 642, 643**. Prefix plus suffix is the complete plant ID; this is an exact set, not a numeric range.

- **St Peters net shed at (187.7, 9.3): preserve these named field plants outside the tested footprint** while checking whether their pictures actually cover the building: `plant.stp_east_end_` suffixes **282, 284, 296, 297, 298, 299, 304, 305, 311, 312, 313, 323, 331, 332, 333, 342, 355, 356, 357, 358, 366, 367, 368, 369, 370, 371, 380, 381, 389, 390, 391, 399, 400, 401, 402, 403, 404, 412, 413, 414, 415, 423, 424, 425, 426, 433, 434, 441, 442, 443, 444, 445, 453, 454**. Prefix plus suffix is the complete plant ID; this is an exact set, not a numeric range.

- **St Peters bait store at (182.3, -8.8): preserve these named field plants outside the tested footprint** while checking whether their pictures actually cover the building: `plant.stp_east_end_` suffixes **22, 23, 24, 33, 34, 42, 43, 44, 45, 51, 52, 62, 63, 97, 98, 108, 109, 110, 111, 121, 122, 123, 131, 132, 133, 143, 144, 145, 150, 151, 152, 153, 158, 159, 160, 161, 162, 163, 164, 165, 176, 177, 178, 179, 180**. Prefix plus suffix is the complete plant ID; this is an exact set, not a numeric range.

- **St Peters white gable at (53, 35.204): preserve these named field plants outside the tested footprint** while checking whether their pictures actually cover the building: `plant.stp_ginnys_plot_` suffixes **1378, 1379, 1380, 1424, 1445, 1446, 1464, 1479, 1480, 1498, 1524, 1525, 1606, 1643, 1707, 1726, 1772, 1790, 1842, 1946, 1947, 1965, 2022, 2037, 2134, 2135, 2177, 2178, 2211, 2240, 2863, 2864, 2873, 2874, 2875, 2887, 2888, 2889, 2900**. Prefix plus suffix is the complete plant ID; this is an exact set, not a numeric range.

- **Generated St Peters plants around all nine village buildings, the cannery and the bait store:** the candidate cell coordinates in the locator appendix are the hold list. Generated plants have **no serialized plant IDs**. The listed species IDs identify kinds, not individual plants; never delete every plant of a listed species. Roots inside footprints are separately marked ACCIDENT below.

- **Trees whose rectangles reach the St Peters cannery:** [BlackSpruce_3 [fileID 286582709]](../../Assets/_Project/Scenes/StPeters.unity#L16303), [RedSpruce_3 [fileID 1441714216]](../../Assets/_Project/Scenes/StPeters.unity#L77835), [RedSpruce_0 [fileID 1676782703]](../../Assets/_Project/Scenes/StPeters.unity#L90924), [WhitePine_1 [fileID 1829785222]](../../Assets/_Project/Scenes/StPeters.unity#L100059). Their crowns may merely pass behind it; rectangle intersection does not establish a roof plant.

- **Nine Mile Creek cold store at (64, 99.2):** [SugarKelp [fileID 881506416]](../../Assets/_Project/Scenes/NineMileCreek.unity#L145888). Keep these pending a picture check. Names repeat in the scene, so the fileIDs and line links are part of the locator.

- **Nine Mile Creek fish plant at (-265, -40):** [WhiteBirch_1 [fileID 101093748]](../../Assets/_Project/Scenes/NineMileCreek.unity#L13211). Keep these pending a picture check. Names repeat in the scene, so the fileIDs and line links are part of the locator.

- **Nine Mile Creek community hall at (-153, 62.5):** [RedOak_10 [fileID 1131412072]](../../Assets/_Project/Scenes/NineMileCreek.unity#L410615), [Meadowsweet_26 [fileID 701717205]](../../Assets/_Project/Scenes/NineMileCreek.unity#L410985), [RedOak_11 [fileID 865912643]](../../Assets/_Project/Scenes/NineMileCreek.unity#L410801). Keep these pending a picture check. Names repeat in the scene, so the fileIDs and line links are part of the locator.

- **Nine Mile Creek boat-gear wholesaler at (-148.4, 235.9):** [WhiteBirch_1 [fileID 1794346413]](../../Assets/_Project/Scenes/NineMileCreek.unity#L307713). Keep these pending a picture check. Names repeat in the scene, so the fileIDs and line links are part of the locator.

- **Greywick shipyard shed at (-8, 3):** [Tree34 [fileID 1095741838]](../../Assets/_Project/Scenes/Greywick.unity#L2659). These are nearby trees, not children of the building.

- **Greywick dory-yard shed at (-8, 8):** [Tree34 [fileID 1095741838]](../../Assets/_Project/Scenes/Greywick.unity#L2659), [Tree05 [fileID 1964713807]](../../Assets/_Project/Scenes/Greywick.unity#L4324). These are nearby trees, not children of the building.

- **Greywick harbourmaster office at (-12, 9):** [Tree01 [fileID 1231305635]](../../Assets/_Project/Scenes/Greywick.unity#L2843). These are nearby trees, not children of the building.

**Excluded entirely from cleanup decisions:** Ginny’s cottage lot, including its three sheds. Its observations are listed separately below; neither an ACCIDENT verdict nor a relocation recommendation is made for that lot.

**The hold list wins if a plant appears in more than one row.** `plant.stp_east_end_467` is rooted inside the cannery footprint but also a possible overlap at the net loft: retain it pending review. The `plant.stp_ginnys_plot_` name also reaches the white-gable area; a prefix is not a parcel boundary. None of the white-gable hold entries authorizes work on the excluded cottage lot.

## What the counts mean

The table separates three things that cannot safely share one verdict: growth baked into a building, plants whose **roots lie inside its ground footprint**, and plants whose **rectangles might overlap its picture**. INTENTIONAL means positive authored evidence. ACCIDENT means external field roots contradict the building-clearance rule. UNSURE means a possible screen overlap or an unverified art path; absence of an intent note alone is not proof of accident.

Named-site counts come from all **9,089 `Authored` records** in the St Peters field and the **50 `PlantSpeciesDef` code bindings**. `Authored` means stored as a row, not necessarily hand-placed for a building. Generated-site counts are a text reconstruction of the field’s hash, jitter, ground recipe and clump law. They are not a Unity test result or a visual plant count. The reconstruction uses the serialized seed and float positions; no random seed or data was changed.

A root inside the ground footprint is counted once per building. Picture candidates exclude those roots and combine roots in the building’s sprite rectangle with plant envelopes reaching that rectangle. Plant envelopes conservatively union the species’ cell rectangles over season, stage, variant and tide, plus the serialized wind margin. They can overcount transparent areas and plants drawn behind a building. Tree/shrub rectangles use the saved sprite metadata. Wind, alpha, render ordering and actual visible coverage were not evaluated. A plant can be a candidate for two buildings; do not sum rows into unique-world totals.

Generated choices with elevation restrictions remain **possible slots**, not counted plants: no height-texture pixels were read. For the reported St Peters footprint-root counts, every selected recipe choice spans −100…100 m, enclosing the height asset’s −4…12 m range. Nine Mile Creek grass counts are only candidates after the serialized density cap and tree/shrub proximity rule; the height test was not executed. No candidate is promoted to intentional merely because it forms a dense cluster.

## Evidence and footprint rules

1. **Plants inside a ruin are not automatically intentional.** `// lifecycle pass, which is where they belong.` ([StPetersGrass.cs:526](../../Assets/_Project/Code/App/Editor/StPetersGrass.cs#L526)) The preceding lines explicitly say the meadow must not grow through a building sprite and that ruin weeds are drawn into its cell. This is stronger evidence than the word “derelict”.

2. **Cannery growth is attached to its art.** `"decay": "collapsing"` ([Buildings.json:647](../../Assets/_Project/Art/Sprites/Buildings/Village/Buildings.json#L647)). The active wharf renderer calls the lifecycle pass at [wharfBuildingRig2.js:153](../../docs/art/rigs/wharf-building-kit-v2/wharfBuildingRig2.js#L153). The lifecycle code emits `weeds(out,x,y,0.28+d*0.85+rnd()*0.4,rnd, rnd()<0.25?'lcMoss':'lcWeed');` ([buildingLifecycleRig.js:662](../../docs/art/rigs/wharf-building-kit-v2/lib/buildingLifecycleRig.js#L662)). These are generated faces in the building bake, not species rows. A count of botanical individuals is not available in the scene or prefab.

3. **Store planters are deliberate.** `if(b.planters){ planter(out, -hw+0.42, y1+0.52); planter(out, hw-0.42, y1+0.52); }` ([shopfrontRig.js:994](../../docs/art/rigs/village-return/shop-building-kit/shopfrontRig.js#L994)), with `planters:true` on the general-store type at [shopfrontRig.js:112](../../docs/art/rigs/village-return/shop-building-kit/shopfrontRig.js#L112) and the `harbourStore` preset at line 156. The bake contract names that preset at [shops.contract.json:17](../../Assets/_Project/Art/Sprites/Buildings/Shops/shops.contract.json#L17).

4. **Roof-moss caution.** `// weather + moss speckle` ([houseIsoRig.js:1058](../../docs/art/rigs/village-return/houses-kit/Art/houseIsoRig.js#L1058)) precedes a roof-only paint operation at line 1064. But `if(!opts.classic && root.CoastalPass && root.CoastalPass.light) return renderLive(dir, opts);` ([houseIsoRig.js:1098](../../docs/art/rigs/village-return/houses-kit/Art/houseIsoRig.js#L1098)) bypasses it. The stored house options contain weather values, not proof that this paint operation produced the current image. This audit therefore protects suspected moss as UNSURE.

5. **Where the field comes from.** `Id: plant_field.st_peters` ([StPetersPlantField.asset:15](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15)); `Origin (-380, -260)`, 1520 × 1040 cells, 0.5 m, seed 9109 (lines 16–20). The scene binds it at [StPeters.unity:161607](../../Assets/_Project/Scenes/StPeters.unity#L161607). The ground asset’s `Payload` is a run-length encoding of recipe/material bytes, not plant IDs: [PlantFieldGroundData.cs:16](../../Assets/_Project/Code/World/PlantFieldGroundData.cs#L16). [PlantField.cs:170](../../Assets/_Project/Code/Art/PlantField.cs#L170) grows generated cells; lines 197–206 independently add every named row without using the ground exclusion flag to reject it. [PlantClumpLaw.cs:20](../../Assets/_Project/Code/Art/PlantClumpLaw.cs#L20) explicitly says clumping does not pick a species or move a plant. Dense generated clumps do not prove authored building overgrowth.

6. **Footprints and pictures are different.** `Footprints:` ([village_plan.st_peters.asset:91](../../Assets/_Project/Data/Regions/StPetersVillage/village_plan.st_peters.asset#L91)) supplies nine local ground bounds; each lot’s `Position` + `FootprintId` locates them. Three are marked provisional at lines 107, 111 and 127. Lot `Outline` is the yard parcel, not the building. For the saved scene, the audit uses its serialized `BuildingInterior` dimensions/facing and [InteriorFootprint.cs:168](../../Assets/_Project/Code/World/InteriorFootprint.cs#L168) (rotate on the ground, then squash Y). The wharf shells without interiors use the bake’s width/length and sprite facing with [wharfBuildingRig2.js:42](../../docs/art/rigs/wharf-building-kit-v2/wharfBuildingRig2.js#L42); the origin is the footprint centre per [Buildings.json:7](../../Assets/_Project/Art/Sprites/Buildings/Village/Buildings.json#L7). Sprite rectangles come from the selected sprite’s meta rect/pivot and PPU; no pixel mask was inferred.

7. **Nine Mile Creek’s generated grass is a separate system.** Its serialized field begins at [NineMileCreek.unity:239865](../../Assets/_Project/Scenes/NineMileCreek.unity#L239865); [GrassField.cs:438](../../Assets/_Project/Code/Art/GrassField.cs#L438) decodes sites and applies the keep rule. `KeepFraction: 0.35` ([GrassThinning.asset:23](../../Assets/_Project/Data/Regions/NineMileCreek/GrassThinning.asset#L23)) is followed by a ground-height condition. The three retained picture candidates in this audit are provisional because the height condition was not run. The separate sugar-kelp sprite has kind `Id: shoreplant.sugar_kelp` ([SugarKelp.asset:15](../../Assets/_Project/Data/Decor/ShorePlants/SugarKelp.asset#L15)); the scene fileID identifies the individual. Tree sprite variants have no plant-field IDs.

### Saved village placements versus lot data

| Building | Saved scene position used here | Lot position | Footprint source |
|---|---|---|---|
| creamSaltbox | (28.5, 35.704) | (28.5, 35.704) | [lot.st_peters.cream_saltbox.asset:19](../../Assets/_Project/Data/Regions/StPetersVillage/lot.st_peters.cream_saltbox.asset#L19); `footprint.st_peters.cream_saltbox` |
| harbourGable | (-6.25, 46.125) | (-6.25, 46.125) | [lot.st_peters.harbour_gable.asset:19](../../Assets/_Project/Data/Regions/StPetersVillage/lot.st_peters.harbour_gable.asset#L19); `footprint.st_peters.harbour_gable` |
| redSaltbox | (25, 8) | (21.696, 18.03) | [lot.st_peters.red_saltbox.asset:19](../../Assets/_Project/Data/Regions/StPetersVillage/lot.st_peters.red_saltbox.asset#L19); `footprint.st_peters.red_saltbox` |
| sageCottage | (-21.5, 41.643) | (-21.5, 41.643) | [lot.st_peters.sage_cottage.asset:19](../../Assets/_Project/Data/Regions/StPetersVillage/lot.st_peters.sage_cottage.asset#L19); `footprint.st_peters.sage_cottage` |
| school | (-11.4, 35.423) | (-11.4, 35.423) | [lot.st_peters.school.asset:19](../../Assets/_Project/Data/Regions/StPetersVillage/lot.st_peters.school.asset#L19); `footprint.st_peters.school` |
| whiteFarmhouse | (21, 26) | (42.301, 16.78) | [lot.st_peters.white_farmhouse.asset:19](../../Assets/_Project/Data/Regions/StPetersVillage/lot.st_peters.white_farmhouse.asset#L19); `footprint.st_peters.white_farmhouse` |
| whiteGable | (53, 35.204) | (53, 35.204) | [lot.st_peters.white_gable.asset:19](../../Assets/_Project/Data/Regions/StPetersVillage/lot.st_peters.white_gable.asset#L19); `footprint.st_peters.white_gable` |

The red saltbox, white farmhouse and post office positions differ between the saved scene and lot rows. Counts below are tied to the saved positions, not silently moved to the planned lots. This document does not reconcile those placements.

## St Peters building table

Each row has independent verdicts for each source. **N** = named field roots inside the footprint; **G** = generated field roots inside it; **U** = named/generated candidates outside it, including envelope-only candidates. An elevation-dependent slot is listed separately and is not included in N/G/U. “No plant IDs” for baked growth is a genuine absence of identity, not a missing lookup.

| Building, prefab and saved position | Count and kind; source | Verdict | Quoted evidence |
|---|---|---|---|
| St Peters **generalStore**, (2.5, 35.422)<br>No matching building prefab; serialized scene object | 2 baked planters, no plant IDs: **INTENTIONAL**. Field: N=0, G=13; `plant.fireweed` × 1, `plant.lupin` × 4, `plant.meadow_grass` × 1, `plant.oxeye_daisy` × 2, `plant.queen_anne` × 2, `plant.staghorn_sumac` × 2, `plant.timothy` × 1. U=0 named + 209 generated; exact kinds/locators below. | Field footprint roots **ACCIDENT**; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: generalStore` ([StPeters.unity:4140](../../Assets/_Project/Scenes/StPeters.unity#L4140)). Footprint and field reconstruction: evidence 5–6; external growth exclusion: evidence 1. |
| St Peters **redSaltbox**, (25, 8)<br>[Village_redSaltbox.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_redSaltbox.prefab) | Possible baked roof-moss treatment, count/plant IDs not serialized: **UNSURE**. Field: N=0, G=5; `plant.buttercup` × 1, `plant.common_juniper` × 1, `plant.goldenrod` × 1, `plant.meadow_grass` × 2. U=0 named + 81 generated; exact kinds/locators below. | Field footprint roots **ACCIDENT**; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: redSaltbox` ([StPeters.unity:19746](../../Assets/_Project/Scenes/StPeters.unity#L19746)). Footprint and field reconstruction: evidence 5–6; external growth exclusion: evidence 1. |
| St Peters **StPetersCannery**, (170, 16)<br>[Village_stPetersCannery.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_stPetersCannery.prefab) | 1 baked lifecycle growth treatment (`lcWeed`/`lcMoss`; individual count and plant IDs not serialized): **INTENTIONAL**. Field: N=13, G=0; `plant.buttercup` × 1, `plant.fireweed` × 3, `plant.goldenrod` × 2, `plant.meadow_grass` × 2, `plant.queen_anne` × 1, `plant.raspberry` × 2, `plant.speckled_alder` × 1, `plant.timothy` × 1. U=184 named + 61 generated; exact kinds/locators below. Plus 4 tree rectangle candidates. | Field footprint roots **ACCIDENT**; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: StPetersCannery` ([StPeters.unity:27742](../../Assets/_Project/Scenes/StPeters.unity#L27742)). Cannery decay / meadow exclusion: evidence 1–2. |
| St Peters **postOffice**, (15, 43)<br>No matching building prefab; serialized scene object | Field: N=0, G=22; `plant.buttercup` × 2, `plant.fireweed` × 1, `plant.goldenrod` × 3, `plant.lupin` × 4, `plant.meadow_grass` × 4, `plant.oxeye_daisy` × 2, `plant.serviceberry` × 1, `plant.timothy` × 5. U=0 named + 124 generated; exact kinds/locators below. Plus 24 possible elevation-dependent slots in the expanded search box. | Field footprint roots **ACCIDENT**; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: postOffice` ([StPeters.unity:43318](../../Assets/_Project/Scenes/StPeters.unity#L43318)). Footprint and field reconstruction: evidence 5–6; external growth exclusion: evidence 1. |
| St Peters **school**, (-11.4, 35.423)<br>[Village_school.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_school.prefab) | Possible baked roof-moss treatment, count/plant IDs not serialized: **UNSURE**. Field: N=0, G=19; `plant.buttercup` × 1, `plant.goldenrod` × 3, `plant.lupin` × 1, `plant.meadow_grass` × 4, `plant.queen_anne` × 3, `plant.raspberry` × 2, `plant.timothy` × 4, `plant.wild_rose` × 1. U=0 named + 116 generated; exact kinds/locators below. | Field footprint roots **ACCIDENT**; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: school` ([StPeters.unity:46603](../../Assets/_Project/Scenes/StPeters.unity#L46603)). Footprint and field reconstruction: evidence 5–6; external growth exclusion: evidence 1. |
| St Peters **sageCottage**, (-21.5, 41.643)<br>[Village_sageCottage.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_sageCottage.prefab) | Possible baked roof-moss treatment, count/plant IDs not serialized: **UNSURE**. Field: N=0, G=8; `plant.buttercup` × 2, `plant.fireweed` × 1, `plant.lupin` × 1, `plant.meadow_grass` × 1, `plant.oxeye_daisy` × 1, `plant.wild_raisin` × 1, `plant.wild_rose` × 1. U=0 named + 80 generated; exact kinds/locators below. Plus 3 possible elevation-dependent slots in the expanded search box. | Field footprint roots **ACCIDENT**; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: sageCottage` ([StPeters.unity:74168](../../Assets/_Project/Scenes/StPeters.unity#L74168)). Footprint and field reconstruction: evidence 5–6; external growth exclusion: evidence 1. |
| St Peters **whiteFarmhouse**, (21, 26)<br>[Village_whiteFarmhouse.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_whiteFarmhouse.prefab) | Possible baked roof-moss treatment, count/plant IDs not serialized: **UNSURE**. Field: N=0, G=19; `plant.buttercup` × 3, `plant.fireweed` × 2, `plant.goldenrod` × 1, `plant.lupin` × 1, `plant.oxeye_daisy` × 2, `plant.queen_anne` × 2, `plant.raspberry` × 2, `plant.timothy` × 4, `plant.wild_rose` × 2. U=0 named + 141 generated; exact kinds/locators below. | Field footprint roots **ACCIDENT**; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: whiteFarmhouse` ([StPeters.unity:90603](../../Assets/_Project/Scenes/StPeters.unity#L90603)). Footprint and field reconstruction: evidence 5–6; external growth exclusion: evidence 1. |
| St Peters **structure.stp_net_loft**, (183.2, 22.8)<br>[Village_stPetersNetLoft.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_stPetersNetLoft.prefab) | Field: N=0, G=0; none. U=59 named + 0 generated; exact kinds/locators below. | No field footprint roots; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: structure.stp_net_loft` ([StPeters.unity:136905](../../Assets/_Project/Scenes/StPeters.unity#L136905)). Footprint and field reconstruction: evidence 5–6; external growth exclusion: evidence 1. |
| St Peters **structure.stp_net_shed**, (187.7, 9.3)<br>[Village_stPetersNetShed.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_stPetersNetShed.prefab) | Field: N=0, G=0; none. U=54 named + 0 generated; exact kinds/locators below. | No field footprint roots; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: structure.stp_net_shed` ([StPeters.unity:137068](../../Assets/_Project/Scenes/StPeters.unity#L137068)). Footprint and field reconstruction: evidence 5–6; external growth exclusion: evidence 1. |
| St Peters **structure.stp_bait_store**, (182.3, -8.8)<br>[Village_stPetersBaitStore.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_stPetersBaitStore.prefab) | Field: N=0, G=0; none. U=45 named + 18 generated; exact kinds/locators below. Plus 3 possible elevation-dependent slots in the expanded search box. | No field footprint roots; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: structure.stp_bait_store` ([StPeters.unity:139007](../../Assets/_Project/Scenes/StPeters.unity#L139007)). Footprint and field reconstruction: evidence 5–6; external growth exclusion: evidence 1. |
| St Peters **creamSaltbox**, (28.5, 35.704)<br>[Village_creamSaltbox.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_creamSaltbox.prefab) | Possible baked roof-moss treatment, count/plant IDs not serialized: **UNSURE**. Field: N=0, G=22; `plant.buttercup` × 3, `plant.fireweed` × 2, `plant.goldenrod` × 3, `plant.lupin` × 3, `plant.oxeye_daisy` × 4, `plant.queen_anne` × 3, `plant.raspberry` × 2, `plant.timothy` × 2. U=0 named + 118 generated; exact kinds/locators below. Plus 11 possible elevation-dependent slots in the expanded search box. | Field footprint roots **ACCIDENT**; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: creamSaltbox` ([StPeters.unity:140062](../../Assets/_Project/Scenes/StPeters.unity#L140062)). Footprint and field reconstruction: evidence 5–6; external growth exclusion: evidence 1. |
| St Peters **harbourGable**, (-6.25, 46.125)<br>[Village_harbourGable.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_harbourGable.prefab) | Possible baked roof-moss treatment, count/plant IDs not serialized: **UNSURE**. Field: N=0, G=29; `plant.common_juniper` × 1, `plant.goldenrod` × 1, `plant.lupin` × 4, `plant.meadow_grass` × 9, `plant.oxeye_daisy` × 3, `plant.queen_anne` × 1, `plant.raspberry` × 1, `plant.red_elderberry` × 1, `plant.serviceberry` × 1, `plant.timothy` × 6, `plant.wild_rose` × 1. U=0 named + 153 generated; exact kinds/locators below. Plus 7 possible elevation-dependent slots in the expanded search box. | Field footprint roots **ACCIDENT**; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: harbourGable` ([StPeters.unity:140848](../../Assets/_Project/Scenes/StPeters.unity#L140848)). Footprint and field reconstruction: evidence 5–6; external growth exclusion: evidence 1. |
| St Peters **whiteGable**, (53, 35.204)<br>[Village_whiteGable.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_whiteGable.prefab) | Possible baked roof-moss treatment, count/plant IDs not serialized: **UNSURE**. Field: N=0, G=18; `plant.buttercup` × 2, `plant.fireweed` × 1, `plant.goldenrod` × 1, `plant.lupin` × 3, `plant.meadow_grass` × 2, `plant.oxeye_daisy` × 2, `plant.queen_anne` × 2, `plant.raspberry` × 1, `plant.timothy` × 3, `plant.wild_rose` × 1. U=39 named + 52 generated; exact kinds/locators below. Plus 15 possible elevation-dependent slots in the expanded search box. | Field footprint roots **ACCIDENT**; external picture candidates **UNSURE**. Baked verdict as stated. | `m_Name: whiteGable` ([StPeters.unity:141634](../../Assets/_Project/Scenes/StPeters.unity#L141634)). Footprint and field reconstruction: evidence 5–6; external growth exclusion: evidence 1. |

The nine village footprints contain **155 generated field roots** in this reconstruction. The cannery additionally contains **13 named field roots**. These are external plants; keeping baked weeds, suspected moss or planters does not require keeping these root intrusions. Conversely, this count is not a deletion script or proof of visible roof coverage.

### Deliberate yard planting near those buildings

| Building | Plant decoration and position | Verdict and evidence |
|---|---|---|
| St Peters general store | `prop.stp_vl_general_store_barrel_planter`; 1 `barrelPlanter` decoration at (-0.5, 29.985); yard source, no species ID | **INTENTIONAL**, `Kind: "barrelPlanter"` ([yard.st_peters.general_store.asset:27](../../Assets/_Project/Data/Regions/StPetersVillage/yard.st_peters.general_store.asset#L27)) and its adjacent PieceId/Position. Authored beside the house, not a field weed. |
| St Peters general store | `prop.stp_vl_general_store_barrel_planter_2`; 1 `barrelPlanter` decoration at (5.5, 29.985); yard source, no species ID | **INTENTIONAL**, `Kind: "barrelPlanter"` ([yard.st_peters.general_store.asset:27](../../Assets/_Project/Data/Regions/StPetersVillage/yard.st_peters.general_store.asset#L27)) and its adjacent PieceId/Position. Authored beside the house, not a field weed. |
| St Peters sage cottage | `prop.stp_vl_sage_cottage_dory_planter`; 1 `doryPlanter` decoration at (-24.3, 36.38); yard source, no species ID | **INTENTIONAL**, `Kind: "doryPlanter"` ([yard.st_peters.sage_cottage.asset:39](../../Assets/_Project/Data/Regions/StPetersVillage/yard.st_peters.sage_cottage.asset#L39)) and its adjacent PieceId/Position. Authored beside the house, not a field weed. |
| St Peters white gable | `prop.stp_vl_white_gable_veg_rows`; 1 `vegRows` decoration at (57.6, 38.785); yard source, no species ID | **INTENTIONAL**, `Kind: "vegRows"` ([yard.st_peters.white_gable.asset:45](../../Assets/_Project/Data/Regions/StPetersVillage/yard.st_peters.white_gable.asset#L45)) and its adjacent PieceId/Position. Authored beside the house, not a field weed. |

## Other region scenes

| Region / building / prefab / position | Plant count and kind; source | Verdict and evidence |
|---|---|---|
| Nine Mile Creek **redSaltbox**, (-148, 196)<br>[Village_redSaltbox.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_redSaltbox.prefab) | 0 separate plant rectangle candidates: ; possible classic-renderer roof moss, no individual count or IDs.. | **UNSURE**. `m_Name: redSaltbox` ([NineMileCreek.unity:26044](../../Assets/_Project/Scenes/NineMileCreek.unity#L26044)). No identified vegetation child; rectangle proximity does not establish a roof garden. |
| Nine Mile Creek **sageCottage**, (-208, 60)<br>[Village_sageCottage.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_sageCottage.prefab) | 0 separate plant rectangle candidates: ; possible classic-renderer roof moss, no individual count or IDs.. | **UNSURE**. `m_Name: sageCottage` ([NineMileCreek.unity:222914](../../Assets/_Project/Scenes/NineMileCreek.unity#L222914)). No identified vegetation child; rectangle proximity does not establish a roof garden. |
| Nine Mile Creek **structure.nmc_cold_store**, (64, 99.2)<br>No matching building prefab; serialized scene object | 1 separate plant rectangle candidates: [SugarKelp [fileID 881506416]](../../Assets/_Project/Scenes/NineMileCreek.unity#L145888) (`SugarKelp_summer_full_v0`). | **UNSURE**. `m_Name: structure.nmc_cold_store` ([NineMileCreek.unity:376375](../../Assets/_Project/Scenes/NineMileCreek.unity#L376375)). No identified vegetation child; rectangle proximity does not establish a roof garden. |
| Nine Mile Creek **NmcFishPlant**, (-265, -40)<br>[NmcFishPlant.prefab](../../Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab) | 1 separate plant rectangle candidates: [WhiteBirch_1 [fileID 101093748]](../../Assets/_Project/Scenes/NineMileCreek.unity#L13211) (`WhiteBirch_mature_summer`); 2 generated grass candidates before height validation: `grass(42,117,0)` (`meadow`), `grass(43,115,0)` (`meadow`). | **UNSURE**. [NineMileCreek.unity:438076](../../Assets/_Project/Scenes/NineMileCreek.unity#L438076) serializes this prefab placement. No identified vegetation child; rectangle proximity does not establish a roof garden. |
| Nine Mile Creek **NmcCommunityHall**, (-153, 62.5)<br>[NmcCommunityHall.prefab](../../Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcCommunityHall.prefab) | 3 separate plant rectangle candidates: [RedOak_10 [fileID 1131412072]](../../Assets/_Project/Scenes/NineMileCreek.unity#L410615) (`RedOak_mature_summer`), [Meadowsweet_26 [fileID 701717205]](../../Assets/_Project/Scenes/NineMileCreek.unity#L410985) (`Meadowsweet_full_atGreen`), [RedOak_11 [fileID 865912643]](../../Assets/_Project/Scenes/NineMileCreek.unity#L410801) (`RedOak_mature_summer`); 1 generated grass candidates before height validation: `grass(115,165,0)` (`sward`). | **UNSURE**. [NineMileCreek.unity:438177](../../Assets/_Project/Scenes/NineMileCreek.unity#L438177) serializes this prefab placement. No identified vegetation child; rectangle proximity does not establish a roof garden. |
| Nine Mile Creek **NmcBoatGearWholesaler**, (-148.4, 235.9)<br>[NmcBoatGearWholesaler.prefab](../../Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcBoatGearWholesaler.prefab) | 1 separate plant rectangle candidates: [WhiteBirch_1 [fileID 1794346413]](../../Assets/_Project/Scenes/NineMileCreek.unity#L307713) (`WhiteBirch_mature_summer`). | **UNSURE**. [NineMileCreek.unity:438243](../../Assets/_Project/Scenes/NineMileCreek.unity#L438243) serializes this prefab placement. No identified vegetation child; rectangle proximity does not establish a roof garden. |
| Greywick **ShipwrightShed**, (-8, 3)<br>No building prefab; [ShipwrightShed.png.meta](../../Assets/_Project/Art/Sprites/Buildings/ShipwrightShed.png.meta) | 1 separate legacy tree rectangle candidates: [Tree34 [fileID 1095741838]](../../Assets/_Project/Scenes/Greywick.unity#L2659). Tree asset names identify variants, not botanical species; no plant-field IDs. | **UNSURE**. `m_Name: ShipwrightShed` ([Greywick.unity:188](../../Assets/_Project/Scenes/Greywick.unity#L188)). Trees have separate scene transforms; no authored growth/decay link found. |
| Greywick **ShipwrightDoryYard**, (-8, 8)<br>No building prefab; [ShipwrightShed.png.meta](../../Assets/_Project/Art/Sprites/Buildings/ShipwrightShed.png.meta) | 2 separate legacy tree rectangle candidates: [Tree34 [fileID 1095741838]](../../Assets/_Project/Scenes/Greywick.unity#L2659), [Tree05 [fileID 1964713807]](../../Assets/_Project/Scenes/Greywick.unity#L4324). Tree asset names identify variants, not botanical species; no plant-field IDs. | **UNSURE**. `m_Name: ShipwrightDoryYard` ([Greywick.unity:2095](../../Assets/_Project/Scenes/Greywick.unity#L2095)). Trees have separate scene transforms; no authored growth/decay link found. |
| Greywick **HarbourmasterOffice**, (-12, 9)<br>No building prefab; [NineMileCreekHouseRed.png.meta](../../Assets/_Project/Art/Sprites/Buildings/NineMileCreekHouseRed.png.meta) | 1 separate legacy tree rectangle candidates: [Tree01 [fileID 1231305635]](../../Assets/_Project/Scenes/Greywick.unity#L2843). Tree asset names identify variants, not botanical species; no plant-field IDs. | **UNSURE**. `m_Name: HarbourmasterOffice` ([Greywick.unity:4134](../../Assets/_Project/Scenes/Greywick.unity#L4134)). Trees have separate scene transforms; no authored growth/decay link found. |

### Coverage, including negative findings

| Scene | What was covered | Result |
|---|---|---|
| StPeters | 17 saved building shells: nine village buildings, cannery, three wharf stores, four excluded cottage-lot buildings; field, standalone shore plants, trees and named yard planting | 13 in-scope building rows above; four observation-only rows below. |
| NineMileCreek | Three legacy/village shells, fuel store/canopy, harbourmaster trailer, fish market, restaurant, 11 wharf sheds/stores, yard shed and four prefab shells: **24 physical building/shelter placements** | Candidate rows above. The extra `seeThrough` interior/ghost is the same fuel canopy, not another building; its artificial fallback rectangle is excluded. Generated grass has no root inside the tested interior footprints after the density/proximity screen; height and final rendering remain unverified. |
| Greywick | Seven building/stall placements, all 11 legacy tree sprite objects | Three possible-overlap rows above; four have no separate tree rectangle overlap. |
| SouthWater | [SouthWater.unity](../../Assets/_Project/Scenes/SouthWater.unity); all serialized GameObjects, transforms, components, sprite references and prefab references | No serialized building-and-flora overlap found. This does not certify unplaced content or raster imagery. |
| RicePoint | [RicePoint.unity](../../Assets/_Project/Scenes/RicePoint.unity); all serialized GameObjects, transforms, components, sprite references and prefab references | No serialized building-and-flora overlap found. This does not certify unplaced content or raster imagery. |
| Greybox | [Greybox.unity](../../Assets/_Project/Scenes/Greybox.unity); all serialized GameObjects, transforms, components, sprite references and prefab references | No serialized building-and-flora overlap found. This does not certify unplaced content or raster imagery. |
| EastWater | [EastWater.unity](../../Assets/_Project/Scenes/EastWater.unity); all serialized GameObjects, transforms, components, sprite references and prefab references | No serialized building-and-flora overlap found. This does not certify unplaced content or raster imagery. |
| Cumberland | [Cumberland.unity](../../Assets/_Project/Scenes/Cumberland.unity); all serialized GameObjects, transforms, components, sprite references and prefab references | No serialized building-and-flora overlap found. This does not certify unplaced content or raster imagery. |

Also inspected `Assets/Scenes/SampleScene.unity` and `Assets/Settings/Scenes/URP2DSceneTemplate.unity`: two objects each, no building/flora pairing. No additional region `.unity` files were present. A region Def without a corresponding scene is not evidence of an instantiated building.

Nine Mile Creek zero-candidate shells: legacy `GeneralStore`; fuel store/canopy; harbourmaster trailer; fish market; restaurant; the eight named wharf sheds, bait shed and trap store; yard shed; fishing-gear wholesaler. Greywick zero-candidate shells: red house, teal house, fish-buyer stall, general store. “Zero” refers to this separate-plant screen, not a claim that every pixel is botanically empty.

### Every building prefab, including unused templates

All **148 prefab files** were scanned by GameObject hierarchy, component type, sprite GUID and vegetation names/paths. No dedicated plant child was found under the following building/shelter prefabs. Painted growth is evaluated separately; a one-object prefab can still contain baked weeds. Root position `(0, 0)` means a template, not a placed building.

| Prefab (root at local 0, 0) | Region use / plant source / verdict |
|---|---|
| [NmcBoatGearWholesaler.prefab](../../Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcBoatGearWholesaler.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [NmcCommunityHall.prefab](../../Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcCommunityHall.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [NmcFishPlant.prefab](../../Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [NmcFishingGearWholesaler.prefab](../../Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishingGearWholesaler.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [Village_creamSaltbox.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_creamSaltbox.prefab) | Village template; possible baked roof moss **UNSURE** (evidence 4), no plant IDs. 0 dedicated plant children. `Village_generalStore` is not the saved St Peters shopfront. |
| [Village_generalStore.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_generalStore.prefab) | Village template; possible baked roof moss **UNSURE** (evidence 4), no plant IDs. 0 dedicated plant children. `Village_generalStore` is not the saved St Peters shopfront. |
| [Village_ginnyLeanTo.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_ginnyLeanTo.prefab) | Excluded cottage lot; baked lifecycle growth documented, no cleanup verdict. 0 dedicated plant children. |
| [Village_ginnyNetStore.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_ginnyNetStore.prefab) | Excluded cottage lot; baked lifecycle growth documented, no cleanup verdict. 0 dedicated plant children. |
| [Village_ginnyWoodshed.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_ginnyWoodshed.prefab) | Excluded cottage lot; baked lifecycle growth documented, no cleanup verdict. 0 dedicated plant children. |
| [Village_harbourGable.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_harbourGable.prefab) | Village template; possible baked roof moss **UNSURE** (evidence 4), no plant IDs. 0 dedicated plant children. `Village_generalStore` is not the saved St Peters shopfront. |
| [Village_redSaltbox.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_redSaltbox.prefab) | Village template; possible baked roof moss **UNSURE** (evidence 4), no plant IDs. 0 dedicated plant children. `Village_generalStore` is not the saved St Peters shopfront. |
| [Village_sageCottage.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_sageCottage.prefab) | Village template; possible baked roof moss **UNSURE** (evidence 4), no plant IDs. 0 dedicated plant children. `Village_generalStore` is not the saved St Peters shopfront. |
| [Village_school.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_school.prefab) | Village template; possible baked roof moss **UNSURE** (evidence 4), no plant IDs. 0 dedicated plant children. `Village_generalStore` is not the saved St Peters shopfront. |
| [Village_stPetersBaitStore.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_stPetersBaitStore.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [Village_stPetersCannery.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_stPetersCannery.prefab) | St Peters cannery; baked lifecycle weeds/moss **INTENTIONAL** (evidence 2); 0 dedicated plant children. |
| [Village_stPetersIceHouse.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_stPetersIceHouse.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [Village_stPetersNetLoft.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_stPetersNetLoft.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [Village_stPetersNetShed.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_stPetersNetShed.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [Village_whiteFarmhouse.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_whiteFarmhouse.prefab) | Village template; possible baked roof moss **UNSURE** (evidence 4), no plant IDs. 0 dedicated plant children. `Village_generalStore` is not the saved St Peters shopfront. |
| [Village_whiteGable.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_whiteGable.prefab) | Village template; possible baked roof moss **UNSURE** (evidence 4), no plant IDs. 0 dedicated plant children. `Village_generalStore` is not the saved St Peters shopfront. |
| [canopy_sB2.prefab](../../Assets/_Project/Prefabs/GasStation/canopy_sB2.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [canopy_sB3.prefab](../../Assets/_Project/Prefabs/GasStation/canopy_sB3.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [canopy_sB4.prefab](../../Assets/_Project/Prefabs/GasStation/canopy_sB4.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [interior_sKiosk.prefab](../../Assets/_Project/Prefabs/GasStation/interior_sKiosk.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [interior_sStore.prefab](../../Assets/_Project/Prefabs/GasStation/interior_sStore.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [store_sKiosk.prefab](../../Assets/_Project/Prefabs/GasStation/store_sKiosk.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |
| [store_sStore.prefab](../../Assets/_Project/Prefabs/GasStation/store_sStore.prefab) | 0 dedicated plant children; no text evidence of attached growth. No separate-plant preservation exception found. Unplaced templates have no world-overlap count. |

### Unplaced authored content is not evidence of a current overlap

The north-creek, south-shore and main-street KeyScene assets each say `Placed: 0`. Their window-box/planter descriptions still express intentional design and must not be converted into a “remove all greenery” rule, but they do not prove those buildings exist in the saved scene. For example: `Placed: 0` ([KeyScene_nmc_main_street.asset:20](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L20)) and the flower shop’s explicit `planter` at [KeyScene_nmc_main_street.asset:194](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_main_street.asset#L194); south-shore house 1’s `xfurn` planters and its saved intended location appear in [KeyScene_nmc_south_shore.asset:22](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_south_shore.asset#L22). North-creek window boxes are named in [KeyScene_nmc_north_creek.asset:125](../../Assets/_Project/Data/KeyScenes/KeyScene_nmc_north_creek.asset#L125). They have building/decoration IDs, not plant-field site IDs. Green roof colours alone were not treated as sod or living moss.

## Ginny’s lot — observations only, excluded

The whole lot is excluded, including its cottage, sheds and field clusters. An open cottage-interior change also exists; this audit changes none of it. The figures below are spatial observations only. Dense clusters and ruined building states are not interpreted as permission to move or retain particular field rows.

| Building / prefab / saved position | What the text contains | Source |
|---|---|---|
| **sageCottage** (84, 28)<br>[Village_sageCottage.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_sageCottage.prefab) | 0 named roots inside the tested footprint; 324 additional named picture/envelope candidates. Kinds: `plant.buttercup` × 25, `plant.fireweed` × 5, `plant.goldenrod` × 11, `plant.lupin` × 66, `plant.meadow_grass` × 119, `plant.oxeye_daisy` × 46, `plant.queen_anne` × 7, `plant.raspberry` × 2, `plant.timothy` × 43. All belong to `plant.stp_ginnys_plot_*`; not individually classified. Possible cottage roof moss; KeyScene also requests window boxes and a trellis vine. | `m_Name: sageCottage` ([StPeters.unity:20066](../../Assets/_Project/Scenes/StPeters.unity#L20066)); [VillageBuildingKit.cs:558](../../Assets/_Project/Art/Editor/VillageBuildingKit.cs#L558) records neglected woodshed / ruin net store / collapsing lean-to. |
| **netStore** (95, 22)<br>[Village_ginnyNetStore.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_ginnyNetStore.prefab) | 7 named roots inside the tested footprint; 247 additional named picture/envelope candidates. Kinds: `plant.beaked_hazelnut` × 4, `plant.buttercup` × 5, `plant.fireweed` × 22, `plant.goldenrod` × 10, `plant.lupin` × 26, `plant.meadow_grass` × 109, `plant.meadowsweet` × 3, `plant.oxeye_daisy` × 22, `plant.queen_anne` × 4, `plant.raspberry` × 6, `plant.serviceberry` × 2, `plant.steeplebush` × 2, `plant.timothy` × 35, `plant.wild_raisin` × 1, `plant.wild_rose` × 1, `shoreplant.sweet_fern` × 2. All belong to `plant.stp_ginnys_plot_*`; not individually classified. Lifecycle state explicitly recorded; weeds/moss belong to its bake, with no standalone plant IDs. | `m_Name: netStore` ([StPeters.unity:24823](../../Assets/_Project/Scenes/StPeters.unity#L24823)); [VillageBuildingKit.cs:558](../../Assets/_Project/Art/Editor/VillageBuildingKit.cs#L558) records neglected woodshed / ruin net store / collapsing lean-to. |
| **woodshed** (93, 34)<br>[Village_ginnyWoodshed.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_ginnyWoodshed.prefab) | 18 named roots inside the tested footprint; 273 additional named picture/envelope candidates. Kinds: `plant.beaked_hazelnut` × 2, `plant.buttercup` × 17, `plant.fireweed` × 6, `plant.goldenrod` × 12, `plant.lupin` × 70, `plant.meadow_grass` × 101, `plant.meadowsweet` × 1, `plant.oxeye_daisy` × 24, `plant.queen_anne` × 5, `plant.raspberry` × 2, `plant.serviceberry` × 4, `plant.steeplebush` × 1, `plant.timothy` × 46. All belong to `plant.stp_ginnys_plot_*`; not individually classified. Lifecycle state explicitly recorded; weeds/moss belong to its bake, with no standalone plant IDs. | `m_Name: woodshed` ([StPeters.unity:36785](../../Assets/_Project/Scenes/StPeters.unity#L36785)); [VillageBuildingKit.cs:558](../../Assets/_Project/Art/Editor/VillageBuildingKit.cs#L558) records neglected woodshed / ruin net store / collapsing lean-to. |
| **leanTo** (74, 33)<br>[Village_ginnyLeanTo.prefab](../../Assets/_Project/Prefabs/Decor/Buildings/Village/Village_ginnyLeanTo.prefab) | 18 named roots inside the tested footprint; 365 additional named picture/envelope candidates. Kinds: `plant.beaked_hazelnut` × 1, `plant.buttercup` × 26, `plant.fireweed` × 12, `plant.goldenrod` × 25, `plant.lupin` × 58, `plant.meadow_grass` × 139, `plant.oxeye_daisy` × 48, `plant.queen_anne` × 13, `plant.raspberry` × 7, `plant.serviceberry` × 1, `plant.steeplebush` × 1, `plant.timothy` × 41, `plant.wild_raisin` × 1, `plant.wild_rose` × 8, `shoreplant.sweet_fern` × 2. All belong to `plant.stp_ginnys_plot_*`; not individually classified. Lifecycle state explicitly recorded; weeds/moss belong to its bake, with no standalone plant IDs. | `m_Name: leanTo` ([StPeters.unity:109502](../../Assets/_Project/Scenes/StPeters.unity#L109502)); [VillageBuildingKit.cs:558](../../Assets/_Project/Art/Editor/VillageBuildingKit.cs#L558) records neglected woodshed / ruin net store / collapsing lean-to. |

The lot’s KeyScene requests `- Key: windowBoxes` ([KeyScene_stp_ginnys_plot.asset:78](../../Assets/_Project/Data/KeyScenes/KeyScene_stp_ginnys_plot.asset#L78)) and `Piece: trellisVine` ([KeyScene_stp_ginnys_plot.asset:725](../../Assets/_Project/Data/KeyScenes/KeyScene_stp_ginnys_plot.asset#L725)). Its shed preset names do not all match the baked `Buildings.json` preset names; the audit uses the actual scene sprite and bake dimensions, not an assumed replacement. A red-maple rectangle also reaches the net-store picture: [RedMaple_2 [fileID 1771812916]](../../Assets/_Project/Scenes/StPeters.unity#L96512). No action is proposed for it.

## Exact field locators and kinds

These locators reproduce the table and the hold list. `cell(x,y)` and `grass(x,y,slot)` are **audit notation, not serialized IDs**. St Peters cell origin/step/seed are stated in evidence 5. Named sites link to their literal `Id` row. For generated plants, the common file/line evidence is the ground payload, recipe choice, scatter and clump code cited above; individual cells are not separate YAML rows. Envelope-only candidates are deliberately conservative. Baked growth has no entries here.

### St Peters generalStore (2.5, 35.422)

**ACCIDENT — roots inside ground footprint:** `plant.fireweed` × 1, `plant.lupin` × 4, `plant.meadow_grass` × 1, `plant.oxeye_daisy` × 2, `plant.queen_anne` × 2, `plant.staghorn_sumac` × 2, `plant.timothy` × 1.

Generated root cells: `cell(762,585)`, `cell(758,586)`, `cell(770,587)`, `cell(768,588)`, `cell(759,589)`, `cell(765,592)`, `cell(769,592)`, `cell(764,593)`, `cell(767,593)`, `cell(762,594)`, `cell(764,595)`, `cell(760,596)`, `cell(770,597)`.

**UNSURE — outside-footprint picture/envelope candidates:** `plant.beaked_hazelnut` × 1, `plant.buttercup` × 22, `plant.common_juniper` × 2, `plant.fireweed` × 11, `plant.goldenrod` × 9, `plant.lupin` × 41, `plant.meadow_grass` × 26, `plant.oxeye_daisy` × 17, `plant.queen_anne` × 20, `plant.raspberry` × 6, `plant.red_elderberry` × 1, `plant.serviceberry` × 4, `plant.staghorn_sumac` × 8, `plant.timothy` × 30, `plant.wild_rose` × 11.

Generated candidate cells, by kind (all UNSURE):

- `plant.beaked_hazelnut`: `cell(771,604)`.

- `plant.buttercup`: `cell(755,588)`, `cell(776,591)`, `cell(753,594)`, `cell(756,594)`, `cell(749,596)`, `cell(753,596)`, `cell(774,596)`, `cell(776,600)`, `cell(781,600)`, `cell(759,602)`, `cell(773,604)`, `cell(753,606)`, `cell(768,606)`, `cell(779,606)`, `cell(780,607)`, `cell(755,608)`, `cell(775,608)`, `cell(776,608)`, `cell(779,611)`, `cell(782,584)`, `cell(783,606)`, `cell(776,612)`.

- `plant.common_juniper`: `cell(774,598)`, `cell(756,613)`.

- `plant.fireweed`: `cell(754,581)`, `cell(756,588)`, `cell(754,593)`, `cell(749,595)`, `cell(775,598)`, `cell(752,604)`, `cell(781,604)`, `cell(752,605)`, `cell(775,610)`, `cell(745,582)`, `cell(783,604)`.

- `plant.goldenrod`: `cell(781,582)`, `cell(768,600)`, `cell(768,602)`, `cell(752,603)`, `cell(761,611)`, `cell(772,611)`, `cell(782,592)`, `cell(785,602)`, `cell(784,606)`.

- `plant.lupin`: `cell(751,583)`, `cell(776,584)`, `cell(754,586)`, `cell(752,588)`, `cell(780,592)`, `cell(756,593)`, `cell(755,596)`, `cell(776,596)`, `cell(763,597)`, `cell(775,597)`, `cell(777,597)`, `cell(753,598)`, `cell(760,598)`, `cell(761,598)`, `cell(765,599)`, `cell(777,599)`, `cell(774,600)`, `cell(778,600)`, `cell(773,601)`, `cell(775,601)`, `cell(776,601)`, `cell(767,602)`, `cell(771,602)`, `cell(757,603)`, `cell(762,603)`, `cell(778,604)`, `cell(756,605)`, `cell(767,605)`, `cell(772,605)`, `cell(762,609)`, `cell(773,609)`, `cell(783,584)`, `cell(745,587)`, `cell(784,589)`, `cell(784,591)`, `cell(784,605)`, `cell(744,611)`, `cell(755,613)`, `cell(761,613)`, `cell(781,613)`, `cell(785,613)`.

- `plant.meadow_grass`: `cell(772,582)`, `cell(755,590)`, `cell(750,592)`, `cell(771,598)`, `cell(776,598)`, `cell(776,599)`, `cell(780,599)`, `cell(764,601)`, `cell(771,601)`, `cell(762,602)`, `cell(765,602)`, `cell(757,604)`, `cell(763,604)`, `cell(770,607)`, `cell(747,608)`, `cell(768,608)`, `cell(782,579)`, `cell(745,581)`, `cell(782,582)`, `cell(783,587)`, `cell(745,589)`, `cell(783,589)`, `cell(785,607)`, `cell(747,611)`, `cell(760,612)`, `cell(778,612)`.

- `plant.oxeye_daisy`: `cell(756,587)`, `cell(780,597)`, `cell(751,602)`, `cell(779,602)`, `cell(762,605)`, `cell(777,605)`, `cell(755,606)`, `cell(781,606)`, `cell(761,607)`, `cell(774,608)`, `cell(758,609)`, `cell(759,610)`, `cell(783,588)`, `cell(784,601)`, `cell(746,608)`, `cell(754,612)`, `cell(784,612)`.

- `plant.queen_anne`: `cell(749,580)`, `cell(753,583)`, `cell(753,588)`, `cell(780,589)`, `cell(780,590)`, `cell(781,593)`, `cell(754,598)`, `cell(769,598)`, `cell(755,601)`, `cell(778,601)`, `cell(775,602)`, `cell(761,606)`, `cell(778,607)`, `cell(754,609)`, `cell(759,609)`, `cell(782,610)`, `cell(745,583)`, `cell(745,584)`, `cell(744,607)`, `cell(758,613)`.

- `plant.raspberry`: `cell(751,584)`, `cell(752,584)`, `cell(773,593)`, `cell(768,601)`, `cell(785,579)`, `cell(783,592)`.

- `plant.red_elderberry`: `cell(748,610)`.

- `plant.serviceberry`: `cell(770,583)`, `cell(769,597)`, `cell(785,600)`, `cell(746,612)`.

- `plant.staghorn_sumac`: `cell(749,585)`, `cell(761,600)`, `cell(774,602)`, `cell(760,608)`, `cell(763,608)`, `cell(788,581)`, `cell(743,606)`, `cell(780,613)`.

- `plant.timothy`: `cell(761,582)`, `cell(753,584)`, `cell(750,588)`, `cell(781,589)`, `cell(753,591)`, `cell(777,593)`, `cell(773,596)`, `cell(779,596)`, `cell(759,597)`, `cell(765,597)`, `cell(766,597)`, `cell(767,600)`, `cell(769,600)`, `cell(774,601)`, `cell(763,605)`, `cell(759,608)`, `cell(760,609)`, `cell(777,609)`, `cell(752,610)`, `cell(777,610)`, `cell(745,586)`, `cell(785,592)`, `cell(744,598)`, `cell(744,599)`, `cell(783,605)`, `cell(745,606)`, `cell(746,610)`, `cell(784,610)`, `cell(750,612)`, `cell(749,614)`.

- `plant.wild_rose`: `cell(756,590)`, `cell(777,594)`, `cell(758,600)`, `cell(766,600)`, `cell(751,601)`, `cell(780,604)`, `cell(755,607)`, `cell(783,590)`, `cell(744,600)`, `cell(745,610)`, `cell(785,612)`.

### St Peters redSaltbox (25, 8)

**ACCIDENT — roots inside ground footprint:** `plant.buttercup` × 1, `plant.common_juniper` × 1, `plant.goldenrod` × 1, `plant.meadow_grass` × 2.

Generated root cells: `cell(811,532)`, `cell(811,534)`, `cell(811,537)`, `cell(809,538)`, `cell(812,540)`.

**UNSURE — outside-footprint picture/envelope candidates:** `plant.beaked_hazelnut` × 3, `plant.buttercup` × 5, `plant.fireweed` × 4, `plant.goldenrod` × 7, `plant.lupin` × 15, `plant.meadow_grass` × 9, `plant.oxeye_daisy` × 8, `plant.queen_anne` × 8, `plant.raspberry` × 2, `plant.red_elderberry` × 2, `plant.staghorn_sumac` × 1, `plant.timothy` × 11, `plant.wild_raisin` × 2, `plant.wild_rose` × 4.

Generated candidate cells, by kind (all UNSURE):

- `plant.beaked_hazelnut`: `cell(800,530)`, `cell(796,522)`, `cell(827,525)`.

- `plant.buttercup`: `cell(818,528)`, `cell(801,540)`, `cell(796,542)`, `cell(803,552)`, `cell(793,548)`.

- `plant.fireweed`: `cell(793,524)`, `cell(795,536)`, `cell(795,540)`, `cell(793,542)`.

- `plant.goldenrod`: `cell(817,528)`, `cell(809,543)`, `cell(818,543)`, `cell(818,544)`, `cell(796,552)`, `cell(793,550)`, `cell(801,554)`.

- `plant.lupin`: `cell(820,535)`, `cell(819,544)`, `cell(800,551)`, `cell(815,523)`, `cell(798,524)`, `cell(802,524)`, `cell(799,526)`, `cell(825,536)`, `cell(796,539)`, `cell(795,541)`, `cell(794,545)`, `cell(795,551)`, `cell(795,553)`, `cell(793,554)`, `cell(802,554)`.

- `plant.meadow_grass`: `cell(816,541)`, `cell(823,544)`, `cell(798,548)`, `cell(800,524)`, `cell(805,524)`, `cell(825,529)`, `cell(825,530)`, `cell(825,548)`, `cell(793,555)`.

- `plant.oxeye_daisy`: `cell(819,542)`, `cell(810,545)`, `cell(799,546)`, `cell(809,548)`, `cell(807,553)`, `cell(814,524)`, `cell(794,541)`, `cell(824,544)`.

- `plant.queen_anne`: `cell(800,539)`, `cell(798,544)`, `cell(800,545)`, `cell(800,548)`, `cell(799,549)`, `cell(813,552)`, `cell(825,527)`, `cell(799,554)`.

- `plant.raspberry`: `cell(814,523)`, `cell(816,523)`.

- `plant.red_elderberry`: `cell(804,550)`, `cell(793,549)`.

- `plant.staghorn_sumac`: `cell(792,535)`.

- `plant.timothy`: `cell(823,533)`, `cell(800,541)`, `cell(801,545)`, `cell(816,546)`, `cell(804,549)`, `cell(801,551)`, `cell(807,552)`, `cell(800,553)`, `cell(808,524)`, `cell(798,556)`, `cell(804,556)`.

- `plant.wild_raisin`: `cell(796,543)`, `cell(792,553)`.

- `plant.wild_rose`: `cell(798,539)`, `cell(823,528)`, `cell(824,530)`, `cell(810,554)`.

### St Peters StPetersCannery (170, 16)

**ACCIDENT — roots inside ground footprint:** `plant.buttercup` × 1, `plant.fireweed` × 3, `plant.goldenrod` × 2, `plant.meadow_grass` × 2, `plant.queen_anne` × 1, `plant.raspberry` × 2, `plant.speckled_alder` × 1, `plant.timothy` × 1.

| Named site | Kind | Root position | Quoted row |
|---|---|---|---|
| `plant.stp_east_end_353` | `plant.meadow_grass` | (166.83, 11.22) | `- Id: plant.stp_east_end_353` ([StPetersPlantField.asset:14616](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14616)) |
| `plant.stp_east_end_376` | `plant.meadow_grass` | (164.61, 12.57) | `- Id: plant.stp_east_end_376` ([StPetersPlantField.asset:14731](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14731)) |
| `plant.stp_east_end_386` | `plant.fireweed` | (165.05, 12.66) | `- Id: plant.stp_east_end_386` ([StPetersPlantField.asset:14781](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14781)) |
| `plant.stp_east_end_408` | `plant.buttercup` | (162.18, 14.3) | `- Id: plant.stp_east_end_408` ([StPetersPlantField.asset:14891](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14891)) |
| `plant.stp_east_end_409` | `plant.raspberry` | (163.08, 14.45) | `- Id: plant.stp_east_end_409` ([StPetersPlantField.asset:14896](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14896)) |
| `plant.stp_east_end_421` | `plant.queen_anne` | (161.98, 14.84) | `- Id: plant.stp_east_end_421` ([StPetersPlantField.asset:14956](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14956)) |
| `plant.stp_east_end_422` | `plant.timothy` | (162.46, 14.97) | `- Id: plant.stp_east_end_422` ([StPetersPlantField.asset:14961](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14961)) |
| `plant.stp_east_end_451` | `plant.raspberry` | (176.83, 16.91) | `- Id: plant.stp_east_end_451` ([StPetersPlantField.asset:15106](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15106)) |
| `plant.stp_east_end_458` | `plant.fireweed` | (176.35, 17.44) | `- Id: plant.stp_east_end_458` ([StPetersPlantField.asset:15141](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15141)) |
| `plant.stp_east_end_459` | `plant.fireweed` | (178.61, 17.22) | `- Id: plant.stp_east_end_459` ([StPetersPlantField.asset:15146](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15146)) |
| `plant.stp_east_end_467` | `plant.speckled_alder` | (177.35, 17.99) | `- Id: plant.stp_east_end_467` ([StPetersPlantField.asset:15186](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15186)) |
| `plant.stp_east_end_475` | `plant.goldenrod` | (175.86, 18.46) | `- Id: plant.stp_east_end_475` ([StPetersPlantField.asset:15226](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15226)) |
| `plant.stp_east_end_483` | `plant.goldenrod` | (175.12, 19.37) | `- Id: plant.stp_east_end_483` ([StPetersPlantField.asset:15266](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15266)) |

**UNSURE — outside-footprint picture/envelope candidates:** `plant.beaked_hazelnut` × 1, `plant.buttercup` × 8, `plant.common_juniper` × 2, `plant.fireweed` × 36, `plant.goldenrod` × 27, `plant.lupin` × 7, `plant.meadow_grass` × 40, `plant.oxeye_daisy` × 13, `plant.queen_anne` × 16, `plant.raspberry` × 22, `plant.serviceberry` × 6, `plant.soft_rush` × 2, `plant.speckled_alder` × 3, `plant.staghorn_sumac` × 4, `plant.timothy` × 29, `plant.tussock_sedge` × 8, `plant.wild_raisin` × 2, `plant.wild_rose` × 9, `shoreplant.bayberry` × 1, `shoreplant.beach_pea` × 3, `shoreplant.marram_grass` × 6.

| Kind | Exact named-site set | Literal first row / source |
|---|---|---|
| `plant.buttercup` × 4 | `plant.stp_east_end_` suffixes **277, 310, 331, 355** | `- Id: plant.stp_east_end_310` ([StPetersPlantField.asset:14401](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14401)) |
| `plant.fireweed` × 34 | `plant.stp_east_end_` suffixes **278, 330, 340, 348, 365, 377, 397, 398, 399, 432, 433, 452, 463, 486, 490, 519, 520, 529, 532, 533, 534, 536, 539, 552, 554, 555, 563, 566, 579, 580, 584, 586, 595, 607** | `- Id: plant.stp_east_end_330` ([StPetersPlantField.asset:14501](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14501)) |
| `plant.goldenrod` × 24 | `plant.stp_east_end_` suffixes **266, 281, 363, 364, 384, 385, 387, 388, 411, 431, 441, 478, 491, 493, 513, 517, 525, 528, 561, 564, 565, 590, 605, 606** | `- Id: plant.stp_east_end_363` ([StPetersPlantField.asset:14666](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14666)) |
| `plant.meadow_grass` × 31 | `plant.stp_east_end_` suffixes **276, 279, 280, 282, 292, 293, 311, 321, 322, 338, 339, 347, 354, 357, 373, 374, 375, 378, 419, 424, 439, 462, 476, 484, 507, 541, 556, 570, 578, 603, 616** | `- Id: plant.stp_east_end_311` ([StPetersPlantField.asset:14406](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14406)) |
| `plant.oxeye_daisy` × 4 | `plant.stp_east_end_` suffixes **332, 383, 430, 457** | `- Id: plant.stp_east_end_383` ([StPetersPlantField.asset:14766](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14766)) |
| `plant.queen_anne` × 13 | `plant.stp_east_end_` suffixes **264, 362, 367, 379, 468, 477, 492, 499, 547, 562, 569, 589, 596** | `- Id: plant.stp_east_end_362` ([StPetersPlantField.asset:14661](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14661)) |
| `plant.raspberry` × 18 | `plant.stp_east_end_` suffixes **263, 295, 341, 410, 440, 461, 485, 494, 500, 511, 527, 537, 551, 557, 567, 581, 588, 604** | `- Id: plant.stp_east_end_341` ([StPetersPlantField.asset:14556](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14556)) |
| `plant.soft_rush` × 2 | `plant.stp_east_end_` suffixes **380, 469** | `- Id: plant.stp_east_end_469` ([StPetersPlantField.asset:15196](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15196)) |
| `plant.speckled_alder` × 3 | `plant.stp_east_end_` suffixes **512, 560, 587** | `- Id: plant.stp_east_end_512` ([StPetersPlantField.asset:15411](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15411)) |
| `plant.staghorn_sumac` × 1 | `plant.stp_east_end_` suffixes **568** | `- Id: plant.stp_east_end_568` ([StPetersPlantField.asset:15691](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15691)) |
| `plant.timothy` × 24 | `plant.stp_east_end_` suffixes **265, 284, 294, 296, 297, 328, 356, 361, 366, 368, 389, 396, 400, 412, 413, 420, 437, 438, 450, 453, 474, 523, 545, 585** | `- Id: plant.stp_east_end_328` ([StPetersPlantField.asset:14491](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14491)) |
| `plant.tussock_sedge` × 8 | `plant.stp_east_end_` suffixes **352, 423, 495, 501, 518, 524, 526, 538** | `- Id: plant.stp_east_end_352` ([StPetersPlantField.asset:14611](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14611)) |
| `plant.wild_raisin` × 1 | `plant.stp_east_end_` suffixes **602** | `- Id: plant.stp_east_end_602` ([StPetersPlantField.asset:15861](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15861)) |
| `plant.wild_rose` × 7 | `plant.stp_east_end_` suffixes **329, 460, 540, 546, 553, 571, 608** | `- Id: plant.stp_east_end_329` ([StPetersPlantField.asset:14496](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14496)) |
| `shoreplant.bayberry` × 1 | `plant.stp_east_end_` suffixes **558** | `- Id: plant.stp_east_end_558` ([StPetersPlantField.asset:15641](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15641)) |
| `shoreplant.beach_pea` × 3 | `plant.stp_east_end_` suffixes **609, 610, 615** | `- Id: plant.stp_east_end_609` ([StPetersPlantField.asset:15896](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15896)) |
| `shoreplant.marram_grass` × 6 | `plant.stp_east_end_` suffixes **591, 597, 598, 599, 600, 617** | `- Id: plant.stp_east_end_597` ([StPetersPlantField.asset:15836](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15836)) |

Generated candidate cells, by kind (all UNSURE):

- `plant.beaked_hazelnut`: `cell(1068,551)`.

- `plant.buttercup`: `cell(1077,538)`, `cell(1073,539)`, `cell(1071,532)`, `cell(1069,545)`.

- `plant.common_juniper`: `cell(1071,541)`, `cell(1069,568)`.

- `plant.fireweed`: `cell(1072,532)`, `cell(1079,577)`.

- `plant.goldenrod`: `cell(1072,570)`, `cell(1069,547)`, `cell(1078,578)`.

- `plant.lupin`: `cell(1078,544)`, `cell(1068,561)`, `cell(1069,562)`, `cell(1069,566)`, `cell(1070,574)`, `cell(1070,577)`, `cell(1071,577)`.

- `plant.meadow_grass`: `cell(1073,543)`, `cell(1074,543)`, `cell(1078,575)`, `cell(1077,532)`, `cell(1073,533)`, `cell(1069,544)`, `cell(1068,560)`, `cell(1069,569)`, `cell(1071,571)`.

- `plant.oxeye_daisy`: `cell(1077,575)`, `cell(1077,531)`, `cell(1071,537)`, `cell(1068,545)`, `cell(1068,558)`, `cell(1070,571)`, `cell(1069,573)`, `cell(1071,575)`, `cell(1072,577)`.

- `plant.queen_anne`: `cell(1074,537)`, `cell(1078,541)`, `cell(1071,569)`.

- `plant.raspberry`: `cell(1076,548)`, `cell(1074,563)`, `cell(1077,568)`, `cell(1068,565)`.

- `plant.serviceberry`: `cell(1079,553)`, `cell(1079,555)`, `cell(1073,560)`, `cell(1068,563)`, `cell(1070,565)`, `cell(1072,579)`.

- `plant.staghorn_sumac`: `cell(1071,529)`, `cell(1075,529)`, `cell(1073,532)`.

- `plant.timothy`: `cell(1079,540)`, `cell(1078,565)`, `cell(1073,568)`, `cell(1071,531)`, `cell(1078,579)`.

- `plant.wild_raisin`: `cell(1078,570)`.

- `plant.wild_rose`: `cell(1073,565)`, `cell(1068,578)`.

### St Peters postOffice (15, 43)

**ACCIDENT — roots inside ground footprint:** `plant.buttercup` × 2, `plant.fireweed` × 1, `plant.goldenrod` × 3, `plant.lupin` × 4, `plant.meadow_grass` × 4, `plant.oxeye_daisy` × 2, `plant.serviceberry` × 1, `plant.timothy` × 5.

Generated root cells: `cell(785,600)`, `cell(784,601)`, `cell(787,601)`, `cell(790,601)`, `cell(795,601)`, `cell(785,602)`, `cell(792,602)`, `cell(795,602)`, `cell(783,604)`, `cell(787,604)`, `cell(783,605)`, `cell(784,605)`, `cell(787,605)`, `cell(789,605)`, `cell(783,606)`, `cell(784,606)`, `cell(785,607)`, `cell(790,608)`, `cell(784,610)`, `cell(786,610)`, `cell(787,610)`, `cell(788,610)`.

**UNSURE — outside-footprint picture/envelope candidates:** `plant.beaked_hazelnut` × 2, `plant.buttercup` × 19, `plant.common_juniper` × 5, `plant.fireweed` × 3, `plant.goldenrod` × 6, `plant.lupin` × 24, `plant.meadow_grass` × 11, `plant.oxeye_daisy` × 11, `plant.queen_anne` × 9, `plant.raspberry` × 4, `plant.red_elderberry` × 3, `plant.serviceberry` × 2, `plant.staghorn_sumac` × 2, `plant.timothy` × 15, `plant.tussock_sedge` × 2, `plant.wild_rose` × 6.

Generated candidate cells, by kind (all UNSURE):

- `plant.beaked_hazelnut`: `cell(780,619)`, `cell(771,604)`.

- `plant.buttercup`: `cell(774,596)`, `cell(776,600)`, `cell(781,600)`, `cell(779,606)`, `cell(780,607)`, `cell(775,608)`, `cell(776,608)`, `cell(800,609)`, `cell(779,611)`, `cell(776,612)`, `cell(797,613)`, `cell(779,614)`, `cell(793,615)`, `cell(780,618)`, `cell(781,622)`, `cell(785,622)`, `cell(805,596)`, `cell(807,603)`, `cell(773,604)`.

- `plant.common_juniper`: `cell(797,598)`, `cell(789,615)`, `cell(787,617)`, `cell(779,618)`, `cell(774,598)`.

- `plant.fireweed`: `cell(775,598)`, `cell(781,604)`, `cell(775,610)`.

- `plant.goldenrod`: `cell(798,600)`, `cell(783,617)`, `cell(782,592)`, `cell(795,594)`, `cell(772,611)`, `cell(772,618)`.

- `plant.lupin`: `cell(776,596)`, `cell(786,596)`, `cell(775,597)`, `cell(777,597)`, `cell(777,599)`, `cell(774,600)`, `cell(778,600)`, `cell(775,601)`, `cell(776,601)`, `cell(803,602)`, `cell(778,604)`, `cell(803,606)`, `cell(791,611)`, `cell(797,612)`, `cell(781,613)`, `cell(785,613)`, `cell(779,617)`, `cell(780,620)`, `cell(807,595)`, `cell(773,601)`, `cell(771,602)`, `cell(772,605)`, `cell(807,607)`, `cell(773,609)`.

- `plant.meadow_grass`: `cell(788,596)`, `cell(804,596)`, `cell(776,598)`, `cell(776,599)`, `cell(780,599)`, `cell(778,612)`, `cell(784,621)`, `cell(771,598)`, `cell(771,601)`, `cell(807,608)`, `cell(777,625)`.

- `plant.oxeye_daisy`: `cell(787,596)`, `cell(780,597)`, `cell(779,602)`, `cell(802,604)`, `cell(777,605)`, `cell(781,606)`, `cell(774,608)`, `cell(784,612)`, `cell(794,613)`, `cell(790,593)`, `cell(785,594)`.

- `plant.queen_anne`: `cell(802,597)`, `cell(778,601)`, `cell(775,602)`, `cell(778,607)`, `cell(797,609)`, `cell(782,610)`, `cell(786,612)`, `cell(786,618)`, `cell(781,593)`.

- `plant.raspberry`: `cell(802,607)`, `cell(783,592)`, `cell(773,593)`, `cell(808,593)`.

- `plant.red_elderberry`: `cell(790,612)`, `cell(787,614)`, `cell(807,602)`.

- `plant.serviceberry`: `cell(797,596)`, `cell(769,597)`.

- `plant.staghorn_sumac`: `cell(774,602)`, `cell(780,613)`.

- `plant.timothy`: `cell(779,596)`, `cell(802,608)`, `cell(777,609)`, `cell(777,610)`, `cell(795,614)`, `cell(792,615)`, `cell(785,617)`, `cell(781,621)`, `cell(785,592)`, `cell(788,592)`, `cell(777,593)`, `cell(804,594)`, `cell(773,596)`, `cell(774,601)`, `cell(808,603)`.

- `plant.tussock_sedge`: `cell(809,625)`, `cell(808,626)`.

- `plant.wild_rose`: `cell(780,604)`, `cell(785,612)`, `cell(779,616)`, `cell(789,617)`, `cell(807,592)`, `cell(777,594)`.

Additional possible slots needing the ground-height check, not counted plants: `cell(807,609)`, `cell(807,612)`, `cell(801,616)`, `cell(811,616)`, `cell(797,617)`, `cell(792,618)`, `cell(797,618)`, `cell(802,618)`, `cell(805,618)`, `cell(807,618)`, `cell(812,618)`, `cell(797,619)`, `cell(799,619)`, `cell(805,620)`, `cell(810,620)`, `cell(793,621)`, `cell(799,621)`, `cell(809,621)`, `cell(810,621)`, `cell(787,622)`, `cell(799,622)`, `cell(803,623)`, `cell(789,625)`, `cell(795,629)`. These search-box slots use recipe/material byte 64 (barren recipe, material 0); species selection is not asserted.

### St Peters school (-11.4, 35.423)

**ACCIDENT — roots inside ground footprint:** `plant.buttercup` × 1, `plant.goldenrod` × 3, `plant.lupin` × 1, `plant.meadow_grass` × 4, `plant.queen_anne` × 3, `plant.raspberry` × 2, `plant.timothy` × 4, `plant.wild_rose` × 1.

Generated root cells: `cell(734,586)`, `cell(735,586)`, `cell(739,587)`, `cell(731,588)`, `cell(732,588)`, `cell(736,588)`, `cell(742,588)`, `cell(743,588)`, `cell(740,589)`, `cell(732,591)`, `cell(731,593)`, `cell(732,593)`, `cell(737,593)`, `cell(740,593)`, `cell(741,593)`, `cell(736,594)`, `cell(737,594)`, `cell(739,594)`, `cell(741,595)`.

**UNSURE — outside-footprint picture/envelope candidates:** `plant.buttercup` × 6, `plant.fireweed` × 9, `plant.goldenrod` × 7, `plant.lupin` × 16, `plant.meadow_grass` × 14, `plant.oxeye_daisy` × 10, `plant.queen_anne` × 13, `plant.raspberry` × 9, `plant.serviceberry` × 1, `plant.staghorn_sumac` × 3, `plant.timothy` × 18, `plant.wild_raisin` × 3, `plant.wild_rose` × 7.

Generated candidate cells, by kind (all UNSURE):

- `plant.buttercup`: `cell(749,596)`, `cell(733,600)`, `cell(732,605)`, `cell(733,605)`, `cell(722,599)`, `cell(753,606)`.

- `plant.fireweed`: `cell(730,583)`, `cell(727,584)`, `cell(749,595)`, `cell(730,580)`, `cell(745,582)`, `cell(752,604)`, `cell(752,605)`, `cell(724,607)`, `cell(724,608)`.

- `plant.goldenrod`: `cell(729,603)`, `cell(737,605)`, `cell(721,581)`, `cell(733,582)`, `cell(752,603)`, `cell(736,607)`, `cell(722,608)`.

- `plant.lupin`: `cell(745,587)`, `cell(724,591)`, `cell(736,595)`, `cell(737,597)`, `cell(737,600)`, `cell(740,600)`, `cell(736,601)`, `cell(738,603)`, `cell(732,604)`, `cell(730,579)`, `cell(742,579)`, `cell(742,582)`, `cell(721,583)`, `cell(751,583)`, `cell(752,588)`, `cell(722,598)`.

- `plant.meadow_grass`: `cell(745,589)`, `cell(726,599)`, `cell(728,602)`, `cell(730,603)`, `cell(740,606)`, `cell(731,580)`, `cell(742,580)`, `cell(745,581)`, `cell(722,585)`, `cell(750,592)`, `cell(722,603)`, `cell(742,608)`, `cell(747,608)`, `cell(741,609)`.

- `plant.oxeye_daisy`: `cell(740,595)`, `cell(738,597)`, `cell(738,598)`, `cell(730,599)`, `cell(729,604)`, `cell(733,581)`, `cell(724,599)`, `cell(723,602)`, `cell(751,602)`, `cell(746,608)`.

- `plant.queen_anne`: `cell(745,583)`, `cell(728,584)`, `cell(730,584)`, `cell(742,584)`, `cell(745,584)`, `cell(731,585)`, `cell(736,596)`, `cell(725,597)`, `cell(735,598)`, `cell(734,603)`, `cell(739,603)`, `cell(744,607)`, `cell(749,580)`.

- `plant.raspberry`: `cell(741,583)`, `cell(729,584)`, `cell(739,584)`, `cell(742,596)`, `cell(751,584)`, `cell(752,584)`, `cell(721,586)`, `cell(722,590)`, `cell(723,607)`.

- `plant.serviceberry`: `cell(728,586)`.

- `plant.staghorn_sumac`: `cell(740,603)`, `cell(743,606)`, `cell(749,585)`.

- `plant.timothy`: `cell(731,583)`, `cell(729,585)`, `cell(727,586)`, `cell(745,586)`, `cell(744,598)`, `cell(744,599)`, `cell(739,601)`, `cell(742,601)`, `cell(740,602)`, `cell(725,606)`, `cell(745,606)`, `cell(727,580)`, `cell(722,582)`, `cell(753,584)`, `cell(750,588)`, `cell(753,591)`, `cell(743,608)`, `cell(739,609)`.

- `plant.wild_raisin`: `cell(726,602)`, `cell(732,606)`, `cell(721,606)`.

- `plant.wild_rose`: `cell(744,600)`, `cell(731,606)`, `cell(722,581)`, `cell(743,581)`, `cell(751,601)`, `cell(729,608)`, `cell(729,609)`.

### St Peters sageCottage (-21.5, 41.643)

**ACCIDENT — roots inside ground footprint:** `plant.buttercup` × 2, `plant.fireweed` × 1, `plant.lupin` × 1, `plant.meadow_grass` × 1, `plant.oxeye_daisy` × 1, `plant.wild_raisin` × 1, `plant.wild_rose` × 1.

Generated root cells: `cell(722,598)`, `cell(722,599)`, `cell(718,600)`, `cell(723,602)`, `cell(722,603)`, `cell(717,606)`, `cell(721,606)`, `cell(716,607)`.

**UNSURE — outside-footprint picture/envelope candidates:** `plant.buttercup` × 5, `plant.common_juniper` × 1, `plant.fireweed` × 9, `plant.goldenrod` × 6, `plant.lupin` × 10, `plant.meadow_grass` × 13, `plant.oxeye_daisy` × 7, `plant.queen_anne` × 3, `plant.raspberry` × 2, `plant.red_elderberry` × 2, `plant.staghorn_sumac` × 1, `plant.timothy` × 10, `plant.wild_raisin` × 2, `plant.wild_rose` × 9.

Generated candidate cells, by kind (all UNSURE):

- `plant.buttercup`: `cell(705,600)`, `cell(707,600)`, `cell(719,612)`, `cell(732,605)`, `cell(733,611)`.

- `plant.common_juniper`: `cell(706,606)`.

- `plant.fireweed`: `cell(724,607)`, `cell(724,608)`, `cell(720,613)`, `cell(715,593)`, `cell(702,601)`, `cell(703,602)`, `cell(703,603)`, `cell(730,614)`, `cell(733,614)`.

- `plant.goldenrod`: `cell(709,597)`, `cell(729,603)`, `cell(722,608)`, `cell(713,615)`, `cell(729,618)`, `cell(731,593)`.

- `plant.lupin`: `cell(707,601)`, `cell(704,603)`, `cell(717,616)`, `cell(727,618)`, `cell(724,591)`, `cell(700,601)`, `cell(732,604)`, `cell(731,612)`, `cell(726,621)`, `cell(727,622)`.

- `plant.meadow_grass`: `cell(726,599)`, `cell(728,602)`, `cell(707,605)`, `cell(723,612)`, `cell(727,619)`, `cell(715,620)`, `cell(729,620)`, `cell(732,591)`, `cell(712,593)`, `cell(701,602)`, `cell(730,603)`, `cell(703,609)`, `cell(732,622)`.

- `plant.oxeye_daisy`: `cell(724,599)`, `cell(729,604)`, `cell(712,609)`, `cell(717,614)`, `cell(723,617)`, `cell(730,599)`, `cell(733,609)`.

- `plant.queen_anne`: `cell(725,597)`, `cell(718,616)`, `cell(732,593)`.

- `plant.raspberry`: `cell(723,607)`, `cell(708,592)`.

- `plant.red_elderberry`: `cell(704,606)`, `cell(727,610)`.

- `plant.staghorn_sumac`: `cell(699,597)`.

- `plant.timothy`: `cell(725,606)`, `cell(729,610)`, `cell(727,611)`, `cell(720,614)`, `cell(722,615)`, `cell(713,619)`, `cell(712,594)`, `cell(702,611)`, `cell(731,618)`, `cell(728,622)`.

- `plant.wild_raisin`: `cell(726,602)`, `cell(732,606)`.

- `plant.wild_rose`: `cell(729,608)`, `cell(721,609)`, `cell(729,609)`, `cell(712,612)`, `cell(712,620)`, `cell(731,606)`, `cell(730,612)`, `cell(732,615)`, `cell(729,621)`.

Additional possible slots needing the ground-height check, not counted plants: `cell(699,616)`, `cell(702,616)`, `cell(702,620)`. These search-box slots use recipe/material byte 64 (barren recipe, material 0); species selection is not asserted.

### St Peters whiteFarmhouse (21, 26)

**ACCIDENT — roots inside ground footprint:** `plant.buttercup` × 3, `plant.fireweed` × 2, `plant.goldenrod` × 1, `plant.lupin` × 1, `plant.oxeye_daisy` × 2, `plant.queen_anne` × 2, `plant.raspberry` × 2, `plant.timothy` × 4, `plant.wild_rose` × 2.

Generated root cells: `cell(799,566)`, `cell(798,567)`, `cell(795,568)`, `cell(798,568)`, `cell(797,569)`, `cell(793,570)`, `cell(797,570)`, `cell(798,570)`, `cell(792,571)`, `cell(793,571)`, `cell(799,571)`, `cell(807,571)`, `cell(799,572)`, `cell(801,572)`, `cell(812,572)`, `cell(795,573)`, `cell(799,573)`, `cell(806,573)`, `cell(807,573)`.

**UNSURE — outside-footprint picture/envelope candidates:** `plant.beaked_hazelnut` × 1, `plant.buttercup` × 14, `plant.common_juniper` × 1, `plant.fireweed` × 6, `plant.goldenrod` × 8, `plant.lupin` × 25, `plant.meadow_grass` × 20, `plant.oxeye_daisy` × 19, `plant.queen_anne` × 8, `plant.raspberry` × 12, `plant.serviceberry` × 1, `plant.staghorn_sumac` × 1, `plant.timothy` × 16, `plant.wild_raisin` × 1, `plant.wild_rose` × 8.

Generated candidate cells, by kind (all UNSURE):

- `plant.beaked_hazelnut`: `cell(800,586)`.

- `plant.buttercup`: `cell(790,562)`, `cell(792,567)`, `cell(791,582)`, `cell(793,584)`, `cell(802,584)`, `cell(787,589)`, `cell(800,589)`, `cell(802,589)`, `cell(806,589)`, `cell(812,590)`, `cell(811,591)`, `cell(783,570)`, `cell(819,581)`, `cell(786,588)`.

- `plant.common_juniper`: `cell(814,581)`.

- `plant.fireweed`: `cell(787,585)`, `cell(789,588)`, `cell(802,588)`, `cell(804,588)`, `cell(815,589)`, `cell(810,591)`.

- `plant.goldenrod`: `cell(800,580)`, `cell(812,581)`, `cell(809,583)`, `cell(810,586)`, `cell(814,586)`, `cell(795,589)`, `cell(818,567)`, `cell(795,594)`.

- `plant.lupin`: `cell(815,565)`, `cell(792,566)`, `cell(788,570)`, `cell(788,573)`, `cell(787,579)`, `cell(813,582)`, `cell(802,583)`, `cell(808,583)`, `cell(815,583)`, `cell(809,584)`, `cell(810,584)`, `cell(813,585)`, `cell(801,587)`, `cell(808,587)`, `cell(805,590)`, `cell(802,591)`, `cell(798,559)`, `cell(810,559)`, `cell(806,560)`, `cell(804,561)`, `cell(783,584)`, `cell(819,585)`, `cell(784,589)`, `cell(820,590)`, `cell(784,591)`.

- `plant.meadow_grass`: `cell(810,563)`, `cell(795,566)`, `cell(804,566)`, `cell(817,569)`, `cell(815,570)`, `cell(808,579)`, `cell(801,583)`, `cell(808,585)`, `cell(786,586)`, `cell(805,587)`, `cell(793,589)`, `cell(805,559)`, `cell(807,559)`, `cell(791,560)`, `cell(793,561)`, `cell(783,563)`, `cell(784,570)`, `cell(783,587)`, `cell(783,589)`, `cell(793,592)`.

- `plant.oxeye_daisy`: `cell(801,562)`, `cell(790,563)`, `cell(791,565)`, `cell(813,566)`, `cell(816,571)`, `cell(812,584)`, `cell(788,585)`, `cell(793,586)`, `cell(793,587)`, `cell(802,587)`, `cell(799,588)`, `cell(811,588)`, `cell(809,591)`, `cell(811,592)`, `cell(809,560)`, `cell(784,561)`, `cell(806,561)`, `cell(808,561)`, `cell(790,593)`.

- `plant.queen_anne`: `cell(787,580)`, `cell(793,581)`, `cell(786,585)`, `cell(792,586)`, `cell(807,586)`, `cell(811,587)`, `cell(800,590)`, `cell(820,594)`.

- `plant.raspberry`: `cell(795,564)`, `cell(793,566)`, `cell(791,585)`, `cell(787,587)`, `cell(811,590)`, `cell(792,591)`, `cell(788,561)`, `cell(821,568)`, `cell(785,579)`, `cell(783,592)`, `cell(808,593)`, `cell(819,593)`.

- `plant.serviceberry`: `cell(807,556)`.

- `plant.staghorn_sumac`: `cell(788,581)`.

- `plant.timothy`: `cell(803,564)`, `cell(789,565)`, `cell(808,581)`, `cell(790,584)`, `cell(803,584)`, `cell(794,585)`, `cell(802,585)`, `cell(802,586)`, `cell(793,588)`, `cell(803,591)`, `cell(798,560)`, `cell(783,567)`, `cell(785,592)`, `cell(788,592)`, `cell(811,593)`, `cell(804,594)`.

- `plant.wild_raisin`: `cell(817,572)`.

- `plant.wild_rose`: `cell(788,566)`, `cell(811,579)`, `cell(798,582)`, `cell(798,584)`, `cell(805,584)`, `cell(809,585)`, `cell(783,590)`, `cell(807,592)`.

### St Peters structure.stp_net_loft (183.2, 22.8)

**ACCIDENT — roots inside ground footprint:** none.

**UNSURE — outside-footprint picture/envelope candidates:** `plant.buttercup` × 1, `plant.common_juniper` × 1, `plant.fireweed` × 2, `plant.goldenrod` × 5, `plant.lupin` × 1, `plant.meadow_grass` × 6, `plant.queen_anne` × 1, `plant.raspberry` × 2, `plant.speckled_alder` × 1, `plant.tussock_sedge` × 2, `plant.wild_rose` × 6, `shoreplant.bayberry` × 1, `shoreplant.beach_pea` × 10, `shoreplant.marram_grass` × 20.

| Kind | Exact named-site set | Literal first row / source |
|---|---|---|
| `plant.buttercup` × 1 | `plant.stp_east_end_` suffixes **487** | `- Id: plant.stp_east_end_487` ([StPetersPlantField.asset:15286](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15286)) |
| `plant.common_juniper` × 1 | `plant.stp_east_end_` suffixes **542** | `- Id: plant.stp_east_end_542` ([StPetersPlantField.asset:15561](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15561)) |
| `plant.fireweed` × 2 | `plant.stp_east_end_` suffixes **486, 529** | `- Id: plant.stp_east_end_486` ([StPetersPlantField.asset:15281](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15281)) |
| `plant.goldenrod` × 5 | `plant.stp_east_end_` suffixes **478, 493, 497, 513, 516** | `- Id: plant.stp_east_end_478` ([StPetersPlantField.asset:15241](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15241)) |
| `plant.lupin` × 1 | `plant.stp_east_end_` suffixes **508** | `- Id: plant.stp_east_end_508` ([StPetersPlantField.asset:15391](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15391)) |
| `plant.meadow_grass` × 6 | `plant.stp_east_end_` suffixes **476, 496, 514, 570, 582, 616** | `- Id: plant.stp_east_end_496` ([StPetersPlantField.asset:15331](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15331)) |
| `plant.queen_anne` × 1 | `plant.stp_east_end_` suffixes **477** | `- Id: plant.stp_east_end_477` ([StPetersPlantField.asset:15236](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15236)) |
| `plant.raspberry` × 2 | `plant.stp_east_end_` suffixes **485, 494** | `- Id: plant.stp_east_end_485` ([StPetersPlantField.asset:15276](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15276)) |
| `plant.speckled_alder` × 1 | `plant.stp_east_end_` suffixes **467** | `- Id: plant.stp_east_end_467` ([StPetersPlantField.asset:15186](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15186)) |
| `plant.tussock_sedge` × 2 | `plant.stp_east_end_` suffixes **495, 501** | `- Id: plant.stp_east_end_495` ([StPetersPlantField.asset:15326](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15326)) |
| `plant.wild_rose` × 6 | `plant.stp_east_end_` suffixes **515, 530, 543, 571, 608, 611** | `- Id: plant.stp_east_end_571` ([StPetersPlantField.asset:15706](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15706)) |
| `shoreplant.bayberry` × 1 | `plant.stp_east_end_` suffixes **558** | `- Id: plant.stp_east_end_558` ([StPetersPlantField.asset:15641](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15641)) |
| `shoreplant.beach_pea` × 10 | `plant.stp_east_end_` suffixes **503, 548, 592, 609, 610, 615, 631, 632, 639, 641** | `- Id: plant.stp_east_end_592` ([StPetersPlantField.asset:15811](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15811)) |
| `shoreplant.marram_grass` × 20 | `plant.stp_east_end_` suffixes **502, 535, 572, 573, 583, 591, 593, 597, 598, 599, 600, 617, 618, 626, 627, 628, 629, 630, 642, 643** | `- Id: plant.stp_east_end_502` ([StPetersPlantField.asset:15361](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L15361)) |

### St Peters structure.stp_net_shed (187.7, 9.3)

**ACCIDENT — roots inside ground footprint:** none.

**UNSURE — outside-footprint picture/envelope candidates:** `plant.buttercup` × 4, `plant.fireweed` × 2, `plant.goldenrod` × 3, `plant.lupin` × 2, `plant.meadow_grass` × 14, `plant.oxeye_daisy` × 5, `plant.queen_anne` × 1, `plant.soft_rush` × 1, `plant.timothy` × 12, `plant.tussock_sedge` × 1, `plant.wild_rose` × 2, `shoreplant.bayberry` × 1, `shoreplant.beach_pea` × 3, `shoreplant.marram_grass` × 1, `shoreplant.sweet_fern` × 2.

| Kind | Exact named-site set | Literal first row / source |
|---|---|---|
| `plant.buttercup` × 4 | `plant.stp_east_end_` suffixes **304, 331, 355, 401** | `- Id: plant.stp_east_end_304` ([StPetersPlantField.asset:14371](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14371)) |
| `plant.fireweed` × 2 | `plant.stp_east_end_` suffixes **399, 433** | `- Id: plant.stp_east_end_399` ([StPetersPlantField.asset:14846](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14846)) |
| `plant.goldenrod` × 3 | `plant.stp_east_end_` suffixes **333, 390, 441** | `- Id: plant.stp_east_end_390` ([StPetersPlantField.asset:14801](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14801)) |
| `plant.lupin` × 2 | `plant.stp_east_end_` suffixes **299, 305** | `- Id: plant.stp_east_end_299` ([StPetersPlantField.asset:14346](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14346)) |
| `plant.meadow_grass` × 14 | `plant.stp_east_end_` suffixes **282, 311, 312, 342, 357, 369, 371, 402, 414, 415, 424, 434, 443, 454** | `- Id: plant.stp_east_end_311` ([StPetersPlantField.asset:14406](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14406)) |
| `plant.oxeye_daisy` × 5 | `plant.stp_east_end_` suffixes **332, 391, 403, 425, 442** | `- Id: plant.stp_east_end_332` ([StPetersPlantField.asset:14511](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14511)) |
| `plant.queen_anne` × 1 | `plant.stp_east_end_` suffixes **367** | `- Id: plant.stp_east_end_367` ([StPetersPlantField.asset:14686](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14686)) |
| `plant.soft_rush` × 1 | `plant.stp_east_end_` suffixes **380** | `- Id: plant.stp_east_end_380` ([StPetersPlantField.asset:14751](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14751)) |
| `plant.timothy` × 12 | `plant.stp_east_end_` suffixes **284, 296, 297, 313, 356, 366, 368, 389, 400, 412, 413, 453** | `- Id: plant.stp_east_end_284` ([StPetersPlantField.asset:14296](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14296)) |
| `plant.tussock_sedge` × 1 | `plant.stp_east_end_` suffixes **423** | `- Id: plant.stp_east_end_423` ([StPetersPlantField.asset:14966](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14966)) |
| `plant.wild_rose` × 2 | `plant.stp_east_end_` suffixes **358, 404** | `- Id: plant.stp_east_end_358` ([StPetersPlantField.asset:14641](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14641)) |
| `shoreplant.bayberry` × 1 | `plant.stp_east_end_` suffixes **381** | `- Id: plant.stp_east_end_381` ([StPetersPlantField.asset:14756](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14756)) |
| `shoreplant.beach_pea` × 3 | `plant.stp_east_end_` suffixes **323, 370, 444** | `- Id: plant.stp_east_end_323` ([StPetersPlantField.asset:14466](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14466)) |
| `shoreplant.marram_grass` × 1 | `plant.stp_east_end_` suffixes **426** | `- Id: plant.stp_east_end_426` ([StPetersPlantField.asset:14981](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14981)) |
| `shoreplant.sweet_fern` × 2 | `plant.stp_east_end_` suffixes **298, 445** | `- Id: plant.stp_east_end_298` ([StPetersPlantField.asset:14341](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L14341)) |

### St Peters structure.stp_bait_store (182.3, -8.8)

**ACCIDENT — roots inside ground footprint:** none.

**UNSURE — outside-footprint picture/envelope candidates:** `plant.black_huckleberry` × 1, `plant.buttercup` × 2, `plant.fireweed` × 5, `plant.goldenrod` × 3, `plant.lowbush_blueberry` × 2, `plant.lupin` × 11, `plant.meadow_grass` × 14, `plant.oxeye_daisy` × 7, `plant.queen_anne` × 3, `plant.sheep_laurel` × 1, `plant.soft_rush` × 2, `plant.timothy` × 8, `plant.wild_rose` × 3, `shoreplant.bayberry` × 1.

| Kind | Exact named-site set | Literal first row / source |
|---|---|---|
| `plant.black_huckleberry` × 1 | `plant.stp_east_end_` suffixes **23** | `- Id: plant.stp_east_end_23` ([StPetersPlantField.asset:13086](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L13086)) |
| `plant.fireweed` × 4 | `plant.stp_east_end_` suffixes **33, 34, 97, 162** | `- Id: plant.stp_east_end_34` ([StPetersPlantField.asset:13141](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L13141)) |
| `plant.goldenrod` × 2 | `plant.stp_east_end_` suffixes **177, 180** | `- Id: plant.stp_east_end_177` ([StPetersPlantField.asset:13856](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L13856)) |
| `plant.lowbush_blueberry` × 2 | `plant.stp_east_end_` suffixes **42, 43** | `- Id: plant.stp_east_end_42` ([StPetersPlantField.asset:13181](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L13181)) |
| `plant.lupin` × 6 | `plant.stp_east_end_` suffixes **98, 108, 110, 150, 159, 160** | `- Id: plant.stp_east_end_110` ([StPetersPlantField.asset:13521](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L13521)) |
| `plant.meadow_grass` × 13 | `plant.stp_east_end_` suffixes **24, 45, 63, 122, 132, 143, 145, 151, 152, 158, 161, 176, 179** | `- Id: plant.stp_east_end_122` ([StPetersPlantField.asset:13581](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L13581)) |
| `plant.oxeye_daisy` × 4 | `plant.stp_east_end_` suffixes **44, 131, 133, 164** | `- Id: plant.stp_east_end_44` ([StPetersPlantField.asset:13191](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L13191)) |
| `plant.queen_anne` × 2 | `plant.stp_east_end_` suffixes **121, 165** | `- Id: plant.stp_east_end_121` ([StPetersPlantField.asset:13576](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L13576)) |
| `plant.sheep_laurel` × 1 | `plant.stp_east_end_` suffixes **62** | `- Id: plant.stp_east_end_62` ([StPetersPlantField.asset:13281](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L13281)) |
| `plant.soft_rush` × 2 | `plant.stp_east_end_` suffixes **111, 163** | `- Id: plant.stp_east_end_111` ([StPetersPlantField.asset:13526](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L13526)) |
| `plant.timothy` × 7 | `plant.stp_east_end_` suffixes **51, 52, 109, 123, 144, 153, 178** | `- Id: plant.stp_east_end_109` ([StPetersPlantField.asset:13516](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L13516)) |
| `shoreplant.bayberry` × 1 | `plant.stp_east_end_` suffixes **22** | `- Id: plant.stp_east_end_22` ([StPetersPlantField.asset:13081](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L13081)) |

Generated candidate cells, by kind (all UNSURE):

- `plant.buttercup`: `cell(1114,494)`, `cell(1135,495)`.

- `plant.fireweed`: `cell(1135,496)`.

- `plant.goldenrod`: `cell(1126,493)`.

- `plant.lupin`: `cell(1121,496)`, `cell(1122,496)`, `cell(1125,498)`, `cell(1112,493)`, `cell(1133,495)`.

- `plant.meadow_grass`: `cell(1120,494)`.

- `plant.oxeye_daisy`: `cell(1121,494)`, `cell(1112,495)`, `cell(1117,495)`.

- `plant.queen_anne`: `cell(1122,495)`.

- `plant.timothy`: `cell(1126,494)`.

- `plant.wild_rose`: `cell(1113,492)`, `cell(1121,492)`, `cell(1116,496)`.

Additional possible slots needing the ground-height check, not counted plants: `cell(1139,491)`, `cell(1139,492)`, `cell(1140,495)`. These search-box slots use recipe/material byte 64 (barren recipe, material 0); species selection is not asserted.

### St Peters creamSaltbox (28.5, 35.704)

**ACCIDENT — roots inside ground footprint:** `plant.buttercup` × 3, `plant.fireweed` × 2, `plant.goldenrod` × 3, `plant.lupin` × 3, `plant.oxeye_daisy` × 4, `plant.queen_anne` × 3, `plant.raspberry` × 2, `plant.timothy` × 2.

Generated root cells: `cell(810,586)`, `cell(814,586)`, `cell(822,586)`, `cell(811,587)`, `cell(821,587)`, `cell(811,588)`, `cell(822,588)`, `cell(815,589)`, `cell(820,589)`, `cell(811,590)`, `cell(812,590)`, `cell(820,590)`, `cell(810,591)`, `cell(811,591)`, `cell(811,592)`, `cell(811,593)`, `cell(819,593)`, `cell(810,594)`, `cell(820,594)`, `cell(821,594)`, `cell(810,596)`, `cell(812,596)`.

**UNSURE — outside-footprint picture/envelope candidates:** `plant.beaked_hazelnut` × 2, `plant.buttercup` × 11, `plant.common_juniper` × 2, `plant.fireweed` × 3, `plant.goldenrod` × 9, `plant.leatherleaf` × 1, `plant.lupin` × 23, `plant.meadow_grass` × 14, `plant.oxeye_daisy` × 11, `plant.queen_anne` × 8, `plant.raspberry` × 5, `plant.red_elderberry` × 2, `plant.rhodora` × 1, `plant.staghorn_sumac` × 1, `plant.timothy` × 15, `plant.tussock_sedge` × 4, `plant.wild_rose` × 6.

Generated candidate cells, by kind (all UNSURE):

- `plant.beaked_hazelnut`: `cell(824,588)`, `cell(800,586)`.

- `plant.buttercup`: `cell(806,589)`, `cell(827,595)`, `cell(805,596)`, `cell(827,596)`, `cell(811,599)`, `cell(807,603)`, `cell(819,581)`, `cell(802,584)`, `cell(800,589)`, `cell(802,589)`, `cell(800,609)`.

- `plant.common_juniper`: `cell(811,601)`, `cell(814,581)`.

- `plant.fireweed`: `cell(804,588)`, `cell(819,601)`, `cell(802,588)`.

- `plant.goldenrod`: `cell(809,583)`, `cell(825,600)`, `cell(823,601)`, `cell(825,601)`, `cell(800,580)`, `cell(812,581)`, `cell(832,584)`, `cell(831,601)`, `cell(830,602)`.

- `plant.leatherleaf`: `cell(832,610)`.

- `plant.lupin`: `cell(808,583)`, `cell(815,583)`, `cell(827,583)`, `cell(809,584)`, `cell(810,584)`, `cell(813,585)`, `cell(819,585)`, `cell(808,587)`, `cell(805,590)`, `cell(807,595)`, `cell(809,600)`, `cell(810,603)`, `cell(820,604)`, `cell(827,604)`, `cell(807,607)`, `cell(813,582)`, `cell(802,583)`, `cell(801,587)`, `cell(833,590)`, `cell(802,591)`, `cell(803,602)`, `cell(831,603)`, `cell(803,606)`.

- `plant.meadow_grass`: `cell(822,584)`, `cell(808,585)`, `cell(805,587)`, `cell(825,591)`, `cell(830,595)`, `cell(804,596)`, `cell(830,600)`, `cell(812,601)`, `cell(815,601)`, `cell(807,608)`, `cell(831,581)`, `cell(833,581)`, `cell(801,583)`, `cell(831,590)`.

- `plant.oxeye_daisy`: `cell(812,584)`, `cell(809,591)`, `cell(825,594)`, `cell(828,601)`, `cell(812,604)`, `cell(822,581)`, `cell(830,581)`, `cell(829,582)`, `cell(802,587)`, `cell(830,603)`, `cell(802,604)`.

- `plant.queen_anne`: `cell(807,586)`, `cell(827,598)`, `cell(809,608)`, `cell(810,608)`, `cell(830,579)`, `cell(800,590)`, `cell(830,596)`, `cell(802,597)`.

- `plant.raspberry`: `cell(827,591)`, `cell(808,593)`, `cell(819,603)`, `cell(822,582)`, `cell(802,607)`.

- `plant.red_elderberry`: `cell(807,602)`, `cell(833,598)`.

- `plant.rhodora`: `cell(833,609)`.

- `plant.staghorn_sumac`: `cell(832,590)`.

- `plant.timothy`: `cell(803,584)`, `cell(803,591)`, `cell(804,594)`, `cell(827,600)`, `cell(830,601)`, `cell(808,603)`, `cell(811,603)`, `cell(825,603)`, `cell(826,579)`, `cell(808,581)`, `cell(802,585)`, `cell(802,586)`, `cell(831,596)`, `cell(831,598)`, `cell(802,608)`.

- `plant.tussock_sedge`: `cell(834,605)`, `cell(832,609)`, `cell(834,609)`, `cell(833,610)`.

- `plant.wild_rose`: `cell(829,583)`, `cell(805,584)`, `cell(809,585)`, `cell(807,592)`, `cell(811,579)`, `cell(830,580)`.

Additional possible slots needing the ground-height check, not counted plants: `cell(837,603)`, `cell(834,604)`, `cell(828,605)`, `cell(822,607)`, `cell(828,607)`, `cell(821,608)`, `cell(807,609)`, `cell(815,610)`, `cell(823,610)`, `cell(807,612)`, `cell(818,612)`. These search-box slots use recipe/material byte 64 (barren recipe, material 0); species selection is not asserted.

### St Peters harbourGable (-6.25, 46.125)

**ACCIDENT — roots inside ground footprint:** `plant.common_juniper` × 1, `plant.goldenrod` × 1, `plant.lupin` × 4, `plant.meadow_grass` × 9, `plant.oxeye_daisy` × 3, `plant.queen_anne` × 1, `plant.raspberry` × 1, `plant.red_elderberry` × 1, `plant.serviceberry` × 1, `plant.timothy` × 6, `plant.wild_rose` × 1.

Generated root cells: `cell(744,607)`, `cell(742,608)`, `cell(743,608)`, `cell(746,608)`, `cell(747,608)`, `cell(741,609)`, `cell(745,610)`, `cell(746,610)`, `cell(748,610)`, `cell(752,610)`, `cell(741,611)`, `cell(743,611)`, `cell(744,611)`, `cell(747,611)`, `cell(743,612)`, `cell(746,612)`, `cell(750,612)`, `cell(754,612)`, `cell(743,614)`, `cell(744,614)`, `cell(745,614)`, `cell(749,614)`, `cell(742,615)`, `cell(746,615)`, `cell(749,615)`, `cell(742,616)`, `cell(743,616)`, `cell(745,616)`, `cell(747,616)`.

**UNSURE — outside-footprint picture/envelope candidates:** `plant.buttercup` × 10, `plant.common_juniper` × 3, `plant.fireweed` × 11, `plant.goldenrod` × 9, `plant.lupin` × 27, `plant.meadow_grass` × 22, `plant.oxeye_daisy` × 16, `plant.queen_anne` × 13, `plant.raspberry` × 2, `plant.serviceberry` × 1, `plant.staghorn_sumac` × 8, `plant.timothy` × 18, `plant.wild_raisin` × 1, `plant.wild_rose` × 12.

Generated candidate cells, by kind (all UNSURE):

- `plant.buttercup`: `cell(753,606)`, `cell(755,608)`, `cell(735,610)`, `cell(752,617)`, `cell(758,617)`, `cell(745,618)`, `cell(759,602)`, `cell(732,605)`, `cell(733,605)`, `cell(733,611)`.

- `plant.common_juniper`: `cell(756,613)`, `cell(752,622)`, `cell(753,625)`.

- `plant.fireweed`: `cell(752,604)`, `cell(752,605)`, `cell(756,617)`, `cell(751,618)`, `cell(757,621)`, `cell(737,625)`, `cell(752,625)`, `cell(730,614)`, `cell(733,614)`, `cell(733,621)`, `cell(739,631)`.

- `plant.goldenrod`: `cell(737,605)`, `cell(736,607)`, `cell(740,610)`, `cell(759,614)`, `cell(734,617)`, `cell(741,625)`, `cell(752,603)`, `cell(761,611)`, `cell(731,629)`.

- `plant.lupin`: `cell(756,605)`, `cell(736,611)`, `cell(755,613)`, `cell(738,614)`, `cell(758,614)`, `cell(754,615)`, `cell(738,616)`, `cell(736,618)`, `cell(735,621)`, `cell(758,622)`, `cell(741,623)`, `cell(750,623)`, `cell(743,626)`, `cell(737,630)`, `cell(737,600)`, `cell(740,600)`, `cell(736,601)`, `cell(738,603)`, `cell(757,603)`, `cell(762,603)`, `cell(732,604)`, `cell(762,609)`, `cell(731,612)`, `cell(761,613)`, `cell(733,626)`, `cell(742,630)`, `cell(747,630)`.

- `plant.meadow_grass`: `cell(757,604)`, `cell(740,606)`, `cell(738,611)`, `cell(760,612)`, `cell(735,614)`, `cell(737,616)`, `cell(742,618)`, `cell(744,618)`, `cell(750,618)`, `cell(742,619)`, `cell(746,621)`, `cell(755,622)`, `cell(734,623)`, `cell(738,625)`, `cell(756,626)`, `cell(734,627)`, `cell(741,628)`, `cell(762,602)`, `cell(763,604)`, `cell(732,622)`, `cell(736,631)`, `cell(748,632)`.

- `plant.oxeye_daisy`: `cell(755,606)`, `cell(758,609)`, `cell(759,610)`, `cell(734,611)`, `cell(753,617)`, `cell(736,619)`, `cell(758,619)`, `cell(753,621)`, `cell(751,622)`, `cell(742,624)`, `cell(734,625)`, `cell(738,628)`, `cell(751,602)`, `cell(762,605)`, `cell(761,607)`, `cell(733,609)`.

- `plant.queen_anne`: `cell(754,609)`, `cell(759,609)`, `cell(739,610)`, `cell(758,613)`, `cell(738,622)`, `cell(755,601)`, `cell(734,603)`, `cell(739,603)`, `cell(761,606)`, `cell(733,613)`, `cell(733,623)`, `cell(741,630)`, `cell(749,631)`.

- `plant.raspberry`: `cell(755,614)`, `cell(731,623)`.

- `plant.serviceberry`: `cell(749,626)`.

- `plant.staghorn_sumac`: `cell(743,606)`, `cell(760,608)`, `cell(739,618)`, `cell(760,596)`, `cell(761,600)`, `cell(740,603)`, `cell(763,608)`, `cell(733,627)`.

- `plant.timothy`: `cell(745,606)`, `cell(759,608)`, `cell(739,609)`, `cell(760,609)`, `cell(739,620)`, `cell(759,623)`, `cell(739,624)`, `cell(750,624)`, `cell(747,625)`, `cell(742,626)`, `cell(744,629)`, `cell(744,599)`, `cell(739,601)`, `cell(742,601)`, `cell(740,602)`, `cell(763,605)`, `cell(731,618)`, `cell(759,630)`.

- `plant.wild_raisin`: `cell(732,606)`.

- `plant.wild_rose`: `cell(755,607)`, `cell(754,617)`, `cell(757,619)`, `cell(736,623)`, `cell(737,626)`, `cell(735,627)`, `cell(744,600)`, `cell(758,600)`, `cell(751,601)`, `cell(731,606)`, `cell(732,615)`, `cell(735,631)`.

Additional possible slots needing the ground-height check, not counted plants: `cell(728,632)`, `cell(729,632)`, `cell(730,632)`, `cell(727,633)`, `cell(729,633)`, `cell(730,633)`, `cell(731,633)`. These search-box slots use recipe/material byte 64 (barren recipe, material 0); species selection is not asserted.

### St Peters whiteGable (53, 35.204)

**ACCIDENT — roots inside ground footprint:** `plant.buttercup` × 2, `plant.fireweed` × 1, `plant.goldenrod` × 1, `plant.lupin` × 3, `plant.meadow_grass` × 2, `plant.oxeye_daisy` × 2, `plant.queen_anne` × 2, `plant.raspberry` × 1, `plant.timothy` × 3, `plant.wild_rose` × 1.

Generated root cells: `cell(867,585)`, `cell(871,586)`, `cell(861,587)`, `cell(864,587)`, `cell(868,587)`, `cell(870,587)`, `cell(870,588)`, `cell(872,588)`, `cell(859,589)`, `cell(863,589)`, `cell(866,589)`, `cell(871,590)`, `cell(865,591)`, `cell(868,591)`, `cell(869,591)`, `cell(862,593)`, `cell(871,593)`, `cell(869,595)`.

**UNSURE — outside-footprint picture/envelope candidates:** `plant.beaked_hazelnut` × 4, `plant.buttercup` × 2, `plant.common_juniper` × 1, `plant.fireweed` × 1, `plant.goldenrod` × 6, `plant.lady_slipper` × 1, `plant.leatherleaf` × 6, `plant.lowbush_blueberry` × 4, `plant.lupin` × 6, `plant.meadow_grass` × 13, `plant.oxeye_daisy` × 4, `plant.queen_anne` × 1, `plant.raspberry` × 2, `plant.rhodora` × 4, `plant.serviceberry` × 2, `plant.sheep_laurel` × 1, `plant.sweet_gale` × 3, `plant.timothy` × 7, `plant.tussock_sedge` × 13, `plant.wild_raisin` × 5, `plant.wild_rose` × 3, `shoreplant.sweet_fern` × 2.

| Kind | Exact named-site set | Literal first row / source |
|---|---|---|
| `plant.beaked_hazelnut` × 2 | `plant.stp_ginnys_plot_` suffixes **1606, 1965** | `- Id: plant.stp_ginnys_plot_1606` ([StPetersPlantField.asset:26191](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L26191)) |
| `plant.goldenrod` × 3 | `plant.stp_ginnys_plot_` suffixes **1424, 1445, 1480** | `- Id: plant.stp_ginnys_plot_1480` ([StPetersPlantField.asset:25576](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L25576)) |
| `plant.lady_slipper` × 1 | `plant.stp_ginnys_plot_` suffixes **2178** | `- Id: plant.stp_ginnys_plot_2178` ([StPetersPlantField.asset:29051](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L29051)) |
| `plant.lowbush_blueberry` × 4 | `plant.stp_ginnys_plot_` suffixes **1498, 1524, 1525, 2022** | `- Id: plant.stp_ginnys_plot_1498` ([StPetersPlantField.asset:25661](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L25661)) |
| `plant.lupin` × 2 | `plant.stp_ginnys_plot_` suffixes **1378, 1380** | `- Id: plant.stp_ginnys_plot_1378` ([StPetersPlantField.asset:25116](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L25116)) |
| `plant.meadow_grass` × 11 | `plant.stp_ginnys_plot_` suffixes **1446, 1707, 1842, 1946, 2864, 2873, 2874, 2875, 2887, 2888, 2889** | `- Id: plant.stp_ginnys_plot_1707` ([StPetersPlantField.asset:26696](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L26696)) |
| `plant.oxeye_daisy` × 1 | `plant.stp_ginnys_plot_` suffixes **2863** | `- Id: plant.stp_ginnys_plot_2863` ([StPetersPlantField.asset:32476](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L32476)) |
| `plant.raspberry` × 1 | `plant.stp_ginnys_plot_` suffixes **1479** | `- Id: plant.stp_ginnys_plot_1479` ([StPetersPlantField.asset:25571](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L25571)) |
| `plant.serviceberry` × 1 | `plant.stp_ginnys_plot_` suffixes **1790** | `- Id: plant.stp_ginnys_plot_1790` ([StPetersPlantField.asset:27111](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L27111)) |
| `plant.sheep_laurel` × 1 | `plant.stp_ginnys_plot_` suffixes **2211** | `- Id: plant.stp_ginnys_plot_2211` ([StPetersPlantField.asset:29216](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L29216)) |
| `plant.timothy` × 2 | `plant.stp_ginnys_plot_` suffixes **1379, 2900** | `- Id: plant.stp_ginnys_plot_1379` ([StPetersPlantField.asset:25121](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L25121)) |
| `plant.tussock_sedge` × 4 | `plant.stp_ginnys_plot_` suffixes **1643, 1947, 2177, 2240** | `- Id: plant.stp_ginnys_plot_1643` ([StPetersPlantField.asset:26376](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L26376)) |
| `plant.wild_raisin` × 4 | `plant.stp_ginnys_plot_` suffixes **1726, 1772, 2134, 2135** | `- Id: plant.stp_ginnys_plot_1726` ([StPetersPlantField.asset:26791](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L26791)) |
| `shoreplant.sweet_fern` × 2 | `plant.stp_ginnys_plot_` suffixes **1464, 2037** | `- Id: plant.stp_ginnys_plot_2037` ([StPetersPlantField.asset:28346](../../Assets/_Project/Data/Plants5/StPetersPlantField.asset#L28346)) |

Generated candidate cells, by kind (all UNSURE):

- `plant.beaked_hazelnut`: `cell(847,585)`, `cell(848,598)`.

- `plant.buttercup`: `cell(875,589)`, `cell(852,585)`.

- `plant.common_juniper`: `cell(849,582)`.

- `plant.fireweed`: `cell(850,591)`.

- `plant.goldenrod`: `cell(867,600)`, `cell(850,581)`, `cell(871,581)`.

- `plant.leatherleaf`: `cell(856,605)`, `cell(852,608)`, `cell(855,608)`, `cell(852,604)`, `cell(850,607)`, `cell(851,608)`.

- `plant.lupin`: `cell(852,586)`, `cell(856,597)`, `cell(854,601)`, `cell(849,587)`.

- `plant.meadow_grass`: `cell(857,601)`, `cell(849,596)`.

- `plant.oxeye_daisy`: `cell(860,596)`, `cell(850,582)`, `cell(849,598)`.

- `plant.queen_anne`: `cell(856,600)`.

- `plant.raspberry`: `cell(850,587)`.

- `plant.rhodora`: `cell(854,607)`, `cell(851,604)`, `cell(849,608)`, `cell(857,609)`.

- `plant.serviceberry`: `cell(875,600)`.

- `plant.sweet_gale`: `cell(853,605)`, `cell(850,604)`, `cell(859,610)`.

- `plant.timothy`: `cell(860,583)`, `cell(855,589)`, `cell(849,581)`, `cell(850,586)`, `cell(848,600)`.

- `plant.tussock_sedge`: `cell(853,604)`, `cell(854,605)`, `cell(852,607)`, `cell(853,608)`, `cell(848,608)`, `cell(853,609)`, `cell(855,609)`, `cell(856,609)`, `cell(865,609)`.

- `plant.wild_raisin`: `cell(848,590)`.

- `plant.wild_rose`: `cell(863,600)`, `cell(849,580)`, `cell(848,597)`.

Additional possible slots needing the ground-height check, not counted plants: `cell(857,603)`, `cell(864,603)`, `cell(855,604)`, `cell(856,604)`, `cell(859,604)`, `cell(860,604)`, `cell(874,604)`, `cell(860,605)`, `cell(869,607)`, `cell(850,609)`, `cell(851,609)`, `cell(851,610)`, `cell(852,610)`, `cell(855,610)`, `cell(868,612)`. These search-box slots use recipe/material byte 64 (barren recipe, material 0); species selection is not asserted.

## Review and limits

Existing checks located before extraction: [StPetersVillagePlanTests.cs:174](../../Assets/Tests/EditMode/StPetersVillagePlanTests.cs#L174) (`G11_FootprintTableHasOneSourcedRowPerBuilding`), [StPetersVillageTests.cs:745](../../Assets/Tests/EditMode/StPetersVillageTests.cs#L745) (tree/footprint clearance), and the existing plant scatter, clump, codec and grass scatter suites under `Assets/Tests/EditMode/Art/`. No tests or validator were added or changed. Private text extraction checked counts, GUID references, scene transforms, selected sprite metadata, field codes and source quotes. **Unity, EditMode, PlayMode, pixel comparisons and CI were not run for this document.**

This is a static audit of the pinned files. It does not accept a look, verify whether transparent pixels cover a roof, certify runtime terrain sampling, or cover future placements of unplaced content. The safe outcome for those uncertainties is the hold list above. No scene, prefab, material, shader, sprite, rig, data asset, GameConfig value or save-format change accompanies this document.

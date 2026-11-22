# Interior furniture and decor inventory

Audited base: `1abca5974ce3d16a3257777797bb2547c9ad8d53`.

The committed home furniture consists of **five PNG sheets, forty sliced sprites**. Each sheet contains eight facings, left to right d0–d7, at 32 pixels per metre. The table records actual serialized sprite references separately from authoring plans. No standalone settee or home decor sheet is present at this base.

All file links below are relative to the repository. Pixel sizes were read from the PNGs and checked against [the interior catalog](../../Assets/_Project/Art/Sprites/Interiors/Interiors.json). Room assignments come from [the room furnishing plans](../../Assets/_Project/Code/App/Editor/StPetersInteriors.cs). Furniture is constructed by the room builder; none of these five sheets is directly referenced by a committed prefab.

| Furniture | File | Sheet px | Cell px / facings | Serialized room use | Authoring use |
|---|---|---:|---|---|---|
| Bed | [InteriorProp_bed.png](../../Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_bed.png) | 672 × 79 | 84 × 79 / 8 | St Peters scene: 5 sprite references | Cottage and red saltbox; two guest/host upper rooms; one bed each in cream saltbox, white gable and harbour gable upper-room plans |
| Table | [InteriorProp_table.png](../../Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_table.png) | 432 × 53 | 54 × 53 / 8 | St Peters scene: 6 sprite references | Cottage, red saltbox, school pupil/teacher desks, farmhouse family table |
| Chair | [InteriorProp_chair.png](../../Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_chair.png) | 192 × 41 | 24 × 41 / 8 | St Peters scene: 13 sprite references | Cottage pair, red saltbox pair, three school chairs, four farmhouse chairs |
| Dresser | [InteriorProp_dresser.png](../../Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_dresser.png) | 320 × 49 | 40 × 49 / 8 | St Peters scene: 6 sprite references | Cottage, red saltbox, school book press, farmhouse; upper-room wardrobe substitute |
| Sea chest | [InteriorProp_seaChest.png](../../Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_seaChest.png) | 288 × 35 | 36 × 35 / 8 | St Peters scene: 7 sprite references | Cottage, red saltbox, school wood box, farmhouse; upper and lower stair substitutes |

The scan covered all tracked Assets PNG names, the complete interior prop catalog, all serialized asset/prefab/scene GUID references, and furnishing code. Scene counts are references, not a claim that every authoring plan is currently placed. The cottage plan is reused at more than one site. Boat and camper furniture embedded in meshes or whole-room imagery is not a standalone home sprite and is not counted as one. Outdoor wharf furniture, terrain shelves and planting beds are excluded.

| Requested category | Result at the audited base |
|---|---|
| Settee / upholstered seating | No standalone PNG found; camper settee is a mesh anchor. Use the existing bed fabric and table wood as the available colour/scale neighbours for proposed upholstered seating. |
| Rugs | No standalone home rug PNG found. |
| Shelves / bookshelf | No standalone home shelf PNG found; school book press uses dresser. |
| Lamps | No standalone home lamp PNG found; street/wharf lamp sheets are exterior equipment. |
| Pictures | No standalone home picture PNG found. |
| Curtains | No standalone home curtain PNG found. |
| Stairs | No dedicated stair-piece PNG found; current cottage authoring plan uses sea chest for both stair endpoints. |
| Wardrobe | No dedicated wardrobe PNG found; current upper-room plan uses dresser. |
| Wash stand | Existing Fixture_washstand.png belongs to the washroom fixture set; excluded from furniture replacements. A proposed jug-and-stand decor asset must remain a separate addition. |

## Readability ranking, weakest first

Judged on the complete eight-facing contact sheet at 2×, with native cells also inspected. These are review priorities, not accepted replacements.

1. **Sea chest:** the short mottled block loses its lid seam and fittings against the wood floor; a clear rim, straps and latch would identify it at native size.
2. **Dresser:** the blue box has little wood construction detail and its drawer divisions/handles disappear in the diagonal views.
3. **Bed:** the broad sage cover reads, but repeated stippling overwhelms any quilt structure and the pillow merges into a single cream strip.
4. **Chair:** the narrow back rails and small seat remain legible, but diagonal one-pixel joins are brittle.
5. **Table:** the top and four legs are the clearest silhouettes; surface grain could be quieter but the object already reads immediately.

## Review evidence and limits

The review contact sheet is `item1-furniture-audit-2x.png`, kept outside the commit. It shows every facing at 2× nearest-neighbour on RGB (129, 90, 57), sampled from the committed white-gable room's wood palette. The bed and table serve as the two existing neighbours for subsequent sheets. No standalone settee was available to sample. This is a source-art review, not a gameplay render or approval of an in-room result.

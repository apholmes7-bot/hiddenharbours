# Interior furniture and decor inventory

Inventory refreshed against `d46bde303e6aa256d23d47a05ede5d7d6f38e51e`. The visual notes below remain historical observations from `1abca5974ce3d16a3257777797bb2547c9ad8d53`; no new appearance assessment was made.

The committed home furniture consists of **five PNG sheets, forty sliced sprites**. Each sheet contains eight facings, left to right d0–d7, at 32 pixels per metre. The table records actual serialized sprite references separately from authoring plans. No standalone settee or home decor sheet is present at this base.

All file links below are relative to the repository. Pixel sizes were read from the PNGs and checked against [the interior catalog](../../Assets/_Project/Art/Sprites/Interiors/Interiors.json). Room assignments come from [the room furnishing plans](../../Assets/_Project/Code/App/Editor/StPetersInteriors.cs). Furniture is constructed by the room builder; none of these five sheets is directly referenced by a committed prefab.

| Furniture | File | Sheet px | Cell px / facings | Serialized room use | Authoring use |
|---|---|---:|---|---|---|
| Bed | [InteriorProp_bed.png](../../Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_bed.png) | 672 × 79 | 84 × 79 / 8 | St Peters scene: 4 sprite references | Village cottage and red saltbox; the player's and host's upstairs bedrooms in Ginny's cottage. The other three upper-room plans are empty. |
| Table | [InteriorProp_table.png](../../Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_table.png) | 432 × 53 | 54 × 53 / 8 | St Peters scene: 6 sprite references | Village cottage, Ginny's kitchen, red saltbox, school pupil/teacher desks, farmhouse family table |
| Chair | [InteriorProp_chair.png](../../Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_chair.png) | 192 × 41 | 24 × 41 / 8 | St Peters scene: 13 sprite references | Village cottage pair, Ginny's kitchen pair, red saltbox pair, three school chairs, four farmhouse chairs; facings now follow their table side |
| Dresser | [InteriorProp_dresser.png](../../Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_dresser.png) | 320 × 49 | 40 × 49 / 8 | St Peters scene: 5 sprite references | Village cottage, red saltbox, school book press, farmhouse; Ginny's upper-room wardrobe substitute |
| Sea chest | [InteriorProp_seaChest.png](../../Assets/_Project/Art/Sprites/Interiors/Props/InteriorProp_seaChest.png) | 288 × 35 | 36 × 35 / 8 | St Peters scene: 6 sprite references | Village cottage, red saltbox, school wood box, farmhouse; upper and lower stair interaction substitutes, whose pictures are hidden in the placed cottage |

The scan covered tracked Assets PNG names, the interior prop catalog, serialized asset/prefab/scene GUID references, and furnishing code. Scene counts are references, not a claim that each sprite is visible. Main's cottage update (#1392, `b5ad7f5294`) gives Ginny's ground floor its own furniture and painted settee/stairs; the village cottage retains its shared plan. The facing correction (#1490, `c626cea10d`) turns chairs toward their tables. Boat, camper and painted-room furniture embedded in meshes or whole-room imagery is not a standalone home sprite and is not counted as one. Outdoor wharf furniture, terrain shelves and planting beds are excluded.

| Requested category | Result at the audited base |
|---|---|
| Settee / upholstered seating | No standalone PNG found; camper settee is a mesh anchor and Ginny's settee is painted into her room sheet. The earlier proposal used existing bed fabric and table wood as neighbours; that is not an accepted appearance. |
| Rugs | No standalone home rug PNG found. |
| Shelves / bookshelf | No standalone home shelf PNG found; school book press uses dresser. |
| Lamps | No standalone home lamp PNG found; street/wharf lamp sheets are exterior equipment. |
| Pictures | No standalone home picture PNG found. |
| Curtains | No standalone home curtain PNG found. |
| Stairs | No dedicated stair-piece PNG found; Ginny's stairs are painted into the room sheets. The plan retains sea chest substitutes for both interaction endpoints and hides their pictures in the placed room. |
| Wardrobe | No dedicated wardrobe PNG found; current upper-room plan uses dresser. |
| Wash stand | Existing Fixture_washstand.png belongs to the washroom fixture set; excluded from furniture replacements. A proposed jug-and-stand decor asset must remain a separate addition. |

## Historical readability ranking, weakest first

The original audit judged the complete eight-facing contact sheet at 2×, with native cells also inspected. These historical priorities have not been reassessed against the current rooms and are not accepted replacements.

1. **Sea chest:** the short mottled block loses its lid seam and fittings against the wood floor; a clear rim, straps and latch would identify it at native size.
2. **Dresser:** the blue box has little wood construction detail and its drawer divisions/handles disappear in the diagonal views.
3. **Bed:** the broad sage cover reads, but repeated stippling overwhelms any quilt structure and the pillow merges into a single cream strip.
4. **Chair:** the narrow back rails and small seat remain legible, but diagonal one-pixel joins are brittle.
5. **Table:** the top and four legs are the clearest silhouettes; surface grain could be quieter but the object already reads immediately.

## Review evidence and limits

The original contact sheet was recorded as `item1-furniture-audit-2x.png`, outside the commit, showing every facing at 2× nearest-neighbour on RGB (129, 90, 57). That external evidence was not revalidated during this refresh. Only dimensions, references and authoring-plan facts were refreshed; no current gameplay render or in-room appearance is approved here.

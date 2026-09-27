# Rig 9 on the appearance contract: the builder map

The table PRs 2 and 3 build from (charter §3.3). It maps every axis and option of the v9 builder
(`data/options.v9.json`, derived from rig `02df29ec…`) onto the appearance contract in
`Assets/_Project/Code/Core/Iso/`: `CharacterAppearanceRecipe`, `CharacterGarmentDef`,
`CharacterClothingFitDef` and `CharacterClothingValidation`. It follows the span rules in
`backlog/character-creator-and-wardrobe.md` and the owner's rulings of 2026-09-24.

This is a proposal. No code reads it yet. Contract changes are Core (lead-architect), and are marked
**PR 2 decides**. Choices for the owner are marked **owner**.

## 1. How v9 builds a figure

- A build is 18 fields, in this order: shape, age, height, weight, head, skin, hair, hairStyle, beard,
  eyes, eyeShape, garment, outfit, shirt, hat, hatCol, apronCol, bottom.
- The rig composes **one mesh** from all 18. There are no separate clothing shells. A garment re-shapes
  body parts (the tee writes torso, upper, fore, collar and more), and a hat cuts the hair. Shape, age,
  height and weight move every part, through bone lengths and ring widths.
- Colour axes pick ramps (`ramps` in the options, such as `skin.fair` with 6 shades). Some ramps are fixed
  and no axis picks them: LEATHER (belt), BOOT, BRASS, INK and EYE_WHITE.
- An option id is `<axis>.<value>` (`shape.0.25`, `height.-1`, `hat.watchcap`). It is stable and never
  reused. It is **not** a Def id: `shape.0.25` and `height.-1` fail the `type.snake_case` shape. Below,
  an option is either a key stored in the recipe, or the key to a Def with its own id.

## 2. Axis by axis

| Axis | v9 owner | Home in the contract | Options (n) | Default | Creator tab |
|---|---|---|---|---|---|
| shape | person | identity, structural: **new key** | 0 0.25 0.5 0.75 1 (5) | 0.25 | Body |
| age | person | identity: comes from the start preset (`BuildId`); not a control | child youth adult elder (4) | adult | not offered |
| height | person | identity, structural: **new key** | -2 -1 0 1 2 (5) | 0 | Body |
| weight | person | identity, structural: **new key** | -2 -1 0 1 2 (5) | 0 | Body |
| head | person | identity, structural: **new key** | round oval square long heart wide pear (7) | round | Face |
| skin | person | `Identity.Skin` (exists) | porcelain fair rose olive tan bronze umber deep ebony (9) | fair | Body |
| hair | person | `Identity.Hair` (exists) | blond sand black brown auburn ginger salt grey white (9) | blond | Hair |
| hairStyle | person | identity, structural: **new key** | crop mop bob long bun ponytail buzz bald (8) | mop | Hair |
| beard | person | identity, structural: **new key** | none stubble moustache chinstrap goatee mutton full long (8) | stubble | Hair |
| eyes | person | `Identity.Eyes` (exists) | sea sky bark slate amber moss umber (7) | sea | Face |
| eyeShape | person | identity, structural: **new key** | round narrow lidded (3) | round | Face |
| garment | clothing | one `CharacterGarmentDef` per option | 13, §4 | overalls | Clothes |
| bottom | clothing | one `CharacterGarmentDef` per option | trousers shorts skirt (3) | trousers | Clothes |
| hat | clothing | one `CharacterGarmentDef` per option; `hat.none` is no selection | 13 with none | none | Hats |
| outfit | clothing | colour role, §5 | 15 | teal | Clothes |
| shirt | clothing | colour role, §5 | 10 | white | Clothes |
| apronCol | clothing | colour role ("trim"), §5 | 7 | canvas | Clothes |
| hatCol | clothing | the hat's colour role, §5 | 9 | char | Hats |

## 3. Identity (the person)

- **Today** `CharacterIdentityRecipe` holds BuildId, FitId, Skin, Hair and Eyes. Skin, Hair and Eyes take
  the v9 colour keys directly. The ramp ids are `ramp.<axis>.<key>`, as in `CharacterRamps.asset`.
- **Missing: seven structural axes.** These are shape, height, weight, head, hairStyle, beard and
  eyeShape. The contract's own rule is: "Additional structural identity choices follow the same explicit
  fit contract when baked." **PR 2 decides** how to add them, append-only: either seven fields holding
  the value as `buildKey` stores it ("0.25", "-1", "round", "mop"…), or one list of option ids.
- **BuildId** is the start preset's `char_build.*`. The creator starts from one of the eight adult,
  youth and elder presets (fisher, ginny, skipper, nan, deckboss, packer, cutter, hand), and age comes
  from that preset. **Gap:** `Data/Characters/Builds/` has nine Build Defs, but no `char_build.fisher`,
  and the Fisher is the default start.
- **Children** (`age.child`) are not offered in the creator: `rules.notOffered`, and owner ruling 2
  (children are cast-only). The cast's boy and girl keep their presets.
- Changing clothes never changes these fields. Coverage (a hat cutting the hair) is drawn from the
  identity, not written back to it.

## 4. Equipment (what they wear)

Slots in the contract: headwear, top, bottom, outerwear, footwear, accessory. A Category is a slot or
"outfit". Proposed ids, one `CharacterGarmentDef` per file (28 in all), all of the validator's shape
`garment.<snake>`:

| v9 option | Proposed Def id | v9 `top` | Category | OccupiedSlots | Colour roles it wears (the rig's `wears` label) | Offered in the creator |
|---|---|---|---|---|---|---|
| garment.tee | `garment.tee` | yes | top | top | shirt ("T-shirt") | yes |
| garment.longsleeve | `garment.longsleeve` | yes | top | top | shirt ("Top") | yes |
| garment.workshirt | `garment.workshirt` | yes | top | top | shirt ("Shirt") | yes |
| garment.sweater | `garment.sweater` | yes | top | top | shirt ("Jumper") | yes |
| garment.vest | `garment.vest` | yes | top | top | outfit ("Vest and" the bottom), shirt ("Shirt") | yes |
| garment.apron | `garment.apron` | yes | top | top | shirt ("Shirt"), trim ("Apron") | wardrobe only |
| garment.overalls | `garment.overalls` | no | outfit | top, bottom | outfit ("Overalls"), shirt ("Shirt") | yes |
| garment.oilskins | `garment.oilskins` | no | outfit | top, bottom | outfit ("Oilskins"), shirt ("Shirt") | yes |
| garment.skirt | `garment.skirt` | no | outfit | top, bottom | outfit ("Skirt and shawl"), shirt ("Blouse") | yes |
| garment.suit | `garment.suit` | no | outfit | top, bottom | outfit ("Suit"), shirt ("Shirt"), trim ("Tie") | wardrobe only |
| garment.tux | `garment.tux` | no | outfit | top, bottom | outfit ("Tuxedo"), shirt ("Shirt"), trim ("Bow tie") | wardrobe only |
| garment.dress | `garment.dress` | no | outfit | top, bottom | outfit ("Dress"), trim ("Sash") | yes |
| garment.gown | `garment.gown` | no | outfit | top, bottom | outfit ("Gown") | wardrobe only |
| bottom.trousers | `garment.bottom_trousers` | | bottom | bottom | outfit | yes |
| bottom.shorts | `garment.bottom_shorts` | | bottom | bottom | outfit | yes |
| bottom.skirt | `garment.bottom_skirt` | | bottom | bottom | outfit | yes |
| hat.watchcap, .souwester, .ballcap, .kerchief, .flatcap, .hood, .sunhat, .bucket | `garment.hat_<value>` | | headwear | headwear | hatCol | yes |
| hat.tophat, .bowler, .captain, .beret | `garment.hat_<value>` | | headwear | headwear | hatCol | wardrobe only |

- **The `bottom_` and `hat_` prefixes are needed.** v9 has a garment `skirt` ("Skirt and shawl", an
  outfit) and a bottom `skirt`.
- **No footwear, outerwear or accessory options exist in v9.** Every garment paints the boots from the
  fixed BOOT ramp, so the boots are part of the figure.
- **Sections.** Each option's `parts` in the options file names the mesh parts it writes. Garments list
  torso, upper, fore, collar, thigh, pelvis, shin and boot, plus their own pieces: belt, vest, apron, bib,
  strap, shawl, skirt, jacket, lapel and tie. Bottoms list pelvis, shin, boot and skirt. Hats list hair,
  hat and hood. The lists **overlap** (pelvis, shin and boot are in both garments and bottoms), so they
  cannot be used as `SourceSectionIds` as they are: the validator rejects a section used twice.
  **PR 2 decides** who owns each shared part. One proposal: top = torso, upper, fore, collar and the
  pieces above; bottom = pelvis, thigh, shin, skirt; figure = boot. Hats cover the hair through a
  `CoverageTags` entry, not by owning it.

## 5. Colour: the contract gap

The contract gives each garment selection **one** `ColourwayId`, and a colourway lists
`MaterialRoles {SourceMaterial, Role, RampId}`. v9 colours by axes that span the whole figure:

- **outfit** paints `over`, `overD` and `overL`. On the six top garments that paint lands on the
  **bottom**, and on the vest it also lands on the vest. So in v9 a vest and its bottom always share one
  colour.
- **shirt** paints `collar`, `shirt` and `shirtD`. **apronCol** paints `apron` and `apronD`: the apron,
  tie, bow tie and the dress's sash. **hatCol** paints `hat`, `hatD` and `hatL`.

**PR 2 decides** between:

- **(a) Enumerate colourways per garment**, as the product of the roles it wears. This is data only: the
  bake stays one mesh per build plus ramps, so there is no garment × colour × animation bake.

  | Garments | Colourways each |
  |---|---|
  | tee, longsleeve, workshirt, sweater | 10 |
  | apron | 70 |
  | vest | 150 |
  | overalls, oilskins, skirt | 150 |
  | suit, tux | 1,050 |
  | dress | 105 |
  | gown | 15 |
  | each bottom | 15 |
  | each hat | 9 |

  That is 3,083 in all.
- **(b) A colour key per role in the selection**, which is v9's own shape. This is a Core change.

Either way, **the vest's shared outfit colour needs a written rule**: a vest coloured apart from its
bottom has no paint the rig can make.

**Ramps.** v9 ships 66 axis ramps and 6 fixed ones. The current `CharacterRamps.asset` (from rig 6's
`options.json`) has 45 keys:

- 44 are identical in v9.
- `hair.white`'s lightest shade changed: `#e8eae3` → `#dfe1db`.
- 21 keys are new in v9:
  - outfit: black, char, denim, khaki, cream, wine, plum, rose, sky;
  - shirt: black, sky, rose;
  - hatCol: black, white, straw;
  - apronCol: black, red, navy, white;
  - eyes: moss, umber.

The v9 figure needs a ramps asset that carries all 66 keys.

## 6. Fit

- **Proposed:** one fit, `fit.character_v9`:
  - `SourceRigSha256` `02df29ec88dffe9ad68b8761290f4ed66ef73e919346682be43c089be6f49144`;
  - `SkeletonId` `skeleton.character_v9`;
  - `BoneIds` the 28 v9 bones, which every preset shares;
  - `RequiredSlots` top and bottom (an outfit fills both);
  - headwear optional.
- **Why one fit can hold every body:** the rig fits every garment to every body when it builds (ring
  widths). `check-bodies` passes all 548 bodies (creator 375, children 125, extremes 48). The contract
  warns that equal bone counts do not prove shared clothing; the evidence here is the rig's own fitting,
  not the bone count.
- **What could split it:** the player has no JavaScript, so the game's builder must reproduce that
  fitting, or bake per body class. If it bakes per class, the fit splits by that class. **PR 2 decides**,
  with the lead-architect's answer on the builder.

## 7. Choices with nothing to show: not offered (a CW rule)

"The workbench must not offer a body/face control without fitted geometry." From `rules.notOffered`
and `rules.sameFigure`:

| Choice | Why | In the contract |
|---|---|---|
| `age.child` | Not a control; the creator never builds a child (owner ruling 2: cast-only) | Not a creator field |
| Any bottom under overalls, oilskins, skirt, suit, tux, dress or gown | The outfit brings its own bottom; every bottom gives the same mesh. Store trousers | The outfit occupies the bottom slot, so the slot rule forbids it |
| hatCol with no hat | No hat material is painted | No headwear selection, so no colourway |
| apronCol (trim) on tee, longsleeve, workshirt, sweater, vest, overalls, oilskins, skirt, gown | The garment paints no apron material | Those garments carry no trim role |
| shirt on dress and gown | The garment paints no shirt material | Those garments carry no shirt role |
| A vest coloured apart from its bottom | One outfit axis paints both | Needs the rule in §5 |

**Offered, but worth knowing:** under the hood, hair `bob` = `long` and `bun` = `ponytail` give the
same mesh. They stay offered: the hood comes off, and the hair is the person's.

## 8. The base set (owner ruling 3, "base clothing set yes", 2026-09-24)

The creator offers a base set. **Every** clothing option can be worn from the cottage wardrobe; that is
its own charter.

| Axis | In the creator (base set) | Wardrobe only |
|---|---|---|
| garment | tee, longsleeve, workshirt, sweater, vest, overalls, oilskins, skirt, dress | apron, suit, tux, gown |
| bottom | trousers, shorts, skirt | — |
| hat | none, watchcap, souwester, ballcap, flatcap, kerchief, bucket, hood, sunhat | tophat, bowler, captain, beret |
| outfit, shirt, hatCol, apronCol | every colour | — |
| every person axis | every option (age: from the start preset) | — |

- **Looks** (`rules.looks`) are the palette the creator applies when you switch into a piece; the rig
  does not enforce them. They are: suit (char, white, red), tux (black, white, black), tophat and bowler
  black, captain white, sunhat straw. Only the sunhat is in the creator's base set.
- **Owner, with PR 3:** does the base set also become `StarterGrant` ownership in the wardrobe? The
  backlog says starting grants are authored and limited.

## 9. What guards this map

`CharacterRig9OptionGuardTests` checks the rig and this map's premises:

- every offered option survives the rig's own `normBuild` unchanged (none silently falls back to the
  Fisher);
- every offered option builds a figure that differs from its axis default, in geometry or paint, in a
  context where the rules say it has an effect;
- the not-offered set is exactly `age.child`.

A rig revision that breaks one of these fails the guard before the map goes stale.

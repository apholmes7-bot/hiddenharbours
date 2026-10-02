# Hidden Harbours — Character Iso · v10.2 kit

**2026-10-01 · rev 10.2** · rig `Art/characterIsoRig10.js` `cbd50b54…` · pose library `Art/characterIsoRig10.poses.js` `89c7c902…` · checks `Art/characterIsoRig10.checks.js` `9b258599…` (full hashes in `SHA256SUMS.txt` and in every stamped file)

This is the first kit on rig 10. It carries 10.0 (the seven-head figure on the pass-9 ground), 10.1 (one body, sex and body type) and 10.2 (beards, hair, wardrobe, swimwear, a cast of thirty). The export symbol is `CharacterIso10`; the cell is **80 × 104**, pivot **(40, 90)**, 32 px/m, 40°, 8 facings. Skeleton (28 bones), clips (53), the face slots, blink and look-at are as in 9.2.

1. Contents · 2. What changed since 9.2 · 3. The cast · 4. Beards · 5. Hair · 6. Wardrobe · 7. randomBuild · 8. Golden suite · 9. Using it · 10. Open · 11. Packaging

## 1. Contents

| Path | What |
|---|---|
| `Art/characterIsoRig10.js`, `.poses.js`, `.checks.js` | the rig, its pose library and the golden checks (load in that order) |
| `Art/characterIsoRig10_1.js`, `_1.poses.js` | 10.1, frozen (`CharacterIso10_1`); only the review page loads it, for its 10.1 / 10.2 comparisons |
| `Character v10.2.dc.html`, `support.js` | the review page: the wardrobe on 10.2, the cast, the beard and hair comparisons, the new clothes, the checks |
| `builds/presets.json` | the 30 cast builds (`normBuild` + `buildKey`), with `core` (the first ten) and `npcs` |
| `builds/<key>.v10.json` | `exportBuild(key)` for each of the 30: skeleton, bind mesh, face groups, shading, blink, look, every clip (numbers to 7 decimals) |
| `gameplay/characterIsoRig10.<key>.gameplay.json` | `gameplay(key)` sidecars: pins per clip and frame, the collider, the contracts |
| `data/options.v10.json` | every option list, the wardrobe tables (garments with their group and `fem`, bottoms, beards, hats), sex, frames, ages, palettes and the randomBuild rules |
| `data/shading.v10.json` | the shading contract on the Fisher |
| `renders/1x`, `renders/4x` | per build: `idle8` (idle f0 at the eight facings, N first) and `walkS` (the eight walk frames at S); `renders/cast.png`; `renders/manifest.json` |
| `golden-report.json` | the 20 checks on all 30 builds |

## 2. What changed since 9.2

**10.0.** About seven heads: the Fisher stands 1.79 m (9.2: 1.54 m). The head is built at 9.2's size and scaled to 0.65, with the face laid so its marks land on single pixels. Narrower feet with a gap at S. No keyline by default. `render({ sky })` lights the figure as the terrain is lit in that weather, and `shadow({ sky })` gives the cast shadow and the contact patch. The cell grew to 80 × 104.

**10.1.** Every face mark is a single pixel placed for the camera it is seen from, so at SE and SW both eyes show either side of the bridge and the mouth shows at the diagonals. Cloth and limbs are lit on smooth normals, so one material reads as one surface; the arm tapers from a deltoid, the thighs fill the hip, the vest covers the shoulders. Builds carry a **sex** (`m`, `f`) and a **frame**: men lean, average, broad, stocky, heavy; women slender, average, hourglass, pear, full. The deck work's grips stay in reach on any build. Two check fixes: `hair` reads the hairline at the head's unscaled size, and `face` accepts the declared-empty `brows.flat`.

**10.2.** Beards rebuilt (§4). Every hair style has its own shape (§5). Six tops, six outfits, four swimsuits and two bottoms (§6). Twenty NPCs: `CAST` is thirty, `CAST10` the first ten, `NPCS` the new twenty. `randomBuild()` (§7). Two body fixes for mirror symmetry (the `light` check): the shin's top ring sits inside the thigh (two bodies drew a knee pixel differently left and right; on the presets this moves one to three knee pixels), and a bare thigh's top sits inside the briefs. The checks are unchanged from 10.1.

## 3. The cast

| Key | Label | Body | Wearing | Hair | Beard | Hat | Tris | Height m | Gated |
|---|---|---|---|---|---|---|---:|---:|---:|
| `fisher` | Fisher | man, average, adult | overalls | mop | stubble | none | 1,165 | 1.79 | 19/19 |
| `ginny` | Ginny | woman, average, adult | overalls | bob | none | hood | 1,154 | 1.67 | 19/19 |
| `skipper` | Skipper | man, stocky, elder | oilskins | crop | full | souwester | 1,426 | 1.67 | 19/19 |
| `nan` | Nan | woman, full, elder | skirt | bun | none | kerchief | 1,248 | 1.54 | 19/19 |
| `deckboss` | Deck boss | man, broad, adult | vest + trousers | buzz | mutton | flatcap | 1,081 | 1.89 | 19/19 |
| `packer` | Packer | woman, pear, adult | apron + trousers | bun | none | kerchief | 1,145 | 1.65 | 19/19 |
| `cutter` | Cutter | woman, slender, youth | apron + trousers | ponytail | none | none | 1,145 | 1.52 | 19/19 |
| `hand` | Deckhand | man, lean, youth | workshirt + trousers | crop | none | ballcap | 1,107 | 1.70 | 19/19 |
| `boy` | Wharf boy | man, average, child | sweater + trousers | mop | none | watchcap | 1,148 | 1.28 | 19/19 |
| `girl` | Wharf girl | woman, average, child | workshirt + trousers | long | none | none | 1,126 | 1.29 | 18/19 |
| `harbourmaster` | Harbourmaster | man, broad, elder | peacoat + trousers | crop | vandyke | captain | 1,294 | 1.74 | 19/19 |
| `lightkeeper` | Lightkeeper | woman, slender, elder | greatcoat | bun | none | watchcap | 1,200 | 1.66 | 19/19 |
| `ferry` | Ferry captain | woman, average, adult | vest + trousers | ponytail | none | captain | 1,124 | 1.68 | 19/19 |
| `monger` | Fishmonger | man, heavy, adult | apron + trousers | crop | full | flatcap | 1,254 | 1.79 | 19/19 |
| `mender` | Net mender | woman, full, elder | cardigan + longskirt | bun | none | kerchief | 1,236 | 1.54 | 19/19 |
| `boatwright` | Boatwright | man, stocky, adult | singlet + trousers | mop | chinstrap | none | 1,143 | 1.70 | 19/19 |
| `postie` | Postmistress | woman, hourglass, adult | pinafore | bob | none | beret | 1,122 | 1.67 | 19/19 |
| `grocer` | Grocer | man, heavy, elder | cardigan + trousers | bald | moustache | none | 1,115 | 1.66 | 19/19 |
| `diver` | Diver | woman, average, youth | wetsuit | buzz | none | none | 974 | 1.59 | 19/19 |
| `lobsterman` | Lobsterman | man, lean, adult | waders | crop | stubble | ballcap | 1,126 | 1.87 | 19/19 |
| `cafe` | Café owner | woman, pear, adult | breton + skirt | long | none | none | 1,279 | 1.63 | 19/19 |
| `swimmer` | Swimmer | man, average, youth | trunks | crop | none | none | 1,048 | 1.69 | 19/19 |
| `oldsalt` | Old salt | man, average, elder | peacoat + trousers | bald | long | watchcap | 1,332 | 1.72 | 19/19 |
| `lifeguard` | Lifeguard | woman, slender, adult | swimsuit | ponytail | none | bucket | 1,095 | 1.74 | 19/19 |
| `painter` | Painter | man, lean, adult | breton + cropped | mop | goatee | beret | 1,346 | 1.81 | 19/19 |
| `coastguard` | Coastguard | woman, hourglass, adult | lifevest + trousers | ponytail | none | ballcap | 1,251 | 1.68 | 19/19 |
| `mechanic` | Mechanic | man, stocky, adult | coveralls | buzz | sideburns | none | 1,005 | 1.66 | 19/19 |
| `baker` | Baker | woman, full, adult | apron + skirt | bun | none | kerchief | 1,145 | 1.67 | 19/19 |
| `tourist` | Tourist | woman, average, adult | sundress | bob | none | sunhat | 1,062 | 1.67 | 19/19 |
| `paperboy` | Paper boy | man, average, child | hoodie + shorts | mop | none | watchcap | 1,140 | 1.29 | 19/19 |

The twenty new builds use every beard and hair style, all nine skins and all ten frames. Two of them wear swimwear (the Swimmer in trunks, the Lifeguard in a swimsuit). A hat on a build keeps its hair style underneath for when the hat comes off.

## 4. Beards

Ten styles (`BEARDS`): none, stubble, moustache, chinstrap, goatee, Van Dyke (new), sideburns (new), mutton chops, full, long. A style is up to three parts:

* **the skull's lower rings painted:** `r0` (chin ring) and `r1` (jaw ring), by octagon side, in `beard` or `stub`;
* **point marks** laid for the eight facings, as the face marks are. At each design camera, the rig works out which face-row columns that camera sees on the skull, then marks the style's columns among them: `T` the moustache (the nose column and one either side; in profile, the front of the face), `E` the edge nearest the ear (both edges from ahead), `A` every column. They never land on an eye's column and sit under the eyes and the mouth (depth bias 0.018; the moustache 0.030, which draws over the nose);
* **a mass** (chinstrap, goatee, Van Dyke, full, long): a solid from the mouth row down past the chin that stands in front of the collar. In 10.1 the collar hid the chin row, so the goatee drew as a ring round the mouth and mutton chops as a moustache.

`stub` is the skin ramp mixed 36 % toward the hair ramp one step down, so stubble reads on every skin. The mass is part `beard` (skinned to the head, scaled with it).

## 5. Hair

All eight styles keep their hair regions (the `hair` check is unchanged), except the crop's front hairline, which moved up to 1.452.

| Style | 10.2 |
|---|---|
| crop | higher front hairline (1.452); back and sides `hairD` |
| mop | three tufts across the crown (the middle one `hairL`), one at the back; bigger tufts over the ears; a ragged fringe |
| bob | a curtain of hair beside each cheek, temple to jaw, its front behind the profile eye; the fringe's edge `hairD` |
| long | the bob's curtain carried on to the shoulder; the back down to the shoulder blades |
| bun | high on the crown, so it shows above the head from ahead (under a hat it is still the hat's bump) |
| ponytail | a tie and a tail wide enough to keep its pixels at the head's 0.65 scale (10.1's dropped to one floating pixel in profile) |
| buzz | the `buzz` material is the hair ramp mixed 24 % toward the skin |
| bald | a highlight (`skinL`) on the crown's front |

Every style with hair has a sheen: the crown's upper front in `hairL`. New materials: `hairL`, `skinL`.

## 6. Wardrobe

| Group | New | Notes |
|---|---|---|
| tops (take a bottom) | Breton top, hoodie, cardigan, pea coat, life vest, singlet | Breton: rings one pixel row tall (RS) every second row on the torso and down both sleeve bones, so the stripes stay clean at every facing; trim colour = stripes. Cardigan and pea coat: trousers a tone darker (`overD`). Pea coat: lapels and two columns of brass buttons. Life vest: a buoyant shell in the trim colour with a strap and a zip. |
| outfits | chest waders, coveralls, greatcoat, wetsuit, sundress, pinafore | Waders and wetsuit bring their own boot colour (`bootMat`). Greatcoat: skirts to above the knee (part `skirt`, cloth), a scarf in the trim colour with both ends down the front. |
| swimwear | swim trunks, swim briefs, swimsuit, bikini | barefoot (`bare`). Trunks and briefs on a woman come with a top. The bikini top and the swimsuit's neckline are the torso's own surface between extra rings, so they cannot slip under the skin in idle. The cup follows the bust: frame and weight set it, and the top reaches lower and higher as it grows. |
| bottoms | cropped, long skirt | cropped trousers stop mid-calf over a rolled cuff |

New colours: `orange` (outfits, trim), `yellow` (trim). Every garment's `look` gives the colours the creator switches to when you pick it. `fem` marks the garments and bottoms randomBuild never gives a man: skirt and shawl, dress, gown, sundress, pinafore, swimsuit, bikini; skirt, long skirt.

## 7. randomBuild

`CharacterIso10.randomBuild(rnd, { sex, age })` → a normalized build. `rnd` defaults to `Math.random`; pass a seeded generator for repeatable NPCs.

* Sex first (`m` or `f`), unless given; age adult 3 in 6, youth, elder and child 1 in 6 each, unless given.
* **A man never draws a skirt or a dress** (anything marked `fem`). A woman may draw anything; men's swimwear comes with a top.
* Swimwear one draw in twelve.
* Beards only on adult and elder men (3 in 5); a youth draws none, stubble or a moustache.
* Elders draw salt, grey or white hair 3 in 4; nobody else draws grey or white, and salt only on adults. Bald only on adult and elder men.
* A hat 3 in 5.

The review page's Randomise keeps the wardrobe's current age.

## 8. Golden suite

`golden-report.json`: the 20 checks on all 30 builds, **569 / 570 gated checks pass** (`budget` is reported, not gated). The one failure is the Wharf girl's `face` at SW (one eye), carried from 10.1: a child's head sits low, and in idle her far shoulder rises over the far edge of her face. Every build's `cell` fits with 4–5 px spare.

## 9. Using it

```
<script src="Art/characterIsoRig10.js"></script>
<script src="Art/characterIsoRig10.poses.js"></script>
<script src="Art/characterIsoRig10.checks.js"></script>   <!-- optional -->

const C = CharacterIso10;
C.render({ build:'harbourmaster', clip:'walk', frame:3, dir:4 });   // { rgba, W, H, ... }
C.exportBuild('lifeguard');                                        // what builds/*.v10.json holds
C.gameplay('mechanic');                                            // what gameplay/*.json holds
C.randomBuild(Math.random, { sex:'m' });                           // a man, never in a skirt or a dress
```

Builds are plain objects (`C.FIELDS` plus `label`, `name`); any missing or unknown field falls back to the Fisher's, and a build without `sex` reads its sex from its shape and beard. Saved builds use `localStorage['hh.player.build.v10']`. A 10.1 build loads unchanged; its beard and garment names are the same and are drawn the 10.2 way.

## 10. Open

* **The Wharf girl at SW** (above). The same happens to a woman with the long head shape in the creator; the Coastguard has an oval head to avoid it.
* **One crotch pixel** on a girl with the full frame at weight −2 differs between E and W mirrored, in every garment (the thighs' order in profile). Not on any cast build.
* **The Character Creator** still runs rig 9. Moving it to 10.2 means the 80 × 104 cell, the sex and frame controls, the new wardrobe and randomBuild; the review page's wardrobe is the working version.
* **Carried from 10.0:** the outline ruling (§4.6), fixture sizes for the 10.x body, feet on St Peters' slopes, outfit ramps matched to the terrain's.
* The renders draw no tools, blink or look-at (the clips as authored), as in 9.2.

## 11. Packaging

`character-v10.2-kit.zip`: 195 files in 9 folders (about 50 MB unpacked; the 30 build exports are 1.4 MB each). `SHA256SUMS.txt` covers the other 194.

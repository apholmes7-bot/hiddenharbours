# hh-village-return · phase 2 · house styles

2026-09-26. The owner asked for the next phase to improve the models themselves, not the town layout, because every house read as facing south. This pass reworks each HouseIso style in the live look. The phase 1 rigs are kept in `phase1-rigs/` for comparison. Boards are in `boards-phase2/`, and the review page is `Village Houses Phase 2.dc.html` at the project root.

## What stayed fixed

- **Dimensions.** `Wd`, `Ln`, `wallH`, `fH` and `pitch` resolve exactly as before, so every InteriorIso room still registers under its shell. The chimney stack (0, −Ln/2+0.22·Ln), the door anchors and the dormer positions are unchanged.
- **`classic:true`.** Byte-identical to the 09-16 cut. The 20 cases at facings 4 and 6 were compared against the phase 1 rigs.
- **Camera-first rules.** `checks/camera-shells.cjs`, ported to this workspace: 293 of 293 pass, including the new preset. Classic identity is checked separately (20 of 20).
- **Materials.** Slate and green joinery, as ruled in phase 1.

## Every style (live look only)

- **Light.** Grazing roof planes striped themselves because the shadow bias was capped too low. The sun and sky maps now use a slope-scaled bias with a normal offset. This lives in `CoastalPass.light`, so InteriorIso's live look gets the fix too.
- **Slate.** 0.30 m courses of 0.30 m slates, staggered by half a slate, with a darker or paler slate here and there. Standing seam gets a lit rib beside each seam.
- **Cornice.** Eave returns at the gable corners and a frieze board under every eave (tag `trim.cornice`).
- **Doors.** A three-light transom over every door. Colonial, seaside and gothic doors also get sidelights. They glow with the hall lamp (`em:'door'`) and take no shutters. Portico hoods rise to clear the transom.
- **Porches.** Post bases and capitals, a bottom rail and square balusters, a skirt board over lattice down to the ground, fascia up the open ends of the roof, and a pediment over the steps. Houses with gingerbread also get brackets. The door stays ≥ 60 % visible at every listed facing.
- **Chimneys.** Clay pots on every flue. Cape and saltbox get the big stack. The smoke anchor moved up to the pot tops.

## Per style

- **Gambrel.** The profile now matches the interior rig's (steep lower slope, shallow upper), with sided gable ends, a pair of sashes in the gambrel storey, and a long shed dormer across the +X slope. New `PRESETS.gambrelColonial` (blue shingle, eave door).
- **Saltbox.** Two full storeys on the front and one at the back. The gable ends are sided (their laps were missing), and the rear gutter and downpipe now hang at the low eave instead of floating at the front eave's height.
- **Cape.** Squat three-light knee windows under the frieze replace the second row of full sashes, so it reads as a storey-and-a-half. The door has sidelights and a transom.
- **Gothic.** Finials at every gable peak, a peaked hood over each sash, and a pointed-arch sash in the gables in place of the diamond.
- **Gable cottages, sage cottage, farmhouse.** The shared cornice, porch, door and roof work. The ell wing gets its own returns and frieze.
- **School.** `belfry: true` puts a sided belfry with an open bell stage and a pyramid cap on the ridge near the front gable (tag `show.belfry`). The village plan's school shell should set it.

## Facings

`placement()` now lists the diagonal that shows the show face and its designed neighbour first (facing 5 for most houses), then the other diagonal, then the square-on facing. The facings themselves are unchanged, except that a front-porch house with dormers now also drops the facings where its +X side faces north.

## To do on node

- Re-run `node checks/camera-shells.cjs` and the 09-16 checks.
- Run `restamp-kit`. The manifest, the sidecars and the layout proposals still carry the phase 1 rig hashes. `houseIsoRig.params.json` needs the new preset and the `belfry` option. `SHA256SUMS.json` is updated for the two changed rigs.

## Phase 2b · St Peters, options, rooms (2026-09-26)

The owner said yes to roof options, asked for siding colours, mirror, the sill cut on every storey, and rooms that make sense. Then they sent the revised village plan (session 1) and asked for every house to stand out on its own.

- **`HouseIso.CAST`.** The seven St Peters houses from the plan, each with its own shape, siding colour, roof, shutters and door. Each entry records who lives there, the plan's facing and the direction the door faces there, which matches the plan: S on the north side of Green Row, W for the red saltbox and the white farmhouse. All seven pass the camera checks (195 / 195).
  - sage cottage: cedar roof, sage clapboard, ivory shutters.
  - school: slate, belfry, blue shutters.
  - harbour gable: Island Gothic in teal shingle under a red tin roof.
  - cream saltbox: eave front under green tin.
  - white gable: mirrored, with a wrap porch, dormers and black shutters.
  - red saltbox: stands in profile under tin, side door to the W with its lamp post.
  - white farmhouse: mirrored ell, side door to the W.
- **Roofs** follow `roof` in the live look: `slate`, `asphaltGrey` (true three-tab asphalt), `asphaltBrown` (cedar shingle), `metal` (galvanised tin), `metalRed`, `metalGreen`. `gothicRevival` and `dormerCape` now name `slate`; classic falls back exactly as before.
- **Siding colours.** New bodies: `yellow`, `green`, `grey`, `teal`, `charcoal`. `shutters` takes `join` (green), `red`, `blue`, `green`, `ivory`, `iron` (black), `oak` or `none`. `doorPaint` takes the same paints.
- **`mirror: true`** flips the house across its ridge. Every anchor, footprint, dooryard, station and placement is mirrored with it. InteriorIso mirrors the room when `shell.mirror` is set, and `furnishings()` mirrors its x values (the result carries `mirrored: true`).
- **Cutaway.** A section cuts each storey at its own sill. The upper storey's roof and rafters go, and the ground storey's outside stands whole beneath it, with its sashes, door and steps. Partitions are cut at the same height and get a pale cap.
- **Rooms (`cottagePlan`).**
  - Ground floor of a two-storey house: the kitchen is at the back by the hearth and stack (range beside the hearth, sink under a side window, hutch, icebox, table and chairs). The parlour is at the front by the door (settee, armchair, bookcase, long clock, sea chest). A partition between them stops at the stair, and there is a doorway on the far side.
  - Upstairs: a back bedroom round the stairwell (bed, washstand, dresser) and a front bedroom behind a partition. `shell.beds` sets the household (0 means the default of 3): one bed in the back room, the rest in front, or a writing desk for one person.
  - A house with no upper storey puts the bedroom in the front room.
  - The room's doorway now follows the shell's door (side and eave entries included). This closes the sage cottage item from phase 1.
  - Every placed piece has a reachable standing point. The long clock is dropped where the wall has no room for it.
- **Checks.** Classic output is byte-identical (20 / 20, with the new preset values). Camera checks: presets and shells 293 / 293, cast 195 / 195.

## To do on node (phase 2b)

- InteriorIso's default render changes with the new room plan, so the 09-16 room pins and `layout-proposals/*.json` need regenerating, alongside `restamp-kit`.
- The school needs its own interior: a classroom with desks, not a house plan.

## Open

- Shops (general store, post office) come with the shop kit.

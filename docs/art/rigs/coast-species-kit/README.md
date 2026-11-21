# Coast species kit (2026-11-20)

Art for the six current-region species that shipped as data with no drawing of their own
(`docs/art/fish-sprites-owed.md`): rainbow smelt, Atlantic tomcod, cunner, green crab, northern quahog
and Atlantic surf clam.

## What it is

One rig, `Art/coastSpeciesRig.js` (catalog key `coastSpecies`, global `CoastSpecies`), loaded after the
catch pass 2 rigs. It is **additive**: `fishIsoRig2.js`, `crustaceanRig2.js` and `shellfishRig2.js` are
not edited (their sources are pinned by `SHA256SUMS.txt` and their `ORDER` / `KINDS` lists by
`CatchPass2KitTests`). Loading this kit moves zero pixels of the seven pass 2 fish, the crab or the clam
(checked over 1,690 cells, every state at three scales).

- **Smelt, tomcod, cunner** are registered into `FishIso2.SPECIES` and `FishIso2.LOOK` at load, so the
  rig's own loft, ramps, light, poses and mass law draw them. `CatchPass2Baker` installs the kit after the
  fish rig and bakes `FishIso2.ORDER` followed by `CoastSpecies.ORDER`. Their marks (mottling, the
  smelt's silver band, the tomcod's barbel) are painted after the rig's paint, on dry pixels only.
  - smelt: slim (girth 0.017), olive back, silver flank, white belly; 0.15-0.26 m across its def's band.
  - tomcod: a small cod, olive-brown, mottled darker, a pale chin barbel; 0.18-0.32 m.
  - cunner: deep and compact (girth 0.050), brown-green with rust blotches; 0.17-0.31 m.
- **Icons for the three finfish:** `iconCell(sp)` is the deck lay the other icons are cut from (row 2,
  frame 0) rendered at icon scale (`ICON`), because their large rungs are only 8-10 px long. The baker
  writes it to `icon-cells/FishIconCell_<sp>.png`; `FishIconBuilder.TrimmedIcon` trims it into
  `Assets/_Project/Art/Fishing/Icons/FishIcon_<sp>.png`.
- **Green crab:** the pass 2 rock crab recoloured dark green with yellow mottle, five marginal teeth a
  side and three lobes between the eyes on the icon (`greenCrabIcon()`), and an 8-cell 48x48 pot-deck
  sheet in the rock crab sheet's layout (`crabDeckSheet()`: walk 0-3, sidle 0 and 2, rear, defend).
- **Quahog and surf clam:** the pass 2 clam plate re-proportioned per call (its tables are restored after),
  siphon closed: the quahog grey, round and ringed with a purple lip; the surf clam big, tan and
  triangular with a brown margin (`shellIcon(key)`).

## Baking

The `Art/Bake Coast Species` menu item (or `-executeMethod
HiddenHarbours.Tools.RigBaking.CoastSpeciesBaker.BakeCli`), then the usual follow-ups: slice the new
sheets, Build Fish Icons, Build Catch Item Library, Build Fish Art Library.

## Second pass (2026-11-20)

- **Fins the rig has no slot for** (`extraFins`): the tomcod's third dorsal and first anal (three dorsals,
  two anals with the rig's own), the cunner's one long spiny dorsal from the nape nearly to the tail. They
  are posed and lit by the fish rig's law, mirrored in this file, and painted only where the rig left the
  cell empty, so they show where a fin stands clear of the body.
- **Cunner:** darker brown and olive-green with rust blotches, a deeper body (`zflat` 1.28: taller, not
  heavier, since the mass law reads `girth`), and thick lips on the icon.
- **One mark that lasts at world scale** (`worldMark`, cells under 160 opaque px, dry and wet pixels
  alike): the smelt's flank turns silver, the tomcod's dark back splits from its pale belly, and the cunner's
  back goes a step darker.
- **Green crab on deck:** the crab rig's own keyline, the shell's lit front edge a step lighter, and legs and
  claws in a lighter yellow-olive than the shell. The size stays true (smaller than the rock crab).

## Known gaps

- Seen from above in the water, the smelt shows little flank, so its silver line is faint while it swims.
- The tomcod has no pelvic threads. The extra fins are hidden where they lie over the body.
- The green crab's teeth are visible on the icon only; the deck cells are too small for them.
- No held meshes for the three finfish yet, and the crab and clams still share the `crab` / `clam` carry
  kinds.

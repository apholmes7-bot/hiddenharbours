# Fish sprites owed (R1, 2026-11-15)

Slice R1 added six current-region species as data. None had its own art. This was the brief for
art-pipeline; each row said what stood in for the missing art.

## Landed (2026-11-20): the coast species kit

All six now have art of their own, from `docs/art/rigs/coast-species-kit/` (see its README) through the
catch pass 2 pipeline:

- **Smelt, tomcod, cunner:** every fish state at three size rungs (`Art/Fishing/Iso/Fish_<sp>*`), the
  anchors sidecar, fight ladders, held and tote sprites, swim-library rows at their own drawn length, and
  an icon (`Art/Fishing/Icons/FishIcon_<sp>.png`) cut from an icon-scale render of the deck lay.
- **Green crab:** its own pot-deck sheet (`Art/Fishing/GreenCrabDeck.png`, the rock crab's layout) and
  icon (`Art/Fishing/Icons/ShellIcon_green_crab.png`). The pot-deck rule and the Def point at them.
- **Northern quahog, Atlantic surf clam:** icons of their own (`ShellIcon_northern_quahog.png`,
  `ShellIcon_atlantic_surf_clam.png`), pointed at by the Defs and the icon library.

Still owed:

- Held MESHES (ADR 0053) for smelt, tomcod and cunner: the held-prop bake reads the fish rig's own
  `ORDER`, which is pinned at seven. Until then they are held as their own sprites
  (`FishArtEverywhereTests.OwedHeldMesh`).
- Carry and tote art for the green crab, quahog and surf clam: they still draw as the shared `crab` and
  `clam` kinds in the hands and the tote.
- The tomcod's thread-like pelvic rays. (Its third dorsal and second anal, and the cunner's long dorsal
  and thick lips, came in the kit's second pass.)

The table below is the original brief, kept for the description of each animal.

## How a species without art is drawn today

- **Catch icon / Def sprite** (`FishSpeciesDef.Sprite`): optional, and no validator requires it. Where a
  stand-in was declared, the Def points at a neighbour's sprite, and the Flavor text does not claim it is
  the real one.
- **Swimming school** (`Resources/FishSwimSpriteLibrary.asset`): a species with no row falls back to the
  `cod` swim kind (the library's fallback), at the cod's length.
- **Carried / held catch mesh** (`Data/Art/CatchItemLibrary.asset`): rows follow
  `CatchItemLibraryBuilder.KindFor`. A species with no matching kind uses the library's `_fallbackKind`
  (`cod`).

## Owed

| Species (id) | Stand-in today | Real size | Colours | Marks that tell it apart |
|---|---|---|---|---|
| Rainbow smelt (`fish.rainbow_smelt`) | Swim and carry: cod fallback. Icon: none. | 15-22 cm, 20-100 g. Slender, about a fifth of the cod sprite's length. | Translucent olive-green back; a bright silver band down the flank; silvery-white belly; pinkish, purple and blue iridescence on the sides. | Long thin body, large mouth with visible teeth, small adipose fin, deeply forked tail. Should read as a "little silver pencil". |
| Atlantic tomcod (`fish.atlantic_tomcod`) | Swim: cod fallback. Carry: `cod` kind (via "cod" in the id). Icon: none. | 15-30 cm, 50-300 g. A small cod shape, about a third of the cod sprite. | Olive-brown to greenish back, mottled darker; yellowish-olive sides; pale belly. | Like a cod in miniature: three dorsal fins, two anal fins, a chin barbel. Rounded tail (cod's is square). The long, thread-like pelvic fin rays are the tell. |
| Cunner (`fish.cunner`) | Swim and carry: cod fallback. Icon: none. | 10-25 cm, 100-600 g. Deep, compact body. | Mottled brown, rust, olive or blue-grey, matching the weed; darker blotches; paler belly. | A wrasse: one long continuous spiny dorsal fin, thick lips, small mouth, pointed snout, rounded tail. Stocky, not torpedo-shaped. |
| Green crab (`fish.green_crab`) | Icon and pot-deck animal: the rock crab's sprites, declared as a placeholder (`Data/Fish/GreenCrab.asset`, `Data/Traps/PotDeckWork.asset`). Carry: `crab` kind. | Carapace 4-9 cm across, 20-100 g. Smaller than the rock crab. | Dark green to olive on top, often with yellow mottling; underside green, yellow or orange-red in older males. | Carapace wider than long, with **five sharp teeth on each side** of the eyes and three bumps between the eyes. Hind legs pointed (not paddles). Must not read as the rock crab, which is rounder, pinker and freckled. |
| Northern quahog (`fish.northern_quahog`) | Icon: the soft-shell clam's sprite, declared as a placeholder. Carry: `clam` kind. | Shell 6-12 cm, 100-300 g. Thick and heavy. | Dull grey-white to tan outside, with fine concentric growth rings; purple patch inside the shell edge. | Hard, rounded, chunky shell that closes tight (the soft-shell gapes). No neck sticking out. |
| Atlantic surf clam (`fish.atlantic_surf_clam`) | Icon: the soft-shell clam's sprite, declared as a placeholder. Carry: `clam` kind. | Shell 10-20 cm, 200-500 g. The biggest clam on the coast. | Smooth yellowish-white to tan shell; thin brown skin (periostracum) at the edges. | Large, smooth, triangular-oval shell, thicker at the hinge. Clearly bigger than both other clams. |

## Also owed

- A pot-deck animal sheet for the green crab (still, six crawl frames, rear and defend poses), to replace
  the rock crab's stand-in on the deck.
- Swim-kind rows in `FishSwimSpriteLibrary` for smelt, tomcod and cunner once their swim art exists, so
  they stop borrowing the cod's length.
- Catch-item mesh kinds for smelt and cunner (run `CatchItemLibraryBuilder` after adding them).

## Catch icons (done 2026-11-20)

`docs/art/rigs/owed-icons/bakeOwedIcons.js` drew the icons the older species still owed, in the catch
pass 2 style. Each is the Def's `Sprite` and its `IconLibrary` entry (`OwedCatchIconTests` pins both).

| Species | Before | Now |
|---|---|---|
| Herring, mackerel, flounder | 14x3, 19x6 and 21x7 deck-cell trims | Icon-scale renders of the same lay, 43x10, 43x12 and 40x14 |
| Lobster, rock crab | The old flat trap-kit sprites (`Art/Fishing/Lobster.png`, `RockCrab.png`, kept as files) | The rig's field-guide plates, `ShellIcon_lobster.png`, `ShellIcon_rock_crab.png` (the crab in its pot-deck red-brown) |
| Soft-shell clam | The old flat `Sprites/Fish/SoftShellClam.png` (still borrowed by the quahog and the surf clam) | The rig's clam plate, `ShellIcon_soft_shell_clam.png` |
| Oyster, blue mussel, sea scallop | No sprite at all, and no icon library entry | `ShellIcon_american_oyster.png`, `ShellIcon_blue_mussel.png`, `ShellIcon_sea_scallop.png` |

The unused `Sprites/Fish/BlueMussel.png` was not wired: it is an open flat "V" in the old style and
does not read as a mussel beside the set.

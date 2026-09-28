# Brief 2026-09-28: Nine Mile Creek's wharf, an art pass

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch.

## The ask

The owner wants a pass over the wharf at Nine Mile Creek with your newest work:
- the St Peters set pieces and their coastal light;
- the wharf rig kit v2 and the wharf building kit v2;
- the flora and rocks of your cliff and rock kit v6;
- the light engine.

In the owner's words, art is the priority here, and the scene can be rearranged to serve it.

So this pass has more freedom than the St Peters key scenes. **Nothing in today's layout is frozen.** Move the walls, the basin, the float, the sheds, the yard, the parking, the beach and the planting wherever the picture is better, as long as the place still does the jobs in section 3. Where a move costs the game work, say so in NOTES. The owner decides, and the game follows the picture.

The owner attaches one zip, `HH-nine-mile-creek-ground-2026-09-28.zip` (section 2). They may add screenshots of the wharf as it plays today. If they do, those are the record of today's look.

## 1. The place

**What it is.** Read sections 2, 3 and 5 of [the wharf doc](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/docs/design/nine-mile-creek-wharf.md): the identity, the layout, and what the player does here.
- It is the first place where the sea is somebody's job: busy, functional, and a little indifferent to you.
- Boats you cannot afford, traps stacked higher than you, a winch you may not touch yet, and a buyer with an honest price.
- Red mud, gravel, tyre fenders, a rusted shed roof. St Peters is the pretty one; this is the working coast.
- Keep that character, and make it beautiful the way a working place is.

**Where it is.** Read [the mainland doc](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/docs/design/nine-mile-creek-mainland.md). The wharf sits on a low made spit at a creek's mouth, on the mainland, one sandbar across from St Peters. The sections that matter here:
- 1: the geography, the roads and the town lots;
- 5: the coast plan;
- 6 and 6a: the depths, the wharf as terrain, and the float;
- 9: the hinterland, with its fields, hedgerows, trees and marsh.

**How the game builds it today.** The `NineMileCreek*.cs` builders in [App/Editor](https://github.com/apholmes7-bot/hiddenharbours/tree/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor) hold every position. Start with:
- [NineMileCreekWharf.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/NineMileCreekWharf.cs) and [NineMileCreekQuayFace.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/NineMileCreekQuayFace.cs): the walls and their faces;
- [NineMileCreekMooredFleet.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/NineMileCreekMooredFleet.cs): the boats at the wall;
- [NineMileCreekShops.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/NineMileCreekShops.cs), [NineMileCreekTruckPark.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/NineMileCreekTruckPark.cs) and [NineMileCreekRoads.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/NineMileCreekRoads.cs): the buildings, the trucks and the roads;
- [NineMileCreekMainland.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/NineMileCreekMainland.cs), [NineMileCreekFields.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/NineMileCreekFields.cs) and [NineMileCreekWoodLots.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/NineMileCreekWoodLots.cs): the land around it.

A new layout becomes new numbers in these builders.

**Light.** Read [lighting and day-night](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/docs/design/lighting-and-daynight.md). The wharf's lamps are already standing: [NineMileCreekDressing.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/NineMileCreekDressing.cs) places them, and [LampPosts.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/LampPosts.cs) lights them at night. The boats carry their own lights.

## 2. The ground (the attached zip)

The zip holds `NineMileCreekSeabed_HeightTex.png`, a README and a SHA256SUMS.txt. This is the ground the game plays today.

**The settings** are in [NineMileCreekSeabed.asset](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Data/Terrain/NineMileCreekSeabed.asset):
- world centre (0, 0);
- world size 760 × 560 m;
- minimum −6 m, maximum +6 m.

**Reading the picture:**
- The PNG is 1520 × 1120 pixels, 8-bit grey: 2 pixels per metre.
- Height in metres = −6 + 12 × grey / 255.
- Count pixel (col, row) from the top-left. Its centre is at x = −380 + (col + 0.5) / 2 and y = 280 − (row + 0.5) / 2, in metres, with x east and y north. North is up.
- The game samples it as it samples St Peters: bilinearly between pixel centres ([PaintedHeightField.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/World/PaintedHeightField.cs)).

**Checking it.** Its sha256 is `bd16d00743182dbc3706db010560ac0c2d77df538c218505ada92f9dfb8930da`, 80,357 bytes. Check the pixels, not the file's bytes.

**Reshaping it.** The game bakes this map from its builders (`BakeNineMileCreekSeabed` in [TerrainPaintTool.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/TerrainPaintTool.cs)). If you reshape the ground, send the new shape as numbers (outlines, heights and slopes), not as paint.

**The tide** is St Peters' own (the mainland doc, section 4): mean 0, amplitude 2.2 m.

## 3. What the place must still do

Wherever things move, the wharf still has these jobs:

1. **The player's first walk here** (the wharf doc, section 5):
   - arrive on the beach off the sandbar;
   - walk the yard. This is the establishing shot: let it just be a place for thirty seconds;
   - sell clams to a buyer at his truck by the parking;
   - watch a boat unload at the winch, which is for later;
   - walk up the road to the town.
2. **The fleet at the wall.**
   - The boats moor along a quay face the camera can see.
   - The wall berths hold water at spring low (the owner, 09-04).
   - The hulls need room: the owner's 09-06 playtest found 12 m hulls overlapping at a 5.5 m pitch.
   - A float for the small boats, reached by a brow that works through the whole tide, and ladders down the quay face.
3. **Boats come and go**, so there is a way in from open water that works at low tide.
4. **Trucks.** The buyers' trucks and the road fleet reach the parking and the apron, and can turn there.
5. **No processing yet:** no fish plant and no cannery. They are a later tier.
6. **The region** stays 760 × 560 m around the origin. The sandbar and the crossing to St Peters stay where they are.
7. **The look's rules** (STATE.md section 3): the camera looks from the south, at 32 pixels per metre.
   - The old wharf kit drew a tall face only on south-facing edges. That is why today's fleet lies along the north wall's south face.
   - If your kits draw other faces well, say so: it opens up the layout.

## 4. What to send back

**First, a quick look,** so the owner can steer before you build the rest:
- one wide frame of the whole wharf;
- a page on the idea and the layout;
- what moves from today.

**Then the scene,** as one zip, `hh-nmc-wharf-return/`, in the St Peters key scenes' shape:
- `scenes/keyscene.nmc_wharf/scene.json` in your SCHEMA.md's KeySceneDef. Positions are in Nine Mile Creek's ground metres (section 2's frame), with the reading stated in `units`.
- NOTES.md:
  - the idea, in a paragraph;
  - what moved from today and why, old and new, in metres;
  - what each move asks of the game: the berths, the float, the roads, the fleet's routes, the ground bake;
  - the owner's calls, with your leans;
  - the budgets.
- Frames, as PNG masters:
  - a wide establishing frame, by day and at dusk at least;
  - on-foot frames: the yard and the buyer's truck, the winch, the float;
  - night, with the lamps lit;
  - fog or rain, if there is time.
- Any change to the ground, as numbers.
- New pieces as rigs in a kit, like the set pieces kit, each piece with its own data.
- Every rig the scene loads inside the zip; a checker that ends in one `RESULT:` line; SHA256SUMS (STATE.md section 5).

## 5. Propose; don't decide

These are the owner's calls. Give your leans:
- anything that changes how the game plays: the number of berths, where the beach landing is, the road's route to the town;
- ground above +6 m or below −6 m, outside the map's range;
- the wharf doc's section 7 questions: whether the winch becomes usable later, and the breakwater's armour.

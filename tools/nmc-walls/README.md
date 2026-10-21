# tools/nmc-walls: Nine Mile Creek's cliff walls as data (NMC H3)

Nine Mile Creek's walls are `CliffWallDef` assets, one per wall by real id (`wall.nmc_NNN`), in
`Assets/_Project/Data/Terrain/NineMileCreekWalls/`. These scripts read them off the committed scene and the live ground
(G3, `NineMileCreekGround_Height.png`) and write them. Nothing here is typed in: a station kept from the scene carries the
scene's own text, and every other number is read off G3, the tide's assets or the ground plan. Node built-ins only; no
editor.

## Running

```
node tools/nmc-walls/derive-walls.cjs --ground-plan <ground/plan.json> [--plan B|A] [--write] [--out <dir>]
node tools/nmc-walls/measure-features.cjs --ground-plan <ground/plan.json> [--plan B|A] [--out <dir>]
```

- `--ground-plan` is H2's ground plan (re-issue 3, sha256 `06d0217c…`). It is not in the repo; the script checks its hash.
- `--plan B` (the default) banks a wall where G3's face under its toe is one the default bake does not draw (bank, under
  55°): the brow stays on G3 and the toe stands as built. `--plan A` gives no wall there.
- `--write` writes the Defs and their metas. Without it, only the tables are written.
- `--out` is where the tables go: `inventory.csv`, `newfaces.csv`, `walls.json` and `summary-plan<B|A>.md` (and
  `measures.md` from `measure-features.cjs`).

Run twice, the Defs come out the same to the bit.

## What each script does

| Script | Does |
|---|---|
| `g3.cjs` | Decodes G3 and samples it the way `PaintedHeightField` does, in float32. |
| `scene.cjs` | Reads the scene's `CliffWalls` root by text: each wall's four documents and its lines, as numbers and as written. |
| `faces.cjs` | Finds faces along a line on G3. Holds the kit's aspect and batter tables. |
| `facemap.cjs` | Groups G3's steep texels (41° and up) into pieces. A piece that falls a metre or more is a face. |
| `chunks.cjs` | Runs `StPetersCliffWalls`' chunk math over the Defs, so each Def is known to be one chunk before an editor runs. |
| `defs.cjs` | Writes a `CliffWallDef` asset and its meta as Unity's text serialiser does. |
| `derive-walls.cjs` | Gives each wall its one disposition and writes its Def, plus the held Defs for the faces G3 asks for. |
| `newfaces.cjs` | Finds the faces G3 holds that no live wall covers, and gives each a held Def that names the face it needs. |
| `measure-features.cjs` | Measures the bluff, the salt pond's berm, the gully, the creek banks, the wharf's west corner, the faces at x −380, Dan's lot, QuayWalls and the town river's wall. |

### How `derive-walls.cjs` judges a station

Each station is read along its own normal, brow to toe:

| Result | When | Brow and toe |
|---|---|---|
| ON | Brow and toe both within 10 cm of G3. | Kept, with the scene's text. |
| Toe lifted | Brow ON, over a face the default bake draws (1 m or more, 55° or steeper). | Toe moved to the face's foot. |
| Re-lined | Otherwise, the same face under the same law. | Both taken from G3. |
| Banked | Plan B only: brow ON, toe over a face the bake does not draw. | Kept as built. |
| Out | None of the above. | No wall stands there. |

A wall's stations decide its disposition:
- **KEEP:** every station is kept.
- **RE-LINE:** some stations moved.
- **SPLIT:** more than one piece stands.
- **RETIRE:** nothing stands. The Def keeps the id and spawns nothing.

## How the walls reach the scene

N1 step `11-cliff-walls-g3` (`NineMileCreekGroundSteps.Walls`) writes the `CliffWalls` root from the Defs by text, through
`StPetersLayerRefresh.CliffWalls`. It:
- edits standing walls in place;
- deletes each retired wall's four documents;
- adds a split's new walls with new ids.

Planned again on its own output, the step is empty. `NineMileCreekBuilder` builds the walls from the same Defs
(`NineMileCreekCliffWalls.Build(LoadDefs())`); the premise has no path to a wall.

`NineMileCreekWallsAgreeWithGroundTests` holds the Defs, the walls step 11 writes, and G3 together. It needs no baked
faces.

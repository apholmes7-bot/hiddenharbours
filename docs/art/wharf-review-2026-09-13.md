# Waterfront asset review — 13 September 2026

Owner request: review docks, piers, wharves, floating docks and breakwaters against character/boat
scale, find defects, and add aesthetic detail. Pillars P2 (physical scale) and P3 (working coast).

## Result

The ISO structure kit is metric and its main dimensions are appropriate for the shared characters
and small craft. All 83 inspected waterfront textures use 32 PPU, Point filtering and no mipmaps.
The 390 directly serialized waterfront SpriteRenderers inspected in Nine Mile Creek and St Peters
have unit scale through their transform ancestry. These checks do not prove runtime collider,
waterline or occlusion alignment; the live Unity bridge was unavailable.

| Asset / fitting | Authored size |
|---|---|
| Timber floating dock bay | 6 × 2.4 m, 0.4 m freeboard |
| Gangway | 12 × 0.95 m, 9 tide-dependent slope frames per facing |
| Low pier | 10.4 × 3 m, deck at 1.3 m above datum |
| Tall pier | 14 × 4.2 m, deck at 2.8 m above datum |
| Concrete quay module | 16 × 8 m, deck at 5.2 m above datum |
| Rock breakwater module | 18 × 6 m; steep crest profile |
| Ladder | 0.45 m wide, rungs 0.30 m apart, grab rails 0.65 m above deck |
| Rail / bollard / tyre | 1.05 m high / 0.75 m high / 1 m outside diameter |

Comparison plates render the existing character and dory source rigs through the same 40° camera
at 32 pixels/metre. The dory is a separate size reference below the structure, not a claimed moored
placement. No water is baked into the production sprites.

![Unscaled before/after comparison: float, pier, breakwater](wharf-review-2026-09-13.png)

## Fixed and baked

- **Incorrect berth sizes.** The old table called a 14.5 m vessel a coastal packet, versus the
  game's 60 m BoatHullDef, and used obsolete lengths for all six reference hulls. Lengths now match
  those Defs; packet/dragger/lobster beam estimates match the art bible. The checker prevents length
  drift. A module shorter than a complete hull plus clearance no longer claims a berth.
- **Telescoping floating ladder.** Its foot previously stayed at chart datum −1 m while its deck
  rode the tide. Source renders therefore changed ladder length across a tide sweep, and the shipped
  raft carried an unnecessarily long ladder. Its length is now freeboard + 1 m. Fixed ladders retain
  their datum-based reach. Rendered geometry and gameplay metadata use the same foot height.
- **Overlapping fenders.** Tyres and foam fenders shared the same station. Foam now replaces the
  tyre at that station; tests check separation over every preset and five tide levels.
- **Obstructed ladder openings.** Timber/plastic curbs and optional water-side rails now leave
  0.70 m ladder openings. Grab rails, base plates and modest worn yellow shoulders make the access
  readable at character scale.
- **Hollow breakwater crest.** The backing quad connected the two low toes beneath a two-sided
  mound, leaving daylight between upper stones. Backing now follows both slopes through the crest.
- **Gangway freeboard.** Standalone gangways now respect the supplied float freeboard, instead of
  substituting 0.40 m for every float.
- **Working details.** Added visible bolts on cleat/bollard bases, sparse replacement deck boards,
  corner plates and ladder handholds. These reuse existing material ramps and are baked geometry;
  they introduce no runtime objects or update work.
- **Stale sidecar.** Regenerated family samples, fitting defaults, presets and boat references, and
  recorded the source SHA in both gameplay and bake contracts. Only the timber float's cell/pivot
  changed; all existing Unity sprite IDs remain intact. Fourteen production PNG sheets changed.

## Remaining scene issues

The scenes mix generations. In the owner's working scene files, Nine Mile Creek references 96 old
breakwater sprites and 13 old fitting sprites alongside the ISO log cribs, float and gangway.
St Peters references 186 old deck sprites and 10 old fitting sprites. The old ladder is only
9 × 20 pixels, the tyre 11 × 14, and the bollard 11 × 12; they remain visibly undersized beside
the metric character. Their import PPU is correct; the drawings themselves are too small.

Replacing those atlases globally would alter every serialized fitting's pivot and the old tile
compositor's edge geometry. They need an editor scene migration to the metric kit, with walkable
edges, ladder reach and tide exposure checked together. This pass preserves the owner's existing
scene edits and reports that remaining work explicitly. It does not claim all waterfront scenery
is now correctly scaled in play.

The low/tall pier and sheet-cell presets pin decks below the default 4.4 m high-water level. They
must be used with a suitable local tidal range or treated as floodable structures. The narrow,
steep rock-breakwater profile should also be reviewed when replacing a scene's legacy armour;
widening its footprint requires updating that scene's collision and navigation boundaries.

## Validation and reproduction

Before editing, the standalone baker reproduced **all 19 committed sheets byte-for-byte in decoded
RGBA**, including counterclockwise facing order and the 72-cell gangway. This validates the offline
packing path against the existing Unity bake, rather than assuming its orientation and pivots.

Run from the repo root:

```text
node tools/wharf-review.mjs --audit
node tools/wharf-review.mjs --check
node tools/wharf-review.mjs --preview after
```

`--check` covers 216 cells, sheet pixels and dimensions, slice rectangles and pivots, source SHA,
gameplay samples, boat lengths, berth fit, five tide levels, fender separation, ladder rigidity,
rail access, freeboard overrides, solid crest backing, binary alpha, deterministic renders and the
4096 texture limit. `--bake` stages every rendered result in memory before writing, then refreshes
the atlas metadata without changing GUIDs, sprite IDs or file IDs. PNGs remain covered by Git LFS.

Unity EditMode/PlayMode, in-game tide sweeps and a playable-build check were not run: Unity was
open but Pipeline reported no reachable server. Reimport the changed assets and run the existing
ISO bake/wharf tests plus a low/high-water visual check before merging.

"""Emitting a ``hiddenharbours.scene/1`` document for one region.

The contract is the one lead-architect settled from the editor's own reference package on
PR #588 — all seventeen questions `docs/tools/scene-export-contract.md` §2 used to hold open,
answered from `sample-scene.json`'s bytes. That doc is the citation for every field name here;
this module is only its implementation.

Two rulings shape the whole file:

  * **Readers MUST ignore unknown keys, and ``x-`` is the reserved extension prefix.** So every
    fact the repo can state but the format does not name — provenance, staleness, which sidecar
    linked a sheet to its rig — ships under ``x-`` rather than being dropped or disguised.
  * **``call``/``opts`` are write-only.** The editor renders from its own live state and writes
    the call record out of it; no renderer reads one back. An export without them is valid, and
    reconstructing one from a baked sheet would be a second definition of the bake.
"""

import json
import math
import os

from . import heightmap, roads, unityyaml as U
from .repo import ASSETS_PPU

SCHEMA = "hiddenharbours.scene/1"
GENERATED_BY = "tools/scene-export/hh_scene_export.py"

# The three terrain layers, in painter order. All three are pictures the repo does not author as
# tiles: ground is an iso-contour of the painted height map, cliffs are placed surfaces, and the
# creek's roads are painted by the builder from centrelines. None survives outside Unity.
TERRAIN_LAYERS = (
    ("ground", "g", 0, "Ground"),
    ("road", "r", 1, "Road"),
    ("cliff", "c", 2, "Cliff"),
)

_LAYER_UNAVAILABLE = {
    "ground": "The ground is not tiles. It is an iso-contour of the painted height map "
              "(PaintedHeightMap + TerrainSplatSurface, ADR 0014) — an R8 texture stored in Git "
              "LFS, whose bytes are absent from a plain checkout. The map is pinned by its LFS "
              "oid under terrain.x-heightMap instead.",
    "road": "Roads are painted by the region builder from centrelines through RoadKitContract "
            "into two tilemaps (RoadTop/RoadSkirt). No road tilemap exists in the committed "
            "scene, and the blob-47 index is deliberately derived in one place only.",
    "cliff": "Cliffs are placed surfaces (CliffWallSurface components), not painted cells. They "
             "are exported as entities, where they are authored.",
}


def build_document(repo, region, scene, provenance):
    """Assemble one region's document. ``region`` is a :func:`repo.Repo.region_def` mapping."""
    width, height = region["worldSizeMeters"]
    centre_x, centre_y = region["worldCenter"]
    cols, rows = int(round(width)), int(round(height))
    origin_nw = [centre_x - width / 2.0, centre_y + height / 2.0]

    entities, rigs, entity_notes = _entities(
        repo, scene, (centre_x, centre_y), origin_nw, cols, rows)
    paths = _paths(repo, scene)
    terrain, tile_counts = _terrain(repo, region, cols, rows, origin_nw, provenance)

    return {
        "schema": SCHEMA,
        "generatedBy": GENERATED_BY,
        # Deterministic by construction: the committer date of the newest input commit, never the
        # wall clock. See provenance.collect.
        "generatedAt": provenance.get("generatedAt"),
        "region": {
            "id": region["id"],
            "sceneName": region["sceneName"],
            "worldCenter": [_num(centre_x), _num(centre_y)],
            "worldSizeMeters": [_num(width), _num(height)],
            "x-displayName": region["displayName"],
            "x-source": region["asset"],
        },
        "frame": {
            "units": "metres",
            "scale_px_per_m": ASSETS_PPU,
            "axes": "+x east, +y north, origin = region centre",
            "camera": "ADR-0006/0022 \u2014 \u00be from the south, elev 40\u00b0, orthographic",
            "sort": "painter, descending world y (north draws first); sortBias breaks ties",
            "pivots": "cell pivot is top-left origin; unityPivot is normalised bottom-left",
        },
        "terrain": terrain,
        "entities": entities,
        "cliffLines": [],
        "paths": paths,
        "collision": {
            "note": "Derived, never authored. This export ships no collision sidecars: colliders "
                    "in the committed scene are builder output, and the shapes that carry "
                    "gameplay law (quay faces, dock zones, passages) are owned by the region "
                    "builder rather than by any document.",
            "sidecars": [],
        },
        "stats": {
            "entities": len(entities),
            "tiles": tile_counts,
            "x-paths": len(paths),
            "x-rigsPinned": len(rigs),
            "x-tilesNote": "painted cell counts per layer. The reference package uses stamp "
                           "counts here, which double-count overlaps; a true cell count is the "
                           "more useful number and the review asked for one or the other, said.",
        },
        "x-rigs": rigs,
        "x-cliffLinesNote": "Empty: cliff lines are an authoring artefact of the editor. The "
                            "repo's cliffs are placed CliffWallSurface components and ship as "
                            "entities.",
        "x-provenance": provenance | {"entityNotes": entity_notes},
    }


# --- terrain ------------------------------------------------------------------------------

def _terrain(repo, region, cols, rows, origin_nw, provenance):
    """The grid, one top-level legend, and the three layers — painted where the repo declares it.

    Ground and road are rasterised from their own sources of truth (see ``heightmap`` and
    ``roads``); cliff stays unpainted because the repo's cliffs are placed surfaces and ship as
    entities. Every layer's RLE covers ``cols x rows`` exactly, whether painted or not.
    """
    total = cols * rows
    legend, painted, notes = {}, {}, {}

    road_grid, road_note = _road_grid(repo, region, cols, rows, origin_nw)
    if road_grid is not None:
        painted["road"] = road_grid
    notes["road"] = road_note

    ground_grid, ground_note = heightmap.contour(
        repo, provenance.get("heightMap"), cols, rows, origin_nw)
    if ground_grid is not None:
        painted["ground"] = ground_grid
    notes["ground"] = ground_note

    layers, tiles = {}, {}
    for name, code, order, label in TERRAIN_LAYERS:
        grid = painted.get(name)
        # An unpainted layer names no kit: claiming a rig for a layer with nothing on it invites
        # a reader to think the kit was consulted and found empty.
        kit = _layer_kit(repo, name) if grid else {"rig": None, "cell": None, "layerOpts": None}
        materials = _legend_for(grid, code, name, kit, legend) if grid else {}
        layer = {
            "code": code,
            "order": order,
            "label": label,
            "rig": kit.get("rig"),
            "cell": kit.get("cell"),
            "layerOpts": kit.get("layerOpts"),
            "rle": _encode(grid, materials, total),
        }
        tiles[name] = sum(1 for value in grid if value) if grid else 0
        # Every layer is read-only and derived whether or not it carries cells: painting one
        # here does not make it authorable, because the gated importer cannot take it back.
        layer["x-readOnly"] = True
        layer["x-derived"] = _LAYER_DERIVED[name]
        layer["x-authorable"] = False
        if grid is None:
            layer["x-unavailable"] = notes.get(name) or _LAYER_UNAVAILABLE[name]
        if name == "cliff":
            layer["pieces"] = {
                "note": "per-cell ledge piece laid by cliff lines; 0 = autotile from neighbours",
                "legend": {},
                "rle": [[0, total]],
            }
        layers[name] = layer

    terrain = {
        "cellMeters": 1,
        "cols": cols,
        "rows": rows,
        "originNW": [_num(origin_nw[0]), _num(origin_nw[1])],
        "note": "row 0 = north edge. 0 = no tile (water — never baked, the shader owns it). "
                "Runs are [value, count]; sum(count) == cols * rows exactly.",
        "legend": legend,
        "layers": layers,
        "x-heightMap": provenance.get("heightMap"),
    }
    return terrain, tiles


_LAYER_DERIVED = {
    "ground": "an iso-contour of the painted height map at the shore map's DECLARED band floors. "
              "Coarser than what Unity paints, which additionally wiggles the elevation, tests a "
              "sandbar spine and picks a band table by weather sector. Those are logic, not "
              "declarations, and reimplementing them here would be a second coastline.",
    "road": "stroked from the region's declared route table at each way's declared half-width "
            "and rank. Surface material only — the blob-47 tile index stays derived in the one "
            "place the repo derives it.",
    "cliff": "not painted; the repo's cliffs are placed surfaces and ship as entities.",
}


def _road_grid(repo, region, cols, rows, origin_nw):
    """Rasterised ways for a region that declares a road table, else ``(None, why)``."""
    scene_name = region["sceneName"]
    table = f"Assets/_Project/Code/App/Editor/{scene_name}Roads.cs"
    geometry = f"Assets/_Project/Code/App/Editor/{scene_name}Mainland.cs"
    if not repo.exists(table):
        return None, (f"{scene_name} declares no road table — this region has no roads to "
                      "rasterise, which is a fact about the region and not a gap in the export.")
    ways, omitted = roads.read_ways(repo, table, geometry)
    if not ways:
        return None, f"{scene_name}'s road table declares no way this reader can follow"
    grid = roads.rasterise(ways, cols, rows, origin_nw)
    return grid, None


def _layer_kit(repo, layer):
    """``{rig, cell, layerOpts}`` for a layer, from the kit contract that owns it."""
    if layer != "road":
        return {"rig": None, "cell": None, "layerOpts": None}
    contract = "docs/art/rigs/road-path-kit-v3/road.contract.json"
    if not repo.exists(contract):
        return {"rig": None, "cell": None, "layerOpts": None}
    with open(repo.abs(contract), encoding="utf-8", errors="replace") as handle:
        data = json.load(handle)
    tile = data.get("tile")
    return {
        "rig": "RoadKit3",
        "cell": {"w": tile, "head": data.get("head"), "plan": tile, "faceH": data.get("skirt")},
        "layerOpts": data.get("bake") or None,
    }


def _legend_for(grid, code, layer, kit, legend):
    """Assign this layer's materials their prefixed legend keys, in first-seen grid order."""
    materials = {}
    for value in grid:
        if value and value not in materials:
            materials[value] = f"{code}{len(materials) + 1}"
            legend[materials[value]] = {
                "layer": layer,
                "rig": kit.get("rig"),
                "rigSource": _RIG_SOURCES.get(layer),
                "material": value,
            }
    return materials


_RIG_SOURCES = {
    "road": "docs/art/rigs/road-path-kit-v3/roadPathRig3.js",
    "ground": None,
}


def _encode(grid, materials, total):
    """Run-length encode a grid to ``[[value, count], …]`` covering every cell exactly once."""
    if grid is None:
        return [[0, total]]
    runs, current, length = [], None, 0
    for cell in grid:
        value = materials.get(cell, 0) if cell else 0
        if value == current:
            length += 1
        else:
            if length:
                runs.append([current, length])
            current, length = value, 1
    if length:
        runs.append([current, length])
    return runs


# --- entities -----------------------------------------------------------------------------

def _entities(repo, scene, centre, origin_nw, cols, rows):
    entities = []
    rigs = {}
    family_counts = {}
    unresolved_sheets = {}
    inactive = 0
    off_grid = 0
    decor_base = repo.decor_base()

    for game_object_id in scene.walk():
        renderer = None
        y_sort = None
        for component in scene.components_of(game_object_id):
            if component.type_name == "SpriteRenderer" and renderer is None:
                renderer = component
            elif component.type_name == "MonoBehaviour":
                script = U.ref_guid(component.data.get("m_Script"))
                path = repo.path_for_guid(script) if script else None
                if path and os.path.basename(path) == "YSortSprite.cs":
                    y_sort = component
        if renderer is None:
            continue
        sprite_ref = renderer.data.get("m_Sprite") or {}
        sheet_guid = U.ref_guid(sprite_ref)
        internal_id = U.ref(sprite_ref)
        if not sheet_guid or not internal_id:
            continue
        sheet = repo.path_for_guid(sheet_guid)
        if not sheet:
            continue
        entry = repo.sprite_table(sheet).get(internal_id)

        x, y, z, rotation, scale_x, scale_y = scene.world_of_game_object(game_object_id)
        column = int(math.floor(x - origin_nw[0]))
        row = int(math.floor(origin_nw[1] - y))
        in_bounds = 0 <= column < cols and 0 <= row < rows
        if not in_bounds:
            off_grid += 1
        active = scene.active_in_hierarchy(game_object_id)
        if not active:
            inactive += 1

        name = scene.name_of(game_object_id)
        family = _family(entry["name"] if entry else name)
        index = family_counts.get(family, 0) + 1
        family_counts[family] = index

        rig_name, rig_source, evidence, candidates = repo.rig_for_sheet(sheet)
        rig_global = repo.rig_global(rig_source) if rig_source else None
        if rig_source and rig_source not in rigs:
            rigs[rig_source] = {
                "rig": rig_name,
                "rigSource": rig_source,
                "sha256": repo.rig_sha(rig_source),
                "shaRule": "sha256 of the rig's bytes with CR stripped (tr -d '\\r' | sha256sum)",
                "x-declaredBy": evidence,
            }
        if rig_source is None:
            note = unresolved_sheets.setdefault(
                sheet, {"placements": 0, "sidecar": evidence, "candidates": candidates})
            note["placements"] += 1

        hierarchy = scene.hierarchy_path(game_object_id)
        parts = hierarchy.split("/")
        record = {
            # Key order follows the reference package's own entity records.
            "id": f"{family}_{index:03d}",
            "family": family,
            "group": parts[-2] if len(parts) > 1 else None,
            "rig": rig_global or rig_name,
            "rigSource": rig_source,
            "call": _call(rig_global),
            "pos": [_num(x - centre[0]), _num(y - centre[1])],
            "flipX": U.as_int(renderer.data.get("m_FlipX"), 0) != 0,
            "sortBias": _sort_bias(y_sort, decor_base),
            "cell": _cell(entry),
            "x-name": name,
            "x-path": hierarchy,
            "x-cellAt": [column, row],
            "x-inBounds": in_bounds,
            "x-active": active,
            "x-rigSha256": rigs[rig_source]["sha256"] if rig_source else None,
            "x-familyIsSpriteStem": True,
            "x-sprite": {
                "sheet": sheet,
                "name": entry["name"] if entry else None,
                "internalId": internal_id,
            },
        }
        if entry:
            record["x-pivotSource"] = entry["pivotSource"]
        else:
            record["x-pivotSource"] = "unresolved — sprite not in the sheet's import settings"
        if y_sort is not None:
            record["x-sortBiasSource"] = (
                f"YSortSprite._baseOrder minus SortingBands.DecorBase ({decor_base})")
        else:
            order = renderer.data.get("m_SortingOrder")
            if order not in (None, ""):
                record["x-sortingOrder"] = U.as_int(order)
        if rotation:
            record["x-rotationDegrees"] = _num(rotation)
        if scale_x != 1.0 or scale_y != 1.0:
            record["x-scale"] = [_num(scale_x), _num(scale_y)]
        if U.as_int(renderer.data.get("m_FlipY"), 0) != 0:
            # The format names flipX only; a vertical flip has nowhere to go, so it is stated
            # rather than dropped silently.
            record["x-flipY"] = True
        entities.append(record)

    notes = {
        "inactiveInHierarchy": inactive,
        "offGrid": off_grid,
        "unresolvedRigPlacements": sum(v["placements"] for v in unresolved_sheets.values()),
        "unresolvedSheets": dict(sorted(unresolved_sheets.items())),
        "x-unresolvedMeaning": "no sidecar beside these sheets names a rig this exporter will "
                               "trust — an ambiguous one names several. The kits that ship a "
                               "*.contract.json resolve exactly; the rest have no committed link "
                               "from sheet to rig, so nothing is pinned rather than guessed.",
        "x-familyVocabulary": "The format's `family` vocabulary is the editor's RigKit ids "
                              "(dory, punt, lobsterboat, camper, character, pot, rock, …). These "
                              "ids are the SPRITE NAME STEM instead, because a baked sheet does "
                              "not record which palette family drew it. Every entity flags this "
                              "with x-familyIsSpriteStem so nothing mistakes one for the other.",
        "x-absentEntityFields": "facing, facingIndex and gameplaySidecar are named by the format "
                                "and absent here. The first two are the editor's own view "
                                "state; the third is a rig gameplay measurement. A baked sprite "
                                "records none of them, and no rig is executed by this exporter.",
        "x-noCallRecord": "call/opts are absent by ruling: the editor writes them out of its own "
                          "live state and no renderer reads one back. The cost is that this "
                          "document can never seed a round-trip INTO the editor, which needs "
                          "family+dir+opts.",
    }
    ordered_rigs = [rigs[key] for key in sorted(rigs)]
    return entities, ordered_rigs, notes


def _call(rig_global):
    """``{fn, args, opts}`` for an entity the catalog can resolve — or ``null``.

    The editor draws only by calling a rig, so an entity without this is an entity it cannot
    render. What can honestly be supplied is the **function and the rig's own defaults**:
    ``opts`` stays empty because a baked sheet does not record which option axes produced a
    given cell, and the rigs fail *soft* — the review measured a mistyped key rendering a
    different object at a different cell size rather than throwing. An empty opts draws the
    rig's default build; a guessed one draws something that is confidently wrong.
    """
    if not rig_global:
        return None
    return {
        "fn": "render",
        "args": ["dir", "opts"],
        "opts": {},
        "x-synthesised": True,
        "x-optsNote": "empty on purpose: the option axes that produced this baked cell are not "
                      "recorded anywhere, and the rigs resolve an unknown key as a silent "
                      "fallback rather than an error. Defaults draw something true; a guess "
                      "draws something confidently wrong.",
    }


def _cell(entry):
    """``{w, h, pivot, pxPerM, unityPivot}`` — or ``null``, as the editor itself emits.

    Both pivot forms ship together, as in the reference package: ``pivot`` in the rig's own
    top-left pixel coordinates and ``unityPivot`` normalised from the bottom left. Only the
    second is read from the import settings; the first is its exact inverse under ADR 0026's
    ``unityPivotY = (h - pivotY) / h``, so the two cannot disagree.
    """
    if not entry:
        return None
    width, height = entry["cell"]
    ux, uy = entry["unityPivot"]
    return {
        "w": _num(width),
        "h": _num(height),
        "pivot": [_num(width * ux), _num(height * (1.0 - uy))],
        "pxPerM": _num(entry["ppu"]),
        "unityPivot": [_num(ux), _num(uy)],
    }


def _sort_bias(y_sort, decor_base):
    """A y-sort **tie-break delta**, not an absolute sorting order.

    ``YSortSprite._baseOrder`` is an absolute order around ``SortingBands.DecorBase``; the
    format's ``sortBias`` only breaks ties between two things at the same world y. Shipping the
    absolute number would read as a bias of about twelve hundred, so what goes out is the delta
    from the band base — 0 for everything the builder left at the default.
    """
    if y_sort is None:
        return 0
    return _num(U.as_float(y_sort.data.get("_baseOrder"), float(decor_base)) - decor_base)


def _family(sprite_name):
    """``trapStack_3`` -> ``trapStack``; ``Armour_185_96.8`` -> ``Armour``."""
    if not sprite_name:
        return "unknown"
    head = sprite_name.split("_")[0]
    return head or sprite_name


# --- paths --------------------------------------------------------------------------------

def _paths(repo, scene):
    """Walkable lanes, where the region has them, in the format's ``paths[]`` shape.

    ``RoutineLanes`` already holds the region's lane network — node positions, a parent tree and
    flattened bend points — and the review is explicit that an imported path should converge on
    that table rather than sit beside it. So the export reads the lanes rather than minting a
    third polyline table, and marks them derived so nothing treats them as authorable here.

    ``curve.kind`` is ``polyline``, not the editor's ``catmull-rom``: these segments are straight
    runs through explicit bend points, so ``polyline`` equals ``nodes`` exactly and no smoothing
    is implied that the villagers do not walk. ``tiles`` is 0 — it is a stamp count, and a lane
    paints nothing.
    """
    paths = []
    for component in scene.behaviours.values():
        script = U.ref_guid(component.data.get("m_Script"))
        path = repo.path_for_guid(script) if script else None
        if not path or os.path.basename(path) != "RoutineLanes.cs":
            continue
        data = component.data
        positions = [U.vec(p, "x", "y") for p in data.get("_nodePositions") or []]
        parents = _packed_ints(data.get("_nodeParents"))
        names = list(data.get("_nodeNames") or [])
        via_start = _packed_ints(data.get("_viaStart"))
        via_count = _packed_ints(data.get("_viaCount"))
        via = [U.vec(p, "x", "y") for p in data.get("_via") or []]
        for index, position in enumerate(positions):
            parent = parents[index] if index < len(parents) else -1
            if parent < 0 or parent >= len(positions):
                continue
            start = via_start[index] if index < len(via_start) else 0
            count = via_count[index] if index < len(via_count) else 0
            bends = via[start:start + count]  # stored child -> parent
            nodes = [[_num(px), _num(py)]
                     for px, py in [position] + list(bends) + [positions[parent]]]
            paths.append({
                "id": f"lane_{names[index] if index < len(names) else index}",
                "layer": None,
                "material": "lane",
                "widthMeters": None,
                "closed": False,
                "curve": {"kind": "polyline", "uniform": True, "tangentScale": 0},
                "nodes": nodes,
                "polyline": nodes,
                "tiles": 0,
                "x-readOnly": True,
                "x-derived": "RoutineLanes — the region builder writes it; villagers read it.",
                "x-layerNote": "null: a walkable lane is not one of the three painted terrain "
                               "layers. Two of St Peters' lanes are drawn by the terrain "
                               "painter, but the lane table is not a layer.",
                "x-from": names[index] if index < len(names) else str(index),
                "x-to": names[parent] if parent < len(names) else str(parent),
            })
    paths.sort(key=lambda p: p["id"])
    return paths


def _packed_ints(value):
    """Unity writes ``int[]`` as a little-endian hex blob; decode it back to signed ints."""
    if isinstance(value, list):
        return [U.as_int(v) for v in value]
    if not isinstance(value, str) or not value:
        return []
    text = value.strip()
    if len(text) % 8 or any(c not in "0123456789abcdefABCDEF" for c in text):
        return []
    out = []
    for i in range(0, len(text), 8):
        out.append(int.from_bytes(bytes.fromhex(text[i:i + 8]), "little", signed=True))
    return out


# --- serialisation --------------------------------------------------------------------------

def _num(value):
    """Round to a stable decimal so the same commit always emits the same bytes."""
    rounded = round(float(value), 6)
    if rounded == int(rounded) and abs(rounded) < 1e15:
        return int(rounded)
    return rounded


def dumps(document):
    """Deterministic JSON, with short scalar arrays kept on one line.

    A coordinate pair exploded over four lines is unreadable and quadruples the file; keeping
    ``[103.5, 92.75]`` inline makes a package diffable by eye. Nothing here depends on dict
    iteration order beyond the order fields were authored in, so the same commit always emits
    the same bytes.
    """
    return _render(document, 0) + "\n"


_INLINE_WIDTH = 96


def _render(value, depth):
    pad = "  " * depth
    inner = "  " * (depth + 1)
    if isinstance(value, dict):
        if not value:
            return "{}"
        parts = [f"{inner}{json.dumps(k, ensure_ascii=False)}: {_render(v, depth + 1)}"
                 for k, v in value.items()]
        return "{\n" + ",\n".join(parts) + "\n" + pad + "}"
    if isinstance(value, list):
        if not value:
            return "[]"
        rendered = [_render(item, depth + 1) for item in value]
        flat = "[" + ", ".join(rendered) + "]"
        if "\n" not in flat and len(flat) + len(inner) <= _INLINE_WIDTH:
            return flat
        return "[\n" + ",\n".join(inner + r for r in rendered) + "\n" + pad + "]"
    return json.dumps(value, ensure_ascii=False, allow_nan=False)

"""Emitting a ``hiddenharbours.scene/1`` document for one region.

The contract is reconstructed from ``docs/tools/scene-editor-review.md`` (#571, corrected by
#576), which is the only description of the format the repo holds. Fields the review names are
emitted under its names; fields it never names are emitted under ``x-`` prefixed names so a
consumer can tell a reconstructed field from a specified one at a glance. Every ruling the
review makes is honoured:

  * the region table comes from the ``RegionDef``, never from a hardcoded size (§6.2)
  * every layer's RLE sums to exactly ``cols x rows`` (§5 Q1, §8.2)
  * ``unityPivot`` is shipped, the rig's own top-left pivot is not (§5 Q3)
  * every rig referenced is pinned by the LF sha256 of its bytes (§6.1)
  * derived layers are marked derived, so nothing unmarked is later trusted (§5 Q5)
"""

import json
import math
import os

from . import unityyaml as U
from .repo import ASSETS_PPU

FORMAT = "hiddenharbours.scene/1"

# The layers the review names. All three are terrain pictures the repo does not author as tiles:
# ground is an iso-contour of the painted height map, cliffs are placed surfaces, and NMC's roads
# are painted by the builder from centrelines. None can be reconstructed outside Unity.
TERRAIN_LAYERS = ("ground", "cliff", "road")


def build_document(repo, region, scene, provenance):
    """Assemble one region's document. ``region`` is a :func:`repo.Repo.region_def` mapping."""
    width, height = region["worldSizeMeters"]
    centre_x, centre_y = region["worldCenter"]
    cols, rows = int(round(width)), int(round(height))
    origin_nw = [centre_x - width / 2.0, centre_y + height / 2.0]

    entities, rigs, entity_notes = _entities(repo, scene, origin_nw, cols, rows)
    paths = _paths(repo, scene)

    document = {
        "format": FORMAT,
        "region": {
            "id": region["id"],
            "name": region["displayName"],
            "scene": region["sceneName"],
            "worldSizeMeters": [_num(width), _num(height)],
            "worldCenter": [_num(centre_x), _num(centre_y)],
            "x-source": region["asset"],
        },
        "frame": {
            "ppu": ASSETS_PPU,
            "cellMeters": 1,
            "originNW": [_num(origin_nw[0]), _num(origin_nw[1])],
            "camera": "3/4 top-down, ADR-0006/0022. Prose for the reader; never parse it.",
        },
        "terrain": _terrain(repo, cols, rows, provenance),
        "paths": paths,
        "entities": entities,
        "x-rigs": rigs,
        "stats": {
            "entities": len(entities),
            "paths": len(paths),
            "x-rigsPinned": len([r for r in rigs if r.get("sha256")]),
            "x-note": "counts of records in this document. Not a checksum of anything painted "
                      "(review §5 Q1: stats.tiles was mistaken for one).",
        },
        "x-provenance": provenance | {
            "entityNotes": entity_notes,
        },
    }
    return document


# --- terrain ------------------------------------------------------------------------------

def _terrain(repo, cols, rows, provenance):
    total = cols * rows
    layers = {}
    for name in TERRAIN_LAYERS:
        layers[name] = {
            "legend": {"0": "unpainted — not readable outside Unity (see x-unavailable)"},
            "rle": [[0, total]],
            "x-readOnly": True,
            "x-derived": True,
            "x-authorable": False,
            "x-unavailable": _LAYER_REASONS[name],
        }
    return {
        "cols": cols,
        "rows": rows,
        "layers": layers,
        "x-rleRule": "runs are [value, count]; sum(count) == cols * rows exactly (review §5 Q1).",
        "x-heightMap": provenance.get("heightMap"),
    }


_LAYER_REASONS = {
    "ground": "The ground is not tiles. It is an iso-contour of the painted height map "
              "(PaintedHeightMap + TerrainSplatSurface, ADR 0014) — an R8 texture stored in Git "
              "LFS, whose bytes are absent from a plain checkout. The map is pinned by its LFS "
              "oid under terrain.x-heightMap instead.",
    "cliff": "Cliffs are placed surfaces (CliffWallSurface components), not painted cells. They "
             "are exported as entities, where they are authored.",
    "road": "Roads are painted by the region builder from centrelines through RoadKitContract "
            "into two tilemaps (RoadTop/RoadSkirt). No road tilemap exists in the committed "
            "scene, and the blob-47 index is deliberately derived in one place only (review "
            "§5 Q5).",
}


# --- entities -----------------------------------------------------------------------------

def _entities(repo, scene, origin_nw, cols, rows):
    entities = []
    rigs = {}
    family_counts = {}
    unresolved_sheets = {}
    inactive = 0
    off_grid = 0

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

        record = {
            "id": f"{family}_{index:03d}",
            "family": family,
            "x-name": name,
            "x-path": scene.hierarchy_path(game_object_id),
            "x-atMeters": [_num(x), _num(y)],
            "x-cellAt": [column, row],
            "x-inBounds": in_bounds,
            "x-active": active,
            "rig": rig_name,
            "rigSource": rig_source,
            "x-rigSha256": rigs[rig_source]["sha256"] if rig_source else None,
            "sprite": {
                "sheet": sheet,
                "name": entry["name"] if entry else None,
                "x-internalId": internal_id,
            },
        }
        if entry:
            record["cell"] = {"w": _num(entry["cell"][0]), "h": _num(entry["cell"][1])}
            record["unityPivot"] = [_num(entry["unityPivot"][0]), _num(entry["unityPivot"][1])]
            record["x-pivotSource"] = entry["pivotSource"]
            record["x-ppu"] = _num(entry["ppu"])
        else:
            record["cell"] = None
            record["unityPivot"] = None
            record["x-pivotSource"] = "unresolved — sprite not found in the sheet's import settings"
        if rotation:
            record["x-rotationDegrees"] = _num(rotation)
        if scale_x != 1.0 or scale_y != 1.0:
            record["x-scale"] = [_num(scale_x), _num(scale_y)]
        flip_x = U.as_int(renderer.data.get("m_FlipX"), 0) != 0
        flip_y = U.as_int(renderer.data.get("m_FlipY"), 0) != 0
        if flip_x or flip_y:
            record["x-flip"] = [flip_x, flip_y]
        if y_sort is not None:
            record["sortBias"] = _num(U.as_float(y_sort.data.get("_baseOrder"), 0.0))
            record["x-sortBiasSource"] = "YSortSprite._baseOrder"
        else:
            order = renderer.data.get("m_SortingOrder")
            if order not in (None, ""):
                record["x-sortingOrder"] = U.as_int(order)
        entities.append(record)

    notes = {
        "inactiveInHierarchy": inactive,
        "offGrid": off_grid,
        "unresolvedRigPlacements": sum(v["placements"] for v in unresolved_sheets.values()),
        "unresolvedSheets": dict(sorted(unresolved_sheets.items())),
        "x-unresolvedMeaning": "no sidecar beside these sheets names a rig this exporter will "
                               "trust — an ambiguous one names several. The 11 kits that ship a "
                               "*.contract.json resolve exactly; the rest have no committed link "
                               "from sheet to rig, so nothing is pinned rather than guessed.",
        "x-noCallRecord": "call/opts are deliberately absent. The sheets are baked; the option "
                          "axes that produced a given cell are not recorded per instance, and the "
                          "review rules the call record provenance-only (§5 Q2). Inventing one "
                          "would be a second definition of the bake.",
    }
    ordered_rigs = [rigs[key] for key in sorted(rigs)]
    return entities, ordered_rigs, notes


def _family(sprite_name):
    """``trapStack_3`` -> ``trapStack``; ``Armour_185_96.8`` -> ``Armour``."""
    if not sprite_name:
        return "unknown"
    head = sprite_name.split("_")[0]
    return head or sprite_name


# --- paths --------------------------------------------------------------------------------

def _paths(repo, scene):
    """Walkable lanes, where the region has them, as polylines.

    ``RoutineLanes`` already holds the region's lane network — node positions, a parent tree and
    flattened bend points. The review's §4 is explicit that an imported path should converge on
    this table rather than sit beside it, so the export reads the lanes rather than minting a
    third polyline table, and marks them derived so nothing treats them as authorable here.
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
            nodes = [position] + list(bends) + [positions[parent]]
            paths.append({
                "id": f"lane_{names[index] if index < len(names) else index}",
                "material": "lane",
                "widthMeters": None,
                "nodes": [[_num(px), _num(py)] for px, py in nodes],
                "x-readOnly": True,
                "x-derived": "RoutineLanes — the region builder writes it; villagers read it.",
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
        word = int.from_bytes(bytes.fromhex(text[i:i + 8]), "little", signed=True)
        out.append(word)
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

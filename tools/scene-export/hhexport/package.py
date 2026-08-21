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
import hashlib
import re
import os

from . import families, recipes, heightmap, roads, unityyaml as U
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


# The inline height field is a SHADING WASH, not a survey: the editor shades tide and shore with
# it and reads the full-resolution map from the referenced file when it needs one. 8 m keeps a
# 760x560 region at 95x70 samples - a few tens of KB - instead of the 425,600 a per-cell grid costs.
HEIGHT_FIELD_STRIDE_M = 8


def build_document(repo, region, scene, provenance):
    """Assemble one region's document. ``region`` is a :func:`repo.Repo.region_def` mapping."""
    width, height = region["worldSizeMeters"]
    centre_x, centre_y = region["worldCenter"]
    cols, rows = int(round(width)), int(round(height))
    origin_nw = [centre_x - width / 2.0, centre_y + height / 2.0]

    entities, rigs, entity_notes, rig_versions = _entities(
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
        "x-rigVersions": rig_versions,
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

    road_grid, road_note, road_omitted = _road_grid(repo, region, cols, rows, origin_nw)
    if road_grid is not None:
        painted["road"] = road_grid
    notes["road"] = road_note

    ground_grid, ground_note = heightmap.contour(
        repo, provenance.get("heightMap"), cols, rows, origin_nw)
    if ground_grid is not None:
        painted["ground"] = ground_grid
    notes["ground"] = ground_note
    # Stamped in place so both x-provenance.heightMap and terrain.x-heightMap carry it: the
    # height texture is a Git LFS object, so the SAME commit exports a contoured ground where
    # the bytes are present and an empty one where they are not. Determinism is per-commit AND
    # per-LFS-state; without this flag the two are indistinguishable in the file, and --check
    # would report a plain "STALE" that sends a reader hunting for a scene re-bank.
    height = provenance.get("heightMap")
    if height is not None:
        height["textureBytesRead"] = ground_grid is not None

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
        if name == "road" and road_omitted:
            # Named, not solved for. Each entry says what it is and why this reader leaves it.
            layer["x-omitted"] = road_omitted
        if name == "cliff":
            layer["pieces"] = {
                "note": "per-cell ledge piece laid by cliff lines; 0 = autotile from neighbours",
                "legend": {},
                "rle": [[0, total]],
            }
        layers[name] = layer

    field, field_note = heightmap.sample_field(
        repo, provenance.get("heightMap"), cols, rows, origin_nw, HEIGHT_FIELD_STRIDE_M)
    terrain = {
        "cellMeters": 1,
        "cols": cols,
        "rows": rows,
        "originNW": [_num(origin_nw[0]), _num(origin_nw[1])],
        "note": "row 0 = north edge. 0 = no tile (water — never baked, the shader owns it). "
                "Runs are [value, count]; sum(count) == cols * rows exactly.",
        # Mean water, from the region's own RegionDef.TideMeanLevel. NOT a constant sea: the tide
        # is recomputed from (worldSeed, gameTime) and swings +/- TideAmplitude about this number,
        # so a shading wash drawn at this level is drawn at MEAN water, not at the water now.
        "waterLevelMeters": _num(region.get("tideMeanLevel", 0.0)),
        "x-waterDatum": "metres relative to chart datum \u2014 the same datum the height map's "
                        "elevations use, so elevation and water level are directly comparable. "
                        "TideModel.Height is `MeanLevel + amplitude * carrier`; this is MeanLevel.",
        "x-tide": {
            "amplitudeMeters": _num(region.get("tideAmplitude", 0.0)),
            "phaseHours": _num(region.get("tidePhaseHours", 0.0)),
            "x-note": "given so a tide-aware wash can swing; the exporter states the model's "
                      "declared terms and does not evaluate it (no time, and rule 5 says the "
                      "tide is recomputed, never stored).",
        },
        "legend": legend,
        "layers": layers,
        "x-heightMap": provenance.get("heightMap"),
        "x-heightField": field if field is not None else {
            "strideMeters": HEIGHT_FIELD_STRIDE_M,
            "values": None,
            "x-unavailable": field_note,
        },
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
    """``(grid, why, omitted)`` — rasterised ways, plus every surface this reader will not solve.

    ``omitted`` is returned rather than dropped because a road layer that quietly leaves out the
    truck-park spur, the town walks and all four paved pads reads as "the region has no such
    thing". It does; they are simply computed elsewhere, and the package should say so.
    """
    scene_name = region["sceneName"]
    table = f"Assets/_Project/Code/App/Editor/{scene_name}Roads.cs"
    geometry = f"Assets/_Project/Code/App/Editor/{scene_name}Mainland.cs"
    if not repo.exists(table):
        return None, (f"{scene_name} declares no road table — this region has no roads to "
                      "rasterise, which is a fact about the region and not a gap in the export."), []
    ways, omitted = roads.read_ways(repo, table, geometry)
    if not ways:
        return None, f"{scene_name}'s road table declares no way this reader can follow", omitted
    grid = roads.rasterise(ways, cols, rows, origin_nw)
    return grid, None, omitted


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
    unlisted_families = {}
    recipe_refusals = []
    inactive = 0
    off_grid = 0
    decor_base = repo.decor_base()
    wire_families, _wire_layers = families.load(repo)

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
        rig_name, rig_source, evidence, candidates = repo.rig_for_sheet(sheet)
        rig_global = repo.rig_global(rig_source) if rig_source else None

        # The editor's `family` vocabulary is closed, and a name outside it is not renderable
        # there. Resolve onto it where the rig's name lands exactly; otherwise keep the sprite
        # stem, say so per-entity, and list the near-miss so a new entry can be requested —
        # never bend it onto a neighbour.
        wire_family, candidate = families.resolve(rig_source, wire_families)
        stem = _family(entry["name"] if entry else name)
        family = wire_family or stem
        index = family_counts.get(family, 0) + 1
        family_counts[family] = index
        if rig_source and not wire_family:
            note = unlisted_families.setdefault(
                candidate or "(unnamed)",
                {"placements": 0, "rigSource": rig_source, "spriteStem": stem})
            note["placements"] += 1
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
            "id": _stable_id(hierarchy, x - centre[0], y - centre[1]),
            "family": family,
            "group": parts[-2] if len(parts) > 1 else None,
            "rig": rig_global or rig_name,
            "rigSource": rig_source,
            "call": _call(rig_global),
            "pos": [_num(x - centre[0]), _num(y - centre[1])],
            # The reference package carries BOTH, and they are different types: `facing` is a
            # compass name ("S", "SE") and `facingIndex` is the baked step. An integer in
            # `facing` would be a confidently wrong value in a field a reader expects to be a
            # string, so the index goes only where the index belongs.
            #
            # `facing` stays null deliberately. Turning a step into a bearing is the one piece of
            # arithmetic this repo has already got wrong: BuildingFacing's remarks record that
            # cell `i` is baked at `dir = (facings - i) mod facings` — bearings DECREASE as the
            # index rises — and the inverted version put the schoolhouse door ~92 degrees off the
            # green, with a passing test, because the test was the algebraic inverse of the
            # implementation. The write-back contract's answer is that nothing derives a facing
            # from an angle; the index is the fact, and a bearing is read back from baked anchors.
            "facing": None,
            "facingIndex": _facing_index(entry["name"] if entry else None),
            "flipX": U.as_int(renderer.data.get("m_FlipX"), 0) != 0,
            "sortBias": _sort_bias(y_sort, decor_base),
            "cell": _cell(entry),
            "x-name": name,
            "x-path": hierarchy,
            # The builder's OWN grouping — the scene root this object was parented under —
            # given whole rather than left to be parsed back out of `x-path`. The editor was
            # inferring zones from the path and misclassifying any row whose key ends in a
            # digit, which told the owner he could not move a building he can. Nothing here is
            # normalised, stripped or title-cased: it is the root's name as the builder wrote it.
            "x-origin": hierarchy.split("/")[0] if hierarchy else None,
            "x-cellAt": [column, row],
            "x-inBounds": in_bounds,
            "x-active": active,
            "x-rigSha256": rigs[rig_source]["sha256"] if rig_source else None,
            "x-familyIsSpriteStem": wire_family is None,
            "x-sprite": {
                "sheet": sheet,
                "name": entry["name"] if entry else None,
                "internalId": internal_id,
            },
        }
        if wire_family is None:
            record["x-familyCandidate"] = candidate
            record["x-spriteStem"] = stem
        # Orientation, per the write-back contract §2/§8.1. The index is already encoded in the
        # sub-sprite's name — `SpriteNameFor(buildKey, facing) => f"{stem}_d{facing}"` — but §8.1
        # asks for a field, because "a parsed suffix is a convention two programs have to keep
        # agreeing about, and a field is not". The COUNT is only ever read from a declaration:
        # §2 is explicit that the importer reads it and never assumes one, so an entity whose
        # sheet declares nothing carries no count rather than a plausible 8.
        # Real opts, where the ledger records them (#629). A lookup, never a derivation.
        recipe, recipe_problem = recipes.read(repo, sheet)
        if recipe is not None:
            index = recipes.cell_index(recipe, (entry or {}).get("rect"))
            real_call, direction = recipes.call_for(recipe, index)
            if real_call:
                record["call"] = real_call | {
                    "x-fromRecipe": recipe["x-recipePath"],
                    "x-dir": direction,
                    "x-cellIndex": index,
                    "x-argsNote": "args are the recipe's own, verbatim. A leading literal names "
                                  "the piece for kits that take one; `$dir` and `$opts` are the "
                                  "ledger's placeholders for the direction and this dict.",
                }
                record["x-rigSha256"] = (recipe.get("rig") or {}).get("sha256") or \
                    record.get("x-rigSha256")
        elif recipe_problem:
            recipe_refusals.append(recipe_problem)

        if sheet:
            count, evidence = repo.facings_for_sheet(sheet)
            if count is not None:
                record["x-facings"] = count
                record["x-facingsSource"] = evidence
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

    _link_interiors(entities)

    notes = {
        "inactiveInHierarchy": inactive,
        "interiorsLinked": sum(1 for e in entities if e.get("x-interiorOf")),
        "offGrid": off_grid,
        "unresolvedRigPlacements": sum(v["placements"] for v in unresolved_sheets.values()),
        "unresolvedSheets": dict(sorted(unresolved_sheets.items())),
        "x-unresolvedMeaning": "no sidecar beside these sheets names a rig this exporter will "
                               "trust — an ambiguous one names several. The kits that ship a "
                               "*.contract.json resolve exactly; the rest have no committed link "
                               "from sheet to rig, so nothing is pinned rather than guessed.",
        "unlistedFamilies": dict(sorted(unlisted_families.items())),
        "optsFromRecipe": sum(1 for e in entities if (e.get("call") or {}).get("x-fromRecipe")),
        "recipeRefusals": sorted(set(recipe_refusals)),
        "x-recipeMeaning": "an entity whose sheet carries a <stem>.recipe.json (#629) ships the "
                           "real option axes that drew its cell. The rest keep the empty-opts "
                           "form and its note. A refusal here is a recipe that exists but must "
                           "not be trusted — a schema this reader does not know, or a sheet hash "
                           "that says the recipe was written for a different bake.",
        "x-familyVocabulary": "`family` is the editor's own wire vocabulary, transcribed at "
                              "docs/tools/reference/family-names.json and matched EXACTLY: a rig "
                              "whose name does not land on a listed one keeps the sprite stem, "
                              "flags x-familyIsSpriteStem, and appears under unlistedFamilies "
                              "with the candidate name — never aliased onto a near neighbour. "
                              "wharfIsoRig normalises to `wharf` and the list holds both "
                              "`wharfbuilding` and `wharfmodule`; guessing between them is the "
                              "aliasing the ruling forbids. Those entries are the request list "
                              "for the editor side.",
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
    return entities, ordered_rigs, notes, _rig_versions(entities, rigs)


def _link_interiors(entities):
    """Point each interior entity at the building that contains it, by HIERARCHY not geometry.

    The editor was drawing interior props on roofs because nothing said which building they belong
    to. The builders already answer it: an interior stands at ``IslandVillage/school/Interior`` and
    its furniture at ``IslandVillage/school/Furniture/...``, so the containing building is the
    nearest ancestor path that is itself an exported entity. That is a declaration in the scene,
    not a point-in-footprint test — a guess would put a prop in the wrong house at a shared wall.
    """
    by_path = {}
    for entity in entities:
        by_path.setdefault(entity.get("x-path"), entity)
    for entity in entities:
        family = entity.get("family") or ""
        candidate = entity.get("x-familyCandidate") or ""
        if not (family.startswith("interior") or candidate.startswith("interior")):
            continue
        parts = (entity.get("x-path") or "").split("/")
        for cut in range(len(parts) - 1, 0, -1):
            ancestor = by_path.get("/".join(parts[:cut]))
            if ancestor is not None and ancestor is not entity:
                entity["x-interiorOf"] = ancestor["id"]
                break
        else:
            # Said rather than dropped: an interior with no exported container is a fact about
            # the scene, and the editor still needs to know not to trust a roof for it.
            entity["x-interiorOf"] = None


def _rig_versions(entities, rigs):
    """``family -> sha256(rig source bytes)``, for the families this scene actually uses.

    The editor hashes its own copy of each rig and badges a mismatch pink rather than refusing to
    draw. One entry per family is what it asks for; a family that resolved through more than one
    rig would make that table a lie, so such a family is reported with its rigs named instead.
    """
    seen = {}
    for entity in entities:
        family, source = entity.get("family"), entity.get("rigSource")
        if not family or not source:
            continue
        seen.setdefault(family, set()).add(source)
    versions = {}
    for family in sorted(seen):
        sources = sorted(seen[family])
        if len(sources) == 1:
            versions[family] = {
                "rigSource": sources[0],
                "sha256": rigs[sources[0]]["sha256"],
            }
        else:
            versions[family] = {
                "rigSource": None,
                "sha256": None,
                "x-ambiguous": [
                    {"rigSource": source, "sha256": rigs[source]["sha256"]} for source in sources
                ],
                "x-note": "this family resolved through more than one rig in this scene, so no "
                          "single hash describes it. Badge per entity from x-rigSha256 instead.",
            }
    return {
        "x-shaRule": "sha256 of the rig's bytes with CR stripped (tr -d '\\r' | sha256sum) \u2014 "
                     "the repo's own convention, the same one docs/art/rigs sidecars publish.",
        "families": versions,
    }


def _stable_id(hierarchy, x, y):
    """An id minted from the row's own identity — content only, no vocabulary in it.

    The editor matches its write-back rows to ours by ``id``, so the id must not move for any
    reason except the content moving. Two things it therefore is not:

    * **Not positional.** An ordinal renumbers when an unrelated entity is inserted ahead of it,
      silently re-pointing every edit after the insertion (the #571 review's §8.3 warning).
    * **Not family-prefixed.** An earlier draft read ``{family}_{hash}``, which matched the
      reference package's ``character_001`` shape but re-keyed whenever a FAMILY VOCABULARY
      ruling renamed a family — 30 rows when the editor published ``interior``/``interiorprop``,
      24 more waiting on ``wharf``. Ruled out on 2026-08-20: stability is the whole point of the
      field, and the entity already carries ``family`` as its own field.

    What remains is the builder's own row identity: it names each object and computes where the
    object stands. Measured on both regions — path alone repeats (94 objects are called
    ``ShorePlants/Eelgrass``), path + position does not collide once.

    Twelve hex characters, not ten. The width only ever costs a re-key to widen, and this ruling
    is the one moment re-keying is free; 48 bits keeps a far larger scene than either of these
    clear of the birthday bound instead of leaving a forced re-key waiting in a future region.
    """
    key = f"{hierarchy}|{x:.3f}|{y:.3f}"
    return hashlib.sha256(key.encode("utf-8")).hexdigest()[:12]


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
        "x-optsNote": "empty on purpose: no <stem>.recipe.json sits beside this sheet, so the "
                      "option axes that drew this cell are not recorded FOR IT, and the rigs "
                      "resolve an unknown key as a silent fallback rather than an error. "
                      "Defaults draw something true; a guess draws something confidently wrong. "
                      "The recipe ledger (#629) covers six kits and names its own gaps; an "
                      "entity whose sheet is covered carries the recipe's own call and an "
                      "x-fromRecipe instead of this note.",
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


_FACING_SUFFIX = re.compile(r"_d(\d+)(?:_|$)")


def _facing_index(sprite_name):
    """The baked facing step in a sub-sprite's name, or ``None`` when it declares no direction.

    The kits slice as ``{stem}_d{facing}`` (`VillageBuildingKit.SpriteNameFor`), and characters
    add a frame — ``Cutter_idle_d4_f0`` — so the direction is the `_d<n>` group specifically and
    not merely the last number in the name. Absence is meaningful and is left as absence: the
    legacy single sprites end `_0` with no `_d` at all, and foliage rigs publish variants and
    seasons rather than directions. Neither gets a fabricated zero.
    """
    if not sprite_name:
        return None
    match = _FACING_SUFFIX.search(sprite_name)
    return int(match.group(1)) if match else None


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

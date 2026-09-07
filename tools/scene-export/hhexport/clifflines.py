"""The cliffs — 165 placed walls the package said it shipped and did not.

⚠ **This corrects a shipped falsehood, not a gap.** Until now every package said, in three places,
that the region's cliffs were somewhere to be found: the cliff terrain layer was empty, `cliffLines`
was `[]` under a note calling cliff lines *"an authoring artefact of the editor"*, and the layer's
`x-derived` read *"not painted; the repo's cliffs are placed surfaces and ship as entities"*. They do
not ship as entities. `CliffWallSurface` carries no ``SpriteRenderer``, the entity walk is a walk of
sprites, and **zero** of the 86 chunks at Nine Mile Creek or the 79 at St Peters ever reached a
package. A reader who trusted any of those three statements concluded the region has no cliffs — and
was pointed at an empty place to look. That is worse than the door being missing: the door was
silently absent, and this was claimed present.

They belong in ``cliffLines`` — **the format's own key**, not a new ``x-`` list. The reference
package's entry is ``{id, layer, material, landSide, corners, closed, curve, nodes, tiles}``, and
``CliffWallSurface._browPlan`` is a ``nodes`` polyline. Everything the format has no room for
travels beside it under ``x-``.

Three things the export says out loud rather than smoothing over:

  * **These are CHUNKS, not runs.** The builder pushes one surface per stretch, so a coastline
    arrives as 86 short lines and not one long one. Joining them would be a derivation this module
    invents, and the count is the guard: as many ``cliffLines`` out as there are
    ``CliffWallSurface`` in the scene, so a claimed-present falsehood cannot recur.
  * **``nodes`` are SAMPLED STATIONS, not hand-placed nodes** — *"one per station along the shore"*,
    measured at a median and maximum spacing of 0.250 m in both regions. The reference's cliff lines
    carry ``nodes`` alone (its paths carry a derived ``polyline`` too), so a reader may spline them;
    at a quarter-metre a catmull-rom through them is the same line, because it interpolates its
    control points. Decimating to "real" control points was considered and refused: a curve fit is a
    second definition of the coastline, which is the review's §7(c) trap.
  * **No ``tiles`` count.** The reference carries one, but this exporter paints no cliff layer at
    all, so a ``0`` would read as a fact about the line rather than about the export.
"""

import os

CLIFF_WALL_SURFACE = "CliffWallSurface.cs"


def collect(repo, scene, centre, mint_id):
    """``(lines, note)`` for one scene, in the scene's own walk order.

    ``mint_id`` is ``package._stable_id`` — the same minting entities use, so a cliff line's id is
    stable across runs and derived from what identifies it (its path and where it stands) rather
    than from its position in a list.
    """
    from . import unityyaml as U

    lines = []
    for game_object in scene.walk():
        component = None
        for candidate in scene.components_of(game_object):
            if candidate.type_name != "MonoBehaviour":
                continue
            guid = U.ref_guid(candidate.data.get("m_Script"))
            path = repo.path_for_guid(guid) if guid else None
            if path and os.path.basename(path) == CLIFF_WALL_SURFACE:
                component = candidate
                break
        if component is None:
            continue
        lines.append(_line(repo, scene, game_object, component, centre, mint_id))
    return lines


def _line(repo, scene, game_object, component, centre, mint_id):
    from . import unityyaml as U

    x, y = scene.world_of_game_object(game_object)[:2]
    hierarchy = scene.hierarchy_path(game_object)
    brow = _plan(component.data.get("_browPlan"), centre)
    toe = _plan(component.data.get("_toePlan"), centre)
    material_guid = U.ref_guid(component.data.get("_material"))
    material_rel = repo.path_for_guid(material_guid) if material_guid else None

    record = {
        # Key order follows the reference package's own cliffLines entry.
        "id": mint_id(hierarchy, x - centre[0], y - centre[1]),
        "layer": "cliff",
        "material": _stem(material_rel),
        # `auto` is the reference's own value and the honest one: which side the land is on is
        # decided by the wall's azimuth, which travels under x- rather than being re-expressed as a
        # handedness this module would have to define.
        "landSide": "auto",
        "corners": "round",
        "closed": False,
        "curve": {
            "kind": "catmull-rom",
            "uniform": True,
            "tangentScale": 0.5,
            "x-note": "the reference's own curve. ⚠ These nodes are SAMPLED STATIONS at ~0.25 m, "
                      "not hand-placed control points, so a spline through them is the same line "
                      "— a catmull-rom interpolates its points. See x-nodesAre.",
        },
        "nodes": brow,
        "x-nodesAre": "CliffWallSurface._browPlan — the clifftop lip, 'one per station along the "
                      "shore', region-relative like every other position here. Median AND maximum "
                      "spacing 0.250 m in both regions. NOT decimated to control points: a curve "
                      "fit would be a second definition of the coastline.",
        "x-name": scene.name_of(game_object),
        "x-path": hierarchy,
        "x-active": scene.active_in_hierarchy(game_object),
        "x-materialAsset": material_rel,
        "x-toePlan": toe,
        "x-toePlanNote": "the foot of the face IN PLAN — where the plunge stops falling, NOT where "
                         "the toe is drawn; the drop below is projected on top of it.",
        "x-dropMetres": _floats(component.data.get("_dropMetres")),
        "x-toeElevations": _floats(component.data.get("_toeElevations")),
        "x-elevationsNote": "per station, paired with nodes. dropMetres is brow elevation minus toe "
                            "elevation — a DIFFERENCE, which cannot say where the sea meets the "
                            "rock; toeElevations is the absolute height above chart datum the drop "
                            "falls to, and is what a waterline is measured against.",
        "x-wallAzimuthDegrees": _num(U.as_float(component.data.get("_wallAzimuth"), None)),
        "x-batterDegrees": _num(U.as_float(component.data.get("_batter"), None)),
        "x-stations": len(brow),
    }
    return record


def _plan(value, centre):
    """A ``Vector2[]`` of world points as region-relative pairs — the frame every other position in
    the package is in, so a cliff and a wharf plot in one picture."""
    from . import unityyaml as U
    if not isinstance(value, list):
        return []
    out = []
    for item in value:
        if not isinstance(item, dict):
            continue
        px, py = U.vec(item, "x", "y")
        out.append([_num(px - centre[0]), _num(py - centre[1])])
    return out


def _floats(value):
    from . import unityyaml as U
    if not isinstance(value, list):
        return []
    return [_num(U.as_float(item, None)) for item in value]


def _stem(rel):
    """A material's NAME, which is what the reference's `material` holds (`"sandstone"`). The asset
    path travels beside it, so nothing here has to invent a rock type nobody declared."""
    if not rel:
        return None
    return os.path.splitext(os.path.basename(rel))[0]


def _num(value):
    if value is None:
        return None
    rounded = round(float(value), 6)
    if rounded == int(rounded):
        return int(rounded)
    return rounded

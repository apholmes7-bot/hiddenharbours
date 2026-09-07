"""The doors between regions — placed, declared, and until now invisible in the picture.

The scene editor draws entities, and an entity here is a **sprite**: the walk that builds them
starts at a ``SpriteRenderer`` and skips any object without one. That is right for a picture, and
it silently drops a whole class of thing the region genuinely has. The 2026-09-07 east door made it
plain — ``PassageToEastWater`` stands at (356, 40) carrying a ``RegionPassage`` and a trigger box,
``StPetersEastWaterArrival`` at (316, 40) carries a bare ``Transform``, and **neither reached the
package at all**, so the wall the game opens through was missing from the owner's view of his own
island. It is the same shape as the moored fleet under ``x-tidalHulls``: declared, placed, drawn by
something else at runtime or not drawn at all.

So this module exports what the scene DECLARES about crossing:

  * **``x-passages``** — every ``RegionPassage``: where it stands, the trigger band it listens on,
    the region it leads to (id *and* scene name, resolved through its ``RegionDef``), and the
    arrival key it asks for on the far side.
  * **``x-arrivals``** — every ``RegionAnchor``: the region's default arrival point, dock zone and
    disembark point, plus each named arrival with its key and the world point it resolves to. This
    is the other half of a door, and a door needs both halves to be legible.

⚠ **A passage's ``arrivalKey`` is resolved in the TARGET region, not this one.** St Peters'
``PassageToEastWater`` asks for ``st_peters``, which is a key on *East Water's* anchor — a region
this exporter does not ship. So the key travels as a string and is **not** validated here: the
lookup crosses a package boundary, and a reader holding both packages can do it while this one
cannot. Claiming otherwise would be the second definition problem in miniature. Every entry says
whether its target is a region this export covers.

Nothing here is derived, guessed or measured — each value is a field on a placed component, or the
world position of the transform a field points at.
"""

import os

REGION_PASSAGE = "RegionPassage.cs"
REGION_ANCHOR = "RegionAnchor.cs"


def collect(repo, scene, centre, exported_region_ids):
    """``(passages, arrivals)`` for one scene, in the scene's own walk order.

    ``exported_region_ids`` is the set of region ids this export ships, so a passage can say
    whether the reader will be holding the package it points at.
    """
    from . import unityyaml as U

    passages, arrivals = [], []
    for game_object in scene.walk():
        found = {}
        for component in scene.components_of(game_object):
            if component.type_name == "MonoBehaviour":
                guid = U.ref_guid(component.data.get("m_Script"))
                path = repo.path_for_guid(guid) if guid else None
                if path:
                    found[os.path.basename(path)] = component
            else:
                found.setdefault(component.type_name, component)

        if REGION_PASSAGE in found:
            passages.append(_passage(
                repo, scene, game_object, found, centre, exported_region_ids))
        if REGION_ANCHOR in found:
            arrivals.append(_anchor(scene, game_object, found[REGION_ANCHOR], centre))
    return passages, arrivals


def _passage(repo, scene, game_object, found, centre, exported_region_ids):
    from . import unityyaml as U

    component = found[REGION_PASSAGE]
    x, y = scene.world_of_game_object(game_object)[:2]
    target_guid = U.ref_guid(component.data.get("_target"))
    target_rel = repo.path_for_guid(target_guid) if target_guid else None
    target = _region_def(repo, target_rel)

    # An EMPTY arrival key is not a missing one: RegionPassage's own tooltip says the empty string
    # means the target region's DEFAULT arrival point, which every RegionAnchor has. Shipping ""
    # as null would turn "land where the region says" into "nobody said".
    key = component.data.get("_arrivalKey")
    key = "" if key is None else str(key)

    record = {
        "x-name": scene.name_of(game_object),
        "x-path": scene.hierarchy_path(game_object),
        "pos": [_num(x - centre[0]), _num(y - centre[1])],
        "band": _band(found.get("BoxCollider2D")),
        "target": {
            "regionId": target.get("id") if target else None,
            "sceneName": target.get("sceneName") if target else None,
            "asset": target_rel,
            # Whether the reader will be holding the package on the other side of this door. False
            # is a fact about the export, not about the region: East Water and Coddle Cove are
            # real places with real RegionDefs that this export does not picture.
            "exportedHere": bool(target and target.get("id") in exported_region_ids),
        },
        "arrivalKey": key,
        "x-arrivalKeyMeaning": (
            "the key of the arrival point this passage lands at IN THE TARGET REGION. Empty means "
            "that region's default arrival point (RegionAnchor's own, not a named one). ⚠ Resolved "
            "against the TARGET's x-arrivals, not this package's — the lookup crosses a package "
            "boundary and is left to a reader holding both."),
        "x-active": scene.active_in_hierarchy(game_object),
        "x-reentryCooldownSeconds": _num(
            U.as_float(component.data.get("_reentryCooldownSeconds"), None)),
    }
    if target is None:
        record["target"]["x-unresolved"] = (
            "RegionPassage._target names no RegionDef this checkout can read, so where this "
            "passage leads is not stated. Left null rather than guessed from the object's name.")
    return record


def _band(box):
    """The trigger the passage listens on — a ``BoxCollider2D``, in metres, centred on ``pos``.

    ``None`` where the object carries no box: the passage still exists and still says where it
    leads, and inventing a band would draw a door the size of nothing in particular.
    """
    if box is None:
        return None
    from . import unityyaml as U
    size = U.vec(box.data.get("m_Size"), "x", "y")
    offset = U.vec(box.data.get("m_Offset"), "x", "y")
    return {
        "widthMeters": _num(size[0]),
        "heightMeters": _num(size[1]),
        "offset": [_num(offset[0]), _num(offset[1])],
        "isTrigger": U.as_int(box.data.get("m_IsTrigger"), 0) != 0,
        "x-note": "the BoxCollider2D the crossing fires on, in metres, centred on pos + offset. "
                  "1 world unit = 1 m in both axes (ADR 0042).",
    }


def _anchor(scene, game_object, component, centre):
    """A ``RegionAnchor``: the region's own ways in, default and named."""
    from . import unityyaml as U

    def point(field_value):
        """The world position of the transform a field points at, or ``None`` when it points at
        nothing. A null here is authored: RegionAnchor's tooltips say an empty arrival or
        disembark point falls back to the region's default, so it is a real state and not a gap.

        ⚠ **The membership test is not paranoia.** ``Scene.world_of`` answers an unknown transform
        id with the identity — ``(0, 0, 0, 0, 1, 1)`` — which is a perfectly plausible position at
        the region's own centre, and an arrival silently placed there is the kind of wrong nobody
        questions. A reference this scene cannot resolve (a deleted object, a transform in another
        scene) has to come back absent instead.
        """
        file_id = U.ref(field_value)
        if not file_id or file_id == "0":
            return None
        if file_id not in scene.transforms:
            return None
        world = scene.world_of(file_id)
        return [_num(world[0] - centre[0]), _num(world[1] - centre[1])]

    named = []
    for arrival in (component.data.get("_arrivals") or []):
        if not isinstance(arrival, dict):
            continue
        named.append({
            "key": arrival.get("Key"),
            "arrivalPoint": point(arrival.get("ArrivalPoint")),
            "disembarkPoint": point(arrival.get("DisembarkPoint")),
        })

    return {
        "x-name": scene.name_of(game_object),
        "x-path": scene.hierarchy_path(game_object),
        "regionId": component.data.get("_regionId"),
        "arrivalPoint": point(component.data.get("_arrivalPoint")),
        "dockZone": point(component.data.get("_dockZone")),
        "disembarkPoint": point(component.data.get("_disembarkPoint")),
        "namedArrivals": named,
        "x-active": scene.active_in_hierarchy(game_object),
        "x-note": "where the game PUTS you when you arrive. arrivalPoint is the boat, "
                  "disembarkPoint the walker, dockZone where the control switcher re-points. The "
                  "top three are this region's default way in; namedArrivals are the others, each "
                  "keyed by the name a RegionPassage in ANOTHER region asks for. A null point is "
                  "authored, not missing: RegionAnchor falls back to the region's default.",
    }


def _region_def(repo, rel):
    """``{id, sceneName}`` from a ``RegionDef`` asset — the two facts a door needs to name a place."""
    if not rel or not repo.exists(rel):
        return None
    from . import unityyaml as U
    docs = U.parse_file(repo.abs(rel))
    data = docs[0].data if docs else None
    if not data:
        return None
    return {"id": data.get("Id"), "sceneName": data.get("SceneName")}


def _num(value):
    """Match ``package._num``'s shape, so a passage's position reads like an entity's."""
    if value is None:
        return None
    rounded = round(float(value), 6)
    if rounded == int(rounded):
        return int(rounded)
    return rounded

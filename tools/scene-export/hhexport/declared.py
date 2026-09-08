"""Placed world content the sprite walk cannot see — the general case, after four specific ones.

The entity list is a walk of ``SpriteRenderer``s. Four times now that has silently dropped
something a region genuinely has: the moored fleet and the harbour float (``x-tidalHulls``), the
doors between regions (``x-passages`` / ``x-arrivals``), the cliffs (``cliffLines`` — and those were
CLAIMED present), and #774's three parked machines, which arrived with 312 lines of scene and zero
entities. Four in three days is a pattern, not a coincidence, so this is the general list.

**The rule, in one sentence: an object earns a place here when it is PLACED WORLD CONTENT — when
its position means something in the harbour.** Cleats, boundaries, fuel pumps, parked vehicles,
standable platforms, ladders, the shipwright, the starting gear, a window's light, a chimney's
smoke: all things that are somewhere. Cameras, ``GameRoot``, the dev toys, routine lanes and
stations, persistent proxies, scene loaders and terrain drivers: all things whose transform is an
implementation detail, and which would be noise a reader has to filter.

⚠ **The rule is expressed as an ALLOW-LIST, and everything else is REPORTED rather than dropped.**
A deny-list would silently admit each new behaviour somebody adds; an allow-list silently excludes
them, which is the same failure facing the other way. So the notes name every component set that
reached neither list, and a component that is *ruled out* is counted separately from one that is
*not yet ruled on* — the distinction ``resolutionExcluded`` and ``unresolvedSheets`` already draw
elsewhere in this exporter, and for the same reason: reporting a defect where there is a decision
is its own kind of wrong.
"""

import os

# PLACED WORLD CONTENT — its position means something in the harbour.
ALLOW = {
    "ShoreCleat": "a mooring cleat on the quay",
    "FloatCleat": "a mooring cleat on the float",
    "PropertyBoundary": "a boundary somebody owns",
    "FuelPump": "a pump you can fuel at",
    "ParkedVehicle": "a machine standing where somebody left it",
    "VehicleController": "a driveable machine",
    "VehicleDoor": "a machine's door",
    "StandablePlatform": "a surface the on-foot sim stands on",
    "GangwayPlatform": "the brow between the wall and the float",
    "WharfLadder": "a ladder down the face",
    "Shipwright": "a vendor standing somewhere",
    "StartingBait": "starting gear, placed",
    "StartingPots": "starting gear, placed",
    "PreconfiguredLight": "a light with a position",
    "ChimneySmoke": "smoke from a particular chimney",
    "PlacedTrapService": "the service that owns placed traps",
}

# NOT placed world content: the transform is an implementation detail.
RULED_OUT = {
    "GameRoot", "CameraFollow", "CameraZoomInput", "FightStrainCamera", "ControlSwitcher",
    "WorldInteractor", "DialoguePresenter", "OnboardingDirector", "RoutineLanes", "RoutineStations",
    "PersistentObject", "PersistentHoldProxy", "PersistentWalletProxy", "RegionSceneLoader",
    "RegionTravelCoordinator", "RegionDisplayNameRegistrar", "MainlandTidalTerrain", "TidalTerrain",
    "TerrainSplatSurface", "GrassField", "DevToast", "DevFastTide", "DevRegionBootstrap",
    "ArrivalOpening", "ArrivalLineSpeaker",
}

# Already carried by a key of their own, with fields this general list has no room for — a draught
# and a bed, a band and a target, a brow polyline. Listed so they are not counted as unruled.
EXPORTED_ELSEWHERE = {
    "MooredBoat": "x-tidalHulls",
    "FloatingPlatform": "x-tidalHulls",
    "BoatController": "x-tidalHulls",
    "RegionPassage": "x-passages",
    "RegionAnchor": "x-arrivals",
    "CliffWallSurface": "cliffLines",
}

UNNAMEABLE = "?unresolved"


def collect(repo, scene, centre):
    """``(objects, notes)`` — the placed world content, and an account of everything left out."""
    from . import unityyaml as U

    objects = []
    by_component, ruled_out, not_yet, elsewhere = {}, {}, {}, {}
    unnameable = 0
    for game_object in scene.walk():
        components, has_sprite = {}, False
        for component in scene.components_of(game_object):
            if component.type_name == "SpriteRenderer":
                has_sprite = True
            elif component.type_name == "MonoBehaviour":
                guid = U.ref_guid(component.data.get("m_Script"))
                path = repo.path_for_guid(guid) if guid else None
                components[os.path.basename(path)[:-3] if path else UNNAMEABLE] = component
        if has_sprite or not components:
            continue

        names = set(components)
        if names & set(EXPORTED_ELSEWHERE):
            _bump(elsewhere, _key(names))
            continue
        allowed = names & set(ALLOW)
        if allowed:
            # Allow-list membership wins over a ruled-out sibling: a cleat that also carries a
            # persistence marker is still a cleat standing somewhere.
            objects.append(_object(repo, scene, game_object, components, centre))
            for name in sorted(allowed):
                _bump(by_component, name)
            continue
        if names == {UNNAMEABLE}:
            # Every script on it is one this checkout cannot name — a package script, usually.
            # Not "ruled out" and not "awaiting a ruling": unclassifiable, and said so.
            unnameable += 1
            continue
        if names & RULED_OUT:
            _bump(ruled_out, _key(names))
            continue
        _bump(not_yet, _key(names))

    notes = {
        "objects": len(objects),
        "byComponent": dict(sorted(by_component.items())),
        "ruledOut": dict(sorted(ruled_out.items())),
        "notYetRuledOn": dict(sorted(not_yet.items())),
        "exportedElsewhere": dict(sorted(elsewhere.items())),
        "unnameableScripts": unnameable,
        "x-rule": "an object earns a place in x-declaredObjects when it is PLACED WORLD CONTENT — "
                  "when its position means something in the harbour. It must also carry no "
                  "SpriteRenderer, or it is an entity already.",
        "x-note": "⚠ `notYetRuledOn` is the one to read: a component set that reached neither the "
                  "allow-list nor the ruled-out list is awaiting a decision, not silently "
                  "excluded. `ruledOut` is a DECISION (the transform is an implementation "
                  "detail) and reporting it as a gap would be reporting a defect where there is "
                  "none. `exportedElsewhere` carries its own key — x-tidalHulls, x-passages, "
                  "x-arrivals, cliffLines — with fields this general list has no room for. "
                  "`unnameableScripts` counts objects whose every behaviour is a script outside "
                  "Assets/ (Unity's own camera data, chiefly), which cannot be classified at all.",
    }
    return objects, notes


def _object(repo, scene, game_object, components, centre):
    from . import unityyaml as U

    x, y = scene.world_of_game_object(game_object)[:2]
    return {
        "x-name": scene.name_of(game_object),
        "x-path": scene.hierarchy_path(game_object),
        "pos": [_num(x - centre[0]), _num(y - centre[1])],
        "components": sorted(components),
        "defs": _defs(repo, components),
        "x-active": scene.active_in_hierarchy(game_object),
    }


def _defs(repo, components):
    """Every committed ``.asset`` a placed behaviour points at, with its declared ``Id``.

    Which def a thing references is most of what it IS — ``vehicle.dually_3500`` says more about a
    parked machine than its position does. Read generically off the serialized fields rather than
    from a table of known component shapes, so a new behaviour's references arrive without anyone
    teaching this module about it. An asset with no ``Id`` field reports its path alone; nothing is
    invented from the filename.
    """
    from . import unityyaml as U

    out = []
    for component_name in sorted(components):
        component = components[component_name]
        for field, value in component.data.items():
            if field.startswith("m_") or not isinstance(value, dict):
                continue
            guid = U.ref_guid(value)
            if not guid:
                continue
            rel = repo.path_for_guid(guid)
            if not rel or not rel.endswith(".asset") or not repo.exists(rel):
                continue
            docs = U.parse_file(repo.abs(rel))
            data = docs[0].data if docs else {}
            out.append({
                "field": f"{component_name}.{field}",
                "id": data.get("Id"),
                "asset": rel,
            })
    return out


def _key(names):
    return " + ".join(sorted(names))


def _bump(table, key):
    table[key] = table.get(key, 0) + 1


def _num(value):
    rounded = round(float(value), 6)
    return int(rounded) if rounded == int(rounded) else rounded

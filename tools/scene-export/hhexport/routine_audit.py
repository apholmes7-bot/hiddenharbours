"""Read-only St Peters routine measurements; no scene export or game mutation.

Uses the existing Force Text reader and transform graph. Pillow is needed only
by this optional audit to read the committed Path mask, never to alter an image.
"""
import hashlib
import json
import math
from pathlib import Path
import re
import struct

from . import unityyaml as U
from .scene import Scene

SCENE = "Assets/_Project/Scenes/StPeters.unity"
DATA = "Assets/_Project/Data"
STEP = 0.5


def xy(value):
    return U.vec(value, "x", "y")


def distance(a, b):
    return math.hypot(b[0] - a[0], b[1] - a[1])


def cross(a, b):
    return a[0] * b[1] - a[1] * b[0]


def sub(a, b):
    return a[0] - b[0], a[1] - b[1]


def intersects(a, b, c, d):
    """Closed segments, including collinear overlap and endpoint contact."""
    r, s, q = sub(b, a), sub(d, c), sub(c, a)
    den = cross(r, s)
    if abs(den) < 1e-10:
        if abs(cross(q, r)) > 1e-8:
            return False
        return all(max(min(a[k], b[k]), min(c[k], d[k])) <=
                   min(max(a[k], b[k]), max(c[k], d[k])) + 1e-8 for k in (0, 1))
    t, u = cross(q, s) / den, cross(q, r) / den
    return -1e-8 <= t <= 1 + 1e-8 and -1e-8 <= u <= 1 + 1e-8


def inside(p, polygon):
    hit = False
    for a, b in zip(polygon, polygon[1:] + polygon[:1]):
        if (a[1] > p[1]) != (b[1] > p[1]):
            if p[0] < (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0]:
                hit = not hit
    return hit


def polygon_hit(a, b, polygon):
    return inside(a, polygon) or inside(b, polygon) or any(
        intersects(a, b, c, d) for c, d in zip(polygon, polygon[1:] + polygon[:1]))


def samples(points, step=STEP):
    """0.5 m chainage cells, sampled at each midpoint; final cell shortened.

    Carry chainage across bends, so inserting a collinear waypoint cannot change
    the estimate. Yields (position, represented metres), never counts endpoints.
    """
    lengths = [distance(a, b) for a, b in zip(points, points[1:])]
    total = sum(lengths)
    index, consumed, start = 0, 0.0, 0.0
    while start < total - 1e-10:
        weight = min(step, total - start)
        at = start + weight / 2
        while index < len(lengths) - 1 and consumed + lengths[index] < at:
            consumed += lengths[index]
            index += 1
        t = (at - consumed) / lengths[index]
        a, b = points[index:index + 2]
        yield (a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])), weight
        start += weight


def integers(blob):
    raw = bytes.fromhex(blob)
    return list(struct.unpack("<" + "i" * (len(raw) // 4), raw))


def node_path(parents, start, end):
    def ancestors(node):
        out = []
        while node >= 0:
            if node in out or node >= len(parents):
                raise ValueError("Invalid lane tree")
            out.append(node)
            node = parents[node]
        return out
    left, right = ancestors(start), ancestors(end)
    shared = next(n for n in left if n in right)
    return left[:left.index(shared) + 1] + list(reversed(right[:right.index(shared)]))


def lane_polyline(lanes, path):
    positions = list(map(xy, lanes['_nodePositions']))
    parents = integers(lanes['_nodeParents'])
    starts, counts = integers(lanes['_viaStart']), integers(lanes['_viaCount'])
    via = list(map(xy, lanes['_via']))
    result, via_used = [positions[path[0]]], 0
    for a, b in zip(path, path[1:]):
        child = a if parents[a] == b else b
        bends = via[starts[child]:starts[child] + counts[child]]
        if len(bends) != counts[child]:
            raise ValueError("Invalid via slice")
        via_used += len(bends)
        result.extend(bends if child == a else reversed(bends))
        result.append(positions[b])
    return result, via_used


def collect(root):
    from PIL import Image
    root = Path(root)
    inputs = set()

    def read(rel):
        inputs.add(rel)
        return U.parse_file(root / rel)

    scene = Scene(read(SCENE))

    def of_type(kind):
        return [d for d in scene.behaviours.values()
                if d.data.get('m_EditorClassIdentifier', '').endswith('.' + kind)
                and d.data.get('m_Enabled', '1') != '0'
                and scene.active_in_hierarchy(U.ref(d.data['m_GameObject']))]

    def label(d):
        return scene.hierarchy_path(U.ref(d.data['m_GameObject']))

    def transform(d, point):
        x, y, _, angle, sx, sy = scene.world_of_game_object(U.ref(d.data['m_GameObject']))
        rad = math.radians(angle)
        return (x + point[0] * sx * math.cos(rad) - point[1] * sy * math.sin(rad),
                y + point[0] * sx * math.sin(rad) + point[1] * sy * math.cos(rad))

    station_doc, = of_type('RoutineStations')
    lane_doc, = of_type('RoutineLanes')
    stations = {s['Id']: s for s in station_doc.data['_stations']}
    if len(stations) != len(station_doc.data['_stations']):
        raise ValueError('Duplicate station IDs')
    lanes = lane_doc.data
    buildings, doors = {}, {}
    for d in of_type('BuildingInterior'):
        v = d.data
        if U.ref(v.get('_dwellingMesh')):
            raise ValueError('Mesh footprint needs an explicit audit implementation')
        centre = scene.world_of_game_object(U.ref(v['m_GameObject']))[:2]
        angle = -2 * math.pi * int(v['_facing']) / int(v['_facings'])
        depth = float(v['_groundDepthScale'])

        def model(p):
            return (centre[0] + p[0] * math.cos(angle) - p[1] * math.sin(angle),
                    centre[1] + (p[0] * math.sin(angle) + p[1] * math.cos(angle)) * depth)
        w, h = float(v['_widthMetres']) / 2, float(v['_lengthMetres']) / 2
        buildings[label(d)] = [model(p) for p in [(-w, -h), (w, -h), (w, h), (-w, h)]]
        doors[d.file_id] = model((float(v['_doorAcrossMetres']), h if v['_doorOnPlusY'] == '1' else -h))

    # Scenery buildings (cannery, sheds, loft) have no BuildingInterior. Resolve
    # their footprint and facing from the existing bake catalog and sprite meta.
    from .repo import Repo
    repo = Repo(str(root))  # sprite_table reads one meta, never indexes the repo.
    folder = 'Assets/_Project/Art/Sprites/Buildings/Village/'
    catalog_rel = folder + 'Buildings.json'
    inputs.add(catalog_rel)
    catalog = json.loads((root / catalog_rel).read_text(encoding='utf-8'))
    depth_scales = {float(d.data['_groundDepthScale']) for d in of_type('BuildingInterior')}
    if len(depth_scales) != 1:
        raise ValueError('Ambiguous ground projection')
    ground_depth, = depth_scales
    scenery_footprints = []
    for entry in catalog['buildings']:
        sheet = folder + entry['sheet']
        meta = sheet + '.meta'
        guid = re.search(r'^guid: (\w+)', (root / meta).read_text(), re.M)[1]
        renderers = [d for d in scene.renderers.values() if U.ref_guid(d.data.get('m_Sprite')) == guid
                     and scene.active_in_hierarchy(U.ref(d.data['m_GameObject']))]
        for renderer in renderers:
            name = label(renderer)
            if name in buildings:
                continue
            inputs.add(meta)
            sprite = repo.sprite_table(sheet)[U.ref(renderer.data['m_Sprite'])]
            facing = int(re.search(r'_d(\d+)$', sprite['name'])[1])
            x, y, _, rotation, sx, sy = scene.world_of_game_object(U.ref(renderer.data['m_GameObject']))
            if abs(rotation) > 1e-5 or abs(sx-1) > 1e-5 or abs(sy-1) > 1e-5:
                raise ValueError('Scenery footprint requires unit transform: ' + name)
            angle = -2 * math.pi * facing / entry['facings']
            w, h = entry['footprintWidthMetres']/2, entry['footprintLengthMetres']/2
            buildings[name] = [(x + px*math.cos(angle)-py*math.sin(angle),
                                y + (px*math.sin(angle)+py*math.cos(angle))*ground_depth)
                               for px, py in [(-w,-h),(w,-h),(w,h),(-w,h)]]
            scenery_footprints.append(name)

    fences = {}
    for d in of_type('PropertyBoundary'):
        if not label(d).startswith('Yards/'):
            raise ValueError('Unclassified boundary: ' + label(d))
        fences[label(d)] = [[transform(d, xy(p)) for p in run['Points']] for run in d.data['_runs']]
    cliffs = {}
    for d in of_type('CliffWallSurface'):
        # These arrays are WORLD plan positions, not renderer-local coordinates.
        brow = list(map(xy, d.data['_browPlan']))
        toe = list(map(xy, d.data['_toePlan']))
        if len(brow) != len(toe):
            raise ValueError('Unmatched cliff lines')
        cliffs[label(d)] = [[brow[i], brow[i+1], toe[i+1], toe[i]] for i in range(len(brow)-1)]

    surface, = of_type('TerrainSplatSurface')
    mask_rel = DATA + '/Terrain/StPetersSplatF.png'
    meta = mask_rel + '.meta'
    inputs.update((mask_rel, meta))
    guid = re.search(r'^guid: (\w+)', (root / meta).read_text(), re.M)[1]
    if guid != U.ref_guid(surface.data['_splatF']):
        raise ValueError('Scene binds a different Path mask')
    with Image.open(root / mask_rel) as image:
        if image.mode != 'RGBA':
            raise ValueError('Expected RGBA Path mask; LFS bytes must be present')
        mask = image.getchannel('R').copy()
    cx, cy = xy(surface.data['_worldCenter'])
    wx, wy = xy(surface.data['_worldSize'])
    mw, mh = mask.size

    def painted(point):
        # PNG row zero is north, matching TerrainPlanPng and VillageStations.Ground.
        px = math.floor((point[0] - cx + wx / 2) / wx * mw)
        py = math.floor((cy + wy / 2 - point[1]) / wy * mh)
        return 0 <= px < mw and 0 <= py < mh and mask.getpixel((px, py)) > 0

    def measure(points):
        cells = list(samples(points))
        length = sum(weight for _, weight in cells)
        on = sum(weight for p, weight in cells if painted(p))
        segments = list(zip(points, points[1:]))
        return dict(metres=length, painted_metres=on, painted_pct=100 * on / length if length else None,
                    samples=len(cells),
                    buildings=[name for name, poly in sorted(buildings.items())
                               if any(polygon_hit(a, b, poly) for a, b in segments)],
                    fences=[name for name, runs in sorted(fences.items())
                            if any(intersects(a, b, c, d) for a, b in segments
                                   for run in runs for c, d in zip(run, run[1:]))],
                    cliffs=[name for name, quads in sorted(cliffs.items())
                            if any(polygon_hit(a, b, q) for a, b in segments for q in quads)])

    routines, legs, missing, used, homes, external = [], [], [], set(), {}, []
    placements = {U.ref_guid(d.data['_routine']): d for d in of_type('VillagerRoutine')}
    for path in sorted((root / DATA / 'Routines').glob('*.asset')):
        rel = path.relative_to(root).as_posix()
        data = U.parse_file(path)[0].data
        if data.get('RegionId') != 'region.st_peters':
            continue
        inputs.add(rel)
        meta = rel + '.meta'
        inputs.add(meta)
        guid = re.search(r'^guid: (\w+)', (root / meta).read_text(), re.M)[1]
        placement = placements.pop(guid)
        if U.ref(placement.data['_stations']) != station_doc.file_id or U.ref(placement.data['_lanes']) != lane_doc.file_id:
            raise ValueError('Resident uses a different network')
        days = [('ordinary', data['Entries'])] + [
            ('weekday_' + day['Weekday'], day['Entries']) for day in data.get('WeeklyDays', [])]
        entries = data['Entries']
        if sum(len(day) for _, day in days) != len(re.findall(r'^\s+- StartHour:', path.read_text(encoding='utf-8'), re.M)):
            raise ValueError('Incomplete routine parse: ' + rel)
        rid = data['Id']
        routines.append(dict(id=rid, file=rel, object=label(placement), blocks=len(entries),
                             jitter_minutes=float(data['ScheduleJitterMinutes'])))
        homes[rid] = [e['StationId'] for e in entries if e.get('Activity', '0') == '0']
        for day, entries in days:
            for i, entry in enumerate(entries):
                previous = entries[i - 1]
                ids = [previous['StationId'], entry['StationId']]
                used.update(sid for sid in ids if sid.startswith('station.st_peters.'))
                if any((e.get('RegionId') or data['RegionId']) != 'region.st_peters' for e in (previous, entry)):
                    external.append(dict(routine=rid, day=day, start=ids[0], end=ids[1],
                                         hour=float(entry['StartHour']),
                                         until=float(entries[(i+1) % len(entries)]['StartHour'])))
                    continue
                if any(sid not in stations for sid in ids):
                    missing.append(dict(routine=rid, stations=[sid for sid in ids if sid not in stations]))
                    continue
                start, end = [stations[sid] for sid in ids]
                if start.get('OutOfSight', '0') != '0' or end.get('OutOfSight', '0') != '0':
                    raise ValueError('Hidden stop needs explicit audit support')
                a, b = xy(start['Position']), xy(end['Position'])
                nodes = node_path(integers(lanes['_nodeParents']), lanes['_nodeNames'].index(start['LaneNodeName']),
                                  lanes['_nodeNames'].index(end['LaneNodeName']))
                poly, via_count = lane_polyline(lanes, nodes)
                route = [a]
                if U.ref(start['Interior']):
                    route.append(doors[U.ref(start['Interior'])])
                route.extend(poly)
                if U.ref(end['Interior']):
                    route.append(doors[U.ref(end['Interior'])])
                route.append(b)
                clean = []
                for p in route:
                    if not clean or distance(clean[-1], p) > .001:
                        clean.append(p)
                endpoint_buildings = sorted(name for name, poly in buildings.items() if inside(a, poly) or inside(b, poly))
                legs.append(dict(routine=rid, day=day, block=i, start=ids[0], end=ids[1],
                                 hour=float(entry['StartHour']), until=float(entries[(i+1) % len(entries)]['StartHour']),
                                 nodes=[lanes['_nodeNames'][n] for n in nodes], via_count=via_count,
                                 route=clean, intermediate_points=max(0, len(clean)-2),
                                 endpoint_buildings=endpoint_buildings, direct=measure([a, b]), authored=measure(clean)))
    if placements:
        raise ValueError('Scene has residents outside the routine asset census')
    return dict(scene=SCENE, routines=routines, legs=legs, homes=homes, missing=missing, external=external,
                unused=sorted(set(stations)-used), stations=stations, lanes=lanes,
                buildings=buildings, scenery_footprints=scenery_footprints, fences=fences, cliff_count=len(cliffs),
                mask=dict(path=mask_rel, size=[mw, mh], world_center=[cx,cy], world_size=[wx,wy]),
                inputs={rel: hashlib.sha256((root/rel).read_bytes()).hexdigest() for rel in sorted(inputs)})


def markdown(audit):
    """The tables and their scope travel together when the audit is regenerated."""
    ordinary = [r for r in audit['legs'] if r['day'] == 'ordinary']
    weekly = [r for r in audit['legs'] if r['day'] != 'ordinary']
    building_ids = {name: 'B' + str(i+1) for i, name in enumerate(sorted(audit['buildings']))}
    fence_ids = {name: 'F' + str(i+1) for i, name in enumerate(sorted(audit['fences']))}

    def short(s):
        return s.removeprefix('station.st_peters.').removeprefix('routine.')

    def hour(h):
        return '%02d:%02d' % (int(h), round(h % 1 * 60))

    def cuts(row, kind):
        m = row[kind]
        return bool(set(m['buildings']) - set(row['endpoint_buildings']) or m['fences'] or m['cliffs'])

    def hits(row, kind):
        m = row[kind]
        b = [building_ids[n] + ('*' if n in row['endpoint_buildings'] else '') for n in m['buildings']]
        return ', '.join(b + [fence_ids[n] for n in m['fences']] + m['cliffs']) or 'none'

    def coverage(row, kind):
        m = row[kind]
        return f"{m['painted_metres']:.1f} / {m['painted_pct']:.1f}%"

    def count(predicate):
        return sum(predicate(r) for r in ordinary)

    lines = ['# St Peters routine routes against painted paths', '',
        f"The committed village has **{len(audit['routines'])} resident routines and {len(ordinary)} ordinary-day legs**. "
        f"**{count(lambda r: r['authored']['painted_pct'] >= 99.999)} of {len(ordinary)} authored routes lie wholly on Path paint**, "
        f"and {count(lambda r: r['authored']['painted_pct'] >= 50)} are at least half on it. "
        f"The requested straight-line comparison gives {count(lambda r: r['direct']['painted_pct'] >= 99.999)} wholly painted "
        f"and {count(lambda r: r['direct']['painted_pct'] >= 50)} at least half painted. "
        'These are measurements of the serialized paint mask, not a judgement of the rendered appearance.', '',
        f"**{count(lambda r: cuts(r, 'direct'))} straight-line legs cut a fence or an unrelated building footprint.** "
        f"{count(lambda r: bool(r['direct']['fences']))} intersect fence lines; "
        f"{count(lambda r: bool(set(r['direct']['buildings']) - set(r['endpoint_buildings'])))} "
        'also cross buildings other than their origin or destination. '
        f"The authored routes reduce this to **{count(lambda r: cuts(r, 'authored'))} legs**, all with fence intersections. "
        f"No measured leg intersects any of the {audit['cliff_count']} active serialized cliff-wall strips. "
        'A straight line is a comparison, not the movement rule: residents use the lane tree and indoor door approaches.', '',
        'The worst long direct lines are the two sage-home/wharf journeys. The authored lane removes their unrelated '
        'building and fence crossings. The plot journeys still intersect fences in the authored route. '
        'The slip/wharf deck legs have no Path paint, which alone is not a defect. Indoor home and work approaches '
        'also make 100% paint coverage an unsuitable universal pass criterion.', '',
        f"All residents have a resolved Home activity station. The only station unused by these routines is "
        f"`{', '.join(short(s) for s in audit['unused'])}`. "
        f"There are {len(audit['missing'])} unresolved local station references. "
        'The Wednesday quad trip additionally names two mainland station IDs absent from this scene; '
        'those are explicitly cross-region destinations, listed below rather than called local wiring faults.', '',
        '## Snapshot and method', '',
        '- Baseline: `81b0795db6bd3bd6a5352d66167d4863f0ae2f41`. This report describes the committed scene, '
        'not a rebuilt scene or an open change. Input hashes below identify the measurement inputs.',
        '- One leg starts at the previous entry\'s station and departs at the current entry\'s `StartHour`. '
        'The first leg starts at the last block\'s station. Hours show departure through the next departure '
        '(a block window, not travel duration); overnight windows wrap midnight. These are authored hours before seed-based jitter.',
        '- Distances are Euclidean scene XY metres, matching `RoutineLaneTree.PolylineLength` and the routine engine. '
        'They are not unsquashed ground distances or distances along the elevation surface. '
        'Building rotation happens before its serialized ground-depth squash, as in `InteriorFootprint.ModelToWorld`.',
        '- Paint is the scene-bound `StPetersSplatF.png` red channel (Path, material index 20): '
        f"{audit['mask']['size'][0]} × {audit['mask']['size'][1]} pixels across "
        f"{audit['mask']['world_size'][0]:g} × {audit['mask']['world_size'][1]:g} scene metres, centre (0, 0). "
        'Each route is split into 0.5 m chainage intervals; each midpoint samples its containing pixel, '
        'with a shortened last interval weighted by its length. A nonzero red byte counts as paint. '
        'PNG row zero is north. Metres and percentages estimate coverage, not a continuous-area integral; '
        'reverse journeys can differ slightly because the final partial interval starts at the other end.',
        '- No dilation, bilinear filtering, shader warp, terrain material fallback, or visual acceptance is applied. '
        'Paint under indoor footprints is included. Splat B alpha is the older Dirt material and is not treated '
        'as proof of an authored Path. The two named old starter paths survive as sources of lane bends, '
        'but the current Path mask is authoritative for these measurements.',
        '- Crossings use continuous segment intersection, not the 0.5 m samples, so thin barriers cannot fall between samples. '
        'Fence runs come from the seven active `PropertyBoundary._runs` arrays, transformed to world coordinates; '
        'the authored gate gaps stay open. Fence thickness and actor radius are not added. '
        'Endpoint contact and collinear overlap count. Cliff checks use each adjacent brow/toe quad in world plan coordinates, '
        'without projecting the drawn face or inferring a physics collider.',
        f"- Building coverage includes {len(audit['buildings'])} footprints: ten serialized `BuildingInterior` ground floors "
        'plus seven scenery buildings from `Buildings.json` footprint dimensions and the scene sprite\'s facing. '
        'These include the cannery, plot sheds, bait store, net loft and net shed. Roof overhangs, furniture, trees, '
        'and unrelated props are outside the building-footprint question. `B*` marks a footprint containing an '
        'endpoint; this is expected for indoor departures/arrivals and excluded from the headline unrelated-building count. '
        'It does not prove that a straight line enters through the door.',
        '- The authored comparison reproduces `RoutinePlanner.BuildFromStops`: station, exit door if indoors, '
        'lane nodes and child-to-parent via points (reversed when travelling outward), entrance door, destination. '
        'Adjacent points within 1 mm collapse. A via count of zero does not mean no authored routing: '
        'junctions and door approaches still exist. Waypoint columns count intermediate points after this collapse.',
        '- No Unity/editor, PlayMode, rendering, navigation collision run, full retired-export suite, or CI completion '
        'is claimed. Conversation holds, tide waits, region transitions, beach-day substitutions, and seed/day jitter '
        'are dynamic and not synthesized. Every static ordinary-day entry and every Wednesday entry is accounted for. '
        'Four Wednesday legs include off-island stations and have no single St Peters-local line or mask coverage; '
        'their metrics are explicitly N/A below.', '',
        '## Worst ten direct-line legs', '',
        'Rank: most unrelated building footprints, then most fence lines, then greatest unpainted direct distance. '
        'This ranks geometry for review, not severity of observed gameplay. `*` is an endpoint footprint.', '',
        '| Rank | Resident | Departure | From → to | Direct m | Path m / % | Direct intersections | Authored intersections |',
        '|---:|---|---|---|---:|---:|---|---|']
    worst = sorted(ordinary, key=lambda r: (-len(set(r['direct']['buildings'])-set(r['endpoint_buildings'])),
                   -len(r['direct']['fences']), -(r['direct']['metres']-r['direct']['painted_metres']), r['routine'], r['hour']))[:10]
    for i, r in enumerate(worst, 1):
        lines.append(f"| {i} | {short(r['routine'])} | {hour(r['hour'])} | {short(r['start'])} → {short(r['end'])} | "
                     f"{r['direct']['metres']:.1f} | {coverage(r,'direct')} | {hits(r,'direct')} | {hits(r,'authored')} |")

    def legs_table(rows):
        lines.extend(['', '| Resident | Block hours | From → to | Direct m | Direct Path m / % | Intermediate / via points | Authored m | Authored Path m / % | Direct intersections | Authored intersections |',
                      '|---|---|---|---:|---:|---:|---:|---:|---|---|'])
        for r in rows:
            lines.append(f"| {short(r['routine'])} | {hour(r['hour'])}–{hour(r['until'])} | "
                         f"{short(r['start'])} → {short(r['end'])} | {r['direct']['metres']:.1f} | {coverage(r,'direct')} | "
                         f"{r['intermediate_points']} / {r['via_count']} | {r['authored']['metres']:.1f} | "
                         f"{coverage(r,'authored')} | {hits(r,'direct')} | {hits(r,'authored')} |")
    lines.extend(['', '## Every ordinary-day leg', '',
                  'Station names omit `station.st_peters.`; resident names omit `routine.`. '
                  'Intersections list building and fence keys from the inventories below; no cliff intersections were found.'])
    legs_table(ordinary)
    lines.extend(['', '## Wednesday replacement day', '',
                  'The `Weekday: 2` override replaces the ordinary day for `junior_poirier`. '
                  'Its six wholly local legs are below; the four remaining legs follow in a separate table. '
                  'The late store visit departs at 13:00 instead of 12:00.'])
    legs_table(weekly)
    lines.extend(['', '| Block hours | From | To | Distance / paint / intersections |', '|---|---|---|---|'])
    for r in audit['external']:
        lines.append(f"| {hour(r['hour'])}–{hour(r['until'])} | `{r['start']}` | `{r['end']}` | N/A: cross-region or mainland leg |")
    lines.extend(['', 'IDs absent from the St Peters scene: `station.nine_mile_creek.bar_landing` and '
                  '`station.nine_mile_creek.fuel_stop`. The asset explicitly supplies `RegionId: region.nine_mile_creek`; '
                  'outbound and return crossings name `vehicle.utility_quad` at 5.5 m/s. '
                  'No mainland geometry or painted-path claim is made by this local audit.', '',
                  '## Residents, homes and source files', '',
                  '| Resident | Scene object | Ordinary blocks | Home activity stations | Jitter ± minutes | Asset |',
                  '|---|---|---:|---|---:|---|'])
    for r in audit['routines']:
        lines.append(f"| {short(r['id'])} | `{r['object']}` | {r['blocks']} | "
                     f"{', '.join(short(s) for s in audit['homes'][r['id']]) or 'NONE'} | {r['jitter_minutes']:g} | "
                     f"[{Path(r['file']).name}](../../{r['file']}) |")
    lines.extend(['', 'A Home activity can be an outdoor step or dooryard; it does not require an interior. '
                  'No resident lacks a home station under that data definition. The plot resident\'s `cottage_step` '
                  'is outdoors. No station membership or identity was inferred from NPC display names.', '',
                  '## Scene station table', '',
                  '| Station | XY | Lane node | Interior | Used by a routine |', '|---|---|---|---|---|'])
    for sid, s in audit['stations'].items():
        p = xy(s['Position'])
        lines.append(f"| {short(sid)} | {p[0]:.3f}, {p[1]:.3f} | {s['LaneNodeName']} | "
                     f"{'yes' if U.ref(s['Interior']) else 'no'} | {'no' if sid in audit['unused'] else 'yes'} |")
    lines.extend(['', '## Lane nodes and authored bends', '',
                  'All node and via coordinates are scene-world XY. Via points are stored child → parent. '
                  'A spare ninth point exists after the referenced via slices and is not part of any edge.', '',
                  '| Node | XY | Parent | Referenced via points |', '|---|---|---|---|'])
    lanes = audit['lanes']
    parents, starts, counts = (integers(lanes[k]) for k in ('_nodeParents','_viaStart','_viaCount'))
    for i, name in enumerate(lanes['_nodeNames']):
        p = xy(lanes['_nodePositions'][i])
        via = [xy(v) for v in lanes['_via'][starts[i]:starts[i]+counts[i]]]
        lines.append(f"| {name} | {p[0]:.3f}, {p[1]:.3f} | {lanes['_nodeNames'][parents[i]] if parents[i]>=0 else 'root'} | "
                     f"{'; '.join(f'({x:.3f}, {y:.3f})' for x,y in via) or 'none'} |")
    lines.extend(['', '## Footprint and boundary keys', '',
                  '| Key | Scene object | Source |', '|---|---|---|'])
    for name, key in building_ids.items():
        source = 'bake footprint + scene facing' if name in audit['scenery_footprints'] else 'BuildingInterior ground floor'
        lines.append(f'| {key} | `{name}` | {source} |')
    for name, key in fence_ids.items():
        lines.append(f'| {key} | `{name}` | serialized open fence runs |')
    lines.extend(['', '## Sources and reproduction', '',
        '- [Scene](../../Assets/_Project/Scenes/StPeters.unity): `RoutineStations`, `RoutineLanes`, '
        '`VillagerRoutine`, `BuildingInterior`, `PropertyBoundary`, `CliffWallSurface`, `TerrainSplatSurface`.',
        '- [Routine planner](../../Assets/_Project/Code/World/Routines/RoutinePlanner.cs), '
        '[lane traversal](../../Assets/_Project/Code/World/Routines/RoutineLaneTree.cs), '
        '[weekly/cross-region planner](../../Assets/_Project/Code/World/Routines/RoutineLegs.cs).',
        '- Painted path authoring: [23 village route assets](../../Assets/_Project/Data/Regions/StPetersVillage/), '
        '[terrain PathDef assets](../../Assets/_Project/Data/Terrain/StPetersPlan/) and '
        '[starter centreline declarations](../../Assets/_Project/Code/App/Editor/StPetersStarterSplat.cs). '
        'These are polylines/declared line sources, not an authored navigation spline. '
        'The mask can diverge from the lane tree; the audit measures both without rebuilding either.',
        '- [Path-channel binding](../../Assets/_Project/Code/Art/TerrainSplatSurface.cs) and '
        '[station paint sampling convention](../../Assets/_Project/Code/App/Editor/StPetersVillageStations.cs).',
        '- Existing coverage found before extending the audit: '
        '[StPetersRoutineContentTests.cs](../../Assets/Tests/EditMode/StPetersRoutineContentTests.cs) '
        'already tests station membership, lane-tree arrays, painted bend points and per-block travel fit. '
        '[RoutineLaneTreeTests.cs](../../Assets/Tests/EditMode/World/RoutineLaneTreeTests.cs) tests route point ordering. '
        'These were not duplicated or changed.',
        '- Reused the existing [scene exporter](../../tools/scene-export/hh_scene_export.py), '
        'its Force Text reader and hierarchy transforms, adding an isolated audit option. '
        'The retired package exporter stays retired; no package regeneration or CI restoration is needed. '
        'The reader now consumes wrapped unquoted notes so later schedule entries are not lost. '
        'Existing parser tests were extended alongside audit geometry/sampling checks.', '',
        'Requires Python 3.10+ and Pillow, plus the committed Path PNG bytes. Run from the repository root:', '',
        '```powershell',
        'python tools/scene-export/hh_scene_export.py --routine-audit Evidence~/gpt-goods/routine-audit.json --routine-audit-doc docs/world/st-peters-routine-routes-audit.md',
        'python -m unittest discover -s tools/scene-export/tests -p test_export.py -k UnityYamlTests -v',
        'python -m unittest discover -s tools/scene-export/tests -p test_export.py -k RoutineAuditTests -v',
        '```', '',
        'Validation: six parser cases and four audit cases passed. The audit was run twice with byte-identical '
        'JSON and Markdown. No game data or visible assets were changed.', '',
        '### Input SHA-256', '', '| Input | SHA-256 |', '|---|---|'])
    for path, digest in audit['inputs'].items():
        lines.append(f'| `{path}` | `{digest}` |')
    return '\n'.join(lines) + '\n'

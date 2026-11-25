"""Read-only berth survey; reuse the scene exporter's YAML, transforms and PNG decoder.

Run from any directory. Writes the generated Markdown to stdout; no Unity or network.
This is an audit of the pinned scene, not a new runtime terrain implementation.
"""
import argparse
import hashlib
import json
import math
import struct
from pathlib import Path

from hhexport import unityyaml as U
from hhexport.scene import Scene
from hhexport.heightmap import decode_r8

ROOT = Path(__file__).resolve().parents[2]
SCENE = 'Assets/_Project/Scenes/StPeters.unity'
DATA = 'Assets/_Project/Data/'
BASE = '81b0795db6bd3bd6a5352d66167d4863f0ae2f41'
CONTACT_EPS = 1e-6  # Treat decimal equality as contact despite binary round-off.


def new_contact(a, p, draft):
    return a >= draft-CONTACT_EPS and p <= draft+CONTACT_EPS and a-p > CONTACT_EPS


def asset(path):
    return next(d.data for d in U.parse_file(ROOT / path) if d.type_name == 'MonoBehaviour')


def vec(v):
    return float(v['x']), float(v['y'])


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(t):
    t = max(0, min(1, t))
    return t*t*(3-2*t)


def distance(p, a, b):
    dx, dy = b[0]-a[0], b[1]-a[1]
    ll = dx*dx+dy*dy
    t = 0 if ll <= 1e-6 else max(0, min(1, ((p[0]-a[0])*dx+(p[1]-a[1])*dy)/ll))
    return math.hypot(p[0]-a[0]-t*dx, p[1]-a[1]-t*dy)


class Analytic:
    """TidalTerrain.ElevationAtZones + CoastPlan.BlendAt, using serialized fields."""
    def __init__(self, data):
        self.d = data

    def f(self, k):
        return float(self.d['_'+k])

    def v(self, k):
        return vec(self.d['_'+k])

    @staticmethod
    def band(d, flat, falloff, inner, outer):
        if d <= flat:
            return inner
        return outer if falloff <= 0 else lerp(inner, outer, smooth((d-flat)/falloff))

    def profile(self, d, coast):
        f, band = self.f, self.band
        r, high, deep = f('islandRadius'), f('islandElevation'), f('deepHarbourElevation')
        if d <= r:
            return high
        fall = f('islandFalloff')
        if coast in (3, 4, 5):
            low = f('tideMean')-f('tideAmplitude')
            toe = deep if coast == 4 else low-f('cliffToeTideFraction')*f('tideAmplitude')
            if coast == 5:
                toe = low+f('ledgeBenchTideFraction')*f('tideAmplitude')
            plunge = max(.01, f('cliffPlungeWidth'))
            if d <= r+plunge:
                return band(d, r, plunge, high, toe)
            end = r+plunge+(max(0, f('ledgeBenchWidth')) if coast == 5 else 0)
            return band(d, end, max(1, fall), toe, deep)
        shelf = f('reefShelfWidth')
        beach_outer = f('reefShelfInnerElevation') if shelf > 0 else deep
        if d <= r+fall or shelf <= 0:
            return band(d, r, fall, high, beach_outer)
        if d <= r+fall+shelf:
            return band(d, r+fall, shelf, beach_outer, f('reefShelfOuterElevation'))
        return band(d, r+fall+shelf, max(1, fall), f('reefShelfOuterElevation'), deep)

    def island(self, p):
        x, y = p[0]-self.v('islandCenter')[0], p[1]-self.v('islandCenter')[1]
        if self.f('islandRadiusY') > 0 and self.f('islandRadius') > 0:
            y *= self.f('islandRadius')/self.f('islandRadiusY')
        d = math.hypot(x, y)
        b = math.degrees(math.atan2(x, y)) % 360 if d*d >= 1e-12 else 0
        sectors = self.d['_coastSectors']
        if not sectors:
            return self.profile(d, 0)
        i = 0
        for j, sector in enumerate(sectors):
            a, z = float(sector['FromBearing']) % 360, float(sectors[(j+1) % len(sectors)]['FromBearing']) % 360
            if (a <= b < z) if a <= z else (b >= a or b < z):
                i = j
                break
        primary = self.profile(d, int(sectors[i]['Class']))
        feather = self.f('coastBlendDegrees')
        start = (b-float(sectors[i]['FromBearing'])) % 360
        end = (float(sectors[(i+1) % len(sectors)]['FromBearing'])-b) % 360
        near = min(start, end)
        if len(sectors) == 1 or feather <= 0 or near >= feather:
            return primary
        neighbour = (i-1 if start <= end else i+1) % len(sectors)
        return lerp(self.profile(d, int(sectors[neighbour]['Class'])), primary,
                    .5+.5*smooth(near/feather))

    def __call__(self, p):
        f, v = self.f, self.v
        deep = f('deepHarbourElevation')
        e = max(deep, self.island(p), self.band(distance(p, v('sandbarFrom'), v('sandbarTo')),
                0, f('sandbarHalfWidth'), f('sandbarCrestElevation'), deep))
        a, b = v('sandbarFrom'), v('sandbarTo')
        cross = tuple(lerp(a[i], b[i], max(0, min(1, f('channelAlong')))) for i in (0, 1))
        length = math.dist(a, b)
        dc = math.dist(p, cross) if length*length <= 1e-6 else abs(sum((p[i]-cross[i])*(b[i]-a[i])/length for i in (0, 1)))
        if dc < f('channelHalfWidth') and e > f('channelBedElevation'):
            e = max(deep, lerp(e, f('channelBedElevation'), 1-smooth(dc/f('channelHalfWidth'))))
        for name in ('berth', 'approach', 'berthPocket', 'northPocket'):
            half, bed = f(name+'HalfWidth'), f(name+'BedElevation')
            if half <= 0 or e <= bed:
                continue
            d = distance(p, v(name+'From'), v(name+'To'))
            if d >= half:
                continue
            flat = max(0, min(half, f(name+'ThalwegHalfWidth')))
            e = max(min(deep, bed), min(e, bed if d <= flat else lerp(e, bed, 1-smooth((d-flat)/(half-flat)))))
        return e


class Painted:
    def __init__(self):
        self.path = DATA+'Terrain/StPetersSeabed_HeightTex.png'
        self.mapping = asset(DATA+'Terrain/StPetersSeabed.asset')
        raw = (ROOT/self.path).read_bytes()
        self.sha = hashlib.sha256(raw).hexdigest()
        self.png = decode_r8(raw)
        self.codes = 255
        if self.png is None:
            if raw[:8] != b'\x89PNG\r\n\x1a\n' or struct.unpack('>BB',raw[24:26]) != (16,0):
                raise ValueError('Expected a present greyscale 8/16-bit height PNG')
            # The existing export decoder deliberately accepts only R8. Pillow preserves
            # unsigned 16-bit samples here; never convert this image to L (8-bit).
            from PIL import Image
            from hhexport.heightmap import Png
            with Image.open(ROOT/self.path) as im:
                w,h=im.size
                pixels=list(im.get_flattened_data())
            self.png=Png(w,h,[pixels[y*w:(y+1)*w] for y in range(h)])
            self.codes=65535
        self.center, self.size = vec(self.mapping['_worldCenter']), vec(self.mapping['_worldSize'])
        self.low, self.high = float(self.mapping['_minElevation']), float(self.mapping['_maxElevation'])

    def __call__(self, p):
        w, h = self.png.width, self.png.height
        fx = (p[0]-self.center[0]+self.size[0]/2)/self.size[0]*w-.5
        fy = (p[1]-self.center[1]+self.size[1]/2)/self.size[1]*h-.5
        x, y = math.floor(fx), math.floor(fy)
        def at(a, b):
            # PNG starts at north; Unity's field starts at south.
            code = self.png.rows[h-1-max(0, min(h-1, b))][max(0, min(w-1, a))]
            return lerp(self.low, self.high, code/self.codes)
        return lerp(lerp(at(x,y), at(x+1,y), fx-x), lerp(at(x,y+1), at(x+1,y+1), fx-x), fy-y)


def kind(doc):
    return doc.data.get('m_EditorClassIdentifier', '').split('.')[-1]


def points_on(a, b, step):
    n = max(1, math.ceil(math.dist(a,b)/step))
    return [tuple(lerp(a[j],b[j],i/n) for j in (0,1)) for i in range(n+1)]


def survey():
    scene = Scene(U.parse_file(ROOT/SCENE))
    def components(k):
        return [d for d in scene.behaviours.values() if kind(d) == k]
    def pos(d):
        t = scene.transform_of(U.ref(d.data['m_GameObject']))
        return scene.world_of(t.file_id)[:2]
    terrain, = components('TidalTerrain')
    old, painted = Analytic(terrain.data), Painted()
    region = asset(DATA+'Regions/StPeters.asset')
    mean, amp = float(region['TideMeanLevel']), float(region['TideAmplitude'])
    tides = [mean-amp, mean, mean+amp]
    hulls = []
    for path in sorted((ROOT/DATA/'Boats').glob('*.asset')):
        d = asset(path.relative_to(ROOT))
        if 'DraughtMeters' in d:
            hulls.append((d['Id'], float(d['DraughtMeters']), path.relative_to(ROOT).as_posix()))
    rows = []
    def add(label, p, source, group):
        a, b = old(p), painted(p)
        rows.append(dict(label=label, x=p[0], y=p[1], analytic=a, painted=b,
                         depths_a=[t-a for t in tides], depths_p=[t-b for t in tides],
                         source=source, group=group))
    opening, = components('ArrivalOpening')
    add('Arrival berth / dock zone', vec(opening.data['_berth']), f"ArrivalOpening &{opening.file_id} _berth", 'berths')
    boat, = components('BoatController')
    add('Dory mooring', pos(boat), f"BoatController &{boat.file_id} transform", 'berths')
    for name in ('berth', 'approach', 'berthPocket', 'northPocket'):
        a, b = old.v(name+'From'), old.v(name+'To')
        for suffix, p in [('from', a), ('mid', tuple((a[i]+b[i])/2 for i in (0,1))), ('to', b)]:
            add(name+' '+suffix, p, f'TidalTerrain _{name}From/To', 'cuts')
    for i, p in enumerate(opening.data['_route']):
        add(f'Arrival waypoint {i}', vec(p), f'ArrivalOpening _route[{i}]', 'route')
    anchor, = components('RegionAnchor')
    arrival = next(v for v in anchor.data['_arrivals'] if v['Key'] == 'east_water')
    entry_id = U.ref(arrival['ArrivalPoint'])
    if entry_id not in scene.transforms:
        raise ValueError('Missing east-water arrival transform')
    entry = scene.world_of(entry_id)[:2]
    add('Arrival continuation entry', entry, f'RegionAnchor east_water transform &{entry_id}', 'route')
    for d in sorted(components('ShoreCleat'), key=lambda d:d.data['_id']):
        add(d.data['_id'], pos(d), f'ShoreCleat &{d.file_id}', 'moorings')
    platform, = components('StandablePlatform')
    rect = platform.data['_footprint']
    x,y,w,h = (float(rect[k]) for k in ('x','y','width','height'))
    faces = {'south':((x,y),(x+w,y)), 'north':((x,y+h),(x+w,y+h)),
             'west':((x,y),(x,y+h)), 'east':((x+w,y),(x+w,y+h))}
    for face,(a,b) in faces.items():
        for suffix,p in [('start',a),('mid',tuple((a[i]+b[i])/2 for i in (0,1))),('end',b)]:
            add(f'Pier {face} {suffix}', p, 'StandablePlatform _footprint', 'faces')
    stations, = components('RoutineStations')
    for st in stations.data['_stations']:
        if st['Id'].endswith('.slip_head'):
            add('Slip head (on deck)', vec(st['Position']), 'RoutineStations slip_head', 'cuts')
    for i,d in enumerate(sorted(components('ClamSpot'), key=lambda d:pos(d))):
        add(f'Clam {i+1:02d}', pos(d), f'ClamSpot &{d.file_id}', 'fishing')
    # Spatial sweeps supplement named points; these are sampled minima, not continuous proofs.
    sweeps = []
    def sweep(label, pts):
        vals = [(p,old(p),painted(p)) for p in pts]
        a = max(vals, key=lambda v:v[1]); b=max(vals, key=lambda v:v[2])
        delta=max(vals,key=lambda v:abs(v[2]-v[1]))
        contacts=[]
        for tide in tides:
            for draft in sorted({d for _,d,_ in hulls}):
                losses=[v for v in vals if new_contact(tide-v[1],tide-v[2],draft)]
                if losses:
                    v=max(losses,key=lambda v:v[2])
                    contacts.append(dict(tide=tide,draft=draft,at=v[0],clearance=tide-v[1]-draft,shortfall=draft-(tide-v[2]),count=len(losses)))
        sweeps.append(dict(label=label,count=len(vals),a=a[1],p=b[2],at_a=a[0],at_p=b[0],delta=delta[2]-delta[1],contacts=contacts))
    for face,(a,b) in faces.items():
        sweep('Pier '+face+' edge', points_on(a,b,.1))
    route = [vec(p) for p in opening.data['_route']]
    for i,(a,b) in enumerate(zip(route,route[1:])):
        sweep(f'Arrival centreline leg {i}-{i+1}', points_on(a,b,.1))
    sweep('Arrival continuation entry to waypoint 1', points_on(entry,route[1],.1))
    # The Def length + measured beam constants used by the existing berth tests.
    builder = (ROOT/'Assets/_Project/Code/App/Editor/StPetersBuilder.cs').read_text(encoding='utf-8-sig')
    import re
    for label,center,hull,beam_name in [('Dory berthed rectangle',pos(boat),'Dory','DoryHalfBeamMetres'),
                                      ('Arrival berthed rectangle',vec(opening.data['_berth']),'CapeIslander','ArrivalHullHalfBeamMetres')]:
        length=float(asset(DATA+'Boats/'+hull+'.asset')['LengthMeters'])
        beam=float(re.search(r'const float '+beam_name+r'\s*=\s*([0-9.]+)f',builder).group(1))
        # Both serialized poses are east-west: full rectangular footprint, conservative at the ends.
        pts=[(px[0],py[1]) for px in points_on((center[0]-length/2,0),(center[0]+length/2,0),.1)
             for py in points_on((0,center[1]-beam),(0,center[1]+beam),.1)]
        sweep(label,pts)
    return dict(rows=rows,sweeps=sweeps,hulls=hulls,tides=tides,png_sha=painted.sha,
                resolution=[painted.png.width,painted.png.height],mapping=painted.mapping)


def tables(report):
    out=[]
    def put(s=''): out.append(s)
    put('<!-- BEGIN GENERATED SURVEY -->')
    put(f"Source base: `{BASE}`. Height PNG SHA-256: `{report['png_sha']}`.")
    put(f"Decoded resolution: {report['resolution'][0]} x {report['resolution'][1]}. {len(report['rows'])} named samples; {len(report['hulls'])} hull Defs screened.")
    put()
    put('A = analytic; P = painted. All numbers are metres; depth triplets are lowest / mean / highest tide. Negative depth means exposed ground. **D** flags absolute bed difference > 0.5 m. **R** flags at least one hull that passes A but touches or penetrates P at a surveyed tide. Exact hulls and shortfalls follow. Coordinates are scene XY, not projected artwork positions.')
    put()
    for group in ['berths','cuts','route','moorings','faces','fishing']:
        put('### '+{'berths':'Occupied berths','cuts':'Slip and dredged cuts','route':'NPC arrival waypoints','moorings':'All shore tie-offs','faces':'All pier faces','fishing':'All authored fishing spots (clam holes)'}[group])
        put()
        put('| Sample | XY | Bed A / P | Depth A: L / M / H | Depth P: L / M / H | Flags | Source in scene |')
        put('|---|---|---|---|---|---|---|')
        for r in report['rows']:
            if r['group'] != group: continue
            delta=r['painted']-r['analytic']
            reg=any(new_contact(da,dp,draft) for da,dp in zip(r['depths_a'],r['depths_p']) for _,draft,_ in report['hulls'])
            flags=('D ' if abs(delta)>.5 else '')+('R' if reg else '')
            trip=lambda v:' / '.join(f'{x:.3f}' for x in v)
            put(f"| {r['label']} | {r['x']:.3f}, {r['y']:.3f} | {r['analytic']:.3f} / {r['painted']:.3f} | {trip(r['depths_a'])} | {trip(r['depths_p'])} | {flags or '-'} | `{r['source']}` |")
        put()
    put('### Every new bottom-contact case at named samples')
    put()
    put('Hull groups below include **all** Defs with that draft (see the fleet table), whether or not those boats are spawned here. Shortfall = draft minus painted depth, using unrounded values; zero means touching. Existing shallow water in both models is not a new contact. No extra under-keel allowance is imposed.')
    put()
    put('| Sample | Tide | Draft group | A clearance | P shortfall |')
    put('|---|---|---:|---:|---:|')
    for r in report['rows']:
        for tide,da,dp in zip(('lowest','mean','highest'),r['depths_a'],r['depths_p']):
            for draft in sorted(set(d for _,d,_ in report['hulls'])):
                if new_contact(da,dp,draft):
                    put(f"| {r['label']} | {tide} | {draft:.2f} | {da-draft:.3f} | {draft-dp:.3f} |")
    put()
    put('### Face, route and berthed-footprint sweeps')
    put()
    put('Spacing at most 0.1 m, endpoints included. A and P maxima may occur at different positions. These are finite samples, not collision or reachability proofs; route turns and the moving hull envelope are not certified.')
    put()
    put('| Sweep | Samples | Highest bed A / P | Minimum depths A; P (L / M / H) | XY worst A; worst P | Max absolute discrepancy (signed P-A) |')
    put('|---|---:|---|---|---|---:|')
    for s in report['sweeps']:
        xy=lambda p:f'({p[0]:.3f}, {p[1]:.3f})'
        da=' / '.join(f'{t-s["a"]:.3f}' for t in report['tides'])
        dp=' / '.join(f'{t-s["p"]:.3f}' for t in report['tides'])
        put(f"| {s['label']} | {s['count']} | {s['a']:.3f} / {s['p']:.3f} | {da}; {dp} | {xy(s['at_a'])}; {xy(s['at_p'])} | {s['delta']:.3f} |")
    put()
    put('New contacts found between named samples, grouped by sweep, tide and draft; the coordinate is the greatest shortfall in that group. Counts include shared endpoints. Tie-off and pier-edge coordinates screen depth at the structure, not an assigned keel.')
    put()
    put('| Sweep | Water level | Draft group | A clearance | P shortfall | Worst XY | Contact samples |')
    put('|---|---:|---:|---:|---:|---|---:|')
    for s in report['sweeps']:
        for c in s['contacts']:
            put(f"| {s['label']} | {c['tide']:.2f} | {c['draft']:.2f} | {c['clearance']:.3f} | {c['shortfall']:.3f} | {xy(c['at'])} | {c['count']} |")
    put()
    put('### Hull drafts from Defs')
    put()
    put('| Hull ID | Draft (m) | Source |')
    put('|---|---:|---|')
    for id,draft,path in report['hulls']:
        put(f'| `{id}` | {draft:.2f} | `{path}` |')
    put()
    put('<!-- END GENERATED SURVEY -->')
    return '\n'.join(out)+'\n'


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--json', action='store_true', help='Emit unrounded data instead of Markdown')
    args=p.parse_args()
    result=survey()
    print(json.dumps(result,indent=2) if args.json else tables(result),end='\n' if args.json else '')

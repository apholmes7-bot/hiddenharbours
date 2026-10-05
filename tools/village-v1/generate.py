#!/usr/bin/env python3
"""Generate the V1 village Defs without Unity (Python standard library only).

python tools/village-v1/generate.py --scene artifacts/village-v1/scene.json
Use --check to compare without writing; JSON comparisons normalize LF/CRLF.
The package is an external, read-only input; no zip is unpacked by this tool.
Existing metas and id mappings are authoritative, never regenerated on reissue.
"""
import argparse
import hashlib
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = Path('Assets/_Project/Data/Regions/StPetersVillage')
CODE = Path('Assets/_Project/Code/World/Village')
MAP = Path('docs/design/st-peters-village-plan/village-id-map.json')


def snake(value):
    return re.sub(r'(?<=[a-z0-9])([A-Z])', r'_\1', value).lower()


def point(value):
    return dict(zip(('x', 'y'), (round(v, 6) for v in value)))


def bounds(value):
    return dict(zip(('x', 'y', 'z', 'w'), (round(v, 6) for v in value)))


def outline(value):
    return [point(v) for v in value]


def rectangle(value):
    x0, y0, x1, y1 = value
    return outline([[x0, y0], [x1, y0], [x1, y1], [x0, y1]])


def compact(value):
    return json.dumps(value, ensure_ascii=False, separators=(',', ':'))


def guid(path):
    existing = ROOT / (str(path) + '.meta')
    if existing.exists():
        return re.search(r'^guid: ([0-9a-f]{32})$', existing.read_text(), re.M)[1]
    return hashlib.sha256(('hiddenharbours.village.v1/' + str(path).replace('\\', '/')).encode()).hexdigest()[:32]


def scalar(value):
    if isinstance(value, bool):
        return '1' if value else '0'
    if isinstance(value, str):
        return json.dumps(value, ensure_ascii=False)
    if isinstance(value, dict) and set(value) <= {'x', 'y', 'z', 'w'}:
        return '{' + ', '.join(k + ': ' + scalar(v) for k, v in value.items()) + '}'
    if isinstance(value, (float, int)):
        return str(value)
    if value is None:
        return '""'
    if value == []:
        return '[]'
    raise TypeError(value)


def yaml_lines(value, indent=2):
    lines = []
    for key, item in value.items():
        prefix = ' ' * indent + key + ':'
        if isinstance(item, list) and item:
            lines.append(prefix)
            for row in item:
                if isinstance(row, dict) and not set(row) <= {'x', 'y', 'z', 'w'}:
                    sub = yaml_lines(row, indent + 2)
                    sub[0] = ' ' * indent + '- ' + sub[0].lstrip()
                    lines.extend(sub)
                else:
                    lines.append(' ' * indent + '- ' + scalar(row))
        elif isinstance(item, dict) and not set(item) <= {'x', 'y', 'z', 'w'}:
            lines.append(prefix)
            lines.extend(yaml_lines(item, indent + 2))
        else:
            lines.append(prefix + ' ' + scalar(item))
    return lines


def asset_text(kind, data):
    header = '''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
'''
    header += '  m_Script: {fileID: 11500000, guid: ' + guid(CODE / (kind + '.cs')) + ', type: 3}\n'
    header += '  m_Name: ' + data['Id'] + '\n'
    header += '  m_EditorClassIdentifier: HiddenHarbours.World::HiddenHarbours.World.' + kind + '\n'
    return header + '\n'.join(yaml_lines(data)) + '\n'


def meta_text(path, folder=False):
    header = 'fileFormatVersion: 2\nguid: ' + guid(path) + '\n'
    if folder:
        return header + 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    if path.suffix == '.cs':
        return header + 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    return header + 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'


def build(plan, scene):
    old_map = json.loads((ROOT / MAP).read_text(encoding='utf-8')) if (ROOT / MAP).exists() else {'renames': []}
    renames = {r['old']: r['new'] for r in old_map['renames']}
    if len(renames) != len(old_map['renames']) or len(set(renames.values())) != len(renames):
        raise ValueError('The append-only id map contains duplicate old or new ids')
    reserved = set(renames.values())
    sequence = Counter()
    light_source = {'light.stp_vl_' + r['k'] + '_' + r['of']: r for r in plan['lights']}
    package_light_ids = {r['id'] for r in scene['pieces'] if r['id'].startswith('light.')}
    if package_light_ids != set(light_source):
        raise ValueError('Package lights must resolve by name to the plan, never by array order')
    for row in scene['pieces']:
        old = row['id']
        base = snake(old.split('@')[0])
        if '@' in old and old not in renames:
            # Reissues can reorder the file. Previously issued identities keep their map;
            # only new rows receive the next free ordinal, in this issue's source order.
            sequence[base] += 1
            while base + f'_{sequence[base]:02}' in reserved:
                sequence[base] += 1
            new = base + f'_{sequence[base]:02}'
        else:
            new = renames.get(old, base)
        if old in light_source:
            light = light_source[old]
            new = 'light.st_peters.' + snake(light['k']) + '_' + light['of']
        if new != old:
            renames.setdefault(old, new)
            reserved.add(new)
    for b in plan['buildings']:
        renames.setdefault('walk.' + b['id'], 'route.stpeters.' + b['id'] + '_walk')
    ids = [renames.get(r['id'], r['id']) for r in scene['pieces']]
    if len(ids) != len(set(ids)):
        raise ValueError('Duplicate mapped package id')
    def rename_values(v):
        if isinstance(v, str):
            return renames.get(v, v)
        if isinstance(v, dict):
            return {k: rename_values(x) for k, x in v.items()}
        if isinstance(v, list):
            return [rename_values(x) for x in v]
        return v

    assets = []
    def add(kind, **row):
        assets.append((kind, row))
        return row

    pieces = []
    piece_by_old = {}
    for row in scene['pieces']:
        piece = dict(Id=renames.get(row['id'], row['id']), Kit=row['kit'], Piece=row['piece'],
                     Position=point(row['at']), Z=row.get('z', 0), Dir=row.get('dir', 4),
                     Pending=row['kit'] == 'pending', OptionsJson=compact(rename_values(row.get('opts', {}))),
                     CasterAnchors=outline([p[:2] for p in row.get('opts', {}).get('panels', [])]),
                     MetadataJson=compact(rename_values({k: v for k, v in row.items() if k not in
                         ('id', 'kit', 'piece', 'at', 'z', 'dir', 'opts', 'footprint')})))
        pieces.append(piece)
        piece_by_old[row['id']] = piece
    def placement(row, candidates=None):
        candidates = candidates or scene['pieces']
        eligible = [r for r in candidates if sum((a-b)**2 for a,b in zip(r['at'], row['at'])) < .0005]
        if row['k'] == 'hearthRing':
            match = next(r for r in scene['pieces'] if r['id'].startswith('light.') and r['piece'] == 'firePit')
        else:
            if len(eligible) != 1:
                raise ValueError('Placement match ambiguous: ' + compact(row))
            match = eligible[0]
        return dict(PieceId=renames.get(match['id'], match['id']), Kind=row['k'], Position=point(row['at']),
                    RouteId='route.stpeters.shore_walk' if row.get('of') == 'shore_walk' else '',
                    Road=row.get('road', ''),
                    MetadataJson=compact({k:v for k,v in row.items() if k not in ('at', 'k')}))

    footprints = []
    buildings = {b['id']: dict(b, **b.get('jobOneAsk', {})) for b in plan['buildings']}
    lots = {}
    measurements = []
    for src in plan['lots']:
        name = src['building']
        b = buildings[name]
        pos = b['at']
        foot_id = 'footprint.st_peters.' + name
        footprints.append(dict(Id=foot_id, LocalBounds=bounds([v-pos[i % 2] for i,v in enumerate(b['footprint'])]),
                               Source=b['footprintFrom'], Provisional='new_homes' in b['footprintFrom']))
        preset = b['preset']
        bake_key = preset[len('new: '):] if preset.startswith('new: ') else (
            preset.removeprefix('(bake: Buildings.json ').removesuffix(')') if preset.startswith('(bake:') else preset)
        lot = add('LotDef', Id=src['id'], BuildingId='building.stp_' + name, BakeKey=bake_key,
                  StreetId=src['street'], Position=point(pos), DoorOffset=point([b['door'][i]-pos[i] for i in range(2)]),
                  FootprintId=foot_id, FacingCell=b['cell'], Entry=b['entry'], DoorFaces=b['doorFaces'],
                  RoofTop=b['roofTop'], ArtBottom=b.get('artBottom', b['footprint'][1]),
                  Gate=point(src['gate']), Approach=point(b['approach']), Outline=outline(src['outline']),
                  SetbackMetres=src['setbackMetres'], FrontageMetres=src['frontageMetres'], DepthMetres=src['depthMetres'],
                  WalkId='route.stpeters.' + name + '_walk')
        lots[name] = lot
        piece = piece_by_old[lot['BuildingId']]
        package_position = list(piece['Position'].values())
        piece['Position'] = point(pos)
        measurements.append(dict(building=name, position=pos, door=b['door'], footprint=b['footprint'],
                                 source='jobOneAsk' if 'jobOneAsk' in b else 'buildings',
                                 package_position=package_position,
                                 package_delta=[round(pos[i]-package_position[i],6) for i in range(2)]))
    fences = {row['building']: row for row in plan['fences']}
    if len(fences) != len(plan['fences']) or set(fences) != {r['building'] for r in plan['yards']}:
        raise ValueError('Yards and fences must resolve by building id, never by array order')
    for row in plan['yards']:
        name = row['building']
        fence = fences[name]
        add('YardDef', Id='yard.st_peters.' + name, LotId=lots[name]['Id'],
            Fence=['None','Picket','PostRail','SplitRail','Wire','Stone','Hedge'].index(row['fence'] or 'None'),
            Mown=['Rough','Kept','Striped'].index(row['mown']), Outline=outline(row['outline']), Gate=point(row['gate']),
            Dressing=[placement(r, [p for p in scene['pieces'] if p['id'].startswith('prop.stp_vl_' + name + '_')]) for r in row['dressing']],
            FenceSummary=dict(Panels=fence['panels'], Posts=fence['posts'], Gates=fence['gates'],
                Offcuts=fence['offcuts'], OffcutMetres=fence['offcut_m'], Metres=fence['metres']))
    for r in plan['lanes']:
        add('RouteDef', Id=r['id'], Class=r['class'], WidthMetres=r.get('width', plan['tunables']['CLASS_M'][r['class']]),
            Points=outline(r['points']), LotId='')
    for name, b in buildings.items():
        walk = [b['path'][0], b['path'][1], b['door']]
        add('RouteDef', Id=lots[name]['WalkId'], Class='footpath', WidthMetres=1.5 if name in ('general_store','post_office') else 1.1,
            Points=outline(walk), LotId=lots[name]['Id'])
    for key in ('square', 'westGarden', 'flakeYard'):
        def owner(position):
            if position[0] < -8:
                return 'westGarden'
            if position[0] >= 34:
                return 'flakeYard'
            return 'square'
        add('CommonsDef', Id='commons.st_peters.' + snake(key), Outline=rectangle(plan['commons'][key]),
            Furniture=[placement(r) for r in plan['furniture'] if owner(r['at']) == key],
            Stations=[dict(Id=r['id'], Position=point(r['at']), Node=r['node'], Who=r['who'])
                      for r in plan['stations'] if owner(r['at']) == key])
    lamp_by_key = {(r['k'],r['of']):r for r in plan['lamps'][:16]}
    light_changes = []
    for old, r in light_source.items():
        pos = list(r['at'])
        if r['k'] == 'doorstepLantern' and r['of'] in ('red_saltbox','white_farmhouse'):
            door = buildings[r['of']]['door']
            old_door = next(b['door'] for b in plan['buildings'] if b['id']==r['of'])
            pos = [round(door[i]+r['at'][i]-old_door[i],6) for i in range(2)]
        lamp = lamp_by_key[(r['k'],r['of'])]
        lot_id = lots[r['of']]['Id'] if r['of'] in lots else ''
        commons = 'square' if r['of'] in ('square','green','green_walk') else 'west_garden' if r['of']=='bluff_walk' else 'flake_yard'
        add('LightPostDef', Id=renames[old], LotId=lot_id, CommonsId='' if lot_id else 'commons.st_peters.'+commons,
            RouteId='route.stpeters.'+r['of'] if any(x['id']=='route.stpeters.'+r['of'] for x in plan['lanes']) else '',
            KitPiece=piece_by_old[old]['Piece'], Position=point(pos), Preset=lamp['kind'], LitBy=r['lit'], Reach=lamp['reach'], CastsShadow=True)
        light_changes.append(dict(id=renames[old], plan_position=r['at'], position=pos,
                                  package_position=list(piece_by_old[old]['Position'].values())))
        piece_by_old[old]['Position'] = point(pos)
    tunables = []
    for key, val in plan['tunables'].items():
        if isinstance(val, dict):
            tunables.extend(dict(Name=key+'_'+k, Value=v) for k,v in val.items())
        else:
            tunables.append(dict(Name=key,Value=val))
    tunables.extend(dict(Name=k,Value=v) for k,v in dict(FOOTPRINT_GAP=4,HEARTH_CLEARANCE=8,BAR_SIGHTLINE=40,
        GATE_WIDTH=1.8,EPSILON=.001,MARKER_REACH=3).items())
    windows = [dict(Key=r['key'], Bounds=bounds(r['rect']), Focal=r['focal'], Wide=False) for r in plan['frames']['windows']]
    for r in plan['frames']['wide']:
        x,y=r['centre'];w=r['w']/2;h=r['h']/2
        windows.append(dict(Key=r['key'],Bounds=bounds([x-w,y-h,x+w,y+h]),Focal=r['focal'],Wide=True))
    add('VillagePlanDef', Id='village_plan.st_peters', Lines=[dict(Name=k,Value=v) for k,v in plan['lines'].items()],
        Tunables=tunables, FixedPoints=[dict(Name=k,Position=point(v)) for k,v in plan['fixedPoints'].items()],
        Footprints=footprints, LotIds=[r['Id'] for k,r in assets if k=='LotDef'], YardIds=[r['Id'] for k,r in assets if k=='YardDef'],
        RouteIds=[r['Id'] for k,r in assets if k=='RouteDef'], CommonsIds=[r['Id'] for k,r in assets if k=='CommonsDef'],
        LightIds=[r['Id'] for k,r in assets if k=='LightPostDef'], Pieces=pieces, Roadside=[placement(r) for r in plan['roadside']],
        HearthRingPosition=point(plan['commons']['hearthRing']['at']), HearthRingRadius=plan['tunables']['RING_R'],
        Tree=[dict(Node=r['node'],Parent=r['parent'] or '',Position=point(r['at']),Via=outline(r['via']),Reason=r['why']) for r in plan['tree']],
        CycleLinks=[dict(Link=r['link'],Between=r['between'],Via=r['via'],WouldServe=r['would_serve']) for r in plan['cycleLinks']],
        Windows=windows, WindowWidth=plan['frames']['window']['w'],WindowHeight=plan['frames']['window']['h'],WindowExtent=bounds(plan['frames']['window']['extent']),
        LampComparisons=[dict(Kind=r['k'],Position=point(r['at']),Owner=r['of'],Preset=r['kind'],Reach=r['reach'],When=r['when'],Active=r['when']!='line') for r in plan['lamps'][16:]])
    mapping=dict(schema='hidden-harbours/village-id-map@1',renames=[dict(old=k,new=v) for k,v in renames.items()])
    report=dict(assets=len(assets),counts=dict(Counter(k for k,r in assets)),mappedIds=len(renames),pieces=len(pieces),
                pendingPieces=sum(p['Pending'] for p in pieces),buildings=measurements,lights=light_changes,
                rows=dict(lots=9,yards=9,lanes=14,walks=9,commons=3,hearthRing=1,furniture=27,roadside=10,fences=9,lights=16,
                          dressing=sum(len(r['dressing']) for r in plan['yards']),stations=9,tree=23,cycleLinks=5,windows=36,wideWindows=4,
                          activeExternalLamps=1,inactiveLineLamps=12))
    report['fencePanelDifferences'] = []
    for fence in plan['fences']:
        package = next((r for r in scene['pieces'] if r['id']=='prop.stp_vl_fence_'+fence['building']),None)
        if package is not None:
            count = len(package.get('opts',{}).get('panels',[]))
            if count != fence['panels']:
                report['fencePanelDifferences'].append(dict(building=fence['building'],plan=fence['panels'],package=count))
    return assets, mapping, report


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scene', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--report', type=Path)
    args=parser.parse_args()
    plan=json.loads((ROOT/'docs/design/st-peters-village-plan/st-peters-village-plan.json').read_text(encoding='utf-8'))
    scene=json.loads(args.scene.read_text(encoding='utf-8'))
    assets,mapping,report=build(plan,scene)
    outputs={MAP:json.dumps(mapping,ensure_ascii=False,indent=2)+'\n'}
    for kind,row in assets:
        path=DATA/(row['Id']+'.asset')
        outputs[path]=asset_text(kind,row)
        outputs[Path(str(path)+'.meta')]=meta_text(path)
    for path in (ROOT/CODE).glob('*.cs'):
        rel=path.relative_to(ROOT)
        meta=Path(str(path)+'.meta')
        outputs[Path(str(rel)+'.meta')]=meta.read_text(encoding='utf8') if meta.exists() else meta_text(rel)
    for folder in (DATA,CODE):
        outputs[Path(str(folder)+'.meta')]=meta_text(folder,folder=True)
    differing=[]
    for rel,text in outputs.items():
        path=ROOT/rel
        # Unity YAML has LF attributes. The JSON map has no eol rule: normalize on read.
        same=path.exists() and path.read_text(encoding='utf-8')==text
        if not same:
            differing.append(str(rel))
            if not args.check:
                path.parent.mkdir(parents=True,exist_ok=True)
                path.write_text(text,encoding='utf-8',newline='\n')
    report['assetBytes']=sum(len(outputs[p].encode('utf8')) for p in outputs if p.suffix=='.asset')
    report['generatedBytes']=sum(len(v.encode('utf8')) for v in outputs.values())
    if args.report:
        args.report.parent.mkdir(parents=True,exist_ok=True)
        args.report.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(('CHECK' if args.check else 'GENERATED')+f': {len(assets)} assets, {len(mapping["renames"])} mapped ids, {len(differing)} differing files, {report["assetBytes"]} asset bytes')
    if args.check and differing:
        raise SystemExit('\n'.join(differing))


if __name__=='__main__':
    main()

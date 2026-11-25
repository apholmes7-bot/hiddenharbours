"""Serialize the delivered module frames into five existing SetPieceDef assets.

Native sprites retain their pivots and individual atlas pages. This writer needs
no editor; the normal Unity importer consumes the resulting text and PNG files.
"""
from pathlib import Path
import copy, hashlib, json, math, struct, yaml

ROOT=Path(__file__).resolve().parents[5]
ART=ROOT/'Assets/_Project/Art/Sprites/NineMileCreek/WaterSide'
DEFS=ROOT/'Assets/_Project/Data/SetPieces'
TODAY=json.loads((ART/'nmcWaterSide.today.json').read_text(encoding='utf-8'))
PARTS={p['key']:p for p in TODAY['parts']}

def guid(name):return hashlib.md5(('nmc-h5/'+name).encode()).hexdigest()
def local_id(name):return int.from_bytes(hashlib.sha256(name.encode()).digest()[:8],'little') & ((1<<63)-1)
def sheet_guid(file):
    existing=Path(str(ART/file)+'.meta')
    return yaml.safe_load(existing.read_text(encoding='utf-8'))['guid'] if existing.exists() else guid(file)
def yaml_strings(value):
    if value is None:return ''
    if isinstance(value,dict):return {k:yaml_strings(v) for k,v in value.items()}
    if isinstance(value,list):return [yaml_strings(v) for v in value]
    return value
def dump(value):return yaml.safe_dump(yaml_strings(value),sort_keys=False,allow_unicode=False,width=100000)
def unity_asset(path,value):path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+dump({'MonoBehaviour':value}),encoding='utf-8',newline='\n')
def vec(values):return dict(zip(['x','y','z'],values))
def meta(path):
    if not Path(str(path)+'.meta').exists():
        Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid(path.relative_to(ROOT).as_posix())+'\n'+('folderAsset: yes\nDefaultImporter:\n' if path.is_dir() else 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n')+'  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')

template_meta=yaml.safe_load((ART.parent/'Yard/SetPiece_pierW.png.meta').read_text(encoding='utf-8'))
template_def_text=(DEFS/'SetPiece_pierW.asset').read_text(encoding='utf-8')
template_def=yaml.safe_load(template_def_text[template_def_text.index('MonoBehaviour:'):])['MonoBehaviour']
for part in PARTS.values():
    if len(part['frames'])!=8:raise ValueError('Bake all eight native facings first: '+part['key'])
    for group in ['min','max']:
        if not all(isinstance(v,(float,int)) and math.isfinite(v) for v in part['bounds'][group]):
            raise ValueError('A native model bound is not finite: '+part['key'])
    if part.get('floating') and not (math.isfinite(part['draught']) and part['draught']>=0):
        raise ValueError('A native float draught is not derived: '+part['key'])
for part in PARTS.values():
    if len(part['frames'])!=8:raise ValueError('Bake all eight native facings first: '+part['key'])
    for sheet in part['sheets']:
        path=ART/sheet['file'];header=path.read_bytes()[:24]
        if struct.unpack('>II',header[16:24])!=(sheet['width'],sheet['height']):raise ValueError('PNG dimensions differ')
        if max(sheet['width'],sheet['height'])>4096:raise ValueError('Atlas exceeds the declared cap')
        m=copy.deepcopy(template_meta);m['guid']=sheet_guid(sheet['file']);imp=m['TextureImporter']
        imp['maxTextureSize']=4096;imp['isReadable']=0;imp['textureCompression']=0
        imp['internalIDToNameTable']=[];sprites=[];names={}
        for f in part['frames']:
            if f['file']!=sheet['file']:continue
            name='WaterSide_'+part['key']+'_'+str(f['frame']);number=local_id(name)
            imp['internalIDToNameTable'].append({'first':{213:number},'second':name});names[name]=number
            sprite=copy.deepcopy(template_meta['TextureImporter']['spriteSheet']['sprites'][0])
            sprite.update(name=name,internalID=number,spriteID=guid(name))
            x,y,w,h=f['rect'];sprite['rect']={'serializedVersion':2,'x':x,'y':y,'width':w,'height':h}
            sprite['pivot']={'x':f['pivot'][0]/w,'y':1-f['pivot'][1]/h};sprite['alignment']=9
            sprites.append(sprite)
        imp['spriteSheet']['sprites']=sprites;imp['spriteSheet']['nameFileIdTable']=names
        for platform in imp.get('platformSettings',[]):platform['maxTextureSize']=4096;platform['textureCompression']=0
        meta_path=Path(str(path)+'.meta')
        if meta_path.exists():
            existing=yaml.safe_load(meta_path.read_text(encoding='utf-8'))
            if existing['guid']!=m['guid'] or existing['TextureImporter']['spriteSheet']['nameFileIdTable']!=names:
                raise ValueError('Existing sprite metadata has a different identity: '+str(meta_path))
            print('SKIPPED existing sprite meta:',meta_path.name)
        else:meta_path.write_text(dump(m),encoding='utf-8',newline='\n')

def frames(part):
    result=[]
    for f in sorted(part['frames'],key=lambda f:f['frame']):
        name='WaterSide_'+part['key']+'_'+str(f['frame'])
        result.append({'Sprite':{'fileID':local_id(name),'guid':sheet_guid(f['file']),'type':3},'RigDir':f['dir'],
            'ShowFaces':str(f['dir']),'BearingDeg':f['dir']*45,'CameraSees':'delivered harbour module',
            'NativeSize':vec(f['native']),'NativePivot':vec(f['nativePivot']),'Crop':vec(f['crop']),'Pivot':vec(f['pivot'])})
    return result

register=TODAY['register']
for piece in sorted(TODAY['pieces'],key=lambda p:p['key']=='fleet'):
    origin=piece['origin'];parts=[PARTS[k] for k in piece['parts']];first=parts[0];anchors={}
    def anchor(name,x,y,z):
        if name in anchors:raise ValueError('Duplicate anchor '+name)
        anchors[name]=[x-origin[0],y-origin[1],z]
    if piece['key']=='fleet':
        for b in register['berths']:anchor(b['id'],sum(b['slot'])/2,b['face'],3.4)
        for b in register['fingerBerths']:
            anchor(b['id'],b['x'],sum(b['y'])/2,0.45)
            finger=next(f for f in register['fingers'] if ('finger_a_' in b['id'])==f['id'].endswith('_a'))
            side=-1 if b['x']<finger['x'] else 1;edge=finger['x']+side*finger['width']/2
            anchor(b['id']+'.cleat.bow',edge,b['y'][1],0.45)
            anchor(b['id']+'.cleat.stern',edge,b['y'][0],0.45)
        for f in register['fingers']:anchor(f['id'],f['x'],sum(f['y'])/2,0.45)
    if piece['key']=='west':
        f=register['workingFloat'];anchor('float.nmc_working',sum(f['x'])/2,sum(f['y'])/2,0.45)
        b=register['crane'];anchor('berth.nmc_crane',sum(b['berth'])/2,b['face'],3.4)
    if piece['key']=='east':
        f=next(p for p in parts if p['name']=='recFloat');anchor('float.nmc_recreation',*f['at'],f['freeboard'])
        for fixture in f['module']['o']['marina']['items']:
            if fixture['kind']!='pedestal2':continue
            x=f['at'][0]-fixture['x'];y=f['at'][1]-f['module']['o']['width']/2
            anchor('berth.nmc_recreation_'+fixture['serves'][0],x,y,f['freeboard'])
    for ladder in register['ladders']:
        owner='west' if ladder['face']==76 else 'east' if ladder['id']=='ladder.nmc_east' else 'fleet'
        if owner==piece['key']:anchor(ladder['id'],ladder['x'],ladder['face'],3.4)
    if piece['key'] in ['arm_w','arm_e']:
        side='west' if piece['key']=='arm_w' else 'east';x,y,_=register['lights'][side]
        anchor('structure.nmc_light_'+side,x,y,3.6)
    seen_gangways=set()
    for p in parts:
        if 'gangway' not in p or p['gangway']['id'] in seen_gangways:continue
        g=p['gangway'];seen_gangways.add(g['id'])
        anchor('gangway.nmc_'+piece['key']+'_'+g['id']+'.head',*g['hinge'],p['elevation'])
        anchor('gangway.nmc_'+piece['key']+'_'+g['id']+'.toe',*g['landing'],0.45)
    # The full native fitting block stays in GameplayJson; stable cleat anchors
    # are derived from the same source list, never positioned beside it by hand.
    for p in parts:
        fit=p.get('fittings') or {};cleats=fit.get('cleats',[])
        if isinstance(cleats,dict):cleats=[q for values in cleats.values() for q in values]
        for i,q in enumerate(cleats):
            if not isinstance(q,dict) or 'x' not in q or 'y' not in q:continue
            rot=p['module']['rot'];x=p['at'][0]+q['x']*math.cos(rot)-q['y']*math.sin(rot);y=p['at'][1]+q['x']*math.sin(rot)+q['y']*math.cos(rot)
            anchor('cleat.nmc_'+piece['key']+'_'+p['name']+'_'+str(i),x,y,p['freeboard'] if p.get('floating') else p['elevation'])
    tide_range=max(p.get('module',{}).get('o',{}).get('tideRange',0) for p in parts);half_tide=tide_range/2
    marks=[{'Z':z,'OverDatum':z+half_tide,'At':vec([0,0,z]),'Label':str(z+half_tide),'Kind':kind} for z,kind in [(-half_tide,'spring low'),(half_tide,'spring high')]]
    path_points=[vec([p[0]-origin[0],p[1]-origin[1]]) for p in register['tracks']['in']] if piece['key']=='west' else []
    value=copy.deepcopy(template_def);value.update(m_Name='SetPiece_nmc_'+piece['key'],Id=piece['id'],DisplayName='Wharf water side '+piece['key'],
        RigKey=piece['source'],SceneId='keyscene.nmc_wharf',BakedOptionsJson='{}',Layer='floor',Walk='on',Mount='ground',
        BoundsMin=vec([min(p['bounds']['min'][i]+(p['at'][i]-origin[i] if i<2 else p['elevation']) for p in parts) for i in range(3)]),
        BoundsMax=vec([max(p['bounds']['max'][i]+(p['at'][i]-origin[i] if i<2 else p['elevation']) for p in parts) for i in range(3)]),
        Frames=frames(first),CellSize=vec(first['cellSize']),PixelsPerMetre=32,ElevationDeg=40,BakedLit=0,Colliders=[],
        Anchors=[{'Name':name,'Position':vec(at)} for name,at in sorted(anchors.items())],Path=path_points,TideMarks=marks,Words=[],HasLight=0,IsAnimated=0,
        GameplayJson=json.dumps({'schema':TODAY['schema'],'id':piece['id'],'origin':origin,'parts':parts,'register':register},separators=(',',':')),
        State='',Variants=[{'Name':p['name'],'BakedOptionsJson':json.dumps({'module':p['key']}),'GameplayJson':json.dumps(p,separators=(',',':')),
            'BoundsMin':vec(p['bounds']['min']),'BoundsMax':vec(p['bounds']['max']),'Frames':frames(p),'Anchors':[],'Colliders':[]} for p in parts[1:]],HasVendor=0)
    path=DEFS/('SetPiece_nmc_'+piece['key']+'.asset');unity_asset(path,value);meta(path)
    print(piece['id'],len(parts),'parts/poses',len(anchors),'anchors')
meta(ART)
today_meta=Path(str(ART/'nmcWaterSide.today.json')+'.meta')
if not today_meta.exists():today_meta.write_text('fileFormatVersion: 2\nguid: '+guid('nmcWaterSide.today.json')+'\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
else:print('SKIPPED existing today metadata')

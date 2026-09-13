// Repeatable, editor-free waterfront audit and bake. Run from the repository root.
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { encodePng, decodePng } from './rig-recipes/lib/png.mjs';

const rigPath = 'docs/art/rigs/iso-rig-pack/wharf-kit-iso/wharfIsoRig.js';
const assetDir = 'Assets/_Project/Art/Sprites/Wharf/Iso';
const contractPath = `${assetDir}/wharfIsoRig.contract.json`;
const mode = process.argv[2] || '--check';
const host = vm.createContext({});
vm.runInContext(mode === '--preview' && process.argv[4]
  ? execFileSync('git',['show',`${process.argv[4]}:${rigPath}`],{encoding:'utf8'})
  : fs.readFileSync(rigPath, 'utf8'), host);
const W = host.WharfIso;
const out = 'Temp/wharf-review';
fs.mkdirSync(out, { recursive: true });
const contract = JSON.parse(fs.readFileSync(contractPath, 'utf8'));
if (mode === '--bake') for (const base of W.MODULE_BASES) {
  for (const part of ['Start','Middle','End']) {
    const key=base+part;
    if (!contract.cells.some(c=>c.key===key)) contract.cells.push({key,sheet:{cols:4,rows:2}});
  }
}
function newMeta(key) {
  const hash = value => crypto.createHash('sha256').update(`HiddenHarbours/wharf-modules/${value}`).digest('hex');
  let meta=fs.readFileSync(`${assetDir}/logCrib.png.meta`,'utf8').replaceAll('logCrib_',`${key}_`);
  const ids=[...meta.matchAll(/      internalID: (-?\d+)/g)].map(m=>m[1]);
  for(let i=0;i<ids.length;i++) meta=meta.replaceAll(ids[i],BigInt(`0x${hash(`${key}/${i}`).slice(0,15)}`).toString());
  let index=0;
  return meta.replace(/^guid: \w+/m,`guid: ${hash(key).slice(0,32)}`)
    .replace(/spriteID: \w+/g,()=>`spriteID: ${hash(`${key}/sprite/${index++}`).slice(0,32)}`)
    .replace(/[ \t]+(?=\r?$)/gm,'');
}
const round = x => { // Mathf.RoundToInt: float32 input, ties to even.
  x = Math.fround(x);
  const lo = Math.floor(x);
  return x - lo === 0.5 ? (lo % 2 === 0 ? lo : lo + 1) : Math.round(x);
};
function blit(dst, dw, dh, src, x, y) {
  for (let sy = 0; sy < src.h; sy++) for (let sx = 0; sx < src.w; sx++) {
    const dx = sx + x, dy = sy + y, si = (sy * src.w + sx) * 4;
    if (dx < 0 || dy < 0 || dx >= dw || dy >= dh || !src.data[si + 3]) continue;
    dst.set(src.data.subarray(si, si + 4), (dy * dw + dx) * 4);
  }
}
function packed(key, cell) {
  const facings = Array.from({ length: 8 * (cell.rungs || 1) }, (_, i) =>
    W.render(key, (8 - i % 8) % 8, cell.rungs ? { rung: Math.floor(i / 8) } : {}));
  const left = Math.floor(Math.min(0, ...facings.map(c => -c.px)));
  const top = Math.floor(Math.min(0, ...facings.map(c => -c.py)));
  const right = Math.ceil(Math.max(0, ...facings.map(c => c.w - 1 - c.px)));
  const bottom = Math.ceil(Math.max(0, ...facings.map(c => c.h - 1 - c.py)));
  const spec = { cellW: right - left + 1, cellH: bottom - top + 1, pivotX: -left, pivotY: -top };
  const width = spec.cellW * cell.sheet.cols, height = spec.cellH * cell.sheet.rows;
  assert(width <= contract.importSizeCap && height <= contract.importSizeCap, `${key}: texture cap`);
  const rgba = new Uint8Array(width * height * 4);
  facings.forEach((c, i) => {
    const ox = round(spec.pivotX - c.px), oy = round(spec.pivotY - c.py);
    assert(ox >= 0 && oy >= 0 && ox + c.w <= spec.cellW && oy + c.h <= spec.cellH);
    blit(rgba, width, height, c, i % cell.sheet.cols * spec.cellW + ox,
      Math.floor(i / cell.sheet.cols) * spec.cellH + oy);
  });
  return { spec, rgba, width, height };
}

if (mode === '--preview') {
  const suffix = process.argv[3] || 'current';
  vm.runInContext(fs.readFileSync('docs/art/rigs/characterIsoRig.js', 'utf8'), host);
  vm.runInContext(fs.readFileSync('docs/art/rigs/doryIsoRig.js', 'utf8'), host);
  const C = host.CharacterIso, D = host.DoryIso;
  const views = ['timberFloat', 'tallPier', 'concreteQuay', 'logCrib', 'breakwater', 'gangway'];
  for (const key of views) {
    const dir = 4, s = W.resolve(key, {}), c = W.render(key, dir);
    const width = Math.max(850, c.w + 160), height = Math.max(400, c.h + 150);
    const data = new Uint8Array(width * height * 4);
    for (let i = 0; i < data.length; i += 4) data.set([20, 34, 41, 255], i);
    const origin = [width / 2, 90 + c.py];
    blit(data, width, height, c, round(origin[0] - c.px), 90);
    const human = W.project(dir, [0, 0, s.deckZ]);
    blit(data, width, height, { data: C.render(dir, {}), w: C.W, h: C.H },
      round(origin[0] + human.x - C.pivot.x), round(origin[1] + human.y - C.pivot.y));
    // Separate baseline references avoid claiming an unverified mooring or depth composite.
    blit(data, width, height, { data: D.render(2, {}), w: D.W, h: D.H },
      round(width / 2 - D.pivot.x), round(height - 25 - D.pivot.y));
    fs.writeFileSync(`${out}/${key}-${suffix}.png`, encodePng(data, width, height));
  }
  console.log(`Six unscaled comparison plates: ${out}/*-${suffix}.png`);
} else if (mode === '--plate') {
  const keys=['timberFloat','tallPier','breakwater'], labels=['FLOAT','PIER','BREAKWATER'];
  const cells=keys.flatMap(key=>['before','after'].map(v=>decodePng(fs.readFileSync(`${out}/${key}-${v}.png`))));
  const cw=Math.max(...cells.map(c=>c.width)), ch=Math.max(...cells.map(c=>c.height))+32;
  const width=cw*2,height=ch*3,data=new Uint8Array(width*height*4);
  for(let i=0;i<data.length;i+=4)data.set([20,34,41,255],i);
  const font={A:[14,17,17,31,17,17,17],B:[30,17,17,30,17,17,30],E:[31,16,16,30,16,16,31],
    F:[31,16,16,30,16,16,16],I:[31,4,4,4,4,4,31],K:[17,18,20,24,20,18,17],L:[16,16,16,16,16,16,31],
    O:[14,17,17,17,17,17,14],P:[30,17,17,30,16,16,16],R:[30,17,17,30,20,18,17],
    T:[31,4,4,4,4,4,4],W:[17,17,17,21,21,21,10]};
  function label(text,x,y){for(const char of text){
    (font[char]||[]).forEach((bits,row)=>{for(let col=0;col<5;col++)if(bits&(1<<(4-col)))
      for(let dy=0;dy<2;dy++)for(let dx=0;dx<2;dx++)data.set([200,213,207,255],((y+row*2+dy)*width+x+col*2+dx)*4);});x+=12;}}
  cells.forEach((c,i)=>{const x=i%2*cw,y=Math.floor(i/2)*ch;
    blit(data,width,height,{data:c.rgba,w:c.width,h:c.height},x,y+32);
    label(`${labels[Math.floor(i/2)]} ${i%2?'AFTER':'BEFORE'}`,x+20,y+10);});
  fs.writeFileSync('docs/art/wharf-review-2026-09-13.png',encodePng(data,width,height));
  console.log('Wrote docs/art/wharf-review-2026-09-13.png');
} else if (mode === '--audit') {
  const assets = new Map(), issues=[];
  for(const dir of ['Assets/_Project/Art/Sprites/Wharf','Assets/_Project/Art/Tilesets/Wharf'])
    for(const name of fs.readdirSync(dir,{recursive:true}).filter(n=>n.endsWith('.png.meta'))){
      const file=path.join(dir,name), meta=fs.readFileSync(file,'utf8');
      const guid=meta.match(/^guid: (\w+)/m)[1], ppu=+meta.match(/spritePixelsToUnits: ([\d.]+)/)[1];
      assets.set(guid,file.slice(0,-5).replaceAll('\\','/'));
      if(ppu!==32 || !/filterMode: 0/.test(meta) || !/enableMipMap: 0/.test(meta)) issues.push(file);
    }
  const scenes={};
  for(const scene of ['NineMileCreek','StPeters']){
    const text=fs.readFileSync(`Assets/_Project/Scenes/${scene}.unity`,'utf8'), counts={}, gos=new Map(), transforms=new Map(), sprites=[];
    for(const m of text.matchAll(/guid: ([a-f0-9]{32})/g)) if(assets.has(m[1])) {
      const file=assets.get(m[1]); counts[file]=(counts[file]||0)+1;
    }
    for(const m of text.matchAll(/--- !u!(\d+) &(\d+)[^\n]*\n([\s\S]*?)(?=--- !u!|$)/g)){
      const [_,type,id,body]=m, go=body.match(/m_GameObject: \{fileID: (\d+)/)?.[1];
      if(type==='1') gos.set(id,body.match(/m_Name: (.*)/)?.[1].trim());
      if(type==='4') transforms.set(id,{go,parent:body.match(/m_Father: \{fileID: (\d+)/)?.[1],
        scale:body.match(/m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)/)?.slice(1).map(Number)});
      if(type==='212'){
        const guid=body.match(/m_Sprite: \{fileID: [^,]+, guid: ([^,]+)/)?.[1];
        if(assets.has(guid)) sprites.push({go,asset:assets.get(guid)});
      }
    }
    const byGo=new Map([...transforms].map(([id,t])=>[t.go,id]));
    function scale(id,seen=new Set()){
      if(!id || id==='0') return [1,1,1]; assert(!seen.has(id),'transform cycle'); seen.add(id);
      const t=transforms.get(id); if(!t?.scale)return [1,1,1];
      return scale(t.parent,seen).map((v,i)=>v*t.scale[i]);
    }
    const scaled=sprites.map(s=>({...s,name:gos.get(s.go),scale:scale(byGo.get(s.go))})).filter(s=>s.scale.some(v=>Math.abs(v-1)>0.001));
    scenes[scene]={references:counts,spriteCount:sprites.length,nonUnitScale:scaled};
  }
  const report={texturesInspected:assets.size,importIssues:issues,scenes};
  fs.writeFileSync(`${out}/scene-audit.json`,JSON.stringify(report,null,2));
  console.log(JSON.stringify({texturesInspected:assets.size,importIssues:issues,
    scenes:Object.fromEntries(Object.entries(scenes).map(([k,v])=>[k,{references:v.references,spriteCount:v.spriteCount,nonUnitScale:v.nonUnitScale.slice(0,8),nonUnitCount:v.nonUnitScale.length}]))}));
} else if (mode === '--baseline') {
  const result = [];
  for (const c of contract.cells) {
    const p = packed(c.key, c), png = decodePng(fs.readFileSync(`${assetDir}/${c.key}.png`));
    result.push({ key: c.key, boundsMatch: ['cellW','cellH','pivotX','pivotY'].every(k => p.spec[k] === c[k]),
      pixelsMatch: png.width === p.width && png.height === p.height && Buffer.from(p.rgba).equals(png.rgba) });
  }
  fs.writeFileSync(`${out}/baseline.json`, JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
} else if (mode === '--check' || mode === '--bake') {
  const hulls = { dory:'Dory', punt:'Punt', skiff:'ConsoleSkiff', lobster:'LobsterBoat', packet:'CoastalPacket', dragger:'SideDragger' };
  for (const b of W.BOATS) {
    const def = fs.readFileSync(`Assets/_Project/Data/Boats/${hulls[b.id]}.asset`, 'utf8');
    assert.equal(b.loa, +def.match(/LengthMeters: ([\d.]+)/)[1], `${b.id}: berth length disagrees with BoatHullDef`);
  }
  for (const key of W.presets()) for (const tide of [0, 1.1, 2.2, 3.3, 4.4]) {
    const s = W.resolve(key, { tide }), t = W.frame(s), p = W.placements(s, t), g = W.gameplay(key, { tide });
    for (const b of p.berths.list) {
      assert(b.x0 >= -p.L / 2 - 0.011 && b.x1 <= p.L / 2 + 0.011, `${key}: hull overhangs berth`);
      assert(b.loa + W.BERTH_CLR <= b.edgeX[1] - b.edgeX[0] + 0.011, `${key}: clipped hull clearance`);
    }
    for (const tyre of p.tyres) for (const foam of p.foams)
      assert(Math.abs(tyre.x - foam.x) >= (W.FIT.tyre.od + W.FIT.foam.od) / 2, `${key}: fenders overlap`);
    for (const l of g.ladders) {
      assert.equal(l.botZ, s.family === 'float' ? +(tide - 1).toFixed(2) : -1);
      if (s.family === 'float') assert(Math.abs(l.topZ - l.botZ - (s.freeboard + 1)) < 0.001, 'float ladder telescopes');
    }
  }
  assert.equal(W.gameplay('pier', { bays:1, bayLen:2 }).berths.length, 0, 'short connector claims a berth');
  for (const freeboard of [0.25,0.4,0.8]) {
    const g = W.gameplay('gangway', { tide:1.1, freeboard });
    assert(Math.abs(g.ends.landing.z - (1.1 + freeboard + 0.06)) < 0.001, 'gangway ignores freeboard');
  }
  const railS = W.resolve('pier', { rail:'pipe', railSides:['water'] });
  const railP = W.placements(railS, W.frame(railS));
  for (const l of railP.ladders) for (const r of railP.rails)
    assert(r[2] <= l.x - W.FIT.ladder.access/2 || r[0] >= l.x + W.FIT.ladder.access/2, 'rail crosses ladder access');
  const coreS = W.resolve('breakwater', {}), faces = [];
  W.FAMILIES.riprap.build(faces, coreS, W.frame(coreS));
  assert(faces.some(f=>f.v.some(v => v[1] === 0 && Math.abs(v[2] - coreS.deckZ + 0.16) < 1e-6)), 'mound core misses crest');
  for(const base of W.MODULE_BASES) for(const part of ['Start','Middle','End']) {
    const key=base+part, g=W.gameplay(key,{}), m=g.module;
    assert.equal(m.negativeOpen,part!=='Start'); assert.equal(m.positiveOpen,part!=='End');
    assert.equal(m.positive[0]-m.negative[0],m.run);
    for(let dir=0;dir<8;dir++) {
      const left=W.project(dir,m.negative),right=W.project(dir,m.positive),step=W.project(dir,[m.run,0,0]);
      assert(Math.abs(right.x-left.x-step.x)<1e-6 && Math.abs(right.y-left.y-step.y)<1e-6,`${key}: socket projection`);
    }
    if(base.includes('Float')) assert.equal(W.resolve(key,{}).rock,false,'connected float rocks apart');
    const geom=W.geometry(key,{});
    for(const f of geom.faces) for(const [open,x] of [[m.negativeOpen,m.negative[0]],[m.positiveOpen,m.positive[0]]])
      if(open) assert(!f.v.every(v=>Math.abs(v[0]-x)<1e-7),`${key}: internal return cap`);
  }
  for(const base of W.MODULE_BASES.filter(b=>b!=='breakwater')) for(let dir=0;dir<8;dir++) {
    const m=W.gameplay(base+'Middle',{}).module;
    const items=['Start','Middle'].map((part,i)=>{
      const c=W.render(base+part,dir),p=W.project(dir,[i*m.run,0,0]);
      return {c,x:round(p.x-c.px),y:round(p.y-c.py)};
    });
    for(const y of [-m.depth/2+0.4,0,m.depth/2-0.4]) for(const dx of [-0.06,0,0.06]) {
      const p=W.project(dir,[m.run/2+dx,y,m.deckZ]);
      const covered=items.some(({c,x,y})=>{
        for(let oy=-1;oy<=1;oy++)for(let ox=-1;ox<=1;ox++){
          const sx=round(p.x)-x+ox,sy=round(p.y)-y+oy;
          if(sx>=0&&sy>=0&&sx<c.w&&sy<c.h&&c.data[(sy*c.w+sx)*4+3])return true;
        } return false;
      });
      assert(covered,`${base}/${dir}: visible deck gap at joining sockets`);
    }
  }
  const a = W.render('timberFloat', 3), b = W.render('timberFloat', 3);
  assert(Buffer.from(a.data).equals(Buffer.from(b.data)), 'render is not deterministic');
  for (let i=3;i<a.data.length;i+=4) assert(a.data[i] === 0 || a.data[i] === 255, 'non-binary alpha');
  if(mode==='--check'){
    const sha=crypto.createHash('sha256').update(fs.readFileSync(rigPath)).digest('hex');
    const side=JSON.parse(fs.readFileSync(path.dirname(rigPath)+'/gameplay/wharfIsoRig.gameplay.json','utf8'));
    assert.equal(contract.derivedFromRigSha256,sha,'stale bake contract hash');
    assert.equal(side.derivedFromRigSha256,sha,'stale gameplay sidecar hash');
    for(const key of W.list()) assert.equal(JSON.stringify(side.samples[key]),JSON.stringify(W.gameplay(key,{})),`${key}: stale gameplay sample`);
    for(const key of W.list()) assert.equal(JSON.stringify(side.families[key].dims),JSON.stringify(W.FAMILIES[key].dims),`${key}: stale dimensional limits`);
  }

  const results = [];
  for (const c of contract.cells) {
    const p = packed(c.key,c);
    const metaPath = `${assetDir}/${c.key}.png.meta`;
    let meta = fs.existsSync(metaPath) ? fs.readFileSync(metaPath,'utf8') : newMeta(c.key);
    const idBefore = [...meta.matchAll(/(?:spriteID|internalID): .+/g)].map(m=>m[0]);
    const next = { ...c, ...p.spec, module:W.gameplay(c.key,{}).module, sheet:{...c.sheet, sheetW:p.width, sheetH:p.height} };
    let slices = 0;
    meta = meta.replace(/(    - serializedVersion: 2\r?\n      name: ([^\r\n]+)\r?\n)([\s\S]*?)(?=    - serializedVersion: 2\r?\n      name:|    outline:)/g,
      (all, head, name, body) => {
        assert(name.startsWith(`${c.key}_`));
        const i = +name.slice(c.key.length+1); slices++;
        const x=i%c.sheet.cols*p.spec.cellW, y=p.height-(Math.floor(i/c.sheet.cols)+1)*p.spec.cellH;
        body=body.replace(/(        x:) [^\r\n]+/,`$1 ${x}`).replace(/(        y:) [^\r\n]+/,`$1 ${y}`)
          .replace(/(        width:) [^\r\n]+/,`$1 ${p.spec.cellW}`).replace(/(        height:) [^\r\n]+/,`$1 ${p.spec.cellH}`)
          .replace(/      pivot: \{[^}]+\}/,`      pivot: {x: ${p.spec.pivotX/p.spec.cellW}, y: ${1-p.spec.pivotY/p.spec.cellH}}`);
        return head+body;
      });
    assert.equal(slices,8*(c.rungs||1), `${c.key}: failed to locate every sprite slice`);
    assert.deepEqual([...meta.matchAll(/(?:spriteID|internalID): .+/g)].map(m=>m[0]),idBefore,'sprite identity changed');
    if(mode === '--check') {
      for(const k of ['cellW','cellH','pivotX','pivotY']) assert.equal(c[k],p.spec[k],`${c.key}: contract ${k}`);
      const png=decodePng(fs.readFileSync(`${assetDir}/${c.key}.png`));
      assert.equal(png.width,p.width); assert.equal(png.height,p.height);
      assert(Buffer.from(p.rgba).equals(png.rgba),`${c.key}: stale sprite sheet`);
      const originalMeta=fs.readFileSync(metaPath,'utf8');
      // Compare numeric slice data with tolerance for Unity's float serialization.
      const numbers = text => [...text.matchAll(/        (?:x|y|width|height): ([\d.]+)/g)].map(m=>+m[1]);
      assert.deepEqual(numbers(originalMeta),numbers(meta),`${c.key}: stale slice rectangles`);
      const pivots = text => [...text.matchAll(/      pivot: \{x: ([\d.]+), y: ([\d.]+)\}/g)].flatMap(m=>[+m[1],+m[2]]);
      pivots(originalMeta).forEach((v,i)=>assert(Math.abs(v-pivots(meta)[i])<1e-6,`${c.key}: stale pivot`));
    }
    results.push({c,next,p,meta,metaPath});
  }
  if(mode === '--bake') {
    // All keys have rendered and passed shape/cap/identity checks before writing any game asset.
    for(const {c,next,p,meta,metaPath} of results){
      const pngPath=`${assetDir}/${c.key}.png`, old=fs.existsSync(pngPath)?decodePng(fs.readFileSync(pngPath)):null;
      if(!old || old.width!==p.width || old.height!==p.height || !Buffer.from(p.rgba).equals(old.rgba))
        fs.writeFileSync(pngPath,encodePng(p.rgba,p.width,p.height));
      if(['cellW','cellH','pivotX','pivotY'].some(k=>next[k]!==c[k])) fs.writeFileSync(metaPath,meta);
      Object.assign(c,next);
    }
    const area=[...contract.cells].sort((a,b)=>b.sheet.sheetW*b.sheet.sheetH-a.sheet.sheetW*a.sheet.sheetH)[0];
    const longest=[...contract.cells].sort((a,b)=>Math.max(b.sheet.sheetW,b.sheet.sheetH)-Math.max(a.sheet.sheetW,a.sheet.sheetH))[0];
    contract.worstSheet={key:area.key,w:area.sheet.sheetW,h:area.sheet.sheetH};
    const maxDim=Math.max(longest.sheet.sheetW,longest.sheet.sheetH);
    contract.worstSheetByMaxDim={key:longest.key,w:longest.sheet.sheetW,h:longest.sheet.sheetH,maxDim,headroomToCap:contract.importSizeCap-maxDim};
    contract.generated='Measured by tools/wharf-review.mjs from all rendered facings and gangway rungs at defaults. Metres, 32 PPU, CCW sheet order, pivot-aligned buffer union. See docs/art/wharf-review-2026-09-13.md.';
    contract.count=contract.cells.length;
    contract.derivedFromRigSha256=crypto.createHash('sha256').update(fs.readFileSync(rigPath)).digest('hex');
    fs.writeFileSync(contractPath,JSON.stringify(contract,null,2)+'\n');
    const sidePath=path.dirname(rigPath)+'/gameplay/wharfIsoRig.gameplay.json';
    const side=JSON.parse(fs.readFileSync(sidePath,'utf8'));
    side.derivedFromRigSha256=contract.derivedFromRigSha256;
    side.fittings=W.FIT; side.deck=W.DECK; side.fittingDefaults=W.FIT_DEFAULT;
    side.presets=W.PRESETS; side.boats=W.BOATS;
    for(const key of W.list()) side.families[key].dims=W.FAMILIES[key].dims;
    side.modules=Object.fromEntries(W.MODULE_BASES.flatMap(base=>['Start','Middle','End'].map(part=>[base+part,W.gameplay(base+part,{}).module])));
    for(const key of W.list()) side.samples[key]=W.gameplay(key,{});
    for(const row of side.tideResponse.rows){
      const g=W.gameplay('pier',{tide:row.tide,tideRange:1.8,clearance:1,bays:4,bayLen:2.8,width:4.2});
      row.freeboard=g.freeboard; row.needsLadder=g.freeboard>1.2;
      row.rungsDry=g.ladders[0]?.rungsDry||0; row.footSubmerged=g.ladders[0]?.bottomSubmerged||false;
    }
    fs.writeFileSync(sidePath,JSON.stringify(side,null,1)+'\n');
  }
  console.log(`${mode}: ${contract.cells.length} sheets / ${contract.cells.reduce((n,c)=>n+8*(c.rungs||1),0)} cells; boat lengths, tide sweep, module sockets/caps, rigid floats, fender separation, berth fit, access, gangway freeboard, crest backing, determinism, 4096 cap and sprite identities passed.`);
} else {
  throw new Error('Use --preview [label], --baseline, --check or --bake');
}

#!/usr/bin/env node
/* Hidden Harbours — bird rig kit: the no-Unity build and check. Node 18+, built-ins only.

     node docs/art/rigs/birds/tools/build.cjs            check the kit; last line is RESULT: PASS or RESULT: FAIL
     node docs/art/rigs/birds/tools/build.cjs --write    regenerate sidecars, sheets and contracts first, then check
     node docs/art/rigs/birds/tools/build.cjs --plates <dir>   also write the preview plates into <dir>
     node docs/art/rigs/birds/tools/build.cjs --gull-rgba <file>   dump the herring gull's sheet as raw RGBA (the route's control)

   What it makes, per bird in BIRDS:
     gameplay/<rig>.gameplay.json      from <Global>.gameplayGeometry(), stamped with derivedFromRigSha256
     birds/sheets/<Name>.png           rows = facings 0..7, columns = sheetOrder(): the layout of the gull's sheet
     birds/sheets/<Name>.contract.json cell, pivot, order, convention, and sha256 of the sheet's RGBA

   The sheet is drawn by the rig's own render(), the same call the in-engine baker makes. The control for that
   claim is the herring gull: --gull-rgba draws main's gull through this route so it can be held against the
   sheet the game ships. */
'use strict';
const fs = require('fs'), path = require('path'), zlib = require('zlib'), crypto = require('crypto');

const RIGS = path.resolve(__dirname, '..', '..');           // docs/art/rigs
const KIT = path.resolve(__dirname, '..');                  // docs/art/rigs/birds
const BIRDS = [
  { name:'Cormorant', rig:'cormorantIsoRig.js', global:'CormorantIso', sprite:'cormorant' },
];
const GULL = { name:'Seagull', rig:'seagullIsoRig.js', global:'SeagullIso', sprite:'gull' };

const args = process.argv.slice(2), WRITE = args.includes('--write');
const argAfter = (f)=>{ const i=args.indexOf(f); return i>=0 ? args[i+1] : null; };

const sha256 = (buf)=>crypto.createHash('sha256').update(buf).digest('hex');
function load(b){ require(path.join(RIGS, b.rig)); const R=globalThis[b.global]; if(!R) throw new Error(b.rig+' did not set globalThis.'+b.global); return R; }

// ---- sheet ----------------------------------------------------------------------------------------
function sheetOf(R){
  const order=R.sheetOrder(), cols=order.length, Wc=R.W, Hc=R.H, w=cols*Wc, h=8*Hc, px=Buffer.alloc(w*h*4);
  for (let d=0; d<8; d++) for (let c=0; c<cols; c++){
    const cell=R.render(d, { anim:order[c].anim, frame:order[c].f });
    for (let y=0; y<Hc; y++){ const src=y*Wc*4, dst=((d*Hc+y)*w + c*Wc)*4; for (let i=0;i<Wc*4;i++) px[dst+i]=cell[src+i]; }
  }
  return { w, h, px, order, cols };
}

// ---- png (8-bit RGBA, filter 0) -------------------------------------------------------------------
const CRC = (()=>{ const t=new Uint32Array(256); for(let n=0;n<256;n++){ let c=n; for(let k=0;k<8;k++) c=c&1?0xEDB88320^(c>>>1):c>>>1; t[n]=c>>>0; } return t; })();
const crc32=(buf)=>{ let c=0xFFFFFFFF; for (const b of buf) c=CRC[(c^b)&255]^(c>>>8); return (c^0xFFFFFFFF)>>>0; };
function png(w,h,px){
  const raw=Buffer.alloc((w*4+1)*h); for (let y=0;y<h;y++){ raw[y*(w*4+1)]=0; px.copy(raw, y*(w*4+1)+1, y*w*4, (y+1)*w*4); }
  const chunk=(type,data)=>{ const len=Buffer.alloc(4); len.writeUInt32BE(data.length); const td=Buffer.concat([Buffer.from(type,'ascii'),data]);
    const crc=Buffer.alloc(4); crc.writeUInt32BE(crc32(td)); return Buffer.concat([len,td,crc]); };
  const ihdr=Buffer.alloc(13); ihdr.writeUInt32BE(w,0); ihdr.writeUInt32BE(h,4); ihdr[8]=8; ihdr[9]=6;
  return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]), chunk('IHDR',ihdr), chunk('IDAT',zlib.deflateSync(raw,{level:9})), chunk('IEND',Buffer.alloc(0))]);
}

// ---- generated files ------------------------------------------------------------------------------
function sidecarText(b, R){
  const rigSha=sha256(fs.readFileSync(path.join(RIGS,b.rig))), g=R.gameplayGeometry(), out={};
  for (const k of Object.keys(g)){ out[k]=g[k]; if (k==='exportSymbol') out.derivedFromRigSha256=rigSha; }
  return JSON.stringify(out,null,2)+'\n';
}
function contractText(b, R, sh){
  const rigSha=sha256(fs.readFileSync(path.join(RIGS,b.rig)));
  return JSON.stringify({
    sheet:'docs/art/rigs/birds/sheets/'+b.name+'.png', rig:'docs/art/rigs/'+b.rig, global:b.global, derivedFromRigSha256:rigSha,
    engine:'V8 (Node, docs/art/rigs/birds/tools/build.cjs)', sheetRgbaSha256:sha256(sh.px),
    cell:{ w:R.W, h:R.H }, pivotTopLeft:{ x:R.pivot.x, y:R.pivot.y }, pivotNormalisedBottomLeft:{ x:R.pivot.x/R.W, y:(R.H-R.pivot.y)/R.H },
    rows:8, columns:sh.cols, pixelsPerUnit:32, requiredMaxTextureSize:4096,
    rigConvention:'Clockwise', facingsAreCounterClockwise:false, spriteName:b.sprite+'_<anim>_<frame>_d<row>',
    order:sh.order.map((o,i)=>({ col:i, anim:o.anim, frame:o.f })),
    note:'Row r is facing r (dir 0 = bill toward north, +45 deg per row, CLOCKWISE: the herring gull\'s convention, declared by the rig and not yet measured in-engine). The pivot is a CONTACT POINT: rock or perch top when standing, the water surface when swimming, the point directly below the bird when airborne. Altitude is a screen offset, never a scale.',
    wetStates:Object.keys(R.POSE).filter(a=>R.POSE[a].some(p=>p.waterZ!==undefined)).join(', ')+' carry waterZ:0 in their own pose and are baked WET.',
  },null,2)+'\n';
}

// ---- checks ---------------------------------------------------------------------------------------
const fails=[], notes=[];
const ok=(cond,msg)=>{ if(!cond) fails.push(msg); };
function checkBird(b){
  const R=load(b), sh=sheetOf(R);
  const files={ sidecar:path.join(RIGS,'gameplay',b.rig.replace(/\.js$/,'.gameplay.json')), sheet:path.join(KIT,'sheets',b.name+'.png'), contract:path.join(KIT,'sheets',b.name+'.contract.json') };
  const want={ sidecar:sidecarText(b,R), contract:contractText(b,R,sh) };
  if (WRITE){ fs.mkdirSync(path.dirname(files.sheet),{recursive:true}); fs.writeFileSync(files.sidecar,want.sidecar); fs.writeFileSync(files.contract,want.contract); fs.writeFileSync(files.sheet,png(sh.w,sh.h,sh.px)); }
  const rigBytes=fs.readFileSync(path.join(RIGS,b.rig));
  ok(!rigBytes.includes(13), b.rig+': has CR bytes (must be LF: the sidecar hashes the working-tree bytes)');
  for (const k of ['sidecar','contract']){ const have=fs.existsSync(files[k]) ? fs.readFileSync(files[k],'utf8') : null;
    ok(have===want[k], path.basename(files[k])+': not what the rig generates now (run --write)'); }
  ok(fs.existsSync(files.sheet), b.name+'.png: missing');
  if (fs.existsSync(files.sheet)){ const have=fs.readFileSync(files.sheet);
    if (!have.equals(png(sh.w,sh.h,sh.px))) notes.push(b.name+'.png: bytes differ from this Node\'s encoding (zlib may differ); its pixels are held by sheetRgbaSha256 in the contract'); }
  // the tables agree with each other
  const g=R.gameplayGeometry();
  ok(g.schema==='hidden-harbours/creature-gameplay@1', b.name+': sidecar schema');
  ok(JSON.stringify(R.AORDER.slice().sort())===JSON.stringify(Object.keys(R.ANIMS).sort()), b.name+': AORDER and ANIMS name different states');
  for (const a of R.AORDER){ ok(R.POSE[a] && R.POSE[a].length===R.ANIMS[a].n, b.name+'.'+a+': POSE frames != ANIMS.n'); ok(!!R.MOTION[a], b.name+'.'+a+': no MOTION'); }
  const drawn=new Set(R.AORDER), frameless=new Set(['under']);
  for (const [from,to] of g.TRANSITIONS){ ok(drawn.has(from)||frameless.has(from), b.name+': transition from unknown state '+from); ok(drawn.has(to)||frameless.has(to), b.name+': transition to unknown state '+to); }
  for (const a of R.AORDER){ const m=R.MOTION[a]; if (m.next) ok(drawn.has(m.next)||frameless.has(m.next), b.name+'.'+a+': next names unknown state '+m.next);
    ok(g.TRANSITIONS.some(t=>t[1]===a), b.name+'.'+a+': no way in'); ok(g.TRANSITIONS.some(t=>t[0]===a), b.name+'.'+a+': no way out'); }
  // every cell holds a bird, and no cell is clipped by its edge
  let minPx=1e9, clipped=0;
  for (let d=0;d<8;d++) for (let c=0;c<sh.cols;c++){ let n=0, edge=0;
    for (let y=0;y<R.H;y++) for (let x=0;x<R.W;x++){ if (sh.px[((d*R.H+y)*sh.w + c*R.W+x)*4+3]){ n++; if (x===0||y===0||x===R.W-1||y===R.H-1) edge++; } }
    minPx=Math.min(minPx,n); if (edge){ clipped++; fails.push(b.name+': '+sh.order[c].anim+' f'+sh.order[c].f+' d'+d+' touches the cell edge ('+edge+' px)'); } }
  ok(minPx>=6, b.name+': a cell has only '+minPx+' px drawn');
  // strict world scale: the open wing measures the declared span, and it is the same at every facing's own axis
  const a0=R.anchors(0,{anim:'glide',frame:0}), spanPx=Math.abs(a0.wingR.dx-a0.wingL.dx);
  ok(Math.abs(spanPx/32-R.BODY.wingspan_m)<=0.10, b.name+': glide span '+(spanPx/32).toFixed(2)+' m, declared '+R.BODY.wingspan_m+' m');
  console.log(b.name+': '+sh.cols+' columns x 8 rows, '+sh.w+'x'+sh.h+' px, min '+minPx+' px in a cell, glide span '+spanPx+' px, rgba '+sha256(sh.px).slice(0,12));
  return { R, sh };
}

// ---- plates ---------------------------------------------------------------------------------------
function Canvas(w,h,fill){ const px=Buffer.alloc(w*h*4); for(let i=0;i<w*h;i++){ px[i*4]=fill[0]; px[i*4+1]=fill[1]; px[i*4+2]=fill[2]; px[i*4+3]=255; } return { w,h,px }; }
function rect(c,x0,y0,x1,y1,rgb){ for(let y=Math.max(0,y0);y<Math.min(c.h,y1);y++) for(let x=Math.max(0,x0);x<Math.min(c.w,x1);x++){ const i=(y*c.w+x)*4; c.px[i]=rgb[0]; c.px[i+1]=rgb[1]; c.px[i+2]=rgb[2]; } }
function blit(c,R,dir,opts,px,py){ const cell=R.render(dir,opts), ox=px-R.pivot.x, oy=py-R.pivot.y;
  for(let y=0;y<R.H;y++) for(let x=0;x<R.W;x++){ const a=cell[(y*R.W+x)*4+3]/255; if(!a) continue; const X=ox+x, Y=oy+y; if(X<0||Y<0||X>=c.w||Y>=c.h) continue;
    const i=(Y*c.w+X)*4, s=(y*R.W+x)*4; for(let k=0;k<3;k++) c.px[i+k]=Math.round(cell[s+k]*a+c.px[i+k]*(1-a)); } }
function shadow(c,px,py,rw){ for(let x=-rw;x<=rw;x++) for(let y=-1;y<=1;y++){ if ((x*x)/(rw*rw)+(y*y)/2.2>1) continue; const X=px+x,Y=py+y; if(X<0||Y<0||X>=c.w||Y>=c.h) continue; const i=(Y*c.w+X)*4; for(let k=0;k<3;k++) c.px[i+k]=Math.round(c.px[i+k]*0.72); } }
function scaleUp(c,k){ const o=Buffer.alloc(c.w*k*c.h*k*4); for(let y=0;y<c.h*k;y++) for(let x=0;x<c.w*k;x++) c.px.copy(o,(y*c.w*k+x)*4,((y/k|0)*c.w+(x/k|0))*4,((y/k|0)*c.w+(x/k|0))*4+4); return { w:c.w*k, h:c.h*k, px:o }; }
const ROCK=[112,108,100], ROCK2=[96,93,88], SEA=[52,104,122], SEA2=[46,95,113];
function plates(dir, bird){
  fs.mkdirSync(dir,{recursive:true});
  const G=load(GULL), C=bird.R, name=bird.b.name.toLowerCase(), out=[];
  const save=(file,c,k)=>{ const s=scaleUp(c,k); fs.writeFileSync(path.join(dir,file),png(s.w,s.h,s.px)); out.push(file); };
  // 1. beside the gull, at game scale: ashore on rock, afloat, and in the air
  for (const [tag,dirs] of [['side',[2,6]],['quarter',[3,5]],['fore-aft',[0,4]]]){
    const c=Canvas(560, 300, SEA);
    rect(c,0,0,560,100,ROCK); for(let x=0;x<560;x+=7) rect(c,x,96+(x*13%5),x+4,100,ROCK2);
    for(let y=104;y<300;y+=9) for(let x=(y*7)%23;x<560;x+=23) rect(c,x,y,x+6,y+1,SEA2);
    const rows=[
      { y:44, d:dirs[0], items:[[G,'stand',0],[G,'perch',0],[C,'stand',0],[C,'stand',1],[C,'perch',0],[C,'dry',0],[C,'dry',2],[C,'takeoff',0],[C,'land',2]] },
      { y:90, d:dirs[1], items:[[G,'stand',0],[G,'perch',0],[C,'stand',0],[C,'stand',1],[C,'perch',0],[C,'dry',0],[C,'dry',2],[C,'takeoff',0],[C,'land',2]] },
      { y:150, d:dirs[0], items:[[G,'float',0],[C,'float',0],[C,'float',1],[C,'dive',0],[C,'dive',1],[C,'dive',2],[C,'dive',3],[C,'surface',0],[C,'surface',1],[C,'surface',2]] },
      { y:196, d:dirs[1], items:[[G,'float',0],[C,'float',0],[C,'float',1],[C,'dive',0],[C,'dive',1],[C,'dive',2],[C,'dive',3],[C,'surface',0],[C,'surface',1],[C,'surface',2]] },
      { y:268, d:dirs[0], alt:26, items:[[G,'fly',1],[G,'glide',0],[C,'fly',1],[C,'fly',4],[C,'glide',0],[C,'patter',0,0],[C,'patter',1,0],[C,'splash',0,0],[C,'splash',1,0]] },
    ];
    for (const r of rows){ const step=Math.floor(540/r.items.length); r.items.forEach((it,i)=>{ const x=30+i*step, alt=it[3]!=null?it[3]:(r.alt||0);
      if (alt) shadow(c,x,r.y,6); blit(c,it[0],r.d,{anim:it[1],frame:it[2]},x,r.y-alt); }); }
    save(name+'-beside-gull-'+tag+'-x3.png', c, 3);
    if (tag==='side') save(name+'-beside-gull-'+tag+'-x1.png', c, 1);
  }
  // 2. the whole sheet over sea colour, so the wet alpha reads
  const sh=bird.sh, c2=Canvas(sh.w, sh.h, SEA);
  for (let d=0;d<8;d++) sh.order.forEach((o,cI)=>blit(c2,C,d,{anim:o.anim,frame:o.f},cI*C.W+C.pivot.x,d*C.H+C.pivot.y));
  save(name+'-sheet-on-sea-x2.png', c2, 2);
  // 3. the signature: the spread-wing roost, all eight facings, with a gull for size
  const c3=Canvas(8*56+70, 76, ROCK); for(let x=0;x<c3.w;x+=7) rect(c3,x,72+(x*13%4),x+4,76,ROCK2);
  blit(c3,G,2,{anim:'stand',frame:0},30,56); for (let d=0;d<8;d++) blit(c3,C,d,{anim:'dry',frame:0},90+d*56,56);
  save(name+'-drying-8-facings-x4.png', c3, 4);
  return out;
}

// ---- main -----------------------------------------------------------------------------------------
const gullOut=argAfter('--gull-rgba');
if (gullOut){ const sh=sheetOf(load(GULL)); fs.writeFileSync(gullOut, sh.px); console.log('gull sheet '+sh.w+'x'+sh.h+' rgba sha256 '+sha256(sh.px)); process.exit(0); }
const built=BIRDS.map(b=>Object.assign({ b }, checkBird(b)));
const plateDir=argAfter('--plates');
if (plateDir) for (const x of built) console.log('plates: '+plates(path.resolve(plateDir), x).join(', '));
for (const n of notes) console.log('NOTE: '+n);
for (const f of fails) console.log('FAIL: '+f);
console.log('RESULT: '+(fails.length?'FAIL ('+fails.length+')':'PASS'));
process.exit(fails.length?1:0);

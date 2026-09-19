import fs from 'node:fs';
import vm from 'node:vm';
import {encodePng} from './rig-recipes/lib/png.mjs';
const host=vm.createContext({});
vm.runInContext(fs.readFileSync('docs/art/rigs/iso-rig-pack/wharf-kit-iso/wharfIsoRig.js','utf8'),host);
const W=host.WharfIso;
vm.runInContext(fs.readFileSync('docs/art/rigs/characterIsoRig.js','utf8'),host);
vm.runInContext(fs.readFileSync('docs/art/rigs/doryIsoRig.js','utf8'),host);
const C=host.CharacterIso,D=host.DoryIso;
const dir=+(process.argv[2]||1),tiles=[];
for(const base of W.MODULE_BASES){
  const run=W.gameplay(base+'Middle',{}).module.run;
  const items=['Start','Middle','End'].map((part,i)=>{
    const c=W.render(base+part,dir),p=W.project(dir,[(i-1)*run,0,0]);
    return {c,x:Math.round(p.x-c.px),y:Math.round(p.y-c.py),depth:p.y};
  });
  const hp=W.project(dir,[0,0,W.gameplay(base+'Middle',{}).deckZ]);
  items.push({c:{w:C.W,h:C.H,data:C.render(dir,{})},x:Math.round(hp.x-C.pivot.x),y:Math.round(hp.y-C.pivot.y),depth:Infinity});
  if(base==='timberFloat'){
    const bp=W.project(dir,[0,W.gameplay(base+'Middle',{}).module.depth/2+2.4,2.42]);
    items.push({c:{w:D.W,h:D.H,data:D.render((dir+2)%8,{})},x:Math.round(bp.x-D.pivot.x),y:Math.round(bp.y-D.pivot.y),depth:Infinity});
  }
  const x0=Math.min(...items.map(i=>i.x)),y0=Math.min(...items.map(i=>i.y));
  const w=Math.max(...items.map(i=>i.x+i.c.w))-x0,h=Math.max(...items.map(i=>i.y+i.c.h))-y0;
  tiles.push({base,items:items.sort((a,b)=>a.depth-b.depth),x0,y0,w,h});
}
const width=Math.max(...tiles.map(t=>t.w))+80,height=tiles.reduce((sum,t)=>sum+t.h+64,0);
const data=new Uint8Array(width*height*4);
for(let i=0;i<data.length;i+=4)data.set([21,37,44,255],i);
let row=32;
for(const t of tiles){
  for(const {c,x,y} of t.items)for(let sy=0;sy<c.h;sy++)for(let sx=0;sx<c.w;sx++){
    const src=(sy*c.w+sx)*4;if(!c.data[src+3])continue;
    const dx=Math.round((width-t.w)/2)+x-t.x0+sx,dy=row+y-t.y0+sy;
    data.set(c.data.subarray(src,src+4),(dy*width+dx)*4);
  }
  row+=t.h+64;
}
fs.mkdirSync('Temp/wharf-review',{recursive:true});
fs.writeFileSync(`Temp/wharf-review/modules-${dir}.png`,encodePng(data,width,height));
console.log(`Top to bottom: ${tiles.map(t=>t.base).join(', ')}. Direction ${dir}, 32 pixels/metre, start + middle + end.`);

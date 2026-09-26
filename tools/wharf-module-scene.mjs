// Narrow, repeatable migration of the observed NMC structural sprite references.
// Never changes transforms, components, object IDs, colliders, or other scene content.
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
const root=path.resolve(process.argv[3]||'.');
const scenePath=path.join(root,'Assets/_Project/Scenes/NineMileCreek.unity');
const text=fs.readFileSync(scenePath,'utf8');
const hash=s=>crypto.createHash('sha256').update(s).digest('hex');
const dir='Assets/_Project/Art/Sprites/Wharf/Iso';
const contract=JSON.parse(fs.readFileSync(`${dir}/wharfIsoRig.contract.json`,'utf8'));
const references=new Map();
for(const c of contract.cells){
  const meta=fs.readFileSync(`${dir}/${c.key}.png.meta`,'utf8'),guid=meta.match(/^guid: (\w+)/m)[1];
  for(const m of meta.matchAll(/      name: (\w+)_(\d+)\r?\n[\s\S]*?      internalID: (-?\d+)/g))
    references.set(`${guid}/${m[3]}`,{key:c.key,facing:+m[2],guid,id:m[3],module:c.module});
}
const docs=[...text.matchAll(/--- !u!(\d+) &(\d+)[^\n]*\n([\s\S]*?)(?=--- !u!|$)/g)];
const gos=new Map(),transforms=new Map(),sprites=new Map();
for(const [_,type,id,b] of docs){
  const go=b.match(/m_GameObject: \{fileID: (\d+)/)?.[1];
  if(type==='1')gos.set(id,b.match(/m_Name: (.*)/)?.[1].trim());
  if(type==='4')transforms.set(go,{parent:b.match(/m_Father: \{fileID: (\d+)/)?.[1],
    p:b.match(/m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)/)?.slice(1).map(Number),
    scale:b.match(/m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)/)?.slice(1).map(Number)});
  if(type==='212'){
    const r=b.match(/m_Sprite: \{fileID: (-?\d+), guid: (\w+), type: 3\}/);
    if(r)sprites.set(go,{docId:id,old:r[0],ref:references.get(`${r[2]}/${r[1]}`)});
  }
}
const groups=new Map();
for(const [go,name] of gos){
  if(!name)continue; // Stripped prefab records carry no local name.
  const face=name.match(/^(NorthWall|WestWall|ApronWest|ApronSouth|QuayHead|Breakwater)_logCrib(?:Start|Middle|End)?_(\d+)$/);
  const float=name.match(/^FloatBay_(\d+)$/);
  if(!face&&!float)continue;
  const group=face?face[1]:'FloatBay',base=face?'logCrib':'timberFloat';
  const sprite=sprites.get(go),t=transforms.get(go);
  assert(sprite?.ref && t,`${name}: missing observed sprite/transform`);
  assert.equal(sprite.ref.module.base,base,`${name}: unexpected construction`);
  assert(t.scale.every(v=>v===1),`${name}: scaled section requires editor review`);
  if(!groups.has(group))groups.set(group,[]);
  groups.get(group).push({name,base,sprite,t,index:+(face?face[2]:float[1])});
}
assert.equal(groups.size,7,'Expected six quay runs and one float run; review a changed layout before migration');
const replacements=new Map(),report=[];
for(const [name,items] of groups){
  items.sort((a,b)=>a.index-b.index); assert(items.length>=2);
  const facing=items[0].sprite.ref.facing,angle=facing*Math.PI/4;
  const unit=[Math.cos(angle),-Math.sin(angle)*contract.projection.depthScale];
  const delta=items[1].t.p.map((v,i)=>v-items[0].t.p[i]);
  const forward=unit[0]*delta[0]+unit[1]*delta[1]>=0;
  const run=items[0].sprite.ref.module.run,drawn=run*Math.hypot(...unit);
  let maxGap=-Infinity;
  for(let i=0;i<items.length;i++){
    const item=items[i];assert.equal(item.sprite.ref.facing,facing);assert.equal(item.t.parent,items[0].t.parent);
    if(i){const step=item.t.p.map((v,j)=>v-items[i-1].t.p[j]);
      assert(Math.abs(step[0]*unit[1]-step[1]*unit[0])<0.001,`${name}: bent run`);
      maxGap=Math.max(maxGap,Math.hypot(step[0],step[1])-drawn);
    }
    const j=forward?i:items.length-1-i;
    const key=item.base+(j===0?'Start':j===items.length-1?'End':'Middle');
    const next=[...references.values()].find(r=>r.key===key&&r.facing===facing);assert(next);
    replacements.set(item.sprite.docId,{old:item.sprite.old,next:`m_Sprite: {fileID: ${next.id}, guid: ${next.guid}, type: 3}`});
  }
  assert(maxGap<=1/32,`${name}: actual placement gap ${maxGap} requires rebuilding the run`);
  report.push({name,sections:items.length,facing,projectedRun:drawn,maxGap,first:forward?'Start':'End'});
}
let changed=0;
const next=text.replace(/--- !u!(\d+) &(\d+)[^\n]*\n[\s\S]*?(?=--- !u!|$)/g,(doc,type,id)=>{
  const r=type==='212'?replacements.get(id):null;if(!r||r.old===r.next)return doc;
  assert(doc.includes(r.old));changed++;return doc.replace(r.old,r.next);
});
if(process.argv[2]==='--apply'&&changed){
  assert.equal(hash(fs.readFileSync(scenePath,'utf8')),hash(text),'Scene changed during audit');
  const backup=path.join(root,'Temp/wharf-review');fs.mkdirSync(backup,{recursive:true});
  fs.writeFileSync(path.join(backup,`NineMileCreek-${hash(text).slice(0,12)}.unity`),text);
  fs.writeFileSync(scenePath,next);
}
console.log(JSON.stringify({mode:process.argv[2]||'--check',scenePath,changed,runs:report}));

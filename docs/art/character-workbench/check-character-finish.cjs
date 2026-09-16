// Focused no-Unity geometry guard for the per-preset body/material tailoring.
const fs=require('fs'),path=require('path'),assert=require('assert'),crypto=require('crypto');
const {loadStudy}=require('./load-study.cjs'),{context:c}=loadStudy(),E=c.CastViewerEngine,F=c.CharacterFinish;
const plain=x=>JSON.parse(JSON.stringify(x)),outDir=path.resolve(__dirname,'../../../artifacts/character-beauty-pass');
const sourceHash=file=>crypto.createHash('sha256').update(fs.readFileSync(path.join(__dirname,file),'utf8').replaceAll('\r\n','\n')).digest('hex');
fs.mkdirSync(outDir,{recursive:true});
let poseSamples=0,protectedCorners=0,maxAnchorDelta=0,maxProtectedDelta=0,negativeChecks=0;const rows=[];
for(const key of E.cast){
  const base=E.create(key,{finish:false}),finished=E.create(key),bind=base.bind;
  const source=key==='fisher'?c.CharacterArtStudy.create({build:base.build,skeletonWorld:c.CharacterIso7.skeletonWorld(base.build),bindMesh:bind.faces}):bind.faces;
  const sourceBefore=plain(source),bindBefore=plain(bind),buildBefore=plain(base.build),transformed=F.body(source,base.build,bind);
  assert.deepStrictEqual(plain(source),sourceBefore,key+' mutated source mesh');assert.deepStrictEqual(plain(bind),bindBefore,key+' mutated bind skeleton');
  assert.deepStrictEqual(plain(base.build),buildBefore,key+' mutated identity');assert.strictEqual(transformed.length,source.length,key+' changed face topology');
  const regions={},protectedCounts={};let maxBindDelta=0;
  for(let i=0;i<source.length;i++){
    const before=source[i],after=transformed[i];assert.strictEqual(after.part,before.part);assert.strictEqual(after.v.length,before.v.length);
    assert.deepStrictEqual(plain(after.bone),plain(before.bone),key+' changed bone weights');
    if(F.protectedPart(before.part)){assert.deepStrictEqual(plain(after),plain(before),key+' edited contact/identity geometry '+before.part);protectedCounts[before.part]=(protectedCounts[before.part]||0)+before.v.length;}
    else for(let j=0;j<before.v.length;j++){
      assert(after.v[j].every(Number.isFinite),key+' has nonfinite bind vertex');const d=Math.hypot(...after.v[j].map((x,k)=>x-before.v[j][k]));
      maxBindDelta=Math.max(maxBindDelta,d);if(d>1e-10)regions[before.part]=Math.max(regions[before.part]||0,d);
    }
  }
  for(const extreme of [Math.min,Math.max]){
    const a=extreme(...source.flatMap(f=>f.v.map(p=>p[2]))),b=extreme(...transformed.flatMap(f=>f.v.map(p=>p[2])));
    assert.strictEqual(b,a,key+' body tailoring changed figure height');
  }
  const allowed=new Set(Object.keys({...c.CharacterFinishConfig.CommonMaterials,...c.CharacterFinishConfig.Presets[key].Materials}));
  for(const name of Object.keys(base.mats))if(!allowed.has(name))assert.deepStrictEqual(plain(finished.mats[name]),plain(base.mats[name]),key+' altered unrelated material '+name);
  assert.deepStrictEqual(plain(finished.colours),plain(base.colours),key+' altered face/eye colours');
  assert.deepStrictEqual(plain(finished.headBuild),plain(base.headBuild),key+' altered face identity');
  assert.deepStrictEqual(plain(finished.bind),plain(base.bind),key+' changed exported bind/rest/bone structure');
  assert.strictEqual(finished.faces.length,base.faces.length,key+' added faces');
  for(const anim of Object.keys(E.animations))for(let sample=0;sample<=12;sample++){
    const a=E.pose(base,anim,sample/12),b=E.pose(finished,anim,sample/12);poseSamples++;
    assert.deepStrictEqual(plain(b.bones),plain(a.bones),key+'/'+anim+' changed animation transforms');
    for(let i=0;i<a.bones.length;i++)if(/^(hand_|foot_|tool_|carry_)/.test(a.bones[i].id))for(let k=0;k<3;k++)maxAnchorDelta=Math.max(maxAnchorDelta,Math.abs(a.bones[i].p[k]-b.bones[i].p[k]));
    for(let i=0;i<b.faces.length;i++)for(let j=0;j<b.faces[i].v.length;j++){
      const p=b.faces[i].v[j];assert(p.every(Number.isFinite),key+'/'+anim+' nonfinite pose vertex');assert(Math.hypot(...p)<4,key+'/'+anim+' escaped pose envelope');
      if(F.protectedPart(b.faces[i].part)){
        protectedCorners++;for(let k=0;k<3;k++)maxProtectedDelta=Math.max(maxProtectedDelta,Math.abs(p[k]-a.faces[i].v[j][k]));
      }
    }
  }
  const oldMaterials=new Set(base.faces.map(f=>f.mat)),newMaterials=new Set(finished.faces.map(f=>f.mat));
  assert(newMaterials.size<=oldMaterials.size,key+' increased source material count');
  rows.push({Preset:key,Garment:base.build.garment,Age:base.build.age,Sex:base.build.sex,Bones:bind.bones.length,Faces:finished.faces.length,
    Vertices:finished.faces.reduce((n,f)=>n+f.v.length,0),MaxBindDisplacementMetres:maxBindDelta,ChangedParts:regions,ProtectedCornersByPart:protectedCounts,
    UsedGeometryMaterials:newMaterials.size,GarmentMaterialKeys:[...allowed].filter(k=>base.mats[k])});
}
assert.strictEqual(maxAnchorDelta,0);assert.strictEqual(maxProtectedDelta,0);
const fisher=E.create('fisher',{finish:false}),source=fisher.bind.faces;
for(const build of [{...fisher.build,preset:'missing'},{...fisher.build,age:'child'},{...fisher.build,garment:'skirt'},{...fisher.build,height:1.1}]){
  assert.throws(()=>F.body(source,build,fisher.bind),/unsupported/);negativeChecks++;
}
const swapped=plain(fisher.bind);[swapped.bones[8],swapped.bones[9]]=[swapped.bones[9],swapped.bones[8]];
assert.throws(()=>F.body(source,fisher.build,swapped),/bone layout/);negativeChecks++;
const config=c.CharacterFinishConfig.Presets.fisher,oldScale=config.UpperRadius;config.UpperRadius=2;
try{assert.throws(()=>F.body(source,fisher.build,fisher.bind),/out-of-range/);negativeChecks++;}finally{config.UpperRadius=oldScale;}
config.Materials.skin='ochre';try{assert.throws(()=>F.materials(fisher.mats,fisher.build),/identity material/);negativeChecks++;}finally{delete config.Materials.skin;}
const report={Scope:'Review-only body/material tailoring; original production rigs untouched. Bone/mesh anchor invariants are not environment contact acceptance.',
  SourceHashes:Object.fromEntries(['character-finish.js','character-finish.json','check-character-finish.cjs'].map(f=>[f,sourceHash(f)])),
  Presets:rows,PoseSamples:poseSamples,ProtectedPosedCorners:protectedCorners,MaxAnchorDeltaMetres:maxAnchorDelta,MaxProtectedGeometryDeltaMetres:maxProtectedDelta,NegativeChecks:negativeChecks,
  Guarantees:'No added faces; original weights, bone IDs, rest/pose transforms, physical body height, head/neck/hands/thumbs/feet geometry, skin/hair/eye material values and face identity stay exact. Separate 44/45/46-bone source layouts; no cross-preset fit claim.',
  Limits:'Material counts retain the existing source names; several cast presets still need production material consolidation. These checks do not establish collision-free cloth, runtime shader parity or boat/chair/bed/tool contact in Unity.'};
fs.writeFileSync(path.join(outDir,'body-finish-checks.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({Passed:true,Presets:rows.length,PoseSamples:poseSamples,MaxAnchorDeltaMetres:maxAnchorDelta,MaxProtectedGeometryDeltaMetres:maxProtectedDelta,NegativeChecks:negativeChecks,
  MaxBindDisplacementMetres:Math.max(...rows.map(r=>r.MaxBindDisplacementMetres)),ChangedRegions:Object.fromEntries(rows.map(r=>[r.Preset,Object.keys(r.ChangedParts)]))}));

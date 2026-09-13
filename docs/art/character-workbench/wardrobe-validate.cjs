// Reproducible no-Unity measurement of the bounded wardrobe authoring proof.
const fs=require('fs'),path=require('path'),vm=require('vm'),crypto=require('crypto'),assert=require('assert');
const {performance}=require('perf_hooks'),{loadStudy}=require('./load-study.cjs'),{png}=require('./sources/face-render.cjs');
const {applyBootSegmentCorrection}=require('./boot-segment-fix.cjs');
const dir=__dirname,read=name=>JSON.parse(fs.readFileSync(path.join(dir,name),'utf8'));
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file,'utf8').replaceAll('\r\n','\n')).digest('hex');
const fit=read('wardrobe-fit-fisher.json');
const effectiveRigSha256=crypto.createHash('sha256').update(applyBootSegmentCorrection(fs.readFileSync(path.join(dir,'sources/characterIsoRig7.js'),'utf8'))).digest('hex');
assert.strictEqual(fit.SourceRigSha256,hash(path.join(dir,'sources/characterIsoRig7.js')),'Fit source pin is stale; review the changed source before updating it');
assert.strictEqual(fit.SourceRigSha256,hash(path.join(dir,'../rigs/characterIsoRig7.js')),'Production solver moved; review the preview snapshot before updating it');
const catalog={SchemaVersion:1,Fits:[fit],Garments:fs.readdirSync(dir).filter(f=>/^wardrobe-garment-.*\.json$/.test(f)).sort().map(read)};
fs.writeFileSync(path.join(dir,'wardrobe-catalog.json'),JSON.stringify(catalog,null,2)+'\n');
const {context:c}=loadStudy();
c.CharacterWardrobeCatalog=catalog;c.CharacterWardrobeRecipes=read('wardrobe-recipes.json');
vm.runInContext(fs.readFileSync(path.join(dir,'wardrobe-assembly.js'),'utf8'),c);
const E=c.CastViewerEngine,W=c.CharacterWardrobeProof,base=E.create('fisher');
const plain=x=>JSON.parse(JSON.stringify(x)),sections=W.sections(base),rows=[],negative=[];
function expectFail(label,fn,pattern){assert.throws(fn,pattern,label);negative.push(label);}
function measure(faces){
  const vertices=faces.reduce((n,f)=>n+f.v.length,0),triangles=faces.reduce((n,f)=>n+f.v.length-2,0);
  const used=new Set(),materials=new Set();let influences=0;
  for(const f of faces){materials.add(f.mat);for(const ws of f.bone){assert(ws.length<=2);assert(Math.abs(ws.reduce((s,x)=>s+x[1],0)-1)<1e-8);influences=Math.max(influences,ws.length);for(const [i] of ws)used.add(base.bind.bones[i].id);}}
  // Same conservative accounting as RigMeshBuilder and CharacterSkinAssetBaker:
  // position 12 + flat normal 12 + UV0 attributes 16 + legacy BoneWeight 32,
  // plus 32-bit index accounting although this mesh fits a 16-bit index buffer.
  return {Faces:faces.length,Vertices:vertices,Triangles:triangles,UsedBoneIds:[...used].sort(),MaxInfluences:influences,
    UsedMaterialIds:[...materials].sort(),ConservativeMeshBytes:vertices*72+triangles*3*4,BindPoseBytes:base.bind.bones.length*64};
}
let poseSamples=0,maxRadius=0,maxHeadDelta=0,renderChecks=0;
for(const p of W.presets){
  const character=W.create(base,p.Recipe),measurements=measure(character.faces),permuted=plain(p.Recipe);
  permuted.Garments.reverse();assert.deepStrictEqual(plain(W.create(base,permuted).faces),plain(character.faces),'Selection order must not alter assembled geometry');
  assert.deepStrictEqual(plain(character.build),plain(base.build));assert.deepStrictEqual(plain(character.headBuild),plain(base.headBuild));
  assert.deepStrictEqual(plain(character.colours),plain(base.colours));
  assert.deepStrictEqual(plain(character.faces.filter(f=>f.part==='head')),plain(base.faces.filter(f=>f.part==='head')),'Clothing must preserve identity head verbatim');
  assert(character.wardrobe.UsedMaterials.length<=16);
  const timings=[];for(let i=0;i<120;i++){const t=performance.now();W.create(base,p.Recipe);if(i>=20)timings.push(performance.now()-t);}timings.sort((a,b)=>a-b);
  const a=character.faces.filter(f=>!f.wardrobeGarmentId),expected=sections.identity.filter(f=>!character.wardrobe.CoverageTags.includes(W.coverageTag(f)));
  assert.deepStrictEqual(plain(a),plain(expected),'Coverage may hide exactly its tagged identity faces');
  for(const anim of Object.keys(E.animations))for(let i=0;i<=12;i++){
    const pose=E.pose(character,anim,i/12),original=E.pose(base,anim,i/12);poseSamples++;
    const head=pose.faces.filter(f=>f.part==='head'),oldHead=original.faces.filter(f=>f.part==='head');
    head.forEach((f,j)=>f.v.forEach((v,k)=>v.forEach((x,d)=>maxHeadDelta=Math.max(maxHeadDelta,Math.abs(x-oldHead[j].v[k][d])))));
    for(const f of pose.faces)for(const v of f.v){assert(v.every(Number.isFinite));maxRadius=Math.max(maxRadius,Math.hypot(...v));}
  }
  assert(maxRadius<4,'Garment escaped the character pose envelope');
  for(const ppm of [32,64]){
    const cellW=Math.ceil(1.7*ppm),cellH=Math.ceil(1.9*ppm),factor=ppm===64?2:4,w=cellW*8*factor,h=cellH*factor;
    const pixels=new Uint8ClampedArray(w*h*4);for(let i=0;i<pixels.length;i+=4){pixels[i]=224;pixels[i+1]=233;pixels[i+2]=223;pixels[i+3]=255;}
    const posed=E.pose(character,'idle',0),H=c.CharacterHeadStudy,state=H.life(0,{expr:'neutral'});
    for(let angle=0;angle<360;angle+=45){
      const rendered=c.raster(posed.faces,character.mats,{w:cellW,h:cellH,cx:cellW/2,cy:cellH-ppm*.22,scale:ppm,angle,elev:40,mode:'proposal',outline:true,
        surface:(u,v)=>H.sample(u,v,state,character.headBuild),surfaceColours:character.colours});
      let ink=0,edge=0;
      for(let y=0;y<cellH;y++)for(let x=0;x<cellW;x++){
        const si=(y*cellW+x)*4;if(!rendered.pixels[si+3])continue;ink++;if(x===0||x===cellW-1||y===0||y===cellH-1)edge++;
        for(let sy=0;sy<factor;sy++)for(let sx=0;sx<factor;sx++){
          const ti=((y*factor+sy)*w+(angle/45*cellW+x)*factor+sx)*4;pixels.set(rendered.pixels.slice(si,si+4),ti);
        }
      }
      assert(ink>100,'Empty preview');assert.strictEqual(edge,0,'Character clipped by proof canvas');renderChecks++;
    }
    png({pixels,w,h},path.join(dir,'wardrobe-'+p.Id+'-'+ppm+'.png'));
  }
  rows.push({Preset:p.Id,Recipe:p.Recipe,...measurements,OccupiedSlots:character.wardrobe.OccupiedSlots,CoverageTags:character.wardrobe.CoverageTags,
    HiddenIdentityFaces:sections.identity.length-a.length,SwapCpuMs:{Samples:timings.length,Median:timings[50],P95:timings[95],Max:timings.at(-1)}});
}
assert.strictEqual(maxHeadDelta,0);
const starter=W.presets[0].Recipe,mixed=W.presets[1].Recipe;
// The five authored garments expose four complete legal combinations, not just the
// two named presentation presets. Exercise the two crossed top/bottom selections too.
for(const [top,bottom] of [[starter.Garments[0],mixed.Garments[1]],[mixed.Garments[0],starter.Garments[1]]]){
  const recipe={SchemaVersion:1,Identity:plain(mixed.Identity),Garments:[plain(top),plain(bottom),plain(mixed.Garments[2])]},character=W.create(base,recipe);
  for(const anim of Object.keys(E.animations))for(let i=0;i<=12;i++){
    const posed=E.pose(character,anim,i/12);poseSamples++;
    for(const face of posed.faces)for(const p of face.v){assert(p.every(Number.isFinite));assert(Math.hypot(...p)<4);}
  }
}
assert(W.create(base,starter).faces.some(f=>W.coverageTag(f)),'Short shirt restores forearms');
assert(!W.create(base,mixed).faces.some(f=>W.coverageTag(f)),'Long sleeves hide exposed forearms');
const forward=sections['fisher_study.trousers'],reversed=base.bind.bones.slice().reverse(),n=reversed.length;
const swapped=forward.map(f=>({...f,bone:f.bone.map(ws=>ws.map(([i,w])=>[n-i-1,w]))}));
assert.deepStrictEqual(plain(W.remapFaces(swapped,reversed,base.bind.bones)),plain(forward),'Remap must use IDs, not array indices');
const damaged=plain(base.bind.bones);damaged.find(b=>b.id==='pelvis').p[2]+=.01;
expectFail('rest-transform mismatch',()=>W.remapFaces(forward,base.bind.bones,damaged),/rest mismatch/);
expectFail('base fit rest-transform mismatch',()=>W.create({...base,bind:{...base.bind,bones:damaged}},mixed),/fit rest transform differs/);
const missing=base.bind.bones.filter(b=>b.id!=='pelvis');expectFail('missing bone',()=>W.remapFaces(forward,base.bind.bones,missing),/missing target bone/);
for(const key of E.cast.filter(x=>x!=='fisher'))expectFail('unsupported cast fit '+key,()=>W.create(E.create(key),mixed),/unsupported body fit/);
for(const axis of ['age','height','weight','garment']){const changed={...base,build:{...base.build,[axis]:axis==='age'?'child':axis==='garment'?'skirt':1.01}};
  expectFail('unsupported structural change '+axis,()=>W.create(changed,mixed),/unsupported structural change/);}
let invalid=plain(mixed);invalid.Garments.push(starter.Garments[1]);expectFail('combined garment slot conflict',()=>W.create(base,invalid),/occupied slot conflict/);
invalid=plain(mixed);invalid.Garments[0].ColourwayId='colourway.missing';expectFail('unknown colourway',()=>W.create(base,invalid),/unknown colourway/);
invalid=plain(mixed);invalid.Garments.pop();expectFail('no bare feet variant',()=>W.create(base,invalid),/missing required fitted slot/);
invalid=plain(mixed);invalid.Identity.Skin='dark';expectFail('garment cannot replace identity',()=>W.create(base,invalid),/identity differs/);
invalid=plain(mixed);invalid.Garments[0].GarmentId='garment.missing';expectFail('unknown garment',()=>W.create(base,invalid),/unknown garment/);
invalid=plain(mixed);invalid.SchemaVersion=2;expectFail('unknown schema version',()=>W.create(base,invalid),/schema version/);
expectFail('unknown palette ramp',()=>W.resolveRamp('ramp.shirt.missing'),/unknown palette ramp/);
// Cross-language contract regressions: these fixtures exercise the same accepted/rejected
// boundaries as Core, including forged accessory geometry and explicit source inheritance.
function withCatalogue(edit,run){
  const original=plain(catalog);
  try{edit(catalog);return run();}finally{for(const key of Object.keys(catalog))delete catalog[key];Object.assign(catalog,original);}
}
const selectedTop=data=>data.Garments.find(g=>g.Id===mixed.Garments[0].GarmentId);
for(const [label,edit,pattern] of [
  ['catalogue schema version',d=>d.SchemaVersion=999,/schema version/],
  ['garment schema version',d=>selectedTop(d).SchemaVersion=999,/schema version/],
  ['fit schema version',d=>d.Fits[0].SchemaVersion=999,/schema version/],
  ['duplicate catalogue garment',d=>d.Garments.push(plain(d.Garments[0])),/duplicate garment ID/],
  ['duplicate catalogue fit',d=>d.Fits.push(plain(d.Fits[0])),/duplicate fit ID/],
  ['trailing newline garment ID',d=>selectedTop(d).Id+='\n',/stable garment/],
  ['trailing newline fit hash',d=>d.Fits[0].SourceRigSha256+='\n',/SHA-256/],
  ['unknown occupied slot',d=>selectedTop(d).OccupiedSlots.push('not_a_slot'),/unknown slot/],
  ['duplicate occupied slot',d=>selectedTop(d).OccupiedSlots.push('top'),/OccupiedSlots repeats/],
  ['unknown fit-required slot',d=>d.Fits[0].RequiredSlots.push('not_a_slot'),/unknown required slot/],
  ['duplicate bone ID',d=>d.Fits[0].BoneIds.push(d.Fits[0].BoneIds[0]),/BoneIds repeats/],
  ['empty section declaration',d=>selectedTop(d).Fits[0].SourceSectionIds=[],/SourceSectionIds must not be empty/],
  ['unknown source section',d=>selectedTop(d).Fits[0].SourceSectionIds=['fisher_study.missing'],/unknown source section/],
  ['unknown coverage tag',d=>selectedTop(d).CoverageTags.push('identity.everything'),/unknown coverage/],
  ['duplicate colourway',d=>selectedTop(d).Colourways.push(plain(selectedTop(d).Colourways[0])),/repeats colourway/],
  ['duplicate material binding',d=>selectedTop(d).Colourways[0].MaterialRoles.push(plain(selectedTop(d).Colourways[0].MaterialRoles[0])),/maps a source material twice/],
  ['missing material role text',d=>selectedTop(d).Colourways[0].MaterialRoles[0].Role='',/material Role is missing/],
  ['self-conflicting compatibility tag',d=>selectedTop(d).IncompatibleTags=['fisher_study'],/both declares and blocks/],
  ['missing catalogue row',d=>d.Garments.push(null),/missing garment/]
])withCatalogue(edit,()=>expectFail(label,()=>W.create(base,mixed),pattern));
withCatalogue(d=>{
  const duplicate=plain(selectedTop(d));duplicate.Id='garment.duplicate_geometry';duplicate.OccupiedSlots=['accessory'];d.Garments.push(duplicate);
},()=>{
  const duplicate=plain(mixed);duplicate.Garments.push({GarmentId:'garment.duplicate_geometry',ColourwayId:mixed.Garments[0].ColourwayId});
  expectFail('accessory cannot reuse selected garment section',()=>W.create(base,duplicate),/duplicate selected source section/);
});
invalid=plain(mixed);invalid.Identity.BuildId+='\n';expectFail('trailing newline identity ID',()=>W.create(base,invalid),/stable char_build/);
const inheritedChecks=[];
for(const variant of ['empty','omitted','null','partial'])withCatalogue(d=>{
  const colour=selectedTop(d).Colourways[0];
  if(variant==='omitted')delete colour.MaterialRoles;
  else if(variant==='null')colour.MaterialRoles=null;
  else if(variant==='partial')colour.MaterialRoles=colour.MaterialRoles.filter(r=>r.SourceMaterial!=='sleeve');
  else colour.MaterialRoles=[];
},()=>{
  assert.deepStrictEqual(plain(W.validateCatalogue()),[]);assert.deepStrictEqual(plain(W.validateRecipe(mixed)),[]);
  const metadata={...base.mats.sleeve,paintGain:.71,paintBias:1.67,off:-.2,gain:.42};
  const withMetadata={...base,mats:{...base.mats,sleeve:metadata}},assembled=W.create(withMetadata,mixed);
  const sleeve=assembled.faces.find(f=>f.wardrobeGarmentId===mixed.Garments[0].GarmentId&&f.part==='upper_L');
  assert.deepStrictEqual(plain(assembled.mats[sleeve.mat]),plain(metadata),'Unbound source material must retain its exact ramp and shader metadata');
  assert.deepStrictEqual(plain(assembled.faces.filter(f=>f.part==='head')),plain(base.faces.filter(f=>f.part==='head')),'Inherited clothing material cannot replace identity');
  inheritedChecks.push(variant);
});
const options=read('../rigs/character/options.json');
for(const garment of catalog.Garments)for(const colourway of garment.Colourways)for(const role of colourway.MaterialRoles){
  const [,axis,key]=role.RampId.split('.'),r=options.colour[axis][key],expected=r.length>4?[r[0],r[2],r[3],r[5]||r.at(-1)]:r;
  assert.deepStrictEqual(plain(W.resolveRamp(role.RampId)),expected,'Preview ramp must come from production palette data');
}
const sources=['../rigs/characterIsoRig7.js','../rigs/character/options.json','sources/eyeIsoRig.js','sources/headIsoRig3.js','sources/characterIsoRig6.js',
  'sources/characterIsoRig7.js','boot-segment-fix.cjs','sources/characterIsoRig6.hands.js','sources/proposal.js','sources/face-rig-pass04.js','sources/face-rig.js','sources/face-render.cjs','character-finish.js','character-finish.json','cast-engine.js','load-study.cjs','wardrobe-assembly.js','wardrobe-validate.cjs','wardrobe-recipes.json',
  'wardrobe-fit-fisher.json',...fs.readdirSync(dir).filter(f=>/^wardrobe-garment-.*\.json$/.test(f))];
const frames=Object.values(E.animations).reduce((n,a)=>n+a.frames,0);
const report={SchemaVersion:1,Scope:'Editor/review proof only; no Unity bake, runtime garment assembly, creator, purchases or save integration.',
  HashConvention:'SHA-256 UTF-8 text with CRLF normalized to LF',SourceHashes:Object.fromEntries(sources.map(s=>[s,hash(path.join(dir,s))])),
  PreviewBootCorrection:true,EffectiveRigSha256:effectiveRigSha256,EffectiveRigSource:'Frozen sources/characterIsoRig7.js with boot-segment-fix.cjs preview patch applied, before bones-only optimization. SourceRigSha256 pins the unchanged original snapshot. Production solver is unchanged; production adoption requires Unity rebake and contact checks.',
  FitId:fit.Id,SkeletonId:fit.SkeletonId,Bones:base.bind.bones.length,Sections:Object.fromEntries(Object.entries(sections).map(([k,v])=>[k,measure(v)])),
  Recipes:rows,SupportedCompleteCombinations:4,PoseSamples:poseSamples,Animations:Object.keys(E.animations).length,MaxRadiusMetres:maxRadius,MaxIdentityHeadDeltaMetres:maxHeadDelta,RenderChecks:renderChecks,
  BoneRemapping:'Reversed source index array maps byte-identically by stable bone ID; missing IDs and different rest transforms rejected.',
  NegativeChecks:negative,InheritedMaterialChecks:inheritedChecks,SharedAnimationBytes:frames*base.bind.bones.length*28,AnimationFrames:frames,
  MemoryLimits:'Mesh bytes are a conservative production-layout estimate, not a Unity profiler capture. Shared clip bytes counted once; no colour × garment × animation bake. Face UV/state implementation, object overhead, driver copies, normals under deformation, uploads and animation caches remain unmeasured in Unity.',
  SwapLimits:'Node CPU assembly of cached fitted sections (20 warmups, 100 samples); excludes rendering, GPU upload and Unity allocations. Figures are host-specific.',
  MaterialLimits:'Geometry ramps deduplicated by identical ramp content; 16-slot cap enforced. Procedural eye/brow/mouth surface colours are separately evaluated in the review rasterizer and do not prove production shader parity.',
  FitLimits:'Only Fisher study fixed adult proportions. No hats/hoods/aprons/skirts/child/elder fit, bare torso, bare legs or bare feet. Missing mandatory top/bottom/footwear fails.'};
fs.writeFileSync(path.join(dir,'wardrobe-measurements.json'),JSON.stringify(report,null,2)+'\n');
const html='<!doctype html><meta charset="utf-8"><title>Wardrobe mixed outfit proof</title><style>body{font:16px system-ui;background:#edf1e7;color:#173c40;margin:28px}img{image-rendering:pixelated;width:100%;height:auto}section{max-width:1800px}p{max-width:850px}</style><h1>Same Fisher, interchangeable clothing</h1><p>Starter shirt + teal bib overalls + deck boots, compared with ochre long sleeves + navy trousers + the same boots. The face, body fit, skin, hair and eyes stay fixed. Views: 0°, 45°, 90°, 135°, 180°, 225°, 270°, 315°. Editor proof; supported Fisher fit only.</p>'+[64,32].map(ppm=>'<section><h2>'+ppm+' px/m · '+(ppm===64?'preferred Detail reference':'global game scale check')+'</h2>'+W.presets.map(p=>'<h3>'+p.DisplayName+'</h3><img src="wardrobe-'+p.Id+'-'+ppm+'.png" alt="'+p.DisplayName+' in eight views">').join('')+'</section>').join('');
fs.writeFileSync(path.join(dir,'wardrobe-proof.html'),html+'\n');
console.log(JSON.stringify({Passed:true,PoseSamples:poseSamples,RenderChecks:renderChecks,NegativeChecks:negative.length,Recipes:rows.map(r=>({Id:r.Preset,Vertices:r.Vertices,Triangles:r.Triangles,Materials:r.UsedMaterialIds.length,MeshBytes:r.ConservativeMeshBytes,SwapCpuMs:r.SwapCpuMs})),SharedAnimationBytes:report.SharedAnimationBytes}));

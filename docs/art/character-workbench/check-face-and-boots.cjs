const assert=require('assert/strict'),fs=require('fs'),path=require('path');
const {loadStudy}=require('./load-study.cjs');
const old=loadStudy({facePass:'before',legacySolver:true}).context;
const now=loadStudy().context,Before=old.CharacterHeadStudy,After=now.CharacterHeadStudy;
const plain=x=>JSON.parse(JSON.stringify(x));
assert.equal(Object.keys(After.EXPRESSIONS).length,8);
let stateChecks=0,geometryChecks=0,segmentChecks=0,correctedBoots=0,maxOldDistance=0,maxNewDistance=0;
for(const expr of Object.keys(After.EXPRESSIONS))for(const seed of [0,17,48,296]){
  const mouths=new Set(),lids=new Set();
  for(let i=0;i<1800;i++){
    const t=i/50,options={expr,seed,talk:i%2===0};
    assert.deepEqual(plain(After.life(t,options)),plain(Before.life(t,options)),'Face timing changed');
    const a=After.life(t,{expr,seed,talk:true}),b=After.life(t,{expr,seed,talk:false});
    assert.equal(a.brow,b.brow,'Speech must not drive brows');assert.equal(a.raise,b.raise);
    mouths.add(a.mouth);lids.add(a.lid===1?'closed':a.lid===0?'open':'partial');stateChecks++;
  }
  assert.deepEqual([...mouths].sort(),['ah','closed','ee','oh']);
  assert(lids.has('closed')&&lids.has('partial'),'Seeded blink must include shut and intermediate lids');
}
for(const key of now.CastViewerEngine.cast){
  const b=now.CastViewerEngine.create(key),a=old.CastViewerEngine.create(key);
  assert.deepEqual(plain(b.build),plain(a.build),'Identity recipe changed');
  assert.deepEqual(plain(b.bind.bones),plain(a.bind.bones),'Rest skeleton changed');
  const oldHead=Before.createHead(a.headBuild,[0,0,0]),newHead=After.createHead(b.headBuild,[0,0,0]);
  assert.equal(newHead.length,oldHead.length);
  let different=0;for(let i=0;i<newHead.length;i++)if(JSON.stringify(newHead[i])!==JSON.stringify(oldHead[i]))different++;
  assert.equal(different,4,'Only the four nose facets should move; hair, hat and jaw stay intact');geometryChecks++;
  for(const anim of Object.keys(now.CastViewerEngine.animations))for(let frame=0;frame<=24;frame++){
    const u=frame/24,newBones=now.CharacterIso7.solve(anim,u,b.build,{bonesOnly:true}).bones;
    const oldBones=old.CharacterIso7.solve(anim,u,a.build,{bonesOnly:true}).bones;
    const byId=Object.fromEntries(newBones.map(x=>[x.id,x])),oldById=Object.fromEntries(oldBones.map(x=>[x.id,x]));
    for(const bone of newBones)assert([...bone.p,...bone.R].every(Number.isFinite),'Nonfinite bone '+key+'/'+anim+'/'+bone.id);
    for(const side of ['L','R']){
      const top=byId['ankle_'+side+'_cuff'];if(!top)continue;
      const ankle=byId['foot_'+side].p,knee=byId['hip_'+side+'_tip'].p,d=knee.map((v,j)=>v-ankle[j]);
      const square=d.reduce((s,v)=>s+v*v,0),v=top.p.map((x,j)=>x-ankle[j]);
      const t=v.reduce((s,x,j)=>s+x*d[j],0)/square;
      assert(t>=-1e-9&&t<=1+1e-9,'Boot outside shin '+key+'/'+anim+'/'+u+'/'+side);
      assert(v.every((x,j)=>Math.abs(x-d[j]*t)<1e-9),'Boot off shin in one axis');
      const oldTop=oldById[top.id].p,maxAbs=p=>Math.max(...p.map(Math.abs));
      maxOldDistance=Math.max(maxOldDistance,maxAbs(oldTop));maxNewDistance=Math.max(maxNewDistance,maxAbs(top.p));
      if(oldTop.some((v,j)=>Math.abs(v-top.p[j])>1e-9))correctedBoots++;
      segmentChecks++;
    }
    // Art pass 03 expression intent and baked-loop timing are preserved too.
    assert.deepEqual(plain(After.poseState(anim,u,{seed:17,talk:true})),plain(Before.poseState(anim,u,{seed:17,talk:true})));
  }
}
assert(correctedBoots>0&&maxOldDistance>100,'Regression positive control must reproduce the old boot escape');
assert(maxNewDistance<4,'Corrected boots exceed the sampled pose envelope');
// Pupil gaze changes colour inside fixed sockets, while the outer eye material mask is stationary.
let pupilMoves=0;
for(const shape of ['round','wide','sharp','narrow','droop'])for(const gx of [-1,1]){
  const b={eyeShape:shape,browShape:'none'},center={gaze:[0,0],mouth:'neutral'},away={...center,gaze:[gx,0]};
  for(let z=.018;z<.076;z+=.001)for(let x=-.115;x<=.115;x+=.001){
    const u=.5+x/.38,v=(z-After.C.uvBottom)/After.C.uvHeight;
    const a=After.sample(u,v,center,b),c=After.sample(u,v,away,b);
    assert.equal(a===0,c===0,'Eye socket moved with gaze');if(a!==c)pupilMoves++;
  }
}
assert(pupilMoves>0);
assert.equal(require('./boot-segment-fix.cjs').productionPatch(),fs.readFileSync(path.join(__dirname,'production-boot-segment.patch'),'utf8'),'Deferred production patch must match the tested correction');
const report={stateChecks,geometryChecks,poseSamples:10*35*25,segmentChecks,correctedBoots,maxOldAbsoluteCoordinateMetres:maxOldDistance,maxNewAbsoluteCoordinateMetres:maxNewDistance,pupilMoves,previewBootCorrection:true,productionBootPortPending:true,unityBakeRun:false};
fs.mkdirSync(path.join(__dirname,'review'),{recursive:true});fs.writeFileSync(path.join(__dirname,'review','face-and-boots.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));

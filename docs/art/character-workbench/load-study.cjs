// Offline authoring harness. Frozen source snapshots reproduce the original study.
// The reviewed boot correction is preview-only until the production Unity rebake.
const fs=require('fs'),path=require('path'),vm=require('vm');
function loadStudy({facePass='after',finishPass='after',bonesOnly=true,legacySolver=false}={}){
  const dir=__dirname,sourceDir=path.join(dir,'sources'),context={console};
  context.globalThis=context;vm.createContext(context);let dependencies='';
  const faceFile=finishPass==='before'?'face-rig-pass04.js':facePass==='before'?'face-rig-pass03.js':facePass==='previous'?'face-rig-pass05.js':'face-rig.js';
  const names=['eyeIsoRig.js','headIsoRig3.js','characterIsoRig6.js','characterIsoRig6.hands.js','characterIsoRig7.js','proposal.js','face-rig-pass04.js'];
  if(faceFile!=='face-rig-pass04.js'){
    names.push('face-rig-pass05.js');
    if(faceFile!=='face-rig-pass05.js')names.push(faceFile);
  }
  for(const name of names){
    const file=path.join(sourceDir,name);
    let source=fs.readFileSync(file,'utf8');
    if(name==='characterIsoRig7.js'&&!legacySolver)source=require('./boot-segment-fix.cjs').applyBootSegmentCorrection(source);
    if(name==='characterIsoRig7.js'&&bonesOnly){
      const marker="    /* ---- faces, in rig 6's facesOf order ---- */";
      const call='const S = solve(P, b, { carry, tier:opts.tier });';
      if(!source.includes(marker)||!source.includes(call))throw Error('Bone solver shortcut markers changed');
      source=source.replace(marker,"    if(ctx.bonesOnly)return {bones:BN};\n"+marker)
        .replace(call,'const S = solve(P, b, { carry, tier:opts.tier, bonesOnly:opts.bonesOnly });');
    }
    if(name==='face-rig-pass04.js')source+='\nglobalThis.CharacterHeadPass04=globalThis.CharacterHeadStudy;';
    if(name==='face-rig-pass05.js')source+='\nglobalThis.CharacterHeadPass05=globalThis.CharacterHeadStudy;';
    vm.runInContext(source,context,{filename:file});dependencies+='\n'+source;
  }
  if(finishPass!=='before'){
    const config=JSON.parse(fs.readFileSync(path.join(dir,'character-finish.json'),'utf8'));
    const finish='globalThis.CharacterFinishConfig='+JSON.stringify(config)+';\n'+fs.readFileSync(path.join(dir,'character-finish.js'),'utf8');
    vm.runInContext(finish,context,{filename:'character-finish.js'});dependencies+='\n'+finish;
  }
  const engine=fs.readFileSync(path.join(dir,'cast-engine.js'),'utf8');vm.runInContext(engine,context);
  let rasterSource=fs.readFileSync(path.join(sourceDir,'face-render.cjs'),'utf8');
  rasterSource=rasterSource.slice(rasterSource.indexOf('function raster('),rasterSource.indexOf('// PNG encoding'));
  vm.runInContext(rasterSource,context);
  return {context,dependencies,engine,rasterSource};
}
module.exports={loadStudy};

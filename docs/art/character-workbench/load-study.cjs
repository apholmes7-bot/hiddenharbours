// Offline authoring harness. Frozen source snapshots reproduce the original study.
// The reviewed boot correction is preview-only until the production Unity rebake.
const fs=require('fs'),path=require('path'),vm=require('vm');
function loadStudy({facePass='after',bonesOnly=true,legacySolver=false}={}){
  const dir=__dirname,sourceDir=path.join(dir,'sources'),context={console};
  context.globalThis=context;vm.createContext(context);let dependencies='';
  const names=['eyeIsoRig.js','headIsoRig3.js','characterIsoRig6.js','characterIsoRig6.hands.js','characterIsoRig7.js','proposal.js',facePass==='before'?'face-rig-pass03.js':'face-rig.js'];
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
    vm.runInContext(source,context,{filename:file});dependencies+='\n'+source;
  }
  const engine=fs.readFileSync(path.join(dir,'cast-engine.js'),'utf8');vm.runInContext(engine,context);
  let rasterSource=fs.readFileSync(path.join(sourceDir,'face-render.cjs'),'utf8');
  rasterSource=rasterSource.slice(rasterSource.indexOf('function raster('),rasterSource.indexOf('// PNG encoding'));
  vm.runInContext(rasterSource,context);
  return {context,dependencies,engine,rasterSource};
}
module.exports={loadStudy};

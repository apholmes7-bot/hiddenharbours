const fs=require('fs'),path=require('path'),vm=require('vm');
const dir=__dirname;
const {context,dependencies,engine,rasterSource:raster}=require('./load-study.cjs').loadStudy();
let wardrobe='';
if(fs.existsSync(path.join(dir,'wardrobe-assembly.js'))){
 const readJson=name=>JSON.parse(fs.readFileSync(path.join(dir,name),'utf8'));
 const files=fs.readdirSync(dir),fits=files.filter(f=>/^wardrobe-fit-.*\.json$/.test(f)).sort(),garments=files.filter(f=>/^wardrobe-garment-.*\.json$/.test(f)).sort();
 if(!fits.length||!garments.length)throw Error('The clothing study requires authored wardrobe-fit-*.json and wardrobe-garment-*.json files');
 const catalogue={SchemaVersion:1,Fits:fits.map(readJson),Garments:garments.map(readJson)};
 wardrobe='globalThis.CharacterWardrobeCatalog='+JSON.stringify(catalogue)+';\n'+
 'globalThis.CharacterWardrobeRecipes='+fs.readFileSync(path.join(dir,'wardrobe-recipes.json'),'utf8')+';\n'+fs.readFileSync(path.join(dir,'wardrobe-assembly.js'),'utf8');
 vm.runInContext(wardrobe,context);
}
else throw Error('Missing wardrobe-assembly.js: the clothing study is a required part of this workbench');
const E=context.CastViewerEngine,characters=E.cast.map(k=>E.create(k)),bounds={},checks=[],outliers=[];
const beforeCharacters=E.cast.map(k=>({...E.create(k,{finish:false,headStudy:context.CharacterHeadPass04}),variantId:'before'}));
const W=context.CharacterWardrobeProof,variants=W?W.presets.map(p=>({...W.create(characters.find(c=>c.key==='fisher'),p.Recipe),variantId:p.Id})):[];
const beforeVariants=W?W.presets.map(p=>({...W.create(beforeCharacters.find(c=>c.key==='fisher'),p.Recipe),variantId:'before_'+p.Id})):[];
// Pass05 is an eye-material comparison with the same geometry as pass06. Assert
// that contract before sharing crops; a later shape edit must expand the pose matrix.
for(const current of characters){
  const previous=E.create(current.key,{headStudy:context.CharacterHeadPass05});
  for(const field of ['faces','bind','build','mats'])if(JSON.stringify(previous[field])!==JSON.stringify(current[field]))throw Error('Previous-eye comparison changed '+field+' for '+current.key);
  if(W&&current.key==='fisher')for(const preset of W.presets){
    const a=W.create(previous,preset.Recipe),b=W.create(current,preset.Recipe);
    if(a.headStudy!==context.CharacterHeadPass05||JSON.stringify(a.faces)!==JSON.stringify(b.faces))throw Error('Previous eyes lost wardrobe/geometry continuity');
  }
}
for(const anim of Object.keys(E.animations)){
  let radius=0,minZ=Infinity,maxZ=-Infinity;
  for(const c of characters.concat(beforeCharacters,variants,beforeVariants)){
    let finite=true;for(let i=0;i<=12;i++){
      const posed=E.pose(c,anim,i/12);
      for(const f of posed.faces)for(const p of f.v){finite=finite&&p.every(Number.isFinite);const r=Math.hypot(p[0],p[1]);if(r>4&&!outliers.some(x=>x.cast===c.key&&x.anim===anim&&x.part===f.part))outliers.push({cast:c.key,anim,part:f.part,u:i/12,r});radius=Math.max(radius,r);minZ=Math.min(minZ,p[2]);maxZ=Math.max(maxZ,p[2]);}
    }
    const fast=context.CharacterIso7.solve(anim,.37,c.build,{bonesOnly:true}).bones,full=context.CharacterIso7.solve(anim,.37,c.build,{}).bones;
    const solverEqual=JSON.stringify(fast)===JSON.stringify(full);
    checks.push({cast:c.key,variant:c.variantId||'original',anim,finite,solverEqual});
  }
  const se=Math.sin(40*Math.PI/180),ce=Math.cos(40*Math.PI/180);
  bounds[anim]={width:Math.max(1.10,radius*2),minY:-radius*se-maxZ*ce,height:radius*2*se+(maxZ-minZ)*ce};
}
fs.writeFileSync(path.join(dir,'cast-validation.json'),JSON.stringify({cast:characters.map(c=>({key:c.key,bones:c.bind.bones.length,faces:c.faces.length})),wardrobeVariants:variants.map(c=>({id:c.variantId,faces:c.faces.length})),animations:Object.keys(E.animations),poseSamples:checks.length*13,previewBootGuard:true,productionBootPortPending:true,outliers,checks,bounds},null,2));
if(outliers.length)throw Error('Geometry escaped the four-metre pose envelope: '+JSON.stringify(outliers));
if(checks.some(c=>!c.finite||!c.solverEqual))throw Error('Cast pose validation failed');
const controller=fs.readFileSync(path.join(dir,'viewer-controller.js'),'utf8').replace('__BOUNDS__',JSON.stringify(bounds));
const fragment=fs.readFileSync(path.join(dir,'viewer-template.html'),'utf8').replace('__DEPENDENCIES__',dependencies).replace('__ENGINE__',engine).replace('__WARDROBE__',wardrobe).replace('__RASTER__',raster).replace('__CONTROLLER__',controller);
fs.writeFileSync(path.join(dir,'hidden-harbours-cast.html'),fragment);
const css=fs.readFileSync(path.join(dir,'standalone.css'),'utf8');
const page=fs.readFileSync(path.join(dir,'standalone-template.html'),'utf8').replace('__CSS__',css).replace('__VIEWER__',fragment);
fs.writeFileSync(path.join(dir,'Hidden-Harbours-Cast-Viewer.html'),page);
console.log(JSON.stringify({characters:characters.length,wardrobeVariants:variants.length,animations:Object.keys(E.animations).length,poseSamples:checks.length*13,valid:checks.every(c=>c.finite&&c.solverEqual),bytes:Buffer.byteLength(fragment)}));

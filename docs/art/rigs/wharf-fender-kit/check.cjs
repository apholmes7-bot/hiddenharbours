'use strict';
// node docs/art/rigs/wharf-fender-kit/check.cjs [outDir]  (run from the repository root)
const fs=require('fs'),path=require('path'),crypto=require('crypto'),assert=require('assert/strict');
const R=require('./wharfFenders.js'),root=process.cwd(),recipe=JSON.parse(fs.readFileSync(path.join(__dirname,'recipes.json'),'utf8'));
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n')).digest('hex');
const checks=[];function check(name,run){run();checks.push({name,result:'PASS'});}
const regionFor={'keyscene.stp_landing':'StPeters','keyscene.nmc_wharf':'NineMileCreek'};
const tideOf=scene=>{
 const text=fs.readFileSync(path.join(root,'Assets/_Project/Data/Regions/'+regionFor[scene]+'.asset'),'utf8');
 const n=k=>+text.match(new RegExp('^  '+k+': ([^\\r\\n]+)','m'))[1];
 return {low:n('TideMeanLevel')-n('TideAmplitude'),high:n('TideMeanLevel')+n('TideAmplitude')};
};
const strings=recipe.pieces.filter(p=>p.key==='fenderString');
check('StableRowsAndExistingDefType',()=>{
 assert.deepEqual(recipe.pieces.map(p=>p.id),['prop.wharf_fender_string_stp','prop.wharf_fender_string_nmc','prop.wharf_ladder_nmc']);
 assert.ok(recipe.pieces.every(p=>p.defType==='SetPieceDef'&&p.rigDir===4&&regionFor[p.scene]));
});
check('WaterBoundsAreTheRegionsTide',()=>{
 for(const p of recipe.pieces){const t=tideOf(p.scene);assert.equal(p.options.lowWater,t.low,p.id);assert.equal(p.options.highWater,t.high,p.id);}
});
check('EveryStringCoversTheRubBandFromLowWaterToHighWaterPlusFreeboard',()=>{
 for(const p of strings){
  const c=R.contract('fenderString',p.options),t=tideOf(p.scene);
  assert.ok(c.rubBandCovered[0]<=t.low+1e-9,p.id+' reaches spring low');
  assert.ok(c.rubBandCovered[1]>=Math.min(t.high+p.options.freeboard,p.options.top-.15)-1e-9,p.id+' reaches high water plus freeboard');
  for(let i=1;i<c.tyreCentres.length;i++)assert.ok(c.tyreCentres[i]-c.tyreCentres[i-1]<=c.outsideDiameter*1.6+1e-9,p.id+' leaves no bare band');
 }
});
check('EveryTyreHangsOnATimberThatReachesBelowSpringLow',()=>{
 for(const p of strings){
  const c=R.contract('fenderString',p.options),t=tideOf(p.scene),b=R.bounds('fenderString',p.options);
  assert.deepEqual(c.hangerPivot,[0,0,0]);assert.ok(b.z[1]<=1e-9,'nothing stands above the deck lip');
  assert.ok(c.timberFoot<=t.low-.5+1e-9,p.id+' timber foot under spring low');
  assert.ok(Math.abs(b.z[0]-(c.timberFoot-p.options.top))<1e-6,p.id+' the timber is the lowest thing drawn');
  assert.ok(c.tyreCentres[0]-c.outsideDiameter/2>=c.timberFoot,p.id+' no tyre dangles past the timber');
 }
});
check('StandOutFitsTheAlongsideGap',()=>{
 for(const p of strings)assert.ok(R.contract('fenderString',p.options).standOut<=R.MAX_STANDOUT+1e-9);
 assert.throws(()=>R.spec('fenderString',{timberDepth:.3}));
});
check('TideDerivedWetAndWeedBandsWithoutPaintedWater',()=>{
 const mats=p=>new Set(R.geometry('fenderString',p.options).faces.map(f=>f.mat));
 for(const p of strings){const m=mats(p);for(const k of ['pole','wet','weed','rubber','rubberWet','rubberWeed'])assert.ok(m.has(k),p.id+' has '+k);}
});
check('LadderReachesFromTheDeckToBelowSpringLow',()=>{
 for(const p of recipe.pieces.filter(q=>q.key==='ladder')){
  const t=tideOf(p.scene);assert.ok(p.options.top-p.options.height<=t.low-.5+1e-9,p.id+' foot');
  assert.equal(p.options.rung,.3);assert.equal(p.options.standoff,.275);
 }
});
check('EveryFacingNativePixelsHeightAndDeterminism',()=>{
 for(const p of strings)for(let dir=0;dir<8;dir++){
  const f=R.frame(p.key,dir,p.options);assert.deepEqual(f.rgba,R.render(p.key,dir,p.options));
  let any=false;for(let i=0;i<f.localZ.length;i++)if(f.rgba[i*4+3]){
   any=true;const x=i%f.w,y=Math.floor(i/f.w);assert.ok(x>0&&x<f.w-1&&y>0&&y<f.h-1);assert.ok(Number.isFinite(f.localZ[i]));
  }assert.ok(any);assert.equal(f.pivot.x,210);assert.equal(f.pivot.y,160);
 }
});
check('ImmutableSourceHashesAndFileSums',()=>{
 for(const p of recipe.pieces)assert.equal(sha(path.join(root,p.source)),p.sourceSha256);
 for(const line of fs.readFileSync(path.join(__dirname,'SHA256SUMS.txt'),'utf8').trim().split(/\r?\n/)){
  const m=line.match(/^([0-9a-f]{64})  (.+)$/);assert.ok(m);assert.equal(sha(path.join(__dirname,m[2])),m[1]);
 }
});
const result={node:process.version,checks:checks.length,passed:checks.length,failed:0,results:checks};
if(process.argv[2]){const out=path.resolve(process.argv[2]);fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'kit-check.json'),JSON.stringify(result,null,2)+'\n');}
console.log(JSON.stringify(result));

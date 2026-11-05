'use strict';
const fs=require('fs'),path=require('path'),crypto=require('crypto'),assert=require('assert/strict');
const R=require('./stPetersLandingFittings.js'),root=process.cwd(),recipe=JSON.parse(fs.readFileSync(path.join(__dirname,'recipes.json'),'utf8'));
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n')).digest('hex');
const checks=[];function check(name,run){run();checks.push({name,result:'PASS'});}
check('ExactlyThreeStableRowsAndExistingDefType',()=>{
 assert.deepEqual(recipe.pieces.map(p=>p.id),['prop.stp_landing_tyre_west','prop.stp_landing_tyre_east','prop.stp_landing_rope']);
 assert.ok(recipe.pieces.every(p=>p.defType==='SetPieceDef'));
 assert.deepEqual(recipe.pieces.map(p=>p.existingAnchor),[[190,2.533452],[208,2.533452],[206.5,3.283452]]);
 assert.ok(recipe.retiredByName.every(p=>p.delete===false));
});
check('TrueTyreDiameterSectionAndTopPivot',()=>{
 const c=R.contract('tyre'),b=R.bounds('tyre');
 assert.equal(c.outsideDiameter,.6);assert.equal(c.sectionDepth,.2);assert.equal(c.openingDiameter,.32);
 assert.deepEqual(c.hangerPivot,[0,0,0]);assert.ok(Math.abs(b.x[1]-b.x[0]-.6)<1e-9);
 assert.ok(b.y[1]-b.y[0]>=.2&&b.y[1]-b.y[0]<=.2*1.15);
 assert.ok(b.z[1]<=0&&b.z[0]>=-.6*1.15);
});
check('TwentyFiveMillimetreLineAndLooseTail',()=>{
 const c=R.contract('ropeCoil'),b=R.bounds('ropeCoil');
 assert.equal(c.lineDiameter,.025);assert.equal(c.tailDiameter,.025);assert.equal(c.layers,3);
 assert.ok(Math.abs(c.looseTail.at(-1)[0]-c.looseTail[0][0]-.32)<1e-9);
 assert.ok(b.z[0]>=0&&b.z[1]-b.z[0]>=.075*.85&&b.z[1]-b.z[0]<=.075*1.15);
 for(const diameter of [.02,.03])assert.equal(R.contract('ropeCoil',{lineDiameter:diameter}).lineDiameter,diameter);
 assert.throws(()=>R.spec('ropeCoil',{lineDiameter:.06}));
 const f=R.frame('ropeCoil',0),edge=R.pivot.x+(.34+.4*.16+.025)*32;
 assert.ok(f.rgba.some((value,i)=>i%4===3&&value&&((i-3)/4)%f.w>edge),'The loose tail must paint beyond the coil body.');
});
check('TideDerivedWetAndWeedBandsWithoutPaintedWater',()=>{
 const names=top=>new Set(R.geometry('tyre',{top}).faces.map(f=>f.mat));
 assert.ok(!names(5.354004).has('rubberWet'));
 assert.ok(names(2.25).has('rubberWet')&&names(2.25).has('rubber'));
 assert.ok(names(-1.4).has('rubberWeed')&&names(-1.4).has('rubberWet'));
 const region=fs.readFileSync(path.join(root,'Assets/_Project/Data/Regions/StPeters.asset'),'utf8');
 const n=k=>+region.match(new RegExp('^  '+k+': ([^\\r\\n]+)','m'))[1];
 for(const p of recipe.pieces){assert.equal(p.options.lowWater,n('TideMeanLevel')-n('TideAmplitude'));assert.equal(p.options.highWater,n('TideMeanLevel')+n('TideAmplitude'));}
});
check('EveryFacingNativePixelsHeightAndDeterminism',()=>{
 for(const p of recipe.pieces)for(let dir=0;dir<8;dir++){
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

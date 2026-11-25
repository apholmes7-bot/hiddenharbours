'use strict';
// Real Node, built-ins only. Output is optional and must be supplied by the caller.
const fs=require('fs'),path=require('path'),vm=require('vm'),crypto=require('crypto'),assert=require('assert/strict');
const kit=__dirname,root=path.resolve(kit,'../../../..'),R=require('./stPetersDockProps.js');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n')).digest('hex');
const recipe=JSON.parse(fs.readFileSync(path.join(kit,'recipes.json'),'utf8'));
const results=[];function check(name,fn){fn();results.push({name,result:'PASS'});}
check('StableLandingIdsAndExistingFitting',()=>{
 assert.deepEqual(recipe.pieces.map(p=>p.id),['prop.stp_landing_cart','prop.stp_landing_traps','prop.stp_landing_totes','prop.stp_landing_ladder']);
 assert.equal(R.ladderContract().fittingId,'wharf.st_peters.ladder_0');
 assert.ok(recipe.pieces.every(p=>p.defType==='SetPieceDef'));
});
check('ExactBoardRecipesRetainExistingDrawings',()=>{
 assert.deepEqual(recipe.pieces[1].options,{variant:0,len:0.5,weather:0.35});
 assert.deepEqual(recipe.pieces[2].options,{variant:1});
 assert.equal(recipe.pieces[1].global,'WharfDecor');assert.equal(recipe.pieces[2].global,'WharfDecor');
 for(const p of recipe.pieces)assert.equal(sha(path.join(root,p.source)),p.sourceSha256);
});
check('LadderRungsWaterAndHangerContract',()=>{
 const c=R.ladderContract();assert.equal(c.top,5.354004);assert.equal(c.foot,-2.745996);
 assert.equal(c.rungZ.length,27);assert.equal(c.rungZ[0],-.30);assert.equal(c.rungZ.at(-1),-8.10);
 assert.equal(c.standoff,.275);assert.deepEqual(c.hangerPivot,[0,0,0]);assert.ok(c.foot<c.lowWater-.5);
 for(let i=1;i<c.rungZ.length;i++)assert.ok(Math.abs(c.rungZ[i-1]-c.rungZ[i]-.30)<1e-9);
 const faces=R.geometry('ladder',{}).faces;
 for(const z of c.rungZ)assert.ok(faces.some(f=>f.v.some(p=>Math.abs(p[0]-.25)<1e-7&&Math.abs(p[2]-z)<.018&&Math.abs(p[1]-.275)<.018)));
 const b=R.bounds('ladder');assert.ok(b.z[0]<=-8.1&&b.z[0]>-8.13);assert.ok(b.z[1]<1e-8);
 assert.equal(c.handsLine.y,.275);assert.equal(c.feetLine.y,.275);
});
check('CartHasTwoEndSupportsAndUsableScale',()=>{
 const g=R.geometry('dockCart',{}),b=R.bounds('dockCart');
 assert.ok(b.z[1]>.95&&b.z[1]<1.05);assert.ok(b.x[1]-b.x[0]>1.2&&b.x[1]-b.x[0]<1.4);
 for(const y of [-.32,.32])assert.ok(g.faces.some(f=>f.v.some(p=>p[0]<-.62&&Math.abs(p[1]-y)<.03&&p[2]>.95)));
 // No drawbar leaves the centre of a deck edge: elevated handle is confined to one end.
 assert.ok(g.faces.flatMap(f=>f.v).filter(p=>p[2]>.9).every(p=>p[0]<-.60));
});
check('EveryFacingAndHeightIsUnclippedAndDeterministic',()=>{
 for(const key of ['dockCart','ladder'])for(const height of (key==='ladder'?[.6,8.1,14]:[8.1]))for(let dir=0;dir<8;dir++){
  const o={height},a=R.frame(key,dir,o),b=R.render(key,dir,o);assert.deepEqual(a.rgba,b);
  let n=0;for(let y=0;y<a.h;y++)for(let x=0;x<a.w;x++)if(a.rgba[(y*a.w+x)*4+3]){
   n++;assert.ok(x>1&&x<a.w-2&&y>1&&y<a.h-2);assert.ok(Number.isFinite(a.localZ[y*a.w+x]));
  }assert.ok(n>20);assert.equal(a.pivot.x,210);assert.equal(a.pivot.y,160);
 }
});
check('SidecarDerivedFromCurrentRig',()=>{
 const c=JSON.parse(fs.readFileSync(path.join(kit,'ladder.gameplay.json'),'utf8'));
 const derived=c.derivedFromRigSha256;delete c.derivedFromRigSha256;
 assert.equal(derived,sha(path.join(kit,'stPetersDockProps.js')));assert.deepEqual(c,R.ladderContract(recipe.pieces[3].options));
});
check('StPetersProfileAndTideBoardAgree',()=>{
 const region=fs.readFileSync(path.join(root,'Assets/_Project/Data/Regions/StPeters.asset'),'utf8');
 const n=k=>+region.match(new RegExp('^  '+k+': ([^\\r\\n]+)','m'))[1];
 assert.equal(n('TideMeanLevel'),0);assert.equal(n('TideAmplitude'),2.2);assert.equal(n('TidePhaseHours'),1);
 assert.equal(recipe.pieces[3].options.lowWater,n('TideMeanLevel')-n('TideAmplitude'));
 assert.equal(recipe.pieces[3].options.highWater,n('TideMeanLevel')+n('TideAmplitude'));
});
const sums=fs.readFileSync(path.join(kit,'SHA256SUMS.txt'),'utf8').trim().split(/\r?\n/);
check('KitSums',()=>{for(const line of sums){const m=line.match(/^([0-9a-f]{64})  (.+)$/);assert.ok(m);assert.equal(sha(path.join(kit,m[2])),m[1]);}});
const report={node:process.version,checks:results.length,passed:results.length,failed:0,results};
if(process.argv[2]){const dest=path.resolve(process.argv[2]);fs.mkdirSync(dest,{recursive:true});fs.writeFileSync(path.join(dest,'kit-check.json'),JSON.stringify(report,null,2)+'\n');}
console.log(JSON.stringify(report));

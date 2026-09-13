// Historical pass04/pass05 face checks. Current eyes are covered by check-eye-refinement.cjs.
const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert/strict'),crypto=require('crypto');
const dir=__dirname,{context:c}=require('./load-study.cjs').loadStudy({finishPass:'before'});
const Before=c.CharacterHeadStudy;
vm.runInContext(fs.readFileSync(path.join(dir,'sources/face-rig-pass05.js'),'utf8'),c);
const After=c.CharacterHeadStudy,E=c.CastViewerEngine,plain=x=>JSON.parse(JSON.stringify(x));
const expressions=Object.keys(After.EXPRESSIONS);
assert.equal(expressions.length,8);
let stateSamples=0,headPairs=0,stationaryVertices=0,refinedVertices=0;
for(const expr of expressions)for(const seed of [0,17,48,296])for(let i=0;i<600;i++){
 const t=i*.05,options={expr,seed,talk:i%2===0};
 assert.deepEqual(plain(After.life(t,options)),plain(Before.life(t,options)),'Face timing or expression intent changed');
 const speaking=After.life(t,{expr,seed,talk:true}),quiet=After.life(t,{expr,seed});
 assert.equal(speaking.brow,quiet.brow,'Speech moved the brow');assert.equal(speaking.raise,quiet.raise);stateSamples++;
}
function mask(st,build={}){
 const p=new Uint8Array(192*192);for(let y=0;y<192;y++)for(let x=0;x<192;x++)p[y*192+x]=After.sample((x+.5)/192,(y+.5)/192,st,build);return p;
}
const hash=x=>crypto.createHash('sha256').update(x).digest('hex');
assert.equal(new Set(expressions.map(expr=>hash(mask(After.life(0,{expr,gaze:[0,0]}))))).size,8,'An expression lost its distinct surface mask');
const speech=new Map();for(let i=0;i<70;i++){const st=After.life(i/62,{talk:true,gaze:[0,0]});speech.set(st.mouth,hash(mask(st)));}
assert.equal(speech.size,4);assert.equal(new Set(speech.values()).size,4,'Speech mouth silhouettes collapsed');
const closed=mask(After.life(0,{lid:1,gaze:[0,0]}));
assert(!closed.some(i=>i===2||i===3||i===10),'Fully closed blink leaks eye material');
let gazeChecks=0;
for(const shape of ['round','wide','sharp','narrow','droop']){
 const b={eyeShape:shape,browShape:'none'},states=[[0,0],[-1,0],[1,0],[0,-1],[0,1]].map(gaze=>mask({gaze,mouth:'neutral'},b));
 for(const m of states.slice(1))for(let i=0;i<m.length;i++)assert.equal(m[i]===0,states[0][i]===0,'Gaze changed the socket silhouette');
 function centroid(p){let n=0,x=0,y=0;for(let i=0;i<p.length;i++)if(p[i]===10){n++;x+=i%192;y+=Math.floor(i/192);}assert(n);return[x/n,y/n];}
 const centers=states.map(centroid);
 assert(centers[1][0]<centers[0][0]&&centers[2][0]>centers[0][0],'Horizontal gaze disappeared');
 assert(centers[3][1]<centers[0][1]&&centers[4][1]>centers[0][1],'Vertical gaze disappeared');gazeChecks+=4;
}
const reports=[{pass:'04',samples:0,whiteOnly:0,scleraPixels:0,darkEyePixels:0},{pass:'05',samples:0,whiteOnly:0,scleraPixels:0,darkEyePixels:0}];
for(const key of E.cast){
 const characters=[Before,After].map(headStudy=>E.create(key,{finish:false,headStudy}));
 assert.deepEqual(plain(characters[0].bind.bones),plain(characters[1].bind.bones),'Art changed the skeleton');
 assert.deepEqual(plain(characters[0].build),plain(characters[1].build),'Art changed the identity recipe');
 const heads=[Before,After].map(H=>H.createHead(characters[0].headBuild,[0,0,0]));
 assert.equal(heads[0].length,heads[1].length,'Face topology changed');
 const scale=After.C.headScale*characters[0].headBuild.headSize;
 for(let i=0;i<heads[0].length;i++)for(let j=0;j<heads[0][i].v.length;j++){
  const a=heads[0][i].v[j],b=heads[1][i].v[j];assert(b.every(Number.isFinite));
  if(a[2]/scale>=-.110){assert.deepEqual(plain(a),plain(b),'Upper face / crown contact moved');stationaryVertices++;}
  else if(JSON.stringify(a)!==JSON.stringify(b))refinedVertices++;
 }
 headPairs++;
 for(const ppm of [32,64])for(const angle of [0,25,35,90,325,335])for(const phase of [0,.5]){
  for(let ix=0;ix<2;ix++){
   const p=characters[ix],H=[Before,After][ix],posed=E.pose(p,'idle',0),w=Math.round(ppm*1.3),h=Math.round(ppm*1.95);
   const r=c.raster(posed.faces,p.mats,{w,h,cx:w/2+phase,cy:h-8,scale:ppm,angle,elev:40,mode:'proposal',surface:(u,v)=>H.sample(u,v,H.life(0,{expr:'neutral',gaze:[0,0]}),p.headBuild),surfaceColours:p.colours});
   let white=0,dark=0;for(const ink of r.featureBuffer){if(ink===2)white++;if(ink===3||ink===(ix?10:1))dark++;}
   // Pass04 shared index1 for pupil/lash: including both makes its baseline conservative.
   const report=reports[ix];report.samples++;report.scleraPixels+=white;report.darkEyePixels+=dark;if(white&&!dark)report.whiteOnly++;
  }
 }
}
assert(refinedVertices>0&&stationaryVertices>refinedVertices,'Jaw refinement must be bounded below the upper face');
assert.equal(reports[1].whiteOnly,0,'Isolated white eyes returned in the sampled fit/view matrix');
assert(reports[1].scleraPixels<reports[0].scleraPixels*.65,'The white eye clusters still dominate');
assert(reports[1].darkEyePixels>reports[0].darkEyePixels,'The dark eye cluster did not improve');
console.log(JSON.stringify({stateSamples,headPairs,stationaryVertices,refinedVertices,distinctExpressions:8,distinctSpeechMouths:4,gazeDirectionsChecked:gazeChecks,raster:reports,limits:'Raster samples use neutral idle at two pixel phases; profile or hair/hat occlusion may hide eyes. Pass04 dark count conservatively includes lashes. No Unity claims.'}));

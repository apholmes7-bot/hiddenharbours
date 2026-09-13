// Reproducible pass04/pass05 visual comparison and body-finish safety checks. Node stdlib only.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),assert=require('assert/strict');
const {loadStudy}=require('./load-study.cjs'),{raster,png}=require('./sources/face-render.cjs');
const dir=__dirname,out=path.join(dir,'review','character-finish');fs.mkdirSync(out,{recursive:true});
const contexts=[loadStudy({finishPass:'before'}).context,loadStudy().context];
const engines=contexts.map(c=>c.CastViewerEngine),cast=engines[0].cast,clips=Object.keys(engines[0].animations);
assert.deepEqual(Array.from(engines[1].cast),Array.from(cast));
assert.deepEqual(Object.keys(engines[1].animations),clips);
const characters=engines.map(e=>Object.fromEntries(cast.map(k=>[k,e.create(k)])));
const controls=Object.fromEntries(cast.map(k=>[k,engines[1].create(k,{finish:false})]));
const headings=[0,45,90,135,180,225,270,315],densities=[64,32],samples=13;
const actions=[{clip:'walk',u:.25},{clip:'haul',u:.5},{clip:'drive',u:.25},{clip:'mountDown',u:.5}];
for(const a of actions)assert(clips.includes(a.clip),'Comparison names a real authored clip');
const plain=v=>JSON.parse(JSON.stringify(v)),eq=(a,b,message)=>assert.deepEqual(plain(a),plain(b),message);
const protectedPart=p=>/^(head|neck|hand_[LR]|thumb_[LR]|foot_[LR])$/.test(p);
const paletteKeys=['skin','hair','beard','beardD','eyes','eye','brow','stub','noseLight','noseShadow'];
const report={Scope:'Offline art comparison. No Unity renderer, gameplay contact, performance or attractiveness score.',
  Before:'Preserved face-rig-pass04.js without CharacterFinish',After:'Current face-rig.js with CharacterFinish',
  Control:'Current face-rig.js without CharacterFinish; isolates body finish from intentional face edits',
  Densities:densities,Headings:headings,CameraElevationDegrees:40,Cast:cast,Animations:clips,
  SamplesPerClip:samples,PosePairs:0,BonePosePairs:0,ProtectedVertexSamples:0,RasterChecks:0,
  EmptyRasters:[],ClippedRasters:[],NewDegenerateTriangles:[],NewDegenerateTrianglesSincePass04:[],Bounds:{},Geometry:{},Plates:[]};
const merge=(b,p)=>{for(let d=0;d<3;d++){b.Min[d]=Math.min(b.Min[d],p[d]);b.Max[d]=Math.max(b.Max[d],p[d]);}};
const emptyBounds=()=>({Min:[Infinity,Infinity,Infinity],Max:[-Infinity,-Infinity,-Infinity]});
function triangleArea2(a,b,c){const u=b.map((v,i)=>v-a[i]),v=c.map((x,i)=>x-a[i]);return Math.hypot(u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]);}
for(const key of cast){
  const after=characters[1][key],control=controls[key];
  eq(after.build,control.build,key+' body finish must not rewrite identity');
  eq(after.bind.bones,control.bind.bones,key+' must retain bind skeleton');
  assert.equal(after.faces.length,control.faces.length,key+' finishing retains face topology');
  for(const name of paletteKeys)eq(after.mats[name]??null,control.mats[name]??null,key+' immutable '+name+' material');
  const bounds=[emptyBounds(),emptyBounds()],protectedFaces=[];
  report.Bounds[key]={Before:bounds[0],After:bounds[1]};
  report.Geometry[key]={Bones:after.bind.bones.length,Faces:after.faces.length,Vertices:after.faces.reduce((n,f)=>n+f.v.length,0),
    Triangles:after.faces.reduce((n,f)=>n+f.v.length-2,0),ProtectedFaces:0,ExistingDegenerateTriangleSamples:0,Pass04DegenerateTriangleSamples:0};
  after.faces.forEach((f,i)=>{
    assert.equal(f.part,control.faces[i].part,key+' stable part at '+i);
    eq(f.bone,control.faces[i].bone,key+' unchanged weights '+i);
    if(protectedPart(f.part)){eq(f.v,control.faces[i].v,key+' protected rest geometry '+f.part);protectedFaces.push(i);}
  });
  report.Geometry[key].ProtectedFaces=protectedFaces.length;
  for(const clip of clips)for(let s=0;s<samples;s++){
    const u=s/(samples-1),beforePose=engines[0].pose(characters[0][key],clip,u),afterPose=engines[1].pose(after,clip,u),controlPose=engines[1].pose(control,clip,u);
    eq(afterPose.bones,controlPose.bones,key+'/'+clip+' body finish must retain animation/contact anchors');report.BonePosePairs++;
    for(const i of protectedFaces){eq(afterPose.faces[i].v,controlPose.faces[i].v,key+'/'+clip+' protected posed geometry');report.ProtectedVertexSamples+=afterPose.faces[i].v.length;}
    for(const [ix,pose] of [beforePose,afterPose].entries())for(const f of pose.faces)for(const p of f.v){assert(p.every(Number.isFinite),key+'/'+clip+' nonfinite vertex');merge(bounds[ix],p);}
    assert.equal(afterPose.faces.length,beforePose.faces.length,key+' pass04/pass05 comparison requires matching topology');
    afterPose.faces.forEach((f,i)=>{
      assert(f.v.length>=3,key+'/'+clip+' malformed polygon');
      for(let t=1;t<f.v.length-1;t++){
        const area=triangleArea2(f.v[0],f.v[t],f.v[t+1]),c=controlPose.faces[i];
        assert(Number.isFinite(area),key+'/'+clip+' invalid triangle');
        const old=beforePose.faces[i],oldArea=triangleArea2(old.v[0],old.v[t],old.v[t+1]);
        if(oldArea<1e-12)report.Geometry[key].Pass04DegenerateTriangleSamples++;
        if(area<1e-12){
          if(oldArea>=1e-12)report.NewDegenerateTrianglesSincePass04.push({key,clip,u,face:i,triangle:t});
          if(triangleArea2(c.v[0],c.v[t],c.v[t+1])>=1e-12)report.NewDegenerateTriangles.push({key,clip,u,face:i,triangle:t});
          else report.Geometry[key].ExistingDegenerateTriangleSamples++;
        }
      }
    });
    report.PosePairs++;
  }
}
assert.equal(report.NewDegenerateTriangles.length,0,'Body finish introduced degenerate triangles');
assert.equal(report.NewDegenerateTrianglesSincePass04.length,0,'Pass05 introduced degenerate triangles relative to pass04');

// Pixel labels keep the exported PNGs understandable without the companion HTML.
const font={
 A:['01110','10001','10001','11111','10001','10001','10001'],B:['11110','10001','10001','11110','10001','10001','11110'],C:['01111','10000','10000','10000','10000','10000','01111'],
 D:['11110','10001','10001','10001','10001','10001','11110'],E:['11111','10000','10000','11110','10000','10000','11111'],F:['11111','10000','10000','11110','10000','10000','10000'],
 G:['01111','10000','10000','10111','10001','10001','01111'],H:['10001','10001','10001','11111','10001','10001','10001'],I:['111','010','010','010','010','010','111'],
 J:['00111','00010','00010','00010','10010','10010','01100'],K:['10001','10010','10100','11000','10100','10010','10001'],L:['10000','10000','10000','10000','10000','10000','11111'],
 M:['10001','11011','10101','10101','10001','10001','10001'],N:['10001','11001','10101','10011','10001','10001','10001'],O:['01110','10001','10001','10001','10001','10001','01110'],
 P:['11110','10001','10001','11110','10000','10000','10000'],Q:['01110','10001','10001','10001','10101','10010','01101'],R:['11110','10001','10001','11110','10100','10010','10001'],
 S:['01111','10000','10000','01110','00001','00001','11110'],T:['11111','00100','00100','00100','00100','00100','00100'],U:['10001','10001','10001','10001','10001','10001','01110'],
 V:['10001','10001','10001','10001','10001','01010','00100'],W:['10001','10001','10001','10101','10101','10101','01010'],X:['10001','10001','01010','00100','01010','10001','10001'],
 Y:['10001','10001','01010','00100','00100','00100','00100'],Z:['11111','00001','00010','00100','01000','10000','11111'],
 '0':['111','101','101','101','101','101','111'],'1':['010','110','010','010','010','010','111'],'2':['111','001','001','111','100','100','111'],'3':['111','001','001','111','001','001','111'],
 '4':['101','101','101','111','001','001','001'],'5':['111','100','100','111','001','001','111'],'6':['111','100','100','111','101','101','111'],'7':['111','001','001','010','010','010','010'],
 '8':['111','101','101','111','101','101','111'],'9':['111','101','101','111','001','001','111'],'-':['000','000','000','111','000','000','000'],'.':['0','0','0','0','0','1','1'],
 '/':['00001','00010','00010','00100','01000','01000','10000']};
function canvas(w,h){const pixels=new Uint8ClampedArray(w*h*4);for(let i=0;i<w*h;i++)pixels.set([231,234,222,255],i*4);return {pixels,w,h};}
function label(image,text,x,y){for(const ch of text.toUpperCase()){
  const glyph=font[ch];if(glyph){glyph.forEach((row,j)=>[...row].forEach((v,i)=>{if(v==='1'&&x+i<image.w&&y+j<image.h)image.pixels.set([37,58,59,255],((y+j)*image.w+x+i)*4);}));x+=glyph[0].length+1;}else x+=4;
}}
function paste(target,r,x,y,zoom=1){for(let j=0;j<r.h*zoom;j++)for(let i=0;i<r.w*zoom;i++){
  const from=(Math.floor(j/zoom)*r.w+Math.floor(i/zoom))*4;if(r.pixels[from+3])target.pixels.set(r.pixels.subarray(from,from+4),((y+j)*target.w+x+i)*4);
}}
function enlarged(image,zoom){const result=canvas(image.w*zoom,image.h*zoom);paste(result,image,0,0,zoom);return result;}
function projected(p,angle){const a=angle*Math.PI/180,e=40*Math.PI/180;return [p[0]*Math.cos(a)-p[1]*Math.sin(a),(p[0]*Math.sin(a)+p[1]*Math.cos(a))*Math.sin(e)-p[2]*Math.cos(e)];}
function picture(pass,key,ppm,view,kind,frame){
  const character=characters[pass][key],pose=engines[pass].pose(character,view.clip,view.u),H=character.headStudy;
  const faces=kind==='face'?pose.faces.filter(f=>f.part==='head'):pose.faces;
  const shift=kind==='face'?pose.head:[0,0,0],centred=faces.map(f=>({...f,v:f.v.map(p=>p.map((v,i)=>v-shift[i]))}));
  const w=Math.ceil((frame.maxX-frame.minX)*ppm)+4,h=Math.ceil((frame.maxY-frame.minY)*ppm)+4;
  const state=H.life(0,{expr:'neutral',gaze:[0,0],anim:view.clip});
  const r=raster(centred,character.mats,{w,h,cx:2-frame.minX*ppm,cy:2-frame.minY*ppm,scale:ppm,angle:view.angle,elev:40,mode:'proposal',outline:true,
    surface:(u,v)=>H.sample(u,v,state,character.headBuild),surfaceColours:character.colours});
  let lit=0,edge=0;for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(r.pixels[(y*w+x)*4+3]){lit++;if(x===0||y===0||x===w-1||y===h-1)edge++;}
  const id={pass,key,ppm,kind,...view};report.RasterChecks++;if(!lit)report.EmptyRasters.push(id);if(edge)report.ClippedRasters.push({...id,edge});
  return r;
}
function plate(kind,views,ppm,name){
  const frame={minX:Infinity,maxX:-Infinity,minY:Infinity,maxY:-Infinity};
  for(const key of cast)for(let pass=0;pass<2;pass++)for(const view of views){
    const pose=engines[pass].pose(characters[pass][key],view.clip,view.u),shift=kind==='face'?pose.head:[0,0,0];
    for(const f of pose.faces){if(kind==='face'&&f.part!=='head')continue;for(const p of f.v){const q=projected(p.map((v,i)=>v-shift[i]),view.angle);frame.minX=Math.min(frame.minX,q[0]);frame.maxX=Math.max(frame.maxX,q[0]);frame.minY=Math.min(frame.minY,q[1]);frame.maxY=Math.max(frame.maxY,q[1]);}}
  }
  const zoom=ppm===32?2:1,cellW=(Math.ceil((frame.maxX-frame.minX)*ppm)+4)*zoom+6,cellH=(Math.ceil((frame.maxY-frame.minY)*ppm)+4)*zoom+14;
  const left=62,top=32,board=canvas(left+cellW*views.length*2,top+cellH*cast.length);
  label(board,'PASS04 A / PASS05 B - '+name+' - '+ppm+' PX/M',4,4);
  views.forEach((v,i)=>{label(board,(v.label||String(v.angle))+' A',left+i*2*cellW,18);label(board,(v.label||String(v.angle))+' B',left+(i*2+1)*cellW,18);});
  cast.forEach((key,row)=>{label(board,key,3,top+row*cellH+4);views.forEach((view,column)=>[0,1].forEach(pass=>{
    paste(board,picture(pass,key,ppm,view,kind,frame),left+(column*2+pass)*cellW,top+row*cellH,zoom);
  }));});
  const filename=name+'-'+ppm+'.png';png(board,path.join(out,filename));
  if(name!=='body-actions')png(enlarged(board,2),path.join(out,name+'-'+ppm+'-enlarged.png'));
  report.Plates.push({File:filename,Kind:kind,PixelsPerMetre:ppm,DisplayZoom:zoom,FrameMetres:frame,Views:views});
}
for(const ppm of densities){
  plate('body',headings.slice(0,4).map(angle=>({clip:'idle',u:0,angle})),ppm,'body-front-turns');
  plate('body',headings.slice(4).map(angle=>({clip:'idle',u:0,angle})),ppm,'body-back-turns');
  plate('face',headings.map(angle=>({clip:'idle',u:0,angle})),ppm,'face-turns');
  plate('body',actions.flatMap(a=>[45,225].map(angle=>({...a,angle,label:a.clip.toUpperCase().slice(0,3)+' '+angle}))),ppm,'body-actions');
}
assert.equal(report.EmptyRasters.length,0,'Empty comparison render');assert.equal(report.ClippedRasters.length,0,'Clipped comparison render');
const sources=['load-study.cjs','cast-engine.js','character-finish.js','character-finish.json','sources/face-rig-pass04.js','sources/face-rig.js','sources/face-render.cjs','compare-character-finish.cjs'];
report.SourceHashes=Object.fromEntries(sources.map(f=>[f,crypto.createHash('sha256').update(fs.readFileSync(path.join(dir,f),'utf8').replaceAll('\r\n','\n')).digest('hex')]));
report.Limits=['Anchor equality covers authored rig bones and preserved hand/foot geometry. It is not contact with an external tool, boat, chair or ground.',
 'Triangle checks use model-space area; naturally edge-on projected faces are not errors. Existing zero-area triangles are counted rather than silently called valid.',
 'Raster crop is fixed across before/after and all cast for each plate. No scaling per character. The32px/m crops are enlarged2x using nearest-neighbour display only.',
 'All35 animation clips are sampled13 times per cast for geometry/anchors. Raster clipping is checked on the published turn/action plates, not every possible continuous pose.'];
fs.writeFileSync(path.join(out,'comparison-measurements.json'),JSON.stringify(report,null,2)+'\n');
fs.writeFileSync(path.join(out,'comparison.html'),'<!doctype html><html lang="en"><meta charset="utf-8"><title>Character finish04 /05</title><style>body{background:#e7eade;color:#253a3b;font:16px system-ui;margin:24px}img{image-rendering:pixelated;max-width:100%;height:auto}p{max-width:90ch}section{margin:32px 0}</style><h1>Character finish · pass04 / pass05</h1><p>Each pair is A: preserved pass04, B: current pass05. All ten cast members, 40° orthographic preview camera, identical pose/time and metre scale. 64px/m preferred detail;32px/m game-density check enlarged2x for display. This is offline art evidence, not Unity acceptance.</p>'+report.Plates.map(p=>'<section><h2>'+p.File+'</h2><img src="'+p.File+'" alt="'+p.File+'; paired before/after all ten cast members"></section>').join('')+'<p>'+report.Limits.join(' ')+'</p></html>');
console.log(JSON.stringify({Passed:true,Output:path.join(out,'comparison.html'),PosePairs:report.PosePairs,BonePosePairs:report.BonePosePairs,ProtectedVertexSamples:report.ProtectedVertexSamples,RasterChecks:report.RasterChecks,NewDegenerateTriangles:report.NewDegenerateTriangles.length,Clipped:report.ClippedRasters.length,Empty:report.EmptyRasters.length}));

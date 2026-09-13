// Reproducible head comparisons at fixed metre scale; no browser or image packages.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {loadStudy}=require('./load-study.cjs'),{raster,png,pngBuffer}=require('./sources/face-render.cjs');
const contexts=[loadStudy({finishPass:'before'}).context,loadStudy().context];
const out=path.join(__dirname,'review');fs.mkdirSync(out,{recursive:true});
const views=[0,35,90,145,180,270,325],cast=contexts[0].CastViewerEngine.cast;
const characters=contexts.map(c=>Object.fromEntries(cast.map(k=>[k,c.CastViewerEngine.create(k)])));
function render(ix,key,ppm,angle,expr='neutral',buildOverrides={}){
 const c=contexts[ix],H=c.CharacterHeadStudy,character=characters[ix][key],b={...character.headBuild,...buildOverrides};
 const faces=H.createHead(b,[0,0,0]),w=Math.ceil(ppm*.76),h=Math.ceil(ppm*.76);
 const state=H.life(0,{expr,gaze:[0,0]});
 return raster(faces,c.CastViewerEngine.materials({...character.build,...buildOverrides}),{w,h,cx:w/2+.5,cy:h/2,scale:ppm,angle,elev:40,mode:'proposal',outline:true,surface:(u,v)=>H.sample(u,v,state,b),surfaceColours:H.colours(b)});
}
function board(ppm){
 const cell=Math.ceil(ppm*.76),gap=4,w=views.length*2*(cell+gap),h=cast.length*(cell+gap),pixels=new Uint8ClampedArray(w*h*4);
 for(let i=0;i<w*h;i++)pixels.set([237,232,219,255],i*4);
 cast.forEach((key,row)=>views.forEach((angle,col)=>contexts.forEach((_,ix)=>{
   const r=render(ix,key,ppm,angle),ox=(col*2+ix)*(cell+gap),oy=row*(cell+gap);
   for(let y=0;y<cell;y++)for(let x=0;x<cell;x++){const a=(y*cell+x)*4;if(r.pixels[a+3])pixels.set(r.pixels.subarray(a,a+4),((oy+y)*w+ox+x)*4);}
 })));
 png({pixels,w,h},path.join(out,'face-turns-'+ppm+'.png'));
 const zoom=ppm===64?2:4,zoomed=new Uint8ClampedArray(w*h*zoom*zoom*4);
 for(let y=0;y<h*zoom;y++)for(let x=0;x<w*zoom;x++){const i=(Math.floor(y/zoom)*w+Math.floor(x/zoom))*4;zoomed.set(pixels.subarray(i,i+4),(y*w*zoom+x)*4);}
 png({pixels:zoomed,w:w*zoom,h:h*zoom},path.join(out,'face-turns-'+ppm+'-enlarged.png'));
}
for(const ppm of [64,32])board(ppm);
// Expression contact sheet: Fisher and dark-skinned Deck boss, before/after at both densities.
{
 const expressions=Object.keys(contexts[1].CharacterHeadStudy.EXPRESSIONS),cell=54,w=cell*8,h=cell*expressions.length,pixels=new Uint8ClampedArray(w*h*4);
 for(let i=0;i<w*h;i++)pixels.set([237,232,219,255],i*4);
 expressions.forEach((expr,row)=>['fisher','deckboss'].forEach((key,k)=>[64,32].forEach((ppm,d)=>[0,1].forEach(ix=>{
   const r=render(ix,key,ppm,0,expr),zoom=ppm===32?2:1,ox=(k*4+d*2+ix)*cell,oy=row*cell;
   for(let y=0;y<r.h*zoom;y++)for(let x=0;x<r.w*zoom;x++){const i=(Math.floor(y/zoom)*r.w+Math.floor(x/zoom))*4;if(r.pixels[i+3])pixels.set(r.pixels.subarray(i,i+4),((oy+y)*w+ox+x)*4);}
 }))));
 const big=new Uint8ClampedArray(w*h*16);for(let y=0;y<h*2;y++)for(let x=0;x<w*2;x++){const i=(Math.floor(y/2)*w+Math.floor(x/2))*4;big.set(pixels.subarray(i,i+4),(y*w*2+x)*4);}
 png({pixels:big,w:w*2,h:h*2},path.join(out,'face-expressions-enlarged.png'));
}
let rows='';
for(const key of cast){
 let cells='';for(const angle of views)for(let ix=0;ix<2;ix++){
   const r=render(ix,key,64,angle),src='data:image/png;base64,'+pngBuffer(r).toString('base64');
   cells+='<td><img alt="'+key+' '+(ix?'after':'before')+' '+angle+' degrees" src="'+src+'"></td>';
 }
 rows+='<tr><th>'+characters[0][key].build.label+'</th>'+cells+'</tr>';
}
let expressions='';for(const expr of Object.keys(contexts[1].CharacterHeadStudy.EXPRESSIONS)){
 expressions+='<tr><th>'+expr+'</th>';
 for(const ppm of [64,32])for(const ix of [0,1]){
  const src='data:image/png;base64,'+pngBuffer(render(ix,'fisher',ppm,0,expr)).toString('base64');
  expressions+='<td><img alt="'+expr+' '+ppm+' '+ix+'" src="'+src+'"></td>';
 }
 expressions+='</tr>';
}
fs.writeFileSync(path.join(out,'face-comparison.html'),'<!doctype html><html lang="en"><meta charset="utf-8"><title>Hidden Harbours · minor face pass</title><style>body{font:16px system-ui;margin:24px;background:#ede8db;color:#263e42}table{border-collapse:collapse;margin:20px 0}th,td{padding:5px;text-align:center}img{width:98px;image-rendering:pixelated}tr:nth-child(even){background:#e2ddd0}summary{cursor:pointer}p{max-width:90ch}</style><h1>Minor face pass · before / after</h1><p>Detail · 64 px/m, 40° camera, unchanged metre scale. Each turn shows pass 04 then pass 06. Rounded eye openings, focused pupils, balanced light corners, softer jaw planes and a more readable resting mouth. Expression, gaze and blink timing are preserved; the whole-cast comparison also shows garment tailoring and material finish. This is an offline study, not a Unity capture.</p><table><thead><tr><th>Cast</th>'+views.map(a=>'<th colspan="2">'+a+'° · before / after</th>').join('')+'</tr></thead>'+rows+'</table><h2>Expressions · Fisher</h2><table><tr><th>Expression</th><th>64 before</th><th>64 after</th><th>32 before</th><th>32 after</th></tr>'+expressions+'</table><h2>32 px/m turn check</h2><p>Rows: '+cast.map(k=>characters[0][k].build.label).join(', ')+'. Columns: '+views.map(a=>a+'° before / after').join(', ')+'.</p><img style="width:100%;max-width:1176px" alt="All ten characters before and after at 32 px/m" src="face-turns-32.png"><p>Geometry is rendered directly at each density, then enlarged with nearest-neighbour sampling. The gameplay camera, shader and actual ashore/aboard appearance still need production validation.</p></html>');
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(path.join(__dirname,f))).digest('hex');
console.log(JSON.stringify({review:path.join(out,'face-comparison.html'),cast:cast.length,views:views.length,densities:[64,32],sourceHashes:{before:hash('sources/face-rig-pass04.js'),after:hash('sources/face-rig.js')}}));

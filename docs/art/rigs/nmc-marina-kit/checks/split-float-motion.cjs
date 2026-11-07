'use strict';
// Render the delivered geometry in three layers. Decks ride; piles and anchors do not.
// Existing float sprite identities, rectangles and pivots stay intact.
const fs=require('fs'),path=require('path');
const B=require('./bake-water-side.cjs'),W=B.C.WharfRig2;
const file=path.join(B.OUT,'nmcWaterSide.today.json');
const output=path.join(B.ROOT,'Evidence~/nmc-h5/phase-b/split');
fs.mkdirSync(output,{recursive:true});
const today=JSON.parse(fs.readFileSync(file,'utf8'));
if(today.parts.some(p=>p.motionLayer))throw new Error('Start with the unsplit native bake');
const SKY=B.C.WeatherSky.at({time:14,cloud:0.1,wind:0.3});
for(const part of today.parts.filter(p=>p.floating)){
 const source=B.parts.find(p=>p.key===part.key);
 const layers=['back','body','front'].map(layer=>{
  const q=JSON.parse(JSON.stringify(part));
  if(layer!=='body'){
   q.key=part.key+'_'+layer;q.name=part.name+'_'+layer;q.floating=false;
   q.frames.forEach(f=>f.file=f.file.replace(part.key,q.key));
   q.sheets.forEach(s=>s.file=s.file.replace(part.key,q.key));
   delete q.walkPolygon;delete q.fittings;
  }
  else {
   q.frames.forEach(f=>f.file=f.file.replace(part.key,part.key+'_body'));
   q.sheets.forEach(s=>s.file=s.file.replace(part.key,part.key+'_body'));
  }
  q.motionLayer=layer;
  return {part:q,pages:q.sheets.map(s=>({sheet:s,rgba:Buffer.alloc(s.width*s.height*4)}))};
 });
 for(let frame=0;frame<8;frame++){
  const original=part.frames.find(f=>f.frame===frame),dir=original.dir;
  for(const layer of layers){
   W.clearCache();
   const options={dir,ppu:32,tide:0,shelter:0,wind:{w:0,gust:0,dir:1}};
   const model=W.model(source.modelKey,options),camera=W.cam(dir,32);
   const staticFaces=model.static;
   const selected=staticFaces.filter(f=>{
    const centre=f.v.reduce((a,p)=>[a[0]+p[0]/f.v.length,a[1]+p[1]/f.v.length,0],[0,0,0]);
    const front=W.proj(centre,camera)[1]>=0;
    return layer.part.motionLayer==='front'?front:!front;
   });
   if(layer.part.motionLayer==='body')model.static=[];
   else {model.static=selected;model.moving=[];model.groups={};model.gangs=[];}
   const fr=W.frame(source.modelKey,options),rgba=Buffer.from(W.view(fr,'lit',SKY,{outline:false}));
   const output=layer.part.frames.find(f=>f.frame===frame),page=layer.pages.find(p=>p.sheet.file===output.file);
   const [left,bottom,cw,ch]=output.rect,top=page.sheet.height-bottom-ch;
   let drawn=0;
   for(let y=0;y<fr.h;y++)for(let x=0;x<fr.w;x++){
    const i=(y*fr.w+x)*4;if(!rgba[i+3])continue;
    const dx=x-fr.px+original.pivot[0],dy=y-fr.py+original.pivot[1];
    if(dx<0||dy<0||dx>=cw||dy>=ch)throw new Error(part.key+' '+layer.part.motionLayer+' exceeds original frame '+frame);
    const k=((top+dy)*page.sheet.width+left+dx)*4;rgba.copy(page.rgba,k,i,i+4);drawn++;
   }
   if(!drawn && layer.part.motionLayer==='body')throw new Error(part.key+' empty body frame '+frame);
   console.log(part.key,frame,layer.part.motionLayer,drawn);
   W.clearCache();if(global.gc)global.gc();
  }
 }
 for(const layer of layers)for(const page of layer.pages){
  fs.writeFileSync(path.join(output,page.sheet.file),B.png(page.sheet.width,page.sheet.height,page.rgba));
  page.sheet.rgbaSha256=B.sha(page.rgba);
 }
 Object.assign(part,layers[1].part);
 const piece=today.pieces.find(p=>p.key===part.harbour),index=piece.parts.indexOf(part.key);
 piece.parts.splice(index,1,layers[0].part.key,part.key,layers[2].part.key);
 today.parts.push(layers[0].part,layers[2].part);
}
today.motionLayers='fixed back / tide body / fixed front; original sprite identities retained';
fs.writeFileSync(path.join(output,'nmcWaterSide.today.json'),JSON.stringify(today,null,1)+'\n');
console.log('SPLIT',today.parts.length,'module/pose variants');

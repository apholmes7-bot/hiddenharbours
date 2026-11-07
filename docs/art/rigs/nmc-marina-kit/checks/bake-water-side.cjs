'use strict';
// The delivered geometry, through the existing SetPieceDef frame/variant shape.
// Each module keeps its own pivot and atlas pages; a harbour remains one Def.
const fs=require('fs'),path=require('path'),vm=require('vm'),zlib=require('zlib'),crypto=require('crypto');
const KIT=path.resolve(__dirname,'..'),ROOT=path.resolve(KIT,'../../../..');
const OUT=path.join(ROOT,'Assets/_Project/Art/Sprites/NineMileCreek/WaterSide');
const SCRIPTS=['../nmc-set-pieces-kit/lib/weatherSky.js','../nmc-set-pieces-kit/lib/coastalPass.js',
 '../nmc-shipyard-kit/wharfRig2.geo.js','../nmc-shipyard-kit/wharfRig2.fam.js','../nmc-shipyard-kit/wharfRig2.verbs.js',
 '../nmc-shipyard-kit/wharfRig2.js','../nmc-shipyard-kit/wharfRig2.kit.js','marinaRig.js','marinaBoats.js','marinaKit.js','nmcWharf.js'];
const C=vm.createContext({console});
for(const f of SCRIPTS)vm.runInContext(fs.readFileSync(path.join(KIT,f),'utf8'),C,{filename:f});
const N=C.NmcWharf,K=C.WharfRig2Kit,W=C.WharfRig2,G=C.WharfGeo2;
N.defineHarbours();
const SPECS=[['west','nmc_west','west'],['fleet','nmc_fleet','fleet'],['east','nmc_east','east'],['arm_w','nmc_armW','armW'],['arm_e','nmc_armE','armE']];
const clone=o=>JSON.parse(JSON.stringify(o)),round=n=>Math.round(n*1e6)/1e6;
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const parts=[],pieces=[];
function bounds(points){const lo=[Infinity,Infinity,Infinity],hi=[-Infinity,-Infinity,-Infinity];for(const p of points)for(let a=0;a<3;a++){lo[a]=Math.min(lo[a],p[a]);hi[a]=Math.max(hi[a],p[a]);}return {min:lo.map(round),max:hi.map(round)};}
function point(m,p){const c=Math.cos(m.rot),s=Math.sin(m.rot);return [round(m.at[0]+c*p[0]-s*p[1]),round(m.at[1]+s*p[0]+c*p[1])];}
for(const [key,source,originKey] of SPECS){
 const H=K.HARBOURS[source],L=H.layout({tideRange:H.tideRange}),origin=N.H[originKey].OX;
 const piece={key,id:'harbour.'+key,origin:[origin,0],parts:[],anchors:{},source,sourceLayout:clone(L)};
 // No boat is baked into a harbour: the owner register places each resident once.
 delete piece.sourceLayout.boats;
 for(const input of L.mods){
  const m=clone(input),resolved=G.resolve(m.key,m.o),floating=!!resolved.floating;
  const at=[round(origin+m.at[0]),m.at[1]],oldAt=m.at.slice();
  const datum=floating?2.2:resolved.deckZ;
  m.at=[0,0];m.o.deckZ=resolved.deckZ-datum;
  if(m.o.toeZ!=null)m.o.toeZ-=datum;
  const modelKey='h5_'+key+'_'+m.id;
  K.define(modelKey,{label:'Wharf water side '+key+' '+m.id,tideRange:H.tideRange,
   bed:(x,y)=>H.bed(x+oldAt[0],y+oldAt[1])-datum,layout:()=>({mods:[m],gangs:[],lines:[]})});
  const model=W.model('harbour:'+modelKey,{tide:0,shelter:0});
  const mod=model.modules[0],hx=mod.s.L/2,hy=mod.s.W/2;
  const poly=[[-hx,-hy],[hx,-hy],[hx,hy],[-hx,hy]].map(p=>point(input,p)).map(p=>[round(p[0]+origin),p[1]]);
  let underside=0;for(const face of model.faces)if(face.grp)for(const v of face.v)underside=Math.min(underside,v[2]);
  const p={key:key+'_'+m.id,harbour:key,name:m.id,at,elevation:floating?0:round(datum-2.2),
   floating,freeboard:floating?mod.s.freeboard:0,draught:floating?round(-underside):0,
   bounds:bounds(model.faces.flatMap(f=>f.v)),walkPolygon:poly,fittings:clone(mod.P),
   module:clone(input),frames:[],sheets:[],modelKey:'harbour:'+modelKey};
  piece.parts.push(p.key);parts.push(p);W.clearCache();
 }
 for(const gw of L.gangs){
  const hinge=[round(origin+gw.hinge[0]),gw.hinge[1]],landing=[round(origin+gw.land[0]),gw.land[1]];
  const groupModule=L.mods.find(m=>m.grp===gw.floatGrp&&G.resolve(m.key,m.o).floating);
  const freeboard=G.resolve(groupModule.key,groupModule.o).freeboard;
  const shore=gw.hinge[2]-2.2,lowDrop=shore-(2.2+freeboard),highDrop=shore-(-2.2+freeboard);
  for(let step=0;step<=8;step++){
   const drop=round(lowDrop+(highDrop-lowDrop)*step/8),modelKey='h5_'+key+'_'+gw.id+'_s'+step;
   const g={id:gw.id,hinge:[0,0,0],u:gw.u,L:gw.L,W:gw.W,floatGrp:null,landZ:()=>-drop};
   K.define(modelKey,{label:'Wharf gangway',tideRange:4.4,bed:()=>-10,layout:()=>({mods:[],gangs:[g],lines:[]})});
   const faces=[];G.gangwayFaces(faces,g.hinge,g.u[0],g.u[1],g.L,-drop,g.W,G.frame(G.resolve('pier',{})),G.resolve('pier',{}));
   const p={key:key+'_'+gw.id+'_s'+step,harbour:key,name:gw.id+'_s'+step,at:hinge,elevation:shore,
    gangway:{id:gw.id,step,drop,hinge,landing,width:gw.W,length:gw.L,floatModule:groupModule.id},
    bounds:bounds(faces.flatMap(f=>f.v)),frames:[],sheets:[],modelKey:'harbour:'+modelKey};
   piece.parts.push(p.key);parts.push(p);
  }
 }
 pieces.push(piece);
}
const SKY=C.WeatherSky.at({time:14,cloud:0.1,wind:0.3});
const CRC=new Uint32Array(256);for(let n=0;n<256;n++){let c=n;for(let k=0;k<8;k++)c=c&1?0xedb88320^(c>>>1):c>>>1;CRC[n]=c>>>0;}
function chunk(type,data){const tag=Buffer.from(type),len=Buffer.alloc(4);len.writeUInt32BE(data.length);let c=0xffffffff;for(const b of Buffer.concat([tag,data]))c=CRC[(c^b)&255]^(c>>>8);const sum=Buffer.alloc(4);sum.writeUInt32BE((c^0xffffffff)>>>0);return Buffer.concat([len,tag,data,sum]);}
function png(w,h,rgba){const raw=Buffer.alloc((w*4+1)*h);for(let y=0;y<h;y++)rgba.copy(raw,y*(w*4+1)+1,y*w*4,(y+1)*w*4);const head=Buffer.alloc(13);head.writeUInt32BE(w);head.writeUInt32BE(h,4);head[8]=8;head[9]=6;return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',head),chunk('IDAT',zlib.deflateSync(raw)),chunk('IEND',Buffer.alloc(0))]);}
function bake(part){
 const cells=[];let cw=0,ch=0;
 for(let frame=0;frame<8;frame++){
  const dir=(8-frame)%8,fr=W.frame(part.modelKey,{dir,ppu:32,tide:0,shelter:0,wind:{w:0,gust:0,dir:1}});
  const rgba=Buffer.from(W.view(fr,'lit',SKY,{outline:false}));let x0=fr.w,y0=fr.h,x1=-1,y1=-1;
  for(let y=0;y<fr.h;y++)for(let x=0;x<fr.w;x++)if(rgba[(y*fr.w+x)*4+3]){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);}
  if(x1<0)throw new Error(part.key+' draws nothing at '+dir);
  x0=Math.min(x0,fr.px);y0=Math.min(y0,fr.py);x1=Math.max(x1,fr.px);y1=Math.max(y1,fr.py);
  cw=Math.max(cw,x1-x0+1);ch=Math.max(ch,y1-y0+1);
  cells.push({frame,dir,w:fr.w,h:fr.h,px:fr.px,py:fr.py,x0,y0,x1,y1,rgba});
  W.clearCache();if(global.gc)global.gc();
 }
 const maxCols=Math.floor(4096/cw),maxRows=Math.floor(4096/ch);if(!maxCols||!maxRows)throw new Error(part.key+' cannot fit the 4096 cap');
 const perPage=Math.min(8,maxCols*maxRows);part.cellSize=[cw,ch];
 for(let start=0,page=0;start<8;start+=perPage,page++){
  const frames=cells.slice(start,start+perPage),cols=Math.min(maxCols,frames.length),rows=Math.ceil(frames.length/cols);
  const w=cols*cw,h=rows*ch,rgba=Buffer.alloc(w*h*4),file='WaterSide_'+part.key+'_'+page+'.png';
  for(let j=0;j<frames.length;j++){
   const f=frames[j],left=j%cols*cw,top=Math.floor(j/cols)*ch;
   for(let y=f.y0;y<=f.y1;y++){
    const i=(y*f.w+f.x0)*4,k=((top+y-f.y0)*w+left)*4;
    f.rgba.copy(rgba,k,i,i+(f.x1-f.x0+1)*4);
   }
   part.frames.push({frame:f.frame,dir:f.dir,file,rect:[left,h-top-ch,cw,ch],native:[f.w,f.h],nativePivot:[f.px,f.py],crop:[f.x0,f.y0],pivot:[f.px-f.x0,f.py-f.y0]});
  }
  fs.writeFileSync(path.join(OUT,file),png(w,h,rgba));part.sheets.push({file,width:w,height:h,rgbaSha256:sha(rgba)});
 }
 console.log('BAKED',part.key,part.sheets.length+' pages',cw+'x'+ch);if(global.gc)global.gc();
}
function register(){const raw=fs.readFileSync(path.join(ROOT,'Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.asset'),'utf8');const m=raw.match(/    - Key: nmc\.scene\.harbour\r?\n      Value: '([^\r\n]*)'/);if(!m)throw new Error('The registered harbour option is not readable');return JSON.parse(m[1].replace(/''/g,"'"));}
function write(planOnly,metadataOnly=false){
 fs.mkdirSync(OUT,{recursive:true});
 // The approved westward crib is the final rendered part.
 const order=parts.filter(p=>p.key!=='fleet_crib').concat(parts.filter(p=>p.key==='fleet_crib'));
 if(metadataOnly){const old=JSON.parse(fs.readFileSync(path.join(OUT,'nmcWaterSide.today.json'),'utf8'));for(const p of parts){const q=old.parts.find(q=>q.key===p.key);p.frames=q.frames;p.sheets=q.sheets;p.cellSize=q.cellSize;}}
 else if(!planOnly)for(const p of order)bake(p);
 const output={schema:'hidden-harbours/nmc-water-side@1',defType:'SetPieceDef',facings:8,pixelsPerMetre:32,importSizeCap:4096,
  sources:SCRIPTS.map(f=>({file:f,sha256Lf:sha(Buffer.from(fs.readFileSync(path.join(KIT,f),'utf8').replace(/\r\n/g,'\n')))})),
  register:register(),pieces,parts,crib:{decision:'west to x 78',renderedLast:'fleet_crib'}};
 fs.writeFileSync(path.join(OUT,'nmcWaterSide.today.json'),JSON.stringify(output,null,1)+'\n');
 console.log(planOnly?'PLAN':'DONE',pieces.length+' harbours',parts.length+' module/pose variants');
 return output;
}
if(require.main===module)write(process.argv.includes('--plan-only'),process.argv.includes('--metadata-only'));
module.exports={write,parts,pieces,OUT,KIT,ROOT,SCRIPTS,C,png,sha};

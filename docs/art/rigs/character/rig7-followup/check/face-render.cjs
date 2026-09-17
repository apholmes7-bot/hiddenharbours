function raster(faces,mats,{w=420,h=480,angle=145,elev=28,scale=240,cx=w/2,cy=h-40,mode='original',outline=false,surface=null,surfaceColours=[]}={}){
 const featureBuffer=new Uint8Array(w*h);const pixels=new Uint8ClampedArray(w*h*4),depth=new Float32Array(w*h).fill(-1e9);
 const a=angle*Math.PI/180,e=elev*Math.PI/180,ca=Math.cos(a),sa=Math.sin(a),ce=Math.cos(e),se=Math.sin(e);
 const rot=([x,y,z])=>[x*ca-y*sa,x*sa+y*ca,z];
 const dot=(a,b)=>a.reduce((s,x,i)=>s+x*b[i],0),sub=(a,b)=>a.map((x,i)=>x-b[i]);
 for(const f of faces){
  const v=f.v.map(rot),u=sub(v[1],v[0]),t=sub(v[2],v[0]);let n=[u[1]*t[2]-u[2]*t[1],u[2]*t[0]-u[0]*t[2],u[0]*t[1]-u[1]*t[0]],len=Math.hypot(...n)||1;n=n.map(x=>x/len);
  const mat=mats[f.mat]||mats.shirt,light=dot(n,[-.36,-.48,.8]);
  let c;
  if(mode==='original') {const ix=mat.idx??Math.round(light*3*(mat.gain??1)+(mat.bias??2.7)+(f.b||0)+(mat.off||0));c=mat.ramp[Math.max(0,Math.min(mat.ramp.length-1,ix))];}
  else {const ramp=mat.ramp;const ix=mat.idx??Math.max(0,Math.min(ramp.length-1,Math.round((light*(mat.paintGain??.75)+(mat.paintBias??1.85)))));c=ramp[ix];}
  let rgb=Array.isArray(c)?c:[parseInt(c.slice(1,3),16),parseInt(c.slice(3,5),16),parseInt(c.slice(5,7),16)];
  const p=v.map(([x,y,z])=>[cx+x*scale,cy+(y*se-z*ce)*scale,y*ce+z*se]);
  for(let k=1;k<p.length-1;k++){
   const A=p[0],B=p[k],C=p[k+1],den=(B[1]-C[1])*(A[0]-C[0])+(C[0]-B[0])*(A[1]-C[1]);if(Math.abs(den)<1e-10)continue;
   for(let y=Math.max(0,Math.floor(Math.min(A[1],B[1],C[1])));y<=Math.min(h-1,Math.ceil(Math.max(A[1],B[1],C[1])));y++)for(let x=Math.max(0,Math.floor(Math.min(A[0],B[0],C[0])));x<=Math.min(w-1,Math.ceil(Math.max(A[0],B[0],C[0])));x++){
    const wa=((B[1]-C[1])*(x+.5-C[0])+(C[0]-B[0])*(y+.5-C[1]))/den,wb=((C[1]-A[1])*(x+.5-C[0])+(A[0]-C[0])*(y+.5-C[1]))/den,wc=1-wa-wb;
    if(wa<0||wb<0||wc<0)continue;const d=wa*A[2]+wb*B[2]+wc*C[2],i=y*w+x;if(d<=depth[i])continue;depth[i]=d;let ink=0,colour=rgb;if(surface&&f.uv){const U=f.uv[0],V=f.uv[k],W=f.uv[k+1];ink=surface(wa*U[0]+wb*V[0]+wc*W[0],wa*U[1]+wb*V[1]+wc*W[1]);if(ink){const hex=surfaceColours[ink];colour=[parseInt(hex.slice(1,3),16),parseInt(hex.slice(3,5),16),parseInt(hex.slice(5,7),16)];}}featureBuffer[i]=ink;pixels[i*4]=colour[0];pixels[i*4+1]=colour[1];pixels[i*4+2]=colour[2];pixels[i*4+3]=255;
   }
  }
 }
 if(outline){const edge=[];for(let y=1;y<h-1;y++)for(let x=1;x<w-1;x++){const i=y*w+x;if(pixels[i*4+3]&&(!pixels[(i+1)*4+3]||!pixels[(i+w)*4+3]))edge.push(i);}for(const i of edge){pixels[i*4]=Math.round(pixels[i*4]*.53);pixels[i*4+1]=Math.round(pixels[i*4+1]*.59);pixels[i*4+2]=Math.round(pixels[i*4+2]*.69);}}
 return {pixels,w,h,featureBuffer};
}
// PNG encoding is deliberately standard-library only, as is the offline viewer build.
function pngBuffer({pixels,w,h}){
 const zlib=require('zlib');
 function crc32(b){let c=0xffffffff;for(const v of b){c^=v;for(let i=0;i<8;i++)c=(c>>>1)^((c&1)?0xedb88320:0);}return(c^0xffffffff)>>>0;}
 function chunk(type,data){const tag=Buffer.from(type),b=Buffer.alloc(data.length+12);b.writeUInt32BE(data.length);tag.copy(b,4);data.copy(b,8);b.writeUInt32BE(crc32(Buffer.concat([tag,data])),data.length+8);return b;}
 const header=Buffer.alloc(13);header.writeUInt32BE(w);header.writeUInt32BE(h,4);header[8]=8;header[9]=6;
 const rows=Buffer.alloc((w*4+1)*h);for(let y=0;y<h;y++)Buffer.from(pixels.buffer,pixels.byteOffset+y*w*4,w*4).copy(rows,y*(w*4+1)+1);
 return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',header),chunk('IDAT',zlib.deflateSync(rows)),chunk('IEND',Buffer.alloc(0))]);
}
function png(r,file){require('fs').writeFileSync(file,pngBuffer(r));}
module.exports={raster,png,pngBuffer};

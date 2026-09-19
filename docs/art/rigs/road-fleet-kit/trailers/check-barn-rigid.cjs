// check-barn-rigid.cjs - is each reefer barn door ONE rigid leaf on the hinge it declares?
// Usage: node check-barn-rigid.cjs [path/to/trailerIsoRig.js]   (exit 0 = all four doors rigid)
// Same measurement as our Unity baker: build the mesh closed and open, take every face that moved, fit ONE rotation
// about the declared z-hinge by least squares over every vertex, report the worst vertex's distance (metres) from
// where that rotation puts it. The bar is 1e-6 m. It is never loosened: 1 px = 31 mm, so anything seen is a change of kind.
const fs=require('fs'),vm=require('vm'),path=require('path');
const file=process.argv[2]||path.join(__dirname,'docs/art/rigs/road-fleet-kit/trailers/trailerIsoRig.js');
const ctx={console};ctx.globalThis=ctx;ctx.window=ctx;vm.createContext(ctx);vm.runInContext(fs.readFileSync(file,'utf8'),ctx);
const T=ctx.TrailerIso;let bad=0;
for(const [body,L] of [['reefer28',8.53],['reefer53',16.15]])for(const [slot,sx] of [['barnL',-1],['barnR',1]]){
  const A=T.mesh({body}),B=T.mesh({body,[slot]:1});
  if(A.length!==B.length){console.log('FAIL',body,slot,'face count differs',A.length,B.length);bad++;continue;}
  const idx=[];for(let i=0;i<A.length;i++)if(A[i].v.length!==B[i].v.length||A[i].v.some((p,k)=>p.some((x,j)=>Math.abs(x-B[i].v[k][j])>1e-9)))idx.push(i);
  const a=sx*1.19,b=-L/2+0.02;let s=0,c=0;
  for(const i of idx)A[i].v.forEach((p,k)=>{const q=B[i].v[k],pu=p[0]-a,pv=p[1]-b,qu=q[0]-a,qv=q[1]-b;s+=pu*qv-pv*qu;c+=pu*qu+pv*qv;});
  const ang=Math.atan2(s,c),C=Math.cos(ang),Sn=Math.sin(ang);let worst=0;const rows=[];
  for(const i of idx){let w=0,dz=0;A[i].v.forEach((p,k)=>{const q=B[i].v[k],du=p[0]-a,dv=p[1]-b;
      w=Math.max(w,Math.hypot(a+du*C-dv*Sn-q[0],b+du*Sn+dv*C-q[1],p[2]-q[2]));dz=Math.max(dz,Math.abs(p[2]-q[2]));});
    worst=Math.max(worst,w);if(w>1e-6)rows.push('    face '+i+' mat '+A[i].mat+' worst '+w.toFixed(6)+' m, of which z '+dz.toFixed(6)+' m');}
  const ok=worst<=1e-6;if(!ok)bad++;
  console.log(ok?'PASS':'FAIL',body,slot,'faces moved',idx.length,'angle',(ang*180/Math.PI).toFixed(4),'deg (mod 360 of 255)','worst',worst.toExponential(3),'m');
  rows.forEach(r=>console.log(r));}
process.exit(bad?1:0);

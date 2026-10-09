/* Modern 2500 — Hidden Harbours modern pickup art rig (globalThis.ModernTruck2500).
 * Claude Design's modern-trucks kit, 2026-09-26.
 * New geometry, authored for this truck. The rasteriser, primitives, camera and fleet palettes are
 * the Modern 3500's (the supplied fleet recipe), so the modern trucks read as one family:
 * 32 px = 1 m, elevation 40, eight facings, N shows the tail.
 * Stylised art dimensions, not manufacturer figures. No brand lettering, logos or model badges.
 * Self-contained. No runtime dependencies.
 */
(function (root) {
  const PX = 32, S = 32;
  const W = 384, H = 320, cx = 192, groundY = 214;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;                    // ADR 0031

  // ---- paints -------------------------------------------------------------------------------
  const BODY = {
    white:       ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    cream:       ['#8a6f3c','#a6884b','#c2a35f','#d8bd7c','#e9d59d','#f5e7c1'],
    red:         ['#4a130f','#671b14','#88271c','#a33124','#bd4230','#d25a42'],
    sage:        ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'],
    blue:        ['#33454a','#43585d','#556d72','#6a848a','#849ea3','#a3b9bd'],
    teal:        ['#123a3a','#1b4d4b','#26635e','#357b73','#4d968b','#6cb1a4'],
    gold:        ['#5e4a12','#7c6119','#987a26','#b39440','#c8ab5e','#dbc182'],
    rustOrange:  ['#4a2410','#6a3514','#8c481a','#a85f27','#c07a3a','#d49657'],
    greyShingle: ['#4c463f','#5d564c','#6f665a','#82786a','#968b7b','#a99d8c'],
    plum:        ['#2e2333','#3f3047','#523f5d','#664f73','#7d648b','#9079a1'],
    graphite:    ['#151c24','#222c36','#33414e','#485967','#647684','#8b9ba6'],
    silver:      ['#4c5a66','#687783','#87969f','#a8b6bb','#c8d3d5','#e3e9e7'],
    midnight:    ['#0c1420','#132235','#20344b','#304c64','#4d6d83','#7895a5'],
    pearl:       ['#90938f','#acaea5','#c8c9be','#dce0d1','#eff1e2','#fcfbed'],
    deepBlue:    ['#0d1829','#14253f','#1d3758','#284a70','#36608b','#4d78a3'],
    crimson:     ['#2b0b0f','#421016','#5c161e','#76202a','#8e2b31','#a83d3d'],
    sterling:    ['#3b4045','#50565b','#676e73','#7f868a','#999fa2','#b5b9bb'],
    black:       ['#0a0b0d','#101215','#181b1f','#22262b','#30363d','#4a525b'],
  };
  const TRIM   = ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'];
  const IRON   = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970'];
  const GALV   = ['#565b5f','#6d7276','#868b8f','#a0a5a8','#bbbfc1','#d6d9da'];
  const RUBBER = ['#121417','#191c20','#22262b','#2c3137','#383e45','#464d55'];
  const CHROME = ['#4a5157','#5f696f','#7b858c','#98a2a8','#b6bec2','#d6dbdd'];
  const CLOTH  = ['#23262b','#2e3238','#3a3f46','#484e56','#575e67','#686f79'];
  const SHADE  = ['#0b0e11','#0f1418','#141a1f','#1a2128','#212a31','#28323a'];
  const GLASSD = ['#1b262b','#243238','#2f4149','#3d545c','#5d7b82','#96b6ba'];
  const GLASSN = ['#141d2b','#1d2a3d','#2a3c53','#3d5570','#6b7f9c','#95a8c0'];
  const GLOW   = ['#7a5a18','#c09a2c','#efd06a','#fdf0b6'];
  const LENSR  = ['#3a0c0a','#5a120e','#7d1c14','#a52a1d','#c93c2a','#e4573f'];
  const LENSA  = ['#4a2c07','#6d420b','#8f5a12','#b0771f','#cc9633','#e5b455'];
  const LINER  = ['#14191e','#1c232a','#263039','#323e47','#415059','#53636a'];
  const CLAD   = ['#17191c','#202327','#2a2e33','#363b41','#444a51','#555c64'];
  const HOOK   = ['#3d0d0c','#5b1310','#7c1b15','#9f261c','#bd3627','#d4503a'];
  const KEY    = '#1a1c22';

  // ---- shading (identical recipe to camperIsoRig / the fleet) ----
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }

  function camBasis(opts){ const dir=opts.dir||0, th=dir*Math.PI/4, e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { th, ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) }; }
  function projVert(x,y,z,B){ const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:groundY-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) }; }
  function normal(a,b,c){ const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx; const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m]; }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }

  // ---- face builders (outward-normal winding, camper conventions) ----
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }
  function quad(out,a,b,c,d,mat,bi,db,uv,tex,flat){ out.push(F([a,b,c,d],mat,bi,db,uv,tex,flat)); }
  function slab(out, pts, z, mat, b, tex){ const uv=tex?pts.map(p=>[p[0],p[1]]):null;
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0, uv, tex)); }
  function wallX(out,x,y0,y1,z0,z1,mat,b,sgn,uv,tex){
    if(sgn>0) quad(out,[x,y0,z0],[x,y1,z0],[x,y1,z1],[x,y0,z1],mat,b,0,uv,tex);
    else      quad(out,[x,y1,z0],[x,y0,z0],[x,y0,z1],[x,y1,z1],mat,b,0,uv,tex);
  }
  function wallY(out,y,x0,x1,z0,z1,mat,b,sgn,uv,tex){
    if(sgn>0) quad(out,[x1,y,z0],[x0,y,z0],[x0,y,z1],[x1,y,z1],mat,b,0,uv,tex);
    else      quad(out,[x0,y,z0],[x1,y,z0],[x1,y,z1],[x0,y,z1],mat,b,0,uv,tex);
  }
  function boxAt(out, x0,x1,y0,y1,z0,z1, mat, b, noTop, tex){
    b=b||0;
    wallY(out,y0,x0,x1,z0,z1,mat,b-0.30,-1,null,tex);
    wallY(out,y1,x0,x1,z0,z1,mat,b+0.10,+1,null,tex);
    wallX(out,x1,y0,y1,z0,z1,mat,b+0.18,+1,null,tex);
    wallX(out,x0,y0,y1,z0,z1,mat,b-0.42,-1,null,tex);
    if(!noTop) slab(out,[[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, b+0.34, tex);
  }
  const sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]];
  const crs=(a,b)=>[a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]];
  const nrm=(v)=>{ const m=Math.hypot(v[0],v[1],v[2])||1; return [v[0]/m,v[1]/m,v[2]/m]; };
  function tube(out, p0, p1, r, n, mat, b, caps, tex){
    const u=nrm(sub(p1,p0)); const a=Math.abs(u[2])>0.9?[1,0,0]:[0,0,1];
    const e1=nrm(crs(a,u)), e2=nrm(crs(u,e1)); n=n||12; b=b||0;
    const L=Math.hypot(p1[0]-p0[0],p1[1]-p0[1],p1[2]-p0[2]), arc=(2*Math.PI*r)/n;
    const P=(i,end)=>{ const th=(i/n)*Math.PI*2, c=Math.cos(th)*r, s=Math.sin(th)*r, q=end?p1:p0;
      return [q[0]+e1[0]*c+e2[0]*s, q[1]+e1[1]*c+e2[1]*s, q[2]+e1[2]*c+e2[2]*s]; };
    for(let i=0;i<n;i++){ const j=(i+1)%n;
      out.push(F([P(i,0),P(j,0),P(j,1),P(i,1)], mat, b, 0,
        tex?[[i*arc,0],[(i+1)*arc,0],[(i+1)*arc,L],[i*arc,L]]:null, tex)); }
    if(caps!==false){ const top=[],bot=[];
      for(let i=0;i<n;i++){ top.push(P(i,1)); bot.push(P(n-1-i,0)); }
      out.push(F(top, mat, b+0.26)); out.push(F(bot, mat, b-0.5)); }
  }
  const bar = (out,p0,p1,r,mat,b)=> tube(out,p0,p1,r,4,mat,b);

  // ---- textures ----
  function wearTex(w){ return (u,v)=>{ if(w>0.03 && hash2(Math.floor(u*6.5)|0, Math.floor(v*6.5)|0) < w*0.10) return -1; return 0; }; }
  function ribTex(){ const p=0.15; return (u,v)=>{ const f=((u%p)+p)%p; return f<0.055?-1:0; }; }
  // tread closes exactly after one revolution: `stripes` blocks round the circumference;
  // `stagger` offsets the outer half by half a block (all-terrain shoulder lugs)
  function treadTex(phase,r,stripes,w,stagger){ const c=2*Math.PI*r/stripes;
    return (u,v)=>{ const o=stagger&&v>w*0.5?c*0.5:0; const f=(((u+phase+o)%c)+c)%c; return f<c*0.42?-1:0; }; }

  // ---- modern-truck primitives ----
  const hingeZ=(p,hx,hy,a)=>{const x=p[0]-hx,y=p[1]-hy;return [hx+x*Math.cos(a)-y*Math.sin(a),hy+x*Math.sin(a)+y*Math.cos(a),p[2]];};
  const hingeX=(p,hy,hz,a)=>{const y=p[1]-hy,z=p[2]-hz;return [p[0],hy+y*Math.cos(a)-z*Math.sin(a),hz+y*Math.sin(a)+z*Math.cos(a)];};
  function part(out,fn,xf,group){const a=[];fn(a);for(const f of a){if(xf)f.v=f.v.map(xf);f.group=group;out.push(f);}}
  function newell(p){let x=0,y=0,z=0;for(let i=0;i<p.length;i++){const a=p[i],b=p[(i+1)%p.length];
    x+=(a[1]-b[1])*(a[2]+b[2]);y+=(a[2]-b[2])*(a[0]+b[0]);z+=(a[0]-b[0])*(a[1]+b[1]);}return[x,y,z];}
  // polygon turned so its normal faces `dir` (a world vector) — no hand winding
  function poly(out,pts,mat,b,dir,uv,tex){const n=newell(pts);let q=pts,u=uv||null;
    if(n[0]*dir[0]+n[1]*dir[1]+n[2]*dir[2]<0){q=pts.slice().reverse();u=u?u.slice().reverse():null;}
    out.push(F(q,mat,b||0,0,u,tex||null));}
  function rectX(out,sx,x,y0,y1,z0,z1,mat,b=0){wallX(out,sx*x,y0,y1,z0,z1,mat,b,sx);}
  function pairedBox(out,sx,x0,x1,y0,y1,z0,z1,mat,b=0,noTop){boxAt(out,Math.min(sx*x0,sx*x1),Math.max(sx*x0,sx*x1),y0,y1,z0,z1,mat,b,noTop);}
  function faceY(out,y,pts,mat,b=0,dir=1){poly(out,pts.map(p=>[p[0],y,p[1]]),mat,b,[0,dir,0]);}
  function ringY(out,y,outer,inner,mat,b=0,dir=1){for(let i=0;i<outer.length;i++){const j=(i+1)%outer.length;
    poly(out,[[outer[i][0],y,outer[i][1]],[outer[j][0],y,outer[j][1]],[inner[j][0],y,inner[j][1]],[inner[i][0],y,inner[i][1]]],mat,b,[0,dir,0]);}}
  // rounded rectangle in (x,z), counter-clockwise from the bottom-left corner
  function rrect(xa,xb,za,zb,rx,rz,n=4){const c=[[xa+rx,za+rz,Math.PI],[xb-rx,za+rz,Math.PI*1.5],[xb-rx,zb-rz,0],[xa+rx,zb-rz,Math.PI*.5]],p=[];
    for(const[x,z,a]of c)for(let i=0;i<=n;i++){const t=a+i*Math.PI/(2*n);p.push([x+Math.cos(t)*rx,z+Math.sin(t)*rz]);}return p;}
  function ellipse(xc,zc,rx,rz,n=12){return Array.from({length:n},(_,i)=>{const t=i*2*Math.PI/n;return[xc+Math.cos(t)*rx,zc+Math.sin(t)*rz];});}
  // wheel opening: superellipse top edge (p=2 is a circle, larger p squarer)
  function archZ(y,A){const t=Math.abs(y-A.yc)/A.r;if(t>=1)return -9;return A.zc+A.r*Math.pow(1-Math.pow(t,A.p),1/A.p);}
  function archPt(A,th,rr){const c=Math.cos(th),s=Math.sin(th);return[A.yc+rr*Math.sign(c)*Math.pow(Math.abs(c),2/A.p),A.zc+rr*Math.pow(Math.abs(s),2/A.p)];}
  function archSpan(A,floor){const k=Math.max(0,Math.min(1,(floor-A.zc)/A.r));return Math.asin(Math.pow(k,A.p/2));}
  // vertical side skin, n strips between y0..y1 at x=xFn(y), from low(y) up to top(y)
  function skinX(out,sx,y0,y1,n,xFn,low,top,mat,b,tex){
    for(let i=0;i<n;i++){const a=y0+(y1-y0)*i/n,c=y0+(y1-y0)*(i+1)/n,ta=top(a),tc=top(c);
      const za=Math.min(low(a),ta),zc=Math.min(low(c),tc);
      if(ta-za<1e-4&&tc-zc<1e-4)continue;
      const q=[[sx*xFn(a),a,za],[sx*xFn(c),c,zc],[sx*xFn(c),c,tc],[sx*xFn(a),a,ta]];
      poly(out,q,mat,b,[sx,0,0],tex?q.map(p=>[p[1],p[2]]):null,tex);}
  }
  // flare / lip round a wheel opening: band w wide standing `off` proud of the skin, plus its
  // outer return so the thickness reads from the 40 degree camera
  function archLip(out,sx,A,w,off,xFn,floor,mat,b,n=18){
    const t0=archSpan(A,floor),X=(y,o)=>sx*(xFn(y)+o);
    for(let i=0;i<n;i++){const a=t0+(Math.PI-2*t0)*i/n,c=t0+(Math.PI-2*t0)*(i+1)/n;
      const[ya,za]=archPt(A,a,A.r),[yc,zc]=archPt(A,c,A.r),[yA,zA]=archPt(A,a,A.r+w),[yC,zC]=archPt(A,c,A.r+w);
      poly(out,[[X(ya,off),ya,za],[X(yc,off),yc,zc],[X(yC,off),yC,zC],[X(yA,off),yA,zA]],mat,b,[sx,0,0]);
      poly(out,[[X(yA,0),yA,zA],[X(yC,0),yC,zC],[X(yC,off),yC,zC],[X(yA,off),yA,zA]],mat,(b||0)+.3,[0,(yA+yC)/2-A.yc,(zA+zC)/2-A.zc]);
      poly(out,[[X(ya,-.02),ya,za],[X(yc,-.02),yc,zc],[X(yc,off),yc,zc],[X(ya,off),ya,za]],mat,(b||0)-.6,[0,A.yc-(ya+yc)/2,A.zc-(za+zc)/2]);
    }
  }
  // thick polyline in a y-plane through (x,z) points; joints simply overlap
  function strokeY(out,y,pts,w,mat,b=0,dir=1){for(let i=0;i<pts.length-1;i++){const[a0,a1]=pts[i],[b0,b1]=pts[i+1],dx=b0-a0,dz=b1-a1,L=Math.hypot(dx,dz)||1,nx=-dz/L*w/2,nz=dx/L*w/2;
    poly(out,[[a0+nx,y,a1+nz],[b0+nx,y,b1+nz],[b0-nx,y,b1-nz],[a0-nx,y,a1-nz]],mat,b,[0,dir,0]);}}
  function arcPts(xc,zc,r,a0,a1,n){return Array.from({length:n+1},(_,i)=>{const a=a0+(a1-a0)*i/n;return[xc+Math.cos(a)*r,zc+Math.sin(a)*r];});}
  // convex (x,z) outline extruded along y from y0 to y1: front face, optional back, and sides
  function prismY(out,pts,y0,y1,mat,b=0,back){faceY(out,y1,pts,mat,b,1);if(back)faceY(out,y0,pts,mat,b-.3,-1);
    const cx=pts.reduce((a,p)=>a+p[0],0)/pts.length,cz=pts.reduce((a,p)=>a+p[1],0)/pts.length;
    for(let i=0;i<pts.length;i++){const a=pts[i],c=pts[(i+1)%pts.length],mz=(a[1]+c[1])/2;
      poly(out,[[a[0],y0,a[1]],[c[0],y0,c[1]],[c[0],y1,c[1]],[a[0],y1,a[1]]],mat,b+(mz>cz?.3:-.2),[(a[0]+c[0])/2-cx,0,mz-cz]);}}
  // lit crease with its shadow along a side skin: zFn(y) is the crease height
  function creaseX(out,sx,y0,y1,n,xFn,zFn,h=.024){for(let j=0;j<n;j++){const a=y0+(y1-y0)*j/n,b=y0+(y1-y0)*(j+1)/n,X=y=>sx*(xFn(y)+.003);
    poly(out,[[X(a),a,zFn(a)],[X(b),b,zFn(b)],[X(b),b,zFn(b)+h],[X(a),a,zFn(a)+h]],'paint',.5,[sx,0,.4]);
    poly(out,[[X(a),a,zFn(a)-h],[X(b),b,zFn(b)-h],[X(b),b,zFn(b)],[X(a),a,zFn(a)]],'paint',-.45,[sx,0,-.2]);}}
  // drop repeated vertices and zero-area faces so the baker never gets a degenerate triangle
  function clean(faces){const out=[];for(const f of faces){const v=[];
    for(const p of f.v){const q=v[v.length-1];if(!q||Math.abs(q[0]-p[0])+Math.abs(q[1]-p[1])+Math.abs(q[2]-p[2])>1e-7)v.push(p);}
    while(v.length>2){const a=v[0],b=v[v.length-1];if(Math.abs(a[0]-b[0])+Math.abs(a[1]-b[1])+Math.abs(a[2]-b[2])>1e-7)break;v.pop();}
    if(v.length<3)continue;let ok=true;
    for(let i=1;i+1<v.length;i++){const c=crs(sub(v[i],v[0]),sub(v[i+1],v[0]));if(Math.hypot(c[0],c[1],c[2])<2e-10){ok=false;break;}}
    if(!ok){const k=[v[0]];for(let i=1;i<v.length;i++){const c=crs(sub(v[i],k[k.length-1]),sub(v[(i+1)%v.length],k[k.length-1]));if(Math.hypot(c[0],c[1],c[2])>=2e-10)k.push(v[i]);}
      if(k.length<3)continue;let ok2=true;for(let i=1;i+1<k.length;i++){const c=crs(sub(k[i],k[0]),sub(k[i+1],k[0]));if(Math.hypot(c[0],c[1],c[2])<2e-10){ok2=false;break;}}
      if(!ok2)continue;v.length=0;v.push(...k);}
    f.v=v;if(f.uv&&f.uv.length!==v.length){f.uv=null;f.tex=null;}out.push(f);}return out;}
  // dark wheel-well tunnel behind an opening, from the skin inward to xIn
  function wellLiner(out,sx,A,xFn,xIn,floor,n=16){
    const t0=archSpan(A,floor);
    for(let i=0;i<n;i++){const a=t0+(Math.PI-2*t0)*i/n,c=t0+(Math.PI-2*t0)*(i+1)/n;
      const[ya,za]=archPt(A,a,A.r-.004),[yc,zc]=archPt(A,c,A.r-.004);
      poly(out,[[sx*xFn(ya),ya,za],[sx*xFn(yc),yc,zc],[sx*xIn,yc,zc],[sx*xIn,ya,za]],'shade',-.85,[0,A.yc-(ya+yc)/2,A.zc-(za+zc)/2]);}
    wallX(out,sx*xIn,A.yc-A.r,A.yc+A.r,floor,A.zc+A.r,'shade',-.9,sx);
  }

  // ================= GEOMETRY — MODERN 2500 =================
  // A modern three-quarter-tonne crew cab, standard box, off-road trim: a very tall square nose,
  // a huge dark grille, slim lamps with vertical C-blade running lights, a domed hood with a front
  // scoop, black cladding flares and bumpers, red recovery hooks, rock rails, roof marker lamps and
  // chunky all-terrain tyres on black wheels. Metres; +x passenger/right, +y nose, +z up.
  const RIG={key:'modern2500',label:'Modern 2500'};
  const G={bumpF:[3.02,3.19],noseY:3.08,tailY:-3.04,bumpR:[-3.19,-3.02],
    cowlY:1.44,cabRear:-0.82,bedFront:-0.88,axF:2.25,axR:-1.79,
    wheelR:0.425,tireW:0.30,frontWX:0.875,rearWX:0.875,
    hwSide:1.03,beltZ:1.50,glassTop:1.99,roofZ:2.07,
    hoodZc:1.665,hoodZn:1.565,bedFloorZ:1.02,railZ:1.52,
    doorF:[0.20,1.44],doorR:[-0.78,0.16],doorZ0:0.68,
    archF:{yc:2.25,zc:0.425,r:0.57,p:3.2},archR:{yc:-1.79,zc:0.425,r:0.58,p:3.2}};
  const TF=.12,TR=.14,STEER=30;
  const BODIES={modern2500:{key:'modern2500',label:'Modern 2500',kind:'crew-cab standard box, off-road trim',
    loa:6.38,width:2.17,bodyW:2.06,height:2.11,wheelbase:4.04,wheels:4}};
  const PRESETS={signature:{paint:'crimson',weather:.08,trim:'black',steps:true},
    stealth:{paint:'black',weather:.06,trim:'black',steps:true},
    sterling:{paint:'sterling',weather:.14,trim:'black',steps:true},
    trail:{paint:'sage',weather:.46,trim:'black',steps:true},
    service:{paint:'white',weather:.18,trim:'chrome',hood:1,gate:1},
    nightShift:{paint:'graphite',weather:.10,night:true,steps:true}};
  const CUES={doors:t=>({dFL:t,dFR:t,dRL:t,dRR:t}),hood:t=>({hood:t}),gate:t=>({gate:t}),
    roll:t=>({roll:t}),steer:t=>({steer:Math.sin(t*2*Math.PI)}),
    bounce:t=>({roll:t,susF:Math.sin(t*2*Math.PI)*.65,susR:Math.sin(t*2*Math.PI+1.15)*.65})};
  function resolve(o={}){
    const val=(k,d)=>{if(o[k]==null)return d;if(typeof o[k]!=='number'||!Number.isFinite(o[k]))throw new TypeError(k+' must be finite');return o[k];};
    const c=(k,d=0)=>Math.max(0,Math.min(1,val(k,d))),s=(k)=>Math.max(-1,Math.min(1,val(k,0)));
    if(o.paint!=null&&!Object.hasOwn(BODY,o.paint))throw new Error('unknown paint '+o.paint);
    if(o.trim!=null&&!['chrome','black'].includes(o.trim))throw new Error('unknown trim '+o.trim);
    return {body:RIG.key,paint:o.paint||'crimson',trim:o.trim||'black',weather:c('weather',.08),
      dFL:c('dFL'),dFR:c('dFR'),dRL:c('dRL'),dRR:c('dRR'),hood:c('hood'),gate:c('gate'),
      roll:val('roll',0),wFL:val('wFL',0),wFR:val('wFR',0),wRL:val('wRL',0),wRR:val('wRR',0),
      susF:s('susF'),susR:s('susR'),steer:s('steer'),night:!!o.night,brake:!!o.brake,
      steps:o.steps!==false,mirrors:o.mirrors!==false,hitch:o.hitch!==false,outline:!!o.outline};
  }
  function dims(){return {...BODIES.modern2500,travelF:TF,travelR:TR};}
  function bodyOffset(y,s){const t=(y-G.axR)/(G.axF-G.axR);return -s.susF*TF*t-s.susR*TR*(1-t);}

  const sweep=([x,y,z])=>{if(y<=2.86)return[x,y,z];const k=Math.min(1,(y-2.86)/.22);return[x,y-.06*k*Math.min(1,Math.abs(x)/1.03)**4,z];};
  const fx=y=>1.03-.04*Math.max(0,(y-2.92)/.16)**2;
  const fTop=y=>G.hoodZn+(G.hoodZc-G.hoodZn)*Math.max(0,Math.min(1,(G.noseY-y)/(G.noseY-G.cowlY)))-.012;
  const hwN=fx(G.noseY);
  const gx=z=>.995-(z-G.beltZ)*.30;
  const bx=()=>G.hwSide;
  const skinUp=z=>1.034+(.995-1.034)*(z-1.15)/(G.beltZ-1.15);

  function chassis(out,s){
    for(const sx of [-1,1]){
      pairedBox(out,sx,.44,.53,-3.00,2.94,.52,.67,'iron',-.4);
      pairedBox(out,sx,.60,.76,G.axR-.60,G.axR+.56,.52,.57,'iron',-.2);
      tube(out,[sx*.66,G.axR+.06,.55],[sx*.84,G.axR+.24,.92],.04,8,'galv',0);
      tube(out,[sx*.60,G.axF-.10,.56],[sx*.76,G.axF+.08,.98],.04,8,'galv',0);
    }
    for(const y of [-2.7,-1.2,.2,1.4,2.6])boxAt(out,-.48,.48,y-.05,y+.05,.54,.63,'iron',-.4);
    boxAt(out,-.92,-.47,-1.30,-.10,.42,.66,'iron',0);
    bar(out,[.06,G.axR+.22,.50],[.06,G.axF-.34,.50],.045,'iron',-.3);
    bar(out,[.50,1.50,.46],[.64,-2.60,.42],.05,'iron',-.3);
    tube(out,[.64,-2.64,.42],[.64,-3.24,.40],.068,12,'galv',.25);
    tube(out,[.64,-3.235,.40],[.64,-3.25,.40],.048,12,'shade',-.8);
    // skid plate and front bumper: black cladding with its outer corners cut up for approach,
    // a rolled top, a framed honeycomb intake, fog pods, red recovery hooks on mounting plates
    const fb=out.length;
    poly(out,[[-.62,2.62,.44],[.62,2.62,.44],[.62,3.02,.44],[-.62,3.02,.44]],'iron',-.3,[0,0,-1]);
    poly(out,[[-.62,3.02,.44],[.62,3.02,.44],[.62,3.13,.55],[-.62,3.13,.55]],'iron',.1,[0,1,.4]);
    for(let x=-.50;x<.51;x+=.25)poly(out,[[x-.03,3.021,.445],[x+.03,3.021,.445],[x+.03,3.121,.545],[x-.03,3.121,.545]],'galv',-.1,[0,1,.4]);
    prismY(out,[[-1.02,.70],[-.86,.545],[.86,.545],[1.02,.70],[1.02,.90],[-1.02,.90]],3.03,3.19,'clad',.12);
    const R=[[3.03,.90],[3.19,.90],[3.145,.955],[3.03,.955]];
    for(let i=0;i<3;i++){const a=R[i],c=R[i+1];
      poly(out,[[-1.0,a[0],a[1]],[1.0,a[0],a[1]],[1.0,c[0],c[1]],[-1.0,c[0],c[1]]],'clad',[.1,.45,.3][i],[0,(a[0]+c[0])/2-3.09,(a[1]+c[1])/2-.92]);}
    for(const sx of [-1,1])poly(out,R.map(([y,z])=>[sx*1.0,y,z]),'clad',sx>0?.18:-.3,[sx,0,0]);
    wallY(out,3.192,-.98,.98,.855,.872,'clad',.55,1);
    faceY(out,3.192,rrect(-.47,.47,.61,.83,.03,.03),'shade',-.6);
    for(let r=0;r<4;r++){const z=.625+r*.05;for(let x=-.44+(r%2)*.035;x<.44;x+=.07)wallY(out,3.194,x,x+.042,z,z+.024,'clad',-.1,1);}
    ringY(out,3.196,rrect(-.50,.50,.59,.85,.035,.035),rrect(-.47,.47,.61,.83,.03,.03),'clad',.35);
    for(const sx of [-1,1]){const M=pts=>pts.map(([x,z])=>[sx*x,z]);
      faceY(out,3.192,M(rrect(.66,.92,.73,.845,.035,.035)),'shade',-.5);
      faceY(out,3.196,M(rrect(.695,.885,.76,.815,.02,.02)),s.night?'glow':'head',.35);
      pairedBox(out,sx,.515,.685,3.19,3.20,.575,.745,'iron',-.1);
      pairedBox(out,sx,.54,.58,3.17,3.28,.60,.72,'hook',.3);
      pairedBox(out,sx,.54,.66,3.24,3.28,.68,.72,'hook',.45);
      pairedBox(out,sx,.62,.66,3.17,3.28,.60,.72,'hook',.3);
    }
    for(let i=fb;i<out.length;i++)out[i].v=out[i].v.map(sweep);
    // rear bumper: black cladding with corner steps, plate recess, receiver
    boxAt(out,-1.03,1.03,-3.19,-3.02,.54,.88,'clad',-.05);
    for(const sx of [-1,1]){pairedBox(out,sx,.76,1.0,-3.18,-3.03,.88,.895,'rubber',.1);
      pairedBox(out,sx,.80,.98,-3.20,-3.19,.62,.74,'shade',-.5);}
    boxAt(out,-.40,.40,-3.18,-3.03,.88,.895,'rubber',.1);
    boxAt(out,-.21,.21,-3.20,-3.192,.60,.76,'shade',-.5);
    wallY(out,-3.203,-.16,.16,.62,.74,'galv',.1,-1);
    if(s.hitch){boxAt(out,-.07,.07,-3.34,-3.16,.42,.54,'iron',-.2);tube(out,[0,-3.30,.54],[0,-3.30,.66],.035,10,'galv',.2);}
  }

  function front(out,s){
    const start=out.length,wear=wearTex(s.weather),lens=s.night?'glow':'head',yN=G.noseY;
    const lowF=y=>Math.max(y>2.92?.955:.68,archZ(y,G.archF));
    for(const sx of [-1,1]){
      skinX(out,sx,G.cowlY,yN,40,fx,lowF,fTop,'paint',sx>0?.12:-.25,wear);
      for(let j=0;j<20;j++){const a=G.cowlY+(yN-G.cowlY)*j/20,b=G.cowlY+(yN-G.cowlY)*(j+1)/20;
        poly(out,[[sx*.90,a,fTop(a)+.014],[sx*.90,b,fTop(b)+.014],[sx*fx(b),b,fTop(b)],[sx*fx(a),a,fTop(a)]],'paint',.28,[sx*.15,0,1]);}
      for(let j=0;j<11;j++){const a=G.cowlY+(yN-G.cowlY)*j/11,b=G.cowlY+(yN-G.cowlY)*(j+1)/11;
        poly(out,[[sx*.893,a,fTop(a)+.0155],[sx*.893,b,fTop(b)+.0155],[sx*.917,b,fTop(b)+.0145],[sx*.917,a,fTop(a)+.0145]],'shade',-.2,[0,0,1]);}
      // two body lines: a shoulder crease, and a lower one that kicks up over the arch
      creaseX(out,sx,G.cowlY+.04,2.96,18,fx,y=>fTop(y)-.09);
      creaseX(out,sx,G.cowlY+.02,1.62,6,fx,()=>1.15);
      archLip(out,sx,G.archF,.10,.055,fx,.68,'clad',.05);
      wellLiner(out,sx,G.archF,fx,.66,.68);
      const[ym,zm]=archPt(G.archF,.42,G.archF.r+.05);
      pairedBox(out,sx,fx(ym)+.05,fx(ym)+.062,ym-.05,ym+.05,zm-.03,zm+.03,'amber',.3);
      // fender gill behind the arch: a dark vent with a bright spear
      const xg=fx(1.52);
      poly(out,[[sx*(xg+.004),1.47,1.08],[sx*(xg+.004),1.60,1.13],[sx*(xg+.004),1.60,1.21],[sx*(xg+.004),1.47,1.19]],'clad',-.1,[sx,0,0]);
      poly(out,[[sx*(xg+.008),1.49,1.135],[sx*(xg+.008),1.58,1.16],[sx*(xg+.008),1.58,1.18],[sx*(xg+.008),1.49,1.158]],'bright',.4,[sx,0,0]);
    }
    // grille: a huge dark field in a thick chamfered surround, staggered honeycomb mesh, four
    // deep bars, a plain badge plate in a frame (no lettering)
    const O=[[-.85,1.005],[-.80,.955],[.80,.955],[.85,1.005],[.85,1.52],[-.85,1.52]];
    const I=[[-.765,1.03],[-.745,1.012],[.745,1.012],[.765,1.03],[.765,1.455],[-.765,1.455]];
    faceY(out,yN-.03,O,'shade',-.55);
    for(let r=0;r<7;r++){const z=1.03+r*.061;for(let x=-.735+(r%2)*.035;x<.73;x+=.07)wallY(out,yN-.022,x,x+.042,z,z+.03,'iron',-.05,1);}
    ringY(out,yN+.014,O,I,'bright',.25);
    for(let i=0;i<O.length;i++){const a=O[i],b=O[(i+1)%O.length];
      poly(out,[[a[0],yN-.03,a[1]],[b[0],yN-.03,b[1]],[b[0],yN+.014,b[1]],[a[0],yN+.014,a[1]]],'bright',-.05,[a[0]+b[0],0,a[1]+b[1]-2.47]);}
    for(const z of [1.06,1.17,1.28,1.39]){boxAt(out,-.765,.765,yN-.022,yN+.016,z,z+.045,'clad',.2);
      wallY(out,yN-.021,-.76,.76,z-.02,z,'shade',-.7,1);}
    boxAt(out,-.21,.21,yN,yN+.026,1.285,1.415,'bright',.3);
    boxAt(out,-.195,.195,yN+.026,yN+.03,1.30,1.40,'galv',.2);
    wallY(out,yN+.031,-.172,.172,1.316,1.384,'hook',.25,1);
    // slim lamps with two projector cells; the C-blade: an LED strip over the lamp that rounds
    // into a blade down the outer edge and hooks back inward at its foot
    for(const sx of [-1,1]){
      const M=pts=>pts.map(([x,z])=>[sx*x,z]);
      pairedBox(out,sx,.85,hwN,yN-.05,yN+.006,.955,1.52,'shade',-.55);
      faceY(out,yN+.008,M([[.868,1.402],[hwN-.02,1.392],[hwN-.02,1.482],[.868,1.482]]),lens,.4);
      for(const x of [.90,.948])faceY(out,yN+.012,M(rrect(x-.019,x+.019,1.418,1.466,.008,.008)),s.night?'glow':'glass',.75);
      const xo=hwN-.026,r=.04;
      strokeY(out,yN+.016,M([[.868,1.502],...arcPts(xo-r,1.502-r,r,Math.PI/2,0,5),...arcPts(xo-r,1.00+r,r,0,-Math.PI/2,5),[.88,1.00]]),.03,'led',.5,1);
      faceY(out,yN+.010,M(rrect(.885,hwN-.062,1.15,1.28,.012,.012)),'amber',.2);
      strokeY(out,yN+.008,M([[.872,1.07],[.905,1.36]]),.02,'clad',.1,1);
    }
    // engine bay
    boxAt(out,-.88,.88,G.cowlY+.04,yN-.10,.84,1.04,'shade',-.65);
    for(const sx of [-1,1])pairedBox(out,sx,.72,.90,G.cowlY+.04,yN-.10,1.04,1.54,'iron',-.5);
    boxAt(out,-.44,.44,1.72,2.84,1.04,1.40,'iron',-.2);
    boxAt(out,-.32,.32,1.82,2.72,1.40,1.49,'clad',.0);
    boxAt(out,-.82,-.52,2.44,2.88,1.08,1.38,'rubber',-.1);
    boxAt(out,-.80,-.76,2.62,2.66,1.38,1.41,'hook',.2);
    boxAt(out,-.72,.72,2.90,2.97,1.00,1.48,'iron',-.3);
    tube(out,[.38,1.84,1.36],[.64,2.64,1.46],.085,10,'rubber',-.1);
    wallY(out,G.cowlY+.035,-.90,.90,1.04,G.hoodZc-.01,'shade',-.8,1);
    poly(out,[[-.95,G.cowlY-.10,1.68],[.95,G.cowlY-.10,1.68],[.95,G.cowlY+.01,G.hoodZc+.004],[-.95,G.cowlY+.01,G.hoodZc+.004]],'rubber',-.1,[0,.3,1]);
    for(let x=-.82;x<.83;x+=.08)poly(out,[[x,G.cowlY-.08,1.681],[x+.04,G.cowlY-.08,1.681],[x+.04,G.cowlY-.01,G.hoodZc+.005],[x,G.cowlY-.01,G.hoodZc+.005]],'shade',-.3,[0,.3,1]);
    for(let i=start;i<out.length;i++){out[i].v=out[i].v.map(sweep);out[i].group='frontClip';}
  }

  function hood(out,s){part(out,T=>{
    // outer panels with a raised power line toward each lamp, a tall central dome with crisp
    // walls that ends in a framed scoop mouth, and a blunt lip that curves down at its corners
    const yF=G.noseY+.02,yS=yF-.16,wear=wearTex(s.weather);
    const zAt=y=>G.hoodZc+(G.hoodZn-G.hoodZc)*(y-G.cowlY)/(yF-G.cowlY);
    const dome=y=>.035+.05*Math.max(0,Math.min(1,(y-G.cowlY)/(yS-G.cowlY)));
    const line=y=>.024*Math.max(0,Math.min(1,(y-G.cowlY+.1)/.5));
    const dip=(x,y)=>-.016*Math.max(0,(Math.abs(x)-.74)/.16)**1.5*Math.max(0,(y-2.86)/(yF-2.86));
    const ys=[G.cowlY,1.9,2.35,2.75,yS];
    const P=(x,y,d=0)=>sweep([x,y,zAt(y)+dip(x,y)+d]);
    const sect=(x0,x1,d0,d1,y0,y1,b)=>{const q=[P(x0,y0,d0(y0)),P(x1,y0,d1(y0)),P(x1,y1,d1(y1)),P(x0,y1,d0(y1))];
      poly(T,q,'paint',b,[0,0,1],q.map(p=>[p[0],p[1]]),wear);};
    const z0=()=>0;
    for(let j=0;j<ys.length-1;j++){const a=ys[j],b=ys[j+1];
      for(const sx of [-1,1]){
        sect(sx*.90,sx*.74,z0,z0,a,b,.1);
        sect(sx*.74,sx*.71,z0,line,a,b,sx>0?.6:-.45);
        sect(sx*.71,sx*.56,line,line,a,b,.12);
        sect(sx*.56,sx*.46,line,dome,a,b,sx>0?.3:-.15);
      }
      sect(-.46,.46,dome,dome,a,b,.14);
    }
    for(const sx of [-1,1]){sect(sx*.90,sx*.74,z0,z0,yS,yF,.1);sect(sx*.74,sx*.71,z0,line,yS,yF,sx>0?.45:-.3);sect(sx*.71,sx*.46,line,line,yS,yF,.12);}
    sect(-.46,.46,line,line,yS,yF,.1);
    // scoop mouth: the dome's front face, framed, dark, with slats
    const m=[P(-.46,yS,line(yS)),P(.46,yS,line(yS)),P(.46,yS,dome(yS)),P(-.46,yS,dome(yS))];
    poly(T,m,'shade',-.3,[0,1,0]);
    for(const sx of [-1,1])poly(T,[P(sx*.46,yS,line(yS)),P(sx*.56,yS,line(yS)),P(sx*.46,yS,dome(yS))],'paint',-.1,[0,1,0]);
    poly(T,[P(-.46,yS+.003,dome(yS)-.014),P(.46,yS+.003,dome(yS)-.014),P(.46,yS+.003,dome(yS)),P(-.46,yS+.003,dome(yS))],'clad',.3,[0,1,0]);
    for(let x=-.40;x<.42;x+=.1)poly(T,[P(x,yS+.004,line(yS)+.008),P(x+.05,yS+.004,line(yS)+.008),P(x+.05,yS+.004,dome(yS)-.018),P(x,yS+.004,dome(yS)-.018)],'clad',-.1,[0,1,0]);
    // underside + blunt leading lip
    poly(T,[P(-.9,G.cowlY,-.03),P(.9,G.cowlY,-.03),P(.9,yF,-.03),P(-.9,yF,-.03)],'shade',-.9,[0,0,-1]);
    for(const [x0,x1,d0,d1] of [[-.90,-.74,z0,z0],[-.74,-.71,z0,line],[-.71,.71,line,line],[.71,.74,line,z0],[.74,.90,z0,z0]]){
      const a=P(x0,yF,d0(yF)),b=P(x1,yF,d1(yF));
      poly(T,[a,b,[b[0],b[1]-.006,b[2]-.04],[a[0],a[1]-.006,a[2]-.04]],'paint',-.02,[0,1,-.1]);}
  },p=>hingeX(p,G.cowlY,G.hoodZc,s.hood*50*DEG),'hood');}

  function cab(out,s){
    for(const sx of [-1,1]){
      rectX(out,sx,.99,G.cabRear+.02,G.cowlY,.62,.685,'paint',-.55);
      for(const [y0,y1] of [[G.doorR[1],G.doorF[0]],[G.cabRear,G.doorR[0]]])
        rectX(out,sx,G.hwSide,y0,y1,G.doorZ0,G.beltZ,'paint',sx>0?.1:-.25);
      const gh=(y,z)=>[sx*gx(z),y,z];
      poly(out,[gh(G.doorR[1]-.03,G.beltZ),gh(G.doorF[0]+.03,G.beltZ),gh(G.doorF[0]+.03,G.glassTop),gh(G.doorR[1]-.03,G.glassTop)],'rubber',-.1,[sx,0,.3]);
      poly(out,[gh(G.cabRear,G.beltZ),gh(G.doorR[0]+.02,G.beltZ),gh(G.doorR[0]+.02,G.glassTop),gh(G.cabRear,G.glassTop)],'paint',-.05,[sx,0,.3]);
      poly(out,[gh(G.cowlY,G.beltZ),[sx*.95,G.cowlY-.10,1.68],[sx*.82,.72,2.005],gh(.76,G.glassTop)],'paint',.05,[sx,.5,.6]);
    }
    const R0=G.cabRear+.01,R1=.72,yc=(R0+R1)/2;
    const outl=[[-.80,R0],[.80,R0],[.86,R0+.08],[.86,R1-.10],[.80,R1],[-.80,R1],[-.86,R1-.10],[-.86,R0+.08]];
    const inn=outl.map(([x,y])=>[x*.9,yc+(y-yc)*.95]);
    slab(out,inn,G.roofZ,'paint',.12);
    for(let i=0;i<8;i++){const j=(i+1)%8;poly(out,[[...outl[i],G.glassTop+.02],[...outl[j],G.glassTop+.02],[...inn[j],G.roofZ],[...inn[i],G.roofZ]],'paint',.05,[outl[i][0]+outl[j][0],outl[i][1]+outl[j][1]-2*yc,.6]);}
    // five roof marker lamps on the front edge
    for(let i=-2;i<=2;i++){boxAt(out,i*.29-.055,i*.29+.055,.50,.62,G.roofZ-.005,G.roofZ+.022,'clad',-.2);
      boxAt(out,i*.29-.04,i*.29+.04,.53,.625,G.roofZ+.02,G.roofZ+.038,'amber',.4);}
    const wb={y:G.cowlY-.10,z:1.68},wt={y:.72,z:2.005};
    const wp=(x,t,o=0)=>[x,wb.y+(wt.y-wb.y)*t+o,wb.z+(wt.z-wb.z)*t+o];
    poly(out,[wp(.95,0),wp(-.95,0),wp(-.82,1),wp(.82,1)],'rubber',-.5,[0,1,1.4]);
    poly(out,[wp(.88,.06,.005),wp(-.88,.06,.005),wp(-.77,.92,.005),wp(.77,.92,.005)],'glass',-.95,[0,1,1.4]);
    poly(out,[wp(.52,.1,.008),wp(.32,.1,.008),wp(-.10,.88,.008),wp(.10,.88,.008)],'glass',-.35,[0,1,1.4]);
    for(const sx of [-1,1])bar(out,wp(sx*.48,.10,.012),wp(sx*.48-.26,.24,.012),.014,'rubber',-.5);
    wallY(out,G.cabRear,-G.hwSide,G.hwSide,.70,G.beltZ,'paint',-.3,-1);
    faceY(out,G.cabRear-.003,[[-gx(G.beltZ),G.beltZ],[gx(G.beltZ),G.beltZ],[gx(G.glassTop),G.glassTop+.02],[-gx(G.glassTop),G.glassTop+.02]],'paint',-.25,-1);
    faceY(out,G.cabRear-.008,rrect(-.72,.72,1.59,1.93,.04,.04),'rubber',-.4,-1);
    faceY(out,G.cabRear-.012,rrect(-.68,.68,1.62,1.90,.03,.03),'glass',-.2,-1);
    for(const x of [-.23,.23])wallY(out,G.cabRear-.016,x-.014,x+.014,1.62,1.90,'rubber',-.3,-1);
    boxAt(out,-.20,.20,G.cabRear-.03,G.cabRear,1.95,1.985,'red',.4);
    slab(out,[[-.96,G.cabRear+.06],[.96,G.cabRear+.06],[.96,G.cowlY-.06],[-.96,G.cowlY-.06]],.90,'shade',-.4);
    wallY(out,G.cabRear+.05,-.96,.96,.90,1.96,'cloth',-.5,1);
  }

  function interior(out,s){
    boxAt(out,-.96,.96,1.06,1.36,1.16,1.60,'rubber',-.25);
    boxAt(out,-.17,.17,1.03,1.06,1.24,1.56,'rubber',-.4);
    wallY(out,1.028,-.12,.12,1.29,1.52,'shade',.5,-1);
    boxAt(out,-.18,.18,.16,1.00,1.02,1.28,'cloth',-.15);
    for(const sx of [-1,1]){
      pairedBox(out,sx,.26,.80,.24,.78,1.08,1.26,'cloth',-.1);
      pairedBox(out,sx,.28,.78,.12,.28,1.25,1.84,'cloth',-.2);
      pairedBox(out,sx,.38,.68,.14,.26,1.85,1.98,'cloth',-.05);
    }
    boxAt(out,-.85,.85,-.72,-.20,1.08,1.26,'cloth',-.1);
    boxAt(out,-.85,.85,-.80,-.68,1.26,1.80,'cloth',-.2);
    for(const x of [-.56,0,.56])boxAt(out,x-.12,x+.12,-.78,-.68,1.82,1.94,'cloth',0);
    const c=[-.50,1.00,1.52];
    for(let i=0;i<14;i++){const a=i*2*Math.PI/14,b=(i+1)*2*Math.PI/14;bar(out,[c[0]+Math.cos(a)*.17,c[1],c[2]+Math.sin(a)*.17],[c[0]+Math.cos(b)*.17,c[1],c[2]+Math.sin(b)*.17],.02,'rubber',-.1);}
    for(const a of [0,Math.PI,Math.PI*1.5])bar(out,c,[c[0]+Math.cos(a)*.16,c[1],c[2]+Math.sin(a)*.16],.014,'galv',-.1);
  }

  function doors(out,s){
    for(const [id,sx,ys,frontDoor] of [['dFL',-1,G.doorF,true],['dFR',1,G.doorF,true],['dRL',-1,G.doorR,false],['dRR',1,G.doorR,false]]){
      const [y0,y1]=ys;
      slab(out,[[sx*.94,y0],[sx*1.01,y0],[sx*1.01,y1],[sx*.94,y1]],.78,'clad',-.1);
      part(out,T=>{
        const wear=wearTex(s.weather);
        const st=[[G.doorZ0,.99],[.80,1.026],[1.15,1.034]];
        for(let i=0;i<2;i++){const[z0,x0]=st[i],[z1,x1]=st[i+1];
          poly(T,[[sx*x0,y0,z0],[sx*x0,y1,z0],[sx*x1,y1,z1],[sx*x1,y0,z1]],'paint',i===0?-.45:-.03,[sx,0,0],[[y0,z0],[y1,z0],[y1,z1],[y0,z1]],wear);}
        poly(T,[[sx*1.034,y0,1.15],[sx*1.034,y1,1.15],[sx*skinUp(G.beltZ),y1,G.beltZ],[sx*skinUp(G.beltZ),y0,G.beltZ]],'paint',.2,[sx,0,.2]);
        rectX(T,sx,1.037,y0+.01,y1-.01,1.14,1.16,'paint',.4);
        rectX(T,sx,1.0,y0+.02,y1-.02,.80,.83,'paint',-.5);
        poly(T,[[sx*1.0,y0+.02,G.beltZ],[sx*1.0,y1-.02,G.beltZ],[sx*.995,y1-.02,G.beltZ+.022],[sx*.995,y0+.02,G.beltZ+.022]],'rubber',.1,[sx,0,.3]);
        const gh=(y,z,o=0)=>[sx*(gx(z)+o),y,z];
        const top=G.glassTop,gt=top-.03,gb=G.beltZ+.03;
        if(frontDoor){
          const rk=z=>y1-(z-G.beltZ)*((y1-.78)/(top-G.beltZ));
          poly(T,[gh(y0,G.beltZ),gh(y0+.06,G.beltZ),gh(y0+.06,top),gh(y0,top)],'rubber',-.2,[sx,0,.3]);
          poly(T,[gh(y0+.06,gt),gh(rk(gt)-.07,gt),gh(rk(top),top),gh(y0+.06,top)],'rubber',-.15,[sx,0,.3]);
          poly(T,[gh(rk(G.beltZ)-.07,G.beltZ),gh(y1,G.beltZ),gh(rk(top),top),gh(rk(gt)-.07,gt)],'rubber',-.15,[sx,0,.3]);
          poly(T,[gh(y0+.06,gb,.006),gh(rk(gb)-.07,gb,.006),gh(rk(gt)-.07,gt,.006),gh(y0+.06,gt,.006)],'glass',-.2,[sx,0,.3]);
          poly(T,[gh(y0+.10,1.86,.008),gh(y0+.34,1.86,.008),gh(y0+.30,1.92,.008),gh(y0+.10,1.92,.008)],'glass',.6,[sx,0,.3]);
          rectX(T,sx,1.036,y1-.40,y1-.18,.95,.985,'galv',.2);
        }else{
          poly(T,[gh(y0,G.beltZ),gh(y1,G.beltZ),gh(y1,top),gh(y0,top)],'rubber',-.2,[sx,0,.3]);
          poly(T,[gh(y0+.06,gb,.006),gh(y1-.05,gb,.006),gh(y1-.05,gt,.006),gh(y0+.06,gt,.006)],'glass',-.2,[sx,0,.3]);
          poly(T,[gh(y0+.12,1.86,.008),gh(y0+.36,1.86,.008),gh(y0+.32,1.92,.008),gh(y0+.12,1.92,.008)],'glass',.6,[sx,0,.3]);
        }
        pairedBox(T,sx,1.034,1.06,y0+.06,y0+.30,1.24,1.295,'paint',.35);
        rectX(T,sx,.95,y0+.04,y1-.04,.80,1.50,'cloth',-.3);
        pairedBox(T,sx,.91,.95,y0+.18,y1-.14,1.14,1.21,'rubber',-.1);
        if(frontDoor&&s.mirrors){
          bar(T,[sx*1.0,y1-.14,1.54],[sx*1.14,y1-.18,1.60],.03,'clad',-.1);
          pairedBox(T,sx,1.10,1.27,y1-.30,y1-.07,1.53,1.76,'clad',.1);
          wallY(T,y1-.305,Math.min(sx*1.115,sx*1.255),Math.max(sx*1.115,sx*1.255),1.56,1.73,'glass',.35,-1);
          pairedBox(T,sx,1.16,1.26,y1-.10,y1-.065,1.53,1.56,'amber',.2);
        }
      },p=>hingeZ(p,sx*1.01,y1,sx*s[id]*68*DEG),id);
    }
    if(s.steps)for(const sx of [-1,1]){
      pairedBox(out,sx,.99,1.12,-.80,1.46,.54,.64,'clad',.0);
      pairedBox(out,sx,1.00,1.115,-.76,1.42,.64,.652,'rubber',.2);
      for(const y of [-.5,.4,1.2])pairedBox(out,sx,.90,1.0,y-.05,y+.05,.56,.62,'iron',-.3);
    }
  }

  function bed(out,s){
    const wear=wearTex(s.weather),lowB=y=>Math.max(.70,archZ(y,G.archR));
    for(const sx of [-1,1]){
      skinX(out,sx,G.tailY,G.bedFront,40,bx,lowB,()=>G.railZ,'paint',sx>0?.12:-.25,wear);
      archLip(out,sx,G.archR,.10,.055,bx,.70,'clad',.05);
      wellLiner(out,sx,G.archR,bx,.62,.70);
      rectX(out,sx,1.034,G.tailY+.14,G.bedFront-.03,1.14,1.16,'paint',.4);
      const[ym,zm]=archPt(G.archR,.42,G.archR.r+.05);
      pairedBox(out,sx,1.08,1.092,ym-.05,ym+.05,zm-.03,zm+.03,'amber',.3);
      pairedBox(out,sx,.88,1.035,G.tailY,G.bedFront,G.railZ,G.railZ+.035,'paint',.2);
      rectX(out,-sx,-.88,G.tailY+.06,G.bedFront-.06,G.bedFloorZ,G.railZ,'bedliner',-.2);
      pairedBox(out,sx,.62,.88,G.axR-.55,G.axR+.55,G.bedFloorZ,1.22,'bedliner',-.4);
      // slim vertical tail lamp with a dark C inside, reverse lamp at its foot
      pairedBox(out,sx,.88,1.04,G.tailY-.018,G.tailY+.10,1.00,1.50,'rubber',-.25);
      pairedBox(out,sx,.892,1.046,G.tailY-.026,G.tailY+.08,1.10,1.485,'red',.05);
      pairedBox(out,sx,.93,.965,G.tailY-.032,G.tailY-.026,1.16,1.43,'shade',-.3);
      pairedBox(out,sx,.93,1.0,G.tailY-.032,G.tailY-.026,1.16,1.185,'shade',-.3);
      pairedBox(out,sx,.892,1.046,G.tailY-.026,G.tailY+.08,1.015,1.10,'galv',.2);
    }
    boxAt(out,-G.hwSide,G.hwSide,G.bedFront-.06,G.bedFront,.70,G.railZ,'paint',0);
    boxAt(out,-1.035,1.035,G.bedFront-.06,G.bedFront,G.railZ,G.railZ+.035,'paint',.2);
    wallY(out,G.bedFront-.061,-.88,.88,G.bedFloorZ,G.railZ,'bedliner',-.2,-1);
    slab(out,[[-.88,G.tailY+.06],[.88,G.tailY+.06],[.88,G.bedFront-.06],[-.88,G.bedFront-.06]],G.bedFloorZ,'bedliner',0,ribTex());
    wallY(out,G.tailY,-G.hwSide,G.hwSide,.88,1.03,'paint',-.3,-1);
    rectX(out,-1,1.036,-1.18,-1.02,1.20,1.34,'paint',-.5);
  }

  function gate(out,s){part(out,T=>{
    const y=G.tailY;
    boxAt(T,-.87,.87,y,y+.06,1.03,1.50,'paint',-.1,false,wearTex(s.weather));
    boxAt(T,-.875,.875,y-.004,y+.064,1.50,1.525,'rubber',.05);
    // the inner gate's outline, a plain badge plate, handle and camera
    faceY(T,y-.003,rrect(-.64,.64,1.10,1.37,.03,.03),'paint',-.35,-1);
    faceY(T,y-.005,rrect(-.62,.62,1.115,1.355,.025,.025),'paint',.02,-1);
    boxAt(T,-.20,.20,y-.016,y,1.395,1.475,'galv',.2);
    wallY(T,y-.017,-.175,.175,1.41,1.46,'hook',.25,-1);
    boxAt(T,-.12,.12,y-.012,y,1.30,1.335,'rubber',-.3);
    boxAt(T,-.03,.03,y-.012,y-.004,1.49,1.515,'glass',-.5);
    wallY(T,y+.061,-.85,.85,1.06,1.49,'bedliner',-.2,1);
  },p=>hingeX(p,G.tailY+.06,1.03,s.gate*92*DEG),'gate');}

  // black 18-inch wheel on a chunky all-terrain tyre: six pockets, a bolt ring, sidewall lugs
  function wheel(out,xc,yc,sx,roll,steerAngle,group){part(out,T=>{
    const r=G.wheelR,w=G.tireW,ph=roll*Math.PI*2;
    tube(T,[xc-w/2,yc,r],[xc+w/2,yc,r],r,22,'rubber',-.05,true,treadTex(roll*2*Math.PI*r,r,22,w,true));
    const xs=xc+sx*(w/2+.006),xf=xc+sx*(w/2+.010);
    tube(T,[xc+sx*(w/2-.012),yc,r],[xs,yc,r],r*.90,22,'rubber',.1);
    const P=(a,rr,o)=>[xf+sx*o,yc+Math.cos(a)*rr,r+Math.sin(a)*rr];
    for(let i=0;i<14;i++){const a=ph+i*2*Math.PI/14;
      poly(T,[P(a-.09,r*.80,-.003),P(a+.09,r*.80,-.003),P(a+.07,r*.89,-.003),P(a-.07,r*.89,-.003)],'rubber',-.45,[sx,0,0]);}
    tube(T,[xf,yc,r],[xf+sx*.012,yc,r],r*.56,16,'iron',.2);
    tube(T,[xf+sx*.013,yc,r],[xf+sx*.018,yc,r],r*.50,16,'iron',.45);
    for(let i=0;i<6;i++){const a=ph+i*Math.PI/3+Math.PI/6;
      poly(T,[P(a-.20,.075,.021),P(a+.20,.075,.021),P(a+.30,.18,.021),P(a-.30,.18,.021)],'shade',-.5,[sx,0,0]);}
    for(let i=0;i<16;i++){const a=ph+i*Math.PI/8;
      poly(T,[P(a-.05,r*.515,.014),P(a+.05,r*.515,.014),P(a+.05,r*.545,.014),P(a-.05,r*.545,.014)],'galv',.1,[sx,0,0]);}
    for(let i=0;i<8;i++){const a=ph+i*Math.PI/4,py=yc+Math.cos(a)*.058,pz=r+Math.sin(a)*.058;
      tube(T,[xf+sx*.02,py,pz],[xf+sx*.032,py,pz],.012,6,'galv',.3);}
    tube(T,[xf+sx*.022,yc,r],[xf+sx*.05,yc,r],.042,12,'iron',.4);
    const vy=yc+Math.cos(ph+.3)*.215,vz=r+Math.sin(ph+.3)*.215;
    tube(T,[xf+sx*.001,vy,vz],[xf+sx*.012,vy,vz],.012,5,'galv',-.3);
  },p=>hingeZ(p,xc,yc,steerAngle),group);}

  function build(s){
    const body=[],rolling=[];
    chassis(body,s);front(body,s);hood(body,s);cab(body,s);interior(body,s);doors(body,s);bed(body,s);gate(body,s);
    for(const f of body){f.group=f.group||'body';f.v=f.v.map(p=>[p[0],p[1],p[2]+bodyOffset(p[1],s)]);}
    tube(rolling,[-.80,G.axF,G.wheelR],[.80,G.axF,G.wheelR],.06,10,'iron',-.2);
    tube(rolling,[-.12,G.axF,G.wheelR],[.18,G.axF,G.wheelR],.13,10,'iron',-.2);
    tube(rolling,[-.80,G.axR,G.wheelR],[.80,G.axR,G.wheelR],.07,10,'iron',-.2);
    tube(rolling,[-.15,G.axR,G.wheelR],[.15,G.axR,G.wheelR],.17,12,'iron',-.2);
    for(const sx of [-1,1]){const id=sx<0?'L':'R';
      wheel(rolling,sx*G.frontWX,G.axF,sx,s.roll+s['wF'+id],s.steer*STEER*DEG,'wheelF'+id);
      wheel(rolling,sx*G.rearWX,G.axR,sx,s.roll+s['wR'+id],0,'wheelR'+id);}
    for(const f of rolling)f.group=f.group||'axles';
    return clean(body.concat(rolling));
  }

  function makeMats(s){
    const cool=r=>s.night?r.map(c=>mix(c,'#101d31',.37)):r;
    const paintRamp=BODY[s.paint];
    const metal=CHROME.map(c=>mix(c,'#555f68',s.weather*.1));
    return {paint:{ramp:cool(paintRamp.map(c=>mix(c,'#665e50',s.weather*.12))),polish:.30},
      bright:{ramp:cool(s.trim==='black'?IRON.map(c=>mix(c,'#5a626b',.18)):metal)},
      iron:{ramp:cool(IRON)},galv:{ramp:cool(GALV)},rubber:{ramp:cool(RUBBER)},
      clad:{ramp:cool(CLAD.map(c=>mix(c,'#5b5347',s.weather*.25)))},shade:{ramp:cool(SHADE)},
      glass:{ramp:s.night?GLASSN:GLASSD},cloth:{ramp:cool(CLOTH)},bedliner:{ramp:cool(LINER)},
      head:{ramp:GLASSD.map(c=>mix(c,'#dfe6e2',0.35))},
      glow:{ramp:s.night?['#9fbfcf','#cfe3ea','#eef7f6','#ffffff']:GLOW},
      led:{ramp:s.night?['#b9d9ec','#e0f1f4','#f4fbfa','#ffffff']:['#859ba4','#aec1c6','#d7e4e3','#edf1eb']},
      amber:{ramp:s.night?['#a26312','#d89932','#efbd5c','#ffe7a3']:LENSA},
      red:{ramp:s.brake?['#aa2116','#d03920','#f16436','#ff9b65']:s.night?LENSR.slice(2):LENSR},
      hook:{ramp:cool(HOOK)}};
  }

  function anchorPoints(opts={}){
    const s=resolve(opts),hl=sweep([.935,G.noseY+.014,1.445]);
    const points={hitch:[0,-3.30,.66],bed:[0,-1.96,G.bedFloorZ],driverSeat:[-.53,.50,1.26],passengerSeat:[.53,.50,1.26],
      rearSeatL:[-.54,-.46,1.26],rearSeatC:[0,-.46,1.26],rearSeatR:[.54,-.46,1.26],
      fuel:[-1.10,-1.10,1.27],exhaust:[.64,-3.25,.40],headlightL:[-hl[0],hl[1],hl[2]],headlightR:hl,
      tailLightL:[-.97,-3.07,1.30],tailLightR:[.97,-3.07,1.30]};
    points.hoodLatch=hingeX(sweep([0,G.noseY-.08,G.hoodZn]),G.cowlY,G.hoodZc,s.hood*50*DEG);
    points.gate=hingeX([0,G.tailY-.01,1.45],G.tailY+.06,1.03,s.gate*92*DEG);
    for(const [id,sx,y] of [['FL',-1,G.doorF],['FR',1,G.doorF],['RL',-1,G.doorR],['RR',1,G.doorR]])
      points['door'+id]=hingeZ([sx*1.06,y[0]+.18,1.27],sx*1.01,y[1],sx*s['d'+id]*68*DEG);
    for(const p of Object.values(points))p[2]+=bodyOffset(p[1],s);
    for(const sx of [-1,1]){const id=sx<0?'L':'R';points['wheelF'+id]=[sx*G.frontWX,G.axF,G.wheelR];points['wheelR'+id]=[sx*G.rearWX,G.axR,G.wheelR];}
    return points;
  }

  // ---- rasteriser (fleet recipe, verbatim) ----
  function paint(faces, B, MATS, s){
    const N=W*H, zbuf=new Float32Array(N).fill(Infinity), dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null), ibuf=new Int16Array(N), nbuf=new Array(N).fill(null);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]); let sh=shadeOf(n, B.se, B.ce);
      if(sh<0 && f.b<=-0.8) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const M=MATS[f.mat]||MATS.paint, ramp=M.ramp, tex=f.tex, uv=f.uv, flat=f.flat;
      const shIdx=(nn,sh0)=>{ let v=sh0*GAIN+BIAS+f.b;
        if(M.polish) v += M.polish*(1.55*nn[2] + 0.50*Math.max(0,1-Math.abs(nn[2])/0.30)*Math.max(0,Math.min(1,sh0*2)));
        return v; };
      let fidx=shIdx(n,sh);
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1],0,t,t+1);
      function fillTri(a,b,c,ia,ib,ic){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy); if(Math.abs(area)<1e-6) return;
        const ua=uv?uv[ia]:null, ub=uv?uv[ib]:null, uc=uv?uv[ic]:null;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){ const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area, w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area, w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-f.db, i=y*W+x;
          if(deff<zbuf[i]){ zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat;
            let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi+=tex(uu,vv); }
            let idx; if(flat){ idx=Math.round(fi); } else { const base=Math.floor(fi); idx=base+((fi-base)>BAYER[x&3][y&3]?1:0); }
            idx=Math.max(0,Math.min(ramp.length-1,idx)); rbuf[i]=ramp; ibuf[i]=idx; } }
      }
    }
    return { rbuf, ibuf, nbuf, dep };
  }
  const LIT={glow:'#eef6f4',led:'#d6eaf2',amber:'#efb951',red:'#e44a29'};
  function post(bufs, s){
    const { rbuf, ibuf, nbuf, dep }=bufs, N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx,ny=y+dy; if(nx>=W||ny>=H) continue; const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j; out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; } } }
    if(s.weather>0.02){ const rnd=mulberry32(9021);
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='paint'||m==='galv'||m==='iron'||m==='rubber'||m==='clad') && rnd()<s.weather*(m==='paint'?.012:.032))
          out[i]=rbuf[i][Math.max(0,ibuf[i]-1)]; } }
    if(s.night){ for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x, m=nbuf[i];
      if(!LIT[m] || (m==='red' && !s.brake && !s.night)) continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*W+(x+dx);
        if(out[j] && !LIT[nbuf[j]]) out[j]=mix(out[j],LIT[m],m==='glow'?.26:.20); } } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; } }
    if(s.outline){ for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
      if(touch) out[i]=KEY; } }
    return out;
  }
  function toRGBA(cols){ const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; }
      const [r,g,b]=hex2rgb(c); rgba[i*4]=r;rgba[i*4+1]=g;rgba[i*4+2]=b;rgba[i*4+3]=255; }
    return rgba;
  }

  function render(dir=3,opts={}){
    if(typeof dir!=='number'||!Number.isFinite(dir))throw new TypeError('facing must be finite');
    if(opts.elev!=null&&(!Number.isFinite(opts.elev)||opts.elev<10||opts.elev>80))throw new RangeError('elevation must be 10..80 degrees');
    const s=resolve(opts),B=camBasis({dir,elev:opts.elev});return toRGBA(post(paint(build(s),B,makeMats(s),s),s));
  }
  function frames(dir,n=8,opts={},cue='doors'){
    if(!Number.isInteger(n)||n<2||n>240)throw new RangeError('frame count must be 2..240');
    if(!Object.hasOwn(CUES,cue))throw new Error('unknown cue '+cue);
    return Array.from({length:n},(_,i)=>render(dir,{...opts,...CUES[cue](i/(['roll','bounce','steer'].includes(cue)?n:n-1))}));
  }
  function project(dir,p,elev=40){const q=projVert(...p,camBasis({dir,elev}));return{x:q.sx,y:q.sy};}
  function anchors(dir,opts={}){return Object.fromEntries(Object.entries(anchorPoints(opts)).map(([k,m])=>[k,{...project(dir,m,opts.elev),m}]));}
  function mesh(opts={}){const s=resolve(opts);return {units:'metres',axes:{x:'passenger/right',y:'forward',z:'up'},
    faces:build(s).map(f=>({vertices:f.v,material:f.mat,group:f.group})),materials:Object.fromEntries(Object.entries(makeMats(s)).map(([k,v])=>[k,v.ramp])),pose:s};}
  function obj(opts={}){
    const m=mesh(opts);let lines=['# '+RIG.label+' - authored procedural mesh','mtllib '+RIG.key+'.mtl'],idx=1,group='';
    for(const f of m.faces){if(f.group!==group){group=f.group;lines.push('g '+group);}lines.push('usemtl '+f.material);
      for(const v of f.vertices)lines.push('v '+v.map(n=>n.toFixed(6)).join(' '));
      for(let i=1;i<f.vertices.length-1;i++)lines.push('f '+[idx,idx+i,idx+i+1].join(' '));idx+=f.vertices.length;
    }
    const mtl=Object.entries(m.materials).map(([k,r])=>{let rgb=hex2rgb(r[Math.floor(r.length/2)]).map(v=>(v/255).toFixed(5));return'newmtl '+k+'\nKd '+rgb.join(' ')+'\nKa 0.15 0.15 0.15\nd 1\n';}).join('\n');
    return{obj:lines.join('\n')+'\n',mtl};
  }
  root.ModernTruck2500={W,H,PX,DIRS:8,pivot:{x:cx,y:groundY},defaultElev:40,order:['N','NE','E','SE','S','SW','W','NW'],
    BODY,PRESETS,CUES,G,travel:{F:TF,R:TR},version:'1.0.0-art-candidate',list:()=>[RIG.key],dims,resolve,render,frames,project,
    anchors,anchorPoints,bodyOffset:(y,o={})=>bodyOffset(y,resolve(o)),mesh,obj};
})(typeof globalThis!=='undefined'?globalThis:window);

/* Modern 350 — Hidden Harbours modern pickup art rig (globalThis.ModernTruck350).
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

  // ================= GEOMETRY — MODERN 350 =================
  // A modern one-tonne crew cab, long box, single rear wheels: a tall blunt nose, three-bar chrome
  // grille, C-clamp lamps, flat sharp shoulders, fender vents, a stepped front-door belt, big tow
  // mirrors, tall tail lamps and a chrome-banded gate. Metres; +x passenger/right, +y nose, +z up.
  const RIG={key:'modern350',label:'Modern 350'};
  const G={bumpF:[3.20,3.38],noseY:3.28,tailY:-3.22,bumpR:[-3.38,-3.22],
    cowlY:1.72,cabRear:-0.80,bedFront:-0.86,axF:2.52,axR:-2.00,
    wheelR:0.435,tireW:0.28,frontWX:0.87,rearWX:0.87,
    hwSide:1.015,beltZ:1.40,beltDrop:1.30,glassTop:1.90,roofZ:2.00,
    hoodZc:1.585,hoodZn:1.47,bedFloorZ:0.96,railZ:1.42,
    doorF:[0.40,1.72],doorR:[-0.76,0.36],doorZ0:0.60,
    archF:{yc:2.52,zc:0.435,r:0.585,p:2.6},archR:{yc:-2.00,zc:0.435,r:0.60,p:2.6}};
  const TF=.11,TR=.13,STEER=30;
  const BODIES={modern350:{key:'modern350',label:'Modern 350',kind:'crew-cab long box, single rear wheels',
    loa:6.76,width:2.11,bodyW:2.03,height:2.00,wheelbase:4.52,wheels:4}};
  const PRESETS={signature:{paint:'deepBlue',weather:.06,trim:'chrome',steps:true},
    platinum:{paint:'white',weather:.04,trim:'chrome',steps:true},
    blackout:{paint:'black',weather:.05,trim:'black',steps:true},
    harbour:{paint:'teal',weather:.30,trim:'chrome',steps:true},
    service:{paint:'silver',weather:.18,hood:1,gate:1},
    nightShift:{paint:'midnight',weather:.10,night:true,steps:true}};
  const CUES={doors:t=>({dFL:t,dFR:t,dRL:t,dRR:t}),hood:t=>({hood:t}),gate:t=>({gate:t}),
    roll:t=>({roll:t}),steer:t=>({steer:Math.sin(t*2*Math.PI)}),
    bounce:t=>({roll:t,susF:Math.sin(t*2*Math.PI)*.65,susR:Math.sin(t*2*Math.PI+1.15)*.65})};
  function resolve(o={}){
    const val=(k,d)=>{if(o[k]==null)return d;if(typeof o[k]!=='number'||!Number.isFinite(o[k]))throw new TypeError(k+' must be finite');return o[k];};
    const c=(k,d=0)=>Math.max(0,Math.min(1,val(k,d))),s=(k)=>Math.max(-1,Math.min(1,val(k,0)));
    if(o.paint!=null&&!Object.hasOwn(BODY,o.paint))throw new Error('unknown paint '+o.paint);
    if(o.trim!=null&&!['chrome','black'].includes(o.trim))throw new Error('unknown trim '+o.trim);
    return {body:RIG.key,paint:o.paint||'deepBlue',trim:o.trim||'chrome',weather:c('weather',.06),
      dFL:c('dFL'),dFR:c('dFR'),dRL:c('dRL'),dRR:c('dRR'),hood:c('hood'),gate:c('gate'),
      roll:val('roll',0),wFL:val('wFL',0),wFR:val('wFR',0),wRL:val('wRL',0),wRR:val('wRR',0),
      susF:s('susF'),susR:s('susR'),steer:s('steer'),night:!!o.night,brake:!!o.brake,
      steps:o.steps!==false,mirrors:o.mirrors!==false,hitch:o.hitch!==false,outline:!!o.outline};
  }
  function dims(){return {...BODIES.modern350,travelF:TF,travelR:TR};}
  function bodyOffset(y,s){const t=(y-G.axR)/(G.axF-G.axR);return -s.susF*TF*t-s.susR*TR*(1-t);}

  // the blunt face eases back at its corners so it still turns the corner in the ¾ views
  const sweep=([x,y,z])=>{if(y<=3.0)return[x,y,z];const k=Math.min(1,(y-3.0)/.28);return[x,y-.07*k*Math.min(1,Math.abs(x)/1.02)**4,z];};
  const fx=y=>1.02+.012*Math.exp(-(((y-G.axF)/.5)**2))-.05*Math.max(0,(y-3.12)/.16)**2;
  const fTop=y=>G.hoodZn+(G.hoodZc-G.hoodZn)*Math.max(0,Math.min(1,(G.noseY-y)/(G.noseY-G.cowlY)))-.012;
  const hwN=fx(G.noseY);
  const gx=z=>.985-(z-G.beltZ)*.30;                  // greenhouse half-width
  const bx=y=>G.hwSide+.008*Math.exp(-(((y-G.axR)/.6)**2));
  // front-door belt: level, then stepping down toward the mirror
  const beltF=y=>y<=1.14?G.beltZ:y>=1.50?G.beltDrop:G.beltZ+(G.beltDrop-G.beltZ)*(y-1.14)/.36;
  const skinUp=z=>1.018+(.985-1.018)*(z-1.13)/(G.beltZ-1.13);

  function chassis(out,s){
    for(const sx of [-1,1]){
      pairedBox(out,sx,.44,.53,-3.20,3.12,.47,.62,'iron',-.4);
      pairedBox(out,sx,.60,.76,G.axR-.62,G.axR+.58,.47,.52,'iron',-.2);
      tube(out,[sx*.66,G.axR+.06,.52],[sx*.82,G.axR+.24,.86],.034,8,'galv',0);
      tube(out,[sx*.60,G.axF-.10,.54],[sx*.74,G.axF+.08,.92],.034,8,'galv',0);
    }
    for(const y of [-2.9,-1.3,.3,1.6,2.8])boxAt(out,-.48,.48,y-.05,y+.05,.49,.58,'iron',-.4);
    boxAt(out,-.92,-.47,-1.40,-.10,.36,.62,'iron',0);
    bar(out,[.06,G.axR+.22,.46],[.06,G.axF-.34,.46],.045,'iron',-.3);
    bar(out,[.50,1.70,.42],[.66,-2.66,.38],.045,'iron',-.3);
    tube(out,[.70,-2.72,.36],[.74,-3.30,.34],.062,12,'chrome',.2);
    tube(out,[.74,-3.295,.34],[.74,-3.31,.34],.042,12,'shade',-.8);
    // front bumper: a faceted chrome brow, a creased dark lower, a chrome-ringed honeycomb
    // intake, fog lamps in chrome bezels, corner air-curtain slots, a bright skid lip, tow hooks
    const fb=out.length;
    const brow=[[3.21,.84],[3.36,.84],[3.38,.862],[3.38,.90],[3.352,.932],[3.21,.932]];
    for(let i=0;i<brow.length-1;i++){const[a0,a1]=brow[i],[b0,b1]=brow[i+1];
      poly(out,[[-1,a0,a1],[1,a0,a1],[1,b0,b1],[-1,b0,b1]],'bright',[-.3,-.1,.15,.5,.3][i],[0,b1-a1,a0-b0]);}
    for(const sx of [-1,1])poly(out,brow.map(([y,z])=>[sx,y,z]),'bright',sx>0?.18:-.3,[sx,0,0]);
    boxAt(out,-.99,.99,3.21,3.365,.46,.84,'rubber',.12);
    wallY(out,3.366,-.97,.97,.782,.80,'rubber',.6,1);
    wallY(out,3.366,-.97,.97,.764,.782,'rubber',-.5,1);
    faceY(out,3.368,rrect(-.585,.585,.515,.745,.03,.03),'shade',-.6);
    for(let r=0;r<4;r++){const z=.53+r*.052;for(let x=-.55+(r%2)*.04;x<.54;x+=.08)wallY(out,3.37,x,x+.05,z,z+.022,'iron',.25,1);}
    ringY(out,3.372,rrect(-.61,.61,.495,.765,.035,.035),rrect(-.585,.585,.515,.745,.03,.03),'bright',.2);
    for(const sx of [-1,1]){const M=pts=>pts.map(([x,z])=>[sx*x,z]);
      faceY(out,3.368,M(rrect(.675,.915,.575,.725,.03,.03)),'shade',-.5);
      ringY(out,3.371,M(rrect(.66,.93,.56,.74,.035,.035)),M(rrect(.675,.915,.575,.725,.03,.03)),'bright',.2);
      faceY(out,3.372,M(rrect(.715,.875,.615,.685,.02,.02)),s.night?'glow':'head',.35);
      wallY(out,3.367,Math.min(sx*.945,sx*.972),Math.max(sx*.945,sx*.972),.54,.76,'shade',-.6,1);
      pairedBox(out,sx,.34,.46,3.30,3.40,.40,.46,'iron',-.2);
    }
    boxAt(out,-.52,.52,3.36,3.378,.46,.49,'galv',.2);
    for(let i=fb;i<out.length;i++)out[i].v=out[i].v.map(sweep);
    // rear bumper: chrome, step pads, plate recess, receiver
    boxAt(out,-1.02,1.02,-3.38,-3.22,.52,.84,'bright',-.1);
    boxAt(out,-.36,.36,-3.37,-3.23,.84,.856,'rubber',.1);
    for(const sx of [-1,1])pairedBox(out,sx,.72,.98,-3.37,-3.23,.84,.856,'rubber',.1);
    boxAt(out,-.21,.21,-3.386,-3.378,.57,.73,'shade',-.5);
    wallY(out,-3.389,-.16,.16,.59,.71,'galv',.1,-1);
    boxAt(out,-1.0,1.0,-3.34,-3.24,.46,.52,'rubber',-.2);
    if(s.hitch){boxAt(out,-.07,.07,-3.52,-3.36,.38,.50,'iron',-.2);tube(out,[0,-3.48,.50],[0,-3.48,.62],.035,10,'chrome',.2);}
  }

  function front(out,s){
    const start=out.length,wear=wearTex(s.weather),lens=s.night?'glow':'head',yN=G.noseY;
    const lowF=y=>Math.max(y>3.10?.925:.60,archZ(y,G.archF));
    for(const sx of [-1,1]){
      skinX(out,sx,G.cowlY,yN,44,fx,lowF,fTop,'paint',sx>0?.12:-.25,wear);
      for(let j=0;j<22;j++){const a=G.cowlY+(yN-G.cowlY)*j/22,b=G.cowlY+(yN-G.cowlY)*(j+1)/22;
        poly(out,[[sx*.875,a,fTop(a)+.012],[sx*.875,b,fTop(b)+.012],[sx*fx(b),b,fTop(b)],[sx*fx(a),a,fTop(a)]],'paint',.28,[sx*.15,0,1]);}
      // hood shut line along the shoulder's inner edge; a shoulder crease under the fender top
      for(let j=0;j<11;j++){const a=G.cowlY+(yN-G.cowlY)*j/11,b=G.cowlY+(yN-G.cowlY)*(j+1)/11;
        poly(out,[[sx*.868,a,fTop(a)+.0135],[sx*.868,b,fTop(b)+.0135],[sx*.892,b,fTop(b)+.0125],[sx*.892,a,fTop(a)+.0125]],'shade',-.2,[0,0,1]);}
      creaseX(out,sx,G.cowlY+.04,3.12,18,fx,y=>fTop(y)-.085);
      archLip(out,sx,G.archF,.06,.024,fx,.60,'paint',.18);
      wellLiner(out,sx,G.archF,fx,.66,.60);
      // fender vent: chrome frame, three dark slots
      const xv=fx(1.84);
      pairedBox(out,sx,xv-.004,xv+.014,1.77,1.91,.90,1.24,'bright',.2);
      for(const z of [.95,1.05,1.15])pairedBox(out,sx,xv+.010,xv+.017,1.79,1.89,z,z+.06,'shade',-.4);
      rectX(out,sx,fx(3.05)+.004,3.00,3.10,1.22,1.27,'amber',.25);
    }
    // grille: chamfered chrome surround under a brow bar, fine mesh, three deep chrome bars
    // casting shadows, a plain oval blank on the middle bar (no lettering)
    const O=[[-.665,.985],[-.615,.925],[.615,.925],[.665,.985],[.665,1.445],[-.665,1.445]];
    const I=[[-.605,1.005],[-.585,.968],[.585,.968],[.605,1.005],[.605,1.395],[-.605,1.395]];
    faceY(out,yN-.02,O,'shade',-.5);
    for(let x=-.575;x<.58;x+=.05)wallY(out,yN-.014,x,x+.011,.975,1.39,'iron',-.2,1);
    ringY(out,yN+.014,O,I,'bright',.25);
    for(let i=0;i<O.length;i++){const a=O[i],b=O[(i+1)%O.length];
      poly(out,[[a[0],yN-.02,a[1]],[b[0],yN-.02,b[1]],[b[0],yN+.014,b[1]],[a[0],yN+.014,a[1]]],'bright',-.05,[a[0]+b[0],0,a[1]+b[1]-2.37]);}
    boxAt(out,-.665,.665,yN-.02,yN+.03,1.395,1.445,'bright',.3);
    for(const z of [1.03,1.165,1.30]){boxAt(out,-.605,.605,yN-.014,yN+.034,z,z+.068,'bright',.18);
      wallY(out,yN-.013,-.60,.60,z-.022,z,'shade',-.7,1);}
    faceY(out,yN+.036,ellipse(0,1.199,.125,.05,14),'shade',-.25);
    faceY(out,yN+.039,ellipse(0,1.199,.10,.034,14),'galv',.15);
    // C-clamp lamps: chrome-lined pocket, round projector in a bezel, a high-beam cell, and one
    // continuous LED stroke with rounded corners that opens toward the grille
    for(const sx of [-1,1]){
      const M=pts=>pts.map(([x,z])=>[sx*x,z]);
      pairedBox(out,sx,.665,hwN,yN-.04,yN+.004,1.13,1.445,'shade',-.5);
      ringY(out,yN+.006,M(rrect(.675,hwN-.006,1.14,1.438,.03,.03)),M(rrect(.692,hwN-.022,1.155,1.423,.02,.02)),'bright',.05);
      faceY(out,yN+.008,M(ellipse(.80,1.29,.085,.085,14)),'bright',.35);
      faceY(out,yN+.012,M(ellipse(.80,1.29,.062,.062,14)),lens,.45);
      faceY(out,yN+.015,M(ellipse(.80,1.29,.026,.026,10)),s.night?'glow':'glass',.8);
      pairedBox(out,sx,.895,.925,yN+.006,yN+.014,1.25,1.33,lens,.3);
      const r=.045,xo=hwN-.03;
      strokeY(out,yN+.017,M([[.70,1.412],...arcPts(xo-r,1.412-r,r,Math.PI/2,0,5),...arcPts(xo-r,1.168+r,r,0,-Math.PI/2,5),[.72,1.168]]),.03,'led',.5,1);
      faceY(out,yN+.008,M([[.68,1.035],[hwN-.012,1.035],[hwN-.012,1.098],[.70,1.098]]),'amber',.15);
      wallY(out,yN,Math.min(sx*.665,sx*hwN),Math.max(sx*.665,sx*hwN),.925,1.03,'paint',-.05,1);
      wallY(out,yN,Math.min(sx*.665,sx*hwN),Math.max(sx*.665,sx*hwN),1.10,1.13,'paint',-.05,1);
      // a crease from the lamp's foot sweeping down into the bumper corner
      strokeY(out,yN+.003,M([[.69,1.018],[.84,.985],[hwN-.008,.94]]),.022,'paint',.5,1);
    }
    // engine bay (seen with the hood up)
    boxAt(out,-.86,.86,G.cowlY+.04,yN-.10,.80,1.00,'shade',-.65);
    for(const sx of [-1,1])pairedBox(out,sx,.70,.87,G.cowlY+.04,yN-.10,1.00,1.44,'iron',-.5);
    boxAt(out,-.42,.42,2.02,3.00,1.00,1.34,'iron',-.2);
    boxAt(out,-.30,.30,2.12,2.88,1.34,1.42,'galv',-.05);
    for(let y=2.2;y<2.85;y+=.13)boxAt(out,-.29,.29,y,y+.04,1.419,1.432,'iron',-.3);
    boxAt(out,-.80,-.50,2.66,3.08,1.04,1.34,'rubber',-.1);
    boxAt(out,-.66,.66,3.10,3.17,.96,1.40,'iron',-.3);
    tube(out,[.36,2.10,1.30],[.62,2.86,1.40],.08,10,'rubber',-.1);
    wallY(out,G.cowlY+.035,-.87,.87,1.0,G.hoodZc-.01,'shade',-.8,1);
    // cowl plenum between the hood's hinge line and the windshield
    poly(out,[[-.93,G.cowlY-.10,1.60],[.93,G.cowlY-.10,1.60],[.93,G.cowlY+.01,G.hoodZc+.004],[-.93,G.cowlY+.01,G.hoodZc+.004]],'rubber',-.1,[0,.3,1]);
    for(let x=-.80;x<.81;x+=.08)poly(out,[[x,G.cowlY-.08,1.601],[x+.04,G.cowlY-.08,1.601],[x+.04,G.cowlY-.01,G.hoodZc+.005],[x,G.cowlY-.01,G.hoodZc+.005]],'shade',-.3,[0,.3,1]);
    for(let i=start;i<out.length;i++){out[i].v=out[i].v.map(sweep);out[i].group='frontClip';}
  }

  function hood(out,s){part(out,T=>{
    // tall, nearly flat hood: a broad raised centre between two crisp creases, outer panels that
    // dip toward the lamp corners, and a blunt lip whose corners curve down over the lamps
    const xs=[-.875,-.74,-.60,-.47,-.44,-.30,.30,.44,.47,.60,.74,.875];
    const dz=[0,0,.012,.03,.058,.066,.066,.058,.03,.012,0,0];
    const yF=G.noseY+.02,ys=[G.cowlY,2.10,2.50,2.90,3.08,3.20,yF];
    const zAt=y=>G.hoodZc+(G.hoodZn-G.hoodZc)*(y-G.cowlY)/(yF-G.cowlY);
    const dip=(x,y)=>-.015*Math.max(0,(Math.abs(x)-.62)/.255)**1.5*Math.max(0,(y-2.95)/(yF-2.95));
    const P=(i,y,d=0)=>sweep([xs[i],y,zAt(y)+dz[i]+dip(xs[i],y)+d]);
    const wear=wearTex(s.weather);
    for(let j=0;j<ys.length-1;j++)for(let i=0;i<xs.length-1;i++){
      const q=[P(i,ys[j]),P(i+1,ys[j]),P(i+1,ys[j+1]),P(i,ys[j+1])],crease=(i===3||i===7);
      poly(T,q,'paint',crease?(i===3?-.25:.45):.1,[0,0,1],q.map(p=>[p[0],p[1]]),wear);
      poly(T,[P(i,ys[j],-.03),P(i+1,ys[j],-.03),P(i+1,ys[j+1],-.03),P(i,ys[j+1],-.03)],'shade',-.9,[0,0,-1]);
    }
    for(let i=0;i<xs.length-1;i++){const a=P(i,yF),b=P(i+1,yF);
      poly(T,[a,b,[b[0],b[1]-.006,b[2]-.036],[a[0],a[1]-.006,a[2]-.036]],'paint',-.02,[0,1,-.1]);}
  },p=>hingeX(p,G.cowlY,G.hoodZc,s.hood*48*DEG),'hood');}

  function cab(out,s){
    for(const sx of [-1,1]){
      rectX(out,sx,.975,G.cabRear+.02,G.cowlY,.55,.605,'paint',-.55);
      for(const [y0,y1] of [[G.doorR[1],G.doorF[0]],[G.cabRear,G.doorR[0]]])
        rectX(out,sx,G.hwSide,y0,y1,G.doorZ0,G.beltZ,'paint',sx>0?.1:-.25);
      const gh=(y,z)=>[sx*gx(z),y,z];
      poly(out,[gh(G.doorR[1]-.03,G.beltZ),gh(G.doorF[0]+.03,G.beltZ),gh(G.doorF[0]+.03,G.glassTop),gh(G.doorR[1]-.03,G.glassTop)],'rubber',-.1,[sx,0,.3]);
      poly(out,[gh(G.cabRear,G.beltZ),gh(G.doorR[0]+.02,G.beltZ),gh(G.doorR[0]+.02,G.glassTop),gh(G.cabRear,G.glassTop)],'paint',-.05,[sx,0,.3]);
      // A pillar: from the front door's raked frame to the windshield's side edge
      poly(out,[gh(G.cowlY,G.beltZ),[sx*.93,G.cowlY-.10,1.60],[sx*.80,1.00,1.935],gh(1.06,G.glassTop)],'paint',.05,[sx,.5,.6]);
    }
    // roof: chamfered perimeter over a gentle crown
    const R0=G.cabRear+.01,R1=1.00,yc=(R0+R1)/2;
    const outl=[[-.78,R0],[.78,R0],[.84,R0+.08],[.84,R1-.12],[.78,R1],[-.78,R1],[-.84,R1-.12],[-.84,R0+.08]];
    const inn=outl.map(([x,y])=>[x*.9,yc+(y-yc)*.95]);
    slab(out,inn,G.roofZ,'paint',.12);
    for(let i=0;i<8;i++){const j=(i+1)%8;poly(out,[[...outl[i],G.glassTop+.02],[...outl[j],G.glassTop+.02],[...inn[j],G.roofZ],[...inn[i],G.roofZ]],'paint',.05,[outl[i][0]+outl[j][0],outl[i][1]+outl[j][1]-2*yc,.6]);}
    // windshield: raked, framed in black, a lighter reflection streak
    const wb={y:G.cowlY-.10,z:1.60},wt={y:1.00,z:1.935};
    const wp=(x,t,o=0)=>[x,wb.y+(wt.y-wb.y)*t+o,wb.z+(wt.z-wb.z)*t+o];
    poly(out,[wp(.93,0),wp(-.93,0),wp(-.80,1),wp(.80,1)],'rubber',-.5,[0,1,1.4]);
    poly(out,[wp(.86,.06,.005),wp(-.86,.06,.005),wp(-.75,.92,.005),wp(.75,.92,.005)],'glass',-.95,[0,1,1.4]);
    poly(out,[wp(.50,.1,.008),wp(.30,.1,.008),wp(-.12,.88,.008),wp(.08,.88,.008)],'glass',-.35,[0,1,1.4]);
    for(const sx of [-1,1])bar(out,wp(sx*.46,.10,.012),wp(sx*.46-.26,.24,.012),.014,'rubber',-.5);
    // cab back: vertical wall, three-pane slider, third brake lamp
    wallY(out,G.cabRear,-G.hwSide,G.hwSide,.64,G.beltZ,'paint',-.3,-1);
    faceY(out,G.cabRear-.003,[[-gx(G.beltZ),G.beltZ],[gx(G.beltZ),G.beltZ],[gx(G.glassTop),G.glassTop+.02],[-gx(G.glassTop),G.glassTop+.02]],'paint',-.25,-1);
    faceY(out,G.cabRear-.008,rrect(-.70,.70,1.49,1.84,.04,.04),'rubber',-.4,-1);
    faceY(out,G.cabRear-.012,rrect(-.66,.66,1.52,1.81,.03,.03),'glass',-.2,-1);
    for(const x of [-.22,.22])wallY(out,G.cabRear-.016,x-.014,x+.014,1.52,1.81,'rubber',-.3,-1);
    boxAt(out,-.19,.19,G.cabRear-.03,G.cabRear,1.86,1.895,'red',.4);
    // floor and back of the cabin, seen through open doors
    slab(out,[[-.95,G.cabRear+.06],[.95,G.cabRear+.06],[.95,G.cowlY-.06],[-.95,G.cowlY-.06]],.82,'shade',-.4);
    wallY(out,G.cabRear+.05,-.95,.95,.82,1.88,'cloth',-.5,1);
  }

  function interior(out,s){
    boxAt(out,-.95,.95,1.36,1.66,1.08,1.50,'rubber',-.25);
    boxAt(out,-.16,.16,1.33,1.36,1.16,1.46,'rubber',-.4);
    wallY(out,1.328,-.11,.11,1.21,1.42,'shade',.5,-1);
    boxAt(out,-.18,.18,.40,1.30,.94,1.20,'cloth',-.15);
    for(const sx of [-1,1]){
      pairedBox(out,sx,.26,.80,.48,1.02,1.00,1.18,'cloth',-.1);
      pairedBox(out,sx,.28,.78,.36,.52,1.17,1.76,'cloth',-.2);
      pairedBox(out,sx,.38,.68,.38,.50,1.77,1.90,'cloth',-.05);
    }
    boxAt(out,-.84,.84,-.72,-.16,1.00,1.18,'cloth',-.1);
    boxAt(out,-.84,.84,-.80,-.66,1.18,1.72,'cloth',-.2);
    for(const x of [-.56,0,.56])boxAt(out,x-.12,x+.12,-.78,-.66,1.74,1.86,'cloth',0);
    const c=[-.50,1.30,1.42];
    for(let i=0;i<14;i++){const a=i*2*Math.PI/14,b=(i+1)*2*Math.PI/14;bar(out,[c[0]+Math.cos(a)*.17,c[1],c[2]+Math.sin(a)*.17],[c[0]+Math.cos(b)*.17,c[1],c[2]+Math.sin(b)*.17],.02,'rubber',-.1);}
    for(const a of [0,Math.PI,Math.PI*1.5])bar(out,c,[c[0]+Math.cos(a)*.16,c[1],c[2]+Math.sin(a)*.16],.014,'galv',-.1);
  }

  function doors(out,s){
    for(const [id,sx,ys,frontDoor] of [['dFL',-1,G.doorF,true],['dFR',1,G.doorF,true],['dRL',-1,G.doorR,false],['dRR',1,G.doorR,false]]){
      const [y0,y1]=ys;
      slab(out,[[sx*.93,y0],[sx*1.0,y0],[sx*1.0,y1],[sx*.93,y1]],.70,'bright',-.1);
      part(out,T=>{
        const wear=wearTex(s.weather);
        const belt=frontDoor?beltF:()=>G.beltZ;
        // pressed skin: tuck under, flat flank, crease, tumblehome up to the belt
        const st=[[G.doorZ0,.975],[.72,1.012],[1.13,1.018]];
        for(let i=0;i<2;i++){const[z0,x0]=st[i],[z1,x1]=st[i+1];
          poly(T,[[sx*x0,y0,z0],[sx*x0,y1,z0],[sx*x1,y1,z1],[sx*x1,y0,z1]],'paint',i===0?-.45:-.03,[sx,0,0],[[y0,z0],[y1,z0],[y1,z1],[y0,z1]],wear);}
        const brk=frontDoor?[y0,1.14,1.50,y1]:[y0,y1];
        for(let i=0;i<brk.length-1;i++){const a=brk[i],b=brk[i+1];
          poly(T,[[sx*1.018,a,1.13],[sx*1.018,b,1.13],[sx*skinUp(belt(b)),b,belt(b)],[sx*skinUp(belt(a)),a,belt(a)]],'paint',.2,[sx,0,.2]);}
        rectX(T,sx,1.021,y0+.01,y1-.01,1.12,1.14,'paint',.38);
        // chrome belt trim following the belt line
        for(let i=0;i<brk.length-1;i++){const a=brk[i]+(i===0?.03:0),b=brk[i+1]-(i===brk.length-2?.03:0);
          poly(T,[[sx*.992,a,belt(a)],[sx*.992,b,belt(b)],[sx*.99,b,belt(b)+.022],[sx*.99,a,belt(a)+.022]],'bright',.15,[sx,0,.3]);}
        // greenhouse: black sash, glass, the drop pane at the front door's nose
        const gh=(y,z,o=0)=>[sx*(gx(z)+o),y,z];
        const top=G.glassTop,gt=top-.03,gb=G.beltZ+.03;
        if(frontDoor){
          const rk=z=>y1-(z-G.beltZ)*((y1-1.06)/(top-G.beltZ));  // raked front frame line
          poly(T,[gh(y0,G.beltZ),gh(y0+.06,G.beltZ),gh(y0+.06,top),gh(y0,top)],'rubber',-.2,[sx,0,.3]);
          poly(T,[gh(y0+.06,gt),gh(rk(gt)-.07,gt),gh(rk(top),top),gh(y0+.06,top)],'rubber',-.15,[sx,0,.3]);
          poly(T,[gh(rk(G.beltZ)-.07,G.beltZ),gh(y1,G.beltZ),gh(rk(top),top),gh(rk(gt)-.07,gt)],'rubber',-.15,[sx,0,.3]);
          poly(T,[gh(y0+.06,gb,.006),gh(rk(gb)-.07,gb,.006),gh(rk(gt)-.07,gt,.006),gh(y0+.06,gt,.006)],'glass',-.2,[sx,0,.3]);
          poly(T,[[sx*.978,1.14,G.beltZ+.022],[sx*.978,1.50,G.beltDrop+.022],[sx*.978,rk(G.beltZ)-.07,G.beltDrop+.022],[sx*.978,rk(G.beltZ)-.07,gb],[sx*.978,1.14,gb]],'glass',-.1,[sx,0,0]);
          poly(T,[gh(y0+.10,1.78,.008),gh(y0+.34,1.78,.008),gh(y0+.30,1.84,.008),gh(y0+.10,1.84,.008)],'glass',.6,[sx,0,.3]);
        }else{
          poly(T,[gh(y0,G.beltZ),gh(y1,G.beltZ),gh(y1,top),gh(y0,top)],'rubber',-.2,[sx,0,.3]);
          poly(T,[gh(y0+.06,gb,.006),gh(y1-.05,gb,.006),gh(y1-.05,gt,.006),gh(y0+.06,gt,.006)],'glass',-.2,[sx,0,.3]);
          poly(T,[gh(y0+.12,1.78,.008),gh(y0+.36,1.78,.008),gh(y0+.32,1.84,.008),gh(y0+.12,1.84,.008)],'glass',.6,[sx,0,.3]);
        }
        pairedBox(T,sx,1.018,1.046,y0+.06,y0+.30,1.155,1.21,'bright',.25);        // handle
        rectX(T,sx,.94,y0+.04,y1-.04,.74,1.40,'cloth',-.3);                        // inner panel
        pairedBox(T,sx,.90,.94,y0+.18,y1-.14,1.06,1.13,'rubber',-.1);
        if(frontDoor&&s.mirrors){
          bar(T,[sx*.99,y1-.14,1.44],[sx*1.18,y1-.20,1.52],.028,'rubber',-.1);
          bar(T,[sx*.99,y1-.10,1.37],[sx*1.18,y1-.18,1.40],.022,'rubber',-.2);
          pairedBox(T,sx,1.16,1.33,y1-.32,y1-.08,1.34,1.56,'rubber',.05);
          pairedBox(T,sx,1.16,1.33,y1-.32,y1-.08,1.56,1.72,'bright',.05);
          wallY(T,y1-.325,Math.min(sx*1.175,sx*1.315),Math.max(sx*1.175,sx*1.315),1.38,1.62,'glass',.35,-1);
          wallY(T,y1-.327,Math.min(sx*1.19,sx*1.30),Math.max(sx*1.19,sx*1.30),1.64,1.70,'glass',.05,-1);
          rectX(T,sx,1.334,y1-.24,y1-.12,1.62,1.66,'amber',.2);
        }
      },p=>hingeZ(p,sx*1.0,y1,sx*s[id]*68*DEG),id);
    }
    if(s.steps)for(const sx of [-1,1]){
      pairedBox(out,sx,1.02,1.20,-.72,1.66,.42,.47,'rubber',-.05);
      pairedBox(out,sx,1.185,1.205,-.70,1.64,.43,.465,'bright',.25);
      for(const y of [-.3,.95])pairedBox(out,sx,.94,1.03,y-.05,y+.05,.44,.56,'iron',-.3);
    }
  }

  function bed(out,s){
    const wear=wearTex(s.weather),lowB=y=>Math.max(.64,archZ(y,G.archR));
    for(const sx of [-1,1]){
      skinX(out,sx,G.tailY,G.bedFront,44,bx,lowB,()=>G.railZ,'paint',sx>0?.12:-.25,wear);
      archLip(out,sx,G.archR,.06,.024,bx,.64,'paint',.18);
      wellLiner(out,sx,G.archR,bx,.62,.64);
      rectX(out,sx,bx(-2)+.004,G.tailY+.14,G.bedFront-.03,1.12,1.14,'paint',.38);
      pairedBox(out,sx,.87,1.03,G.tailY,G.bedFront,G.railZ,G.railZ+.04,'rubber',.05);
      rectX(out,-sx,-.87,G.tailY+.06,G.bedFront-.06,G.bedFloorZ,G.railZ,'bedliner',-.2);
      pairedBox(out,sx,.62,.87,G.axR-.56,G.axR+.56,G.bedFloorZ,1.17,'bedliner',-.4);
      for(const y of [-3.08,-1.02])pairedBox(out,sx,.92,1.0,y-.05,y+.05,G.railZ+.04,G.railZ+.046,'shade',-.5);
      // tall corner lamp: red, a clear reverse band, red
      pairedBox(out,sx,.865,1.028,G.tailY-.018,G.tailY+.12,.96,1.41,'rubber',-.25);
      pairedBox(out,sx,.878,1.034,G.tailY-.026,G.tailY+.10,1.21,1.395,'red',.1);
      pairedBox(out,sx,.878,1.034,G.tailY-.026,G.tailY+.10,1.10,1.21,'galv',.25);
      pairedBox(out,sx,.878,1.034,G.tailY-.026,G.tailY+.10,.975,1.10,'red',.1);
      rectX(out,sx,bx(-2.94)+.004,-2.99,-2.91,1.17,1.22,'red',.25);
    }
    boxAt(out,-G.hwSide,G.hwSide,G.bedFront-.06,G.bedFront,.64,G.railZ,'paint',0);
    boxAt(out,-1.03,1.03,G.bedFront-.06,G.bedFront,G.railZ,G.railZ+.04,'rubber',0);
    wallY(out,G.bedFront-.061,-.87,.87,G.bedFloorZ,G.railZ,'bedliner',-.2,-1);
    slab(out,[[-.87,G.tailY+.06],[.87,G.tailY+.06],[.87,G.bedFront-.06],[-.87,G.bedFront-.06]],G.bedFloorZ,'bedliner',0,ribTex());
    wallY(out,G.tailY,-G.hwSide,G.hwSide,.84,.97,'paint',-.3,-1);
    rectX(out,-1,bx(-1.17)+.006,-1.26,-1.08,1.16,1.30,'paint',-.5);
  }

  function gate(out,s){part(out,T=>{
    const y=G.tailY;
    boxAt(T,-.86,.86,y,y+.06,.97,1.41,'paint',-.1,false,wearTex(s.weather));
    boxAt(T,-.865,.865,y-.004,y+.064,1.41,1.445,'rubber',.05);
    boxAt(T,-.03,.03,y-.012,y-.004,1.415,1.44,'glass',-.5);
    faceY(T,y-.006,rrect(-.80,.80,1.14,1.27,.03,.03),'bright',.2,-1);
    wallY(T,y-.008,-.76,.76,1.200,1.212,'rubber',-.2,-1);
    wallY(T,y-.003,-.84,.84,1.30,1.312,'paint',.35,-1);
    wallY(T,y-.003,-.84,.84,1.05,1.062,'paint',-.4,-1);
    faceY(T,y-.01,ellipse(.60,1.345,.07,.028,10),'bright',.1,-1);
    boxAt(T,-.14,.14,y-.014,y,1.35,1.395,'rubber',-.3);
    wallY(T,y+.061,-.84,.84,1.0,1.40,'bedliner',-.2,1);
  },p=>hingeX(p,G.tailY+.06,.97,s.gate*92*DEG),'gate');}

  // polished 20-inch wheel: eight spokes cut by dark pockets, chrome lip, lugs, valve index
  function wheel(out,xc,yc,sx,roll,steerAngle,group){part(out,T=>{
    const r=G.wheelR,w=G.tireW,ph=roll*Math.PI*2;
    tube(T,[xc-w/2,yc,r],[xc+w/2,yc,r],r,20,'rubber',-.1,true,treadTex(roll*2*Math.PI*r,r,28,w,false));
    const xf=xc+sx*(w/2+.008);
    tube(T,[xc+sx*(w/2-.013),yc,r],[xf,yc,r],r*.84,20,'rubber',.05);
    tube(T,[xf,yc,r],[xf+sx*.012,yc,r],r*.60,16,'chrome',.45);
    tube(T,[xf+sx*.013,yc,r],[xf+sx*.018,yc,r],r*.54,16,'chrome',.1);
    const P=(a,rr,o)=>[xf+sx*o,yc+Math.cos(a)*rr,r+Math.sin(a)*rr];
    for(let i=0;i<8;i++){const a=ph+i*Math.PI/4+Math.PI/8;
      poly(T,[P(a-.16,.085,.021),P(a+.16,.085,.021),P(a+.24,.212,.021),P(a-.24,.212,.021)],'shade',-.5,[sx,0,0]);}
    for(let i=0;i<8;i++){const a=ph+i*Math.PI/4,py=yc+Math.cos(a)*.062,pz=r+Math.sin(a)*.062;
      tube(T,[xf+sx*.02,py,pz],[xf+sx*.034,py,pz],.013,6,'galv',.3);}
    tube(T,[xf+sx*.024,yc,r],[xf+sx*.058,yc,r],.048,12,'chrome',.35);
    const vy=yc+Math.cos(ph+.3)*.245,vz=r+Math.sin(ph+.3)*.245;
    tube(T,[xf+sx*.001,vy,vz],[xf+sx*.012,vy,vz],.013,5,'galv',-.3);
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
      bright:{ramp:cool(s.trim==='black'?IRON:metal)},chrome:{ramp:cool(metal)},
      iron:{ramp:cool(IRON)},galv:{ramp:cool(GALV)},rubber:{ramp:cool(RUBBER)},shade:{ramp:cool(SHADE)},
      glass:{ramp:s.night?GLASSN:GLASSD},cloth:{ramp:cool(CLOTH)},bedliner:{ramp:cool(LINER)},
      head:{ramp:GLASSD.map(c=>mix(c,'#dfe6e2',0.35))},
      glow:{ramp:s.night?['#9fbfcf','#cfe3ea','#eef7f6','#ffffff']:GLOW},
      led:{ramp:s.night?['#b9d9ec','#e0f1f4','#f4fbfa','#ffffff']:['#859ba4','#aec1c6','#d7e4e3','#edf1eb']},
      amber:{ramp:s.night?['#a26312','#d89932','#efbd5c','#ffe7a3']:LENSA},
      red:{ramp:s.brake?['#aa2116','#d03920','#f16436','#ff9b65']:s.night?LENSR.slice(2):LENSR}};
  }

  function anchorPoints(opts={}){
    const s=resolve(opts),hl=sweep([.805,G.noseY+.012,1.285]);
    const points={hitch:[0,-3.48,.62],bed:[0,-2.04,G.bedFloorZ],driverSeat:[-.53,.74,1.18],passengerSeat:[.53,.74,1.18],
      rearSeatL:[-.54,-.44,1.18],rearSeatC:[0,-.44,1.18],rearSeatR:[.54,-.44,1.18],
      fuel:[-1.10,-1.17,1.23],exhaust:[.74,-3.31,.34],headlightL:[-hl[0],hl[1],hl[2]],headlightR:hl,
      tailLightL:[-.955,-3.25,1.30],tailLightR:[.955,-3.25,1.30]};
    points.hoodLatch=hingeX(sweep([0,G.noseY-.08,G.hoodZn]),G.cowlY,G.hoodZc,s.hood*48*DEG);
    points.gate=hingeX([0,G.tailY-.01,1.37],G.tailY+.06,.97,s.gate*92*DEG);
    for(const [id,sx,y] of [['FL',-1,G.doorF],['FR',1,G.doorF],['RL',-1,G.doorR],['RR',1,G.doorR]])
      points['door'+id]=hingeZ([sx*1.05,y[0]+.18,1.18],sx*1.0,y[1],sx*s['d'+id]*68*DEG);
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
  root.ModernTruck350={W,H,PX,DIRS:8,pivot:{x:cx,y:groundY},defaultElev:40,order:['N','NE','E','SE','S','SW','W','NW'],
    BODY,PRESETS,CUES,G,travel:{F:TF,R:TR},version:'1.0.0-art-candidate',list:()=>[RIG.key],dims,resolve,render,frames,project,
    anchors,anchorPoints,bodyOffset:(y,o={})=>bodyOffset(y,resolve(o)),mesh,obj};
})(typeof globalThis!=='undefined'?globalThis:window);

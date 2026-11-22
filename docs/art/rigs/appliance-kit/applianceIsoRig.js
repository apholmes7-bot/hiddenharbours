/* Parametric ISO APPLIANCE rig: the bathroom and kitchen fixtures a player USES (bath, water closet,
   basin, shower, wharf shower, cook stove, prep counter, microwave). Same turntable, camera and
   shading recipe as interiorPropRig.js (45deg steps, elev 40deg, flat-facet shading from the fixed
   upper-left key, z-buffered, ordered dither, depth-edge darkening, 1px keyline, no AA, 32 px = 1 m),
   so an appliance stands on the room floor beside the furniture with nothing to reconcile.

   WHY A SEPARATE RIG. interiorPropRig.js is pinned by digest in the interior contract and every yard
   recipe, and rowhouseUnitIsoRig.js draws its pieces in situ. These fixtures need things a plain prop
   never did: REAL HOLLOWS (a basin is an outer wall, a rim ring, an inner wall and a floor, never a box
   with a slab painted on top), STATE (water level, suds, a flush, a running shower) and a NEAR/FAR SPLIT
   so a figure can sit inside a tub with the near rim drawn over him. Each is an option to render().

   PROP ORIGIN: the floor centre of the footprint. FRONT is -Y; the BACK (+Y) is the side that stands
   against a wall (taps, cisterns, stovepipes, tiled walls are all at +Y).

   OPTIONS (every one optional):
     fill:0..1    water level in a bath or basin (0 = dry)
     pour:bool    water running from the tap (the fill frames)
     froth:0..3   suds frame on the water (0 = none); frames 1..3 move the bubbles, seeded, deterministic
     flush:0..3   water closet flush frame (0 = still)
     run:0..3     shower running frame (0 = off)
     split:'near' draw ONLY the geometry nearer the camera than the piece's seat plane (see SEAT), so a
                  host can stack: full sprite, the figure, then this near layer
     weather:0..1 scuffs     night:bool warm-lamp tint

   SEAT. A piece a figure occupies carries seat:[x,y,z] (prop metres): the point his feet/hips stand on,
   and the plane through it, square to the view, is where 'near' splits. heading is the model-frame
   direction (deg, 0 = +X, CCW) the occupant faces. The runtime reads the same numbers from its own table
   (ApplianceSheets); an EditMode test holds the two equal.

   Exposes globalThis.ApplianceIso = { W,H,PX,pivot,defaultElev,order,PROPS,list(),footprint(name),
     height(name,opts),seat(name),render(name,dir,opts),project(dir,p,elev),freezerWell(name,heap) }.
   lid:0..1 (chest freezer lid, closed to open) and heap:0..2 (its frozen-catch rung) are freezer options;
   split:'near' on a piece with no seat splits on its splitAt point instead. */
(function (root) {
  const PX = 32, S = 32;
  const W = 460, H = 460, cx = 230, groundY = 330;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;

  // ---- palettes (the harbour master ramps, same values as interiorPropRig.js) ----
  const BODY = {
    white: ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    cream: ['#8a6f3c','#a6884b','#c2a35f','#d8bd7c','#e9d59d','#f5e7c1'],
    red:   ['#4a130f','#671b14','#88271c','#a33124','#bd4230','#d25a42'],
    sage:  ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'],
    blue:  ['#33454a','#43585d','#556d72','#6a848a','#849ea3','#a3b9bd'],
    steel: ['#565b5f','#6d7276','#868b8f','#a0a5a8','#bbbfc1','#d6d9da'],
  };
  const WOODS = {
    pine:   ['#6b4e2b','#7e5d34','#8f6d40','#a5824f','#bb9a63','#d0b47e'],
    oak:    ['#553d23','#6b4e30','#7f603b','#957749','#ab8d5f','#c1a578'],
    walnut: ['#2f2018','#412c20','#54402e','#68533e','#7d6851','#947f66'],
  };
  const WORKTOPS = {
    slate:     ['#3a3d43','#494d54','#5b6067','#70757c','#868b91','#9ca1a6'],
    marble:    ['#8f918c','#a9aaa3','#c3c4bb','#d8d9d0','#e9eae1','#f6f7ef'],
    butcher:   ['#6b4e2b','#7e5d34','#8f6d40','#a5824f','#bb9a63','#d0b47e'],
    greentile: ['#26362f','#33473c','#456155','#5c8071','#79a08d','#9abfa9'],
    bluetile:  ['#25353a','#33484f','#456068','#5c8088','#7aa0a8','#9abfc6'],
  };
  const TRIM    = ['#9aa09a','#b4b8b0','#ccd0c7','#e0e2da','#eef0e8','#f8f9f2'];
  const IRON    = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970'];
  const TIN     = ['#565b5f','#6d7276','#868b8f','#a0a5a8','#bbbfc1','#d6d9da'];
  const BRASS   = ['#59410f','#795819','#977326','#b28e3b','#c8a757','#dcc17d'];
  const CERAMIC = ['#868b89','#a3a8a4','#c0c4be','#d8dcd4','#ebeee6','#f7f9f2'];
  const GLASSD  = ['#7d9ea6','#a1c2c6','#cfe6e8'], GLASSN = ['#141d2b','#233247','#3d5570'];
  const FIRE    = ['#7a2a10','#b5541a','#e59433','#f6cf6a'];
  // new for the fixtures: still water, suds, a falling stream, the dark of a drain, a bar of soap
  const WATER   = ['#24485c','#2f5d74','#3d768c','#5491a3','#79afbd','#a9d0d8'];
  const SUDS    = ['#a9b6bb','#c3cfd2','#d8e1e3','#e9eff0','#f5f9f9','#ffffff'];
  const STREAM  = ['#6f9fb0','#8fbac7','#b3d5dd','#d3e9ed','#e9f5f7','#ffffff'];
  const DARK    = ['#0c0e12','#15181d','#1f2329','#2a2f36','#363c44','#434a53'];
  const SOAP    = ['#8f6f6a','#a8857e','#c29e95','#d6b5ab','#e6cbc2','#f2e0d9'];
  // the freezer: rime on the well and the liner, and the silver-blue of fish frozen hard
  const FROST     = ['#7d97a3','#9bb3bd','#bacdd4','#d4e2e6','#e7f0f2','#f5fafb'];
  const FROSTFISH = ['#46545d','#5d6e77','#788a93','#96a8af','#b5c4ca','#d3dee1'];
  const KEY     = '#1a1c22';

  // ---- shading (identical recipe to interiorPropRig) ----
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

  // ---- face builders (outward-normal winding: counter-clockwise seen from outside) ----
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat, layer:0 }; }
  function wall(out, x0,y0,x1,y1, z0,z1, mat, tex, b){ const L=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]], mat, b||0, 0, [[0,z0],[L,z0],[L,z1],[0,z1]], tex)); }
  function slab(out, pts, z, mat, b, tex, db){ const uv=tex?pts.map(p=>[p[0],p[1]]):null;
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, db||0, uv, tex)); }
  function box(out, x0,x1,y0,y1,z0,z1, mat, b, tex, noTop){
    wall(out, x0,y0, x1,y0, z0,z1, mat, tex, b);
    wall(out, x1,y1, x0,y1, z0,z1, mat, tex, b);
    wall(out, x1,y0, x1,y1, z0,z1, mat, tex, b);
    wall(out, x0,y1, x0,y0, z0,z1, mat, tex, b);
    if(!noTop) slab(out, [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, (b||0)+0.28, tex);
  }
  function leg(out, x,y, r, z0,z1, mat, b){ box(out, x-r,x+r, y-r,y+r, z0,z1, mat, b||0); }
  function ring(cxp, cyp, rx, ry, n, a0){ const p=[]; for(let i=0;i<n;i++){ const a=(a0||0)+(i/n)*Math.PI*2; p.push([cxp+Math.cos(a)*rx, cyp+Math.sin(a)*ry]); } return p; }
  function prism(out, cxp,cyp, r, z0,z1, n, mat, b, texTop){
    const p=ring(cxp,cyp,r,r,n);
    for(let i=0;i<n;i++){ const A=p[i], Bp=p[(i+1)%n]; wall(out, A[0],A[1], Bp[0],Bp[1], z0,z1, mat, null, b); }
    slab(out, p, z1, mat, (b||0)+0.22, texTop||null);
  }
  // a rounded rectangle, counter-clockwise: half-extents a (x) and b (y), corner radius r, k steps a corner
  function rrect(cxp, cyp, a, b, r, k){ r=Math.min(r, a, b); const p=[], q=k||4;
    const cs=[[a-r,b-r,0],[-(a-r),b-r,90],[-(a-r),-(b-r),180],[a-r,-(b-r),270]];
    for(const [ox,oy,deg] of cs) for(let i=0;i<=q;i++){ const t=(deg+90*i/q)*DEG; p.push([cxp+ox+Math.cos(t)*r, cyp+oy+Math.sin(t)*r]); }
    return p; }
  // walls between two same-count rings (bottom ring at zb[i], top ring at zt[i]); inward=true faces the centre
  function loft(out, bot, top, zb, zt, mat, b, inward, tex){ const n=bot.length;
    const Z=(z,i)=> typeof z==='function' ? z(i) : z;
    for(let i=0;i<n;i++){ const j=(i+1)%n; const B0=bot[i], B1=bot[j], T0=top[i], T1=top[j];
      const v = inward ? [[B1[0],B1[1],Z(zb,j)],[B0[0],B0[1],Z(zb,i)],[T0[0],T0[1],Z(zt,i)],[T1[0],T1[1],Z(zt,j)]]
                       : [[B0[0],B0[1],Z(zb,i)],[B1[0],B1[1],Z(zb,j)],[T1[0],T1[1],Z(zt,j)],[T0[0],T0[1],Z(zt,i)]];
      out.push(F(v, mat, b||0, 0, tex?[[0,v[0][2]],[0.1,v[1][2]],[0.1,v[2][2]],[0,v[3][2]]]:null, tex||null)); } }
  // the flat ring between an outer and an inner ring at z (per-vertex z allowed)
  function band(out, outer, inner, z, mat, b){ const n=outer.length, Z=(i)=> typeof z==='function'?z(i):z;
    for(let i=0;i<n;i++){ const j=(i+1)%n;
      out.push(F([[outer[i][0],outer[i][1],Z(i)],[outer[j][0],outer[j][1],Z(j)],[inner[j][0],inner[j][1],Z(j)],[inner[i][0],inner[i][1],Z(i)]], mat, b||0, 0)); } }
  function lerpRing(a, b, t){ return a.map((p,i)=>[p[0]+(b[i][0]-p[0])*t, p[1]+(b[i][1]-p[1])*t]); }
  // decals on a face (recessed panels, hardware, glass)
  function decalY(out, yv, ny, xs,xe, z0,z1, mat, b, tex, flat, db){ const e=0.02*ny, uw=xe-xs, uh=z1-z0;
    const P = ny>0 ? [[xs,yv+e,z0],[xe,yv+e,z0],[xe,yv+e,z1],[xs,yv+e,z1]] : [[xe,yv+e,z0],[xs,yv+e,z0],[xs,yv+e,z1],[xe,yv+e,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat)); }
  function decalX(out, xv, nx, ys,ye, z0,z1, mat, b, tex, flat, db){ const e=0.02*nx, uw=ye-ys, uh=z1-z0;
    const P = nx>0 ? [[xv+e,ye,z0],[xv+e,ys,z0],[xv+e,ys,z1],[xv+e,ye,z1]] : [[xv+e,ys,z0],[xv+e,ye,z0],[xv+e,ye,z1],[xv+e,ys,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat)); }
  function decalTop(out, x0,x1,y0,y1, z, mat, b, db){
    out.push(F([[x0,y0,z],[x1,y0,z],[x1,y1,z],[x0,y1,z]], mat, b||0, db!=null?db:0.05, null, null, true)); }
  function panelsY(out, y, xa,xb, z0,z1, rows,cols, knob){ const m=0.045;
    for(let r=0;r<rows;r++) for(let c=0;c<cols;c++){
      const pa=xa+(xb-xa)*(c/cols)+m, pb=xa+(xb-xa)*((c+1)/cols)-m;
      const q0=z0+(z1-z0)*(r/rows)+m, q1=z0+(z1-z0)*((r+1)/rows)-m;
      decalY(out,y,-1, pa,pb, q0,q1, 'body', -1.6, null, true, 0.05);
      decalY(out,y,-1, pa,pb, q1-0.02,q1, 'body', 0.8, null, true, 0.06);
      decalY(out,y,-1, pa,pa+0.02, q0,q1, 'body', -0.8, null, true, 0.06);
      if(knob){ const kx=(pa+pb)/2, kz = rows>1 ? q1-0.07 : (q0+q1)/2;
        decalY(out,y,-1, kx-0.03,kx+0.03, kz-0.03,kz+0.03, 'brass', 0.9, null, true, 0.08); } }
  }
  function plankTex(pw){ pw=pw||0.26; return (u,v)=>{ const f=((u%pw)+pw)%pw; if(f<0.03) return -2;
    const p=Math.floor(u/pw); return hash2(p|0,0)<0.5?0:(hash2(p|0,3)<0.4?-1:0); }; }
  function tileTex(c){ c=c||0.15; return (u,v)=>{ const fu=((u%c)+c)%c, fv=((v%c)+c)%c; return (fu<0.016||fv<0.016)?-2:0; }; }
  function weaveTex(){ const c=0.09; return (u,v)=>{ const fu=((u%c)+c)%c, fv=((v%c)+c)%c; return (fu<c*0.5)^(fv<c*0.5)?0:-1; }; }
  function rippleTex(seed){ return (u,v)=>{ const w=Math.sin(u*31+seed*1.7)+Math.sin(v*23-seed*2.3+u*7);
    return w>1.25?1.2:(w<-1.4?-0.8:0); }; }

  // ---- water, suds and streams (shared by every wet piece) ----
  // still water filling a hollow: floorRing at zFloor rising toward lipRing at zLip, t = 0..1
  function water(out, floorRing, lipRing, zFloor, zLip, t, seed){ if(!(t>0)) return null;
    const k=0.06+0.94*t, r=lerpRing(floorRing, lipRing, k), z=zFloor+(zLip-zFloor)*k;
    slab(out, r, z, 'water', 0.15, rippleTex(seed|0), 0.012); return { ring:r, z }; }
  // a lid of suds on that water: a ring of fat blobs at the edge, scattered blobs across, a few loose bubbles
  function suds(out, surf, frame, density){ if(!surf||!(frame>0)) return;
    const rnd=mulberry32(9001+frame*131), r=surf.ring, n=r.length;
    let mx=0,my=0; for(const p of r){ mx+=p[0]; my+=p[1]; } mx/=n; my/=n;
    const blob=(x,y,rad,h)=>{ prism(out, x,y, rad, surf.z, surf.z+h, 6, 'suds', 0.35); };
    for(let i=0;i<n;i++){ if(rnd()<0.25) continue; const p=r[i], q=r[(i+1)%n], t=rnd();
      const x=p[0]+(q[0]-p[0])*t, y=p[1]+(q[1]-p[1])*t; const ix=x+(mx-x)*0.12, iy=y+(my-y)*0.12;
      blob(ix, iy, 0.035+rnd()*0.03, 0.03+rnd()*0.03); }
    const m=Math.round((density||14)*(0.85+0.3*rnd()));
    for(let i=0;i<m;i++){ const a=rnd()*Math.PI*2, s=Math.sqrt(rnd())*0.8, p=r[Math.floor(rnd()*n)];
      const x=mx+(p[0]-mx)*s, y=my+(p[1]-my)*s; blob(x, y, 0.025+rnd()*0.035, 0.02+rnd()*0.04); }
    for(let i=0;i<3;i++){ const p=r[Math.floor(rnd()*n)], s=0.6*rnd(); const x=mx+(p[0]-mx)*s, y=my+(p[1]-my)*s;
      prism(out, x,y, 0.022, surf.z+0.12+0.08*((frame+i)%3), surf.z+0.15+0.08*((frame+i)%3), 6, 'suds', 0.6); }
  }
  // a falling stream from (x,y,zTop) to zBot, 1-2 px wide, with a wobble per frame
  function stream(out, x,y, zTop, zBot, frame, wide){ const w=(wide||0.022)/2, wob=((frame|0)%3-1)*0.006;
    box(out, x-w+wob, x+w+wob, y-w, y+w, zBot, zTop, 'stream', 0.3); }
  function splash(out, x,y, z, frame){ const rnd=mulberry32(4242+frame*17);
    for(let i=0;i<5;i++){ const a=rnd()*Math.PI*2, d=0.04+rnd()*0.07;
      box(out, x+Math.cos(a)*d-0.012, x+Math.cos(a)*d+0.012, y+Math.sin(a)*d-0.012, y+Math.sin(a)*d+0.012, z, z+0.015+rnd()*0.03, 'stream', 0.5); } }

  // ================= THE FIXTURES =================

  // CLAWFOOT BATH. Long axis on X, back (+Y) to the wall. A slipper end rises at -X so the silhouette says
  // "bath" from every side; the taps and gooseneck stand at +X; brass claw feet; a mat on the floor in front.
  function pBath(out, s){
    const A=0.80, Bh=0.38, z0=0.17, rimLo=0.64, rise=0.15, inset=0.06;
    const lift=(x)=>{ const t=Math.max(0,Math.min(1,(-x-0.28)/0.5)); return t*t*(3-2*t)*rise; };
    const top=rrect(0,0,A,Bh,0.30,5), bot=rrect(0,0,A-0.12,Bh-0.08,0.24,5), lip=rrect(0,0,A+0.025,Bh+0.025,0.32,5);
    const inTop=rrect(0,0,A-inset,Bh-inset,0.25,5), inFloor=rrect(0,0,A-0.26,Bh-0.17,0.12,5);
    const zt=(i)=>rimLo+lift(top[i][0]), zl=(i)=>rimLo+lift(lip[i][0]), zi=(i)=>rimLo+lift(inTop[i][0]);
    const zFloor=z0+0.07;
    // claw feet: a ball, a shank, a flared knuckle under the belly
    for(const fx of [-0.56,0.56]) for(const fy of [-0.22,0.22]){
      prism(out, fx+Math.sign(fx)*0.02, fy+Math.sign(fy)*0.03, 0.045, 0, 0.045, 6, 'brass', 0.2);
      box(out, fx-0.025,fx+0.025, fy-0.025,fy+0.025, 0.035, z0+0.03, 'brass', 0.1);
      prism(out, fx, fy, 0.05, z0-0.02, z0+0.03, 6, 'brass', 0.3); }
    loft(out, bot, top, z0, (i)=>zt(i)-0.04, 'ceramic', 0);                      // the flared belly
    slab(out, bot.slice().reverse(), z0, 'ceramic', -0.6);                       // underside
    loft(out, top, lip, (i)=>zt(i)-0.04, (i)=>zl(i)-0.015, 'ceramic', 0.2);       // rolled rim (outer roll)
    band(out, lip, inTop, (i)=>zl(i), 'ceramic', 0.45);                           // rim top (lip ring to inner edge)
    loft(out, inFloor, inTop, zFloor, (i)=>zi(i), 'ceramic', -1.2, true);         // the hollow's inner wall
    slab(out, inFloor, zFloor, 'ceramic', -0.9);                                  // the hollow's floor
    prism(out, A-0.33, 0, 0.03, zFloor, zFloor+0.006, 8, 'dark', 0);              // plug hole
    box(out, A-0.34,A-0.32, -0.01,0.01, zFloor+0.006, zFloor+0.012, 'brass', 0.4); // plug chain
    // water, suds, the tap running
    const surf=water(out, inFloor, inTop, zFloor, rimLo-0.07, s.fill, s.froth||s.fill*7);
    suds(out, surf, s.froth, 18);
    // taps at +X: two cross-handle pillars and a gooseneck spout over the water
    const tx=A-0.11, zr=rimLo;
    for(const ty of [-0.11,0.11]){ box(out, tx-0.022,tx+0.022, ty-0.022,ty+0.022, zr, zr+0.12, 'brass', 0.2);
      box(out, tx-0.055,tx+0.055, ty-0.012,ty+0.012, zr+0.12, zr+0.145, 'brass', 0.5);
      box(out, tx-0.012,tx+0.012, ty-0.055,ty+0.055, zr+0.12, zr+0.145, 'brass', 0.5);
      prism(out, tx, ty, 0.016, zr+0.145, zr+0.165, 6, 'ceramic', 0.6); }
    box(out, tx-0.02,tx+0.02, -0.02,0.02, zr, zr+0.26, 'brass', 0.15);              // spout riser
    box(out, tx-0.16,tx+0.02, -0.02,0.02, zr+0.22, zr+0.26, 'brass', 0.4);          // gooseneck
    box(out, tx-0.18,tx-0.14, -0.02,0.02, zr+0.15, zr+0.24, 'brass', 0.25);         // spout mouth
    if(s.pour) stream(out, tx-0.16, 0, zr+0.15, surf?surf.z:zFloor, s.fill*9|0, 0.03);
    // soap dish on the back rim, a bar of soap; a folded towel over the slipper end; a mat in front
    box(out, -0.05,0.09, Bh-0.04,Bh+0.03, zr+0.005, zr+0.03, 'ceramic', 0.4);
    box(out, -0.03,0.07, Bh-0.025,Bh+0.015, zr+0.03, zr+0.055, 'soap', 0.4);
    const tz=rimLo+rise;
    box(out, -A+0.02,-A+0.26, -Bh-0.035,-Bh+0.01, tz-0.32, tz+0.01, 'fabric', 0.05, plankTex(0.07));
    box(out, -A+0.02,-A+0.26, -Bh+0.01,-Bh+0.12, tz-0.005, tz+0.02, 'fabric', 0.35);
    slab(out, [[-0.42,-0.86],[0.42,-0.86],[0.42,-0.48],[-0.42,-0.48]], 0.012, 'fabric2', 0.1, weaveTex());
  }

  // WATER CLOSET. Period: varnished wood seat and lid, a low wooden cistern with a brass lever and
  // flush pipe. The bowl is a real hollow with dark water in it, so it reads as a toilet from the back too.
  function pToilet(out, s){
    const by=-0.11, zb=0.40, cyl=0.10;
    prism(out, 0, by+0.03, 0.12, 0, 0.05, 12, 'ceramic', 0.1);                      // foot
    loft(out, ring(0,by+0.03,0.11,0.12,14), ring(0,by,0.2,0.24,14), 0.05, zb-0.03, 'ceramic', 0); // flared pedestal-bowl
    const outer=ring(0,by,0.205,0.245,16), inner=ring(0,by-0.01,0.15,0.19,16), deep=ring(0,by+0.02,0.07,0.09,16);
    loft(out, ring(0,by,0.2,0.24,16), outer, zb-0.03, zb, 'ceramic', 0.1);
    band(out, outer, inner, zb, 'ceramic', 0.4);
    loft(out, deep, inner, zb-0.2, zb, 'ceramic', -1.1, true);                      // the bowl's hollow
    const fl=s.flush|0, lvl=[0,0.03,0.07,0.11][fl];
    slab(out, lerpRing(deep, inner, 0.45-lvl*2.5), zb-0.11-lvl, 'water', -0.1, fl?((u,v)=>{
      const a=Math.atan2(v-by,u)+fl*1.9, r=Math.hypot(u,v-by); return Math.sin(a*3+r*40)>0.3?1.3:-0.3; }):rippleTex(3), 0.01);
    // seat ring (wood) and the lid raised against the cistern
    const sOut=ring(0,by-0.005,0.2,0.235,16), sIn=ring(0,by-0.015,0.125,0.16,16);
    loft(out, sOut, sOut, zb+0.005, zb+0.03, 'wood', 0.1);
    band(out, sOut, sIn, zb+0.03, 'wood', 0.4);
    loft(out, sIn, sIn, zb+0.005, zb+0.03, 'wood', -0.6, true);
    out.push(F([[-0.19,0.13,zb+0.04],[0.19,0.13,zb+0.04],[0.19,0.17,zb+0.43],[-0.19,0.17,zb+0.43]], 'wood', 0.25, 0.03, null, null));
    out.push(F([[0.19,0.15,zb+0.04],[-0.19,0.15,zb+0.04],[-0.19,0.19,zb+0.43],[0.19,0.19,zb+0.43]], 'wood', -0.3, 0.03, null, null));
    // the low cistern behind, with a lid, a brass lever (down while flushing) and a flush pipe
    box(out, -0.2,0.2, 0.14,0.33, 0.48,0.82, 'wood', 0.05, plankTex(0.1));
    box(out, -0.22,0.22, 0.12,0.35, 0.82,0.86, 'wood', 0.35);
    box(out, -0.03,0.03, 0.17,0.23, zb-0.05, 0.48, 'brass', 0.1);                   // flush pipe
    const lv=fl?-0.07:0;
    box(out, -0.2-0.012,-0.2+0.005, 0.15,0.17, 0.74, 0.77, 'brass', 0.4);           // lever boss
    box(out, -0.24,-0.212, 0.04,0.17, 0.745+lv*0.3, 0.765+lv*0.3, 'brass', 0.6);    // lever arm
    prism(out, -0.226, 0.04, 0.018, 0.735+lv*0.3, 0.775+lv*0.3, 6, 'ceramic', 0.5); // lever knob
    // a toilet roll on the cistern lid
    prism(out, 0.12, 0.24, 0.05, 0.86, 0.96, 10, 'trim', 0.4);
  }

  // WASHSTAND BASIN. A walnut stand with a marble top, a real china bowl (hollow, fills), a brass tap
  // on a back riser, a jug beside it and a towel on the side bar.
  function pBasin(out, s){
    const w=0.58, d=0.44, ht=0.80;
    for(const x of [-w/2+0.05,w/2-0.05]) for(const y of [-d/2+0.05,d/2-0.05]) leg(out,x,y,0.03,0,ht-0.06,'wood',0);
    box(out,-w/2+0.03,w/2-0.03,-d/2+0.03,d/2-0.03, 0.16,0.2,'wood',-0.2, plankTex(0.16));   // lower shelf
    box(out,-w/2,w/2,-d/2,d/2, ht-0.07,ht, 'stone', 0.15);                                  // marble top
    box(out,-w/2,w/2, d/2-0.04,d/2, ht,ht+0.16,'stone',0.05);                               // splashback
    box(out,-w/2-0.01,w/2+0.01, d/2-0.05,d/2+0.01, ht+0.16,ht+0.18,'stone',0.35);
    // the bowl: a china ring standing proud of the top, hollow inside
    const bx=-0.04, byy=-0.02, oR=ring(bx,byy,0.19,0.16,16), oB=ring(bx,byy,0.15,0.12,16), iR=ring(bx,byy,0.16,0.13,16), iF=ring(bx,byy,0.07,0.055,16);
    const zr=ht+0.09;
    loft(out, oB, oR, ht, zr, 'ceramic', 0.1);
    band(out, ring(bx,byy,0.2,0.17,16), iR, zr+0.008, 'ceramic', 0.5);
    loft(out, oR, ring(bx,byy,0.2,0.17,16), zr, zr+0.008, 'ceramic', 0.3);
    loft(out, iF, iR, ht+0.01, zr+0.008, 'ceramic', -1.1, true);
    slab(out, iF, ht+0.01, 'ceramic', -0.8);
    prism(out, bx, byy, 0.018, ht+0.01, ht+0.014, 8, 'dark', 0);
    const surf=water(out, iF, iR, ht+0.01, zr-0.015, s.fill, 5+(s.froth|0));
    suds(out, surf, s.froth, 7);
    // tap: a brass riser off the splashback, a spout over the bowl, a cross handle
    box(out, bx-0.018,bx+0.018, d/2-0.09,d/2-0.04, ht, ht+0.2, 'brass', 0.15);
    box(out, bx-0.018,bx+0.018, byy+0.07,d/2-0.06, ht+0.17, ht+0.2, 'brass', 0.4);
    box(out, bx-0.016,bx+0.016, byy+0.06,byy+0.09, ht+0.12, ht+0.18, 'brass', 0.25);
    box(out, bx-0.05,bx+0.05, d/2-0.075,d/2-0.055, ht+0.2, ht+0.22, 'brass', 0.6);
    if(s.pour) stream(out, bx, byy+0.075, ht+0.12, surf?surf.z:ht+0.01, (s.fill*9)|0, 0.02);
    // the jug, a soap dish, a towel on the side bar
    prism(out, w/2-0.1, 0.06, 0.055, ht, ht+0.17, 10, 'ceramic', 0.1);
    prism(out, w/2-0.1, 0.06, 0.065, ht+0.14, ht+0.17, 10, 'ceramic', 0.35);
    box(out, w/2-0.17,w/2-0.15, 0.04,0.08, ht+0.06, ht+0.14, 'ceramic', 0.2);              // handle
    box(out, -w/2+0.03,-w/2+0.13, d/2-0.13,d/2-0.07, ht, ht+0.02, 'ceramic', 0.4);
    box(out, -w/2+0.045,-w/2+0.115, d/2-0.12,d/2-0.08, ht+0.02, ht+0.04, 'soap', 0.4);
    box(out, w/2, w/2+0.03, -d/2+0.07, d/2-0.07, ht-0.24, ht-0.21, 'brass', 0.3);          // side bar
    box(out, w/2+0.005, w/2+0.035, -d/2+0.1, d/2-0.1, ht-0.5, ht-0.2, 'fabric', 0.1, plankTex(0.06));
  }

  // WALK-IN SHOWER. A tiled tray with a curb (a real hollow), tiled walls at the back and one side, the
  // riser and a rain head on an arm above the walls, a curtain on a rail across the open sides drawn
  // back to the corner so the stall reads from every wall. Running: streams, splash, wet tray.
  function pShower(out, s){
    const w=0.85, d=0.85, curb=0.09, gh=1.92, t=0.05, STUB=0.62;
    const o=[[-w/2,-d/2],[w/2,-d/2],[w/2,d/2],[-w/2,d/2]], i=[[-w/2+t,-d/2+t],[w/2-t,-d/2+t],[w/2-t,d/2-t],[-w/2+t,d/2-t]];
    loft(out, o, o, 0, curb, 'trim', 0.05);
    band(out, o, i, curb, 'trim', 0.4);
    loft(out, i, i, 0.02, curb, 'trim', -1.0, true);
    slab(out, i, 0.02, 'worktop', -0.5, tileTex(0.1));
    prism(out, 0, 0, 0.035, 0.02, 0.026, 8, 'dark', 0);
    // tiled walls: back (+Y) and right (+X). A wall that stands between the camera and the stall is cut to a
    // stub, the rooms' own dollhouse rule, so a stall on a near wall still shows its tray, head and curtain
    const cam=s.cam||{ct:1,stt:0}, backH=cam.ct< -0.01?STUB:gh, sideH=cam.stt< -0.01?STUB:gh;
    box(out, -w/2, w/2, d/2-0.04, d/2, curb, backH, 'worktop', 0.12, tileTex(0.15));
    box(out, w/2-0.04, w/2, -d/2, d/2-0.04, curb, sideH, 'worktop', 0.04, tileTex(0.15));
    box(out, -w/2-0.01, w/2+0.01, d/2-0.05, d/2+0.01, backH, backH+0.03, 'trim', 0.4);
    box(out, w/2-0.05, w/2+0.01, -d/2-0.01, d/2-0.05, sideH, sideH+0.03, 'trim', 0.4);
    // plumbing: valve and riser on the back wall, the arm reaching out to a round rain head
    prism(out, -0.18, d/2-0.06, 0.05, 1.0, 1.04, 10, 'tin', 0.4);
    box(out, -0.185,-0.175, d/2-0.11, d/2-0.05, 1.0, 1.03, 'tin', 0.6);
    box(out, -0.02,0.02, d/2-0.08, d/2-0.04, 1.0, gh+0.14, 'tin', 0.2);
    box(out, -0.02,0.02, -0.02, d/2-0.04, gh+0.10, gh+0.14, 'tin', 0.35);
    prism(out, 0, -0.02, 0.11, gh+0.06, gh+0.1, 12, 'tin', 0.3);
    prism(out, 0, -0.02, 0.085, gh+0.055, gh+0.06, 12, 'dark', 0.4);
    // curtain rail round the open sides, the curtain bunched at the -X back corner
    box(out, -w/2, w/2-0.04, -d/2-0.01, -d/2+0.01, gh-0.02, gh, 'tin', 0.5);
    box(out, -w/2-0.01, -w/2+0.01, -d/2, d/2-0.04, gh-0.02, gh, 'tin', 0.5);
    for(let k=0;k<4;k++){ const y0=d/2-0.08-k*0.075; box(out, -w/2-0.02+(k&1)*0.025, -w/2+0.03+(k&1)*0.025, y0-0.075, y0, curb+0.25, gh-0.03, 'fabric', 0.12-(k&1)*0.5); }
    box(out, -w/2-0.02, -w/2+0.05, d/2-0.1, d/2-0.04, curb+0.2, gh-0.03, 'fabric', 0.2);
    // a bath mat outside the front, a soap shelf in the corner
    slab(out, [[-0.3,-d/2-0.34],[0.3,-d/2-0.34],[0.3,-d/2-0.04],[-0.3,-d/2-0.04]], 0.012, 'fabric2', 0.1, weaveTex());
    box(out, w/2-0.16, w/2-0.04, d/2-0.16, d/2-0.04, 1.1, 1.12, 'tin', 0.5);
    box(out, w/2-0.14, w/2-0.07, d/2-0.13, d/2-0.07, 1.12, 1.15, 'soap', 0.4);
    const run=s.run|0; if(run>0){ const rnd=mulberry32(77+run*13);
      for(let k=0;k<9;k++){ const a=k/9*Math.PI*2+run*0.4, r=0.03+0.06*rnd(); const x=Math.cos(a)*r, y=-0.02+Math.sin(a)*r;
        if(((k+run)%3)===0) continue; stream(out, x, y, gh+0.05, 0.03+0.25*rnd()*((k+run)&1), run+k, 0.014); }
      splash(out, 0, -0.02, 0.03, run); slab(out, i, 0.024, 'water', 0.3, rippleTex(run*5), 0.01); }
  }

  // WHARF SHOWER. A post with a pipe, an elbow and a rose head, a pull chain, a slatted pallet to stand
  // on, and a pail at the foot. Open on every side, so it needs no split.
  function pWharfShower(out, s){
    const ph=2.15;
    for(let k=0;k<5;k++){ const x=-0.4+k*0.2; box(out, x-0.08, x+0.08, -0.42, 0.42, 0.04, 0.08, 'wood', 0.15-(k&1)*0.3); }
    for(const y of [-0.34,0.34]) box(out, -0.48, 0.48, y-0.05, y+0.05, 0, 0.04, 'wood', -0.4);
    box(out, -0.06, 0.06, 0.38, 0.5, 0, ph, 'wood', 0.05, plankTex(0.04));             // the post
    box(out, -0.08, 0.08, 0.36, 0.52, ph, ph+0.04, 'wood', 0.35);
    box(out, -0.018, 0.018, 0.34, 0.38, 0.3, ph-0.05, 'tin', 0.2);                    // pipe up the post
    box(out, -0.018, 0.018, 0.0, 0.38, ph-0.09, ph-0.05, 'tin', 0.35);                // arm out
    prism(out, 0, 0.0, 0.075, ph-0.16, ph-0.1, 10, 'tin', 0.3);                      // rose
    prism(out, 0, 0.0, 0.055, ph-0.165, ph-0.16, 10, 'dark', 0.4);
    const pull=(s.run|0)>0?0.06:0;
    box(out, 0.1, 0.11, 0.0, 0.01, 1.2-pull, ph-0.07, 'brass', -0.2);                 // pull chain
    box(out, 0.08, 0.13, -0.02, 0.03, 1.1-pull, 1.2-pull, 'wood', 0.4);               // its toggle
    box(out, 0.05, 0.11, 0.0, 0.01, ph-0.08, ph-0.07, 'brass', 0.3);
    prism(out, 0.32, 0.3, 0.11, 0, 0.22, 12, 'tin', 0.1);                            // pail
    prism(out, 0.32, 0.3, 0.12, 0.2, 0.22, 12, 'tin', 0.4);
    box(out, 0.21, 0.43, 0.295, 0.305, 0.22, 0.3, 'tin', 0.5);
    const run=s.run|0; if(run>0){ const rnd=mulberry32(91+run*7);
      for(let k=0;k<8;k++){ const a=k/8*Math.PI*2+run*0.5, r=0.025+0.05*rnd(); if(((k+run)%3)===0) continue;
        stream(out, Math.cos(a)*r, Math.sin(a)*r, ph-0.17, 0.08+0.2*rnd()*((k+run)&1), run+k, 0.014); }
      splash(out, 0, 0, 0.08, run); }
  }

  // COOK STOVE. Black iron on legs: oven door with a brass handle and a heat dial, the firebox door with
  // its glow, four hob plates, a warming shelf, a stovepipe with a damper, a kettle and a frying pan,
  // so it reads as a stove from the side and the back as well as the front.
  function pCookStove(out, s){
    const w=0.74, d=0.58, ht=0.82;
    for(const x of [-w/2+0.05,w/2-0.05]) for(const y of [-d/2+0.05,d/2-0.05]){
      box(out, x-0.035,x+0.035, y-0.035,y+0.035, 0, 0.14, 'iron', 0.1);
      prism(out, x, y, 0.045, 0, 0.03, 6, 'iron', 0.2); }
    box(out, -w/2, w/2, -d/2, d/2, 0.14, ht, 'iron', 0);
    box(out, -w/2-0.03, w/2+0.03, -d/2-0.03, d/2+0.02, ht, ht+0.045, 'iron', 0.45);   // cooktop slab
    box(out, -w/2+0.01, w/2-0.01, -d/2-0.012, -d/2, 0.16, 0.19, 'tin', 0.5);          // nickel trim rail
    // oven (right) and firebox (left) doors on the front, both with handles; the firebox glows
    decalY(out, -d/2, -1, -0.02, w/2-0.05, 0.22, ht-0.1, 'iron', -1.3, null, true, 0.05);
    decalY(out, -d/2, -1, 0.02, w/2-0.09, 0.26, ht-0.14, 'iron', 0.5, null, true, 0.06);
    decalY(out, -d/2, -1, 0.06, w/2-0.13, ht-0.2, ht-0.16, 'brass', 0.9, null, true, 0.08);
    decalY(out, -d/2, -1, 0.16, 0.22, 0.45, 0.51, 'tin', 1.0, null, true, 0.08);       // heat dial
    decalY(out, -d/2, -1, -w/2+0.05, -0.06, 0.4, ht-0.1, 'iron', -1.3, null, true, 0.05);
    for(let k=0;k<3;k++) decalY(out, -d/2, -1, -w/2+0.09, -0.1, 0.5+k*0.06, 0.52+k*0.06, 'fire', 0.0, null, true, 0.08);
    decalY(out, -d/2, -1, -w/2+0.05, -0.06, 0.22, 0.34, 'iron', -0.5, null, true, 0.06); // ash door
    decalX(out, -w/2, -1, -d/2+0.1, d/2-0.1, 0.42, 0.62, 'iron', -1.3, null, true, 0.05); // side firebox door
    for(let k=0;k<3;k++) decalX(out, -w/2, -1, -d/2+0.15, d/2-0.15, 0.47+k*0.05, 0.49+k*0.05, 'fire', 0.0, null, true, 0.08);
    decalX(out, w/2, 1, -d/2+0.08, d/2-0.08, 0.24, ht-0.12, 'iron', -1.0, null, true, 0.05);
    // four hob plates, raised, each with its lifting notch
    const z1=ht+0.045;
    for(const hx of [-0.18,0.18]) for(const hy of [-0.1,0.13]){
      prism(out, hx, hy, 0.11, z1, z1+0.012, 12, 'iron', -0.5);
      prism(out, hx, hy, 0.085, z1+0.012, z1+0.02, 12, 'iron', 0.4);
      box(out, hx-0.012, hx+0.012, hy-0.07, hy-0.02, z1+0.02, z1+0.024, 'iron', -1.0); }
    // warming shelf on brackets at the back, the stovepipe with its damper, rising past the shelf
    box(out, -w/2, w/2, d/2-0.05, d/2, z1, z1+0.42, 'iron', 0.0);
    box(out, -w/2-0.02, w/2+0.02, d/2-0.2, d/2, z1+0.38, z1+0.42, 'iron', 0.4);
    box(out, w/2-0.2, w/2-0.08, d/2-0.13, d/2-0.01, z1, z1+1.2, 'iron', 0.12);
    prism(out, w/2-0.14, d/2-0.07, 0.085, z1+0.6, z1+0.64, 10, 'iron', 0.3);
    box(out, w/2-0.08, w/2-0.0, d/2-0.08, d/2-0.06, z1+0.61, z1+0.63, 'brass', 0.5);   // damper key
    // kettle on the back-left plate, a frying pan on the front-right one
    prism(out, -0.18, 0.13, 0.08, z1+0.02, z1+0.13, 10, 'tin', 0.15);
    prism(out, -0.18, 0.13, 0.05, z1+0.13, z1+0.16, 10, 'tin', 0.4);
    box(out, -0.28, -0.18, 0.12, 0.14, z1+0.07, z1+0.1, 'tin', 0.3);                   // spout
    box(out, -0.2, -0.16, 0.06, 0.2, z1+0.19, z1+0.21, 'wood', 0.4);                    // handle
    box(out, -0.19, -0.17, 0.12, 0.14, z1+0.15, z1+0.19, 'tin', 0.2);
    prism(out, 0.18, -0.1, 0.1, z1+0.02, z1+0.05, 12, 'iron', 0.2);
    prism(out, 0.18, -0.1, 0.08, z1+0.045, z1+0.05, 12, 'iron', -1.0);
    box(out, 0.27, 0.45, -0.115, -0.085, z1+0.035, z1+0.05, 'wood', 0.4);              // pan handle
  }

  // PREP COUNTER. A painted base with a drawer bank and doors, a butcher-block top with a cutting
  // board, a knife and a loaf, a crock of utensils at the back and a towel on the front rail; a small
  // hollow sink with a gooseneck tap at the far end makes the run read as a kitchen from any wall.
  function pPrepCounter(out, s){
    const w=0.9, d=0.6, ht=0.9, tz=ht;
    box(out, -w/2, w/2, -d/2, d/2, 0.08, ht-0.05, 'body', 0);
    box(out, -w/2+0.03, w/2-0.03, -d/2+0.04, d/2, 0, 0.08, 'iron', -0.5, null, true);  // toe kick
    panelsY(out, -d/2, -w/2+0.04, 0.0, ht-0.3, ht-0.08, 2, 1, true);                   // drawers
    panelsY(out, -d/2, -w/2+0.04, 0.0, 0.1, ht-0.32, 1, 1, true);                      // door
    panelsY(out, -d/2, 0.02, w/2-0.04, 0.1, ht-0.08, 1, 1, true);                      // sink cupboard
    for(const sx of [-1,1]) decalX(out, sx*w/2, sx, -d/2+0.06, d/2-0.06, 0.12, ht-0.1, 'body', -1.4, null, true, 0.05); // side panels
    // the top, with a real sink hollow at +X
    const sx0=0.08, sx1=w/2-0.06, sy0=-d/2+0.1, sy1=d/2-0.14;
    const o=[[-w/2-0.02,-d/2-0.02],[w/2+0.02,-d/2-0.02],[w/2+0.02,d/2],[-w/2-0.02,d/2]];
    loft(out, o, o, ht-0.05, tz, 'worktop', 0.1);
    slab(out, [[-w/2-0.02,-d/2-0.02],[sx0,-d/2-0.02],[sx0,d/2],[-w/2-0.02,d/2]], tz, 'worktop', 0.3, plankTex(0.06));
    slab(out, [[sx0,-d/2-0.02],[w/2+0.02,-d/2-0.02],[w/2+0.02,sy0],[sx0,sy0]], tz, 'worktop', 0.3, plankTex(0.06));
    slab(out, [[sx1,sy0],[w/2+0.02,sy0],[w/2+0.02,d/2],[sx1,d/2]], tz, 'worktop', 0.3, plankTex(0.06));
    slab(out, [[sx0,sy1],[sx1,sy1],[sx1,d/2],[sx0,d/2]], tz, 'worktop', 0.3, plankTex(0.06));
    const hole=[[sx0,sy0],[sx1,sy0],[sx1,sy1],[sx0,sy1]], floor=[[sx0+0.02,sy0+0.02],[sx1-0.02,sy0+0.02],[sx1-0.02,sy1-0.02],[sx0+0.02,sy1-0.02]];
    loft(out, floor, hole, tz-0.17, tz, 'ceramic', -1.1, true);
    slab(out, floor, tz-0.17, 'ceramic', -0.8);
    prism(out, (sx0+sx1)/2, (sy0+sy1)/2, 0.022, tz-0.17, tz-0.166, 8, 'dark', 0);
    const gx=(sx0+sx1)/2;                                                              // gooseneck tap
    box(out, gx-0.018, gx+0.018, d/2-0.1, d/2-0.06, tz, tz+0.3, 'tin', 0.15);
    box(out, gx-0.018, gx+0.018, d/2-0.22, d/2-0.06, tz+0.27, tz+0.3, 'tin', 0.4);
    box(out, gx-0.016, gx+0.016, d/2-0.23, d/2-0.2, tz+0.2, tz+0.28, 'tin', 0.25);
    for(const tx of [gx-0.09, gx+0.09]) box(out, tx-0.02, tx+0.02, d/2-0.1, d/2-0.06, tz, tz+0.07, 'brass', 0.4);
    // cutting board, a knife and a loaf; a crock of spoons at the back; a towel on the front rail
    box(out, -0.36, -0.04, -0.2, 0.08, tz, tz+0.025, 'top', 0.45);
    box(out, -0.3, -0.14, -0.13, -0.11, tz+0.025, tz+0.032, 'tin', 0.9);
    box(out, -0.14, -0.08, -0.13, -0.11, tz+0.025, tz+0.035, 'iron', 0.2);
    box(out, -0.32, -0.16, -0.02, 0.06, tz+0.025, tz+0.1, 'fabric2', 0.3);
    prism(out, -0.33, d/2-0.1, 0.06, tz, tz+0.14, 10, 'ceramic', 0.0);
    for(const [ux,uy,hh] of [[-0.35,d/2-0.11,0.12],[-0.31,d/2-0.09,0.1],[-0.34,d/2-0.07,0.14]]) box(out, ux-0.008, ux+0.008, uy-0.008, uy+0.008, tz+0.14, tz+0.14+hh, 'wood', 0.3);
    box(out, -w/2+0.02, 0.0, -d/2-0.05, -d/2-0.03, ht-0.14, ht-0.12, 'tin', 0.5);      // front rail
    box(out, -0.32, -0.14, -d/2-0.065, -d/2-0.045, ht-0.42, ht-0.12, 'fabric', 0.1, plankTex(0.05));
  }

  // MICROWAVE on its own short cabinet: the dark glazed door with a plate showing through it, the
  // keypad and dial at the side, a handle, vents and a cord at the back.
  function pMicrowave(out, s){
    const w=0.5, d=0.44, ht=0.72;
    box(out, -w/2, w/2, -d/2, d/2, 0.06, ht-0.03, 'body', 0);
    box(out, -w/2+0.03, w/2-0.03, -d/2+0.04, d/2, 0, 0.06, 'iron', -0.5, null, true);
    panelsY(out, -d/2, -w/2+0.03, w/2-0.03, 0.08, ht-0.06, 1, 1, true);
    box(out, -w/2-0.015, w/2+0.015, -d/2-0.015, d/2+0.01, ht-0.03, ht, 'worktop', 0.3);
    const mw=0.46, md=0.34, mh=0.27, y0=-d/2+0.04, z0=ht;
    box(out, -mw/2, mw/2, y0, y0+md, z0+0.015, z0+mh, 'steel', 0.05);
    for(const fx of [-mw/2+0.04, mw/2-0.04]) box(out, fx-0.02, fx+0.02, y0+0.03, y0+md-0.03, z0, z0+0.015, 'iron', -0.3);
    decalY(out, y0, -1, -mw/2+0.02, mw/2-0.13, z0+0.035, z0+mh-0.025, 'iron', 0.2, null, true, 0.05);  // door frame
    decalY(out, y0, -1, -mw/2+0.05, mw/2-0.16, z0+0.06, z0+mh-0.05, 'glassdark', 0.4, null, true, 0.06); // window
    decalY(out, y0, -1, -0.12, 0.0, z0+0.065, z0+0.085, 'ceramic', 0.6, null, true, 0.07);             // the plate
    decalY(out, y0, -1, mw/2-0.155, mw/2-0.145, z0+0.06, z0+mh-0.05, 'tin', 1.0, null, true, 0.07);    // handle
    decalY(out, y0, -1, mw/2-0.12, mw/2-0.02, z0+0.035, z0+mh-0.025, 'steel', -0.6, null, true, 0.05); // control panel
    decalY(out, y0, -1, mw/2-0.11, mw/2-0.03, z0+mh-0.08, z0+mh-0.045, 'lcd', 0.6, null, true, 0.07);  // clock
    for(let r=0;r<3;r++) for(let c=0;c<3;c++){ const kx=mw/2-0.105+c*0.027, kz=z0+0.06+r*0.035;
      decalY(out, y0, -1, kx, kx+0.016, kz, kz+0.018, 'tin', 0.9, null, true, 0.07); }
    decalY(out, y0, -1, mw/2-0.09, mw/2-0.05, z0+mh-0.13, z0+mh-0.095, 'tin', 1.1, null, true, 0.07);  // dial
    for(let k=0;k<4;k++) decalX(out, mw/2, 1, y0+0.06, y0+md-0.08, z0+0.08+k*0.04, z0+0.09+k*0.04, 'iron', -0.6, null, true, 0.05);
    for(let k=0;k<4;k++) decalY(out, y0+md, 1, -mw/2+0.06, mw/2-0.06, z0+0.07+k*0.04, z0+0.08+k*0.04, 'iron', -0.6, null, true, 0.05);
    box(out, -0.15, -0.13, y0+md, d/2+0.02, z0+0.02, z0+0.04, 'iron', 0.0);           // cord to the wall
  }

  // CHEST FREEZER. A white enamel chest on a dark plinth: a real hollow well (outer wall, rim with its
  // grey gasket, frosted inner walls, a frosted floor), a lid hinged at the back on two chrome hinges
  // that swings up (lid:0..1) to show its frosted liner and gasket, a chrome latch handle on the lid
  // front, a thermostat dial, a green pilot lamp, a drain plug and a vent grille on the side.
  // heap:0..2 is the frozen-catch rung under any fish the game lays on top: 0 bare frost, 1 a frost
  // crust and a few frozen fish down at the bottom, 2 the chest packed nearly to the rim. The size is
  // the PROPS entry's foot (a longer freezer is one more entry: chestFreezerLarge is the camper's, 1.75 m
  // long), and the well, the bed heights and the split plane all follow it. Exposes its well through freezerWell(name, heap) for the parity tests.
  // The enamel (the 'body' faces) is drawn FLAT, one ramp step per face with no ordered dither, so the
  // white chest reads as clean enamel and not as a grey speckle. The lid's underside is a white plastic
  // liner in shade with a moulded inner panel and a grey gasket all round, so the lid swung up reads as a
  // lid and not as a second frosted window. fishPitch is the well length one frozen fish takes in the
  // baked bed (never fewer than three across), so a longer chest packs more fish, not bigger ones.
  const FREEZER = { ht:0.86, plinth:0.06, wall:0.055, floorZ:0.16, lidT:0.07, lidOpenDeg:104,
                    bedZ:[0.16, 0.48, 0.70], fishPitch:0.38, linerB:-0.55, panelB:-0.2, panelInset:0.13 };
  function freezerWell(name, heap){ const P=PROPS[name]; if(!P) return null; const w=P.foot[0], d=P.foot[1];
    const h=Math.max(0, Math.min(2, heap|0));
    return { x0:-w/2+FREEZER.wall, x1:w/2-FREEZER.wall, y0:-d/2+FREEZER.wall, y1:d/2-FREEZER.wall,
             rimZ:FREEZER.ht, bedZ:FREEZER.bedZ[h] }; }
  function frostTex(seed){ return (u,v)=>{ const h=hash2(Math.floor(u*60)+seed*7, Math.floor(v*60)-seed*3);
    return h>0.86?1:(h<0.1?-1:0); }; }
  // a frozen fish lying in the chest: a tapered body of rounded rings and a forked tail, frosted
  function frozenFish(out, x, y, len, ang, z, mat){ const c=Math.cos(ang), s=Math.sin(ang), wd=len*0.17, hh=len*0.11;
    const P=(u,v)=>[x+u*c - v*s, y+u*s + v*c];
    const pts=[]; const n=10;
    for(let i=0;i<=n;i++){ const u=-len/2+len*0.85*i/n, t=i/n, half=wd*(0.35+0.65*Math.sin(Math.PI*Math.min(1,t*1.15))); pts.push([u, half]); }
    for(let i=n;i>=0;i--){ const u=-len/2+len*0.85*i/n, t=i/n, half=wd*(0.35+0.65*Math.sin(Math.PI*Math.min(1,t*1.15))); pts.push([u, -half]); }
    const ring2=pts.map(p=>P(p[0],p[1])), top2=pts.map(p=>P(p[0],p[1]*0.6));
    loft(out, ring2, top2, z, z+hh, mat, 0.1);
    slab(out, top2, z+hh, mat, 0.45, frostTex(Math.round(x*100)));
    const tx=-len/2, tl=len*0.16;
    slab(out, [P(tx,0), P(tx-tl,wd*0.9), P(tx-tl*0.7,0), P(tx-tl,-wd*0.9)].map(p=>p), z+hh*0.5, mat, 0.2);
  }
  function pChestFreezer(out, s){
    const P=PROPS[s.name], w=P.foot[0], d=P.foot[1], F0=FREEZER, ht=F0.ht, wt=F0.wall;
    // plinth (toe kick) and the enamel body as a hollow: outer wall, the rim with its gasket, the frosted well
    box(out, -w/2+0.035, w/2-0.035, -d/2+0.035, d/2-0.035, 0, F0.plinth, 'iron', -0.5, null, true);
    const oR=rrect(0,0,w/2,d/2,0.05,3), iR=rrect(0,0,w/2-wt,d/2-wt,0.025,3);
    loft(out, oR, oR, F0.plinth, ht, 'body', 0.0);
    band(out, oR, lerpRing(oR, iR, 0.45), ht, 'body', 0.4);
    band(out, lerpRing(oR, iR, 0.45), iR, ht+0.004, 'dark', 0.8);                          // gasket
    const fl=rrect(0,0,w/2-wt-0.01,d/2-wt-0.01,0.02,3);
    loft(out, fl, iR, F0.floorZ, ht, 'frost', -0.6, true, frostTex(2));
    slab(out, fl, F0.floorZ, 'frost', -0.3, frostTex(5));
    loft(out, lerpRing(fl, iR, 0.5), iR, ht-0.07, ht, 'frost', 0.6, true, frostTex(9)); // rime along the top
    // front: a brand badge, the thermostat dial, the pilot lamp, the drain plug; vents on the right side
    decalY(out, -d/2, -1, -0.09, 0.09, ht-0.2, ht-0.16, 'tin', 0.9, null, true, 0.07);
    decalY(out, -d/2, -1, w/2-0.2, w/2-0.12, 0.2, 0.28, 'tin', 0.8, null, true, 0.07);
    decalY(out, -d/2, -1, w/2-0.17, w/2-0.15, 0.22, 0.26, 'dark', 0.2, null, true, 0.09);
    decalY(out, -d/2, -1, w/2-0.27, w/2-0.24, 0.23, 0.26, 'lcd', 1.4, null, true, 0.08);
    decalY(out, -d/2, -1, -w/2+0.1, -w/2+0.14, F0.plinth+0.03, F0.plinth+0.07, 'dark', 0.3, null, true, 0.07);
    for(let k=0;k<5;k++) decalX(out, w/2, 1, -d/2+0.14, d/2-0.14, 0.14+k*0.035, 0.155+k*0.035, 'iron', -0.4, null, true, 0.05);
    decalY(out, d/2, 1, -w/2+0.15, w/2-0.15, 0.1, 0.34, 'iron', -0.8, null, true, 0.05);   // condenser at the back
    // the hinges at the back
    for(const hx of [-w/2+0.18, w/2-0.18]) box(out, hx-0.04, hx+0.04, d/2-0.005, d/2+0.03, ht-0.07, ht+0.03, 'tin', 0.4);
    // the frozen-catch rung
    const h=Math.max(0, Math.min(2, s.heap|0)), well=freezerWell(s.name, h);
    if(h>=1){ const bed=rrect(0,0,w/2-wt-0.012,d/2-wt-0.012,0.02,3);
      slab(out, bed, F0.bedZ[1], 'frost', 0.1, frostTex(13), 0.004);
      const rnd=mulberry32(321+h*17); const L=(well.x1-well.x0);
      // one fish per FREEZER.fishPitch of well, never fewer than three (Ginny's chest: three)
      const cols=Math.max(3, Math.round(L/F0.fishPitch));
      const n = h===1 ? cols : 0;
      for(let i=0;i<n;i++) frozenFish(out, well.x0+L*(0.22+0.56*i/(n-1)), well.y0+0.12+0.18*rnd(), L*1.02/n, (rnd()-0.5)*0.5, F0.bedZ[1], 'frostfish'); }
    if(h>=2){ // packed: three layers of frozen fish rising to the bed, the top layer frosted over
      const rnd=mulberry32(777), L=well.x1-well.x0, D=well.y1-well.y0, cols=Math.max(3, Math.round(L/F0.fishPitch));
      for(let layer=0; layer<3; layer++){ const z=F0.bedZ[1]+layer*((F0.bedZ[2]-F0.bedZ[1]-0.05)/3);
        for(let r=0;r<3;r++) for(let c=0;c<cols;c++){
          const fx=well.x0+L*(0.18+0.64*c/(cols-1))+(rnd()-0.5)*0.05, fy=well.y0+D*(0.2+0.3*r)+(rnd()-0.5)*0.03;
          frozenFish(out, fx, fy, L*0.9/cols, (rnd()-0.5)*0.4+(layer%2?Math.PI:0), z, 'frostfish'); } }
      slab(out, rrect(0,0,w/2-wt-0.015,d/2-wt-0.015,0.02,3), F0.bedZ[2]-0.012, 'frost', 0.2, frostTex(21), 0.03); }
    // the lid: built closed, then swung about the hinge line (y = d/2, z = ht)
    const th=Math.max(0, Math.min(1, +s.lid||0))*F0.lidOpenDeg*DEG, ct=Math.cos(th), st=Math.sin(th);
    const rot=(p)=>{ const dy=p[1]-d/2, dz=p[2]-ht; return [p[0], d/2+dy*ct+dz*st, ht-dy*st+dz*ct]; };
    const lid=[], z0=ht+0.006, z1=z0+F0.lidT;
    box(lid, -w/2-0.006, w/2+0.006, -d/2-0.012, d/2, z0, z1, 'body', 0.05);
    box(lid, -w/2+0.05, w/2-0.05, -d/2+0.04, d/2-0.04, z1, z1+0.012, 'body', 0.15);        // raised panel
    box(lid, -0.14, 0.14, -d/2-0.035, -d/2-0.012, z0+0.01, z0+0.05, 'tin', 0.5);          // latch handle
    box(lid, -0.1, 0.1, -d/2-0.04, -d/2-0.03, z0+0.015, z0+0.045, 'dark', 0.2);
    lid.push(F([[-w/2+wt,d/2-wt,z0-0.002],[w/2-wt,d/2-wt,z0-0.002],[w/2-wt,-d/2+wt,z0-0.002],[-w/2+wt,-d/2+wt,z0-0.002]], 'body', F0.linerB, 0));   // liner (underside), in shade
    const pi=F0.panelInset;   // the liner's moulded inner panel
    lid.push(F([[-w/2+pi,d/2-pi,z0-0.008],[w/2-pi,d/2-pi,z0-0.008],[w/2-pi,-d/2+pi,z0-0.008],[-w/2+pi,-d/2+pi,z0-0.008]], 'body', F0.panelB, 0));
    // the gasket all round the liner: back, front, left, right
    lid.push(F([[-w/2+0.02,d/2-0.02,z0-0.004],[w/2-0.02,d/2-0.02,z0-0.004],[w/2-wt,d/2-wt,z0-0.004],[-w/2+wt,d/2-wt,z0-0.004]], 'dark', 0.2, 0));
    lid.push(F([[-w/2+0.02,-d/2+wt,z0-0.004],[w/2-0.02,-d/2+wt,z0-0.004],[w/2-0.02,-d/2+0.02,z0-0.004],[-w/2+0.02,-d/2+0.02,z0-0.004]], 'dark', 0.2, 0));
    lid.push(F([[-w/2+0.02,d/2-0.02,z0-0.004],[-w/2+wt,d/2-wt,z0-0.004],[-w/2+wt,-d/2+wt,z0-0.004],[-w/2+0.02,-d/2+0.02,z0-0.004]], 'dark', 0.2, 0));
    lid.push(F([[w/2-wt,d/2-wt,z0-0.004],[w/2-0.02,d/2-0.02,z0-0.004],[w/2-0.02,-d/2+0.02,z0-0.004],[w/2-wt,-d/2+wt,z0-0.004]], 'dark', 0.2, 0));
    for(const f of lid){ f.v=f.v.map(rot); out.push(f); }
    for(const f of out) if(f.mat==='body') f.flat=true;   // clean enamel, see FREEZER
  }

  const PROPS = {
    bath:        { label:'Clawfoot bath',   cat:'bath',    foot:[1.6,0.78], seat:[-0.26,0.0,0.24], heading:0,   def:{fabric:'sage',fabric2:'blue'}, build:pBath },
    toilet:      { label:'Water closet',    cat:'bath',    foot:[0.46,0.7], seat:[0.0,-0.14,0.43], heading:270, def:{wood:'oak'},   build:pToilet },
    basin:       { label:'Washstand basin', cat:'bath',    foot:[0.58,0.44],                                  def:{wood:'walnut',fabric:'white'}, build:pBasin },
    shower:      { label:'Walk-in shower',  cat:'bath',    foot:[0.85,0.85], seat:[0.0,-0.02,0.02], heading:270, def:{worktop:'bluetile',fabric:'white',fabric2:'sage'}, build:pShower },
    wharfShower: { label:'Wharf shower',    cat:'bath',    foot:[0.96,0.84], seat:[0.0,-0.02,0.08], heading:270, def:{wood:'pine'},  build:pWharfShower },
    cookStove:   { label:'Cook stove',      cat:'kitchen', foot:[0.74,0.58],                                  def:{wood:'walnut'}, build:pCookStove },
    prepCounter: { label:'Prep counter',    cat:'kitchen', foot:[0.9,0.6],                                    def:{paint:'cream',wood:'pine',worktop:'butcher',fabric:'red',fabric2:'cream'}, build:pPrepCounter },
    microwave:   { label:'Microwave',       cat:'kitchen', foot:[0.5,0.44],                                   def:{paint:'white',wood:'oak',worktop:'slate'}, build:pMicrowave },
    chestFreezer:{ label:'Chest freezer',   cat:'kitchen', foot:[1.3,0.76], splitAt:[0.0,-0.325,0.0],          def:{paint:'white'}, build:pChestFreezer },
    chestFreezerLarge:{ label:'Chest freezer, large', cat:'kitchen', foot:[1.75,0.8], splitAt:[0.0,-0.345,0.0], def:{paint:'white'}, build:pChestFreezer },
  };

  function resolve(name, opts){ opts=opts||{}; const P=PROPS[name]||PROPS.bath;
    const g=(k,d)=> opts[k]!=null?opts[k] : (P.def[k]!=null?P.def[k]:d);
    return { name, wood:g('wood','pine'), paint: opts.paint!==undefined?opts.paint:(P.def.paint||null),
      fabric:g('fabric','white'), fabric2:g('fabric2','cream'), worktop:g('worktop','slate'),
      variant: opts.variant!=null?opts.variant:0, lid: Math.max(0, Math.min(1, +opts.lid||0)), heap: (opts.heap|0),
      fill: Math.max(0, Math.min(1, +opts.fill||0)), pour: !!opts.pour, froth: (opts.froth|0),
      flush: (opts.flush|0), run: (opts.run|0), split: opts.split||null,
      door: Math.max(0, Math.min(3, opts.door|0)),
      weather: opts.weather!=null?opts.weather:0.15, night:!!opts.night }; }

  function makeMats(s){ const wx=s.weather, night=s.night;
    const grime=r=>r.map(c=>mix(desat(c,wx*0.25),'#403627',wx*0.12));
    const warm=r=>night?r.map(c=>mix(mix(c,'#c98b3f',0.12),'#241a10',0.2)):r;
    const t=r=>warm(grime(r)), clean=r=>warm(r);
    const wood=WOODS[s.wood]||WOODS.pine, paint=s.paint?(BODY[s.paint]||BODY.sage):null;
    return { wood:{ramp:t(wood)}, body:{ramp:t(paint||wood)}, top:{ramp:t(wood)},
      worktop:{ramp:t(WORKTOPS[s.worktop]||WORKTOPS.slate)}, iron:{ramp:t(IRON)}, tin:{ramp:t(TIN)},
      steel:{ramp:t(BODY.steel)}, brass:{ramp:t(BRASS)}, stone:{ramp:t(WORKTOPS.marble)},
      ceramic:{ramp:t(CERAMIC)}, fabric:{ramp:t(BODY[s.fabric]||BODY.white)},
      fabric2:{ramp:t(BODY[s.fabric2]||BODY.cream)}, trim:{ramp:t(TRIM)},
      glass:{ramp: night?GLASSN:GLASSD}, glassdark:{ramp: GLASSN}, lcd:{ramp:['#1f3a2a','#2f5a3c','#4f8a5c','#7fbf86']},
      fire:{ramp:FIRE}, water:{ramp:clean(WATER)}, suds:{ramp:clean(SUDS)}, stream:{ramp:clean(STREAM)},
      dark:{ramp:DARK}, soap:{ramp:clean(SOAP)}, frost:{ramp:warm(FROST)}, frostfish:{ramp:warm(FROSTFISH)} }; }

  // ================= THE KITCHEN STORE AND THE KITCHEN SINK =================
  // Two more pieces, appended to PROPS below (never an edit of the table above).
  //   door:0..3   the fridge door, shut (0), ajar, half open, wide open (3): FRIDGE.doorDeg[door]

  // UPRIGHT FRIDGE. A white enamel cabinet with a rounded shoulder on a dark plinth, a chrome lever handle and a badge
  // on the door, a grille at the foot and the condenser coil on the back. The door hangs on the right-hand
  // edge and swings out (door:1..3) to show the cold inside: a white liner, the icebox at the top with its
  // frosted flap, two wire shelves with a milk bottle, a jar, a butter dish, a covered bowl and a wrapped
  // parcel of fish, the glass crisper at the bottom with apples in it, and the door's own racks of bottles.
  // The lamp inside lifts the liner while the door is open.
  const FRIDGE = { ht:1.62, plinth:0.07, wall:0.035, doorT:0.06, doorZ0:0.2, doorDeg:[0, 24, 58, 102] };
  function pFridge(out, s){
    const P=PROPS[s.name], w=P.foot[0], d=P.foot[1], R=FRIDGE, ht=R.ht, t=R.wall, pl=R.plinth;
    const open=(s.door|0)>0, lit=open?0.35:0;
    // plinth and the cabinet: two sides, the back, the top with its rounded shoulder, the compressor base
    box(out, -w/2+0.03, w/2-0.03, -d/2+0.05, d/2-0.03, 0, pl, 'iron', -0.5, null, true);
    box(out, -w/2, -w/2+t, -d/2, d/2, pl, ht, 'body', 0);
    box(out, w/2-t, w/2, -d/2, d/2, pl, ht, 'body', 0);
    box(out, -w/2+t, w/2-t, d/2-t, d/2, pl, ht, 'body', 0);
    box(out, -w/2, w/2, -d/2, d/2, ht-t, ht, 'body', 0.05);
    box(out, -w/2+0.02, w/2-0.02, -d/2+0.02, d/2-0.02, ht, ht+0.03, 'body', 0.4);       // the shoulder
    box(out, -w/2+t, w/2-t, -d/2, d/2-t, pl, R.doorZ0, 'body', 0);                      // compressor base
    decalY(out, -d/2, -1, -w/2+0.035, w/2-0.035, pl+0.008, R.doorZ0-0.008, 'tin', 0.15, null, true, 0.04);     // the chrome kick plate
    decalY(out, -d/2, -1, -w/2+0.035, w/2-0.035, pl+0.008, pl+0.018, 'dark', 0.1, null, true, 0.045);           // its shadowed foot
    for(let k=0;k<4;k++) decalY(out, -d/2, -1, -w/2+0.07, w/2-0.07, pl+0.025+k*0.03, pl+0.04+k*0.03, 'iron', -0.6, null, true, 0.05); // foot grille
    // side shading: each flank darkens toward the back and catches a lit strip at its front edge
    for(const [xv,nx] of [[-w/2,-1],[w/2,1]]){
      decalX(out, xv, nx, 0.0, d/2-0.01, pl+0.01, ht-0.01, 'body', -0.22, null, true, 0.03);
      decalX(out, xv, nx, d/4, d/2-0.01, pl+0.01, ht-0.01, 'body', -0.38, null, true, 0.035);
      decalX(out, xv, nx, -d/2+0.005, -d/2+0.025, pl+0.01, ht-0.01, 'body', 0.35, null, true, 0.035); }
    decalTop(out, -w/2+0.02, w/2-0.02, -d/2+0.02, -d/2+0.06, ht+0.03, 'body', 0.6, 0.06);                       // the lit front of the shoulder
    for(let k=0;k<8;k++) decalY(out, d/2, 1, -w/2+0.1, w/2-0.1, 0.3+k*0.14, 0.32+k*0.14, 'iron', -0.7, null, true, 0.05);      // coil on the back
    for(const x of [-w/2+0.12, w/2-0.12]) decalY(out, d/2, 1, x-0.012, x+0.012, 0.28, 1.36, 'iron', -0.3, null, true, 0.06);
    // the liner: white on the inside faces, the floor of the cold space, lifted by the lamp when open
    decalX(out, -w/2+t, 1, -d/2+0.005, d/2-t, R.doorZ0, ht-t, 'trim', 0.2+lit, null, true, 0.02);
    decalX(out, w/2-t, -1, -d/2+0.005, d/2-t, R.doorZ0, ht-t, 'trim', -0.2+lit, null, true, 0.02);
    decalY(out, d/2-t, -1, -w/2+t, w/2-t, R.doorZ0, ht-t, 'trim', 0.1+lit, null, true, 0.02);
    decalTop(out, -w/2+t, w/2-t, -d/2, d/2-t, R.doorZ0, 'trim', 0.3+lit, 0.02);
    // the icebox under the top: a frosted box with its tin flap and a latch
    const ix0=-w/2+t+0.03, ix1=w/2-t-0.03, iy0=-d/2+0.07, iz0=ht-0.34, iz1=ht-t-0.02;
    box(out, ix0, ix1, iy0, d/2-t, iz0, iz1, 'frost', 0.1);
    decalY(out, iy0, -1, ix0+0.02, ix1-0.02, iz0+0.02, iz1-0.02, 'tin', 0.4, null, true, 0.05);
    decalY(out, iy0, -1, -0.05, 0.05, iz0+0.03, iz0+0.06, 'iron', 0.2, null, true, 0.07);
    decalY(out, iy0, -1, ix0+0.02, ix1-0.02, iz0+0.02, iz0+0.035, 'frost', 0.9, null, true, 0.06);
    // two wire shelves
    const shelf=[0.64, 0.98];
    for(const z of shelf){ box(out, -w/2+t, w/2-t, -d/2+0.04, d/2-t, z, z+0.012, 'tin', 0.3);
      for(let k=0;k<5;k++){ const y=-d/2+0.08+k*0.1; box(out, -w/2+t, w/2-t, y-0.004, y+0.004, z+0.012, z+0.016, 'tin', 0.7); } }
    // the top shelf: a milk bottle, a jam jar, a butter dish
    const z1=shelf[1]+0.016;
    prism(out, -0.18, 0.02, 0.045, z1, z1+0.2, 10, 'suds', 0.1);
    prism(out, -0.18, 0.02, 0.028, z1+0.2, z1+0.24, 10, 'suds', 0.3);
    prism(out, -0.18, 0.02, 0.03, z1+0.24, z1+0.255, 10, 'tin', 0.6);
    prism(out, -0.05, 0.1, 0.04, z1, z1+0.09, 10, 'fabric', 0.0);                       // the jam, red
    prism(out, -0.05, 0.1, 0.042, z1+0.09, z1+0.11, 10, 'brass', 0.4);
    box(out, 0.04, 0.22, -0.12, 0.0, z1, z1+0.02, 'ceramic', 0.3);
    box(out, 0.07, 0.19, -0.1, -0.02, z1+0.02, z1+0.06, 'fabric2', 0.3);                 // butter under its dish
    // the lower shelf: a covered bowl and a parcel of fish in paper, tied
    const z2=shelf[0]+0.016;
    prism(out, -0.14, -0.02, 0.1, z2, z2+0.08, 12, 'ceramic', 0.05);
    prism(out, -0.14, -0.02, 0.105, z2+0.08, z2+0.1, 12, 'fabric2', 0.35);
    box(out, 0.02, 0.24, -0.06, 0.16, z2, z2+0.07, 'fabric2', 0.15);
    box(out, 0.02, 0.24, 0.04, 0.06, z2+0.07, z2+0.075, 'iron', 0.2);
    box(out, 0.12, 0.14, -0.06, 0.16, z2+0.07, z2+0.075, 'iron', 0.2);
    // the crisper: a glass drawer, apples showing through its top
    const cz0=R.doorZ0+0.01, cz1=R.doorZ0+0.2;
    box(out, -w/2+t+0.02, w/2-t-0.02, -d/2+0.05, d/2-t-0.02, cz0, cz1, 'glass', 0.1);
    for(const [ax,ay] of [[-0.16,-0.08],[-0.06,-0.1],[0.05,-0.06],[0.15,-0.1],[-0.1,0.04],[0.1,0.05]])
      prism(out, ax, ay, 0.04, cz1, cz1+0.05, 8, 'fabric', 0.2);
    box(out, -0.06, 0.06, -d/2+0.05, -d/2+0.06, cz1-0.06, cz1-0.03, 'tin', 0.6);         // its pull
    // the door, built shut and swung about its hinge line (x = +w/2, y = -d/2)
    const dz0=R.doorZ0, dz1=ht-0.012, dy0=-d/2-R.doorT, dy1=-d/2, door=[];
    box(door, -w/2, w/2, dy0, dy1, dz0, dz1, 'body', 0.05);
    box(door, -w/2+0.015, w/2-0.015, dy0-0.008, dy0, dz1-0.02, dz1, 'tin', 0.5);         // chrome rim along the top
    // the freezer split: a dark seam across the door at the icebox, the top a short freezer door
    const zs=ht-0.38;
    decalY(door, dy0, -1, -w/2+0.008, w/2-0.008, zs-0.012, zs+0.006, 'dark', 0.05, null, true, 0.075);
    decalY(door, dy0, -1, -w/2+0.008, w/2-0.008, zs+0.006, zs+0.016, 'body', 0.55, null, true, 0.075);
    // the door's own shading: its free edge falls into shadow, its hinge edge catches the light
    decalY(door, dy0, -1, -w/2+0.004, -w/2+0.03, dz0+0.01, dz1-0.03, 'body', -0.3, null, true, 0.07);
    decalY(door, dy0, -1, w/2-0.025, w/2-0.004, dz0+0.01, dz1-0.03, 'body', 0.3, null, true, 0.07);
    decalY(door, dy0, -1, -0.09, 0.09, dz1-0.19, dz1-0.11, 'tin', 0.9, null, true, 0.07); // the badge, chrome
    decalY(door, dy0, -1, -0.07, 0.07, dz1-0.175, dz1-0.125, 'fabric', 0.15, null, true, 0.08); // red enamel
    decalY(door, dy0, -1, -0.045, 0.045, dz1-0.155, dz1-0.145, 'tin', 1.0, null, true, 0.09);   // its script
    const hx=-w/2+0.065, hz0=0.78, hz1=1.14;                                               // the lever handle
    for(const z of [hz0, hz1-0.05]) box(door, hx-0.016, hx+0.016, dy0-0.05, dy0, z, z+0.05, 'tin', 0.3);
    box(door, hx-0.02, hx+0.02, dy0-0.07, dy0-0.04, hz0, hz1, 'tin', 0.75);
    decalY(door, dy0, -1, hx+0.02, hx+0.04, hz0-0.02, hz1, 'dark', 0.25, null, true, 0.072);   // its shadow on the door
    for(const z of [zs+0.05, zs+0.17]) box(door, hx-0.016, hx+0.016, dy0-0.05, dy0, z, z+0.04, 'tin', 0.3); // the freezer's
    box(door, hx-0.02, hx+0.02, dy0-0.07, dy0-0.04, zs+0.05, zs+0.21, 'tin', 0.75);
    decalY(door, dy0, -1, hx+0.02, hx+0.04, zs+0.03, zs+0.21, 'dark', 0.25, null, true, 0.072);
    // the door's inside: the gasket, the liner, three racks with bottles and eggs
    decalY(door, dy1, 1, -w/2+0.015, w/2-0.015, dz0+0.015, dz1-0.015, 'dark', 0.2, null, true, 0.04);
    decalY(door, dy1, 1, -w/2+0.04, w/2-0.04, dz0+0.04, dz1-0.04, 'trim', 0.3+lit, null, true, 0.05);
    const racks=[0.42, 0.82, 1.18];
    for(let r=0;r<racks.length;r++){ const z=racks[r];
      box(door, -w/2+0.06, w/2-0.06, dy1, dy1+0.075, z, z+0.012, 'trim', -0.2);
      box(door, -w/2+0.06, w/2-0.06, dy1+0.065, dy1+0.075, z+0.012, z+0.07, 'trim', 0.3);
      if(r===2){ for(let k=0;k<5;k++) prism(door, -0.2+k*0.1, dy1+0.035, 0.025, z+0.012, z+0.05, 8, 'soap', 0.5); continue; }
      for(let k=0;k<(r?4:3);k++){ const bx=-0.2+k*(r?0.13:0.18), tall=r?0.17:0.24;
        prism(door, bx, dy1+0.035, 0.028, z+0.012, z+tall, 8, r?'glass':'ceramic', 0.15);
        prism(door, bx, dy1+0.035, 0.016, z+tall, z+tall+0.04, 8, r?'glass':'ceramic', 0.35);
        prism(door, bx, dy1+0.035, 0.018, z+tall+0.04, z+tall+0.05, 8, k&1?'brass':'tin', 0.5); } }
    const th=R.doorDeg[Math.max(0, Math.min(3, s.door|0))]*DEG, ct=Math.cos(th), st=Math.sin(th), Hx=w/2, Hy=-d/2;
    const rot=(p)=>{ const dx=p[0]-Hx, dy=p[1]-Hy; return [Hx+dx*ct-dy*st, Hy+dx*st+dy*ct, p[2]]; };
    for(const f of door){ f.v=f.v.map(rot); out.push(f); }
    // the two hinges stay on the cabinet
    for(const z of [dz0+0.06, dz1-0.1]) box(out, w/2-0.01, w/2+0.012, -d/2-0.04, -d/2+0.01, z, z+0.06, 'tin', 0.4);
  }

  // KITCHEN SINK. A painted base unit with cupboard doors under a deep white china sink whose apron stands
  // proud of the front, set in a wooden worktop with a grooved draining board, a tiled upstand, a brass
  // bridge tap with two cross handles and a swan spout, a soap dish, a jug on the board and a tea towel on
  // the rail. The sink is a real hollow and fills, froths and runs exactly as the bathroom basin does.
  const SINK = { ht:0.9, top:0.05, bowlX:[-0.44, 0.02], bowlBack:0.16, bowlDepth:0.24, apron:0.025 };
  function grooveTex(pitch){ return (u,v)=>{ const f=((v%pitch)+pitch)%pitch; return f<0.012?-2:(f<0.02?0.6:0); }; }
  function pKitchenSink(out, s){
    const P=PROPS[s.name], w=P.foot[0], d=P.foot[1], K=SINK, ht=K.ht, tz=ht, tb=ht-K.top;
    const bx0=K.bowlX[0], bx1=K.bowlX[1], by0=-d/2-K.apron, by1=K.bowlBack;
    // the base unit: walls with no lid (the sink sits down into it), the toe kick, doors and a drawer
    box(out, -w/2, w/2, -d/2, d/2, 0.08, tb, 'body', 0, null, true);
    box(out, -w/2+0.03, w/2-0.03, -d/2+0.04, d/2, 0, 0.08, 'iron', -0.5, null, true);
    panelsY(out, -d/2, -w/2+0.04, bx1, 0.1, ht-0.3, 1, 2, true);                    // the sink cupboard, two doors
    panelsY(out, -d/2, bx1+0.02, w/2-0.04, ht-0.27, tb-0.02, 1, 1, true);           // a drawer
    panelsY(out, -d/2, bx1+0.02, w/2-0.04, 0.1, ht-0.29, 1, 1, true);               // and a door
    for(const sx of [-1,1]) decalX(out, sx*w/2, sx, -d/2+0.06, d/2-0.06, 0.12, tb-0.04, 'body', -1.4, null, true, 0.05);
    // the worktop round the sink: a strip at the left, a strip behind, the grooved draining board at the right
    box(out, -w/2-0.02, bx0, -d/2-0.02, d/2, tb, tz, 'worktop', 0.1, null, true);
    slab(out, [[-w/2-0.02,-d/2-0.02],[bx0,-d/2-0.02],[bx0,d/2],[-w/2-0.02,d/2]], tz, 'worktop', 0.3, plankTex(0.06));
    box(out, bx0, bx1, by1, d/2, tb, tz, 'worktop', 0.1, null, true);
    slab(out, [[bx0,by1],[bx1,by1],[bx1,d/2],[bx0,d/2]], tz, 'worktop', 0.3, plankTex(0.06));
    box(out, bx1, w/2+0.02, -d/2-0.02, d/2, tb, tz, 'worktop', 0.1, null, true);
    slab(out, [[bx1,-d/2-0.02],[w/2+0.02,-d/2-0.02],[w/2+0.02,d/2],[bx1,d/2]], tz, 'worktop', 0.35, grooveTex(0.05));
    box(out, bx1, w/2+0.02, -d/2-0.025, -d/2-0.005, tz, tz+0.012, 'worktop', 0.45);    // the board's front lip
    // the tiled upstand behind
    box(out, -w/2-0.02, w/2+0.02, d/2-0.03, d/2, tz, tz+0.22, 'trim', 0.05, tileTex(0.1));
    box(out, -w/2-0.03, w/2+0.03, d/2-0.045, d/2+0.005, tz+0.22, tz+0.24, 'trim', 0.4);
    // the china sink: an outer wall (its apron shows on the front), the rolled rim, the hollow and its drain
    const bcx=(bx0+bx1)/2, bcy=(by0+by1)/2, ha=(bx1-bx0)/2, hb=(by1-by0)/2, zr=tz+0.012, zf=tz-K.bowlDepth;
    const oR=rrect(bcx,bcy,ha,hb,0.035,3), iR=rrect(bcx,bcy,ha-0.035,hb-0.035,0.03,3), iF=rrect(bcx,bcy,ha-0.07,hb-0.07,0.05,3);
    loft(out, oR, oR, tz-0.3, zr, 'ceramic', 0.05);
    band(out, oR, iR, zr, 'ceramic', 0.45);
    loft(out, iF, iR, zf, zr, 'ceramic', -1.1, true);
    slab(out, iF, zf, 'ceramic', -0.8);
    prism(out, bcx, bcy+0.02, 0.025, zf, zf+0.005, 8, 'dark', 0);
    box(out, bcx-0.006, bcx+0.006, bcy+0.02, bcy+0.11, zf+0.005, zf+0.009, 'brass', 0.3);   // the plug's chain
    const surf=water(out, iF, iR, zf, zr-0.03, s.fill, 11+(s.froth|0));
    suds(out, surf, s.froth, 9);
    // the bridge tap: two pillars with cross handles on the strip behind, a bridge, a swan spout over the sink
    const ty=(by1+d/2-0.03)/2, sy=bcy+0.04;
    for(const tx of [bcx-0.09, bcx+0.09]){
      box(out, tx-0.02, tx+0.02, ty-0.02, ty+0.02, tz, tz+0.12, 'brass', 0.2);
      box(out, tx-0.05, tx+0.05, ty-0.01, ty+0.01, tz+0.12, tz+0.14, 'brass', 0.5);
      box(out, tx-0.01, tx+0.01, ty-0.05, ty+0.05, tz+0.12, tz+0.14, 'brass', 0.5);
      prism(out, tx, ty, 0.014, tz+0.14, tz+0.158, 6, 'ceramic', 0.6); }
    box(out, bcx-0.09, bcx+0.09, ty-0.015, ty+0.015, tz+0.07, tz+0.1, 'brass', 0.35);
    box(out, bcx-0.018, bcx+0.018, ty-0.018, ty+0.018, tz+0.1, tz+0.27, 'brass', 0.15);
    box(out, bcx-0.018, bcx+0.018, sy, ty+0.018, tz+0.24, tz+0.27, 'brass', 0.4);
    box(out, bcx-0.016, bcx+0.016, sy-0.01, sy+0.025, tz+0.16, tz+0.25, 'brass', 0.25);
    if(s.pour) stream(out, bcx, sy+0.007, tz+0.16, surf?surf.z:zf, (s.fill*9)|0, 0.024);
    // a soap dish behind the board, an enamel jug on it, a towel on the rail of the drawer
    box(out, bx1+0.06, bx1+0.18, d/2-0.13, d/2-0.05, tz, tz+0.02, 'ceramic', 0.4);
    box(out, bx1+0.08, bx1+0.16, d/2-0.12, d/2-0.06, tz+0.02, tz+0.045, 'soap', 0.4);
    prism(out, w/2-0.14, -0.04, 0.06, tz, tz+0.16, 10, 'ceramic', 0.1);
    prism(out, w/2-0.14, -0.04, 0.07, tz+0.13, tz+0.16, 10, 'ceramic', 0.35);
    box(out, w/2-0.22, w/2-0.2, -0.06, -0.02, tz+0.05, tz+0.13, 'ceramic', 0.2);
    prism(out, w/2-0.14, -0.04, 0.072, tz+0.158, tz+0.165, 10, 'iron', 0.1);           // its dark rim
    box(out, bx1+0.04, w/2-0.04, -d/2-0.05, -d/2-0.03, ht-0.31, ht-0.29, 'tin', 0.5);
    box(out, bx1+0.12, bx1+0.3, -d/2-0.065, -d/2-0.045, ht-0.6, ht-0.29, 'fabric', 0.1, plankTex(0.05));
  }

  PROPS.fridge      = { label:'Fridge',       cat:'kitchen', foot:[0.7,0.68], def:{paint:'white',fabric:'red',fabric2:'cream'}, build:pFridge };
  PROPS.kitchenSink = { label:'Kitchen sink', cat:'kitchen', foot:[1.0,0.6],  def:{paint:'cream',wood:'pine',worktop:'butcher',fabric:'blue'}, build:pKitchenSink };

  // ---- the near/far split: clip every face at the plane through the seat, square to the view ----
  function splitFaces(faces, B, seat){ const yS=seat[0]*B.stt + seat[1]*B.ct, out=[];
    const f=(p)=> p[0]*B.stt + p[1]*B.ct - yS;
    for(const face of faces){ const ds=face.v.map(f);
      if(ds.every(d=>d<=1e-6)){ face.layer=1; out.push(face); continue; }
      if(ds.every(d=>d>=-1e-6)){ face.layer=0; out.push(face); continue; }
      for(const keepNear of [true,false]){ const pv=[], pu=[]; const n=face.v.length;
        for(let i=0;i<n;i++){ const j=(i+1)%n, a=face.v[i], b=face.v[j], da=ds[i], db=ds[j];
          const inA = keepNear ? da<=0 : da>=0, inB = keepNear ? db<=0 : db>=0;
          if(inA){ pv.push(a); if(face.uv) pu.push(face.uv[i]); }
          if(inA!==inB){ const t=da/(da-db);
            pv.push([a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t, a[2]+(b[2]-a[2])*t]);
            if(face.uv) pu.push([face.uv[i][0]+(face.uv[j][0]-face.uv[i][0])*t, face.uv[i][1]+(face.uv[j][1]-face.uv[i][1])*t]); } }
        if(pv.length>=3) out.push({ v:pv, mat:face.mat, b:face.b, db:face.db, uv:face.uv?pu:null, tex:face.tex, flat:face.flat, layer:keepNear?1:0 }); } }
    return out; }

  // ---- rasteriser (the interiorPropRig recipe, plus the winning face's layer per pixel) ----
  function paint(faces, B, MATS){ const N=W*H;
    const zbuf=new Float32Array(N).fill(Infinity), dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null), ibuf=new Int16Array(N), nbuf=new Array(N).fill(null), lbuf=new Int8Array(N);
    for(const f of faces){ const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]); let sh=shadeOf(n, B.se, B.ce);
      if(sh<0 && (f.b<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx=sh*GAIN+BIAS+f.b; const M=MATS[f.mat]||MATS.body, ramp=M.ramp, tex=f.tex, uv=f.uv, flat=f.flat, layer=f.layer|0;
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
          if(deff<zbuf[i]){ zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat; lbuf[i]=layer; let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi+=tex(uu,vv); }
            let idx; if(flat){ idx=Math.round(fi); } else { const base=Math.floor(fi); idx=base+((fi-base)>BAYER[x&3][y&3]?1:0); }
            idx=Math.max(0,Math.min(ramp.length-1,idx)); rbuf[i]=ramp; ibuf[i]=idx; } }
      }
    }
    return { rbuf, ibuf, nbuf, dep, lbuf };
  }
  function post(bufs, s){ const { rbuf, ibuf, nbuf, dep }=bufs, N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx,ny=y+dy; if(nx>=W||ny>=H) continue; const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j; out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; } } }
    if(s.weather>0.02){ const rnd=mulberry32(701|((s.variant*53)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='wood'||m==='body'||m==='top'||m==='fabric'||m==='stone') && rnd()<s.weather*0.05)
          out[i]=rbuf[i][Math.max(0,ibuf[i]-1)]; } }
    if(s.night){ for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x; if(nbuf[i]!=='fire') continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1],[1,-1],[-1,-1]]){ const j=(y+dy)*W+(x+dx); if(out[j]&&nbuf[j]!=='fire') out[j]=mix(out[j],'#f0b45a',0.24); } } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
      if(touch) out[i]=KEY; }
    return out;
  }
  // keep only the near layer of a full render: its own pixels, and the keyline pixels that border them
  function nearOnly(cols, bufs){ const { rbuf, lbuf }=bufs, N=W*H, out=new Array(N).fill(null);
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; const c=cols[i]; if(!c) continue;
      if(rbuf[i]){ if(lbuf[i]===1) out[i]=c; continue; }
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx<0||nx>=W||ny<0||ny>=H) continue;
        const j=ny*W+nx; if(rbuf[j]&&lbuf[j]===1){ out[i]=c; break; } } }
    return out; }
  function toRGBA(cols){ const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; } const [r,g,bl]=hex2rgb(c); rgba[i*4]=r;rgba[i*4+1]=g;rgba[i*4+2]=bl;rgba[i*4+3]=255; }
    return rgba;
  }

  function render(name, dir, opts){ opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const P=PROPS[name]||PROPS.bath, s=resolve(name,opts), B=camBasis({dir,elev:opts.elev}), MATS=makeMats(s);
    s.cam=B; let faces=[]; P.build(faces, s);
    const at = P.seat || P.splitAt, near = s.split==='near' && at;
    if(near) faces=splitFaces(faces, B, at);
    const bufs=paint(faces,B,MATS), cols=post(bufs, s);
    return toRGBA(near ? nearOnly(cols, bufs) : cols);
  }
  function footprint(name){ const P=PROPS[name]||PROPS.bath; return { w:P.foot[0], d:P.foot[1] }; }
  function height(name, opts){ const out=[]; (PROPS[name]||PROPS.bath).build(out, resolve(name,opts));
    let h=0; for(const f of out) for(const v of f.v) if(v[2]>h) h=v[2]; return h; }
  function seat(name){ const P=PROPS[name]; return P&&P.seat ? { x:P.seat[0], y:P.seat[1], z:P.seat[2], heading:P.heading||0 } : null; }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }
  function list(){ return Object.keys(PROPS); }

  root.ApplianceIso = { W, H, PX, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], PROPS, list, footprint, height, seat, render, project, freezerWell };
})(typeof globalThis!=='undefined'?globalThis:window);

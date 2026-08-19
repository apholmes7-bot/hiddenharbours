/* Hidden Harbours — parametric ISO AMPHIBIOUS 8x8 rig (SAME turntable + camera + shading as
   vehicleIsoRig.js / camperIsoRig.js / the fleet). First body: OTTER 8x8 — a skid-steer
   amphibious XTV: one sealed poly tub hull lofted off stations, eight low-pressure tires on
   rigid axles, NO suspension, optional rubber tracks wrapping each side, and a FLOAT pose that
   settles the machine onto its waterline. 45deg steps, elev 40deg, flat-facet shading from the
   fixed upper-LEFT key, z-buffered, ordered dither, depth-edge darkening, NO AA. 32 px = 1 m.
   Ringless (ADR 0031).

   THE BAKE IS DRY. Nothing below the waterline is cut and no wake is painted: `float` only
   lowers the machine by its draft so the attitude is right, and the GAME clips the sprite
   against its own water shader. The plane to clip at, the draft, the freeboard and the
   waterline section are published in amphibIsoRig.otter8x8.gameplay.json (section FLOAT).

   ARTICULATION — pose params on render(dir,opts):
     roll              master wheel/track roll, in REVOLUTIONS (cyclic)
     rollL rollR       per-SIDE offsets, revolutions. Skid steer: rollL=+t, rollR=-t spins it.
     yaw               heading off the 45deg facing grid, in DEGREES (-45..45). SHE HAS NO STEER
                       AXLE — nothing on this machine yaws but the machine itself, so this IS her
                       steering. It turns the whole hull under the fixed key, so the shading stays
                       right; a quarter-turn of side split (0.246 rev) is 45deg of yaw, one facing.
     float   0..1      the machine settles 0.52 m onto its waterline (no clipping — see above)
     hatch   0..1      bow engine hatch, hinged at its aft edge, 0 -> 52deg (bay underneath)

   FITTED: tracks(false) rack(true) winch(true) screen(false) bimini(false) lamps via night.

   RAMP BUDGET: a placed Otter paints 16 ramps — the fleet cap. It was 17 until #558 merged the
   cockpit MAT into MESH (see makeMats). A 17th ramp added here makes her unplaceable again.

   ORIGIN / PIVOT: ground-centre of the hull footprint. +x curb side, +y bow, +z up.
   Facing 0 (N) shows the TRANSOM; facing 4 (S) shows the bow.

   Exposes globalThis.AmphibIso = { W,H,PX,DIRS,pivot,order,defaultElev, BODY,POLY,IRON,GALV,
     RUBBER,CHROME,CLOTH,GLASSD,GLASSN,KEY, BODIES,PRESETS,CUES,G, list(), dims(opts),
     resolve(opts), render(dir,opts), frames(dir,n,opts,cue), anchors(dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 256, H = 192, cx = 128, groundY = 128;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;                    // ADR 0031

  // ---- ramps (harbour master ramps, shared with the houses, the fleet and the dually) ----
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
  };
  const POLY   = ['#101315','#171b1e','#1f2529','#293034','#353d43','#434d54']; // black lower hull
  const TRIM   = ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'];
  const IRON   = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970'];
  const GALV   = ['#565b5f','#6d7276','#868b8f','#a0a5a8','#bbbfc1','#d6d9da'];
  const RUBBER = ['#121417','#191c20','#22262b','#2c3137','#383e45','#464d55'];
  const CHROME = ['#4a5157','#5f696f','#7b858c','#98a2a8','#b6bec2','#d6dbdd'];
  const CLOTH  = ['#1b1d21','#232629','#2c3034','#373c41','#44494f','#53595f'];
  const SHADE  = ['#0b0e11','#0f1418','#141a1f','#1a2128','#212a31','#28323a'];
  const GLASSD = ['#1b262b','#243238','#2f4149','#3d545c','#5d7b82','#96b6ba'];
  const GLASSN = ['#141d2b','#1d2a3d','#2a3c53','#3d5570','#6b7f9c','#95a8c0'];
  const GLOW   = ['#7a5a18','#c09a2c','#efd06a','#fdf0b6'];
  const LENSR  = ['#3a0c0a','#5a120e','#7d1c14','#a52a1d','#c93c2a','#e4573f'];
  const LENSA  = ['#4a2c07','#6d420b','#8f5a12','#b0771f','#cc9633','#e5b455'];
  const KEY    = '#1a1c22';

  // ---- shading (identical recipe to vehicleIsoRig / the fleet) ----
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }

  function camBasis(opts){ const dir=opts.dir||0, th=dir*Math.PI/4 + (opts.yaw||0)*DEG, e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { th, ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) }; }
  function projVert(x,y,z,B){ const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:groundY-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) }; }
  function normal(a,b,c){ const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx; const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m]; }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }

  // ---- face builders (outward-normal winding, fleet conventions) ----
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
  function ventTex(){ const p=0.075; return (u,v)=>{ if(v<1.00||v>1.26||u<-0.24||u>0.24) return 0;
    const f=((v%p)+p)%p; return f<0.032?-2:0; }; }
  function meshTex(){ const p=0.062; return (u,v)=>{ const f=((u%p)+p)%p; return f<0.028?-2:0; }; }
  function lugTex(phase){ const c=0.155; return (u,v)=>{ const f=(((u+phase)%c)+c)%c; return f<c*0.46?-1:0; }; }

  // ================= GEOMETRY — OTTER 8x8 =================
  const G = {
    bowY:1.62, tailY:-1.48, beam:0.70, keelZ:0.24, railZ:0.86, floorZ:0.44,
    axY:[1.02,0.34,-0.34,-1.02], wheelX:0.63, wheelR:0.32, tireW:0.30,
    cockpit:[-1.14,0.80], sinkMax:0.52, trackHW:0.20, trackR:0.39, trackLift:0.07,
    trackWidth:1.26, revPerYaw45:0.2461,
    hatchY:[0.90,1.34], hatchHW:0.34,
  };
  const hwAt=(y)=>{ const b=G.beam;
    if(y>0.80){ const t=Math.min(1,(y-0.80)/(G.bowY-0.80)); return 0.11+(b-0.11)*Math.sqrt(Math.max(0,1-t*t)); }
    if(y<-1.05){ const t=Math.min(1,(-1.05-y)/(G.tailY+1.05?Math.abs(G.tailY+1.05):1)); return 0.50+(b-0.50)*Math.sqrt(Math.max(0,1-t*t)); }
    return b; };
  const bzAt=(y)=>{
    if(y>0.72){ const t=Math.min(1,(y-0.72)/(G.bowY-0.72)); return G.keelZ+0.44*Math.pow(t,1.7); }
    if(y<-1.10){ const t=Math.min(1,(-1.10-y)/Math.abs(G.tailY+1.10)); return G.keelZ+0.10*t*t; }
    return G.keelZ; };
  const topAt=(y)=>{
    if(y>0.80){ const t=Math.min(1,(y-0.80)/(G.bowY-0.80)); return G.railZ+0.09*Math.sin(Math.PI*t)-0.12*t*t*t; }
    if(y<-1.14){ const t=Math.min(1,(-1.14-y)/Math.abs(G.tailY+1.14)); return G.railZ-0.05*t*t; }
    return G.railZ; };
  const crownAt=(y)=>{
    if(y>0.80){ const t=Math.min(1,(y-0.80)/(G.bowY-0.80)); return topAt(y)+0.17*Math.sin(Math.PI*Math.pow(t,0.72)); }
    return topAt(y)+0.02; };
  function profile(y){
    const hw=hwAt(y), bz=bzAt(y), top=topAt(y);
    const kw=Math.min(0.42, hw*0.60);
    const ch=Math.max(bz+0.03, Math.min(bz+0.32, top-0.07));
    const zb=Math.max(bz, Math.min(bz+0.10, ch-0.02));
    return [[0,bz],[kw,bz],[hw*0.90,zb],[hw,ch],[hw*0.985,top]];
  }
  const SEG_MAT=['hull','hull','hull','paint'];
  const SEG_B  =[-0.55,-0.05,0.10,0.18];

  const STATIONS=[-1.48,-1.38,-1.26,-1.14,-0.96,-0.70,-0.40,-0.08,0.24,0.52,0.80,0.90,1.02,1.14,1.26,1.34,1.44,1.54,1.62];

  const BODIES = {
    otter8x8: { key:'otter8x8', label:'Otter 8x8', kind:'amphib_xtv',
      loa:+(G.bowY-G.tailY).toFixed(2), beam:+(G.beam*2).toFixed(2),
      widthOverTires:+((G.wheelX+G.tireW/2)*2).toFixed(2),
      widthOverTracks:+((G.wheelX+G.trackHW)*2).toFixed(2),
      height:+(crownAt(1.16)).toFixed(2), clearance:G.keelZ,
      wheels:8, axles:4, wheelR:G.wheelR, drive:'skid steer', afloat_draft:G.sinkMax },
  };

  const PRESETS = {
    showroom:   { paint:'sage', weather:0.06 },
    bushOlive:  { paint:'sage', weather:0.44 },
    tracked:    { paint:'greyShingle', weather:0.58, tracks:true },
    afloat:     { paint:'sage', weather:0.34, float:1 },
    harbourHaul:{ paint:'rustOrange', weather:0.30, bimini:true, screen:true },
  };
  const CUES = {
    roll:   (t)=>({ roll:t }),
    spin:   (t)=>({ rollL:t*0.2461, rollR:-t*0.2461, yaw:t*45 }),
    launch: (t)=>({ float:t, roll:t*0.6 }),
    hatch:  (t)=>({ hatch:t }),
    bob:    (t)=>({ float:0.86+0.12*Math.sin(t*Math.PI*2), roll:t*0.35 }),
  };

  function resolve(opts){
    opts=opts||{};
    const g=(k,d)=> opts[k]!=null?opts[k]:d;
    const c01=(v)=>Math.max(0,Math.min(1,v));
    return {
      body:'otter8x8', B:BODIES.otter8x8,
      paint: opts.paint||'sage', weather:g('weather',0.34),
      roll:g('roll',0), rollL:g('rollL',0), rollR:g('rollR',0),
      yaw:Math.max(-45,Math.min(45,g('yaw',0))),
      float:c01(g('float',0)), hatch:c01(g('hatch',0)),
      tracks:g('tracks',false), rack:g('rack',true),
      winch:g('winch',true), screen:g('screen',false), bimini:g('bimini',false),
      night:!!opts.night, outline: opts.outline!=null?!!opts.outline:KEYLINE_DEFAULT };
  }
  function dims(opts){ const s=resolve(opts); return Object.assign({}, s.B); }

  const rotX=(p,hy,hz,ca,sa)=>{ const dy=p[1]-hy, dz=p[2]-hz; return [p[0], hy+dy*ca-dz*sa, hz+dy*sa+dz*ca]; };
  function part(out, fn, xf){ const T=[]; fn(T); if(xf) for(const f of T) f.v=f.v.map(xf); for(const f of T) out.push(f); }

  // loft a station pair, right side + mirrored left
  function loftPair(out, y0, p0, y1, p1, matFor, bFor, tex){
    const n=Math.min(p0.length,p1.length);
    for(let k=0;k+1<n;k++){
      const a0=[p0[k][0],y0,p0[k][1]], b0=[p1[k][0],y1,p1[k][1]];
      const b1=[p1[k+1][0],y1,p1[k+1][1]], a1=[p0[k+1][0],y0,p0[k+1][1]];
      const mat=matFor(k), bb=bFor(k);
      const uv=tex?[[y0,p0[k][1]],[y1,p1[k][1]],[y1,p1[k+1][1]],[y0,p0[k+1][1]]]:null;
      out.push(F([a0,b0,b1,a1], mat, bb, 0, uv, tex));
      out.push(F([[-b0[0],b0[1],b0[2]],[-a0[0],a0[1],a0[2]],[-a1[0],a1[1],a1[2]],[-b1[0],b1[1],b1[2]]], mat, bb-0.60, 0,
        tex?[[y1,p1[k][1]],[y0,p0[k][1]],[y0,p0[k+1][1]],[y1,p1[k+1][1]]]:null, tex));
    }
  }

  function buildHull(out,s){
    const wear=wearTex(s.weather);
    for(let i=0;i+1<STATIONS.length;i++){
      const y0=STATIONS[i], y1=STATIONS[i+1];
      loftPair(out, y0, profile(y0), y1, profile(y1), (k)=>SEG_MAT[k], (k)=>SEG_B[k], wear);
    }
    // bow cap and transom cap
    const capRing=(y)=>{ const p=profile(y), r=[]; for(let k=p.length-1;k>=0;k--) r.push([p[k][0],y,p[k][1]]);
      for(let k=1;k<p.length;k++) r.push([-p[k][0],y,p[k][1]]); return r; };
    out.push(F(capRing(G.bowY), 'paint', 0.02));
    out.push(F(capRing(G.tailY).slice().reverse(), 'paint', -0.30));
    // rub rail along the chine
    for(let i=0;i+1<STATIONS.length;i++){
      const y0=STATIONS[i], y1=STATIONS[i+1], a=profile(y0), b=profile(y1);
      const q=(sx)=>out.push(F([[sx*a[3][0]*1.012,y0,a[3][1]],[sx*b[3][0]*1.012,y1,b[3][1]],
        [sx*b[3][0]*1.012,y1,b[3][1]+0.055],[sx*a[3][0]*1.012,y0,a[3][1]+0.055]], 'rubber', sx>0?0.16:-0.44));
      q(1); q(-1);
    }
  }

  function buildDecks(out,s){
    const wear=wearTex(s.weather);
    const dp=(y,xin)=>{ const hw=hwAt(y), top=topAt(y), cr=crownAt(y);
      const pts=[[hw*0.985,top],[hw*0.68,cr-0.02]];
      if(xin>0) pts.push([xin,cr]); else { pts.push([hw*0.30,cr]); pts.push([0,cr+0.01]); }
      return pts; };
    const run=(ys)=>{ for(let i=0;i+1<ys.length;i++){ const y0=ys[i], y1=ys[i+1];
      const inHatch = (y0>=G.hatchY[0]-1e-6 && y1<=G.hatchY[1]+1e-6);
      const xin = inHatch?G.hatchHW:0;
      loftPair(out, y0, dp(y0,xin), y1, dp(y1,xin), ()=>'paint', (k)=>k===0?0.26:0.34, wear); } };
    run([G.tailY,-1.38,-1.26,-1.14]);
    run([0.80,0.90,1.02,1.14,1.26,1.34,1.44,1.54,G.bowY]);
    // cockpit gunwale cap
    const cs=STATIONS.filter(y=>y>=G.cockpit[0]-1e-6 && y<=G.cockpit[1]+1e-6);
    const gp=(y)=>{ const hw=hwAt(y), top=topAt(y); return [[hw*0.985,top],[hw*0.985-0.085,top-0.025]]; };
    for(let i=0;i+1<cs.length;i++) loftPair(out, cs[i], gp(cs[i]), cs[i+1], gp(cs[i+1]), ()=>'paint', ()=>0.30, wear);
    // hatch recess + engine bay
    const cr=crownAt(1.12), fz=cr-0.26;
    wallX(out,  G.hatchHW, G.hatchY[0], G.hatchY[1], fz, cr, 'shade', -0.85, -1);
    wallX(out, -G.hatchHW, G.hatchY[0], G.hatchY[1], fz, cr, 'shade', -0.85, +1);
    wallY(out, G.hatchY[0]+0.005, -G.hatchHW, G.hatchHW, fz, cr, 'shade', -0.85, +1);
    wallY(out, G.hatchY[1]-0.005, -G.hatchHW, G.hatchHW, fz, cr, 'shade', -0.85, -1);
    slab(out, [[-G.hatchHW,G.hatchY[0]],[G.hatchHW,G.hatchY[0]],[G.hatchHW,G.hatchY[1]],[-G.hatchHW,G.hatchY[1]]], fz, 'shade', -0.6);
    boxAt(out, -0.24,0.24, 0.96, 1.24, fz, fz+0.17, 'iron', -0.30);
    boxAt(out, -0.13,0.13, 1.02, 1.16, fz+0.17, fz+0.23, 'galv', -0.10);
    boxAt(out, -0.30,-0.16, 1.24, 1.32, fz, fz+0.13, 'rubber', -0.25);
  }

  function buildHatch(out,s){
    const a=s.hatch*52*DEG, ca=Math.cos(a), sa=Math.sin(a);
    const hy0=G.hatchY[0]-0.02, hy1=G.hatchY[1];
    part(out,(T)=>{
      const ys=[hy0,1.02,1.14,1.26,hy1], vent=ventTex();
      for(let i=0;i+1<ys.length;i++){
        const y0=ys[i], y1=ys[i+1], z0=crownAt(y0)+0.012, z1=crownAt(y1)+0.012;
        quad(T,[-G.hatchHW,y0,z0],[G.hatchHW,y0,z0],[G.hatchHW,y1,z1],[-G.hatchHW,y1,z1],'paint',0.36,0,
          [[-G.hatchHW,y0],[G.hatchHW,y0],[G.hatchHW,y1],[-G.hatchHW,y1]], vent);
        quad(T,[G.hatchHW,y0,z0-0.03],[-G.hatchHW,y0,z0-0.03],[-G.hatchHW,y1,z1-0.03],[G.hatchHW,y1,z1-0.03],'leaf',-0.85);
      }
      wallX(T, G.hatchHW, hy0,hy1, crownAt(1.12)-0.02, crownAt(1.12)+0.012,'paint',0.14,+1);
      wallX(T,-G.hatchHW, hy0,hy1, crownAt(1.12)-0.02, crownAt(1.12)+0.012,'paint',-0.46,-1);
    }, (p)=>rotX(p, hy0, crownAt(hy0)+0.012, ca, sa));
  }

  function buildTub(out,s){
    const y0=G.cockpit[0]+0.04, y1=G.cockpit[1]-0.06, hx=0.53, fz=G.floorZ;
    slab(out, [[-hx,y0],[hx,y0],[hx,y1],[-hx,y1]], fz, 'mesh', -0.30, meshTex());
    wallX(out,  hx, y0,y1, fz, G.railZ-0.02, 'liner', -0.30, -1);
    wallX(out, -hx, y0,y1, fz, G.railZ-0.02, 'liner', -0.30, +1);
    wallY(out, y0, -hx,hx, fz, G.railZ-0.02, 'liner', -0.26, +1);
    wallY(out, y1, -hx,hx, fz, G.railZ-0.02, 'liner', -0.26, -1);
    // side vent/mesh panels on the topsides
    for(const sx of [-1,1]){
      const yA=0.06, yB=0.66, hwA=hwAt(yA)*1.014;
      wallX(out, sx*hwA, yA, yB, 0.60, 0.79, 'mesh', sx>0?0.06:-0.52, sx, [[yA,0],[yB,0],[yB,1],[yA,1]], meshTex());
      const yC=-1.02, yD=-0.52, hwC=hwAt(yC)*1.014;
      wallX(out, sx*hwC, yC, yD, 0.60, 0.79, 'mesh', sx>0?0.06:-0.52, sx, [[yC,0],[yD,0],[yD,1],[yC,1]], meshTex());
    }
  }

  function buildSeats(out,s){
    // front bench, on a moulded pedestal off the tub floor
    boxAt(out, -0.44,0.44, 0.14, 0.34, G.floorZ, 0.60, 'liner', -0.20);
    boxAt(out, -0.47,0.47, 0.14, 0.58, 0.60, 0.76, 'cloth', -0.30);
    boxAt(out, -0.47,0.47, 0.00, 0.16, 0.76, 1.16, 'cloth', -0.40);
    // aft bench, raised on the engine/tank box that closes the back of the tub
    boxAt(out, -0.50,0.50, -1.10,-0.58, G.floorZ, 0.74, 'liner', -0.15);
    boxAt(out, -0.49,0.49, -1.00,-0.60, 0.74, 0.90, 'cloth', -0.28);
    boxAt(out, -0.49,0.49, -1.10,-0.98, 0.90, 1.28, 'cloth', -0.38);
    for(const sx of [-1,1]) bar(out,[sx*0.44,-1.04,0.74],[sx*0.44,-1.04,0.90],0.022,'iron',-0.3);
  }

  function buildHelm(out,s){
    boxAt(out, -0.34,0.34, 0.62, 0.78, 0.66, 0.94, 'dash', -0.35);   // console
    tube(out, [0,0.70,0.94],[0,0.70,1.02], 0.045, 8, 'iron', -0.2);  // stem
    tube(out, [-0.30,0.66,1.03],[0.30,0.66,1.03], 0.028, 8, 'iron', 0.05);
    for(const sx of [-1,1]) tube(out,[sx*0.20,0.66,1.03],[sx*0.30,0.66,1.03],0.038,8,'rubber',-0.15);
    boxAt(out, -0.13,0.13, 0.63, 0.70, 0.94, 1.00, 'dash', 0.1, false);
    if(s.screen){
      quad(out,[0.42,0.80,0.88],[-0.42,0.80,0.88],[-0.40,0.70,1.36],[0.40,0.70,1.36],'glass',-0.12);
      for(const sx of [-1,1]) bar(out,[sx*0.42,0.80,0.88],[sx*0.40,0.70,1.36],0.022,'galv',0.1);
      bar(out,[-0.40,0.70,1.36],[0.40,0.70,1.36],0.022,'galv',0.2);
    }
    if(s.bimini){
      for(const sy of [0.56,-1.02]) for(const sx of [-1,1])
        bar(out,[sx*0.50,sy,0.84],[sx*0.52,sy,1.60],0.026,'iron',0.0);
      const roof=(zc,zo,mat,b)=>{
        quad(out,[0.52,-1.08,zo],[0,-1.08,zc],[0,0.62,zc],[0.52,0.62,zo],mat,b);
        quad(out,[0,-1.08,zc],[-0.52,-1.08,zo],[-0.52,0.62,zo],[0,0.62,zc],mat,b);
      };
      roof(1.68,1.60,'canvas',0.34);
      quad(out,[-0.52,-1.08,1.57],[0.52,-1.08,1.57],[0.52,0.62,1.57],[-0.52,0.62,1.57],'canvas',-0.55);
      for(const sx of [-1,1]) wallX(out, sx*0.52, -1.08, 0.62, 1.57, 1.60, 'canvas', sx>0?0.16:-0.44, sx);
      wallY(out, 0.62, -0.52, 0.52, 1.57, 1.60, 'canvas', 0.10, +1);
      wallY(out,-1.08, -0.52, 0.52, 1.57, 1.60, 'canvas', -0.30, -1);
    }
  }

  function buildFittings(out,s){
    // headlamps set into the bow face, just under the deck edge
    for(const sx of [-1,1]){
      tube(out,[sx*0.30,1.28,0.74],[sx*0.30,1.60,0.74],0.095,10, s.night?'glow':'head', 0.30, true);
      tube(out,[sx*0.30,1.26,0.74],[sx*0.30,1.30,0.74],0.115,10,'chrome',0.20,false);
    }
    // amber deck markers + a red pair at the transom
    for(const sx of [-1,1]){
      boxAt(out, Math.min(sx*0.50,sx*0.58), Math.max(sx*0.50,sx*0.58), 1.16, 1.24, crownAt(1.20)-0.06, crownAt(1.20), 'lensA', 0.30);
      boxAt(out, Math.min(sx*0.30,sx*0.40), Math.max(sx*0.30,sx*0.40), G.tailY-0.01, G.tailY+0.04, 0.68, 0.78, 'lensR', 0.25);
    }
    if(s.winch){
      boxAt(out, -0.21,0.21, 1.36, 1.50, crownAt(1.43), crownAt(1.43)+0.05, 'iron', -0.15);
      tube(out, [-0.15,1.43,crownAt(1.43)+0.10],[0.15,1.43,crownAt(1.43)+0.10], 0.055, 10, 'iron', 0.10);
      tube(out, [-0.17,1.43,crownAt(1.43)+0.10],[-0.21,1.43,crownAt(1.43)+0.10], 0.038, 8, 'galv', -0.05);
      // bow guard hoop
      bar(out,[-0.28,1.50,0.62],[-0.28,1.58,0.88],0.026,'iron',0.05);
      bar(out,[ 0.28,1.50,0.62],[ 0.28,1.58,0.88],0.026,'iron',0.05);
      bar(out,[-0.28,1.58,0.88],[0.28,1.58,0.88],0.026,'iron',0.15);
    }
    if(s.rack){
      for(const sx of [-1,1]){
        bar(out,[sx*0.44,-1.06,0.84],[sx*0.46,-1.10,1.30],0.026,'iron',0.05);
        bar(out,[sx*0.46,-1.42,1.00],[sx*0.46,-1.42,1.30],0.026,'iron',0.05);
        bar(out,[sx*0.46,-1.10,1.30],[sx*0.46,-1.42,1.30],0.024,'iron',0.20);
      }
      for(const sx of [-0.30,0,0.30]) bar(out,[sx,-1.10,1.29],[sx,-1.42,1.29],0.020,'iron',0.16);
      bar(out,[-0.46,-1.42,1.30],[0.46,-1.42,1.30],0.024,'iron',0.20);
      bar(out,[-0.46,-1.10,1.30],[0.46,-1.10,1.30],0.024,'iron',0.20);
    }
  }

  // ---- wheels & tracks (the rolling group) ----
  function wheelAt(out, xc, yc, sxOut, roll, dim){
    const r=G.wheelR, w=G.tireW, ph=roll*2*Math.PI;
    tube(out,[xc-w/2,yc,r],[xc+w/2,yc,r], r, 14, 'rubber', dim?-0.5:-0.05, true, lugTex(roll*2*Math.PI*r));
    const xf=xc+sxOut*(w/2+0.012);
    if(dim){ tube(out,[xc+sxOut*(w/2-0.03),yc,r],[xf,yc,r], r*0.52, 12, 'rubber', -0.45); return; }
    tube(out,[xc+sxOut*(w/2-0.03),yc,r],[xf,yc,r], r*0.52, 12, 'alloy', 0.22);
    for(let k=0;k<5;k++){ const th=ph+k*2*Math.PI/5, py=yc+Math.cos(th)*0.115, pz=r+Math.sin(th)*0.115;
      tube(out,[xf-sxOut*0.004,py,pz],[xf+sxOut*0.022,py,pz],0.024,6,'galv',0.35); }
    { const th=ph+Math.PI/3, py=yc+Math.cos(th)*0.215, pz=r+Math.sin(th)*0.215;
      tube(out,[xf,py,pz],[xf+sxOut*0.016,py,pz],0.026,5,'iron',-0.45); }
    tube(out,[xf,yc,r],[xf+sxOut*0.03,yc,r],0.055,8,'chrome',0.35);
  }
  function buildWheels(out,s){
    for(const y of G.axY) tube(out,[-0.50,y,G.wheelR],[0.50,y,G.wheelR],0.05,8,'iron',-0.25);
    for(const y of G.axY){
      wheelAt(out,  G.wheelX, y, +1, s.roll+s.rollR, s.tracks);
      wheelAt(out, -G.wheelX, y, -1, s.roll+s.rollL, s.tracks);
    }
  }
  // belt path: a stadium loop round the end axles, sampled bow-over-top-to-stern
  function beltPath(n){
    const R=G.trackR, yF=G.axY[0], yR=G.axY[3], zc=G.wheelR, pts=[];
    const straight=yF-yR, arc=Math.PI*R, total=2*straight+2*arc;
    for(let i=0;i<n;i++){
      const d=(i/n)*total; let y,z,nx,nz;
      if(d<straight){ y=yF-d; z=zc+R; nz=1; nx=0; }
      else if(d<straight+arc){ const a=(d-straight)/R; y=yR-Math.sin(a)*R; z=zc+Math.cos(a)*R; nz=Math.cos(a); nx=-Math.sin(a); }
      else if(d<2*straight+arc){ const t=d-straight-arc; y=yR+t; z=zc-R; nz=-1; nx=0; }
      else { const a=(d-2*straight-arc)/R; y=yF+Math.sin(a)*R; z=zc-Math.cos(a)*R; nz=-Math.cos(a); nx=Math.sin(a); }
      pts.push({y,z,ny:nx,nz, d});
    }
    return { pts, total };
  }
  function buildTracks(out,s){
    if(!s.tracks) return;
    const N=48, {pts,total}=beltPath(N), TH=0.085;
    for(const sx of [-1,1]){
      const x0=sx>0?G.wheelX-G.trackHW:-(G.wheelX+G.trackHW);
      const x1=sx>0?G.wheelX+G.trackHW:-(G.wheelX-G.trackHW);
      const phase=((sx>0?s.roll+s.rollR:s.roll+s.rollL)*2*Math.PI*G.wheelR);
      for(let i=0;i<N;i++){
        const a=pts[i], b=pts[(i+1)%N];
        const ao=[a.y+a.ny*0, a.z], bo=[b.y+b.ny*0, b.z];
        // outer tread strip
        out.push(F([[x0,ao[0],ao[1]],[x0,bo[0],bo[1]],[x1,bo[0],bo[1]],[x1,ao[0],ao[1]]].map(p=>[p[0],p[1],p[2]]),
          'track', 0.08, 0, [[a.d,0],[b.d,0],[b.d,1],[a.d,1]], lugTex(phase)));
        // inner face of the belt
        const ai=[a.y-a.ny*TH, a.z-a.nz*TH], bi=[b.y-b.ny*TH, b.z-b.nz*TH];
        out.push(F([[x1,ai[0],ai[1]],[x1,bi[0],bi[1]],[x0,bi[0],bi[1]],[x0,ai[0],ai[1]]],'track',-0.75));
        // the two ring sides
        out.push(F([[x1,ao[0],ao[1]],[x1,bo[0],bo[1]],[x1,bi[0],bi[1]],[x1,ai[0],ai[1]]],'track',sx>0?0.10:-0.46));
        out.push(F([[x0,bo[0],bo[1]],[x0,ao[0],ao[1]],[x0,ai[0],ai[1]],[x0,bi[0],bi[1]]],'track',sx>0?-0.46:0.10));
      }
    }
  }

  function build(s){
    const body=[], rolling=[];
    buildHull(body,s); buildDecks(body,s); buildHatch(body,s);
    buildTub(body,s); buildSeats(body,s); buildHelm(body,s); buildFittings(body,s);
    buildWheels(rolling,s); buildTracks(rolling,s);
    const all=body.concat(rolling);
    // tracks stand the machine up on the belt; float settles it onto its waterline.
    // Nothing is clipped either way — the game's water shader owns the cut.
    const dz=(s.tracks?G.trackLift:0) - G.sinkMax*s.float;
    if(dz) for(const f of all) f.v=f.v.map(p=>[p[0],p[1],p[2]+dz]);
    return all;
  }

  // ---- materials ----
  function makeMats(s){
    const wx=s.weather, night=s.night;
    const grime=r=>r.map(c=>mix(desat(c,wx*0.24),'#3a3128',wx*0.12));
    const rust =r=>r.map(c=>mix(c,'#6d3417',wx*0.26));
    const cool =r=>night?r.map(c=>mix(desat(c,0.30),'#1b2740',0.40)):r;
    const t=r=>cool(grime(r)), tm=r=>cool(rust(grime(r)));
    const paintRamp=BODY[s.paint]||BODY.sage;
    return {
      paint :{ ramp:t(paintRamp), polish:0.15 },
      hull  :{ ramp:t(POLY) },
      trim  :{ ramp:t(TRIM) },
      leaf  :{ ramp:cool(TRIM.map(c=>mix(desat(c,0.34),'#4a4f52',0.40))) },
      liner :{ ramp:t((BODY[s.paint]||BODY.sage).map(c=>mix(desat(c,0.28),'#2b3130',0.30))) },
      // #558 — MAT is merged into MESH: one dark-rubber ramp, not two. The cockpit mat was RUBBER
      // mixed 30% to #24292d, the screens 35% to #20262b — the same ramp to within 3/255 at the
      // top step and 0/255 at the bottom. The sole slab takes MESH; the screens are untouched.
      mesh  :{ ramp:cool(RUBBER.map(c=>mix(c,'#20262b',0.35))) },
      dash  :{ ramp:cool(RUBBER.map(c=>mix(c,'#4a4f52',0.20))) },
      cloth :{ ramp:cool(CLOTH) }, shade:{ ramp:cool(SHADE) },
      canvas:{ ramp:t((BODY[s.paint]||BODY.sage).map(c=>mix(desat(c,0.44),'#8f9384',0.34))) },
      iron  :{ ramp:tm(IRON) }, galv:{ ramp:tm(GALV) },
      chrome:{ ramp:t(CHROME) }, alloy:{ ramp:t(CHROME.map(c=>desat(c,0.15))) },
      rubber:{ ramp:t(RUBBER) }, track:{ ramp:t(RUBBER.map(c=>mix(c,'#15181c',0.35))) },
      lensR :{ ramp:LENSR }, lensA:{ ramp:LENSA },
      head  :{ ramp:GLASSD.map(c=>mix(c,'#dfe6e2',0.35)) },
      glass :{ ramp:night?GLASSN:GLASSD },
      glow  :{ ramp:night?GLOW:['#5f6a5e','#8d9a8b','#b6c2b0','#d3ddcb'] },
    };
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
          if(deff<zbuf[i]){
            let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi+=tex(uu,vv); }
            zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat;
            let idx; if(flat){ idx=Math.round(fi); } else { const base=Math.floor(fi); idx=base+((fi-base)>BAYER[x&3][y&3]?1:0); }
            idx=Math.max(0,Math.min(ramp.length-1,idx)); rbuf[i]=ramp; ibuf[i]=idx; } }
      }
    }
    return { rbuf, ibuf, nbuf, dep };
  }
  function post(bufs, s){
    const { rbuf, ibuf, nbuf, dep }=bufs, N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx,ny=y+dy; if(nx>=W||ny>=H) continue; const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j; out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; } } }
    if(s.weather>0.02){ const rnd=mulberry32(9021);
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='paint'||m==='galv'||m==='iron'||m==='hull'||m==='rubber') && rnd()<s.weather*0.05)
          out[i]=rbuf[i][Math.max(0,ibuf[i]-1)]; } }
    if(s.night){ for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x;
      if(nbuf[i]!=='glow' && nbuf[i]!=='glass') continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*W+(x+dx);
        if(out[j] && nbuf[j]!=='glow' && nbuf[j]!=='glass') out[j]=mix(out[j],'#f2c25e',nbuf[i]==='glow'?0.30:0.14); } } }
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

  function render(dir, opts){ opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const s=resolve(opts), B=camBasis({dir,elev:opts.elev,yaw:s.yaw});
    return toRGBA(post(paint(build(s), B, makeMats(s), s), s));
  }
  function frames(dir, n, opts, cue){ n=n||8; const fn=CUES[cue||'roll']||CUES.roll, out=[];
    const cyclic = (cue==='roll'||cue==='spin'||cue==='bob');
    for(let i=0;i<n;i++){ const t = cyclic ? i/n : i/(n-1);
      out.push(render(dir, Object.assign({}, opts, fn(t)))); }
    return out;
  }
  function project(dir, p, elev, yaw){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev,yaw})); return {x:v.sx,y:v.sy}; }
  function anchors(dir, opts){ opts=opts||{}; const s=resolve(opts), e=opts.elev, dz=-G.sinkMax*s.float;
    const P=(p)=>{ const q=project(dir,[p[0],p[1],p[2]+dz],e,s.yaw); return { x:q.x, y:q.y, m:p }; };
    return {
      helm:P([0,0.66,1.03]), benchF:P([0,0.30,0.74]), benchR:P([0,-0.69,0.88]),
      hatch:P([0,1.12,crownAt(1.12)]), winch:P([0,1.38,crownAt(1.38)+0.14]),
      rack:P([0,-1.26,1.30]), transom:P([0,G.tailY,0.80]), bow:P([0,G.bowY,topAt(G.bowY)]),
      wheelFL:P([-G.wheelX,G.axY[0],G.wheelR]), wheelFR:P([G.wheelX,G.axY[0],G.wheelR]),
      wheelRL:P([-G.wheelX,G.axY[3],G.wheelR]), wheelRR:P([G.wheelX,G.axY[3],G.wheelR]),
      waterline:P([0,0,G.sinkMax*s.float]),
      loa:s.B.loa, beam:s.B.beam, height:s.B.height, wheels:8,
    };
  }
  function list(){ return Object.keys(BODIES); }

  root.AmphibIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    BODY, POLY, TRIM, IRON, GALV, RUBBER, CHROME, CLOTH, GLASSD, GLASSN, KEY,
    BODIES, PRESETS, CUES, G, hwAt, bzAt, topAt, crownAt,
    list, dims, resolve, render, frames, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);

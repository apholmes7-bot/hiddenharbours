/* Hidden Harbours — parametric ISO ALUMINIUM CAMPER rig (SAME turntable + camera + shading as
   houseIsoRig.js / utilityIsoRig.js / interiorIsoRig.js / the fleet). A riveted-aluminium monocoque
   travel trailer, parked as a dwelling. ONE lofted 3D body, baked to pixels through the SHARED 3/4
   camera: 45deg steps, elev 40deg, flat-facet shading from the fixed key, z-buffered, ordered dither,
   per-face uv texture, depth-edge darkening, NO AA. 32 px = 1 m, true to the fleet.

   ADR 0031 — RINGLESS FROM BIRTH. KEYLINE_DEFAULT = false; render(dir,{outline:true}) is kept as a
   live A/B only, never as the shipping bake.

   TWO VARIANTS, one loft:
     bantam   16 ft · 4.34 m body · single axle · one-room, rear berth
     clipper  26 ft · 7.16 m body · single axle · rear berth + head + galley + front lounge

   THE BODY IS A LOFT, NOT A BOX. Every station is a super-ellipse ring (rounder over the crown,
   flatter under the belly); the plan half-width and the crown height roll off over noseRun/tailRun
   into a rounded dome at each end. Panel seams and their rivet lines are a UV TEXTURE on the skin,
   not geometry, so the seams converge into the nose exactly as the real panels do and the ring stays
   cheap. Windows, the door and the wheel arches are CLASSIFIED OUT of that ring — a window is the
   same lofted quads re-materialled and pushed 3.5% toward the section centre, so glass curves with
   the body and can never z-fight it.

   ORIGIN / PIVOT: ground-centre of the BODY footprint (the tongue hangs off +y beyond it), so
   CamperIso.render(dir,…) drops straight onto a terrain point like the house and the room rigs.
   Axes: +x curb side (the door side), +y bow / hitch, +z up. Bow-up authoring, as the fleet.

   DOOR SWING — the enter cue. swing 0..1 drives ONE animation: the leaf (hinged on its forward
   edge) rotates 0 -> 104 deg about a vertical axis while the two-tread step unfolds under the
   threshold over the back half of the run. frames(dir,n,opts) bakes the whole cue as a strip; the
   engine plays it forward on enter, reversed on exit. Everything else on this rig is static.

   PAINT: 'bare' is the polished skin — a 9-step alum ramp plus a POLISH term (+z normals take the
   sky, -z normals take the ground) which is what makes a curved aluminium tube read as metal rather
   than as a grey pipe. Any BODY ramp repaints it (a camper becomes a canteen, a shop, a bait van);
   painted skins drop the polish to a matte 0.22. Weather greys the paint, dulls the polish and
   pits the panels. No colours are invented — the ramps are the harbour's.

   GAMEPLAY: gameplayGeometry({variant}) generates the sidecar (walkable floor, threshold + swing
   arc, step risers, fit-out obstructions with heights, hookups) from the SAME numbers the bake uses,
   so it cannot drift from the art. Art/_sidecarExport.js stamps derivedFromRigSha256 at save time.

   Exposes globalThis.CamperIso = { W,H,PX,DIRS,pivot,order,defaultElev, SKIN,BODY,TRIM,IRON,GALV,
     RUBBER,CHROME,CANVAS,WOOD,GLASSD,GLASSN,KEY, VARIANTS,PRESETS,LAYOUT, list(), dims(opts),
     resolve(opts), render(dir,opts), frames(dir,n,opts), anchors(dir,opts), project(dir,p,elev),
     gameplayGeometry(opts) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 384, H = 320, cx = 192, groundY = 214;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;                    // ADR 0031

  // ---- ramps (harbour master ramps; the skin is the only new one) ----
  const SKIN = ['#2f3438','#3d4449','#4c5459','#5d666c','#707a80','#848e95','#9aa3a9','#b2babe','#ccd2d4'];
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
  const TRIM   = ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec']; // frames, jambs, liner
  const IRON   = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970']; // chassis
  const GALV   = ['#565b5f','#6d7276','#868b8f','#a0a5a8','#bbbfc1','#d6d9da']; // jacks, coupler, bottles
  const RUBBER = ['#121417','#191c20','#22262b','#2c3137','#383e45','#464d55'];
  const CHROME = ['#4a5157','#5f696f','#7b858c','#98a2a8','#b6bec2','#d6dbdd'];
  const CANVAS = ['#4a4536','#5f5943','#786f52','#8f8663','#a79c77','#bfb28c']; // awning duck
  const WOOD   = ['#553d23','#6b4e30','#7f603b','#957749','#ab8d5f','#c1a578']; // interior sole
  const SHADE  = ['#0b0e11','#0f1418','#141a1f','#1a2128','#212a31','#28323a']; // seen-in interior
  const GLASSD = ['#1b262b','#243238','#2f4149','#3d545c','#5d7b82','#96b6ba'];
  const GLASSN = ['#141d2b','#1d2a3d','#2a3c53','#3d5570','#6b7f9c','#95a8c0'];
  const GLOW   = ['#7a5a18','#c09a2c','#efd06a','#fdf0b6'];
  const LENSR  = ['#3a0c0a','#5a120e','#7d1c14','#a52a1d','#c93c2a','#e4573f'];
  const LENSA  = ['#4a2c07','#6d420b','#8f5a12','#b0771f','#cc9633','#e5b455'];
  const KEY    = '#1a1c22';

  // ---- shading (identical recipe to utilityIsoRig / interiorPropRig) ----
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

  // ---- face builders (outward-normal winding) ----
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }
  function quad(out,a,b,c,d,mat,bi,db,uv,tex,flat){ out.push(F([a,b,c,d],mat,bi,db,uv,tex,flat)); }
  function slab(out, pts, z, mat, b, tex){ const uv=tex?pts.map(p=>[p[0],p[1]]):null;
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0, uv, tex)); }
  function boxAt(out, x0,x1,y0,y1,z0,z1, mat, b, noTop, tex){
    b=b||0;
    quad(out,[x0,y0,z0],[x1,y0,z0],[x1,y0,z1],[x0,y0,z1],mat,b-0.30,0,null,tex);          // -y
    quad(out,[x1,y1,z0],[x0,y1,z0],[x0,y1,z1],[x1,y1,z1],mat,b+0.10,0,null,tex);          // +y
    quad(out,[x1,y0,z0],[x1,y1,z0],[x1,y1,z1],[x1,y0,z1],mat,b+0.18,0,null,tex);          // +x
    quad(out,[x0,y1,z0],[x0,y0,z0],[x0,y0,z1],[x0,y1,z1],mat,b-0.42,0,null,tex);          // -x
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
  // Skin panel seams. u = arc position around the ring (m), v = y along the body (m).
  // Longitudinal seams every `belt` of arc, circumferential every `pan` of length; both dotted at
  // 0.105 m so the seam reads as a rivet line rather than a drawn stroke.
  function skinTex(pan, belt, wear){
    return (u,v)=>{
      const fu=((u%belt)+belt)%belt, fv=((v%pan)+pan)%pan;
      const onU = fu<0.040, onV = fv<0.050;
      if(onU||onV){ const t=onV?u:v; const d=((t%0.105)+0.105)%0.105; return d<0.048?-2:-1; }
      if(wear>0.03 && hash2(Math.floor(u*7.3)|0, Math.floor(v*7.3)|0) < wear*0.10) return -1;
      return 0;
    };
  }
  function stripeTex(p){ p=p||0.44; return (u,v)=>{ const f=((v%p)+p)%p; return f<p*0.30?-1:0; }; }
  function grainTex(p){ p=p||0.26; return (u,v)=>{ const f=((v%p)+p)%p; if(f<0.03) return -2;
    return hash2(Math.floor(v/p)|0, Math.floor(u*3)|0)<0.5?0:-1; }; }
  function treadTex(){ const c=0.07; return (u,v)=>{ const f=((u%c)+c)%c; return f<c*0.45?-1:0; }; }

  // ================= THE LOFT =================
  const NTOP = 3.05, NBOT = 3.70;    // super-ellipse exponents: near-flat sides, big crown radius
  function ringPt(hw, zc, hzT, hzB, th){
    const c=Math.cos(th), s=Math.sin(th), p = 2/(s>=0?NTOP:NBOT);
    return [ hw*Math.sign(c)*Math.pow(Math.abs(c),p),
             zc + (s>=0?hzT:hzB)*Math.sign(s)*Math.pow(Math.abs(s),p) ];
  }
  // station profile at u in [0,1]  (y = (u-0.5)*bodyL, +y = bow)
  function prof(V,u){
    const y=(u-0.5)*V.bodyL, halfL=V.bodyL/2, dEnd=halfL-Math.abs(y);
    const run = y>0?V.noseRun:V.tailRun, drop = y>0?V.noseDrop:V.tailDrop;
    const t = dEnd>=run ? 0 : (1 - dEnd/run);
    const g = Math.sqrt(Math.max(0,1-t*t));
    return { y,
      hw : V.halfW*(0.085+0.915*g),
      zc : V.beltZ,
      hzT: (V.topZ-V.beltZ)*(1-drop*t*t)*(0.60+0.40*Math.sqrt(Math.max(0,1-t*t*0.95))),
      hzB: (V.beltZ-V.botZ)*(1-0.22*t*t) };
  }

  const VARIANTS = {
    bantam: { key:'bantam', label:'Bantam 16', ft:16, bodyL:4.34, halfW:0.99, beltZ:1.30, topZ:2.58,
      botZ:0.455, noseRun:0.92, tailRun:0.78, noseDrop:0.26, tailDrop:0.20, floorZ:0.68,
      axleY:-0.34, tongue:1.24, panel:0.98, vents:2, acPod:false, rack:false,
      door:{ y0:-0.16, y1:0.52, z1:2.38 },
      win:[ {kind:'side',side: 1,y0:-1.62,y1:-0.44,z0:1.52,z1:2.14},
            {kind:'side',side:-1,y0:-1.10,y1: 0.46,z0:1.52,z1:2.14},
            {kind:'end', end:  1,at: 1.00,z0:1.58,z1:2.14,xmax:0.84},
            {kind:'end', end: -1,at:-1.24,z0:1.54,z1:2.08,xmax:0.72} ] },
    clipper:{ key:'clipper',label:'Clipper 26', ft:26, bodyL:7.16, halfW:1.10, beltZ:1.36, topZ:2.80,
      botZ:0.470, noseRun:1.10, tailRun:0.88, noseDrop:0.28, tailDrop:0.21, floorZ:0.70,
      axleY:-0.62, tongue:1.42, panel:1.06, vents:3, acPod:true, rack:true,
      door:{ y0:-0.28, y1:0.44, z1:2.42 },
      win:[ {kind:'side',side: 1,y0:-2.86,y1:-1.62,z0:1.62,z1:2.26},
            {kind:'side',side: 1,y0: 1.06,y1: 2.42,z0:1.62,z1:2.26},
            {kind:'side',side:-1,y0:-0.52,y1: 1.62,z0:1.62,z1:2.26},
            {kind:'side',side:-1,y0:-2.86,y1:-1.74,z0:1.62,z1:2.26},
            {kind:'end', end:  1,at: 2.42,z0:1.68,z1:2.28,xmax:0.92},
            {kind:'end', end: -1,at:-2.66,z0:1.64,z1:2.20,xmax:0.84} ] },
  };

  // Fit-out. ONE source of truth: the dark volumes you see through the open door, and the
  // obstruction footprints + heights the sidecar carries. Heights are ABOVE THE SOLE.
  const LAYOUT = {
    bantam: [
      { id:'rear_berth', kind:'berth',   x0:-0.95,x1:0.95, y0:-1.94,y1:-0.86, h:0.52, note:'transverse double, lift-up storage under' },
      { id:'gaucho',     kind:'seat',    x0:-0.95,x1:-0.30,y0:-0.80,y1: 0.30, h:0.42, note:'street-side gaucho, folds to a single' },
      { id:'table',      kind:'table',   x0:-0.95,x1:-0.16,y0:-0.34,y1: 0.06, h:0.72, note:'drop-leaf on a single post — step-under at 0.72' },
      { id:'galley',     kind:'counter', x0:-0.95,x1:-0.36,y0: 0.36,y1: 1.30, h:0.88, note:'two-burner, sink, ice box below' },
      { id:'wardrobe',   kind:'tall',    x0: 0.26,x1: 0.82,y0: 0.72,y1: 1.24, h:1.30, note:'full-height locker — a wall, not a step-over' },
    ],
    clipper: [
      { id:'rear_berth', kind:'berth',   x0:-1.02,x1:1.02, y0:-3.06,y1:-1.88, h:0.52, note:'transverse double across the tail' },
      { id:'wardrobe',   kind:'tall',    x0:-1.02,x1:-0.42,y0:-1.84,y1:-1.40, h:1.66, note:'full-height' },
      { id:'head',       kind:'room',    x0: 0.30,x1: 1.02,y0:-1.84,y1:-0.96, h:1.90, note:'partitioned head — a room within the room, door on its -y face' },
      { id:'galley',     kind:'counter', x0:-1.02,x1:-0.40,y0:-1.30,y1: 0.10, h:0.88, note:'range, sink, fridge below the counter' },
      { id:'dinette',    kind:'seat',    x0: 0.26,x1: 1.02,y0: 0.62,y1: 1.78, h:0.44, note:'facing benches, curb side' },
      { id:'table',      kind:'table',   x0: 0.26,x1: 1.02,y0: 1.02,y1: 1.42, h:0.74, note:'drops to make the second berth' },
      { id:'lounge',     kind:'seat',    x0:-1.02,x1:-0.34,y0: 1.20,y1: 2.60, h:0.44, note:'front gaucho under the panoramic band' },
      { id:'front_locker',kind:'low',    x0:-0.60,x1: 0.60,y0: 2.62,y1: 3.06, h:0.60, note:'nose locker under the front window' },
    ],
  };

  const PRESETS = {
    bantamBare:   { variant:'bantam',  paint:null,   weather:0.30, awning:false, winAwn:false, rack:false },
    bantamPaint:  { variant:'bantam',  paint:'teal', weather:0.24, awning:true,  winAwn:true },
    clipperBare:  { variant:'clipper', paint:null,   weather:0.34, awning:true,  winAwn:false },
    clipperCamp:  { variant:'clipper', paint:null,   weather:0.42, awning:true,  winAwn:true, swing:1 },
    canteen:      { variant:'clipper', paint:'red',  weather:0.20, awning:true,  winAwn:true, rack:false },
  };

  function resolve(opts){
    opts=opts||{};
    const V = VARIANTS[opts.variant] || VARIANTS.clipper;
    const g=(k,d)=> opts[k]!=null?opts[k]:d;
    return { V, variant:V.key,
      paint : opts.paint!==undefined?opts.paint:null,     // null = bare polished aluminium
      weather:g('weather',0.32), swing:Math.max(0,Math.min(1,g('swing',0))),
      awning :g('awning', V.key==='clipper'), winAwn:g('winAwn',false),
      vents  :g('vents',true), acPod:g('acPod',V.acPod), rack:g('rack',false),
      propane:g('propane',true), jacks:g('jacks',true), chocks:g('chocks',true),
      hookups:g('hookups',true), step:g('step',true),
      night  :!!opts.night, outline: opts.outline!=null?!!opts.outline:KEYLINE_DEFAULT };
  }
  function dims(opts){ const s=resolve(opts), V=s.V;
    return { variant:V.key, label:V.label, ft:V.ft, bodyL:V.bodyL, loa:+(V.bodyL+V.tongue).toFixed(2),
      width:+(V.halfW*2).toFixed(2), height:+(V.topZ).toFixed(2), floorZ:V.floorZ,
      interiorH:+(V.topZ-0.10-V.floorZ).toFixed(2), axleY:V.axleY }; }

  // ================= BUILD =================
  const NR = 36;                                     // ring segments
  function stations(V){ const n = Math.max(26, Math.round(V.bodyL/0.17));
    const u=[]; for(let i=0;i<=n;i++) u.push(0.5-0.5*Math.cos(Math.PI*i/n)); return u; }

  function classify(V, s, y, z, x){
    const D=V.door;
    if(x>0 && y>=D.y0 && y<=D.y1 && z>=V.floorZ-0.06 && z<=D.z1) return ['door',null];
    const ay=Math.abs(y-V.axleY);
    if(Math.abs(x)>0.52*V.halfW && ((ay/0.62)*(ay/0.62)+((z-0.26)/0.66)*((z-0.26)/0.66))<1) return ['arch',null];
    for(const w of V.win){
      if(w.kind==='side'){
        if(Math.sign(x)===w.side && y>=w.y0 && y<=w.y1 && z>=w.z0 && z<=w.z1) return ['glass',w];
      } else {
        const inEnd = w.end>0 ? y>=w.at : y<=w.at;
        if(inEnd && Math.abs(x)<=w.xmax && z>=w.z0 && z<=w.z1) return ['glass',w];
      }
    }
    return ['skin',null];
  }
  // a quad is FRAME when it straddles the opening's edge — one quad wide, whatever the ring density
  function straddles(V, cls, rect, q){
    const ys=q.map(p=>p[1]), zs=q.map(p=>p[2]), xs=q.map(p=>Math.abs(p[0]));
    const y0=Math.min(...ys), y1=Math.max(...ys), z0=Math.min(...zs), z1=Math.max(...zs);
    if(cls==='door'){ const D=V.door; return y0<D.y0 || y1>D.y1 || z0<V.floorZ-0.06 || z1>D.z1; }
    if(rect.kind==='side') return y0<rect.y0 || y1>rect.y1 || z0<rect.z0 || z1>rect.z1;
    return z0<rect.z0 || z1>rect.z1 || Math.max(...xs)>rect.xmax || (rect.end>0 ? y0<rect.at : y1>rect.at);
  }
  const scaleToCentre=(p,zc,k)=>[p[0]*k, p[1], zc+(p[2]-zc)*k];

  function buildShell(out, s){
    const V=s.V, US=stations(V), tex=skinTex(V.panel, 0.315, s.weather);
    const R=[];                                       // per-station ring cache: pts + arc
    for(const u of US){
      const P=prof(V,u), pts=[], arc=[0];
      for(let j=0;j<=NR;j++){ const th=(j/NR)*Math.PI*2 - Math.PI/2;   // start at the belly keel
        const [x,z]=ringPt(P.hw,P.zc,P.hzT,P.hzB,th); pts.push([x,P.y,z]);
        if(j) arc.push(arc[j-1]+Math.hypot(x-pts[j-1][0], z-pts[j-1][2])); }
      R.push({P,pts,arc});
    }
    // per-vertex normals — a polished tube shaded per FACET quilts; this is the one place the rig
    // interpolates, and only the skin gets it.
    const NM=R.map((_,i)=>{ const a=[];
      for(let j=0;j<=NR;j++){
        const im=Math.max(0,i-1), ip=Math.min(R.length-1,i+1);
        const jp=(j+1>NR)?1:j+1, jm=(j-1<0)?NR-1:j-1;
        const du=sub(R[ip].pts[j], R[im].pts[j]), dv=sub(R[i].pts[jp], R[i].pts[jm]);
        const n=crs(du,dv), m=Math.hypot(n[0],n[1],n[2]);
        a.push(m>1e-6?[n[0]/m,n[1]/m,n[2]/m]:[0,0,1]);
      } return a; });
    const doorQ=[];
    for(let i=0;i<R.length-1;i++){
      const A=R[i], Bs=R[i+1];
      for(let j=0;j<NR;j++){
        const a=A.pts[j], b=Bs.pts[j], c=Bs.pts[j+1], d=A.pts[j+1];
        const mx=(a[0]+b[0]+c[0]+d[0])/4, my=(a[1]+b[1])/2, mz=(a[2]+b[2]+c[2]+d[2])/4;
        const [cls,rect]=classify(V,s,my,mz,mx);
        const uv=[[A.arc[j],a[1]],[Bs.arc[j],b[1]],[Bs.arc[j+1],c[1]],[A.arc[j+1],d[1]]];
        const vn=[NM[i][j],NM[i+1][j],NM[i+1][j+1],NM[i][j+1]];
        if(cls==='door'){ doorQ.push({v:[a,b,c,d],uv,vn,zc:A.P.zc,border:straddles(V,'door',null,[a,b,c,d])}); continue; }
        if(cls==='glass'){
          if(straddles(V,'glass',rect,[a,b,c,d])){ quad(out,a,b,c,d,'frame',0.34,0.03); continue; }
          const k=0.962, zc=A.P.zc;
          quad(out, scaleToCentre(a,zc,k),scaleToCentre(b,zc,k),scaleToCentre(c,zc,k),scaleToCentre(d,zc,k),
               'glass', -0.55, -0.02); continue; }
        if(cls==='arch'){
          const k=0.90, zc=A.P.zc;
          quad(out, scaleToCentre(a,zc,k),scaleToCentre(b,zc,k),scaleToCentre(c,zc,k),scaleToCentre(d,zc,k),
               'shade', -0.6, -0.03); continue; }
        const f=F([a,b,c,d],'skin',0,0,uv,tex); f.vn=vn; out.push(f);
      }
    }
    return { R, doorQ, tex };
  }

  // rotate a point about the vertical hinge axis at (hx,hy) by a radians
  const hinge=(p,hx,hy,ca,sa)=>{ const dx=p[0]-hx, dy=p[1]-hy;
    return [hx+dx*ca-dy*sa, hy+dx*sa+dy*ca, p[2]]; };

  function buildDoor(out, s, sh){
    const V=s.V, D=V.door, a=s.swing*104*DEG, ca=Math.cos(a), sa=Math.sin(a);
    // opening: bright jamb ring. NO backing panel — you look through to the sole and the far liner,
    // which is what makes the doorway read as a way in rather than a decal.
    for(const q of sh.doorQ) if(q.border){ const [p0,p1,p2,p3]=q.v; quad(out,p0,p1,p2,p3,'frame',0.30,0.03); }
    // the leaf — the same lofted patch, hinged on its forward (+y) edge
    const hx = V.halfW*0.86, hy = D.y1;
    const winY0=D.y0+0.16, winY1=D.y1-0.16, winZ0=V.floorZ+0.92, winZ1=V.floorZ+1.34;
    for(const q of sh.doorQ){
      const [p0,p1,p2,p3]=q.v, my=(p0[1]+p1[1])/2, mz=(p0[2]+p2[2])/2, zc=q.zc;
      if(q.border) continue;
      const inWin = my>winY0 && my<winY1 && mz>winZ0 && mz<winZ1;
      const o=[p0,p1,p2,p3].map(p=>hinge(p,hx,hy,ca,sa));
      const iq=[p0,p1,p2,p3].map(p=>hinge(scaleToCentre(p,zc,0.955),hx,hy,ca,sa));
      if(inWin){ quad(out,o[0],o[1],o[2],o[3],'glass',-0.5,0.02); }
      else {
        const f=F(o,'skin',0.10,0.02,q.uv,sh.tex);
        f.vn=q.vn.map(n=>[n[0]*ca-n[1]*sa, n[0]*sa+n[1]*ca, n[2]]);
        out.push(f);
        quad(out,iq[3],iq[2],iq[1],iq[0],'leaf',-0.35,0.02);         // inner face of the leaf, reversed
      }
    }
    // grab handle on the free edge
    const gh=hinge([V.halfW*0.90, D.y0+0.10, V.floorZ+0.80],hx,hy,ca,sa);
    const gh2=hinge([V.halfW*0.90, D.y0+0.10, V.floorZ+0.98],hx,hy,ca,sa);
    tube(out,[gh[0]+0.05*sa,gh[1]-0.05*ca,gh[2]],[gh2[0]+0.05*sa,gh2[1]-0.05*ca,gh2[2]],0.022,6,'chrome',0.5);
  }

  function buildStep(out, s){
    if(!s.step) return;
    const V=s.V, D=V.door, dp=Math.max(0,Math.min(1,(s.swing-0.42)/0.58));  // unfolds over the back half
    const yc=(D.y0+D.y1)/2, xw=V.halfW*0.80;
    const fold=(1-dp)*72*DEG;                          // stowed = tucked up under the sill
    const treads=[{o:0.30,z:V.floorZ-0.24},{o:0.56,z:V.floorZ-0.46}];
    for(const t of treads){
      const r=Math.cos(fold), lift=(1-r)*(t.o*0.86);
      const x0=xw+t.o*r-0.13, x1=xw+t.o*r+0.13, z=t.z+lift;
      boxAt(out, x0,x1, yc-0.30,yc+0.30, z, z+0.035, 'galv', 0.15, false, treadTex());
      for(const sy of [-1,1]) bar(out,[xw-0.02,yc+sy*0.28,V.floorZ-0.05],[x1-0.06,yc+sy*0.28,z],0.018,'iron',-0.1);
    }
    boxAt(out, xw-0.10,xw+0.06, yc-0.32,yc+0.32, V.floorZ-0.10, V.floorZ-0.02, 'galv', 0.1);  // sill plate
  }

  function buildInterior(out, s){
    const V=s.V, US=stations(V);
    // sole — the walkable plane, inset from the skin at floor height (CCW from above)
    const L=[],Rt=[];
    for(const u of US){ const P=prof(V,u); const hwF=interiorHW(V,P); if(hwF<=0.02) continue;
      L.push([-hwF,P.y]); Rt.push([hwF,P.y]); }
    slab(out, Rt.concat(L.reverse()), V.floorZ, 'wood', -0.15, grainTex(0.30));
    // liner on the far (street) side, only where the doorway can see it
    const D=V.door;
    for(let i=0;i<US.length-1;i++){
      const A=prof(V,US[i]), Bp=prof(V,US[i+1]);
      if(A.y < D.y0-1.5 || A.y > D.y1+1.5) continue;
      for(let j=0;j<NR;j++){ const th0=(j/NR)*Math.PI*2-Math.PI/2, th1=((j+1)/NR)*Math.PI*2-Math.PI/2;
        const m=(th0+th1)/2, mc=Math.cos(m);
        if(mc>-0.15) continue;                                    // port half only
        const g=(P,th)=>{ const [x,z]=ringPt(P.hw,P.zc,P.hzT,P.hzB,th); return [x*0.94,P.y,P.zc+(z-P.zc)*0.94]; };
        if(g(A,th0)[2] < V.floorZ) continue;
        quad(out, g(A,th1),g(Bp,th1),g(Bp,th0),g(A,th0), 'liner', -0.9);
      }
    }
    for(const it of fitOut(V)){
      const mat = it.kind==='table'?'wood' : it.kind==='room'?'liner' : it.kind==='berth'?'liner':'wood';
      boxAt(out, it.x0,it.x1, it.y0,it.y1, V.floorZ, V.floorZ+it.h, mat, it.kind==='table'?-0.2:-0.6);
    }
  }
  const profAtY=(V,y)=>prof(V, Math.max(0,Math.min(1,(y/V.bodyL)+0.5)));
  // skin half-width at any height on a station — the one function the fit-out, the sole and the
  // sidecar all measure against, so nothing can be authored outside the shell.
  function skinHW(V,P,z,inset){
    const dz=z-P.zc, h=(dz>=0?P.hzT:P.hzB), n=(dz>=0?NTOP:NBOT);
    if(h<=1e-4) return 0;
    const t=Math.min(1,Math.abs(dz)/h);
    return Math.max(0, P.hw*Math.pow(Math.max(0,1-Math.pow(t,n)),1/n) - (inset||0));
  }
  function interiorHW(V,P){ return skinHW(V,P,V.floorZ,0.055); }
  // LAYOUT clamped INTO the loft: nothing may pierce the skin, and a piece the taper squeezes out
  // is dropped rather than shrunk to a token.
  function fitOut(V){
    const out=[];
    for(const it of (LAYOUT[V.key]||[])){
      const P0=profAtY(V,it.y0), P1=profAtY(V,it.y1), zt=V.floorZ+it.h;
      const lim=Math.min(skinHW(V,P0,zt,0.06), skinHW(V,P1,zt,0.06),
                         skinHW(V,P0,V.floorZ,0.06), skinHW(V,P1,V.floorZ,0.06));
      const x0=Math.max(it.x0,-lim), x1=Math.min(it.x1,lim);
      if(x1-x0 < 0.14) continue;
      out.push(Object.assign({}, it, { x0:+x0.toFixed(3), x1:+x1.toFixed(3) }));
    }
    return out;
  }

  function buildChassis(out, s){
    const V=s.V, halfL=V.bodyL/2, railZ=0.34, rx=V.halfW*0.56;
    for(const sx of [-1,1]){
      boxAt(out, sx*rx-0.045, sx*rx+0.045, -halfL+0.10, halfL-0.16, railZ, railZ+0.16, 'iron', -0.1);
      bar(out,[sx*rx, halfL-0.20, railZ+0.08],[sx*0.10, halfL+V.tongue-0.18, railZ+0.10], 0.055,'iron',-0.05); // A-frame
    }
    for(const y of [-halfL+0.35, V.axleY, halfL-0.45]) boxAt(out, -rx-0.04,rx+0.04, y-0.05,y+0.05, railZ, railZ+0.10,'iron',-0.25);
    // coupler + tongue jack (parked: down, on a foot)
    const ty = halfL+V.tongue-0.16;
    boxAt(out, -0.11,0.11, ty-0.10, ty+0.20, railZ+0.02, railZ+0.20, 'galv', 0.2);
    tube(out,[0,ty+0.10,railZ+0.20],[0,ty+0.10,railZ+0.30],0.055,8,'galv',0.25);
    tube(out,[0.20,ty-0.34,railZ+0.34],[0.20,ty-0.34,0.06],0.045,8,'galv',0.15);
    boxAt(out, 0.09,0.31, ty-0.46,ty-0.22, 0.0,0.06,'iron',0.1);
    // axle + wheels
    const wx=V.halfW-0.17, wr=0.335;
    tube(out,[-wx+0.02,V.axleY,wr],[wx-0.02,V.axleY,wr],0.045,8,'iron',-0.2);
    for(const sx of [-1,1]){
      tube(out,[sx*(wx-0.10),V.axleY,wr],[sx*(wx+0.10),V.axleY,wr],wr,16,'rubber',-0.05,false);
      tube(out,[sx*(wx+0.06),V.axleY,wr],[sx*(wx+0.11),V.axleY,wr],wr*0.56,14,'chrome',0.3);
      if(s.chocks){ const cy=V.axleY-wr-0.16;
        out.push(F([[sx*(wx-0.12),cy,0],[sx*(wx+0.12),cy,0],[sx*(wx+0.12),cy+0.26,0.20],[sx*(wx-0.12),cy+0.26,0.20]],'wood',-0.2,0,null,grainTex(0.16)));
        boxAt(out, sx*(wx-0.12),sx*(wx+0.12), cy,cy+0.02, 0,0.20,'wood',-0.4,true); }
    }
    // stabiliser jacks, all four corners, wound down
    if(s.jacks) for(const sx of [-1,1]) for(const sy of [-1,1]){
      const jx=sx*(V.halfW-0.30), jy=sy*(halfL-0.52);
      bar(out,[jx,jy,railZ],[jx+sx*0.16,jy+sy*0.18,0.05],0.030,'galv',0.05);
      bar(out,[jx+sx*0.16,jy+sy*0.18,0.05],[jx+sx*0.02,jy+sy*0.30,0.05],0.024,'galv',-0.1);
      boxAt(out, jx+sx*0.06,jx+sx*0.26, jy+sy*0.08,jy+sy*0.28, 0,0.045,'iron',0.05);
    }
    // propane bottles + battery box on the A-frame
    if(s.propane){
      for(const sx of [-1,1]){ const bx=sx*0.20, by=halfL+V.tongue-0.62;
        tube(out,[bx,by,railZ+0.10],[bx,by,railZ+0.66],0.135,12,'trim',-0.12);
        tube(out,[bx,by,railZ+0.66],[bx,by,railZ+0.74],0.10,12,'trim',0.02);
        tube(out,[bx,by,railZ+0.74],[bx,by,railZ+0.80],0.045,8,'galv',0.3); }
      boxAt(out, -0.20,0.20, halfL+0.16, halfL+0.50, railZ+0.06, railZ+0.34,'iron',-0.05);
    }
    // rear bumper + lamps
    boxAt(out, -V.halfW*0.62,V.halfW*0.62, -halfL-0.16,-halfL-0.02, railZ+0.02, railZ+0.20,'galv',0.05);
    for(const sx of [-1,1]){
      tube(out,[sx*V.halfW*0.40,-halfL+0.10,V.beltZ-0.44],[sx*V.halfW*0.40,-halfL-0.02,V.beltZ-0.44],0.072,10,'lensR',0.5);
      tube(out,[sx*V.halfW*0.52,-halfL+0.34,V.topZ-0.62],[sx*V.halfW*0.52,-halfL+0.26,V.topZ-0.62],0.030,8,'lensA',0.5);
    }
    for(let i=0;i<3;i++){ const x=(i-1)*0.17;                        // nose clearance lamps
      tube(out,[x,halfL-0.30,V.topZ-0.50],[x,halfL-0.20,V.topZ-0.48],0.028,8,'lensA',0.5); }
    // service hookups, street side aft
    if(s.hookups){
      const hy=V.axleY-0.95;
      boxAt(out, -V.halfW*0.99,-V.halfW*0.86, hy-0.16,hy+0.16, 0.86,1.16,'frame',0.05);
      tube(out,[-V.halfW*0.92,hy+0.42,0.78],[-V.halfW*0.92,hy+0.42,0.60],0.045,8,'galv',0.1);
      tube(out,[-V.halfW*0.84,hy-0.30,0.50],[-V.halfW*1.32,hy-0.30,0.34],0.055,8,'iron',-0.15);
    }
  }

  function buildRoof(out, s){
    const V=s.V, halfL=V.bodyL/2;
    const crown=(y)=>{ const u=(y/V.bodyL)+0.5, P=prof(V,Math.max(0,Math.min(1,u))); return P.zc+P.hzT; };
    if(s.vents){ const n=V.vents;
      for(let i=0;i<n;i++){ const y=(-0.42+ (n===2? i*0.90 : (i-1)*1.55))*(V.bodyL/4.34);
        const z=crown(y);
        boxAt(out, -0.20,0.20, y-0.20,y+0.20, z-0.05, z+0.07,'plastic',-0.05);
        if(i===0) out.push(F([[-0.21,y-0.21,z+0.07],[0.21,y-0.21,z+0.07],[0.21,y+0.14,z+0.24],[-0.21,y+0.14,z+0.24]],'plastic',0.12)); }
    }
    if(s.acPod){ const y=-halfL*0.52, z=crown(y);
      boxAt(out, -0.34,0.34, y-0.46,y+0.46, z-0.04, z+0.22,'plastic',-0.10);
      out.push(F([[-0.30,y-0.42,z+0.22],[0.30,y-0.42,z+0.22],[0.30,y+0.42,z+0.22],[-0.30,y+0.42,z+0.22]],'plastic',0.14)); }
    if(s.rack){ const y0=halfL*0.10, y1=halfL*0.74;
      for(const sx of [-1,1]) for(const y of [y0+0.10,y1-0.10]){ const z=crown(y);
        tube(out,[sx*0.44,y,z-0.02],[sx*0.44,y,z+0.12],0.030,6,'galv',0.1); }
      for(const sx of [-1,1]) bar(out,[sx*0.44,y0,crown(y0)+0.12],[sx*0.44,y1,crown(y1)+0.12],0.026,'galv',0.2);
      const yc=(y0+y1)/2, zc=crown(yc)+0.13;
      boxAt(out, -0.40,0.40, yc-0.46,yc+0.46, zc, zc+0.26,'canvas',-0.10);        // lashed cargo box
      for(const y of [yc-0.24,yc+0.24]) bar(out,[-0.42,y,zc+0.27],[0.42,y,zc+0.27],0.016,'iron',0.1); }
  }

  function buildAwning(out, s){
    const V=s.V, D=V.door, y0=D.y0-Math.min(1.25,V.bodyL*0.20), y1=D.y1+Math.min(0.95,V.bodyL*0.16), hx=V.halfW*0.98;
    const rz=V.topZ-0.46, oz=rz-0.42, ox=hx+Math.min(1.72,V.bodyL*0.30);
    tube(out,[hx-0.04,y0-0.06,rz],[hx-0.04,y1+0.06,rz],0.075,10,'galv',0.15);
    out.push(F([[hx,y0,rz-0.02],[ox,y0,oz],[ox,y1,oz],[hx,y1,rz-0.02]],'canvas',-0.25,0,
      [[0,y0],[ox-hx,y0],[ox-hx,y1],[0,y1]], stripeTex(0.44)));
    out.push(F([[hx,y1,rz-0.02],[ox,y1,oz],[ox,y0,oz],[hx,y0,rz-0.02]],'canvas',-1.05,0));   // underside
    for(const y of [y0,y1]){
      tube(out,[ox,y,oz],[ox,y,0.02],0.032,8,'galv',0.1);
      out.push(F([[hx,y,rz-0.02],[ox,y,oz],[ox,y,oz-0.16],[hx,y,rz-0.18]],'canvas',-0.6));   // valance end
    }
    out.push(F([[hx,y0,oz-0.16],[ox,y0,oz-0.16],[ox,y0,oz],[hx,y0,oz]],'canvas',-0.2));
  }
  function buildWinAwnings(out, s){
    const V=s.V;
    for(const w of V.win){ if(w.kind!=='side') continue;
      const sx=w.side, hx=sx*V.halfW*0.965, ox=sx*(V.halfW*0.965+0.28), tz=w.z1+0.07, oz=w.z1-0.15;
      out.push(sx>0
        ? F([[hx,w.y0,tz],[ox,w.y0,oz],[ox,w.y1,oz],[hx,w.y1,tz]],'canvas',-0.30,0,[[0,w.y0],[0.3,w.y0],[0.3,w.y1],[0,w.y1]],stripeTex(0.30))
        : F([[hx,w.y1,tz],[ox,w.y1,oz],[ox,w.y0,oz],[hx,w.y0,tz]],'canvas',-0.30,0,[[0,w.y1],[0.3,w.y1],[0.3,w.y0],[0,w.y0]],stripeTex(0.30)));
      for(const y of [w.y0,w.y1]) bar(out,[hx,y,tz-0.02],[ox,y,oz],0.018,'galv',0.1);
    }
  }
  function buildPorchLamp(out, s){
    const V=s.V, D=V.door, y=D.y1+0.16, x=V.halfW*0.90, z=D.z1-0.06;
    boxAt(out, x-0.03,x+0.07, y-0.07,y+0.07, z, z+0.10, s.night?'glow':'trim', 0.3);
  }

  function build(s){
    const out=[];
    buildInterior(out,s);
    const sh=buildShell(out,s);
    buildChassis(out,s);
    buildRoof(out,s);
    buildDoor(out,s,sh);
    buildStep(out,s);
    if(s.awning) buildAwning(out,s);
    if(s.winAwn) buildWinAwnings(out,s);
    buildPorchLamp(out,s);
    return out;
  }

  // ---- materials ----
  function makeMats(s){
    const wx=s.weather, night=s.night;
    const grime=r=>r.map(c=>mix(desat(c,wx*0.24),'#3a3128',wx*0.12));
    const dull =r=>r.map(c=>mix(desat(c,wx*0.34),'#4a4f52',wx*0.16));
    const rust =r=>r.map(c=>mix(c,'#6d3417',wx*0.26));
    const cool =r=>night?r.map(c=>mix(desat(c,0.30),'#1b2740',0.40)):r;
    const t=r=>cool(grime(r)), tm=r=>cool(rust(grime(r))), ts=r=>cool(dull(r));
    const painted = !!s.paint;
    const skin = painted ? (BODY[s.paint]||BODY.sage) : SKIN;
    return {
      skin  :{ ramp: painted?t(skin):ts(skin), polish: painted?0.22:1 },
      trim  :{ ramp: t(TRIM) }, liner:{ ramp: cool(TRIM.map(c=>mix(desat(c,0.34),'#4a4f52',0.44))) },
      frame :{ ramp: t(CHROME.map(c=>mix(c,'#c9cfd1',0.22))) },
      leaf  :{ ramp: t(TRIM.map(c=>mix(desat(c,0.20),'#cdcfc9',0.30))) },
      plastic:{ ramp: t(TRIM.map(c=>mix(desat(c,0.30),'#6f736c',0.46))) },
      wood  :{ ramp: t(WOOD) }, shade:{ ramp: cool(SHADE) },
      iron  :{ ramp: tm(IRON) }, galv:{ ramp: tm(GALV) }, chrome:{ ramp: t(CHROME) },
      rubber:{ ramp: t(RUBBER) }, canvas:{ ramp: t(CANVAS) },
      lensR :{ ramp: LENSR }, lensA:{ ramp: LENSA },
      glass :{ ramp: night?GLASSN:GLASSD }, glow:{ ramp: night?GLOW:['#5f6a5e','#8d9a8b','#b6c2b0','#d3ddcb'] },
    };
  }

  // ---- rasteriser (fleet recipe; POLISH is the one addition, and only the skin sees it) ----
  function paint(faces, B, MATS, s){
    const N=W*H, zbuf=new Float32Array(N).fill(Infinity), dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null), ibuf=new Int16Array(N), nbuf=new Array(N).fill(null);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]); let sh=shadeOf(n, B.se, B.ce);
      if(sh<0 && f.b<=-0.8) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const M=MATS[f.mat]||MATS.skin, ramp=M.ramp, tex=f.tex, uv=f.uv, flat=f.flat;
      const shIdx=(nn,sh0)=>{ let v=sh0*GAIN+BIAS+f.b;
        if(M.polish) v += M.polish*(1.55*nn[2] + 0.50*Math.max(0,1-Math.abs(nn[2])/0.30)*Math.max(0,Math.min(1,sh0*2)));
        return v; };
      let fidx=shIdx(n,sh), vfi=null;
      if(f.vn){ vfi=f.vn.map(n0=>{ const nn=[n0[0]*B.ct-n0[1]*B.stt, n0[0]*B.stt+n0[1]*B.ct, n0[2]];
        return shIdx(nn, shadeOf(nn,B.se,B.ce)); }); }
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
            let fi = vfi ? (w0*vfi[ia]+w1*vfi[ib]+w2*vfi[ic]) : fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi+=tex(uu,vv); }
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
    if(s.weather>0.02){ const rnd=mulberry32(4471|((s.V.ft*97)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='skin'||m==='galv'||m==='iron'||m==='canvas') && rnd()<s.weather*0.05)
          out[i]=rbuf[i][Math.max(0,ibuf[i]-1)]; } }
    if(s.night){ for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x;
      if(nbuf[i]!=='glow' && nbuf[i]!=='glass') continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*W+(x+dx);
        if(out[j] && nbuf[j]!=='glow' && nbuf[j]!=='glass') out[j]=mix(out[j],'#f2c25e',nbuf[i]==='glow'?0.30:0.16); } } }
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
    const s=resolve(opts), B=camBasis({dir,elev:opts.elev});
    return toRGBA(post(paint(build(s), B, makeMats(s), s), s));
  }
  // the enter cue, baked: n frames of the door + step, swing 0 -> 1
  function frames(dir, n, opts){ n=n||8; const out=[];
    for(let i=0;i<n;i++) out.push(render(dir, Object.assign({}, opts, { swing:i/(n-1) })));
    return out;
  }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }
  function anchors(dir, opts){ opts=opts||{}; const s=resolve(opts), V=s.V, e=opts.elev, halfL=V.bodyL/2;
    const P=(p)=>{ const q=project(dir,p,e); return { x:q.x, y:q.y, m:p }; };
    const D=V.door, yc=(D.y0+D.y1)/2;
    return {
      floor:P([0,0,V.floorZ]), door:P([V.halfW*0.92,yc,V.floorZ]),
      threshold:P([V.halfW*0.88,yc,V.floorZ]), hinge:P([V.halfW*0.86,D.y1,V.floorZ]),
      step:P([V.halfW*0.80+0.56,yc,V.floorZ-0.46]),
      hitch:P([0,halfL+V.tongue-0.06,0.44]),
      lamp:P([V.halfW*0.92,D.y1+0.16,D.z1-0.01]),
      awning: s.awning?P([V.halfW*0.98+2.30,yc,V.topZ-0.82]):null,
      vents:(function(){ const a=[],n=V.vents; for(let i=0;i<n;i++){ const y=(-0.42+(n===2?i*0.90:(i-1)*1.55))*(V.bodyL/4.34);
        const u=Math.max(0,Math.min(1,(y/V.bodyL)+0.5)), Pp=prof(V,u); a.push(P([0,y,Pp.zc+Pp.hzT+0.08])); } return a; })(),
      bodyL:V.bodyL, width:V.halfW*2, floorZ:V.floorZ, height:V.topZ, loa:V.bodyL+V.tongue,
    };
  }

  // ================= GAMEPLAY SIDECAR =================
  // Generated from the same resolve/prof math the bake uses — never hand-transcribed.
  function gameplayGeometry(opts){
    const s=resolve(opts), V=s.V, halfL=V.bodyL/2, D=V.door, yc=(D.y0+D.y1)/2;
    const US=stations(V), port=[], star=[];
    for(const u of US){ const P=prof(V,u), hw=interiorHW(V,P); if(hw<=0.06) continue;
      port.push([-(+hw.toFixed(3)), +P.y.toFixed(3)]); star.push([+hw.toFixed(3), +P.y.toFixed(3)]); }
    const floor=star.concat(port.reverse());        // CCW viewed from +Z, as the section declares
    const obstructions=fitOut(V).map(it=>({
      id:it.id, kind:it.kind,
      footprint:[[it.x0,it.y0],[it.x1,it.y0],[it.x1,it.y1],[it.x0,it.y1]],
      height_above_sole_m:it.h,
      treatment: it.h<=0.50 ? 'step_over' : (it.h<=0.95 ? 'waist_block' : 'wall'),
      note:it.note, provenance:'rig LAYOUT.'+V.key+' entry "'+it.id+'", x clamped to the loft by fitOut()' }));
    return {
      exportSymbol:'CamperIso', rig:'camperIsoRig.js', variant:V.key,
      generatedBy:'CamperIso.gameplayGeometry({variant:"'+V.key+'"})',
      hull:{ label:V.label, nominal_ft:V.ft, body_length_m:V.bodyL, overall_length_m:+(V.bodyL+V.tongue).toFixed(2),
        width_m:+(V.halfW*2).toFixed(2), height_m:V.topZ, sole_z_m:V.floorZ,
        interior_headroom_m:+(V.topZ-0.10-V.floorZ).toFixed(2), axle_y_m:V.axleY },
      frame:{ units:'metres', scale_px_per_m:32, origin:'ground-centre of the BODY footprint (tongue excluded)',
        axes:'+x curb side (door side), +y bow/hitch, +z up', heading_independent:true },
      SOLE:[{ id:'saloon', winding:'ccw_from_above', z:V.floorZ, polygon:floor,
        note:'One continuous sole from tail to nose, inset 0.055 m off the skin at sole height — the '+
             'shell tumblehome means the walkable width is NARROWER than the body width everywhere. '+
             'Fit-out is listed as obstructions, never as holes.',
        _notes:obstructions }],
      THRESHOLD:{ id:'entry', side:'curb', door_clear_width_m:+(D.y1-D.y0).toFixed(2),
        door_clear_height_m:+(D.z1-V.floorZ).toFixed(2),
        threshold_point:[+(V.halfW*0.88).toFixed(3), +yc.toFixed(3), V.floorZ],
        hinge_axis:{ x:+(V.halfW*0.86).toFixed(3), y:+D.y1.toFixed(3), vertical:true },
        swing:{ outward:true, toward:'+y (forward)', open_deg:104,
          arc_radius_m:+(D.y1-D.y0).toFixed(2),
          keep_clear:[[+(V.halfW*0.86).toFixed(3), +D.y1.toFixed(3)],
                      [+(V.halfW*0.86+ (D.y1-D.y0)*Math.sin(104*DEG)).toFixed(3), +(D.y1-(D.y1-D.y0)*Math.cos(104*DEG)).toFixed(3)],
                      [+(V.halfW*0.86).toFixed(3), +D.y0.toFixed(3)]],
          note:'Collider is the swept arc, not the leaf — the leaf is hinged on its FORWARD edge and '+
               'sweeps out over the step. 8-frame cue, played reversed on exit.' } },
      STEP:{ id:'entry_step', treads:[
          { index:1, top_z_m:+(V.floorZ-0.24).toFixed(3), riser_from_sole_m:0.24, out_x_m:+(V.halfW*0.80+0.30).toFixed(3) },
          { index:2, top_z_m:+(V.floorZ-0.46).toFixed(3), riser_from_below_m:0.22, out_x_m:+(V.halfW*0.80+0.56).toFixed(3) }],
        ground_to_first_tread_m:+(V.floorZ-0.46).toFixed(3), width_m:0.60,
        note:'Folds down with the door over the back 58% of the swing. Stowed it is inside the body '+
             'silhouette and NOT walkable.' },
      HOOKUPS:[
        { id:'shore_power', type:'power', pos:[+(-V.halfW*0.92).toFixed(3), +(V.axleY-0.95).toFixed(3), 1.01], side:'street' },
        { id:'water_fill',  type:'water', pos:[+(-V.halfW*0.92).toFixed(3), +(V.axleY-0.53).toFixed(3), 0.69], side:'street' },
        { id:'sewer_outlet',type:'sewer', pos:[+(-V.halfW*1.32).toFixed(3), +(V.axleY-1.25).toFixed(3), 0.34], side:'street' },
        { id:'propane',     type:'fuel',  pos:[0.20, +(halfL+V.tongue-0.62).toFixed(3), 1.00], side:'A-frame' }],
      _excluded:{
        WASHBOARD:'Not a hull. No side decks.',
        CLEATS:'Parked dwelling — the coupler at ['+[0,+(halfL+V.tongue-0.06).toFixed(2),0.44].join(', ')+'] is a tow point, not a tie-off, and the four stabiliser jacks are level pads, not moorings.',
        ROOF:'Vents, AC pod and the rack are modelled but no roof surface is walkable on a monocoque this light.' },
      _confirm:{
        head_partition:V.key==='clipper'?'The head is given as a 1.90 m partition (a wall). If gameplay needs to enter it, it wants its own sole polygon and a second door.':'n/a',
        awning_footprint:'The awning is a deployable and its shade footprint is NOT in this file — it moves with the toggle, so it belongs to the runtime, not the geometry.' },
      _sections_elsewhere:{
        INTERACT:'Generated by camperInteriorRig.js — CamperInterior.interactables({variant:"'+V.key+'"}). '+
          'The berth (sleep/save), wardrobe, shelf and wall calendar live there because the interior rig '+
          'places them; merge that output in as this file\'s INTERACT section rather than re-typing it. '+
          'Its reach points are sole-level standing spots and must be checked against SOLE above.' },
    };
  }

  function list(){ return Object.keys(VARIANTS); }

  root.CamperIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    SKIN, BODY, TRIM, IRON, GALV, RUBBER, CHROME, CANVAS, WOOD, GLASSD, GLASSN, KEY,
    VARIANTS, PRESETS, LAYOUT, list, dims, resolve, render, frames, anchors, project, gameplayGeometry,
    /* THE LOFT, published. camperInteriorRig.js builds the room inside this shell and MUST measure
       against these exact functions — a re-derived profile is how an interior drifts off its body. */
    loft:{ NTOP, NBOT, ringPt, prof, profAtY, skinHW, interiorHW, stations, fitOut, NR,
      shade:{ GAIN, BIAS, EDGE, LN, BAYER, KEY }, cell:{ W, H, cx, groundY, S } } };
})(typeof globalThis!=='undefined'?globalThis:window);

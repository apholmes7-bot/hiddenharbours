/* Hidden Harbours — parametric ISO MANOR / MANSION rig (ADR-0006 bake pipeline, SAME turntable +
   camera + shading as houseIsoRig.js / rowhouseIsoRig.js / stackFlatsIsoRig.js / interiorIsoRig.js /
   the fleet). BUILDING CLASS 4: the single grand house. ONE parametric 3D manor — a masonry main
   block, a roof storey, and a vertical accent (entry tower or corner turret) — baked to pixel sheets
   through the SHARED 3/4 camera: 45deg steps, elev 40deg default, flat-facet shading from the fixed
   upper-LEFT key, z-buffered, ordered dither, per-face uv texture, depth-edge darkening, NO AA.
   32 px = 1 m. All 8 facings fall out of one model by construction.

   WHY IT IS NOT houseIsoRig. The house rig is a cottage-to-farmhouse vernacular box: one gable
   massing, painted wood, trim boards, a stoop. A manor is a different problem — it is MASONRY and
   it is DRESSED. The thing that makes it read as a manor at 20 px is not size, it is the SILHOUETTE
   FURNITURE: a roof storey with its own dormers, a projecting vertical accent, a cornice heavy
   enough to throw a soffit shadow, and stone that is cut differently at the corners, the base and
   the openings than in the field. So every detail here that breaks the outline is real geometry
   (cornice, brackets, sills, hoods, cresting, finials, dormers, tower, turret, balustrades) and
   every detail that lies flush is a uv texture (brick, rubble, ashlar, rustication, quoins, slate,
   dentils, louvres). Geometry for silhouette, texture for surface — the pixel-art division of
   labour, restated for masonry.

   THE TWO STYLES (whole vocabularies, not repaints — the stack-flats rule):
     secondEmpire  MANSARD MANOR — brick or painted body on a rusticated basement with a water
                   table, dressed quoins, a string course per floor, and a deep BRACKETED CORNICE
                   (paired scroll brackets under a dentil frieze and a projecting shelf). Above it a
                   true MANSARD roof storey: near-vertical slated lower slope, a kicked break band,
                   a shallow deck, IRON CRESTING and corner finials along the deck edge, and
                   segmental-arched dormers standing proud of the slope. A projecting ENTRY TOWER
                   carries the centre one storey higher under its own mansard cap with an oculus, a
                   balustraded balcony over the double doors and a fanlight; a two-storey canted BAY
                   on the flank; an optional PORTE-COCHERE on the other. Windows are tall 2/2 with
                   segmental brick heads, projecting hoods, dressed sills on brackets, and shutters.
                   Corbelled brick stacks with pots.
     baronial      STONE MANOR — battered rubble or ashlar walls with a chamfered plinth, dressed
                   quoins and hood moulds with label stops, a corbel course at the eaves, and a
                   steep SLATE gable roof whose ends rise into CROW-STEP gables with dressed coping.
                   A full-height round TURRET with a conical slated cap, a corbelled base ring and a
                   finial; a corbelled BARTIZAN at the far corner; a projecting stone ORIEL on the
                   front; WALL DORMERS rising through the eaves with their own coped gables; an
                   arched stone entry with a hood, heraldic panel and stone steps; CLUSTERED chimney
                   stacks on the gable apexes. No cornice, no cresting, no shutters — the whole
                   vocabulary is cut stone and slate.

   BUILDER SURFACE (every axis resolved per render, no re-modelling):
     tier:'secondEmpire'|'baronial'   size:0..1 (~11x15m -> ~17x22m)   storeys:2|3 (full masonry
     storeys; the roof storey is a further habitable level, so a default manor is 3 levels)
     clad:'brick'|'painted'|'rubble'|'ashlar'|'render'   body / dress / roofMat: ramp keys
     roofForm:'mansard'|'gable'|'hip'   accent:'tower'|'turret'|'none'   bay:'none'|'canted'|'oriel'
     porte:bool  dormers:0..4  chimneys:0..3  crestingOn / quoins / stringCourse / waterTable /
     rustBase / brackets / dentils / shutters / hoods / balustrade / bartizan / crowStep / finials
     winStyle:'twoOverTwo'|'sixOverSix'|'fourOverFour'|'oneOverOne'|'mullioned'  winDensity:0..1
     weather:0..1   lit:0..1 (night occupancy)   night:bool   elev:deg   outline:bool (ADR-0031 A/B)

   REGISTRATION CONTRACT for a future manor-interior pass (the station / terrace precedent — the
   interior rig measures nothing, it reads the shell): shell(opts) publishes wall thickness, the
   floor and ceiling height of every level including the roof storey and the basement, the interior
   footprint of each level, the accent's own interior cylinder or box, and every opening (door,
   window, dormer, oriel) in the SAME metres this bake uses, so a room registers under its window
   to the pixel. units(opts) publishes the gameplay table — the household's levels, floor areas,
   entries and service door. anchors(dir,opts) reports door / steps / stack / tower-top /
   turret-top / balcony / window points in cell px per facing, so smoke, lit windows, lamps and NPC
   entry cues are runtime overlays on baked points, never re-drawn art.

   LIGHT: matches the shipped neighbours (houseIsoRig / rowhouseIsoRig / stackFlatsIsoRig / the
   fleet) — upper-left key, LN = normalize([-0.42,0.72,0.52]). The art bible §1 rules the canonical
   key is top-of-frame and asks that upper-left rigs be corrected before their next bake; a manor
   stands on the hill above the same street as those rigs, so matching the shipped set was chosen
   over matching the doc. It is a one-constant change and a whole-set decision, not this rig's.
   KEYLINE: ringless by default (ADR-0031); {outline:true} kept as a live A/B.

   PALETTE: every ramp is lifted VERBATIM from the rig that owns it (houseIsoRig, stackFlatsIsoRig,
   rowhouseIsoRig, vanIsoRig, wharfBuildingRig) or derived in-file through yardIsoRig's rampFrom()
   from a canonical step, with the source named on the line. No ramp is forked, no hue invented.

   Exposes globalThis.ManorIso = { W,H,PX,DIRS,pivot,order,defaultElev, TIERS,CLADS,BODY,ROOFS,
     DRESS,TRIM,IRON,DOORPAINTS,WINDOWS,ACCENTS,BAYS,ROOFFORMS,PRESETS, dims(opts), shell(opts),
     units(opts), render(dir,opts), anchors(dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1040, H = 1300, cx = 520, groundY = 1000;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;

  // ---- colour helpers, declared before the palettes so derived ramps can use them -------------
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }
  // yardIsoRig.js:127 — 6-step ramp around one colour, value structure of the master ramps.
  function rampFrom(hex){
    return [mix(hex,'#141a18',0.62), mix(hex,'#141a18',0.38), mix(hex,'#141a18',0.14),
            hex, mix(hex,'#f4f1e4',0.2), mix(hex,'#f4f1e4',0.42)];
  }

  // ---- palettes, dark -> light. Source named per line; derived ramps go through rampFrom(). ----
  const CREAM   = ['#8a6f3c','#a6884b','#c2a35f','#d8bd7c','#e9d59d','#f5e7c1']; // houseIsoRig BODY.cream
  const ASPHB   = ['#2a211a','#3a2e23','#4c3d2e','#5f4d3a','#736046','#877254']; // houseIsoRig ROOFS.asphaltBrown
  const BRICKR  = ['#3a201a','#552b20','#6e3728','#874634','#a05743','#b96b55']; // houseIsoRig · interiorIsoRig BRICK
  const RUBBLE  = ['#3b3d3a','#4a4d49','#5c605b','#70746e','#858982','#9a9e96']; // stackFlatsIsoRig RUBBLE
  const GRANITE = ['#4a4d51','#5a5e62','#6d7175','#818589','#95999d','#a9adb1']; // stackFlatsIsoRig GRANITE
  const SLATE   = ['#282d33','#333940','#40474f','#4f5760','#606872','#727b85']; // stackFlatsIsoRig SLATE
  const COPPER  = ['#1e3a33','#2b5045','#3a6659','#4b7d6d','#5d9482','#71ab97']; // stackFlatsIsoRig COPPER
  const STONEH  = ['#33343a','#42444b','#54575d','#666a70','#7a7e84'];           // houseIsoRig STONE
  const BODY = {
    redBrick:     BRICKR,
    buffBrick:    rampFrom(CREAM[1]),          // derived · cream step 1
    brownstone:   rampFrom(ASPHB[3]),          // derived · asphaltBrown step 3
    greyBrick:    rampFrom(STONEH[2]),         // derived · houseIsoRig stone step 2
    paintedWhite: ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'], // houseIsoRig BODY.white
    paintedCream: CREAM,
    paintedSage:  ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'], // houseIsoRig BODY.sage
    paintedBlue:  ['#33454a','#43585d','#556d72','#6a848a','#849ea3','#a3b9bd'], // houseIsoRig BODY.blue
    rubbleStone:  RUBBLE,
    ashlarGrey:   GRANITE,
    sandstone:    rampFrom(CREAM[2]),          // derived · cream step 2
    seaSlate:     rampFrom(SLATE[3]),          // derived · slate step 3
  };
  const ROOFS = {
    slate:       SLATE,
    slateBlue:   rampFrom(mix(SLATE[3],'#3b4c63',0.45)),  // derived · slate step 3 toward harbour blue
    asphaltGrey: ['#23262b','#2e333a','#3c424a','#4c535c','#5d6570','#6f7883'], // houseIsoRig ROOFS.asphaltGrey
    metal:       ['#424d52','#556065','#6c7c81','#88999e','#a4babe','#c0d4d7'],  // houseIsoRig ROOFS.metal
    copper:      COPPER,
  };
  const DRESS  = GRANITE;                                                       // dressed stone: quoins, sills, coping
  const TRIM   = ['#9aa09a','#b4b8b0','#ccd0c7','#e0e2da','#eef0e8','#f8f9f2']; // houseIsoRig TRIM
  const IRON   = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970']; // vanIsoRig IRON
  const WOOD   = ['#4f3a24','#63492d','#785a39','#8f7049','#a6875d','#bd9f74']; // houseIsoRig WOOD
  const CONC   = ['#55564f','#686962','#7c7d74','#919287','#a6a79b','#bbbcae']; // wharfIsoRig CONCRETE
  const GLASSD = ['#33474d','#40585f','#547078'];                               // wharfBuildingRig day glass
  const GLASSN = ['#7a4f18','#b98a2f','#eed07a'];                               // wharfBuildingRig night glass
  const GLASS_HI = '#cfe6e8';                                                   // houseIsoRig catch-light
  const KEY = '#1a1c22';
  const DOORPAINTS = {
    plum:  ['#2e2333','#3f3047','#523f5d','#664f73','#7d648b','#9079a1'],       // vanIsoRig BODY.plum
    teal:  ['#123a3a','#1b4d4b','#26635e','#357b73','#4d968b','#6cb1a4'],       // vanIsoRig BODY.teal
    oxblood: ['#4a130f','#671b14','#88271c','#a33124','#bd4230','#d25a42'],     // houseIsoRig BODY.red
    oak:   WOOD,
    black: IRON,
  };

  const CLADS     = ['brick','painted','rubble','ashlar','render'];
  const ROOFFORMS = ['mansard','gable','hip'];
  const ACCENTS   = ['tower','turret','none'];
  const BAYS      = ['none','canted','oriel'];
  const WINDOWS   = ['twoOverTwo','sixOverSix','fourOverFour','oneOverOne','mullioned'];
  const WINSTYLES = {
    twoOverTwo:   { v:1, r:[0.5] },
    sixOverSix:   { v:2, r:[0.25,0.5,0.75] },
    fourOverFour: { v:1, r:[0.25,0.5,0.75] },
    oneOverOne:   { v:0, r:[0.5] },
    mullioned:    { v:2, r:[0.34,0.68], stone:true },
  };

  // ---- the two style vocabularies -------------------------------------------------------------
  const TIERS = {
    secondEmpire: {
      label:'Second Empire', clad:'brick', body:'brownstone', dress:'ashlarGrey', roofMat:'slate',
      roofForm:'mansard', accent:'tower', bay:'canted', porte:false,
      storeyH:3.55, fH:1.05, wallT:0.5, ov:0.62, cornH:0.62, friezeH:0.60,
      mansRise:3.05, mansIns:0.60, deckRise:1.25, deckIns:4.20,
      quoins:true, stringCourse:true, waterTable:true, rustBase:true, brackets:true, dentils:true,
      shutters:true, hoods:true, crestingOn:true, finials:true, balustrade:true,
      crowStep:false, bartizan:false, corbelEaves:false,
      winStyle:'twoOverTwo', arch:'segmental', winDensity:0.62, dormers:3, chimneys:2,
      door:'plum', weather:0.12,
    },
    baronial: {
      label:'Scottish Baronial', clad:'rubble', body:'rubbleStone', dress:'ashlarGrey', roofMat:'slate',
      roofForm:'gable', accent:'turret', bay:'oriel', porte:false,
      storeyH:3.35, fH:0.95, wallT:0.72, ov:0.30, cornH:0.34, friezeH:0.0,
      pitch:1.42,
      quoins:true, stringCourse:true, waterTable:true, rustBase:false, brackets:false, dentils:false,
      shutters:false, hoods:true, crestingOn:false, finials:true, balustrade:false,
      crowStep:true, bartizan:true, corbelEaves:true,
      winStyle:'mullioned', arch:'round', winDensity:0.5, dormers:3, chimneys:3,
      door:'oak', weather:0.3,
    },
  };

  const PRESETS = {
    mansardManor:  { tier:'secondEmpire', body:'brownstone',   roofMat:'slate',     size:0.5, storeys:2, porte:true,  bay:'canted', dormers:3, weather:0.12 },
    harbourmaster: { tier:'secondEmpire', body:'paintedCream', roofMat:'slateBlue', size:0.28, storeys:2, clad:'painted', porte:false, bay:'canted', dormers:2, shutters:true, weather:0.2 },
    merchantsSeat: { tier:'secondEmpire', body:'redBrick',     roofMat:'slateBlue', size:0.78, storeys:3, porte:true,  bay:'canted', dormers:4, chimneys:3, weather:0.08 },
    stoneManor:    { tier:'baronial',     body:'rubbleStone',  roofMat:'slate',     size:0.5, storeys:2, accent:'turret', bay:'oriel', bartizan:true, dormers:3, weather:0.3 },
    lairdsHouse:   { tier:'baronial',     body:'ashlarGrey',   roofMat:'slate',     size:0.34, storeys:2, clad:'ashlar', accent:'none', bay:'none', bartizan:false, dormers:2, chimneys:2, weather:0.16 },
    cliffSeat:     { tier:'baronial',     body:'sandstone',    roofMat:'slateBlue', size:0.72, storeys:3, accent:'turret', bay:'oriel', bartizan:true, dormers:4, chimneys:3, weather:0.44 },
  };

  // ---- shading constants (fleet recipe) -------------------------------------------------------
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  function hash2(a,b){ let h=Math.imul((a|0)^0x9e3779b9,0x85ebca6b) ^ Math.imul((b|0)^0xc2b2ae35,0x27d4eb2f); h^=h>>>13; return ((h>>>0)%997)/997; }

  // ---- camera / projection (identical to houseIsoRig, so a manor composites with the street) ---
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) };
  }
  function projVert(x,y,z,B){
    const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:groundY-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) };
  }
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }

  // ---- faces + plane-general primitives -------------------------------------------------------
  // Every face: { v:[[x,y,z]..], mat, b, db, uv:[[u,v]..]|null, tex:fn|null, flat:bool }
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }

  // A VERTICAL PLANE A->B. Outward normal is the right-hand of the travel direction, which matches
  // houseIsoRig's wall() winding, so axis walls, canted-bay cheeks, tower faces and the facets of a
  // round turret are all ONE code path — that is what lets a turret carry the same sash as a wall.
  function P_(ax,ay,bx,by){
    const dx=bx-ax, dy=by-ay, L=Math.hypot(dx,dy)||1e-6, ux=dx/L, uy=dy/L;
    return { ax,ay,bx,by,ux,uy, nx:uy, ny:-ux, L };
  }
  function pPt(P,t,d,z){ return [P.ax+P.ux*t+P.nx*(d||0), P.ay+P.uy*t+P.ny*(d||0), z]; }
  function pWall(out,P,z0,z1,mat,tex,b,t0,t1,d){
    const a=t0||0, e=(t1==null?P.L:t1);
    out.push(F([pPt(P,a,d,z0),pPt(P,e,d,z0),pPt(P,e,d,z1),pPt(P,a,d,z1)],mat,b||0,0,
      [[a,z0],[e,z0],[e,z1],[a,z1]],tex));
  }
  // flush decal on a plane (quoins, shutters, glass, panels) — offset out, depth-biased forward
  function pDecal(out,P,t0,t1,z0,z1,mat,b,tex,flat,db,d){
    const dd=(d||0)+0.02;
    out.push(F([pPt(P,t0,dd,z0),pPt(P,t1,dd,z0),pPt(P,t1,dd,z1),pPt(P,t0,dd,z1)],mat,b||0.3,
      db!=null?db:0.06,[[0,z0],[t1-t0,z0],[t1-t0,z1],[0,z1]],tex||null,!!flat));
  }
  // a PROJECTING box straddling a plane: sills, hoods, cornices, string courses, brackets,
  // pilasters, cresting posts. Five faces — outward, top (lit), soffit (dark), two returns.
  function pBox(out,P,t0,t1,z0,z1,d0,d1,mat,b,tex){
    const p=(t,d,z)=>pPt(P,t,d,z), B=b||0;
    out.push(F([p(t0,d1,z0),p(t1,d1,z0),p(t1,d1,z1),p(t0,d1,z1)],mat,B,0,[[0,z0],[t1-t0,z0],[t1-t0,z1],[0,z1]],tex||null));
    out.push(F([p(t0,d0,z1),p(t0,d1,z1),p(t1,d1,z1),p(t1,d0,z1)],mat,B+0.4,0));
    out.push(F([p(t0,d1,z0),p(t0,d0,z0),p(t1,d0,z0),p(t1,d1,z0)],mat,B-1.15,0));
    out.push(F([p(t1,d1,z0),p(t1,d0,z0),p(t1,d0,z1),p(t1,d1,z1)],mat,B-0.15,0));
    out.push(F([p(t0,d0,z0),p(t0,d1,z0),p(t0,d1,z1),p(t0,d0,z1)],mat,B-0.35,0));
  }
  function box(out,x0,x1,y0,y1,z0,z1,mat,tex,b){
    pWall(out,P_(x0,y0,x1,y0),z0,z1,mat,tex,b);
    pWall(out,P_(x1,y1,x0,y1),z0,z1,mat,tex,b);
    pWall(out,P_(x1,y0,x1,y1),z0,z1,mat,tex,b);
    pWall(out,P_(x0,y1,x0,y0),z0,z1,mat,tex,b);
    out.push(F([[x0,y0,z1],[x1,y0,z1],[x1,y1,z1],[x0,y1,z1]],mat,(b||0)+0.4,0));
  }
  function slab(out,x0,x1,y0,y1,z,mat,b,tex){
    out.push(F([[x0,y0,z],[x1,y0,z],[x1,y1,z],[x0,y1,z]],mat,(b||0)+0.4,0,
      tex?[[0,0],[x1-x0,0],[x1-x0,y1-y0],[0,y1-y0]]:null,tex||null));
  }
  function quad(out,p0,p1,p2,p3,mat,b,tex){
    const u=Math.hypot(p1[0]-p0[0],p1[1]-p0[1],p1[2]-p0[2]);
    const v=Math.hypot(p3[0]-p0[0],p3[1]-p0[1],p3[2]-p0[2]);
    out.push(F([p0,p1,p2,p3],mat,b||0,0,tex?[[0,0],[u,0],[u,v],[0,v]]:null,tex||null));
  }
  // 4 trapezoids from rect(x0..x1, y0..y1) at zLow, stepping IN by ins and UP by dz: one primitive
  // for the mansard's steep band, its shallow deck slope, and any hipped roof.
  function hipBand(out,x0,x1,y0,y1,zLow,ins,dz,mat,b,tex){
    const X0=x0+ins,X1=x1-ins,Y0=y0+ins,Y1=y1-ins, zh=zLow+dz, sl=Math.hypot(ins,dz), B=b||0;
    const uvW=(w)=>tex?[[0,0],[w,0],[w-ins,sl],[ins,sl]]:null;
    out.push(F([[x1,y1,zLow],[x0,y1,zLow],[X0,Y1,zh],[X1,Y1,zh]],mat,B+0.30,0,uvW(x1-x0),tex||null));
    out.push(F([[x0,y0,zLow],[x1,y0,zLow],[X1,Y0,zh],[X0,Y0,zh]],mat,B-0.30,0,uvW(x1-x0),tex||null));
    out.push(F([[x1,y0,zLow],[x1,y1,zLow],[X1,Y1,zh],[X1,Y0,zh]],mat,B+0.15,0,uvW(y1-y0),tex||null));
    out.push(F([[x0,y1,zLow],[x0,y0,zLow],[X0,Y0,zh],[X0,Y1,zh]],mat,B-0.15,0,uvW(y1-y0),tex||null));
  }

  // ---- surface textures: integer ramp deltas (negative = joint / shadow) ----------------------
  function tex_brick(){ const CO=0.17, BW=0.34;
    return (u,v)=>{ const row=Math.floor(v/CO), fv=((v%CO)+CO)%CO;
      const su=(((u+(row&1)*BW*0.5)%BW)+BW)%BW;
      if(fv<0.035) return -2;
      if(su<0.03) return -1;
      if(fv>CO-0.03) return 1;
      return hash2(Math.floor((u+(row&1)*BW*0.5)/BW),row)>0.88?1:0; };
  }
  function tex_rubble(){ const CO=0.34;
    return (u,v)=>{ const row=Math.floor(v/CO), fv=((v%CO)+CO)%CO;
      const off=hash2(row,7)*0.7, bw=0.52+hash2(row,3)*0.42;
      const su=(((u+off)%bw)+bw)%bw, k=hash2(Math.floor((u+off)/bw),row);
      if(fv<0.055||su<0.055) return -2;
      if(fv>CO-0.045) return 1;
      return k<0.26?-1:(k>0.79?1:0); };
  }
  function tex_ashlar(){ const CO=0.36, BW=0.92;
    return (u,v)=>{ const row=Math.floor(v/CO), fv=((v%CO)+CO)%CO;
      const su=(((u+(row&1)*BW*0.5)%BW)+BW)%BW;
      if(fv<0.045||su<0.045) return -2;
      if(fv>CO-0.04) return 1;
      return hash2(Math.floor(u/BW),row)>0.84?1:0; };
  }
  function tex_rust(){ const CO=0.52, BW=1.15;   // rusticated basement: deep chamfered joints
    return (u,v)=>{ const row=Math.floor(v/CO), fv=((v%CO)+CO)%CO;
      const su=(((u+(row&1)*BW*0.5)%BW)+BW)%BW;
      if(fv<0.085||su<0.075) return -3;
      if(fv<0.145||su<0.13) return -1;
      if(fv>CO-0.055) return 1;
      return 0; };
  }
  function tex_render(){ return (u,v)=> hash2(Math.floor(u*7),Math.floor(v*7))>0.93?-1:0; }
  function tex_quoin(){ const CO=0.42;           // alternating long/short dressed corner blocks
    return (u,v)=>{ const row=Math.floor(v/CO), fv=((v%CO)+CO)%CO;
      const long=(row&1)===0, w=long?1.0:0.62;
      if(u>w) return -4;                          // outside this course's block: read as field wall
      if(fv<0.055) return -2;
      if(u>w-0.05) return -2;
      if(fv>CO-0.05) return 1;
      return 0; };
  }
  function tex_slate(){ const CO=0.24, BW=0.30;
    return (u,v)=>{ const row=Math.floor(v/CO), fv=((v%CO)+CO)%CO;
      const su=(((u+(row&1)*BW*0.5)%BW)+BW)%BW, k=hash2(Math.floor(u/BW),row);
      if(fv<0.04) return -2;
      if(su<0.028) return -1;
      if(fv>CO-0.035) return 1;
      return k<0.16?-1:(k>0.9?1:0); };
  }
  function tex_seam(){ const SM=0.44; return (u,v)=> (((u%SM)+SM)%SM)<0.05?-2:0; }
  function tex_clap(){ const LAP=0.28;
    return (u,v)=>{ const f=((v%LAP)+LAP)%LAP; return f<0.05?-2:(f>LAP-0.04?1:0); };
  }
  function tex_dentil(){ const PT=0.26;
    return (u,v)=>{ const f=((u%PT)+PT)%PT; return f<0.13?1:-2; };
  }
  function tex_corbel(){ const PT=0.52;
    return (u,v)=>{ const f=((u%PT)+PT)%PT; return f<0.30?(v>0.12?1:0):-2; };
  }
  function tex_louvre(){ const PT=0.11;
    return (u,v)=>{ const f=((v%PT)+PT)%PT; return f<0.045?-2:0; };
  }
  function tex_plank(){ const PT=0.24;
    return (u,v)=>{ const f=((u%PT)+PT)%PT; return f<0.035?-2:0; };
  }
  function cladTex(clad){
    return clad==='brick'?tex_brick():clad==='rubble'?tex_rubble():clad==='ashlar'?tex_ashlar()
         : clad==='render'?tex_render():tex_clap();
  }

  // ---- openings --------------------------------------------------------------------------------
  // stepped arch head: pixel-honest voussoir ring. steps=1 gives a flat lintel.
  function archHead(out,P,c,ww,z,rise,steps,mat,d0,d1){
    if(rise<=0.02||steps<2){ pBox(out,P,c-ww/2-0.1,c+ww/2+0.1,z,z+0.14,d0,d1,mat,0.5); return; }
    for(let i=0;i<steps;i++){
      const f0=i/steps, f1=(i+1)/steps;
      const hw0=(ww/2+0.1)*Math.cos(f0*Math.PI/2*0.92), hw1=(ww/2+0.1)*Math.cos(f1*Math.PI/2*0.92);
      const zz=z+rise*f0, zz1=z+rise*f1+0.05;
      pBox(out,P,c-hw0,c+hw0,zz,zz1,d0,d1,mat,0.45+i*0.06);
      if(hw1<=0.02) break;
    }
  }
  // one sash: reveal, glass, muntins, catch light, dressed sill on brackets, arch head, hood, shutters
  function sash(out,P,c,z,ww,wh,b,opt){
    opt=opt||{};
    const st=WINSTYLES[opt.winStyle||b.winStyle]||WINSTYLES.twoOverTwo;
    const arch=opt.arch!=null?opt.arch:(b.arch||'none');
    const rise = arch==='round'?ww*0.46 : arch==='segmental'?ww*0.20 : 0;
    const topZ=z+wh, dressM=opt.dress||'dress';
    // Solid-shell decal stack: reveal, glass, then muntins on one plane.
    // A recessed pane would be hidden behind the uncut reveal quad at oblique views.
    pDecal(out,P,c-ww/2-0.09,c+ww/2+0.09,z-0.02,topZ+0.09+rise*0.5,'reveal',-0.9,null,true,0.05);
    pDecal(out,P,c-ww/2,c+ww/2,z,topZ,'glass',0,null,true,0.11,0);
    pDecal(out,P,c-ww/2+0.03,c-ww/2+ww*0.36,z+wh*0.55,topZ-0.05,'glassHi',0,null,true,0.13,0);
    const mb=0.055;
    if(st.v>0){ const cols=st.v+1;
      for(let i=1;i<=st.v;i++){ const mx=c-ww/2+ww*(i/cols);
        pDecal(out,P,mx-mb/2,mx+mb/2,z,topZ,st.stone?dressM:'trim',0.55,null,true,0.15,0); } }
    for(const r of st.r){ const rz=z+wh*r;
      pDecal(out,P,c-ww/2,c+ww/2,rz-mb/2,rz+mb/2,st.stone?dressM:'trim',0.55,null,true,0.15,0); }
    if(arch!=='none'){
      // arched pane top, in the same stepped rhythm as the ring so glass and stone agree
      const stps=arch==='round'?4:3;
      for(let i=0;i<stps;i++){ const f0=i/stps,f1=(i+1)/stps;
        const h0=(ww/2)*Math.cos(f0*Math.PI/2*0.9);
        pDecal(out,P,c-h0,c+h0,topZ+rise*f0,topZ+rise*f1+0.04,'glass',0,null,true,0.11,0); }
    }
    archHead(out,P,c,ww,topZ+rise,arch==='none'?0:0.16,arch==='none'?1:3,dressM,0,0.11);
    if(b.hoods!==false && opt.hood!==false){
      const hw2=ww/2+0.30, hz=topZ+rise+(arch==='none'?0.14:0.20);
      pBox(out,P,c-hw2,c+hw2,hz,hz+0.15,0,0.26,dressM,0.7);
      if(b.tier==='baronial'){   // label stops: the hood mould returns down at each end
        pBox(out,P,c-hw2,c-hw2+0.17,hz-0.30,hz,0,0.22,dressM,0.4);
        pBox(out,P,c+hw2-0.17,c+hw2,hz-0.30,hz,0,0.22,dressM,0.4);
      }
    }
    pBox(out,P,c-ww/2-0.20,c+ww/2+0.20,z-0.17,z-0.02,0,0.20,dressM,0.75);   // sill
    if(b.tier==='secondEmpire'){                                            // sill brackets
      for(const sx of [-ww*0.34,ww*0.34]) pBox(out,P,c+sx-0.06,c+sx+0.06,z-0.36,z-0.17,0,0.13,dressM,0.2);
    }
    if(b.shutters && opt.shutters!==false){
      for(const s of [-1,1]){ const a=c+s*(ww/2+0.03), b2=c+s*(ww/2+0.03+ww*0.46);
        pDecal(out,P,Math.min(a,b2),Math.max(a,b2),z,topZ,'shutter',0.15,tex_louvre(),false,0.07,0.03); }
    }
  }
  function doorway(out,P,c,z0,dw,dh,b,opt){
    opt=opt||{};
    const arch=opt.arch||(b.tier==='baronial'?'round':'none'), rise=arch==='round'?dw*0.44:0;
    pDecal(out,P,c-dw/2-0.10,c+dw/2+0.10,z0,z0+dh+rise*0.5+0.10,'reveal',-0.9,null,true,0.05);
    pBox(out,P,c-dw/2-0.28,c-dw/2-0.10,z0,z0+dh+0.24,0,0.16,'dress',0.35);   // jamb stones
    pBox(out,P,c+dw/2+0.10,c+dw/2+0.28,z0,z0+dh+0.24,0,0.16,'dress',0.35);
    const leaf=(dw-0.06)/2;
    for(const s of [-1,1]){ const cc=c+s*leaf/2;
      pDecal(out,P,cc-leaf/2+0.02,cc+leaf/2-0.02,z0,z0+dh,'door',0.85,tex_plank(),false,0.10,0);
      pDecal(out,P,cc-leaf/2+0.14,cc+leaf/2-0.14,z0+0.22,z0+dh*0.44,'door',-0.55,null,true,0.12,0);
      pDecal(out,P,cc-leaf/2+0.14,cc+leaf/2-0.14,z0+dh*0.52,z0+dh-0.16,'door',-0.55,null,true,0.12,0);
      pDecal(out,P,cc+s*(leaf/2-0.16)-0.035,cc+s*(leaf/2-0.16)+0.035,z0+dh*0.46,z0+dh*0.46+0.07,'iron',0.9,null,true,0.14,0);
    }
    if(arch==='round'){
      for(let i=0;i<4;i++){ const f0=i/4,f1=(i+1)/4, h0=(dw/2)*Math.cos(f0*Math.PI/2*0.9);
        pDecal(out,P,c-h0,c+h0,z0+dh+rise*f0,z0+dh+rise*f1+0.04,'glass',0,null,true,0.11,0); }
      archHead(out,P,c,dw,z0+dh+rise,0.18,4,'dress',0,0.13);
    } else {
      pDecal(out,P,c-dw/2+0.04,c+dw/2-0.04,z0+dh+0.06,z0+dh+0.52,'glass',0,null,true,0.11,0);  // fanlight
      for(let i=1;i<4;i++){ const mx=c-dw/2+dw*(i/4);
        pDecal(out,P,mx-0.03,mx+0.03,z0+dh+0.06,z0+dh+0.52,'trim',0.6,null,true,0.14,0); }
      pBox(out,P,c-dw/2-0.30,c+dw/2+0.30,z0+dh+0.52,z0+dh+0.70,0,0.30,'dress',0.7);
    }
  }
  function steps(out,P,c,z0,wide,n){
    const run=0.34;
    for(let s=0;s<n;s++){ const z=z0*(s+1)/n, d=(n-s)*run;
      pBox(out,P,c-wide/2-s*0.06,c+wide/2+s*0.06,0,z,0,d,'dress',0.15); }
  }
  function balustrade(out,P,z,h,t0,t1,mat){
    pBox(out,P,t0,t1,z,z+0.12,-0.02,0.22,mat,0.25);                          // base rail
    const n=Math.max(2,Math.round((t1-t0)/0.36));
    for(let i=0;i<n;i++){ const a=t0+(t1-t0)*((i+0.5)/n);
      pBox(out,P,a-0.065,a+0.065,z+0.12,z+h-0.12,0.02,0.17,mat,0.1); }
    pBox(out,P,t0,t1,z+h-0.12,z+h,-0.04,0.24,mat,0.55);                       // cap rail
  }
  // iron cresting: spear posts on a rail. Real geometry — it is a silhouette element.
  function cresting(out,x0,x1,y0,y1,z){
    const runs=[P_(x1,y1,x0,y1),P_(x0,y0,x1,y0),P_(x1,y0,x1,y1),P_(x0,y1,x0,y0)];
    for(const P of runs){
      pBox(out,P,0,P.L,z,z+0.09,-0.05,0.05,'iron',0.3);
      const n=Math.max(2,Math.round(P.L/0.58));
      for(let i=0;i<n;i++){ const t=P.L*((i+0.5)/n), tall=(i%2===0)?0.42:0.27;
        pBox(out,P,t-0.075,t+0.075,z+0.09,z+0.09+tall,-0.04,0.04,'iron',0.15);
        if(i%2===0) pBox(out,P,t-0.035,t+0.035,z+0.51,z+0.62,-0.03,0.03,'iron',0.5); }
    }
  }
  function finial(out,x,y,z,h,mat){
    box(out,x-0.12,x+0.12,y-0.12,y+0.12,z,z+h*0.34,mat,null,0.25);
    box(out,x-0.065,x+0.065,y-0.065,y+0.065,z+h*0.34,z+h*0.82,mat,null,0.35);
    box(out,x-0.03,x+0.03,y-0.03,y+0.03,z+h*0.82,z+h,mat,null,0.6);
  }
  // corbelled stack. clustered=true gives the baronial 2-3 flue group with a shared corbel cap.
  function stack(out,x,y,zBase,zTop,clustered,mat){
    const flues=clustered?3:1, wid=clustered?0.34:0.42, gap=0.10;
    const span=flues*wid+(flues-1)*gap;
    box(out,x-span/2-0.16,x+span/2+0.16,y-wid/2-0.20,y+wid/2+0.20,zBase,zTop-0.85,mat,tex_brick(),0);
    for(let i=0;i<3;i++){   // corbelled cap: three stepping courses
      const g=0.16+i*0.075;
      box(out,x-span/2-g,x+span/2+g,y-wid/2-0.20-g*0.5,y+wid/2+0.20+g*0.5,
          zTop-0.85+i*0.14,zTop-0.85+(i+1)*0.14,'dress',null,0.2+i*0.12);
    }
    for(let i=0;i<flues;i++){
      const fx=x-span/2+wid/2+i*(wid+gap);
      box(out,fx-wid/2,fx+wid/2,y-wid/2,y+wid/2,zTop-0.43,zTop,'clay',null,0.3);
      box(out,fx-wid/2+0.06,fx+wid/2-0.06,y-wid/2+0.06,y+wid/2-0.06,zTop-0.05,zTop+0.02,'reveal',null,-0.9);
    }
  }

  // ---- round turret: N facets through the same plane pipeline, conical slate cap --------------
  function turret(out,cxp,cyp,r,z0,z1,b,opt){
    opt=opt||{};
    const N=16, ring=[];
    for(let i=0;i<N;i++){ const a=(i/N)*Math.PI*2; ring.push([cxp+Math.cos(a)*r, cyp+Math.sin(a)*r, a]); }
    const facets=[];
    for(let i=0;i<N;i++){ const A=ring[i], Bp=ring[(i+1)%N]; facets.push(P_(A[0],A[1],Bp[0],Bp[1])); }
    const wallTex=cladTex(b.clad);
    for(const P of facets) pWall(out,P,z0,z1,'body',wallTex);
    // corbelled base ring + a string course at each floor + a corbel course under the cap
    const bands=[[z0,0.34,0.16],[z1-0.42,0.34,0.20]];
    if(b.stringCourse) for(let s=1;s<=b.storeys;s++) bands.push([b.fH+s*b.storeyH-0.16,0.22,0.12]);
    for(const [bz,bh,bd] of bands) for(const P of facets) pBox(out,P,0,P.L,bz,bz+bh,0,bd,'dress',0.45);
    // conical cap: slated triangles + finial
    const capH=r*2.75, apex=z1+capH;
    for(let i=0;i<N;i++){
      const A=ring[i], Bp=ring[(i+1)%N], wid=Math.hypot(Bp[0]-A[0],Bp[1]-A[1]);
      const eA=[A[0]+(A[0]-cxp)*0.13, A[1]+(A[1]-cyp)*0.13, z1+0.04];
      const eB=[Bp[0]+(Bp[0]-cxp)*0.13, Bp[1]+(Bp[1]-cyp)*0.13, z1+0.04];
      out.push(F([eA,eB,[cxp,cyp,apex]],'roof',0.05+Math.cos((A[2]+0.35))*0.55,0,
        [[0,0],[wid,0],[wid/2,capH]],tex_slate()));
    }
    finial(out,cxp,cyp,apex-0.06,1.05,'iron');
    // sash on the three facets that face the front quarter (+X/+Y), one per storey
    const pick=[1,2,3,14,15];
    for(let s=0;s<b.storeys;s++){
      const z=b.fH+s*b.storeyH+1.0;
      for(const k of [pick[1],pick[3]]){
        const P=facets[k];
        sash(out,P,P.L/2,z,Math.min(0.72,P.L-0.22),1.55,b,{arch:b.tier==='baronial'?'round':'segmental',shutters:false,hood:false});
      }
    }
    // attic slit in the cap zone
    const Pt=facets[2]; sash(out,Pt,Pt.L/2,z1-1.35,Math.min(0.5,Pt.L-0.3),0.9,b,{arch:'round',shutters:false,hood:false});
    return { apex, r, cx:cxp, cy:cyp, top:z1 };
  }
  // corbelled bartizan: a small round turret carried on stepped corbels at eaves level
  function bartizan(out,cxp,cyp,r,zBase,b){
    for(let i=0;i<3;i++){
      const rr=r*(0.42+i*0.20), zz=zBase-0.75+i*0.25;
      const N=10, pts=[];
      for(let k=0;k<N;k++){ const a=(k/N)*Math.PI*2; pts.push([cxp+Math.cos(a)*rr,cyp+Math.sin(a)*rr]); }
      for(let k=0;k<N;k++){ const A=pts[k],Bp=pts[(k+1)%N]; pWall(out,P_(A[0],A[1],Bp[0],Bp[1]),zz,zz+0.25,'dress',null,0.3+i*0.1); }
    }
    const N=12, ring=[], z0=zBase, z1=zBase+2.5;
    for(let i=0;i<N;i++){ const a=(i/N)*Math.PI*2; ring.push([cxp+Math.cos(a)*r,cyp+Math.sin(a)*r,a]); }
    for(let i=0;i<N;i++){ const A=ring[i],Bp=ring[(i+1)%N];
      pWall(out,P_(A[0],A[1],Bp[0],Bp[1]),z0,z1,'body',cladTex(b.clad)); }
    const capH=r*2.1, apex=z1+capH;
    for(let i=0;i<N;i++){ const A=ring[i],Bp=ring[(i+1)%N], wid=Math.hypot(Bp[0]-A[0],Bp[1]-A[1]);
      out.push(F([[A[0]*1.06-cxp*0.06,A[1]*1.06-cyp*0.06,z1+0.03],[Bp[0]*1.06-cxp*0.06,Bp[1]*1.06-cyp*0.06,z1+0.03],[cxp,cyp,apex]],
        'roof',0.05+Math.cos(A[2]+0.35)*0.5,0,[[0,0],[wid,0],[wid/2,capH]],tex_slate())); }
    finial(out,cxp,cyp,apex-0.04,0.7,'iron');
    const Pb=P_(ring[2][0],ring[2][1],ring[3][0],ring[3][1]);
    sash(out,Pb,Pb.L/2,z0+0.95,Math.min(0.46,Pb.L-0.2),0.85,b,{arch:'round',shutters:false,hood:false});
    return { apex };
  }

  // ---- dormers ---------------------------------------------------------------------------------
  // Proud dormer on the mansard's near-vertical face: front wall, cheeks, cornice, arched sash.
  function mansardDormer(out,P,t,zBase,h,b,kind){
    const wid=1.30, dp=0.55, D=P_(P.ax+P.nx*dp,P.ay+P.ny*dp,P.bx+P.nx*dp,P.by+P.ny*dp);
    const z1=zBase+h;
    pWall(out,D,zBase-0.35,z1,'dormer',cladTex(b.clad==='brick'?'painted':b.clad),0.15,t-wid/2,t+wid/2);
    for(const s of [-1,1]){                                   // cheeks, running back into the slope
      const a=t+s*wid/2;
      out.push(F(s>0?[pPt(P,a,0,zBase-0.5),pPt(P,a,0,z1),pPt(D,a,0,z1),pPt(D,a,0,zBase-0.35)]
                    :[pPt(D,a,0,zBase-0.35),pPt(D,a,0,z1),pPt(P,a,0,z1),pPt(P,a,0,zBase-0.5)],
        'dormer',s>0?0.1:-0.35,0));
    }
    const capRise=kind==='segmental'?0.30:kind==='pedimented'?0.46:0.10;
    pBox(out,D,t-wid/2-0.20,t+wid/2+0.20,z1,z1+0.17,-dp,0.24,'trim',0.7);      // dormer cornice
    if(kind==='pedimented'){
      out.push(F([pPt(D,t-wid/2-0.2,0.20,z1+0.17),pPt(D,t+wid/2+0.2,0.20,z1+0.17),pPt(D,t,0.20,z1+0.17+capRise)],'trim',0.6,0.06,null,null,true));
      out.push(F([pPt(D,t-wid/2-0.06,0.14,z1+0.17),pPt(D,t+wid/2+0.06,0.14,z1+0.17),pPt(D,t,0.14,z1+0.17+capRise*0.7)],'dormer',0.2,0.08,null,null,true));
    } else {
      for(let i=0;i<3;i++){ const f0=i/3,f1=(i+1)/3;
        const hwid=(wid/2+0.2)*Math.cos(f0*Math.PI/2*0.85);
        pBox(out,D,t-hwid,t+hwid,z1+0.17+capRise*f0,z1+0.17+capRise*f1+0.04,-0.1,0.22,'roof',0.35+i*0.1); }
    }
    sash(out,D,t,zBase+0.22,0.66,h-0.72,b,{arch:kind==='eyebrow'?'round':'segmental',shutters:false,hood:false,dress:'trim'});
  }
  // Wall dormer: the WALL ITSELF carried up through the eaves under its own coped gable — the
  // baronial signature, and the reason its attic storey reads from the street. Built on a plane
  // pushed clear of the eaves overhang so the gable breaks the roofline instead of sinking into it;
  // the slate behind the coping is the main roof, which is exactly how the real thing works.
  function wallDormer(out,P,t,b,eaveZ,pitch){
    const wid=2.15, dp=b.ov+0.14;
    const D=P_(P.ax+P.nx*dp,P.ay+P.ny*dp,P.bx+P.nx*dp,P.by+P.ny*dp);
    const wallTop=eaveZ+1.5, apex=wallTop+(wid/2)*Math.min(1.35,pitch);
    const tex=cladTex(b.clad);
    pWall(out,D,b.fH,wallTop,'body',tex,0.06,t-wid/2,t+wid/2);
    for(const s of [-1,1]){ const a=t+s*wid/2;   // returns back to the main wall face
      out.push(F(s>0?[pPt(P,a,0,eaveZ-2.2),pPt(P,a,0,wallTop),pPt(D,a,0,wallTop),pPt(D,a,0,eaveZ-2.2)]
                    :[pPt(D,a,0,eaveZ-2.2),pPt(D,a,0,wallTop),pPt(P,a,0,wallTop),pPt(P,a,0,eaveZ-2.2)],
        'body',s>0?0.06:-0.42,0));
      if(b.quoins) pDecal(out,D,a-s*0.34,a,b.fH,wallTop,'dress',0.22,tex_quoin(),false,0.05);
    }
    const n=b.crowStep?4:7;
    for(const s of [-1,1]) for(let i=0;i<n;i++){
      const f0=i/n, f1=(i+1)/n;
      const a0=t+s*(wid/2)*(1-f0), a1=t+s*(wid/2)*(1-f1);
      const z0=wallTop+(apex-wallTop)*f0, z1=wallTop+(apex-wallTop)*f1;
      const lo=Math.min(a0,a1), hi=Math.max(a0,a1);
      const zt=b.crowStep?Math.max(z0,z1):Math.max(z0,z1);
      pBox(out,D,lo,hi,wallTop-0.3,zt,-0.10,0.14,'body',0.05,tex);
      pBox(out,D,lo-0.05,hi+0.05,zt-0.02,zt+0.20,-0.16,0.21,'dress',0.72);
    }
    sash(out,D,t,eaveZ+0.10,0.84,1.30,b,{arch:'round',shutters:false});
    if(b.finials){ const fp=pPt(D,t,0,0); finial(out,fp[0],fp[1],apex+0.16,0.8,'dress'); }
    return apex;
  }

  // ---- projecting bays -------------------------------------------------------------------------
  // canted two-storey bay: 3 faces on 45deg cheeks, own cornice + hipped cap
  function cantedBay(out,P,t,b,z0,zTop){
    const open=3.4, front=1.9, dp=1.05;
    const t0=t-open/2, t1=t+open/2, f0=t-front/2, f1=t+front/2;
    const A=P_(P.ax+P.ux*t1+P.nx*0, P.ay+P.uy*t1, P.ax+P.ux*f1+P.nx*dp, P.ay+P.uy*f1+P.ny*dp);
    const Fr=P_(P.ax+P.ux*f1+P.nx*dp, P.ay+P.uy*f1+P.ny*dp, P.ax+P.ux*f0+P.nx*dp, P.ay+P.uy*f0+P.ny*dp);
    const Bk=P_(P.ax+P.ux*f0+P.nx*dp, P.ay+P.uy*f0+P.ny*dp, P.ax+P.ux*t0, P.ay+P.uy*t0);
    const tex=cladTex(b.clad);
    // plinth under the bay
    for(const Q of [A,Fr,Bk]){ pWall(out,Q,0,b.fH,'plinth',b.rustBase?tex_rust():tex_ashlar(),-0.1);
      pWall(out,Q,b.fH,zTop,'body',tex); }
    if(b.waterTable) for(const Q of [A,Fr,Bk]) pBox(out,Q,0,Q.L,b.fH-0.14,b.fH+0.16,0,0.20,'dress',0.55);
    for(const Q of [A,Fr,Bk]){
      if(b.quoins){ pDecal(out,Q,0,0.62,b.fH,zTop,'dress',0.25,tex_quoin(),false,0.05);
                    pDecal(out,Q,Q.L-0.62,Q.L,b.fH,zTop,'dress',0.25,tex_quoin(),false,0.05); }
      pBox(out,Q,0,Q.L,zTop-0.62,zTop-0.14,0,0.16,'trim',0.35,b.dentils?tex_dentil():null);
      pBox(out,Q,0,Q.L,zTop-0.14,zTop+0.16,0,0.44,'trim',0.6);
    }
    for(let s=0;s<Math.max(1,b.storeys-((b.storeys>2)?1:0));s++){
      const z=b.fH+s*b.storeyH+0.95;
      sash(out,Fr,Fr.L/2,z,1.05,b.storeyH*0.56,b,{shutters:false});
      sash(out,A,A.L/2,z,0.66,b.storeyH*0.56,b,{shutters:false,hood:false});
      sash(out,Bk,Bk.L/2,z,0.66,b.storeyH*0.56,b,{shutters:false,hood:false});
    }
    // hipped cap over the bay + a balustrade parapet
    const cA=pPt(P,t0,0,0), cB=pPt(P,t1,0,0), nA=pPt(P,t0,dp+0.2,0);
    const xs=[cA[0],cB[0],nA[0],pPt(P,t1,dp+0.2,0)[0]], ys=[cA[1],cB[1],nA[1],pPt(P,t1,dp+0.2,0)[1]];
    hipBand(out,Math.min(...xs),Math.max(...xs),Math.min(...ys),Math.max(...ys),zTop+0.16,0.42,0.34,'roof',0,tex_slate());
    if(b.balustrade) balustrade(out,Fr,zTop+0.5,0.62,0.1,Fr.L-0.1,'dress');
    return { front:Fr, zTop };
  }
  // stone oriel: projecting bay carried on corbels, one storey up. Baronial.
  function oriel(out,P,t,b,zSill){
    const open=2.6, front=1.6, dp=1.20, hgt=b.storeyH*0.9;
    const f0=t-front/2, f1=t+front/2;
    for(let i=0;i<4;i++){   // stepped corbel bracket carrying it off the wall
      const g=0.44-i*0.11, dd=dp*(0.24+i*0.20);
      pBox(out,P,t-open/2+g,t+open/2-g,zSill-1.28+i*0.32,zSill-1.28+(i+1)*0.32,0,dd,'dress',0.24+i*0.14);
    }
    const A=P_(P.ax+P.ux*(t+open/2), P.ay+P.uy*(t+open/2), P.ax+P.ux*f1+P.nx*dp, P.ay+P.uy*f1+P.ny*dp);
    const Fr=P_(P.ax+P.ux*f1+P.nx*dp, P.ay+P.uy*f1+P.ny*dp, P.ax+P.ux*f0+P.nx*dp, P.ay+P.uy*f0+P.ny*dp);
    const Bk=P_(P.ax+P.ux*f0+P.nx*dp, P.ay+P.uy*f0+P.ny*dp, P.ax+P.ux*(t-open/2), P.ay+P.uy*(t-open/2));
    for(const Q of [A,Fr,Bk]){
      pWall(out,Q,zSill,zSill+hgt,'dress',tex_ashlar(),0.12);
      pBox(out,Q,0,Q.L,zSill+hgt,zSill+hgt+0.24,0,0.26,'dress',0.75);   // coped cap
      pBox(out,Q,0,Q.L,zSill-0.20,zSill+0.06,0,0.22,'dress',0.55);      // sill course
    }
    sash(out,Fr,Fr.L/2,zSill+0.45,1.0,hgt-0.85,b,{arch:'round',shutters:false});
    sash(out,A,A.L/2,zSill+0.45,0.5,hgt-0.85,b,{shutters:false,hood:false});
    sash(out,Bk,Bk.L/2,zSill+0.45,0.5,hgt-0.85,b,{shutters:false,hood:false});
    // small slated pent roof
    const p1=pPt(Fr,0,0,0), p2=pPt(Fr,Fr.L,0,0), q1=pPt(P,t+open/2,0,0), q2=pPt(P,t-open/2,0,0);
    quad(out,[p1[0],p1[1],zSill+hgt+0.20],[p2[0],p2[1],zSill+hgt+0.20],
             [q2[0],q2[1],zSill+hgt+0.85],[q1[0],q1[1],zSill+hgt+0.85],'roof',0.3,tex_slate());
    return { front:Fr, top:zSill+hgt };
  }

  // ---- resolve / dims --------------------------------------------------------------------------
  function resolve(opts){
    opts=opts||{};
    const tierKey = TIERS[opts.tier]?opts.tier:'secondEmpire';
    const T = TIERS[tierKey];
    const g=(k,d)=> opts[k]!=null?opts[k]:(T[k]!=null?T[k]:d);
    const size = opts.size!=null?opts.size:0.5;
    const b={
      tier:tierKey, label:T.label, size,
      clad:g('clad','brick'), body:g('body','brownstone'), dress:g('dress','ashlarGrey'),
      roofMat:g('roofMat','slate'), roofForm:g('roofForm','mansard'),
      accent:g('accent','tower'), bay:g('bay','canted'), porte:!!g('porte',false),
      storeys:Math.max(2,Math.min(3,g('storeys',2)|0)),
      storeyH:g('storeyH',3.5), fH:g('fH',1.0), wallT:g('wallT',0.5), ov:g('ov',0.6),
      cornH:g('cornH',0.6), friezeH:g('friezeH',0.6),
      mansRise:g('mansRise',2.6), mansIns:g('mansIns',0.44), deckRise:g('deckRise',0.5), deckIns:g('deckIns',2.3),
      pitch:g('pitch',1.4),
      quoins:g('quoins',true), stringCourse:g('stringCourse',true), waterTable:g('waterTable',true),
      rustBase:g('rustBase',false), brackets:g('brackets',false), dentils:g('dentils',false),
      shutters:g('shutters',false), hoods:g('hoods',true), crestingOn:g('crestingOn',false),
      finials:g('finials',true), balustrade:g('balustrade',false), crowStep:g('crowStep',false),
      bartizan:g('bartizan',false), corbelEaves:g('corbelEaves',false),
      winStyle:g('winStyle','twoOverTwo'), arch:g('arch','none'), winDensity:g('winDensity',0.6),
      dormers:Math.max(0,Math.min(4,g('dormers',3)|0)),
      chimneys:Math.max(0,Math.min(3,g('chimneys',2)|0)),
      door:g('door','plum'),
      weather:opts.weather!=null?opts.weather:T.weather,
      lit:opts.lit!=null?opts.lit:0.6, night:!!opts.night,
      outline:opts.outline!=null?!!opts.outline:KEYLINE_DEFAULT,
    };
    b.Wd = 11.5 + size*5.5;               // 11.5 -> 17 m across
    b.Ln = 15.0 + size*7.0;               // 15   -> 22 m deep
    b.wallH = b.storeys*b.storeyH;
    b.eaveZ = b.fH + b.wallH;
    if(b.roofForm==='mansard'){
      b.breakZ = b.eaveZ + b.cornH*0.58 + b.mansRise;
      b.deckZ  = b.breakZ + b.deckRise;
      b.topZ   = b.deckZ + (b.crestingOn?0.62:0);
    } else if(b.roofForm==='gable'){
      b.rise = (b.Wd/2)*b.pitch;
      b.ridgeZ = b.eaveZ + b.rise;
      b.topZ = b.ridgeZ;
    } else {
      b.rise = (b.Wd/2)*0.62;
      b.ridgeZ = b.eaveZ + b.rise;
      b.topZ = b.ridgeZ;
    }
    b.surfaceCalm=Math.max(0,Math.min(1,Number(opts.surfaceCalm)||0));
    return b;
  }
  function dims(opts){
    const b=resolve(opts);
    return { Wd:b.Wd, Ln:b.Ln, storeys:b.storeys, levels:b.storeys+1, storeyH:b.storeyH,
      fH:b.fH, eaveZ:b.eaveZ, topZ:b.topZ, area:+(b.Wd*b.Ln).toFixed(1), label:b.label };
  }

  // ---- materials (weathering / night are ramp transforms — no new colours) --------------------
  function makeMats(b){
    const wx=b.weather, night=b.night, calm=b.surfaceCalm||0;
    const stoneW=(r)=>r.map(c=>{ let x=desat(c,wx*0.4); x=mix(x,'#6d6a5e',wx*0.22); if(night)x=mix(x,'#1b2733',0.44); return x; });
    const roofW =(r)=>r.map(c=>{ let x=mix(c,'#4d5a44',wx*0.16); if(night)x=mix(x,'#141d27',0.48); return x; });
    const woodW =(r)=>r.map(c=>{ let x=mix(c,'#8a8172',wx*0.35); if(night)x=mix(x,'#1b2230',0.42); return x; });
    const bodyRamp=Array.isArray(b.body)?b.body:(BODY[b.body]||BODY.brownstone);
    const dressRamp=(BODY[b.dress]||DRESS).map(c=>mix(c,'#c0b9a8',calm*0.24));
    const trimR=TRIM.map(c=>{ let x=mix(desat(c,wx*0.3),'#c0b9a8',calm*0.24); if(night)x=mix(x,'#24303c',0.42); return x; });
    return {
      body:   { ramp: stoneW(bodyRamp) },
      dormer: { ramp: stoneW(b.tier==='secondEmpire'?TRIM:bodyRamp) },
      plinth: { ramp: stoneW(b.clad==='rubble'?RUBBLE:GRANITE) },
      dress:  { ramp: stoneW(dressRamp) },
      trim:   { ramp: trimR },
      roof:   { ramp: roofW(ROOFS[b.roofMat]||ROOFS.slate) },
      iron:   { ramp: roofW(IRON) },
      copper: { ramp: roofW(COPPER) },
      wood:   { ramp: woodW(WOOD) },
      conc:   { ramp: stoneW(CONC) },
      clay:   { ramp: stoneW(BRICKR) },
      shutter:{ ramp: woodW(b.tier==='secondEmpire'?BODY.paintedSage:WOOD) },
      door:   { ramp: night?roofW(DOORPAINTS[b.door]||DOORPAINTS.oak):(DOORPAINTS[b.door]||DOORPAINTS.oak) },
      glass:  { ramp: night?GLASSN:GLASSD, off: night?1:0 },
      glassHi:{ ramp: [ night?'#ffe6a6':GLASS_HI ] },
      reveal: { ramp: [ mix(KEY,'#2b3238',0.5) ] },
      dark:   { ramp: [KEY] },
    };
  }

  // ---- rasterizer (fleet recipe + uv interpolation + per-face tex) ----------------------------
  function paint(faces, opts, MATS){
    const B=camBasis(opts), N=W*H;
    const zbuf=new Float32Array(N).fill(Infinity);
    const dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null);
    const ibuf=new Int16Array(N);
    const nbuf=new Array(N).fill(null);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      const n=normal(rv[0],rv[1],rv[2]);
      const sh=shadeOf(n,B.se,B.ce);
      const fidx=sh*GAIN + BIAS + f.b;
      const M=MATS[f.mat]||MATS.body;
      const ramp=M.ramp, off=M.off||0, tex=f.tex, uv=f.uv, flat=f.flat;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1],0,t,t+1);
      function fillTri(a,b2,c,ia,ib,ic){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b2.sx,c.sx)));
        const maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b2.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b2.sy,c.sy)));
        const maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b2.sy,c.sy)));
        const area=(b2.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b2.sy-a.sy);
        if(Math.abs(area)<1e-6) return;
        const ua=uv?uv[ia]:null, ub=uv?uv[ib]:null, uc=uv?uv[ic]:null;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b2.sx-px)*(c.sy-py)-(c.sx-px)*(b2.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b2.d+w2*c.d, deff=d-f.db;
          const i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat;
            let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi+=tex(uu,vv); }
            let idx;
            if(flat){ idx=Math.round(fi)+off; }
            else { const base=Math.floor(fi); idx=base+((fi-base)>BAYER[x&3][y&3]?1:0)+off; }
            idx=Math.max(0,Math.min(ramp.length-1,idx));
            rbuf[i]=ramp; ibuf[i]=idx;
          }
        }
      }
    }
    return { rbuf, ibuf, nbuf, dep };
  }

  // ---- the two builders ------------------------------------------------------------------------
  function planes(b,inset){
    const hw=b.Wd/2-(inset||0), y0=-b.Ln/2+(inset||0), y1=b.Ln/2-(inset||0);
    return { hw,y0,y1,
      front:P_(hw,y1,-hw,y1), back:P_(-hw,y0,hw,y0), right:P_(hw,y0,hw,y1), left:P_(-hw,y1,-hw,y0) };
  }
  function evenTs(L,n,margin){
    const m=margin==null?1.5:margin, span=L-2*m, ts=[];
    if(n<=0) return ts;
    if(n===1) return [L/2];
    for(let i=0;i<n;i++) ts.push(m+span*(i/(n-1)));
    return ts;
  }
  function blocked(t,avoid,pad){
    for(const [a,c] of (avoid||[])) if(t>a-(pad||0.9)&&t<c+(pad||0.9)) return true;
    return false;
  }
  function wallRun(out,P,b,avoid,openings){
    const tex=cladTex(b.clad);
    pWall(out,P,0,b.fH,'plinth',b.rustBase?tex_rust():tex_ashlar(),-0.12);
    pWall(out,P,b.fH,b.eaveZ,'body',tex);
    if(b.waterTable) pBox(out,P,0,P.L,b.fH-0.16,b.fH+0.20,0,0.24,'dress',0.55);
    if(b.quoins){
      pDecal(out,P,0,1.0,b.fH,b.eaveZ,'dress',0.22,tex_quoin(),false,0.05);
      pDecal(out,P,P.L-1.0,P.L,b.fH,b.eaveZ,'dress',0.22,tex_quoin(),false,0.05);
    }
    if(b.stringCourse) for(let s=1;s<b.storeys;s++){
      const z=b.fH+s*b.storeyH-0.30;
      pBox(out,P,0,P.L,z,z+0.24,0,0.18,'dress',0.5);
    }
    // sash grid
    const n=Math.max(1,Math.round((P.L/3.0)*(0.55+b.winDensity)));
    const ts=evenTs(P.L,n,1.7);
    for(const t of ts){
      if(blocked(t,avoid,1.5)) continue;
      for(let s=0;s<b.storeys;s++){
        const z=b.fH+s*b.storeyH+(s===0?1.05:0.95);
        const wh=b.storeyH*(s===0?0.60:0.54);
        sash(out,P,t,z,s===0?0.92:0.86,wh,b,{});
      }
      if(openings) openings.push({plane:P,t:t});
    }
    return ts;
  }
  function cornice(out,P,b,z){
    if(b.friezeH>0) pBox(out,P,0,P.L,z-b.friezeH,z-0.14,0,0.16,'trim',0.3,b.dentils?tex_dentil():null);
    if(b.brackets){
      const n=Math.max(2,Math.round(P.L/1.55));
      for(let i=0;i<n;i++){ const t=P.L*((i+0.5)/n);
        for(const s of [-0.19,0.19]) pBox(out,P,t+s-0.085,t+s+0.085,z-b.friezeH-0.10,z-0.12,0,b.ov*0.68,'trim',0.05);
        pBox(out,P,t-0.34,t+0.34,z-b.friezeH-0.18,z-b.friezeH-0.04,0,b.ov*0.6,'trim',0.3);
      }
    }
    pBox(out,P,-0.1,P.L+0.1,z-0.14,z+b.cornH*0.36,0,b.ov,'trim',0.55);          // the shelf
    pBox(out,P,-0.14,P.L+0.14,z+b.cornH*0.36,z+b.cornH*0.58,0,b.ov+0.08,'trim',0.8);
  }
  function corbelCourse(out,P,b,z){
    pBox(out,P,0,P.L,z-0.34,z-0.10,0,0.20,'dress',0.35,tex_corbel());
    pBox(out,P,-0.08,P.L+0.08,z-0.10,z+0.16,0,0.32,'dress',0.6);
  }

  function buildSecondEmpire(b){
    const out=[], PL=planes(b), hw=PL.hw, y0=PL.y0, y1=PL.y1;
    const twr = b.accent==='tower', tw=Math.min(5.6,b.Wd*0.38), tProj=1.75;
    const towerT=[PL.front.L/2-tw/2, PL.front.L/2+tw/2];
    const bayOn = b.bay==='canted';
    const bayT=[PL.right.L*0.5-1.9, PL.right.L*0.5+1.9];
    const openings=[];
    // ---- basement + main mass on all four planes
    box(out,-hw-0.22,hw+0.22,y0-0.22,y1+0.22,0,b.fH*0.34,'plinth',tex_rust(),-0.18);   // plinth splay
    wallRun(out,PL.front,b,twr?[towerT]:[[PL.front.L/2-1.3,PL.front.L/2+1.3]],openings);
    wallRun(out,PL.back,b,[],openings);
    wallRun(out,PL.right,b,bayOn?[bayT]:[],openings);
    wallRun(out,PL.left,b,b.porte?[[PL.left.L*0.5-2.6,PL.left.L*0.5+2.6]]:[],openings);
    for(const P of [PL.front,PL.back,PL.right,PL.left]) cornice(out,P,b,b.eaveZ);
    // ---- mansard roof storey. TWO steep bands, the lower nearly vertical and the upper kicked
    // back: that convex (bell) profile is what separates a Second Empire mansard from a plain
    // truncated hip, and the break between them reads as a line even at 20 px.
    const mx=hw+0.30, my=b.Ln/2+0.30, mz=b.eaveZ+b.cornH*0.58;
    const r1=b.mansRise*0.60, i1=b.mansIns*0.26, r2=b.mansRise-r1, i2=b.mansIns-i1;
    hipBand(out,-mx,mx,-my,my,mz,i1,r1,'roof',0,tex_slate());
    const mx2=mx-i1, my2=my-i1;
    for(const P of [P_(mx2,my2,-mx2,my2),P_(-mx2,-my2,mx2,-my2),P_(mx2,-my2,mx2,my2),P_(-mx2,my2,-mx2,-my2)])
      pBox(out,P,0,P.L,mz+r1-0.10,mz+r1+0.10,0,0.14,'dress',0.6);
    hipBand(out,-mx2,mx2,-my2,my2,mz+r1,i2,r2,'roof',0.08,tex_slate());
    const bx=mx2-i2, by=my2-i2;
    for(const P of [P_(bx,by,-bx,by),P_(-bx,-by,bx,-by),P_(bx,-by,bx,by),P_(-bx,by,-bx,-by)])
      pBox(out,P,0,P.L,b.breakZ-0.18,b.breakZ+0.14,0,0.22,'dress',0.6);         // the kick / break band
    hipBand(out,-bx,bx,-by,by,b.breakZ+0.12,b.deckIns,b.deckRise,'roof',0.1,tex_seam());
    slab(out,-bx+b.deckIns,bx-b.deckIns,-by+b.deckIns,by-b.deckIns,b.deckZ+0.12,'roof',0.2,tex_seam());
    if(b.crestingOn) cresting(out,-bx+b.deckIns+0.1,bx-b.deckIns-0.1,-by+b.deckIns+0.1,by-b.deckIns-0.1,b.deckZ+0.12);
    if(b.finials) for(const sx of [-1,1]) for(const sy of [-1,1])
      finial(out,sx*(bx-0.28),sy*(by-0.28),b.breakZ+0.10,1.35,'iron');
    // ---- dormers in the mansard, front / sides / back
    const md=b.dormers;
    const mansPlanes=[[P_(mx,my,-mx,my),Math.max(0,md)],[P_(mx,-my,mx,my),Math.max(0,md-1)],
                      [P_(-mx,my,-mx,-my),Math.max(0,md-1)],[P_(-mx,-my,mx,-my),Math.max(0,md-2)]];
    mansPlanes.forEach(([P,cnt],pi)=>{
      const ts=evenTs(P.L,cnt,2.4);
      for(const t of ts){
        if(pi===0&&twr&&t>towerT[0]-1.4&&t<towerT[1]+1.4) continue;
        mansardDormer(out,P,t,mz+0.42,2.05,b,'segmental');
      }
    });
    // ---- canted bay on the +X flank
    if(bayOn) cantedBay(out,PL.right,PL.right.L*0.5,b,0,b.fH+Math.min(2,b.storeys)*b.storeyH-0.35);
    // ---- entry tower: one storey taller, own mansard cap, oculus, balcony, doors
    if(twr){
      const tx0=-tw/2, tx1=tw/2, ty0=y1-0.4, ty1=y1+tProj;
      const tEave=b.eaveZ+b.storeyH*0.86;
      const T={ front:P_(tx1,ty1,tx0,ty1), right:P_(tx1,ty0,tx1,ty1), left:P_(tx0,ty1,tx0,ty0) };
      box(out,tx0-0.2,tx1+0.2,ty0,ty1+0.2,0,b.fH*0.34,'plinth',tex_rust(),-0.18);
      for(const k in T){ const P=T[k];
        pWall(out,P,0,b.fH,'plinth',b.rustBase?tex_rust():tex_ashlar(),-0.12);
        pWall(out,P,b.fH,tEave,'body',cladTex(b.clad));
        if(b.waterTable) pBox(out,P,0,P.L,b.fH-0.16,b.fH+0.20,0,0.24,'dress',0.55);
        if(b.quoins){ pDecal(out,P,0,0.9,b.fH,tEave,'dress',0.22,tex_quoin(),false,0.05);
                      pDecal(out,P,P.L-0.9,P.L,b.fH,tEave,'dress',0.22,tex_quoin(),false,0.05); }
        if(b.stringCourse) for(let s=1;s<=b.storeys;s++){ const z=b.fH+s*b.storeyH-0.30;
          pBox(out,P,0,P.L,z,z+0.24,0,0.18,'dress',0.5); }
        cornice(out,P,b,tEave);
      }
      // tower windows: one tall sash per stage on the front, singles on the returns, and a top
      // stage above the main eaves — the stage that turns a projecting bay into a tower
      for(let s=1;s<=b.storeys;s++){
        const z=b.fH+s*b.storeyH+0.75, wh=b.storeyH*0.56;
        sash(out,T.front,T.front.L/2,z,1.05,wh,b,{shutters:false});
        sash(out,T.right,T.right.L*0.55,z,0.6,wh,b,{shutters:false,hood:false});
        sash(out,T.left,T.left.L*0.45,z,0.6,wh,b,{shutters:false,hood:false});
      }
      const tz=b.eaveZ+0.42, twh=b.storeyH*0.50;
      sash(out,T.front,T.front.L/2,tz,1.0,twh,b,{shutters:false});
      sash(out,T.right,T.right.L*0.55,tz,0.55,twh,b,{shutters:false,hood:false});
      sash(out,T.left,T.left.L*0.45,tz,0.55,twh,b,{shutters:false,hood:false});
      // entry: double doors + fanlight, steps, balustraded balcony above
      doorway(out,T.front,T.front.L/2,b.fH,1.9,2.5,b,{});
      steps(out,T.front,T.front.L/2,b.fH,2.7,3);
      if(b.balustrade){
        pBox(out,T.front,-0.25,T.front.L+0.25,b.fH+b.storeyH-0.24,b.fH+b.storeyH-0.06,0,0.85,'dress',0.5);
        balustrade(out,P_(tx1+0.6,ty1+0.62,tx0-0.6,ty1+0.62),b.fH+b.storeyH-0.06,0.72,0,tw+1.2,'dress');
      }
      // tower mansard cap + oculus + finial
      const tmz=tEave+b.cornH*0.58, tbreak=tmz+b.mansRise*1.06;
      hipBand(out,tx0-0.3,tx1+0.3,ty0,ty1+0.3,tmz,0.40,b.mansRise*1.06,'roof',0,tex_slate());
      const ox=tx1+0.3-0.40, oy=ty1+0.3-0.40;
      hipBand(out,tx0+0.1,ox,ty0+0.1,oy,tbreak,Math.min(1.15,tw*0.30),0.95,'roof',0.1,tex_seam());
      const Oc=P_(ox,oy,tx0+0.1,oy);
      pDecal(out,Oc,Oc.L/2-0.42,Oc.L/2+0.42,tmz+0.6,tmz+1.44,'dress',0.5,null,true,0.06);
      pDecal(out,Oc,Oc.L/2-0.31,Oc.L/2+0.31,tmz+0.71,tmz+1.33,'glass',0,null,true,0.09);
      if(b.crestingOn) cresting(out,tx0+0.25,ox-0.15,ty0+0.25,oy-0.15,tbreak+0.95);
      finial(out,0,(ty0+oy)/2,tbreak+0.98,2.35,'iron');
    } else {
      doorway(out,PL.front,PL.front.L/2,b.fH,1.8,2.45,b,{});
      steps(out,PL.front,PL.front.L/2,b.fH,2.6,3);
      if(b.balustrade){
        const pP=P_(2.0,y1+1.5,-2.0,y1+1.5);
        slab(out,-2.0,2.0,y1,y1+1.5,b.fH,'dress',0.2,tex_ashlar());
        balustrade(out,pP,b.fH,0.68,0,pP.L,'dress');
        for(const sx of [-1.75,1.75]) box(out,sx-0.22,sx+0.22,y1+1.1,y1+1.5,b.fH,b.fH+2.9,'trim',null,0.3);
        slab(out,-2.2,2.2,y1+0.9,y1+1.7,b.fH+2.9,'trim',0.4);
        pBox(out,P_(2.2,y1+1.7,-2.2,y1+1.7),0,4.4,b.fH+2.9,b.fH+3.2,0,0.2,'trim',0.6,b.dentils?tex_dentil():null);
      }
    }
    // ---- porte-cochere on the -X flank
    if(b.porte){
      const px0=-hw-3.6, px1=-hw, pcy=0, pw=4.8, pz=b.fH+b.storeyH*0.96;
      slab(out,px0,px1,pcy-pw/2,pcy+pw/2,b.fH*0.5,'conc',0.1,tex_ashlar());
      for(const sx of [px0+0.42,px1-0.55]) for(const sy of [pcy-pw/2+0.5,pcy+pw/2-0.5]){
        box(out,sx-0.155,sx+0.155,sy-0.155,sy+0.155,b.fH*0.5,pz-0.3,'trim',null,0.25);
        box(out,sx-0.235,sx+0.235,sy-0.235,sy+0.235,pz-0.3,pz,'trim',null,0.45);
        box(out,sx-0.235,sx+0.235,sy-0.235,sy+0.235,b.fH*0.5,b.fH*0.5+0.24,'trim',null,0.35);
      }
      slab(out,px0-0.3,px1,pcy-pw/2-0.3,pcy+pw/2+0.3,pz,'roof',0.25,tex_seam());
      for(const P of [P_(px0-0.3,pcy+pw/2+0.3,px1,pcy+pw/2+0.3),P_(px0-0.3,pcy-pw/2-0.3,px0-0.3,pcy+pw/2+0.3),P_(px1,pcy-pw/2-0.3,px0-0.3,pcy-pw/2-0.3)])
        pBox(out,P,0,P.L,pz-0.34,pz+0.06,0,0.18,'trim',0.5,b.dentils?tex_dentil():null);
      if(b.balustrade) balustrade(out,P_(px0-0.3,pcy+pw/2+0.3,px1,pcy+pw/2+0.3),pz+0.06,0.6,0.2,3.2,'trim');
      doorway(out,PL.left,PL.left.L*0.5,b.fH,1.3,2.2,b,{});
    }
    // ---- corbelled stacks straddling the mansard
    const nc=b.chimneys;
    const stz=b.deckZ+2.70;
    if(nc>=1) stack(out,-hw*0.55,y0+b.Ln*0.24,b.eaveZ-1.2,stz,false,'body');
    if(nc>=2) stack(out, hw*0.55,y1-b.Ln*0.28,b.eaveZ-1.2,stz,false,'body');
    if(nc>=3) stack(out,-hw*0.55,y1-b.Ln*0.30,b.eaveZ-1.2,stz,false,'body');
    return out;
  }

  function buildBaronial(b){
    const out=[], PL=planes(b), hw=PL.hw, y0=PL.y0, y1=PL.y1;
    const turOn=b.accent==='turret', tr=1.75;
    const tcx=hw-0.35, tcy=y1-0.35;
    const orielOn=b.bay==='oriel';
    const openings=[];
    // battered rubble plinth: bottom wider than top
    for(const P of [PL.front,PL.back,PL.right,PL.left]){
      const p0=pPt(P,0,0.34,0), p1=pPt(P,P.L,0.34,0), q0=pPt(P,0,0,b.fH), q1=pPt(P,P.L,0,b.fH);
      quad(out,[p0[0],p0[1],0],[p1[0],p1[1],0],[q1[0],q1[1],b.fH],[q0[0],q0[1],b.fH],'plinth',-0.1,tex_rubble());
    }
    const avoidF=[], avoidR=[];
    if(turOn){ avoidF.push([PL.front.L-2*tr-0.4, PL.front.L]); avoidR.push([PL.right.L-2*tr-0.4, PL.right.L]); }
    if(orielOn) avoidF.push([PL.front.L*0.28-1.8, PL.front.L*0.28+1.8]);
    avoidF.push([PL.front.L/2-1.5,PL.front.L/2+1.5]);
    wallRun(out,PL.front,b,avoidF,openings);
    wallRun(out,PL.back,b,[],openings);
    wallRun(out,PL.right,b,avoidR,openings);
    wallRun(out,PL.left,b,[],openings);
    if(b.corbelEaves) for(const P of [PL.right,PL.left]) corbelCourse(out,P,b,b.eaveZ);
    // ---- steep slate gable roof, ridge along Y
    const ov=b.ov, rTex=tex_slate();
    quad(out,[-hw-ov,y0-ov,b.eaveZ],[-hw-ov,y1+ov,b.eaveZ],[0,y1+ov,b.ridgeZ],[0,y0-ov,b.ridgeZ],'roof',-0.05,rTex);
    quad(out,[hw+ov,y1+ov,b.eaveZ],[hw+ov,y0-ov,b.eaveZ],[0,y0-ov,b.ridgeZ],[0,y1+ov,b.ridgeZ],'roof',0.18,rTex);
    box(out,-0.11,0.11,y0-ov,y1+ov,b.ridgeZ-0.12,b.ridgeZ+0.10,'dress',null,0.05); // ridge course
    // ---- gable ends: wall + crow steps
    for(const [yv,ny] of [[y0,-1],[y1,1]]){
      const P = ny>0?P_(hw,yv,-hw,yv):P_(-hw,yv,hw,yv);
      const uv = ny>0?[[0,b.eaveZ],[2*hw,b.eaveZ],[hw,b.ridgeZ]]:[[0,b.eaveZ],[2*hw,b.eaveZ],[hw,b.ridgeZ]];
      out.push(F([pPt(P,0,0,b.eaveZ),pPt(P,P.L,0,b.eaveZ),pPt(P,P.L/2,0,b.ridgeZ)],'body',0,0,uv,cladTex(b.clad)));
      if(b.crowStep){
        const n=7;
        for(let s of [-1,1]) for(let i=0;i<n;i++){
          const f0=i/n, f1=(i+1)/n;
          const t0=P.L/2 + s*(P.L/2)*(1-f1), t1=P.L/2 + s*(P.L/2)*(1-f0);
          const zTop=b.eaveZ+(b.ridgeZ-b.eaveZ)*f1;
          const a=Math.min(t0,t1), c=Math.max(t0,t1);
          pBox(out,P,a,c,b.eaveZ-0.3,zTop,-0.30,0.34,'body',0.05,cladTex(b.clad));
          pBox(out,P,a-0.06,c+0.06,zTop,zTop+0.20,-0.36,0.40,'dress',0.7);
        }
      } else {
        pBox(out,P,-0.1,P.L+0.1,b.eaveZ-0.28,b.eaveZ,-0.2,0.26,'dress',0.5);
      }
      // gable sash + a vent slit at the apex
      sash(out,P,P.L/2,b.eaveZ+0.55,0.72,1.3,b,{arch:'round'});
    }
    // ---- wall dormers on the eave walls
    const nd=b.dormers;
    for(const [P,cnt] of [[PL.right,Math.ceil(nd/2)],[PL.left,Math.floor(nd/2)]]){
      const ts=evenTs(P.L,cnt,3.2);
      for(const t of ts){ if(turOn&&P===PL.right&&t>P.L-2*tr-1.4) continue; wallDormer(out,P,t,b,b.eaveZ,b.pitch); }
    }
    // ---- turret at the front-right corner, bartizan at the front-left
    if(turOn) turret(out,tcx,tcy,tr,0,b.eaveZ+2.0,b,{});
    if(b.bartizan) bartizan(out,-hw+0.25,y1-0.25,0.95,b.eaveZ-0.2,b);
    // ---- oriel + arched entry on the front
    if(orielOn) oriel(out,PL.front,PL.front.L*0.28,b,b.fH+b.storeyH+0.35);
    doorway(out,PL.front,PL.front.L/2,b.fH,1.75,2.4,b,{arch:'round'});
    steps(out,PL.front,PL.front.L/2,b.fH,2.5,3);
    // entry hood on two corbels, then the heraldic panel above it
    const dc=PL.front.L/2, dTop=b.fH+2.4+1.75*0.44+0.26;
    for(const s of [-1,1]) pBox(out,PL.front,dc+s*1.06-0.13,dc+s*1.06+0.13,dTop-0.66,dTop,0,0.46,'dress',0.15);
    pBox(out,PL.front,dc-1.30,dc+1.30,dTop,dTop+0.27,0,0.60,'dress',0.78);
    pBox(out,PL.front,dc-0.55,dc+0.55,dTop+0.50,dTop+1.42,0,0.15,'dress',0.6);
    pDecal(out,PL.front,dc-0.42,dc+0.42,dTop+0.61,dTop+1.31,'reveal',-0.6,null,true,0.06,0.15);
    // ---- clustered stacks on the gable apexes + one on the ridge
    if(b.chimneys>=1) stack(out,0,y0-0.15,b.ridgeZ-2.6,b.ridgeZ+2.35,true,'body');
    if(b.chimneys>=2) stack(out,0,y1+0.15,b.ridgeZ-2.6,b.ridgeZ+2.05,true,'body');
    if(b.chimneys>=3) stack(out,0,y0+b.Ln*0.42,b.ridgeZ-0.9,b.ridgeZ+1.65,false,'body');
    return out;
  }

  function build(b){ return b.tier==='baronial'?buildBaronial(b):buildSecondEmpire(b); }

  // ---- weathering / night post pass + RGBA ----------------------------------------------------
  function post(bufs,b){
    const { rbuf, ibuf, nbuf, dep } = bufs;
    const N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j;
          out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; }
      }
    }
    const wx=b.weather;
    if(wx>0.02){
      const rnd=mulberry32(4711|((b.size*131)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='body'||m==='plinth') && rnd()<wx*0.07) out[i]=rbuf[i][Math.max(0,ibuf[i]-1)];
        if(m==='roof' && rnd()<wx*0.032) out[i]=mix(out[i],'#47543c',0.26+rnd()*0.16);
        if(m==='dress' && rnd()<wx*0.03) out[i]=mix(out[i],'#5c6350',0.22);
      }
    }
    if(b.night){
      for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x;
        if(nbuf[i]==='glass'){ for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*W+(x+dx);
          if(out[j]&&nbuf[j]!=='glass'&&nbuf[j]!=='glassHi') out[j]=mix(out[j],'#f0c66a',0.26); } } }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue;
      let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; }
    }
    if(b.outline){
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue;
        let touch=false;
        for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
          if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
        if(touch) out[i]=KEY;
      }
    }
    return out;
  }
  function toRGBA(cols){
    const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; }
      const [r,g,bl]=hex2rgb(c); rgba[i*4]=r; rgba[i*4+1]=g; rgba[i*4+2]=bl; rgba[i*4+3]=255; }
    return rgba;
  }

  function render(dir,opts){
    opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const b=resolve(opts);
    const MATS=makeMats(b);
    let faces=build(b);
    if(root.CoastalPass) faces=root.CoastalPass.apply('manor',faces,MATS,b,opts);
    const bufs=paint(faces,{dir,elev:opts.elev},MATS);
    return toRGBA(post(bufs,b));
  }

  // ---- registration contracts ------------------------------------------------------------------
  // shell(): everything an interior pass needs, in the SAME metres this bake uses. It measures
  // nothing — the interior rig reads these numbers and registers under the shell to the pixel.
  function shell(opts){
    const b=resolve(opts), t=b.wallT, hw=b.Wd/2, y0=-b.Ln/2, y1=b.Ln/2;
    const levels=[];
    levels.push({ id:'basement', kind:'service', floorZ:0, ceilZ:b.fH, headroom:b.fH,
      box:{x0:-hw+t,x1:hw-t,y0:y0+t,y1:y1-t}, note:'raised basement inside the rusticated plinth' });
    for(let s=0;s<b.storeys;s++){
      levels.push({ id:'level'+s, kind:s===0?'reception':'chamber',
        floorZ:b.fH+s*b.storeyH, ceilZ:b.fH+(s+1)*b.storeyH-0.32, headroom:b.storeyH-0.32,
        box:{x0:-hw+t,x1:hw-t,y0:y0+t,y1:y1-t} });
    }
    if(b.roofForm==='mansard'){
      levels.push({ id:'mansard', kind:'attic', floorZ:b.eaveZ, ceilZ:b.breakZ-0.2, headroom:b.breakZ-0.2-b.eaveZ,
        box:{x0:-hw+t+b.mansIns,x1:hw-t-b.mansIns,y0:y0+t+b.mansIns,y1:y1-t-b.mansIns},
        note:'walls rake in on the mansard slope; dormers give the only full-height reveals' });
    } else {
      levels.push({ id:'attic', kind:'attic', floorZ:b.eaveZ, ceilZ:b.ridgeZ-0.3, headroom:b.ridgeZ-0.3-b.eaveZ,
        box:{x0:-hw+t,x1:hw-t,y0:y0+t,y1:y1-t}, slope:{pitch:b.pitch, ridgeX:0},
        note:'sloped ceiling either side of the ridge; wall dormers give standing height at the eaves' });
    }
    const openings=[];
    const push=(kind,x,y,z,w,h,facing)=>openings.push({kind,x,y,z,w,h,facing});
    push('door',0,y1,b.fH,b.tier==='baronial'?1.75:1.9,2.45,'+Y');
    const PL=planes(b);
    for(const [P,f] of [[PL.front,'+Y'],[PL.back,'-Y'],[PL.right,'+X'],[PL.left,'-X']]){
      const n=Math.max(1,Math.round((P.L/3.0)*(0.55+b.winDensity)));
      for(const t2 of evenTs(P.L,n,1.7)) for(let s=0;s<b.storeys;s++){
        const p=pPt(P,t2,0,0);
        push('window',+p[0].toFixed(2),+p[1].toFixed(2),b.fH+s*b.storeyH+(s===0?1.05:0.95),
          s===0?0.92:0.86, b.storeyH*(s===0?0.6:0.54), f);
      }
    }
    return { tier:b.tier, wallT:t, partyT:0, Wd:b.Wd, Ln:b.Ln, fH:b.fH, storeyH:b.storeyH,
      storeys:b.storeys, eaveZ:b.eaveZ, topZ:b.topZ, roofForm:b.roofForm,
      accent: b.accent==='turret'            ? {kind:'turret',cx:b.Wd/2-0.35,cy:b.Ln/2-0.35,r:1.75,rIn:1.75-t,top:b.eaveZ+2.0}
            : b.accent==='tower'?{kind:'tower',w:Math.min(5.6,b.Wd*0.38),proj:1.75,top:b.eaveZ+b.storeyH*0.86}
            : null,
      levels, openings, ext:'ManorIso', extSize:b.size };
  }
  // units(): the gameplay table. One household, many levels — the manor's answer to the terrace's
  // per-bay unit list.
  function units(opts){
    const b=resolve(opts), sh=shell(opts);
    const area=(L)=>+((L.box.x1-L.box.x0)*(L.box.y1-L.box.y0)).toFixed(1);
    return [{
      id:'manor', label:b.label, beds:b.storeys>2?7:5, levels:sh.levels.length,
      floorArea: sh.levels.reduce((s,L)=>s+area(L),0),
      perLevel: sh.levels.map(L=>({id:L.id,kind:L.kind,area:area(L)})),
      entries:[{kind:'main',facing:'+Y'}].concat(b.porte?[{kind:'carriage',facing:'-X'}]:[])
             .concat([{kind:'service',facing:'-Y'}]),
      partyWalls:0, accent:sh.accent?sh.accent.kind:null,
    }];
  }
  function anchors(dir,opts){
    opts=opts||{}; const b=resolve(opts), B=camBasis({dir,elev:opts.elev});
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return {x:v.sx,y:v.sy}; };
    const hw=b.Wd/2, y0=-b.Ln/2, y1=b.Ln/2, st=[];
    if(b.tier==='baronial'){
      if(b.chimneys>=1) st.push(pj(0,y0-0.15,b.ridgeZ+2.4));
      if(b.chimneys>=2) st.push(pj(0,y1+0.15,b.ridgeZ+2.1));
      if(b.chimneys>=3) st.push(pj(0,y0+b.Ln*0.42,b.ridgeZ+1.7));
    } else {
      const stz=b.deckZ+2.70;
      if(b.chimneys>=1) st.push(pj(-hw*0.55,y0+b.Ln*0.24,stz));
      if(b.chimneys>=2) st.push(pj( hw*0.55,y1-b.Ln*0.28,stz));
      if(b.chimneys>=3) st.push(pj(-hw*0.55,y1-b.Ln*0.30,stz));
    }
    const wins=[];
    const PL=planes(b);
    for(const P of [PL.front,PL.right,PL.left,PL.back]){
      const n=Math.max(1,Math.round((P.L/3.0)*(0.55+b.winDensity)));
      for(const t of evenTs(P.L,n,1.7)) for(let s=0;s<b.storeys;s++){
        const p=pPt(P,t,0.06,0);
        wins.push(pj(p[0],p[1],b.fH+s*b.storeyH+(s===0?1.05:0.95)+b.storeyH*0.28));
      }
    }
    const tw=Math.min(5.6,b.Wd*0.38);
    return {
      chimneys:st, windows:wins,
      door: b.accent==='tower'?pj(0,y1+1.55,b.fH+1.1):pj(0,y1,b.fH+1.1),
      steps: b.accent==='tower'?pj(0,y1+2.6,0):pj(0,y1+1.1,0),
      balcony: b.accent==='tower'?pj(0,y1+2.2,b.fH+b.storeyH):null,
      accentTop: b.accent==='turret'?pj(hw-0.35,y1-0.35,b.eaveZ+2.0+1.75*2.75+1.0)
               : b.accent==='tower'?pj(0,y1+0.6,b.eaveZ+b.storeyH*0.86+b.cornH*0.58+b.mansRise*1.06+0.95+2.35)
               : pj(0,0,b.topZ),
      ridge: pj(0,0,b.roofForm==='mansard'?b.deckZ:b.ridgeZ),
      Wd:b.Wd, Ln:b.Ln, topZ:b.topZ, tw,
    };
  }
  function project(dir,p,elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.ManorIso = { W,H,PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    TIERS, CLADS, BODY, ROOFS, DRESS, TRIM, IRON, DOORPAINTS, WINDOWS, ACCENTS, BAYS, ROOFFORMS,
    PRESETS, KEY, dims, shell, units, render, anchors, project, resolved:(o)=>resolve(o) };
})(typeof globalThis!=='undefined'?globalThis:window);

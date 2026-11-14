/* Hidden Harbours — DOUBLE-CRESTED CORMORANT iso rig (Nannopterum auritum). The first diving bird.
   Same fixed 3/4 turntable as the herring gull and the fleet (45deg steps CW, elev 40, 32 px = 1 m,
   ordered dither, depth-edge darkening, no AA, RINGLESS per ADR 0031 — render(dir,{outline:true}) is
   the A/B). STRICT WORLD SCALE: 0.82 m bill-to-tail, wingspan 1.20 m (38 px). No readability floor.

   ITS OWN BODY, not a gull preset. Three things a gull body cannot give it: a long neck that bends
   (a chain, bent by two angles), legs that stay plumb under a body standing nearly upright (legs are
   resolved in the contact frame, not the bird's), and a stiff tail that props on the rock (the tail
   bends at its root). The painter, palette ramps, cell, pivot and API are the gull's, so the same
   sheet route (rows = facings, columns = sheetOrder()) bakes it.

   Cell 64x64, pivot (32,46) = the CONTACT point under the body centre: rock / perch top when
   standing, the water surface when swimming or diving, the point directly below the bird when
   airborne. Pose z = body centre over the contact plane. Pixels below waterZ bake the depth-graded
   water tint + alpha exactly as the gull does; waterZ:null = dry.

   ANIMS (n frames): fly 6 · glide 2 · dive 4 · surface 4 · splash 4 · float 2 · patter 4 · land 4 ·
     stand 2 · dry 4 · perch 2 · takeoff 4.
   Names are the gull's where the slot means the same thing (fly, glide, dive, splash, float, land,
   stand, perch, takeoff). New here: surface (comes up after a dive), patter (the run along the water
   that gets it airborne), dry (stands with its wings spread). A gull's swoop, preen, peck and walk
   are not drawn: absence is data.
   BETWEEN dive AND surface THE BIRD IS NOT DRAWN. The page hides it for the UNDER spell the sidecar
   declares and brings it up somewhere else.

   gameplayGeometry() — the sidecar generator (gameplay/cormorantIsoRig.gameplay.json): states,
   transitions, motion, UNDER, DRY, PERCH, WATER and GROUP contracts, all from the same tables the
   bake uses. birds/tools/build.cjs stamps derivedFromRigSha256 at save time.

   Exposes globalThis.CormorantIso = { W,H,pivot,KEY,WATER,KEYLINE_DEFAULT,BODY,ANIMS,AORDER,MOTION,POSE,
     GROUP,MATS,defaultElev,resolve,render,anchors,project,sheetOrder,dirOf,gameplayGeometry }. */
(function (root) {
  const S = 32, DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const W = 64, H = 64, cx = 32, cy = 46;
  const KEY = '#101a19', WATER = '#123034', KEYLINE_DEFAULT = false;

  // ---- the bird (metres, bird-local: +x right wing, +y bill, +z up; origin = body centre) ---------
  const BODY = {
    label:'DOUBLE-CRESTED CORMORANT', length_m:0.82, wingspan_m:1.20, mass_kg:1.7,
    bodyLen:0.40, girth:0.085,           // trunk loft: long and heavy, deepest ahead of the middle
    shoulder:[0.066,0.075,0.045],        // wing root (x mirrored)
    arm:0.19, hand:0.18, prim:0.19,      // wing segments, root -> tip
    chord:[0.21,0.20,0.16,0.07],         // broad, blunt wing: chord at root / elbow / wrist / tip
    neck:[0.15,0.25], neckR:[0.040,0.026], // neck length drawn-in -> stretched; radius root -> head
    headR:0.036, bill:0.062, hook:0.014, // slender head, long bill, hooked tip
    tail:0.16, tailW:[0.05,0.11],        // stiff wedge tail, width folded -> spread
    hip:[0.040,-0.125,-0.045], leg:0.085, foot:[0.075,0.055], // legs short and set far back; big webbed feet
    standZ:0.215, perchZ:0.205, floatZ:-0.030, draft:0.115,     // swims LOW: the body centre is under the surface
  };

  const ANIMS = {
    fly:{n:6,ms:80}, glide:{n:2,ms:300}, dive:{n:4,ms:110}, surface:{n:4,ms:130}, splash:{n:4,ms:110},
    float:{n:2,ms:520}, patter:{n:4,ms:80}, land:{n:4,ms:110}, stand:{n:2,ms:640}, dry:{n:4,ms:420},
    perch:{n:2,ms:640}, takeoff:{n:4,ms:100},
  };
  const AORDER = ['fly','glide','dive','surface','splash','float','patter','land','stand','dry','perch','takeoff'];
  // page-side motion contract. v m/s along heading; vz m/s (negative = descending); alt = typical
  // altitude band (m over the contact plane) the page blits at; loop or oneshot -> next.
  const MOTION = {
    fly:    { v:13.0, vz:0,    alt:[0.5,4],  loop:true,  note:'low and straight over the water, neck out; never wheels or soars' },
    glide:  { v:11.0, vz:-0.6, alt:[0.3,3],  loop:true,  note:'a short flat glide on the way in; not a soaring state' },
    dive:   { v:1.2,  vz:0,    alt:[0,0],    loop:false, next:'under', travel:0.5, note:'from float only: a forward leap, bill first, and gone. Not a plunge from the air' },
    surface:{ v:0.4,  vz:0,    alt:[0,0],    loop:false, next:'float', travel:0.3, note:'head and neck first; the page places it at the end of the UNDER spell' },
    splash: { v:3.0,  vz:0,    alt:[0,0],    loop:false, next:'float', travel:1.2, note:'water landing: feet first, skis, settles; splashRig.js burst at f1' },
    float:  { v:0.5,  vz:0,    alt:[0,0],    loop:true,  note:'swims low, bill tilted up; paddles under its own power' },
    patter: { v:7.0,  vz:0.3,  alt:[0,0.2],  loop:true,  cycles:[3,6], next:'fly', note:'the run along the water: both wings beating, feet striking; splashRig.js small burst every frame 0 and 2' },
    land:   { v:2.5,  vz:-1.6, alt:[0.6,0],  loop:false, next:'stand', travel:0.6 },
    stand:  { v:0,    vz:0,    alt:[0,0],    loop:true,  note:'nearly upright, propped on its tail' },
    dry:    { v:0,    vz:0,    alt:[0,0],    loop:true,  cycles:[20,90], note:'wings held half open to dry, slow fanning. Reached from stand and perch only' },
    perch:  { v:0,    vz:0,    alt:[0,0],    loop:true,  note:'contact plane = the perch top (piling, buoy, ledge lip)' },
    takeoff:{ v:4.0,  vz:0.8,  alt:[0,0.6],  loop:false, next:'fly', travel:1.0, note:'from stand / perch / dry: drops forward off the perch and beats away low' },
  };

  // pose scalars: pitch/roll (rad), z (m body centre over contact), dh dihedral (rad, +up), sw sweep (rad,
  // + back), fold (extra hand sweep), pdh primaries dihedral (+up), chordK (wing compression when folded),
  // lenK (wing length when folded), wingL/wingR per-side override. NECK: na / nb = elevation (rad, from
  // the body axis toward the back) of the neck at its root / at the head; nk 0..1 length; hy yaw of
  // the whole neck; hp head pitch (bill elevation from the body axis). tail 0..1 spread, ta tail bend
  // at its root (rad, + toward the back: props the tail when the body stands up). legs 0..1 (0 =
  // tucked, not drawn), stride (m fore/aft of the left foot; right mirrors), ff both feet forward (m),
  // plant true = soles on the contact plane.
  const FOLD = { dh:0.22, sw:1.48, fold:0.12, pdh:-0.06, chordK:0.50, lenK:0.50 };
  const OUT  = { na:0.16, nb:-0.04, nk:1.0, hp:0.03 };             // flight: neck out, a slight kink, head a touch high
  const UPN  = { na:1.10, nb:1.38, nk:0.85, hp:0.36 };             // swimming: neck up, bill tilted up
  const SN   = { na:0.32, nb:-0.62, nk:0.90, hp:-1.08 };           // standing (relative to an upright body): S-neck, bill level
  const DRYW = { dh:-0.04, sw:-0.25, fold:0.75, pdh:-0.20, chordK:1, lenK:0.86 }; // the spread (body upright, so -sweep lifts the arm): wrists high, primaries hanging
  const flap = (f,n)=>{ const ph=f/n*Math.PI*2, s=Math.sin(ph);
    return { dh:0.52*s+0.04, sw:0.08+0.08*Math.max(0,-s), fold:0.08+0.22*Math.max(0,-s), pdh:-0.30*s, chordK:1, z:0.015*Math.cos(ph) }; };
  const A = Object.assign;
  const POSE = {
    fly:    [0,1,2,3,4,5].map(f=>A({ pitch:0.04, roll:0, hy:0, tail:0.25, legs:0 }, OUT, flap(f,6))),
    glide:  [A({ dh:0.10,sw:0.12,fold:0.06,pdh:-0.10,chordK:1, pitch:0.03, roll:0.03, tail:0.4, legs:0, z:0 }, OUT),
             A({ dh:0.13,sw:0.12,fold:0.06,pdh:-0.13,chordK:1, pitch:0.03, roll:-0.03, tail:0.4, legs:0, z:0.01 }, OUT)],
    dive:   [A({ pitch:0.22,  z:0.005,  na:0.95, nb:0.45, nk:1, hp:0.10,  tail:0.1, legs:0, waterZ:0 }, FOLD),
             A({ pitch:-0.38, z:0.035,  na:0.10, nb:-0.95,nk:1, hp:-1.10, tail:0.1, legs:0, waterZ:0 }, FOLD),
             A({ pitch:-0.95, z:-0.045, na:0.0,  nb:-0.40,nk:1, hp:-0.45, tail:0.2, legs:0, waterZ:0 }, FOLD),
             A({ pitch:-1.10, z:-0.130, na:0.0,  nb:0.0,  nk:0.5, hp:0.0,   tail:0.3, legs:0, waterZ:0 }, FOLD)],
    surface:[A({ pitch:0.78, z:-0.235, na:0.20, nb:0.30, nk:1,    hp:0.30, tail:0.1, legs:0, waterZ:0 }, FOLD),
             A({ pitch:0.46, z:-0.125, na:0.60, nb:0.80, nk:0.95, hp:0.34, tail:0.1, legs:0, waterZ:0 }, FOLD),
             A({ pitch:0.20, z:-0.060, na:0.95, nb:1.20, nk:0.90, hp:0.36, tail:0.1, legs:0, waterZ:0 }, FOLD),
             A({ pitch:0.09, z:-0.034, hy:-0.30, tail:0.1, legs:0, waterZ:0 }, UPN, FOLD)],
    splash: [A({ dh:0.80,sw:0.05,fold:0.12,pdh:0.05,chordK:1, pitch:0.46, tail:0.8, legs:1, ff:0.09, z:0.34, waterZ:0 }, OUT, { na:0.45, hp:-0.2 }),
             A({ dh:0.90,sw:0.04,fold:0.14,pdh:0.10,chordK:1, pitch:0.38, tail:0.9, legs:1, ff:0.10, z:0.16, waterZ:0 }, OUT, { na:0.55, hp:-0.2 }),
             A({ dh:0.50,sw:0.70,fold:0.30,pdh:-0.05,chordK:0.8, lenK:0.8, pitch:0.20, tail:0.4, legs:0, z:0.035, waterZ:0, na:0.85, nb:0.9, nk:0.9, hp:0.2 }),
             A({ pitch:0.08, z:-0.030, tail:0.1, legs:0, waterZ:0 }, UPN, FOLD)],
    float:  [A({ pitch:0.08, z:-0.030, hy:0.0,  tail:0.1, legs:0, waterZ:0 }, UPN, FOLD),
             A({ pitch:0.07, z:-0.026, hy:0.38, tail:0.1, legs:0, waterZ:0 }, UPN, FOLD)],
    patter: [0,1.5,3,4.5].map((f,i)=>A({ pitch:0.16, roll:0, hy:0, tail:0.3, legs:1, stride:0.085*(i%2?-1:1), waterZ:0 }, OUT, { na:0.26 }, flap(f,6), { z:0.135+0.012*(i%2) })),
    land:   [A({ dh:0.80,sw:0.05,fold:0.12,pdh:0.05,chordK:1, pitch:0.50, tail:0.85, legs:1, ff:0.06, z:0.62 }, OUT, { na:0.40, hp:-0.3 }),
             A({ dh:0.92,sw:0.04,fold:0.15,pdh:0.10,chordK:1, pitch:0.60, tail:0.9,  legs:1, ff:0.05, z:0.42 }, OUT, { na:0.45, hp:-0.4 }),
             A({ dh:0.60,sw:0.40,fold:0.30,pdh:0.00,chordK:0.9, lenK:0.9, pitch:0.90, tail:0.5, ta:0.4, legs:1, ff:0.02, z:0.30 }, SN),
             A({ dh:0.35,sw:1.00,fold:0.25,pdh:-0.05,chordK:0.7, lenK:0.7, pitch:1.10, tail:0.2, ta:0.8, legs:1, plant:true, z:0.215 }, SN)],
    stand:  [A({ pitch:1.15, z:0.215, hy:0.0,   tail:0.15, ta:0.85, legs:1, plant:true, stride:0.012 }, SN, FOLD),
             A({ pitch:1.15, z:0.217, hy:-0.50, tail:0.15, ta:0.85, legs:1, plant:true, stride:0.012 }, SN, FOLD)],
    dry:    [A({ pitch:1.15, z:0.215, hy:0.55,  tail:0.5, ta:0.85, legs:1, plant:true, stride:0.012 }, SN, DRYW),
             A({ pitch:1.15, z:0.216, hy:0.55,  tail:0.5, ta:0.85, legs:1, plant:true, stride:0.012 }, SN, DRYW, { dh:0.03, fold:0.69 }),
             A({ pitch:1.15, z:0.215, hy:-0.25, tail:0.5, ta:0.85, legs:1, plant:true, stride:0.012 }, SN, DRYW, { dh:-0.09, fold:0.80 }),
             A({ pitch:1.15, z:0.216, hy:0.15,  tail:0.5, ta:0.85, legs:1, plant:true, stride:0.012 }, SN, DRYW, { dh:-0.01, fold:0.73 })],
    perch:  [A({ pitch:1.08, z:0.205, hy:0.45,  tail:0.15, ta:0.45, legs:1, plant:true, stride:0.0 }, SN, { nk:0.72 }, FOLD),
             A({ pitch:1.08, z:0.207, hy:-0.45, tail:0.15, ta:0.45, legs:1, plant:true, stride:0.0 }, SN, { nk:0.76 }, FOLD)],
    takeoff:[A({ dh:0.35,sw:0.70,fold:0.35,pdh:-0.05,chordK:0.8, lenK:0.85, pitch:0.55, tail:0.4, ta:0.2, legs:1, plant:true, z:0.175, na:0.30, nb:-0.10, nk:0.95, hp:-0.40 }),
             A({ dh:0.82,sw:0.12,fold:0.18,pdh:0.05,chordK:1, pitch:0.34, tail:0.6, legs:1, stride:0, z:0.24 }, OUT, { na:0.25 }),
             A({ dh:-0.30,sw:0.10,fold:0.10,pdh:-0.25,chordK:1, pitch:0.20, tail:0.5, legs:0.6, z:0.32 }, OUT),
             A({ dh:-0.50,sw:0.12,fold:0.12,pdh:-0.32,chordK:1, pitch:0.12, tail:0.4, legs:0, z:0.46 }, OUT)],
  };

  // ---- palette (fleet ramp maker; five-step ramps, as the gull) -------------------------------------
  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.12;
  const LN = (()=>{ const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  const hex2rgb=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  const rgb2hex=(r,g,b)=>'#'+[r,g,b].map(v=>Math.max(0,Math.min(255,Math.round(v))).toString(16).padStart(2,'0')).join('');
  const light=(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r+(255-r)*f, g+(255-g)*f, b+(255-b)*f); };
  const dark =(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r*f, g*f, b*f); };
  const mkRamp=(base)=>[dark(base,0.42), dark(base,0.66), base, light(base,0.17), light(base,0.34)];
  const MATS = {
    // the two plumage ramps are written out: mkRamp's top steps lift a black bird to grey
    black: { ramp:['#0d1011','#15191b','#1e2325','#293033','#374043'], off:0 },   // head, neck, underparts, flight feathers, tail (green-black gloss)
    bronze:{ ramp:['#14120e','#1e1b16','#2a261e','#373126','#453e30'], off:0 },   // mantle + upperwing coverts (bronze-brown, dark-edged)
    gular: { ramp:mkRamp('#e0892b'), off:0 },   // bare orange-yellow throat skin and lores
    bill:  { ramp:mkRamp('#7d7a68'), off:0 },   // dusky horn bill
    leg:   { ramp:mkRamp('#1c2022'), off:0 },   // black legs and webs
    eye:   { ramp:mkRamp('#3fb8aa'), off:0 },   // aquamarine eye
  };
  const RINDEX = {}; for (const m of Object.values(MATS)) m.ramp.forEach((c,i)=>{ RINDEX[c]={r:m.ramp,i}; });
  const WRGB = hex2rgb(WATER);
  const mod=(a,n)=>((a%n)+n)%n;

  // ---- camera ---------------------------------------------------------------------------------------
  function camBasis(opts){
    const dir=opts.dir||0, th=-dir*Math.PI/4, e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) };
  }
  function projVert(x,y,z,B){
    const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) };
  }
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  const shadeOf=(n,se,ce)=>n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];

  // ---- pose resolution ------------------------------------------------------------------------------
  function resolve(opts){
    opts=opts||{};
    const anim = POSE[opts.anim] ? opts.anim : 'stand', poses=POSE[anim], p=poses[mod(opts.frame||0, poses.length)];
    const r = Object.assign({ pitch:0, roll:0, dh:0, sw:0, fold:0, pdh:0, chordK:1, lenK:1, hy:0, hp:0, na:0.2, nb:0, nk:0.8,
      tail:0.1, ta:0, legs:0, stride:0, ff:0, plant:false, z:0 }, p);
    for (const k of ['pitch','roll','z','hy','hp','na','nb','nk','tail','ta','legs']) if (opts[k]!=null) r[k]=opts[k];
    r.anim=anim; r.scale=opts.scale||1;
    r.outline = opts.outline===true || (opts.outline==null && KEYLINE_DEFAULT);
    r.waterZ = opts.waterZ!==undefined ? opts.waterZ : (p.waterZ!==undefined ? p.waterZ : null);
    return r;
  }

  // ---- geometry -------------------------------------------------------------------------------------
  function facesOf(r){
    const B=BODY, k=r.scale, F=[];
    const cP=Math.cos(r.pitch), sP=Math.sin(r.pitch), cR=Math.cos(r.roll), sR=Math.sin(r.roll);
    // bird-local -> contact frame: roll about y, pitch about x, lift z
    const T=(p)=>{ const x1=p[0]*cR-p[2]*sR, z1=p[0]*sR+p[2]*cR; const y2=p[1]*cP-z1*sP, z2=p[1]*sP+z1*cP;
      return [x1*k, y2*k, z2*k + r.z]; };
    const push=(v,mat,o)=>F.push(Object.assign({v:v.map(T), mat, b:0, db:0}, o||{}));
    const pushW=(v,mat,o)=>F.push(Object.assign({v, mat, b:0, db:0}, o||{}));   // already in the contact frame
    const anchors={};

    // trunk: lathe along y, deepest ahead of the middle, long taper to the rump
    const L=B.bodyLen, g=(u)=>B.girth*Math.max(0.14, u<0.32 ? 0.60+0.40*(u/0.32) : 1-0.80*Math.pow((u-0.32)/0.68,1.5));
    const NSEG=8, NR=8, rings=[];
    for (let i=0;i<=NSEG;i++){ const u=i/NSEG, rr=g(u), y=L*(0.5-u), ring=[];
      for (let j=0;j<NR;j++){ const ph=j/NR*2*Math.PI; ring.push([Math.cos(ph)*rr*1.08, y, Math.sin(ph)*rr - rr*0.06]); }
      rings.push(ring); }
    for (let i=0;i<NSEG;i++) for (let j=0;j<NR;j++){ const j2=(j+1)%NR;
      const ms=(Math.sin(j/NR*2*Math.PI)+Math.sin(j2/NR*2*Math.PI))/2;
      push([rings[i][j],rings[i][j2],rings[i+1][j2],rings[i+1][j]], (ms>0.5 && i>0 && i<6)?'bronze':'black'); }
    const breast=[0, L*0.5+g(0)*0.7, -0.005];
    for (let j=0;j<NR;j++){ const j2=(j+1)%NR; push([breast, rings[0][j2], rings[0][j], rings[0][j]], 'black'); }
    const rump=[0, -L*0.5-g(1)*0.6, 0.0];
    for (let j=0;j<NR;j++){ const j2=(j+1)%NR; push([rump, rings[NSEG][j], rings[NSEG][j2], rings[NSEG][j2]], 'black'); }

    // neck: a chain of NN segments in the body's own vertical plane, elevation running na -> nb, then
    // yawed as one about the root. Head: its own pitch hp, rings along the bill axis.
    const nroot=[0, L*0.43, B.girth*0.28], nkL=B.neck[0]+(B.neck[1]-B.neck[0])*r.nk;
    const chy=Math.cos(r.hy), shy=Math.sin(r.hy);
    const NY=(p)=>[nroot[0]+p[0]*chy-p[1]*shy, nroot[1]+p[0]*shy+p[1]*chy, nroot[2]+p[2]];
    const NN=3, NR2=6, cpt=[[0,0,0]], cang=[];
    for (let i=0;i<NN;i++){ const a=r.na+(r.nb-r.na)*(i/(NN-1)); cang.push(a);
      const q=cpt[i]; cpt.push([0, q[1]+Math.cos(a)*nkL/NN, q[2]+Math.sin(a)*nkL/NN]); }
    const tube=(c,rr,a)=>{ const out=[]; for (let j=0;j<NR2;j++){ const ph=j/NR2*2*Math.PI;
      out.push(NY([c[0]+Math.cos(ph)*rr, c[1]-Math.sin(ph)*rr*Math.sin(a), c[2]+Math.sin(ph)*rr*Math.cos(a)])); } return out; };
    const nrings=[];
    for (let i=0;i<=NN;i++){ const a = i===0 ? cang[0] : i===NN ? cang[NN-1] : (cang[i-1]+cang[i])/2;
      const rr=B.neckR[0]+(B.neckR[1]-B.neckR[0])*(i/NN); nrings.push(tube(cpt[i], rr, a)); }
    for (let i=0;i<NN;i++) for (let j=0;j<NR2;j++){ const j2=(j+1)%NR2;
      push([nrings[i][j2],nrings[i][j],nrings[i+1][j],nrings[i+1][j2]], 'black'); }
    const hR=B.headR, hd=[0,Math.cos(r.hp),Math.sin(r.hp)], hn=[0,-Math.sin(r.hp),Math.cos(r.hp)];
    const ne=cpt[NN], hc=[0, ne[1]+hd[1]*hR*0.45, ne[2]+hd[2]*hR*0.45];
    const along=(t,up)=>[hc[0], hc[1]+hd[1]*t+hn[1]*(up||0), hc[2]+hd[2]*t+hn[2]*(up||0)];
    const hring=(t,rr)=>tube(along(t), rr, r.hp);
    const h0=hring(-hR*0.70, hR*0.72), h1=hring(0, hR), h2=hring(hR*0.85, hR*0.66);
    for (let j=0;j<NR2;j++){ const j2=(j+1)%NR2, low=(Math.sin(j/NR2*2*Math.PI)+Math.sin(j2/NR2*2*Math.PI))<-0.5;
      push([h0[j2],h0[j],h1[j],h1[j2]], 'black');
      push([h1[j2],h1[j],h2[j],h2[j2]], low?'gular':'black'); }
    const hb=NY(along(-hR*1.10)), hf=NY(along(hR*1.15,-hR*0.10));
    for (let j=0;j<NR2;j++){ const j2=(j+1)%NR2, low=(Math.sin(j/NR2*2*Math.PI)+Math.sin(j2/NR2*2*Math.PI))<0.1;
      push([hb,h0[j],h0[j2],h0[j2]],'black'); push([hf,h2[j2],h2[j],h2[j]], low?'gular':'black'); }
    // bill: slender wedge, tip dropped into the hook
    const bw=hR*0.30, bh=hR*0.30, bt=NY(along(hR*0.95+B.bill, -hR*0.12-B.hook));
    const bq=[[-bw,bh],[bw,bh],[bw,-bh],[-bw,-bh]].map(([x,u])=>{ const p=along(hR*0.90, u-hR*0.12); return NY([p[0]+x,p[1],p[2]]); });
    push([bq[0],bq[1],bt,bt],'bill',{b:0.3}); push([bq[1],bq[2],bt,bt],'bill'); push([bq[2],bq[3],bt,bt],'gular',{b:-0.3}); push([bq[3],bq[0],bt,bt],'bill');
    anchors.bill=T(bt); anchors.head=T(NY(hc));
    const eyes=[1,-1].map(s=>{ const p=along(hR*0.35, hR*0.20); return T(NY([p[0]+s*hR*0.95, p[1], p[2]])); });

    // wings (the gull's three-segment wing). Front of a quad = the underwing; back = the upperwing.
    const wing=(s, wp)=>{
      const sh=[s*B.shoulder[0], B.shoulder[1], B.shoulder[2]];
      const seg=(from, sw, dh, len)=>{ const d=[s*Math.cos(sw)*Math.cos(dh), -Math.sin(sw)*Math.cos(dh), Math.sin(dh)];
        return [from[0]+d[0]*len, from[1]+d[1]*len, from[2]+d[2]*len]; };
      const chordV=(sw)=>[s*Math.sin(sw), Math.cos(sw), 0];
      const lk=wp.lenK||1;
      const el=seg(sh, wp.sw, wp.dh, B.arm*lk);
      const wr=seg(el, wp.sw+wp.fold, wp.dh*0.6+wp.pdh*0.4, B.hand*lk);
      const tp=seg(wr, wp.sw+wp.fold*1.15, wp.pdh, B.prim*lk);
      const ck=wp.chordK, C=B.chord.map(c=>c*ck);
      const edge=(p, sw, c, lead)=>{ const v=chordV(sw); const f=lead? c*0.40 : -c*0.60; return [p[0]+v[0]*f, p[1]+v[1]*f, p[2]+v[2]*f]; };
      const q=(a,aw,ac, b,bw2,bc, under, upper)=>{
        const A0=edge(a,aw,ac,true), Ab=edge(a,aw,ac,false), Bl=edge(b,bw2,bc,true), Bb=edge(b,bw2,bc,false);
        const v = s>0 ? [A0,Bl,Bb,Ab] : [Bl,A0,Ab,Bb];
        push(v, under, { matBack:upper, ds:1, db:-0.02 });
      };
      q(sh,wp.sw,C[0], el,wp.sw,C[1], 'black','bronze');
      q(el,wp.sw+wp.fold,C[1], wr,wp.sw+wp.fold,C[2], 'black','bronze');
      q(wr,wp.sw+wp.fold*1.15,C[2], tp,wp.sw+wp.fold*1.15,C[3], 'black','black');
      return T(tp);
    };
    const wpL=Object.assign({dh:r.dh,sw:r.sw,fold:r.fold,pdh:r.pdh,chordK:r.chordK,lenK:r.lenK}, r.wingL||{});
    const wpR=Object.assign({dh:r.dh,sw:r.sw,fold:r.fold,pdh:r.pdh,chordK:r.chordK,lenK:r.lenK}, r.wingR||{});
    anchors.wingL=wing(-1,wpL); anchors.wingR=wing(1,wpR);

    // tail: a stiff wedge that bends at its root (ta), double-sided
    const tw=B.tailW[0]+(B.tailW[1]-B.tailW[0])*r.tail, t0=[0,-L*0.45,0.0], tl=B.tail, cta=Math.cos(r.ta), sta=Math.sin(r.ta);
    const tp=(x,d)=>[x, t0[1]-d*cta, t0[2]+d*sta];
    push([tp(-tw*0.30,0),tp(tw*0.30,0),tp(tw*0.5,tl*0.8),tp(-tw*0.5,tl*0.8)], 'black', {ds:1, matBack:'black', db:-0.01});
    push([tp(-tw*0.5,tl*0.8),tp(tw*0.5,tl*0.8),tp(tw*0.22,tl),tp(-tw*0.22,tl)], 'black', {ds:1, matBack:'black', db:-0.01, b:-0.4});
    anchors.tail=T(tp(0,tl));

    // legs + webbed feet, in the CONTACT frame: a cormorant's legs stay plumb under a body that stands up
    if (r.legs>0.05){
      const ext=r.legs, legL=B.leg*ext*k;
      for (const s of [-1,1]){
        const hip=T([s*B.hip[0], B.hip[1], B.hip[2]]), st=r.stride*(s<0?1:-1)+r.ff;
        const foot = r.plant ? [hip[0]+s*0.012*k, hip[1]+st+0.015*k, 0] : [hip[0]+s*0.008*k, hip[1]+st, hip[2]-legL];
        const rr=0.011*k, P=(p,dx,dy)=>[p[0]+dx,p[1]+dy,p[2]];
        pushW([P(hip,-rr,0),P(hip,rr,0),P(foot,rr,0),P(foot,-rr,0)],'leg',{ds:1,matBack:'leg',db:-0.01});
        pushW([P(hip,0,-rr),P(hip,0,rr),P(foot,0,rr),P(foot,0,-rr)],'leg',{ds:1,matBack:'leg',db:-0.01});
        if (ext>0.8){ const fl=B.foot[0]*k, fw=B.foot[1]*k;
          pushW([[foot[0],foot[1]-0.012*k,foot[2]],[foot[0]+fw/2,foot[1]+fl,foot[2]],[foot[0]-fw/2,foot[1]+fl,foot[2]]],'leg',{ds:1,matBack:'leg',b:0.5}); }
        anchors[s<0?'footL':'footR']=foot;
      }
    }
    return { F, eyes, anchors };
  }

  // ---- paint (the gull's painter) -------------------------------------------------------------------
  function _paint(r, o){
    const B=camBasis(o), {F,eyes}=facesOf(r);
    const zbuf=new Float32Array(W*H).fill(Infinity), col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H), zw=new Float32Array(W*H);
    for (const f of F){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]), mat=f.mat;
      const facing=(n[1]*B.ce - n[2]*B.se) < 0;
      if (!facing && f.ds){ n=[-n[0],-n[1],-n[2]]; mat=f.matBack||f.mat; }
      const sh=shadeOf(n,B.se,B.ce), fidx=sh*GAIN+BIAS+(f.b||0), M=MATS[mat]||MATS.black;
      let filled=0;
      for (let tt=1;tt+1<rv.length;tt++) fillTri(rv[0],rv[tt],rv[tt+1]);
      // thin double-sided members (a wing seen edge-on, the tail, a leg) fall between pixel centres:
      // when the fill left fewer pixels than the face's perimeter, trace its edges as a 1px line
      if (f.ds){ let per=0; for (let e=0;e<rv.length;e++){ const a=rv[e], b=rv[(e+1)%rv.length]; per+=Math.hypot(b.sx-a.sx,b.sy-a.sy); }
        if (filled<per*0.5) for (let e=0;e<rv.length;e++) edgeLine(rv[e], rv[(e+1)%rv.length]); }
      function edgeLine(a,b){ const n=Math.max(1,Math.ceil(Math.hypot(b.sx-a.sx,b.sy-a.sy)));
        for (let t=0;t<=n;t++){ const u=t/n, x=Math.round(a.sx+(b.sx-a.sx)*u-0.5), y=Math.round(a.sy+(b.sy-a.sy)*u-0.5);
          if (x<0||x>=W||y<0||y>=H) continue; const i=y*W+x, d=a.d+(b.d-a.d)*u, deff=d-(f.db||0)-0.01;
          if (deff<zbuf[i]){ zbuf[i]=deff; dep[i]=d; zw[i]=a.zr+(b.zr-a.zr)*u;
            const base=Math.floor(fidx), idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off; col[i]=M.ramp[Math.max(0,Math.min(4,idx))]; } } }
      function fillTri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if (Math.abs(area)<1e-6) return;
        for (let y=minY;y<=maxY;y++) for (let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area, w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area, w2=1-w0-w1;
          if (w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0), i=y*W+x;
          if (deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d; zw[i]=w0*a.zr+w1*b.zr+w2*c.zr; filled++;
            const base=Math.floor(fidx), idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(4,idx))];
          }
        }
      }
    }
    // legs can vanish between pixel centres: guarantee a 1px line
    for (const f of F){ if (f.mat!=='leg' || f.v.length!==4) continue;
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      line(rv[0],rv[3]); line(rv[1],rv[2]);
      function line(a,b){ const n=Math.max(1,Math.ceil(Math.hypot(b.sx-a.sx,b.sy-a.sy)));
        for (let t=0;t<=n;t++){ const u=t/n, x=Math.round(a.sx+(b.sx-a.sx)*u-0.5), y=Math.round(a.sy+(b.sy-a.sy)*u-0.5);
          if (x<0||x>=W||y<0||y>=H) continue; const i=y*W+x, d=a.d+(b.d-a.d)*u;
          if (d-0.02<zbuf[i]){ zbuf[i]=d; dep[i]=d; zw[i]=a.zr+(b.zr-a.zr)*u; col[i]=MATS.leg.ramp[2]; } } }
    }
    const eyeC=MATS.eye.ramp[1];
    for (const e of eyes){
      const v=projVert(e[0],e[1],e[2],B), x=Math.round(v.sx-0.5), y=Math.round(v.sy-0.5);
      if (x<0||x>=W||y<0||y>=H) continue;
      const i=y*W+x; if (v.d-0.03<zbuf[i] && col[i]){ col[i]=eyeC; dep[i]=v.d; }
    }
    const out=col.slice();
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x; if (!col[i]) continue;
      for (const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if (nx>=W||ny>=H) continue;
        const j=ny*W+nx; if (!col[j]) continue;
        if (Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]]; if (e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)]; }
      }
    }
    if (r.outline) for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x; if (out[i]) continue;
      for (const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy; if (nx<0||nx>=W||ny<0||ny>=H) continue;
        const j=ny*W+nx; if (col[j]){ out[i]=KEY; zw[i]=zw[j]; break; }
      }
    }
    const rgba=new Uint8ClampedArray(W*H*4);
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x, c=out[i]; if (!c){ rgba[i*4+3]=0; continue; }
      let [rr,gg,bb]=hex2rgb(c), a=255;
      if (r.waterZ!=null && zw[i]<r.waterZ-0.004){
        const dz=r.waterZ-zw[i], mix=Math.min(0.72, 0.30+dz*0.9);
        rr=rr*(1-mix)+WRGB[0]*mix; gg=gg*(1-mix)+WRGB[1]*mix; bb=bb*(1-mix)+WRGB[2]*mix;
        a = dz>0.35 ? 115 : 160;
      }
      rgba[i*4]=Math.round(rr); rgba[i*4+1]=Math.round(gg); rgba[i*4+2]=Math.round(bb); rgba[i*4+3]=a;
    }
    return rgba;
  }

  function render(dir, opts){ return _paint(resolve(opts), Object.assign({},opts,{dir})); }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir, elev})); return { dx:v.sx-cx, dy:v.sy-cy }; }
  // screen offsets (px from the pivot) of the bill tip, head, wingtips, tail, feet for the resolved pose
  function anchors(dir, opts){
    const r=resolve(opts), {anchors:An}=facesOf(r), B=camBasis(Object.assign({},opts||{},{dir})), out={};
    for (const k in An){ const v=projVert(An[k][0],An[k][1],An[k][2],B); out[k]={ dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy), z:+An[k][2].toFixed(3) }; }
    return out;
  }
  function sheetOrder(){ const o=[]; for (const a of AORDER) for (let f=0;f<ANIMS[a].n;f++) o.push({anim:a,f}); return o; }
  const dirOf=(h)=>mod(Math.round(h/(Math.PI/4)), 8);

  // how the birds keep company and where they go under. Numbers the page times; the rig only poses.
  const GROUP = { size:[1,5], solitary_ok:true, flight:'line astern, low', flight_spacing_m:1.6, flight_alt_m:[0.5,3],
    roost_spacing_m:0.7, flee_radius_m:6, flee_reaction:'takeoff (from a perch) or dive (from the water)' };
  const UNDER = { spell_s:[18,40], travel_m:[6,25], rest_between_s:[8,20], dives_before_drying:[4,10] };

  // ---- gameplay sidecar -----------------------------------------------------------------------------
  function gameplayGeometry(){
    const Bd=BODY;
    const st=anchors(0,{anim:'stand'}), fl=anchors(0,{anim:'float'}), pc=anchors(0,{anim:'perch'}), dr=anchors(0,{anim:'dry'});
    const STATES={}; for (const a of AORDER) STATES[a]=Object.assign({ frames:ANIMS[a].n, ms:ANIMS[a].ms }, MOTION[a]);
    return {
      schema:'hidden-harbours/creature-gameplay@1', rig:'cormorantIsoRig.js', exportSymbol:'CormorantIso', units:'metres',
      frame:{ origin:'contact point under the body centre (rock / perch top / water surface)', axes:'+x right wing, +y bill, +z up',
        scale_px_per_m:S, cell:[W,H], pivot:[cx,cy], dirs:8, dir0:'bill toward +y (N), 45deg steps CW' },
      authoring:'Generated by CormorantIso.gameplayGeometry() from the same BODY/POSE/MOTION tables the bake uses. Do not hand-edit — re-generate (docs/art/rigs/birds/tools/build.cjs --write).',
      extractor_contract:'per section: rig export -> this sidecar -> absent section = the creature does not support the feature (not an error).',
      creature:{ label:Bd.label, species_id_proposed:'bird.double_crested_cormorant', length_m:Bd.length_m, wingspan_m:Bd.wingspan_m, mass_kg:Bd.mass_kg,
        body_centre_z:{ stand:Bd.standZ, perch:Bd.perchZ, float:Bd.floatZ }, draft_m:Bd.draft,
        footprint_m:{ stand:[0.20,0.30], wings_spread:[0.95,0.30], wings_open:[Bd.wingspan_m,0.45] } },
      STATES,
      TRANSITIONS:[
        ['float','dive'],['dive','under'],['under','surface'],['surface','float'],
        ['float','patter'],['patter','fly'],['fly','glide'],['glide','fly'],['fly','splash'],['glide','splash'],['splash','float'],
        ['fly','land'],['glide','land'],['land','stand'],['land','perch'],
        ['stand','dry'],['dry','stand'],['perch','dry'],['dry','perch'],
        ['stand','takeoff'],['perch','takeoff'],['dry','takeoff'],['takeoff','fly'],
      ],
      UNDER:Object.assign({ note:'"under" is a STATE WITH NO FRAMES: between dive and surface the bird is not drawn. The page hides it for spell_s, moves it travel_m (toward fish if it knows where they are), and plays surface there. A ring of ripples at the dive point and at the surfacing point is the page\'s (splashRig.js, smallest burst). After dives_before_drying dives it flies to a perch and plays dry.' }, UNDER),
      DRY:{ note:'The pose that makes it a cormorant: upright, wings held half open, slow fanning. Only on a perch or flat rock clear of the water. Faces into the wind when there is one (page turns the facing; the rig is the same).',
        entered_from:['stand','perch'], hold_s:[20,90], needs_clear_m:[0.95,0.30], prefers:['rock_ledge','piling','buoy_top','breakwater','wreck','wharf_edge'], not_in:['rain','night'] },
      LAND:{ note:'land() ends in stand (rock, ledge, breakwater) or perch (a piling or buoy top). It does not land on sand flats, grass, roofs or decks.',
        needs_clear_m:[Bd.wingspan_m,0.7], approach_into_wind:true, min_flat_m:0.25 },
      PERCH:{ note:'The pivot sits ON the surface. A cormorant wants a top it can stand its whole foot on and room for the tail behind it.',
        rules:{ min_width_m:0.12, flat_ok_min_m:0.25, max_slope_deg:30, clearance_above_m:0.8, tail_room_behind_m:0.20 },
        candidates:['piling','mooring_post','buoy_top','rock_ledge','breakwater_rock','wreck_rib','wharf_edge','channel_marker'],
        feet_px:{ dir0:{ L:pc.footL, R:pc.footR } }, exit:'takeoff' },
      WATER:{ note:'float pivots at the surface with the body centre BELOW it (float_body_z is negative): the back is awash and the tail is under. Pixels under the surface are baked as the depth tint, as the gull\'s. It cannot take off from the water in one jump: float -> patter (a loop the page holds for cycles) -> fly.',
        float_body_z:Bd.floatZ, bill_px:{ dir0:fl.bill }, splash_burst_frame:1, splash_rig:'splashRig.js', patter_burst_frames:[0,2],
        drift_with_current:false, dive_yield:{ note:'a dive is a hunt; the page rolls a catch at surface f0', catch_p:0.5, catch_species_note:'small bottom and mid-water fish; the game\'s species data names them' } },
      GROUP:Object.assign({ note:'Loose company, not a wheeling flock: a few on a ledge, one or two fishing, a short line flying low to the next spot. The gull\'s attractors (gutting, tubs, bait) do NOT draw it: it hunts its own fish.' }, GROUP),
      ANCHORS_PX:{ note:'screen offsets from the pivot at dir 0 (bill toward N); use CormorantIso.anchors(dir,{anim,frame}) for the rest',
        stand:st, float:fl, perch:pc, dry:dr },
      FIELD_NOTES:{ species:'Double-crested cormorant, Nannopterum auritum: the common breeding cormorant of the Maritimes\' harbours and estuaries in the warm months. The great cormorant (Phalacrocorax carbo) is the winter bird of the outer coast: bigger, white throat and flank patch. Not drawn; it could be a second look on this body.',
        sizes:'length 70-90 cm, wingspan 114-123 cm, mass 1.2-2.5 kg (the brief\'s figures; the rig draws 0.82 m and 1.20 m). To be confirmed against a field guide by title, edition and page before the look is called final.',
        marks:'adult in breeding dress: all black with a green gloss, bronze-brown upperwing coverts edged dark, bare orange-yellow throat skin and lores, dusky hooked bill, aquamarine eye, black legs and webs. The crests are a few thin plumes for a few weeks in spring and are not drawn at this size.' },
      _excluded:{ states:'No swoop, preen, peck or walk: a cormorant does not wheel over a boat, and on land it stands. No plunge from the air: its dive starts from the water.',
        looks:'One look: the breeding adult. The pale-breasted first-year bird and the great cormorant are not drawn — absence is data.',
        skinned_export:'No skeleton / bindMesh / clips. This rig ships on the sprite-sheet route the herring gull is on today.',
        sound:'Calls belong to the audio lane; a cormorant is near silent away from the colony.' },
    };
  }

  root.CormorantIso = { W, H, pivot:{x:cx,y:cy}, KEY, WATER, KEYLINE_DEFAULT, BODY, ANIMS, AORDER, MOTION, POSE, GROUP, UNDER, MATS,
    defaultElev:DEFAULT_ELEV, resolve, render, anchors, project, sheetOrder, dirOf, gameplayGeometry };
})(typeof globalThis!=='undefined'?globalThis:window);

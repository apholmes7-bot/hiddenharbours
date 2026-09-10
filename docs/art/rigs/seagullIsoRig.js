/* Hidden Harbours — HERRING GULL iso rig (Larus argentatus). Ambient + gameplay wildlife on the
   same fixed 3/4 turntable as the fleet and FishIso2 (45deg steps CW, elev 40, 32 px = 1 m, ordered
   dither, depth-edge darkening, no AA, RINGLESS per ADR 0031 — render(dir,{outline:true}) is the A/B).
   STRICT WORLD SCALE: body 0.60 m bill-to-tail, wingspan 1.40 m (45 px). No readability floor.

   ONE parametric bird: body loft + neck/head/bill + two 3-segment wings (arm / hand / primaries)
   + tail fan + legs. Every animation below is a POSE TABLE over the same scalars, so the 8-dir bake,
   the eye, the bill and the foot anchors all come from one function.

   Cell 64x64, pivot (32,46) = the CONTACT point under the body centre: ground when standing / walking /
   perched, the water surface when floating, the point directly below the bird when airborne (page
   blits the frame at pivot + the state's altitude; the shadow stays at the pivot). Pose z = body
   centre over the contact plane. Pixels below waterZ bake the depth-graded water tint + alpha exactly
   as FishIso2 (never clip at runtime); waterZ:null = dry.

   ANIMS (n frames, ms, page-side MOTION contract in m/s along the heading):
     fly 6 · glide 2 · swoop 4 · dive 4 · splash 5 · float 2 · preen 4 · peck 3 · land 4 · stand 2 ·
     walk 6 · perch 2 · takeoff 4.
   flock(n, t, opts) — RUNTIME SINGLES exactly like FishIso2.shoal: world-metre offsets, heading, 8-dir
   bake, anim + frame for n birds wheeling round one anchor. Deterministic per seed.
   gameplayGeometry() — the sidecar generator (Art/gameplay/seagullIsoRig.gameplay.json): states,
   transitions, motion, PERCH contract, WATER contract, FLOCK + attractor rules, all from the same
   constants the bake uses. Art/_sidecarExport.js stamps derivedFromRigSha256 at save time.

   Exposes globalThis.SeagullIso = { W,H,pivot,KEY,WATER,KEYLINE_DEFAULT,BODY,ANIMS,AORDER,MOTION,POSE,
     defaultElev,resolve,render,anchors,project,sheetOrder,flock,dirOf,gameplayGeometry }. */
(function (root) {
  const S = 32, DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const W = 64, H = 64, cx = 32, cy = 46;
  const KEY = '#101a19', WATER = '#123034', KEYLINE_DEFAULT = false;

  // ---- the bird (metres, bird-local: +x right wing, +y bill, +z up; origin = body centre) ---------
  const BODY = {
    label:'HERRING GULL', length_m:0.60, wingspan_m:1.40, mass_kg:1.0,
    bodyLen:0.30, girth:0.082,           // trunk loft
    shoulder:[0.062,0.045,0.052],        // wing root (x mirrored)
    arm:0.24, hand:0.22, prim:0.24,      // wing segments, root -> tip (sum 0.70 = half span)
    chord:[0.17,0.15,0.11,0.03],         // chord at root / elbow / wrist / tip
    neck:[0.06,0.12],                    // neck length tucked -> stretched
    headR:0.05, bill:0.066,
    tail:0.17, tailW:[0.06,0.15],        // tail length, width folded -> spread
    hip:[0.032,-0.015,-0.06], leg:0.115, // hip (x mirrored), leg length hip -> sole
    standZ:0.19, perchZ:0.165, floatZ:0.035, draft:0.05,
  };

  const ANIMS = {
    fly:{n:6,ms:90}, glide:{n:2,ms:320}, swoop:{n:4,ms:110}, dive:{n:4,ms:85}, splash:{n:5,ms:110},
    float:{n:2,ms:420}, preen:{n:4,ms:230}, peck:{n:3,ms:140}, land:{n:4,ms:110}, stand:{n:2,ms:520},
    walk:{n:6,ms:130}, perch:{n:2,ms:560}, takeoff:{n:4,ms:100},
  };
  const AORDER = ['fly','glide','swoop','dive','splash','float','preen','peck','land','stand','walk','perch','takeoff'];
  // page-side motion contract. v m/s along heading; vz m/s (negative = descending); alt = typical
  // altitude band (m over the contact plane) the page blits at; loop or oneshot -> next.
  const MOTION = {
    fly:    { v:9.0,  vz:0,     alt:[6,25],  loop:true },
    glide:  { v:8.0,  vz:-0.4,  alt:[3,25],  loop:true },
    swoop:  { v:12.0, vz:-3.0,  alt:[1,8],   loop:true,  note:'banked pass; page steers the heading round the target' },
    dive:   { v:6.0,  vz:-9.0,  alt:[0.3,8], loop:false, next:'splash', note:'bill-first plunge; ends at the surface' },
    splash: { v:1.2,  vz:0,     alt:[0,0],   loop:false, next:'float', travel:0.6, note:'frames 0-2 brake with wings up, 3-4 settle; splashRig.js burst at f2' },
    float:  { v:0.15, vz:0,     alt:[0,0],   loop:true,  note:'drifts with the surface current' },
    preen:  { v:0,    vz:0,     alt:[0,0],   loop:true,  cycles:[2,5] },
    peck:   { v:0,    vz:0,     alt:[0,0],   loop:true,  cycles:[1,3] },
    land:   { v:2.0,  vz:-1.5,  alt:[0.6,0], loop:false, next:'stand', travel:0.5 },
    stand:  { v:0,    vz:0,     alt:[0,0],   loop:true },
    walk:   { v:0.45, vz:0,     alt:[0,0],   loop:true },
    perch:  { v:0,    vz:0,     alt:[0,0],   loop:true,  note:'contact plane = the perch top; feet grip its edge' },
    takeoff:{ v:2.5,  vz:2.2,   alt:[0,1.2], loop:false, next:'fly', travel:0.8 },
  };

  // pose scalars: pitch/roll (rad), z (m body centre over contact), dh dihedral (rad, +up), sw sweep (rad,
  // + back), fold (extra hand sweep), pdh primaries dihedral (+up), chordK (wing compression when folded),
  // wingL/wingR per-side override, hy head yaw, hp head pitch (- = down), nk neck 0..1, tail 0..1 spread,
  // legs 0..1 (0 = tucked, not drawn), stride (m fore/aft of the left foot; right mirrors), knee bend.
  const FOLD = { dh:0.25, sw:1.50, fold:0.15, pdh:-0.08, chordK:0.55, lenK:0.55 };  // lenK ≈ the Z-fold of arm/forearm/hand at this resolution
  const flap = (f,n)=>{ const ph=f/n*Math.PI*2, s=Math.sin(ph);
    return { dh:0.62*s+0.06, sw:0.10+0.12*Math.max(0,-s), fold:0.10+0.30*Math.max(0,-s), pdh:-0.35*s, chordK:1, z:0.02*Math.cos(ph) }; };
  const POSE = {
    fly:    [0,1,2,3,4,5].map(f=>Object.assign({ pitch:0.05, roll:0, hy:0, hp:0, nk:0.55, tail:0.35, legs:0 }, flap(f,6))),
    glide:  [{dh:0.16,sw:0.12,fold:0.06,pdh:-0.14,chordK:1, pitch:0.02, roll:0.05, nk:0.5, tail:0.45, legs:0, z:0},
             {dh:0.19,sw:0.12,fold:0.06,pdh:-0.18,chordK:1, pitch:0.02, roll:-0.05, nk:0.5, tail:0.45, legs:0, z:0.01}],
    swoop:  [{dh:-0.05,sw:0.55,fold:0.32,pdh:-0.10,chordK:1, pitch:-0.30, roll:0.55, nk:0.7, hp:-0.15, tail:0.3, legs:0, z:0},
             {dh:-0.02,sw:0.60,fold:0.36,pdh:-0.12,chordK:1, pitch:-0.36, roll:0.65, nk:0.75, hp:-0.2, tail:0.3, legs:0, z:0},
             {dh: 0.02,sw:0.58,fold:0.34,pdh:-0.10,chordK:1, pitch:-0.32, roll:0.60, nk:0.7, hp:-0.15, tail:0.3, legs:0, z:0},
             {dh: 0.00,sw:0.52,fold:0.30,pdh:-0.08,chordK:1, pitch:-0.26, roll:0.48, nk:0.65, hp:-0.1, tail:0.3, legs:0, z:0}],
    dive:   [{dh:0.30,sw:1.05,fold:0.40,pdh:0.10,chordK:0.8, pitch:-0.85, nk:1, hp:0.1, tail:0.1, legs:0, z:0},
             {dh:0.34,sw:1.15,fold:0.45,pdh:0.12,chordK:0.75, pitch:-1.05, nk:1, hp:0.1, tail:0.1, legs:0, z:0},
             {dh:0.36,sw:1.20,fold:0.48,pdh:0.12,chordK:0.7, pitch:-1.15, nk:1, hp:0.05, tail:0.1, legs:0, z:0},
             {dh:0.34,sw:1.12,fold:0.44,pdh:0.10,chordK:0.75, pitch:-1.00, nk:1, hp:0.05, tail:0.1, legs:0, z:0}],
    splash: [{dh:0.85,sw:0.05,fold:0.10,pdh:0.05,chordK:1, pitch:0.40, nk:0.5, hp:-0.2, tail:0.8, legs:1, stride:0.06, knee:0, z:0.42, waterZ:0},
             {dh:0.95,sw:0.02,fold:0.12,pdh:0.10,chordK:1, pitch:0.36, nk:0.5, hp:-0.2, tail:0.9, legs:1, stride:0.07, knee:0, z:0.24, waterZ:0},
             {dh:0.75,sw:0.10,fold:0.20,pdh:0.05,chordK:1, pitch:0.22, nk:0.45, hp:-0.1, tail:0.7, legs:1, stride:0.05, knee:0.3, z:0.08, waterZ:0},
             {dh:0.50,sw:0.70,fold:0.30,pdh:-0.05,chordK:0.8, pitch:0.08, nk:0.4, tail:0.4, legs:0, z:0.03, waterZ:0},
             Object.assign({ pitch:0.02, nk:0.35, tail:0.15, legs:0, z:0.035, waterZ:0 }, FOLD)],
    float:  [Object.assign({ pitch:0.02, nk:0.35, hy:0.0, tail:0.12, legs:0, z:0.035, waterZ:0 }, FOLD),
             Object.assign({ pitch:0.00, nk:0.38, hy:0.25, tail:0.12, legs:0, z:0.045, waterZ:0 }, FOLD)],
    preen:  [Object.assign({ pitch:0.02, nk:0.6, hy:2.35, hp:-0.55, tail:0.15, legs:0, z:0.035, waterZ:0, wingL:{dh:0.55,sw:1.0,fold:0.3,lenK:0.8} }, FOLD),
             Object.assign({ pitch:0.02, nk:0.7, hy:2.55, hp:-0.75, tail:0.15, legs:0, z:0.04,  waterZ:0, wingL:{dh:0.60,sw:1.0,fold:0.3,lenK:0.8} }, FOLD),
             Object.assign({ pitch:0.02, nk:0.55,hy:2.20, hp:-0.45, tail:0.15, legs:0, z:0.035, waterZ:0, wingL:{dh:0.50,sw:1.05,fold:0.3,lenK:0.8} }, FOLD),
             Object.assign({ pitch:0.02, nk:0.45,hy:0.6,  hp:-0.15, tail:0.15, legs:0, z:0.04,  waterZ:0 }, FOLD)],
    peck:   [Object.assign({ pitch:0.06, nk:0.55, hp:-0.35, tail:0.15, legs:0, z:0.035, waterZ:0 }, FOLD),
             Object.assign({ pitch:0.16, nk:0.85, hp:-0.85, tail:0.2,  legs:0, z:0.03,  waterZ:0 }, FOLD),
             Object.assign({ pitch:0.24, nk:1.0,  hp:-1.15, tail:0.25, legs:0, z:0.025, waterZ:0 }, FOLD)],
    land:   [{dh:0.80,sw:0.05,fold:0.12,pdh:0.05,chordK:1, pitch:0.42, nk:0.5, hp:-0.25, tail:0.85, legs:1, stride:0.07, knee:0, z:0.55},
             {dh:0.92,sw:0.04,fold:0.15,pdh:0.10,chordK:1, pitch:0.36, nk:0.5, hp:-0.2, tail:0.9, legs:1, stride:0.08, knee:0, z:0.36},
             {dh:0.70,sw:0.20,fold:0.25,pdh:0.05,chordK:1, pitch:0.18, nk:0.45, hp:-0.1, tail:0.6, legs:1, stride:0.05, knee:0.15, z:0.21},
             {dh:0.45,sw:0.85,fold:0.30,pdh:-0.05,chordK:0.75, pitch:0.06, nk:0.4, tail:0.3, legs:1, stride:0.02, knee:0.05, z:0.19}],
    stand:  [Object.assign({ pitch:0.04, nk:0.45, hy:0.0, tail:0.1, legs:1, stride:0.015, knee:0, z:0.19 }, FOLD),
             Object.assign({ pitch:0.04, nk:0.5,  hy:-0.35, hp:0.05, tail:0.1, legs:1, stride:0.015, knee:0, z:0.192 }, FOLD)],
    walk:   [0,1,2,3,4,5].map(f=>{ const ph=f/6*Math.PI*2;
              return Object.assign({ pitch:0.06, roll:0.07*Math.sin(ph), nk:0.5+0.08*Math.sin(ph*2), hp:0.05*Math.sin(ph*2), tail:0.1,
                legs:1, stride:0.055*Math.sin(ph), knee:0, z:0.19+0.006*Math.abs(Math.cos(ph)) }, FOLD); }),
    perch:  [Object.assign({ pitch:0.02, nk:0.4, hy:0.5,  tail:0.1, legs:1, stride:0.0, knee:0.35, z:0.165 }, FOLD),
             Object.assign({ pitch:0.02, nk:0.42,hy:-0.4, tail:0.1, legs:1, stride:0.0, knee:0.35, z:0.168 }, FOLD)],
    takeoff:[{dh:0.35,sw:0.70,fold:0.35,pdh:-0.05,chordK:0.8, pitch:0.28, nk:0.7, hp:-0.1, tail:0.4, legs:1, stride:0.0, knee:0.45, z:0.15},
             {dh:0.80,sw:0.15,fold:0.20,pdh:0.05,chordK:1, pitch:0.35, nk:0.75, tail:0.6, legs:1, stride:-0.04, knee:0, z:0.36},
             {dh:-0.30,sw:0.10,fold:0.10,pdh:-0.25,chordK:1, pitch:0.30, nk:0.7, tail:0.6, legs:0.6, stride:-0.05, knee:0, z:0.62},
             {dh:-0.55,sw:0.12,fold:0.12,pdh:-0.35,chordK:1, pitch:0.22, nk:0.6, tail:0.5, legs:0.2, stride:-0.05, knee:0, z:0.88}],
  };

  // ---- palette (fleet ramps; nothing invented) -----------------------------------------------------
  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.12;
  const LN = (()=>{ const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  const hex2rgb=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  const rgb2hex=(r,g,b)=>'#'+[r,g,b].map(v=>Math.max(0,Math.min(255,Math.round(v))).toString(16).padStart(2,'0')).join('');
  const light=(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r+(255-r)*f, g+(255-g)*f, b+(255-b)*f); };
  const dark =(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r*f, g*f, b*f); };
  const mkRamp=(base)=>[dark(base,0.42), dark(base,0.66), base, light(base,0.17), light(base,0.34)];
  const MATS = {
    white: { ramp:mkRamp('#d9dcd8'), off:0 },   // head, breast, belly, underwing, tail
    mantle:{ ramp:mkRamp('#8a9298'), off:0 },   // back + upper wing
    tip:   { ramp:mkRamp('#2c3135'), off:0 },   // primaries, tail band
    bill:  { ramp:mkRamp('#d4a83a'), off:0 },
    leg:   { ramp:mkRamp('#c99298'), off:0 },
    eye:   { ramp:mkRamp('#1a1f22'), off:0 },
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
    const r = Object.assign({ pitch:0, roll:0, dh:0, sw:0, fold:0, pdh:0, chordK:1, lenK:1, hy:0, hp:0, nk:0.5, tail:0.1, legs:0, stride:0, knee:0, z:0 }, p);
    for (const k of ['pitch','roll','z','hy','hp','nk','tail','legs']) if (opts[k]!=null) r[k]=opts[k];
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
    const anchors={};

    // trunk: lathe along y, radius profile peaks forward of centre (deep breast, tapering rump)
    const L=B.bodyLen, g=(u)=>B.girth*Math.max(0.12, u<0.35 ? 0.55+0.45*(u/0.35) : 1-0.85*Math.pow((u-0.35)/0.65,1.4));
    const NSEG=8, NR=8, rings=[];
    for (let i=0;i<=NSEG;i++){ const u=i/NSEG, rr=g(u), y=L*(0.5-u), ring=[];
      for (let j=0;j<NR;j++){ const ph=j/NR*2*Math.PI; ring.push([Math.cos(ph)*rr*1.05, y, Math.sin(ph)*rr - rr*0.08]); }
      rings.push(ring); }
    for (let i=0;i<NSEG;i++) for (let j=0;j<NR;j++){ const j2=(j+1)%NR;
      const ms=(Math.sin(j/NR*2*Math.PI)+Math.sin(j2/NR*2*Math.PI))/2;
      push([rings[i][j],rings[i][j2],rings[i+1][j2],rings[i+1][j]], ms>0.5?'mantle':'white'); }
    const breast=[0, L*0.5+g(0)*0.8, -0.01];
    for (let j=0;j<NR;j++){ const j2=(j+1)%NR; push([breast, rings[0][j2], rings[0][j], rings[0][j]], 'white'); }
    const rump=[0, -L*0.5-g(1)*0.6, 0.0];
    for (let j=0;j<NR;j++){ const j2=(j+1)%NR; push([rump, rings[NSEG][j], rings[NSEG][j2], rings[NSEG][j2]], 'white'); }

    // neck + head + bill (head frame: yaw about z then pitch about x, from the neck root)
    const root=[0, L*0.42, B.girth*0.55], nkL=B.neck[0]+(B.neck[1]-B.neck[0])*r.nk;
    const chy=Math.cos(r.hy), shy=Math.sin(r.hy), chp=Math.cos(r.hp), shp=Math.sin(r.hp);
    const HD=(p)=>{ const y1=p[1]*chp-p[2]*shp, z1=p[1]*shp+p[2]*chp; return [root[0]+p[0]*chy-y1*shy, root[1]+p[0]*shy+y1*chy, root[2]+z1]; };
    // neck: rises with the pitch of the head
    const neckUp=0.35+0.4*r.nk;
    const hc=HD([0, nkL*Math.cos(neckUp*0.6), nkL*Math.sin(neckUp*0.6)+B.headR*0.6]);
    const NR2=6, nring=(c,rr,dirv)=>{ const out=[]; for (let j=0;j<NR2;j++){ const ph=j/NR2*2*Math.PI;
      out.push(HD([c[0]+Math.cos(ph)*rr, c[1]-Math.sin(ph)*rr*dirv[2], c[2]+Math.sin(ph)*rr*dirv[1]])); } return out; };
    const n0=nring([0,0,0], B.girth*0.5, [0,1,0]), n1=nring([0, nkL*Math.cos(neckUp*0.6), nkL*Math.sin(neckUp*0.6)+B.headR*0.6], B.headR*0.7, [0,1,0]);
    for (let j=0;j<NR2;j++){ const j2=(j+1)%NR2; push([n0[j],n0[j2],n1[j2],n1[j]], 'white'); }
    // head sphere as two rings + nose/back caps
    const hR=B.headR, hl=[0, nkL*Math.cos(neckUp*0.6), nkL*Math.sin(neckUp*0.6)+B.headR*0.6];
    const hring=(dy,rr)=>{ const out=[]; for (let j=0;j<NR2;j++){ const ph=j/NR2*2*Math.PI; out.push(HD([hl[0]+Math.cos(ph)*rr, hl[1]+dy, hl[2]+Math.sin(ph)*rr])); } return out; };
    const h0=hring(-hR*0.55, hR*0.8), h1=hring(0, hR), h2=hring(hR*0.6, hR*0.75);
    for (const [a,b] of [[h0,h1],[h1,h2]]) for (let j=0;j<NR2;j++){ const j2=(j+1)%NR2; push([a[j],a[j2],b[j2],b[j]], 'white'); }
    const hb=HD([hl[0],hl[1]-hR*0.95,hl[2]]), hf=HD([hl[0],hl[1]+hR*0.85,hl[2]-hR*0.1]);
    for (let j=0;j<NR2;j++){ const j2=(j+1)%NR2; push([hb,h0[j],h0[j2],h0[j2]],'white'); push([hf,h2[j2],h2[j],h2[j]],'white'); }
    // bill: tapered wedge
    const bl=B.bill, b0=[hl[0], hl[1]+hR*0.8, hl[2]-hR*0.15], bt=[hl[0], hl[1]+hR*0.8+bl, hl[2]-hR*0.28];
    const bw=hR*0.32, bh=hR*0.3;
    const bq=[[b0[0]-bw,b0[1],b0[2]+bh],[b0[0]+bw,b0[1],b0[2]+bh],[b0[0]+bw,b0[1],b0[2]-bh],[b0[0]-bw,b0[1],b0[2]-bh]].map(HD);
    const bT=HD(bt);
    push([bq[0],bq[1],bT,bT],'bill',{b:0.3}); push([bq[1],bq[2],bT,bT],'bill'); push([bq[2],bq[3],bT,bT],'bill',{b:-0.6}); push([bq[3],bq[0],bT,bT],'bill');
    anchors.bill=T(bT); anchors.head=T(HD(hl));
    // eyes (painted as points after the z-pass)
    const eyes=[HD([hl[0]+hR*0.95, hl[1]+hR*0.25, hl[2]+hR*0.15]), HD([hl[0]-hR*0.95, hl[1]+hR*0.25, hl[2]+hR*0.15])].map(T);

    // wings
    const wing=(s, wp)=>{
      const sh=[s*B.shoulder[0], B.shoulder[1], B.shoulder[2]];
      const seg=(from, sw, dh, len)=>{ const d=[s*Math.cos(sw)*Math.cos(dh), -Math.sin(sw)*Math.cos(dh), Math.sin(dh)];
        return [from[0]+d[0]*len, from[1]+d[1]*len, from[2]+d[2]*len]; };
      const chordV=(sw)=>[s*Math.sin(sw), Math.cos(sw), 0];   // forward along the chord
      const lk=wp.lenK||1;
      const el=seg(sh, wp.sw, wp.dh, B.arm*lk);
      const wr=seg(el, wp.sw+wp.fold, wp.dh*0.6+wp.pdh*0.4, B.hand*lk);
      const tp=seg(wr, wp.sw+wp.fold*1.15, wp.pdh, B.prim*lk);
      const ck=wp.chordK, C=B.chord.map(c=>c*ck);
      const edge=(p, sw, c, lead)=>{ const v=chordV(sw); const f=lead? c*0.45 : -c*0.55; return [p[0]+v[0]*f, p[1]+v[1]*f, p[2]+v[2]*f]; };
      const q=(a,aw,ac, b,bw,bc, mat, matBack)=>{
        const A=edge(a,aw,ac,true), Ab=edge(a,aw,ac,false), Bl=edge(b,bw,bc,true), Bb=edge(b,bw,bc,false);
        const v = s>0 ? [A,Bl,Bb,Ab] : [Bl,A,Ab,Bb];
        push(v, mat, { matBack, ds:1, db:-0.02 });
      };
      q(sh,wp.sw,C[0], el,wp.sw,C[1], 'mantle','white');
      q(el,wp.sw+wp.fold,C[1], wr,wp.sw+wp.fold,C[2], 'mantle','white');
      q(wr,wp.sw+wp.fold*1.15,C[2], tp,wp.sw+wp.fold*1.15,C[3], 'tip','tip');
      return T(tp);
    };
    const wpL=Object.assign({dh:r.dh,sw:r.sw,fold:r.fold,pdh:r.pdh,chordK:r.chordK,lenK:r.lenK}, r.wingL||{});
    const wpR=Object.assign({dh:r.dh,sw:r.sw,fold:r.fold,pdh:r.pdh,chordK:r.chordK,lenK:r.lenK}, r.wingR||{});
    anchors.wingL=wing(-1,wpL); anchors.wingR=wing(1,wpR);

    // tail fan: white blades with a dark band, double-sided
    const tw=B.tailW[0]+(B.tailW[1]-B.tailW[0])*r.tail, t0=[0,-L*0.42,0.012], tl=B.tail;
    const tq=(y0,y1,w0,w1,mat)=>push([[-w0/2,t0[1]-y0,t0[2]+y0*0.15],[w0/2,t0[1]-y0,t0[2]+y0*0.15],[w1/2,t0[1]-y1,t0[2]+y1*0.15],[-w1/2,t0[1]-y1,t0[2]+y1*0.15]], mat, {ds:1, matBack:mat, db:-0.01});
    tq(0, tl*0.7, tw*0.45, tw*0.9, 'white'); tq(tl*0.7, tl, tw*0.9, tw, 'tip');
    anchors.tail=T([0,t0[1]-tl,t0[2]+tl*0.15]);

    // legs + feet
    if (r.legs>0.05){
      const ext=r.legs, legL=B.leg*(1-r.knee*0.28)*ext;
      for (const s of [-1,1]){
        const hip=[s*B.hip[0], B.hip[1], B.hip[2]], st=r.stride*(s<0?1:-1);
        const foot=[hip[0]+s*0.012, hip[1]+st+0.02, hip[2]-legL];
        const rr=0.009;
        const P=(p,dx,dy)=>[p[0]+dx,p[1]+dy,p[2]];
        push([P(hip,-rr,0),P(hip,rr,0),P(foot,rr,0),P(foot,-rr,0)],'leg',{ds:1,matBack:'leg',db:-0.01});
        push([P(hip,0,-rr),P(hip,0,rr),P(foot,0,rr),P(foot,0,-rr)],'leg',{ds:1,matBack:'leg',db:-0.01});
        if (ext>0.8) push([[foot[0]-0.02,foot[1]-0.01,foot[2]],[foot[0]+0.02,foot[1]-0.01,foot[2]],[foot[0],foot[1]+0.055,foot[2]]],'leg',{ds:1,matBack:'leg',b:-0.4});
        anchors[s<0?'footL':'footR']=T(foot);
      }
    }
    return { F, eyes, anchors };
  }

  // ---- paint ----------------------------------------------------------------------------------------
  function _paint(r, o){
    const B=camBasis(o), {F,eyes}=facesOf(r);
    const zbuf=new Float32Array(W*H).fill(Infinity), col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H), zw=new Float32Array(W*H);
    for (const f of F){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]), mat=f.mat;
      const facing=(n[1]*B.ce - n[2]*B.se) < 0;
      if (!facing && f.ds){ n=[-n[0],-n[1],-n[2]]; mat=f.matBack||f.mat; }
      const sh=shadeOf(n,B.se,B.ce), fidx=sh*GAIN+BIAS+(f.b||0), M=MATS[mat]||MATS.white;
      let filled=0;
      for (let tt=1;tt+1<rv.length;tt++) fillTri(rv[0],rv[tt],rv[tt+1]);
      // thin double-sided members (a wing seen edge-on, a tail blade, a leg) fall between pixel centres:
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
    // thin members (legs, primaries edge-on) can vanish between pixel centres: guarantee a 1px line
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
    const r=resolve(opts), {anchors:A}=facesOf(r), B=camBasis(Object.assign({},opts||{},{dir})), out={};
    for (const k in A){ const v=projVert(A[k][0],A[k][1],A[k][2],B); out[k]={ dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy), z:+A[k][2].toFixed(3) }; }
    return out;
  }
  function sheetOrder(){ const o=[]; for (const a of AORDER) for (let f=0;f<ANIMS[a].n;f++) o.push({anim:a,f}); return o; }
  const dirOf=(h)=>mod(Math.round(h/(Math.PI/4)), 8);
  function mulberry(seed){ let a=seed>>>0; return function(){ a|=0; a=(a+0x6D2B79F5)|0;
    let t=Math.imul(a^(a>>>15),1|a); t=(t+Math.imul(t^(t>>>7),61|t))^t; return ((t^(t>>>14))>>>0)/4294967296; }; }

  /* RUNTIME FLOCK. n singles wheeling round one anchor (the attractor: a boat gutting fish, a tub on
     the wharf, a bait bucket). Each bird rides its own ellipse (radius R*[0.6..1.3] m, own phase and
     period) at its own altitude, alternating fly / glide on a slow beat; the lowest bird in the wheel
     swoops the anchor every few seconds. t in ms. Returns [{x,y (m from the anchor), z (m altitude),
     heading (rad, 0=N, CW), dir, anim, frame, scale}]. Deterministic per seed. */
  const FLOCK = { size:[3,9], radius_m:6, alt_m:[3,14], period_s:[7,14], glide_duty:0.45, swoop_every_s:[4,9], spacing_m:1.6,
    attract_radius_m:40, flee_radius_m:2.5, settle_after_s:20 };
  function flock(n, t, opts){
    opts=opts||{}; const rng=mulberry((opts.seed||7)*2654435761), R=opts.radius!=null?opts.radius:FLOCK.radius_m, tt=t/1000, out=[];
    for (let i=0;i<n;i++){
      const rr=R*(0.6+rng()*0.7), per=FLOCK.period_s[0]+rng()*(FLOCK.period_s[1]-FLOCK.period_s[0]), ph=rng()*6.28, ccw=rng()<0.5?1:-1;
      const alt=FLOCK.alt_m[0]+rng()*(FLOCK.alt_m[1]-FLOCK.alt_m[0]), sq=0.7+rng()*0.3, gph=rng()*6.28, swEvery=FLOCK.swoop_every_s[0]+rng()*(FLOCK.swoop_every_s[1]-FLOCK.swoop_every_s[0]);
      const a=ccw*(tt/per*6.283)+ph, x=rr*Math.cos(a), y=rr*sq*Math.sin(a);
      const vx=-rr*Math.sin(a)*ccw, vy=rr*sq*Math.cos(a)*ccw, heading=Math.atan2(vx,vy);
      const sw=((tt+ph)%swEvery)/swEvery, swooping=i===0 && sw<0.28;
      const gl=Math.sin(tt/3.1+gph)>1-2*FLOCK.glide_duty;
      const anim=swooping?'swoop':(gl?'glide':'fly');
      const zed=swooping ? alt*(0.25+0.75*Math.abs(sw/0.28-0.5)*2) : alt+Math.sin(tt*0.7+gph)*0.6;
      out.push({ x, y, z:zed, heading, dir:dirOf(heading), anim, frame:mod(Math.floor(t/ANIMS[anim].ms+ph*3), ANIMS[anim].n), scale:0.92+rng()*0.16 });
    }
    return out;
  }

  // ---- gameplay sidecar -----------------------------------------------------------------------------
  function gameplayGeometry(){
    const r3=(n)=>+n.toFixed(3), Bd=BODY;
    const st=anchors(0,{anim:'stand'}), fl=anchors(0,{anim:'float'}), pk=anchors(0,{anim:'peck',frame:2}), pc=anchors(0,{anim:'perch'});
    const STATES={}; for (const a of AORDER) STATES[a]=Object.assign({ frames:ANIMS[a].n, ms:ANIMS[a].ms }, MOTION[a]);
    return {
      schema:'hidden-harbours/creature-gameplay@1', rig:'seagullIsoRig.js', exportSymbol:'SeagullIso', units:'metres',
      frame:{ origin:'contact point under the body centre (ground / water surface / perch top)', axes:'+x right wing, +y bill, +z up',
        scale_px_per_m:S, cell:[W,H], pivot:[cx,cy], dirs:8, dir0:'bill toward +y (N), 45deg steps CW' },
      authoring:'Generated by SeagullIso.gameplayGeometry() from the same BODY/POSE/MOTION/FLOCK tables the bake uses. Do not hand-edit — re-generate.',
      extractor_contract:'per section: rig export -> this sidecar -> absent section = the creature does not support the feature (not an error).',
      creature:{ label:Bd.label, length_m:Bd.length_m, wingspan_m:Bd.wingspan_m, mass_kg:Bd.mass_kg,
        body_centre_z:{ stand:Bd.standZ, perch:Bd.perchZ, float:Bd.floatZ }, draft_m:Bd.draft,
        footprint_m:{ stand:[0.16,0.30], wings_open:[Bd.wingspan_m,0.40] } },
      STATES,
      TRANSITIONS:[
        ['stand','walk'],['walk','stand'],['stand','takeoff'],['walk','takeoff'],['perch','takeoff'],
        ['takeoff','fly'],['fly','glide'],['glide','fly'],['fly','swoop'],['glide','swoop'],['swoop','fly'],['swoop','dive'],['swoop','land'],['swoop','splash'],
        ['glide','land'],['glide','splash'],['glide','dive'],['dive','splash'],['splash','float'],
        ['float','preen'],['preen','float'],['float','peck'],['peck','float'],['float','takeoff'],
        ['land','stand'],['land','perch'],
      ],
      LAND:{ note:'land() may end in stand (flat ground: sand, wharf deck, grass, road, roof flat) or perch (an edge). Page picks by the surface it resolves under the pivot.',
        needs_clear_m:[Bd.wingspan_m, 0.6], approach_into_wind:true, min_flat_m:0.3 },
      PERCH:{ note:'Any object surface tagged perchable. The pivot sits ON the surface; the feet grip its edge. Contact plane = surface top z. Sidecars of props/hulls declare perch points as ANCHORS type "perch"; absent = not perchable (the page may still use the rules below on untagged geometry).',
        rules:{ min_width_m:0.03, max_width_for_grip_m:0.12, flat_ok_min_m:0.25, max_slope_deg:35, clearance_above_m:0.5 },
        candidates:['rail','bollard','piling','mooring_post','mast_head','boom','buoy_top','roof_ridge','chimney','lamp_post','sign','fence_post','tub_rim','trap_stack','wheelhouse_roof','cabin_top','dinghy_gunwale'],
        feet_px:{ dir0:{ L:pc.footL, R:pc.footR } }, exit:'takeoff' },
      WATER:{ note:'float pivots at the surface; body draft below is baked as the depth tint. peck dips the bill to the surface; preen lifts one wing. Sea state adds the fleet ROCK pose: roll_deg 2.4 / heave_px 1 max — a gull rides higher than a hull.',
        float_body_z:Bd.floatZ, bill_at_surface_px:{ dir0:pk.bill }, splash_burst_frame:2, splash_rig:'splashRig.js',
        drift_with_current:true, dive_yield:{ note:'dive is a feeding strike; page rolls a catch (bait fish) at splash f2', catch_p:0.35, catch_species:['herring','mackerel'], catch_rig:'fishIsoRig2.js' } },
      FLOCK:Object.assign({ note:'Runtime singles from SeagullIso.flock(n,t,{seed,radius}). A flock forms on an attractor and dissolves settle_after_s after it ends. GAMEPLAY: a flock over a boat is visible from 40 m — it tells the player (and rival crews) where fish are being cleaned; birds that land on deck steal from an uncovered tub; a gull on a trap stack fouls it (small repair cost).',
        attractors:[{ id:'gutting', weight:1.0, source:'catch handling on deck' },{ id:'open_tub', weight:0.8, source:'fish tub with lid off (fishTubRig.js)' },
          { id:'bait_bucket', weight:0.6, source:'bucketRig.js bait' },{ id:'chum', weight:1.0, source:'discards over the side' },{ id:'shoal_surface', weight:0.5, source:'FishIso2.shoal at z > -0.2' },
          { id:'trawler_wake', weight:0.7, source:'stern trawler under way' }],
        steal:{ from:'open_tub', per_bird_s:12, takes:'1 small fish', deterred_by:['lid','crew within 2.5 m','dog'] },
        flee:{ radius_m:FLOCK.flee_radius_m, reaction:'takeoff', regroup_s:8 } }, FLOCK),
      ANCHORS_PX:{ note:'screen offsets from the pivot at dir 0 (bill toward N); use SeagullIso.anchors(dir,{anim,frame}) for the rest',
        stand:st, float:fl, perch:pc },
      _excluded:{ variants:'One species, one plumage (adult). Juvenile brown mottle and other Larus are not drawn — absence is data.',
        sound:'Calls belong to the audio sidecar; this file only names the states that cue them (fly, swoop, stand, flock size >= 4).' },
    };
  }

  root.SeagullIso = { W, H, pivot:{x:cx,y:cy}, KEY, WATER, KEYLINE_DEFAULT, BODY, ANIMS, AORDER, MOTION, POSE, FLOCK, MATS,
    defaultElev:DEFAULT_ELEV, resolve, render, anchors, project, sheetOrder, flock, dirOf, gameplayGeometry };
})(typeof globalThis!=='undefined'?globalThis:window);

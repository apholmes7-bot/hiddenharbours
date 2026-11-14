/* Hidden Harbours — GREAT BLUE HERON iso rig (Ardea herodias). The tall lone bird of the flats.
   Same fixed 3/4 turntable as the herring gull and the fleet (45deg steps CW, elev 40, 32 px = 1 m,
   ordered dither, depth-edge darkening, no AA, RINGLESS per ADR 0031 — render(dir,{outline:true}) is
   the A/B). STRICT WORLD SCALE: 1.17 m bill-to-tail, wingspan 1.83 m (59 px), about 1.1 m tall standing.

   ITS OWN BODY. A heron is legs and neck: two-jointed legs that plant, stride and lift in the contact
   frame, trail straight behind in flight and hang at take-off and landing; a five-segment neck bent
   by three angles, so it stands in an S, coils, strikes out straight, and folds back onto the
   shoulders in flight; a long dagger of a bill. The painter, ramp maker and API are the gull's.

   Cell 96x96 (a heron stands taller than the gull's 64), pivot (48,72) = the CONTACT point under the
   body centre: the BOTTOM it stands on (mud, sand, rock) or the point directly below it when
   airborne. Pose z = body centre over the contact plane. stand / stalk / poise / strike are baked
   WADING: waterZ sits BODY.wade above the contact plane and everything under it takes the depth tint
   and alpha, as the gull's wet states do. roost, takeoff, land and the flight states are dry.

   ANIMS (n frames): fly 6 · glide 2 · takeoff 4 · land 4 · stand 2 · stalk 6 · poise 2 · strike 4 ·
     roost 2.
   stand = tall and still in the shallows. stalk = the slow wade, one foot lifted clear at a time.
   poise = frozen over a fish, neck coiled. strike = the stab and the swallow. roost = hunched and
   dry, neck folded down, for a rock, a bank or a treetop. fly = slow heavy beats, neck folded, legs
   trailing. A gull's swoop, dive, float, splash, preen, peck, walk and perch are not drawn.

   gameplayGeometry() — the sidecar generator (gameplay/heronIsoRig.gameplay.json).
   birds/tools/build.cjs stamps derivedFromRigSha256 at save time.

   Exposes globalThis.HeronIso = { W,H,pivot,KEY,WATER,KEYLINE_DEFAULT,BODY,ANIMS,AORDER,MOTION,POSE,
     GROUP,MATS,defaultElev,resolve,render,anchors,project,sheetOrder,dirOf,gameplayGeometry }. */
(function (root) {
  const S = 32, DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const W = 96, H = 96, cx = 48, cy = 72;
  const KEY = '#101a19', WATER = '#123034', KEYLINE_DEFAULT = false;

  // ---- the bird (metres, bird-local: +x right wing, +y bill, +z up; origin = body centre) ---------
  const BODY = {
    label:'GREAT BLUE HERON', length_m:1.17, wingspan_m:1.83, mass_kg:2.4, stands_m:1.1,
    bodyLen:0.44, girth:0.095,           // lean trunk
    shoulder:[0.075,0.10,0.05],          // wing root (x mirrored)
    arm:0.30, hand:0.28, prim:0.30,      // long broad wing
    chord:[0.30,0.29,0.25,0.11],
    neck:[0.30,0.52], neckR:[0.036,0.021], // neck length folded -> stretched; radius root -> head
    headR:0.034, bill:0.14,              // small head, long dagger bill
    tail:0.10, tailW:[0.07,0.14],
    hip:[0.045,-0.06,-0.06], tibia:0.20, tarsus:0.22, toe:0.07,
    standZ:0.50, wade:0.12,              // body centre standing; the depth the wading states are baked in
  };

  const ANIMS = {
    fly:{n:6,ms:150}, glide:{n:2,ms:400}, takeoff:{n:4,ms:140}, land:{n:4,ms:140}, stand:{n:2,ms:900},
    stalk:{n:6,ms:300}, poise:{n:2,ms:800}, strike:{n:4,ms:80}, roost:{n:2,ms:1100},
  };
  const AORDER = ['fly','glide','takeoff','land','stand','stalk','poise','strike','roost'];
  // page-side motion contract. v m/s along heading; vz m/s (negative = descending); alt = typical
  // altitude band (m over the contact plane) the page blits at; loop or oneshot -> next.
  const MOTION = {
    fly:    { v:10.0, vz:0,    alt:[3,18],  loop:true,  note:'slow, deep beats; neck folded, legs trailing. Flies alone and in a straight line' },
    glide:  { v:9.0,  vz:-0.5, alt:[2,18],  loop:true,  note:'wings bowed, on the way down to the flats' },
    takeoff:{ v:3.0,  vz:1.6,  alt:[0,1.0], loop:false, next:'fly', travel:1.2, note:'a crouch and a leap; legs hang, then trail' },
    land:   { v:2.5,  vz:-1.5, alt:[1.0,0], loop:false, next:'stand', travel:0.8, note:'legs reach down first; ends standing (in water) or roosting (dry)' },
    stand:  { v:0,    vz:0,    alt:[0,0],   loop:true,  note:'still as a post, in the shallows' },
    stalk:  { v:0.12, vz:0,    alt:[0,0],   loop:true,  note:'the slow wade: one step every 1.8 s. The page moves it this slowly or the feet slide' },
    poise:  { v:0,    vz:0,    alt:[0,0],   loop:true,  cycles:[2,12], note:'frozen over a fish, neck coiled' },
    strike: { v:0,    vz:0,    alt:[0,0],   loop:false, next:'stand', note:'f1 the lunge, f2 the bill in the water (splashRig.js smallest burst at the bill), f3 head up, swallowing' },
    roost:  { v:0,    vz:0,    alt:[0,0],   loop:true,  note:'hunched and dry: a rock, a bank, a wharf corner, a treetop' },
  };

  // pose scalars: pitch/roll (rad), z (m body centre over contact), wing dh / sw / fold / pdh / chordK /
  // lenK as the gull's. NECK: na / nm / nb = elevation (rad, from the body axis toward the back) of the
  // neck at its root / middle / head; nk 0..1 length; hy yaw of the whole neck; hp head pitch from the
  // body axis. LEGS: legs 0..1; legmode 'plant' (soles on the contact plane; stride m fore/aft of the
  // left foot, right mirrors; lift m, + lifts the left foot, - the right), 'hang' (hang rad: 0 plumb,
  // + swung forward), 'trail' (straight back along the body, in flight). tail 0..1 spread.
  const FOLD = { dh:0.20, sw:1.45, fold:0.12, pdh:-0.05, chordK:0.50, lenK:0.42 };
  const A = Object.assign;
  // neck given in WORLD elevations for a body at this pitch (what the eye sees), stored body-relative
  const NK = (pitch, a, m, b, nk, hp)=>({ pitch, na:a-pitch, nm:m-pitch, nb:b-pitch, nk, hp:hp-pitch });
  const TALL = (p)=>NK(p, 1.95, 1.15, 1.55, 0.93, 0.0);            // standing S
  const TUCK = (p)=>NK(p, 2.60, 0.30, -0.50, 0.50, 0.0);           // folded back onto the shoulders
  const flap = (f,n)=>{ const ph=f/n*Math.PI*2, s=Math.sin(ph);
    return { dh:0.60*s+0.05, sw:0.10, fold:0.10+0.25*Math.max(0,-s), pdh:-0.38*s-0.10, chordK:1, z:0.03*Math.cos(ph) }; };
  const WET = { waterZ:BODY.wade };
  const POSE = {
    fly:    [0,1,2,3,4,5].map(f=>A({ roll:0, hy:0, tail:0.3, legs:1, legmode:'trail' }, TUCK(0.06), flap(f,6))),
    glide:  [A({ dh:0.12,sw:0.10,fold:0.08,pdh:-0.22,chordK:1, roll:0.03,  tail:0.5, legs:1, legmode:'trail', z:0 }, TUCK(0.04)),
             A({ dh:0.15,sw:0.10,fold:0.08,pdh:-0.26,chordK:1, roll:-0.03, tail:0.5, legs:1, legmode:'trail', z:0.01 }, TUCK(0.04))],
    takeoff:[A({ dh:0.85,sw:0.05,fold:0.12,pdh:0.10,chordK:1,  tail:0.6, legs:1, legmode:'plant', stride:0.03, z:0.44 }, NK(0.30, 1.60, 0.90, 0.60, 0.90, -0.10)),
             A({ dh:-0.45,sw:0.08,fold:0.12,pdh:-0.40,chordK:1, tail:0.6, legs:1, legmode:'hang', hang:-0.20, z:0.66 }, NK(0.35, 1.90, 0.70, 0.30, 0.75, 0.0)),
             A({ dh:0.70,sw:0.08,fold:0.14,pdh:0.05,chordK:1,  tail:0.5, legs:1, legmode:'hang', hang:-0.65, z:0.84 }, NK(0.25, 2.30, 0.50, 0.00, 0.60, 0.0)),
             A({ dh:-0.40,sw:0.10,fold:0.12,pdh:-0.40,chordK:1, tail:0.4, legs:1, legmode:'trail', z:1.00 }, TUCK(0.15))],
    land:   [A({ dh:0.60,sw:0.02,fold:0.10,pdh:0.00,chordK:1,  tail:1.0, legs:1, legmode:'hang', hang:0.30, z:1.00 }, NK(0.30, 2.30, 0.50, 0.10, 0.60, -0.10)),
             A({ dh:0.85,sw:0.02,fold:0.12,pdh:0.10,chordK:1,  tail:1.0, legs:1, legmode:'hang', hang:0.35, z:0.82 }, NK(0.42, 2.10, 0.70, 0.40, 0.70, -0.20)),
             A({ dh:0.95,sw:0.04,fold:0.15,pdh:0.15,chordK:1,  tail:0.9, legs:1, legmode:'hang', hang:0.20, z:0.64 }, NK(0.50, 2.00, 0.90, 0.90, 0.80, -0.20)),
             A({ dh:0.40,sw:0.90,fold:0.30,pdh:-0.05,chordK:0.75, lenK:0.70, tail:0.3, legs:1, legmode:'plant', stride:0.02, z:0.50 }, TALL(0.45))],
    stand:  [A({ hy:0.0,   tail:0.1, legs:1, legmode:'plant', stride:0.02, z:0.500 }, TALL(0.45), FOLD, WET),
             A({ hy:-0.45, tail:0.1, legs:1, legmode:'plant', stride:0.02, z:0.502 }, TALL(0.45), FOLD, WET)],
    stalk:  [0,1,2,3,4,5].map(f=>{ const ph=f/6*Math.PI*2;
              return A({ tail:0.1, legs:1, legmode:'plant', stride:0.11*Math.sin(ph), lift:0.10*Math.cos(ph), z:0.488+0.006*Math.abs(Math.cos(ph)) },
                NK(0.30, 1.50, 0.75, 1.00, 0.93+0.04*Math.sin(ph*2), -0.15), FOLD, WET); }),
    poise:  [A({ tail:0.1, legs:1, legmode:'plant', stride:0.06, z:0.485 }, NK(0.12, 1.90, 0.40, -0.20, 0.75, -0.55), FOLD, WET),
             A({ tail:0.1, legs:1, legmode:'plant', stride:0.06, z:0.483 }, NK(0.10, 1.95, 0.38, -0.25, 0.74, -0.60), FOLD, WET)],
    strike: [A({ tail:0.1, legs:1, legmode:'plant', stride:0.06, z:0.483 }, NK(0.10, 2.10, 0.30, -0.30, 0.70, -0.60), FOLD, WET),
             A({ tail:0.2, legs:1, legmode:'plant', stride:0.06, z:0.475 }, NK(-0.05, 0.20, -0.35, -0.70, 1.0, -0.85), FOLD, WET),
             A({ tail:0.2, legs:1, legmode:'plant', stride:0.06, z:0.470 }, NK(-0.15, -0.10, -0.55, -0.90, 1.0, -1.00), FOLD, WET),
             A({ tail:0.1, legs:1, legmode:'plant', stride:0.06, z:0.495 }, NK(0.35, 1.80, 1.20, 1.30, 0.95, 0.35), FOLD, WET)],
    roost:  [A({ hy:0.0,  tail:0.1, legs:1, legmode:'plant', stride:0.0, z:0.500 }, NK(0.55, 2.70, 0.60, 0.20, 0.45, -0.15), FOLD),
             A({ hy:0.35, tail:0.1, legs:1, legmode:'plant', stride:0.0, z:0.502 }, NK(0.55, 2.70, 0.60, 0.20, 0.47, -0.10), FOLD)],
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
    grey:  { ramp:mkRamp('#6f8597'), off:0 },   // back, upperwing coverts, tail: the blue-grey
    slate: { ramp:mkRamp('#39454f'), off:0 },   // flight feathers, underwing, belly streaking
    neck:  { ramp:mkRamp('#a39892'), off:0 },   // greyish-buff neck
    white: { ramp:mkRamp('#dcdfdb'), off:0 },   // face and crown
    black: { ramp:mkRamp('#24292d'), off:0 },   // the stripe over the eye and its plume
    bill:  { ramp:mkRamp('#d9a93a'), off:0 },   // yellow dagger
    leg:   { ramp:mkRamp('#5d5748'), off:0 },   // dusky legs
    eye:   { ramp:mkRamp('#f0d23c'), off:0 },   // yellow eye
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
    const r = Object.assign({ pitch:0, roll:0, dh:0, sw:0, fold:0, pdh:0, chordK:1, lenK:1, hy:0, hp:0, na:1.5, nm:0.7, nb:1.1, nk:0.9,
      tail:0.1, legs:0, legmode:'plant', stride:0, lift:0, hang:0, z:0 }, p);
    for (const k of ['pitch','roll','z','hy','hp','na','nm','nb','nk','tail','legs']) if (opts[k]!=null) r[k]=opts[k];
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

    // trunk: lathe along y; grey above, dark-streaked below
    const L=B.bodyLen, g=(u)=>B.girth*Math.max(0.16, u<0.30 ? 0.62+0.38*(u/0.30) : 1-0.78*Math.pow((u-0.30)/0.70,1.5));
    const NSEG=8, NR=8, rings=[];
    for (let i=0;i<=NSEG;i++){ const u=i/NSEG, rr=g(u), y=L*(0.5-u), ring=[];
      for (let j=0;j<NR;j++){ const ph=j/NR*2*Math.PI; ring.push([Math.cos(ph)*rr*1.0, y, Math.sin(ph)*rr*1.15 - rr*0.05]); }
      rings.push(ring); }
    for (let i=0;i<NSEG;i++) for (let j=0;j<NR;j++){ const j2=(j+1)%NR;
      const ms=(Math.sin(j/NR*2*Math.PI)+Math.sin(j2/NR*2*Math.PI))/2;
      push([rings[i][j],rings[i][j2],rings[i+1][j2],rings[i+1][j]], ms>-0.3?'grey':'slate'); }
    const breast=[0, L*0.5+g(0)*0.7, 0.0];
    for (let j=0;j<NR;j++){ const j2=(j+1)%NR; push([breast, rings[0][j2], rings[0][j], rings[0][j]], 'neck'); }
    const rump=[0, -L*0.5-g(1)*0.6, 0.0];
    for (let j=0;j<NR;j++){ const j2=(j+1)%NR; push([rump, rings[NSEG][j], rings[NSEG][j2], rings[NSEG][j2]], 'grey'); }

    // neck: a chain of NN segments in the body's own vertical plane, elevation running na -> nm -> nb,
    // then yawed as one about the root. Head: its own pitch hp, rings along the bill axis.
    const nroot=[0, L*0.42, B.girth*0.35], nkL=B.neck[0]+(B.neck[1]-B.neck[0])*r.nk;
    const chy=Math.cos(r.hy), shy=Math.sin(r.hy);
    const NY=(p)=>[nroot[0]+p[0]*chy-p[1]*shy, nroot[1]+p[0]*shy+p[1]*chy, nroot[2]+p[2]];
    const NN=5, NR2=6, cpt=[[0,0,0]], cang=[];
    for (let i=0;i<NN;i++){ const t=i/(NN-1), a = t<0.5 ? r.na+(r.nm-r.na)*(t*2) : r.nm+(r.nb-r.nm)*((t-0.5)*2); cang.push(a);
      const q=cpt[i]; cpt.push([0, q[1]+Math.cos(a)*nkL/NN, q[2]+Math.sin(a)*nkL/NN]); }
    const tube=(c,rr,a)=>{ const out=[]; for (let j=0;j<NR2;j++){ const ph=j/NR2*2*Math.PI;
      out.push(NY([c[0]+Math.cos(ph)*rr, c[1]-Math.sin(ph)*rr*Math.sin(a), c[2]+Math.sin(ph)*rr*Math.cos(a)])); } return out; };
    const nrings=[];
    for (let i=0;i<=NN;i++){ const a = i===0 ? cang[0] : i===NN ? cang[NN-1] : (cang[i-1]+cang[i])/2;
      const rr=B.neckR[0]+(B.neckR[1]-B.neckR[0])*Math.min(1,i/(NN-1)); nrings.push(tube(cpt[i], rr, a)); }
    for (let i=0;i<NN;i++) for (let j=0;j<NR2;j++){ const j2=(j+1)%NR2;
      push([nrings[i][j2],nrings[i][j],nrings[i+1][j],nrings[i+1][j2]], 'neck'); }
    const hR=B.headR, hd=[0,Math.cos(r.hp),Math.sin(r.hp)], hn=[0,-Math.sin(r.hp),Math.cos(r.hp)];
    const ne=cpt[NN], hc=[0, ne[1]+hd[1]*hR*0.45, ne[2]+hd[2]*hR*0.45];
    const along=(t,up)=>[hc[0], hc[1]+hd[1]*t+hn[1]*(up||0), hc[2]+hd[2]*t+hn[2]*(up||0)];
    const hring=(t,rr)=>tube(along(t), rr, r.hp);
    const h0=hring(-hR*0.70, hR*0.72), h1=hring(0, hR), h2=hring(hR*0.85, hR*0.62);
    for (let j=0;j<NR2;j++){ const j2=(j+1)%NR2, top=(Math.sin(j/NR2*2*Math.PI)+Math.sin(j2/NR2*2*Math.PI))>0.5;
      push([h0[j2],h0[j],h1[j],h1[j2]], top?'black':'white');
      push([h1[j2],h1[j],h2[j],h2[j2]], 'white'); }
    const hb=NY(along(-hR*1.10)), hf=NY(along(hR*1.10));
    for (let j=0;j<NR2;j++){ const j2=(j+1)%NR2; push([hb,h0[j],h0[j2],h0[j2]],'black'); push([hf,h2[j2],h2[j],h2[j]],'white'); }
    // the plume: a black wisp off the back of the crown
    push([NY(along(-hR*0.9, hR*0.5)), NY(along(-hR*0.9, hR*0.1)), NY(along(-hR*0.9-0.075, -hR*0.2))], 'black', {ds:1, matBack:'black'});
    // bill: a long straight dagger
    const bw=hR*0.32, bh=hR*0.34, bt=NY(along(hR*0.90+B.bill, -hR*0.10));
    const bq=[[-bw,bh],[bw,bh],[bw,-bh],[-bw,-bh]].map(([x,u])=>{ const p=along(hR*0.85, u-hR*0.05); return NY([p[0]+x,p[1],p[2]]); });
    push([bq[0],bq[1],bt,bt],'bill',{b:0.3}); push([bq[1],bq[2],bt,bt],'bill'); push([bq[2],bq[3],bt,bt],'bill',{b:-0.5}); push([bq[3],bq[0],bt,bt],'bill');
    anchors.bill=T(bt); anchors.head=T(NY(hc));
    const eyes=[1,-1].map(s=>{ const p=along(hR*0.40, hR*0.10); return T(NY([p[0]+s*hR*0.95, p[1], p[2]])); });

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
      q(sh,wp.sw,C[0], el,wp.sw,C[1], 'slate','grey');
      q(el,wp.sw+wp.fold,C[1], wr,wp.sw+wp.fold,C[2], 'slate','grey');
      q(wr,wp.sw+wp.fold*1.15,C[2], tp,wp.sw+wp.fold*1.15,C[3], 'slate','slate');
      return T(tp);
    };
    const wpL=Object.assign({dh:r.dh,sw:r.sw,fold:r.fold,pdh:r.pdh,chordK:r.chordK,lenK:r.lenK}, r.wingL||{});
    const wpR=Object.assign({dh:r.dh,sw:r.sw,fold:r.fold,pdh:r.pdh,chordK:r.chordK,lenK:r.lenK}, r.wingR||{});
    anchors.wingL=wing(-1,wpL); anchors.wingR=wing(1,wpR);

    // tail: short and square, double-sided
    const tw=B.tailW[0]+(B.tailW[1]-B.tailW[0])*r.tail, t0=[0,-L*0.45,0.0], tl=B.tail;
    push([[-tw*0.35,t0[1],t0[2]],[tw*0.35,t0[1],t0[2]],[tw*0.5,t0[1]-tl,t0[2]-tl*0.1],[-tw*0.5,t0[1]-tl,t0[2]-tl*0.1]], 'grey', {ds:1, matBack:'grey', db:-0.01});
    anchors.tail=T([0,t0[1]-tl,t0[2]-tl*0.1]);

    // legs: hip -> ankle -> foot. plant and hang are resolved in the CONTACT frame; trail in the bird's.
    if (r.legs>0.05){
      const tib=B.tibia*k, tar=B.tarsus*k, LL=tib+tar, rr=0.010*k, P=(p,dx,dy)=>[p[0]+dx,p[1]+dy,p[2]];
      const limb=(a,b)=>{ pushW([P(a,-rr,0),P(a,rr,0),P(b,rr,0),P(b,-rr,0)],'leg',{ds:1,matBack:'leg',db:-0.01});
                          pushW([P(a,0,-rr),P(a,0,rr),P(b,0,rr),P(b,0,-rr)],'leg',{ds:1,matBack:'leg',db:-0.01}); };
      for (const s of [-1,1]){
        const hl=[s*B.hip[0], B.hip[1], B.hip[2]], hip=T(hl), side=(s<0?1:-1);
        let ankle, foot, toes=true;
        if (r.legmode==='trail'){
          ankle=T([hl[0]*0.6, hl[1]-B.tibia, hl[2]+0.015]); foot=T([hl[0]*0.5, hl[1]-B.tibia-B.tarsus, hl[2]+0.03]); toes=false;
        } else if (r.legmode==='hang'){
          const a1=r.hang-0.25, a2=r.hang+0.15;
          ankle=[hip[0], hip[1]+Math.sin(a1)*tib, hip[2]-Math.cos(a1)*tib];
          foot=[ankle[0], ankle[1]+Math.sin(a2)*tar, ankle[2]-Math.cos(a2)*tar];
        } else {
          const lift=Math.max(0, r.lift*side);
          foot=[hip[0]+s*0.010*k, hip[1]+r.stride*side+0.02*k, lift];
          const t=tib/LL, slack=Math.max(0, LL-Math.hypot(foot[0]-hip[0],foot[1]-hip[1],foot[2]-hip[2]));
          ankle=[hip[0]+(foot[0]-hip[0])*t, hip[1]+(foot[1]-hip[1])*t-(0.02*k+lift*0.9+slack*0.8), hip[2]+(foot[2]-hip[2])*t];
        }
        limb(hip,ankle); limb(ankle,foot);
        if (toes){ const fl=B.toe*k; pushW([[foot[0],foot[1]-0.015*k,foot[2]],[foot[0]+fl*0.35,foot[1]+fl,foot[2]],[foot[0]-fl*0.35,foot[1]+fl,foot[2]]],'leg',{ds:1,matBack:'leg',b:0.4}); }
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
      const sh=shadeOf(n,B.se,B.ce), fidx=sh*GAIN+BIAS+(f.b||0), M=MATS[mat]||MATS.grey;
      let filled=0;
      for (let tt=1;tt+1<rv.length;tt++) fillTri(rv[0],rv[tt],rv[tt+1]);
      // thin double-sided members (a wing seen edge-on, the plume, a leg) fall between pixel centres:
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

  // company: none. Numbers the page times; the rig only poses.
  const GROUP = { size:[1,1], solitary:true, keeps_apart_m:60, flee_radius_m:25, flee_reaction:'takeoff', returns_after_s:[120,600] };

  // ---- gameplay sidecar -----------------------------------------------------------------------------
  function gameplayGeometry(){
    const Bd=BODY;
    const st=anchors(0,{anim:'stand'}), po=anchors(0,{anim:'poise'}), ro=anchors(0,{anim:'roost'}), sk=anchors(0,{anim:'strike',frame:2});
    const STATES={}; for (const a of AORDER) STATES[a]=Object.assign({ frames:ANIMS[a].n, ms:ANIMS[a].ms }, MOTION[a]);
    return {
      schema:'hidden-harbours/creature-gameplay@1', rig:'heronIsoRig.js', exportSymbol:'HeronIso', units:'metres',
      frame:{ origin:'contact point under the body centre (the bottom it stands on; the point below it when airborne)', axes:'+x right wing, +y bill, +z up',
        scale_px_per_m:S, cell:[W,H], pivot:[cx,cy], dirs:8, dir0:'bill toward +y (N), 45deg steps CW' },
      authoring:'Generated by HeronIso.gameplayGeometry() from the same BODY/POSE/MOTION tables the bake uses. Do not hand-edit — re-generate (docs/art/rigs/birds/tools/build.cjs --write).',
      extractor_contract:'per section: rig export -> this sidecar -> absent section = the creature does not support the feature (not an error).',
      creature:{ label:Bd.label, species_id_proposed:'bird.great_blue_heron', length_m:Bd.length_m, wingspan_m:Bd.wingspan_m, mass_kg:Bd.mass_kg, stands_m:Bd.stands_m,
        body_centre_z:{ stand:Bd.standZ }, footprint_m:{ stand:[0.25,0.45], wings_open:[Bd.wingspan_m,0.6] } },
      STATES,
      TRANSITIONS:[
        ['stand','stalk'],['stalk','stand'],['stand','poise'],['stalk','poise'],['poise','strike'],['poise','stalk'],['poise','stand'],['strike','stand'],
        ['stand','takeoff'],['stalk','takeoff'],['poise','takeoff'],['roost','takeoff'],['takeoff','fly'],
        ['fly','glide'],['glide','fly'],['fly','land'],['glide','land'],['land','stand'],['land','roost'],
      ],
      WADE:{ note:'stand, stalk, poise and strike are baked WADING: the pivot is on the BOTTOM and the water surface is baked_water_z above it, so the legs under it take the depth tint. Place the bird where the water over the bottom is inside depth_m at that moment of the tide; when the flat dries or floods past it, the bird walks or flies to where it is right. A heron on dry ground uses roost.',
        baked_water_z:Bd.wade, depth_m:[0.04,0.22], bottoms:['mud','sand','eelgrass','gravel','marsh_edge'], tide:'the falling tide, low water and the first of the flood', wet_states:['stand','stalk','poise','strike'] },
      STRIKE:{ note:'poise -> strike is the hunt. The page rolls the catch at f2; f3 (head up) plays either way.',
        bill_in_water_px:{ dir0:sk.bill }, burst_frame:2, splash_rig:'splashRig.js', catch_p:0.4, poise_s:[2,10], between_hunts_s:[20,90],
        catch_species_note:'small shallow-water fish; the game\'s species data names them' },
      ROOST:{ note:'Dry and hunched, neck folded down. The pivot sits ON the surface.',
        candidates:['rock','marsh_bank','wharf_corner','piling','breakwater','treetop','dead_snag','boat_house_roof'], rules:{ min_flat_m:0.25, clearance_above_m:1.3 }, at:['high water','night','rain'] },
      LAND:{ note:'land() ends in stand when the pivot is in wadeable water, roost when it is dry.', needs_clear_m:[Bd.wingspan_m,1.2], approach_into_wind:true },
      GROUP:Object.assign({ note:'Alone. Two herons on one flat keep keeps_apart_m between them; a second that lands closer is chased (the page may play takeoff on the newcomer). It flushes early: at flee_radius_m from a walker or a boat it is gone, with a slow start.' }, GROUP),
      ANCHORS_PX:{ note:'screen offsets from the pivot at dir 0 (bill toward N); use HeronIso.anchors(dir,{anim,frame}) for the rest',
        stand:st, poise:po, roost:ro },
      FIELD_NOTES:{ species:'Great blue heron, Ardea herodias: the heron of Maritime tidal flats, marsh edges and creek mouths from spring to autumn.',
        sizes:'length 97-137 cm, wingspan 167-201 cm, mass 2.1-2.5 kg (the brief\'s figures; the rig draws 1.17 m and 1.83 m, standing about 1.1 m). To be confirmed against a field guide by title, edition and page before the look is called final.',
        marks:'adult: blue-grey back and upperwing coverts, dark slate flight feathers (the two-tone wing in flight), greyish-buff neck, white face and crown with a black stripe over the eye running into a plume, yellow dagger bill, dusky legs. The neck streaking, the chestnut thighs and the breast plumes are not drawn at this size.' },
      _excluded:{ states:'No swoop, dive, float, splash, preen, peck, walk or perch. It does not swim or plunge; on the ground its walk is the stalk.',
        looks:'One look: the adult. The darker-crowned first-year bird is not drawn — absence is data.',
        dry_stalk:'stalk and strike are drawn wading only. A heron hunting a dry field or a wharf is not drawn.',
        skinned_export:'No skeleton / bindMesh / clips. This rig ships on the sprite-sheet route the herring gull is on today.',
        sound:'Calls belong to the audio lane: one hoarse croak at takeoff when flushed.' },
    };
  }

  root.HeronIso = { W, H, pivot:{x:cx,y:cy}, KEY, WATER, KEYLINE_DEFAULT, BODY, ANIMS, AORDER, MOTION, POSE, GROUP, MATS,
    defaultElev:DEFAULT_ELEV, resolve, render, anchors, project, sheetOrder, dirOf, gameplayGeometry };
})(typeof globalThis!=='undefined'?globalThis:window);

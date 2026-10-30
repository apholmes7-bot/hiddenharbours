/* Hidden Harbours — CRUSTACEAN rig, PASS 2 (lobster + rock crab). crustaceanRig.js is untouched.
   WHY A REBUILD. Pass 1 was a top-down 2D plot squashed by 0.85 — not on the fleet turntable
   (elev 40 foreshortens depth by 0.64), one flat shading gradient, a hard 1 px ring. Pass 2
   lofts both animals as REAL SOLIDS on Art/isoSolid.js' turntable: carapace and abdomen are
   lathes, claws are lathes with a movable dactyl, the tail fan is plates; legs and antennae are
   depth-tested 1 px plots (at 32 px = 1 m a leg is 0.15 px — pixel art draws it 1 px, and the
   depth test still hides it behind the body). Same key light, dither and edge-darkening as the
   fish and the tote, so all three composite. RINGLESS (ADR 0031; {outline:true} is the A/B).
   STRICT WORLD SCALE: scale 1 = a 0.40 m lobster overall (0.11 m carapace), a 0.13 m crab.
   POSES  walk 4f · rear · defend (claws up, gape) · held 2f (by the back; pivot = THE GRIP)
     + lobster flip 4f  — the tail-flip escape: abdomen snaps under, body lifts and pitches;
       MOTION.lobster.flip says how far BACKWARD the page moves it per cycle, and the hop.
     + crab sidle 4f    — travel is body +x (a crab faces ACROSS its travel); MOTION gives v.
     + crab burrow 4f   — sinks into the sand: pixels under sandZ (default 0 for burrow) are cut
       with a dithered edge; MOTION.crab.burrow.sink is the per-frame depth for the page's mound.
   WATER  waterZ (m above the pivot; default null = dry) bakes the same depth-graded underwater
     tint + alpha as the fish, so a seabed walk composites with a swimming cod.
   HEADING ang (radians, 0 = N, CW) turns the animal on the spot — continuous, so fills scatter
     it; dir (0..7) is the turntable camera like every other rig. The two compose.
   Cell 64x64, pivot (32,40) = ground centre; held uses hpivot (32,12) (pass E: hpivot per kind,
   the lobster's (32,16)).
   Exposes globalThis.Crustacean2 = { W,H,pivot,hpivot,KINDS,POSES,MOTION,SIZES,KEYLINE_DEFAULT,
   render, hold, project }. Needs IsoSolid loaded first.
   CATCH REVIEW PASS B (2026-10-27; "make them read, scale kept"): the carapace's most lit pixel,
   and the crab's most lit claw pixel, take a glint halfway to white, a wet shell at any heading;
   a crab held by the back turns HELD_ROLL (22.5deg) about its length, so no heading sees it
   edge-on. The red is still pass 1's, verbatim.
   CATCH REVIEW PASS D (2026-10-28; the owner's ruling 3 of 10-27, the ranges to the defs' bands
   under "Drawings grow to the weights")
   · SIZES gives each kind its range, to its def's band by hold()'s law (mass = SIZES.mass x
     scale^3), in two decimals like the fish's: the lobster x0.83-1.79 (0.40-4.01 kg; the band
     0.4-4), the crab x0.63-1 (0.10-0.40 kg; the band 0.1-0.4). The drawing is unchanged: every
     scale-1 cell is pass B's, byte for byte. At x1.79 the held lobster heading S (dir 4) loses
     2 px above the cell's top in each frame; every other cell fits at its range's ends.
   CATCH REVIEW PASS E (2026-10-28; the owner's rulings "Olive in pass E" and "Bigger, with field marks")
   · the lobster is drawn live, dark olive: CARA, CLAW and LEG take LIVE's ramps, in every state it
     draws; RUST, CREAM and EYE stay pass 1's. Pass 1's red is kept, verbatim, for a cooked one:
     render('lobster', {cooked:true}) draws every cell as pass D drew it. The crab is unchanged.
   · hpivot is per kind: the lobster's grip sits 4 px lower in its held cell, (32,16), so at x1.79
     heading S it keeps every pixel (pass D's 2 px above the top); 4 px keeps the dither's phase, so
     each held lobster cell is pass D's moved down 4 px. The crab's stays (32,12), every cell
     byte for byte.
   · renderPlate(kind, scale) draws the kind for the field guide's plate, larger than the world: from
     above (the lobster head left, the crab legs across), walk frame 0, at 32 px = 1 m x scale,
     centred in a PW x PH (160 x 96) cell. The solids are the world's in finer rings (FINE: each
     lathe's stations eased between, its rings x3); the plots draw 2 px wide; then the marks a field
     guide shows (MARKS).
   THE OWNER'S LOOK AT PASS E (2026-10-28; "the crab legs look a little off"): a plate draws its own limbs,
   where the world's 1 px plots, seen from straight above, read as whiskers. The legs are jointed solids
   (limb, plateLeg), tapering to the foot, fanned and at rest, out from under the shell's rim, a dark line
   at each joint; the crab's claws fold in before its face, the arm out from under the shell; the
   lobster's antennae go out from under the eyes, back over the claws' elbows and down its sides, wide of
   the legs. The plate's alone (renderPlate passes {plate:true}): every world cell is pass E's, byte for byte. */
(function (root) {
  const K = root.IsoSolid;
  const S = 32, W = 64, H = 64, PX = 32, PY = 40, HPX = 32, HPY = 12, DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const HPIV = { lobster:{x:HPX,y:HPY+4}, crab:{x:HPX,y:HPY} };     // the grip in a held cell, per kind (pass E)
  const CELL = { W, H };
  const KEYLINE_DEFAULT = false, KEY = '#101a19', WATER = '#123034';
  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.12;
  const LN = (()=>{ const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  const hex2rgb=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  const rgb2hex=(r,g,b)=>'#'+[r,g,b].map(v=>Math.max(0,Math.min(255,Math.round(v))).toString(16).padStart(2,'0')).join('');
  const light=(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r+(255-r)*f, g+(255-g)*f, b+(255-b)*f); };
  const WRGB=hex2rgb(WATER), SPRGB=[125,154,70];
  // palettes verbatim from crustaceanRig.js (nothing invented) -> 5-step ramps
  const P1 = {
    lobster:{ CARA:{mid:'#c33a29',hi:'#e5604a',sh:'#8f2519',dp:'#5c150e'}, CLAW:{mid:'#d04434',hi:'#f06e55',sh:'#9a2b1d',dp:'#651710'},
      LEG:{mid:'#a83122',hi:'#c5493a',sh:'#6c1b11',dp:'#6c1b11'}, RUST:{mid:'#e08a3e',hi:'#f4ab62',sh:'#a85c22',dp:'#a85c22'},
      CREAM:{mid:'#f0e2c4',hi:'#fbf1da',sh:'#c2a877',dp:'#c2a877'}, EYE:{mid:'#1a0705',hi:'#1a0705',sh:'#1a0705',dp:'#1a0705'} },
    crab:{ CARA:{mid:'#b25e3e',hi:'#cf7a52',sh:'#8a4530',dp:'#5f2c20'}, CLAW:{mid:'#b25e3e',hi:'#cf7a52',sh:'#8a4530',dp:'#5f2c20'},
      LEG:{mid:'#8f4630',hi:'#ad5a3c',sh:'#5a281d',dp:'#5a281d'}, RUST:{mid:'#ad5a3c',hi:'#cf7a52',sh:'#5a281d',dp:'#5a281d'},
      CREAM:{mid:'#cdb890',hi:'#ece0c8',sh:'#9c7f57',dp:'#9c7f57'}, EYE:{mid:'#241512',hi:'#241512',sh:'#241512',dp:'#241512'} },
  };
  // the live lobster (pass E): dark olive shell, claws and legs; pass 1's red is the cooked one's
  const LIVE = { CARA:{mid:'#454e24',hi:'#5a6430',sh:'#323a1a',dp:'#1f2410',top:'#717b3d'}, CLAW:{mid:'#4b5626',hi:'#616d32',sh:'#373f1b',dp:'#22280f',top:'#78843f'},
    LEG:{mid:'#565427',hi:'#6e6b39',sh:'#39371b',dp:'#39371b'} };
  const PAL = { lobster:Object.assign({}, P1.lobster, LIVE), cooked:P1.lobster, crab:P1.crab };
  const ramp5=(m)=>[m.dp,m.sh,m.mid,m.hi,m.top||light(m.hi,0.22)];      // top: LIVE's own lit step (pass E)
  const MATS={};
  for (const k in PAL){ MATS[k]={}; for (const m in PAL[k]) MATS[k][m]={ramp:ramp5(PAL[k][m]),off:0}; MATS[k].BELLY={ramp:MATS[k].CARA.ramp,off:-1}; }

  const KINDS=['lobster','crab'];
  const HELD_ROLL = Math.PI/8;      // a crab held by the back turns 22.5deg about its length: never edge-on (pass B)
  const POSES={ walk:{n:4,ms:140}, rear:{n:1,ms:400}, defend:{n:1,ms:400}, held:{n:2,ms:420},
                flip:{n:4,ms:70}, sidle:{n:4,ms:120}, burrow:{n:4,ms:160} };
  const MOTION={
    lobster:{ walk:{v:0.07}, flip:{travel:-0.32, at:[0.05,0.45,0.85,1.0], hop:[0,0.06,0.10,0.04]} },
    crab:   { walk:{v:0.05}, sidle:{v:0.16, axis:'x'}, burrow:{sink:[0,0.014,0.030,0.046]} },
  };
  const SIZES={ lobster:{len:0.40, body:0.26, mass:0.7, label:'LOBSTER', range:[0.83,1.79]}, crab:{len:0.13, span:0.30, mass:0.4, label:'ROCK CRAB', range:[0.63,1]} };

  // ---- transforms (points in metres) ------------------------------------------------------------
  const rotX=(a)=>{ const c=Math.cos(a), s=Math.sin(a); return (p)=>[p[0], p[1]*c-p[2]*s, p[1]*s+p[2]*c]; };
  const rotY=(a)=>{ const c=Math.cos(a), s=Math.sin(a); return (p)=>[p[0]*c+p[2]*s, p[1], -p[0]*s+p[2]*c]; };
  const rotZ=(a)=>{ const c=Math.cos(a), s=Math.sin(a); return (p)=>[p[0]*c-p[1]*s, p[0]*s+p[1]*c, p[2]]; };
  const mv=(dx,dy,dz)=>(p)=>[p[0]+dx,p[1]+dy,p[2]+dz];
  const sc=(s)=>(p)=>[p[0]*s,p[1]*s,p[2]*s];
  const chain=(...fns)=>(p)=>fns.reduce((q,f)=>f(q),p);
  const mid=(a,b)=>[(a[0]+b[0])/2,(a[1]+b[1])/2,(a[2]+b[2])/2];
  function orient(v, ref){           // wind so the normal points away from ref (outward)
    const a=v[0], b=v[1], c=v[2];
    const ux=b[0]-a[0],uy=b[1]-a[1],uz=b[2]-a[2], vx=c[0]-a[0],vy=c[1]-a[1],vz=c[2]-a[2];
    const nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    let gx=0,gy=0,gz=0; for (const p of v){ gx+=p[0]; gy+=p[1]; gz+=p[2]; } gx/=v.length; gy/=v.length; gz/=v.length;
    return (nx*(gx-ref[0])+ny*(gy-ref[1])+nz*(gz-ref[2]))<0 ? v.slice().reverse() : v;
  }
  // a plate's solids (pass E): while FINE is set, each lathe's stations are eased between (Catmull-Rom,
  // FINE.st to a span) and its rings take FINE.nr x the sides; the bias keeps its station and side
  let FINE = null;
  function refine(st, n){
    const keys=['y','x','rx','rz','zc'], at=(i)=>st[Math.max(0,Math.min(st.length-1,i))], out=[];
    for (let i=0;i+1<st.length;i++) for (let j=0;j<n;j++){ const t=j/n, s={};
      for (const k of keys){ const p0=at(i-1)[k]||0, p1=at(i)[k]||0, p2=at(i+1)[k]||0, p3=at(i+2)[k]||0;
        s[k]=0.5*(2*p1+(p2-p0)*t+(2*p0-5*p1+4*p2-p3)*t*t+(3*p1-p0-3*p2+p3)*t*t*t); }
      s.rx=Math.max(0.0005,s.rx); s.rz=Math.max(0.0005,s.rz); out.push(s); }
    out.push(Object.assign({}, st[st.length-1]));
    return out;
  }
  function lathe(F, st, NR, mat, b, xf, biasOf){
    if (FINE){ const n=FINE.st, m=FINE.nr, bo=biasOf; st=refine(st, n); NR*=m; if (bo) biasOf=(i,k)=>bo(Math.floor(i/n), Math.floor(k/m)); }
    latheRaw(F, st, NR, mat, b, xf, biasOf);
  }
  // lathe along +y through stations [{y,x,rx,rz,zc}], NR-gon rings, capped, outward-wound
  function latheRaw(F, st, NR, mat, b, xf, biasOf){
    const rings=st.map(s=>{ const ring=[]; for (let k=0;k<NR;k++){ const ph=k/NR*2*Math.PI;
      ring.push(xf([(s.x||0)+Math.cos(ph)*s.rx, s.y, s.zc+Math.sin(ph)*s.rz])); } return ring; });
    const cen=st.map(s=>xf([(s.x||0), s.y, s.zc]));
    for (let i=0;i+1<rings.length;i++){ const r0=rings[i], r1=rings[i+1], ref=mid(cen[i],cen[i+1]);
      for (let k=0;k<NR;k++){ const k2=(k+1)%NR;
        F.push({ v:orient([r0[k],r0[k2],r1[k2],r1[k]], ref), mat, b:biasOf?biasOf(i,k):(b||0), db:0 }); } }
    F.push({ v:orient(rings[0].slice(), mid(cen[0],cen[1])), mat, b:b||0, db:0 });
    F.push({ v:orient(rings[rings.length-1].slice(), mid(cen[cen.length-1],cen[cen.length-2])), mat, b:b||0, db:0 });
  }
  function ellipsoid(F, c, r, NR, mat, b, xf, nst){
    nst=nst||5; const st=[];
    if (FINE){ nst=(nst-1)*FINE.st+1; NR*=FINE.nr; }
    for (let i=0;i<nst;i++){ const t=i/(nst-1)*Math.PI, f=Math.max(0.12, Math.sin(t));
      st.push({ x:c[0], y:c[1]+r[1]*Math.cos(t), rx:r[0]*f, rz:r[2]*f, zc:c[2] }); }
    latheRaw(F, st, NR, mat, b, xf);
  }
  const boxAt=(F,c,h,mat,b,db,xf)=>{ for (const f of K.box(c,h,mat,b,db)) F.push({ v:f.v.map(xf), mat:f.mat, b:f.b, db:f.db }); };
  const barAt=(F,a,b2,r,mat,b,xf)=>{ for (const f of K.bar(a,b2,r,mat,b,0)) F.push({ v:f.v.map(xf), mat:f.mat, b:f.b, db:0 }); };
  const line=(Pl,a,b,mat,k,xf)=>Pl.push({ a:xf(a), b:xf(b), mat, k:k==null?2:k });
  const dot =(Pl,a,mat,k,xf)=>Pl.push({ a:xf(a), b:xf(a), mat, k:k==null?2:k });
  // a plate's limb (the owner's look at pass E): a lathe from a to b through stations [[t, rx, rz]] (t 0 at a,
  // 1 at b; rx across and level, never under rmin; rz up); its frame comes back for the joints' marks
  function limb(F, a, b, st, mat, bias, xf, rmin){
    const d=[b[0]-a[0],b[1]-a[1],b[2]-a[2]], len=Math.hypot(d[0],d[1],d[2])||1e-6, D=d.map(c=>c/len);
    const m=Math.hypot(D[0],D[1]), Sd=m>1e-6?[D[1]/m,-D[0]/m,0]:[1,0,0];
    const U=[Sd[1]*D[2]-Sd[2]*D[1], Sd[2]*D[0]-Sd[0]*D[2], Sd[0]*D[1]-Sd[1]*D[0]];
    const at=(p)=>xf([a[0]+Sd[0]*p[0]+D[0]*p[1]+U[0]*p[2], a[1]+Sd[1]*p[0]+D[1]*p[1]+U[1]*p[2], a[2]+Sd[2]*p[0]+D[2]*p[1]+U[2]*p[2]]);
    const r=(t,rx,rz)=>({ y:t*len, rx:Math.max(rx,rmin||0), rz, zc:0 });
    lathe(F, st.map(([t,rx,rz])=>r(t,rx,rz)), 6, mat, bias, at);
    return { at, len, arc:(t,rx,rz)=>{ const q=r(t,rx,rz), o=[];                     // a joint: over the top, at t
      for (let k=0;k<=8;k++){ const ph=k/8*Math.PI; o.push(at([Math.cos(ph)*q.rx*1.1, q.y, Math.sin(ph)*q.rz*1.1])); } return o; } };
  }
  // a plate's leg, at rest: from its root under the shell, each joint a heading (deg from abeam, + forward) and
  // a reach (m, level), down to the knee's, the ankle's and the foot's heights; the joints go to mk
  function plateLeg(F, Pl, mk, sd, root, hd, len, z, R, mat, bias, tip, xf, rmin){
    const step=(p,h,l,zz)=>[p[0]+sd*Math.cos(h*DEG)*l, p[1]+Math.sin(h*DEG)*l, zz];
    const p1=step(root,hd[0],len[0],z[0]), p2=step(p1,hd[1],len[1],z[1]), p3=step(p2,hd[2],len[2],z[2]);
    limb(F, root, p1, R[0], mat, bias, xf, rmin);
    const b=limb(F, p1, p2, R[1], mat, bias-0.1, xf, rmin), c=limb(F, p2, p3, R[2], mat, bias-0.2, xf, rmin);
    mk.legJoints.push(b.arc(0, R[1][0][1], R[1][0][2]), c.arc(0, R[2][0][1], R[2][0][2]));
    dot(Pl, p3, tip, 1, xf);
  }

  // ---- the lobster (metres at scale 1, body frame: +y forward, +x right, z up, ground 0) --------
  function lobster(pose, f, s, o){
    const F=[], Pl=[];
    const held=pose==='held', defend=pose==='defend', rear=pose==='rear', flip=pose==='flip', walk=pose==='walk';
    const ph=f/POSES.walk.n*Math.PI*2;
    const swy = walk ? Math.sin(ph)*0.07 : 0;                                   // abdomen sway (rad)
    const curl = flip ? [0.5,1.5,0.9,0.2][f] : held ? 0.55 : 0;                  // abdomen curl under (rad)
    const bodyPitch = flip ? [0.15,0.55,0.35,0.05][f] : rear ? 0.30 : held ? -1.2 : 0;
    const lift = flip ? MOTION.lobster.flip.hop[f] : 0;
    const pivY = rear ? -0.06 : 0;
    const body = chain(mv(0,-pivY,-0.02), rotX(bodyPitch), rotY(o.sway||0), mv(0,pivY,0.02+lift));
    const A = chain(sc(s), body);
    // cephalothorax
    lathe(F, [ { y:0.118, rx:0.007, rz:0.007, zc:0.040 }, { y:0.098, rx:0.021, rz:0.017, zc:0.038 },
               { y:0.062, rx:0.036, rz:0.028, zc:0.040 }, { y:0.020, rx:0.038, rz:0.030, zc:0.040 },
               { y:-0.014, rx:0.032, rz:0.025, zc:0.038 } ], 8, 'CARA', 0.05, A,
          (i,k)=> (k>=5&&k<=6) ? -0.9 : (i===0?0.2:0.05));                       // underside darker
    const mk = FINE ? { joints:[], claws:[], legJoints:[] } : null;                // a plate's marks (pass E)
    const plate = !!o.plate, rmin = 0.55/(S*s);                                  // a plate's own limbs (the owner's look)
    if (mk){ mk.groove=[]; for (let t=-1;t<=1.0001;t+=0.1) mk.groove.push(A([t*0.036, 0.052-0.012*t*t, 0.040+0.0285*Math.sqrt(Math.max(0,1-t*t))]));
      mk.rostrum=[A([-0.0045,0.112,0.050]), A([0,0.142,0.047]), A([0.0045,0.112,0.050])]; }
    // abdomen: six segments curling under from the thorax
    const segL=[0.026,0.025,0.024,0.023,0.022,0.020], segR=[0.031,0.029,0.026,0.023,0.020,0.017], segH=[0.024,0.022,0.020,0.018,0.016,0.014];
    let org=[0,-0.014,0.036], dirP=0, frame=null;
    for (let k=0;k<6;k++){
      dirP += curl/6;
      const yaw = swy*(k+1)/6;
      const lf = chain(rotZ(yaw), rotX(dirP), mv(org[0],org[1],org[2]));
      frame=lf;
      if (mk){ const r=[]; for (let a=0;a<=12;a++) r.push(chain(lf,A)([Math.cos(a/12*Math.PI)*segR[k], 0, Math.sin(a/12*Math.PI)*segH[k]])); mk.joints.push(r); }
      lathe(F, [ { y:0, rx:segR[k], rz:segH[k], zc:0 }, { y:-segL[k], rx:segR[k]*0.94, rz:segH[k]*0.94, zc:0 } ], 8, 'CARA',
            k%2 ? -0.30 : 0.15, chain(lf, A), (i,kk)=> (kk>=5&&kk<=6) ? -0.9 : (k%2 ? -0.30 : 0.15));
      org = lf([0,-segL[k],0]);
    }
    // tail fan: telson + two uropods a side, plates in the last segment's frame
    boxAt(F,[0,-0.024,0],[0.011,0.024,0.0025],'CARA',0.10,0.01,chain(frame,A));
    for (const sd of [-1,1]){
      boxAt(F,[sd*0.022,-0.020,-0.001],[0.010,0.022,0.0022],'CARA',-0.05,0.005,chain(rotZ(sd*0.35),frame,A));
      dot(Pl,[sd*0.024,-0.040,0.002],'RUST',1,chain(rotZ(sd*0.35),frame,A));
    }
    dot(Pl,[0,-0.046,0.003],'RUST',2,chain(frame,A));
    // claws: shoulder -> merus -> claw (crusher starboard, pincer port)
    const spread = defend?1.0 : held?0.30 : rear?0.85 : 0.62;
    const raise  = defend?0.045 : rear?0.05 : 0;
    for (const sd of [-1,1]){
      const bob = walk ? Math.sin(ph+sd)*0.004 : 0;
      const shoulder=[sd*0.030, 0.088, 0.030], elbow=[sd*(0.045+0.035*spread), 0.128+0.008*(1-spread)+bob, 0.030+raise*0.5+(held?-0.035:0)];
      barAt(F, shoulder, elbow, [0.008,0.007], 'CLAW', 0, A);
      const crusher = sd>0, cl = crusher?0.105:0.085, cw = crusher?0.024:0.017, ch = crusher?0.016:0.012;
      const yaw = 0.15+0.55*spread, pitch = defend?0.5 : (rear&&crusher)?0.6 : held?-1.15 : 0.04;
      const cxf = chain(rotX(pitch), rotZ(-sd*yaw), mv(elbow[0],elbow[1],elbow[2]), A);
      if (mk) mk.claws.push({ teeth: crusher ? 'blunt' : 'fine', gape:[cxf([sd*cw*0.42, cl*0.56, ch*0.9]), cxf([sd*cw*0.40, cl*1.0, ch*0.3])] });
      ellipsoid(F, [0,cl*0.5,0], [cw,cl*0.5,ch], 8, 'CLAW', 0.05, cxf, 6);
      const gape = defend?0.5:0.08;                                              // dactyl on the outboard edge
      boxAt(F,[sd*cw*0.62,cl*0.80,0],[0.0035,cl*0.24,0.0045],'CLAW',0.25,-0.01,
            chain(mv(-sd*cw*0.62,-cl*0.58,0), rotZ(sd*gape), mv(sd*cw*0.62,cl*0.58,0), cxf));
      dot(Pl,[sd*cw*0.2,cl*1.02,0],'CREAM',3,cxf);                              // pale tip
      dot(Pl,[0,cl*0.02,ch*0.9],'RUST',2,cxf);                                   // band pip at the joint
      dot(Pl,[0,cl*0.45,ch*0.95],'CLAW',4,cxf);                                  // shell glint
    }
    // legs: four a side, two segments each, 1 px plots; a plate's jointed, tapering, fanned and at rest
    if (plate) for (const sd of [-1,1]) for (const [y,hd] of [[0.066,[24,46,58]],[0.042,[6,24,34]],[0.018,[-10,-22,-30]],[-0.006,[-30,-46,-56]]])
      plateLeg(F, Pl, mk, sd, [sd*0.026, y, 0.024], hd, [0.040,0.022,0.009], [0.034,0.012,0], [[[0,0.0038,0.0030],[0.5,0.0046,0.0032],[1,0.0036,0.0028]],
        [[0,0.0033,0.0027],[1,0.0027,0.0022]], [[0,0.0022,0.0019],[1,0.0005,0.0005]]], 'LEG', -0.3, 'RUST', A, rmin);
    else for (const sd of [-1,1]) for (let i=0;i<4;i++){
      const by=0.066-i*0.032, base=[sd*0.030, by, 0.022];
      const step = walk ? Math.sin(ph+i*1.4+(sd>0?0:Math.PI))*0.018 : 0;
      const splay = held ? 0.45 : flip ? 0.35 : 1.0;
      let knee, foot;
      if (held){ knee=[sd*0.045, by+0.020, 0.010]; foot=[sd*0.050, by+0.055, -0.010]; }
      else { knee=[sd*(0.030+0.040*splay), by+step*0.6-0.005*i, 0.032+(flip?0.01:0)]; foot=[sd*(0.030+0.075*splay), by+step-0.010*i, flip?0.02:0]; }
      line(Pl, base, knee, 'LEG', 2, A); line(Pl, knee, foot, 'LEG', 1, A); dot(Pl, foot, 'RUST', 1, A);
    }
    // antennae: long rust whips swept back, short antennules forward, stalked eyes; a plate's go out and
    // forward from under the eyes, back over the claws' elbows, then down the sides wide of the legs
    for (const sd of [-1,1]){
      const sw = walk ? Math.sin(ph*0.5+sd)*0.012 : 0;
      if (plate){ const C=[[0.016,0.112,0.046],[0.032,0.134,0.054],[0.054,0.142,0.060],[0.078,0.130,0.064],[0.098,0.100,0.064],
          [0.111,0.052,0.060],[0.119,-0.004,0.054],[0.124,-0.066,0.046],[0.126,-0.130,0.036]].map(p=>[sd*p[0],p[1],p[2]]), pts=[];
        for (let i=0;i+1<C.length;i++) for (let j=0;j<3;j++){ const t=j/3, p0=C[Math.max(0,i-1)], p1=C[i], p2=C[i+1], p3=C[Math.min(C.length-1,i+2)];
          pts.push([0,1,2].map(k=>0.5*(2*p1[k]+(p2[k]-p0[k])*t+(2*p0[k]-5*p1[k]+4*p2[k]-p3[k])*t*t+(3*p1[k]-p0[k]-3*p2[k]+p3[k])*t*t*t))); }
        pts.push(C[C.length-1]);
        for (let i=0;i+1<pts.length;i++) line(Pl, pts[i], pts[i+1], 'RUST', i<3?2:1, A);
      } else {
      const pts=[]; for (let t=0;t<=1.001;t+=0.25){
        pts.push([ sd*(0.010+0.055*t+0.03*t*t)+sw*t, 0.112-0.30*t*(held?0.55:1), 0.046+0.02*Math.sin(Math.PI*t)-(held?0.05*t:0) ]); }
      for (let i=0;i+1<pts.length;i++) line(Pl, pts[i], pts[i+1], 'RUST', i<2?2:1, A); }
      line(Pl, [sd*0.008,0.114,0.042], [sd*0.020,0.150,0.050], 'RUST', 2, A);
      dot(Pl, [sd*0.011, 0.102, 0.053], 'EYE', 2, A);
    }
    return { F, Pl, grip:A([0,0.030,0.070]), sink:0, mk };
  }

  // ---- the rock crab (metres at scale 1) ----------------------------------------------------------
  function crab(pose, f, s, o){
    const F=[], Pl=[];
    const held=pose==='held', defend=pose==='defend', rear=pose==='rear', sidle=pose==='sidle', burrow=pose==='burrow', walk=pose==='walk';
    const ph=f/4*Math.PI*2;
    const sink = burrow ? MOTION.crab.burrow.sink[f] : 0;
    const bodyPitch = rear ? 0.25 : held ? -1.0 : 0;
    const body = chain(mv(0,0,-0.02), rotX(bodyPitch), rotY(o.sway||0), mv(0,0,0.02-sink));
    const A = chain(sc(s), body);
    ellipsoid(F, [0,0,0.032], [0.065,0.045,0.017], 10, 'CARA', 0.05, A, 6);        // wide flat carapace
    const mk = FINE ? { claws:[], teeth:[], legJoints:[] } : null;                 // a plate's marks (pass E)
    const plate = !!o.plate, rmin = 0.55/(S*s);                                  // a plate's own limbs (the owner's look)
    if (mk) for (const sd of [-1,1]) for (let k=0;k<9;k++){                         // nine teeth a side, the eye to the widest
      const f0=(76-70*k/9)*DEG, f1=(76-70*(k+1)/9)*DEG, pt=(f,r)=>A([sd*Math.cos(f)*0.065*r, Math.sin(f)*0.045*r, 0.032]);
      mk.teeth.push([pt(f0,0.985), pt((f0+f1)/2,1.07), pt(f1,0.985)]); }
    boxAt(F,[0,0,0.017],[0.054,0.036,0.004],'BELLY',-0.6,0,A);                      // underside plate
    for (const sd of [-1,1]){                                                       // claws
      const up = defend || (rear && sd>0), tuck = sidle || burrow || held;
      const yaw = plate ? -1.0 : up ? 0.35 : tuck ? 1.15 : 0.55, pitch = plate ? 0.05 : up ? 0.9 : held ? -0.9 : 0.1;
      const base = plate ? [sd*0.054, 0.046, 0.027] : [sd*0.045, 0.030, 0.026+(up?0.01:0)];
      const cl=0.05, cw=0.016, ch=0.012;
      if (plate){                                                                   // a plate's: the arm out from under the shell, the wrist,
        limb(F, [sd*0.034,0.018,0.023], base, [[0,0.0070,0.0062],[1,0.0082,0.0072]], 'CLAW', 0, A, rmin);   // the hand folded in before the face
        ellipsoid(F, base, [0.0095,0.0095,0.0085], 8, 'CLAW', 0.05, A, 5); }
      const cxf=chain(rotX(pitch), rotZ(-sd*yaw), mv(base[0],base[1],base[2]), A);
      if (mk) mk.claws.push({ teeth:null, gape:[cxf([sd*cw*0.40, cl*0.58, ch*0.9]), cxf([sd*cw*0.38, cl*1.0, ch*0.3])] });
      ellipsoid(F, [0,cl*0.5,0], [cw,cl*0.5,ch], 8, 'CLAW', 0.05, cxf, 5);
      const gape = defend ? 0.5 : 0.1;
      boxAt(F,[sd*cw*0.6,cl*0.82,0],[0.003,cl*0.22,0.004],'CLAW',0.25,-0.01,
            chain(mv(-sd*cw*0.6,-cl*0.6,0), rotZ(sd*gape), mv(sd*cw*0.6,cl*0.6,0), cxf));
      dot(Pl,[sd*cw*0.2,cl*1.02,0],'CREAM',3,cxf);
    }
    if (plate) for (const sd of [-1,1]) for (const [a,hd,ln] of [[2,[30,55,68],[0.050,0.022,0.014]],[-18,[8,28,38],[0.056,0.024,0.015]],
        [-36,[-16,-34,-44],[0.056,0.024,0.015]],[-54,[-40,-60,-72],[0.050,0.022,0.014]]])       // a plate's legs: from under the shell's rim,
      plateLeg(F, Pl, mk, sd, [sd*0.065*0.78*Math.cos(a*DEG), 0.045*0.78*Math.sin(a*DEG), 0.022], hd, ln, [0.040,0.018,0],   // jointed, tapering, fanned
        [[[0,0.0054,0.0036],[0.6,0.0064,0.0038],[1,0.0050,0.0036]], [[0,0.0046,0.0038],[1,0.0040,0.0032]],
         [[0,0.0033,0.0028],[0.6,0.0024,0.0021],[1,0.0005,0.0005]]], 'LEG', 0, 'CREAM', A, rmin);
    else for (const sd of [-1,1]) for (let i=0;i<4;i++){                           // legs
      const by=0.030-i*0.024, base=[sd*0.058, by, 0.024];
      const phase = ph+i*1.6+(sd>0?0:2.2);
      const st = walk ? Math.sin(phase)*0.02 : 0, sx = sidle ? Math.sin(phase)*0.022 : 0;
      const splay = burrow ? 1.25 : held ? 0.5 : 1.0;
      const dig = burrow ? Math.sin(ph*2+i)*0.008 : 0;
      const knee=[sd*(0.058+0.045*splay)+sx*0.6, by+st*0.6-0.004*i, 0.038+(held?-0.025:0)];
      const foot=[sd*(0.058+0.085*splay)+sx, by+st-0.008*i, (held?-0.030:0)+dig];
      line(Pl, base, knee, 'LEG', 2, A); line(Pl, knee, foot, 'LEG', 1, A); dot(Pl, foot, 'CREAM', 1, A);
    }
    for (const sd of [-1,1]){ dot(Pl,[sd*0.012,0.047,0.038],'EYE',2,A); dot(Pl,[sd*0.032,0.044,0.036],'CARA',0,A); }
    dot(Pl,[0,0.049,0.036],'CARA',0,A);
    return { F, Pl, grip:A([0,0,0.049]), sink, mk };
  }

  // ---- raster: z-buffered facets + depth-tested plots, dither, edge darkening, tints ------------
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  const shadeOf=(n,se,ce)=>n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];
  function _paint(kind, geo, o, cell){
    const pk = kind==='lobster' && o.cooked ? 'cooked' : kind;               // pass E: a cooked lobster keeps pass 1's red
    const B=K.camBasis({dir:o.dir||0, elev:o.elev}), M=MATS[pk];
    const RINDEX={}; for (const m in M) M[m].ramp.forEach((c,i)=>{ RINDEX[c]={r:M[m].ramp,i}; });
    const plate=!!cell; cell=cell||CELL;                                      // a plate's cell (pass E), or the world's
    const W=cell.W, H=cell.H, ks=plate?(o.scale||1):1;
    const cxp=plate?cell.cx:o.held?HPIV[kind].x:PX, cyp=plate?cell.cy:o.held?HPIV[kind].y:PY, sh=o.held?geo.grip:[0,0,0];
    const pv=(p)=>K.proj(p[0]-sh[0],p[1]-sh[1],p[2]-sh[2],B,cxp,cyp);
    const zbuf=new Float32Array(W*H).fill(Infinity), col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H), zw=new Float32Array(W*H), shv=new Float32Array(W*H), mt=new Array(W*H).fill(null);
    for (const f of geo.F){
      const rv=f.v.map(pv), n=normal(rv[0],rv[1],rv[2]);
      const fidx=shadeOf(n,B.se,B.ce)*GAIN+BIAS+(f.b||0), MM=M[f.mat]||M.CARA;
      for (let tt=1;tt+1<rv.length;tt++) tri(rv[0],rv[tt],rv[tt+1]);
      function tri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy); if (Math.abs(area)<1e-6) return;
        for (let y=minY;y<=maxY;y++) for (let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area, w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area, w2=1-w0-w1;
          if (w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0), i=y*W+x;
          if (deff<zbuf[i]){ zbuf[i]=deff; dep[i]=d; zw[i]=w0*a.zr+w1*b.zr+w2*c.zr+sh[2];
            const base=Math.floor(fidx), idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+(MM.off||0);
            col[i]=MM.ramp[Math.max(0,Math.min(4,idx))]; shv[i]=fidx; mt[i]=f.mat; }
        }
      }
    }
    const wide = (o.scale||1) > 1.45 ? 1 : 0, lift = 0.012*ks;
    for (const L of geo.Pl){
      const bw = plate ? Math.max(1, cell.brush-(L.k<2?1:0)) : 1, thick = plate ? bw-1 : wide, tall = bw-1;   // a plate's thin plots 1 px (pass E)
      const a=pv(L.a), b=pv(L.b), n=Math.max(1, Math.ceil(Math.max(Math.abs(b.sx-a.sx), Math.abs(b.sy-a.sy))*1.2));
      const ramp=(M[L.mat]||M.LEG).ramp, c=ramp[Math.max(0,Math.min(4,L.k))];
      for (let i=0;i<=n;i++){ const t=i/n, sx=a.sx+(b.sx-a.sx)*t, sy=a.sy+(b.sy-a.sy)*t, d=a.d+(b.d-a.d)*t, zr=a.zr+(b.zr-a.zr)*t;
        for (let tx=0;tx<=thick;tx++) for (let ty=0;ty<=tall;ty++){
          const x=Math.floor(sx)+tx, y=Math.floor(sy)+ty; if (x<0||x>=W||y<0||y>=H) continue;
          const j=y*W+x; if (d-lift>zbuf[j]) continue;
          zbuf[j]=d-lift; dep[j]=d; zw[j]=zr+sh[2]; col[j]=c; mt[j]=null; } }
    }
    // the glint (catch review pass B): the carapace's most lit pixel, and the crab's most lit claw
    // pixel (the lobster's claws carry their own), lighten halfway to white: a wet shell at any heading
    for (const m of kind==='crab'?['CARA','CLAW']:['CARA']){ let best=-1;
      for (let i=0;i<W*H;i++) if (mt[i]===m && (best<0 || shv[i]>shv[best])) best=i;
      if (best>=0) col[best]=light(PAL[pk][m].hi,0.5); }
    const out=col.slice();
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x; if (!col[i]) continue;
      for (const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if (nx>=W||ny>=H) continue;
        const j=ny*W+nx; if (!col[j]) continue;
        if (Math.abs(dep[i]-dep[j])>EDGE*ks){ const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]]; if (e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)]; }
      }
    }
    const outline = o.outline===true || (o.outline==null && KEYLINE_DEFAULT);
    if (outline) for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x; if (out[i]) continue;
      for (const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx, ny=y+dy; if (nx<0||nx>=W||ny<0||ny>=H) continue;
        const j=ny*W+nx; if (col[j]){ out[i]=KEY; zw[i]=zw[j]; break; } }
    }
    if (plate && cell.post) cell.post({ kind, pk, M, RINDEX, out, col, mt, shv, dep, W, H, geo, pv, ks });
    const spoil=Math.max(0,Math.min(1,o.spoil||0));
    const sandZ = o.sandZ!=null ? o.sandZ : (o.pose==='burrow' ? 0 : null);
    const waterZ = o.waterZ===undefined ? null : o.waterZ;
    const rgba=new Uint8ClampedArray(W*H*4);
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x, c=out[i]; if (!c){ rgba[i*4+3]=0; continue; }
      if (sandZ!=null){ const dz=sandZ-zw[i]; if (dz>0.006 || (dz>-0.004 && BAYER[x&3][y&3]<0.5)){ rgba[i*4+3]=0; continue; } }
      let [rr,gg,bb]=hex2rgb(c), a=255;
      if (spoil>0 && c!==KEY){ const m=spoil*(0.40+(BAYER[x&3][y&3]<spoil*0.55?0.28:0));
        rr=rr*(1-m)+SPRGB[0]*m; gg=gg*(1-m)+SPRGB[1]*m; bb=bb*(1-m)+SPRGB[2]*m; }
      if (waterZ!=null && zw[i]<waterZ-0.004){ const dz=waterZ-zw[i], m=Math.min(0.72,0.30+dz*0.9);
        rr=rr*(1-m)+WRGB[0]*m; gg=gg*(1-m)+WRGB[1]*m; bb=bb*(1-m)+WRGB[2]*m; a=dz>0.35?115:160; }
      rgba[i*4]=Math.round(rr); rgba[i*4+1]=Math.round(gg); rgba[i*4+2]=Math.round(bb); rgba[i*4+3]=a;
    }
    return rgba;
  }
  function render(kind, opts){
    opts=opts||{};
    const k=KINDS.indexOf(kind)>=0?kind:'lobster';
    let pose=POSES[opts.pose]?opts.pose:'walk';
    if (k==='lobster' && (pose==='sidle'||pose==='burrow')) pose='walk';
    if (k==='crab' && pose==='flip') pose='walk';
    const n=POSES[pose].n, f=((opts.frame||0)%n+n)%n, scale=opts.scale||1, held=pose==='held';
    const geo=(k==='lobster'?lobster:crab)(pose, f, scale, { sway: held ? (f?0.10:-0.10)+(k==='crab'?HELD_ROLL:0) : 0 });
    const ang = held ? 0 : (opts.ang!=null ? opts.ang : 0);
    if (ang){ const R=rotZ(-ang); for (const fc of geo.F) fc.v=fc.v.map(R); for (const L of geo.Pl){ L.a=R(L.a); L.b=R(L.b); } geo.grip=R(geo.grip); }
    return _paint(k, geo, Object.assign({}, opts, { pose, held, scale }));
  }
  function hold(kind, scale){
    const base = kind==='crab' ? SIZES.crab.mass : SIZES.lobster.mass, s=scale||1, mass=base*s*s*s;
    return { mass:Math.round(mass*100)/100, hands: mass>=2.2 ? 2 : 1 };
  }
  function project(dir, p, elev){ const v=K.proj(p[0],p[1],p[2],K.camBasis({dir, elev}),PX,PY); return { dx:v.sx-PX, dy:v.sy-PY }; }

  // ---- the field guide's plate (pass E): the kind at more pixels, for plates only ----------------
  const PW=160, PH=96, PLATE_VIEW={ lobster:{ dir:6, elev:90 }, crab:{ dir:0, elev:90 } };
  const MARKS = {                                                                 // the marks a field guide shows, by kind
    lobster:['the shell mottled', 'the cervical groove across the back', 'the rostrum between the eyes', 'the tail\'s six segments jointed',
      'the claws unlike: the crusher\'s blunt teeth, the cutter\'s fine ones', 'the legs jointed and tapering', 'the antennae swept back wide of the body',
      'the wet glint'],
    crab:['nine teeth a side on the shell\'s front edge', 'the shell closely speckled', 'the claws folded before the face, their gape',
      'the legs jointed and tapering', 'the wet glint'] };
  const hsh=(x,y,s)=>{ let h=(x*374761393+y*668265263+s*1442695041)|0; h=Math.imul(h^(h>>>13),1274126177); return ((h^(h>>>16))>>>0)/4294967296; };
  function plateMarks(q){
    const { kind, pk, M, RINDEX, out, mt, shv, W, H, geo, pv } = q, mk=geo.mk;
    const at=(x,y)=>(x>=0&&y>=0&&x<W&&y<H)?y*W+x:-1;
    const own=(i,d)=>{ const e=RINDEX[out[i]]; if (e) out[i]=e.r[Math.max(0,Math.min(4,e.i+d))]; };
    const S2=(p)=>{ const v=pv(p); return [v.sx, v.sy]; };
    const seg=(a,b,fn)=>{ const n=Math.max(1,Math.ceil(Math.max(Math.abs(b[0]-a[0]),Math.abs(b[1]-a[1]))));
      for (let k=0;k<=n;k++){ const i=at(Math.floor(a[0]+(b[0]-a[0])*k/n), Math.floor(a[1]+(b[1]-a[1])*k/n)); if (i>=0) fn(i); } };
    const path=(pts,fn)=>{ const seen=new Set(), s=pts.map(S2); for (let k=0;k+1<s.length;k++) seg(s[k],s[k+1],(i)=>{ if (!seen.has(i)){ seen.add(i); fn(i); } }); };
    const fill=(pts,fn)=>{ const s=pts.map(S2); let x0=W,x1=0,y0=H,y1=0;
      for (const p of s){ x0=Math.min(x0,Math.floor(p[0])); x1=Math.max(x1,Math.ceil(p[0])); y0=Math.min(y0,Math.floor(p[1])); y1=Math.max(y1,Math.ceil(p[1])); }
      for (let y=Math.max(0,y0);y<=Math.min(H-1,y1);y++) for (let x=Math.max(0,x0);x<=Math.min(W-1,x1);x++){ const px=x+0.5, py=y+0.5; let inside=false;
        for (let a=0,b=s.length-1;a<s.length;b=a++){ const xa=s[a][0], ya=s[a][1], xb=s[b][0], yb=s[b][1];
          if ((ya>py)!==(yb>py) && px<(xb-xa)*(py-ya)/(yb-ya)+xa) inside=!inside; }
        if (inside) fn(y*W+x); } };
    const on=(m)=>(i)=>mt[i]===m;
    // the shell: the live lobster mottled in blotches, the rock crab closely speckled
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){ const i=y*W+x;
      if (kind==='crab'){ if (mt[i]!=='CARA') continue; const h=hsh(x,y,11); if (h<0.07) own(i,-2); else if (h<0.25) own(i,-1); }
      else if ((mt[i]==='CARA'||mt[i]==='CLAW') && hsh(x>>1,y>>1,7)<0.13 && hsh(x,y,9)<0.75) own(i,-1); }
    if (kind==='lobster'){
      path(mk.groove, (i)=>{ if (on('CARA')(i)) own(i,-2); });                       // the cervical groove
      for (const r of mk.joints) path(r, (i)=>{ if (on('CARA')(i)) own(i,-2); });      // the tail's joints
      const ro=mk.rostrum.map(S2), lit=(ro[0][1]+ro[1][1])<(ro[2][1]+ro[1][1]) ? 0 : 2;  // the rostrum, lit on its upper edge
      fill(mk.rostrum, (i)=>{ out[i]=M.CARA.ramp[2]; });
      path([mk.rostrum[lit], mk.rostrum[1]], (i)=>{ out[i]=M.CARA.ramp[3]; });
    } else {
      for (const t of mk.teeth){                                                     // the marginal teeth, the shell's own shade
        const s=t.map(S2), mx=(s[0][0]+s[2][0])/2, my=(s[0][1]+s[2][1])/2, d=Math.hypot(mx-s[1][0],my-s[1][1])||1;
        const j=at(Math.floor(mx+(mx-s[1][0])/d*1.5), Math.floor(my+(my-s[1][1])/d*1.5)), tone=j>=0&&mt[j]==='CARA' ? out[j] : M.CARA.ramp[2];
        fill(t, (i)=>{ if (!out[i] || mt[i]==='CARA') out[i]=tone; });
        for (const p of [t[0],t[2]]){ const n=S2(p), i=at(Math.floor(n[0]),Math.floor(n[1])); if (i>=0 && mt[i]==='CARA') own(i,-2); } }
    }
    for (const r of mk.legJoints) path(r, (i)=>{ if (on('LEG')(i)) own(i,-2); });       // the legs' joints (the owner's look)
    for (const g of mk.claws){                                                         // the claws: the gape, and the lobster's teeth
      path(g.gape, (i)=>{ if (on('CLAW')(i)) out[i]=M.CLAW.ramp[0]; });
      if (!g.teeth) continue;
      const a=S2(g.gape[0]), b=S2(g.gape[1]), len=Math.hypot(b[0]-a[0],b[1]-a[1])||1, ux=(b[0]-a[0])/len, uy=(b[1]-a[1])/len;
      const step=g.teeth==='blunt'?4:2, size=g.teeth==='blunt'?2:1;
      for (let t=step/2, k=0; t<len-1; t+=step, k++){ const sd=k%2?1:-1, x=Math.floor(a[0]+ux*t-uy*sd*1.5), y=Math.floor(a[1]+uy*t+ux*sd*1.5);
        for (let dy=0;dy<size;dy++) for (let dx=0;dx<size;dx++){ const i=at(x+dx,y+dy); if (i>=0 && on('CLAW')(i)) out[i]=M.CREAM.ramp[3]; } }
    }
    let best=-1; for (let i=0;i<W*H;i++) if (mt[i]==='CARA' && (best<0 || shv[i]>shv[best])) best=i;   // the wet glint, a plus
    if (best>=0){ const gl=light(PAL[pk].CARA.hi,0.5), x=best%W, y=(best/W)|0;
      for (const [dx,dy] of [[0,0],[1,0],[-1,0],[0,1],[0,-1]]){ const i=at(x+dx,y+dy); if (i>=0 && mt[i]==='CARA') out[i]=gl; } }
  }
  function renderPlate(kind, scale){
    if (KINDS.indexOf(kind)<0) return null;
    const s=scale||1, o=Object.assign({ pose:'walk', frame:0, held:false, scale:s }, PLATE_VIEW[kind]);
    FINE={ st:3, nr:3 };
    let geo; try { geo=(kind==='lobster'?lobster:crab)('walk', 0, s, { sway:0, plate:true }); } finally { FINE=null; }
    const B=K.camBasis({ dir:o.dir, elev:o.elev });                         // centred by its facets and plots
    let x0=Infinity, x1=-Infinity, y0=Infinity, y1=-Infinity;
    const see=(p)=>{ const v=K.proj(p[0],p[1],p[2],B,0,0); x0=Math.min(x0,v.sx); x1=Math.max(x1,v.sx); y0=Math.min(y0,v.sy); y1=Math.max(y1,v.sy); };
    for (const f of geo.F) for (const p of f.v) see(p);
    for (const L of geo.Pl){ see(L.a); see(L.b); }
    const cell={ W:PW, H:PH, cx:Math.round(PW/2-(x0+x1)/2), cy:Math.round(PH/2-(y0+y1)/2), brush:2, post:plateMarks };
    return _paint(kind, geo, o, cell);
  }
  root.Crustacean2 = { W, H, pivot:{x:PX,y:PY}, hpivot:HPIV, KINDS, POSES, MOTION, SIZES, KEYLINE_DEFAULT, KEY, WATER,
    defaultElev:DEFAULT_ELEV, render, hold, project, PW, PH, PLATE_VIEW, MARKS, renderPlate };
})(typeof globalThis!=='undefined'?globalThis:window);

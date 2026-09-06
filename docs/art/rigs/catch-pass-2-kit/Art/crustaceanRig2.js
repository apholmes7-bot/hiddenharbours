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
   Cell 64x64, pivot (32,40) = ground centre; held uses hpivot (32,12).
   Exposes globalThis.Crustacean2 = { W,H,pivot,hpivot,KINDS,POSES,MOTION,SIZES,KEYLINE_DEFAULT,
   render, hold, project }. Needs IsoSolid loaded first. */
(function (root) {
  const K = root.IsoSolid;
  const S = 32, W = 64, H = 64, PX = 32, PY = 40, HPX = 32, HPY = 12, DEG = Math.PI/180, DEFAULT_ELEV = 40;
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
  const ramp5=(m)=>[m.dp,m.sh,m.mid,m.hi,light(m.hi,0.22)];
  const MATS={};
  for (const k in P1){ MATS[k]={}; for (const m in P1[k]) MATS[k][m]={ramp:ramp5(P1[k][m]),off:0}; MATS[k].BELLY={ramp:MATS[k].CARA.ramp,off:-1}; }

  const KINDS=['lobster','crab'];
  const POSES={ walk:{n:4,ms:140}, rear:{n:1,ms:400}, defend:{n:1,ms:400}, held:{n:2,ms:420},
                flip:{n:4,ms:70}, sidle:{n:4,ms:120}, burrow:{n:4,ms:160} };
  const MOTION={
    lobster:{ walk:{v:0.07}, flip:{travel:-0.32, at:[0.05,0.45,0.85,1.0], hop:[0,0.06,0.10,0.04]} },
    crab:   { walk:{v:0.05}, sidle:{v:0.16, axis:'x'}, burrow:{sink:[0,0.014,0.030,0.046]} },
  };
  const SIZES={ lobster:{len:0.40, body:0.26, mass:0.7, label:'LOBSTER'}, crab:{len:0.13, span:0.30, mass:0.4, label:'ROCK CRAB'} };

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
  // lathe along +y through stations [{y,x,rx,rz,zc}], NR-gon rings, capped, outward-wound
  function lathe(F, st, NR, mat, b, xf, biasOf){
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
    for (let i=0;i<nst;i++){ const t=i/(nst-1)*Math.PI, f=Math.max(0.12, Math.sin(t));
      st.push({ x:c[0], y:c[1]+r[1]*Math.cos(t), rx:r[0]*f, rz:r[2]*f, zc:c[2] }); }
    lathe(F, st, NR, mat, b, xf);
  }
  const boxAt=(F,c,h,mat,b,db,xf)=>{ for (const f of K.box(c,h,mat,b,db)) F.push({ v:f.v.map(xf), mat:f.mat, b:f.b, db:f.db }); };
  const barAt=(F,a,b2,r,mat,b,xf)=>{ for (const f of K.bar(a,b2,r,mat,b,0)) F.push({ v:f.v.map(xf), mat:f.mat, b:f.b, db:0 }); };
  const line=(Pl,a,b,mat,k,xf)=>Pl.push({ a:xf(a), b:xf(b), mat, k:k==null?2:k });
  const dot =(Pl,a,mat,k,xf)=>Pl.push({ a:xf(a), b:xf(a), mat, k:k==null?2:k });

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
    // abdomen: six segments curling under from the thorax
    const segL=[0.026,0.025,0.024,0.023,0.022,0.020], segR=[0.031,0.029,0.026,0.023,0.020,0.017], segH=[0.024,0.022,0.020,0.018,0.016,0.014];
    let org=[0,-0.014,0.036], dirP=0, frame=null;
    for (let k=0;k<6;k++){
      dirP += curl/6;
      const yaw = swy*(k+1)/6;
      const lf = chain(rotZ(yaw), rotX(dirP), mv(org[0],org[1],org[2]));
      frame=lf;
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
      ellipsoid(F, [0,cl*0.5,0], [cw,cl*0.5,ch], 8, 'CLAW', 0.05, cxf, 6);
      const gape = defend?0.5:0.08;                                              // dactyl on the outboard edge
      boxAt(F,[sd*cw*0.62,cl*0.80,0],[0.0035,cl*0.24,0.0045],'CLAW',0.25,-0.01,
            chain(mv(-sd*cw*0.62,-cl*0.58,0), rotZ(sd*gape), mv(sd*cw*0.62,cl*0.58,0), cxf));
      dot(Pl,[sd*cw*0.2,cl*1.02,0],'CREAM',3,cxf);                              // pale tip
      dot(Pl,[0,cl*0.02,ch*0.9],'RUST',2,cxf);                                   // band pip at the joint
      dot(Pl,[0,cl*0.45,ch*0.95],'CLAW',4,cxf);                                  // shell glint
    }
    // legs: four a side, two segments each, 1 px plots
    for (const sd of [-1,1]) for (let i=0;i<4;i++){
      const by=0.066-i*0.032, base=[sd*0.030, by, 0.022];
      const step = walk ? Math.sin(ph+i*1.4+(sd>0?0:Math.PI))*0.018 : 0;
      const splay = held ? 0.45 : flip ? 0.35 : 1.0;
      let knee, foot;
      if (held){ knee=[sd*0.045, by+0.020, 0.010]; foot=[sd*0.050, by+0.055, -0.010]; }
      else { knee=[sd*(0.030+0.040*splay), by+step*0.6-0.005*i, 0.032+(flip?0.01:0)]; foot=[sd*(0.030+0.075*splay), by+step-0.010*i, flip?0.02:0]; }
      line(Pl, base, knee, 'LEG', 2, A); line(Pl, knee, foot, 'LEG', 1, A); dot(Pl, foot, 'RUST', 1, A);
    }
    // antennae: long rust whips swept back, short antennules forward, stalked eyes
    for (const sd of [-1,1]){
      const sw = walk ? Math.sin(ph*0.5+sd)*0.012 : 0;
      const pts=[]; for (let t=0;t<=1.001;t+=0.25){
        pts.push([ sd*(0.010+0.055*t+0.03*t*t)+sw*t, 0.112-0.30*t*(held?0.55:1), 0.046+0.02*Math.sin(Math.PI*t)-(held?0.05*t:0) ]); }
      for (let i=0;i+1<pts.length;i++) line(Pl, pts[i], pts[i+1], 'RUST', i<2?2:1, A);
      line(Pl, [sd*0.008,0.114,0.042], [sd*0.020,0.150,0.050], 'RUST', 2, A);
      dot(Pl, [sd*0.011, 0.102, 0.053], 'EYE', 2, A);
    }
    return { F, Pl, grip:A([0,0.030,0.070]), sink:0 };
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
    boxAt(F,[0,0,0.017],[0.054,0.036,0.004],'BELLY',-0.6,0,A);                      // underside plate
    for (const sd of [-1,1]){                                                       // claws
      const up = defend || (rear && sd>0), tuck = sidle || burrow || held;
      const yaw = up ? 0.35 : tuck ? 1.15 : 0.55, pitch = up ? 0.9 : held ? -0.9 : 0.1;
      const base=[sd*0.045, 0.030, 0.026+(up?0.01:0)];
      const cl=0.05, cw=0.016, ch=0.012;
      const cxf=chain(rotX(pitch), rotZ(-sd*yaw), mv(base[0],base[1],base[2]), A);
      ellipsoid(F, [0,cl*0.5,0], [cw,cl*0.5,ch], 8, 'CLAW', 0.05, cxf, 5);
      const gape = defend ? 0.5 : 0.1;
      boxAt(F,[sd*cw*0.6,cl*0.82,0],[0.003,cl*0.22,0.004],'CLAW',0.25,-0.01,
            chain(mv(-sd*cw*0.6,-cl*0.6,0), rotZ(sd*gape), mv(sd*cw*0.6,cl*0.6,0), cxf));
      dot(Pl,[sd*cw*0.2,cl*1.02,0],'CREAM',3,cxf);
    }
    for (const sd of [-1,1]) for (let i=0;i<4;i++){                                // legs
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
    return { F, Pl, grip:A([0,0,0.049]), sink };
  }

  // ---- raster: z-buffered facets + depth-tested plots, dither, edge darkening, tints ------------
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  const shadeOf=(n,se,ce)=>n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];
  function _paint(kind, geo, o){
    const B=K.camBasis({dir:o.dir||0, elev:o.elev}), M=MATS[kind];
    const RINDEX={}; for (const m in M) M[m].ramp.forEach((c,i)=>{ RINDEX[c]={r:M[m].ramp,i}; });
    const cxp=o.held?HPX:PX, cyp=o.held?HPY:PY, sh=o.held?geo.grip:[0,0,0];
    const pv=(p)=>K.proj(p[0]-sh[0],p[1]-sh[1],p[2]-sh[2],B,cxp,cyp);
    const zbuf=new Float32Array(W*H).fill(Infinity), col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H), zw=new Float32Array(W*H);
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
            col[i]=MM.ramp[Math.max(0,Math.min(4,idx))]; }
        }
      }
    }
    const thick = (o.scale||1) > 1.45 ? 1 : 0;
    for (const L of geo.Pl){
      const a=pv(L.a), b=pv(L.b), n=Math.max(1, Math.ceil(Math.max(Math.abs(b.sx-a.sx), Math.abs(b.sy-a.sy))*1.2));
      const ramp=(M[L.mat]||M.LEG).ramp, c=ramp[Math.max(0,Math.min(4,L.k))];
      for (let i=0;i<=n;i++){ const t=i/n, sx=a.sx+(b.sx-a.sx)*t, sy=a.sy+(b.sy-a.sy)*t, d=a.d+(b.d-a.d)*t, zr=a.zr+(b.zr-a.zr)*t;
        for (let tx=0;tx<=thick;tx++){
          const x=Math.floor(sx)+tx, y=Math.floor(sy); if (x<0||x>=W||y<0||y>=H) continue;
          const j=y*W+x; if (d-0.012>zbuf[j]) continue;
          zbuf[j]=d-0.012; dep[j]=d; zw[j]=zr+sh[2]; col[j]=c; } }
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
    const outline = o.outline===true || (o.outline==null && KEYLINE_DEFAULT);
    if (outline) for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x; if (out[i]) continue;
      for (const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx, ny=y+dy; if (nx<0||nx>=W||ny<0||ny>=H) continue;
        const j=ny*W+nx; if (col[j]){ out[i]=KEY; zw[i]=zw[j]; break; } }
    }
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
    const geo=(k==='lobster'?lobster:crab)(pose, f, scale, { sway: held ? (f?0.10:-0.10) : 0 });
    const ang = held ? 0 : (opts.ang!=null ? opts.ang : 0);
    if (ang){ const R=rotZ(-ang); for (const fc of geo.F) fc.v=fc.v.map(R); for (const L of geo.Pl){ L.a=R(L.a); L.b=R(L.b); } geo.grip=R(geo.grip); }
    return _paint(k, geo, Object.assign({}, opts, { pose, held, scale }));
  }
  function hold(kind, scale){
    const base = kind==='crab' ? SIZES.crab.mass : SIZES.lobster.mass, s=scale||1, mass=base*s*s*s;
    return { mass:Math.round(mass*100)/100, hands: mass>=2.2 ? 2 : 1 };
  }
  function project(dir, p, elev){ const v=K.proj(p[0],p[1],p[2],K.camBasis({dir, elev}),PX,PY); return { dx:v.sx-PX, dy:v.sy-PY }; }
  root.Crustacean2 = { W, H, pivot:{x:PX,y:PY}, hpivot:{x:HPX,y:HPY}, KINDS, POSES, MOTION, SIZES, KEYLINE_DEFAULT, KEY, WATER,
    defaultElev:DEFAULT_ELEV, render, hold, project };
})(typeof globalThis!=='undefined'?globalThis:window);

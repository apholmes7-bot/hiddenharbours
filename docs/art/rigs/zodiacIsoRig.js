/* Hidden Harbours — parametric ISO coast-guard RHIB (rigid-hull inflatable), same bake recipe as
   consoleIsoRig.js / puntIsoRig.js. TWO BUILDS OFF ONE LOFT:
     build:'hurricane'  7.3 m over tubes — four jockey seats, full tubular arch, TWIN outboards
     build:'frc'        6.5 m over tubes — bench + open aft deck, low arch with a davit lifting eye,
                        SINGLE outboard
   Fixed 3/4 turntable camera (elev 40deg default), 45deg steps, flat-facet shading from the fleet's
   upper-left key, z-buffered, ordered dither, NO AA. 32 px = 1 m.

   WHAT IS NEW IN THIS RIG, versus the five painted hulls
   1. THE COLLAR. She is a rigid deep-V hull with an INFLATED TUBE swept along the sheer — the thing
      that makes a RHIB read at 30 px. It is a real swept solid: a 12-sided ring lofted along a plan
      curve that follows the sheer, converges to the stem at the bow and tapers to a cone abaft the
      transom. Ring angle picks the material, so the black D-fender, the underside wear patch and
      the chamber seams all fall out of the sweep instead of being drawn on.
   2. POSES BEYOND THE WAVE. A RHIB spends its life on plane and hard over, so rock(i) is not enough:
      plane(t) gives the bow-up planing trim through the hump, turn(s) gives the banked hard-over
      with the bow yawed inside the track. That needed a YAW term in the camera basis (rotation
      about the boat's own z BEFORE roll/pitch) — new here, and the thing that lets a heading-locked
      sprite show crab. Feed one pose object into every layer, exactly like rock(i).
   3. LIT LAYERS. The beacon lens and the searchlight drum are tagged overlay layers painted WITH the
      hull and masked to their own pixels, so the arch correctly occludes them. The hull carries the
      dark lens and the light's yoke, so she is complete with no overlay drawn.
   4. NO KEYLINE. Per ADR 0031 she bakes ringless from birth; KEYLINE_DEFAULT=false with
      render(dir,{outline:true}) kept as a live A/B, never as the shipping default.

   OUTBOARDS ARE NOT IN THIS FILE. She mounts skiffMotorRig.js — the rigid hull is lofted so her
   transom clamp lands on that rig's own swivel axis (y=-3.57, z=0.72) for the 'hurricane' build, so
   SkiffMotor pins with no maths. Use variant:'guard' (white cowl, orange band) for the service
   livery, twins at MOTOR.mounts.dual. The 'frc' build is shorter, so its clamp sits forward of the
   skiff's — motorOffset(dir,opts) returns the exact per-heading screen shift for EITHER build,
   already including the two cells' pivot difference. Draw the far engine first.

   LIVERY IS FIXED and hand-tuned — no scheme table, no paint mixer. A rescue boat is the most
   saturated thing on this water on purpose, so her orange ramps deliberately break the fleet's
   C_CAP=0.115 chroma ceiling; that is a livery decision, not an oversight, and it is why she has no
   rampFrom(). Markings are a reflective stripe plus abstracted decal blocks — a wordmark is 4 px
   tall at 32 px/m, so it is massed, not lettered.

   Cell 272x248, pivot (136,138) = boat origin (amidships, keel bottom, centreline), pinned every
   heading. Exposes globalThis.ZodiacIso. */
(function (root) {
  const PX = 32, S = 32;
  const W = 272, H = 248, cx = 136, cy = 138;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;                  // ADR 0031 — ringless from birth; {outline:true} is the A/B

  // ---- poses -----------------------------------------------------------------------------------
  // Wide tubes make her stiff in roll and light in pitch — the opposite of the dory's signature.
  const ROCK = { frames: 8, rollA: 2.4, pitchA: 2.6, heaveA: 1.5, period: 2.4 };
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2),
             heave: ROCK.heaveA*Math.sin(a), yaw: 0 };
  }
  // planing trim: bow climbs through the hump around t=0.45, then settles as she gets on top of it
  const PLANE = { frames: 6, humpAt: 0.45, humpPitch: 11.5, cruisePitch: 6.6, lift: 9 };
  function planeMotion(t){
    t = Math.max(0, Math.min(1, t));
    const hump = Math.sin(Math.PI*Math.min(1, t/PLANE.humpAt)) * (t<PLANE.humpAt ? 1 : Math.max(0,1-(t-PLANE.humpAt)/(1-PLANE.humpAt)));
    const pitch = PLANE.cruisePitch*t + (PLANE.humpPitch-PLANE.cruisePitch)*hump;
    return { roll: 0, pitch, heave: PLANE.lift*t*t - 3.2*t*(1-t), yaw: 0 };
  }
  // hard-over: she banks INTO the turn and the bow yaws inside the track — set-and-drift made visible
  const TURN = { maxRoll: 13.5, maxYaw: 8.5, pitch: 5.6, heave: 4 };
  function turnMotion(s){
    s = Math.max(-1, Math.min(1, s||0));
    return { roll: s*TURN.maxRoll, pitch: TURN.pitch, heave: TURN.heave, yaw: -s*TURN.maxYaw };
  }
  const posesAdd = (a,b)=>({ roll:(a.roll||0)+(b.roll||0), pitch:(a.pitch||0)+(b.pitch||0),
                             heave:(a.heave||0)+(b.heave||0), yaw:(a.yaw||0)+(b.yaw||0) });

  // ---- livery: hand-tuned ramps, dark -> light --------------------------------------------------
  const HULL = ['#471a0d','#6a2610','#903414','#b8461a','#d75e26','#ee7c3e','#fb9d63'];  // GRP topsides
  const TUBE = ['#3f180d','#5f2412','#833218','#a8441f','#c65a2b','#dd7644','#ee9464'];  // collar fabric, matte
  const BOOT = ['#12161a','#1b2126','#262e34','#333d44','#434e56'];                      // bottom below the chine
  const RUB  = ['#0d1012','#161a1d','#212830','#2f383f'];                                // D-fender + wear patch
  const SOLEG= ['#2b3237','#3a4348','#4b565b','#5d6a70','#727e83','#8a959a'];            // grey non-skid sole
  const SILV = ['#6e7e85','#93a3a8','#b8c5c8','#dbe4e4','#f2f7f5'];                      // retroreflective tape
  const PAINT= ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];  // fleet white — radome, cowls
  const ALU  = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];            // arch tube, rails
  const MOTO = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];  // fleet dark gear
  const GLAS = ['#16333c','#24505a','#3a7680','#5fa3a6','#8fc9c4'];                      // windscreen
  const BLU  = ['#0b1c4a','#123078','#1c4fb4','#3d7ee6','#7db4ff'];                      // beacon lens, blue
  const AMB  = ['#5a3103','#8f5406','#c8800f','#efb02c','#ffd870'];                      // beacon lens, amber
  const KEY  = '#101a19';

  // dith = ordered-dither weight (1 = full 4x4 Bayer, 0 = round to the nearest ramp step). Painted
  // GRP and the tube fabric are crisp; the non-skid sole keeps grain, where it reads as texture.
  const MATS = {
    hull:{ramp:HULL,off:0,dith:0},   tube:{ramp:TUBE,off:0,dith:0.15},
    tubed:{ramp:TUBE,off:-1,dith:0}, boot:{ramp:BOOT,off:0,dith:0},
    rub:{ramp:RUB,off:0,dith:0},     liner:{ramp:HULL,off:-1,dith:0},
    sole:{ramp:SOLEG,off:0,dith:0.5},silv:{ramp:SILV,off:0,dith:0},
    white:{ramp:PAINT,off:0,dith:0}, alu:{ramp:ALU,off:0,dith:0},
    moto:{ramp:MOTO,off:0,dith:0},   blk:{ramp:MOTO,off:-2,dith:0},
    glas:{ramp:GLAS,off:0,dith:0},   blu:{ramp:BLU,off:0,dith:0},
    blulit:{ramp:BLU,off:2,dith:0},  amb:{ramp:AMB,off:0,dith:0},
    amblit:{ramp:AMB,off:2,dith:0},  seat:{ramp:MOTO,off:-1,dith:0.3},
  };
  const RINDEX = {};
  [HULL,TUBE,BOOT,RUB,SOLEG,SILV,PAINT,ALU,MOTO,GLAS,BLU,AMB].forEach(r=>r.forEach((c,i)=>{ if(!RINDEX[c]) RINDEX[c]={r,i}; }));

  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- builds -----------------------------------------------------------------------------------
  // kb / kz scale the LOFT only (beam, depth); furniture is authored in absolute metres and placed
  // against build-relative landmarks, because a coxswain is the same size in either boat.
  const BUILDS = {
    hurricane: { id:'hurricane', name:'7.3 m Hurricane', L:7.00, kb:1.00, kz:1.00,
                 archZ:2.34, seats:4, engines:2, davit:false, loa:7.28,
                 note:'four jockey seats, full arch, twin outboards' },
    frc:       { id:'frc',       name:'6.5 m FRC',       L:6.40, kb:0.93, kz:0.96,
                 archZ:2.06, seats:0, engines:1, davit:true,  loa:6.66,
                 note:'bench, open aft deck for a casualty, davit lifting eye, single outboard' },
  };
  const DEFAULT_BUILD = 'hurricane';
  const buildOf = (o)=>BUILDS[(o&&o.build)||DEFAULT_BUILD] || BUILDS[DEFAULT_BUILD];

  // ---- offsets table: transom(0) -> stem(8). [sheerHalf, chineHalf, chineZ, sheerZ, keelZ] (m) ----
  // Warped deep-V: 18deg deadrise at the transom opening to 34deg forward, hard chine with a spray
  // rail all the way. Authored for the hurricane; kb/kz scale it for the FRC.
  const T = [
    [1.10, 1.06, 0.38, 0.72, 0.02],   // transom — sheerZ 0.72 IS skiffMotorRig's clamp height
    [1.14, 1.09, 0.42, 0.74, 0.00],
    [1.16, 1.10, 0.47, 0.76, 0.00],
    [1.15, 1.08, 0.53, 0.78, 0.00],
    [1.11, 1.01, 0.60, 0.81, 0.01],
    [1.03, 0.90, 0.68, 0.85, 0.05],
    [0.89, 0.73, 0.77, 0.90, 0.14],
    [0.64, 0.47, 0.88, 0.97, 0.32],
    [0.09, 0.05, 0.99, 1.06, 0.62],   // raked stem
  ];
  const lerp = (a,b,t)=>a+(b-a)*t;
  const clamp01 = (v)=>v<0?0:v>1?1:v;
  function station(u, B){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], C=T[i+1];
    return { ws:lerp(A[0],C[0],fr)*B.kb, wc:lerp(A[1],C[1],fr)*B.kb,
             zc:lerp(A[2],C[2],fr)*B.kz, zs:lerp(A[3],C[3],fr)*B.kz, kz:lerp(A[4],C[4],fr)*B.kz,
             y:(-0.5+u)*B.L };
  }
  // f: 0 = keel, 0.5 = chine, 1 = sheer — one parameter across the whole rigid section
  function skinPt(u, f, B, inset){
    const st=station(u,B), k=inset?0.972:1;
    if(f<=0.5){ const t=f/0.5; return [st.wc*t*k, st.y, st.kz+(st.zc-st.kz)*t]; }
    const t=(f-0.5)/0.5; return [(st.wc+(st.ws-st.wc)*t)*k, st.y, st.zc+(st.zs-st.zc)*t];
  }
  function hullHalf(u, z, B){
    const st=station(u,B);
    if(z<=st.zc) return st.wc*clamp01((z-st.kz)/Math.max(1e-4,st.zc-st.kz));
    return st.wc+(st.ws-st.wc)*clamp01((z-st.zc)/Math.max(1e-4,st.zs-st.zc));
  }
  const soleZ = (B)=>0.42*B.kz;
  const soleHalf = (u,B)=>Math.max(0.02, hullHalf(u, soleZ(B), B)-0.035);

  // ---- collar: the swept inflatable tube ---------------------------------------------------------
  // v = 0 at the aft cone tip, 1 at the bow tip. The path rides just outboard of the sheer, lifts
  // and converges to the stem forward, and overhangs the transom aft.
  const TUBE_R = 0.270, RING = 12;
  function tubePath(v, B){
    const u = -0.026 + v*1.032;
    const uu = clamp01(u), st = station(uu, B);
    const cvg = u>0.885 ? Math.pow((u-0.885)/0.147, 1.45) : 0;         // tubes close on the stem
    const x = (st.ws + 0.055) * (1 - Math.min(1, cvg)*0.965);
    const y = (-0.5 + u)*B.L;
    const lift = u>0.86 ? (u-0.86)*0.95 : 0;                            // the bow tube rides up
    const drop = u<0 ? u*0.22 : 0;                                      // aft cone tucks down a touch
    return [x, y, st.zs + 0.135*B.kz + lift + drop];
  }
  function tubeRad(v){
    if(v < 0.085) return TUBE_R*(0.26 + 0.74*(v/0.085));                // aft cone
    if(v < 0.735) return TUBE_R;
    return TUBE_R*(1 - 0.66*Math.pow((v-0.735)/0.265, 1.25));           // bow taper
  }
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s];
  const v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function tubeFrame(v, B){
    const e=0.004, P=tubePath(v,B);
    const Tg=v_norm(v_sub(tubePath(Math.min(1,v+e),B), tubePath(Math.max(0,v-e),B)));
    const R=v_norm(v_cross(Tg,[0,0,1])), U=v_cross(R,Tg);
    return { P, R, U };
  }
  // th: 0 = straight outboard, +PI/2 = up, -PI/2 = down
  function tubePt(v, th, B, k){
    const fr=tubeFrame(v,B), r=tubeRad(v)*(k==null?1:k);
    return [ fr.P[0]+r*(Math.cos(th)*fr.R[0]+Math.sin(th)*fr.U[0]),
             fr.P[1]+r*(Math.cos(th)*fr.R[1]+Math.sin(th)*fr.U[1]),
             fr.P[2]+r*(Math.cos(th)*fr.R[2]+Math.sin(th)*fr.U[2]) ];
  }
  // Chamber seams are a whisper, not a division. One ramp step, one facet wide: any stronger and the
  // collar reads as a string of sausages instead of one inflated tube.
  const SEAMS = [0.345, 0.605];
  function tubeMat(th, v){
    for(const s of SEAMS) if(Math.abs(v-s) < 0.006) return 'tubed';
    if(th < -0.10 && th > -0.66) return 'rub';                          // D-fender on the outer face
    if(th < -1.20 && th > -2.65) return 'rub';                          // underside wear patch
    return 'tube';
  }

  // ---- generic solids ----------------------------------------------------------------------------
  const ID=(p)=>p;
  function boxOf(c,h,mat,b,db,xf){
    xf=xf||ID;
    const P=(sx,sy,sz)=>xf([c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]]);
    const f=(v)=>({v,mat,b:b||0,db:db||0});
    return [ f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]), f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]),
             f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]), f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]),
             f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]), f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]) ];
  }
  function tubeOf(A,B2,rad,mat,b,sides){
    sides=sides||6;
    const P0=A, P1=B2, ax=v_norm(v_sub(P1,P0));
    let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax), out=[];
    const ring=(P)=>{ const o=[]; for(let k=0;k<sides;k++){ const a=2*Math.PI*k/sides;
      o.push(v_add(P, v_add(v_mul(r,rad*Math.cos(a)), v_mul(u,rad*Math.sin(a))))); } return o; };
    const r0=ring(P0), r1=ring(P1);
    for(let k=0;k<sides;k++){ const k2=(k+1)%sides; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:-0.12}); }
    return out;
  }
  // mirror a face list across the centreline; reversing the winding keeps the outward normal
  const mirrorFaces = (fs)=>fs.map(f=>Object.assign({}, f, { v: f.v.map(p=>[-p[0],p[1],p[2]]).reverse() }));

  // ---- the boat ----------------------------------------------------------------------------------
  const _cache = {};
  function assemble(B){
    const F = [], SH = [], HALF = [];                    // HALF is mirrored at the end
    const face=(v,mat,b,db)=>F.push({v,mat:mat||'hull',b:b||0,db:db||0});
    const half=(v,mat,b,db)=>HALF.push({v,mat:mat||'hull',b:b||0,db:db||0});
    const halfBox=(c,h,mat,b,db)=>{ HALF.push.apply(HALF, boxOf(c,h,mat,b,db)); };
    const boxF=(c,h,mat,b,db)=>{ F.push.apply(F, boxOf(c,h,mat,b,db)); };
    const tubeF=(A,C,r,mat,b,s)=>{ F.push.apply(F, tubeOf(A,C,r,mat,b,s)); };
    const halfTube=(A,C,r,mat,b,s)=>{ HALF.push.apply(HALF, tubeOf(A,C,r,mat,b,s)); };
    const L=B.L, SOLE=soleZ(B), NSEG=26;

    // -- rigid hull skin (starboard half; keel band is drawn full-width so the V closes) ------------
    const BANDS = [ [0,0.26,'boot',-0.15], [0.26,0.5,'boot',0.05], [0.5,0.545,'rub',0.35,0.02],
                    [0.545,0.78,'hull',0.1], [0.78,1,'hull',0.0] ];
    for(let i=0;i<NSEG;i++){
      const u0=i/NSEG, u1=(i+1)/NSEG;
      for(const [f0,f1,mat,b,db] of BANDS)
        half([skinPt(u0,f0,B), skinPt(u1,f0,B), skinPt(u1,f1,B), skinPt(u0,f1,B)],mat,b,db);
      // inner liner from the sole up to the gunwale
      const za=SOLE, zb=station(u0,B).zs;
      if(zb>za+0.02){
        const ia=[hullHalf(u0,za,B)*0.972, station(u0,B).y, za], ib=[hullHalf(u1,za,B)*0.972, station(u1,B).y, za];
        const ja=[hullHalf(u0,zb,B)*0.972, station(u0,B).y, zb], jb=[hullHalf(u1,zb,B)*0.972, station(u1,B).y, zb];
        half([jb,ja,ia,ib],'liner',-1.7);
      }
      // gunwale flat the collar sits on
      const ga=skinPt(u0,1,B), gb=skinPt(u1,1,B), ha=skinPt(u0,1,B,1), hb=skinPt(u1,1,B,1);
      half([ga,gb,hb,ha],'hull',0.35,0.02);
    }
    // keel bottom closes the V (single centre band, both sides at once)
    for(let i=0;i<NSEG;i++){
      const u0=i/NSEG, u1=(i+1)/NSEG;
      const a=skinPt(u0,0,B), b2=skinPt(u1,0,B);
      face([[-a[0]-0.012,a[1],a[2]],[-b2[0]-0.012,b2[1],b2[2]],[b2[0]+0.012,b2[1],b2[2]],[a[0]+0.012,a[1],a[2]]],'boot',-0.9);
    }
    // transom
    (function(){
      const N=6;
      for(let k=0;k<N;k++){
        const f0=k/N, f1=(k+1)/N;
        const a=skinPt(0,f0,B), b2=skinPt(0,f1,B);
        const mat = f1<=0.52 ? 'boot' : 'hull';
        face([[-a[0],a[1],a[2]],[a[0],a[1],a[2]],[b2[0],b2[1],b2[2]],[-b2[0],b2[1],b2[2]]],mat,-0.25,0.006);
      }
      const st=station(0,B);
      // motor pad: reinforced plate the outboards clamp to
      face([[-0.46,st.y-0.02,st.zs],[0.46,st.y-0.02,st.zs],[0.46,st.y-0.02,st.zs-0.30],[-0.46,st.y-0.02,st.zs-0.30]],'moto',-0.55,-0.03);
      face([[-0.46,st.y-0.02,st.zs],[0.46,st.y-0.02,st.zs],[0.46,st.y+0.10,st.zs],[-0.46,st.y+0.10,st.zs]],'moto',0.4,-0.03);
    })();

    // -- collar ------------------------------------------------------------------------------------
    const VN=38;
    for(let i=0;i<VN;i++){
      const v0=i/VN, v1=(i+1)/VN;
      for(let k=0;k<RING;k++){
        const t0=-Math.PI + 2*Math.PI*k/RING, t1=-Math.PI + 2*Math.PI*(k+1)/RING;
        const tm=(t0+t1)/2, vm=(v0+v1)/2;
        const b = Math.sin(tm)*0.82 + Math.cos(tm)*0.16;     // round the section into the ramp
        half([tubePt(v0,t0,B), tubePt(v1,t0,B), tubePt(v1,t1,B), tubePt(v0,t1,B)], tubeMat(tm,vm), b);
      }
    }
    // cone caps, fore and aft
    for(const [v,inv] of [[0,true],[1,false]]){
      const ctr=tubePath(v,B), pts=[];
      for(let k=0;k<RING;k++) pts.push(tubePt(v, -Math.PI+2*Math.PI*k/RING, B));
      for(let k=0;k<RING;k++){
        const k2=(k+1)%RING;
        half(inv?[ctr,pts[k2],pts[k]]:[ctr,pts[k],pts[k2]],'tube',inv?-0.7:0.5);
      }
    }
    // reflective tape and abstracted decal blocks on the outer face
    const patch=(v0,v1,t0,t1,mat,b)=>half([tubePt(v0,t0,B,1.012),tubePt(v1,t0,B,1.012),
                                           tubePt(v1,t1,B,1.012),tubePt(v0,t1,B,1.012)],mat,b,0.04);
    for(const v of [0.30, 0.50, 0.70]) patch(v-0.022, v+0.022, 0.10, 0.52, 'silv', 0.55);
    patch(0.355, 0.475, -0.06, 0.16, 'blk', 0.15);          // decal block — service mark, massed
    patch(0.495, 0.545, -0.06, 0.16, 'blk', 0.15);
    patch(0.815, 0.865, -0.10, 0.12, 'blk', 0.15);          // registration block near the bow
    patch(0.875, 0.905, -0.10, 0.12, 'blk', 0.15);
    // grab line: short hand loops along the top of the collar
    for(let k=0;k<6;k++){
      const v=0.18+k*0.115;
      const a=tubePt(v-0.014, 1.15, B), c=tubePt(v+0.014, 1.15, B);
      halfTube([a[0],a[1],a[2]+0.01],[c[0],c[1],c[2]+0.055], 0.016,'blk',0.3,4);
    }

    // -- sole + fittings -----------------------------------------------------------------------------
    const FWD = 0.80;                                        // fore bulkhead station
    const DSEG=18;
    for(let i=0;i<DSEG;i++){
      const u0=FWD*i/DSEG, u1=FWD*(i+1)/DSEG;
      face([[-soleHalf(u0,B),station(u0,B).y,SOLE],[soleHalf(u0,B),station(u0,B).y,SOLE],
            [soleHalf(u1,B),station(u1,B).y,SOLE],[-soleHalf(u1,B),station(u1,B).y,SOLE]],'sole',-0.3);
    }
    // fore bulkhead + raised bow locker deck
    (function(){
      const st=station(FWD,B), zt=st.zs-0.10, xb=soleHalf(FWD,B), xt=hullHalf(FWD,zt,B)*0.95;
      face([[-xt,st.y,zt],[xt,st.y,zt],[xb,st.y,SOLE],[-xb,st.y,SOLE]],'liner',-1.9);
      const FS=5;
      for(let i=0;i<FS;i++){
        const u0=FWD+0.19*i/FS, u1=FWD+0.19*(i+1)/FS;
        const w0=hullHalf(u0,station(u0,B).zs-0.10,B)*0.95, w1=hullHalf(u1,station(u1,B).zs-0.10,B)*0.95;
        face([[-w0,station(u0,B).y,station(u0,B).zs-0.10],[w0,station(u0,B).y,station(u0,B).zs-0.10],
              [w1,station(u1,B).y,station(u1,B).zs-0.10],[-w1,station(u1,B).y,station(u1,B).zs-0.10]],'hull',0.35);
      }
      face([[-xt,st.y,zt],[xt,st.y,zt],[xt,st.y+0.06,zt+0.006],[-xt,st.y+0.06,zt+0.006]],'hull',0.55,0.03);
    })();
    // towing gear: bow eye on the stem, a strong point each quarter, and the bridle ring aft
    (function(){
      const st=station(0.985,B), zb=st.kz+(st.zs-st.kz)*0.55;
      boxF([0,st.y+0.02,zb],[0.034,0.028,0.030],'alu',0.5,0.03);
      const ey=station(0.075,B), SX=soleHalf(0.075,B)*0.94;
      halfBox([SX, ey.y, ey.zs+0.02],[0.032,0.070,0.030],'alu',0.35,0.04);
      boxF([0, station(0.115,B).y, soleZ(B)+0.14],[0.048,0.048,0.14],'alu',0.35,0.02);   // towing post
      boxF([0, station(0.115,B).y, soleZ(B)+0.29],[0.062,0.026,0.020],'alu',0.7,0.03);   // its cross head
    })();
    // deck cleats and a fender-line ring aft
    for(const uy of [0.20, 0.62]) halfBox([soleHalf(uy,B)*0.98, station(uy,B).y, station(uy,B).zs-0.03],[0.026,0.075,0.026],'alu',0.25,0.04);

    // -- console -------------------------------------------------------------------------------------
    const CY = 0.115*L;                                       // console station, both builds
    (function(){
      const X0=0.34, XT=0.30, Y0=CY-0.30, Y1=CY+0.34, YT=CY+0.20, Z0=SOLE, ZTF=1.30, ZTA=1.36;
      const A=[-X0,Y0,Z0], b2=[X0,Y0,Z0], C=[X0,Y1,Z0], D=[-X0,Y1,Z0];
      const E=[-XT,Y0,ZTA], Fq=[XT,Y0,ZTA], G=[XT,YT,ZTF], Hq=[-XT,YT,ZTF];
      face([Hq,G,C,D],'hull',0.55,-0.01);                     // front slant
      face([E,Fq,G,Hq],'blk',0.85,-0.01);                     // helm panel top
      face([A,b2,Fq,E],'hull',-0.75,-0.01);                   // aft face
      face([b2,C,G,Fq],'hull',-0.15,-0.01);
      face([D,A,E,Hq],'hull',-0.15,-0.01);
      // instrument blocks let into the panel
      face([[-0.20,Y0+0.03,ZTA-0.02],[0.06,Y0+0.03,ZTA-0.02],[0.06,Y0+0.20,ZTA-0.09],[-0.20,Y0+0.20,ZTA-0.09]],'glas',0.95,-0.03);
      face([[0.10,Y0+0.03,ZTA-0.02],[0.22,Y0+0.03,ZTA-0.02],[0.22,Y0+0.17,ZTA-0.08],[0.10,Y0+0.17,ZTA-0.08]],'blk',0.4,-0.03);
      // windscreen + grab rail
      face([[-0.24,YT+0.02,1.58],[0.24,YT+0.02,1.58],[0.27,YT,ZTF],[-0.27,YT,ZTF]],'glas',0.9,-0.02);
      tubeF([-0.255,YT+0.015,1.60],[0.255,YT+0.015,1.60],0.020,'alu',0.25,6);
      // wheel: 0.32 m across, baked flat into the aft face — she steers hydraulic, no spin layer
      const WH=[0,Y0-0.045,1.02];
      tubeF([WH[0],WH[1],WH[2]],[WH[0],WH[1]-0.02,WH[2]],0.155,'blk',0.5,10);
      tubeF([WH[0],WH[1]-0.02,WH[2]],[WH[0],WH[1]-0.05,WH[2]],0.048,'moto',0.7,6);
      boxF([0,Y0-0.025,0.74],[0.032,0.032,0.24],'moto',-0.35,-0.02);   // column
      // throttle binnacle to starboard
      boxF([0.30,CY-0.08,1.22],[0.070,0.095,0.052],'blk',0.05,0.02);
      tubeF([0.30,CY-0.10,1.25],[0.30,CY-0.24,1.42],0.018,'alu',0.45,5);
      // deck-mounted VHF / plotter box on the console front
      boxF([0,CY+0.36,1.14],[0.16,0.035,0.10],'blk',0.3,0.03);
    })();

    // -- seating ---------------------------------------------------------------------------------------
    if(B.seats){
      // four jockey seats: two abreast at the helm, two abreast behind — the RHIB signature
      const rows=[CY-0.62, CY-1.42];
      for(const ry of rows) for(const sx of [1]){
        halfBox([sx*0.36, ry, SOLE+0.29],[0.165,0.235,0.29],'seat',-0.35);         // straddle pod
        halfBox([sx*0.36, ry, SOLE+0.615],[0.190,0.260,0.040],'seat',0.55);        // saddle
        halfBox([sx*0.36, ry-0.275, SOLE+0.83],[0.180,0.042,0.19],'hull',-0.25);   // orange bolster back
        halfTube([sx*0.36-0.16, ry-0.30, SOLE+1.00],[sx*0.36+0.16, ry-0.30, SOLE+1.00],0.018,'alu',0.4,5);
        halfBox([sx*0.36, ry+0.27, SOLE+0.09],[0.11,0.032,0.050],'moto',0.2);      // footrest
      }
    } else {
      // FRC: a thwart bench forward and a clear aft deck for a casualty
      boxF([0,CY-0.80,SOLE+0.24],[0.52,0.16,0.24],'seat',-0.4);
      boxF([0,CY-0.80,SOLE+0.515],[0.60,0.20,0.038],'seat',0.55);
      boxF([0,CY-1.12,SOLE+0.72],[0.58,0.035,0.16],'seat',-0.5);
    }
    // aft deck: a stowed line coil and a grab rail across the stern
    (function(){
      const st=station(0.055,B);
      tubeF([-soleHalf(0.10,B)*0.9, st.y+0.10, st.zs+0.05],[soleHalf(0.10,B)*0.9, st.y+0.10, st.zs+0.05],0.019,'alu',0.35,5);
      boxF([-0.30, station(0.16,B).y, SOLE+0.055],[0.16,0.16,0.055],'silv',-0.1,0.02);
    })();

    // -- arch, radome, whip, beacon stalk, searchlight yoke ---------------------------------------------
    const AZ = B.archZ, AY = CY-1.95, ATOPX = 0.60;
    (function(){
      const LX=soleHalf(0.30,B)*0.92, LZ=soleZ(B);
      halfTube([LX, AY-0.10, LZ],[ATOPX, AY+0.10, AZ], 0.044,'moto',0.15,6);                    // leg
      halfTube([LX*0.92, AY-0.06, LZ+0.62],[ATOPX-0.02, AY+0.62, AZ-0.30], 0.028,'moto',-0.05,5); // fore brace
      tubeF([-ATOPX, AY+0.10, AZ],[ATOPX, AY+0.10, AZ], 0.042,'moto',0.45,6);                  // top bar
      tubeF([-ATOPX, AY+0.10, AZ-0.26],[ATOPX, AY+0.10, AZ-0.26], 0.030,'moto',0.2,5);         // lower bar
      // electronics box slung under the bar
      boxF([0.0, AY+0.10, AZ-0.44],[0.24,0.10,0.11],'blk',-0.1,0.02);
      // radome: white drum with a domed cap, aft-centre on the bar
      tubeF([0.0, AY+0.10, AZ+0.06],[0.0, AY+0.10, AZ+0.21], 0.140,'white',0.85,10);
      tubeF([0.0, AY+0.10, AZ+0.21],[0.0, AY+0.10, AZ+0.245], 0.108,'white',1.3,10);
      // whip antenna to port, and its spring base
      boxF([-ATOPX+0.06, AY+0.10, AZ+0.05],[0.030,0.030,0.048],'blk',0.2,0.02);
      tubeF([-ATOPX+0.06, AY+0.10, AZ+0.09],[-ATOPX+0.10, AY+0.06, AZ+1.16], 0.017,'alu',0.7,4);
      // beacon stalk — the LENS ITSELF is here (dark); renderBeacon lights it
      boxF([ATOPX-0.10, AY+0.10, AZ+0.06],[0.040,0.040,0.048],'blk',0.15,0.02);
      // searchlight YOKE only; the drum is renderLight's layer so it can be aimed
      halfBox([0.0, AY+0.44, AZ-0.10],[0.016,0.034,0.090],'moto',0.3,0.02);
      boxF([0.0, AY+0.44, AZ-0.21],[0.060,0.060,0.034],'moto',0.1,0.02);
      if(B.davit){
        // FRC lifting frame: a single hook eye over the centre of gravity
        tubeF([-0.30, CY-0.55, AZ-0.02],[0.30, CY-0.55, AZ-0.02],0.032,'moto',0.5,6);
        halfTube([ATOPX-0.06, AY+0.10, AZ-0.06],[0.30, CY-0.55, AZ-0.02],0.028,'moto',0.2,5);
        boxF([0, CY-0.55, AZ-0.12],[0.040,0.040,0.060],'silv',0.8,0.03);
      }
      // contact shadow of the arch + console mass on the sole
      SH.push({ steps:2, v:[[-0.52,AY-0.10,SOLE+0.002],[0.34,AY-0.10,SOLE+0.002],
                            [0.34,CY+0.30,SOLE+0.002],[-0.52,CY+0.30,SOLE+0.002]] });
    })();

    F.push.apply(F, HALF);
    F.push.apply(F, mirrorFaces(HALF));
    _lit(F, B);
    return { F, SH, B };
  }
  // unlit lens sits with the hull so she is complete with no overlay drawn
  function _lit(F, B){
    const g=lampGeo(B);
    F.push.apply(F, tubeOf(g.beacon, [g.beacon[0], g.beacon[1], g.beacon[2]+0.085], 0.046,'blu',-0.2,8));
  }
  function lampGeo(B){
    const AZ=B.archZ, AY=0.115*B.L-1.95, ATOPX=0.60;
    return { beacon:[ATOPX-0.10, AY+0.10, AZ+0.105], light:[0, AY+0.44, AZ-0.02], AY, AZ, ATOPX };
  }
  function facesOf(B){
    if(!_cache[B.id]) _cache[B.id]=assemble(B);
    return _cache[B.id];
  }

  // ---- rasterizer (fleet recipe; yaw is the addition) ---------------------------------------------
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG, yaw=(opts.yaw||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch),
      cw:Math.cos(yaw), sw:Math.sin(yaw), heave:(opts.heave||0) };
  }
  function projVert(x,y,z,B2){
    const xw=x*B2.cw - y*B2.sw, yw=x*B2.sw + y*B2.cw;       // yaw about the boat's own z, first
    const x1=xw*B2.cr+z*B2.sr, z1=-xw*B2.sr+z*B2.cr;
    const y2=yw*B2.cq - z1*B2.sq, z2=yw*B2.sq + z1*B2.cq;
    const xr=x1*B2.ct - y2*B2.stt, yr=x1*B2.stt + y2*B2.ct, zr=z2;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B2.se+zr*B2.ce)*S - B2.heave, d:(yr*B2.ce-zr*B2.se) };
  }
  function _paint(faces, opts, shadows){
    const Bs=camBasis(opts);
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    const tag=new Int8Array(W*H);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,Bs));
      const n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, Bs.se, Bs.ce);
      if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], Bs.se, Bs.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.hull;
      const base=Math.floor(fidx), frac=Math.max(0,Math.min(1,fidx-base));
      const dith=M.dith==null?1:M.dith;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1]);
      function fillTri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) return;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d; tag[i]=f.tag||0;
            const thr = dith<=0 ? 0.5 : 0.5+(BAYER[x&3][y&3]-0.5)*dith;
            const idx=base+(frac>=thr?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    const out=col.slice();
    if(opts.onlyTag){ for(let i=0;i<W*H;i++) if(tag[i]!==opts.onlyTag){ out[i]=null; col[i]=null; } }
    for(const sq of (shadows||[])){
      const rv=sq.v.map(([x,y,z])=>projVert(x,y,z,Bs)), steps=sq.steps||1;
      for(let t=1;t+1<rv.length;t++){
        const a=rv[0], b=rv[t], c=rv[t+1];
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) continue;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const i=y*W+x; if(!out[i]) continue;
          const sd=w0*a.d+w1*b.d+w2*c.d;
          if(dep[i] < sd-0.06) continue;
          const e=RINDEX[out[i]]; if(e && e.i>0) out[i]=e.r[Math.max(0,e.i-steps)];
        }
      }
    }
    if(opts.outline===true || (opts.outline==null && KEYLINE_DEFAULT)){
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){
        const i=y*W+x; if(out[i]) continue;
        for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
          const nx=x+dx, ny=y+dy;
          if(nx>=0&&nx<W&&ny>=0&&ny<H&&col[ny*W+nx]){ out[i]=KEY; break; }
        }
      }
    }
    return out;
  }
  function _toRGBA(out){
    const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){
      const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16);
      rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255;
    }
    return rgba;
  }
  function render(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    const A=facesOf(buildOf(opts));
    return _toRGBA(_paint(A.F, Object.assign({}, opts, {dir}), A.SH));
  }

  // ---- beacon: a tagged layer so the flash is a frame index, not a re-bake -------------------------
  const BEACON = { frames:6, pattern:[1,0,1,0,0,0], colours:['blue','amber'] };
  const beaconFrame=(i)=>({ on: !!BEACON.pattern[((i|0)%BEACON.frames+BEACON.frames)%BEACON.frames] });
  function beaconFaces(B, colour){
    const g=lampGeo(B), lit=colour==='amber'?'amblit':'blulit', dim=colour==='amber'?'amb':'blu';
    const P=g.beacon, out=[];
    const push=(fs,mat,b)=>fs.forEach(f=>out.push(Object.assign({},f,{mat,b:b==null?f.b:b,db:0.42,tag:2})));
    push(tubeOf(P,[P[0],P[1],P[2]+0.085],0.046,lit,0.9), lit, 0.9);
    push(tubeOf([P[0],P[1],P[2]+0.085],[P[0],P[1],P[2]+0.105],0.030,lit,1.2), lit, 1.2);
    // halo: a small camera-facing puff one ramp step down, so the flash blooms 2 px instead of 0
    push(tubeOf([P[0],P[1],P[2]+0.04],[P[0],P[1],P[2]+0.05],0.082,dim,0.6), lit, 0.35);
    return out;
  }
  function renderBeacon(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    const B=buildOf(opts), A=facesOf(B);
    if(opts.on===false) return _toRGBA(new Array(W*H).fill(null));
    if(opts.frame!=null && !beaconFrame(opts.frame).on) return _toRGBA(new Array(W*H).fill(null));
    return _toRGBA(_paint(A.F.concat(beaconFaces(B, opts.colour||'blue')),
                          Object.assign({}, opts, {dir, onlyTag:2})));
  }

  // ---- searchlight: the drum is a layer so it can be aimed --------------------------------------
  const LIGHT = { panMax:170, tiltMin:-12, tiltMax:38, r:0.105, len:0.20 };
  function lightFaces(B, opts){
    const g=lampGeo(B), P=g.light;
    const pan=Math.max(-LIGHT.panMax,Math.min(LIGHT.panMax,opts.pan||0))*DEG;
    const tilt=Math.max(LIGHT.tiltMin,Math.min(LIGHT.tiltMax,opts.tilt==null?4:opts.tilt))*DEG;
    const ax=[Math.sin(pan)*Math.cos(tilt), Math.cos(pan)*Math.cos(tilt), Math.sin(tilt)];
    const back=[P[0]-ax[0]*LIGHT.len*0.45, P[1]-ax[1]*LIGHT.len*0.45, P[2]-ax[2]*LIGHT.len*0.45];
    const front=[P[0]+ax[0]*LIGHT.len*0.55, P[1]+ax[1]*LIGHT.len*0.55, P[2]+ax[2]*LIGHT.len*0.55];
    const on = opts.on!==false;
    const out=[];
    tubeOf(back,front,LIGHT.r,'white',0.2,10).forEach(f=>out.push(Object.assign({},f,{db:0.40,tag:3})));
    // lens face
    const fr=[]; for(let k=0;k<10;k++){
      const a=2*Math.PI*k/10;
      let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
      const r1=v_norm(v_cross(ax,up)), u1=v_cross(r1,ax);
      fr.push(v_add(front, v_add(v_mul(r1,LIGHT.r*0.92*Math.cos(a)), v_mul(u1,LIGHT.r*0.92*Math.sin(a)))));
    }
    for(let k=1;k+1<10;k++) out.push({v:[fr[0],fr[k],fr[k+1]], mat:on?'silv':'glas', b:on?1.4:0.2, db:0.46, tag:3});
    return out;
  }
  function renderLight(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    const B=buildOf(opts), A=facesOf(B);
    return _toRGBA(_paint(A.F.concat(lightFaces(B, opts)), Object.assign({}, opts, {dir, onlyTag:3})));
  }

  // ---- anchors (hull-cell px) ---------------------------------------------------------------------
  function _at(P, dir, opts){
    const o=Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const p=projVert(P.x,P.y,P.z,camBasis(o));
    return { x:p.sx, y:p.sy };
  }
  const MOUNT=(B)=>({ x:0, y:-B.L/2-0.07, z:station(0,B).zs });
  const HELM =(B)=>({ x:0.34, y:0.115*B.L-0.62, z:soleZ(B)+0.69 });
  const PILOT=(B)=>({ x:0.34, y:0.115*B.L-0.30, z:soleZ(B) });
  const PAINTER=(B)=>({ x:0, y:station(0.985,B).y+0.02, z:station(0.985,B).kz+(station(0.985,B).zs-station(0.985,B).kz)*0.55 });
  const STERN_EYE=(B)=>({ x:0, y:-B.L/2+0.04, z:station(0,B).zs-0.14 });
  const BRIDLE=(B)=>({ x:0, y:station(0.115,B).y, z:soleZ(B)+0.30 });
  function seatsOf(B){
    if(!B.seats) return [{x:0, y:0.115*B.L-0.80, z:soleZ(B)+0.55}];
    const out=[];
    for(const ry of [0.115*B.L-0.62, 0.115*B.L-1.42]) for(const sx of [0.34,-0.34]) out.push({x:sx,y:ry,z:soleZ(B)+0.69});
    return out;
  }
  const mk=(fn)=>function(dir,opts){ return _at(fn(buildOf(typeof opts==='object'?opts:{})), dir, opts); };
  const motorMount=mk(MOUNT), helmSeat=mk(HELM), pilotStand=mk(PILOT),
        painter=mk(PAINTER), sternEye=mk(STERN_EYE), towBridle=mk(BRIDLE);
  const beaconHead=function(dir,opts){ const B=buildOf(typeof opts==='object'?opts:{}); const g=lampGeo(B);
    return _at({x:g.beacon[0],y:g.beacon[1],z:g.beacon[2]+0.05}, dir, opts); };
  const lightHead=function(dir,opts){ const B=buildOf(typeof opts==='object'?opts:{}); const g=lampGeo(B);
    return _at({x:g.light[0],y:g.light[1],z:g.light[2]}, dir, opts); };
  function jockeySeats(dir, opts){
    const B=buildOf(typeof opts==='object'?opts:{});
    return seatsOf(B).map(p=>_at(p, dir, opts));
  }
  // collar contact points: where fenders, boarding hands and rafting FX belong
  function collarPoints(dir, opts){
    const B=buildOf(typeof opts==='object'?opts:{}), out=[];
    for(const v of [0.20,0.40,0.60,0.80]) for(const s of [1,-1]){
      const p=tubePt(v, 1.05, B);
      out.push(_at({x:s*p[0], y:p[1], z:p[2]}, dir, opts));
    }
    return out;
  }

  // ---- skiffMotorRig registration ------------------------------------------------------------------
  // SkiffMotor is lofted for the 7.0 m skiff: swivel axis y=-3.57, clamp z=0.72, cell 272x216 pivot
  // (136,120). The 'hurricane' loft puts her clamp on exactly that point, so the shift is the two
  // cells' pivot difference and nothing else. The shorter 'frc' clamp sits forward, and the shift is
  // an exact per-heading screen offset because the bake is orthographic.
  const SKIFF_MOTOR = { axisY:-3.57, clampZ:0.72, cell:{W:272,H:216}, pivot:{x:136,y:120},
                        variant:'guard', mounts:{ single:[0], twin:[-0.34,0.34] } };
  function motorOffset(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=buildOf(opts), Bs=camBasis(opts), M=MOUNT(B);
    const a=projVert(0, SKIFF_MOTOR.axisY, SKIFF_MOTOR.clampZ, Bs);
    const b=projVert(0, M.y, M.z, Bs);
    return { dx: Math.round(b.sx-a.sx) + (cx-SKIFF_MOTOR.pivot.x),
             dy: Math.round(b.sy-a.sy) + (cy-SKIFF_MOTOR.pivot.y) };
  }
  const enginesOf=(o)=>{ const B=buildOf(o); return B.engines===2 ? SKIFF_MOTOR.mounts.twin : SKIFF_MOTOR.mounts.single; };

  root.ZodiacIso = {
    W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    BUILDS, buildIds:Object.keys(BUILDS), defaultBuild:DEFAULT_BUILD,
    render, renderBeacon, renderLight,
    ROCK, rock:rockMotion, PLANE, plane:planeMotion, TURN, turn:turnMotion, addPose:posesAdd,
    BEACON, beaconFrame, LIGHT,
    motorMount, helmSeat, pilotStand, painter, sternEye, towBridle,
    beaconHead, lightHead, jockeySeats, collarPoints,
    MOUNT, HELM, PILOT, PAINTER, STERN_EYE, BRIDLE, seatsOf,
    SKIFF_MOTOR, motorOffset, engines:enginesOf,
    station, soleZ, soleHalf, tubePath, tubeRad, TUBE_R, KEYLINE_DEFAULT,
    HULL, TUBE, BOOT, RUB, SOLEG, SILV, PAINT, ALU, MOTO, GLAS, BLU, AMB, KEY,
  };
})(typeof globalThis!=='undefined'?globalThis:window);

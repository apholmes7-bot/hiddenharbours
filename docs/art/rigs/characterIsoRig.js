/* Hidden Harbours — parametric ISO CHARACTER rig (M2 bake recipe, ADR-0006 — same pipeline as
   the boat fleet: puntIsoRig.js et al). One 3D mannequin, posed by a small skeleton (FK spine +
   2-bone IK limbs), baked to pixel sheets: fixed 3/4 turntable camera (elev 40deg default,
   matches the fleet so characters sit true on decks), 45deg steps, flat-facet shading from the
   fixed upper-left key, z-buffered, ordered dither, 1px keyline post-pass, NO AA. 32 px = 1 m.

   Cell 64x88, pivot (32,80) = ground contact under the character origin, pinned every heading
   and every frame. Proportions keyed to the reference boards: chunky ~4.5-head build, big hair
   mass, face read by 1-2 px eye pixels + fringe shadow (blink baked into the idle loop).

   BUILD AXES (the character-creator surface — all resolved per render, no re-modelling):
     skin: 'fair'|'tan'|'deep' | custom ramp      eyes: 'sea'|'sky'|'bark'|'slate'|'amber' | hex
     hair: 'blond'|'black'|'brown'|'ginger'|'grey' | custom ramp
     hairStyle: 'crop'|'mop'|'bob'|'bun'|'bald'
     outfit: 'teal'|'navy'|'oil'|'rust' | custom ramp   (overalls; shirt stays fleet white)
     height: 0.92..1.08   weight: 0.88..1.15
   ANIMS: idle (6f, weight-shift + breathe + blink), walk (8f), run (6f) — one gait function,
   amplitudes per anim, so motion stays consistent across all 8 directions by construction.
   TOOL ANIMS (rod kit rodIsoRig.js, shovel kit shovelIsoRig.js): hold (6f fishing stance),
   dig (10f raise/thrust/pry/toss, spoil FX launches ~f8) + cast (10f windup/snap/settle;
   opts.power 'short'|'long' scales the swing). tool(dir,opts) -> {grip px, wrist 3D, pitch, yaw,
   bend} = the rod mount, per frame; projectLocal(dir,p,elev) -> cell px for runtime line FX.
   Anchors baked from day one (the motor-mount pattern): anchors(dir,opts) -> {handL,handR,
   head,hip} cell px for rod / held-item / hat overlay layers.
   CARRY MODIFIER (bucket kit bucketRig.js): opts.carry 'buckets'|'tray' overrides the arm
   targets of idle/walk/run (hands hang at the sides / both hands forward on the tray rim);
   carry(dir,opts) -> hand px + pendulum swing (rad) + behind flags = the bucket mount.
   Exposes globalThis.CharacterIso = { W,H,PX,DIRS,pivot,order,ANIMS,BUILDS,SKINS,HAIRS,
   OUTFITS,EYES,SHIRT,BOOT,HAIRSTYLES,KEY,defaultElev, render(dir,opts), anchors(dir,opts) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 64, H = 88, cx = 32, cy = 80;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;

  // ---- palettes, dark->light (KTC master ramps shared with the fleet) ----
  const SKINS = {
    fair: ['#6b4028','#8a5636','#a8724a','#c98d63','#e0a981','#f0c9a2'],
    tan:  ['#59331f','#754628','#936036','#b07d4a','#cc9a63','#e2b57e'],
    deep: ['#301c12','#45291a','#5e3a24','#7a4f31','#966843','#b08055'],
  };
  const HAIRS = {
    blond: ['#7a5a1c','#946218','#b07d1f','#e0b13a','#f2cf6a'],
    black: ['#0e1114','#171b21','#232a32','#333c46','#4a545f'],
    brown: ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48'],
    ginger:['#5e2013','#7d301c','#9c4327','#b85835','#d07048'],
    grey:  ['#6f7a78','#878b85','#a2a7a0','#bcc2ba','#cfd4cc'],
  };
  const OUTFITS = {
    teal: ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'],   // the fisher's overalls
    navy: ['#0e1526','#172644','#223764','#2f4c88','#4166ac'],
    oil:  ['#946218','#b07d1f','#cf9d24','#e0b13a','#f2cf6a'],   // oilskin yellow
    rust: ['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c'],
  };
  const SHIRTS = {
    white: ['#878b85','#a2a7a0','#bcc2ba','#cfd4cc','#dfe3dc','#eef0ea'],
    cream: ['#8c6a45','#a98352','#c2a06b','#d8c290','#ead9ae','#f4e8c8'],
    navy:  ['#0e1526','#172644','#223764','#2f4c88','#4166ac','#5a80c2'],
    red:   ['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c','#ee7a55'],
    moss:  ['#27312c','#333f37','#414e43','#505e50','#616f60','#748172'],
  };
  const SHIRT = SHIRTS.white;
  const BOOT  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63'];
  const EYES  = { sea:'#2ba39a', sky:'#4166ac', bark:'#6b4f35', slate:'#8a969b', amber:'#e0b13a' };
  const KEY   = '#101a19';
  const HAIRSTYLES = ['crop','mop','bob','bun','bald'];
  const BUILDS = {
    fisher:  { skin:'fair', hair:'blond', hairStyle:'mop',  eyes:'sea',   outfit:'teal', shirt:'white', height:1.00, weight:1.00 },
    skipper: { skin:'tan',  hair:'grey',  hairStyle:'crop', eyes:'slate', outfit:'navy', shirt:'cream', height:1.04, weight:1.12 },
    ginny:   { skin:'fair', hair:'ginger',hairStyle:'bob',  eyes:'amber', outfit:'rust', shirt:'moss',  height:0.97, weight:0.94 },
  };
  const ANIMS = { idle:{frames:6, ms:170}, walk:{frames:8, ms:110}, run:{frames:6, ms:80},
                  hold:{frames:6, ms:170}, cast:{frames:10, ms:70}, dig:{frames:10, ms:90} };

  function rampOf(map, key, fb){ return Array.isArray(key) ? key : (map[key] || map[fb]); }
  function makeMats(b){
    const skin = rampOf(SKINS, b.skin, 'fair'), hair = rampOf(HAIRS, b.hair, 'blond'),
          over = rampOf(OUTFITS, b.outfit, 'teal'), shirt = rampOf(SHIRTS, b.shirt, 'white');
    const eye = EYES[b.eyes] || b.eyes || EYES.sea;
    const MATS = { skin:{ramp:skin,off:0}, hair:{ramp:hair,off:0}, over:{ramp:over,off:0},
                   shirt:{ramp:shirt,off:0}, boot:{ramp:BOOT,off:0}, eye:{ramp:[eye],off:0},
                   lash:{ramp:BOOT,off:-1},
                   lip:{ramp:skin,off:-2}, sole:{ramp:BOOT,off:-2} };
    const RINDEX = {}; [skin,hair,over,shirt,BOOT].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
    return { MATS, RINDEX };
  }

  // ---- shading constants (fleet recipe) ----
  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.13;   // tighter depth-edge than the hulls so limbs separate from the torso
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- solids ----
  const ID=(p)=>p;
  const TX=(dx,dy,dz)=>(p)=>[p[0]+dx,p[1]+dy,p[2]+dz];
  const rotZ=(a)=>{ const c=Math.cos(a), s=Math.sin(a); return (p)=>[p[0]*c-p[1]*s, p[0]*s+p[1]*c, p[2]]; };
  const pitchX=(a,z0)=>{ const c=Math.cos(a), s=Math.sin(a); return (p)=>[p[0], p[1]*c+(p[2]-z0)*s, z0-p[1]*s+(p[2]-z0)*c]; };
  const chain=(...fs)=>(p)=>{ for(let i=fs.length-1;i>=0;i--) p=fs[i](p); return p; };
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{ const m=Math.hypot(a[0],a[1],a[2])||1; return [a[0]/m,a[1]/m,a[2]/m]; };
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function box(c,h,mat,b,db,xf){
    xf=xf||ID;
    const P=(sx,sy,sz)=>xf([c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]]);
    const f=(v)=>({v,mat,b:b||0,db:db||0});
    return [
      f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]),
      f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]),
      f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]),
      f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]),
      f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]),
      f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]),
    ];
  }
  function tube(A,B2,rad,mat,b){   // capped square-section limb segment
    const P0=A, P1=B2;
    const ax=v_norm(v_sub(P1,P0)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P)=>[ v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)), v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                      v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)), v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad)) ];
    const r0=ring(P0), r1=ring(P1), out=[];
    for(let k=0;k<4;k++){ const k2=(k+1)%4; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:-0.05}); }
    out.push({v:r1.slice(),mat,b:b||0,db:-0.05});
    out.push({v:r0.slice().reverse(),mat,b:b||0,db:-0.05});
    return out;
  }
  // 2-bone IK in the sagittal (y,z) plane. sign +1 bends toward +y (knee), -1 toward -y (elbow).
  function ik2(Sy,Sz,Ty,Tz,l1,l2,sign){
    let dy=Ty-Sy, dz=Tz-Sz, d=Math.hypot(dy,dz)||1e-3;
    const dc=Math.min(d, l1+l2-0.005);
    const a=(l1*l1-l2*l2+dc*dc)/(2*dc), h=Math.sqrt(Math.max(0,l1*l1-a*a));
    const uy=dy/d, uz=dz/d, ny=-uz, nz=uy;
    return [Sy+uy*a+ny*h*sign, Sz+uz*a+nz*h*sign];
  }

  // ---- tool (rod) pose curves: hold = fishing stance loop; cast = windup / snap / settle ----
  // power 'short'|'long' scales the windup and the snap. Angles deg; wr/wl = wrist targets (y,z).
  const lerp=(a,b2,t)=>a+(b2-a)*t, sm=(t)=>t*t*(3-2*t);
  function toolCurve(anim, u, power){
    const long = power==='long';
    if(anim==='hold') return { pitch:56+3*Math.sin(2*Math.PI*u), yaw:16, wrY:0.150, wrZ:0.60,
      wlY:0.085, wlZ:0.50, lean:0, twist:6, dip:0, bend:0.05+0.02*Math.sin(2*Math.PI*u) };
    if(anim==='dig'){
      // shovel: right hand low on the shaft (= grip pivot), left hand rides 0.26 m up toward the
      // D-grip — wl is derived from wr + pitch so both hands stay ON the shaft at every frame.
      const dg=(o)=>{ const cp=Math.cos(o.pitch*DEG), sp=Math.sin(o.pitch*DEG);
        o.wlY=o.wrY-0.26*cp; o.wlZ=o.wrZ-0.26*sp; o.dig=true; return o; };
      const r1=0.28, t1=0.48, p1=0.70;
      if(u<r1){ const t=sm(u/r1);                       // raise: lift the blade clear
        return dg({ pitch:lerp(-34,-8,t), yaw:8, wrY:lerp(0.185,0.150,t), wrZ:lerp(0.56,0.74,t),
                    lean:lerp(6,-5,t), twist:lerp(3,-7,t), dip:0.010*(1-t), bend:0 }); }
      if(u<t1){ const t=Math.pow((u-r1)/(t1-r1),1.8);   // thrust: drive the blade into the ground
        return dg({ pitch:lerp(-8,-60,t), yaw:8, wrY:lerp(0.150,0.265,t), wrZ:lerp(0.74,0.470,t),
                    lean:lerp(-5,16,t), twist:lerp(-7,4,t), dip:0.055*t, bend:0 }); }
      if(u<p1){ const t=sm((u-t1)/(p1-t1));             // pry: lever back, loading the blade
        return dg({ pitch:lerp(-60,-38,t), yaw:8, wrY:lerp(0.265,0.205,t), wrZ:lerp(0.470,0.435,t),
                    lean:lerp(16,4,t), twist:lerp(4,6,t), dip:lerp(0.055,0.065,t), bend:0 }); }
      const t=sm((u-p1)/(1-p1)), toss=Math.sin(Math.PI*Math.min(1,t*1.35));   // heave + toss aside, settle to loop
      return dg({ pitch:lerp(-38,-34,t)+26*toss, yaw:8, wrY:lerp(0.205,0.185,t)-0.03*toss,
                  wrZ:lerp(0.435,0.56,t)+0.10*toss, lean:lerp(4,6,t)-6*toss, twist:lerp(6,3,t)-22*toss,
                  dip:lerp(0.065,0.010,t), bend:0 });
    }
    const PB=long?150:128, PF=long?-6:10, LB=long?-8:-5, LF=long?12:7, SB=long?0.78:0.55;
    const w1=0.34, s1=0.50;
    if(u<w1){ const t=sm(u/w1);   // windup: rod back over the shoulder, lean back, twist away
      return { pitch:lerp(56,PB,t), yaw:16, wrY:lerp(0.150,-0.075,t), wrZ:lerp(0.60,0.94,t),
               wlY:lerp(0.085,0.02,t), wlZ:lerp(0.50,0.60,t), lean:lerp(0,LB,t), twist:lerp(6,-11,t), dip:0, bend:0.10*t };
    }
    if(u<s1){ const t=(u-w1)/(s1-w1), tt=Math.pow(t,2.1);   // snap: accelerating whip forward
      return { pitch:lerp(PB,PF,tt), yaw:16, wrY:lerp(-0.075,0.255,tt), wrZ:lerp(0.94,0.60,tt),
               wlY:lerp(0.02,0.13,tt), wlZ:lerp(0.60,0.50,tt), lean:lerp(LB,LF,tt), twist:lerp(-11,9,tt), dip:0.020*t, bend:lerp(0.10,SB,tt) };
    }
    const t=sm((u-s1)/(1-s1));    // settle: ease back to the hold stance (loops seamlessly)
    return { pitch:lerp(PF,56,t), yaw:16, wrY:lerp(0.255,0.150,t), wrZ:0.60,
             wlY:lerp(0.13,0.085,t), wlZ:0.50, lean:lerp(LF,0,t), twist:lerp(9,6,t), dip:0.020*(1-t), bend:lerp(SB,0.05,t) };
  }

  // ---- gait / pose: one parametric cycle, amplitudes per anim ----
  function pose(anim, u, b, power){
    const hS=b.height||1, wS=b.weight||1;
    let stride=0, lift=0, bob=0, lean=0, arm=0, yaw=0, handZ=0.63;
    if(anim==='walk'){ stride=0.16; lift=0.09; bob=0.030; lean=4*DEG;  arm=0.13; yaw=8*DEG;  handZ=0.65; }
    if(anim==='run') { stride=0.24; lift=0.16; bob=0.055; lean=11*DEG; arm=0.18; yaw=12*DEG; handZ=0.78; }
    const tc = (anim==='hold'||anim==='cast'||anim==='dig') ? toolCurve(anim,u,power) : null;
    const carry = (!tc && (arguments.length>4)) ? arguments[4] : null;
    const tw=Math.sin(2*Math.PI*u);
    const idle = anim==='idle', calm = idle || anim==='hold';
    if(tc) lean = tc.lean*DEG;
    if(carry==='tray') lean = -5*DEG;                    // counterweight lean-back under the tray
    if(carry==='buckets') lean = lean*0.5;               // loaded shoulders damp the gait lean
    const breathe = calm ? 0.018*Math.sin(2*Math.PI*u) : 0;
    const swayX = tc ? (calm?0.012*tw:0) : (idle ? 0.012*tw : 0.010*tw);
    const dip = tc ? tc.dip : (idle ? 0 : bob*(0.5+0.5*Math.cos(4*Math.PI*u)));
    const hipZ = 0.575*hS - dip;   // chunky ~3.7-head build (reference boards): short legs, long torso, big head
    const yawS = tc ? tc.twist*DEG : yaw*tw, yawH = tc ? tc.twist*0.45*DEG : -0.6*yaw*tw;
    const leanP = pitchX(lean, hipZ);
    const P = { anim,u,hS,wS, hipZ, swayX, lean, breathe, yawS, yawH, leanP };
    // legs — feet pinned to the treadmill loop; 2-bone IK, knee forward
    P.legs = {};
    const thigh=0.28*hS, shin=0.25*hS;
    for(const [side, ph] of [['L',0],['R',0.5]]){
      const sgn = side==='L' ? -1 : 1;
      const p2=(u+ph)%1;
      const hip0 = rotZ(yawH)([sgn*0.07*wS, 0, 0]); hip0[0]+=swayX*0.5; hip0[2]=hipZ;
      let yF, zF;
      if(tc){ yF = sgn<0 ? (tc.dig?0.105:0.075) : (tc.dig?-0.085:-0.055); zF = 0.06*hS; }   // staggered stance, left forward (wider for dig)
      else if(idle){ yF = sgn*0.012; zF = 0.06*hS; }
      else { yF = stride*Math.cos(2*Math.PI*p2); zF = 0.06*hS + lift*Math.max(0,-Math.sin(2*Math.PI*p2)); }
      const [ky,kz]=ik2(hip0[1],hip0[2], yF, zF, thigh, shin, +1);
      P.legs[side]={ hip:hip0, knee:[hip0[0]*0.97,ky,kz], ankle:[hip0[0]*0.94, yF, zF] };
    }
    // arms — counter-phase to the legs; 2-bone IK, elbow back
    P.arms = {};
    const upA=0.23*hS, foA=0.21*hS, shZ=(1.06+breathe)*hS, sw=0.15*wS;
    for(const [side, ph] of [['L',0.5],['R',0]]){
      const sgn = side==='L' ? -1 : 1;
      const p2=(u+ph)%1;
      const sh = P.leanP(rotZ(yawS)([sgn*sw, 0, shZ])); sh[0]+=swayX*0.5;
      let ty, tz, tx=sh[0]+sgn*0.012;
      if(tc){ ty = side==='R' ? tc.wrY : tc.wlY; tz = (side==='R' ? tc.wrZ : tc.wlZ)*hS + breathe*0.5; }
      else if(carry==='buckets'){ tx = sh[0]+sgn*0.048; ty = 0.012 + 0.30*arm*Math.cos(2*Math.PI*p2); tz = 0.60*hS + breathe*0.5; }
      else if(carry==='tray'){ tx = sgn*0.24; ty = 0.150 + (idle ? 0.006*Math.sin(2*Math.PI*u) : 0.012*Math.cos(4*Math.PI*u)); tz = 0.615*hS + breathe*0.5; }
      else if(idle){ ty = 0.015 + 0.008*Math.sin(2*Math.PI*u+(sgn>0?0.6:0)); tz = 0.63*hS+breathe*0.5; }
      else { ty = sh[1] + arm*Math.cos(2*Math.PI*p2); tz = handZ*hS; }
      const [ey,ez]=ik2(sh[1],sh[2], ty, tz, upA, foA, -1);
      P.arms[side]={ sh, elbow:[sh[0]+sgn*0.01,ey,ez], wrist:[tx,ty,tz] };
    }
    P.neckB = P.leanP([swayX*0.5, 0, 1.05*hS]);
    const headLean = pitchX(lean*0.55, hipZ);
    P.headC = headLean([swayX*0.5, 0.005, 1.24*hS + breathe]);
    P.eyesClosed = calm && u>0.60 && u<0.78;   // baked blink
    P.tool = tc ? { pitch:tc.pitch*DEG, yaw:tc.yaw*DEG, bend:tc.bend } : null;
    return P;
  }

  // ---- mannequin assembly (mat names resolved per build in makeMats) ----
  function facesOf(P, b){
    const wS=P.wS, hS=P.hS, F=[];
    const add=(fs)=>{ for(const f of fs) F.push(f); };
    const torsoXf = chain(TX(P.swayX*0.5,0,0), P.leanP, rotZ(P.yawS*0.6));
    // legs + boots
    for(const side of ['L','R']){
      const g=P.legs[side];
      add(tube(g.hip, g.knee, 0.058*wS, 'over', -0.1));
      add(tube(g.knee, g.ankle, 0.050*wS, 'over', -0.35));
      const a=g.ankle;
      add(box([a[0], a[1]+0.028, a[2]-0.015], [0.058*wS, 0.090*wS, 0.045], 'boot', -0.2, 0));
    }
    // pelvis + torso + overalls bib & straps
    // overalls: bib high on the chest, wide straps, hip buttons, chest pocket — the garment must READ
    add(box([P.swayX*0.5, 0, P.hipZ+0.028], [0.12*wS, 0.09*wS, 0.062], 'over', -0.15, 0, rotZ(P.yawH)));
    add(box([0,0,0.855*hS], [0.125*wS, 0.095*wS, 0.175*hS], 'shirt', 0, 0, torsoXf));
    add(box([0, 0.088*wS, 0.90*hS], [0.092*wS, 0.018, 0.125*hS], 'over', 0.15, -0.02, torsoXf));      // bib to mid-chest
    add(box([0, 0.104*wS, 0.945*hS], [0.048*wS, 0.008, 0.045*hS], 'over', -0.55, -0.03, torsoXf));    // chest pocket (dark step)
    for(const sgn of [-1,1]){
      add(box([sgn*0.062*wS, 0.012, 1.015*hS], [0.028, 0.105*wS, 0.020], 'over', 0.15, -0.02, torsoXf)); // wide straps
      add(box([sgn*0.086*wS, 0.106*wS, 1.005*hS], [0.014, 0.007, 0.014], 'boot', 0.9, -0.04, torsoXf));  // strap buckle
      add(box([sgn*0.128*wS, 0.055*wS, (P.hipZ+0.075)], [0.006, 0.016, 0.016], 'boot', 0.9, -0.04));     // hip button
    }
    // waistband: overalls wrap the lower torso so trousers+bib join up
    add(box([0, 0, (P.hipZ+0.105)], [0.128*wS, 0.098*wS, 0.035], 'over', -0.1, -0.01, torsoXf));
    // arms + hands (rolled sleeves: shirt upper, skin forearm)
    for(const side of ['L','R']){
      const a=P.arms[side];
      add(tube(a.sh, a.elbow, 0.048*wS, 'shirt', -0.1));
      add(tube(a.elbow, a.wrist, 0.042*wS, 'skin', -0.15));
      const d=v_norm(v_sub(a.wrist,a.elbow));
      const hc=v_add(a.wrist, v_mul(d,0.035));
      add(box(hc, [0.034*wS,0.036*wS,0.038], 'skin', -0.1, -0.03));
    }
    // neck + head
    // head: wider skull over a narrower chamfered jaw (less blocky), hair tight to the crown
    add(tube(P.neckB, [P.headC[0],P.headC[1]-0.005,P.headC[2]-0.10], 0.040*wS, 'skin', -0.5));
    const hx=0.14*(1+(wS-1)*0.4), hd=0.125, hz=0.15;
    const hc=P.headC;
    add(box([hc[0],hc[1],hc[2]+0.035], [hx,hd,hz-0.035], 'skin', 0.1, 0));                       // skull
    add(box([hc[0],hc[1]+0.006,hc[2]-0.105], [hx-0.028,hd-0.018,0.045], 'skin', 0.05, 0));       // jaw, narrower + shallower
    // face — proud decals; eyes 2px tall with a dark lash line so they read at 32px/m
    const fy=hc[1]+hd+0.004, fyj=hc[1]+0.006+hd-0.018+0.004;
    const q=(x0,z0,hw,hh,mat,bb,yy)=>F.push({v:[[x0-hw,yy||fy,z0+hh],[x0+hw,yy||fy,z0+hh],[x0+hw,yy||fy,z0-hh],[x0-hw,yy||fy,z0-hh]],mat,b:bb,db:0.06});   // positive db biases the decal IN FRONT (deff=d-db)
    if(P.eyesClosed){
      q(-0.058*wS, hc[2]-0.006, 0.026, 0.009, 'lash', 0.2); q(0.058*wS, hc[2]-0.006, 0.026, 0.009, 'lash', 0.2);
    } else {
      q(-0.058*wS, hc[2]+0.022, 0.026, 0.012, 'lash', 0.3); q(0.058*wS, hc[2]+0.022, 0.026, 0.012, 'lash', 0.3);   // lash/brow line
      q(-0.058*wS, hc[2]-0.012, 0.026, 0.026, 'eye', 0.6);  q(0.058*wS, hc[2]-0.012, 0.026, 0.026, 'eye', 0.6);    // iris, 2px
    }
    q(0, hc[2]-0.118, 0.030, 0.011, 'lip', 0.3, fyj);
    // hair mass by style
    const st=b.hairStyle||'crop';
    if(st!=='bald'){
      add(box([hc[0],hc[1]-0.008,hc[2]+0.118], [hx+0.012,hd+0.004,0.052], 'hair', 0.35, -0.01));            // crown cap, tighter
      add(box([hc[0],hc[1]-hd-0.006,hc[2]+0.02], [hx+0.006,0.026, st==='bob'?0.15:0.115], 'hair', -0.2, -0.01)); // back
      if(st==='mop'||st==='bob'){
        add(box([hc[0],hc[1]+hd-0.014,hc[2]+0.112], [hx+0.008,0.030,0.034], 'hair', 0.3, -0.03));           // fringe above the brow line
        for(const sgn of [-1,1])
          add(box([hc[0]+sgn*(hx+0.004),hc[1]-0.018,hc[2]+(st==='bob'?-0.005:0.05)], [0.022,hd-0.014, st==='bob'?0.14:0.078], 'hair', -0.15, -0.01));
      }
      if(st==='bun') add(box([hc[0],hc[1]-hd-0.042,hc[2]+0.11], [0.05,0.045,0.05], 'hair', 0.1, -0.01));
    }
    return F;
  }

  // ---- rasterizer (fleet recipe, per-build materials) ----
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }
  function camBasis(opts){
    // ADR-0006 fix: azimuth turns CW to match the label order N,NE,E,SE,S,SW,W,NW.
    // (was +dir*PI/4 = CCW, which mirrored every cell left<->right, swapping E<->W.)
    const dir=opts.dir||0, th=-dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) };
  }
  function projVert(x,y,z,B){
    const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) };
  }
  function _paint(faces, opts, MATS, RINDEX){
    const B=camBasis(opts);
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.shirt;
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
          let w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          let w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          let w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d;
            let base=Math.floor(fidx);
            let idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    const out=new Array(W*H).fill(null);
    for(let i=0;i<W*H;i++) out[i]=col[i];
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){          // depth-edge darkening (limb / torso separation)
      const i=y*W+x; if(!col[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!col[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){
          const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
          if(e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)];
        }
      }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){          // 1px keyline
      const i=y*W+x; if(out[i]) continue;
      let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&col[ny*W+nx]){ touch=true; break; }
      }
      if(touch) out[i]=KEY;
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

  function resolveOpts(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    const b = Object.assign({}, BUILDS.fisher, opts.build||{});
    const anim = opts.anim||'idle';
    const A = ANIMS[anim]||ANIMS.idle;
    const u = opts.u!=null ? opts.u : (((opts.frame||0)%A.frames+A.frames)%A.frames)/A.frames;
    const power = opts.power==='long' ? 'long' : 'short';
    const carry = (opts.carry==='buckets'||opts.carry==='tray') ? opts.carry : null;
    return { o:Object.assign({},opts,{dir}), b, anim, u, power, carry };
  }
  function render(dir, opts){
    const {o,b,anim,u,power,carry}=resolveOpts(dir,opts);
    const {MATS,RINDEX}=makeMats(b);
    return _toRGBA(_paint(facesOf(pose(anim,u,b,power,carry), b), o, MATS, RINDEX));
  }
  // hand / head / hip anchors in cell px — the motor-mount pattern, for rod & held-item layers
  function anchors(dir, opts){
    const {o,b,anim,u,power,carry}=resolveOpts(dir,opts);
    const P=pose(anim,u,b,power,carry), B=camBasis(o);
    const pt=(p)=>{ const v=projVert(p[0],p[1],p[2],B); return {x:v.sx, y:v.sy}; };
    return { handL:pt(P.arms.L.wrist), handR:pt(P.arms.R.wrist),
             head:pt([P.headC[0],P.headC[1],P.headC[2]+0.17]), hip:pt([P.swayX*0.5,0,P.hipZ]) };
  }
  // the rod mount, per frame — grip in cell px + rod orientation (radians) + blank flex 0..1.
  // wrist = the 3D character-local wrist point (bobber launch origin for runtime FX).
  function tool(dir, opts){
    const {o,b,anim,u,power}=resolveOpts(dir,opts);
    const P=pose(anim,u,b,power);
    if(!P.tool) return null;
    const B=camBasis(o), w=P.arms.R.wrist;
    const v=projVert(w[0]+0.005, w[1]+0.010, w[2]+0.012, B);
    return { grip:{x:v.sx, y:v.sy}, wrist:[w[0],w[1],w[2]], pitch:P.tool.pitch, yaw:P.tool.yaw, bend:P.tool.bend };
  }
  // the bucket / tray mount, per frame — hand px + pendulum swing (radians) + layer flags.
  // buckets: swingL/swingR rotate each bucket about its grip in the fore-aft plane (pass to
  // BucketIso.render as opts.swing); behindL/behindR = blit that bucket UNDER the sprite.
  // tray: mid = near-rim-centre pin between the two hands; swing = small carry bob.
  function carry(dir, opts){
    const {o,b,anim,u,power,carry:c}=resolveOpts(dir,Object.assign({carry:'buckets'},opts));
    const P=pose(anim,u,b,power,c), B=camBasis(o);
    const pj=(p)=>projVert(p[0],p[1],p[2],B);
    const wl=P.arms.L.wrist, wr=P.arms.R.wrist, vl=pj(wl), vr=pj(wr);
    const A=(anim==='run'?15:anim==='walk'?9:1.6)*DEG, lag=0.9;
    const swingR=A*Math.sin(2*Math.PI*u-lag), swingL=A*Math.sin(2*Math.PI*(u+0.5)-lag);
    if(c==='tray'){
      const mid=pj([(wl[0]+wr[0])/2,(wl[1]+wr[1])/2,(wl[2]+wr[2])/2]);
      return { mode:'tray', handL:{x:vl.sx,y:vl.sy}, handR:{x:vr.sx,y:vr.sy}, mid:{x:mid.sx,y:mid.sy},
               swing:(anim==='idle'?0.8*Math.sin(2*Math.PI*u):2.0*Math.cos(4*Math.PI*u))*DEG,
               behind:B.ct>0.05 };
    }
    const beh=(wx)=>(wx*B.stt + 0.012*B.ct) > 0.001;
    return { mode:'buckets', handL:{x:vl.sx,y:vl.sy}, handR:{x:vr.sx,y:vr.sy},
             swingL, swingR, behindL:beh(wl[0]), behindR:beh(wr[0]), behind:false };
  }
  // character-local 3D point -> cell px (may exceed the cell — offset from pivot for line FX)
  function projectLocal(dir, p, elev){
    const v=projVert(p[0],p[1],p[2],camBasis({dir, elev}));
    return { x:v.sx, y:v.sy };
  }

  root.CharacterIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    ANIMS, BUILDS, SKINS, HAIRS, OUTFITS, EYES, SHIRT, SHIRTS, BOOT, HAIRSTYLES, KEY,
    render, anchors, tool, carry, projectLocal };
})(typeof globalThis!=='undefined'?globalThis:window);

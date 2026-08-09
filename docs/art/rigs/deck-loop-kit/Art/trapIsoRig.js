/* Hidden Harbours — TRAP ISO RIG. Four builds of pot, on the fleet turntable.
   Same fixed 3/4 view as the hulls and the insulated tote (45deg steps CW, elev 40,
   32 px = 1 m, dither, no AA) via Art/isoSolid.js. RINGLESS by default (ADR 0031);
   {keyline:true} is kept as a live A/B.

     wood      traditional half-round bow-top — oak bows, lengthwise laths, tarred head
     wire      rectangular vinyl-coated wire lobster trap — kitchen + parlour, oak runners
     crabWire  the smaller, finer-meshed rock crab pot — twin entry rings, ballast bricks
     crabCone  the conical rope-wrapped crab pot — 11 rope courses, mesh cone, top ring

   Cell 72x60, pivot (36,46) = ground under the centre. Every build renders in the SAME
   cell with the same pivot, so a stack is one sprite blitted N times up the z step and
   the game can swap builds in place without a pixel of drift.

   HOLLOW, like the tote: the shell paints the frame only. slots(build,dir) hands back
   projected floor points (2 layers, back-to-front, monotonic), opening(build,dir) the
   interior silhouette to clip to, and renderFront(build,dir) the camera-facing mesh
   ALONE — draw shell, blit TrapCatch items, then draw the front pass and the catch is
   genuinely behind the wire.

   Stacking is data: STACK.step_m per build, and CAPS what each surface will take —
   Cape Islander 3 high, lobster boat 5 high, a washboard 2 (the working trap and one
   waiting), the wharf 6.

   Exposes globalThis.TrapIso = { W,H,pivot,BUILDS,LABELS,DIMS,COLOURS,CAPS,STACK,
     render(build,dir,opts), renderFront(build,dir,opts), slots(build,dir),
     opening(build,dir), stackStep(build,elev), sheet(build,opts) }. */
(function (root) {
  const K = root.IsoSolid;
  const W = 72, H = 60, cx = 36, cy = 46, S = 32;
  const KEY = '#0f1614';

  const R = {
    OAK  :['#241a11','#3a2a1c','#63482f','#8c6a45','#b9975f'],
    FRAME:['#161009','#241a11','#3a2a1a','#5a4028','#7d5c3b'],
    VINYL:['#12181a','#1d262a','#2c383d','#405056','#596c73'],
    GALV :['#2c3539','#49555a','#71807f','#a6b6b8','#c6d2d3'],
    MANIL:['#5a4326','#7a6242','#a98f66','#cdbe97','#e3d8b8'],
    POLY :['#6a3a18','#8e5320','#b8722d','#d9963f','#eeb85c'],
    NET  :['#141210','#1b1813','#2b271e','#474134','#6c6656'],
    VOID :['#070b0c','#0c1113','#121a1c','#1a2427','#243033'],
    WEED :['#1a2311','#26301a','#39471d','#5c6e2c','#8a9a45'],
    BRICK:['#2e2e2a','#414139','#57574e','#6f6f66','#8a8a80'],
  };
  const BUILDS = ['wood','wire','crabWire','crabCone'];
  const LABELS = { wood:'WOODEN BOW-TOP', wire:'WIRE LOBSTER', crabWire:'WIRE CRAB POT', crabCone:'CONICAL CRAB POT' };
  const COLOURS = {
    wood:['oak','tarred'], wire:['black','galv'], crabWire:['black','galv'], crabCone:['manila','poly'],
  };
  const CLABEL = { oak:'BARE OAK', tarred:'TARRED', black:'BLACK VINYL', galv:'GALVANISED', manila:'MANILA', poly:'ORANGE POLY' };

  // metric footprints — length(x) × beam(y) × height(z), and the interior the catch lies in
  const DIMS = {
    wood:     { l:1.06, w:0.56, h:0.44, inner:{ hx:0.46, hy:0.20, z:0.075 } },
    wire:     { l:1.22, w:0.54, h:0.40, inner:{ hx:0.54, hy:0.20, z:0.075 } },
    crabWire: { l:0.80, w:0.52, h:0.31, inner:{ hx:0.33, hy:0.19, z:0.065 } },
    crabCone: { l:0.96, w:0.96, h:1.18, inner:{ hx:0.16, hy:0.16, z:1.00, round:true } },
  };
  // stack step = how far the next pot up sits, plus the lateral slew of a real stack
  const STACK = {
    wood:    { step_m:0.40, slew:[0,-1,1,-1,0,1], label:'bows nest a little — 0.40 m a tier' },
    wire:    { step_m:0.38, slew:[0,1,-1,1,0,-1], label:'square tiers — 0.38 m, runners on rims' },
    crabWire:{ step_m:0.30, slew:[0,-1,1,0,-1,1], label:'small pots — 0.30 m a tier' },
    crabCone:{ step_m:0.52, slew:[0,0,0,0,0,0],   label:'cones NEST — 0.52 m, not 1.18' },
  };
  // what each surface takes (owner call, this pass)
  const CAPS = {
    lobsterBoat:{ deck:5, washboard:2, label:'LOBSTER BOAT' },
    capeIslander:{ deck:3, washboard:2, label:'CAPE ISLANDER' },
    wharf:{ deck:6, washboard:0, label:'WHARF' },
  };

  const MATS_FOR=(build,colour,wet)=>{
    const wireRamp = colour==='galv' ? R.GALV : R.VINYL;
    const ropeRamp = colour==='poly' ? R.POLY : R.MANIL;
    const oakRamp  = colour==='tarred' ? R.FRAME : R.OAK;
    return {
      OAK:{ramp:oakRamp,off:wet?-1:0}, FRAME:{ramp:R.FRAME,off:wet?-1:0},
      WIRE:{ramp:wireRamp,off:wet?-1:0}, ROPE:{ramp:ropeRamp,off:wet?-1:0},
      NET:{ramp:R.NET,off:0}, VOID:{ramp:R.VOID,off:0},
      WEED:{ramp:R.WEED,off:0}, BRICK:{ramp:R.BRICK,off:wet?-1:0},
    };
  };

  // ---- builders -------------------------------------------------------------
  const push=(A,B2)=>{ for(const f of B2) A.push(f); return A; };

  function meshPanel(F, axis, at, span, hz, z0, step, thick, mat, tag){
    // a flat grid of bars in the plane {axis}=at. span = half-extent along the other
    // horizontal axis; hz = height. Verticals every `step`, horizontals every `step`.
    const n=Math.max(1,Math.round(span*2/step));
    for (let i=0;i<=n;i++){
      const u=-span+ (i*span*2/n);
      const p0 = axis==='x' ? [at,u,z0] : [u,at,z0];
      const p1 = axis==='x' ? [at,u,z0+hz] : [u,at,z0+hz];
      push(F, K.bar(p0,p1,thick,mat,0.1,0,tag));
    }
    const m=Math.max(1,Math.round(hz/step));
    for (let j=0;j<=m;j++){
      const z=z0 + j*hz/m;
      const p0 = axis==='x' ? [at,-span,z] : [-span,at,z];
      const p1 = axis==='x' ? [at, span,z] : [ span,at,z];
      push(F, K.bar(p0,p1,thick,mat,-0.15,0.004,tag));
    }
  }
  function meshTop(F, hx, hy, z, step, thick, mat, tag){
    const n=Math.max(1,Math.round(hx*2/step));
    for (let i=0;i<=n;i++){ const x=-hx+i*hx*2/n; push(F,K.bar([x,-hy,z],[x,hy,z],thick,mat,0.25,0,tag)); }
    const m=Math.max(1,Math.round(hy*2/step));
    for (let j=0;j<=m;j++){ const y=-hy+j*hy*2/m; push(F,K.bar([-hx,y,z],[hx,y,z],thick,mat,0.05,0.004,tag)); }
  }
  function ring(F, c, r, seg, thick, mat, plane, tag, b){
    for (let i=0;i<seg;i++){
      const a0=(i/seg)*Math.PI*2, a1=((i+1)/seg)*Math.PI*2;
      const p=(a)=> plane==='xz' ? [c[0]+Math.cos(a)*r, c[1], c[2]+Math.sin(a)*r]
                  : plane==='yz' ? [c[0], c[1]+Math.cos(a)*r, c[2]+Math.sin(a)*r]
                                 : [c[0]+Math.cos(a)*r, c[1]+Math.sin(a)*r, c[2]];
      push(F, K.bar(p(a0),p(a1),thick,mat,b!=null?b:0.2,0.006,tag));
    }
  }

  function buildWire(o){
    const D=DIMS[o.build], F=[];
    const hx=D.l/2, hy=D.w/2, hz=D.h, crab=o.build==='crabWire';
    const step=crab?0.105:0.135, th=crab?0.0075:0.0085;
    const z0=0.055;
    // oak runners under the base — what a stacked pot lands on
    for (const y of [-hy+0.06, hy-0.06]) push(F, K.box([0,y,0.027],[hx*0.97,0.045,0.027],'OAK',0.15,0,null,'base'));
    // mesh floor over a dark bed, so the catch reads against something
    meshTop(F, hx*0.99, hy*0.99, z0, step, th, 'WIRE', 'base');
    push(F, K.box([0,0,z0-0.014],[hx*0.95,hy*0.95,0.007],'VOID',-1.1,0.02,null,'base'));
    meshPanel(F,'y', hy, hx, hz-z0, z0, step, th, 'WIRE','shell');
    meshPanel(F,'y',-hy, hx, hz-z0, z0, step, th, 'WIRE','shell');
    meshPanel(F,'x', hx, hy, hz-z0, z0, step, th, 'WIRE','shell');
    meshPanel(F,'x',-hx, hy, hz-z0, z0, step, th, 'WIRE','shell');
    meshTop(F, hx, hy, hz, step, th, 'WIRE','shell');
    // heavier frame at every edge
    const fr=th*2.1;
    for (const z of [z0, hz]) for (const y of [-hy,hy]) push(F, K.bar([-hx,y,z],[hx,y,z],fr,'WIRE',0.3,0.008,'shell'));
    for (const z of [z0, hz]) for (const x of [-hx,hx]) push(F, K.bar([x,-hy,z],[x,hy,z],fr,'WIRE',0.3,0.008,'shell'));
    for (const x of [-hx,hx]) for (const y of [-hy,hy]) push(F, K.bar([x,y,z0],[x,y,hz],fr,'WIRE',0.35,0.008,'shell'));
    // kitchen | parlour partition + its tunnel head
    const px0 = crab ? 0.0 : 0.14;
    if (!crab){
      meshPanel(F,'x', px0, hy*0.98, hz-z0-0.02, z0, step*1.15, th, 'WIRE','shell');
      ring(F,[px0,0,z0+0.15],0.075,10,0.011,'NET','yz','base',0.4);
    }
    // entrance heads — netting funnels on the long side (crab pot gets two rings)
    const heads = crab ? [[-0.22,-hy],[0.22,-hy]] : [[-hx+0.13,-hy]];
    for (const [ex,ey] of heads){
      ring(F,[ex,ey,z0+0.115],crab?0.085:0.105,12,0.013,'NET','xz','shell',0.5);
      // the funnel proper, driven inboard
      for (let i=0;i<10;i++){
        const a=(i/10)*Math.PI*2;
        const r=crab?0.082:0.10;
        push(F, K.bar([ex+Math.cos(a)*r, ey, z0+0.115+Math.sin(a)*r],
                      [ex+Math.cos(a)*r*0.35, ey+(ey<0?0.19:-0.19), z0+0.10+Math.sin(a)*r*0.35],
                      0.010,'NET',-0.3,0.004,'base'));
      }
    }
    // becket rope coiled on the lid
    ring(F,[hx*0.52,0.02,hz+0.022],0.075,12,0.015,'ROPE','xy','shell',0.5);
    ring(F,[hx*0.52,0.02,hz+0.048],0.062,12,0.015,'ROPE','xy','shell',0.6);
    return F;
  }

  function buildWood(o){
    const D=DIMS.wood, F=[];
    const hx=D.l/2, hy=D.w/2, base=0.052, arcH=D.h-base, ry=hy-0.02;
    // sill runners + bottom laths
    for (const y of [-hy+0.05, hy-0.05]) push(F, K.box([0,y,0.026],[hx,0.05,0.026],'FRAME',0,0,null,'base'));
    for (const y of [-0.20,-0.10,0,0.10,0.20]) push(F, K.box([0,y,base-0.011],[hx*0.98,0.036,0.011],'OAK',0.05,0,null,'base'));
    push(F, K.box([0,0,base-0.026],[hx*0.94,hy*0.86,0.006],'VOID',-1.1,0.02,null,'base'));
    for (const x of [-0.30,0.30]) push(F, K.box([x,0,base+0.026],[0.07,hy*0.55,0.020],'BRICK',-0.2,0,null,'base'));
    // bows (oak hoops) — half ellipses in the y-z plane
    const bowAt=(x,thick,mat,b)=>{
      const n=9;
      for (let i=0;i<n;i++){
        const t0=(i/n)*Math.PI, t1=((i+1)/n)*Math.PI;
        const p=(t)=>[x, Math.cos(t)*ry, base+Math.sin(t)*arcH];
        push(F, K.bar(p(t0),p(t1),thick,mat,b,0.006,'shell'));
      }
    };
    for (const x of [-hx+0.03, -0.36, 0.0, 0.36, hx-0.03]) bowAt(x, 0.019, 'OAK', 0.3);
    // lengthwise laths following the bow profile
    for (const t of [0.055,0.175,0.30,0.42,0.50,0.58,0.70,0.825,0.945]){
      const a=t*Math.PI, y=Math.cos(a)*ry, z=base+Math.sin(a)*arcH;
      const lift = Math.abs(t-0.5)<0.09 ? 0.35 : 0.05;
      push(F, K.bar([-hx,y,z],[hx,y,z],[0.026,0.012],'OAK',lift,0.004,'shell'));
    }
    // end frames — heavier timber posts, tarred corner blocks
    for (const sx of [-1,1]){
      push(F, K.box([sx*(hx-0.012),0,base+arcH*0.30],[0.014,ry*0.98,0.016],'FRAME',0.15,0.006,null,'shell'));
      for (const sy of [-1,1]) push(F, K.box([sx*(hx-0.03), sy*(hy-0.04), base+0.02],[0.032,0.036,0.038],'FRAME',-0.3,0,null,'base'));
    }
    // tarred head — the net funnel in the side, and the parlour head inboard
    ring(F,[-0.30,-hy+0.02,base+0.14],0.115,12,0.013,'NET','xz','shell',0.5);
    for (let i=0;i<10;i++){ const a=(i/10)*Math.PI*2;
      push(F, K.bar([-0.30+Math.cos(a)*0.115, -hy+0.02, base+0.14+Math.sin(a)*0.115],
                    [-0.30+Math.cos(a)*0.04, -hy+0.24, base+0.12+Math.sin(a)*0.04],
                    0.010,'NET',-0.3,0.004,'base')); }
    ring(F,[0.26,0,base+0.15],0.09,10,0.012,'NET','yz','base',0.35);
    // bridle rope arcing over the bows to a becket
    for (let i=0;i<8;i++){
      const u0=-0.42+i*0.105, u1=u0+0.105;
      const zf=(u)=>base+arcH+0.028-Math.pow(u/0.5,2)*0.05;
      push(F, K.bar([u0,0.02,zf(u0)],[u1,0.02,zf(u1)],0.015,'ROPE',0.55,0.012,'shell'));
    }
    ring(F,[0.50,0.02,base+arcH+0.05],0.05,10,0.013,'ROPE','xy','shell',0.6);
    return F;
  }

  function buildCone(o){
    const F=[], seg=18, rB=0.48, rS=0.435, rT=0.15, hTot=1.10, shoulder=0.575;
    const lay=(i)=>((i%2)?0.42:-0.32);
    const lay2=(i)=>((i%2)?0.18:-0.46);
    // mesh floor — the pot sits flat on the deck
    push(F, K.tube(0,0,0.010,0.026,rB*0.95,rB*0.95,seg,'NET',{caps:'top',b:-0.9,capB:-1.2,tag:'base'}));
    // rope courses up the barrel
    let z=0.026;
    for (let c=0;c<10;c++){
      const t=c/9, r0=rB-(rB-rS)*t, r1=rB-(rB-rS)*Math.min(1,t+0.11);
      push(F, K.tube(0,0,z,z+0.044, r0, r1, seg,'ROPE',{ biasOf:lay, tag:'shell' }));
      z+=0.056;
    }
    ring(F,[0,0,shoulder],rS*1.01,seg,0.028,'ROPE','xy','shell',0.6);
    // rope-wrapped cone up to the entry ring — steeper than the camera, so it reads
    // as a cone with a small dark mouth, not a dish
    for (let c=0;c<6;c++){
      const t=c/6, t2=(c+1)/6;
      push(F, K.tube(0,0, shoulder+0.012+t*0.50, shoulder+0.012+t2*0.50 - 0.010,
        rS+(rT-rS)*t, rS+(rT-rS)*t2, seg, 'ROPE',
        { b:-0.15, biasOf:lay2, tag:'shell' }));
    }
    ring(F,[0,0,hTot-0.015],rT,seg,0.030,'ROPE','xy','shell',0.7);
    // vertical stiffeners + the bridle to a becket
    for (let i=0;i<3;i++){
      const a=(i/3)*Math.PI*2+0.39;
      push(F, K.bar([Math.cos(a)*rB*0.99, Math.sin(a)*rB*0.99, 0.03],
                    [Math.cos(a)*rT*1.03, Math.sin(a)*rT*1.03, hTot-0.03],0.013,'ROPE',0.85,0.012,'shell'));
    }
    for (let i=0;i<3;i++){
      const a=(i/3)*Math.PI*2;
      push(F, K.bar([Math.cos(a)*rT, Math.sin(a)*rT, hTot-0.015],[0,0,hTot+0.11],0.014,'ROPE',0.55,0.014,'shell'));
    }
    ring(F,[0,0,hTot+0.125],0.048,10,0.015,'ROPE','xy','shell',0.7);
    return F;
  }

  function weedOn(F, build){
    const D=DIMS[build];
    const drapes = build==='crabCone'
      ? [[0.30,0.26,0.72],[-0.36,0.16,0.60],[0.08,-0.42,0.55]]
      : [[-0.22,-D.w/2+0.02,D.h],[0.18,D.w/2-0.03,D.h],[0.40,-0.06,D.h]];
    drapes.forEach((d,i)=>{
      let [x,y,z]=d;
      for (let k=0;k<5;k++){
        const nx=x+(k%2?0.035:-0.03), ny=y+(y<0?-0.028:0.028), nz=z-0.055;
        push(F, K.bar([x,y,z],[nx,ny,nz],0.017-k*0.001,'WEED',k%2?0.5:0.05,0.014,'shell'));
        x=nx; y=ny; z=nz;
      }
    });
  }

  const FCACHE={};
  function facesOf(build, wet){
    const k=build+'|'+(wet?1:0);
    if (FCACHE[k]) return FCACHE[k];
    let F;
    if (build==='wood') F=buildWood({build});
    else if (build==='crabCone') F=buildCone({build});
    else F=buildWire({build});
    if (wet) weedOn(F, build);
    return (FCACHE[k]=F);
  }

  const camFacing=(n,B)=> (n[1]*B.ce - n[2]*B.se) < -0.04;

  function _render(build, dir, opts, front){
    opts=opts||{};
    const b=BUILDS.indexOf(build)>=0?build:'wire';
    const colour=opts.colour || COLOURS[b][0];
    const M=MATS_FOR(b,colour,!!opts.wet);
    return K.paint(facesOf(b,!!opts.wet), {
      W,H,cx,cy, dir, elev:opts.elev, MATS:M, key:KEY,
      keyline: opts.keyline!=null?opts.keyline:false,
      emit: front ? ((f,n,B)=> f.tag==='shell' && camFacing(n,B)) : null,
    });
  }
  function render(build, dir, opts){ return _render(build,dir,opts,false); }
  function renderFront(build, dir, opts){ return _render(build,dir,opts,true); }

  // ---- fill geometry --------------------------------------------------------
  // two layers of floor points (three heaped in the cone's mouth, which is the only
  // place a rope-wrapped pot shows what it caught).
  function slots(build, dir, opts){
    opts=opts||{};
    const D=DIMS[BUILDS.indexOf(build)>=0?build:'wire'];
    const B=K.camBasis({dir, elev:opts.elev});
    const rng=K.mulberry(53), pts=[];
    const layers = D.inner.round?3:2;
    for (let L=0; L<layers; L++){
      const lz=D.inner.z + L*(D.inner.round?0.085:0.105);
      const lp=[];
      if (D.inner.round){
        const rows=[[0,1],[0.62,4],[1.0,6]][L] || [0.5,6];
        for (let i=0;i<rows[1];i++){
          const a=(i/rows[1])*Math.PI*2 + L*0.7;
          const r=D.inner.hx*rows[0];
          const v=K.proj(Math.cos(a)*r+(rng()-0.5)*0.04, Math.sin(a)*r+(rng()-0.5)*0.04, lz, B, cx, cy);
          lp.push({ dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy), sy:v.sy });
        }
      } else {
        const nx = D.inner.hx>0.4?5:3;
        for (let gy=-1;gy<=1;gy+=2) for (let i=0;i<nx;i++){
          const gx=-D.inner.hx*0.78 + (i/(nx-1))*D.inner.hx*1.56;
          const v=K.proj(gx+(rng()-0.5)*0.07 + (L?0.06:0), gy*D.inner.hy*0.48+(rng()-0.5)*0.06 + (L?0.04:0), lz, B, cx, cy);
          lp.push({ dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy), sy:v.sy });
        }
      }
      lp.sort((a,b2)=>a.sy-b2.sy);
      pts.push.apply(pts, lp);
    }
    return pts;
  }
  // interior silhouette (convex hull of the projected inner volume) — the clip region
  function opening(build, dir, opts){
    opts=opts||{};
    const D=DIMS[BUILDS.indexOf(build)>=0?build:'wire'];
    const B=K.camBasis({dir, elev:opts.elev});
    const zTop=D.inner.z + (D.inner.round?0.34:0.24);
    const P=[];
    if (D.inner.round){
      for (let i=0;i<12;i++){ const a=(i/12)*Math.PI*2;
        for (const z of [D.inner.z-0.10, zTop]) P.push(K.proj(Math.cos(a)*D.inner.hx*1.95, Math.sin(a)*D.inner.hy*1.95, z, B, cx, cy)); }
    } else {
      for (const sx of [-1,1]) for (const sy of [-1,1]) for (const z of [D.inner.z-0.02, zTop])
        P.push(K.proj(sx*D.inner.hx, sy*D.inner.hy, z, B, cx, cy));
    }
    const pts=P.map(v=>[v.sx, v.sy]);
    pts.sort((a,b2)=>a[0]-b2[0]||a[1]-b2[1]);
    const cross=(o,a,b2)=>(a[0]-o[0])*(b2[1]-o[1])-(a[1]-o[1])*(b2[0]-o[0]);
    const lo=[],hi=[];
    for (const p of pts){ while(lo.length>=2&&cross(lo[lo.length-2],lo[lo.length-1],p)<=0)lo.pop(); lo.push(p); }
    for (let i=pts.length-1;i>=0;i--){ const p=pts[i];
      while(hi.length>=2&&cross(hi[hi.length-2],hi[hi.length-1],p)<=0)hi.pop(); hi.push(p); }
    lo.pop(); hi.pop();
    return lo.concat(hi).map(p=>({ dx:Math.round(p[0]-cx), dy:Math.round(p[1]-cy) }));
  }
  // px between one pot and the one stacked on it, at the rig's elevation
  function stackStep(build, elev){
    const e=((elev!=null?elev:K.DEFAULT_ELEV))*K.DEG;
    return Math.round((STACK[build]||STACK.wire).step_m * S * Math.cos(e));
  }
  // 8-dir strip for the bake
  function sheet(build, opts){
    const out=[]; for (let d=0;d<8;d++) out.push(render(build,d,opts));
    return out;
  }

  root.TrapIso = { W,H, pivot:{x:cx,y:cy}, KEY, BUILDS, LABELS, DIMS, COLOURS, CLABEL,
    CAPS, STACK, RAMPS:R, defaultElev:K.DEFAULT_ELEV,
    render, renderFront, slots, opening, stackStep, sheet };
})(typeof globalThis!=='undefined'?globalThis:window);

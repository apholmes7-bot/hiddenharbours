/* Hidden Harbours — characterIsoRig9 POSE LIBRARY (pass 8's clips, retargeted per build). Every clip the game ships, authored from scratch against
   the pass-8 skeleton. A pose is an INTENT: pelvis, four spine angles, ankle targets (world or pelvis frame), palm
   targets (chest frame, or world read through the unrocked chest), a face, a tool attitude, contract pins. The
   solver in characterIsoRig8.js turns it into bone locals; nothing here touches a bone.

   Laws kept from the shipped rigs:
     · frames and ms are rig 6's, to the frame (the ANIMS table lives in the core and is gated).
     · world contracts are world metres and clamp to the figure's reach, reporting what they did.
     · the rod's attitude tables (pitch / bend per frame) are rig 6's, so RodIso's bake and release timing hold.
     · one-shots that hand off are BUILT from the clip they hand to (idle at the dock / deck, astride frame 0),
       never re-typed, so the seams are exact and the goldens assert it.
   New:
     · walk and run plant their stance foot at constant ground speed (0.727 m/s, 2.0 m/s — rig 6's averages), so
       an engine moving the figure at that speed shows no skate.
     · oars_row is a braced standing stroke (two per loop); the legs no longer walk while rowing.
   Pass 9: body-relative targets go through K.bp (a hand point on the Fisher -> the same place on this build) and
   K.ft (a foot placement); strides, lifts and reaches scale by K.k. World contracts are untouched. */
(function (root) {
  'use strict';
  const C8 = root.CharacterIso9;
  if(!C8){ console.warn('characterIsoRig9.poses.js: load Art/characterIsoRig9.js first'); return; }
  C8.definePoses(function (L) {
    const { DEG, TAU, vadd, vsub, vmul, vlen, vnorm, vlerp, vcross, clamp01, lerp, sm, seg, frac, sn, cs, bez, keys, ftab, hangC, baseIntent, bodyOf, blendIntent, fkp, mV, mTV } = L;
    const G=(s)=>s==='L'?-1:1;
    const face=(I,e,b,m)=>{ I.face={ eyes:e||'open', brows:b||'flat', mouth:m||'flat' }; };
    const breath=(I,u,k)=>{ k=k==null?1:k; I.chest.pitch=(I.chest.pitch||0)-1.1*k*sn(u); I.pelvis.p[2]+=0.002*k*sn(u); I.head.pitch=(I.head.pitch||0)+0.6*k*sn(u,0.1); };
    const cp=(o)=>JSON.parse(JSON.stringify(o));
    function copyIntent(I,J){ for(const k of ['pelvis','spine','chest','neck','head','legs','arms','face']) I[k]=cp(J[k]);
      for(const k of ['pelvis','spine','chest','neck','head']) for(const a of ['yaw','pitch','roll']) if(typeof I[k][a]!=='number') I[k][a]=0; }
    function footPivot(p,pitch,h,fk){ if(!pitch) return p.slice(); fk=fk||1; const t=Math.abs(pitch)*DEG, c=Math.cos(t), s=Math.sin(t);
      if(pitch>0){ const toe=0.178*fk; return [p[0], p[1]+toe-toe*c+h*s, p[2]-h+toe*s+h*c]; }
      const heel=0.083*fk; return [p[0], p[1]-heel+heel*c-h*s, p[2]-h+heel*s+h*c]; }

    /* a palm target held inside the arm's reach, read through the chest as the intent now stands (a hand in transit, or on a free path) */
    /* the chest as the solver will see it: the elder's stoop is added after the pose (intentOf), so a reach worked out here adds it too */
    const stooped=(I,K)=>{ const so=K.stoop||0; return so ? { pelvis:I.pelvis, spine:Object.assign({}, I.spine, { pitch:(I.spine.pitch||0)+so*0.5 }), chest:Object.assign({}, I.chest, { pitch:(I.chest.pitch||0)+so*0.5 }) } : I; };
    function inReach(I,K,s,w,margin){ const D=K.D, sk=K.B.sk, Bf=bodyOf(stooped(I,K),K.B), shw=fkp(Bf.Wc, sk.rest[sk.ix['shoulder_'+s]]), arm=D.upper+D.fore+D.palmLen-(margin==null?0.004:margin), d=vsub(w,shw), dl=vlen(d);
      return dl>arm ? vadd(shw, vmul(d, arm/dl)) : w.slice(); }
    /* the hang-to-target blend the solver would make, done here so the in-between frames stay in reach; at k = 1 the target is exact */
    function blendArm(I,K,s,c,w,k,el){ if(k<=0){ I.arms[s]={ c:c.slice(), el }; return; } const Bf=bodyOf(stooped(I,K),K.B), p=vlerp(fkp(Bf.Wc,c), w, clamp01(k)); I.arms[s]={ w: k<1 ? inReach(I,K,s,p) : p, el }; }

    /* ================= base ================= */
    function idle(u,I,K){ const D=K.D;
      I.pelvis.p=[0.006*sn(u), 0, D.pelvisZ-0.018+0.0025*cs(u)]; I.pelvis.roll=-1.4; I.pelvis.yaw=1.5;
      I.spine.roll=1.6; I.chest.roll=0.4; I.spine.yaw=-1; I.spine.pitch=0;
      I.legs.L={ p:K.ft(-0.100,0.024), yaw:-8 }; I.legs.R={ p:K.ft(0.092,-0.012), yaw:7 };
      for(const s of ['L','R']){ const c=hangC(s,D); c[1]+=0.008*sn(u,G(s)*0.12); I.arms[s]={ c }; }
      breath(I,u,1); I.head.roll=-0.8; I.neck.pitch=2;
    }
    const idleAt=(K,dz,dx)=>{ const J=baseIntent(K.D); idle(0,J,K); const d=[dx||0,0,dz||0];
      J.pelvis.p=vadd(J.pelvis.p,d); for(const s of ['L','R']) J.legs[s].p=vadd(J.legs[s].p,d); return J; };
    const WALK={ T:0.88, v:0.727, beta:0.55, lift:0.075, strike:12, toeOff:22, width:0.092, toeOut:5, bob:0.012, sway:0.016, hipYaw:5, hipRoll:2.2, lean:3, arm:0.13, armLen:0.455, armBend:0.05, drop:0.024 };
    const RUN ={ T:0.48, v:2.0, beta:0.36, lift:0.15, kick:0.10, strike:6, toeOff:28, width:0.078, toeOut:3, bob:0.020, sway:0.008, hipYaw:7, hipRoll:1.6, lean:10, run:true, drop:0.045 };
    /* stance: the foot runs back at the walk speed exactly — heel strike, flat, toe-off pivoting on the toe */
    function gait(u,I,K,g){ const D=K.D, k=K.k, beta=g.beta, v=g.v*k.l, Dst=v*beta*g.T, lift=g.lift*k.l, kick=(g.kick||0)*k.l, width=g.width*k.f;
      for(const s of ['L','R']){ const sg=G(s), phi=frac(u+(s==='L'?0:0.5)); let y, z=D.ankle, pitch, planted;
        if(phi<beta){ const t=phi/beta; planted=true; y=Dst/2-Dst*t; pitch = t<0.2 ? -g.strike*(1-sm(t/0.2)) : t>0.6 ? g.toeOff*sm((t-0.6)/0.4) : 0; }
        else { const t=(phi-beta)/(1-beta); planted=false; y=-Dst/2+Dst*sm(t)-kick*Math.sin(Math.PI*t)*(1-t)*2; z=D.ankle+lift*Math.sin(Math.PI*t);
          pitch = t<0.4 ? g.toeOff*(1-sm(t/0.4))-6*sm(t/0.4) : -6-(g.strike-6)*sm((t-0.4)/0.6); }
        I.legs[s]={ p:footPivot([sg*width,y,z],pitch,D.ankle,D.footK), yaw:sg*g.toeOut, pitch }; I.plant[s]=planted; }
      const c2=Math.cos(TAU*2*(u-beta/2)), c1=Math.cos(TAU*(u-beta/2)), py=g.hipYaw*Math.cos(TAU*u);
      I.pelvis.p=[-g.sway*k.f*c1, 0, D.pelvisZ-g.drop*k.l+(g.run?-g.bob*c2:g.bob*c2)*k.l]; I.pelvis.yaw=py; I.pelvis.roll=g.hipRoll*c1;
      I.spine.yaw=-0.8*py; I.chest.yaw=-0.8*py; I.neck.yaw=0.3*py; I.head.yaw=0.3*py;
      I.spine.pitch=g.lean*0.55; I.chest.pitch=g.lean*0.45; I.spine.roll=-0.6*g.hipRoll*c1; I.head.pitch=-g.lean*0.6+(g.run?1.5:0.8)*c2;
      for(const s of ['L','R']){ const sg=G(s), sw=(s==='L'?-1:1)*Math.cos(TAU*u);
        if(g.run) I.arms[s]={ c:[sg*(D.shX+0.035), (0.07+0.15*sw)*k.a, (D.shZ-D.chestZ)+(-0.27+0.05*Math.max(0,sw))*k.a], el:[sg*0.25,-1,-0.25] };
        else { const A=g.arm*k.a, yy=A*sw, Lr=(g.armLen-g.armBend*Math.max(0,yy)/A)*k.a; I.arms[s]={ c:[sg*(D.shX+0.02), yy, (D.shZ-D.chestZ)-Math.sqrt(Math.max(0.04,Lr*Lr-yy*yy))], el:[sg*0.12,-1,-0.1] }; } }
      I.meta.speed=+v.toFixed(3);
    }

    /* ================= fishing — rig 6's rod attitude per frame; the body is new ================= */
    const rodDir=(p,y)=>[Math.cos(p*DEG)*Math.sin(y*DEG), Math.cos(p*DEG)*Math.cos(y*DEG), Math.sin(p*DEG)];
    function angler(I,K,lean,yaw,dy){ const D=K.D; yaw=yaw==null?26:yaw;
      I.legs.L={ p:K.ft(-0.118,0.085), yaw:-10 }; I.legs.R={ p:K.ft(0.108,-0.075), yaw:24 };
      I.pelvis.p=[0.004, dy||0, D.pelvisZ-0.030]; I.pelvis.yaw=yaw*0.55; I.spine.yaw=yaw*0.25; I.chest.yaw=yaw*0.20;
      I.spine.pitch=lean*0.55; I.chest.pitch=lean*0.45; I.neck.yaw=-yaw*0.30; I.head.yaw=-yaw*0.22; I.head.pitch=7; }
    /* right palm on the grip; the left palm on the butt (0.15 m behind the grip along the rod), or given */
    /* 9.2: on a build whose arms cannot make the table's grip (the smallest, broadest children) the rod is held in to the reach, and the
       off hand slides up the rod toward the grip until it reaches; on every other build nothing moves */
    function rodGrip(I,K,grip,pitch,yaw,bend,left){ const d=rodDir(pitch,yaw); grip=inReach(I,K,'R',grip);
      I.arms.R={ w:grip.slice(), el:[1,-0.5,-0.9] };
      if(left==='butt'){ const at=(t)=>vsub(grip,vmul(d,t)), ok=(t)=>{ const q=at(t), r=inReach(I,K,'L',q); return Math.hypot(r[0]-q[0],r[1]-q[1],r[2]-q[2])<1e-9; }; let t=0.15*K.k.a;
        if(!ok(t)){ let lo=0, hi=t; for(let i=0;i<20;i++){ const m=(lo+hi)/2; if(ok(m)) lo=m; else hi=m; } t=lo; } I.arms.L={ w:inReach(I,K,'L',at(t)), el:[-0.8,-0.6,-0.6] }; }
      else if(Array.isArray(left)) I.arms.L={ w:inReach(I,K,'L',left), el:[-0.8,-0.6,-0.6] };
      I.tool={ held:true, kind:'rod', pitch, yaw, bend, len:K.D.rodLen }; }
    const ROD = {
      hold:{ p:[56,58.6,58.6,56,53.4,53.4], b:[.05,.07,.07,.05,.03,.03] },
      cast:{ p:[56,71,101.4,125.2,113,10,14.8,26.2,39.8,51.2], b:[0,.02,.06,.10,.16,.55,.50,.37,.23,.10],
        g:[[0.10,0.22,0.78],[0.12,0.18,0.87],[0.16,0.10,1.01],[0.19,0.04,1.10],[0.18,0.12,1.10],[0.14,0.38,0.95],[0.13,0.36,0.90],[0.12,0.32,0.85],[0.11,0.27,0.81],[0.10,0.24,0.79]],
        lean:[5,2,-2,-4,2,12,11,9,7,6], yaw:[26,28,31,32,26,16,18,21,24,25], dy:[0,-0.01,-0.03,-0.04,-0.01,0.05,0.045,0.03,0.015,0.005] },
      castBack:{ p:[56,61.3,74.7,92,109.3,122.7], b:[0,.01,.03,.05,.07,.09],
        g:[[0.10,0.22,0.78],[0.11,0.20,0.83],[0.13,0.15,0.92],[0.15,0.10,1.01],[0.17,0.06,1.07],[0.19,0.04,1.10]], lean:[5,4,2,0,-2,-4], yaw:[26,27,28,30,31,32], dy:[0,-0.005,-0.015,-0.025,-0.035,-0.04] },
      castRelease:{ p:[128,98.6,10,13.7,22.3,33.3,44.3,52.7], b:[.10,.21,.55,.51,.42,.30,.18,.09],
        g:[[0.19,0.03,1.11],[0.18,0.14,1.09],[0.14,0.38,0.95],[0.13,0.36,0.91],[0.12,0.33,0.86],[0.115,0.29,0.83],[0.105,0.26,0.80],[0.10,0.23,0.785]],
        lean:[-4,3,12,11,9.5,8,7,6], yaw:[32,26,16,18,20,22,24,25], dy:[-0.04,-0.01,0.05,0.045,0.035,0.025,0.012,0.004] },
      bite:{ p:[54,51.9,54,54,51.9,54], b:[.06,.15,.06,.06,.15,.06] },
      strike:{ p:[54,48.9,50.3,87.8,97.7,77.8], b:[.10,.16,.23,.45,.51,.41],
        g:[[0.10,0.23,0.775],[0.10,0.27,0.755],[0.10,0.25,0.76],[0.15,0.10,0.99],[0.16,0.06,1.05],[0.15,0.10,0.98]], lean:[5,8,7,-5,-8,-5], dy:[0,0.015,0.01,-0.03,-0.04,-0.03] },
      reel:{ p:[64,72,77.9,80,77.9,72,64,56,50.1,48,50.1,56], b:[.42,.5,.56,.58,.56,.5,.42,.42,.42,.42,.42,.42] },
      land:{ p:[64,60.1,53.8,58.1,72.2,85.2,97.6,109.6,110.1,103.8,95.8,88.9], b:[.42,.44,.47,.45,.39,.34,.28,.23,.21,.19,.16,.13],
        g:[[0.10,0.22,0.80],[0.10,0.225,0.79],[0.10,0.23,0.775],[0.10,0.22,0.79],[0.13,0.18,0.90],[0.15,0.16,0.99],[0.17,0.14,1.05],[0.18,0.12,1.09],[0.18,0.12,1.10],[0.17,0.14,1.07],[0.16,0.16,1.03],[0.15,0.18,0.99]],
        L:[null,null,null,[0.05,0.28,0.88],[0.02,0.36,1.00],[-0.01,0.42,0.99],[-0.03,0.45,0.97],[-0.05,0.44,0.95],[-0.07,0.38,0.92],[-0.10,0.31,0.90],[-0.12,0.27,0.92],[-0.12,0.25,0.94]] } };
    function rodTable(u,I,K,T,left){ const m=K.mode, p=ftab(u,T.p,m), b=ftab(u,T.b,m), g=K.bp(ftab(u,T.g,m));
      angler(I,K, T.lean?ftab(u,T.lean,m):5, T.yaw?ftab(u,T.yaw,m):26, T.dy?ftab(u,T.dy,m):0); rodGrip(I,K,g,p,16,b,left); }
    const fr=(u,n)=>Math.round(frac(u)*n)%n;

    /* ================= digging — the shaft runs left palm to right palm ================= */
    const DIG={ L:[[0.02,0.16,0.84],[0.03,0.19,0.79],[0.03,0.21,0.75],[0.00,0.09,0.68],[0.02,0.13,0.82],[0.10,0.12,0.92],[0.12,0.10,0.94],[0.06,0.13,0.90],[0.03,0.17,0.87],[0.02,0.17,0.85]],
                R:[[0.17,0.30,0.63],[0.18,0.33,0.58],[0.19,0.34,0.57],[0.18,0.32,0.58],[0.18,0.34,0.71],[0.32,0.26,0.83],[0.36,0.18,0.81],[0.26,0.28,0.73],[0.18,0.32,0.67],[0.17,0.30,0.65]],
                lean:[22,26,28,20,18,14,12,16,20,22], yaw:[8,8,8,5,7,20,26,17,10,9] };

    /* ================= the saddle ================= */
    function solveLean(I,K,grip,lo,hi){ const D=K.D, arm=D.upper+D.fore+D.palmLen-0.012;
      const need=(deg)=>{ const Bf=bodyOf({ pelvis:I.pelvis, spine:{ pitch:deg*0.6 }, chest:{ pitch:deg*0.4 } }, K.B); return vlen(vsub(grip, fkp(Bf.Wc,[D.shX,0,D.shZ-D.chestZ])))-arm; };
      if(need(lo)<=0) return { deg:lo, solved:false, short_mm:0 };
      if(need(hi)>0) return { deg:hi, solved:true, short_mm:Math.round(need(hi)*1000) };
      let a=lo, b=hi; for(let i=0;i<24;i++){ const m=(a+b)/2; if(need(m)>0) a=m; else b=m; } return { deg:b, solved:true, short_mm:0 }; }
    function saddlePose(u,I,K,up){ const M=K.saddle, bench=M.bench && !up, j=Math.sin(2*TAU*u), sway=sn(u), seat=M.seat;
      /* sat forward on the saddle, toward the tank: from the seat reference this build's legs cannot make the pegs */
      I.pelvis.p=[0, seat[1]+(up?0.22:0.17), seat[2]+0.075+(up?0.02:0)+(up?0.008:0.005)*j]; I.pelvis.pitch=up?10:5;
      const floor=up?22:bench?4:9, lean=solveLean(I,K,M.gripR,floor,up?40:34);
      I.spine.pitch=lean.deg*0.6+(up?1.6:0.9)*j; I.chest.pitch=lean.deg*0.4; I.spine.roll=(up?1.4:0.8)*sway; I.chest.yaw=(up?2.6:1.8)*sway;
      I.neck.pitch=-lean.deg*0.35; I.head.pitch=-lean.deg*0.25;
      const kx=up?0.74:bench?0.39:0.86;
      for(const s of ['L','R']){ const g=G(s), peg=g<0?M.pegL:M.pegR;
        I.legs[s]={ p:[peg[0], peg[1], peg[2]+0.055+g*0.004*j*(up?1:0.5)], yaw:g*6, pitch:-4, splay:kx };
        I.arms[s]={ w:(g<0?M.gripL:M.gripR).slice(), el:[g*0.8,-0.4,-0.6] }; }
      I.plant={ L:true, R:true };
      I.pins.saddle={ posture:up?'standing_on_pegs':bench?'seated_bench':'seated_astride', seat:M.seat, gripL:M.gripL, gripR:M.gripR, pegL:M.pegL, pegR:M.pegR,
        lean:{ deg:+lean.deg.toFixed(2), floor, solved:lean.solved, short_mm:lean.short_mm }, machineLean:M.leanDeg, saddleSupplied:M.supplied };
    }
    /* the mount (9.2). The standing foot stays on the ground until the seat carries the body. mountUp on t (the dismounts run it backward):
         0.00-0.30  a four-step shuffle in beside the machine, the swing foot first, each foot half the way per step: the standing
                    foot ends beside its own peg, the swing foot just behind it
         0.30-0.70  the swing leg over the back of the seat (in front of it on the bench) to the far peg
         0.64-0.90  the body moves over the seat and sits; the hands take the grips; the standing foot has not moved
         0.90-1.00  the seat carries the body and the standing foot comes up onto its peg
       The pelvis is held low enough that every foot reaches its target (a planted foot is never pulled off the ground to follow it);
       hands in transit to the grips are held inside the arm's reach. On a seat too high for the standing leg to sit down on (the
       default saddle's 0.94 m seat is higher than any body can sit on from the ground) the body cannot be carried there with the foot
       down: the last phase lifts it the rest of the way and ikShort reports the shortfall. */
    function mount(u,I,K){ const D=K.D, B=K.B, cab=K.anim==='mountCab'||K.anim==='mountCabDown', rev=K.anim==='mountDown'||K.anim==='mountCabDown';
      const t=rev?1-u:u, s=K.mountSide, restX=s*K.reachX, I0=idleAt(K,0,restX), sk=B.sk, rest=sk.rest, ix=sk.ix;
      const Ks=Object.assign({},K,{ saddle: cab ? Object.assign({},K.saddle,{ bench:true }) : K.saddle }), I1=baseIntent(D); saddlePose(0,I1,Ks,false); const M=Ks.saddle;
      const stand=s<0?'L':'R', swing=s<0?'R':'L', pegN=s<0?M.pegL:M.pegR, pegF=s<0?M.pegR:M.pegL, a=D.ankle, leg=D.thigh+D.shin-0.003, arm=D.upper+D.fore+D.palmLen-0.004;
      const P0=I0.pelvis.p, P1=I1.pelvis.p, A0=I0.legs[swing].p, B0=I0.legs[stand].p, A1=I1.legs[swing].p, B1=I1.legs[stand].p;
      /* the ground spots: the swing foot 5 cm outside the near peg, the standing foot a stance beyond it; then beside its own peg */
      const stN=[pegN[0]+s*0.045, pegN[1], a], swG=[pegN[0]+s*0.01, pegN[1]-0.24, a], stG=stN, xIn=P0[0]+((swG[0]+stG[0])-(A0[0]+B0[0]))/2, yIn=((swG[1]+stG[1])-(A0[1]+B0[1]))/2;
      const s1=seg(t,0.00,0.10), s2=seg(t,0.07,0.17), s3=seg(t,0.13,0.23), s4=seg(t,0.20,0.30), app=seg(t,0.02,0.30);
      const swLin=clamp01((t-0.30)/0.40), sw=0.5*(swLin+sm(swLin)), sit=seg(t,0.64,0.90), lift=seg(t,0.90,1);   // the swing eased half-linear: a long foot path at an even pace
      const gripK={ [stand]:seg(t,0.56,0.89), [swing]:seg(t,0.62,0.90) };
      /* can the seat carry the body before the standing foot leaves the ground? seated pose, standing foot beside its peg on its toes (40 deg) */
      const hip1=fkp(bodyOf(I1,B).Wp, rest[ix['hip_'+stand]]), carryGap=Math.max(0, vlen(vsub(hip1, footPivot(stN,40,a,D.footK)))-leg);
      copyIntent(I,I0);
      /* the feet */
      const hopZ=(k)=>0.05*Math.sin(Math.PI*k);
      const mA=vlerp(A0,swG,0.5), mB=vlerp(B0,stG,0.5);
      let pSw = s3>0 ? vlerp(mA,swG,s3) : vlerp(A0,mA,s1); pSw[2]+=hopZ(s3>0?s3:s1);
      const clearZ = cab ? Math.max(pegF[2], M.seat[2]-0.20)+0.12+a : M.seat[2]+0.08+a, farPeg=A1;
      /* the swing foot passes through W at the middle of its path: behind the seat at the seat's clearance on the saddle (a leg swung back and
         over, never through the hip), in front of the seat on the bench */
      if(sw>0){ const Wm=[lerp(swG[0],farPeg[0],0.5), cab ? lerp(swG[1],farPeg[1],0.5)+0.22 : Math.min(swG[1],farPeg[1],M.seat[1])-0.14, clearZ], ctl=vsub(vmul(Wm,2), vmul(vadd(swG,farPeg),0.5)); pSw=bez(swG,ctl,farPeg,sw); }
      let pSt = s4>0 ? vlerp(mB,stG,s4) : vlerp(B0,mB,s2); pSt[2]+=hopZ(s4>0?s4:s2);
      if(lift>0){ pSt=bez(stN,[lerp(stN[0],B1[0],0.3), stN[1], Math.max(stN[2],B1[2])+0.05],B1,lift); }
      const kSw=Math.max(sw, 0), kSt=lift;
      const spl=lerp(0.10, I1.legs[swing].splay, kSw);
      I.legs[swing]={ p:pSw, yaw:lerp(I0.legs[swing].yaw||0, I1.legs[swing].yaw||0, kSw), pitch:lerp(I0.legs[swing].pitch||0, I1.legs[swing].pitch||0, kSw), splay:spl };
      I.legs[stand]={ p:pSt, yaw:lerp(I0.legs[stand].yaw||0, I1.legs[stand].yaw||0, kSt), pitch:lerp(I0.legs[stand].pitch||0, I1.legs[stand].pitch||0, kSt), splay:lerp(0.10, I1.legs[stand].splay, kSt) };
      const stepping=(k)=>k>0 && k<1;
      I.plant[swing] = !(stepping(s1)||stepping(s3)||stepping(sw)); I.plant[stand] = !(stepping(s2)||stepping(s4)||stepping(lift));
      /* the body: in beside the machine, leaning toward it through the swing, then over and down onto the seat */
      const lean=sw*(1-sit);
      I.pelvis.p=[lerp(lerp(P0[0],xIn,app),P1[0],sit)-s*0.02*lean, lerp(P0[1]+yIn*app,P1[1],sit), lerp(P0[2]-0.012*Math.sin(Math.PI*app),P1[2],sit)];
      for(const k of ['yaw','pitch','roll']) I.pelvis[k]=lerp(I0.pelvis[k]||0, I1.pelvis[k]||0, sit);
      for(const part of ['spine','chest','neck','head']) for(const k of ['yaw','pitch','roll']) I[part][k]=lerp(I0[part][k]||0, I1[part][k]||0, sit);
      I.spine.pitch+=(cab?10:8)*lean; I.spine.roll+=-s*(cab?5:7)*lean; I.chest.yaw+=s*(cab?7:10)*lean*(1-sit);
      /* the governor: lower the pelvis until every foot below its hip reaches its target; it lets go as the seat takes the body (t 0.90 -> 1) */
      const hold=1-lift; let seatGap=0, toe=0; if(hold>0){ const Wp=bodyOf(I,B).Wp;
        const zMaxOf=(f)=>{ const T=I.legs[f].p, h=vsub(fkp(Wp, rest[ix['hip_'+f]]), Wp.p), hw=vadd(I.pelvis.p,h), dx=hw[0]-T[0], dy=hw[1]-T[1], r2=leg*leg-dx*dx-dy*dy;
          return (hw[2]<=T[2] || r2<=0) ? Infinity : T[2]+Math.sqrt(r2)-h[2]; };
        /* sitting down onto a seat the flat foot cannot hold: the standing foot rises onto its toes first (up to 40 deg) */
        if(sit>0 && lift<=0 && zMaxOf(stand)<I.pelvis.p[2]){ const base=I.legs[stand].p.slice(), fit=(pd)=>{ I.legs[stand].p=footPivot(base,pd,a,D.footK); I.legs[stand].pitch=pd; return zMaxOf(stand)>=I.pelvis.p[2]; };
          let lo=0, hi=40*sit; if(fit(hi)){ for(let i=0;i<20;i++){ const m=(lo+hi)/2; if(fit(m)) hi=m; else lo=m; } } fit(hi); toe=hi; }
        let dz=0; for(const f of ['L','R']) dz=Math.min(dz, zMaxOf(f)-I.pelvis.p[2]);
        I.pelvis.p[2]+=dz*hold; if(sit>=1 || lift>0) seatGap=-dz*hold; }
      seatGap=Math.max(seatGap, carryGap);
      /* the hands: from the hang to the grips; in transit held inside the arm's reach, on the grip exact */
      const Bf=bodyOf(stooped(I,K),B);
      for(const h of ['L','R']){ const g=G(h), k=gripK[h], cw=fkp(Bf.Wc, I0.arms[h].c), grip=I1.arms[h].w; let w=vlerp(cw, grip, k);
        if(k<=0){ I.arms[h]={ c:I0.arms[h].c.slice(), el:[g*0.12,-1,-0.1] }; continue; }
        if(k<1){ const shw=fkp(Bf.Wc, rest[ix['shoulder_'+h]]), d=vsub(w,shw), dl=vlen(d); if(dl>arm) w=vadd(shw, vmul(d, arm/dl)); }
        I.arms[h]={ w, el:vlerp([g*0.12,-1,-0.1], I1.arms[h].el, k) }; }
      I.pins.saddle=Object.assign({}, I1.pins.saddle, { posture:rev?'dismounting':'mounting', xOff:+I.pelvis.p[0].toFixed(4),
        phase:{ t:+t.toFixed(3), side:s, standSide:stand, swingSide:swing, approach:+app.toFixed(3), swing:+sw.toFixed(3), seated:+sit.toFixed(3), grounded:+(1-lift).toFixed(3), reachX:K.reachX,
          standFoot:lift>0 ? (lift>=1 ? 'peg' : 'rising') : 'ground', swingFoot:sw>=1 ? 'peg' : sw>0 ? 'over' : 'ground', toeDeg:+toe.toFixed(2), seatGap_m:+seatGap.toFixed(4), carried:carryGap<=0.001 } });
      I.meta.handoff = rev ? { from:cab?'astride(bench) f0':'astride f0', to:'idle f0 at xOff' } : { from:'idle f0 at xOff', to:cab?'astride(bench) f0':'astride f0' };
    }

    const POSES = {
      idle,
      walk(u,I,K){ gait(u,I,K,WALK); },
      run(u,I,K){ gait(u,I,K,RUN); },
      hold(u,I,K){ const T=ROD.hold, p=ftab(u,T.p,K.mode), b=ftab(u,T.b,K.mode); angler(I,K,5,26,0); breath(I,u,0.8); rodGrip(I,K,K.bp([0.10,0.22,0.78+0.0015*(p-56)]),p,16,b,'butt'); },
      cast(u,I,K){ rodTable(u,I,K,ROD.cast,'butt'); const f=fr(u,10); face(I,'open',(f>=2&&f<=4)?'knit':'flat', f===5?'open':'flat'); },
      castBack(u,I,K){ rodTable(u,I,K,ROD.castBack,'butt'); if(u>0.5) face(I,'open','knit','flat'); },
      castRelease(u,I,K){ rodTable(u,I,K,ROD.castRelease,'butt'); if(fr(u,8)===2) face(I,'open','knit','open'); },
      bite(u,I,K){ const T=ROD.bite, p=ftab(u,T.p,K.mode), b=ftab(u,T.b,K.mode), jolt=(54-p)/2.1; angler(I,K,6,26,0);
        rodGrip(I,K,K.bp([0.10,0.225+0.008*jolt,0.775-0.014*jolt]),p,16,b,'butt'); I.head.pitch=10; face(I, jolt>0.3?'wide':'open', jolt>0.3?'up':'flat', 'flat'); },
      strike(u,I,K){ rodTable(u,I,K,ROD.strike,'butt'); if(u>=0.45) face(I,'open','knit','grit'); },
      reel(u,I,K){ const T=ROD.reel, p=ftab(u,T.p,K.mode), b=ftab(u,T.b,K.mode); angler(I,K,7,26,0);
        const grip=K.bp([0.10,0.21,0.80+0.002*(p-64)]); rodGrip(I,K,grip,p,16,b,null);
        const d=rodDir(p,16), side=vnorm(vcross([0,0,1],d)), upv=vcross(d,side), th=TAU*frac(u)*2, c=vadd(vadd(grip,vmul(d,0.07)),vmul(side,0.055));
        I.arms.L={ w:vadd(c, vadd(vmul(d,0.04*Math.cos(th)), vmul(upv,0.04*Math.sin(th)))), el:[-0.8,-0.6,-0.6] }; face(I,'open','knit','grit'); },
      land(u,I,K){ const T=ROD.land, m=K.mode, p=ftab(u,T.p,m), b=ftab(u,T.b,m), g=K.bp(ftab(u,T.g,m)); angler(I,K,6,26,0);
        const Lp=T.L.map((q,k)=>q || vsub(T.g[k], vmul(rodDir(T.p[k],16),0.15))); rodGrip(I,K,g,p,16,b,K.bp(ftab(u,Lp,m)));
        const f=fr(u,12); face(I,'open', f>=6&&f<=8?'knit':'flat', f>=6&&f<=8?'open':f>=9?'smile':'flat'); },
      dig(u,I,K){ const D=K.D, m=K.mode, Lh=K.bp(ftab(u,DIG.L,m)), Rh=K.bp(ftab(u,DIG.R,m)), lean=ftab(u,DIG.lean,m), yaw=ftab(u,DIG.yaw,m);
        I.legs.L={ p:K.ft(-0.125,0.13), yaw:-8 }; I.legs.R={ p:K.ft(0.125,-0.08), yaw:20 };
        I.pelvis.p=[0.01,-0.03,D.pelvisZ-0.07]; I.pelvis.yaw=yaw*0.6; I.spine.yaw=yaw*0.25; I.chest.yaw=yaw*0.15;
        I.spine.pitch=lean*0.55; I.chest.pitch=lean*0.45; I.neck.pitch=-lean*0.3; I.head.pitch=10;
        I.arms.L={ w:Lh, el:[-0.6,-0.8,-0.4] }; I.arms.R={ w:Rh, el:[0.9,-0.4,-0.6] };
        const d=vsub(Rh,Lh), dl=vlen(d)||1; I.tool={ held:true, kind:'shovel', pitch:Math.asin(d[2]/dl)/DEG, yaw:Math.atan2(d[0],d[1])/DEG, bend:0, len:1.05 };
        const f=fr(u,10); face(I,'open',(f>=1&&f<=3)?'knit':'flat',(f>=1&&f<=3)?'grit':(f===5||f===6)?'open':'flat'); },
      balance(u,I,K){ const D=K.D, s1=sn(u), s2=sn(u,0.25);
        I.legs.L={ p:K.ft(-0.175,0.02), yaw:-14 }; I.legs.R={ p:K.ft(0.175,-0.02), yaw:14 };
        I.pelvis.p=[0.028*s1,0,D.pelvisZ-0.07+0.008*sn(2*u)]; I.pelvis.roll=-2.5*s1;
        I.spine.roll=5*s1; I.chest.roll=3*s1; I.spine.pitch=6; I.chest.pitch=2; I.neck.roll=-3*s1; I.head.roll=-3*s1;
        for(const s of ['L','R']){ const g=G(s); I.arms[s]={ c:[g*(D.shX+0.36*K.k.a), (0.05+0.03*s2*g)*K.k.a, (D.shZ-D.chestZ)+(0.02-0.10*s1*g)*K.k.a], el:[g*0.2,-0.6,-1] }; }
        face(I,'open','knit','flat'); },
      stagger(u,I,K){ const D=K.D, a=D.ankle, SF=(q)=>[Math.sign(q[0])*(D.hipX+(Math.abs(q[0])-0.085)*K.k.l), q[1]*K.k.l, a+(q[2]-a)*K.k.l];
        const lurch=keys(u,[[0,0],[0.18,1],[0.42,-0.45],[0.66,0.35],[0.86,-0.1],[1,0]]);
        I.pelvis.p=[0.07*lurch*K.k.l, 0.02*sn(u), D.pelvisZ-(0.04+0.02*Math.abs(lurch))*K.k.l]; I.pelvis.roll=-4*lurch;
        I.spine.roll=11*lurch; I.chest.roll=6*lurch; I.spine.pitch=8; I.neck.roll=-5*lurch; I.head.roll=-4*lurch;
        I.legs.R={ p:SF(keys(u,[[0,[0.10,0,a]],[0.12,[0.17,0.02,a+0.08]],[0.24,[0.25,0.03,a]],[0.60,[0.25,0.03,a]],[0.70,[0.18,0,a+0.06]],[0.80,[0.10,-0.01,a]],[1,[0.10,0,a]]])), yaw:10 };
        I.legs.L={ p:SF(keys(u,[[0,[-0.10,0,a]],[0.30,[-0.10,0,a]],[0.38,[-0.14,-0.02,a+0.07]],[0.48,[-0.20,-0.03,a]],[0.82,[-0.20,-0.03,a]],[0.90,[-0.15,-0.01,a+0.05]],[1,[-0.10,0,a]]])), yaw:-10 };
        I.plant={ R:I.legs.R.p[2]<a+0.002, L:I.legs.L.p[2]<a+0.002 };
        for(const s of ['L','R']){ const g=G(s); I.arms[s]={ c:[g*(D.shX+0.22*K.k.a), 0.08*K.k.a, (D.shZ-D.chestZ)+(-0.22-0.16*lurch*g)*K.k.a], el:[g*0.3,-0.6,-1] }; }
        face(I,'wide','up','open'); },

      /* ---- boarding: built from idle at the dock and idle on the deck ---- */
      board(u,I,K){ const D=K.D, Rz=K.railZ, a=D.ankle, carried=!!K.carry, t=clamp01(u/0.9), I0=idleAt(K,0,0), I1=idleAt(K,Rz,0); copyIntent(I,I0);
        const step=seg(t,0.00,0.54), trail=seg(t,0.36,0.90), drive=seg(t,0.28,0.88), settle=seg(t,0.82,1), reach=seg(t,0.02,0.22);
        const R0=I0.legs.R.p, R1=I1.legs.R.p, L0=I0.legs.L.p, L1=I1.legs.L.p;
        I.legs.R.p=vlerp(bez(R0,[R0[0],0.30,Rz+a+0.10],[R0[0],0.24,Rz+a],step), R1, settle);
        I.legs.L.p=bez(L0,[L0[0],0.12,Rz+a+0.08],L1,trail); I.legs.L.pitch=(I.legs.L.pitch||0)+18*seg(t,0.22,0.32)*(1-trail);
        if(I.legs.L.pitch>0.5) I.legs.L.p=footPivot(I.legs.L.p, I.legs.L.pitch, a, D.footK);
        I.plant.R=step<=0||step>=1; I.plant.L=trail<=0||trail>=1;
        const P0=I0.pelvis.p; I.pelvis.p=[P0[0]+0.012*drive*(1-settle), P0[1]+0.18*drive*(1-settle)+0.03*reach*(1-drive), P0[2]-0.07*step*(1-drive)+Rz*drive-0.03*seg(t,0.72,0.84)*(1-settle)];
        const lean=22*reach*(1-0.7*drive)*(1-settle); I.spine.pitch+=lean*0.6; I.chest.pitch+=lean*0.4; I.head.pitch+=-lean*0.35;
        blendIntent(I,I1,settle);
        const handOn=carried?0:seg(t,0.10,0.24)*(1-seg(t,0.30,0.44)), rail=[-0.13*K.k.x,0.20,Rz];
        if(!carried) I.arms.L={ c:I0.arms.L.c.slice(), w:[rail[0],rail[1],Rz+0.012], k:handOn, el:vlerp([-0.12,-1,-0.1],[-0.5,-1,-0.2],handOn) };
        I.pins.board={ railZ:+Rz.toFixed(4), requested:K.railReq, clamped:K.railReq!==Rz, rise:+(Rz*drive).toFixed(4), phase:t<0.10?'load':t<0.26?'reach':t<0.40?'step':t<0.80?'drive':'settle',
          handOn:+handOn.toFixed(3), gripped:!carried&&handOn>0.5, carried, rail, leadFoot:'R', trailFoot:'L', landing:[0,0,Rz] };
        if(drive>0.1 && drive<0.9) face(I,'open','knit','flat');
        I.meta.handoff={ from:'idle f0 (dock)', to:'idle f0 (deck, +railZ)' }; },
      boardDown(u,I,K){ const D=K.D, Rz=K.railZ, a=D.ankle, t=clamp01(u/(5/6)), I0=idleAt(K,Rz,0), I1=idleAt(K,0,0); copyIntent(I,I0);
        const lead=seg(t,0.02,0.52), shift=seg(t,0.04,0.56), trail=seg(t,0.40,0.90), settle=seg(t,0.58,1), crouch=seg(t,0.06,0.40)*(1-seg(t,0.50,0.92));
        const P0=I0.pelvis.p, px=[P0[0], P0[1]+0.16*shift*(1-settle), P0[2]-Rz*seg(t,0.20,0.86)-0.10*crouch];
        I.pelvis.p=px;
        let leadP=bez(I0.legs.R.p,[I0.legs.R.p[0],0.24,Rz+a+0.06],[I0.legs.R.p[0],0.20,a],lead);
        const hipR=[px[0]+D.hipX,px[1],px[2]-(D.pelvisZ-D.hipZ)], dv=vsub(leadP,hipR), dl=vlen(dv), lr=D.thigh+D.shin-0.01; if(dl>lr) leadP=vadd(hipR,vmul(dv,lr/dl));
        I.legs.R.p=vlerp(leadP, I1.legs.R.p, settle);
        I.legs.L.p=bez(I0.legs.L.p,[I0.legs.L.p[0],0.06,Rz*0.55+a+0.10],I1.legs.L.p,trail); I.legs.L.pitch=(I.legs.L.pitch||0)+16*seg(t,0.30,0.50)*(1-trail);
        I.plant.R=I.legs.R.p[2]<=a+0.002 || lead<=0; I.plant.L=trail<=0||trail>=1;
        I.spine.pitch+=10*lead*(1-settle)+6*crouch; I.chest.pitch+=4*crouch; I.head.pitch+=6*lead*(1-settle);
        for(const s of ['L','R']){ const g=G(s); I.arms[s]={ c:vadd(I0.arms[s].c,[g*0.10*crouch, 0.06*crouch, 0.10*crouch]), el:vlerp([g*0.12,-1,-0.1],[g*0.3,-1,-0.3],crouch) }; }
        blendIntent(I,I1,settle); for(const s of ['L','R']) I.arms[s].c=vlerp(I.arms[s].c, I1.arms[s].c, settle);
        I.pins.board={ railZ:+Rz.toFixed(4), requested:K.railReq, clamped:K.railReq!==Rz, rise:+(I.pelvis.p[2]-I1.pelvis.p[2]).toFixed(4), phase:t<0.02?'lift':t<0.52?'step':t<0.90?'lower':'settle',
          handOn:0, gripped:false, carried:!!K.carry, leadFoot:'R', trailFoot:'L', landing:[0,0,Rz] };
        I.meta.handoff={ from:'idle f0 (deck, +railZ)', to:'idle f0 (dock)' }; },
      haul(u,I,K){ const D=K.D, pull=0.5-0.5*Math.cos(TAU*2*u);
        I.legs.L={ p:K.ft(-0.12,0.10), yaw:-8 }; I.legs.R={ p:K.ft(0.11,-0.10), yaw:18 };
        I.pelvis.p=[0,-0.02-0.02*pull,D.pelvisZ-0.05]; I.pelvis.yaw=6; I.spine.pitch=-6-4*pull; I.chest.pitch=-3-2*pull; I.head.pitch=8; I.neck.pitch=4;
        const path=(ph)=>{ ph=frac(ph); if(ph<0.5) return vlerp([0,0.36,0.97],[0,0.10,0.86],sm(ph/0.5)); const t=sm((ph-0.5)/0.5), q=vlerp([0,0.10,0.86],[0,0.36,0.97],t); q[2]+=0.07*Math.sin(Math.PI*t); q[0]-=0.05*Math.sin(Math.PI*t); return q; };
        const pr=K.bp(path(u)), pl=K.bp(path(u+0.5)); pr[0]+=0.045; pl[0]-=0.01;
        /* the rope runs through the hands wherever they are, so the stroke is held inside each arm's reach */
        I.arms.R={ w:inReach(I,K,'R',pr), el:[0.7,-0.8,-0.5] }; I.arms.L={ w:inReach(I,K,'L',pl), el:[-0.7,-0.8,-0.5] };
        face(I,'open',pull>0.6?'knit':'flat',pull>0.6?'grit':'flat'); I.pins.haul={ lead:'R', tension:+pull.toFixed(3), out:[0,0.5,0.12] }; },
      /* ---- the ladder, body-anchored: the pelvis holds its height, the rungs run up through the clip at the rate,
              and the engine lowers the figure at `rate` (root motion). The loop then closes exactly, with no re-seat. ---- */
      ladderDown(u,I,K){ const D=K.D, r=K.rung, sY=K.standoff, stepZ=2*r, w2=Math.min(0.17,K.ladderW/2-0.05), a=D.ankle, drop=stepZ*u, fy=sY-0.11;
        const rS=seg(u,0.20,0.50), lS=seg(u,0.70,1.0), rz=lerp(r,-r,rS)+0.05*Math.sin(Math.PI*rS)+drop, lz=lerp(0,-2*r,lS)+0.05*Math.sin(Math.PI*lS)+drop;
        I.legs.R={ p:[0.10*K.k.f, fy-0.06*Math.sin(Math.PI*rS), rz+a], yaw:4, pitch:-4, splay:0.6 };
        I.legs.L={ p:[-0.10*K.k.f, fy-0.06*Math.sin(Math.PI*lS), lz+a], yaw:-4, pitch:-4, splay:0.6 };
        I.plant.R=rS<=0||rS>=1; I.plant.L=lS<=0||lS>=1;
        I.pelvis.p=[0,0,D.pelvisZ-0.035]; I.pelvis.pitch=4; I.spine.pitch=4; I.chest.pitch=2; I.neck.pitch=6; I.head.pitch=14;
        const lH=seg(u,0.0,0.38), rH=seg(u,0.50,0.88);
        I.arms.L={ w:[-w2, sY-0.03-0.07*Math.sin(Math.PI*lH), lerp(4*r,2*r,lH)+0.02+0.04*Math.sin(Math.PI*lH)+drop], el:[-0.9,-0.5,-0.5] };
        I.arms.R={ w:[ w2, sY-0.03-0.07*Math.sin(Math.PI*rH), lerp(3*r,r,rH)+0.02+0.04*Math.sin(Math.PI*rH)+drop], el:[0.9,-0.5,-0.5] };
        const moving=rS>0&&rS<1?'R':lS>0&&lS<1?'L':lH>0&&lH<1?'handL':rH>0&&rH<1?'handR':'set';
        I.pins.ladder={ rung:r, requested:K.rungReq, width:K.ladderW, stepZ, descend:+drop.toFixed(4), rate:+(stepZ/1.1).toFixed(3), rootMotion:[0,0,-+(stepZ/1.1).toFixed(3)], standoff:sY, moving,
          airL:I.plant.L?0:1, airR:I.plant.R?0:1, handsOn:true, rungZ:{ L:+lz.toFixed(4), R:+rz.toFixed(4) },
          rig6:'rig 6 anchored the cell to a rung and sank the body; here the rung the lower foot last planted on sits at z = descend' };
        I.meta.speed=+(stepZ/1.1).toFixed(3); },
      /* ---- the deck-work family: workZ is a world metre ---- */
      hauler(u,I,K){ const D=K.D, Z=K.workZ, sv=K.sheave, pull=0.5-0.5*Math.cos(TAU*u);
        I.legs.L={ p:K.ft(-0.12,0.10), yaw:-10 }; I.legs.R={ p:K.ft(0.12,-0.06), yaw:14 };
        I.pelvis.p=[0.01,-0.01-0.03*pull,D.pelvisZ-0.035]; I.pelvis.yaw=10; I.spine.pitch=8-6*pull; I.chest.pitch=4-3*pull; I.spine.yaw=-4; I.head.pitch=10; I.head.yaw=12;
        I.arms.L={ w:[sv[0]-0.08, sv[1]-0.06, Z-0.02+0.02*pull], el:[-0.7,-0.8,-0.5] }; I.arms.R={ w:vlerp([0.16,0.30,Z-0.10],[0.20,0.06,Z-0.34],pull), el:[0.8,-0.8,-0.5] };
        face(I,'open',pull>0.6?'knit':'flat',pull>0.6?'grit':'flat');
        I.pins.work={ layer:'warp', workZ:Z, requested:K.workReq, clamped:K.workReq!==Z, tension:+pull.toFixed(3), turns:+u.toFixed(3), sheave:sv, out:vnorm(vsub(sv,[sv[0]-0.08,sv[1]-0.06,Z])).map(x=>+x.toFixed(4)) }; },
      bench(u,I,K){ const D=K.D, Z=K.workZ, item=[-0.02,0.34,Z], th=TAU*u, band=seg(u,0.55,0.70)*(1-seg(u,0.90,1.0));
        I.legs.L={ p:K.ft(-0.11,0.02), yaw:-6 }; I.legs.R={ p:K.ft(0.11,0), yaw:6 };
        I.pelvis.p=[0,-0.02,D.pelvisZ-0.03]; I.spine.pitch=10; I.chest.pitch=6; I.neck.pitch=8; I.head.pitch=14;
        I.arms.L={ w:[item[0]-0.07,item[1]-0.02,Z+0.05+0.01*sn(u)], el:[-0.7,-0.8,-0.4] };
        I.arms.R={ w:[item[0]+0.08+0.03*Math.cos(2*th), item[1]+0.02*Math.sin(2*th), Z+0.09+0.03*Math.sin(th)], el:[0.7,-0.8,-0.4] };
        I.pins.work={ layer:'bench', workZ:Z, requested:K.workReq, clamped:K.workReq!==Z, item, band:+band.toFixed(3) }; },
      chop(u,I,K){ const D=K.D, Z=K.workZ, m=K.mode, p=ftab(u,[-55.4,-23.2,-14.6,-16.5,-43.3,-59.7,-45.5,-37.2],m), hz=ftab(u,[0.22,0.10,0.03,0.03,0.12,0.24,0.20,0.16],m);
        I.legs.L={ p:K.ft(-0.11,0.04), yaw:-8 }; I.legs.R={ p:K.ft(0.11,-0.02), yaw:10 };
        I.pelvis.p=[0,-0.02,D.pelvisZ-0.03]; I.spine.pitch=12; I.chest.pitch=6; I.neck.pitch=6; I.head.pitch=16;
        I.arms.R={ w:[0.10,0.30,Z+0.07+hz], el:[0.8,-0.8,-0.3] }; I.arms.L={ w:[-0.08,0.31,Z+0.05], el:[-0.7,-0.8,-0.4] };
        I.tool={ held:true, kind:'knife', pitch:p, yaw:10, bend:0, len:0.22 };
        I.pins.work={ layer:'knife', workZ:Z, requested:K.workReq, clamped:K.workReq!==Z, cut:hz<0.05?1:0, lift:+hz.toFixed(3), item:[-0.02,0.32,Z] }; },
      lift(u,I,K){ const D=K.D, Z=K.workZ, t=clamp01(u/0.875), I0=idleAt(K,0,0); copyIntent(I,I0);
        const down=seg(t,0,0.40)*(1-seg(t,0.46,0.98)), grip=seg(t,0.34,0.46), up=seg(t,0.46,1.0), lz=lerp(0.30,Z,up), ly=lerp(0.28,0.34,up);
        I.pelvis.p=[I0.pelvis.p[0], -0.09*down, I0.pelvis.p[2]-0.25*K.k.l*down]; I.pelvis.pitch=10*down;
        I.spine.pitch+=22*down+4*up; I.chest.pitch+=6*down; I.neck.pitch+=-6*down; I.head.pitch+=4*up;
        for(const s of ['L','R']){ const g=G(s); I.legs[s]={ p:vlerp(I0.legs[s].p,K.ft(g*0.13,0.03),seg(t,0,0.2)), yaw:lerp(I0.legs[s].yaw,g*12,seg(t,0,0.2)), splay:lerp(0.10,0.35,seg(t,0,0.2)) };
          blendArm(I,K,s,I0.arms[s].c,[g*0.25,ly,lz],seg(t,0.08,0.42),vlerp([g*0.12,-1,-0.1],[g*0.7,-0.8,-0.4],seg(t,0.08,0.42))); }
        I.pins.work={ layer:'load', workZ:Z, requested:K.workReq, clamped:K.workReq!==Z, mid:[0,+ly.toFixed(4),+lz.toFixed(4)], hold:+grip.toFixed(3), holding:grip>0.5,
          phase:t<0.34?'reach':t<0.46?'grip':t<0.86?'lift':'raise', progress:+t.toFixed(3) };
        if(up>0.05 && up<0.9) face(I,'open','knit','grit'); },
      place(u,I,K){ const D=K.D, Z=K.workZ, t=clamp01(u/0.875), I0=idleAt(K,0,0); copyIntent(I,I0);
        const set=seg(t,0,0.55), rel=seg(t,0.57,1.0), mid=vlerp(K.bp([0,0.24,0.86]),[0,0.42,Z+0.03],set);
        I.spine.pitch+=10*set*(1-rel)+2; I.chest.pitch+=4*set*(1-rel); I.pelvis.p=[I0.pelvis.p[0],0.02*set*(1-rel),I0.pelvis.p[2]-0.03*set*(1-rel)]; I.head.pitch+=8;
        for(const s of ['L','R']){ const g=G(s); blendArm(I,K,s,I0.arms[s].c,vadd(mid,[g*0.25,0,0]),1-rel,vlerp([g*0.12,-1,-0.1],[g*0.7,-0.8,-0.4],1-rel)); }
        const hold=t<0.57?1:0; I.pins.work={ layer:'load', workZ:Z, requested:K.workReq, clamped:K.workReq!==Z, mid:mid.map(x=>+x.toFixed(4)), hold, holding:hold>0.5,
          phase:t<0.55?'set':t<0.57?'release':'clear', progress:+t.toFixed(3) }; },
      toss(u,I,K){ const D=K.D, Z=K.workZ, rel=CONTRACTREL(K), I0=idleAt(K,0,0); copyIntent(I,I0);
        const TK=[[0,[0,0.24,0.82]],[0.20,[0,0.07,0.65]],[0.40,[0,0.30,0.94]],[0.52,[0,0.42,1.08]],[0.70,[0,0.44,1.10]],[0.875,[0,0.38,0.98]]];
        /* the heave is one continuous throw: linear through its keys, eased only into the backswing and out of the follow-through */
        let mid=K.bp(TK[TK.length-1][1]); for(let i=0;i+1<TK.length;i++){ const a=TK[i], b=TK[i+1]; if(u<=b[0]){ let f=(u-a[0])/(b[0]-a[0]); if(i===0||i===TK.length-2) f=sm(f); mid=vlerp(K.bp(a[1]),K.bp(b[1]),f); break; } }
        const wind=seg(u,0.04,0.22)*(1-seg(u,0.24,0.46)), heave=seg(u,0.26,0.56);
        I.pelvis.p=[I0.pelvis.p[0],-0.03*wind+0.04*heave,I0.pelvis.p[2]-0.08*wind]; I.spine.pitch+=14*wind-5*heave; I.chest.pitch+=6*wind-4*heave; I.head.pitch+=-6*heave+4;
        const rp=12*heave*(1-seg(u,0.7,0.875)); I.legs.L={ p:K.ft(-0.11,0.10), yaw:-10 }; I.legs.R={ p:footPivot(K.ft(0.11,-0.05),rp,D.ankle,D.footK), yaw:12, pitch:rp };
        for(const s of ['L','R']){ const g=G(s); I.arms[s]={ w:vadd(mid,[g*0.24,0,0]), el:[g*0.7,-0.8,-0.4] }; }
        const fired=u>=rel-1e-9;
        I.pins.work={ layer:'load', workZ:Z, requested:K.workReq, clamped:K.workReq!==Z, mid:mid.map(x=>+x.toFixed(4)), hold:fired?0:1, holding:!fired, phase:u<0.22?'wind':!fired?'heave':'follow', release:rel, fired, out:[0,0.8682,0.4962] };
        if(heave>0.1 && !fired) face(I,'open','knit','grit'); else if(fired) face(I,'open','up','open'); },
      /* ---- off deck ---- */
      swim(u,I,K){ const D=K.D, Pz=0.40+((K.B.gm ? K.B.gm.skirt : K.B.b.garment==='skirt')?0.05:0);
        /* centred on the body's length: the Fisher's pelvis sits 0.02 forward; a longer or shorter build keeps its midpoint there */
        I.pelvis.p=[0,0.161+(D.pelvisZ-(D.headZ+D.crown[2]-D.pelvisZ))/2,Pz+0.012*sn(u,0.1)]; I.pelvis.pitch=80; I.spine.pitch=-10; I.chest.pitch=-8; I.neck.pitch=-34; I.head.pitch=-26;
        const h=keys(u,[[0,[0.12,0.10,0.60]],[0.12,[0.14,0.10,0.60]],[0.28,[0.40,0.12,0.36]],[0.40,[0.16,0.24,0.16]],[0.52,[0.08,0.20,0.34]],[0.68,[0.11,0.12,0.58]],[1,[0.12,0.10,0.60]]]);
        const f=keys(u,[[0,[0.09,-0.02,-0.51]],[0.40,[0.09,-0.02,-0.51]],[0.55,[0.16,-0.12,-0.30]],[0.66,[0.26,-0.02,-0.44]],[0.76,[0.10,-0.01,-0.51]],[1,[0.09,-0.02,-0.51]]]);
        const point=lerp(65,10,seg(u,0.45,0.6)*(1-seg(u,0.72,0.82)));
        for(const s of ['L','R']){ const g=G(s); const ka=Math.min(1,K.k.a), kl=Math.min(1,K.k.l); /* a build longer than the Fisher glides with its stroke tucked, so the 64 px cell still holds it at E and W */
          I.arms[s]={ c:[g*h[0]*ka,h[1]*ka,h[2]*ka], el:[g*0.6,-0.3,-0.8] }; I.legs[s]={ P:[g*f[0]*Math.min(1,K.k.f),f[1]*kl,f[2]*kl], rel:'shin', pitch:point, yaw:0 }; }
        I.plant={ L:false, R:false };
        const surge=clamp01(0.15+0.85*seg(u,0.62,0.76)*(1-seg(u,0.85,1.0))), kick=seg(u,0.55,0.66)*(1-seg(u,0.66,0.78));
        I.meta.water={ waterZ:+(Pz+0.09).toFixed(4), waterRow:Math.round(82-(Pz+0.09)*Math.cos(40*DEG)*32), phase:u<0.12?'glide':u<0.40?'pull':u<0.55?'recover':u<0.76?'kick':'glide',
          surge:+surge.toFixed(3), kick:+kick.toFixed(3), bob:null, speed:+(0.18+0.42*surge).toFixed(3) };
        I.meta.speed=0.24; if(u>=0.40 && u<0.55) face(I,'open','flat','open'); },
      tread(u,I,K){ const D=K.D, bob=0.016*sn(u);
        I.pelvis.p=[0,0,D.pelvisZ-0.06+bob]; I.spine.pitch=5; I.chest.pitch=2; I.head.pitch=-6; I.neck.pitch=-2;
        for(const s of ['L','R']){ const g=G(s), th=TAU*(u+(s==='L'?0:0.5));
          I.legs[s]={ P:[g*(0.15+0.05*Math.cos(th))*K.k.f, (0.16+0.07*Math.sin(th))*K.k.l, -0.36*K.k.l], rel:'shin', pitch:30, splay:0.7 };
          I.arms[s]={ c:[g*(0.40+0.07*sn(u,g*0.25))*K.k.a, (0.16+0.07*cs(u))*K.k.a, (D.shZ-D.chestZ)-0.12*K.k.a], el:[g*0.5,-0.8,-0.6] }; }
        I.plant={ L:false, R:false }; I.meta.water={ bob }; I.meta.speed=0; },
      sleep(u,I,K){ const D=K.D, bz=K.bedZ, br=0.5-0.5*Math.cos(TAU*u);
        I.pelvis.yaw=180; I.pelvis.pitch=-90; I.pelvis.p=[0,-0.18,bz+0.105];
        I.spine.pitch=-2-1.2*br; I.chest.pitch=-0.8*br; I.neck.pitch=18; I.head.pitch=10; I.head.roll=9;
        I.legs.L={ P:[-0.105*K.k.f,0.035,-0.515*K.k.l], rel:'pelvis', yaw:-22 }; I.legs.R={ P:[0.095*K.k.f,0.025,-0.515*K.k.l], rel:'pelvis', yaw:18 };
        I.arms.R={ c:[0.05,0.17*K.k.a,0.02], el:[0.6,-0.2,-0.7] }; I.arms.L={ c:[-0.25*K.k.x,0.03,-0.28*K.k.a], el:[-0.2,-1,0] };
        I.plant={ L:false, R:false }; face(I,'shut','flat','flat'); I.pins.bed={ bedZ:bz, breath:+br.toFixed(3), lid:1 }; },
      drive(u,I,K){ const dv=K.drive, j=0.003*Math.sin(2*TAU*u), steer=6*sn(u);
        I.pelvis.p=[0,-0.02,dv.seatZ+0.10+j]; I.pelvis.pitch=-12; I.spine.pitch=6; I.chest.pitch=3; I.neck.pitch=-2; I.head.pitch=2;
        I.legs.L={ p:[-0.10,0.28,0.10], yaw:-4, pitch:-12 }; I.legs.R={ p:[0.10,0.29,0.105+0.004*Math.max(0,sn(u))], yaw:4, pitch:-12 };
        const c=[0,dv.wheelY,dv.wheelZ], r=0.17, tl=28*DEG, e1=[1,0,0], e2=[0,-Math.sin(tl),Math.cos(tl)];
        for(const s of ['L','R']){ const ph=((s==='L'?145:35)-steer)*DEG; I.arms[s]={ w:vadd(c,vadd(vmul(e1,r*Math.cos(ph)),vmul(e2,r*Math.sin(ph)))), el:[G(s)*0.8,-0.6,-0.6] }; }
        I.pins.wheel={ seatZ:dv.seatZ, wheelZ:dv.wheelZ, wheelY:dv.wheelY, clamped:!!dv.clamped, steer:+(steer*DEG).toFixed(4), judder:+(j/0.003).toFixed(3), wheelMid:c, rimRadius:r, tiltDeg:28 }; },
      /* ---- the hand-over (tool continuity law): home by 0.62, the hand opens at 0.72 ---- */
      reach(u,I,K){ const D=K.D, R=K.reach, I0=idleAt(K,0,0); copyIntent(I,I0);
        const arrive=sm(clamp01(u/R.arrive)), released=u>=R.release-1e-9, up=Math.pow(clamp01((u-R.release)/(1-R.release)),0.55), low=clamp01((0.62-R.lift)/0.62), dip=arrive*(1-up);
        I.pelvis.p=[I0.pelvis.p[0]+0.01*dip, -0.10*dip*low, I0.pelvis.p[2]-0.365*K.k.l*dip*low]; I.pelvis.pitch=14*dip*low;
        I.spine.pitch+=30*dip*low+6*dip*(1-low); I.chest.pitch+=16*dip*low; I.neck.pitch+=-12*dip*low; I.head.pitch+=8*dip;
        for(const s of ['L','R']){ const g=G(s); I.legs[s]={ p:vlerp(I0.legs[s].p,K.ft(g*(s==='L'?0.12:0.11), g*-0.06),dip*low), yaw:lerp(I0.legs[s].yaw,g*14,dip*low), splay:lerp(0.10,0.4,dip*low) }; }
        const hold0=K.bp([0.166,0.15,0.62]), gp=vlerp(hold0,R.want,arrive);
        /* after the release the hand is free: it rides the chest back to its hang, so it never asks the arm for
           more than it has while the body stands up */
        let cFree=null; if(released){ const Bf=bodyOf(I,K.B), sh=[K.D.shX,0,K.D.shZ-K.D.chestZ], rr=0.46*K.k.a; let c0=mTV(Bf.Wc.R, vsub(vadd(R.want,[0.02,-0.03,0.06]), Bf.Wc.p));
          const dv=vsub(c0,sh), dl=vlen(dv); if(dl>rr) c0=vadd(sh, vmul(dv,rr/dl)); cFree=vlerp(c0, I0.arms.R.c, up); }
        I.arms.R = released ? { c:cFree, el:vlerp([0.8,-0.8,-0.5],[0.12,-1,-0.1],up) } : { w:gp, el:[0.8,-0.8,-0.5] };
        I.arms.L={ c:I0.arms.L.c.slice(), w:[-0.13,0.20,0.40], k:0.9*dip*low, el:vlerp([-0.12,-1,-0.1],[-0.6,-1,-0.2],dip*low) };
        I.tool=released?null:{ held:true, kind:'rest', pitch:lerp(-8,-2,arrive), yaw:12, bend:0, len:D.rodLen, advisory:true };
        I.pins.rest={ lift:R.lift, requested:R.requested, clamped:R.clamped, gripRise:R.gripRise, restZ:+(R.lift+R.gripRise).toFixed(4), rest:R.rest, hand:'R', gripped:!released,
          arrive:R.arrive, release:R.release, phase:u<R.arrive-1e-9?'reach':!released?'home':'rise', crouch:+(dip*low).toFixed(3), want:R.want.map(x=>+x.toFixed(4)) };
        I.meta.handoff={ from:'idle f0, rod at the side', to:'idle f0' }; },
      /* ---- the saddle family ---- */
      astride(u,I,K){ saddlePose(u,I,K,false); },
      astrideStand(u,I,K){ saddlePose(u,I,K,true); },
      mountUp:mount, mountDown:mount, mountCab:mount, mountCabDown:mount
    };
    function CONTRACTREL(K){ return (K.o && K.o.release!=null && isFinite(+K.o.release)) ? +K.o.release : L.CONTRACT.tossRelease; }

    /* ================= carry stances (after the clip; tool clips never take one) ================= */
    const carryPin=(I,mode)=>{ I.pins.carry={ mode, swingL:+(I.swing.L||0).toFixed(3), swingR:+(I.swing.R||0).toFixed(3), swingMid:+(I.swing.mid||0).toFixed(3) }; };
    const CARRY_STYLE = {
      buckets(u,I,K){ const D=K.D, a=K.anim, A=a==='run'?15:a==='walk'?9:1.6;
        for(const s of ['L','R']){ const g=G(s); const x=Math.max(D.shX+0.05,D.hangX+0.034), arm=D.upper+D.fore+D.palmLen-0.008, dz=Math.min(0.468*K.k.a, Math.sqrt(Math.max(0.01, arm*arm-(x-D.shX)*(x-D.shX)-0.000144)));
          I.arms[s]={ c:[g*x, 0.012, (D.shZ-D.chestZ)-dz], el:[g*0.3,-1,0] }; }
        I.swing.R=A*Math.sin(TAU*u-0.9); I.swing.L=A*Math.sin(TAU*(u+0.5)-0.9); I.chest.pitch=(I.chest.pitch||0)-2; I.neck.pitch=(I.neck.pitch||0)+1.5; carryPin(I,'buckets'); },
      tray(u,I,K){ const D=K.D; for(const s of ['L','R']){ const g=G(s); I.arms[s]={ c:[g*0.15,0.30*K.k.a,(D.shZ-D.chestZ)-0.30*K.k.a], el:[g*0.9,-0.5,-0.5] }; }
        I.swing.mid=K.anim==='idle'?0.8*sn(u):2.0*Math.cos(2*TAU*u); carryPin(I,'tray'); },
      pot(u,I,K){ const D=K.D; for(const s of ['L','R']){ const g=G(s); I.arms[s]={ c:[g*0.29,0.20*K.k.a,(D.shZ-D.chestZ)-0.34*K.k.a], el:[g*0.8,-0.8,-0.2] }; }
        I.swing.mid=(K.anim==='idle'?0.8*sn(u):2.0*Math.cos(2*TAU*u))*0.6; I.chest.pitch=(I.chest.pitch||0)-4; carryPin(I,'pot'); },
      helm(u,I,K){ const D=K.D;
        if(K.anim==='idle'){ I.legs.L.p=K.ft(-0.15,0.02); I.legs.R.p=K.ft(0.15,-0.02); I.legs.L.yaw=-12; I.legs.R.yaw=12; I.pelvis.p[2]-=0.012; }
        const st=K.anim==='idle'?3*sn(u):1.5*sn(2*u);
        for(const s of ['L','R']){ const g=G(s), ph=((s==='L'?150:30)-st)*DEG; I.arms[s]={ c:[0.17*Math.cos(ph),0.34*K.k.a,(D.shZ-D.chestZ)-0.22*K.k.a+0.10*Math.sin(ph)], el:[g*0.8,-0.6,-0.5] }; }
        carryPin(I,'helm'); I.pins.carry.steer=+(st*DEG).toFixed(4); },
      oars(u,I,K){ const D=K.D, hold=(s,c,el)=>{ I.arms[s]={ w:inReach(I,K,s,fkp(bodyOf(stooped(I,K),K.B).Wc,c)), el }; };   // 9.2: the looms held in the arm's reach (narrow children)
        if(K.anim==='idle'){ for(const s of ['L','R']){ const g=G(s); hold(s,[g*0.25,(0.34+0.02*sn(u))*K.k.a,(D.shZ-D.chestZ)+(-0.26+0.01*cs(u))*K.k.a],[g*0.7,-0.8,-0.3]); } carryPin(I,'oars'); return; }
        const ph=frac(2*u), drive=ph<0.55?sm(ph/0.55):1-sm((ph-0.55)/0.45), recov=ph>=0.55;
        I.legs.L={ p:K.ft(-0.13,0.12), yaw:-10 }; I.legs.R={ p:K.ft(0.12,-0.12), yaw:14 }; I.plant={ L:true, R:true };
        I.pelvis.p=[0,-0.03*drive,D.pelvisZ-(0.04+0.015*drive)*K.k.l]; I.pelvis.yaw=0; I.pelvis.roll=0;
        I.spine.pitch=14-18*drive; I.chest.pitch=6-6*drive; I.spine.yaw=0; I.chest.yaw=0; I.spine.roll=0; I.neck.yaw=0; I.head.yaw=0; I.head.pitch=4+8*drive;
        for(const s of ['L','R']){ const g=G(s); hold(s,[g*0.25, lerp(0.38,0.06,drive)*K.k.a, (D.shZ-D.chestZ)+(-0.26+(recov?0.06*Math.sin(Math.PI*(ph-0.55)/0.45):0))*K.k.a],[g*0.7,-0.8,-0.3]); }
        I.meta.speed=null; carryPin(I,'oars'); I.pins.carry.stroke=+ph.toFixed(3); I.pins.carry.drive=+drive.toFixed(3); face(I,'open',drive>0.6?'knit':'flat','flat'); } };

    /* ================= post-solve pins (need the solved skeleton) ================= */
    function saddlePost(S,P,H){ const s=P.saddle; if(!s || (S.cd.anim!=='astride' && S.cd.anim!=='astrideStand')) return;
      const D=S.B.D, ix=S.B.sk.ix, arm=D.upper+D.fore+D.palmLen;
      const reachOf=(side)=>{ const need=H.vdist(S.W[ix['shoulder_'+side]].p, side==='L'?s.gripL:s.gripR); return { need_m:+need.toFixed(4), have_m:+arm.toFixed(4), over_mm:Math.max(0,Math.round((need-arm)*1000)) }; };
      const oL=reachOf('L'), oR=reachOf('R'), gap=(side)=>Math.round(((S.err['foot'+side])||0)*1000);
      s.overreach={ L:oL, R:oR, worst_mm:Math.max(oL.over_mm,oR.over_mm), fits:Math.max(oL.over_mm,oR.over_mm)===0 };
      s.footgap={ L:{ short_mm:gap('L') }, R:{ short_mm:gap('R') }, worst_mm:Math.max(gap('L'),gap('R')), onPegs:Math.max(gap('L'),gap('R'))<=6 }; }
    const POST = {
      swim(S,P){ const w=S.I.meta.water; P.water=Object.assign({}, w, { headClear:+(P.head[2]-w.waterZ).toFixed(3) }); },
      tread(S,P){ const ix=S.B.sk.ix, wz=+((S.W[ix.shoulder_L].p[2]+S.W[ix.shoulder_R].p[2])/2-0.05).toFixed(4);
        P.water={ waterZ:wz, waterRow:Math.round(82-wz*Math.cos(40*DEG)*32), phase:'tread', surge:0, kick:+(0.5+0.5*sn(S.u)).toFixed(3), bob:+S.I.meta.water.bob.toFixed(4), speed:0, headClear:+(P.head[2]-wz).toFixed(3) }; },
      sleep(S,P,H){ const d=H.vnorm(H.vsub(P.head,P.hip)); P.bed=Object.assign({}, S.I.pins.bed, { axis:d.map(x=>+x.toFixed(4)), handChest:P.handR, handSide:P.handL }); },
      reach(S,P,H){ const g=P.rest; if(!g) return; g.grip=P.handR; g.slip=+((S.err.handR)||0).toFixed(4); g.slipPx=+(g.slip*32*Math.cos(40*DEG)).toFixed(2); g.toWant=+H.vdist(P.handR,g.want).toFixed(4); },
      ladderDown(S,P){ P.ladder.rungL=[P.footL[0],P.footL[1],P.ladder.rungZ.L]; P.ladder.rungR=[P.footR[0],P.footR[1],P.ladder.rungZ.R]; },
      astride:saddlePost, astrideStand:saddlePost };
    return { POSES, CARRY_STYLE, POST };
  });
})(typeof globalThis!=='undefined' ? globalThis : window);

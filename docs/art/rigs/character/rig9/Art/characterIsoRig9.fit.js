/* Hidden Harbours — characterIsoRig9 WORLD FIT (characterIsoRig9.fit.js). Optional. Load after characterIsoRig9.js and
   characterIsoRig9.poses.js. Adds CharacterIso9.worldFit(build) and CharacterIso9.FIXTURES; reads the rig, changes nothing in it,
   so the rig and pose-library hashes every sidecar carries stay valid.

   COLLIDER
     collision_radius_m  the shoulder joint's distance from the centreline (shoulder_R bind.pos x)
     footprint_m         [2 x that, 2 x the toe's reach ahead of the ankle (0.180 m x footK)]
     On the Fisher these are exactly 0.20 and 0.40 x 0.36, the constants in every committed sidecar. `measured` adds the bind
     mesh's half-widths (whole mesh, and body only: no hair, hat, face marks, nose, ears or beard) for an engine that wants a
     more conservative collider.

   FIXTURES
     The world contracts the clips clamp to (CharacterIso9.CONTRACT), one at a time, every other contract at its default.
     A build FITS a fixture when every limb in every frame of the fixture's clips makes its target to within 5 mm, the golden
     suite's contact gate. For each fixture: the shortfall at the default (mm, and where), and the values of its parameter at
     which the build fits, scanned over the contract's own range with each edge refined by bisection (intervals in metres,
     rounded to the millimetre). When nothing in range fits, `best` is the value with the smallest shortfall.
       rail    board, boardDown (railZ). The carried variants share these legs and never grip the rail: they are checked at
               the default only.
       ladder  ladderDown (rung spacing).
       warp, bench, knife, load   hauler, bench, chop, lift + place + toss (workZ). The hauler's hands also work at the sheave, which
               sits a fixed 0.24 m across and 0.34 m out (ctxOf's default); `sheave` is the scale of that offset, toward the body,
               at which the build fits at the default height (and, when none does, at the height that comes closest).
       rest    reach, the tool hand-over (lift: the rest's height; named rests ground / stowV / stowH).
       wheel   drive. The feet reach a floor fixed in the cab, so the seat height is fitted first (feet only); the wheel is then
               fitted at that seat (hands only): the fitting point nearest the default wheel, on a 1 cm grid.
       saddle  astride, astrideStand. Three independent fits: peg height (feet), grip distance forward (hands), and a uniform
               scale of the whole default saddle about the pivot (a smaller machine; every limb).
       mount   mountUp, mountDown, mountCab, mountCabDown (9.2: fitted). A uniform scale of the default saddle, the idle spot beside it
               moved in with it (CharacterIso9.saddleOpts); a build fits when every limb makes its target, the seat carries the body
               before the standing foot leaves the ground (no seat gap), and that foot stays planted until then. The default saddle's
               seat (0.94 m) is higher than any body can sit down on from the ground. CONTRACT.saddleFit is the size the golden suite
               gates the saddle family at.
   9.2: the collider is the rig's CharacterIso9.colliderOf (and in every sidecar); the saddle scale is scanned from 0.30. */
(function (root) {
  'use strict';
  const C = root.CharacterIso9;
  if(!C || !C.posesLoaded) throw new Error('characterIsoRig9.fit.js: load characterIsoRig9.js and characterIsoRig9.poses.js first');
  const GATE = 0.005, K = C.CONTRACT, S0 = K.saddle;
  const mm = (m)=>Math.round(m*1000), r3 = (x)=>Math.round(x*1000)/1000;
  const LEGS = ['footL','footR'], ARMS = ['handL','handR'], ALL = LEGS.concat(ARMS);

  function shortOf(B, clips, opts, limbs){ let m=0, at=null, limb=null; limbs=limbs||ALL;
    for(const n of clips){ const cd=C.clipDef(n), A=C.ANIMS[cd.anim];
      for(let k=0;k<A.frames;k++){ const S=C.evalClip(n, C.uOf(cd.anim,k), B, opts);
        for(const l of limbs){ const v=S.err[l]||0; if(v>m){ m=v; at=n+' f'+k; limb=l; } } } }
    return { m, at, limb }; }
  /* the feasible set of one parameter over [lo, hi]: scanned at `step`, every edge refined by bisection */
  function feasible(f, lo, hi, step){ const xs=[]; for(let i=0;;i++){ const x=Math.min(hi, +(lo+i*step).toFixed(6)); xs.push(x); if(x>=hi) break; }
    const v=xs.map(f), out=[]; let best=0; for(let i=1;i<xs.length;i++) if(v[i]<v[best]-1e-12) best=i;
    const edge=(a,b)=>{ let p=a, q=b; for(let k=0;k<6;k++){ const m=(p+q)/2; if(f(m)<=GATE) p=m; else q=m; } return p; };
    for(let i=0;i<xs.length;i++){ if(v[i]>GATE) continue; let j=i; while(j+1<xs.length && v[j+1]<=GATE) j++;
      out.push([r3(i>0 ? edge(xs[i],xs[i-1]) : xs[i]), r3(j<xs.length-1 ? edge(xs[j],xs[j+1]) : xs[j])]); i=j; }
    return { fits:out, best:{ at:r3(xs[best]), short_mm:mm(v[best]) } }; }
  const within=(fits,x)=>fits.some(([a,b])=>x>=a-1e-9 && x<=b+1e-9);
  const nearest=(fits,x)=>{ let o=null, d=Infinity; for(const [a,b] of fits){ const y=x<a?a:x>b?b:x, e=Math.abs(y-x); if(e<d){ d=e; o=y; } } return o; };

  const FIXTURES = {
    rail:  { label:'boarding rail height', param:'railZ', def:K.railZ.def, range:[K.railZ.min,K.railZ.max], step:0.04, clips:['board','boardDown'],
             also:['board+buckets','board+tray','board+pot','boardDown+buckets','boardDown+tray','boardDown+pot'], opts:(v)=>({ railZ:v }) },
    ladder:{ label:'ladder rung spacing', param:'rung', def:K.rung.def, range:[K.rung.min,K.rung.max], step:0.02, clips:['ladderDown'], opts:(v)=>({ rung:v }) },
    warp:  { label:'hauler (warp) height', param:'workZ', def:K.workZ.hauler, range:[K.workZ.min,K.workZ.max], step:0.02, clips:['hauler'], opts:(v)=>({ workZ:v }), sheave:[0.24,0.34,0.26] },
    bench: { label:'work bench height', param:'workZ', def:K.workZ.bench, range:[K.workZ.min,K.workZ.max], step:0.02, clips:['bench'], opts:(v)=>({ workZ:v }) },
    knife: { label:'cutting surface height', param:'workZ', def:K.workZ.chop, range:[K.workZ.min,K.workZ.max], step:0.02, clips:['chop'], opts:(v)=>({ workZ:v }) },
    load:  { label:'lift, place and toss height', param:'workZ', def:K.workZ.lift, range:[K.workZ.min,K.workZ.max], step:0.02, clips:['lift','place','toss'], opts:(v)=>({ workZ:v }) },
    rest:  { label:'tool rest height (hand-over)', param:'lift', def:K.reach.rests.ground, range:[0,1.25], step:0.02, clips:['reach'], opts:(v)=>({ lift:v }), named:K.reach.rests },
    wheel: { label:'cab seat and wheel', params:['seatZ','wheelZ','wheelY'], def:K.drive, range:{ seatZ:[0.25,0.70], wheelZ:[0.45,0.95], wheelY:[0.20,0.45] }, clips:['drive'] },
    saddle:{ label:'saddle: seat, grips and pegs', params:['pegZ','gripY','scale'], def:S0, range:{ pegZ:[0.42,0.90], gripY:[-0.10,0.42], scale:[0.30,1.00] }, clips:['astride','astrideStand'] },
    mount: { label:'mount and dismount', params:['scale'], def:S0, range:{ scale:[0.30,1.00] }, clips:['mountUp','mountDown','mountCab','mountCabDown'] } };

  const RIG_COLLIDER=C.colliderOf, colliderOf=(B)=>RIG_COLLIDER(B.key);
  function colliderOf91(B){ const D=B.D, EX={ hair:1, hat:1, hood:1, face:1, nose:1, ear:1, beard:1 }; let bx=0, by=0, hx=0, hy=0;
    for(const f of B.mesh.faces){ const ex=!!(f.group || EX[f.part]);
      for(const p of f.v){ const ax=Math.abs(p[0]), ay=Math.abs(p[1]); if(ax>bx) bx=ax; if(ay>by) by=ay; if(!ex){ if(ax>hx) hx=ax; if(ay>hy) hy=ay; } } }
    return { collision_radius_m:r3(D.shX), footprint_m:[r3(2*D.shX), r3(2*0.18*D.footK)], from:{ shoulder_half_span_m:r3(D.shX), toe_reach_m:r3(0.18*D.footK) },
      measured:{ bind_half_width_m:[r3(bx),r3(by)], body_half_width_m:[r3(hx),r3(hy)] } }; }

  function atDefault(B, clips){ const d=shortOf(B, clips); return { short_mm:mm(d.m), fits:d.m<=GATE, at:d.m>GATE ? d.at+' '+d.limb : null }; }
  const saddleOf=(o)=>({ saddle:Object.assign({ seat:S0.seat.slice(), gripL:S0.gripL.slice(), gripR:S0.gripR.slice(), pegL:S0.pegL.slice(), pegR:S0.pegR.slice() }, o||{}) });
  const scaledSaddle=(s)=>C.saddleOpts(s);

  function fitScalar(B, F){ const row=Object.assign({ param:F.param, default:F.def }, atDefault(B, F.clips.concat(F.also||[])));
    const s=feasible((v)=>shortOf(B, F.clips, F.opts(v)).m, F.range[0], F.range[1], F.step);
    row.fit=s.fits; if(!s.fits.length) row.best=s.best;
    if(F.named){ row.named={}; for(const [k,v] of Object.entries(F.named)) row.named[k]=within(s.fits,v); }
    if(F.sheave){ const sv=F.sheave, at=(z)=>feasible((k)=>shortOf(B, F.clips, { workZ:z, sheave:[sv[0]*k, sv[1]*k, z+sv[2]] }).m, 0.40, 1.00, 0.02);
      const a=at(F.def); row.sheave={ workZ:F.def, scale_fit:a.fits }; if(!a.fits.length){ row.sheave.best=a.best;
        const z2 = s.fits.length ? r3(nearest(s.fits, F.def)) : s.best.at, b2=at(z2); row.sheave.closest={ workZ:z2, scale_fit:b2.fits }; if(!b2.fits.length) row.sheave.closest.best=b2.best; } }
    return row; }

  function fitWheel(B, F){ const d=F.def, R=F.range, row=Object.assign({ params:F.params, default:{ seatZ:d.seatZ, wheelZ:d.wheelZ, wheelY:d.wheelY } }, atDefault(B, F.clips));
    row.feet_mm=mm(shortOf(B, F.clips, null, LEGS).m); row.hands_mm=mm(shortOf(B, F.clips, null, ARMS).m);
    const seat=feasible((v)=>shortOf(B, F.clips, { seatZ:v }, LEGS).m, R.seatZ[0], R.seatZ[1], 0.02);
    row.seatZ_fit=seat.fits; if(!seat.fits.length) row.seatZ_best=seat.best;
    const sz = seat.fits.length ? r3(nearest(seat.fits, d.seatZ)) : seat.best.at; row.seatZ_used=sz;
    const hands=(wz,wy)=>shortOf(B, F.clips, { seatZ:sz, wheelZ:wz, wheelY:wy }, ARMS).m, dist=(wz,wy)=>Math.hypot(wz-d.wheelZ, wy-d.wheelY);
    let h0=hands(d.wheelZ, d.wheelY);
    if(h0<=GATE){ row.wheel={ wheelZ:d.wheelZ, wheelY:d.wheelY, short_mm:mm(h0), default:true }; return row; }
    const grid=(z0,z1,y0,y1,st)=>{ const o=[]; for(let z=z0; z<=z1+1e-9; z+=st) for(let y=y0; y<=y1+1e-9; y+=st){ const wz=r3(z), wy=r3(y); o.push({ wz, wy, v:hands(wz,wy) }); } return o; };
    const pick=(pts)=>{ const ok=pts.filter(p=>p.v<=GATE); if(ok.length) return ok.reduce((a,b)=>dist(b.wz,b.wy)<dist(a.wz,a.wy)-1e-12 ? b : a);
      return pts.reduce((a,b)=>b.v<a.v-1e-12 ? b : a); };
    const c=pick(grid(R.wheelZ[0],R.wheelZ[1],R.wheelY[0],R.wheelY[1],0.05));
    const f=pick(grid(Math.max(R.wheelZ[0],c.wz-0.05), Math.min(R.wheelZ[1],c.wz+0.05), Math.max(R.wheelY[0],c.wy-0.05), Math.min(R.wheelY[1],c.wy+0.05), 0.01));
    row.wheel={ wheelZ:f.wz, wheelY:f.wy, short_mm:mm(f.v), default:false, fits:f.v<=GATE }; return row; }

  function fitSaddle(B, F){ const row=Object.assign({ params:F.params, default:{ seat:S0.seat, gripL:S0.gripL, gripR:S0.gripR, pegL:S0.pegL, pegR:S0.pegR } }, atDefault(B, F.clips));
    row.feet_mm=mm(shortOf(B, F.clips, null, LEGS).m); row.hands_mm=mm(shortOf(B, F.clips, null, ARMS).m); const R=F.range;
    const peg=feasible((z)=>shortOf(B, F.clips, saddleOf({ pegL:[S0.pegL[0],S0.pegL[1],z], pegR:[S0.pegR[0],S0.pegR[1],z] }), LEGS).m, R.pegZ[0], R.pegZ[1], 0.02);
    const grip=feasible((y)=>shortOf(B, F.clips, saddleOf({ gripL:[S0.gripL[0],y,S0.gripL[2]], gripR:[S0.gripR[0],y,S0.gripR[2]] }), ARMS).m, R.gripY[0], R.gripY[1], 0.02);
    const sc=feasible((s)=>shortOf(B, F.clips, scaledSaddle(s)).m, R.scale[0], R.scale[1], 0.02);
    row.pegZ_fit=peg.fits; if(!peg.fits.length) row.pegZ_best=peg.best;
    row.gripY_fit=grip.fits; if(!grip.fits.length) row.gripY_best=grip.best;
    row.scale_fit=sc.fits; if(!sc.fits.length) row.scale_best=sc.best;
    return row; }

  /* a mount fits at a saddle scale when every limb makes its target, there is no seat gap and the standing foot is planted from the moment the
     swing foot leaves the ground until the seat carries the body; the value is the limb shortfall, pushed over the gate when either rule fails */
  function mountShort(B, F, s){ const o=C.saddleOpts(s); let m=0, bad=false;
    for(const n of F.clips){ const A=C.ANIMS[n]; for(let k=0;k<A.frames;k++){ const S=C.evalClip(n, C.uOf(n,k), B, o); for(const v of Object.values(S.err)) if(v>m) m=v;
      const ph=S.I.pins.saddle.phase; if(ph.seatGap_m>0.001 || (ph.swing>0 && ph.seated<1 && !(S.I.plant[ph.standSide] && ph.standFoot==='ground'))) bad=true; } }
    return bad ? Math.max(m, 1) : m; }
  function fitMount(B, F){ const row=Object.assign({ params:F.params }, atDefault(B, F.clips)); row.clips={}; for(const n of F.clips) row.clips[n]=mm(shortOf(B,[n]).m);
    const sc=feasible((s)=>mountShort(B, F, s), F.range.scale[0], F.range.scale[1], 0.02); row.scale_fit=sc.fits; if(!sc.fits.length) row.scale_best=sc.best;
    row.saddleFit={ scale:C.CONTRACT.saddleFit.scale, fits:mountShort(B, F, C.CONTRACT.saddleFit.scale)<=GATE }; return row; }

  function worldFit(build){ const B=C.buildOf(build), o={ key:B.key, collider:colliderOf(B), fixtures:{} };
    for(const [id,F] of Object.entries(FIXTURES)) o.fixtures[id] = F.param ? fitScalar(B,F) : id==='wheel' ? fitWheel(B,F) : id==='saddle' ? fitSaddle(B,F) : fitMount(B,F);
    return o; }

  C.FIXTURES = FIXTURES; C.FIT_GATE = GATE; C.worldFit = worldFit; C.fitLoaded = true;
})(typeof globalThis!=='undefined' ? globalThis : window);

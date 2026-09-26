/* Hidden Harbours — characterIsoRig9 GOLDEN CHECKS (per build: the page runs them for every preset and for a creator build). Each check is a generator: the page steps it in slices so it
   stays live; node / a sandbox can run it to the end. A check returns { pass, value, detail, rows? }.
   `gate:false` checks report and never fail the run (they measure a contract the rig cannot meet by itself).
   9.2: hair, gaze and look are new; contacts gates the saddle family at CONTRACT.saddleFit; budget reports (gate:false). */
(function (root) {
  'use strict';
  const C8 = root.CharacterIso9;
  if(!C8){ console.warn('characterIsoRig9.checks.js: load Art/characterIsoRig9.js first'); return; }
  const sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]], dist=(a,b)=>Math.hypot(a[0]-b[0],a[1]-b[1],a[2]-b[2]);
  const mV=(R,v)=>[R[0]*v[0]+R[3]*v[1]+R[6]*v[2], R[1]*v[0]+R[4]*v[1]+R[7]*v[2], R[2]*v[0]+R[5]*v[1]+R[8]*v[2]];
  const mM=(A,B)=>{ const o=new Array(9); for(let c=0;c<3;c++){ const v=mV(A,[B[c*3],B[c*3+1],B[c*3+2]]); o[c*3]=v[0]; o[c*3+1]=v[1]; o[c*3+2]=v[2]; } return o; };
  const mdiff=(A,B)=>{ let m=0; for(let i=0;i<9;i++) m=Math.max(m,Math.abs(A[i]-B[i])); return m; };
  const e=(x)=>x===0 ? '0' : x.toExponential(2);
  function frames(){ const o=[]; for(const n of C8.clipNames()){ const cd=C8.clipDef(n), A=C8.ANIMS[cd.anim]; for(let k=0;k<A.frames;k++) o.push({ n, cd, k, u:C8.uOf(cd.anim,k) }); } return o; }
  /* the bind mesh skinned by a set of world frames — every face, groups included */
  function skin(B, W){ const M=W.map((w,i)=>({ R:w.R, t:sub(w.p, mV(w.R, B.sk.bones[i].p)) })), out=[];
    for(const f of B.mesh.faces) for(let k=0;k<f.v.length;k++){ const sk=f.bone[k], p=f.v[k];
      if(sk.length===1){ const m=M[sk[0][0]]; out.push(add(mV(m.R,p),m.t)); continue; }
      const o=[0,0,0]; for(const [bi,w] of sk){ const m=M[bi], q=add(mV(m.R,p),m.t); o[0]+=q[0]*w; o[1]+=q[1]*w; o[2]+=q[2]*w; } out.push(o); }
    return out; }
  const _parts=new WeakMap(); const PARTS=(B)=>{ let p=_parts.get(B); if(!p){ p=[]; for(const f of B.mesh.faces) for(const v of f.v) p.push(f.part); _parts.set(B,p); } return p; };
  const maxDelta=(A,B,shift)=>{ let m=0; for(let i=0;i<A.length;i++){ const b=shift?add(B[i],shift):B[i]; m=Math.max(m, dist(A[i],b)); } return m; };
  const worldsFromLocals=(B, L)=>{ const W=new Array(L.length); L.forEach((l,i)=>{ const par=B.sk.bones[i].parent; W[i]= par<0 ? { p:l.p.slice(), R:l.R.slice() } : { p:add(W[par].p, mV(W[par].R,l.p)), R:mM(W[par].R,l.R) }; }); return W; };
  const TOOLS={ hold:1, cast:1, castBack:1, castRelease:1, bite:1, strike:1, reel:1, land:1 };
  const REPORT_ONLY={ astride:'pegs', astrideStand:'pegs', mountUp:'transition', mountDown:'transition', mountCab:'transition', mountCabDown:'transition' };
  /* world contracts sized for an adult (the rung, the default wheel and seat, the work bench) are reported, not gated, on a child or youth */
  const WORLD={ ladderDown:'rung', drive:'wheel', hauler:'workZ', bench:'workZ', chop:'workZ', lift:'workZ', place:'workZ', toss:'workZ', board:'railZ', boardDown:'railZ', reach:'lift' };
  const small=(B)=>{ const k=C8.kOf(B.D); return Math.min(k.a,k.l)<0.97; };
  const reportOnly=(B,anim)=>REPORT_ONLY[anim] || (small(B) && WORLD[anim] ? WORLD[anim]+' sized for the Fisher' : null);
  const CLOTH={ skirt:1, apron:1 };

  /* the skull's pixels placed on the head (the pixel's bind point) and sorted: skin where the hair is (the rig's hair region), or the skin
     that is meant to show (face and forehead, temple, ear, nape); a hair-coloured skull pixel is the painted scalp showing */
  const SKIN_M={ skin:1, skinD:1, stub:1 }, TOLH=0.0015;
  const nrm=(v)=>{ let x=0,y=0,z=0; for(let i=0;i<v.length;i++){ const a=v[i], c=v[(i+1)%v.length]; x+=(a[1]-c[1])*(a[2]+c[2]); y+=(a[2]-c[2])*(a[0]+c[0]); z+=(a[0]-c[0])*(a[1]+c[1]); } const m=Math.hypot(x,y,z)||1; return [x/m,y/m,z/m]; };
  const skullSide=(f)=>{ const n=nrm(f.v), h=Math.hypot(n[0],n[1]); if(h<0.05) return n[2]>0?'top':'bottom'; const ax=Math.abs(n[0])/h, ay=n[1]/h;
    return ax>0.92388 ? 'side' : ay>0.92388 ? 'front' : ay<-0.92388 ? 'back' : ay>0 ? 'corner' : 'back'; };
  function bindPoint(R,B,fi,x,y){ const f=R.faces[fi], src=B.mesh.faces[fi], P=f.v.map(p=>C8.proj(p,R.cam)); if(R.snap && f.head) for(const q of P){ q.sx+=R.snap[0]; q.sy+=R.snap[1]; }
    const px=x+0.5, py=y+0.5;
    for(let t=1;t+1<P.length;t++){ const a=P[0], b=P[t], c=P[t+1], area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy); if(Math.abs(area)<1e-9) continue;
      const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area, w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area, w2=1-w0-w1; if(w0<-1e-6||w1<-1e-6||w2<-1e-6) continue;
      const A=src.v[0], Bv=src.v[t], Cv=src.v[t+1]; return [0,1,2].map(i=>w0*A[i]+w1*Bv[i]+w2*Cv[i]); }
    return null; }
  const strictlyHair=(B,HL,side,p)=>{ if(!HL) return false; const zc=C8.zcOf(B,p[2]), tz=TOLH/B.D.zs; for(const dy of [-TOLH,TOLH]) for(const dz of [-tz,tz]) if(!C8.inHair(HL,side,p[1]+dy,zc+dz)) return false; return true; };
  function skullCount(R,B,HL,cache){ const nb=B.mesh.groups['eyes.open'][0], o={ hair:0, face:0, temple:0, ear:0, nape:0, scalp:0 };
    for(let y=0;y<R.H;y++) for(let x=0;x<R.W;x++){ const fi=R.face[y*R.W+x]; if(fi<0 || fi>=nb) continue; const f=R.faces[fi]; if(f.part!=='head') continue;
      if(!SKIN_M[f.mat]){ o.scalp++; continue; } const side=cache[fi]||(cache[fi]=skullSide(B.mesh.faces[fi])), p=bindPoint(R,B,fi,x,y); if(!p) continue;
      if(strictlyHair(B,HL,side,p)) o.hair++; else if(side==='side') o[p[1]>C8.EAR.front?'temple':p[1]>=C8.EAR.back?'ear':'nape']++; else if(side==='back'||side==='bottom') o.nape++; else o.face++; }
    return o; }
  /* see-through at the neck: uncovered pixels shut in by the neck, a collar or the hood and by nothing below the neck */
  const NECKP={ neck:1, collar:1, hood:1 };
  function neckGaps(R){ const W=R.W, H=R.H, cov=(i)=>R.face[i]>=0, seen=new Uint8Array(W*H), st=[]; for(let x=0;x<W;x++) st.push(x,(H-1)*W+x); for(let y=0;y<H;y++) st.push(y*W,y*W+W-1);
    while(st.length){ const i=st.pop(); if(seen[i]||cov(i)) continue; seen[i]=1; const x=i%W, y=(i-x)/W; if(x>0) st.push(i-1); if(x<W-1) st.push(i+1); if(y>0) st.push(i-W); if(y<H-1) st.push(i+W); }
    const cls=(j)=>{ const F=R.faces[R.face[j]]; return NECKP[F.part] ? 1 : F.head ? 2 : 4; }, comp=new Uint8Array(W*H); let n=0;
    for(let i=0;i<W*H;i++){ if(cov(i)||seen[i]||comp[i]) continue; const q=[i]; let cells=0, mask=0; comp[i]=1;
      while(q.length){ const j=q.pop(); cells++; const x=j%W, y=(j-x)/W; for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const X=x+dx, Y=y+dy; if(X<0||Y<0||X>=W||Y>=H) continue; const k=Y*W+X; if(cov(k)) mask|=cls(k); else if(!comp[k]){ comp[k]=1; q.push(k); } } }
      if((mask&1) && !(mask&4)) n+=cells; }
    return n; }
  const partPx=(R,part)=>{ let n=0; for(const f of R.face) if(f>=0 && R.faces[f].part===part) n++; return n; };
  /* the dark of the eye (ink and iris pixels of the eye groups): the pupil, and on the narrow eye its lash line */
  const irisOf=(R)=>{ const o=[]; for(let i=0;i<R.face.length;i++){ const fi=R.face[i]; if(fi>=0 && R.faces[fi].group && R.faces[fi].group.slice(0,5)==='eyes.' && (R.faces[fi].mat==='iris' || R.faces[fi].mat==='ink')) o.push(i); } return o; };
  const LIMITS=[[-60,0],[60,0],[0,-15],[0,20],[-60,20],[60,20],[-60,-15],[60,-15]];

  const CHECKS = [
    { id:'timing', title:'Clip table equals the shipped sheets — frames, ms, one-shot, settle', *run(){
      const tok=C8.ANIMS_SHIPPED.split(' '), bad=[]; let n=0;
      for(let i=0;i<tok.length;i+=2){ const name=tok[i], m=/^(\d+)\/(\d+)(!?)(s?)$/.exec(tok[i+1]), A=C8.ANIMS[name]; n++;
        if(!A || +m[1]!==A.frames || +m[2]!==A.ms || !!m[3]!==!!A.oneShot || !!m[4]!==!!A.settle) bad.push(name); }
      const extra=Object.keys(C8.ANIMS).filter(k=>tok.indexOf(k)<0);
      const C6=root.CharacterIso6; let vs6='rig 6 not loaded — compared against the shipped string';
      if(C6 && C6.ANIMS){ const d=Object.keys(C6.ANIMS).filter(k=>{ const a=C6.ANIMS[k], b=C8.ANIMS[k]; return !b || a.frames!==b.frames || a.ms!==b.ms || !!a.oneShot!==!!b.oneShot || !!a.settle!==!!b.settle; }); vs6 = d.length ? 'differs from CharacterIso6 on '+d.join(', ') : 'identical to CharacterIso6.ANIMS'; if(d.length) bad.push(...d); }
      const stances=Object.entries(C8.CARRY_CLIPS).map(([k,v])=>k+' rides '+v.anim+' ('+C8.ANIMS[v.anim].frames+'f/'+C8.ANIMS[v.anim].ms+'ms)');
      return { pass:!bad.length && !extra.length, value:(n-bad.length)+' / '+n, detail:(bad.length?'mismatch: '+bad.join(', ')+'. ':'')+vs6+'. '+stances.join(' · ') }; } },
    { id:'skeleton', title:'Skeleton: 28 bones, parents first, identity bind, every deform length constant in every frame', *run(B){
      const S=B.sk, ok1=S.bones.every((b,i)=>b.parent<i), deform=S.bones.filter(b=>b.kind==='deform');
      let worst=0, at='', i=0;
      for(const F of frames()){ const R=C8.evalClip(F.n,F.u,B);
        for(const b of deform){ const j=S.ix[b.id], p=b.parent; if(S.bones[p].kind!=='deform') continue;
          const d=Math.abs(dist(R.W[j].p,R.W[p].p)-Math.hypot(...S.rest[j])); if(d>worst){ worst=d; at=F.n+' f'+F.k+' '+b.id; } }
        if(++i%40===0) yield i; }
      return { pass:ok1 && worst<1e-9 && S.bones.length===28, value:e(worst)+' m', detail:S.bones.length+' bones ('+S.deform+' deform + root + '+(S.bones.length-S.deform-1)+' sockets). Largest change in any joint-to-joint length over '+i+' frames: '+e(worst)+' m'+(at?' ('+at+')':'')+'. No tip, cuff, hem or inseam bones.' }; } },
    { id:'skin', title:'Skin weights: every vertex sums to 1; the only blended ring is the waist', *run(B){
      let bad=0, blended=new Set(), verts=0;
      for(const f of B.mesh.faces) f.bone.forEach((sk,k)=>{ verts++; const s=sk.reduce((a,p)=>a+p[1],0); if(Math.abs(s-1)>1e-12) bad++; if(sk.length>1) blended.add(f.v[k].map(x=>x.toFixed(5)).join(',')); });
      const zs=[...blended].map(s=>+s.split(',')[2]); const ring=zs.every(z=>Math.abs(z-B.D.waistZ)<0.011);
      return { pass:!bad && ring, value:blended.size+' blended', detail:verts+' vertices, '+bad+' with weights that do not sum to 1. '+blended.size+' distinct blended positions (spine 0.5 / chest 0.5), all on this build\'s waist ring at '+B.D.waistZ.toFixed(3)+' m (and the bib or apron edge that rides it).' }; } },
    { id:'roundtrip', title:'Export round trip: quaternions out → FK → skinned mesh equals the solver', *run(B){
      let worst=0, at='', n=0;
      for(const name of C8.clipNames()){ const cl=C8.clip(name,B.key), cd=C8.clipDef(name);
        for(const tr of cl.tracks){ const L=B.sk.bones.map(b=>{ const x=tr.bones[b.id]; return { p:x.pos, R:C8.matOf(x.rot) }; });
          const d=maxDelta(skin(B, worldsFromLocals(B,L)), skin(B, C8.evalClip(name,C8.uOf(cd.anim,tr.frame),B).W)); if(d>worst){ worst=d; at=name+' f'+tr.frame; } n++; }
        yield n; }
      return { pass:worst<1e-6, value:e(worst)+' m', detail:n+' frames through clip() → quaternion → matrix → FK → linear-blend skin, against the solver\'s own worlds. Worst '+e(worst)+' m'+(at?' at '+at:'')+'; gate 1e-6 m.' }; } },
    { id:'continuity', title:'Quaternion tracks stay in one hemisphere frame to frame', *run(B){
      let flips=0, worst=0, at='';
      for(const name of C8.clipNames()){ const cl=C8.clip(name,B.key);
        for(let k=1;k<cl.tracks.length;k++) for(const id of cl.bones){ const a=cl.tracks[k-1].bones[id].rot, b=cl.tracks[k].bones[id].rot, d=a[0]*b[0]+a[1]*b[1]+a[2]*b[2]+a[3]*b[3];
          if(d<0) flips++; const ang=2*Math.acos(Math.min(1,Math.abs(d)))*180/Math.PI; if(ang>worst && C8.SOCKETS[id]==null){ worst=ang; at=name+' f'+(k-1)+'→'+k+' '+id; } }
        yield name; }
      return { pass:!flips, value:flips+' flips', detail:'Sign continuity on every bone of every clip. Largest joint step between two frames: '+worst.toFixed(1)+'° ('+at+') — a slerp between neighbours never takes the long way round.' }; } },
    { id:'rock', title:'Deck rock is a three-bone additive, keyed on the rock alone', *run(B){
      const rocks=[{ roll:10, pitch:5, counter:1 },{ roll:-15, pitch:10, counter:1 },{ roll:7, pitch:-12, counter:0.5 },{ roll:15, pitch:15, counter:1 }];
      const three=new Set(C8.ROCK_BONES.map(id=>B.sk.ix[id])); let other=0, tabl=0, exp=0, n=0, touched=new Set();
      for(const F of frames()){ const S0=C8.evalClip(F.n,F.u,B);
        for(const r of rocks){ const S1=C8.evalClip(F.n,F.u,B,null,r), Q=C8.rockDelta(r);
          S1.loc.forEach((l,i)=>{ const dp=dist(l.p,S0.loc[i].p), dr=mdiff(l.R,S0.loc[i].R); if(dp>1e-12||dr>1e-12) touched.add(B.sk.bones[i].id);
            if(three.has(i)) tabl=Math.max(tabl, mdiff(l.R, mM(Q[B.sk.bones[i].id], S0.loc[i].R)), dp); else other=Math.max(other,dp,dr); });
          const L=S0.loc.map((l,i)=>{ const id=B.sk.bones[i].id; return three.has(i) ? { p:l.p, R:C8.matOf(C8.qmul(C8.quatOf(Q[id]), C8.quatOf(l.R))) } : l; });
          exp=Math.max(exp, maxDelta(skin(B,worldsFromLocals(B,L)), skin(B,S1.W))); n++; }
        if(n%80===0) yield n; }
      const T=C8.rockTable();
      return { pass:other<1e-12 && tabl<1e-12 && exp<1e-6 && touched.size<=3, value:touched.size+' of '+B.sk.bones.length+' bones',
        detail:n+' rocked frames (every clip, every frame, four sea states). Bones touched: '+[...touched].join(', ')+'. Every other bone unchanged to '+e(other)+'. Deltas equal the table (keyed on the rock alone) to '+e(tabl)+'; clip + table through quaternions skins to the rocked solve within '+e(exp)+' m. Table: '+T.rows.length+' rows. Rig 7 part 2 measured 21 of 45 bones and 176° of arm spread for one rock.' }; } },
    { id:'loops', title:'Loops close: u = 1 is u = 0, the ladder included (it is body-anchored)', *run(B){
      let worst=0, at='', n=0;
      for(const name of C8.clipNames()){ const cd=C8.clipDef(name), A=C8.ANIMS[cd.anim]; if(A.oneShot) continue;
        const d=maxDelta(skin(B,C8.evalClip(name,0,B).W), skin(B,C8.evalClip(name,1,B).W)); if(d>worst){ worst=d; at=name; } n++; yield n; }
      return { pass:worst<1e-6, value:e(worst)+' m', detail:n+' looping clips. Worst seam '+e(worst)+' m'+(at?' ('+at+')':'')+'.' }; } },
    { id:'handoffs', title:'One-shots meet the clips they hand to, to the micron', *run(B){
      const rz=C8.CONTRACT.railZ.def, rx=-C8.CONTRACT.reachX, S=(n,u,o)=>skin(B,C8.evalClip(n,u,B,o).W), last=(n)=>C8.uOf(n,C8.ANIMS[n].frames-1), rows=[];
      const idle=S('idle',0), astr=S('astride',0), bench=S('astride',0,{ saddle:{ bench:true } });
      const t=(label,a,b,shift)=>{ const d=maxDelta(a,b,shift); rows.push({ label, d }); };
      t('board last = idle on the deck', S('board',last('board')), idle, [0,0,rz]);
      t('boardDown f0 = idle on the deck', S('boardDown',0), idle, [0,0,rz]); t('boardDown last = idle on the dock', S('boardDown',last('boardDown')), idle);
      t('mountUp f0 = idle beside the machine', S('mountUp',0), idle, [rx,0,0]); t('mountUp last = astride f0', S('mountUp',1), astr);
      t('mountDown f0 = astride f0', S('mountDown',0), astr); t('mountDown last = idle beside', S('mountDown',1), idle, [rx,0,0]);
      t('mountCab last = astride(bench) f0', S('mountCab',1), bench); t('mountCabDown f0 = astride(bench) f0', S('mountCabDown',0), bench);
      t('reach last = idle', S('reach',1), idle); yield 1;
      const worst=rows.reduce((m,r)=>Math.max(m,r.d),0);
      return { pass:worst<1e-6, value:e(worst)+' m', detail:rows.map(r=>r.label+' '+e(r.d)).join(' · ') }; } },
    { id:'contacts', title:'Contacts: no skate at the engine speed, no sole below the ground, limbs make their targets', *run(B){
      let skate=0, skAt='', below=0, blAt='', cloth=0, clAt='', n=0; const short={}; const ix=B.sk.ix;
      for(const name of ['walk','run']){ const A=C8.ANIMS[name], v=C8.evalClip(name,0,B).I.meta.speed;
        for(const s of ['L','R']){ const pts=[];
          for(let k=0;k<A.frames;k++){ const S=C8.evalClip(name,C8.uOf(name,k),B); if(!S.I.plant[s] || Math.abs(S.I.legs[s].pitch||0)>0.5) continue;
            const sole=add(S.W[ix['foot_'+s]].p, mV(S.W[ix['foot_'+s]].R, B.D.sole)); pts.push(sole[1]+v*k*A.ms/1000); }
          if(pts.length>1){ const d=Math.max(...pts)-Math.min(...pts); if(d>skate){ skate=d; skAt=name+' '+s; } } } }
      const noGround={ swim:1, tread:1, sleep:1, ladderDown:1 };
      for(const F of frames()){ const S=C8.evalClip(F.n,F.u,B); n++;
        for(const [k,v] of Object.entries(S.err)){ const key=F.n; if(v>(short[key]?short[key].v:0)) short[key]={ v, at:k+' f'+F.k }; }
        if(!noGround[F.cd.anim]){ const V=skin(B,S.W); let mz=Infinity, mc=Infinity; V.forEach((p,i)=>{ if(CLOTH[PARTS(B)[i]]) mc=Math.min(mc,p[2]); else mz=Math.min(mz,p[2]); }); if(-mz>below){ below=-mz; blAt=F.n+' f'+F.k; } if(-mc>cloth){ cloth=-mc; clAt=F.n+' f'+F.k; } }
        if(n%60===0) yield n; }
      /* the saddle family at the fitted saddle: every limb within 5 mm, and in the mounts the standing foot planted on the ground from the
         moment the swing foot leaves the ground until the seat carries the body (no seat gap). Gated on creator-size builds, reported on smaller ones. */
      const fitO=C8.saddleOpts(C8.CONTRACT.saddleFit.scale, C8.CONTRACT.saddleFit.reachX), SAD=['astride','astrideStand','mountUp','mountDown','mountCab','mountCabDown']; let sadMax=0, sadAt='', seatGap=0, gapAt=''; const lifted=[];
      for(const n2 of SAD){ const A=C8.ANIMS[n2];
        for(let k=0;k<A.frames;k++){ const S=C8.evalClip(n2,C8.uOf(n2,k),B,fitO); for(const [l,v] of Object.entries(S.err)) if(v>sadMax){ sadMax=v; sadAt=n2+' f'+k+' '+l; }
          const ph=S.I.pins.saddle && S.I.pins.saddle.phase; if(!ph) continue; if(ph.seatGap_m>seatGap){ seatGap=ph.seatGap_m; gapAt=n2+' f'+k; }
          if(ph.swing>0 && ph.seated<1 && !(S.I.plant[ph.standSide] && ph.standFoot==='ground')) lifted.push(n2+' f'+k); } }
      yield 'saddle';
      const sadGate=!small(B), sadOk=sadMax<=0.005 && seatGap<=0.001 && !lifted.length;
      const gated=Object.entries(short).filter(([k,x])=>!reportOnly(B,C8.clipDef(k).anim) && x.v>0.005), reported=Object.entries(short).filter(([k,x])=>reportOnly(B,C8.clipDef(k).anim) && x.v>0.001);
      const sadText=' Saddle family at the fitted saddle (the default scaled to '+C8.CONTRACT.saddleFit.scale+', the idle spot '+C8.CONTRACT.saddleFit.reachX+' m beside it): worst limb '+(sadMax*1000).toFixed(0)+' mm'+(sadAt&&sadMax>0.001?' ('+sadAt+')':'')+', seat gap '+(seatGap*1000).toFixed(0)+' mm'+(gapAt&&seatGap>0.0005?' ('+gapAt+')':'')+', standing foot '+(lifted.length?'off the ground before the seat carries the body at '+lifted.slice(0,4).join(', '):'on the ground until the seat carries the body')+(sadGate?' — gated.':' — reported (a build smaller than the Fisher).');
      const rows=Object.entries(short).filter(([,x])=>x.v>0.001).map(([k,x])=>({ label:k, value:(x.v*1000).toFixed(0)+' mm', note:x.at+(reportOnly(B,C8.clipDef(k).anim)?' · '+reportOnly(B,C8.clipDef(k).anim)+', reported':''), ok:!!reportOnly(B,C8.clipDef(k).anim) || x.v<=0.005 }));
      rows.push({ label:'saddle family at scale '+C8.CONTRACT.saddleFit.scale, value:(sadMax*1000).toFixed(0)+' mm', note:'seat gap '+(seatGap*1000).toFixed(0)+' mm · standing foot '+(lifted.length?'lifted early':'grounded')+(sadGate?'':' · reported'), ok:!sadGate || sadOk });
      return { pass:skate<=0.002 && below<=0.003 && !gated.length && (!sadGate || sadOk), value:(skate*1000).toFixed(2)+' mm skate',
        detail:'Planted-foot drift against a ground moving at the clip speed: '+(skate*1000).toFixed(2)+' mm ('+skAt+'). Deepest vertex below z = 0 on grounded clips: '+(below*1000).toFixed(1)+' mm'+(blAt?' ('+blAt+')':'')+(cloth>0.003?'. Cloth (skirt, apron) that meets the ground in a crouch, reported: '+(cloth*1000).toFixed(0)+' mm ('+clAt+') — it folds in the engine':'')+'. Limbs short of a target by > 5 mm on gated clips: '+(gated.length?gated.map(g=>g[0]).join(', '):'none')+'. Reported, not gated: '+(reported.length?reported.map(([k,x])=>k+' '+(x.v*1000).toFixed(0)+' mm').join(', '):'none')+' — at the default saddle, whose seat and pegs these legs cannot reach.'+sadText, rows }; } },
    { id:'travel', title:'No limb teleports: every vertex moves ≤ 0.35 m between frames (scaled by limb length)', *run(B){
      let worst=0, at='', n=0, cl=0, clA='', tr=0, trA=''; const fast={ reach:0, boardDown:0 }, kk=C8.kOf(B.D), gate=0.35*Math.max(1,kk.a,kk.l,kk.x), P=PARTS(B);
      const mvd=(A2,B2)=>{ let m=0, mc=0; for(let i=0;i<A2.length;i++){ const d=dist(A2[i],B2[i]); if(CLOTH[P[i]]) mc=Math.max(mc,d); else m=Math.max(m,d); } return [m,mc]; };
      for(const name of C8.clipNames()){ const cd=C8.clipDef(name), A=C8.ANIMS[cd.anim]; let prev=null, first=null;
        for(let k=0;k<A.frames;k++){ const V=skin(B,C8.evalClip(name,C8.uOf(cd.anim,k),B).W); if(!first) first=V;
          if(prev){ const [d,dc]=mvd(prev,V); if(dc>cl){ cl=dc; clA=name+' f'+(k-1)+'→'+k; } if(fast[cd.anim]!=null) fast[cd.anim]=Math.max(fast[cd.anim],d); else if(REPORT_ONLY[cd.anim]==='transition' || (small(B) && WORLD[cd.anim])){ if(d>tr){ tr=d; trA=name+' f'+(k-1)+'→'+k; } } else if(d>worst){ worst=d; at=name+' f'+(k-1)+'→'+k; } } prev=V; }
        if(!A.oneShot && !C8.SEGMENTS[cd.anim]){ const d=mvd(prev,first)[0]; if(d>worst){ worst=d; at=name+' wrap'; } }
        n++; yield n; }
      /* the mounts are gated at the fitted saddle (the default saddle's seat is out of every body's reach, and they are reported there) */
      let mt=0, mtA=''; const fitO=C8.saddleOpts(C8.CONTRACT.saddleFit.scale, C8.CONTRACT.saddleFit.reachX);
      for(const name of ['mountUp','mountDown','mountCab','mountCabDown']){ const A=C8.ANIMS[name]; let prev=null;
        for(let k=0;k<A.frames;k++){ const V=skin(B,C8.evalClip(name,C8.uOf(name,k),B,fitO).W); if(prev){ const d=mvd(prev,V)[0]; if(d>mt){ mt=d; mtA=name+' f'+(k-1)+'→'+k; } } prev=V; } }
      if(!small(B) && mt>worst){ worst=mt; at=mtA+' at the fitted saddle'; }
      return { pass:worst<=gate, value:worst.toFixed(3)+' m', detail:'Largest single-frame vertex move '+worst.toFixed(3)+' m ('+at+'), gate '+gate.toFixed(3)+' m (0.35 m on the Fisher, scaled by this build\'s limb length and shoulder width).'+(cl>0?' Cloth (skirt, apron) reported: '+cl.toFixed(3)+' m ('+clA+').':'')+' The mounts at the fitted saddle: '+mt.toFixed(3)+' m ('+mtA+')'+(small(B)?', reported on a build smaller than the Fisher':'')+'.'+(tr>0?' Reported: the mounts at the default saddle'+(small(B)?' and the world-contract clips (rail, bench, cab) on this build smaller than the Fisher':'')+', '+tr.toFixed(3)+' m ('+trA+').':'')+' Every loop wrap is checked except the four segments the table lists as loops but the game chains in order ('+Object.keys(C8.SEGMENTS).join(', ')+'). Reported, not gated, because the shipped frame count sets the pace: reach '+fast.reach.toFixed(3)+' m (280 ms from the hand opening at the ground to the idle it hands to), boardDown '+fast.boardDown.toFixed(3)+' m (a 0.55 m drop in six frames, 570 ms).' }; } },
    { id:'cell', title:'Every frame fits the 64 × 92 cell at every facing, keyline included, 1 px spare', *run(B){
      let tight=99, at='', n=0, lyT=99, lyA='', clT=99, clA=''; const skip={ mountUp:1, mountDown:1, mountCab:1, mountCabDown:1 }, LYING={ swim:1, sleep:1, tread:1 }, tall=B.D.heightM>1.545;
      for(const F of frames()){ if(skip[F.cd.anim]) continue; const S=C8.evalClip(F.n,F.u,B);
        for(let d=0;d<8;d++){ const R=C8.paintSolved(S,B,{ dir:d, keyline:false, edges:false }); let x0=R.W, x1=-1, y0=R.H, y1=-1, X0=R.W, X1=-1, Y0=R.H, Y1=-1;
          /* a skirt or apron is rigid on the pelvis, so in a crouch it can swing past the cell edge; those pixels (and the keyline they own) are reported, not gated */
          const own=(x,y)=>{ const f=R.face[y*R.W+x]; return f>=0 && !CLOTH[R.faces[f].part]; };
          /* coverage only: the keyline is exactly the empty pixels with a covered 4-neighbour, so it is read off the coverage (the same pixels the drawn keyline has) */
          const cv=(x,y)=>x>=0 && y>=0 && x<R.W && y<R.H && R.face[y*R.W+x]>=0, drawn=(x,y)=>cv(x,y) || cv(x,y-1) || cv(x+1,y) || cv(x-1,y) || cv(x,y+1);
          for(let y=0;y<R.H;y++) for(let x=0;x<R.W;x++) if(drawn(x,y)){ if(x<X0) X0=x; if(x>X1) X1=x; if(y<Y0) Y0=y; if(y>Y1) Y1=y;
            if(own(x,y) || (R.face[y*R.W+x]<0 && ((x>0&&own(x-1,y))||(x+1<R.W&&own(x+1,y))||(y>0&&own(x,y-1))||(y+1<R.H&&own(x,y+1))))){ if(x<x0) x0=x; if(x>x1) x1=x; if(y<y0) y0=y; if(y>y1) y1=y; } }
          const mc=Math.min(X0, R.W-1-X1, Y0, R.H-1-Y1); if(mc<clT){ clT=mc; clA=F.n+' f'+F.k+' '+C8.order[d]; }
          const m=Math.min(x0, R.W-1-x1, y0, R.H-1-y1); if(LYING[F.cd.anim] && tall){ if(m<lyT){ lyT=m; lyA=F.n+' f'+F.k+' '+C8.order[d]; } } else if(m<tight){ tight=m; at=F.n+' f'+F.k+' '+C8.order[d]; } n++; }
        if(n%160===0) yield n; }
      return { pass:tight>=1, value:tight+' px spare', detail:n+' renders. Tightest: '+tight+' px ('+at+').'+(clT<1?' Reported, not gated: cloth (skirt, apron) reaches the cell edge in '+clA+'; it folds in the engine.':'')+(lyT<99?' Reported, not gated: swim, sleep and tread on a build taller than the Fisher, tightest '+lyT+' px ('+lyA+'); at 0 px the body reaches the cell edge.':'')+' The four mount clips start a reachX beside the machine and are stamped into its cell (saddle.at), so they are not held to this one.' }; } },
    { id:'face', title:'The face resolves at 32 px/m: two eyes in front views, one in profile, none from behind', *run(B){
      const S=C8.evalClip('idle',0,B), rows=[]; let ok=true;
      const count=(R, test)=>{ let l=0, r=0; for(let i=0;i<R.face.length;i++){ const fi=R.face[i]; if(fi<0) continue; const f=R.faces[fi]; if(test(f)){ if((f.side||f.bx)<0) l++; else r++; } } return [l,r]; };
      const want={ N:[0,0], NE:[0,0], E:[1,1], SE:[2,2], S:[2,2], SW:[2,2], W:[1,1], NW:[0,0] };
      for(let d=0;d<8;d++){ const R=C8.paintSolved(S,B,{ dir:d }), eyes=count(R,f=>f.group==='eyes.open'), nose=count(R,f=>f.part==='nose'), mouth=count(R,f=>f.group==='mouth.flat');
        const nEyes=(eyes[0]>0)+(eyes[1]>0), w=want[C8.order[d]], good=nEyes>=w[0] && nEyes<=w[1];
        if(!good) ok=false;
        rows.push({ label:C8.order[d], value:nEyes+' eye'+(nEyes===1?'':'s'), note:'eyes '+eyes[0]+'+'+eyes[1]+' px · nose '+(nose[0]+nose[1])+' px · mouth '+(mouth[0]+mouth[1])+' px', ok:good }); }
      const groups=[], same=[], sig=(R,slot)=>{ const o=[]; for(let i=0;i<R.face.length;i++){ const fi=R.face[i]; if(fi<0) continue; const f=R.faces[fi]; if(f.group && f.group.split('.')[0]===slot) o.push(i+':'+R.rgba[i*4]+','+R.rgba[i*4+1]+','+R.rgba[i*4+2]); } return o.join('|'); };
      const def={ eyes:'open', brows:'flat', mouth:'flat' }, diag=[];
      for(const g of C8.GROUP_ORDER){ const [slot,state]=g.split('.'), fc=Object.assign({}, def); fc[slot]=state;
        const R=C8.paintSolved(S,B,{ dir:4, face:fc }), c=count(R,f=>f.group===g); groups.push(g+' '+(c[0]+c[1])); if(c[0]+c[1]<1) ok=false;
        if(state!==def[slot]){ const R0=C8.paintSolved(S,B,{ dir:4, face:def }); if(sig(R,slot)===sig(R0,slot)){ same.push(g); ok=false; }
          for(const d of [3,5]){ const Ra=C8.paintSolved(S,B,{ dir:d, face:fc }), Rb=C8.paintSolved(S,B,{ dir:d, face:def }); if(sig(Ra,slot)===sig(Rb,slot)) diag.push(g+' '+C8.order[d]); } } }
      yield 1;
      return { pass:ok, value:rows.filter(r=>r.ok).length+' / 8 facings', detail:'Every face group draws at S: '+groups.join(' · ')+' px. Every state changes its slot pixels at S against the default'+(same.length?', except '+same.join(', '):'')+'. At the diagonals '+(diag.length?'these read the same as the default: '+diag.join(', '):'every state still changes pixels')+'.', rows }; } },
    { id:'hair', title:'Hair: no skull skin where the hair is — 16 facings, idle, walk and the head turned to its limits', *run(B){
      const HL=C8.hairline(B.key), cache={}, most={ face:0, temple:0, ear:0, nape:0, scalp:0 }; let hair=0, at='', n=0;
      const jobs=[['idle',0,null],['idle',3,null],['walk',2,null],['walk',6,null]].concat(LIMITS.slice(0,4).map(([y,p])=>['idle',0,{ yaw:y, pitch:p }]));
      for(const [clip,frame,look] of jobs){ for(let k=0;k<16;k++){ const R=C8.render({ clip, frame, yaw:k*22.5, build:B.key, look, keyline:false, edges:false }), c=skullCount(R,B,HL,cache); n++;
          if(c.hair>hair){ hair=c.hair; at=clip+' f'+frame+(look?' turned '+look.yaw+'/'+look.pitch:'')+' at '+(k*22.5)+'°'; } for(const z of Object.keys(most)) most[z]=Math.max(most[z],c[z]); } yield n; }
      let meshBad=0; const nb=B.mesh.groups['eyes.open'][0];
      for(let fi=0;fi<nb;fi++){ const f=B.mesh.faces[fi]; if(f.part!=='head' || !SKIN_M[f.mat]) continue; const side=skullSide(f), c=f.v.reduce((a,p)=>[a[0]+p[0]/f.v.length,a[1]+p[1]/f.v.length,a[2]+p[2]/f.v.length],[0,0,0]); if(strictlyHair(B,HL,side,c)) meshBad++; }
      const hl=HL ? (HL.horseshoe ? 'bald: the horseshoe, back of the head, canonical z '+HL.horseshoe.join('-') : 'front hairline '+HL.F.toFixed(3)+', corners '+HL.C+', temples '+HL.T+', over the ear '+HL.E.toFixed(3)+', nape '+HL.N) : 'no hair region';
      return { pass:hair===0 && meshBad===0, value:hair+' px', detail:n+' renders (16 facings × idle f0, f3, walk f2, f6 and idle with the head turned to yaw ±60 and pitch −15 / +20). Skull skin inside the hair region: '+hair+' px'+(at?' ('+at+')':'')+'; skull faces painted skin inside it: '+meshBad+'. Hair region ('+B.b.hairStyle+'): '+hl+'. The skin meant to show, most in one render: face and forehead '+most.face+' px, temples '+most.temple+', over the ear line '+most.ear+', nape '+most.nape+'. Painted scalp (hair-coloured skull) showing through the shell: at most '+most.scalp+' px.' }; } },
    { id:'gaze', title:'Gaze: eyes.left and eyes.right move the pupils at every facing where the eyes show', *run(B){
      const S=C8.evalClip('idle',0,B), rows=[]; let ok=true, shown=0;
      const cx=(A,W)=>A.length ? A.reduce((s,i)=>s+(i%W),0)/A.length : null;
      for(let k=0;k<16;k++){ const yaw=k*22.5, P={}; let W=64; for(const st of ['open','left','right']){ const R=C8.paintSolved(S,B,{ yaw, face:{ eyes:st, brows:'flat', mouth:'flat' }, keyline:false, edges:false }); W=R.W; P[st]=irisOf(R); }
        if(!P.open.length){ rows.push({ label:yaw+'°', value:'no eyes', note:'', ok:true }); continue; } shown++;
        const moved=(a,b)=>{ const A=new Set(a); return b.length!==a.length || b.some(i=>!A.has(i)); }, sh=(a,b)=>{ const x0=cx(a,W), x1=cx(b,W); return x1==null ? 'hidden' : (x1-x0>=0?'+':'')+(x1-x0).toFixed(1)+' px'; };
        /* two eyes showing: each gaze moves the pupils. One eye in profile (a pupil of one pixel, with hair or the ear over the pixel behind it):
           the two gazes read apart and one of them moves it (toward the camera it keeps its pupil, away it shows white) */
        const R0=C8.paintSolved(S,B,{ yaw, face:{ eyes:'open', brows:'flat', mouth:'flat' }, keyline:false, edges:false }), sides=new Set(P.open.map(i=>R0.faces[R0.face[i]].side)).size;
        const good = sides>=2 ? moved(P.open,P.left) && moved(P.open,P.right) && moved(P.left,P.right) : moved(P.left,P.right) && (moved(P.open,P.left) || moved(P.open,P.right)); if(!good) ok=false;
        rows.push({ label:yaw+'°', value:'left '+sh(P.open,P.left)+' · right '+sh(P.open,P.right), note:'pupil px '+P.open.length+' / '+P.left.length+' / '+P.right.length, ok:good }); }
      yield 1;
      return { pass:ok, value:shown+' facings', detail:'At 32 px/m, idle f0, 16 facings: the pupil (the ink and iris pixels) of eyes.left and of eyes.right against eyes.open, screen x of the pupils\' centre ("hidden" where the only eye shown turns away in profile). Where two eyes show, eyes.left and eyes.right each move the pupils; where one eye shows in profile the two read apart (toward the camera the pupil stays, away it turns white).', rows }; } },
    { id:'look', title:'Look-at: the head turned to every limit opens no gap in the hair, hood or collar; the aim lands', *run(B){
      const HL=C8.hairline(B.key), cache={}, hooded=B.b.hat==='hood'; let scalp=0, sAt='', hood=0, hAt='', gap=0, gAt='', n=0;
      for(const clip of ['idle','walk']) for(let k=0;k<16;k++){ const frame=clip==='walk'?2:0, R0=C8.render({ clip, frame, yaw:k*22.5, build:B.key, keyline:false, edges:false }), neck0=hooded?partPx(R0,'neck'):0, g0=neckGaps(R0);
        for(const [y,p] of LIMITS){ const R=C8.render({ clip, frame, yaw:k*22.5, build:B.key, look:{ yaw:y, pitch:p }, keyline:false, edges:false }), c=skullCount(R,B,HL,cache), nk=hooded?partPx(R,'neck')-neck0:0, gg=neckGaps(R)-g0, lab=clip+' at '+(k*22.5)+'° turned '+y+'/'+p; n++;
          if(c.hair>scalp){ scalp=c.hair; sAt=lab; } if(nk>hood){ hood=nk; hAt=lab; } if(gg>gap){ gap=gg; gAt=lab; } }
        yield n; }
      const S=C8.evalClip('idle',0,B), ix=B.sk.ix; let aim=0, aAt='', inside=0;
      const W0=S.W, e0=add(W0[ix.head].p, mV(W0[ix.head].R, B.D.headMid));
      for(const bear of [-50,-30,-15,0,15,30,50]) for(const dz of [-0.35,0,0.25]){ const T=[e0[0]+2*Math.sin(bear*Math.PI/180), e0[1]+2*Math.cos(bear*Math.PI/180), e0[2]+dz], L=C8.lookAt(S,T,1);
        if(Math.abs(L.need.yaw)>C8.LOOK.yaw[1] || L.need.pitch<C8.LOOK.pitch[0] || L.need.pitch>C8.LOOK.pitch[1]) continue; inside++;
        const S2=C8.evalClip('idle',0,B,null,null,{ yaw:L.yaw, pitch:L.pitch }), Wh=S2.W[ix.head], f=mV(Wh.R,[0,1,0]), ep=add(Wh.p, mV(Wh.R,B.D.headMid)), d=sub(T,ep), dl=Math.hypot(...d), a=Math.acos(Math.max(-1,Math.min(1,(f[0]*d[0]+f[1]*d[1]+f[2]*d[2])/dl)))*180/Math.PI;
        if(a>aim){ aim=a; aAt='bearing '+bear+'°, '+(dz>=0?'+':'')+dz+' m'; } }
      return { pass:scalp===0 && hood===0 && gap===0, value:gap+' px', detail:n+' renders: idle f0 and walk f2 at 16 facings, the head turned to each limit (yaw ±'+C8.LOOK.yaw[1]+', pitch '+C8.LOOK.pitch.join(' / ')+' and the four corners; neck '+C8.LOOK.split.neck+', head '+C8.LOOK.split.head+'). Scalp inside the hair region: '+scalp+' px'+(sAt?' ('+sAt+')':'')+'. '+(hooded?'Neck showing through the hood beyond the unturned pose: '+hood+' px'+(hAt?' ('+hAt+')':'')+'. ':'')+'See-through at the neck, collar or hood beyond the unturned pose: '+gap+' px'+(gAt?' ('+gAt+')':'')+'. Aim (share 1): for '+inside+' targets 2 m away inside the limits, lookAt() then the turn points the head\'s +y within '+aim.toFixed(2)+'° of the target'+(aAt?' (worst at '+aAt+')':'')+'.' }; } },
    { id:'tones', title:'Tone discipline: each material lights at most four ramp steps in a frame (the contour step aside)', *run(B){
      let worst=0, at='', n=0;
      for(const name of ['idle','walk','run','hold','cast','dig','haul','ladderDown','lift','swim','sleep','drive','astride']){ const A=C8.ANIMS[C8.clipDef(name).anim];
        for(let k=0;k<A.frames;k+=2){ const S=C8.evalClip(name,C8.uOf(C8.clipDef(name).anim,k),B);
          for(let d=0;d<8;d++){ const R=C8.paintSolved(S,B,{ dir:d, keyline:false, edges:false }), sets={};
            for(let i=0;i<R.mat.length;i++){ if(R.mat[i]<0) continue; const m=R.mats[R.mat[i]]; if(B.mats[m].fixed) continue; (sets[m]=sets[m]||new Set()).add(R.lit[i]); }
            for(const [m,st] of Object.entries(sets)) if(st.size>worst){ worst=st.size; at=m+' · '+name+' f'+k+' '+C8.order[d]; } n++; } }
        yield n; }
      return { pass:worst<=4, value:worst+' steps', detail:n+' renders. The most lit steps any one material spends in a frame: '+worst+' ('+at+'). The inner contour darkens one step past that — the outline doing its job, not a fifth tone.' }; } },
    { id:'light', title:'Top-of-frame key: the rest pose at E is the mirror of W, to the pixel', *run(B){
      const I=C8.baseIntent(B.D), S=C8.solve(I,B); S.B=B;
      const E=C8.paintSolved(S,B,{ dir:2 }), Wm=C8.paintSolved(S,B,{ dir:6 }); let diff=0;
      for(let y=0;y<E.H;y++) for(let x=0;x<E.W;x++){ const a=(y*E.W+x)*4, b=(y*E.W+(E.W-1-x))*4; for(let c=0;c<4;c++) if(E.rgba[a+c]!==Wm.rgba[b+c]){ diff++; break; } }
      const S2=C8.paintSolved(S,B,{ dir:4, snapHead:false }); let asym=0;
      for(let y=0;y<S2.H;y++) for(let x=0;x<S2.W/2;x++){ const a=(y*S2.W+x)*4, b=(y*S2.W+(S2.W-1-x))*4; for(let c=0;c<3;c++) if(S2.rgba[a+c]!==S2.rgba[b+c]){ asym++; break; } }
      yield 1;
      return { pass:diff===0 && asym===0, value:diff+' px differ', detail:'E against W mirrored: '+diff+' px differ. S against itself mirrored (head snap off — an odd-width head cannot sit on a pixel centre and be centred on the pivot seam at once): '+asym+' px. An upper-left key cannot pass this; the key is '+C8.SHADING.key.map(x=>x.toFixed(3)).join(', ')+' (right, up, toward camera) — the fleet key without its left component.' }; } },
    { id:'budget', title:'Budget against rig 7 (reported: over 1,000 tris is allowed)', gate:false, *run(B){ const s=C8.sizes(B.key), clipsKB=Math.round(JSON.stringify(C8.clips(B.key)).length/1024); yield 1;
      return { pass:s.tris<=1000 && s.bones<=32, value:s.tris+' tris', detail:s.tris+' tris ('+s.faces+' faces) against rig 7\'s 1,570 · '+s.bones+' bones against 45 · '+s.clipCount+' clips, '+s.frames+' frames, '+clipsKB+' KB of clip JSON against 2,155 KB for 35 · bind mesh '+Math.round(s.bindBytes/1024)+' KB against 235 KB.' }; } },
    { id:'sidecar', title:'Gameplay sidecar: every clip, every frame, the sections its mount needs', *run(B){
      const G=C8.gameplay(B.key); yield 1; const need={ rod:'tool', shovel:'tool', knife:'work', ladder:'ladder', warp:'work', bench:'work', load:'work', water:'water', bed:'bed', wheel:'wheel', rest:'rest', saddle:'saddle' };
      const miss=[]; let frames=0;
      for(const [name,c] of Object.entries(G.clips)){ if(c.pins.length!==c.frames) miss.push(name+' frames'); frames+=c.pins.length;
        const sec=need[c.mount]; if(sec && c.pins.some(p=>!p[sec] && !(c.mount==='rest' && p.rest))) miss.push(name+' '+sec);
        if(c.carry && c.pins.some(p=>!p.carry)) miss.push(name+' carry');
        if(C8.clipDef(name).anim==='board' || C8.clipDef(name).anim==='boardDown'){ if(c.pins.some(p=>!p.board)) miss.push(name+' board'); } }
      const sp=(n)=>G.clips[n].speed_mps, kl=C8.kOf(B.D).l; const speedsOk=Math.abs(sp('walk')-0.727*kl)<=0.0006 && Math.abs(sp('run')-2.0*kl)<=0.0006 && G.clips.ladderDown.pins[0].ladder.rate===0.545;
      return { pass:!miss.length && speedsOk, value:Object.keys(G.clips).length+' clips · '+frames+' frames', detail:'Schema '+G.schema+'. Walk '+sp('walk')+' m/s, run '+sp('run')+' m/s, ladder '+G.clips.ladderDown.pins[0].ladder.rate+' m/s (the Fisher 0.727 and 2.0, scaled by this build\'s leg length '+kl.toFixed(3)+'; ladder 0.545). '+(miss.length?'Missing: '+miss.slice(0,8).join(', '):'No section missing.') }; } },
    { id:'heights', title:'World heights: the crown in idle is the build\'s designed height', *run(B){ const S=C8.evalClip('idle',0,B), P=C8.pinsOf(Object.assign(S,{ B })); yield 1;
      const want=B.D.headZ+B.D.crown[2]; return { pass:Math.abs(P.head[2]-want)<=0.02, value:P.head[2].toFixed(3)+' m', detail:'Crown in idle '+P.head[2].toFixed(3)+' m against the build\'s '+want.toFixed(3)+' m ('+B.b.age+', height step '+(B.b.height>0?'+':'')+B.b.height+'; the Fisher is 1.522, rig 7 1.523). Hip joint '+(B.D.hipZ).toFixed(3)+' (rig 7 0.587). Palms at rest '+P.handL[2].toFixed(3)+' m. Legs '+(B.D.thigh+B.D.shin).toFixed(3)+' m hip to ankle (rig 7 0.541), arms '+(B.D.upper+B.D.fore+B.D.palmLen).toFixed(3)+' m shoulder to palm.' }; } } ];

  function runAll(build, onRow){ const B=C8.buildOf(build), out=[];
    for(const c of CHECKS){ const t0=Date.now(), it=c.run(B); let r=it.next(); while(!r.done) r=it.next(); const row=Object.assign({ id:c.id, title:c.title, ms:Date.now()-t0 }, r.value, { gate:c.gate!==false }); out.push(row); if(onRow) onRow(row); }
    return out; }
  C8.CHECKS=CHECKS; C8.runChecks=runAll; C8.checkHelpers={ skin, maxDelta, worldsFromLocals, skullCount, skullSide, bindPoint, strictlyHair, neckGaps, irisOf, partPx, LIMITS };
})(typeof globalThis!=='undefined' ? globalThis : window);

/* Hidden Harbours — FISH iso rig, PASS 2 (catch pass 2). fishIsoRig.js is untouched; this is a
   drop-in superset on the same fixed 3/4 turntable as the fleet (45deg steps CW, elev 40,
   32 px = 1 m, ordered dither, no AA).
   STRICT WORLD SCALE: scale 1 = the species' real length (SPECIES.len). A herring is 8 px long,
   a striped bass 24 — there is no readability floor; `scale` is the specimen (SPECIES.range).
   NEW IN PASS 2
   · species: flounder (a true flatfish — depressed body via zflat, fringe fins down both sides,
     eyes on top, undulating swim, lies FLAT on deck), herring (shoaling, silver), striped bass
     (dark lateral stripe band, two dorsals). The four pass-1 blocks are verbatim.
   · swim is 6f with a body-wave lag behind the tail beat (pass 1 wagged from the tail root);
     dart 3f; thrash pitches the head through the surface; + roll 4f (surface roll — belly
     flash) and jump 6f (clears the water: the rig bakes the z arc + pitch, MOTION says how far
     the page moves it forward).
   · fins that read at 20 px: paired pectorals, an anal fin, a second dorsal per species
     (dorsals), a darker gill edge, and dithered spots on the flounder's back.
   · shoal(species, n, t, opts) — RUNTIME SINGLES: world-metre positions + heading + the 8-dir
     bake + swim frame for n fish on one lazy loop. The game blits singles; no group is baked.
     dirOf(heading) snaps any heading to the turntable.
   · RINGLESS (ADR 0031): KEYLINE_DEFAULT = false; render(dir,{outline:true}) is the live A/B.
   Cell 64x64, pivot (32,38) = the water-surface point under the body centre. Pose z = spine
   height over the surface; pixels below waterZ (default 0) bake the depth-graded underwater
   tint + alpha (never clip at runtime); waterZ:null = dry. spoil 0..1 = the rot.
   RESTS keep the pass-1 contract: deck 4 lays · gill 2f · tail 2f · cradle 2f (pivot = THE
   GRIP, pins to CharacterIso hand anchors). hold(species,scale) -> {mass, hands}.
   Exposes globalThis.FishIso2 = { W,H,pivot,KEY,SPOIL,WATER,KEYLINE_DEFAULT,ORDER,SPECIES,
   ANIMS,AORDER,MOTION,POSE,RESTS,RPOSE,defaultElev,render,mouth,hold,sizeOf,project,shoal,
   dirOf,sheetOrder }. */
(function (root) {
  const S = 32, DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const W = 64, H = 64, cx = 32, cy = 38;
  const KEY = '#101a19', WATER = '#123034', KEYLINE_DEFAULT = false;
  const SPECIES = {
    cod:      { label:'COD', len:0.70, girth:0.105, flat:0.72, stripes:'none', dorsals:2, range:[0.6,1.5], massK:1,
                back:'#6c673b', flank:'#7d7649', belly:'#c2bc90', fin:'#54502c' },
    haddock:  { label:'HADDOCK', len:0.55, girth:0.090, flat:0.68, stripes:'none', dorsals:2, range:[0.7,1.3], massK:1,
                back:'#525f68', flank:'#697379', belly:'#cdd3d2', fin:'#222b30' },
    pollock:  { label:'POLLOCK', len:0.60, girth:0.092, flat:0.70, stripes:'none', dorsals:2, range:[0.7,1.4], massK:1,
                back:'#48543f', flank:'#55614f', belly:'#c2c8ba', fin:'#222a1e' },
    mackerel: { label:'MACKEREL', len:0.45, girth:0.068, flat:0.62, stripes:'bars', dorsals:2, range:[0.7,1.2], massK:1,
                back:'#27564a', stripe:'#173b32', flank:'#7fa79c', belly:'#bcc6c2', fin:'#142e28' },
    bass:     { label:'STRIPED BASS', len:0.75, girth:0.110, flat:0.70, stripes:'lines', dorsals:2, range:[0.6,1.6], massK:1.3,
                back:'#4a5c52', stripe:'#33413e', flank:'#aab8b4', belly:'#e0e7e4', fin:'#3d4a45' },
    flounder: { label:'FLOUNDER', len:0.35, girth:0.080, flat:1.0, zflat:0.24, fringe:true, bottom:true, undulate:true,
                stripes:'none', dorsals:0, range:[0.7,1.6], massK:0.5,
                back:'#5c4a34', flank:'#6b5840', belly:'#d9cfb6', fin:'#4a3a28', spots:'#3a2c1c' },
    herring:  { label:'HERRING', len:0.25, girth:0.028, flat:0.55, stripes:'none', dorsals:1, range:[0.8,1.2], massK:2, shoal:true,
                back:'#2e4d61', flank:'#95adb5', belly:'#dbe4e5', fin:'#3b5563' },
  };
  const ORDER = ['cod','haddock','pollock','mackerel','bass','flounder','herring'];
  const ANIMS = { swim:{n:6,ms:120}, dart:{n:3,ms:80}, thrash:{n:4,ms:110}, shadow:{n:2,ms:280}, roll:{n:4,ms:130}, jump:{n:6,ms:95} };
  const AORDER = ['swim','dart','thrash','shadow','roll','jump'];
  // page-side motion contract (m/s along the heading; jump.travel = metres over the 6 frames)
  const MOTION = { swim:{v:0.35}, dart:{v:1.4}, thrash:{v:0}, shadow:{v:0.18}, roll:{v:0.12}, jump:{v:0.9, travel:0.55} };
  const POSE = {
    swim:   [0,1,2,3,4,5].map(f=>{ const ph=f/6*Math.PI*2;
              return { sweep:0.55*Math.sin(ph), curve:0.16*Math.sin(ph-1.2), z:-0.15+0.006*Math.sin(ph*2), wph:ph }; }),
    dart:   [{sweep:0.30,stretch:1.08,z:-0.20,wph:0},{sweep:0,stretch:1.12,z:-0.20,wph:2.1},{sweep:-0.30,stretch:1.08,z:-0.20,wph:4.2}],
    thrash: [{curve:0.8,roll:0.6,sweep:0.6,pitch:0.25,z:0.02},{curve:-0.8,roll:-0.6,sweep:-0.6,pitch:0.10,z:0.04},
             {curve:0.5,roll:1.1,sweep:0.4,pitch:-0.15,z:0.05},{curve:-0.5,roll:-1.1,sweep:-0.4,pitch:0.05,z:0.02}],
    shadow: [{sweep:0.4,z:-0.50,wph:0},{sweep:-0.4,z:-0.50,wph:3.1}],
    roll:   [{roll:0.35,curve:0.25,sweep:0.2,z:0.0},{roll:1.5,curve:0.3,sweep:-0.2,z:0.01},
             {roll:2.7,curve:0.2,sweep:0.2,z:0.0},{roll:1.5,curve:-0.2,sweep:-0.2,z:-0.02}],
    jump:   [{z:0.03,pitch:0.95,sweep:0.5,roll:0.15,curve:0.15},{z:0.20,pitch:0.65,sweep:-0.4,roll:0.3,curve:-0.1},
             {z:0.34,pitch:0.20,sweep:0.3,roll:0.35,curve:0.1},{z:0.36,pitch:-0.25,sweep:-0.2,roll:0.3,curve:-0.1},
             {z:0.22,pitch:-0.65,sweep:0.3,roll:0.1,curve:0.15},{z:0.05,pitch:-0.95,sweep:-0.5,roll:0,curve:-0.15}],
  };
  const HPI = Math.PI/2;
  const RPOSE = {
    deck:   [{roll:HPI,curve:0.25},{roll:-HPI,curve:-0.2},{roll:HPI,curve:-0.3},{roll:-HPI,curve:0.15}],
    gill:   [{pitch:1.48,curve:0.12,gripU:0.13},{pitch:1.48,curve:-0.15,gripU:0.13}],
    tail:   [{pitch:-1.48,curve:0.10,gripU:0.95},{pitch:-1.48,curve:-0.12,gripU:0.95}],
    cradle: [{roll:0.35,curve:0.10,gripU:0.5},{roll:-0.35,curve:-0.10,gripU:0.5}],
  };
  const RESTS = ['deck','gill','tail','cradle'];
  const SPOIL = '#7d9a46', SPRGB = [0x7d,0x9a,0x46];

  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.12;
  const LN = (()=>{ const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  const hex2rgb=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  const rgb2hex=(r,g,b)=>'#'+[r,g,b].map(v=>Math.max(0,Math.min(255,Math.round(v))).toString(16).padStart(2,'0')).join('');
  const light=(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r+(255-r)*f, g+(255-g)*f, b+(255-b)*f); };
  const dark =(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r*f, g*f, b*f); };
  const mkRamp=(base)=>[dark(base,0.42), dark(base,0.66), base, light(base,0.17), light(base,0.34)];
  const WRGB = hex2rgb(WATER);
  const mod=(a,n)=>((a%n)+n)%n, clamp01=(v)=>Math.max(0,Math.min(1,v));

  const matsCache = {};
  function matsFor(key){
    if (matsCache[key]) return matsCache[key];
    const sp = SPECIES[key];
    const flank = mkRamp(sp.flank);
    const MATS = {
      back:{ramp:mkRamp(sp.back),off:0}, stripe:{ramp:mkRamp(sp.stripe||sp.back),off:0},
      flank:{ramp:flank,off:0}, gill:{ramp:flank,off:-1}, belly:{ramp:mkRamp(sp.belly),off:-1},
      fin:{ramp:mkRamp(sp.fin),off:0}, spot:{ramp:mkRamp(sp.spots||sp.back),off:0},
    };
    const RINDEX = {};
    for (const m of Object.values(MATS)) m.ramp.forEach((c,i)=>{ RINDEX[c]={r:m.ramp,i}; });
    return matsCache[key] = { MATS, RINDEX };
  }
  function camBasis(opts){
    const dir=opts.dir||0, th=-dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
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

  // ---- pose resolution -------------------------------------------------------
  function resolve(opts){
    opts = opts||{};
    const spKey = SPECIES[opts.species] ? opts.species : 'mackerel', sp = SPECIES[spKey];
    const spoil = clamp01(opts.spoil||0), scale = opts.scale||1;
    const outline = opts.outline===true || (opts.outline==null && KEYLINE_DEFAULT);
    if (opts.rest && RPOSE[opts.rest]){
      const poses = RPOSE[opts.rest], p = poses[mod(opts.frame||0, poses.length)];
      const flatDeck = !!sp.bottom && opts.rest==='deck';
      return { spKey, sp, anim:opts.rest, sweep:0, curve:p.curve||0, roll:flatDeck?0:(p.roll||0), pitch:p.pitch||0,
        zB: opts.rest==='deck' ? (flatDeck ? sp.girth*scale*(sp.zflat||1) : sp.girth*scale*sp.flat) : 0,
        stretch:1, scale, gripU:p.gripU!=null?p.gripU:null, wave:0, wph:0,
        waterZ: opts.waterZ===undefined ? null : opts.waterZ, spoil, outline };
    }
    const anim = POSE[opts.anim] ? opts.anim : 'swim', poses = POSE[anim], p = poses[mod(opts.frame||0, poses.length)];
    const wave = (sp.undulate && p.wph!=null) ? 1 : 0;
    return { spKey, sp, anim,
      sweep: opts.sweep!=null?opts.sweep:((p.sweep||0)*(wave?0.35:1)),
      curve: opts.curve!=null?opts.curve:(p.curve||0),
      roll:  opts.roll !=null?opts.roll :(p.roll ||0),
      pitch: opts.pitch!=null?opts.pitch:(p.pitch||0),
      zB:    opts.z    !=null?opts.z    :(p.z    ||0),
      stretch:p.stretch||1, scale, gripU:null, wave, wph:p.wph||0,
      waterZ: opts.waterZ===undefined ? 0 : opts.waterZ, spoil, outline };
  }
  // ---- body loft --------------------------------------------------------------
  function facesOf(r){
    const { sp, sweep, curve, roll, pitch, zB, stretch, scale, wave, wph } = r;
    const L = sp.len*scale, bodyLen = L*0.78*stretch, tl = L*0.24, girth = sp.girth*scale;
    const flat = sp.flat, zf = sp.zflat||1, fringe = !!sp.fringe;
    const g = (u)=>Math.max(0.012*scale, girth*(u<0.28 ? 0.4+0.6*(u/0.28) : 1-0.78*((u-0.28)/0.72)));
    const Cc=(u)=>4*(u-0.5)*(u-0.5)-0.5, TT=(u)=>Math.pow(Math.max(0,(u-0.45)/0.55),2);
    const lat =(u)=>curve*Cc(u)*L*0.18 + sweep*TT(u)*L*0.22;
    const vert=(u)=>wave ? Math.sin(u*Math.PI*2.6 - wph)*L*0.045*(0.4+0.6*u) : 0;
    const yOf =(u)=>bodyLen*(0.5-u);
    const cR=Math.cos(roll), sR=Math.sin(roll), cP=Math.cos(pitch), sP=Math.sin(pitch);
    const T0=(p)=>{ const x1=p[0]*cR-p[2]*sR, z1=p[0]*sR+p[2]*cR; const y2=p[1]*cP-z1*sP, z2=p[1]*sP+z1*cP; return [x1,y2,z2+zB]; };
    let T=T0;
    if (r.gripU!=null){ const gp=T0([lat(r.gripU), yOf(r.gripU), vert(r.gripU)]);
      T=(p)=>{ const q=T0(p); return [q[0]-gp[0], q[1]-gp[1], q[2]-gp[2]]; }; }
    const F=[], NSEG=9, NR=8, rings=[];
    for (let i=0;i<=NSEG;i++){
      const u=i/NSEG, rr=g(u), y=yOf(u), x0=lat(u), z0=vert(u), ring=[];
      for (let k=0;k<NR;k++){ const ph=k/NR*2*Math.PI; ring.push(T([x0+Math.cos(ph)*rr*flat, y, z0+Math.sin(ph)*rr*zf])); }
      rings.push(ring);
    }
    for (let i=0;i<NSEG;i++){
      const r0=rings[i], r1=rings[i+1];
      for (let k=0;k<NR;k++){
        const k2=(k+1)%NR, ms=(Math.sin(k/NR*2*Math.PI)+Math.sin(k2/NR*2*Math.PI))/2;
        let mat = ms>0.35 ? 'back' : ms<-0.35 ? 'belly' : 'flank';
        if (mat==='back' && sp.stripes==='bars' && i%2===1) mat='stripe';
        if (mat==='flank' && sp.stripes==='lines' && ms>0) mat='stripe';
        if (mat==='flank' && i===1) mat='gill';
        F.push({v:[r0[k],r0[k2],r1[k2],r1[k]], mat, b:0, db:0});
      }
    }
    const nose=T([lat(0), yOf(0)+g(0)*0.9, vert(0)]), rN=rings[0];
    for (let k=0;k<NR;k++){ const k2=(k+1)%NR; F.push({v:[nose,rN[k2],rN[k],rN[k]], mat:'flank', b:0, db:0}); }
    // tail fan — forked, swung with the sweep; flatfish tails spread in the body plane
    const ty=sweep*1.15+curve*0.5, Bp=[lat(1), yOf(1), vert(1)], bd=[Math.sin(ty), -Math.cos(ty), 0];
    const tp = fringe ? [-bd[1], bd[0], 0] : [0,0,1];
    const TP=(sx,sp2)=>T([Bp[0]+bd[0]*tl*sx+tp[0]*tl*sp2, Bp[1]+bd[1]*tl*sx+tp[1]*tl*sp2, Bp[2]+tp[2]*tl*sp2]);
    for (const s of [1,-0.85]){
      if (fringe) F.push({v:[T(Bp), TP(0.85,0.55*s), TP(1.0,0.22*s), TP(0.6,0)], mat:'fin', b:-0.1, db:-0.05, ds:1});
      else        F.push({v:[T(Bp), TP(1,0.62*s), TP(1.02,0.25*s), TP(0.55,0)], mat:'fin', b:-0.1, db:-0.05, ds:1});
    }
    const h=girth*0.55;
    const fin=(a,b,c,d,bb)=>F.push({v:[a,b,c,d], mat:'fin', b:bb!=null?bb:-0.1, db:-0.05, ds:1});
    const P=(u,dx,dz)=>T([lat(u)+dx, yOf(u), vert(u)+dz]);
    if (fringe){                                                     // fringe fins down both edges
      const fw=(u)=>girth*0.42*Math.sin(Math.PI*(u-0.14)/0.76);
      for (const sd of [-1,1]) for (const [u0,u1] of [[0.16,0.40],[0.40,0.64],[0.64,0.88]])
        fin(P(u0,sd*g(u0)*flat,0), P(u1,sd*g(u1)*flat,0), P(u1,sd*(g(u1)*flat+fw(u1)),-0.004*scale), P(u0,sd*(g(u0)*flat+fw(u0)),-0.004*scale));
    } else {
      const d1=0.36, d2=0.58;                                        // main dorsal
      fin(P(d1,0,g(d1)), P(d2,0,g(d2)), P(d2,0,g(d2)+h*0.6), P(d1,0,g(d1)+h));
      if (sp.dorsals>=2){ const e1=0.20, e2=0.31; fin(P(e1,0,g(e1)), P(e2,0,g(e2)), P(e2,0,g(e2)+h*0.9), P(e1,0,g(e1)+h*0.55)); }
      const a1=0.60, a2=0.74;                                        // anal fin
      fin(P(a1,0,-g(a1)), P(a2,0,-g(a2)), P(a2,0,-g(a2)-h*0.45), P(a1,0,-g(a1)-h*0.35));
      for (const sd of [-1,1]){                                      // pectorals, swept back and down
        const u=0.30, rr=g(u);
        const A=P(u, sd*rr*flat*0.9, -rr*0.15);
        const Bq=T([lat(u)+sd*(rr*flat*0.9+girth*0.45), yOf(u)-girth*0.8, vert(u)-rr*0.55]);
        const Cq=T([lat(u)+sd*(rr*flat*0.9+girth*0.2), yOf(u)-girth*1.0, vert(u)-rr*0.35]);
        F.push({v:[A,Bq,Cq,Cq], mat:'fin', b:-0.15, db:-0.03, ds:1});
      }
    }
    const ue=0.10, re=g(ue);
    const eyes = fringe
      ? [T([lat(ue)+re*flat*0.38, yOf(ue), vert(ue)+re*zf*0.9]), T([lat(ue)-re*flat*0.38, yOf(ue), vert(ue)+re*zf*0.9])]
      : [T([lat(ue)+re*flat*0.95, yOf(ue), vert(ue)+re*0.25]),  T([lat(ue)-re*flat*0.95, yOf(ue), vert(ue)+re*0.25])];
    return { F, eyes };
  }

  // ---- paint: z-buffered facets, dither, edge darkening, eyes, (gated) keyline, tints ------
  function _paint(r, o){
    const B=camBasis(o), {MATS,RINDEX}=matsFor(r.spKey), {F,eyes}=facesOf(r);
    const SPOT=!!r.sp.spots;
    const zbuf=new Float32Array(W*H).fill(Infinity), col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H), zw=new Float32Array(W*H);
    for (const f of F){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]), sh=shadeOf(n,B.se,B.ce);
      if (sh<0 && f.ds) sh=shadeOf([-n[0],-n[1],-n[2]],B.se,B.ce)*0.9;
      const fidx=sh*GAIN+BIAS+(f.b||0), M=MATS[f.mat]||MATS.flank, spotty=SPOT && f.mat==='back';
      for (let tt=1;tt+1<rv.length;tt++) fillTri(rv[0],rv[tt],rv[tt+1]);
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
            zbuf[i]=deff; dep[i]=d; zw[i]=w0*a.zr+w1*b.zr+w2*c.zr;
            const base=Math.floor(fidx), idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            const RM=(spotty && ((((x*73856093)^(y*19349663))>>>0)%6)===0) ? MATS.spot : M;
            col[i]=RM.ramp[Math.max(0,Math.min(4,idx))];
          }
        }
      }
    }
    const finD=MATS.fin.ramp[0];
    for (const e of eyes){
      const v=projVert(e[0],e[1],e[2],B), x=Math.round(v.sx), y=Math.round(v.sy);
      if (x<0||x>=W||y<0||y>=H) continue;
      const i=y*W+x; if (v.d-0.05<zbuf[i] && col[i]){ col[i]=finD; dep[i]=v.d; }
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
      if (r.spoil>0 && c!==KEY){
        const mixS=r.spoil*(0.40 + (BAYER[x&3][y&3] < r.spoil*0.55 ? 0.28 : 0));
        rr=rr*(1-mixS)+SPRGB[0]*mixS; gg=gg*(1-mixS)+SPRGB[1]*mixS; bb=bb*(1-mixS)+SPRGB[2]*mixS;
      }
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
  function mouth(dir, opts){
    const r=resolve(opts), L=r.sp.len*r.scale, y=L*0.78*r.stretch*0.5 + r.sp.girth*r.scale*0.9;
    const cP=Math.cos(r.pitch), sP=Math.sin(r.pitch);
    const v=projVert(0, y*cP, y*sP + r.zB, camBasis({dir, elev:opts&&opts.elev}));
    return { dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy) };
  }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir, elev})); return { dx:v.sx-cx, dy:v.sy-cy }; }
  function sheetOrder(){
    const o=[]; for (const a of AORDER) for (let f=0;f<ANIMS[a].n;f++) o.push({anim:a,f});
    for (const rk of RESTS) for (let f=0;f<RPOSE[rk].length;f++) o.push({rest:rk,f});
    return o;
  }
  // size x weight decides the carry — computed, so a new species inherits it
  function hold(spKey, scale){
    const sp=SPECIES[spKey]||SPECIES.mackerel, s=scale||1;
    const mass=sp.len*sp.girth*sp.girth*s*s*s*390*(sp.massK||1);
    return { mass:Math.round(mass*100)/100, hands: mass>=2.2 ? 2 : 1 };
  }
  function sizeOf(spKey, scale){ const sp=SPECIES[spKey]||SPECIES.mackerel, s=scale||1; return { len:+(sp.len*s).toFixed(3), px:+(sp.len*s*S).toFixed(1) }; }
  const dirOf=(h)=>mod(Math.round(h/(Math.PI/4)), 8);
  function mulberry(seed){ let a=seed>>>0; return function(){ a|=0; a=(a+0x6D2B79F5)|0;
    let t=Math.imul(a^(a>>>15),1|a); t=(t+Math.imul(t^(t>>>7),61|t))^t; return ((t^(t>>>14))>>>0)/4294967296; }; }
  /* RUNTIME SHOAL. n singles behind one leader on a lazy figure-eight (radius m, speed m/s).
     t in ms. Returns [{x,y,z (world metres from the shoal anchor), heading (rad, 0=N, CW),
     dir (8-dir bake), frame (swim), scale}] — the game blits FishIso2.render(dir,{species,
     anim:'swim',frame,scale}) at each. Deterministic per seed, so two clients agree. */
  function shoal(species, n, t, opts){
    opts=opts||{};
    const sp=SPECIES[species]||SPECIES.herring, L=sp.len*(opts.scale||1);
    const rng=mulberry((opts.seed||11)*2654435761), R=opts.radius!=null?opts.radius:1.2;
    const w=(opts.speed!=null?opts.speed:0.35)/R, tt=t/1000, a=w*tt;
    const lx=R*Math.sin(a), ly=R*0.55*Math.sin(2*a);
    const vx=R*w*Math.cos(a), vy=R*1.1*w*Math.cos(2*a);
    const head=Math.atan2(vx,vy), ch=Math.cos(head), sh=Math.sin(head);
    const out=[];
    for (let i=0;i<n;i++){
      const back=-(0.3+rng()*1.6)*L*(1+i/n*1.5), side=(rng()-0.5)*L*3.2*(0.35+i/n), lift=(rng()-0.5)*0.08;
      const ph=rng()*6.28, fr=0.6+rng()*0.8, sc=(opts.scale||1)*(0.85+rng()*0.3);
      const fwd=back+Math.cos(tt*fr*0.7+ph)*L*0.15, lat=side+Math.sin(tt*fr+ph)*L*0.25;
      const hd=head+Math.sin(tt*fr*1.3+ph)*0.18;
      out.push({ x:lx+fwd*sh+lat*ch, y:ly+fwd*ch-lat*sh, z:(opts.z!=null?opts.z:-0.15)+lift+Math.sin(tt*fr+ph)*0.01,
        heading:hd, dir:dirOf(hd), frame:mod(Math.floor(t/ANIMS.swim.ms*fr+ph*2), ANIMS.swim.n), scale:sc });
    }
    return out;
  }
  root.FishIso2 = { W, H, pivot:{x:cx,y:cy}, KEY, SPOIL, WATER, KEYLINE_DEFAULT, ORDER, SPECIES, ANIMS, AORDER, MOTION, POSE,
    RESTS, RPOSE, defaultElev:DEFAULT_ELEV, render, mouth, hold, sizeOf, project, shoal, dirOf, sheetOrder };
})(typeof globalThis!=='undefined'?globalThis:window);

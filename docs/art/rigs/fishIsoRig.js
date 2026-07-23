/* Hidden Harbours — FISH iso rig (M2 bake recipe, ADR-0006 — fishing iso Wave 3).
   Fish are rigs too: a parametric 3D loft baked on the SAME fixed 3/4 turntable as the
   fleet, the character and the rod (45deg steps CW, upper-left key, dither, 1px keyline,
   no AA, 32 px = 1 m). One body builder; each SPECIES is a data block — length, girth,
   lateral flatness, stripes, ramps (sampled from the catch icons) — so skins and sizes
   are edits to a table, never new art. `scale` scales any catch on the same skeleton.
   Cell 64x64, pivot (32,38) = THE WATER-SURFACE POINT under the body centre. Pose z is
   height of the spine over the surface; pixels below waterZ (default 0) bake with a
   depth-graded underwater tint + alpha, so the runtime never clips against water —
   same contract as bobberRig. waterZ:null bakes a plain dry turntable sprite (icons,
   tanks, trophies can re-bake from this rig later).
   ANIMS: swim 4f tail-beat under the surface · dart 2f stretched lunge · thrash 4f
   surface break (body C-curve + roll = belly flash, breaks above z=0) · shadow 2f deep
   pre-hook tease. render(dir,{species,anim,frame,scale,...}) also takes raw pose
   overrides (sweep,curve,roll,pitch,z). mouth(dir,opts) -> screen px offset of the
   mouth from the pivot (line attach in the fight).
   RESTS (dry bakes, waterZ off, diegetic handling — no icons in this game):
   deck 4 lay variants (on its side, fills containers + loose on deck) · gill 2f (held
   head-up, pivot = THE GRIP — pins to CharacterIso hand anchors like the rod) · tail 2f
   (held head-down) · cradle 2f (two-arm carry, pivot = mid-body). hold(species,scale)
   -> {mass kg, hands 1|2} from len×girth² — many species later, so it is computed.
   spoil 0..1 on ANY render mixes ramps toward SPOIL green with dither mottle (the
   rotten state — green particle FX stay runtime, colour = SPOIL).
   Exposes globalThis.FishIso = { W,H,pivot,KEY,SPOIL,ORDER,SPECIES,ANIMS,AORDER,RESTS,
   RPOSE,defaultElev,render,mouth,hold,project,sheetOrder }. */
(function (root) {
  const S = 32, DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const W = 64, H = 64, cx = 32, cy = 38;
  const KEY = '#101a19', WATER = '#123034';
  // base tones sampled from Art/Sprites/Fish/*.png — back / flank / belly / fin
  const SPECIES = {
    cod:      { label:'COD',      len:0.70, girth:0.105, flat:0.72, stripes:false,
                back:'#6c673b', flank:'#7d7649', belly:'#c2bc90', fin:'#54502c' },
    haddock:  { label:'HADDOCK',  len:0.55, girth:0.090, flat:0.68, stripes:false,
                back:'#525f68', flank:'#697379', belly:'#cdd3d2', fin:'#222b30' },
    pollock:  { label:'POLLOCK',  len:0.60, girth:0.092, flat:0.70, stripes:false,
                back:'#48543f', flank:'#55614f', belly:'#c2c8ba', fin:'#222a1e' },
    mackerel: { label:'MACKEREL', len:0.45, girth:0.068, flat:0.62, stripes:true,
                back:'#27564a', stripe:'#173b32', flank:'#7fa79c', belly:'#bcc6c2', fin:'#142e28' },
  };
  const ORDER = ['cod','haddock','pollock','mackerel'];
  const ANIMS = { swim:{n:4,ms:150}, dart:{n:2,ms:90}, thrash:{n:4,ms:110}, shadow:{n:2,ms:280} };
  const AORDER = ['swim','dart','thrash','shadow'];
  const POSE = {
    swim:   [{sweep:0.5,z:-0.16},{sweep:0,z:-0.15},{sweep:-0.5,z:-0.16},{sweep:0,z:-0.15}],
    dart:   [{sweep:0.22,stretch:1.1,z:-0.20},{sweep:-0.22,stretch:1.1,z:-0.20}],
    thrash: [{curve:0.8,roll:0.6,sweep:0.6,z:0.02},{curve:-0.8,roll:-0.6,sweep:-0.6,z:0.03},
             {curve:0.5,roll:1.1,sweep:0.4,z:0.05},{curve:-0.5,roll:-1.1,sweep:-0.4,z:0.02}],
    shadow: [{sweep:0.4,z:-0.50},{sweep:-0.4,z:-0.50}],
  };
  const HPI = Math.PI/2;
  const RPOSE = {   // dry rest poses — frame = lay/sway variant; gripU = pivot point on the spine
    deck:   [{roll:HPI,curve:0.25},{roll:-HPI,curve:-0.2},{roll:HPI,curve:-0.3},{roll:-HPI,curve:0.15}],
    gill:   [{pitch:1.48,curve:0.12,gripU:0.13},{pitch:1.48,curve:-0.15,gripU:0.13}],
    tail:   [{pitch:-1.48,curve:0.10,gripU:0.95},{pitch:-1.48,curve:-0.12,gripU:0.95}],
    cradle: [{roll:0.35,curve:0.10,gripU:0.5},{roll:-0.35,curve:-0.10,gripU:0.5}],
  };
  const RESTS = ['deck','gill','tail','cradle'];
  const SPOIL = '#7d9a46';
  const SPRGB = [0x7d,0x9a,0x46];

  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.12;
  const LN = (()=>{ const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  const hex2rgb=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  const rgb2hex=(r,g,b)=>'#'+[r,g,b].map(v=>Math.max(0,Math.min(255,Math.round(v))).toString(16).padStart(2,'0')).join('');
  const light=(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r+(255-r)*f, g+(255-g)*f, b+(255-b)*f); };
  const dark =(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r*f, g*f, b*f); };
  const mkRamp=(base)=>[dark(base,0.42), dark(base,0.66), base, light(base,0.17), light(base,0.34)];
  const WRGB = hex2rgb(WATER);

  const matsCache = {};
  function matsFor(key){
    if (matsCache[key]) return matsCache[key];
    const sp = SPECIES[key];
    const MATS = {
      back:{ramp:mkRamp(sp.back),off:0}, backS:{ramp:mkRamp(sp.stripe||sp.back),off:0},
      flank:{ramp:mkRamp(sp.flank),off:0}, belly:{ramp:mkRamp(sp.belly),off:-1},
      fin:{ramp:mkRamp(sp.fin),off:0},
    };
    const RINDEX = {};
    for (const m of Object.values(MATS)) m.ramp.forEach((c,i)=>{ RINDEX[c]={r:m.ramp,i}; });
    return matsCache[key] = { MATS, RINDEX };
  }

  function camBasis(opts){
    const dir=opts.dir||0, th=-dir*Math.PI/4;                 // CW azimuth — ADR-0006
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
    const spKey = SPECIES[opts.species] ? opts.species : 'mackerel';
    const spoil = Math.max(0, Math.min(1, opts.spoil||0));
    const scale = opts.scale||1;
    if (opts.rest && RPOSE[opts.rest]){
      const poses = RPOSE[opts.rest];
      const p = poses[(((opts.frame||0)%poses.length)+poses.length)%poses.length];
      const sp = SPECIES[spKey];
      return { spKey, sp, anim:opts.rest,
        sweep:0, curve:p.curve||0, roll:p.roll||0, pitch:p.pitch||0,
        zB: opts.rest==='deck' ? sp.girth*scale*sp.flat : 0,
        stretch:1, scale, gripU:p.gripU!=null?p.gripU:null,
        waterZ: opts.waterZ===undefined ? null : opts.waterZ, spoil };
    }
    const anim = POSE[opts.anim] ? opts.anim : 'swim';
    const poses = POSE[anim];
    const p = poses[(((opts.frame||0)%poses.length)+poses.length)%poses.length];
    return {
      spKey, sp:SPECIES[spKey], anim,
      sweep: opts.sweep!=null?opts.sweep:(p.sweep||0),
      curve: opts.curve!=null?opts.curve:(p.curve||0),
      roll:  opts.roll !=null?opts.roll :(p.roll ||0),
      pitch: opts.pitch!=null?opts.pitch:(p.pitch||0),
      zB:    opts.z    !=null?opts.z    :(p.z    ||0),
      stretch: p.stretch||1,
      scale, gripU:null,
      waterZ: opts.waterZ===undefined ? 0 : opts.waterZ, spoil,
    };
  }
  // ---- body loft --------------------------------------------------------------
  function facesOf(r){
    const { sp, sweep, curve, roll, pitch, zB, stretch, scale } = r;
    const L = sp.len*scale, bodyLen = L*0.78*stretch, tl = L*0.24, girth = sp.girth*scale;
    const g = (u)=>Math.max(0.012*scale, girth*(u<0.28 ? 0.4+0.6*(u/0.28) : 1-0.78*((u-0.28)/0.72)));
    const lat = (u)=> curve!==0
      ? curve*(4*(u-0.5)*(u-0.5)-0.5)*L*0.18 + sweep*Math.pow(Math.max(0,(u-0.45)/0.55),2)*L*0.10
      : sweep*Math.pow(Math.max(0,(u-0.45)/0.55),2)*L*0.22;
    const cR=Math.cos(roll), sR=Math.sin(roll), cP=Math.cos(pitch), sP=Math.sin(pitch);
    const T0 = (p)=>{                                      // roll (y-axis) → pitch (x-axis) → lift
      const x1=p[0]*cR - p[2]*sR, z1=p[0]*sR + p[2]*cR;
      const y2=p[1]*cP - z1*sP,  z2=p[1]*sP + z1*cP;
      return [x1, y2, z2+zB];
    };
    let T = T0;
    if (r.gripU!=null){                                    // held: the grip point IS the pivot
      const gp = T0([lat(r.gripU), bodyLen*(0.5-r.gripU), 0]);
      T = (p)=>{ const q=T0(p); return [q[0]-gp[0], q[1]-gp[1], q[2]-gp[2]]; };
    }
    const F=[], NSEG=9, NR=8;
    const rings=[];
    for (let i=0;i<=NSEG;i++){
      const u=i/NSEG, rr=g(u), y=bodyLen*(0.5-u), x0=lat(u);
      const ring=[];
      for (let k=0;k<NR;k++){
        const ph=k/NR*2*Math.PI;
        ring.push(T([x0+Math.cos(ph)*rr*sp.flat, y, Math.sin(ph)*rr]));
      }
      rings.push({ring, u, rr, y, x0});
    }
    for (let i=0;i<NSEG;i++){
      const r0=rings[i].ring, r1=rings[i+1].ring;
      for (let k=0;k<NR;k++){
        const k2=(k+1)%NR;
        const ms=(Math.sin(k/NR*2*Math.PI)+Math.sin(k2/NR*2*Math.PI))/2;
        let mat = ms>0.35 ? 'back' : ms<-0.35 ? 'belly' : 'flank';
        if (mat==='back' && sp.stripes && i%2===1) mat='backS';
        F.push({v:[r0[k],r0[k2],r1[k2],r1[k]], mat, b:0, db:0});
      }
    }
    const nose=T([lat(0), bodyLen*0.5+g(0)*0.9, 0]);       // nose cap fan
    const rN=rings[0].ring;
    for (let k=0;k<NR;k++){ const k2=(k+1)%NR;
      F.push({v:[nose,rN[k2],rN[k],rN[k]], mat:'flank', b:0, db:0}); }
    // tail fan — forked, swung with the sweep; thin double-sided quads
    const ty=(sweep*1.15+curve*0.5);
    const B=[lat(1), bodyLen*(0.5-1), 0], bd=[Math.sin(ty), -Math.cos(ty), 0];
    const TP=(sx,sy2,uz)=>T([B[0]+bd[0]*tl*sx, B[1]+bd[1]*tl*sx + 0*sy2, uz*tl]);
    for (const s of [1,-0.85]){
      F.push({v:[T(B), TP(1,0,0.62*s), TP(1.02,0,0.25*s), TP(0.55,0,0)], mat:'fin', b:-0.1, db:-0.05, ds:1});
    }
    // dorsal fin
    const d1=0.36, d2=0.58, h=girth*0.55;
    F.push({v:[T([lat(d1),bodyLen*(0.5-d1),g(d1)]), T([lat(d2),bodyLen*(0.5-d2),g(d2)]),
               T([lat(d2),bodyLen*(0.5-d2),g(d2)+h*0.6]), T([lat(d1),bodyLen*(0.5-d1),g(d1)+h])],
             mat:'fin', b:-0.1, db:-0.05, ds:1});
    // eye points (plotted post-paint)
    const ue=0.10, re=g(ue);
    const eyes=[T([lat(ue)+re*sp.flat*0.95, bodyLen*(0.5-ue), re*0.25]),
                T([lat(ue)-re*sp.flat*0.95, bodyLen*(0.5-ue), re*0.25])];
    return { F, eyes };
  }

  // ---- paint (rod-kit machinery + per-pixel world z for the water tint) -------
  function _paint(r, o){
    const B=camBasis(o);
    const {MATS,RINDEX}=matsFor(r.spKey);
    const {F,eyes}=facesOf(r);
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H), zw=new Float32Array(W*H);
    for (const f of F){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n,B.se,B.ce);
      if (sh<0 && f.ds) sh=shadeOf([-n[0],-n[1],-n[2]],B.se,B.ce)*0.9;
      const fidx=sh*GAIN+BIAS+(f.b||0);
      const M=MATS[f.mat]||MATS.flank;
      for (let tt=1;tt+1<rv.length;tt++) fillTri(rv[0],rv[tt],rv[tt+1]);
      function fillTri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if (Math.abs(area)<1e-6) return;
        for (let y=minY;y<=maxY;y++) for (let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          let w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          let w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          let w2=1-w0-w1;
          if (w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*W+x;
          if (deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d; zw[i]=w0*a.zr+w1*b.zr+w2*c.zr;
            let base=Math.floor(fidx);
            let idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    // eyes
    const finD=MATS.fin.ramp[0];
    for (const e of eyes){
      const v=projVert(e[0],e[1],e[2],B);
      const x=Math.round(v.sx), y=Math.round(v.sy);
      if (x<0||x>=W||y<0||y>=H) continue;
      const i=y*W+x;
      if (v.d-0.05<zbuf[i] && col[i]){ col[i]=finD; dep[i]=v.d; }
    }
    // depth-edge darkening
    const out=col.slice();
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x; if (!col[i]) continue;
      for (const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if (nx>=W||ny>=H) continue;
        const j=ny*W+nx; if (!col[j]) continue;
        if (Math.abs(dep[i]-dep[j])>EDGE){
          const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
          if (e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)];
        }
      }
    }
    // keyline (inherits the neighbour's world z so the water tint stays coherent)
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x; if (out[i]) continue;
      for (const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy;
        if (nx<0||nx>=W||ny<0||ny>=H) continue;
        const j=ny*W+nx;
        if (col[j]){ out[i]=KEY; zw[i]=zw[j]; break; }
      }
    }
    // RGBA + baked underwater tint (graded by depth) + spoil green (dither mottle)
    const rgba=new Uint8ClampedArray(W*H*4);
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x;
      const c=out[i]; if (!c){ rgba[i*4+3]=0; continue; }
      let [rr,gg,bb]=hex2rgb(c), a=255;
      if (r.spoil>0 && c!==KEY){
        const mixS=r.spoil*(0.40 + (BAYER[x&3][y&3] < r.spoil*0.55 ? 0.28 : 0));
        rr=rr*(1-mixS)+SPRGB[0]*mixS; gg=gg*(1-mixS)+SPRGB[1]*mixS; bb=bb*(1-mixS)+SPRGB[2]*mixS;
      }
      if (r.waterZ!=null && zw[i]<r.waterZ-0.004){
        const dz=r.waterZ-zw[i];
        const mix=Math.min(0.72, 0.30+dz*0.9);
        rr=rr*(1-mix)+WRGB[0]*mix; gg=gg*(1-mix)+WRGB[1]*mix; bb=bb*(1-mix)+WRGB[2]*mix;
        a = dz>0.35 ? 115 : 160;
      }
      rgba[i*4]=Math.round(rr); rgba[i*4+1]=Math.round(gg); rgba[i*4+2]=Math.round(bb); rgba[i*4+3]=a;
    }
    return rgba;
  }

  function render(dir, opts){
    const r=resolve(opts);
    return _paint(r, Object.assign({},opts,{dir}));
  }
  function mouth(dir, opts){
    const r=resolve(opts);
    const L=r.sp.len*r.scale, y=L*0.78*r.stretch*0.5 + r.sp.girth*r.scale*0.9;
    const cP=Math.cos(r.pitch), sP=Math.sin(r.pitch);
    const p=[0, y*cP, y*sP + r.zB];
    const v=projVert(p[0],p[1],p[2],camBasis({dir, elev:opts&&opts.elev}));
    return { dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy) };
  }
  // world point -> screen px offset from the pivot (FX registration)
  function project(dir, p, elev){
    const v=projVert(p[0],p[1],p[2],camBasis({dir, elev}));
    return { dx:v.sx-cx, dy:v.sy-cy };
  }
  function sheetOrder(){
    const o=[]; for (const a of AORDER) for (let f=0;f<ANIMS[a].n;f++) o.push({anim:a,f});
    for (const rk of RESTS) for (let f=0;f<RPOSE[rk].length;f++) o.push({rest:rk,f});
    return o;
  }
  // size × weight decides the carry — computed, because more species are coming
  function hold(spKey, scale){
    const sp = SPECIES[spKey] || SPECIES.mackerel, s = scale||1;
    const mass = sp.len*sp.girth*sp.girth*s*s*s*390;
    return { mass: Math.round(mass*10)/10, hands: mass>=2.2 ? 2 : 1 };
  }
  root.FishIso = { W, H, pivot:{x:cx,y:cy}, KEY, SPOIL, ORDER, SPECIES, ANIMS, AORDER,
    RESTS, RPOSE, defaultElev:DEFAULT_ELEV, render, mouth, hold, project, sheetOrder };
})(typeof globalThis!=='undefined'?globalThis:window);

/* Hidden Harbours — parametric ROCK family (iso shore rock props). SUPERSEDES the near-plan
   Art/Sprites/Shore/Rock{Small,Mid,Cluster}.png + TidePoolRock.png and ShoreIso.boulder/stack.

   Projection: the ADR-0006/0022 camera — ¾ from the SOUTH at 40° elevation, orthographic.
   Ground depth foreshortens by sin40 (0.643), height by cos40 (0.766); depth key along the view
   axis so a short near rock correctly occludes a tall far one. 32 px = 1 m. Upper-left key light.
   Solid bands + Bayer dither at band edges only, no AA, binary alpha, 1 px #171009 keyline.
   Pivot = BOTTOM-CENTRE GROUND CONTACT (the tile the rock stands on), NOT the bbox centre.

   Not drawings — a tiny iso renderer. Every species builds a set of superellipsoid LUMPS
   (rx,ry,rz + angularity exponent + hash relief); each lump rasterises its top surface and drops
   a column to the ground, so silhouette, self-occlusion, and the near flank all fall out of the
   geometry. Bedding strata are a function of world Z, so the beds WRAP the volume like real
   sedimentary rock instead of being painted stripes. Cracks are cell-noise on (worldX, worldZ).

   SPECIES (parametric GENERATORS):
     Erratic   — single shore boulder sitting on sand/grass
     Outcrop   — 2–5 boulder cluster, shoreline edge dressing
     PoolLedge — flat shelf with a tide-pool BASIN cut into the top plate
     Skerry    — awash hazard rock; `waterline` clips it at the sea plane
     Cloven    — split landmark rock, chart-mark scale (flat sheer cut face)
     Cobble    — beach cobble / shingle scatter, tiny filler

   PARAMS (uniform space, read differently per species):
     sizeM 0.3–3 (span) · lumps 1–14 (boulders / plates / cobbles) · angular 0–1 (rounded ↔
     fractured & flat-topped) · bedding 0–1 (strata line strength) · waterline 0–0.85 (fraction of
     height below the sea plane; >0 ⇒ the rock is CLIPPED there and gains a wet-glint band)
     dress 0|1|2 = bare | barnacled | weeded  — the DRESSING AXIS, real pixels, sheet rows
     seed — fixed per shipped variant; each variant is its own baked cell (no mirroring).

   RAMPS = STONE TYPE × TIDE STATE (colour + a structure reweight; the build stays seed-stable):
     stone  sandstone — PEI red, ShoreIso PAL.redrock VERBATIM: bedded, the cliff/ledge stone
            granite   — ice-dropped erratic: rounded, unbedded, coarse speckle + feldspar fleck
            basalt    — dark blue-grey: angular, vertical COLUMNAR jointing
            quartzite — pale: angular, bright quartz VEINS on the facet breaks
     tide   dry   — above the wrack line, sun-dry
            wet   — below the tide mark: darkened + cooled toward DepthRamp deep
            awash — sea-glazed: wet, with the upper steps lifted toward DepthRamp shallow #8EA59C
   NO WATER OR FOAM IS EVER BAKED (ADR-0010/0012/0023 — the shader owns the sea, the foam, and
   the tide-pool fill). `waterline` is a CLIP; the tide pool bakes an empty damp BASIN + ships the
   basin rect so the shader can fill it.

   ANCHORS AS DATA (per variant, measured off the built volume, shipped in the sidecar):
     footprint — collision ellipse (rx,ry in m) + ground contact px
     perch     — highest standable point (gull / fisher / fox); flat=false ⇒ decorative only
     snags     — 2–3 outer silhouette catches for rope & pot lines, ≥60° apart, near the tide mark
     hazard    — awash danger radius in m (waterline > 0 only)
     pool      — tide-pool basin rect + depth (PoolLedge only) — the shader's fill target
     weedLine  — the tide mark: screen row + height in m where rockweed drapes

   Exposes globalThis.RockIso = { PPU, ELEV, Q, HZ, KEYLINE, SPECIES, STONES, TIDES, RAMPS, DRESS,
     render(species, opts, tideKey) -> { w, h, rgba:Uint8ClampedArray, anchors, params } }
     opts: { variant: 0..n-1, dress: 0|1|2, stone: 'sandstone'|'granite'|'basalt'|'quartzite' }
     or a params object. RAMPS[stone][tide].
   Runs in the run_script sandbox (bake) and the browser (live preview). */
(function (root) {
  const PPU = 32, ELEV = 40 * Math.PI / 180;
  const Q = Math.sin(ELEV), HZ = Math.cos(ELEV);          // depth squash / height scale @ 40°
  const KEYLINE = '#171009';                               // ShoreIso PAL.KEY (v)
  const DRESS = ['bare', 'barnacled', 'weeded'];

  const SPECIES = {
    Erratic: { name:'Erratic', cell:{w:52,h:44}, note:'Single shore boulder — one rounded mass, sometimes a shoulder stone',
      reads:{ lumps:'shoulder stones', angular:'rounded ↔ fractured' },
      variants:[
        { seed:5101, sizeM:0.95, lumps:1, angular:0.30, bedding:0.55, waterline:0, dress:0 },
        { seed:5102, sizeM:1.30, lumps:2, angular:0.55, bedding:0.70, waterline:0, dress:1 },
        { seed:5103, sizeM:0.62, lumps:1, angular:0.18, bedding:0.40, waterline:0, dress:0 },
        { seed:5104, sizeM:1.10, lumps:2, angular:0.80, bedding:0.85, waterline:0, dress:2 } ] },
    Outcrop: { name:'Outcrop', cell:{w:88,h:60}, note:'Boulder cluster for shoreline edges — leaning masses, wedged gaps',
      reads:{ lumps:'boulders in the cluster', angular:'rounded ↔ fractured' },
      variants:[
        { seed:5201, sizeM:1.9, lumps:3, angular:0.45, bedding:0.60, waterline:0,    dress:1 },
        { seed:5202, sizeM:2.4, lumps:5, angular:0.65, bedding:0.75, waterline:0,    dress:2 },
        { seed:5203, sizeM:1.5, lumps:2, angular:0.35, bedding:0.50, waterline:0.15, dress:0 } ] },
    PoolLedge: { name:'Pool Ledge', cell:{w:80,h:48}, note:'Flat wave-cut plate with a tide-pool basin — bakes the hollow, not the water',
      reads:{ lumps:'rim stones', angular:'plate edge squareness' },
      variants:[
        { seed:5301, sizeM:1.8, lumps:2, angular:0.80, bedding:0.85, waterline:0,    dress:2 },
        { seed:5302, sizeM:2.2, lumps:3, angular:0.92, bedding:0.70, waterline:0.10, dress:1 },
        { seed:5303, sizeM:1.4, lumps:1, angular:0.70, bedding:0.60, waterline:0,    dress:0 } ] },
    Skerry: { name:'Skerry', cell:{w:104,h:52}, note:'Awash hazard rock — clipped at the sea plane, wet-glint band at the cut',
      reads:{ lumps:'heads breaking the surface', angular:'weathered ↔ sheer' },
      variants:[
        { seed:5401, sizeM:2.2, lumps:2, angular:0.55, bedding:0.65, waterline:0.45, dress:2 },
        { seed:5402, sizeM:3.0, lumps:3, angular:0.70, bedding:0.75, waterline:0.60, dress:2 },
        { seed:5403, sizeM:1.6, lumps:1, angular:0.40, bedding:0.55, waterline:0.30, dress:1 },
        { seed:5404, sizeM:2.6, lumps:2, angular:0.85, bedding:0.80, waterline:0.72, dress:0 } ] },
    Cloven: { name:'Cloven Rock', cell:{w:60,h:76}, note:'Split landmark — two halves, sheer inner faces, chart-mark scale',
      reads:{ lumps:'rubble at the foot', angular:'cut-face squareness' },
      variants:[
        { seed:5501, sizeM:1.5, lumps:2, angular:0.75, bedding:0.90, waterline:0,    dress:1 },
        { seed:5502, sizeM:1.9, lumps:3, angular:0.88, bedding:0.80, waterline:0,    dress:0 },
        { seed:5503, sizeM:1.2, lumps:1, angular:0.62, bedding:0.70, waterline:0.20, dress:2 } ] },
    Cobble: { name:'Cobble Scatter', cell:{w:52,h:32}, note:'Beach cobbles & shingle — tiny filler, scatter freely',
      reads:{ lumps:'stones in the scatter', angular:'rolled ↔ broken shingle' },
      variants:[
        { seed:5601, sizeM:0.9, lumps:7,  angular:0.25, bedding:0.30, waterline:0, dress:0 },
        { seed:5602, sizeM:1.3, lumps:11, angular:0.55, bedding:0.35, waterline:0, dress:1 },
        { seed:5603, sizeM:0.7, lumps:5,  angular:0.15, bedding:0.25, waterline:0, dress:0 } ] },
  };

  // ---- colour ---------------------------------------------------------------
  function h2r(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function r2h(c){ return '#'+c.map(v=>Math.max(0,Math.min(255,Math.round(v))).toString(16).padStart(2,'0')).join(''); }
  function mix(a,b,t){ const A=h2r(a),B=h2r(b); return r2h([A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t]); }

  // ShoreIso PAL.redrock VERBATIM (7) + one derived deep step for the undercut shadow.
  const ROCK_DRY = ['#33150e','#4a1e14','#6b2c1c','#87301f','#a04528','#bd6038','#d17c4c','#e0a06e'];
  const DEEP = '#0F2227', SHALLOW = '#8EA59C';             // repo DepthRamp endpoints (v)
  const BONE = ['#a89878','#d9cdb6','#e9e6df'];            // CottageIso whites + bone (v) — barnacle
  const WEED = ['#242f1c','#3b4b2b','#586b38'];            // driftWeedRig LIVING Bladderwrack (v)

  function ramp8(a,b,c){                                   // 8 steps through three anchor hexes
    const out=[]; for(let i=0;i<8;i++){ const t=i/7;
      out.push(t<0.5 ? mix(a,b,t*2) : mix(b,c,(t-0.5)*2)); } return out;
  }

  // STONE TYPE — colour AND structure. Each type reweights bedding / angularity / grain so the
  // four read apart in silhouette and texture, not just in hue.
  const STONES = {
    sandstone: { name:'Red Sandstone', steps:ROCK_DRY, bedding:1.00, angular:0.00, grain:0.030,
      _prov:'ShoreIso PAL.redrock verbatim (the cliff + ledge tiles); deep step = redrock[0] × KEY 0.28' },
    granite:   { name:'Granite', steps:ramp8('#1b2a2c','#5d7169','#d8ded4'), bedding:0.22, angular:-0.14, grain:0.115,
      fleck:'#bd6038', _prov:'DepthRamp deep → shallow → bone; feldspar fleck = redrock[5] (v). Ice-dropped erratic — rounded, unbedded, coarsely speckled' },
    basalt:    { name:'Basalt', steps:ramp8('#0d1518','#2c3f45','#7d8f92'), bedding:0.50, angular:0.26, grain:0.035,
      columnar:true, _prov:'DepthRamp deep #0F2227 cooled; top step held short of shallow #8EA59C. Columnar — vertical jointing' },
    quartzite: { name:'Quartzite', steps:ramp8('#2e2a26','#8d8272','#f0e9db'), bedding:0.40, angular:0.20, grain:0.055,
      vein:'#f6f2e7', _prov:'CottageIso whites #a89878/#d9cdb6 + bone #e9e6df (v); quartz veins on the facet breaks' },
  };

  function tideRamp(kind, ST){
    const steps = ST.steps.map((c,i)=>{
      if(kind==='dry') return c;
      let x = mix(mix(c, KEYLINE, 0.30), DEEP, 0.12);      // wet: darker + cooled
      if(kind==='awash' && i>=5) x = mix(x, SHALLOW, 0.16+0.06*(i-5)); // sea glaze on the lit steps
      return x;
    });
    const wf = kind==='dry' ? 0 : (kind==='awash' ? 0.34 : 0.20);
    const mid = ST.steps[2];
    return {
      key: KEYLINE, steps,
      barn: BONE.map(c => kind==='dry' ? c : mix(mix(c, KEYLINE, 0.22), SHALLOW, wf*0.5)),
      weed: WEED.map(c => kind==='dry' ? mix(c, KEYLINE, 0.28) : (kind==='awash' ? mix(c, SHALLOW, 0.16) : c)),
      glint: kind==='dry' ? ST.steps[7] : mix(SHALLOW, '#eaf3ee', kind==='awash'?0.45:0.15),
      basin: kind==='dry' ? mix(mid, KEYLINE, 0.30) : mix(mix(mid, KEYLINE, 0.42), DEEP, 0.30),
      fleck: ST.fleck || null, vein: ST.vein || null,
      _prov: kind==='dry' ? ST._prov : 'dry × KEY 0.30 × DepthRamp deep 0.12' + (kind==='awash' ? '; lit steps × DepthRamp shallow' : ''),
    };
  }
  const TIDES = ['dry','wet','awash'];
  const RAMPS = {};
  for(const s in STONES){ RAMPS[s]={}; for(const t of TIDES) RAMPS[s][t]=tideRamp(t,STONES[s]); }

  // ---- rng / dither ---------------------------------------------------------
  function mulberry(a){ return function(){ a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296; }; }
  function hash2(x,y,s){ let h=(x*374761393 + y*668265263 + ((s|0))*2246822519)|0; h=Math.imul(h^(h>>>13),1274126177); return ((h^(h>>>16))>>>0)/4294967296; }
  const B4=[[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]];
  // Solid bands with dither ONLY in the transition zone — a constant fraction across a flat plane
  // would otherwise fill it with a visible checkerboard (the kit's law: band edges only).
  function pick(ramp,f,x,y){ f=Math.max(0,Math.min(ramp.length-1,f)); const i=Math.floor(f), fr=f-i;
    const hi=ramp[Math.min(ramp.length-1,i+1)];
    if(fr<0.34) return ramp[i];
    if(fr>0.66) return hi;
    return ((B4[y&3][x&3]+0.5)/16 < (fr-0.34)/0.32) ? hi : ramp[i]; }

  // ---- cell buffers ---------------------------------------------------------
  // mask: 0 empty · 1 rock · 2 barnacle · 3 weed · 4 basin floor
  function Cell(w,h){ this.w=w; this.h=h; const n=w*h;
    this.mask=new Uint8Array(n); this.zb=new Float32Array(n).fill(-1e9);
    this.lit=new Float32Array(n); this.hz=new Float32Array(n);
    this.wx=new Float32Array(n); this.wy=new Float32Array(n); this.top=new Uint8Array(n); }

  const LV=(function(){ const v=[-0.55,0.45,0.70], m=Math.hypot(v[0],v[1],v[2]); return v.map(c=>c/m); })();
  // key light + a sky/bounce term, so the south-facing flank sits in shadow without going to soot
  function lam(nx,ny,nz){ const m=Math.hypot(nx,ny,nz)||1; nx/=m; ny/=m; nz/=m;
    return 0.80*Math.max(0,nx*LV[0]+ny*LV[1]+nz*LV[2]) + 0.20*(0.42+0.58*nz); }

  function paintLump(c,L,cx,baseY,y0){
    const ang=Math.min(1,0.26+L.ang*0.74);                 // nothing is a perfect ovoid
    const e1=1+ang*1.6, e2=0.5-ang*0.18, sd=L.seed|0;
    // relief = a few BIG facets (low frequency), leaned off-axis, tapered to nothing at the rim so
    // the silhouette stays a clean stone instead of fraying into gravel.
    const ta=hash2(3,7,sd)*6.283, tilt=0.16+0.16*hash2(5,11,sd);
    const relief=(X,Y)=>{ const u=X/L.rx, v=Y/L.ry, r2=Math.min(1,u*u+v*v);
      const n1=(hash2(Math.round(X*0.24),Math.round(Y*0.24),sd)-0.5)*2;
      const n2=(hash2(Math.round(X*0.58),Math.round(Y*0.58),sd+7)-0.5)*2;
      const lean=tilt*(u*Math.cos(ta)+v*Math.sin(ta))*(1-ang*0.55);
      return Math.max(0.3, 1+lean+(n1*0.12+n2*0.045)*(0.45+ang)*(1-r2*0.5)); };
    const T0=(X,Y)=>{ const u=X/L.rx, v=Y/L.ry, r2=u*u+v*v; if(r2>=1) return 0;
      return L.rz*Math.pow(Math.max(0,1-Math.pow(r2,e1)),e2); };
    const T=(X,Y)=>T0(X,Y)*relief(X,Y);
    const st=0.5, cl=L.clip;
    for(let Yd=L.ry; Yd>=-L.ry; Yd-=st){
      for(let X=-L.rx; X<=L.rx; X+=st){
        if(cl && (X<cl[0] || X>cl[1])) continue;
        const tz=T(X,Yd); if(tz<=0.02) continue;
        const qs=1.0+1.9*ang, tq=Math.max(qs*0.55, (Math.floor(tz/qs)+0.5)*qs);   // terraced ledges
        // normal comes MOSTLY from the smooth base form — relief only nudges it, or the surface
        // shatters into per-pixel noise and the stone reads as gravel.
        const d=0.9;
        const gx=0.74*(T0(X+d,Yd)-T0(X-d,Yd))/(2*d) + 0.26*(T(X+d,Yd)-T(X-d,Yd))/(2*d);
        const gy=0.74*(T0(X,Yd+d)-T0(X,Yd-d))/(2*d) + 0.26*(T(X,Yd+d)-T(X,Yd-d))/(2*d);
        const onCut = cl && (X-cl[0]<st*1.5 || cl[1]-X<st*1.5);
        const cutS = cl && (cl[1]-X<st*1.5) ? 1 : -1;
        const Xw=L.x+X, Yw=L.y+Yd, Zt=L.z0+tq;
        const sx=Math.round(cx+Xw);
        if(sx<0||sx>=c.w) continue;
        const syTop=Math.round(baseY - Q*(Yw+y0) - HZ*Zt), syBot=Math.round(baseY - Q*(Yw+y0) - HZ*L.zg);
        const un=X/L.rx, vn=Yd/L.ry, nl=Math.hypot(un,vn)||1;
        const litTop=lam(-gx,-gy,1), litSide=onCut?lam(cutS,0,0.18):lam(un/nl,vn/nl,0.30);
        for(let sy=syTop; sy<=syBot; sy++){
          if(sy<0||sy>=c.h) continue;
          const i=sy*c.w+sx;
          const Zp = sy===syTop ? Zt : (baseY - Q*(Yw+y0) - sy)/HZ;
          const key = -(HZ*Yw - Q*Zp);                       // view-axis depth: larger = nearer
          if(c.mask[i] && c.zb[i]>=key) continue;
          c.mask[i]=1; c.zb[i]=key; c.hz[i]=Zp; c.wx[i]=Xw; c.wy[i]=Yw;
          c.top[i]= sy===syTop ? 1 : 0; c.lit[i]= sy===syTop ? litTop : litSide;
        }
      }
    }
  }

  // Break the lit surface into FLAT PLANES. A smooth ellipsoid reads as a loaf; rock reads as a
  // few big facets meeting at hard edges. Cells are a jittered lattice over (worldX, depth, Z),
  // each gets the mean of its lambert plus a small tonal offset so neighbouring planes separate.
  function facetise(c,seed){
    const n=c.w*c.h, fid=new Int32Array(n), S=7.0, sum={}, cnt={};
    for(let i=0;i<n;i++){ if(!c.mask[i]) continue;
      const a=Math.floor((c.wx[i]+2.4*Math.sin(c.hz[i]*0.31+seed))/S);
      const b=Math.floor(c.wy[i]/(S*1.35));
      const d=Math.floor((c.hz[i]+2.0*Math.sin(c.wx[i]*0.26+seed*1.7))/S);
      const k=(Math.imul(a,73856093)^Math.imul(b,19349663)^Math.imul(d,83492791))|0;
      fid[i]=k; sum[k]=(sum[k]||0)+c.lit[i]; cnt[k]=(cnt[k]||0)+1; }
    for(let i=0;i<n;i++){ if(!c.mask[i]) continue; const k=fid[i];
      const j=(hash2(k&1023,(k>>>10)&1023,seed)-0.5)*0.075;
      c.lit[i]=Math.max(0,Math.min(1, 0.18*c.lit[i]+0.82*(sum[k]/cnt[k]) + j)); }
    c.fid=fid;
  }

  // ---- species generators (return { lumps, tideZ, pool }) -------------------
  function base(p,rng,S){ return { span:Math.max(6,p.sizeM*PPU), rng }; }

  function genErratic(p,rng,S){
    const R=p.sizeM*PPU/2, lumps=[];
    lumps.push({ x:0, y:0, z0:0, zg:0, rx:R, ry:R*(0.74+rng()*0.14), rz:R*(0.80+rng()*0.34), ang:p.angular, seed:p.seed });
    for(let i=1;i<Math.min(3,p.lumps);i++){ const a=rng()*6.28, d=R*(0.48+rng()*0.26), r=R*(0.22+rng()*0.16);
      lumps.push({ x:Math.cos(a)*d, y:Math.sin(a)*d*0.8, z0:0, zg:0, rx:r, ry:r*0.8, rz:r*(0.55+rng()*0.4), ang:Math.min(1,p.angular+0.15), seed:p.seed+i*7 }); }
    const top=Math.max.apply(null,lumps.map(l=>l.z0+l.rz));
    return { lumps, tideZ: top*0.42 };
  }

  function genOutcrop(p,rng,S){
    const R=p.sizeM*PPU/2, n=Math.max(2,Math.round(p.lumps)), lumps=[];
    for(let i=0;i<n;i++){ const t=i/Math.max(1,n-1)-0.5;
      const r=R*(0.24+rng()*0.20)*(i===0?1.2:1);
      lumps.push({ x:t*R*0.95+(rng()-0.5)*R*0.22, y:(rng()-0.5)*R*0.55, z0:0, zg:0,
        rx:r, ry:r*(0.72+rng()*0.2), rz:r*(0.78+rng()*0.60), ang:Math.min(1,p.angular*(0.8+rng()*0.5)), seed:p.seed+i*13 }); }
    const top=Math.max.apply(null,lumps.map(l=>l.z0+l.rz));
    return { lumps, tideZ: top*0.38 };
  }

  function genPoolLedge(p,rng,S){
    const R=p.sizeM*PPU/2, lumps=[];
    const plate={ x:0, y:0, z0:0, zg:0, rx:R, ry:R*0.72, rz:R*(0.15+rng()*0.05), ang:0.95, seed:p.seed };
    lumps.push(plate);
    for(let i=1;i<Math.max(1,Math.round(p.lumps));i++){ const a=(i/p.lumps)*6.28+rng(), d=R*(0.74+rng()*0.22), r=R*(0.16+rng()*0.12);
      lumps.push({ x:Math.cos(a)*d, y:Math.sin(a)*d*0.7, z0:plate.rz*0.42, zg:0, rx:r, ry:r*0.8, rz:r*(0.7+rng()*0.5), ang:Math.min(1,p.angular+0.1), seed:p.seed+i*17 }); }
    return { lumps, tideZ: plate.rz*0.70,
      pool:{ x:(rng()-0.5)*R*0.34, y:(rng()-0.5)*R*0.20, rx:R*(0.20+rng()*0.07), ry:R*0.13, depth:plate.rz*0.75 } };
  }

  function genSkerry(p,rng,S){
    const R=p.sizeM*PPU/2, n=Math.max(1,Math.round(p.lumps)), lumps=[];
    for(let i=0;i<n;i++){ const t=n===1?0:(i/(n-1)-0.5);
      const r=R*(0.32+rng()*0.26)*(i===0?1.2:0.9);
      lumps.push({ x:t*R*0.90+(rng()-0.5)*R*0.22, y:(rng()-0.5)*R*0.45, z0:0, zg:0,
        rx:r, ry:r*(0.66+rng()*0.2), rz:r*(0.62+rng()*0.38), ang:Math.min(1,0.35+p.angular*0.65), seed:p.seed+i*11 }); }
    const top=Math.max.apply(null,lumps.map(l=>l.z0+l.rz));
    return { lumps, tideZ: Math.max(top*p.waterline+1.5, top*0.5) };
  }

  function genCloven(p,rng,S){
    const R=p.sizeM*PPU/2, gap=1.2, lean=(rng()-0.5)*0.16, lumps=[];
    const H=R*(1.45+rng()*0.40), ang=Math.min(1,0.15+p.angular*0.32);
    // ONE mass split down the middle: halves overlap in depth and are cut flat at ±gap/2
    lumps.push({ x:-R*0.34, y:1.3, z0:0, zg:0, rx:R*0.66, ry:R*0.56, rz:H, ang, seed:p.seed,
      clip:[-R, R*0.34-gap/2] });
    lumps.push({ x:R*0.30, y:-1.3, z0:0, zg:0, rx:R*0.60, ry:R*0.52, rz:H*(0.76+lean), ang, seed:p.seed+31,
      clip:[-R*0.30+gap/2, R] });
    for(let i=2;i<Math.max(1,Math.round(p.lumps))+1;i++){ const a=rng()*6.28, d=R*(0.62+rng()*0.26), r=R*(0.14+rng()*0.10);
      lumps.push({ x:Math.cos(a)*d, y:Math.abs(Math.sin(a))*d*0.5-R*0.2, z0:0, zg:0, rx:r, ry:r*0.8, rz:r*0.7, ang:0.7, seed:p.seed+i*19 }); }
    return { lumps, tideZ: H*0.26 };
  }

  function genCobble(p,rng,S){
    const R=p.sizeM*PPU/2, n=Math.max(3,Math.round(p.lumps)), lumps=[];
    const cols=Math.ceil(Math.sqrt(n*1.5)), rows=Math.ceil(n/cols);
    for(let i=0;i<n;i++){ const gx=(i%cols)/Math.max(1,cols-1)-0.5, gy=(Math.floor(i/cols))/Math.max(1,rows-1)-0.5;
      const r=3.0+rng()*3.0;
      lumps.push({ x:gx*R*1.45+(rng()-0.5)*R*0.34, y:gy*R*0.90+(rng()-0.5)*R*0.28, z0:0, zg:0,
        rx:r, ry:r*(0.74+rng()*0.16), rz:r*(0.46+rng()*0.24), ang:Math.min(1,0.1+p.angular*0.8), seed:p.seed+i*23 }); }
    return { lumps, tideZ: 3.6 };
  }

  const GEN = { Erratic:genErratic, Outcrop:genOutcrop, PoolLedge:genPoolLedge, Skerry:genSkerry, Cloven:genCloven, Cobble:genCobble };

  // ---- dressing + clip ------------------------------------------------------
  function carvePool(c,cx,baseY,pool,seed){
    if(!pool) return null;
    let x0=1e9,y0=1e9,x1=-1e9,y1=-1e9;
    for(let y=0;y<c.h;y++)for(let x=0;x<c.w;x++){ const i=y*c.w+x; if(c.mask[i]!==1||!c.top[i]) continue;
      const dx=(c.wx[i]-pool.x)/pool.rx, dy=(c.wy[i]-pool.y)/pool.ry;
      const w=dx*dx+dy*dy + (hash2(x,y,seed+5)-0.5)*0.22;
      if(w>1) continue;
      c.mask[i]=4; c.hz[i]-=pool.depth*(1-w)*0.8; c.lit[i]=0.30+0.16*(1-w);
      if(x<x0)x0=x; if(y<y0)y0=y; if(x>x1)x1=x; if(y>y1)y1=y; }
    if(x1<x0) return null;
    return { x:x0, y:y0, w:x1-x0+1, h:y1-y0+1, depthM:+(pool.depth/PPU).toFixed(2) };
  }

  function clipWaterline(c,waterZ){
    for(let i=0;i<c.mask.length;i++) if(c.mask[i] && c.hz[i]<waterZ) c.mask[i]=0;
  }

  function dressRock(c,p,tideZ,seed){
    if(p.dress===1){                                                     // barnacle crust, tide band
      for(let y=0;y<c.h;y++)for(let x=0;x<c.w;x++){ const i=y*c.w+x; if(c.mask[i]!==1) continue;
        const z=c.hz[i]; if(z<tideZ*0.70 || z>tideZ*1.65) continue;
        const clump=hash2(Math.floor(x/4),Math.floor(y/3),seed+41);
        if(clump<0.24 && hash2(x,y,seed+42)<0.58 && c.lit[i]>0.24) c.mask[i]=2; }
    } else if(p.dress===2){                                              // rockweed drape at the tide mark
      const lim=tideZ*1.15;
      for(let y=0;y<c.h;y++)for(let x=0;x<c.w;x++){ const i=y*c.w+x; if(c.mask[i]!==1) continue;
        const z=c.hz[i], edge=lim*(0.80+0.42*hash2(x,0,seed+51));
        if(z<edge && hash2(x,y,seed+52)<0.86) c.mask[i]=3; }
      for(let x=0;x<c.w;x++){                                            // fronds hanging past the rim
        if(hash2(x,7,seed+53)>0.34) continue;
        let low=-1; for(let y=0;y<c.h;y++){ const i=y*c.w+x; if(c.mask[i]===3) low=y; }
        if(low<0) continue;
        const n=1+Math.floor(hash2(x,9,seed+54)*3);
        for(let k=1;k<=n;k++){ const yy=low+k; if(yy>=c.h) break; const i=yy*c.w+x;
          if(c.mask[i]) break; c.mask[i]=3; c.hz[i]=Math.max(0,c.hz[low*c.w+x]-k/HZ); c.lit[i]=0.34; }
      }
    }
  }

  // ---- colourise ------------------------------------------------------------
  function colourise(c,R,p,tideZ,waterZ,ST){
    const {w,h}=c, out=new Uint8ClampedArray(w*h*4), NS=R.steps.length-1, sd=p.seed|0;
    for(let y=0;y<h;y++)for(let x=0;x<w;x++){
      const i=y*w+x, m=c.mask[i]; if(!m){ out[i*4+3]=0; continue; }
      let hx;
      if(m===1||m===4){
        const li=Math.pow(c.lit[i],0.82);
        let f=0.5+li*5.9;
        const bz=(c.hz[i]+1.3*Math.sin(c.wx[i]*0.19+sd))/5.0;            // beds wrap the volume
        const bf=bz-Math.floor(bz), bw=p.bedding*ST.bedding;
        if(bf<0.30) f-=1.7*bw; else if(bf<0.52) f+=0.9*bw;
        if(ST.columnar){                                                 // basalt: vertical jointing
          const cj=(c.wx[i]+2.2*Math.sin(c.hz[i]*0.11+sd))/5.6;
          const cf=cj-Math.floor(cj);
          if(cf<0.16) f-=1.5; else if(cf<0.30) f+=0.6;
        }
        const lft=i-1, up=i-w;                                           // hard edges between facets
        const brk = (x>0 && c.mask[lft] && c.fid[i]!==c.fid[lft]);
        if(brk) f-=1.35;
        if(y>0 && c.mask[up] && c.fid[i]!==c.fid[up]) f-=0.8;
        if(y>0 && c.mask[up] && Math.abs(c.hz[i]-c.hz[up])>3.2) f-=1.1;  // ledge undercut
        f-=1.8*Math.exp(-c.hz[i]/2.2);                                   // contact occlusion
        if(m===4) f-=1.7;
        const n=hash2(x,y,sd+3);
        if(n<ST.grain) f-=0.7; else if(n>1-ST.grain*0.9) f+=0.7;
        if(waterZ>0 && c.hz[i]<waterZ+1.9) f+=1.6;                       // wet glint at the sea cut
        hx = m===4 && f<1.4 ? R.basin : pick(R.steps,f,x,y);
        if(R.fleck && m===1 && hash2(x,y,sd+77)<0.030 && c.lit[i]>0.30) hx=R.fleck;   // feldspar
        if(R.vein && brk && hash2(c.fid[i]&2047,(c.fid[i]>>>11)&2047,sd+88)<0.17) hx=R.vein;              // quartz vein
      } else if(m===2){
        const f=0.55+c.lit[i]*1.7 + (hash2(x,y,sd+61)-0.5)*0.7;
        hx = pick(R.barn,f,x,y);
      } else {
        const f=(0.2+0.8*c.lit[i])*2 + (hash2(x,y,sd+71)-0.5)*1.0;
        hx = pick(R.weed,f,x,y);
      }
      const rgb=h2r(hx); out[i*4]=rgb[0]; out[i*4+1]=rgb[1]; out[i*4+2]=rgb[2]; out[i*4+3]=255;
    }
    // 1 px keyline rim (sprites only)
    const K=h2r(R.key), solid=(x,y)=>x>=0&&x<w&&y>=0&&y<h&&c.mask[y*w+x]>0;
    for(let y=0;y<h;y++)for(let x=0;x<w;x++){ const i=y*w+x; if(!c.mask[i]) continue;
      if(solid(x-1,y)&&solid(x+1,y)&&solid(x,y-1)&&solid(x,y+1)) continue;
      out[i*4]=Math.round(out[i*4]*0.32+K[0]*0.68);
      out[i*4+1]=Math.round(out[i*4+1]*0.32+K[1]*0.68);
      out[i*4+2]=Math.round(out[i*4+2]*0.32+K[2]*0.68); }
    return out;
  }

  // ---- anchors --------------------------------------------------------------
  function anchorsFor(c,cx,baseY,p,tideZ,waterZ,pool,y0){
    const {w,h}=c;
    let minX=1e9,maxX=-1e9,minY=1e9,maxY=-1e9,best=-1,bi=-1;
    for(let y=0;y<h;y++)for(let x=0;x<w;x++){ const i=y*w+x; if(!c.mask[i]) continue;
      if(c.wx[i]<minX)minX=c.wx[i]; if(c.wx[i]>maxX)maxX=c.wx[i];
      if(c.wy[i]<minY)minY=c.wy[i]; if(c.wy[i]>maxY)maxY=c.wy[i];
      if(c.top[i] && c.mask[i]!==4 && c.hz[i]>best){ best=c.hz[i]; bi=i; } }
    const px=Math.round(cx), gy=Math.round(baseY-Q*y0), footprint={
      rx:+(Math.max(1,(maxX-minX)/2)/PPU).toFixed(2), ry:+(Math.max(1,(maxY-minY)/2)/PPU).toFixed(2),
      ground:{x:px,y:gy} };
    let perch=null;
    if(bi>=0){ const bx=bi%w, by=(bi/w)|0; let flat=0;
      for(let dx=-2;dx<=2;dx++)for(let dy=-1;dy<=1;dy++){ const j=(by+dy)*w+(bx+dx);
        if(bx+dx>=0&&bx+dx<w&&by+dy>=0&&by+dy<h&&c.top[j]&&c.mask[j]&&Math.abs(c.hz[j]-best)<1.6) flat++; }
      perch={ x:bx, y:by, zM:+(best/PPU).toFixed(2), flat:flat>=8 }; }
    const rim=[];
    for(let y=0;y<h;y++)for(let x=0;x<w;x++){ const i=y*w+x; if(!c.mask[i]) continue;
      const open=(x===0||!c.mask[i-1])||(x===w-1||!c.mask[i+1])||(y===h-1||!c.mask[i+w]);
      if(!open) continue;
      rim.push({x,y,d:Math.hypot(x-px,(y-gy)/Q), a:Math.atan2((gy-y)/Q,x-px), z:c.hz[i]}); }
    rim.sort((a,b)=>(b.d - Math.abs(b.z-tideZ)*0.55)-(a.d - Math.abs(a.z-tideZ)*0.55));
    const snags=[]; const ad=(u,v)=>{ let d=Math.abs(u-v)%(Math.PI*2); return Math.min(d,Math.PI*2-d); };
    for(const r of rim){ if(snags.every(s=>ad(s.a,r.a)>1.05)){ snags.push(r); if(snags.length===3) break; } }
    return {
      footprint, perch,
      snags: snags.map(s=>({x:s.x,y:s.y,zM:+(s.z/PPU).toFixed(2)})),
      hazard: waterZ>0 ? { x:px, y:Math.round(baseY-Q*y0-HZ*waterZ), rM:+(Math.max(footprint.rx,footprint.ry)*1.35).toFixed(2) } : null,
      pool,
      weedLine: { y:Math.round(baseY-Q*y0-HZ*tideZ), zM:+(tideZ/PPU).toFixed(2) },
      pivot: { x:px, y:Math.round(baseY-Q*y0) },
    };
  }

  // Scale + centre the built lump set so it always fits its cell, whatever the sliders say.
  function fitToCell(g,S){
    const L=g.lumps;
    let minX=1e9,maxX=-1e9,minY=1e9,maxY=-1e9,maxZ=0;
    for(const l of L){ minX=Math.min(minX,l.x-l.rx); maxX=Math.max(maxX,l.x+l.rx);
      minY=Math.min(minY,l.y-l.ry); maxY=Math.max(maxY,l.y+l.ry); maxZ=Math.max(maxZ,l.z0+l.rz); }
    const ox=(minX+maxX)/2;
    for(const l of L) l.x-=ox;
    if(g.pool) g.pool.x-=ox;
    const k=Math.min(1, (S.cell.w-2)/((maxX-minX)+1), (S.cell.h-3)/(Q*(maxY-minY)+HZ*maxZ+2));
    if(k<1){
      for(const l of L){ l.x*=k; l.y*=k; l.z0*=k; l.rx*=k; l.ry*=k; l.rz*=k;
        if(l.clip) l.clip=[l.clip[0]*k,l.clip[1]*k]; }
      if(g.pool){ g.pool.x*=k; g.pool.y*=k; g.pool.rx*=k; g.pool.ry*=k; g.pool.depth*=k; }
      g.tideZ*=k;
    }
    g.y0 = -Math.min.apply(null, L.map(l=>l.y-l.ry));    // ground row = the NEAREST footprint edge
    return k;
  }

  // ---- public ---------------------------------------------------------------
  function render(spKey, opts, rampKey){
    const key = spKey in SPECIES ? spKey : 'Erratic', S = SPECIES[key];
    let p;
    if(opts && typeof opts.variant==='number') p=Object.assign({}, S.variants[Math.max(0,Math.min(S.variants.length-1,opts.variant))]);
    else p=Object.assign({}, S.variants[0], opts||{});
    if(opts && typeof opts.dress==='number') p.dress=opts.dress;
    const stone = (opts&&opts.stone in STONES) ? opts.stone : (p.stone in STONES ? p.stone : 'sandstone');
    const ST = STONES[stone]; p.stone = stone;
    p.sizeM=Math.max(0.3,Math.min(3,p.sizeM)); p.waterline=Math.max(0,Math.min(0.85,p.waterline||0));
    p.dress=Math.max(0,Math.min(2,p.dress|0));
    p.angular=Math.max(0,Math.min(1,p.angular+ST.angular));              // stone reshapes the mass

    const c=new Cell(S.cell.w,S.cell.h), rng=mulberry(p.seed|0);
    const cx=(S.cell.w-1)/2, baseY=S.cell.h-2;
    const g=GEN[key](p,rng,S);
    fitToCell(g,S);
    const y0=g.y0;
    g.lumps.sort((a,b)=>b.y-a.y);
    for(const L of g.lumps) paintLump(c,L,cx,baseY,y0);
    facetise(c,p.seed|0);
    const topZ=Math.max.apply(null,g.lumps.map(l=>l.z0+l.rz));
    const waterZ = p.waterline>0 ? topZ*p.waterline : 0;
    const pool = carvePool(c,cx,baseY,g.pool,p.seed|0);
    dressRock(c,p,g.tideZ,p.seed|0);
    if(waterZ>0) clipWaterline(c,waterZ);
    const R=(RAMPS[stone]&&RAMPS[stone][rampKey])||RAMPS[stone].dry;
    const rgba=colourise(c,R,p,g.tideZ,waterZ,ST);
    const anchors=anchorsFor(c,cx,baseY,p,g.tideZ,waterZ,pool,y0);
    const topM = Math.max(topZ/PPU, anchors.perch?anchors.perch.zM:0);
    return { w:S.cell.w, h:S.cell.h, rgba, anchors, params:p, topM:+topM.toFixed(2) };
  }

  root.RockIso = { PPU, ELEV:40, Q, HZ, KEYLINE, SPECIES, STONES, TIDES, RAMPS, DRESS, render };
})(typeof globalThis!=='undefined'?globalThis:window);

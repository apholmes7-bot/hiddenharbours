/* Hidden Harbours — parametric ISO Sport Fishers, CLEAN-SHEET REBUILD (ADR-0006 bake recipe).
   The first sportfisher rig reused the lobster-boat approach — one continuous loft from foredeck to
   tower — and the proportions were wrong from the first pass. This rig starts over. Two hulls off one
   core: a 16.2 m convertible ("53", open flybridge + pipe tower) and a 27.4 m Viking-style skybridge
   ("90", enclosed skylounge with the tuna tower standing ON its roof).

   What was learned, held as rules:
   1. HULL FIRST. The sheer leaves the transom LOW, accelerates through midships, crests SHORT of the
      stem (u .945) and eases back down to the stem head. The transom is the NARROWEST station above
      water — max beam sits at u .56, abreast the house nose — and the cockpit sole sits deep, so the
      aft third of the boat reads low and tight while the bow stands tall over a fine, flared entry.
   2. STACKED VOLUMES, not one wedge. The salon deckhouse and the skylounge (or flybridge coaming) are
      SEPARATE rounded-plan lofts, each with its own raked front, its own roof, its own glass. The
      visible step between them is the point.
   3. The house is SMALL against the hull. >40% of LOA is clean foredeck; the salon roof sits about a
      bow-freeboard above the sheer, not two.
   4. GLASS is a level-edged ribbon: header held level bow-to-stern, sill rising aft, band wrapping the
      rounded nose as one clean dark sheet (no mullions) and pinching shut on an ellipse well forward
      of the house aft end. Curved corners are inherent — the band lies on the rounded plan.
   5. AFT WALLS RAKE BACKWARDS. Every house volume's aft face leans toward the cockpit as it rises —
      fAft(z) moves aft with z — never a vertical slab.
   6. The 90's tuna tower is PIPEWORK standing on the skylounge roof: the roof is the tower floor. Four
      near-vertical legs on low pads, an open rail platform, a crowned buggy top on posts, whips aft.
      No moulded columns, nothing raked down past the bridge glass.

   Fixed 3/4 turntable camera (elev 40deg default), 45deg steps, flat-facet shading, ordered dither,
   1px depth keyline, NO AA. Each hull owns its cell + pivot (projection of boat origin = amidships,
   keel bottom, centreline), pinned for every heading AND both rigger states:
     CONVERTIBLE  820 x 770,  pivot (410,475)
     SKYBRIDGE   1200 x 1170, pivot (600,730)
   OUTRIGGERS are a flag: render(dir,{riggers:'stowed'|'out'}). PAINT swaps ramps only — one
   silhouette, one face list, one anchor table per hull.

   Deck anchors (cell coords, per heading): helmSeat, towerStation, chairMount, mezzSeat, rodMounts[],
   doorMounts{transom,tuna}, tubMounts[], navMounts{port,star,stern,mast}.

   Exposes globalThis.SportFisherIso2 = { CONVERTIBLE, SKYBRIDGE, HULLS, byId(id), PAINTS, paintRamps,
     PX, DIRS, order, TEAK,DECKF,GRIP,GLAS,STEEL,IRON,KEY,HULL,BOOT,CREAM,BLUE }.

   PASS 2 — THE SALON SLIDER AND THE PUBLISHED LOFT (tranche 4 of the interiors program). The salon
   aft glass is now a REAL two-panel slider: a fixed starboard pane and a glass leaf that
   opts.doorOpen 0..1 rides to starboard along its track, parking over the fixed pane (0 = closed,
   fleet default; no swept arc — the collider is the leaf itself, riding the raked aft wall). The
   sill sits at mezzanine height, so a character walks in level off the mezzanine deck.
   doorMount(dir,opts) -> threshold + leading edge + clear state. Each hull publishes DOOR + HOUSE
   (salon + below levels; the open flybridge — and the 90's skylounge — stay the EXTRACTOR's
   surfaces: decks.bridge is external) + loft so boatInteriorRig.js builds the rooms from these
   exact numbers. The extractor shim anchors (return block head, export line) are untouched. */
(function (root) {
  const PX = 32, S = 32;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;

  // ---- ramps dark->light --------------------------------------------------------------------------
  const HULL  = ['#7f878c','#9ea6a9','#bcc3c2','#d5dbd7','#e9ede7','#f4f7f1','#fcfdf8'];
  const BOOT  = ['#090c11','#0f131a','#161c25','#202734','#2b3341'];
  const CREAM = ['#8a9297','#a7afb2','#c4cbca','#dbe0dc','#ecefe9','#f6f8f2','#fdfef9'];
  const BLUE  = ['#0d2a4e','#17457a','#245f9c','#3878ba','#5391cf'];
  const TEAK  = ['#3f2814','#54351d','#6c4626','#855a31','#9c6e40','#b28553'];
  const DECKF = ['#61675f','#787e74','#8f948a','#a4a99e','#b8bdb1','#cacec3'];
  const GRIP  = ['#34382f','#40443a','#4c5045','#585c50','#64685b','#706f66'];
  const GLAS  = ['#05090c','#0a1218','#101a22','#17242e','#20323f'];
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];
  const IRON  = ['#0d1013','#161a20','#222931','#323b45'];
  const KEY   = '#0d1418';

  // ---- PAINT: 4 schemes over the same hulls (ramps only) -------------------------------------------
  function oklchHex(Lp, C, h){
    const hr = h*DEG, a = C*Math.cos(hr), b = C*Math.sin(hr);
    const l_ = Lp + 0.3963377774*a + 0.2158037573*b;
    const m_ = Lp - 0.1055613458*a - 0.0638541728*b;
    const s_ = Lp - 0.0894841775*a - 1.2914855480*b;
    const l = l_*l_*l_, m = m_*m_*m_, s = s_*s_*s_;
    const r =  4.0767416621*l - 3.3077115913*m + 0.2309699292*s;
    const g = -1.2684380046*l + 2.6097574011*m - 0.3413193965*s;
    const bb = -0.0041960863*l - 0.7034186147*m + 1.7076147010*s;
    const enc = (u)=>{ u = u<=0.0031308 ? 12.92*u : 1.055*Math.pow(Math.max(u,0),1/2.4)-0.055;
      const n = Math.round(Math.max(0,Math.min(1,u))*255); return (n<16?'0':'')+n.toString(16); };
    return '#'+enc(r)+enc(g)+enc(bb);
  }
  function mkRamp(n, L0, L1, C, h){
    const out = [];
    for(let i=0;i<n;i++){ const t = n===1?0:i/(n-1);
      out.push(oklchHex(L0+(L1-L0)*t, C*(1-0.12*t), h)); }
    return out;
  }
  const PAINTS = [
    { id:'gelcoat',  label:'WHITE GELCOAT', note:'The canonical slice — white topsides, near-black boot, one blue cove line.',
      literal:{ top:HULL, boot:BOOT, stripe:BLUE, house:CREAM } },
    { id:'navy',     label:'AWLGRIP NAVY',  note:'Tournament navy topsides, white house, a gold cove and boot stripe.',
      top:[0.22,0.50,0.086,264], boot:[0.14,0.27,0.042,264], stripe:[0.52,0.80,0.105,85], house:[0.62,0.97,0.006,250] },
    { id:'gunmetal', label:'GUNMETAL',      note:'Cool grey metallic topsides, black boot, a white cove — the modern battlewagon.',
      top:[0.34,0.62,0.014,242], boot:[0.13,0.26,0.010,250], stripe:[0.66,0.94,0.005,240], house:[0.60,0.95,0.006,240] },
    { id:'ivory',    label:'IVORY',         note:'Warm ivory topsides over a near-black bottom, navy cove.',
      top:[0.56,0.90,0.030,88],  boot:[0.16,0.30,0.028,255], stripe:[0.30,0.54,0.092,262], house:[0.64,0.97,0.012,88] },
  ];
  const PAINT_BY = {}; PAINTS.forEach(p=>{ PAINT_BY[p.id]=p; });
  const _rampCache = {};
  function paintRamps(id){
    const p = PAINT_BY[id] || PAINT_BY.gelcoat;
    if(_rampCache[p.id]) return _rampCache[p.id];
    const r = p.literal ? p.literal : {
      top:    mkRamp(7, p.top[0],    p.top[1],    p.top[2],    p.top[3]),
      boot:   mkRamp(5, p.boot[0],   p.boot[1],   p.boot[2],   p.boot[3]),
      stripe: mkRamp(5, p.stripe[0], p.stripe[1], p.stripe[2], p.stripe[3]),
      house:  mkRamp(7, p.house[0],  p.house[1],  p.house[2],  p.house[3]),
    };
    return (_rampCache[p.id] = r);
  }
  const _matCache = {};
  function matsFor(id){
    const key = PAINT_BY[id] ? id : 'gelcoat';
    if(_matCache[key]) return _matCache[key];
    const R = paintRamps(key);
    const MATS = { hull:{ramp:R.top,off:0}, boot:{ramp:R.boot,off:0}, cream:{ramp:R.house,off:0},
                   stripe:{ramp:R.stripe,off:0}, teak:{ramp:TEAK,off:0}, deck:{ramp:DECKF,off:0},
                   grip:{ramp:GRIP,off:0}, glas:{ramp:GLAS,off:0}, steel:{ramp:STEEL,off:0},
                   iron:{ramp:IRON,off:0}, blk:{ramp:R.boot,off:-1}, dark:{ramp:R.boot,off:-2} };
    const RINDEX = {};
    [R.top,R.boot,R.house,R.stripe,TEAK,DECKF,GRIP,GLAS,STEEL,IRON].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
    return (_matCache[key] = { MATS, RINDEX });
  }

  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- monotone cubic fairing (Fritsch–Carlson) — smooth, cannot overshoot ------------------------
  function mono(pts){
    pts = pts.slice().sort((a,b)=>a[0]-b[0]);
    const n=pts.length, xs=pts.map(p=>p[0]), ys=pts.map(p=>p[1]);
    if(n===1) return ()=>ys[0];
    const d=[]; for(let i=0;i<n-1;i++) d.push((ys[i+1]-ys[i])/(xs[i+1]-xs[i]));
    const m=[d[0]];
    for(let i=1;i<n-1;i++){
      if(d[i-1]*d[i]<=0) m.push(0);
      else { const h0=xs[i]-xs[i-1], h1=xs[i+1]-xs[i], w1=2*h1+h0, w2=h1+2*h0;
             m.push((w1+w2)/(w1/d[i-1]+w2/d[i])); }
    }
    m.push(d[n-2]);
    return function(x){
      if(x<=xs[0]) return ys[0]+m[0]*(x-xs[0]);
      if(x>=xs[n-1]) return ys[n-1]+m[n-1]*(x-xs[n-1]);
      let i=0; while(i<n-2 && x>xs[i+1]) i++;
      const h=xs[i+1]-xs[i], t=(x-xs[i])/h, t2=t*t, t3=t2*t;
      return (2*t3-3*t2+1)*ys[i] + (t3-2*t2+t)*h*m[i] + (-2*t3+3*t2)*ys[i+1] + (t3-t2)*h*m[i+1];
    };
  }
  const colOf = (tbl,k)=>tbl.map(r=>[r[0], r[k]]);
  const clamp01 = (t)=>t<0?0:(t>1?1:t);
  const lerp = (a,b,t)=>a+(b-a)*t;

  // ---- generic solids ------------------------------------------------------------------------------
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s];
  const v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function box(c,h,mat,b,db){
    const P=(sx,sy,sz)=>[c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]];
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
  function tube(A,B2,rad,mat,b){
    const ax=v_norm(v_sub(B2,A)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P)=>[ v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)), v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                      v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)), v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad)) ];
    const r0=ring(A), r1=ring(B2), out=[];
    for(let k=0;k<4;k++){ const k2=(k+1)%4; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:-0.15}); }
    return out;
  }
  function taper(A,B2,r0,r1,mat,b){
    const ax=v_norm(v_sub(B2,A)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P,rr)=>[ v_add(v_add(P,v_mul(r,rr)),v_mul(u,rr)), v_add(v_add(P,v_mul(r,-rr)),v_mul(u,rr)),
                         v_add(v_add(P,v_mul(r,-rr)),v_mul(u,-rr)), v_add(v_add(P,v_mul(r,rr)),v_mul(u,-rr)) ];
    const a0=ring(A,r0), a1=ring(B2,r1), out=[];
    for(let k=0;k<4;k++){ const k2=(k+1)%4; out.push({v:[a0[k],a0[k2],a1[k2],a1[k]],mat,b:b||0,db:-0.15}); }
    return out;
  }
  const DBP = 0.05;
  function objNormal(a,b,c){ const ux=b[0]-a[0],uy=b[1]-a[1],uz=b[2]-a[2], vx=c[0]-a[0],vy=c[1]-a[1],vz=c[2]-a[2];
    return [uy*vz-uz*vy, uz*vx-ux*vz, ux*vy-uy*vx]; }
  function faceO(v, outward, mat, b, db){ const n=objNormal(v[0],v[1],v[2]);
    if(n[0]*outward[0]+n[1]*outward[1]+n[2]*outward[2] < 0) v=v.slice().reverse();
    return {v, mat, b:b||0, db:(db==null?DBP:db)}; }

  // ---- rasterizer (locked recipe) -------------------------------------------------------------------
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) };
  }
  function projVert(x,y,z,B,G){
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:G.cx+xr*S, sy:G.cy-(yr*B.se+zr*B.ce)*S - B.heave, d:(yr*B.ce-zr*B.se) };
  }
  function _paint(faces, opts, G, MAT){
    const PW=G.W, PH=G.H;
    const MATS=MAT.MATS, RINDEX=MAT.RINDEX;
    const B=camBasis(opts);
    const zbuf=new Float32Array(PW*PH).fill(Infinity);
    const col=new Array(PW*PH).fill(null);
    const dep=new Float32Array(PW*PH);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B,G));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.hull;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1]);
      function fillTri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(PW-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(PH-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) return;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          let w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          let w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          let w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*PW+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d;
            let base=Math.floor(fidx);
            let idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    const out=col.slice();
    for(let y=0;y<PH;y++) for(let x=0;x<PW;x++){
      const i=y*PW+x; if(!col[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=PW||ny>=PH) continue;
        const j=ny*PW+nx; if(!col[j]) continue;
        if(Math.abs(dep[i]-dep[j])>0.30){
          const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
          if(e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)];
        }
      }
    }
    for(let y=0;y<PH;y++) for(let x=0;x<PW;x++){
      const i=y*PW+x; if(out[i]) continue;
      let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy;
        if(nx>=0&&nx<PW&&ny>=0&&ny<PH&&col[ny*PW+nx]){ touch=true; break; }
      }
      if(touch) out[i]=KEY;
    }
    return out;
  }
  function _toRGBA(out, PW, PH){
    const rgba=new Uint8ClampedArray(PW*PH*4);
    for(let i=0;i<PW*PH;i++){
      const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16);
      rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255;
    }
    return rgba;
  }

  // ===================================================================================================
  // ONE builder, two hulls.
  // ===================================================================================================
  function makeRig(spec){
    const L=spec.L, DECK=spec.DECK, RAKE=spec.RAKE, TH=spec.TH;
    const G={ W:spec.W, H:spec.H, cx:spec.cx, cy:spec.cy };
    const NSEG=spec.NSEG||30;

    // ---- hull lines: sheer master arc, plan, flare ----
    const fWs=mono(colOf(spec.T,1)), fWb=mono(colOf(spec.T,2)), fKz=mono(colOf(spec.T,3));
    const fSheer=mono(spec.sheer), fFlare=mono(spec.flare);
    const uOf=(y)=>(y+L/2)/L;
    const yOf=(u)=>-L/2+u*L;
    const sheerZ=(u)=>fSheer(clamp01(u));
    const _stC={};
    function station(u){
      u=clamp01(u);
      const k=u.toFixed(5); if(_stC[k]) return _stC[k];
      const ws=Math.max(0.04,fWs(u)), wb=Math.max(0.03,Math.min(fWb(u),ws)), kz=fKz(u), z=sheerZ(u);
      return (_stC[k]={ws,wb,kz,dep:Math.max(0.22,z-kz),y:yOf(u),sheer:z});
    }
    function hbOf(u,frac){ const st=station(u); return st.wb + (st.ws-st.wb)*Math.pow(clamp01(frac), fFlare(clamp01(u))); }
    function bowRake(u,frac){ const t=Math.max(0,(u-0.50)/0.50), s=t*t*(3-2*t); return RAKE*s*(0.18+0.82*frac); }
    function skin(side,u,frac,inset){
      const st=station(u);
      const w=hbOf(u,frac) - (inset ? TH*(0.55+0.45*clamp01(frac)) : 0);
      return [ side*Math.max(0.012,w), st.y+bowRake(u,frac), st.kz + clamp01(frac)*st.dep - (inset?0.02:0) ];
    }
    const dfrac=(st)=>Math.max(0.04, Math.min(0.98, (DECK-st.kz)/st.dep));
    const zdl=(y)=>sheerZ(clamp01(uOf(y)))-spec.drop;   // deck line (covering board top)
    // house side limit: hull half-width just below the deck line, less skin + full cap + clearance,
    // so the covering board runs stem to transom as ONE unbroken line
    const clampHW=(y)=>{ const u=clamp01(uOf(y)), st=station(u);
      const z=st.sheer-spec.drop-0.10, frac=clamp01((z-st.kz)/st.dep);
      return st.wb+(st.ws-st.wb)*Math.pow(frac,fFlare(u)) - TH - spec.capW - 0.04; };

    const F=[], RIGF={ stowed:[], out:[] };
    let TARGET=F;
    const face=(v,mat,b,db)=>TARGET.push({v,mat:mat||'hull',b:b||0,db:db||0});
    const pushO=(v,outw,mat,b,db)=>TARGET.push(faceO(v,outw,mat,b,db));
    const boxF=(c,h,mat,b,db)=>{ TARGET.push.apply(TARGET, box(c,h,mat,b,db)); };
    const tubeF=(A,B2,rad,mat,b)=>{ TARGET.push.apply(TARGET, tube(A,B2,rad,mat,b)); };
    const taperF=(A,B2,r0,r1,mat,b)=>{ TARGET.push.apply(TARGET, taper(A,B2,r0,r1,mat,b)); };
    const OB=[ [0,0.300,'boot',-0.2,0], [0.300,0.340,'stripe',0.2,0.01], [0.340,0.905,'hull',0,0],
               [0.905,0.940,'stripe',0.28,0.01], [0.940,1,'dark',-0.25,0.006] ];

    // ============ HOUSE VOLUME: a rounded-plan loft with raked front AND raked aft wall ============
    // o = { z0,z1, nose:[[z,y]..], aft:[[z,y]..], hw:[[y,halfw]..], nr, ar, aw, inset, cam,
    //       ns, nz, mat, clamp, open:{rim,sole} }
    function volume(o){
      const fN=mono(o.nose), fA=mono(o.aft), fHw=mono(o.hw);
      const NS=o.ns||18, AW=(o.aw==null?0.60:o.aw);
      const ins=(z)=>(o.inset||0)*clamp01((z-o.z0)/(o.z1-o.z0));
      function xAt(y,z){
        const yF=fN(z), yA=fA(z);
        let x=Math.max(0.02, Math.min(fHw(y), o.clamp?o.clamp(y):1e9) - ins(z));
        const nr=Math.min(o.nr||0.8,(yF-yA)*0.45);
        if(y>yF-nr){ const q=clamp01((y-(yF-nr))/nr); x*=Math.sqrt(Math.max(0,1-q*q)); }
        const ar=Math.min(o.ar||0.6,(yF-yA)*0.30);
        if(y<yA+ar){ const q=clamp01((yA+ar-y)/ar); x*=AW+(1-AW)*Math.sqrt(Math.max(0,1-q*q)); }
        return Math.max(0.015,x);
      }
      const TS=[]; for(let i=0;i<=NS;i++) TS.push(i/NS); TS.push(1.35,1.7,2.0);
      function P(t,z){
        const yF=fN(z), yA=fA(z);
        if(t>1){ const e=xAt(yA+1e-4,z); return {x:Math.max(0.012,e*(2-t)), y:yA, nx:0, ny:-1}; }
        const g=(1-Math.cos(Math.PI*t))/2, y=yF-(yF-yA)*g;
        const x=xAt(y,z);
        const d=0.014, t0=Math.max(0,t-d), t1=Math.min(1,t+d);
        const g0=(1-Math.cos(Math.PI*t0))/2, g1=(1-Math.cos(Math.PI*t1))/2;
        const ya_=yF-(yF-yA)*g0, yb_=yF-(yF-yA)*g1;
        const xa_=xAt(ya_,z), xb_=xAt(yb_,z);
        let nx=-(yb_-ya_), ny=(xb_-xa_);
        const m=Math.hypot(nx,ny)||1;
        return {x,y,nx:nx/m,ny:ny/m};
      }
      // walls
      const NZ=o.nz||10;
      const zs=[]; for(let k=0;k<=NZ;k++) zs.push(o.z0+(o.z1-o.z0)*k/NZ);
      for(let k=0;k<NZ;k++) for(let j=0;j+1<TS.length;j++) for(const sd of [-1,1]){
        const a0=P(TS[j],zs[k]), a1=P(TS[j+1],zs[k]), b1=P(TS[j+1],zs[k+1]), b0=P(TS[j],zs[k+1]);
        const ny=(a0.ny+a1.ny)/2, nx=(a0.nx+a1.nx)/2;
        const bias=(o.b||0)+0.02+0.40*Math.max(0,ny)-0.50*Math.max(0,-ny)+0.16*(k/NZ);
        pushO([[sd*a0.x,a0.y,zs[k]],[sd*a1.x,a1.y,zs[k]],[sd*b1.x,b1.y,zs[k+1]],[sd*b0.x,b0.y,zs[k+1]]],
              [sd*nx,ny,0.06], o.mat||'cream', bias, DBP);
      }
      if(!o.open){
        // crowned roof: strips along y over the top ring plan; crown falls to the wall top at the edge,
        // reading as a rolled shoulder — no square roof edge anywhere
        const cam=(o.cam==null?0.15:o.cam), XS=[0,0.45,0.8,1];
        const cz=(f)=>o.z1+cam*(1-Math.pow(f,2.2));
        for(let j=0;j+1<TS.length;j++){
          const t0=TS[j], t1=TS[j+1]; if(t1>1) break;
          const A=P(t0,o.z1), B=P(t1,o.z1);
          for(let k=0;k+1<XS.length;k++) for(const sd of [-1,1])
            pushO([[sd*A.x*XS[k],A.y,cz(XS[k])],[sd*A.x*XS[k+1],A.y,cz(XS[k+1])],
                   [sd*B.x*XS[k+1],B.y,cz(XS[k+1])],[sd*B.x*XS[k],B.y,cz(XS[k])]],
                  [sd*XS[k]*0.35,0,1], o.mat||'cream', 0.55-0.13*k, -0.01);
        }
      } else {
        const rim=o.open.rim, sole=o.open.sole;
        for(let j=0;j+1<TS.length;j++) for(const sd of [-1,1]){
          const A=P(TS[j],o.z1), B=P(TS[j+1],o.z1);
          const iA={x:Math.max(0.02,A.x-rim*Math.abs(A.nx)-0.02*Math.abs(A.ny)), y:A.y-rim*A.ny};
          const iB={x:Math.max(0.02,B.x-rim*Math.abs(B.nx)-0.02*Math.abs(B.ny)), y:B.y-rim*B.ny};
          pushO([[sd*A.x,A.y,o.z1],[sd*B.x,B.y,o.z1],[sd*iB.x,iB.y,o.z1],[sd*iA.x,iA.y,o.z1]],
                [0,0,1], o.mat||'cream', 0.55, -0.01);
          pushO([[sd*iA.x,iA.y,o.z1],[sd*iB.x,iB.y,o.z1],[sd*iB.x,iB.y,sole],[sd*iA.x,iA.y,sole]],
                [-sd*A.nx,-A.ny,0.25], o.mat||'cream', -1.3, -0.02);
        }
        for(let j=0;j+1<TS.length;j++){
          const t0=TS[j], t1=TS[j+1]; if(t1>1) break;
          const A=P(t0,o.z1), B=P(t1,o.z1);
          const ax=Math.max(0.03,A.x-rim), bx=Math.max(0.03,B.x-rim);
          face([[-ax,A.y,sole],[ax,A.y,sole],[bx,B.y,sole],[-bx,B.y,sole]], o.mat||'cream', -0.2, -0.01);
        }
      }
      return { P, TS, xAt, fN, fA, z0:o.z0, z1:o.z1, cam:(o.cam==null?0.15:o.cam) };
    }

    // ============ GLASS: a level-edged ribbon wrapped on a volume's skin ============
    // bd = { lo:[[y,z]..], hi:[[y,z]..], yEnd, ease, zRef } — header/sill are WORLD heights, the band
    // wraps the nose as one sheet (no mullions) and pinches shut on an ellipse at yEnd.
    function volBand(V,bd){
      const fLo=mono(bd.lo), fHi=mono(bd.hi);
      const easeK=(q)=> q<=0?0:(q>=1?1:Math.sqrt(1-(1-q)*(1-q)));
      const zRef=bd.zRef!=null?bd.zRef:(V.z0+V.z1)/2;
      const E=(t)=>{
        const p=V.P(t,zRef), y=p.y;
        const k=easeK((y-bd.yEnd)/(bd.ease||1));
        if(k<0.05) return null;
        const zl=fLo(y), zh=fHi(y);
        const zc=(zl+zh)/2, half=(zh-zl)/2*k;
        if(half<0.05) return null;
        return {zb:zc-half, zt:zc+half};
      };
      for(let j=0;j+1<V.TS.length;j++){
        const t0=V.TS[j], t1=V.TS[j+1]; if(t1>1) break;
        const A=E(t0), B=E(t1); if(!A||!B) continue;
        for(const sd of [-1,1]) for(const [off,ex,mat,b] of [[0.028,0.07,'iron',-0.15],[0.062,0,'glas',-0.62]]){
          const q=(t,z)=>{ const p=V.P(t,z); return [sd*(p.x+off*Math.abs(p.nx)), p.y+off*p.ny, z]; };
          const n=V.P((t0+t1)/2, zRef);
          pushO([q(t0,A.zb-ex),q(t0,A.zt+ex),q(t1,B.zt+ex),q(t1,B.zb-ex)],
                [sd*n.nx,n.ny,0.10], mat, b, DBP+(mat==='glas'?0.02:0));
        }
      }
    }
    // glass laid on a raked AFT wall (sliders, skylounge door)
    function aftGlass(V,x0,x1,z0,z1,b){
      const q=(x,z)=>[x, V.fA(z)-0.05, z];
      pushO([q(x0,z0),q(x1,z0),q(x1,z1),q(x0,z1)],[0,-1,0.05],'glas',b==null?-0.55:b,DBP+0.02);
    }

    // ---- crowned, plan-rounded slab with a rolled outboard edge (platforms, buggy top) ----
    function crownSlab(o){
      const n=o.n||10, r=o.r, hx=o.hx, th=o.th||0.07, cam=(o.cam==null?0.07:o.cam), b=o.b||0;
      const rf=(o.rf==null?0.22:o.rf);
      const hwf=(y)=>{ const d=Math.min(y-o.ya, o.yf-y);
        if(d>=r) return hx;
        const t=clamp01((r-d)/r); return Math.max(0.012, hx*Math.sqrt(Math.max(0,1-t*t))); };
      const SEC=(w)=>{
        const P=[], xe=w*(1-rf), re=w*rf, dz=Math.min(re, th*0.85);
        const cz=(x)=>o.z + cam*(1-(x/Math.max(w,1e-4))*(x/Math.max(w,1e-4)));
        for(let i=0;i<=3;i++){ const x=xe*i/3; P.push([x,cz(x)]); }
        const z0=cz(xe);
        for(let i=1;i<=3;i++){ const a=(Math.PI/2)*i/3;
          P.push([xe+re*Math.sin(a), z0-dz*(1-Math.cos(a))]); }
        P.push([w, o.z-th]);
        return P;
      };
      for(let i=0;i<n;i++){
        const y0=o.ya+(o.yf-o.ya)*i/n, y1=o.ya+(o.yf-o.ya)*(i+1)/n;
        const A=SEC(hwf(y0)), B=SEC(hwf(y1));
        for(let k=0;k+1<A.length;k++) for(const sd of [-1,1]){
          const p0=[sd*A[k][0],y0,A[k][1]], p1=[sd*A[k+1][0],y0,A[k+1][1]];
          const q1=[sd*B[k+1][0],y1,B[k+1][1]], q0=[sd*B[k][0],y1,B[k][1]];
          const mx=(Math.abs(p0[0])+Math.abs(p1[0]))/2/Math.max(hx,1e-4);
          pushO([p0,p1,q1,q0],[sd*mx,0,k<4?1:0.22], o.mat, b+0.44-0.15*k, -0.01);
        }
        pushO([[-hwf(y0),y0,o.z-th],[hwf(y0),y0,o.z-th],[hwf(y1),y1,o.z-th],[-hwf(y1),y1,o.z-th]],
              [0,0,-1],o.mat,b-1.5,-0.01);
      }
    }
    // rounded lofted pod (helm consoles)
    function pod(o){
      const ny=o.ny||9, cam=(o.cam==null?0.08:o.cam), cx=o.cx||0, b=o.b||0;
      const rp=(o.rp==null?Math.min(o.hx*0.58,(o.yf-o.ya)*0.34):o.rp);
      const sh=(o.sh==null?Math.min(o.hx*0.52,(o.z1-o.z0)*0.34):o.sh);
      const hwf=(y)=>{ const d=Math.min(y-o.ya,o.yf-y);
        if(d>=rp) return o.hx;
        const t=clamp01((rp-d)/rp); return Math.max(0.014,o.hx*Math.sqrt(Math.max(0,1-t*t))); };
      const SEC=(w)=>{
        const f=w/Math.max(o.hx,1e-4), s=sh*f, P=[[w,o.z0],[w,o.z0+(o.z1-o.z0-s)*0.55],[w,o.z1-s]];
        for(let i=1;i<=3;i++){ const a=(Math.PI/2)*i/3;
          P.push([w-s*(1-Math.cos(a))*0.92, o.z1-s+s*Math.sin(a)]); }
        const xk=P[P.length-1][0];
        P.push([xk*0.55, o.z1+cam*0.64]); P.push([0, o.z1+cam]);
        return P;
      };
      const zc=(o.z0+o.z1)/2;
      for(let i=0;i<ny;i++){
        const y0=o.ya+(o.yf-o.ya)*i/ny, y1=o.ya+(o.yf-o.ya)*(i+1)/ny;
        const A=SEC(hwf(y0)), B=SEC(hwf(y1));
        for(let k=0;k+1<A.length;k++) for(const sd of [-1,1]){
          const p0=[cx+sd*A[k][0],y0,A[k][1]], p1=[cx+sd*A[k+1][0],y0,A[k+1][1]];
          const q1=[cx+sd*B[k+1][0],y1,B[k+1][1]], q0=[cx+sd*B[k][0],y1,B[k][1]];
          const mz=(A[k][1]+A[k+1][1])/2;
          pushO([p0,p1,q1,q0],[sd*(A[k][0]+A[k+1][0])/2,0,mz-zc],o.mat,
                b+(k<3?-0.10-0.12*k:0.34+0.10*(k-3)),DBP);
        }
      }
      const wa=hwf(o.ya+1e-3), wf=hwf(o.yf-1e-3);
      pushO([[cx-wa,o.ya,o.z0],[cx+wa,o.ya,o.z0],[cx+wf,o.yf,o.z0],[cx-wf,o.yf,o.z0]],
            [0,0,-1],o.mat,b-1.6,-0.01);
      return hwf;
    }
    function podGlass(o, hwf, zA, zB){
      const ny=o.ny||9, cx=o.cx||0;
      for(let i=0;i<ny;i++){
        const y0=o.ya+(o.yf-o.ya)*i/ny, y1=o.ya+(o.yf-o.ya)*(i+1)/ny;
        const w0=hwf(y0), w1=hwf(y1);
        if(Math.min(w0,w1) < o.hx*0.34) continue;
        for(const sd of [-1,1])
          for(const [off,ex,mat,bb] of [[0.020,0.070,'iron',-0.15],[0.052,0,'glas',-0.62]])
            pushO([[cx+sd*(w0+off),y0,zA-ex],[cx+sd*(w0+off),y0,zB+ex],
                   [cx+sd*(w1+off),y1,zB+ex],[cx+sd*(w1+off),y1,zA-ex]],
                  [sd,0,0],mat,bb,DBP+(mat==='glas'?0.02:0));
      }
    }
    const ladder=(x0,y0,z0,x1,y1,z1,N,r)=>{
      const a=[-x0,y0,z0], b=[-x1,y1,z1], c=[x0,y0,z0], d=[x1,y1,z1];
      tubeF(a,b,r*0.62,'steel',0.2); tubeF(c,d,r*0.62,'steel',-0.3);
      for(let k=1;k<N;k++){ const t=k/N;
        tubeF([lerp(a[0],b[0],t),lerp(a[1],b[1],t),lerp(a[2],b[2],t)],
              [lerp(c[0],d[0],t),lerp(c[1],d[1],t),lerp(c[2],d[2],t)],r*0.42,'steel',0.3); }
    };

    let VOLS={};
    (function build(){
      // ---- hull shell, cockpit liner, bottom ----
      for(const side of [-1,1]){
        for(let i=0;i<NSEG;i++){
          const u0=i/NSEG, u1=(i+1)/NSEG;
          for(const [f0,f1,mat,b,db] of OB)
            face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],mat,b,db);
          const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb);
          if(sa.y <= spec.cockpitTo+0.25){
            const LT=0.94;
            for(let k=0;k<2;k++){
              const g0a=fa+(LT-fa)*k/2, g1a=fa+(LT-fa)*(k+1)/2;
              const g0b=fb+(LT-fb)*k/2, g1b=fb+(LT-fb)*(k+1)/2;
              face([skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'cream',-1.5,-0.03);
            }
          }
          face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'boot',-1.0);
        }
      }
      // ---- covering board: ONE rolled cap, flat washboard aft, lifting into a low bulwark forward ----
      const toeOf=(u)=>{ const t=clamp01((u-spec.toe[0])/(1-spec.toe[0])); return spec.toe[1]*t*t*(3-2*t); };
      const CAPN=5, CAPMAT='teak';
      for(const side of [-1,1]) for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        const rail=(u)=>{
          const st=station(u), o=skin(side,u,1), toe=toeOf(u);
          const xo=o[0], xi=side*(st.ws-TH-spec.capW), P=[];
          for(let k=0;k<=CAPN;k++){
            const t=k/CAPN;
            const bell=Math.sin(Math.PI*Math.min(1,t*1.32));
            P.push([lerp(xo,xi,t), o[1], o[2]-0.004-spec.drop*t*t + toe*bell]);
          }
          return P;
        };
        const A=rail(u0), B=rail(u1);
        for(let k=0;k+1<A.length;k++)
          pushO([A[k],A[k+1],B[k+1],B[k]],[side*(1-k/CAPN)*0.62,0,1],CAPMAT,-0.80-0.22*(k/CAPN),0.03);
      }
      // ---- cockpit sole + teak planks ----
      const SOLE_U=uOf(spec.cockpitTo);
      const DSEG=18, BORD=spec.capW*0.9;
      const dw=(u)=>Math.max(0.05,(hbOf(u,dfrac(station(u)))-TH)*0.965);
      for(let i=0;i<DSEG;i++){
        const u0=SOLE_U*i/DSEG, u1=SOLE_U*(i+1)/DSEG;
        face([[-dw(u0),station(u0).y,DECK],[dw(u0),station(u0).y,DECK],
              [dw(u1),station(u1).y,DECK],[-dw(u1),station(u1).y,DECK]],'cream',-0.3);
      }
      const gA=station(0).y+0.30, gF=spec.cockpitTo-0.05, PLANKS=9;
      for(let i=0;i<DSEG;i++){
        const u0=SOLE_U*i/DSEG, u1=SOLE_U*(i+1)/DSEG, y0=station(u0).y, y1=station(u1).y;
        if(y1<gA || y0>gF) continue;
        const w0=Math.max(0,dw(u0)-BORD), w1=Math.max(0,dw(u1)-BORD);
        for(let k=0;k<PLANKS;k++){
          const t0=k/PLANKS, t1=(k+1)/PLANKS;
          face([[-w0+2*w0*t0,y0,DECK+0.006],[-w0+2*w0*t1,y0,DECK+0.006],
                [-w1+2*w1*t1,y1,DECK+0.006],[-w1+2*w1*t0,y1,DECK+0.006]],'teak',k%2?-0.55:-0.15,0.02);
        }
      }
      const hatch=(x,y,w,l,handle)=>{
        const z=DECK;
        face([[x-w,y-l,z+0.008],[x+w,y-l,z+0.008],[x+w,y+l,z+0.008],[x-w,y+l,z+0.008]],'iron',0.0,0.03);
        const iw=w-0.05, il=l-0.05;
        face([[x-iw,y-il,z+0.014],[x,y-il,z+0.014],[x,y+il,z+0.014],[x-iw,y+il,z+0.014]],'steel',-3.5,0.05);
        face([[x,y-il,z+0.014],[x+iw,y-il,z+0.014],[x+iw,y+il,z+0.014],[x,y+il,z+0.014]],'steel',-2.7,0.05);
        if(handle) boxF([x, y-il+0.07, z+0.03],[0.12,0.022,0.022],'steel',-1.7,0.07);
      };
      spec.hatches.forEach(h=>hatch(h[0],h[1],h[2],h[3],h[4]!==false));
      // fighting chair
      (function(){ const y=spec.chair.y, r=spec.chair.r;
        face([[-r,y-r,DECK+0.012],[r,y-r,DECK+0.012],[r,y+r,DECK+0.012],[-r,y+r,DECK+0.012]],'steel',-2.2,0.05);
        boxF([0,y,DECK+0.10],[r*0.55,r*0.55,0.10],'iron',0.1,-0.02);
        const fy=y+r+spec.chair.foot;
        tubeF([-r*0.9,fy,DECK+0.30],[r*0.9,fy,DECK+0.30],0.05,'steel',0.2);
        for(const s of [-1,1]) tubeF([s*r*0.85,fy,DECK+0.02],[s*r*0.85,fy,DECK+0.30],0.045,'steel',0.0);
      })();

      // ---- house volumes ----
      spec.volumes.forEach(vs=>{
        const o=Object.assign({}, vs, { clamp: vs.clampHull!==false ? clampHW : null, mat:'cream' });
        const V=volume(o);
        VOLS[vs.id]=V;
        (vs.bands||[]).forEach(bd=>volBand(V,bd));
        (vs.aftGlass||[]).forEach(gl=>aftGlass(V,gl[0],gl[1],gl[2],gl[3]));
      });
      const SAL=VOLS.salon;

      // ---- deck margin: washboards + the strip alongside the house down to the covering board ----
      const MSEG=26, mU0=uOf(spec.foredeckTo+0.10);
      for(const side of [-1,1]) for(let i=0;i<MSEG;i++){
        const u0=mU0*i/MSEG, u1=mU0*(i+1)/MSEG;
        const sa=station(u0), sb=station(u1);
        const inner=(st)=>{ const y=st.y;
          return (y>=spec.cockpitTo-0.05 && y<=spec.foredeckTo+0.12)
            ? Math.min(st.ws-TH-spec.capW, SAL.xAt(y, zdl(y))+0.02) : st.ws-TH-spec.capW; };
        const xo0=side*(sa.ws-TH-spec.capW), xi0=side*inner(sa), z0=sa.sheer-spec.drop;
        const xo1=side*(sb.ws-TH-spec.capW), xi1=side*inner(sb), z1=sb.sheer-spec.drop;
        const q = side>0 ? [[xi0,sa.y,z0],[xo0,sa.y,z0],[xo1,sb.y,z1],[xi1,sb.y,z1]]
                         : [[xo0,sa.y,z0],[xi0,sa.y,z0],[xi1,sb.y,z1],[xo1,sb.y,z1]];
        face(q,'cream',-0.6);
      }
      // ---- transom + doors ----
      const tp=(s,f)=>skin(s,0,f);
      for(const [f0,f1,mat,b] of OB)
        face([tp(-1,f1),tp(1,f1),tp(1,f0),tp(-1,f0)], mat, (b||0)-0.8, 0.005);
      (function(){ const s0=station(0), zt=s0.sheer, wsx=s0.ws-TH;
        face([[-wsx,s0.y,zt],[wsx,s0.y,zt],[wsx,s0.y+spec.capW,zt-0.004],[-wsx,s0.y+spec.capW,zt-0.004]],'teak',-0.9,0.03);
        const D=spec.doors, y=s0.y-0.02;
        const panel=(xa,xb,za,zb,mat,b)=>face([[xb,y,zb],[xa,y,zb],[xa,y,za],[xb,y,za]],mat,b,DBP);
        panel(D.tr[0], D.tr[1], DECK+0.05, DECK+D.trH, 'dark', -0.5);
        panel(D.tr[0]-0.06, D.tr[0]-0.02, DECK+0.05, DECK+D.trH, 'steel', -1.0);
        panel(D.tu[0], D.tu[1], DECK+0.04, DECK+D.tuH, 'dark', -0.7);
        panel(D.tu[1]+0.02, D.tu[1]+0.06, DECK+0.04, DECK+D.tuH, 'steel', -1.2);
      })();
      // topside vents (louvre slit, high in the topsides)
      if(spec.vent) for(const s of [-1,1]){
        const u=uOf(spec.vent.y), st=station(u), z=st.kz+st.dep*0.80;
        boxF([s*(hbOf(u,0.80)-0.02), spec.vent.y, z],[0.05, spec.vent.len/2, 0.13],'iron',0.05,0.04);
      }
      // hull side lights (slim lozenge let into the topsides)
      (spec.hullWins||[]).forEach(w=>{
        const n=w.n||16, yA=w.y-w.len/2, yB=w.y+w.len/2, e=w.ease||0.30;
        const pin=(p)=> p>=e?1:Math.sqrt(Math.max(0,1-(1-p/e)*(1-p/e)));
        for(const side of [-1,1])
        for(const [off,ex,mat,b] of [[0.014,0.055,'iron',-0.10],[0.034,0,'glas',side<0?-0.30:-0.95]]){
          for(let i=0;i<n;i++){
            const E=(t)=>{ const y=lerp(yA,yB,t), u=uOf(y), st=station(u);
              const k=Math.min(pin(t),pin(1-t)), half=(w.f1-w.f0)/2*k;
              if(half<0.004) return null;
              const dz=ex/Math.max(0.5,st.dep)*k, mid=(w.f0+w.f1)/2;
              return {u, lo:Math.max(0.36,mid-half-dz), hi:Math.min(0.90,mid+half+dz)}; };
            const A=E(i/n), B=E((i+1)/n); if(!A||!B) continue;
            const P=(uu,f)=>{ const q=skin(side,uu,f); return [q[0]+side*off, q[1], q[2]]; };
            pushO([P(A.u,A.lo),P(A.u,A.hi),P(B.u,B.hi),P(B.u,B.lo)],[side,0,0],mat,b,DBP+(mat==='glas'?0.02:0));
          }
        }
      });

      // ---- cambered foredeck, edge landing on the covering board ----
      const FSEG=12, FCAP=0.988;
      const fzE=(u)=>sheerZ(u)-spec.drop;
      const fw=(u)=>Math.max(0.02, station(u).ws-TH-spec.capW);
      const fyr=(u)=>station(u).y + bowRake(u,1);
      const FU0=uOf(spec.foredeckTo-0.30);
      const FX=[0,0.55,0.85,1.0];
      const fzc=(u,t)=>fzE(u)+spec.fcam*(1-t*t);
      for(let i=0;i<FSEG;i++){
        const u0=FU0+(FCAP-FU0)*i/FSEG, u1=FU0+(FCAP-FU0)*(i+1)/FSEG;
        for(let k=0;k+1<FX.length;k++) for(const sd of [-1,1])
          pushO([[sd*fw(u0)*FX[k],fyr(u0),fzc(u0,FX[k])],[sd*fw(u0)*FX[k+1],fyr(u0),fzc(u0,FX[k+1])],
                 [sd*fw(u1)*FX[k+1],fyr(u1),fzc(u1,FX[k+1])],[sd*fw(u1)*FX[k],fyr(u1),fzc(u1,FX[k])]],
                [sd*FX[k]*0.4,0,1],'cream',0.55-0.1*k,-0.02);
      }
      // ---- bow rail, pulpit, windlass, nav housings ----
      (function(){
        const us=spec.railU;
        const pt=(u,s)=>{ const st=station(u); return [s*(st.ws-TH-spec.capW*0.45), fyr(u), fzE(u)]; };
        for(const s of [-1,1]){
          let prev=null;
          for(const u of us){
            const p=pt(u,s), top=[p[0],p[1],p[2]+spec.railH];
            tubeF(p, top, 0.034, 'steel', 0.1);
            if(prev) tubeF(prev, top, 0.032, 'steel', 0.28);
            prev=top;
          }
          const pf=pt(FCAP,s); tubeF(prev,[pf[0]*0.55,pf[1],pf[2]+spec.railH*0.84],0.032,'steel',0.28);
        }
        const u=0.985, y=fyr(u), z=fzE(u);
        boxF([0, y+spec.pulpit*0.8, z+0.04],[spec.capW*0.55, spec.pulpit, 0.04],'cream',0.6,-0.02);
        boxF([0, y-spec.pulpit*1.8, z+0.12],[0.19,0.22,0.12],'steel',-1.4,-0.02);
        boxF([0, y-spec.pulpit*4.0, z+0.09],[0.06,0.07,0.09],'iron',0.15,-0.02);
      })();
      for(const s of [-1,1]){
        const u=spec.navU, st=station(u);
        boxF([s*(st.ws-TH-spec.capW*0.6), fyr(u), fzE(u)+0.09],[0.07,0.10,0.09],'iron',0.1,-0.02);
      }
      boxF([0, station(0).y+spec.capW*0.5, sheerZ(0)+0.08],[0.06,0.05,0.08],'iron',0.1,-0.02);
      for(const s of [-1,1]) boxF([s*(station(0).ws-spec.capW), station(0).y+spec.capW*1.4,
        sheerZ(0)+0.05],[0.055,0.10,0.055],'iron',0.15,-0.02);

      // ---- mezzanine against the raked house aft wall ----
      (function(){ const MZ=spec.mezz;
        const st=station(uOf(MZ.y1)), hw=Math.min(SAL.xAt(MZ.y1, zdl(MZ.y1))+0.24, st.ws-TH-0.10);
        const z=DECK+MZ.z;
        face([[-hw,MZ.y0,z],[hw,MZ.y0,z],[hw,MZ.y1,z],[-hw,MZ.y1,z]],'teak',0.35,-0.01);
        face([[-hw,MZ.y0,DECK],[hw,MZ.y0,DECK],[hw,MZ.y0,z],[-hw,MZ.y0,z]],'cream',-0.2,DBP);
        boxF([0,(MZ.y0+MZ.y1)/2+MZ.bench, z+MZ.seat/2],[hw*0.64,MZ.seat*0.7,MZ.seat/2],'cream',0.15,-0.02);
        boxF([0,MZ.y1-0.10, z+MZ.seat+MZ.back/2],[hw*0.64,0.07,MZ.back/2],'cream',0.45,-0.02);
      })();

      // ---- aft deck at upper level (90: behind the skylounge, over the mezzanine) ----
      if(spec.aftDeck){
        const AD=spec.aftDeck, rr=Math.min(0.62,(AD.yf-AD.ya)*0.34), N=8;
        const adw=(y)=>{ const d=Math.min(y-AD.ya, AD.yf-y);
          if(d>=rr) return AD.hx;
          const t=clamp01((rr-d)/rr); return Math.max(0.012, AD.hx*Math.sqrt(Math.max(0,1-t*t))); };
        crownSlab({ya:AD.ya, yf:AD.yf, hx:AD.hx, z:AD.z, th:AD.th||0.14, cam:0.05,
                   r:rr, rf:0.24, mat:'cream', n:N});
        const RH=AD.railH||0, ya=AD.ya+0.13, yf=AD.yf-0.16;
        const rx=(y)=>Math.max(0.10, adw(y)-0.16);
        if(RH>0){ for(const s2 of [-1,1]){
          for(const y of [ya,(ya+yf)/2,yf])
            tubeF([s2*rx(y),y,AD.z+0.05],[s2*rx(y),y,AD.z+RH],0.034,'steel',0.1);
          for(const f of [1,0.55])
            tubeF([s2*rx(ya),ya,AD.z+RH*f],[s2*rx(yf),yf,AD.z+RH*f],f>0.9?0.032:0.026,'steel',0.26);
        }
        for(const f of [1,0.55])
          tubeF([-rx(ya),ya,AD.z+RH*f],[rx(ya),ya,AD.z+RH*f],f>0.9?0.032:0.026,'steel',0.26); }
      }

      // ---- bridge furniture (helm console + seats on the open bridge / in nothing visible when enclosed) ----
      if(spec.helmPod){
        const HP=spec.helmPod;
        const o={ya:HP.y-0.32, yf:HP.y+0.32, hx:HP.hx||0.72, z0:HP.z, z1:HP.z+0.78, cx:HP.x||0,
                 cam:0.05, rp:0.22, sh:0.18, mat:'cream', b:0.25, ny:8};
        const hw=pod(o);
        podGlass(o, hw, HP.z+0.48, HP.z+0.72);
        boxF([HP.x||0,HP.y-0.08,HP.z+0.86],[0.55,0.12,0.04],'iron',-0.2,-0.04);
        (HP.seats||[]).forEach(st=>boxF([st[0],st[1],HP.z+0.30],[0.28,0.24,0.30],'cream',0.1,-0.02));
      }

      // ---- TUNA TOWER (optional — the 90 gets height for free and carries an open coaming instead) ----
      if(spec.tower)(function(){ const TW=spec.tower;
        // pads + near-vertical legs
        TW.feet.forEach((p,i)=>{ for(const s of [-1,1]){
          boxF([s*p[0],p[1],p[2]+0.025],[0.15,0.19,0.05],'cream',0.35,-0.02);
          tubeF([s*p[0],p[1],p[2]],[s*TW.top[i][0],TW.top[i][1],TW.z],TW.r,'steel',s>0?-0.3:0.15);
        }});
        // one aft-raking diagonal per side
        for(const s of [-1,1]) tubeF([s*TW.feet[0][0],TW.feet[0][1],TW.feet[0][2]+0.04],
                                     [s*TW.top[1][0],TW.top[1][1],TW.z-0.05],TW.r*0.62,'steel',0.25);
        crownSlab({ya:TW.ty[0], yf:TW.ty[1], hx:TW.tx, z:TW.z, th:0.09, cam:0.07,
                   r:Math.min(0.5,(TW.ty[1]-TW.ty[0])*0.32), rf:0.28, mat:'cream'});
        // rail
        const RH=TW.railH||0.62, RX=TW.tx-0.06;
        for(const s of [-1,1]){
          for(const y of [TW.ty[0]+0.06, TW.ty[1]-0.06])
            tubeF([s*RX, y, TW.z+0.06],[s*RX, y, TW.z+RH],0.032,'steel',0.1);
          tubeF([s*RX, TW.ty[0]+0.06, TW.z+RH],[s*RX, TW.ty[1]-0.06, TW.z+RH],0.030,'steel',0.26);
          tubeF([s*RX, TW.ty[0]+0.06, TW.z+RH*0.52],[s*RX, TW.ty[1]-0.06, TW.z+RH*0.52],0.024,'steel',0.26);
        }
        tubeF([-RX, TW.ty[0]+0.06, TW.z+RH],[RX, TW.ty[0]+0.06, TW.z+RH],0.030,'steel',0.26);
        // control head
        (function(){
          const cs=TW.consoleW||0.40, cy0=TW.ty[1]-0.30-cs*0.5;
          const o={ya:cy0, yf:cy0+cs*0.80, hx:cs, cam:0.05, rp:cs*0.34, sh:0.14, mat:'cream', ny:8};
          const chw=pod(Object.assign({z0:TW.z+0.08, z1:TW.z+0.08+cs*1.05}, o));
          podGlass(o, chw, TW.z+0.08+cs*0.52, TW.z+0.08+cs*0.98);
        })();
        if(TW.seats) for(const s of [-1,1])
          boxF([s*TW.seats, TW.ty[0]+0.42, TW.z+0.26],[0.26,0.22,0.20],'cream',0.15,-0.02);
        // crowned buggy top on slim posts, radome + whips above
        const yc=(TW.ty[0]+TW.ty[1])/2;
        if(TW.btop){
          const HT=TW.btop;
          crownSlab({ya:HT.ya, yf:HT.yf, hx:HT.x, z:HT.z, th:0.08, cam:0.11,
                     r:Math.min(0.55,(HT.yf-HT.ya)*0.30), rf:0.28, mat:'cream'});
          for(const s of [-1,1]) for(const y of [HT.ya+0.24, HT.yf-0.26])
            tubeF([s*(HT.x-0.18), y, TW.z+0.08],[s*(HT.x-0.18), y, HT.z],0.042,'steel',0.1);
          for(const s of [-1,1]) taperF([s*0.55, yc-0.15, HT.z+0.08],[s*0.85, yc-0.75, TW.antZ],0.040,0.011,'steel',0.3);
          taperF([0.25, yc-0.30, HT.z+0.08],[0.35, yc-0.95, TW.antZ-0.35],0.030,0.009,'steel',0.3);
        } else {
          tubeF([0, yc+0.10, TW.z+0.10],[0, yc+0.10, TW.z+0.30],0.22,'cream',0.15);
          for(const s of [-1,1]) taperF([s*0.45, yc-0.10, TW.z+0.10],[s*0.66, yc-0.55, TW.antZ],0.034,0.010,'steel',0.3);
        }
        // ladders
        (TW.lads||[]).forEach(Ld=>ladder(Ld[0],Ld[1],Ld[2],Ld[3],Ld[4],Ld[5],Ld[6],TW.r));
      })();
      (spec.lads||[]).forEach(Ld=>ladder(Ld[0],Ld[1],Ld[2],Ld[3],Ld[4],Ld[5],Ld[6],0.05));
      // off-centre exterior ladders (pass 2): the routes UP to the helms
      (spec.extLadders||[]).forEach(Ld=>{
        const a=[Ld.cx-Ld.hw,Ld.y0,Ld.z0], b=[Ld.cx-Ld.hw,Ld.y1,Ld.z1], c=[Ld.cx+Ld.hw,Ld.y0,Ld.z0], d=[Ld.cx+Ld.hw,Ld.y1,Ld.z1];
        tubeF(a,b,0.032,'steel',0.2); tubeF(c,d,0.032,'steel',-0.3);
        for(let k=1;k<Ld.rungs;k++){ const tt=k/Ld.rungs;
          tubeF([a[0],lerp(a[1],b[1],tt),lerp(a[2],b[2],tt)],[c[0],lerp(c[1],d[1],tt),lerp(c[2],d[2],tt)],0.022,'steel',0.3); }
      });
      (spec.whips||[]).forEach(w=>taperF(w[0],w[1],w[2],w[3],'steel',0.3));
      if(spec.radome){ const RD=spec.radome;
        tubeF([RD.x||0,RD.y,RD.z],[RD.x||0,RD.y,RD.z+RD.h],0.055,'steel',0.1);
        tubeF([RD.x||0,RD.y,RD.z+RD.h],[RD.x||0,RD.y,RD.z+RD.h+0.30],0.26,'cream',0.2); }
      // ---- sleek hardtop over the top station ----
      if(spec.hardtop){ const HT=spec.hardtop;
        crownSlab({ya:HT.ya, yf:HT.yf, hx:HT.x, z:HT.z, th:0.10, cam:HT.cam==null?0.10:HT.cam,
                   r:Math.min(0.60,(HT.yf-HT.ya)*0.30), rf:0.26, mat:'cream'});
        (HT.posts||[]).forEach(p=>{ for(const s of [-1,1])
          tubeF([s*p[0],p[1],p[2]],[s*p[0]*0.96,p[1],HT.z],0.042,'steel',0.12); });
      }
      // ---- electronics: open-array radar + satellite radomes ----
      (spec.domes||[]).forEach(D=>{
        const z0=D.z, r=D.r||0.26, h=D.h||0.40;
        if(D.type==='array'){
          tubeF([D.x,D.y,z0],[D.x,D.y,z0+0.20],0.045,'steel',0.1);
          boxF([D.x,D.y,z0+0.26],[D.len||0.55,0.085,0.055],'cream',0.35,-0.02);
        } else {
          tubeF([D.x,D.y,z0],[D.x,D.y,z0+0.10],0.06,'steel',0.0);
          tubeF([D.x,D.y,z0+0.08],[D.x,D.y,z0+0.08+h*0.62],r,'cream',0.25);
          taperF([D.x,D.y,z0+0.08+h*0.62],[D.x,D.y,z0+0.08+h],r,r*0.45,'cream',0.35);
          boxF([D.x,D.y,z0+0.08+h],[r*0.42,r*0.42,0.02],'cream',0.5,-0.02);
        }
      });

      // ---- rocket launcher + covering-board rod holders ----
      (function(){
        const RL=spec.rocket;
        boxF([RL.cx||0,RL.y,RL.z],[RL.x+0.08,0.07,0.07],'steel',-0.6,-0.02);
        for(let k=0;k<RL.n;k++){
          const x=(RL.cx||0)-RL.x + 2*RL.x*(RL.n===1?0.5:k/(RL.n-1));
          tubeF([x,RL.y,RL.z],[x*1.10, RL.y-0.28, RL.z+0.44],0.052,'iron',0.1);
        }
        for(const s of [-1,1]) spec.capRods.forEach(y=>{
          const st=station(uOf(y)), z=st.sheer;
          tubeF([s*(st.ws-TH-spec.capW*0.55), y, z-0.02],[s*(st.ws-TH-spec.capW*0.18), y, z+0.30],0.042,'steel',-1.4);
        });
      })();

      // ---- outriggers: one cell, two states ----
      (function(){ const OR=spec.rig;
        for(const s of [-1,1]){
          boxF([s*OR.x, OR.y, OR.z-0.05],[0.09,0.15,0.08],'iron',0.15,-0.03);
          tubeF([s*OR.x, OR.y, OR.z],[s*(OR.x+0.09), OR.y, OR.z+0.24],0.055,'steel',0.2);
        }
        const A=52*DEG, EL=12*DEG, ST=21*DEG;
        for(const [name,dirv] of [['out',(s)=>[s*Math.sin(A)*Math.cos(EL), -Math.cos(A)*Math.cos(EL), Math.sin(EL)]],
                                  ['stowed',(s)=>[s*0.10, -Math.sin(ST), Math.cos(ST)]]]){
          TARGET=RIGF[name];
          for(const s of [-1,1]){
            const P0=[s*(OR.x+0.09), OR.y, OR.z+0.24], d=v_norm(dirv(s));
            const P1=v_add(P0, v_mul(d, OR.len)), PM=v_add(P0, v_mul(d, OR.len*0.42));
            taperF(P0,PM,0.070,0.042,'steel', s>0?-0.3:0.3);
            taperF(PM,P1,0.042,0.013,'steel', s>0?-0.3:0.3);
          }
          TARGET=F;
        }
      })();
    })();

    // ---- bake ----
    const _fl={};
    function faceList(riggers){
      const k=RIGF[riggers]?riggers:'stowed';
      return _fl[k] || (_fl[k]=F.concat(RIGF[k]));
    }
    // ---- the salon slider — built per render so opts.doorOpen (0..1) can pose it ----
    const DS=spec.door||null;
    let DOOR=null, HOUSE=null, DOOR2=null;
    if(DS){
      const wD=DS.x1-DS.x0, mid=+(DS.x0+wD*0.5).toFixed(3);
      DOOR={ kind:'slide', face:'aft', y:DS.y, x0:DS.x0, x1:mid, z0:DS.z0, z1:DS.z1,
             travel:+(wD*0.5).toFixed(3), clearAt:0.55, sillZ:spec.interior.soleZ,
             leaf:{ x0:DS.x0, x1:mid } };
      if(spec.door2){ const d2=spec.door2, w2=d2.x1-d2.x0;
        DOOR2={ kind:'slide', face:'aft', y:d2.y, x0:d2.x0, x1:d2.x1, z0:d2.z0, z1:d2.z1,
                travel:+(w2*0.98).toFixed(3), clearAt:0.60, sillZ:d2.sill,
                leaf:{ x0:d2.x0, x1:d2.x1 } }; }
      const IT=spec.interior;
      HOUSE={ kind:'sport', soleZ:IT.soleZ, door:DOOR, door2:DOOR2||undefined,
        levels:IT.bridge?['bridge','house','below']:['house','below'],
        decks:{ house:Object.assign({ soleZ:IT.soleZ }, IT.house, { hxAt:(z)=>IT.house.hx }),
                bridge:IT.bridge ? Object.assign({}, IT.bridge, { hxAt:(z)=>IT.bridge.hx })
                                 : { soleZ:IT.bridgeSole, external:true },
                below:Object.assign({}, IT.below) },
        block:{ hx:IT.house.hx, y0:IT.house.y0, y1:IT.house.y1, topZ:IT.house.ceilZ },
        helms:IT.helms||[], ladders:IT.ladders||[],
        mainDeckExternal:true, excluded:IT.excluded||{} };
    }
    function doorFacesFn(t){
      if(!DS) return [];
      t=Math.max(0,Math.min(1, t!=null ? +t : 0));
      const out=[];
      const one=(ds, V, twoPanel)=>{
        const q=(x0,x1,z0,z1,off,mat,b,db)=>out.push(faceO(
          [[x0,V.fA(z0)-off,z0],[x1,V.fA(z0)-off,z0],[x1,V.fA(z1)-off,z1],[x0,V.fA(z1)-off,z1]],
          [0,-1,0.10], mat, b, db==null?DBP+0.02:db));
        q(ds.x0,ds.x1,ds.z0,ds.z1,-0.16,'dark',-1.3,-0.02);                         // through the opening: dim room
        let lx0, lx1;
        if(twoPanel){ const mid=ds.x0+(ds.x1-ds.x0)*0.5;
          q(mid,ds.x1,ds.z0,ds.z1,0.02,'iron',-0.20,DBP+0.02);                      // fixed pane frame
          q(mid+0.05,ds.x1-0.05,ds.z0+0.06,ds.z1-0.06,0.045,'glas',-0.55,DBP+0.03); // fixed pane
          const sft=t*(mid-ds.x0); lx0=ds.x0+sft; lx1=mid+sft;
        } else { const sft=t*(ds.x1-ds.x0)*0.98; lx0=ds.x0+sft; lx1=ds.x1+sft; }
        q(lx0,lx1,ds.z0,ds.z1,0.085,'iron',-0.05,DBP+0.03);                         // leaf frame
        q(lx0+0.05,lx1-0.05,ds.z0+0.06,ds.z1-0.06,0.11,'glas',-0.45,DBP+0.04);      // leaf glass
        const zt=ds.z1+0.05, zm=(ds.z0+ds.z1)/2, trkEnd=twoPanel?ds.x1:ds.x1+(ds.x1-ds.x0);
        out.push.apply(out,tube([ds.x0-0.06,V.fA(zt)-0.10,zt],[trkEnd+0.06,V.fA(zt)-0.10,zt],0.028,'steel',0.25));   // track
        out.push.apply(out,tube([lx1-0.09,V.fA(zm)-0.13,ds.z0+0.70],[lx1-0.09,V.fA(zm)-0.13,ds.z0+1.12],0.020,'steel',0.35)); // pull
      };
      one(spec.door, VOLS.salon, true);
      if(spec.door2 && VOLS.sky) one(spec.door2, VOLS.sky, false);
      return out;
    }
    function render(dir, opts){
      opts=(typeof opts==='number')?{elev:opts}:(opts||{});
      const fl=faceList(opts.riggers||'stowed');
      const faces = DS ? fl.concat(doorFacesFn(opts.doorOpen)) : fl;
      return _toRGBA(_paint(faces, Object.assign({},opts,{dir}), G, matsFor(opts.paint)), G.W, G.H);
    }
    const ROCK=spec.ROCK;
    function rockMotion(i, frames){
      frames=frames||ROCK.frames;
      const a=2*Math.PI*(((i%frames)+frames)%frames)/frames;
      return { roll:ROCK.rollA*Math.sin(a), pitch:ROCK.pitchA*Math.sin(a+Math.PI/2), heave:ROCK.heaveA*Math.sin(a) };
    }

    // ---- anchors ----
    const norm=(opts)=>Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}));
    function proj(p, dir, opts){
      const o=norm(opts); o.dir=dir;
      const B=camBasis(o), q=projVert(p.x,p.y,p.z,B,G);
      return { x:q.sx, y:q.sy };
    }
    const A=spec.anchors;
    const RODS=(function(){
      const out=[]; const RL=spec.rocket;
      for(let k=0;k<RL.n;k++){ const x=(RL.cx||0)-RL.x + 2*RL.x*(RL.n===1?0.5:k/(RL.n-1));
        out.push({x:x*1.10, y:RL.y-0.28, z:RL.z+0.44}); }
      for(const s of [-1,1]) spec.capRods.forEach(y=>{
        const st=station(uOf(y)); out.push({x:s*(st.ws-TH-spec.capW*0.15), y, z:st.sheer+0.34}); });
      return out;
    })();
    const DOORS=(function(){ const s0=station(0), D=spec.doors;
      return { transom:{x:(D.tr[0]+D.tr[1])/2, y:s0.y, z:DECK+D.trH*0.5},
               tuna:   {x:(D.tu[0]+D.tu[1])/2, y:s0.y, z:DECK+D.tuH*0.5} }; })();
    const TUBS=spec.tubs.map(t=>({x:t[0],y:t[1],z:DECK+(t[2]||0)}));
    const NAV=(function(){ const st=station(spec.navU), s0=station(0);
      return { port:{x:-(st.ws-TH-spec.capW*0.6), y:st.y+bowRake(spec.navU,1), z:st.sheer+0.05},
               star:{x: (st.ws-TH-spec.capW*0.6), y:st.y+bowRake(spec.navU,1), z:st.sheer+0.05},
               stern:{x:0, y:s0.y+spec.capW*0.5, z:s0.sheer+0.14},
               mast: spec.mast ? spec.mast
                 : {x:0, y:(spec.tower.ty[0]+spec.tower.ty[1])/2, z:spec.tower.antZ-0.10} }; })();
    const MEZZ={x:0, y:(spec.mezz.y0+spec.mezz.y1)/2+spec.mezz.bench, z:DECK+spec.mezz.z+spec.mezz.seat};
    const CHAIR={x:0, y:spec.chair.y, z:DECK+0.20};
    // door threshold anchor + open-state report for the enter cue
    function doorMount(dir, opts){
      if(!DOOR) return null;
      const o=norm(opts); o.dir=dir;
      const B=camBasis(o), t=Math.max(0,Math.min(1, o.doorOpen!=null ? +o.doorOpen : 0));
      const q=projVert((DOOR.x0+DOOR.x1)/2, DOOR.y, DOOR.sillZ, B, G);
      const le=projVert(DOOR.leaf.x1+t*DOOR.travel, DOOR.y, DOOR.sillZ, B, G);
      return { x:q.sx, y:q.sy, lead:{x:le.sx,y:le.sy}, open:t, clear:t>=DOOR.clearAt };
    }
    // hull half-width at (y, z) — the flare law included — and sheer height at y (metres in/out)
    function halfAtZ(y, z){ const u=clamp01(uOf(y)), st=station(u);
      const fr=clamp01((z-st.kz)/st.dep); return hbOf(u,fr)-TH; }
    const loft = DS ? { station, skin, bowRake, halfAtZ, sheerZ:(y)=>sheerZ(clamp01(uOf(y))),
      L, TH, DECK, SOLE_U:uOf(spec.cockpitTo), NSEG, house:HOUSE,
      shade:{ GAIN, BIAS, LN, BAYER, KEY, EDGE:0.30 }, cell:{ W:G.W, H:G.H, cx:G.cx, cy:G.cy, S } } : null;

    return {
      id:spec.id, label:spec.label, note:spec.note, loa:spec.loa, L, DECK,
      doorMount, DOOR, HOUSE, loft,
      W:G.W, H:G.H, PX, DIRS:8, pivot:{x:G.cx,y:G.cy}, defaultElev:DEFAULT_ELEV,
      order:['N','NE','E','SE','S','SW','W','NW'],
      RIGGERS:['stowed','out'], PAINTS, paintRamps, defaultPaint:'gelcoat',
      faceCount:F.length + RIGF.stowed.length,
      sheerZ, station, render, ROCK, rock:rockMotion,
      HELM:A.helm, helmSeat:(d,o)=>proj(A.helm,d,o),
      TOWER_STATION:A.towerStation, towerStation:(d,o)=>proj(A.towerStation,d,o),
      CHAIR, chairMount:(d,o)=>proj(CHAIR,d,o),
      MEZZ, mezzSeat:(d,o)=>proj(MEZZ,d,o),
      RODS, rodMounts:(d,o)=>RODS.map(p=>proj(p,d,o)),
      DOORS, doorMounts:(d,o)=>({transom:proj(DOORS.transom,d,o), tuna:proj(DOORS.tuna,d,o)}),
      TUBS, tubMounts:(d,o)=>TUBS.map(p=>proj(p,d,o)),
      NAV, navMounts:(d,o)=>({port:proj(NAV.port,d,o), star:proj(NAV.star,d,o),
                              stern:proj(NAV.stern,d,o), mast:proj(NAV.mast,d,o)}),
    };
  }

  // flare exponent: 1.0 amidships-aft, rising to ~1.7 at the stem
  const FLARE = [[0,1.00],[0.38,1.02],[0.60,1.16],[0.78,1.40],[0.90,1.62],[1,1.70]];

  // ===================================================================================================
  // HULL 1 — 16.2 m convertible ("53"): open flybridge in a coaming on the salon roof, pipe tower over.
  // ===================================================================================================
  const CONVERTIBLE = makeRig({
    id:'convertible', label:'53\u2032 CONVERTIBLE', loa:'16.2 m \u00b7 53\u2032',
    note:'Salon deckhouse + open flybridge coaming; pipe tower straddling the bridge; dark wrapped salon glass, moulded WHITE venturi on the bridge; aft wall tucks forward under the overhanging bridge deck.',
    L:16.2, TH:0.06, DECK:1.25, RAKE:0.85, NSEG:30, capW:0.34,
    drop:0.07, fcam:0.14, toe:[0.55,0.15],
    W:820, H:770, cx:410, cy:475,
    ROCK:{ frames:8, rollA:2.1, pitchA:1.25, heaveA:0.95, period:3.0 },
    // plan: NARROWEST at the transom, max beam at u .56 abreast the house nose, fine entry
    T:[ [0.00,2.05,1.72,0.10],[0.12,2.22,1.94,0.03],[0.24,2.38,2.10,0.00],[0.36,2.50,2.16,0.00],
        [0.48,2.56,2.10,0.01],[0.56,2.58,1.92,0.04],[0.66,2.52,1.60,0.11],[0.75,2.38,1.18,0.24],
        [0.83,2.14,0.78,0.40],[0.89,1.82,0.48,0.52],[0.94,1.40,0.26,0.62],[0.97,0.95,0.13,0.68],
        [1.00,0.10,0.03,0.74] ],
    // sheer: low transom, accelerates through midships, crest at u .945, eases DOWN to the stem head
    sheer:[ [0.00,2.30],[0.16,2.42],[0.34,2.62],[0.50,2.90],[0.64,3.24],[0.76,3.62],
            [0.86,3.96],[0.915,4.14],[0.945,4.20],[1.00,4.12] ],
    flare:FLARE,
    cockpitTo:-4.90, foredeckTo:1.70,
    volumes:[
      { id:'salon', z0:1.75, z1:4.65, ns:24, nz:10, nr:0.95, ar:0.70, aw:0.62, inset:0.16, cam:0.16,
        nose:[[1.75,1.72],[2.6,1.62],[3.4,1.38],[4.1,1.05],[4.65,0.72]],
        aft: [[1.75,-5.30],[3.2,-5.16],[4.65,-4.88]],
        hw:  [[1.7,1.75],[0.8,2.05],[0,2.15],[-2,2.20],[-4.9,2.10]],
        bands:[ { yEnd:-3.30, ease:1.0, zRef:4.05,
                  lo:[[-3.5,4.02],[-1.5,3.76],[1.6,3.52]],
                  hi:[[-3.5,4.34],[1.6,4.40]] } ],
        aftGlass:[] },
      // open flybridge: a low coaming ring with a venturi screen, sunk sole, its own raked front
      { id:'bridge', z0:4.63, z1:5.42, ns:20, nz:4, nr:0.75, ar:0.60, aw:0.65, inset:0.06, cam:0,
        clampHull:false, open:{rim:0.15, sole:4.86},
        nose:[[4.63,-0.30],[5.42,-0.72]],
        aft: [[4.63,-4.72],[5.42,-4.56]],
        hw:  [[-0.3,1.35],[-1.5,1.75],[-3.0,1.85],[-4.95,1.62]] },
    ],
    helmPod:{ x:0, y:-1.85, z:4.86, hx:0.68, seats:[[-0.6,-4.2],[0.6,-4.2]] },
    // the salon slider (pass 2): sill at mezzanine height — walk in level off the mezz deck
    door:{ x0:-0.85, x1:0.75, y:-5.26, z0:1.80, z1:3.55 },
    extLadders:[ { cx:1.30, hw:0.22, y0:-5.52, z0:1.82, y1:-4.98, z1:4.72, rungs:9 } ],
    interior:{ soleZ:1.78, bridgeSole:4.86,
      ladders:[ { id:'flybridge_ladder', face:'aft', x:1.30, y:-5.30, z0:1.78, z1:4.86,
                  connects:['mezzanine','bridge_sole'],
                  note:'Mezzanine to the flybridge, salon aft wall stbd of the slider — the EXTERIOR route to the upper helm (the interior companionway is the other).' } ],
      helms:[
        { id:'helm_bridge', deck:'bridge_sole', pos:[0,-1.85,4.86], reach:[0,-2.50,4.86],
          note:'flybridge helm pod — the boat is worked from here; reached by the interior companionway (STAIRS.house_to_bridge)' },
        { id:'helm_tower', deck:'tower_platform', pos:[0,-2.95,7.10], reach:[0,-3.55,7.00],
          note:'tuna tower control head — reached by the tower ladder (extractor tower_platform._notes.tower_ladder)' } ],
      house:{ ceilZ:4.42, hx:1.95, y0:-5.18, y1:1.55,
              portholes:{ ys:[-2.6,-1.5,-0.4,0.7], z0:3.62, z1:4.26 } },
      below:{ soleZ:0.55, ceilZ:2.45, y0:0.45, y1:4.50, hxCap:1.85 },
      excluded:{
        bridge_route_closed:'THE FINDING CLOSED: the interior companionway salon→bridge_sole is modelled (STAIRS.house_to_bridge) — the flybridge is reachable without a game-side teleport',
        foredeck_route:'no exterior route cockpit→foredeck — the extractor finding stands (no washboards by design)',
        below_aft:'the below flat’s aft end tucks under the raised salon — full headroom holds only forward of the trunk (authored abstraction, flagged)' } },
    aftDeck:{ ya:-5.62, yf:-4.78, hx:1.94, z:4.66, th:0.12, railH:0 },
    tower:{ feet:[[1.82,-1.35,4.70],[1.82,-4.30,4.68]], top:[[0.85,-2.05],[0.85,-3.85]],
            tx:0.95, ty:[-4.00,-1.85], z:7.00, r:0.048, railH:0.58, consoleW:0.36, antZ:9.60,
            btop:{ x:0.85, ya:-3.70, yf:-2.10, z:8.30 },
            lads:[[0.40,-4.55,5.40, 0.34,-4.05,7.00, 5]] },
    domes:[ {type:'array', x:0, y:-2.55, z:8.42, len:0.44},
            {x:0.02, y:-3.30, z:8.40, r:0.21, h:0.34} ],
    mezz:{ y0:-5.90, y1:-5.26, z:0.52, bench:0.06, seat:0.40, back:0.38 },
    chair:{ y:-6.55, r:0.42, foot:0.46 },
    hatches:[[0,-7.30,0.42,0.26],[-1.25,-6.35,0.25,0.26,false],[1.25,-6.35,0.25,0.26,false]],
    doors:{ tr:[0.70,1.50], trH:1.00, tu:[-1.35,-0.65], tuH:0.60 },
    vent:{ y:-5.9, len:1.10 },
    hullWins:[{ y:0.55, len:1.90, f0:0.55, f1:0.67 }],
    railU:[0.845,0.895,0.940], railH:0.58, pulpit:0.30, navU:0.885,
    capRods:[-5.45,-6.85], rocket:{ n:5, x:1.00, y:-4.62, z:5.55 },
    tubs:[[-1.55,-7.30,0],[1.55,-7.30,0],[-1.62,-6.10,0],[1.62,-6.10,0]],
    rig:{ x:1.58, y:-4.30, z:4.90, len:7.5 },
    anchors:{ helm:{x:0,y:-1.85,z:4.80}, towerStation:{x:0,y:-2.95,z:7.10} },
  });

  // ===================================================================================================
  // HULL 2 — 27.4 m skybridge ("90"): salon + enclosed skylounge as stacked volumes, tuna tower
  // standing ON the skylounge roof (the roof is the tower floor), aft deck over the mezzanine.
  // ===================================================================================================
  const SKYBRIDGE = makeRig({
    id:'skybridge', label:'90\u2032 SKYBRIDGE', loa:'27.4 m \u00b7 90\u2032',
    note:'Salon, full-height skylounge and an open white control coaming stacked in ONE gradual raked ascent — the coaming IS the tower, the height comes free; aft walls tuck forward so each deck floats out over the one below; teak caps and planked sole.',
    L:27.4, TH:0.08, DECK:2.05, RAKE:1.35, NSEG:34, capW:0.42,
    drop:0.12, fcam:0.19, toe:[0.55,0.20],
    W:1200, H:1170, cx:600, cy:730,
    ROCK:{ frames:8, rollA:1.35, pitchA:0.80, heaveA:0.70, period:4.2 },
    T:[ [0.00,2.72,2.30,0.14],[0.10,2.98,2.62,0.05],[0.20,3.20,2.86,0.00],[0.30,3.38,3.00,0.00],
        [0.42,3.56,3.04,0.00],[0.50,3.63,2.92,0.02],[0.56,3.66,2.72,0.06],[0.64,3.62,2.34,0.14],
        [0.72,3.50,1.86,0.28],[0.79,3.30,1.38,0.44],[0.85,3.02,0.94,0.60],[0.90,2.64,0.60,0.74],
        [0.94,2.14,0.36,0.86],[0.97,1.46,0.18,0.95],[1.00,0.14,0.04,1.04] ],
    sheer:[ [0.00,3.15],[0.15,3.28],[0.32,3.52],[0.48,3.86],[0.62,4.30],[0.74,4.82],
            [0.84,5.38],[0.91,5.78],[0.945,5.92],[1.00,5.80] ],
    flare:FLARE,
    cockpitTo:-9.60, foredeckTo:2.40,
    volumes:[
      // SALON — huge foredeck ahead of it (>40% LOA), smoothly raked front, level dark ribbon
      { id:'salon', z0:2.60, z1:7.25, ns:26, nz:12, nr:1.30, ar:0.90, aw:0.60, inset:0.22, cam:0.18,
        nose:[[2.6,2.42],[3.6,2.28],[4.8,1.90],[5.8,1.48],[6.6,1.12],[7.25,0.82]],
        aft: [[2.6,-10.35],[4.6,-10.12],[6.0,-9.82],[7.25,-9.55]],
        hw:  [[2.4,2.60],[1.4,3.00],[0,3.20],[-2,3.30],[-6,3.30],[-10.5,3.12]],
        bands:[ { yEnd:-6.30, ease:1.3, zRef:6.40,
                  lo:[[-6.6,6.45],[-4,6.10],[-1,5.85],[2.4,5.62]],
                  hi:[[-6.6,6.92],[2.4,6.98]] } ],
        aftGlass:[[-2.35,-1.55,3.30,4.35]] },
      // SKYLOUNGE — set well back on the salon roof, steeper rake, its own ribbon, raked aft wall
      { id:'sky', z0:7.22, z1:9.55, ns:22, nz:8, nr:1.15, ar:0.80, aw:0.62, inset:0.14, cam:0.15,
        clampHull:false,
        nose:[[7.22,0.25],[8.4,-0.15],[9.55,-0.55]],
        aft: [[7.22,-9.60],[9.55,-9.05]],
        hw:  [[0.3,1.60],[-1,2.10],[-3,2.42],[-5,2.45],[-9.6,2.20]],
        bands:[ { yEnd:-7.30, ease:1.1, zRef:8.90,
                  lo:[[-7.5,8.80],[-4,8.45],[0.2,8.15]],
                  hi:[[-7.5,9.22],[0.2,9.30]] } ],
        aftGlass:[] },
      // open control coaming ON the skylounge roof — the 53's upper station verbatim; this IS the tower
      { id:'bridge', z0:9.53, z1:10.36, ns:20, nz:4, nr:0.80, ar:0.60, aw:0.65, inset:0.06, cam:0,
        clampHull:false, open:{rim:0.16, sole:9.74},
        nose:[[9.53,-1.10],[10.36,-1.52]],
        aft: [[9.53,-8.75],[10.36,-8.48]],
        hw:  [[-1.1,1.20],[-2.5,1.70],[-4.5,1.80],[-8.75,1.52]] },
    ],
    // open aft deck at skylounge level over the mezzanine — floats past the tucked-forward wall below
    aftDeck:{ ya:-11.55, yf:-9.45, hx:2.55, z:7.28, th:0.15, railH:0.66 },
    helmPod:{ x:0, y:-3.30, z:9.74, hx:0.70, seats:[[-0.62,-5.9],[0.62,-5.9]] },
    // the salon slider (pass 2): sill at mezzanine height
    door:{ x0:-1.30, x1:1.50, y:-10.24, z0:2.90, z1:4.65 },
    // second-level skylounge slider onto the aft deck (pass 2) — replaces the fixed sky aft pane
    door2:{ x0:0.35, x1:1.30, y:-9.36, z0:7.45, z1:9.10, sill:7.28 },
    extLadders:[ { cx:2.05, hw:0.24, y0:-11.30, z0:2.90, y1:-10.85, z1:7.24, rungs:13 } ],
    interior:{ soleZ:2.83, bridgeSole:9.74,
      // the FULL DECK dedicated to the helm: the skylounge, baked as a real interior level
      bridge:{ deckId:'bridge_sole', soleZ:7.30, ceilZ:9.35, hx:2.28, y0:-9.50,
               front:{ yBot:0.05, yTop:-0.50 },
               frontGlass:{ z0:8.20, z1:9.18, panes:[[-1.45,-0.10],[0.10,1.45]] },
               sideGlass:{ z0:8.45, z1:9.15, runs:[[-7.2,-5.8],[-5.4,-4.0],[-3.6,-2.2]] },
               aftGlass:{ z0:7.62, z1:8.88, panes:[[-2.05,-0.45]] } },
      ladders:[ { id:'aft_deck_ladder', face:'aft', x:2.05, y:-11.00, z0:2.83, z1:7.28,
                  connects:['mezzanine','aft_deck'],
                  note:'Mezzanine to the skylounge aft deck, starboard quarter — first leg of the exterior route up.' },
                { id:'bridge_ladder', face:'aft', x:0, y:-9.40, z0:7.32, z1:9.55,
                  connects:['aft_deck','bridge_sole'],
                  note:'Aft deck to the open coaming — the rig’s own raked ladder, declared as a route (second leg to the upper helm).' } ],
      helms:[
        { id:'helm_bridge', deck:'bridge_sole', pos:[0,-3.30,9.74], reach:[0,-3.95,9.74],
          note:'open control coaming helm pod — reached by the aft-deck ladder (extractor bridge_sole._notes.bridge_ladder)' } ],
      house:{ ceilZ:7.00, hx:3.02, y0:-10.05, y1:1.20,
              portholes:{ ys:[-5.4,-4.0,-2.6,-1.2,0.2], z0:5.72, z1:6.86 } },
      below:{ soleZ:0.60, ceilZ:2.58, y0:0.20, y1:6.80, hxCap:2.60 },
      excluded:{
        foredeck_route:'no exterior route cockpit→foredeck — the extractor finding stands',
        below_aft:'the below flat’s aft end tucks under the raised salon — full headroom holds only forward of the trunk (authored abstraction, flagged)' } },
    lads:[[0.45,-9.70,7.32, 0.40,-9.08,9.55, 6]],
    whips:[[[1.02,-7.95,11.18],[1.42,-9.15,14.20],0.038,0.010],
           [[-1.02,-7.95,11.18],[-1.42,-9.15,14.20],0.038,0.010]],
    hardtop:{ x:1.55, ya:-8.20, yf:-2.60, z:11.15, cam:0.10,
              posts:[[1.62,-3.15,10.30],[1.55,-7.50,10.30]] },
    domes:[ {type:'array', x:0, y:-5.10, z:11.26, len:0.62},
            {x:0.58, y:-6.55, z:11.23, r:0.33, h:0.50},
            {x:-0.55, y:-6.60, z:11.22, r:0.23, h:0.38} ],
    mast:{ x:0, y:-5.10, z:11.95 },
    mezz:{ y0:-11.40, y1:-10.32, z:0.78, bench:0.12, seat:0.46, back:0.48 },
    chair:{ y:-12.35, r:0.58, foot:0.66 },
    hatches:[[0,-12.90,0.64,0.42],[0,-11.55,0.50,0.28],[-2.25,-12.05,0.30,0.32,false],[2.25,-12.05,0.30,0.32,false]],
    doors:{ tr:[1.05,2.20], trH:1.35, tu:[-2.00,-1.00], tuH:0.80 },
    vent:{ y:-8.6, len:1.90 },
    hullWins:[{ y:1.00, len:3.20, f0:0.548, f1:0.685 }],
    railU:[0.845,0.895,0.940], railH:0.70, pulpit:0.42, navU:0.885,
    capRods:[-10.20,-11.60,-12.90], rocket:{ n:7, x:1.45, y:-11.30, z:7.98 },
    tubs:[[-2.30,-13.05,0],[2.30,-13.05,0],[-2.50,-11.30,0],[2.50,-11.30,0],[0,-13.25,0]],
    rig:{ x:2.02, y:-7.60, z:9.66, len:9.5 },
    anchors:{ helm:{x:0.65,y:-3.60,z:7.45}, towerStation:{x:0,y:-3.30,z:9.85} },
  });

  const HULLS = [CONVERTIBLE, SKYBRIDGE];
  root.SportFisherIso2 = {
    CONVERTIBLE, SKYBRIDGE, HULLS,
    byId:(id)=>HULLS.filter(h=>h.id===id)[0] || CONVERTIBLE,
    PX, DIRS:8, order:['N','NE','E','SE','S','SW','W','NW'],
    PAINTS, paintRamps, defaultPaint:'gelcoat',
    HULL, BOOT, CREAM, BLUE, TEAK, DECKF, GRIP, GLAS, STEEL, IRON, KEY,
  };
})(typeof globalThis!=='undefined'?globalThis:window);

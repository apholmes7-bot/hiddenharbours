/* Hidden Harbours — parametric ISO centre-console skiff (M2 bake recipe, ADR-0006 — same pipeline
   as puntIsoRig.js / doryIsoRig.js). PASS 2: geometry fixes, fit-out hardware, colourways.
   The step between the punt and the Cape Islander: ~7.0 m LOA, flared bow, wide low transom cut for
   an outboard, self-draining wood sole, painted liner. Centre console amidships with raked
   windscreen, wheel and a side-mount throttle binnacle; helm seat just aft; cambered teal-canvas
   canopy on four legs over both. Fixed 3/4 turntable camera (elev 40deg default), 45deg steps,
   flat-facet shading from the upper-left key, z-buffered, ordered dither, 1px keyline, NO AA.
   32 px = 1 m.

   PASS-2 CHANGES
   1. Foredeck sits flush at the sheer, cut inboard of the gunwale cap, so it no longer spills over
      the rail; the bulkhead under its lip is a TRAPEZOID that follows the hull's inner section, so
      its lower corners no longer punch out through the topsides. Coaming lip caps the joint.
   2. Per-material dither weight (mat.dith): flat facets landing between two ramp steps used to read
      as a 50% checkerboard across a whole panel. Painted mats now round to the nearest ramp step
      instead — crisp banded paint at the same brightness — while the sole keeps some grain.
   3. Canopy: narrower footprint, real camber (two facets per slope, higher ridge) and a depth-tested
      CONTACT SHADOW on the sole so it stops floating.
   4. Fit-out: throttle binnacle + grab rail + recessed door on the console, sole hatch with a ring
      pull, rubbing strake along the sheer, bow eye, two quarter cleats.
   5. COLOURWAYS — paint is data, not baked pixels, exactly as on the punt. render(dir,{scheme:'…'})
      picks a named preset; render(dir,{paint:{hull,trim,cove,bottom,interior,canopy,console}}) mixes
      any base colours (bottom defaults to trim, console to hull, interior to bare wood + hull liner).
      rampFrom(hex,n) derives the ramp in OKLCH (locked lightness curve, chroma capped at C_CAP,
      shadows rotated cool / highlights warm) so a free colour choice cannot go off-model; chipWall()
      is the legal swatch grid a picker offers. Six fleet schemes shared with the punt, three
      skiff-only ones that use the canopy and console slots.

   Cell 244x216, pivot (122,120) = boat origin (amidships, keel bottom, centreline), pinned every
   heading. Outboard ships as its own pivoting layer (remote-steer variant of the punt motor); the
   transom clamp point is baked: motorMount(dir,opts) -> cell {x,y}. Pass the hull's rock(i) values
   so overlays ride the same wave. helmSeat / wheelHub / painter / sternEye / tubMounts / pilotStand
   are baked the same way. The WHEEL IS A LAYER: renderWheel(dir,{steer|deg, elev, roll,pitch,heave})
   bakes the spoked wheel into the hull's own cell at the hull's pivot, so it foreshortens correctly at
   all eight headings and rides the same rock(i) as the hull. wheelRig.js is the control-scale version of the
   same wheel for the helm view.
   Exposes globalThis.ConsoleIso = { W,H,PX,DIRS,pivot,order,ROCK,rock(i),render(dir,opts),
   motorMount,MOUNT, helmSeat,HELM, wheelHub,WHEEL_HUB, painter,PAINTER, sternEye,STERN_EYE,
   TUBS,tubMounts, PILOT,pilotStand, SCHEMES,schemeIds,defaultScheme,palette,rampFrom,chipWall,C_CAP,
   PAINT,TRIM,GOLD,CANV,GLAS,WOOD,IRON,MOTO,KEY }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 244, H = 216, cx = 122, cy = 120;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const ROCK = { frames: 8, rollA: 3.4, pitchA: 1.9, heaveA: 1.3, period: 2.8 };  // heavier hull, stiffer than the punt
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 7.0, TH = 0.045, DECK = 0.28;
  const NSEG = 20;

  // paint ramps dark->light (KTC: fleet scheme — teal #2ba39a / white #eef0ea / gold #e0b13a)
  const PAINT = ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];  // white topsides + console
  const TRIM  = ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'];                      // teal sheer band + bottom + rails
  const GOLD  = ['#7a5a1c','#a8842a','#e0b13a'];                                          // cove pinstripe
  const CANV  = ['#2a5750','#3d7469','#559182','#74ad97','#97c6ab'];                      // sun-faded canopy canvas
  const GLAS  = ['#16333c','#24505a','#3a7680','#5fa3a6','#8fc9c4'];                      // windscreen glass
  const WOOD  = ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48','#9a7853','#a98352'];  // sole + seats (dory ramp)
  const IRON  = ['#20180f','#2a2014','#3a2c1c'];
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];  // canopy legs, wheel, rails
  const KEY   = '#101a19';

  // ---- paint mixer (OKLCH): any base colour -> a fleet-legal ramp -------------------------------
  // The rig owns the ramp SHAPE (lightness curve, chroma ceiling, hue rotation); the caller only
  // picks base colours. That is what keeps a free colour choice from going off-model. Shared
  // envelope with puntIsoRig.js so the two boats stay in the same fleet.
  const clamp01=(v)=>v<0?0:v>1?1:v;
  const s2l=(c)=>c<=0.04045?c/12.92:Math.pow((c+0.055)/1.055,2.4);
  const l2s=(c)=>c<=0.0031308?c*12.92:1.055*Math.pow(c,1/2.4)-0.055;
  function hex2oklch(hex){
    const R=s2l(parseInt(hex.slice(1,3),16)/255), G=s2l(parseInt(hex.slice(3,5),16)/255), B=s2l(parseInt(hex.slice(5,7),16)/255);
    const l=Math.cbrt(0.4122214708*R+0.5363325363*G+0.0514459929*B);
    const m=Math.cbrt(0.2119034982*R+0.6806995451*G+0.1073969566*B);
    const s=Math.cbrt(0.0883024619*R+0.2817188376*G+0.6299787005*B);
    const Lo=0.2104542553*l+0.7936177850*m-0.0040720468*s;
    const A=1.9779984951*l-2.4285922050*m+0.4505937099*s;
    const Bb=0.0259040371*l+0.7827717662*m-0.8086757660*s;
    return { L:Lo, C:Math.hypot(A,Bb), h:(Math.atan2(Bb,A)*180/Math.PI+360)%360 };
  }
  function _lin(L2,C,h){
    const a=C*Math.cos(h*DEG), b=C*Math.sin(h*DEG);
    const l_=L2+0.3963377774*a+0.2158037573*b, m_=L2-0.1055613458*a-0.0638541728*b, s_=L2-0.0894841775*a-1.2914855480*b;
    const l=l_*l_*l_, m=m_*m_*m_, s=s_*s_*s_;
    return [ 4.0767416621*l-3.3077115913*m+0.2309699292*s,
            -1.2684380046*l+2.6097574011*m-0.3413193965*s,
            -0.0041960863*l-0.7034186147*m+1.7076147010*s ];
  }
  function oklch2hex(L2,C,h){                 // gamut-fit by walking chroma down, so hue/value survive
    let c=C;
    for(let i=0;i<14;i++){ const v=_lin(L2,c,h); if(v.every(x=>x>=-0.002&&x<=1.002)) break; c*=0.9; }
    const v=_lin(L2,c,h).map(x=>('0'+Math.round(clamp01(l2s(x))*255).toString(16)).slice(-2));
    return '#'+v.join('');
  }
  const HUE_WARM=92, HUE_COOL=258;           // key light / sky fill — every ramp in the fleet leans these ways
  const C_CAP=0.115;                         // saturation ceiling: no neon paint in this harbour
  function rotToward(h,t,amt){ const d=((t-h+540)%360)-180; return (h+d*amt+360)%360; }
  function rampFrom(hex, n, o){
    o=o||{};
    const anchor=o.anchor!=null?o.anchor:0.78, g=o.gamma||0.88, b=hex2oklch(hex);
    const C0=Math.min(b.C, o.cap!=null?o.cap:C_CAP);
    const Ldk=Math.max(0.24, b.L*(o.floor!=null?o.floor:0.60));
    const Lhi=Math.min(0.985, b.L+(1-b.L)*(o.ceil!=null?o.ceil:0.38));
    const out=[];
    for(let i=0;i<n;i++){
      const t=i/(n-1), k=t-anchor;
      let Lv = t<anchor ? Ldk+(b.L-Ldk)*Math.pow(t/anchor,g) : b.L+(Lhi-b.L)*((t-anchor)/(1-anchor));
      const C = Math.max(0.003, C0*(k<0 ? 1+0.10*(-k) : 1-0.55*k));
      const h = rotToward(b.h, k<0?HUE_COOL:HUE_WARM, Math.min(0.14, Math.abs(k)*0.20));
      let hx = oklch2hex(Lv,C,h);
      for(let s=0; s<6 && out.length && hx===out[out.length-1]; s++){ Lv=Math.min(0.99,Lv+0.022); hx=oklch2hex(Lv,C,h); }
      out.push(hx);
    }
    return out;
  }
  // the legal swatch grid a picker offers: chroma peaks midtone, neutral column first
  const CHIP_HUES=[null,22,48,84,132,172,196,232,268,318];
  const CHIP_L=[0.30,0.44,0.57,0.70,0.83,0.94];
  function chipWall(){
    return CHIP_L.map(Lv=>CHIP_HUES.map(h=>{
      if(h==null) return oklch2hex(Lv,0.006,236);
      const env=Math.sin(Math.PI*clamp01((Lv-0.10)/0.88));
      return oklch2hex(Lv, Math.min(C_CAP, 0.018+0.098*env), h);
    }));
  }

  // ---- colourways: presets are just base colours; the mixer derives the ramps -------------------
  // First six are the punt's canon schemes, value for value, so a harbour of mixed boats still reads
  // as one fleet. The last three are skiff-only — they use the two slots the punt hasn't got.
  const SCHEMES = {
    'harbour-white': { name:'Harbour White', hull:'#eef0ea', trim:'#2ba39a', cove:'#e0b13a', canopy:'#74ad97',
                       ramps:{ hull:PAINT, trim:TRIM, cove:GOLD, canopy:CANV, console:PAINT }, // hand-tuned pass-1 bake, kept exact
                       note:'the fleet scheme — matches her buoy' },
    'dory-buff':     { name:'Dory Buff',    hull:'#e3d1a4', trim:'#8f3527', cove:'#3b3a33', canopy:'#c8b58c',
                       note:'cream topsides, oxblood rail, bare sole' },
    'bottle-green':  { name:'Bottle Green', hull:'#31694c', trim:'#e2dcc7', cove:'#e0b13a', bottom:'#8c3f2c',
                       interior:'#cdb98d', canopy:'#e2dcc7', note:'dark green, cream sheer, buff sole' },
    'squall-grey':   { name:'Squall Grey',  hull:'#8a99a1', trim:'#22354a', cove:'#c9ccc4', bottom:'#7c3a2a',
                       interior:'#909c99', canopy:'#5d6f7a', note:'workaday grey-blue, deck-grey sole' },
    'cranberry':     { name:'Cranberry',    hull:'#a8452f', trim:'#e6ddc6', cove:'#e0b13a', canopy:'#e6ddc6',
                       note:'bog red, cream boot top' },
    'tar-black':     { name:'Tar Black',    hull:'#2a2f33', trim:'#2ba39a', cove:'#e0b13a', canopy:'#2ba39a',
                       note:'tarred topsides, teal rail and awning' },
    'sand-canvas':   { name:'Sand Canvas',  hull:'#eef0ea', trim:'#22354a', cove:'#e0b13a', canopy:'#d9c79a',
                       note:'skiff only — navy sheer under a sand awning' },
    'spruce-shade':  { name:'Spruce Shade', hull:'#31694c', trim:'#e2dcc7', cove:'#e0b13a', bottom:'#8c3f2c',
                       interior:'#cdb98d', canopy:'#2a4a3c', console:'#e2dcc7',
                       note:'skiff only — cream console in deep spruce shade' },
    'teal-console':  { name:'Teal Console', hull:'#eef0ea', trim:'#2ba39a', cove:'#e0b13a', canopy:'#e6ddc6',
                       console:'#2ba39a', note:'skiff only — teal console, bone awning' },
  };
  const DEFAULT_SCHEME='harbour-white';

  const _pal={}; let _palOrder=[];
  function palette(o){
    o=o||{};
    const DF=SCHEMES[DEFAULT_SCHEME];
    const base = o.paint || SCHEMES[o.scheme] || DF;
    const hull=base.hull||DF.hull, trim=base.trim||DF.trim, cove=base.cove||DF.cove;
    const bottom=base.bottom||trim, interior=base.interior||null;
    const canopy=base.canopy||DF.canopy, cons=base.console||hull;
    const key=[hull,trim,cove,bottom,interior||'bare',canopy,cons,base.ramps?'baked':'mix'].join('|');
    if(_pal[key]) return _pal[key];
    const R=base.ramps||{};
    const hullR=R.hull||rampFrom(hull,7), trimR=R.trim||rampFrom(trim,5,{anchor:0.75}),
          coveR=R.cove||rampFrom(cove,3,{anchor:0.85}),
          canvR=R.canopy||rampFrom(canopy,5,{anchor:0.72}),
          consR=(cons===hull ? hullR : (R.console||rampFrom(cons,7)));
    const botR = bottom===trim ? trimR : rampFrom(bottom,5,{anchor:0.75});
    const intR = interior ? rampFrom(interior,7,{anchor:0.72}) : null;
    const linerR = intR || hullR;                 // painted liner: interior slot, else the topside white
    const soleR  = intR || WOOD;                  // sole + hatch: interior slot, else bare wood
    // dith = ordered-dither weight. 1 = full 4x4 Bayer, so a flat facet landing between two ramp
    // steps becomes a 50% checkerboard across the whole panel; 0 = round to the nearest step, i.e.
    // crisp banded paint at the same brightness. Painted areas are crisp; the sole and the bare wood
    // keep a little grain, where the pattern reads as texture rather than noise.
    const mats = { paint:{ramp:hullR,off:0,dith:0},   trim:{ramp:trimR,off:-1,dith:0},
                   gold:{ramp:coveR,off:-3,dith:0},   bottom:{ramp:botR,off:-1,dith:0},
                   liner:{ramp:linerR,off:0,dith:0}, sole:{ramp:soleR,off:0,dith:0.5},
                   cons:{ramp:consR,off:0,dith:0},    canv:{ramp:canvR,off:0,dith:0},
                   wood:{ramp:WOOD,off:0,dith:0.45},  glas:{ramp:GLAS,off:0,dith:0},
                   iron:{ramp:IRON,off:-2,dith:0},    moto:{ramp:MOTO,off:0,dith:0},
                   blk:{ramp:MOTO,off:-2,dith:0} };
    const rindex={};
    [hullR,trimR,coveR,botR,linerR,soleR,consR,canvR,WOOD,GLAS,IRON,MOTO,PAINT].forEach(r=>r.forEach((c,i)=>{ if(!rindex[c]) rindex[c]={r,i}; }));
    const out={ key, id:o.paint?'custom':(SCHEMES[o.scheme]?o.scheme:DEFAULT_SCHEME), name:base.name||'Custom',
                base:{hull,trim,cove,bottom,interior,canopy,console:cons}, note:base.note||'',
                ramps:{hull:hullR,trim:trimR,cove:coveR,bottom:botR,interior:soleR,canopy:canvR,console:consR},
                mats, rindex };
    _pal[key]=out; _palOrder.push(key);
    if(_palOrder.length>24){ delete _pal[_palOrder.shift()]; }
    return out;
  }

  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m) ----
  // fuller and deeper than the punt: flared bow, flat run aft, wide transom for the outboard.
  const T = [
    [0.92,0.74,0.66,0.06],   // transom
    [1.05,0.84,0.64,0.01],
    [1.12,0.90,0.63,0.00],
    [1.15,0.92,0.62,0.00],
    [1.13,0.89,0.62,0.00],
    [1.06,0.80,0.64,0.02],
    [0.90,0.62,0.68,0.07],
    [0.62,0.34,0.74,0.16],
    [0.07,0.03,0.82,0.30],   // raked stem
  ];
  const lerp = (a,b,t)=>a+(b-a)*t;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), wb:lerp(A[1],B[1],fr), dep:lerp(A[2],B[2],fr),
             kz:lerp(A[3],B[3],fr), y:-L/2+u*L };
  }
  function skin(side,u,frac,inset){
    const st=station(u);
    const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
    return [ side*lerp(wb,ws,frac), st.y, st.kz+lerp(0,dep,frac) ];
  }
  function dfrac(st){ return Math.max(0.04, Math.min(1, (DECK - st.kz)/st.dep)); }
  // fraction of a station's depth at an absolute height, and the inner-skin half-width there —
  // this pair is what keeps deck panels and bulkheads inside the hull instead of through it.
  const zfrac=(st,z)=>Math.max(0.02, Math.min(1, (z-st.kz)/st.dep));
  function innerHalf(u,z){ const st=station(u); return skin(1,u,zfrac(st,z),1)[0]; }

  // ---- generic solids ----
  const ID=(p)=>p;
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
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
  function tube(A,B2,rad,mat,b,xf){
    xf=xf||ID;
    const P0=xf(A), P1=xf(B2);
    const ax=v_norm(v_sub(P1,P0)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P)=>[ v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)), v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                      v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)), v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad)) ];
    const r0=ring(P0), r1=ring(P1), out=[];
    for(let k=0;k<4;k++){ const k2=(k+1)%4; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:-0.15}); }
    return out;
  }

  // ---- face list ----
  const F = [];
  const SHADOWS = [];                                       // depth-tested contact-shadow patches
  const face=(v,mat,b,db)=>F.push({v,mat:mat||'paint',b:b||0,db:db||0});
  const boxF=(c,h,mat,b,db)=>{ F.push.apply(F, box(c,h,mat,b,db)); };
  const tubeF=(A,B2,rad,mat,b)=>{ F.push.apply(F, tube(A,B2,rad,mat,b)); };
  // outer paint scheme: white body, gold cove line, teal sheer band  [f0, f1, mat, b, db]
  const OB = [ [0,1/3,'paint',0,0], [1/3,2/3,'paint',0,0], [2/3,0.79,'paint',0,0],
               [0.79,0.86,'gold',0.3,0.01], [0.86,0.94,'trim',0,0] ];
  const FORE_U = 0.80;                                      // foredeck bulkhead station
  const fz=(u)=>{ const st=station(u); return st.kz+st.dep-0.012; };            // foredeck: flush at the sheer
  const fw=(u)=>{ const st=station(u); return Math.max(0.02, st.ws-TH*1.5-0.014); }; // …cut inboard of the rail cap
  (function build(){
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        // outer skin bands (smooth glass hull — no plank seams)
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],mat,b,db);
        // rubbing strake: proud dark lip capping the sheer band, under the rail
        const sa0=skin(side,u0,0.94), sb0=skin(side,u1,0.94);
        const out=(p)=>[p[0]+side*0.032,p[1],p[2]+0.006];
        const dn=(p)=>[p[0],p[1],p[2]-0.055];
        face([sa0,sb0,out(sb0),out(sa0)],'moto',-0.25,0.03);             // strake top
        face([out(sa0),out(sb0),dn(out(sb0)),dn(out(sa0))],'moto',-1.3,0.03);
        face([skin(side,u0,0.94),skin(side,u1,0.94),skin(side,u1,1),skin(side,u0,1)],'trim',0.05);
        // inner skin, deck line to sheer (painted liner), 2 bands
        const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb);
        for(let k=0;k<2;k++){
          const g0a=fa+(1-fa)*k/2, g1a=fa+(1-fa)*(k+1)/2;
          const g0b=fb+(1-fb)*k/2, g1b=fb+(1-fb)*(k+1)/2;
          face([skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'liner',-1.6);
        }
        // bottom (anti-foul, own colour slot)
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'bottom',-1.0);
        // gunwale cap (teal rail)
        const oa=skin(side,u0,1),ob=skin(side,u1,1),ia=skin(side,u0,1,1),ib=skin(side,u1,1,1);
        const inb=(p)=>[p[0]-side*TH*1.4,p[1],p[2]];
        face([oa,ob,inb(ib),inb(ia)],'trim',0.4,0.03);
      }
    }
    // deck sole (wood or painted), transom to foredeck bulkhead
    const DSEG=16;
    const dw=(u)=>{ const st=station(u); return (lerp(st.wb,st.ws,dfrac(st))-TH)*0.96; };
    for(let i=0;i<DSEG;i++){
      const u0=FORE_U*i/DSEG, u1=FORE_U*(i+1)/DSEG;
      face([[-dw(u0),station(u0).y,DECK],[dw(u0),station(u0).y,DECK],[dw(u1),station(u1).y,DECK],[-dw(u1),station(u1).y,DECK]],'sole',-0.35);
    }
    // transom: paint bands carried across
    const tp=(s,f)=>skin(s,0,f);
    for(const [f0,f1,mat,b] of OB)
      face([tp(-1,f1),tp(1,f1),tp(1,f0),tp(-1,f0)], mat, (b||0)-0.8, 0.005);
    face([tp(-1,1),tp(1,1),tp(1,0.94),tp(-1,0.94)],'trim',-0.75,0.005);
    // motor pad: wood clamp plate proud of the transom, top centre
    (function(){ const st=station(0), zt=st.kz+st.dep, zb=st.kz+st.dep*0.45, y=st.y-0.03, hx=0.17;
      face([[-hx,y,zt],[hx,y,zt],[hx,y,zb],[-hx,y,zb]],'wood',-0.55,-0.03);
      face([[-hx,y,zt],[hx,y,zt],[hx,st.y+0.08,zt],[-hx,st.y+0.08,zt]],'wood',0.35,-0.03);
    })();
    // ---- foredeck: raised bow platform, flush at the sheer and cut inboard of the rail ----
    const FSEG=6;
    for(let i=0;i<FSEG;i++){
      const u0=FORE_U+0.185*i/FSEG, u1=FORE_U+0.185*(i+1)/FSEG;
      face([[-fw(u0),station(u0).y,fz(u0)],[fw(u0),station(u0).y,fz(u0)],
            [fw(u1),station(u1).y,fz(u1)],[-fw(u1),station(u1).y,fz(u1)]],'wood',0.5);
    }
    // bulkhead under the foredeck lip: trapezoid following the hull's inner section (was a straight
    // wall at sheer width, whose lower corners punched out through the topsides)
    (function(){
      const u=FORE_U, st=station(u), y=st.y, zt=fz(u), xt=fw(u), xb=dw(u);
      face([[-xt,y,zt],[xt,y,zt],[xb,y,DECK],[-xb,y,DECK]],'liner',-1.9);
      // coaming lip: short ledge along the top edge so the joint reads finished
      face([[-xt,y,zt],[xt,y,zt],[xt-0.0,y+0.055,zt+0.004],[-xt+0.0,y+0.055,zt+0.004]],'trim',0.55,0.03);
      // anchor-locker hatch let into the foredeck just forward of the bulkhead
      const hy0=y+0.18, hy1=y+0.60, hx=Math.min(0.26, fw(u+0.05)*0.5), hz=fz(u+0.05)+0.004;
      face([[-hx-0.024,hy0-0.024,hz],[hx+0.024,hy0-0.024,hz],[hx+0.024,hy1+0.024,hz],[-hx-0.024,hy1+0.024,hz]],'wood',-1.0,0.02);
      face([[-hx,hy0,hz+0.004],[hx,hy0,hz+0.004],[hx,hy1,hz+0.004],[-hx,hy1,hz+0.004]],'wood',0.45,0.03);
      boxF([0,(hy0+hy1)/2,hz+0.016],[0.026,0.010,0.009],'iron',0.2,0.04);
    })();
    boxF([0,3.10,fz(0.94)+0.045],[0.028,0.10,0.038],'iron',0.15,-0.02);   // bow cleat
    // bow eye: towing ring low on the stem
    (function(){ const u=0.972, st=station(u), z=st.kz+st.dep*0.30, y=st.y+0.03;
      boxF([0,y,z],[0.028,0.022,0.022],'iron',0.4,0.03);
    })();
    // quarter cleats on the aft side decks
    for(const s of [-1,1]){
      const u=0.11, st=station(u);
      boxF([s*(st.ws-TH*1.5), st.y, st.kz+st.dep+0.035],[0.030,0.085,0.032],'iron',0.2,0.04);
    }
    // sole hatch (self-draining bilge access) aft of the console, with a flush ring pull
    (function(){ const y0=-2.18, y1=-1.56, hx=0.28, z=DECK+0.004;
      face([[-hx-0.024,y0-0.024,z],[hx+0.024,y0-0.024,z],[hx+0.024,y1+0.024,z],[-hx-0.024,y1+0.024,z]],'sole',-1.0,0.02);
      face([[-hx,y0,z+0.004],[hx,y0,z+0.004],[hx,y1,z+0.004],[-hx,y1,z+0.004]],'sole',0.4,0.03);
      boxF([0,(y0+y1)/2,z+0.015],[0.030,0.011,0.009],'iron',0.2,0.05);
    })();
    // ---- centre console (slanted front, raked windscreen, wheel + throttle binnacle) ----
    (function(){
      const X0=0.34, XT=0.30, Y0=-0.25, Y1=0.55, YT=0.40, Z0=DECK, ZTF=1.16, ZTA=1.20;
      const A=[-X0,Y0,Z0], B=[X0,Y0,Z0], C=[X0,Y1,Z0], D=[-X0,Y1,Z0];
      const E=[-XT,Y0,ZTA], Fq=[XT,Y0,ZTA], G=[XT,YT,ZTF], Hq=[-XT,YT,ZTF];
      face([Hq,G,C,D],'cons',0.55,-0.01);                        // front slant
      face([E,Fq,G,Hq],'cons',0.85,-0.01);                       // top (helm panel)
      face([A,B,Fq,E],'cons',-0.75,-0.01);                       // aft face (helm side)
      face([B,C,G,Fq],'cons',-0.15,-0.01);                       // starboard side
      face([D,A,E,Hq],'cons',-0.15,-0.01);                       // port side
      // recessed locker door on the aft face + latch
      face([[-0.215,Y0-0.004,0.40],[0.215,Y0-0.004,0.40],[0.215,Y0-0.004,0.86],[-0.215,Y0-0.004,0.86]],'cons',-1.9,0.03);
      boxF([0.155,Y0-0.02,0.63],[0.022,0.014,0.022],'iron',0.3,0.05);
      // windscreen: raked glass off the front top edge + grab rail
      face([[-0.225,0.26,1.42],[0.225,0.26,1.42],[0.27,0.40,1.165],[-0.27,0.40,1.165]],'glas',0.9,-0.02);
      tubeF([-0.24,0.255,1.44],[0.24,0.255,1.44],0.021,'moto',0.2);
      // steering column + hub stub. The spoked wheel is its OWN layer (renderWheel) mounted here,
      // the same way the outboard is a layer on the transom clamp — so it can turn and rock.
      boxF([0,Y0-0.030,0.72],[0.030,0.030,0.20],'moto',-0.35,-0.02);
      boxF([0,Y0-0.052,0.95],[0.052,0.020,0.052],'moto',0.15,-0.03);
      // side-mount binnacle: ONE lever — ahead / neutral / astern, throttle on the same swing
      boxF([0.245,0.10,1.185],[0.072,0.098,0.050],'blk',0.05,0.02);
      tubeF([0.245,0.075,1.215],[0.245,-0.055,1.395],0.019,'moto',0.45);
      boxF([0.245,-0.070,1.410],[0.028,0.028,0.026],'blk',0.55,0.03);
      // grab rail down the port side of the console
      tubeF([-0.352,-0.10,1.00],[-0.352,0.30,1.00],0.018,'moto',0.3);
    })();
    // helm seat (centre, wheel reach) — pedestal box, wood slab, low backrest
    boxF([0,-0.86,0.45],[0.24,0.15,0.17],'cons',-0.35);
    boxF([0,-0.86,0.645],[0.30,0.185,0.035],'wood',0.6);
    boxF([0,-1.055,0.80],[0.28,0.028,0.14],'wood',-0.5);
    // aft bench across the stern
    boxF([0,-3.10,0.42],[0.62,0.12,0.12],'cons',-0.6);
    boxF([0,-3.10,0.575],[0.70,0.155,0.032],'wood',0.55);
    // ---- canopy: four legs + cambered teal canvas top with skirts ----
    for(const s of [-1,1]){
      tubeF([s*0.44,-0.88,DECK],[s*0.375,-0.94,1.90],0.024,'moto',-0.1);
      tubeF([s*0.44, 0.50,DECK],[s*0.375, 0.56,1.90],0.024,'moto',-0.1);
    }
    tubeF([-0.375,-0.94,1.87],[0.375,-0.94,1.87],0.019,'moto',-0.3);
    tubeF([-0.375, 0.56,1.87],[0.375, 0.56,1.87],0.019,'moto',-0.3);
    (function(){
      const Ya=-1.12, Yf=0.70, X=0.52, XM=X*0.55, ZE=1.88, ZM=1.985, ZR=2.035, ZS=ZE-0.075;
      // two facets per slope: the extra crease is what makes the canvas read cambered, not slabbed
      face([[0,Ya,ZR],[0,Yf,ZR],[-XM,Yf,ZM],[-XM,Ya,ZM]],'canv',0.55);
      face([[-XM,Ya,ZM],[-XM,Yf,ZM],[-X,Yf,ZE],[-X,Ya,ZE]],'canv',0.3);
      face([[XM,Ya,ZM],[XM,Yf,ZM],[0,Yf,ZR],[0,Ya,ZR]],'canv',0.05);
      face([[X,Ya,ZE],[X,Yf,ZE],[XM,Yf,ZM],[XM,Ya,ZM]],'canv',-0.2);
      face([[-X,Yf,ZE],[-XM,Yf,ZM],[0,Yf,ZR],[XM,Yf,ZM],[X,Yf,ZE],[X,Yf,ZS],[-X,Yf,ZS]],'canv',-0.5);   // front skirt
      face([[X,Ya,ZE],[XM,Ya,ZM],[0,Ya,ZR],[-XM,Ya,ZM],[-X,Ya,ZE],[-X,Ya,ZS],[X,Ya,ZS]],'canv',-1.0);   // aft skirt
      face([[-X,Ya,ZE],[-X,Yf,ZE],[-X,Yf,ZS],[-X,Ya,ZS]],'canv',-0.6);      // port skirt
      face([[X,Yf,ZE],[X,Ya,ZE],[X,Ya,ZS],[X,Yf,ZS]],'canv',-0.8);          // starboard skirt
      // contact shadow: the canopy footprint dropped on the sole, offset off the key light
      SHADOWS.push({ steps:2, v:[[-X*0.92-0.12,Ya+0.06,DECK+0.002],[X*0.92-0.12,Ya+0.06,DECK+0.002],
                                 [X*0.92-0.12,Yf-0.02,DECK+0.002],[-X*0.92-0.12,Yf-0.02,DECK+0.002]] });
    })();
  })();

  // ---- rasterizer (shared recipe; G overrides cell geometry for future overlay layers) ----
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){
    return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];
  }
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) };
  }
  function projVert(x,y,z,B,G){
    const gx=G?G.cx:cx, gy=G?G.cy:cy;
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:gx+xr*S, sy:gy-(yr*B.se+zr*B.ce)*S - B.heave, d:(yr*B.ce-zr*B.se) };
  }
  function _paint(faces, opts, doEdge, G, shadows){
    const PW=G?G.W:W, PH=G?G.H:H;
    const B=camBasis(opts);
    const pal=palette(opts), MATS=pal.mats, RINDEX=pal.rindex;
    const zbuf=new Float32Array(PW*PH).fill(Infinity);
    const col=new Array(PW*PH).fill(null);
    const dep=new Float32Array(PW*PH);
    const tag=new Int8Array(PW*PH);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B,G));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.paint;
      const base=Math.floor(fidx);
      const frac=Math.max(0,Math.min(1,fidx-base));
      const dith=M.dith==null?1:M.dith;
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
            zbuf[i]=deff; dep[i]=d; tag[i]=f.tag||0;
            const thr = dith<=0 ? 0.5 : 0.5+(BAYER[x&3][y&3]-0.5)*dith;
            let idx=base+(frac>=thr?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    const out=new Array(PW*PH).fill(null);
    for(let i=0;i<PW*PH;i++) out[i]=col[i];
    if(opts.onlyTag){                       // overlay pass: keep only the layer's own pixels
      for(let i=0;i<PW*PH;i++) if(tag[i]!==opts.onlyTag){ out[i]=null; col[i]=null; }
    }
    if(doEdge){
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
    }
    // contact shadows: darken whatever is at or behind the patch plane, leave what is above it alone
    for(const sq of (shadows||[])){
      const rv=sq.v.map(([x,y,z])=>projVert(x,y,z,B,G)), steps=sq.steps||1;
      for(let t=1;t+1<rv.length;t++){
        const a=rv[0], b=rv[t], c=rv[t+1];
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(PW-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(PH-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) continue;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const i=y*PW+x; if(!out[i]) continue;
          const sd=w0*a.d+w1*b.d+w2*c.d;
          if(dep[i] < sd-0.06) continue;                    // in front of the plane: not in shadow
          const e=RINDEX[out[i]]; if(e && e.i>0) out[i]=e.r[Math.max(0,e.i-steps)];
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
    PW=PW||W; PH=PH||H;
    const rgba=new Uint8ClampedArray(PW*PH*4);
    for(let i=0;i<PW*PH;i++){
      const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16);
      rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255;
    }
    return rgba;
  }
  function render(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    return _toRGBA(_paint(F, Object.assign({}, opts, {dir}), true, null, SHADOWS));
  }
  // ---- iso wheel layer -------------------------------------------------------------------------
  // The wheel the player spins is a control-scale rig (wheelRig.js). On the boat it is 0.35 m across
  // — about 11 px — seen at 40 deg from eight headings, so it cannot be that flat sprite: it is built
  // here as real geometry on the console's aft face and rendered into the HULL's cell at the HULL's
  // pivot, exactly like the oar and motor layers. Pass the hull's rock(i) values and it rides the
  // same wave; pass steer (or deg) and the spokes turn about the column axis in 3D, which is what
  // keeps the gold king spoke readable at every heading instead of only head-on.
  const WHEEL_GEO = { rad:0.175, rimIn:0.146, rake:14, knobs:8, spokes:8, seg:14, hubR:0.052 };
  const WHEEL_LOCK = 1.5;                                        // turns each way, matching wheelRig
  function wheelFaces(deg){
    const G=WHEEL_GEO, r=G.rake*DEG, out=[];
    const P0=[WHEEL_HUB.x, WHEEL_HUB.y, WHEEL_HUB.z];
    const n=[0,-Math.cos(r),Math.sin(r)];                        // axis: aft and a touch up
    const e1=[1,0,0], e2=[0,Math.sin(r),Math.cos(r)];            // in-plane basis
    const at=(rho,th,off)=>[ P0[0]+rho*(Math.cos(th)*e1[0]+Math.sin(th)*e2[0])+n[0]*off,
                             P0[1]+rho*(Math.cos(th)*e1[1]+Math.sin(th)*e2[1])+n[1]*off,
                             P0[2]+rho*(Math.cos(th)*e1[2]+Math.sin(th)*e2[2])+n[2]*off ];
    const push=(v,mat,b,db)=>out.push({v,mat,b:b||0,db:db==null?0.30:db,tag:1});
    const th0=(deg||0)*DEG + Math.PI/2;   // 0 deg = king spoke straight up
    // rim: front annulus + outer edge, segment by segment
    for(let k=0;k<G.seg;k++){
      const a0=th0+2*Math.PI*k/G.seg, a1=th0+2*Math.PI*(k+1)/G.seg;
      push([at(G.rimIn,a0,0.016),at(G.rad,a0,0.016),at(G.rad,a1,0.016),at(G.rimIn,a1,0.016)],'blk',0.55);
      push([at(G.rad,a0,0.016),at(G.rad,a0,-0.012),at(G.rad,a1,-0.012),at(G.rad,a1,0.016)],'blk',-0.35);
    }
    // spokes, king spoke in cove gold so the angle reads at 11 px
    for(let k=0;k<G.spokes;k++){
      const a=th0+2*Math.PI*k/G.spokes, w=0.019;
      const ta=a+Math.PI/2, wx=w*(Math.cos(ta)*e1[0]+Math.sin(ta)*e2[0]),
            wy=w*(Math.cos(ta)*e1[1]+Math.sin(ta)*e2[1]), wz=w*(Math.cos(ta)*e1[2]+Math.sin(ta)*e2[2]);
      const A=at(G.hubR*0.8,a,0.012), B=at(G.rimIn+0.004,a,0.012);
      const off=(p,s)=>[p[0]+wx*s,p[1]+wy*s,p[2]+wz*s];
      push([off(A,1),off(B,1),off(B,-1),off(A,-1)], k===0?'gold':'moto', k===0?0.7:0.45);
      // turned knob on the rim
      const K=at(G.rad+0.030,a,0.006), kw=0.024;
      push([off(K,1),[K[0]+n[0]*0.026+wx,K[1]+n[1]*0.026+wy,K[2]+n[2]*0.026+wz],
            [K[0]+n[0]*0.026-wx,K[1]+n[1]*0.026-wy,K[2]+n[2]*0.026-wz],off(K,-1)], k===0?'gold':'blk', 0.1);
      void kw;
    }
    // hub boss
    for(let k=0;k<8;k++){
      const a0=th0+2*Math.PI*k/8, a1=th0+2*Math.PI*(k+1)/8;
      push([P0,at(G.hubR,a0,0.020),at(G.hubR,a1,0.020),P0],'moto',0.8);
    }
    return out;
  }
  function wheelDeg(o){
    o=o||{};
    if(o.deg!=null) return o.deg;
    const turns=o.turns==null?WHEEL_LOCK:o.turns;
    return Math.max(-1,Math.min(1,o.steer||0))*turns*360;
  }
  // same cell, same pivot as the hull — composite straight over the hull sprite, no offset maths
  function renderWheel(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    // painted together with the hull, then masked to the wheel's own pixels — that is what makes the
    // console hide the wheel bow-on instead of the layer floating over it
    return _toRGBA(_paint(F.concat(wheelFaces(wheelDeg(opts))), Object.assign({}, opts, {dir, onlyTag:1}), true));
  }

  const _anchor=(P)=>function(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(P.x, P.y, P.z, B);
    return { x:p.sx, y:p.sy };
  };
  // outboard clamp point (transom top centre) — cell coords; motor layer is remote-steer
  const MOUNT = { x:0, y:-L/2-0.03, z:T[0][3]+T[0][2] };
  const motorMount = _anchor(MOUNT);
  // helm seat anchor (seated operator sprite sits here, facing the bow)
  const HELM = { x:0, y:-0.86, z:0.68 };
  const helmSeat = _anchor(HELM);
  // wheel hub — where wheelRig.js mounts its spinnable wheel on the console's aft face
  const WHEEL_HUB = { x:0, y:-0.31, z:0.95 };
  const wheelHub = _anchor(WHEEL_HUB);
  // painter / bow eye (mooring line origin) and stern eye (tow point)
  const PAINTER = { x:0, y:station(0.988).y-0.05, z:station(0.988).kz+station(0.988).dep*0.42 };
  const painter = _anchor(PAINTER);
  const STERN_EYE = { x:0, y:-L/2+0.02, z:T[0][3]+T[0][2]*0.35 };
  const sternEye = _anchor(STERN_EYE);
  // fish-tub deck mounts (boat-local; carries 3 — two aft quarters, one forward of the console)
  const TUBS = [ {x:-0.48,y:-1.95,z:DECK}, {x:0.48,y:-1.95,z:DECK}, {x:0,y:1.30,z:DECK} ];
  function tubMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  // pilot foot-contact on the sole, standing at the wheel just aft of the console — rides the wave
  const PILOT = { x:0, y:-0.55, z:DECK };
  const pilotStand = _anchor(PILOT);

  root.ConsoleIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], PAINT, TRIM, GOLD, CANV, GLAS, WOOD, IRON, MOTO, KEY,
    render, ROCK, rock:rockMotion,
    motorMount, MOUNT, helmSeat, HELM, wheelHub, WHEEL_HUB, painter, PAINTER, sternEye, STERN_EYE,
    renderWheel, wheelFaces, WHEEL_GEO, WHEEL_LOCK,
    TUBS, tubMounts, PILOT, pilotStand,
    SCHEMES, schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, palette, rampFrom, chipWall, C_CAP };
})(typeof globalThis!=='undefined'?globalThis:window);

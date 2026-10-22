/* Hidden Harbours — GO-FAST, THE NEW LAYOUT (2026-10-09). ADR 0048 §8 A; the contract is
   docs/art/hull-layout-spec.md. One rig, one hull, through its global:
     GoFastIso.byId('go_fast') = { faceList(state,tower), partFaces(id,state), interiorFaces(),
       doorFaces(t), geometry(), matsFor(scheme), walk(), fitCheck(), GAIN, BIAS, LN, BAYER, PX, W, H,
       pivot, defaultElev, schemeIds, defaultScheme, DWL, cockpitZ, loa }
   and the same names on the global for the default hull. Every face carries lv (its level), ro (its
   role) and it (true only on the room's faces). One full mesh, outside and in: the hull, the cuddy
   inside it (10-03's interior rig, moved in), the door leaf and the walk, all from this file.
   Her walk, docs/art/rigs/gameplay/goFastIsoRig.go_fast.walk.json, is WRITTEN by
   tools/rig-recipes/hull-walk.mjs from walk(), never by hand, and stamped with this file's LF sha256.
   WHAT MOVED FROM THE 10-03 PASS (CHANGES.md lists each, old -> new):
     - the wheel: hub (0.42,-1.719,1.10) rake +24 -> (0.42,-1.78,1.40) rake -20, its column along the
       axis into a new helm pod on the dash (x 0.345-0.495): seated, 30 of 30 builds on 10.3
     - the cuddy door: the two glass panes drawn on the console face are the leaf now (doorFaces(t)),
       vertex for vertex when shut; the console face has a real opening behind it (0.40 x 0.51 m),
       lined back to the cuddy's aft bulkhead, and a dark back that lifts with the lid
     - the cuddy itself: inside her hull (fitCheck), with an aft bulkhead at y -1.50; the step moved
       inside it; the forward bulkhead 3.10 -> 3.06
   Every other exterior face is the 10-03 face, vertex for vertex, in every scheme.
   The header below is the 10-03 rig's own, unchanged. */
/* Hidden Harbours — parametric ISO GO-FAST (cigarette-type offshore raceboat) — M2 bake recipe,
   ADR-0006, same pipeline and rasterizer as bowriderIsoRig.js. PASS 1.
   Built to the black-and-yellow reference pair, plan and cockpit placed off the top-down reference:
   an 11.9 m (39 ft) deep-vee raceboat, 2.20 m beam, 24 deg deadrise at the transom, the cockpit from
   20 to 40 % of the hull so 6.9 m of crowned foredeck runs ahead of a smoked wraparound screen, three
   bolsters aft and two forward, twin big-block V8s under a 2.2 m raised engine hatch, twin racing
   sterndrives through the transom, a stainless swim grate over the legs. 32 px = 1 m. Fixed 3/4
   turntable camera (elev 40), 45 deg steps, flat facets, fleet upper-left key, z-buffered, NO AA,
   ringless from birth (ADR 0031: render(dir,{outline:true}) is the A/B).

   LIVERY. A colourway is colours AND a graphic. Graphics are bands on two surface charts:
     hull topsides   u stern 0 -> stem 1, q chine 0 -> sheer 1
     deck and hatch  u, s centreline 0 -> rail 1 (mirrored)
   A band is a pair of edge curves. paintStrip() splits every strip at every band edge and crease,
   so a stripe is a real polygon edge in the mesh, never a texture. Five graphics ship: sting, split,
   stack, claw, drip. render(dir,{scheme}) picks a preset; render(dir,{paint:{...},graphic}) mixes
   any colours through the fleet OKLCH mixer at the fleet cap (C_CAP 0.115).
   Slots: hull (topsides), bottom (below the chine), deck, liner (cockpit + bay), g1 g2 g3 (graphic
   colours; g1 also paints the valve covers), uph acc (vinyl), sole (cockpit floor).

   MOVING PARTS. hatch 0..1 lifts the engine hatch on its transom hinge to 52 deg and builds the bay:
   two big-blocks side by side, valve covers in g1, rams to the lid. The drives are ONE tagged layer
   (tag 2): both legs trim, tilt and steer together. The wheel is tag 1, as on the skiffs.

   Cell 464x384, pivot (232,200) = boat origin (amidships of the 11.9 m hull, keel bottom,
   centreline). Swept over elev 30/40/50 x 8 headings x rock, plane, hard over and their sums x hatch
   and trailer tilt: 2 px or more clear of every edge. bounds(dir,opts) returns the projected box.
   Exposes globalThis.GoFastIso. */
(function (root) {
  const PX = 32, S = 32;
  const W = 464, H = 384, cx = 232, cy = 200;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;

  // ---- poses: 5 t on a narrow deep vee — stiff in pitch, banks hard into a turn -----------------
  const ROCK = { frames: 8, rollA: 2.8, pitchA: 1.3, heaveA: 1.2, period: 2.9 };
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a), yaw: 0 };
  }
  const PLANE = { frames: 12, humpAt: 0.34, humpPitch: 6.2, cruisePitch: 3.0, lift: 9 };
  function planeMotion(t){
    t = Math.max(0, Math.min(1, t||0));
    const hump = t < PLANE.humpAt ? Math.sin(0.5*Math.PI*t/PLANE.humpAt) : Math.pow(Math.max(0, 1-(t-PLANE.humpAt)/(1-PLANE.humpAt)), 1.5);
    const pitch = PLANE.cruisePitch*t + (PLANE.humpPitch - PLANE.cruisePitch*PLANE.humpAt)*hump;
    return { roll: 0, pitch, heave: PLANE.lift*t*t - 2.6*t*(1-t), yaw: 0 };
  }
  const TURN = { maxRoll: 15, maxYaw: 6, pitch: 2.4, heave: 5 };
  function turnMotion(s){
    s = Math.max(-1, Math.min(1, s||0));
    return { roll: s*TURN.maxRoll, pitch: TURN.pitch, heave: TURN.heave, yaw: -s*TURN.maxYaw };
  }
  const posesAdd = (a,b)=>({ roll:(a.roll||0)+(b.roll||0), pitch:(a.pitch||0)+(b.pitch||0),
                             heave:(a.heave||0)+(b.heave||0), yaw:(a.yaw||0)+(b.yaw||0) });

  // ---- fixed ramps (never painted) ----------------------------------------------------------------
  const WHITE = '#eef0ea';
  const PAINT = ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];
  const GLAS  = ['#16333c','#24505a','#3a7680','#5fa3a6','#8fc9c4'];
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];
  const KEY   = '#101a19';

  // ---- paint mixer (OKLCH) — the fleet's shared envelope, value for value ---------------------------
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
  function oklch2hex(L2,C,h){
    let c=C;
    for(let i=0;i<14;i++){ const v=_lin(L2,c,h); if(v.every(x=>x>=-0.002&&x<=1.002)) break; c*=0.9; }
    const v=_lin(L2,c,h).map(x=>('0'+Math.round(clamp01(l2s(x))*255).toString(16)).slice(-2));
    return '#'+v.join('');
  }
  const HUE_WARM=92, HUE_COOL=258;
  const C_CAP=0.115;
  // the go-fast is the one boat allowed louder than the fleet: livery ramps cap at 0.145.
  // render(dir,{cap:C_CAP}) holds it to the fleet envelope.
  const LIVERY_CAP=0.145;
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
  const CHIP_HUES=[null,22,48,84,132,172,196,232,268,318];
  const CHIP_L=[0.30,0.44,0.57,0.70,0.83,0.94];
  function chipWall(){
    return CHIP_L.map(Lv=>CHIP_HUES.map(h=>{
      if(h==null) return oklch2hex(Lv,0.006,236);
      const env=Math.sin(Math.PI*clamp01((Lv-0.10)/0.88));
      return oklch2hex(Lv, Math.min(C_CAP, 0.018+0.098*env), h);
    }));
  }

  // ---- colourways: colours + a graphic -------------------------------------------------------------
  const BLACK = '#1f2225';
  const SCHEMES = {
    'hornet':   { name:'Hornet',   graphic:'sting', hull:BLACK, bottom:BLACK, deck:'#f0c22e', liner:'#2a2e31',
                  g1:'#f0c22e', g2:'#8d9598', g3:BLACK, uph:'#2c3033', acc:'#f0c22e', sole:'#3b4044',
                  note:'the reference pair: black hull, yellow deck poured down the quarters, black spear and hatch' },
    'corsa':    { name:'Corsa',    graphic:'split', hull:'#c3c8c8', bottom:'#2a2f33', deck:'#b8352b', liner:WHITE,
                  g1:'#b8352b', g2:BLACK, g3:WHITE, uph:'#e6e3da', acc:'#b8352b', sole:'#4b5054',
                  note:'red over silver on a raked split, black pin, twin white stripes transom to stem' },
    'biscayne': { name:'Biscayne', graphic:'stack', hull:WHITE, bottom:'#1d6a6e', deck:WHITE, liner:WHITE,
                  g1:'#1f8c8a', g2:'#e07a62', g3:'#1f8c8a', uph:'#e8e3d3', acc:'#1f8c8a', sole:'#5e6a69',
                  note:'white glass, three stacked stripes peeling off the sheer, teal bottom, coral pin' },
    'blaze':    { name:'Blaze',    graphic:'claw', hull:'#4b5358', bottom:BLACK, deck:'#565f64', liner:'#3b4246',
                  g1:'#e0672b', g2:BLACK, g3:'#b9bfc0', uph:'#30353a', acc:'#e0672b', sole:'#3b4044',
                  note:'gunmetal, three orange claws at the bow, chevrons up the foredeck, orange hump' },
    'venom':    { name:'Venom',    graphic:'drip', hull:'#93c43f', bottom:BLACK, deck:BLACK, liner:'#2a2e31',
                  g1:BLACK, g2:'#93c43f', g3:'#b9bfc0', uph:'#2c3033', acc:'#93c43f', sole:'#3b4044',
                  note:'acid green topsides under a black deck that runs over the sheer and drips' },
  };
  const DEFAULT_SCHEME = 'hornet';
  const SLOTS = ['hull','bottom','deck','liner','g1','g2','g3','uph','acc','sole'];

  const _pal={}; let _palOrder=[];
  function palette(o){
    o=o||{};
    const DF=SCHEMES[DEFAULT_SCHEME];
    const base = o.paint || SCHEMES[o.scheme] || DF;
    const P={}; for(const k of SLOTS) P[k]=String(base[k]||DF[k]).toLowerCase();
    const cap=o.cap!=null?+o.cap:LIVERY_CAP;
    const key=SLOTS.map(k=>P[k]).join('|')+'|'+cap;
    if(_pal[key]) return _pal[key];
    const rc={};
    const r7=(hex)=>rc[hex] || (rc[hex] = hex===WHITE ? PAINT : rampFrom(hex,7,{cap}));
    const R={ hull:r7(P.hull), bottom:r7(P.bottom), deck:r7(P.deck), liner:r7(P.liner),
              g1:r7(P.g1), g2:r7(P.g2), g3:r7(P.g3),
              uph:rampFrom(P.uph,5,{anchor:0.72}), acc:rampFrom(P.acc,5,{anchor:0.72,cap}), sole:rampFrom(P.sole,5,{anchor:0.70}) };
    const mats = { hull:{ramp:R.hull,off:0,dith:0}, bottom:{ramp:R.bottom,off:0,dith:0}, deck:{ramp:R.deck,off:0,dith:0},
                   liner:{ramp:R.liner,off:0,dith:0}, g1:{ramp:R.g1,off:0,dith:0}, g2:{ramp:R.g2,off:0,dith:0}, g3:{ramp:R.g3,off:0,dith:0},
                   uph:{ramp:R.uph,off:0,dith:0.12}, acc:{ramp:R.acc,off:0,dith:0.10}, sole:{ramp:R.sole,off:0,dith:0.40},
                   steel:{ramp:STEEL,off:0,dith:0}, glas:{ramp:GLAS,off:-2,dith:0},
                   moto:{ramp:MOTO,off:0,dith:0}, blk:{ramp:MOTO,off:-2,dith:0},
                   drv:{ramp:MOTO,off:1,dith:0}, eng:{ramp:MOTO,off:0,dith:0} };
    const rindex={};
    Object.values(R).concat([STEEL,GLAS,MOTO,PAINT]).forEach(r=>r.forEach((c,i)=>{ if(!rindex[c]) rindex[c]={r,i}; }));
    const out={ key, id:o.paint?'custom':(SCHEMES[o.scheme]?o.scheme:DEFAULT_SCHEME), name:base.name||'Custom',
                base:P, cap, note:base.note||'', graphic:base.graphic||DF.graphic, ramps:R, mats, rindex };
    _pal[key]=out; _palOrder.push(key);
    if(_palOrder.length>32){ delete _pal[_palOrder.shift()]; }
    return out;
  }

  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- curves -------------------------------------------------------------------------------------
  // monotone cubic through [[x,y],...] (Fritsch-Carlson): smooth sheer and chine, no overshoot
  function mono(pts){
    const n=pts.length, xs=pts.map(p=>p[0]), ys=pts.map(p=>p[1]), d=[], m=new Array(n);
    for(let i=0;i<n-1;i++) d.push((ys[i+1]-ys[i])/(xs[i+1]-xs[i]));
    m[0]=d[0]; m[n-1]=d[n-2];
    for(let i=1;i<n-1;i++) m[i]= d[i-1]*d[i]<=0 ? 0 : (d[i-1]+d[i])/2;
    for(let i=0;i<n-1;i++){
      if(d[i]===0){ m[i]=0; m[i+1]=0; continue; }
      const a=m[i]/d[i], b=m[i+1]/d[i], s=a*a+b*b;
      if(s>9){ const t=3/Math.sqrt(s); m[i]=t*a*d[i]; m[i+1]=t*b*d[i]; }
    }
    return (x)=>{
      if(x<=xs[0]) return ys[0]; if(x>=xs[n-1]) return ys[n-1];
      let i=0; while(i<n-2 && x>xs[i+1]) i++;
      const h=xs[i+1]-xs[i], t=(x-xs[i])/h, t2=t*t, t3=t2*t;
      return (2*t3-3*t2+1)*ys[i]+(t3-2*t2+t)*h*m[i]+(-2*t3+3*t2)*ys[i+1]+(t3-t2)*h*m[i+1];
    };
  }
  const lerp=(a,b,t)=>a+(b-a)*t;
  const smooth=(t)=>{ t=clamp01(t); return t*t*(3-2*t); };
  function grid(u0,u1,n,knots){
    const g=[]; for(let i=0;i<=n;i++) g.push(u0+(u1-u0)*i/n);
    for(const k of (knots||[])) if(k>u0+1e-4 && k<u1-1e-4) g.push(k);
    g.sort((a,b)=>a-b);
    const out=[g[0]];
    for(let i=1;i<g.length;i++) if(g[i]-out[out.length-1]>0.0012) out.push(g[i]);
    if(out[out.length-1]<u1-1e-9) out[out.length-1]=u1;
    return out;
  }

  // ---- the hull -----------------------------------------------------------------------------------
  // [u, sheerHalf, chineHalf, keelHalf, chineRise, depth, keelZ] (m). Deadrise at the transom =
  // atan(0.384/(0.893-0.03)) = 24.0 deg; the vee fines to 50 deg by u 0.92. Beam 2.20 m at u 0.30.
  // Plan taken off the top-down reference: parallel from the transom to u 0.55, then an ogive bow.
  // The sheer climbs 0.42 m to the stem; the keel rockers up from amidships into a raked forefoot.
  const L = 11.90, TH = 0.045, NSEG = 56;
  const T = [
    [0.000, 1.045,0.893,0.030,0.384,1.340,0.000],
    [0.150, 1.083,0.929,0.030,0.400,1.350,0.000],
    [0.300, 1.100,0.937,0.030,0.404,1.365,0.000],
    [0.450, 1.095,0.910,0.030,0.403,1.390,0.005],
    [0.580, 1.064,0.853,0.030,0.412,1.425,0.030],
    [0.690, 1.010,0.762,0.030,0.422,1.450,0.085],
    [0.780, 0.897,0.624,0.030,0.420,1.450,0.170],
    [0.860, 0.721,0.458,0.030,0.386,1.400,0.290],
    [0.920, 0.490,0.290,0.030,0.305,1.320,0.420],
    [0.960, 0.292,0.172,0.025,0.235,1.230,0.530],
    [0.985, 0.154,0.091,0.020,0.195,1.150,0.610],
    [1.000, 0.050,0.035,0.015,0.180,1.100,0.660],
  ];
  const COL=[1,2,3,4,5,6].map(k=>mono(T.map(r=>[r[0],r[k]])));
  function station(u){
    u=clamp01(u);
    return { ws:COL[0](u), ch:COL[1](u), kw:COL[2](u), cd:COL[3](u), dep:COL[4](u), kz:COL[5](u), y:-L/2+u*L };
  }
  const FC = 0.34, FK = 0.62, KB = 0.03, KB_MAX = 0.04;
  const RAKE = 1.0, RAKE_U = 0.78;   // the forefoot tucks a metre aft of the stem head: ~42 deg of rake
  const rakeAt=(u,frac)=>-RAKE*Math.pow(Math.max(0,(u-RAKE_U)/(1-RAKE_U)),1.6)*(1-Math.min(1,frac));
  function skin(side,u,frac,inset){
    const st=station(u), t=inset?1:0;
    const ws=st.ws-(t?TH:0), chh=st.ch-(t?TH:0), kw=Math.max(0.004, st.kw-(t?TH*0.5:0));
    const kz=st.kz+(t?TH*0.7:0), cz=st.kz+st.cd+(t?TH*0.3:0), sz=st.kz+st.dep-(t?0.012:0);
    let x,z;
    if(frac<=FC){ const q=FC>0?frac/FC:0; x=lerp(kw,chh,q); z=lerp(kz,cz,q); }
    else {
      const K=(FK-FC)/(1-FC);
      const knuck=Math.min(KB_MAX, KB*Math.max(0,(st.ws/Math.max(1e-6,st.ch))-1));
      const xk=lerp(chh,ws,K)+knuck, zk=lerp(cz,sz,K);
      if(frac<=FK){ const q=(frac-FC)/(FK-FC); x=lerp(chh,xk,q); z=lerp(cz,zk,q); }
      else { const q=(frac-FK)/(1-FK); x=lerp(xk,ws,q); z=lerp(zk,sz,q); }
    }
    return [ side*x, st.y+rakeAt(u,(z-st.kz)/Math.max(1e-6,st.dep)), z ];
  }
  function fracAtZ(st,z){
    const cz=st.kz+st.cd, sz=st.kz+st.dep;
    if(z<=cz) return FC*Math.max(0.02,Math.min(1,(z-st.kz)/Math.max(1e-6,st.cd)));
    return FC+(1-FC)*Math.max(0,Math.min(1,(z-cz)/Math.max(1e-6,sz-cz)));
  }
  const halfAtZ=(u,z,inset)=>skin(1,u,fracAtZ(station(u),z),inset)[0];
  const sheerZ=(u)=>{ const st=station(u); return st.kz+st.dep; };
  const uOf=(y)=>(y+L/2)/L, yOf=(u)=>-L/2+u*L;
  const QK=(FK-FC)/(1-FC);   // the topside crease in q

  // ---- deck, cockpit, hatch plan ---------------------------------------------------------------------
  const RUBIN = TH*0.76;
  const xOut=(u)=>skin(1,u,1)[0];
  const xRail=(u)=>Math.max(0.012, xOut(u)-RUBIN);
  const capZ=(u)=>sheerZ(u)+0.010;
  const CROWN = mono([[0,0.07],[0.20,0.07],[0.30,0.12],[0.40,0.20],[0.52,0.20],[0.72,0.13],[0.90,0.06],[1,0.02]]);
  const deckZ=(u,s)=>capZ(u)+CROWN(u)*(1-s*s);
  const SOLE = 0.62;
  // cockpit from u 0.20 to 0.40, as on the reference: 2.2 m hatch, 6.9 m of foredeck ahead of the screen
  const Y_H0 = -5.78, Y_C0 = -3.57, Y_F0 = -1.90, Y_FC = -1.30;
  const U_H0 = uOf(Y_H0), U_C0 = uOf(Y_C0), U_F0 = uOf(Y_F0), U_FC = uOf(Y_FC);
  const SIDE = 0.30, DASH_D = 0.26, Z_DASH = 1.24, FRONT_N = 3.2;
  const xo=(u)=>Math.max(0.05, xRail(u)-SIDE);            // cockpit and hatch half-width, straight sides
  const XF = xo(U_F0);
  // the cockpit front is a rounded superellipse from (XF, Y_F0) round to (0, Y_FC): the screen wraps it
  const yFront=(x)=>{ const a=Math.min(1,Math.abs(x)/XF); return Y_F0+(Y_FC-Y_F0)*Math.pow(Math.max(0,1-Math.pow(a,FRONT_N)),1/FRONT_N); };
  const xFront=(y)=>{ if(y<=Y_F0) return XF; if(y>=Y_FC) return 0; const r=(y-Y_F0)/(Y_FC-Y_F0); return XF*Math.pow(Math.max(0,1-Math.pow(r,FRONT_N)),1/FRONT_N); };
  const xc=(u)=>{ const y=yOf(u); if(y<Y_H0-1e-9||y>Y_FC) return 0; if(y<=Y_F0) return xo(u); return Math.min(xo(u), xFront(y)); };
  const FRONT_KNOTS=[0.22,0.43,0.60,0.73,0.83,0.90,0.95,0.975,0.99].map(r=>uOf(Y_F0+r*(Y_FC-Y_F0)));
  const xl=(u,z)=>Math.min(xo(u), halfAtZ(u,z,1));
  const SEAT_Z = SOLE+0.56, ROW1 = -2.20, REAR = -3.12, FRONT_X = 0.42, REAR_X = 0.49;

  // ---- generic solids (fleet helpers) ---------------------------------------------------------------
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
  function prism(c,rad,h,n,mat,b,db){
    const top=[], bot=[], out=[];
    for(let k=0;k<n;k++){ const a=2*Math.PI*k/n+Math.PI/n;
      top.push([c[0]+rad*Math.cos(a), c[1]+rad*Math.sin(a), c[2]+h]);
      bot.push([c[0]+rad*Math.cos(a), c[1]+rad*Math.sin(a), c[2]]); }
    out.push({v:top,mat,b:b||0,db:db||0});
    for(let k=0;k<n;k++){ const k2=(k+1)%n; out.push({v:[top[k],top[k2],bot[k2],bot[k]],mat,b:b||0,db:db||0}); }
    return out;
  }
  function nrm(v){ let nx=0,ny=0,nz=0;
    for(let i=0;i<v.length;i++){ const a=v[i], b=v[(i+1)%v.length];
      nx+=(a[1]-b[1])*(a[2]+b[2]); ny+=(a[2]-b[2])*(a[0]+b[0]); nz+=(a[0]-b[0])*(a[1]+b[1]); }
    return [nx,ny,nz]; }
  function faceO(out, v, mat, b, db, want){
    const n=nrm(v); if(n[0]*want[0]+n[1]*want[1]+n[2]*want[2]<0) v=v.slice().reverse();
    out.push({v, mat, b:b||0, db:db||0});
  }
  function slab(c,h,mat,bTop,bSide,xf){
    xf=xf||ID;
    const P=(sx,sy,sz)=>xf([c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]]);
    const f=(v,b)=>({v,mat,b,db:0});
    return [ f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)],bTop),
             f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)],bSide),
             f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)],bSide),
             f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)],bSide),
             f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)],bSide) ];
  }

  // ---- livery charts ------------------------------------------------------------------------------
  // split one strip [ua,ub] of a charted surface at every band edge and crease; the last band wins.
  // A band's material is read where the interval is widest, so a blade tip ending mid-strip keeps it.
  function paintStrip(ua, ub, P, lo, hi, knots, layers, base, bOf, emit){
    const um=(ua+ub)/2;
    const lA=lo(ua), lM=lo(um), lB=lo(ub), hA=hi(ua), hM=hi(um), hB=hi(ub);
    if(hA-lA<1e-4 && hB-lB<1e-4 && hM-lM<1e-4) return;
    const cl=(t,l,h)=>t<l?l:t>h?h:t, E=[[lA,lM,lB],[hA,hM,hB]];
    for(const k of knots) if(k>lM+1e-4 && k<hM-1e-4) E.push([cl(k,lA,hA),k,cl(k,lB,hB)]);
    const act=[];
    for(const Ly of layers){
      const a0=cl(Ly.a(ua),lA,hA), am=cl(Ly.a(um),lM,hM), a1=cl(Ly.a(ub),lB,hB);
      const b0=cl(Ly.b(ua),lA,hA), bm=cl(Ly.b(um),lM,hM), b1=cl(Ly.b(ub),lB,hB);
      if(Math.max(b0-a0,bm-am,b1-a1)<=1e-4) continue;
      act.push(Ly); E.push([a0,am,a1],[b0,bm,b1]);
    }
    E.sort((p,q)=>(p[1]-q[1])||((p[0]+p[2])-(q[0]+q[2])));
    for(let i=0;i+1<E.length;i++){
      const e0=E[i], e1=E[i+1], wa=e1[0]-e0[0], wm=e1[1]-e0[1], wb=e1[2]-e0[2];
      if(Math.max(wa,wm,wb)<1e-4) continue;
      let uu=um, tt=(e0[1]+e1[1])/2;
      if(wa>wm && wa>=wb){ uu=ua; tt=(e0[0]+e1[0])/2; } else if(wb>wm && wb>wa){ uu=ub; tt=(e0[2]+e1[2])/2; }
      let mat=base;
      for(const Ly of act) if(tt>=Ly.a(uu) && tt<=Ly.b(uu)) mat=Ly.mat;
      emit(P(ua,e0[0]),P(ub,e0[2]),P(ub,e1[2]),P(ua,e1[0]),mat,bOf(mat));
    }
  }
  function bandsAt(u, layers, knots, base, lo, hi){
    lo=lo==null?0:lo; hi=hi==null?1:hi;
    const E=[lo,hi]; for(const k of knots||[]) if(k>lo&&k<hi) E.push(k);
    const act=[];
    for(const Ly of layers){ const a=Math.max(lo,Math.min(hi,Ly.a(u))), b=Math.max(lo,Math.min(hi,Ly.b(u))); if(b-a>1e-4){ act.push(Ly); E.push(a,b); } }
    E.sort((p,q)=>p-q);
    const out=[];
    for(let i=0;i+1<E.length;i++){
      if(E[i+1]-E[i]<1e-4) continue;
      const t=(E[i]+E[i+1])/2; let mat=base;
      for(const Ly of act) if(t>=Ly.a(u)&&t<=Ly.b(u)) mat=Ly.mat;
      out.push([E[i],E[i+1],mat]);
    }
    return out;
  }
  const matAt=(layers,u,t,base)=>{ let m=base; for(const Ly of layers) if(t>=Ly.a(u)&&t<=Ly.b(u)) m=Ly.mat; return m; };

  // band builders. env: 0 outside [u0,u1], ramps in over tin and out over tout (p<1 = fuller tips)
  const env=(u,u0,u1,tin,tout,p)=>{ if(u<=u0||u>=u1) return 0; const e=Math.min(1,(u-u0)/Math.max(1e-6,tin),(u1-u)/Math.max(1e-6,tout)); return p?Math.pow(e,p):e; };
  const K=(v)=>()=>v;
  const blade=(c,hw,u0,u1,tin,tout,mat,p)=>({ mat, a:(u)=>c(u)-hw(u)*env(u,u0,u1,tin,tout,p), b:(u)=>c(u)+hw(u)*env(u,u0,u1,tin,tout,p), knots:[u0,u0+tin,u1-tout,u1] });
  const above=(edge,mat)=>({ mat, a:edge, b:K(1.05) });
  const inHatch=(u)=>u>U_H0+0.004 && u<U_C0-0.004;
  const hatchBand=(s0,s1,mat)=>({ mat, a:(u)=>inHatch(u)?s0:-1, b:(u)=>inHatch(u)?s1:-1 });

  const GRAPHICS = {
    // black hull, yellow deck poured down the aft quarters, a yellow pin under it, a grey blade low;
    // a black spear up the foredeck with a pin each side, black hatch panel
    sting: (()=>{
      const Wq=mono([[0,0.60],[0.22,0.56],[0.45,0.60],[0.62,0.72],[0.76,0.90],[0.86,1.04]]);
      const spear=(u)=>0.34*env(u,U_FC-0.03,0.93,0.001,0.50,0.9);
      return { name:'Sting',
        hull:[ above(Wq,'g1'),
               blade((u)=>Wq(u)-0.125,K(0.045),0.012,0.84,0.02,0.12,'g1'),
               blade(mono([[0.04,0.24],[0.30,0.26],[0.55,0.34],[0.68,0.46]]),K(0.058),0.035,0.68,0.025,0.22,'g2',0.8) ],
        deck:[ { mat:'g3', a:K(-1), b:spear, knots:[U_FC-0.03,0.43,0.93] },
               blade((u)=>spear(u)+0.085,K(0.038),U_FC-0.02,0.88,0.004,0.25,'g3'),
               hatchBand(-1,0.46,'g3') ] };
    })(),
    // red over silver on a raked split with a black pin, a white speed line in the red; twin white
    // stripes with black pins run transom to stem over the hatch
    split: (()=>{
      const Cq=mono([[0,0.30],[0.25,0.33],[0.50,0.45],[0.68,0.66],[0.82,0.90],[0.90,1.05]]);
      return { name:'Split',
        hull:[ above(Cq,'g1'),
               blade((u)=>Cq(u)-0.075,K(0.042),-0.01,0.89,0.001,0.10,'g2'),
               blade(mono([[0.06,0.80],[0.40,0.83],[0.64,0.88]]),K(0.03),0.06,0.66,0.06,0.16,'g3',0.8) ],
        deck:[ { mat:'g3', a:K(0.075), b:(u)=>u<0.975?0.175:0.075 },
               { mat:'g2', a:K(0.175), b:(u)=>u<0.90?0.225:0.175 } ] };
    })(),
    // three stacked stripes from the transom, climbing and peeling off the sheer toward the bow; a teal
    // dart on the foredeck, a teal centre stripe and coral pins on the hatch
    stack: (()=>{
      const Bq=mono([[0,0.34],[0.35,0.37],[0.60,0.47],[0.78,0.66],[0.92,0.97]]);
      const dart=(u)=>0.30*env(u,U_FC-0.03,0.80,0.001,0.38,0.9);
      return { name:'Stack',
        hull:[ blade((u)=>Bq(u)+0.085,K(0.085),-0.01,0.985,0.001,0.06,'g1'),
               blade((u)=>Bq(u)+0.25,K(0.042),-0.01,0.985,0.001,0.06,'g1'),
               blade((u)=>Bq(u)+0.35,K(0.04),-0.01,0.985,0.001,0.06,'g2') ],
        deck:[ { mat:'g1', a:K(-1), b:dart, knots:[U_FC-0.03,0.42,0.80] },
               blade((u)=>dart(u)+0.08,K(0.036),U_FC-0.02,0.78,0.004,0.22,'g2'),
               hatchBand(-1,0.085,'g1'), hatchBand(0.13,0.165,'g2') ] };
    })(),
    // three forward-raked claws across the bow topsides, an orange pin low aft; three chevrons up the
    // foredeck pointing at the stem, orange hump on the hatch
    claw: (()=>{
      const claws=[0.60,0.655,0.71].map((uk,i)=>{ const m=0.085-0.008*i, h=0.13;
        const c=(u)=>(u-uk)/m, w=(u)=>{ const t=c(u); return t<=0||t>=1?0:h*Math.pow(Math.sin(Math.PI*t),0.8); };
        return { mat:'g1', a:(u)=>c(u)-w(u), b:(u)=>c(u)+w(u), knots:[0,0.125,0.25,0.375,0.5,0.625,0.75,0.875,1].map(k=>uk+m*k) }; });
      const chev=[0.66,0.72,0.78].map((vk)=>{ const m=0.10, h=0.10;
        const c=(u)=>1-(u-vk)/m, w=(u)=>{ const t=1-c(u); return t<=0||t>=1?0:h*Math.pow(Math.sin(Math.PI*t),0.7); };
        return { mat:'g1', a:(u)=>c(u)-w(u), b:(u)=>c(u)+w(u), knots:[0,0.125,0.25,0.375,0.5,0.625,0.75,0.875,1].map(k=>vk+m*k) }; });
      return { name:'Claw',
        hull: claws.concat([ blade(K(0.15),K(0.045),-0.01,0.56,0.001,0.08,'g1') ]),
        deck: chev.concat([ hatchBand(-1,0.30,'g1') ]) };
    })(),
    // the black deck runs over the sheer as a band and hangs in drips of uneven length; two green pins
    // up the foredeck, green hump on the hatch
    drip: (()=>{
      const D=[[0.05,0.30,0.016],[0.11,0.16,0.012],[0.17,0.44,0.018],[0.245,0.24,0.014],[0.31,0.36,0.016],[0.385,0.14,0.012],
               [0.45,0.50,0.02],[0.53,0.26,0.014],[0.60,0.40,0.017],[0.67,0.18,0.012],[0.735,0.33,0.016],[0.80,0.20,0.013],[0.86,0.12,0.011]];
      const drop=(u)=>{ let d=0; for(const [uk,len,wk] of D){ const t=(u-uk)/wk; if(t>-1&&t<1) d=Math.max(d,len*Math.pow(1-t*t,0.55)); } return d; };
      const knots=[]; for(const [uk,,wk] of D) for(const k of [-1,-0.8,-0.55,-0.3,0,0.3,0.55,0.8,1]) knots.push(uk+k*wk);
      return { name:'Drip',
        hull:[ { mat:'g1', a:(u)=>0.88-drop(u), b:K(1.05), knots } ],
        deck:[ blade(K(0.215),K(0.036),U_FC-0.03,0.90,0.004,0.20,'g2'), hatchBand(-1,0.30,'g2') ] };
    })(),
  };
  const graphicIds = Object.keys(GRAPHICS);
  function graphicOf(o){
    o=o||{};
    const id = o.graphic || (o.paint && o.paint.graphic) || (SCHEMES[o.scheme] && SCHEMES[o.scheme].graphic) || SCHEMES[DEFAULT_SCHEME].graphic;
    return GRAPHICS[id] ? id : SCHEMES[DEFAULT_SCHEME].graphic;
  }
  const knotsOf=(layers)=>{ const k=[]; for(const Ly of layers) for(const v of (Ly.knots||[])) k.push(v); return k; };
  const DECK_B = -0.5, HULL_B = 0.8, DECK_SK = [0.2,0.4,0.6,0.8];

  // painted faces for one graphic: topsides, transom and stem bands, the deck chart
  const _liv = {};
  function liveryFaces(gid){
    if(_liv[gid]) return _liv[gid];
    const G=GRAPHICS[gid], out=[], zero=K(0), one=K(1);
    const f=(v,mat,b,db)=>out.push({v,mat,b:b||0,db:db||0});
    const hullU=grid(0,1,NSEG,knotsOf(G.hull));
    for(const side of [-1,1]){
      const P=(u,q)=>skin(side,u,FC+(1-FC)*q);
      for(let i=0;i+1<hullU.length;i++)
        paintStrip(hullU[i],hullU[i+1],P,zero,one,[QK],G.hull,'hull',()=>HULL_B,(A,B,C,D,mat,b)=>f([A,B,C,D],mat,b,0));
    }
    // transom: the topside bands carried straight across, then the rim up to the crowned deck edge
    const tp=(s,q)=>skin(s,0,FC+(1-FC)*q);
    const tb=bandsAt(0,G.hull,[QK],'hull');
    for(const [q0,q1,mat] of tb) f([tp(-1,q0),tp(1,q0),tp(1,q1),tp(-1,q1)],mat,HULL_B-0.55,0.005);
    f([skin(-1,0,0),skin(1,0,0),skin(1,0,FC),skin(-1,0,FC)],'bottom',-1.5,0.005);
    (function(){
      const y=-L/2, zs=sheerZ(0), xr=xRail(0), N=8, rim=tb.length?tb[tb.length-1][2]:'hull';
      for(let k=0;k<N;k++){
        const sa=-1+2*k/N, sb=-1+2*(k+1)/N;
        faceO(out,[[sa*xr,y,zs],[sb*xr,y,zs],[sb*xr,y,deckZ(0,Math.abs(sb))],[sa*xr,y,deckZ(0,Math.abs(sa))]],rim,HULL_B-0.55,0.005,[0,-1,0]);
      }
    })();
    // stem: close the last station across the centreline
    (function(){
      const sb=bandsAt(1,G.hull,[QK],'hull'), sp=(s,q)=>skin(s,1,FC+(1-FC)*q);
      for(const [q0,q1,mat] of sb) faceO(out,[sp(-1,q0),sp(1,q0),sp(1,q1),sp(-1,q1)],mat,HULL_B,0,[0,1,0.2]);
      faceO(out,[skin(-1,1,0),skin(1,1,0),skin(1,1,FC),skin(-1,1,FC)],'bottom',-0.3,0,[0,1,-0.3]);
    })();
    const _deck0=out.length;
    // deck chart: aft strip, side decks along the hatch and cockpit, the foredeck
    const deckU=grid(0,1,70,[U_H0,U_C0,U_F0,U_FC].concat(FRONT_KNOTS,knotsOf(G.deck)));
    for(const side of [-1,1]){
      const P=(u,s)=>[side*s*xRail(u), yOf(u), deckZ(u,s)];
      const emit=(A,B,C,D,mat,b)=>faceO(out,[A,B,C,D],mat,b,0,[0,0,1]);
      for(let i=0;i+1<deckU.length;i++){
        const ua=deckU[i], ub=deckU[i+1], um=(ua+ub)/2;
        const lo = (um<U_H0 || um>U_FC) ? zero : (u)=>Math.min(1, xc(u)/xRail(u));
        paintStrip(ua,ub,P,lo,one,DECK_SK,G.deck,'deck',()=>DECK_B,emit);
      }
    }
    gfTagLivery(out, _deck0);
    _liv[gid]=out;
    return out;
  }

  // ---- STATIC faces: everything that neither moves nor carries the graphic --------------------------
  const F = [];
  const face=(v,mat,b,db)=>F.push({v,mat:mat||'deck',b:b||0,db:db||0});
  const fo=(v,mat,b,db,want)=>faceO(F,v,mat,b,db,want);
  const boxF=(c,h,mat,b,db,xf)=>{ F.push.apply(F, box(c,h,mat,b,db,xf)); };
  const tubeF=(A,B2,rad,mat,b)=>{ F.push.apply(F, tube(A,B2,rad,mat,b)); };
  const prismF=(c,rad,h,n,mat,b,db)=>{ F.push.apply(F, prism(c,rad,h,n,mat,b,db)); };
  // ---- the new layout's tags (ADR 0048 §8 A) ---------------------------------------------------------
  // Every face carries lv (its level), ro (its role) and it (true only on the room's faces). build()
  // marks each of its sections with TAG(level, role, byMat): every face the section pushes takes that
  // level and role, and byMat maps a material to another role (or to 'level/role') inside the same
  // section. The parts that move are stamped where they are assembled. The ids are v3's, unchanged.
  const LV = { hull:0, main_deck:1, house:2, bridge:3, below:4, rigging:5, foredeck:6, top:7 };
  const RO = { shell:0, wall:1, roof:2, lining:3, part:4, floor:5, fit:6, tall:7, trim:8, rig:9 };
  const GHOST_ROLES = ['shell','wall','roof','lining','part','tall','trim'];
  function stampTags(arr, from, to, lv, ro, byMat, it){
    for(let i=from;i<to;i++){
      const f=arr[i], m=(byMat&&byMat[f.mat])||ro, k=m.indexOf('/');
      const l=k<0?lv:m.slice(0,k), r=k<0?m:m.slice(k+1);
      if(!(l in LV) || !(r in RO)) throw new Error('stampTags: unknown tag '+l+'/'+r);
      f.lv=LV[l]; f.ro=RO[r]; f.it=!!it;
    }
    return arr;
  }
  let _tagAt=0, _tagCur=null;
  function TAG(lv, ro, byMat){
    if(_tagCur) stampTags(F, _tagAt, F.length, _tagCur[0], _tagCur[1], _tagCur[2], false);
    _tagAt=F.length; _tagCur=lv ? [lv, ro, byMat] : null;
  }
  // the new layout's fixed points, read by build() below and by the hull's block at the end
  //   GF_HUB    the wheel's hub, rake (negative: top toward the helmsman), the column's length along the axis, and
  //             the helm pod it stands in (its top podUp over the column's foot, podW either side of the hub)
  //   GF_DOORX  the cuddy door's opening in the console face, the room's aft bulkhead behind it, and the leaf's
  //             travel: push back off its seat, then slide to port
  const GF_HUB = { x:FRONT_X, y:-1.78, z:1.40, rake:-20, col:0.10, podUp:0.06, podW:0.075 };
  const GF_DOORX = { x0:-0.20, x1:0.20, sillZ:SOLE+0.04, headZ:Z_DASH-0.07, yBulk:-1.50, push:0.036, travel:0.40 };
  const gfLeafY=(x)=>yFront(Math.abs(x))-DASH_D-0.006;   // the 10-03 panes' plane
  const B_BOT=(u)=>-0.95+0.55*Math.pow(Math.max(0,(u-0.74)/0.26),1.3);

  const DRIVE = { xs:[-0.42,0.42], yG:-L/2-0.02, zT:0.56, k:1.3, steerMax:26, trimMin:-4, trimMax:10, tiltMax:40 };
  const WS = { off:0.035, yEnd:Y_F0-0.64, ht:0.26, rake:0.26, NC:9, NW:4 };

  (function build(){
    TAG('hull','shell',{steel:'trim',blk:'trim'});
    // ---- bottom, spray rail, rub rail, keel pad (uniform stations) ------------------------------
    const hullU=grid(0,1,NSEG,[]);
    const srt=(u)=>0.055*(1-Math.pow(clamp01((u-0.80)/0.19),1.3));
    for(const side of [-1,1]){
      for(let i=0;i+1<hullU.length;i++){
        const u0=hullU[i], u1=hullU[i+1];
        face([skin(side,u0,0),skin(side,u1,0),skin(side,u1,FC),skin(side,u0,FC)],'bottom',B_BOT(u0));
        const ca=skin(side,u0,FC), cb=skin(side,u1,FC);
        const oc0=[ca[0]+side*srt(u0),ca[1],ca[2]+0.012], oc1=[cb[0]+side*srt(u1),cb[1],cb[2]+0.012];
        const dnp=(p)=>[p[0],p[1],p[2]-0.05];
        face([ca,cb,oc1,oc0],'hull',HULL_B+0.4,0.03);
        face([oc0,oc1,dnp(oc1),dnp(oc0)],'bottom',-1.25,0.03);
        const oa=skin(side,u0,1), ob2=skin(side,u1,1);
        const inb=(p)=>[p[0]-side*RUBIN,p[1],p[2]];
        const or0=[oa[0],oa[1],oa[2]+0.002], or1=[ob2[0],ob2[1],ob2[2]+0.002];
        face([or0,or1,inb(ob2),inb(oa)],'steel',0.6,0.04);
        face([or0,or1,[or1[0],or1[1],or1[2]-0.075],[or0[0],or0[1],or0[2]-0.075]],'blk',0.15,0.04);
      }
    }
    for(let i=0;i+1<hullU.length;i++){
      const u0=hullU[i], u1=hullU[i+1], kb=-1.0+1.0*Math.pow(Math.max(0,(u0-0.72)/0.28),1.2);
      face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'bottom',kb);
    }

    TAG('hull','fit');
    // ---- transom hardware: gimbal plates, exhaust tips, eyes, the swim grate ---------------------
    for(const xs of DRIVE.xs) boxF([xs,-L/2-0.02,DRIVE.zT-0.03],[0.17,0.02,0.15],'drv',-0.1,0.01);
    for(const ex of [-0.70,-0.16,0.16,0.70]){
      boxF([ex,-L/2-0.03,0.74],[0.075,0.03,0.06],'steel',0.5,0.01);
      fo([[ex-0.045,-L/2-0.062,0.70],[ex+0.045,-L/2-0.062,0.70],[ex+0.045,-L/2-0.062,0.78],[ex-0.045,-L/2-0.062,0.78]],'blk',-0.6,0.012,[0,-1,0]);
    }
    for(const s of [-1,1]) boxF([s*0.85,-L/2-0.012,1.12],[0.030,0.012,0.022],'steel',0.3,0.02);
    TAG('main_deck','floor');   // the swim grate
    (function(){
      const y0=-L/2-0.01, y1=-L/2-0.46, z=0.97, t=0.035, hw=0.92, N=13;
      for(let k=0;k<N;k++){
        const xa=-hw+2*hw*k/N, xb=-hw+2*hw*(k+1)/N, bar=k%2===0;
        fo([[xa,y0,z],[xb,y0,z],[xb,y1,z],[xa,y1,z]], bar?'steel':'blk', bar?0.55:0.1, 0, [0,0,1]);
      }
      fo([[-hw,y0,z-t],[hw,y0,z-t],[hw,y1,z-t],[-hw,y1,z-t]],'blk',-1.2,0,[0,0,-1]);
      boxF([0,y1+0.02,z-t/2],[hw+0.02,0.02,t/2],'steel',0.35);
      for(const s of [-1,1]) boxF([s*(hw+0.01),(y0+y1)/2,z-t/2],[0.02,(y0-y1)/2,t/2],'steel',0.3);
      for(const s of [-1,1]) tubeF([s*0.80,-L/2,0.80],[s*0.80,y1+0.10,z-0.03],0.022,'steel',0.25);
    })();

    TAG('hull','fit');
    // ---- stern cleats on the side decks, bow cleat, bow eye ---------------------------------------
    for(const s of [-1,1]){ const u=uOf(-5.55), x=xRail(u)-0.12; boxF([s*x,yOf(u),deckZ(u,x/xRail(u))+0.014],[0.022,0.065,0.012],'steel',0.5,-0.02); }
    boxF([0,yOf(0.955),deckZ(0.955,0)+0.016],[0.022,0.07,0.014],'steel',0.55,-0.03);
    boxF([0,5.22,0.92],[0.024,0.035,0.02],'steel',0.3,-0.03);

    TAG('foredeck','fit');
    // ---- foredeck hatch: smoked acrylic in a black frame, split at the crown ----------------------
    (function(){
      const y0=0.90, y1=1.40, hw=0.25, fr=0.04;
      const pt=(x,y,dz)=>{ const u=uOf(y); return [x,y,deckZ(u,Math.abs(x)/xRail(u))+dz]; };
      for(const s of [-1,1]){
        fo([pt(0,y0-fr,0.008),pt(s*(hw+fr),y0-fr,0.008),pt(s*(hw+fr),y1+fr,0.008),pt(0,y1+fr,0.008)],'blk',0.3,0.02,[0,0,1]);
        fo([pt(0,y0,0.014),pt(s*hw,y0,0.014),pt(s*hw,y1,0.014),pt(0,y1,0.014)],'glas',-0.9,0.03,[0,0,1]);
      }
    })();

    TAG('main_deck','wall',{uph:'fit'});
    // ---- cockpit: side liners, coaming pads, aft bulkhead, sole -----------------------------------
    const NI=24;
    for(const side of [-1,1]){
      for(let i=0;i<NI;i++){
        const u0=lerp(U_C0,U_F0,i/NI), u1=lerp(U_C0,U_F0,(i+1)/NI);
        const x0=xo(u0), x1=xo(u1), zt0=deckZ(u0,x0/xRail(u0)), zt1=deckZ(u1,x1/xRail(u1));
        fo([[side*x0,yOf(u0),zt0],[side*x1,yOf(u1),zt1],[side*x1,yOf(u1),SOLE],[side*x0,yOf(u0),SOLE]],'liner',-0.8,0,[-side,0,0.15]);
        if(i>0 && i<NI-1){
          const p0=x0-0.035, p1=x1-0.035;
          fo([[side*p0,yOf(u0),zt0-0.04],[side*p1,yOf(u1),zt1-0.04],[side*p1,yOf(u1),zt1-0.24],[side*p0,yOf(u0),zt0-0.24]],'uph',-0.1,0.004,[-side,0,0.2]);
          fo([[side*x0,yOf(u0),zt0-0.04],[side*x1,yOf(u1),zt1-0.04],[side*p1,yOf(u1),zt1-0.04],[side*p0,yOf(u0),zt0-0.04]],'uph',-1.6,0.004,[0,0,1]);
        }
      }
    }
    (function(){   // aft bulkhead, cockpit side
      const u=U_C0, xr=xRail(u), xw=xo(u), N=8;
      for(let k=0;k<N;k++){
        const xa=-xw+2*xw*k/N, xb=-xw+2*xw*(k+1)/N;
        fo([[xa,Y_C0,SOLE],[xb,Y_C0,SOLE],[xb,Y_C0,deckZ(u,Math.abs(xb)/xr)],[xa,Y_C0,deckZ(u,Math.abs(xa)/xr)]],'liner',-0.3,0,[0,1,0]);
      }
    })();
    TAG('main_deck','floor');
    (function(){   // sole, to under the dash
      const N=16, yA=Y_C0, yB=yFront(0)-DASH_D+0.02;
      for(let i=0;i<N;i++){
        const y0=lerp(yA,yB,i/N), y1=lerp(yA,yB,(i+1)/N), w0=xl(uOf(y0),SOLE)*0.99, w1=xl(uOf(y1),SOLE)*0.99;
        fo([[-w0,y0,SOLE],[w0,y0,SOLE],[w1,y1,SOLE],[-w1,y1,SOLE]],'sole',-2.9,0,[0,0,1]);
      }
    })();

    // ---- dash: black anti-glare panel hanging from the rounded cockpit front, console face below --
    (function(){
      const N=14, top=(x)=>{ const y=yFront(x), u=uOf(y); return [x,y,deckZ(u,Math.min(1,Math.abs(x)/xRail(u)))]; };
      const bot=(x)=>[x,yFront(x)-DASH_D,Z_DASH];
      for(let k=0;k<N;k++){
        const xa=-XF+2*XF*k/N, xb=-XF+2*XF*(k+1)/N;
        TAG('foredeck','roof');
        fo([bot(xa),bot(xb),top(xb),top(xa)],'blk',0.25,0,[0,-1,0.8]);
        TAG('main_deck','wall');
        const fa=[xa,bot(xa)[1],SOLE], fb=[xb,bot(xb)[1],SOLE], D=GF_DOORX;
        if(xb<=D.x0 || xa>=D.x1){ fo([fa,fb,bot(xb),bot(xa)],'liner',-0.25,0,[0,-1,0]); continue; }
        // a strip the doorway cuts: full height either side of it, the sill below it, the head above it
        const yAt=(x)=>lerp(bot(xa)[1],bot(xb)[1],(x-xa)/(xb-xa)), P=(x,z)=>[x,yAt(x),z], c0=Math.max(xa,D.x0), c1=Math.min(xb,D.x1);
        if(c0>xa+1e-6) fo([P(xa,SOLE),P(c0,SOLE),P(c0,Z_DASH),P(xa,Z_DASH)],'liner',-0.25,0,[0,-1,0]);
        if(xb>c1+1e-6) fo([P(c1,SOLE),P(xb,SOLE),P(xb,Z_DASH),P(c1,Z_DASH)],'liner',-0.25,0,[0,-1,0]);
        fo([P(c0,SOLE),P(c1,SOLE),P(c1,D.sillZ),P(c0,D.sillZ)],'liner',-0.25,0,[0,-1,0]);
        fo([P(c0,D.headZ),P(c1,D.headZ),P(c1,Z_DASH),P(c0,Z_DASH)],'liner',-0.25,0,[0,-1,0]);
      }
      // the cuddy door (the new layout): the 10-03 panes are the leaf now, doorFaces(t). The opening through the
      // console face is lined back to the cuddy's aft bulkhead (GF_DOORX.yBulk): jambs, sill, head. The leaf backs
      // off its seat and slides to port into the pocket behind the console face, through a slot in the port jamb.
      (function(){
        const D=GF_DOORX, cY=(x)=>{ const k=Math.min(N-1,Math.max(0,Math.floor((x+XF)/(2*XF)*N))), xa=-XF+2*XF*k/N, xb=-XF+2*XF*(k+1)/N;
          return lerp(bot(xa)[1],bot(xb)[1],(x-xa)/(xb-xa)); };
        const ys=(gfLeafY(0)+gfLeafY(0.20))/2+D.push, slot=[ys-0.008, ys+0.008];
        const xs=[D.x0]; for(let k=0;k<=N;k++){ const x=-XF+2*XF*k/N; if(x>D.x0+1e-6 && x<D.x1-1e-6) xs.push(x); } xs.push(D.x1);
        fo([[D.x1,cY(D.x1),D.sillZ],[D.x1,D.yBulk,D.sillZ],[D.x1,D.yBulk,D.headZ],[D.x1,cY(D.x1),D.headZ]],'liner',-0.35,0,[-1,0,0]);
        for(const [ya,yb] of [[cY(D.x0),slot[0]],[slot[1],D.yBulk]])
          fo([[D.x0,ya,D.sillZ],[D.x0,yb,D.sillZ],[D.x0,yb,D.headZ],[D.x0,ya,D.headZ]],'liner',-0.15,0,[1,0,0]);
        fo(xs.map(x=>[x,cY(x),D.sillZ]).concat([[D.x1,D.yBulk,D.sillZ],[D.x0,D.yBulk,D.sillZ]]),'liner',-1.2,0,[0,0,1]);
        fo(xs.map(x=>[x,cY(x),D.headZ]).concat([[D.x1,D.yBulk,D.headZ],[D.x0,D.yBulk,D.headZ]]),'liner',-0.4,0,[0,0,-1]);
        TAG('foredeck','fit');   // the doorway's dark back: drawn while the room is not, lifted with the lid when it is
        fo([[D.x0,D.yBulk-0.002,D.sillZ],[D.x1,D.yBulk-0.002,D.sillZ],[D.x1,D.yBulk-0.002,D.headZ],[D.x0,D.yBulk-0.002,D.headZ]],'blk',-0.8,0,[0,-1,0]);
      })();
      TAG('foredeck','fit');
      // gauge lenses in front of both front-row seats
      const on=(x,t,dz)=>{ const a=bot(x), b=top(x); return [x, lerp(a[1],b[1],t)-0.006, lerp(a[2],b[2],t)+dz]; };
      for(const [x0,x1] of [[0.22,0.62],[-0.62,-0.22]])
        fo([on(x0,0.22,0.006),on(x1,0.22,0.006),on(x1,0.55,0.006),on(x0,0.55,0.006)],'glas',0.9,0.01,[0,-1,0.8]);
      // the helm pod (the new layout): the wheel stands 0.30 m higher and leans its top toward the helmsman, so its
      // column needs a seat on the dash: a black wedge on the panel, its aft face square to the column
      (function(){
        const G=GF_HUB, r=G.rake*DEG, n=[0,-Math.cos(r),Math.sin(r)], e2=[0,Math.sin(r),Math.cos(r)];
        const C=[G.x-n[0]*G.col, G.y-n[1]*G.col, G.z-n[2]*G.col];
        const A=[C[1]+e2[1]*G.podUp, C[2]+e2[2]*G.podUp], hD=(C[2]-Z_DASH)/e2[2], B=[C[1]-e2[1]*hD, Z_DASH];
        const at=(x)=>{ const b=bot(x), t=top(x), k=(A[1]-Z_DASH)/(t[2]-Z_DASH); return { A:[x,A[0],A[1]], B:[x,B[0],B[1]], D:b, E:[x,lerp(b[1],t[1],k),A[1]] }; };
        const p0=at(G.x-G.podW), p1=at(G.x+G.podW);
        fo([p0.A,p1.A,p1.E,p0.E],'blk',0.35,0.01,[0,0,1]);
        fo([p0.A,p1.A,p1.B,p0.B],'blk',-0.1,0.01,n);
        fo([p0.A,p0.B,p0.D,p0.E],'blk',0,0.01,[-1,0,0]);
        fo([p1.A,p1.B,p1.D,p1.E],'blk',0,0.01,[1,0,0]);
      })();
      TAG('main_deck','fit');
      // throttles and trim levers for the port seat
      const tx=-FRONT_X, ty=yFront(tx)-DASH_D-0.05;
      boxF([tx,ty,Z_DASH+0.03],[0.09,0.05,0.04],'blk',0.2,-0.01);
      for(const dx of [-0.05,-0.017,0.017,0.05]) tubeF([tx+dx,ty,Z_DASH+0.06],[tx+dx,ty-0.09,Z_DASH+(Math.abs(dx)>0.03?0.20:0.15)],0.014,'steel',0.45);
    })();

    TAG('main_deck','fit');
    // ---- seats: two rows of bolsters, the rear bench on the bulkhead --------------------------------
    function bolster(sx, sy, hw){
      const zs=SEAT_Z, ai=hw-0.105;
      tubeF([sx,sy,SOLE],[sx,sy,zs-0.10],0.05,'blk',0.2);
      boxF([sx,sy,SOLE+0.012],[0.14,0.14,0.012],'blk',0.1);
      F.push.apply(F, slab([sx,sy+0.02,zs-0.05],[hw-0.005,0.21,0.05],'uph',-1.7,0.1));
      fo([[sx-ai,sy-0.14,zs+0.002],[sx+ai,sy-0.14,zs+0.002],[sx+ai,sy+0.18,zs+0.002],[sx-ai,sy+0.18,zs+0.002]],'acc',-2.3,0.012,[0,0,1]);
      const rk=(p)=>[p[0], p[1]-(p[2]-zs)*0.20, p[2]];
      boxF([sx,sy-0.25,zs+0.30],[hw,0.06,0.30],'uph',0.05,0,rk);
      fo([rk([sx-ai,sy-0.186,zs+0.10]),rk([sx+ai,sy-0.186,zs+0.10]),rk([sx+ai,sy-0.186,zs+0.52]),rk([sx-ai,sy-0.186,zs+0.52])],'acc',0.05,0.012,[0,1,0.2]);
      for(const s of [-1,1]) boxF([sx+s*(hw-0.03),sy-0.13,zs+0.16],[0.03,0.08,0.16],'uph',0.1,0,rk);
    }
    for(const sx of [-FRONT_X,FRONT_X]) bolster(sx,ROW1,0.225);
    for(const sx of [-REAR_X,0,REAR_X]) bolster(sx,REAR,0.215);

    TAG('foredeck','wall',{blk:'trim'});
    // ---- windscreen: smoked glass wrapped round the cockpit front, wings sweeping down the side decks
    (function(){
      const n=FRONT_N, A=XF+WS.off, B=(Y_FC-Y_F0)+WS.off, pts=[];
      for(let k=0;k<=WS.NC;k++){ const th=Math.PI/2*(1-k/WS.NC); pts.push([A*Math.pow(Math.cos(th),2/n), Y_F0+B*Math.pow(Math.sin(th),2/n)]); }
      for(let k=1;k<=WS.NW;k++) pts.push([A, lerp(Y_F0,WS.yEnd,k/WS.NW)]);
      const M=pts.length-1;
      const baseZ=(x,y)=>{ const u=uOf(y); return deckZ(u,Math.min(1,Math.abs(x)/xRail(u)))+0.004; };
      const nOf=(k)=>{ const a=pts[Math.max(0,k-1)], b=pts[Math.min(M,k+1)]; let t=[b[0]-a[0],b[1]-a[1]]; const m=Math.hypot(t[0],t[1])||1; let nn=[t[1]/m,-t[0]/m];
        if(nn[0]*pts[k][0]+nn[1]*(pts[k][1]-Y_F0+0.3)<0) nn=[-nn[0],-nn[1]]; return nn; };
      for(const s of [-1,1]){
        const base=[], topP=[];
        for(let k=0;k<=M;k++){
          const a=k/M, x=s*pts[k][0], y=pts[k][1], nn=nOf(k);
          const ht=WS.ht-0.20*smooth((a-0.40)/0.60), rk=WS.rake*(1-0.5*smooth((a-0.5)/0.5));
          const b=[x,y,baseZ(x,y)];
          base.push(b); topP.push([x-s*nn[0]*rk, y-nn[1]*rk, b[2]+ht]);
        }
        for(let k=0;k<M;k++){
          const nn=nOf(k);
          fo([base[k],base[k+1],topP[k+1],topP[k]],'glas',-1.1,0,[s*nn[0],nn[1],0.35]);
          tubeF(topP[k],topP[k+1],0.018,'blk',0.45);
          tubeF(base[k],base[k+1],0.016,'blk',0.25);
        }
        tubeF(base[M],topP[M],0.018,'blk',0.3);
      }
    })();
  })();

  TAG(null);

  // ---- DYNAMIC parts -------------------------------------------------------------------------------
  // engine hatch: a raised lid over the bay with a centre hump, hinged at its aft edge
  const HATCH = { rise:0.14, hump:0.075, maxDeg:52, vents:[[-5.40,-5.24],[-5.14,-4.98],[-4.88,-4.72],[-4.62,-4.46]] };
  const sH=(u)=>Math.min(1, xo(u)/xRail(u));
  const hump=(u,s)=>{ const y=yOf(u); return HATCH.hump*(1-smooth((s-0.20)/0.22))*smooth((y+5.45)/0.30)*(1-smooth((y+4.20)/0.30)); };
  const lidZ=(u,s)=>deckZ(u,s)+HATCH.rise+hump(u,s);
  HATCH.yH=Y_H0; HATCH.zH=lidZ(U_H0,0);
  const _lid={};
  function lidFaces(gid){
    if(_lid[gid]) return _lid[gid];
    const G=GRAPHICS[gid], out=[], zero=K(0);
    const uu=grid(U_H0,U_C0,20,[-5.45,-5.30,-5.15,-4.20,-4.05,-3.90].map(uOf).concat(knotsOf(G.deck)));
    const SK=[0.10,0.20,0.31,0.42,0.6];
    for(const side of [-1,1]){
      const P=(u,s)=>[side*s*xRail(u), yOf(u), lidZ(u,s)];
      paintStrip.length;
      for(let i=0;i+1<uu.length;i++)
        paintStrip(uu[i],uu[i+1],P,zero,sH,SK,G.deck,'deck',()=>DECK_B,(A,B,C,D,mat,b)=>faceO(out,[A,B,C,D],mat,b,0,[0,0,1]));
      for(let i=0;i+1<uu.length;i++){   // side walls, painted as the lid's outermost band
        const ua=uu[i], ub=uu[i+1], um=(ua+ub)/2, mat=matAt(G.deck,um,sH(um)-0.004,'deck');
        const A=[side*xo(ua),yOf(ua),deckZ(ua,sH(ua))], B=[side*xo(ub),yOf(ub),deckZ(ub,sH(ub))];
        faceO(out,[A,B,[B[0],B[1],lidZ(ub,sH(ub))],[A[0],A[1],lidZ(ua,sH(ua))]],mat,-0.2,0,[side,0,0]);
      }
      for(const [y0,y1] of HATCH.vents){
        const ua=uOf(y0), ub=uOf(y1), xa=side*(xo(ua)+0.004), xb=side*(xo(ub)+0.004);
        const za=deckZ(ua,sH(ua)), zb=deckZ(ub,sH(ub));
        faceO(out,[[xa,y0,za+0.035],[xb,y1,zb+0.035],[xb,y1,zb+HATCH.rise-0.035],[xa,y0,za+HATCH.rise-0.035]],'blk',0.1,0.01,[side,0,0]);
      }
      for(const [u,wy] of [[U_C0,1],[U_H0,-1]]){   // front and back walls
        const xr=xRail(u), st=[0,0.2,0.42,0.6,sH(u)];
        for(let k=0;k+1<st.length;k++){
          const s0=st[k], s1=st[k+1], mat=matAt(G.deck,u+(wy>0?-0.005:0.005),(s0+s1)/2,'deck');
          faceO(out,[[side*s0*xr,yOf(u),deckZ(u,s0)],[side*s1*xr,yOf(u),deckZ(u,s1)],[side*s1*xr,yOf(u),lidZ(u,s1)],[side*s0*xr,yOf(u),lidZ(u,s0)]],mat,wy>0?-0.1:-0.5,0,[0,wy,0]);
        }
      }
      for(let i=0;i<8;i++){   // underside
        const ua=lerp(U_H0,U_C0,i/8), ub=lerp(U_H0,U_C0,(i+1)/8);
        for(const [s0,s1] of [[0,0.5],[0.5,1]]){
          const Pq=(u,t)=>{ const s=t*sH(u); return [side*s*xRail(u),yOf(u),deckZ(u,s)+0.01]; };
          faceO(out,[Pq(ua,s0),Pq(ub,s0),Pq(ub,s1),Pq(ua,s1)],'liner',-1.3,0,[0,0,-1]);
        }
      }
    }
    _lid[gid]=out;
    return out;
  }
  const hatchXf=(h)=>{ const a=h*HATCH.maxDeg*DEG, ca=Math.cos(a), sa=Math.sin(a), yh=HATCH.yH, zh=HATCH.zH;
    return (p)=>{ const dy=p[1]-yh, dz=p[2]-zh; return [p[0], yh+dy*ca-dz*sa, zh+dy*sa+dz*ca]; }; };

  // the bay and the twin big-blocks, only built with the lid up
  const BAY = [];
  (function(){
    const out=BAY, BZ=0.52, push=(fs)=>{ for(const f of fs) out.push(f); };
    const N=10;
    for(let i=0;i<N;i++){
      const u0=lerp(U_H0,U_C0,i/N), u1=lerp(U_H0,U_C0,(i+1)/N), w0=xl(u0,BZ), w1=xl(u1,BZ);
      faceO(out,[[-w0,yOf(u0),BZ],[w0,yOf(u0),BZ],[w1,yOf(u1),BZ],[-w1,yOf(u1),BZ]],'liner',-1.6,0,[0,0,1]);
      for(const s of [-1,1]){
        const x0=xo(u0), x1=xo(u1);
        faceO(out,[[s*x0,yOf(u0),BZ],[s*x1,yOf(u1),BZ],[s*x1,yOf(u1),deckZ(u1,sH(u1))],[s*x0,yOf(u0),deckZ(u0,sH(u0))]],'liner',-1.0,0,[-s,0,0]);
      }
    }
    for(const [u,wy,b] of [[U_H0,1,-0.9],[U_C0,-1,-1.1]]){
      const xr=xRail(u), xw=xo(u), y=yOf(u)+wy*0.004;
      for(let k=0;k<8;k++){
        const xa=-xw+2*xw*k/8, xb=-xw+2*xw*(k+1)/8;
        faceO(out,[[xa,y,BZ],[xb,y,BZ],[xb,y,deckZ(u,Math.abs(xb)/xr)],[xa,y,deckZ(u,Math.abs(xa)/xr)]],'liner',b,0,[0,wy,0]);
      }
    }
    for(const ex of [-0.40,0.40]){
      push(box([ex,-4.67,0.80],[0.19,0.64,0.22],'eng',0.1));
      push(box([ex,-5.44,0.76],[0.19,0.12,0.21],'eng',-0.15));
      for(const s of [-1,1]) push(box([ex+s*0.135,-4.67,1.065],[0.065,0.56,0.045],'g1',0.35));
      push(box([ex,-4.67,1.06],[0.06,0.50,0.05],'steel',0.25));
      push(box([ex,-4.40,1.16],[0.10,0.12,0.06],'steel',0.55));
      push(prism([ex,-4.40,1.22],0.09,0.05,8,'blk',0.3));
      for(const s of [-1,1]) push(box([ex+s*0.25,-4.75,0.90],[0.045,0.50,0.075],'blk',0.2));
      push(box([ex,-3.95,0.72],[0.11,0.05,0.14],'blk',0.25));
    }
    push(box([0,-3.82,0.60],[0.08,0.09,0.08],'blk',0.25));   // battery box on the centreline
  })();
  const RAM = [ [ -0.62, -4.55, 0.80, -4.05 ], [ 0.62, -4.55, 0.80, -4.05 ] ];   // x, y on the bay wall, z, y under the lid

  // sterndrives (tag 2): the fleet leg at 1.3x, twinned, trim pins then the vertical gimbal axis
  function driveFaces(s, out, tag){
    const k=DRIVE.k;
    const tr=(DRIVE.trimMin+(DRIVE.trimMax-DRIVE.trimMin)*s.trim+DRIVE.tiltMax*s.tilt)*DEG;
    const st=s.steer*DRIVE.steerMax*DEG, ct=Math.cos(tr), stt=Math.sin(tr), cs=Math.cos(st), ss=Math.sin(st);
    for(const xs of DRIVE.xs){
      const R=(x,dy,dz)=>[xs+x*k, DRIVE.yG+dy*k, DRIVE.zT+dz*k];
      const trim=(p)=>{ const dy=p[1]-DRIVE.yG, dz=p[2]-DRIVE.zT; return [p[0], DRIVE.yG+dy*ct+dz*stt, DRIVE.zT-dy*stt+dz*ct]; };
      const steer=(p)=>{ const dx=p[0]-xs, dy=p[1]-DRIVE.yG; return [xs+dx*cs+dy*ss, DRIVE.yG-dx*ss+dy*cs, p[2]]; };
      const full=(p)=>steer(trim(p));
      const add=(fs,xf)=>{ for(const f of fs){ f.v=f.v.map(xf); f.tag=tag; if(f.db==null||f.db===0) f.db=0.01; out.push(f); } };
      const B=(c,h,mat,b)=>box(R(c[0],c[1],c[2]),[h[0]*k,h[1]*k,h[2]*k],mat,b);
      add(B([0,0.015,-0.04],[0.12,0.03,0.11],'drv',0.0), steer);
      add(B([0,-0.05,-0.03],[0.10,0.05,0.10],'drv',0.1), full);
      add(B([0,-0.15,-0.06],[0.085,0.10,0.12],'drv',0.25), full);
      add(B([0,-0.13,0.08],[0.07,0.085,0.028],'drv',0.55), full);
      (function(){
        const t0=-0.18, t1=-0.48, a=[[-0.055,-0.05],[0.055,-0.05],[0.055,-0.23],[-0.055,-0.23]], b=[[-0.045,-0.09],[0.045,-0.09],[0.045,-0.26],[-0.045,-0.26]];
        const fs=[];
        for(let q=0;q<4;q++){ const q2=(q+1)%4;
          const v=[R(a[q][0],a[q][1],t0),R(a[q2][0],a[q2][1],t0),R(b[q2][0],b[q2][1],t1),R(b[q][0],b[q][1],t1)];
          faceO(fs,v,'drv',0.05,0,[(a[q][0]+a[q2][0])/2,(a[q][1]+a[q2][1])/2+0.15,0]); }
        add(fs, full);
      })();
      add(B([0,-0.18,-0.34],[0.13,0.17,0.008],'drv',0.55), full);
      add(tube(R(0,-0.06,-0.54),R(0,-0.35,-0.54),0.055*k,'drv',0.1), full);
      add(B([0,-0.22,-0.67],[0.010,0.05,0.08],'drv',-0.2), full);
      add(tube(R(0,-0.35,-0.54),R(0,-0.43,-0.54),0.032*k,'steel',0.3), full);
      const pa=(s.prop||0)*DEG+(xs<0?Math.PI/4:0), blades=[];
      for(let q=0;q<4;q++){
        const a=pa+q*Math.PI/2, r0=0.035, r1=0.17, w0=0.026, w1=0.046;
        const e=[Math.cos(a),Math.sin(a)], t=[-Math.sin(a),Math.cos(a)];
        const P=(r,w)=>R(e[0]*r+t[0]*w, -0.39, -0.54+e[1]*r+t[1]*w);
        blades.push({v:[P(r0,-w0),P(r1,-w1),P(r1,w1*0.6),P(r0,w0)], mat:'steel', b:0.35, db:0.01});
      }
      add(blades, full);
    }
  }

  // steering wheel (tag 1): three-spoke sport wheel on the starboard console face
  // 10-03: hub y yFront(FRONT_X)-DASH_D-0.13 (-1.719), z 1.10, rake 24 (top toward the bow): the hub sat 0.08 m
  // under the bolster's cushion and 10.3's drive clip missed the rim (0 of 30). Now GF_HUB: z 1.40, y -1.78, rake -20
  // (top toward the helmsman), the column along the axis into the helm pod: seated, 30 of 30 builds on 10.3
  const WHEEL_HUB = { x:GF_HUB.x, y:GF_HUB.y, z:GF_HUB.z };
  const WHEEL_GEO = { rad:0.185, rimIn:0.155, rake:GF_HUB.rake, seg:16, hubR:0.05, spokes:[0,180,270], col:GF_HUB.col };
  const WHEEL_LOCK = 1.5;
  function wheelFaces(deg){
    const G=WHEEL_GEO, r=G.rake*DEG, out=[];
    const P0=[WHEEL_HUB.x, WHEEL_HUB.y, WHEEL_HUB.z];
    const n=[0,-Math.cos(r),Math.sin(r)], e1=[1,0,0], e2=[0,Math.sin(r),Math.cos(r)];
    const at=(rho,th,off)=>[ P0[0]+rho*(Math.cos(th)*e1[0]+Math.sin(th)*e2[0])+n[0]*off,
                             P0[1]+rho*(Math.cos(th)*e1[1]+Math.sin(th)*e2[1])+n[1]*off,
                             P0[2]+rho*(Math.cos(th)*e1[2]+Math.sin(th)*e2[2])+n[2]*off ];
    const push=(v,mat,b)=>out.push({v,mat,b:b||0,db:0.30,tag:1});
    const th0=(deg||0)*DEG;
    for(let k=0;k<G.seg;k++){
      const a0=th0+2*Math.PI*k/G.seg, a1=th0+2*Math.PI*(k+1)/G.seg;
      push([at(G.rimIn,a0,0.016),at(G.rad,a0,0.016),at(G.rad,a1,0.016),at(G.rimIn,a1,0.016)],'blk',0.6);
      push([at(G.rad,a0,0.016),at(G.rad,a0,-0.012),at(G.rad,a1,-0.012),at(G.rad,a1,0.016)],'blk',-0.3);
    }
    for(const sd of G.spokes){
      const a=th0+sd*DEG, w=0.022, ta=a+Math.PI/2;
      const wv=[w*(Math.cos(ta)*e1[0]+Math.sin(ta)*e2[0]), w*(Math.cos(ta)*e1[1]+Math.sin(ta)*e2[1]), w*(Math.cos(ta)*e1[2]+Math.sin(ta)*e2[2])];
      const A=at(G.hubR*0.8,a,0.012), B=at(G.rimIn+0.004,a,0.012);
      const off=(p,s)=>[p[0]+wv[0]*s,p[1]+wv[1]*s,p[2]+wv[2]*s];
      push([off(A,1),off(B,1),off(B,-1),off(A,-1)],'steel',0.6);
    }
    for(let k=0;k<8;k++){ const a0=2*Math.PI*k/8, a1=2*Math.PI*(k+1)/8; push([P0,at(G.hubR,a0,0.020),at(G.hubR,a1,0.020),P0],'steel',0.85); }
    for(const f of tube([P0[0]-n[0]*G.col,P0[1]-n[1]*G.col,P0[2]-n[2]*G.col],P0,0.022,'blk',0.2)){ f.tag=1; f.db=0.25; out.push(f); }   // the column, along the axis into the pod
    return out;
  }
  function wheelDeg(o){
    o=o||{}; if(o.deg!=null) return o.deg;
    const turns=o.turns==null?WHEEL_LOCK:o.turns;
    return -Math.max(-1,Math.min(1,o.steer||0))*turns*360;
  }

  // ---- assembly -----------------------------------------------------------------------------------
  const r3=(v)=>Math.round(v*1000)/1000;
  function stateOf(o){
    o=o||{};
    return { hatch:clamp01(+o.hatch||0), steer:Math.max(-1,Math.min(1,+o.steer||0)), trim:clamp01(o.trim!=null?+o.trim:0.3),
             tilt:clamp01(+o.tilt||0), prop:+o.prop||0, drive:o.drive!==false, wheel:o.wheel!==false };
  }
  const _asm=new Map();
  function assemble(s, gid){
    const key=[gid, r3(s.hatch), s.drive?[r3(s.steer),r3(s.trim),r3(s.tilt),Math.round(s.prop)].join(','):'-', s.wheel?r3(wheelDeg(s)):'-'].join('|');
    if(_asm.has(key)) return _asm.get(key);
    const dyn=[], xf=hatchXf(s.hatch);
    for(const f of lidFaces(gid)) dyn.push({ v:f.v.map(xf), mat:f.mat, b:f.b, db:f.db });
    const _l=dyn.length;
    if(s.hatch>0.001){
      for(const f of BAY) dyn.push(f);
      for(const [x,yb,z,yl] of RAM){
        const ul=uOf(yl), lp=xf([x,yl,deckZ(ul,Math.min(1,Math.abs(x)/xRail(ul)))+0.005]);
        for(const f of tube([x,yb,z],lp,0.018,'steel',0.4)) dyn.push(f);
      }
    }
    const _b=dyn.length;
    if(s.drive) driveFaces(s, dyn, 2);
    const _d=dyn.length;
    if(s.wheel) for(const f of wheelFaces(wheelDeg(s))) dyn.push(f);
    // the new layout: the parts that move take their tags here (the lid is main_deck/fit; the bay, the engines and
    // the rams, drawn only with the lid up, and the legs are hull/part; the wheel stands on the pod, foredeck/fit)
    stampTags(dyn, 0, _l, 'main_deck', 'fit');
    stampTags(dyn, _l, _b, 'hull', 'part');
    stampTags(dyn, _b, _d, 'hull', 'part');
    stampTags(dyn, _d, dyn.length, 'foredeck', 'fit');
    const A={ F:F.concat(liveryFaces(gid), dyn), SH:[] };
    _asm.set(key,A); if(_asm.size>48) _asm.delete(_asm.keys().next().value);
    return A;
  }

  // ---- rasterizer (fleet recipe; yaw in the basis as on the RHIB; Newell normals so a blade tip's
  //      collapsed corner cannot flip its shade) ------------------------------------------------------
  function normal(rv){
    let nx=0,ny=0,nz=0;
    for(let i=0;i<rv.length;i++){ const a=rv[i], b=rv[(i+1)%rv.length];
      nx+=(a.yr-b.yr)*(a.zr+b.zr); ny+=(a.zr-b.zr)*(a.xr+b.xr); nz+=(a.xr-b.xr)*(a.yr+b.yr); }
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG, yaw=(opts.yaw||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch),
      cw:Math.cos(yaw), sw:Math.sin(yaw), heave:(opts.heave||0) };
  }
  function projVert(x,y,z,B){
    const xw=x*B.cw - y*B.sw, yw=x*B.sw + y*B.cw;
    const x1=xw*B.cr+z*B.sr, z1=-xw*B.sr+z*B.cr;
    const y2=yw*B.cq - z1*B.sq, z2=yw*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S - B.heave, d:(yr*B.ce-zr*B.se) };
  }
  function _paint(faces, opts, doEdge){
    const B=camBasis(opts), N=W*H;
    const pal=palette(opts), MATS=opts.mats||pal.mats, RINDEX=opts.mats ? nlRindex(opts.mats, pal.rindex) : pal.rindex;
    const zbuf=new Float32Array(N).fill(Infinity), col=new Array(N).fill(null), dep=new Float32Array(N), tag=new Int8Array(N);
    for(const f of faces){
      const rv=f.v.map(p=>projVert(p[0],p[1],p[2],B));
      const n=normal(rv);
      let sh=shadeOf(n,B.se,B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]],B.se,B.ce)*0.9;
      const fidx=sh*GAIN+BIAS+(f.b||0), M=MATS[f.mat]||MATS.deck;
      const base=Math.floor(fidx), frac=Math.max(0,Math.min(1,fidx-base)), dith=M.dith==null?1:M.dith;
      const db=f.db||0, ft=f.tag||0, ramp=M.ramp, rl=ramp.length-1, off=M.off;
      for(let t=1;t+1<rv.length;t++){
        const a=rv[0], b=rv[t], c=rv[t+1];
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) continue;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-db, i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d; tag[i]=ft;
            const thr = dith<=0 ? 0.5 : 0.5+(BAYER[x&3][y&3]-0.5)*dith;
            const idx=base+(frac>=thr?1:0)+off;
            col[i]=ramp[idx<0?0:idx>rl?rl:idx];
          }
        }
      }
    }
    const out=col.slice();
    if(opts.onlyTag){ for(let i=0;i<N;i++) if(tag[i]!==opts.onlyTag){ out[i]=null; col[i]=null; } }
    if(doEdge){
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){
        const i=y*W+x; if(!col[i]) continue;
        for(const [dx,dy] of [[1,0],[0,1]]){
          const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
          const j=ny*W+nx; if(!col[j]) continue;
          if(Math.abs(dep[i]-dep[j])>0.30){
            const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
            if(e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)];
          }
        }
      }
    }
    if(opts.outline===true || (opts.outline==null && KEYLINE_DEFAULT)){
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){
        const i=y*W+x; if(out[i]) continue;
        for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
          const nx=x+dx, ny=y+dy;
          if(nx>=0&&nx<W&&ny>=0&&ny<H&&col[ny*W+nx]){ out[i]=KEY; break; }
        }
      }
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
  const norm=(opts)=>(typeof opts==='number') ? {elev:opts} : (opts||{});
  function render(dir, opts){
    opts=norm(opts);
    const A=assemble(stateOf(opts), graphicOf(opts));
    return _toRGBA(_paint(A.F.concat(doorFaces(opts.doorOpen||0)), Object.assign({}, opts, {dir}), true));   // the leaf: doorFaces(t)
  }
  function renderWheel(dir, opts){
    opts=norm(opts);
    const A=assemble(Object.assign(stateOf(opts),{wheel:true}), graphicOf(opts));
    return _toRGBA(_paint(A.F, Object.assign({}, opts, {dir, onlyTag:1}), true));
  }
  function renderDrive(dir, opts){
    opts=norm(opts);
    const A=assemble(Object.assign(stateOf(opts),{drive:true}), graphicOf(opts));
    return _toRGBA(_paint(A.F, Object.assign({}, opts, {dir, onlyTag:2}), true));
  }
  // projected vertex box in cell px, for the cell-fit sweep
  function bounds(dir, opts){
    opts=norm(opts);
    const A=assemble(stateOf(opts), graphicOf(opts)), B=camBasis(Object.assign({}, opts, {dir}));
    let x0=1e9,y0=1e9,x1=-1e9,y1=-1e9;
    for(const f of A.F) for(const p of f.v){ const q=projVert(p[0],p[1],p[2],B); if(q.sx<x0)x0=q.sx; if(q.sx>x1)x1=q.sx; if(q.sy<y0)y0=q.sy; if(q.sy>y1)y1=q.sy; }
    return { x0, y0, x1, y1 };
  }

  // ---- anchors (hull-cell px), metres published beside them -----------------------------------------
  const _at=(P,dir,opts)=>{ const o=Object.assign({}, norm(opts), {dir}); const p=projVert(P.x,P.y,P.z,camBasis(o)); return { x:p.sx, y:p.sy }; };
  const _anchor=(P)=>function(dir,opts){ return _at(P,dir,opts); };
  const many=(list)=>function(dir,opts){ return list.map(P=>_at(P,dir,opts)); };
  const HELM      = { x:FRONT_X, y:ROW1, z:SEAT_Z };
  const CODRIVER  = { x:-FRONT_X, y:ROW1, z:SEAT_Z };
  const SEATS = [
    Object.assign({ id:'helm' }, HELM), Object.assign({ id:'codriver' }, CODRIVER),
    { id:'rear_port', x:-REAR_X, y:REAR, z:SEAT_Z }, { id:'rear_centre', x:0, y:REAR, z:SEAT_Z }, { id:'rear_stbd', x:REAR_X, y:REAR, z:SEAT_Z },
  ];
  const PILOT = { x:FRONT_X, y:ROW1+0.34, z:SOLE };
  const DRIVE_MOUNTS = DRIVE.xs.map(x=>({ x, y:DRIVE.yG, z:DRIVE.zT }));
  const PAINTER = { x:0, y:5.24, z:0.92 };
  const STERN_EYES = [ {x:-0.85,y:-L/2-0.012,z:1.12}, {x:0.85,y:-L/2-0.012,z:1.12} ];
  const GRATE = { x:0, y:-L/2-0.25, z:0.97 };
  const HINGE = { x:0, y:HATCH.yH, z:HATCH.zH };
  const WATERLINE = 0.52;

  const SPEC = { reference:'39 ft cigarette-type go-fast (black-and-yellow reference pair)', loa_hull:L, loa_grate:0.46, beam:2*T[2][1],
    deadrise_transom_deg:24, engines:'twin big-block V8s', drives:'twin racing sterndrives', seats:5,
    hatch_deg:HATCH.maxDeg };

  // ==================================================================================================
  // THE NEW LAYOUT: shared helpers. The same block stands in all three recreation rigs; the hull's own
  // block below it says what she is. Spec: docs/art/hull-layout-spec.md.
  // ==================================================================================================
  const NL_FRAME = 'metres; +x starboard, +y bow, +z up; origin amidships, keel bottom, centreline; polygons counter-clockwise seen from above';
  // the walker: radius = the largest collider of character rig 10.3's cast (deckboss, 0.23 m), so every
  // area fits every build; stepTol = v3's
  const NL_WALK = { radius:0.23, stepTol:0.34 };
  const NL_RULE = 'A step is legal when the target point is inside an area polygon and outside its holes, and |area z - player z| < stepTol. '+
    'Ramps interpolate z along their axis (x or y) between a0 and a1, and hold z0 / z1 beyond them. cut = the cutaway key while standing there; '+
    'cutHi / cutLo replace it above / below zMid (stairs and ladders). Polygons are player-centre: already inset by radius, so the reader never insets them again.';
  const q3=(v)=>Math.round(v*1000)/1000;
  const nlRect=(x0,y0,x1,y1)=>[[q3(x0),q3(y0)],[q3(x1),q3(y0)],[q3(x1),q3(y1)],[q3(x0),q3(y1)]];
  const nlGrow=(x0,y0,x1,y1,d)=>nlRect(x0-d,y0-d,x1+d,y1+d);
  const nlPt=(p)=>({ x:q3(p.x), y:q3(p.y), z:q3(p.z) });
  const nlV=(p)=>[q3(p[0]),q3(p[1]),q3(p[2])];
  function nlPip(pt, poly){ let c=false; for(let i=0,j=poly.length-1;i<poly.length;j=i++){ const a=poly[i], b=poly[j];
      if(((a[1]>pt[1])!==(b[1]>pt[1])) && (pt[0] < (b[0]-a[0])*(pt[1]-a[1])/((b[1]-a[1])||1e-12)+a[0])) c=!c; } return c; }
  function nlSegD(p,a,b){ const dx=b[0]-a[0], dy=b[1]-a[1], L2=dx*dx+dy*dy; let t=L2?((p[0]-a[0])*dx+(p[1]-a[1])*dy)/L2:0; t=Math.max(0,Math.min(1,t));
    return Math.hypot(p[0]-a[0]-t*dx, p[1]-a[1]-t*dy); }
  function nlPolyD(p, poly){ if(nlPip(p,poly)) return 0; let d=Infinity; for(let i=0;i<poly.length;i++) d=Math.min(d, nlSegD(p,poly[i],poly[(i+1)%poly.length])); return d; }
  // a symmetric area from a half-width profile: half(y) is the centre's largest |x| at y (already inset)
  function nlSide(half, y0, y1, n){ const R=[], Lt=[];
    for(let i=0;i<=n;i++){ const y=y0+(y1-y0)*i/n, w=Math.max(0.005, half(y)); R.push([q3(w),q3(y)]); Lt.unshift([q3(-w),q3(y)]); }
    return R.concat(Lt); }
  // the largest contiguous |x| from the centreline where ok(x, y) holds, in 5 mm steps (-1 if the centre fails)
  function nlHalfAt(ok, y){ if(!ok(0,y)) return -1; let x=0; while(x<4 && ok(x+0.005,y)) x+=0.005; return x; }
  // one scheme's paint table: MATS (name -> {ramp:[hex], off, dith}), its ramps deduplicated by content,
  // and M (name -> {ramp: index into ramps, off, dith}). roomMats names the room's materials.
  function nlMatsTable(scheme, MATS, roomMats){
    const ramps=[], at={}, M={};
    for(const k of Object.keys(MATS)){ const r=MATS[k].ramp, kk=r.join(',');
      if(at[kk]==null){ at[kk]=ramps.length; ramps.push(r.slice()); }
      M[k]={ ramp:at[kk], off:MATS[k].off||0 }; if(MATS[k].dith) M[k].dith=MATS[k].dith; }
    return { scheme, MATS, ramps, M, roomMats:(roomMats||[]).slice() };
  }
  const nlFaceCount=(faces)=>{ const o={}; for(const f of faces){ const k=Object.keys(LV).find(n=>LV[n]===f.lv)+'/'+Object.keys(RO).find(n=>RO[n]===f.ro); o[k]=(o[k]||0)+1; } return o; };
  // drop the vertices that lie within eps of the line through their neighbours (a straight run is one edge)
  function nlSimplify(poly, eps){ eps=eps==null?0.002:eps; const p=poly.slice(); let changed=true;
    while(changed && p.length>3){ changed=false;
      for(let i=0;i<p.length && p.length>3;i++){ const a=p[(i+p.length-1)%p.length], b=p[i], c=p[(i+1)%p.length];
        const dx=c[0]-a[0], dy=c[1]-a[1], l=Math.hypot(dx,dy);
        const d=l<1e-9 ? Math.hypot(b[0]-a[0],b[1]-a[1]) : Math.abs(dx*(b[1]-a[1])-dy*(b[0]-a[0]))/l;
        if(d<eps){ p.splice(i,1); changed=true; i--; } } }
    return p; }
  // a crowned or humped surface as walk areas of v3's two kinds: flat (z) or a ramp along x or y. The surface is cut
  // fore and aft into slices, and each slice athwartships into a centre cell and cells either side of it (mirrored to
  // port), each as wide as its best kind (flat, ramp x, ramp y) holds zAt within tol. A slice grows forward while it
  // needs no more than K cells a side; K from 1 to 4 is tried and the fewest pieces kept. zAt(x, y) is symmetric in x;
  // halfAt(y) is the centre's largest |x| at y (already inset by the walker). Ids <id>_<slice><c | s<k> | p<k>>.
  function nlSurfaceAreas(id, cut, zAt, halfAt, y0, y1, tol){
    tol=tol==null?0.02:tol;
    // zAt cached on a 0.01 m lattice over the surface's box (bilinear between its nodes)
    let W0=0; for(let y=y0; y<=y1+1e-9; y+=0.01) W0=Math.max(W0, halfAt(Math.min(y,y1)));
    const CX=Math.ceil(W0/0.01)+2, CY=Math.ceil((y1-y0)/0.01)+2, Zc=new Float64Array((CX+1)*(CY+1));
    for(let j=0;j<=CY;j++) for(let i=0;i<=CX;i++) Zc[j*(CX+1)+i]=zAt(i*0.01, Math.min(y1, y0+j*0.01));
    const Z=(x,y)=>{ const fx=Math.min(CX-1e-9,Math.abs(x)/0.01), fy=Math.max(0,Math.min(CY-1e-9,(y-y0)/0.01)), i=Math.floor(fx), j=Math.floor(fy), tx=fx-i, ty=fy-j, k=j*(CX+1)+i;
      return (Zc[k]*(1-tx)+Zc[k+1]*tx)*(1-ty)+(Zc[k+CX+1]*(1-tx)+Zc[k+CX+2]*tx)*ty; };
    // the best kind for the cell [xa, xb] x [ya, yb] clipped to |x| <= halfAt(y): { err, z } or { err, ramp }
    function fitCell(xa, xb, ya, yb){
      const ny=Math.max(2,Math.ceil((yb-ya)/0.04)+1), nx=Math.max(2,Math.ceil((xb-xa)/0.04)+1), P=[];
      for(let j=0;j<ny;j++){ const y=ya+(yb-ya)*j/(ny-1), h=halfAt(y), lo=Math.max(xa,-h), hi=Math.min(xb,h); if(hi<=lo+1e-6) continue;
        for(let i=0;i<nx;i++){ const x=lo+(hi-lo)*i/(nx-1); P.push([x,y,Z(x,y)]); } }
      if(!P.length) return null;
      let lo=Infinity, hi=-Infinity; for(const p of P){ lo=Math.min(lo,p[2]); hi=Math.max(hi,p[2]); }
      let best={ err:(hi-lo)/2, z:(hi+lo)/2 };
      for(const ax of [0,1]){ const a0=ax?ya:xa, a1=ax?yb:xb; if(a1-a0<1e-6) continue;
        let n=0, st=0, sz=0, stt=0, stz=0; for(const p of P){ const t=(p[ax]-a0)/(a1-a0); n++; st+=t; sz+=p[2]; stt+=t*t; stz+=t*p[2]; }
        const den=n*stt-st*st; if(Math.abs(den)<1e-12) continue;
        const q=(n*stz-st*sz)/den; let c=(sz-q*st)/n, rlo=Infinity, rhi=-Infinity;
        for(const p of P){ const r=p[2]-(c+q*(p[ax]-a0)/(a1-a0)); rlo=Math.min(rlo,r); rhi=Math.max(rhi,r); }
        c+=(rlo+rhi)/2; const err=(rhi-rlo)/2;
        if(err<best.err-1e-9) best={ err, ramp:{ axis:ax?'y':'x', a0, a1, z0:c, z1:c+q } }; }
      return best;
    }
    // the widest cell from xa (to at most xEnd) that fits, by bisection on 0.02 m steps (the narrowest is one step)
    function widest(xa, xEnd, ya, yb, centred){
      const fit=(xb)=>centred ? fitCell(-xb, xb, ya, yb) : fitCell(xa, xb, ya, yb);
      const ok=(xb)=>{ const f=fit(xb); return !!(f && f.err<=tol-0.0015); };   // 1.5 mm kept back for the rounding to mm
      const n=Math.max(1, Math.ceil((xEnd-xa)/0.02-1e-9)), at=(k)=>k>=n ? xEnd : xa+k*0.02;
      if(ok(at(n))) return { xb:xEnd, f:fit(xEnd) };
      let lo=0, hi=n; while(hi-lo>1){ const m=(lo+hi)>>1; if(ok(at(m))) lo=m; else hi=m; }
      const xb=at(Math.max(1,lo)); return { xb, f:fit(xb) };
    }
    function tileSlice(ya, yb){
      let wmax=0; for(let k=0;k<=8;k++) wmax=Math.max(wmax, halfAt(ya+(yb-ya)*k/8));
      const c=widest(0, wmax, ya, yb, true), cells=[{ xa:-c.xb, xb:c.xb, f:c.f, centre:true }];
      let xa=c.xb; while(xa<wmax-0.005){ const w=widest(xa, wmax, ya, yb, false); cells.push({ xa, xb:w.xb, f:w.f }); xa=w.xb; }
      return cells;
    }
    function plan(K){
      const slices=[]; let ya=y0;
      while(ya<y1-0.005){
        const n=Math.max(1, Math.ceil((y1-ya)/0.02-1e-9)), at=(k)=>k>=n ? y1 : ya+k*0.02;
        const fits=(k)=>{ const t=tileSlice(ya, at(k)); return t.length-1<=K ? t : null; };
        let t=fits(n), yb=y1;
        if(!t){ let lo=0, hi=n; while(hi-lo>1){ const m=(lo+hi)>>1; if(fits(m)) lo=m; else hi=m; } yb=at(Math.max(1,lo)); t=tileSlice(ya, yb); }
        slices.push({ ya, yb, cells:t }); ya=yb;
      }
      return slices;
    }
    let best=null; for(let K=1;K<=4;K++){ const p=plan(K), n=p.reduce((a,sl)=>a+2*sl.cells.length-1,0); if(!best || n<best.n) best={ p, n, K }; }
    // the pieces' outlines and kinds (each side cell mirrored to port)
    const areas=[];
    const kind=(f, mirror)=>{ if(f.z!=null) return { z:q3(f.z) }; const r=f.ramp;
      if(r.axis==='y' || !mirror) return { ramp:{ axis:r.axis, a0:q3(r.a0), a1:q3(r.a1), z0:q3(r.z0), z1:q3(r.z1) } };
      return { ramp:{ axis:'x', a0:q3(-r.a1), a1:q3(-r.a0), z0:q3(r.z1), z1:q3(r.z0) } }; };
    best.p.forEach((sl, si)=>{
      const m=Math.max(2, Math.round((sl.yb-sl.ya)/0.05)), ys=[]; for(let k=0;k<=m;k++) ys.push(sl.ya+(sl.yb-sl.ya)*k/m);
      sl.cells.forEach((c, k)=>{
        // the cell's outline: its outer edge follows halfAt where that is inside xb; rows where halfAt <= xa drop out
        const live=ys.filter(y=>Math.min(c.xb, halfAt(y))>(c.centre?0.005:c.xa+0.005)); if(live.length<2) return;
        const R=live.map(y=>[q3(Math.min(c.xb, halfAt(y))), q3(y)]);
        if(c.centre){ areas.push(Object.assign({ id:id+'_'+(si+1)+'c', cut }, kind(c.f,false), { poly:nlSimplify(R.concat(R.map(p=>[-p[0],p[1]]).reverse())), holes:[] })); return; }
        const poly=nlSimplify([[q3(c.xa), q3(live[live.length-1])],[q3(c.xa), q3(live[0])]].concat(R));
        areas.push(Object.assign({ id:id+'_'+(si+1)+'s'+k, cut }, kind(c.f,false), { poly, holes:[] }));
        areas.push(Object.assign({ id:id+'_'+(si+1)+'p'+k, cut }, kind(c.f,true), { poly:poly.map(p=>[-p[0],p[1]]).reverse(), holes:[] }));
      });
    });
    // the error measured again on the pieces as written (rounded), against zAt itself, every 0.02 m inside each
    let worst=0; for(const a of areas){ let x0=Infinity,x1=-Infinity,ya=Infinity,yb=-Infinity; for(const [x,y] of a.poly){ x0=Math.min(x0,x); x1=Math.max(x1,x); ya=Math.min(ya,y); yb=Math.max(yb,y); }
      for(let y=ya; y<=yb+1e-9; y+=0.02) for(let x=x0; x<=x1+1e-9; x+=0.02){ if(!nlPip([x,y],a.poly)) continue;
        worst=Math.max(worst, Math.abs(nlAreaZ(a,x,y)-zAt(x,y))); } }
    return { areas, worst:q3(worst), slices:best.p.length, K:best.K };
  }
  // the cut as the engine's gate draws it (ADR 0048 §6.4, the lift-set form), for review pictures: standing under cut
  // key `key`, the levels in gate[key] lift whole and its level:role entries lift that role (an untagged face counts
  // as every role); the room of the level cutaway.shows[key] names is drawn, its front faces only (the engine's lvl.z
  // test: near walls culled, far walls kept), moved forward by the hull's bounding diameter as the interior bake does
  // (BoatInteriorGeometryExtractor: her own db kept on top). Everything else is painted exactly as render() paints it;
  // the paints are matsFor's table (the hull's, and the room's appended), which the painter takes as opts.mats.
  function renderCut(dir, key, opts){
    opts=norm(opts);
    const C=geometry().cutaway;
    if(!C.gate || !Object.prototype.hasOwnProperty.call(C.gate, key)) throw new Error('renderCut: no cut key '+key);
    const whole=new Set(), roles=new Map();
    for(const e of C.gate[key]){ const k=e.indexOf(':');
      if(k<0){ whole.add(LV[e]); continue; }
      const l=LV[e.slice(0,k)]; if(!roles.has(l)) roles.set(l, new Set()); roles.get(l).add(RO[e.slice(k+1)]); }
    const lifted=(f)=>whole.has(f.lv) || (roles.has(f.lv) && (f.ro==null || roles.get(f.lv).has(f.ro)));
    const s=stateOf(opts), A=assemble(s, typeof graphicOf==='function' ? graphicOf(opts) : undefined);
    const ext=A.F.concat(doorFaces(s.door!=null ? s.door : (+opts.doorOpen||0))).filter(f=>!lifted(f)), room=[];
    const shown=C.shows && C.shows[key]!=null ? LV[C.shows[key]] : null;
    if(shown!=null){
      const B=camBasis(Object.assign({}, opts, { dir }));
      let x0=Infinity, y0=Infinity, z0=Infinity, x1=-Infinity, y1=-Infinity, z1=-Infinity;
      for(const f of A.F) for(const p of f.v){ if(p[0]<x0)x0=p[0]; if(p[0]>x1)x1=p[0]; if(p[1]<y0)y0=p[1]; if(p[1]>y1)y1=p[1]; if(p[2]<z0)z0=p[2]; if(p[2]>z1)z1=p[2]; }
      const shift=Math.hypot(x1-x0, y1-y0, z1-z0);
      for(const f of interiorFaces()){ if(f.lv!==shown) continue;
        const rv=f.v.map(p=>projVert(p[0],p[1],p[2],B)); let ny=0, nz=0;
        for(let i=0;i<rv.length;i++){ const a=rv[i], b=rv[(i+1)%rv.length]; ny+=(a.zr-b.zr)*(a.xr+b.xr); nz+=(a.xr-b.xr)*(a.yr+b.yr); }
        if(-ny*B.ce+nz*B.se<0) continue;   // turned away from the camera
        room.push(Object.assign({}, f, { db:(f.db||0)+shift })); }
    }
    return _toRGBA(_paint(ext.concat(room), Object.assign({}, opts, { dir, mats:matsFor(opts).MATS }), true, A.SH||[]));
  }
  // a colour -> { r, i } index over a mats table, the palette's own first (the painter's edge and shade steps read it)
  function nlRindex(mats, base){ const rx=Object.assign({}, base);
    for(const k of Object.keys(mats)) mats[k].ramp.forEach((c,i)=>{ if(!rx[c]) rx[c]={ r:mats[k].ramp, i }; });
    return rx; }
  // house.levels and house.decks (ADR 0048 §8 B) from geometry().levels: levels names the cabin's levels (kind hard),
  // as v3's walk does; decks gives every level's sole and ceiling as geometry() has them (ceilZ = its ceil; ceilLowZ
  // where the lid's lowest point over the sole differs from it; open: true where the level is sky)
  function nlHouseDecks(levels){
    const decks={};
    for(const l of levels){ const d={ deck:l.deck, kind:l.kind, soleZ:l.sole, ceilZ:l.ceil };
      if(l.kind==='open') d.open=true;
      if(l.ceil!=null && l.ceilingZ!=null && Math.abs(l.ceilingZ-l.ceil)>0.0005) d.ceilLowZ=l.ceilingZ;
      decks[l.id]=d; }
    return { levels:levels.filter(l=>l.kind==='hard').map(l=>l.id), decks };
  }
  // a berth for anchors.berths: the mattress's top outline as the room draws it (the liner less 0.09 m, at least
  // 0.06 m a side), CCW from above; a V-berth runs fore and aft (head aft, on the pillows), an athwartships berth
  // has its outline from y0 to y1 at the top's width (head to port)
  function nlBerth(id, interact, kind, y0, y1, top, deckheadZ, n){
    const w=(y)=>Math.max(0.06, Math.max(0.04, rmHalfAtZ(y, top, 1)-(RM_WT+0.02))), P=[];
    n=n||10;
    if(kind==='midberth'){ const a=w(y0), b=w(y1); P.push([-a,y0],[a,y0],[b,y1],[-b,y1]); }
    else { for(let i=0;i<=n;i++){ const y=y0+(y1-y0)*i/n; P.push([w(y),y]); }            // starboard, aft to forward
           for(let i=n;i>=0;i--){ const y=y0+(y1-y0)*i/n; P.push([-w(y),y]); } }          // port, forward to aft
    const poly=P.map(p=>[q3(p[0]),q3(p[1])]); let s=0;
    for(let i=0;i<poly.length;i++){ const a=poly[i], b=poly[(i+1)%poly.length]; s+=a[0]*b[1]-b[0]*a[1]; }
    return { id, interact, kind, topZ:q3(top), head:kind==='midberth'?'port':'aft', deckheadZ:q3(deckheadZ), poly:s<0?poly.reverse():poly };
  }
  // an area's z at (x, y): flat, or its ramp (held beyond a0 / a1)
  function nlAreaZ(a, x, y){ if(a.z!=null) return a.z; const r=a.ramp, v=r.axis==='x'?x:y, t=Math.max(0,Math.min(1,(v-r.a0)/(r.a1-r.a0))); return r.z0+(r.z1-r.z0)*t; }

  // ==================================================================================================
  // THE ROOM KIT: the 10-03 interior rig's builders (recreationInteriorRig.js), ported into the hull's own
  // rig as one dir-free mesh. The same block stands in the go-fast and the cruiser. Every face is turned
  // to the side it is seen from (the engine culls back faces and draws the room front-facing only, for the
  // level shown), tagged it:true on the room's level. Paints are the 10-03 room's. Two things changed from
  // 10-03, both so the room stays inside her hull (fitCheck):
  //   - the V: 10-03 drew a fitting's sides straight down to the sole; here a fitting whose face stands
  //     where the hull is narrower starts where the hull reaches it (zIn, vblock), as the liner does
  //   - the bow's rake: below the sheer a station's skin stands aft of its y (rakeAt), so the hull at
  //     world (y, z) is a station forward of uOf(y), narrower. Every width here is read at that station
  //     (rmU); 10-03 read uOf(y), which put the forward berth outside her forefoot
  // ==================================================================================================
  const RM_WT = 0.07;                                   // the liner's inset from the inner skin (fleet constant)
  // the station whose skin passes through world (y, z); null when (y, z) is forward of the stem at that height
  const rmYAt=(u,z)=>{ const st=station(u); return st.y+rakeAt(u,(z-st.kz)/Math.max(1e-6,st.dep)); };
  function rmU(y, z){ const u0=uOf(y); if(rmYAt(u0,z)>=y-1e-9) return u0; if(rmYAt(1,z)<y) return null;
    let lo=u0, hi=1; for(let i=0;i<40;i++){ const m=(lo+hi)/2; if(rmYAt(m,z)<y) lo=m; else hi=m; } return hi; }
  const rmHalfAtZ=(y,z,inset)=>{ const u=rmU(y,z); return u==null ? 0 : halfAtZ(u,z,inset); };
  const RM_RAMPS = {
    joinery:['#3b2216','#52301e','#6a3f27','#825032','#9a613d','#b0744b'],
    carpet: ['#23262a','#2e3237','#3a3f45','#474d54','#555c64','#646b73'],
    linen:  ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    stone:  ['#8d8d86','#a5a59d','#bcbcb3','#d0d0c7','#e1e1d8','#eeeee6'],
    porc:   ['#9aa09c','#b4b9b3','#ccd0c9','#dee1da','#eceee8','#f7f8f3'],
    cavity: ['#0d0b0a','#141110','#1b1715','#231d1a','#2b2420','#332b26'],
  };
  // the room's paints, appended after the hull's: white liner (lining) and its cut cap, the joinery and soft
  // goods; the berth's quilt takes the scheme's ramp named by quiltFrom (10-03: the go-fast's seat vinyl, the
  // others' accent)
  const RM_MATS = ['lining','cut','joinery','carpet','linen','quilt','stone','porc','cavity'];
  function rmMats(hullMats, white, quiltFrom){
    const R=RM_RAMPS, q=(hullMats[quiltFrom]&&hullMats[quiltFrom].ramp)||R.linen;
    return { lining:{ ramp:white, off:-1, dith:0 }, cut:{ ramp:white.slice(Math.max(0,white.length-3)), off:0, dith:0 },
      joinery:{ ramp:R.joinery, off:0, dith:0.15 }, carpet:{ ramp:R.carpet, off:0, dith:0.25 }, linen:{ ramp:R.linen, off:0, dith:0 },
      quilt:{ ramp:q, off:0, dith:0.12 }, stone:{ ramp:R.stone, off:0, dith:0 }, porc:{ ramp:R.porc, off:0, dith:0 },
      cavity:{ ramp:R.cavity, off:0, dith:0 } };
  }
  function rmTidy(p){
    const q=[]; for(const v of p){ const l=q[q.length-1]; if(!l || Math.hypot(v[0]-l[0],v[1]-l[1],v[2]-l[2])>1e-5) q.push(v); }
    if(q.length>1){ const f=q[0], l=q[q.length-1]; if(Math.hypot(f[0]-l[0],f[1]-l[1],f[2]-l[2])<=1e-5) q.pop(); }
    return q.length<3 ? null : q;
  }
  function rmKit(lvName){
    const out=[], LVN=LV[lvName];
    const half=(y,z,inset)=>Math.max(0.04, rmHalfAtZ(y,z,1)-(inset==null?RM_WT:inset));
    // the V: the lowest z at or above z0 where the liner (less 5 mm) reaches |x| = ax at y; the sheer if it never does.
    // A fitting whose face stands at ax starts there, so nothing stands outside the hull where she narrows to the keel
    const zIn=(y,ax,z0,inset)=>{ const u=uOf(y), k=inset==null?RM_WT+0.005:inset, ok=(z)=>rmHalfAtZ(y,z,1)-k>=ax-1e-9;
      if(ok(z0)) return z0; let lo=z0, hi=sheerZ(u); if(!ok(hi)) return hi;
      for(let i=0;i<32;i++){ const m=(lo+hi)/2; if(ok(m)) hi=m; else lo=m; } return hi; };
    // drop the points of a plane polygon that lie on the line through their neighbours
    const flat=(p)=>{ const q=p.slice(); for(let i=0;i<q.length && q.length>3;i++){ const a=q[(i+q.length-1)%q.length], b=q[i], c=q[(i+1)%q.length];
      const ab=[b[0]-a[0],b[1]-a[1],b[2]-a[2]], bc=[c[0]-b[0],c[1]-b[1],c[2]-b[2]];
      const x=[ab[1]*bc[2]-ab[2]*bc[1], ab[2]*bc[0]-ab[0]*bc[2], ab[0]*bc[1]-ab[1]*bc[0]];
      if(Math.hypot(x[0],x[1],x[2])<1e-7){ q.splice(i,1); i--; } } return q; };
    const nv=(v)=>{ let nx=0,ny=0,nz=0; for(let i=0;i<v.length;i++){ const a=v[i], b=v[(i+1)%v.length];
      nx+=(a[1]-b[1])*(a[2]+b[2]); ny+=(a[2]-b[2])*(a[0]+b[0]); nz+=(a[0]-b[0])*(a[1]+b[1]); } return [nx,ny,nz]; };
    const put=(v,mat,b,db,ro,want)=>{ const n=nv(v); if(want && n[0]*want[0]+n[1]*want[1]+n[2]*want[2]<0) v=v.slice().reverse();
      if(!(ro in RO)) throw new Error('room: unknown role '+ro);
      const f={ v, mat, b:b||0, db:db==null?0.02:db, lv:LVN, ro:RO[ro], it:true }; out.push(f); return f; };
    // a box standing on z0: top and four sides (no bottom), each turned outward
    const box=(x0,x1,y0,y1,z0,z1,mat,b,ro,bTop)=>{ b=b||0;
      put([[x0,y0,z1],[x1,y0,z1],[x1,y1,z1],[x0,y1,z1]],mat,bTop!=null?bTop:b+0.1,0.02,ro,[0,0,1]);
      put([[x0,y0,z0],[x1,y0,z0],[x1,y0,z1],[x0,y0,z1]],mat,b,0.02,ro,[0,-1,0]);
      put([[x1,y1,z0],[x0,y1,z0],[x0,y1,z1],[x1,y1,z1]],mat,b,0.02,ro,[0,1,0]);
      put([[x0,y1,z0],[x0,y0,z0],[x0,y0,z1],[x0,y1,z1]],mat,b,0.02,ro,[-1,0,0]);
      put([[x1,y0,z0],[x1,y1,z0],[x1,y1,z1],[x1,y0,z1]],mat,b,0.02,ro,[1,0,0]); };
    // a block standing on z0 inside the V: wherever the hull is narrower than the block its ends follow the section
    // (the liner less 5 mm) and its sides start where the hull reaches them; top at z1, no bottom
    const vblock=(x0,x1,y0,y1,z0,z1,mat,b,ro,bTop)=>{ b=b||0; const M=6, N=4;
      const hw=(y,z)=>Math.max(0.005, rmHalfAtZ(y,z,1)-RM_WT-0.005);
      const xr=(y,z)=>Math.min(x1, hw(y,z)), xl=(y,z)=>Math.max(x0, -hw(y,z));
      put([[xl(y0,z1),y0,z1],[xr(y0,z1),y0,z1],[xr(y1,z1),y1,z1],[xl(y1,z1),y1,z1]],mat,bTop!=null?bTop:b+0.1,0.02,ro,[0,0,1]);
      for(const [y,ny] of [[y0,-1],[y1,1]]){ const zs=[]; for(let i=0;i<=M;i++) zs.push(lerp(z0,z1,i/M));
        const tp=rmTidy(zs.map(z=>[xr(y,z),y,z]).concat(zs.slice().reverse().map(z=>[xl(y,z),y,z])));
        if(tp) put(flat(tp),mat,b,0.02,ro,[0,ny,0]); }
      for(const [x,nx] of [[x0,-1],[x1,1]]){ const ys=[]; for(let i=0;i<=N;i++) ys.push(lerp(y0,y1,i/N));
        const zi=ys.map(y=>Math.min(z1,zIn(y,Math.abs(x),z0)));
        if(zi.every(z=>z<=z0+1e-6)){ put([[x,y0,z0],[x,y1,z0],[x,y1,z1],[x,y0,z1]],mat,b,0.02,ro,[nx,0,0]); continue; }
        for(let i=0;i<N;i++){ if(zi[i]>=z1-1e-4 && zi[i+1]>=z1-1e-4) continue;
          const tp=rmTidy([[x,ys[i],zi[i]],[x,ys[i+1],zi[i+1]],[x,ys[i+1],z1],[x,ys[i],z1]]); if(tp) put(tp,mat,b,0.02,ro,[nx,0,0]); } } };
    // a sole: strips across the hull's section at zw (less the liner), laid at z
    const floor=(y0,y1,z,zw,n,mat,b)=>{ for(let i=0;i<n;i++){ const ya=lerp(y0,y1,i/n), yb=lerp(y0,y1,(i+1)/n), wa=half(ya,zw), wb=half(yb,zw);
      put([[-wa,ya,z],[wa,ya,z],[wb,yb,z],[-wb,yb,z]],mat,b,0.01,'floor',[0,0,1]); } };
    // the hull liner, one side, from zLo to zHi(y), in three bands; the cap is a light strip from the outer skin to the
    // liner along its top, so the section reads as a cut when the lid is lifted
    const liner=(side,y0,y1,zLo,zHi,n,mat)=>{ for(let i=0;i<n;i++){ const ya=lerp(y0,y1,i/n), yb=lerp(y0,y1,(i+1)/n), za=zHi(ya), zb=zHi(yb);
      for(let k=0;k<3;k++){ const z0a=lerp(zLo,za,k/3), z1a=lerp(zLo,za,(k+1)/3), z0b=lerp(zLo,zb,k/3), z1b=lerp(zLo,zb,(k+1)/3);
        put([[side*half(ya,z0a),ya,z0a],[side*half(yb,z0b),yb,z0b],[side*half(yb,z1b),yb,z1b],[side*half(ya,z1a),ya,z1a]],mat,k===2?0.05:-0.25,0.04,'lining',[-side,0,0]); }
      // the cap's outer edge stays under the sheer, where the deck crowns down to the skin (the liner's top is the lid's
      // underside at the liner, higher than the deck's edge): it never stands above the hull's own top edge
      const zo=(y,z)=>Math.min(z, sheerZ(uOf(y))-0.008);
      put([[side*rmHalfAtZ(ya,zo(ya,za)),ya,zo(ya,za)+0.004],[side*rmHalfAtZ(yb,zo(yb,zb)),yb,zo(yb,zb)+0.004],[side*half(yb,zb),yb,zb+0.004],[side*half(ya,za),ya,za+0.004]],'cut',0.6,0.03,'trim',[0,0,1]); } };
    // a transverse wall at y facing the room (want), rows from z0 to the top; each row is held inside the hull's section
    // and under top(x) (a crown, highest on the centreline); openings {x0, x1, z0, z1} are left out, a header over each
    // (clip {x0, x1}: the wall stops there instead of at the hull, where the hull is wider)
    const bulkhead=(y,z0,top,want,mat,openings,NZ,clip)=>{ openings=openings||[]; NZ=NZ||8;
      const zc=top(0), cAt=(z)=>{ if(top(0)<=z) return 0; let lo=0, hi=3; for(let i=0;i<32;i++){ const m=(lo+hi)/2; if(top(m)>=z) lo=m; else hi=m; } return lo; };
      const wAt=(z)=>Math.min(half(y,z), cAt(z)), lo=(w)=>clip?Math.max(clip.x0,-w):-w, hi=(w)=>clip?Math.min(clip.x1,w):w;
      const zs=[]; for(let k=0;k<=NZ;k++) zs.push(lerp(z0,zc,k/NZ));
      for(const o of openings) for(const z of [o.z0,o.z1]) if(z>z0+0.005 && z<zc-0.005) zs.push(z);
      zs.sort((a,b)=>a-b);
      for(let k=0;k+1<zs.length;k++){ const za=zs[k], zb=zs[k+1]; if(zb-za<0.004) continue;
        const wa=wAt(za), wb=wAt(Math.min(zb,zc-1e-6)), zm=(za+zb)/2;
        const cut=openings.filter(o=>zm>o.z0 && zm<o.z1).sort((a,b)=>a.x0-b.x0);
        const xs=[[lo(wa),lo(wb)]]; for(const o of cut) xs.push([o.x0,o.x0],[o.x1,o.x1]); xs.push([hi(wa),hi(wb)]);
        for(let j=0;j<xs.length;j+=2){ const a=xs[j], c=xs[j+1]; if(c[0]-a[0]<0.01 && c[1]-a[1]<0.01) continue;
          put([[a[0],y,za],[c[0],y,za],[c[1],y,zb],[a[1],y,zb]],mat,-0.1,0.03,'wall',want); } } };
    // a cabinet against the hull side: inboard face at xIn, top at `top`, ends following the section, a door line
    // (where the hull is narrower than xIn at the sole, the inboard face and the ends start where it reaches xIn)
    const cabinet=(it,SOLE)=>{ const s=it.side, top=it.top, y0=it.y0, y1=it.y1, xi=it.xIn, mat=it.mat||'joinery';
      const xo=(y,z)=>s*half(y,z,RM_WT+0.005), N=4, zi=(y)=>Math.min(top,zIn(y,Math.abs(xi),SOLE)), vee=zi(y0)>SOLE+1e-6 || zi(y1)>SOLE+1e-6;
      for(let i=0;i<N;i++){ const ya=lerp(y0,y1,i/N), yb=lerp(y0,y1,(i+1)/N);
        put([[xi,ya,top],[xo(ya,top),ya,top],[xo(yb,top),yb,top],[xi,yb,top]],it.topMat||mat,-0.9,0.02,'fit',[0,0,1]); }
      if(!vee) put([[xi,y0,SOLE],[xi,y1,SOLE],[xi,y1,top],[xi,y0,top]],mat,-0.2,0.02,'fit',[-s,0,0]);
      else for(let i=0;i<N;i++){ const ya=lerp(y0,y1,i/N), yb=lerp(y0,y1,(i+1)/N), tp=rmTidy([[xi,ya,zi(ya)],[xi,yb,zi(yb)],[xi,yb,top],[xi,ya,top]]);
        if(tp) put(tp,mat,-0.2,0.02,'fit',[-s,0,0]); }
      for(const [y,ny] of [[y0,-1],[y1,1]]){ const z0=zi(y), pts=[[xi,y,z0]], M=5; for(let k=0;k<=M;k++){ const z=lerp(z0,top,k/M); pts.push([s*Math.max(Math.abs(xi),Math.abs(xo(y,z))),y,z]); } pts.push([xi,y,top]);
        const tp=rmTidy(pts); if(tp) put(tp,mat,-0.35,0.02,'fit',[0,ny,0]); }
      const ym=(y0+y1)/2, zc=Math.max(SOLE+0.08, zi(ym)+0.04);
      if(top-0.06-zc>0.05) put([[xi-s*0.004,ym-0.006,zc],[xi-s*0.004,ym+0.006,zc],[xi-s*0.004,ym+0.006,top-0.06],[xi-s*0.004,ym-0.006,top-0.06]],'cavity',0.2,0.03,'fit',[-s,0,0]);
      if(it.cushion){ const z=top+0.005, ya=y0+0.05, yb=y1-0.05;   // a seat box's cushion
        put([[xi+s*0.04,ya,z],[xi+s*0.30,ya,z],[xi+s*0.30,yb,z],[xi+s*0.04,yb,z]],'uph',-1.6,0.02,'fit',[0,0,1]); } };
    // a V-berth: platform front, mattress (linen aft where the pillows lie, the quilt forward), two pillows
    const vberth=(it,SOLE)=>{ const top=it.top, base=top-it.mattress, NB=8, bw=(y,z)=>Math.max(0.06, half(y,z,RM_WT+0.02)), yA=it.y0, yB=it.y1;
      { const zs=[0,1,2,3].map(k=>lerp(SOLE,base,k/3));   // the berth's front follows the section down to the sole
        put(zs.map(z=>[bw(yA,z),yA,z]).concat(zs.slice().reverse().map(z=>[-bw(yA,z),yA,z])),'joinery',-0.15,0.02,'fit',[0,-1,0]); }
      for(let i=0;i<NB;i++){ const ya=lerp(yA,yB,i/NB), yb=lerp(yA,yB,(i+1)/NB);
        put([[-bw(ya,top),ya,top],[bw(ya,top),ya,top],[bw(yb,top),yb,top],[-bw(yb,top),yb,top]],i<2?'linen':'quilt',i<2?-1.2:-1.5,0.03,'fit',[0,0,1]);
        for(const sd of [-1,1]) put([[sd*bw(ya,base),ya,base],[sd*bw(yb,base),yb,base],[sd*bw(yb,top),yb,top],[sd*bw(ya,top),ya,top]],'quilt',-0.6,0.03,'fit',[sd,0,0]); }
      put([[-bw(yA,base),yA,base],[bw(yA,base),yA,base],[bw(yA,top),yA,top],[-bw(yA,top),yA,top]],'linen',-0.4,0.03,'fit',[0,-1,0]);   // 10-03 drew its foot at the top's width
      const pw=Math.min(0.30, bw(yA+0.2,top)*0.45);
      for(const k of [-1,1]) box(k*pw-pw*0.44,k*pw+pw*0.44,yA+0.06,yA+0.36,top,top+0.11,'linen',-0.3,'fit',-1.0); };
    // an athwartships berth under a deck (the cruiser's mid-cabin): front, mattress, pillows to port
    const midberth=(it,SOLE)=>{ const top=it.top, base=top-it.mattress, y0=it.y0, y1=it.y1, w=(y,z)=>half(y,z,RM_WT+0.02), NB=6;
      { const zs=[0,1,2,3].map(k=>lerp(SOLE,base,k/3));   // the berth's front follows the section down to the sole
        put(zs.map(z=>[w(y1,z),y1,z]).concat(zs.slice().reverse().map(z=>[-w(y1,z),y1,z])),'joinery',-0.15,0.02,'fit',[0,1,0]); }
      for(let i=0;i<NB;i++){ const xa=lerp(-1,1,i/NB), xb=lerp(-1,1,(i+1)/NB), edge=(i===0||i===NB-1);
        put([[xa*w(y0,top),y0,top],[xb*w(y0,top),y0,top],[xb*w(y1,top),y1,top],[xa*w(y1,top),y1,top]],edge?'linen':'quilt',edge?-1.2:-1.5,0.03,'fit',[0,0,1]); }
      put([[-w(y1,base),y1,base],[w(y1,base),y1,base],[w(y1,top),y1,top],[-w(y1,top),y1,top]],'quilt',-0.5,0.03,'fit',[0,1,0]);   // 10-03 drew its foot at the top's width
      box(-w(y0+0.5,top)+0.08,-w(y0+0.5,top)+0.42,y0+0.15,y1-0.15,top,top+0.11,'linen',-0.3,'fit',-1.0); };
    const shelf=(it)=>{ const N=6; for(let i=0;i<N;i++){ const ya=lerp(it.y0,it.y1,i/N), yb=lerp(it.y0,it.y1,(i+1)/N), wa=half(ya,it.z), wb=half(yb,it.z);
      put([[-wa,ya,it.z],[wa,ya,it.z],[wb,yb,it.z],[-wb,yb,it.z]],it.mat||'lining',-1.0,0.02,'fit',[0,0,1]); } };
    const berthfront=(it,SOLE,BTOP)=>{ const y=it.y-0.004, z0=SOLE+0.06, z1=BTOP-0.16;
      put([[-0.32,y,z0],[0.32,y,z0],[0.32,y,z1],[-0.32,y,z1]],'joinery',-0.05,0.035,'fit',[0,-1,0]); };
    const stove=(it)=>{ box(it.x0,it.x1,it.y0,it.y1,it.z,it.z+0.03,'blk',0.1,'fit',-0.4);
      for(const [cx2,cy2] of [[(it.x0+it.x1)/2,it.y0+0.10],[(it.x0+it.x1)/2,it.y1-0.10]]) box(cx2-0.06,cx2+0.06,cy2-0.06,cy2+0.06,it.z+0.03,it.z+0.045,'steel',0.2,'fit',-0.2); };
    const sink=(it)=>{ put([[it.x0,it.y0,it.z+0.004],[it.x1,it.y0,it.z+0.004],[it.x1,it.y1,it.z+0.004],[it.x0,it.y1,it.z+0.004]],'steel',-1.3,0.02,'fit',[0,0,1]);
      const fx=it.x0+0.04, fy=(it.y0+it.y1)/2; box(fx-0.015,fx+0.015,fy-0.015,fy+0.015,it.z,it.z+0.18,'steel',0.3,'fit'); };
    const toilet=(it,SOLE)=>{ const z=it.z0!=null?it.z0:SOLE+0.06;   // z0: the floor it stands on
      box(it.x-0.15,it.x+0.15,it.y-0.21,it.y+0.17,z,z+0.40,'porc',-0.1,'fit',-0.6);
      box(it.x-0.16,it.x+0.16,it.y+0.10,it.y+0.24,z+0.40,z+0.72,'porc',0.0,'fit',-0.6); };
    // a tall locker against the hull side: inboard face with a door line, a top, ends following the section
    const tall=(it,SOLE)=>{ const s=it.side, xi=it.xIn, y0=it.y0, y1=it.y1, z1=it.z1, xo=(y,z)=>s*half(y,z,RM_WT+0.005);
      const N=4, zi=(y)=>Math.min(z1,zIn(y,Math.abs(xi),SOLE)), vee=zi(y0)>SOLE+1e-6 || zi(y1)>SOLE+1e-6;
      if(!vee) put([[xi,y0,SOLE],[xi,y1,SOLE],[xi,y1,z1],[xi,y0,z1]],'joinery',-0.2,0.02,'tall',[-s,0,0]);
      else for(let i=0;i<N;i++){ const ya=lerp(y0,y1,i/N), yb=lerp(y0,y1,(i+1)/N), tp=rmTidy([[xi,ya,zi(ya)],[xi,yb,zi(yb)],[xi,yb,z1],[xi,ya,z1]]);
        if(tp) put(tp,'joinery',-0.2,0.02,'tall',[-s,0,0]); }
      const ym=(y0+y1)/2, zc=Math.max(SOLE+0.06, zi(ym)+0.04);
      put([[xi-s*0.004,ym-0.006,zc],[xi-s*0.004,ym+0.006,zc],[xi-s*0.004,ym+0.006,z1-0.06],[xi-s*0.004,ym-0.006,z1-0.06]],'cavity',0.2,0.03,'tall',[-s,0,0]);
      put([[xi,y0,z1],[xo(y0,z1),y0,z1],[xo(y1,z1),y1,z1],[xi,y1,z1]],'joinery',-0.6,0.02,'tall',[0,0,1]);
      for(const [y,ny] of [[y0,-1],[y1,1]]){ const z0=zi(y), pts=[[xi,y,z0]], M=6; for(let k=0;k<=M;k++){ const z=lerp(z0,z1,k/M); pts.push([s*Math.max(Math.abs(xi),Math.abs(xo(y,z))),y,z]); } pts.push([xi,y,z1]);
        const tp=rmTidy(pts); if(tp) put(tp,'joinery',-0.35,0.02,'tall',[0,ny,0]); } };
    // an enclosed compartment against the hull side (the cruiser's head): inboard wall with a door, two end walls, a
    // raised floor at floorZ (the plinth under it is the wall's foot) and a vanity; z1 is its top. Where the hull is
    // narrower than xIn the walls start where it reaches xIn
    const compartment=(it,SOLE)=>{ const s=it.side, xi=it.xIn, y0=it.y0, y1=it.y1, z1=it.z1, d=it.door, xo=(y,z)=>s*half(y,z);
      const inb=[-s,0,0], zf=it.floorZ!=null?it.floorZ:SOLE+0.06, zd=Math.min(zf+1.88, z1-0.06), N=3, zi=(y)=>Math.min(z1,zIn(y,Math.abs(xi),SOLE));
      const wall=(ya,yb,zt)=>{ for(let i=0;i<N;i++){ const a=lerp(ya,yb,i/N), b=lerp(ya,yb,(i+1)/N);
        if(zi(a)>=zt-1e-4 && zi(b)>=zt-1e-4) continue;
        const tp=rmTidy([[xi,a,Math.min(zt,zi(a))],[xi,b,Math.min(zt,zi(b))],[xi,b,zt],[xi,a,zt]]); if(tp) put(tp,'lining',-0.1,0.03,'wall',inb); } };
      wall(y0,d.y0,z1); wall(d.y1,y1,z1); wall(d.y0,d.y1,zf);
      put([[xi,d.y0,zd+0.02],[xi,d.y1,zd+0.02],[xi,d.y1,z1],[xi,d.y0,z1]],'lining',-0.1,0.03,'wall',inb);
      put([[xi-s*0.01,d.y0,zf+0.02],[xi-s*0.01,d.y1,zf+0.02],[xi-s*0.01,d.y1,zd],[xi-s*0.01,d.y0,zd]],'joinery',-0.15,0.035,'wall',inb);
      put([[xi,y0,z1+0.003],[xo(y0,z1),y0,z1+0.003],[xo(y1,z1),y1,z1+0.003],[xi,y1,z1+0.003]],'cut',0.5,0.02,'trim',[0,0,1]);
      for(const [y,ny] of [[y0,-1],[y1,1]]){ const z0=zi(y), pts=[[xi,y,z0]], M=6; for(let k=0;k<=M;k++){ const z=lerp(z0,z1,k/M); pts.push([s*Math.max(Math.abs(xi),Math.abs(xo(y,z))),y,z]); } pts.push([xi,y,z1]);
        const tp=rmTidy(pts); if(tp) put(tp,'lining',-0.2,0.03,'wall',[0,-ny,0]); }
      put([[xi,y0,zf],[xo(y0,zf),y0,zf],[xo(y1,zf),y1,zf],[xi,y1,zf]],'lining',-1.4,0.01,'floor',[0,0,1]);
      const vy0=y1-0.34; vblock(s>0?xi+0.08:-3, s>0?3:xi-0.08, vy0, y1-0.02, zf, zf+0.76, 'joinery', -0.2, 'fit'); };
    // a flight of treads, each a solid block to the sole inside the V, with a steel nosing on the edge it is stepped
    // down from (down: +1 when the flight descends toward +y). A tread may carry its own x0 / x1
    const stair=(it,SOLE)=>{ for(const t of it.treads){ const x0=t.x0!=null?t.x0:it.x0, x1=t.x1!=null?t.x1:it.x1;
      vblock(x0,x1,t.y0,t.y1,SOLE,t.z,'joinery',-0.2,'fit',-1.2);
      const ya=it.down>0?t.y1-0.05:t.y0+0.005, yb=ya+0.045, w=Math.max(0.005, rmHalfAtZ((ya+yb)/2,t.z,1)-RM_WT-0.005);
      const a=Math.max(x0,-w)+0.01, c=Math.min(x1,w)-0.01;
      if(c>a) put([[a,ya,t.z+0.003],[c,ya,t.z+0.003],[c,yb,t.z+0.003],[a,yb,t.z+0.003]],'steel',-0.6,0.02,'fit',[0,0,1]); } };
    return { out, half, zIn, put, box, vblock, floor, liner, bulkhead, cabinet, vberth, midberth, shelf, berthfront, stove, sink, toilet, tall, compartment, stair };
  }
  // how far the room's faces stand outside the hull: per vertex, the larger of |x| past the outer skin, z below the
  // outer bottom (both at the station through the vertex, rmU), y forward of the stem at its height, z above the lid
  // over it (topAt), y aft of the room's aft limit (aftAt); worst over all vertices
  function rmFit(faces, topAt, aftAt, tol){
    tol=tol==null?0.01:tol; let worst=0, at=null, outside=0, nv=0;
    const bottomAt=(x,u)=>{ const st=station(u), kw=st.kw, ch=st.ch, ax=Math.abs(x);
      if(ax<=kw) return st.kz; if(ax>=ch) return st.kz+st.cd; return st.kz+st.cd*(ax-kw)/(ch-kw); };
    for(const f of faces) for(const p of f.v){ nv++;
      const [x,y,z]=p, u=rmU(y,z);
      const hull=u==null ? y-rmYAt(1,z) : Math.max(Math.abs(x)-halfAtZ(u,z,0), bottomAt(x,u)-z);
      const e=Math.max(hull, z-topAt(x,y), aftAt?aftAt(x)-y:-1);
      if(e>tol) outside++;
      if(e>worst){ worst=e; at=[q3(x),q3(y),q3(z)]; } }
    return { tol, faces:faces.length, vertices:nv, outside, worst:q3(worst), worstAt:at };
  }

  // ==================================================================================================
  // THE GO-FAST IN THE NEW LAYOUT (ADR 0048 §8 A). Hull id 'go_fast', through byId('go_fast').
  // A CUDDY boat: one room, the cuddy under the foredeck, through a pocket slide door in the console face.
  //   levels  main_deck   the cockpit: sole, liners, the console face and the doorway's lining, the bolsters,
  //                       the throttles, the swim grate and the engine-hatch lid (fit)
  //           below       the cuddy: the room (interiorFaces(), it:true) and the door leaf (doorFaces(t))
  //           foredeck    over the cuddy: its deck (floor), the dash panel (roof), the windscreen (wall, its
  //                       frame trim), the helm pod, gauges, deck hatch, the doorway's dark back, the wheel (fit)
  //           hull        the shell, the deck outside the cuddy's span, the hull's own fittings, the bay and its
  //                       rams (part, drawn only with the lid up) and the sterndrives (part): never lifted
  //   cut     'deck'      cockpit, hatch, side decks and foredeck: nothing lifts
  //           'below'     in the cuddy: the foredeck lifts (its roof, wall and trim come back as ghost); the room shows
  //   parts   the wheel is face group tag 1 (state wheel:false drops it), the sterndrives tag 2 (drive:false);
  //           hatch, steer, trim, tilt and prop are state, as in 10-03; the leaf is doorFaces(t), t 0 shut .. 1 open
  // ==================================================================================================
  const HULL_ID='go_fast', RIG_FILE='goFastIsoRig.js', EXPORT_SYMBOL='GoFastIso', HULL_LABEL='Offshore 39 go-fast';
  // the cuddy (10-03's recreationInteriorRig planGoFast, moved into this rig): sole, bulkheads, berth. The forward
  // bulkhead sits on a deck-chart line (10-03: y 3.10); the step moved inside the aft bulkhead (10-03: y -1.54)
  const GF = { sole:0.26, yBulk:GF_DOORX.yBulk, yFwd:3.06, yB0:-0.55, yB1:2.30, btop:0.62, mattress:0.11 };
  // the lid over the cuddy (outer surface): the dash panel aft of the cockpit front, the foredeck forward of it;
  // the room's ceiling is 0.03 m under the panel, 0.05 m under the deck
  const gfUnderDash=(x,y)=>Math.abs(x)<XF && y<=yFront(Math.abs(x));
  function gfLid(x, y){
    const ax=Math.abs(x);
    if(gfUnderDash(x,y)){ const yt=yFront(ax), u=uOf(yt), t=clamp01((y-(yt-DASH_D))/DASH_D);
      return lerp(Z_DASH, deckZ(u, Math.min(1, ax/xRail(u))), t); }
    const u=uOf(y); return deckZ(u, Math.min(1, ax/xRail(u)));
  }
  const gfCeil=(x,y)=>gfLid(x,y)-(gfUnderDash(x,y)?0.03:0.05);
  // the room's aft limit: the console face's chord inboard of the dash's ends, the aft bulkhead's cap outboard
  function gfConsoleY(x){
    const ax=Math.abs(x), N=14; if(ax>=XF) return GF.yBulk-0.03;
    const k=Math.min(N-1, Math.floor((ax+XF)/(2*XF)*N)), xa=-XF+2*XF*k/N, xb=-XF+2*XF*(k+1)/N;
    return lerp(yFront(xa)-DASH_D, yFront(xb)-DASH_D, (ax-xa)/(xb-xa));
  }
  const gfHalf=(y,z)=>Math.max(0.04, halfAtZ(uOf(y),z,1)-RM_WT-0.005);
  // the livery's deck chart over the cuddy is its lid (foredeck/floor); every other livery face is the hull's shell
  function gfTagLivery(out, deck0){
    for(let i=0;i<out.length;i++){ const f=out[i]; let yc=0; for(const p of f.v) yc+=p[1]; yc/=f.v.length;
      const lid=i>=deck0 && yc>=GF.yBulk && yc<=GF.yFwd;
      f.lv=LV[lid?'foredeck':'hull']; f.ro=RO[lid?'floor':'shell']; f.it=false; }
    return out;
  }

  // state: the 10-03 state object ({hatch, steer, trim, tilt, prop, drive, wheel}) plus scheme / graphic / paint,
  // which pick the livery (its graphic is cut into the deck and topside faces, so each scheme is its own face set);
  // a pose name such as 'stowed' is the default state. tower: none on this hull.
  function faceList(state, tower){ const o=state && typeof state==='object' ? state : {}; return assemble(stateOf(o), graphicOf(o)).F; }
  function partFaces(id, state){
    if(id==='door') return doorFaces(state);
    const tag=({ wheel:1, drive:2 })[id]; return tag ? faceList(state).filter(f=>f.tag===tag) : [];
  }
  // the leaf: the two 10-03 panes, shut at t 0 (vertex for vertex). 0..0.15 backs it 0.036 m off its seat into the
  // pocket; 0.15..1 slides it 0.40 m to port, clear of the doorway. t is a number, or a state with doorOpen.
  function doorFaces(t){
    if(t && typeof t==='object') t=t.doorOpen;
    t=clamp01(+t||0);
    const D=GF_DOORX, dy=D.push*Math.min(1,t/0.15), dx=-D.travel*clamp01((t-0.15)/0.85), out=[];
    for(const s of [-1,1]){
      const v=[[0,gfLeafY(0),D.sillZ],[s*0.20,gfLeafY(0.20),D.sillZ],[s*0.20,gfLeafY(0.20),D.headZ],[0,gfLeafY(0),D.headZ]];
      faceO(out, (dx||dy) ? v.map(p=>[p[0]+dx,p[1]+dy,p[2]]) : v, 'glas', -0.2, 0.01, [0,-1,0]);
    }
    return stampTags(out, 0, out.length, 'below', 'wall', null, false);
  }
  // the room: one dir-free mesh, every face turned to the side it is seen from, tagged below / it:true
  let _room=null;
  function interiorFaces(){
    if(_room) return _room;
    const K=rmKit('below'), S=GF.sole, D=GF_DOORX;
    K.floor(GF.yBulk, GF.yB0, S, S+0.02, 6, 'carpet', -0.6);
    for(const side of [-1,1]) K.liner(side, GF.yBulk, GF.yFwd, S, (y)=>gfCeil(side*K.half(y,1.0), y)-0.01, 18, 'lining');
    // the aft bulkhead (the console's back), flat to the dash's foot, the doorway cut through it
    K.bulkhead(GF.yBulk, S, ()=>Z_DASH, [0,1,0], 'lining', [{ x0:D.x0, x1:D.x1, z0:D.sillZ, z1:D.headZ }]);
    // the forward bulkhead, under the deck's crown
    K.bulkhead(GF.yFwd, S+0.10, (x)=>gfCeil(x, GF.yFwd)-0.01, [0,-1,0], 'lining');
    // the pocket's lid and the aft bulkhead's cap: a section through the console when the lid is lifted
    (function(){ const z=Z_DASH+0.004, w=K.half(GF.yBulk, Z_DASH), xs=[];
      for(let i=0;i<=16;i++) xs.push(-w+2*w*i/16);
      for(let i=0;i<16;i++){ const xa=xs[i], xb=xs[i+1];
        K.put([[xa,gfConsoleY(xa),z],[xb,gfConsoleY(xb),z],[xb,GF.yBulk,z],[xa,GF.yBulk,z]],'cut',0.6,0.03,'trim',[0,0,1]); } })();
    // berth, step, cooler, head, berth-front locker, forepeak shelf (10-03's)
    K.vberth({ y0:GF.yB0, y1:GF.yB1, top:GF.btop, mattress:GF.mattress }, S);
    K.box(-0.22, 0.22, GF.yBulk, GF.yBulk+0.20, S, S+0.20, 'joinery', -0.2, 'fit', -1.2);
    K.cabinet({ side:-1, xIn:-0.30, y0:GF.yBulk+0.24, y1:GF.yB0-0.04, top:S+0.55, topMat:'stone' }, S);
    K.cabinet({ side:1, xIn:0.30, y0:GF.yBulk+0.24, y1:GF.yB0-0.04, top:S+0.42, cushion:true }, S);
    K.berthfront({ y:GF.yB0 }, S, GF.btop);
    K.shelf({ y0:GF.yB1, y1:GF.yFwd, z:GF.btop-0.10, mat:'lining' });
    _room=K.out; return _room;
  }
  function matsFor(id){
    const pal=palette(id && typeof id==='object' ? id : { scheme:id });
    return nlMatsTable(pal.id, Object.assign({}, pal.mats, rmMats(pal.mats, PAINT, 'uph')), RM_MATS);
  }
  function fitCheck(tol){ return rmFit(interiorFaces(), gfLid, gfConsoleY, tol); }
  // the cuddy's standing patch (player-centre), its ceiling and the door
  const gfCuddy=()=>{ const R=NL_WALK.radius; return { x:0.30-R, y0:GF.yBulk+0.20+R, y1:GF.yB0-R }; };
  function gfCuddyCeil(){ const c=gfCuddy(); let z=Infinity;
    for(let i=0;i<=8;i++) for(let j=0;j<=4;j++) z=Math.min(z, gfCeil(lerp(-c.x,c.x,j/4), lerp(c.y0,c.y1,i/8))); return q3(z); }
  function gfCuddyCeilMax(){ const c=gfCuddy(); let z=-Infinity;
    for(let i=0;i<=8;i++) for(let j=0;j<=4;j++) z=Math.max(z, gfCeil(lerp(-c.x,c.x,j/4), lerp(c.y0,c.y1,i/8))); return q3(z); }
  function gfDoor(){ const D=GF_DOORX;
    return { id:'cuddy_door', kind:'slide', face:'aft', y:q3(gfConsoleY(0)), x0:D.x0, x1:D.x1, z0:q3(D.sillZ), z1:q3(D.headZ), sillZ:q3(D.sillZ),
      travel:D.travel, opensTo:'port', clearAt:1.0, leaf:'doorFaces(t)', lv:'below', ro:'wall',
      clear:{ width:q3(D.x1-D.x0), height:q3(D.headZ-D.sillZ) },
      note:'shut until opened; E opens it; t 0..0.15 backs the leaf into the pocket, 0.15..1 slides it to port. The opening is '+
        q3(D.x1-D.x0)+' x '+q3(D.headZ-D.sillZ)+' m: going through waits on duck and crawl (see _excluded cuddy_door).' }; }
  function geometry(){
    const cz=gfCuddyCeil(), zFd=q3(deckZ(uOf(1.0),0));
    return {
      schema:'hidden-harbours/hull-geometry@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:HULL_ID, units:'m', frame:NL_FRAME,
      ids:Object.assign({}, LV), roles:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice(), dwlZ:WATERLINE,
      levels:[
        { id:'main_deck', deck:'cockpit', sole:SOLE, ceil:null, kind:'open', soleZ:SOLE, ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the cockpit, the engine hatch and the swim grate are sky' } },
        { id:'below', deck:'cuddy', sole:GF.sole, ceil:cz, kind:'hard', soleZ:GF.sole, ceilingZ:cz,
          ceiling:{ kind:'hard', lid:'foredeck', z:cz, of:'the foredeck and the dash panel over the cuddy (level foredeck), lowest over the cuddy sole: '+
            q3(cz-GF.sole)+' m clear. It lifts under cut key below.' } },
        { id:'foredeck', deck:'foredeck', sole:zFd, ceil:null, kind:'open', soleZ:zFd, ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'crowned, no rail: boardable (owner ruling, 10-03). soleZ is the crown at y 1.0' } },
      ],
      cutaway:{
        ghost:{ deck:[], below:['foredeck'] },
        gate:{ deck:[], below:['foredeck'] },
        shows:{ deck:null, below:'below' },
        note:'gate lists are complete lift sets in ghost’s vocabulary (whole levels, or level:role). Under below the foredeck lifts whole; '+
          'its roof, wall and trim come back as ghost, its floor and fit (the deck, the pod, the gauges, the wheel, the doorway’s dark back) do not.',
      },
      door:gfDoor(),
      parts:[
        { id:'wheel', group:'tag', tag:1, state:'wheel', lv:'foredeck', ro:'fit', hub:nlV([WHEEL_HUB.x,WHEEL_HUB.y,WHEEL_HUB.z]),
          axis:nlV([0,-Math.cos(WHEEL_GEO.rake*DEG),Math.sin(WHEEL_GEO.rake*DEG)]), rake_deg:WHEEL_GEO.rake, radius:WHEEL_GEO.rad,
          note:'turns about axis through hub (the axis points at the helmsman); wheelFaces(deg) draws it at any angle; its column runs '+WHEEL_GEO.col+' m up the axis into the helm pod' },
        { id:'drive', group:'tag', tag:2, state:'drive', lv:'hull', ro:'part', gimbals:DRIVE.xs.map(x=>nlV([x,DRIVE.yG,DRIVE.zT])),
          note:'twin legs: trim and tilt about the trim pins, steer about the vertical gimbal axis: state trim, tilt, steer, prop' },
        { id:'engine_hatch', group:'state', state:'hatch', lv:'main_deck', ro:'fit', hinge:nlV([0,HATCH.yH,HATCH.zH]), maxDeg:HATCH.maxDeg,
          note:'hatch 0..1 lifts the lid about its aft edge; the bay, the engines and the rams (hull/part) are drawn only while it is up' },
        { id:'door', group:'doorFaces', lv:'below', ro:'wall', kind:'slide', travel:GF_DOORX.travel, opensTo:'port', note:'doorFaces(t): see door' },
      ],
      states:{ hatch:'0..1', steer:'-1..1 (wheel and legs)', trim:'0..1 (default 0.3)', tilt:'0..1', prop:'blade phase, deg', drive:'true|false', wheel:'true|false',
        doorOpen:'0..1 (render option; the leaf is doorFaces(t))' },
    };
  }
  function walk(){
    const R=NL_WALK.radius, S=GF.sole, D=GF_DOORX;
    // the foredeck: from the windscreen's foot to where the bow narrows under the walker. Crowned, so cut into pieces,
    // flat or ramped along x or y, each within 0.02 m of the deck (the deck hatch follows the crown 0.014 m proud: flush)
    const zDeck=(x,y)=>{ const u=uOf(y); return deckZ(u, Math.min(1, Math.abs(x)/xRail(u))); };
    let yTip=3; while(yTip<L/2 && xRail(uOf(yTip+0.01))-R>0.02) yTip+=0.01;
    const yScreen=Y_FC+WS.off;
    const fd=nlSurfaceAreas('foredeck', 'deck', zDeck, (y)=>xRail(uOf(y))-R, q3(yScreen+R), q3(yTip), 0.02);
    // the engine hatch, shut: its crown and hump, in pieces as the foredeck
    const zLid=(x,y)=>{ const u=uOf(y); return lidZ(u, Math.min(1, Math.abs(x)/xRail(u))); };
    const eh=nlSurfaceAreas('engine_hatch', 'deck', zLid, (y)=>xo(uOf(y))-R, q3(Y_H0+R), q3(Y_C0-R), 0.02);
    const c=gfCuddy(), cz=gfCuddyCeil();
    let fdSlope=0; for(let y=yScreen+R; y<=yTip; y+=0.05){ const x=xRail(uOf(y))-R; fdSlope=Math.max(fdSlope, Math.atan(Math.abs(zDeck(x+0.01,y)-zDeck(x-0.01,y))/0.02)/DEG); }
    fd.areas[0].note='the foredeck (pieces foredeck_*) is boardable (owner ruling, 10-03): crowned, no rail; from the windscreen’s foot ('+q3(yScreen)+') to y '+q3(yTip)+
      ', where the bow narrows under the walker. '+fd.areas.length+' pieces in '+fd.slices+' slices, each flat or ramped along x or y and within '+fd.worst+
      ' m of the deck; at the walk’s edge the crown slopes '+Math.round(fdSlope)+'° athwartships. The deck hatch follows the crown 0.014 m proud (flush); the bow cleat (0.03 m) is stepped over.';
    eh.areas[0].note='the engine hatch, shut (hatch 0; pieces engine_hatch_*): the lid stands 0.14 m over the deck, its hump 0.075 m more; '+eh.areas.length+' pieces, each within '+eh.worst+
      ' m. Open, it is not a standing surface. Reached from the cockpit (see _excluded cockpit) or the stern (board).';
    // the cuddy's sole: a floor no adult stands up on, so not a walk area; its outline (player-centre, as an area's),
    // its floor and its deckhead are kept here for the day duck and crawl arrive
    const cuddy={ id:'cuddy_sole', of:'area', cut:'below', z:S, poly:nlRect(-c.x, c.y0, c.x, c.y1), headroom:q3(cz-S), headroomMax:q3(gfCuddyCeilMax()-S),
      reason:'the carpet between the step, the cooler, the head and the berth front, '+q3(cz-S)+' to '+q3(gfCuddyCeilMax()-S)+' m to the deckhead: children stand, adults stoop. '+
        'Reached only through the door (cuddy_door: duck, crawl); standing up waits on duck.', waitsOn:['duck','crawl'] };
    const areas=fd.areas.concat(eh.areas);
    const spawnAt=[0,-4.9], spawn=areas.find(a=>a.cut==='deck' && nlPip(spawnAt, a.poly));
    // the gaps the walker would need in the cockpit, for the record
    const gap={ frontPair:q3(2*(FRONT_X-0.225)), rows:q3((ROW1-0.31)-(REAR+0.23)), fwdOfRow1:q3(gfConsoleY(FRONT_X)-(ROW1+0.23)),
      fwdOfRow1Centre:q3(gfConsoleY(0)-(ROW1+0.23)), aftOfRear:q3((REAR-0.31)-Y_C0), outboard:q3(xo(uOf(REAR))-(REAR_X+0.215)) };
    const _excluded=[
      { id:'cockpit', of:'area', z:SOLE, gaps_m:gap, reason:'the cockpit is the five bolsters. No centre point keeps the walker’s 0.46 m clear of them: '+
        gap.frontPair+' m between the front pair, '+gap.rows+' m between the rows, '+gap.fwdOfRow1+'-'+gap.fwdOfRow1Centre+' m forward of the front row, '+
        gap.aftOfRear+' m aft of the rear row, '+gap.outboard+' m outboard. The seats are taken seated (drive at the helm, sit at the others); '+
        'walking through waits on the owner’s call on the walker (CHANGES.md, question 3).', waitsOn:['sit'] },
      { id:'side_deck_port', of:'area', reason:'the side deck beside the hatch and the cockpit, '+SIDE+' m from the cap to the coaming (10-03: 6 of 30 builds fit it): '+
        'narrower than the walker’s 0.46 m. It is the washboard (washboards[]): climbed onto with Space and walked by the washboard’s own rule.' },
      { id:'side_deck_stbd', of:'area', reason:'as side_deck_port.' },
      { id:'swim_grate', of:'area', z:q3(GRATE.z), reason:'0.46 m deep: no depth left for the walker’s centre. It stands '+q3(GRATE.z-WATERLINE)+
        ' m above the waterline with no ladder, so coming aboard from the water waits on ladderOn and a ladder (question 4). From a quay the stern is boarded onto the engine hatch.',
        waitsOn:['ladderOn'] },
      { id:'aft_deck', of:'area', reason:'the deck strip aft of the engine hatch, '+q3(Y_H0+L/2)+' m: a step between the grate and the hatch, not a standing surface.' },
      { id:'cuddy_door', of:'door', at:nlPt({ x:0, y:gfConsoleY(0), z:(D.sillZ+D.headZ)/2 }), reason:'the doorway is '+q3(D.x1-D.x0)+' m wide and '+q3(D.headZ-D.sillZ)+
        ' m high (sill '+q3(D.sillZ)+', head '+q3(D.headZ)+'): narrower than the walker and lower than any 10.3 build’s crouch. Going below is a duck and a crawl through it, '+
        'then down the step ('+q3(D.sillZ-(S+0.20))+' + 0.20 m) to the cuddy sole.', waitsOn:['duck','crawl'] },
      { id:'cuddy_step', of:'area', z:q3(S+0.20), reason:'the step inside the door, 0.44 m wide and 0.20 m deep: narrower than the walker; part of the way below (cuddy_door).',
        waitsOn:['crawl'] },
      cuddy,
      { id:'vberth', of:'berth', z:GF.btop, reason:'the V-berth: a bed under '+q3(gfCeil(0,0.4)-GF.btop)+' m of deckhead. Getting onto it waits on crawl; sleeping is the bunk interact.',
        waitsOn:['crawl'] },
      { id:'head', of:'seat', at:nlPt({ x:(0.30+gfHalf(-0.925,S+0.42))/2, y:-0.925, z:S+0.42 }), reason:'the head’s seat box: sitting waits on sit.', waitsOn:['sit'] },
      { id:'seat_codriver', of:'seat', at:nlPt(CODRIVER), reason:'a bolster: sitting waits on sit (10.3 has no sit clip).', waitsOn:['sit'] },
      { id:'seat_rear_port', of:'seat', at:nlPt(SEATS[2]), reason:'as seat_codriver.', waitsOn:['sit'] },
      { id:'seat_rear_centre', of:'seat', at:nlPt(SEATS[3]), reason:'as seat_codriver.', waitsOn:['sit'] },
      { id:'seat_rear_stbd', of:'seat', at:nlPt(SEATS[4]), reason:'as seat_codriver.', waitsOn:['sit'] },
      { id:'stern_eyes', of:'cleat', at:nlPt(STERN_EYES[0]), reason:'the transom’s two tie-down eyes (anchors.sternEyes) are trailer and ski-tow eyes, not mooring cleats (10-03).' },
    ];
    const inCuddy=(x,y)=>({ x:q3(x), y:q3(y), z:S });
    const yRows=((ROW1-0.31)+(REAR+0.23))/2;   // between the bolster rows: where a figure stands to take a rear seat
    const interact=[
      { id:'helm', action:'enter_helm', label:'Helm', prompt:'Take the wheel', at:nlPt(WHEEL_HUB), seat:nlPt(HELM), stand:nlPt(PILOT), reach:nlPt(PILOT), area:null, offWalk:'cockpit',
        note:'taken seated (drive: 30 of 30 on 10.3); the cockpit has no walk area at the walker’s 0.46 m (see _excluded cockpit). Standing: 0 of 30 (see the fit)' },
      { id:'seat_codriver', action:'enter_ride', label:'Bolster', prompt:'Ride', at:nlPt(CODRIVER), reach:nlPt({ x:CODRIVER.x, y:PILOT.y, z:SOLE }), area:null, offWalk:'cockpit', waitsOn:['sit'] },
      { id:'seat_rear_port', action:'enter_ride', label:'Bolster', prompt:'Ride', at:nlPt(SEATS[2]), reach:nlPt({ x:SEATS[2].x, y:yRows, z:SOLE }), area:null, offWalk:'cockpit', waitsOn:['sit'] },
      { id:'seat_rear_centre', action:'enter_ride', label:'Bolster', prompt:'Ride', at:nlPt(SEATS[3]), reach:nlPt({ x:SEATS[3].x, y:yRows, z:SOLE }), area:null, offWalk:'cockpit', waitsOn:['sit'] },
      { id:'seat_rear_stbd', action:'enter_ride', label:'Bolster', prompt:'Ride', at:nlPt(SEATS[4]), reach:nlPt({ x:SEATS[4].x, y:yRows, z:SOLE }), area:null, offWalk:'cockpit', waitsOn:['sit'] },
      { id:'engine', action:'open_close_engine', label:'Engine hatch', prompt:'Open the engine hatch', state:'hatch', at:nlPt({ x:0, y:-4.675, z:lidZ(uOf(-4.675),0) }), reach:{ x:0, y:q3(REAR+0.23+R), z:SOLE }, area:null, offWalk:'cockpit',
        note:'the lid lifts about its aft edge, so it is opened from the cockpit side (10-03 reach); the cockpit has no walk area (see _excluded cockpit)' },
      { id:'cuddy_door', action:'open_close', label:'Cuddy door', prompt:'Open · shut', state:'doorOpen', at:nlPt({ x:0, y:gfConsoleY(0), z:(D.sillZ+D.headZ)/2 }), reach:{ x:0, y:q3(gfConsoleY(0)-R), z:SOLE }, area:null, offWalk:'cockpit',
        note:'E opens and shuts the leaf (doorFaces(t)); going through waits on duck and crawl', waitsOn:['duck','crawl'] },
      { id:'bunk', action:'sleep', label:'V-berth', prompt:'Sleep · save', at:nlPt({ x:0, y:(GF.yB0+GF.yB1)/2, z:GF.btop }), reach:inCuddy(0,c.y1), area:null, offWalk:'cuddy_sole', waitsOn:['duck','crawl'] },
      { id:'cooler', action:'storage', label:'Icebox', prompt:'Store · retrieve', at:nlPt({ x:-(0.30+gfHalf(-0.925,S+0.55))/2, y:-0.925, z:S+0.55 }), reach:inCuddy(0,-0.925), area:null, offWalk:'cuddy_sole', waitsOn:['duck','crawl'] },
      { id:'head', action:'toilet', label:'Head', prompt:'Use the head', at:nlPt({ x:(0.30+gfHalf(-0.925,S+0.42))/2, y:-0.925, z:S+0.42 }), reach:inCuddy(0,-0.925), area:null, offWalk:'cuddy_sole', waitsOn:['duck','crawl','sit'] },
      { id:'locker', action:'storage', label:'Under-berth locker', prompt:'Store · retrieve', at:nlPt({ x:0, y:GF.yB0-0.004, z:(S+0.06+GF.btop-0.16)/2 }), reach:inCuddy(0,c.y1), area:null, offWalk:'cuddy_sole', waitsOn:['duck','crawl'] },
    ];
    // 10-03's ids and types (build(): the stern cleats on the side decks, the bow cleat, the stem eye)
    const sternCleat=(s)=>{ const u=uOf(-5.55), x=xRail(u)-0.12; return nlV([s*x, yOf(u), deckZ(u,x/xRail(u))+0.014]); };
    const cleats=[
      { id:'stern_port', type:'cleat', pos:sternCleat(-1) },
      { id:'stern_star', type:'cleat', pos:sternCleat(1) },
      { id:'bow_1', type:'cleat', pos:nlV([0, yOf(0.955), deckZ(0.955,0)+0.016]) },
      { id:'bow_eye', type:'bow_eye', pos:nlV([PAINTER.x,PAINTER.y,PAINTER.z]), note:'the painter and trailer eye on the stem' },
    ];
    const edge=(side)=>{ const e=[]; for(let i=0;i<=10;i++){ const y=lerp(Y_H0,Y_F0,i/10), u=uOf(y); e.push(nlV([side*xRail(u), y, capZ(u)])); } return e; };
    const washboards=[
      { side:'port', width_m:SIDE, outer_edge:edge(-1), note:'the side deck from the stern to the windscreen’s wing: climbed onto with Space' },
      { side:'starboard', width_m:SIDE, outer_edge:edge(1), note:'as port' },
    ];
    return {
      schema:'hidden-harbours/hull-walk@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:HULL_ID, label:HULL_LABEL,
      kind:'cabin', units:'metres', frame:NL_FRAME,
      loa_m:q3(L+0.46), loh_m:L, beam_m:q3(2*T[2][1]), DWL:WATERLINE, cockpitZ:SOLE,
      cell:{ W, H, pivot:{ x:cx, y:cy }, pxPerM:PX, defaultElev:DEFAULT_ELEV, order:['N','NE','E','SE','S','SW','W','NW'] },
      tags:{ lv:Object.assign({}, LV), ro:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice() },
      walk:{ radius:R, stepTol:NL_WALK.stepTol, rule:NL_RULE, spawn:{ x:spawnAt[0], y:spawnAt[1], area:spawn?spawn.id:null,
        note:'on the engine hatch: the cockpit has no walk area at the walker’s 0.46 m' }, areas },
      house:Object.assign({ door:gfDoor(), room:{ level:'below', soleZ:S, ceilingZ:cz, fitCheck:'qa.fitCheck' } }, nlHouseDecks(geometry().levels)),
      anchors:{ helm:nlPt(HELM), wheel:nlPt(WHEEL_HUB), pilot:nlPt(PILOT), seats:SEATS.map(s=>Object.assign({ id:s.id }, nlPt(s))),
        painter:nlPt(PAINTER), sternEyes:STERN_EYES.map(nlPt), grate:nlPt(GRATE), hinge:nlPt(HINGE), driveMounts:DRIVE_MOUNTS.map(nlPt),
        berths:[ nlBerth('vberth', 'bunk', 'vberth', GF.yB0, GF.yB1, GF.btop, Math.min(gfCeil(0,GF.yB0+0.3), gfCeil(0,(GF.yB0+GF.yB1)/2), gfCeil(0,GF.yB1-0.3))) ] },
      interact, cleats, washboards, _excluded,
      rock:{ frames:ROCK.frames, rollA:ROCK.rollA, pitchA:ROCK.pitchA, heaveA:ROCK.heaveA, period:ROCK.period },
      qa:{ fitCheck:fitCheck(), surfaceFit:{ foredeck:fd.worst, engine_hatch:eh.worst, tol:0.02 } },
    };
  }
  const HULL={ id:HULL_ID, label:HULL_LABEL, kind:'cabin', faceList, partFaces, interiorFaces, doorFaces, geometry, matsFor, walk, fitCheck, render, renderCut,
    GAIN, BIAS, LN, BAYER, PX, W, H, pivot:{ x:cx, y:cy }, defaultElev:DEFAULT_ELEV, KEY,
    schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, DWL:WATERLINE, cockpitZ:SOLE, loa:L+0.46 };
  const HULLS={ go_fast:HULL };
  function byId(id){ return HULLS[id]||null; }

  root.GoFastIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV, KEYLINE_DEFAULT,
    order:['N','NE','E','SE','S','SW','W','NW'],
    render, renderWheel, renderDrive, bounds, allFaces:(o)=>assemble(stateOf(o||{}), graphicOf(o||{})).F, wheelFaces, WHEEL_GEO, WHEEL_LOCK, WHEEL_HUB,
    ROCK, rock:rockMotion, PLANE, plane:planeMotion, TURN, turn:turnMotion, addPose:posesAdd,
    DRIVE, HATCH, SPEC, stateOf,
    helmSeat:_anchor(HELM), codriverSeat:_anchor(CODRIVER), wheelHub:_anchor(WHEEL_HUB), pilotStand:_anchor(PILOT),
    driveMounts:many(DRIVE_MOUNTS), painter:_anchor(PAINTER), sternEyes:many(STERN_EYES), grate:_anchor(GRATE),
    hinge:_anchor(HINGE), seats:many(SEATS),
    HELM, CODRIVER, SEATS, PILOT, DRIVE_MOUNTS, PAINTER, STERN_EYES, GRATE, HINGE,
    L, LOA:L+0.46, BEAM:2*T[2][1], SOLE, WATERLINE, station,
    SCHEMES, schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, SLOTS,
    GRAPHICS, graphicIds, palette, rampFrom, chipWall, C_CAP, LIVERY_CAP,
    PAINT, STEEL, GLAS, MOTO, KEY,
    // the new layout (ADR 0048 §8 A): one hull, through byId('go_fast'); the same names for the default hull
    byId, HULLS, hullIds:Object.keys(HULLS), defaultHull:HULL_ID, LV, RO, GHOST_ROLES,
    faceList, partFaces, interiorFaces, doorFaces, geometry, matsFor, MATS:matsFor(DEFAULT_SCHEME).MATS, walk, fitCheck, renderCut,
    GAIN, BIAS, LN, BAYER, cell:{ W, H, pivot:{ x:cx, y:cy }, PX, defaultElev:DEFAULT_ELEV } };
})(typeof globalThis!=='undefined'?globalThis:window);

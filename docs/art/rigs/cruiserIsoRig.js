/* Hidden Harbours — EXPRESS CRUISER, THE NEW LAYOUT (2026-10-09). ADR 0048 §8 A; the contract is
   docs/art/hull-layout-spec.md. One rig, one hull, through its global:
     CruiserIso.byId('express_cruiser') = { faceList(state,tower), partFaces(id,state), interiorFaces(),
       doorFaces(t), geometry(), matsFor(scheme), walk(), fitCheck(), GAIN, BIAS, LN, BAYER, PX, W, H,
       pivot, defaultElev, schemeIds, defaultScheme, DWL, cockpitZ, loa }
   and the same names on the global for the default hull. Every face carries lv (its level), ro (its
   role) and it (true only on the room's faces). One full mesh, outside and in: the hull, the cabin
   inside it (10-03's interior rig, moved in), the companionway door's leaf and the walk, all from this
   file. Her walk, docs/art/rigs/gameplay/cruiserIsoRig.express_cruiser.walk.json, is WRITTEN by
   tools/rig-recipes/hull-walk.mjs from walk(), never by hand, and stamped with this file's LF sha256.
   WHAT MOVED FROM THE 10-03 PASS (CHANGES.md lists each, old -> new):
     - the wheel: hub (0.52,-0.619,1.62) rake +25 -> (0.52,-1.14,1.96) rake -20, its column along the
       axis into a new helm pod on the dash (x 0.43-0.61): seated, 30 of 30 builds on 10.3
     - the helm and companion seats: 1.02 wide on x 0.69 -> 0.94 on x 0.73, so the aisle between them
       is 0.52 m (10-03: 0.36, narrower than the walker)
     - the companionway door: drawn in the assembly -> the leaf, doorFaces(t), vertex for vertex
     - the cabin: inside her hull (fitCheck), where 10-03's stood outside it at the forefoot and the
       turn of the bilge (1,451 px over 7 facings); the berths, the head and the stair moved to fit
   Every other exterior face is the 10-03 face, vertex for vertex, in every scheme.
   The header below is the 10-03 rig's own, unchanged. */
/* Hidden Harbours — parametric ISO EXPRESS CRUISER (31-ft sterndrive cabin cruiser, hardtop) — M2 bake
   recipe, ADR-0006, same pipeline and rasterizer as bowriderIsoRig.js / goFastIsoRig.js. PASS 2: the
   sweep, the cut-away windscreen and the arch legs redrawn to the photos (pass 1: cruiserIsoRig.pass1.js).
   Built to the first two photos of the express-cruiser reference set (one boat, one length): a 9.45 m
   hull + 0.75 m integrated swim platform, 3.05 m beam, 21 deg deadrise at the transom. Navy topsides
   run to a thin white sheer band at the stem; their top edge is one convex curve that dips to the boot
   line at u 0.10, so the topsides aft are a white wedge carrying four raked engine-air gills; three oval ports a side in the navy; a crowned foredeck over the cabin between
   non-skid side decks, a hatch, a bow rail round to the anchor pulpit; a strongly raked wraparound
   windscreen at half height, cut away at the sides, and a hardtop on two forward-raked arch legs; twin V8s under the cockpit sole driving twin duoprop
   sterndrives. 32 px = 1 m. Fixed 3/4 turntable camera (elev 40), 45 deg steps, flat facets, the
   fleet's upper-left key, z-buffered, ordered dither, NO AA, ringless from birth (ADR 0031:
   render(dir,{outline:true}) is the A/B).

   WHAT SHE CARRIES, stern to stem
     swim platform   integrated, chamfered corners, flush ladder lid; the legs sit under it
     stern           the top-aft corner is an elliptical fillet FIL.R long: the sheer rolls down to FIL.z0 at
                     the transom, so the stern slopes to the platform in profile, as in the photos
     transom door    a channel through the stern moulding to starboard, the door at its forward end; gate
                     0..1 swings it aft down the channel toward the platform (92 deg)
     aft bench       recessed into the stern moulding, from the port side to the walk-through
     engine hatch    the aft cockpit sole: hatch 0..1 lifts it on its aft hinge to 45 deg on two rams
                     and builds the bay — twin V8s (flame arrestors, valve covers, manifolds, risers),
                     a genset and two batteries forward
     helm deck       one step up (0.22 m) over the mid-cabin: double helm seat to starboard, double
                     companion seat to port, a wet bar behind the companion seat
     helm            black dash panel, two screens, throttles; the wheel is a TAGGED LAYER (tag 1)
     companionway    to port of the helm in the dash bulkhead; door 0..1 swings it aft through 95 deg
                     and shows the step down into the cabin
     windscreen      raked wraparound, dark tint, HALF HEIGHT (WS_H 0.5 of the rake up to the roof line): it
                     stops clear of the roof so the cockpit shows under it face on. Cut away at the sides:
                     a glass wing whose top edge sweeps from the corner post down to the arch foot
     arch legs       one a side, raked forward: a concave leading edge from the roof down to the coaming,
                     a tinted window through the upper half
     hardtop         a TAGGED LAYER (tag 3): the roof, its sunroof, the radome and the whip. sunroof 0..1
                     slides the tinted panel aft on its rails. Bake the hull with {roof:false} and draw
                     renderRoof over it, so the game can fade the roof off a figure under it; the roof's
                     shade stays on the deck either way
     foredeck        crowned cabin top between non-skid side decks: hatch, windlass, locker, cleats, the
                     bow rail on six stanchions a side meeting over the pulpit and its anchor
     drives          twin sterndrives as ONE tagged layer (tag 2): both legs trim, tilt and steer together

   COLOURWAYS. Paint is data, as on the bowrider: render(dir,{scheme}) picks a preset;
   render(dir,{paint:{hull,deck,stripe,bottom,uph,acc,sole}}) mixes any base colours through the fleet
   OKLCH mixer (rampFrom, chipWall, C_CAP 0.115). hull is the topside band from the boot line up to the
   sweep; deck the white above it and every moulding; stripe the pin on the sweep; uph/acc the vinyl;
   sole the cockpit teak. The seven fleet schemes carry the bowrider's hull, pin and vinyl values.

   HULL BANDS. Every band edge is a real polygon edge and every strip is split at the chine and the
   knuckle, so a band that crosses a crease keeps it (the boot line runs above the chine aft and below
   it forward; the sweep crosses the knuckle aft). Aft of the sweep's tip the paint and pin bands close
   to nothing and their strips are skipped.

   HULL-SKIN WINDING follows bowriderIsoRig.js (so sportSkiffIsoRig.js) vertex for vertex; every other
   face is wound by an explicit outward vector (faceO).

   Cell 400x360, pivot (200,196) = boat origin (amidships of the 9.45 m hull, keel bottom, centreline),
   pinned every heading. Swept over elev 30/40/50 x 8 headings x rock, plane, hard over and their sums
   x every part open and trailer tilt: 2 px or more clear of every edge. bounds(dir,opts) returns the
   projected box. Exposes globalThis.CruiserIso. */
(function (root) {
  const PX = 32, S = 32;
  const W = 400, H = 360, cx = 200, cy = 196;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;

  // ---- poses: 5.3 t on a 3.05 m beam — slower in roll than the bowrider, soft in pitch -------------
  const ROCK = { frames: 8, rollA: 3.0, pitchA: 1.5, heaveA: 1.3, period: 3.1 };
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a), yaw: 0 };
  }
  const PLANE = { frames: 12, humpAt: 0.38, humpPitch: 7.4, cruisePitch: 3.6, lift: 7 };
  function planeMotion(t){
    t = Math.max(0, Math.min(1, t||0));
    const hump = t < PLANE.humpAt ? Math.sin(0.5*Math.PI*t/PLANE.humpAt) : Math.pow(Math.max(0, 1-(t-PLANE.humpAt)/(1-PLANE.humpAt)), 1.5);
    const pitch = PLANE.cruisePitch*t + (PLANE.humpPitch - PLANE.cruisePitch*PLANE.humpAt)*hump;
    return { roll: 0, pitch, heave: PLANE.lift*t*t - 2.6*t*(1-t), yaw: 0 };
  }
  const TURN = { maxRoll: 10, maxYaw: 6, pitch: 3.0, heave: 4 };
  function turnMotion(s){
    s = Math.max(-1, Math.min(1, s||0));
    return { roll: s*TURN.maxRoll, pitch: TURN.pitch, heave: TURN.heave, yaw: -s*TURN.maxYaw };
  }
  const posesAdd = (a,b)=>({ roll:(a.roll||0)+(b.roll||0), pitch:(a.pitch||0)+(b.pitch||0),
                             heave:(a.heave||0)+(b.heave||0), yaw:(a.yaw||0)+(b.yaw||0) });

  // ---- fixed ramps (never painted) ---------------------------------------------------------------
  const WHITE = '#eef0ea';
  const PAINT = ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];
  const GLAS  = ['#16333c','#24505a','#3a7680','#5fa3a6','#8fc9c4'];
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];
  const KEY   = '#101a19';

  // ---- paint mixer (OKLCH) — the fleet's shared envelope, value for value -------------------------
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

  // ---- colourways ---------------------------------------------------------------------------------
  const TEAK = '#9b7b56';
  const SCHEMES = {
    'express-navy':  { name:'Express Navy',  hull:'#1f3858', deck:WHITE, stripe:'#b4bcbf', bottom:WHITE, uph:'#ebe5d3', acc:'#1f3858', sole:TEAK,
                       note:'the reference boat \u2014 navy topsides sweeping down into a white wedge aft, silver pin, teak sole' },
    'harbour-white': { name:'Harbour White', hull:WHITE, deck:WHITE, stripe:'#2ba39a', bottom:WHITE, uph:'#e3dcc8', acc:'#2ba39a', sole:TEAK,
                       note:'the fleet scheme \u2014 white glass, teal pin' },
    'cranberry':     { name:'Cranberry',     hull:'#a8452f', deck:WHITE, stripe:'#2a2f33', bottom:WHITE, uph:'#ece6d6', acc:'#a8452f', sole:TEAK,
                       note:'the fleet\u2019s bog red, black pin' },
    'tar-black':     { name:'Tar Black',     hull:'#2a2f33', deck:WHITE, stripe:'#b9bfc0', bottom:WHITE, uph:'#e6e3da', acc:'#3d454b', sole:TEAK,
                       note:'the fleet\u2019s tar value, silver pin' },
    'champagne':     { name:'Champagne',     hull:'#c9b186', deck:WHITE, stripe:'#6b4f2a', bottom:WHITE, uph:'#efe8d6', acc:'#b39468', sole:TEAK,
                       note:'tan topsides, brown pin' },
    'seafoam':       { name:'Seafoam',       hull:'#a3d9c8', deck:WHITE, stripe:'#1c7367', bottom:WHITE, uph:'#e8e3d3', acc:'#2b8f86', sole:TEAK,
                       note:'the sport skiff\u2019s mint, deep teal pin' },
    'bottle-green':  { name:'Bottle Green',  hull:'#31694c', deck:WHITE, stripe:'#e0b13a', bottom:WHITE, uph:'#e2dcc7', acc:'#31694c', sole:TEAK,
                       note:'shared with the workboats, value for value' },
    'squall-grey':   { name:'Squall Grey',   hull:'#8a99a1', deck:WHITE, stripe:'#22354a', bottom:WHITE, uph:'#d9dcd6', acc:'#22354a', sole:TEAK,
                       note:'the punt\u2019s grey-blue, navy pin' },
  };
  const DEFAULT_SCHEME = 'express-navy';
  const SLOTS = ['hull','deck','stripe','bottom','uph','acc','sole'];

  const _pal={}; let _palOrder=[];
  function palette(o){
    o=o||{};
    const DF=SCHEMES[DEFAULT_SCHEME];
    const base = o.paint || SCHEMES[o.scheme] || DF;
    const deck=base.deck||DF.deck, hull=base.hull||DF.hull, stripe=base.stripe||DF.stripe;
    const bottom=base.bottom||deck, uph=base.uph||DF.uph, acc=base.acc||hull, sole=base.sole||DF.sole;
    const key=[hull,deck,stripe,bottom,uph,acc,sole].join('|');
    if(_pal[key]) return _pal[key];
    const r7=(hex)=>hex.toLowerCase()===WHITE ? PAINT : rampFrom(hex,7);
    const deckR=r7(deck);
    const hullR=hull===deck ? deckR : r7(hull);
    const botR=bottom===deck ? deckR : (bottom===hull ? hullR : r7(bottom));
    const stripeR=rampFrom(stripe,5,{anchor:0.75});
    const uphR=rampFrom(uph,5,{anchor:0.72});
    const accR=rampFrom(acc,5,{anchor:0.72});
    const soleR=rampFrom(sole,5,{anchor:0.70});
    const mats = { paint:{ramp:hullR,off:0,dith:0},       deck:{ramp:deckR,off:0,dith:0},
                   nonskid:{ramp:deckR,off:-1,dith:0.22}, liner:{ramp:deckR,off:0,dith:0},
                   stripe:{ramp:stripeR,off:-1,dith:0},   bottom:{ramp:botR,off:0,dith:0},
                   uph:{ramp:uphR,off:0,dith:0.12},       acc:{ramp:accR,off:0,dith:0.10},
                   sole:{ramp:soleR,off:0,dith:0.30},     steel:{ramp:STEEL,off:0,dith:0},
                   glas:{ramp:GLAS,off:-2,dith:0},        side:{ramp:GLAS,off:-1,dith:0},
                   moto:{ramp:MOTO,off:0,dith:0},         blk:{ramp:MOTO,off:-2,dith:0},
                   drv:{ramp:MOTO,off:1,dith:0},          eng:{ramp:MOTO,off:0,dith:0} };
    const rindex={};
    [hullR,deckR,botR,stripeR,uphR,accR,soleR,STEEL,GLAS,MOTO,PAINT].forEach(r=>r.forEach((c,i)=>{ if(!rindex[c]) rindex[c]={r,i}; }));
    const out={ key, id:o.paint?'custom':(SCHEMES[o.scheme]?o.scheme:DEFAULT_SCHEME), name:base.name||'Custom',
                base:{hull,deck,stripe,bottom,uph,acc,sole}, note:base.note||'',
                ramps:{hull:hullR,deck:deckR,stripe:stripeR,bottom:botR,uph:uphR,acc:accR,sole:soleR}, mats, rindex };
    _pal[key]=out; _palOrder.push(key);
    if(_palOrder.length>32){ delete _pal[_palOrder.shift()]; }
    return out;
  }

  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- the hull -----------------------------------------------------------------------------------
  // [u, sheerHalf, chineHalf, keelHalf, chineRise, depth, keelZ] (m). Deadrise at the transom
  // atan(0.47/(1.30-0.06)) = 20.8 deg; beam 3.05 m at u 0.375; sheer 1.66 aft to 2.22 at the stem head.
  const L = 9.45, TH = 0.06, NSEG = 36, NI = 26, WL = 0.58;
  const T = [
    [0.000, 1.430,1.300,0.060,0.470,1.640,0.020],
    [0.125, 1.490,1.330,0.060,0.490,1.670,0.010],
    [0.250, 1.515,1.340,0.055,0.520,1.700,0.000],
    [0.375, 1.525,1.320,0.050,0.560,1.740,0.000],
    [0.500, 1.500,1.250,0.045,0.610,1.800,0.000],
    [0.625, 1.420,1.100,0.040,0.660,1.860,0.020],
    [0.750, 1.240,0.860,0.040,0.700,1.890,0.090],
    [0.840, 1.020,0.640,0.040,0.680,1.850,0.200],
    [0.920, 0.720,0.400,0.035,0.600,1.740,0.380],
    [0.970, 0.420,0.220,0.030,0.480,1.630,0.550],
    [1.000, 0.120,0.090,0.030,0.360,1.520,0.700],
  ];
  const FC = 0.34, FK = 0.62, KB = 0.05, KB_MAX = 0.07;
  const lerp=(a,b,t)=>a+(b-a)*t;
  const smooth=(t)=>{ t=clamp01(t); return t*t*(3-2*t); };
  // the stern: the top-aft corner is an elliptical fillet FIL.R long, rolling from the sheer down to
  // FIL.z0 at the transom (tangent to the transom there and to the sheer at U_F), so the stern slopes
  // down to the platform in profile
  const FIL = { R:0.525, z0:1.02 }, U_F = FIL.R/L;
  function station(u){
    u=Math.max(0,Math.min(1,u));
    let i=0; while(i<T.length-2 && u>T[i+1][0]) i++;
    const A=T[i], B=T[i+1], fr=(u-A[0])/(B[0]-A[0]), kz=lerp(A[6],B[6],fr);
    let dep=lerp(A[5],B[5],fr);
    if(u<U_F){ const zf=FIL.z0-0.012, zs=kz+dep; dep=zf+(zs-zf)*Math.sin(Math.acos(1-u/U_F))-kz; }
    return { ws:lerp(A[1],B[1],fr), ch:lerp(A[2],B[2],fr), kw:lerp(A[3],B[3],fr),
             cd:lerp(A[4],B[4],fr), dep, kz, y:-L/2+u*L };
  }
  const RAKE=0.95;
  const rakeAt=(u,frac)=>-RAKE*Math.pow(Math.max(0,(u-0.72)/0.28),1.5)*(1-Math.min(1,frac));
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
  const yAt=(u,z)=>{ const st=station(u); return st.y+rakeAt(u,(z-st.kz)/Math.max(1e-6,st.dep)); };

  // deck cap, cockpit opening, floors
  const RUBIN = TH*0.76;
  const capW=(u)=>0.19+0.05*smooth((u-0.62)/0.28);
  const xOut=(u)=>skin(1,u,1)[0];
  const capZ=(u)=>sheerZ(u)+0.012;
  const xo=(u)=>Math.max(0.04, xOut(u)-capW(u));
  const xl=(u,z)=>Math.min(xo(u), halfAtZ(u,z,1));
  const xEdge=(u)=>Math.max(0.012, xOut(u)-RUBIN);
  const SOLE=0.95, HDECK=1.17, Y_STEP=-2.35, Y_TR=-L/2+0.105;
  const U_TR=uOf(Y_TR), U_STEP=uOf(Y_STEP);
  const floorZ=(u)=>yOf(u)<Y_STEP ? SOLE : HDECK;
  // the foredeck: a flat non-skid side deck SD wide along the sheer, the cabin top crowned inboard of it
  const SD=0.26;
  const crown=(u)=>0.33*Math.max(0,1-Math.pow((u-0.5)/0.5,2));
  const deckZ=(u,x)=>{ const e=xEdge(u), c=Math.max(0.05,e-SD), q=Math.min(1,Math.abs(x)/c); return capZ(u)+crown(u)*Math.max(0,1-q*q); };

  // colour: the navy runs up to a thin white sheer band forward and sweeps down aft into a white wedge
  const zBoot=()=>WL+0.06;
  // the sweep: one convex curve, 0.92 of the topside at the stem easing to 0.70 at the knee (u 0.42),
  // then diving on a parabola to the boot line at U_END; the pin rides it and tapers out at the tip
  const U_END=0.10, U_KNEE=0.42;
  const navyFrac=(u)=>{ if(u<=U_END) return 0; const t=clamp01((u-U_END)/(U_KNEE-U_END));
    return (0.70+0.22*Math.pow(clamp01((u-U_END)/(1-U_END)),1.3))*(1-(1-t)*(1-t)); };
  const zCol=(u)=>zBoot()+navyFrac(u)*(sheerZ(u)-zBoot());
  const zPin=(u)=>zCol(u)+0.032*smooth((u-U_END)/0.06);
  const F_BOOT=(u)=>fracAtZ(station(u), zBoot(u));
  const F_COL=(u)=>Math.max(F_BOOT(u), fracAtZ(station(u), zCol(u)));
  const F_PIN=(u)=>Math.min(0.985, Math.max(F_COL(u), fracAtZ(station(u), zPin(u))));
  const B_BOT=(u)=>-0.95+0.55*Math.pow(Math.max(0,(u-0.74)/0.26),1.3);
  const BANDS = [ [()=>0, F_BOOT, 'bottom', B_BOT, 0],
                  [F_BOOT, F_COL, 'paint', 0, 0],
                  [F_COL, F_PIN, 'stripe', 0.3, 0.004],
                  [F_PIN, ()=>1, 'deck', 0.15, 0] ];
  const fv=(f,u)=>typeof f==='function'?f(u):f;
  const bv=(b,u)=>typeof b==='function'?b(u):(b||0);

  // ---- generic solids ---------------------------------------------------------------------------
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
  const zFloorAt=(u,x,z0)=>{
    if(xl(u,z0)>=x) return z0;
    let lo=z0, hi=capZ(u);
    for(let k=0;k<18;k++){ const m=(lo+hi)/2; if(xl(u,m)>=x) hi=m; else lo=m; }
    return hi;
  };
  function wallPoly(u, xa, za, zb, side, inset, yfn){
    inset=inset||0;
    const X=(z)=>Math.max(xa, xl(u,z)-inset), Y=(z)=>yfn?yfn(z):yOf(u);
    const z0=Math.min(zb, zFloorAt(u, xa+inset, za)), N=5, raw=[[side*xa, Y(z0), z0]];
    for(let k=0;k<=N;k++){ const z=lerp(z0,zb,k/N); raw.push([side*X(z), Y(z), z]); }
    raw.push([side*xa, Y(zb), zb]);
    const pts=[];
    for(const p of raw){ const q=pts[pts.length-1]; if(!q || Math.abs(q[0]-p[0])+Math.abs(q[1]-p[1])+Math.abs(q[2]-p[2])>1e-5) pts.push(p); }
    return pts;
  }

  // ---- plan constants shared by static and moving parts -------------------------------------------
  const PL = { y0:-L/2, y1:-L/2-0.75, zt:0.90, zb:0.78, hw:1.30, ch:0.22 };
  const GATE = { x0:0.62, x1:1.22, y:-4.23, z0:SOLE+0.01, z1:1.56, maxDeg:92 };
  const BENCH = { yb:-4.47, x1:0.55, zc:1.37 };
  const HATCH = { x:1.10, y0:-4.20, y1:-2.40, z:SOLE, t:0.06, maxDeg:45 };
  // windscreen: base curve on the deck, top curve under the hardtop, strongly raked
  const WS = { XB:1.36, YB:0.25, WB:0.75, PB:2.2, XT:1.27, YT:-0.85, WT:0.45, ZT:3.02, NS:16 };
  const wsBY=(x)=>WS.YB-WS.WB*Math.pow(Math.min(1,Math.abs(x)/WS.XB),WS.PB);
  const wsTY=(x)=>WS.YT-WS.WT*Math.pow(Math.min(1,Math.abs(x)/WS.XT),WS.PB);
  const wsBZ=(x)=>deckZ(uOf(wsBY(x)),x)+0.008;
  const wsBase=(s)=>{ const x=s*WS.XB; return [x, wsBY(x), wsBZ(x)]; };
  const ZSIDE=2.80;   // the windscreen's full-height line at the corners; the glass stops at WS_H of it
  const wsTZ=(s)=>WS.ZT-(WS.ZT-ZSIDE)*Math.pow(Math.min(1,Math.abs(s)),3);
  const wsTop=(s)=>{ const x=s*WS.XT; return [x, wsTY(x), wsTZ(s)]; };
  // the glass stops at WS_H of the way up the rake, so the cockpit shows under the roof face on
  const WS_H=0.5;
  const wsMid=(s)=>{ const a=wsBase(s), b=wsTop(s); return [lerp(a[0],b[0],WS_H), lerp(a[1],b[1],WS_H), lerp(a[2],b[2],WS_H)]; };
  const U_W0=uOf(wsBY(WS.XB)), U_YB=uOf(WS.YB);
  const xbAt=(y)=>WS.XB*Math.pow(Math.max(0,Math.min(1,(WS.YB-y)/WS.WB)),1/WS.PB);
  // the dash follows the windscreen: deep under the raked centre, shallow at the corners
  const DASH = { top:1.93, dd:0.75, ddE:0.35, xw:1.34, cw0:-0.62, cw1:0.02 };
  const dashY=(x)=>wsBY(x)-(DASH.dd-DASH.ddE*Math.pow(Math.min(1,Math.abs(x)/WS.XB),2));
  const dashZ=(x)=>Math.min(DASH.top, wsBZ(x)-0.10);
  const DOOR = { xh:DASH.cw0, maxDeg:95 };
  // hardtop
  const HT = { XH:1.34, Y1:-2.95, CH:0.16, ZU:3.04, ZE:3.12, CR:0.05, BROW:0.18, ZA:3.03, T:0.09,
               SR:{ x:0.55, y0:-1.10, y1:-1.95, travel:0.85 },
               LEG:{ tf:-2.02, bf:-2.42, ta:-2.80, ba:-3.00, bow:0.12, xb:1.36, xt:1.30, w:0.10 } };
  const htFrontY=(x)=>wsTY(Math.min(Math.abs(x),WS.XT))+HT.BROW*(1-0.6*Math.pow(Math.min(1,Math.abs(x)/HT.XH),2));
  const htZ=(x)=>HT.ZE+HT.CR*(1-Math.pow(Math.min(1,Math.abs(x)/HT.XH),2));
  const HT_XS=[-HT.XH,-0.95,-HT.SR.x,-0.20,0.20,HT.SR.x,0.95,HT.XH];
  // outline [x, y, fascia bottom z]: a thin free-standing roof, HT.T deep all round
  const HT_OUT=(()=>{ const o=HT_XS.map(x=>[x,htFrontY(x),htZ(x)-HT.T]);
    o.push([HT.XH,HT.Y1+HT.CH,HT.ZA],[HT.XH-HT.CH,HT.Y1,HT.ZA],[-HT.XH+HT.CH,HT.Y1,HT.ZA],[-HT.XH,HT.Y1+HT.CH,HT.ZA]); return o; })();
  // seats
  // 10-03: w 1.02 on xc 0.69, each seat 0.18..1.20 off the centreline: the aisle between them was 0.36 m, narrower
  // than the walker (2 x 0.23 m). Now 0.94 on xc 0.73 (0.26..1.20, the outboard edge unchanged): the aisle is 0.52 m
  const SEAT = { y0:-1.85, y1:-1.30, zs:1.60, w:0.94, xc:0.73 };

  // ---- STATIC faces --------------------------------------------------------------------------------
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
  // the new layout's fixed point, read by build() below and by the hull's block at the end
  //   CR_HUB    the wheel's hub, rake (negative: top toward the helmsman), the column's length along the axis, and
  //             the helm pod it stands in: its top podUp and its foot podDn along the wheel's plane from the column's
  //             end, podW either side of the hub, meeting the dash top podT of the way up it
  const CR_HUB = { x:0.52, y:-1.14, z:1.96, rake:-20, col:0.10, podUp:0.06, podDn:0.08, podW:0.09, podT:0.08 };
  const creased=(a,c)=>[a, Math.min(c,Math.max(a,FC)), Math.min(c,Math.max(a,FK)), c];
  function bandStrip(side,u0,u1,f0,f1,mat,b,db){
    const e0=creased(fv(f0,u0),fv(f1,u0)), e1=creased(fv(f0,u1),fv(f1,u1));
    for(let k=0;k<3;k++){
      if(e0[k+1]-e0[k]<1e-5 && e1[k+1]-e1[k]<1e-5) continue;
      face([skin(side,u0,e0[k]),skin(side,u1,e1[k]),skin(side,u1,e1[k+1]),skin(side,u0,e0[k+1])],mat,b,db);
    }
  }
  // a decal on the outer skin: an outline in (du, dz) about (uc, zc), stood just proud of the hull
  const onHull=(side,u,z,off)=>[side*(halfAtZ(u,z)+off), yAt(u,z), z];

  (function build(){
    TAG('hull','shell',{steel:'trim',blk:'trim'});
    // ---- hull skin (bowrider winding), spray rail, rub rail --------------------------------------
    const UB=(()=>{ const a=[]; for(let k=0;k<=10;k++) a.push(U_F*(1-Math.cos(k/10*Math.PI/2)));
      for(let i=1;i<=NSEG;i++) if(i/NSEG>U_F+1e-4) a.push(i/NSEG); return a; })();
    for(const side of [-1,1]){
      for(let i=0;i+1<UB.length;i++){
        const u0=UB[i], u1=UB[i+1];
        for(const [f0,f1,mat,b,db] of BANDS) bandStrip(side,u0,u1,f0,f1,mat,bv(b,u0),db);
        const srt=(u)=>0.060*(1-Math.pow(clamp01((u-0.78)/0.20),1.3));
        const ca=skin(side,u0,FC), cb=skin(side,u1,FC);
        const oc0=[ca[0]+side*srt(u0),ca[1],ca[2]+0.014], oc1=[cb[0]+side*srt(u1),cb[1],cb[2]+0.014];
        const dnp=(p)=>[p[0],p[1],p[2]-0.055];
        face([ca,cb,oc1,oc0], ca[2]<zBoot()?'bottom':'paint', 0.8, 0.03);
        face([oc0,oc1,dnp(oc1),dnp(oc0)],'bottom',-1.25,0.03);
        const oa=skin(side,u0,1), ob2=skin(side,u1,1);
        const inb=(p)=>[p[0]-side*RUBIN,p[1],p[2]];
        const or0=[oa[0],oa[1],oa[2]+0.002], or1=[ob2[0],ob2[1],ob2[2]+0.002];
        face([or0,or1,inb(ob2),inb(oa)],'steel',0.8,0.04);
        face([or0,or1,[or1[0],or1[1],or1[2]-0.07],[or0[0],or0[1],or0[2]-0.07]],'blk',0.2,0.04);
      }
    }
    for(let i=0;i<NSEG;i++){   // keel pad
      const u0=i/NSEG, u1=(i+1)/NSEG, kb=-1.0+1.0*Math.pow(Math.max(0,(u0-0.72)/0.28),1.2);
      face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)], station(u0).kz>zBoot()?'paint':'bottom', kb);
    }
    // the stem: close the two skins where they meet
    for(const [f0,f1,mat,b] of BANDS){
      const E=creased(fv(f0,1),fv(f1,1));
      for(let k=0;k<3;k++){ if(E[k+1]-E[k]<1e-5) continue;
        fo([skin(-1,1,E[k]),skin(1,1,E[k]),skin(1,1,E[k+1]),skin(-1,1,E[k+1])], mat, bv(b,1)+0.2, 0, [0,1,0.2]); }
    }

    TAG('hull','trim');
    // ---- hull decals: three oval ports a side in the navy, four raked gills in the white wedge -----
    for(const side of [-1,1]){
      for(const uc of [0.575,0.655,0.735]){
        const zc=sheerZ(uc)-0.43, A=0.22/L, Bz=0.075, N=12;
        const P=(k,sc,off)=>{ const a=2*Math.PI*k/N, c=Math.cos(a), s=Math.sin(a);
          const q=Math.pow(Math.abs(c),0.6)*Math.sign(c), r=Math.pow(Math.abs(s),0.6)*Math.sign(s);   // a stadium, not an ellipse
          return onHull(side, uc+A*sc*q, zc+Bz*sc*r, off); };
        for(let k=0;k<N;k++) fo([P(k,1,0.006),P(k+1,1,0.006),P(k+1,0.66,0.006),P(k,0.66,0.006)],'steel',0.9,0.03,[side,0,0]);
        const lens=[]; for(let k=0;k<N;k++) lens.push(P(k,0.66,0.007));
        fo(lens,'blk',0.6,0.035,[side,0,0]);
      }
      for(const uc of [0.150,0.172,0.194,0.216]){
        const zc=sheerZ(uc)-0.24, h=0.10, w=0.022/L, sl=0.06/L;
        const P=(du,dz)=>onHull(side, uc+du, zc+dz, 0.006);
        fo([P(-w+sl,-h),P(w+sl,-h),P(w-sl,h),P(-w-sl,h)],'blk',0.1,0.03,[side,0,0]);
      }
    }

    TAG('hull','shell',{nonskid:'main_deck/floor'});
    // ---- transom: strips by height, split round the walk-through above the platform --------------
    (function(){
      const st=station(0), y=-L/2, tx=(z)=>halfAtZ(0,z);
      const cz=st.kz+st.cd, sz=st.kz+st.dep, zk=lerp(cz,sz,(FK-FC)/(1-FC));
      const zs=[st.kz,cz,zk,zBoot(),zCol(0),zPin(0),PL.zb,PL.zt,sz].filter(z=>z<=sz+1e-6).sort((a,b)=>a-b);
      const matAt=(z)=>z<zBoot()?'bottom':z<zCol(0)?'paint':z<zPin(0)?'stripe':'deck';
      const bAt=(m)=>({bottom:-1.3,paint:-0.55,stripe:-0.25,deck:-0.4})[m];
      for(let k=0;k+1<zs.length;k++){
        const za=zs[k], zb=zs[k+1]; if(zb-za<1e-4) continue;
        const m=matAt((za+zb)/2), b=bAt(m), xa=tx(za), xb=tx(zb);
        if(za>=PL.zt-1e-4){
          fo([[-xa,y,za],[GATE.x0,y,za],[GATE.x0,y,zb],[-xb,y,zb]],m,b,0.005,[0,-1,0]);
          if(xa>GATE.x1) fo([[GATE.x1,y,za],[xa,y,za],[xb,y,zb],[GATE.x1,y,zb]],m,b,0.005,[0,-1,0]);
        } else fo([[-xa,y,za],[xa,y,za],[xb,y,zb],[-xb,y,zb]],m,b,0.005,[0,-1,0]);
      }
      // the walk-through's sill and its riser; the channel runs forward through the stern moulding
      fo([[GATE.x0,y,SOLE],[GATE.x1,y,SOLE],[GATE.x1,Y_TR,SOLE],[GATE.x0,Y_TR,SOLE]],'nonskid',0.1,0,[0,0,1]);
      fo([[GATE.x0,y,PL.zt],[GATE.x1,y,PL.zt],[GATE.x1,y,SOLE],[GATE.x0,y,SOLE]],'deck',-0.6,0.004,[0,-1,0]);
    })();

    TAG('main_deck','wall',{deck:'hull/shell',uph:'fit',acc:'fit',steel:'hull/fit'});
    // ---- the stern moulding: the fillet rolls the top-aft corner down to the transom foot; the
    //      walk-through is a channel through it, and the aft bench is recessed into its forward face
    (function(){
      const yT=-L/2, yF=yT+FIL.R, B=BENCH, zF=(y)=>capZ(uOf(y)), xE=(y)=>xEdge(uOf(y));
      const xi=xl(uOf((B.yb+yF)/2),B.zc)-0.01;
      const ys=[]; for(let k=0;k<=12;k++) ys.push(yT+FIL.R*(1-Math.cos(k/12*Math.PI/2)));
      ys.push(B.yb); ys.sort((a,b)=>a-b);
      const XA=(x,y)=>x==='P'?-xE(y):x==='S'?xE(y):x;
      for(let k=0;k+1<ys.length;k++){
        const ya=ys[k], yb=ys[k+1]; if(yb-ya<1e-5) continue;
        const za=zF(ya), zb=zF(yb), rec=ya>=B.yb-1e-6;
        [['P',-xi],[-xi,B.x1],[B.x1,GATE.x0],[GATE.x1,'S']].forEach(([x0,x1],j)=>{
          if(j===1 && rec) return;
          fo([[XA(x0,ya),ya,za],[XA(x1,ya),ya,za],[XA(x1,yb),yb,zb],[XA(x0,yb),yb,zb]],'deck',0.35,0,[0,-0.7,1]);
        });
        fo([[GATE.x0,ya,SOLE],[GATE.x0,yb,SOLE],[GATE.x0,yb,zb],[GATE.x0,ya,za]],'liner',-0.5,0,[1,0,0]);
        fo([[GATE.x1,ya,SOLE],[GATE.x1,yb,SOLE],[GATE.x1,yb,zb],[GATE.x1,ya,za]],'liner',-0.35,0,[-1,0,0]);
      }
      // the moulding's forward face beside the bench and to starboard of the channel
      const zt=zF(yF), xs=(z)=>xl(U_F,z)-0.004;
      fo([[GATE.x1,yF,SOLE],[xs(SOLE),yF,SOLE],[xs(zt),yF,zt],[GATE.x1,yF,zt]],'liner',-0.45,0,[0,1,0]);
      fo([[B.x1,yF,SOLE],[GATE.x0,yF,SOLE],[GATE.x0,yF,zt],[B.x1,yF,zt]],'liner',-0.45,0,[0,1,0]);
      // the aft bench: riser, seat, a low raked back against the fillet, the recess's end wall
      fo([[-xi,yF,SOLE],[B.x1,yF,SOLE],[B.x1,yF,B.zc-0.08],[-xi,yF,B.zc-0.08]],'liner',-0.2,0,[0,1,0]);
      const xa=-xi+0.02;
      F.push.apply(F, slab([(xa+B.x1)/2,(B.yb+yF)/2+0.01,B.zc-0.04],[(B.x1-xa)/2,(yF-B.yb)/2,0.04],'uph',-1.8,0.1));
      const zk=zF(B.yb), q=(z0,z1,y0,y1)=>[[xa,y0,z0],[B.x1,y0,z0],[B.x1,y1,z1],[xa,y1,z1]];
      fo(q(B.zc,lerp(B.zc,zk,0.62),B.yb+0.05,B.yb+0.02),'uph',0.05,0.004,[0,1,0.3]);
      fo(q(lerp(B.zc,zk,0.62),lerp(B.zc,zk,0.80),B.yb+0.02,B.yb+0.012),'acc',0.05,0.004,[0,1,0.3]);
      fo(q(lerp(B.zc,zk,0.80),zk-0.005,B.yb+0.012,B.yb+0.002),'uph',0.1,0.004,[0,1,0.3]);
      const wall=[[B.x1,yF,B.zc-0.08],[B.x1,B.yb,B.zc-0.08]];
      for(const y of ys) if(y>=B.yb-1e-6 && y<=yF+1e-6) wall.push([B.x1,y,zF(y)]);
      fo(wall,'liner',-0.6,0,[-1,0,0]);
      // stern eyes on the fillet
      const yE=yT+FIL.R*(1-Math.cos(35*DEG)), zE=zF(yE);
      for(const s of [-1,1]) boxF([s*1.02,yE,zE+0.012],[0.03,0.022,0.012],'steel',0.3,0.02);
    })();

    TAG('hull','shell',{nonskid:'main_deck/floor',steel:'main_deck/fit'});
    // ---- swim platform --------------------------------------------------------------------------
    (function(){
      const {y0,y1,zt,zb,hw,ch}=PL;
      const p=[[-hw,y0],[-hw,y1+ch],[-hw+ch,y1],[hw-ch,y1],[hw,y1+ch],[hw,y0]];
      fo(p.map(([x,y])=>[x,y,zt]),'nonskid',0.25,0,[0,0,1]);
      fo(p.map(([x,y])=>[x,y,zb]),'bottom',-1.5,0,[0,0,-1]);
      for(let k=0;k+1<p.length;k++){
        const a=p[k], b=p[k+1], m=[(a[0]+b[0])/2,(a[1]+b[1])/2-(y0+y1)/2,0];
        fo([[a[0],a[1],zb],[b[0],b[1],zb],[b[0],b[1],zt],[a[0],a[1],zt]],'deck',-0.35,0.002,m);
      }
      boxF([-0.70,y1+0.13,zt+0.006],[0.15,0.075,0.006],'steel',0.15,0.01);   // flush boarding-ladder lid
    })();

    TAG('hull','shell');
    // ---- deck cap along the cockpit -------------------------------------------------------------
    for(const side of [-1,1]){
      for(let i=0;i<NI;i++){
        const u0=U_F+(U_W0-U_F)*i/NI, u1=U_F+(U_W0-U_F)*(i+1)/NI, y0=yOf(u0), y1=yOf(u1);
        const a0=[side*xEdge(u0), y0, capZ(u0)], a1=[side*xEdge(u1), y1, capZ(u1)];
        const b0=[side*xo(u0), y0, capZ(u0)+0.006], b1=[side*xo(u1), y1, capZ(u1)+0.006];
        fo([a0,a1,b1,b0],'deck',0.35,0,[0,0,1]);
      }
    }

    TAG('foredeck','floor',{glas:'fit',steel:'fit',blk:'fit'});
    // ---- foredeck: wings beside the windscreen, then the crowned cabin top to the stem ------------
    (function(){
      const NW=8, NF=24, TIP=0.998;
      for(let i=0;i<NW;i++){
        const u0=U_W0+(U_YB-U_W0)*i/NW, u1=U_W0+(U_YB-U_W0)*(i+1)/NW;
        const cols=(u)=>{ const xb=xbAt(yOf(u)), e=xEdge(u), c=Math.max(xb,e-SD); return [xb,lerp(xb,c,1/3),lerp(xb,c,2/3),c,e]; };
        const c0=cols(u0), c1=cols(u1);
        for(const s of [-1,1]) for(let k=0;k<4;k++){
          const P=(u,x)=>[s*x, yOf(u), deckZ(u,x)];
          fo([P(u0,c0[k]),P(u0,c0[k+1]),P(u1,c1[k+1]),P(u1,c1[k])], k===3?'nonskid':'deck', k===3?0.2:0.25, 0, [0,0,1]);
        }
      }
      for(let i=0;i<NF;i++){
        const u0=U_YB+(TIP-U_YB)*i/NF, u1=U_YB+(TIP-U_YB)*(i+1)/NF;
        const cols=(u)=>{ const e=xEdge(u), c=Math.max(0,e-SD); return [-e,-c,-c*2/3,-c/3,0,c/3,c*2/3,c,e]; };
        const c0=cols(u0), c1=cols(u1);
        for(let k=0;k<8;k++){
          const P=(u,x)=>[x, yOf(u), deckZ(u,x)], sd=(k===0||k===7), panel=(k>=2&&k<=5)&&u0>=U_YB+0.02&&u1<=0.905;
          fo([P(u0,c0[k]),P(u0,c0[k+1]),P(u1,c1[k+1]),P(u1,c1[k])], sd||panel?'nonskid':'deck', sd?0.2:panel?0.35:0.25, 0, [0,0,1]);
        }
      }
      // cabin hatch: moulded frame, tinted lens
      const yh=2.25, uh=uOf(yh), zh=deckZ(uh,0)+0.012;
      boxF([0,yh,zh],[0.31,0.31,0.022],'deck',0.4,-0.01);
      fo([[-0.25,yh-0.25,zh+0.023],[0.25,yh-0.25,zh+0.023],[0.25,yh+0.25,zh+0.023],[-0.25,yh+0.25,zh+0.023]],'glas',0.7,0.01,[0,0,1]);
      // anchor locker lid, windlass, bow cleat
      const ua=0.905, ub=0.940, la=Math.max(0.05,xEdge(ua)-0.34), lb=Math.max(0.04,xEdge(ub)-0.30);
      fo([[-la,yOf(ua),deckZ(ua,la)+0.012],[la,yOf(ua),deckZ(ua,la)+0.012],[lb,yOf(ub),deckZ(ub,lb)+0.012],[-lb,yOf(ub),deckZ(ub,lb)+0.012]],'deck',-0.8,-0.02,[0,0,1]);
      const uw=0.955; boxF([0,yOf(uw),deckZ(uw,0)+0.05],[0.08,0.09,0.05],'steel',0.25);
      const uc=0.975; boxF([0,yOf(uc),deckZ(uc,0)+0.016],[0.022,0.07,0.014],'steel',0.55,-0.03);
      // anchor pulpit, roller, the anchor on it
      const zs=capZ(1);
      boxF([0,L/2+0.13,zs-0.035],[0.12,0.24,0.035],'deck',0.3);
      boxF([0,L/2+0.34,zs-0.02],[0.05,0.035,0.035],'steel',0.3,0.01);
      tubeF([0,L/2+0.33,zs-0.04],[0,L/2+0.22,zs-0.30],0.022,'steel',0.35);
      F.push.apply(F, box([0,L/2+0.20,zs-0.33],[0.11,0.05,0.03],'steel',0.2,0,(p)=>[p[0],p[1],p[2]+Math.abs(p[0])*0.5]));
    })();

    TAG('main_deck','wall'); const _liner0=F.length;
    // ---- liner: from the cap's inner edge down to the local floor --------------------------------
    function linerStrip(side,u0,u1,bands){
      const zt=(u)=>capZ(u)+0.006, zf=(u)=>floorZ(u);
      for(let k=0;k<bands.length;k++){
        const P=(u,j)=>{ const z=zt(u)-(zt(u)-zf(u))*j/bands.length; return [side*xl(u,z), yAt(u,z), z]; };
        fo([P(u0,k),P(u1,k),P(u1,k+1),P(u0,k+1)],'liner',bands[k],0,[-side,0,0.15]);
      }
    }
    for(const side of [-1,1]){
      const n1=10, n2=10;
      for(let i=0;i<n1;i++) linerStrip(side, U_TR+(U_STEP-U_TR)*i/n1, U_TR+(U_STEP-U_TR)*(i+1)/n1, [-0.55,-0.85]);
      for(let i=0;i<n2;i++) linerStrip(side, U_STEP+(U_W0-U_STEP)*i/n2, U_STEP+(U_W0-U_STEP)*(i+1)/n2, [-0.55,-0.85]);
    }

    TAG(null);   // the liner forward of the helm step is the helm deck's
    for(let i=_liner0;i<F.length;i++){ const v=F[i].v; let y=0; for(const p of v) y+=p[1]; if(y/v.length>Y_STEP) stampTags(F, i, i+1, 'house', 'wall'); }
    TAG('main_deck','floor',{liner:'house/wall'});
    // ---- cockpit soles: aft sole round the hatch, the step, the helm deck -------------------------
    (function(){
      const soleRow=(yA,yB,xin,zz,n)=>{
        for(let i=0;i<n;i++){
          const ya=lerp(yA,yB,i/n), yb=lerp(yA,yB,(i+1)/n), wa=xl(uOf(ya),zz)+0.03, wb=xl(uOf(yb),zz)+0.03;
          if(xin>0){ for(const s of [-1,1]) fo([[s*xin,ya,zz],[s*wa,ya,zz],[s*wb,yb,zz],[s*xin,yb,zz]],'sole',-2.5,0,[0,0,1]); }
          else fo([[-wa,ya,zz],[wa,ya,zz],[wb,yb,zz],[-wb,yb,zz]],'sole',-2.5,0,[0,0,1]);
        }
      };
      soleRow(Y_TR, HATCH.y0, 0, SOLE, 2);
      soleRow(HATCH.y0, HATCH.y1, HATCH.x, SOLE, 5);
      soleRow(HATCH.y1, Y_STEP, 0, SOLE, 1);
      const w=xl(U_STEP,SOLE)+0.02;
      fo([[-w,Y_STEP,SOLE],[w,Y_STEP,SOLE],[w,Y_STEP,HDECK],[-w,Y_STEP,HDECK]],'liner',-0.35,0,[0,-1,0]);
      TAG('house','floor');
      const N=14, XW=1.37;
      for(let i=0;i<N;i++){
        const xa=-XW+2*XW*i/N, xb=-XW+2*XW*(i+1)/N;
        fo([[xa,Y_STEP,HDECK],[xb,Y_STEP,HDECK],[xb,dashY(xb),HDECK],[xa,dashY(xa),HDECK]],'sole',-2.5,0,[0,0,1]);
      }
    })();


    TAG('house','fit');
    // ---- helm and companion seats: double bench seats on moulded bases ----------------------------
    for(const s of [-1,1]){
      const xc=s*SEAT.xc, w=SEAT.w, y0=SEAT.y0, y1=SEAT.y1, zs=SEAT.zs;
      boxF([xc,(y0+y1)/2-0.02,(HDECK+zs)/2],[w/2-0.05,(y1-y0)/2-0.05,(zs-HDECK)/2],'liner',-0.15);
      F.push.apply(F, slab([xc,(y0+y1)/2,zs+0.04],[w/2,(y1-y0)/2,0.04],'uph',-1.7,0.1));
      fo([[xc-w/2,y1+0.001,zs],[xc+w/2,y1+0.001,zs],[xc+w/2,y1+0.001,zs+0.08],[xc-w/2,y1+0.001,zs+0.08]],'acc',0.0,0.01,[0,1,0]);
      const rk=(p)=>[p[0], p[1]-(p[2]-zs)*0.18, p[2]];
      boxF([xc,y0-0.02,zs+0.37],[w/2,0.06,0.29],'uph',0.05,0,rk);
      boxF([xc,y0-0.02,zs+0.69],[w/2,0.07,0.035],'acc',0.15,0,rk);
    }

    TAG('house','fit');
    // ---- wet bar behind the companion seat ---------------------------------------------------------
    (function(){
      const x0=-1.26, x1=-0.50, y0=-2.33, y1=-2.00, zt=1.97;
      boxF([(x0+x1)/2,(y0+y1)/2,(HDECK+zt)/2],[(x1-x0)/2,(y1-y0)/2,(zt-HDECK)/2],'liner',-0.2);
      F.push.apply(F, slab([(x0+x1)/2,(y0+y1)/2,zt+0.015],[(x1-x0)/2+0.02,(y1-y0)/2+0.02,0.015],'deck',0.2,-0.3));
      fo([[-1.02,-2.25,zt+0.031],[-0.78,-2.25,zt+0.031],[-0.78,-2.08,zt+0.031],[-1.02,-2.08,zt+0.031]],'steel',-0.9,0.01,[0,0,1]);
      fo([[x1+0.003,y0+0.04,HDECK+0.10],[x1+0.003,y1-0.04,HDECK+0.10],[x1+0.003,y1-0.04,zt-0.12],[x1+0.003,y0+0.04,zt-0.12]],'liner',-0.9,0.01,[1,0,0]);
    })();

    TAG('house','wall',{deck:'roof',sole:'floor',blk:'fit',glas:'fit',steel:'fit'});
    // ---- dash: top from the bulkhead to the windscreen base, the bulkhead with the companionway -----
    (function(){
      const N=16, XW=DASH.xw;
      for(let i=0;i<N;i++){
        const xa=-XW+2*XW*i/N, xb=-XW+2*XW*(i+1)/N;
        fo([[xa,dashY(xa),dashZ(xa)],[xb,dashY(xb),dashZ(xb)],[xb,wsBY(xb),wsBZ(xb)-0.004],[xa,wsBY(xa),wsBZ(xa)-0.004]],'deck',0.45,0,[0,-0.3,1]);
      }
      for(const [x0,x1] of [[-XW,DASH.cw0],[DASH.cw1,XW]]){
        const n=6;
        for(let i=0;i<n;i++){
          const xa=x0+(x1-x0)*i/n, xb=x0+(x1-x0)*(i+1)/n;
          fo([[xa,dashY(xa),HDECK],[xb,dashY(xb),HDECK],[xb,dashY(xb),dashZ(xb)],[xa,dashY(xa),dashZ(xa)]],'liner',-0.1,0,[0,-1,0]);
        }
      }
      // the companionway: jambs, soffit, the top tread, the dark stair well
      const x0=DASH.cw0, x1=DASH.cw1, yF=dashY((x0+x1)/2), yB=WS.YB-0.10, zt=dashZ((x0+x1)/2), yS=yF+0.32;
      fo([[x0,dashY(x0),HDECK],[x0,yB,HDECK],[x0,yB,zt],[x0,dashY(x0),zt]],'liner',-1.3,0,[1,0,0]);
      fo([[x1,dashY(x1),HDECK],[x1,yB,HDECK],[x1,yB,zt],[x1,dashY(x1),zt]],'liner',-1.45,0,[-1,0,0]);
      fo([[x0,dashY(x0),zt],[x1,dashY(x1),zt],[x1,yB,zt],[x0,yB,zt]],'liner',-1.6,0,[0,0,-1]);
      fo([[x0,yF-0.02,HDECK-0.002],[x1,yF-0.02,HDECK-0.002],[x1,yS,HDECK-0.002],[x0,yS,HDECK-0.002]],'sole',-2.9,0,[0,0,1]);
      fo([[x0,yS,HDECK],[x1,yS,HDECK],[x1,yS,0.86],[x0,yS,0.86]],'blk',-0.2,0,[0,-1,0]);
      fo([[x0,yS,0.86],[x1,yS,0.86],[x1,yB,0.86],[x0,yB,0.86]],'blk',0.3,0,[0,0,1]);
      fo([[x0,yB,0.86],[x1,yB,0.86],[x1,yB,zt],[x0,yB,zt]],'blk',-0.1,0,[0,-1,0]);
      // helm: black panel on the dash top, two screens, throttles on the bulkhead
      const P=(x,t,dz)=>[x, lerp(dashY(x),wsBY(x),t), lerp(dashZ(x),wsBZ(x),t)+dz];
      const panel=(xa,xb,ta,tb,dz,mat,b,db)=>{ const n=3; for(let i=0;i<n;i++){ const p=lerp(xa,xb,i/n), q=lerp(xa,xb,(i+1)/n);
        fo([P(p,ta,dz),P(q,ta,dz),P(q,tb,dz),P(p,tb,dz)],mat,b,db,[0,-0.4,1]); } };
      panel(0.12,1.08,0.04,0.46,0.012,'blk',0.3,0.01);
      panel(0.20,0.56,0.10,0.36,0.02,'glas',0.9,0.02);
      panel(0.64,1.00,0.10,0.36,0.02,'glas',0.9,0.02);
      boxF([1.02,dashY(1.02)-0.06,1.74],[0.07,0.06,0.05],'blk',0.2);
      for(const dx of [-0.03,0.03]) tubeF([1.02+dx,dashY(1.02)-0.08,1.78],[1.02+dx,dashY(1.02)-0.16,1.88],0.014,'steel',0.4);
      // the helm pod (the new layout): the wheel stands 0.34 m higher and leans its top toward the helmsman, so its
      // column needs a seat on the dash: a black wedge from the dash top, its aft face square to the column
      (function(){
        const G=CR_HUB, r=G.rake*DEG, n=[0,-Math.cos(r),Math.sin(r)], e2=[0,Math.sin(r),Math.cos(r)];
        const C=[G.x-n[0]*G.col, G.y-n[1]*G.col, G.z-n[2]*G.col];
        const at=(x)=>{ const A=[x,C[1]+e2[1]*G.podUp,C[2]+e2[2]*G.podUp], B=[x,C[1]-e2[1]*G.podDn,C[2]-e2[2]*G.podDn];
          return { A, B, D:[x,dashY(x),B[2]], E:P(x,G.podT,0) }; };
        const p0=at(G.x-G.podW), p1=at(G.x+G.podW);
        fo([p0.A,p1.A,p1.E,p0.E],'blk',0.35,0.01,[0,0,1]);
        fo([p0.A,p1.A,p1.B,p0.B],'blk',-0.1,0.01,n);
        fo([p0.B,p1.B,p1.D,p0.D],'blk',-0.6,0.01,[0,0,-1]);
        fo([p0.A,p0.B,p0.D,p0.E],'blk',0,0.01,[-1,0,0]);
        fo([p1.A,p1.B,p1.D,p1.E],'blk',0,0.01,[1,0,0]);
      })();
      // port: glovebox door and a grab rail
      fo([[-0.78,dashY(-0.78)-0.004,1.45],[-1.18,dashY(-1.18)-0.004,1.45],[-1.18,dashY(-1.18)-0.004,1.70],[-0.78,dashY(-0.78)-0.004,1.70]],'liner',-0.9,-0.01,[0,-1,0]);
    })();

    TAG('house','wall',{blk:'trim',steel:'trim'});
    // ---- windscreen, side glass, hardtop legs ---------------------------------------------------------
    (function(){
      const N=WS.NS;
      for(let i=0;i<N;i++){
        const s0=-1+2*i/N, s1=-1+2*(i+1)/N, m=(s0+s1)/2;
        fo([wsBase(s0),wsBase(s1),wsMid(s1),wsMid(s0)],'glas',-1.2,0,[m*0.8,1,0.6]);
        tubeF(wsBase(s0),wsBase(s1),0.02,'blk',0.25);
        tubeF(wsMid(s0),wsMid(s1),0.018,'steel',0.5);
      }
      const G=HT.LEG, NT=8;
      // the side fascia's lower edge: the roof is HT.T thick all round
      const zFas=()=>HT.ZE-HT.T;
      // arch leg at height t (0 under the roof, 1 on the coaming) and chord v (0 leading edge, 1 trailing)
      const legY=(t,v)=>lerp(lerp(G.tf,G.bf,t)-G.bow*Math.sin(Math.PI*t), lerp(G.ta,G.ba,t), v);
      const legP=(s,t,v,inner)=>{ const y=legY(t,v), z=lerp(zFas(y)+0.02, capZ(uOf(y))+0.004, t);
        return [s*(lerp(G.xt,G.xb,t)-(inner?G.w:0)), y, z]; };
      const TS=[0,0.10,0.22,0.36,0.50,0.66,0.83,1], VS=[0,0.24,0.84,1], WIN=(ti,vi)=>ti>=1&&ti<=3&&vi===1;
      for(const s of [-1,1]){
        TAG('house','wall',{blk:'trim',steel:'trim'});
        tubeF(wsBase(s),wsMid(s),0.022,'blk',0.35);
        // cut away: a glass wing under a top edge that sweeps from the corner post down to the arch foot
        const yB=wsBY(WS.XB), B0=[s*WS.XB, yB, capZ(uOf(yB))+0.006], T0=wsMid(s), zt=T0[2], xt=Math.abs(T0[0]), zb=capZ(uOf(G.bf))+0.006;
        const cut=[]; for(let k=0;k<=NT;k++){ const q=k/NT, y=lerp(T0[1],G.bf,q), z=lerp(zt,zb,Math.pow(q,1.15));
          cut.push([s*lerp(xt,WS.XB,(zt-z)/(zt-zb)), y, z]); }
        fo([B0].concat(cut),'side',-0.2,0,[s,0,0.1]);
        for(let k=0;k<NT;k++) tubeF(cut[k],cut[k+1],0.016,'steel',0.45);
        tubeF(B0,cut[NT],0.018,'blk',0.25);
        TAG('top','tall');
        // the arch leg: outer and inner skins with the window through both, then the two edges
        for(let ti=0;ti+1<TS.length;ti++) for(let vi=0;vi+1<VS.length;vi++){
          const ta=TS[ti], tb=TS[ti+1], va=VS[vi], vb=VS[vi+1], w=WIN(ti,vi);
          fo([legP(s,ta,va),legP(s,tb,va),legP(s,tb,vb),legP(s,ta,vb)], w?'glas':'deck', w?-0.3:0.1, 0, [s,0,0.05]);
          fo([legP(s,ta,va,1),legP(s,tb,va,1),legP(s,tb,vb,1),legP(s,ta,vb,1)], w?'glas':'liner', w?-0.6:-0.4, 0, [-s,0,0]);
        }
        for(let ti=0;ti+1<TS.length;ti++){
          const ta=TS[ti], tb=TS[ti+1];
          fo([legP(s,ta,0),legP(s,tb,0),legP(s,tb,0,1),legP(s,ta,0,1)],'deck',-0.1,0,[0,1,-0.3]);
          fo([legP(s,ta,1),legP(s,tb,1),legP(s,tb,1,1),legP(s,ta,1,1)],'deck',-0.3,0,[0,-1,0.2]);
        }
      }
    })();

    TAG('foredeck','fit');
    // ---- bow rail: six stanchions a side, meeting over the pulpit -----------------------------------
    (function(){
      const U=[0.555,0.64,0.72,0.80,0.87,0.935], tip=[0, L/2+0.26, capZ(1)+0.50];
      for(const s of [-1,1]){
        const pts=U.map(u=>[s*(xEdge(u)-0.08), yOf(u), capZ(u)+0.62]);
        pts.push([s*0.15, L/2+0.24, capZ(1)+0.52]);
        for(let k=0;k+1<pts.length;k++) tubeF(pts[k],pts[k+1],0.022,'steel',0.6);
        U.forEach((u,k)=>tubeF([pts[k][0],pts[k][1],capZ(u)],pts[k],0.018,'steel',0.3));
        tubeF([s*0.10,L/2+0.20,capZ(1)-0.02],pts[pts.length-1],0.018,'steel',0.3);
      }
      tubeF([-0.15,L/2+0.24,capZ(1)+0.52],[0.15,L/2+0.24,capZ(1)+0.52],0.022,'steel',0.6);
      void tip;
    })();

    TAG('hull','fit');
    // ---- cleats -----------------------------------------------------------------------------------------
    for(const s of [-1,1]){
      for(const u of [0.03,0.47]) boxF([s*(xEdge(u)-0.10),yOf(u),capZ(u)+0.016],[0.022,0.07,0.014],'steel',0.5,-0.02);
    }
  })();

  TAG(null);

  // ---- DYNAMIC parts ---------------------------------------------------------------------------------
  // engine hatch: the aft cockpit sole, hinged at its forward edge by the helm step; the aft edge lifts
  // on two rams, so the bay opens toward the transom and reads from astern
  function hatchFaces(h, out){
    const a=h*HATCH.maxDeg*DEG, ca=Math.cos(a), sa=Math.sin(a), yh=HATCH.y1, zh=HATCH.z;
    const _o=out.length;
    const xf=(p)=>{ const dy=p[1]-yh, dz=p[2]-zh; return [p[0], yh+dy*ca+dz*sa, zh-dy*sa+dz*ca]; };
    const push=(fs)=>{ for(const f of fs) out.push(f); };
    const X=HATCH.x, y0=HATCH.y0, y1=HATCH.y1, t=HATCH.t;
    const P=box([0,(y0+y1)/2,SOLE-t/2],[X-0.005,(y1-y0)/2-0.005,t/2],'liner',-0.7,0,xf);
    P[0].mat='sole'; P[0].b=-2.35; P[0].db=0.001;
    push(P);
    push(box([0,y0+0.10,SOLE+0.004],[0.09,0.025,0.004],'steel',0.3,0.01,xf));   // flush lift ring
    stampTags(out, _o, out.length, 'main_deck', 'fit', { sole:'floor' }); const _b=out.length;
    if(h<=0.001) return;
    // the bay
    const BX=HATCH.x-0.02, BZ=0.30, BY0=-4.58, BY1=HATCH.y1-0.02, TZ=SOLE-0.002;
    faceO(out,[[-BX,BY0,BZ],[BX,BY0,BZ],[BX,BY1,BZ],[-BX,BY1,BZ]],'liner',-1.7,0,[0,0,1]);
    faceO(out,[[-BX,BY1,BZ],[BX,BY1,BZ],[BX,BY1,TZ],[-BX,BY1,TZ]],'liner',-1.2,0,[0,-1,0]);
    faceO(out,[[-BX,BY0,BZ],[BX,BY0,BZ],[BX,BY0,TZ],[-BX,BY0,TZ]],'liner',-1.0,0,[0,1,0]);
    for(const s of [-1,1]) faceO(out,[[s*BX,BY0,BZ],[s*BX,BY1,BZ],[s*BX,BY1,TZ],[s*BX,BY0,TZ]],'liner',-1.05,0,[-s,0,0]);
    // twin V8s, flywheel ends at the transom
    for(const s of [-1,1]){
      const x=s*0.50;
      push(box([x,-3.95,0.55],[0.22,0.42,0.17],'eng',0.1));
      push(box([x,-4.43,0.54],[0.21,0.06,0.18],'eng',-0.1));
      push(box([x,-3.49,0.50],[0.12,0.05,0.12],'blk',0.25));
      for(const v of [-1,1]){
        push(box([x+v*0.15,-3.95,0.745],[0.08,0.36,0.035],'steel',0.05));
        push(box([x+v*0.28,-3.95,0.58],[0.05,0.34,0.07],'blk',0.2));
        push(box([x+v*0.28,-4.30,0.70],[0.055,0.06,0.09],'blk',0.3));
      }
      push(prism([x,-3.92,0.76],0.13,0.09,10,'steel',0.55,-0.01));
      push(box([x+s*0.16,-3.50,0.62],[0.06,0.05,0.06],'steel',0.1));    // alternator
    }
    push(box([0,-2.80,0.48],[0.26,0.25,0.18],'liner',-0.35));             // genset in its shield
    for(const s of [-1,1]) push(box([s*0.84,-2.72,0.42],[0.13,0.09,0.11],'blk',0.25));
    for(const s of [-1,1]){ const top=xf([s*0.96,-3.20,SOLE-t-0.004]); push(tube([s*0.98,-3.30,0.45],top,0.02,'steel',0.45)); }
    stampTags(out, _b, out.length, 'hull', 'part');
  }

  // companionway door: hinged on the port jamb, swings aft. The new layout draws it as the leaf, doorFaces(t)
  function companionLeaf(d, out){
    const a=d*DOOR.maxDeg*DEG, ca=Math.cos(a), sa=Math.sin(a), xh=DOOR.xh, yh=dashY(DASH.cw0)-0.005;
    const xf=(p)=>{ const dx=p[0]-xh, dy=p[1]-yh; return [xh+dx*ca+dy*sa, yh-dx*sa+dy*ca, p[2]]; };
    const w=DASH.cw1-DASH.cw0-0.02, z0=HDECK+0.01, z1=dashZ((DASH.cw0+DASH.cw1)/2)-0.02;
    for(const f of box([xh+0.01+w/2, yh-0.018, (z0+z1)/2],[w/2,0.018,(z1-z0)/2],'liner',-0.2,0,xf)) out.push(f);
    faceO(out,[[xh+0.08,yh-0.038,z0+0.30],[xh+w-0.06,yh-0.038,z0+0.30],[xh+w-0.06,yh-0.038,z1-0.08],[xh+0.08,yh-0.038,z1-0.08]].map(xf),'glas',0.2,0.01,xf([0,-1,0]).map((v,i)=>v-[xh,yh,0][i]));
    for(const f of box([xh+w-0.05, yh-0.045, z0+0.42],[0.012,0.01,0.06],'steel',0.4,0.01,xf)) out.push(f);
  }

  // transom door: hinged on the inboard jamb, swings aft over the platform
  function gateFaces(g, out){
    const a=g*GATE.maxDeg*DEG, ca=Math.cos(a), sa=Math.sin(a), xh=GATE.x0+0.01, yh=GATE.y;
    const xf=(p)=>{ const dx=p[0]-xh, dy=p[1]-yh; return [xh+dx*ca+dy*sa, yh-dx*sa+dy*ca, p[2]]; };
    const w=GATE.x1-GATE.x0-0.03;
    for(const f of box([xh+w/2,yh,(GATE.z0+GATE.z1)/2],[w/2,0.022,(GATE.z1-GATE.z0)/2],'deck',-0.05,0,xf)) out.push(f);
    for(const f of box([xh+w-0.07,yh-0.03,GATE.z1-0.16],[0.012,0.01,0.05],'steel',0.4,0.01,xf)) out.push(f);
  }

  // hardtop (tag 3): roof with the sunroof well, the sliding panel on its rails, radome, whip antenna
  function roofFaces(sr, out){
    const fs=[], SRX=HT.SR.x, SY0=HT.SR.y0, SY1=HT.SR.y1, Y1=HT.Y1, CH=HT.CH, XH=HT.XH, Z=htZ;
    const Pt=(x,y)=>[x,y,Z(x)];
    for(let k=0;k+1<HT_XS.length;k++){
      const xa=HT_XS[k], xb=HT_XS[k+1], fa=htFrontY(xa), fb=htFrontY(xb);
      if(xa>=-SRX-1e-6 && xb<=SRX+1e-6){
        faceO(fs,[Pt(xa,fa),Pt(xb,fb),Pt(xb,SY0),Pt(xa,SY0)],'deck',-0.2,0,[0,0,1]);
        faceO(fs,[Pt(xa,SY1),Pt(xb,SY1),Pt(xb,Y1),Pt(xa,Y1)],'deck',-0.2,0,[0,0,1]);
      } else {
        const v=[Pt(xa,fa),Pt(xb,fb)];
        if(xb>=XH-1e-6) v.push(Pt(xb,Y1+CH),Pt(xb-CH,Y1),Pt(xa,Y1));
        else if(xa<=-XH+1e-6) v.push(Pt(xb,Y1),Pt(xa+CH,Y1),Pt(xa,Y1+CH));
        else v.push(Pt(xb,Y1),Pt(xa,Y1));
        faceO(fs,v,'deck',-0.2,0,[0,0,1]);
      }
    }
    const cyo=(htFrontY(0)+Y1)/2;
    for(let k=0;k<HT_OUT.length;k++){
      const a=HT_OUT[k], b=HT_OUT[(k+1)%HT_OUT.length], m=[(a[0]+b[0])/2,(a[1]+b[1])/2-cyo,0];
      faceO(fs,[[a[0],a[1],a[2]],[b[0],b[1],b[2]],[b[0],b[1],Z(b[0])],[a[0],a[1],Z(a[0])]],'deck',-0.45,0,m);
    }
    const Hh=[[-SRX,SY0],[SRX,SY0],[SRX,SY1],[-SRX,SY1]], hc=(SY0+SY1)/2;
    for(let k=0;k<4;k++){
      const a=Hh[k], b=Hh[(k+1)%4], m=[-(a[0]+b[0])/2,-((a[1]+b[1])/2-hc),0];
      faceO(fs,[[a[0],a[1],HT.ZU],[b[0],b[1],HT.ZU],[b[0],b[1],Z(b[0])],[a[0],a[1],Z(a[0])]],'liner',-1.0,0,m);
    }
    stampTags(fs, 0, fs.length, 'top', 'roof'); const _r=fs.length;
    // the sunroof panel rides on two rails, sliding aft over the roof
    const dy=-clamp01(sr)*HT.SR.travel, zp=Z(0)+0.01;
    for(const s of [-1,1]) for(const f of tube([s*(SRX+0.05),SY0,Z(SRX+0.05)+0.012],[s*(SRX+0.05),SY1-HT.SR.travel-0.02,Z(SRX+0.05)+0.012],0.012,'steel',0.35)) fs.push(f);
    stampTags(fs, _r, fs.length, 'top', 'fit'); const _p=fs.length;
    for(const f of box([0,hc+dy,zp+0.018],[SRX+0.03,(SY0-SY1)/2+0.03,0.018],'blk',0.1)) fs.push(f);
    faceO(fs,[[-SRX,SY0+dy,zp+0.037],[SRX,SY0+dy,zp+0.037],[SRX,SY1+dy,zp+0.037],[-SRX,SY1+dy,zp+0.037]],'glas',0.55,0.01,[0,0,1]);
    stampTags(fs, _p, fs.length, 'top', 'roof'); const _q=fs.length;
    // radome on its pod, whip antenna on a ratchet mount, both aft
    for(const f of prism([-0.82,-2.52,Z(-0.82)-0.004],0.20,0.10,10,'deck',0.05)) fs.push(f);
    for(const f of prism([-0.82,-2.52,Z(-0.82)+0.096],0.15,0.05,10,'deck',0.35)) fs.push(f);
    for(const f of box([0.98,-2.82,Z(0.98)+0.03],[0.035,0.035,0.03],'steel',0.3)) fs.push(f);
    for(const f of tube([0.98,-2.82,Z(0.98)+0.05],[0.98,-3.32,Z(0.98)+1.40],0.016,'deck',0.35)) fs.push(f);
    stampTags(fs, _q, fs.length, 'top', 'fit');
    for(const f of fs){ f.tag=3; if(!f.db) f.db=0.002; out.push(f); }
  }

  // twin sterndrives (tag 2): trim pins, then each leg's vertical gimbal axis; duoprops
  const DRIVE = { yG:-L/2-0.085, zT:0.52, xs:[-0.55,0.55], k:1.25, steerMax:28, trimMin:-4, trimMax:12, tiltMax:34 };
  function driveFaces(s, out, tag){
    const k=DRIVE.k, yG=DRIVE.yG, zT=DRIVE.zT;
    const tr=(DRIVE.trimMin+(DRIVE.trimMax-DRIVE.trimMin)*s.trim+DRIVE.tiltMax*s.tilt)*DEG;
    const st=s.steer*DRIVE.steerMax*DEG, ct=Math.cos(tr), stt=Math.sin(tr), cs=Math.cos(st), ss=Math.sin(st);
    for(const x0 of DRIVE.xs){
      const trim=(p)=>{ const dy=p[1]-yG, dz=p[2]-zT; return [p[0], yG+dy*ct+dz*stt, zT-dy*stt+dz*ct]; };
      const steer=(p)=>{ const dx=p[0]-x0, dy=p[1]-yG; return [x0+dx*cs+dy*ss, yG-dx*ss+dy*cs, p[2]]; };
      const full=(p)=>steer(trim(p));
      const add=(fs,xf)=>{ for(const f of fs){ f.v=f.v.map(xf); f.tag=tag; if(f.db==null||f.db===0) f.db=0.01; out.push(f); } };
      const C=(dy,dz)=>[x0, yG+dy*k, zT+dz*k], Hs=(a,b,c)=>[a*k,b*k,c*k];
      add(box(C(0.015,-0.04),Hs(0.12,0.03,0.11),'drv',0.0), steer);
      add(box(C(-0.05,-0.03),Hs(0.10,0.05,0.10),'drv',0.1), full);
      add(box(C(-0.15,-0.06),Hs(0.085,0.10,0.12),'drv',0.25), full);
      add(box(C(-0.13,0.08),Hs(0.07,0.085,0.028),'drv',0.55), full);
      (function(){
        const t0=zT-0.18*k, t1=zT-0.48*k;
        const a=[[-0.055,-0.05],[0.055,-0.05],[0.055,-0.23],[-0.055,-0.23]], b=[[-0.045,-0.09],[0.045,-0.09],[0.045,-0.26],[-0.045,-0.26]];
        const fs=[], P=(pt,z)=>[x0+pt[0]*k, yG+pt[1]*k, z];
        for(let q=0;q<4;q++){ const q2=(q+1)%4;
          faceO(fs,[P(a[q],t0),P(a[q2],t0),P(b[q2],t1),P(b[q],t1)],'drv',0.05,0,[(a[q][0]+a[q2][0])/2,(a[q][1]+a[q2][1])/2+0.15,0]); }
        add(fs, full);
      })();
      add(box(C(-0.18,-0.34),Hs(0.13,0.17,0.008),'drv',0.55), full);
      add(tube(C(-0.06,-0.54),C(-0.35,-0.54),0.055*k,'drv',0.1), full);
      add(box(C(-0.22,-0.67),Hs(0.010,0.05,0.08),'drv',-0.2), full);
      add(tube(C(-0.35,-0.54),C(-0.48,-0.54),0.032*k,'steel',0.3), full);
      const pa=(s.prop||0)*DEG, blades=[];
      for(const [yy,ph,dn] of [[-0.39,0,1],[-0.46,Math.PI/3,-1]]){
        for(let q=0;q<3;q++){
          const a=dn*pa+ph+q*2*Math.PI/3, r0=0.035*k, r1=0.15*k, w0=0.024*k, w1=0.044*k, y=yG+yy*k, zc=zT-0.54*k;
          const e=[Math.cos(a),Math.sin(a)], t=[-Math.sin(a),Math.cos(a)];
          const P=(r,w)=>[x0+e[0]*r+t[0]*w, y, zc+e[1]*r+t[1]*w];
          blades.push({v:[P(r0,-w0),P(r1,-w1),P(r1,w1*0.6),P(r0,w0)], mat:'steel', b:0.35, db:0.01});
        }
      }
      add(blades, full);
    }
  }

  // steering wheel (tag 1): sport wheel on the helm bulkhead, raked toward the helm seat
  // 10-03: hub (0.52, dashY(0.52)-0.08 = -0.619, 1.62), rake +25 (top toward the bow): 0.92 m ahead of the seat,
  // where 10.3's drive clip reaches 0.45 m (0 of 30). Now CR_HUB: (0.52, -1.14, 1.96), rake -20 (top toward the
  // helmsman), its column along the axis into a helm pod on the dash: seated, 30 of 30 builds on 10.3
  const WHEEL_HUB = { x:CR_HUB.x, y:CR_HUB.y, z:CR_HUB.z };
  const WHEEL_GEO = { rad:0.19, rimIn:0.158, rake:CR_HUB.rake, seg:16, hubR:0.05, spokes:[0,180,270], col:CR_HUB.col };
  const WHEEL_LOCK = 1.75;
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
    return { sunroof:clamp01(+o.sunroof||0), hatch:clamp01(+o.hatch||0), door:clamp01(+(o.doorOpen!=null?o.doorOpen:o.door)||0), gate:clamp01(+o.gate||0),
             steer:Math.max(-1,Math.min(1,+o.steer||0)), trim:clamp01(o.trim!=null?+o.trim:0.25), tilt:clamp01(+o.tilt||0),
             prop:+o.prop||0, drive:o.drive!==false, wheel:o.wheel!==false, roof:o.roof!==false };
  }
  const _asm=new Map();
  function assemble(s){
    const key=[r3(s.hatch),r3(s.gate),s.roof?r3(s.sunroof):'-',
               s.drive?[r3(s.steer),r3(s.trim),r3(s.tilt),Math.round(s.prop)].join(','):'-', s.wheel?r3(wheelDeg(s)):'-'].join('|');
    if(_asm.has(key)) return _asm.get(key);
    const dyn=[], SH=[];
    hatchFaces(s.hatch, dyn);                     // tags itself: the lid main_deck/fit (its top a floor), the bay hull/part
    // the companionway door is the leaf now, doorFaces(t), drawn beside the assembly (the new layout)
    const _g=dyn.length;
    gateFaces(s.gate, dyn);
    stampTags(dyn, _g, dyn.length, 'main_deck', 'wall', { steel:'trim' });
    if(s.roof) roofFaces(s.sunroof, dyn);         // tags itself: top/roof and top/fit
    const _d=dyn.length;
    if(s.drive) driveFaces(s, dyn, 2);
    stampTags(dyn, _d, dyn.length, 'hull', 'part');
    const _w=dyn.length;
    if(s.wheel) for(const f of wheelFaces(wheelDeg(s))) dyn.push(f);
    stampTags(dyn, _w, dyn.length, 'house', 'fit');
    // the roof's shade on the helm deck: cast along the key at every heading, kept inside the cockpit
    SH.push({ v:HT_OUT.map(([x,y])=>[x,y,HT.ZU]), z0:HDECK, steps:1, near:0.75, clip:[-1.30,1.30,-2.36,-0.40] });
    const A={ F:F.concat(dyn), SH };
    _asm.set(key,A); if(_asm.size>64) _asm.delete(_asm.keys().next().value);
    return A;
  }

  // ---- rasterizer (fleet recipe; yaw in the basis as on the RHIB) ----------------------------------
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
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
  function _tris(rv, fn){
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
        fn(x, y, w0*a.d+w1*b.d+w2*c.d);
      }
    }
  }
  function _paint(faces, opts, doEdge, shadows){
    const B=camBasis(opts), N=W*H;
    const pal=palette(opts), MATS=opts.mats||pal.mats, RINDEX=opts.mats ? nlRindex(opts.mats, pal.rindex) : pal.rindex;
    const zbuf=new Float32Array(N).fill(Infinity), col=new Array(N).fill(null), dep=new Float32Array(N), tag=new Int8Array(N);
    for(const f of faces){
      const rv=f.v.map(p=>projVert(p[0],p[1],p[2],B));
      const n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n,B.se,B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]],B.se,B.ce)*0.9;
      const fidx=sh*GAIN+BIAS+(f.b||0), M=MATS[f.mat]||MATS.deck;
      const base=Math.floor(fidx), frac=Math.max(0,Math.min(1,fidx-base)), dith=M.dith==null?1:M.dith;
      const db=f.db||0, ft=f.tag||0, ramp=M.ramp, rl=ramp.length-1, off=M.off;
      _tris(rv, (x,y,d)=>{
        const deff=d-db, i=y*W+x;
        if(deff<zbuf[i]){
          zbuf[i]=deff; dep[i]=d; tag[i]=ft;
          const thr = dith<=0 ? 0.5 : 0.5+(BAYER[x&3][y&3]-0.5)*dith;
          const idx=base+(frac>=thr?1:0)+off;
          col[i]=ramp[idx<0?0:idx>rl?rl:idx];
        }
      });
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
    // cast shade: the caster outline is dropped along the key onto its receiving plane (heading-aware,
    // poses ignored), clipped to a plan box; it darkens what lies at that plane or up to `near` above it
    const Lr=[LN[0], B.se*LN[1]-B.ce*LN[2], B.ce*LN[1]+B.se*LN[2]], kx=-Lr[0]/Lr[2], ky=-Lr[1]/Lr[2];
    for(const sq of (opts.onlyTag ? [] : (shadows||[]))){
      const cl=sq.clip, near=sq.near||0.06, steps=sq.steps||1;
      const rv=sq.v.map(p=>{ const h=p[2]-sq.z0, oxr=h*kx, oyr=h*ky;
        let x=p[0]+oxr*B.ct+oyr*B.stt, y=p[1]-oxr*B.stt+oyr*B.ct;
        if(cl){ x=Math.max(cl[0],Math.min(cl[1],x)); y=Math.max(cl[2],Math.min(cl[3],y)); }
        return projVert(x,y,sq.z0,B); });
      const hit=new Uint8Array(N);
      _tris(rv, (x,y,sd)=>{
        const i=y*W+x; if(hit[i] || !out[i] || tag[i]) return;
        if(dep[i] < sd-near || dep[i] > sd+0.45) return;
        hit[i]=1; const e=RINDEX[out[i]]; if(e && e.i>0) out[i]=e.r[Math.max(0,e.i-steps)];
      });
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
    const s=stateOf(opts), A=assemble(s);
    return _toRGBA(_paint(A.F.concat(doorFaces(s.door)), Object.assign({}, opts, {dir}), true, A.SH));   // the leaf: doorFaces(t)
  }
  // layers: painted WITH the hull, then masked to their own pixels
  function layer(tagN, force){ return function(dir, opts){
    opts=norm(opts);
    const s=Object.assign(stateOf(opts), force), A=assemble(s);
    return _toRGBA(_paint(A.F.concat(doorFaces(s.door)), Object.assign({}, opts, {dir, onlyTag:tagN}), true));
  }; }
  const renderWheel=layer(1,{wheel:true}), renderDrive=layer(2,{drive:true}), renderRoof=layer(3,{roof:true});
  function bounds(dir, opts){
    opts=norm(opts);
    const A=assemble(Object.assign(stateOf(opts),{roof:true,drive:true,wheel:true})), B=camBasis(Object.assign({}, opts, {dir}));
    let x0=1e9,y0=1e9,x1=-1e9,y1=-1e9;
    for(const f of A.F.concat(doorFaces(stateOf(opts).door))) for(const p of f.v){ const q=projVert(p[0],p[1],p[2],B); if(q.sx<x0)x0=q.sx; if(q.sx>x1)x1=q.sx; if(q.sy<y0)y0=q.sy; if(q.sy>y1)y1=q.sy; }
    return { x0, y0, x1, y1 };
  }

  // ---- anchors (hull-cell px), metres published beside them ---------------------------------------
  const _at=(P,dir,opts)=>{ const o=Object.assign({}, norm(opts), {dir}); const p=projVert(P.x,P.y,P.z,camBasis(o)); return { x:p.sx, y:p.sy }; };
  const _anchor=(P)=>function(dir,opts){ return _at(P,dir,opts); };
  const many=(list)=>function(dir,opts){ return list.map(P=>_at(P,dir,opts)); };
  const r2=(v)=>Math.round(v*100)/100;
  const HELM      = { x:0.52, y:-1.56, z:SEAT.zs+0.08 };
  const PASSENGER = { x:-0.52, y:-1.56, z:SEAT.zs+0.08 };
  const SEATS = [
    Object.assign({ id:'helm' }, HELM), { id:'helm_outboard', x:0.94, y:-1.56, z:SEAT.zs+0.08 },
    Object.assign({ id:'companion' }, PASSENGER), { id:'companion_outboard', x:-0.94, y:-1.56, z:SEAT.zs+0.08 },
    { id:'bench_port', x:-0.82, y:-4.34, z:1.37 }, { id:'bench_centre', x:-0.22, y:-4.34, z:1.37 }, { id:'bench_stbd', x:0.34, y:-4.34, z:1.37 },
  ];
  const PILOT   = { x:0.52, y:-1.12, z:HDECK };
  const DRIVE_MOUNTS = DRIVE.xs.map(x=>({ x, y:DRIVE.yG, z:DRIVE.zT }));
  const PAINTER = { x:0, y:r2(yAt(1,1.00)+0.03), z:1.00 };
  const BOW_CLEAT = { x:0, y:r2(yOf(0.975)), z:r2(deckZ(0.975,0)+0.03) };
  const SPRING_CLEATS = [-1,1].map(s=>({ x:r2(s*(xEdge(0.47)-0.10)), y:r2(yOf(0.47)), z:r2(capZ(0.47)+0.03) }));
  const STERN_CLEATS  = [-1,1].map(s=>({ x:r2(s*(xEdge(0.03)-0.10)), y:r2(yOf(0.03)), z:r2(capZ(0.03)+0.03) }));
  const _yE=-L/2+FIL.R*(1-Math.cos(35*DEG));
  const STERN_EYES = [-1,1].map(s=>({ x:s*1.02, y:r2(_yE), z:r2(capZ(uOf(_yE))+0.024) }));
  const LADDER  = { x:-0.70, y:PL.y1+0.13, z:PL.zt };
  const PLATFORM = { x:0, y:(PL.y0+PL.y1)/2, z:PL.zt };
  const GATE_AT = { x:(GATE.x0+GATE.x1)/2, y:r2((GATE.y-L/2)/2), z:SOLE };
  const COMPANIONWAY = { x:(DASH.cw0+DASH.cw1)/2, y:r2(dashY(-0.30)+0.16), z:HDECK };
  const ENGINE_HATCH = { x:0, y:(HATCH.y0+HATCH.y1)/2, z:SOLE };
  const ANCHOR = { x:0, y:L/2+0.34, z:r2(capZ(1)-0.02) };
  const SUNROOF = { x:0, y:(HT.SR.y0+HT.SR.y1)/2, z:HT.ZE+HT.CR };
  const WATERLINE = WL;

  const SPEC = { reference:'31 ft express cruiser, hardtop, twin sterndrives (photos 1\u20132)', loa_hull:L, loa_platform:0.75, beam:3.05,
    deadrise_transom_deg:21, displacement_t:5.3, engine:'twin 6.2 L V8 petrol, 300 hp each', drive:'twin duoprop sterndrives',
    berths:'V-berth forward, mid-cabin under the helm deck', seats:SEATS.length,
    draft_drive_down:r2(WL-(DRIVE.zT-0.75*DRIVE.k)), draft_drive_up:r2(WL), bridge_clearance:r2(HT.ZE+HT.CR+0.15-WL), headroom_helm:r2(HT.ZU-HDECK) };

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
  // THE EXPRESS CRUISER IN THE NEW LAYOUT (ADR 0048 §8 A). Hull id 'express_cruiser', through
  // byId('express_cruiser'). A CABIN boat: one room under the helm deck and the cabin top, down a stair
  // through the companionway.
  //   levels  main_deck   the aft cockpit: its sole and the engine-hatch lid (floor), the liners and the stern
  //                       moulding (wall), the bench and the hatch's sides (fit), the transom door, the platform's top
  //           house       the helm deck one step up: its sole and the companionway's top tread (floor), the dash
  //                       top (roof), the dash, the companionway's lining, the helm-deck liners and the windscreen
  //                       (wall, its frame trim), the seats, the wet bar, the helm, the pod, the dark stair well and
  //                       the wheel (fit), and the companionway door (doorFaces(t))
  //           below       the cabin: the room (interiorFaces(), it:true): the mid-cabin aft under the helm deck,
  //                       the stair, the galley, the head, the hanging locker and the V-berth forward
  //           foredeck    the cabin top and the decks beside it forward of the windscreen (floor), its hatch,
  //                       windlass, bow cleat, pulpit and bow rail (fit)
  //           top         the hardtop (tag 3): its roof and the sunroof panel (roof), the rails, the radome and the
  //                       whip (fit); the arch legs (tall)
  //           hull        the shell, the platform's edges and underside, the deck cap, the decals and the cleats;
  //                       the engine bay with the hatch up and the sterndrives (part): never lifted
  //   cut     'deck'      the aft cockpit outside the hardtop, the transom channel, the platform, the foredeck:
  //                       nothing lifts
  //           'house'     on the helm deck and under the hardtop: the top lifts (its roof and the arch legs come back
  //                       as ghost)
  //           'below'     on the stair's lower half and in the cabin: the top, the house and the foredeck lift (their
  //                       shell, wall, roof, lining, part, tall and trim come back as ghost); the room shows
  //   parts   the wheel is face group tag 1 (state wheel:false drops it), the sterndrives tag 2 (drive:false), the
  //           hardtop tag 3 (roof:false); hatch, gate, sunroof, steer, trim, tilt and prop are state, as in 10-03;
  //           the companionway door's leaf is doorFaces(t), t 0 shut .. 1 open
  // ==================================================================================================
  const HULL_ID='express_cruiser', RIG_FILE='cruiserIsoRig.js', EXPORT_SYMBOL='CruiserIso', HULL_LABEL='Express cruiser 31';
  // the cabin (10-03's recreationInteriorRig planCruiser, moved into this rig): sole, the mid-cabin's bulkheads, the
  // berths. The fittings that stood outside her hull in 10-03 moved (CHANGES.md lists each): the V-berth's forward end
  // 3.95 -> 3.65 and the forward bulkhead 4.00 -> 3.70, where 10-03's stood forward of her raked forefoot (the hull
  // at the berth's base, z 0.68, ends at y 3.76). To keep 10-03's 2.2 m of berth (the tallest 10.3 build lies 2.06 m,
  // head pin to toes, with the pillow's 0.12) the V-berth starts 0.32 m further aft, 1.75 -> 1.43; the galley and the
  // head end there, 1.55 -> 1.43
  const CR = { sole:0.22, yAft:-2.30, yMidF:-0.56, yV0:1.43, yV1:3.65, yFwd:3.70, vtop:0.80, mtop:0.55, mattress:0.12 };
  // the lid over the cabin (outer surface): the helm deck aft of the companionway's foot, the dash top under the
  // windscreen, the cabin top forward of the windscreen's base. The room's ceiling is 0.05 m under the helm deck and
  // 0.06 m under the rest (10-03's)
  const CR_YF=dashY((DASH.cw0+DASH.cw1)/2), CR_YS=CR_YF+0.32, CR_YB=WS.YB-0.10;
  function crLid(x, y){
    if(y<CR_YF) return HDECK;
    const xc=Math.max(-WS.XB, Math.min(WS.XB, x)), wb=wsBY(xc);
    if(y<wb){ const t=clamp01((y-dashY(xc))/Math.max(0.05, wb-dashY(xc))); return lerp(dashZ(xc), wsBZ(xc), t); }
    return deckZ(uOf(y), Math.abs(x));
  }
  const crCeil=(x,y)=>crLid(x,y)-(y<CR_YF?0.05:0.06);
  // the liner's top on one side at y: as high as the ceiling allows over the liner's outer edge (its cap runs out to
  // the skin, and the side deck over it is lower than the cabin top), by bisection
  function crTop(side, y){ let lo=CR.sole, hi=3.2; for(let k=0;k<30;k++){ const m=(lo+hi)/2; if(crCeil(side*rmHalfAtZ(y,m,0),y)-0.015>=m) lo=m; else hi=m; } return lo; }
  const CR_HU=HDECK-0.06;                                 // the mid-cabin's bulkheads stop under the helm deck's ceiling
  const crHalf=(y,z)=>Math.max(0.04, rmHalfAtZ(y,z,1)-RM_WT-0.005);
  // the companionway flight (10-03's treads; the lower two moved to starboard as the V narrows): each tread a solid
  // block to the sole, a steel nosing on its forward edge
  const CR_STAIR = { down:1, treads:[
    { y0:CR_YF-0.02, y1:CR_YS, z:HDECK, x0:-0.60, x1:0.00 },
    { y0:CR_YS, y1:CR_YB, z:0.86, x0:-0.56, x1:0.12 },
    { y0:CR_YB, y1:CR_YB+0.22, z:0.64, x0:-0.48, x1:0.20 },
    { y0:CR_YB+0.22, y1:CR_YB+0.44, z:0.43, x0:-0.40, x1:0.28 } ] };
  // the head: a compartment to starboard on a raised floor (the V leaves no room for it at the sole)
  // (10-03: z1 2.02, through the side deck; its top is the liner's now, crHeadTop())
  const CR_HEAD = { side:1, xIn:0.42, y0:0.62, y1:1.43, door:{ y0:0.80, y1:1.26 }, floorZ:0.60 };   // 10-03: y1 1.55, door to 1.36
  const crHeadTop=()=>{ let z=9; for(let i=0;i<=8;i++) z=Math.min(z, crTop(1, lerp(CR_HEAD.y0, CR_HEAD.y1, i/8))); return q3(z); };
  const CR_TOILET = { x:0.60, y:0.95, z0:CR_HEAD.floorZ };

  // state: the 10-03 state object ({hatch, gate, sunroof, steer, trim, tilt, prop, drive, wheel, roof}) plus scheme /
  // paint, which pick the paints (matsFor). The faces are the same in every scheme. tower: none on this hull.
  function faceList(state, tower){ const o=state && typeof state==='object' ? state : {}; return assemble(stateOf(o)).F; }
  function partFaces(id, state){
    if(id==='door') return doorFaces(state);
    const tag=({ wheel:1, drive:2, roof:3 })[id]; return tag ? faceList(state).filter(f=>f.tag===tag) : [];
  }
  // the leaf: 10-03's companionway door (companionLeaf), shut at t 0; t 0..1 swings it aft about its port jamb through
  // DOOR.maxDeg. t is a number, or a state with doorOpen (or door).
  function doorFaces(t){
    if(t && typeof t==='object') t=t.doorOpen!=null ? t.doorOpen : t.door;
    const out=[]; companionLeaf(clamp01(+t||0), out);
    return stampTags(out, 0, out.length, 'house', 'wall', { steel:'trim' }, false);
  }
  // the room: one dir-free mesh, every face turned to the side it is seen from, tagged below / it:true
  let _room=null;
  function interiorFaces(){
    if(_room) return _room;
    const K=rmKit('below'), S=CR.sole;
    // soles: the cabin from the mid-cabin's bulkhead to the berth step, and the strip in front of the mid-cabin's berth
    K.floor(CR.yMidF, CR.yV0, S, S+0.02, 14, 'sole', -0.6);
    K.floor(-0.86, CR.yMidF, S, S+0.02, 3, 'sole', -0.6);
    // the liner: under the helm deck, under the dash and the cabin top, and over the V-berth from its base
    for(const side of [-1,1]){ const zHi=(y)=>crTop(side,y);
      K.liner(side, CR.yAft, CR_YF, S, zHi, 9, 'lining');
      K.liner(side, CR_YF, CR.yV0, S, zHi, 12, 'lining');
      K.liner(side, CR.yV0, CR.yFwd, CR.vtop-CR.mattress, zHi, 12, 'lining'); }
    // the mid-cabin: its aft bulkhead under the helm step, its forward bulkhead (seen from both sides) with the doorway
    // to starboard; 10-03 stopped the forward one at x -0.62, open to the void beside the stair: it runs to the hull now
    const door=[{ x0:0.06, x1:0.96, z0:S, z1:CR_HU-0.06 }];
    K.bulkhead(CR.yAft, S, ()=>CR_HU, [0,1,0], 'lining');
    K.bulkhead(CR.yMidF, S, ()=>CR_HU, [0,1,0], 'lining', door);
    K.bulkhead(CR.yMidF-0.02, S, ()=>CR_HU, [0,-1,0], 'lining', door);
    // the forward bulkhead, over the V-berth, under the cabin top's crown
    K.bulkhead(CR.yFwd, CR.vtop-CR.mattress, (x)=>crCeil(x, CR.yFwd)-0.01, [0,-1,0], 'lining');
    // caps on the mid-cabin's bulkheads: a section through them when the helm deck is lifted
    for(const [y0,y1] of [[CR.yAft, CR.yAft+0.03], [CR.yMidF-0.02, CR.yMidF]]){ const z=CR_HU+0.004, N=12;
      for(let i=0;i<N;i++){ const wa=crHalf(y1,CR_HU), xa=-wa+2*wa*i/N, xb=-wa+2*wa*(i+1)/N;
        K.put([[xa,y0,z],[xb,y0,z],[xb,y1,z],[xa,y1,z]], 'cut', 0.6, 0.03, 'trim', [0,0,1]); } }
    // the companionway flight
    K.stair(CR_STAIR, S);
    // the galley to port: counter, stove and sink (10-03's)
    K.cabinet({ side:-1, xIn:-0.42, y0:0.62, y1:1.43, top:S+0.90, topMat:'stone' }, S);     // 10-03: y1 1.55
    K.stove({ x0:-0.92, x1:-0.50, y0:0.70, y1:1.08, z:S+0.90 });
    K.sink({ x0:-0.90, x1:-0.54, y0:1.08, y1:1.38, z:S+0.90 });                             // 10-03: y 1.16 to 1.46
    // the head to starboard, on its raised floor; the toilet on that floor
    K.compartment(Object.assign({ z1:crHeadTop() }, CR_HEAD), S);
    K.toilet(CR_TOILET, S);
    // the hanging locker, starboard aft of the head
    K.tall({ side:1, xIn:0.60, y0:-0.12, y1:0.52, z1:1.72 }, S);   // 10-03: z1 1.84, through the side deck over it
    // the berth step, the V-berth, the mid-cabin's berth
    K.vblock(-0.30, 0.30, CR.yV0-0.22, CR.yV0, S, S+0.29, 'joinery', -0.2, 'fit', -1.2);
    K.vberth({ y0:CR.yV0, y1:CR.yV1, top:CR.vtop, mattress:CR.mattress }, S);
    K.midberth({ y0:CR.yAft+0.04, y1:-0.86, top:CR.mtop, mattress:CR.mattress }, S);
    _room=K.out; return _room;
  }
  function matsFor(id){
    const pal=palette(id && typeof id==='object' ? id : { scheme:id });
    return nlMatsTable(pal.id, Object.assign({}, pal.mats, rmMats(pal.mats, PAINT, 'acc')), RM_MATS);
  }
  function fitCheck(tol){ return rmFit(interiorFaces(), crLid, ()=>CR.yAft, tol); }
  // the walker's capsule in the V: radius R, a hemispherical foot on the floor at z0, H of body; the largest |x| its
  // centre reaches at y without touching the liner (halfAtZ less RM_WT)
  function crCapHalf(y, z0, H){ const R=NL_WALK.radius; let m=9;
    for(let h=0; h<=H+1e-9; h+=0.01){ const r=h>=R?R:Math.sqrt(Math.max(0,R*R-(R-h)*(R-h)));
      m=Math.min(m, rmHalfAtZ(y, z0+h, 1)-RM_WT-r); } return m; }
  const CR_TALL=1.87;                                     // the tallest 10.3 build's crown is 1.864 m
  function crMinCeil(poly, ceil){ let x0=9,x1=-9,y0=9,y1=-9; for(const [x,y] of poly){ x0=Math.min(x0,x); x1=Math.max(x1,x); y0=Math.min(y0,y); y1=Math.max(y1,y); }
    let z=Infinity; for(let i=0;i<=16;i++) for(let j=0;j<=8;j++){ const p=[lerp(x0,x1,j/8), lerp(y0,y1,i/16)]; if(nlPolyD(p,poly)<=1e-6) z=Math.min(z, ceil(p[0],p[1])); }
    return q3(z); }
  function crMaxCeil(poly, ceil){ let x0=9,x1=-9,y0=9,y1=-9; for(const [x,y] of poly){ x0=Math.min(x0,x); x1=Math.max(x1,x); y0=Math.min(y0,y); y1=Math.max(y1,y); }
    let z=-Infinity; for(let i=0;i<=16;i++) for(let j=0;j<=8;j++){ const p=[lerp(x0,x1,j/8), lerp(y0,y1,i/16)]; if(nlPolyD(p,poly)<=1e-6) z=Math.max(z, ceil(p[0],p[1])); }
    return q3(z); }
  // the hardtop's underside over the helm deck: htZ less its thickness; the sunroof well's lip hangs to HT.ZU
  const crRoofUnder=(x,y)=>{ const inWell=Math.abs(x)<=HT.SR.x+0.10 && y<=HT.SR.y0+0.10 && y>=HT.SR.y1-0.10; return Math.min(htZ(x)-HT.T, inWell?HT.ZU:9); };
  // the companionway door: hinged on the port jamb, swings aft over the helm deck (10-03's leaf)
  function crDoor(){
    const R=NL_WALK.radius, x0=DASH.cw0, x1=DASH.cw1, zt=dashZ((x0+x1)/2), w=x1-x0-0.02, yh=dashY(x0)-0.005;
    // clear when the walker, at the doorway's starboard limit (x1 - R), passes the leaf's free edge
    const clearAt=q3(Math.acos(Math.max(-1,Math.min(1,(x1-2*R-DOOR.xh)/(w+0.01))))/DEG/DOOR.maxDeg);
    const keepClear=[[q3(x0),q3(yh)]]; for(let k=0;k<=8;k++){ const a=k/8*DOOR.maxDeg*DEG; keepClear.push([q3(DOOR.xh+(w+0.01)*Math.cos(a)), q3(yh-(w+0.01)*Math.sin(a))]); }
    return { id:'companionway', kind:'hinge', face:'aft', y:q3(CR_YF), x0, x1, z0:HDECK, z1:q3(zt), sillZ:HDECK,
      hinge:{ x:DOOR.xh, y:q3(yh), z0:q3(HDECK+0.01), z1:q3(zt-0.02) }, maxDeg:DOOR.maxDeg, swings:'aft', opensTo:'aft',
      clearAt, keepClear, leaf:'doorFaces(t)', lv:'house', ro:'wall',
      clear:{ width:q3(x1-x0), height:q3(zt-HDECK) },
      note:'shut until opened; E opens it; t 0..1 swings the leaf aft over the helm deck about its port jamb ('+DOOR.maxDeg+'°). keepClear is the swept quarter: '+
        'the leaf’s collider is that arc. The doorway is '+q3(x1-x0)+' x '+q3(zt-HDECK)+' m: going below is down the stair (walk area companionway), '+
        'ducking under the soffit (waits on duck, stairDown, stairUp).' }; }
  function geometry(){
    const cab=crCabinPoly(), cz=crMinCeil(cab, crCeil), zFd=q3(deckZ(uOf(1.0),0));
    return {
      schema:'hidden-harbours/hull-geometry@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:HULL_ID, units:'m', frame:NL_FRAME,
      ids:Object.assign({}, LV), roles:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice(), dwlZ:WATERLINE,
      levels:[
        { id:'main_deck', deck:'cockpit', sole:SOLE, ceil:null, kind:'open', soleZ:SOLE, ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the aft cockpit is sky aft of the hardtop (y '+HT.Y1+'); forward of it the hardtop is the lid (level top), lifted under cut key house' } },
        { id:'house', deck:'helm_deck', sole:HDECK, ceil:q3(htZ(0)-HT.T), kind:'hard', soleZ:HDECK, ceilingZ:q3(HT.ZU),
          ceiling:{ kind:'hard', lid:'top', z:q3(htZ(0)-HT.T), of:'the hardtop (level top): its underside '+q3(htZ(0)-HT.T)+' on the centreline, '+q3(htZ(HT.XH)-HT.T)+
            ' at its edges; the sunroof well’s lip hangs to '+HT.ZU+' (ceilingZ, the lowest over the helm deck: '+q3(HT.ZU-HDECK)+' m clear). It lifts under cut key house.' } },
        { id:'below', deck:'cabin', sole:CR.sole, ceil:cz, kind:'hard', soleZ:CR.sole, ceilingZ:cz,
          ceiling:{ kind:'hard', lid:'house, foredeck', z:cz, of:'the helm deck over the mid-cabin, the dash over the stair, the cabin top over the cabin (levels house and foredeck); '+
            'lowest over the cabin sole: '+q3(cz-CR.sole)+' m clear. They lift under cut key below.' } },
        { id:'foredeck', deck:'cabin_top', sole:zFd, ceil:null, kind:'open', soleZ:zFd, ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'crowned, inside the bow rail. soleZ is the crown at y 1.0' } },
        { id:'top', deck:'hardtop', sole:q3(htZ(0)), ceil:null, kind:'open', soleZ:q3(htZ(0)), ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the hardtop: not a standing surface (see _excluded hardtop)' } },
      ],
      cutaway:{
        ghost:{ deck:[], house:['top'], below:['top','house','foredeck'] },
        gate:{ deck:[], house:['top'], below:['top','house','foredeck'] },
        shows:{ deck:null, house:null, below:'below' },
        note:'gate lists are complete lift sets in ghost’s vocabulary (whole levels). Under house the top lifts: its roof and the arch legs come back as ghost, '+
          'the rails, radome and whip do not. Under below the top, the house and the foredeck lift; their shell, wall, roof, lining, part, tall and trim come back as ghost, '+
          'their floor and fit (the helm deck, the cabin top, the seats, the helm, the dark stair well) do not.',
      },
      door:crDoor(),
      parts:[
        { id:'wheel', group:'tag', tag:1, state:'wheel', lv:'house', ro:'fit', hub:nlV([WHEEL_HUB.x,WHEEL_HUB.y,WHEEL_HUB.z]),
          axis:nlV([0,-Math.cos(WHEEL_GEO.rake*DEG),Math.sin(WHEEL_GEO.rake*DEG)]), rake_deg:WHEEL_GEO.rake, radius:WHEEL_GEO.rad,
          note:'turns about axis through hub (the axis points at the helmsman); wheelFaces(deg) draws it at any angle; its column runs '+WHEEL_GEO.col+' m up the axis into the helm pod' },
        { id:'drive', group:'tag', tag:2, state:'drive', lv:'hull', ro:'part', gimbals:DRIVE.xs.map(x=>nlV([x,DRIVE.yG,DRIVE.zT])),
          note:'twin legs: trim and tilt about the trim pins, steer about the vertical gimbal axis: state trim, tilt, steer, prop' },
        { id:'roof', group:'tag', tag:3, state:'roof', lv:'top', ro:'roof', alsoRoles:['fit'],
          note:'the hardtop: roof and sunroof panel (top/roof), rails, radome and whip (top/fit). roof:false drops it; the arch legs stay (top/tall)' },
        { id:'engine_hatch', group:'state', state:'hatch', lv:'main_deck', ro:'fit', hinge:nlV([0,HATCH.y1,HATCH.z]), maxDeg:HATCH.maxDeg,
          note:'hatch 0..1 lifts the aft sole (main_deck/floor; its sides and lift ring fit) about its forward edge; the bay, the engines, the genset and the rams (hull/part) are drawn only while it is up' },
        { id:'transom_door', group:'state', state:'gate', lv:'main_deck', ro:'wall', hinge:nlV([GATE.x0+0.01,GATE.y,GATE.z0]), maxDeg:GATE.maxDeg,
          note:'gate 0..1 swings the door aft down the channel toward the platform' },
        { id:'sunroof', group:'state', state:'sunroof', lv:'top', ro:'roof', travel:HT.SR.travel, note:'sunroof 0..1 slides the panel aft over the roof (part of the roof, tag 3)' },
        { id:'door', group:'doorFaces', lv:'house', ro:'wall', kind:'hinge', maxDeg:DOOR.maxDeg, opensTo:'aft', note:'doorFaces(t): see door' },
      ],
      states:{ hatch:'0..1', gate:'0..1', sunroof:'0..1', steer:'-1..1 (wheel and legs)', trim:'0..1 (default 0.25)', tilt:'0..1', prop:'blade phase, deg',
        drive:'true|false', wheel:'true|false', roof:'true|false', doorOpen:'0..1 (render option; the leaf is doorFaces(t))' },
    };
  }
  // the cabin's standing patch: between the galley and the head (their faces at x -0.42 / 0.42), from the stair's foot to
  // the berth step, held off the V by the walker's capsule
  function crCabinHalf(y){ return Math.min(0.42-NL_WALK.radius, crCapHalf(y, CR.sole, CR_TALL)); }
  const CR_CAB = { y0:q3(CR_STAIR.treads[3].y1), y1:q3(CR.yV0-0.22-NL_WALK.radius) };
  function crCabinPoly(){ return nlSimplify(nlSide(crCabinHalf, CR_CAB.y0, CR_CAB.y1, 14), 0.002); }
  function walk(){
    const R=NL_WALK.radius, S=CR.sole, D=crDoor();
    const xlS=(y)=>xl(uOf(y),SOLE)-R, xlH=(y)=>xl(uOf(y),HDECK)-R;
    // the aft cockpit: from the bench (its front at y -4.15) to the helm step; aft of the hardtop the cut is deck, under it house
    const yBench=-4.15+R, yRoof=HT.Y1;
    const aft=nlSimplify(nlSide(xlS, q3(yBench), q3(yRoof), 8));
    const aftRoof=nlSimplify(nlSide(xlS, q3(yRoof), q3(Y_STEP), 4));
    const hatchNote='the aft sole is the engine hatch (x ±'+HATCH.x+', y '+HATCH.y0+'..'+HATCH.y1+'): a floor while shut; with hatch > 0 it is the open bay, not a standing surface';
    // the transom channel: through the stern moulding to starboard, the transom door across it at y GATE.y
    const chan=nlRect(GATE.x0+R, -L/2, GATE.x1-R, yBench);
    // the swim platform, inset; its notch runs up to the channel's mouth
    const pw=PL.hw-R, k=PL.ch+R*(Math.SQRT2-1);
    const plat=[[-pw,PL.y0-R],[-pw,PL.y1+k],[-PL.hw+k,PL.y1+R],[PL.hw-k,PL.y1+R],[pw,PL.y1+k],[pw,PL.y0-R],
      [GATE.x1-R,PL.y0-R],[GATE.x1-R,PL.y0],[GATE.x0+R,PL.y0],[GATE.x0+R,PL.y0-R]].map(([x,y])=>[q3(x),q3(y)]);
    // the helm deck: behind the seats (the wet bar to port), the aisle between them, and forward of them to the dash,
    // port of the wheel and its pod (the rim, x ±0.19 about the hub, and the pod stand chest-high over the deck)
    const seatIn=SEAT.xc-SEAT.w/2, yBack=SEAT.y0-0.08, ySeatF=SEAT.y1+R;
    const wheelX0=q3(WHEEL_HUB.x-WHEEL_GEO.rad-R);
    const dashAt=(x)=>dashY(x)-R, xPort=-0.96;
    const helmAft=[[q3(-0.50+R),q3(Y_STEP)],[q3(xlH(Y_STEP+0.10)),q3(Y_STEP)],[q3(xlH(yBack-R)),q3(yBack-R)],[q3(seatIn-R),q3(yBack-R)],
      [q3(seatIn-R),q3(ySeatF)],[q3(-(seatIn-R)),q3(ySeatF)],[q3(-(seatIn-R)),q3(yBack-R)],[q3(-0.50+R),q3(yBack-R)]];
    const fwd=[[q3(xPort),q3(ySeatF)],[wheelX0,q3(ySeatF)]];
    for(let i=0;i<=6;i++){ const x=lerp(wheelX0, xPort, i/6); fwd.push([q3(x), q3(dashAt(x))]); }
    const helmFwd=nlSimplify(fwd, 0.002);
    // the companionway: a stair from the helm deck down to the cabin sole, its treads' strips (each tread inset by the walker)
    const TR=CR_STAIR.treads, yTop=q3(dashAt(-0.30)-0.02);
    const sR=[[q3(TR[0].x1-R),yTop],[q3(TR[0].x1-R),q3(TR[0].y1)],[q3(TR[1].x1-R),q3(TR[0].y1)],[q3(TR[1].x1-R),q3(TR[1].y1)],[q3(TR[2].x1-R),q3(TR[1].y1)],
      [q3(TR[2].x1-R),q3(TR[2].y1)],[q3(TR[3].x1-R),q3(TR[2].y1)],[q3(TR[3].x1-R),q3(TR[3].y1+0.11)]];
    const sL=[[q3(TR[3].x0+R),q3(TR[3].y1+0.11)],[q3(TR[3].x0+R),q3(TR[2].y1)],[q3(TR[2].x0+R),q3(TR[2].y1)],[q3(TR[2].x0+R),q3(TR[1].y1)],[q3(TR[1].x0+R),q3(TR[1].y1)],
      [q3(TR[1].x0+R),q3(TR[0].y1)],[q3(TR[0].x0+R),q3(TR[0].y1)],[q3(TR[0].x0+R),yTop]];
    const yM=(TR[0].y0+TR[0].y1)/2, yS=TR[3].y1+0.11;
    const cab=crCabinPoly(), cz=crMinCeil(cab, crCeil);
    // the foredeck: from the windscreen's foot forward, inside the bow rail (its stanchions stand 0.08 in from the edge
    // between u 0.555 and 0.935), to the walker's radius short of the windlass (build(): a box on the centreline at
    // u 0.955, 0.09 m each way fore and aft). Crowned up to 0.33 m, so cut into pieces, flat or ramped along x or y, each
    // holding the deck within 0.02 m. The deck hatch (build(): 0.62 m square at y 2.25, its lens 0.035 m over the
    // crown's top) is a hole in the pieces under it and an area of its own
    const railIn=(u)=>u>=0.555 && u<=0.935 ? xEdge(u)-0.08-0.018-R : xEdge(u)-R;
    const fdHalf=(y)=>railIn(uOf(y)), fdZ=(x,y)=>deckZ(uOf(y),Math.abs(x));
    const yWin=q3(yOf(0.955)-0.09-R);
    let yTip=2; while(yTip<yWin && fdHalf(yTip+0.01)>0.02) yTip+=0.01;
    const yFd0=q3(WS.YB+R), yFd1=q3(Math.min(yTip,yWin));
    const FH={ y:2.25, h:0.31 }, fhZ=q3(deckZ(uOf(FH.y),0)+0.012+0.023), fhPoly=nlRect(-FH.h, FH.y-FH.h, FH.h, FH.y+FH.h);
    const fd=nlSurfaceAreas('foredeck', 'deck', fdZ, fdHalf, yFd0, yFd1, 0.02);
    for(const a of fd.areas){ const xs=a.poly.map(p=>p[0]), ys=a.poly.map(p=>p[1]);
      if(Math.min(...xs)<FH.h && Math.max(...xs)>-FH.h && Math.min(...ys)<FH.y+FH.h && Math.max(...ys)>FH.y-FH.h) a.holes.push(fhPoly); }
    let fdSlope=0; for(let y=yFd0; y<=yFd1; y+=0.05){ const x=fdHalf(y); fdSlope=Math.max(fdSlope, Math.atan(Math.abs(fdZ(x+0.01,y)-fdZ(x-0.01,y))/0.02)/DEG); }
    fdSlope=Math.round(fdSlope);
    fd.areas[0].note='the foredeck (pieces foredeck_*): the cabin top and the decks beside it, from the windscreen’s foot ('+WS.YB+') to y '+yFd1+
      ' (the walker’s radius short of the windlass), inside the bow rail. Crowned up to '+q3(crown(uOf(yFd0)))+' m, so '+fd.areas.length+' pieces in '+fd.slices+
      ' slices, each flat or ramped along x or y and within '+fd.worst+' m of the deck; at the walk’s edge the crown slopes '+fdSlope+'° athwartships. '+
      'The deck hatch is a hole in the pieces under it and its own area. Reached from the helm deck by the washboards (Space).';
    const fdHatch={ id:'foredeck_hatch', cut:'deck', z:fhZ, poly:fhPoly, holes:[],
      note:'the deck hatch’s frame and lens (closed), '+q3(fhZ-fdZ(0,FH.y))+' m over the crown’s top: a step up from the foredeck round it' };
    const helmHead=q3(Math.min(crMinCeil(helmAft, crRoofUnder), crMinCeil(helmFwd, crRoofUnder))-HDECK);
    const areas=[
      { id:'aft_cockpit', cut:'deck', z:SOLE, poly:aft, holes:[], onlyWhen:{ hatch:0 }, note:'the aft cockpit sole, from the bench to the hardtop’s aft edge (y '+yRoof+'). '+hatchNote },
      { id:'aft_cockpit_roof', cut:'house', z:SOLE, poly:aftRoof, holes:[], onlyWhen:{ hatch:0 }, headroom:q3(crMinCeil(aftRoof, crRoofUnder)-SOLE), headroomMax:q3(crMaxCeil(aftRoof, crRoofUnder)-SOLE),
        note:'the aft cockpit under the hardtop, to the helm step (y '+Y_STEP+', 0.22 m up). '+hatchNote },
      { id:'transom_channel', cut:'deck', z:SOLE, poly:chan, holes:[], door:'transom_door',
        note:'the walk-through to starboard, from the platform through the stern moulding to the aft cockpit. The transom door stands across it at y '+GATE.y+
          ': a wall while shut; passable from gate '+crGateClear()+' (interact transom_door)' },
      { id:'swim_platform', cut:'deck', z:PL.zt, poly:plat, holes:[],
        note:'the integrated platform, '+q3(PL.zt-WATERLINE)+' m above the waterline; its stowed ladder lid is flush (see _excluded swim_ladder). Up 0.05 m into the channel' },
      { id:'helm_aft', cut:'house', z:HDECK, poly:helmAft, holes:[], headroom:helmHead, headroomMax:q3(crMaxCeil(helmAft, crRoofUnder)-HDECK),
        note:'the helm deck behind the seats (the wet bar to port) and the aisle between them (0.52 m: 10-03’s 0.36 m let no walker through; the seats moved, CHANGES.md)' },
      { id:'helm_fwd', cut:'house', z:HDECK, poly:helmFwd, holes:[], headroom:helmHead, headroomMax:q3(crMaxCeil(helmFwd, crRoofUnder)-HDECK),
        note:'the helm deck forward of the seats, port of the wheel and its pod, to the dash and the companionway. Headroom '+helmHead+' m under the sunroof well’s lip '+
          '(the tallest 10.3 build’s crown is 1.864); |x| > 0.966 is lower than 1.884 under the hardtop’s edge, so the area stops at x '+xPort },
      { id:'companionway', kind:'stair', cut:'house', cutHi:'house', cutLo:'below', zMid:0.70,
        ramp:{ axis:'y', a0:q3(yM), a1:q3(yS), z0:HDECK, z1:S }, poly:sR.concat(sL), holes:[], headroom:q3(dashZ((DASH.cw0+DASH.cw1)/2)-HDECK),
        waitsOn:['stairDown','stairUp','duck'],
        note:'down through the companionway (the door: house.door) and four treads ('+TR.map(t=>q3(t.z)).join(', ')+') to the cabin sole: the ramp holds the treads within 0.03 m. '+
          'Under the doorway’s soffit there is '+q3(dashZ((DASH.cw0+DASH.cw1)/2)-HDECK)+' m: going down waits on duck and stairDown, coming up on stairUp. The lower treads step to starboard as the V narrows.' },
      { id:'cabin_sole', cut:'below', z:S, poly:cab, holes:[], headroom:q3(cz-S), headroomMax:q3(crMaxCeil(cab, crCeil)-S),
        note:'the cabin sole between the galley and the head, from the stair’s foot to the berth step. '+q3(cz-S)+' m to the cabin top: every 10.3 build stands. '+
          'Its width follows the walker’s capsule in the V (the feet narrow it first): '+q3(2*crCabinHalf(CR_CAB.y1))+' m of centre at the berth step.' },
    ].concat(fd.areas, [fdHatch]);
    const spawnAt=[0,-3.4], spawn=areas.find(a=>a.cut==='deck' && nlPip(spawnAt, a.poly));
    const capMid=q3(crCapHalf(-0.71, S, CR_TALL)), passS=q3(crCapHalf(0.2, S, 1.0)-0.12);
    const _excluded=[
      { id:'midcabin_sole', of:'area', z:S, headroom:q3(crCeil(0,(CR.yMidF-0.86)/2)-S), reason:'the strip in front of the mid-cabin’s berth, '+q3(CR.yMidF+0.86)+' m deep and '+q3(CR_HU-S+0.01)+' m under the helm deck: '+
        'no room to stand. Getting there and onto the berth waits on crawl (the way in runs from the stair’s foot aft between the stair and the hull: about '+passS+' m beside the stair at the sole).',
        waitsOn:['crawl'] },
      { id:'bunk_aft', of:'berth', z:CR.mtop, reason:'the mid-cabin’s berth, athwartships under the helm deck: '+q3(CR_HU-CR.mtop)+' m to the deckhead. Getting onto it waits on crawl; sleeping is the bunk_aft interact.', waitsOn:['crawl'] },
      { id:'vberth', of:'berth', z:CR.vtop, reason:'the V-berth: '+q3(crCeil(0,2.6)-CR.vtop)+' m to the cabin top. Getting onto it (over the berth step) waits on crawl; sleeping is the bunk interact.', waitsOn:['crawl'] },
      { id:'berth_step', of:'area', z:q3(S+0.29), reason:'the step onto the V-berth, 0.22 m deep and 0.60 wide: narrower than the walker; part of getting onto the berth.', waitsOn:['crawl'] },
      { id:'head', of:'area', z:CR_HEAD.floorZ, reason:'the head compartment, on its floor '+q3(CR_HEAD.floorZ-S)+' m over the sole (more than stepTol), its top under the side deck at '+crHeadTop()+' ('+q3(crHeadTop()-CR_HEAD.floorZ)+' m over its floor): a seated space, entered by its interact, not walked.', waitsOn:['sit'] },
      { id:'seat_helm', of:'seat', at:nlPt(HELM), reason:'the helm seat: taken by the helm interact (drive).' },
      { id:'seat_helm_outboard', of:'seat', at:nlPt(SEATS[1]), reason:'the helm bench’s outboard place: sitting waits on sit (10.3 has no sit clip).', waitsOn:['sit'] },
      { id:'seat_companion', of:'seat', at:nlPt(SEATS[2]), reason:'as seat_helm_outboard.', waitsOn:['sit'] },
      { id:'seat_companion_outboard', of:'seat', at:nlPt(SEATS[3]), reason:'as seat_helm_outboard.', waitsOn:['sit'] },
      { id:'seat_bench_port', of:'seat', at:nlPt(SEATS[4]), reason:'the aft bench: sitting waits on sit.', waitsOn:['sit'] },
      { id:'seat_bench_centre', of:'seat', at:nlPt(SEATS[5]), reason:'as seat_bench_port.', waitsOn:['sit'] },
      { id:'seat_bench_stbd', of:'seat', at:nlPt(SEATS[6]), reason:'as seat_bench_port.', waitsOn:['sit'] },
      { id:'side_deck_pinch', of:'area', reason:'the side deck beside the windscreen, between the rail and the glass: 0.086 m at its narrowest (10-03). It is the washboard (washboards[]): climbed onto with Space and walked by the washboard’s own rule.' },
      { id:'hardtop', of:'area', z:q3(htZ(0)), reason:'the hardtop is not a standing surface: no rail, no way up (10-03).' },
      { id:'stern_moulding', of:'area', reason:'the moulding’s top round the bench and the channel: a fillet and a seat back, not a standing surface.' },
      { id:'stern_eyes', of:'cleat', at:nlPt(STERN_EYES[0]), reason:'the transom’s two eyes (anchors.sternEyes) are transom eyes, not mooring cleats: the stern lines go to the stern cleats (10-03).' },
      { id:'swim_ladder', of:'climb', kind:'ladder', at:nlPt(LADDER), reason:'the boarding ladder is drawn stowed (its flush lid on the platform, port side); coming aboard from the water waits on ladderOn and the ladder’s deployed state.',
        waitsOn:['ladderOn'] },
      { id:'midcabin_capsule', of:'note', reason:'the walker’s capsule at the mid-cabin’s door (y -0.71) reaches |x| '+capMid+' at the sole: the V, not the door, closes it.' },
    ];
    const inCab=(x,y)=>({ x:q3(x), y:q3(y), z:S });
    const sinkY=q3(CR_CAB.y1-0.03), headY=q3(Math.min(1.08, CR_CAB.y1-0.03)), cabX=(y)=>q3(crCabinHalf(y)-0.02);
    const interact=[
      { id:'helm', action:'enter_helm', label:'Helm', prompt:'Take the wheel', at:nlPt(WHEEL_HUB), seat:nlPt(HELM), stand:null, reach:{ x:0, y:q3(HELM.y), z:HDECK }, area:'helm_aft',
        note:'taken seated (drive: 30 of 30 on 10.3), from the aisle. Standing: 0 of 30 (10.3’s helm_idle takes no wheel position; see the fit); 10-03’s PILOT stand point is under the rim now' },
      { id:'seat_helm_outboard', action:'enter_ride', label:'Companion seat', prompt:'Ride', at:nlPt(SEATS[1]), reach:{ x:0, y:q3(HELM.y), z:HDECK }, area:'helm_aft', waitsOn:['sit'] },
      { id:'seat_companion', action:'enter_ride', label:'Companion seat', prompt:'Ride', at:nlPt(SEATS[2]), reach:{ x:0, y:q3(HELM.y), z:HDECK }, area:'helm_aft', waitsOn:['sit'] },
      { id:'seat_companion_outboard', action:'enter_ride', label:'Companion seat', prompt:'Ride', at:nlPt(SEATS[3]), reach:{ x:0, y:q3(HELM.y), z:HDECK }, area:'helm_aft', waitsOn:['sit'] },
      { id:'seat_bench_port', action:'enter_ride', label:'Aft bench', prompt:'Ride', at:nlPt(SEATS[4]), reach:{ x:q3(SEATS[4].x), y:-3.84, z:SOLE }, area:'aft_cockpit', waitsOn:['sit'] },
      { id:'seat_bench_centre', action:'enter_ride', label:'Aft bench', prompt:'Ride', at:nlPt(SEATS[5]), reach:{ x:q3(SEATS[5].x), y:-3.84, z:SOLE }, area:'aft_cockpit', waitsOn:['sit'] },
      { id:'seat_bench_stbd', action:'enter_ride', label:'Aft bench', prompt:'Ride', at:nlPt(SEATS[6]), reach:{ x:q3(SEATS[6].x), y:-3.84, z:SOLE }, area:'aft_cockpit', waitsOn:['sit'] },
      { id:'engine', action:'open_close_engine', label:'Engine hatch', prompt:'Open the engine hatch', state:'hatch', at:nlPt(ENGINE_HATCH), reach:{ x:0, y:-2.2, z:HDECK }, area:'helm_aft',
        note:'10-03’s reach: from the helm deck, over the hatch’s forward hinge' },
      { id:'transom_door', action:'open_close', label:'Transom door', prompt:'Open · shut', state:'gate', clearAt:crGateClear(), at:nlPt(GATE_AT), reach:{ x:q3((GATE.x0+GATE.x1)/2), y:q3(yBench-0.03), z:SOLE }, area:'transom_channel',
        note:'E opens and shuts it (gate 0..1); the channel is passable from gate '+crGateClear() },
      { id:'sunroof', action:'open_close', label:'Sunroof', prompt:'Slide the sunroof', state:'sunroof', at:nlPt(SUNROOF), reach:{ x:0, y:-1.1, z:HDECK }, area:'helm_aft' },
      { id:'companionway', action:'open_close', label:'Companionway door', prompt:'Open · shut', state:'doorOpen', at:nlPt({ x:(DASH.cw0+DASH.cw1)/2, y:CR_YF, z:(HDECK+D.z1)/2 }),
        reach:{ x:q3((DASH.cw0+DASH.cw1)/2), y:q3(dashAt((DASH.cw0+DASH.cw1)/2)-0.05), z:HDECK }, area:'helm_fwd',
        note:'E opens and shuts the leaf (doorFaces(t)); the doorway is clear from t '+D.clearAt },
      { id:'windlass', action:'anchor', label:'Windlass', prompt:'Let go / weigh anchor', at:nlPt({ x:0, y:4.3, z:2.281 }), reach:null, area:null,
        note:'10-03’s anchor and reach point; the reach stands on the foredeck piece under it' },
      { id:'swim', action:'enter_water', label:'Swim platform', prompt:'Swim', at:nlPt(PLATFORM), reach:{ x:0.5, y:q3(PLATFORM.y), z:PL.zt }, area:'swim_platform' },
      { id:'bunk', action:'sleep', label:'V-berth', prompt:'Sleep · save', at:nlPt({ x:0, y:(CR.yV0+CR.yV1)/2, z:CR.vtop }), reach:inCab(0, CR_CAB.y1-0.03), area:'cabin_sole' },
      { id:'bunk_aft', action:'sleep', label:'Mid-cabin berth', prompt:'Sleep', at:nlPt({ x:0, y:(CR.yAft+0.04-0.86)/2, z:CR.mtop }), reach:nlPt({ x:0, y:(-0.86+CR.yMidF)/2, z:S }), area:null, offWalk:'midcabin_sole', waitsOn:['crawl'],
        note:'the mid-cabin is reached by a crawl (see _excluded midcabin_sole)' },
      { id:'stove', action:'cook', label:'Stove', prompt:'Cook · brew up', at:nlPt({ x:-0.71, y:0.89, z:S+0.93 }), reach:inCab(-cabX(0.89), 0.89), area:'cabin_sole' },
      { id:'sink', action:'wash_dishes', label:'Galley sink', prompt:'Wash up', at:nlPt({ x:-0.72, y:1.23, z:S+0.93 }), reach:inCab(-cabX(sinkY), sinkY), area:'cabin_sole' },
      { id:'head', action:'toilet', label:'Head', prompt:'Use the head', at:nlPt({ x:CR_TOILET.x, y:CR_TOILET.y, z:CR_TOILET.z0+0.40 }), reach:inCab(cabX(headY), headY), area:'cabin_sole', waitsOn:['sit'],
        note:'the head’s door faces the cabin sole; its floor is '+q3(CR_HEAD.floorZ-S)+' m up (see _excluded head)' },
      { id:'locker', action:'storage', label:'Hanging locker', prompt:'Store · retrieve', at:nlPt({ x:(0.60+crHalf(0.2,1.42))/2, y:0.2, z:1.42 }), reach:inCab(cabX(CR_CAB.y0+0.05), CR_CAB.y0+0.05), area:'cabin_sole' },
    ];
    { const w=interact.find(i=>i.id==='windlass'), a=fd.areas.find(a=>nlPip([0,3.78],a.poly));
      w.reach={ x:0, y:3.78, z:q3(nlAreaZ(a,0,3.78)) }; w.area=a.id; }
    const cleats=[
      { id:'bow_1', type:'cleat', pos:nlV([BOW_CLEAT.x,BOW_CLEAT.y,BOW_CLEAT.z]) },
      { id:'mid_port', type:'cleat', pos:nlV([SPRING_CLEATS[0].x,SPRING_CLEATS[0].y,SPRING_CLEATS[0].z]) },
      { id:'mid_star', type:'cleat', pos:nlV([SPRING_CLEATS[1].x,SPRING_CLEATS[1].y,SPRING_CLEATS[1].z]) },
      { id:'stern_port', type:'cleat', pos:nlV([STERN_CLEATS[0].x,STERN_CLEATS[0].y,STERN_CLEATS[0].z]) },
      { id:'stern_star', type:'cleat', pos:nlV([STERN_CLEATS[1].x,STERN_CLEATS[1].y,STERN_CLEATS[1].z]) },
      { id:'bow_eye', type:'bow_eye', pos:nlV([PAINTER.x,PAINTER.y,PAINTER.z]), note:'the stem-face eye (the painter)' },
    ];
    const edge=(side)=>{ const e=[]; const ya=-0.5, yb=3.969; for(let i=0;i<=12;i++){ const y=lerp(ya,yb,i/12), u=uOf(y); e.push(nlV([side*xEdge(u), y, capZ(u)])); } return e; };
    const washboards=[
      { side:'port', width_m:SD, pinch_m:0.086, outer_edge:edge(-1), note:'the flat non-skid side deck, '+SD+' m inboard of the rail, from beside the windscreen forward: '+
        '0.086 m at its narrowest between the rail and the glass; forward of the screen’s base the cabin top beside it is the foredeck area. Climbed onto with Space from the helm deck.' },
      { side:'starboard', width_m:SD, pinch_m:0.086, outer_edge:edge(1), note:'as port' },
    ];
    return {
      schema:'hidden-harbours/hull-walk@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:HULL_ID, label:HULL_LABEL,
      kind:'cabin', units:'metres', frame:NL_FRAME,
      loa_m:q3(L+0.75), loh_m:L, beam_m:q3(2*T[3][1]), DWL:WATERLINE, cockpitZ:SOLE,
      cell:{ W, H, pivot:{ x:cx, y:cy }, pxPerM:PX, defaultElev:DEFAULT_ELEV, order:['N','NE','E','SE','S','SW','W','NW'] },
      tags:{ lv:Object.assign({}, LV), ro:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice() },
      walk:{ radius:R, stepTol:NL_WALK.stepTol, rule:NL_RULE, spawn:{ x:spawnAt[0], y:spawnAt[1], area:spawn?spawn.id:null, note:'in the aft cockpit' }, areas },
      house:Object.assign({ door:D, room:{ level:'below', soleZ:S, ceilingZ:cz, fitCheck:'qa.fitCheck' } }, nlHouseDecks(geometry().levels)),
      anchors:{ helm:nlPt(HELM), wheel:nlPt(WHEEL_HUB), pilot:nlPt(PILOT), seats:SEATS.map(s=>Object.assign({ id:s.id }, nlPt(s))),
        painter:nlPt(PAINTER), sternEyes:STERN_EYES.map(nlPt), ladder:nlPt(LADDER), platform:nlPt(PLATFORM), gate:nlPt(GATE_AT),
        companionway:nlPt(COMPANIONWAY), engineHatch:nlPt(ENGINE_HATCH), anchorRoller:nlPt(ANCHOR), sunroof:nlPt(SUNROOF), driveMounts:DRIVE_MOUNTS.map(nlPt),
        berths:[ nlBerth('vberth', 'bunk', 'vberth', CR.yV0, CR.yV1, CR.vtop, Math.min(crCeil(0,CR.yV0+0.3), crCeil(0,(CR.yV0+CR.yV1)/2), crCeil(0,CR.yV1-0.3))),
                 nlBerth('bunk_aft', 'bunk_aft', 'midberth', CR.yAft+0.04, -0.86, CR.mtop, CR_HU) ] },
      interact, cleats, washboards, _excluded,
      rock:{ frames:ROCK.frames, rollA:ROCK.rollA, pitchA:ROCK.pitchA, heaveA:ROCK.heaveA, period:ROCK.period },
      qa:{ fitCheck:fitCheck(), surfaceFit:{ foredeck:fd.worst, tol:0.02 } },
    };
  }
  // the transom door is clear when the walker, at the channel's starboard limit (GATE.x1 - R), passes its free edge
  function crGateClear(){ const R=NL_WALK.radius, w=GATE.x1-GATE.x0-0.03, xh=GATE.x0+0.01;
    return q3(Math.acos(Math.max(-1,Math.min(1,(GATE.x1-2*R-xh)/w)))/DEG/GATE.maxDeg); }
  const HULL={ id:HULL_ID, label:HULL_LABEL, kind:'cabin', faceList, partFaces, interiorFaces, doorFaces, geometry, matsFor, walk, fitCheck, render, renderCut,
    GAIN, BIAS, LN, BAYER, PX, W, H, pivot:{ x:cx, y:cy }, defaultElev:DEFAULT_ELEV, KEY,
    schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, DWL:WATERLINE, cockpitZ:SOLE, loa:L+0.75 };
  const HULLS={ express_cruiser:HULL };
  function byId(id){ return HULLS[id]||null; }

  root.CruiserIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV, KEYLINE_DEFAULT,
    order:['N','NE','E','SE','S','SW','W','NW'],
    render, renderWheel, renderDrive, renderRoof, bounds, allFaces:(o)=>{ const s=stateOf(o||{}); return assemble(s).F.concat(doorFaces(s.door)); }, wheelFaces, WHEEL_GEO, WHEEL_LOCK, WHEEL_HUB,
    ROCK, rock:rockMotion, PLANE, plane:planeMotion, TURN, turn:turnMotion, addPose:posesAdd,
    HT, HATCH, GATE, DOOR, DRIVE, PL, SEAT, SPEC, FIL, BENCH, stateOf,
    helmSeat:_anchor(HELM), passengerSeat:_anchor(PASSENGER), wheelHub:_anchor(WHEEL_HUB), pilotStand:_anchor(PILOT),
    driveMounts:many(DRIVE_MOUNTS), painter:_anchor(PAINTER), bowCleat:_anchor(BOW_CLEAT), springCleats:many(SPRING_CLEATS),
    sternCleats:many(STERN_CLEATS), sternEyes:many(STERN_EYES), ladder:_anchor(LADDER), platform:_anchor(PLATFORM),
    gate:_anchor(GATE_AT), companionway:_anchor(COMPANIONWAY), engineHatch:_anchor(ENGINE_HATCH), anchorRoller:_anchor(ANCHOR),
    sunroof:_anchor(SUNROOF), seats:many(SEATS),
    HELM, PASSENGER, SEATS, PILOT, DRIVE_MOUNTS, PAINTER, BOW_CLEAT, SPRING_CLEATS, STERN_CLEATS, STERN_EYES, LADDER, PLATFORM,
    GATE_AT, COMPANIONWAY, ENGINE_HATCH, ANCHOR, SUNROOF,
    L, LOA:L+0.75, BEAM:2*T[3][1], SOLE, HDECK, DECK:SOLE, WATERLINE, station, sheerZ,
    SCHEMES, schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, SLOTS,
    palette, rampFrom, chipWall, C_CAP,
    PAINT, STEEL, GLAS, MOTO, KEY,
    // the new layout (ADR 0048 §8 A): one hull, through byId('express_cruiser'); the same names for the default hull
    byId, HULLS, hullIds:Object.keys(HULLS), defaultHull:HULL_ID, LV, RO, GHOST_ROLES,
    faceList, partFaces, interiorFaces, doorFaces, geometry, matsFor, MATS:matsFor(DEFAULT_SCHEME).MATS, walk, fitCheck, renderCut,
    GAIN, BIAS, LN, BAYER, cell:{ W, H, pivot:{ x:cx, y:cy }, PX, defaultElev:DEFAULT_ELEV } };
})(typeof globalThis!=='undefined'?globalThis:window);

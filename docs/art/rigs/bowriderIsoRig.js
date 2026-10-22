/* Hidden Harbours — BOWRIDER, THE NEW LAYOUT (2026-10-09). ADR 0048 §8 A; the contract is
   docs/art/hull-layout-spec.md. One rig, one hull, through its global:
     BowriderIso.byId('bowrider') = { faceList(state,tower), partFaces(id,state), interiorFaces(),
       doorFaces(t), geometry(), matsFor(scheme), walk(), fitCheck(), GAIN, BIAS, LN, BAYER, PX, W, H,
       pivot, defaultElev, schemeIds, defaultScheme, DWL, cockpitZ, loa }
   and the same names on the global for the default hull. Every face carries lv (its level), ro (its
   role) and it (false on every face: she is an OPEN boat, with no room, no door leaf and no house).
   Her walk, docs/art/rigs/gameplay/bowriderIsoRig.bowrider.walk.json, is WRITTEN by
   tools/rig-recipes/hull-walk.mjs from walk(), never by hand, and stamped with this file's LF sha256.
   WHAT MOVED FROM THE 10-03 PASS: the steering wheel only. Its rim was raked 30 deg top toward the
   bow, hub y 0.055; 10.3's drive clip holds its hands on a rim tilted top toward the figure, so the
   hands missed by up to 0.10 m (0 of 30). Now rake -12 (top 12 deg toward the helmsman), hub y 0.020,
   and the column runs along the wheel's axis into the console: seated, 30 of 30 builds on 10.3.
   Every other face is the 10-03 face, vertex for vertex, in every scheme.
   The header below is the 10-03 rig's own, unchanged. */
/* Hidden Harbours — parametric ISO BOWRIDER (fibreglass sterndrive runabout) — M2 bake recipe,
   ADR-0006, same pipeline as sportSkiffIsoRig.js / consoleIsoRig.js / zodiacIsoRig.js. PASS 1.
   The harbour's first pleasure boat, built to the 18-ft sterndrive bowrider reference set (the
   185-class boat): 5.64 m hull + 0.34 m integrated swim platform, 2.29 m beam, 19 deg deadrise at
   the transom, a V6 under the sun pad driving an outdrive leg through the transom. Fixed 3/4
   turntable camera (elev 40 default), 45 deg steps, flat-facet shading from the fleet's upper-left
   key, z-buffered, ordered dither, NO AA, ringless from birth (ADR 0031: KEYLINE_DEFAULT=false,
   render(dir,{outline:true}) is the A/B). 32 px = 1 m.

   WHAT SHE CARRIES, stern to stem
     swim platform   integrated moulding, notched at the centre so the leg can tilt; flush ladder lid
     sterndrive      gimbal plate on the transom; the leg is a TAGGED LAYER (tag 2) that trims, tilts
                     and steers about the real axes — trim pins, then the vertical gimbal axis
     sun pad         full-width lounge over the engine. It IS the engine hatch: hatch 0..1 swings
                     it up on its forward hinge to 68 deg and opens the bay underneath. The folded
                     top lies on it, so opening the hatch lifts the top to its bundle (top >= 0.5)
     engine          only built when the hatch is open: V6 block, valve covers, manifolds and risers,
                     the round silver flame arrestor, alternator. Flywheel end at the transom
     aft bench       full width, backrest is the sun pad's forward face
     helm            starboard console with a black dash, gauge lens and visor; three-spoke wheel as
                     a TAGGED LAYER (tag 1) exactly like the skiffs; port console with a glovebox
     bucket seats    two, swivel pedestals, two-tone
     windscreen      wraparound, raked, walk-through centre panel over the passage to the bow
     bow             U-lounge on a rising sole, closed bow deck with a cleat, short bow rails
     bimini          three-bow folding top. top 0..1 is ONE NUMBER: 0 = folded and zipped in its boot
                     on the sun pad, 0.5 = bows bundled upright, 1 = spread and taut. The canvas is
                     inextensible: as the bows close it takes up the slack as hanging folds, so
                     every in-between frame is a real state of the same top, not a crossfade

   COLOURWAYS. Paint is data, as on the skiffs. render(dir,{scheme:'…'}) picks a preset;
   render(dir,{paint:{hull,deck,stripe,bottom,uph,acc,canvas,sole}}) mixes any base colours through
   the shared OKLCH mixer (rampFrom, chipWall, C_CAP=0.115 — same envelope as the fleet). hull is the
   topside colour band from the chine up to a sweep line that drops toward the bow; deck is the white
   above it and every moulded deck surface; stripe the pinstripe on the sweep; uph/acc the two-tone
   vinyl; canvas the bimini and its boot; sole the snap-in carpet. Four schemes are the fleet's own
   hull values (cranberry, tar black, bottle green, squall grey) so a mixed harbour still reads as one
   fleet. Canvas and carpet are held darker than the deck on purpose: the hull review's first finding
   was a pale top over a pale sole over pale furniture reading as one white tray.

   POSES. rock(i), plane(t) and turn(s) — she is a planing hull, so she gets the RHIB's pose set,
   including YAW in the camera basis. Feed one pose object to render, renderWheel and renderDrive.

   HULL-SKIN WINDING follows sportSkiffIsoRig.js vertex for vertex, so the topsides take the key the
   same way as the benchmark hull at every heading. Every other face is wound by an explicit outward
   vector (faceO), so no seat or liner panel shades as if it faced the wrong way.

   Cell 240x212, pivot (120,108) = boat origin (amidships of the 5.64 m hull, keel bottom,
   centreline), pinned every heading. The leg hangs below the keel, so the cell carries it. Swept
   over elev 30/40/50 x 8 headings x 27 poses (rock, plane, hard over and their sums) x three
   top/hatch/tilt states: 2 px or more clear of every cell edge.
   Exposes globalThis.BowriderIso = { W,H,PX,DIRS,pivot,order,defaultElev,KEYLINE_DEFAULT,
   render, renderWheel, renderDrive, wheelFaces, WHEEL_GEO, WHEEL_LOCK, ROCK,rock, PLANE,plane,
   TURN,turn, addPose, TOP, DRIVE, SPEC, anchors (helmSeat, passengerSeat, wheelHub, pilotStand,
   driveMount, painter, sternEyes, ladder, sunPad, seats) + their metre values, SCHEMES, schemeIds,
   defaultScheme, SLOTS, palette, rampFrom, chipWall, C_CAP, L, LOA, BEAM, DECK, WATERLINE }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 240, H = 212, cx = 120, cy = 108;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;

  // ---- poses -------------------------------------------------------------------------------------
  // 1.2 t of glass on a 2.29 m beam: livelier in roll than the 7 m skiffs, quick in pitch.
  const ROCK = { frames: 8, rollA: 4.2, pitchA: 2.4, heaveA: 1.4, period: 2.2 };
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a), yaw: 0 };
  }
  // planing: the bow climbs to the hump, then settles to a flat running trim — the reference hull
  // gets out of the hole with little bow lift, so the hump is lower than the RHIB's
  const PLANE = { frames: 12, humpAt: 0.36, humpPitch: 7.8, cruisePitch: 4.2, lift: 6 };
  function planeMotion(t){
    t = Math.max(0, Math.min(1, t||0));
    const hump = t < PLANE.humpAt ? Math.sin(0.5*Math.PI*t/PLANE.humpAt) : Math.pow(Math.max(0, 1-(t-PLANE.humpAt)/(1-PLANE.humpAt)), 1.5);
    const pitch = PLANE.cruisePitch*t + (PLANE.humpPitch - PLANE.cruisePitch*PLANE.humpAt)*hump;
    return { roll: 0, pitch, heave: PLANE.lift*t*t - 2.6*t*(1-t), yaw: 0 };
  }
  // hard over: banks into the turn, bow yawed inside the track
  const TURN = { maxRoll: 12, maxYaw: 7, pitch: 3.6, heave: 3 };
  function turnMotion(s){
    s = Math.max(-1, Math.min(1, s||0));
    return { roll: s*TURN.maxRoll, pitch: TURN.pitch, heave: TURN.heave, yaw: -s*TURN.maxYaw };
  }
  const posesAdd = (a,b)=>({ roll:(a.roll||0)+(b.roll||0), pitch:(a.pitch||0)+(b.pitch||0),
                             heave:(a.heave||0)+(b.heave||0), yaw:(a.yaw||0)+(b.yaw||0) });

  // ---- fixed ramps (never painted) ---------------------------------------------------------------
  const WHITE = '#eef0ea';
  const PAINT = ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];  // fleet white gelcoat, hand-tuned
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];            // stainless / aluminium
  const GLAS  = ['#16333c','#24505a','#3a7680','#5fa3a6','#8fc9c4'];                      // windscreen glass
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];  // drive, engine, rub insert
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
  const SCHEMES = {
    'summer-navy':   { name:'Summer Navy',   hull:'#23405f', deck:WHITE, stripe:'#c9a45a', bottom:WHITE,
                       uph:'#e8e2d0', acc:'#23405f', canvas:'#1f3148', sole:'#646c74',
                       note:'navy topsides under a white deck, gold pin, navy top' },
    'harbour-white': { name:'Harbour White', hull:WHITE, deck:WHITE, stripe:'#2ba39a', bottom:WHITE,
                       uph:'#e3dcc8', acc:'#2ba39a', canvas:'#2f5a55', sole:'#66706e',
                       note:'the fleet scheme \u2014 white glass, teal sweep, deep teal top' },
    'cranberry':     { name:'Cranberry',     hull:'#a8452f', deck:WHITE, stripe:'#2a2f33', bottom:WHITE,
                       uph:'#ece6d6', acc:'#a8452f', canvas:'#2a2f33', sole:'#5f6368',
                       note:'the fleet\u2019s bog red, black pin, black top' },
    'tar-black':     { name:'Tar Black',     hull:'#2a2f33', deck:WHITE, stripe:'#b9bfc0', bottom:WHITE,
                       uph:'#e6e3da', acc:'#3d454b', canvas:'#2a2f33', sole:'#585d61',
                       note:'the fleet\u2019s tar value, silver pin' },
    'champagne':     { name:'Champagne',     hull:'#c9b186', deck:WHITE, stripe:'#6b4f2a', bottom:WHITE,
                       uph:'#efe8d6', acc:'#b39468', canvas:'#7d6a4f', sole:'#72695c',
                       note:'tan band, brown pin, sand top' },
    'seafoam':       { name:'Seafoam',       hull:'#a3d9c8', deck:WHITE, stripe:'#1c7367', bottom:WHITE,
                       uph:'#e8e3d3', acc:'#2b8f86', canvas:'#1f4f4a', sole:'#5e6a69',
                       note:'the sport skiff\u2019s mint, deep teal pin' },
    'bottle-green':  { name:'Bottle Green',  hull:'#31694c', deck:WHITE, stripe:'#e0b13a', bottom:WHITE,
                       uph:'#e2dcc7', acc:'#31694c', canvas:'#2a4a3c', sole:'#5f665f',
                       note:'shared with the workboats, value for value' },
    'squall-grey':   { name:'Squall Grey',   hull:'#8a99a1', deck:WHITE, stripe:'#22354a', bottom:WHITE,
                       uph:'#d9dcd6', acc:'#22354a', canvas:'#22354a', sole:'#5b6466',
                       note:'the punt\u2019s grey-blue, navy pin and top' },
  };
  const DEFAULT_SCHEME = 'summer-navy';
  const SLOTS = ['hull','deck','stripe','bottom','uph','acc','canvas','sole'];

  const _pal={}; let _palOrder=[];
  function palette(o){
    o=o||{};
    const DF=SCHEMES[DEFAULT_SCHEME];
    const base = o.paint || SCHEMES[o.scheme] || DF;
    const deck=base.deck||DF.deck, hull=base.hull||DF.hull, stripe=base.stripe||DF.stripe;
    const bottom=base.bottom||deck, uph=base.uph||DF.uph, acc=base.acc||hull;
    const canvas=base.canvas||DF.canvas, sole=base.sole||DF.sole;
    const key=[hull,deck,stripe,bottom,uph,acc,canvas,sole].join('|');
    if(_pal[key]) return _pal[key];
    const r7=(hex)=>hex.toLowerCase()===WHITE ? PAINT : rampFrom(hex,7);
    const deckR=r7(deck);
    const hullR=hull===deck ? deckR : r7(hull);
    const botR=bottom===deck ? deckR : (bottom===hull ? hullR : r7(bottom));
    const stripeR=rampFrom(stripe,5,{anchor:0.75});
    const uphR=rampFrom(uph,5,{anchor:0.72});
    const accR=rampFrom(acc,5,{anchor:0.72});
    const canvR=rampFrom(canvas,5,{anchor:0.72});
    const soleR=rampFrom(sole,5,{anchor:0.70});
    // dith: 0 = crisp banded gelcoat and vinyl; the carpet and the moulded non-skid keep grain
    const mats = { paint:{ramp:hullR,off:0,dith:0},       deck:{ramp:deckR,off:0,dith:0},
                   nonskid:{ramp:deckR,off:-1,dith:0.22}, liner:{ramp:deckR,off:0,dith:0},
                   stripe:{ramp:stripeR,off:-1,dith:0},   bottom:{ramp:botR,off:0,dith:0},
                   uph:{ramp:uphR,off:0,dith:0.12},       acc:{ramp:accR,off:0,dith:0.10},
                   canv:{ramp:canvR,off:0,dith:0}, canvd:{ramp:canvR,off:-1,dith:0},     sole:{ramp:soleR,off:0,dith:0.40},
                   steel:{ramp:STEEL,off:0,dith:0},       glas:{ramp:GLAS,off:-2,dith:0},
                   moto:{ramp:MOTO,off:0,dith:0},         blk:{ramp:MOTO,off:-2,dith:0},
                   drv:{ramp:MOTO,off:1,dith:0},          eng:{ramp:MOTO,off:0,dith:0} };
    const rindex={};
    [hullR,deckR,botR,stripeR,uphR,accR,canvR,soleR,STEEL,GLAS,MOTO,PAINT].forEach(r=>r.forEach((c,i)=>{ if(!rindex[c]) rindex[c]={r,i}; }));
    const out={ key, id:o.paint?'custom':(SCHEMES[o.scheme]?o.scheme:DEFAULT_SCHEME), name:base.name||'Custom',
                base:{hull,deck,stripe,bottom,uph,acc,canvas,sole}, note:base.note||'',
                ramps:{hull:hullR,deck:deckR,stripe:stripeR,bottom:botR,uph:uphR,acc:accR,canvas:canvR,sole:soleR},
                mats, rindex };
    _pal[key]=out; _palOrder.push(key);
    if(_palOrder.length>32){ delete _pal[_palOrder.shift()]; }
    return out;
  }

  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- the hull -----------------------------------------------------------------------------------
  // Stations at explicit u (stern 0 -> stem 1), extra ones forward so the bow is round in plan — it
  // has to hold a U-lounge. [u, sheerHalf, chineHalf, keelHalf, chineRise, depth, keelZ] (m).
  // Deadrise at the transom = atan(0.33/(1.00-0.045)) = 19.1 deg. Beam 2.29 m at u 0.375.
  const L = 5.64, TH = 0.045, DECK = 0.34, NSEG = 26, NI = 22;
  const T = [
    [0.000, 1.075,1.000,0.045,0.33,0.98,0.02],
    [0.125, 1.110,1.030,0.045,0.35,0.99,0.01],
    [0.250, 1.135,1.040,0.040,0.38,1.00,0.00],
    [0.375, 1.145,1.020,0.040,0.42,1.02,0.00],
    [0.500, 1.130,0.960,0.035,0.47,1.05,0.00],
    [0.625, 1.070,0.840,0.030,0.52,1.09,0.02],
    [0.750, 0.950,0.650,0.030,0.56,1.13,0.08],
    [0.840, 0.800,0.480,0.040,0.54,1.08,0.18],
    [0.920, 0.580,0.300,0.035,0.46,0.98,0.32],
    [0.970, 0.340,0.170,0.030,0.36,0.89,0.43],
    [1.000, 0.120,0.080,0.030,0.27,0.82,0.52],
  ];
  const FC = 0.34, FK = 0.62, KB = 0.05, KB_MAX = 0.07;
  const lerp=(a,b,t)=>a+(b-a)*t;
  const smooth=(t)=>{ t=clamp01(t); return t*t*(3-2*t); };
  function station(u){
    u=Math.max(0,Math.min(1,u));
    let i=0; while(i<T.length-2 && u>T[i+1][0]) i++;
    const A=T[i], B=T[i+1], fr=(u-A[0])/(B[0]-A[0]);
    return { ws:lerp(A[1],B[1],fr), ch:lerp(A[2],B[2],fr), kw:lerp(A[3],B[3],fr),
             cd:lerp(A[4],B[4],fr), dep:lerp(A[5],B[5],fr), kz:lerp(A[6],B[6],fr), y:-L/2+u*L };
  }
  const RAKE=0.48;   // the forefoot tucks this far aft of the stem head: a raked sport stem
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
  // interior y at height z: the raked forefoot pulls the low part of every bow section aft, so
  // anything built low in the bow has to move aft with it or it pokes out through the stem
  const yAt=(u,z)=>{ const st=station(u); return st.y+rakeAt(u,(z-st.kz)/Math.max(1e-6,st.dep)); };

  // deck cap (the moulded gunwale), cockpit opening, floors
  const RUBIN = TH*0.76;                                  // rub rail cap depth — the deck cap starts inboard of it
  const capW=(u)=>0.15+0.04*smooth((u-0.62)/0.28);
  const xOut=(u)=>skin(1,u,1)[0];
  const capZ=(u)=>sheerZ(u)+0.010;
  const xo=(u)=>Math.max(0.04, xOut(u)-capW(u));          // cockpit opening half-width at the cap
  const U_BD=0.915;                                       // closed bow deck forward of this
  const U_SP=uOf(-1.90);                                  // engine box / sun pad forward edge
  const soleZ=(u)=>DECK+0.10*smooth((u-0.68)/0.22);       // the bow sole climbs with the forefoot
  const xl=(u,z)=>Math.min(xo(u), halfAtZ(u,z,1));        // liner half-width at height z — never outside the hull
  const floorZ=(u)=>u<U_SP ? 0.945 : soleZ(u);

  // colour sweep: the hull band runs from the chine up to a line near the rail aft that drops toward
  // the bow while the sheer climbs, so the flare forward is white — the family read of the type
  const fwd=(u)=>Math.pow(Math.max(0,(u-0.62)/0.38),1.2);
  const zCol=(u)=>{ const st=station(u), zc=lerp(0.80,0.60,smooth((u-0.40)/0.56));
    return Math.min(st.kz+st.dep-0.16, Math.max(st.kz+st.cd+0.07, zc)); };
  const F_COL=(u)=>Math.max(FC+0.015, fracAtZ(station(u), zCol(u)));
  const F_PIN=(u)=>Math.min(0.975, fracAtZ(station(u), zCol(u)+0.055+0.035*fwd(u)));
  const B_BOT=(u)=>-0.95+0.55*Math.pow(Math.max(0,(u-0.74)/0.26),1.3);
  const B_PIN=(u)=>0.25+0.95*fwd(u);
  const OB = [ [0, FC, 'bottom', B_BOT, 0],
               [FC, F_COL, 'paint', 0, 0],
               [F_COL, F_PIN, 'stripe', B_PIN, 0.004],
               [F_PIN, 1, 'deck', 0.15, 0] ];
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
  function nrm(v){ let nx=0,ny=0,nz=0;   // Newell: robust for any planar-ish polygon
    for(let i=0;i<v.length;i++){ const a=v[i], b=v[(i+1)%v.length];
      nx+=(a[1]-b[1])*(a[2]+b[2]); ny+=(a[2]-b[2])*(a[0]+b[0]); nz+=(a[0]-b[0])*(a[1]+b[1]); }
    return [nx,ny,nz]; }
  // push a face wound so its normal points along `want` (an outward vector in boat metres)
  function faceO(out, v, mat, b, db, want){
    const n=nrm(v); if(n[0]*want[0]+n[1]*want[1]+n[2]*want[2]<0) v=v.slice().reverse();
    out.push({v, mat, b:b||0, db:db||0});
  }

  // a box with its own top brightness: up-facing faces sit near the top of every ramp (shade 0.90
  // at any heading), so a cushion or a canvas reads at its base colour only with a deep top offset
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
  // lowest height at station u where the inner skin is at least x wide
  const zFloorAt=(u,x,z0)=>{
    if(xl(u,z0)>=x) return z0;
    let lo=z0, hi=capZ(u);
    for(let k=0;k<18;k++){ const m=(lo+hi)/2; if(xl(u,m)>=x) hi=m; else lo=m; }
    return hi;
  };
  // a transverse wall from x=xa out to the inner skin, za..zb: the outer edge walks the section, so no
  // lower corner of a bench, console or bulkhead punches out through the vee
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

  // ---- STATIC faces: everything that does not move ----------------------------------------------
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

  // swim platform, shared with the drive clearance maths
  const PL = { y0:-L/2, y1:-L/2-0.34, zt:0.54, zb:0.44, hw:0.95, ch:0.15, nW:0.24, nY:-L/2-0.15 };
  // windscreen plan: base curve along the dash, raked top
  const WS = { XE:0.93, YC:0.96, YE:0.36, zc:1.13, ze:0.05, rake:0.26, ht:0.34, htE:0.08, htE2:0.09, NS:14, sW:0.26, lw:0.16, corner:0.62 };
  const wsBase=(s)=>{ const a=Math.abs(s); return [s*WS.XE, WS.YC-(WS.YC-WS.YE)*Math.pow(a,1.7), WS.zc-WS.ze*s*s]; };
  function wsNorm(s){
    const e=1e-3, p0=wsBase(Math.max(-1,s-e)), p1=wsBase(Math.min(1,s+e));
    let n=[p1[1]-p0[1], -(p1[0]-p0[0])]; const m=Math.hypot(n[0],n[1])||1; n=[n[0]/m,n[1]/m];
    const b=wsBase(s); if(n[0]*b[0]+n[1]*(b[1]-0.2)<0) n=[-n[0],-n[1]];
    return n;
  }
  // the wings sweep down into the gunwale instead of ending as a tall pane standing on the rail
  const wsTop=(s)=>{ const b=wsBase(s), n=wsNorm(s), a=Math.abs(s), h=WS.ht-WS.htE*s*s-WS.htE2*Math.pow(a,6), r=WS.rake*(1-0.35*Math.pow(a,6));
    return [b[0]-n[0]*r, b[1]-n[1]*r, b[2]+h]; };
  // the glass stands ON the deck: a moulded ledge runs forward of the base curve from rail to rail,
  // so the dash and console tops carry past the glass instead of the glass sitting on a cliff edge
  const wsLedge=(s)=>{ const b=wsBase(s), n=wsNorm(s), w=WS.lw*(1-Math.pow(Math.abs(s),1.5))+0.025; return [b[0]+n[0]*w, b[1]+n[1]*w, b[2]]; };
  // console plan
  const CON = { xi:0.24, ya:0.08, za:1.02 };
  const dashFront=(x)=>wsLedge(Math.max(-1,Math.min(1,x/WS.XE)));
  const dashZ=(x,y)=>{ const f=dashFront(x); return lerp(CON.za, f[2], Math.max(0,Math.min(1,(y-CON.ya)/Math.max(0.05,f[1]-CON.ya)))); };

  (function build(){
    TAG('hull','shell',{steel:'trim',blk:'trim'});
    // ---- hull skin (skiff winding), spray rail, rub rail ----------------------------------------
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,fv(f0,u0)),skin(side,u1,fv(f0,u1)),
                skin(side,u1,fv(f1,u1)),skin(side,u0,fv(f1,u0))],mat,bv(b,u0),db);
        const srt=(u)=>0.050*(1-Math.pow(clamp01((u-0.78)/0.20),1.3));
        const ca=skin(side,u0,FC), cb=skin(side,u1,FC);
        const oc0=[ca[0]+side*srt(u0),ca[1],ca[2]+0.012], oc1=[cb[0]+side*srt(u1),cb[1],cb[2]+0.012];
        const dnp=(p)=>[p[0],p[1],p[2]-0.048];
        face([ca,cb,oc1,oc0],'paint',0.8,0.03);
        face([oc0,oc1,dnp(oc1),dnp(oc0)],'bottom',-1.25,0.03);
        const oa=skin(side,u0,1), ob2=skin(side,u1,1);
        const inb=(p)=>[p[0]-side*RUBIN,p[1],p[2]];
        const or0=[oa[0],oa[1],oa[2]+0.002], or1=[ob2[0],ob2[1],ob2[2]+0.002];
        face([or0,or1,inb(ob2),inb(oa)],'steel',0.8,0.04);
        face([or0,or1,[or1[0],or1[1],or1[2]-0.056],[or0[0],or0[1],or0[2]-0.056]],'blk',0.15,0.04);
      }
    }
    for(let i=0;i<NSEG;i++){   // keel pad, converging and brightening forward
      const u0=i/NSEG, u1=(i+1)/NSEG, kb=-1.0+1.0*Math.pow(Math.max(0,(u0-0.72)/0.28),1.2);
      face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'bottom',kb);
    }

    TAG('hull','shell');
    // ---- transom ------------------------------------------------------------------------------
    const tp=(s,f)=>skin(s,0,f);
    for(const [f0,f1,mat,b] of OB)
      face([tp(-1,fv(f0,0)),tp(1,fv(f0,0)),tp(1,fv(f1,0)),tp(-1,fv(f1,0))], mat, bv(b,0)-0.55, 0.005);
    TAG('hull','fit');
    boxF([0,-L/2-0.02,0.30],[0.15,0.02,0.12],'drv',-0.1,0.01);                 // gimbal housing plate
    for(const s of [-1,1]) boxF([s*0.80,-L/2-0.012,0.84],[0.030,0.012,0.022],'steel',0.3,0.02);   // stern eyes
    TAG('hull','trim');
    (function(){   // transom cap across the stern
      const x=xOut(0)-RUBIN, y0=-L/2, y1=-L/2+0.10, z=capZ(0);
      fo([[-x,y0,z],[x,y0,z],[x,y1,z],[-x,y1,z]],'deck',0.4,0,[0,0,1]);
    })();

    TAG('main_deck','floor',{bottom:'hull/shell',paint:'hull/shell',steel:'fit'});
    // ---- swim platform: two wings and a centre strip, the notch clears the tilted leg -----------
    (function(){
      const {y0,y1,zt,zb,hw,ch,nW,nY}=PL;
      const pieces=[ [[-hw,y0],[-hw,y1+ch],[-hw+ch,y1],[-nW,y1],[-nW,y0]],
                     [[-nW,y0],[-nW,nY],[nW,nY],[nW,y0]],
                     [[nW,y0],[nW,y1],[hw-ch,y1],[hw,y1+ch],[hw,y0]] ];
      for(const p of pieces){
        fo(p.map(([x,y])=>[x,y,zt]),'nonskid',0.25,0,[0,0,1]);
        fo(p.map(([x,y])=>[x,y,zb]),'bottom',-1.5,0,[0,0,-1]);
      }
      const rim=[[-hw,y0],[-hw,y1+ch],[-hw+ch,y1],[-nW,y1],[-nW,nY],[nW,nY],[nW,y1],[hw-ch,y1],[hw,y1+ch],[hw,y0]];
      for(let k=0;k+1<rim.length;k++){
        const a=rim[k], b=rim[k+1], e=[b[0]-a[0], b[1]-a[1]];
        const out=[e[1],-e[0],0];                    // right of a CCW outline = outward
        fo([[a[0],a[1],zb],[b[0],b[1],zb],[b[0],b[1],zt],[a[0],a[1],zt]],'paint',-0.25,0.002,out);
      }
      boxF([-0.60,y1+0.10,zt+0.006],[0.13,0.065,0.006],'steel',0.15,0.01);      // flush boarding-ladder lid
    })();

    TAG('hull','trim');
    // ---- deck cap: stern to the bow deck, both sides --------------------------------------------
    for(const side of [-1,1]){
      for(let i=0;i<NI;i++){
        const u0=U_BD*i/NI, u1=U_BD*(i+1)/NI;
        const y0=yOf(u0), y1=yOf(u1);
        const a0=[side*(xOut(u0)-RUBIN), y0, capZ(u0)], a1=[side*(xOut(u1)-RUBIN), y1, capZ(u1)];
        const b0=[side*xo(u0), y0, capZ(u0)+0.006], b1=[side*xo(u1), y1, capZ(u1)+0.006];
        fo([a0,a1,b1,b0],'deck',0.35,0,[0,0,1]);
      }
    }
    TAG('foredeck','floor');
    // closed bow deck forward of U_BD, converging on the stem head
    (function(){
      const N=7, TIP=0.998;
      for(let i=0;i<N;i++){
        const u0=U_BD+(TIP-U_BD)*i/N, u1=U_BD+(TIP-U_BD)*(i+1)/N;
        const w0=Math.max(0.012,xOut(u0)-RUBIN), w1=Math.max(0.012,xOut(u1)-RUBIN);
        fo([[-w0,yOf(u0),capZ(u0)],[w0,yOf(u0),capZ(u0)],[w1,yOf(u1),capZ(u1)],[-w1,yOf(u1),capZ(u1)]],'deck',0.30,0,[0,0,1]);
      }
      TAG('foredeck','fit');
      // anchor locker lid + bow cleat
      const ua=0.925, ub=0.958, la=Math.max(0.05,xOut(ua)-0.16), lb=Math.max(0.04,xOut(ub)-0.12);
      fo([[-la,yOf(ua),capZ(ua)+0.012],[la,yOf(ua),capZ(ua)+0.012],[lb,yOf(ub),capZ(ub)+0.012],[-lb,yOf(ub),capZ(ub)+0.012]],'deck',-0.9,-0.02,[0,0,1]);
      boxF([0,yOf(0.972),capZ(0.972)+0.016],[0.022,0.065,0.014],'steel',0.55,-0.03);
    })();

    TAG('main_deck','wall');
    // ---- liner: from the cap's inner edge down to the local floor, two bands --------------------
    function linerStrip(side,u0,u1,bands){
      const zt=(u)=>capZ(u)+0.006, zf=(u)=>floorZ(u);
      for(let k=0;k<bands.length;k++){
        const P=(u,j)=>{ const z=zt(u)-(zt(u)-zf(u))*j/bands.length; return [side*xl(u,z), yAt(u,z), z]; };
        fo([P(u0,k),P(u1,k),P(u1,k+1),P(u0,k+1)],'liner',bands[k],0,[-side,0,0.15]);
      }
    }
    for(const side of [-1,1]){
      for(let i=0;i<4;i++) linerStrip(side, U_SP*i/4, U_SP*(i+1)/4, [-0.8]);
      for(let i=0;i<NI;i++) linerStrip(side, U_SP+(U_BD-U_SP)*i/NI, U_SP+(U_BD-U_SP)*(i+1)/NI, [-0.7,-0.95]);
    }
    // bow deck aft face (the U-lounge's forward backrest wall)
    (function(){
      const u=U_BD, zt=capZ(u)+0.006, zf=soleZ(u);
      for(const s of [-1,1]) fo(wallPoly(u,0,zf,zt,s,0,(z)=>yAt(u,z)),'liner',-0.7,0,[0,-1,0]);
    })();

    TAG('main_deck','floor');
    // ---- carpet sole + ski locker ---------------------------------------------------------------
    (function(){
      const N=18, uA=U_SP-0.01;
      for(let i=0;i<N;i++){
        const u0=uA+(U_BD-uA)*i/N, u1=uA+(U_BD-uA)*(i+1)/N;
        const z0=soleZ(u0), z1=soleZ(u1), w0=xl(u0,z0)*0.985, w1=xl(u1,z1)*0.985;
        fo([[-w0,yAt(u0,z0),z0],[w0,yAt(u0,z0),z0],[w1,yAt(u1,z1),z1],[-w1,yAt(u1,z1),z1]],'sole',-2.9,0,[0,0,1]);
      }
      const z=DECK+0.008;
      fo([[-0.19,-1.30,z],[0.19,-1.30,z],[0.19,-0.12,z],[-0.19,-0.12,z]],'sole',-2.2,-0.01,[0,0,1]);   // ski locker lid
      TAG('main_deck','fit');
      boxF([0,-1.24,z+0.008],[0.05,0.022,0.008],'steel',0.5,-0.02);
    })();

    TAG('main_deck','fit');
    // ---- aft bench: base, cushion, backrest on the engine box face -------------------------------
    (function(){
      const ya=-1.88, yf=-1.42, zc=0.73, x=xl(uOf(-1.65),0.66)-0.012;
      for(const s of [-1,1]) fo(wallPoly(uOf(yf),0,DECK,zc-0.07,s,0.012),'liner',-0.25,0,[0,1,0]);   // base front
      fo([[-0.46,yf+0.003,DECK+0.07],[0.46,yf+0.003,DECK+0.07],[0.46,yf+0.003,DECK+0.25],[-0.46,yf+0.003,DECK+0.25]],'liner',-1.0,-0.01,[0,1,0]);  // storage door
      F.push.apply(F, slab([0,(ya+yf)/2+0.01,zc-0.035],[x-0.01,(yf-ya)/2+0.01,0.035],'uph',-1.8,0.1));             // cushion
      const q=(z0,z1,y0,y1)=>[[-x+0.02,y0,z0],[x-0.02,y0,z0],[x-0.02,y1,z1],[-x+0.02,y1,z1]];
      fo(q(zc,0.82,-1.885,-1.905),'uph',0.05,0,[0,1,0.3]);
      fo(q(0.82,0.90,-1.905,-1.925),'acc',0.05,0,[0,1,0.3]);
      fo(q(0.90,0.955,-1.925,-1.940),'uph',0.1,0,[0,1,0.3]);
    })();

    // ---- bucket seats: helm (starboard) and passenger ---------------------------------------------
    for(const sx of [-0.50,0.50]){
      tubeF([sx,-0.38,DECK],[sx,-0.38,0.62],0.045,'blk',0.2);
      boxF([sx,-0.38,DECK+0.012],[0.13,0.13,0.012],'blk',0.1);
      F.push.apply(F, slab([sx,-0.38,0.66],[0.225,0.215,0.036],'uph',-1.7,0.1));
      fo([[sx-0.12,-0.53,0.6975],[sx+0.12,-0.53,0.6975],[sx+0.12,-0.25,0.6975],[sx-0.12,-0.25,0.6975]],'acc',-2.3,0.012,[0,0,1]);
      const rk=(p)=>[p[0], p[1]-(p[2]-0.69)*0.16, p[2]];
      boxF([sx,-0.605,0.93],[0.225,0.05,0.24],'uph',0.05,0,rk);
      fo([rk([sx-0.13,-0.553,0.78]),rk([sx+0.13,-0.553,0.78]),rk([sx+0.13,-0.553,1.10]),rk([sx-0.13,-0.553,1.10])],'acc',0.05,0.012,[0,1,0.16]);
    }

    // ---- consoles and dash: strips across x, the front edge follows the windscreen base ---------
    for(const s of [-1,1]){
      const N=4, uA=uOf(CON.ya);
      const xOuter=xl(uA,CON.za)-0.012;
      for(let i=0;i<N;i++){
        const xa=CON.xi+(xOuter-CON.xi)*i/N, xb=CON.xi+(xOuter-CON.xi)*(i+1)/N;
        const fa=dashFront(xa), fb=dashFront(xb);
        const A=[s*xa,CON.ya,CON.za], B=[s*xb,CON.ya,CON.za], C=[s*xb,fb[1],fb[2]], D=[s*xa,fa[1],fa[2]];
        fo([A,B,C,D],'deck',0.5,0,[0,-0.2,1]);                                                           // dash top
        const zfa=Math.min(fa[2],zFloorAt(uOf(fa[1]),xa+0.012,DECK)), zfb=Math.min(fb[2],zFloorAt(uOf(fb[1]),xb+0.012,DECK));
        if(fa[2]-zfa>0.02 || fb[2]-zfb>0.02) fo([[s*xa,fa[1],fa[2]],[s*xb,fb[1],fb[2]],[s*xb,fb[1],zfb],[s*xa,fa[1],zfa]],'liner',-0.3,0,[0,1,0]);   // forward face
      }
      fo(wallPoly(uA,CON.xi,DECK,CON.za,s,0.012),'liner',-0.05,0,[0,-1,0]);   // aft face
      const fi=dashFront(CON.xi);
      fo([[s*CON.xi,CON.ya,DECK],[s*CON.xi,fi[1],DECK],[s*CON.xi,fi[1],fi[2]],[s*CON.xi,CON.ya,CON.za]],'liner',-0.35,0,[-s,0,0]);   // walk-through side
    }
    (function(){   // helm dash: black panel, gauge lens, visor
      const y0=CON.ya+0.04, y1=CON.ya+0.34, P=(x,y,dz)=>[x,y,dashZ(x,y)+dz];
      fo([P(0.34,y0,0.014),P(0.80,y0,0.014),P(0.80,y1,0.014),P(0.34,y1,0.014)],'blk',0.3,0,[0,-0.2,1]);
      fo([P(0.40,y0+0.07,0.022),P(0.74,y0+0.07,0.022),P(0.74,y0+0.19,0.022),P(0.40,y0+0.19,0.022)],'glas',0.9,0,[0,-0.2,1]);
      boxF([0.57,y1+0.02,dashZ(0.57,y1)+0.05],[0.25,0.04,0.045],'blk',0.35,-0.01);
      // port: glovebox door on the console's aft face + grab handle
      fo([[-0.36,CON.ya-0.004,0.70],[-0.80,CON.ya-0.004,0.70],[-0.80,CON.ya-0.004,0.94],[-0.36,CON.ya-0.004,0.94]],'liner',-0.95,-0.01,[0,-1,0]);
      tubeF([-0.40,CON.ya+0.07,1.075],[-0.78,CON.ya+0.07,1.075],0.02,'steel',0.6);
      // throttle box on the starboard liner
      boxF([0.86,-0.02,0.93],[0.035,0.07,0.06],'blk',0.2,-0.01);
      tubeF([0.86,0.00,0.98],[0.84,-0.06,1.10],0.016,'steel',0.4);
    })();

    TAG('main_deck','wall',{blk:'trim'});
    // ---- windscreen: raked wraparound glass, walk-through centre panel, black frame -------------
    (function(){
      const N=WS.NS;
      for(let i=0;i<N;i++){
        const s0=-1+2*i/N, s1=-1+2*(i+1)/N, n=wsNorm((s0+s1)/2);
        fo([wsBase(s0),wsBase(s1),wsTop(s1),wsTop(s0)],'glas',-0.35,0,[n[0],n[1],0.35]);
        tubeF(wsTop(s0),wsTop(s1),0.022,'blk',0.45);
        tubeF(wsBase(s0),wsBase(s1),0.018,'blk',0.25);                      // base rail, seated on the ledge
      }
      for(const s of [-1,1,-WS.sW,WS.sW,-WS.corner,WS.corner]) tubeF(wsBase(s),wsTop(s),Math.abs(s)===1?0.024:0.019,'blk',0.35);
      // the walk-through gap: the ledge returns into the opening as a moulded jamb each side
      for(const sg of [-1,1]){
        const b=wsBase(sg*WS.sW), f=wsLedge(sg*WS.sW);
        fo([[b[0],b[1],b[2]],[f[0],f[1],f[2]],[f[0],f[1],DECK+0.02],[b[0],b[1],DECK+0.02]],'liner',-0.45,0.002,[-sg,0,0]);
      }
    })();

    TAG('main_deck','fit');
    // ---- bow: U-lounge on the rising sole, backrests up the liner, short bow rails ---------------
    (function(){
      const UB0=uOf(1.12), N=10, SD=0.42;
      const zs=(u)=>soleZ(u)+0.38;
      const o=(u)=>xl(u,zs(u))-0.012, n=(u)=>Math.max(0,o(u)-SD);
      for(const s of [-1,1]){
        for(let i=0;i<N;i++){
          const u0=UB0+(U_BD-UB0)*i/N, u1=UB0+(U_BD-UB0)*(i+1)/N;
          const z0=zs(u0), z1=zs(u1), y0=yAt(u0,z0), y1=yAt(u1,z1);
          const o0=o(u0), o1=o(u1), n0=n(u0), n1=n(u1);
          fo([[s*n0,y0,z0],[s*o0,y0,z0],[s*o1,y1,z1],[s*n1,y1,z1]],'uph',-1.8,0,[0,0,1]);
          if(n0>0.01||n1>0.01){
            const yb0=yAt(u0,soleZ(u0)), yb1=yAt(u1,soleZ(u1));
            fo([[s*n0,y0,z0],[s*n1,y1,z1],[s*n1,y1,z1-0.06],[s*n0,y0,z0-0.06]],'uph',-0.2,0,[-s,0,0]);
            fo([[s*n0,y0,z0-0.06],[s*n1,y1,z1-0.06],[s*n1,yb1,soleZ(u1)],[s*n0,yb0,soleZ(u0)]],'liner',-0.7,0,[-s,0,0]);
          }
          // backrest up the liner, accent band at the top
          const zb0=z0+0.03, zb1=z1+0.03, zt0=capZ(u0)-0.06, zt1=capZ(u1)-0.06;
          const zm0=lerp(zb0,zt0,0.62), zm1=lerp(zb1,zt1,0.62);
          const X=(u,z)=>s*(xl(u,z)-0.035);
          fo([[X(u0,zb0),yAt(u0,zb0),zb0],[X(u1,zb1),yAt(u1,zb1),zb1],[X(u1,zm1),yAt(u1,zm1),zm1],[X(u0,zm0),yAt(u0,zm0),zm0]],'uph',-0.15,0.004,[-s,0,0.3]);
          fo([[X(u0,zm0),yAt(u0,zm0),zm0],[X(u1,zm1),yAt(u1,zm1),zm1],[X(u1,zt1),yAt(u1,zt1),zt1],[X(u0,zt0),yAt(u0,zt0),zt0]],'acc',0.0,0.004,[-s,0,0.3]);
        }
        // aft end of the side seat
        const z=zs(UB0), nn=n(UB0);
        fo(wallPoly(UB0,nn,z-0.06,z,s,0.012),'uph',-0.2,0,[0,-1,0]);
        fo(wallPoly(UB0,nn,soleZ(UB0),z-0.06,s,0.012),'liner',-0.4,0,[0,-1,0]);
      }
      // forward backrest on the bow deck's aft wall
      const u=U_BD, zb=zs(u)+0.03, zt=capZ(u)-0.06, zm=lerp(zb,zt,0.62), Y=(z)=>yAt(u,z)-0.035;
      for(const s of [-1,1]){
        fo(wallPoly(u,0,zb,zm,s,0.03,Y),'uph',-0.15,0.004,[0,-1,0.3]);
        fo(wallPoly(u,0,zm,zt,s,0.03,Y),'acc',0.0,0.004,[0,-1,0.3]);
      }
      TAG('hull','trim');
      // bow grab rails along the cap
      for(const s of [-1,1]){
        const U=[0.80,0.83,0.86,0.89], pts=U.map(u=>[s*(xOut(u)-0.10), yOf(u), capZ(u)+0.11]);
        for(let k=0;k+1<pts.length;k++) tubeF(pts[k],pts[k+1],0.026,'steel',0.6);
        for(const k of [0,3]) tubeF([pts[k][0],pts[k][1],capZ(U[k])],pts[k],0.022,'steel',0.25);
      }
      TAG('hull','fit');
      // bow eye on the raked stem
      boxF([0,2.44,0.66],[0.024,0.035,0.02],'steel',0.3,-0.03);
    })();

    TAG('hull','fit');
    // ---- stern cleats ---------------------------------------------------------------------------
    for(const s of [-1,1]){ const u=0.03; boxF([s*(xOut(u)-0.10),yOf(u),capZ(u)+0.016],[0.022,0.065,0.014],'steel',0.5,-0.02); }
    TAG(null);
  })();

  // ---- DYNAMIC parts ------------------------------------------------------------------------------
  // sun pad = engine hatch, hinged at its forward edge
  const SP = { y0:-2.74, y1:-1.94, zl:0.965, lt:0.05, hw:0.86, cz:0.07, maxDeg:68 };
  function hatchFaces(h, out){
    const a=h*SP.maxDeg*DEG, ca=Math.cos(a), sa=Math.sin(a), yh=SP.y1, zh=SP.zl;
    const xf=(p)=>{ const dy=p[1]-yh, dz=p[2]-zh; return [p[0], yh+dy*ca+dz*sa, zh-dy*sa+dz*ca]; };
    const push=(fs)=>{ for(const f of fs) out.push(f); };
    push(box([0,(SP.y0+SP.y1)/2,SP.zl-SP.lt/2],[SP.hw,(SP.y1-SP.y0)/2,SP.lt/2],'deck',0.2,0,xf));
    const cw=SP.hw-0.05, zc=SP.zl+SP.cz/2, hc=SP.cz/2;
    push(slab([0,(SP.y0+0.05-2.58)/2,zc],[cw,(-2.58-(SP.y0+0.05))/2,hc],'uph',-1.8,0.1,xf));
    push(slab([0,-2.515,zc],[cw,0.065,hc],'acc',-2.3,0.1,xf));
    push(slab([0,(-2.45+SP.y1-0.04)/2,zc],[cw,((SP.y1-0.04)-(-2.45))/2,hc],'uph',-1.8,0.1,xf));
    if(h<=0.001){   // closed: the gutter round the lid
      const N=4;
      for(let i=0;i<N;i++){
        const ya=-2.80+(SP.y1+0.04+2.80)*i/N, yb=-2.80+(SP.y1+0.04+2.80)*(i+1)/N;
        const wa=xl(uOf(ya),0.945), wb=xl(uOf(yb),0.945);
        faceO(out,[[-wa,ya,0.945],[wa,ya,0.945],[wb,yb,0.945],[-wb,yb,0.945]],'liner',-1.3,-0.005,[0,0,1]);
      }
      return;
    }
    // open: the bay, then the engine in it
    const BX=0.80, BY0=-2.78, BY1=SP.y1-0.02, BZ=0.32, TZ=0.945;
    faceO(out,[[-BX,BY0,BZ],[BX,BY0,BZ],[BX,BY1,BZ],[-BX,BY1,BZ]],'liner',-1.6,0,[0,0,1]);
    faceO(out,[[-BX,BY0,BZ],[BX,BY0,BZ],[BX,BY0,TZ],[-BX,BY0,TZ]],'liner',-0.9,0,[0,1,0]);
    faceO(out,[[-BX,BY1,BZ],[BX,BY1,BZ],[BX,BY1,TZ],[-BX,BY1,TZ]],'liner',-1.1,0,[0,-1,0]);
    for(const s of [-1,1]){
      faceO(out,[[s*BX,BY0,BZ],[s*BX,BY1,BZ],[s*BX,BY1,TZ],[s*BX,BY0,TZ]],'liner',-1.0,0,[-s,0,0]);
      const N=3;
      for(let i=0;i<N;i++){
        const ya=BY0+(BY1-BY0)*i/N, yb=BY0+(BY1-BY0)*(i+1)/N, wa=xl(uOf(ya),TZ), wb=xl(uOf(yb),TZ);
        faceO(out,[[s*BX,ya,TZ],[s*wa,ya,TZ],[s*wb,yb,TZ],[s*BX,yb,TZ]],'liner',-0.8,0,[0,0,1]);
      }
    }
    faceO(out,[[-BX,-2.80,TZ],[BX,-2.80,TZ],[BX,BY0,TZ],[-BX,BY0,TZ]],'liner',-0.8,0,[0,0,1]);
    // engine: flywheel end at the transom, pulleys facing the bench
    push(box([0,-2.36,0.52],[0.21,0.31,0.18],'eng',0.1));
    push(box([0,-2.70,0.50],[0.19,0.05,0.17],'eng',-0.1));
    for(const s of [-1,1]){
      push(box([s*0.17,-2.36,0.745],[0.095,0.27,0.05],'eng',0.45));
      push(box([s*0.31,-2.36,0.60],[0.05,0.26,0.075],'blk',0.2));
      push(box([s*0.31,-2.64,0.75],[0.055,0.055,0.10],'blk',0.3));
    }
    push(prism([0,-2.35,0.795],0.14,0.09,10,'steel',0.55,-0.01));
    push(box([0.19,-2.03,0.64],[0.065,0.055,0.065],'steel',0.1));
    push(box([0,-2.04,0.49],[0.09,0.02,0.09],'blk',0.2));
    push(box([-0.60,-2.12,0.44],[0.12,0.09,0.11],'blk',0.25));                  // battery box
    push(box([-0.64,-2.12,0.555],[0.02,0.02,0.012],'steel',0.5));
    push(box([-0.56,-2.12,0.555],[0.02,0.02,0.012],'steel',0.5));
  }

  // bimini: three bows on one gunwale hinge each side
  const TOP = {
    yP:-0.80, xT:0.90, R:0.024, camber:0.07, NX:6,
    bows:[ { len:1.30, up: 33, bundle: -4, stow:-86 },
           { len:1.10, up: -3, bundle: -8, stow:-84 },
           { len:1.32, up:-42, bundle:-12, stow:-82 } ],
    overF:0.30, dropF:0.04, overA:0.10, dropA:0.03, sagMax:0.16, bootAt:0.02,
    boot:{ y:-2.04, z:1.175, ry:0.16, rz:0.09, hx:0.88 },
  };
  TOP.uP=uOf(TOP.yP); TOP.xP=xo(TOP.uP)-0.03; TOP.zP=capZ(TOP.uP)+0.02;
  function bowAngles(t){
    return TOP.bows.map(b=> t<=0.5 ? lerp(b.stow,b.bundle,smooth(t/0.5)) : lerp(b.bundle,b.up,smooth((t-0.5)/0.5)));
  }
  function bowTop(i,th){ const b=TOP.bows[i], a=th*DEG; return [TOP.yP+b.len*Math.sin(a), TOP.zP+b.len*Math.cos(a)]; }
  const barPt=(yz,x)=>[x, yz[0], yz[1]+TOP.camber*(1-(x/TOP.xT)*(x/TOP.xT))];
  function canvasLine(t){   // front edge, three bars, rear edge — as [y,z] centreline tops
    const th=bowAngles(t), B=th.map((a,i)=>bowTop(i,a)), sp=smooth((t-0.5)/0.5);
    const dir=(a,b)=>{ const d=[a[0]-b[0],a[1]-b[1]], m=Math.hypot(d[0],d[1]); return m<0.05?null:[d[0]/m,d[1]/m]; };
    const df=dir(B[0],B[1])||[1,0], dr=dir(B[2],B[1])||[-1,0];
    const oF=0.04+(TOP.overF-0.04)*sp, oA=0.04+(TOP.overA-0.04)*sp;
    const Ef=[B[0][0]+df[0]*oF, B[0][1]+df[1]*oF-TOP.dropF*sp], Er=[B[2][0]+dr[0]*oA, B[2][1]+dr[1]*oA-TOP.dropA*sp];
    return { th, B, Ef, Er, sp };
  }
  const RAISED=canvasLine(1);
  const D0=[ [RAISED.Ef,RAISED.B[0]], [RAISED.B[0],RAISED.B[1]], [RAISED.B[1],RAISED.B[2]], [RAISED.B[2],RAISED.Er] ]
           .map(([a,b])=>Math.hypot(a[0]-b[0],a[1]-b[1]));
  function topFaces(t, out, shadows){
    const push=(fs)=>{ for(const f of fs) out.push(f); };
    const C=canvasLine(t), R=TOP.R;
    for(const s of [-1,1]){   // hinges + legs
      push(box([s*TOP.xP,TOP.yP,TOP.zP-0.012],[0.03,0.04,0.02],'steel',0.3));
      for(let i=0;i<3;i++) push(tube([s*TOP.xP,TOP.yP,TOP.zP],[s*TOP.xT,C.B[i][0],C.B[i][1]],R,'steel',0.45));
    }
    if(t<=TOP.bootAt){        // folded and zipped: the boot on the sun pad
      const bt=TOP.boot, K=8, ring=[];
      for(let k=0;k<K;k++){ const a=2*Math.PI*k/K; ring.push([bt.y+bt.ry*Math.cos(a), bt.z+bt.rz*Math.sin(a)]); }
      for(let k=0;k<K;k++){ const a=ring[k], b=ring[(k+1)%K], m=[(a[0]+b[0])/2-bt.y,(a[1]+b[1])/2-bt.z];
        faceO(out,[[-bt.hx,a[0],a[1]],[bt.hx,a[0],a[1]],[bt.hx,b[0],b[1]],[-bt.hx,b[0],b[1]]],'canvd',-0.9,0,[0,m[0],m[1]]); }
      for(const s of [-1,1]) faceO(out,ring.map(p=>[s*bt.hx,p[0],p[1]]),'canvd',-0.6,0,[s,0,0]);
      return;
    }
    for(let i=0;i<3;i++){      // top bars (under the canvas when taut, bare when folding)
      const xs=[-TOP.xT,-TOP.xT/2,0,TOP.xT/2,TOP.xT];
      for(let k=0;k+1<xs.length;k++) push(tube(barPt(C.B[i],xs[k]),barPt(C.B[i],xs[k+1]),R*0.9,'steel',0.5));
    }
    // canvas: four panels, inextensible — the slack between two closing bows hangs as a fold
    const lines=[C.Ef, C.B[0], C.B[1], C.B[2], C.Er];
    const NX=TOP.NX, xAt=(k)=>-TOP.xT+2*TOP.xT*k/NX;
    const edgeL=[], edgeR=[];
    for(let p=0;p<4;p++){
      const P=lines[p], Q=lines[p+1], d=Math.hypot(P[0]-Q[0],P[1]-Q[1]), d0=D0[p];
      const sag=Math.min(TOP.sagMax, Math.sqrt(Math.max(0, d0*d0/4-d*d/4)))+0.012;
      const M=[(P[0]+Q[0])/2, (P[1]+Q[1])/2-sag];
      const rows=[P,M,Q];
      for(let r=0;r<2;r++){
        for(let k=0;k<NX;k++){
          const a=rows[r], b=rows[r+1];
          faceO(out,[barPt(a,xAt(k)),barPt(a,xAt(k+1)),barPt(b,xAt(k+1)),barPt(b,xAt(k))],'canv',-2.3,0.01,[0,0,1]);
        }
      }
      if(p===0) { edgeL.push(P); }
      edgeL.push(M, Q);
    }
    // the canvas reads as fabric over a frame: a lit ridge where it is pulled over each bow, a darker
    // bound edge fore and aft. A blank slab of navy at 60 px is the T-top problem over again.
    const along=(a,b,d)=>{ const v=[b[0]-a[0],b[1]-a[1]], m=Math.hypot(v[0],v[1])||1; return [a[0]+v[0]/m*d, a[1]+v[1]/m*d]; };
    if(C.sp>0.15){
      for(let i=0;i<3;i++){
        const Bi=C.B[i], fa=along(Bi,lines[i],0.035), fb=along(Bi,lines[i+2],0.035);
        for(let k=0;k<NX;k++){
          const lift=(p)=>[p[0],p[1],p[2]+0.012];
          faceO(out,[lift(barPt(fa,xAt(k))),lift(barPt(fa,xAt(k+1))),lift(barPt(fb,xAt(k+1))),lift(barPt(fb,xAt(k)))],'canv',-1.4,0.02,[0,0,1]);
        }
      }
      for(const [E,N] of [[C.Ef,C.B[0]],[C.Er,C.B[2]]]){
        const Ei=along(E,N,0.05);
        for(let k=0;k<NX;k++){
          const lift=(p)=>[p[0],p[1],p[2]+0.006];
          faceO(out,[lift(barPt(E,xAt(k))),lift(barPt(E,xAt(k+1))),lift(barPt(Ei,xAt(k+1))),lift(barPt(Ei,xAt(k)))],'canvd',-2.3,0.015,[0,0,1]);
        }
      }
    }
    for(const s of [-1,1]){    // side hems
      for(let k=0;k+1<edgeL.length;k++){
        const a=barPt(edgeL[k],s*TOP.xT), b=barPt(edgeL[k+1],s*TOP.xT);
        faceO(out,[a,b,[b[0],b[1],b[2]-0.035],[a[0],a[1],a[2]-0.035]],'canvd',-0.9,0.012,[s,0,0]);
      }
    }
    if(C.sp>0.3 && shadows){   // contact shadow on the cockpit
      const q=[barPt(C.Ef,-TOP.xT),barPt(C.Ef,TOP.xT),barPt(C.Er,TOP.xT),barPt(C.Er,-TOP.xT)];
      shadows.push({ v:q.map(([x,y])=>[x+0.30,y-0.24,DECK+0.004]), steps:1 });
    }
  }

  // sterndrive leg: trim pins, then the vertical gimbal axis
  const DRIVE = { yG:-2.90, zT:0.34, steerMax:28, trimMin:-4, trimMax:12, tiltMax:38 };
  function driveFaces(s, out, tag){
    const tr=(DRIVE.trimMin+(DRIVE.trimMax-DRIVE.trimMin)*s.trim+DRIVE.tiltMax*s.tilt)*DEG;
    const st=s.steer*DRIVE.steerMax*DEG, ct=Math.cos(tr), stt=Math.sin(tr), cs=Math.cos(st), ss=Math.sin(st);
    const trim=(p)=>{ const dy=p[1]-DRIVE.yG, dz=p[2]-DRIVE.zT; return [p[0], DRIVE.yG+dy*ct+dz*stt, DRIVE.zT-dy*stt+dz*ct]; };
    const steer=(p)=>{ const dx=p[0], dy=p[1]-DRIVE.yG; return [dx*cs+dy*ss, DRIVE.yG-dx*ss+dy*cs, p[2]]; };
    const full=(p)=>steer(trim(p));
    const add=(fs,xf)=>{ for(const f of fs){ f.v=f.v.map(xf); f.tag=tag; if(f.db==null||f.db===0) f.db=0.01; out.push(f); } };
    add(box([0,-2.885,0.30],[0.12,0.03,0.11],'drv',0.0), steer);                          // gimbal ring
    add(box([0,-2.95,0.31],[0.10,0.05,0.10],'drv',0.1), full);                            // bell housing
    add(box([0,-3.05,0.28],[0.085,0.10,0.12],'drv',0.25), full);                          // upper gear housing
    add(box([0,-3.03,0.42],[0.07,0.085,0.028],'drv',0.55), full);                         // top cover
    (function(){                                                                         // driveshaft housing, tapered
      const t0=0.16, t1=-0.14, a=[[-0.055,-2.95],[0.055,-2.95],[0.055,-3.13],[-0.055,-3.13]], b=[[-0.045,-2.99],[0.045,-2.99],[0.045,-3.16],[-0.045,-3.16]];
      const fs=[];
      for(let k=0;k<4;k++){ const k2=(k+1)%4;
        const q=[[a[k][0],a[k][1],t0],[a[k2][0],a[k2][1],t0],[b[k2][0],b[k2][1],t1],[b[k][0],b[k][1],t1]];
        const mid=[(a[k][0]+a[k2][0])/2,(a[k][1]+a[k2][1])/2+3.05,0];
        faceO(fs,q,'drv',0.05,0,mid); }
      add(fs, full);
    })();
    add(box([0,-3.08,0.00],[0.13,0.17,0.008],'drv',0.55), full);                          // anti-ventilation plate
    add(tube([0,-2.96,-0.20],[0,-3.25,-0.20],0.055,'drv',0.1), full);                     // gearcase torpedo
    add(box([0,-3.12,-0.33],[0.010,0.05,0.08],'drv',-0.2), full);                         // skeg
    add(tube([0,-3.25,-0.20],[0,-3.33,-0.20],0.032,'steel',0.3), full);                   // prop hub
    const pa=(s.prop||0)*DEG, blades=[];
    for(let k=0;k<3;k++){
      const a=pa+k*2*Math.PI/3, r0=0.035, r1=0.17, w0=0.028, w1=0.05, y=-3.29;
      const e=[Math.cos(a),Math.sin(a)], t=[-Math.sin(a),Math.cos(a)];
      const P=(r,w)=>[e[0]*r+t[0]*w, y, -0.20+e[1]*r+t[1]*w];
      blades.push({v:[P(r0,-w0),P(r1,-w1),P(r1,w1*0.6),P(r0,w0)], mat:'steel', b:0.35, db:0.01});
    }
    add(blades, full);
  }

  // steering wheel (tag 1): three-spoke sport wheel on the helm console's aft face
  // 10-03: hub y 0.055, rake 30 (top toward the bow). 10.3’s drive clip holds its hands on a rim
  // tilted top toward the figure: rake -12 and hub y 0.020 put every build’s hands on the rim (30/30)
  const WHEEL_HUB = { x:0.50, y:0.02, z:1.00 };
  const WHEEL_GEO = { rad:0.175, rimIn:0.146, rake:-12, seg:16, hubR:0.05, spokes:[0,180,270], col:0.082 };
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
    tubeTo(out,[P0[0]-n[0]*G.col,P0[1]-n[1]*G.col,P0[2]-n[2]*G.col],P0);   // the column runs along the axis into the console
    return out;
  }
  function tubeTo(out,A,B2){ for(const f of tube(A,B2,0.022,'blk',0.2)){ f.tag=1; f.db=0.25; out.push(f); } }
  function wheelDeg(o){
    o=o||{}; if(o.deg!=null) return o.deg;
    const turns=o.turns==null?WHEEL_LOCK:o.turns;
    return -Math.max(-1,Math.min(1,o.steer||0))*turns*360;     // steer > 0 = starboard = clockwise from the helm
  }

  // ---- assembly -----------------------------------------------------------------------------------
  const r3=(v)=>Math.round(v*1000)/1000;
  function stateOf(o){
    o=o||{};
    // the folded top lies on the sun pad, which is the engine hatch: opening the hatch lifts the top to
    // its upright bundle first (by hatch 0.33), the way you would have to on the boat
    const hatch=clamp01(+o.hatch||0), top0=clamp01(o.top!=null?+o.top:0);
    return { top:Math.max(top0, 0.5*Math.min(1,hatch*3)), hatch,
             steer:Math.max(-1,Math.min(1,+o.steer||0)), trim:clamp01(o.trim!=null?+o.trim:0.25),
             tilt:clamp01(+o.tilt||0), prop:+o.prop||0, drive:o.drive!==false, wheel:o.wheel!==false };
  }
  const _asm=new Map();
  function assemble(s){
    const key=[r3(s.top),r3(s.hatch),s.drive?[r3(s.steer),r3(s.trim),r3(s.tilt),Math.round(s.prop)].join(','):'-',
               s.wheel?r3(wheelDeg(s)):'-'].join('|');
    if(_asm.has(key)) return _asm.get(key);
    const dyn=[], SH=[];
    hatchFaces(s.hatch, dyn); const _h=dyn.length;
    topFaces(s.top, dyn, SH); const _t=dyn.length;
    if(s.drive) driveFaces(s, dyn, 2); const _d=dyn.length;
    if(s.wheel) for(const f of wheelFaces(wheelDeg(s))) dyn.push(f);
    // the new layout: the parts that move take their tags here (the sun pad is main_deck/fit; the top is
    // main_deck/fit folded in its boot, rigging otherwise, its canvas the roof; the leg is hull/part)
    stampTags(dyn, 0, _h, 'main_deck', 'fit');
    if(s.top<=TOP.bootAt) stampTags(dyn, _h, _t, 'main_deck', 'fit');
    else stampTags(dyn, _h, _t, 'rigging', 'rig', {canv:'roof', canvd:'roof'});
    stampTags(dyn, _t, _d, 'hull', 'part');
    stampTags(dyn, _d, dyn.length, 'main_deck', 'fit');
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
    if(doEdge){   // depth-edge seams: the far side of a depth break steps down two, so planes separate without a ring
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
    for(const sq of (shadows||[])){
      const rv=sq.v.map(p=>projVert(p[0],p[1],p[2],B)), steps=sq.steps||1;
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
          const i=y*W+x; if(!out[i] || tag[i]) continue;
          const sd=w0*a.d+w1*b.d+w2*c.d;
          if(dep[i] < sd-0.06) continue;
          const e=RINDEX[out[i]]; if(e && e.i>0) out[i]=e.r[Math.max(0,e.i-steps)];
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
    const A=assemble(stateOf(opts));
    return _toRGBA(_paint(A.F, Object.assign({}, opts, {dir}), true, A.SH));
  }
  // layers: painted WITH the hull, then masked to their own pixels, so the console hides the wheel
  // bow-on and the platform hides the top of the leg
  function renderWheel(dir, opts){
    opts=norm(opts);
    const A=assemble(Object.assign(stateOf(opts),{wheel:true}));
    return _toRGBA(_paint(A.F, Object.assign({}, opts, {dir, onlyTag:1}), true));
  }
  function renderDrive(dir, opts){
    opts=norm(opts);
    const A=assemble(Object.assign(stateOf(opts),{drive:true}));
    return _toRGBA(_paint(A.F, Object.assign({}, opts, {dir, onlyTag:2}), true));
  }

  // ---- anchors (hull-cell px), metres published beside them ---------------------------------------
  const _at=(P,dir,opts)=>{ const o=Object.assign({}, norm(opts), {dir}); const p=projVert(P.x,P.y,P.z,camBasis(o)); return { x:p.sx, y:p.sy }; };
  const _anchor=(P)=>function(dir,opts){ return _at(P,dir,opts); };
  const bowSeat=(y,side)=>{ const u=uOf(y), zs=soleZ(u)+0.38, o=xl(u,zs)-0.012; return { x:side*(o-0.21), y, z:zs }; };
  const HELM      = { x:0.50, y:-0.38, z:0.70 };
  const PASSENGER = { x:-0.50, y:-0.38, z:0.70 };
  const SEATS = [
    Object.assign({ id:'helm' }, HELM), Object.assign({ id:'passenger' }, PASSENGER),
    { id:'bench_port', x:-0.56, y:-1.64, z:0.73 }, { id:'bench_centre', x:0, y:-1.64, z:0.73 }, { id:'bench_stbd', x:0.56, y:-1.64, z:0.73 },
    Object.assign({ id:'bow_port' }, bowSeat(1.50,-1)), Object.assign({ id:'bow_stbd' }, bowSeat(1.50,1)),
    { id:'bow_fwd', x:0, y:yOf(U_BD)-0.30, z:soleZ(U_BD)+0.38 },
  ];
  const PILOT   = { x:0.50, y:-0.10, z:DECK };
  const DRIVE_MOUNT = { x:0, y:DRIVE.yG, z:DRIVE.zT };
  const PAINTER = { x:0, y:2.46, z:0.66 };
  const STERN_EYES = [ {x:-0.80,y:-L/2-0.012,z:0.84}, {x:0.80,y:-L/2-0.012,z:0.84} ];
  const LADDER  = { x:-0.60, y:PL.y1+0.10, z:PL.zt };
  const SUNPAD  = { x:0, y:-2.32, z:SP.zl+SP.cz };
  const WATERLINE = 0.40;
  const many=(list)=>function(dir,opts){ return list.map(P=>_at(P,dir,opts)); };

  const SPEC = { reference:'18 ft sterndrive bowrider (185 class)', loa_hull:5.64, loa_platform:0.34, beam:2.29,
    deadrise_transom_deg:19, engine:'4.3 L V6 petrol, 190 hp', drive:'Alpha-type outdrive, 1.81:1', seats:8,
    draft_drive_down:0.86, draft_drive_up:0.43 };

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
  // THE BOWRIDER IN THE NEW LAYOUT (ADR 0048 §8 A). Hull id 'bowrider', through byId('bowrider').
  // An OPEN boat: no room (interiorFaces() is empty), no door leaf (doorFaces() is empty), no house.
  //   levels  main_deck   the cockpit: sole, liner, seats, consoles, windscreen, bow lounge, sun pad
  //           foredeck    the closed bow deck over the anchor locker, with its cleat
  //           hull        the shell, the transom, the cap and the hull's own fittings (never lifted)
  //           rigging     the bimini while it stands (frame role rig, canvas role roof)
  //   cut     'deck'      forward of the windscreen: nothing lifts
  //           'cockpit'   aft of it, under the bimini: the rigging lifts, so the walker shows
  //   parts   the wheel is face group tag 1 (state wheel:false drops it), the sterndrive tag 2
  //           (drive:false); the top, the sun-pad hatch, steer, trim and tilt are state, as in 10-03
  // ==================================================================================================
  const HULL_ID='bowrider', RIG_FILE='bowriderIsoRig.js', EXPORT_SYMBOL='BowriderIso';
  // state: the 10-03 state object ({top, hatch, steer, trim, tilt, prop, drive, wheel}); a pose name
  // such as 'stowed' (what the bake passes today) is the default state. tower: none on this hull.
  function faceList(state, tower){ return assemble(stateOf(state && typeof state==='object' ? state : {})).F; }
  function partFaces(id, state){ const tag=({ wheel:1, drive:2 })[id]; return tag ? faceList(state).filter(f=>f.tag===tag) : []; }
  function interiorFaces(){ return []; }
  function doorFaces(t){ return []; }
  function matsFor(id){
    const pal=palette(id && typeof id==='object' ? id : { scheme:id });
    return nlMatsTable(pal.id, pal.mats, []);
  }
  function fitCheck(tol){ return { tol:tol==null?0.01:tol, faces:0, outside:0, worst:0, note:'an open boat: no room faces to check' }; }
  function geometry(){
    return {
      schema:'hidden-harbours/hull-geometry@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:HULL_ID, units:'m', frame:NL_FRAME,
      ids:Object.assign({}, LV), roles:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice(), dwlZ:WATERLINE,
      riggingClass:'rigging: the bimini while it stands (frame role rig, canvas role roof). Folded into its boot (top <= '+TOP.bootAt+') it lies on the sun pad as main_deck/fit.',
      levels:[
        { id:'main_deck', deck:'cockpit', sole:DECK, ceil:null, kind:'open', soleZ:DECK, ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the cockpit and the bow lounge are sky; the bimini (rigging) lifts under cut key cockpit' } },
        { id:'foredeck', deck:'bow_deck', sole:q3(capZ(U_BD)), ceil:null, kind:'open', soleZ:q3(capZ(U_BD)), ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the closed bow deck over the anchor locker: a surface, not a walk area (see _excluded)' } },
      ],
      cutaway:{
        ghost:{ deck:[], cockpit:['rigging'] },
        gate:{ deck:[], cockpit:['rigging'] },
        shows:{ deck:null, cockpit:null },
        note:'gate lists are complete lift sets in ghost’s vocabulary (whole levels, or level:role). No key shows a room: she has none.',
      },
      parts:[
        { id:'wheel', group:'tag', tag:1, state:'wheel', lv:'main_deck', ro:'fit', hub:nlV([WHEEL_HUB.x,WHEEL_HUB.y,WHEEL_HUB.z]),
          axis:nlV([0,-Math.cos(WHEEL_GEO.rake*DEG),Math.sin(WHEEL_GEO.rake*DEG)]), rake_deg:WHEEL_GEO.rake, radius:WHEEL_GEO.rad,
          note:'turns about axis through hub; wheelFaces(deg) draws it at any angle' },
        { id:'drive', group:'tag', tag:2, state:'drive', lv:'hull', ro:'part', gimbal:nlV([0,DRIVE.yG,DRIVE.zT]),
          note:'trims and tilts about the trim pins, steers about the vertical gimbal axis: state trim, tilt, steer' },
        { id:'bimini', group:'state', state:'top', lv:'rigging', ro:'rig|roof', note:'top 0 boot (main_deck/fit), 0.5 bundled, 1 spread' },
        { id:'sun_pad', group:'state', state:'hatch', lv:'main_deck', ro:'fit', note:'the engine hatch: hatch 0..1 swings it up 68 deg on its forward hinge' },
      ],
      states:{ top:'0..1', hatch:'0..1', steer:'-1..1 (wheel and leg)', trim:'0..1 (default 0.25)', tilt:'0..1', prop:'blade phase', drive:'true|false', wheel:'true|false' },
    };
  }
  function walk(){
    const R=NL_WALK.radius;
    const halfLiner=(y)=>{ const u=uOf(y); return xl(u, soleZ(u)+0.02)-R; };
    // the cockpit: bench front to the consoles' aft face, the liner inset, and the walk-through between
    // the consoles (0.48 m: the centre has 0.02 m) up to the windscreen line
    const yA=-1.42+R, yC=CON.ya-R, yW=0.95, xC=CON.xi-R;
    const S=[], P=[]; for(let i=0;i<=8;i++){ const y=yA+(yC-yA)*i/8, w=halfLiner(y); S.push([q3(w),q3(y)]); P.unshift([q3(-w),q3(y)]); }
    const cockpitPoly=S.concat([[q3(xC),q3(yC)],[q3(xC),q3(yW)],[q3(-xC),q3(yW)],[q3(-xC),q3(yC)]], P);
    const seatHoles=[-0.50,0.50].map(sx=>nlGrow(sx-0.225,-0.73,sx+0.225,-0.165,R));
    // the bow: forward of the consoles, between the lounge's seat fronts, to the forward backrest
    const UB0=uOf(1.12), SD=0.42, zsL=(u)=>soleZ(u)+0.38, oL=(u)=>xl(u,zsL(u))-0.012, nL=(u)=>Math.max(0,oL(u)-SD);
    const xOuter=xl(uOf(CON.ya),CON.za)-0.012;
    const conPoly=[[CON.xi,CON.ya],[xOuter,CON.ya]]; for(let i=0;i<=12;i++){ const x=xOuter+(CON.xi-xOuter)*i/12; conPoly.push([x, dashFront(x)[1]]); }
    const lounge=[], lo=[]; for(let i=0;i<=10;i++){ const u=UB0+(U_BD-UB0)*i/10, y=yAt(u,zsL(u)); lounge.push([nL(u),y]); lo.unshift([oL(u),y]); }
    const loungePoly=lounge.concat(lo), yFwd=yAt(U_BD,soleZ(U_BD))-0.035;
    const okBow=(x,y)=>{ const u=uOf(y); return x<=xl(u,soleZ(u)+0.02)-R && y<=yFwd-R && nlPolyD([x,y],conPoly)>=R && nlPolyD([x,y],loungePoly)>=R; };
    const yB0=0.85, rows=[];
    for(let y=yB0; y<yFwd; y+=0.02){ let h=Infinity; for(let k=0;k<=4;k++) h=Math.min(h, nlHalfAt(okBow, y+0.005*k)); if(h<0.005) break; rows.push([y,h]); }
    const yB1=rows.length ? rows[rows.length-1][0]+0.02 : yB0+0.02;
    const runs=[]; for(const [y,h] of rows){ const r=runs[runs.length-1]; if(r && r.h===h) r.y1=y+0.02; else runs.push({ y0:y, y1:y+0.02, h }); }
    const bS=[], bP=[]; for(const r of runs){ bS.push([q3(r.h),q3(r.y0)],[q3(r.h),q3(r.y1)]); bP.unshift([q3(-r.h),q3(r.y1)],[q3(-r.h),q3(r.y0)]); }
    const bowPoly=bS.concat(bP);
    const zBow0=soleZ(uOf(0.99)), zBow1=soleZ(uOf(yB1));
    // the sun pad (hatch shut), and the transom steps: sun pad -> bench cushion -> sole
    const zPad=SP.zl+SP.cz, padPoly=nlRect(-(SP.hw-R), SP.y0+R, SP.hw-R, SP.y1-R);
    const stepPoly=nlRect(-(SP.hw-R), SP.y1-R-0.03, SP.hw-R, -1.42+R+0.04);
    const areas=[
      { id:'cockpit', cut:'cockpit', z:DECK, poly:cockpitPoly, holes:seatHoles,
        note:'the helm and passenger buckets are holes; the aisle between them is 0.09 m for the centre, the walk-through 0.02 m' },
      { id:'bow', cut:'deck', ramp:{ axis:'y', a0:0.99, a1:q3(yB1), z0:q3(zBow0), z1:q3(zBow1) }, poly:bowPoly, holes:[],
        note:'the walk-through’s far end and the strip between the lounge’s seat fronts: the bow is for sitting (waits on sit)' },
      { id:'sun_pad', cut:'cockpit', z:q3(zPad), poly:padPoly, holes:[],
        note:'with the hatch shut (hatch 0). Folded (top 0) the bimini’s boot lies across its forward edge, 0.18 m high: stepped over' },
      { id:'transom_steps', cut:'cockpit', cutHi:'cockpit', cutLo:'cockpit', zMid:q3((zPad+DECK)/2), kind:'stair',
        ramp:{ axis:'y', a0:q3(SP.y1-R), a1:q3(-1.42+R), z0:q3(zPad), z1:DECK }, poly:stepPoly, holes:[],
        note:'sun pad 1.035 -> bench cushion 0.73 -> sole 0.34; walked as a ramp until stairDown / stairUp exist', waitsOn:['stairDown','stairUp'] },
    ];
    const capW0=xOut(0.5)-RUBIN-xo(0.5), capW1=xOut(U_BD)-RUBIN-xo(U_BD);
    const _excluded=[
      { id:'swim_platform', of:'area', z:PL.zt, reason:'0.34 m deep fore and aft: shallower than the walker’s 0.46 m, so no centre point fits. The way aboard from the water is the swim ladder.', waitsOn:['ladderOn'] },
      { id:'gunwale_port', of:'area', reason:'the deck cap: '+q3(capW0)+'-'+q3(capW1)+' m of moulding beside the cockpit, narrower than the walker. An open boat is boarded over it (board / boardDown), not walked.' },
      { id:'gunwale_stbd', of:'area', reason:'as gunwale_port.' },
      { id:'bow_deck', of:'area', z:q3(capZ(U_BD)), reason:'the closed bow deck over the anchor locker: 0.87 m above the bow sole, over the lounge’s backrest, tapering to the stem in 0.47 m. No walkway; the anchor is worked from the bow (interact anchor).' },
      { id:'seat_passenger', of:'seat', at:nlPt(PASSENGER), reason:'a seat, not a standing surface: sitting waits on a sit clip (10.3 has none).', waitsOn:['sit'] },
      { id:'aft_bench', of:'seat', at:nlPt(SEATS[3]), reason:'a seat for three; it is also the middle step of transom_steps. Sitting waits on sit.', waitsOn:['sit'] },
      { id:'bow_lounge', of:'seat', at:nlPt(SEATS[5]), reason:'the U-lounge (port, starboard, forward): seats. Sitting waits on sit.', waitsOn:['sit'] },
      { id:'washboards', of:'washboards', reason:'an open boat: no side deck to work from; the cap is 0.15-0.19 m of moulding (gunwale_port, gunwale_stbd). Lines are worked from the cockpit and the bow.' },
      { id:'stern_eyes', of:'cleat', at:nlPt(STERN_EYES[0]), reason:'the transom’s two tie-down eyes (anchors.sternEyes) are trailer and ski-tow eyes, not mooring cleats.' },
      { id:'grab_rails', of:'cleat', reason:'the bow grab rails along the cap are handholds, not tie-offs (10-03).' },
    ];
    const reachCockpit=(x,y)=>({ x:q3(x), y:q3(y), z:DECK });
    const interact=[
      { id:'helm', action:'enter_helm', label:'Helm', prompt:'Take the wheel', at:nlPt(WHEEL_HUB), reach:reachCockpit(0.02,-0.40), area:'cockpit', seat:nlPt(HELM), stand:nlPt(PILOT),
        note:'seated: drive, 30 of 30 on 10.3; standing: helm_idle (see the fit)' },
      { id:'seat_passenger', action:'enter_ride', label:'Passenger seat', prompt:'Ride', at:nlPt(PASSENGER), reach:reachCockpit(-0.02,-0.40), area:'cockpit', waitsOn:['sit'] },
      { id:'seat_bench_port', action:'enter_ride', label:'Aft bench', prompt:'Ride', at:nlPt(SEATS[2]), reach:reachCockpit(-0.56,-1.15), area:'cockpit', waitsOn:['sit'] },
      { id:'seat_bench_centre', action:'enter_ride', label:'Aft bench', prompt:'Ride', at:nlPt(SEATS[3]), reach:reachCockpit(0,-1.15), area:'cockpit', waitsOn:['sit'] },
      { id:'seat_bench_stbd', action:'enter_ride', label:'Aft bench', prompt:'Ride', at:nlPt(SEATS[4]), reach:reachCockpit(0.56,-1.15), area:'cockpit', waitsOn:['sit'] },
      { id:'seat_bow_port', action:'enter_ride', label:'Bow lounge', prompt:'Ride', at:nlPt(SEATS[5]), reach:{ x:0, y:q3(yB1-0.04), z:q3(zBow1) }, area:'bow', waitsOn:['sit'] },
      { id:'seat_bow_stbd', action:'enter_ride', label:'Bow lounge', prompt:'Ride', at:nlPt(SEATS[6]), reach:{ x:0, y:q3(yB1-0.04), z:q3(zBow1) }, area:'bow', waitsOn:['sit'] },
      { id:'seat_bow_fwd', action:'enter_ride', label:'Bow lounge', prompt:'Ride', at:nlPt(SEATS[7]), reach:{ x:0, y:q3(yB1-0.04), z:q3(zBow1) }, area:'bow', waitsOn:['sit'] },
      { id:'engine', action:'open_close_engine', label:'Sun pad / engine', prompt:'Lift the sun pad', state:'hatch', at:nlPt({ x:0, y:-2.32, z:zPad }), reach:reachCockpit(0,-1.17), area:'cockpit',
        note:'the sun pad is the hatch; it lifts aft edge first on its forward hinge, so it is opened from the cockpit side, not stood on' },
      { id:'bimini', action:'open_close', label:'Bimini', prompt:'Raise / stow the top', state:'top', at:nlPt({ x:TOP.xP, y:TOP.yP, z:TOP.zP }), reach:reachCockpit(0.40,-1.00), area:'cockpit' },
      { id:'ski_locker', action:'storage', label:'Ski locker', prompt:'Store · retrieve', at:nlPt({ x:0, y:-0.71, z:DECK+0.008 }), reach:reachCockpit(0,-1.00), area:'cockpit' },
      { id:'glovebox', action:'storage', label:'Glovebox', prompt:'Store · retrieve', at:nlPt({ x:-0.58, y:CON.ya-0.004, z:0.82 }), reach:reachCockpit(-0.02,-0.17), area:'cockpit' },
      { id:'bench_locker', action:'storage', label:'Bench locker', prompt:'Store · retrieve', at:nlPt({ x:0, y:-1.42, z:0.50 }), reach:reachCockpit(0,-1.15), area:'cockpit',
        note:'under the aft bench’s cushion (10-03)' },
      { id:'bow_locker_port', action:'storage', label:'Bow locker', prompt:'Store · retrieve', at:nlPt({ x:-0.62, y:1.70, z:0.74 }), reach:{ x:0, y:q3(Math.min(1.70,yB1-0.04)), z:q3(zBow1) }, area:'bow',
        note:'under the bow lounge’s port seat (10-03)' },
      { id:'bow_locker_stbd', action:'storage', label:'Bow locker', prompt:'Store · retrieve', at:nlPt({ x:0.62, y:1.70, z:0.74 }), reach:{ x:0, y:q3(Math.min(1.70,yB1-0.04)), z:q3(zBow1) }, area:'bow',
        note:'under the bow lounge’s starboard seat (10-03)' },
      { id:'anchor', action:'anchor', label:'Anchor locker', prompt:'Let go / weigh anchor', at:nlPt({ x:0, y:yOf(0.94), z:capZ(0.94)+0.012 }), reach:{ x:0, y:q3(yB1-0.04), z:q3(zBow1) }, area:'bow',
        note:'the anchor locker lid on the closed bow deck, reached over the forward backrest' },
      { id:'swim', action:'enter_water', label:'Swim platform', prompt:'Swim', at:nlPt(LADDER), reach:{ x:-0.60, y:q3(SP.y0+R+0.02), z:q3(zPad) }, area:'sun_pad', waitsOn:['ladderOn'],
        note:'down into the water is ladderDown (fits, see the fit); back aboard from the swim waits on ladderOn' },
    ];
    // 10-03's ids and types. 10-03's sidecar listed no bow cleat, but build() draws one on the bow deck (u 0.972,
    // beside the anchor locker's lid): it is bow_1 here
    const cleats=[
      { id:'stern_port', type:'cleat', pos:nlV([-(xOut(0.03)-0.10), yOf(0.03), capZ(0.03)+0.016]) },
      { id:'stern_star', type:'cleat', pos:nlV([xOut(0.03)-0.10, yOf(0.03), capZ(0.03)+0.016]) },
      { id:'bow_1', type:'cleat', pos:nlV([0, yOf(0.972), capZ(0.972)+0.016]), note:'build(): on the bow deck ahead of the anchor locker’s lid (10-03’s sidecar said there was none)' },
      { id:'bow_eye', type:'bow_eye', pos:nlV([0,2.44,0.66]), note:'the painter and trailer eye on the stem' },
    ];
    return {
      schema:'hidden-harbours/hull-walk@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:HULL_ID, label:'Bowrider 18',
      kind:'open', units:'metres', frame:NL_FRAME,
      loa_m:q3(L-PL.y1-L/2), loh_m:L, beam_m:q3(2*T[3][1]), DWL:WATERLINE, cockpitZ:DECK,
      cell:{ W, H, pivot:{ x:cx, y:cy }, pxPerM:PX, defaultElev:DEFAULT_ELEV, order:['N','NE','E','SE','S','SW','W','NW'] },
      tags:{ lv:Object.assign({}, LV), ro:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice() },
      walk:{ radius:R, stepTol:NL_WALK.stepTol, rule:NL_RULE, spawn:{ x:0, y:-1.0, area:'cockpit' }, areas },
      anchors:{ helm:nlPt(HELM), wheel:nlPt(WHEEL_HUB), pilot:nlPt(PILOT), seats:SEATS.map(s=>Object.assign({ id:s.id }, nlPt(s))),
        painter:nlPt(PAINTER), ladder:nlPt(LADDER), sunPad:nlPt(SUNPAD), driveMount:nlPt(DRIVE_MOUNT) },
      interact, cleats, washboards:[], _excluded,
      rock:{ frames:ROCK.frames, rollA:ROCK.rollA, pitchA:ROCK.pitchA, heaveA:ROCK.heaveA, period:ROCK.period },
      qa:{ fitCheck:fitCheck() },
    };
  }
  const HULL={ id:HULL_ID, label:'Bowrider 18', kind:'open', faceList, partFaces, interiorFaces, doorFaces, geometry, matsFor, walk, fitCheck, render, renderCut,
    GAIN, BIAS, LN, BAYER, PX, W, H, pivot:{ x:cx, y:cy }, defaultElev:DEFAULT_ELEV, KEY,
    schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, DWL:WATERLINE, cockpitZ:DECK, loa:L-PL.y1-L/2 };
  const HULLS={ bowrider:HULL };
  function byId(id){ return HULLS[id]||null; }

  root.BowriderIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV, KEYLINE_DEFAULT,
    order:['N','NE','E','SE','S','SW','W','NW'],
    render, renderWheel, renderDrive, allFaces:(o)=>assemble(stateOf(o||{})).F, wheelFaces, WHEEL_GEO, WHEEL_LOCK, WHEEL_HUB,
    ROCK, rock:rockMotion, PLANE, plane:planeMotion, TURN, turn:turnMotion, addPose:posesAdd,
    TOP, DRIVE, SP, PL, SPEC, stateOf,
    helmSeat:_anchor(HELM), passengerSeat:_anchor(PASSENGER), wheelHub:_anchor(WHEEL_HUB), pilotStand:_anchor(PILOT),
    driveMount:_anchor(DRIVE_MOUNT), painter:_anchor(PAINTER), sternEyes:many(STERN_EYES), ladder:_anchor(LADDER),
    sunPad:_anchor(SUNPAD), seats:many(SEATS),
    HELM, PASSENGER, SEATS, PILOT, DRIVE_MOUNT, PAINTER, STERN_EYES, LADDER, SUNPAD,
    L, LOA:L-PL.y1-L/2, BEAM:2*T[3][1], DECK, WATERLINE, station, soleZ,
    SCHEMES, schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, SLOTS,
    palette, rampFrom, chipWall, C_CAP,
    PAINT, STEEL, GLAS, MOTO, KEY,
    // the new layout (ADR 0048 §8 A): one hull, through byId('bowrider'); the same names for the default hull
    byId, HULLS, hullIds:Object.keys(HULLS), defaultHull:HULL_ID, LV, RO, GHOST_ROLES,
    faceList, partFaces, interiorFaces, doorFaces, geometry, matsFor, MATS:matsFor(DEFAULT_SCHEME).MATS, walk, fitCheck, renderCut,
    GAIN, BIAS, LN, BAYER, cell:{ W, H, pivot:{ x:cx, y:cy }, PX, defaultElev:DEFAULT_ELEV } };
})(typeof globalThis!=='undefined'?globalThis:window);

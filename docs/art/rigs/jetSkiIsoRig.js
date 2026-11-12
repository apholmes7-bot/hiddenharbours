/* Hidden Harbours — JETSKI (a sit-down personal watercraft), IN THE NEW LAYOUT. ADR 0048 §8 A; the
   contract is docs/art/hull-layout-spec.md. One rig, one hull, through its global:
     JetSkiIso.byId('jet_ski') = { faceList(state,tower), partFaces(id,state), interiorFaces(),
       doorFaces(t), geometry(), matsFor(scheme), walk(), fitCheck(), GAIN, BIAS, LN, BAYER, PX, W, H,
       pivot, defaultElev, schemeIds, defaultScheme, DWL, cockpitZ, loa }
   and the same names on the global for the default hull. Every face carries lv (its level), ro (its
   role) and it (false on every face: she is an OPEN boat, with no room, no door leaf and no house).
   Her walk, docs/art/rigs/gameplay/jetSkiIsoRig.jet_ski.walk.json, is WRITTEN by
   tools/rig-recipes/hull-walk.mjs from walk(), never by hand, and stamped with this file's LF sha256.
   The house style, the helpers and the export contract are bowriderIsoRig.js's (the newest small
   open hull): the paint mixer, the solids, the tags, the rasterizer and the layout helpers are that
   file's blocks, value for value. */
/* Hidden Harbours — parametric ISO JETSKI (three-seat runabout PWC) — same pipeline as
   bowriderIsoRig.js / goFastIsoRig.js. PASS 1.
   A modern sit-down runabout: 3.20 m hull + 0.10 m of jet nozzle = 3.30 m overall, 1.20 m beam,
   about 20 deg deadrise at the transom, 0.30 m draught at rest. Fixed 3/4 turntable camera (elev 40
   default), 45 deg steps, flat-facet shading from the fleet's upper-left key, z-buffered, ordered
   dither, NO AA, ringless (ADR 0031: KEYLINE_DEFAULT=false, render(dir,{outline:true}) is the A/B).
   32 px = 1 m.

   WHAT SHE CARRIES, stern to stem
     jet pump        the pump housing and ride plate are fixed on the transom at the centreline; the
                     NOZZLE (a short steerable tube under a reverse-bucket lip) is a TAGGED LAYER
                     (tag 2) that steers about the vertical axis through NOZZLE.pivot. No propeller.
     boarding deck   a flat platform at the bond line across the stern, traction mat, grab handle
     saddle          a long stepped straddle seat on its pedestal: the passenger's raised seat aft,
                     the driver's seat forward of the step, the nose running up to the cowl
     footwells       either side of the saddle, traction-mat soles 0.09 m under the boarding deck
     handlebars      the upper steering column, the crossbar, two grips and the centre pad with its
                     gauge lens are a TAGGED LAYER (tag 1), ONE part group, built in one block
                     (handlebarFaces) and posed about the column's own axis: HANDLEBAR.pivot / .axis
     cowl            the raised hood forward of the saddle, crowned, with a storage lid and a gauge pod
     hull            deep-V planing hull skinned from stations, chine spray rail, bond-line rub rail

   COLOURWAYS. Paint is data, as on the bowrider. render(dir,{scheme:'…'}) picks a preset;
   render(dir,{paint:{hull,deck,stripe,bottom,uph,acc,sole}}) mixes any base colours through the
   shared OKLCH mixer (rampFrom, chipWall, C_CAP=0.115). hull is the topsides from the chine to the
   bond line and the cowl's crown; deck the cowl's shoulders, the gunwale caps, the boarding deck and
   the footwell liners; stripe the sweep on the topsides and the band along the cowl; uph the saddle;
   acc the passenger bolsters and the handlebar pad; sole the traction mats.

   POSES. rock(i), plane(t) and turn(s), the bowrider's pose set with YAW in the camera basis. She is
   light: livelier in roll than any other hull, and she leans INTO a turn hard.

   HULL-SKIN WINDING follows bowriderIsoRig.js vertex for vertex. Every other face is wound by an
   explicit outward vector (faceO).

   Cell 128x120, pivot (64,72) = boat origin (amidships of the 3.20 m hull, keel bottom,
   centreline), pinned every heading.
   Exposes globalThis.JetSkiIso = { W,H,PX,DIRS,pivot,order,defaultElev,KEYLINE_DEFAULT,
   render, renderHandlebar (= renderWheel), renderNozzle (= renderDrive), handlebarFaces, nozzleFaces,
   HANDLEBAR, NOZZLE, ROCK,rock, PLANE,plane, TURN,turn, addPose, SPEC, stateOf, anchors (helmSeat,
   passengerSeat, handlebarPivot, nozzlePivot, painter, seats) + their metre values (HELM, PASSENGER,
   SEATS, FEET, GRIPS, PAINTER, GRAB), SCHEMES, schemeIds, defaultScheme, SLOTS, palette, rampFrom,
   chipWall, C_CAP, L, LOA, BEAM, DECK, PLAT, WATERLINE, and the new layout's members (byId, HULLS,
   hullIds, defaultHull, LV, RO, GHOST_ROLES, faceList, partFaces, interiorFaces, doorFaces, geometry,
   matsFor, MATS, walk, fitCheck, renderCut, GAIN, BIAS, LN, BAYER, cell) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 128, H = 120, cx = 64, cy = 72;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;

  // ---- poses -------------------------------------------------------------------------------------
  // 0.35 t on a 1.20 m beam with the rider's weight high on the saddle: the liveliest hull in the fleet.
  const ROCK = { frames: 8, rollA: 6.0, pitchA: 3.2, heaveA: 1.6, period: 1.6 };
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a), yaw: 0 };
  }
  // planing: she pops onto the plane at once (an early, sharp hump), then runs flat on the aft third
  const PLANE = { frames: 12, humpAt: 0.28, humpPitch: 9.5, cruisePitch: 3.4, lift: 5 };
  function planeMotion(t){
    t = Math.max(0, Math.min(1, t||0));
    const hump = t < PLANE.humpAt ? Math.sin(0.5*Math.PI*t/PLANE.humpAt) : Math.pow(Math.max(0, 1-(t-PLANE.humpAt)/(1-PLANE.humpAt)), 1.5);
    const pitch = PLANE.cruisePitch*t + (PLANE.humpPitch - PLANE.cruisePitch*PLANE.humpAt)*hump;
    return { roll: 0, pitch, heave: PLANE.lift*t*t - 2.0*t*(1-t), yaw: 0 };
  }
  // hard over: she leans INTO the turn, twice the bowrider's bank, the bow yawed inside the track
  const TURN = { maxRoll: 24, maxYaw: 10, pitch: 2.6, heave: 2 };
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
  const GLAS  = ['#16333c','#24505a','#3a7680','#5fa3a6','#8fc9c4'];                      // gauge lens
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];  // pump, nozzle, rub rail, grips
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
    'signal-yellow': { name:'Signal Yellow', hull:'#e0b13a', deck:WHITE, stripe:'#2a2f33', bottom:'#2a2f33',
                       uph:'#2a2f33', acc:'#3d454b', sole:'#585d61',
                       note:'the fleet’s buoy yellow over a tar bottom, black sweep, black saddle' },
    'cranberry':     { name:'Cranberry',     hull:'#a8452f', deck:WHITE, stripe:'#2a2f33', bottom:WHITE,
                       uph:'#2a2f33', acc:'#a8452f', sole:'#5f6368',
                       note:'the fleet’s bog red, black sweep, black saddle with red bolsters' },
    'seafoam':       { name:'Seafoam',       hull:'#a3d9c8', deck:WHITE, stripe:'#1c7367', bottom:WHITE,
                       uph:'#3d454b', acc:'#2b8f86', sole:'#5e6a69',
                       note:'the sport skiff’s mint, deep teal sweep, slate saddle' },
    'tar-black':     { name:'Tar Black',     hull:'#2a2f33', deck:'#8a99a1', stripe:'#2ba39a', bottom:'#2a2f33',
                       uph:'#3d454b', acc:'#2ba39a', sole:'#585d61',
                       note:'the fleet’s tar value under squall-grey shoulders, teal sweep' },
  };
  const DEFAULT_SCHEME = 'signal-yellow';
  const SLOTS = ['hull','deck','stripe','bottom','uph','acc','sole'];

  const _pal={}; let _palOrder=[];
  function palette(o){
    o=o||{};
    const DF=SCHEMES[DEFAULT_SCHEME];
    const base = o.paint || SCHEMES[o.scheme] || DF;
    const deck=base.deck||DF.deck, hull=base.hull||DF.hull, stripe=base.stripe||DF.stripe;
    const bottom=base.bottom||deck, uph=base.uph||DF.uph, acc=base.acc||hull;
    const sole=base.sole||DF.sole;
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
    // dith: 0 = crisp banded gelcoat and vinyl; the traction mats and the moulded non-skid keep grain
    const mats = { paint:{ramp:hullR,off:0,dith:0},       deck:{ramp:deckR,off:0,dith:0},
                   nonskid:{ramp:deckR,off:-1,dith:0.22}, liner:{ramp:deckR,off:0,dith:0},
                   stripe:{ramp:stripeR,off:-1,dith:0},   bottom:{ramp:botR,off:0,dith:0},
                   uph:{ramp:uphR,off:0,dith:0.12},       acc:{ramp:accR,off:0,dith:0.10},
                   sole:{ramp:soleR,off:0,dith:0.40},
                   steel:{ramp:STEEL,off:0,dith:0},       glas:{ramp:GLAS,off:-2,dith:0},
                   moto:{ramp:MOTO,off:0,dith:0},         blk:{ramp:MOTO,off:-2,dith:0},
                   drv:{ramp:MOTO,off:1,dith:0} };
    const rindex={};
    [hullR,deckR,botR,stripeR,uphR,accR,soleR,STEEL,GLAS,MOTO,PAINT].forEach(r=>r.forEach((c,i)=>{ if(!rindex[c]) rindex[c]={r,i}; }));
    const out={ key, id:o.paint?'custom':(SCHEMES[o.scheme]?o.scheme:DEFAULT_SCHEME), name:base.name||'Custom',
                base:{hull,deck,stripe,bottom,uph,acc,sole}, note:base.note||'',
                ramps:{hull:hullR,deck:deckR,stripe:stripeR,bottom:botR,uph:uphR,acc:accR,sole:soleR},
                mats, rindex };
    _pal[key]=out; _palOrder.push(key);
    if(_palOrder.length>32){ delete _pal[_palOrder.shift()]; }
    return out;
  }

  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- the hull -----------------------------------------------------------------------------------
  // Stations at explicit u (stern 0 -> stem 1). [u, sheerHalf, chineHalf, keelHalf, chineRise, depth, keelZ] (m).
  // Deadrise at the transom = atan(0.15/(0.48-0.06)) = 19.7 deg. Beam 1.20 m at u 0.32. The sheer is the
  // bond line: level at 0.38 m across the boarding deck (u <= 0.19), 0.47 m amidships, 0.68 m at the stem.
  const L = 3.20, TH = 0.030, DECK = 0.30, PLAT = 0.39, NSEG = 10;
  const T = [
    [0.000, 0.555,0.480,0.060,0.150,0.360,0.020],
    [0.150, 0.590,0.500,0.050,0.160,0.370,0.010],
    [0.190, 0.592,0.501,0.048,0.162,0.375,0.005],
    [0.320, 0.600,0.505,0.040,0.175,0.430,0.000],
    [0.480, 0.590,0.480,0.035,0.200,0.470,0.000],
    [0.620, 0.540,0.410,0.030,0.230,0.500,0.010],
    [0.750, 0.450,0.310,0.030,0.260,0.520,0.040],
    [0.860, 0.330,0.200,0.028,0.265,0.510,0.100],
    [0.940, 0.200,0.110,0.025,0.230,0.470,0.180],
    [1.000, 0.070,0.045,0.020,0.170,0.410,0.270],
  ];
  const FC = 0.40, FK = 0.72, KB = 0.04, KB_MAX = 0.035;
  const lerp=(a,b,t)=>a+(b-a)*t;
  const smooth=(t)=>{ t=clamp01(t); return t*t*(3-2*t); };
  function station(u){
    u=Math.max(0,Math.min(1,u));
    let i=0; while(i<T.length-2 && u>T[i+1][0]) i++;
    const A=T[i], B=T[i+1], fr=(u-A[0])/(B[0]-A[0]);
    return { ws:lerp(A[1],B[1],fr), ch:lerp(A[2],B[2],fr), kw:lerp(A[3],B[3],fr),
             cd:lerp(A[4],B[4],fr), dep:lerp(A[5],B[5],fr), kz:lerp(A[6],B[6],fr), y:-L/2+u*L };
  }
  const RAKE=0.20;   // the forefoot tucks this far aft of the stem head
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

  // the bond-line rub rail, the gunwale cap, the boarding deck, the footwells, the saddle, the cowl
  const RUBIN = TH*0.76;                                  // rub rail cap depth — the deck starts inboard of it
  const CAPW = 0.09;                                      // the gunwale cap beside a footwell
  const xOut=(u)=>skin(1,u,1)[0];
  const capZ=(u)=>sheerZ(u)+0.010;
  const xo=(u)=>Math.max(0.03, xOut(u)-CAPW);             // footwell opening half-width at the cap
  const Y_PL=-1.00, Y_FW=0.30;                            // the boarding deck's forward edge; the cowl's aft face
  const U_PL=uOf(Y_PL), U_FW=uOf(Y_FW);
  const XS = 0.19;                                        // the saddle pedestal's half-width: the footwells' inboard wall
  const xw=(u)=>Math.min(0.50, xo(u), halfAtZ(u,DECK+0.012,1)-0.004);   // footwell sole's outboard edge — never outside the hull
  // the cowl's crown along the centreline [y, z], and its section at a station: rail, shoulder, the stripe's
  // upper edge, the crown's edge, the crown — as [half-width, z]
  const COWL = [[0.30,0.880],[0.50,0.870],[0.80,0.810],[1.10,0.755],[1.35,0.715],[1.60,0.695]];
  function cowlTop(y){
    let i=0; while(i<COWL.length-2 && y>COWL[i+1][0]) i++;
    const A=COWL[i], B=COWL[i+1]; return lerp(A[1],B[1],clamp01((y-A[0])/(B[0]-A[0])));
  }
  function cowlSec(u){
    const xr=Math.max(0.012, xOut(u)-RUBIN), zc=capZ(u), h=Math.max(0.006, cowlTop(yOf(u))-zc);
    return [[xr,zc],[0.84*xr,zc+0.30*h],[0.74*xr,zc+0.46*h],[0.58*xr,zc+0.78*h],[0.26*xr,zc+h]];
  }
  // the saddle: [y, top z, top half-width], aft to forward. The passenger's raised seat, the step down to the
  // driver's seat at y -0.45, the nose climbing to the cowl
  const SAD = [[-1.00,0.700,0.100],[-0.93,0.820,0.120],[-0.72,0.840,0.130],[-0.50,0.840,0.130],
               [-0.40,0.775,0.130],[-0.15,0.760,0.125],[0.10,0.780,0.115],[0.30,0.850,0.100]];
  const SAD_PED = 0.20;                                   // the pedestal's top sits this far under the saddle's
  const GRAB = { y:-1.045, x:0.16, z:0.72, r:0.018 };     // the boarding grab handle behind the passenger

  // THE TWO PARTS THAT STEER, as the game poses them. Each turns about `axis` through `pivot` (boat metres),
  // through +/- `lock` degrees at steer +/- 1.
  //   HANDLEBAR  the upper steering column: its pivot is where it leaves the cowl, its axis runs up the
  //              column, raked `rake` degrees aft of vertical (the top toward the rider)
  //   NOZZLE     the jet's outlet: its pivot is on the transom at the pump's centre, its axis vertical
  const HB_RAKE = 24;
  const HANDLEBAR = { pivot:[0,0.36,0.88], axis:[0,-Math.sin(HB_RAKE*DEG),Math.cos(HB_RAKE*DEG)], rake:HB_RAKE, lock:35,
                      col:0.15, half:0.33, sweep:0.06, drop:0.015, grip:0.12, barR:0.014, gripR:0.021 };
  const NOZZLE = { pivot:[0,-L/2,0.14], axis:[0,0,1], lock:22, len:0.10, r0:0.072, r1:0.054 };

  // colour sweep: the stripe runs high on the topsides aft and drops toward the bow while the sheer climbs
  const F_COL=(u)=>FC+(1-FC)*lerp(0.56,0.26,smooth((u-0.30)/0.62));
  const F_PIN=(u)=>F_COL(u)+(1-FC)*0.20;
  const B_BOT=(u)=>-0.95+0.55*Math.pow(Math.max(0,(u-0.74)/0.26),1.3);
  const OB = [ [0, FC, 'bottom', B_BOT, 0],
               [FC, F_COL, 'paint', 0, 0],
               [F_COL, F_PIN, 'stripe', 0.25, 0.004],
               [F_PIN, 1, 'paint', 0.15, 0] ];
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

  (function build(){
    TAG('hull','shell',{blk:'trim'});
    // ---- hull skin (bowrider winding), spray rail, bond-line rub rail ---------------------------
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,fv(f0,u0)),skin(side,u1,fv(f0,u1)),
                skin(side,u1,fv(f1,u1)),skin(side,u0,fv(f1,u0))],mat,bv(b,u0),db);
        const srt=(u)=>0.030*(1-Math.pow(clamp01((u-0.70)/0.26),1.3));
        const ca=skin(side,u0,FC), cb=skin(side,u1,FC);
        const oc0=[ca[0]+side*srt(u0),ca[1],ca[2]+0.008], oc1=[cb[0]+side*srt(u1),cb[1],cb[2]+0.008];
        const dnp=(p)=>[p[0],p[1],p[2]-0.030];
        face([ca,cb,oc1,oc0],'paint',0.8,0.03);
        face([oc0,oc1,dnp(oc1),dnp(oc0)],'bottom',-1.25,0.03);
        const oa=skin(side,u0,1), ob2=skin(side,u1,1);
        const inb=(p)=>[p[0]-side*RUBIN,p[1],p[2]];
        const or0=[oa[0],oa[1],oa[2]+0.002], or1=[ob2[0],ob2[1],ob2[2]+0.002];
        face([or0,or1,inb(ob2),inb(oa)],'blk',0.9,0.04);
        face([or0,or1,[or1[0],or1[1],or1[2]-0.040],[or0[0],or0[1],or0[2]-0.040]],'blk',0.15,0.04);
      }
    }
    for(let i=0;i<NSEG;i++){   // keel pad, converging and brightening forward
      const u0=i/NSEG, u1=(i+1)/NSEG, kb=-1.0+1.0*Math.pow(Math.max(0,(u0-0.72)/0.28),1.2);
      face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'bottom',kb);
    }

    TAG('hull','shell');
    // ---- transom, and the stem's face ------------------------------------------------------------
    const tp=(s,f)=>skin(s,0,f);
    for(const [f0,f1,mat,b] of OB)
      face([tp(-1,fv(f0,0)),tp(1,fv(f0,0)),tp(1,fv(f1,0)),tp(-1,fv(f1,0))], mat, bv(b,0)-0.55, 0.005);
    (function(){
      const fr=[0,FC,FK,1], P=fr.map(f=>skin(1,1,f)).concat(fr.slice().reverse().map(f=>skin(-1,1,f)));
      fo(P,'paint',0.1,0,[0,1,0.2]);
    })();
    TAG('hull','fit');
    // ---- the jet pump's fixed parts: the opening in the transom, the housing, the ride plate ------
    fo([[-0.115,-L/2-0.002,0.050],[0.115,-L/2-0.002,0.050],[0.115,-L/2-0.002,0.245],[-0.115,-L/2-0.002,0.245]],'blk',-0.6,0.004,[0,-1,0]);
    boxF([0,-L/2-0.010,NOZZLE.pivot[2]],[0.092,0.010,0.088],'drv',-0.1,0.01);
    boxF([0,-L/2-0.035,0.040],[0.105,0.045,0.007],'steel',0.25,0.01);                  // ride plate
    boxF([0,-L/2-0.010,0.335],[0.030,0.010,0.018],'steel',0.3,0.02);                   // stern eye
    (function(){ const e=skin(1,1,FK); boxF([0,e[1]+0.014,e[2]],[0.020,0.020,0.018],'steel',0.3,0.02); })();   // bow eye

    TAG('main_deck','floor');
    // ---- boarding deck: flat at the bond line across the stern, traction mat ---------------------
    (function(){
      const N=3;
      for(let i=0;i<N;i++){
        const u0=U_PL*i/N, u1=U_PL*(i+1)/N, w0=xOut(u0)-RUBIN, w1=xOut(u1)-RUBIN;
        fo([[-w0,yOf(u0),PLAT],[w0,yOf(u0),PLAT],[w1,yOf(u1),PLAT],[-w1,yOf(u1),PLAT]],'deck',0.30,0,[0,0,1]);
      }
      const z=PLAT+0.006, ya=-L/2+0.05, yb=GRAB.y-0.06;
      fo([[-0.42,ya,z],[0.42,ya,z],[0.42,yb,z],[-0.42,yb,z]],'sole',-2.4,0,[0,0,1]);
    })();

    TAG('main_deck','wall',{sole:'floor',deck:'hull/trim'});
    // ---- footwells: the gunwale cap, the liner down to the sole, the traction-mat sole -----------
    (function(){
      const N=5;
      for(const s of [-1,1]){
        for(let i=0;i<N;i++){
          const u0=U_PL+(U_FW-U_PL)*i/N, u1=U_PL+(U_FW-U_PL)*(i+1)/N, y0=yOf(u0), y1=yOf(u1);
          const r0=xOut(u0)-RUBIN, r1=xOut(u1)-RUBIN, c0=capZ(u0), c1=capZ(u1);
          fo([[s*r0,y0,c0],[s*r1,y1,c1],[s*xo(u1),y1,c1+0.004],[s*xo(u0),y0,c0+0.004]],'deck',0.35,0,[0,0,1]);
          fo([[s*xo(u0),y0,c0+0.004],[s*xo(u1),y1,c1+0.004],[s*xw(u1),y1,DECK],[s*xw(u0),y0,DECK]],'liner',-0.7,0,[-s,0,0.15]);
          fo([[s*XS,y0,DECK],[s*xw(u0),y0,DECK],[s*xw(u1),y1,DECK],[s*XS,y1,DECK]],'sole',-2.4,0,[0,0,1]);
        }
        // the step up to the boarding deck
        fo([[s*XS,Y_PL,DECK],[s*xw(U_PL),Y_PL,DECK],[s*xw(U_PL),Y_PL,PLAT],[s*XS,Y_PL,PLAT]],'liner',-0.4,0,[0,1,0]);
      }
    })();

    TAG('main_deck','wall');
    // ---- the cowl's aft face: the footwells' forward wall and the panel under the handlebars ------
    (function(){
      const A=cowlSec(U_FW), zc=capZ(U_FW), Y=Y_FW;
      const zXS=lerp(A[3][1],A[4][1],clamp01((A[3][0]-XS)/(A[3][0]-A[4][0])));
      for(const s of [-1,1]){
        fo([[s*XS,Y,DECK],[s*xw(U_FW),Y,DECK],[s*xo(U_FW),Y,zc+0.004],[s*XS,Y,zc+0.004]],'liner',-0.5,0,[0,-1,0]);
        fo([[s*XS,Y,zc],[s*A[0][0],Y,A[0][1]],[s*A[1][0],Y,A[1][1]],[s*A[2][0],Y,A[2][1]],[s*A[3][0],Y,A[3][1]],[s*XS,Y,zXS]],'liner',-0.3,0,[0,-1,0]);
      }
      const zp=SAD[SAD.length-1][1]-SAD_PED;
      fo([[-XS,Y,zp],[XS,Y,zp],[XS,Y,zXS],[A[4][0],Y,A[4][1]],[-A[4][0],Y,A[4][1]],[-XS,Y,zXS]],'blk',0.2,0,[0,-1,0]);
    })();

    TAG('main_deck','fit');
    // ---- the saddle on its pedestal: pedestal side, bolster, shoulder, top ------------------------
    (function(){
      const sec=(k,s)=>{ const y=SAD[k][0], zt=SAD[k][1], ht=SAD[k][2];
        return [[s*XS,y,DECK],[s*XS,y,zt-SAD_PED],[s*(XS-0.012),y,zt-0.065],[s*ht,y,zt]]; };
      for(let k=0;k+1<SAD.length;k++){
        const pass=SAD[k+1][0]<=-0.40;                       // the passenger's seat: accent bolsters
        for(const s of [-1,1]){
          const A=sec(k,s), B=sec(k+1,s);
          fo([A[0],B[0],B[1],A[1]],'paint',-0.35,0,[s,0,0]);
          fo([A[1],B[1],B[2],A[2]],pass?'acc':'uph',-0.1,0,[s,0,0.2]);
          fo([A[2],B[2],B[3],A[3]],'uph',-0.7,0,[s,0,1]);
        }
        fo([sec(k,-1)[3],sec(k,1)[3],sec(k+1,1)[3],sec(k+1,-1)[3]],'uph',-1.3,0,[0,0,1]);
      }
      // the aft end, over the boarding deck
      const a=sec(0,1), y=SAD[0][0];
      fo([[-XS,y,PLAT],[XS,y,PLAT],a[1],[-a[1][0],y,a[1][2]]],'paint',-0.5,0,[0,-1,0]);
      fo([[-a[1][0],y,a[1][2]],a[1],a[2],a[3],[-a[3][0],y,a[3][2]],[-a[2][0],y,a[2][2]]],'uph',-0.3,0,[0,-1,0.2]);
      // the boarding grab handle
      for(const s of [-1,1]) tubeF([s*GRAB.x,GRAB.y+0.015,PLAT],[s*GRAB.x,GRAB.y,GRAB.z],GRAB.r,'blk',0.35);
      tubeF([-GRAB.x,GRAB.y,GRAB.z],[GRAB.x,GRAB.y,GRAB.z],GRAB.r,'blk',0.5);
    })();

    TAG('foredeck','floor',{blk:'fit',glas:'fit',steel:'fit'});
    // ---- the cowl: lofted from the cowl's aft face to the stem head, six strips, crowned ---------
    (function(){
      const NC=6, TIP=0.996, MB=[['deck',0.10],['stripe',0.0],['paint',0.0],['paint',0.35]];
      for(let i=0;i<NC;i++){
        const u0=U_FW+(TIP-U_FW)*i/NC, u1=U_FW+(TIP-U_FW)*(i+1)/NC, y0=yOf(u0), y1=yOf(u1), A=cowlSec(u0), B=cowlSec(u1);
        for(const s of [-1,1]) for(let j=0;j<4;j++)
          fo([[s*A[j][0],y0,A[j][1]],[s*B[j][0],y1,B[j][1]],[s*B[j+1][0],y1,B[j+1][1]],[s*A[j+1][0],y0,A[j+1][1]]],MB[j][0],MB[j][1],0,[s*(j<2?1:0.4),0,1]);
        fo([[-A[4][0],y0,A[4][1]],[A[4][0],y0,A[4][1]],[B[4][0],y1,B[4][1]],[-B[4][0],y1,B[4][1]]],'paint',0.5,0,[0,0,1]);
      }
      // the storage lid on the crown, its latch, and the gauge pod ahead of the handlebars
      const ya=0.74, yb=1.12, wa=cowlSec(uOf(ya))[4][0]*0.88, wb=cowlSec(uOf(yb))[4][0]*0.88, za=cowlTop(ya)+0.010, zb=cowlTop(yb)+0.010;
      fo([[-wa,ya,za],[wa,ya,za],[wb,yb,zb],[-wb,yb,zb]],'deck',-0.5,0.006,[0,0,1]);
      boxF([0,ya+0.03,za+0.006],[0.030,0.012,0.006],'steel',0.5,0.01);
      const yg=0.50, zg=cowlTop(yg)+0.018;
      boxF([0,yg,zg],[0.075,0.050,0.020],'blk',0.3,0.006);
      fo([[-0.055,yg-0.035,zg+0.022],[0.055,yg-0.035,zg+0.022],[0.055,yg+0.030,zg+0.022],[-0.055,yg+0.030,zg+0.022]],'glas',0.9,0.008,[0,0,1]);
      // the fixed boot the upper column turns in
      prismF([HANDLEBAR.pivot[0],HANDLEBAR.pivot[1],HANDLEBAR.pivot[2]-0.022],0.048,0.034,6,'blk',0.2,0.006);
    })();
    TAG(null);
  })();

  // ---- DYNAMIC parts ------------------------------------------------------------------------------
  // ==== THE HANDLEBARS (tag 1): ONE part group — the upper column, the crossbar, the two grips and the
  // centre pad with its gauge lens. Nothing else in this file draws any of it; partFaces('handlebar')
  // returns exactly these faces, state wheel:false drops them, and the boot they turn in stays with the
  // cowl. They turn about HANDLEBAR.axis through HANDLEBAR.pivot: handlebarFaces(deg), deg > 0 = to
  // starboard (the starboard grip comes aft). Built in the column's own frame: a athwartships, q across
  // the column toward the rider, c up the column. ====
  function hbXf(deg){
    const P=HANDLEBAR.pivot, n=HANDLEBAR.axis, q=[0,-n[2],n[1]], th=(deg||0)*DEG, c=Math.cos(th), s=Math.sin(th);
    return (p)=>{ const a=p[0]*c-p[1]*s, b=p[0]*s+p[1]*c;
      return [P[0]+a, P[1]+b*q[1]+p[2]*n[1], P[2]+b*q[2]+p[2]*n[2]]; };
  }
  function handlebarFaces(deg){
    const Hb=HANDLEBAR, xf=hbXf(deg), out=[], zb=Hb.col;
    const add=(fs)=>{ for(const f of fs){ f.tag=1; f.db=0.04; out.push(f); } };
    const O=xf([0,0,0]), addO=(v,mat,b,want)=>{ const fs=[], w=xf(want); faceO(fs,v.map(xf),mat,b,0,[w[0]-O[0],w[1]-O[1],w[2]-O[2]]); add(fs); };
    add(tube([0,0,0.010],[0,0,zb],0.024,'blk',0.2,xf));                                       // upper column
    for(const s of [-1,1]){                                                                   // crossbar and grips
      const A=[s*0.085,0,zb], E=[s*Hb.half,Hb.sweep,zb-Hb.drop], t=1-Hb.grip/(Hb.half-0.085);
      const G=[lerp(A[0],E[0],t),lerp(A[1],E[1],t),lerp(A[2],E[2],t)];
      add(tube(A,G,Hb.barR,'steel',0.5,xf));
      add(tube(G,E,Hb.gripR,'blk',0.1,xf));
    }
    add(box([0,0,zb+0.006],[0.092,0.036,0.030],'blk',0.25,0,xf));                             // clamp and pod
    const zt=zb+0.0375;
    addO([[-0.055,-0.022,zt],[0.055,-0.022,zt],[0.055,0.024,zt],[-0.055,0.024,zt]],'glas',0.9,[0,0,1]);     // gauge lens
    addO([[-0.080,0.0375,zb-0.018],[0.080,0.0375,zb-0.018],[0.080,0.0375,zb+0.030],[-0.080,0.0375,zb+0.030]],'acc',0.2,[0,1,0]);   // crash pad
    return out;
  }
  // ==== THE NOZZLE (tag 2): the steerable outlet and its reverse-bucket lip (drawn stowed, up). It steers
  // about the vertical axis through NOZZLE.pivot: steer > 0 = to starboard (the outlet swings to starboard,
  // the stern is pushed to port). partFaces('nozzle'); state drive:false drops it. ====
  function nozzleFaces(steer){
    const Nz=NOZZLE, P=Nz.pivot, th=(steer||0)*Nz.lock*DEG, c=Math.cos(th), s=Math.sin(th), out=[];
    const xf=(p)=>[P[0]+p[0]*c-p[1]*s, P[1]+p[0]*s+p[1]*c, P[2]+p[2]], dir=(v)=>[v[0]*c-v[1]*s, v[0]*s+v[1]*c, v[2]];
    const put=(v,mat,b,want)=>{ const fs=[]; faceO(fs,v.map(xf),mat,b,0.01,dir(want)); fs[0].tag=2; out.push(fs[0]); };
    const K=6, ring=(y,r)=>{ const o=[]; for(let k=0;k<K;k++){ const a=2*Math.PI*k/K+Math.PI/K; o.push([r*Math.cos(a),y,r*Math.sin(a)]); } return o; };
    const r0=ring(-0.020,Nz.r0), r1=ring(-Nz.len,Nz.r1);
    for(let k=0;k<K;k++){ const k2=(k+1)%K;
      put([r0[k],r0[k2],r1[k2],r1[k]],'drv',0.1,[r0[k][0]+r0[k2][0],0,r0[k][2]+r0[k2][2]]); }
    put(r1.slice(),'blk',-1.2,[0,-1,0]);                                                      // the outlet
    const zh=Nz.r0+0.012, zl=Nz.r1+0.026;                                                     // reverse bucket, stowed
    put([[-0.070,-0.030,zh],[0.070,-0.030,zh],[0.064,-Nz.len,zl],[-0.064,-Nz.len,zl]],'drv',0.45,[0,0,1]);
    put([[-0.064,-Nz.len,zl],[0.064,-Nz.len,zl],[0.058,-Nz.len,Nz.r1-0.004],[-0.058,-Nz.len,Nz.r1-0.004]],'drv',0.0,[0,-1,0]);
    return out;
  }

  // ---- assembly -----------------------------------------------------------------------------------
  const r3=(v)=>Math.round(v*1000)/1000;
  function stateOf(o){
    o=o||{};
    return { steer:Math.max(-1,Math.min(1,+o.steer||0)), drive:o.drive!==false, wheel:o.wheel!==false };
  }
  const barDeg=(s)=>s.steer*HANDLEBAR.lock;
  const _asm=new Map();
  function assemble(s){
    const key=[s.drive?r3(s.steer):'-', s.wheel?r3(barDeg(s)):'-'].join('|');
    if(_asm.has(key)) return _asm.get(key);
    const dyn=[];
    if(s.wheel) for(const f of handlebarFaces(barDeg(s))) dyn.push(f);
    const _h=dyn.length;
    if(s.drive) for(const f of nozzleFaces(s.steer)) dyn.push(f);
    // the new layout: the parts that move take their tags here (the handlebars are main_deck/fit, the
    // nozzle hull/part)
    stampTags(dyn, 0, _h, 'main_deck', 'fit');
    stampTags(dyn, _h, dyn.length, 'hull', 'part');
    const A={ F:F.concat(dyn), SH:[] };
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
  // layers: painted WITH the hull, then masked to their own pixels, so the saddle hides the handlebars
  // from astern and the boarding deck hides the top of the nozzle
  function renderHandlebar(dir, opts){
    opts=norm(opts);
    const A=assemble(Object.assign(stateOf(opts),{wheel:true}));
    return _toRGBA(_paint(A.F, Object.assign({}, opts, {dir, onlyTag:1}), true));
  }
  function renderNozzle(dir, opts){
    opts=norm(opts);
    const A=assemble(Object.assign(stateOf(opts),{drive:true}));
    return _toRGBA(_paint(A.F, Object.assign({}, opts, {dir, onlyTag:2}), true));
  }

  // ---- anchors (hull-cell px), metres published beside them ---------------------------------------
  const _at=(P,dir,opts)=>{ const o=Object.assign({}, norm(opts), {dir}); const p=projVert(P.x,P.y,P.z,camBasis(o)); return { x:p.sx, y:p.sy }; };
  const _anchor=(P)=>function(dir,opts){ return _at(P,dir,opts); };
  // the driver's hip point on the saddle, astride, and the passenger's on the raised seat behind
  const HELM      = { x:0, y:-0.15, z:0.76 };
  const PASSENGER = { x:0, y:-0.70, z:0.84 };
  const SEATS = [ Object.assign({ id:'helm' }, HELM), Object.assign({ id:'passenger' }, PASSENGER) ];
  // the driver's feet, one in each footwell; the grips' centres with the bars straight
  const FEET  = [ { id:'port', x:-0.34, y:0.10, z:DECK }, { id:'stbd', x:0.34, y:0.10, z:DECK } ];
  const GRIPS = (function(){ const xf=hbXf(0), a=HANDLEBAR.half-HANDLEBAR.grip/2, t=(a-0.085)/(HANDLEBAR.half-0.085);
    return [-1,1].map(s=>{ const p=xf([s*a, HANDLEBAR.sweep*t, HANDLEBAR.col-HANDLEBAR.drop*t]); return { id:s<0?'port':'stbd', x:p[0], y:p[1], z:p[2] }; }); })();
  const HB_PIVOT  = { x:HANDLEBAR.pivot[0], y:HANDLEBAR.pivot[1], z:HANDLEBAR.pivot[2] };
  const HB_HEAD   = (function(){ const p=hbXf(0)([0,0,HANDLEBAR.col]); return { x:p[0], y:p[1], z:p[2] }; })();
  const NZ_PIVOT  = { x:NOZZLE.pivot[0], y:NOZZLE.pivot[1], z:NOZZLE.pivot[2] };
  const PAINTER   = (function(){ const e=skin(1,1,FK); return { x:0, y:e[1]+0.014, z:e[2] }; })();
  const STERN_EYE = { x:0, y:-L/2-0.010, z:0.335 };
  const WATERLINE = 0.30;
  const many=(list)=>function(dir,opts){ return list.map(P=>_at(P,dir,opts)); };

  const SPEC = { reference:'3.3 m three-seat sit-down runabout PWC', loa_hull:3.20, loa_nozzle:0.10, beam:1.20,
    deadrise_transom_deg:20, engine:'1.5 L three-cylinder petrol, 130 hp', drive:'axial-flow jet pump, steerable nozzle, reverse bucket', seats:3,
    draft:0.30 };

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

  // ==================================================================================================
  // THE JETSKI IN THE NEW LAYOUT (ADR 0048 §8 A). Hull id 'jet_ski', through byId('jet_ski').
  // An OPEN boat: no room (interiorFaces() is empty), no door leaf (doorFaces() is empty), no house.
  //   levels  main_deck   the footwells (its sole), the boarding deck, the saddle, the handlebars
  //           foredeck    the cowl: a moulded hood, a surface and not a walk area (see _excluded)
  //           hull        the shell, the transom, the gunwale caps, the pump and the nozzle (never lifted)
  //   cut     'deck'      everywhere: nothing lifts (she has nothing over a figure's head)
  //   parts   the handlebars are face group tag 1 (state wheel:false drops them), the nozzle tag 2
  //           (drive:false); steer turns both
  // ==================================================================================================
  const HULL_ID='jet_ski', RIG_FILE='jetSkiIsoRig.js', EXPORT_SYMBOL='JetSkiIso', LABEL='Jetski';
  const LOA=L+NOZZLE.len, HOOD_Z=q3(cowlTop(0.80));
  // state: { steer, wheel, drive }; a pose name such as 'stowed' (what the bake passes today) is the
  // default state. tower: none on this hull.
  function faceList(state, tower){ return assemble(stateOf(state && typeof state==='object' ? state : {})).F; }
  // 'handlebar' and 'nozzle' are her own names; 'wheel' and 'drive' are the same two groups under the
  // names the other recreation hulls use for tag 1 and tag 2
  function partFaces(id, state){ const tag=({ handlebar:1, wheel:1, nozzle:2, drive:2 })[id]; return tag ? faceList(state).filter(f=>f.tag===tag) : []; }
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
      levels:[
        { id:'main_deck', deck:'footwells', sole:DECK, ceil:null, kind:'open', soleZ:DECK, ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the footwells and the boarding deck are sky' } },
        { id:'foredeck', deck:'cowl', sole:HOOD_Z, ceil:null, kind:'open', soleZ:HOOD_Z, ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the cowl: a crowned hood over the storage bin, a surface and not a walk area (see _excluded)' } },
      ],
      cutaway:{
        ghost:{ deck:[] },
        gate:{ deck:[] },
        shows:{ deck:null },
        note:'gate lists are complete lift sets in ghost’s vocabulary (whole levels, or level:role). One key, and it lifts nothing: she has no room and nothing overhead.',
      },
      parts:[
        { id:'handlebar', group:'tag', tag:1, state:'wheel', lv:'main_deck', ro:'fit', pivot:nlV(HANDLEBAR.pivot), axis:HANDLEBAR.axis.map(v=>Math.round(v*1e6)/1e6),
          rake_deg:HANDLEBAR.rake, lock_deg:HANDLEBAR.lock, half_span:HANDLEBAR.half,
          note:'the upper column, crossbar, grips and pad: turns about axis through pivot, lock_deg each way at steer +/- 1 (steer > 0: the starboard grip comes aft); handlebarFaces(deg) draws it at any angle; partFaces(\'handlebar\') (or \'wheel\')' },
        { id:'nozzle', group:'tag', tag:2, state:'drive', lv:'hull', ro:'part', pivot:nlV(NOZZLE.pivot), axis:NOZZLE.axis.slice(), lock_deg:NOZZLE.lock,
          note:'the jet outlet and its reverse-bucket lip: steers about the vertical axis through pivot, lock_deg each way at steer +/- 1 (steer > 0: the outlet swings to starboard); partFaces(\'nozzle\') (or \'drive\')' },
      ],
      states:{ steer:'-1..1 (handlebars and nozzle)', drive:'true|false (the nozzle, tag 2)', wheel:'true|false (the handlebars, tag 1)' },
    };
  }
  function walk(){
    const R=NL_WALK.radius, D=2*R, fl=(v)=>Math.floor(v*1000+1e-6)/1000;
    // the boarding deck: the transom edge to the grab handle, rail to rail. The walker's disc must lie on it,
    // so its centre keeps R inside every edge: a strip 0.09 m fore and aft across the stern
    const yA=-L/2+R, yB=GRAB.y-GRAB.r-R;
    let hw=Infinity; for(let k=0;k<=6;k++) hw=Math.min(hw, xOut(uOf(lerp(-L/2,yB+R,k/6)))-RUBIN-R);
    hw=fl(hw);
    const areas=[
      { id:'aft_deck', cut:'deck', z:PLAT, poly:nlRect(-hw,yA,hw,yB), holes:[],
        note:'the boarding deck, '+q3(Y_PL+L/2)+' m fore and aft and '+q3(2*(xOut(0)-RUBIN))+' m across at the transom: the only surface aboard that takes the walker’s '+D+' m. '+
          'The centre has '+q3(yB-yA)+' m fore and aft between the transom edge and the grab handle. It is where she is boarded and where the saddle is mounted from.' },
    ];
    const wellPoly=(s)=>{ const N=5, P=[[XS,Y_PL]]; for(let i=0;i<=N;i++){ const u=U_PL+(U_FW-U_PL)*i/N; P.push([xw(u),yOf(u)]); } P.push([XS,Y_FW]);
      const Q=P.map(p=>[q3(s*p[0]),q3(p[1])]); return s<0 ? Q.reverse() : Q; };
    const wellW=q3(xw(uOf(-0.35))-XS), wellF=q3(xw(U_FW)-XS), wellGap={ width:wellW, width_forward:wellF, length:q3(Y_FW-Y_PL) };
    const capA=q3(xOut(U_PL)-RUBIN-xo(U_PL)), capB=q3(xOut(U_FW)-RUBIN-xo(U_FW));
    const _excluded=[
      { id:'footwell_port', of:'area', z:DECK, poly:wellPoly(-1), gaps_m:wellGap,
        reason:'a footwell: '+wellW+' m between the saddle’s pedestal and the liner ('+wellF+' m at its forward end), '+wellGap.length+' m long. The walker needs '+D+' m clear, '+
          'so no centre point fits; a well wide enough for the walker would leave no saddle on a 1.20 m beam. It is where the rider’s feet go (anchors.feet), not a place to walk.' },
      { id:'footwell_stbd', of:'area', z:DECK, poly:wellPoly(1), gaps_m:wellGap, reason:'as footwell_port.' },
      { id:'gunwale_port', of:'area', reason:'the gunwale cap beside the footwell: '+capB+'-'+capA+' m of moulding, narrower than the walker. She is boarded over the stern (aft_deck), not over the side.' },
      { id:'gunwale_stbd', of:'area', reason:'as gunwale_port.' },
      { id:'cowl', of:'area', z:HOOD_Z, reason:'the cowl: a crowned hood '+q3(HOOD_Z-capZ(uOf(0.80)))+' m above the rail, falling to the stem. No flat on it and no way onto it past the handlebars; the storage bin under its lid is reached from the footwell (interact hood_bin).' },
      { id:'saddle_helm', of:'seat', at:nlPt(HELM), reason:'the driver’s seat: astride the saddle, a foot in each footwell, both hands on the grips. 10.3’s only seated clip is drive, whose legs are together and whose hands hold a wheel rim: riding waits on a straddle clip.', waitsOn:['straddle'] },
      { id:'seat_passenger', of:'seat', at:nlPt(PASSENGER), reason:'the raised seat behind the driver, astride: a seat, not a standing surface. Sitting waits on a sit clip (10.3 has none).', waitsOn:['sit'] },
      { id:'reboarding', of:'note', z:PLAT, reason:'coming back aboard from the water is over the stern onto aft_deck, '+q3(PLAT-WATERLINE)+' m above the waterline, by the grab handle. No boarding step is drawn.', waitsOn:['ladderOn'] },
      { id:'washboards', of:'washboards', reason:'an open boat 1.20 m across: no side deck to work from; the caps are '+capB+'-'+capA+' m of moulding (gunwale_port, gunwale_stbd). Lines are worked from the boarding deck and the saddle.' },
    ];
    const onDeck=(x,y)=>({ x:q3(x), y:q3(y), z:PLAT }), yM=q3((yA+yB)/2);
    const interact=[
      { id:'helm', action:'enter_helm', label:'Handlebars', prompt:'Ride', at:nlPt(HB_HEAD), reach:onDeck(0,yB), area:'aft_deck', seat:nlPt(HELM),
        feet:FEET.map(p=>Object.assign({ id:p.id }, nlPt(p))), grips:GRIPS.map(p=>Object.assign({ id:p.id }, nlPt(p))), waitsOn:['straddle'],
        note:'mounted from the boarding deck, over the passenger seat. Astride at anchors.helm, feet in the footwells, hands on the grips (see _excluded saddle_helm)' },
      { id:'seat_passenger', action:'enter_ride', label:'Passenger seat', prompt:'Ride', at:nlPt(PASSENGER), reach:onDeck(0,yB), area:'aft_deck', waitsOn:['sit'] },
      { id:'hood_bin', action:'storage', label:'Storage bin', prompt:'Store · retrieve', at:nlPt({ x:0, y:0.93, z:cowlTop(0.93)+0.010 }), reach:{ x:q3(-(XS+wellF/2)), y:0.15, z:DECK }, area:null, offWalk:'footwell_port',
        note:'under the lid on the cowl’s crown, reached from the forward end of the port footwell' },
      { id:'swim', action:'enter_water', label:'Boarding deck', prompt:'Swim', at:nlPt({ x:0, y:-L/2, z:PLAT }), reach:onDeck(0,yA), area:'aft_deck', waitsOn:['ladderOn'],
        note:'off the stern; back aboard from the swim waits on ladderOn (see _excluded reboarding)' },
    ];
    const cleats=[
      { id:'bow_eye', type:'bow_eye', pos:nlV([PAINTER.x,PAINTER.y,PAINTER.z]), note:'the painter and trailer eye on the stem: she lies to it at a wharf' },
      { id:'stern_eye', type:'eye', pos:nlV([STERN_EYE.x,STERN_EYE.y,STERN_EYE.z]), note:'the stern eye on the transom over the pump: the stern line and the tow line' },
    ];
    return {
      schema:'hidden-harbours/hull-walk@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:HULL_ID, label:LABEL,
      kind:'open', units:'metres', frame:NL_FRAME,
      loa_m:q3(LOA), loh_m:L, beam_m:q3(2*T[3][1]), DWL:WATERLINE, cockpitZ:DECK,
      cell:{ W, H, pivot:{ x:cx, y:cy }, pxPerM:PX, defaultElev:DEFAULT_ELEV, order:['N','NE','E','SE','S','SW','W','NW'] },
      tags:{ lv:Object.assign({}, LV), ro:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice() },
      walk:{ radius:R, stepTol:NL_WALK.stepTol, rule:NL_RULE, spawn:{ x:0, y:yM, area:'aft_deck' }, areas },
      house:null,
      anchors:{ helm:nlPt(HELM), passenger:nlPt(PASSENGER), seats:SEATS.map(s=>Object.assign({ id:s.id }, nlPt(s))),
        feet:FEET.map(p=>Object.assign({ id:p.id }, nlPt(p))), grips:GRIPS.map(p=>Object.assign({ id:p.id }, nlPt(p))),
        handlebar:{ pivot:nlPt(HB_PIVOT), head:nlPt(HB_HEAD), axis:HANDLEBAR.axis.map(v=>Math.round(v*1e6)/1e6), lock_deg:HANDLEBAR.lock },
        nozzle:{ pivot:nlPt(NZ_PIVOT), axis:NOZZLE.axis.slice(), lock_deg:NOZZLE.lock },
        painter:nlPt(PAINTER), sternEye:nlPt(STERN_EYE), platform:nlPt({ x:0, y:yM, z:PLAT }) },
      interact, cleats, washboards:[], _excluded,
      rock:{ frames:ROCK.frames, rollA:ROCK.rollA, pitchA:ROCK.pitchA, heaveA:ROCK.heaveA, period:ROCK.period },
      qa:{ fitCheck:fitCheck() },
    };
  }
  const HULL={ id:HULL_ID, label:LABEL, kind:'open', faceList, partFaces, interiorFaces, doorFaces, geometry, matsFor, walk, fitCheck, render, renderCut,
    GAIN, BIAS, LN, BAYER, PX, W, H, pivot:{ x:cx, y:cy }, defaultElev:DEFAULT_ELEV, KEY, ROCK, HANDLEBAR, NOZZLE,
    schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, DWL:WATERLINE, cockpitZ:DECK, loa:LOA };
  const HULLS={ jet_ski:HULL };
  function byId(id){ return HULLS[id]||null; }

  root.JetSkiIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV, KEYLINE_DEFAULT,
    order:['N','NE','E','SE','S','SW','W','NW'],
    render, renderHandlebar, renderNozzle, renderWheel:renderHandlebar, renderDrive:renderNozzle,
    allFaces:(o)=>assemble(stateOf(o||{})).F, handlebarFaces, nozzleFaces, HANDLEBAR, NOZZLE,
    ROCK, rock:rockMotion, PLANE, plane:planeMotion, TURN, turn:turnMotion, addPose:posesAdd,
    SPEC, stateOf,
    helmSeat:_anchor(HELM), passengerSeat:_anchor(PASSENGER), handlebarPivot:_anchor(HB_PIVOT), nozzlePivot:_anchor(NZ_PIVOT),
    painter:_anchor(PAINTER), seats:many(SEATS),
    HELM, PASSENGER, SEATS, FEET, GRIPS, HB_PIVOT, HB_HEAD, NZ_PIVOT, PAINTER, STERN_EYE, GRAB,
    L, LOA, BEAM:2*T[3][1], DECK, PLAT, WATERLINE, station,
    SCHEMES, schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, SLOTS,
    palette, rampFrom, chipWall, C_CAP,
    PAINT, STEEL, GLAS, MOTO, KEY,
    // the new layout (ADR 0048 §8 A): one hull, through byId('jet_ski'); the same names for the default hull
    byId, HULLS, hullIds:Object.keys(HULLS), defaultHull:HULL_ID, LV, RO, GHOST_ROLES,
    faceList, partFaces, interiorFaces, doorFaces, geometry, matsFor, MATS:matsFor(DEFAULT_SCHEME).MATS, walk, fitCheck, renderCut,
    GAIN, BIAS, LN, BAYER, cell:{ W, H, pivot:{ x:cx, y:cy }, PX, defaultElev:DEFAULT_ELEV } };
})(typeof globalThis!=='undefined'?globalThis:window);

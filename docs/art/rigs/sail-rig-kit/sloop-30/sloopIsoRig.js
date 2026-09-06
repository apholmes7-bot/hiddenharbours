/* Hidden Harbours — parametric ISO SLOOP 30 (fibreglass fractional sloop) — M2 bake recipe, ADR-0006,
   same pipeline as sportSkiffIsoRig.js / lobsterBoatIsoRig.js. PASS 1: hull, deck, coachroof, cockpit,
   the whole rig, and a cabin you can walk into.

   The first SAIL in the fleet. ~9.4 m LOA (30 ft), 2.98 m beam, plumb bow, wide chined stern with an open
   walk-through transom and a swim step, long hull windows, a low coachroof with a raked front, a single
   pedestal wheel, cabin-top halyard winches, coaming primaries. Fractional rig on two swept spreader
   sets: masthead 13.0 m above the canoe body, boom 3.55 m, main P 9.5 / E 3.4 with four full battens
   and a square-ish head, 105% jib on a roller furler, split backstay, twin lifelines on seven
   stanchions a side, bow pulpit and quarter pushpits.

   THE SAILS ARE POSE, NOT PIXELS. render(dir,{awa,aws,main,jib,...}) reads the apparent wind (awa deg,
   + = wind over the starboard bow; aws kn) and two sheets (0 = eased, 1 = hardened) and derives the
   whole picture from one law, sailPose():
     boom  = min(sheet limit, |awa| - 6)         the wind pushes the boom out to the sheet, never past
     aoa   = |awa| - boom                        angle of attack
     fill  = clamp((aoa-5)/13) * clamp(aws/6)    camber, from a slack luffing cloth to a full belly
     luff  = clamp((8-aoa)/8)                    flogging near the luff; in irons (|awa|<25) both sails flog
     stall = aoa > 40                            eased far past the wind: full but dead — the leech falls off
     heel  = min(24, 0.067*aws^2*(0.25+0.75*fill_main)*k(awa)) degrees to leeward, on top of rock(i)
   `frame` 0..7 drives the flogging cloth, the winch handle, and (rock:true) the wave.
   Hoist/lower/store: opts.hoist 0..1 shortens the luff along the mast and grows the stack pack on the
   boom; opts.cover zips it shut at hoist 0 — the STORED state. opts.furl 0..1 rolls the jib onto the
   forestay; the rolled sausage wears the UV strip (canvas slot). Both sheets are drawn as ROPE that
   follows the pose: the mainsheet from the boom end to its sole block, the working jib sheet taut
   through its car to the leeward primary, the lazy sheet slack across the foredeck, lazy jacks, topping
   lift, halyard tails and the furling line along the port deck. opts.grind:'jib'|'main' puts a turning
   handle on that winch.

   THE CABIN. opts.view:'cabin' cuts the boat open: deck and coachroof over the accommodation are lifted,
   the camera-side topsides, coachroof wall and companionway bulkhead are culled at a bright section lip
   (0.62 m — the boot top), and the room is drawn: teak sole, companionway steps, galley (stove) port,
   head compartment starboard, settees and table, lockers, compression post, V-berth through the forward
   bulkhead. opts.doorOpen 0..1 swings the companionway door 105° outward on its port hinge (default
   CLOSED, owner ruling 2026-08-19); opts.hatchOpen slides the hatch forward (follows the door unless set).
   doorMount(dir,opts) -> threshold + leading edge + clear flag for the enter cue. Boat interiors ROCK:
   the cabin view takes the same pose as the exterior, so the two never shear.

   ORIGIN. Amidships / CANOE-BODY BOTTOM / centreline, pinned every heading — the fleet convention. The
   design waterline is 0.55 m above it (WATERLINE in the sidecar carries the shader's cut). The fin keel
   (1.90 m draft) and spade rudder are baked only with opts.underbody — afloat they are under the water
   the shader owns.

   Cell W x H below, pivot (cx,cy). 32 px = 1 m. Paint never moves a vertex: SCHEMES swap ramps only.
   Exposes globalThis.SloopIso = { W,H,PX,DIRS,pivot,order,ROCK,rock(i),render(dir,opts),renderWheel,
   sailPose,poseOf,anchors,doorMount,DOOR,HATCH,RIG,SAILS,ANCHOR_PTS,CLEAT_PTS,loft,geometry,
   gameplayGeometry,SCHEMES,schemeIds,defaultScheme,SLOTS,palette,rampFrom,chipWall,C_CAP,
   PAINT,STEEL,DECKF,GLAS,MOTO,ROPE,TEAK,CREAM,KEY }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 400, H = 552, cx = 200, cy = 432;   // locked by the cell-fit sweep (elev 30–50 × 8 dirs × heel × boom × rock)
  const DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const ROCK = { frames:8, rollA:2.4, pitchA:1.5, heaveA:1.3, period:3.2 };
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 9.4, TH = 0.05;
  const DWL = 0.55;          // design waterline above the canoe-body bottom
  const SOLE = 1.15;         // cockpit sole
  const CAB_SOLE = 0.35;     // cabin sole
  const ZLIP = 0.62;         // cabin section cut — the boot-top line
  const NSEG = 22;
  const clamp01=(v)=>v<0?0:v>1?1:v, clamp=(v,a,b)=>v<a?a:v>b?b:v;
  const lerp=(a,b,t)=>a+(b-a)*t;

  // ---- fixed ramps (never painted) -------------------------------------------------------------
  const PAINT = ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];  // white gelcoat (fleet)
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];            // stainless
  const DECKF = ['#6a7069','#848a82','#9ca29a','#b4bab0','#cad0c5'];                      // moulded non-skid
  const GLAS  = ['#16333c','#24505a','#3a7680','#5fa3a6','#8fc9c4'];                      // smoked teal glass
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];  // dark fittings
  const ROPE  = ['#54432c','#7a6242','#a98f66','#cdbe97','#e3dbc1'];                      // hemp-tone line (RopeProps)
  const TEAK  = ['#3f2814','#54351d','#6c4626','#855a31','#9c6e40','#b28553'];            // varnished teak (sport fisher)
  const CREAM = ['#868e93','#a2aaae','#bfc6c6','#d6dbd7','#e8ebe5','#f2f4ee','#fbfcf6'];  // interior liner (lobster boat)
  const KEY   = '#101a19';

  // ---- paint mixer (OKLCH) — identical envelope to the small craft ------------------------------
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
  const HUE_WARM=92, HUE_COOL=258, C_CAP=0.115;
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

  // ---- colourways --------------------------------------------------------------------------------
  // Slots: hull (topsides + coachroof gelcoat), stripe (cove line + boot top), bottom (anti-foul),
  // deck (non-skid), canvas (stack pack, jib UV strip, cockpit dodger bag), sail (cloth), teak
  // (cockpit sole, seats, swim step, cabin joinery), uph (cockpit + saloon cushions).
  const SCHEMES = {
    'gelcoat-white': { name:'Gelcoat White', hull:'#eef0ea', stripe:'#7a2f22', bottom:'#2a2f33', deck:'#c6c9bd',
                       canvas:'#22354a', sail:'#d9dbd6', teak:'#9c6e40', uph:'#d9c79a',
                       ramps:{ hull:PAINT, teak:TEAK },
                       note:'the yard\u2019s own \u2014 white glass, bordeaux cove, navy canvas, grey laminate' },
    'atlantic-navy': { name:'Atlantic Navy', hull:'#254a6b', stripe:'#eef0ea', bottom:'#8c3f2c', deck:'#b4bab0',
                       canvas:'#c9b98f', sail:'#eef0ea', teak:'#9c6e40', uph:'#e6ddc6',
                       note:'navy hull, white cove, sand canvas, white dacron' },
    'oyster-bone':   { name:'Oyster Bone',   hull:'#e6ddc6', stripe:'#22354a', bottom:'#22354a', deck:'#b8b7ac',
                       canvas:'#7a2f22', sail:'#efe9d6', teak:'#9c6e40', uph:'#9aa6a4',
                       note:'bone gelcoat, navy cove, bordeaux canvas, cream cloth' },
    'squall-grey':   { name:'Squall Grey',   hull:'#8a99a1', stripe:'#22354a', bottom:'#2a2f33', deck:'#909c99',
                       canvas:'#343b41', sail:'#b9bfc1', teak:'#855a31', uph:'#a9b0a8',
                       note:'grey-blue hull, charcoal canvas, grey laminate' },
    'bottle-green':  { name:'Bottle Green',  hull:'#31694c', stripe:'#e2dcc7', bottom:'#8c3f2c', deck:'#c3bda6',
                       canvas:'#b89a6a', sail:'#a8563a', teak:'#9c6e40', uph:'#cdb98d',
                       note:'green hull, cream cove, tan canvas, TANBARK sails' },
    'graphite':      { name:'Graphite',      hull:'#343b41', stripe:'#2ba39a', bottom:'#2a2f33', deck:'#8d938c',
                       canvas:'#1d2127', sail:'#6b7378', teak:'#6c4626', uph:'#8f8a7e',
                       note:'dark glass, teal cove, black canvas, dark laminate' },
    'cranberry':     { name:'Cranberry',     hull:'#a8452f', stripe:'#e6ddc6', bottom:'#2a2f33', deck:'#c0bcae',
                       canvas:'#c9b98f', sail:'#eef0ea', teak:'#9c6e40', uph:'#e6ddc6',
                       note:'bog red hull, cream cove, sand canvas' },
    'seafoam':       { name:'Seafoam',       hull:'#a3d9c8', stripe:'#7a2f22', bottom:'#7a2f22', deck:'#c6c9bd',
                       canvas:'#22354a', sail:'#eef0ea', teak:'#9c6e40', uph:'#c8b58c',
                       note:'mint hull, oxblood cove + bottom, navy canvas' },
  };
  const DEFAULT_SCHEME='gelcoat-white';
  const SLOTS=['hull','stripe','bottom','deck','canvas','sail','teak','uph'];
  const _pal={}; let _palOrder=[];
  function palette(o){
    o=o||{};
    const DF=SCHEMES[DEFAULT_SCHEME];
    const base = o.paint || SCHEMES[o.scheme] || DF;
    const B={}; SLOTS.forEach(k=>{ B[k]=base[k]||DF[k]; });
    const key=SLOTS.map(k=>B[k]).join('|')+(base.ramps?'|baked':'|mix');
    if(_pal[key]) return _pal[key];
    const R=base.ramps||{};
    const hullR   = R.hull   || rampFrom(B.hull,7);
    const stripeR = R.stripe || rampFrom(B.stripe,5,{anchor:0.75});
    const botR    = R.bottom || rampFrom(B.bottom,5,{anchor:0.75});
    const deckR   = R.deck   || rampFrom(B.deck,5,{anchor:0.70});
    const canvR   = R.canvas || rampFrom(B.canvas,5,{anchor:0.72});
    const sailR   = R.sail   || rampFrom(B.sail,7,{anchor:0.74,ceil:0.55,floor:0.62});
    const teakR   = R.teak   || rampFrom(B.teak,6,{anchor:0.70});
    const uphR    = R.uph    || rampFrom(B.uph,5,{anchor:0.72});
    const mats = { paint:{ramp:hullR,off:0,dith:0},    stripe:{ramp:stripeR,off:-1,dith:0},
                   bottom:{ramp:botR,off:-1,dith:0},   deck:{ramp:deckR,off:0,dith:0.28},
                   liner:{ramp:hullR,off:0,dith:0},    canvas:{ramp:canvR,off:0,dith:0.15},
                   sail:{ramp:sailR,off:0,dith:0.10},  batten:{ramp:sailR,off:-2,dith:0},
                   teak:{ramp:teakR,off:0,dith:0.12},  uph:{ramp:uphR,off:0,dith:0.18},
                   cream:{ramp:CREAM,off:0,dith:0},    steel:{ramp:STEEL,off:0,dith:0},
                   mast:{ramp:STEEL,off:-1,dith:0},    wire:{ramp:STEEL,off:-2,dith:0},
                   rope:{ramp:ROPE,off:0,dith:0},      glas:{ramp:GLAS,off:0,dith:0},
                   moto:{ramp:MOTO,off:0,dith:0},      blk:{ramp:MOTO,off:-2,dith:0} };
    const rindex={};
    [hullR,stripeR,botR,deckR,canvR,sailR,teakR,uphR,CREAM,STEEL,ROPE,GLAS,MOTO,PAINT,TEAK].forEach(r=>r.forEach((c,i)=>{ if(!rindex[c]) rindex[c]={r,i}; }));
    const out={ key, id:o.paint?'custom':(SCHEMES[o.scheme]?o.scheme:DEFAULT_SCHEME), name:base.name||'Custom', base:B, note:base.note||'',
                ramps:{hull:hullR,stripe:stripeR,bottom:botR,deck:deckR,canvas:canvR,sail:sailR,teak:teakR,uph:uphR}, mats, rindex };
    _pal[key]=out; _palOrder.push(key);
    if(_palOrder.length>24){ delete _pal[_palOrder.shift()]; }
    return out;
  }

  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- section table: stern(0) -> bow(8) --------------------------------------------------------
  // [sheerHalf, bilgeHalf, keelHalf, bilgeRise, sheerRise, keelZ] (m). A modern cruiser: beam carried aft,
  // hard turn of bilge aft (the chine), fine plumb entry, sheer rising 1.55 -> 2.05 m stern to stem.
  const T = [
    [1.30, 1.05, 0.10, 0.30, 1.15, 0.40],   // transom             sheer 1.55
    [1.44, 1.22, 0.12, 0.40, 1.43, 0.15],   //                           1.58
    [1.48, 1.30, 0.14, 0.48, 1.57, 0.04],   //                           1.61
    [1.49, 1.32, 0.14, 0.52, 1.65, 0.00],   // max beam 2.98 m           1.65
    [1.47, 1.26, 0.13, 0.54, 1.70, 0.00],   //                           1.70
    [1.38, 1.10, 0.11, 0.55, 1.74, 0.02],   //                           1.76
    [1.20, 0.84, 0.08, 0.55, 1.75, 0.08],   //                           1.83
    [0.86, 0.48, 0.05, 0.55, 1.72, 0.20],   // fine entry                1.92
    [0.12, 0.08, 0.03, 0.45, 1.63, 0.42],   // stem head                 2.05
  ];
  const FC = 0.42, FK = 0.62;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), ch:lerp(A[1],B[1],fr), kw:lerp(A[2],B[2],fr),
             cd:lerp(A[3],B[3],fr), dep:lerp(A[4],B[4],fr), kz:lerp(A[5],B[5],fr), y:-L/2+u*L };
  }
  const RAKE=0.10;
  const rakeAt=(u,frac)=>-RAKE*Math.pow(Math.max(0,(u-0.80)/0.20),1.6)*(1-frac);
  function skin(side,u,frac,inset){
    const st=station(u), t=inset?1:0;
    const ws=st.ws-(t?TH:0), chh=st.ch-(t?TH:0), kw=Math.max(0.004, st.kw-(t?TH*0.5:0));
    const kz=st.kz+(t?TH*0.7:0), cz=st.kz+st.cd+(t?TH*0.3:0), sz=st.kz+st.dep-(t?0.012:0);
    let x,z;
    if(frac<=FC){ const q=frac/FC; x=lerp(kw,chh,q); z=lerp(kz,cz,q); }
    else {
      const K=(FK-FC)/(1-FC);
      const knuck=0.035*Math.pow(Math.max(0,1-u/0.70),1.2);
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
  const deckZ=(u)=>sheerZ(u)-0.02;
  const uOf=(y)=>(y+L/2)/L, yOf=(u)=>-L/2+u*L;

  // ---- plan constants ----------------------------------------------------------------------------
  const RF = { yA:-0.55, yF:3.05, yNose:3.45, hA:0.62, hF:0.42, sdA:0.42, sdF:0.54 };   // coachroof
  const rfT=(y)=>clamp01((y-RF.yA)/(RF.yF-RF.yA));
  const roofZ=(y)=>{ const t=rfT(y); return deckZ(uOf(y)) + RF.hA + (RF.hF-RF.hA)*t*t; };
  const hxRoof=(y)=>{ const t=rfT(y); return Math.max(0.30, halfAtZ(uOf(y), deckZ(uOf(y)), 1) - (RF.sdA + (RF.sdF-RF.sdA)*t)); };
  const CK = { yA:-0.62, yB:-3.05, yT:-4.55, xi:0.62, seatZ:1.60, coamZ:1.88, coamT:0.10 };   // cockpit
  const xCoam=(y)=>halfAtZ(uOf(y), deckZ(uOf(y)), 1) - 0.42;      // outer face of the coaming = inner edge of the side deck
  const MAST = { x:0, y:0.85, footZ:roofZ(0.85), headZ:13.0, rakeDeg:1.5 };
  const mastY=(z)=>MAST.y - (z-MAST.footZ)*Math.tan(MAST.rakeDeg*DEG);
  const mastAt=(z)=>[0, mastY(z), z];
  const GOOSE = [0, mastY(3.05)-0.09, 3.05];
  const BOOM_L = 3.55;
  const MAIN = { P:9.5, E:3.4, head:0.30, roach:0.42, battens:[0.22,0.44,0.66,0.86] };
  const FORESTAY = { foot:[0, 4.58, 2.10], headZ:12.0 };
  FORESTAY.head = mastAt(FORESTAY.headZ); FORESTAY.head[1]+=0.06;
  const fsAt=(t)=>[0, lerp(FORESTAY.foot[1],FORESTAY.head[1],t), lerp(FORESTAY.foot[2],FORESTAY.head[2],t)];
  const JIB = { tackT:0.03, headT:0.965, LP:3.6, clewUp:0.42 };
  JIB.tack=fsAt(JIB.tackT); JIB.head=fsAt(JIB.headT);
  JIB.luff=Math.hypot(JIB.head[1]-JIB.tack[1], JIB.head[2]-JIB.tack[2]);
  const SPREADERS = [ { z:6.2, len:0.95, sweep:22 }, { z:9.6, len:0.70, sweep:22 } ];
  const CHAIN = { x:1.40, yCap:0.95, yFwd:1.28, yAft:0.62 };
  const WINCH = { primaryY:-2.30, primaryX:()=>xCoam(-2.30)-0.02, cabinY:-0.20, cabinX:0.62, clutchY:0.22 };
  const CAR_Y = 1.0, carX=()=>halfAtZ(uOf(CAR_Y),deckZ(uOf(CAR_Y)),1)-0.22;
  const SHEET_BLOCK = [0, -2.80, SOLE+0.05];
  const PED = { x:0, y:-3.35, hx:0.12, hy:0.10, h:0.72 };
  const WHEEL_HUB = { x:0, y:-3.49, z:SOLE+0.82 };
  const WHEEL_GEO = { rad:0.45, rimIn:0.405, rake:-8, spokes:8, seg:16, hubR:0.06 };
  const WHEEL_LOCK = 1.5;
  const DOOR = { kind:'hinge', face:'aft', y:RF.yA, x0:-0.32, x1:0.32, z0:SOLE+0.15, z1:2.18,
                 hinge:'port', hingeX:-0.34, leaf:{ w:0.66, th:0.04 }, swing:105, outward:true, clearAt:0.62 };
  const HATCH = { x0:-0.44, x1:0.44, y0:-0.60, y1:0.28, travel:0.72, aperture:{ x0:-0.34, x1:0.34, y0:-0.55, y1:0.12 } };
  const RIG = { mast:MAST, goose:GOOSE, boomL:BOOM_L, main:MAIN, forestay:FORESTAY, jib:JIB, spreaders:SPREADERS, chain:CHAIN };

  // ---- generic solids ----------------------------------------------------------------------------
  const ID=(p)=>p;
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  const lerp3=(a,b,t)=>[lerp(a[0],b[0],t),lerp(a[1],b[1],t),lerp(a[2],b[2],t)];
  const rotZabout=(p,C,a)=>{ const c=Math.cos(a), s=Math.sin(a), x=p[0]-C[0], y=p[1]-C[1];
    return [C[0]+x*c-y*s, C[1]+x*s+y*c, p[2]]; };
  function mk(v,mat,b,db,ex){ const f={v,mat:mat||'paint',b:b||0,db:db||0}; if(ex) Object.assign(f,ex); return f; }
  function box(out,c,h,mat,b,db,xf,ex,mats){
    xf=xf||ID;
    const P=(sx,sy,sz)=>xf([c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]]);
    const Q=[ [P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)], [P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)],
              [P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)], [P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)],
              [P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)], [P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)] ];
    Q.forEach((v,i)=>out.push(mk(v,(mats&&mats[i])||mat,b,db,ex)));
  }
  // square/rectangular bar from p0 to p1; r = half-thickness or [r1,r2]
  function bar(out,p0,p1,r,mat,b,db,ex){
    const d=v_sub(p1,p0), Ln=Math.hypot(d[0],d[1],d[2])||1, u=v_mul(d,1/Ln);
    let a=Math.abs(u[2])<0.9?[0,0,1]:[1,0,0];
    let n1=v_norm(v_cross(u,a)); const n2=v_cross(n1,u);
    const rr=Array.isArray(r)?r:[r,r], c=lerp3(p0,p1,0.5);
    const P=(su,s1,s2)=>[ c[0]+u[0]*su*Ln/2+n1[0]*s1*rr[0]+n2[0]*s2*rr[1],
                          c[1]+u[1]*su*Ln/2+n1[1]*s1*rr[0]+n2[1]*s2*rr[1],
                          c[2]+u[2]*su*Ln/2+n1[2]*s1*rr[0]+n2[2]*s2*rr[1] ];
    const Q=[ [P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)], [P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)],
              [P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)], [P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)],
              [P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)], [P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)] ];
    for(const v of Q) out.push(mk(v,mat,b,db==null?-0.12:db,ex));
  }
  function prism(out,c,rad,h,n,mat,b,db,ex){
    const top=[], bot=[];
    for(let k=0;k<n;k++){ const a=2*Math.PI*k/n+Math.PI/n;
      top.push([c[0]+rad*Math.cos(a), c[1]+rad*Math.sin(a), c[2]+h]);
      bot.push([c[0]+rad*Math.cos(a), c[1]+rad*Math.sin(a), c[2]]); }
    out.push(mk(top,mat,b,db,ex));
    for(let k=0;k<n;k++){ const k2=(k+1)%n; out.push(mk([top[k],top[k2],bot[k2],bot[k]],mat,b,db,ex)); }
  }
  // rope along a polyline; sag hangs each leg in a shallow catenary (metres at mid-span)
  function rope(out,pts,r,mat,b,sag,ex){
    for(let i=0;i+1<pts.length;i++){
      const A=pts[i], B=pts[i+1], sg=Array.isArray(sag)?(sag[i]||0):(sag||0);
      if(sg<=0.005){ bar(out,A,B,r,mat,b,-0.10,ex); continue; }
      const N=6; let prev=A;
      for(let k=1;k<=N;k++){ const t=k/N, p=lerp3(A,B,t); p[2]-=4*sg*t*(1-t); bar(out,prev,p,r,mat,b,-0.10,ex); prev=p; }
    }
  }
  const face=(F,v,mat,b,db,ex)=>F.push(mk(v,mat,b,db,ex));

  // ---- paint bands (outer skin), keyed to the LEVEL waterline ---------------------------------------
  const fAtZ=(u,z)=>fracAtZ(station(u),z);
  const fB=(d,u)=>{ const st=station(u); return Math.min(0.996, Math.max(FC+0.05, fracAtZ(st, st.kz+st.dep-d))); };
  const F_BOOT0=(u)=>fAtZ(u,DWL-0.05), F_BOOT1=(u)=>fAtZ(u,ZLIP);
  const F_COVE=(u)=>fB(0.20,u), F_SHEER=(u)=>fB(0.11,u);
  const OB = [ [0, F_BOOT0, 'bottom', -0.2, 0, false],
               [F_BOOT0, F_BOOT1, 'stripe', 0.30, 0.005, false],
               [F_BOOT1, F_COVE, 'paint', 0, 0, true],
               [F_COVE, F_SHEER, 'stripe', 0.35, 0.006, true],
               [F_SHEER, 1, 'paint', 0.10, 0, true] ];
  const fv=(f,u)=>typeof f==='function'?f(u):f;
  const CAB_U0=uOf(RF.yA), CAB_U1=uOf(4.45);
  const inCabin=(u0,u1)=>u1>CAB_U0+1e-6 && u0<CAB_U1-1e-6;

  // ---- the static mesh -----------------------------------------------------------------------------
  const F = [];
  (function build(){
    const CUT=(u0,u1,extra)=>inCabin(u0,u1)?Object.assign({cut:'cabin'},extra||{}):(extra||null);
    // ---- hull skin, both sides ----
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        // outward horizontal normal of this segment (perpendicular to the sheer run in plan)
        const A=skin(side,u0,1), B=skin(side,u1,1), dx=B[0]-A[0], dy=B[1]-A[1], m=Math.hypot(dx,dy)||1;
        const onx=[ dy/m, -dx/m, 0 ]; if(onx[0]*side<0){ onx[0]=-onx[0]; onx[1]=-onx[1]; }
        for(const [f0,f1,mat,b,db,cuttable] of OB){
          const ex = cuttable ? CUT(u0,u1,{cutN:onx}) : null;
          face(F,[skin(side,u0,fv(f0,u0)),skin(side,u1,fv(f0,u1)),skin(side,u1,fv(f1,u1)),skin(side,u0,fv(f1,u0))],mat,b,db,ex);
        }
        // deck edge: low stainless cap on the sheer over a dark insert (the toe rail line)
        const oa=skin(side,u0,1), ob2=skin(side,u1,1);
        const inb=(p)=>[p[0]-side*TH*0.8,p[1],p[2]];
        const or0=[oa[0],oa[1],oa[2]+0.002], or1=[ob2[0],ob2[1],ob2[2]+0.002];
        const capEx = CUT(u0,u1,{cutN:onx});
        face(F,[or0,or1,inb(ob2),inb(oa)],'steel',0.8,0.04,capEx);
        face(F,[or0,or1,[or1[0],or1[1],or1[2]-0.045],[or0[0],or0[1],or0[2]-0.045]],'blk',0.15,0.04,capEx);
        // keel pad along the canoe bottom
        if(side>0) face(F,[skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'bottom',-0.9);
        // cockpit liner in the helm area (sole -> sheer)
        if(u1<=uOf(CK.yB)+1e-6){
          const fa=fAtZ(u0,SOLE), fb=fAtZ(u1,SOLE);
          for(let k=0;k<2;k++){
            const g0a=fa+(1-fa)*k/2, g1a=fa+(1-fa)*(k+1)/2, g0b=fb+(1-fb)*k/2, g1b=fb+(1-fb)*(k+1)/2;
            face(F,[skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'liner',-1.3);
          }
        }
        // cabin liner + section lip (interior view only)
        if(inCabin(u0,u1)){
          const a0=Math.max(u0,CAB_U0), a1=Math.min(u1,CAB_U1);
          const fs0=fAtZ(a0,CAB_SOLE), fs1=fAtZ(a1,CAB_SOLE), fl0=fAtZ(a0,ZLIP), fl1=fAtZ(a1,ZLIP);
          const fd0=fAtZ(a0,deckZ(a0)-0.04), fd1=fAtZ(a1,deckZ(a1)-0.04);
          face(F,[skin(side,a1,fs1,1),skin(side,a0,fs0,1),skin(side,a0,fl0,1),skin(side,a1,fl1,1)],'cream',-0.6,0,{lv:'cabin'});
          for(let k=0;k<2;k++){
            const g0a=fl0+(fd0-fl0)*k/2, g1a=fl0+(fd0-fl0)*(k+1)/2, g0b=fl1+(fd1-fl1)*k/2, g1b=fl1+(fd1-fl1)*(k+1)/2;
            face(F,[skin(side,a1,g0b,1),skin(side,a0,g0a,1),skin(side,a0,g1a,1),skin(side,a1,g1b,1)],'cream',-0.2,0,{lv:'cabin',cut:'cabin',cutN:onx});
          }
          const L0=skin(side,a0,fl0), L1=skin(side,a1,fl1), I0=skin(side,a0,fl0,1), I1=skin(side,a1,fl1,1);
          const up=(p)=>[p[0],p[1],p[2]+0.05];
          const lipQ=[up(I0),up(I1),up(L1),up(L0)];
          face(F,side>0?lipQ:lipQ.slice().reverse(),'cream',1.6,-0.02,{lv:'cabin',lip:'cabin',cutN:onx});
          face(F,[up(L0),up(L1),L1,L0],'cream',0.9,-0.02,{lv:'cabin',lip:'cabin',cutN:onx});
        }
        // long hull window (proud smoked panel), y 0.35..2.55
        const wy0=uOf(0.35), wy1=uOf(2.55);
        if(u0>=wy0-1e-6 && u1<=wy1+1e-6){
          const z0=1.06, z1=1.24, o=(p)=>[p[0]+side*0.012,p[1],p[2]];
          const q=[o(skin(side,u0,fAtZ(u0,z1))),o(skin(side,u1,fAtZ(u1,z1))),o(skin(side,u1,fAtZ(u1,z0))),o(skin(side,u0,fAtZ(u0,z0)))];
          face(F,q,'glas',0.55,0.02,CUT(u0,u1,{cutN:onx}));
        }
      }
    }
    // ---- transom: bands below the sole, two quarter pieces above it (open walk-through) ----
    (function(){
      const st=station(0), tz=(z)=>fracAtZ(st,z), tp=(s,f)=>skin(s,0,f);
      const bands=[[st.kz,DWL-0.05,'bottom',-0.35],[DWL-0.05,ZLIP,'stripe',0.1],[ZLIP,SOLE,'paint',-0.45]];
      for(const [z0,z1,mat,b] of bands) face(F,[tp(-1,tz(z0)),tp(1,tz(z0)),tp(1,tz(z1)),tp(-1,tz(z1))],mat,b,0.005);
      const sh=st.kz+st.dep, xg=0.38;
      for(const s of [-1,1]){
        const xo=(z)=>tp(s,tz(z))[0];
        const q1=[[s*xg,st.y,SOLE],[xo(SOLE),st.y,SOLE],[xo(sh-0.20),st.y,sh-0.20],[s*xg,st.y,sh-0.20]];
        const q2=[[s*xg,st.y,sh-0.20],[xo(sh-0.20),st.y,sh-0.20],[xo(sh-0.11),st.y,sh-0.11],[s*xg,st.y,sh-0.11]];
        const q3=[[s*xg,st.y,sh-0.11],[xo(sh-0.11),st.y,sh-0.11],[xo(sh),st.y,sh],[s*xg,st.y,sh]];
        for(const [q,mat,b] of [[q1,'paint',-0.45],[q2,'stripe',0.1],[q3,'paint',-0.35]]) face(F,s>0?q.slice().reverse():q,mat,b,0.005);
        // inner cheek of the walk-through
        face(F,s>0?[[s*xg,st.y,SOLE],[s*xg,st.y+0.16,SOLE],[s*xg,st.y+0.16,sh],[s*xg,st.y,sh]].reverse():[[s*xg,st.y,SOLE],[s*xg,st.y+0.16,SOLE],[s*xg,st.y+0.16,sh],[s*xg,st.y,sh]],'liner',-0.8,-0.01);
        const cap=[[s*xg,st.y,sh],[s*xg,st.y+0.16,sh],[xo(sh),st.y+0.16,sh],[xo(sh),st.y,sh]];
        face(F,s>0?cap:cap.slice().reverse(),'paint',0.4,-0.01);
      }
      // swim step hung off the transom, 0.20 below the sole
      box(F,[0,st.y-0.17,SOLE-0.22],[0.56,0.17,0.02],'teak',0.3,-0.01);
      box(F,[0,st.y-0.02,SOLE-0.10],[0.40,0.02,0.10],'paint',-0.6,-0.01);   // step riser under the sole edge
    })();

    // ---- cockpit ----
    (function(){
      const yA=CK.yA, yB=CK.yB, yT=CK.yT;
      // walkway sole: teak planks (strips along y)
      const NP=8, xw=CK.xi;
      for(let k=0;k<NP;k++){ const x0=-xw+2*xw*k/NP, x1=-xw+2*xw*(k+1)/NP;
        face(F,[[x0,yB,SOLE],[x1,yB,SOLE],[x1,yA,SOLE],[x0,yA,SOLE]],'teak',(k%2?0.05:-0.30)); }
      // helm sole: full width, planked athwart-ish in fewer strips
      const NH=6;
      for(let i=0;i<NH;i++){
        const y0=yB+(yT-yB)*i/NH, y1=yB+(yT-yB)*(i+1)/NH;
        const w0=halfAtZ(uOf(y0),SOLE,1)-0.06, w1=halfAtZ(uOf(y1),SOLE,1)-0.06;
        face(F,[[-w0,y0,SOLE],[w0,y0,SOLE],[w1,y1,SOLE],[-w1,y1,SOLE]],'teak',(i%2?0.05:-0.30));
      }
      // liner rim (gelcoat) at the sheer around the helm area is the deck-edge margin — drawn below
      // benches: fronts, tops (teak), aft ends; the coaming (seat back) outboard
      for(const s of [-1,1]){
        const NS2=6;
        for(let i=0;i<NS2;i++){
          const y0=yA+(yB-yA)*i/NS2, y1=yA+(yB-yA)*(i+1)/NS2;
          const xo0=xCoam(y0)-CK.coamT, xo1=xCoam(y1)-CK.coamT;
          const top=[[s*xw,y0,CK.seatZ],[s*xo0,y0,CK.seatZ],[s*xo1,y1,CK.seatZ],[s*xw,y1,CK.seatZ]];
          face(F,s>0?top:top.slice().reverse(),'teak',(i%2?0.15:-0.20),0.01);
          const fr=[[s*xw,y0,SOLE],[s*xw,y1,SOLE],[s*xw,y1,CK.seatZ],[s*xw,y0,CK.seatZ]];
          face(F,s>0?fr.slice().reverse():fr,'paint',-0.85,0.005);
          // coaming: inner face, top, and the outer (side-deck) face
          const ci=[[s*xo0,y0,CK.seatZ],[s*xo1,y1,CK.seatZ],[s*xo1,y1,CK.coamZ],[s*xo0,y0,CK.coamZ]];
          face(F,s>0?ci.slice().reverse():ci,'paint',-0.55,0.005);
          const ct=[[s*xo0,y0,CK.coamZ],[s*xCoam(y0),y0,CK.coamZ],[s*xCoam(y1),y1,CK.coamZ],[s*xo1,y1,CK.coamZ]];
          face(F,s>0?ct:ct.slice().reverse(),'paint',0.6,0.01);
          const co=[[s*xCoam(y0),y0,deckZ(uOf(y0))],[s*xCoam(y1),y1,deckZ(uOf(y1))],[s*xCoam(y1),y1,CK.coamZ],[s*xCoam(y0),y0,CK.coamZ]];
          face(F,s>0?co:co.slice().reverse(),'paint',0.05,0.005);
        }
        // aft end of the bench + coaming, facing the helm
        const xo=xCoam(yB)-CK.coamT;
        const e1=[[s*xw,yB,SOLE],[s*xCoam(yB),yB,SOLE],[s*xCoam(yB),yB,CK.coamZ],[s*xw,yB,CK.seatZ]];
        face(F,s>0?e1:e1.slice().reverse(),'paint',-0.5,0.005);
        // cockpit cushions on the benches (upholstery slot), two pads per side
        for(const [c0,c1] of [[-0.75,-1.75],[-1.95,-2.90]]){
          const xm=(s*(xw+xo))/2, hw=(xo-xw)/2-0.04;
          box(F,[xm,(c0+c1)/2,CK.seatZ+0.045],[hw,(c0-c1)/2,0.045],'uph',0.45,0.012);
        }
      }
      // companionway bulkhead (aft face of the coachroof + cockpit forward wall) built around the opening
      const yq=RF.yA, hx=hxRoof(yq), rz=roofZ(yq);
      const wall=(x0,x1,z0,z1,b)=>face(F,[[x0,yq,z0],[x1,yq,z0],[x1,yq,z1],[x0,yq,z1]].reverse(),'paint',b,0.004,{cut:'cabin',cutN:[0,-1,0]});
      wall(-CK.xi, CK.xi, SOLE, DOOR.z0, -0.35);               // sill wall below the door
      wall(-CK.xi, DOOR.x0, DOOR.z0, CK.seatZ, -0.3); wall(DOOR.x1, CK.xi, DOOR.z0, CK.seatZ, -0.3);
      wall(-hx, DOOR.x0, CK.seatZ, rz, -0.25); wall(DOOR.x1, hx, CK.seatZ, rz, -0.25);
      wall(DOOR.x0, DOOR.x1, DOOR.z1, rz, -0.25);
      // dim vestibule behind the opening (exterior view only — the cabin view has the real steps)
      face(F,[[DOOR.x0,yq+0.30,DOOR.z0],[DOOR.x1,yq+0.30,DOOR.z0],[DOOR.x1,yq+0.30,DOOR.z1],[DOOR.x0,yq+0.30,DOOR.z1]].reverse(),'blk',-0.4,0,{lv:'lid'});
      // jambs + sill nosing
      box(F,[0,yq-0.02,DOOR.z0-0.02],[DOOR.x1+0.03,0.03,0.02],'steel',0.5,-0.02);
      // wheel pedestal + compass, mainsheet block + cleat
      box(F,[PED.x,PED.y,SOLE+PED.h/2],[PED.hx,PED.hy,PED.h/2],'paint',-0.2,-0.01);
      prism(F,[PED.x,PED.y,SOLE+PED.h],0.10,0.09,10,'blk',0.2,-0.02);
      prism(F,[PED.x,PED.y,SOLE+PED.h+0.09],0.075,0.06,10,'glas',0.9,-0.03);
      box(F,[SHEET_BLOCK[0],SHEET_BLOCK[1],SOLE+0.035],[0.05,0.05,0.035],'blk',0.2,-0.02);
      box(F,[0,PED.y-PED.hy-0.03,SOLE+0.42],[0.045,0.03,0.02],'steel',0.6,-0.03);
      // primaries on coaming pads + cabin-top winches, clutches, furling-line cleat
      for(const s of [-1,1]){
        const px=WINCH.primaryX();
        box(F,[s*px,WINCH.primaryY,(CK.seatZ+CK.coamZ)/2],[0.13,0.14,(CK.coamZ-CK.seatZ)/2],'paint',0.1,-0.01);
        drum(F,[s*px,WINCH.primaryY,CK.coamZ]);
        box(F,[s*(xCoam(-1.0)-0.05),-1.0,CK.coamZ+0.02],[0.05,0.035,0.02],'steel',0.55,-0.03);   // furling / spare cleats
      }
      // quarter cleats
      for(const s of [-1,1]) box(F,[s*1.18,-4.40,deckZ(uOf(-4.40))+0.035],[0.026,0.095,0.036],'steel',0.5,-0.02);
    })();
    function drum(out,c){
      prism(out,[c[0],c[1],c[2]],0.095,0.06,10,'steel',0.35,-0.02);
      prism(out,[c[0],c[1],c[2]+0.06],0.085,0.09,10,'blk',0.15,-0.02);
      prism(out,[c[0],c[1],c[2]+0.15],0.095,0.05,10,'steel',0.7,-0.03);
    }

    // ---- decks: side decks along the coachroof and cockpit, foredeck, aft margins ----
    (function(){
      const DS=18;
      const edgeX=(u)=>halfAtZ(u,deckZ(u),1)-0.006;
      // side decks from the transom quarters to the coachroof front: margin (gelcoat) + non-skid panel
      for(const s of [-1,1]){
        for(let i=0;i<DS;i++){
          const u0=uOf(CK.yT)+(uOf(RF.yF)-uOf(CK.yT))*i/DS, u1=uOf(CK.yT)+(uOf(RF.yF)-uOf(CK.yT))*(i+1)/DS;
          const y0=yOf(u0), y1=yOf(u1), z0=deckZ(u0), z1=deckZ(u1);
          const inner=(y)=> y<=CK.yB ? halfAtZ(uOf(y),SOLE,1)-0.06 : (y<=RF.yA ? xCoam(y) : hxRoof(y));
          const e0=edgeX(u0), e1=edgeX(u1), i0=inner(y0), i1=inner(y1);
          const lid = (y0>RF.yA-0.01) ? {lv:'lid'} : null;
          const m0=Math.max(i0,e0-0.14), m1=Math.max(i1,e1-0.14);
          const mq=[[s*m0,y0,z0],[s*e0,y0,z0],[s*e1,y1,z1],[s*m1,y1,z1]];
          face(F,s>0?mq:mq.slice().reverse(),'paint',0.35,0,lid);
          if(m0>i0+0.02 && m1>i1+0.02){
            const pq=[[s*i0,y0,z0],[s*m0,y0,z0],[s*m1,y1,z1],[s*i1,y1,z1]];
            face(F,s>0?pq:pq.slice().reverse(),'deck',0.35,0,lid);
          }
        }
      }
      // foredeck from the coachroof nose to the stem head
      const BS=10, u0f=uOf(RF.yNose), u1f=0.985;
      for(let i=0;i<BS;i++){
        const u0=u0f+(u1f-u0f)*i/BS, u1=u0f+(u1f-u0f)*(i+1)/BS;
        const e0=edgeX(u0), e1=edgeX(u1), y0=yOf(u0)+rakeAt(u0,1), y1=yOf(u1)+rakeAt(u1,1), z0=deckZ(u0), z1=deckZ(u1);
        const lid = (yOf(u0)<4.45) ? {lv:'lid'} : null;
        const m0=Math.max(0.02,e0-0.14), m1=Math.max(0.02,e1-0.14);
        face(F,[[-e0,y0,z0],[e0,y0,z0],[e1,y1,z1],[-e1,y1,z1]],'paint',0.4,0,lid);
        if(m0>0.05) face(F,[[-m0,y0,z0+0.001],[m0,y0,z0+0.001],[m1,y1,z1+0.001],[-m1,y1,z1+0.001]],'deck',0.4,-0.01,lid);
      }
      // stem head: anchor roller cheeks + furler drum + bow cleats
      const ys=yOf(0.975)+rakeAt(0.975,1), zs=deckZ(0.975);
      box(F,[0,ys-0.05,zs+0.02],[0.04,0.09,0.02],'blk',0.1,-0.05);
      box(F,[0,ys-0.05,zs+0.036],[0.02,0.06,0.009],'steel',0.75,-0.06);
      prism(F,[FORESTAY.foot[0],FORESTAY.foot[1]-0.02,FORESTAY.foot[2]-0.02],0.075,0.16,10,'blk',0.15,-0.02);
      for(const s of [-1,1]){
        box(F,[s*0.34,4.35,deckZ(uOf(4.35))+0.035],[0.026,0.09,0.036],'steel',0.5,-0.02);
        box(F,[s*1.36,0.10,deckZ(uOf(0.10))+0.035],[0.026,0.095,0.036],'steel',0.5,-0.02);
      }
      // jib sheet tracks + cars
      for(const s of [-1,1]){
        const x=carX();
        bar(F,[s*x,0.40,deckZ(uOf(0.4))+0.02],[s*x,1.60,deckZ(uOf(1.6))+0.02],[0.022,0.018],'blk',0.1,-0.02);
        box(F,[s*x,CAR_Y,deckZ(uOf(CAR_Y))+0.055],[0.035,0.06,0.03],'steel',0.45,-0.03);
      }
    })();

    // ---- coachroof ----
    (function(){
      const N=12, yA=RF.yA, yF=RF.yF;
      for(let i=0;i<N;i++){
        const y0=yA+(yF-yA)*i/N, y1=yA+(yF-yA)*(i+1)/N;
        const h0=hxRoof(y0), h1=hxRoof(y1), z0=roofZ(y0), z1=roofZ(y1), d0=deckZ(uOf(y0)), d1=deckZ(uOf(y1));
        // top: gelcoat margin + a non-skid panel
        face(F,[[-(h0-0.06),y0,z0],[h0-0.06,y0,z0],[h1-0.06,y1,z1],[-(h1-0.06),y1,z1]],'paint',0.55,0,{lv:'lid'});
        if(y0>-0.10 && y1<2.95){
          const p0=h0-0.26, p1=h1-0.26;
          face(F,[[-p0,y0,z0+0.002],[p0,y0,z0+0.002],[p1,y1,z1+0.002],[-p1,y1,z1+0.002]],'deck',0.35,-0.01,{lv:'lid'});
        }
        // sides with a little tumblehome
        for(const s of [-1,1]){
          const q=[[s*h0,y0,d0],[s*h1,y1,d1],[s*(h1-0.06),y1,z1],[s*(h0-0.06),y0,z0]];
          face(F,s>0?q:q.slice().reverse(),'paint',-0.15,0,{cut:'cabin',cutN:[s,0,0]});
        }
      }
      // coachroof windows: two long smoked panels a side
      for(const s of [-1,1]) for(const [w0,w1] of [[0.05,1.25],[1.55,2.75]]){
        const NW=4;
        for(let i=0;i<NW;i++){
          const y0=w0+(w1-w0)*i/NW, y1=w0+(w1-w0)*(i+1)/NW;
          const zb0=deckZ(uOf(y0))+0.20, zb1=deckZ(uOf(y1))+0.20, zt0=roofZ(y0)-0.10, zt1=roofZ(y1)-0.10;
          const xw=(y,z)=>{ const t=(z-deckZ(uOf(y)))/(roofZ(y)-deckZ(uOf(y))); return hxRoof(y)-0.06*t+0.012; };
          const q=[[s*xw(y0,zb0),y0,zb0],[s*xw(y1,zb1),y1,zb1],[s*xw(y1,zt1),y1,zt1],[s*xw(y0,zt0),y0,zt0]];
          face(F,s>0?q:q.slice().reverse(),'glas',0.6,0.02,{cut:'cabin',cutN:[s,0,0]});
        }
      }
      // raked front with a forward window
      const hF=hxRoof(yF)-0.06, zF=roofZ(yF), yN=RF.yNose, zN=deckZ(uOf(yN)), hN=hxRoof(yN)*0.92;
      face(F,[[-hF,yF,zF],[hF,yF,zF],[hN,yN,zN],[-hN,yN,zN]],'paint',0.5,0,{cut:'cabin',cutN:[0,1,0]});
      face(F,[[-0.34,yF+0.06,zF-0.04],[0.34,yF+0.06,zF-0.04],[0.30,yN-0.10,zN+0.11],[-0.30,yN-0.10,zN+0.11]],'glas',0.7,0.02,{cut:'cabin',cutN:[0,1,0]});
      for(const s of [-1,1]){   // front side cheeks
        const q=[[s*hF,yF,zF],[s*hN,yN,zN],[s*hxRoof(yN),yN,zN],[s*hxRoof(yF),yF,deckZ(uOf(yF))]];
        face(F,s>0?q:q.slice().reverse(),'paint',-0.1,0,{cut:'cabin',cutN:[s,0.4,0]});
      }
      // deck hatches on the roof (smoked lids in steel frames)
      for(const [hy,hs] of [[1.55,0.28],[2.55,0.24]]){
        box(F,[0,hy,roofZ(hy)+0.03],[hs+0.03,hs+0.03,0.03],'steel',0.3,-0.02,null,{lv:'lid'});
        face(F,[[-hs,hy-hs,roofZ(hy)+0.061],[hs,hy-hs,roofZ(hy)+0.061],[hs,hy+hs,roofZ(hy)+0.061],[-hs,hy+hs,roofZ(hy)+0.061]],'glas',0.9,-0.03,{lv:'lid'});
      }
      // cabin-top winches, clutch banks, halyard tails from the mast foot
      for(const s of [-1,1]){
        drum(F,[s*WINCH.cabinX,WINCH.cabinY,roofZ(WINCH.cabinY)]);
        box(F,[s*WINCH.cabinX,WINCH.clutchY,roofZ(WINCH.clutchY)+0.03],[0.11,0.05,0.03],'blk',0.2,-0.02,null,{lv:'lid'});
        for(let k=0;k<3;k++) box(F,[s*(WINCH.cabinX-0.07+0.07*k),WINCH.clutchY+0.02,roofZ(WINCH.clutchY)+0.085],[0.012,0.02,0.025],'steel',0.6,-0.03,null,{lv:'lid'});
        const tails=[[s*0.14,MAST.y-0.12,roofZ(MAST.y-0.12)+0.03],[s*0.40,0.55,roofZ(0.55)+0.03],[s*WINCH.cabinX,WINCH.clutchY+0.06,roofZ(WINCH.clutchY)+0.03],[s*WINCH.cabinX,WINCH.cabinY+0.10,roofZ(WINCH.cabinY)+0.03]];
        rope(F,tails,0.024,'rope',0.2,0,{lv:'lid'});
      }
      // mast collar + foot
      box(F,[0,MAST.y,MAST.footZ+0.035],[0.14,0.16,0.035],'steel',0.4,-0.02,null,{lv:'lid'});
    })();

    // ---- mast, spreaders, standing rigging, pulpits, stanchions, lifelines ----
    (function(){
      const RIGX={lv:'rig'};
      bar(F,mastAt(MAST.footZ+0.07),mastAt(MAST.headZ),[0.085,0.058],'mast',0.25,-0.10,RIGX);
      box(F,[0,mastY(MAST.headZ)+0.0,MAST.headZ+0.03],[0.07,0.11,0.03],'blk',0.2,-0.02,null,RIGX);   // masthead crane
      const WR=0.026;
      // spreaders (swept aft) and cap shrouds down through their tips
      const caps=[[-1],[1]].map(([s])=>[[s*CHAIN.x,CHAIN.yCap,deckZ(uOf(CHAIN.yCap))+0.05]]);
      SPREADERS.forEach((sp,k)=>{
        const root=mastAt(sp.z), sw=sp.sweep*DEG;
        for(const s of [-1,1]){
          const tip=[s*sp.len, root[1]-Math.sin(sw)*sp.len*0.55, sp.z+0.04];
          bar(F,[s*0.06,root[1],sp.z],tip,[0.028,0.036],'mast',0.35,-0.10,RIGX);
          caps[s>0?1:0].push(tip);
        }
      });
      const mh=mastAt(MAST.headZ-0.12);
      for(const s of [-1,1]){
        const pts=caps[s>0?1:0].concat([[s*0.07,mh[1],mh[2]]]);
        rope(F,pts,WR,'wire',0.45,0,RIGX);
        // lowers: forward + aft, deck to the first spreader root
        const r1=mastAt(SPREADERS[0].z);
        rope(F,[[s*CHAIN.x,CHAIN.yFwd,deckZ(uOf(CHAIN.yFwd))+0.05],[s*0.07,r1[1]+0.02,r1[2]-0.05]],WR,'wire',0.35,0,RIGX);
        rope(F,[[s*CHAIN.x,CHAIN.yAft,deckZ(uOf(CHAIN.yAft))+0.05],[s*0.07,r1[1]-0.02,r1[2]-0.05]],WR,'wire',0.35,0,RIGX);
        // split backstay legs to the quarters
        rope(F,[[s*1.05,-4.45,deckZ(uOf(-4.45))+0.05],[s*0.03,mastY(MAST.headZ)-0.10,MAST.headZ-0.05]],WR,'wire',0.4,0,RIGX);
        // chainplates
        box(F,[s*CHAIN.x,CHAIN.yCap,deckZ(uOf(CHAIN.yCap))+0.03],[0.03,0.36,0.03],'steel',0.55,-0.02,null,RIGX);
      }
      rope(F,[FORESTAY.foot,FORESTAY.head],WR,'wire',0.45,0,RIGX);
      // stanchions + twin lifelines, pulpit, pushpits
      const US=[0.075,0.19,0.32,0.46,0.60,0.74,0.86], SH=0.62;
      const footX=(u)=>halfAtZ(u,deckZ(u),1)-0.07;
      for(const s of [-1,1]){
        const tops=[], mids=[];
        for(const u of US){
          const x=footX(u), y=yOf(u)+rakeAt(u,1), z=deckZ(u);
          bar(F,[s*x,y,z],[s*(x-0.01),y,z+SH],0.020,'steel',0.25,-0.10,RIGX);
          tops.push([s*(x-0.01),y,z+SH]); mids.push([s*(x-0.005),y,z+SH*0.52]);
        }
        // pushpit: quarter frame from the last stanchion aft round the quarter
        const yq=-4.55, xq=footX(uOf(yq))-0.02, zq=deckZ(uOf(yq));
        bar(F,[s*xq,yq,zq],[s*xq,yq,zq+SH],0.020,'steel',0.25,-0.10,RIGX);
        bar(F,[s*0.50,yq-0.05,zq],[s*0.50,yq-0.05,zq+SH],0.020,'steel',0.25,-0.10,RIGX);
        const pushTop=[[s*xq,yq,zq+SH],[s*0.50,yq-0.05,zq+SH]];
        rope(F,[tops[0]].concat(pushTop),0.020,'steel',0.6,0,RIGX);
        rope(F,tops,0.017,'wire',0.6,0,RIGX);
        rope(F,mids,0.015,'wire',0.45,0,RIGX);
        // pulpit: from the forward stanchion to a hoop round the stem head
        const yp=4.22, xp=0.30, zp=deckZ(uOf(yp));
        bar(F,[s*xp,yp,zp],[s*xp,yp,zp+SH],0.020,'steel',0.25,-0.10,RIGX);
        rope(F,[tops[tops.length-1],[s*xp,yp,zp+SH],[s*0.16,4.56,zp+SH-0.02],[0,4.66,zp+SH-0.03]],0.020,'steel',0.6,0,RIGX);
        rope(F,[mids[mids.length-1],[s*xp,yp,zp+SH*0.52]],0.015,'wire',0.45,0,RIGX);
      }
      // furling line: drum -> along the port deck -> coaming cleat
      const fl=[[-0.06,FORESTAY.foot[1]-0.02,FORESTAY.foot[2]-0.02]];
      for(const y of [3.6,2.4,1.2,0.0]) fl.push([-(halfAtZ(uOf(y),deckZ(uOf(y)),1)-0.16), y, deckZ(uOf(y))+0.035]);
      fl.push([-(xCoam(-1.0)-0.05),-1.0,CK.coamZ+0.04]);
      rope(F,fl,0.021,'rope',0.15,0,RIGX);
    })();

    // ---- fin keel + spade rudder (underbody, optional) ----
    (function(){
      const U={lv:'under'};
      const sec=(yc,c,z,t)=>[[yc+c*0.5,z,0],[yc+c*0.1,z,t],[yc-c*0.35,z,t*0.85],[yc-c*0.5,z,0],[yc-c*0.35,z,-t*0.85],[yc+c*0.1,z,-t]];
      const A=sec(-0.05,1.60,0.02,0.09), B=sec(-0.15,1.05,-1.20,0.065);
      const P=(q)=>[q[2],q[0],q[1]];   // [y,z,x] -> [x,y,z]
      for(let k=0;k<6;k++){ const k2=(k+1)%6; face(F,[P(A[k]),P(A[k2]),P(B[k2]),P(B[k])],'bottom',-0.3,0,U); }
      // bulb
      const rings=[[-0.95,0.06],[-0.65,0.14],[-0.25,0.17],[0.15,0.14],[0.42,0.06]];
      for(let i=0;i+1<rings.length;i++){
        const [y0,r0]=rings[i], [y1,r1]=rings[i+1], n=8, ring=(y,r)=>{ const o=[]; for(let k=0;k<n;k++){ const a=2*Math.PI*k/n; o.push([r*Math.cos(a)*1.1, y, -1.28+r*Math.sin(a)*0.8]); } return o; };
        const R0=ring(y0,r0), R1=ring(y1,r1);
        for(let k=0;k<n;k++){ const k2=(k+1)%n; face(F,[R0[k],R1[k],R1[k2],R0[k2]],'bottom',-0.5,0,U); }
      }
    })();

    // ---- cabin interior (view:'cabin') ----
    (function(){
      const C={lv:'cabin'}, CC={lv:'cabin',cut:'cabin'};
      const half=(y,z)=>halfAtZ(uOf(y),z,1)-0.05;
      // teak sole from the foot of the steps to the forward bulkhead
      const NS3=8, y0s=-0.05, y1s=RF.yF;
      for(let i=0;i<NS3;i++){
        const y0=y0s+(y1s-y0s)*i/NS3, y1=y0s+(y1s-y0s)*(i+1)/NS3;
        const w0=Math.min(0.55,half(y0,CAB_SOLE)), w1=Math.min(0.55,half(y1,CAB_SOLE));
        face(F,[[-w0,y0,CAB_SOLE],[w0,y0,CAB_SOLE],[w1,y1,CAB_SOLE],[-w1,y1,CAB_SOLE]],'teak',(i%2?0.35:0.05),0,C);
      }
      // companionway steps: three teak treads down from the sill
      const treads=[[RF.yA,-0.33,1.06],[-0.33,-0.11,0.82],[-0.11,0.11,0.58]];
      for(const [ya,yb,zt] of treads){
        box(F,[0,(ya+yb)/2,(zt+CAB_SOLE)/2],[0.30,(yb-ya)/2,(zt-CAB_SOLE)/2],'teak',0.1,0,null,C,['teak','teak','teak','teak','teak','teak']);
        face(F,[[-0.30,yb,zt+0.004],[0.30,yb,zt+0.004],[0.30,yb+0.03,zt+0.004],[-0.30,yb+0.03,zt+0.004]],'steel',0.6,-0.02,C);
      }
      // galley, port aft: counter, sink, stove (INTERACT stove)
      box(F,[-0.78,0.68,(CAB_SOLE+1.25)/2],[0.32,0.52,(1.25-CAB_SOLE)/2],'teak',-0.1,0,null,C);
      box(F,[-0.78,0.30,1.245],[0.17,0.14,0.012],'steel',0.5,-0.02,null,C);
      box(F,[-0.76,0.92,1.28],[0.20,0.20,0.03],'blk',0.2,-0.02,null,C);
      for(const [bx,by] of [[-0.86,0.84],[-0.66,0.84],[-0.86,1.02],[-0.66,1.02]]) prism(F,[bx,by,1.31],0.045,0.012,8,'steel',0.7,-0.03,C);
      // head compartment, starboard aft: a wall box with a teak door (culled when it faces the camera)
      const HD={x0:0.45,x1:1.08,y0:0.15,y1:1.20,z1:2.05};
      face(F,[[HD.x0,HD.y0,CAB_SOLE],[HD.x0,HD.y1,CAB_SOLE],[HD.x0,HD.y1,HD.z1],[HD.x0,HD.y0,HD.z1]],'cream',-0.15,0,Object.assign({cutN:[-1,0,0]},CC));
      face(F,[[HD.x0,HD.y0+0.10,CAB_SOLE+0.01],[HD.x0-0.01,HD.y0+0.10,CAB_SOLE+0.01],[HD.x0-0.01,HD.y0+0.10+0.62,1.85],[HD.x0,HD.y0+0.10+0.62,1.85]],'teak',0.2,0.02,Object.assign({cutN:[-1,0,0]},CC));
      face(F,[[HD.x0,HD.y0,CAB_SOLE],[HD.x0,HD.y0,HD.z1],[HD.x1,HD.y0,HD.z1],[HD.x1,HD.y0,CAB_SOLE]],'cream',-0.15,0,Object.assign({cutN:[0,-1,0]},CC));
      face(F,[[HD.x1,HD.y1,CAB_SOLE],[HD.x0,HD.y1,CAB_SOLE],[HD.x0,HD.y1,HD.z1],[HD.x1,HD.y1,HD.z1]],'cream',-0.15,0,Object.assign({cutN:[0,1,0]},CC));
      box(F,[0.80,0.75,CAB_SOLE+0.20],[0.18,0.22,0.20],'paint',0.3,0,null,C);   // the toilet
      // settees with cushions, backrests against the hull, lockers above (INTERACT locker = starboard)
      for(const s of [-1,1]){
        const y0=1.30, y1=RF.yF-0.05, N2=4;
        for(let i=0;i<N2;i++){
          const ya=y0+(y1-y0)*i/N2, yb=y0+(y1-y0)*(i+1)/N2;
          const oa=half(ya,CAB_SOLE+0.42), ob=half(yb,CAB_SOLE+0.42);
          const base=[[s*0.55,ya,CAB_SOLE+0.40],[s*oa,ya,CAB_SOLE+0.40],[s*ob,yb,CAB_SOLE+0.40],[s*0.55,yb,CAB_SOLE+0.40]];
          face(F,s>0?base:base.slice().reverse(),'uph',0.5,0.01,C);
          const fr=[[s*0.55,ya,CAB_SOLE],[s*0.55,yb,CAB_SOLE],[s*0.55,yb,CAB_SOLE+0.40],[s*0.55,ya,CAB_SOLE+0.40]];
          face(F,s>0?fr.slice().reverse():fr,'teak',-0.2,0,C);
          const oa2=half(ya,CAB_SOLE+0.85)-0.06, ob2=half(yb,CAB_SOLE+0.85)-0.06;
          const bk=[[s*oa,ya,CAB_SOLE+0.40],[s*ob,yb,CAB_SOLE+0.40],[s*ob2,yb,CAB_SOLE+0.85],[s*oa2,ya,CAB_SOLE+0.85]];
          face(F,s>0?bk.slice().reverse():bk,'uph',0.15,0.01,C);
          // lockers above the backrest, hugging the hull
          const la=half(ya,1.45)-0.02, lb=half(yb,1.45)-0.02;
          const lk=[[s*(la-0.30),ya,1.30],[s*(la-0.30),yb,1.30],[s*(lb-0.30),yb,1.72],[s*(la-0.30),ya,1.72]];
          face(F,s>0?lk.slice().reverse():lk,'teak',0.1,0,C);
          const lt=[[s*(la-0.30),ya,1.72],[s*la,ya,1.72],[s*lb,yb,1.72],[s*(lb-0.30),yb,1.72]];
          face(F,s>0?lt:lt.slice().reverse(),'teak',0.45,0,C);
        }
      }
      // saloon table on a steel post
      box(F,[0,2.15,1.04],[0.30,0.55,0.02],'teak',0.5,-0.01,null,C);
      bar(F,[0,2.15,CAB_SOLE],[0,2.15,1.02],0.03,'steel',0.4,-0.1,C);
      // compression post under the mast
      bar(F,[0,MAST.y,CAB_SOLE],[0,MAST.y,roofZ(MAST.y)-0.06],0.04,'steel',0.35,-0.1,C);
      // forward bulkhead with the V-berth door opening
      const yb2=RF.yF, hb=half(yb2,1.2)+0.05, zt=roofZ(yb2)-0.04;
      const bw=(x0,x1,z0,z1)=>face(F,[[x0,yb2,z0],[x1,yb2,z0],[x1,yb2,z1],[x0,yb2,z1]],'cream',-0.1,0,Object.assign({cutN:[0,-1,0]},CC));
      bw(-hb,-0.30,CAB_SOLE,zt); bw(0.30,hb,CAB_SOLE,zt); bw(-0.30,0.30,1.85,zt);
      // V-berth: mattress platform tapering with the hull (INTERACT bunk)
      const NB=5, y0b=yb2+0.02, y1b=4.42;
      for(let i=0;i<NB;i++){
        const ya=y0b+(y1b-y0b)*i/NB, yb=y0b+(y1b-y0b)*(i+1)/NB, za=CAB_SOLE+0.50;
        const wa=Math.max(0.10,half(ya,za)-0.04), wb=Math.max(0.10,half(yb,za)-0.04);
        face(F,[[-wa,ya,za],[wa,ya,za],[wb,yb,za],[-wb,yb,za]],'uph',(i%2?0.55:0.4),0,C);
      }
      face(F,[[-half(y0b,CAB_SOLE+0.5)+0.04,y0b,CAB_SOLE],[half(y0b,CAB_SOLE+0.5)-0.04,y0b,CAB_SOLE],[half(y0b,CAB_SOLE+0.5)-0.04,y0b,CAB_SOLE+0.50],[-half(y0b,CAB_SOLE+0.5)+0.04,y0b,CAB_SOLE+0.50]].reverse(),'teak',-0.3,0,C);
      box(F,[0.28,3.55,CAB_SOLE+0.55],[0.20,0.26,0.05],'cream',0.8,0.01,null,C);   // pillows
      box(F,[-0.28,3.55,CAB_SOLE+0.55],[0.20,0.26,0.05],'cream',0.8,0.01,null,C);
    })();
  })();

  // ---- pose: apparent wind + sheets -> boom, jib, fill, luff, heel --------------------------------
  function sailPose(o){
    o=o||{};
    let awa=(o.awa==null?45:+o.awa); awa=((awa+180)%360+360)%360-180;
    const aws=Math.max(0,o.aws==null?12:+o.aws);
    const main=clamp01(o.main==null?0.7:+o.main), jib=clamp01(o.jib==null?0.7:+o.jib);
    const a=Math.abs(awa), side = awa>=0 ? -1 : 1;          // wind over stbd -> sails to port
    const irons = a<25 && aws>0.5;
    const mainLimit=4+82*(1-main), jibLimit=9+76*(1-jib);
    const phase=2*Math.PI*((o.frame||0)%8)/8;
    let boom = Math.min(mainLimit, Math.max(0, a-6));
    let jibA = Math.min(jibLimit, Math.max(0, a-6));
    if(irons){ boom=3*Math.sin(phase); jibA=3*Math.sin(phase+1); }
    if(aws<0.5){ boom=Math.min(boom,8); jibA=Math.min(jibA,10); }          // becalmed: the boom hangs near the centreline
    const hoist=clamp01(o.hoist==null?1:+o.hoist);
    if(hoist<=0.02) boom=Math.min(boom,2);                                 // main down: sheeted home, boom parked
    const aoaM=a-boom, aoaJ=a-jibA;
    const wind=clamp01(aws/6);
    const fill=(aoa)=>clamp01((aoa-5)/13)*wind;
    let fillM=fill(aoaM), fillJ=fill(aoaJ);
    if(a>150) fillJ*=0.4;                                 // blanketed by the main dead downwind
    const flog=(aoa)=>aws<0.5?0:(irons?1:clamp01((8-aoa)/8));
    const flogM=flog(aoaM), flogJ=flog(aoaJ);
    const stallM=clamp01((aoaM-40)/40), stallJ=clamp01((aoaJ-40)/40);
    const k = a<100 ? 1 : lerp(1,0.35,(a-100)/80);
    const heelDeg = Math.min(24, 0.067*aws*aws*(0.25+0.75*fillM)*k)*(irons?0.15:1);
    const mode = aws<0.5 ? 'becalmed' : irons ? 'in irons' : (flogM>0.01||flogJ>0.01) ? 'luffing' : (stallM>0.3) ? 'stalled' : 'drawing';
    return { awa, aws, side, boom:Math.abs(boom), boomSigned:side*Math.abs(boom), jibAngle:Math.abs(jibA), jibSigned:side*Math.abs(jibA),
             aoaMain:aoaM, aoaJib:aoaJ, fillMain:fillM, fillJib:fillJ, flogMain:flogM, flogJib:flogJ, stallMain:stallM, stallJib:stallJ,
             irons, heel: side*heelDeg, heelDeg, phase, mode, mainSheet:main, jibSheet:jib };
  }
  function poseOf(o){
    o=o||{};
    const sp=sailPose(o);
    const rk = o.rock ? rockMotion(o.frame||0) : {roll:0,pitch:0,heave:0};
    const heel = o.heel===false ? 0 : (o.heelDeg!=null ? +o.heelDeg : sp.heel);
    return { roll:(o.roll||0)+rk.roll+heel, pitch:(o.pitch||0)+rk.pitch, heave:(o.heave||0)+rk.heave, sail:sp };
  }

  // ---- dynamic faces: boom, sails, stack pack, ropes, door, hatch, wheel, rudder, winch handle ------
  function sailMesh(out,P){
    const NT=P.NT||8, NS=P.NS||6;
    const pt=(s,t)=>{
      const Lp=P.luff(t), c=P.chord(t), u=P.udir(t), n=P.lee(t);
      const camb=P.depth(t)*Math.sin(Math.PI*Math.pow(s,0.72));
      const fl=P.flog>0 ? P.flog*P.amp*Math.sin(Math.PI*s)*(P.irons?1:Math.pow(1-s,0.7))*Math.sin(2*Math.PI*(2.1*s+0.7*t)+P.phase) : 0;
      const off=camb+fl;
      return [ Lp[0]+u[0]*c*s+n[0]*off, Lp[1]+u[1]*c*s+n[1]*off, Lp[2]+(P.dz?P.dz(t)*s:0) ];
    };
    const rows=[]; for(let j=0;j<=NT;j++){ const t=j/NT, r=[]; for(let i=0;i<=NS;i++) r.push(pt(i/NS,t)); rows.push(r); }
    for(let j=0;j<NT;j++) for(let i=0;i<NS;i++){
      const a=rows[j][i], b=rows[j][i+1], c=rows[j+1][i+1], d=rows[j+1][i];
      out.push({ v:[a,b,c,d], mat:P.mat, b:P.b||0, db:P.db||0, two:true, lv:'rig' });
    }
    for(const tb of (P.battens||[])){ const t0=tb-0.007, t1=tb+0.007;
      for(let i=0;i<NS;i++){ const s0=0.04+0.94*i/NS, s1=0.04+0.94*(i+1)/NS;
        out.push({ v:[pt(s0,t0),pt(s1,t0),pt(s1,t1),pt(s0,t1)], mat:'batten', b:0.2, db:0.03, two:true, lv:'rig' }); } }
  }
  function dynamicFaces(o, pose, view){
    const D=[], sp=pose.sail, side=sp.side, RX={lv:'rig'};
    const showSails = o.sails!=null ? !!o.sails : (view!=='cabin');
    const h=clamp01(o.hoist==null?1:+o.hoist), furl=clamp01(o.furl==null?0:+o.furl);
    const cover = !!o.cover && h<0.02;
    // ---- boom + stack pack ----
    const beta=sp.boom*DEG, ub=[side*Math.sin(beta), -Math.cos(beta)];
    const G=GOOSE, E=[G[0]+ub[0]*BOOM_L, G[1]+ub[1]*BOOM_L, G[2]];
    bar(D,G,E,[0.055,0.085],'mast',0.3,-0.10,RX);
    box(D,[G[0],G[1],G[2]],[0.10,0.12,0.11],'blk',0.2,-0.02,null,RX);   // gooseneck fitting
    const xfB=(p)=>rotZabout(p,G,side*beta);
    const bh=0.06+0.14*(1-h), bw=0.12+0.06*(1-h);
    if(showSails){
      const topMat = cover ? 'canvas' : (h<0.98 ? 'sail' : 'canvas');
      box(D,[G[0],G[1]-BOOM_L*0.52,G[2]+0.085+bh],[bw,BOOM_L*0.46,bh],'canvas',0.25,-0.01,xfB,RX,[topMat,'canvas','canvas','canvas','canvas','canvas']);
      // lazy jacks from the mast to the bag
      const lj=mastAt(6.3);
      for(const s of [-1,1]) for(const d of [1.0,2.35]){
        const p=xfB([G[0]+s*bw*0.9,G[1]-d,G[2]+0.085+2*bh]);
        rope(D,[[s*0.07,lj[1],lj[2]],p],0.016,'rope',0.3,0,RX);
      }
    }
    // topping lift: masthead -> boom end
    rope(D,[[0,mastY(MAST.headZ)-0.08,MAST.headZ-0.10],[E[0],E[1],E[2]+0.06]],0.015,'wire',0.35,0,RX);
    // ---- mainsail ----
    if(showSails && h>0.02){
      const P=MAIN.P*h;
      const twist=(5+9*(1-sp.mainSheet))*DEG;
      const chord=(t)=>MAIN.E*((1-t)*0.93+0.07)+MAIN.roach*Math.sin(Math.PI*Math.pow(t,0.85));
      const udir=(t)=>{ const a=beta+twist*t; return [side*Math.sin(a),-Math.cos(a)]; };
      sailMesh(D,{ luff:(t)=>{ const z=G[2]+0.06+P*t; return [0,mastY(z)-0.10,z]; },
        chord, udir, lee:(t)=>{ const u=udir(t); return [-u[1]*side,u[0]*side]; },
        depth:(t)=>sp.fillMain*(0.085+0.05*(1-sp.mainSheet))*chord(t)*(1-0.25*t)*(1-0.4*sp.stallMain),
        flog:sp.flogMain, amp:0.22, phase:sp.phase, irons:sp.irons, mat:'sail', NT:8, NS:6, battens:MAIN.battens });
    }
    // mainsheet: boom end -> sole block -> pedestal cleat
    const slackM = 0.05+0.22*(1-sp.fillMain);
    rope(D,[[E[0],E[1]+0.08*Math.cos(beta),E[2]-0.09],SHEET_BLOCK],0.028,'rope',0.35,slackM*0.4,RX);
    rope(D,[SHEET_BLOCK,[0,PED.y-PED.hy-0.04,SOLE+0.40]],0.024,'rope',0.25,0.04,RX);
    // ---- jib ----
    const Tk=JIB.tack, Hd=JIB.head;
    let clew=null;
    if(showSails && furl<0.97){
      const g=sp.jibAngle*DEG, twistJ=(6+10*(1-sp.jibSheet))*DEG, LP=JIB.LP*(1-furl);
      const uj0=[side*Math.sin(g),-Math.cos(g)];
      clew=[Tk[0]+uj0[0]*LP*0.97, Tk[1]+uj0[1]*LP*0.97, Tk[2]+JIB.clewUp+0.5*furl];
      const udir=(t)=>{ const a=g+twistJ*t; return [side*Math.sin(a),-Math.cos(a)]; };
      const chord=(t)=>LP*(1-t)*0.97;
      sailMesh(D,{ luff:(t)=>lerp3(Tk,Hd,t), chord, udir, lee:(t)=>{ const u=udir(t); return [-u[1]*side,u[0]*side]; },
        depth:(t)=>sp.fillJib*0.11*chord(t)*(1-0.2*t)*(1-0.4*sp.stallJib), dz:(t)=>(clew[2]-Tk[2])*(1-t),
        flog:sp.flogJib, amp:0.18, phase:sp.phase+0.9, irons:sp.irons, mat:'sail', NT:7, NS:5 });
    }
    // the furled roll (or the bare luff foil) along the forestay
    if(showSails){
      const rr = furl>0.15 ? 0.035+0.07*furl : 0.03;
      bar(D,[Tk[0],Tk[1],Tk[2]-0.05],[Hd[0],Hd[1],Hd[2]+0.10],rr,furl>0.15?'canvas':'blk',0.2,-0.05,RX);
      if(!clew) clew=[0,Tk[1]-0.05,Tk[2]+0.65];
    }
    // jib sheets: working sheet taut through the leeward car to its primary, lazy sheet slack across
    if(clew){
      const cxr=carX(), zc=deckZ(uOf(CAR_Y))+0.09;
      for(const s of [-1,1]){
        const car=[s*cxr,CAR_Y,zc], mid=[s*(xCoam(-0.6)-0.02),-0.6,deckZ(uOf(-0.6))+0.06], wn=[s*WINCH.primaryX(),WINCH.primaryY,CK.coamZ+0.16];
        const working = (s===side) && furl<0.97;
        const sag = working ? 0 : (furl>=0.97 ? 0.30 : 0.42);
        rope(D,[clew,car],0.028,'rope',working?0.35:0.2,sag,RX);
        rope(D,[car,mid,wn],0.028,'rope',0.3,working?0:0.05,RX);
      }
    }
    // halyard on the mast (the standing part, only when hoisted enough to matter) — a thin line up the aft face
    // winch handle when grinding
    if(o.grind){
      const c = o.grind==='main' ? [WINCH.cabinX,WINCH.cabinY,roofZ(WINCH.cabinY)+0.20] : [(-side)*WINCH.primaryX(),WINCH.primaryY,CK.coamZ+0.20];
      const a=((o.frame||0)%8)*Math.PI/4, tip=[c[0]+Math.cos(a)*0.24,c[1]+Math.sin(a)*0.24,c[2]+0.02];
      bar(D,c,tip,0.014,'blk',0.3,-0.06,RX); bar(D,tip,[tip[0],tip[1],tip[2]+0.08],0.02,'steel',0.5,-0.06,RX);
    }
    // ---- companionway door + hatch ----
    const dt=clamp01(o.doorOpen==null?0:+o.doorOpen), th=dt*DOOR.swing*DEG;
    (function(){
      const hx=DOOR.hingeX, y=DOOR.y-0.02, d=[Math.cos(th),-Math.sin(th)], n=[d[1],-d[0]], w=DOOR.leaf.w, tk=DOOR.leaf.th;
      const P=(s,k,z)=>[hx+d[0]*s*w+n[0]*k*tk, y+d[1]*s*w+n[1]*k*tk, z];
      const z0=DOOR.z0, z1=DOOR.z1;
      face(D,[P(0,1,z0),P(1,1,z0),P(1,1,z1),P(0,1,z1)],'paint',0.35,0.01);        // outer (aft) face
      face(D,[P(1,0,z0),P(0,0,z0),P(0,0,z1),P(1,0,z1)],'teak',0.2,0.01);           // inner face
      face(D,[P(0,0,z1),P(1,0,z1),P(1,1,z1),P(0,1,z1)],'paint',0.6,0.01);          // top edge
      face(D,[P(1,0,z0),P(1,1,z0),P(1,1,z1),P(1,0,z1)],'paint',0.1,0.01);          // free edge
      face(D,[P(0.15,1.02,z0+0.25),P(0.85,1.02,z0+0.25),P(0.85,1.02,z1-0.10),P(0.15,1.02,z1-0.10)],'glas',0.7,0.02);   // window
      box(D,[hx,y,(z0+z1)/2],[0.02,0.02,(z1-z0)/2],'steel',0.5,-0.02);            // hinge post
    })();
    const ht=clamp01(o.hatchOpen==null?dt:+o.hatchOpen);
    if(view!=='cabin'){
      const yc=(HATCH.y0+HATCH.y1)/2+HATCH.travel*ht, zc=roofZ(yc)+0.045;
      if(ht>0.05){
        const A=HATCH.aperture;
        face(D,[[A.x0,A.y0,roofZ(A.y0)-0.012],[A.x1,A.y0,roofZ(A.y0)-0.012],[A.x1,A.y1,roofZ(A.y1)-0.012],[A.x0,A.y1,roofZ(A.y1)-0.012]],'blk',-0.6,0.01);
      }
      box(D,[0,yc,zc],[HATCH.x1,(HATCH.y1-HATCH.y0)/2,0.03],'paint',0.5,-0.01);
      face(D,[[-0.30,yc-0.28,zc+0.031],[0.30,yc-0.28,zc+0.031],[0.30,yc+0.28,zc+0.031],[-0.30,yc+0.28,zc+0.031]],'glas',0.9,-0.03);
    }
    // ---- wheel ----
    if(o.wheel!==false) D.push.apply(D, wheelFaces(wheelDeg(o)));
    // ---- rudder (underbody) ----
    if(o.underbody){
      const st=Math.max(-1,Math.min(1,o.steer||0))*35*DEG, y0=-3.80, zr=0.30, zt=-0.95;
      const q=(y,z)=>rotZabout([0,y,z],[0,y0,0],-st);
      const c=(z)=>lerp(0.44,0.30,(zr-z)/(zr-zt));
      const nz=5; for(let k=0;k<nz;k++){ const za=lerp(zr,zt,k/nz), zb=lerp(zr,zt,(k+1)/nz);
        for(const s of [-1,1]){
          const t=0.03; const A=q(y0,za),B=q(y0-c(za),za),C2=q(y0-c(zb),zb),Dd=q(y0,zb);
          const off=(p)=>[p[0]+s*t,p[1],p[2]];
          const f=[off(A),off(B),off(C2),off(Dd)]; face(D,s>0?f:f.slice().reverse(),'bottom',-0.3,0,{lv:'under'});
        } }
    }
    return D;
  }

  // ---- iso wheel layer (skiff precedent): real geometry at the pedestal, tag 1 for the masked bake ----
  function wheelFaces(deg){
    const G=WHEEL_GEO, r=G.rake*DEG, out=[];
    const P0=[WHEEL_HUB.x, WHEEL_HUB.y, WHEEL_HUB.z];
    const n=[0,-Math.cos(r),Math.sin(r)];
    const e1=[1,0,0], e2=[0,Math.sin(r),Math.cos(r)];
    const at=(rho,th,off)=>[ P0[0]+rho*(Math.cos(th)*e1[0]+Math.sin(th)*e2[0])+n[0]*off,
                             P0[1]+rho*(Math.cos(th)*e1[1]+Math.sin(th)*e2[1])+n[1]*off,
                             P0[2]+rho*(Math.cos(th)*e1[2]+Math.sin(th)*e2[2])+n[2]*off ];
    const push=(v,mat,b,db)=>out.push({v,mat,b:b||0,db:db==null?0.30:db,tag:1});
    const th0=(deg||0)*DEG + Math.PI/2;
    for(let k=0;k<G.seg;k++){
      const a0=th0+2*Math.PI*k/G.seg, a1=th0+2*Math.PI*(k+1)/G.seg;
      push([at(G.rimIn,a0,0.016),at(G.rad,a0,0.016),at(G.rad,a1,0.016),at(G.rimIn,a1,0.016)],'blk',0.55);
      push([at(G.rad,a0,0.016),at(G.rad,a0,-0.012),at(G.rad,a1,-0.012),at(G.rad,a1,0.016)],'blk',-0.35);
    }
    for(let k=0;k<G.spokes;k++){
      const a=th0+2*Math.PI*k/G.spokes, w=0.014;
      const ta=a+Math.PI/2, wx=w*(Math.cos(ta)*e1[0]+Math.sin(ta)*e2[0]), wy=w*(Math.cos(ta)*e1[1]+Math.sin(ta)*e2[1]), wz=w*(Math.cos(ta)*e1[2]+Math.sin(ta)*e2[2]);
      const A=at(G.hubR*0.8,a,0.012), B=at(G.rimIn+0.004,a,0.012);
      const off=(p,s)=>[p[0]+wx*s,p[1]+wy*s,p[2]+wz*s];
      push([off(A,1),off(B,1),off(B,-1),off(A,-1)], k===0?'stripe':'steel', k===0?0.9:0.55);
    }
    for(let k=0;k<8;k++){ const a0=th0+2*Math.PI*k/8, a1=th0+2*Math.PI*(k+1)/8; push([P0,at(G.hubR,a0,0.020),at(G.hubR,a1,0.020),P0],'steel',0.8); }
    // pedestal guard: a low hoop behind the wheel
    return out;
  }
  function wheelDeg(o){ o=o||{}; if(o.deg!=null) return o.deg; const turns=o.turns==null?WHEEL_LOCK:o.turns; return Math.max(-1,Math.min(1,o.steer||0))*turns*360; }

  // ---- rasteriser (shared recipe + level gates, two-sided cloth, section cut) ----------------------
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  const shadeOf=(n,se,ce)=>n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) };
  }
  function rotV(x,y,z,B){
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    return { xr:x1*B.ct - y2*B.stt, yr:x1*B.stt + y2*B.ct, zr:z2 };
  }
  function projVert(x,y,z,B,G){
    const gx=G?G.cx:cx, gy=G?G.cy:cy, r=rotV(x,y,z,B);
    return { xr:r.xr,yr:r.yr,zr:r.zr, sx:gx+r.xr*S, sy:gy-(r.yr*B.se+r.zr*B.ce)*S - B.heave, d:(r.yr*B.ce-r.zr*B.se) };
  }
  const facingN=(n,B)=> (n[1]*B.ce - n[2]*B.se) < 0;
  function facingHull(nH,B){ const r=rotV(nH[0],nH[1],nH[2],B); return facingN([r.xr,r.yr,r.zr],B); }
  function _paint(faces, opts, doEdge, G){
    const PW=G?G.W:W, PH=G?G.H:H;
    const B=camBasis(opts), view=opts.view||'exterior';
    const pal=palette(opts), MATS=pal.mats, RINDEX=pal.rindex;
    const zbuf=new Float32Array(PW*PH).fill(Infinity);
    const col=new Array(PW*PH).fill(null);
    const dep=new Float32Array(PW*PH);
    const tag=new Int8Array(PW*PH);
    for(const f of faces){
      if(f.lv==='cabin' && view!=='cabin') continue;
      if(f.lv==='lid' && view==='cabin') continue;
      if(f.lv==='under' && !opts.underbody) continue;
      if(view==='cabin'){
        if(f.cut && (f.cutN ? facingHull(f.cutN,B) : null)===true) continue;
        if(f.lip && !facingHull(f.cutN,B)) continue;
      } else if(f.lip) continue;
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B,G));
      let n=normal(rv[0],rv[1],rv[2]);
      if(view==='cabin' && f.cut && !f.cutN && facingN(n,B)) continue;
      let bAdj=0;
      if(f.two && !facingN(n,B)){ n=[-n[0],-n[1],-n[2]]; bAdj=-0.5; }
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0) + bAdj;
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
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
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
    const out=col.slice();
    if(opts.onlyTag){ for(let i=0;i<PW*PH;i++) if(tag[i]!==opts.onlyTag){ out[i]=null; col[i]=null; } }
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
  function allFaces(dir, opts){
    opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const pose=poseOf(opts), view=opts.view||'exterior';
    const o=Object.assign({}, opts, {dir, roll:pose.roll, pitch:pose.pitch, heave:pose.heave, view});
    return { faces:F.concat(dynamicFaces(o, pose, view)), o, pose };
  }
  function render(dir, opts){
    const r=allFaces(dir, opts);
    return _toRGBA(_paint(r.faces, r.o, true));
  }
  function renderWheel(dir, opts){
    opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const r=allFaces(dir, Object.assign({}, opts, {wheel:false}));
    return _toRGBA(_paint(r.faces.concat(wheelFaces(wheelDeg(opts))), Object.assign(r.o,{onlyTag:1}), true));
  }

  // ---- anchors -----------------------------------------------------------------------------------
  const ANCHOR_PTS = {
    helm:{x:0,y:-3.95,z:SOLE}, wheelHub:WHEEL_HUB, threshold:{x:0,y:DOOR.y,z:DOOR.z0},
    mastBase:{x:0,y:MAST.y,z:MAST.footZ}, bowRoller:{x:0,y:4.62,z:deckZ(0.975)+0.04}, swimStep:{x:0,y:-L/2-0.17,z:SOLE-0.20},
    winchPort:{x:-WINCH.primaryX(),y:WINCH.primaryY,z:CK.coamZ+0.20}, winchStbd:{x:WINCH.primaryX(),y:WINCH.primaryY,z:CK.coamZ+0.20},
    halyardWinch:{x:WINCH.cabinX,y:WINCH.cabinY,z:roofZ(WINCH.cabinY)+0.20}, mainsheetBlock:{x:SHEET_BLOCK[0],y:SHEET_BLOCK[1],z:SHEET_BLOCK[2]},
    furlerCleat:{x:-(xCoam(-1.0)-0.05),y:-1.0,z:CK.coamZ+0.04}, seatPort:{x:-0.95,y:-1.8,z:CK.seatZ}, seatStbd:{x:0.95,y:-1.8,z:CK.seatZ},
    goose:{x:GOOSE[0],y:GOOSE[1],z:GOOSE[2]}, masthead:{x:0,y:mastY(MAST.headZ),z:MAST.headZ},
  };
  const CLEAT_PTS = [
    { id:'bow_port', type:'cleat', pos:[-0.34,4.35,+(deckZ(uOf(4.35))+0.035).toFixed(3)] }, { id:'bow_star', type:'cleat', pos:[0.34,4.35,+(deckZ(uOf(4.35))+0.035).toFixed(3)] },
    { id:'mid_port', type:'cleat', pos:[-1.36,0.10,+(deckZ(uOf(0.10))+0.035).toFixed(3)] },  { id:'mid_star', type:'cleat', pos:[1.36,0.10,+(deckZ(uOf(0.10))+0.035).toFixed(3)] },
    { id:'stern_port', type:'cleat', pos:[-1.18,-4.40,+(deckZ(uOf(-4.40))+0.035).toFixed(3)] }, { id:'stern_star', type:'cleat', pos:[1.18,-4.40,+(deckZ(uOf(-4.40))+0.035).toFixed(3)] },
  ];
  function anchors(dir, opts){
    opts=Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const pose=poseOf(opts), B=camBasis(Object.assign({},opts,{roll:pose.roll,pitch:pose.pitch,heave:pose.heave}));
    const out={};
    for(const k in ANCHOR_PTS){ const P=ANCHOR_PTS[k], p=projVert(P.x,P.y,P.z,B); out[k]={x:p.sx,y:p.sy}; }
    // boom end and clew ride the pose
    const sp=pose.sail, beta=sp.boom*DEG, ub=[sp.side*Math.sin(beta),-Math.cos(beta)];
    const E=[GOOSE[0]+ub[0]*BOOM_L, GOOSE[1]+ub[1]*BOOM_L, GOOSE[2]]; const pe=projVert(E[0],E[1],E[2],B); out.boomEnd={x:pe.sx,y:pe.sy};
    out.cleats=CLEAT_PTS.map(c=>{ const p=projVert(c.pos[0],c.pos[1],c.pos[2],B); return {id:c.id,x:p.sx,y:p.sy}; });
    out.pose=pose; return out;
  }
  function doorMount(dir, opts){
    opts=Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const t=clamp01(opts.doorOpen==null?0:+opts.doorOpen), th=t*DOOR.swing*DEG;
    const pose=poseOf(opts), B=camBasis(Object.assign({},opts,{roll:pose.roll,pitch:pose.pitch,heave:pose.heave}));
    const thr=projVert(0,DOOR.y,DOOR.z0,B);
    const ex=DOOR.hingeX+Math.cos(th)*DOOR.leaf.w, ey=DOOR.y-0.02-Math.sin(th)*DOOR.leaf.w;
    const edge=projVert(ex,ey,DOOR.z0,B);
    return { threshold:{x:thr.sx,y:thr.sy}, edge:{x:edge.sx,y:edge.sy}, open:t, clear:t>=DOOR.clearAt, swingDeg:t*DOOR.swing };
  }

  // ---- published geometry + gameplay sidecar generator --------------------------------------------
  const LEVEL_IDS = { hull:0, cockpit:1, coachroof:2, foredeck:3, cabin:4, rig:5 };
  function geometry(){
    return { schema:'hidden-harbours/hull-geometry@1', hull:'sloopIsoRig', units:'m',
      frame:'+x stbd, +y bow, +z up; origin amidships, canoe-body bottom, centreline', ids:Object.assign({},LEVEL_IDS),
      levels:[
        { id:'cockpit', deck:'cockpit_sole', soleZ:SOLE, ceilingZ:null, ceiling:{ kind:'open', lid:null, partial:{ z:GOOSE[2]-0.085, y0:GOOSE[1]-BOOM_L, y1:GOOSE[1], of:'boom underside over the cockpit centreline — 1.85 m over the sole; it swings' } } },
        { id:'coachroof', deck:'coachroof', soleZ:roofZ(RF.yA), ceilingZ:null, sole:{ kind:'raked', zAft:+roofZ(RF.yA).toFixed(3), zFwd:+roofZ(RF.yF).toFixed(3) }, ceiling:{ kind:'open', lid:null } },
        { id:'foredeck', deck:'foredeck', soleZ:deckZ(uOf(RF.yNose)), ceilingZ:null, sole:{ kind:'raked', zAft:+deckZ(uOf(RF.yNose)).toFixed(3), zFwd:+deckZ(0.985).toFixed(3), follows:'sheer - 0.02' }, ceiling:{ kind:'open', lid:null } },
        { id:'cabin', deck:'cabin_sole', soleZ:CAB_SOLE, ceilingZ:+(roofZ(RF.yF)-0.05).toFixed(3),
          ceiling:{ kind:'raked', lid:'coachroof', zAft:+(roofZ(RF.yA)-0.05).toFixed(3), zFwd:+(roofZ(RF.yF)-0.05).toFixed(3), y0:RF.yA, y1:RF.yF, of:'coachroof underside; the V-berth forward lies under the foredeck (deck - 0.05)' } },
      ] };
  }
  const r3=(v)=>+(+v).toFixed(3);
  function gameplayGeometry(){
    const soleHalf=(y)=>halfAtZ(uOf(y),SOLE,1)-0.06;
    // cockpit sole: walkway between the benches, widening into the helm area, to the transom
    const poly=[]; const N=6;
    poly.push([-CK.xi,CK.yA]); poly.push([-CK.xi,CK.yB]);
    for(let i=0;i<=N;i++){ const y=CK.yB+(CK.yT-CK.yB)*i/N; poly.push([-r3(soleHalf(y)),r3(y)]); }
    for(let i=N;i>=0;i--){ const y=CK.yB+(CK.yT-CK.yB)*i/N; poly.push([r3(soleHalf(y)),r3(y)]); }
    poly.push([CK.xi,CK.yB]); poly.push([CK.xi,CK.yA]);
    const cockpit={ id:'cockpit_sole', z:SOLE, winding:'ccw_from_above', polygon:poly.map(p=>[r3(p[0]),r3(p[1])]),
      note:'Teak-planked self-draining sole: a 1.24 m walkway between the benches from the companionway bulkhead (y='+CK.yA+') to the bench ends at y='+CK.yB+', then the full-width helm area to the open transom at y='+r3(-L/2)+'. The walk-through in the transom (x ±0.38) steps 0.20 m down onto the swim step.',
      _notes:[
        { id:'pedestal', kind:'obstruction', type:'console', footprint:{x:[-PED.hx,PED.hx],y:[PED.y-PED.hy,PED.y+PED.hy]}, height_above_floor_m:{top:PED.h+0.15}, treatment:'wall', provenance:'exact, PED const; compass binnacle on top' },
        { id:'wheel', kind:'obstruction', type:'wheel', footprint:{x:[-WHEEL_GEO.rad,WHEEL_GEO.rad],y:[WHEEL_HUB.y-0.05,WHEEL_HUB.y+0.05]}, height_above_floor_m:{top:r3(WHEEL_HUB.z+WHEEL_GEO.rad-SOLE),underside:r3(WHEEL_HUB.z-WHEEL_GEO.rad-SOLE)}, treatment:'waist_block', provenance:'exact, WHEEL_HUB + WHEEL_GEO.rad; the helm stands aft of it at ANCHORS.helm' },
        { id:'mainsheet_block', kind:'obstruction', type:'fitting', footprint:{x:[-0.05,0.05],y:[SHEET_BLOCK[1]-0.05,SHEET_BLOCK[1]+0.05]}, height_above_floor_m:{top:0.07}, treatment:'step_over', provenance:'exact, SHEET_BLOCK; the mainsheet rises from it to the boom end — a rope in the air, not a floor obstruction' },
        { id:'bench_port', kind:'obstruction', type:'seat', footprint:{x:[-r3(xCoam(-1.8)-CK.coamT),-CK.xi],y:[CK.yB,CK.yA]}, height_above_floor_m:{top:r3(CK.seatZ-SOLE)}, treatment:'step_over', provenance:'exact, CK const; cushions +0.09 in two pads; published as DECK cockpit_seat_port as well — you stand on it to reach the boom' },
        { id:'bench_stbd', kind:'obstruction', type:'seat', footprint:{x:[CK.xi,r3(xCoam(-1.8)-CK.coamT)],y:[CK.yB,CK.yA]}, height_above_floor_m:{top:r3(CK.seatZ-SOLE)}, treatment:'step_over', provenance:'mirror of bench_port' },
        { id:'boom', kind:'overhead', type:'spar', footprint:{x:'swings 0..86° either side about the gooseneck',y:[r3(GOOSE[1]-BOOM_L),r3(GOOSE[1])]}, height_above_floor_m:{underside:r3(GOOSE[2]-0.085-SOLE)}, treatment:'overhead', provenance:'exact, GOOSE + BOOM_L; 1.85 m over the sole — a tall character ducks' },
      ],
      _notes_convention:'Footprints and heights above the sole plane (z='+SOLE+'). Game-side colliders sourced from these notes — never authored holes in the polygon (2026-07-22 ruling).',
      access:['companionway door (THRESHOLD) forward','open transom walk-through aft onto the swim step','either bench up onto the side decks'] };
    const seat=(s)=>({ id:'cockpit_seat_'+(s<0?'port':'stbd'), z:CK.seatZ, winding:'ccw_from_above',
      polygon:(s>0?[[CK.xi,CK.yB],[r3(xCoam(CK.yB)-CK.coamT),CK.yB],[r3(xCoam(CK.yA)-CK.coamT),CK.yA],[CK.xi,CK.yA]]:[[-r3(xCoam(CK.yA)-CK.coamT),CK.yA],[-r3(xCoam(CK.yB)-CK.coamT),CK.yB],[-CK.xi,CK.yB],[-CK.xi,CK.yA]]),
      note:'Bench top, level with the side deck. The coaming (seat back) outboard rises to z='+CK.coamZ+' and carries the primary winch pad at y='+WINCH.primaryY+'.',
      _notes:[{ id:'coaming', kind:'obstruction', type:'bulkhead_step', footprint:{x:'0.10 m band outboard of the seat', y:[CK.yB,CK.yA]}, height_above_floor_m:{top:r3(CK.coamZ-CK.seatZ)}, treatment:'step_over', provenance:'exact, CK.coamZ/coamT — the route from the seat up onto the side deck' },
              { id:'cushions', kind:'surface', type:'cushion', footprint:{y:[[-1.75,-0.75],[-2.90,-1.95]]}, height_above_floor_m:{top:0.09}, treatment:'flat', provenance:'exact, cockpit build()' }] });
    const roofPoly=[]; const NR=8;
    for(let i=0;i<=NR;i++){ const y=RF.yA+(RF.yF-RF.yA)*i/NR; roofPoly.push([-r3(hxRoof(y)-0.06),r3(y),r3(roofZ(y))]); }
    for(let i=NR;i>=0;i--){ const y=RF.yA+(RF.yF-RF.yA)*i/NR; roofPoly.push([r3(hxRoof(y)-0.06),r3(y),r3(roofZ(y))]); }
    const coachroof={ id:'coachroof', winding:'ccw_from_above', polygon3d:roofPoly,
      note:'Walkable — this is how a crew reaches the mast and the boom. Falls '+r3(RF.hA-RF.hF)+' m going forward; the raked front (y '+RF.yF+'..'+RF.yNose+') is a slope down to the foredeck, not a step.',
      _notes:[
        { id:'mast', kind:'obstruction', type:'post', footprint:{x:[-0.09,0.09],y:[r3(MAST.y-0.09),r3(MAST.y+0.09)]}, height_above_floor_m:{top:'full height — masthead z '+MAST.headZ}, treatment:'wall', provenance:'exact, MAST' },
        { id:'sliding_hatch', kind:'obstruction', type:'hatch_lid', footprint:{x:[HATCH.x0,HATCH.x1],y:[HATCH.y0,HATCH.y1]}, height_above_floor_m:{top:0.075}, treatment:'step_over', provenance:'exact, HATCH; travels +'+HATCH.travel+' m forward when open — the APERTURE it uncovers (x ±0.34, y '+HATCH.aperture.y0+'..'+HATCH.aperture.y1+') is a hole down to the steps' },
        { id:'cabin_top_winches', kind:'obstruction', type:'winch', footprint:{x:[[-WINCH.cabinX-0.1,-WINCH.cabinX+0.1],[WINCH.cabinX-0.1,WINCH.cabinX+0.1]],y:[WINCH.cabinY-0.1,WINCH.cabinY+0.1]}, height_above_floor_m:{top:0.20}, treatment:'step_over', provenance:'exact, WINCH.cabin*' },
        { id:'deck_hatches', kind:'surface', type:'hatch_lid', footprint:{y:[[1.27,1.83],[2.31,2.79]]}, height_above_floor_m:{top:0.06}, treatment:'flat', provenance:'exact, coachroof build()' },
        { id:'boom', kind:'overhead', type:'spar', footprint:{y:[r3(GOOSE[1]-BOOM_L),r3(GOOSE[1])]}, height_above_floor_m:{underside:r3(GOOSE[2]-0.085-roofZ(RF.yA))}, treatment:'overhead', provenance:'exact — only '+r3(GOOSE[2]-0.085-roofZ(RF.yA))+' m over the aft roof: a crouch' },
      ] };
    const forePoly=[]; const NF=6, u0f=uOf(RF.yNose), u1f=0.975;
    for(let i=0;i<=NF;i++){ const u=u0f+(u1f-u0f)*i/NF; forePoly.push([-r3(halfAtZ(u,deckZ(u),1)-0.02), r3(yOf(u)+rakeAt(u,1)), r3(deckZ(u))]); }
    for(let i=NF;i>=0;i--){ const u=u0f+(u1f-u0f)*i/NF; forePoly.push([r3(halfAtZ(u,deckZ(u),1)-0.02), r3(yOf(u)+rakeAt(u,1)), r3(deckZ(u))]); }
    const foredeck={ id:'foredeck', winding:'ccw_from_above', polygon3d:forePoly,
      note:'Rising with the sheer from the coachroof nose to the stem head; fenced by the pulpit and lifelines. Reached over the coachroof front slope or along either side deck (WASHBOARD).',
      _notes:[
        { id:'furler_drum', kind:'obstruction', type:'fitting', footprint:{x:[-0.08,0.08],y:[r3(FORESTAY.foot[1]-0.10),r3(FORESTAY.foot[1]+0.06)]}, height_above_floor_m:{top:r3(FORESTAY.foot[2]+0.14-deckZ(0.975))}, treatment:'step_over', provenance:'exact, FORESTAY.foot' },
        { id:'pulpit', kind:'obstruction', type:'guard_rail', footprint:{x:'both deck edges from y 4.22 round the stem head'}, height_above_floor_m:{top:0.62}, treatment:'wall', provenance:'exact, rigging build()' },
        { id:'jib_tracks', kind:'obstruction', type:'rail', footprint:{x:[[-r3(carX()+0.03),-r3(carX()-0.03)],[r3(carX()-0.03),r3(carX()+0.03)]],y:[0.40,1.60]}, height_above_floor_m:{top:0.04, car:0.085}, treatment:'step_over', provenance:'exact, carX()/CAR_Y — on the side decks, listed here because the lazy sheet crosses the foredeck to the windward car' },
      ] };
    const cabinPoly=[[-0.55,-0.05],[-0.55,RF.yF-0.02],[0.55,RF.yF-0.02],[0.55,-0.05]];
    const cabin={ id:'cabin_sole', z:CAB_SOLE, level:'cabin', winding:'ccw_from_above', polygon:cabinPoly,
      note:'Below deck. The passage between galley/head aft and the settees forward, from the foot of the companionway steps to the forward bulkhead door. Headroom '+r3(roofZ(RF.yA)-0.05-CAB_SOLE)+' aft to '+r3(roofZ(RF.yF)-0.05-CAB_SOLE)+' forward under the coachroof. The V-berth beyond the bulkhead is a bunk, not a floor.',
      _notes:[
        { id:'galley', kind:'obstruction', type:'counter', footprint:{x:[-1.10,-0.46],y:[0.16,1.20]}, height_above_floor_m:{top:0.90}, treatment:'waist_block', provenance:'exact, cabin build(); stove on top at y 0.72..1.12 (INTERACT stove)' },
        { id:'head', kind:'obstruction', type:'compartment', footprint:{x:[0.45,1.08],y:[0.15,1.20]}, height_above_floor_m:{top:1.70}, treatment:'wall', provenance:'exact, HD const; teak door on the inboard face' },
        { id:'compression_post', kind:'obstruction', type:'post', footprint:{x:[-0.04,0.04],y:[r3(MAST.y-0.04),r3(MAST.y+0.04)]}, height_above_floor_m:{top:'to the deckhead'}, treatment:'wall', provenance:'exact, under MAST' },
        { id:'settees', kind:'obstruction', type:'seat', footprint:{x:'outboard of ±0.55 to the hull',y:[1.30,3.00]}, height_above_floor_m:{top:0.40}, treatment:'step_over', provenance:'exact, cabin build(); lockers above from 0.95 to 1.37 (INTERACT locker = starboard)' },
        { id:'table', kind:'obstruction', type:'table', footprint:{x:[-0.30,0.30],y:[1.60,2.70]}, height_above_floor_m:{top:0.71}, treatment:'waist_block', provenance:'exact, cabin build()' },
        { id:'steps', kind:'obstruction', type:'bulkhead_step', footprint:{x:[-0.30,0.30],y:[RF.yA,0.11]}, height_above_floor_m:{treads:[0.23,0.47,0.71],top:0.95}, treatment:'stairs', provenance:'exact — see STAIRS.companionway' },
      ] };
    const wash=(s)=>{ const pts=[]; const NW=8; for(let i=0;i<=NW;i++){ const u=uOf(CK.yT)+(uOf(RF.yF)-uOf(CK.yT))*i/NW; pts.push([r3(s*(halfAtZ(u,deckZ(u),1)-0.006)), r3(yOf(u)), r3(deckZ(u))]); } return pts; };
    const WASHBOARD=[
      { side:'port', width_m:0.42, outer_edge:wash(-1), note:'Side deck from the transom quarter to the coachroof front, 0.42 m inboard from the sheer along the cockpit coaming and the coachroof (widening to 0.54 at the coachroof nose). Stanchions stand 0.07 m in from the edge with twin lifelines at 0.32 / 0.62. Chainplates + shroud bases at y '+CHAIN.yAft+'..'+CHAIN.yFwd+', x ±'+CHAIN.x+' are a step-over; the jib car at y '+CAR_Y+' is 0.085 proud.', provenance:'rig sdA/sdF + xCoam(y)' },
      { side:'starboard', mirror:'port across x=0' } ];
    const THRESHOLD={ id:'companionway', side:'aft', mechanism:'hinged',
      door_clear_width_m:r3(DOOR.x1-DOOR.x0), door_clear_height_m:r3(DOOR.z1-DOOR.z0), threshold_point:[0,DOOR.y,DOOR.z0],
      sill_above_cockpit_sole_m:r3(DOOR.z0-SOLE),
      hinge_axis:{ x:DOOR.hingeX, y:r3(DOOR.y-0.02), vertical:true, side:'port' },
      swing:{ angle_deg:DOOR.swing, outward:true, into:'cockpit', clear_at_open_fraction:DOOR.clearAt,
        keep_clear:(function(){ const arc=[[DOOR.hingeX,r3(DOOR.y-0.02)]]; for(let k=0;k<=6;k++){ const a=DOOR.swing*DEG*k/6; arc.push([r3(DOOR.hingeX+Math.cos(a)*DOOR.leaf.w), r3(DOOR.y-0.02-Math.sin(a)*DOOR.leaf.w)]); } return arc; })(),
        note:'The collider is the swept sector, not the leaf. Fully open the leaf lies along the port bench face.' },
      hatch:{ kind:'sliding', travel_m:HATCH.travel, direction:'+y (forward) over the coachroof', follows_door:true, aperture:{ x:[HATCH.aperture.x0,HATCH.aperture.x1], y:[HATCH.aperture.y0,HATCH.aperture.y1] }, note:'Opens the top of the companionway; with the hatch shut and the door open the headroom in the opening is '+r3(DOOR.z1-DOOR.z0)+' m — a duck.' },
      door_cue:{ frames:8, played_reversed_on_exit:true, doorOpen_per_frame:'k/7 for k in 0..7', suggested_ms_per_frame:70 },
      provenance:'rig DOOR + HATCH consts; defaults CLOSED (owner ruling 2026-08-19)' };
    const STAIRS={ companionways:[{ id:'companionway', from:'cockpit_sole', to:'cabin_sole', mechanism:'InteriorStair',
      opening:{ x0:DOOR.x0, x1:DOOR.x1, at_y:DOOR.y, z0:DOOR.z0, z1:DOOR.z1 }, sill_z:DOOR.z0,
      total_rise_m:r3(DOOR.z0-CAB_SOLE), treads:[{top_z:1.06,going_m:0.22},{top_z:0.82,going_m:0.22},{top_z:0.58,going_m:0.22}],
      direction:'down going forward (+y)', provenance:'cabin build() treads; sill = DOOR.z0' }] };
    const A=ANCHOR_PTS, P3=(p)=>[r3(p.x),r3(p.y),r3(p.z)];
    const facings=(nx,ny)=>{ const o=[]; ['N','NE','E','SE','S','SW','W','NW'].forEach((d,i)=>{ const th=i*Math.PI/4; if(-nx*Math.sin(th)+ny*Math.cos(th)<0) o.push(d); }); return o; };
    const INTERACT=[
      { id:'helm', action:'enter_helm', label:'Wheel', verb:'Take the helm', level:'cockpit_sole', pos:P3(A.wheelHub), reach_point:P3(A.helm), visible_facings:['N','NE','E','SE','S','SW','W','NW'], mech:'the single pedestal wheel; renderWheel(dir,{steer}) turns it, the rudder follows underbody', provenance:'WHEEL_HUB / ANCHOR_PTS.helm' },
      { id:'mainsheet', action:'trim_main', label:'Mainsheet', verb:'Trim the main', level:'cockpit_sole', pos:P3(A.mainsheetBlock), reach_point:[0.40,-2.55,SOLE], visible_facings:['N','NE','E','SE','S','SW','W','NW'], mech:'opts.main 0..1 — the boom follows within the wind', provenance:'SHEET_BLOCK' },
      { id:'jib_winch_port', action:'trim_jib', label:'Port primary', verb:'Trim the jib', level:'cockpit_seat_port', pos:P3(A.winchPort), reach_point:[-0.62,-2.10,SOLE], visible_facings:['N','NE','E','SE','S','SW','W','NW'], mech:'opts.jib 0..1; opts.grind:"jib" animates the handle on the LEEWARD primary', provenance:'WINCH.primary*' },
      { id:'jib_winch_stbd', action:'trim_jib', label:'Starboard primary', verb:'Trim the jib', level:'cockpit_seat_stbd', pos:P3(A.winchStbd), reach_point:[0.62,-2.10,SOLE], visible_facings:['N','NE','E','SE','S','SW','W','NW'], mech:'as port', provenance:'WINCH.primary*' },
      { id:'halyard_winch', action:'hoist_main', label:'Halyard winch', verb:'Hoist / lower the main', level:'coachroof', pos:P3(A.halyardWinch), reach_point:[0.45,-0.70,CK.seatZ], visible_facings:['N','NE','E','SE','S','SW','W','NW'], mech:'opts.hoist 0..1; opts.cover zips the stack pack at 0 (STORED)', provenance:'WINCH.cabin* — reached standing on the bridge deck / stbd bench' },
      { id:'furler', action:'furl_jib', label:'Furling line', verb:'Furl / unfurl the jib', level:'cockpit_sole', pos:P3(A.furlerCleat), reach_point:[-0.62,-1.05,SOLE], visible_facings:['N','NE','E','SE','S','SW','W','NW'], mech:'opts.furl 0..1 — the line runs the port deck to the drum', provenance:'furling line build()' },
      { id:'stove', action:'cook', label:'Stove', verb:'Cook', level:'cabin_sole', pos:[-0.76,0.92,1.28], reach_point:[-0.20,0.90,CAB_SOLE], visible_facings:facings(-1,0), mech:'galley, port aft', provenance:'cabin build()' },
      { id:'locker', action:'storage', label:'Locker', verb:'Open the locker', level:'cabin_sole', pos:[r3(halfAtZ(uOf(2.1),1.45,1)-0.20),2.10,1.50], reach_point:[0.30,2.10,CAB_SOLE], visible_facings:facings(1,0), mech:'starboard saloon locker over the settee', provenance:'cabin build()' },
      { id:'bunk', action:'sleep', label:'V-berth', verb:'Sleep · save', level:'cabin_sole', pos:[0,3.70,r3(CAB_SOLE+0.50)], reach_point:[0,2.90,CAB_SOLE], visible_facings:facings(0,1), mech:'InteriorBed (rest + save) — through the forward bulkhead door', provenance:'cabin build()' },
    ];
    const SAIL={ rig:'fractional sloop · deck-stepped mast on the coachroof · two swept spreader sets · split backstay · roller-furling jib',
      mast:{ foot:[0,MAST.y,r3(MAST.footZ)], head_z:MAST.headZ, rake_deg:MAST.rakeDeg, compression_post:'cabin, x 0 y '+MAST.y, spreaders:SPREADERS.map(s=>({z:s.z,len_m:s.len,sweep_deg:s.sweep})), chainplates:{x:CHAIN.x,y:[CHAIN.yAft,CHAIN.yCap,CHAIN.yFwd]} },
      boom:{ gooseneck:P3({x:GOOSE[0],y:GOOSE[1],z:GOOSE[2]}), length_m:BOOM_L, section_m:[0.11,0.17], clearance_over_cockpit_sole_m:r3(GOOSE[2]-0.085-SOLE), swing_deg:{min:0,max:86,side:'leeward = opposite the wind'}, end_over:'the cockpit, forward of the wheel' },
      main:{ P_m:MAIN.P, E_m:MAIN.E, head_m:MAIN.head, roach_m:MAIN.roach, battens:MAIN.battens, area_m2:r3(0.5*MAIN.P*MAIN.E*1.18),
        hoist:'opts.hoist 0..1 — luff = P*hoist up the mast; the stack pack on the boom grows 0.10 -> 0.40 m as the cloth comes down', stored:'hoist 0 + cover:true — the stack pack zipped; the STORED state', sheet:'opts.main 0..1 — 1 hardened (boom 4°), 0 eased (boom 86°)', luff_on:'mast aft face', foot_on:'boom (loose-footed)' },
      jib:{ luff_m:r3(JIB.luff), LP_m:JIB.LP, area_m2:r3(0.5*JIB.luff*JIB.LP), overlap:'105% of J', tack:P3({x:JIB.tack[0],y:JIB.tack[1],z:JIB.tack[2]}), head:P3({x:JIB.head[0],y:JIB.head[1],z:JIB.head[2]}),
        furl:'opts.furl 0..1 — rolled onto the forestay; rolled radius 0.045+0.11*furl, the UV strip is the canvas slot', sheet:'opts.jib 0..1 — 1 hardened (9°), 0 eased (85°)', leads:{ cars:[[-r3(carX()),CAR_Y],[r3(carX()),CAR_Y]], primaries:[[-r3(WINCH.primaryX()),WINCH.primaryY],[r3(WINCH.primaryX()),WINCH.primaryY]] } },
      pose_law:{ inputs:'awa (deg, + = wind over the starboard bow), aws (kn), main 0..1, jib 0..1, frame 0..7',
        boom_deg:'min(4 + 82*(1-main), max(0, |awa| - 6))', jib_deg:'min(9 + 76*(1-jib), max(0, |awa| - 6))',
        angle_of_attack:'|awa| - sail angle', fill:'clamp((aoa-5)/13) * clamp(aws/6); jib *0.4 when |awa|>150 (blanketed)',
        luff:'clamp((8-aoa)/8) — flogging near the luff; |awa|<25 = in irons, both sails flog whole and the boom wanders ±3°',
        stall:'aoa>40 — full but dead; camber eases 40% by aoa 80', heel_deg:'min(24, 0.067*aws^2*(0.25+0.75*fill_main)*k(awa)) to leeward; k=1 to 100°, 0.35 dead downwind; 15% in irons',
        frames:'8, looping: cloth flutter + winch handle + (rock:true) the wave', note:'ART-SIDE law: it makes the sprite agree with itself. Gameplay owns the polar and drive; feed the rig the awa/aws/sheet it decides and the picture follows.' },
      standing_rigging:{ forestay:[P3({x:FORESTAY.foot[0],y:FORESTAY.foot[1],z:FORESTAY.foot[2]}),P3({x:FORESTAY.head[0],y:FORESTAY.head[1],z:FORESTAY.head[2]})], backstay:'split, masthead to the quarters at (±1.05, -4.45)', caps:'chainplates (±'+CHAIN.x+', '+CHAIN.yCap+') through both spreader tips to the masthead', lowers:'fore + aft from (±'+CHAIN.x+', '+CHAIN.yFwd+'/'+CHAIN.yAft+') to the lower spreader root' },
      running_rigging:{ mainsheet:'boom end -> sole block '+JSON.stringify(SHEET_BLOCK.map(r3))+' -> pedestal cleat', jib_sheets:'clew -> car -> primary, both sides; the lazy sheet sags across the foredeck forward of the mast', halyards:'mast foot -> deck organiser -> clutch banks -> cabin-top winches (±'+WINCH.cabinX+', '+WINCH.cabinY+')', furling_line:'drum -> port side deck -> coaming cleat (-1.0)', lazy_jacks:'mast z 6.3 -> stack pack, two legs a side', topping_lift:'masthead -> boom end' } };
    const WATERLINE={ z:DWL, clip_rule:'the water shader cuts at hull z='+DWL+' — the boot-top BOTTOM. The pivot row is the canoe-body bottom, '+DWL+' m (~'+Math.round(DWL*Math.cos(40*DEG)*32)+' px at elev 40) below it; there is no per-facing correction, the DWL is a horizontal line in sprite space under roll 0.',
      draft_m:{ canoe_body:DWL, keel:1.90, rudder:1.50 }, underbody:'fin keel (root chord 1.60 at y -0.85..0.75, bulb at z -1.28) and spade rudder (stock y -3.80) bake only with opts.underbody — the dry reference. Afloat they are under the shader\u2019s water.',
      note:'Heel is a render param (pose.heel, degrees about the origin): the waterline on the sprite tilts with it; the shader cut stays level in world.' };
    return {
      schema:'hidden-harbours/boat-gameplay-geometry@1', rig:'sloopIsoRig.js', exportSymbol:'SloopIso', units:'metres',
      frame:{ origin:'amidships / canoe-body bottom / centreline', axes:'+x starboard, +y bow, +z up', scale_px_per_m:PX, LOA_m:L, polygon_winding:'CCW viewed from +Z (above)' },
      authoring:'Generated by SloopIso.gameplayGeometry() from the same constants the bake uses (station/skin/halfAtZ/deckZ/roofZ, CK, RF, DOOR, HATCH, MAST, GOOSE, WINCH). Fit-out footprints are the build literals. Do not hand-edit — regenerate from the builder page; Art/_sidecarExport.js stamps derivedFromRigSha256.',
      extractor_contract:'per section: rig export -> this sidecar -> absent section = hull does not support the feature (not an error).',
      hull:{ loa:L, beam:r3(2*T[3][0]), transomBeam:r3(2*T[0][0]), dwl_z:DWL, cockpitSole:SOLE, cabinSole:CAB_SOLE, seat:CK.seatZ, coaming:CK.coamZ, sheerAft:r3(sheerZ(0)), sheerAmid:r3(sheerZ(0.5)), sheerStem:r3(sheerZ(1)), coachroofAft:r3(roofZ(RF.yA)), coachroofFwd:r3(roofZ(RF.yF)), masthead:MAST.headZ, bilgeFrac:FC, rake_m:RAKE },
      DECK:[cockpit, seat(-1), seat(1), coachroof, foredeck, cabin],
      WASHBOARD, CLEATS:CLEAT_PTS.map(c=>Object.assign({},c,{provenance:'exact, deck build() cleat boxes'})),
      ANCHORS:Object.keys(A).reduce((o,k)=>{ o[k]=P3(A[k]); return o; },{}),
      THRESHOLD, STAIRS, INTERACT, SAIL, WATERLINE,
      _excluded:{ LADDER:'None — a 30-footer: the coachroof is a step up from the bench, the swim step a step down from the sole.',
        sprayhood_bimini:'Not modelled in pass 1 (clutter at 32 px/m); the canvas slot dresses the stack pack and the jib UV strip.',
        spinnaker:'No downwind sail. The jib is blanketed dead downwind (fill *0.4) and that is the picture.',
        anchor_locker:'The stem head carries the roller and the furler drum; the locker lid is not modelled.',
        wake_spray:'The game\u2019s water owns the cut, the wake and the heel-line foam.' },
      _confirm:{ heel_law:'art-side; gameplay may override with heelDeg per frame', boom_over_cockpit:'1.85 m over the sole — whether a character ducks or clips is a gameplay ruling', coachroof_walkable:'published walkable; slope and hatches as notes — rule whether the front slope is a route down to the foredeck', reach_points:'untested requests, per the camper precedent' } };
  }

  root.SloopIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], PAINT, STEEL, DECKF, GLAS, MOTO, ROPE, TEAK, CREAM, KEY,
    render, renderWheel, wheelFaces, WHEEL_GEO, WHEEL_HUB, WHEEL_LOCK, ROCK, rock:rockMotion,
    sailPose, poseOf, anchors, ANCHOR_PTS, CLEAT_PTS, doorMount, DOOR, HATCH, RIG, SAILS:{ main:MAIN, jib:JIB },
    L, DWL, SOLE, CAB_SOLE, ZLIP, geometry, LEVEL_IDS, gameplayGeometry, faces:()=>F,
    loft:{ station, skin, fracAtZ, halfAtZ, sheerZ, deckZ, roofZ, hxRoof, xCoam, uOf, yOf, L, TH, DWL, SOLE, CAB_SOLE, NSEG,
           coachroof:RF, cockpit:CK, mast:MAST, shade:{ GAIN, BIAS, LN, BAYER, KEY, EDGE:0.30 }, cell:{ W, H, cx, cy, S } },
    SCHEMES, schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, SLOTS, palette, rampFrom, chipWall, C_CAP };
})(typeof globalThis!=='undefined'?globalThis:window);

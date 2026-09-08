/* Hidden Harbours — parametric ISO SLOOP 88 (27.0 m / 88.5 ft performance cruising yacht) — M2 bake recipe,
   ADR-0006, same pipeline as sloopIsoRig.js (the Sloop 30). PASS 1: hull, laid-teak decks, the deck saloon,
   a twin-wheel cockpit with two U-settees, the aft deck with its transom stair and fold-down platform, the
   whole three-spreader rig with a self-tacking staysail, and the saloon + forward accommodation you can walk into.

   Built to the reference set (2026-09-05): plumb bow, long flat sheer, wide stern, a low flush deck saloon with
   a wraparound smoked-glass band and two glass roof panels, twin pedestal wheels with instrument pods and helm
   seats, an aft deck with two flush garage lids, a stair down the transom onto a fold-down swim platform,
   flush foredeck hatches, a sunken tender well forward of the mast, a bow pad inside the pulpit and seat
   pads in the quarter pushpits, hull slit windows in two groups of three. 

   ADDITIONAL HARDWARE FOR THE 88 (not on the 30): twin helm stations · powered primaries + mainsheet winches
   + halyard winches (six drums) · mainsheet traveller across the aft deck · a Park-Avenue boom the main flakes
   INTO (no stack pack) with a rigid hydraulic vang (no topping lift) · three swept spreader sets with
   discontinuous diagonals and twin backstays · inner forestay + self-tacking staysail on a curved track ·
   windlass, bow roller arm and anchor · fold-down transom platform (opts.platform) · sliding companionway
   door · eight cleats (bow, two spring pairs, stern) · painted white spars.

   THE SAILS ARE POSE, NOT PIXELS — the Sloop 30 law, retuned for 27 m: heel = min(20, 0.040*aws^2*(...)*k).
   render(dir,{awa,aws,main,jib,hoist,furl,sfurl,cover,doorOpen,platform,view,steer,grind,frame,rock,underbody,
   scheme|paint,elev}). opts.sfurl 0..1 rolls the staysail on the inner forestay (default 1 = furled).
   opts.platform 0..1 (default 1 = down): 0 folds the platform up as a flat transom door over the stair.

   THE CABIN. opts.view:'cabin' cuts the boat open at the boot top (ZLIP 1.55): the deck saloon (raised sole
   2.05, U-settee + table port, chart table + settee starboard, the mast through the middle), two steps down
   forward to the lower passage (sole 1.20): galley port, guest double starboard, the VIP island berth, the
   crew cabin. The owner's cabin under the cockpit is pass 2 (_excluded).

   ORIGIN. Amidships / CANOE-BODY BOTTOM / centreline, pinned every heading — the fleet convention. DWL 1.35 m
   above it. 32 px = 1 m. Paint never moves a vertex: SCHEMES swap ramps only.
   Exposes globalThis.Sloop88Iso (same surface as SloopIso + bounds(dir,opts), WHEEL_HUBS, STAY). */
(function (root) {
  const PX = 32, S = 32;
  const W = 1072, H = 1504, cx = 536, cy = 1150;   // locked by the cell-fit sweep: bounds() over elev 30–50 × 8 dirs × heel 20° × boom 86° × rock (±519 / 1133 up / 339 down)
  const DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const ROCK = { frames:8, rollA:1.1, pitchA:0.55, heaveA:1.0, period:5.2 };   // 27 m — long, slow
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 27.0, TH = 0.07;
  const DWL = 1.35;          // design waterline above the canoe-body bottom
  const ZLIP = 1.55;         // cabin section cut — the boot-top line
  const SOLE = 2.55;         // cockpit sole
  const SAL = 2.05;          // deck-saloon sole
  const LOW = 1.20;          // lower accommodation sole
  const PLAT = 1.55;         // swim platform (down)
  const NSEG = 30;
  const HEEL_MAX = 20, HEEL_K = 0.040;
  const clamp01=(v)=>v<0?0:v>1?1:v, clamp=(v,a,b)=>v<a?a:v>b?b:v;
  const lerp=(a,b,t)=>a+(b-a)*t;

  // ---- fixed ramps (never painted) -------------------------------------------------------------
  const PAINT = ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];
  const DECKF = ['#6a7069','#848a82','#9ca29a','#b4bab0','#cad0c5'];
  const GLAS  = ['#16333c','#24505a','#3a7680','#5fa3a6','#8fc9c4'];
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];
  const ROPE  = ['#54432c','#7a6242','#a98f66','#cdbe97','#e3dbc1'];
  const TEAK  = ['#3f2814','#54351d','#6c4626','#855a31','#9c6e40','#b28553'];
  const CREAM = ['#868e93','#a2aaae','#bfc6c6','#d6dbd7','#e8ebe5','#f2f4ee','#fbfcf6'];
  const KEY   = '#101a19';

  // ---- paint mixer (OKLCH) — identical envelope to the small craft and the Sloop 30 ---------------
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

  // ---- colourways (same eight as the Sloop 30, hull for hull; the deck slot is LAID TEAK here) -------
  // Slots: hull (topsides + coachroof gelcoat), stripe (cove line + boot top), bottom (anti-foul), deck
  // (laid teak decks), canvas (boom cover, sail UV strips), sail (cloth), teak (cockpit + saloon joinery,
  // steps, platform), uph (cockpit, helm, bow + quarter pads, saloon + berths).
  const SCHEMES = {
    'gelcoat-white': { name:'Gelcoat White', hull:'#eef0ea', stripe:'#7a2f22', bottom:'#2a2f33', deck:'#b8956a',
                       canvas:'#22354a', sail:'#d9dbd6', teak:'#9c6e40', uph:'#9aa6b4',
                       ramps:{ hull:PAINT, teak:TEAK },
                       note:'the yard\u2019s own \u2014 white glass, bordeaux cove, navy canvas, grey laminate, slate-blue pads' },
    'atlantic-navy': { name:'Atlantic Navy', hull:'#254a6b', stripe:'#eef0ea', bottom:'#8c3f2c', deck:'#b8956a',
                       canvas:'#c9b98f', sail:'#eef0ea', teak:'#9c6e40', uph:'#e6ddc6',
                       note:'navy hull, white cove, sand canvas, white dacron' },
    'oyster-bone':   { name:'Oyster Bone',   hull:'#e6ddc6', stripe:'#22354a', bottom:'#22354a', deck:'#b8956a',
                       canvas:'#7a2f22', sail:'#efe9d6', teak:'#9c6e40', uph:'#9aa6a4',
                       note:'bone gelcoat, navy cove, bordeaux canvas, cream cloth' },
    'squall-grey':   { name:'Squall Grey',   hull:'#8a99a1', stripe:'#22354a', bottom:'#2a2f33', deck:'#9c9284',
                       canvas:'#343b41', sail:'#b9bfc1', teak:'#855a31', uph:'#a9b0a8',
                       note:'grey-blue hull, silvered teak, charcoal canvas, grey laminate' },
    'bottle-green':  { name:'Bottle Green',  hull:'#31694c', stripe:'#e2dcc7', bottom:'#8c3f2c', deck:'#b8956a',
                       canvas:'#b89a6a', sail:'#a8563a', teak:'#9c6e40', uph:'#cdb98d',
                       note:'green hull, cream cove, tan canvas, TANBARK sails' },
    'graphite':      { name:'Graphite',      hull:'#343b41', stripe:'#2ba39a', bottom:'#2a2f33', deck:'#9c9284',
                       canvas:'#1d2127', sail:'#6b7378', teak:'#6c4626', uph:'#8f8a7e',
                       note:'dark glass, teal cove, silvered teak, black canvas, dark laminate' },
    'cranberry':     { name:'Cranberry',     hull:'#a8452f', stripe:'#e6ddc6', bottom:'#2a2f33', deck:'#b8956a',
                       canvas:'#c9b98f', sail:'#eef0ea', teak:'#9c6e40', uph:'#e6ddc6',
                       note:'bog red hull, cream cove, sand canvas' },
    'seafoam':       { name:'Seafoam',       hull:'#a3d9c8', stripe:'#7a2f22', bottom:'#7a2f22', deck:'#b8956a',
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
    const deckR   = R.deck   || rampFrom(B.deck,6,{anchor:0.66});
    const canvR   = R.canvas || rampFrom(B.canvas,5,{anchor:0.72});
    const sailR   = R.sail   || rampFrom(B.sail,7,{anchor:0.74,ceil:0.55,floor:0.62});
    const teakR   = R.teak   || rampFrom(B.teak,6,{anchor:0.70});
    const uphR    = R.uph    || rampFrom(B.uph,5,{anchor:0.72});
    const mats = { paint:{ramp:hullR,off:0,dith:0},    stripe:{ramp:stripeR,off:-1,dith:0},
                   bottom:{ramp:botR,off:-1,dith:0},   deck:{ramp:deckR,off:0,dith:0.22},
                   liner:{ramp:hullR,off:0,dith:0},    canvas:{ramp:canvR,off:0,dith:0.15},
                   sail:{ramp:sailR,off:0,dith:0.10},  batten:{ramp:sailR,off:-2,dith:0},
                   teak:{ramp:teakR,off:0,dith:0.12},  uph:{ramp:uphR,off:0,dith:0.18},
                   cream:{ramp:CREAM,off:0,dith:0},    steel:{ramp:STEEL,off:0,dith:0},
                   spar:{ramp:PAINT,off:-1,dith:0},    mast:{ramp:STEEL,off:-1,dith:0},
                   wire:{ramp:STEEL,off:-2,dith:0},    rope:{ramp:ROPE,off:0,dith:0},
                   glas:{ramp:GLAS,off:0,dith:0},      moto:{ramp:MOTO,off:0,dith:0},
                   blk:{ramp:MOTO,off:-2,dith:0} };
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

  // ---- section table: stern(0) -> bow(8), 3.375 m apart --------------------------------------------
  // [sheerHalf, bilgeHalf, keelHalf, bilgeRise, sheerRise, keelZ] (m). Beam 6.70 carried well aft to a 5.4 m
  // transom, soft chine aft, a fine plumb entry, the sheer nearly flat: 3.30 at the transom -> 3.95 at the stem.
  const T = [
    [2.70, 2.42, 0.30, 0.38, 2.15, 1.15],   // transom                sheer 3.30
    [3.14, 2.86, 0.36, 0.62, 2.72, 0.60],   //                              3.32
    [3.32, 3.02, 0.38, 0.88, 3.22, 0.13],   //                              3.35
    [3.35, 3.00, 0.36, 1.02, 3.38, 0.00],   // max beam 6.70 m              3.38
    [3.30, 2.86, 0.33, 1.10, 3.45, 0.00],   // amidships                    3.45
    [3.10, 2.52, 0.28, 1.18, 3.48, 0.04],   //                              3.52
    [2.70, 1.92, 0.20, 1.20, 3.50, 0.12],   //                              3.62
    [1.92, 1.12, 0.12, 1.15, 3.42, 0.35],   // fine entry                   3.77
    [0.16, 0.11, 0.04, 0.95, 3.10, 0.85],   // stem head                    3.95
  ];
  const FC = 0.42, FK = 0.62;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), ch:lerp(A[1],B[1],fr), kw:lerp(A[2],B[2],fr),
             cd:lerp(A[3],B[3],fr), dep:lerp(A[4],B[4],fr), kz:lerp(A[5],B[5],fr), y:-L/2+u*L };
  }
  const RAKE=0.06;
  const rakeAt=(u,frac)=>-RAKE*Math.pow(Math.max(0,(u-0.80)/0.20),1.6)*(1-frac);
  function skin(side,u,frac,inset){
    const st=station(u), t=inset?1:0;
    const ws=st.ws-(t?TH:0), chh=st.ch-(t?TH:0), kw=Math.max(0.004, st.kw-(t?TH*0.5:0));
    const kz=st.kz+(t?TH*0.7:0), cz=st.kz+st.cd+(t?TH*0.3:0), sz=st.kz+st.dep-(t?0.012:0);
    let x,z;
    if(frac<=FC){ const q=frac/FC; x=lerp(kw,chh,q); z=lerp(kz,cz,q); }
    else {
      const K=(FK-FC)/(1-FC);
      const knuck=0.05*Math.pow(Math.max(0,1-u/0.72),1.2);
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
  const deckZ=(u)=>sheerZ(u)-0.03;
  const uOf=(y)=>(y+L/2)/L, yOf=(u)=>-L/2+u*L;
  const dZ=(y)=>deckZ(uOf(y));                                  // deck height at a plan y
  const hD=(y)=>halfAtZ(uOf(y),deckZ(uOf(y)),1);               // half-beam at the deck edge (inner skin)


  // ---- plan constants ----------------------------------------------------------------------------
  const RF = { yA:-1.6, yF:4.3, yNose:5.8, hA:1.40, hF:1.00, sdA:0.85, sdF:1.05 };   // deck saloon
  const rfT=(y)=>clamp01((y-RF.yA)/(RF.yF-RF.yA));
  const roofZ=(y)=>{ const t=rfT(y); return dZ(y) + RF.hA + (RF.hF-RF.hA)*t*t; };
  const hxRoof=(y)=>{ const t=rfT(y); return Math.max(0.6, hD(y) - (RF.sdA + (RF.sdF-RF.sdA)*t)); };
  const innerRoof=(y)=> y<=RF.yF ? hxRoof(y) : lerp(hxRoof(RF.yF), hxRoof(RF.yNose)*0.92, clamp01((y-RF.yF)/(RF.yNose-RF.yF)));
  const CK = { yA:-1.6, yB:-5.9, yH:-7.9, xi:0.55, seatZ:3.00, coamZ:3.58, coamT:0.14, sd:0.80 };   // cockpit
  const xCoam=(y)=>hD(y) - CK.sd;                 // outer face of the coaming = inner edge of the side deck
  const xIn=(y)=>xCoam(y) - CK.coamT;             // inner face of the coaming
  const STEP1 = SOLE + 0.36;                      // walkway step up to the aft deck
  const AFT = { y0:-11.6, y1:CK.yH };             // aft deck (planked, full width)
  const STAIR = { x:1.0, floorY:-13.1, treads:[[-11.6,-12.1,2.85],[-12.1,-12.6,2.42],[-12.6,-13.1,1.98]] };   // [yFwd, yAft, top]
  const PLATFORM = { y0:-13.5, y1:-14.45, hx:1.6, z:PLAT, th:0.08 };
  const TABLE = { x:1.35, y:-3.75, hx:0.5, hy:1.0, z:SOLE+0.72 };
  const PED = { x:2.0, y:-6.95, hx:0.16, hy:0.13, h:0.92 };
  const WHEEL_HUBS = [ { x:-2.0, y:-7.12, z:SOLE+1.02 }, { x:2.0, y:-7.12, z:SOLE+1.02 } ];
  const WHEEL_GEO = { rad:0.60, rimIn:0.55, rake:-10, spokes:8, seg:20, hubR:0.07 };
  const WHEEL_LOCK = 2.0;
  const HSEAT = { x:2.05, y:-7.68, hx:0.5, hy:0.17, z:3.02 };
  const MAST = { x:0, y:2.2, footZ:roofZ(2.2), headZ:37.0, rakeDeg:1.2 };
  const mastY=(z)=>MAST.y - (z-MAST.footZ)*Math.tan(MAST.rakeDeg*DEG);
  const mastAt=(z)=>[0, mastY(z), z];
  const GOOSE_Z = MAST.footZ+1.75;
  const GOOSE = [0, mastY(GOOSE_Z)-0.16, GOOSE_Z];
  const BOOM_L = 10.6, BOOM_SEC=[0.30,0.24];     // Park-Avenue section: 0.60 wide, 0.48 tall
  const MAIN = { P:30.0, E:10.2, head:0.9, roach:1.5, battens:[0.18,0.36,0.54,0.71,0.87] };
  const STEM_Y = 13.5;
  const FORESTAY = { foot:[0, 13.30, dZ(13.1)+0.32], headZ:36.55 };
  FORESTAY.head = mastAt(FORESTAY.headZ); FORESTAY.head[1]+=0.08;
  const fsAt=(t)=>[0, lerp(FORESTAY.foot[1],FORESTAY.head[1],t), lerp(FORESTAY.foot[2],FORESTAY.head[2],t)];
  const JIB = { tackT:0.025, headT:0.97, LP:10.2, clewUp:1.25 };
  JIB.tack=fsAt(JIB.tackT); JIB.head=fsAt(JIB.headT);
  JIB.luff=Math.hypot(JIB.head[1]-JIB.tack[1], JIB.head[2]-JIB.tack[2]);
  const INNER = { foot:[0, 8.3, dZ(8.3)+0.15], headZ:28.0 };
  INNER.head = mastAt(INNER.headZ); INNER.head[1]+=0.06;
  const isAt=(t)=>[0, lerp(INNER.foot[1],INNER.head[1],t), lerp(INNER.foot[2],INNER.head[2],t)];
  const STAY = { tackT:0.03, headT:0.96, LP:5.6, clewUp:0.95, trackY:6.35, trackHx:1.55, maxDeg:55 };
  STAY.tack=isAt(STAY.tackT); STAY.head=isAt(STAY.headT);
  STAY.luff=Math.hypot(STAY.head[1]-STAY.tack[1], STAY.head[2]-STAY.tack[2]);
  const SPREADERS = [ { z:12.6, len:2.75, sweep:20 }, { z:20.9, len:2.25, sweep:20 }, { z:29.0, len:1.65, sweep:20 } ];
  const CHAIN = { x:2.95, yCap:2.3, yFwd:3.4, yAft:1.2 };
  const BACKSTAY = { x:2.25, y:-12.9 };
  const WINCH = { primaryY:-6.4, primaryX:()=>xCoam(-6.4)-0.03, mainY:-8.65, mainX:2.15, halyardY:-0.9, halyardX:1.45, clutchY:-0.25 };
  const TRAV = { y:-8.35, hx:1.9 };
  const CAR_Y = 0.9, carX=()=>hD(CAR_Y)-0.30;
  const TURN = { y:-5.0, x:()=>xCoam(-5.0)+0.14 };
  const FURL_CLEAT = [-(xCoam(-5.4)-0.06), -5.4, CK.coamZ+0.05];
  const DOOR = { kind:'slide', face:'aft', y:RF.yA, x0:-0.62, x1:0.62, z0:SOLE+0.08, z1:roofZ(RF.yA)-0.16,
                 slide:'port', travel:1.30, leaf:{ w:1.24, th:0.05 }, clearAt:0.6 };
  const WELL = { y0:6.5, y1:10.3, hxA:1.55, hxF:1.05, depth:0.22 };
  const hxWell=(y)=>lerp(WELL.hxA,WELL.hxF,clamp01((y-WELL.y0)/(WELL.y1-WELL.y0)));
  const WINDLASS = { y:12.1 };
  const HATCHES = [[-2.0,7.2,0.30],[2.0,7.2,0.30],[-1.55,9.3,0.28],[1.55,9.3,0.28],[-0.8,11.0,0.24],[0.8,11.0,0.24]];
  const ROOF_GLASS = [[-1.1,0.3],[0.6,1.9]];
  const HULL_WIN = { slits:[-8.9,-8.4,-7.9, 6.4,6.9,7.4], ports:[[-2.6,-2.0],[9.6,10.3]], z0:2.25, z1:2.80 };
  const CLEAT_PTS = [
    { id:'bow_port', type:'cleat', pos:[-0.4,12.4,+(dZ(12.4)+0.045).toFixed(3)] }, { id:'bow_star', type:'cleat', pos:[0.4,12.4,+(dZ(12.4)+0.045).toFixed(3)] },
    { id:'spring_fwd_port', type:'cleat', pos:[-(+(hD(4.0)-0.32).toFixed(3)),4.0,+(dZ(4.0)+0.045).toFixed(3)] }, { id:'spring_fwd_star', type:'cleat', pos:[+(hD(4.0)-0.32).toFixed(3),4.0,+(dZ(4.0)+0.045).toFixed(3)] },
    { id:'spring_aft_port', type:'cleat', pos:[-(+(hD(-4.0)-0.32).toFixed(3)),-4.0,+(dZ(-4.0)+0.045).toFixed(3)] }, { id:'spring_aft_star', type:'cleat', pos:[+(hD(-4.0)-0.32).toFixed(3),-4.0,+(dZ(-4.0)+0.045).toFixed(3)] },
    { id:'stern_port', type:'cleat', pos:[-2.55,-12.75,+(dZ(-12.75)+0.045).toFixed(3)] }, { id:'stern_star', type:'cleat', pos:[2.55,-12.75,+(dZ(-12.75)+0.045).toFixed(3)] },
  ];
  const RIG = { mast:MAST, goose:GOOSE, boomL:BOOM_L, boomSec:BOOM_SEC, main:MAIN, forestay:FORESTAY, jib:JIB, inner:INNER, stay:STAY, spreaders:SPREADERS, chain:CHAIN, backstay:BACKSTAY, traveller:TRAV };

  // ---- generic solids ----------------------------------------------------------------------------
  const ID=(p)=>p;
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  const lerp3=(a,b,t)=>[lerp(a[0],b[0],t),lerp(a[1],b[1],t),lerp(a[2],b[2],t)];
  const rotZabout=(p,C,a)=>{ const c=Math.cos(a), s=Math.sin(a), x=p[0]-C[0], y=p[1]-C[1];
    return [C[0]+x*c-y*s, C[1]+x*s+y*c, p[2]]; };
  function mk(v,mat,b,db,ex){ const f={v,mat:mat||'paint',b:b||0,db:db||0}; if(ex) Object.assign(f,ex); return f; }
  const face=(F,v,mat,b,db,ex)=>F.push(mk(v,mat,b,db,ex));
  // face with an OUTWARD hint: the vertex order is flipped if the polygon's normal disagrees with `want`
  function faceN(out,v,mat,b,db,ex,want){
    const a=v[0], p=v[1], c=v[2];
    const u=[p[0]-a[0],p[1]-a[1],p[2]-a[2]], w=[c[0]-a[0],c[1]-a[1],c[2]-a[2]];
    const n=[u[1]*w[2]-u[2]*w[1], u[2]*w[0]-u[0]*w[2], u[0]*w[1]-u[1]*w[0]];
    if(want && (n[0]*want[0]+n[1]*want[1]+n[2]*want[2])<0) v=v.slice().reverse();
    out.push(mk(v,mat,b,db,ex));
  }
  function box(out,c,h,mat,b,db,xf,ex,mats){
    xf=xf||ID;
    const P=(sx,sy,sz)=>xf([c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]]);
    const Q=[ [P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)], [P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)],
              [P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)], [P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)],
              [P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)], [P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)] ];
    Q.forEach((v,i)=>out.push(mk(v,(mats&&mats[i])||mat,b,db,ex)));
  }
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
  function rope(out,pts,r,mat,b,sag,ex){
    for(let i=0;i+1<pts.length;i++){
      const A=pts[i], B=pts[i+1], sg=Array.isArray(sag)?(sag[i]||0):(sag||0);
      if(sg<=0.005){ bar(out,A,B,r,mat,b,-0.10,ex); continue; }
      const N=6; let prev=A;
      for(let k=1;k<=N;k++){ const t=k/N, p=lerp3(A,B,t); p[2]-=4*sg*t*(1-t); bar(out,prev,p,r,mat,b,-0.10,ex); prev=p; }
    }
  }
  function drum(out,c,k,ex){ k=k||1;
    prism(out,[c[0],c[1],c[2]],0.10*k,0.07*k,10,'steel',0.35,-0.02,ex);
    prism(out,[c[0],c[1],c[2]+0.07*k],0.09*k,0.11*k,10,'blk',0.15,-0.02,ex);
    prism(out,[c[0],c[1],c[2]+0.18*k],0.10*k,0.05*k,10,'steel',0.7,-0.03,ex);
  }
  // laid-teak planking, swept with the deck edge: a gelcoat margin, then `perSide` planks from inner(y) out
  function planks(out,y0,y1,nseg,innerFn,o){
    o=o||{}; const ps=o.perSide||5, mg=o.margin==null?0.12:o.margin, ex=o.lid?{lid:1}:null;
    const zF=o.zFn||dZ, eF=o.edgeFn||((y)=>hD(y)-0.006);
    for(let i=0;i<nseg;i++){
      const ya=y0+(y1-y0)*i/nseg, yb=y0+(y1-y0)*(i+1)/nseg;
      const za=zF(ya), zb=zF(yb), ea=eF(ya), eb=eF(yb), ia=innerFn(ya), ib=innerFn(yb);
      const ra=Math.max(ia,ea-mg), rb=Math.max(ib,eb-mg);
      for(const s of [-1,1]){
        if(mg>0) faceN(out,[[s*ra,ya,za],[s*ea,ya,za],[s*eb,yb,zb],[s*rb,yb,zb]],'paint',0.35,0,ex,[0,0,1]);
        for(let k=0;k<ps;k++){
          const xa0=ia+(ra-ia)*k/ps, xa1=ia+(ra-ia)*(k+1)/ps, xb0=ib+(rb-ib)*k/ps, xb1=ib+(rb-ib)*(k+1)/ps;
          if(xa1-xa0<0.01 && xb1-xb0<0.01) continue;
          faceN(out,[[s*xa0,ya,za+0.001],[s*xa1,ya,za+0.001],[s*xb1,yb,zb+0.001],[s*xb0,yb,zb+0.001]],'deck',0.35+(k%2?0.14:-0.10),-0.01,ex,[0,0,1]);
        }
      }
    }
  }

  // ---- paint bands (outer skin), keyed to the LEVEL waterline ---------------------------------------
  const fAtZ=(u,z)=>fracAtZ(station(u),z);
  const fB=(d,u)=>{ const st=station(u); return Math.min(0.996, Math.max(FC+0.05, fracAtZ(st, st.kz+st.dep-d))); };
  const F_BOOT0=(u)=>fAtZ(u,DWL-0.08), F_BOOT1=(u)=>fAtZ(u,ZLIP);
  const F_COVE=(u)=>fB(0.30,u), F_SHEER=(u)=>fB(0.16,u);
  const OB = [ [0, F_BOOT0, 'bottom', -0.2, 0, false],
               [F_BOOT0, F_BOOT1, 'stripe', 0.30, 0.005, false],
               [F_BOOT1, F_COVE, 'paint', 0, 0, true],
               [F_COVE, F_SHEER, 'stripe', 0.35, 0.006, true],
               [F_SHEER, 1, 'paint', 0.10, 0, true] ];
  const fv=(f,u)=>typeof f==='function'?f(u):f;
  const CAB_Y0=RF.yA, CAB_Y1=12.6, CAB_U0=uOf(CAB_Y0), CAB_U1=uOf(CAB_Y1);
  const inCabin=(u0,u1)=>u1>CAB_U0+1e-6 && u0<CAB_U1-1e-6;

  // ---- the static mesh -----------------------------------------------------------------------------
  const F = [];
  (function build(){
    // ---- authoring cursor: every face DECLARES the level it belongs to ---------------------------
    // RigMeshExtractor's contract: a rig that publishes geometry().ids must stamp EVERY face with a
    // key of that table. No default is defensible — the only one would be `hull`, which means NEVER
    // CULL, so a missed stamp ships as a room that quietly stops opening. The cursor rides the build
    // order, so a face declares its level at the point it is emitted and nothing is ever re-derived
    // from geometry. `mark` carries the section's cutaway properties (inside/lid/under) alongside,
    // because those are the RASTERISER's switches and must stay independent of the level.
    // An explicit tag on the face itself always wins — that is how the inner skin declares which of
    // the two accommodation levels it is lining from inside a section that is building hull.
    let LV='hull', MARK=null;
    const lv=(id,mark)=>{ LV=id; MARK=mark||null; };
    F.push=function(){
      for(let i=0;i<arguments.length;i++){
        const f=arguments[i];
        if(f.lv==null) f.lv=LV;
        if(MARK) for(const k in MARK){ if(f[k]==null) f[k]=MARK[k]; }
      }
      return Array.prototype.push.apply(this,arguments);
    };
    const CUT=(u0,u1,extra)=>inCabin(u0,u1)?Object.assign({cut:'cabin'},extra||{}):(extra||null);
    const LID={lid:1}, RIGX={lv:'rig'};
    // ---- hull skin, both sides ----
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        const A=skin(side,u0,1), B=skin(side,u1,1), dx=B[0]-A[0], dy=B[1]-A[1], m=Math.hypot(dx,dy)||1;
        const onx=[ dy/m, -dx/m, 0 ]; if(onx[0]*side<0){ onx[0]=-onx[0]; onx[1]=-onx[1]; }
        for(const [f0,f1,mat,b,db,cuttable] of OB){
          const ex = cuttable ? CUT(u0,u1,{cutN:onx}) : null;
          faceN(F,[skin(side,u0,fv(f0,u0)),skin(side,u1,fv(f0,u1)),skin(side,u1,fv(f1,u1)),skin(side,u0,fv(f1,u0))],mat,b,db,ex,[side,0,0.15]);
        }
        const oa=skin(side,u0,1), ob2=skin(side,u1,1);
        const inb=(p)=>[p[0]-side*TH*0.9,p[1],p[2]];
        const or0=[oa[0],oa[1],oa[2]+0.002], or1=[ob2[0],ob2[1],ob2[2]+0.002];
        const capEx = CUT(u0,u1,{cutN:onx});
        faceN(F,[or0,or1,inb(ob2),inb(oa)],'steel',0.8,0.04,capEx,[0,0,1]);
        faceN(F,[or0,or1,[or1[0],or1[1],or1[2]-0.06],[or0[0],or0[1],or0[2]-0.06]],'blk',0.15,0.04,capEx,[side,0,0]);
        if(side>0) faceN(F,[skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'bottom',-0.9,0,null,[0,0,-1]);
        if(inCabin(u0,u1)){
          const a0=Math.max(u0,CAB_U0), a1=Math.min(u1,CAB_U1);
          const fs0=fAtZ(a0,LOW-0.06), fs1=fAtZ(a1,LOW-0.06), fl0=fAtZ(a0,ZLIP), fl1=fAtZ(a1,ZLIP);
          const fd0=fAtZ(a0,deckZ(a0)-0.04), fd1=fAtZ(a1,deckZ(a1)-0.04);
          // The inner skin runs the whole accommodation, so it declares the ROOM it lines rather
          // than one blanket name: the deck saloon aft of its own forward bulkhead (RF.yF), the
          // lower accommodation forward of it. Both are levels this rig publishes in geometry().ids.
          const CLV=(yOf((a0+a1)/2)<RF.yF)?'saloon':'lower';
          faceN(F,[skin(side,a1,fs1,1),skin(side,a0,fs0,1),skin(side,a0,fl0,1),skin(side,a1,fl1,1)],'cream',-0.6,0,{lv:CLV,inside:1},[-side,0,0]);
          for(let k=0;k<2;k++){
            const g0a=fl0+(fd0-fl0)*k/2, g1a=fl0+(fd0-fl0)*(k+1)/2, g0b=fl1+(fd1-fl1)*k/2, g1b=fl1+(fd1-fl1)*(k+1)/2;
            faceN(F,[skin(side,a1,g0b,1),skin(side,a0,g0a,1),skin(side,a0,g1a,1),skin(side,a1,g1b,1)],'cream',-0.2,0,{lv:CLV,inside:1,cut:'cabin',cutN:onx},[-side,0,0]);
          }
          const L0=skin(side,a0,fl0), L1=skin(side,a1,fl1), I0=skin(side,a0,fl0,1), I1=skin(side,a1,fl1,1);
          const up=(p)=>[p[0],p[1],p[2]+0.05];
          faceN(F,[up(I0),up(I1),up(L1),up(L0)],'cream',1.6,-0.02,{lv:CLV,inside:1,lip:'cabin',cutN:onx},[0,0,1]);
          faceN(F,[up(L0),up(L1),L1,L0],'cream',0.9,-0.02,{lv:CLV,inside:1,lip:'cabin',cutN:onx},[side,0,0]);
        }
      }
      // hull windows: two groups of three slits + two rectangular ports (proud smoked panels)
      const win=(y0,y1,z0,z1)=>{
        const u0=uOf(y0), u1=uOf(y1), o=(p)=>[p[0]+side*0.015,p[1],p[2]];
        faceN(F,[o(skin(side,u0,fAtZ(u0,z1))),o(skin(side,u1,fAtZ(u1,z1))),o(skin(side,u1,fAtZ(u1,z0))),o(skin(side,u0,fAtZ(u0,z0)))],'glas',0.55,0.02,
              (y0>CAB_Y0&&y1<CAB_Y1)?{cut:'cabin',cutN:[side,0,0]}:null,[side,0,0]); };
      for(const y of HULL_WIN.slits) win(y-0.09,y+0.09,HULL_WIN.z0,HULL_WIN.z1);
      for(const [y0,y1] of HULL_WIN.ports) win(y0,y1,2.38,2.66);
    }

    lv('hull');        // transom, quarter pieces and the stair recess: exterior silhouette
    // ---- transom: bands below the platform, quarter pieces above, the stair recess between ----
    (function(){
      const st=station(0), tz=(z)=>fracAtZ(st,z), tp=(s,f)=>skin(s,0,f), sh=st.kz+st.dep;
      for(const [z0,z1,mat,b] of [[st.kz,DWL-0.08,'bottom',-0.35],[DWL-0.08,PLAT,'stripe',0.1]])
        faceN(F,[tp(-1,tz(z0)),tp(1,tz(z0)),tp(1,tz(z1)),tp(-1,tz(z1))],mat,b,0.005,null,[0,-1,0]);
      const xg=STAIR.x;
      for(const s of [-1,1]){
        const xo=(z)=>tp(s,tz(z))[0];
        for(const [z0,z1,mat,b] of [[PLAT,sh-0.30,'paint',-0.45],[sh-0.30,sh-0.16,'stripe',0.1],[sh-0.16,sh,'paint',-0.35]])
          faceN(F,[[s*xg,st.y,z0],[xo(z0),st.y,z0],[xo(z1),st.y,z1],[s*xg,st.y,z1]],mat,b,0.005,null,[0,-1,0]);
        faceN(F,[[s*xg,st.y,PLAT],[s*xg,AFT.y0,PLAT],[s*xg,AFT.y0,dZ(AFT.y0)],[s*xg,st.y,sh]],'liner',-0.7,-0.01,null,[-s,0,0]);
      }
      let zPrev=dZ(AFT.y0);
      for(const [ya,yb,zt] of STAIR.treads){
        box(F,[0,(ya+yb)/2,zt-0.03],[xg-0.01,(ya-yb)/2,0.03],'teak',0.15,-0.01);
        faceN(F,[[-xg+0.01,ya,zt-0.06],[xg-0.01,ya,zt-0.06],[xg-0.01,ya,zPrev],[-xg+0.01,ya,zPrev]],'paint',-0.4,0,null,[0,-1,0]);
        zPrev=zt;
      }
      faceN(F,[[-xg+0.01,STAIR.floorY,PLAT],[xg-0.01,STAIR.floorY,PLAT],[xg-0.01,STAIR.floorY,zPrev-0.06],[-xg+0.01,STAIR.floorY,zPrev-0.06]],'paint',-0.4,0,null,[0,-1,0]);
      box(F,[0,(STAIR.floorY+st.y)/2,PLAT-0.03],[xg-0.01,(STAIR.floorY-st.y)/2,0.03],'teak',0.15,-0.01);
    })();

    lv('aft_deck');    // the planked quarters and the aft deck, back to the stern stairs
    // ---- decks: aft quarters, aft deck, side decks, foredeck (laid teak, gelcoat margins) ----
    planks(F,-13.5,AFT.y0,3,()=>STAIR.x,{perSide:4});
    planks(F,AFT.y0,AFT.y1,6,()=>0,{perSide:5});
    lv('hull');   // side decks and margins are WASHBOARD — hull silhouette, never culled
    planks(F,CK.yH,CK.yA,9,xCoam,{perSide:3});
    planks(F,CK.yA,RF.yNose,11,innerRoof,{perSide:3,lid:true});
    lv('foredeck');    // from the deckhouse nose forward, round the tender well to the stem
    planks(F,RF.yNose,WELL.y0,1,()=>0,{perSide:5,lid:true});
    planks(F,WELL.y0,WELL.y1,5,hxWell,{perSide:3,lid:true});
    planks(F,WELL.y1,CAB_Y1,4,()=>0,{perSide:5,lid:true});
    planks(F,CAB_Y1,13.45,2,()=>0,{perSide:4});
    lv('aft_deck');    // the garage lids and quarter pads sit on it
    // aft-deck garage lids (smoked, steel frames) + quarter seat pads
    for(const s of [-1,1]){
      const hy=0.85, hx=0.95, yc=-9.75, xc=s*1.35, z=dZ(yc);
      faceN(F,[[xc-hx-0.05,yc-hy-0.05,z+0.025],[xc+hx+0.05,yc-hy-0.05,z+0.025],[xc+hx+0.05,yc+hy+0.05,z+0.025],[xc-hx-0.05,yc+hy+0.05,z+0.025]],'steel',0.3,-0.02,null,[0,0,1]);
      faceN(F,[[xc-hx,yc-hy,z+0.04],[xc+hx,yc-hy,z+0.04],[xc+hx,yc+hy,z+0.04],[xc-hx,yc+hy,z+0.04]],'glas',0.85,-0.03,null,[0,0,1]);
      box(F,[s*2.0,-12.55,dZ(-12.55)+0.06],[0.45,0.32,0.06],'uph',0.5,0.012);
    }

    lv('cockpit');     // the well, its settees and both helms
    // ---- cockpit: walkway, helm sole, U-settees, tables, coaming, pedestals, helm seats ----
    (function(){
      const yA=CK.yA, yB=CK.yB, yH=CK.yH, xi=CK.xi;
      const NP=6;
      for(let k=0;k<NP;k++){ const x0=-xi+2*xi*k/NP, x1=-xi+2*xi*(k+1)/NP;
        faceN(F,[[x0,yB,SOLE],[x1,yB,SOLE],[x1,yA,SOLE],[x0,yA,SOLE]],'teak',(k%2?0.05:-0.30),0,null,[0,0,1]); }
      const NH=8;
      for(let i=0;i<NH;i++){ const y0=yH+(yB-yH)*i/NH, y1=yH+(yB-yH)*(i+1)/NH;
        faceN(F,[[-xIn(y0),y0,SOLE],[xIn(y0),y0,SOLE],[xIn(y1),y1,SOLE],[-xIn(y1),y1,SOLE]],'teak',(i%2?0.05:-0.30),0,null,[0,0,1]); }
      // aft-deck riser + the walkway step
      faceN(F,[[-xIn(yH),yH,SOLE],[xIn(yH),yH,SOLE],[xIn(yH),yH,dZ(yH)],[-xIn(yH),yH,dZ(yH)]],'paint',-0.5,0.004,null,[0,1,0]);
      box(F,[0,yH+0.16,(SOLE+STEP1)/2],[xi,0.16,(STEP1-SOLE)/2],'teak',0.1,0,null,null,['teak','teak','paint','paint','paint','paint']);
      for(const s of [-1,1]){
        // coaming in two runs: helm (inner face from the sole) and settee (inner face from the seat)
        for(const [r0,r1,nseg,zb] of [[yH,yB,3,SOLE],[yB,yA,6,CK.seatZ]]){
          for(let i=0;i<nseg;i++){
            const y0=r0+(r1-r0)*i/nseg, y1=r0+(r1-r0)*(i+1)/nseg, xo0=xIn(y0), xo1=xIn(y1), xc0=xCoam(y0), xc1=xCoam(y1);
            faceN(F,[[s*xo0,y0,zb],[s*xo1,y1,zb],[s*xo1,y1,CK.coamZ],[s*xo0,y0,CK.coamZ]],'paint',-0.55,0.005,null,[-s,0,0]);
            faceN(F,[[s*xo0,y0,CK.coamZ],[s*xc0,y0,CK.coamZ],[s*xc1,y1,CK.coamZ],[s*xo1,y1,CK.coamZ]],'paint',0.6,0.01,null,[0,0,1]);
            faceN(F,[[s*xc0,y0,dZ(y0)],[s*xc1,y1,dZ(y1)],[s*xc1,y1,CK.coamZ],[s*xc0,y0,CK.coamZ]],'paint',0.05,0.005,null,[s,0,0]);
          }
        }
        faceN(F,[[s*xIn(yH),yH,dZ(yH)],[s*xCoam(yH),yH,dZ(yH)],[s*xCoam(yH),yH,CK.coamZ],[s*xIn(yH),yH,CK.coamZ]],'paint',-0.3,0.005,null,[0,-1,0]);
        // U-settee: outboard bench (teak top, painted front) + fore/aft returns + cushions
        const bo0=yB+0.1, bo1=yA-0.1, N3=6;
        for(let i=0;i<N3;i++){ const y0=bo0+(bo1-bo0)*i/N3, y1=bo0+(bo1-bo0)*(i+1)/N3, xa0=xIn(y0)-0.5, xa1=xIn(y1)-0.5;
          faceN(F,[[s*xa0,y0,CK.seatZ],[s*xIn(y0),y0,CK.seatZ],[s*xIn(y1),y1,CK.seatZ],[s*xa1,y1,CK.seatZ]],'teak',(i%2?0.15:-0.2),0.01,null,[0,0,1]);
          faceN(F,[[s*xa0,y0,SOLE],[s*xa1,y1,SOLE],[s*xa1,y1,CK.seatZ],[s*xa0,y0,CK.seatZ]],'paint',-0.85,0.005,null,[-s,0,0]); }
        for(const [r0,r1] of [[yB+0.1,yB+0.8],[yA-0.8,yA-0.1]]){
          const xout=xIn((r0+r1)/2)-0.5, xm=s*(xi+xout)/2, hw=(xout-xi)/2;
          box(F,[xm,(r0+r1)/2,(SOLE+CK.seatZ)/2],[hw,(r1-r0)/2,(CK.seatZ-SOLE)/2],'paint',-0.5,0.005,null,null,['teak','paint','paint','paint','paint','paint']);
          box(F,[xm,(r0+r1)/2,CK.seatZ+0.05],[hw-0.03,(r1-r0)/2-0.03,0.05],'uph',0.45,0.012);
        }
        { const ym=(bo0+bo1)/2; box(F,[s*(xIn(ym)-0.25),ym,CK.seatZ+0.05],[0.22,(bo1-bo0)/2-0.03,0.05],'uph',0.45,0.012); }
        box(F,[s*TABLE.x,TABLE.y,TABLE.z],[TABLE.hx,TABLE.hy,0.025],'teak',0.5,-0.01);
        bar(F,[s*TABLE.x,TABLE.y,SOLE],[s*TABLE.x,TABLE.y,TABLE.z-0.02],0.045,'steel',0.4,-0.1);
        // helm station: pedestal, instrument pod (smoked screen aft), compass, helm seat
        box(F,[s*PED.x,PED.y,SOLE+PED.h/2],[PED.hx,PED.hy,PED.h/2],'paint',-0.2,-0.01);
        box(F,[s*PED.x,PED.y+0.02,SOLE+PED.h+0.12],[0.26,0.10,0.12],'blk',0.2,-0.02,null,null,['blk','blk','blk','glas','blk','blk']);
        prism(F,[s*PED.x,PED.y-0.02,SOLE+PED.h+0.24],0.07,0.05,10,'glas',0.9,-0.03);
        box(F,[s*HSEAT.x,HSEAT.y,(SOLE+HSEAT.z)/2],[HSEAT.hx,HSEAT.hy,(HSEAT.z-SOLE)/2],'paint',-0.45,0.005,null,null,['teak','paint','paint','paint','paint','paint']);
        box(F,[s*HSEAT.x,HSEAT.y,HSEAT.z+0.05],[HSEAT.hx-0.03,HSEAT.hy-0.02,0.05],'uph',0.45,0.012);
        // winches: primaries on the coaming, mainsheet winches on the aft deck; jib turning block
        drum(F,[s*WINCH.primaryX(),WINCH.primaryY,CK.coamZ],1.5);
        drum(F,[s*WINCH.mainX,WINCH.mainY,dZ(WINCH.mainY)],1.25);
        box(F,[s*TURN.x(),TURN.y,dZ(TURN.y)+0.05],[0.07,0.09,0.05],'blk',0.2,-0.02);
      }
      // mainsheet traveller across the aft deck
      bar(F,[-TRAV.hx,TRAV.y,dZ(TRAV.y)+0.04],[TRAV.hx,TRAV.y,dZ(TRAV.y)+0.04],[0.035,0.03],'blk',0.1,-0.02);
      for(const s of [-1,1]) box(F,[s*TRAV.hx,TRAV.y,dZ(TRAV.y)+0.06],[0.05,0.05,0.05],'steel',0.5,-0.03);
      box(F,[FURL_CLEAT[0],FURL_CLEAT[1],CK.coamZ+0.025],[0.06,0.05,0.025],'steel',0.55,-0.03);
      for(const c of CLEAT_PTS) box(F,[c.pos[0],c.pos[1],c.pos[2]],[0.03,0.13,0.045],'steel',0.5,-0.02);
      // companionway bulkhead (aft face of the saloon) around the door opening
      const yq=RF.yA, hx=hxRoof(yq), rz=roofZ(yq);
      const wall=(x0,x1,z0,z1,b)=>faceN(F,[[x0,yq,z0],[x1,yq,z0],[x1,yq,z1],[x0,yq,z1]],'paint',b,0.004,{cut:'cabin',cutN:[0,-1,0]},[0,-1,0]);
      wall(-hx,hx,SOLE,DOOR.z0,-0.35);
      wall(-hx,DOOR.x0,DOOR.z0,rz,-0.25); wall(DOOR.x1,hx,DOOR.z0,rz,-0.25); wall(DOOR.x0,DOOR.x1,DOOR.z1,rz,-0.25);
      faceN(F,[[DOOR.x0,yq+0.35,DOOR.z0],[DOOR.x1,yq+0.35,DOOR.z0],[DOOR.x1,yq+0.35,DOOR.z1],[DOOR.x0,yq+0.35,DOOR.z1]],'blk',-0.4,0,{lid:1},[0,-1,0]);
      box(F,[0,yq-0.02,DOOR.z0-0.02],[DOOR.x1+0.04,0.03,0.02],'steel',0.5,-0.02);
    })();

    lv('coachroof');   // the deckhouse STRUCTURE; `saloon` is the room it covers
    // ---- deck saloon ----
    (function(){
      const N=14, yA=RF.yA, yF=RF.yF;
      for(let i=0;i<N;i++){
        const y0=yA+(yF-yA)*i/N, y1=yA+(yF-yA)*(i+1)/N;
        const h0=hxRoof(y0), h1=hxRoof(y1), z0=roofZ(y0), z1=roofZ(y1), d0=dZ(y0), d1=dZ(y1);
        faceN(F,[[-(h0-0.07),y0,z0],[h0-0.07,y0,z0],[h1-0.07,y1,z1],[-(h1-0.07),y1,z1]],'paint',0.55,0,LID,[0,0,1]);
        for(const s of [-1,1]) faceN(F,[[s*h0,y0,d0],[s*h1,y1,d1],[s*(h1-0.07),y1,z1],[s*(h0-0.07),y0,z0]],'paint',-0.15,0,{cut:'cabin',cutN:[s,0,0]},[s,0,0]);
      }
      for(const s of [-1,1]){ const w0=-1.3, w1=3.9, NW=8;
        for(let i=0;i<NW;i++){ const y0=w0+(w1-w0)*i/NW, y1=w0+(w1-w0)*(i+1)/NW;
          const zb0=dZ(y0)+0.42, zb1=dZ(y1)+0.42, zt0=roofZ(y0)-0.20, zt1=roofZ(y1)-0.20;
          const xw=(y,z)=>{ const t=(z-dZ(y))/(roofZ(y)-dZ(y)); return hxRoof(y)-0.07*t+0.014; };
          faceN(F,[[s*xw(y0,zb0),y0,zb0],[s*xw(y1,zb1),y1,zb1],[s*xw(y1,zt1),y1,zt1],[s*xw(y0,zt0),y0,zt0]],'glas',0.6,0.02,{cut:'cabin',cutN:[s,0,0]},[s,0,0]); } }
      const hF=hxRoof(yF)-0.07, zF=roofZ(yF), yN=RF.yNose, zN=dZ(yN), hN=hxRoof(yN)*0.92;
      faceN(F,[[-hF,yF,zF],[hF,yF,zF],[hN,yN,zN],[-hN,yN,zN]],'paint',0.5,0,{cut:'cabin',cutN:[0,1,0]},[0,1,1]);
      faceN(F,[[-(hF-0.35),yF+0.08,zF-0.06],[hF-0.35,yF+0.08,zF-0.06],[hN*0.75,yN-0.35,zN+0.22],[-hN*0.75,yN-0.35,zN+0.22]],'glas',0.7,0.02,{cut:'cabin',cutN:[0,1,0]},[0,1,1]);
      for(const s of [-1,1]) faceN(F,[[s*hF,yF,zF],[s*hN,yN,zN],[s*hxRoof(yN),yN,zN],[s*hxRoof(yF),yF,dZ(yF)]],'paint',-0.1,0,{cut:'cabin',cutN:[s,0.4,0]},[s,0.4,0]);
      for(const [g0,g1] of ROOF_GLASS){ const hxg=0.85;
        faceN(F,[[-hxg-0.05,g0-0.05,roofZ(g0-0.05)+0.03],[hxg+0.05,g0-0.05,roofZ(g0-0.05)+0.03],[hxg+0.05,g1+0.05,roofZ(g1+0.05)+0.03],[-hxg-0.05,g1+0.05,roofZ(g1+0.05)+0.03]],'steel',0.3,-0.02,LID,[0,0,1]);
        faceN(F,[[-hxg,g0,roofZ(g0)+0.045],[hxg,g0,roofZ(g0)+0.045],[hxg,g1,roofZ(g1)+0.045],[-hxg,g1,roofZ(g1)+0.045]],'glas',0.9,-0.03,LID,[0,0,1]); }
      for(const s of [-1,1]){
        drum(F,[s*WINCH.halyardX,WINCH.halyardY,roofZ(WINCH.halyardY)],1.15,LID);
        box(F,[s*WINCH.halyardX,WINCH.clutchY,roofZ(WINCH.clutchY)+0.035],[0.16,0.06,0.035],'blk',0.2,-0.02,null,LID);
        for(let k=0;k<4;k++) box(F,[s*(WINCH.halyardX-0.11+0.073*k),WINCH.clutchY+0.02,roofZ(WINCH.clutchY)+0.1],[0.014,0.024,0.03],'steel',0.6,-0.03,null,LID);
        const tails=[[s*0.22,MAST.y-0.25,roofZ(MAST.y-0.25)+0.035],[s*0.9,1.0,roofZ(1.0)+0.035],[s*WINCH.halyardX,WINCH.clutchY+0.08,roofZ(WINCH.clutchY)+0.035],[s*WINCH.halyardX,WINCH.halyardY+0.14,roofZ(WINCH.halyardY)+0.035]];
        rope(F,tails,0.026,'rope',0.2,0,LID);
      }
      box(F,[0,MAST.y,MAST.footZ+0.05],[0.26,0.30,0.05],'steel',0.4,-0.02,null,LID);
    })();

    lv('foredeck');    // the well, hatches, windlass and ground tackle standing on it
    // ---- foredeck fittings: tender well, hatches, windlass, bow roller + anchor, furler, pads, tracks ----
    (function(){
      const wy0=WELL.y0, wy1=WELL.y1, NWl=5;
      for(let i=0;i<NWl;i++){ const y0=wy0+(wy1-wy0)*i/NWl, y1=wy0+(wy1-wy0)*(i+1)/NWl, z0=dZ(y0)-WELL.depth, z1=dZ(y1)-WELL.depth, w0=hxWell(y0), w1=hxWell(y1);
        for(let k=0;k<4;k++){ const f0=-1+2*k/4, f1=-1+2*(k+1)/4;
          faceN(F,[[w0*f0,y0,z0],[w0*f1,y0,z0],[w1*f1,y1,z1],[w1*f0,y1,z1]],'teak',(k%2?0.1:-0.2),0,LID,[0,0,1]); }
        for(const s of [-1,1]) faceN(F,[[s*w0,y0,z0],[s*w1,y1,z1],[s*w1,y1,dZ(y1)],[s*w0,y0,dZ(y0)]],'paint',-0.6,0,LID,[-s,0,0]);
      }
      faceN(F,[[-hxWell(wy0),wy0,dZ(wy0)-WELL.depth],[hxWell(wy0),wy0,dZ(wy0)-WELL.depth],[hxWell(wy0),wy0,dZ(wy0)],[-hxWell(wy0),wy0,dZ(wy0)]],'paint',-0.5,0,LID,[0,1,0]);
      faceN(F,[[-hxWell(wy1),wy1,dZ(wy1)-WELL.depth],[hxWell(wy1),wy1,dZ(wy1)-WELL.depth],[hxWell(wy1),wy1,dZ(wy1)],[-hxWell(wy1),wy1,dZ(wy1)]],'paint',-0.5,0,LID,[0,-1,0]);
      const rim=[[-hxWell(wy0),wy0],[hxWell(wy0),wy0],[hxWell(wy1),wy1],[-hxWell(wy1),wy1]];
      for(let k=0;k<4;k++){ const a=rim[k], b=rim[(k+1)%4]; bar(F,[a[0],a[1],dZ(a[1])+0.03],[b[0],b[1],dZ(b[1])+0.03],[0.05,0.03],'steel',0.5,-0.02,LID); }
      for(const [hx0,hy0,hs] of HATCHES){ const z=dZ(hy0);
        faceN(F,[[hx0-hs-0.04,hy0-hs-0.04,z+0.03],[hx0+hs+0.04,hy0-hs-0.04,z+0.03],[hx0+hs+0.04,hy0+hs+0.04,z+0.03],[hx0-hs-0.04,hy0+hs+0.04,z+0.03]],'steel',0.3,-0.02,LID,[0,0,1]);
        faceN(F,[[hx0-hs,hy0-hs,z+0.045],[hx0+hs,hy0-hs,z+0.045],[hx0+hs,hy0+hs,z+0.045],[hx0-hs,hy0+hs,z+0.045]],'glas',0.9,-0.03,LID,[0,0,1]); }
      const yw=WINDLASS.y, zw=dZ(yw);
      box(F,[0,yw,zw+0.13],[0.24,0.2,0.13],'blk',0.1,-0.02);
      prism(F,[0,yw+0.02,zw+0.26],0.13,0.12,10,'steel',0.5,-0.03);
      prism(F,[0,yw+0.02,zw+0.38],0.09,0.05,10,'blk',0.2,-0.03);
      const zs=dZ(13.1);
      bar(F,[0,13.1,zs+0.10],[0,14.05,zs+0.07],[0.07,0.055],'steel',0.55,-0.05);
      box(F,[0,13.6,zs+0.14],[0.11,0.36,0.03],'steel',0.75,-0.06);
      bar(F,[0,14.0,zs+0.02],[0,14.0,zs-0.45],0.028,'blk',0.25,-0.05);
      box(F,[0,14.02,zs-0.48],[0.24,0.05,0.05],'blk',0.1,-0.05);
      prism(F,[FORESTAY.foot[0],FORESTAY.foot[1]-0.02,FORESTAY.foot[2]-0.30],0.15,0.30,10,'blk',0.15,-0.02);
      box(F,[0,12.6,dZ(12.6)+0.06],[0.32,0.22,0.06],'uph',0.5,0.012);
      const ty=STAY.trackY, tz=dZ(ty)+0.04, thx=STAY.trackHx, arc=[[-thx,ty-0.12],[-thx/3,ty],[thx/3,ty],[thx,ty-0.12]];
      for(let k=0;k<3;k++) bar(F,[arc[k][0],arc[k][1],tz],[arc[k+1][0],arc[k+1][1],tz],[0.04,0.03],'blk',0.1,-0.02,LID);
      box(F,[0,INNER.foot[1],INNER.foot[2]-0.05],[0.06,0.10,0.05],'steel',0.5,-0.03,null,LID);
      for(const s of [-1,1]){ const x=carX();
        bar(F,[s*x,0.1,dZ(0.1)+0.025],[s*x,2.0,dZ(2.0)+0.025],[0.03,0.025],'blk',0.1,-0.02,LID);
        box(F,[s*x,CAR_Y,dZ(CAR_Y)+0.07],[0.045,0.08,0.04],'steel',0.45,-0.03,null,LID);
        box(F,[s*CHAIN.x,CHAIN.yCap,dZ(CHAIN.yCap)+0.03],[0.04,0.55,0.03],'steel',0.55,-0.02,null,LID); }
    })();

    lv('rig');         // a DEDICATED class, so a cut can never take a spar with the space below it
    // ---- mast, spreaders, standing rigging, stanchions, lifelines, pulpit, pushpits ----
    (function(){
      bar(F,mastAt(MAST.footZ+0.1),mastAt(MAST.headZ),[0.17,0.11],'spar',0.25,-0.10,RIGX);
      box(F,[0,mastY(MAST.headZ),MAST.headZ+0.06],[0.12,0.26,0.06],'blk',0.2,-0.02,null,RIGX);
      bar(F,[0,mastY(MAST.headZ)+0.2,MAST.headZ+0.1],[0,mastY(MAST.headZ)+0.9,MAST.headZ+0.1],0.02,'blk',0.2,-0.05,RIGX);
      const WR=0.03;
      const caps=[[[-CHAIN.x,CHAIN.yCap,dZ(CHAIN.yCap)+0.06]],[[CHAIN.x,CHAIN.yCap,dZ(CHAIN.yCap)+0.06]]];
      SPREADERS.forEach((sp)=>{ const root=mastAt(sp.z), sw=sp.sweep*DEG;
        for(const s of [-1,1]){ const tip=[s*sp.len, root[1]-Math.sin(sw)*sp.len*0.6, sp.z+0.08];
          bar(F,[s*0.1,root[1],sp.z],tip,[0.04,0.055],'spar',0.35,-0.10,RIGX); caps[s>0?1:0].push(tip); } });
      const mh=mastAt(MAST.headZ-0.2);
      for(const s of [-1,1]){
        const cp=caps[s>0?1:0];
        rope(F,cp.concat([[s*0.1,mh[1],mh[2]]]),WR,'wire',0.45,0,RIGX);
        const r1=mastAt(SPREADERS[0].z);
        rope(F,[[s*CHAIN.x,CHAIN.yFwd,dZ(CHAIN.yFwd)+0.06],[s*0.1,r1[1]+0.03,r1[2]-0.08]],WR,'wire',0.35,0,RIGX);
        rope(F,[[s*CHAIN.x,CHAIN.yAft,dZ(CHAIN.yAft)+0.06],[s*0.1,r1[1]-0.03,r1[2]-0.08]],WR,'wire',0.35,0,RIGX);
        for(let k=0;k+1<SPREADERS.length;k++){ const r=mastAt(SPREADERS[k+1].z); rope(F,[cp[k+1],[s*0.1,r[1],r[2]-0.08]],WR*0.8,'wire',0.35,0,RIGX); }
        rope(F,[[s*BACKSTAY.x,BACKSTAY.y,dZ(BACKSTAY.y)+0.06],[s*0.05,mastY(MAST.headZ)-0.14,MAST.headZ-0.05]],WR,'wire',0.4,0,RIGX);
        box(F,[s*BACKSTAY.x,BACKSTAY.y,dZ(BACKSTAY.y)+0.03],[0.035,0.2,0.03],'steel',0.55,-0.02,null,RIGX);
      }
      rope(F,[FORESTAY.foot,FORESTAY.head],WR,'wire',0.45,0,RIGX);
      rope(F,[INNER.foot,INNER.head],WR*0.9,'wire',0.45,0,RIGX);
      const US=[0.045,0.13,0.215,0.30,0.385,0.47,0.555,0.64,0.725,0.81,0.895,0.935], SH=0.72;
      const footX=(u)=>halfAtZ(u,deckZ(u),1)-0.08;
      for(const s of [-1,1]){
        const tops=[], mids=[];
        for(const u of US){ const x=footX(u), y=yOf(u), z=deckZ(u);
          bar(F,[s*x,y,z],[s*(x-0.012),y,z+SH],0.022,'steel',0.25,-0.10,RIGX);
          tops.push([s*(x-0.012),y,z+SH]); mids.push([s*(x-0.006),y,z+SH*0.5]); }
        const yq=-13.2, xq=footX(uOf(yq))-0.03, zq=deckZ(uOf(yq));
        bar(F,[s*xq,yq,zq],[s*xq,yq,zq+SH],0.022,'steel',0.25,-0.10,RIGX);
        bar(F,[s*1.15,yq-0.15,zq],[s*1.15,yq-0.15,zq+SH],0.022,'steel',0.25,-0.10,RIGX);
        rope(F,[tops[0],[s*xq,yq,zq+SH],[s*1.15,yq-0.15,zq+SH]],0.022,'steel',0.6,0,RIGX);
        rope(F,[mids[0],[s*xq,yq,zq+SH*0.5],[s*1.15,yq-0.15,zq+SH*0.5]],0.016,'wire',0.45,0,RIGX);
        rope(F,tops,0.018,'wire',0.6,0,RIGX); rope(F,mids,0.016,'wire',0.45,0,RIGX);
        const yp=12.3, xp=0.6, zp=dZ(yp);
        bar(F,[s*xp,yp,zp],[s*xp,yp,zp+SH],0.022,'steel',0.25,-0.10,RIGX);
        rope(F,[tops[tops.length-1],[s*xp,yp,zp+SH],[s*0.22,13.15,zp+SH-0.02],[0,13.42,zp+SH-0.03]],0.022,'steel',0.6,0,RIGX);
        rope(F,[mids[mids.length-1],[s*xp,yp,zp+SH*0.5],[s*0.22,13.15,zp+SH*0.5]],0.016,'wire',0.45,0,RIGX);
      }
      // furling line: drum -> along the port side deck -> coaming cleat
      const fl=[[-0.10,FORESTAY.foot[1]-0.05,FORESTAY.foot[2]-0.25]];
      for(const y of [11.5,9.0,6.5,4.0,1.5,-1.0,-3.5]) fl.push([-(hD(y)-0.30),y,dZ(y)+0.04]);
      fl.push([FURL_CLEAT[0],FURL_CLEAT[1],FURL_CLEAT[2]]);
      rope(F,fl,0.022,'rope',0.15,0,RIGX);
    })();

    lv('hull',{under:1});   // keel and bulb are exterior silhouette; `under` keeps them off unless asked
    // ---- fin keel + bulb (underbody, optional) ----
    (function(){
      const U={under:1};
      const sec=(yc,c,z,t)=>[[yc+c*0.5,z,0],[yc+c*0.1,z,t],[yc-c*0.35,z,t*0.85],[yc-c*0.5,z,0],[yc-c*0.35,z,-t*0.85],[yc+c*0.1,z,-t]];
      const A=sec(1.2,4.4,0.02,0.24), B=sec(0.9,3.0,-2.65,0.17);
      const P=(q)=>[q[2],q[0],q[1]];
      for(let k=0;k<6;k++){ const k2=(k+1)%6; face(F,[P(A[k]),P(A[k2]),P(B[k2]),P(B[k])],'bottom',-0.3,0,U); }
      const rings=[[-1.4,0.14],[-0.7,0.32],[0.3,0.40],[1.4,0.34],[2.3,0.16]];
      for(let i=0;i+1<rings.length;i++){
        const [y0,r0]=rings[i], [y1,r1]=rings[i+1], n=8, ring=(y,r)=>{ const o=[]; for(let k=0;k<n;k++){ const a=2*Math.PI*k/n; o.push([r*Math.cos(a)*1.1, y, -2.85+r*Math.sin(a)*0.8]); } return o; };
        const R0=ring(y0,r0), R1=ring(y1,r1);
        for(let k=0;k<n;k++){ const k2=(k+1)%n; face(F,[R0[k],R1[k],R1[k2],R0[k2]],'bottom',-0.5,0,U); }
      }
    })();

    lv('saloon',{inside:1});   // the deck saloon, lidded by `coachroof`; `inside` keeps it off the exterior
    // ---- interior (view:'cabin'): deck saloon + lower accommodation forward ----
    (function(){
      const C={inside:1}, CC={inside:1,cut:'cabin'};
      const half=(y,z)=>halfAtZ(uOf(y),z,1)-0.05;
      // a bulkhead shaped to the hull section (+ coachroof above the deck), with an optional door opening
      const bulkhead=(y,z0,zTop,door,mat)=>{
        const hz=(z)=> z<=dZ(y) ? half(y,z) : hxRoof(y)-0.08;
        const zs=[z0, lerp(z0,dZ(y),0.3), lerp(z0,dZ(y),0.6), dZ(y)-0.02, zTop];
        const ex=Object.assign({cutN:[0,-1,0]},CC);
        if(!door){ const pts=[]; zs.forEach(z=>pts.push([hz(z),y,z])); zs.slice().reverse().forEach(z=>pts.push([-hz(z),y,z])); faceN(F,pts,mat||'cream',-0.1,0,ex,[0,-1,0]); return; }
        const [dx,dz0,dz1]=door;
        for(const s of [-1,1]){ const pts=[[s*dx,y,z0]]; zs.forEach(z=>pts.push([s*hz(z),y,z])); pts.push([s*dx,y,zTop]); faceN(F,pts,mat||'cream',-0.1,0,ex,[0,-1,0]); }
        faceN(F,[[-dx,y,dz1],[dx,y,dz1],[dx,y,zTop],[-dx,y,zTop]],mat||'cream',-0.1,0,ex,[0,-1,0]);
        if(dz0>z0+0.01) faceN(F,[[-dx,y,z0],[dx,y,z0],[dx,y,dz0],[-dx,y,dz0]],mat||'cream',-0.1,0,ex,[0,-1,0]);
      };
      const bench=(x0,x1,y0,y1,z0)=>{ const zt=z0+0.42;
        box(F,[(x0+x1)/2,(y0+y1)/2,(z0+zt)/2],[(x1-x0)/2,(y1-y0)/2,(zt-z0)/2],'teak',-0.15,0,null,C);
        box(F,[(x0+x1)/2,(y0+y1)/2,zt+0.06],[(x1-x0)/2-0.03,(y1-y0)/2-0.03,0.06],'uph',0.45,0.01,null,C); };
      const berth=(x0,x1,y0,y1,z0,zt,pillowY)=>{
        box(F,[(x0+x1)/2,(y0+y1)/2,(z0+zt)/2],[(x1-x0)/2,(y1-y0)/2,(zt-z0)/2],'teak',-0.15,0,null,C,['uph','teak','teak','teak','teak','teak']);
        const n=(x1-x0)>1.4?2:1, pw=(x1-x0)/(n*2)-0.08;
        for(let k=0;k<n;k++) box(F,[x0+(x1-x0)*(2*k+1)/(2*n),pillowY,zt+0.05],[pw,0.2,0.05],'cream',0.8,0.01,null,C); };
      // saloon sole (raised, 2.05) + the companionway tread
      const NS3=10, y0s=-1.0, y1s=RF.yF-0.02;
      for(let i=0;i<NS3;i++){ const y0=y0s+(y1s-y0s)*i/NS3, y1=y0s+(y1s-y0s)*(i+1)/NS3;
        faceN(F,[[-0.7,y0,SAL],[1.5,y0,SAL],[1.5,y1,SAL],[-0.7,y1,SAL]],'teak',(i%2?0.35:0.05),0,C,[0,0,1]); }
      box(F,[0,-1.45,(2.34+SAL)/2],[0.55,0.15,(2.34-SAL)/2],'teak',0.1,0,null,C);
      faceN(F,[[-0.55,-1.3,2.344],[0.55,-1.3,2.344],[0.55,-1.27,2.344],[-0.55,-1.27,2.344]],'steel',0.6,-0.02,C,[0,0,1]);
      // port: U-settee + dining table
      bench(-2.45,-1.75,-0.6,3.6,SAL); bench(-1.75,-0.7,3.0,3.6,SAL); bench(-1.75,-0.7,-0.6,0.0,SAL);
      box(F,[-1.3,1.5,SAL+0.72],[0.5,1.1,0.025],'teak',0.5,-0.01,null,C);
      bar(F,[-1.3,1.5,SAL],[-1.3,1.5,SAL+0.70],0.05,'steel',0.4,-0.1,C);
      // starboard: chart table with plotter + helm-style seat, settee, cabinet
      box(F,[1.95,-0.75,SAL+0.40],[0.45,0.45,0.40],'teak',-0.1,0,null,C);
      box(F,[1.95,-0.95,SAL+0.85],[0.30,0.12,0.09],'blk',0.2,-0.02,null,C);
      faceN(F,[[1.68,-1.075,SAL+0.78],[2.22,-1.075,SAL+0.78],[2.22,-1.075,SAL+0.92],[1.68,-1.075,SAL+0.92]],'glas',0.9,-0.03,C,[0,-1,0]);
      bar(F,[1.15,-0.75,SAL],[1.15,-0.75,SAL+0.40],0.04,'steel',0.4,-0.1,C);
      box(F,[1.15,-0.75,SAL+0.45],[0.22,0.22,0.05],'uph',0.5,0.01,null,C);
      bench(1.75,2.45,0.3,3.6,SAL);
      box(F,[2.05,3.95,SAL+0.45],[0.4,0.3,0.45],'teak',-0.1,0,null,C);
      for(const s of [-1,1]) box(F,[s*2.35,1.5,3.2],[0.14,2.1,0.25],'teak',0.15,0,null,C);
      bar(F,[0,mastY(SAL),SAL],[0,mastY(roofZ(MAST.y)),roofZ(MAST.y)-0.02],[0.17,0.11],'spar',0.25,-0.1,C);
      // forward bulkhead + two steps down to the lower passage
      bulkhead(RF.yF,LOW-0.1,roofZ(RF.yF)-0.04,[0.5,1.7,SAL+2.0]);
      for(const [ya,yb,zt] of [[4.3,4.6,1.77],[4.6,4.9,1.49]]) box(F,[0,(ya+yb)/2,(zt+LOW)/2],[0.5,(yb-ya)/2,(zt-LOW)/2],'teak',0.1,0,null,C);
      lv('lower',{inside:1});   // forward of the saloon's bulkhead: the lower passage, galley, VIP and crew,
                                // lidded by `foredeck` rather than by the coachroof
      const NL=12, yl0=4.9, yl1=12.4;
      for(let i=0;i<NL;i++){ const y0=yl0+(yl1-yl0)*i/NL, y1=yl0+(yl1-yl0)*(i+1)/NL;
        faceN(F,[[-0.55,y0,LOW],[0.55,y0,LOW],[0.55,y1,LOW],[-0.55,y1,LOW]],'teak',(i%2?0.35:0.05),0,C,[0,0,1]); }
      // galley port (counter, sink, stove), guest double starboard
      box(F,[-1.45,6.45,(LOW+2.12)/2],[0.7,1.3,(2.12-LOW)/2],'teak',-0.1,0,null,C);
      box(F,[-1.45,5.7,2.125],[0.22,0.18,0.012],'steel',0.5,-0.02,null,C);
      box(F,[-1.45,6.9,2.15],[0.28,0.28,0.03],'blk',0.2,-0.02,null,C);
      for(const [bx,by] of [[-1.58,6.77],[-1.32,6.77],[-1.58,7.03],[-1.32,7.03]]) prism(F,[bx,by,2.18],0.06,0.012,8,'steel',0.7,-0.03,C);
      berth(0.75,2.25,5.4,7.8,LOW,1.75,7.5);
      bulkhead(8.0,LOW-0.1,dZ(8.0)-0.04,[0.5,LOW,LOW+1.95]);
      // VIP: island berth + side cabinets
      berth(-0.9,0.9,8.5,10.6,LOW,1.76,10.35);
      for(const s of [-1,1]) box(F,[s*1.35,9.6,(LOW+1.9)/2],[0.2,1.3,(1.9-LOW)/2],'teak',0.0,0,null,C);
      bulkhead(11.3,LOW-0.1,dZ(11.3)-0.04,[0.45,LOW,LOW+1.9]);
      // crew cabin: a bunk a side on a teak base
      for(const s of [-1,1]){ box(F,[s*0.5,11.95,(LOW+1.62)/2],[0.28,0.5,(1.62-LOW)/2],'teak',-0.15,0,null,C); box(F,[s*0.5,11.95,1.67],[0.26,0.48,0.05],'uph',0.45,0.01,null,C); }
      bulkhead(12.5,LOW-0.1,dZ(12.5)-0.04,null);
    })();
    delete F.push;   // the cursor is an AUTHORING tool; F leaves build() a plain array
  })();


  // ---- pose: apparent wind + sheets -> boom, jib, staysail, fill, luff, heel ------------------------
  function sailPose(o){
    o=o||{};
    let awa=(o.awa==null?45:+o.awa); awa=((awa+180)%360+360)%360-180;
    const aws=Math.max(0,o.aws==null?12:+o.aws);
    const main=clamp01(o.main==null?0.7:+o.main), jib=clamp01(o.jib==null?0.7:+o.jib);
    const a=Math.abs(awa), side = awa>=0 ? -1 : 1;
    const irons = a<25 && aws>0.5;
    const mainLimit=4+82*(1-main), jibLimit=9+76*(1-jib);
    const phase=2*Math.PI*((o.frame||0)%8)/8;
    let boom = Math.min(mainLimit, Math.max(0, a-6));
    let jibA = Math.min(jibLimit, Math.max(0, a-6));
    if(irons){ boom=2*Math.sin(phase); jibA=2.5*Math.sin(phase+1); }
    if(aws<0.5){ boom=Math.min(boom,6); jibA=Math.min(jibA,8); }
    const hoist=clamp01(o.hoist==null?1:+o.hoist);
    if(hoist<=0.02) boom=Math.min(boom,2);
    boom=Math.abs(boom); jibA=Math.abs(jibA);
    const stayA=Math.min(jibA, STAY.maxDeg);
    const aoaM=a-boom, aoaJ=a-jibA, aoaS=a-stayA;
    const wind=clamp01(aws/6);
    const fill=(aoa)=>clamp01((aoa-5)/13)*wind;
    let fillM=fill(aoaM), fillJ=fill(aoaJ), fillS=fill(aoaS);
    if(a>150){ fillJ*=0.4; fillS*=0.4; }
    const furl=clamp01(o.furl==null?0:+o.furl);
    if(furl<0.6) fillS*=0.6;                              // the staysail sits in the genoa's shadow
    const flog=(aoa)=>aws<0.5?0:(irons?1:clamp01((8-aoa)/8));
    const flogM=flog(aoaM), flogJ=flog(aoaJ), flogS=flog(aoaS);
    const stallM=clamp01((aoaM-40)/40), stallJ=clamp01((aoaJ-40)/40), stallS=clamp01((aoaS-40)/40);
    const k = a<100 ? 1 : lerp(1,0.35,(a-100)/80);
    const heelDeg = Math.min(HEEL_MAX, HEEL_K*aws*aws*(0.25+0.75*fillM)*k)*(irons?0.15:1);
    const mode = aws<0.5 ? 'becalmed' : irons ? 'in irons' : (flogM>0.01||flogJ>0.01) ? 'luffing' : (stallM>0.3) ? 'stalled' : 'drawing';
    return { awa, aws, side, boom, boomSigned:side*boom, jibAngle:jibA, jibSigned:side*jibA, stayAngle:stayA, staySigned:side*stayA,
             aoaMain:aoaM, aoaJib:aoaJ, aoaStay:aoaS, fillMain:fillM, fillJib:fillJ, fillStay:fillS, flogMain:flogM, flogJib:flogJ, flogStay:flogS,
             stallMain:stallM, stallJib:stallJ, stallStay:stallS, irons, heel: side*heelDeg, heelDeg, phase, mode, mainSheet:main, jibSheet:jib };
  }
  function poseOf(o){
    o=o||{};
    const sp=sailPose(o);
    const rk = o.rock ? rockMotion(o.frame||0) : {roll:0,pitch:0,heave:0};
    const heel = o.heel===false ? 0 : (o.heelDeg!=null ? +o.heelDeg : sp.heel);
    return { roll:(o.roll||0)+rk.roll+heel, pitch:(o.pitch||0)+rk.pitch, heave:(o.heave||0)+rk.heave, sail:sp };
  }
  const travCarX=(sp)=>sp.side*TRAV.hx*0.9*Math.pow(1-sp.mainSheet,0.7);
  const stayCarX=(sp)=>sp.side*STAY.trackHx*0.95*(sp.stayAngle/STAY.maxDeg);

  // ---- dynamic faces: boom, vang, sails, sheets, door, platform, wheels, rudder, winch handle --------
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
    for(const tb of (P.battens||[])){ const t0=tb-0.006, t1=tb+0.006;
      for(let i=0;i<NS;i++){ const s0=0.04+0.94*i/NS, s1=0.04+0.94*(i+1)/NS;
        out.push({ v:[pt(s0,t0),pt(s1,t0),pt(s1,t1),pt(s0,t1)], mat:'batten', b:0.2, db:0.03, two:true, lv:'rig' }); } }
  }
  function dynamicFaces(o, pose, view){
    const D=[], sp=pose.sail, side=sp.side, RX={lv:'rig'};
    const showSails = o.sails!=null ? !!o.sails : (view!=='cabin');
    const h=clamp01(o.hoist==null?1:+o.hoist), furl=clamp01(o.furl==null?0:+o.furl), sfurl=clamp01(o.sfurl==null?1:+o.sfurl);
    const cover = !!o.cover && h<0.02;
    // ---- boom (Park Avenue), gooseneck, hydraulic vang ----
    const beta=sp.boom*DEG, ub=[side*Math.sin(beta), -Math.cos(beta)];
    const G=GOOSE, E=[G[0]+ub[0]*BOOM_L, G[1]+ub[1]*BOOM_L, G[2]];
    bar(D,G,E,BOOM_SEC,'spar',0.3,-0.10,RX);
    box(D,[G[0],G[1]+0.1,G[2]],[0.14,0.16,0.22],'blk',0.2,-0.02,null,RX);
    const xfB=(p)=>rotZabout(p,G,side*beta);
    bar(D,[0,mastY(MAST.footZ+0.55),MAST.footZ+0.55],xfB([0,G[1]-BOOM_L*0.27,G[2]-BOOM_SEC[1]-0.02]),0.07,'spar',0.2,-0.08,RX);
    if(showSails){
      if(h<0.98){ const ph=0.05+0.32*(1-h);
        box(D,[G[0],G[1]-BOOM_L*0.50,G[2]+BOOM_SEC[1]+ph],[BOOM_SEC[0]-0.03,BOOM_L*0.46,ph],cover?'canvas':'sail',0.25,-0.01,xfB,RX); }
      const lj=mastAt(MAST.footZ+13.5);
      for(const s of [-1,1]) for(const d of [2.6,5.6,8.6]){ const p=xfB([G[0]+s*(BOOM_SEC[0]-0.05),G[1]-d,G[2]+BOOM_SEC[1]+0.1]); rope(D,[[s*0.1,lj[1],lj[2]],p],0.016,'rope',0.3,0,RX); }
    }
    // ---- mainsail ----
    if(showSails && h>0.02){
      const P=MAIN.P*h, twist=(4+8*(1-sp.mainSheet))*DEG;
      const chord=(t)=>MAIN.E*((1-t)*0.91+0.09)+MAIN.roach*Math.sin(Math.PI*Math.pow(t,0.85));
      const udir=(t)=>{ const a=beta+twist*t; return [side*Math.sin(a),-Math.cos(a)]; };
      sailMesh(D,{ luff:(t)=>{ const z=G[2]+BOOM_SEC[1]+0.1+P*t; return [0,mastY(z)-0.14,z]; },
        chord, udir, lee:(t)=>{ const u=udir(t); return [-u[1]*side,u[0]*side]; },
        depth:(t)=>sp.fillMain*(0.085+0.05*(1-sp.mainSheet))*chord(t)*(1-0.25*t)*(1-0.4*sp.stallMain),
        flog:sp.flogMain, amp:0.5, phase:sp.phase, irons:sp.irons, mat:'sail', NT:10, NS:7, battens:MAIN.battens });
    }
    // ---- mainsheet: boom end -> traveller car -> track end -> leeward mainsheet winch ----
    const aftZ=dZ(TRAV.y), car=[travCarX(sp),TRAV.y,aftZ+0.09];
    const slackM = 0.05+0.25*(1-sp.fillMain);
    rope(D,[[E[0],E[1]+0.12*Math.cos(beta),E[2]-BOOM_SEC[1]],car],0.03,'rope',0.35,slackM*0.4,RX);
    box(D,[car[0],car[1],aftZ+0.08],[0.07,0.06,0.04],'steel',0.5,-0.03,null,RX);
    rope(D,[car,[side*TRAV.hx,TRAV.y,aftZ+0.08],[side*WINCH.mainX,WINCH.mainY,dZ(WINCH.mainY)+0.2]],0.026,'rope',0.25,0,RX);
    // ---- genoa ----
    const Tk=JIB.tack, Hd=JIB.head;
    let clew=null;
    if(showSails && furl<0.97){
      const g=sp.jibAngle*DEG, twistJ=(6+10*(1-sp.jibSheet))*DEG, LP=JIB.LP*(1-furl);
      const uj0=[side*Math.sin(g),-Math.cos(g)];
      clew=[Tk[0]+uj0[0]*LP*0.97, Tk[1]+uj0[1]*LP*0.97, Tk[2]+JIB.clewUp+1.2*furl];
      const udir=(t)=>{ const a=g+twistJ*t; return [side*Math.sin(a),-Math.cos(a)]; };
      const chord=(t)=>LP*(1-t)*0.97;
      sailMesh(D,{ luff:(t)=>lerp3(Tk,Hd,t), chord, udir, lee:(t)=>{ const u=udir(t); return [-u[1]*side,u[0]*side]; },
        depth:(t)=>sp.fillJib*0.11*chord(t)*(1-0.2*t)*(1-0.4*sp.stallJib), dz:(t)=>(clew[2]-Tk[2])*(1-t),
        flog:sp.flogJib, amp:0.42, phase:sp.phase+0.9, irons:sp.irons, mat:'sail', NT:9, NS:6 });
    }
    if(showSails){
      const rr = furl>0.15 ? 0.05+0.13*furl : 0.04;
      bar(D,[Tk[0],Tk[1],Tk[2]-0.1],[Hd[0],Hd[1],Hd[2]+0.15],rr,furl>0.15?'canvas':'blk',0.2,-0.05,RX);
      if(!clew) clew=[0,Tk[1]-0.1,Tk[2]+1.4];
    }
    if(clew){
      const cxr=carX(), zc=dZ(CAR_Y)+0.11;
      for(const s of [-1,1]){
        const carJ=[s*cxr,CAR_Y,zc], turn=[s*TURN.x(),TURN.y,dZ(TURN.y)+0.1], wn=[s*WINCH.primaryX(),WINCH.primaryY,CK.coamZ+0.30];
        const working = (s===side) && furl<0.97;
        const sag = working ? 0 : (furl>=0.97 ? 0.6 : 0.9);
        rope(D,[clew,carJ],0.03,'rope',working?0.35:0.2,sag,RX);
        rope(D,[carJ,turn,wn],0.03,'rope',0.3,working?0:0.08,RX);
      }
    }
    // ---- staysail on the inner forestay, self-tacking ----
    const Ts=STAY.tack, Hs=STAY.head;
    let sclew=null;
    if(showSails && sfurl<0.97){
      const g=sp.stayAngle*DEG, LP=STAY.LP*(1-sfurl), twistS=8*DEG;
      const u0=[side*Math.sin(g),-Math.cos(g)];
      sclew=[Ts[0]+u0[0]*LP*0.97, Ts[1]+u0[1]*LP*0.97, Ts[2]+STAY.clewUp+0.8*sfurl];
      const udir=(t)=>{ const a=g+twistS*t; return [side*Math.sin(a),-Math.cos(a)]; };
      const chord=(t)=>LP*(1-t)*0.97;
      sailMesh(D,{ luff:(t)=>lerp3(Ts,Hs,t), chord, udir, lee:(t)=>{ const u=udir(t); return [-u[1]*side,u[0]*side]; },
        depth:(t)=>sp.fillStay*0.10*chord(t)*(1-0.2*t)*(1-0.4*sp.stallStay), dz:(t)=>(sclew[2]-Ts[2])*(1-t),
        flog:sp.flogStay, amp:0.3, phase:sp.phase+1.7, irons:sp.irons, mat:'sail', NT:7, NS:5 });
    }
    if(showSails){
      const rr = sfurl>0.15 ? 0.04+0.09*sfurl : 0.032;
      bar(D,[Ts[0],Ts[1],Ts[2]-0.08],[Hs[0],Hs[1],Hs[2]+0.12],rr,sfurl>0.15?'canvas':'blk',0.2,-0.05,RX);
      const scar=[stayCarX(sp),STAY.trackY-0.12*Math.pow(stayCarX(sp)/STAY.trackHx,2),dZ(STAY.trackY)+0.1];
      box(D,[scar[0],scar[1],scar[2]-0.02],[0.06,0.06,0.035],'steel',0.5,-0.03,null,RX);
      if(sclew) rope(D,[sclew,scar],0.026,'rope',0.3,0,RX);
    }
    // ---- winch handle when grinding ----
    if(o.grind){
      const c = o.grind==='main' ? [side*WINCH.mainX,WINCH.mainY,dZ(WINCH.mainY)+0.30] : [side*WINCH.primaryX(),WINCH.primaryY,CK.coamZ+0.36];
      const a=((o.frame||0)%8)*Math.PI/4, tip=[c[0]+Math.cos(a)*0.26,c[1]+Math.sin(a)*0.26,c[2]+0.02];
      bar(D,c,tip,0.014,'blk',0.3,-0.06,RX); bar(D,tip,[tip[0],tip[1],tip[2]+0.09],0.02,'steel',0.5,-0.06,RX);
    }
    // ---- companionway: sliding glass door (to port) ----
    const dt=clamp01(o.doorOpen==null?0:+o.doorOpen);
    (function(){
      const xc=(DOOR.x0+DOOR.x1)/2 - DOOR.travel*dt, y=DOOR.y-0.06, zc=(DOOR.z0+DOOR.z1)/2, hz=(DOOR.z1-DOOR.z0)/2;
      box(D,[xc,y,zc],[DOOR.leaf.w/2,DOOR.leaf.th/2,hz],'glas',0.6,0.02,null,null,['steel','steel','glas','glas','steel','steel']);
      box(D,[xc,y,DOOR.z1-0.02],[DOOR.leaf.w/2,DOOR.leaf.th/2+0.005,0.04],'steel',0.5,0.01);
      box(D,[xc,y,DOOR.z0+0.03],[DOOR.leaf.w/2,DOOR.leaf.th/2+0.005,0.04],'steel',0.5,0.01);
      box(D,[xc+DOOR.leaf.w/2-0.05,y-0.04,zc-0.2],[0.015,0.015,0.14],'steel',0.6,-0.02);
    })();
    // ---- fold-down transom platform (down) or the transom door (folded) ----
    const pf=clamp01(o.platform==null?1:+o.platform);
    if(pf>=0.5){
      box(D,[0,(PLATFORM.y0+PLATFORM.y1)/2,PLATFORM.z-PLATFORM.th/2],[PLATFORM.hx,(PLATFORM.y0-PLATFORM.y1)/2,PLATFORM.th/2],'paint',-0.2,-0.01,null,null,['teak','paint','paint','paint','paint','paint']);
    } else {
      const st=station(0), sh=st.kz+st.dep;
      faceN(D,[[-STAIR.x,st.y-0.01,PLAT],[STAIR.x,st.y-0.01,PLAT],[STAIR.x,st.y-0.01,sh-0.02],[-STAIR.x,st.y-0.01,sh-0.02]],'paint',-0.4,0.004,null,[0,-1,0]);
      faceN(D,[[-STAIR.x,st.y-0.012,sh-0.30],[STAIR.x,st.y-0.012,sh-0.30],[STAIR.x,st.y-0.012,sh-0.16],[-STAIR.x,st.y-0.012,sh-0.16]],'stripe',0.1,0.004,null,[0,-1,0]);
    }
    // ---- wheels ----
    if(o.wheel!==false) D.push.apply(D, wheelFaces(wheelDeg(o)));
    // ---- rudder (underbody) ----
    if(o.underbody){
      const st=Math.max(-1,Math.min(1,o.steer||0))*35*DEG, y0=-11.2, zr=0.85, zt=-1.9;
      const q=(y,z)=>rotZabout([0,y,z],[0,y0,0],-st);
      const c=(z)=>lerp(1.5,0.9,(zr-z)/(zr-zt));
      const nz=5; for(let k=0;k<nz;k++){ const za=lerp(zr,zt,k/nz), zb=lerp(zr,zt,(k+1)/nz);
        for(const s of [-1,1]){
          const t=0.05; const A=q(y0,za),B=q(y0-c(za),za),C2=q(y0-c(zb),zb),Dd=q(y0,zb);
          const off=(p)=>[p[0]+s*t,p[1],p[2]];
          const f=[off(A),off(B),off(C2),off(Dd)]; face(D,s>0?f:f.slice().reverse(),'bottom',-0.3,0,{under:1});
        } }
    }
    return D;
  }

  // ---- twin wheels: real geometry at both pedestals, tag 1 for the masked bake ----------------------
  function wheelFaces(deg){
    const G=WHEEL_GEO, r=G.rake*DEG, out=[];
    for(const HUB of WHEEL_HUBS){
      const P0=[HUB.x, HUB.y, HUB.z];
      const n=[0,-Math.cos(r),Math.sin(r)];
      const e1=[1,0,0], e2=[0,Math.sin(r),Math.cos(r)];
      const at=(rho,th,off)=>[ P0[0]+rho*(Math.cos(th)*e1[0]+Math.sin(th)*e2[0])+n[0]*off,
                               P0[1]+rho*(Math.cos(th)*e1[1]+Math.sin(th)*e2[1])+n[1]*off,
                               P0[2]+rho*(Math.cos(th)*e1[2]+Math.sin(th)*e2[2])+n[2]*off ];
      const push=(v,mat,b,db)=>out.push({v,mat,b:b||0,db:db==null?0.30:db,tag:1});
      const th0=(deg||0)*DEG + Math.PI/2;
      for(let k=0;k<G.seg;k++){
        const a0=th0+2*Math.PI*k/G.seg, a1=th0+2*Math.PI*(k+1)/G.seg;
        push([at(G.rimIn,a0,0.018),at(G.rad,a0,0.018),at(G.rad,a1,0.018),at(G.rimIn,a1,0.018)],'blk',0.55);
        push([at(G.rad,a0,0.018),at(G.rad,a0,-0.014),at(G.rad,a1,-0.014),at(G.rad,a1,0.018)],'blk',-0.35);
      }
      for(let k=0;k<G.spokes;k++){
        const a=th0+2*Math.PI*k/G.spokes, w=0.016;
        const ta=a+Math.PI/2, wx=w*(Math.cos(ta)*e1[0]+Math.sin(ta)*e2[0]), wy=w*(Math.cos(ta)*e1[1]+Math.sin(ta)*e2[1]), wz=w*(Math.cos(ta)*e1[2]+Math.sin(ta)*e2[2]);
        const A=at(G.hubR*0.8,a,0.012), B=at(G.rimIn+0.004,a,0.012);
        const off=(p,s)=>[p[0]+wx*s,p[1]+wy*s,p[2]+wz*s];
        push([off(A,1),off(B,1),off(B,-1),off(A,-1)], k===0?'stripe':'steel', k===0?0.9:0.55);
      }
      for(let k=0;k<8;k++){ const a0=th0+2*Math.PI*k/8, a1=th0+2*Math.PI*(k+1)/8; push([P0,at(G.hubR,a0,0.022),at(G.hubR,a1,0.022),P0],'steel',0.8); }
    }
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
  function gate(f, view, opts){
    // The three cutaway switches are face PROPERTIES, never the level tag: `lv` is the mesh
    // vocabulary (a key of geometry().ids) and RigMeshExtractor reads it, so a level must never be
    // able to move a pixel. `inside` = accommodation, drawn only in the cabin view; `lid` = lifted
    // when the cabin opens; `under` = the underbody, drawn only when asked.
    if(f.inside && view!=='cabin') return false;
    if(f.lid && view==='cabin') return false;
    if(f.under && !opts.underbody) return false;
    return true;
  }
  function _paint(faces, opts, doEdge, G){
    const PW=G?G.W:W, PH=G?G.H:H;
    const B=camBasis(opts), view=opts.view||'exterior';
    const pal=palette(opts), MATS=pal.mats, RINDEX=pal.rindex;
    const zbuf=new Float32Array(PW*PH).fill(Infinity);
    const col=new Array(PW*PH).fill(null);
    const dep=new Float32Array(PW*PH);
    const tag=new Int8Array(PW*PH);
    for(const f of faces){
      if(!gate(f,view,opts)) continue;
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
  // projected extent of every drawn vertex, relative to the pivot — the cell-fit sweep reads this
  function bounds(dir, opts){
    const r=allFaces(dir, opts), B=camBasis(r.o), view=r.o.view||'exterior';
    let x0=1e9,x1=-1e9,y0=1e9,y1=-1e9;
    for(const f of r.faces){ if(!gate(f,view,r.o)) continue;
      for(const v of f.v){ const p=projVert(v[0],v[1],v[2],B); if(p.sx<x0)x0=p.sx; if(p.sx>x1)x1=p.sx; if(p.sy<y0)y0=p.sy; if(p.sy>y1)y1=p.sy; } }
    return { left:cx-x0, right:x1-cx, up:cy-y0, down:y1-cy };
  }

  // ---- anchors -----------------------------------------------------------------------------------
  const ANCHOR_PTS = {
    helmPort:{x:-2.0,y:-7.5,z:SOLE}, helmStbd:{x:2.0,y:-7.5,z:SOLE}, wheelHubPort:WHEEL_HUBS[0], wheelHubStbd:WHEEL_HUBS[1],
    threshold:{x:0,y:DOOR.y,z:DOOR.z0}, mastBase:{x:0,y:MAST.y,z:MAST.footZ}, bowRoller:{x:0,y:14.0,z:dZ(13.1)+0.1},
    windlass:{x:0,y:WINDLASS.y,z:dZ(WINDLASS.y)+0.26}, swimPlatform:{x:0,y:-14.05,z:PLAT}, stairTop:{x:0,y:AFT.y0,z:dZ(AFT.y0)},
    winchPrimaryPort:{x:-WINCH.primaryX(),y:WINCH.primaryY,z:CK.coamZ+0.35}, winchPrimaryStbd:{x:WINCH.primaryX(),y:WINCH.primaryY,z:CK.coamZ+0.35},
    winchMainPort:{x:-WINCH.mainX,y:WINCH.mainY,z:dZ(WINCH.mainY)+0.29}, winchMainStbd:{x:WINCH.mainX,y:WINCH.mainY,z:dZ(WINCH.mainY)+0.29},
    halyardWinchPort:{x:-WINCH.halyardX,y:WINCH.halyardY,z:roofZ(WINCH.halyardY)+0.27}, halyardWinchStbd:{x:WINCH.halyardX,y:WINCH.halyardY,z:roofZ(WINCH.halyardY)+0.27},
    furlerCleat:{x:FURL_CLEAT[0],y:FURL_CLEAT[1],z:FURL_CLEAT[2]}, tablePort:{x:-TABLE.x,y:TABLE.y,z:TABLE.z}, tableStbd:{x:TABLE.x,y:TABLE.y,z:TABLE.z},
    seatPort:{x:-(2.2),y:-3.75,z:CK.seatZ}, seatStbd:{x:2.2,y:-3.75,z:CK.seatZ}, helmSeatPort:{x:-HSEAT.x,y:HSEAT.y,z:HSEAT.z}, helmSeatStbd:{x:HSEAT.x,y:HSEAT.y,z:HSEAT.z},
    bowPad:{x:0,y:12.6,z:dZ(12.6)+0.12}, quarterPadPort:{x:-2.0,y:-12.55,z:dZ(-12.55)+0.12}, quarterPadStbd:{x:2.0,y:-12.55,z:dZ(-12.55)+0.12},
    tenderWell:{x:0,y:(WELL.y0+WELL.y1)/2,z:dZ((WELL.y0+WELL.y1)/2)-WELL.depth},
    goose:{x:GOOSE[0],y:GOOSE[1],z:GOOSE[2]}, masthead:{x:0,y:mastY(MAST.headZ),z:MAST.headZ},
  };
  function anchors(dir, opts){
    opts=Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const pose=poseOf(opts), B=camBasis(Object.assign({},opts,{roll:pose.roll,pitch:pose.pitch,heave:pose.heave}));
    const out={};
    for(const k in ANCHOR_PTS){ const P=ANCHOR_PTS[k], p=projVert(P.x,P.y,P.z,B); out[k]={x:p.sx,y:p.sy}; }
    const sp=pose.sail, beta=sp.boom*DEG, ub=[sp.side*Math.sin(beta),-Math.cos(beta)];
    const E=[GOOSE[0]+ub[0]*BOOM_L, GOOSE[1]+ub[1]*BOOM_L, GOOSE[2]]; const pe=projVert(E[0],E[1],E[2],B); out.boomEnd={x:pe.sx,y:pe.sy};
    const tc=projVert(travCarX(sp),TRAV.y,dZ(TRAV.y)+0.09,B); out.travellerCar={x:tc.sx,y:tc.sy};
    const sc=projVert(stayCarX(sp),STAY.trackY,dZ(STAY.trackY)+0.1,B); out.stayCar={x:sc.sx,y:sc.sy};
    out.cleats=CLEAT_PTS.map(c=>{ const p=projVert(c.pos[0],c.pos[1],c.pos[2],B); return {id:c.id,x:p.sx,y:p.sy}; });
    out.pose=pose; return out;
  }
  function doorMount(dir, opts){
    opts=Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const t=clamp01(opts.doorOpen==null?0:+opts.doorOpen);
    const pose=poseOf(opts), B=camBasis(Object.assign({},opts,{roll:pose.roll,pitch:pose.pitch,heave:pose.heave}));
    const thr=projVert(0,DOOR.y,DOOR.z0,B);
    const edge=projVert(DOOR.x1-DOOR.travel*t,DOOR.y-0.06,DOOR.z0,B);
    return { threshold:{x:thr.sx,y:thr.sy}, edge:{x:edge.sx,y:edge.sy}, open:t, clear:t>=DOOR.clearAt, clearWidth:Math.max(0,Math.min(DOOR.x1-DOOR.x0, DOOR.travel*t)) };
  }


  // ---- published geometry + gameplay sidecar generator --------------------------------------------
  const LEVEL_IDS = { hull:0, cockpit:1, aft_deck:2, coachroof:3, foredeck:4, saloon:5, lower:6, rig:7 };
  const r3=(v)=>+(+v).toFixed(3);
  function geometry(){
    return { schema:'hidden-harbours/hull-geometry@1', hull:'sloop88IsoRig', units:'m',
      frame:'+x stbd, +y bow, +z up; origin amidships, canoe-body bottom, centreline', ids:Object.assign({},LEVEL_IDS),
      levels:[
        { id:'cockpit', deck:'cockpit_sole', soleZ:SOLE, ceilingZ:null, ceiling:{ kind:'open', lid:null, partial:{ z:r3(GOOSE[2]-BOOM_SEC[1]), y0:r3(GOOSE[1]-BOOM_L), y1:r3(GOOSE[1]), of:'boom underside over the centreline — '+r3(GOOSE[2]-BOOM_SEC[1]-SOLE)+' m over the cockpit sole, '+r3(GOOSE[2]-BOOM_SEC[1]-dZ(TRAV.y))+' m over the aft deck; it swings' } } },
        { id:'aft_deck', deck:'aft_deck', soleZ:r3(dZ(-9.75)), ceilingZ:null, ceiling:{ kind:'open', lid:null } },
        { id:'coachroof', deck:'coachroof', soleZ:r3(roofZ(RF.yA)), ceilingZ:null, sole:{ kind:'raked', zAft:r3(roofZ(RF.yA)), zFwd:r3(roofZ(RF.yF)) }, ceiling:{ kind:'open', lid:null } },
        { id:'foredeck', deck:'foredeck', soleZ:r3(dZ(RF.yNose)), ceilingZ:null, sole:{ kind:'raked', zAft:r3(dZ(RF.yNose)), zFwd:r3(dZ(13.4)), follows:'sheer - 0.03' }, ceiling:{ kind:'open', lid:null } },
        { id:'saloon', deck:'saloon_sole', soleZ:SAL, ceilingZ:r3(roofZ(1.3)-0.05), ceiling:{ kind:'raked', lid:'coachroof', zAft:r3(roofZ(RF.yA)-0.05), zFwd:r3(roofZ(RF.yF)-0.05), y0:RF.yA, y1:RF.yF, of:'coachroof underside' } },
        { id:'lower', deck:'lower_sole', soleZ:LOW, ceilingZ:r3(dZ(8.5)-0.05), ceiling:{ kind:'raked', lid:'foredeck', zAft:r3(dZ(4.9)-0.05), zFwd:r3(dZ(12.4)-0.05), y0:4.9, y1:12.5, of:'foredeck underside' } },
      ] };
  }
  function gameplayGeometry(){
    const P3=(p)=>[r3(p.x),r3(p.y),r3(p.z)];
    const NOTE_CONV='Footprints and heights above the named sole plane. Game-side colliders sourced from these notes — never authored holes in the polygon (2026-07-22 ruling).';
    // cockpit sole: the walkway between the two U-settees, widening into the full-width helm area
    const poly=[]; const N=4;
    poly.push([-CK.xi,CK.yA]); poly.push([-CK.xi,CK.yB]);
    for(let i=0;i<=N;i++){ const y=CK.yB+(CK.yH-CK.yB)*i/N; poly.push([-r3(xIn(y)),r3(y)]); }
    for(let i=N;i>=0;i--){ const y=CK.yB+(CK.yH-CK.yB)*i/N; poly.push([r3(xIn(y)),r3(y)]); }
    poly.push([CK.xi,CK.yB]); poly.push([CK.xi,CK.yA]);
    const cockpit={ id:'cockpit_sole', z:SOLE, winding:'ccw_from_above', polygon:poly,
      note:'Teak-planked sole: a 1.10 m walkway between the two U-settees from the saloon door (y='+CK.yA+') to the settee ends at y='+CK.yB+', then the full-width helm area between the coaming inner faces to the aft-deck riser at y='+CK.yH+'. One 0.36 m step (x ±'+CK.xi+', y '+r3(CK.yH)+'..'+r3(CK.yH+0.32)+') then a second up onto the aft deck.',
      _notes:[
        { id:'pedestal_port', kind:'obstruction', type:'console', footprint:{x:[-PED.x-PED.hx,-PED.x+PED.hx],y:[PED.y-PED.hy,PED.y+PED.hy]}, height_above_floor_m:{top:r3(PED.h+0.29)}, treatment:'wall', provenance:'exact, PED; instrument pod + compass on top' },
        { id:'pedestal_stbd', kind:'obstruction', type:'console', footprint:{x:[PED.x-PED.hx,PED.x+PED.hx],y:[PED.y-PED.hy,PED.y+PED.hy]}, height_above_floor_m:{top:r3(PED.h+0.29)}, treatment:'wall', provenance:'mirror' },
        { id:'wheel_port', kind:'obstruction', type:'wheel', footprint:{x:[-WHEEL_HUBS[0].x-WHEEL_GEO.rad,-WHEEL_HUBS[0].x+WHEEL_GEO.rad].sort((a,b)=>a-b),y:[WHEEL_HUBS[0].y-0.05,WHEEL_HUBS[0].y+0.05]}, height_above_floor_m:{top:r3(WHEEL_HUBS[0].z+WHEEL_GEO.rad-SOLE),underside:r3(WHEEL_HUBS[0].z-WHEEL_GEO.rad-SOLE)}, treatment:'waist_block', provenance:'exact, WHEEL_HUBS[0] + WHEEL_GEO.rad; the helm stands aft of it at ANCHORS.helmPort' },
        { id:'wheel_stbd', kind:'obstruction', type:'wheel', footprint:{x:[WHEEL_HUBS[1].x-WHEEL_GEO.rad,WHEEL_HUBS[1].x+WHEEL_GEO.rad],y:[WHEEL_HUBS[1].y-0.05,WHEEL_HUBS[1].y+0.05]}, height_above_floor_m:{top:r3(WHEEL_HUBS[1].z+WHEEL_GEO.rad-SOLE),underside:r3(WHEEL_HUBS[1].z-WHEEL_GEO.rad-SOLE)}, treatment:'waist_block', provenance:'mirror; ANCHORS.helmStbd' },
        { id:'helm_seat_port', kind:'obstruction', type:'seat', footprint:{x:[-HSEAT.x-HSEAT.hx,-HSEAT.x+HSEAT.hx],y:[HSEAT.y-HSEAT.hy,HSEAT.y+HSEAT.hy]}, height_above_floor_m:{top:r3(HSEAT.z+0.1-SOLE)}, treatment:'step_over', provenance:'exact, HSEAT' },
        { id:'helm_seat_stbd', kind:'obstruction', type:'seat', footprint:{x:[HSEAT.x-HSEAT.hx,HSEAT.x+HSEAT.hx],y:[HSEAT.y-HSEAT.hy,HSEAT.y+HSEAT.hy]}, height_above_floor_m:{top:r3(HSEAT.z+0.1-SOLE)}, treatment:'step_over', provenance:'mirror' },
        { id:'settee_port', kind:'obstruction', type:'seat', footprint:{x:[-r3(xIn(-3.75)),-CK.xi],y:[CK.yB+0.1,CK.yA-0.1]}, height_above_floor_m:{top:r3(CK.seatZ+0.1-SOLE)}, treatment:'step_over', provenance:'exact — U: outboard bench 0.50 deep + returns y '+r3(CK.yB+0.1)+'..'+r3(CK.yB+0.8)+' and '+r3(CK.yA-0.8)+'..'+r3(CK.yA-0.1)+'; the table sits in the U (see table_port)' },
        { id:'settee_stbd', kind:'obstruction', type:'seat', footprint:{x:[CK.xi,r3(xIn(-3.75))],y:[CK.yB+0.1,CK.yA-0.1]}, height_above_floor_m:{top:r3(CK.seatZ+0.1-SOLE)}, treatment:'step_over', provenance:'mirror of settee_port' },
        { id:'table_port', kind:'obstruction', type:'table', footprint:{x:[-TABLE.x-TABLE.hx,-TABLE.x+TABLE.hx],y:[TABLE.y-TABLE.hy,TABLE.y+TABLE.hy]}, height_above_floor_m:{top:r3(TABLE.z-SOLE)}, treatment:'waist_block', provenance:'exact, TABLE' },
        { id:'table_stbd', kind:'obstruction', type:'table', footprint:{x:[TABLE.x-TABLE.hx,TABLE.x+TABLE.hx],y:[TABLE.y-TABLE.hy,TABLE.y+TABLE.hy]}, height_above_floor_m:{top:r3(TABLE.z-SOLE)}, treatment:'waist_block', provenance:'mirror' },
        { id:'boom', kind:'overhead', type:'spar', footprint:{x:'swings 0..86° either side about the gooseneck',y:[r3(GOOSE[1]-BOOM_L),r3(GOOSE[1])]}, height_above_floor_m:{underside:r3(GOOSE[2]-BOOM_SEC[1]-SOLE)}, treatment:'overhead', provenance:'exact, GOOSE + BOOM_L + BOOM_SEC — '+r3(GOOSE[2]-BOOM_SEC[1]-SOLE)+' m over the sole: clear' },
      ],
      _notes_convention:NOTE_CONV,
      access:['saloon sliding door (THRESHOLD) forward','walkway step + aft-deck riser aft (STAIRS.helm_to_aft_deck)','either settee up over the coaming onto the side decks'] };
    const aftPoly=[]; const NA=4;
    for(let i=0;i<=NA;i++){ const y=AFT.y1+(AFT.y0-AFT.y1)*i/NA; aftPoly.push([-r3(hD(y)-0.02),r3(y)]); }
    aftPoly.push([-r3(hD(AFT.y0)-0.02),-13.5],[-STAIR.x,-13.5],[-STAIR.x,AFT.y0],[STAIR.x,AFT.y0],[STAIR.x,-13.5],[r3(hD(AFT.y0)-0.02),-13.5]);
    for(let i=NA;i>=0;i--){ const y=AFT.y1+(AFT.y0-AFT.y1)*i/NA; aftPoly.push([r3(hD(y)-0.02),r3(y)]); }
    const aftDeck={ id:'aft_deck', z:r3(dZ(-9.75)), winding:'ccw_from_above', polygon:aftPoly,
      note:'Planked aft deck at deck level from the helm-area riser (y='+CK.yH+') to the transom, the stair recess (x ±'+STAIR.x+', y '+AFT.y0+'..-13.5) cut out of it. Two flush garage lids, the mainsheet traveller and both mainsheet winches live here; the quarter pushpits carry seat pads.',
      _notes:[
        { id:'traveller', kind:'obstruction', type:'rail', footprint:{x:[-TRAV.hx,TRAV.hx],y:[TRAV.y-0.05,TRAV.y+0.05]}, height_above_floor_m:{top:0.07,car:0.12}, treatment:'step_over', provenance:'exact, TRAV; the car rides the pose (ANCHORS.travellerCar), the sheet rises from it to the boom end' },
        { id:'mainsheet_winches', kind:'obstruction', type:'winch', footprint:{x:[[-WINCH.mainX-0.13,-WINCH.mainX+0.13],[WINCH.mainX-0.13,WINCH.mainX+0.13]],y:[WINCH.mainY-0.13,WINCH.mainY+0.13]}, height_above_floor_m:{top:0.29}, treatment:'step_over', provenance:'exact, WINCH.main*' },
        { id:'garage_lids', kind:'surface', type:'hatch_lid', footprint:{x:[[-2.3,-0.4],[0.4,2.3]],y:[-10.6,-8.9]}, height_above_floor_m:{top:0.04}, treatment:'flat', provenance:'exact, decks build()' },
        { id:'quarter_pads', kind:'surface', type:'cushion', footprint:{x:[[-2.45,-1.55],[1.55,2.45]],y:[-12.87,-12.23]}, height_above_floor_m:{top:0.12}, treatment:'flat', provenance:'exact, decks build()' },
        { id:'stair_top', kind:'edge', type:'stair_head', footprint:{x:[-STAIR.x,STAIR.x],y:[AFT.y0-0.02,AFT.y0]}, treatment:'stairs', provenance:'STAIRS.transom_stair' },
      ] };
    const platform={ id:'swim_platform', z:PLAT, winding:'ccw_from_above', polygon:[[-PLATFORM.hx,PLATFORM.y1],[PLATFORM.hx,PLATFORM.y1],[PLATFORM.hx,PLATFORM.y0],[STAIR.x,PLATFORM.y0],[STAIR.x,STAIR.floorY],[-STAIR.x,STAIR.floorY],[-STAIR.x,PLATFORM.y0],[-PLATFORM.hx,PLATFORM.y0]],
      note:'The fold-down platform (opts.platform 1 = down) plus the recess floor inside the transom, '+r3(PLAT-DWL)+' m above the DWL. Absent when opts.platform is 0 — the transom door is shut over the stair.', conditional:'opts.platform >= 0.5' };
    const roofPoly=[]; const NR=8;
    for(let i=0;i<=NR;i++){ const y=RF.yA+(RF.yF-RF.yA)*i/NR; roofPoly.push([-r3(hxRoof(y)-0.07),r3(y),r3(roofZ(y))]); }
    for(let i=NR;i>=0;i--){ const y=RF.yA+(RF.yF-RF.yA)*i/NR; roofPoly.push([r3(hxRoof(y)-0.07),r3(y),r3(roofZ(y))]); }
    const coachroof={ id:'coachroof', winding:'ccw_from_above', polygon3d:roofPoly,
      note:'Walkable — the route to the mast, the halyard winches and the boom. Falls '+r3(RF.hA-RF.hF)+' m going forward; the raked front (y '+RF.yF+'..'+RF.yNose+') is a slope down to the foredeck. Reached from the cockpit coaming or either side deck ('+r3(RF.hA)+' m up from the side deck aft).',
      _notes:[
        { id:'mast', kind:'obstruction', type:'post', footprint:{x:[-0.17,0.17],y:[r3(MAST.y-0.11),r3(MAST.y+0.11)]}, height_above_floor_m:{top:'full height — masthead z '+MAST.headZ}, treatment:'wall', provenance:'exact, MAST' },
        { id:'roof_glass', kind:'surface', type:'hatch_lid', footprint:{x:[-0.85,0.85],y:ROOF_GLASS}, height_above_floor_m:{top:0.045}, treatment:'flat', provenance:'exact, ROOF_GLASS — fixed panels, not opening hatches' },
        { id:'halyard_winches', kind:'obstruction', type:'winch', footprint:{x:[[-WINCH.halyardX-0.12,-WINCH.halyardX+0.12],[WINCH.halyardX-0.12,WINCH.halyardX+0.12]],y:[WINCH.halyardY-0.12,WINCH.halyardY+0.12]}, height_above_floor_m:{top:0.27}, treatment:'step_over', provenance:'exact, WINCH.halyard*; clutch banks at y '+WINCH.clutchY },
        { id:'boom', kind:'overhead', type:'spar', footprint:{y:[r3(GOOSE[1]-BOOM_L),r3(GOOSE[1])]}, height_above_floor_m:{underside:r3(GOOSE[2]-BOOM_SEC[1]-roofZ(RF.yA))}, treatment:'overhead', provenance:'exact — '+r3(GOOSE[2]-BOOM_SEC[1]-roofZ(RF.yA))+' m over the aft roof: a crouch; the vang strut runs from the mast foot to the boom at y '+r3(GOOSE[1]-BOOM_L*0.27) },
      ] };
    const forePoly=[]; const NF=8, yf0=RF.yNose, yf1=13.4;
    for(let i=0;i<=NF;i++){ const y=yf0+(yf1-yf0)*i/NF; forePoly.push([-r3(hD(y)-0.02), r3(y), r3(dZ(y))]); }
    for(let i=NF;i>=0;i--){ const y=yf0+(yf1-yf0)*i/NF; forePoly.push([r3(hD(y)-0.02), r3(y), r3(dZ(y))]); }
    const foredeck={ id:'foredeck', winding:'ccw_from_above', polygon3d:forePoly,
      note:'Rising gently with the sheer from the saloon nose to the stem head; fenced by the pulpit and twin lifelines. The tender well is a '+WELL.depth+' m drop in the middle of it (see tender_well).',
      _notes:[
        { id:'tender_well', kind:'hole', type:'well', footprint:{x:'±'+WELL.hxA+' aft tapering to ±'+WELL.hxF+' fwd',y:[WELL.y0,WELL.y1]}, height_above_floor_m:{floor:-WELL.depth,rim:0.06}, treatment:'drop', provenance:'exact, WELL — published as DECK tender_well; the lid is off in pass 1' },
        { id:'hatches', kind:'surface', type:'hatch_lid', footprint:{centres:HATCHES.map(h=>[h[0],h[1]]),half:HATCHES.map(h=>h[2])}, height_above_floor_m:{top:0.045}, treatment:'flat', provenance:'exact, HATCHES' },
        { id:'windlass', kind:'obstruction', type:'fitting', footprint:{x:[-0.24,0.24],y:[r3(WINDLASS.y-0.2),r3(WINDLASS.y+0.2)]}, height_above_floor_m:{top:0.43}, treatment:'step_over', provenance:'exact, WINDLASS' },
        { id:'bow_pad', kind:'surface', type:'cushion', footprint:{x:[-0.32,0.32],y:[12.38,12.82]}, height_above_floor_m:{top:0.12}, treatment:'flat', provenance:'exact, foredeck build()' },
        { id:'furler_drum', kind:'obstruction', type:'fitting', footprint:{x:[-0.15,0.15],y:[r3(FORESTAY.foot[1]-0.17),r3(FORESTAY.foot[1]+0.13)]}, height_above_floor_m:{top:r3(FORESTAY.foot[2]-dZ(13.1))}, treatment:'step_over', provenance:'exact, FORESTAY.foot' },
        { id:'staysail_track', kind:'obstruction', type:'rail', footprint:{x:[-STAY.trackHx,STAY.trackHx],y:[STAY.trackY-0.16,STAY.trackY+0.04]}, height_above_floor_m:{top:0.07,car:0.14}, treatment:'step_over', provenance:'exact, STAY.track*; the car rides the pose (ANCHORS.stayCar)' },
        { id:'inner_forestay_base', kind:'obstruction', type:'fitting', footprint:{x:[-0.06,0.06],y:[r3(INNER.foot[1]-0.1),r3(INNER.foot[1]+0.1)]}, height_above_floor_m:{top:0.15}, treatment:'step_over', provenance:'exact, INNER.foot' },
        { id:'pulpit', kind:'obstruction', type:'guard_rail', footprint:{x:'both deck edges from y 12.3 round the stem head'}, height_above_floor_m:{top:0.72}, treatment:'wall', provenance:'exact, rigging build()' },
        { id:'jib_tracks', kind:'obstruction', type:'rail', footprint:{x:[[-r3(carX()+0.03),-r3(carX()-0.03)],[r3(carX()-0.03),r3(carX()+0.03)]],y:[0.1,2.0]}, height_above_floor_m:{top:0.05,car:0.11}, treatment:'step_over', provenance:'exact, carX()/CAR_Y — on the side decks; the lazy sheet crosses the foredeck to the windward car' },
      ] };
    const wellPoly=[[-WELL.hxA,WELL.y0],[WELL.hxA,WELL.y0],[WELL.hxF,WELL.y1],[-WELL.hxF,WELL.y1]];
    const well={ id:'tender_well', z:r3(dZ((WELL.y0+WELL.y1)/2)-WELL.depth), winding:'ccw_from_above', polygon:wellPoly, note:'Teak-soled well sunk '+WELL.depth+' m into the foredeck with a proud steel rim; the RIB stows here under its lid (lid not modelled — pass 1 shows it open).', access:['step down from the foredeck over the rim, any side'] };
    const saloon={ id:'saloon_sole', z:SAL, level:'saloon', winding:'ccw_from_above', polygon:[[-0.7,-1.0],[-0.7,RF.yF-0.02],[1.5,RF.yF-0.02],[1.5,-1.0]],
      note:'The raised deck saloon: from the foot of the companionway tread to the forward bulkhead door, between the port U-settee/table and the starboard chart table/settee. Headroom '+r3(roofZ(RF.yA)-0.05-SAL)+' aft to '+r3(roofZ(RF.yF)-0.05-SAL)+' forward. The mast comes through it at y '+MAST.y+'.',
      _notes:[
        { id:'settee_port_U', kind:'obstruction', type:'seat', footprint:{x:[-2.45,-0.7],y:[-0.6,3.6]}, height_above_floor_m:{top:0.54}, treatment:'step_over', provenance:'exact, interior build() bench(); outboard run x -2.45..-1.75 full length, returns y -0.6..0.0 and 3.0..3.6' },
        { id:'dining_table', kind:'obstruction', type:'table', footprint:{x:[-1.8,-0.8],y:[0.4,2.6]}, height_above_floor_m:{top:0.745}, treatment:'waist_block', provenance:'exact, interior build()' },
        { id:'chart_table', kind:'obstruction', type:'console', footprint:{x:[1.5,2.4],y:[-1.2,-0.3]}, height_above_floor_m:{top:0.94}, treatment:'waist_block', provenance:'exact, interior build(); plotter faces aft (INTERACT chart_table)' },
        { id:'nav_seat', kind:'obstruction', type:'seat', footprint:{x:[0.93,1.37],y:[-0.97,-0.53]}, height_above_floor_m:{top:0.50}, treatment:'step_over', provenance:'exact' },
        { id:'settee_stbd', kind:'obstruction', type:'seat', footprint:{x:[1.75,2.45],y:[0.3,3.6]}, height_above_floor_m:{top:0.54}, treatment:'step_over', provenance:'exact' },
        { id:'cabinet_stbd', kind:'obstruction', type:'counter', footprint:{x:[1.65,2.45],y:[3.65,4.25]}, height_above_floor_m:{top:0.90}, treatment:'waist_block', provenance:'exact' },
        { id:'mast', kind:'obstruction', type:'post', footprint:{x:[-0.17,0.17],y:[r3(MAST.y-0.11),r3(MAST.y+0.11)]}, height_above_floor_m:{top:'to the deckhead'}, treatment:'wall', provenance:'exact, MAST through the saloon' },
        { id:'companionway_tread', kind:'obstruction', type:'bulkhead_step', footprint:{x:[-0.55,0.55],y:[-1.6,-1.3]}, height_above_floor_m:{top:0.29}, treatment:'stairs', provenance:'STAIRS.companionway' },
        { id:'forward_steps', kind:'obstruction', type:'bulkhead_step', footprint:{x:[-0.5,0.5],y:[4.3,4.9]}, height_above_floor_m:{treads:[-0.28,-0.56],bottom:-0.85}, treatment:'stairs', provenance:'STAIRS.saloon_to_lower — down going forward' },
      ] };
    const lower={ id:'lower_sole', z:LOW, level:'lower', winding:'ccw_from_above', polygon:[[-0.55,4.9],[-0.55,12.4],[0.55,12.4],[0.55,4.9]],
      note:'Centreline passage below the foredeck from the foot of the saloon steps to the crew cabin: galley port / guest double starboard (y 5.4..7.8), a bulkhead door at y 8.0 into the VIP (island berth), a door at y 11.3 into the crew cabin, the chain-locker bulkhead at 12.5. Headroom '+r3(dZ(6.5)-0.05-LOW)+' m.',
      _notes:[
        { id:'galley', kind:'obstruction', type:'counter', footprint:{x:[-2.15,-0.75],y:[5.15,7.75]}, height_above_floor_m:{top:0.92}, treatment:'waist_block', provenance:'exact; sink y 5.52..5.88, stove y 6.62..7.18 (INTERACT stove)' },
        { id:'guest_berth', kind:'obstruction', type:'bed', footprint:{x:[0.75,2.25],y:[5.4,7.8]}, height_above_floor_m:{top:0.55}, treatment:'step_over', provenance:'exact, berth()' },
        { id:'vip_berth', kind:'obstruction', type:'bed', footprint:{x:[-0.9,0.9],y:[8.5,10.6]}, height_above_floor_m:{top:0.56}, treatment:'step_over', provenance:'exact, berth() — island berth, walk round both sides (INTERACT bunk)' },
        { id:'vip_cabinets', kind:'obstruction', type:'counter', footprint:{x:[[-1.55,-1.15],[1.15,1.55]],y:[8.3,10.9]}, height_above_floor_m:{top:0.70}, treatment:'waist_block', provenance:'exact' },
        { id:'crew_bunks', kind:'obstruction', type:'bed', footprint:{x:[[-0.78,-0.22],[0.22,0.78]],y:[11.45,12.45]}, height_above_floor_m:{top:0.52}, treatment:'step_over', provenance:'exact' },
        { id:'bulkhead_doors', kind:'opening', type:'door', footprint:{y:[8.0,11.3],x:[-0.5,0.5]}, height_above_floor_m:{top:1.95}, treatment:'pass', provenance:'exact, bulkhead() door openings; dressed open' },
      ] };
    const wash=(s)=>{ const pts=[]; const NW=10; for(let i=0;i<=NW;i++){ const u=uOf(CK.yH)+(uOf(RF.yNose)-uOf(CK.yH))*i/NW; pts.push([r3(s*(halfAtZ(u,deckZ(u),1)-0.006)), r3(yOf(u)), r3(deckZ(u))]); } return pts; };
    const WASHBOARD=[
      { side:'port', width_m:CK.sd, outer_edge:wash(-1), note:'Side deck from the aft-deck riser (y '+CK.yH+') to the saloon nose (y '+RF.yNose+'): '+CK.sd+' m inboard from the sheer along the cockpit coaming, widening to '+RF.sdA+'..'+RF.sdF+' along the saloon. Stanchions 0.08 in from the edge, twin lifelines at 0.36 / 0.72. Chainplates at y '+CHAIN.yAft+'..'+CHAIN.yFwd+' x ±'+CHAIN.x+' and the jib car (y '+CAR_Y+') are step-overs; the jib turning block at y '+TURN.y+', the spring cleats at y ±4.0.', provenance:'CK.sd / RF.sd* + xCoam(y) / hxRoof(y)' },
      { side:'starboard', mirror:'port across x=0' } ];
    const THRESHOLD={ id:'companionway', side:'aft', mechanism:'sliding',
      door_clear_width_m:r3(DOOR.x1-DOOR.x0), door_clear_height_m:r3(DOOR.z1-DOOR.z0), threshold_point:[0,DOOR.y,r3(DOOR.z0)],
      sill_above_cockpit_sole_m:r3(DOOR.z0-SOLE),
      slide:{ direction:'-x (to port) along the saloon aft face, outside the bulkhead', travel_m:DOOR.travel, leaf:{ width_m:DOOR.leaf.w, thickness_m:DOOR.leaf.th, at_y:r3(DOOR.y-0.06) }, clear_at_open_fraction:DOOR.clearAt,
        keep_clear:[[DOOR.x0-DOOR.travel,r3(DOOR.y-0.1)],[DOOR.x1,r3(DOOR.y-0.1)],[DOOR.x1,r3(DOOR.y-0.02)],[DOOR.x0-DOOR.travel,r3(DOOR.y-0.02)]],
        note:'A sliding leaf sweeps no arc: the keep_clear is the pocket strip it runs in along the bulkhead (x '+r3(DOOR.x0-DOOR.travel)+'..'+DOOR.x1+'). clear width = travel * doorOpen.' },
      hatch:null,
      door_cue:{ frames:8, played_reversed_on_exit:true, doorOpen_per_frame:'k/7 for k in 0..7', suggested_ms_per_frame:80 },
      provenance:'rig DOOR const; defaults CLOSED (fleet ruling 2026-08-19)' };
    const STAIRS={ companionways:[
      { id:'companionway', from:'cockpit_sole', to:'saloon_sole', mechanism:'InteriorStair', opening:{ x0:DOOR.x0, x1:DOOR.x1, at_y:DOOR.y, z0:r3(DOOR.z0), z1:r3(DOOR.z1) }, sill_z:r3(DOOR.z0),
        total_rise_m:r3(DOOR.z0-SAL), treads:[{top_z:2.34,going_m:0.30}], direction:'down going forward (+y)', provenance:'interior build(); sill = DOOR.z0' },
      { id:'saloon_to_lower', from:'saloon_sole', to:'lower_sole', mechanism:'InteriorStair', opening:{ x0:-0.5, x1:0.5, at_y:RF.yF, z0:1.7, z1:r3(SAL+2.0) }, sill_z:SAL,
        total_rise_m:r3(SAL-LOW), treads:[{top_z:1.77,going_m:0.30},{top_z:1.49,going_m:0.30}], direction:'down going forward (+y)', provenance:'interior build()' },
      { id:'helm_to_aft_deck', from:'cockpit_sole', to:'aft_deck', mechanism:'step', opening:{ x0:-CK.xi, x1:CK.xi, at_y:CK.yH }, total_rise_m:r3(dZ(CK.yH)-SOLE), treads:[{top_z:r3(STEP1),going_m:0.32}], direction:'up going aft (-y)', provenance:'cockpit build(); STEP1' },
      { id:'transom_stair', from:'aft_deck', to:'swim_platform', mechanism:'ExteriorStair', opening:{ x0:-STAIR.x, x1:STAIR.x, at_y:AFT.y0 }, total_rise_m:r3(dZ(AFT.y0)-PLAT),
        treads:STAIR.treads.map(t=>({top_z:t[2],going_m:0.5})), direction:'down going aft (-y) onto the recess floor at z '+PLAT+' then the platform', provenance:'STAIR const' } ] };
    const A=ANCHOR_PTS;
    const facings=(nx,ny)=>{ const o=[]; ['N','NE','E','SE','S','SW','W','NW'].forEach((d,i)=>{ const th=i*Math.PI/4; if(-nx*Math.sin(th)+ny*Math.cos(th)<0) o.push(d); }); return o; };
    const ALL=['N','NE','E','SE','S','SW','W','NW'];
    const INTERACT=[
      { id:'helm_port', action:'enter_helm', label:'Port wheel', verb:'Take the helm', level:'cockpit_sole', pos:P3(A.wheelHubPort), reach_point:P3(A.helmPort), visible_facings:ALL, mech:'twin wheels, one rudder: renderWheel(dir,{steer}) turns both, the rudder follows underbody', provenance:'WHEEL_HUBS[0] / ANCHOR_PTS.helmPort' },
      { id:'helm_stbd', action:'enter_helm', label:'Starboard wheel', verb:'Take the helm', level:'cockpit_sole', pos:P3(A.wheelHubStbd), reach_point:P3(A.helmStbd), visible_facings:ALL, mech:'as port', provenance:'WHEEL_HUBS[1]' },
      { id:'mainsheet_port', action:'trim_main', label:'Port mainsheet winch', verb:'Trim the main', level:'aft_deck', pos:P3(A.winchMainPort), reach_point:[-1.7,-8.65,r3(dZ(-8.65))], visible_facings:ALL, mech:'opts.main 0..1 — the boom follows within the wind; the traveller car goes to leeward as the sheet eases; opts.grind:"main" animates the handle on the LEEWARD winch', provenance:'WINCH.main*' },
      { id:'mainsheet_stbd', action:'trim_main', label:'Starboard mainsheet winch', verb:'Trim the main', level:'aft_deck', pos:P3(A.winchMainStbd), reach_point:[1.7,-8.65,r3(dZ(-8.65))], visible_facings:ALL, mech:'as port', provenance:'WINCH.main*' },
      { id:'primary_port', action:'trim_jib', label:'Port primary', verb:'Trim the genoa', level:'cockpit_sole', pos:P3(A.winchPrimaryPort), reach_point:[-1.9,-6.4,SOLE], visible_facings:ALL, mech:'opts.jib 0..1; opts.grind:"jib" animates the handle on the LEEWARD primary', provenance:'WINCH.primary*' },
      { id:'primary_stbd', action:'trim_jib', label:'Starboard primary', verb:'Trim the genoa', level:'cockpit_sole', pos:P3(A.winchPrimaryStbd), reach_point:[1.9,-6.4,SOLE], visible_facings:ALL, mech:'as port', provenance:'WINCH.primary*' },
      { id:'halyard_winch', action:'hoist_main', label:'Halyard winch', verb:'Hoist / lower the main', level:'coachroof', pos:P3(A.halyardWinchStbd), reach_point:[1.0,-0.9,r3(roofZ(-0.9))], visible_facings:ALL, mech:'opts.hoist 0..1 — the main flakes INTO the boom; opts.cover zips the boom cover at 0 (STORED)', provenance:'WINCH.halyard*' },
      { id:'furler', action:'furl_jib', label:'Furling line', verb:'Furl / unfurl the genoa', level:'cockpit_sole', pos:P3(A.furlerCleat), reach_point:[-1.9,-5.4,SOLE], visible_facings:ALL, mech:'opts.furl 0..1 — the line runs the port deck to the drum; the staysail furls on its own (opts.sfurl, electric)', provenance:'FURL_CLEAT' },
      { id:'windlass', action:'anchor', label:'Windlass', verb:'Drop / weigh anchor', level:'foredeck', pos:P3(A.windlass), reach_point:[0,11.7,r3(dZ(11.7))], visible_facings:ALL, mech:'the anchor hangs on the bow roller arm; no chain-out state in pass 1', provenance:'WINDLASS' },
      { id:'platform', action:'fold_platform', label:'Swim platform', verb:'Lower / raise the platform', level:'aft_deck', pos:P3(A.stairTop), reach_point:[0,-11.3,r3(dZ(-11.3))], visible_facings:ALL, mech:'opts.platform 0..1; 0 shuts the transom door over the stair', provenance:'PLATFORM / STAIR' },
      { id:'dining_table', action:'sit', label:'Saloon table', verb:'Sit down', level:'saloon_sole', pos:[-1.3,1.5,r3(SAL+0.745)], reach_point:[-0.4,1.5,SAL], visible_facings:facings(-1,0), mech:'the port U-settee', provenance:'interior build()' },
      { id:'chart_table', action:'navigate', label:'Chart table', verb:'Plot a course', level:'saloon_sole', pos:[1.95,-1.05,r3(SAL+0.85)], reach_point:[1.15,-0.75,SAL], visible_facings:facings(1,0), mech:'plotter screen faces aft; the nav seat is at (1.15, -0.75)', provenance:'interior build()' },
      { id:'stove', action:'cook', label:'Stove', verb:'Cook', level:'lower_sole', pos:[-1.45,6.9,2.18], reach_point:[-0.4,6.9,LOW], visible_facings:facings(-1,0), mech:'galley, port, lower level', provenance:'interior build()' },
      { id:'bunk', action:'sleep', label:'VIP berth', verb:'Sleep · save', level:'lower_sole', pos:[0,9.6,1.76], reach_point:[0,8.2,LOW], visible_facings:facings(0,1), mech:'InteriorBed (rest + save) — through the bulkhead door at y 8.0', provenance:'interior build()' },
    ];
    const SAIL={ rig:'masthead sloop with an inner forestay · deck-stepped mast through the saloon roof · three swept spreader sets, discontinuous diagonals · twin backstays · roller-furling genoa · self-tacking furling staysail · Park-Avenue boom with a rigid hydraulic vang (no topping lift)',
      mast:{ foot:[0,MAST.y,r3(MAST.footZ)], head_z:MAST.headZ, rake_deg:MAST.rakeDeg, section_m:[0.34,0.22], through:'saloon (a post from the saloon sole in the cabin view)', spreaders:SPREADERS.map(s=>({z:s.z,len_m:s.len,sweep_deg:s.sweep})), chainplates:{x:CHAIN.x,y:[CHAIN.yAft,CHAIN.yCap,CHAIN.yFwd]}, backstays:{x:BACKSTAY.x,y:BACKSTAY.y,twin:true} },
      boom:{ gooseneck:P3({x:GOOSE[0],y:GOOSE[1],z:GOOSE[2]}), length_m:BOOM_L, section_m:[2*BOOM_SEC[0],2*BOOM_SEC[1]], kind:'Park Avenue — the main flakes into the trough; opts.cover is the boom cover', vang:'rigid strut, mast foot +0.55 -> boom at 27% of its length', clearance_over_cockpit_sole_m:r3(GOOSE[2]-BOOM_SEC[1]-SOLE), clearance_over_aft_deck_m:r3(GOOSE[2]-BOOM_SEC[1]-dZ(TRAV.y)), swing_deg:{min:0,max:86,side:'leeward = opposite the wind'}, end_over:'the aft deck above the traveller' },
      main:{ P_m:MAIN.P, E_m:MAIN.E, head_m:MAIN.head, roach_m:MAIN.roach, battens:MAIN.battens, area_m2:r3(0.5*MAIN.P*MAIN.E*1.2),
        hoist:'opts.hoist 0..1 — luff = P*hoist up the mast; the flaked pile on the boom grows 0.10 -> 0.74 m as the cloth comes down', stored:'hoist 0 + cover:true — the boom cover zipped; the STORED state', sheet:'opts.main 0..1 — 1 hardened (boom 4°, traveller car centred), 0 eased (boom 86°, car at the leeward track end)', luff_on:'mast aft face', foot_on:'boom' },
      jib:{ kind:'genoa', luff_m:r3(JIB.luff), LP_m:JIB.LP, area_m2:r3(0.5*JIB.luff*JIB.LP), overlap:'~92% of J', tack:P3({x:JIB.tack[0],y:JIB.tack[1],z:JIB.tack[2]}), head:P3({x:JIB.head[0],y:JIB.head[1],z:JIB.head[2]}),
        furl:'opts.furl 0..1 — rolled onto the forestay; rolled radius 0.05+0.13*furl, the UV strip is the canvas slot', sheet:'opts.jib 0..1 — 1 hardened (9°), 0 eased (85°)', leads:{ cars:[[-r3(carX()),CAR_Y],[r3(carX()),CAR_Y]], turning_blocks:[[-r3(TURN.x()),TURN.y],[r3(TURN.x()),TURN.y]], primaries:[[-r3(WINCH.primaryX()),WINCH.primaryY],[r3(WINCH.primaryX()),WINCH.primaryY]] } },
      staysail:{ luff_m:r3(STAY.luff), LP_m:STAY.LP, area_m2:r3(0.5*STAY.luff*STAY.LP), tack:P3({x:STAY.tack[0],y:STAY.tack[1],z:STAY.tack[2]}), head:P3({x:STAY.head[0],y:STAY.head[1],z:STAY.head[2]}),
        furl:'opts.sfurl 0..1 (default 1 = furled)', sheet:'self-tacking: angle = min(jib angle, '+STAY.maxDeg+'°); the car rides the curved track at y '+STAY.trackY+' to x = ±'+r3(STAY.trackHx*0.95)+' at full travel', shadow:'fill *0.6 while the genoa is set (furl < 0.6)' },
      pose_law:{ inputs:'awa (deg, + = wind over the starboard bow), aws (kn), main 0..1, jib 0..1, frame 0..7',
        boom_deg:'min(4 + 82*(1-main), max(0, |awa| - 6))', jib_deg:'min(9 + 76*(1-jib), max(0, |awa| - 6))', stay_deg:'min(jib_deg, '+STAY.maxDeg+')',
        angle_of_attack:'|awa| - sail angle', fill:'clamp((aoa-5)/13) * clamp(aws/6); jib + staysail *0.4 when |awa|>150 (blanketed)',
        luff:'clamp((8-aoa)/8) — flogging near the luff; |awa|<25 = in irons, every sail flogs whole and the boom wanders ±2°',
        stall:'aoa>40 — full but dead; camber eases 40% by aoa 80', heel_deg:'min('+HEEL_MAX+', '+HEEL_K+'*aws^2*(0.25+0.75*fill_main)*k(awa)) to leeward; k=1 to 100°, 0.35 dead downwind; 15% in irons — a 27 m yacht is stiffer than the 30',
        frames:'8, looping: cloth flutter + winch handle + (rock:true) the wave', note:'ART-SIDE law: it makes the sprite agree with itself. Gameplay owns the polar and drive; feed the rig the awa/aws/sheet it decides and the picture follows.' },
      standing_rigging:{ forestay:[P3({x:FORESTAY.foot[0],y:FORESTAY.foot[1],z:FORESTAY.foot[2]}),P3({x:FORESTAY.head[0],y:FORESTAY.head[1],z:FORESTAY.head[2]})], inner_forestay:[P3({x:INNER.foot[0],y:INNER.foot[1],z:INNER.foot[2]}),P3({x:INNER.head[0],y:INNER.head[1],z:INNER.head[2]})], backstays:'twin, masthead to the quarters at (±'+BACKSTAY.x+', '+BACKSTAY.y+')', caps:'chainplates (±'+CHAIN.x+', '+CHAIN.yCap+') through all three spreader tips to the masthead', diagonals:'D2 spreader-1 tip -> spreader-2 root, D3 spreader-2 tip -> spreader-3 root', lowers:'fore + aft from (±'+CHAIN.x+', '+CHAIN.yFwd+'/'+CHAIN.yAft+') to the lower spreader root' },
      running_rigging:{ mainsheet:'boom end -> traveller car (aft deck, y '+TRAV.y+') -> track end -> leeward mainsheet winch (±'+WINCH.mainX+', '+WINCH.mainY+')', jib_sheets:'clew -> car -> turning block -> primary, both sides; the lazy sheet sags across the foredeck forward of the mast', staysail_sheet:'clew -> self-tacking car; led below', halyards:'mast foot -> organiser -> clutch banks (y '+WINCH.clutchY+') -> halyard winches (±'+WINCH.halyardX+', '+WINCH.halyardY+') on the saloon roof', furling_line:'drum -> port side deck -> coaming cleat ('+FURL_CLEAT[1]+')', lazy_jacks:'mast z '+r3(MAST.footZ+13.5)+' -> boom top, three legs a side' } };
    const WATERLINE={ z:DWL, clip_rule:'the water shader cuts at hull z='+DWL+' — the boot-top BOTTOM. The pivot row is the canoe-body bottom, '+DWL+' m (~'+Math.round(DWL*Math.cos(40*DEG)*32)+' px at elev 40) below it; there is no per-facing correction, the DWL is a horizontal line in sprite space under roll 0.',
      draft_m:{ canoe_body:DWL, keel:r3(DWL+2.65+0.6), rudder:r3(DWL+1.9) }, underbody:'fin keel (root chord 4.4 at y -1.0..3.4, bulb at z -2.85) and spade rudder (stock y -11.2) bake only with opts.underbody — the dry reference. Afloat they are under the shader\u2019s water. The swim platform sits '+r3(PLAT-DWL)+' m above the cut.',
      note:'Heel is a render param (pose.heel, degrees about the origin): the waterline on the sprite tilts with it; the shader cut stays level in world.' };
    return {
      schema:'hidden-harbours/boat-gameplay-geometry@1', rig:'sloop88IsoRig.js', exportSymbol:'Sloop88Iso', units:'metres',
      frame:{ origin:'amidships / canoe-body bottom / centreline', axes:'+x starboard, +y bow, +z up', scale_px_per_m:PX, LOA_m:L, polygon_winding:'CCW viewed from +Z (above)' },
      authoring:'Generated by Sloop88Iso.gameplayGeometry() from the same constants the bake uses (station/skin/halfAtZ/deckZ/roofZ, CK, RF, DOOR, MAST, GOOSE, WINCH, TRAV, STAY, WELL, STAIR). Fit-out footprints are the build literals. Do not hand-edit — regenerate from the builder page; Art/_sidecarExport.js stamps derivedFromRigSha256.',
      extractor_contract:'per section: rig export -> this sidecar -> absent section = hull does not support the feature (not an error).',
      hull:{ loa:L, beam:r3(2*T[3][0]), transomBeam:r3(2*T[0][0]), dwl_z:DWL, cockpitSole:SOLE, saloonSole:SAL, lowerSole:LOW, platform:PLAT, seat:CK.seatZ, coaming:CK.coamZ, sheerAft:r3(sheerZ(0)), sheerAmid:r3(sheerZ(0.5)), sheerStem:r3(sheerZ(1)), coachroofAft:r3(roofZ(RF.yA)), coachroofFwd:r3(roofZ(RF.yF)), masthead:MAST.headZ, bilgeFrac:FC, rake_m:RAKE },
      DECK:[cockpit, aftDeck, platform, coachroof, foredeck, well, saloon, lower],
      WASHBOARD, CLEATS:CLEAT_PTS.map(c=>Object.assign({},c,{provenance:'exact, cockpit build() cleat boxes'})),
      ANCHORS:Object.keys(A).reduce((o,k)=>{ o[k]=P3(A[k]); return o; },{}),
      THRESHOLD, STAIRS, INTERACT, SAIL, WATERLINE,
      _excluded:{ LADDER:'None — stairs everywhere: the transom stair, the walkway step, the companionway tread, the saloon steps. The swim ladder folds out of the platform and is not modelled.',
        owners_cabin:'The aft (owner\u2019s) cabin under the cockpit and aft deck is not cut open in pass 1 — the cockpit and aft deck keep their soles in the cabin view.',
        tender:'The RIB that lives in the tender well is not modelled; the well is shown open, lid off.',
        bimini_sprayhood:'Not modelled — the reference deck is clean.',
        spinnaker_code_zero:'No downwind sail. The genoa and staysail are blanketed dead downwind (fill *0.4) and that is the picture.',
        passerelle_davit_radar:'Not modelled.',
        wake_spray:'The game\u2019s water owns the cut, the wake and the heel-line foam.' },
      _confirm:{ heel_law:'art-side, capped 20°; gameplay may override with heelDeg per frame', boom_over_aft_deck:r3(GOOSE[2]-BOOM_SEC[1]-dZ(TRAV.y))+' m over the aft deck where the traveller is — clear, but the sheet is a rope in the air across it', coachroof_walkable:'published walkable; the front slope is the route down to the foredeck', platform_states:'swim_platform is conditional on opts.platform — rule whether the folded state is a game state at all', reach_points:'untested requests, per the camper precedent' } };
  }

  root.Sloop88Iso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], PAINT, STEEL, DECKF, GLAS, MOTO, ROPE, TEAK, CREAM, KEY,
    render, renderWheel, wheelFaces, WHEEL_GEO, WHEEL_HUBS, WHEEL_LOCK, ROCK, rock:rockMotion,
    sailPose, poseOf, anchors, ANCHOR_PTS, CLEAT_PTS, doorMount, DOOR, RIG, STAY, SAILS:{ main:MAIN, jib:JIB, staysail:STAY },
    L, DWL, SOLE, SAL, LOW, PLAT, ZLIP, geometry, LEVEL_IDS, gameplayGeometry, faces:()=>F, bounds,
    loft:{ station, skin, fracAtZ, halfAtZ, sheerZ, deckZ, roofZ, hxRoof, xCoam, uOf, yOf, L, TH, DWL, SOLE, SAL, LOW, NSEG,
           coachroof:RF, cockpit:CK, aft:AFT, stair:STAIR, platform:PLATFORM, well:WELL, mast:MAST, shade:{ GAIN, BIAS, LN, BAYER, KEY, EDGE:0.30 }, cell:{ W, H, cx, cy, S } },
    SCHEMES, schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, SLOTS, palette, rampFrom, chipWall, C_CAP };
})(typeof globalThis!=='undefined'?globalThis:window);

/* sternTrawlerMk2IsoRig.js — Hidden Harbours STERN TRAWLER Mk II, 38 m, in the hull layout (ADR 0048 §8 A)
   (procedural, zero dependencies, ¾ top-down, 8 directions). Spec: docs/art/hull-layout-spec.md.

   The stern trawler's fuller, flared sister: a high bow whose sections hollow out to a heavy flare under a
   whaleback, a thick cream sheer band and a cream stripe along the middle of the grey topsides, the deckhouse
   forward of midships with a wheelhouse that FLARES as it rises (its sides lean out, its windscreen's head
   leans out over the bow), a raked buff funnel behind it, and a long working deck aft that ends in the STERN
   RAMP. Over the ramp head stands the plated steel GANTRY, grown out of the bulwarks and painted with the hull;
   an arm off each of its legs carries a trawl door's block outboard of the rail. Two wood-coamed fish hatches
   are on the centreline; the split trawl winch sits behind the house and its warps run aft through deck
   bollards to the door blocks; the net drum is ahead of the ramp with the net wound on it.

   The hull is the old Mk II rig's, number for number: the same offsets table and flare, L 38.0, beam 9.0,
   deck 3.5, the same sheer, the same cell (PX 32, 1344 x 1152, pivot 672,672). What is new is everything the
   hull layout asks: tagged faces (lv / ro / it), three rooms (house: the mess and galley; bridge: the
   wheelhouse; below: the crew's cabin), two hinged doors as leaves (doorFaces(t)), the cutaway table, four
   paint schemes, the walk. The house and the wheelhouse are narrower than the old rig's (see the arrangement).

   Frame: +x starboard, +y bow, +z up, metres; origin amidships at the keel bottom on the centreline.

   API (global SternTrawlerMk2Iso; the hull is byId('stern_trawler_mk2') and the same names stand on the global):
     faceList(state), partFaces(id, state), interiorFaces(), doorFaces(t), geometry(), matsFor(scheme),
     walk(), fitCheck(tol), render(dir, opts), renderCut(dir, key, opts), schemeIds, defaultScheme,
     GAIN, BIAS, LN, BAYER, PX, W, H, pivot, defaultElev, DWL, cockpitZ, loa.
   State keys (geometry().states): doors 0..1 (the trawl doors, stowed at the gantry's arms -> lowered to the water),
     drum (deg), net 0..1 (wound on the drum -> shot down the ramp), hatch 0..1 (the two fish hatches' lids),
     doorOpen 0..1 (render option; the leaves are doorFaces(t)).
   Kept from the old rig: render, W, H, PX, DIRS, pivot, defaultElev, order, the ramps (HULL .. IRON, KEY), ROCK,
     rock, helmSeat / HELM, gantryMounts / GANTRY, gallowsMounts / GALLOWS, haulerMount / HAULER, drumMount / DRUM,
     tubMounts / TUBS, navMounts, doorMount, DOOR, HOUSE, LEVEL_IDS, faces(), loft. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1344, H = 1152, cx = 672, cy = 672;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;                          // ADR 0031: the fleet draws no keyline
  const ID=(p)=>p;
  const ROCK = { frames: 8, rollA: 1.6, pitchA: 0.9, heaveA: 1.0, period: 5.0 };  // 38 m of steel: the slowest, stiffest roll in the fleet
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }

  // ---- fixed ramps: the old rig's, value for value (the default scheme paints with exactly these) ----
  const HULL  = ['#15191c','#2b3338','#454e54','#565c5f','#6f767a','#868d90','#a3adb2'];  // grey steel topsides
  const BOOT  = ['#0a0c0e','#111417','#181c20','#1d2226','#262b2e'];                       // near-black boot / bottom
  const CREAM = ['#8f8a7a','#a8a294','#c2bcae','#cdc7b6','#d8d2c6','#eae6da'];             // house + cove line
  const DECKF = ['#2e2b26','#3a3630','#46443a','#5c5852','#74706a','#828884','#93907f'];  // taupe steel deck / bulwark liner
  const WOOD  = ['#3a2c20','#5a4634','#6f5840','#8c6a45','#a5825a'];                       // pound boards, coamings, trawl doors
  const GLAS  = ['#131c21','#213039','#33434e','#48657a','#6b91a1'];                       // window glass (sea-grey)
  const BUFF  = ['#6b4a22','#8c5e2c','#b06f32','#cf7a35','#e29a55','#f0b878'];             // ochre gantry / net drum / funnel
  const NET   = ['#050516','#0d0d2b','#1a1a44','#2b2b5e','#414178'];                       // navy trawl net
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2'];                       // winch drums, rails, aerials
  const IRON  = ['#0e1114','#171b21','#232a32','#333c46'];                                 // dark fittings, ramp plating
  const SAFE  = ['#7a2a14','#a63a18','#cf5320','#e8722e','#f39a52'];                       // lifering, liferafts, floats, totes
  const KEY   = '#0d1013';

  // ---- the fleet's OKLCH ramp mixer (cruiserIsoRig.js, verbatim) -------------------------------------
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

  // ---- colourways: four working boats. The default is the old rig's paint, its ramps taken as written ----
  // (slot cove is her sheer band, the stripe along the topsides and the rail cap; slot hull is the topsides AND the gantry)
  const SCHEMES = {
    'slate-grey':   { name:'Slate Grey',   hull:'#565c5f', house:'#cdc7b6', cove:'#cdc7b6', gear:'#cf7a35', bottom:'#1d2226',
                      ramps:{ hull:HULL, house:CREAM, cove:CREAM, gear:BUFF, bottom:BOOT },
                      note:'the old rig’s paint, ramp for ramp: grey steel, a cream sheer band, stripe and house, a buff funnel and gear, a black boot' },
    'spruce-green': { name:'Spruce Green', hull:'#2d5146', house:'#e0dccb', cove:'#e0dccb', gear:'#c9a03a', bottom:'#6e2f25',
                      note:'dark green topsides and gantry, a cream sheer band and house, a yellow funnel and gear, red oxide below the boot top' },
    'tar-black':    { name:'Tar Black',    hull:'#2a2e31', house:'#d6cfba', cove:'#8e2f22', gear:'#8e2f22', bottom:'#6e2f25',
                      note:'black topsides and gantry, a red lead sheer band, stripe, funnel and gear, a cream house, red oxide below' },
    'atlantic-blue':{ name:'Atlantic Blue',hull:'#1f3f63', house:'#e4e4dc', cove:'#e4e4dc', gear:'#d8b548', bottom:'#1d2226',
                      note:'deep blue topsides and gantry, a white sheer band and house, a yellow funnel and gear, a black boot' },
  };
  const DEFAULT_SCHEME = 'slate-grey';
  const SLOTS = ['hull','house','cove','gear','bottom'];
  const _pal={};
  function palette(o){
    o=o||{};
    const id=o.paint ? 'custom' : (SCHEMES[o.scheme] ? o.scheme : DEFAULT_SCHEME), DF=SCHEMES[DEFAULT_SCHEME];
    const base=o.paint || SCHEMES[id], fixed=base.ramps || {};
    const hex=(k)=>base[k] || DF[k];
    const key=SLOTS.map(hex).join('|')+(base.ramps?'|fixed':'');
    if(_pal[key]) return _pal[key];
    const hullR=fixed.hull || rampFrom(hex('hull'),7), houseR=fixed.house || rampFrom(hex('house'),6,{anchor:0.70});
    const coveR=fixed.cove || (hex('cove')===hex('house') ? houseR : rampFrom(hex('cove'),6,{anchor:0.70}));
    const gearR=fixed.gear || (hex('gear')===hex('cove') ? coveR : rampFrom(hex('gear'),6,{anchor:0.62}));
    const botR=fixed.bottom || (hex('bottom')===DF.bottom ? BOOT : rampFrom(hex('bottom'),7,{anchor:0.86, floor:0.50}));
    const mats = { hull:{ramp:hullR,off:0,dith:0},     boot:{ramp:botR,off:0,dith:0},
                   cream:{ramp:houseR,off:0,dith:0},   cove:{ramp:coveR,off:0,dith:0},
                   deck:{ramp:DECKF,off:0,dith:0.22},  wood:{ramp:WOOD,off:0,dith:0.30},
                   glas:{ramp:GLAS,off:0,dith:0},      buff:{ramp:gearR,off:0,dith:0},
                   net:{ramp:NET,off:0,dith:0.50},     steel:{ramp:STEEL,off:0,dith:0},
                   iron:{ramp:IRON,off:0,dith:0},      blk:{ramp:BOOT,off:-1,dith:0},
                   dark:{ramp:BOOT,off:-2,dith:0},     safe:{ramp:SAFE,off:0,dith:0} };
    const rindex={};
    [hullR,botR,houseR,coveR,DECKF,WOOD,GLAS,gearR,NET,STEEL,IRON,BOOT,SAFE].forEach(r=>r.forEach((c,i)=>{ if(!rindex[c]) rindex[c]={r,i}; }));
    const out={ key, id, name:base.name||'Custom', base:{ hull:hex('hull'), house:hex('house'), cove:hex('cove'), gear:hex('gear'), bottom:hex('bottom') },
                note:base.note||'', ramps:{ hull:hullR, house:houseR, cove:coveR, gear:gearR, bottom:botR }, mats, rindex };
    _pal[key]=out;
    return out;
  }

  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- the hull: the old rig's lines, unchanged -------------------------------------------------------
  const L = 38.0, TH = 0.09, DECK = 3.5;
  const NSEG = 32;
  const RAKE = 1.30;                                      // raked trawler stem
  const DWL = 1.63;                                       // HullMeshDef.RestingDraftMeters (SternTrawlerMk2IsoHullMesh.asset)
  const Z_BOOT = 1.85;                                    // the boot top: level, 0.22 over the waterline
  // offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m). A broad transom stern (the ramp
  // cut through it), a full midbody, a marked sheer to a high raked bow: fuller and rounder in plan forward
  // than the Mk I, and finer below, so the bow sections hollow out to a heavy flare (flareExp).
  // sheerZ = kz+depth: 4.75, 4.53, 4.44, 4.45, 4.50, 4.65, 5.13, 6.15, 7.30
  const T = [
    [3.40,2.40,4.30,0.45],   // 0 transom (ramp)
    [4.10,3.30,4.35,0.18],   // 1
    [4.40,3.80,4.40,0.04],   // 2
    [4.50,4.00,4.45,0.00],   // 3
    [4.50,4.00,4.50,0.00],   // 4 amidships (max beam 9 m)
    [4.45,3.70,4.65,0.00],   // 5
    [4.25,2.80,5.10,0.03],   // 6
    [3.65,1.25,5.95,0.20],   // 7 bow shoulder: full and round in plan, heavy flare
    [0.22,0.05,6.70,0.60],   // 8 stem
  ];
  const lerp = (a,b,t)=>a+(b-a)*t;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), wb:lerp(A[1],B[1],fr), dep:lerp(A[2],B[2],fr),
             kz:lerp(A[3],B[3],fr), y:-L/2+u*L };
  }
  function bowRake(u,frac){ const t=Math.max(0,(u-0.62)/0.38), s=t*t*(3-2*t); return RAKE*s*(0.30+0.70*frac); }
  // the flare: forward of u 0.52 a section's breadth grows as frac^flareExp, so the bow is hollow below and flares at the rail
  function flareExp(u){ const t=Math.max(0,(u-0.52)/0.48), s=t*t*(3-2*t); return 1+1.9*s; }
  function skin(side,u,frac,inset){
    const st=station(u);
    const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
    return [ side*lerp(wb,ws,Math.pow(frac,flareExp(u))), st.y+bowRake(u,frac), st.kz+lerp(0,dep,frac) ];
  }
  function dfrac(st){ return Math.max(0.04, Math.min(0.98, (DECK - st.kz)/st.dep)); }
  const uOf=(y)=>(y+L/2)/L, yOf=(u)=>-L/2+u*L;
  const fracAt=(st,z)=>Math.max(0,Math.min(1,(z-st.kz)/st.dep));
  const sheerU=(u)=>{ const st=station(u); return st.kz+st.dep; };
  // the station whose skin passes through world (y, z): forward of u 0.62 the stem's rake carries the skin forward
  const yAtU=(u,z)=>{ const st=station(u); return st.y+bowRake(u,fracAt(st,z)); };
  function uAt(y,z){ if(yAtU(1,z)<=y) return 1; if(yAtU(0,z)>=y) return 0;
    let lo=0, hi=1; for(let i=0;i<40;i++){ const m=(lo+hi)/2; if(yAtU(m,z)<y) lo=m; else hi=m; } return (lo+hi)/2; }
  // the hull's half-breadth at world (y, z): outside (inset falsy) or the plating's inner face
  const halfAt=(y,z,inset)=>{ const u=uAt(y,z), st=station(u); return Math.abs(skin(1,u,fracAt(st,z),inset)[0]); };
  // a point on the outer skin at station u and height z, stood off by `off`
  const onHull=(side,u,z,off)=>{ const st=station(u), p=skin(side,u,fracAt(st,z)); return [p[0]+side*(off||0), p[1], z]; };
  // the working deck's half-breadth at station u (inside the bulwark plating)
  const dw=(u)=>{ const st=station(u); return (lerp(st.wb,st.ws,Math.pow(dfrac(st),flareExp(u)))-TH)*0.97; };
  const dwY=(y)=>dw(uOf(y));

  // ---- generic solids (the fleet's) ---------------------------------------------------------------------
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
  // an n-sided drum (or cone: rad2 at B) from A to B, turned by rot about its axis; caps 1: at B, 2: both ends
  function cyl(A,B,rad,n,mat,b,db,caps,rad2,capB,rot){
    const ax=v_norm(v_sub(B,A)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax), out=[], r0=[], r1=[], R2=rad2==null?rad:rad2;
    const dirAt=(a)=>[r[0]*Math.cos(a)+u[0]*Math.sin(a), r[1]*Math.cos(a)+u[1]*Math.sin(a), r[2]*Math.cos(a)+u[2]*Math.sin(a)];
    for(let k=0;k<n;k++){ const d=dirAt(2*Math.PI*(k+0.5)/n+(rot||0)); r0.push(v_add(A,v_mul(d,rad))); r1.push(v_add(B,v_mul(d,R2))); }
    for(let k=0;k<n;k++){ const k2=(k+1)%n; faceO(out,[r0[k],r0[k2],r1[k2],r1[k]],mat,b,db,dirAt(2*Math.PI*(k+1)/n+(rot||0))); }
    if(caps){ faceO(out,r1,mat,capB==null?b:capB,db,ax); if(caps>1) faceO(out,r0,mat,capB==null?b:capB,db,[-ax[0],-ax[1],-ax[2]]); }
    return out;
  }
  function rrect(ua,ub,va,vb,c){ return [[ua+c,va],[ub-c,va],[ub,va+c],[ub,vb-c],[ub-c,vb],[ua+c,vb],[ua,vb-c],[ua,va+c]]; }
  const ngon=(n,rad,map)=>{ const p=[]; for(let k=0;k<n;k++){ const a=2*Math.PI*(k+0.5)/n; p.push(map(rad*Math.cos(a), rad*Math.sin(a))); } return p; };

  // ---- the arrangement: every number the mesh, the rooms and the walk are built from --------------------
  const SOLE_U = 0.755, U0 = 0.016, BRK = yOf(SOLE_U);    // the working deck runs from the transom to the break (y 9.69)
  // the stern ramp: a slot through the transom, its floor rising to the deck at RYT
  const RW = 1.70, RYT = -13.6, RZ0 = 1.75;
  const rampZ=(y)=> RZ0 + (DECK-0.02-RZ0)*((y+L/2)/(RYT+L/2));
  // the deckhouse (level house) and the wheelhouse on it (level bridge). The old rig's house was 3.45 half breadth
  // and its wheelhouse 2.68 flaring to 3.14: the house is 0.90 narrower now and the wheelhouse 0.58 / 0.64, so the
  // side decks are wide enough to walk and to carry the fo'c'sle stairs (her flared bow leaves the deck only 3.25
  // half breadth at the break). The wheelhouse keeps its flare: its sides lean out 0.40 from sole to head, and its
  // front leans out over the bow, 0.62 from FYb to FYt.
  const HXl = 2.55, HYa = 1.5, HYf = BRK, HZ1l = 6.50;
  const HXw0 = 2.10, HXw1 = 2.50, WYa = 3.4, FYb = 9.00, FYt = 9.62;
  const WZ0 = 6.56, WZ1 = 8.95, ROOFZ = 9.05;
  const xwz=(z)=> HXw0 + (HXw1-HXw0)*(z-WZ0)/(WZ1-WZ0);   // the wheelhouse's half breadth at height z
  const yFront=(z)=> FYb + (FYt-FYb)*(z-WZ0)/(WZ1-WZ0);
  const BL = { sole:1.15, ceil:DECK-0.12, hx:2.90, y0:2.1, y1:8.9 };     // the crew's cabin under the house
  // the doors: hinged weather-tight steel, both in aft walls, both swinging aft
  const DOOR  = { id:'house_door', kind:'hinge', face:'aft', y:HYa, x0:-0.55, x1:0.45, z0:DECK+0.05, z1:5.50,
                  hinge:'port', swingDeg:110, maxDeg:110, clearAt:0.60, sillZ:DECK, lv:'house' };
  const WDOOR = { id:'wheelhouse_door', kind:'hinge', face:'aft', y:WYa, x0:0.95, x1:1.65, z0:WZ0+0.05, z1:8.50,
                  hinge:'starboard', swingDeg:105, maxDeg:105, clearAt:0.60, sillZ:WZ0, lv:'bridge' };
  const LEAVES = [DOOR, WDOOR];
  // the stairs inside: house -> bridge to port, house -> below to starboard, both climbing or falling forward
  const ST_UP = { x0:-2.00, x1:-1.20, y0:4.0, y1:6.4, z0:DECK, z1:WZ0, n:13 };
  const ST_DN = { x0:1.65, x1:2.45, y0:4.0, y1:6.0, z0:DECK, z1:BL.sole, n:10 };
  // the stairs outside: from each side deck up to the whaleback at the break
  const ST_FD = { xa:HXl+0.06, xb:HXl+0.66, y0:7.3, y1:BRK, n:7 };
  const LADX = 2.25;                                      // the boat deck's ladder, on the house's aft wall to starboard
  // working-deck gear
  const HATCH   = { id:'hold_hatch',     y:-6.4, hw:1.20, hl:1.0, h:0.30, maxDeg:95 };   // the after fish hatch, hinged on its forward edge
  const HATCH_F = { id:'hold_hatch_fwd', y:-3.2, hw:1.20, hl:1.0, h:0.30, maxDeg:95 };   // the forward fish hatch, the same
  const HATCHES = [HATCH, HATCH_F];
  const SORT = { hw:2.30, y0:-9.30, y1:-7.90, spillY:-9.40, sorterX:1.20, sorterY:-8.20 };   // the sorting ground: clear deck between the net drum and the aft hatch
  const WINCH = { y:0.5, x:1.9, hx:0.95, hy:0.50 };                      // the split trawl winch: a drum either side of the door
  const DRUMG = { y:-11.3, z:4.75, hx:1.90, stand:2.35, rFl:0.78, rFull:0.66, rBare:0.28 };
  // the stern gantry: a plated arch whose legs stand in the bulwarks at x0 and lean in to x1 under a deep box beam
  const GANT  = { y:-14.4, x0:3.42, x1:2.62, lw:0.40, dyh:0.60, zt:8.42, beamZ:8.72, beamHz:0.36, beamHx:3.02, cheekY:1.05 };
  // the trawl doors' gallows: an arm off each leg, out over the rail; the door's block hangs under its end
  const GAL   = { y:GANT.y+0.10, hy:0.16, zt:6.95, blockZ:6.55, out:0.28 };
  const FUNNEL= { y0:2.20, y1:1.85, r:0.52, zt:9.10 };
  const WARP_POST = { x:2.90, y:-9.72 };                  // the warps' deck bollards
  const GAL_BLOCK=[-1,1].map(s=>({ x:s*(halfAt(GAL.y, sheerU(uOf(GAL.y)))+GAL.out), y:GAL.y, z:GAL.blockZ }));
  // the whaleback: its stations from the break to the stem, 0.10 under the sheer, inside the plating
  const FD=(function(){ const a=[], N=19;
    for(let i=0;i<=N;i++){ const u=SOLE_U+(1-SOLE_U)*i/N, st=station(u), fr=1-0.10/st.dep, p=skin(1,u,fr,1); a.push({ u, y:p[1], z:p[2], half:p[0] }); }
    a.unshift({ u:SOLE_U, y:BRK, z:a[0].z, half:a[0].half }); return a; })();
  function fdAt(y){
    if(y<=FD[0].y) return { z:FD[0].z, half:FD[0].half };
    for(let i=0;i+1<FD.length;i++){ const A=FD[i], B=FD[i+1]; if(y<=B.y){ const t=(y-A.y)/Math.max(1e-9,B.y-A.y); return { z:lerp(A.z,B.z,t), half:lerp(A.half,B.half,t) }; } }
    const Z=FD[FD.length-1]; return { z:Z.z, half:Z.half };
  }
  const FD_Z0 = FD[0].z;                                  // the whaleback at the break
  // the helm: the cruiser's seated fit (30 of 30 on character rig 10.3), carried to the wheelhouse's sole
  const HS_F = DECK+0.02, BR_F = +(WZ0+0.02).toFixed(2);                 // the rooms' soles: the house's on the deck plate, the wheelhouse's on the boat deck
  const WHEEL_HUB = { x:0.30, y:8.02, z:BR_F+0.79 };
  const WHEEL_GEO = { rad:0.19, rimIn:0.158, rake:-20, seg:16, hubR:0.05, spokes:[0,180,270], col:0.10 };
  const HELM  = { x:0.30, y:7.60, z:BR_F+0.51 };
  const PILOT = { x:0.30, y:8.04, z:BR_F };
  const SAMSON = { x:0, y:18.4, z:fdAt(18.4).z+0.22 };    // the samson post on the stem head

  // ---- STATIC faces --------------------------------------------------------------------------------
  const F = [];
  const fo=(v,mat,b,db,want)=>faceO(F,v,mat,b,db,want);
  const boxF=(c,h,mat,b,db,xf)=>{ F.push.apply(F, box(c,h,mat,b,db,xf)); };
  const tubeF=(A,B2,rad,mat,b)=>{ F.push.apply(F, tube(A,B2,rad,mat,b)); };
  const cylF=(A,B,rad,n,mat,b,db,caps,rad2,capB)=>{ F.push.apply(F, cyl(A,B,rad,n,mat,b,db,caps,rad2,capB)); };
  // axis-aligned panels, turned to face `side` / `dir` / up
  const pX=(out,side,x,y0,y1,z0,z1,mat,b,db)=>faceO(out,[[x,y0,z0],[x,y1,z0],[x,y1,z1],[x,y0,z1]],mat,b,db,[side,0,0]);
  const pY=(out,dir,y,x0,x1,z0,z1,mat,b,db)=>faceO(out,[[x0,y,z0],[x1,y,z0],[x1,y,z1],[x0,y,z1]],mat,b,db,[0,dir,0]);
  const pZ=(out,dir,z,x0,x1,y0,y1,mat,b,db)=>faceO(out,[[x0,y0,z],[x1,y0,z],[x1,y1,z],[x0,y1,z]],mat,b,db,[0,0,dir]);
  // ---- level and role tags (the fleet's, verbatim) ---------------------------------------------------
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
    const glaze=(mk,want,ua,ub,va,vb,gb,cut)=>{ cut=cut||0.08;
      fo(rrect(ua-0.06,ub+0.06,va-0.055,vb+0.055,cut+0.03).map(([u,v])=>mk(0.03)(u,v)),'iron',-0.15,0.05,want);
      fo(rrect(ua,ub,va,vb,cut).map(([u,v])=>mk(0.065)(u,v)),'glas',gb,0.05,want); };
    const portX=(side,x,y,z,r)=>{ fo(ngon(8,r+0.07,(a,b)=>[x+side*0.03,y+a,z+b]),'steel',-0.2,0.05,[side,0,0]);
      fo(ngon(8,r,(a,b)=>[x+side*0.06,y+a,z+b]),'glas',-0.3,0.05,[side,0,0]); };
    const portY=(dir,y,x,z,r)=>{ fo(ngon(8,r+0.07,(a,b)=>[x+a,y+dir*0.03,z+b]),'steel',-0.2,0.05,[0,dir,0]);
      fo(ngon(8,r,(a,b)=>[x+a,y+dir*0.06,z+b]),'glas',-0.3,0.05,[0,dir,0]); };
    // a rail: stanchions at pts (their feet), a top rail h up and a mid rail half way
    const rail=(pts,h,mat,skip)=>{
      for(let i=0;i<pts.length;i++){ const p=pts[i]; tubeF(p,[p[0],p[1],p[2]+h],0.022,mat,-0.1);
        if(i+1<pts.length && !(skip&&skip(i))){ const q=pts[i+1];
          tubeF([p[0],p[1],p[2]+h],[q[0],q[1],q[2]+h],0.028,mat,0.15); tubeF([p[0],p[1],p[2]+h*0.5],[q[0],q[1],q[2]+h*0.5],0.018,mat,-0.1); } } };

    // ================= THE HULL =======================================================================
    TAG('hull','shell');
    // ---- shell: a level boot top, grey topsides with the cream stripe along them, the thick cream sheer band ----
    // (the old rig's bands: the stripe from 0.575 to 0.62 of the section's depth, the sheer band from 0.795 up)
    const bandsAt=(u)=>{ const st=station(u); return [0, fracAt(st,Z_BOOT), 0.575, 0.62, 0.795, 1]; };
    const BMAT=[['boot',0.3,0],['hull',1.0,0],['cove',0.9,0.01],['hull',1.0,0],['cove',1.0,0.01]];
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG, f0=bandsAt(u0), f1=bandsAt(u1);
        for(let k=0;k<BMAT.length;k++)
          fo([skin(side,u0,f0[k]),skin(side,u1,f1[k]),skin(side,u1,f1[k+1]),skin(side,u0,f0[k+1])],BMAT[k][0],BMAT[k][1],BMAT[k][2],[side,0,0]);
        // the rail cap along the whole sheer, in the band's paint
        const oa=skin(side,u0,1), ob=skin(side,u1,1), w=0.30;
        fo([oa,ob,[ob[0]-side*Math.min(w,Math.abs(ob[0])),ob[1],ob[2]-0.004],[oa[0]-side*Math.min(w,Math.abs(oa[0])),oa[1],oa[2]-0.004]],'cove',-0.5,0.03,[0,0,1]);
      }
    }
    for(let i=0;i<NSEG;i++){ const u0=i/NSEG, u1=(i+1)/NSEG;
      fo([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'boot',-1.0,0,[0,0,-1]); }
    { const f=bandsAt(1);                                  // the stem bar
      for(let k=0;k<BMAT.length;k++) fo([skin(-1,1,f[k]),skin(1,1,f[k]),skin(1,1,f[k+1]),skin(-1,1,f[k+1])],BMAT[k][0],BMAT[k][1]+0.25,BMAT[k][2],[0,1,0]); }
    // ---- the stern ramp: a sloped wet-steel floor with tread bars, slot walls, cheeks, the lip roller ---
    (function(){
      const yA=-L/2, seg=[[yA,-17.2,-0.45],[-17.2,-15.4,-0.25],[-15.4,RYT,-0.45]];
      for(const [y0,y1,b] of seg)
        fo([[-RW+0.06,y0,rampZ(y0)],[RW-0.06,y0,rampZ(y0)],[RW-0.06,y1,rampZ(y1)],[-RW+0.06,y1,rampZ(y1)]],'iron',b,-0.02,[0,0,1]);
      for(let y=-18.3; y<RYT-0.2; y+=0.72)                 // tread bars across the plating
        fo([[-RW+0.12,y,rampZ(y)+0.03],[RW-0.12,y,rampZ(y)+0.03],[RW-0.12,y+0.10,rampZ(y+0.10)+0.03],[-RW+0.12,y+0.10,rampZ(y+0.10)+0.03]],'iron',0.7,0,[0,0,1]);
      for(const s of [-1,1]){
        fo([[s*RW,yA,rampZ(yA)],[s*RW,RYT,rampZ(RYT)],[s*RW,RYT,DECK+0.02],[s*RW,yA,DECK+0.02]],'deck',s<0?-0.9:-0.6,-0.02,[-s,0,0]);
        fo([[s*RW,yA,DECK],[s*RW,-17.4,DECK],[s*RW,yA,4.72]],'deck',s<0?-1.0:-0.7,-0.02,[-s,0,0]);       // the cheek, up through the bulwark
      }
      fo([[-RW,RYT,rampZ(RYT)],[RW,RYT,rampZ(RYT)],[RW,RYT,DECK],[-RW,RYT,DECK]],'iron',0.2,0,[0,-1,0]);   // the ramp head's lip
    })();
    // ---- the transom: the paint bands, split round the ramp's mouth -----------------------------------
    (function(){
      const st0=station(0), sy=st0.y, xw=(f)=>lerp(st0.wb,st0.ws,f), zf=(f)=>st0.kz+f*st0.dep;
      const RF=(RZ0-st0.kz)/st0.dep, fb=bandsAt(0);
      const band=(f0,f1,mat,b)=>{
        if(f1<=f0+1e-6) return;
        if(f1<=RF){ fo([[-xw(f0),sy,zf(f0)],[xw(f0),sy,zf(f0)],[xw(f1),sy,zf(f1)],[-xw(f1),sy,zf(f1)]],mat,b,0.005,[0,-1,0]); return; }
        if(f0<RF){ band(f0,RF,mat,b); f0=RF; }
        for(const s of [-1,1])
          fo([[s*RW,sy,zf(f0)],[s*xw(f0),sy,zf(f0)],[s*xw(f1),sy,zf(f1)],[s*RW,sy,zf(f1)]],mat,b,0.005,[0,-1,0]);
      };
      for(let k=0;k<BMAT.length;k++) band(fb[k],fb[k+1],BMAT[k][0],BMAT[k][1]-0.5);
      for(const s of [-1,1]){
        fo([[s*RW,sy,4.75],[s*(st0.ws-TH),sy,4.75],[s*(st0.ws-TH),sy+0.30,4.746],[s*RW,sy+0.30,4.746]],'cove',-0.5,0.03,[0,0,1]);   // the covering board
        fo([[s*RW,sy+0.30,DECK],[s*dw(0.008),sy+0.30,DECK],[s*dw(0.008),sy+0.30,4.74],[s*RW,sy+0.30,4.74]],'deck',-0.9,0,[0,1,0]);   // its inner face
      }
    })();
    // ---- the break of the fo'c'sle: the bulkhead across the deck under the whaleback -----------------
    fo([[-dw(SOLE_U),BRK,DECK],[dw(SOLE_U),BRK,DECK],[FD[0].half,BRK,FD_Z0],[-FD[0].half,BRK,FD_Z0]],'cream',-0.5,0,[0,-1,0]);

    TAG('hull','trim');
    // ---- the rubbing strake: a proud belt at the deck line, transom to the break ----------------------
    for(const side of [-1,1]) for(let i=1;i<24;i++){
      const u0=i/NSEG, u1=(i+1)/NSEG, za=DECK-0.10, zb=DECK+0.10, o=0.07;
      const a0=onHull(side,u0,za,o), a1=onHull(side,u1,za,o), b0=onHull(side,u0,zb,o), b1=onHull(side,u1,zb,o);
      fo([a0,a1,b1,b0],'hull',0.2,0.02,[side,0,0]);
      fo([b0,b1,onHull(side,u1,zb,0),onHull(side,u0,zb,0)],'hull',1.2,0.02,[0,0,1]);
    }
    for(const side of [-1,1]){
      // freeing ports through the bulwark, over the strake
      for(const uc of [0.085,0.150,0.215,0.280,0.345,0.410,0.475,0.530]){ const du=0.42/L, z0=DECK+0.14, z1=DECK+0.36;
        fo([onHull(side,uc-du,z0,0.012),onHull(side,uc+du,z0,0.012),onHull(side,uc+du,z1,0.012),onHull(side,uc-du,z1,0.012)],'blk',-0.4,0.03,[side,0,0]); }
      // draft marks at the stern and the stem
      for(const uc of [0.045,0.930]) for(let k=0;k<3;k++){ const z0=2.05+k*0.27, du=0.13/L;
        fo([onHull(side,uc-du,z0,0.012),onHull(side,uc+du,z0,0.012),onHull(side,uc+du,z0+0.12,0.012),onHull(side,uc-du,z0+0.12,0.012)],'cream',0.2,0.03,[side,0,0]); }
      // the port letters and number on the bow, on the sheer band: black blocks, two and three
      { const zc=(u)=>sheerU(u)-0.64;
        [0.838,0.851,0.870,0.883,0.896].forEach((uc)=>{ const du=0.17/L, z=zc(uc);
          fo([onHull(side,uc-du,z-0.26,0.012),onHull(side,uc+du,z-0.26,0.012),onHull(side,uc+du,z+0.26,0.012),onHull(side,uc-du,z+0.26,0.012)],'blk',1.4,0.03,[side,0,0]);
          fo([onHull(side,uc-du*0.35,z-0.10,0.02),onHull(side,uc+du*0.35,z-0.10,0.02),onHull(side,uc+du*0.35,z+0.10,0.02),onHull(side,uc-du*0.35,z+0.10,0.02)],'cove',0.6,0.04,[side,0,0]); }); }
      // the hawse pipe and the anchor housed in it, in the grey between the stripe and the sheer band
      { const uc=0.935, z=sheerU(uc)-1.85, c=onHull(side,uc,z,0.02), dy=0.9/L;
        fo(ngon(8,0.27,(a,b)=>onHull(side,uc+a/L,z+b,0.015)),'blk',-0.6,0.03,[side,0,0]);
        fo([onHull(side,uc-0.09/L,z-0.85,0.05),onHull(side,uc+0.09/L,z-0.85,0.05),onHull(side,uc+0.09/L,z+0.05,0.05),onHull(side,uc-0.09/L,z+0.05,0.05)],'iron',0.5,0.05,[side,0,0]);
        fo([onHull(side,uc-dy*0.5,z-1.02,0.06),onHull(side,uc+dy*0.5,z-1.02,0.06),onHull(side,uc+dy*0.32,z-0.80,0.06),onHull(side,uc-dy*0.32,z-0.80,0.06)],'iron',0.2,0.05,[side,0,0]);
        void c; }
    }
    // the lip roller at the ramp's foot and the guide rollers at its head
    cylF([-1.55,-18.85,RZ0+0.14],[1.55,-18.85,RZ0+0.14],0.14,8,'steel',-0.2,0,2);
    for(const s of [-1,1]){ boxF([s*1.61,-18.85,RZ0+0.14],[0.05,0.10,0.16],'iron',0.1);
      cylF([s*(RW+0.02),RYT-0.12,DECK],[s*(RW+0.02),RYT-0.12,DECK+0.85],0.10,6,'steel',-0.1,0,1); }

    // ================= THE WORKING DECK ===============================================================
    TAG('main_deck','floor');
    { const U_RAMP=(RYT+L/2)/L, ASEG=5, BSEG=16;
      for(let i=0;i<ASEG;i++){                             // beside the ramp: a strip either side
        const u0=U0+(U_RAMP-U0)*i/ASEG, u1=U0+(U_RAMP-U0)*(i+1)/ASEG, y0=yOf(u0), y1=yOf(u1);
        for(const s of [-1,1]) fo([[s*RW,y0,DECK],[s*dw(u0),y0,DECK],[s*dw(u1),y1,DECK],[s*RW,y1,DECK]],'deck',-0.35,0,[0,0,1]);
      }
      for(let i=0;i<BSEG;i++){                             // forward of it: the trawl way down the middle, worn darker
        const u0=U_RAMP+(SOLE_U-U_RAMP)*i/BSEG, u1=U_RAMP+(SOLE_U-U_RAMP)*(i+1)/BSEG, y0=yOf(u0), y1=yOf(u1);
        const lane=y1<=HYa+0.2;
        for(const s of [-1,1]) fo([[s*RW,y0,DECK],[s*dw(u0),y0,DECK],[s*dw(u1),y1,DECK],[s*RW,y1,DECK]],'deck',-0.35,0,[0,0,1]);
        fo([[-RW,y0,DECK],[RW,y0,DECK],[RW,y1,DECK],[-RW,y1,DECK]],'deck',lane?-0.75:-0.35,0,[0,0,1]);
      }
    }
    TAG('main_deck','wall');
    // ---- the bulwark's inner face and its stanchions ---------------------------------------------------
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=Math.min(SOLE_U,(i+1)/NSEG); if(u1<=u0) break;
        const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb), LT=0.985;
        fo([skin(side,u1,fb,1),skin(side,u0,fa,1),skin(side,u0,LT,1),skin(side,u1,LT,1)],'deck',-0.9,-0.03,[-side,0,0]);
      }
      for(let y=-17.6; y<7.0; y+=1.9){ const x=dwY(y)+0.01, zt=sheerU(uOf(y))-0.06;
        fo([[side*x,y-0.04,DECK],[side*(x-0.26),y-0.04,DECK],[side*x,y-0.04,zt]],'deck',-0.3,0,[0,-1,0]);
        fo([[side*x,y+0.04,DECK],[side*(x-0.26),y+0.04,DECK],[side*x,y+0.04,zt]],'deck',0.2,0,[0,1,0]);
        fo([[side*(x-0.26),y-0.04,DECK],[side*(x-0.26),y+0.04,DECK],[side*x,y+0.04,zt],[side*x,y-0.04,zt]],'deck',0.5,0,[-side,0,0.3]); }
    }

    TAG('main_deck','fit');
    // ---- quarter bollards: twin posts on a foot ---------------------------------------------------------
    for(const s of [-1,1]){ boxF([s*2.6,-18.0,DECK+0.04],[0.14,0.30,0.04],'iron',0.1,-0.02);
      for(const dy of [-0.16,0.16]) cylF([s*2.6,-18.0+dy,DECK+0.06],[s*2.6,-18.0+dy,DECK+0.42],0.075,6,'iron',0.3,0,1,0.09); }
    // ---- the two fish hatches: a wood coaming, the dark hold in it (the lids are a part: state hatch) ---
    for(const H of HATCHES){ const zt=DECK+H.h, t=0.09;
      pY(F,-1,H.y-H.hl,-H.hw,H.hw,DECK,zt,'wood',0.3); pY(F,1,H.y+H.hl,-H.hw,H.hw,DECK,zt,'wood',0.3);
      pX(F,-1,-H.hw,H.y-H.hl,H.y+H.hl,DECK,zt,'wood',0.3); pX(F,1,H.hw,H.y-H.hl,H.y+H.hl,DECK,zt,'wood',0.3);
      for(const [x0,x1,y0,y1] of [[-H.hw,H.hw,H.y-H.hl,H.y-H.hl+t],[-H.hw,H.hw,H.y+H.hl-t,H.y+H.hl],[-H.hw,-H.hw+t,H.y-H.hl,H.y+H.hl],[H.hw-t,H.hw,H.y-H.hl,H.y+H.hl]])
        pZ(F,1,zt,x0,x1,y0,y1,'wood',0.5);
      // inside: the well's walls and the hold, seen only while the lid is up
      pY(F,1,H.y-H.hl+t,-H.hw+t,H.hw-t,DECK+0.03,zt,'dark',-0.2); pY(F,-1,H.y+H.hl-t,-H.hw+t,H.hw-t,DECK+0.03,zt,'dark',-0.6);
      pX(F,1,-H.hw+t,H.y-H.hl+t,H.y+H.hl-t,DECK+0.03,zt,'dark',-0.4); pX(F,-1,H.hw-t,H.y-H.hl+t,H.y+H.hl-t,DECK+0.03,zt,'dark',-0.4);
      pZ(F,1,DECK+0.03,-H.hw+t,H.hw-t,H.y-H.hl+t,H.y+H.hl-t,'dark',-0.4);
      for(const [x,y] of [[-0.55,-0.35],[0.25,-0.40],[-0.15,0.30],[0.65,0.25]]) pZ(F,1,DECK+0.05,x-0.32,x+0.32,H.y+y-0.22,H.y+y+0.22,'safe',-1.6);   // fish boxes in the gloom
      for(let k=0;k<2;k++) tubeF([H.hw-0.16,H.y+H.hl-t-0.02,DECK+0.08+k*0.12],[H.hw-0.50,H.y+H.hl-t-0.02,DECK+0.08+k*0.12],0.018,'steel',-0.4);   // the hold ladder's head
    }
    // ---- the split trawl winch: a drum either side of the house door, the brake stand between ------------
    for(const s of [-1,1]){ const Wn=WINCH, xi=s*(Wn.x-Wn.hx+0.12), xo=s*(Wn.x+Wn.hx-0.12), zc=DECK+0.74;
      boxF([s*Wn.x,Wn.y,DECK+0.09],[Wn.hx,Wn.hy,0.09],'iron',0.2,-0.02);                                     // the bed
      for(const x of [xi,xo]) boxF([x,Wn.y,DECK+0.46],[0.07,0.34,0.30],'iron',0.5);                          // the bearing pedestals
      cylF([xi,Wn.y,zc],[xo,Wn.y,zc],0.30,10,'steel',-0.9,0,0);                                               // the barrel, wound with warp
      for(let k=0;k<5;k++){ const x=lerp(xi,xo,(k+0.5)/5); cylF([x-s*0.02,Wn.y,zc],[x+s*0.02,Wn.y,zc],0.315,10,'steel',-1.5,0,0); }   // the lay of the wire
      for(const x of [xi+s*0.10,xo-s*0.10]) cylF([x-s*0.03,Wn.y,zc],[x+s*0.03,Wn.y,zc],0.52,10,'iron',0.9,0,2,null,0.5);   // the flanges
      boxF([s*(Wn.x-Wn.hx-0.02),Wn.y+0.02,DECK+0.52],[0.16,0.30,0.34],'iron',0.3,-0.02);                     // the gearbox
      cylF([xo,Wn.y,zc],[xo+s*0.20,Wn.y,zc],0.17,8,'steel',0.1,0,1);                                          // the warping head
      boxF([s*0.82,-0.30,DECK+0.42],[0.07,0.07,0.42],'iron',0.4); tubeF([s*0.82,-0.30,DECK+0.80],[s*0.82,-0.52,DECK+1.12],0.022,'steel',0.3);   // the brake lever
    }
    // ---- the warps' deck bollards: a sheave on a post either side ------------------------------------------
    for(const s of [-1,1]){ const Wp=WARP_POST; boxF([s*Wp.x,Wp.y,DECK+0.24],[0.10,0.10,0.24],'iron',0.4);
      cylF([s*Wp.x,Wp.y,DECK+0.48],[s*Wp.x,Wp.y,DECK+0.60],0.21,8,'steel',-0.2,0,1,null,0.2); }
    // ---- the net drum's stands and its drive (the drum and the net are parts: states drum, net) ----------
    for(const s of [-1,1]){ const D=DRUMG;
      boxF([s*D.stand,D.y,DECK+0.60],[0.12,0.16,0.60],'iron',0.3,-0.02);
      for(const dy of [-1,1]) fo([[s*D.stand-0.10,D.y+dy*0.16,DECK+1.05],[s*D.stand+0.10,D.y+dy*0.16,DECK+1.05],[s*D.stand+0.10,D.y+dy*0.62,DECK],[s*D.stand-0.10,D.y+dy*0.62,DECK]],'iron',dy<0?0.0:0.6,0,[0,dy,0.6]);
      boxF([s*D.stand,D.y,D.z],[0.15,0.20,0.20],'iron',0.7); }
    boxF([-2.02,DRUMG.y+0.66,DECK+0.36],[0.24,0.20,0.36],'iron',0.5,-0.02);
    cylF([-2.02,DRUMG.y+0.50,DECK+0.80],[-2.02,DRUMG.y+0.20,DRUMG.z-0.10],0.07,6,'steel',-0.3,0,0);
    // ---- fish totes stacked to starboard, the deck wash hose coiled to port --------------------------------
    for(const [x,y,n] of [[1.76,-6.05,3],[1.76,-6.85,2],[2.42,-6.05,1]]) for(let k=0;k<n;k++){
      F.push.apply(F, slab([x,y,DECK+0.15+k*0.31],[0.31,0.38,0.15],'safe',0.4,-0.3));
      pZ(F,1,DECK+0.302+k*0.31,x-0.25,x+0.25,y-0.32,y+0.32,'safe',-1.5,0.01); }
    { const c=[-2.30,-0.52], n=10;
      for(let k=0;k<n;k++){ const a0=2*Math.PI*k/n, a1=2*Math.PI*(k+1)/n, P=(r,a)=>[c[0]+r*Math.cos(a),c[1]+r*Math.sin(a),DECK+0.07];
        fo([P(0.20,a0),P(0.46,a0),P(0.46,a1),P(0.20,a1)],'iron',1.2,0.02,[0,0,1]); }
      tubeF([c[0]+0.46,c[1],DECK+0.07],[c[0]+0.46,c[1]+0.55,DECK+0.07],0.035,'steel',0.4); }
    // ---- the stairs to the fo'c'sle head: treads on two stringers, a handrail outboard -----------------
    for(const s of [-1,1]){ const A=ST_FD, run=(A.y1-A.y0)/A.n, rise=(FD_Z0-DECK)/A.n, xm=s*(A.xa+A.xb)/2, hx=(A.xb-A.xa)/2;
      for(let k=1;k<A.n;k++) F.push.apply(F, slab([xm,A.y0+(k+0.5)*run,DECK+k*rise-0.02],[hx,run/2,0.02],'deck',0.4,-0.6));
      for(const x of [s*A.xa,s*A.xb]) fo([[x,A.y0+run,DECK],[x,A.y1,FD_Z0-0.02],[x,A.y1,FD_Z0-0.28],[x,A.y0+run*1.9,DECK]],'iron',0.5,0,[x*s>(A.xa+A.xb)/2?s:-s,0,0]);
      rail([[s*(A.xb-0.03),A.y0+run,DECK+rise],[s*(A.xb-0.03),(A.y0+run+A.y1)/2,(DECK+rise+FD_Z0)/2],[s*(A.xb-0.03),A.y1,FD_Z0]],0.95,'steel'); }

    // ================= THE GEAR ALOFT (level rigging: no cut lifts it) ====================================
    TAG('rigging','rig');
    // ---- the stern gantry: a heavy plated arch grown straight out of the bulwarks, in the hull's paint -----
    (function(){ const G=GANT, lx=(z)=>G.x0+(G.x1-G.x0)*(z-DECK)/(G.zt-DECK), xa=lx(DECK), xb=lx(G.zt), w=G.lw, d=G.dyh;
      for(const s of [-1,1]){
        fo([[s*(xa+w),G.y-d,DECK],[s*(xa+w),G.y+d,DECK],[s*(xb+w),G.y+d,G.zt],[s*(xb+w),G.y-d,G.zt]],'hull',0.1,-0.02,[s,0,0.2]);     // the leg: outboard
        fo([[s*(xa-w),G.y-d,DECK],[s*(xa-w),G.y+d,DECK],[s*(xb-w),G.y+d,G.zt],[s*(xb-w),G.y-d,G.zt]],'hull',-0.3,-0.02,[-s,0,-0.2]);  // inboard
        fo([[s*(xa-w),G.y+d,DECK],[s*(xa+w),G.y+d,DECK],[s*(xb+w),G.y+d,G.zt],[s*(xb-w),G.y+d,G.zt]],'hull',-0.1,-0.02,[0,1,0]);     // forward
        fo([[s*(xa-w),G.y-d,DECK],[s*(xa+w),G.y-d,DECK],[s*(xb+w),G.y-d,G.zt],[s*(xb-w),G.y-d,G.zt]],'hull',-0.2,-0.02,[0,-1,0]);    // aft
        boxF([s*3.58,G.y+0.10,4.55],[0.28,G.cheekY,1.05],'hull',0,-0.02);                                     // the bulwark cheek: the sheer plating carried up into the leg
        boxF([s*1.25,G.y,G.beamZ-G.beamHz-0.22],[0.09,0.09,0.22],'iron',-0.2,-0.02);                          // the hanging blocks under the beam
        boxF([s*1.9,G.y+d+0.10,G.beamZ-0.12],[0.16,0.10,0.11],'steel',0.1); pY(F,1,G.y+d+0.21,s*1.9-0.12,s*1.9+0.12,G.beamZ-0.20,G.beamZ-0.04,'cream',1.2,0.02);   // the deck floodlights
        // the door's gallows: a plated arm off the leg, out over the rail, on a knee; the block hangs under its end
        const A=GAL, B=GAL_BLOCK[s<0?0:1], xi=lx(A.zt)+w-0.06, xo=Math.abs(B.x)+0.14;
        boxF([s*(xi+xo)/2,A.y,A.zt],[(xo-xi)/2,A.hy,0.13],'hull',0.1,-0.02);
        for(const dy of [-1,1]) fo([[s*(lx(5.60)+w-0.04),A.y+dy*A.hy,5.60],[s*(xo-0.06),A.y+dy*A.hy,A.zt-0.13],[s*xi,A.y+dy*A.hy,A.zt-0.13]],'hull',dy<0?-0.3:0,-0.02,[0,dy,0]);
        fo([[s*(lx(5.60)+w-0.04),A.y-A.hy,5.60],[s*(lx(5.60)+w-0.04),A.y+A.hy,5.60],[s*(xo-0.06),A.y+A.hy,A.zt-0.13],[s*(xo-0.06),A.y-A.hy,A.zt-0.13]],'hull',-0.6,-0.02,[s,0,-1]);
        boxF([B.x,B.y,A.zt-0.20],[0.05,0.05,0.10],'iron',0.2); boxF([B.x,B.y,B.z-0.06],[0.07,0.13,0.15],'iron',0.5,-0.02);   // the hanging block
        // the warp: off the winch's barrel, aft through the deck bollard, up over the rail to the door's block
        tubeF([s*(WINCH.x+0.35),WINCH.y-0.28,DECK+0.86],[s*(WARP_POST.x-0.02),WARP_POST.y+0.20,DECK+0.54],0.022,'steel',-0.4);
        tubeF([s*(WARP_POST.x+0.03),WARP_POST.y-0.20,DECK+0.54],[B.x,B.y+0.12,B.z],0.022,'steel',-0.5);
      }
      boxF([0,G.y,G.beamZ],[G.beamHx,d,G.beamHz],'hull',-0.1,-0.02);                                          // the deep box cross beam
      pY(F,1,G.y+d+0.02,-2.55,2.55,G.beamZ+0.10,G.beamZ+0.30,'cove',0.2,0.01);                                // the band that ties the arch into the hull's paint
      pY(F,-1,G.y-d-0.02,-2.55,2.55,G.beamZ+0.10,G.beamZ+0.30,'cove',0.2,0.01);
      boxF([0,G.y,G.beamZ-G.beamHz-0.10],[0.08,0.08,0.10],'iron',0.2); tubeF([0,G.y,G.beamZ-G.beamHz-0.20],[0,G.y,G.beamZ-G.beamHz-1.30],0.02,'steel',-0.3);   // the codend's lifting block and its whip
      boxF([0,G.y,G.beamZ-G.beamHz-1.40],[0.07,0.03,0.10],'iron',0.4);
      boxF([0,G.y,G.beamZ+G.beamHz+0.10],[0.10,0.10,0.10],'iron',0.3);                                        // the stern light, in its box on the beam
      pY(F,-1,G.y-0.11,-0.07,0.07,G.beamZ+G.beamHz+0.05,G.beamZ+G.beamHz+0.16,'cream',1.4,0.02);
    })();

    // ================= THE DECKHOUSE (level house) =========================================================
    TAG('house','wall',{ glas:'trim', iron:'trim', steel:'trim', safe:'fit', dark:'trim' });
    (function(){ const D=DOOR;
      pY(F,1,HYf,-HXl,HXl,DECK,HZ1l,'cream',0.1);
      pY(F,-1,HYa,-HXl,D.x0,DECK,HZ1l,'cream',-0.3); pY(F,-1,HYa,D.x1,HXl,DECK,HZ1l,'cream',-0.3); pY(F,-1,HYa,D.x0,D.x1,D.z1,HZ1l,'cream',-0.3);
      pX(F,-1,-HXl,HYa,HYf,DECK,HZ1l,'cream',-0.1); pX(F,1,HXl,HYa,HYf,DECK,HZ1l,'cream',-0.1);
      // the door's frame and sill
      for(const [xa,xb,za,zb] of [[D.x0-0.07,D.x0,DECK,D.z1+0.07],[D.x1,D.x1+0.07,DECK,D.z1+0.07],[D.x0,D.x1,D.z1,D.z1+0.07],[D.x0,D.x1,DECK,D.z0]])
        pY(F,-1,HYa-0.012,xa,xb,za,zb,'steel',-0.4,0.03);
      for(const x of [-1.25,1.25]) portY(-1,HYa,x,5.05,0.25);                                               // ports in the aft wall
      for(const side of [-1,1]){
        for(const y of [3.0,4.6,6.2,7.8]) portX(side,side*HXl,y,4.95,0.25);                                  // four ports a side
        const gr=[]; for(const y of [1.8,3.8,5.8,7.6,9.4]) gr.push([side*(HXl+0.09),y,5.72]);                // the grab rail
        for(let i=0;i+1<gr.length;i++) tubeF(gr[i],gr[i+1],0.025,'steel',0.2);
        for(const p of gr) tubeF([side*HXl,p[1],p[2]],p,0.02,'steel',-0.2);
        // a lifering in its cradle
        const P=(r,a)=>[side*(HXl+0.06),2.25+r*Math.cos(a),5.25+r*Math.sin(a)];
        for(let k=0;k<8;k++){ const a0=2*Math.PI*k/8, a1=2*Math.PI*(k+1)/8; fo([P(0.17,a0),P(0.34,a0),P(0.34,a1),P(0.17,a1)],k%2?'safe':'cream',k%2?0.3:0.6,0.05,[side,0,0]); }
      }
      // the ladder to the boat deck, on the aft wall to starboard
      { const lx=LADX, y=HYa-0.12;
        for(const s of [-1,1]) tubeF([lx+s*0.22,y,DECK+0.10],[lx+s*0.22,y,WZ0+0.95],0.03,'steel',-0.1);
        for(let z=DECK+0.38; z<WZ0; z+=0.31) tubeF([lx-0.19,y+0.02,z],[lx+0.19,y+0.02,z],0.022,'steel',-0.2);
        for(const z of [DECK+1.2,HZ1l-0.3]) for(const s of [-1,1]) tubeF([lx+s*0.22,y,z],[lx+s*0.22,HYa,z],0.02,'steel',-0.3); }
      // the hose reel on the aft wall to port
      cylF([-2.10,HYa-0.02,4.55],[-2.10,HYa-0.26,4.55],0.34,8,'iron',0.6,0,1,null,0.3);
      cylF([-2.10,HYa-0.06,4.55],[-2.10,HYa-0.22,4.55],0.27,8,'buff',-0.4,0,0);
    })();
    // ---- the boat deck: the house's top, the wheelhouse's sole (level bridge: it lifts with the wheelhouse)
    TAG('bridge','floor',{ cream:'trim' });
    { const x=HXl+0.10, ya=HYa-0.08, yf=HYf+0.08;
      pZ(F,1,WZ0,-x,x,ya,yf,'deck',0.25);
      pY(F,-1,ya,-x,x,HZ1l,WZ0,'cream',0.5); pY(F,1,yf,-x,x,HZ1l,WZ0,'cream',0.5);
      pX(F,-1,-x,ya,yf,HZ1l,WZ0,'cream',0.5); pX(F,1,x,ya,yf,HZ1l,WZ0,'cream',0.5);
      pZ(F,-1,HZ1l,-x,x,ya,yf,'cream',-1.6); }
    TAG('bridge','fit',{ buff:'tall', blk:'tall' });
    { const x=HXl+0.05, ya=HYa-0.03, yf=HYf+0.03, z=WZ0, P=[];
      // rails round the boat deck; the gap in the aft run is the ladder's head
      for(const xx of [LADX+0.26,3.15]) P.push([Math.min(xx,x),ya,z]);
      for(const y of [2.9,4.4,5.9,7.4,8.6]) P.push([x,y,z]);
      for(const xx of [3.15,1.3,0,-1.3,-3.15]) P.push([Math.max(-x,Math.min(x,xx)),yf,z]);
      for(const y of [8.6,7.4,5.9,4.4,2.9]) P.push([-x,y,z]);
      for(const xx of [-3.15,-1.6,-0.3,1.0,LADX-0.26]) P.push([Math.max(-x,xx),ya,z]);
      rail(P,1.0,'steel');
      // the liferaft canisters in their cradles on the wings
      for(const s of [-1,1]){ const xr=s*(HXl-0.13);
        for(const y of [4.55,5.15]) boxF([xr,y,z+0.07],[0.18,0.04,0.07],'iron',0.3);
        cylF([xr,4.32,z+0.34],[xr,5.38,z+0.34],0.22,8,'cream',0.2,0,2,null,0.5);
        for(const y of [4.62,5.08]) cylF([xr,y-0.03,z+0.34],[xr,y+0.03,z+0.34],0.232,8,'iron',0.4,0,0); }
      // the funnel: buff, raked, a band of the hull's paint, a black top, the exhausts standing out of it
      const Fn=FUNNEL, at=(t)=>[0,lerp(Fn.y0,Fn.y1,t),lerp(z,Fn.zt,t)];
      cylF(at(0),at(0.60),Fn.r,10,'buff',0,0,0,Fn.r*0.95);
      cylF(at(0.60),at(0.74),Fn.r*0.97,10,'hull',0.2,0,0,Fn.r*0.94);
      cylF(at(0.74),at(1),Fn.r*0.94,10,'blk',1.6,0,1,Fn.r*0.90,0.6);
      for(const dx of [-0.16,0.16]) cylF([dx,Fn.y1+0.02,Fn.zt-0.1],[dx,Fn.y1-0.10,Fn.zt+0.42],0.075,6,'iron',0.6,0,1,null,-0.5);
      boxF([0,Fn.y0+0.02,z+0.05],[Fn.r+0.10,Fn.r+0.10,0.05],'iron',0.5,-0.02);
    }

    // ================= THE WHEELHOUSE (level bridge) =======================================================
    TAG('bridge','wall',{ glas:'trim', iron:'trim', steel:'trim', safe:'fit' });
    (function(){ const D=WDOOR;
      // the sides lean out as they rise (HXw0 at the sole, HXw1 at the head); the front's head leans out over the bow
      fo([[-HXw0,WYa,WZ0],[-HXw1,WYa,WZ1],[-HXw1,FYt,WZ1],[-HXw0,FYb,WZ0]],'cream',-0.1,0,[-1,0,-0.2]);
      fo([[HXw0,WYa,WZ0],[HXw0,FYb,WZ0],[HXw1,FYt,WZ1],[HXw1,WYa,WZ1]],'cream',-0.1,0,[1,0,-0.2]);
      fo([[-HXw1,FYt,WZ1],[HXw1,FYt,WZ1],[HXw0,FYb,WZ0],[-HXw0,FYb,WZ0]],'cream',0.2,0,[0,1,-0.28]);
      fo([[-HXw0,WYa,WZ0],[D.x0,WYa,WZ0],[D.x0,WYa,WZ1],[-HXw1,WYa,WZ1]],'cream',-0.3,0,[0,-1,0]);
      fo([[D.x1,WYa,WZ0],[HXw0,WYa,WZ0],[HXw1,WYa,WZ1],[D.x1,WYa,WZ1]],'cream',-0.3,0,[0,-1,0]);
      pY(F,-1,WYa,D.x0,D.x1,D.z1,WZ1,'cream',-0.3);
      for(const [xa,xb,za,zb] of [[D.x0-0.06,D.x0,WZ0,D.z1+0.06],[D.x1,D.x1+0.06,WZ0,D.z1+0.06],[D.x0,D.x1,D.z1,D.z1+0.06],[D.x0,D.x1,WZ0,D.z0]])
        pY(F,-1,WYa-0.012,xa,xb,za,zb,'steel',-0.4,0.03);
      const ry=(WZ1-WZ0), rz=(FYb-FYt), rn=Math.hypot(ry,rz), nY=ry/rn, nZ=rz/rn;
      for(const [xa,xb] of [[-2.06,-1.34],[-1.20,-0.48],[-0.36,0.36],[0.48,1.20],[1.34,2.06]])              // the five-pane windscreen
        glaze((pr)=>((x,z)=>[x, yFront(z)+nY*pr, z+nZ*pr]), [0,nY,nZ], xa,xb, 7.45,8.45, 0.3, 0.06);
      const sh=Math.hypot(WZ1-WZ0,HXw1-HXw0), sc=(WZ1-WZ0)/sh, ss=(HXw1-HXw0)/sh;                            // the leaning side's normal: (side*sc, 0, -ss)
      for(const side of [-1,1]) for(const [ya,yb] of [[4.05,5.05],[5.45,6.30],[6.62,7.42],[7.74,8.48]])     // four windows a side
        glaze((pr)=>((y,z)=>[side*(xwz(z)+sc*pr),y,z-ss*pr]), [side*sc,0,-ss], ya,yb, 7.45,8.35, -0.3);
      for(const [xa,xb] of [[-2.06,-0.90],[-0.60,0.62],[1.84,2.10]])                                         // the trawl-watching windows aft
        glaze((pr)=>((x,z)=>[x,WYa-pr,z]), [0,-1,0], xa,xb, 7.45,8.35, -0.3);
      // sidelights on their screens: red to port, green to starboard (no green ramp: the glass's)
      for(const side of [-1,1]){ const xs=xwz(8.76); boxF([side*(xs+0.16),6.45,8.62],[0.14,0.30,0.02],'iron',0.3); pX(F,side,side*(xs+0.04),6.15,6.75,8.62,8.90,'iron',0.6,0.03);
        boxF([side*(xs+0.16),6.45,8.72],[0.07,0.08,0.07],side<0?'safe':'glas',side<0?0.6:1.5); }
    })();
    // ---- the wheelhouse top (level top): the roof and its brow, then what stands on it ---------------------
    TAG('top','roof');
    { const x=HXw1+0.14, ya=WYa-0.14, yf=FYt+0.34;
      pZ(F,1,ROOFZ,-x,x,ya,yf,'cream',0.45);
      pY(F,-1,ya,-x,x,WZ1,ROOFZ,'cream',0.3); pY(F,1,yf,-x,x,WZ1,ROOFZ,'cream',0.6);
      pX(F,-1,-x,ya,yf,WZ1,ROOFZ,'cream',0.3); pX(F,1,x,ya,yf,WZ1,ROOFZ,'cream',0.3);
      pZ(F,-1,WZ1,-x,x,ya,yf,'cream',-1.8); }
    TAG('top','fit',{ buff:'rig' });
    (function(){ const z=ROOFZ;
      // the searchlight, the horn, the satellite dome, two whips
      cylF([1.55,8.55,z],[1.55,8.55,z+0.30],0.05,5,'steel',0,0,0); cylF([1.55,8.42,z+0.42],[1.55,8.74,z+0.42],0.15,8,'steel',-0.1,0,2,null,0.2);
      pY(F,1,8.745,1.44,1.66,z+0.31,z+0.53,'cream',1.2,0.02);
      cylF([-1.5,8.2,z+0.16],[-1.5,8.75,z+0.16],0.06,6,'steel',0.1,0,1,0.11);
      cylF([-1.65,4.3,z],[-1.65,4.3,z+0.26],0.24,8,'cream',0.5,0,1,0.20,0.9);
      for(const s of [-1,1]){ tubeF([s*(HXw1-0.10),3.6,z],[s*(HXw1-0.10),3.45,z+3.1],0.018,'steel',0.2); boxF([s*(HXw1-0.10),3.6,z+0.06],[0.05,0.05,0.06],'iron',0.3); }
      // the radar mast: a post and two legs, the scanner on its platform, the yard and its lights
      const my=5.35, top=z+2.85;
      cylF([0,my,z],[0,my,top],0.09,6,'buff',0,0,0);
      for(const s of [-1,1]) cylF([s*0.85,my-1.05,z],[0,my-0.05,z+1.85],0.055,5,'buff',-0.2,0,0);
      boxF([0,my+0.42,z+1.22],[0.42,0.46,0.03],'steel',0.2); tubeF([0,my+0.80,z+0.72],[0,my+0.06,z+1.20],0.03,'buff',-0.2);
      cylF([0,my+0.50,z+1.25],[0,my+0.50,z+1.45],0.11,6,'steel',0,0,1);
      boxF([0,my+0.50,z+1.52],[0.85,0.07,0.06],'cream',0.6);                                                 // the scanner
      tubeF([-1.25,my,z+2.25],[1.25,my,z+2.25],0.035,'buff',0.1);                                            // the yard
      for(const s of [-1,1]) boxF([s*1.15,my,z+2.36],[0.06,0.06,0.07],'iron',0.4);
      boxF([0,my,top+0.08],[0.08,0.08,0.08],'cream',1.0); tubeF([0,my,top+0.16],[0,my,top+0.70],0.015,'steel',0.2);   // the masthead light, the wind vane's spindle
      boxF([0,my+0.14,z+2.02],[0.07,0.07,0.08],'cream',0.9);
    })();
    TAG('top','rig');
    tubeF([0,5.35,ROOFZ+2.80],[0,FD[FD.length-1].y-0.25,FD[FD.length-1].z+1.30],0.015,'steel',-0.2);        // the forestay to the jackstaff
    tubeF([0,5.35,ROOFZ+2.80],[0,GANT.y,GANT.beamZ+GANT.beamHz+0.20],0.015,'steel',-0.3);                    // the triatic stay to the gantry's beam

    // ================= THE FO'C'SLE HEAD (level foredeck) ==================================================
    TAG('foredeck','floor');
    for(let i=0;i+1<FD.length;i++){ const A=FD[i], B=FD[i+1];
      fo([[-A.half,A.y,A.z],[A.half,A.y,A.z],[B.half,B.y,B.z],[-B.half,B.y,B.z]],'deck',-0.35,0,[0,0,1]); }
    TAG('foredeck','fit',{ deck:'trim' });
    (function(){ const zAt=(y)=>fdAt(y).z;
      // the anchor windlass: a bed, two gypsies, the warping ends, the motor; the cables run forward to the hawse
      const wy=14.0, wz=zAt(wy);
      boxF([0,wy,wz+0.08],[0.78,0.42,0.08],'iron',0.3,-0.02);
      for(const s of [-1,1]){ boxF([s*0.26,wy,wz+0.36],[0.06,0.30,0.22],'iron',0.5);
        cylF([s*0.32,wy,wz+0.48],[s*0.62,wy,wz+0.48],0.27,8,'steel',-0.6,0,2,null,0.0);
        cylF([s*0.62,wy,wz+0.48],[s*0.95,wy,wz+0.48],0.15,8,'steel',0.2,0,1,0.20);
        tubeF([s*0.47,wy+0.25,wz+0.52],[s*0.55,16.6,zAt(16.6)+0.10],0.04,'iron',0.6);
        boxF([s*0.55,16.65,zAt(16.65)+0.08],[0.13,0.16,0.08],'iron',0.4,-0.02); }
      boxF([0,wy-0.05,wz+0.36],[0.20,0.26,0.20],'steel',-0.1);
      // the fore hatch to the fo'c'sle store
      const hy=11.5, hz=zAt(hy);
      boxF([0,hy,hz+0.14],[0.45,0.45,0.14],'iron',0.9); F.push.apply(F, slab([0,hy,hz+0.31],[0.50,0.50,0.03],'deck',0.5,0.0));
      boxF([0,hy-0.42,hz+0.36],[0.08,0.03,0.02],'steel',0.2);
      // the bow bollards and the samson post (the cleats' places)
      for(const s of [-1,1]){ const bz=zAt(12.6); boxF([s*1.1,12.6,bz+0.03],[0.13,0.28,0.03],'iron',0.1,-0.02);
        for(const dy of [-0.15,0.15]) cylF([s*1.1,12.6+dy,bz+0.05],[s*1.1,12.6+dy,bz+0.40],0.07,6,'iron',0.3,0,1,0.085); }
      const sp=SAMSON; boxF([sp.x,sp.y,sp.z-0.02],[0.09,0.09,0.20],'iron',0.3); tubeF([-0.24,sp.y,sp.z+0.06],[0.24,sp.y,sp.z+0.06],0.035,'iron',0.6);
      // the jackstaff at the stem head
      const Z=FD[FD.length-1]; tubeF([0,Z.y-0.25,Z.z],[0,Z.y-0.25,Z.z+1.30],0.025,'steel',0.2);
      // rails round the fo'c'sle head, from the stairs' heads to the stem
      for(const s of [-1,1]){ const P=[]; for(let i=0;i<FD.length;i+=2){ const A=FD[i]; if(A.half<0.25) break; P.push([s*(A.half-0.08),A.y+(i?0:0.04),A.z]); }
        rail(P,1.0,'steel'); }
    })();
    TAG();
  })();

  // ================= THE PARTS THAT MOVE =================================================================
  const r3=(v)=>Math.round(v*1000)/1000;
  // ---- the trawl doors (tag 2; state doors 0..1): stowed hanging at the gantry arms' blocks, outboard of the rail;
  //      lowered down the ship's side to the water on their warps
  const TD = { len:2.30, hgt:1.35, zStow:5.45, zDown:1.55 };
  function trawlDoorFaces(t, out){
    const from=out.length;
    for(const s of [-1,1]){ const B=GAL_BLOCK[s<0?0:1], zc=lerp(TD.zStow,TD.zDown,t), x=B.x+s*0.02, hy=TD.len/2, hz=TD.hgt/2;
      const P=(dy,dz,o)=>[x+s*o, B.y+dy, zc+dz];
      // the plate: an oval-ish board, its corners cut, iron-shod along the keel
      const o8=[[-hy+0.25,-hz],[hy-0.45,-hz],[hy,-hz+0.45],[hy,hz-0.30],[hy-0.30,hz],[-hy+0.25,hz],[-hy,hz-0.25],[-hy,-hz+0.25]];
      faceO(out,o8.map(([a,b])=>P(a,b,0.07)),'wood',0.2,0,[s,0,0]);
      faceO(out,o8.map(([a,b])=>P(a,b,-0.03)),'wood',-0.8,0,[-s,0,0]);
      faceO(out,[P(-hy+0.25,hz,-0.03),P(hy-0.30,hz,-0.03),P(hy-0.30,hz,0.07),P(-hy+0.25,hz,0.07)],'iron',0.8,0,[0,0,1]);
      faceO(out,[P(-hy+0.20,-hz,0.085),P(hy-0.40,-hz,0.085),P(hy-0.05,-hz+0.40,0.085),P(hy-0.05,-hz+0.62,0.085),P(-hy+0.20,-hz+0.22,0.085)],'iron',0.4,0.02,[s,0,0]);   // the shoe
      for(const dy of [-0.55,0.10,0.70]) faceO(out,[P(dy-0.035,-hz+0.22,0.085),P(dy+0.035,-hz+0.22,0.085),P(dy+0.035,hz-0.04,0.085),P(dy-0.035,hz-0.04,0.085)],'steel',-0.3,0.02,[s,0,0]);   // the straps
      // the brackets and the warp up to the block
      for(const f of tube(P(-0.45,0.10,0.09),P(0,hz+0.30,0.10),0.02,'steel',-0.2)) out.push(f);
      for(const f of tube(P(0.45,0.10,0.09),P(0,hz+0.30,0.10),0.02,'steel',-0.2)) out.push(f);
      for(const f of tube(P(0,hz+0.30,0.10),[B.x,B.y,B.z-0.18],0.022,'steel',-0.5)) out.push(f);
    }
    stampTags(out, from, out.length, 'rigging', 'part');
    for(let i=from;i<out.length;i++) out[i].tag=2;
  }
  // ---- the net drum (tag 3; state drum, deg) and the net (tag 4; state net 0..1) ------------------------
  function drumFaces(deg, net, out){
    const D=DRUMG, rot=(deg||0)*DEG, from=out.length, push=(a)=>{ for(const f of a) out.push(f); };
    push(cyl([-D.hx,D.y,D.z],[D.hx,D.y,D.z],D.rBare,10,'buff',-0.3,0,0,null,null,rot));                       // the barrel
    for(const s of [-1,1]){
      push(cyl([s*(D.hx-0.10),D.y,D.z],[s*D.hx,D.y,D.z],D.rFl,12,'buff',0.2,0,2,null,0.5,rot));              // the flanges
      for(let k=0;k<4;k++){ const a=rot+k*Math.PI/2+0.4, c=Math.cos(a), sn=Math.sin(a), r0=0.36, r1=0.62, w=0.16;   // their lightening holes turn with it
        const Q=(r,d)=>[s*(D.hx+0.012), D.y+r*c-d*sn, D.z+r*sn+d*c];
        faceO(out,[Q(r0,-w*0.7),Q(r1,-w),Q(r1,w),Q(r0,w*0.7)],'iron',-0.2,0.03,[s,0,0]); }
      push(cyl([s*D.hx,D.y,D.z],[s*(D.stand-0.12),D.y,D.z],0.10,6,'steel',0,0,0));                           // the shaft to its bearing
    }
    stampTags(out, from, out.length, 'main_deck', 'fit');
    for(let i=from;i<out.length;i++) out[i].tag=3;
    const at=out.length, wound=1-net, r=lerp(D.rBare+0.03, D.rFull, wound);
    if(wound>0.02){
      push(cyl([-(D.hx-0.13),D.y,D.z],[D.hx-0.13,D.y,D.z],r,12,'net',0.2,0,2,null,-0.4,rot));                 // the net wound on
      for(const x of [-1.25,-0.45,0.35,1.15]) push(cyl([x,D.y,D.z],[x+0.16,D.y,D.z],r+0.035,12,'safe',0.1,0,0,null,null,rot));   // floats wound in with it
    }
    // the codend, heaped at the ramp head while the net is aboard
    if(wound>0.05){ const k=0.35+0.65*wound, y0=-13.45, y1=y0+1.15*k, hx=1.25*k, h=0.50*k;
      faceO(out,[[-hx,y0,DECK],[hx,y0,DECK],[hx*0.7,y0+0.12,DECK+h],[-hx*0.7,y0+0.12,DECK+h]],'net',-0.6,0,[0,-1,0.3]);
      faceO(out,[[-hx*0.7,y0+0.12,DECK+h],[hx*0.7,y0+0.12,DECK+h],[hx*0.6,y1-0.15,DECK+h*0.85],[-hx*0.6,y1-0.15,DECK+h*0.85]],'net',0.5,0,[0,0,1]);
      faceO(out,[[-hx*0.6,y1-0.15,DECK+h*0.85],[hx*0.6,y1-0.15,DECK+h*0.85],[hx,y1,DECK],[-hx,y1,DECK]],'net',0.1,0,[0,1,0.3]);
      for(const s of [-1,1]) faceO(out,[[s*hx,y0,DECK],[s*hx,y1,DECK],[s*hx*0.6,y1-0.15,DECK+h*0.85],[s*hx*0.7,y0+0.12,DECK+h]],'net',s<0?-0.2:-0.9,0,[s,0,0.3]);
      for(const [fx,fy] of [[-0.55,0.30],[0.10,0.55],[0.60,0.25],[-0.15,0.80]]) push(box([fx*k,y0+fy*k,DECK+h+0.05],[0.11,0.11,0.07],'safe',0.3));   // floats on the heap
      // the bridle from the heap up to the drum
      faceO(out,[[-0.5*k,y1-0.20,DECK+h*0.8],[0.5*k,y1-0.20,DECK+h*0.8],[0.9,D.y-r*0.75,D.z-r*0.55],[-0.9,D.y-r*0.75,D.z-r*0.55]],'net',-0.2,0,[0,-0.5,1]);
    }
    // the net shot: off the drum, over the ramp head, down the ramp and out over the lip
    if(net>0.02){ const yEnd=lerp(RYT+0.2,-L/2-0.9,net), hw=1.15, zr=(y)=>Math.max(rampZ(Math.max(y,-L/2)),RZ0)+0.06, r0=Math.max(r,D.rBare+0.05);
      const pts=[[D.y-r0*0.8,D.z-r0*0.5],[RYT+0.25,DECK+0.10]];
      for(let y=RYT-0.4; y>yEnd; y-=0.9) pts.push([y,zr(y)]); pts.push([yEnd, yEnd<-L/2 ? RZ0-0.25 : zr(yEnd)]);
      for(let i=0;i+1<pts.length;i++){ const A=pts[i], B=pts[i+1];
        faceO(out,[[-hw,A[0],A[1]],[hw,A[0],A[1]],[hw,B[0],B[1]],[-hw,B[0],B[1]]],'net',i%2?0.3:-0.1,0.02,[0,0,1]);
        if(i>0) for(const s of [-1,1]) push(box([s*(hw-0.05),(A[0]+B[0])/2,(A[1]+B[1])/2+0.07],[0.09,0.12,0.07],'safe',0.3)); }
    }
    stampTags(out, at, out.length, 'main_deck', 'fit');
    for(let i=at;i<out.length;i++) out[i].tag=4;
  }
  // ---- the fish hatches' lids (tag 5; state hatch 0..1): each hinged on its coaming's forward edge; boards under
  //      a dark tarpaulin, battened down with wood
  function hatchFaces(t, out){
    const from=out.length;
    for(const Hh of HATCHES){
      const a=t*Hh.maxDeg*DEG, c=Math.cos(a), sn=Math.sin(a), yh=Hh.y+Hh.hl+0.04, zh=DECK+Hh.h+0.012;
      const xf=(p)=>[p[0], yh+p[1]*c+p[2]*sn, zh-p[1]*sn+p[2]*c];
      const len=2*Hh.hl+0.10, hw=Hh.hw+0.06;
      for(const f of slab([0,-len/2,0.03],[hw,len/2,0.03],'dark',2.6,1.6,xf)) out.push(f);
      faceO(out,[[-hw,0,0],[hw,0,0],[hw,-len,0],[-hw,-len,0]].map(xf),'wood',-0.6,0,xf([0,-len/2,-1]).map((v,i)=>v-xf([0,-len/2,0])[i]));   // its underside: the boards
      for(const dx of [-0.80,0,0.80]) for(const f of slab([dx,-len/2,0.075],[0.045,len/2-0.06,0.015],'wood',0.2,-0.2,xf)) out.push(f);   // the battens
      for(const f of slab([0,-len+0.14,0.085],[0.14,0.03,0.025],'steel',0.5,0,xf)) out.push(f);                // the lifting handle
    }
    stampTags(out, from, out.length, 'main_deck', 'fit');
    for(let i=from;i<out.length;i++) out[i].tag=5;
  }
  // ---- the leaves: weather-tight steel doors, hinged on a jamb, swinging aft ----------------------------
  const leafHinge=(D)=>({ x:D.hinge==='port'?D.x0:D.x1, y:D.y-0.015, sg:D.hinge==='port'?1:-1 });
  function leafFaces(D, t, out){
    const Hn=leafHinge(D), a=t*D.maxDeg*DEG, dx=Hn.sg*Math.cos(a), dy=-Math.sin(a), nx=-Hn.sg*Math.sin(a), ny=-Math.cos(a);
    const w=D.x1-D.x0, z0=D.z0-0.03, z1=D.z1+0.04, from=out.length;
    const P=(s,z,o)=>[Hn.x+dx*s+nx*o, Hn.y+dy*s+ny*o, z];
    const N=[nx,ny,0], Dd=[dx,dy,0];
    faceO(out,[P(-0.04,z0,0.05),P(w+0.04,z0,0.05),P(w+0.04,z1,0.05),P(-0.04,z1,0.05)],'cream',-0.5,0.02,N);            // outside
    faceO(out,[P(-0.04,z0,0),P(w+0.04,z0,0),P(w+0.04,z1,0),P(-0.04,z1,0)],'cream',-1.2,0,[-nx,-ny,0]);                 // inside
    faceO(out,[P(w+0.04,z0,0),P(w+0.04,z0,0.05),P(w+0.04,z1,0.05),P(w+0.04,z1,0)],'steel',-0.2,0,Dd);                  // the free edge
    faceO(out,[P(-0.04,z1,0),P(w+0.04,z1,0),P(w+0.04,z1,0.05),P(-0.04,z1,0.05)],'steel',0.3,0,[0,0,1]);
    const zp=lerp(D.z0,D.z1,0.70);
    faceO(out,ngon(8,0.19,(u,v)=>P(w/2+u,zp+v,0.06)),'steel',-0.2,0.03,N);                                             // the port light
    faceO(out,ngon(8,0.13,(u,v)=>P(w/2+u,zp+v,0.07)),'glas',-0.3,0.04,N);
    for(const zz of [D.z0+0.25,D.z1-0.22]) faceO(out,[P(w-0.16,zz-0.03,0.065),P(w-0.02,zz-0.03,0.065),P(w-0.02,zz+0.03,0.065),P(w-0.16,zz+0.03,0.065)],'steel',0.3,0.03,N);   // the dogs
    faceO(out,[P(w-0.14,lerp(D.z0,D.z1,0.45)-0.10,0.065),P(w-0.09,lerp(D.z0,D.z1,0.45)-0.10,0.065),P(w-0.09,lerp(D.z0,D.z1,0.45)+0.10,0.065),P(w-0.14,lerp(D.z0,D.z1,0.45)+0.10,0.065)],'steel',0.6,0.03,N);   // the handle
    for(const zz of [D.z0+0.20,D.z1-0.20]) faceO(out,[P(-0.04,zz-0.07,0.065),P(0.10,zz-0.07,0.065),P(0.10,zz+0.07,0.065),P(-0.04,zz+0.07,0.065)],'iron',0.2,0.03,N);          // the hinges
    stampTags(out, from, out.length, D.lv, 'wall', { glas:'trim', steel:'trim', iron:'trim' }, false);
  }

  function stateOf(o){
    o=o||{};
    return { doors:clamp01(+o.doors||0), drum:+o.drum||0, net:clamp01(+o.net||0), hatch:clamp01(+o.hatch||0),
             door:clamp01(+(o.doorOpen!=null?o.doorOpen:o.door)||0) };
  }
  const _asm=new Map();
  function assemble(s){
    const key=[r3(s.doors),Math.round(((s.drum%360)+360)%360*10)/10,r3(s.net),r3(s.hatch)].join('|');
    if(_asm.has(key)) return _asm.get(key);
    const dyn=[], SH=[];
    trawlDoorFaces(s.doors, dyn);
    drumFaces(s.drum, s.net, dyn);
    hatchFaces(s.hatch, dyn);
    // cast shade along the key: the boat deck's on the working deck and the side decks, the wheelhouse top's on the boat deck
    { const x=HXl+0.10, ya=HYa-0.08, yf=HYf+0.08;
      SH.push({ v:[[-x,ya,WZ0],[x,ya,WZ0],[x,yf,WZ0],[-x,yf,WZ0]], z0:DECK, steps:1, near:0.30, clip:[-4.45,4.45,-6.5,BRK] });
      const xr=HXw1+0.14, yra=WYa-0.14, yrf=FYt+0.34;
      SH.push({ v:[[-xr,yra,ROOFZ],[xr,yra,ROOFZ],[xr,yrf,ROOFZ],[-xr,yrf,ROOFZ]], z0:WZ0, steps:1, near:0.30, clip:[-x,x,ya,yf] });
      const G=GANT, bx=G.beamHx; SH.push({ v:[[-bx,G.y-G.dyh,G.beamZ],[bx,G.y-G.dyh,G.beamZ],[bx,G.y+G.dyh,G.beamZ],[-bx,G.y+G.dyh,G.beamZ]], z0:DECK, steps:1, near:0.20, clip:[-4.2,4.2,-18.9,-6.0] }); }
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
    return _toRGBA(_paint(A.F.concat(doorFaces(s.door)), Object.assign({}, opts, {dir}), true, A.SH));   // the leaves: doorFaces(t)
  }
  // layers: painted WITH the hull, then masked to their own pixels (2 trawl doors, 3 drum, 4 net, 5 hatch lid)
  function layer(tagN){ return function(dir, opts){
    opts=norm(opts);
    const s=stateOf(opts), A=assemble(s);
    return _toRGBA(_paint(A.F.concat(doorFaces(s.door)), Object.assign({}, opts, {dir, onlyTag:tagN}), true));
  }; }
  const renderTrawlDoors=layer(2), renderDrum=layer(3), renderNet=layer(4), renderHatch=layer(5);

  // ---- the old rig's screen anchors, kept (cell pixels at a heading; pass the hull's rock pose in opts) ----
  const _scr=(dir,opts,pts)=>{ const B=camBasis(Object.assign({}, norm(opts), {dir})); return pts.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return { x:p.sx, y:p.sy }; }); };
  function helmSeat(dir, opts){ return _scr(dir,opts,[HELM])[0]; }
  const GANTRY = [ { x:-1.25, y:GANT.y, z:7.90 }, { x:1.25, y:GANT.y, z:7.90 } ];   // the gantry's two hanging blocks, as before
  function gantryMounts(dir, opts){ return _scr(dir,opts,GANTRY); }
  const GALLOWS = GAL_BLOCK.map(b=>({ x:b.x, y:b.y, z:b.z }));    // the door blocks on the gantry's arms (the old rig had no gallows: it answered with the gantry's)
  function gallowsMounts(dir, opts){ return _scr(dir,opts,GALLOWS); }
  const HAULER = GANTRY[0];
  function haulerMount(dir, opts){ return gantryMounts(dir, opts)[0]; }
  const DRUM = { x:0, y:DRUMG.y, z:DRUMG.z };
  function drumMount(dir, opts){ return _scr(dir,opts,[DRUM])[0]; }
  const TUBS = [ {x:-2.4,y:-1.2,z:DECK}, {x:2.4,y:-1.2,z:DECK}, {x:-2.4,y:-8.8,z:DECK}, {x:2.4,y:-8.8,z:DECK}, {x:0,y:-9.4,z:DECK} ];
  function tubMounts(dir, opts){ return _scr(dir,opts,TUBS); }
  const NAV = { port:{ x:-(xwz(8.76)+0.16), y:6.45, z:8.72 }, star:{ x:xwz(8.76)+0.16, y:6.45, z:8.72 }, stern:{ x:0, y:GANT.y-0.11, z:GANT.beamZ+GANT.beamHz+0.10 }, mast:{ x:0, y:5.35, z:ROOFZ+2.93 } };
  function navMounts(dir, opts){ const p=_scr(dir,opts,[NAV.port,NAV.star,NAV.stern,NAV.mast]); return { port:p[0], star:p[1], stern:p[2], mast:p[3] }; }
  function doorMount(dir, opts){
    opts=norm(opts); const t=clamp01(opts.doorOpen!=null?+opts.doorOpen:0), A=t*DOOR.maxDeg*DEG, w=DOOR.x1-DOOR.x0;
    const p=_scr(dir,opts,[{ x:(DOOR.x0+DOOR.x1)/2, y:HYa, z:DECK }, { x:DOOR.x0+w*Math.cos(A), y:HYa-w*Math.sin(A), z:DECK }]);
    return { x:p[0].x, y:p[0].y, lead:p[1], open:t, clear:t>=DOOR.clearAt };
  }
  // hull half-width at (y, z) and sheer height at y, as the old loft answered them (by station: no rake)
  function halfAtZ(y, z){ const u=Math.max(0,Math.min(1,(y+L/2)/L)), st=station(u); return lerp(st.wb,st.ws,Math.pow(fracAt(st,z),flareExp(u))); }
  function sheerZ(y){ const st=station(Math.max(0,Math.min(1,(y+L/2)/L))); return st.kz+st.dep; }

  // ---- the hull layout's shared helpers (cruiserIsoRig.js, verbatim) --------------------------------
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
  // an area's z at (x, y): flat, or its ramp (held beyond a0 / a1)
  function nlAreaZ(a, x, y){ if(a.z!=null) return a.z; const r=a.ramp, v=r.axis==='x'?x:y, t=Math.max(0,Math.min(1,(v-r.a0)/(r.a1-r.a0))); return r.z0+(r.z1-r.z0)*t; }

  // ==================================================================================================
  // THE ROOMS: three, in one dir-free mesh (interiorFaces). Every face is turned to the side it is seen
  // from (the engine culls back faces and draws a room front-facing only), tagged with its level, it:true.
  //   house   the mess and the galley, behind the deck door; the stair up to the wheelhouse to port, the
  //           stair down to the crew's cabin to starboard
  //   bridge  the wheelhouse: the console and the wheel, the helm chair, the chart table, the radio rack
  //   below   the crew's cabin under the house: four berths athwartships, lockers, a table
  // ==================================================================================================
  const RM_RAMPS = {
    join:   ['#2e2016','#4a3423','#6a4c30','#8a6840','#a98656','#c4a474'],     // varnished joinery
    lino:   ['#22302c','#30423c','#42574f','#577067','#6f887e'],               // green deck lino
    linen:  ['#7f7c72','#a09c90','#bfbbad','#d9d5c6','#eeebe0'],               // sheets, pillows, charts
    blanket:['#3f1a1c','#5e2626','#7f3430','#9c4a3e','#b96a58'],               // grey-red wool blankets, cushions
    stone:  ['#33373a','#4d5356','#6b7275','#8d9496','#b0b6b7'],               // galley tops, the stove's plate
    cavity: ['#07090a','#0d1012','#14181b','#1b2024'],                         // wells, kneeholes
  };
  const RM_MATS = ['rm_join','rm_lino','rm_linen','rm_blanket','rm_stone','rm_cavity'];
  const _rmMats = { rm_join:{ ramp:RM_RAMPS.join, off:0, dith:0.25 },     rm_lino:{ ramp:RM_RAMPS.lino, off:0, dith:0.30 },
                    rm_linen:{ ramp:RM_RAMPS.linen, off:0, dith:0 },      rm_blanket:{ ramp:RM_RAMPS.blanket, off:0, dith:0.25 },
                    rm_stone:{ ramp:RM_RAMPS.stone, off:0, dith:0 },      rm_cavity:{ ramp:RM_RAMPS.cavity, off:0, dith:0 } };
  const RM_WT = 0.08;                                     // lining thickness: the rooms stand this far inside the plating
  const HS = { x:HXl-RM_WT, y0:HYa+RM_WT, y1:HYf-RM_WT, z0:HS_F, zt:HZ1l-0.06 };
  // the wheelhouse's lining leans with its sides: x is its half breadth at the sole, xAt(z) at height z
  const BR = { x:xwz(BR_F)-RM_WT, xAt:(z)=>xwz(z)-RM_WT, y0:WYa+RM_WT, z0:BR_F, zt:WZ1-0.06, yF:(z)=>yFront(z)-RM_WT, sill:BR_F+0.85 };
  const BW = { x:BL.hx, y0:BL.y0, y1:BL.y1, z0:BL.sole, zt:BL.ceil };
  // a five-faced box (no underside), every face turned out
  const rbox=(o,x0,x1,y0,y1,z0,z1,mat,bT,bS)=>{ const s=bS==null?0:bS;
    pZ(o,1,z1,x0,x1,y0,y1,mat,bT==null?0.4:bT); pY(o,-1,y0,x0,x1,z0,z1,mat,s-0.3); pY(o,1,y1,x0,x1,z0,z1,mat,s+0.1);
    pX(o,-1,x0,y0,y1,z0,z1,mat,s-0.1); pX(o,1,x1,y0,y1,z0,z1,mat,s-0.5); };
  const rtube=(o,A,B,rad,mat,b)=>{ for(const f of tube(A,B,rad,mat,b)) o.push(f); };
  // a wall in two bands: a joinery dado, painted lining over it
  const wallX=(o,side,x,ya,yb,z0,zt,zd)=>{ pX(o,side,x,ya,yb,z0,zd,'rm_join',-0.2); pX(o,side,x,ya,yb,zd,zt,'cream',0); };
  const wallY=(o,dir,y,xa,xb,z0,zt,zd)=>{ if(zd>z0) pY(o,dir,y,xa,xb,z0,Math.min(zd,zt),'rm_join',-0.3); if(zt>zd) pY(o,dir,y,xa,xb,Math.max(zd,z0),zt,'cream',-0.1); };
  // a flight of treads from (ya, za) to (yb, zb) between x0 and x1; only the treads between zLo and zHi
  function rmStair(o, x0, x1, ya, za, yb, zb, n, zLo, zHi, railX){
    const run=(yb-ya)/n, rise=(zb-za)/n, up=zb>za;
    for(let k=1;k<n;k++){ const z=za+rise*k; if(z<zLo-1e-6 || z>zHi+1e-6) continue;
      const y0=ya+run*(k-0.5), y1=y0+run;
      pZ(o,1,z,x0,x1,Math.min(y0,y1),Math.max(y0,y1),'rm_join',k%2?0.5:0.2);
      pY(o,up?-1:1,up?y0:y1,x0,x1,z-Math.abs(rise),z,'rm_join',-0.6); }
    const a=Math.max(0,Math.min(1,(zLo-za)/(zb-za))), b=Math.max(0,Math.min(1,(zHi-za)/(zb-za))), t0=Math.min(a,b), t1=Math.max(a,b);
    if(railX!=null && t1>t0+0.05){ const P=(t,h)=>[railX, ya+(yb-ya)*t, za+(zb-za)*t+h];
      rtube(o,P(t0,0.90),P(t1,0.90),0.025,'steel',0.2); rtube(o,P(t0,0),P(t0,0.90),0.02,'steel',-0.1); rtube(o,P(t1,0),P(t1,0.90),0.02,'steel',-0.1); }
  }
  // the wheel, standing in the console: a rim on three spokes, raked top-aft; drawn both sides
  function rmWheel(o){
    const Wg=WHEEL_GEO, r=Wg.rake*DEG, e2=[0,Math.sin(r),Math.cos(r)], ax=[0,-Math.cos(r),Math.sin(r)], Hc=[WHEEL_HUB.x,WHEEL_HUB.y,WHEEL_HUB.z];
    const P=(u,v,d)=>[Hc[0]+u+ax[0]*d, Hc[1]+e2[1]*v+ax[1]*d, Hc[2]+e2[2]*v+ax[2]*d], nA=ax, nF=[-ax[0],-ax[1],-ax[2]];
    for(let k=0;k<Wg.seg;k++){ const a0=2*Math.PI*k/Wg.seg, a1=2*Math.PI*(k+1)/Wg.seg, c0=Math.cos(a0), s0=Math.sin(a0), c1=Math.cos(a1), s1=Math.sin(a1);
      for(const [d,n] of [[0.015,nA],[-0.015,nF]])
        faceO(o,[P(Wg.rimIn*c0,Wg.rimIn*s0,d),P(Wg.rad*c0,Wg.rad*s0,d),P(Wg.rad*c1,Wg.rad*s1,d),P(Wg.rimIn*c1,Wg.rimIn*s1,d)],'rm_join',k%2?0.9:0.5,0.03,n); }
    for(const deg of Wg.spokes){ const a=deg*DEG, c=Math.cos(a), s=Math.sin(a), w=0.014;
      for(const [d,n] of [[0.012,nA],[-0.012,nF]])
        faceO(o,[P(Wg.hubR*c+w*s,Wg.hubR*s-w*c,d),P(Wg.rimIn*c+w*s,Wg.rimIn*s-w*c,d),P(Wg.rimIn*c-w*s,Wg.rimIn*s+w*c,d),P(Wg.hubR*c-w*s,Wg.hubR*s+w*c,d)],'steel',0.4,0.03,n); }
    for(const [d,n] of [[0.02,nA],[-0.02,nF]]) faceO(o,ngon(8,Wg.hubR,(u,v)=>P(u,v,d)),'steel',0.8,0.03,n);
    rtube(o,P(0,0,0),P(0,0,-0.36),0.03,'steel',-0.2);     // the column, forward into the console
  }
  let _room=null, _wheel=null;
  function buildRooms(){
    const all=[], put=(arr,lv,ro)=>{ stampTags(arr,0,arr.length,lv,ro,null,true); for(const f of arr){ if(f.db==null) f.db=0; all.push(f); } };

    // ---------------- house: the mess and the galley ---------------------------------------------------
    (function(){ const R=HS, D=DOOR, fl=[], wl=[], ft=[], zd=R.z0+1.0, U=ST_UP, Dn=ST_DN, z0=R.z0;
      // the sole, cut round the stair well to starboard
      pZ(fl,1,z0,-R.x,Dn.x0,R.y0,R.y1,'rm_lino',0); pZ(fl,1,z0,Dn.x0,Dn.x1,R.y0,Dn.y0,'rm_lino',0);
      pZ(fl,1,z0,Dn.x0,Dn.x1,Dn.y1,R.y1,'rm_lino',0); pZ(fl,1,z0,Dn.x1,R.x,R.y0,R.y1,'rm_lino',0);
      for(const x of [-1.2,0.2,1.2]) pZ(fl,1,z0+0.004,x-0.01,x+0.01,R.y0,R.y1,'rm_lino',-1.2);                 // the lino's seams
      wallX(wl,1,-R.x,R.y0,R.y1,z0,R.zt,zd); wallX(wl,-1,R.x,R.y0,R.y1,z0,R.zt,zd); wallY(wl,-1,R.y1,-R.x,R.x,z0,R.zt,zd);
      wallY(wl,1,R.y0,-R.x,D.x0,z0,R.zt,zd); wallY(wl,1,R.y0,D.x1,R.x,z0,R.zt,zd); wallY(wl,1,R.y0,D.x0,D.x1,D.z1,R.zt,zd);
      for(const side of [-1,1]) for(const y of [3.0,4.6,6.2,7.8]){                                            // the ports, from inside
        faceO(wl,ngon(8,0.31,(a,b)=>[side*(R.x-0.006),y+a,4.95+b]),'steel',-0.2,0.01,[-side,0,0]);
        faceO(wl,ngon(8,0.23,(a,b)=>[side*(R.x-0.012),y+a,4.95+b]),'glas',1.2,0.02,[-side,0,0]); }
      // the well and the stair down (its upper treads; the lower ones are the cabin's)
      const zw=2.45;
      pZ(ft,1,zw,Dn.x0,Dn.x1,Dn.y0,Dn.y1,'rm_cavity',-0.5);
      pY(ft,1,Dn.y0,Dn.x0,Dn.x1,zw,z0,'rm_cavity',0.4); pY(ft,-1,Dn.y1,Dn.x0,Dn.x1,zw,z0,'rm_cavity',0.2);
      pX(ft,1,Dn.x0,Dn.y0,Dn.y1,zw,z0,'rm_cavity',0.6); pX(ft,-1,Dn.x1,Dn.y0,Dn.y1,zw,z0,'rm_cavity',0.1);
      rmStair(ft,Dn.x0+0.04,Dn.x1-0.04,Dn.y0,Dn.z0,Dn.y1,Dn.z1,Dn.n,zw+0.2,Dn.z0,null);
      rbox(ft,Dn.x0-0.05,Dn.x0,Dn.y0,Dn.y1+0.30,z0,z0+0.95,'rm_join',0.5,0.1);                                 // the well's half-height screen
      rbox(ft,Dn.x0-0.05,Dn.x1,Dn.y1+0.25,Dn.y1+0.30,z0,z0+0.95,'rm_join',0.5,0.1);
      // the stair up to the wheelhouse, its lockers outboard
      rmStair(ft,U.x0,U.x1,U.y0,U.z0,U.y1,BR_F,U.n,U.z0,BR_F,U.x1-0.02);
      faceO(ft,[[U.x1,U.y0+0.18,z0],[U.x1,U.y1,z0],[U.x1,U.y1,BR_F-0.24]],'rm_join',-0.4,0,[1,0,0]);            // the stringer, panelled in under the flight
      pY(ft,1,U.y1,U.x0,U.x1,z0,R.zt,'rm_join',-0.2);
      rbox(ft,-R.x,U.x0,U.y0,7.0,z0,z0+1.90,'cream',0.3,-0.1);                                                 // lockers down the port side, outboard of the flight
      for(const y of [4.75,5.5,6.25]) pX(ft,1,U.x0+0.004,y-0.008,y+0.008,z0+0.10,z0+1.85,'cream',-1.6);
      // the oilskin locker by the door, the gear hanging in it
      rbox(ft,1.95,R.x,R.y0,3.0,z0,z0+1.95,'cream',0.3,-0.1);
      pX(ft,-1,1.946,R.y0+0.10,2.9,z0+0.12,z0+1.85,'rm_cavity',0.6);
      for(const [y,m] of [[1.9,'safe'],[2.25,'buff'],[2.6,'safe']]) pX(ft,-1,1.94,y-0.14,y+0.14,z0+0.55,z0+1.70,m,-0.3);
      rbox(ft,1.60,1.92,2.3,2.95,z0,z0+0.34,'safe',0.3,-0.2);                                                  // the boot rack: a tote of sea boots
      // the mess: an L of settee, the table in its corner
      const zs=z0+0.45;
      rbox(ft,2.00,R.x,Dn.y1+0.30,R.y1,z0,zs,'rm_join',0,-0.1); pZ(ft,1,zs+0.006,2.04,R.x-0.03,Dn.y1+0.36,R.y1-0.5,'rm_blanket',0.3);
      rbox(ft,0.55,2.00,9.10,R.y1,z0,zs,'rm_join',0,-0.1);     pZ(ft,1,zs+0.006,0.60,2.00,9.14,R.y1-0.03,'rm_blanket',0.3);
      pX(ft,-1,R.x-0.03,Dn.y1+0.36,R.y1-0.03,zs,zs+0.42,'rm_blanket',-0.3); pY(ft,-1,R.y1-0.03,0.60,R.x-0.03,zs,zs+0.42,'rm_blanket',-0.5);
      rbox(ft,0.80,1.85,7.13,8.75,z0+0.70,z0+0.76,'rm_join',0.9,0.2);
      for(const [xa,xb,ya,yb] of [[0.80,1.85,7.13,7.16],[0.80,1.85,8.72,8.75],[0.80,0.83,7.13,8.75],[1.82,1.85,7.13,8.75]]) pZ(ft,1,z0+0.785,xa,xb,ya,yb,'rm_join',-0.6);   // the fiddles
      rbox(ft,1.26,1.40,7.87,8.01,z0,z0+0.70,'steel',0,-0.2);
      for(const [x,y,m] of [[1.05,7.5,'cream'],[1.55,8.3,'safe'],[1.5,7.6,'cream']]) rbox(ft,x-0.05,x+0.05,y-0.05,y+0.05,z0+0.76,z0+0.86,m,0.6,0);   // mugs
      pZ(ft,1,z0+0.765,1.0,1.4,8.0,8.5,'rm_linen',0.3);                                                       // the tide tables, left open
      // the galley: the range on the forward wall, an L of counter to port, lockers over it
      rbox(ft,-0.75,0.55,9.05,R.y1,z0,z0+0.90,'iron',0.6,0.4);
      pZ(ft,1,z0+0.905,-0.68,0.48,9.10,R.y1-0.04,'rm_stone',-0.2);
      for(const x of [-0.42,0.20]) faceO(ft,ngon(8,0.13,(a,b)=>[x+a,9.32+b,z0+0.91]),'iron',-0.3,0.01,[0,0,1]);
      rbox(ft,-0.52,-0.32,9.22,9.42,z0+0.91,z0+1.09,'steel',0.9,0.2);                                          // the kettle
      rbox(ft,0.28,0.44,9.36,9.52,z0+0.90,R.zt,'iron',0,0.3);                                                  // the flue
      pY(ft,-1,9.046,-0.60,0.40,z0+0.22,z0+0.62,'rm_cavity',0.8);                                              // the oven door
      rbox(ft,-R.x,-0.75,9.0,R.y1,z0,z0+0.92,'rm_join',0,0); pZ(ft,1,z0+0.925,-R.x,-0.75,9.0,R.y1,'rm_stone',0.5);
      rbox(ft,-R.x,-2.02,7.0,9.0,z0,z0+0.92,'rm_join',0,0);  pZ(ft,1,z0+0.925,-R.x,-2.02,7.0,9.0,'rm_stone',0.5);
      pZ(ft,1,z0+0.93,-1.75,-1.15,9.12,9.50,'steel',-0.8); pZ(ft,1,z0+0.932,-1.70,-1.20,9.16,9.46,'iron',0.6);   // the sink
      rtube(ft,[-1.45,9.54,z0+0.93],[-1.45,9.44,z0+1.16],0.02,'steel',0.4);
      for(const x of [-2.0,-1.4]) pY(ft,-1,8.996,x-0.008,x+0.008,z0+0.08,z0+0.88,'rm_join',-1.6);
      for(const y of [7.65,8.35]) pX(ft,1,-2.016,y-0.008,y+0.008,z0+0.08,z0+0.88,'rm_join',-1.6);
      rbox(ft,-R.x+0.04,-2.16,7.2,7.60,z0+0.93,z0+1.12,'rm_linen',0.4,0);                                      // the bread crock
      pY(ft,-1,R.y1-0.34,-R.x,-0.85,z0+1.75,R.zt,'rm_join',-0.2); pZ(ft,-1,z0+1.75,-R.x,-0.85,R.y1-0.34,R.y1,'rm_join',-1.2);   // the lockers over the counter
      for(const x of [-2.0,-1.45]) pY(ft,-1,R.y1-0.344,x-0.008,x+0.008,z0+1.80,R.zt-0.05,'rm_join',-1.6);
      // on the aft wall: the clock, the notice board, the extinguisher by the door
      faceO(ft,ngon(8,0.15,(a,b)=>[-1.35+a,R.y0+0.012,5.55+b]),'steel',0.6,0.02,[0,1,0]); faceO(ft,ngon(8,0.11,(a,b)=>[-1.35+a,R.y0+0.016,5.55+b]),'rm_linen',0.6,0.03,[0,1,0]);
      pY(ft,1,R.y0+0.012,-2.30,-1.70,4.75,5.65,'rm_join',0.5,0.02); pY(ft,1,R.y0+0.016,-2.23,-1.93,5.0,5.5,'rm_linen',0.3,0.03); pY(ft,1,R.y0+0.016,-1.89,-1.75,4.85,5.25,'rm_linen',0,0.03);
      rbox(ft,0.62,0.78,R.y0,R.y0+0.14,z0+0.95,z0+1.45,'safe',0.6,0.2);
      put(fl,'house','floor'); put(wl,'house','lining'); put(ft,'house','fit');
    })();

    // ---------------- bridge: the wheelhouse -------------------------------------------------------------
    (function(){ const R=BR, D=WDOOR, fl=[], wl=[], ft=[], wh=[], z0=R.z0, zt=R.zt, zs=R.sill, U=ST_UP, yf0=R.yF(z0);
      pZ(fl,1,z0,-R.x,U.x0,R.y0,yf0,'rm_lino',0.1); pZ(fl,1,z0,U.x0,U.x1,R.y0,U.y0,'rm_lino',0.1);
      pZ(fl,1,z0,U.x0,U.x1,U.y1,yf0,'rm_lino',0.1); pZ(fl,1,z0,U.x1,R.x,R.y0,yf0,'rm_lino',0.1);
      // the walls: joinery to the sill, painted over it; the sides lean out with the plating, the front leans out with the glass
      const X=R.xAt;
      for(const side of [-1,1]) for(const [za,zb,m,b] of [[z0,zs,'rm_join',-0.2],[zs,zt,'cream',0]])
        faceO(wl,[[side*X(za),R.y0,za],[side*X(za),R.yF(za),za],[side*X(zb),R.yF(zb),zb],[side*X(zb),R.y0,zb]],m,b,0,[-side,0,0.17]);
      for(const [za,zb,m,b] of [[z0,zs,'rm_join',-0.3],[zs,zt,'cream',-0.1]])
        faceO(wl,[[-X(za),R.yF(za),za],[X(za),R.yF(za),za],[X(zb),R.yF(zb),zb],[-X(zb),R.yF(zb),zb]],m,b,0,[0,-1,0.26]);
      for(const [za,zb,m,b] of [[z0,zs,'rm_join',-0.3],[zs,zt,'cream',-0.1]]){                              // the aft wall, either side of the door and over it
        faceO(wl,[[-X(za),R.y0,za],[D.x0,R.y0,za],[D.x0,R.y0,zb],[-X(zb),R.y0,zb]],m,b,0,[0,1,0]);
        faceO(wl,[[D.x1,R.y0,za],[X(za),R.y0,za],[X(zb),R.y0,zb],[D.x1,R.y0,zb]],m,b,0,[0,1,0]); }
      pY(wl,1,R.y0,D.x0,D.x1,D.z1,zt,'cream',-0.1);
      // the glass, from inside
      for(const side of [-1,1]) for(const [ya,yb] of [[4.05,5.05],[5.45,6.30],[6.62,7.42],[7.74,8.48]])
        faceO(wl,[[side*(X(7.45)-0.012),ya,7.45],[side*(X(7.45)-0.012),yb,7.45],[side*(X(8.35)-0.012),yb,8.35],[side*(X(8.35)-0.012),ya,8.35]],'glas',1.4,0.02,[-side,0,0.17]);
      for(const [xa,xb] of [[-2.06,-1.34],[-1.20,-0.48],[-0.36,0.36],[0.48,1.20],[1.34,2.06]])
        faceO(wl,[[xa,R.yF(7.45)-0.012,7.45],[xb,R.yF(7.45)-0.012,7.45],[xb,R.yF(8.45)-0.012,8.45],[xa,R.yF(8.45)-0.012,8.45]],'glas',1.6,0.02,[0,-1,0.26]);
      for(const [xa,xb] of [[-2.06,-0.90],[-0.60,0.62],[1.84,2.10]]) pY(wl,1,R.y0+0.012,xa,xb,7.45,8.35,'glas',1.0,0.02);
      // the stair well from the mess: its top treads, a rail round it
      const zw=z0-1.25;
      pZ(ft,1,zw,U.x0,U.x1,U.y0,U.y1,'rm_cavity',-0.5);
      pY(ft,1,U.y0,U.x0,U.x1,zw,z0,'rm_cavity',0.4); pX(ft,1,U.x0,U.y0,U.y1,zw,z0,'rm_cavity',0.6); pX(ft,-1,U.x1,U.y0,U.y1,zw,z0,'rm_cavity',0.1);
      rmStair(ft,U.x0+0.03,U.x1-0.03,U.y0,U.z0,U.y1,z0,U.n,zw+0.15,z0,null);
      for(const [A,B] of [[[U.x1,U.y0,z0],[U.x1,U.y1-0.05,z0]],[[U.x0,U.y0,z0],[U.x1,U.y0,z0]]]){
        rtube(ft,[A[0],A[1],z0+0.95],[B[0],B[1],z0+0.95],0.025,'steel',0.3); rtube(ft,[A[0],A[1],z0+0.50],[B[0],B[1],z0+0.50],0.018,'steel',-0.1);
        rtube(ft,A,[A[0],A[1],z0+0.95],0.02,'steel',-0.1); rtube(ft,B,[B[0],B[1],z0+0.95],0.02,'steel',-0.1); }
      // the console across the front: three bays, the helmsman's the middle one with his knees under it. It stands
      // plumb from the front's foot; its top runs on to the leaning glass
      const zc=z0+0.95, yc=8.20, yb=R.yF(z0)-0.02, hx=WHEEL_HUB.x;
      rbox(ft,-R.x+0.04,hx-0.34,yc,yb,z0,zc,'rm_join',0.2,0); rbox(ft,hx+0.34,R.x-0.04,yc,yb,z0,zc,'rm_join',0.2,0);
      rbox(ft,hx-0.34,hx+0.34,yc+0.14,yb,z0,zc,'rm_join',0.2,0); pY(ft,-1,yc+0.138,hx-0.30,hx+0.30,z0+0.04,z0+0.62,'rm_cavity',0.6);
      pZ(ft,1,zc+0.004,-X(zc)+0.04,X(zc)-0.04,yc+0.05,R.yF(zc)-0.03,'iron',0.9);                                 // the instrument top
      rbox(ft,-1.50,-0.86,8.36,8.84,zc,zc+0.30,'iron',0.6,0.5); pY(ft,-1,8.356,-1.44,-0.92,zc+0.04,zc+0.27,'glas',1.8,0.01);   // the radar
      faceO(ft,ngon(8,0.17,(a,b)=>[-1.18+a*0.9,8.352,zc+0.155+b*0.6]),'glas',2.6,0.02,[0,-1,0]);
      rbox(ft,-0.72,-0.24,8.40,8.80,zc,zc+0.24,'iron',0.6,0.5); pY(ft,-1,8.396,-0.68,-0.28,zc+0.04,zc+0.21,'glas',1.2,0.01);   // the sounder
      pY(ft,-1,8.392,-0.64,-0.32,zc+0.07,zc+0.11,'safe',0.8,0.02);
      for(const f of cyl([hx,8.62,zc],[hx,8.62,zc+0.16],0.12,8,'steel',0.2,0,1,0.09,1.2)) ft.push(f);           // the compass
      rbox(ft,0.92,1.22,8.38,8.62,zc,zc+0.07,'steel',0.4,0); for(const x of [1.00,1.14]){ rtube(ft,[x,8.50,zc+0.07],[x,8.40,zc+0.27],0.015,'steel',0.3); rbox(ft,x-0.03,x+0.03,8.36,8.42,zc+0.25,zc+0.31,'safe',0.6,0); }   // the engine levers
      rbox(ft,1.45,1.95,8.42,8.78,zc,zc+0.18,'iron',0.6,0.5); pY(ft,-1,8.416,1.50,1.90,zc+0.03,zc+0.15,'steel',-0.4,0.01);   // the radio
      rtube(ft,[1.80,8.42,zc+0.10],[1.92,8.28,zc+0.05],0.02,'iron',0.6);
      rmWheel(wh);
      // the helm chair on its pedestal
      const hs=HELM;
      rbox(ft,hs.x-0.06,hs.x+0.06,hs.y-0.02,hs.y+0.10,z0,hs.z-0.08,'steel',0,-0.2);
      rbox(ft,hs.x-0.24,hs.x+0.24,hs.y-0.22,hs.y+0.22,hs.z-0.09,hs.z,'rm_blanket',0.5,0);
      rbox(ft,hs.x-0.24,hs.x+0.24,hs.y-0.30,hs.y-0.22,hs.z-0.09,hs.z+0.55,'rm_blanket',0.5,0.2);
      for(const s of [-1,1]) rbox(ft,hs.x+s*0.27-0.03,hs.x+s*0.27+0.03,hs.y-0.26,hs.y+0.14,hs.z+0.16,hs.z+0.21,'iron',0.6,0);
      // the chart table to starboard: the chart, the dividers' lamp, drawers under
      const zc2=z0+0.95;
      rbox(ft,1.40,R.x,4.90,6.60,z0,zc2,'rm_join',0.5,0); pZ(ft,1,zc2,R.x,X(zc2),4.90,6.60,'rm_join',0.5);    // (its top runs out to the leaning wall)
      pZ(ft,1,zc2+0.004,1.47,R.x-0.06,5.02,6.46,'rm_linen',0.4); pZ(ft,1,zc2+0.008,1.55,1.88,5.25,6.05,'glas',2.2); pZ(ft,1,zc2+0.01,1.66,1.92,5.80,6.30,'rm_linen',-0.6);
      for(const z of [0.30,0.60]) pX(ft,-1,1.396,4.96,6.54,z0+z-0.008,z0+z+0.008,'rm_join',-1.6);
      rtube(ft,[R.x-0.05,5.0,zc2],[R.x-0.20,5.25,zc2+0.34],0.018,'steel',0.3); rbox(ft,R.x-0.30,R.x-0.14,5.20,5.36,zc2+0.32,zc2+0.38,'safe',0.8,0);
      // the radio rack on the aft wall, the extinguisher, the kettle shelf
      rbox(ft,-1.10,0.0,R.y0,R.y0+0.37,z0,z0+1.10,'iron',0.6,0.4);
      for(const z of [0.25,0.55,0.85]) pY(ft,1,R.y0+0.374,-1.04,-0.06,z0+z-0.10,z0+z+0.10,'steel',z>0.5?-0.6:-0.2,0.01);
      pY(ft,1,R.y0+0.378,-0.95,-0.75,z0+0.80,z0+0.90,'safe',0.8,0.02);
      rbox(ft,0.25,0.39,R.y0,R.y0+0.13,z0+0.20,z0+0.70,'safe',0.6,0.2);
      rbox(ft,-R.x,U.x1,R.y0,U.y0-0.02,z0,z0+0.95,'rm_join',0.4,0); pZ(ft,1,z0+0.95,-X(z0+0.95),-R.x,R.y0,U.y0-0.02,'rm_join',0.4);   // the oilskin chest in the port after corner
      put(fl,'bridge','floor'); put(wl,'bridge','lining'); put(ft,'bridge','fit'); put(wh,'bridge','fit'); _wheel=wh;
    })();

    // ---------------- below: the crew's cabin ---------------------------------------------------------------
    (function(){ const R=BW, fl=[], wl=[], ft=[], z0=R.z0, zt=R.zt, zd=z0+1.05, Dn=ST_DN;
      pZ(fl,1,z0,-R.x,R.x,R.y0,R.y1,'rm_lino',-0.2);
      for(const x of [-1.45,0,1.45]) pZ(fl,1,z0+0.004,x-0.01,x+0.01,R.y0,R.y1,'rm_lino',-1.4);
      wallX(wl,1,-R.x,R.y0,R.y1,z0,zt,zd); wallX(wl,-1,R.x,R.y0,R.y1,z0,zt,zd); wallY(wl,1,R.y0,-R.x,R.x,z0,zt,zd); wallY(wl,-1,R.y1,-R.x,R.x,z0,zt,zd);
      // four berths athwartships, heads to port: a joinery box, a mattress, a pillow, a blanket turned down, a leeboard
      for(const B of BERTHS){ const x0=B.x0, x1=B.x1, y0=B.y0, y1=B.y1, zb=B.top-0.10;
        rbox(ft,x0,x1,y0,y1,z0,zb,'rm_join',0,-0.1);
        rbox(ft,x0+0.03,x1-0.03,y0+0.03,y1-0.03,zb,B.top,'rm_linen',0.3,-0.4);
        pZ(ft,1,B.top+0.05,x0+0.10,x0+0.52,y0+0.12,y1-0.12,'rm_linen',1.0); pX(ft,1,x0+0.52,y0+0.12,y1-0.12,B.top,B.top+0.05,'rm_linen',-0.2); pX(ft,-1,x0+0.10,y0+0.12,y1-0.12,B.top,B.top+0.05,'rm_linen',-0.2);
        pY(ft,-1,y0+0.12,x0+0.10,x0+0.52,B.top,B.top+0.05,'rm_linen',-0.4); pY(ft,1,y1-0.12,x0+0.10,x0+0.52,B.top,B.top+0.05,'rm_linen',-0.1);
        pZ(ft,1,B.top+0.012,x0+0.78,x1-0.05,y0+0.04,y1-0.04,'rm_blanket',0.3); pZ(ft,1,B.top+0.016,x0+0.66,x0+0.80,y0+0.04,y1-0.04,'rm_linen',0.6);
        const ya=B.inner<0 ? y1-0.03 : y0, yb=ya+0.03;                                                          // the leeboard on the open side
        rbox(ft,x0+0.55,x1-0.25,ya,yb,zb,B.top+0.14,'rm_join',0.6,0.2);
        pY(ft,B.inner<0?-1:1,B.inner<0?y0+0.004:y1-0.004,x0+0.9,x0+1.5,B.top+0.55,B.top+0.95,'rm_join',0.4,0.02);   // a shelf of one's own on the bulkhead
        pY(ft,B.inner<0?-1:1,B.inner<0?y0+0.008:y1-0.008,x0+1.0,x0+1.2,B.top+0.62,B.top+0.88,'rm_linen',0.2,0.03);
      }
      // lockers down the port side, a bench before them
      rbox(ft,-R.x,-2.35,3.13,7.87,z0,z0+1.85,'cream',0.3,-0.1);
      for(const y of [3.9,4.7,5.5,6.3,7.1]) pX(ft,1,-2.346,y-0.008,y+0.008,z0+0.10,z0+1.80,'cream',-1.6);
      for(const y of [3.55,4.3,5.1,5.9,6.7,7.5]) pX(ft,1,-2.344,y-0.03,y+0.03,z0+0.95,z0+1.02,'steel',0.4,0.01);
      // the table, a deck of cards and two mugs on it
      rbox(ft,-1.30,-0.30,4.60,6.00,z0+0.70,z0+0.76,'rm_join',0.9,0.2); rbox(ft,-0.87,-0.73,5.23,5.37,z0,z0+0.70,'steel',0,-0.2);
      pZ(ft,1,z0+0.765,-1.05,-0.85,5.0,5.14,'rm_linen',0.6); for(const [x,y,m] of [[-0.6,4.9,'cream'],[-1.0,5.6,'safe']]) rbox(ft,x-0.05,x+0.05,y-0.05,y+0.05,z0+0.76,z0+0.86,m,0.6,0);
      // the stair from the mess: every tread, the handrail inboard, a locker under its head
      rmStair(ft,Dn.x0+0.04,Dn.x1-0.04,Dn.y0,Dn.z0,Dn.y1,z0,Dn.n,z0,Dn.z0,Dn.x0+0.02);
      faceO(ft,[[Dn.x0,Dn.y0,DECK-0.04],[Dn.x0,Dn.y1-0.20,z0],[Dn.x0,Dn.y0,z0]],'rm_join',-0.4,0,[-1,0,0]);
      pY(ft,-1,Dn.y0,Dn.x0,R.x,z0,zt,'rm_join',-0.3);
      // the heater on the starboard side forward of the stair, its flue to the deckhead; boots by it
      rbox(ft,2.40,R.x,6.55,7.05,z0,z0+0.80,'iron',0.6,0.4); rbox(ft,2.60,2.74,6.73,6.87,z0+0.80,zt,'iron',0,0.3); pX(ft,-1,2.396,6.62,6.98,z0+0.20,z0+0.55,'safe',0.4,0.01);
      rbox(ft,2.45,2.80,7.25,7.70,z0,z0+0.30,'safe',0.3,-0.2);
      put(fl,'below','floor'); put(wl,'below','lining'); put(ft,'below','fit');
    })();
    return all;
  }
  // the berths: lower and only; x0 is the head (to port), `inner` the side the leeboard stands on
  const BERTHS = [
    { id:'berth_aft_port', x0:-2.88, x1:-0.65, y0:BL.y0, y1:BL.y0+0.80, top:BL.sole+0.55, inner:-1 },
    { id:'berth_aft_stbd', x0:0.65,  x1:2.88,  y0:BL.y0, y1:BL.y0+0.80, top:BL.sole+0.55, inner:-1 },
    { id:'berth_fwd_port', x0:-2.88, x1:-0.65, y0:BL.y1-0.80, y1:BL.y1, top:BL.sole+0.55, inner:1 },
    { id:'berth_fwd_stbd', x0:0.65,  x1:2.88,  y0:BL.y1-0.80, y1:BL.y1, top:BL.sole+0.55, inner:1 },
  ];
  function interiorFaces(){ if(!_room) _room=buildRooms(); return _room; }

  // ---- the new layout's reads ----------------------------------------------------------------------------
  function faceList(state){ const o=state && typeof state==='object' ? state : {}; return assemble(stateOf(o)).F; }
  const PART_TAGS = { trawl_doors:2, drum:3, net:4, hatch:5 };
  function partFaces(id, state){
    if(id==='door' || id==='doors') return doorFaces(state);
    if(id==='wheel'){ interiorFaces(); return _wheel; }
    const tag=PART_TAGS[id]; return tag ? faceList(state).filter(f=>f.tag===tag) : [];
  }
  // the leaves: both doors' (the deckhouse's and the wheelhouse's), shut at t 0, swung aft through maxDeg at t 1.
  // t is a number, or a state with doorOpen (or door).
  function doorFaces(t){
    if(t && typeof t==='object') t=t.doorOpen!=null ? t.doorOpen : t.door;
    const out=[]; for(const D of LEAVES) leafFaces(D, clamp01(+t||0), out);
    return out;
  }
  function matsFor(id){
    const pal=palette(id && typeof id==='object' ? id : { scheme:id });
    return nlMatsTable(pal.id, Object.assign({}, pal.mats, _rmMats), RM_MATS);
  }
  // how far a room's vertex stands outside the ship: the hull under the deck plate (inside the plating), the
  // deckhouse and the boat deck's slab over it, the wheelhouse under its raked front. 0 inside any of them.
  function rmExcess(p){
    const x=Math.abs(p[0]), y=p[1], z=p[2], st=station(uAt(y,z));
    const hull=Math.max(x-halfAt(y,z,1), z-DECK, st.kz-z, -L/2-y);
    const house=Math.max(x-HXl, HYa-y, y-HYf, DECK-z, z-WZ0);
    const wheel=Math.max(x-xwz(z), WYa-y, y-yFront(z), WZ0-z, z-WZ1);
    return Math.max(0, Math.min(hull, house, wheel));
  }
  function fitCheck(tol){
    tol=tol==null?0.01:tol; let worst=0, at=null, outside=0, nv=0; const faces=interiorFaces();
    for(const f of faces) for(const p of f.v){ nv++; const e=rmExcess(p); if(e>tol) outside++; if(e>worst){ worst=e; at=[q3(p[0]),q3(p[1]),q3(p[2])]; } }
    return { tol, faces:faces.length, vertices:nv, outside, worst:q3(worst), worstAt:at };
  }

  const RIG_FILE='sternTrawlerMk2IsoRig.js', EXPORT_SYMBOL='SternTrawlerMk2Iso', HULL_ID='stern_trawler_mk2', HULL_LABEL='Stern Trawler Mk II · 38 m';
  // a door as the walk publishes it: clear when the walker, at the doorway's far limit, passes the leaf's free edge
  function doorSpec(D, note){
    const R=NL_WALK.radius, Hn=leafHinge(D), w=D.x1-D.x0, sw=w+0.04;
    const clearAt=q3(Math.acos(Math.max(-1,Math.min(1,(w-2*R)/sw)))/DEG/D.maxDeg);
    const keepClear=[[q3(Hn.x),q3(Hn.y)]]; for(let k=0;k<=8;k++){ const a=k/8*D.maxDeg*DEG; keepClear.push([q3(Hn.x+Hn.sg*sw*Math.cos(a)), q3(Hn.y-sw*Math.sin(a))]); }
    return { id:D.id, kind:'hinge', face:'aft', y:D.y, x0:D.x0, x1:D.x1, z0:q3(D.z0), z1:D.z1, sillZ:D.sillZ,
      hinge:{ x:Hn.x, y:q3(Hn.y), z0:q3(D.z0), z1:D.z1, side:D.hinge }, maxDeg:D.maxDeg, swings:'aft', opensTo:'aft',
      clearAt, keepClear, leaf:'doorFaces(t)', lv:D.lv, ro:'wall', clear:{ width:q3(w), height:q3(D.z1-D.sillZ) }, note };
  }
  const DOOR_SPECS = [
    doorSpec(DOOR, 'the deckhouse door, in its aft wall on the working deck: weather-tight steel, hinged on its port jamb, swinging aft over the deck between the winches. '+
      'Shut until opened; E opens it. A '+q3(DOOR.z0-DOOR.sillZ)+' m coaming under it. keepClear is the swept arc: the leaf’s collider. doorFaces(t) swings both of the ship’s leaves.'),
    doorSpec(WDOOR, 'the wheelhouse door, in its aft wall on the boat deck to starboard: hinged on its starboard jamb, swinging aft. '+
      'The boat deck is reached from the wheelhouse by this door, and from the working deck by the ladder on the house’s aft wall (see _excluded boat_deck_ladder).'),
  ];
  DOOR.clearAt = DOOR_SPECS[0].clearAt;

  function geometry(){
    const W_=WHEEL_GEO, r=W_.rake*DEG;
    return {
      schema:'hidden-harbours/hull-geometry@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:HULL_ID, units:'m', frame:NL_FRAME,
      ids:Object.assign({}, LV), roles:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice(), dwlZ:DWL,
      levels:[
        { id:'main_deck', deck:'deck', sole:DECK, ceil:null, kind:'open', soleZ:DECK, ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the working deck, from the transom to the break of the fo’c’sle at y '+q3(BRK)+', and the side decks past the house: sky. The gantry, its door arms and the warps (level rigging) stand over it and never lift.' } },
        { id:'house', deck:'house', sole:HS.z0, ceil:q3(HS.zt), kind:'hard', soleZ:HS.z0, ceilingZ:q3(HS.zt),
          ceiling:{ kind:'hard', lid:'bridge, top', z:q3(HS.zt), of:'the boat deck (level bridge, role floor): '+q3(HS.zt-HS.z0)+' m clear over the mess. It lifts, with the wheelhouse on it and the wheelhouse top, under cut key house.' } },
        { id:'bridge', deck:'bridge', sole:BR.z0, ceil:q3(BR.zt), kind:'hard', soleZ:BR.z0, ceilingZ:q3(BR.zt),
          ceiling:{ kind:'hard', lid:'top', z:q3(BR.zt), of:'the wheelhouse top (level top): '+q3(BR.zt-BR.z0)+' m clear. It lifts, with the mast, under cut key bridge.' } },
        { id:'below', deck:'below', sole:BW.z0, ceil:q3(BW.zt), kind:'hard', soleZ:BW.z0, ceilingZ:q3(BW.zt),
          ceiling:{ kind:'hard', lid:'house, bridge, top', z:q3(BW.zt), of:'the deck plate under the deckhouse: '+q3(BW.zt-BW.z0)+' m clear over the crew’s cabin. The house, the wheelhouse and its top lift under cut key below.' } },
        { id:'foredeck', deck:'focsle_head', sole:q3(FD_Z0), ceil:null, kind:'open', soleZ:q3(FD_Z0), ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the whaleback: it follows the sheer from '+q3(FD_Z0)+' at the break to '+q3(FD[FD.length-1].z)+' at the stem head. soleZ is its height at the break.' } },
        { id:'top', deck:'wheelhouse_top', sole:ROOFZ, ceil:null, kind:'open', soleZ:ROOFZ, ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the wheelhouse top: the mast, the scanner, the searchlight. Not a standing surface (see _excluded wheelhouse_top)' } },
      ],
      cutaway:{
        ghost:{ deck:[], house:['bridge','top'], bridge:['top'], below:['house','bridge','top'] },
        gate:{ deck:[], house:['bridge','top'], bridge:['top'], below:['house','bridge','top'] },
        shows:{ deck:null, house:'house', bridge:'bridge', below:'below' },
        note:'gate lists are complete lift sets in ghost’s vocabulary (whole levels). Under house the wheelhouse, the boat deck it stands on, the funnel and the wheelhouse top with the mast lift, '+
          'and the mess is drawn in the deckhouse. Under bridge the wheelhouse top and the mast lift. Under below the deckhouse lifts too and the crew’s cabin is drawn under it. '+
          'The hull, the working deck and its gear, the fo’c’sle head and the gear aloft (level rigging: the gantry and its door arms, the warps, the trawl doors) never lift.',
      },
      door:DOOR_SPECS[0], doors:DOOR_SPECS.slice(),
      parts:[
        { id:'wheel', group:'room', lv:'bridge', ro:'fit', hub:nlV([WHEEL_HUB.x,WHEEL_HUB.y,WHEEL_HUB.z]),
          axis:nlV([0,-Math.cos(r),Math.sin(r)]), rake_deg:W_.rake, radius:W_.rad,
          note:'the wheel stands in the wheelhouse, which is a room: partFaces(\'wheel\') returns its faces from interiorFaces(). It is drawn amidships; there is no steer state (as on the 84 and the 120).' },
        { id:'trawl_doors', group:'tag', tag:2, state:'doors', lv:'rigging', ro:'part', blocks:GAL_BLOCK.map(b=>nlV([b.x,b.y,b.z])), travel:q3(TD.zStow-TD.zDown),
          note:'doors 0..1: 0 the two trawl doors hang stowed at the blocks on the gantry’s arms, outboard of the rail; 1 they are lowered '+q3(TD.zStow-TD.zDown)+' m down the ship’s side to the water on their warps' },
        { id:'drum', group:'tag', tag:3, state:'drum', lv:'main_deck', ro:'fit', axis:[1,0,0], at:nlV([0,DRUMG.y,DRUMG.z]), note:'drum (degrees) turns the net drum about its athwartships axis' },
        { id:'net', group:'tag', tag:4, state:'net', lv:'main_deck', ro:'fit',
          note:'net 0..1: 0 the net is wound on the drum and the codend heaped at the ramp head; 1 the drum is bare and the net runs over the ramp head, down the ramp and out over the lip' },
        { id:'hatch', group:'tag', tag:5, state:'hatch', lv:'main_deck', ro:'fit', hinge:nlV([0,HATCH.y+HATCH.hl+0.04,DECK+HATCH.h]), maxDeg:HATCH.maxDeg,
          hinges:HATCHES.map(Hh=>nlV([0,Hh.y+Hh.hl+0.04,DECK+Hh.h])),
          note:'hatch 0..1 lifts the two fish hatches’ lids together, each about its coaming’s forward edge (hinge is the after hatch’s, hinges both); the dark hold and the fish boxes in it are static, under the lids' },
        { id:'door', group:'doorFaces', lv:'house', ro:'wall', kind:'hinge', maxDeg:DOOR.maxDeg, opensTo:'aft', note:'doorFaces(t): both leaves, the deckhouse door (level house) and the wheelhouse door (level bridge): see door, doors' },
      ],
      states:{ doors:'0..1 (the trawl doors: stowed .. lowered)', drum:'deg (the net drum)', net:'0..1 (wound on .. shot down the ramp)', hatch:'0..1 (the two fish hatches’ lids)',
        doorOpen:'0..1 (render option; the leaves are doorFaces(t))' },
    };
  }

  // ---- the walk (hull-walk@1) ---------------------------------------------------------------------------------
  function walk(){
    const R=NL_WALK.radius, G=geometry(), areas=[];
    const half=(y)=>dwY(y)-0.27-R;                         // the deck's edge for the walker's centre: clear of the bulwark stanchions' knees
    const sidePoly=(inner, y0, y1, n, outer)=>{ const P=[[q3(inner(y0)),q3(y0)]];
      for(let i=0;i<=n;i++){ const y=y0+(y1-y0)*i/n; P.push([q3(outer(y)),q3(y)]); }
      P.push([q3(inner(y1)),q3(y1)]); return P; };
    const mirror=(poly)=>poly.map(p=>[-p[0],p[1]]).reverse();
    // -- the working deck: either side of the ramp and the net drum, then the whole deck to the house --
    const yQ=-17.45, yD=-10.12, xRamp=q3(RW+R+0.05), xDrum=q3(DRUMG.stand+0.25+R), yDrumA=q3(-12.10-R);
    const yGa=GANT.y+0.10-GANT.cheekY-R-0.04, yGb=GANT.y+0.10+GANT.cheekY+R+0.04, xGant=GANT.x0-GANT.lw-R-0.12;   // past the gantry's leg and its cheek
    { const outer=(y)=>Math.min(half(y), (y>yGa && y<yGb) ? xGant : 9), ys=[];
      for(let i=0;i<=14;i++) ys.push(yQ+(yD+0.06-yQ)*i/14);
      for(const yy of [yGa, yGb]){ ys.push(yy-0.001, yy+0.001); }
      ys.sort((a,b)=>a-b);
      const P=[[xRamp,q3(yQ)]]; for(const y of ys) P.push([q3(outer(y)),q3(y)]);
      P.push([xDrum,q3(yD+0.06)],[xDrum,yDrumA],[xRamp,yDrumA]);
      const star=nlSimplify(P);
      areas.push({ id:'ramp_strip_star', cut:'deck', z:DECK, poly:star, holes:[], note:'the starboard side of the working deck, from the quarter past the gantry’s leg and the net drum; the ramp’s slot and the codend are inboard of it' });
      areas.push({ id:'ramp_strip_port', cut:'deck', z:DECK, poly:mirror(star), holes:[], note:'the port side, the same' });
    }
    { const P=nlSimplify(nlSide(half, yD, HYa-R-0.03, 24));
      const post=(s)=>{ const a=nlGrow(WARP_POST.x-0.10,WARP_POST.y-0.10,WARP_POST.x+0.10,WARP_POST.y+0.10,R); return s>0 ? a : mirror(a); };
      const winch=(s)=>{ const a=nlGrow(0.75,-1.0,WINCH.x+WINCH.hx+0.08,1.0,R); return s>0 ? a : mirror(a); };
      areas.push({ id:'trawl_deck', cut:'deck', z:DECK, poly:P,
        holes:[ post(1), post(-1), nlGrow(-HATCH.hw-0.06,HATCH.y-HATCH.hl-0.06,2.75,HATCH.y+HATCH.hl+0.06,R),
                nlGrow(-HATCH_F.hw-0.06,HATCH_F.y-HATCH_F.hl-0.06,HATCH_F.hw+0.06,HATCH_F.y+HATCH_F.hl+0.06,R), winch(1), winch(-1) ],
        note:'the working deck from the net drum to the house: the warps’ deck bollards either side, the after fish hatch with the totes beside it, the forward fish hatch, '+
          'the split winch either side of the door (the wash hose coiled by the port drum). The holes are those, grown by the walker’s radius.' });
    }
    // -- the side decks past the house, and the stairs up them to the fo'c'sle head --
    { const inner=()=>HXl+0.12+R, P=nlSimplify(sidePoly(inner, 1.0, ST_FD.y0+0.05, 10, half));
      areas.push({ id:'house_alley_star', cut:'deck', z:DECK, poly:P, holes:[], note:'the starboard side deck, between the house (its grab rail) and the bulwark: '+q3(dwY(ST_FD.y0)-HXl)+' m wide at the stair’s foot' });
      areas.push({ id:'house_alley_port', cut:'deck', z:DECK, poly:mirror(P), holes:[], note:'the port side deck, the same' });
      const A=ST_FD, sp=nlRect(A.xa+R, A.y0-0.05, A.xb-R, BRK+R+0.03), ramp={ axis:'y', a0:A.y0, a1:q3(BRK), z0:DECK, z1:q3(FD_Z0) };
      const note='seven treads up the side deck to the fo’c’sle head at the break: the ramp carries the walker';
      areas.push({ id:'fore_stair_star', cut:'deck', kind:'stair', ramp, zMid:q3((DECK+FD_Z0)/2), cutHi:'deck', cutLo:'deck', poly:sp, holes:[], note });
      areas.push({ id:'fore_stair_port', cut:'deck', kind:'stair', ramp:Object.assign({}, ramp), zMid:q3((DECK+FD_Z0)/2), cutHi:'deck', cutLo:'deck', poly:mirror(sp), holes:[], note });
    }
    // -- the fo'c'sle head: the whaleback follows the sheer, so it is ramps fore and aft --
    let yFdEnd=BRK+R; for(let y=BRK+R; y<L/2+RAKE; y+=0.01){ if(fdAt(y).half-0.34<0.30) break; yFdEnd=y; }
    const fd=nlSurfaceAreas('focsle_deck', 'deck', (x,y)=>fdAt(y).z, (y)=>fdAt(y).half-0.34, BRK+R, yFdEnd, 0.02);
    { const zf=(y)=>fdAt(y).z, hs=[ nlGrow(-0.95,13.55,0.95,14.45,R), nlGrow(-0.70,14.45,0.70,16.85,R), nlGrow(-0.50,11.0,0.50,12.0,R),
        nlGrow(0.97,12.30,1.23,12.90,R), mirror(nlGrow(0.97,12.30,1.23,12.90,R)) ];
      for(const a of fd.areas){ let ya=Infinity, yb=-Infinity; for(const p of a.poly){ ya=Math.min(ya,p[1]); yb=Math.max(yb,p[1]); }
        a.holes=hs.filter(h=>Math.min.apply(null,h.map(p=>p[1]))<yb && Math.max.apply(null,h.map(p=>p[1]))>ya).map(h=>h.map(p=>p.slice()));
        a.note='the fo’c’sle head inside its rails; holes: the windlass and its cables, the fore hatch, the bow bollards'; areas.push(a); }
      void zf; }
    // -- the deckhouse: the door, the mess, the two stairs --
    areas.push({ id:'house_doorway', cut:'deck', z:DECK, poly:nlRect(DOOR.x0+R, HYa-R-0.12, DOOR.x1-R, HS.y0+R+0.02), holes:[], door:'house_door',
      note:'through the deckhouse door: a wall while the leaf is shut, passable from doorOpen '+DOOR_SPECS[0].clearAt+' (interact house_door). Over a '+q3(DOOR.z0-DOOR.sillZ)+' m coaming.' });
    const hx=q3(HS.x-R), hy0=q3(HS.y0+R), U=ST_UP, Dn=ST_DN;
    areas.push({ id:'house_sole', cut:'house', z:HS.z0, headroom:q3(HS.zt-HS.z0),
      poly:[[-hx,hy0],[q3(1.95-R),hy0],[q3(1.95-R),q3(3.0+R)],[hx,q3(3.0+R)],[hx,q3(Dn.y0-R)],[q3(Dn.x0-R),q3(Dn.y0-R)],[q3(Dn.x0-R),6.90],[0.32,6.90],[0.32,8.77],
            [q3(-2.02+R),8.77],[q3(-2.02+R),q3(7.0-R)],[q3(U.x1+R),q3(7.0-R)],[q3(U.x1+R),q3(U.y0-R)],[-hx,q3(U.y0-R)]], holes:[],
      note:'the mess: from the door forward between the two stairs to the range, the galley’s counter to port, the mess table and its settees to starboard. The stairs’ feet are aft of them, each side.' });
    areas.push({ id:'stair_bridge', cut:'house', kind:'stair', ramp:{ axis:'y', a0:U.y0, a1:U.y1, z0:HS.z0, z1:BR.z0 }, zMid:q3((HS.z0+BR.z0)/2), cutHi:'bridge', cutLo:'house',
      poly:nlRect(U.x0+R, U.y0-0.30, U.x1-R, U.y1+0.05), holes:[], note:'thirteen treads up to the wheelhouse, to port, climbing forward: the ramp carries the walker; the cut changes at zMid' });
    areas.push({ id:'stair_below', cut:'house', kind:'stair', ramp:{ axis:'y', a0:Dn.y0, a1:Dn.y1, z0:HS.z0, z1:BW.z0 }, zMid:q3((HS.z0+BW.z0)/2), cutHi:'house', cutLo:'below',
      poly:nlRect(Dn.x0+R, Dn.y0-0.30, Dn.x1-R, Dn.y1+0.30), holes:[], note:'ten treads down to the crew’s cabin, to starboard, descending forward: steep, a ship’s stair; the cut changes at zMid' });
    // -- the wheelhouse and the boat deck behind it --
    const bx=q3(BR.x-R), by0=q3(BR.y0+R), ch=nlGrow(HELM.x-0.25,HELM.y-0.30,HELM.x+0.25,HELM.y+0.20,R);
    areas.push({ id:'wheelhouse', cut:'bridge', z:BR.z0, headroom:q3(BR.zt-BR.z0),
      poly:[[0.23,by0],[bx,by0],[bx,q3(4.90-R)],[q3(1.40-R),q3(4.90-R)],[q3(1.40-R),q3(6.60+R)],[bx,q3(6.60+R)],[bx,7.97],[ch[1][0],7.97],[ch[1][0],ch[0][1]],[ch[0][0],ch[0][1]],[ch[0][0],7.97],
            [-bx,7.97],[-bx,q3(U.y1)],[q3(U.x1+R),q3(U.y1)],[q3(U.x1+R),q3(BR.y0+0.37+R)],[0.23,q3(BR.y0+0.37+R)]], holes:[],
      note:'the wheelhouse: aft of the console, round the helm chair (the notch), between the chart table to starboard and the stair well’s rail to port; the stair’s head is at its port forward end' });
    areas.push({ id:'wheelhouse_doorway', cut:'deck', z:WZ0, poly:nlRect(WDOOR.x0+R, WYa-R-0.12, WDOOR.x1-R, BR.y0+R+0.02), holes:[], door:'wheelhouse_door',
      note:'through the wheelhouse door onto the boat deck: a wall while the leaf is shut, passable from doorOpen '+DOOR_SPECS[1].clearAt+' (interact wheelhouse_door)' });
    { const x=q3(HXl+0.05-R), ya=q3(HYa-0.03+R), yb=q3(WYa-R-0.03), fx=q3(FUNNEL.r+0.10+R), fy=q3(FUNNEL.y0+0.02+FUNNEL.r+0.10+R);
      areas.push({ id:'boat_deck', cut:'deck', z:WZ0, poly:[[-x,ya],[-fx,ya],[-fx,fy],[fx,fy],[fx,ya],[x,ya],[x,yb],[-x,yb]], holes:[],
        note:'the boat deck aft of the wheelhouse, inside its rails, round the funnel (the notch). Reached by the wheelhouse door; the ladder from the working deck is _excluded boat_deck_ladder.' }); }
    // -- the crew's cabin --
    { const yb0=q3(BW.y0+R), yb1=q3(BW.y1-R), ax=0.42, ya=q3(BW.y0+0.80+R), yf=q3(BW.y1-0.80-R);
      areas.push({ id:'below_sole', cut:'below', z:BW.z0, headroom:q3(BW.zt-BW.z0),
        poly:[[-ax,yb0],[ax,yb0],[ax,ya],[q3(BW.x-R),ya],[q3(BW.x-R),q3(Dn.y0-R)],[q3(Dn.x0-R),q3(Dn.y0-R)],[q3(Dn.x0-R),Dn.y1],[q3(BW.x-R),Dn.y1],[q3(BW.x-R),q3(6.55-R)],[q3(2.40-R),q3(6.55-R)],[q3(2.40-R),yf],
              [ax,yf],[ax,yb1],[-ax,yb1],[-ax,yf],[q3(-2.35+R),yf],[q3(-2.35+R),ya],[-ax,ya]],
        holes:[ nlGrow(-1.30,4.60,-0.30,6.00,R) ],
        note:'the crew’s cabin: an aisle between the berths’ feet at each end, the lockers to port, the table (the hole), the stair’s foot and the heater to starboard' }); }
    const aAt=(id)=>areas.find(a=>a.id===id);
    const spawnAt=[0,-1.6];
    const fdPiece=(x,y)=>fd.areas.find(a=>nlPip([x,y],a.poly) && !a.holes.some(h=>nlPip([x,y],h)));
    const wl=[0,13.05], wlA=fdPiece(wl[0],wl[1]);

    const berths=BERTHS.map(B=>({ id:B.id, interact:B.id, kind:'midberth', topZ:q3(B.top), head:'port', deckheadZ:q3(BW.zt),
      poly:nlRect(B.x0+0.03, B.y0+0.03, B.x1-0.03, B.y1-0.03) }));
    const interact=[
      { id:'helm', action:'helm', label:'Helm', prompt:'Take the wheel', at:nlPt(HELM), reach:{ x:-0.45, y:q3(HELM.y), z:BR.z0 }, area:'wheelhouse',
        note:'seated in the helm chair: anchors.helm is the seat, anchors.wheel the hub, anchors.pilot the feet' },
      { id:'house_door', action:'open_close', label:'Deckhouse door', prompt:'Open · shut', state:'doorOpen', clearAt:DOOR_SPECS[0].clearAt,
        at:nlPt({ x:(DOOR.x0+DOOR.x1)/2, y:HYa, z:(DOOR.z0+DOOR.z1)/2 }), reach:{ x:q3((DOOR.x0+DOOR.x1)/2), y:q3(HYa-R-0.20), z:DECK }, area:'trawl_deck' },
      { id:'wheelhouse_door', action:'open_close', label:'Wheelhouse door', prompt:'Open · shut', state:'doorOpen', clearAt:DOOR_SPECS[1].clearAt,
        at:nlPt({ x:(WDOOR.x0+WDOOR.x1)/2, y:WYa, z:(WDOOR.z0+WDOOR.z1)/2 }), reach:{ x:q3((WDOOR.x0+WDOOR.x1)/2), y:q3(BR.y0+R+0.20), z:BR.z0 }, area:'wheelhouse' },
    ].concat(BERTHS.map(B=>({ id:B.id, action:'sleep', label:'Berth', prompt:'Turn in', at:{ x:q3((B.x0+B.x1)/2), y:q3((B.y0+B.y1)/2), z:q3(B.top) },
        reach:{ x:q3(B.x0<0?-0.30:0.30), y:q3((B.y0+B.y1)/2+(B.inner<0?0.12:-0.12)), z:BW.z0 }, area:'below_sole' })), [
      { id:'winch', action:'haul', label:'Trawl winch', prompt:'Haul · veer', state:'doors', at:nlPt({ x:0, y:WINCH.y, z:DECK+0.74 }), reach:{ x:0, y:q3(WINCH.y-0.50), z:DECK }, area:'trawl_deck',
        note:'the brake stands are between the two drums: the winchman stands on the centreline abaft the door' },
      { id:'net_drum', action:'shoot_haul', label:'Net drum', prompt:'Shoot · haul the net', state:'net', also:['drum'], at:nlPt({ x:0, y:DRUMG.y, z:DRUMG.z }), reach:{ x:0, y:q3(yD+0.14), z:DECK }, area:'trawl_deck',
        note:'from the trawl way forward of the drum, between the warps’ deck bollards' },
      { id:'gallows_port', action:'trawl_door', label:'Port trawl door', prompt:'Lower · stow the door', state:'doors', at:nlPt(GAL_BLOCK[0]), reach:{ x:-3.00, y:q3(yGb+0.28), z:DECK }, area:'ramp_strip_port',
        note:'the door hangs from the arm off the gantry’s port leg; it is worked from the deck just forward of the leg' },
      { id:'gallows_stbd', action:'trawl_door', label:'Starboard trawl door', prompt:'Lower · stow the door', state:'doors', at:nlPt(GAL_BLOCK[1]), reach:{ x:3.00, y:q3(yGb+0.28), z:DECK }, area:'ramp_strip_star' },
      { id:'hold_hatch', action:'open_close', label:'Fish hold', prompt:'Open · shut the hatches', state:'hatch', at:nlPt({ x:0, y:HATCH.y, z:DECK+HATCH.h }), reach:{ x:0, y:q3(HATCH.y+HATCH.hl+0.06+R+0.14), z:DECK }, area:'trawl_deck',
        note:'the after hatch; the hold itself is not walked (see _excluded fish_hold). One state lifts both lids.' },
      { id:'hold_hatch_fwd', action:'open_close', label:'Fish hold, forward hatch', prompt:'Open · shut the hatches', state:'hatch', at:nlPt({ x:0, y:HATCH_F.y, z:DECK+HATCH_F.h }), reach:{ x:0, y:q3(HATCH_F.y+HATCH_F.hl+0.06+R+0.14), z:DECK }, area:'trawl_deck',
        note:'the forward hatch: the same state' },
      { id:'galley', action:'cook', label:'Galley range', prompt:'Put the kettle on', at:{ x:-0.10, y:9.33, z:q3(HS.z0+0.90) }, reach:{ x:-0.10, y:8.60, z:HS.z0 }, area:'house_sole' },
      { id:'mess_table', action:'use', label:'Mess table', prompt:'Sit in', at:{ x:1.32, y:7.94, z:q3(HS.z0+0.76) }, reach:{ x:0.20, y:7.90, z:HS.z0 }, area:'house_sole', waitsOn:['sit'] },
      { id:'chart_table', action:'use', label:'Chart table', prompt:'Read the chart', at:{ x:1.70, y:5.75, z:q3(BR.z0+0.95) }, reach:{ x:1.00, y:5.75, z:BR.z0 }, area:'wheelhouse' },
      { id:'windlass', action:'anchor', label:'Windlass', prompt:'Let go · weigh', at:nlPt({ x:0, y:14.0, z:fdAt(14.0).z+0.48 }), reach:{ x:wl[0], y:wl[1], z:wlA ? q3(nlAreaZ(wlA,wl[0],wl[1])) : q3(fdAt(wl[1]).z) }, area:wlA ? wlA.id : null },
    ]);
    const cleats=[
      { id:'bow_1', type:'samson_post', pos:nlV([SAMSON.x,SAMSON.y,SAMSON.z+0.06]) },
      { id:'bow_port', type:'bollard', pos:nlV([-1.1,12.6,fdAt(12.6).z+0.40]) }, { id:'bow_star', type:'bollard', pos:nlV([1.1,12.6,fdAt(12.6).z+0.40]) },
      { id:'quarter_port', type:'bollard', pos:nlV([-2.6,-18.0,DECK+0.42]) }, { id:'quarter_star', type:'bollard', pos:nlV([2.6,-18.0,DECK+0.42]) },
    ];
    const _excluded=[
      { id:'wheelhouse_top', of:'area', z:ROOFZ, reason:'the wheelhouse top: the mast, the scanner, the searchlight and the aerials; no way up, not a standing surface.' },
      { id:'stern_ramp', of:'area', z:RZ0, slope:{ y0:q3(-L/2), y1:RYT, z0:RZ0, z1:q3(DECK-0.02) }, halfW:RW, waitsOn:['ladderOn'],
        reason:'the stern ramp’s floor: wet plating at '+q3(Math.atan((DECK-0.02-RZ0)/(RYT+L/2))/DEG)+'°, the net’s road, not the crew’s (the owner ruled it excluded, 2026-07-22). Its lip is '+q3(RZ0-DWL)+
          ' m over the waterline: the only way aboard from the water, and it waits on a clip.' },
      { id:'ramp_quarters', of:'area', reason:'the deck beside the ramp abaft y '+yQ+': the quarter bollards fill it. The trawl doors are worked from forward of the gantry’s legs (interact gallows_port / gallows_stbd).' },
      { id:'gantry_legs', of:'area', reason:'the gantry’s legs and the bulwark cheeks they grow from, each side at y '+GANT.y+': the walk passes inboard of them (ramp_strip_port / _star narrow to x '+q3(xGant)+' there).' },
      { id:'boat_deck_wings', of:'area', reason:'the boat deck beside the wheelhouse: '+q3(HXl+0.10-HXw0)+' m at the sole and less at the head, where the wheelhouse’s sides lean out over it; under the liferafts’ cradles.' },
      { id:'boat_deck_ladder', of:'climb', kind:'ladder', x:LADX, y:q3(HYa-0.12), z0:DECK, z1:WZ0, waitsOn:['ladderOn'],
        reason:'the fixed ladder up the house’s aft wall to the boat deck, starboard of the door: a vertical ladder, which waits on a ladder clip. The boat deck is reached through the wheelhouse meanwhile.' },
      { id:'fish_hold', of:'room', reason:'the fish hold under the working deck (the hatches at y '+HATCH.y+' and y '+HATCH_F.y+'): drawn as a dark well with boxes in it under each lid; no room, no way down but its ladder.' },
      { id:'engine_room', of:'room', reason:'the engine room, under the working deck abaft the crew’s cabin: not drawn.' },
      { id:'focsle_store', of:'room', reason:'the fo’c’sle store under the whaleback (the fore hatch at y 11.5): not drawn.' },
      { id:'seat_helm', of:'seat', at:nlPt(HELM), reason:'the helm chair: taken by the helm interact (drive).' },
      { id:'mess_settee', of:'seat', at:{ x:2.24, y:8.0, z:q3(HS.z0+0.45) }, waitsOn:['sit'], reason:'the mess’s settees, round the table: sitting at them waits on a sit clip.' },
      { id:'washboards', of:'washboards', reason:'none: she has steel bulwarks to the rail all round the working deck and side decks wide enough to walk (areas house_alley_port / _star); nobody walks her rail.' },
      { id:'working_gear', of:'cleat', reason:'the gantry and its door arms, the winch’s warping heads and the warps’ deck bollards are working gear, not tie-offs: mooring lines go to the samson post, the bow bollards and the quarter bollards (cleats[]).' },
    ];
    const spawn=aAt('trawl_deck');
    return {
      schema:'hidden-harbours/hull-walk@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:HULL_ID, label:HULL_LABEL,
      kind:'ship', units:'metres', frame:NL_FRAME, loa_m:L, beam_m:9.0, DWL, cockpitZ:DECK,
      cell:{ W, H, pivot:{ x:cx, y:cy }, pxPerM:PX, defaultElev:DEFAULT_ELEV, order:['N','NE','E','SE','S','SW','W','NW'] },
      tags:{ lv:Object.assign({}, LV), ro:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice() },
      walk:{ radius:R, stepTol:NL_WALK.stepTol, rule:NL_RULE, spawn:{ x:spawnAt[0], y:spawnAt[1], area:spawn?spawn.id:null, note:'on the working deck abaft the winch' }, areas },
      house:Object.assign({ door:DOOR_SPECS[0], doors:DOOR_SPECS.slice(),
        rooms:[ { level:'house', name:'the mess and the galley', soleZ:HS.z0, ceilingZ:q3(HS.zt) }, { level:'bridge', name:'the wheelhouse', soleZ:BR.z0, ceilingZ:q3(BR.zt) },
                { level:'below', name:'the crew’s cabin', soleZ:BW.z0, ceilingZ:q3(BW.zt) } ], fitCheck:'qa.fitCheck' }, nlHouseDecks(G.levels)),
      anchors:{ helm:nlPt(HELM), wheel:nlPt(WHEEL_HUB), pilot:nlPt(PILOT), berths,
        ramp:{ head:{ x:0, y:RYT, z:q3(DECK-0.02) }, foot:{ x:0, y:q3(-L/2), z:RZ0 }, halfW:RW },
        gallows:GAL_BLOCK.map((b,i)=>({ id:i?'gallows_stbd':'gallows_port', side:i?'starboard':'port', block:nlPt(b), y0:q3(GAL.y-GAL.hy), y1:q3(GAL.y+GAL.hy), headZ:GAL.zt })),
        winch:[-1,1].map(s=>({ side:s<0?'port':'starboard', x:s*WINCH.x, y:WINCH.y, z:q3(DECK+0.74) })),
        drum:{ x:0, y:DRUMG.y, z:DRUMG.z, axis:'x', halfLen:DRUMG.hx, flangeR:DRUMG.rFl },
        holdHatch:{ x:0, y:HATCH.y, z:q3(DECK+HATCH.h), halfW:HATCH.hw, halfL:HATCH.hl, hinge:nlV([0,HATCH.y+HATCH.hl+0.04,DECK+HATCH.h]) },
        holdHatches:HATCHES.map(Hh=>({ id:Hh.id, x:0, y:Hh.y, z:q3(DECK+Hh.h), halfW:Hh.hw, halfL:Hh.hl, hinge:nlV([0,Hh.y+Hh.hl+0.04,DECK+Hh.h]) })),
        gantry:GANTRY.map(nlPt), tubs:TUBS.map(nlPt), nav:{ port:nlPt(NAV.port), star:nlPt(NAV.star), stern:nlPt(NAV.stern), mast:nlPt(NAV.mast) },
        windlass:nlPt({ x:0, y:14.0, z:fdAt(14.0).z+0.48 }), boatDeckLadder:{ x:LADX, y:q3(HYa-0.12), z0:DECK, z1:WZ0 },
        // ---- the trawl's working points, named for the tow, the haul and the hands that sort (gameplay reads these by name)
        warps:[-1,1].map((s,i)=>({ id:s<0?'warp_port':'warp_stbd', side:s<0?'port':'starboard',
          winch:{ x:s*WINCH.x, y:WINCH.y, z:q3(DECK+0.74) }, lead:nlPt(GANTRY[i]), door:nlPt(GAL_BLOCK[i]) })),
        codEnd:{ rampFoot:{ x:0, y:q3(-L/2), z:RZ0 }, rampHead:{ x:0, y:RYT, z:q3(DECK-0.02) }, spill:{ x:0, y:SORT.spillY, z:DECK } },
        sorting:{ x0:-SORT.hw, x1:SORT.hw, y0:SORT.y0, y1:SORT.y1, z:DECK, totes:[nlPt(TUBS[2]), nlPt(TUBS[3])] },
        crew:[ { id:'winchman', at:'winch' }, { id:'drum_hand', at:'net_drum' }, { id:'door_hand_port', at:'gallows_port' }, { id:'door_hand_stbd', at:'gallows_stbd' },
               { id:'sorter_port', x:-SORT.sorterX, y:SORT.sorterY, z:DECK }, { id:'sorter_stbd', x:SORT.sorterX, y:SORT.sorterY, z:DECK },
               { id:'hatch_hand', at:'hold_hatch' } ] },
      interact, cleats, washboards:[], _excluded,
      rock:{ frames:ROCK.frames, rollA:ROCK.rollA, pitchA:ROCK.pitchA, heaveA:ROCK.heaveA, period:ROCK.period },
      qa:{ fitCheck:fitCheck(), surfaceFit:{ foredeck:fd.worst, tol:0.02 } },
    };
  }

  // ---- the old rig's house block, kept for its readers: the same shape, this rig's numbers ---------------------
  const HOUSE = {
    kind:'ship', soleZ:DECK, door:DOOR,
    decks:{
      house:{ soleZ:DECK, ceilZ:HZ1l, hx:HXl, hxAt:()=>HXl, y0:HYa, y1:HYf, portholes:{ ys:[3.0,4.6,6.2,7.8], z0:4.70, z1:5.20 }, aftPorts:{ xs:[-1.25,1.25], z0:4.80, z1:5.30 } },
      bridge:{ soleZ:WZ0, ceilZ:WZ1, hx:HXw1, hxAt:xwz, y0:WYa, front:{ yBot:FYb, yTop:FYt },
               frontGlass:{ z0:7.45, z1:8.45, panes:[[-2.06,-1.34],[-1.20,-0.48],[-0.36,0.36],[0.48,1.20],[1.34,2.06]] },
               sideGlass:{ z0:7.45, z1:8.35, runs:[[4.05,5.05],[5.45,6.30],[6.62,7.42],[7.74,8.48]] },
               aftGlass:{ z0:7.45, z1:8.35, panes:[[-2.06,-0.90],[-0.60,0.62],[1.84,2.10]] } },
      below:{ soleZ:BL.sole, ceilZ:BL.ceil, y0:BL.y0, y1:BL.y1 },
    },
    ladder:{ id:'boat_deck_ladder', face:'aft', x:LADX, y:HYa, z0:DECK, z1:WZ0 },
    ramp:{ halfW:RW, yTop:RYT },
  };
  function faces(){ return F; }                           // the static tagged mesh, as before; the leaves are doorFaces(t)

  const HULL_OBJ={ id:HULL_ID, label:HULL_LABEL, kind:'ship', faceList, partFaces, interiorFaces, doorFaces, geometry, matsFor, walk, fitCheck, render, renderCut,
    GAIN, BIAS, LN, BAYER, PX, W, H, pivot:{ x:cx, y:cy }, defaultElev:DEFAULT_ELEV, KEY, cell:{ W, H, pivot:{ x:cx, y:cy }, PX, defaultElev:DEFAULT_ELEV },
    schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, DWL, cockpitZ:DECK, loa:L,
    // the lamp join reads the hull's own scope: where her nav lamps are drawn, the house they light, and her stem station
    DIRS:8, navMounts, HOUSE, station };
  const HULLS={ stern_trawler_mk2:HULL_OBJ };
  function byId(id){ return HULLS[id]||null; }

  root.SternTrawlerMk2Iso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV, KEYLINE_DEFAULT,
    order:['N','NE','E','SE','S','SW','W','NW'],
    HULL, BOOT, CREAM, DECKF, WOOD, GLAS, BUFF, NET, STEEL, IRON, SAFE, KEY,
    render, renderCut, renderTrawlDoors, renderDrum, renderNet, renderHatch,
    allFaces:(o)=>{ const s=stateOf(o||{}); return assemble(s).F.concat(doorFaces(s.door)); },
    ROCK, rock:rockMotion, helmSeat, HELM, gantryMounts, GANTRY, gallowsMounts, GALLOWS, haulerMount, HAULER,
    drumMount, DRUM, tubMounts, TUBS, navMounts, doorMount, DOOR, WDOOR, HOUSE, geometry, faces, doorFaces, LEVEL_IDS:LV,
    WHEEL_GEO, WHEEL_HUB, PILOT,
    loft:{ station, skin, dfrac, bowRake, halfAtZ, sheerZ, L, TH, DECK, SOLE_U, NSEG,
           house:HOUSE, shade:{ GAIN, BIAS, LN, BAYER, KEY, EDGE:0.30 }, cell:{ W, H, cx, cy, S } },
    SCHEMES, schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, SLOTS,
    // the hull layout (ADR 0048 §8 A): one hull, through byId('stern_trawler_mk2'); the same names for the default hull
    byId, HULLS, hullIds:Object.keys(HULLS), defaultHull:HULL_ID, LV, RO, GHOST_ROLES, kind:'ship',
    faceList, partFaces, interiorFaces, matsFor, walk, fitCheck, DWL, cockpitZ:DECK, loa:L,
    GAIN, BIAS, LN, BAYER, cell:{ W, H, pivot:{ x:cx, y:cy }, PX, defaultElev:DEFAULT_ELEV } };
})(typeof window !== 'undefined' ? window : globalThis);

/* sideDraggerIsoRig.js — Hidden Harbours SIDE DRAGGER, 25 m, in the hull layout (ADR 0048 §8 A)
   (procedural, zero dependencies, ¾ top-down, 8 directions). Spec: docs/art/hull-layout-spec.md.

   Tier 4, the first offshore hull: a wooden-era SIDE TRAWLER that works The Banks. A long open working deck
   forward with two wood-coamed fish hatches on the centreline and the foremast between them, a raised whaleback
   fo'c'sle at the raked stem, the trawl winch athwartships in front of the house, and the whole house AFT: a
   cream deckhouse, a glassy wheelhouse on it looking over the deck, a buff funnel with a black cap behind the
   wheelhouse, a mizzen pole, a cruiser stern. She tows from ONE side: the two GALLOWS, fore and aft, stand at
   the STARBOARD rail (the working side) and a trawl door hangs outboard from each; the net is stowed along the
   starboard bulwark between them; the fish pounds are boarded off on the port side of the deck.

   SHE HAS NO RAMP. The cod end comes aboard over the working-side rail between the gallows: lifted on the
   gilson from the head of the foremast's derrick, swung inboard and emptied on the deck by the pounds
   (anchors.codEnd is { rail, lift, spill }, not the stern trawlers' { rampFoot, rampHead, spill }).

   The hull is the old rig's, number for number: the same offsets table, L 25.0, beam 7.0, deck 2.05, the same
   sheer, rake and bands of paint, the same cell (PX 32, 896 x 792, pivot 448,450), the same rock. What is new
   is everything the hull layout asks: tagged faces (lv / ro / it), three rooms (house: the mess and galley;
   bridge: the wheelhouse; below: the crew's cabin), two hinged doors as leaves (doorFaces(t)), the cutaway
   table, four paint schemes, the walk. The house is narrower and shorter than the old rig's (see the
   arrangement): a walker passes along BOTH side decks.

   Frame: +x starboard, +y bow, +z up, metres; origin amidships at the keel bottom on the centreline.

   API (global SideDraggerIso; the hull is byId('side_dragger') and the same names stand on the global):
     faceList(state), partFaces(id, state), interiorFaces(), doorFaces(t), geometry(), matsFor(scheme),
     walk(), fitCheck(tol), render(dir, opts), renderCut(dir, key, opts), schemeIds, defaultScheme,
     GAIN, BIAS, LN, BAYER, PX, W, H, pivot, defaultElev, DWL, cockpitZ, loa.
   State keys (geometry().states): doors 0..1 (the trawl doors, stowed at the gallows -> lowered to the water),
     winch (deg), net 0..1 (stowed along the working-side bulwark -> shot over the rail), hatch 0..1 (the two
     fish hatches' lids), doorOpen 0..1 (render option; the leaves are doorFaces(t)).
   Kept from the old rig: render, W, H, PX, DIRS, pivot, defaultElev, order, the ramps (HULL .. IRON, KEY), ROCK,
     rock, helmSeat / HELM, gallowsMounts / GALLOWS, haulerMount / HAULER, tubMounts / TUBS, navMounts,
     doorMount, DOOR, HOUSE, LEVEL_IDS, faces(), loft. */
(function (root) {
  const PX = 32, S = 32;
  const W = 896, H = 792, cx = 448, cy = 450;   // cell + pivot (projection of boat origin)
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;                          // ADR 0031: the fleet draws no keyline
  const ID=(p)=>p;
  const ROCK = { frames: 8, rollA: 2.0, pitchA: 1.1, heaveA: 1.0, period: 4.2 };  // 25 m of steel — slow, stiff offshore roll
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }

  // ---- fixed ramps: the old rig's, value for value (the default scheme paints with exactly these) ----
  const HULL  = ['#3a1a14','#582a20','#7a382c','#9c4a3c','#b5604e','#c97a62','#d99678'];  // rust-red oxide topsides
  const BOOT  = ['#0e1114','#16191e','#20242a','#2b3338','#3a4450'];                       // near-black boot / bottom
  const CREAM = ['#8f8672','#a89c80','#c2b79c','#d8cdb4','#eae0c8','#f4ecd8'];             // house + cove line
  const DECKF = ['#3a3630','#46443a','#5a5248','#645c52','#7e756a','#8a8175','#9c937f'];  // taupe steel deck / bulwark liner
  const WOOD  = ['#3a2c20','#5a4634','#6f5840','#8c6a45','#a5825a'];                       // hatch coamings, pound boards, trawl doors
  const GLAS  = ['#131c21','#213039','#33434e','#48657a','#6b91a1'];                       // window glass (sea-grey)
  const BUFF  = ['#6b4a22','#8c5e2c','#b06f32','#cf7a35','#e29a55','#f0b878'];             // ochre mast / derrick / funnel / gallows
  const NET   = ['#16241d','#22362b','#2c4438','#3f5e4f','#5c7d6b'];                       // trawl net
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2'];                       // winch drums, rails, aerials
  const IRON  = ['#0e1114','#171b21','#232a32','#333c46'];                                 // dark fittings
  const SAFE  = ['#7a2a14','#a63a18','#cf5320','#e8722e','#f39a52'];                       // lifering, liferaft, floats, totes (new with the layout)
  const KEY   = '#140f0c';
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
  // (slot cove is her cove line and the rail cap; slot gear is the mast, the derrick, the gallows and the funnel)
  const SCHEMES = {
    'rust-red':   { name:'Rust Red',   hull:'#9c4a3c', house:'#d8cdb4', cove:'#d8cdb4', gear:'#cf7a35', bottom:'#2b3338',
                    ramps:{ hull:HULL, house:CREAM, cove:CREAM, gear:BUFF, bottom:BOOT },
                    note:'the old rig’s paint, ramp for ramp: rust-red oxide topsides, a cream cove line and house, ochre mast, gallows and funnel, a black boot' },
    'moss-green': { name:'Moss Green', hull:'#35543f', house:'#e0dccb', cove:'#e0dccb', gear:'#c9a03a', bottom:'#6e2f25',
                    note:'dark green topsides, a cream cove line and house, yellow mast, gallows and funnel, red oxide below' },
    'coal-black': { name:'Coal Black', hull:'#2a2e31', house:'#d6cfba', cove:'#b9482e', gear:'#b9482e', bottom:'#6e2f25',
                    note:'black topsides, a red lead cove line, mast, gallows and funnel, a cream house, red oxide below' },
    'gulf-blue':  { name:'Gulf Blue',  hull:'#23486b', house:'#e4e4dc', cove:'#e4e4dc', gear:'#d8b548', bottom:'#2b3338',
                    note:'deep blue topsides, a white cove line and house, yellow mast, gallows and funnel, a black boot' },
  };
  const DEFAULT_SCHEME = 'rust-red';
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
  const L = 25.0, TH = 0.07, DECK = 2.05;
  const NSEG = 24;
  const RAKE = 0.90;   // raked trawler stem
  const DWL = 1.10;                                       // HullMeshDef.RestingDraftMeters (SideDraggerIsoHullMesh.asset)
  // ---- offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m) ----
  // full midbody, cruiser stern (keel rises aft), marked sheer sweeping to a high raked bow.
  // sheerZ = kz+depth: 3.20, 2.95, 2.77, 2.62, 2.60, 2.75, 3.12, 3.80, 4.70
  const T = [
    [2.30,1.30,2.90,0.30],   // 0 cruiser stern
    [3.00,2.30,2.85,0.10],   // 1
    [3.35,2.80,2.75,0.02],   // 2
    [3.50,3.00,2.62,0.00],   // 3
    [3.50,3.00,2.60,0.00],   // 4 amidships (max beam 7 m)
    [3.42,2.80,2.75,0.00],   // 5
    [3.10,2.20,3.10,0.02],   // 6
    [2.30,1.10,3.65,0.15],   // 7 bow shoulder (flare)
    [0.14,0.06,4.30,0.40],   // 8 stem
  ];
  const lerp = (a,b,t)=>a+(b-a)*t;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), wb:lerp(A[1],B[1],fr), dep:lerp(A[2],B[2],fr),
             kz:lerp(A[3],B[3],fr), y:-L/2+u*L };
  }
  function bowRake(u,frac){ const t=Math.max(0,(u-0.62)/0.38), s=t*t*(3-2*t); return RAKE*s*(0.30+0.70*frac); }
  function skin(side,u,frac,inset){
    const st=station(u);
    const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
    return [ side*lerp(wb,ws,frac), st.y+bowRake(u,frac), st.kz+lerp(0,dep,frac) ];
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
  const dw=(u)=>{ const st=station(u); return (lerp(st.wb,st.ws,dfrac(st))-TH)*0.97; };
  const dwY=(y)=>dw(uOf(y));
  // the rail at world y: the sheer's half-breadth and its height (by station: the rake is slight where this is asked)
  const railAt=(y)=>{ const u=uOf(y); return { x:station(u).ws, z:sheerU(u) }; };

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
  const SOLE_U = 0.80, U0 = 0.012, BRK = yOf(SOLE_U);     // the working deck runs from the stern to the break of the fo'c'sle (y 7.5)
  // the deckhouse (level house) and the wheelhouse on it (level bridge), all aft. The old rig's house was 2.35
  // half breadth from y -10.6 to -4.6 and filled the deck from bulwark to bulwark; its wheelhouse was 2.05. The
  // house is 1.65 now and its aft wall stands 1.0 m further forward (y -9.6), the wheelhouse 1.50: a side deck
  // either side wide enough to walk, and a clear deck right round the stern. The wheelhouse keeps its front,
  // raked AFT as it rises (the old rig's), flush with the house's front at its foot.
  const HXl = 1.65, HYa = -9.6, HYf = -4.6, HZ1l = 4.50;
  const HXw = 1.50, WYa = -8.2, FYb = -4.60, FYt = -4.95;
  const WZ0 = 4.56, WZ1 = 6.60, ROOFZ = 6.66;
  const xwz=(z)=> HXw;                                    // the wheelhouse's half breadth at height z: its sides are plumb
  const yFront=(z)=> FYb + (FYt-FYb)*(z-WZ0)/(WZ1-WZ0);
  const BL = { sole:0.30, ceil:DECK-0.10, hx:2.20, y0:-9.4, y1:-5.0 };   // the crew's cabin under the house
  // the doors: hinged weather-tight steel, both in aft walls, both swinging aft (the old rig's one door was in the
  // house's FRONT wall, facing the winch: the house door is on the after deck now)
  const DOOR  = { id:'house_door', kind:'hinge', face:'aft', y:HYa, x0:-0.50, x1:0.40, z0:DECK+0.05, z1:4.05,
                  hinge:'port', swingDeg:110, maxDeg:110, clearAt:0.60, sillZ:DECK, lv:'house' };
  const WDOOR = { id:'wheelhouse_door', kind:'hinge', face:'aft', y:WYa, x0:0.55, x1:1.20, z0:WZ0+0.05, z1:6.45,
                  hinge:'starboard', swingDeg:105, maxDeg:105, clearAt:0.60, sillZ:WZ0, lv:'bridge' };
  const LEAVES = [DOOR, WDOOR];
  // the stairs inside: house -> bridge to port, house -> below to starboard, both climbing or falling forward
  const ST_UP = { x0:-1.40, x1:-0.64, y0:-7.9, y1:-5.9, z0:DECK, z1:WZ0, n:13 };
  const ST_DN = { x0:0.78, x1:1.54, y0:-7.9, y1:-6.1, z0:DECK, z1:BL.sole, n:9 };
  // the stair outside: from the port side of the deck up to the whaleback at the break (the starboard side is the
  // working side: the fore gallows stands there)
  const ST_FD = { xa:1.55, xb:2.15, y0:6.40, y1:BRK, n:6 };
  // working-deck gear
  const HATCH   = { id:'hold_hatch',     y:1.7, hw:1.15, hl:0.95, h:0.30, maxDeg:95 };   // the fish hold's hatch amidships, hinged on its forward edge
  const HATCH_F = { id:'hold_hatch_fwd', y:4.9, hw:1.15, hl:0.95, h:0.30, maxDeg:95 };   // the forward fish hatch, the same
  const HATCHES = [HATCH, HATCH_F];
  const MY = 3.3, MAST_TOP = 11.2, XTREE = 8.5;           // the foremast, between the hatches (the old rig's)
  const WINCH = { y:-3.4, hx:1.45, hy:0.50, drumX:0.75, drumHx:0.48, z:DECK+0.74 };   // the trawl winch, athwartships in front of the house: a drum either side of its gearbox
  const POUND = { x0:-2.35, x1:-1.05, y0:-2.20, y1:0.20, ym:-1.00, h:0.32 };          // the fish pounds: two pens boarded off on the port side
  const SORT  = { x0:-0.60, x1:1.90, y0:-2.30, y1:-0.10, spill:{ x:0.60, y:-1.20 }, sorterX:[-0.45,1.25], sorterY:-1.75 };   // the sorting ground: clear deck between the winch and the hatch
  const NETS  = { y0:0.60, y1:4.80, w:0.85, off:0.20, h:0.50 };                       // the net, stowed along the working-side bulwark
  // the gallows, fore and aft at the starboard rail: an inverted U whose legs stand on the deck inside the bulwark and
  // lean out, so its head and the door's block hang outboard of the rail
  const GAL = { ys:[6.2,-1.6], hy:0.55, headHy:0.28, zt:4.35, blockZ:4.05, out:0.22 };
  const GAL_BLOCK = GAL.ys.map(y=>({ x:+(railAt(y).x+GAL.out).toFixed(3), y, z:GAL.blockZ }));   // [fore, aft]
  // the warps' deck bollards: a sheave on a post where each warp turns out to its gallows
  const WARP_LEAD = [ { x:1.85, y:6.00 }, { x:2.40, y:-2.30 } ];                    // [fore, aft]
  // the cod end's road aboard: over the working-side rail between the gallows, under the derrick's head
  const COD_Y = -0.70;
  const DERRICK = { heel:{ x:0, y:MY-0.16, z:DECK+1.25 }, head:{ x:+(railAt(COD_Y).x-0.25).toFixed(3), y:COD_Y, z:6.80 } };
  const FUNNEL = { y0:-8.85, y1:-9.05, r:0.38, zt:7.20 };
  const MIZZEN = { y:-9.47, zt:8.80 };
  const CAPSTAN = { y:-11.95, r:0.22 };
  const QUARTER = { y:-11.85 };
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
  const WINDLASS_Y = 9.6, BOWBOLL = { x:0.85, y:8.6 };
  // the helm: the cruiser's seated fit (30 of 30 on character rig 10.3), carried to the wheelhouse's sole
  const HS_F = DECK+0.02, BR_F = +(WZ0+0.02).toFixed(2);                 // the rooms' soles: the house's on the deck plate, the wheelhouse's on the boat deck
  const CONSOLE_Y = -5.40;                                               // the console's after face
  const WHEEL_HUB = { x:0.30, y:CONSOLE_Y-0.18, z:BR_F+0.79 };
  const WHEEL_GEO = { rad:0.19, rimIn:0.158, rake:-20, seg:16, hubR:0.05, spokes:[0,180,270], col:0.10 };
  const HELM  = { x:0.30, y:CONSOLE_Y-0.60, z:BR_F+0.51 };
  const PILOT = { x:0.30, y:CONSOLE_Y-0.16, z:BR_F };
  const CHART = { x0:1.02, y0:-7.30, y1:-6.76 };                         // the chart table, on the wheelhouse's starboard side
  const SAMSON = { x:0, y:11.7, z:fdAt(11.7).z+0.22 };    // the samson post on the stem head
  // glass and ports (the mesh, the rooms and the old readers' HOUSE block all read these)
  const GZ0 = 5.55, GZ1 = 6.35;
  const W_FRONT = [[-1.38,-0.52],[-0.40,0.40],[0.52,1.38]];
  const W_SIDE  = [[-7.90,-7.10],[-6.80,-6.00],[-5.75,-5.15]];
  const W_AFT   = [[-1.38,-0.75],[-0.55,0.35]];
  const HPORT_Y = [-8.8,-7.6,-6.4,-5.4], HPORT_Z = DECK+1.55, HPORT_R = 0.22;

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
    // a rail: stanchions at pts (their feet), a top rail h up and a mid rail half way
    const rail=(pts,h,mat,skip)=>{
      for(let i=0;i<pts.length;i++){ const p=pts[i]; tubeF(p,[p[0],p[1],p[2]+h],0.022,mat,-0.1);
        if(i+1<pts.length && !(skip&&skip(i))){ const q=pts[i+1];
          tubeF([p[0],p[1],p[2]+h],[q[0],q[1],q[2]+h],0.028,mat,0.15); tubeF([p[0],p[1],p[2]+h*0.5],[q[0],q[1],q[2]+h*0.5],0.018,mat,-0.1); } } };

    // ================= THE HULL =======================================================================
    TAG('hull','shell');
    // ---- shell: the old rig's bands by the section's depth: boot to 0.30, rust-red to 0.90, the cream cove line to 0.938, red to the sheer
    const BANDS=[0,0.30,0.90,0.938,1];
    const BMAT=[['boot',0.3,0],['hull',1.0,0],['cove',0.9,0.01],['hull',1.0,0]];
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(let k=0;k<BMAT.length;k++)
          fo([skin(side,u0,BANDS[k]),skin(side,u1,BANDS[k]),skin(side,u1,BANDS[k+1]),skin(side,u0,BANDS[k+1])],BMAT[k][0],BMAT[k][1],BMAT[k][2],[side,0,0]);
        // the rail cap along the whole sheer, in the cove line's paint
        const oa=skin(side,u0,1), ob=skin(side,u1,1), w=0.22;
        fo([oa,ob,[ob[0]-side*Math.min(w,Math.abs(ob[0])),ob[1],ob[2]-0.004],[oa[0]-side*Math.min(w,Math.abs(oa[0])),oa[1],oa[2]-0.004]],'cove',-0.5,0.03,[0,0,1]);
      }
    }
    for(let i=0;i<NSEG;i++){ const u0=i/NSEG, u1=(i+1)/NSEG;
      fo([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'boot',-1.0,0,[0,0,-1]); }
    for(let k=0;k<BMAT.length;k++){                       // the stem bar, and the stern's flat
      fo([skin(-1,1,BANDS[k]),skin(1,1,BANDS[k]),skin(1,1,BANDS[k+1]),skin(-1,1,BANDS[k+1])],BMAT[k][0],BMAT[k][1]+0.25,BMAT[k][2],[0,1,0]);
      fo([skin(-1,0,BANDS[k]),skin(1,0,BANDS[k]),skin(1,0,BANDS[k+1]),skin(-1,0,BANDS[k+1])],BMAT[k][0],BMAT[k][1]-0.5,BMAT[k][2],[0,-1,0]); }
    // ---- the taffrail: the covering board across the stern and its inner face -----------------------------
    { const st0=station(0), ya=-L/2, yb=yOf(U0), zt=st0.kz+st0.dep;
      fo([[-(st0.ws-0.02),ya,zt],[st0.ws-0.02,ya,zt],[dw(U0),yb,zt-0.004],[-dw(U0),yb,zt-0.004]],'cove',-0.5,0.03,[0,0,1]);
      fo([[-dw(U0),yb,DECK],[dw(U0),yb,DECK],[dw(U0),yb,zt-0.004],[-dw(U0),yb,zt-0.004]],'deck',-0.9,0,[0,1,0]); }
    // ---- the break of the fo'c'sle: the bulkhead across the deck under the whaleback -----------------
    fo([[-dw(SOLE_U),BRK,DECK],[dw(SOLE_U),BRK,DECK],[FD[0].half,BRK,FD_Z0],[-FD[0].half,BRK,FD_Z0]],'cream',-0.5,0,[0,-1,0]);

    TAG('hull','trim');
    // ---- the rubbing strake: a proud belt at the deck line, stern quarter to the break ------------------
    for(const side of [-1,1]) for(let i=1;i<19;i++){
      const u0=i/NSEG, u1=(i+1)/NSEG, za=DECK-0.08, zb=DECK+0.08, o=0.06;
      const a0=onHull(side,u0,za,o), a1=onHull(side,u1,za,o), b0=onHull(side,u0,zb,o), b1=onHull(side,u1,zb,o);
      fo([a0,a1,b1,b0],'hull',0.2,0.02,[side,0,0]);
      fo([b0,b1,onHull(side,u1,zb,0),onHull(side,u0,zb,0)],'hull',1.2,0.02,[0,0,1]);
    }
    for(const side of [-1,1]){
      // freeing ports through the bulwark, over the strake
      for(const uc of [0.12,0.22,0.32,0.42,0.52,0.62,0.72]){ const du=0.36/L, z0=DECK+0.11, z1=DECK+0.27;
        fo([onHull(side,uc-du,z0,0.012),onHull(side,uc+du,z0,0.012),onHull(side,uc+du,z1,0.012),onHull(side,uc-du,z1,0.012)],'blk',-0.4,0.03,[side,0,0]); }
      // draft marks at the stern and the stem
      for(const uc of [0.06,0.93]) for(let k=0;k<3;k++){ const z0=1.20+k*0.24, du=0.11/L;
        fo([onHull(side,uc-du,z0,0.012),onHull(side,uc+du,z0,0.012),onHull(side,uc+du,z0+0.10,0.012),onHull(side,uc-du,z0+0.10,0.012)],'cream',0.2,0.03,[side,0,0]); }
      // the port letters and number on the bow, under the cove line: black blocks, two and three
      [0.842,0.858,0.880,0.896,0.912].forEach((uc)=>{ const du=0.15/L, z=sheerU(uc)-0.78;
        fo([onHull(side,uc-du,z-0.20,0.012),onHull(side,uc+du,z-0.20,0.012),onHull(side,uc+du,z+0.20,0.012),onHull(side,uc-du,z+0.20,0.012)],'blk',1.4,0.03,[side,0,0]);
        fo([onHull(side,uc-du*0.35,z-0.08,0.02),onHull(side,uc+du*0.35,z-0.08,0.02),onHull(side,uc+du*0.35,z+0.08,0.02),onHull(side,uc-du*0.35,z+0.08,0.02)],'cove',0.6,0.04,[side,0,0]); });
      // the hawse pipe and the anchor housed in it
      { const uc=0.945, z=sheerU(uc)-1.05;
        fo(ngon(8,0.20,(a,b)=>onHull(side,uc+a/L,z+b,0.015)),'blk',-0.6,0.03,[side,0,0]);
        fo([onHull(side,uc-0.07/L,z-0.62,0.05),onHull(side,uc+0.07/L,z-0.62,0.05),onHull(side,uc+0.07/L,z+0.04,0.05),onHull(side,uc-0.07/L,z+0.04,0.05)],'iron',0.5,0.05,[side,0,0]);
        fo([onHull(side,uc-0.34/L,z-0.76,0.06),onHull(side,uc+0.34/L,z-0.76,0.06),onHull(side,uc+0.22/L,z-0.58,0.06),onHull(side,uc-0.22/L,z-0.58,0.06)],'iron',0.2,0.05,[side,0,0]); }
    }

    // ================= THE WORKING DECK ===============================================================
    TAG('main_deck','floor');
    { const N=20;
      for(let i=0;i<N;i++){ const u0=U0+(SOLE_U-U0)*i/N, u1=U0+(SOLE_U-U0)*(i+1)/N, y0=yOf(u0), y1=yOf(u1);
        const worn=y0>WINCH.y && y1<HATCH.y-HATCH.hl;      // the sorting ground, worn darker
        fo([[-dw(u0),y0,DECK],[dw(u0),y0,DECK],[dw(u1),y1,DECK],[-dw(u1),y1,DECK]],'deck',worn?-0.6:-0.35,0,[0,0,1]); } }
    TAG('main_deck','wall');
    // ---- the bulwark's inner face and its stanchions ---------------------------------------------------
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=Math.min(SOLE_U,(i+1)/NSEG); if(u1<=u0) break;
        const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb), LT=0.985;
        fo([skin(side,u1,fb,1),skin(side,u0,fa,1),skin(side,u0,LT,1),skin(side,u1,LT,1)],'deck',-0.9,-0.03,[-side,0,0]);
      }
      for(let y=-11.2; y<7.0; y+=1.6){ const x=dwY(y)+0.01, zt=sheerU(uOf(y))-0.06;
        fo([[side*x,y-0.04,DECK],[side*(x-0.18),y-0.04,DECK],[side*x,y-0.04,zt]],'deck',-0.3,0,[0,-1,0]);
        fo([[side*x,y+0.04,DECK],[side*(x-0.18),y+0.04,DECK],[side*x,y+0.04,zt]],'deck',0.2,0,[0,1,0]);
        fo([[side*(x-0.18),y-0.04,DECK],[side*(x-0.18),y+0.04,DECK],[side*x,y+0.04,zt],[side*x,y-0.04,zt]],'deck',0.5,0,[-side,0,0.3]); }
    }

    TAG('main_deck','fit');
    // ---- the stern: quarter bollards (twin posts on a foot) and the capstan ------------------------------
    for(const s of [-1,1]){ const x=s*(dwY(QUARTER.y)-0.34);
      boxF([x,QUARTER.y,DECK+0.04],[0.12,0.26,0.04],'iron',0.1,-0.02);
      for(const dy of [-0.14,0.14]) cylF([x,QUARTER.y+dy,DECK+0.06],[x,QUARTER.y+dy,DECK+0.38],0.065,6,'iron',0.3,0,1,0.08); }
    cylF([0,CAPSTAN.y,DECK],[0,CAPSTAN.y,DECK+0.14],CAPSTAN.r+0.06,8,'iron',0.2,0,1);
    cylF([0,CAPSTAN.y,DECK+0.14],[0,CAPSTAN.y,DECK+0.62],CAPSTAN.r-0.06,8,'steel',-0.3,0,0,CAPSTAN.r-0.10);
    cylF([0,CAPSTAN.y,DECK+0.62],[0,CAPSTAN.y,DECK+0.74],CAPSTAN.r,8,'iron',0.4,0,1);
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
    // ---- the trawl winch's bed, pedestals, gearbox and brakes (its drums are a part: state winch) ---------
    { const Wn=WINCH, zc=Wn.z;
      boxF([0,Wn.y,DECK+0.09],[Wn.hx,Wn.hy,0.09],'iron',0.2,-0.02);                                           // the bed
      for(const s of [-1,1]){
        for(const x of [s*(Wn.drumX-Wn.drumHx-0.10),s*(Wn.drumX+Wn.drumHx+0.10)]) boxF([x,Wn.y,DECK+0.46],[0.07,0.34,0.30],'iron',0.5);   // the bearing pedestals
        cylF([s*(Wn.drumX+Wn.drumHx+0.17),Wn.y,zc],[s*(Wn.drumX+Wn.drumHx+0.40),Wn.y,zc],0.16,8,'steel',0.1,0,1);   // the warping head
        boxF([s*0.36,Wn.y+0.36,DECK+0.42],[0.06,0.06,0.42],'iron',0.4); tubeF([s*0.36,Wn.y+0.36,DECK+0.80],[s*0.36,Wn.y+0.46,DECK+1.12],0.022,'steel',0.3);   // the brake lever, on the forward side
      }
      boxF([0,Wn.y-0.04,DECK+0.52],[0.13,0.34,0.34],'iron',0.3,-0.02);                                        // the gearbox
    }
    // ---- the warps' deck bollards: a sheave on a post -----------------------------------------------------
    for(const Wp of WARP_LEAD){ boxF([Wp.x,Wp.y,DECK+0.24],[0.10,0.10,0.24],'iron',0.4);
      cylF([Wp.x,Wp.y,DECK+0.48],[Wp.x,Wp.y,DECK+0.60],0.21,8,'steel',-0.2,0,1,null,0.2); }
    // ---- the fish pounds: two pens of pound boards on the port side, the catch's gurry dark in them --------
    { const P=POUND, t=0.03, z1=DECK+P.h;
      for(const x of [P.x0,P.x1]) F.push.apply(F, slab([x,(P.y0+P.y1)/2,(DECK+z1)/2],[t,(P.y1-P.y0)/2,P.h/2],'wood',0.5,0.1));
      for(const y of [P.y0,P.ym,P.y1]) F.push.apply(F, slab([(P.x0+P.x1)/2,y,(DECK+z1)/2],[(P.x1-P.x0)/2,t,P.h/2],'wood',0.5,-0.2));
      pZ(F,1,DECK+0.012,P.x0+t,P.x1-t,P.y0+t,P.y1-t,'deck',-1.3,0.01); }
    // ---- fish totes: in the pound, on the sorting ground, by the hatch -----------------------------------
    for(const [x,y,n] of [[-1.8,-1.8,2],[1.6,-1.8,1],[-1.8,0.9,2],[1.6,0.9,1]]) for(let k=0;k<n;k++){
      F.push.apply(F, slab([x,y,DECK+0.15+k*0.31],[0.31,0.38,0.15],'safe',0.4,-0.3));
      pZ(F,1,DECK+0.302+k*0.31,x-0.25,x+0.25,y-0.32,y+0.32,'safe',-1.5,0.01); }
    // ---- the stair to the fo'c'sle head, on the port side: treads on two stringers, a handrail outboard ----
    { const s=-1, A=ST_FD, run=(A.y1-A.y0)/A.n, rise=(FD_Z0-DECK)/A.n, xm=s*(A.xa+A.xb)/2, hx=(A.xb-A.xa)/2;
      for(let k=1;k<A.n;k++) F.push.apply(F, slab([xm,A.y0+(k+0.5)*run,DECK+k*rise-0.02],[hx,run/2,0.02],'deck',0.4,-0.6));
      for(const x of [s*A.xa,s*A.xb]) fo([[x,A.y0+run,DECK],[x,A.y1,FD_Z0-0.02],[x,A.y1,FD_Z0-0.28],[x,A.y0+run*1.9,DECK]],'iron',0.5,0,[x*s>(A.xa+A.xb)/2?s:-s,0,0]);
      rail([[s*(A.xb-0.03),A.y0+run,DECK+rise],[s*(A.xb-0.03),(A.y0+run+A.y1)/2,(DECK+rise+FD_Z0)/2],[s*(A.xb-0.03),A.y1,FD_Z0]],0.95,'steel'); }

    // ================= THE GEAR ALOFT (level rigging: no cut lifts it) ====================================
    TAG('rigging','rig');
    // ---- the foremast: ochre, stepped on the deck between the hatches; the crosstree, the masthead light ----
    cylF([0,MY,DECK],[0,MY,MAST_TOP],0.13,8,'buff',0,0,1,0.07);
    boxF([0,MY,DECK+0.10],[0.22,0.22,0.10],'iron',0.3,-0.02);
    tubeF([-0.95,MY,XTREE],[0.95,MY,XTREE],0.04,'buff',0.1);
    boxF([0,MY-0.14,MAST_TOP-0.02],[0.07,0.07,0.08],'cream',1.0);
    for(const s of [-1,1]){                               // the shrouds, to the rail abaft the mast
      const ys=MY-0.9, r=railAt(ys);
      tubeF([s*0.95,MY,XTREE],[s*(r.x-0.10),ys,r.z],0.014,'steel',-0.3); tubeF([s*0.10,MY,MAST_TOP-0.6],[s*0.95,MY,XTREE],0.014,'steel',-0.3); }
    tubeF([0,MY,MAST_TOP-0.5],[0,FD[FD.length-1].y-0.25,FD[FD.length-1].z+1.20],0.014,'steel',-0.2);         // the forestay to the jackstaff
    // ---- the derrick: heeled on the mast, topped up aft over the working-side rail; the gilson hangs from its head
    { const A=DERRICK.heel, B=DERRICK.head;
      cylF([A.x,A.y,A.z],[B.x,B.y,B.z],0.085,6,'buff',-0.1,0,1,0.055);
      boxF([A.x,A.y+0.04,A.z],[0.10,0.10,0.10],'iron',0.3);
      tubeF([0,MY-0.05,XTREE+1.6],[B.x,B.y,B.z+0.04],0.014,'steel',-0.3);                                     // the topping lift
      boxF([B.x,B.y,B.z-0.14],[0.06,0.06,0.10],'iron',0.2); tubeF([B.x,B.y,B.z-0.24],[B.x,B.y,B.z-1.70],0.016,'steel',-0.3);   // the gilson block and its whip
      boxF([B.x,B.y,B.z-1.78],[0.05,0.03,0.08],'iron',0.4); }
    // ---- the two gallows at the starboard rail, and each warp: off the winch, through its bollard, up to its block
    GAL.ys.forEach((gy,i)=>{ const B=GAL_BLOCK[i], zt=GAL.zt;
      const foot=(dy)=>[dwY(gy+dy)-0.13, gy+dy, DECK], head=(dy)=>[B.x, gy+dy, zt];
      for(const sy of [-1,1]){
        cylF(foot(sy*GAL.hy),head(sy*GAL.headHy),0.075,6,'buff',sy<0?-0.2:0.1,0,0);
        boxF([foot(sy*GAL.hy)[0],gy+sy*GAL.hy,DECK+0.04],[0.14,0.14,0.04],'iron',0.2,-0.02);
        tubeF([foot(sy*GAL.hy)[0]-0.55,gy+sy*GAL.hy,DECK],[lerp(foot(sy*GAL.hy)[0],B.x,0.45),lerp(gy+sy*GAL.hy,gy+sy*GAL.headHy,0.45),lerp(DECK,zt,0.45)],0.04,'buff',-0.3); }   // the inboard strut
      cylF(head(-GAL.headHy-0.06),head(GAL.headHy+0.06),0.085,6,'buff',0.3,0,2);                                // the head bar
      boxF([B.x,B.y,zt-0.14],[0.05,0.05,0.07],'iron',0.2); boxF([B.x,B.y,B.z-0.06],[0.07,0.12,0.14],'iron',0.5,-0.02);   // the hanging block
      const Wp=WARP_LEAD[i], dx=i?WINCH.drumX:-WINCH.drumX;                                                    // the fore warp is the port drum's, the aft warp the starboard drum's
      tubeF([dx,WINCH.y+0.26,WINCH.z+0.10],[Wp.x-0.03,Wp.y-0.20,DECK+0.54],0.020,'steel',-0.4);
      tubeF([Wp.x+0.03,Wp.y+0.20*(i?1:0),DECK+0.54],[B.x,B.y,B.z],0.020,'steel',-0.5);
    });

    // ================= THE DECKHOUSE (level house) =========================================================
    TAG('house','wall',{ glas:'trim', iron:'trim', steel:'trim', safe:'fit', dark:'trim' });
    (function(){ const D=DOOR;
      pY(F,1,HYf,-HXl,HXl,DECK,HZ1l,'cream',0.1);
      pY(F,-1,HYa,-HXl,D.x0,DECK,HZ1l,'cream',-0.3); pY(F,-1,HYa,D.x1,HXl,DECK,HZ1l,'cream',-0.3); pY(F,-1,HYa,D.x0,D.x1,D.z1,HZ1l,'cream',-0.3);
      pX(F,-1,-HXl,HYa,HYf,DECK,HZ1l,'cream',-0.1); pX(F,1,HXl,HYa,HYf,DECK,HZ1l,'cream',-0.1);
      // the door's frame and sill
      for(const [xa,xb,za,zb] of [[D.x0-0.07,D.x0,DECK,D.z1+0.07],[D.x1,D.x1+0.07,DECK,D.z1+0.07],[D.x0,D.x1,D.z1,D.z1+0.07],[D.x0,D.x1,DECK,D.z0]])
        pY(F,-1,HYa-0.012,xa,xb,za,zb,'steel',-0.4,0.03);
      for(const side of [-1,1]) for(const y of HPORT_Y) portX(side,side*HXl,y,HPORT_Z,HPORT_R);              // four ports a side
      // a lifering in its cradle on the front wall, over the winch
      const P=(r,a)=>[-0.95+r*Math.cos(a),HYf+0.06,3.55+r*Math.sin(a)];
      for(let k=0;k<8;k++){ const a0=2*Math.PI*k/8, a1=2*Math.PI*(k+1)/8; fo([P(0.17,a0),P(0.34,a0),P(0.34,a1),P(0.17,a1)],k%2?'safe':'cream',k%2?0.3:0.6,0.05,[0,1,0]); }
    })();
    // ---- the boat deck: the house's top, the wheelhouse's sole (level bridge: it lifts with the wheelhouse)
    TAG('bridge','floor',{ cream:'trim' });
    { const x=HXl+0.10, ya=HYa-0.08, yf=HYf+0.08;
      pZ(F,1,WZ0,-x,x,ya,yf,'deck',0.25);
      pY(F,-1,ya,-x,x,HZ1l,WZ0,'cream',0.5); pY(F,1,yf,-x,x,HZ1l,WZ0,'cream',0.5);
      pX(F,-1,-x,ya,yf,HZ1l,WZ0,'cream',0.5); pX(F,1,x,ya,yf,HZ1l,WZ0,'cream',0.5);
      pZ(F,-1,HZ1l,-x,x,ya,yf,'cream',-1.6); }
    TAG('bridge','fit',{ buff:'tall', blk:'tall' });
    { const x=HXl+0.05, ya=HYa-0.03, z=WZ0, P=[];
      // rails round the boat deck, from abreast the wheelhouse's front down each side and across the stern end
      for(const y of [-5.0,-6.2,-7.4,-8.5]) P.push([-x,y,z]);
      P.push([-x,ya,z]); for(const xx of [-0.85,0,0.85]) P.push([xx,ya,z]); P.push([x,ya,z]);
      for(const y of [-8.5,-7.4,-6.2,-5.0]) P.push([x,y,z]);
      rail(P,0.95,'steel');
      // the liferaft canister in its cradle, on the port side abaft the wheelhouse
      { const xr=-1.12;
        for(const y of [-9.15,-8.65]) boxF([xr,y,z+0.07],[0.18,0.04,0.07],'iron',0.3);
        cylF([xr,-9.35,z+0.34],[xr,-8.45,z+0.34],0.22,8,'cream',0.2,0,2,null,0.5);
        for(const y of [-9.10,-8.70]) cylF([xr,y-0.03,z+0.34],[xr,y+0.03,z+0.34],0.232,8,'iron',0.4,0,0); }
      // the funnel: buff, raked, a band of the hull's paint, a black cap, the exhaust standing out of it
      const Fn=FUNNEL, at=(t)=>[0,lerp(Fn.y0,Fn.y1,t),lerp(z,Fn.zt,t)];
      cylF(at(0),at(0.60),Fn.r,10,'buff',0,0,0,Fn.r*0.95);
      cylF(at(0.60),at(0.74),Fn.r*0.97,10,'hull',0.2,0,0,Fn.r*0.94);
      cylF(at(0.74),at(1),Fn.r*0.94,10,'blk',1.6,0,1,Fn.r*0.90,0.6);
      cylF([0,Fn.y1+0.02,Fn.zt-0.1],[0,Fn.y1-0.08,Fn.zt+0.36],0.075,6,'iron',0.6,0,1,null,-0.5);
      boxF([0,Fn.y0+0.02,z+0.05],[Fn.r+0.10,Fn.r+0.10,0.05],'iron',0.5,-0.02);
      // the mizzen pole abaft the funnel, the stern light on it
      cylF([0,MIZZEN.y,z],[0,MIZZEN.y,MIZZEN.zt],0.06,6,'buff',0,0,1,0.04);
      boxF([0,MIZZEN.y-0.09,z+1.20],[0.06,0.05,0.07],'cream',1.2);
    }

    // ================= THE WHEELHOUSE (level bridge) =======================================================
    TAG('bridge','wall',{ glas:'trim', iron:'trim', steel:'trim', safe:'fit' });
    (function(){ const D=WDOOR;
      // plumb sides; the front rakes AFT as it rises (FYb at the sole, FYt at the head)
      fo([[-HXw,WYa,WZ0],[-HXw,WYa,WZ1],[-HXw,FYt,WZ1],[-HXw,FYb,WZ0]],'cream',-0.1,0,[-1,0,0]);
      fo([[HXw,WYa,WZ0],[HXw,FYb,WZ0],[HXw,FYt,WZ1],[HXw,WYa,WZ1]],'cream',-0.1,0,[1,0,0]);
      fo([[-HXw,FYt,WZ1],[HXw,FYt,WZ1],[HXw,FYb,WZ0],[-HXw,FYb,WZ0]],'cream',0.2,0,[0,1,0.17]);
      pY(F,-1,WYa,-HXw,D.x0,WZ0,WZ1,'cream',-0.3); pY(F,-1,WYa,D.x1,HXw,WZ0,WZ1,'cream',-0.3); pY(F,-1,WYa,D.x0,D.x1,D.z1,WZ1,'cream',-0.3);
      for(const [xa,xb,za,zb] of [[D.x0-0.06,D.x0,WZ0,D.z1+0.06],[D.x1,D.x1+0.06,WZ0,D.z1+0.06],[D.x0,D.x1,D.z1,D.z1+0.06],[D.x0,D.x1,WZ0,D.z0]])
        pY(F,-1,WYa-0.012,xa,xb,za,zb,'steel',-0.4,0.03);
      const ry=(WZ1-WZ0), rz=(FYb-FYt), rn=Math.hypot(ry,rz), nY=ry/rn, nZ=rz/rn;
      for(const [xa,xb] of W_FRONT)                                                                          // the three-pane windscreen
        glaze((pr)=>((x,z)=>[x, yFront(z)+nY*pr, z+nZ*pr]), [0,nY,nZ], xa,xb, GZ0,GZ1, 0.3, 0.06);
      for(const side of [-1,1]) for(const [ya,yb] of W_SIDE)                                                 // three windows a side
        glaze((pr)=>((y,z)=>[side*(HXw+pr),y,z]), [side,0,0], ya,yb, GZ0,GZ1, -0.3);
      for(const [xa,xb] of W_AFT)                                                                            // the windows aft, beside the door
        glaze((pr)=>((x,z)=>[x,WYa-pr,z]), [0,-1,0], xa,xb, GZ0,GZ1, -0.3);
      // sidelights on their screens: red to port, green to starboard (no green ramp: the glass's)
      for(const side of [-1,1]){ boxF([side*(HXw+0.16),-6.40,6.20],[0.14,0.28,0.02],'iron',0.3); pX(F,side,side*(HXw+0.04),-6.68,-6.12,6.20,6.46,'iron',0.6,0.03);
        boxF([side*(HXw+0.16),-6.40,6.30],[0.07,0.08,0.07],side<0?'safe':'glas',side<0?0.6:1.5); }
    })();
    // ---- the wheelhouse top (level top): the roof and its brow, then what stands on it ---------------------
    TAG('top','roof');
    { const x=HXw+0.14, ya=WYa-0.14, yf=FYb+0.02;
      pZ(F,1,ROOFZ,-x,x,ya,yf,'cream',0.45);
      pY(F,-1,ya,-x,x,WZ1,ROOFZ,'cream',0.3); pY(F,1,yf,-x,x,WZ1,ROOFZ,'cream',0.6);
      pX(F,-1,-x,ya,yf,WZ1,ROOFZ,'cream',0.3); pX(F,1,x,ya,yf,WZ1,ROOFZ,'cream',0.3);
      pZ(F,-1,WZ1,-x,x,ya,yf,'cream',-1.8); }
    TAG('top','fit',{ buff:'rig' });
    (function(){ const z=ROOFZ;
      // the searchlight, the horn, a whip aerial, the radar on its post
      cylF([1.05,-5.05,z],[1.05,-5.05,z+0.28],0.05,5,'steel',0,0,0); cylF([1.05,-5.18,z+0.40],[1.05,-4.88,z+0.40],0.14,8,'steel',-0.1,0,2,null,0.2);
      pY(F,1,-4.875,0.95,1.15,z+0.30,z+0.50,'cream',1.2,0.02);
      cylF([-1.0,-5.35,z+0.15],[-1.0,-4.85,z+0.15],0.055,6,'steel',0.1,0,1,0.10);
      tubeF([-(HXw-0.12),-7.9,z],[-(HXw-0.12),-8.0,z+2.6],0.018,'steel',0.2); boxF([-(HXw-0.12),-7.9,z+0.06],[0.05,0.05,0.06],'iron',0.3);
      cylF([0,-6.9,z],[0,-6.9,z+0.90],0.07,6,'buff',0,0,0);
      boxF([0,-6.9,z+0.93],[0.30,0.30,0.03],'steel',0.2); cylF([0,-6.9,z+0.96],[0,-6.9,z+1.12],0.10,6,'steel',0,0,1);
      boxF([0,-6.9,z+1.19],[0.72,0.07,0.06],'cream',0.6);                                                    // the scanner
    })();

    // ================= THE FO'C'SLE HEAD (level foredeck) ==================================================
    TAG('foredeck','floor');
    for(let i=0;i+1<FD.length;i++){ const A=FD[i], B=FD[i+1];
      fo([[-A.half,A.y,A.z],[A.half,A.y,A.z],[B.half,B.y,B.z],[-B.half,B.y,B.z]],'deck',-0.35,0,[0,0,1]); }
    TAG('foredeck','fit',{ deck:'trim' });
    (function(){ const zAt=(y)=>fdAt(y).z;
      // the anchor windlass: a bed, two gypsies, the warping ends, the motor; the cables run forward to the hawse
      const wy=WINDLASS_Y, wz=zAt(wy);
      boxF([0,wy,wz+0.07],[0.62,0.34,0.07],'iron',0.3,-0.02);
      for(const s of [-1,1]){ boxF([s*0.22,wy,wz+0.30],[0.05,0.24,0.18],'iron',0.5);
        cylF([s*0.27,wy,wz+0.40],[s*0.50,wy,wz+0.40],0.22,8,'steel',-0.6,0,2,null,0.0);
        cylF([s*0.50,wy,wz+0.40],[s*0.74,wy,wz+0.40],0.12,8,'steel',0.2,0,1,0.16);
        tubeF([s*0.38,wy+0.20,wz+0.44],[s*0.42,11.0,zAt(11.0)+0.09],0.035,'iron',0.6);
        boxF([s*0.42,11.05,zAt(11.05)+0.07],[0.11,0.14,0.07],'iron',0.4,-0.02); }
      boxF([0,wy-0.04,wz+0.30],[0.17,0.22,0.17],'steel',-0.1);
      // the bow bollards and the samson post (the cleats' places)
      for(const s of [-1,1]){ const bz=zAt(BOWBOLL.y); boxF([s*BOWBOLL.x,BOWBOLL.y,bz+0.03],[0.12,0.26,0.03],'iron',0.1,-0.02);
        for(const dy of [-0.14,0.14]) cylF([s*BOWBOLL.x,BOWBOLL.y+dy,bz+0.05],[s*BOWBOLL.x,BOWBOLL.y+dy,bz+0.36],0.065,6,'iron',0.3,0,1,0.08); }
      const sp=SAMSON; boxF([sp.x,sp.y,sp.z-0.02],[0.08,0.08,0.20],'iron',0.3); tubeF([-0.22,sp.y,sp.z+0.06],[0.22,sp.y,sp.z+0.06],0.032,'iron',0.6);
      // the jackstaff at the stem head
      const Z=FD[FD.length-1]; tubeF([0,Z.y-0.25,Z.z],[0,Z.y-0.25,Z.z+1.20],0.022,'steel',0.2);
      // rails round the fo'c'sle head, from the break to the stem (the port rail starts forward of the stair's head)
      for(const s of [-1,1]){ const P=[]; for(let i=0;i<FD.length;i+=2){ const A=FD[i]; if(A.half<0.25) break; P.push([s*(A.half-0.08),A.y+(i?0:0.04),A.z]); }
        rail(P,0.95,'steel'); }
    })();
    TAG();
  })();

  // ================= THE PARTS THAT MOVE =================================================================
  const r3=(v)=>Math.round(v*1000)/1000;
  // ---- the trawl doors (tag 2; state doors 0..1): stowed hanging at the gallows' blocks, outboard of the working-side
  //      rail; lowered down the ship's side to the water on their warps
  const TD = { len:1.80, hgt:1.05, zStow:3.05, zDown:1.05 };
  function trawlDoorFaces(t, out){
    const from=out.length, s=1;
    for(const B of GAL_BLOCK){ const zc=lerp(TD.zStow,TD.zDown,t), x=B.x+s*0.02, hy=TD.len/2, hz=TD.hgt/2;
      const P=(dy,dz,o)=>[x+s*o, B.y+dy, zc+dz];
      // the plate: an oval-ish board, its corners cut, iron-shod along the keel
      const o8=[[-hy+0.20,-hz],[hy-0.35,-hz],[hy,-hz+0.35],[hy,hz-0.24],[hy-0.24,hz],[-hy+0.20,hz],[-hy,hz-0.20],[-hy,-hz+0.20]];
      faceO(out,o8.map(([a,b])=>P(a,b,0.07)),'wood',0.2,0,[s,0,0]);
      faceO(out,o8.map(([a,b])=>P(a,b,-0.03)),'wood',-0.8,0,[-s,0,0]);
      faceO(out,[P(-hy+0.20,hz,-0.03),P(hy-0.24,hz,-0.03),P(hy-0.24,hz,0.07),P(-hy+0.20,hz,0.07)],'iron',0.8,0,[0,0,1]);
      faceO(out,[P(-hy+0.16,-hz,0.085),P(hy-0.32,-hz,0.085),P(hy-0.04,-hz+0.32,0.085),P(hy-0.04,-hz+0.50,0.085),P(-hy+0.16,-hz+0.18,0.085)],'iron',0.4,0.02,[s,0,0]);   // the shoe
      for(const dy of [-0.42,0.08,0.55]) faceO(out,[P(dy-0.03,-hz+0.18,0.085),P(dy+0.03,-hz+0.18,0.085),P(dy+0.03,hz-0.04,0.085),P(dy-0.03,hz-0.04,0.085)],'steel',-0.3,0.02,[s,0,0]);   // the straps
      // the brackets and the warp up to the block
      for(const f of tube(P(-0.35,0.08,0.09),P(0,hz+0.24,0.10),0.018,'steel',-0.2)) out.push(f);
      for(const f of tube(P(0.35,0.08,0.09),P(0,hz+0.24,0.10),0.018,'steel',-0.2)) out.push(f);
      for(const f of tube(P(0,hz+0.24,0.10),[B.x,B.y,B.z-0.18],0.020,'steel',-0.5)) out.push(f);
    }
    stampTags(out, from, out.length, 'rigging', 'part');
    for(let i=from;i<out.length;i++) out[i].tag=2;
  }
  // ---- the winch's two drums (tag 3; state winch, deg): each a barrel wound with warp between two flanges ----
  function winchFaces(deg, out){
    const Wn=WINCH, rot=(deg||0)*DEG, from=out.length, push=(a)=>{ for(const f of a) out.push(f); };
    for(const s of [-1,1]){ const xi=s*(Wn.drumX-Wn.drumHx), xo=s*(Wn.drumX+Wn.drumHx);
      push(cyl([xi,Wn.y,Wn.z],[xo,Wn.y,Wn.z],0.27,10,'steel',-0.9,0,0,null,null,rot));                        // the barrel, wound with warp
      for(let k=0;k<4;k++){ const x=lerp(xi,xo,(k+0.5)/4); push(cyl([x-s*0.02,Wn.y,Wn.z],[x+s*0.02,Wn.y,Wn.z],0.285,10,'steel',-1.5,0,0,null,null,rot)); }   // the lay of the wire
      for(const [xa,xb,sg] of [[xi,xi+s*0.06,-s],[xo-s*0.06,xo,s]]){
        push(cyl([xa,Wn.y,Wn.z],[xb,Wn.y,Wn.z],0.48,10,'iron',0.9,0,2,null,0.5,rot));                         // the flanges
        for(let k=0;k<4;k++){ const a=rot+k*Math.PI/2+0.4, c=Math.cos(a), sn=Math.sin(a), r0=0.30, r1=0.42, w=0.10;   // their lightening holes turn with them
          const Q=(r,d)=>[(sg>0?Math.max(xa,xb):Math.min(xa,xb))+sg*0.012, Wn.y+r*c-d*sn, Wn.z+r*sn+d*c];
          faceO(out,[Q(r0,-w*0.7),Q(r1,-w),Q(r1,w),Q(r0,w*0.7)],'steel',-0.6,0.03,[sg,0,0]); } }
    }
    stampTags(out, from, out.length, 'main_deck', 'fit');
    for(let i=from;i<out.length;i++) out[i].tag=3;
  }
  // ---- the net (tag 4; state net 0..1): 0 stowed in a heap along the working-side bulwark, its floats on it;
  //      1 shot over the rail and streaming down the ship's side to the water
  function netFaces(net, out){
    const N=NETS, from=out.length, wound=1-net, push=(a)=>{ for(const f of a) out.push(f); };
    if(wound>0.05){ const k=0.35+0.65*wound, h=N.h*k, n=6;
      const xi=(y)=>dwY(y)-N.w, xo=(y)=>dwY(y)-N.off, xr=(y)=>lerp(xi(y),xo(y),0.45);
      for(let i=0;i<n;i++){ const y0=lerp(N.y0,N.y1,i/n), y1=lerp(N.y0,N.y1,(i+1)/n), b=i%2?0.2:-0.1;
        faceO(out,[[xi(y0),y0,DECK],[xi(y1),y1,DECK],[xr(y1),y1,DECK+h],[xr(y0),y0,DECK+h]],'net',b-0.5,0,[-1,0,0.6]);
        faceO(out,[[xr(y0),y0,DECK+h],[xr(y1),y1,DECK+h],[xo(y1),y1,DECK+h*0.8],[xo(y0),y0,DECK+h*0.8]],'net',b+0.4,0,[0,0,1]);
        if(i%2===0) push(box([xr((y0+y1)/2)+0.12,(y0+y1)/2,DECK+h+0.05],[0.10,0.10,0.07],'safe',0.3)); }
      for(const [y,d] of [[N.y0,-1],[N.y1,1]])
        faceO(out,[[xi(y),y,DECK],[xo(y),y,DECK],[xo(y),y,DECK+h*0.8],[xr(y),y,DECK+h]],'net',d<0?-0.7:0.1,0,[0,d,0.3]);
    }
    if(net>0.02){ const ya=N.y0+0.5, yb=N.y1-0.5, n=4, zEnd=lerp(railAt(ya).z-0.2, DWL-0.45, net);
      for(let i=0;i<n;i++){ const y0=lerp(ya,yb,i/n), y1=lerp(ya,yb,(i+1)/n), r0=railAt(y0), r1=railAt(y1), b=i%2?0.3:-0.1;
        faceO(out,[[dwY(y0)-0.45,y0,DECK+0.06],[dwY(y1)-0.45,y1,DECK+0.06],[r1.x-0.10,y1,r1.z+0.05],[r0.x-0.10,y0,r0.z+0.05]],'net',b,0.02,[-0.6,0,1]);
        faceO(out,[[r0.x-0.10,y0,r0.z+0.05],[r1.x-0.10,y1,r1.z+0.05],[r1.x+0.10,y1,r1.z+0.05],[r0.x+0.10,y0,r0.z+0.05]],'net',b+0.3,0.02,[0,0,1]);
        faceO(out,[[r0.x+0.10,y0,r0.z+0.05],[r1.x+0.10,y1,r1.z+0.05],[r1.x+0.16,y1,zEnd],[r0.x+0.16,y0,zEnd]],'net',b-0.2,0.02,[1,0,0.1]);
        push(box([r0.x+0.20,(y0+y1)/2,zEnd+0.10],[0.07,0.11,0.07],'safe',0.3)); }
    }
    stampTags(out, from, out.length, 'main_deck', 'fit');
    for(let i=from;i<out.length;i++) out[i].tag=4;
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
    return { doors:clamp01(+o.doors||0), winch:+o.winch||0, net:clamp01(+o.net||0), hatch:clamp01(+o.hatch||0),
             door:clamp01(+(o.doorOpen!=null?o.doorOpen:o.door)||0) };
  }
  const _asm=new Map();
  function assemble(s){
    const key=[r3(s.doors),Math.round(((s.winch%360)+360)%360*10)/10,r3(s.net),r3(s.hatch)].join('|');
    if(_asm.has(key)) return _asm.get(key);
    const dyn=[], SH=[];
    trawlDoorFaces(s.doors, dyn);
    winchFaces(s.winch, dyn);
    netFaces(s.net, dyn);
    hatchFaces(s.hatch, dyn);
    // cast shade along the key: the boat deck's on the deck round the house, the wheelhouse top's on the boat deck
    { const x=HXl+0.10, ya=HYa-0.08, yf=HYf+0.08;
      SH.push({ v:[[-x,ya,WZ0],[x,ya,WZ0],[x,yf,WZ0],[-x,yf,WZ0]], z0:DECK, steps:1, near:0.30, clip:[-3.45,3.45,-12.3,BRK] });
      const xr=HXw+0.14, yra=WYa-0.14, yrf=FYb+0.02;
      SH.push({ v:[[-xr,yra,ROOFZ],[xr,yra,ROOFZ],[xr,yrf,ROOFZ],[-xr,yrf,ROOFZ]], z0:WZ0, steps:1, near:0.30, clip:[-x,x,ya,yf] }); }
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
  // layers: painted WITH the hull, then masked to their own pixels (2 trawl doors, 3 winch drums, 4 net, 5 hatch lids)
  function layer(tagN){ return function(dir, opts){
    opts=norm(opts);
    const s=stateOf(opts), A=assemble(s);
    return _toRGBA(_paint(A.F.concat(doorFaces(s.door)), Object.assign({}, opts, {dir, onlyTag:tagN}), true));
  }; }
  const renderTrawlDoors=layer(2), renderWinch=layer(3), renderNet=layer(4), renderHatch=layer(5);

  // ---- the old rig's screen anchors, kept (cell pixels at a heading; pass the hull's rock pose in opts) ----
  const _scr=(dir,opts,pts)=>{ const B=camBasis(Object.assign({}, norm(opts), {dir})); return pts.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return { x:p.sx, y:p.sy }; }); };
  function helmSeat(dir, opts){ return _scr(dir,opts,[HELM])[0]; }
  const GALLOWS = GAL_BLOCK.map(b=>({ x:b.x, y:b.y, z:b.z }));    // the two gallows' block points, fore then aft (the old rig's order; they hang outboard of the rail now)
  function gallowsMounts(dir, opts){ return _scr(dir,opts,GALLOWS); }
  const HAULER = GALLOWS[0];
  function haulerMount(dir, opts){ return gallowsMounts(dir, opts)[0]; }
  const TUBS = [ {x:-1.8,y:0.9,z:DECK}, {x:1.6,y:0.9,z:DECK},
                 {x:-1.8,y:-1.8,z:DECK}, {x:1.6,y:-1.8,z:DECK}, {x:-1.8,y:6.0,z:DECK} ];   // the old rig's, number for number
  function tubMounts(dir, opts){ return _scr(dir,opts,TUBS); }
  // nav lights: sidelights on the wheelhouse sides, the stern light on the taffrail, the masthead light (the old rig's heights)
  const NAV = { port:{ x:-(HXw+0.16), y:-6.40, z:6.30 }, star:{ x:HXw+0.16, y:-6.40, z:6.30 }, stern:{ x:0, y:-12.35, z:3.30 }, mast:{ x:0, y:MY-0.14, z:MAST_TOP } };
  function navMounts(dir, opts){ const p=_scr(dir,opts,[NAV.port,NAV.star,NAV.stern,NAV.mast]); return { port:p[0], star:p[1], stern:p[2], mast:p[3] }; }
  function doorMount(dir, opts){
    opts=norm(opts); const t=clamp01(opts.doorOpen!=null?+opts.doorOpen:0), A=t*DOOR.maxDeg*DEG, w=DOOR.x1-DOOR.x0;
    const p=_scr(dir,opts,[{ x:(DOOR.x0+DOOR.x1)/2, y:HYa, z:DECK }, { x:DOOR.x0+w*Math.cos(A), y:HYa-w*Math.sin(A), z:DECK }]);
    return { x:p[0].x, y:p[0].y, lead:p[1], open:t, clear:t>=DOOR.clearAt };
  }
  // hull half-width at (y, z) and sheer height at y, as the old loft answered them (by station: no rake)
  function halfAtZ(y, z){ const u=Math.max(0,Math.min(1,(y+L/2)/L)), st=station(u); return lerp(st.wb,st.ws,fracAt(st,z)); }
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
  //   house   the mess and the galley, behind the after-deck door; the stair up to the wheelhouse to port,
  //           the stair down to the crew's cabin to starboard
  //   bridge  the wheelhouse: the console and the wheel, the helm chair, the chart table
  //   below   the crew's cabin under the house: four berths athwartships down the port side, heads outboard
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
      for(const x of [-0.55,0.45]) pZ(fl,1,z0+0.004,x-0.01,x+0.01,R.y0,R.y1,'rm_lino',-1.2);                   // the lino's seams
      wallX(wl,1,-R.x,R.y0,R.y1,z0,R.zt,zd); wallX(wl,-1,R.x,R.y0,R.y1,z0,R.zt,zd); wallY(wl,-1,R.y1,-R.x,R.x,z0,R.zt,zd);
      wallY(wl,1,R.y0,-R.x,D.x0,z0,R.zt,zd); wallY(wl,1,R.y0,D.x1,R.x,z0,R.zt,zd); wallY(wl,1,R.y0,D.x0,D.x1,D.z1,R.zt,zd);
      for(const side of [-1,1]) for(const y of HPORT_Y){                                                      // the ports, from inside
        faceO(wl,ngon(8,HPORT_R+0.06,(a,b)=>[side*(R.x-0.006),y+a,HPORT_Z+b]),'steel',-0.2,0.01,[-side,0,0]);
        faceO(wl,ngon(8,HPORT_R-0.02,(a,b)=>[side*(R.x-0.012),y+a,HPORT_Z+b]),'glas',1.2,0.02,[-side,0,0]); }
      // the well and the stair down (its upper treads; the lower ones are the cabin's)
      const zw=DECK-1.05;
      pZ(ft,1,zw,Dn.x0,Dn.x1,Dn.y0,Dn.y1,'rm_cavity',-0.5);
      pY(ft,1,Dn.y0,Dn.x0,Dn.x1,zw,z0,'rm_cavity',0.4); pY(ft,-1,Dn.y1,Dn.x0,Dn.x1,zw,z0,'rm_cavity',0.2);
      pX(ft,1,Dn.x0,Dn.y0,Dn.y1,zw,z0,'rm_cavity',0.6); pX(ft,-1,Dn.x1,Dn.y0,Dn.y1,zw,z0,'rm_cavity',0.1);
      rmStair(ft,Dn.x0+0.04,Dn.x1-0.04,Dn.y0,Dn.z0,Dn.y1,Dn.z1,Dn.n,zw+0.2,Dn.z0,null);
      rbox(ft,Dn.x0-0.05,Dn.x0,Dn.y0,Dn.y1+0.30,z0,z0+0.95,'rm_join',0.5,0.1);                                 // the well's half-height screen
      rbox(ft,Dn.x0-0.05,Dn.x1,Dn.y1+0.25,Dn.y1+0.30,z0,z0+0.95,'rm_join',0.5,0.1);
      // the stair up to the wheelhouse
      rmStair(ft,U.x0,U.x1,U.y0,U.z0,U.y1,BR_F,U.n,U.z0,BR_F,U.x1-0.02);
      faceO(ft,[[U.x1,U.y0+0.18,z0],[U.x1,U.y1,z0],[U.x1,U.y1,BR_F-0.24]],'rm_join',-0.4,0,[1,0,0]);            // the stringer, panelled in under the flight
      pY(ft,1,U.y1,U.x0,U.x1,z0,R.zt,'rm_join',-0.2);
      // the oilskin lockers in the after corners, either side of the door: the gear hanging in the starboard one
      rbox(ft,-R.x,-1.05,R.y0,-8.85,z0,z0+1.90,'cream',0.3,-0.1);
      pX(ft,1,-1.046,R.y0+0.34,R.y0+0.36,z0+0.10,z0+1.85,'cream',-1.6);
      rbox(ft,1.05,R.x,R.y0,-8.85,z0,z0+1.90,'cream',0.3,-0.1);
      pX(ft,-1,1.046,R.y0+0.08,-8.93,z0+0.12,z0+1.85,'rm_cavity',0.6);
      for(const [y,m] of [[-9.30,'safe'],[-9.08,'buff']]) pX(ft,-1,1.04,y-0.09,y+0.09,z0+0.55,z0+1.70,m,-0.3);
      // the mess: a settee down the starboard side and across the forward wall, the table in its corner
      const zs=z0+0.45;
      rbox(ft,1.22,R.x,-5.78,R.y1,z0,zs,'rm_join',0,-0.1); pZ(ft,1,zs+0.006,1.25,R.x-0.03,-5.74,R.y1-0.34,'rm_blanket',0.3);
      rbox(ft,0.45,1.22,-5.02,R.y1,z0,zs,'rm_join',0,-0.1); pZ(ft,1,zs+0.006,0.49,1.22,-4.99,R.y1-0.03,'rm_blanket',0.3);
      pX(ft,-1,R.x-0.03,-5.74,R.y1-0.03,zs,zs+0.42,'rm_blanket',-0.3); pY(ft,-1,R.y1-0.03,0.49,R.x-0.03,zs,zs+0.42,'rm_blanket',-0.5);
      rbox(ft,0.62,1.17,-5.72,-5.10,z0+0.70,z0+0.76,'rm_join',0.9,0.2);
      rbox(ft,0.83,0.96,-5.47,-5.34,z0,z0+0.70,'steel',0,-0.2);
      for(const [x,y,m] of [[0.74,-5.58,'cream'],[1.05,-5.24,'safe']]) rbox(ft,x-0.05,x+0.05,y-0.05,y+0.05,z0+0.76,z0+0.86,m,0.6,0);   // mugs
      pZ(ft,1,z0+0.765,0.78,1.08,-5.52,-5.30,'rm_linen',0.3);                                                 // the tide tables, left open
      // the galley: the range on the forward wall, the counter to port of it, lockers over the counter
      rbox(ft,-0.45,0.40,-5.20,R.y1,z0,z0+0.90,'iron',0.6,0.4);
      pZ(ft,1,z0+0.905,-0.40,0.35,-5.15,R.y1-0.04,'rm_stone',-0.2);
      for(const x of [-0.22,0.16]) faceO(ft,ngon(8,0.11,(a,b)=>[x+a,-4.96+b,z0+0.91]),'iron',-0.3,0.01,[0,0,1]);
      rbox(ft,-0.30,-0.14,-5.04,-4.88,z0+0.91,z0+1.07,'steel',0.9,0.2);                                        // the kettle
      rbox(ft,0.24,0.36,-4.86,-4.74,z0+0.90,R.zt,'iron',0,0.3);                                                // the flue
      pY(ft,-1,-5.204,-0.36,0.30,z0+0.22,z0+0.62,'rm_cavity',0.8);                                             // the oven door
      rbox(ft,-R.x,-0.45,-5.20,R.y1,z0,z0+0.92,'rm_join',0,0); pZ(ft,1,z0+0.925,-R.x,-0.45,-5.20,R.y1,'rm_stone',0.5);
      pZ(ft,1,z0+0.93,-1.30,-0.80,-5.08,-4.78,'steel',-0.8); pZ(ft,1,z0+0.932,-1.25,-0.85,-5.04,-4.82,'iron',0.6);   // the sink
      rtube(ft,[-1.05,-4.74,z0+0.93],[-1.05,-4.84,z0+1.14],0.02,'steel',0.4);
      for(const x of [-1.05,-0.75]) pY(ft,-1,-5.204,x-0.008,x+0.008,z0+0.08,z0+0.88,'rm_join',-1.6);
      pY(ft,-1,R.y1-0.30,-R.x,-0.50,z0+1.75,R.zt,'rm_join',-0.2); pZ(ft,-1,z0+1.75,-R.x,-0.50,R.y1-0.30,R.y1,'rm_join',-1.2);   // the lockers over the counter
      // on the aft wall: the clock, the extinguisher by the door
      faceO(ft,ngon(8,0.14,(a,b)=>[-0.78+a,R.y0+0.012,3.75+b]),'steel',0.6,0.02,[0,1,0]); faceO(ft,ngon(8,0.10,(a,b)=>[-0.78+a,R.y0+0.016,3.75+b]),'rm_linen',0.6,0.03,[0,1,0]);
      rbox(ft,0.62,0.78,R.y0,R.y0+0.14,z0+0.95,z0+1.45,'safe',0.6,0.2);
      put(fl,'house','floor'); put(wl,'house','lining'); put(ft,'house','fit');
    })();

    // ---------------- bridge: the wheelhouse -------------------------------------------------------------
    (function(){ const R=BR, D=WDOOR, fl=[], wl=[], ft=[], wh=[], z0=R.z0, zt=R.zt, zs=R.sill, U=ST_UP, yf0=R.yF(z0);
      pZ(fl,1,z0,-R.x,U.x0,R.y0,yf0,'rm_lino',0.1); pZ(fl,1,z0,U.x0,U.x1,R.y0,U.y0,'rm_lino',0.1);
      pZ(fl,1,z0,U.x0,U.x1,U.y1,yf0,'rm_lino',0.1); pZ(fl,1,z0,U.x1,R.x,R.y0,yf0,'rm_lino',0.1);
      // the walls: joinery to the sill, painted over it; plumb sides, the front raking aft with the glass
      for(const side of [-1,1]) for(const [za,zb,m,b] of [[z0,zs,'rm_join',-0.2],[zs,zt,'cream',0]])
        faceO(wl,[[side*R.x,R.y0,za],[side*R.x,R.yF(za),za],[side*R.x,R.yF(zb),zb],[side*R.x,R.y0,zb]],m,b,0,[-side,0,0]);
      for(const [za,zb,m,b] of [[z0,zs,'rm_join',-0.3],[zs,zt,'cream',-0.1]])
        faceO(wl,[[-R.x,R.yF(za),za],[R.x,R.yF(za),za],[R.x,R.yF(zb),zb],[-R.x,R.yF(zb),zb]],m,b,0,[0,-1,-0.17]);
      for(const [za,zb,m,b] of [[z0,zs,'rm_join',-0.3],[zs,zt,'cream',-0.1]]){                              // the aft wall, either side of the door and over it
        pY(wl,1,R.y0,-R.x,D.x0,za,zb,m,b); pY(wl,1,R.y0,D.x1,R.x,za,zb,m,b); }
      pY(wl,1,R.y0,D.x0,D.x1,D.z1,zt,'cream',-0.1);
      // the glass, from inside
      for(const side of [-1,1]) for(const [ya,yb] of W_SIDE) pX(wl,-side,side*(R.x-0.012),ya,yb,GZ0,GZ1,'glas',1.4,0.02);
      for(const [xa,xb] of W_FRONT)
        faceO(wl,[[xa,R.yF(GZ0)-0.012,GZ0],[xb,R.yF(GZ0)-0.012,GZ0],[xb,R.yF(GZ1)-0.012,GZ1],[xa,R.yF(GZ1)-0.012,GZ1]],'glas',1.6,0.02,[0,-1,-0.17]);
      for(const [xa,xb] of W_AFT) pY(wl,1,R.y0+0.012,xa,xb,GZ0,GZ1,'glas',1.0,0.02);
      // the stair well from the mess: its top treads, a rail round it
      const zw=z0-1.25;
      pZ(ft,1,zw,U.x0,U.x1,U.y0,U.y1,'rm_cavity',-0.5);
      pY(ft,1,U.y0,U.x0,U.x1,zw,z0,'rm_cavity',0.4); pX(ft,1,U.x0,U.y0,U.y1,zw,z0,'rm_cavity',0.6); pX(ft,-1,U.x1,U.y0,U.y1,zw,z0,'rm_cavity',0.1);
      rmStair(ft,U.x0+0.03,U.x1-0.03,U.y0,U.z0,U.y1,z0,U.n,zw+0.15,z0,null);
      for(const [A,B] of [[[U.x1,U.y0,z0],[U.x1,U.y1-0.05,z0]],[[U.x0,U.y0,z0],[U.x1,U.y0,z0]]]){
        rtube(ft,[A[0],A[1],z0+0.95],[B[0],B[1],z0+0.95],0.025,'steel',0.3); rtube(ft,[A[0],A[1],z0+0.50],[B[0],B[1],z0+0.50],0.018,'steel',-0.1);
        rtube(ft,A,[A[0],A[1],z0+0.95],0.02,'steel',-0.1); rtube(ft,B,[B[0],B[1],z0+0.95],0.02,'steel',-0.1); }
      // the console across the front: three bays, the helmsman's the middle one with his knees under it. It stands
      // plumb; the raking glass comes aft over its top
      const zc=z0+0.95, yc=CONSOLE_Y, yb=R.yF(zc)-0.02, hx=WHEEL_HUB.x;
      rbox(ft,-R.x+0.04,hx-0.34,yc,yb,z0,zc,'rm_join',0.2,0); rbox(ft,hx+0.34,R.x-0.04,yc,yb,z0,zc,'rm_join',0.2,0);
      rbox(ft,hx-0.34,hx+0.34,yc+0.14,yb,z0,zc,'rm_join',0.2,0); pY(ft,-1,yc+0.138,hx-0.30,hx+0.30,z0+0.04,z0+0.62,'rm_cavity',0.6);
      pZ(ft,1,zc+0.004,-R.x+0.06,R.x-0.06,yc+0.16,yb-0.01,'iron',0.9);                                          // the instrument top
      rbox(ft,-1.20,-0.66,yc+0.18,yc+0.44,zc,zc+0.28,'iron',0.6,0.5); pY(ft,-1,yc+0.176,-1.15,-0.71,zc+0.04,zc+0.25,'glas',1.8,0.01);   // the radar
      faceO(ft,ngon(8,0.15,(a,b)=>[-0.93+a*0.9,yc+0.172,zc+0.145+b*0.6]),'glas',2.6,0.02,[0,-1,0]);
      rbox(ft,-0.56,-0.14,yc+0.20,yc+0.44,zc,zc+0.22,'iron',0.6,0.5); pY(ft,-1,yc+0.196,-0.52,-0.18,zc+0.04,zc+0.19,'glas',1.2,0.01);   // the sounder
      pY(ft,-1,yc+0.192,-0.48,-0.22,zc+0.07,zc+0.11,'safe',0.8,0.02);
      for(const f of cyl([hx,yc+0.36,zc],[hx,yc+0.36,zc+0.15],0.11,8,'steel',0.2,0,1,0.08,1.2)) ft.push(f);     // the compass
      rbox(ft,0.80,1.06,yc+0.20,yc+0.40,zc,zc+0.07,'steel',0.4,0); for(const x of [0.87,0.99]){ rtube(ft,[x,yc+0.30,zc+0.07],[x,yc+0.22,zc+0.26],0.015,'steel',0.3); rbox(ft,x-0.03,x+0.03,yc+0.18,yc+0.24,zc+0.24,zc+0.30,'safe',0.6,0); }   // the engine levers
      rmWheel(wh);
      // the helm chair on its pedestal
      const hs=HELM;
      rbox(ft,hs.x-0.06,hs.x+0.06,hs.y-0.02,hs.y+0.10,z0,hs.z-0.08,'steel',0,-0.2);
      rbox(ft,hs.x-0.24,hs.x+0.24,hs.y-0.22,hs.y+0.22,hs.z-0.09,hs.z,'rm_blanket',0.5,0);
      rbox(ft,hs.x-0.24,hs.x+0.24,hs.y-0.30,hs.y-0.22,hs.z-0.09,hs.z+0.55,'rm_blanket',0.5,0.2);
      for(const s of [-1,1]) rbox(ft,hs.x+s*0.27-0.03,hs.x+s*0.27+0.03,hs.y-0.26,hs.y+0.14,hs.z+0.16,hs.z+0.21,'iron',0.6,0);
      // the chart table on the starboard side: the chart, its lamp, drawers under
      const zc2=z0+0.95, C=CHART;
      rbox(ft,C.x0,R.x,C.y0,C.y1,z0,zc2,'rm_join',0.5,0);
      pZ(ft,1,zc2+0.004,C.x0+0.04,R.x-0.04,C.y0+0.05,C.y1-0.05,'rm_linen',0.4); pZ(ft,1,zc2+0.008,C.x0+0.09,C.x0+0.27,C.y0+0.12,C.y1-0.12,'glas',2.2);
      for(const z of [0.30,0.60]) pX(ft,-1,C.x0-0.004,C.y0+0.05,C.y1-0.05,z0+z-0.008,z0+z+0.008,'rm_join',-1.6);
      // the radio on the aft wall, the extinguisher under it
      rbox(ft,-0.30,0.30,R.y0,R.y0+0.16,z0+1.05,z0+1.35,'iron',0.6,0.4); pY(ft,1,R.y0+0.164,-0.25,0.25,z0+1.09,z0+1.31,'steel',-0.4,0.01);
      rbox(ft,0.36,0.48,R.y0,R.y0+0.12,z0+0.20,z0+0.68,'safe',0.6,0.2);
      put(fl,'bridge','floor'); put(wl,'bridge','lining'); put(ft,'bridge','fit'); put(wh,'bridge','fit'); _wheel=wh;
    })();

    // ---------------- below: the crew's cabin ---------------------------------------------------------------
    (function(){ const R=BW, fl=[], wl=[], ft=[], z0=R.z0, zt=R.zt, zd=z0+0.90, Dn=ST_DN;
      pZ(fl,1,z0,-R.x,R.x,R.y0,R.y1,'rm_lino',-0.2);
      for(const x of [0.9,1.7]) pZ(fl,1,z0+0.004,x-0.01,x+0.01,R.y0,R.y1,'rm_lino',-1.4);
      wallX(wl,1,-R.x,R.y0,R.y1,z0,zt,zd); wallX(wl,-1,R.x,R.y0,R.y1,z0,zt,zd); wallY(wl,1,R.y0,-R.x,R.x,z0,zt,zd); wallY(wl,-1,R.y1,-R.x,R.x,z0,zt,zd);
      // four berths athwartships down the port side, heads outboard: a joinery box, a mattress, a pillow, a blanket
      // turned down, a bunk board on its forward side
      for(const B of BERTHS){ const x0=B.x0, x1=B.x1, y0=B.y0, y1=B.y1, zb=B.top-0.10;
        rbox(ft,x0,x1,y0,y1,z0,zb,'rm_join',0,-0.1);
        rbox(ft,x0+0.03,x1-0.03,y0+0.03,y1-0.03,zb,B.top,'rm_linen',0.3,-0.4);
        rbox(ft,x0+0.10,x0+0.50,y0+0.12,y1-0.12,B.top,B.top+0.05,'rm_linen',1.0,-0.2);                          // the pillow
        pZ(ft,1,B.top+0.012,x0+0.78,x1-0.05,y0+0.04,y1-0.04,'rm_blanket',0.3); pZ(ft,1,B.top+0.016,x0+0.66,x0+0.80,y0+0.04,y1-0.04,'rm_linen',0.6);
        rbox(ft,x0,x1-0.30,y1,y1+0.03,zb,B.top+0.16,'rm_join',0.6,0.2);                                         // the bunk board
      }
      // lockers across the forward end of the berths' side
      rbox(ft,-R.x,0.0,BERTHS[3].y1+0.10,R.y1,z0,zt-0.02,'cream',0.3,-0.1);
      for(const x of [-1.65,-1.10,-0.55]) pY(ft,-1,BERTHS[3].y1+0.096,x-0.008,x+0.008,z0+0.10,zt-0.10,'cream',-1.6);
      // the stair from the mess: every tread, the handrail inboard
      rmStair(ft,Dn.x0+0.04,Dn.x1-0.04,Dn.y0,Dn.z0,Dn.y1,z0,Dn.n,z0,Dn.z0,Dn.x0+0.02);
      faceO(ft,[[Dn.x0,Dn.y0,DECK-0.04],[Dn.x0,Dn.y1-0.20,z0],[Dn.x0,Dn.y0,z0]],'rm_join',-0.4,0,[-1,0,0]);
      pY(ft,-1,Dn.y0,Dn.x0,R.x,z0,zt,'rm_join',-0.3);
      // on the starboard side: a shelf of books forward of the stair, sea boots under it
      pX(ft,-1,R.x-0.004,-5.90,-5.20,z0+1.00,z0+1.30,'rm_join',0.4,0.02); pX(ft,-1,R.x-0.008,-5.80,-5.40,z0+1.05,z0+1.26,'rm_linen',0.2,0.03);
      put(fl,'below','floor'); put(wl,'below','lining'); put(ft,'below','fit');
    })();
    return all;
  }
  // the berths: lower and only, athwartships down the port side; x0 is the head (outboard, to port)
  const BERTHS = ['berth_aft','berth_mid_aft','berth_mid_fwd','berth_fwd'].map((id,i)=>{ const y0=+(BL.y0+0.02+i*0.90).toFixed(2);
    return { id, x0:-BL.hx+0.03, x1:0.0, y0, y1:+(y0+0.80).toFixed(2), top:BL.sole+0.55 }; });
  function interiorFaces(){ if(!_room) _room=buildRooms(); return _room; }

  // ---- the new layout's reads ----------------------------------------------------------------------------
  function faceList(state){ const o=state && typeof state==='object' ? state : {}; return assemble(stateOf(o)).F; }
  const PART_TAGS = { trawl_doors:2, winch:3, net:4, hatch:5 };
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

  const RIG_FILE='sideDraggerIsoRig.js', EXPORT_SYMBOL='SideDraggerIso', HULL_ID='side_dragger', HULL_LABEL='Side Dragger · 25 m';
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
    doorSpec(DOOR, 'the deckhouse door, in its aft wall on the after deck: weather-tight steel, hinged on its port jamb, swinging aft. '+
      'Shut until opened; E opens it. A '+q3(DOOR.z0-DOOR.sillZ)+' m coaming under it. keepClear is the swept arc: the leaf’s collider. doorFaces(t) swings both of the ship’s leaves.'),
    doorSpec(WDOOR, 'the wheelhouse door, in its aft wall on the boat deck to starboard: hinged on its starboard jamb, swinging aft, clear of the funnel. '+
      'The boat deck is reached from the wheelhouse by this door; there is no ladder up the house.'),
  ];
  DOOR.clearAt = DOOR_SPECS[0].clearAt;

  function geometry(){
    const W_=WHEEL_GEO, r=W_.rake*DEG;
    return {
      schema:'hidden-harbours/hull-geometry@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:HULL_ID, units:'m', frame:NL_FRAME,
      ids:Object.assign({}, LV), roles:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice(), dwlZ:DWL,
      levels:[
        { id:'main_deck', deck:'deck', sole:DECK, ceil:null, kind:'open', soleZ:DECK, ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the working deck, from the stern to the break of the fo’c’sle at y '+q3(BRK)+', the side decks past the house and the after deck: sky. The foremast, the derrick, the gallows and the warps (level rigging) stand over it and never lift.' } },
        { id:'house', deck:'house', sole:HS.z0, ceil:q3(HS.zt), kind:'hard', soleZ:HS.z0, ceilingZ:q3(HS.zt),
          ceiling:{ kind:'hard', lid:'bridge, top', z:q3(HS.zt), of:'the boat deck (level bridge, role floor): '+q3(HS.zt-HS.z0)+' m clear over the mess. It lifts, with the wheelhouse on it and the wheelhouse top, under cut key house.' } },
        { id:'bridge', deck:'bridge', sole:BR.z0, ceil:q3(BR.zt), kind:'hard', soleZ:BR.z0, ceilingZ:q3(BR.zt),
          ceiling:{ kind:'hard', lid:'top', z:q3(BR.zt), of:'the wheelhouse top (level top): '+q3(BR.zt-BR.z0)+' m clear. It lifts under cut key bridge.' } },
        { id:'below', deck:'below', sole:BW.z0, ceil:q3(BW.zt), kind:'hard', soleZ:BW.z0, ceilingZ:q3(BW.zt),
          ceiling:{ kind:'hard', lid:'house, bridge, top', z:q3(BW.zt), of:'the deck plate under the deckhouse: '+q3(BW.zt-BW.z0)+' m clear over the crew’s cabin (low: she is 2.05 m from keel to deck). The house, the wheelhouse and its top lift under cut key below.' } },
        { id:'foredeck', deck:'focsle_head', sole:q3(FD_Z0), ceil:null, kind:'open', soleZ:q3(FD_Z0), ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the whaleback: it follows the sheer from '+q3(FD_Z0)+' at the break to '+q3(FD[FD.length-1].z)+' at the stem head. soleZ is its height at the break.' } },
        { id:'top', deck:'wheelhouse_top', sole:ROOFZ, ceil:null, kind:'open', soleZ:ROOFZ, ceilingZ:null,
          ceiling:{ kind:'open', lid:null, note:'the wheelhouse top: the radar, the searchlight, the horn. Not a standing surface (see _excluded wheelhouse_top)' } },
      ],
      cutaway:{
        ghost:{ deck:[], house:['bridge','top'], bridge:['top'], below:['house','bridge','top'] },
        gate:{ deck:[], house:['bridge','top'], bridge:['top'], below:['house','bridge','top'] },
        shows:{ deck:null, house:'house', bridge:'bridge', below:'below' },
        note:'gate lists are complete lift sets in ghost’s vocabulary (whole levels). Under house the wheelhouse, the boat deck it stands on, the funnel, the mizzen and the wheelhouse top lift, '+
          'and the mess is drawn in the deckhouse. Under bridge the wheelhouse top lifts. Under below the deckhouse lifts too and the crew’s cabin is drawn under it. '+
          'The hull, the working deck and its gear, the fo’c’sle head and the gear aloft (level rigging: the foremast and its derrick, the gallows, the warps, the trawl doors) never lift.',
      },
      door:DOOR_SPECS[0], doors:DOOR_SPECS.slice(),
      parts:[
        { id:'wheel', group:'room', lv:'bridge', ro:'fit', hub:nlV([WHEEL_HUB.x,WHEEL_HUB.y,WHEEL_HUB.z]),
          axis:nlV([0,-Math.cos(r),Math.sin(r)]), rake_deg:W_.rake, radius:W_.rad,
          note:'the wheel stands in the wheelhouse, which is a room: partFaces(\'wheel\') returns its faces from interiorFaces(). It is drawn amidships; there is no steer state (as on the stern trawlers).' },
        { id:'trawl_doors', group:'tag', tag:2, state:'doors', lv:'rigging', ro:'part', blocks:GAL_BLOCK.map(b=>nlV([b.x,b.y,b.z])), travel:q3(TD.zStow-TD.zDown),
          note:'doors 0..1: 0 the two trawl doors hang stowed at the gallows’ blocks, outboard of the starboard rail; 1 they are lowered '+q3(TD.zStow-TD.zDown)+' m down the ship’s side to the water on their warps. blocks are fore, aft.' },
        { id:'winch', group:'tag', tag:3, state:'winch', lv:'main_deck', ro:'fit', axis:[1,0,0], at:nlV([0,WINCH.y,WINCH.z]),
          drums:[-1,1].map(s=>nlV([s*WINCH.drumX,WINCH.y,WINCH.z])), note:'winch (degrees) turns the trawl winch’s two drums about their athwartships shaft' },
        { id:'net', group:'tag', tag:4, state:'net', lv:'main_deck', ro:'fit',
          note:'net 0..1: 0 the net is stowed in a heap along the starboard bulwark between the gallows; 1 the heap is gone and the net runs over the rail and down the ship’s side to the water' },
        { id:'hatch', group:'tag', tag:5, state:'hatch', lv:'main_deck', ro:'fit', hinge:nlV([0,HATCH.y+HATCH.hl+0.04,DECK+HATCH.h]), maxDeg:HATCH.maxDeg,
          hinges:HATCHES.map(Hh=>nlV([0,Hh.y+Hh.hl+0.04,DECK+Hh.h])),
          note:'hatch 0..1 lifts the two fish hatches’ lids together, each about its coaming’s forward edge (hinge is the hold hatch’s amidships, hinges both); the dark hold and the fish boxes in it are static, under the lids' },
        { id:'door', group:'doorFaces', lv:'house', ro:'wall', kind:'hinge', maxDeg:DOOR.maxDeg, opensTo:'aft', note:'doorFaces(t): both leaves, the deckhouse door (level house) and the wheelhouse door (level bridge): see door, doors' },
      ],
      states:{ doors:'0..1 (the trawl doors: stowed .. lowered)', winch:'deg (the trawl winch’s drums)', net:'0..1 (stowed along the bulwark .. shot over the rail)', hatch:'0..1 (the two fish hatches’ lids)',
        doorOpen:'0..1 (render option; the leaves are doorFaces(t))' },
    };
  }

  // ---- the walk (hull-walk@1) ---------------------------------------------------------------------------------
  function walk(){
    const R=NL_WALK.radius, G=geometry(), areas=[];
    const half=(y)=>dwY(y)-0.21-R;                         // the deck's edge for the walker's centre: clear of the bulwark stanchions' knees and the gallows' feet
    const sidePoly=(inner, y0, y1, n, outer)=>{ const P=[[q3(inner(y0)),q3(y0)]];
      for(let i=0;i<=n;i++){ const y=y0+(y1-y0)*i/n; P.push([q3(outer(y)),q3(y)]); }
      P.push([q3(inner(y1)),q3(y1)]); return P; };
    const mirror=(poly)=>poly.map(p=>[-p[0],p[1]]).reverse();
    // -- the after deck: round the stern, abaft the house --
    const yS=q3(CAPSTAN.y+CAPSTAN.r+R+0.02), yAd=q3(HYa-R-0.08);
    areas.push({ id:'aft_deck', cut:'deck', z:DECK, poly:nlSimplify(nlSide(half, yS, yAd, 10)), holes:[],
      note:'the after deck, from the capstan to the house’s aft wall: the deckhouse door opens onto it, and a side deck runs forward from each of its corners' });
    // -- the side decks past the house: BOTH sides --
    { const inner=()=>HXl+0.08+R, yA=-10.0, yB=HYf+0.60, P=nlSimplify(sidePoly(inner, yA, yB, 12, half));
      areas.push({ id:'house_alley_star', cut:'deck', z:DECK, poly:P, holes:[], note:'the starboard side deck, between the house and the bulwark: '+q3(dwY(HYa)-HXl)+' m wide at the house’s aft wall, '+q3(dwY(HYf)-HXl)+' m at its front' });
      areas.push({ id:'house_alley_port', cut:'deck', z:DECK, poly:mirror(P), holes:[], note:'the port side deck, the same' }); }
    // -- the working deck, from the house's front to the break --
    const yT0=q3(HYf+R+0.08), yT1=q3(BRK-R-0.03);
    const netA=NETS.y0-R-0.02, netB=NETS.y1+R+0.02, stairY=ST_FD.y0+0.02;
    const outerS=(y)=> (y>netA && y<netB) ? Math.min(half(y), dwY(y)-NETS.w-R-0.02) : half(y);   // inboard of the net's stow, to starboard
    const outerP=(y)=> y>stairY ? Math.min(half(y), ST_FD.xa-R) : half(y);                       // inboard of the fo'c'sle stair, to port
    { const ys=[]; for(let i=0;i<=30;i++) ys.push(yT0+(yT1-yT0)*i/30);
      for(const yy of [netA, netB, stairY]) ys.push(yy-0.001, yy+0.001);
      ys.sort((a,b)=>a-b);
      const Rr=[], Ll=[]; for(const y of ys){ Rr.push([q3(outerS(y)),q3(y)]); Ll.unshift([q3(-outerP(y)),q3(y)]); }
      const Wn=WINCH, post=(Wp)=>nlGrow(Wp.x-0.10,Wp.y-0.10,Wp.x+0.10,Wp.y+0.10,R);
      areas.push({ id:'trawl_deck', cut:'deck', z:DECK, poly:nlSimplify(Rr.concat(Ll)),
        holes:[ nlGrow(-(Wn.drumX+Wn.drumHx+0.40),Wn.y-Wn.hy,Wn.drumX+Wn.drumHx+0.40,Wn.y+Wn.hy,R),
                nlGrow(POUND.x0,POUND.y0,POUND.x1,POUND.y1,R),
                nlGrow(-HATCH.hw-0.06,HATCH.y-HATCH.hl-0.06,HATCH.hw+0.06,HATCH_F.y+HATCH_F.hl+0.06,R),
                post(WARP_LEAD[0]), post(WARP_LEAD[1]) ],
        note:'the working deck from the house to the break of the fo’c’sle: the winch before the house, the pounds to port, the sorting ground, the two fish hatches with the foremast between them (one hole), '+
          'the warps’ deck bollards. To starboard its edge steps inboard of the net’s stow; to port, inboard of the fo’c’sle stair. The holes are those, grown by the walker’s radius.' });
    }
    // -- the stair up the port side to the fo'c'sle head --
    { const A=ST_FD, ramp={ axis:'y', a0:A.y0, a1:q3(BRK), z0:DECK, z1:q3(FD_Z0) };
      areas.push({ id:'fore_stair_port', cut:'deck', kind:'stair', ramp, zMid:q3((DECK+FD_Z0)/2), cutHi:'deck', cutLo:'deck',
        poly:nlRect(-(A.xb-R), A.y0-0.05, -(A.xa+R), BRK+R+0.03), holes:[],
        note:'six treads up the port side of the deck to the fo’c’sle head at the break: the ramp carries the walker. The starboard side has none: the fore gallows stands there.' }); }
    // -- the fo'c'sle head: the whaleback follows the sheer, so it is ramps fore and aft --
    let yFdEnd=BRK+R; for(let y=BRK+R; y<L/2+RAKE; y+=0.01){ if(fdAt(y).half-0.34<0.30) break; yFdEnd=y; }
    const fd=nlSurfaceAreas('focsle_deck', 'deck', (x,y)=>fdAt(y).z, (y)=>fdAt(y).half-0.34, BRK+R, yFdEnd, 0.02);
    { const hs=[ nlGrow(-0.76,WINDLASS_Y-0.36,0.76,WINDLASS_Y+0.36,R), nlGrow(-0.55,WINDLASS_Y+0.36,0.55,11.20,R),
        nlGrow(BOWBOLL.x-0.13,BOWBOLL.y-0.28,BOWBOLL.x+0.13,BOWBOLL.y+0.28,R), mirror(nlGrow(BOWBOLL.x-0.13,BOWBOLL.y-0.28,BOWBOLL.x+0.13,BOWBOLL.y+0.28,R)) ];
      for(const a of fd.areas){ let ya=Infinity, yb=-Infinity; for(const p of a.poly){ ya=Math.min(ya,p[1]); yb=Math.max(yb,p[1]); }
        a.holes=hs.filter(h=>Math.min.apply(null,h.map(p=>p[1]))<yb && Math.max.apply(null,h.map(p=>p[1]))>ya).map(h=>h.map(p=>p.slice()));
        a.note='the fo’c’sle head inside its rails; holes: the windlass and its cables, the bow bollards'; areas.push(a); } }
    // -- the deckhouse: the door, the mess, the two stairs --
    areas.push({ id:'house_doorway', cut:'deck', z:DECK, poly:nlRect(DOOR.x0+R, HYa-R-0.12, DOOR.x1-R, HS.y0+R+0.02), holes:[], door:'house_door',
      note:'through the deckhouse door: a wall while the leaf is shut, passable from doorOpen '+DOOR_SPECS[0].clearAt+' (interact house_door). Over a '+q3(DOOR.z0-DOOR.sillZ)+' m coaming.' });
    const hx=q3(HS.x-R), hy0=q3(HS.y0+R), U=ST_UP, Dn=ST_DN;
    { const xL=q3(-1.05+R), xR=q3(1.05-R), yL=q3(-8.85+R), yU=q3(U.y0-R), xa=q3(U.x1+R), xb=q3(Dn.x0-0.05-R), xt=q3(0.62-R), yt=q3(-5.72-R), yg=q3(-5.20-R), yh=q3(U.y1+R);
      areas.push({ id:'house_sole', cut:'house', z:HS.z0, headroom:q3(HS.zt-HS.z0),
        poly:[[xL,hy0],[xR,hy0],[xR,yL],[hx,yL],[hx,yU],[xb,yU],[xb,yt],[xt,yt],[xt,yg],[-hx,yg],[-hx,yh],[xa,yh],[xa,yU],[-hx,yU],[-hx,yL],[xL,yL]], holes:[],
        note:'the mess: from the door forward between the oilskin lockers and the two stairs to the range, the galley’s counter to port, the mess table and its settee to starboard. The stairs’ feet are abaft them, each side.' }); }
    areas.push({ id:'stair_bridge', cut:'house', kind:'stair', ramp:{ axis:'y', a0:U.y0, a1:U.y1, z0:HS.z0, z1:BR.z0 }, zMid:q3((HS.z0+BR.z0)/2), cutHi:'bridge', cutLo:'house',
      poly:nlRect(U.x0+R, U.y0-0.30, U.x1-R, U.y1+0.05), holes:[], note:'thirteen treads up to the wheelhouse, to port, climbing forward: the ramp carries the walker; the cut changes at zMid' });
    areas.push({ id:'stair_below', cut:'house', kind:'stair', ramp:{ axis:'y', a0:Dn.y0, a1:Dn.y1, z0:HS.z0, z1:BW.z0 }, zMid:q3((HS.z0+BW.z0)/2), cutHi:'house', cutLo:'below',
      poly:nlRect(Dn.x0+R, Dn.y0-0.30, Dn.x1-R, Dn.y1+0.30), holes:[], note:'nine treads down to the crew’s cabin, to starboard, descending forward: steep, a ship’s stair; the cut changes at zMid' });
    // -- the wheelhouse and the boat deck behind it --
    const bx=q3(BR.x-R), by0=q3(BR.y0+R), ch=nlGrow(HELM.x-0.25,HELM.y-0.30,HELM.x+0.25,HELM.y+0.20,R);
    { const xw=q3(U.x1+R), yc=q3(CONSOLE_Y-R), xc=q3(CHART.x0-R);
      areas.push({ id:'wheelhouse', cut:'bridge', z:BR.z0, headroom:q3(BR.zt-BR.z0),
        poly:[[xw,by0],[bx,by0],[bx,q3(CHART.y0-R)],[xc,q3(CHART.y0-R)],[xc,ch[0][1]],[ch[0][0],ch[0][1]],[ch[0][0],yc],[-bx,yc],[-bx,q3(U.y1)],[xw,q3(U.y1)]], holes:[],
        note:'the wheelhouse: abaft the helm chair and the chart table, then forward between the chair and the stair well’s rail to the console; the stair’s head is at its port forward end' }); }
    areas.push({ id:'wheelhouse_doorway', cut:'deck', z:WZ0, poly:nlRect(WDOOR.x0+R, WYa-R-0.12, WDOOR.x1-R, BR.y0+R+0.02), holes:[], door:'wheelhouse_door',
      note:'through the wheelhouse door onto the boat deck: a wall while the leaf is shut, passable from doorOpen '+DOOR_SPECS[1].clearAt+' (interact wheelhouse_door)' });
    { const x=q3(HXl+0.05-R), ya=q3(HYa-0.03+R), yb=q3(WYa-R-0.03), fx=q3(FUNNEL.r+0.10+R);
      areas.push({ id:'boat_deck', cut:'deck', z:WZ0, poly:nlRect(fx, ya, x, yb), holes:[],
        note:'the boat deck abaft the wheelhouse, to starboard of the funnel, inside its rails. Reached by the wheelhouse door only.' }); }
    // -- the crew's cabin --
    { const xw=q3(BERTHS[0].x1+R), xe=q3(BW.x-R), ya=q3(BW.y0+R), yb=q3(BW.y1-R);
      areas.push({ id:'below_sole', cut:'below', z:BW.z0, headroom:q3(BW.zt-BW.z0),
        poly:[[xw,ya],[xe,ya],[xe,q3(Dn.y0-R)],[q3(Dn.x0-R),q3(Dn.y0-R)],[q3(Dn.x0-R),Dn.y1],[xe,Dn.y1],[xe,yb],[xw,yb]], holes:[],
        note:'the crew’s cabin: the aisle down the berths’ feet, round the stair; the stair’s foot is at its forward end to starboard. '+q3(BW.zt-BW.z0)+' m under the deck plate: the taller builds wait on duck.' }); }
    const aAt=(id)=>areas.find(a=>a.id===id);
    const spawnAt=[SORT.spill.x, SORT.spill.y];
    const fdPiece=(x,y)=>fd.areas.find(a=>nlPip([x,y],a.poly) && !a.holes.some(h=>nlPip([x,y],h)));
    const wl=[0,q3(WINDLASS_Y-0.36-R-0.20)], wlA=fdPiece(wl[0],wl[1]);
    const hatchAft=q3(HATCH.y-HATCH.hl-0.06-R-0.14), hatchFwd=q3(HATCH_F.y+HATCH_F.hl+0.06+R+0.14);
    const galF={ x:q3(half(5.35)-0.10), y:5.35 }, galA={ x:q3(half(-0.95)-0.10), y:-0.95 };

    const berths=BERTHS.map(B=>({ id:B.id, interact:B.id, kind:'midberth', topZ:q3(B.top), head:'port', deckheadZ:q3(BW.zt),
      poly:nlRect(B.x0+0.03, B.y0+0.03, B.x1-0.03, B.y1-0.03) }));
    const interact=[
      { id:'helm', action:'helm', label:'Helm', prompt:'Take the wheel', at:nlPt(HELM), reach:{ x:-0.30, y:q3(HELM.y), z:BR.z0 }, area:'wheelhouse',
        note:'seated in the helm chair: anchors.helm is the seat, anchors.wheel the hub, anchors.pilot the feet' },
      { id:'house_door', action:'open_close', label:'Deckhouse door', prompt:'Open · shut', state:'doorOpen', clearAt:DOOR_SPECS[0].clearAt,
        at:nlPt({ x:(DOOR.x0+DOOR.x1)/2, y:HYa, z:(DOOR.z0+DOOR.z1)/2 }), reach:{ x:q3((DOOR.x0+DOOR.x1)/2), y:q3(HYa-R-0.20), z:DECK }, area:'aft_deck' },
      { id:'wheelhouse_door', action:'open_close', label:'Wheelhouse door', prompt:'Open · shut', state:'doorOpen', clearAt:DOOR_SPECS[1].clearAt,
        at:nlPt({ x:(WDOOR.x0+WDOOR.x1)/2, y:WYa, z:(WDOOR.z0+WDOOR.z1)/2 }), reach:{ x:q3((WDOOR.x0+WDOOR.x1)/2), y:q3(BR.y0+R+0.20), z:BR.z0 }, area:'wheelhouse' },
    ].concat(BERTHS.map(B=>({ id:B.id, action:'sleep', label:'Berth', prompt:'Turn in', at:{ x:q3((B.x0+B.x1)/2), y:q3((B.y0+B.y1)/2), z:q3(B.top) },
        reach:{ x:q3(B.x1+R+0.09), y:q3((B.y0+B.y1)/2), z:BW.z0 }, area:'below_sole' })), [
      { id:'winch', action:'haul', label:'Trawl winch', prompt:'Haul · veer', state:'doors', also:['winch'], at:nlPt({ x:0, y:WINCH.y, z:WINCH.z }), reach:{ x:0, y:q3(WINCH.y+WINCH.hy+R+0.14), z:DECK }, area:'trawl_deck',
        note:'the brake levers are on the winch’s forward side: the winchman stands on the centreline before it, facing aft, the whole deck at his back' },
      { id:'net', action:'shoot_haul', label:'Trawl net', prompt:'Shoot · haul the net', state:'net', at:nlPt({ x:dwY(3.0)-NETS.w*0.55, y:3.0, z:DECK+NETS.h }), reach:{ x:q3(outerS(3.0)-0.08), y:3.0, z:DECK }, area:'trawl_deck',
        note:'from the deck inboard of the net’s stow, between the hatches and the working-side bulwark' },
      { id:'gallows_fore', action:'trawl_door', label:'Fore trawl door', prompt:'Lower · stow the door', state:'doors', at:nlPt(GAL_BLOCK[0]), reach:{ x:galF.x, y:galF.y, z:DECK }, area:'trawl_deck',
        note:'the door hangs outboard of the rail from the fore gallows; it is worked from the deck just abaft the gallows’ after leg' },
      { id:'gallows_aft', action:'trawl_door', label:'Aft trawl door', prompt:'Lower · stow the door', state:'doors', at:nlPt(GAL_BLOCK[1]), reach:{ x:galA.x, y:galA.y, z:DECK }, area:'trawl_deck',
        note:'the door hangs outboard of the rail from the aft gallows; it is worked from the deck just forward of the gallows’ forward leg' },
      { id:'hold_hatch', action:'open_close', label:'Fish hold', prompt:'Open · shut the hatches', state:'hatch', at:nlPt({ x:0, y:HATCH.y, z:DECK+HATCH.h }), reach:{ x:0, y:hatchAft, z:DECK }, area:'trawl_deck',
        note:'the hold’s hatch amidships, worked from its after side; the hold itself is not walked (see _excluded fish_hold). One state lifts both lids.' },
      { id:'hold_hatch_fwd', action:'open_close', label:'Fish hold, forward hatch', prompt:'Open · shut the hatches', state:'hatch', at:nlPt({ x:0, y:HATCH_F.y, z:DECK+HATCH_F.h }), reach:{ x:0, y:hatchFwd, z:DECK }, area:'trawl_deck',
        note:'the forward hatch: the same state' },
      { id:'galley', action:'cook', label:'Galley range', prompt:'Put the kettle on', at:{ x:-0.03, y:-4.96, z:q3(HS.z0+0.90) }, reach:{ x:-0.03, y:-5.46, z:HS.z0 }, area:'house_sole' },
      { id:'mess_table', action:'use', label:'Mess table', prompt:'Sit in', at:{ x:0.90, y:-5.41, z:q3(HS.z0+0.76) }, reach:{ x:0.30, y:-5.50, z:HS.z0 }, area:'house_sole', waitsOn:['sit'] },
      { id:'chart_table', action:'use', label:'Chart table', prompt:'Read the chart', at:{ x:1.22, y:q3((CHART.y0+CHART.y1)/2), z:q3(BR.z0+0.95) }, reach:{ x:q3(CHART.x0-R-0.05), y:q3((CHART.y0+CHART.y1)/2), z:BR.z0 }, area:'wheelhouse' },
      { id:'windlass', action:'anchor', label:'Windlass', prompt:'Let go · weigh', at:nlPt({ x:0, y:WINDLASS_Y, z:fdAt(WINDLASS_Y).z+0.40 }), reach:{ x:wl[0], y:wl[1], z:wlA ? q3(nlAreaZ(wlA,wl[0],wl[1])) : q3(fdAt(wl[1]).z) }, area:wlA ? wlA.id : null },
    ]);
    const qx=dwY(QUARTER.y)-0.34;
    const cleats=[
      { id:'bow_1', type:'samson_post', pos:nlV([SAMSON.x,SAMSON.y,SAMSON.z+0.06]) },
      { id:'bow_port', type:'bollard', pos:nlV([-BOWBOLL.x,BOWBOLL.y,fdAt(BOWBOLL.y).z+0.36]) }, { id:'bow_star', type:'bollard', pos:nlV([BOWBOLL.x,BOWBOLL.y,fdAt(BOWBOLL.y).z+0.36]) },
      { id:'quarter_port', type:'bollard', pos:nlV([-qx,QUARTER.y,DECK+0.38]) }, { id:'quarter_star', type:'bollard', pos:nlV([qx,QUARTER.y,DECK+0.38]) },
    ];
    const rl=railAt(COD_Y);
    const _excluded=[
      { id:'wheelhouse_top', of:'area', z:ROOFZ, reason:'the wheelhouse top: the radar, the searchlight, the horn and the aerial; no way up, not a standing surface.' },
      { id:'stern_quarters', of:'area', reason:'the deck round the stern abaft y '+yS+': the capstan and the quarter bollards fill it.' },
      { id:'boat_deck_port', of:'area', z:WZ0, reason:'the boat deck to port of the funnel and abaft it: the liferaft’s cradle and the mizzen pole fill it; '+q3(HYa-0.03-(FUNNEL.y1-FUNNEL.r))+' m between the funnel and the after rail.' },
      { id:'boat_deck_wings', of:'area', z:WZ0, reason:'the boat deck beside the wheelhouse: '+q3(HXl+0.10-HXw)+' m, under the sidelights’ screens.' },
      { id:'pounds', of:'area', z:DECK, reason:'the two fish pounds on the port side of the deck (x '+POUND.x0+' .. '+POUND.x1+', y '+POUND.y0+' .. '+POUND.y1+'): pound boards '+POUND.h+' m high, the catch in them; the crew sort from the clear deck beside them (anchors.sorting).' },
      { id:'net_stow', of:'area', z:DECK, reason:'the starboard side of the deck under the net, y '+NETS.y0+' .. '+NETS.y1+': '+NETS.w+' m in from the bulwark. Closed whether the net is aboard or shot (the walk does not change with state net).' },
      { id:'focsle_stair_pocket', of:'area', z:DECK, reason:'the deck outboard of the fo’c’sle stair, to port forward of y '+ST_FD.y0+': under the stair’s treads and behind its stringer.' },
      { id:'fish_hold', of:'room', reason:'the fish hold under the working deck (the hatches at y '+HATCH.y+' and y '+HATCH_F.y+'): drawn as a dark well with boxes in it under each lid; no room, no way down but its ladder.' },
      { id:'engine_room', of:'room', reason:'the engine room, under the deck abaft the crew’s cabin and under the after deck: not drawn.' },
      { id:'focsle_store', of:'room', reason:'the fo’c’sle under the whaleback, behind the bulkhead at the break: not drawn.' },
      { id:'seat_helm', of:'seat', at:nlPt(HELM), reason:'the helm chair: taken by the helm interact (drive).' },
      { id:'mess_settee', of:'seat', at:{ x:1.40, y:-5.40, z:q3(HS.z0+0.45) }, waitsOn:['sit'], reason:'the mess’s settee, round the table: sitting at it waits on a sit clip.' },
      { id:'washboards', of:'washboards', reason:'none: she has steel bulwarks to the rail all round the deck and a side deck either side of the house wide enough to walk (areas house_alley_port / _star); nobody walks her rail.' },
      { id:'working_gear', of:'cleat', reason:'the gallows, the winch’s warping heads, the warps’ deck bollards and the capstan are working gear, not tie-offs: mooring lines go to the samson post, the bow bollards and the quarter bollards (cleats[]).' },
    ];
    const spawn=aAt('trawl_deck');
    return {
      schema:'hidden-harbours/hull-walk@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:HULL_ID, label:HULL_LABEL,
      kind:'ship', units:'metres', frame:NL_FRAME, loa_m:L, beam_m:7.0, DWL, cockpitZ:DECK,
      cell:{ W, H, pivot:{ x:cx, y:cy }, pxPerM:PX, defaultElev:DEFAULT_ELEV, order:['N','NE','E','SE','S','SW','W','NW'] },
      tags:{ lv:Object.assign({}, LV), ro:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice() },
      walk:{ radius:R, stepTol:NL_WALK.stepTol, rule:NL_RULE, spawn:{ x:spawnAt[0], y:spawnAt[1], area:spawn?spawn.id:null, note:'on the sorting ground, before the winch' }, areas },
      house:Object.assign({ door:DOOR_SPECS[0], doors:DOOR_SPECS.slice(),
        rooms:[ { level:'house', name:'the mess and the galley', soleZ:HS.z0, ceilingZ:q3(HS.zt) }, { level:'bridge', name:'the wheelhouse', soleZ:BR.z0, ceilingZ:q3(BR.zt) },
                { level:'below', name:'the crew’s cabin', soleZ:BW.z0, ceilingZ:q3(BW.zt) } ], fitCheck:'qa.fitCheck' }, nlHouseDecks(G.levels)),
      anchors:{ helm:nlPt(HELM), wheel:nlPt(WHEEL_HUB), pilot:nlPt(PILOT), berths,
        gallows:GAL_BLOCK.map((b,i)=>({ id:i?'gallows_aft':'gallows_fore', side:'starboard', block:nlPt(b), y0:q3(b.y-GAL.hy), y1:q3(b.y+GAL.hy), headZ:GAL.zt })),
        winch:[-1,1].map(s=>({ side:s<0?'port':'starboard', x:s*WINCH.drumX, y:WINCH.y, z:q3(WINCH.z) })),
        holdHatch:{ x:0, y:HATCH.y, z:q3(DECK+HATCH.h), halfW:HATCH.hw, halfL:HATCH.hl, hinge:nlV([0,HATCH.y+HATCH.hl+0.04,DECK+HATCH.h]) },
        holdHatches:HATCHES.map(Hh=>({ id:Hh.id, x:0, y:Hh.y, z:q3(DECK+Hh.h), halfW:Hh.hw, halfL:Hh.hl, hinge:nlV([0,Hh.y+Hh.hl+0.04,DECK+Hh.h]) })),
        tubs:TUBS.map(nlPt), nav:{ port:nlPt(NAV.port), star:nlPt(NAV.star), stern:nlPt(NAV.stern), mast:nlPt(NAV.mast) },
        windlass:nlPt({ x:0, y:WINDLASS_Y, z:fdAt(WINDLASS_Y).z+0.40 }),
        foremast:{ x:0, y:MY, z0:DECK, z1:MAST_TOP }, derrick:{ heel:nlPt(DERRICK.heel), head:nlPt(DERRICK.head) },
        netStow:{ side:'starboard', y0:NETS.y0, y1:NETS.y1, z:DECK, width:NETS.w },
        // ---- the trawl's working points, named for the tow, the haul and the hands that sort (gameplay reads these by name).
        //      A SIDE trawler: both warps and both doors are on the working side (starboard), fore and aft; she has no ramp.
        warps:GAL_BLOCK.map((b,i)=>({ id:i?'warp_aft':'warp_fore', side:'starboard',
          winch:{ x:(i?1:-1)*WINCH.drumX, y:WINCH.y, z:q3(WINCH.z) }, lead:{ x:WARP_LEAD[i].x, y:WARP_LEAD[i].y, z:q3(DECK+0.54) }, door:nlPt(b), gallows:i?'gallows_aft':'gallows_fore' })),
        codEnd:{ rail:{ x:q3(rl.x), y:COD_Y, z:q3(rl.z) }, lift:nlPt(DERRICK.head), spill:{ x:SORT.spill.x, y:SORT.spill.y, z:DECK }, side:'starboard',
          note:'no ramp: the cod end comes over the working-side rail at rail, lifted on the gilson from the derrick’s head at lift, and is emptied on the deck at spill' },
        sorting:{ x0:SORT.x0, x1:SORT.x1, y0:SORT.y0, y1:SORT.y1, z:DECK, totes:[nlPt(TUBS[2]), nlPt(TUBS[3])] },
        crew:[ { id:'winchman', at:'winch' }, { id:'gallows_hand_fore', at:'gallows_fore' }, { id:'gallows_hand_aft', at:'gallows_aft' },
               { id:'sorter_port', x:SORT.sorterX[0], y:SORT.sorterY, z:DECK }, { id:'sorter_stbd', x:SORT.sorterX[1], y:SORT.sorterY, z:DECK },
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
      house:{ soleZ:DECK, ceilZ:HZ1l, hx:HXl, hxAt:()=>HXl, y0:HYa, y1:HYf, portholes:{ ys:HPORT_Y.slice(), z0:HPORT_Z-HPORT_R, z1:HPORT_Z+HPORT_R }, aftPorts:{ xs:[], z0:HPORT_Z-HPORT_R, z1:HPORT_Z+HPORT_R } },
      bridge:{ soleZ:WZ0, ceilZ:WZ1, hx:HXw, hxAt:xwz, y0:WYa, front:{ yBot:FYb, yTop:FYt },
               frontGlass:{ z0:GZ0, z1:GZ1, panes:W_FRONT.map(p=>p.slice()) },
               sideGlass:{ z0:GZ0, z1:GZ1, runs:W_SIDE.map(p=>p.slice()) },
               aftGlass:{ z0:GZ0, z1:GZ1, panes:W_AFT.map(p=>p.slice()) } },
      below:{ soleZ:BL.sole, ceilZ:BL.ceil, y0:BL.y0, y1:BL.y1 },
    },
  };
  function faces(){ return F; }                           // the static tagged mesh, as before; the leaves are doorFaces(t)

  const HULL_OBJ={ id:HULL_ID, label:HULL_LABEL, kind:'ship', faceList, partFaces, interiorFaces, doorFaces, geometry, matsFor, walk, fitCheck, render, renderCut,
    GAIN, BIAS, LN, BAYER, PX, W, H, pivot:{ x:cx, y:cy }, defaultElev:DEFAULT_ELEV, KEY, cell:{ W, H, pivot:{ x:cx, y:cy }, PX, defaultElev:DEFAULT_ELEV },
    schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, DWL, cockpitZ:DECK, loa:L,
    // the lamp join reads the hull's own scope: where her nav lamps are drawn, the house they light, and her stem station
    DIRS:8, navMounts, HOUSE, station };
  const HULLS={ side_dragger:HULL_OBJ };
  function byId(id){ return HULLS[id]||null; }

  root.SideDraggerIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV, KEYLINE_DEFAULT,
    order:['N','NE','E','SE','S','SW','W','NW'],
    HULL, BOOT, CREAM, DECKF, WOOD, GLAS, BUFF, NET, STEEL, IRON, SAFE, KEY,
    render, renderCut, renderTrawlDoors, renderWinch, renderNet, renderHatch,
    allFaces:(o)=>{ const s=stateOf(o||{}); return assemble(s).F.concat(doorFaces(s.door)); },
    ROCK, rock:rockMotion, helmSeat, HELM, gallowsMounts, GALLOWS, haulerMount, HAULER,
    tubMounts, TUBS, navMounts, doorMount, DOOR, WDOOR, HOUSE, geometry, faces, doorFaces, LEVEL_IDS:LV,
    WHEEL_GEO, WHEEL_HUB, PILOT,
    loft:{ station, skin, dfrac, bowRake, halfAtZ, sheerZ, L, TH, DECK, SOLE_U, NSEG,
           house:HOUSE, shade:{ GAIN, BIAS, LN, BAYER, KEY, EDGE:0.30 }, cell:{ W, H, cx, cy, S } },
    SCHEMES, schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, SLOTS,
    // the hull layout (ADR 0048 §8 A): one hull, through byId('side_dragger'); the same names for the default hull
    // (id is the default hull's: a reader that takes the global with no scope finds her walk sidecar by it)
    byId, HULLS, hullIds:Object.keys(HULLS), defaultHull:HULL_ID, id:HULL_ID, label:HULL_LABEL, LV, RO, GHOST_ROLES, kind:'ship',
    faceList, partFaces, interiorFaces, matsFor, walk, fitCheck, DWL, cockpitZ:DECK, loa:L,
    GAIN, BIAS, LN, BAYER, cell:{ W, H, pivot:{ x:cx, y:cy }, PX, defaultElev:DEFAULT_ELEV } };
})(typeof window !== 'undefined' ? window : globalThis);

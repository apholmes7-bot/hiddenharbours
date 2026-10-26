/* Hidden Harbours — ISO Sport Fishers, PASS 4: the re-lofted hulls (ADR-0006 bake recipe).
   Two hulls off one core, same ids, same cells, same pivots, same global as every pass before:
     convertible  16.2 m "53"  820 x 770,  pivot (410,475)   flybridge convertible: hardtop, tuna tower optional
     skybridge    27.4 m "90" 1200 x 1170, pivot (600,730)   salon, enclosed skylounge, open top station

   What pass 4 changes. The hulls are lofted again, low and long. Pass 3 stood about 1.7x too
   tall, with a near-vertical stem and windshields metres above the foredeck. Here the sheer runs low
   (53: 1.95 -> 2.83 m, 90: 2.11 -> 4.00 m over the keel), one straight stem is raked 44 deg / 47 deg, and
   each windshield starts just above the deck it rises from. The shell is 46 cosine-spaced sections with
   the boot, boot-top and cove stripe cut by waterline height; the house tiers are ring lofts on the sheer.
   The 53's house stands in behind 0.45 m side decks; the 90's house sides stand on the sheer.

   What pass 4 keeps. File name, global, hull ids and both hulls in one file. makeRig's return is still
   `const RIG = {` with `id:spec.id,` first on the next line, and faceList / F / RIGF are in scope there.
   paintTable('gelcoat').MATS is still the twelve-material table in the same order (hull, boot, cream,
   stripe, teak, deck, grip, glas, steel, iron, blk, dark). The four colourways, defaultElev 40, the posed
   salon slider (doorFaces) and the pass-3 cutaway hooks (cullLevels, cutaway:true -> BoatCutaway) are
   unchanged. Closed, stowed and uncut, the rasteriser draws the pass-3 bytes.

   hull-layout-1 (the lane's pass B, under ADR 0048). Every face carries lv and ro as NUMBERS and it as a
   boolean:
     lv  hull 0 · main_deck 1 · house 2 · bridge 3 · below 4 · rigging 5 · foredeck 6 · top 7
         (the 53's flybridge is bridge; the 90's skylounge is bridge and her open top station is top)
     ro  shell 0 · wall 1 · roof 2 · lining 3 · part 4 · floor 5 · fit 6 · tall 7 · trim 8 · rig 9
         ghost roles: shell, wall, roof, lining, part, tall, trim
   geometry() (§4.7) publishes rig, exportSymbol, hull, ids, roles, ghostRoles, dwlZ, the levels (id, deck,
   sole, ceil, kind, soleZ, ceilingZ, ceiling), cutaway (ghost, gate, shows, note), the door (the salon
   slider), doors (the 90 adds her skylounge leaf), parts and states. The cut keys: deck (nothing), house,
   bridge (the 90), fly (the 53), below. Both lists are in one vocabulary (a whole level, or level:role); the
   gate is ADR 0048 §6.4's lift-set form: the ghost's levels plus the shown level's walls and roof.
   renderCut(dir, key, opts) draws the gate (§4.8): what the key lifts is dropped, and the shown level's room
   is drawn over the rest, front faces only. render(dir,{cutaway:{mode:'ghost'|'gate', key, player:{x,y,z}}})
   is the lane's ghost: the rooms in true depth, and with a player the near ghost-role faces open round her;
   view(dir) is the same cut for a moving player (base + region).
   On each hull (§4.1): faceList(state, tower) (or faceList({riggers, tower})), partFaces(id, state),
   interiorFaces(), doorFaces(t, id) (the salon leaf; the 90's 'skylounge' by name), geometry(),
   matsFor(scheme) ({scheme, MATS, ramps, M, roomMats}, §5), paintTable(id) and cutTable(id) (the
   rasteriser's tables), schemeIds, defaultScheme, fitCheck(tol), leakPx(), WALK (the walk's areas and
   spawn), walk() and walkSidecar(), GAIN, BIAS, LN, BAYER, PX, W, H, pivot, defaultElev and cell.

   The ways down from the upper helm (owner's rulings of 2026-09-19): R2, the Convertible leaves her
   flybridge by the companionway, whose hatch is cut through the bridge deck over the salon; R3, the
   Skybridge stands her in the skylounge and takes her down the interior stair. The walk climbs them as
   flyhatch (a near-upright ship's ladder under the 0.55 m hatch) and upstair (one stair, in the well).
   HOUSE publishes both (HOUSE.wayDown) for boatInteriorRig.js; the deck plan the gameplay extractor reads
   is deckPlan().

   Fixed 3/4 turntable camera (elev 40 deg default), 45 deg steps, flat-facet shading, ordered dither,
   1 px depth keyline, no AA. OUTRIGGERS are a flag: render(dir,{riggers:'stowed'|'out'}). The 53's tuna
   tower is opt-in: render(dir,{tower:true}) / faceList(riggers, true); her default is the hardtop.
   PAINT swaps ramps only — one silhouette, one face list, one anchor table per hull.

   Exposes globalThis.SportFisherIso2 = { CONVERTIBLE, SKYBRIDGE, HULLS, hullIds, defaultHull, byId(id) (null
     for an id she does not have), PAINTS, paintRamps, schemeIds, defaultScheme, PX, DIRS, order,
     TEAK,DECKF,GRIP,GLAS,STEEL,IRON,KEY,HULL,BOOT,CREAM,BLUE, the room ramps, matsFor, GAIN, BIAS, LN, BAYER,
     LV, RO, GHOST_ROLES, tagMats, the default hull's (the convertible's) members under their own names, cell
     (the default hull's) and cells (by id) }. Plain script, no DOM: runs in a browser, in node (vm or
     require), or in the baker's V8. */
(function (root) {
  const PX = 32, S = 32;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const RIG_FILE = 'sportFisherIsoRig2.js', EXPORT_SYMBOL = 'SportFisherIso2';
  const FRAME = 'metres; +x starboard, +y bow, +z up; origin amidships, keel bottom, centreline; polygons counter-clockwise seen from above'; // geometry() and the walk share it: the bake compares the two exactly

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
  // the rooms' own ramps (v3's): carpet, upholstery, joinery, counter stone, linings, tinted glass
  const CARPET= ['#6f685d','#847c70','#999183','#ada596','#c0b9a9','#d1cbbc'];
  const LEATH = ['#6e5f4c','#8a7a62','#a69378','#c0ac8d','#d6c4a4','#e8d9bd'];
  const TEAKG = ['#2e1d10','#432b16','#5a3a1e','#714a27','#8a5c31','#a4703c'];
  const STONE = ['#8d8d86','#a5a59d','#bcbcb3','#d0d0c7','#e1e1d8','#eeeee6'];
  const WHITEG= ['#9aa09c','#b4b9b3','#ccd0c9','#dee1da','#eceee8','#f7f8f3'];
  const GLASSD= ['#7d9ea6','#a1c2c6','#c2dcdd','#d8ebec','#e8f4f4','#f4fafa'];

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
    { id:'navy',     label:'NAVY',          note:'Tournament navy topsides, white house, a gold cove and boot stripe.',
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
  // the painter's tables. paintTable(id): the exterior's twelve (MATS), its colour index (RINDEX) and the rooms'
  // table (ROOM); cutTable(id): both together for the cut views. matsFor() below is the spec's form (§5).
  const _matCache = {};
  function paintTable(id){
    const key = PAINT_BY[id] ? id : 'gelcoat';
    if(_matCache[key]) return _matCache[key];
    const R = paintRamps(key);
    const MATS = { hull:{ramp:R.top,off:0}, boot:{ramp:R.boot,off:0}, cream:{ramp:R.house,off:0},
                   stripe:{ramp:R.stripe,off:0}, teak:{ramp:TEAK,off:0}, deck:{ramp:DECKF,off:0},
                   grip:{ramp:GRIP,off:0}, glas:{ramp:GLAS,off:0}, steel:{ramp:STEEL,off:0},
                   iron:{ramp:IRON,off:0}, blk:{ramp:R.boot,off:-1}, dark:{ramp:R.boot,off:-2} };
    const RINDEX = {};
    [R.top,R.boot,R.house,R.stripe,TEAK,DECKF,GRIP,GLAS,STEEL,IRON].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
    // ROOM: the rooms' table, MATS-shaped (name -> {ramp, off}); every interior face's mat names one of its keys
    const ROOM = { carpet:{ramp:CARPET,off:0}, leath:{ramp:LEATH,off:0}, teakg:{ramp:TEAKG,off:0}, stone:{ramp:STONE,off:0},
                   whiteg:{ramp:WHITEG,off:0}, glassd:{ramp:GLASSD,off:0}, teak:MATS.teak, cream:MATS.cream, glas:MATS.glas,
                   steel:MATS.steel, iron:MATS.iron };
    return (_matCache[key] = { MATS, RINDEX, ROOM });
  }
  // the cut views paint the exterior and the rooms together: MATS + ROOM, and a colour index in which the
  // exterior's ramps win a shared colour (so an exterior pixel darkens at a depth step as it does uncut)
  const _roomCache = {};
  function cutTable(id){
    const key = PAINT_BY[id] ? id : 'gelcoat';
    if(_roomCache[key]) return _roomCache[key];
    const X = paintTable(key), MATS = Object.assign({}, X.MATS, X.ROOM), RINDEX = {};
    [CARPET,LEATH,TEAKG,STONE,WHITEG,GLASSD].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
    Object.assign(RINDEX, X.RINDEX);
    return (_roomCache[key] = { MATS, RINDEX });
  }
  // hull-layout-1 §5: matsFor(scheme | { scheme } | nothing) -> { scheme, MATS, ramps, M, roomMats }. MATS is the
  // exterior's twelve in their order with the rooms' own six appended (the rooms' teak, cream, glass, steel and iron
  // are the exterior's); ramps lists each distinct ramp once and M names a paint's ramp by its index there.
  const SCHEME_IDS = PAINTS.map(p=>p.id), DEFAULT_SCHEME = 'gelcoat';
  const ROOM_MATS = ['carpet','leath','teakg','stone','whiteg','glassd'];
  const _specCache = {};
  function matsFor(s){
    const id = (s && typeof s==='object') ? s.scheme : s, key = PAINT_BY[id] ? id : DEFAULT_SCHEME;
    if(_specCache[key]) return _specCache[key];
    const X = paintTable(key), MATS = {}, ramps = [], at = {}, M = {};
    for(const k of Object.keys(X.MATS)) MATS[k] = { ramp:X.MATS[k].ramp.slice(), off:X.MATS[k].off };
    for(const k of ROOM_MATS) MATS[k] = { ramp:X.ROOM[k].ramp.slice(), off:X.ROOM[k].off };
    for(const k of Object.keys(MATS)){ const kk = MATS[k].ramp.join(',');
      if(at[kk]==null){ at[kk] = ramps.length; ramps.push(MATS[k].ramp.slice()); }
      M[k] = { ramp:at[kk], off:MATS[k].off }; }
    return (_specCache[key] = { scheme:key, MATS, ramps, M, roomMats:ROOM_MATS.slice() });
  }

  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- ADR 0048 tags (hull-layout-1 §4.2–4.3). Faces carry lv and ro as these NUMBERS; geometry() publishes the
  // maps as ids and roles; LVN and RON name a number back ----
  const LV = { hull:0, main_deck:1, house:2, bridge:3, below:4, rigging:5, foredeck:6, top:7 };
  const RO = { shell:0, wall:1, roof:2, lining:3, part:4, floor:5, fit:6, tall:7, trim:8, rig:9 };
  const LVN = {}, RON = {}; Object.keys(LV).forEach(k=>{ LVN[LV[k]]=k; }); Object.keys(RO).forEach(k=>{ RON[RO[k]]=k; });
  const GHOST_ROLES = ['shell','wall','roof','lining','part','tall','trim'];
  const GHOSTS = {}; GHOST_ROLES.forEach(r=>{ GHOSTS[RO[r]]=true; });
  // review only: render(dir,{tags:'lv'|'ro'|'it'}) paints each face by its tag, one hue per value, shaded as usual
  const _tagCache = {};
  function tagMats(kind){
    if(_tagCache[kind]) return _tagCache[kind];
    const names = kind==='ro' ? Object.keys(RO) : (kind==='it' ? ['false','true'] : Object.keys(LV));
    const MATS = {}, RINDEX = {}, swatch = {};
    names.forEach((n,k)=>{ const h=20+360*k/names.length, r=[]; for(let i=0;i<7;i++) r.push(oklchHex(0.40+0.08*i, 0.12, h));
      MATS[n]={ ramp:r, off:0 }; swatch[n]=r[4]; r.forEach((c,i)=>{ RINDEX[c]={r,i}; }); });
    MATS.hull = MATS[names[0]];
    return (_tagCache[kind] = { MATS, RINDEX, names, swatch });
  }

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
  // Three optional inputs, all absent on the uncut path (which then draws the pass-3 bytes):
  //   opts._clip {x0,y0,x1,y1}  raster inside this rect only (view().region);
  //   opts._post {x0,y0,x1,y1}  run the depth step and the keyline over this rect (default: _clip);
  //   opts._ghost               ghostParams(): a fragment of a ghost-role face nearer than the player and inside the
  //                             reveal ellipse is discarded (solid inside the core, Bayer-dithered out to the rim).
  function _paint(faces, opts, G, MAT){
    const PW=G.W, PH=G.H;
    const MATS=MAT.MATS, RINDEX=MAT.RINDEX;
    const B=camBasis(opts);
    const RC=opts._clip||{ x0:0, y0:0, x1:PW-1, y1:PH-1 }, PR=opts._post||RC, GP=opts._ghost||null;
    const zbuf=new Float32Array(PW*PH).fill(Infinity);
    const col=new Array(PW*PH).fill(null);
    const dep=new Float32Array(PW*PH);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B,G));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.hull, g = (GP && GHOSTS[f.ro]) ? GP : null;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1]);
      function fillTri(a,b,c){
        const minX=Math.max(RC.x0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(RC.x1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(RC.y0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(RC.y1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
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
            if(g && d<g.dP){ const ex=(px-g.cx)*g.irx, ey=(py-g.cy)*g.iry, e=ex*ex+ey*ey;
              if(e<1){ const r=Math.sqrt(e); if(r<g.core || (r-g.core)*g.icore<BAYER[x&3][y&3]) continue; } }
            zbuf[i]=deff; dep[i]=d;
            let base=Math.floor(fidx);
            let idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    const out=col.slice();
    for(let y=PR.y0;y<=PR.y1;y++) for(let x=PR.x0;x<=PR.x1;x++){
      const i=y*PW+x; if(!col[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>RC.x1||ny>RC.y1) continue;
        const j=ny*PW+nx; if(!col[j]) continue;
        if(Math.abs(dep[i]-dep[j])>0.30){
          const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
          if(e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)];
        }
      }
    }
    for(let y=PR.y0;y<=PR.y1;y++) for(let x=PR.x0;x<=PR.x1;x++){
      const i=y*PW+x; if(out[i]) continue;
      let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy;
        if(nx>=RC.x0&&nx<=RC.x1&&ny>=RC.y0&&ny<=RC.y1&&col[ny*PW+nx]){ touch=true; break; }
      }
      if(touch){ out[i]=KEY; dep[i]=Infinity; }
    }
    out.dep=dep;   // depth rides along for the cutaway composite (boatCutawayRig.js); outline pixels sit behind everything
    return out;
  }
  function _toRGBA(out, PW, PH){
    const rgba=new Uint8ClampedArray(PW*PH*4); if(out.dep) rgba.dep=out.dep;
    for(let i=0;i<PW*PH;i++){
      const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16);
      rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255;
    }
    return rgba;
  }

  // ---- v3 loft helpers ----
  const clamp = (v,a,b)=>v<a?a:(v>b?b:v);
  const smooth = (t)=>{ t=clamp01(t); return t*t*(3-2*t); };
  function newell(v){ let nx=0,ny=0,nz=0; for(let i=0;i<v.length;i++){ const a=v[i], b=v[(i+1)%v.length];
    nx+=(a[1]-b[1])*(a[2]+b[2]); ny+=(a[2]-b[2])*(a[0]+b[0]); nz+=(a[0]-b[0])*(a[1]+b[1]); } return [nx,ny,nz]; }
  // split a polygon by a linear function g: [part g<=0, part g>0]
  function splitBy(v, g){
    const neg=[], pos=[], n=v.length;
    for(let i=0;i<n;i++){ const a=v[i], b=v[(i+1)%n], ga=g(a), gb=g(b);
      if(ga<=0) neg.push(a); else pos.push(a);
      if((ga<=0&&gb>0)||(ga>0&&gb<=0)){ const t=ga/(ga-gb); const p=[a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t, a[2]+(b[2]-a[2])*t]; neg.push(p); pos.push(p); } }
    return [neg.length>=3?neg:null, pos.length>=3?pos:null];
  }
  // plan polygons for the walk: point in polygon (even-odd), and an axis-aligned rect
  const pinPoly = (pt, poly)=>{ let c=false; for(let i=0,j=poly.length-1;i<poly.length;j=i++){ const a=poly[i], b=poly[j];
    if(((a[1]>pt[1])!==(b[1]>pt[1])) && (pt[0] < (b[0]-a[0])*(pt[1]-a[1])/(b[1]-a[1])+a[0])) c=!c; } return c; };
  const rectPoly = (x0,y0,x1,y1)=>[[x0,y0],[x1,y0],[x1,y1],[x0,y1]];

  // ===================================================================================================
  // ONE builder, two hulls.
  // ===================================================================================================
  function makeRig(spec){
    const L=spec.L, yT=-L/2, yB=L/2, DWL=spec.DWL, LZ=spec.loftZ!=null?spec.loftZ:DWL;   // DWL: the painted waterline. LZ: the datum the stem rake and transom teak are lofted from
    const G={ W:spec.W, H:spec.H, cx:spec.cx, cy:spec.cy };
    const stemY=(z)=>spec.stem.y0+(z-LZ)*spec.stem.k;
    const KEND=stemY(spec.keelEndZ), CEND=stemY(spec.chineEndZ);
    const fSz=mono(spec.sheer), fSx0=mono(spec.beam), fKz=mono(spec.keel.concat([[KEND,spec.keelEndZ]])),
          fCz=mono(spec.chineZ.concat([[CEND,spec.chineEndZ]])), fCx0=mono(spec.chineX.concat([[CEND,0]])), fP=mono(spec.flare), fCam=mono(spec.camber);
    const fSx=(y)=>Math.max(0,fSx0(Math.min(y,yB))), fCx=(y)=>Math.max(0,fCx0(Math.min(y,CEND)));
    const yK=(s)=>yT+s*(KEND-yT), yC=(s)=>yT+s*(CEND-yT), yS=(s)=>yT+s*(yB-yT);
    function section(s){
      const yk=yK(s), yc=yC(s), ys=yS(s), zk=fKz(yk), zc=fCz(yc), xc=fCx(yc), zs=fSz(ys), xs=Math.max(fSx(ys), xc);
      const P=[]; for(const u of [0,1/3,2/3]) P.push([u*xc, lerp(yk,yc,u), lerp(zk,zc,u)]);
      P.push([xc,yc,zc]);
      const dz=Math.max(0.3,zs-zc), tc0=clamp(1-0.34/dz,0.64,0.95), tc1=clamp(1-0.27/dz,tc0+0.01,0.975), p=fP((yc+ys)/2);
      for(const t of [0.2,0.42,Math.min(0.62,tc0-0.03),tc0,tc1,1]) P.push([xc+(xs-xc)*Math.pow(t,p), lerp(yc,ys,t), lerp(zc,zs,t)]);
      return P;
    }
    // shell sections (cosine-spaced) — the mesh the hull is built from
    const NS=46, SS=[]; for(let i=0;i<=NS;i++) SS.push(0.5-0.5*Math.cos(Math.PI*i/NS));
    const SEC=SS.map(section);
    // MESH-TRUE half-breadth: the shell quads sliced at y = const and tabulated x(z), so interior fit and
    // topside details follow the skin that is actually drawn (keel, chine and sheer run on different y per
    // section, so a planar reading of the three curves drifts off the mesh near the bow).
    const HDY=0.05, HDZ=0.04, HNY=Math.ceil(L/HDY)+1, HNZ=Math.ceil((Math.max(...spec.sheer.map(p=>p[1]))+0.3)/HDZ)+1, HX=new Float32Array(HNY*HNZ);
    for(let iy=0;iy<HNY;iy++){ const yc=Math.min(yB-1e-4,Math.max(yT+1e-4,yT+iy*HDY)), sg=[];
      for(let i=0;i<NS;i++){ const A=SEC[i], B2=SEC[i+1]; let y0=1e9, y1=-1e9; for(let k=0;k<10;k++){ y0=Math.min(y0,A[k][1],B2[k][1]); y1=Math.max(y1,A[k][1],B2[k][1]); } if(y0>yc||y1<yc) continue;
        for(let k=0;k<9;k++){ const q=[A[k],B2[k],B2[k+1],A[k+1]], pts=[];
          for(let e=0;e<4;e++){ const a=q[e], b=q[(e+1)%4], da=a[1]-yc, db=b[1]-yc;
            if((da<=0&&db>0)||(da>0&&db<=0)){ const t=da/(da-db); pts.push([a[0]+(b[0]-a[0])*t, a[2]+(b[2]-a[2])*t]); } }
          if(pts.length>=2) sg.push(pts[0],pts[1]); } }
      let lo=-1, hi=-1;
      for(let iz=0;iz<HNZ;iz++){ const zc=iz*HDZ; let best=-1;
        for(let s=0;s<sg.length;s+=2){ const p=sg[s], r=sg[s+1]; if((p[1]-zc)*(r[1]-zc)>0) continue; const dz=r[1]-p[1];
          const x=Math.abs(dz)<1e-9?Math.min(p[0],r[0]):p[0]+(r[0]-p[0])*(zc-p[1])/dz; if(best<0||x<best) best=x; }
        HX[iy*HNZ+iz]=best; if(best>=0){ if(lo<0) lo=iz; hi=iz; } }
      for(let iz=0;iz<HNZ;iz++){ const k=iy*HNZ+iz; if(lo<0||iz<lo) HX[k]=0; else if(iz>hi) HX[k]=HX[iy*HNZ+hi]; else if(HX[k]<0) HX[k]=HX[k-1]; } }
    function halfAtZ(y, z){
      if(y<yT||y>yB) return 0;
      const fy=(y-yT)/HDY, iy=Math.min(HNY-2,Math.max(0,Math.floor(fy))), ty=clamp01(fy-iy), fz=clamp(z/HDZ,0,HNZ-1.001), iz=Math.floor(fz), tz=fz-iz;
      const a=HX[iy*HNZ+iz], b=HX[iy*HNZ+iz+1], c=HX[(iy+1)*HNZ+iz], d=HX[(iy+1)*HNZ+iz+1];
      return Math.max(0, Math.min(lerp(a,b,tz), lerp(c,d,tz)));   // the narrower neighbour slice: conservative for fit
    }

    const F=[], IF=[], RIGF={ stowed:[], out:[] }, TWF=[], HTP=[];
    // ---- face emission (ADR 0048 §3, hull-layout-1 §4.2): the builder names a level and a role (at), and every
    // face carries them as the NUMBERS geometry().ids / .roles map them to, and it (true only for the rooms, IF).
    // The 90's open top station is its own level, top. Exterior upholstery is drawn in the house white one ramp
    // step down, so the exterior stays at the fleet's twelve materials; the rooms keep their leather.
    let TG=F, cLV='hull', cRO='shell';
    const MATMAP={ leath:['cream',-0.2] };
    const at=(lv, ro)=>{ cLV=lv; cRO=ro; };
    const put=(f)=>{ const it=(TG===IF); if(!it){ const m=MATMAP[f.mat]; if(m){ f.mat=m[0]; f.b=(f.b||0)+m[1]; } }
      f.lv=LV[cLV]; f.ro=RO[cRO]; f.it=it; TG.push(f); };
    function Q(v,out,mat,b,db){ const n=newell(v); if(Math.hypot(n[0],n[1],n[2])<1e-7) return;
      if(n[0]*out[0]+n[1]*out[1]+n[2]*out[2]<0) v=v.slice().reverse(); put({ v, mat, b:b||0, db:db||0 }); }
    function boxQ(x0,x1,y0,y1,z0,z1,mat,b,db,topMat){
      Q([[x0,y0,z1],[x1,y0,z1],[x1,y1,z1],[x0,y1,z1]],[0,0,1],topMat||mat,b,db);
      Q([[x0,y0,z0],[x1,y0,z0],[x1,y1,z0],[x0,y1,z0]],[0,0,-1],mat,(b||0)-1.2,db);
      Q([[x0,y1,z0],[x1,y1,z0],[x1,y1,z1],[x0,y1,z1]],[0,1,0],mat,b,db);
      Q([[x0,y0,z0],[x1,y0,z0],[x1,y0,z1],[x0,y0,z1]],[0,-1,0],mat,b,db);
      Q([[x1,y0,z0],[x1,y1,z0],[x1,y1,z1],[x1,y0,z1]],[1,0,0],mat,b,db);
      Q([[x0,y0,z0],[x0,y1,z0],[x0,y1,z1],[x0,y0,z1]],[-1,0,0],mat,b,db);
    }
    function tubeQ(A,B2,r0,r1,mat,b){
      const ax=v_norm(v_sub(B2,A)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
      const u=v_norm(v_cross(ax,up)), w=v_cross(u,ax);
      const ring=(P,r)=>[0,1,2,3].map(k=>{ const a=k*Math.PI/2+Math.PI/4; return v_add(P, v_add(v_mul(u,Math.cos(a)*r), v_mul(w,Math.sin(a)*r))); });
      const R0=ring(A,r0), R1=ring(B2,r1==null?r0:r1), mid=v_mul(v_add(A,B2),0.5);
      for(let k=0;k<4;k++){ const k2=(k+1)%4, q=[R0[k],R0[k2],R1[k2],R1[k]], c=v_mul(v_add(v_add(q[0],q[1]),v_add(q[2],q[3])),0.25);
        Q(q, v_sub(c,mid), mat, b, -0.15); }
    }
    function bands(v, out, BD, bb){ let rest=v;
      for(const [zt,mat,b,db] of BD){ if(!rest) break;
        if(zt===Infinity){ Q(rest,out,mat,(b||0)+(bb||0),db); break; }
        const [lo,hi]=splitBy(rest,(p)=>p[2]-zt); if(lo) Q(lo,out,mat,(b||0)+(bb||0),db); rest=hi; } }

    // ---- plan outlines + ring lofts (house tiers, coamings, slabs) ----
    const NN=14, NSD=18, NCR=5, NAF=4, NC=NN+1+NSD+NCR;   // columns nose tip → aft corner end; aft segment follows
    function outline(R, o){
      const e=o.e||2.2, rA=Math.min(o.rA||0.5, R.W*0.5), P=[];
      const yS1=R.yA+rA, yN=Math.max(R.yF-R.N, yS1+0.02), N=R.yF-yN;
      for(let i=0;i<=NN;i++){ const th=(i/NN)*Math.PI/2; P.push([R.W*Math.pow(Math.sin(th),2/e), yN+N*Math.pow(Math.cos(th),2/e)]); }
      for(let i=1;i<=NSD;i++) P.push([R.W, lerp(yN,yS1,i/NSD)]);
      for(let i=1;i<=NCR;i++){ const a=(i/NCR)*Math.PI/2; P.push([R.W-rA+rA*Math.cos(a), R.yA+rA-rA*Math.sin(a)]); }
      for(let i=1;i<=NAF;i++) P.push([(R.W-rA)*(1-i/NAF), R.yA]);
      if(o.clamp) for(const p of P) p[0]=Math.min(p[0], Math.max(0.02, o.clamp(p[1])));
      return P;
    }
    const pnorm=(P,j)=>{ const a=P[Math.max(0,j-1)], b=P[Math.min(P.length-1,j+1)]; const tx=b[0]-a[0], ty=b[1]-a[1], m=Math.hypot(tx,ty)||1; return [-ty/m, tx/m]; };
    function ringTable(tbl){ const fs=[1,2,3,4].map(c=>mono(tbl.map(r=>[r[0],r[c]]))); return (z)=>({ z, yF:fs[0](z), yA:fs[1](z), W:fs[2](z), N:fs[3](z) }); }
    function ringsFrom(tbl, zs, o){ const f=ringTable(tbl); return zs.map(z=>({ z, P:outline(f(z),o) })); }
    function ringAt(RG, z){ let k=0; while(k<RG.length-2 && z>RG[k+1].z) k++; const A=RG[k], B2=RG[k+1], t=clamp01((z-A.z)/(B2.z-A.z));
      return { z, P:A.P.map((p,j)=>[lerp(p[0],B2.P[j][0],t), lerp(p[1],B2.P[j][1],t)]) }; }
    function hwAt(P, y){ if(y>P[0][1]) return 0; for(let j=0;j+1<NC;j++){ const a=P[j], b=P[j+1]; if((a[1]-y)*(b[1]-y)<=0){ const t=a[1]===b[1]?0:(a[1]-y)/(a[1]-b[1]); return lerp(a[0],b[0],t); } } return 0; }
    function loftWalls(RG, mat, b){
      for(let k=0;k+1<RG.length;k++){ const A=RG[k], B2=RG[k+1], zf=k/(RG.length-1);
        for(let j=0;j+1<NC;j++) for(const sd of [-1,1]){
          const a0=A.P[j], a1=A.P[j+1], b0=B2.P[j], b1=B2.P[j+1], n0=pnorm(A.P,j), n1=pnorm(A.P,j+1), nx=(n0[0]+n1[0])/2, ny=(n0[1]+n1[1])/2;
          Q([[sd*a0[0],a0[1],A.z],[sd*a1[0],a1[1],A.z],[sd*b1[0],b1[1],B2.z],[sd*b0[0],b0[1],B2.z]],[sd*nx,ny,0.04],mat,
            (b||0)+0.02+0.40*Math.max(0,ny)-0.45*Math.max(0,-ny)+0.12*zf, 0);
        }
      }
    }
    // straight aft segment as a full-width grid: openings skipped, glazing painted glass
    function aftGrid(RG, o, mat, b){
      const brk=new Set([0]); (o.open||[]).concat(o.glaze||[]).forEach(r=>{ brk.add(r.x0); brk.add(r.x1); });
      for(let k=0;k+1<RG.length;k++){ const A=RG[k], B2=RG[k+1], EA=A.P[NC-1][0], EB=B2.P[NC-1][0], yA=A.P[NC][1], yB2=B2.P[NC][1];
        const xs=[...brk].filter(x=>Math.abs(x)<Math.min(EA,EB)-0.02).concat([-1e9,1e9]).sort((a,b)=>a-b);
        for(let i=0;i+1<xs.length;i++){ const xa=xs[i], xb=xs[i+1];
          const ca=(x,E)=>clamp(x,-E,E), xm=(ca(xa,EA)+ca(xb,EA))/2, zm=(A.z+B2.z)/2;
          if((o.open||[]).some(r=>xm>r.x0&&xm<r.x1&&zm>r.z0&&zm<r.z1)) continue;
          const gl=(o.glaze||[]).some(r=>xm>r.x0&&xm<r.x1&&zm>r.z0&&zm<r.z1);
          Q([[ca(xa,EA),yA,A.z],[ca(xb,EA),yA,A.z],[ca(xb,EB),yB2,B2.z],[ca(xa,EB),yB2,B2.z]],[0,-1,0.05], gl?'glas':mat, gl?-0.55:(b||0)-0.45, gl?0.02:0);
        }
      }
    }
    // holes in lids (stairwells, hatches): keep the parts of a polygon outside a plan rect
    function punch(v, r){ const out=[]; let rest=v;
      for(const g of [(p)=>p[0]-r.x0, (p)=>r.x1-p[0], (p)=>p[1]-r.y0, (p)=>r.y1-p[1]]){ if(!rest) break; const s=splitBy(rest,g); if(s[0]) out.push(s[0]); rest=s[1]; }
      return out; }
    function putH(v,out,mat,b,db,holes){ let parts=[v]; if(holes&&holes.length) for(const r of holes){ const nx=[]; parts.forEach(p=>punch(p,r).forEach(q=>nx.push(q))); parts=nx; } parts.forEach(p=>Q(p,out,mat,b,db)); }
    function crown(P, n, z, cam, mat, b, db, down, holes){
      const XS=[0,0.5,0.82,1], cz=(f)=>z+(down?0:cam*(1-Math.pow(f,2.2)));
      for(let j=0;j+1<n;j++){ const A=P[j], B2=P[j+1];
        for(let k=0;k+1<XS.length;k++) for(const sd of [-1,1])
          putH([[sd*A[0]*XS[k],A[1],cz(XS[k])],[sd*A[0]*XS[k+1],A[1],cz(XS[k+1])],[sd*B2[0]*XS[k+1],B2[1],cz(XS[k+1])],[sd*B2[0]*XS[k],B2[1],cz(XS[k])]],
            down?[0,0,-1]:[sd*XS[k]*0.35,0,1], mat, down?(b||0)-1.5:(b||0)+0.55-0.13*k, db||0, holes);
      }
    }
    // dark glass band laid on a ring loft (frame + glass, two offset layers)
    function bandOn(RG, bd){
      const lo=mono(bd.lo), hi=mono(bd.hi), cols=[];
      const colAt=(j,z)=>{ const R=ringAt(RG,z); return R.P[j]; };
      const colN=(j,z)=>{ const R=ringAt(RG,z); return pnorm(R.P,j); };
      for(let j=0;j<NN+1+NSD;j++){
        let zb=bd.z0, zt=bd.z1;
        for(let it=0;it<3;it++){ zb=lo(colAt(j,zb)[1]); zt=hi(colAt(j,zt)[1]); }
        const ym=colAt(j,(zb+zt)/2)[1];
        if(zt<zb){ const m=(zb+zt)/2; zb=m; zt=m; }
        cols.push({ ok: ym>=bd.yTip-0.05, zb, zt });
      }
      for(let j=0;j+1<cols.length;j++){ const c0=cols[j], c1=cols[j+1]; if(!c0.ok||!c1.ok) continue; if(c0.zt-c0.zb<0.01 && c1.zt-c1.zb<0.01) continue;
        for(const [off,ex,mat,b,db] of [[0.024,0.045,'iron',-0.2,0.05],[0.048,0,'glas',-0.62,0.07]])
          for(let r=0;r<3;r++){ const f0=r/3, f1=(r+1)/3;
            const z0a=lerp(c0.zb-ex*(c0.zt>c0.zb+0.01?1:0),c0.zt+ex*(c0.zt>c0.zb+0.01?1:0),f0), z1a=lerp(c0.zb-ex*(c0.zt>c0.zb+0.01?1:0),c0.zt+ex*(c0.zt>c0.zb+0.01?1:0),f1);
            const z0b=lerp(c1.zb-ex*(c1.zt>c1.zb+0.01?1:0),c1.zt+ex*(c1.zt>c1.zb+0.01?1:0),f0), z1b=lerp(c1.zb-ex*(c1.zt>c1.zb+0.01?1:0),c1.zt+ex*(c1.zt>c1.zb+0.01?1:0),f1);
            for(const sd of [-1,1]){
              const P=(jj,z)=>{ const p=colAt(jj,z), n=colN(jj,z); return [sd*(p[0]+off*n[0]), p[1]+off*n[1], z]; };
              const n=colN(j,(c0.zb+c0.zt)/2);
              Q([P(j,z0a),P(j+1,z0b),P(j+1,z1b),P(j,z1a)],[sd*n[0],n[1],0.1],mat,b,db);
            }
          }
      }
    }
    // slab: plan outline, crowned top (floor), fascia sweeping down to zf(y) (wall), underside (wall)
    function slab(o){
      const P=outline({ z:o.zTop, yF:o.yF, yA:o.yA, W:o.W, N:o.N }, { e:o.e||2.0, rA:o.rA }), zf=mono(o.zf), n=P.length;
      at(o.lv, o.rWall||'wall');
      for(let j=0;j+1<n;j++){ const a=P[j], b=P[j+1], nn=pnorm(P,j);
        for(const sd of [-1,1]) Q([[sd*a[0],a[1],zf(a[1])],[sd*b[0],b[1],zf(b[1])],[sd*b[0],b[1],o.zTop],[sd*a[0],a[1],o.zTop]],[sd*nn[0],nn[1],0],'cream',(o.bF||0.15)+0.35*Math.max(0,nn[1])-0.4*Math.max(0,-nn[1]),0); }
      at(o.lv, o.rTop||'floor'); crown(P, n, o.zTop, o.cam||0.03, 'cream', o.bT||0, 0, false, o.holes);
      at(o.lv, o.rWall||'wall');
      for(let j=0;j+1<n;j++){ const A=P[j], B2=P[j+1];
        for(const sd of [-1,1]) putH([[0,A[1],zf(A[1])+0.002],[sd*A[0],A[1],zf(A[1])],[sd*B2[0],B2[1],zf(B2[1])],[0,B2[1],zf(B2[1])+0.002]],[0,0,-1],'cream',-1.6,0,o.holes); }
      return P;
    }

    // ==================================== EXTERIOR ====================================
    // ---- hull shell: 46 cosine-spaced sections; boot + boot-top by z-clip; cove row on the topsides ----
    at('hull','shell');
    const mx=(p,sd)=>[sd*p[0],p[1],p[2]];
    const HB=[[DWL-0.03,'boot',-0.3,0],[DWL+0.08,'stripe',0.1,0.004],[Infinity,'hull',0,0]];
    for(let i=0;i<NS;i++){ const A=SEC[i], B2=SEC[i+1];
      for(let k=0;k<9;k++) for(const sd of [-1,1]){
        const q=[mx(A[k],sd),mx(B2[k],sd),mx(B2[k+1],sd),mx(A[k+1],sd)], out=k<3?[sd*0.45,0,-1]:[sd,0,0.08];
        if(k===7) Q(q,out,'stripe',0.18,0.006); else bands(q,out,HB,k<3?-0.15:0);
      }
    }
    { const s0=SEC[0], poly=[]; for(let k=9;k>=0;k--) poly.push([s0[k][0],yT,s0[k][2]]); for(let k=1;k<=9;k++) poly.push([-s0[k][0],yT,s0[k][2]]);
      bands(poly,[0,-1,0],[[DWL-0.03,'boot',-0.9,0],[DWL+0.08,'stripe',-0.5,0.004],[LZ+0.40,'hull',-0.7,0],[Infinity,'teak',-0.55,0.004]],0); }
    // ---- teak cap: wide covering board in the cockpit, a slim rail stem to transom ----
    const CF=spec.cockpitFwd, CZ=spec.cockpitZ;
    const capW=(y)=>{ if(y<CF-0.1) return spec.capW; if(y>CF+0.9) return spec.railW; return lerp(spec.capW,spec.railW,smooth((y-CF+0.1)/1.0)); };
    const xin=(y)=>Math.max(0.05,fSx(y)-capW(y));
    at('hull','trim');
    for(let i=0;i<NS;i++) for(const sd of [-1,1]){
      const c=(s)=>{ const y=yS(s), z=fSz(y), x=fSx(y); return { y, z, x, w:Math.min(capW(y),x) }; }, a=c(SS[i]), b=c(SS[i+1]);
      Q([[sd*(a.x+0.015),a.y,a.z+0.004],[sd*(b.x+0.015),b.y,b.z+0.004],[sd*(b.x-b.w),b.y,b.z+0.012],[sd*(a.x-a.w),a.y,a.z+0.012]],[sd*0.2,0,1],'teak',-0.35,0.012);
      Q([[sd*(a.x+0.015),a.y,a.z-0.07],[sd*(b.x+0.015),b.y,b.z-0.07],[sd*(b.x+0.015),b.y,b.z+0.004],[sd*(a.x+0.015),a.y,a.z+0.004]],[sd,0,0],'teak',-0.25,0.014);
    }
    { const z=fSz(yT), x=fSx(yT)+0.015, w=spec.capW;
      Q([[-x,yT-0.015,z+0.004],[x,yT-0.015,z+0.004],[x,yT+w,z+0.012],[-x,yT+w,z+0.012]],[0,0,1],'teak',-0.35,0.012);
      Q([[-x,yT-0.015,z-0.07],[x,yT-0.015,z-0.07],[x,yT-0.015,z+0.004],[-x,yT-0.015,z+0.004]],[0,-1,0],'teak',-0.6,0.014); }
    // ---- topside details: hull side light (slanted lozenge) + engine-room intake ----
    function hullPatch(y0,y1,z0,z1,mat,b,off,slant,ends){
      const N=12, e=(t)=>{ const k=Math.min(1,Math.min(t,1-t)/(ends||0.16)); return Math.sqrt(Math.max(0,1-(1-k)*(1-k))); };
      for(let i=0;i<N;i++){ const ta=i/N, tb=(i+1)/N, ya=lerp(y0,y1,ta), yb=lerp(y0,y1,tb), zm=(z0+z1)/2;
        const ha=(z1-z0)/2*e(ta), hb=(z1-z0)/2*e(tb), sa=slant*(ta-0.5), sb=slant*(tb-0.5);
        for(const sd of [-1,1]){ const P=(y,z)=>[sd*(halfAtZ(y,z)+off),y,z];
          Q([P(ya,zm-ha+sa),P(yb,zm-hb+sb),P(yb,zm+hb+sb),P(ya,zm+ha+sa)],[sd,0,0],mat,b,DBP); } }
    }
    at('hull','shell');
    if(spec.hullWin){ hullPatch(spec.hullWin.y0,spec.hullWin.y1,spec.hullWin.z0,spec.hullWin.z1,'iron',-0.2,0.012,spec.hullWin.slant,0.12);
      hullPatch(spec.hullWin.y0+0.05,spec.hullWin.y1-0.05,spec.hullWin.z0+0.04,spec.hullWin.z1-0.04,'glas',-0.5,0.03,spec.hullWin.slant,0.12); }
    { const V=spec.vent, sl=V.slant||0; hullPatch(V.y0,V.y1,V.z0,V.z1,'iron',0.35,0.012,sl,V.ends||0.05);
      for(let k=1;k<V.n;k++){ const z=lerp(V.z0,V.z1,k/V.n), ins=V.ends?0.10+0.05*Math.abs(k-V.n/2):0.06; hullPatch(V.y0+ins,V.y1-ins,z-0.012,z+0.012,'steel',0.4,0.03,sl,0.02); } }

    // ---- cockpit: planked sole, liner, fighting chair, side stairs, mezzanine ----
    at('main_deck','floor');
    const CS=14;
    for(let i=0;i<CS;i++){ const y0=lerp(yT+0.06,CF,i/CS), y1=lerp(yT+0.06,CF,(i+1)/CS), w0=xin(y0)-0.01, w1=xin(y1)-0.01, PL=10;
      for(let k=0;k<PL;k++){ const t0=k/PL, t1=(k+1)/PL;
        Q([[lerp(-w0,w0,t0),y0,CZ],[lerp(-w0,w0,t1),y0,CZ],[lerp(-w1,w1,t1),y1,CZ],[lerp(-w1,w1,t0),y1,CZ]],[0,0,1],'teak',k%2?-0.5:-0.1,0); } }
    at('hull','shell');
    for(let i=0;i<CS;i++) for(const sd of [-1,1]){ const y0=lerp(yT+0.06,CF,i/CS), y1=lerp(yT+0.06,CF,(i+1)/CS);
      Q([[sd*xin(y0),y0,CZ],[sd*xin(y1),y1,CZ],[sd*xin(y1),y1,fSz(y1)-0.003],[sd*xin(y0),y0,fSz(y0)-0.003]],[-sd,0,0.05],'cream',-0.9,0); }
    { const w=xin(yT+0.06); Q([[-w,yT+0.06,CZ],[w,yT+0.06,CZ],[w,yT+0.06,fSz(yT)-0.003],[-w,yT+0.06,fSz(yT)-0.003]],[0,1,0],'cream',-0.6,0); }
    at('hull','trim');
    { const D=spec.doors; const y=yT-0.02; for(const [x0,x1,h] of [[D.tr[0],D.tr[1],D.trH],[D.tu[0],D.tu[1],D.tuH]]){
      const zb=CZ+0.04, zt=CZ+h; Q([[x0,y,zb],[x1,y,zb],[x1,y,zb+0.03],[x0,y,zb+0.03]],[0,-1,0],'iron',-0.3,0.02);
      Q([[x0,y,zb],[x0+0.03,y,zb],[x0+0.03,y,zt],[x0,y,zt]],[0,-1,0],'iron',-0.3,0.02); Q([[x1-0.03,y,zb],[x1,y,zb],[x1,y,zt],[x1-0.03,y,zt]],[0,-1,0],'iron',-0.3,0.02); } }
    for(const sd of [-1,1]) spec.capRods.forEach(y=>{ const z=fSz(y), x=sd*(xin(y)+capW(y)*0.45); tubeQ([x,y,z-0.03],[x*1.02,y-0.12,z+0.24],0.035,0.035,'steel',0.1); });
    at('main_deck','fit');
    { const y=spec.chair.y;
      boxQ(-0.15,0.15,y-0.15,y+0.15,CZ,CZ+0.44,'steel',-0.2);
      boxQ(-0.42,0.42,y-0.40,y+0.36,CZ+0.44,CZ+0.58,'leath',0.1);
      boxQ(-0.40,0.40,y+0.28,y+0.42,CZ+0.58,CZ+1.08,'leath',-0.1);
      for(const sd of [-1,1]) boxQ(sd*0.36,sd*0.46,y-0.30,y+0.36,CZ+0.58,CZ+0.78,'teak',0.0);
      tubeQ([-0.34,y-0.82,CZ+0.30],[0.34,y-0.82,CZ+0.30],0.04,0.04,'steel',0.2);
      for(const sd of [-1,1]) tubeQ([sd*0.30,y-0.82,CZ+0.30],[sd*0.10,y-0.20,CZ+0.36],0.03,0.03,'steel',0.1); }
    spec.tubs.forEach(t=>boxQ(t[0]-0.28,t[0]+0.28,t[1]-0.22,t[1]+0.22,CZ,CZ+0.52,'cream',0.1,0,'iron'));
    const ST=spec.sideStair, MZ=spec.mezz;
    for(const sd of [-1,1]){ const xa=sd>0?ST.x0:-ST.x1, xb=sd>0?ST.x1:-ST.x0, run=(CF-ST.y0)/ST.n;
      for(let k=0;k<ST.n;k++){ const ya=ST.y0+k*run, zt=lerp(CZ,MZ.z,(k+1)/(ST.n+1));
        boxQ(xa,xb,ya,CF,CZ,zt-0.03,'cream',-0.25); boxQ(xa,xb,ya,ya+run+0.01,zt-0.03,zt,'teak',-0.1); } }
    at('main_deck','floor');
    const mzw=(y)=>spec.wings?spec.wings.x0-0.01:xin(y)-0.13;
    for(let i=0;i<4;i++){ const y0=lerp(MZ.y0,MZ.y1,i/4), y1=lerp(MZ.y0,MZ.y1,(i+1)/4);
      Q([[-mzw(y0),y0,MZ.z],[mzw(y0),y0,MZ.z],[mzw(y1),y1,MZ.z],[-mzw(y1),y1,MZ.z]],[0,0,1],'teak',i%2?-0.35:-0.15,0); }
    at('main_deck','wall');
    Q([[-xin(MZ.y0),MZ.y0,CZ],[xin(MZ.y0),MZ.y0,CZ],[xin(MZ.y0),MZ.y0,MZ.z],[-xin(MZ.y0),MZ.y0,MZ.z]],[0,-1,0],'cream',-0.35,0);
    for(const x of MZ.drawers) boxQ(x-MZ.dw,x+MZ.dw,MZ.y0-0.012,MZ.y0,CZ+0.28,Math.min(CZ+0.92,MZ.z-0.08),'cream',-0.6,0.01);
    for(const sd of [-1,1]){ if(spec.wings) break; const P=(y,x,z)=>[sd*x,y,z], a=MZ.y0, b=MZ.y1, za=fSz(MZ.y0)-0.05, zb=MZ.z+0.52, ia=mzw(a)-0.02, ib=mzw(b)-0.02, oa=xin(a), ob=xin(b);
      Q([P(a,ia,zb),P(b,ib,zb),P(b,ob,zb),P(a,oa,zb)],[0,0,1],'cream',0.05,0); Q([P(a,ia,za),P(b,ib,za),P(b,ib,zb),P(a,ia,zb)],[-sd,0,0],'cream',0.05,0);
      Q([P(a,oa,za),P(b,ob,za),P(b,ob,zb),P(a,oa,zb)],[sd,0,0],'cream',0.05,0); Q([P(a,ia,za),P(a,oa,za),P(a,oa,zb),P(a,ia,zb)],[0,-1,0],'cream',0.05,0);
      Q([P(b,ib,za),P(b,ob,za),P(b,ob,zb),P(b,ib,zb)],[0,1,0],'cream',0.05,0); }
    // mezzanine forward riser: closes the gap outboard of the house corner, down to the side-deck line
    if(MZ.z>fSz(MZ.y1)-0.06) Q([[-mzw(MZ.y1),MZ.y1,fSz(MZ.y1)-0.06],[mzw(MZ.y1),MZ.y1,fSz(MZ.y1)-0.06],[mzw(MZ.y1),MZ.y1,MZ.z],[-mzw(MZ.y1),MZ.y1,MZ.z]],[0,1,0],'cream',-0.2,0);
    // the 53: the house sides run aft past the salon bulkhead as raked wings flanking the mezzanine
    if(spec.wings){ const Wg=spec.wings, N=8; at('house','wall');
      const edge=(t)=>[lerp(Wg.yA,Wg.yF,t), lerp(Wg.zA,Wg.zF,Math.pow(t,Wg.p||1))];
      for(const sd of [-1,1]){ const P=(x,y,z)=>[sd*x,y,z];
        for(let i=0;i<N;i++){ const [ya,za]=edge(i/N), [yb,zb]=edge((i+1)/N);
          Q([P(Wg.x1,ya,za),P(Wg.x1,yb,zb),P(Wg.x0,yb,zb),P(Wg.x0,ya,za)],[0,-0.6,1],'cream',0.25,0);
          Q([P(Wg.x1,ya,CZ),P(Wg.x1,yb,CZ),P(Wg.x1,yb,zb),P(Wg.x1,ya,za)],[sd,0,0],'cream',0.05,0);
          Q([P(Wg.x0,ya,CZ),P(Wg.x0,yb,CZ),P(Wg.x0,yb,zb),P(Wg.x0,ya,za)],[-sd,0,0],'cream',-0.25,0); }
        Q([P(Wg.x0,Wg.yA,CZ),P(Wg.x1,Wg.yA,CZ),P(Wg.x1,Wg.yA,Wg.zA),P(Wg.x0,Wg.yA,Wg.zA)],[0,-1,0],'cream',-0.3,0);
        for(let i=0;i<4;i++){ const y0=lerp(Wg.yA,Wg.yF,i/4), y1=lerp(Wg.yA,Wg.yF,(i+1)/4);   // side deck outboard of the wing
          Q([P(Wg.x1,y0,fSz(y0)+0.013),P(xin(y0),y0,fSz(y0)+0.012),P(xin(y1),y1,fSz(y1)+0.012),P(Wg.x1,y1,fSz(y1)+0.013)],[0,0,1],'cream',0.2,0); } } }
    at('main_deck','fit');
    { const s=MZ.seat, z=MZ.z; boxQ(s.x0,s.x1,s.y0,s.y1,z,z+0.34,'cream',-0.1); boxQ(s.x0+0.05,s.x1-0.05,s.y0+0.02,s.y1-0.02,z+0.34,z+0.44,'leath',0.15);
      if(s.back==='-y') boxQ(s.x0+0.05,s.x1-0.05,s.y0,s.y0+0.16,z+0.44,z+0.92,'leath',-0.05); else boxQ(s.x0+0.05,s.x1-0.05,s.y1-0.16,s.y1,z+0.44,z+0.92,'leath',-0.05); }

    // ---- SALON: ring loft on the sheer, raked front, band, aft wall with slider, roof ----
    const SA=spec.salon, SO={ e:2.2, rA:SA.rA!=null?SA.rA:0.55, clamp:(y)=>fSx(y)-0.20 };
    at('house','wall');
    const SAL=ringsFrom(SA.rings, SA.zs, SO);
    loftWalls(SAL,'cream',0);
    aftGrid(SAL, { open:[SA.door], glaze:[SA.fixed] }, 'cream', 0);
    bandOn(SAL, SA.band);
    at('house','roof'); crown(SAL[SAL.length-1].P, NC+NAF, SAL[SAL.length-1].z, SA.cam, 'cream', 0, 0, false, [].concat(spec.sky?[spec.sky.well]:[], spec.flyComp?[spec.flyComp]:[]));
    const salonAft=(z)=>ringAt(SAL,z).P[NC][1];
    // ---- side decks + foredeck (the below flat's lid) ----
    at('foredeck','floor');
    // TUCK: the deck's outer chord and the cap rail's inner chord are sampled at different stations, so they
    // part by a few mm between samples and the salon/berth linings speckled through. A 35 mm strip dipping
    // under the cap (4 mm below its face, which also carries the 12 mm decal bias) closes the seam unseen.
    const tuck=(sd,y0,y1,x0,x1,b,n)=>{ const t0=Math.max(x0,Math.min(x0+0.035,fSx(y0)-0.012)), t1=Math.max(x1,Math.min(x1+0.035,fSx(y1)-0.012));
      Q([[sd*x0,y0,fSz(y0)+0.012],[sd*t0,y0,fSz(y0)+0.005],[sd*t1,y1,fSz(y1)+0.005],[sd*x1,y1,fSz(y1)+0.012]],n,'cream',b,0); };
    { const R3=ringAt(SAL,3.40).P, N=26;
      for(let i=0;i<N;i++){ const y0=lerp(SA.aftY,spec.foreFrom,i/N), y1=lerp(SA.aftY,spec.foreFrom,(i+1)/N);
        for(const sd of [-1,1]){ const xh=(y)=>hwAt(ringAt(SAL,fSz(y)+0.013).P,y)-0.03, xi0=Math.min(xh(y0),fSx(y0)-spec.railW-0.02), xi1=Math.min(xh(y1),fSx(y1)-spec.railW-0.02);
          const xo0=fSx(y0)-spec.railW, xo1=fSx(y1)-spec.railW;
          Q([[sd*xi0,y0,fSz(y0)+0.013],[sd*xo0,y0,fSz(y0)+0.012],[sd*xo1,y1,fSz(y1)+0.012],[sd*xi1,y1,fSz(y1)+0.013]],[0,0,1],'cream',0.2,0);
          tuck(sd,y0,y1,xo0,xo1,0.2,[0,0,1]); } } }
    { const FD=20, XF=[0,0.35,0.62,0.85,1];
      for(let i=0;i<FD;i++){ const y0=lerp(spec.foreFrom,yB-0.02,i/FD), y1=lerp(spec.foreFrom,yB-0.02,(i+1)/FD);
        const w0=Math.max(0.01,fSx(y0)-spec.railW), w1=Math.max(0.01,fSx(y1)-spec.railW), z=(y,t)=>fSz(y)+0.012+fCam(y)*(1-t*t);
        for(let k=0;k+1<XF.length;k++) for(const sd of [-1,1])
          Q([[sd*w0*XF[k],y0,z(y0,XF[k])],[sd*w0*XF[k+1],y0,z(y0,XF[k+1])],[sd*w1*XF[k+1],y1,z(y1,XF[k+1])],[sd*w1*XF[k],y1,z(y1,XF[k])]],[sd*XF[k]*0.3,0,1],'cream',0.45-0.08*k,0);
        for(const sd of [-1,1]) tuck(sd,y0,y1,w0,w1,0.21,[sd*0.3,0,1]); }
      // riser where the cambered foredeck meets the flat side decks: closes the step at foreFrom
      const yf=spec.foreFrom, wf=Math.max(0.01,fSx(yf)-spec.railW), zc=(t)=>fSz(yf)+0.012+fCam(yf)*(1-t*t);
      for(let k=0;k+1<XF.length;k++) for(const sd of [-1,1]) Q([[sd*wf*XF[k],yf,fSz(yf)-0.03],[sd*wf*XF[k+1],yf,fSz(yf)-0.03],[sd*wf*XF[k+1],yf,zc(XF[k+1])],[sd*wf*XF[k],yf,zc(XF[k])]],[0,-1,0],'cream',-0.4,0); }
    at('foredeck','trim');
    { const RY=spec.railY, pt=(y,sd)=>[sd*(fSx(y)-spec.railW*0.6), y, fSz(y)+0.01];
      for(const sd of [-1,1]){ let prev=null;
        for(const y of RY){ const p=pt(y,sd), t=[p[0],p[1],p[2]+spec.railH]; tubeQ(p,t,0.028,0.028,'steel',0.1); if(prev) tubeQ(prev,t,0.026,0.026,'steel',0.3); prev=t; }
        const tip=[sd*0.12, yB-0.10, fSz(yB-0.1)+spec.railH*0.85]; tubeQ(prev,tip,0.026,0.026,'steel',0.3); }
      at('foredeck','fit');
      boxQ(-0.20,0.20,yB-1.45,yB-1.05,fSz(yB-1.2)+fCam(yB-1.2),fSz(yB-1.2)+fCam(yB-1.2)+0.22,'steel',-0.8);
      boxQ(-0.06,0.06,yB-0.50,yB+0.10,fSz(yB)+0.02,fSz(yB)+0.09,'steel',-0.4);
      const hz=fSz(spec.hatchY)+fCam(spec.hatchY); boxQ(-0.42,0.42,spec.hatchY-0.38,spec.hatchY+0.38,hz-0.01,hz+0.04,'iron',-0.1,0,'glas'); }

    // ---- BRIDGE DECK SLAB (salon lid; skybridge floor on the 90, flybridge deck on the 53): fascia sweeps down aft ----
    const BR=spec.bridgeSlab, SK=spec.sky||null, STN=spec.station;
    slab(Object.assign({ lv:'bridge', holes:[].concat(SK?[SK.well]:[], STN.hatch?[STN.hatch]:[], spec.flyComp?[spec.flyComp]:[]) }, BR));
    let SKY=null;
    if(SK){
      // ---- SKYBRIDGE (enclosed, the 90) ----
      at('bridge','wall');
      SKY=ringsFrom(SK.rings, SK.zs, { e:2.2, rA:0.6 });
      loftWalls(SKY,'cream',0.04);
      aftGrid(SKY, { open:[SK.door], glaze:[SK.fixed] }, 'cream', 0.04);
      bandOn(SKY, SK.band);
      at('bridge','roof'); crown(SKY[SKY.length-1].P, NC+NAF, SKY[SKY.length-1].z, SK.cam, 'cream', 0.02, 0, false);
      // bridge aft deck (on the slab): rail + settee facing aft
      at('bridge','trim');
      { const y0=BR.yA+0.22, y1=SK.aftY-0.05, xr=BR.W-0.20, z=BR.zTop;
        for(const sd of [-1,1]){ for(const y of [y0,(y0+y1)/2,y1]) tubeQ([sd*xr,y,z],[sd*xr,y,z+0.90],0.026,0.026,'steel',0.1);
          tubeQ([sd*xr,y0,z+0.90],[sd*xr,y1,z+0.90],0.026,0.026,'steel',0.3); tubeQ([sd*xr,y0,z+0.48],[sd*xr,y1,z+0.48],0.02,0.02,'steel',0.3); }
        for(const x of [-1.6,0,1.6]) tubeQ([x,y0,z],[x,y0,z+0.90],0.026,0.026,'steel',0.1);
        tubeQ([-xr,y0,z+0.90],[xr,y0,z+0.90],0.026,0.026,'steel',0.3); tubeQ([-xr,y0,z+0.48],[xr,y0,z+0.48],0.02,0.02,'steel',0.3); }
      at('bridge','fit');
      { const z=BR.zTop, y1=SK.aftY-0.02, xa=-1.5, xb=SK.door.x0-0.25; boxQ(xa,xb,y1-0.60,y1,z,z+0.36,'cream',-0.1); boxQ(xa+0.05,xb-0.05,y1-0.58,y1-0.02,z+0.36,z+0.45,'leath',0.15); boxQ(xa+0.05,xb-0.05,y1-0.14,y1-0.02,z+0.45,z+0.92,'leath',-0.05); }
    }
    // ---- OPEN STATION: the 90's top station on its own slab / the 53's flybridge on the bridge slab ----
    const TP={ zTop: STN.slab ? STN.slab.zTop : BR.zTop }, SLV=STN.lv;
    if(STN.slab) slab(Object.assign({ lv:SLV }, STN.slab));
    at(SLV,'wall');
    const COAM=ringsFrom(STN.coaming.rings, STN.coaming.zs, { e:2.0, rA:0.6 });
    { const RG=COAM; loftWalls(RG,'cream',0.1);
      const T=RG[RG.length-1].P, Ti=T.map((p,j)=>{ const n=pnorm(T,j); return [Math.max(0.02,p[0]-0.13*n[0]), p[1]-0.13*n[1]]; }), zt=RG[RG.length-1].z;
      if(STN.coaming.lens){
        // lens brow: rings shrink both ways, so the top edge slopes down to the nose and down aft. Inner skin
        // 0.11 in, a lip on the top ring, and a cap along each sloping aft edge between the skins.
        const IN=RG.map(r=>({ z:r.z, P:r.P.map((p,j)=>{ const n=pnorm(r.P,j); return [Math.max(0.02,p[0]-0.11*n[0]), p[1]-0.11*n[1]]; }) }));
        for(let k=0;k+1<IN.length;k++){ const A=IN[k], B2=IN[k+1];
          for(let j=0;j+1<NC;j++) for(const sd of [-1,1]){ const n=pnorm(A.P,j);
            Q([[sd*A.P[j][0],A.P[j][1],A.z],[sd*A.P[j+1][0],A.P[j+1][1],A.z],[sd*B2.P[j+1][0],B2.P[j+1][1],B2.z],[sd*B2.P[j][0],B2.P[j][1],B2.z]],[-sd*n[0],-n[1],0.15],'cream',-1.0,0); }
          const j=NC-1; for(const sd of [-1,1]) Q([[sd*RG[k].P[j][0],RG[k].P[j][1],RG[k].z],[sd*RG[k+1].P[j][0],RG[k+1].P[j][1],RG[k+1].z],[sd*B2.P[j][0],B2.P[j][1],B2.z],[sd*A.P[j][0],A.P[j][1],A.z]],[0,-0.7,0.7],'cream',0.35,0); }
        const TI=IN[IN.length-1].P;
        for(let j=0;j+1<NC;j++) for(const sd of [-1,1]) Q([[sd*T[j][0],T[j][1],zt],[sd*T[j+1][0],T[j+1][1],zt],[sd*TI[j+1][0],TI[j+1][1],zt],[sd*TI[j][0],TI[j][1],zt]],[0,0,1],'cream',0.5,0);
      } else
      for(let j=0;j+1<NC;j++) for(const sd of [-1,1]){
        Q([[sd*T[j][0],T[j][1],zt],[sd*T[j+1][0],T[j+1][1],zt],[sd*Ti[j+1][0],Ti[j+1][1],zt],[sd*Ti[j][0],Ti[j][1],zt]],[0,0,1],'cream',0.5,0);
        const n=pnorm(T,j); Q([[sd*Ti[j][0],Ti[j][1],zt],[sd*Ti[j+1][0],Ti[j+1][1],zt],[sd*Ti[j+1][0],Ti[j+1][1],TP.zTop+0.01],[sd*Ti[j][0],Ti[j][1],TP.zTop+0.01]],[-sd*n[0],-n[1],0.2],'cream',-1.1,0); }
      if(STN.deckRail){ at(SLV,'trim'); const Rr=STN.deckRail, P=outline({ z:BR.zTop, yF:BR.yF, yA:BR.yA, W:BR.W, N:BR.N }, { e:BR.e||2.0, rA:BR.rA }), zr=TP.zTop;
        const pts=[]; P.forEach((p,j)=>{ if(p[1]<=Rr.y0){ const n=pnorm(P,j); pts.push([Math.max(0,p[0]-0.12*n[0]), p[1]-0.12*n[1]]); } });
        for(const sd of [-1,1]){ for(let i=0;i+1<pts.length;i++){ const a=pts[i], b=pts[i+1];
            tubeQ([sd*a[0],a[1],zr+Rr.h],[sd*b[0],b[1],zr+Rr.h],0.026,0.026,'steel',0.3); tubeQ([sd*a[0],a[1],zr+Rr.h*0.5],[sd*b[0],b[1],zr+Rr.h*0.5],0.02,0.02,'steel',0.3); }
          pts.forEach((p,i)=>{ if(i%4===0||i===pts.length-1) tubeQ([sd*p[0],p[1],zr],[sd*p[0],p[1],zr+Rr.h],0.024,0.024,'steel',0.1); }); } } }
    at(SLV,'fit');
    { const C=STN.console, z=TP.zTop; boxQ(-C.hx,C.hx,C.y0,C.y1,z,z+0.86,'cream',0.1);
      Q([[-C.hx+0.08,C.y0+0.05,z+0.87],[C.hx-0.08,C.y0+0.05,z+0.87],[C.hx-0.08,C.y1-0.12,z+0.95],[-C.hx+0.08,C.y1-0.12,z+0.95]],[0,-0.3,1],'glas',-0.4,0.02);
      for(const x of STN.seats) { boxQ(x-0.08,x+0.08,C.y0-0.95,C.y0-0.79,z,z+0.46,'steel',-0.2); boxQ(x-0.27,x+0.27,C.y0-1.10,C.y0-0.62,z+0.46,z+0.58,'leath',0.1); boxQ(x-0.26,x+0.26,C.y0-1.12,C.y0-0.98,z+0.58,z+1.02,'leath',-0.1); }
      (STN.settees||[]).forEach(s=>{ boxQ(s.x0,s.x1,s.y0,s.y1,z,z+0.36,'cream',-0.1); boxQ(s.x0+0.02,s.x1-0.02,s.y0+0.02,s.y1-0.02,z+0.36,z+0.45,'leath',0.15);
        if(s.back==='-x') boxQ(s.x0,s.x0+0.14,s.y0,s.y1,z+0.45,z+0.88,'leath',-0.05); if(s.back==='+x') boxQ(s.x1-0.14,s.x1,s.y0,s.y1,z+0.45,z+0.88,'leath',-0.05); }); }
    function ladder(Lg){ for(const sd of [-1,1]) tubeQ([Lg.x+sd*0.22,Lg.y0,Lg.z0],[Lg.x+sd*0.22,Lg.y1,Lg.z1],0.026,0.026,'steel',sd>0?-0.3:0.2);
      for(let k=1;k<Lg.n;k++){ const t=k/Lg.n; tubeQ([Lg.x-0.22,lerp(Lg.y0,Lg.y1,t),lerp(Lg.z0,Lg.z1,t)],[Lg.x+0.22,lerp(Lg.y0,Lg.y1,t),lerp(Lg.z0,Lg.z1,t)],0.018,0.018,'steel',0.3); } }
    if(STN.ladder){ at(SLV,'trim'); ladder(STN.ladder); }
    // the 53: exterior ladder from the mezzanine up through the overhang hatch onto the flybridge
    if(spec.flyLadder){ at('bridge','trim'); ladder(spec.flyLadder);
      const Hh=STN.hatch, z=BR.zTop; at('bridge','wall');
      for(const [x0,x1,y0,y1] of [[Hh.x0-0.05,Hh.x1+0.05,Hh.y0-0.05,Hh.y0],[Hh.x0-0.05,Hh.x1+0.05,Hh.y1,Hh.y1+0.05],[Hh.x0-0.05,Hh.x0,Hh.y0,Hh.y1],[Hh.x1,Hh.x1+0.05,Hh.y0,Hh.y1]])
        boxQ(x0,x1,y0,y1,z-0.12,z+0.05,'cream',0.1); }

    // ---- RIGGING: hardtop on posts, radar, domes, whips, rocket launcher, outriggers ----
    at('rigging','rig');
    { const HT=spec.hardtop;
      slab({ lv:'rigging', rWall:'rig', rTop:'rig', yF:HT.yF, yA:HT.yA, W:HT.W, N:HT.N, rA:HT.rA, zTop:HT.z, zf:[[HT.yA,HT.z-0.10],[HT.yF,HT.z-0.07]], cam:0.08, bF:0.3 });
      at('rigging','rig');
      TG=HTP; (HT.posts||[]).forEach(p=>{ for(const sd of [-1,1]) tubeQ([sd*p[0],p[1],TP.zTop+0.02],[sd*p[0]*0.95,p[1]+p[2],HT.z-0.09],0.045,0.04,'steel',0.1); }); TG=F;
      const RD=spec.radar; tubeQ([0,RD.y,HT.z+0.05],[0,RD.y,HT.z+0.26],0.05,0.05,'steel',0.1); boxQ(-RD.len,RD.len,RD.y-0.08,RD.y+0.08,HT.z+0.26,HT.z+0.38,'cream',0.35);
      spec.domes.forEach(D=>{ tubeQ([D.x,D.y,HT.z+0.05],[D.x,D.y,HT.z+0.14],0.06,0.06,'steel',0); tubeQ([D.x,D.y,HT.z+0.12],[D.x,D.y,HT.z+0.12+D.r*1.2],D.r,D.r,'cream',0.25); tubeQ([D.x,D.y,HT.z+0.12+D.r*1.2],[D.x,D.y,HT.z+0.14+D.r*1.9],D.r,D.r*0.35,'cream',0.4); });
      spec.whips.forEach(w=>tubeQ(w[0],w[1],0.03,0.009,'steel',0.3));
      const RL=spec.rocket; boxQ(-RL.x-0.06,RL.x+0.06,RL.y-0.05,RL.y+0.05,HT.z-0.05,HT.z+0.02,'steel',-0.5);
      for(let k=0;k<RL.n;k++){ const x=-RL.x+2*RL.x*k/(RL.n-1); tubeQ([x,RL.y,HT.z],[x*1.06,RL.y-0.30,HT.z+0.42],0.045,0.045,'iron',0.1); }
      // the 53: tuna tower — four legs from the flybridge deck through the hardtop to a railed platform
      const TW=spec.tower; if(TW){ const T=TW.top; TG=TWF;
        for(const sd of [-1,1]){ tubeQ([sd*TW.feet[0][0],TW.feet[0][1],TP.zTop+0.02],[sd*T.hx,T.y1,T.z-0.04],0.05,0.042,'steel',sd>0?-0.2:0.2);
          tubeQ([sd*TW.feet[1][0],TW.feet[1][1],TP.zTop+0.02],[sd*T.hx,T.y0,T.z-0.04],0.05,0.042,'steel',sd>0?-0.2:0.2);
          tubeQ([sd*T.hx,T.y0,T.z],[sd*T.hx,T.y0,T.z+TW.rail],0.03,0.03,'steel',0.1); tubeQ([sd*T.hx,T.y1,T.z],[sd*T.hx,T.y1,T.z+TW.rail],0.03,0.03,'steel',0.1);
          tubeQ([sd*T.hx,T.y0,T.z+TW.rail],[sd*T.hx,T.y1,T.z+TW.rail],0.028,0.028,'steel',0.3); }
        tubeQ([-T.hx,T.y1,T.z+TW.rail],[T.hx,T.y1,T.z+TW.rail],0.028,0.028,'steel',0.3); tubeQ([-T.hx,T.y0,T.z+TW.rail],[T.hx,T.y0,T.z+TW.rail],0.028,0.028,'steel',0.3);
        boxQ(-T.hx-0.04,T.hx+0.04,T.y0-0.04,T.y1+0.04,T.z-0.07,T.z,'cream',0.2,0,'teak');
        const Pd=TW.pod; boxQ(-Pd.hx,Pd.hx,Pd.y0,Pd.y1,T.z,T.z+0.84,'cream',0.1);
        Q([[-Pd.hx+0.05,Pd.y0+0.04,T.z+0.85],[Pd.hx-0.05,Pd.y0+0.04,T.z+0.85],[Pd.hx-0.05,Pd.y1-0.06,T.z+0.92],[-Pd.hx+0.05,Pd.y1-0.06,T.z+0.92]],[0,-0.3,1],'glas',-0.4,0.02);
        ladder(TW.ladder); TG=F; } }
    { const OR=spec.rig, A=52*DEG, EL=12*DEG, STW=OR.stowed*DEG;
      for(const sd of [-1,1]){ boxQ(sd*OR.x-0.08,sd*OR.x+0.08,OR.y-0.14,OR.y+0.14,OR.z-0.12,OR.z+0.02,'iron',0.15); }
      for(const [name,dirv] of [['out',(s)=>[s*Math.sin(A)*Math.cos(EL), -Math.cos(A)*Math.cos(EL), Math.sin(EL)]],['stowed',(s)=>[s*0.10, -Math.sin(STW), Math.cos(STW)]]]){
        TG=RIGF[name];
        for(const sd of [-1,1]){ const P0=[sd*(OR.x+0.06),OR.y,OR.z], d=v_norm(dirv(sd)), P1=v_add(P0,v_mul(d,OR.len)), PM=v_add(P0,v_mul(d,OR.len*0.42));
          tubeQ(P0,PM,0.065,0.040,'steel',sd>0?-0.3:0.3); tubeQ(PM,P1,0.040,0.012,'steel',sd>0?-0.3:0.3); }
        TG=F;
      } }

    TG=F;
    const r3=(v)=>+(+v).toFixed(3);
    // ==================================== CABIN CONSTANTS (published as HOUSE + loft) ====================================
    const ZS=SA.soleZ, ZC=SA.ceilZ, YDASH=SA.dashY, YAIN=SA.aftY+0.10;
    const BZ=SK?SK.soleZ:BR.zTop, BC=SK?SK.ceilZ:null, YSD=SK?SK.dashY:null, YSA=SK?SK.aftY+0.10:null;
    const BLW=spec.below, CAB=spec.cabin;
    const salIn=(y)=>Math.max(0.05, hwAt(ringAt(SAL,ZS+0.05).P,y)-0.10);
    const skyIn=SK?((y)=>Math.max(0.05, hwAt(ringAt(SKY,BZ+0.05).P,y)-0.10)):null;
    const houseHx=r3(Math.min(salIn(lerp(YAIN,YDASH,0.3)), salIn(lerp(YAIN,YDASH,0.7))));
    const skyHx=SK?r3(Math.min(skyIn(lerp(YSA,YSD,0.3)), skyIn(lerp(YSA,YSD,0.7)))):null;

    // ==================================== INTERIOR (mesh) ====================================
    TG=IF;
    // ZS, ZC, YDASH, YAIN, BZ, BC, YSD, YSA, BLW, salIn and skyIn are the CABIN CONSTANTS above
    const OB={ house:[], bridge:[], below:[], deck:[] };   // walk obstacles (plan rects, already padded)
    const block=(lvl,x0,x1,y0,y1,pad)=>{ const p=pad==null?0.22:pad; OB[lvl].push(rectPoly(x0-p,y0-p,x1+p,y1+p)); };
    // FITTED BOX: |x| is held inside env(y,z) at every height, so an outboard face follows the shell (hull
    // lining, house lining, coaming) instead of cutting through it. Empty levels top and bottom drop out.
    let ENV=null;
    function fitBox(env,x0,x1,y0,y1,z0,z1,mat,b,topMat,nz){
      if(!env){ boxQ(x0,x1,y0,y1,z0,z1,mat,b,0,topMat); return true; }
      nz=nz||8; const Z=[], Lx=[], Rx=[]; let cl=false;
      for(let i=0;i<=nz;i++){ const z=lerp(z0,z1,i/nz), w=Math.min(env(y0,z),env(y1,z),env((y0+y1)/2,z)), l=Math.max(x0,-w), r=Math.min(x1,w);
        if(l>x0+1e-4||r<x1-1e-4) cl=true; Z.push(z); Lx.push(l); Rx.push(r); }
      if(!cl){ boxQ(x0,x1,y0,y1,z0,z1,mat,b,0,topMat); return true; }
      let i0=0, i1=nz; while(i0<=nz && Rx[i0]-Lx[i0]<0.03) i0++; while(i1>=i0 && Rx[i1]-Lx[i1]<0.03) i1--;
      if(i1-i0<1) return false;
      const zb=Z[i0], zt=Z[i1];
      Q([[Lx[i1],y0,zt],[Rx[i1],y0,zt],[Rx[i1],y1,zt],[Lx[i1],y1,zt]],[0,0,1],topMat||mat,b,0);
      Q([[Lx[i0],y0,zb],[Rx[i0],y0,zb],[Rx[i0],y1,zb],[Lx[i0],y1,zb]],[0,0,-1],mat,(b||0)-1.2,0);
      for(const [yy,ny] of [[y0,-1],[y1,1]]){ const P=[]; for(let i=i0;i<=i1;i++) P.push([Rx[i],yy,Z[i]]); for(let i=i1;i>=i0;i--) P.push([Lx[i],yy,Z[i]]); Q(P,[0,ny,0],mat,b,0); }
      for(let i=i0;i<i1;i++){ Q([[Rx[i],y0,Z[i]],[Rx[i],y1,Z[i]],[Rx[i+1],y1,Z[i+1]],[Rx[i+1],y0,Z[i+1]]],[1,0,0],mat,b,0);
        Q([[Lx[i],y0,Z[i]],[Lx[i],y1,Z[i]],[Lx[i+1],y1,Z[i+1]],[Lx[i+1],y0,Z[i+1]]],[-1,0,0],mat,b,0); }
      return true;
    }
    const fb=(x0,x1,y0,y1,z0,z1,mat,b,db,topMat)=>fitBox(ENV,x0,x1,y0,y1,z0,z1,mat,b,topMat);
    // lining loft: inset rings, clipped at the dash plane, split by the band edges → panelling / glass / liner
    function lining(RG, z0, z1, yFront, bd, lowMat){
      const zs=RG.map(r=>r.z).filter(z=>z>z0+0.02&&z<z1-0.02); const Z=[z0].concat(zs,[z1]);
      const RR=Z.map(z=>{ const R=ringAt(RG,z); return { z, P:R.P.map((p,j)=>{ const n=pnorm(R.P,j); return [Math.max(0.02,p[0]-0.10*n[0]), p[1]-0.10*n[1]]; }) }; });
      const lo=bd?mono(bd.lo):null, hi=bd?mono(bd.hi):null;
      for(let k=0;k+1<RR.length;k++){ const A=RR[k], B2=RR[k+1];
        for(let j=0;j+1<NC;j++) for(const sd of [-1,1]){
          let q=[[sd*A.P[j][0],A.P[j][1],A.z],[sd*A.P[j+1][0],A.P[j+1][1],A.z],[sd*B2.P[j+1][0],B2.P[j+1][1],B2.z],[sd*B2.P[j][0],B2.P[j][1],B2.z]];
          if(Math.min(q[0][1],q[1][1],q[2][1],q[3][1])>yFront) continue;
          q=splitBy(q,(p)=>p[1]-yFront)[0]; if(!q) continue;
          const n=pnorm(A.P,j), inw=[-sd*n[0],-n[1],0];
          if(!bd){ bands(q,inw,[[z0+0.92,lowMat,-0.1,0],[Infinity,'whiteg',0.1,0]],0); continue; }
          const ya=Math.min(A.P[j][1],A.P[j+1][1]), yb=Math.max(A.P[j][1],A.P[j+1][1])+1e-3;
          const lin=(f)=>{ const fa=f(ya), fb=f(yb); return (p)=>p[2]-(fa+(fb-fa)*(p[1]-ya)/(yb-ya)); };
          const glassHere = bd && (hi((ya+yb)/2)-lo((ya+yb)/2) > 0.05) && (ya+yb)/2 > bd.yTip;
          if(!glassHere){ bands(q,inw,[[z0+0.92,lowMat,-0.1,0],[Infinity,'whiteg',0.1,0]],0); continue; }
          const [below,rest]=splitBy(q,lin(lo)); if(below) Q(below,inw,lowMat,-0.1,0);
          if(rest){ const [win,above]=splitBy(rest,lin(hi)); if(win) Q(win,inw,'glassd',0.3,0); if(above) Q(above,inw,'whiteg',0.1,0); }
        }
      }
    }
    function aftLining(RG, z0, z1, open, lowMat){
      const zs=[z0,z0+0.92,open.z1,z1].sort((a,b)=>a-b);
      for(let k=0;k+1<zs.length;k++){ const za=zs[k], zb=zs[k+1]; if(zb-za<0.01) continue;
        const ya=ringAt(RG,za).P[NC][1]+0.10, yb2=ringAt(RG,zb).P[NC][1]+0.10, Ea=ringAt(RG,za).P[NC-1][0]-0.10, Eb=ringAt(RG,zb).P[NC-1][0]-0.10;
        const xs=[-1e9,open.x0,open.x1,1e9];
        for(let i=0;i<3;i++){ const xm=(clamp(xs[i],-Ea,Ea)+clamp(xs[i+1],-Ea,Ea))/2; if(i===1 && (za+zb)/2<open.z1) continue;
          Q([[clamp(xs[i],-Ea,Ea),ya,za],[clamp(xs[i+1],-Ea,Ea),ya,za],[clamp(xs[i+1],-Eb,Eb),yb2,zb],[clamp(xs[i],-Eb,Eb),yb2,zb]],[0,1,0],za<z0+0.9?lowMat:'whiteg',za<z0+0.9?-0.2:0.0,0); } }
    }
    function soleStrips(xw, y0, y1, z, mat, holes, n){
      const br=new Set(); for(let i=0;i<=n;i++) br.add(lerp(y0,y1,i/n)); (holes||[]).forEach(h=>{ if(h.y0>y0&&h.y0<y1) br.add(h.y0); if(h.y1>y0&&h.y1<y1) br.add(h.y1); });
      const ys=[...br].sort((a,b)=>a-b);
      for(let i=0;i+1<ys.length;i++){ const ya=ys[i], yb=ys[i+1], ym=(ya+yb)/2, wa=xw(ya), wb=xw(yb); if(wa<0.05&&wb<0.05) continue;
        let segs=[[-1,1]]; (holes||[]).forEach(h=>{ if(ym>h.y0&&ym<h.y1){ const nx=[]; for(const [a,b] of segs){ const wm=(wa+wb)/2, ha=h.x0/wm, hb=h.x1/wm;
          if(hb<=a||ha>=b){ nx.push([a,b]); continue; } if(ha>a) nx.push([a,ha]); if(hb<b) nx.push([hb,b]); } segs=nx; } });
        for(const [a,b] of segs) Q([[a*wa,ya,z],[b*wa,ya,z],[b*wb,yb,z],[a*wb,yb,z]],[0,0,1],mat,i%2?-0.08:0.02,0);
      }
    }
    // furniture kit
    function settee(lvl,x0,x1,y0,y1,z,backSide){ fb(x0,x1,y0,y1,z,z+0.30,'leath',-0.35); fb(x0+0.02,x1-0.02,y0+0.02,y1-0.02,z+0.30,z+0.42,'leath',0.2);
      const t=0.16; if(backSide==='-x') fb(x0,x0+t,y0,y1,z+0.42,z+0.86,'leath',-0.05); if(backSide==='+x') fb(x1-t,x1,y0,y1,z+0.42,z+0.86,'leath',-0.05);
      if(backSide==='-y') fb(x0,x1,y0,y0+t,z+0.42,z+0.86,'leath',-0.05); if(backSide==='+y') fb(x0,x1,y1-t,y1,z+0.42,z+0.86,'leath',-0.05); block(lvl,x0,x1,y0,y1); }
    function table(lvl,x0,x1,y0,y1,z,h,mat){ fb(x0,x1,y0,y1,z+h-0.05,z+h,mat||'teakg',0.25); const cx=(x0+x1)/2, cy=(y0+y1)/2; boxQ(cx-0.10,cx+0.10,cy-0.10,cy+0.10,z,z+h-0.05,'teakg',-0.3); block(lvl,x0,x1,y0,y1,0.18); }
    function stool(lvl,x,y,z){ boxQ(x-0.04,x+0.04,y-0.04,y+0.04,z,z+0.62,'teakg',-0.2); boxQ(x-0.19,x+0.19,y-0.19,y+0.19,z+0.62,z+0.74,'leath',0.2); block(lvl,x-0.19,x+0.19,y-0.19,y+0.19,0.12); }
    function bed(lvl,x0,x1,y0,y1,z,headY){ fb(x0,x1,y0,y1,z,z+0.36,'teakg',-0.2); fb(x0+0.03,x1-0.03,y0+0.03,y1-0.03,z+0.36,z+0.56,'whiteg',0.35);
      const hy=headY>0?y1:y0, sgn=headY>0?-1:1; fb(x0+0.10,(x0+x1)/2-0.05,Math.min(hy,hy+sgn*0.40),Math.max(hy,hy+sgn*0.40),z+0.56,z+0.66,'stone',0.4);
      fb((x0+x1)/2+0.05,x1-0.10,Math.min(hy,hy+sgn*0.40),Math.max(hy,hy+sgn*0.40),z+0.56,z+0.66,'stone',0.4);
      fb(x0,x1,Math.min(hy,hy+sgn*0.08),Math.max(hy,hy+sgn*0.08),z+0.36,z+1.10,'teakg',-0.1); block(lvl,x0,x1,y0,y1,0.18); }
    function wall(lvl,x0,x1,y0,y1,z0,z1,mat){ fb(x0,x1,y0,y1,z0,z1,mat||'whiteg',0.0); block(lvl,x0,x1,y0,y1,0.20); }
    function stairF(x0,x1,y0,y1,z0,z1,n,role){ const run=(y1-y0)/n, up=z1>z0;
      for(let k=0;k<n;k++){ const zt=lerp(z0,z1,(k+1)/n), ya=up?y0+k*run:y0+k*run, yb=up?y1:y0+(k+1)*run;
        at(cLVname, (role&&Math.max(zt,z0)>Math.min(z0,z1)+1.3)?'tall':'fit');
        if(up){ boxQ(x0,x1,ya,yb,Math.min(z0,z1),zt-0.03,'cream',-0.3); boxQ(x0,x1,ya,ya+run+0.01,zt-0.03,zt,'teak',-0.05); }
        else { const zz=lerp(z0,z1,(k+1)/n); boxQ(x0,x1,ya,yb,zz-0.03,zz,'teak',-0.05); boxQ(x0,x1,yb-0.02,yb,zz,lerp(z0,z1,k/n),'cream',-0.3); } } }
    let cLVname='house';

    // generic fit-out list: [kind, ...] — settee x0 x1 y0 y1 back · table x0 x1 y0 y1 h · counter x0 x1 y0 y1 h · tall x0 x1 y0 y1 h
    // upper x0 x1 y0 y1 z0 z1 · stool x y · tv x0 x1 y0 y1 z0 z1 · bed x0 x1 y0 y1 head · stand x0 x1 y0 y1 h · wall x0 x1 y0 y1 h
    function furnish(lvl, list, zAt){
      (list||[]).forEach(it=>{ const k=it[0], a=it, z=zAt(a[3]);
        if(k==='settee'){ at(lvl,'fit'); settee(lvl,a[1],a[2],a[3],a[4],z,a[5]); }
        else if(k==='table'){ at(lvl,'fit'); table(lvl,a[1],a[2],a[3],a[4],z,a[5]); }
        else if(k==='counter'){ at(lvl,'fit'); fb(a[1],a[2],a[3],a[4],z,z+a[5],'teakg',-0.15,0,'stone'); block(lvl,a[1],a[2],a[3],a[4]); }
        else if(k==='stand'){ at(lvl,'fit'); fb(a[1],a[2],a[3],a[4],z,z+a[5],'teakg',0.0,0,'stone'); block(lvl,a[1],a[2],a[3],a[4],0.15); }
        else if(k==='tall'){ at(lvl,'tall'); fb(a[1],a[2],a[3],a[4],z,z+a[5],'teakg',0.0); block(lvl,a[1],a[2],a[3],a[4]); }
        else if(k==='upper'){ at(lvl,'tall'); fb(a[1],a[2],a[3],a[4],z+a[5],z+a[6],'teakg',0.1); }
        else if(k==='stool'){ at(lvl,'fit'); stool(lvl,a[1],a[2],zAt(a[2])); }
        else if(k==='tv'){ at(lvl,'fit'); fb(a[1],a[2],a[3],a[4],z+a[5],z+a[6],'iron',0.1); }
        else if(k==='bed'){ at(lvl,'fit'); bed(lvl,a[1],a[2],a[3],a[4],z,a[5]); }
        else if(k==='wall'){ at(lvl,'part'); wall(lvl,a[1],a[2],a[3],a[4],z,z+a[5]); } });
    }

    // ---- SALON interior (lv house) ----
    cLVname='house';
    const envS=(y,z)=>hwAt(ringAt(SAL,clamp(z,SAL[0].z,SAL[SAL.length-1].z)).P,y)-0.12;
    at('house','floor'); soleStrips(salIn, YAIN, YDASH, ZS, 'carpet', [SA.comp], 30);
    at('house','lining'); lining(SAL, ZS, ZC, YDASH, SA.band, 'teakg'); aftLining(SAL, ZS, ZC, SA.door, 'teakg');
    ENV=envS;
    at('house','part');
    { const w=salIn(YDASH)-0.02; fb(-w,w,YDASH-0.34,YDASH,ZS,ZS+(SA.dashH||0.97),'teakg',-0.15,0,'stone'); block('house',-w,w,YDASH-0.34,YDASH,0.25); }
    furnish('house', SA.fit, ()=>ZS);
    if(SA.head){ const H=SA.head; at('house','part');
      wall('house',H.x1-0.08,H.x1,H.y0,H.y1,ZS,ZS+2.05); wall('house',H.x0,H.doorX0,H.y1-0.08,H.y1,ZS,ZS+2.05); wall('house',H.doorX1,H.x1,H.y1-0.08,H.y1,ZS,ZS+2.05);
      boxQ(H.doorX0,H.doorX1,H.y1-0.08,H.y1,ZS+1.95,ZS+2.05,'whiteg',0.0); block('house',H.x0,H.x1,H.y0,H.y1,0.2);
      at('house','fit'); fb(H.x0+0.05,H.x0+0.50,H.y0+0.10,H.y0+0.55,ZS,ZS+0.44,'whiteg',0.3); fb(H.x0+0.05,H.x0+0.55,H.y0+0.65,H.y1-0.20,ZS,ZS+0.85,'teakg',-0.1,0,'stone'); }
    ENV=null;
    at('house','part');
    { const C=SA.comp; for(const sd of [-1,1]) boxQ(sd>0?C.x1:C.x0-0.06,sd>0?C.x1+0.06:C.x0,C.y0,YDASH-0.34,ZS,ZS+0.92,'teakg',-0.1,0,'stone');
      OB.house.push(rectPoly(C.x0-0.26,C.y0-0.26,C.x0+0.10,YDASH)); OB.house.push(rectPoly(C.x1-0.10,C.y0-0.26,C.x1+0.26,YDASH)); OB.house.push(rectPoly(C.x0+0.05,C.y0+0.30,C.x1-0.05,YDASH)); }
    // companionway down (lv house above, below half)
    cLVname='house'; stairF(SA.comp.x0+0.02,SA.comp.x1-0.02,SA.comp.y0+0.05,SA.comp.y1,ZS,spec.below.soleZ,SA.comp.n||9,false);
    // the 90: stair up to the skybridge + its balustrade wall with the TV
    if(SA.up){ const U=SA.up; cLVname='house'; stairF(U.x0,U.x1,U.y0,U.y1,ZS,BZ,12,true); OB.house.push(rectPoly(U.x0-0.20,U.y0+0.35,U.x1+0.30,U.y1+0.3));
      at('house','part'); wall('house',U.x0-0.10,U.x0,U.y0+0.90,U.y1,ZS,ZS+2.15); at('house','fit');
      boxQ(U.x0-0.14,U.x0-0.10,U.tv0,U.tv1,ZS+0.95,ZS+1.72,'iron',0.1); }
    // the 53 (R2): the ship's ladder from the salon sole up to the flybridge hatch, rails and treads, drawn to the
    // well's lid (ZC − 0.45, the bridge's: above it the well draws the rest, so nothing of the room shows through the
    // closed hatch). It takes the place of the L-settee's aft leg; she climbs it facing aft and steps off aft onto
    // the flybridge (the walk's flyhatch)
    if(spec.flyComp && spec.flyComp.ladder){ const L=spec.flyComp.ladder, zt=ZC-0.46, yAt=(z)=>lerp(L.yFoot,L.yTop,(z-ZS)/(BZ-ZS));
      cLVname='house'; at('house','tall');
      for(const x of [L.x0,L.x1]) tubeQ([x,L.yFoot,ZS],[x,yAt(zt),zt],0.024,0.024,'steel',x<-0.9?0.2:-0.3);
      for(let k=1;k<L.n;k++){ const z=lerp(ZS,BZ,k/L.n); if(z>zt-0.05) break; const y=yAt(z); boxQ(L.x0+0.02,L.x1-0.02,y-0.05,y+0.07,z-0.03,z,'teakg',0.25); }
      OB.house.push(rectPoly(L.x0-0.22,L.yTop-0.22,L.x1+0.22,L.yFoot+0.22)); }

    // ---- SKYBRIDGE interior (lv bridge, the 90) ----
    if(SK){ cLVname='bridge';
      const envK=(y,z)=>hwAt(ringAt(SKY,clamp(z,SKY[0].z,SKY[SKY.length-1].z)).P,y)-0.12;
      at('bridge','floor'); soleStrips(skyIn, YSA, YSD, BZ, 'carpet', [SK.well], 22);
      at('bridge','lining'); lining(SKY, BZ, BC, YSD, SK.band, 'teakg'); aftLining(SKY, BZ, BC, SK.door, 'teakg');
      const J=SK.items; ENV=envK;
      at('bridge','part');
      { const w=skyIn(YSD)-0.02; fb(-w,w,YSD-0.30,YSD,BZ,BZ+0.80,'teakg',-0.15,0,'stone'); block('bridge',-w,w,YSD-0.30,YSD,0.25);
        const Wl=SK.well; fb(Wl.x0-0.06,Wl.x0,Wl.y0,Wl.y1,BZ,BZ+0.92,'teakg',-0.1,0,'stone'); fb(Wl.x0-0.06,Wl.x1,Wl.y0-0.06,Wl.y0,BZ,BZ+0.92,'teakg',-0.1,0,'stone');
        OB.bridge.push(rectPoly(Wl.x0-0.26,Wl.y0-0.26,Wl.x1+0.3,Wl.y1-0.15)); }
      at('bridge','fit');
      { const C=J.console; fb(C.x0,C.x1,C.y0,C.y1,BZ,BZ+0.98,'teakg',-0.1); Q([[C.x0+0.08,C.y0+0.05,BZ+0.99],[C.x1-0.08,C.y0+0.05,BZ+0.99],[C.x1-0.08,C.y1-0.10,BZ+1.08],[C.x0+0.08,C.y1-0.10,BZ+1.08]],[0,-0.3,1],'glas',-0.4,0.02);
        block('bridge',C.x0,C.x1,C.y0,C.y1);
        J.chairs.forEach(x=>{ const y=J.chairY; boxQ(x-0.08,x+0.08,y-0.08,y+0.08,BZ,BZ+0.50,'steel',-0.2); boxQ(x-0.28,x+0.28,y-0.26,y+0.26,BZ+0.50,BZ+0.64,'leath',0.15); boxQ(x-0.27,x+0.27,y-0.30,y-0.14,BZ+0.64,BZ+1.20,'leath',-0.05); block('bridge',x-0.28,x+0.28,y-0.30,y+0.26,0.12); });
        settee('bridge',J.lset.x0,J.lset.x1,J.lset.y0,J.lset.y1,BZ,'-x'); table('bridge',J.tab.x0,J.tab.x1,J.tab.y0,J.tab.y1,BZ,0.70);
        settee('bridge',J.sset.x0,J.sset.x1,J.sset.y0,J.sset.y1,BZ,'+x'); }
      ENV=null; }

    // ---- BELOW (lv below): cabins forward of the engine bulkhead; hull lining; engine room aft ----
    // every piece is held inside the MESH-TRUE hull (halfAtZ) at its own height: soles at their own z,
    // partitions and berths fitted to the lining, the bulkhead to the skin.
    cLVname='below';
    const ZB=BLW.soleZ, ZBF=BLW.fwdZ;
    const belowCeil=(y)=>y<YDASH?ZS-0.12:fSz(y)+fCam(y)-0.12;
    const bIn=(y,z)=>Math.max(0.05, halfAtZ(y,z)-0.14);
    // the lid over the cabins is cambered: near the hull side the foredeck is lower than on the centreline, so
    // every fitted piece is also held under the lid's underside at its own |x| (it used to poke through there)
    const deckHalf=(y,z,m)=>{ m=m==null?0.10:m; if(y<YDASH) return z<=ZS-0.12?9:-1; const zs=fSz(y), c=fCam(y), w=Math.max(0.05,fSx(y)-spec.railW);
      if(z<=zs+0.012-m) return 9; const q=1-(z-zs-0.012+m)/Math.max(0.01,c); return q<=0?-1:w*Math.sqrt(q); };
    const envB=(y,z)=>Math.min(halfAtZ(y,z)-0.16, deckHalf(y,z)), envW=(y,z)=>Math.min(halfAtZ(y,z)-0.14, deckHalf(y,z)), envE=(y,z)=>Math.min(halfAtZ(y,z)-0.03, deckHalf(y,z,0.06));
    const zSole=(y)=>y>=BLW.stepY1-0.01?ZBF:ZB;
    at('below','floor');
    soleStrips((y)=>bIn(y,ZB), BLW.y0, BLW.stepY0, ZB, 'carpet', null, 18);
    soleStrips((y)=>bIn(y,ZBF), BLW.stepY1, BLW.y1, ZBF, 'carpet', null, 10);
    at('below','fit'); ENV=envB;
    fb(-0.55,0.55,BLW.stepY0,BLW.stepY1,ZB,ZBF,'teak',-0.1); fb(-9,-0.55,BLW.stepY0,BLW.stepY1,ZB,ZBF,'teakg',-0.2); fb(0.55,9,BLW.stepY0,BLW.stepY1,ZB,ZBF,'teakg',-0.2);
    at('below','lining');
    { const N=34;
      for(let i=0;i<N;i++){ const y0=lerp(BLW.y0,BLW.y1,i/N), y1=lerp(BLW.y0,BLW.y1,(i+1)/N);
        for(const sd of [-1,1]){ const top0=Math.min(belowCeil(y0),fSz(y0)-0.15), top1=Math.min(belowCeil(y1),fSz(y1)-0.15);
          const P=(y,z)=>[sd*bIn(y,z),y,z], z00=zSole(y0), z10=zSole(y1), zm0=z00+0.90, zm1=z10+0.90;
          Q([P(y0,z00),P(y1,z10),P(y1,zm1),P(y0,zm0)],[-sd,0,0],'teakg',-0.1,0); Q([P(y0,zm0),P(y1,zm1),P(y1,top1),P(y0,top0)],[-sd,0,0.1],'whiteg',0.1,0); } }
      const hw=spec.hullWin; if(hw) for(const sd of [-1,1]){ const P=(y,z)=>[sd*(bIn(y,z)-0.02),y,z]; Q([P(hw.y0+0.2,hw.z0+0.06),P(hw.y1-0.2,hw.z0+0.06+hw.slant*0.5),P(hw.y1-0.2,hw.z1-0.06+hw.slant*0.5),P(hw.y0+0.2,hw.z1-0.06)],[-sd,0,0],'glassd',0.4,0.02); } }
    at('below','part');
    { const eb=BLW.y0; fitBox(envE,-9,9,eb-0.10,eb,Math.min(BLW.engZ,ZB),ZS-0.12,'whiteg',-0.1,null,10); OB.below.push(rectPoly(-4,eb-1,4,eb+0.22));
      BLW.walls.forEach(W2=>{ const z0=zSole(W2[2]), top=Math.min(belowCeil((W2[2]+W2[3])/2), z0+2.05); fitBox(envW,W2[0],W2[1],W2[2],W2[3],z0,top,'whiteg',0.0,null,10); OB.below.push(rectPoly(W2[0]-0.20,W2[2]-0.20,W2[1]+0.20,W2[3]+0.20)); }); }
    // engine room (opened by the below cut: the salon lifts off it)
    at('below','floor'); { const ez=BLW.engZ; soleStrips((y)=>Math.max(0.05,halfAtZ(y,ez)-0.05), MZ.y0+0.1, BLW.y0-0.1, ez, 'iron', null, 8); }
    at('below','fit'); { const E=BLW.eng, ez=BLW.engZ;
      for(const sd of [-1,1]){ const xc=sd*E.xc;
        fb(xc-E.hw,xc+E.hw,E.y0,E.y1,ez,ez+E.h,'steel',-0.1,0,'iron'); fb(xc-E.hw*0.62,xc+E.hw*0.62,E.y0+0.3,E.y1-0.3,ez+E.h,ez+E.h+E.ht,'steel',0.25);
        fb(xc-0.20,xc+0.20,E.gear[0],E.gear[1],ez+0.20,ez+E.h*0.8,'iron',-0.2); }
      (E.gens||[]).forEach(g=>fb(g[0],g[1],g[2],g[3],ez,ez+g[4],'cream',-0.2));
      if(E.box) fb(E.box[0],E.box[1],E.box[2],E.box[3],ez,ez+E.box[4],'steel',-0.2); }
    BLW.beds.forEach(b2=>{ at('below','fit'); bed('below',b2[0],b2[1],b2[2],b2[3],zSole(b2[2]),b2[4]); });
    BLW.stands.forEach(s2=>{ at('below','fit'); fb(s2[0],s2[1],s2[2],s2[3],ZB,ZB+0.55,'teakg',0.0,0,'stone'); block('below',s2[0],s2[1],s2[2],s2[3],0.15); });
    furnish('below', BLW.fit, zSole);
    ENV=null;
    { const C=SA.comp; OB.below.push(rectPoly(C.x0-0.05,C.y0-0.25,C.x1+0.05,C.y1-0.15)); }
    TG=F;

    // ==================================== WALK DATA ====================================
    // areas: plan polygons (player centre, already shrunk/padded), z constant or a ramp; cut = which cutaway applies
    const WALK=(function(){
      const A=[], side=(fn,y0,y1,n,shr)=>{ const P=[], Pm=[]; for(let i=0;i<=n;i++){ const y=lerp(y0,y1,i/n), w=Math.max(0.05,fn(y)-shr); P.push([w,y]); Pm.unshift([-w,y]); } return P.concat(Pm); };
      A.push({ id:'cockpit', cut:'deck', z:CZ, poly:side((y)=>xin(y)-0.02,yT+0.35,CF-0.02,8,0.25),
        holes:[rectPoly(-0.62,spec.chair.y-0.95,0.62,spec.chair.y+0.62), rectPoly(ST.x0-0.25,ST.y0+0.22,ST.x1+0.4,CF+0.1), rectPoly(-ST.x1-0.4,ST.y0+0.22,-ST.x0+0.25,CF+0.1)].concat(spec.tubs.map(t=>rectPoly(t[0]-0.50,t[1]-0.45,t[0]+0.50,t[1]+0.45))) });
      for(const sd of [-1,1]) A.push({ id:'sidestair'+(sd>0?'S':'P'), cut:'deck', ramp:{ ax:1, a0:ST.y0, a1:CF, z0:CZ, z1:MZ.z }, poly:rectPoly(sd>0?ST.x0+0.04:-ST.x1+0.04, ST.y0-0.10, sd>0?ST.x1-0.04:-ST.x0-0.04, CF+0.12), holes:[] });
      const FL=spec.flyLadder, sHole=(s,p)=>rectPoly(s.x0-p,s.y0-p,s.x1+p,s.y1+p);
      A.push({ id:'mezzanine', cut:'deck', z:MZ.z, poly:side((y)=>mzw(y),MZ.y0+0.02,MZ.y1-0.22,3,0.25),
        holes:[sHole(MZ.seat,0.25)].concat(FL?[rectPoly(FL.x-0.45,FL.y0,FL.x+0.45,MZ.y1+0.2)]:[]) });
      A.push({ id:'door', cut:'house', z:ZS, poly:rectPoly(SA.door.x0+0.26,MZ.y1-0.30,SA.door.x1-0.26,YAIN+0.30), holes:[] });
      A.push({ id:'salon', cut:'house', z:ZS, poly:side(salIn,YAIN+0.24,YDASH-0.30,24,0.26), holes:OB.house.slice() });
      if(SA.up) A.push({ id:'upstair', kind:'stair', cut:'house', cutHi:'bridge', zMid:ZS+(BZ-ZS)*0.52, ramp:{ ax:1, a0:SA.up.y0, a1:SA.up.y1, z0:ZS, z1:BZ }, poly:rectPoly(SA.up.x0+0.12,SA.up.y0-0.25,SA.up.x1-0.10,SA.up.y1+0.25), holes:[] });
      A.push({ id:'companionway', kind:'stair', cut:'house', cutLo:'below', zMid:(ZS+ZB)/2, ramp:{ ax:1, a0:SA.comp.y0+0.05, a1:SA.comp.y1, z0:ZS, z1:ZB }, poly:rectPoly(SA.comp.x0+0.14,SA.comp.y0-0.20,SA.comp.x1-0.14,SA.comp.y1+0.35), holes:[] });
      if(SK){
        A.push({ id:'skybridge', cut:'bridge', z:BZ, poly:side(skyIn,YSA+0.24,YSD-0.28,18,0.26), holes:OB.bridge.slice() });
        A.push({ id:'skydoor', cut:'bridge', z:BZ, poly:rectPoly(SK.door.x0+0.24,SK.aftY-0.40,SK.door.x1-0.24,YSA+0.30), holes:[] });
        A.push({ id:'aftdeck', cut:'deck', z:BR.zTop, poly:rectPoly(-(BR.W-0.50),BR.yA+0.48,BR.W-0.50,SK.aftY-0.05), holes:[rectPoly(-1.75,SK.aftY-0.85,SK.door.x0,SK.aftY+0.5)] });
      }
      if(FL){
        // the 53: mezzanine → flybridge ladder (steep ramp) and the open flybridge deck (the rigging lifts off)
        A.push({ id:'flyladder', kind:'ladder', cut:'deck', cutHi:'fly', zMid:MZ.z+(BZ-MZ.z)*0.55, ramp:{ ax:1, a0:FL.y0, a1:FL.y1, z0:MZ.z, z1:BZ }, poly:rectPoly(FL.x-0.16,FL.y0-0.18,FL.x+0.16,FL.y1+0.22), holes:[] });
        // R2, her way down: the ship's ladder through the hatch over the salon. Her centre keeps the walk's radius
        // inside the hatch's sides (0.55 m less 2 x 0.25: 0.05 m across); the ramp lifts her from the sole, in front of
        // the ladder's foot, to the bridge deck, where she steps off aft of the hatch
        const FC=spec.flyComp, WR=0.25;
        if(FC && FC.ladder) A.push({ id:'flyhatch', kind:'ladder', cut:'house', cutHi:'fly', zMid:ZS+(BZ-ZS)*0.55,
          ramp:{ ax:1, a0:FC.y1-WR, a1:FC.y0+WR, z0:ZS, z1:BZ }, poly:rectPoly(FC.x0+WR,FC.y0-0.25,FC.x1-WR,FC.y1-WR+0.20), holes:[] });
        const C=STN.console, R0=COAM[0].P, Hh=STN.hatch, SP=outline({ z:BR.zTop, yF:BR.yF, yA:BR.yA, W:BR.W, N:BR.N }, { e:BR.e||2.0, rA:BR.rA });
        const flyIn=(y)=>Math.max(0.05, y>R0[NC][1]+0.05 ? hwAt(R0,y)-0.15 : hwAt(SP,y)-0.20);
        const holes=[rectPoly(-C.hx-0.24,C.y0-1.36,C.hx+0.24,C.y1+0.24), rectPoly(Hh.x0-0.30,Hh.y0-0.30,Hh.x1+0.30,Hh.y1+0.02)].concat((STN.settees||[]).map(s=>sHole(s,0.24)));
        if(FC) holes.push(rectPoly(FC.x0-0.10,FC.y0-0.10,FC.x1+0.10,FC.y1+0.10));
        if(spec.tower) spec.tower.feet.forEach(f=>{ for(const sd of [-1,1]) holes.push(rectPoly(sd*f[0]-0.26,f[1]-0.26,sd*f[0]+0.26,f[1]+0.26)); });
        A.push({ id:'flybridge', cut:'fly', z:BZ, poly:side(flyIn,BR.yA+0.36,C.y1+0.30,22,0.26), holes });
      }
      A.push({ id:'below', cut:'below', z:ZB, poly:side((y)=>bIn(y,ZB+0.10),BLW.walkY0!=null?BLW.walkY0:BLW.y0+0.24,BLW.stepY0+0.05,16,0.24), holes:OB.below.slice() });
      A.push({ id:'belowstep', cut:'below', ramp:{ ax:1, a0:BLW.stepY0, a1:BLW.stepY1, z0:ZB, z1:ZBF }, poly:rectPoly(-0.42,BLW.stepY0-0.1,0.42,BLW.stepY1+0.1), holes:[] });
      A.push({ id:'vip', cut:'below', z:ZBF, poly:side((y)=>bIn(y,ZBF+0.10),BLW.stepY1-0.05,BLW.y1-0.30,10,0.24), holes:OB.below.slice() });
      A.forEach(a=>{ a.zAt=a.ramp?((x,y)=>{ const c=a.ramp.ax?y:x; return lerp(a.ramp.z0,a.ramp.z1,clamp01((c-a.ramp.a0)/(a.ramp.a1-a.ramp.a0))); }):(()=>a.z);
        a.cutAt=(z)=> a.cutHi&&z>a.zMid ? a.cutHi : (a.cutLo&&z<a.zMid ? a.cutLo : a.cut);
        a.contains=(x,y)=>pinPoly([x,y],a.poly) && !a.holes.some(h=>pinPoly([x,y],h)); });
      return { areas:A, spawn:{ x:spec.spawn[0], y:spec.spawn[1], area:'salon' }, radius:0.25, stepTol:0.34 };
    })();

    // ==================================== THE FLYBRIDGE COMPANIONWAY (the 53, ruling R2) ====================================
    // Her way down from the flybridge: a hatch through the bridge deck and the salon overhead, over the stair the
    // interiors kit builds (HOUSE.stairs.up). From above it reads as a framed dark well with the top treads.
    if(spec.flyComp){ const C=spec.flyComp, z=BR.zTop, zw=ZC-0.45;
      at('bridge','wall');
      for(const [x0,x1,y0,y1] of [[C.x0-0.05,C.x1+0.05,C.y0-0.05,C.y0],[C.x0-0.05,C.x1+0.05,C.y1,C.y1+0.05],[C.x0-0.05,C.x0,C.y0,C.y1],[C.x1,C.x1+0.05,C.y0,C.y1]])
        boxQ(x0,x1,y0,y1,z-0.10,z+0.07,'cream',0.1);
      Q([[C.x0,C.y0,zw],[C.x1,C.y0,zw],[C.x1,C.y0,z-0.10],[C.x0,C.y0,z-0.10]],[0,1,0],'cream',-1.0,0);
      Q([[C.x0,C.y1,zw],[C.x1,C.y1,zw],[C.x1,C.y1,z-0.10],[C.x0,C.y1,z-0.10]],[0,-1,0],'cream',-1.2,0);
      Q([[C.x0,C.y0,zw],[C.x0,C.y1,zw],[C.x0,C.y1,z-0.10],[C.x0,C.y0,z-0.10]],[1,0,0],'cream',-1.1,0);
      Q([[C.x1,C.y0,zw],[C.x1,C.y1,zw],[C.x1,C.y1,z-0.10],[C.x1,C.y0,z-0.10]],[-1,0,0],'cream',-1.3,0);
      Q([[C.x0,C.y0,zw],[C.x1,C.y0,zw],[C.x1,C.y1,zw],[C.x0,C.y1,zw]],[0,0,1],'dark',-0.4,0);
      for(let k=1;k<=2;k++){ const zt=z-0.12-k*0.24, y=lerp(C.y0,C.y1,k/3); boxQ(C.x0+0.03,C.x1-0.03,y-0.06,y+0.06,zt-0.03,zt,'teak',-0.3); }
    }

    // ==================================== CLEATS (drawn, and published for the sidecar) ====================================
    at('hull','trim');
    const CLEAT_AT=[];
    function cleat(id, x, y, z, along){ const a=along==='y', hx=a?0.035:0.11, hy=a?0.11:0.035;
      boxQ(x-hx*0.45,x+hx*0.45,y-hy*0.45,y+hy*0.45,z,z+0.04,'steel',-0.1);
      boxQ(x-hx,x+hx,y-hy,y+hy,z+0.04,z+0.065,'steel',0.25);
      CLEAT_AT.push({ id, type:'cleat', pos:[r3(x),r3(y),r3(z+0.065)] }); }
    { const yq=yT+0.45, ys=CF-0.40, yb=yB-1.85;
      for(const [nm,sd] of [['port',-1],['star',1]]){
        cleat('stern_'+nm, sd*(fSx(yq)-capW(yq)*0.5), yq, fSz(yq)+0.012, 'y');
        cleat('spring_'+nm, sd*(fSx(ys)-capW(ys)*0.5), ys, fSz(ys)+0.012, 'y'); }
      cleat('bow_1', 0, yb, fSz(yb)+fCam(yb)+0.012, 'y'); }

    // ==================================== DOORS: posed leaves, built per call ====================================
    // Salon slider on both hulls; the 90 also has the skylounge slider onto her bridge aft deck. Each leaf rides
    // its own raked aft wall toward its fixed pane; behind the opening a dim quad stands in for the room.
    // doorFacesFn(t, which): both leaves at t (render's picture), or one by its door id ('salon', 'skylounge').
    const DS=SA.door, DK=SK?SK.door:null;
    function doorFacesFn(t, which){
      t=clamp01(t==null?0:+t); const saveTG=TG, saveLV=cLV, saveRO=cRO, out=[]; TG=out;
      const leaf=(ds, yAt, fixed, lv)=>{ at(lv, 'wall');
        const sl=ds.slide||1, span=(ds.x1-ds.x0)*0.98, z0=ds.z0-0.04, z1=ds.z1+0.04;
        const n0=out.length;
        Q([[ds.x0,yAt(ds.z0)+0.16,ds.z0],[ds.x1,yAt(ds.z0)+0.16,ds.z0],[ds.x1,yAt(ds.z1)+0.16,ds.z1],[ds.x0,yAt(ds.z1)+0.16,ds.z1]],[0,-1,0.05],'dark',-1.3,-0.02);
        if(out.length>n0) out[n0].standIn=true;   // the dim quad that stands in for the room: dropped whenever the rooms draw
        const lx0=ds.x0-0.06+sl*t*span, lx1=ds.x1+0.06+sl*t*span;
        Q([[lx0,yAt(z0)-0.012,z0],[lx1,yAt(z0)-0.012,z0],[lx1,yAt(z1)-0.012,z1],[lx0,yAt(z1)-0.012,z1]],[0,-1,0.05],'iron',-0.1,0.03);
        Q([[lx0+0.08,yAt(z0)-0.03,z0+0.08],[lx1-0.08,yAt(z0)-0.03,z0+0.08],[lx1-0.08,yAt(z1)-0.03,z1-0.08],[lx0+0.08,yAt(z1)-0.03,z1-0.08]],[0,-1,0.05],'glas',-0.45,0.04);
        const zt=ds.z1+0.07, xa=Math.min(ds.x0,fixed.x0)-0.06, xb=Math.max(ds.x1,fixed.x1)+0.06;
        tubeQ([xa,yAt(zt)-0.05,zt],[xb,yAt(zt)-0.05,zt],0.022,0.022,'steel',0.25); };
      if(which!=='skylounge') leaf(DS, (z)=>salonAft(z)-0.012, SA.fixed, 'house');
      if(DK && which!=='salon') leaf(DK, (z)=>ringAt(SKY,Math.min(z,SKY[SKY.length-1].z)).P[NC][1]-0.012, SK.fixed, 'bridge');
      TG=saveTG; cLV=saveLV; cRO=saveRO; return out;
    }
    const mkDoor=(ds, y, sill, fixed)=>({ kind:'slide', face:'aft', y:r3(y), x0:ds.x0, x1:ds.x1, z0:ds.z0, z1:ds.z1,
      travel:r3((ds.x1-ds.x0)*0.98), slide:(ds.slide||1), clearAt:0.55, sillZ:sill, leaf:{ x0:ds.x0, x1:ds.x1 }, fixed:{ x0:fixed.x0, x1:fixed.x1 } });
    const DOOR=mkDoor(DS, SA.aftY, ZS, SA.fixed), DOOR2=DK?mkDoor(DK, SK.aftY, BZ, SK.fixed):null;

    // ==================================== FACE LISTS + RENDER ====================================
    const _fl={}, TWR=!!spec.tower && spec.towerDefault!==false;
    // F + the outriggers in their pose + EITHER the tuna tower (53, opt-in) or the bare hardtop posts.
    // faceList(riggers, tower) as before, or faceList(state) (hull-layout-1 §4.4): { riggers: 'out' | 'stowed' | true
    // (out) | false (stowed), tower: true | false (the 53) }; a scheme in the state changes no face
    function faceList(riggers, tower){
      if(riggers && typeof riggers==='object'){ const s=riggers; if(s.tower!=null) tower=s.tower; riggers=s.riggers; }
      if(riggers===true) riggers='out'; else if(riggers===false) riggers='stowed';
      const k=RIGF[riggers]?riggers:'stowed', tw=(tower==null?TWR:!!tower)&&TWF.length>0, kk=k+(tw?'+tower':'');
      return _fl[kk]||(_fl[kk]=F.concat(RIGF[k], tw?TWF:HTP)); }
    // ---- THE CUT (ADR 0048 §6.4, hull-layout-1 §4.8) — geometry().cutaway, per hull. A key is where the player
    // stands (every walk area names one). An entry lifts a whole level ('bridge') or one role of a level
    // ('house:roof'). The ghost lifts the levels over the room and its roof (its walls stay and are ghosted near
    // the player); the gate is the lift-set form: the ghost's levels and the shown level's walls and roof. shows
    // names the level whose room renderCut draws at that key.
    const CUTAWAY = SK ? {
      ghost:{ deck:[], house:['bridge','top','rigging','house:roof'], bridge:['top','rigging','bridge:roof'], below:['foredeck','house','bridge','top','rigging'] },
      gate: { deck:[], house:['bridge','top','rigging','house:wall','house:roof'], bridge:['top','rigging','bridge:wall','bridge:roof'], below:['foredeck','house','bridge','top','rigging'] },
      shows:{ deck:null, house:'house', bridge:'bridge', below:'below' } } : {
      ghost:{ deck:[], house:['bridge','rigging','house:roof'], fly:['rigging'], below:['foredeck','house','bridge','rigging'] },
      gate: { deck:[], house:['bridge','rigging','house:wall','house:roof'], fly:['rigging'], below:['foredeck','house','bridge','rigging'] },
      shows:{ deck:null, house:'house', fly:null, below:'below' } };
    const CUT_NOTE='Keys: deck (cockpit, mezzanine, side stairs'+(SK?', the aft deck':', the fly ladder')+'), house (the salon), '+(SK?'bridge (the skylounge)':'fly (the open flybridge)')+
      ', below (the cabins). The gate lists are complete lift sets in the ghost’s vocabulary (whole levels, and level:role): the ghost’s levels, and the shown level’s walls and roof (ADR 0048 §6.4). '+
      'The ghost lifts the levels and the shown level’s roof; its walls stay and come back as ghost round the player. '+(SK?'':'This hull has no top level. ')+
      'renderCut draws the gate: the lifted faces dropped, the shown level’s room on top, front faces only.';
    const GHOST_RULE='role ∈ ghostRoles && depth < playerDepth − 0.20 m && inside the reveal ellipse (rx 1.25 m, ry 1.10 m of screen at 32 px/m, centred 0.95 m above the feet) → discard; solid < 0.60 R, Bayer 4×4 dither 0.60–1.0 R';
    function cutTest(list){
      if(!list || !list.length) return null;
      const whole=new Set(), part=new Set();
      list.forEach(s=>{ const k=s.indexOf(':'); if(k<0) whole.add(LV[s]); else part.add(LV[s.slice(0,k)]+':'+RO[s.slice(k+1)]); });
      return (f)=>whole.has(f.lv) || part.has(f.lv+':'+f.ro);
    }
    const _cut={};
    function cutOf(mode, key){ const k=mode+'|'+key; if(!(k in _cut)) _cut[k]=cutTest((CUTAWAY[mode]||CUTAWAY.ghost)[key]); return _cut[k]; }
    // what a cut view draws: the exterior, the posed leaves (less the stand-in, when the rooms draw) and the rooms
    function allFaces(opts){ opts=opts||{}; const room=opts.interior!==false, door=doorFacesFn(opts.doorOpen);
      return faceList(opts.riggers||'stowed', opts.tower).concat(room?door.filter(f=>!f.standIn):door, room?IF:[]); }
    function ghostParams(B, p, g){ g=g||{}; const c=projVert(p.x,p.y,p.z+0.95,B,G), rx=(g.rx||1.25)*S, ry=(g.ry||1.10)*S, core=g.core!=null?g.core:0.60;
      return { cx:c.sx, cy:c.sy, irx:1/rx, iry:1/ry, dP:c.d-(g.margin!=null?g.margin:0.20), core, icore:1/(1-core), rx, ry }; }
    // one cut (or tag) view: cw = { mode:'ghost'|'gate', key (v3: level), player:{x,y,z}, ghost:{rx,ry,core,margin} }.
    // The gate is gatePaint (renderCut's picture); the ghost draws the exterior, the leaves and every room not
    // lifted in true depth, and with a player opens the near ghost-role faces round her.
    function cutPaint(dir, opts, cw, gp, clip, post){
      if(cw && cw.mode==='gate' && !opts.tags) return gatePaint(dir, cw.key||cw.level||'deck', opts, clip, post);
      const o=Object.assign({}, opts, { dir }), B=camBasis(o);
      let faces=cw ? allFaces(opts) : faceList(opts.riggers||'stowed', opts.tower).concat(doorFacesFn(opts.doorOpen));
      const cut=cw ? cutOf(cw.mode||'ghost', cw.key||cw.level||'deck') : null; if(cut) faces=faces.filter(f=>!cut(f));
      o._ghost=gp || (cw && cw.player ? ghostParams(B, cw.player, cw.ghost) : null);
      if(clip){ o._clip=clip; o._post=post||clip; }
      let MT=cw ? cutTable(opts.scheme||opts.paint) : paintTable(opts.scheme||opts.paint);
      if(opts.tags){ const k=opts.tags; MT=tagMats(k);
        faces=faces.map(f=>({ v:f.v, mat:String(k==='ro'?RON[f.ro]:(k==='it'?!!f.it:LVN[f.lv])), b:f.b, db:f.db, ro:f.ro })); }
      return _paint(faces, o, G, MT);
    }
    // THE GATE (hull-layout-1 §4.8): every face the key's gate list lifts is dropped; the shown level's room is drawn,
    // its front faces only, moved toward the camera by the hull's bounding diagonal (its own db kept on top);
    // everything else is painted as render paints it. The shown level's stand-in goes whenever a level is shown;
    // opts.interior false leaves the room's faces out and nothing else (the room-pixel count: the pixels that change).
    let _diag=null;
    function hullDiag(){ if(_diag!=null) return _diag; let x0=Infinity, y0=Infinity, z0=Infinity, x1=-Infinity, y1=-Infinity, z1=-Infinity;
      for(const f of faceList('stowed', TWF.length>0)) for(const p of f.v){ if(p[0]<x0) x0=p[0]; if(p[0]>x1) x1=p[0]; if(p[1]<y0) y0=p[1]; if(p[1]>y1) y1=p[1]; if(p[2]<z0) z0=p[2]; if(p[2]>z1) z1=p[2]; }
      return (_diag=Math.hypot(x1-x0, y1-y0, z1-z0)); }
    function gatePaint(dir, key, opts, clip, post){
      opts=opts||{};
      if(!Object.prototype.hasOwnProperty.call(CUTAWAY.gate, key)) throw new Error('renderCut: no cut key '+key);
      const o=Object.assign({}, opts, { dir }), B=camBasis(o), lifted=cutOf('gate', key);
      const shownLv=CUTAWAY.shows[key]!=null ? LV[CUTAWAY.shows[key]] : null, shown=opts.interior!==false ? shownLv : null;
      let ext=faceList(opts.riggers||'stowed', opts.tower).concat(doorFacesFn(opts.doorOpen));
      if(lifted) ext=ext.filter(f=>!lifted(f));
      const room=[];
      if(shownLv!=null) ext=ext.filter(f=>!(f.standIn && f.lv===shownLv));
      if(shown!=null){
        const shift=hullDiag();
        for(const f of IF){ if(f.lv!==shown) continue;
          const rv=f.v.map(p=>projVert(p[0],p[1],p[2],B,G)); let ny=0, nz=0;
          for(let i=0;i<rv.length;i++){ const a=rv[i], b=rv[(i+1)%rv.length]; ny+=(a.zr-b.zr)*(a.xr+b.xr); nz+=(a.xr-b.xr)*(a.yr+b.yr); }
          if(-ny*B.ce+nz*B.se<0) continue;   // turned away from the camera
          room.push(Object.assign({}, f, { db:(f.db||0)+shift })); }
      }
      if(clip){ o._clip=clip; o._post=post||clip; }
      const sch=opts.scheme||opts.paint;
      return _paint(ext.concat(room), o, G, shownLv!=null ? cutTable(sch) : paintTable(sch));
    }
    function renderCut(dir, key, opts){ return _toRGBA(gatePaint(dir, key, typeof opts==='number'?{ elev:opts }:(opts||{})), G.W, G.H); }
    // VIEW (v3's): one heading's camera; base(mode,key) is the whole cut view, cached; region(mode,key,rc,ghost,out)
    // repaints rect rc of an RGBA buffer with the ghost from ghost(p) (it rasters rc+2 and posts rc+1, writes rc)
    function view(dir, opts){
      opts=Object.assign({ doorOpen:1 }, typeof opts==='number'?{ elev:opts }:(opts||{}));
      const B=camBasis(Object.assign({}, opts, { dir })), bases={}, W=G.W, H=G.H;
      const grow=(r,g)=>({ x0:Math.max(0,r.x0-g), y0:Math.max(0,r.y0-g), x1:Math.min(W-1,r.x1+g), y1:Math.min(H-1,r.y1+g) });
      return { dir, W, H, faceCount:allFaces(opts).length,
        project:(x,y,z)=>{ const q=projVert(x,y,z,B,G); return [q.sx,q.sy,q.d]; },
        ghost:(p,g)=>ghostParams(B,p,g),
        base(mode, key){ const k=mode+'|'+key; return bases[k]||(bases[k]=_toRGBA(cutPaint(dir, opts, { mode, key }), W, H)); },
        region(mode, key, rc, ghost, out){
          const r={ x0:Math.max(0,Math.floor(rc.x0)), y0:Math.max(0,Math.floor(rc.y0)), x1:Math.min(W-1,Math.ceil(rc.x1)), y1:Math.min(H-1,Math.ceil(rc.y1)) };
          const col=cutPaint(dir, opts, { mode, key }, ghost||null, grow(r,2), grow(r,1));
          for(let y=r.y0;y<=r.y1;y++) for(let x=r.x0;x<=r.x1;x++){ const i=y*W+x, c=col[i], q=i*4;
            if(!c){ out[q]=out[q+1]=out[q+2]=out[q+3]=0; continue; }
            out[q]=parseInt(c.slice(1,3),16); out[q+1]=parseInt(c.slice(3,5),16); out[q+2]=parseInt(c.slice(5,7),16); out[q+3]=255; }
          return out; } };
    }
    function render(dir, opts){
      opts=(typeof opts==='number')?{elev:opts}:(opts||{});
      const cw=(opts.cutaway && typeof opts.cutaway==='object') ? opts.cutaway : null;
      if(cw || opts.tags) return _toRGBA(cutPaint(dir, opts, cw), G.W, G.H);   // the rig's own cut; the tag review
      let faces=faceList(opts.riggers||'stowed', opts.tower).concat(doorFacesFn(opts.doorOpen));
      if(opts.cullLevels && opts.cullLevels.length){ const cut=new Set(opts.cullLevels.map(l=>typeof l==='number'?l:LV[l])); faces=faces.filter(f=>!cut.has(f.lv)); }   // reference cut (level names or numbers); absent → byte-identical
      if(opts.cutaway && root.BoatCutaway) faces=root.BoatCutaway.filter(faces, RIG, Object.assign({}, opts, {dir}));            // cutaway:true — pass 3's hook (boatCutawayRig.js owns that rule set)
      return _toRGBA(_paint(faces, Object.assign({},opts,{dir}), G, paintTable(opts.scheme||opts.paint)), G.W, G.H);
    }
    const ROCK=spec.ROCK;
    function rockMotion(i, frames){
      frames=frames||ROCK.frames;
      const a=2*Math.PI*(((i%frames)+frames)%frames)/frames;
      return { roll:ROCK.rollA*Math.sin(a), pitch:ROCK.pitchA*Math.sin(a+Math.PI/2), heave:ROCK.heaveA*Math.sin(a) };
    }

    // ==================================== ANCHORS ====================================
    const norm=(opts)=>Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}));
    function proj(p, dir, opts){ const o=norm(opts); o.dir=dir; const q=projVert(p.x,p.y,p.z,camBasis(o),G); return { x:q.sx, y:q.sy }; }
    const A=spec.anchors;
    const RODS=[]; { const RL=spec.rocket, HT=spec.hardtop; for(let k=0;k<RL.n;k++){ const x=-RL.x+2*RL.x*k/(RL.n-1); RODS.push({ x:r3(x*1.06), y:r3(RL.y-0.30), z:r3(HT.z+0.42) }); }
      for(const sd of [-1,1]) spec.capRods.forEach(y=>RODS.push({ x:r3(sd*(xin(y)+capW(y)*0.45)), y:r3(y-0.12), z:r3(fSz(y)+0.24) })); }
    const DOORS={ transom:{ x:(spec.doors.tr[0]+spec.doors.tr[1])/2, y:yT, z:r3(CZ+spec.doors.trH*0.5) }, tuna:{ x:(spec.doors.tu[0]+spec.doors.tu[1])/2, y:yT, z:r3(CZ+spec.doors.tuH*0.5) } };
    const TUBS=spec.tubs.map(t=>({ x:t[0], y:t[1], z:CZ }));
    const NAV={ port:{ x:r3(-(fSx(spec.railY[0])-0.06)), y:spec.railY[0], z:r3(fSz(spec.railY[0])+0.08) }, star:{ x:r3(fSx(spec.railY[0])-0.06), y:spec.railY[0], z:r3(fSz(spec.railY[0])+0.08) },
      stern:{ x:0, y:r3(yT+0.1), z:r3(fSz(yT)+0.12) }, mast:{ x:0, y:spec.radar.y, z:r3(spec.hardtop.z+0.45) } };
    const MEZZ={ x:0, y:r3(MZ.y0+0.30), z:r3(MZ.z+0.44) }, CHAIR={ x:0, y:spec.chair.y, z:r3(CZ+0.58) };
    function doorMount(dir, opts){
      const o=norm(opts); o.dir=dir;
      const B=camBasis(o), t=clamp01(o.doorOpen!=null?+o.doorOpen:0);
      const q=projVert((DOOR.x0+DOOR.x1)/2, DOOR.y, DOOR.sillZ, B, G);
      const le=projVert((DOOR.slide<0?DOOR.x1:DOOR.x0)+DOOR.slide*t*DOOR.travel, DOOR.y, DOOR.sillZ, B, G);
      return { x:q.sx, y:q.sy, lead:{x:le.sx,y:le.sy}, open:t, clear:t>=DOOR.clearAt };
    }

    // ==================================== HOUSE + LOFT (what boatInteriorRig.js builds the rooms from) ====================================
    const HOUSE={ kind:'sport', soleZ:ZS, door:DOOR, door2:DOOR2||undefined,
      levels:SK?['bridge','house','below']:['house','below'],
      decks:{ house:{ soleZ:ZS, ceilZ:ZC, hx:houseHx, y0:r3(YAIN), y1:r3(CAB.houseY1!=null?CAB.houseY1:YDASH), portholes:CAB.portholes, hxAt:()=>houseHx },
              bridge:SK ? { deckId:'bridge_sole', soleZ:BZ, ceilZ:BC, hx:skyHx, y0:r3(YSA), front:CAB.skyFront, frontGlass:CAB.skyFrontGlass,
                            sideGlass:CAB.skySideGlass, aftGlass:CAB.skyAftGlass, hxAt:()=>skyHx }
                        : { soleZ:BZ, external:true },
              below:{ soleZ:BLW.soleZ, ceilZ:CAB.below.ceilZ, y0:CAB.below.y0, y1:CAB.below.y1, hxCap:CAB.below.hxCap } },
      block:{ hx:houseHx, y0:r3(YAIN), y1:r3(CAB.houseY1!=null?CAB.houseY1:YDASH), topZ:ZC },
      helms:CAB.helms, ladders:CAB.ladders, wayDown:CAB.wayDown, mainDeckExternal:true, excluded:CAB.excluded };
    const loft={ station:section, halfAtZ, sheerZ:fSz, sheerX:fSx, camber:fCam, L, DWL, TH:0.06, DECK:CZ, house:HOUSE,
      shade:{ GAIN, BIAS, LN, BAYER, KEY, EDGE:0.30 }, cell:{ W:G.W, H:G.H, cx:G.cx, cy:G.cy, S } };

    // ==================================== LEVELS (cutaway data) ====================================
    const LEVEL_IDS = LV;
    // a level as §8.A names it (sole, ceil — null when open — and kind) beside the fields today's baker reads
    const LVL=(o)=>Object.assign(o, { sole:o.soleZ, ceil:o.ceilingZ, kind:o.ceiling.kind });
    function geometry(){
      const fdY=spec.foreFrom+1, fdZ=r3(fSz(fdY)+fCam(fdY));
      const bridge = SK
        ? { id:'bridge', deck:'bridge_sole', soleZ:BZ, ceilingZ:BC,
            ceiling:{ kind:'hard', lid:null, z:BC, of:'skylounge deckhead (HOUSE.decks.bridge.ceilZ) — the top station on this roof is its own level, top; the hardtop over it is rigging' } }
        : { id:'bridge', deck:'bridge_sole', soleZ:BZ, ceilingZ:null, external:true, y0:r3(COAM[0].P[NC][1]), y1:r3(COAM[0].P[0][1]),
            ceiling:{ kind:'open', lid:null, note:'the open flybridge under the hardtop (rigging); its deck is the salon lid' } };
      const top = SLV==='top'
        ? [{ id:'top', deck:'helm_coaming', soleZ:r3(TP.zTop), ceilingZ:null, ceiling:{ kind:'open', lid:null, note:'the open top station on the skylounge roof, under the hardtop (rigging)' } }]
        : [];
      return {
        schema:'hidden-harbours/hull-geometry@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:spec.id, units:'m',
        frame:FRAME,
        ids:Object.assign({}, LEVEL_IDS), roles:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice(), dwlZ:DWL,
        riggingClass:'rigging — hardtop, radar, domes, whips, rocket launcher, outriggers'+(spec.tower?', tuna tower (opt-in)':'')+': tagged by CLASS; the section drops what stood on a lifted roof',
        levels:[
          { id:'house', deck:'house_sole', soleZ:ZS, ceilingZ:ZC,
            ceiling:{ kind:'hard', lid:null, z:ZC, of:'salon overhead (HOUSE.decks.house.ceilZ) — the crowned roof is tagged house and goes with the room; the bridge deck slab above it is bridge' } },
          bridge,
          { id:'below', deck:'below_sole', soleZ:BLW.soleZ, ceilingZ:CAB.below.ceilZ,
            ceiling:{ kind:'hard', lid:'foredeck', z:CAB.below.ceilZ, of:'salon sole aft of the dash, foredeck forward — the foredeck lifts over the flat only' } },
          { id:'main_deck', deck:'cockpit', soleZ:CZ, ceilingZ:null,
            ceiling:{ kind:'open', lid:null, note:'the cockpit and mezzanine are sky (the bridge deck overhangs the mezzanine)' } },
          { id:'foredeck', deck:'foredeck', soleZ:fdZ, ceilingZ:null,
            ceiling:{ kind:'open', lid:null, note:'the foredeck and side decks are sky; soleZ is the crown one metre forward of foreFrom' } },
        ].concat(top).map(LVL),
        cutaway:{ ghost:JSON.parse(JSON.stringify(CUTAWAY.ghost)), gate:JSON.parse(JSON.stringify(CUTAWAY.gate)),
                  shows:Object.assign({}, CUTAWAY.shows), note:CUT_NOTE, ghostRule:GHOST_RULE },
        door:geoDoor(DOOR, 'salon', 'house'),
        doors:[geoDoor(DOOR, 'salon', 'house')].concat(DOOR2 ? [geoDoor(DOOR2, 'skylounge', 'bridge')] : []),
        parts:[
          { id:'door', group:'doorFaces', door:'salon', lv:'house', ro:'wall', kind:'slide', opensTo:'aft', note:'doorFaces(t): the salon slider, the default door (see door)' },
        ].concat(DOOR2 ? [
          { id:'skydoor', group:'doorFaces', door:'skylounge', lv:'bridge', ro:'wall', kind:'slide', opensTo:'aft',
            note:'doorFaces(t, \'skylounge\'), partFaces(\'skydoor\', t): the skylounge slider onto the aft deck. NEW: a second leaf (doors[1])' },
        ] : []).concat([
          { id:'riggers', group:'state', state:'riggers', lv:'rigging', ro:'rig', note:'riggers true swings the outriggers out; false (the default) stows them' },
        ], TWF.length ? [
          { id:'tower', group:'state', state:'tower', lv:'rigging', ro:'rig', note:'tower true stands the tuna tower on the hardtop; false (the default) draws the bare hardtop posts' },
        ] : []),
        states:Object.assign({ riggers:'true|false (out | stowed, the default; faceList also takes the names)' },
          TWF.length ? { tower:'true|false (the tuna tower, opt-in; the default is the hardtop)' } : {},
          { doorOpen:'0..1 (render option; the leaf is doorFaces(t))' }),
      };
    }
    // a door as geometry() and the walk's house.door give it (hull-layout-1 §6.7): the slider's opening, its leaf
    // (doorFaces), its level and its clear width and height when open
    function geoDoor(D, id, lv){
      const w=r3(D.x1-D.x0), clearW=r3(D.travel-0.06);
      return { id, kind:'slide', face:D.face, y:D.y, x0:D.x0, x1:D.x1, z0:D.z0, z1:D.z1, sillZ:D.sillZ, travel:D.travel, slide:D.slide,
        leaf:id==='salon' ? 'doorFaces(t)' : 'doorFaces(t, \''+id+'\')', lv, ro:'wall', clear:{ width:clearW, height:r3(D.z1-D.z0) }, opensTo:'aft', clearAt:D.clearAt,
        note:'shut until opened; E opens it; t 0..1 slides the leaf '+(D.slide<0?'to port':'to starboard')+' over its fixed pane. The opening is '+w+' m wide; open, '+clearW+' m stands clear.' };
    }

    // ==================================== QA: FIT CHECK, LEAK ====================================
    // FIT CHECK (v3's): every room vertex must sit inside the boat's own shell — the mesh-true hull under the decks,
    // or the salon / skylounge ring lofts above them. Returns the room faces that break out.
    // { tol, faces, vertices, outside, worst, worstAt, list } (hull-layout-1 §4.5, §6.11): worst over the faces out
    // by more than tol, worstAt the first corner of the worst one; list the faces out (lv / ro by name)
    function fitCheck(tol){
      tol=tol==null?0.01:tol; const bad=[]; let worst=0, worstAt=null, nv=0;
      const rooms=[SAL].concat(SKY?[SKY]:[]).map(RG=>({ RG, z0:RG[0].z, z1:RG[RG.length-1].z }));
      const outBy=(p)=>{ const x=Math.abs(p[0]), y=p[1], z=p[2]; let best=Infinity;
        if(y>=yT-1e-3&&y<=yB+1e-3&&z<=fSz(clamp(y,yT,yB))+(y>spec.foreFrom?fCam(y):0)+0.02) best=x-halfAtZ(clamp(y,yT,yB),z);
        for(const R of rooms){ if(z<R.z0-0.02||z>R.z1+0.02) continue; const w=hwAt(ringAt(R.RG,clamp(z,R.z0,R.z1)).P,y); if(w>0) best=Math.min(best,x-w); }
        return best; };
      IF.forEach((f,i)=>{ let m=-Infinity; nv+=f.v.length; f.v.forEach(p=>{ m=Math.max(m,outBy(p)); });
        if(f.lv===LV.below) f.v.forEach(p=>{ const dh=deckHalf(p[1],p[2],0); if(dh<9) m=Math.max(m, dh<0?Infinity:Math.abs(p[0])-dh); });
        if(m>tol){ bad.push({ i, lv:LVN[f.lv], ro:RON[f.ro], mat:f.mat, by:m===Infinity?'no room':+m.toFixed(3), at:f.v[0].map(c=>+c.toFixed(2)) });
          if(m!==Infinity && m>worst){ worst=m; worstAt=f.v[0].map(r3); } } });
      return { tol, faces:IF.length, vertices:nv, outside:bad.length, worst:+worst.toFixed(3), worstAt, list:bad };
    }
    // LEAK: room pixels that show through the closed, uncut exterior — each heading drawn with and without the
    // rooms (door closed, stand-in kept), pixels that differ counted
    function leakPx(opts){
      opts=Object.assign({}, opts||{}, { doorOpen:0 }); const per=[];
      for(let d=0; d<8; d++){ const o=Object.assign({}, opts, { dir:d }), ext=faceList(opts.riggers||'stowed', opts.tower).concat(doorFacesFn(0));
        const a=_paint(ext, o, G, cutTable(opts.scheme||opts.paint)), b=_paint(ext.concat(IF), o, G, cutTable(opts.scheme||opts.paint));
        let n=0; for(let i=0;i<a.length;i++) if(a[i]!==b[i]) n++; per.push(n); }
      return per;
    }

    // ==================================== DECK PLAN (the gameplay extractor reads this, nothing else) ====================================
    // Every polygon below is computed from the constants the mesh above is built from. CCW from +Z.
    function deckPlan(){
      const pp=(x,y)=>[r3(x),r3(y)], NP=16;
      const strip=(fx, y0, y1)=>{ const out=[]; for(let i=0;i<=NP;i++){ const y=lerp(y1,y0,i/NP); out.push(pp(-fx(y),y)); } for(let i=0;i<=NP;i++){ const y=lerp(y0,y1,i/NP); out.push(pp(fx(y),y)); } return out; };
      const ringOf=(P, inset)=>{ const Q2=P.slice(0,NC+NAF+1).map((p,j)=>{ const n=pnorm(P,j); return [Math.max(0,p[0]-inset*n[0]), p[1]-inset*n[1]]; });
        const port=Q2.map(p=>pp(-p[0],p[1])), star=Q2.slice().reverse().map(p=>pp(p[0],p[1])); return port.concat(star); };
      const box=(x0,x1,y0,y1)=>({ x:[r3(x0),r3(x1)], y:[r3(y0),r3(y1)] });
      const D=[];
      // cockpit sole: transom liner to the mezzanine face
      const cw=(y)=>xin(y)-0.01, ST2=spec.sideStair, rise=(MZ.z-CZ)/(ST2.n+1), run=(CF-ST2.y0)/ST2.n;
      D.push({ id:'cockpit', z:CZ, polygon:strip(cw, yT+0.06, CF), notes:{
        chair:{ fp:box(-0.42,0.42,spec.chair.y-0.82,spec.chair.y+0.42), top:1.08 },
        tubs:spec.tubs.map(t=>({ fp:box(t[0]-0.28,t[0]+0.28,t[1]-0.22,t[1]+0.22), top:0.52 })),
        steps:{ x:[[-ST2.x1,-ST2.x0],[ST2.x0,ST2.x1]], y:[r3(ST2.y0),r3(CF)], treads:ST2.n, rise:r3(rise), run:r3(run), to:r3(MZ.z),
          tread_z:Array.from({length:ST2.n},(_,k)=>r3(lerp(CZ,MZ.z,(k+1)/(ST2.n+1)))) },
        riser:{ y:r3(CF), x:[-r3(xin(CF)),r3(xin(CF))], top:r3(MZ.z-CZ) },
        doors:{ transom:{ x:spec.doors.tr, h:spec.doors.trH }, tuna:{ x:spec.doors.tu, h:spec.doors.tuH } } } });
      // mezzanine
      const mw=(y)=>(spec.wings?spec.wings.x0-0.01:xin(y)-0.13);
      D.push({ id:'mezzanine', z:MZ.z, polygon:strip(mw, MZ.y0, MZ.y1), notes:{
        seat:{ fp:box(MZ.seat.x0,MZ.seat.x1,MZ.seat.y0,MZ.seat.y1), top:0.92 },
        wings:spec.wings?{ x:[spec.wings.x0,spec.wings.x1], y:[spec.wings.yA,spec.wings.yF], z:[spec.wings.zA,spec.wings.zF] }:null,
        door:{ x:[DS.x0,DS.x1], y:r3(SA.aftY), sill:ZS } } });
      // side decks (walkable only where the reveal clears 0.35 m)
      const xh=(y)=>Math.min(hwAt(ringAt(SAL,fSz(y)+0.013).P,y)-0.03, fSx(y)-spec.railW-0.02), xo=(y)=>fSx(y)-spec.railW;
      let wmin=9, wmax=0; for(let i=0;i<=24;i++){ const y=lerp(SA.aftY+0.05,spec.foreFrom-0.05,i/24), w=xo(y)-xh(y); wmin=Math.min(wmin,w); wmax=Math.max(wmax,w); }
      const sideOK=wmin>=0.35;
      if(sideOK) for(const [nm,sd] of [['port',-1],['star',1]]){ const out=[], N=12;
        const ya=SA.aftY, yf=spec.foreFrom, z=(y)=>r3(fSz(y)+0.013);
        const inner=[], outer=[]; for(let i=0;i<=N;i++){ const y=lerp(ya,yf,i/N); inner.push([r3(sd*xh(y)),r3(y),z(y)]); outer.push([r3(sd*xo(y)),r3(y),z(y)]); }
        const ring=sd<0 ? outer.slice().reverse().concat(inner) : inner.slice().reverse().concat(outer);
        D.push({ id:'side_deck_'+nm, polygon3d:ring, notes:{ width:[r3(wmin),r3(wmax)] } }); }
      // foredeck: outboard edge on the rail line, crown fCam(y)
      { const L3=[], R3=[], N=18; let yEnd=yB-0.02; while(xo(yEnd)<0.07) yEnd-=0.02;
        for(let i=0;i<=N;i++){ const y=lerp(yEnd,spec.foreFrom,i/N); L3.push([r3(-xo(y)),r3(y),r3(fSz(y)+0.012)]); }
        for(let i=0;i<=N;i++){ const y=lerp(spec.foreFrom,yEnd,i/N); R3.push([r3(xo(y)),r3(y),r3(fSz(y)+0.012)]); }
        const nose=[]; for(let j=0;j<=NN;j++){ const p=ringAt(SAL,fSz(spec.foreFrom)+0.02).P[j]; if(p[1]>=spec.foreFrom) nose.push(p); }
        const noseFp=nose.map(p=>pp(-p[0],p[1])).concat(nose.slice().reverse().map(p=>pp(p[0],p[1])));
        D.push({ id:'foredeck', polygon3d:L3.concat(R3), notes:{ crown:[r3(fCam(spec.foreFrom)),r3(fCam(yEnd))], from:spec.foreFrom, to:r3(yEnd),
          rail:{ y:spec.railY, h:spec.railH }, windlass:{ fp:box(-0.20,0.20,yB-1.45,yB-1.05), top:0.22 }, hatch:{ fp:box(-0.42,0.42,spec.hatchY-0.38,spec.hatchY+0.38), top:0.05 },
          house_nose:noseFp.length>2?noseFp:null } }); }
      // upper decks
      if(SK){
        const P=outline({ z:BR.zTop, yF:BR.yF, yA:BR.yA, W:BR.W, N:BR.N }, { e:BR.e||2.0, rA:BR.rA });
        const y1=SK.aftY-0.02, ring=[]; const pts=P.slice(0,NC+NAF+1).filter(p=>p[1]<=y1); const wAt=(y)=>hwAt(P,y);
        const port=[[-wAt(y1)+0.20,y1]].concat(pts.map(p=>[-(Math.max(0,p[0]-0.20)),Math.max(p[1],BR.yA+0.22)]));
        const star=port.slice().reverse().map(p=>[-p[0],p[1]]);
        port.concat(star).forEach(p=>ring.push(pp(p[0],p[1])));
        D.push({ id:'aft_deck', z:BR.zTop, polygon:ring, notes:{ rail:{ x:r3(BR.W-0.20), y0:r3(BR.yA+0.22), h:0.90 },
          settee:{ fp:box(-1.5,SK.door.x0-0.25,y1-0.60,y1), top:0.92 }, door:{ x:[DK.x0,DK.x1], y:r3(SK.aftY), sill:BZ },
          ladder:STN.ladder?{ x:STN.ladder.x, y:[STN.ladder.y0,STN.ladder.y1], z:[STN.ladder.z0,STN.ladder.z1], rungs:STN.ladder.n }:null } });
        const C=STN.console;
        D.push({ id:'helm_coaming', z:TP.zTop, polygon:ringOf(COAM[0].P,0.13), notes:{ console:{ fp:box(-C.hx,C.hx,C.y0,C.y1), top:0.95 },
          seats:STN.seats.map(x=>({ fp:box(x-0.27,x+0.27,C.y0-1.12,C.y0-0.62), top:1.02 })), coaming_top:r3(COAM[COAM.length-1].z) } });
      } else {
        const C=STN.console;
        D.push({ id:'bridge_sole', z:BR.zTop, polygon:ringOf(COAM[0].P,0.13), notes:{ console:{ fp:box(-C.hx,C.hx,C.y0,C.y1), top:0.95 },
          seats:STN.seats.map(x=>({ fp:box(x-0.27,x+0.27,C.y0-1.12,C.y0-0.62), top:1.02 })),
          settees:(STN.settees||[]).map(s=>({ fp:box(s.x0,s.x1,s.y0,s.y1), top:0.88 })),
          companionway:spec.flyComp?{ fp:box(spec.flyComp.x0,spec.flyComp.x1,spec.flyComp.y0,spec.flyComp.y1), down_to:ZS }:null,
          ladder_hatch:STN.hatch?{ fp:box(STN.hatch.x0,STN.hatch.x1,STN.hatch.y0,STN.hatch.y1), ladder:spec.flyLadder }:null,
          rail:STN.deckRail?{ y0:STN.deckRail.y0, h:STN.deckRail.h }:null, brow_peak:r3(COAM[COAM.length-1].z) } });
      }
      return { hull:{ loa:L, beam:r3(2*Math.max(...spec.beam.map(p=>p[1]))), transomBeam:r3(2*fSx(yT)), dwl:DWL, sole:CZ,
                      sheerAft:r3(fSz(yT)), sheerStem:r3(fSz(yB)), coveringBoard:spec.capW, rail:spec.railW, cockpitTo:CF, foredeckFrom:spec.foreFrom,
                      mezzanine:MZ.z, salonSole:ZS, salonCeiling:ZC, bridgeDeck:BR.zTop, skylounge:SK?BZ:null, topStation:SK?TP.zTop:null, belowSole:BLW.soleZ },
               decks:D, sideReveal:[r3(wmin),r3(wmax)], sideDecksWalkable:sideOK, cleats:CLEAT_AT.slice() };
    }
    function faces(){ return F; }                                  // the static TAGGED mesh (+ RIGF per rigger state); the posed leaves are doorFaces(opts)
    // hull-layout-1 §4.6: doorFaces(t) and doorFaces({ doorOpen }) are the salon slider's leaf, the default door;
    // a second door by its id: doorFaces(t, 'skylounge') (the 90). render() draws every leaf at doorOpen.
    const DOOR_IDS=['salon'].concat(DK?['skylounge']:[]);
    function doorFaces(t, id){
      if(t!=null && typeof t==='object'){ if(id==null && t.door!=null && typeof t.door==='string') id=t.door; t=t.doorOpen; }
      id=id==null?'salon':id; if(DOOR_IDS.indexOf(id)<0) return [];
      return doorFacesFn(t, id);
    }
    // hull-layout-1 §4.4: one separable part's faces. door / skydoor: a leaf at t; riggers: the outriggers in the
    // state's pose; tower: the 53's tuna tower
    function partFaces(id, state){
      if(id==='door') return doorFaces(state);
      if(id==='skydoor') return DK ? doorFaces(state, 'skylounge') : [];
      const s=(state && typeof state==='object') ? state : {};
      if(id==='riggers'){ let r=s.riggers; if(r===true) r='out'; else if(r===false||r==null) r='stowed'; return (RIGF[r]||RIGF.stowed).slice(); }
      if(id==='tower') return TWF.slice();
      return [];
    }

    // ==================================== THE WALK (hull-layout-1 §6, hull-walk@1), written by the rig ====================================
    // walk() is the walk file's own fields, in the spec's order; the kit writer (hull-walk.mjs --write) stamps
    // derivedFromRigSha256 and authoring on it and adds qa.character from the character kit. An area's headroom is its
    // clear height to the lowest surface over it, sampled every 0.05 m: the hardtop, the overhangs of the bridge deck and
    // of the top station, the salon and skylounge deckheads, the dash, and the lid over the cabins (the salon sole aft of
    // the dash, the foredeck forward of it). A climb sees up through the opening it runs through.
    const STAND_CLEAR=1.885;   // character 10.3's tallest crown (1.864 m) and 0.02 m: an area with less headroom waits on duck. Re-check at kit 10.4
    function walkSidecar(){
      const g=geometry(), dp=deckPlan(), HT=spec.hardtop, FC=spec.flyComp||null, Hh=STN.hatch||null, WL=SK?SK.well:null, C0=SA.comp;
      const pt=(p)=>[r3(p[0]),r3(p[1])], P3=(x,y,z)=>({ x:r3(x), y:r3(y), z:r3(z) }), mid=(a,b)=>(a+b)/2, ft=(v)=>String(r3(v));
      // ---- over a point: the lowest surface above z (Infinity under the sky) ----
      const plan=(o)=>({ P:outline({ z:o.zTop, yF:o.yF, yA:o.yA, W:o.W, N:o.N }, { e:o.e||2.0, rA:o.rA }), yA:o.yA, yF:o.yF, uf:mono(o.zf) });
      const pHT=plan({ zTop:HT.z, yF:HT.yF, yA:HT.yA, W:HT.W, N:HT.N, rA:HT.rA, zf:[[HT.yA,HT.z-0.10],[HT.yF,HT.z-0.07]] }), pBR=plan(BR), pTP=STN.slab?plan(STN.slab):null;
      const inP=(S,x,y)=>y>=S.yA && y<=S.yF && Math.abs(x)<=hwAt(S.P,y), inR=(R,x,y)=>x>=R.x0 && x<=R.x1 && y>=R.y0 && y<=R.y1, dashW=salIn(YDASH)-0.02;
      function over(x,y,z,op){
        const ax=Math.abs(x), shut=(k)=>!((op&&op[k])||[]).some(R=>inR(R,x,y)), c=[], inSal=y>=SA.aftY && y<=YDASH && ax<=salIn(y)+0.10;
        if(inP(pHT,x,y)) c.push(pHT.uf(y));                                                  // the hardtop
        if(!inSal && inP(pBR,x,y) && shut('slab')) c.push(pBR.uf(y));                         // the bridge deck's overhang, outside the salon (its aft corners too)
        if(pTP && y<SK.aftY && inP(pTP,x,y)) c.push(pTP.uf(y));                               // the top station's overhang (the 90)
        if(inSal && shut('ceil')) c.push(ZC);                                                  // the salon deckhead
        if(SK && y>=SK.aftY && y<=YSD && ax<=skyIn(y)+0.10) c.push(BC);                       // the skylounge deckhead
        if(y>=YDASH-0.34 && y<=YDASH && ax<=dashW) c.push(ZS);                                 // the dash
        if(y>=YDASH){ const t=Math.min(1, ax/Math.max(0.05, fSx(y)-spec.railW)); c.push(fSz(y)+fCam(y)*(1-t*t)-0.12); }   // the foredeck over the cabins
        else if(y>=BLW.y0-0.10 && shut('lid')) c.push(ZS-0.12);                                // the salon sole over the cabins
        let m=Infinity; for(const v of c) if(v>z+0.05 && v<m) m=v; return m;
      }
      const OPEN={ companionway:{ lid:[C0] } };
      if(FC) OPEN.flyhatch={ ceil:[FC] }; if(Hh) OPEN.flyladder={ slab:[Hh] }; if(WL) OPEN.upstair={ ceil:[WL] };
      const EDGE=[YDASH-0.34, YDASH, SA.aftY, C0.y0, C0.y1, BR.yA, HT.yA, HT.yF].concat(SK?[SK.aftY, WL.y0, WL.y1]:[], STN.slab?[STN.slab.yA]:[], FC?[FC.y0, FC.y1]:[], Hh?[Hh.y0, Hh.y1]:[]);
      function clear(a){
        const xs=a.poly.map(p=>p[0]), ys=a.poly.map(p=>p[1]), x0=Math.min(...xs), x1=Math.max(...xs), y0=Math.min(...ys), y1=Math.max(...ys), X=[], Y=[];
        for(let x=x0; x<x1; x+=0.05) X.push(x); X.push(x1);
        for(let y=y0; y<y1; y+=0.05) Y.push(y); Y.push(y1);
        EDGE.forEach(e=>{ for(const v of [e-0.005, e+0.005]) if(v>y0 && v<y1) Y.push(v); });
        let lo=Infinity, hi=-Infinity, sky=false, loAt=null;
        for(const y of Y) for(const x of X){ if(!a.contains(x,y)) continue; const z=a.zAt(x,y), o=over(x,y,z,OPEN[a.id]);
          if(o===Infinity){ sky=true; continue; } if(o-z<lo){ lo=o-z; loAt=[x,y]; } hi=Math.max(hi,o-z); }
        return { lo, hi, sky, loAt };
      }

      // ---- the areas: the rig's WALK as published, with each one's headroom, waits and note ----
      const HEAD={ door:DOOR.z1-DOOR.sillZ }; if(DOOR2) HEAD.skydoor=DOOR2.z1-DOOR2.sillZ;
      const span=(o)=>o.headroom==null?'open overhead':(o.headroomMax!=null?ft(o.headroom)+'–'+ft(o.headroomMax):ft(o.headroom))+' m clear';
      const duckS=(o)=>o.headroom!=null && o.headroom<STAND_CLEAR?'; under '+ft(STAND_CLEAR)+' m the taller builds wait on duck':'';
      const NOTE={
        cockpit:(o)=>'the open cockpit at '+ft(CZ)+' m'+(o.headroom!=null?'; the bridge deck overhangs its forward end ('+ft(o.headroom)+' m clear)':''),
        sidestairP:()=>'the port steps from the cockpit ('+ft(CZ)+' m) up to the mezzanine ('+ft(MZ.z)+' m): '+ST.n+' treads',
        sidestairS:()=>'the starboard steps from the cockpit ('+ft(CZ)+' m) up to the mezzanine ('+ft(MZ.z)+' m): '+ST.n+' treads',
        mezzanine:(o,h)=>'the mezzanine at '+ft(MZ.z)+' m under the bridge deck’s overhang ('+span(o)+(h.sky?'; open at its aft edge':'')+')'+duckS(o),
        door:(o)=>'the salon doorway: a wall while the slider is shut, passable from doorOpen '+DOOR.clearAt+' (interact salon). Its head is '+ft(HEAD.door)+' m over the sill ('+span(o)+')'+duckS(o),
        salon:(o)=>'the salon at '+ft(ZS)+' m, '+span(o)+' under its deckhead'+duckS(o),
        upstair:(o)=>'the stair in the well from the salon ('+ft(ZS)+' m) up to the skylounge ('+ft(BZ)+' m), 12 treads: going up waits on stairUp, coming down on stairDown ('+span(o)+')'+duckS(o),
        companionway:(o,h)=>'the companionway stair from the salon sole ('+ft(ZS)+' m) down to the cabins ('+ft(ZB)+' m), '+(C0.n||9)+' treads: going down waits on stairDown, coming up on stairUp. The least clear over it is '+ft(o.headroom)+' m, at y '+ft(h.loAt[1])+duckS(o),
        skybridge:(o)=>'the skylounge at '+ft(BZ)+' m, '+span(o)+' under its deckhead'+duckS(o),
        skydoor:(o)=>'the skylounge doorway onto the aft deck: a wall while the slider is shut, passable from its own doorOpen '+(DOOR2?DOOR2.clearAt:'')+' (interact skylounge). Its head is '+ft(HEAD.skydoor||0)+' m over the sill ('+span(o)+')'+duckS(o),
        aftdeck:(o,h)=>'the aft deck on the bridge deck ('+ft(BR.zTop)+' m) under the top station’s overhang ('+span(o)+(h.sky?'; open at its edges':'')+')'+duckS(o),
        flyladder:(o)=>'the outside ladder from the mezzanine ('+ft(MZ.z)+' m) up through the overhang’s hatch to the flybridge ('+ft(BZ)+' m), '+(spec.flyLadder?spec.flyLadder.n:0)+' rungs: it waits on ladderOn ('+span(o)+')'+duckS(o),
        flyhatch:(o)=>'the ship’s ladder from the salon sole ('+ft(ZS)+' m) up through the '+ft(FC?FC.x1-FC.x0:0)+' m hatch to the flybridge ('+ft(BZ)+' m): the walker’s centre has '+ft(FC?FC.x1-FC.x0-2*WALK.radius:0)+' m across between the hatch’s sides; it waits on ladderOn ('+span(o)+')'+duckS(o),
        flybridge:(o,h)=>'the open flybridge on the bridge deck ('+ft(BZ)+' m) under the hardtop ('+span(o)+(h.sky?'; open at its edges':'')+')'+duckS(o),
        below:(o)=>'the cabins’ sole at '+ft(ZB)+' m ('+span(o)+')'+duckS(o),
        belowstep:(o)=>'the step up from the cabins’ sole ('+ft(ZB)+' m) to the forward cabin ('+ft(ZBF)+' m), '+span(o)+duckS(o),
        vip:(o)=>'the forward cabin at '+ft(ZBF)+' m ('+span(o)+')'+duckS(o),
      };
      const areas=WALK.areas.map(a=>{
        const o={ id:a.id }; if(a.kind) o.kind=a.kind; o.cut=a.cut;
        if(a.cutHi) o.cutHi=a.cutHi; if(a.cutLo) o.cutLo=a.cutLo; if(a.zMid!=null) o.zMid=r3(a.zMid);
        if(a.ramp) o.ramp={ axis:a.ramp.ax?'y':'x', a0:r3(a.ramp.a0), a1:r3(a.ramp.a1), z0:r3(a.ramp.z0), z1:r3(a.ramp.z1) }; else o.z=r3(a.z);
        o.poly=a.poly.map(pt); o.holes=a.holes.map(h=>h.map(pt));
        if(HEAD[a.id]!=null) o.door=a.id;                                                       // the part it runs through (geometry().parts)
        const h=clear(a);
        if(h.lo<Infinity){ const lo=HEAD[a.id]!=null?Math.min(h.lo,HEAD[a.id]):h.lo; o.headroom=r3(lo); if(h.hi>lo+0.005) o.headroomMax=r3(h.hi); }
        const w=[].concat(a.kind==='stair'||/^sidestair/.test(a.id)?['stairDown','stairUp']:[], a.kind==='ladder'?['ladderOn']:[], o.headroom!=null && o.headroom<STAND_CLEAR?['duck']:[]);
        if(w.length) o.waitsOn=w;
        if(NOTE[a.id]) o.note=NOTE[a.id](o,h);
        return o; });

      // ---- the house: the door (doors on the 90), the hard levels, and every level's deck ----
      const decks={}; g.levels.forEach(l=>{ decks[l.id]=Object.assign({ deck:l.deck, kind:l.kind, soleZ:l.sole, ceilZ:l.ceil }, l.kind==='open'?{ open:true }:{}); });
      const house=Object.assign({ door:g.door }, DOOR2?{ doors:g.doors }:{}, { levels:g.levels.filter(l=>l.kind==='hard').map(l=>l.id), decks });

      // ---- the berths: each mattress's top as bed() fits it (inside the lining at its own height), its deckhead over it ----
      const fwdY=BLW.beds.reduce((m,b)=>Math.max(m,mid(b[2],b[3])), -Infinity);
      const berths=BLW.beds.map(b=>{
        const zt=r3(zSole(b[2])+0.56), y0=b[2]+0.03, y1=b[3]-0.03, ym=mid(y0,y1);
        const w=Math.min(envB(y0,zt), envB(y1,zt), envB(ym,zt)), x0=Math.max(b[0]+0.03,-w), x1=Math.min(b[1]-0.03,w);
        const vip=mid(b[2],b[3])===fwdY, wide=x1-x0>=1.2, sd=mid(b[0],b[1])<0?'port':'starboard';
        let dh=Infinity; for(let y=y0; y<=y1+1e-9; y+=0.05) for(let x=x0; x<=x1+1e-9; x+=0.05) dh=Math.min(dh, over(x,y,zt));
        return { id:vip?'vip_berth':(wide?'master':'single_'+sd), interact:vip?'bunk_vip':(wide?'bunk':'bunk_'+sd), kind:wide?'double':'single',
                 topZ:zt, head:b[4]>0?'fwd':'aft', deckheadZ:r3(dh), poly:rectPoly(r3(x0),r3(y0),r3(x1),r3(y1)) }; });
      const anchors={ helm:A.helm, towerStation:A.towerStation, chair:CHAIR, mezzSeat:MEZZ, rods:RODS, doors:DOORS, tubs:TUBS, nav:NAV, berths };

      // ---- the interact points: at is the thing; reach stands on the floor of area, at the nearest point 0.02 m inside it ----
      const AR={}; areas.forEach(o=>{ AR[o.id]=o; });
      const zOn=(o,x,y)=>{ if(o.z!=null) return o.z; const R=o.ramp, c=R.axis==='y'?y:x; return r3(lerp(R.z0,R.z1,clamp01((c-R.a0)/(R.a1-R.a0)))); };
      const segD=(p,a,b)=>{ const dx=b[0]-a[0], dy=b[1]-a[1], q=dx*dx+dy*dy, t=q?clamp01(((p[0]-a[0])*dx+(p[1]-a[1])*dy)/q):0; return Math.hypot(p[0]-a[0]-t*dx, p[1]-a[1]-t*dy); };
      const edgeD=(p,P)=>{ let m=Infinity; for(let i=0;i<P.length;i++) m=Math.min(m, segD(p,P[i],P[(i+1)%P.length])); return m; };
      const OFS=[]; for(let j=-80;j<=80;j++) for(let i=-80;i<=80;i++){ const d=Math.hypot(i,j)*0.02; if(d<=1.6) OFS.push([i,j,d]); }
      OFS.sort((a,b)=>a[2]-b[2] || a[1]-b[1] || a[0]-b[0]);
      function reachOn(ids,tx,ty){ let best=null;
        for(const id of ids){ const o=AR[id]; if(!o) continue;
          for(const f of OFS){ if(best && f[2]>=best.d) break; const p=[r3(tx+f[0]*0.02), r3(ty+f[1]*0.02)];
            if(!pinPoly(p,o.poly) || edgeD(p,o.poly)<0.02 || o.holes.some(hl=>pinPoly(p,hl) || edgeD(p,hl)<0.02)) continue;
            best={ d:f[2], reach:{ x:p[0], y:p[1], z:zOn(o,p[0],p[1]) }, area:id }; break; } }
        if(!best) throw new Error('walk '+spec.id+': no reach on '+ids.join('|')+' within 1.6 m of '+r3(tx)+', '+r3(ty));
        return best; }
      const KEYS=['id','action','label','prompt','state','clearAt','at','seat','stand','reach','area','offWalk','waitsOn','note'], I=[], SEATS=[];
      const put=(o)=>{ const r={}; KEYS.forEach(k=>{ if(o[k]!==undefined) r[k]=o[k]; }); I.push(r); return r; };
      const on=(ids,tx,ty,o)=>{ const b=reachOn(ids,tx,ty); return put(Object.assign(o, { reach:b.reach, area:b.area })); };
      const seat=(ids,id,label,at)=>{ SEATS.push({ id, label, at }); on(ids, at.x, at.y, { id, action:'enter_ride', label, prompt:'Sit', at, waitsOn:['sit'] }); };
      const ROOM=['salon'], DOWN=['below','vip','belowstep'], UP=SK?'skybridge':'flybridge', SC=STN.console;
      const helm=on([UP], A.helm.x, A.helm.y, { id:'helm', action:'enter_helm', label:'Helm', prompt:'Take the helm',
        at:SK?P3(0, SK.items.console.y0, BZ+0.98):P3(0, SC.y0, TP.zTop+0.86), seat:A.helm, stand:null,
        note:'taken seated at the '+(SK?'skylounge':'flybridge')+' console, from behind the helm seat. No wheel is drawn (the global has no WHEEL_GEO), so the kit fits no hands to a rim and writes no helm fixture' });
      if(spec.tower){ const T=spec.tower.top;
        put({ id:'helm_tower', action:'enter_helm', label:'Tower helm', prompt:'Take the tower helm', at:A.towerStation, seat:A.towerStation, stand:null,
          reach:P3(0, T.y0+0.35, T.z), area:null, offWalk:'tower_top', waitsOn:['ladderOn'],
          note:'only with the tower up (state tower; the default is the bare hardtop): its top is reached by the tower ladder, which waits on ladderOn' }); }
      if(SLV==='top') put({ id:'helm_top', action:'enter_helm', label:'Top station helm', prompt:'Take the top helm', at:P3(0, SC.y0, TP.zTop+0.86), seat:A.towerStation, stand:null,
          reach:P3(0, SC.y0-1.30, TP.zTop), area:null, offWalk:'top_station', waitsOn:['ladderOn'],
          note:'the open top station, reached by its ladder from the aft deck, which waits on ladderOn; the reach stands behind the station seats' });
      const dAt=(D)=>P3(mid(D.x0,D.x1), D.y, mid(D.z0,D.z1));
      on(['door'], mid(DOOR.x0,DOOR.x1), DOOR.y-0.30, { id:'salon', action:'open_close', label:'Salon door', prompt:'Open · shut', state:'doorOpen', clearAt:DOOR.clearAt, at:dAt(DOOR),
        note:'E opens and shuts it (doorOpen 0..1, doorFaces(t)); the doorway (area door) is passable from doorOpen '+DOOR.clearAt });
      if(DOOR2) on(['skydoor'], mid(DOOR2.x0,DOOR2.x1), DOOR2.y-0.30, { id:'skylounge', action:'open_close', label:'Skylounge door', prompt:'Open · shut', state:'skyDoorOpen', clearAt:DOOR2.clearAt, at:dAt(DOOR2),
        note:'E opens and shuts it (its leaf is doorFaces(t, \'skylounge\'); render() draws both leaves at doorOpen); the doorway (area skydoor) is passable from '+DOOR2.clearAt });
      const fitC=(f)=>[mid(f[1],f[2]), mid(f[3],f[4])], counters=SA.fit.filter(f=>f[0]==='counter'), gal=counters.reduce((m,f)=>f[5]>m[5]?f:m, counters[0]);
      on(ROOM, fitC(gal)[0], fitC(gal)[1], { id:'stove', action:'cook', label:'Galley', prompt:'Cook · brew up', at:P3(fitC(gal)[0], fitC(gal)[1], ZS+gal[5]),
        note:'the galley: the salon’s tallest counter (its worktop at '+ft(gal[5])+' m)' });
      SA.fit.filter(f=>f[0]==='tall').forEach((f,i)=>on(ROOM, fitC(f)[0], fitC(f)[1], { id:i?'locker_'+(i+1):'locker', action:'storage', label:'Hanging locker', prompt:'Store · retrieve', at:P3(fitC(f)[0], fitC(f)[1], ZS+f[5]/2) }));
      (BLW.fit||[]).filter(f=>f[0]==='tall').forEach((f,i)=>on(DOWN, fitC(f)[0], fitC(f)[1], { id:i?'locker_below_'+(i+1):'locker_below', action:'storage', label:'Hanging locker', prompt:'Store · retrieve', at:P3(fitC(f)[0], fitC(f)[1], zSole(f[3])+f[5]/2) }));
      const BL={ vip_berth:'VIP berth', master:'Master berth', single_port:'Port berth', single_starboard:'Starboard berth' };
      berths.forEach(b=>{ const cx=mid(b.poly[0][0],b.poly[2][0]), cy=mid(b.poly[0][1],b.poly[2][1]);
        on(DOWN, cx, cy, { id:b.interact, action:'sleep', label:BL[b.id], prompt:'Sleep · save', at:P3(cx,cy,b.topZ) }); });
      if(SA.head){ const Hd=SA.head; on(ROOM, mid(Hd.doorX0,Hd.doorX1), Hd.y1+0.30, { id:'head', action:'toilet', label:'Head', prompt:'Use the head', at:P3(Hd.x0+0.275, Hd.y0+0.325, ZS+0.44), waitsOn:['sit'],
        note:'the day head: its door faces the settees’ aft ends, so the reach is the nearest free floor' }); }
      on(['cockpit'], CHAIR.x, spec.chair.y-0.95, { id:'fighting_chair', action:'enter_ride', label:'Fighting chair', prompt:'Sit', at:CHAIR, waitsOn:['sit'] }); SEATS.push({ id:'fighting_chair', label:'the fighting chair', at:CHAIR });
      seat(['mezzanine'], 'seat_mezzanine', 'Mezzanine seat', MEZZ);
      const CX=SK?SK.items.chairs.filter(x=>x):STN.seats.filter(x=>x), CY=SK?SK.items.chairY:A.helm.y;
      CX.forEach(x=>seat([UP], 'seat_helm_'+(x<0?'port':'starboard'), 'Helm companion seat', P3(x, CY, A.helm.z)));
      SA.fit.filter(f=>f[0]==='settee').forEach((f,i)=>seat(ROOM, 'seat_salon_'+(i+1), 'Salon settee', P3(fitC(f)[0], fitC(f)[1], ZS+0.42)));
      SA.fit.filter(f=>f[0]==='stool').forEach((f,i)=>seat(ROOM, 'seat_stool_'+(i+1), 'Bar stool', P3(f[1], f[2], ZS+0.74)));
      if(SK){ const J=SK.items; [J.lset, J.sset].forEach((s,i)=>seat(['skybridge'], 'seat_skylounge_'+(i+1), 'Skylounge settee', P3(mid(s.x0,s.x1), mid(s.y0,s.y1), BZ+0.42)));
        seat(['aftdeck'], 'seat_aftdeck', 'Aft deck settee', P3(mid(-1.5, SK.door.x0-0.25), SK.aftY-0.32, BR.zTop+0.45)); }
      else (STN.settees||[]).forEach((s,i)=>seat(['flybridge'], 'seat_flybridge'+(i?'_'+(i+1):''), 'Flybridge settee', P3(mid(s.x0,s.x1), mid(s.y0,s.y1), TP.zTop+0.45)));
      spec.tubs.forEach(t=>on(['cockpit'], t[0], t[1], { id:'tub_'+(t[0]<0?'port':'starboard'), action:'storage', label:'Cockpit tub', prompt:'Store · retrieve', at:P3(t[0], t[1], CZ+0.52) }));
      [['port',spec.rocket.n+1],['starboard',spec.rocket.n+4]].forEach(([s,k])=>{ const r=RODS[k]; on(['cockpit'], r.x, r.y, { id:'rods_'+s, action:'storage', label:'Rod holders', prompt:'Take · stow a rod', at:r,
        note:'the middle of the three rod holders in the '+s+' covering board' }); });
      { const yw=yB-1.25; put({ id:'windlass', action:'anchor', label:'Windlass', prompt:'Let go / weigh anchor', at:P3(0, yw, fSz(yw)+fCam(yw)+0.22), reach:helm.reach, area:helm.area,
        note:'on the foredeck, which is no walk on this hull (see _excluded foredeck): worked from the helm' }); }

      // ---- what is not walked: every other standing surface, seat, berth, climb and door, with its reason ----
      const X=[], ex=(o)=>{ const r={}; ['id','of','kind','z','at','poly','headroom','reason','waitsOn'].forEach(k=>{ if(o[k]!==undefined) r[k]=o[k]; }); X.push(r); };
      const fd=g.levels.find(l=>l.id==='foredeck'), WB=(!SK && dp.sideDecksWalkable)?['port','starboard']:null;
      ex({ id:'foredeck', of:'area', z:fd.sole, reason:'the foredeck forward of the house, its crown at '+ft(fd.sole)+' m: '+(WB?'reached only along the side decks, which are narrower than the walker (side_decks)':'no way there outside, since the house sides stand on the sheer behind the '+ft(spec.railW)+' m rail ('+dp.sideReveal[0]+'–'+dp.sideReveal[1]+' m reveal)')+', and no hatch from below, so no walk area. The windlass is worked from the helm.' });
      if(WB) ex({ id:'side_decks', of:'area', reason:'the side decks beside the house, from the salon door forward: '+dp.sideReveal[0]+'–'+dp.sideReveal[1]+' m between the house and the rail, under the walker’s 2 × '+WALK.radius+' m, so no walk area. They are the washboards (washboards[]).' });
      if(spec.tower){ const T=spec.tower.top;
        ex({ id:'tower_top', of:'area', z:T.z, reason:'the tuna tower’s top, '+ft(2*T.hx)+' × '+ft(T.y1-T.y0)+' m at '+ft(T.z)+' m: opt-in (state tower; the default is the bare hardtop) and reached only by the tower ladder (tower_ladder), so no walk area. Its helm is the helm_tower interact.' }); }
      if(SLV==='top'){ const R0=COAM[0].P; let lo=Infinity;
        for(let y=R0[NC][1]+0.25; y<=R0[0][1]-0.25; y+=0.05){ const w=hwAt(R0,y)-0.25; for(let x=-w; x<=w; x+=0.05){
          if(Math.abs(x)<=SC.hx+0.05 && y>=SC.y0-0.05 && y<=SC.y1+0.05) continue; const o=over(x,y,TP.zTop); if(o<Infinity) lo=Math.min(lo,o-TP.zTop); } }
        ex({ id:'top_station', of:'area', z:r3(TP.zTop), headroom:r3(lo), reason:'the open top station on the skylounge roof ('+ft(TP.zTop)+' m), '+ft(lo)+' m under the hardtop: reached only by the station ladder from the aft deck (top_station_ladder), so no walk area. Its helm is the helm_top interact.', waitsOn:['duck'] }); }
      ex({ id:'hardtop', of:'area', z:HT.z, reason:'the hardtop’s roof at '+ft(HT.z)+' m carries the radar and the rod launcher: no rail and no way up, so no standing surface.' });
      ex({ id:'engine_room', of:'area', z:BLW.engZ, reason:'the engine room aft of the cabins’ bulkhead (y '+ft(BLW.y0)+'), its floor at '+ft(BLW.engZ)+' m: the engines fill it and it opens only with the below cut, so no walk area.' });
      if(SA.head){ const Hd=SA.head, gap=Math.min(...SA.fit.filter(f=>f[0]==='settee' && f[3]>Hd.y1).map(f=>f[3]-Hd.y1));
        ex({ id:'head', of:'area', z:ZS, reason:'the day head, '+ft(Hd.x1-Hd.x0)+' × '+ft(Hd.y1-Hd.y0)+' m inside its walls on the salon sole: a seated space entered by its interact, not walked. Its door faces the settees’ aft ends '+ft(gap)+' m away.', waitsOn:['sit'] }); }
      if(spec.tower){ const Lt=spec.tower.ladder;
        ex({ id:'tower_ladder', of:'climb', kind:'ladder', at:P3(Lt.x, mid(Lt.y0,Lt.y1), mid(Lt.z0,Lt.z1)), reason:'the tower ladder from the flybridge ('+ft(Lt.z0)+' m) to the tower top ('+ft(Lt.z1)+' m), '+Lt.n+' rungs and near upright: drawn with the tower only, and climbing it waits on ladderOn.', waitsOn:['ladderOn'] }); }
      if(STN.ladder && SLV==='top'){ const Lt=STN.ladder;
        ex({ id:'top_station_ladder', of:'climb', kind:'ladder', at:P3(Lt.x, mid(Lt.y0,Lt.y1), mid(Lt.z0,Lt.z1)), reason:'the station ladder at the aft rail, from the aft deck ('+ft(Lt.z0)+' m) to the top station ('+ft(Lt.z1)+' m), '+Lt.n+' rungs and near upright: its foot is outside the aft deck’s walk, and climbing it waits on ladderOn.', waitsOn:['ladderOn'] }); }
      ex({ id:'swim_ladder', of:'climb', kind:'ladder', z:CZ, at:P3(DOORS.transom.x, yT, CZ), reason:'no swim ladder or boarding platform is drawn on this hull, and the transom door is drawn shut: coming aboard from the water waits on ladderOn and a ladder.', waitsOn:['ladderOn'] });
      ex({ id:'transom_door', of:'door', at:DOORS.transom, reason:'the transom door, '+ft(spec.doors.tr[1]-spec.doors.tr[0])+' m wide and '+ft(spec.doors.trH)+' m high, is drawn shut and no state opens it yet, so the transom stays a wall.' });
      ex({ id:'tuna_door', of:'door', at:DOORS.tuna, reason:'the tuna door, '+ft(spec.doors.tu[1]-spec.doors.tu[0])+' m wide and '+ft(spec.doors.tuH)+' m high, is drawn shut and no state opens it yet, so the transom stays a wall.' });
      if(!WB) ex({ id:'washboards', of:'washboards', reason:'no washboards: the house sides stand on the sheer behind the '+ft(spec.railW)+' m rail, '+dp.sideReveal[0]+'–'+dp.sideReveal[1]+' m from it, so there is no side deck to climb onto.' });
      ex({ id:'seat_helm', of:'seat', at:A.helm, reason:'the helm seat: taken by the helm interact (drive).' });
      SEATS.forEach(s=>ex({ id:s.id, of:'seat', at:s.at, reason:s.label.charAt(0).toUpperCase()+s.label.slice(1)+': sitting waits on sit.', waitsOn:['sit'] }));
      if(SLV==='top') STN.seats.forEach(x=>ex({ id:'seat_top_'+(x<0?'port':'starboard'), of:'seat', at:P3(x, SC.y0-0.86, TP.zTop+0.58), reason:'a top station seat, off the walk (top_station): sitting waits on sit.', waitsOn:['sit'] }));
      berths.forEach(b=>ex({ id:b.id, of:'berth', z:b.topZ, reason:BL[b.id]+': '+ft(b.deckheadZ-b.topZ)+' m from its top to the deckhead. Getting onto it waits on crawl; sleeping is the '+b.interact+' interact.', waitsOn:['crawl'] }));

      // ---- the walk file ----
      const xo=(y)=>fSx(y)-spec.railW;
      const w={ schema:'hidden-harbours/hull-walk@1', rig:RIG_FILE, exportSymbol:EXPORT_SYMBOL, hull:spec.id, label:spec.label, kind:'cabin',
        units:'metres', frame:FRAME,
        loa_m:L, loh_m:r3(yB-yT), beam_m:dp.hull.beam, DWL, cockpitZ:CZ,
        cell:{ W:G.W, H:G.H, pivot:{ x:G.cx, y:G.cy }, pxPerM:PX, defaultElev:DEFAULT_ELEV, order:['N','NE','E','SE','S','SW','W','NW'] },
        tags:{ lv:Object.assign({}, LV), ro:Object.assign({}, RO), ghostRoles:GHOST_ROLES.slice() },
        walk:{ radius:WALK.radius, stepTol:WALK.stepTol,
          rule:'A step is legal when the target point is inside an area polygon and outside its holes, and |area z − player z| < stepTol. Ramps interpolate z along their axis between a0 and a1, and hold z0 / z1 beyond them. cut is the cutaway key while standing there; cutHi / cutLo replace it above / below zMid. Polygons are player-centre: already inset by the radius, so the reader never insets them again.',
          spawn:Object.assign({}, WALK.spawn, { note:'in the salon' }), areas },
        house, anchors, interact:I, cleats:CLEAT_AT.map(c=>({ id:c.id, type:c.type, pos:c.pos.slice() })) };
      if(WB) w.washboards=WB.map(s=>{ const sd=s==='port'?-1:1, E=[]; for(let i=0;i<=12;i++){ const y=lerp(SA.aftY,spec.foreFrom,i/12); E.push([r3(sd*xo(y)), r3(y), r3(fSz(y)+0.013)]); }
        return { side:s, width_m:dp.sideReveal[1], pinch_m:dp.sideReveal[0], outer_edge:E,
          note:'the flat side deck between the house and the '+ft(spec.railW)+' m rail, from the salon door (y '+ft(SA.aftY)+') to the foredeck (y '+ft(spec.foreFrom)+'): '+dp.sideReveal[0]+'–'+dp.sideReveal[1]+' m wide, walked by the washboard’s own rule' }; });
      w._excluded=X;
      w.rock={ frames:ROCK.frames, rollA:ROCK.rollA, pitchA:ROCK.pitchA, heaveA:ROCK.heaveA, period:ROCK.period };
      w.qa={ fitCheck:fitCheck() };
      return w;
    }

    const RIG = {
      id:spec.id, label:spec.label, note:spec.note, loa:L, loaLabel:spec.loa, L, DECK:CZ, DWL,
      doorMount, DOOR, DOOR2, HOUSE, loft, geometry, deckPlan, faces, doorFaces, LEVEL_IDS, KEY, capMat:'cream',
      W:G.W, H:G.H, PX, DIRS:8, pivot:{x:G.cx,y:G.cy}, defaultElev:DEFAULT_ELEV,
      order:['N','NE','E','SE','S','SW','W','NW'],
      RIGGERS:['stowed','out'], PAINTS, paintRamps, defaultPaint:'gelcoat',
      faceCount:faceList('stowed').length, hasTower:TWF.length>0, towerDefault:TWR, hasSky:!!SK,
      sheerZ:fSz, station:section, halfAtZ, render, ROCK, rock:rockMotion, CLEATS:CLEAT_AT,
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
    RIG.faceList=faceList;                                         // riggers + the 53's opt-in tower; the extractor's widening reaches the same function
    // ADR 0048 §8.A and hull-layout-1 §4.1 by name, on each hull: the rooms, the tags, the shade, the paints, the
    // cut, the walk. walk() is the walk file's fields (§6; the kit writer adds the sha, authoring and qa.character); WALK is the walk's own data
    // (areas with zAt / cutAt / contains, the spawn) for the review page.
    Object.assign(RIG, { kind:'cabin', cockpitZ:CZ, interiorFaces:()=>IF, interiorFaceCount:IF.length, partFaces, allFaces, ROLES:RO, GHOST_ROLES,
      stationLevel:SLV, doorIds:DOOR_IDS.slice(), schemeIds:SCHEME_IDS.slice(), defaultScheme:DEFAULT_SCHEME, matsFor, paintTable, cutTable,
      GAIN, BIAS, LN, BAYER, fitCheck, leakPx, view, renderCut, cutKeys:Object.keys(CUTAWAY.gate), WALK, walk:()=>walkSidecar(), walkSidecar,
      cell:{ W:G.W, H:G.H, PX, pivot:{ x:G.cx, y:G.cy }, defaultElev:DEFAULT_ELEV } });
    return RIG;
  }

  // ===================================================================================================
  // 27.4 m SKYBRIDGE ("90"): salon, enclosed skylounge and an open top station stacked on one gentle ascent
  // ===================================================================================================
  const SKYBRIDGE = makeRig({
    id:'skybridge', label:'90\u2032 SKYBRIDGE', loa:'27.4 m \u00b7 90\u2032',
    note:'Low sheer and a 47° raked stem; salon, skybridge and open top station stacked on one gentle ascent; every windshield starts just above the deck it rises from; the fascias sweep down aft.',
    L:27.4, DWL:1.13, W:1200, H:1170, cx:600, cy:730,
    ROCK:{ frames:8, rollA:1.35, pitchA:0.80, heaveA:0.70, period:4.2 },
    stem:{ y0:10.675, k:1.054 }, keelEndZ:0.60, chineEndZ:2.00,
    sheer:[[-13.7,2.11],[-11.5,2.14],[-10,2.17],[-9,2.25],[-8,2.34],[-6,2.56],[-4,2.79],[-2,3.02],[0,3.24],[2,3.45],[3.5,3.57],[5,3.655],[7,3.74],[9,3.82],[11,3.90],[13.7,4.00]],
    beam:[[-13.7,3.08],[-13.43,3.16],[-13.02,3.22],[-12.33,3.28],[-10.96,3.34],[-8.22,3.43],[-5.48,3.495],[-2.19,3.52],[0.55,3.52],[2.74,3.47],[4.93,3.36],[7.12,3.045],[8.49,2.675],[9.86,2.235],[10.96,1.795],[12.06,1.23],[12.74,0.70],[13.29,0.32],[13.7,0]],
    keel:[[-13.7,0.42],[-10,0.24],[-6,0.09],[-2,0.01],[1,0],[4,0.03],[6.5,0.12],[8.5,0.30],[9.6,0.48]],
    chineZ:[[-13.7,1.06],[-8,1.12],[-3,1.20],[1,1.28],[4,1.40],[6.5,1.56],[8.5,1.74],[10,1.90]],
    chineX:[[-13.7,2.95],[-11,3.14],[-7,3.26],[-3,3.30],[0,3.24],[2.5,3.05],[5,2.62],[7,2.05],[9,1.30],[10.6,0.62]],
    flare:[[-13.7,1.0],[0,1.05],[4,1.2],[7,1.45],[9.5,1.7],[11.6,1.9]],
    camber:[[2.9,0.25],[4.4,0.24],[8,0.18],[12,0.10],[13.7,0.05]],
    capW:0.34, railW:0.09, cockpitZ:1.45, cockpitFwd:-9.9, foreFrom:2.90,
    hullWin:{ y0:-0.8, y1:1.0, z0:2.14, z1:2.52, slant:0.14 }, vent:{ y0:-6.6, y1:-3.2, z0:2.03, z1:2.29, n:5 },
    doors:{ tr:[1.05,2.10], trH:0.72, tu:[-2.10,-1.10], tuH:0.62 },
    capRods:[-13.0,-12.1,-11.2], tubs:[[-2.55,-13.05],[2.55,-13.05]],
    chair:{ y:-11.85 }, sideStair:{ x0:2.40, x1:3.00, y0:-11.75, n:6 },
    mezz:{ y0:-9.9, y1:-8.30, z:2.88, seat:{ x0:-2.05, x1:2.05, y0:-9.88, y1:-9.24, back:'+y' }, drawers:[-1.55,-0.55,0.55,1.55], dw:0.44 },
    railY:[8.2,9.6,11.0,12.3], railH:0.62, hatchY:8.6,
    salon:{ aftY:-8.30, soleZ:2.88, ceilZ:5.08, dashY:2.90, cam:0.12,
      zs:[2.30,2.88,3.40,3.65,3.85,4.00,4.15,4.30,4.48,4.65,4.82,4.93,5.08,5.20,5.33],
      rings:[[2.30,5.90,-8.30,3.24,4.90],[3.40,5.40,-8.27,3.23,4.85],[3.85,4.50,-8.25,3.22,4.60],[4.00,3.50,-8.24,3.21,4.20],[4.22,2.62,-8.23,3.20,3.90],
             [4.48,2.00,-8.21,3.19,3.70],[4.74,1.45,-8.19,3.18,3.55],[4.99,0.90,-8.17,3.17,3.45],[5.19,0.50,-8.16,3.16,3.35],[5.33,0.20,-8.15,3.15,3.25]],
      band:{ yTip:-6.55, z0:3.9, z1:4.7,
        hi:[[-6.55,3.80],[-5.78,4.19],[-5.17,4.34],[-4.57,4.49],[-3.96,4.58],[-2.75,4.70],[-1.54,4.79],[-0.33,4.88],[0.88,5.00],[1.48,5.06],[2.6,5.10],[4.5,5.12]],
        lo:[[-6.55,3.80],[-5.8,3.76],[-4.57,3.76],[-3.96,3.79],[-3.0,3.82],[-1.54,3.88],[-0.33,3.94],[0.88,4.00],[1.48,4.07],[2.09,4.18],[2.45,4.27],[3.0,4.40],[4.5,4.70]] },
      door:{ x0:-1.80, x1:-0.20, z0:2.88, z1:4.93 }, fixed:{ x0:-0.20, x1:1.40, z0:2.95, z1:4.88 },
      comp:{ x0:-0.55, x1:0.55, y0:0.50, y1:2.85 },
      up:{ x0:2.10, x1:2.78, y0:-6.90, y1:-4.00, tv0:-6.00, tv1:-4.60 },   // R3: one stair, in drop 15's well (y −6.90 … −4.00)
      fit:[ ['settee',-2.95,-2.30,-6.30,-3.10,'-x'], ['settee',-2.30,-1.60,-6.30,-5.70,'-y'], ['table',-2.05,-1.25,-5.20,-3.80,0.46],
        ['counter',0.70,1.60,-2.50,0.30,0.90], ['stool',0.25,-1.9], ['stool',0.25,-1.0], ['stool',0.25,-0.1], ['counter',2.40,3.00,-2.50,0.60,0.92],
        ['settee',-3.00,-2.40,-1.90,1.20,'-x'], ['settee',-2.40,-1.40,-1.90,-1.40,'-y'], ['settee',-2.40,-1.40,0.70,1.20,'+y'], ['table',-2.30,-1.50,-1.20,0.50,0.72],
        ['tall',2.40,3.00,-3.30,-2.50,2.0], ['upper',2.64,3.00,-3.30,-1.20,1.45,2.05] ],
      head:{ x0:-2.95, x1:-1.95, y0:-8.05, y1:-6.75, doorX0:-2.75, doorX1:-2.15 } },
    bridgeSlab:{ yF:1.05, yA:-9.55, W:3.24, N:3.0, e:2.0, rA:0.9, zTop:5.52, cam:0.03, zf:[[1.05,5.40],[-1,5.31],[-4,5.15],[-7,4.97],[-9.55,4.80]] },
    sky:{ aftY:-7.40, soleZ:5.56, ceilZ:7.62, dashY:-1.70, cam:0.13,
      zs:[5.46,5.52,5.56,5.85,6.10,6.30,6.45,6.60,6.75,6.95,7.10,7.30,7.45,7.56,7.62,7.72],
      rings:[[5.46,1.00,-7.40,2.98,3.70],[5.52,0.70,-7.40,2.98,3.60],[5.85,0.10,-7.38,2.97,3.45],[6.45,-0.94,-7.35,2.95,3.25],[6.95,-1.90,-7.32,2.93,3.05],
             [7.30,-2.75,-7.29,2.91,2.85],[7.55,-3.30,-7.27,2.90,2.70],[7.72,-3.70,-7.25,2.89,2.60]],
      band:{ yTip:-6.62, z0:6.2, z1:7.0,
        lo:[[-6.62,6.20],[-5.5,6.15],[-4,6.19],[-2.75,6.26],[-1.54,6.37],[-0.94,6.45],[0,6.50],[1.2,6.6]],
        hi:[[-6.62,6.20],[-6.38,6.51],[-5.78,6.69],[-5.17,6.90],[-4.57,6.99],[-3.96,7.02],[-3.36,7.08],[-2.75,7.17],[0,7.30],[1.2,7.35]] },
      door:{ x0:0.35, x1:1.45, z0:5.56, z1:7.56, slide:-1 }, fixed:{ x0:-1.55, x1:0.35, z0:6.0, z1:7.40 },
      well:{ x0:2.02, x1:2.82, y0:-6.90, y1:-4.00 },
      items:{ console:{ x0:-1.20, x1:1.20, y0:-2.70, y1:-2.00 }, chairs:[-0.80,0,0.80], chairY:-3.30,
        lset:{ x0:-2.80, x1:-2.20, y0:-6.90, y1:-3.40 }, tab:{ x0:-1.95, x1:-1.25, y0:-6.0, y1:-4.3 }, sset:{ x0:1.95, x1:2.50, y0:-3.20, y1:-2.50 } } },
    station:{ lv:'top', slab:{ yF:-2.30, yA:-9.10, W:2.72, N:2.4, e:2.0, rA:0.8, zTop:7.97, cam:0.02, zf:[[-2.3,7.88],[-4.5,7.70],[-7,7.50],[-9.1,7.35]] },
      coaming:{ zs:[7.95,8.25,8.55], rings:[[7.95,-2.40,-8.80,2.62,2.30],[8.55,-3.35,-8.80,2.55,2.00]] },
      console:{ hx:0.70, y0:-4.30, y1:-3.60 }, seats:[-0.55,0.55], ladder:{ x:1.35, y0:-9.30, z0:5.52, y1:-8.95, z1:7.97, n:9 } },
    hardtop:{ yF:-4.40, yA:-8.80, W:1.95, N:1.2, rA:0.7, z:9.55, posts:[[1.90,-4.85,0.1],[1.90,-8.35,-0.1]] },
    radar:{ y:-6.30, len:0.72 }, domes:[{ x:0.55, y:-7.90, r:0.30 },{ x:-0.50, y:-7.95, r:0.24 }],
    whips:[[[1.35,-8.55,9.60],[1.75,-9.60,12.40]],[[-1.35,-8.55,9.60],[-1.75,-9.60,12.40]]],
    rocket:{ n:7, x:1.30, y:-8.55 },
    rig:{ x:2.42, y:-7.60, z:8.30, len:11.0, stowed:38 },
    below:{ soleZ:0.82, fwdZ:1.12, engZ:0.70, y0:-3.00, y1:9.00, stepY0:4.60, stepY1:5.00,
      eng:{ xc:1.05, hw:0.48, y0:-7.9, y1:-4.2, h:1.05, ht:0.27, gear:[-4.2,-3.7], gens:[[1.8,2.4,-3.9,-3.3,1.1],[-2.4,-1.8,-3.9,-3.3,1.1]], box:[-0.35,0.35,-9.4,-8.7,0.9] },
      walls:[[-2.6,-1.10,-0.25,-0.15],[0.55,2.6,-0.25,-0.15],[-1.15,-1.05,-0.15,0.95],[-1.15,-1.05,1.70,3.0],[0.50,0.60,-0.15,0.95],[0.50,0.60,1.70,3.0],
             [-2.6,-0.55,3.0,3.1],[0.55,2.6,3.0,3.1],[-0.60,-0.50,3.1,4.6],[0.50,0.60,3.1,4.6],[-2.2,-0.45,5.0,5.1],[0.45,2.2,5.0,5.1]],
      beds:[[-0.90,0.90,-2.92,-0.95,-1],[-2.30,-1.35,0.15,2.35,-1],[1.05,1.75,0.10,2.20,-1],[-0.85,0.85,5.75,7.85,1]],
      stands:[[-1.45,-1.02,-2.92,-2.50],[1.02,1.45,-2.92,-2.50]] },
    spawn:[-1.1,-7.30],   // v3's: where the walk puts her (area salon)
    cabin:{ portholes:{ ys:[-5.5,-4.1,-2.7,-1.3,0.1,1.5], z0:3.95, z1:4.85 },
      skyFront:{ yBot:-1.70, yTop:-3.40 }, skyFrontGlass:{ z0:6.25, z1:7.30, panes:[[-1.45,-0.10],[0.10,1.45]] },
      skySideGlass:{ z0:6.25, z1:7.00, runs:[[-6.4,-5.1],[-4.8,-3.5],[-3.2,-2.2]] }, skyAftGlass:{ z0:6.00, z1:7.40, panes:[[-1.55,0.25]] },
      below:{ ceilZ:2.76, y0:-2.90, y1:8.90, hxCap:2.90 },
      helms:[ { id:'helm_bridge', deck:'bridge_sole', pos:[0,-3.30,5.56], reach:[0,-2.70,5.56],
                note:'the skylounge helm — the full deck dedicated to the helm; her way down is the interior stair to the salon (R3)' },
              { id:'helm_top', deck:'helm_coaming', pos:[0,-5.05,7.97], reach:[0,-4.30,7.97],
                note:'open top station on the skylounge roof — reached from the aft deck by the station ladder' } ],
      ladders:[ { id:'top_station_ladder', face:'aft', x:1.35, y:-9.10, z0:5.52, z1:7.97, connects:['aft_deck','helm_coaming'],
                  note:'Aft deck to the open top station — the rig’s own raked ladder.' } ],
      wayDown:{ id:'house_to_bridge', kind:'interior_stair', from:'bridge_sole', to:'house_sole', well:{ x0:2.02, x1:2.82, y0:-6.90, y1:-4.00 },
                note:'R3 (2026-09-19): she stands in the skylounge and goes down the interior stair to the salon, then out the salon slider to the mezzanine and cockpit.' },
      excluded:{ foredeck_route:'no exterior route cockpit→foredeck — the house sides stand on the sheer behind the cap rail',
                 below_aft:'aft of the dash the staterooms sit under the salon sole (1.94 m clear) — the foredeck is the lid only forward of y 2.90' } },
    anchors:{ helm:{ x:0, y:-3.30, z:6.20 }, towerStation:{ x:0, y:-5.05, z:8.55 } },
  });

  // ===================================================================================================
  // 16.2 m CONVERTIBLE ("53") — id kept as 'convertible' so SportFisherFleet's key still resolves.
  // Her lines: a low sheer
  // 1.95 → 2.83 m over the keel (bow freeboard 1.83 m) under a dead-level foredeck crown, 44° straight stem,
  // house set in behind 0.45 m side decks, one long wedge of glass running up to the brow, and the flybridge
  // brow as a lens (peak 4.77 m) that slopes down to its nose over the windshield and down aft over the
  // mezzanine. Hardtop by default; the tuna tower is an option (opts.tower).
  // ===================================================================================================
  const CONVERTIBLE = makeRig({
    id:'convertible', label:'53\u2032 CONVERTIBLE', loa:'16.2 m \u00b7 53\u2032',
    note:'Low sheer to 2.83 m, a 44° stem, a level foredeck crown, one wedge of glass under a lens-shaped flybridge brow; hardtop, optional tuna tower.',
    L:16.2, DWL:0.67, loftZ:1.00, W:820, H:770, cx:410, cy:475,   // DWL = the fleet draft, 4% of LOA (was 1.00); her lines stay lofted from 1.00
    ROCK:{ frames:8, rollA:2.1, pitchA:1.25, heaveA:0.95, period:3.0 },
    stem:{ y0:6.37, k:0.964 }, keelEndZ:0.55, chineEndZ:1.55,
    sheer:[[-8.1,1.95],[-7.0,1.95],[-6.0,1.97],[-5.0,2.01],[-4.0,2.09],[-3.0,2.20],[-2.0,2.29],[-1.0,2.39],[0,2.49],[1.0,2.565],[2.0,2.62],[3.0,2.68],[4.0,2.735],[5.0,2.77],[6.0,2.80],[7.0,2.82],[8.1,2.83]],
    beam:[[-8.1,2.20],[-7.38,2.24],[-6.74,2.28],[-5.15,2.37],[-3.55,2.43],[-1.95,2.50],[-0.36,2.55],[1.24,2.61],[1.88,2.61],[2.83,2.58],[3.79,2.49],[4.43,2.39],[5.39,2.19],[6.03,1.91],[6.66,1.50],[7.30,0.93],[7.62,0.60],[7.94,0.22],[8.1,0]],
    keel:[[-8.1,0.36],[-6.0,0.22],[-3.6,0.09],[-1.2,0.02],[0.6,0],[2.4,0.03],[3.9,0.12],[4.9,0.26],[5.5,0.40]],
    chineZ:[[-8.1,0.92],[-4.7,0.97],[-1.8,1.04],[0.6,1.12],[2.4,1.22],[3.9,1.33],[5.0,1.42],[6.2,1.50]],
    chineX:[[-8.1,2.08],[-6.5,2.20],[-4.0,2.30],[-1.8,2.36],[0,2.34],[1.5,2.25],[3.0,1.98],[4.2,1.62],[5.3,1.12],[6.2,0.55]],
    flare:[[-8.1,1.0],[0,1.05],[2.4,1.2],[4.1,1.45],[5.6,1.7],[6.9,1.9]],
    camber:[[0.0,0.42],[1.0,0.40],[2.0,0.36],[3.0,0.30],[4.0,0.245],[5.0,0.21],[6.0,0.18],[7.0,0.155],[8.1,0.10]],
    capW:0.30, railW:0.08, cockpitZ:1.30, cockpitFwd:-5.05, foreFrom:0.0,
    hullWin:null, vent:{ y0:-3.43, y1:-0.43, z0:1.88, z1:2.27, n:4, slant:0.22, ends:0.10 },
    doors:{ tr:[0.70,1.40], trH:0.62, tu:[-1.35,-0.75], tuH:0.48 },
    capRods:[-7.65,-7.05,-6.45], tubs:[[-1.55,-7.60],[1.55,-7.60]],
    chair:{ y:-6.75 }, sideStair:{ x0:1.35, x1:1.80, y0:-5.85, n:3 },
    mezz:{ y0:-5.05, y1:-4.36, z:1.72, seat:{ x0:-1.20, x1:0.38, y0:-4.92, y1:-4.38, back:'+y' }, drawers:[], dw:0.30 },
    wings:{ x0:1.85, x1:1.97, yA:-5.57, yF:-4.28, zA:2.02, zF:3.66, p:1.15 },
    railY:[1.9,3.0,4.1,5.2,6.3,7.4], railH:0.55, hatchY:4.9,
    salon:{ aftY:-4.36, soleZ:1.72, ceilZ:3.74, dashY:0.0, dashH:1.02, cam:0.03, rA:0.12,
      zs:[1.60,1.72,1.95,2.20,2.45,2.70,2.90,3.00,3.09,3.20,3.35,3.45,3.58,3.66,3.74],
      rings:[[1.60,2.75,-4.36,1.97,2.9],[2.50,2.40,-4.36,1.97,2.8],[2.98,1.97,-4.36,1.96,2.6],[3.09,1.50,-4.36,1.95,2.35],[3.35,1.20,-4.35,1.94,2.2],[3.58,0.95,-4.35,1.93,2.05],[3.74,0.62,-4.34,1.92,1.9]],
      band:{ yTip:-4.21, z0:2.9, z1:3.6,
        lo:[[-4.21,2.84],[-3.5,2.87],[-2.7,2.90],[-1.11,2.96],[0.47,3.04],[1.50,3.09],[2.2,3.20]],
        hi:[[-4.21,2.84],[-3.95,3.10],[-3.81,3.22],[-3.26,3.46],[-2.46,3.58],[-1.11,3.66],[0.0,3.70],[1.5,3.72]] },
      door:{ x0:0.45, x1:1.45, z0:1.72, z1:3.55, slide:-1 }, fixed:{ x0:-1.75, x1:0.45, z0:2.70, z1:3.50 },
      comp:{ x0:-0.05, x1:0.60, y0:-1.15, y1:-0.10, n:7 },
      fit:[ ['settee',-1.82,-1.25,-4.20,-2.30,'-x'], ['table',-0.85,0.10,-3.25,-2.60,0.46],
        ['counter',1.40,1.84,-4.20,-2.55,0.78], ['tv',1.58,1.62,-3.85,-2.90,0.82,1.45],
        ['counter',-1.84,-0.15,-2.12,-1.45,0.92], ['tall',-1.84,-1.35,-1.45,-0.95,1.85],
        ['settee',1.28,1.84,-2.35,-0.45,'+x'], ['table',0.78,1.22,-2.05,-0.85,0.72] ] },
    bridgeSlab:{ yF:0.71, yA:-5.36, W:2.05, N:2.6, e:2.2, rA:0.85, zTop:3.92, cam:0.02, zf:[[0.71,3.82],[0.47,3.78],[-0.5,3.72],[-1.11,3.68],[-2.7,3.66],[-4.29,3.64],[-5.0,3.70],[-5.36,3.79]] },
    station:{ lv:'bridge', slab:null,
      coaming:{ lens:true, zs:[3.92,4.05,4.22,4.45,4.62,4.77],
        rings:[[3.92,0.50,-5.20,2.02,2.5],[4.05,0.20,-4.85,2.00,2.3],[4.22,-0.12,-4.30,1.98,2.1],[4.45,-0.55,-3.50,1.95,1.9],[4.62,-0.85,-2.95,1.93,1.7],[4.77,-1.13,-2.48,1.90,0.6]] },
      console:{ hx:0.70, y0:-2.00, y1:-1.45 }, seats:[-0.62,0,0.62], deckRail:{ y0:-4.30, h:0.80 },
      settees:[{ x0:-1.85, x1:-1.30, y0:-3.90, y1:-3.05, back:'-x' }],
      hatch:{ x0:-1.82, x1:-1.34, y0:-5.02, y1:-4.47 } },
    flyLadder:{ x:-1.58, y0:-4.93, z0:1.72, y1:-4.47, z1:3.92, n:8 },
    hardtop:{ yF:-1.10, yA:-5.00, W:1.80, N:1.0, rA:0.6, z:5.95, posts:[[1.72,-1.60,0.10],[1.78,-4.80,-0.05]] },
    tower:{ feet:[[1.55,-1.75],[1.65,-4.70]], top:{ hx:0.58, y0:-3.95, y1:-2.55, z:7.85 }, rail:0.62,
      pod:{ hx:0.30, y0:-2.92, y1:-2.60 }, ladder:{ x:0, y0:-5.05, z0:3.94, y1:-4.05, z1:7.85, n:11 } }, towerDefault:false,
    radar:{ y:-1.75, len:0.52 }, domes:[{ x:0.62, y:-4.25, r:0.22 },{ x:-0.58, y:-4.40, r:0.18 }],
    whips:[[[0.95,-4.60,6.00],[1.25,-5.25,8.40]],[[-0.95,-4.60,6.00],[-1.25,-5.25,8.40]]],
    rocket:{ n:4, x:1.05, y:-4.80 },
    rig:{ x:1.62, y:-4.30, z:5.55, len:7.0, stowed:36 },
    below:{ soleZ:0.55, fwdZ:0.98, engZ:0.42, y0:-1.30, y1:6.05, stepY0:4.10, stepY1:4.40, walkY0:-0.25,
      eng:{ xc:0.78, hw:0.40, y0:-4.20, y1:-2.60, h:0.85, ht:0.20, gear:[-4.55,-4.20], gens:[[1.30,1.75,-2.35,-1.90,0.70]], box:null },
      walls:[[-2.4,-0.30,-0.05,0.01],[-0.36,-0.30,0.01,0.40],[-0.36,-0.30,1.00,2.02],[-2.4,-0.30,2.02,2.08],[-0.36,-0.30,2.60,3.55],[-2.2,-0.30,3.55,3.61],
             [0.55,2.4,0.55,0.61],[0.55,0.61,1.25,2.35],[0.55,2.4,2.35,2.41],[0.55,0.61,2.95,3.60],[0.55,2.2,3.60,3.66],[-2.0,-0.35,4.10,4.16],[0.35,2.0,4.10,4.16]],
      beds:[[-1.95,-0.50,0.15,1.90,-1],[1.05,1.95,0.75,2.25,-1],[-0.75,0.75,4.70,5.85,-1]],
      stands:[],
      fit:[['counter',-1.95,-1.50,3.00,3.45,0.42],['counter',-1.40,-0.45,2.15,2.55,0.85],['counter',0.75,1.15,3.05,3.50,0.42],['counter',1.30,2.00,2.50,2.95,0.85],['tall',-0.90,-0.40,4.20,4.55,1.60]] },
    spawn:[0.9,-3.3],     // v3's: where the walk puts her (area salon)
    // R2's hatch (drop 15's) and the ship's ladder under it: the hatch is 0.65 m long and the salon 2.02 m high, so
    // the ladder stands near upright against the hatch's aft edge (pass B)
    flyComp:{ x0:-1.20, x1:-0.65, y0:-4.10, y1:-3.45, ladder:{ x0:-1.10, x1:-0.75, yFoot:-3.90, yTop:-4.06, n:8 } },
    cabin:{ portholes:{ ys:[-3.3,-2.2,-1.1], z0:2.96, z1:3.58 }, houseY1:0.70,   // the salon floor runs 0.70 m under the windshield wedge, to the stair down
      below:{ ceilZ:2.60, y0:0.10, y1:5.85, hxCap:1.85 },
      helms:[ { id:'helm_bridge', deck:'bridge_sole', pos:[0,-2.86,3.92], reach:[0,-2.00,3.92],
                note:'flybridge helm console — the boat is worked from here; her way down is the companionway to the salon (R2)' } ],
      ladders:[ { id:'flybridge_ladder', face:'aft', x:-1.58, y:-4.70, z0:1.72, z1:3.92, connects:['mezzanine','bridge_sole'],
                  note:'Exterior ladder from the mezzanine up through the overhang hatch, port quarter — a way up; R2 makes the companionway her way down.' } ],
      wayDown:{ id:'house_to_bridge', kind:'companionway', from:'bridge_sole', to:'house_sole', hatch:{ x0:-1.20, x1:-0.65, y0:-4.10, y1:-3.45 },
                note:'R2 (2026-09-19): from the flybridge she goes down the companionway into the salon, then out the salon slider to the mezzanine and cockpit.' },
      excluded:{ foredeck_route:'the side decks run the length of the house (0.45 m clear) — cockpit→foredeck is walkable outside on this hull, over the low aft end of the house wings',
                 below_aft:'aft of the dash the staterooms sit under the salon sole (1.05 m clear) — full headroom holds forward of y 0.0' } },
    anchors:{ helm:{ x:0, y:-2.86, z:4.40 }, towerStation:{ x:0, y:-3.25, z:8.00 } },
  });


  const HULLS = [CONVERTIBLE, SKYBRIDGE];
  // the global (hull-layout-1 §4.1): byId(id) one hull, or null for an id it does not have; hullIds; and the
  // default hull's (the convertible's) members under the same names. Each hull keeps its own cell (byId(id).cell, and
  // cells by id): cell is the default hull's.
  const DEFAULT_HULL = CONVERTIBLE, D0 = DEFAULT_HULL;
  root.SportFisherIso2 = {
    CONVERTIBLE, SKYBRIDGE, HULLS, hullIds:HULLS.map(h=>h.id), defaultHull:D0.id,
    byId:(id)=>HULLS.filter(h=>h.id===id)[0] || null,
    PX, DIRS:8, order:['N','NE','E','SE','S','SW','W','NW'],
    PAINTS, paintRamps, defaultPaint:'gelcoat', schemeIds:SCHEME_IDS.slice(), defaultScheme:DEFAULT_SCHEME,
    HULL, BOOT, CREAM, BLUE, TEAK, DECKF, GRIP, GLAS, STEEL, IRON, KEY,
    CARPET, LEATH, TEAKG, STONE, WHITEG, GLASSD,
    matsFor, GAIN, BIAS, LN, BAYER, LV, RO, GHOST_ROLES, tagMats,
    id:D0.id, label:D0.label, kind:D0.kind, loa:D0.loa, DWL:D0.DWL, cockpitZ:D0.cockpitZ,
    faceList:D0.faceList, partFaces:D0.partFaces, interiorFaces:D0.interiorFaces, doorFaces:D0.doorFaces, geometry:D0.geometry,
    walk:D0.walk, fitCheck:D0.fitCheck, render:D0.render, renderCut:D0.renderCut,
    W:D0.W, H:D0.H, pivot:Object.assign({}, D0.pivot), defaultElev:D0.defaultElev,
    cell:{ W:D0.W, H:D0.H, PX, pivot:Object.assign({}, D0.pivot), defaultElev:D0.defaultElev },
    cells:HULLS.reduce((o,h)=>{ o[h.id]={ W:h.W, H:h.H, PX, pivot:Object.assign({}, h.pivot), defaultElev:h.defaultElev }; return o; }, {}),
  };
})(typeof globalThis!=='undefined'?globalThis:window);

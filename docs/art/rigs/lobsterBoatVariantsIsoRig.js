/* Hidden Harbours — parametric ISO Lobster Boat VARIANTS (M2 bake recipe, ADR-0006 — same pipeline as
   lobsterBoatIsoRig.js, which stays the canonical Tier-3 bake and is NOT touched by this file).

   PASS 3 — FULL REBUILD against the wharf references (Yarmouth night fleet, an MDJ-class Cape Islander,
   a Northumberland tuna sedan, a Cape-Sable-style tall house). The pass-1/2 mistake was letting STYLE own
   the wheelhouse and REGION only the offsets table — three hulls that differ by centimetres all wear the
   same house, so the fleet read as one boat. Rebuilt ownership:

     REGION  owns the hull lines AND the wheelhouse DNA — footprint, height, glass vocabulary, windscreen
             rake, and a silhouette signature you can tell at anchor:
               northumberland  the Strait sedan — long full-width house, a BAND of lights, low rail
                               around the cabin top, springy sheer, raked stem, broad flat run aft
               fundy           Cape-Islander lineage — short narrow house set forward wearing a proper
                               HAULING MAST + BOOM, small punched lights, a forward brow, tall flared
                               stem, high freeboard, tucked narrow transom
               newfoundland    longliner blood — TALL house full of deep glass, near-plumb screen under
                               a heavy visor, DRY STACK aft of the house, roof edge picked out in the
                               cove colour, straight sheer, plumb stem, wide square transom
     STYLE   owns only the aft arrangement: open (roof stops at the house) · hardtop (a SHORT
             CANTILEVERED roof extension over the helm + hauling station — no support posts — plus the
             stainless arch). The old posted "shelter" is gone: no reference boat carries one.
     SIZE    owns scale AND the gear level: inshore drops a light, a screen pane, the dome and the
             exhaust; offshore adds a light, a taller house, stanchion RAILS down both washboards, pods,
             flood bar, extra whips and a liferaft canister
     PAINT   12 schemes (topsides/boot/stripe/house ramps, OKLCH-generated) — unchanged from pass 1.

   GLAZING, the skew fix: every light is a TRUE RECTANGLE on a PARALLEL-SIDED wall — one level sill, one
   level head, plumb mullions, no steps, no leaning, no trapezoids chasing the windscreen plane. The wall
   only tapers over a short windowless NOSE between a solid corner post and the screen, so no pane ever
   sits on a slanted surface. The windscreen is its own band between the corner posts and may stand a
   little taller than the side band on a high-sheered hull (exactly what the references do).

   3 × 2 × 3 = 18 hulls, × 12 paints = 216 paint-outs. Fixed 3/4 turntable camera (elev 40deg default,
   30–50 adjustable), 45deg steps, flat-facet shading from a fixed upper-left key, z-buffered, ordered
   dither, 1px keyline post-pass, NO AA. 32 px = 1 m.

   Single cell 480x420, pivot (240,232) = boat origin (amidships, keel bottom, centreline), pinned for
   EVERY size / style / region / heading. No outboard (inboard diesel). Deck anchors baked per variant:
   helmSeat / haulerMount / tubMounts(5) / navMounts take the same variant descriptor as render(). Pass
   the hull's rock(i) so overlays ride the wave.

   Exposes globalThis.LobsterBoatVariantsIso = { W,H,PX,DIRS,pivot,order,ROCK,rock(i),
     SIZES,STYLES,REGIONS,PAINTS, resolve(v), render(dir,opts), hullMeta(v), paintRamps(id),
     windowPlan,glazingCheck,glazingReport, anchors,gameplayGeometry,
     helmSeat,haulerMount,tubMounts,navMounts, doorMount,houseOf,loftOf,interiorEnv,
     geometry,faces,doorFaces,LEVEL_IDS, DECKF,GRIP,GLAS,STEEL,IRON,KEY }.
   opts = { size, style, region, paint, elev, roll, pitch, heave, doorOpen }.

   PASS 4 — THE DOOR AND THE PUBLISHED HOUSE (tranche 4 of the interiors program). The aft doorway
   (the dark panel every variant carries, offset a shade to port) is now a REAL opening closed by a
   SLIDING door — opts.doorOpen 0..1 rides the leaf to starboard along an exterior track, parking
   over the aft window (0 = closed, fleet default; no swept arc — the collider is the leaf itself).
   doorMount(dir,opts) -> threshold + leading edge + clear state. houseOf(v)/loftOf(v) publish the
   wheelhouse + cuddy + hull loft per variant so boatInteriorRig.js builds all 18 interiors from
   these exact numbers; interiorEnv(v) hands boatInteriorRig a per-variant env. The house aft wall
   is real; STYLE stays an aft-arrangement flag (roof only), so all 18 share the door mechanism.

   PASS 5 — CUTAWAY DATA (batch 2 of the owner-ruled cutaway composite — same mechanism as the
   canonical lobster, no new semantics). ONE variantAware rig = 18 boats: geometry(v), faces(v) and
   the exported doorFaces(opts) take the SAME top-level {size,style,region} descriptor render()
   takes — the nested `variant` spelling belongs to the interior sidecars, not this rig. ASK B:
   every face DECLARES its level in `lv` — an authoring cursor inside facesFor(V), stamped on
   F.push; ids match the lobster family table (hull/cockpit/foredeck/house/cuddy/rigging). ASK A:
   geometry(v) publishes soleZ + ceilingZ (or an EXPLICIT open-above) per walkable level per
   variant, declared from the same resolve(v)/houseOf(v) constants the mesh is built from; the
   cuddy ceiling law is the foredeck underside the interior dresses (sheerZ(y)-0.16, the fleet
   liner constant). THE TIE: cockpit and house share one sole z (DECK) on all 18 — the published
   ceilings break it in-file. render(dir,{cullLevels:[...]}) is the reference cut. Outside the new
   fields the meshes and pixels are byte-identical across all 18 — adjudicated against
   qa/cutaway-baseline/ in qa/Boat Cutaway QA 2.dc.html. Adds geometry,faces,doorFaces,LEVEL_IDS
   to the exports. */
(function (root) {
  const PX = 32, S = 32;
  const W = 480, H = 420, cx = 240, cy = 232;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const ROCK = { frames: 8, rollA: 2.8, pitchA: 1.6, heaveA: 1.2, period: 3.2 };
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }

  // ============================ OKLCH ramp generation (unchanged, pass 1) ============================
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

  // shared, paint-independent ramps (sampled, KTC)
  const DECKF = ['#5f655f','#767c73','#8d9289','#a2a79d','#b6bbb0','#c8ccc2'];
  const GRIP  = ['#33372f','#3f4339','#4b4f44','#575b4f','#63675a','#6f7365'];
  const GLAS  = ['#131c21','#213039','#33434e','#48657a','#6b91a1'];
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];
  const IRON  = ['#0e1114','#171b21','#232a32','#333c46'];
  const KEY   = '#0d1418';

  // the original sampled Knuckles & Claws slice, kept literal so `gelcoat` matches the canonical bake
  const KTC_TOP  = ['#7c848a','#9aa2a6','#b7bfbf','#d0d7d4','#e4e9e3','#f0f3ed','#f9fbf5'];
  const KTC_BOOT = ['#0a0d12','#10141b','#171d27','#212836','#2c3444'];
  const KTC_HOUS = ['#868e93','#a2aaae','#bfc6c6','#d6dbd7','#e8ebe5','#f2f4ee','#fbfcf6'];
  const KTC_STRP = ['#0f2f57','#194d84','#2668a9','#3a81c6','#579ad9'];

  /* 12 paint identities (unchanged from pass 1 — the paint kit is signed off). spec = [Ldark, Llight,
     chroma, hue]; topsides/house 7 steps, boot/stripe 5. Flat-facet shading (GAIN 3.0 / BIAS 2.7) lands
     most lit hull faces on steps 3-5, so dark paints keep a compressed low L range. */
  const PAINTS = [
    { id:'gelcoat',  label:'WHITE GELCOAT',  note:'The canonical Knuckles & Claws slice — white topsides, near-black boot, twin blue stripes.',
      literal:{ top:KTC_TOP, boot:KTC_BOOT, stripe:KTC_STRP, house:KTC_HOUS } },
    { id:'harbour',  label:'HARBOUR NAVY',   note:'Deep navy topsides, white house, a red waterline and cove.',
      top:[0.24,0.52,0.080,262], boot:[0.16,0.29,0.050,262], stripe:[0.38,0.64,0.150,30],  house:[0.60,0.96,0.008,250] },
    { id:'spruce',   label:'SPRUCE GREEN',   note:'Dark spruce topsides over a black bottom, cream cove — the old wooden-boat scheme.',
      top:[0.26,0.52,0.062,152], boot:[0.15,0.28,0.016,150], stripe:[0.58,0.84,0.050,92],  house:[0.60,0.95,0.020,96] },
    { id:'ochre',    label:'OCHRE',          note:'Mustard-ochre topsides, dark green boot — the loudest hull in the harbour.',
      top:[0.44,0.74,0.100,82],  boot:[0.17,0.30,0.040,150], stripe:[0.26,0.48,0.055,150], house:[0.62,0.96,0.014,88] },
    { id:'oxblood',  label:'OXBLOOD',        note:'Deep oxblood topsides with a cream cove stripe.',
      top:[0.30,0.58,0.105,26],  boot:[0.16,0.28,0.040,20],  stripe:[0.60,0.86,0.045,88],  house:[0.62,0.95,0.014,60] },
    { id:'fog',      label:'FOG GREY',       note:'Pale cool grey topsides, white house, oxblood boot.',
      top:[0.50,0.86,0.014,232], boot:[0.22,0.38,0.085,24],  stripe:[0.34,0.58,0.095,24],  house:[0.64,0.97,0.006,240] },
    { id:'capelin',  label:'CAPELIN',        note:'Seafoam blue-green topsides, blue cove — the inshore favourite.',
      top:[0.52,0.86,0.042,178], boot:[0.16,0.28,0.025,200], stripe:[0.40,0.64,0.095,235], house:[0.64,0.96,0.008,190] },
    { id:'buff',     label:'DORY BUFF',      note:'Buff-tan topsides over an oxblood bottom, straight off a dory.',
      top:[0.54,0.87,0.052,74],  boot:[0.22,0.38,0.085,24],  stripe:[0.34,0.58,0.090,26],  house:[0.64,0.96,0.016,72] },
    { id:'tarblack', label:'TAR BLACK',      note:'Black topsides, white cove, red boot — the hard-used workhorse.',
      top:[0.19,0.44,0.010,250], boot:[0.26,0.42,0.120,28],  stripe:[0.64,0.90,0.006,250], house:[0.58,0.94,0.006,250] },
    { id:'bluefin',  label:'BLUEFIN',        note:'Mid cerulean topsides, white house, white cove.',
      top:[0.38,0.68,0.100,250], boot:[0.16,0.28,0.040,250], stripe:[0.66,0.92,0.008,240], house:[0.64,0.97,0.006,240] },
    { id:'rust',     label:'RED LEAD',       note:'Red-lead primer topsides — never finished, always working.',
      top:[0.40,0.70,0.110,45],  boot:[0.18,0.31,0.035,40],  stripe:[0.26,0.50,0.034,46],  house:[0.60,0.92,0.022,62] },
    { id:'pearl',    label:'PEARL & GOLD',   note:'Off-white pearl topsides, blue-grey boot, a gold cove line.',
      top:[0.56,0.92,0.018,88],  boot:[0.22,0.36,0.028,250], stripe:[0.52,0.76,0.090,78],  house:[0.64,0.98,0.008,88] },
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
  const _matCache = {};
  function matsFor(id){
    const p = PAINT_BY[id] ? id : 'gelcoat';
    if(_matCache[p]) return _matCache[p];
    const R = paintRamps(p);
    const MATS = { hull:{ramp:R.top,off:0}, boot:{ramp:R.boot,off:0}, cream:{ramp:R.house,off:0},
                   deck:{ramp:DECKF,off:0}, grip:{ramp:GRIP,off:0}, glas:{ramp:GLAS,off:0},
                   blue:{ramp:R.stripe,off:0}, steel:{ramp:STEEL,off:0}, iron:{ramp:IRON,off:0},
                   blk:{ramp:R.boot,off:-1}, dark:{ramp:R.boot,off:-2} };
    const RINDEX = {};
    [R.top,R.boot,R.house,DECKF,GRIP,GLAS,R.stripe,STEEL,IRON].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
    return (_matCache[p] = {MATS, RINDEX});
  }

  // ============================ variant axes ============================
  /* SIZE: scalars + the GEAR LEVEL. houseK scales the house-height floor; glassK the window-band depth.
     lightStep/paneStep add/drop side lights and screen panes. Booleans are the fit-out: dome, pods,
     washboard rails, roof liferaft, flood bar, side exhaust, hatch count. */
  const SIZES = [
    { id:'inshore',  label:'INSHORE',  loa:8.6,  beam:0.80, dep:0.88, gear:0.85, houseK:0.96, glassK:0.94,
      lightStep:-1, paneStep:-1, radar:false, pods:false, rails:false, raft:false, floods:false, exhaust:false, hatches:2,
      note:'Small day boat — beamier for her length, shallow. One light fewer a side, one screen pane fewer, no dome, no stack of electronics: harbour gear for short strings.' },
    { id:'standard', label:'STANDARD', loa:12.0, beam:1.00, dep:1.00, gear:1.00, houseK:1.00, glassK:1.00,
      lightStep:0,  paneStep:0,  radar:true,  pods:false, rails:false, raft:false, floods:false, exhaust:true,  hatches:3,
      note:'The Tier-3 hull. The 12 m boat the fleet is measured against.' },
    { id:'offshore', label:'OFFSHORE', loa:14.6, beam:1.14, dep:1.10, gear:1.06, houseK:1.10, glassK:1.12,
      lightStep:1,  paneStep:0,  radar:true,  pods:true,  rails:true,  raft:true,  floods:true,  exhaust:true,  hatches:3,
      note:'Big boat for the outside grounds: taller house with deeper glass and an extra light a side, stanchion rails down both washboards, pods and a flood bar on the arch, a liferaft canister on the roof.' },
  ];
  /* STYLE: the aft arrangement ONLY. ext = how far the roof cantilevers aft of the house, metres at
     12 m (null = roof stops at the house). No support posts anywhere — the extension is a cantilever.
     arch = stainless radar arch over the cabin roof. */
  const STYLES = [
    { id:'open',    label:'OPEN BOAT', ext:null, posts:0, arch:false,
      note:'Roof stops at the house and the deck is open to the sky — the cheap arrangement, and the one the older boats keep. The masthead is whatever the region ships: rail and light pole, mast and boom, or the stack.' },
    { id:'hardtop', label:'HARDTOP',   ext:1.15, posts:0, arch:true,
      note:'A short roof extension carried aft off the house — a clean cantilever, no posts — sheltering the helm and the hauling station, with a stainless arch over the cabin for the radar and aerials (the inshore boat goes without the dome).' },
  ];
  /* REGION: hull offsets table (stern(0)->bow(8), [sheerHalf, bottomHalf, depth, keelZ] at 12 m, scaled
     by size) + the complete wheelhouse DNA + a rig signature.
       house: hfA/hfF house span as fractions of L (aft wall fwd of amidships -> front wall)
              hx     house half-width as a fraction of the sheer half-width (capped inside the hull)
              minH   house-height floor in metres (the glazing can only grow it)
              glassH window-band depth · cut corner cut radius · round bezier segs (1 = hard chamfer)
              frame  trim-frame width · srake windscreen top-vs-bottom y offset (+fwd brow / -recline)
              panes  windscreen panes · lights side lights (at standard) · mull mullion width (m at 12 m)
              nose   windowless tapered nose length · post solid corner post between glass and nose
              visor  roof lip forward of the screen top · doorH door height off the sole
       sig: 'roofrail' | 'mastboom' | 'stack' */
  const REGIONS = [
    { id:'northumberland', label:'NORTHUMBERLAND STRAIT', rake:0.50, wb:0.44, sig:'roofrail',
      house:{ hfA:0.020, hfF:0.355, hx:0.76, minH:2.20, glassH:0.56, cut:0.055, round:1, frame:0.048,
              srake:-0.34, panes:3, lights:4, mull:0.15, nose:0.60, post:0.16, visor:0.16, doorH:2.00, runA:0.14 },
      note:'The Strait sedan: a long full-width house wearing a band of lights down each side, a low rail around the cabin top and flag whips aft — tuna blood. Beamy hull, raked stem, springy sheer, a broad near-flat run aft.',
      T:[[1.80,1.48,1.14,0.05],[2.06,1.62,1.13,0.01],[2.18,1.66,1.19,0.00],[2.22,1.62,1.30,0.00],
         [2.20,1.50,1.45,0.00],[2.04,1.24,1.66,0.01],[1.74,0.88,1.95,0.06],[1.20,0.44,2.24,0.18],[0.12,0.05,2.44,0.40]] },
    { id:'fundy', label:'BAY OF FUNDY', rake:0.24, wb:0.47, sig:'mastboom',
      house:{ hfA:0.130, hfF:0.320, hx:0.64, minH:2.26, glassH:0.44, cut:0.020, round:1, frame:0.075,
              srake:+0.14, panes:2, lights:2, mull:0.24, nose:0.60, post:0.14, visor:0.22, doorH:1.95, runA:0.12 },
      note:'Cape-Islander lineage for the big tides: a tall flared stem, high freeboard, a narrow tucked transom — and a short house set forward wearing a proper hauling mast and boom over the working deck. Small punched lights on heavy frames, and the old forward brow.',
      T:[[1.58,1.26,1.36,0.06],[1.90,1.46,1.32,0.02],[2.08,1.56,1.36,0.00],[2.16,1.56,1.46,0.00],
         [2.16,1.44,1.62,0.00],[2.04,1.18,1.86,0.01],[1.80,0.84,2.18,0.07],[1.34,0.42,2.30,0.18],[0.14,0.05,2.56,0.40]] },
    { id:'newfoundland', label:'NEWFOUNDLAND', rake:0.08, wb:0.53, sig:'stack',
      house:{ hfA:0.065, hfF:0.340, hx:0.78, minH:2.62, glassH:0.74, cut:0.150, round:2, frame:0.034,
              srake:-0.12, panes:3, lights:2, mull:0.30, nose:0.50, post:0.18, visor:0.26, doorH:2.05, runA:0.12 },
      note:'Longliner blood: a tall house full of deep glass on a straight-sheered, plumb-stemmed hull with a wide square transom and heavy bulwarks. A near-plumb screen under a heavy visor, a dry stack aft of the house, and the roof edge picked out in the cove colour.',
      T:[[2.00,1.70,1.26,0.04],[2.16,1.78,1.26,0.01],[2.24,1.78,1.30,0.00],[2.26,1.72,1.38,0.00],
         [2.24,1.58,1.48,0.00],[2.12,1.34,1.64,0.01],[1.88,1.00,1.86,0.05],[1.36,0.52,2.10,0.16],[0.14,0.06,2.30,0.36]] },
  ];
  const byId = (arr,id,def)=>arr.find(o=>o.id===id) || arr.find(o=>o.id===def);

  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  const lerp = (a,b,t)=>a+(b-a)*t;
  const NSEG = 24, TH = 0.05;

  // ============================ resolve a variant descriptor ============================
  const SILL_CLEAR = 0.19, EAVE_CLEAR = 0.24;   // above MIN_CLEAR + the widest frame, by construction
  function resolve(v){
    v = v || {};
    const Z = byId(SIZES, v.size, 'standard'), Y = byId(STYLES, v.style, 'hardtop'),
          R = byId(REGIONS, v.region, 'northumberland');
    const paint = PAINT_BY[v.paint] ? v.paint : 'gelcoat';
    const L = Z.loa, bK = Z.beam, dK = Z.dep, gK = Z.gear, lK = L/12;
    const T = R.T.map(([ws,wb,dep,kz])=>[ws*bK, wb*bK, dep*dK, kz*dK]);
    const RAKE = R.rake*lK;
    const DECK = 0.50*dK;
    const HS = R.house;
    const HYaft = HS.hfA*L, HYfwd = HS.hfF*L, houseLen = HYfwd-HYaft;
    const cockLen = HYaft + L/2;
    const extAft = Y.ext==null ? null : HYaft - Y.ext*lK;
    const station = (u)=>{
      const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i, A=T[i], B=T[i+1];
      return { ws:lerp(A[0],B[0],fr), wb:lerp(A[1],B[1],fr), dep:lerp(A[2],B[2],fr),
               kz:lerp(A[3],B[3],fr), y:-L/2+u*L };
    };
    const uOf = (y)=>Math.max(0,Math.min(1,(y+L/2)/L));
    const sheerAt = (y)=>{ const st=station(uOf(y)); return st.kz+st.dep; };
    /* House plan — PARALLEL side walls over the whole glazed run (the skew fix), then a short windowless
       tapered nose to the windscreen. HX is capped inside the hull along the full run. */
    const noseLen = HS.nose*lK, noseY = HYfwd - noseLen;
    const wsIn = (y)=>station(uOf(y)).ws - TH - 0.10*bK;
    let HX = HS.hx * station(uOf(HYaft + 0.30*houseLen)).ws;
    for(let k=0;k<=8;k++) HX = Math.min(HX, wsIn(HYaft + (noseY-HYaft)*k/8));
    const HXf = Math.min(HX*0.90, wsIn(HYfwd)+0.04);
    /* Level glazing band. The side/aft sill clears the highest deck under the GLAZED RUN (not under the
       whole house — the run stops at the corner post, before the sheer sweeps up). The windscreen keeps
       the same head but may take a higher sill on a high-foredeck hull; the house grows for whichever
       band needs more. */
    const deckUnderWall = (y)=> (y <= HYaft+1e-4) ? DECK : sheerAt(y) - 0.05*dK;
    const yGlassF = noseY - HS.post*lK;
    const GLASS_H = HS.glassH*Z.glassK;
    const CAP = (EAVE_CLEAR + HS.frame)*dK;
    const sillZ = deckUnderWall(yGlassF) + SILL_CLEAR*dK;
    const scrSill0 = Math.max(sillZ, deckUnderWall(HYfwd) + (0.10 + HS.frame + 0.05)*dK);
    const houseH = Math.max( HS.minH*dK*Z.houseK,
                             sillZ + GLASS_H*dK + CAP - DECK,
                             scrSill0 + 0.42*dK + CAP - DECK );
    const HZ0 = DECK, HZ1 = DECK+houseH;
    const headZ = Math.min(HZ1 - CAP, sillZ + GLASS_H*dK);
    const scrSill = scrSill0, scrHead = Math.min(HZ1 - CAP, Math.max(headZ, scrSill + 0.42*dK));
    const ROOFZ = HZ1+0.06;
    const FYb = HYfwd, FYt = HYfwd + HS.srake*lK;
    const V = { size:Z, style:Y, region:R, paint, L, bK, dK, gK, lK, T, RAKE, DECK,
                HYaft, HYfwd, houseLen, cockLen, houseH, HZ0, HZ1, ROOFZ, extAft, WB:R.wb*bK,
                HX, HXf, noseY, noseLen, FYb, FYt, sillZ, headZ, scrSill, scrHead,
                GLASS_H, CAP, deckUnderWall,
                key:[Z.id,Y.id,R.id,paint].join('|') };
    V.station = station;
    V.uOf = uOf;
    V.sheerAt = sheerAt;
    V.yFront = (z)=>FYb + (FYt-FYb)*(z-HZ0)/(HZ1-HZ0);
    // raked stem: the shift pivots at frac 0.75, so the stemhead leans out while LOA stays in the cell
    V.bowRake = (u,frac)=>{ const t=Math.max(0,(u-0.60)/0.40), s=t*t*(3-2*t); return RAKE*s*(frac-0.75); };
    V.skin = (side,u,frac,inset)=>{
      const st=V.station(u);
      const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
      return [ side*lerp(wb,ws,frac), st.y+V.bowRake(u,frac), st.kz+lerp(0,dep,frac) ];
    };
    V.dfrac = (st)=>Math.max(0.04, Math.min(0.98, (DECK-st.kz)/st.dep));
    V.HXat = (y)=> y<=noseY ? HX : lerp(HX, HXf, Math.max(0,Math.min(1,(y-noseY)/noseLen)));
    V.hy = (f)=>HYaft + f*houseLen;
    V.cy = (f)=>HYaft - f*cockLen;
    V.SOLE_U = uOf(HYfwd);
    V.dw = (u)=>{ const st=V.station(u); return (lerp(st.wb,st.ws,V.dfrac(st))-TH)*0.96; };
    V.fz = (u)=>{ const st=V.station(u); return st.kz+st.dep-0.05*dK; };
    V.fw = (u)=>{ const st=V.station(u); return Math.max(0.02, st.ws-0.30*bK); };
    V.fy = (u)=>V.station(u).y + V.bowRake(u,1);
    return V;
  }

  /* ============================ glazing plan ============================
     Rectangles only. n equal lights on equal PLUMB mullions across the run [runA .. corner post], one
     level sill, one level head. n = region lights + size step, and a light is never narrower than
     0.55 m. The windscreen band lives between the corner posts on the raked plane. */
  function windowPlan(V){
    const HS=V.region.house;
    const yA = V.HYaft + HS.runA*V.lK;
    const yF = V.noseY - HS.post*V.lK;
    const span = Math.max(0.30, yF - yA);
    const gap = HS.mull*V.lK;
    let n = Math.max(1, HS.lights + V.size.lightStep), w = (span - gap*(n-1))/n;
    while(n>1 && w < 0.50*V.lK){ n--; w = (span - gap*(n-1))/n; }
    let y00 = yA;
    if(n===1 && w > 1.25*V.lK){ y00 = yA + (w - 1.25*V.lK)/2; w = 1.25*V.lK; }   // a lone light stays a window, not a slot
    const side=[];
    for(let i=0;i<n;i++){ const y0=y00+i*(w+gap); side.push({ y0, y1:y0+w }); }
    const sp = Math.max(2, HS.panes + V.size.paneStep);
    const X = 0.92*V.HXf, mg = 0.12*V.lK, pw = (2*X - mg*(sp-1))/sp, panes=[];
    for(let i=0;i<sp;i++){ const a=-X+i*(pw+mg); panes.push([a, a+pw]); }
    const aftX = V.region.id==='fundy' ? [0.44,0.88] : [0.37,0.90];
    return {
      side, lightW:w, mullion:gap, sill:V.sillZ, head:V.headZ,
      cut:HS.cut*V.dK, round:HS.round, frame:HS.frame*V.dK,
      front:{ panes, z0:V.scrSill, z1:V.scrHead },
      aft:{ x:aftX, z0:V.sillZ, z1:V.headZ,
            doorTop:Math.min(V.HZ0 + HS.doorH*V.dK, V.HZ1 - EAVE_CLEAR*V.dK) }
    };
  }

  /* Acceptance check: NOTHING CLIPS. Every glazed edge — trim frame included — sits at least MIN_CLEAR
     clear of the deck directly beneath it and of the eave above it, across all 27 hulls. */
  const MIN_CLEAR = 0.10;
  function glazingCheck(v){
    const V=resolve(v), P=windowPlan(V), fails=[], fr=P.frame;
    let worst=Infinity, worstAt='';
    const test=(id,c)=>{ if(c<worst){ worst=c; worstAt=id; }
      if(c<MIN_CLEAR) fails.push({id, clear:+c.toFixed(3)}); };
    const deck=(id,y,z)=>test(id+'/deck', (z-fr-V.deckUnderWall(y))/V.dK);
    const roof=(id,z)=>test(id+'/roof', (V.HZ1-(z+fr))/V.dK);
    P.side.forEach((wd,i)=>{ [wd.y0,wd.y1].forEach(y=>deck('side_'+(i+1),y,P.sill)); roof('side_'+(i+1),P.head); });
    deck('windscreen', V.yFront(P.front.z0), P.front.z0); roof('windscreen', P.front.z1);
    deck('aft_light', V.HYaft-0.03, P.aft.z0);            roof('aft_light', P.aft.z1);
    roof('door', P.aft.doorTop);
    return { key:V.size.id+'/'+V.style.id+'/'+V.region.id, ok:fails.length===0,
             worst:+worst.toFixed(3), worstAt, min:MIN_CLEAR, fails };
  }
  function glazingReport(){
    const rows=[];
    for(const s of SIZES) for(const y of STYLES) for(const r of REGIONS)
      rows.push(glazingCheck({size:s.id, style:y.id, region:r.id}));
    const fails=rows.filter(r=>!r.ok);
    const worst=rows.reduce((a,b)=>b.worst<a.worst?b:a, rows[0]);
    return { hulls:rows.length, ok:fails.length===0, min:MIN_CLEAR,
             worst:worst.worst, worstHull:worst.key, worstAt:worst.worstAt, fails, rows };
  }

  function hullMeta(v){
    const V = resolve(v), P = windowPlan(V);
    const amid = V.station(0.5), st0 = V.station(0), stB = V.station(0.98);
    const HS = V.region.house;
    const sigName = { roofrail:'cabin-top rail + flag whips', mastboom:'hauling mast & boom', stack:'dry stack, cove-striped roof' };
    let top = V.style.arch ? (V.size.radar ? 'stainless arch + radar' : 'stainless arch, no dome') : 'light pole';
    if(V.region.sig==='mastboom') top = V.style.arch ? 'arch + mast & boom' : 'mast & boom';
    return { loa:V.L, beam:+(amid.ws*2).toFixed(2), depth:+(amid.dep).toFixed(2),
      freeboardAft:+(st0.kz+st0.dep-V.DECK).toFixed(2),
      freeboardAmid:+(amid.kz+amid.dep-V.DECK).toFixed(2),
      bowHeight:+(stB.kz+stB.dep).toFixed(2), transomBeam:+(st0.ws*2).toFixed(2),
      rake:+V.RAKE.toFixed(2), washboard:+V.WB.toFixed(2), sole:+V.DECK.toFixed(2),
      houseSpan:[+V.HYaft.toFixed(2), +V.HYfwd.toFixed(2)], houseH:+V.houseH.toFixed(2),
      houseHmin:+(HS.minH*V.dK*V.size.houseK).toFixed(2), houseRake:+(V.FYt-V.FYb).toFixed(2),
      houseHalfW:+V.HX.toFixed(2), roofZ:+V.ROOFZ.toFixed(2),
      extAft:V.extAft==null?null:+V.extAft.toFixed(2),
      glazing:{ sillClear:+(SILL_CLEAR*V.dK).toFixed(2), glassH:+(V.headZ-V.sillZ).toFixed(2),
                eaveClear:+(EAVE_CLEAR*V.dK).toFixed(2),
                band:[+V.sillZ.toFixed(2), +V.headZ.toFixed(2)],
                screenBand:[+V.scrSill.toFixed(2), +V.scrHead.toFixed(2)],
                deckClear:+((V.sillZ-P.frame-V.deckUnderWall(V.noseY-HS.post*V.lK))/1).toFixed(2),
                roofClear:+((V.HZ1-(V.headZ+P.frame))/1).toFixed(2),
                lightW:+P.lightW.toFixed(2), mullion:+P.mullion.toFixed(2),
                sideLights:P.side.length, screenPanes:P.front.panes.length,
                corner:+HS.cut.toFixed(3), frame:+HS.frame.toFixed(3),
                post:+(HS.post*V.lK).toFixed(2), nose:+V.noseLen.toFixed(2) },
      sig:V.region.sig, sigName:sigName[V.region.sig],
      rails:!!V.size.rails, raft:!!(V.size.raft && V.style.arch),
      posts:V.style.posts, top, key:V.key };
  }

  // ============================ generic solids ============================
  const ID=(p)=>p;
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function box(c,h,mat,b,db,xf){
    xf=xf||ID;
    const P=(sx,sy,sz)=>xf([c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]]);
    const f=(v)=>({v,mat,b:b||0,db:db||0});
    return [ f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]), f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]),
             f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]), f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]),
             f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]), f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]) ];
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
  const DBP = 0.05;
  const backPanel = (y,xa,xb,za,zb,mat,b)=>({v:[[xb,y,zb],[xa,y,zb],[xa,y,za],[xb,y,za]],mat,b:b||0,db:DBP});
  function objNormal(a,b,c){ const ux=b[0]-a[0],uy=b[1]-a[1],uz=b[2]-a[2], vx=c[0]-a[0],vy=c[1]-a[1],vz=c[2]-a[2];
    return [uy*vz-uz*vy, uz*vx-ux*vz, ux*vy-uy*vx]; }
  function faceO(v, outward, mat, b, db){ const n=objNormal(v[0],v[1],v[2]);
    if(n[0]*outward[0]+n[1]*outward[1]+n[2]*outward[2] < 0) v=v.slice().reverse();
    return {v, mat, b:b||0, db:(db==null?DBP:db)}; }
  function rrect(ua,ub,va,vb,c){ return [[ua+c,va],[ub-c,va],[ub,va+c],[ub,vb-c],[ub-c,vb],[ua+c,vb],[ua,vb-c],[ua,va+c]]; }
  // corner treatment on a 2D pt loop: quadratic bezier per corner (round=1 is a hard chamfer)
  function cornersOf(pts,c,k){
    if(!(c>0)) return pts;
    const n=pts.length, out=[], K=Math.max(1,k||1);
    const lp=(P,Q,t)=>[P[0]+(Q[0]-P[0])*t, P[1]+(Q[1]-P[1])*t];
    for(let i=0;i<n;i++){
      const P=pts[i], A=pts[(i+n-1)%n], B=pts[(i+1)%n];
      const Pa=lp(P,A,Math.min(0.45, c/(Math.hypot(A[0]-P[0],A[1]-P[1])||1)));
      const Pb=lp(P,B,Math.min(0.45, c/(Math.hypot(B[0]-P[0],B[1]-P[1])||1)));
      for(let s=0;s<=K;s++){ const t=s/K, m=1-t;
        out.push([m*m*Pa[0]+2*m*t*P[0]+t*t*Pb[0], m*m*Pa[1]+2*m*t*P[1]+t*t*Pb[1]]); }
    }
    return out;
  }

  // outer paint scheme, frac 0(keel) -> 1(sheer)
  const OB = [ [0,0.27,'boot',-0.2,0], [0.27,0.315,'blue',0.2,0.01], [0.315,0.90,'hull',0,0],
               [0.90,0.945,'blue',0.28,0.01], [0.945,1,'dark',-0.25,0.006] ];

  // ============================ the build ============================
  const _faceCache = new Map();
  function facesFor(V){
    if(_faceCache.has(V.key)) return _faceCache.get(V.key);
    const F = [];
    /* PASS 5 — every face DECLARES its level (ASK B), identically across all 18 variants: LV is an
       authoring cursor stamped on F.push, so every emission path carries it. */
    let LV='hull';
    const lv=(id)=>{ LV=id; };
    F.push=function(){ for(let i=0;i<arguments.length;i++) arguments[i].lv=LV; return Array.prototype.push.apply(this,arguments); };
    const face=(v,mat,b,db)=>F.push({v,mat:mat||'hull',b:b||0,db:db||0});
    const boxF=(c,h,mat,b,db)=>{ F.push.apply(F, box(c,h,mat,b,db)); };
    const tubeF=(A,B2,rad,mat,b)=>{ F.push.apply(F, tube(A,B2,rad,mat,b)); };
    const winRR=(mapUV,outward,ua,ub,va,vb,cut,mat,b)=>faceO(rrect(ua,ub,va,vb,cut).map(([u,v])=>mapUV(u,v)), outward, mat, b);
    const glaze=(mk,outward,ua,ub,va,vb,glassB,cut)=>{ cut=cut||0.10;
      F.push(winRR(mk(0.03),  outward, ua-0.06,ub+0.06, va-0.055,vb+0.055, cut+0.03, 'iron', -0.15));
      F.push(winRR(mk(0.065), outward, ua,ub, va,vb, cut, 'glas', glassB)); };

    const { L, bK, dK, gK, lK, DECK, HYaft, HYfwd, houseLen, HZ0, HZ1, ROOFZ, extAft, WB,
            HX, HXf, noseY, noseLen } = V;
    const station=V.station, skin=V.skin, dfrac=V.dfrac, HXat=V.HXat;
    const hy=V.hy, cyf=V.cy;
    const Z=V.size, HS=V.region.house;

    lv('hull');                                   // hull shell + bulwark liner + bottom + rail caps
    // ---- hull shell ----
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],mat,b,db);
        const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb);
        if(sa.y <= HYaft+0.2){                     // inner bulwark liner (house colour), cockpit only
          const LT=0.95;
          for(let k=0;k<2;k++){
            const g0a=fa+(LT-fa)*k/2, g1a=fa+(LT-fa)*(k+1)/2;
            const g0b=fb+(LT-fb)*k/2, g1b=fb+(LT-fb)*(k+1)/2;
            face([skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'cream',-1.5,-0.03);
          }
        }
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'boot',-1.0);   // bottom
        const oa=skin(side,u0,1),ob=skin(side,u1,1),ia=skin(side,u0,1,1),ib=skin(side,u1,1,1);
        const inb=(p)=>[p[0]-side*0.30*bK,p[1],p[2]-0.004];
        face([oa,ob,inb(ib),inb(ia)],'deck',-1.2,0.03);   // covering board / rail cap
      }
    }

    lv('cockpit');                                // the working deck and everything standing on it
    // ---- cockpit sole: house-colour margin + darker grippy panel ----
    const SOLE_U = V.SOLE_U;
    const DSEG=20, BORD=0.24*bK;
    const dw=V.dw;
    for(let i=0;i<DSEG;i++){
      const u0=SOLE_U*i/DSEG, u1=SOLE_U*(i+1)/DSEG;
      face([[-dw(u0),station(u0).y,DECK],[dw(u0),station(u0).y,DECK],[dw(u1),station(u1).y,DECK],[-dw(u1),station(u1).y,DECK]],'cream',-0.3);
    }
    const gA=station(0).y+0.34*lK, gF=HYaft-0.12;
    for(let i=0;i<DSEG;i++){
      const u0=SOLE_U*i/DSEG, u1=SOLE_U*(i+1)/DSEG, y0=station(u0).y, y1=station(u1).y;
      if(y1<gA || y0>gF) continue;
      const w0=Math.max(0,dw(u0)-BORD), w1=Math.max(0,dw(u1)-BORD);
      face([[-w0,y0,DECK+0.006],[w0,y0,DECK+0.006],[w1,y1,DECK+0.006],[-w1,y1,DECK+0.006]],'grip',-0.25,0.02);
    }
    // flush stainless deck hatches — count is part of the size's fit-out
    const hatch=(x,y,w,l,handle)=>{
      const z=DECK;
      face([[x-w,y-l,z+0.008],[x+w,y-l,z+0.008],[x+w,y+l,z+0.008],[x-w,y+l,z+0.008]],'iron',0.0,0.03);
      const iw=w-0.05, il=l-0.05;
      face([[x-iw,y-il,z+0.014],[x,y-il,z+0.014],[x,y+il,z+0.014],[x-iw,y+il,z+0.014]],'steel',-3.5,0.05);
      face([[x,y-il,z+0.014],[x+iw,y-il,z+0.014],[x+iw,y+il,z+0.014],[x,y+il,z+0.014]],'steel',-2.7,0.05);
      if(handle) boxF([x, y-il+0.07, z+0.03],[0.12,0.022,0.022],'steel',-1.7,0.07);
    };
    const hw0=0.56*bK, hl0=0.44*lK, HFR=[0.26,0.47,0.69];
    for(let i=0;i<Z.hatches;i++) hatch(0,cyf(HFR[i]),hw0*(i===2?0.89:1),hl0*(i===2?0.95:1),true);
    if(Z.hatches>=3){ hatch(-1.00*bK,cyf(0.36),0.30*bK,0.32*lK,false); hatch(1.00*bK,cyf(0.36),0.30*bK,0.32*lK,false); }
    lv('hull');                                   // washboards + side decks are hull structure
    // side decks / washboards — continuous, narrowing to the house wall
    const innerX=(st)=>{ if(st.y > HYaft-0.05) return Math.min(st.ws-TH-0.10, HXat(st.y)); return st.ws-TH-WB; };
    for(const side of [-1,1]){
      for(let i=0;i<DSEG;i++){
        const u0=SOLE_U*i/DSEG, u1=SOLE_U*(i+1)/DSEG, sa=station(u0), sb=station(u1);
        const xo0=side*(sa.ws-TH), xi0=side*innerX(sa), z0=sa.kz+sa.dep-0.02;
        const xo1=side*(sb.ws-TH), xi1=side*innerX(sb), z1=sb.kz+sb.dep-0.02;
        const q = side>0 ? [[xi0,sa.y,z0],[xo0,sa.y,z0],[xo1,sb.y,z1],[xi1,sb.y,z1]]
                         : [[xo0,sa.y,z0],[xi0,sa.y,z0],[xi1,sb.y,z1],[xo1,sb.y,z1]];
        face(q,'hull',-0.6);
      }
    }
    // ---- transom ----
    const tp=(s,f)=>skin(s,0,f);
    for(const [f0,f1,mat,b] of OB) face([tp(-1,f1),tp(1,f1),tp(1,f0),tp(-1,f0)], mat, (b||0)-0.8, 0.005);
    (function(){ const s0=station(0), zt=s0.kz+s0.dep, wsx=s0.ws-TH;
      face([[-wsx,s0.y,zt],[wsx,s0.y,zt],[wsx,s0.y+0.26*lK,zt-0.004],[-wsx,s0.y+0.26*lK,zt-0.004]],'deck',-0.9,0.03); })();
    lv('foredeck');                               // the cuddy's lid — a walkable level of its own
    // ---- foredeck ----
    const FSEG=8, FCAP=0.985;
    const fz=V.fz, fw=V.fw, fy=V.fy;
    for(let i=0;i<FSEG;i++){
      const u0=SOLE_U+(FCAP-SOLE_U)*i/FSEG, u1=SOLE_U+(FCAP-SOLE_U)*(i+1)/FSEG;
      face([[-fw(u0),fy(u0),fz(u0)],[fw(u0),fy(u0),fz(u0)],[fw(u1),fy(u1),fz(u1)],[-fw(u1),fy(u1),fz(u1)]],'hull',0.5,-0.02);
    }
    (function(){ const u=SOLE_U, wv=fw(u), z=fz(u), y=station(u).y, yF=fy(u), st=station(u);
      const hwid=(zz)=>{ const fr=Math.max(0,Math.min(1,(zz-st.kz)/st.dep)); return lerp(st.wb,st.ws,fr)-TH; };
      const wTop=Math.min(wv,hwid(z)), wDeck=hwid(DECK);
      lv('house');                                // the V bulkhead is the base of the house front
      face([[-wTop,y,z],[wTop,y,z],[wDeck,y,DECK],[-wDeck,y,DECK]],'cream',-1.4,-0.03);
      lv('foredeck');
      face([[-wv,y-0.36*lK,z],[wv,y-0.36*lK,z],[wv,yF,z],[-wv,yF,z]],'hull',0.5,-0.03); })();
    (function(){ const u=0.93, y=fy(u), z=fz(u); boxF([0, y, z+0.09],[0.035,0.05,0.09*dK],'iron',0.15,-0.02); })();
    lv('hull');                                   // stern cleats ride the rail cap
    for(const s of [-1,1]) boxF([s*(station(0).ws-0.22*bK), station(0).y+0.28*lK, station(0).kz+station(0).dep+0.03],
                                [0.05,0.09,0.05],'iron',0.15,-0.02);

    lv('house');                                  // walls, glazing, vestibule, roof — cuts with the room
    // ---- WHEELHOUSE: parallel-sided over the glazed run, tapered windowless nose, raked screen ----
    const P = windowPlan(V);
    const SWZa = V.sheerAt(HYaft) - 0.15*dK, SWZn = V.sheerAt(noseY) - 0.15*dK,
          SWZf = V.sheerAt(HYfwd) - 0.15*dK;
    const FYb=V.FYb, FYt=V.FYt;
    const FYf = FYb + (FYt-FYb)*(SWZf-HZ0)/(HZ1-HZ0);
    const AL=[-HX,HYaft,HZ0], AR=[HX,HYaft,HZ0], ALt=[-HX,HYaft,HZ1], ARt=[HX,HYaft,HZ1];
    const ALs=[-HX,HYaft,SWZa], ARs=[HX,HYaft,SWZa];
    const NLb=[-HX,noseY,SWZn], NRb=[HX,noseY,SWZn], NLt=[-HX,noseY,HZ1], NRt=[HX,noseY,HZ1];
    const FLb=[-HXf,FYf,SWZf], FRb=[HXf,FYf,SWZf], FLt=[-HXf,FYt,HZ1], FRt=[HXf,FYt,HZ1];
    face([ALs,ALt,NLt,NLb],'cream',-0.1);          // port wall, parallel run
    face([ARs,NRb,NRt,ARt],'cream',-1.0);          // starboard wall, parallel run (shaded)
    face([NLb,NLt,FLt,FLb],'cream',-0.1);          // port nose (tapered, windowless)
    face([NRb,FRb,FRt,NRt],'cream',-1.0);          // starboard nose
    face([FLt,FRt,FRb,FLb],'cream',0.4);           // front wall — windscreen band
    face([AL,AR,ARt,ALt],'cream',-0.7);            // aft wall (into cockpit, full height to the sole)
    const _rny=(HZ1-HZ0), _rnz=(FYb-FYt), _rn=Math.hypot(_rny,_rnz), nY=_rny/_rn, nZ=_rnz/_rn;
    const yFront=V.yFront;
    // windscreen panes (rectangles in x-z on the raked plane)
    for(const [xa,xb] of P.front.panes)
      glaze((pr)=>((x,z)=>[x, yFront(z)+nY*pr, z+nZ*pr]), [0,nY,nZ], xa,xb,
            P.front.z0, P.front.z1, 0.5, Math.min(0.07, HS.cut*dK*0.6));
    // side lights — TRUE RECTANGLES at constant x on the parallel wall. No lean, no step, no taper.
    const sideWin=(side, y0, y1, glassB)=>{
      const rect=[[y0,P.sill],[y1,P.sill],[y1,P.head],[y0,P.head]];
      const cyg=(y0+y1)/2, czg=(P.sill+P.head)/2, fr=P.frame;
      const trim=cornersOf(rect.map(([y,z])=>[y+Math.sign(y-cyg)*fr, z+Math.sign(z-czg)*fr]), P.cut+fr*0.7, P.round);
      const glass=cornersOf(rect, P.cut, P.round);
      F.push(faceO(trim.map(([y,z])=>[side*(HX+0.03), y, z]),   [side,0,0], 'iron', -0.15, DBP));
      F.push(faceO(glass.map(([y,z])=>[side*(HX+0.065), y, z]), [side,0,0], 'glas', glassB, DBP));
    };
    for(const side of [-1,1]){
      const b0 = side<0 ? -0.15 : -1.05;
      for(const wd of P.side) sideWin(side, wd.y0, wd.y1, b0);
    }
    const AY=HYaft-0.03;
    F.push(backPanel(AY, -0.267*HX, 0.227*HX, HZ0+0.02, P.aft.doorTop,'dark',-0.5));   // doorway
    glaze((pr)=>((x,z)=>[x, AY-pr, z]), [0,-1,0], P.aft.x[0]*HX, P.aft.x[1]*HX,
          P.aft.z0, P.aft.z1, -0.25, Math.min(0.07, HS.cut*dK*0.6));

    // ---- roof / hardtop / shelter ----
    const RHX = (extAft==null ? HX+0.10 : HX+0.14*bK);
    const RYf = FYt+HS.visor*lK, RYa = (extAft==null ? HYaft-0.15*lK : extAft);
    if(V.region.sig==='stack'){
      // Newfoundland roof: cove-coloured fascia band under a paint lid (the Ocean-Beauty edge)
      boxF([0,(RYf+RYa)/2,ROOFZ+0.030],[RHX+0.02,(RYf-RYa)/2+0.02,0.034],'blue',0.5,-0.008);
      boxF([0,(RYf+RYa)/2,ROOFZ+0.085],[RHX-0.03,(RYf-RYa)/2-0.03,0.028],'cream',0.6,-0.012);
      boxF([0,RYf-0.02,ROOFZ-0.03],[RHX-0.02,0.05,0.06],'blue',0.2);                 // deep visor lip
    } else {
      boxF([0,(RYf+RYa)/2,ROOFZ+0.045],[RHX,(RYf-RYa)/2,0.05],'cream',0.6,-0.01);
      boxF([0,RYf-0.02,ROOFZ-0.02],[RHX-0.04,0.05,0.05],'cream',0.2);                // front visor lip
    }
    if(extAft!=null)   // grab rail under the cantilever's aft lip (trim, not structure)
      tubeF([-(RHX-0.12), extAft+0.10*lK, ROOFZ-0.06],[ (RHX-0.12), extAft+0.10*lK, ROOFZ-0.06],0.035,'steel',0.2);

    lv('rigging');                                // DEDICATED class — signatures + arch gear survive every cut
    // ---- REGION SIGNATURE ----
    let NAVMAST = null;
    if(V.region.sig==='roofrail'){
      // low stainless rail around the cabin top (the Strait sedan look) + flag whips at its aft corners
      const rx=RHX-0.16, ry0=hy(0.06), ry1=hy(0.60), rz=ROOFZ+0.30*dK;
      for(const [px,py] of [[-rx,ry0],[rx,ry0],[-rx,ry1],[rx,ry1]])
        tubeF([px,py,ROOFZ+0.02],[px,py,rz],0.028,'steel',0.1);
      tubeF([-rx,ry0,rz],[rx,ry0,rz],0.026,'steel',0.25);                    // aft rail
      for(const s of [-1,1]) tubeF([s*rx,ry0,rz],[s*rx,ry1,rz],0.026,'steel',s<0?0.15:-0.3);
      for(const s of [-1,1]) tubeF([s*rx,ry0,rz],[s*(rx+0.10),ry0-0.16*lK,ROOFZ+1.75*dK],0.028,'steel',0.3);  // flag whips
      if(!V.style.arch){
        tubeF([0,hy(0.14),ROOFZ],[0,hy(0.12),ROOFZ+0.85*dK],0.04,'steel',0.2);   // anchor-light pole
        boxF([0,hy(0.12),ROOFZ+0.90*dK],[0.05,0.05,0.05],'iron',0.2,-0.02);
        NAVMAST=[0,hy(0.12),ROOFZ+0.90*dK];
      }
    }
    if(V.region.sig==='mastboom'){
      // the Fundy hauling mast + boom: mast at the house aft wall, boom swung aft over the deck
      const MB=[0, HYaft+0.24*lK, ROOFZ-0.02], MT=[0, HYaft-0.06*lK, ROOFZ+2.30*dK];
      tubeF(MB, MT, 0.070*gK, 'steel', 0.2);                                  // mast (raked aft)
      const gz=ROOFZ+0.52*dK, gt=(gz-MB[2])/(MT[2]-MB[2]);
      const GN=[0, MB[1]+(MT[1]-MB[1])*gt, gz];                               // gooseneck on the mast
      const BT=[0, cyf(0.34), ROOFZ+1.10*dK];                                 // boom tip over the deck
      tubeF(GN, BT, 0.048*gK, 'steel', 0.15);                                 // boom
      tubeF(MT, BT, 0.018, 'steel', 0.3);                                     // topping lift
      tubeF(MT, [0, fy(0.90), fz(0.90)+0.06], 0.018, 'steel', 0.25);          // forestay
      boxF([BT[0],BT[1],BT[2]-0.06],[0.06,0.06,0.07],'iron',0.15,-0.02);      // tip block
      tubeF([0,BT[1],BT[2]-0.12],[0,BT[1],BT[2]-0.42*dK],0.020,'iron',0.0);   // fall + hook
      boxF([0, MT[1], MT[2]+0.05],[0.05,0.05,0.05],'iron',0.2,-0.02);         // masthead light
      NAVMAST=[0, MT[1], MT[2]+0.05];
    }
    if(V.region.sig==='stack'){
      // Newfoundland dry stack through the roof, starboard aft — dark iron with a rain cap
      lv('house');                                // the stack is the funnel — it stands on the house roof
      const sx=0.52*HX, sy2=HYaft+0.30*lK, sz=ROOFZ+(Z.id==='inshore'?0.68:0.95)*dK;
      tubeF([sx,sy2,ROOFZ-0.04],[sx,sy2,sz],0.085*gK,'iron',0.35);
      tubeF([sx,sy2,sz],[sx,sy2,sz+0.10],0.105*gK,'iron',-0.5);               // cap ring
      boxF([sx,sy2,sz+0.13],[0.13*gK,0.13*gK,0.022],'iron',0.5,-0.02);        // rain cap
      if(!V.style.arch){
        lv('rigging');                            // the light pole is a spar
        tubeF([-0.40*HX,hy(0.14),ROOFZ],[-0.40*HX,hy(0.12),ROOFZ+0.80*dK],0.04,'steel',0.2);
        boxF([-0.40*HX,hy(0.12),ROOFZ+0.85*dK],[0.05,0.05,0.05],'iron',0.2,-0.02);
        NAVMAST=[-0.40*HX,hy(0.12),ROOFZ+0.85*dK];
      }
    }

    lv('rigging');                                // arch, dome, pods, floods, whips — class-tagged
    // ---- stainless arch over the cabin roof (hardtop / shelter), fit-out by size ----
    if(V.style.arch){
      const ATZ = ROOFZ + 0.96*dK;
      const ARY = hy(V.region.sig==='mastboom' ? 0.58 : 0.45);   // keep clear of the Fundy mast
      const ALX = HX-0.06, ATX = 0.68*HX;
      tubeF([-ALX, ARY, ROOFZ+0.02],[-ATX, ARY-0.05, ATZ],0.06*gK,'steel',0.15);
      tubeF([ ALX, ARY, ROOFZ+0.02],[ ATX, ARY-0.05, ATZ],0.06*gK,'steel',-0.4);
      tubeF([-ATX, ARY-0.05, ATZ],[ ATX, ARY-0.05, ATZ],0.055*gK,'steel',0.25);
      for(const s of [-1,1]) tubeF([s*(ATX-0.04), ARY-0.05, ATZ-0.02],[s*(ATX-0.04), ARY+0.55*lK, ATZ-0.24],0.04*gK,'steel',s<0?0.1:-0.3);
      if(Z.floods){
        tubeF([-0.94*gK, ARY+0.16*lK, ATZ-0.40],[0.94*gK, ARY+0.16*lK, ATZ-0.40],0.045*gK,'steel',0.2);
        for(const x of [-0.6,-0.2,0.2,0.6]) boxF([x*gK, ARY+0.20*lK, ATZ-0.40],[0.07,0.03,0.05],'glas',0.4,-0.05);
      }
      if(Z.radar){
        tubeF([0, ARY-0.05, ATZ+0.03],[0, ARY-0.05, ATZ+0.22],0.33*gK,'cream',0.15);   // radar dome
        boxF([0, ARY-0.05, ATZ+0.24],[0.33*gK,0.26*gK,0.03],'cream',0.5);
      }
      if(Z.pods) for(const x of [-0.66,0.66]) tubeF([x*gK, ARY-0.05, ATZ+0.02],[x*gK, ARY-0.05, ATZ+0.16],0.10*gK,'cream',0.3);
      for(const s of [-1,1]) tubeF([s*0.80*gK, ARY-0.02, ATZ],[s*1.00*gK, ARY-0.34, ATZ+2.03*dK],0.033*gK,'steel',0.3);
      if(Z.pods) for(const s of [-1,1]) tubeF([s*0.52*gK, ARY-0.02, ATZ],[s*0.60*gK, ARY-0.50, ATZ+1.55*dK],0.030*gK,'steel',0.25);
      boxF([0, ARY-0.05, ATZ+0.02],[0.05,0.05,0.05],'iron',0.2,-0.02);
      if(!NAVMAST) NAVMAST=[0, ARY-0.05, ATZ];
      if(Z.raft){   // liferaft canister stowed on the cabin top, aft of the screen, clear of the arch
        lv('house');                              // stowed on the house's lid — goes with the house
        const yR = hy(0.24);
        tubeF([-0.46*gK, yR, ROOFZ+0.20],[0.46*gK, yR, ROOFZ+0.20],0.155*gK,'cream',0.35);
        for(const s of [-1,1]) boxF([s*0.30*gK, yR, ROOFZ+0.20],[0.02,0.17*gK,0.17*gK],'iron',-0.2,0.02);
      }
    }
    if(!NAVMAST) NAVMAST=[0, hy(0.12), ROOFZ+0.85*dK];

    lv('hull');                                   // stanchion rails stand on the washboards — hull structure
    // ---- offshore washboard rails: stanchions + top rail from the house aft to the quarter ----
    if(Z.rails){
      for(const s of [-1,1]){
        const yA=HYaft-0.25*lK, yB=station(0.06).y;
        const NPST=Math.max(3, Math.round((yA-yB)/1.35));
        let prev=null;
        for(let i=0;i<=NPST;i++){
          const yq=yA+(yB-yA)*i/NPST, st=station(V.uOf(yq)), xq=s*(st.ws-TH-0.16*bK), zq=st.kz+st.dep;
          tubeF([xq,yq,zq],[xq,yq,zq+0.50*dK],0.026,'steel',s<0?0.1:-0.25);
          if(prev) tubeF(prev,[xq,yq,zq+0.50*dK],0.030,'steel',s<0?0.2:-0.2);
          prev=[xq,yq,zq+0.50*dK];
        }
      }
    }

    lv('house');                                  // exits the house aft corner
    // ---- side exhaust (wet) at the house aft corner — not on the stack boats, not inshore ----
    if(Z.exhaust && V.region.sig!=='stack')
      tubeF([HX-0.02, HYaft+0.20*lK, DECK+0.60*dK],[HX+0.16, HYaft+0.20*lK, DECK+0.60*dK],0.055,'steel',0.2);
    lv('cockpit');                                // worked from the cockpit — rises from its level
    // ---- hauling block on the starboard washboard, just aft of the house ----
    (function(){ const y=cyf(0.313), st=station(V.uOf(y)), z=st.kz+st.dep, x=0.60*st.ws;
      boxF([x,y,z+0.10],[0.10,0.12,0.10],'iron',0.2);
      tubeF([x-0.12,y,z+0.14],[x+0.12,y,z+0.14],0.05,'steel',0.3); })();

    const built = { F, NAVMAST };
    if(_faceCache.size > 48) _faceCache.delete(_faceCache.keys().next().value);
    _faceCache.set(V.key, built);
    return built;
  }

  // ============================ rasterizer (shared recipe, unchanged) ============================
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
  function projVert(x,y,z,B){
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S - B.heave, d:(yr*B.ce-zr*B.se) };
  }
  function _paint(faces, opts, MAT){
    const B=camBasis(opts), MATS=MAT.MATS, RINDEX=MAT.RINDEX;
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      const n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.hull;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1]);
      function fillTri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) return;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d;
            const base=Math.floor(fidx);
            const idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    const out=col.slice();
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
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(out[i]) continue;
      let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&col[ny*W+nx]){ touch=true; break; }
      }
      if(touch) out[i]=KEY;
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
  function render(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    const V = resolve(opts), built = facesFor(V);
    const t = Math.max(0,Math.min(1, opts.doorOpen!=null ? +opts.doorOpen : 0));
    let fl = built.F.concat(doorFaces(V,t));
    if(opts.cullLevels && opts.cullLevels.length){ const cut=new Set(opts.cullLevels); fl=fl.filter(f=>!cut.has(f.lv)); }   // pass-5 reference cut; absent → byte-identical
    return _toRGBA(_paint(fl, Object.assign({}, opts, {dir}), matsFor(V.paint)));
  }
  // ---- the sliding aft door — built per render so opts.doorOpen (0..1) can pose it ----
  function doorOf(V){
    const P=windowPlan(V), r3=(n)=>+n.toFixed(3), x0=-0.267*V.HX, x1=0.227*V.HX;
    return { kind:'slide', face:'aft', y:r3(V.HYaft), x0:r3(x0), x1:r3(x1),
      z0:r3(V.HZ0+0.02), z1:r3(P.aft.doorTop), travel:r3((x1-x0)*0.96), clearAt:0.55,
      sillZ:r3(V.DECK), leaf:{ x0:r3(x0), x1:r3(x1) } };
  }
  function doorFaces(V,t){
    const D=doorOf(V), AY=V.HYaft-0.03, sft=t*D.travel, x0=D.leaf.x0+sft, x1=D.leaf.x1+sft;
    const out=[], zg0=Math.max(V.sillZ, D.z0+0.80), zg1=Math.min(V.headZ, D.z1-0.10);
    out.push(faceO([[x0,AY-0.085,D.z0],[x1,AY-0.085,D.z0],[x1,AY-0.085,D.z1],[x0,AY-0.085,D.z1]],[0,-1,0],'cream',-0.35,DBP+0.02));
    if(zg1>zg0+0.10){
      out.push(faceO([[x0+0.07,AY-0.115,zg0],[x1-0.07,AY-0.115,zg0],[x1-0.07,AY-0.115,zg1],[x0+0.07,AY-0.115,zg1]],[0,-1,0],'iron',-0.15,DBP+0.03));
      out.push(faceO([[x0+0.10,AY-0.13,zg0+0.03],[x1-0.10,AY-0.13,zg0+0.03],[x1-0.10,AY-0.13,zg1-0.03],[x0+0.10,AY-0.13,zg1-0.03]],[0,-1,0],'glas',-0.30,DBP+0.04));
    }
    out.push.apply(out, tube([D.x0-0.06,AY-0.10,D.z1+0.05],[D.x1+D.travel+0.06,AY-0.10,D.z1+0.05],0.026,'steel',0.25));  // track
    out.push.apply(out, tube([x1-0.10,AY-0.12,D.z0+0.82],[x1-0.10,AY-0.12,D.z0+1.14],0.020,'steel',0.35));                // pull
    for(const f of out) f.lv='house';             // the leaf is house enclosure — it cuts with the room
    return out;
  }
  // door threshold anchor + open-state report for the enter cue
  function doorMount(dir, opts){
    const o=_opt(opts), V=resolve(o), D=doorOf(V), B=camBasis(Object.assign({},o,{dir}));
    const t=Math.max(0,Math.min(1, o.doorOpen!=null ? +o.doorOpen : 0));
    const p=projVert((D.x0+D.x1)/2, V.HYaft, V.DECK, B);
    const le=projVert(D.leaf.x1+t*D.travel, V.HYaft, V.DECK, B);
    return { x:p.sx, y:p.sy, lead:{x:le.sx,y:le.sy}, open:t, clear:t>=D.clearAt };
  }
  /* THE WHEELHOUSE + CUDDY, published per variant — boatInteriorRig.js builds the rooms inside this
     shell and MUST measure against these exact numbers. */
  function houseOf(v){
    const V=resolve(v), P=windowPlan(V), r3=(n)=>+n.toFixed(3);
    const cudLen=Math.min(0.21*V.L, (0.985-V.SOLE_U)*V.L*0.62);
    return { soleZ:V.DECK, eaveZ:r3(V.HZ1-0.04), yAft:r3(V.HYaft), yFwd:r3(V.FYb),
      hxAt:(y)=>V.HXat(y)-0.06, door:doorOf(V),
      sideGlass:{ runs:P.side.map(w=>[r3(w.y0),r3(w.y1)]), z0:r3(P.sill), z1:r3(P.head) },
      aftGlass:{ x0:r3(P.aft.x[0]*V.HX), x1:r3(P.aft.x[1]*V.HX), z0:r3(P.aft.z0), z1:r3(P.aft.z1) },
      front:{ kind:'screen', yBot:r3(V.FYb), yTop:r3(V.FYt), zBot:r3(V.scrSill),
              glass:{ panes:P.front.panes.map(p=>[r3(p[0]),r3(p[1])]), z0:r3(V.scrSill), z1:r3(V.scrHead) } },
      cuddy:{ soleZ:r3(V.DECK-0.42*V.dK), y0:r3(V.HYfwd), y1:r3(V.HYfwd+cudLen),
              opening:{ x0:r3(-0.44*V.lK), x1:r3(0.44*V.lK), z1:r3(V.HZ0+1.42*V.dK) }, step:{ treads:2 } } };
  }
  function loftOf(v){
    const V=resolve(v);
    return { halfAtZ:(y,z)=>{ const st=V.station(V.uOf(y));
        const fr=Math.max(0,Math.min(1,(z-st.kz)/Math.max(0.2,st.dep))); return lerp(st.wb,st.ws,fr)-TH; },
      sheerZ:(y)=>V.sheerAt(y), station:V.station, L:V.L, TH, DECK:V.DECK, SOLE_U:V.SOLE_U,
      house:houseOf(v), shade:{ GAIN, BIAS, LN, BAYER, KEY, EDGE:0.30 }, cell:{ W, H, cx, cy, S } };
  }
  function interiorEnv(v){ return Object.assign({}, root.LobsterBoatVariantsIso, { loft:loftOf(v) }); }

  /* PASS 5 — ASK A: geometry(v), per variant. Same record shape as the canonical lobster: one
     record per WALKABLE level, DECLARED from the same resolve(v)/houseOf(v) constants the mesh is
     built from — never re-measured off it. Open sky is explicit, never absent. */
  const LEVEL_IDS = { hull:0, cockpit:1, foredeck:2, house:3, cuddy:4, rigging:5 };
  function geometry(v){
    const V=resolve(v), Hh=houseOf(v), C=Hh.cuddy, r3=(n)=>+n.toFixed(3);
    const cl=(y)=>r3(V.sheerAt(y)-0.16);          // cuddy ceiling law — the foredeck underside boatInteriorRig dresses (topAt)
    const fs=(y)=>r3(V.sheerAt(y)-0.05*V.dK);     // foredeck walking surface (rig fz law)
    const yCap=r3(-V.L/2+0.985*V.L);              // foredeck forward cap (FCAP=0.985)
    const roofUnder = V.region.sig==='stack' ? r3(V.ROOFZ+0.030-0.034) : r3(V.ROOFZ+0.045-0.05);
    return {
      schema:'hidden-harbours/hull-geometry@1', hull:'lobsterBoatVariantsIsoRig', units:'m',
      variant:{ size:V.size.id, style:V.style.id, region:V.region.id },
      frame:'+x stbd, +y bow, +z up; origin amidships, keel bottom, centreline',
      ids:Object.assign({}, LEVEL_IDS),
      riggingClass:'rigging — arch, dome, pods, floods, whips, mast & boom, cabin-top rail, light poles: tagged by CLASS, never welded to a cullable room',
      tieBreak:'cockpit and house share one sole z ('+r3(V.DECK)+') — the published ceilings break the tie: house '+r3(Hh.eaveZ)+', cockpit open',
      levels:[
        { id:'house', deck:'house_sole', soleZ:r3(V.DECK), ceilingZ:r3(Hh.eaveZ),
          ceiling:{ kind:'hard', lid:null, z:r3(Hh.eaveZ), of:'wheelhouse eave — the deckhead the interior dresses (houseOf(v).eaveZ)' } },
        { id:'cuddy', deck:'cuddy_sole', soleZ:C.soleZ, ceilingZ:cl(C.y0),
          ceiling:{ kind:'raked', lid:'foredeck', zAft:cl(C.y0), zFwd:cl(C.y1), y0:C.y0, y1:C.y1,
                    of:'foredeck underside = sheerZ(y)-0.16, rising toward the bow; ceilingZ is the honest minimum at the companionway' } },
        { id:'cockpit', deck:'cockpit', soleZ:r3(V.DECK), ceilingZ:null,
          ceiling: V.extAft==null
            ? { kind:'open', lid:null, note:'open boat — the roof stops at the house; the deck is sky' }
            : { kind:'open', lid:null, partial:{ z:roofUnder, y0:r3(V.extAft), y1:r3(V.HYaft),
                of:'hardtop-cantilever underside over the FORWARD cockpit only — aft of y '+r3(V.extAft)+' is sky' } } },
        { id:'foredeck', deck:'foredeck', soleZ:fs(V.HYfwd), ceilingZ:null,
          sole:{ kind:'raked', zAft:fs(V.HYfwd), zFwd:fs(yCap), follows:'sheer - 0.05·dK over y '+r3(V.HYfwd)+'..'+yCap },
          ceiling:{ kind:'open', lid:null } },
      ],
    };
  }
  function faces(v){ return facesFor(resolve(v)).F; }   // the static TAGGED mesh for one variant; the posed leaf is the exported doorFaces(opts)

  // ============================ deck anchors (cell px; pass rock(i) to ride the wave) ============================
  const _opt=(opts)=>(typeof opts==='number'?{elev:opts}:(opts||{}));
  function helmSeat(dir, opts){
    const o=_opt(opts), V=resolve(o), B=camBasis(Object.assign({},o,{dir}));
    const p=projVert(0, V.hy(0.16), V.DECK+0.02, B); return {x:p.sx, y:p.sy};
  }
  function haulerMount(dir, opts){
    const o=_opt(opts), V=resolve(o), B=camBasis(Object.assign({},o,{dir}));
    const y=V.cy(0.313), st=V.station(V.uOf(y));
    const p=projVert(0.60*st.ws, y, st.kz+st.dep+0.14, B); return {x:p.sx, y:p.sy};
  }
  function tubSlots(V){
    return [[-0.38,0.450],[0.38,0.450],[-0.38,0.649],[0.38,0.649],[0,0.847]].map(([xf,yf])=>{
      const y=V.cy(yf), st=V.station(V.uOf(y)); return {x:xf*st.ws, y, z:V.DECK}; });
  }
  function tubMounts(dir, opts){
    const o=_opt(opts), V=resolve(o), B=camBasis(Object.assign({},o,{dir}));
    return tubSlots(V).map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  function navMounts(dir, opts){
    const o=_opt(opts), V=resolve(o), B=camBasis(Object.assign({},o,{dir}));
    const built=facesFor(V), s7=V.station(0.95), s0=V.station(0);
    const pt=(x,y,z)=>{ const p=projVert(x,y,z,B); return {x:p.sx,y:p.sy}; };
    return {
      port:  pt(-(s7.ws-0.10), s7.y+V.bowRake(0.95,1)-0.2*V.lK, s7.kz+s7.dep+0.06),
      star:  pt( (s7.ws-0.10), s7.y+V.bowRake(0.95,1)-0.2*V.lK, s7.kz+s7.dep+0.06),
      stern: pt(0, s0.y+0.05, s0.kz+s0.dep+0.10),
      mast:  pt(built.NAVMAST[0], built.NAVMAST[1], built.NAVMAST[2]),
    };
  }
  // boat-local anchor table (metres) — what the gameplay sidecar is generated from
  function anchors(v){
    const V=resolve(v), y=V.cy(0.313), st=V.station(V.uOf(y)), built=facesFor(V);
    return {
      helm:   { x:0, y:+V.hy(0.16).toFixed(3), z:+(V.DECK+0.02).toFixed(3) },
      hauler: { x:+(0.60*st.ws).toFixed(3), y:+y.toFixed(3), z:+(st.kz+st.dep+0.14).toFixed(3) },
      tubs:   tubSlots(V).map(m=>({x:+m.x.toFixed(3), y:+m.y.toFixed(3), z:+m.z.toFixed(3)})),
      mast:   { x:+built.NAVMAST[0].toFixed(3), y:+built.NAVMAST[1].toFixed(3), z:+built.NAVMAST[2].toFixed(3) },
    };
  }

  /* ---- gameplay geometry sidecar, generated per variant ----
     Same schema as Art/gameplay/lobsterBoatIsoRig.gameplay.json. Generated rather than hand-written:
     every number is derived from the same resolve(v) the bake uses, so a rig reshape can never leave
     the sidecar stale. */
  function gameplayGeometry(v){
    const V = resolve(v), r3 = (n)=>+n.toFixed(3);
    const N = 11, US = V.SOLE_U;
    const port = [], star = [];
    for(let i=0;i<=N;i++){ const u=US*(1-i/N), st=V.station(u), w=V.dw(u);
      port.push([r3(-w), r3(st.y)]); star.unshift([r3(w), r3(st.y)]); }
    const fore = [], FCAP=0.985, FN=6;
    for(let i=0;i<=FN;i++){ const u=FCAP-(FCAP-US)*i/FN; fore.push([r3(-V.fw(u)), r3(V.fy(u)), r3(V.fz(u))]); }
    for(let i=0;i<=FN;i++){ const u=US+(FCAP-US)*i/FN; fore.push([r3(V.fw(u)), r3(V.fy(u)), r3(V.fz(u))]); }
    const outer = [];
    for(let i=0;i<=10;i++){ const u=US*i/10, st=V.station(u);
      outer.push([r3(st.ws), r3(st.y), r3(st.kz+st.dep)]); }
    const s0 = V.station(0), a = anchors(v), m = hullMeta(v);
    return {
      schema: 'hidden-harbours/boat-gameplay-geometry@1',
      rig: 'lobsterBoatVariantsIsoRig.js',
      exportSymbol: 'LobsterBoatVariantsIso',
      variant: { size:V.size.id, style:V.style.id, region:V.region.id, paint:V.paint },
      units: 'metres',
      frame: { origin:'amidships / keel-bottom / centreline', axes:'+x starboard, +y bow, +z up',
               scale_px_per_m:PX, LOA_m:V.L, polygon_winding:'CCW viewed from +Z (above)' },
      authoring: 'Generated by LobsterBoatVariantsIso.gameplayGeometry(variant). Rig tuning constants (F/MATS/GAIN/BIAS/LN) stay in the rig. Do not hand-edit — re-generate.',
      extractor_contract: 'per section: rig export -> this sidecar -> absent section = hull does not support the feature (not an error).',
      hull: m,
      DECK: [
        { id:'cockpit', z:r3(V.DECK), winding:'ccw_from_above', polygon:port.concat(star),
          note:'Open working sole aft of the wheelhouse (HYaft='+r3(V.HYaft)+') to the transom; half-width = rig dw().'
               + (V.extAft==null ? ' Fully open to the sky.' : ' Roofed forward of y='+r3(V.extAft)+' by the '+V.style.id+'.') },
        { id:'foredeck', winding:'ccw_from_above', polygon3d:fore,
          note:'Boardable. Raised, follows the raked sheer; z=keelZ+depth-0.05, y+=bowRake. Reached via the washboards.' }
      ],
      WASHBOARD: [
        { side:'starboard', width_m:r3(V.WB), cap:'sheer (z=keelZ+depth per station)',
          inner:'outer edge minus width_m, inboard', outer_edge:outer,
          note:'Transom quarter forward to the foredeck — the walk-forward mooring route.'
               + (V.size.rails ? ' Carries the offshore stanchion rail (0.50 m above the cap).' : '') },
        { side:'port', width_m:r3(V.WB), mirror:'starboard across x=0' }
      ],
      CLEATS: [
        { id:'bow_1', type:'samson_post', pos:[0, r3(V.fy(0.93)), r3(V.fz(0.93)+0.09)], provenance:'stem samson post, u=0.93' },
        { id:'stern_port', type:'cleat', pos:[r3(-(s0.ws-0.22*V.bK)), r3(s0.y+0.28*V.lK), r3(s0.kz+s0.dep+0.03)], provenance:'exact, build()' },
        { id:'stern_star', type:'cleat', pos:[r3( (s0.ws-0.22*V.bK)), r3(s0.y+0.28*V.lK), r3(s0.kz+s0.dep+0.03)], provenance:'exact, build()' }
      ],
      ANCHORS: a,
      _excluded: { hauling_block: 'Starboard washboard hauler+roller is gear, not a tie-off — it is in ANCHORS.hauler.',
                   boom_fall: V.region.sig==='mastboom' ? 'The Fundy boom fall/hook is hauling gear over ANCHORS.hauler, not a tie-off.' : undefined }
    };
  }

  root.LobsterBoatVariantsIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    SIZES, STYLES, REGIONS, PAINTS, resolve, hullMeta, paintRamps, anchors, gameplayGeometry,
    windowPlan, glazingCheck, glazingReport,
    DECKF, GRIP, GLAS, STEEL, IRON, KEY,
    render, ROCK, rock:rockMotion, helmSeat, haulerMount, tubMounts, navMounts,
    doorMount, houseOf, loftOf, interiorEnv, geometry, faces, LEVEL_IDS,
    doorFaces:(opts)=>{ const o=_opt(opts), V=resolve(o), t=Math.max(0,Math.min(1, o.doorOpen!=null?+o.doorOpen:0)); return doorFaces(V,t); } };
})(typeof globalThis!=='undefined'?globalThis:window);

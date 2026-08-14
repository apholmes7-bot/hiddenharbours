/* Hidden Harbours — parametric ISO SPORT SKIFF (fibreglass) — M2 bake recipe, ADR-0006, same
   pipeline as consoleIsoRig.js / puntIsoRig.js. PASS 5: THE BOW RAIL STOPS BEING A RING.
   The console skiff's fancy sister: same ~7.0 m envelope and same anchor set, laid up in glass

   PASS-5 CHANGES — the bow still read wrong after 4b; per-heading crops showed four culprits
   1. THE BOW RAIL WAS THE DARK RING. 80 mm tube, feet on the deck edge, 0.24 m tall, rendered at
      steel's dark steps: at elev 40 the whole assembly projected straight onto the rub-rail line as a
      charcoal arc hugging the foredeck — a second gunwale. Now 56 mm, 0.34 m tall, feet a hand
      inboard, top run BRIGHT (b 0.95) and stanchions at mid-steel (dark stanchions vanished against
      the background on the far side, leaving that side's bright run floating disconnected).
   2. TOPSIDE BANDS BRIGHTEN FORWARD. The constant-width stripe and cove of 4b survived to the point
      but arrived at their DARKEST ramp steps — the bow twists away from the light — so on a white
      hull the teal stripe read as charcoal and re-drew the double-bow ring. B_STRIPE/B_COVE lift
      forward of u 0.62; midships unchanged. The cove also WIDENS toward the stem (fwd taper on
      F_COVE): constant metres is not constant screen px on a foreshortening surface, and 45 mm broke
      into gold dashes up the last two metres of sheer.
   3. ANTI-FOUL LIFT HALVED (0.55 → 0.30) and the keel-band lift eased: at 4b's full lift the forward
      bottom clamped near the top of the ramp and the bow wore a loud bright-teal wedge at the
      quarter headings.
   4. RUB-RAIL INSERT DEEPENED 22 → 56 mm, and (5b) the cap's proud 26 mm lip removed — it was
      sub-pixel everywhere except as an artifact: on the far bow side the 2:1 flare let it hang past
      the silhouette as a detached bright whisker at NE/NW.
   5. (5b) THE RAIL RUNS THE WHOLE BOW as a swept polyline — fourteen short segments from beside the
      step (u 0.665) to one apex over the stem head, a gentle climb aft and a pinch to centreline
      forward, instead of four stanchion-to-stanchion chords that kinked at every joint and dove 50°
      to the deck at the aft end. Run at mid-bright 0.6: lit top over darker underside, readable
      against the pale deck from astern AND against open water on the far side.

   PASS-4b CHANGES — the double bow, root-caused off a per-pixel material map of the stem head
   The hull closed in the last pass, but the stem head still read as a small grey bow nested inside
   the painted hull's bow. Tagging every material with its own colour and dumping the bow region showed
   why: across the forwardmost 8 rows of the E cell there was NO hull paint at all — only deck grey,
   stainless and the black anchor roller. Three things were crowding a stem head 5 px of half-beam
   wide, and one band system could not survive the taper.
   1. NON-SKID STOPS AT 0.955. Carried to the tip, the foredeck capped the stem with a flat grey plate
      3 px wide sitting 0.6 px under the rail cap — it occluded the whole top of the section, so the
      cove and sheer stripe died 8 px short of the point while the rub rail ran on to it. The last
      0.30 m is now GELCOAT, converging to the point: real boats finish a stem head that way, and it
      is what lets the topside bands own the tip. Hull, cove and stripe now run out to one point.
   2. COVE + SHEER STRIPE ARE CONSTANT WIDTH, NOT CONSTANT FRACTION. As fractions of the section they
      shrank with it: forward of station 7 both fell under a pixel, broke into dashes, then vanished.
      fBelow(d) pins an edge d metres under the sheer — which is what a cove line actually is, constant
      width parallel to the sheer. Values are the midships ones from the pass-1 bake, so nothing
      amidships moved; forward, both bands stay solid to the stem head.
   3. RUB RAIL HALVED, 80 mm → 38 mm. Six pixels of bright stainless along the whole sheer was the
      heaviest line on the boat and the outer half of the double bow. One crisp two-value edge now.
   4. Stem head decluttered: anchor roller cut to half section and pulled off the point, bow rail apex
      pulled back to 0.950, bow eye moved aft to 0.905 (dead centre on the 8 px forefoot it punched a
      dark chip straight through the point bow-on).
   5. FOREFOOT READS AS THE VEE CLOSING. 55° of deadrise splits the two bottom panels four ramp steps
      apart, so bow-on the forefoot was the DARKEST anti-foul step hard against the BRIGHTEST — a black
      needle hung off the point. The anti-foul now lifts toward the stem, the keel pad lifts with it and
      narrows 0.065 → 0.030 m, and the vee closes to a fine stem instead of a 4 px black column.
   Verified: 504 combinations (elev 30–50 × 8 rock frames × 8 headings) clear of all four cell edges;
   all 16 scheme × heading renders are a SINGLE connected component with no orphan fragments.
   instead of planked, so nothing about her should read like the workboat at 32 px.

   PASS-3b — the bow read as a bow inside a bow, and this is why
   F. THE FOREDECK CLOSES ON THE SHEER. Its edge was cut 4.5% inboard of the rail cap and held 0.055 m
      under it, and the coaming carrying it down to the lounge closed slowly (t^0.8), so a strip of
      interior liner stayed exposed the whole way round — painted at the cockpit's near-darkest step it
      drew a charcoal ring around the entire foredeck. That ring is the gap you read as a second hull.
      Now the edge lands on the hull's own inner skin one flange-thickness under the cap, the coaming
      closes by station ~6.3 (t^0.45), and the small wall that remains forward is the lounge footwell,
      lit like the recess it is (-0.4) rather than like the cockpit floor (-1.6).
   G. TOE RAIL DELETED. Its two contours sat 1 px inboard of the rub rail's two: five parallel lines
      stacked at the stem head. The rub rail now carries the break alone — bright stainless cap over a
      dark insert, which is both truer and a stronger two-value edge than a same-material lip.
   H. FINE STEM HEAD. Station 8 sheerHalf 0.155 (was 0.26): the head was 0.52 m — 16 px — of blunt deck.
   I. Projections tapered to nothing at the stem: the rub rail cap hung 26 mm past the fine entry as a
      white bar floating off the bow, and the spray rail ran full width to station 8 as a shelf.
   J. Reading fixes: the anchor roller was a bright chip sitting on the stem (dark cheeks, steel pin
      now); the bow eye sat in the anti-foul (up into the boot stripe); the rocket rack's tube bases
      were occluded by the top from ahead so it read as slashes floating over the roof (moved 0.20 m
      inboard); the foredeck forward of the pads was a featureless pale mass (anchor locker lid); the
      bow rail vanished into the pale non-skid at steel's top step (stanchions run dark now).

   PASS-3 CHANGES — all of them about the bow reading clearly in all eight cells
   A. FOREDECK SWEEPS UP. bowZ(u) rises from 0.30 m under the rail cap at the step to 0.055 m under it
      at the stem head, instead of sitting flat at 0.86 m. Flat, it left a 0.40 m deep flared trough
      forward of the step: from every heading the bow read as a hollow scoop that stopped short. Rising,
      the bow is a closed solid — bow-on you read the deck plane against the topsides, broadside the
      sheer and the deck sweep up together. The bow lounge, its toe rail and the bow rail all ride it.
   B. TOPSIDE KNUCKLE at FK=0.62. Above the chine the section used to run dead straight to the sheer:
      one flat plate stem to transom, one value at every heading, hence an egg-shaped bow-on read. Now
      the topside breaks — hard flare below, near-vertical above — scaled by how flared that station
      actually is, so the crease is pronounced at the 2:1 bow and dies to nothing at the transom.
   C. RAKE MEASURED AFT. rakeAt() now pulls the forefoot aft of the stem head instead of running the
      sheer forward of station 8. The rail cap is the forward extreme and 7.0 m LOA is really 7.0 m;
      before, the stem sat 3 px off the cell edge at broadside and clipped outright bow-up at elev 50.
      Swept over elev 30–50 × 8 rock frames × 8 headings: clear of all four edges everywhere.
   D. CUTAWAY FOREFOOT + a keel pad that survives the raster (0.09 m, was 0.02). At 0.02 m the stem was
      a third of a pixel and the forefoot rendered as a hanging 2 px hair — an artifact, not a stem.
   E. Reading fixes found in the same sweep: the trim tabs projected clear of the transom silhouette
      and read as two white bars floating off the stern; the transom top ledge clamped to the brightest
      gelcoat step and read as a slab; the bow-lounge and coaming quads were wound so their normals
      faced down, shading every upholstered surface as if it sat in shadow; the rocket rack raked far
      enough aft to break into a fan of 1 px scratches; the hard top was one blank white octagon.

   PASS-2 CHANGES
   1. DEEP-V HULL. The section is a three-point polyline — narrow keel pad, HARD CHINE, flared
      topside — so every station carries real deadrise and real flare. The chine sits at FC in the 0..1
      skin parameter, which lets the paint bands, the spray rail and the boot stripe all key off it.
      Sheer sweeps 1.00 m aft to 1.42 m at the stem (the workboat's is flat at 0.72), the entry is
      fine, the bow flares 2:1 over the chine. Beam 2.54 m at station 3.
   2. HARD T-TOP replaces the domed bimini: a moulded fibreglass slab on an octagonal plan over four
      stainless legs, forward brace pair to the windscreen frame, electronics pod and radome on top,
      a five-tube rocket-launcher rack on the aft edge, whip antenna, and a depth-tested CONTACT
      SHADOW on the sole so it does not float.
   3. BOW DECK cut inboard of the rail cap and the step riser built from the hull's own inner section at
      each height (halfAtZ), so no deck or bulkhead corner punches out through the topsides. The
      painted liner starts at whatever the local sole height is, so there is no gap either.
   4. COLOURWAYS. Paint is data, not baked pixels. render(dir,{scheme:'…'}) picks a preset;
      render(dir,{paint:{hull,stripe,cove,bottom,deck,top,console,uph}}) mixes any base colours.
      rampFrom(hex,n) derives each ramp in OKLCH (locked lightness curve, chroma capped at C_CAP,
      shadows rotated cool / highlights warm) so a free choice cannot go off-model; chipWall() is the
      legal swatch grid a picker offers. Four fleet schemes are shared with the punt and the console
      skiff value for value; five are sport-only.
   5. WHEEL IS A LAYER. renderWheel(dir,{steer|deg, elev, roll,pitch,heave}) bakes the destroyer
      wheel into the HULL's own cell at the HULL's pivot, so it foreshortens correctly at all eight
      headings, hides behind the console bow-on, and rides the same rock(i) as the hull.
   6. Fit-out: wraparound windscreen with side wings, dash with plotter, forward console seat, twin
      helm chairs on a leaning post with rod holders, upholstered coaming bolsters, bow lounge
      cushions, transom livewell, trim tabs, spray rails, stainless rub rail with a dark insert,
      anchor roller, four cleats, bow eye.

   Cell 244x216, pivot (122,120) = boat origin (amidships, keel bottom, centreline), pinned every
   heading. 32 px = 1 m. Outboards ship as their own layer — MOUNTS carries the twin clamp points
   plus the single centre MOUNT; pass the hull's rock(i) values so overlays ride the same wave.
   Exposes globalThis.SportSkiffIso = { W,H,PX,DIRS,pivot,order,ROCK,rock(i),render(dir,opts),
   renderWheel,wheelFaces,WHEEL_GEO,WHEEL_LOCK, motorMount,MOUNT,motorMounts,MOUNTS,
   helmSeat,HELM, wheelHub,WHEEL_HUB, painter,PAINTER, sternEye,STERN_EYE, TUBS,tubMounts,
   PILOT,pilotStand, SCHEMES,schemeIds,defaultScheme,palette,rampFrom,chipWall,C_CAP,
   PAINT,TRIM,STEEL,DECKF,CANV,GLAS,MOTO,KEY }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 244, H = 216, cx = 122, cy = 120;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const ROCK = { frames: 8, rollA: 3.8, pitchA: 2.2, heaveA: 1.5, period: 2.4 };  // light glass hull, livelier
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 7.0, TH = 0.05;
  const DECK = 0.46;          // cockpit sole — the vee eats interior depth, so she sits higher than the workboat
  const FORE_U = 0.72;        // step riser station
  const TOE = 0.018;          // foredeck tucks this far under the rail cap at the stem head — the
                              // thickness of the bonding flange, sub-pixel, so deck and cap read as one
  const COAM0 = 0.30;         // ...and this far under it at the step: the bow-lounge footwell
  const NSEG = 20;

  // ---- fixed ramps (never painted) -------------------------------------------------------------
  const PAINT = ['#5d6a70','#7e8c90','#a3b0b1','#c2cdca','#dde5df','#eef0ea','#f7f8f3'];  // hand-tuned white gelcoat
  const TRIM  = ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'];                      // fleet teal
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];            // stainless
  const DECKF = ['#6a7069','#848a82','#9ca29a','#b4bab0','#cad0c5'];                      // moulded non-skid
  const CANV  = ['#2a5750','#3d7469','#559182','#74ad97','#97c6ab'];
  const GLAS  = ['#16333c','#24505a','#3a7680','#5fa3a6','#8fc9c4'];                      // windscreen glass
  const MOTO  = ['#101317','#1d2127','#2b323a','#3d454e','#525c63','#6b767b','#8a9499'];  // dark fittings
  const KEY   = '#101a19';

  // ---- paint mixer (OKLCH): any base colour -> a fleet-legal ramp -------------------------------
  // Shared envelope with puntIsoRig.js and consoleIsoRig.js: the rig owns the ramp SHAPE, the caller
  // only picks base colours. That is what keeps a free colour choice from going off-model.
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

  // ---- colourways --------------------------------------------------------------------------------
  // Slots: hull (topsides gelcoat), stripe (boot + sport sheer stripes), cove (hairline), bottom
  // (anti-foul below the chine), deck (moulded non-skid), top (T-top shell), console, uph
  // (upholstery: cushions + coaming bolsters).
  const SCHEMES = {
    'gelcoat-white': { name:'Gelcoat White', hull:'#eef0ea', stripe:'#2ba39a', cove:'#e0b13a', bottom:'#2ba39a',
                       deck:'#b4bab0', top:'#f2f3ee', uph:'#d9c79a',
                       ramps:{ hull:PAINT, stripe:TRIM, deck:DECKF },   // pass-1 bake, kept exact
                       note:'the yard\u2019s own \u2014 white glass, twin teal stripes' },
    'seafoam':       { name:'Seafoam',       hull:'#a3d9c8', stripe:'#7a2f22', cove:'#e6ddc6', bottom:'#7a2f22',
                       deck:'#c6c9bd', top:'#f2f3ee', uph:'#c8b58c',
                       note:'mint topsides, oxblood boot, bone top' },
    'atlantic-navy': { name:'Atlantic Navy', hull:'#254a6b', stripe:'#eef0ea', cove:'#e0b13a', bottom:'#8c3f2c',
                       deck:'#b4bab0', top:'#f2f3ee', console:'#eef0ea', uph:'#d9c79a',
                       note:'deep blue hull, white boot and top' },
    'oyster-bone':   { name:'Oyster Bone',   hull:'#e6ddc6', stripe:'#22354a', cove:'#22354a', bottom:'#22354a',
                       deck:'#b8b7ac', top:'#eef0ea', uph:'#9aa6a4',
                       note:'bone gelcoat, navy boot, grey hides' },
    'graphite':      { name:'Graphite',      hull:'#343b41', stripe:'#c9ccc4', cove:'#e0b13a', bottom:'#8c3f2c',
                       deck:'#8d938c', top:'#3d454b', console:'#3d454b', uph:'#8f8a7e',
                       note:'dark glass, grey stripe, black top' },
    'bottle-green':  { name:'Bottle Green',  hull:'#31694c', stripe:'#e2dcc7', cove:'#e0b13a', bottom:'#8c3f2c',
                       deck:'#c3bda6', top:'#e2dcc7', uph:'#cdb98d',
                       note:'shared with the workboats, value for value' },
    'cranberry':     { name:'Cranberry',     hull:'#a8452f', stripe:'#e6ddc6', cove:'#e0b13a', bottom:'#8c3f2c',
                       deck:'#c0bcae', top:'#e6ddc6', uph:'#e6ddc6',
                       note:'bog red \u2014 the fleet\u2019s loudest hull' },
    'squall-grey':   { name:'Squall Grey',   hull:'#8a99a1', stripe:'#22354a', cove:'#c9ccc4', bottom:'#7c3a2a',
                       deck:'#909c99', top:'#5d6f7a', uph:'#a9b0a8',
                       note:'workaday grey-blue, shared with the punt' },
    'tar-black':     { name:'Tar Black',     hull:'#2a2f33', stripe:'#2ba39a', cove:'#e0b13a', bottom:'#2ba39a',
                       deck:'#7f867e', top:'#2ba39a', uph:'#74ad97',
                       note:'tarred topsides, teal stripe and top' },
  };
  const DEFAULT_SCHEME='gelcoat-white';
  const SLOTS=['hull','stripe','cove','bottom','deck','top','console','uph'];

  const _pal={}; let _palOrder=[];
  function palette(o){
    o=o||{};
    const DF=SCHEMES[DEFAULT_SCHEME];
    const base = o.paint || SCHEMES[o.scheme] || DF;
    const hull=base.hull||DF.hull, stripe=base.stripe||DF.stripe, cove=base.cove||DF.cove;
    const bottom=base.bottom||stripe, deck=base.deck||DF.deck;
    const top=base.top||DF.top, cons=base.console||hull, uph=base.uph||DF.uph;
    const key=[hull,stripe,cove,bottom,deck,top,cons,uph,base.ramps?'baked':'mix'].join('|');
    if(_pal[key]) return _pal[key];
    const R=base.ramps||{};
    const hullR   = R.hull   || rampFrom(hull,7);
    const stripeR = R.stripe || rampFrom(stripe,5,{anchor:0.75});
    const coveR   = R.cove   || rampFrom(cove,3,{anchor:0.85});
    const botR    = bottom===stripe ? stripeR : (R.bottom||rampFrom(bottom,5,{anchor:0.75}));
    const deckR   = R.deck   || rampFrom(deck,5,{anchor:0.70});
    const topR    = R.top    || rampFrom(top,5,{anchor:0.72});
    const consR   = cons===hull ? hullR : (R.console||rampFrom(cons,7));
    const uphR    = R.uph    || rampFrom(uph,5,{anchor:0.72});
    // dith = ordered-dither weight. 0 rounds to the nearest ramp step — crisp banded gelcoat, which
    // is what glass wants; the moulded non-skid and the hides keep a little grain so they read as
    // texture rather than noise.
    const mats = { paint:{ramp:hullR,off:0,dith:0},    stripe:{ramp:stripeR,off:-1,dith:0},
                   cove:{ramp:coveR,off:-1,dith:0},    bottom:{ramp:botR,off:-1,dith:0},
                   liner:{ramp:hullR,off:0,dith:0},    deck:{ramp:deckR,off:0,dith:0.28},
                   cons:{ramp:consR,off:0,dith:0},     top:{ramp:topR,off:0,dith:0},
                   uph:{ramp:uphR,off:0,dith:0.18},    steel:{ramp:STEEL,off:0,dith:0},
                   glas:{ramp:GLAS,off:0,dith:0},      moto:{ramp:MOTO,off:0,dith:0},
                   blk:{ramp:MOTO,off:-2,dith:0} };
    const rindex={};
    [hullR,stripeR,coveR,botR,deckR,topR,consR,uphR,STEEL,GLAS,MOTO,PAINT].forEach(r=>r.forEach((c,i)=>{ if(!rindex[c]) rindex[c]={r,i}; }));
    const out={ key, id:o.paint?'custom':(SCHEMES[o.scheme]?o.scheme:DEFAULT_SCHEME), name:base.name||'Custom',
                base:{hull,stripe,cove,bottom,deck,top,console:cons,uph}, note:base.note||'',
                ramps:{hull:hullR,stripe:stripeR,cove:coveR,bottom:botR,deck:deckR,top:topR,console:consR,uph:uphR},
                mats, rindex };
    _pal[key]=out; _palOrder.push(key);
    if(_palOrder.length>24){ delete _pal[_palOrder.shift()]; }
    return out;
  }

  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- section table: stern(0) -> bow(8) --------------------------------------------------------
  // [sheerHalf, chineHalf, keelHalf, chineRise, sheerRise, keelZ] (m). Deadrise = atan(chineRise /
  // (chineHalf-keelHalf)): ~17 deg at the transom winding up to ~55 deg at the forefoot. Flare =
  // sheerHalf/chineHalf, 1.07 aft and 2.0 at station 7 — that is the bow you see from three-quarters.
  // PASS 3 — stations 7 and 8 carry a CUTAWAY FOREFOOT (keelZ 0.26 / 0.54, was 0.18 / 0.40) and a keel
  // pad wide enough to survive the raster (0.09 m, was 0.02). At 0.02 m the stem was a third of a pixel
  // and the forefoot rendered as a 2 px hair hanging under the hull — an artifact, not a stem. sheerRise
  // drops to match, so the sheer still tops out at 1.42 m and the cell height is unchanged.
  const T = [
    [1.16, 1.09, 0.05, 0.34, 0.98, 0.02],   // transom          sheer 1.00
    [1.22, 1.13, 0.05, 0.37, 0.99, 0.01],   //                        1.00
    [1.26, 1.14, 0.04, 0.41, 1.01, 0.00],   //                        1.01
    [1.27, 1.11, 0.04, 0.46, 1.05, 0.00],   // max beam 2.54 m        1.05
    [1.25, 1.03, 0.04, 0.52, 1.10, 0.00],   //                        1.10
    [1.18, 0.89, 0.03, 0.58, 1.16, 0.02],   //                        1.18
    [1.06, 0.69, 0.03, 0.62, 1.21, 0.07],   //                        1.28
    [0.80, 0.42, 0.045,0.56, 1.12, 0.26],   // fine entry, cutaway forefoot  1.38
    [0.155,0.10, 0.030,0.30, 0.84, 0.58],   // fine stem head         1.42
  ];
  const FC = 0.34;      // where the chine sits in the 0..1 skin parameter
  // PASS 3 — TOPSIDE KNUCKLE. Above the chine the section used to run dead straight to the sheer: one
  // flat plate from stem to transom, so the flare carried a single value at every heading and the bow
  // read as a smooth egg bow-on. Now the topside breaks at FK — hard flare below, near-vertical above —
  // and the break is scaled by how flared that station actually is, so it is pronounced at the 2:1 bow
  // and dies away to nothing at the transom. That crease is the bow's whole read at 32 px/m.
  const FK = 0.62, KB = 0.075, KB_MAX = 0.11;
  const lerp = (a,b,t)=>a+(b-a)*t;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), ch:lerp(A[1],B[1],fr), kw:lerp(A[2],B[2],fr),
             cd:lerp(A[3],B[3],fr), dep:lerp(A[4],B[4],fr), kz:lerp(A[5],B[5],fr), y:-L/2+u*L };
  }
  // PASS 3 — the rake now measures AFT from the stem head instead of forward from station 8, so the
  // rail cap IS the boat's forward extreme and 7.0 m LOA is really 7.0 m. Before, the sheer ran 0.13 m
  // proud of the last station: the stem head sat 3 px off the cell edge at broadside and clipped
  // outright bow-up at elev 50. A forefoot tucking aft is what reads as rake anyway.
  const RAKE=0.21;
  const rakeAt=(u,frac)=>-RAKE*Math.pow(Math.max(0,(u-0.74)/0.26),1.5)*(1-frac);
  // skin(side,u,frac,inset): frac 0 = keel pad, FC = chine, FK = topside knuckle, 1 = sheer.
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
  // frac at an absolute height, and the inner half-width there — this pair is what keeps deck panels
  // and bulkheads INSIDE the hull instead of punching out through the topsides.
  function fracAtZ(st,z){
    const cz=st.kz+st.cd, sz=st.kz+st.dep;
    if(z<=cz) return FC*Math.max(0.02,Math.min(1,(z-st.kz)/Math.max(1e-6,st.cd)));
    return FC+(1-FC)*Math.max(0,Math.min(1,(z-cz)/Math.max(1e-6,sz-cz)));
  }
  const halfAtZ=(u,z,inset)=>skin(1,u,fracAtZ(station(u),z),inset)[0];
  const sheerZ=(u)=>{ const st=station(u); return st.kz+st.dep; };
  // PASS 3 — the foredeck SWEEPS UP to the stem instead of sitting flat 0.40 m below the rail cap.
  // Flat, it left a deep flared trough forward of the step: at every heading the bow read as a hollow
  // scoop that stopped short — the cut-off bow. Rising, the bow is a closed solid, so bow-on you read
  // the deck plane against the topsides and broadside the sheer and the deck sweep up together.
  // PASS 3b — the coaming closes EARLY (t^0.45, was t^0.8). At 0.8 the deck was still 0.14 m under the
  // cap at station 6.9, so a strip of interior liner stayed exposed around the whole foredeck and the
  // bow read as a second shell nested inside the hull. At 0.45 the wall is gone by ~0.90 and the
  // foredeck is flush to the rail cap over most of its length — one bow, one outline.
  function bowZ(u){
    const t=Math.max(0,Math.min(1,(u-FORE_U)/(1-FORE_U)));
    return sheerZ(u) - (COAM0 + (TOE-COAM0)*Math.pow(t,0.45));
  }
  const ZBOW = bowZ(FORE_U);                    // step top
  const soleZ=(u)=>u<FORE_U ? DECK : bowZ(u);   // cockpit sole aft of the step, rising foredeck forward

  // ---- generic solids ----
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

  // ---- face list ----
  const F = [];
  const SHADOWS = [];
  const face=(v,mat,b,db)=>F.push({v,mat:mat||'paint',b:b||0,db:db||0});
  const boxF=(c,h,mat,b,db)=>{ F.push.apply(F, box(c,h,mat,b,db)); };
  const tubeF=(A,B2,rad,mat,b)=>{ F.push.apply(F, tube(A,B2,rad,mat,b)); };
  const prismF=(c,rad,h,n,mat,b,db)=>{ F.push.apply(F, prism(c,rad,h,n,mat,b,db)); };
  // outer paint bands, keyed off the chine: anti-foul below, boot stripe riding the chine, gelcoat
  // topsides, twin sport stripes and a hairline cove up under the rail   [f0, f1, mat, b, db]
  // PASS 4b — the cove and sheer stripe are no longer constant FRACTIONS of the section. A fraction
  // shrinks with the section, so forward of station 7 both bands fell under a pixel and broke into
  // dashes, then vanished entirely at the stem. fBelow(d) pins an edge d metres under the sheer, which
  // is what a cove line actually is: constant width, parallel to the sheer, unbroken to the stem head.
  // The values are the midships ones the pass-1 bake used, so nothing amidships shifts.
  const fB=(d,u)=>{ const st=station(u);
    return Math.min(0.996, Math.max(FK+0.03, fracAtZ(st, st.kz+st.dep-d))); };
  // PASS 5 — the cove WIDENS toward the stem. Constant width in metres is still not constant width on
  // screen: the bow topside twists toward vertical and foreshortens, so 45 mm quantized to 1 px
  // amidships and to alternating 0/1 px forward — gold dashes up the last two metres of sheer.
  const fwd=(u)=>Math.pow(Math.max(0,(u-0.62)/0.38),1.2);
  const F_COVE=(u)=>fB(0.175+0.060*fwd(u),u), F_SHEER=(u)=>fB(0.130,u);
  // PASS 4b — the anti-foul lifts toward the stem. At a flat -0.15 the cutaway forefoot put the DARKEST
  // anti-foul step hard against the BRIGHTEST one (55° of deadrise splits the two bottom panels four
  // ramp steps apart), and bow-on that 8 px wedge read as a black needle hung off the point rather than
  // as the vee closing. Lifting it closes the split to about two steps and the forefoot reads as one mass.
  const B_BOTTOM=(u)=>-0.15+0.30*Math.pow(Math.max(0,(u-0.74)/0.26),1.3);
  // PASS 5 — the topside BANDS brighten toward the stem. The bow surface twists away from the light,
  // so the constant-width stripe and cove of pass 4b survived to the point but arrived at their
  // DARKEST ramp steps: on a white hull a teal stripe at step 0 is charcoal, and the whole foredeck
  // edge read as a dark ring — the same double-bow silhouette by another route. Midships unchanged.
  const B_STRIPE=(u)=>0.22+1.05*fwd(u), B_COVE=(u)=>0.34+0.95*fwd(u);
  const OB = [ [0, FC-0.03,   'bottom', B_BOTTOM, 0],
               [FC-0.03, FC+0.02, 'stripe', 0.35, 0.005],
               [FC+0.02, FK-0.028, 'paint', 0,   0],
               [FK-0.028, FK+0.028, 'paint', 0.5, 0.004],   // knuckle highlight lip
               [FK+0.028, F_COVE, 'paint',  0,    0],
               [F_COVE, F_SHEER,  'cove',    B_COVE, 0.006],
               [F_SHEER, 1,       'stripe',  B_STRIPE, 0.004] ];
  const fv=(f,u)=>typeof f==='function'?f(u):f;
  const bv=(b,u)=>typeof b==='function'?b(u):(b||0);

  // T-top plan (octagon), tilted a touch down at the front
  const TT = { hx:0.68, ya:-1.30, yf:0.86, z:2.22, th:0.055, ch:0.20, tilt:0.06 };
  const ttZ=(y)=>TT.z - TT.tilt*((y-TT.ya)/(TT.yf-TT.ya));
  function ttPlan(dz, shrink){
    const hx=TT.hx-(shrink||0), ya=TT.ya+(shrink||0), yf=TT.yf-(shrink||0), c=TT.ch;
    const P=(x,y)=>[x,y,ttZ(y)+dz];
    return [ P(-hx+c,yf), P(hx-c,yf), P(hx,yf-c), P(hx,ya+c), P(hx-c,ya), P(-hx+c,ya), P(-hx,ya+c), P(-hx,yf-c) ];
  }

  (function build(){
    // ---- hull skin, spray rails, rub rail, liner, bolsters -------------------------------------
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,fv(f0,u0)),skin(side,u1,fv(f0,u1)),
                skin(side,u1,fv(f1,u1)),skin(side,u0,fv(f1,u0))],mat,bv(b,u0),db);
        // spray rail: proud lip on the chine, gelcoat on top, anti-foul underneath. Tapers out before
        // the stem — held full width to u=1 it projected past the fine entry as a floating shelf.
        const srt=(u)=>0.055*(1-Math.pow(Math.max(0,Math.min(1,(u-0.76)/0.20)),1.3));
        const ca=skin(side,u0,FC), cb=skin(side,u1,FC);
        const sp0=srt(u0), sp1=srt(u1);
        const oc0=[ca[0]+side*sp0,ca[1],ca[2]+0.014], oc1=[cb[0]+side*sp1,cb[1],cb[2]+0.014];
        const dnp=(p)=>[p[0],p[1],p[2]-0.052];
        face([ca,cb,oc1,oc0],'paint',0.8,0.03);
        face([oc0,oc1,dnp(oc1),dnp(oc0)],'bottom',-1.25,0.03);
        // stainless rub rail capping the sheer: bright cap over a dark insert, so the deck-to-topside
        // break is one strong two-value line. PASS 5b — the cap sits ON the sheer, no outboard lip:
        // the proud 26 mm was sub-pixel everywhere except as an artifact — on the far bow side the
        // 2:1 flare let it hang past the silhouette as a detached bright whisker at NE/NW.
        const oa=skin(side,u0,1), ob2=skin(side,u1,1);
        const inb=(p)=>[p[0]-side*TH*0.76,p[1],p[2]];
        const or0=[oa[0],oa[1],oa[2]+0.002], or1=[ob2[0],ob2[1],ob2[2]+0.002];
        face([or0,or1,inb(ob2),inb(oa)],'steel',0.8,0.04);
        // PASS 5 — insert deepened 22 → 56 mm. At 0.7 px it rendered on almost no rows, so where the
        // bow flare pulls the topside inboard the bright cap hung past the silhouette with raw
        // background between: a detached white hair down the far bow side at the quarter headings.
        face([or0,or1,[or1[0],or1[1],or1[2]-0.056],[or0[0],or0[1],or0[2]-0.056]],'blk',0.15,0.04);
        // keel pad. PASS 4b — it converges to a fine stem and brightens as it goes. Held 0.13 m wide and
        // at the darkest anti-foul step all the way forward, the pad's face tilts toward the camera as the
        // forefoot rakes up: bow-on it was a 4 px black column down the centre of the bow with the lit
        // starboard panel hard beside it, which read as a needle hung off the point, not as the vee closing.
        const kb=-1.0+1.0*Math.pow(Math.max(0,(u0-0.72)/0.28),1.2);
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'bottom',kb);
        // painted liner, local sole up to the sheer, 2 bands. Forward of the step the exposed band is
        // the shallow lounge footwell wall, which catches sky — at the cockpit's -1.6 it rendered as a
        // charcoal ring around the whole foredeck and split the bow into two nested shells.
        const lb = u0>=FORE_U ? -0.4 : -1.6;
        const fa=fracAtZ(station(u0),soleZ(u0)), fb=fracAtZ(station(u1),soleZ(u1));
        for(let k=0;k<2;k++){
          const g0a=fa+(1-fa)*k/2, g1a=fa+(1-fa)*(k+1)/2;
          const g0b=fb+(1-fb)*k/2, g1b=fb+(1-fb)*(k+1)/2;
          face([skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'liner',lb);
        }
        // upholstered coaming bolster along the cockpit sides — extruded INBOARD off the inner skin,
        // which is the whole trick: pushed outboard it punches through the topsides
        if(u1<=FORE_U-0.02 && u0>=0.05){
          const b0=skin(side,u0,0.90,1), b1=skin(side,u1,0.90,1);
          const inn=(p)=>[p[0]-side*0.045,p[1],p[2]-0.15];
          const dn=(p)=>[p[0],p[1],p[2]-0.05];
          // wind by side: mirrored quads need the reverse order or their normals point down and the
          // whole port bolster shades as if it were in shadow
          const capQ=[b0,b1,inn(b1),inn(b0)];
          face(side>0?capQ:capQ.slice().reverse(),'uph',0.35,0.02);
          const faceQ=[inn(b0),inn(b1),dn(inn(b1)),dn(inn(b0))];
          face(side>0?faceQ:faceQ.slice().reverse(),'uph',-0.5,0.02);
        }
      }
    }

    // ---- cockpit sole (moulded non-skid) + hatch ------------------------------------------------
    const DSEG=15;
    const dw=(u)=>Math.max(0.02, halfAtZ(u,DECK,1)*0.97);
    for(let i=0;i<DSEG;i++){
      const u0=FORE_U*i/DSEG, u1=FORE_U*(i+1)/DSEG;
      face([[-dw(u0),station(u0).y,DECK],[dw(u0),station(u0).y,DECK],
            [dw(u1),station(u1).y,DECK],[-dw(u1),station(u1).y,DECK]],'deck',-0.35);
    }
    (function(){   // sole hatches: two aft fish boxes and a centre inspection lid, proud moulded lids
      const lid=(x0,x1,y0,y1)=>{
        const z=DECK+0.014;
        face([[x0,y0,z],[x1,y0,z],[x1,y1,z],[x0,y1,z]],'deck',-1.9,-0.02);
        face([[x0,y0,z],[x1,y0,z],[x1,y0,DECK],[x0,y0,DECK]],'deck',-2.9,-0.02);
        face([[x1,y0,z],[x1,y1,z],[x1,y1,DECK],[x1,y0,DECK]],'deck',-2.4,-0.02);
        face([[x0,y1,z],[x0,y0,z],[x0,y0,DECK],[x0,y1,DECK]],'deck',-2.4,-0.02);
      };
      lid(-0.88,-0.16,-2.88,-2.08); lid(0.16,0.88,-2.88,-2.08);
      lid(-0.34,0.34,-1.86,-1.28);
      boxF([-0.52,-2.48,DECK+0.024],[0.05,0.035,0.008],'steel',0.8,-0.03);
      boxF([ 0.52,-2.48,DECK+0.024],[0.05,0.035,0.008],'steel',0.8,-0.03);
    })();

    // ---- step riser at the bow bulkhead: trapezoid on the hull's own inner section ---------------
    (function(){
      const u=FORE_U, y=station(u).y;
      const xb=halfAtZ(u,DECK,1)*0.97, xt=halfAtZ(u,ZBOW,1)*0.96;
      face([[-xt,y,ZBOW],[xt,y,ZBOW],[xb,y,DECK],[-xb,y,DECK]],'cons',-1.4);              // riser face
      face([[-xt,y,ZBOW],[xt,y,ZBOW],[xt,y+0.07,ZBOW],[-xt,y+0.07,ZBOW]],'deck',0.7);     // step nose
      boxF([0,y-0.03,DECK+0.15],[0.17,0.02,0.05],'steel',0.6,-0.02);                      // lower tread
      boxF([0,y-0.05,DECK+0.33],[0.17,0.02,0.05],'steel',0.6,-0.03);                      // upper tread
      for(const s of [-1,1]){                                                             // side lockers in the riser
        const q=[[s*0.30,y-0.01,ZBOW-0.07],[s*xb*0.94,y-0.01,ZBOW-0.07],[s*xb*0.94,y-0.01,DECK+0.09],[s*0.30,y-0.01,DECK+0.09]];
        face(s>0?q.slice().reverse():q,'cons',-0.9,-0.02);
      }
    })();

    // ---- rising foredeck, closing ON the sheer at the stem head ---------------------------------
    // PASS 3b — the deck edge lands on the hull's own inner skin at the rail cap, not 4.5% inboard of
    // it. Inboard, it left a lip of exposed liner all the way round: the deck read as a separate shell
    // sitting inside the hull, which is the bow-within-a-bow. The toe rail goes with it — its two
    // contours sat 1 px inboard of the rub rail's two, stacking five parallel lines at the stem head.
    // The rub rail's bright cap over its dark insert is the break now, and it is a stronger one.
    // PASS 4b — and the non-skid now STOPS at DECK_U. Carried to the tip it capped the stem with a flat
    // grey plate 3 px wide sitting 0.6 px under the rail cap, which occluded the top of the section:
    // the cove and sheer stripe died 8 px short of the point while the rub rail ran on to it. That is
    // the double bow — a grey wedge nested inside the painted hull's own bow, exactly as reported.
    const BSEG=12, DECK_U=0.955;
    const bw=(u)=>Math.max(0.02, halfAtZ(u,bowZ(u),1)-0.006);
    const bdy=(u)=>{ const st=station(u); return st.y+rakeAt(u,fracAtZ(st,bowZ(u))); };
    for(let i=0;i<BSEG;i++){
      const u0=FORE_U+(DECK_U-FORE_U)*i/BSEG, u1=FORE_U+(DECK_U-FORE_U)*(i+1)/BSEG;
      const w0=bw(u0), w1=bw(u1), y0=bdy(u0), y1=bdy(u1), z0=bowZ(u0), z1=bowZ(u1);
      face([[-w0,y0,z0],[w0,y0,z0],[w1,y1,z1],[-w1,y1,z1]],'deck',0.45);
    }
    // painted stem head: the last 0.3 m of foredeck is gelcoat, not non-skid, and it converges to the
    // point. Real boats finish the stem that way — and here it is what lets the topside bands own the
    // tip, so hull, cove and sheer stripe all run out to one point instead of stopping at a grey plate.
    (function(){
      const TIP=0.998, N=5;
      const stemW=(u)=>{ const t=Math.max(0,Math.min(1,(u-DECK_U)/(TIP-DECK_U)));
        return Math.max(0.012, bw(u)*(1-Math.pow(t,0.9)*0.94)); };
      for(let i=0;i<N;i++){
        const u0=DECK_U+(TIP-DECK_U)*i/N, u1=DECK_U+(TIP-DECK_U)*(i+1)/N;
        const w0=stemW(u0), w1=stemW(u1);
        face([[-w0,bdy(u0),bowZ(u0)],[w0,bdy(u0),bowZ(u0)],
              [w1,bdy(u1),bowZ(u1)],[-w1,bdy(u1),bowZ(u1)]],'paint',-0.3);
      }
    })();
    (function(){   // anchor roller: dark cheeks with a stainless pin, let into the deck edge. Pulled off
      // the point and cut to half section — at 0.12 m wide on a 0.20 m stem head it WAS the tip.
      const yS=bdy(0.942), zS=bowZ(0.942);
      boxF([0,yS-0.045,zS+0.020],[0.038,0.080,0.020],'blk',0.1,-0.05);
      boxF([0,yS-0.045,zS+0.034],[0.019,0.055,0.009],'steel',0.75,-0.06);
    })();
    // bow lounge: narrow pads along the deck edge, ending well short of the stem so the head is plain
    // closed deck. Run out to the tip they put tan right at the point and blunted it.
    (function(){
      const uA=0.752, uB=0.878, N=6, TP=0.05, PW=0.20;
      for(const s of [-1,1]) for(let i=0;i<N;i++){
        const u0=uA+(uB-uA)*i/N, u1=uA+(uB-uA)*(i+1)/N;
        const o0=bw(u0)-0.085, o1=bw(u1)-0.085;
        const n0=Math.max(0.05,o0-PW), n1=Math.max(0.05,o1-PW);
        const y0=bdy(u0), y1=bdy(u1), z0=bowZ(u0)+TP, z1=bowZ(u1)+TP;
        const top=[[s*n0,y0,z0],[s*o0,y0,z0],[s*o1,y1,z1],[s*n1,y1,z1]];
        face(s>0?top:top.slice().reverse(),'uph',0.62);
        const inb=[[s*n0,y0,z0],[s*n1,y1,z1],[s*n1,y1,z1-TP],[s*n0,y0,z0-TP]];
        face(s>0?inb:inb.slice().reverse(),'uph',-0.15,0.015);
      }
      // anchor locker lid, forward of the lounge. The deck ahead of the pads is the single biggest
      // flat area on the boat — left blank it read as a featureless pale mass and the bow lost its scale.
      const uL=0.888, uR=0.940, LZ=0.016;
      const wl=Math.max(0.045,bw(uL)-0.075), wr=Math.max(0.032,bw(uR)-0.050);
      const zl=bowZ(uL)+LZ, zr=bowZ(uR)+LZ, yl=bdy(uL), yr=bdy(uR);
      face([[-wl,yl,zl],[wl,yl,zl],[wr,yr,zr],[-wr,yr,zr]],'deck',-1.15,-0.02);
      face([[-wl,yl,zl],[wl,yl,zl],[wl,yl,zl-LZ],[-wl,yl,zl-LZ]],'deck',-2.5,-0.03);
      boxF([0,yl+0.06,zl+0.010],[0.042,0.026,0.010],'steel',0.7,-0.05);   // lifting ring
    })();

    // ---- transom -------------------------------------------------------------------------------
    const tp=(s,f)=>skin(s,0,f);
    for(const [f0,f1,mat,b] of OB)
      face([tp(-1,fv(f0,0)),tp(1,fv(f0,0)),tp(1,fv(f1,0)),tp(-1,fv(f1,0))], mat, bv(b,0)-0.55, 0.005);
    (function(){   // moulded motor pad, transom top centre + twin clamp plates
      const st=station(0), zt=st.kz+st.dep, zb=st.kz+st.dep*0.52, y=st.y-0.035, hx=0.44;
      face([[-hx,y,zb],[hx,y,zb],[hx,y,zt],[-hx,y,zt]],'paint',-0.5,-0.03);
      // transom top ledge. As bright gelcoat it clamped to the top ramp step and read at broadside as a
      // hard white slab hung off the stern; moulded non-skid ties it to the sole instead.
      face([[-hx,y,zt],[hx,y,zt],[hx,st.y+0.10,zt],[-hx,st.y+0.10,zt]],'deck',0.0,-0.03);
      for(const s of [-1,1]) boxF([s*0.34,y-0.02,zt-0.10],[0.10,0.02,0.09],'blk',0.15,-0.04);
      // trim tabs: dark plates tucked under the chine, inboard. Out at x=0.74 in steel they projected
      // clear of the transom silhouette and read as two white bars floating off the stern.
      for(const s of [-1,1]) boxF([s*0.50,st.y-0.045,st.kz+st.cd*0.50],[0.15,0.045,0.016],'blk',0.2,0.02);
      // transom livewell hatch + quarter cleats
      face([[-0.46,st.y+0.13,DECK+0.30],[0.46,st.y+0.13,DECK+0.30],[0.46,st.y+0.13,DECK+0.02],[-0.46,st.y+0.13,DECK+0.02]],'cons',-1.2,-0.02);
      boxF([0,st.y+0.11,DECK+0.32],[0.50,0.05,0.03],'deck',0.5,-0.03);
    })();
    for(const s of [-1,1]){
      boxF([s*0.94,-3.00,station(0.02).kz+station(0.02).dep-0.035],[0.026,0.095,0.036],'steel',0.5,-0.02);
      boxF([s*1.06, 1.30,station(0.69).kz+station(0.69).dep-0.035],[0.026,0.095,0.036],'steel',0.5,-0.02);
    }
    (function(){   // bow eye, up in the boot stripe on the raked stem face where it belongs. Down at
      // 0.40 of the depth it sat in the anti-foul and read as a light chip on the bottom paint.
      // PASS 4b — pulled aft to 0.905: at 0.962 it sat dead centre on the 8 px forefoot wedge and
      // punched a dark chip clean through the point in the bow-on view.
      const u=0.905, st=station(u), z=st.kz+st.dep*0.58;
      boxF([0,st.y+rakeAt(u,fracAtZ(st,z))+0.022,z],[0.026,0.042,0.020],'blk',0.3,-0.03);
    })();

    // ---- bow rail (stainless): three stanchions a side, rail closing at the stem head -----------
    // Feet land on the FOREDECK, inboard of the rail cap. Tubes are 80 mm — oversize for stainless,
    // but at 32 px/m anything finer breaks into unconnected stubs instead of reading as a fence. The
    // stanchions run DARK and the top run bright: at steel's top step the whole rail vanished into the
    // pale non-skid behind it.
    // PASS 5 — the rail was the dark ring. At 80 mm tube on 40 mm radius quads, feet ON the deck edge
    // and only 0.24 m of height, the whole assembly projected straight onto the rub-rail line at
    // elev 40 and rendered at steel's dark steps: a charcoal arc hugging the foredeck that read as a
    // second gunwale. Thinner (56 mm), taller (0.34 m), feet pulled a hand inboard so the arc
    // separates from the sheer line in plan, and the top run rendered BRIGHT so it reads as polished
    // rail against the deck instead of as outline. Stanchions sit at mid-steel: dark they vanished
    // against the background on the far side, leaving that side's bright run floating disconnected.
    // PASS 5b — the run is a SWEPT POLYLINE, not stanchion-to-stanchion chords. Four long tubes
    // kinked at every joint, the aft one dove 50° to the deck as a grey slash across the foredeck,
    // and at glancing headings a long tube's cross-section collapses — the far-side run broke into
    // floating strokes. Twelve short segments follow the deck-edge curve: a gentle climb aft, level
    // over the stanchions, pinching to one apex point off the stem. The run renders at mid-bright
    // (0.6) so the tube keeps a lit top over a darker underside — readable against the pale deck
    // from astern AND against open water on the far side.
    (function(){
      const R=0.028, RR=0.034, RH=0.34;
      const footX=(u)=>Math.max(0.035, bw(u)-0.075);
      const US=[0.740,0.826,0.906];
      for(const s of [-1,1]){
        for(const u of US){
          const x=footX(u), y=bdy(u), z=bowZ(u)+0.050;
          tubeF([s*x,y,z],[s*(x-0.030),y,z+RH],R,'steel',0.2);
        }
        const U0=0.665, U1=0.972, N=14, pts=[];
        for(let k=0;k<=N;k++){
          const t=k/N, u=U0+(U1-U0)*t;
          const ramp=Math.min(1,t/0.14);
          const close=Math.max(0,(t-0.86)/0.14);
          const x=(footX(u)-0.030)*(1-close);
          const h=0.050+RH*ramp*(1-0.68*close*close);
          pts.push([s*x, bdy(u)-0.06*close, bowZ(u)+h]);
        }
        for(let k=0;k+1<pts.length;k++) tubeF(pts[k],pts[k+1],RR,'steel',0.6);
      }
    })();

    // ---- centre console: moulded shell, wraparound screen, dash, forward seat -------------------
    (function(){
      const X0=0.42, XT=0.36, Y0=-0.34, Y1=0.72, YT=0.54, Z0=DECK, ZTA=1.34, ZTF=1.26;
      const A=[-X0,Y0,Z0], B=[X0,Y0,Z0], C=[X0,Y1,Z0], D=[-X0,Y1,Z0];
      const E=[-XT,Y0,ZTA], Fq=[XT,Y0,ZTA], G=[XT,YT,ZTF], Hq=[-XT,YT,ZTF];
      face([Hq,G,C,D],'cons',0.5,-0.01);            // forward face, raked
      face([E,Fq,G,Hq],'cons',0.9,-0.01);           // dash top
      face([A,B,Fq,E],'cons',-0.75,-0.01);          // aft face (wheel lives here)
      face([B,C,G,Fq],'cons',-0.1,-0.01);           // starboard side
      face([D,A,E,Hq],'cons',-0.1,-0.01);           // port side
      // sport flash: a band around the base of the console, wide enough to read at 32 px/m
      for(const s of [-1,1]){
        const q=[[s*(X0+0.005),Y0+0.06,DECK+0.20],[s*(X0+0.005),Y1-0.06,DECK+0.20],[s*(X0+0.005),Y1-0.06,DECK+0.09],[s*(X0+0.005),Y0+0.06,DECK+0.09]];
        face(s>0?q.slice().reverse():q,'stripe',0.15,-0.03);
      }
      // dash panel + plotter screen + throttle binnacle
      face([[-0.30,Y0+0.16,ZTA+0.005],[0.30,Y0+0.16,ZTA+0.005],[0.30,YT-0.09,ZTF+0.005],[-0.30,YT-0.09,ZTF+0.005]],'blk',0.35,-0.02);
      face([[-0.22,Y0+0.22,ZTA+0.02],[0.22,Y0+0.22,ZTA+0.02],[0.22,YT-0.13,ZTF+0.02],[-0.22,YT-0.13,ZTF+0.02]],'glas',0.95,-0.03);
      boxF([0.30,-0.30,1.06],[0.055,0.10,0.10],'blk',0.2,-0.02);
      boxF([0.30,-0.40,1.13],[0.022,0.035,0.06],'steel',0.7,-0.03);
      // recessed door, port side
      face([[-X0-0.004,0.10,1.02],[-X0-0.004,0.52,1.02],[-X0-0.004,0.52,DECK+0.08],[-X0-0.004,0.10,DECK+0.08]],'cons',-0.9,-0.02);
      // wraparound windscreen: centre pane + two wings on a stainless frame
      const ZS=1.66;
      face([[-0.22,YT-0.20,ZS],[0.22,YT-0.20,ZS],[0.26,YT-0.02,ZTF+0.01],[-0.26,YT-0.02,ZTF+0.01]],'glas',1.0,-0.02);
      for(const s of [-1,1]){
        const q=[[s*0.26,YT-0.02,ZTF+0.01],[s*0.34,Y0+0.55,ZTA+0.01],[s*0.33,Y0+0.50,ZS-0.10],[s*0.22,YT-0.20,ZS]];
        face(s>0?q.slice().reverse():q,'glas',0.55,-0.02);
      }
      tubeF([-0.225,YT-0.20,ZS+0.01],[0.225,YT-0.20,ZS+0.01],0.020,'steel',0.75);
      for(const s of [-1,1]) tubeF([s*0.225,YT-0.20,ZS+0.01],[s*0.33,Y0+0.50,ZS-0.10],0.018,'steel',0.5);
      // forward console seat: moulded base, cushion, low backrest
      boxF([0,0.94,DECK+0.19],[0.36,0.20,0.19],'cons',-0.35);
      boxF([0,0.94,DECK+0.42],[0.39,0.23,0.05],'uph',0.55);
      boxF([0,0.74,DECK+0.62],[0.36,0.05,0.16],'uph',0.3);
    })();

    // ---- helm: leaning post with twin chairs, footrest, rod holders ------------------------------
    (function(){
      const YH=-0.92;
      boxF([0,YH,DECK+0.23],[0.44,0.19,0.23],'cons',-0.4);
      boxF([0,YH-0.19,DECK+0.34],[0.40,0.03,0.11],'blk',-0.9,-0.02);   // tackle drawer face
      for(const s of [-1,1]){
        boxF([s*0.23,YH,DECK+0.50],[0.20,0.22,0.055],'uph',0.6);        // seat cushion
        boxF([s*0.23,YH-0.20,DECK+0.72],[0.19,0.045,0.20],'uph',0.35);  // backrest
        boxF([s*0.23,YH-0.20,DECK+0.93],[0.19,0.05,0.025],'steel',0.7); // backrest cap
      }
      tubeF([-0.30,YH+0.16,DECK+0.06],[0.30,YH+0.16,DECK+0.06],0.019,'steel',0.25);   // footrest
      for(let k=0;k<4;k++){   // rod holders raked aft off the post
        const x=-0.30+k*0.20;
        tubeF([x,YH-0.26,DECK+0.86],[x,YH-0.44,DECK+1.10],0.020,'blk',0.3);
      }
    })();

    // ---- hard T-top: octagonal moulded slab, four legs, forward braces, pod, radome, rack --------
    (function(){
      const topP=ttPlan(0,0), botP=ttPlan(-TT.th,0.012), inP=ttPlan(0.002,0.085);
      // the slab is the biggest single surface on the boat: split it into a perimeter band and an inner
      // panel one step apart, or from above it is a blank white octagon with a pod stuck to it
      for(let k=0;k<topP.length;k++){ const k2=(k+1)%topP.length;
        face([topP[k2],topP[k],inP[k],inP[k2]],'top',0.35,-0.01); }
      face(inP.slice().reverse(),'top',0.95,-0.012);
      face(botP,'top',-1.5,-0.01);
      for(let k=0;k<topP.length;k++){ const k2=(k+1)%topP.length;
        face([topP[k],topP[k2],botP[k2],botP[k]],'top',0.1,-0.01); }
      // legs
      const legs=[[-0.54,-1.06],[0.54,-1.06],[-0.54,0.66],[0.54,0.66]];
      for(const [lx,ly] of legs){
        const tx=lx*1.16, ty=ly+(ly<0?-0.12:0.10);
        tubeF([lx,ly,DECK],[tx,ty,ttZ(ty)-TT.th],0.030,'steel',0.3);
      }
      // side rails between the legs + forward braces down to the screen frame
      for(const s of [-1,1]){
        tubeF([s*0.62,-1.14,1.54],[s*0.62,0.72,1.54],0.019,'steel',0.4);
        tubeF([s*0.62,0.76,ttZ(0.76)-TT.th],[s*0.30,0.36,1.70],0.019,'steel',0.5);
      }
      tubeF([-0.62,-1.20,ttZ(-1.20)-TT.th-0.01],[0.62,-1.20,ttZ(-1.20)-TT.th-0.01],0.020,'steel',0.45);
      // electronics pod + radome on the roof
      boxF([0,-0.44,ttZ(-0.44)+0.055],[0.21,0.15,0.055],'blk',0.55,-0.02);
      boxF([0,0.28,ttZ(0.28)+0.020],[0.10,0.10,0.020],'blk',0.2,-0.02);
      prismF([0,0.28,ttZ(0.28)+0.038],0.135,0.10,10,'top',0.85,-0.03);
      // rocket-launcher rack. Sat right on TT.ya the top occluded every tube base from ahead and the
      // rack read as five dark slashes floating above the roof — moved 0.20 m inboard so it reads
      // planted, and shortened, since what shows above the shell is all that has to carry it.
      const ry=TT.ya+0.20;
      tubeF([-0.52,ry,ttZ(ry)+0.02],[0.52,ry,ttZ(ry)+0.02],0.026,'blk',0.3);
      for(let k=0;k<5;k++){ const x=-0.46+k*0.23;
        tubeF([x,ry,ttZ(ry)+0.02],[x,ry-0.09,ttZ(ry)+0.20],0.032,'blk',0.4); }
      // whip antenna, stood up at the port end of the rack so the cluster reads as one fitting
      tubeF([-0.60,ry+0.02,ttZ(ry)+0.02],[-0.62,ry-0.04,ttZ(ry)+0.54],0.026,'blk',0.5);
      // spreader light under the forward edge
      boxF([0,TT.yf-0.10,ttZ(TT.yf)-TT.th-0.03],[0.075,0.045,0.028],'steel',0.3,-0.02);
      // depth-tested contact shadow, thrown down-light onto whatever is under the top
      const sh=ttPlan(0,0.02).map(([x,y])=>[x+0.30,y-0.24,DECK+0.004]);
      SHADOWS.push({v:sh, steps:1});
    })();
  })();

  // ---- rasterizer (shared recipe) ----
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){
    return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];
  }
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) };
  }
  function projVert(x,y,z,B,G){
    const gx=G?G.cx:cx, gy=G?G.cy:cy;
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:gx+xr*S, sy:gy-(yr*B.se+zr*B.ce)*S - B.heave, d:(yr*B.ce-zr*B.se) };
  }
  function _paint(faces, opts, doEdge, G, shadows){
    const PW=G?G.W:W, PH=G?G.H:H;
    const B=camBasis(opts);
    const pal=palette(opts), MATS=pal.mats, RINDEX=pal.rindex;
    const zbuf=new Float32Array(PW*PH).fill(Infinity);
    const col=new Array(PW*PH).fill(null);
    const dep=new Float32Array(PW*PH);
    const tag=new Int8Array(PW*PH);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B,G));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
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
          let w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          let w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          let w2=1-w0-w1;
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
    const out=new Array(PW*PH).fill(null);
    for(let i=0;i<PW*PH;i++) out[i]=col[i];
    if(opts.onlyTag){                       // overlay pass: keep only the layer's own pixels
      for(let i=0;i<PW*PH;i++) if(tag[i]!==opts.onlyTag){ out[i]=null; col[i]=null; }
    }
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
    for(const sq of (shadows||[])){
      const rv=sq.v.map(([x,y,z])=>projVert(x,y,z,B,G)), steps=sq.steps||1;
      for(let t=1;t+1<rv.length;t++){
        const a=rv[0], b=rv[t], c=rv[t+1];
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(PW-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(PH-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) continue;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const i=y*PW+x; if(!out[i]) continue;
          const sd=w0*a.d+w1*b.d+w2*c.d;
          if(dep[i] < sd-0.06) continue;
          const e=RINDEX[out[i]]; if(e && e.i>0) out[i]=e.r[Math.max(0,e.i-steps)];
        }
      }
    }
    for(let y=0;y<PH;y++) for(let x=0;x<PW;x++){
      const i=y*PW+x; if(out[i]) continue;
      let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy;
        if(nx>=0&&nx<PW&&ny>=0&&ny<PH&&col[ny*PW+nx]){ touch=true; break; }
      }
      if(touch) out[i]=KEY;
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
  function render(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    return _toRGBA(_paint(F, Object.assign({}, opts, {dir}), true, null, SHADOWS));
  }

  // ---- iso wheel layer --------------------------------------------------------------------------
  // A 0.36 m destroyer wheel is ~11 px on this camera, so it cannot be a flat sprite: it is built as
  // real geometry on the console's aft face and rendered into the HULL's cell at the HULL's pivot.
  // Pass the hull's rock(i) and it rides the same wave; pass steer (or deg) and the spokes turn about
  // the column axis in 3D, which keeps the cove-gold king spoke readable at every heading.
  const WHEEL_HUB = { x:0, y:-0.36, z:1.10 };
  const WHEEL_GEO = { rad:0.180, rimIn:0.150, rake:12, spokes:6, seg:14, hubR:0.054 };
  const WHEEL_LOCK = 1.5;
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
      const a=th0+2*Math.PI*k/G.spokes, w=0.021;
      const ta=a+Math.PI/2, wx=w*(Math.cos(ta)*e1[0]+Math.sin(ta)*e2[0]),
            wy=w*(Math.cos(ta)*e1[1]+Math.sin(ta)*e2[1]), wz=w*(Math.cos(ta)*e1[2]+Math.sin(ta)*e2[2]);
      const A=at(G.hubR*0.8,a,0.012), B=at(G.rimIn+0.004,a,0.012);
      const off=(p,s)=>[p[0]+wx*s,p[1]+wy*s,p[2]+wz*s];
      push([off(A,1),off(B,1),off(B,-1),off(A,-1)], k===0?'cove':'steel', k===0?0.7:0.55);
      const K=at(G.rad+0.028,a,0.006);
      push([off(K,1),[K[0]+n[0]*0.024+wx,K[1]+n[1]*0.024+wy,K[2]+n[2]*0.024+wz],
            [K[0]+n[0]*0.024-wx,K[1]+n[1]*0.024-wy,K[2]+n[2]*0.024-wz],off(K,-1)], k===0?'cove':'blk', 0.1);
    }
    for(let k=0;k<8;k++){
      const a0=th0+2*Math.PI*k/8, a1=th0+2*Math.PI*(k+1)/8;
      push([P0,at(G.hubR,a0,0.020),at(G.hubR,a1,0.020),P0],'steel',0.8);
    }
    return out;
  }
  function wheelDeg(o){
    o=o||{};
    if(o.deg!=null) return o.deg;
    const turns=o.turns==null?WHEEL_LOCK:o.turns;
    return Math.max(-1,Math.min(1,o.steer||0))*turns*360;
  }
  function renderWheel(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    // painted together with the hull, then masked to the wheel's own pixels — that is what makes the
    // console hide the wheel bow-on instead of the layer floating over it
    return _toRGBA(_paint(F.concat(wheelFaces(wheelDeg(opts))), Object.assign({}, opts, {dir, onlyTag:1}), true));
  }

  const _anchor=(P)=>function(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(P.x, P.y, P.z, B);
    return { x:p.sx, y:p.sy };
  };
  const TRANSOM_Z = T[0][5]+T[0][4];
  const MOUNT  = { x:0, y:-L/2-0.035, z:TRANSOM_Z };
  const MOUNTS = [ {x:-0.34, y:-L/2-0.055, z:TRANSOM_Z-0.10}, {x:0.34, y:-L/2-0.055, z:TRANSOM_Z-0.10} ];
  const motorMount = _anchor(MOUNT);
  function motorMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return MOUNTS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  const HELM = { x:0, y:-0.92, z:DECK+0.56 };          // leaning-post cushion top
  const helmSeat = _anchor(HELM);
  const wheelHub = _anchor(WHEEL_HUB);
  const PAINTER = (function(){ const u=0.962, st=station(u), z=st.kz+st.dep*0.58;
    return { x:0, y:st.y+rakeAt(u,fracAtZ(st,z))+0.025, z }; })();
  const painter = _anchor(PAINTER);
  const STERN_EYE = { x:0, y:-L/2+0.02, z:T[0][5]+T[0][3]*0.5 };
  const sternEye = _anchor(STERN_EYE);
  const TUBS = [ {x:-0.56,y:-2.10,z:DECK}, {x:0.56,y:-2.10,z:DECK}, {x:0,y:1.28,z:DECK} ];
  function tubMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  const PILOT = { x:0, y:-0.62, z:DECK };
  const pilotStand = _anchor(PILOT);

  root.SportSkiffIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], PAINT, TRIM, STEEL, DECKF, CANV, GLAS, MOTO, KEY,
    render, ROCK, rock:rockMotion,
    renderWheel, wheelFaces, WHEEL_GEO, WHEEL_LOCK,
    motorMount, MOUNT, motorMounts, MOUNTS, helmSeat, HELM, wheelHub, WHEEL_HUB,
    painter, PAINTER, sternEye, STERN_EYE, TUBS, tubMounts, PILOT, pilotStand,
    L, DECK, ZBOW, FC,
    SCHEMES, schemeIds:Object.keys(SCHEMES), defaultScheme:DEFAULT_SCHEME, SLOTS,
    palette, rampFrom, chipWall, C_CAP };
})(typeof globalThis!=='undefined'?globalThis:window);

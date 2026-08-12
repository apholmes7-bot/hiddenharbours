/* Hidden Harbours — NAVIGATION BUOY RIG. Aids to navigation: the marks that tell the
   player where the channel is. Built on Art/isoSolid.js — 45deg steps CW, elev 40,
   32 px = 1 m, dither, no AA, RINGLESS (ADR 0031, {keyline:true} kept as the A/B).

   NOT buoyIsoRig.js. That is the lobster-pot float, 1.2 m of foam in a fisher's colours,
   and it says "my pots are here". This kit says "the channel is here", in IALA Region B
   as the Canadian Coast Guard flies it, and it is steel, and some of it is 6 m tall.

   THREE AXES
     13 TYPES   lateral (can/nun/lighted), 4 cardinals, isolated danger, safe water,
                special, regulatory, mooring, spar.
     5 SIZES    1.2 / 1.8 / 2.0 / 2.4 / 3.0 m hull, matched to 0-10 .. 20-100 m of water.
     3 WEAR     fresh / working / derelict.

   THE MOTION IS THE POINT AND IT IS NOT A SINE WAVE.
   motion() is a live solver: the engine hands it sea state, wave direction, time and the
   buoy's WORLD POSITION, and gets back roll/pitch/heave/yaw/sink in world axes. Three
   things make it read right and all three are size-dependent:

     SLOPE, NOT HEIGHT.  A buoy tilts to the local wave SLOPE (a*k), not its height. A
       harbour chop is 0.35 m high on a 9 m wavelength and tilts a buoy 7 deg; an ocean
       swell is 1.6 m high on a 190 m wavelength and tilts it 1.5 deg while heaving it
       five times as far. Get this wrong and every sea state looks like the same sea.

     THE WATERPLANE AVERAGES.  A hull of radius R spanning a wave of number k sees the
       mean, not the peak: disc-average 2*J1(kR)/(kR) for heave, the moment-average for
       tilt. A 3 m buoy in a 9 m chop straddles a third of a wavelength and shrugs.

     SLOPE-FOLLOW SHARE.  A body only follows the wave surface if its righting moment
       comes from its WATERPLANE. Split GM into BM = R^2/4d (waterplane) and BG (ballast):
       follow = BM/(BM+BG). A flat can is 0.6 and rolls its guts out; a counterweighted
       steel buoy is 0.18 and stands up in a sea; a spar is 0.002 and stays dead vertical,
       which is the entire reason spar buoys exist.

   Then a second-order response about each natural period (T_heave = 2pi*sqrt(d/g),
   T_roll = 2pi*kxx/sqrt(g*GM)) with base-excitation transmissibility and its phase lag,
   so a buoy near resonance pumps, a buoy far above it punches through the crest, and a
   buoy below it just floats. Three wave components at spread headings, so it never
   metronomes. Then the mooring: chain snub on the up-heave, current lean, watch-circle
   yaw. Rough seas overtop the small hulls -- motion() reports `submerged`.

   Exposes globalThis.NavBuoy = { W,H (per type+size), pivot, TYPES, SIZES, WEAR, SEAS,
     LIGHTS, cell(type,size), meta(type,size), render(type,dir,opts), sheet(type,opts),
     motion(type,size,opts), surface(opts), respond(type,size,sea), lightOn(id,t),
     riserPath(type,size,dir,opts) }. */
(function (root) {
  const K = root.IsoSolid;
  const S = 32, DEG = Math.PI/180, KEY = '#0f1614';
  const CE = Math.cos(40*DEG), SE = Math.sin(40*DEG);   // default camera elevation terms
  const G = 9.81, RHO = 1025;

  // ---- palette: fleet registry colours, plus the two the aids-to-nav set needs -------
  const C = {
    green:  '#3d7f4a',   // channel green — a chroma step up from the packet's #4e7d52
    red:    '#b5403a',   // fleet red, verbatim
    yellow: '#e0b13a',   // fleet gold, verbatim
    black:  '#1d2226',
    white:  '#eef0ea',
    orange: '#e0862e',
    blue:   '#2a3b52',
    steel:  '#6f7d84',
    rust:   '#8a4a30',
    growth: '#38492f',
  };
  const DARK = ['#0d1114','#161b1f','#222a2f','#313b41','#455158'];
  const CHAIN = ['#1a1f22','#2b3238','#3e474d','#525e65','#68757c'];

  const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
  const hx=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0');
  function shift(hex, mul, lift, desat){
    const [r,g,b] = K.hex2rgb(hex);
    const l = 0.299*r+0.587*g+0.114*b;
    const m=(c)=>c+(l-c)*(desat||0);
    return '#'+hx(m(r)*mul+lift)+hx(m(g)*mul+lift)+hx(m(b)*mul+lift);
  }
  function rampOf(hex){
    return [shift(hex,0.42,-6,0), shift(hex,0.62,-2,0), hex, shift(hex,1.14,10,0), shift(hex,1.30,26,0)];
  }
  const _rc = {};
  const ramp = (hex) => _rc[hex] || (_rc[hex] = rampOf(hex));

  // ---- the sizes -------------------------------------------------------------------
  /* dia = hull diameter, draft = still-water draft, water = charted depth range it is
     rated for. Straight off the manufacturer ladder in the reference sheet. */
  const SIZES = [
    { id:'s12', label:'1.2 m', dia:1.20, draft:0.52, water:'0–10 m',    role:'harbour, creek mouths, private channels' },
    { id:'s18', label:'1.8 m', dia:1.75, draft:0.78, water:'5–20 m',    role:'the working default — most of the marks in a bay' },
    { id:'s20', label:'2.0 m', dia:2.00, draft:0.98, water:'10–30 m',   role:'main channel, exposed approaches' },
    { id:'s24', label:'2.4 m', dia:2.40, draft:1.30, water:'10–40 m',   role:'shipping channel, lit, radar-fitted' },
    { id:'s30', label:'3.0 m', dia:3.00, draft:1.80, water:'20–100 m',  role:'landfall and offshore — the ones you see from the ferry' },
  ];
  const SIZE_BY = {}; SIZES.forEach((s,i)=>{ SIZE_BY[s.id]=s; s.i=i; });

  // ---- the marks -------------------------------------------------------------------
  /* bands run BOTTOM -> TOP as fractions of the whole painted height (hull AND tower, so
     a cardinal's bands cross both, which is how they are actually painted). Vertical
     stripes take the segment index instead. */
  const TYPES = {
    PortCan:   { shape:'can',    bands:[[0,1,'green']], top:null,   light:'FlG4',
                 name:'Port hand', gloss:'Keep on your LEFT going upstream. Odd numbers. The SHAPE is the mark.' },
    StbdNun:   { shape:'nun',    bands:[[0,1,'red']],   top:null,   light:'FlR4',
                 name:'Starboard hand', gloss:'Keep on your RIGHT going upstream. Even numbers.' },
    PortLit:   { shape:'steel',  bands:[[0,1,'green']], top:'can',  light:'FlG4',
                 name:'Port hand, lighted', gloss:'The steel one. Channel edge in the shipping lane.' },
    StbdLit:   { shape:'steel',  bands:[[0,1,'red']],   top:'cone', light:'FlR4',
                 name:'Starboard hand, lighted', gloss:'The steel one, other side.' },
    CardinalN: { shape:'pillar', bands:[[0,0.5,'yellow'],[0.5,1,'black']], top:'NN', light:'Q',
                 name:'North cardinal', gloss:'Black over yellow. Safe water to the NORTH.' },
    CardinalS: { shape:'pillar', bands:[[0,0.5,'black'],[0.5,1,'yellow']], top:'SS', light:'Q6LFl',
                 name:'South cardinal', gloss:'Yellow over black. Safe water to the SOUTH.' },
    CardinalE: { shape:'pillar', bands:[[0,0.34,'black'],[0.34,0.66,'yellow'],[0.66,1,'black']], top:'EE', light:'Q3',
                 name:'East cardinal', gloss:'Black-yellow-black. Safe water to the EAST.' },
    CardinalW: { shape:'pillar', bands:[[0,0.34,'yellow'],[0.34,0.66,'black'],[0.66,1,'yellow']], top:'WW', light:'Q9',
                 name:'West cardinal', gloss:'Yellow-black-yellow. Safe water to the WEST.' },
    Isolated:  { shape:'pillar', bands:[[0,0.38,'black'],[0.38,0.60,'red'],[0.60,1,'black']], top:'BB', light:'Fl2W5',
                 name:'Isolated danger', gloss:'Black with a red band. A rock, with water all round it.' },
    SafeWater: { shape:'pillar', stripes:['red','white'], top:'B', light:'MoA',
                 name:'Safe water', gloss:'Red and white stripes. Fairway — pass either side.' },
    Special:   { shape:'pillar', bands:[[0,1,'yellow']], top:'X', light:'FlY4',
                 name:'Special purpose', gloss:'All yellow, saltire on top. Cables, spoil, races.' },
    Regulatory:{ shape:'can',    bands:[[0,0.16,'orange'],[0.16,0.86,'white'],[0.86,1,'orange']], top:'mark', light:'FlY4',
                 name:'Regulatory', gloss:'White with orange bands. The shape on it is the rule.' },
    Mooring:   { shape:'sphere', bands:[[0,0.50,'white'],[0.50,0.64,'blue'],[0.64,1,'white']], top:null, light:null,
                 name:'Mooring', gloss:'White with a blue band. Tie up here.' },
    Spar:      { shape:'spar',   bands:[[0,0.62,'green'],[0.62,1,'black']], top:null, light:null,
                 name:'Spar / winter buoy', gloss:'What replaces the steel ones before the ice comes.' },
  };
  const TYPE_IDS = Object.keys(TYPES);
  const WEAR = ['fresh','working','derelict'];

  // ---- lights ----------------------------------------------------------------------
  /* on-intervals in seconds within the period. Quick = 60/min. */
  const q = (n, t0, gap) => { const a=[]; for(let i=0;i<n;i++) a.push([t0+i*(gap||1.0), t0+i*(gap||1.0)+0.32]); return a; };
  const LIGHTS = {
    FlG4:  { col:'green',  period:4.0,  on:[[0,0.6]],                      txt:'Fl G 4s' },
    FlR4:  { col:'red',    period:4.0,  on:[[0,0.6]],                      txt:'Fl R 4s' },
    FlY4:  { col:'yellow', period:4.0,  on:[[0,0.6]],                      txt:'Fl Y 4s' },
    Q:     { col:'white',  period:1.0,  on:[[0,0.32]],                     txt:'Q' },
    Q3:    { col:'white',  period:10.0, on:q(3,0),                         txt:'Q(3) 10s' },
    Q6LFl: { col:'white',  period:15.0, on:q(6,0).concat([[6.4,8.4]]),     txt:'Q(6) + LFl 15s' },
    Q9:    { col:'white',  period:15.0, on:q(9,0),                         txt:'Q(9) 15s' },
    Fl2W5: { col:'white',  period:5.0,  on:[[0,0.4],[1.0,1.4]],            txt:'Fl(2) W 5s' },
    MoA:   { col:'white',  period:6.0,  on:[[0,0.4],[1.0,2.4]],            txt:'Mo(A) W 6s' },
  };
  function lightOn(id, t){
    const L = LIGHTS[id]; if (!L) return false;
    const p = ((t % L.period) + L.period) % L.period;
    for (const [a,b] of L.on) if (p>=a && p<b) return true;
    return false;
  }

  // ---- sea states -------------------------------------------------------------------
  /* Hs = significant height, Tp = peak period. Deep-water dispersion gives the length:
     L = g*T^2/2pi = 1.56*T^2, and it is the RATIO of the two that sets the character.
     spread = heading spread of the three components, deg. */
  const SEAS = {
    calm:    { id:'calm',    label:'Glass calm',  Hs:0.06, Tp:3.4,  spread:26, gust:0.00, ripple:0.004, note:'Harbour at dawn. The chain is what moves.' },
    chop:    { id:'chop',    label:'Harbour chop',Hs:0.38, Tp:2.4,  spread:34, gust:0.35, ripple:0.016, note:'Short and steep. Tilts as hard as a gale and heaves almost nothing.' },
    working: { id:'working', label:'Working sea', Hs:1.05, Tp:4.6,  spread:30, gust:0.55, ripple:0.024, note:'A day you can still haul in.' },
    rough:   { id:'rough',   label:'Rough',       Hs:2.60, Tp:6.6,  spread:38, gust:0.85, ripple:0.034, note:'The small hulls go green over and the chain snubs hard.' },
    swell:   { id:'swell',   label:'Swell, no wind', Hs:1.60, Tp:11.5, spread:14, gust:0.10, ripple:0.009, note:'Long and low. Big slow heave, barely any tilt — nothing else does this.' },
  };
  const SEA_IDS = ['calm','chop','working','rough','swell'];

  // ---- Bessel J0/J1 (A&S 9.4) — the waterplane averaging is a real integral ----------
  function J0(x){ x=Math.abs(x);
    if (x<3){ const t=(x/3)**2;
      return 1-2.2499997*t+1.2656208*t*t-0.3163866*t**3+0.0444479*t**4-0.0039444*t**5+0.00021*t**6; }
    const t=3/x, f=0.79788456-0.00000077*t-0.00552740*t*t-0.00009512*t**3+0.00137237*t**4-0.00072805*t**5+0.00014476*t**6;
    const th=x-0.78539816-0.04166397*t-0.00003954*t*t+0.00262573*t**3-0.00054125*t**4-0.00029333*t**5+0.00013558*t**6;
    return f*Math.cos(th)/Math.sqrt(x);
  }
  function J1(x){ const s=x<0?-1:1; x=Math.abs(x);
    if (x<3){ const t=(x/3)**2;
      return s*x*(0.5-0.56249985*t+0.21093573*t*t-0.03954289*t**3+0.00443319*t**4-0.00031761*t**5+0.00001109*t**6); }
    const t=3/x, f=0.79788456+0.00000156*t+0.01659667*t*t+0.00017105*t**3-0.00249511*t**4+0.00113653*t**5-0.00020033*t**6;
    const th=x-2.35619449+0.12499612*t+0.00005650*t*t-0.00637879*t**3+0.00074348*t**4+0.00079824*t**5-0.00029166*t**6;
    return s*f*Math.cos(th)/Math.sqrt(x);
  }
  // mean surface elevation over a disc of radius R
  const discAvg  = (kR) => kR<1e-4 ? 1 : 2*J1(kR)/kR;
  // moment-weighted mean slope over a disc: 8*J2/(kR)^2, J2 = 2*J1/x - J0
  const discSlope= (kR) => { if (kR<1e-4) return 1; const J2 = 2*J1(kR)/kR - J0(kR); return 8*J2/(kR*kR); };

  // base-excited second-order follower: amplitude ratio and phase lag
  function follow(r, z){
    const a = 1-r*r, b = 2*z*r;
    return { amp: Math.sqrt((1+b*b)/(a*a+b*b)), lag: Math.atan2(b*r*r, a*a+b*b) };
  }

  // ---- hull metrics ------------------------------------------------------------------
  /* SHAPES carry what the physics needs and the geometry builder reads the same numbers,
     so the picture and the motion can never disagree about how deep the thing floats. */
  const SHAPES = {
    can:    { rW:0.50, dMul:1.00, fb:0.95, ballast:0.10, kxxM:0.40 },
    nun:    { rW:0.50, dMul:1.00, fb:1.25, ballast:0.12, kxxM:0.42 },
    pillar: { rW:0.50, dMul:1.05, fb:1.62, ballast:0.55, kxxM:0.46 },
    steel:  { rW:0.50, dMul:1.15, fb:2.05, ballast:1.40, kxxM:0.48 },
    sphere: { rW:0.50, dMul:0.90, fb:1.00, ballast:0.06, kxxM:0.38 },
    spar:   { rW:0.17, dMul:2.60, fb:1.55, ballast:2.50, kxxM:0.44 },
  };
  const _meta = {};
  function meta(type, sizeId){
    const key = type+'|'+sizeId;
    if (_meta[key]) return _meta[key];
    const T = TYPES[type], sz = SIZE_BY[sizeId] || SIZES[1], sh = SHAPES[T.shape];
    const D = sz.dia, R = D*sh.rW, d = sz.draft*sh.dMul;
    const zTop = D*sh.fb;                       // top of paint/tower above still water
    const BM = R*R/(4*d);                       // waterplane metacentric radius
    const BG = sh.ballast*D*0.5 + 0.05;         // ballast lever
    const GM = BM + BG;
    const kxx = sh.kxxM*(zTop + d)/2;
    const Th = 2*Math.PI*Math.sqrt(d/G);        // heave natural period
    const Tr = 2*Math.PI*kxx/Math.sqrt(G*GM);   // roll natural period
    const follow = BM/(BM+BG);                  // share of righting from the waterplane
    const disp = RHO*Math.PI*R*R*d/1000;        // tonnes
    const focal = T.shape==='steel' ? zTop*0.94 : (T.shape==='pillar' ? zTop*0.80 : zTop*0.9);
    const m = { type, size:sizeId, D, R, draft:d, zTop, freeboard:D*sh.fb, BM, BG, GM, kxx,
                Th, Tr, follow, disp, focal, shape:T.shape, water:sz.water };
    _meta[key] = m; return m;
  }

  // ---- the live motion solver ---------------------------------------------------------
  /* opts = { sea, t, x, y, waveDeg, current, currentDeg, depth, seed }
     Returns world-axis values. roll is about +y (so it tips the buoy along +x), pitch is
     about +x. Both are camera-independent: the same numbers are right at all 8 headings,
     which is the whole reason they are solved in world axes and not in the cell. */
  /* THE RIPPLE EARNS ITS PLACE TWICE. A real sea always carries short wind ripple on top
     of whatever swell it has, and without it a long swell renders as a flat plane at
     gameplay zoom — 200 m of wavelength across 35 m of screen is a gradient, not a wave.
     It is also the averaging mechanism made visible: at 2.6 m of wavelength the ripple is
     shorter than the buoys are wide, so the waterplane integral eats most of it and the
     big hulls ignore water the player can plainly see moving. */
  function components(sea, waveDeg){
    const Sst = SEAS[sea] || SEAS.working, a0 = Sst.Hs/2, sp = Sst.spread;
    return [
      { a:a0*0.78,     T:Sst.Tp,       dir:waveDeg,        ph:0.0 },
      { a:a0*0.46,     T:Sst.Tp*0.68,  dir:waveDeg+sp,     ph:2.19 },
      { a:a0*0.30,     T:Sst.Tp*1.42,  dir:waveDeg-sp*0.7, ph:4.71 },
      { a:Sst.ripple,  T:1.35,         dir:waveDeg+34,     ph:1.07, ripple:true },
      { a:Sst.ripple*0.62, T:0.88,     dir:waveDeg-52,     ph:3.61, ripple:true },
      /* Not water. A slow, wide field that PATCHES the ripple — cat's paws. Two clean
         sinusoids of ripple across a whole bay is a woven cloth, and the eye reads the
         regularity instantly. Carries no elevation and no slope; renderers use it as a
         multiplier, the solver skips it. */
      { a:1, T:3.75, dir:waveDeg+68, ph:0.83, gust:true },
    ].map(c=>{ c.L = 1.56*c.T*c.T; c.k = 2*Math.PI/c.L; c.w = 2*Math.PI/c.T; return c; });
  }
  /* The slope a renderer normalises its shading against. Long wave and ripple are asked
     for SEPARATELY and shaded at separate weights: ripple slope is comparable to swell
     slope in absolute terms — a 3 cm ripple on a 1.2 m wavelength is steeper than a 1.6 m
     swell on 200 m — so normalising against the sum buries the swell under corduroy. */
  function slopeScale(sea, kind){
    let s = 0;
    for (const c of components(sea,0)){ if (c.gust) continue;
      if (kind==='ripple' ? c.ripple : !c.ripple) s += c.a*c.k; }
    return Math.max(0.008, s*0.72);
  }

  /* The free surface, for anything that needs to agree with the buoys — the water shader,
     a second buoy 30 m away, a hull. Same three components, same phase convention. */
  function surface(opts){
    const o = opts||{}, comps = components(o.sea||'working', o.waveDeg||0);
    let eta=0, sx=0, sy=0;
    for (const c of comps){
      if (c.gust) continue;
      const cd=Math.cos(c.dir*DEG), sd=Math.sin(c.dir*DEG);
      const psi = c.k*((o.x||0)*cd + (o.y||0)*sd) - c.w*(o.t||0) + c.ph;
      eta += c.a*Math.cos(psi);
      const sl = -c.a*c.k*Math.sin(psi);
      sx += sl*cd; sy += sl*sd;
    }
    return { eta, slopeX:sx, slopeY:sy };
  }

  function motion(type, sizeId, opts){
    const o = opts||{}, M = meta(type, sizeId);
    const seaId = o.sea||'working', St = SEAS[seaId]||SEAS.working;
    const t = o.t||0, comps = components(seaId, o.waveDeg||0);
    const ZH = 0.42, ZR = 0.16;                       // damping ratios: heave, roll
    let heave=0, tiltX=0, tiltY=0, surge=0, sway=0, eta=0;

    for (const c of comps){
      if (c.gust) continue;
      const cd=Math.cos(c.dir*DEG), sd=Math.sin(c.dir*DEG);
      const psi = c.k*((o.x||0)*cd + (o.y||0)*sd) - c.w*t + c.ph;
      eta += c.a*Math.cos(psi);

      const kR = c.k*M.R;
      const fh = follow(M.Th/c.T, ZH), fr = follow(M.Tr/c.T, ZR);

      // heave: disc-averaged elevation, run through the heave response
      const h = c.a*discAvg(kR)*fh.amp;
      heave += h*Math.cos(psi - fh.lag);

      // tilt: the wave SLOPE (a*k), moment-averaged over the waterplane, times the share
      // of righting that comes from the waterplane, times the roll response
      const th = c.a*c.k*discSlope(kR)*M.follow*fr.amp;
      const s = -th*Math.sin(psi - fr.lag);
      tiltX += s*cd; tiltY += s*sd;

      // deep-water orbital surge, in quadrature with the heave
      const orb = c.a*discAvg(kR)*Math.sin(psi);
      surge += orb*cd; sway += orb*sd;
    }

    // --- the mooring -------------------------------------------------------------------
    // Chain snub: the riser goes taut before the buoy makes the top of a big crest, so the
    // up-heave clips and the down-heave does not. Asymmetry is most of what reads as moored.
    const slack = M.draft*0.85 + (o.depth ? Math.min(0.35, o.depth*0.012) : 0.2);
    if (heave > slack) heave = slack + (heave-slack)*0.34;

    // Current lean + the slow watch-circle wander. A small hull in a tide leans visibly;
    // a 3 m steel buoy does not care.
    const cur = o.current||0, cDeg = (o.currentDeg!=null?o.currentDeg:(o.waveDeg||0));
    const leanK = 0.85/(M.disp+0.6);
    const lean = clamp(cur*cur*leanK, 0, 0.30);       // radians, downstream
    tiltX += lean*Math.cos(cDeg*DEG); tiltY += lean*Math.sin(cDeg*DEG);

    const sd0 = (o.seed||0)*0.7391;
    const wander = St.gust*0.55/(1+M.disp*0.35);
    const yaw = (Math.sin(t*2*Math.PI/19.3 + sd0)*0.62 + Math.sin(t*2*Math.PI/47.1 + sd0*3.1)*0.38)
                * (14 + wander*46) + (cur>0.15 ? cDeg*0.0 : 0);
    surge += Math.sin(t*2*Math.PI/23.7 + sd0)*wander*0.9 + lean*8*Math.cos(cDeg*DEG);
    sway  += Math.cos(t*2*Math.PI/31.1 + sd0)*wander*0.9 + lean*8*Math.sin(cDeg*DEG);

    // --- what the hull sees ------------------------------------------------------------
    const sink = eta - heave;                          // + = water higher than the buoy
    const submerged = clamp((sink - M.freeboard*0.55)/(M.D*0.35), 0, 1);

    return {
      roll:  tiltX/DEG, pitch: tiltY/DEG,
      heave, yaw, surge, sway, sink, submerged, eta,
      /* screen px, ready to use: add heavePx to the pivot to place the sprite, and clip
         the hull at pivotY + waterPx — the waterline rides the LOCAL wave, not the buoy,
         which is why a lagging buoy visibly buries its bow band in a crest. */
      heavePx: -heave*S*CE, waterPx: -sink*S*CE, etaPx: -eta*S*CE,
      focalPx: -(M.focal)*S*CE, lightZ: M.focal + heave,
    };
  }

  /* Steady-state summary for one buoy in one sea — the number that goes in a table.
     Peak tilt and peak heave, not RMS, because that is what an artist is looking at. */
  const _resp = {};
  function respond(type, sizeId, sea){
    const key = type+'|'+sizeId+'|'+sea;
    if (_resp[key]) return _resp[key];
    let mxT=0, mxH=0, mnH=0, sub=0;
    for (let i=0;i<420;i++){
      const m = motion(type, sizeId, { sea, t:i*0.06, waveDeg:0 });
      mxT = Math.max(mxT, Math.hypot(m.roll, m.pitch));
      mxH = Math.max(mxH, m.heave); mnH = Math.min(mnH, m.heave);
      sub = Math.max(sub, m.submerged);
    }
    const M = meta(type, sizeId);
    return (_resp[key] = { tilt:mxT, heave:mxH-mnH, submerged:sub, Th:M.Th, Tr:M.Tr, follow:M.follow });
  }

  // ---- geometry -----------------------------------------------------------------------
  const rotX=(a)=>{ const c=Math.cos(a), s=Math.sin(a);
    return (p)=>[p[0], p[1]*c - p[2]*s, p[1]*s + p[2]*c]; };
  const push=(F,a)=>{ for(const f of a) F.push(f); };

  function ball(F, zc, r, mat, b, seg){
    seg = seg||14; const n=5;
    for (let i=0;i<n;i++){
      const t0=i/n, t1=(i+1)/n;
      const z0=zc-r*Math.cos(Math.PI*t0), z1=zc-r*Math.cos(Math.PI*t1);
      const r0=r*Math.sin(Math.PI*t0), r1=r*Math.sin(Math.PI*t1);
      push(F, K.tube(0,0,z0,z1,r0,r1,seg,mat,{b:(b||0)+(i-2)*0.16}));
    }
  }
  /* apexUp=true -> wide at z0, point at z1. The cardinal topmarks are the one place in
     the kit where getting this backwards ships a north buoy that says south. */
  function cone(F, z0, z1, r, mat, b, seg, apexUp){
    push(F, K.tube(0,0,z0,z1, apexUp?r:0.02, apexUp?0.02:r, seg||12, mat, {b:b||0, caps:apexUp?'bottom':'top', capB:(b||0)-0.2}));
  }
  function lattice(F, z0, z1, r0, r1, mat, bays){
    bays = bays||3;
    const legs=4;
    for (let l=0;l<legs;l++){
      const a=(l/legs)*Math.PI*2+Math.PI/4;
      const ca=Math.cos(a), sa=Math.sin(a);
      for (let bI=0;bI<bays;bI++){
        const t0=bI/bays, t1=(bI+1)/bays;
        const za=z0+(z1-z0)*t0, zb=z0+(z1-z0)*t1;
        const ra=r0+(r1-r0)*t0, rb=r0+(r1-r0)*t1;
        push(F, K.bar([ca*ra,sa*ra,za],[ca*rb,sa*rb,zb], 0.030, mat, 0.15, 0.01));
        // one diagonal per bay per face, alternating hand — an X reads as mush at 32 px/m
        const a2=((l+1)%legs/legs)*Math.PI*2+Math.PI/4;
        const c2=Math.cos(a2), s2=Math.sin(a2);
        const up = (bI+l)%2===0;
        push(F, K.bar([ca*ra, sa*ra, up?za:zb],[c2*rb, s2*rb, up?zb:za], 0.021, mat, -0.05, 0.01));
      }
      const ring=(z,r)=>push(F, K.tube(0,0,z-0.018,z+0.018,r*1.02,r*1.02,10,mat,{b:0.25,db:0.005}));
      if (l===0){ for (let bI=1;bI<bays;bI++){ const t=bI/bays; ring(z0+(z1-z0)*t, r0+(r1-r0)*t); } }
    }
  }

  /* Bands are resolved against the VISIBLE height — waterline to the top of the tower,
     hull and tower as one body. Measuring from the bottom of the paint instead squeezes
     the lowest band into a sliver, because a third of it is under water. */
  function bandFn(T, zLo, zHi){
    if (T.stripes) return null;
    const bands = T.bands||[[0,1,'white']];
    return (z)=>{ const t = clamp((z-zLo)/((zHi-zLo)||1), 0, 0.9999);
      for (const [a,b,c] of bands) if (t>=a && t<b) return c;
      return bands[bands.length-1][2]; };
  }
  // emit a painted tube, slicing it at every band boundary so no segment is two colours
  function painted(F, z0, z1, r0, r1, seg, T, zLo, zHi, opts){
    opts = opts||{};
    if (T.stripes){
      const fs = K.tube(0,0,z0,z1,r0,r1,seg,'M_'+T.stripes[0],opts);
      fs.forEach((f,i)=>{ if (i<seg) f.mat = 'M_'+T.stripes[i%2 ? 1 : 0]; });
      push(F, fs); return;
    }
    const bf = bandFn(T, 0, zHi);
    const cuts = [z0];
    for (const [a,b] of (T.bands||[])){
      for (const t of [a,b]){ const z = zLo+(zHi-zLo)*t; if (z>z0+1e-4 && z<z1-1e-4) cuts.push(z); }
    }
    cuts.push(z1); cuts.sort((p,q)=>p-q);
    for (let i=0;i<cuts.length-1;i++){
      const za=cuts[i], zb=cuts[i+1];
      const ra=r0+(r1-r0)*(za-z0)/((z1-z0)||1), rb=r0+(r1-r0)*(zb-z0)/((z1-z0)||1);
      const o = Object.assign({}, opts);
      if (i>0) o.caps = (opts.caps==='both'||opts.caps==='top') ? 'top' : 'none';
      if (i<cuts.length-2) o.caps = (o.caps==='top'||o.caps==='both') ? 'none' : o.caps;
      push(F, K.tube(0,0,za,zb,ra,rb,seg,'M_'+bf((za+zb)/2),o));
    }
  }

  /* Topmarks carry the meaning and they are usually BLACK sitting on top of a black
     tower, so every one of them gets a stalk with air under it and a ramp-bias lift.
     Without those two things a north cardinal and a south cardinal are the same sprite. */
  function topmark(F, kind, zBase, r, mat, mark){
    const s = r, LIFT = 0.30;
    push(F, K.tube(0,0,zBase-s*0.42, zBase, s*0.13, s*0.13, 8,'STEEL',{b:0.25}));
    switch(kind){
      case 'can':  push(F, K.tube(0,0,zBase,zBase+s*1.5, s*0.62,s*0.62,12,mat,{caps:'top',b:LIFT+0.2,capB:LIFT+0.5})); break;
      case 'cone': cone(F, zBase, zBase+s*1.7, s*0.66, mat, LIFT+0.2, 12, true); break;
      case 'NN':   cone(F, zBase, zBase+s*1.30, s*0.72, mat, LIFT,12,true);        // both points UP
                   cone(F, zBase+s*1.56, zBase+s*2.86, s*0.72, mat, LIFT+0.20,12,true); break;
      case 'SS':   cone(F, zBase, zBase+s*1.30, s*0.72, mat, LIFT,12,false);       // both points DOWN
                   cone(F, zBase+s*1.56, zBase+s*2.86, s*0.72, mat, LIFT+0.20,12,false); break;
      case 'EE':   cone(F, zBase, zBase+s*1.30, s*0.72, mat, LIFT,12,false);       // base to base -> diamond
                   cone(F, zBase+s*1.30, zBase+s*2.60, s*0.72, mat, LIFT+0.14,12,true); break;
      case 'WW':   cone(F, zBase, zBase+s*1.30, s*0.72, mat, LIFT,12,true);        // point to point -> hourglass
                   cone(F, zBase+s*1.30, zBase+s*2.60, s*0.72, mat, LIFT+0.14,12,false); break;
      case 'BB':   ball(F, zBase+s*0.70, s*0.66, mat, LIFT,12);
                   ball(F, zBase+s*2.24, s*0.66, mat, LIFT+0.14,12); break;
      case 'B':    ball(F, zBase+s*0.74, s*0.72, mat, LIFT,12); break;
      case 'X':    push(F, K.bar([-s*0.80,0,zBase],[s*0.80,0,zBase+s*1.7],0.055,mat,LIFT+0.3,0.02));
                   push(F, K.bar([s*0.80,0,zBase],[-s*0.80,0,zBase+s*1.7],0.055,mat,LIFT+0.1,0.01)); break;
      case 'mark': {
        const m = mark||'diamond', cc=[0,0,zBase+s*1.00], h=s*1.02;
        if (m==='circle') ball(F, zBase+s*0.86, h, 'M_orange', LIFT+0.1, 14);
        else if (m==='square') push(F, K.box(cc,[h,0.085,h],'M_orange',LIFT+0.25,0.02));
        else if (m==='cross'){ push(F, K.box(cc,[h,0.085,h*0.28],'M_orange',LIFT+0.25,0.02));
                               push(F, K.box(cc,[h*0.28,0.09,h],'M_orange',LIFT+0.3,0.03)); }
        else push(F, K.box(cc,[h*0.82,0.085,h*0.82],'M_orange',LIFT+0.25,0.02,
                    K.chain(K.move(0,0,-cc[2]), K.rotY(Math.PI/4), K.move(0,0,cc[2]))));
        break;
      }
    }
  }

  const _faces = {};
  function facesOf(type, sizeId, wear, mark){
    const key = type+'|'+sizeId+'|'+wear+'|'+(mark||'');
    if (_faces[key]) return _faces[key];
    const T = TYPES[type], M = meta(type, sizeId), F = [];
    const R = M.R, D = M.D, d = M.draft, sh = T.shape;
    const zLo = -d*0.42;                                   // paint runs below the waterline
    let zHi = M.zTop, seg = D>2 ? 16 : 14;

    // counterweight / tail — everything below the paint is DARK, and it is where the
    // ballast lever in meta() physically lives
    push(F, K.tube(0,0,-d, -d+D*0.10, R*0.80, R*0.97, seg,'DARK',{caps:'bottom',b:-0.4}));
    push(F, K.tube(0,0,-d+D*0.10, zLo, R*0.97, R, seg,'DARK',{b:-0.2}));
    // mooring shackle
    push(F, K.tube(0,0,-d-D*0.10,-d, R*0.14,R*0.20, 8,'STEEL',{caps:'bottom',b:-0.1}));

    if (sh==='can' || sh==='steel'){
      const zDeck = sh==='steel' ? D*0.42 : zHi;
      painted(F, zLo, zDeck, R, R*(sh==='steel'?0.97:0.96), seg, T, 0, zHi, {b:0});
      push(F, K.tube(0,0,zDeck, zDeck+D*0.035, R*(sh==='steel'?0.97:0.96), R*(sh==='steel'?0.90:0.84), seg,
        'M_'+(bandFn(T,0,zHi)?bandFn(T,0,zHi)(zDeck-0.01):(T.stripes?T.stripes[0]:'white')), {caps:'top',b:0.35,capB:0.55}));
      if (sh==='steel'){
        lattice(F, zDeck+D*0.03, D*1.62, R*0.60, R*0.34, 'M_'+(T.bands?T.bands[T.bands.length-1][2]:'red'), 3);
        // lantern cage + lamp
        const zc = D*1.62;
        push(F, K.tube(0,0,zc, zc+D*0.30, R*0.40,R*0.40, 12,'M_'+(T.bands?T.bands[T.bands.length-1][2]:'red'),{b:0.1}));
        push(F, K.tube(0,0,zc+D*0.30, zc+D*0.34, R*0.44,R*0.44, 12,'STEEL',{caps:'top',b:0.4,capB:0.5}));
        push(F, K.tube(0,0,zc+D*0.34, zc+D*0.52, R*0.17,R*0.17, 10,'LENS',{b:0.5}));
        push(F, K.tube(0,0,zc+D*0.52, zc+D*0.58, R*0.17,R*0.06, 10,'STEEL',{caps:'top',b:0.6,capB:0.7}));
        zHi = zc+D*0.58;
      }
      if (T.top) topmark(F, T.top, zHi+D*(sh==='steel'?0.16:0.20), R*0.80, 'M_'+(T.bands?T.bands[T.bands.length-1][2]:'green'), mark);
    }
    else if (sh==='nun'){
      const zShoulder = D*0.42;
      painted(F, zLo, zShoulder, R, R, seg, T, 0, zHi, {b:0});
      painted(F, zShoulder, zHi, R, 0.03, seg, T, 0, zHi, {b:0.1, caps:'top', capB:0.4});
      push(F, K.tube(0,0,zHi,zHi+D*0.10, 0.05,0.05, 8,'STEEL',{caps:'top',b:0.5,capB:0.6}));
    }
    else if (sh==='pillar'){
      const zDeck = D*0.70;
      painted(F, zLo, zDeck, R, R*0.97, seg, T, 0, zHi, {b:0});
      push(F, K.tube(0,0,zDeck,zDeck+D*0.04, R*0.97,R*0.86, seg,'DARK',{caps:'top',b:0.3,capB:0.45}));
      // the pillar carries the bands too — that is how a cardinal reads at distance
      painted(F, zDeck+D*0.04, zHi, R*0.46, R*0.36, 12, T, 0, zHi, {b:0.05});
      const capMat = 'M_'+(T.stripes ? T.stripes[1] : T.bands[T.bands.length-1][2]);
      // lantern
      push(F, K.tube(0,0,zHi, zHi+D*0.09, R*0.40,R*0.40, 12,'STEEL',{b:0.3}));
      push(F, K.tube(0,0,zHi+D*0.09, zHi+D*0.25, R*0.24,R*0.24, 10,'LENS',{b:0.5}));
      push(F, K.tube(0,0,zHi+D*0.25, zHi+D*0.30, R*0.25,R*0.11, 10,'STEEL',{caps:'top',b:0.6,capB:0.7}));
      const topMat = T.top==='X' ? 'M_yellow' : T.top==='B' ? 'M_red'
                   : /^(BB|NN|SS|EE|WW)$/.test(T.top||'') ? 'M_black' : capMat;
      if (T.top) topmark(F, T.top, zHi+D*0.42, R*0.80, topMat, mark);
      zHi = zHi+D*0.30;
    }
    else if (sh==='sphere'){
      // a real ball with its equator near the waterline, not a hull with a lid
      const r = R*1.06, zc = D*0.14, bf = bandFn(T, 0, zc+r), n=8;
      for (let i=0;i<n;i++){
        const u0=-1+2*i/n, u1=-1+2*(i+1)/n;
        const z0=zc+r*u0, z1=zc+r*u1;
        const r0=r*Math.sqrt(Math.max(0.015,1-u0*u0)), r1=r*Math.sqrt(Math.max(0.015,1-u1*u1));
        push(F, K.tube(0,0,z0,z1,r0,r1,seg,'M_'+bf((z0+z1)/2),{b:(i-3.5)*0.13, caps:i===n-1?'top':'none', capB:0.45}));
      }
      push(F, K.tube(0,0,zc+r, zc+r+D*0.16, 0.045,0.045, 8,'STEEL',{caps:'top',b:0.4,capB:0.55}));
    }
    else if (sh==='spar'){
      painted(F, zLo, zHi, R, R*0.86, 10, T, 0, zHi, {b:0});
      push(F, K.tube(0,0,zHi, zHi+D*0.06, R*0.86,0.02, 10,'M_black',{caps:'top',b:0.4,capB:0.5}));
      // float collar at the waterline — what keeps a spar buoy up
      push(F, K.tube(0,0,-D*0.18, D*0.04, R*2.5, R*3.0, 14,'M_'+(T.bands?T.bands[0][2]:'green'),{b:-0.05}));
      push(F, K.tube(0,0, D*0.04, D*0.28, R*3.0, R*2.3, 14,'M_'+(T.bands?T.bands[0][2]:'green'),{b:0.15,caps:'top',capB:0.4}));
    }

    // ---- wear ---------------------------------------------------------------------------
    if (wear!=='fresh'){
      const rnd = K.mulberry(({fresh:1,working:7,derelict:13})[wear]*977 + TYPE_IDS.indexOf(type)*31 + M.D*7);
      const heavy = wear==='derelict';
      for (const f of F){
        if (!f.mat.startsWith('M_') && f.mat!=='STEEL') continue;
        const zc = f.v.reduce((s,v)=>s+v[2],0)/f.v.length;
        const low = zc < D*0.35;
        const r = rnd();
        if (heavy && r < (low?0.30:0.16)) f.mat = 'RUST';
        else if (!heavy && r < (low?0.13:0.05)) f.mat = 'RUST';
        if (zc > M.zTop*0.78 && r > (heavy?0.72:0.90)) f.b = (f.b||0)+0.7;   // guano on the topmark
      }
      // marine growth at the waterline
      push(F, K.tube(0,0, -D*(heavy?0.14:0.09), D*0.03, R*1.008, R*1.008, seg,'GROW',{b:-0.35,db:-0.02}));
    }

    F.__zHi = zHi; F.__zLo = -d - D*0.10;
    _faces[key] = F; return F;
  }

  // ---- cell + pivot ---------------------------------------------------------------------
  const TILT_MAX = 16*DEG;
  const _cells = {};
  function cell(type, sizeId){
    const key = type+'|'+sizeId;
    if (_cells[key]) return _cells[key];
    const M = meta(type, sizeId), F = facesOf(type, sizeId, 'fresh');
    let rmax=0, zmax=-1e9, zmin=1e9;
    for (const f of F) for (const v of f.v){
      rmax=Math.max(rmax, Math.hypot(v[0],v[1])); zmax=Math.max(zmax,v[2]); zmin=Math.min(zmin,v[2]);
    }
    const ct=Math.cos(TILT_MAX), st=Math.sin(TILT_MAX);
    const r2 = rmax*ct + Math.max(Math.abs(zmax),Math.abs(zmin))*st;
    const zt = zmax*ct + rmax*st, zb = zmin*ct - rmax*st;
    const cy = Math.ceil(3 + (r2*SE + zt*CE)*S);
    const H  = Math.ceil(cy + (r2*SE - zb*CE)*S + 3);
    const W  = Math.ceil(2*r2*S) + 6;
    const c = { W: W+(W%2), H: H+(H%2), cx:Math.round((W+(W%2))/2), cy, meta:M };
    _cells[key] = c; return c;
  }

  // ---- render ----------------------------------------------------------------------------
  /* opts = { size, wear, roll, pitch, yaw, heave, elev, lit, mark, keyline, riser }
     roll/pitch/yaw in degrees, world axes — pass motion() straight through. `heave` is in
     METRES and shifts the sprite inside its own cell, so a caller who cannot move its quad
     still gets the bob (an engine that can should place by pivot and pass heave:0). */
  function render(type, dir, opts){
    opts = opts||{};
    const sizeId = opts.size || 's18', wear = opts.wear || 'working';
    const T = TYPES[type] || TYPES.PortCan;
    const c = cell(type, sizeId);
    let F = facesOf(type, sizeId, wear, opts.mark);

    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG, yaw=(opts.yaw||0)*DEG;
    if (roll||pitch||yaw){
      const xf = K.chain(K.rotZ(yaw), K.rotY(roll), rotX(pitch));
      F = F.map(f=>({ v:f.v.map(xf), mat:f.mat, b:f.b, db:f.db, tag:f.tag }));
    }
    const dy = (opts.heave||0)*S*CE;

    const lit = !!opts.lit;
    const MATS = { DARK:{ramp:DARK}, STEEL:{ramp:ramp(C.steel)}, CHAIN:{ramp:CHAIN},
      RUST:{ramp:ramp(C.rust)}, GROW:{ramp:ramp(C.growth)},
      LENS:{ramp: lit ? ['#8a7a44','#c6b268','#efe0a2','#fff6cf','#ffffff'] : ramp('#8d8f7e')} };
    const dim = wear==='derelict' ? {m:0.80,d:0.34} : wear==='working' ? {m:0.92,d:0.16} : {m:1,d:0};
    for (const k in C){
      const base = dim.m===1 ? C[k] : shift(C[k], dim.m, 0, dim.d);
      MATS['M_'+k] = { ramp: ramp(base) };
    }

    return K.paint(F, { W:c.W, H:c.H, cx:c.cx, cy:c.cy - dy, dir, elev:opts.elev, key:KEY,
      keyline: opts.keyline!=null?opts.keyline:false, MATS });
  }
  function sheet(type, opts){ const o=[]; for(let d=0;d<8;d++) o.push(render(type,d,opts)); return o; }

  // ---- the riser -------------------------------------------------------------------------
  /* The chain is not a sprite — 30 m of it is 960 px and no cell holds that. riserPath()
     returns a polyline in CELL PIXELS from the shackle down to the sinker, so the engine
     draws it as a line under the buoy and fades it with depth. In 4 m of clear water you
     see the lot; in 30 m you see the first two segments and a shadow. */
  function riserPath(type, sizeId, dir, opts){
    opts = opts||{};
    const M = meta(type, sizeId), c = cell(type, sizeId);
    const depth = opts.depth!=null ? opts.depth : 8;
    const scope = opts.scope!=null ? opts.scope : 1.45;      // chain length / depth
    const lead = (opts.leadDeg!=null?opts.leadDeg:0)*DEG;
    const drift = Math.min(0.92, Math.sqrt(Math.max(0,scope*scope-1))) * depth * 0.55
                * (0.35 + 0.65*clamp(opts.current||0.3,0,1.2));
    const th = -(dir||0)*Math.PI/4, ct=Math.cos(th), st=Math.sin(th);
    const pts = [];
    const n = 14;
    for (let i=0;i<=n;i++){
      const u = i/n;
      // catenary-ish: hangs steep at the buoy, lies along the ground at the sinker
      const s = Math.pow(u, 1.7);
      const x = drift*s*Math.cos(lead), y = drift*s*Math.sin(lead);
      const z = -M.draft - (depth-M.draft)*(1-Math.pow(1-u,1.55));
      const xr = x*ct - y*st, yr = x*st + y*ct;
      pts.push({ x: c.cx + xr*S, y: (c.cy - (opts.heave||0)*S*CE) - (yr*SE + z*CE)*S, t:u, depth:-z });
    }
    return { pts, shackle:pts[0], sinker:pts[n], ramp:CHAIN };
  }

  root.NavBuoy = { S, TYPES, TYPE_IDS, SIZES, SIZE_BY, WEAR, SEAS, SEA_IDS, LIGHTS, C,
    cell, meta, facesOf, render, sheet, motion, surface, respond, lightOn, riserPath,
    components, slopeScale,
    discAvg, discSlope, follow };
})(typeof globalThis!=='undefined'?globalThis:window);

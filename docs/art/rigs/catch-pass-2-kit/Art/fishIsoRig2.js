/* Hidden Harbours — FISH iso rig, PASS 2 (catch pass 2). fishIsoRig.js is untouched; this is a
   drop-in superset on the same fixed 3/4 turntable as the fleet (45deg steps CW, elev 40,
   32 px = 1 m, ordered dither, no AA).
   STRICT WORLD SCALE: scale 1 = the species' real length (SPECIES.len). A herring is 8 px long,
   a striped bass 24 — there is no readability floor; `scale` is the specimen (SPECIES.range).
   NEW IN PASS 2
   · species: flounder (a true flatfish — depressed body via zflat, fringe fins down both sides,
     eyes on top, undulating swim, lies FLAT on deck), herring (shoaling, silver), striped bass
     (dark lateral stripe band, two dorsals). The four pass-1 blocks are verbatim.
   · swim is 6f with a body-wave lag behind the tail beat (pass 1 wagged from the tail root);
     dart 3f; thrash pitches the head through the surface; + roll 4f (surface roll — belly
     flash) and jump 6f (clears the water: the rig bakes the z arc + pitch, MOTION says how far
     the page moves it forward).
   · fins that read at 20 px: paired pectorals, an anal fin, a second dorsal per species
     (dorsals), a darker gill edge, and dithered spots on the flounder's back.
   · shoal(species, n, t, opts) — RUNTIME SINGLES: world-metre positions + heading + the 8-dir
     bake + swim frame for n fish on one lazy loop. The game blits singles; no group is baked.
     dirOf(heading) snaps any heading to the turntable.
   · RINGLESS (ADR 0031): KEYLINE_DEFAULT = false; render(dir,{outline:true}) is the live A/B.
   Cell 64x80 (pass B; 64x64 before), pivot (32,38) = the water-surface point under the body centre. Pose z = spine
   height over the surface; pixels below waterZ (default 0) bake the depth-graded underwater
   tint + alpha (never clip at runtime); waterZ:null = dry. spoil 0..1 = the rot.
   RESTS keep the pass-1 contract: deck 4 lays · gill 2f · tail 2f · cradle 2f (pivot = THE
   GRIP, pins to CharacterIso hand anchors). hold(species,scale) -> {mass, hands}.
   Exposes globalThis.FishIso2 = { W,H,pivot,KEY,SPOIL,WATER,KEYLINE_DEFAULT,ORDER,SPECIES,
   ANIMS,AORDER,MOTION,POSE,RESTS,RPOSE,defaultElev,render,mouth,hold,sizeOf,project,shoal,
   dirOf,sheetOrder }.
   CATCH REVIEW PASS B (2026-10-27; the owner's rulings of 10-27, "make them read, scale kept" and
   "drawings grow to the weights")
   · SPECIES.range grows to each def's weight band by hold()'s own law (mass ∝ scale³): the mass at
     each end is the band's end within 1%. The cell grows to 64x80 so the top of every range fits
     (a striped bass at 1.76 held by the gill hangs 34 px below the grip); the pivot stays (32,38).
   · the spine (the herring and the flounder, the two bodies thinner than 2 px): a 1 px line down
     the body's axis and the tail's fills any pixel the facets miss, so no cell draws nothing (a
     herring is 1 px across; end-on, or a flatfish edge-on, its facets fall between pixels). It
     takes the base colour of the material SPECIES.spine names: the herring's flank (at elev 40 a
     body 1 px round shows about twice as much flank as back), the flounder's back (its eyed side).
   · the flash (flash:true, the herring): in the water, on the frames FLASH_FRAMES names (two of
     each water anim's; the shadow's one went in pass D), the two brightest flank pixels of a cell catch the
     light, mixed toward WATER at a third of the depth's rate and drawn at alpha 230, so a shoal
     glints out of step as its fish turn.
   · a flatfish held by the gill or the tail turns HANG_ROLL (22.5°) about its length, so no
     heading sees it edge-on.
   · WATER stays #123034: of five others tried on main's eight water presets (the default's deep
     and mid anchors, and three darker), each read better on some draws and worse on others, and
     none on all.
   CATCH REVIEW PASS D (2026-10-28; the owner's ruling 1 of 10-27, "a Midwater school reads as one
   shape")
   · the shadow anim bakes every pixel under the water KEY, the rig's near black, at SHADOW_A (160,
     the depth's alpha above 0.35 m), with no flash: a shape under the surface and no more, no fin,
     stripe or glint. On main's eight water presets it is seen (ΔE76 ≥ 10) on every mid anchor,
     Storm Grey's the least at 11.2; at 115, the depth's alpha at the shadow's 0.5 m, that one
     would read 8.0.
   CATCH REVIEW PASS E (2026-10-28; the owner's ruling of 10-28, "Bigger, with field marks")
   · renderPlate(species, scale) draws the species for the field guide's plate, larger than the world:
     side-on facing left (the flounder from above, as it lies) at 32 px = 1 m x scale, centred in a
     PW x PH (160 x 96) cell. The loft is the world's in finer rings (PLATE_LOFT, 36 x 16 for 9 x 8),
     so the shape and the ramps are the species' own; on it go the marks a field guide shows (MARKS),
     each in the species' own ramps, and on every side-on fish the eye, the gill cover, the mouth and
     the fins' rays. render() takes no loft and no plate cell: every world cell is pass D's, byte for
     byte (until pass F's A).
   · the plate's profile is the world's through the same depths (the snout's root, the shoulder at
     u 0.28, the tail's root) with its two corners rounded (plateG); the snout is a dome of quarter
     rings; side-on, the back, the flank and the belly meet in a dithered band.
   · found in pass E and left as it is: at the world's 8 rings every body facet's mean sine is
     ±0.354 or ±0.854, past the ±0.35 that makes a flank, so a world fish has no flank band. The
     flank ramp draws only on the snout's cone (a pixel or two a cell), the gill band never, and the
     bass's stripes never (0 px in every state and frame). The plate's 16 rings draw all three.
   CATCH REVIEW, A SHOWN FIRST (2026-10-28; the owner's "Preview A", the review's question 6)
   · render(dir, {look:'plate'}) draws the world's cell in the plate's look, as far as it holds at 32 px and
     in motion: the plate's loft (PLATE_LOFT, its profile and its snout) and on it the marks LOOK names, each
     a point or a line on the body's surface, carried through the pose and the camera and depth-tested, so it
     turns with the body (lookMarks). A mark draws where the body is LOOK_MIN (2) px deep or more at its
     station, the gill cover LOOK_GILL (3). The game took no look: every world cell stayed pass E's, byte for
     byte (until pass F), and so did every plate.
   CATCH REVIEW PASS F (2026-10-28; the owner's ruling on the review's question 7, "all leans": A, every
   fish, one look)
   · LOOK_DEFAULT = 'plate': render() draws A unless opts.look says otherwise, so every world cell of every
     fish is A, shown first's, byte for byte, and so is every cell drawn from them (CatchKit2's fills, a
     shoal's singles). render(dir, {look:'world'}) is the live A/B: pass E's cell, byte for byte (the world's
     9 x 8 loft, its stripe, gill and spot facets, no mark). Every plate is pass E's, byte for byte. */
(function (root) {
  const S = 32, DEG = Math.PI/180, DEFAULT_ELEV = 40;
  const W = 64, H = 80, cx = 32, cy = 38, CELL = { W, H, cx, cy };
  const KEY = '#101a19', WATER = '#123034', KEYLINE_DEFAULT = false, LOOK_DEFAULT = 'plate';
  const SPECIES = {
    cod:      { label:'COD', len:0.70, girth:0.105, flat:0.72, stripes:'none', dorsals:2, range:[0.87,1.59], massK:1,
                back:'#6c673b', flank:'#7d7649', belly:'#c2bc90', fin:'#54502c' },
    haddock:  { label:'HADDOCK', len:0.55, girth:0.090, flat:0.68, stripes:'none', dorsals:2, range:[0.83,1.51], massK:1,
                back:'#525f68', flank:'#697379', belly:'#cdd3d2', fin:'#222b30' },
    pollock:  { label:'POLLOCK', len:0.60, girth:0.092, flat:0.70, stripes:'none', dorsals:2, range:[0.91,1.66], massK:1,
                back:'#48543f', flank:'#55614f', belly:'#c2c8ba', fin:'#222a1e' },
    mackerel: { label:'MACKEREL', len:0.45, girth:0.068, flat:0.62, stripes:'bars', dorsals:2, range:[0.72,1.23], massK:1,
                back:'#27564a', stripe:'#173b32', flank:'#7fa79c', belly:'#bcc6c2', fin:'#142e28' },
    bass:     { label:'STRIPED BASS', len:0.75, girth:0.110, flat:0.70, stripes:'lines', dorsals:2, range:[0.60,1.76], massK:1.3,
                back:'#4a5c52', stripe:'#33413e', flank:'#aab8b4', belly:'#e0e7e4', fin:'#3d4a45' },
    flounder: { label:'FLOUNDER', len:0.35, girth:0.080, flat:1.0, zflat:0.24, fringe:true, bottom:true, undulate:true, spine:'back',
                stripes:'none', dorsals:0, range:[0.77,1.66], massK:0.5,
                back:'#5c4a34', flank:'#6b5840', belly:'#d9cfb6', fin:'#4a3a28', spots:'#3a2c1c' },
    herring:  { label:'HERRING', len:0.25, girth:0.028, flat:0.55, stripes:'none', dorsals:1, range:[0.87,1.48], massK:2, shoal:true, flash:true, spine:'flank',
                back:'#2e4d61', flank:'#95adb5', belly:'#dbe4e5', fin:'#3b5563' },
  };
  const ORDER = ['cod','haddock','pollock','mackerel','bass','flounder','herring'];
  const ANIMS = { swim:{n:6,ms:120}, dart:{n:3,ms:80}, thrash:{n:4,ms:110}, shadow:{n:2,ms:280}, roll:{n:4,ms:130}, jump:{n:6,ms:95} };
  const AORDER = ['swim','dart','thrash','shadow','roll','jump'];
  // page-side motion contract (m/s along the heading; jump.travel = metres over the 6 frames)
  const MOTION = { swim:{v:0.35}, dart:{v:1.4}, thrash:{v:0}, shadow:{v:0.18}, roll:{v:0.12}, jump:{v:0.9, travel:0.55} };
  const POSE = {
    swim:   [0,1,2,3,4,5].map(f=>{ const ph=f/6*Math.PI*2;
              return { sweep:0.55*Math.sin(ph), curve:0.16*Math.sin(ph-1.2), z:-0.15+0.006*Math.sin(ph*2), wph:ph }; }),
    dart:   [{sweep:0.30,stretch:1.08,z:-0.20,wph:0},{sweep:0,stretch:1.12,z:-0.20,wph:2.1},{sweep:-0.30,stretch:1.08,z:-0.20,wph:4.2}],
    thrash: [{curve:0.8,roll:0.6,sweep:0.6,pitch:0.25,z:0.02},{curve:-0.8,roll:-0.6,sweep:-0.6,pitch:0.10,z:0.04},
             {curve:0.5,roll:1.1,sweep:0.4,pitch:-0.15,z:0.05},{curve:-0.5,roll:-1.1,sweep:-0.4,pitch:0.05,z:0.02}],
    shadow: [{sweep:0.4,z:-0.50,wph:0},{sweep:-0.4,z:-0.50,wph:3.1}],
    roll:   [{roll:0.35,curve:0.25,sweep:0.2,z:0.0},{roll:1.5,curve:0.3,sweep:-0.2,z:0.01},
             {roll:2.7,curve:0.2,sweep:0.2,z:0.0},{roll:1.5,curve:-0.2,sweep:-0.2,z:-0.02}],
    jump:   [{z:0.03,pitch:0.95,sweep:0.5,roll:0.15,curve:0.15},{z:0.20,pitch:0.65,sweep:-0.4,roll:0.3,curve:-0.1},
             {z:0.34,pitch:0.20,sweep:0.3,roll:0.35,curve:0.1},{z:0.36,pitch:-0.25,sweep:-0.2,roll:0.3,curve:-0.1},
             {z:0.22,pitch:-0.65,sweep:0.3,roll:0.1,curve:0.15},{z:0.05,pitch:-0.95,sweep:-0.5,roll:0,curve:-0.15}],
  };
  const HPI = Math.PI/2;
  const RPOSE = {
    deck:   [{roll:HPI,curve:0.25},{roll:-HPI,curve:-0.2},{roll:HPI,curve:-0.3},{roll:-HPI,curve:0.15}],
    gill:   [{pitch:1.48,curve:0.12,gripU:0.13},{pitch:1.48,curve:-0.15,gripU:0.13}],
    tail:   [{pitch:-1.48,curve:0.10,gripU:0.95},{pitch:-1.48,curve:-0.12,gripU:0.95}],
    cradle: [{roll:0.35,curve:0.10,gripU:0.5},{roll:-0.35,curve:-0.10,gripU:0.5}],
  };
  const RESTS = ['deck','gill','tail','cradle'];
  const HANG_ROLL = Math.PI/8;                   // a flatfish hung by the gill or the tail: never edge-on
  const SPOIL = '#7d9a46', SPRGB = [0x7d,0x9a,0x46];
  // the flash: on these frames of each water anim, a flash species' brightest flank pixels catch the light
  const FLASH = '#eef4f4', FLASH_N = 2, FLASH_MIX = 1/3, FLASH_A = 230;
  const FLASH_FRAMES = { swim:[1,4], dart:[0,2], thrash:[0,2], roll:[1,3], jump:[2,3] };

  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.12;
  const LN = (()=>{ const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  const hex2rgb=(h)=>[parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)];
  const rgb2hex=(r,g,b)=>'#'+[r,g,b].map(v=>Math.max(0,Math.min(255,Math.round(v))).toString(16).padStart(2,'0')).join('');
  const light=(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r+(255-r)*f, g+(255-g)*f, b+(255-b)*f); };
  const dark =(hex,f)=>{ const [r,g,b]=hex2rgb(hex); return rgb2hex(r*f, g*f, b*f); };
  const mkRamp=(base)=>[dark(base,0.42), dark(base,0.66), base, light(base,0.17), light(base,0.34)];
  const WRGB = hex2rgb(WATER);
  // the shadow (pass D): one shape, every pixel under the water KEY at SHADOW_A
  const KRGB = hex2rgb(KEY), SHADOW_A = 160;
  const mod=(a,n)=>((a%n)+n)%n, clamp01=(v)=>Math.max(0,Math.min(1,v));

  const matsCache = {};
  function matsFor(key){
    if (matsCache[key]) return matsCache[key];
    const sp = SPECIES[key];
    const flank = mkRamp(sp.flank);
    const MATS = {
      back:{ramp:mkRamp(sp.back),off:0}, stripe:{ramp:mkRamp(sp.stripe||sp.back),off:0},
      flank:{ramp:flank,off:0}, gill:{ramp:flank,off:-1}, belly:{ramp:mkRamp(sp.belly),off:-1},
      fin:{ramp:mkRamp(sp.fin),off:0}, spot:{ramp:mkRamp(sp.spots||sp.back),off:0},
    };
    const RINDEX = {};
    for (const m of Object.values(MATS)) m.ramp.forEach((c,i)=>{ RINDEX[c]={r:m.ramp,i}; });
    return matsCache[key] = { MATS, RINDEX };
  }
  function camBasis(opts){
    const dir=opts.dir||0, th=-dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) };
  }
  function projVert(x,y,z,B,c){
    c=c||CELL; const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:c.cx+xr*S, sy:c.cy-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) };
  }
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  const shadeOf=(n,se,ce)=>n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];

  // ---- pose resolution -------------------------------------------------------
  function resolve(opts){
    opts = opts||{};
    const spKey = SPECIES[opts.species] ? opts.species : 'mackerel', sp = SPECIES[spKey];
    const spoil = clamp01(opts.spoil||0), scale = opts.scale||1;
    const outline = opts.outline===true || (opts.outline==null && KEYLINE_DEFAULT);
    if (opts.rest && RPOSE[opts.rest]){
      const poses = RPOSE[opts.rest], p = poses[mod(opts.frame||0, poses.length)];
      const flatDeck = !!sp.bottom && opts.rest==='deck', flatHang = !!sp.bottom && (opts.rest==='gill' || opts.rest==='tail');
      return { spKey, sp, anim:opts.rest, sweep:0, curve:p.curve||0, roll:flatDeck?0:flatHang?HANG_ROLL:(p.roll||0), pitch:p.pitch||0,
        zB: opts.rest==='deck' ? (flatDeck ? sp.girth*scale*(sp.zflat||1) : sp.girth*scale*sp.flat) : 0,
        stretch:1, scale, gripU:p.gripU!=null?p.gripU:null, wave:0, wph:0,
        waterZ: opts.waterZ===undefined ? null : opts.waterZ, spoil, outline };
    }
    const anim = POSE[opts.anim] ? opts.anim : 'swim', poses = POSE[anim], frame = mod(opts.frame||0, poses.length), p = poses[frame];
    const wave = (sp.undulate && p.wph!=null) ? 1 : 0;
    return { spKey, sp, anim, frame,
      sweep: opts.sweep!=null?opts.sweep:((p.sweep||0)*(wave?0.35:1)),
      curve: opts.curve!=null?opts.curve:(p.curve||0),
      roll:  opts.roll !=null?opts.roll :(p.roll ||0),
      pitch: opts.pitch!=null?opts.pitch:(p.pitch||0),
      zB:    opts.z    !=null?opts.z    :(p.z    ||0),
      stretch:p.stretch||1, scale, gripU:null, wave, wph:p.wph||0,
      waterZ: opts.waterZ===undefined ? 0 : opts.waterZ, spoil, outline };
  }
  // ---- body loft --------------------------------------------------------------
  // a plate's profile (pass E): the world's, through the same depths at the snout's root, the shoulder
  // (u 0.28) and the tail's root, with its two corners rounded: the crown a quarter sine, the shoulder
  // eased over the first quarter of the taper
  const plateG=(girth, scale)=>(u)=>{ const TAU=0.25, t=(u-0.28)/0.72;
    const f = u<0.28 ? 0.4+0.6*Math.sin(Math.PI/2*Math.max(-1,u/0.28))
      : 1-0.78*(t>=TAU ? t-TAU/2 : t*t/(2*TAU))/(1-TAU/2);
    return Math.max(0.012*scale, girth*f); };
  function facesOf(r){
    const { sp, sweep, curve, roll, pitch, zB, stretch, scale, wave, wph } = r;
    const L = sp.len*scale, bodyLen = L*0.78*stretch, tl = L*0.24, girth = sp.girth*scale;
    const flat = sp.flat, zf = sp.zflat||1, fringe = !!sp.fringe;
    const g = r.loft ? plateG(girth, scale)
      : (u)=>Math.max(0.012*scale, girth*(u<0.28 ? 0.4+0.6*(u/0.28) : 1-0.78*((u-0.28)/0.72)));
    const Cc=(u)=>4*(u-0.5)*(u-0.5)-0.5, TT=(u)=>Math.pow(Math.max(0,(u-0.45)/0.55),2);
    const lat =(u)=>curve*Cc(u)*L*0.18 + sweep*TT(u)*L*0.22;
    const vert=(u)=>wave ? Math.sin(u*Math.PI*2.6 - wph)*L*0.045*(0.4+0.6*u) : 0;
    const yOf =(u)=>bodyLen*(0.5-u);
    const cR=Math.cos(roll), sR=Math.sin(roll), cP=Math.cos(pitch), sP=Math.sin(pitch);
    const T0=(p)=>{ const x1=p[0]*cR-p[2]*sR, z1=p[0]*sR+p[2]*cR; const y2=p[1]*cP-z1*sP, z2=p[1]*sP+z1*cP; return [x1,y2,z2+zB]; };
    let T=T0;
    if (r.gripU!=null){ const gp=T0([lat(r.gripU), yOf(r.gripU), vert(r.gripU)]);
      T=(p)=>{ const q=T0(p); return [q[0]-gp[0], q[1]-gp[1], q[2]-gp[2]]; }; }
    const F=[], NSEG=r.loft?r.loft.nseg:9, NR=r.loft?r.loft.nr:8, rings=[];
    for (let i=0;i<=NSEG;i++){
      const u=i/NSEG, rr=g(u), y=yOf(u), x0=lat(u), z0=vert(u), ring=[];
      for (let k=0;k<NR;k++){ const ph=k/NR*2*Math.PI; ring.push(T([x0+Math.cos(ph)*rr*flat, y, z0+Math.sin(ph)*rr*zf])); }
      rings.push(ring);
    }
    for (let i=0;i<NSEG;i++){
      const r0=rings[i], r1=rings[i+1];
      for (let k=0;k<NR;k++){
        const k2=(k+1)%NR, ms=(Math.sin(k/NR*2*Math.PI)+Math.sin(k2/NR*2*Math.PI))/2;
        let mat = ms>0.35 ? 'back' : ms<-0.35 ? 'belly' : 'flank';
        if (mat==='back' && sp.stripes==='bars' && i%2===1 && !r.loft) mat='stripe';   // a plate draws its own (plateMarks)
        if (mat==='flank' && sp.stripes==='lines' && ms>0 && !r.loft) mat='stripe';
        if (mat==='flank' && i===1 && !r.loft) mat='gill';
        F.push({v:[r0[k],r0[k2],r1[k2],r1[k]], mat, b:0, db:0});
      }
    }
    const nose=T([lat(0), yOf(0)+g(0)*0.9, vert(0)]);
    let rN=rings[0];
    if (r.loft) for (let j=1;j<4;j++){                               // a plate's snout: rounded, banded as the body
      const a=j/4*Math.PI/2, rr=g(0)*Math.cos(a), y=yOf(0)+g(0)*0.9*Math.sin(a), ring=[];
      for (let k=0;k<NR;k++){ const ph=k/NR*2*Math.PI; ring.push(T([lat(0)+Math.cos(ph)*rr*flat, y, vert(0)+Math.sin(ph)*rr*zf])); }
      for (let k=0;k<NR;k++){ const k2=(k+1)%NR, ms=(Math.sin(k/NR*2*Math.PI)+Math.sin(k2/NR*2*Math.PI))/2;
        F.push({v:[ring[k],ring[k2],rN[k2],rN[k]], mat: ms>0.35 ? 'back' : ms<-0.35 ? 'belly' : 'flank', b:0, db:0}); }
      rN=ring; }
    for (let k=0;k<NR;k++){ const k2=(k+1)%NR; F.push({v:[nose,rN[k2],rN[k],rN[k]], mat:'flank', b:0, db:0}); }
    // tail fan — forked, swung with the sweep; flatfish tails spread in the body plane
    const ty=sweep*1.15+curve*0.5, Bp=[lat(1), yOf(1), vert(1)], bd=[Math.sin(ty), -Math.cos(ty), 0];
    const tp = fringe ? [-bd[1], bd[0], 0] : [0,0,1];
    const TP=(sx,sp2)=>T([Bp[0]+bd[0]*tl*sx+tp[0]*tl*sp2, Bp[1]+bd[1]*tl*sx+tp[1]*tl*sp2, Bp[2]+tp[2]*tl*sp2]);
    for (const s of [1,-0.85]){
      if (fringe) F.push({v:[T(Bp), TP(0.85,0.55*s), TP(1.0,0.22*s), TP(0.6,0)], mat:'fin', b:-0.1, db:-0.05, ds:1, tag:'tail'});
      else        F.push({v:[T(Bp), TP(1,0.62*s), TP(1.02,0.25*s), TP(0.55,0)], mat:'fin', b:-0.1, db:-0.05, ds:1, tag:'tail'});
    }
    const h=girth*0.55;
    const fin=(a,b,c,d,bb,tag)=>F.push({v:[a,b,c,d], mat:'fin', b:bb!=null?bb:-0.1, db:-0.05, ds:1, tag});
    const P=(u,dx,dz)=>T([lat(u)+dx, yOf(u), vert(u)+dz]);
    if (fringe){                                                     // fringe fins down both edges
      const fw=(u)=>girth*0.42*Math.sin(Math.PI*(u-0.14)/0.76);
      for (const sd of [-1,1]) for (const [u0,u1] of [[0.16,0.40],[0.40,0.64],[0.64,0.88]])
        fin(P(u0,sd*g(u0)*flat,0), P(u1,sd*g(u1)*flat,0), P(u1,sd*(g(u1)*flat+fw(u1)),-0.004*scale), P(u0,sd*(g(u0)*flat+fw(u0)),-0.004*scale), null, 'fringe');
    } else {
      const d1=0.36, d2=0.58;                                        // main dorsal
      fin(P(d1,0,g(d1)), P(d2,0,g(d2)), P(d2,0,g(d2)+h*0.6), P(d1,0,g(d1)+h), null, 'dorsal');
      if (sp.dorsals>=2){ const e1=0.20, e2=0.31; fin(P(e1,0,g(e1)), P(e2,0,g(e2)), P(e2,0,g(e2)+h*0.9), P(e1,0,g(e1)+h*0.55), null, 'dorsal'); }
      const a1=0.60, a2=0.74;                                        // anal fin
      fin(P(a1,0,-g(a1)), P(a2,0,-g(a2)), P(a2,0,-g(a2)-h*0.45), P(a1,0,-g(a1)-h*0.35), null, 'anal');
      for (const sd of [-1,1]){                                      // pectorals, swept back and down
        const u=0.30, rr=g(u);
        const A=P(u, sd*rr*flat*0.9, -rr*0.15);
        const Bq=T([lat(u)+sd*(rr*flat*0.9+girth*0.45), yOf(u)-girth*0.8, vert(u)-rr*0.55]);
        const Cq=T([lat(u)+sd*(rr*flat*0.9+girth*0.2), yOf(u)-girth*1.0, vert(u)-rr*0.35]);
        F.push({v:[A,Bq,Cq,Cq], mat:'fin', b:-0.15, db:-0.03, ds:1, tag:'pec', root:A});
      }
    }
    const ue=0.10, re=g(ue);
    const eyes = fringe
      ? [T([lat(ue)+re*flat*0.38, yOf(ue), vert(ue)+re*zf*0.9]), T([lat(ue)-re*flat*0.38, yOf(ue), vert(ue)+re*zf*0.9])]
      : [T([lat(ue)+re*flat*0.95, yOf(ue), vert(ue)+re*0.25]),  T([lat(ue)-re*flat*0.95, yOf(ue), vert(ue)+re*0.25])];
    // the spine: nose, the body's axis to the tail root, then the tail's centre to its notch (pass B)
    const spine=[nose];
    for (let i=0;i<=NSEG;i++){ const u=i/NSEG; spine.push(T([lat(u), yOf(u), vert(u)])); }
    spine.push(TP(0.55,0));
    return { F, eyes, spine, at:{ L, bodyLen, girth, flat, zf, g, lat, yOf, vert, T } };
  }

  // ---- paint: z-buffered facets, dither, edge darkening, eyes, (gated) keyline, tints ------
  function _paint(r, o, c){
    c=c||CELL; const W=c.W, H=c.H;
    const B=camBasis(o), {MATS,RINDEX}=matsFor(r.spKey), {F,eyes,spine,at}=facesOf(r);
    const SPOT=!!r.sp.spots && !r.loft;
    const zbuf=new Float32Array(W*H).fill(Infinity), col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H), zw=new Float32Array(W*H), shv=new Float32Array(W*H), mt=new Array(W*H).fill(null);
    const ff=c.post?new Array(W*H).fill(null):null;                 // a plate's marks read which face drew each pixel
    for (const f of F){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B,c));
      let n=normal(rv[0],rv[1],rv[2]), sh=shadeOf(n,B.se,B.ce);
      if (sh<0 && f.ds) sh=shadeOf([-n[0],-n[1],-n[2]],B.se,B.ce)*0.9;
      const fidx=sh*GAIN+BIAS+(f.b||0), M=MATS[f.mat]||MATS.flank, spotty=SPOT && f.mat==='back';
      for (let tt=1;tt+1<rv.length;tt++) fillTri(rv[0],rv[tt],rv[tt+1]);
      function fillTri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if (Math.abs(area)<1e-6) return;
        for (let y=minY;y<=maxY;y++) for (let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area, w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area, w2=1-w0-w1;
          if (w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0), i=y*W+x;
          if (deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d; zw[i]=w0*a.zr+w1*b.zr+w2*c.zr;
            const base=Math.floor(fidx), idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            const RM=(spotty && ((((x*73856093)^(y*19349663))>>>0)%6)===0) ? MATS.spot : M;
            col[i]=RM.ramp[Math.max(0,Math.min(4,idx))]; shv[i]=fidx; mt[i]=f.mat; if (ff) ff[i]=f;
          }
        }
      }
    }
    const finD=MATS.fin.ramp[0];
    for (const e of eyes){
      const v=projVert(e[0],e[1],e[2],B,c), x=Math.round(v.sx), y=Math.round(v.sy);
      if (x<0||x>=W||y<0||y>=H) continue;
      const i=y*W+x; if (v.d-0.05<zbuf[i] && col[i]){ col[i]=finD; dep[i]=v.d; mt[i]='eye'; }
    }
    // the spine fills what the facets missed (pass B): a 1 px line in its material's base colour
    const spineC=r.sp.spine?MATS[r.sp.spine].ramp[2]:null;
    if (spineC) for (let k=0;k+1<spine.length;k++){
      const a=projVert(spine[k][0],spine[k][1],spine[k][2],B,c), b=projVert(spine[k+1][0],spine[k+1][1],spine[k+1][2],B,c);
      const n=Math.max(1, Math.ceil(Math.hypot(b.sx-a.sx, b.sy-a.sy)*3));
      for (let s=0;s<=n;s++){
        const t=s/n, x=Math.floor(a.sx+(b.sx-a.sx)*t), y=Math.floor(a.sy+(b.sy-a.sy)*t);
        if (x<0||x>=W||y<0||y>=H) continue;
        const i=y*W+x; if (col[i]) continue;
        col[i]=spineC; dep[i]=a.d+(b.d-a.d)*t; zw[i]=a.zr+(b.zr-a.zr)*t; mt[i]='spine';
      }
    }
    // the flash (pass B): on its frames, a flash species' two brightest flank pixels catch the light
    const flash=new Uint8Array(W*H);
    if (r.sp.flash && r.waterZ!=null && (FLASH_FRAMES[r.anim]||[]).indexOf(r.frame)>=0){
      const cand=[];
      for (let i=0;i<W*H;i++) if (col[i] && (mt[i]==='flank'||mt[i]==='belly'||mt[i]==='gill')) cand.push(i);
      cand.sort((p,q)=>shv[q]-shv[p] || p-q);
      for (const i of cand.slice(0,FLASH_N)) flash[i]=1;
    }
    const out=col.slice();
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x; if (!col[i]) continue;
      for (const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if (nx>=W||ny>=H) continue;
        const j=ny*W+nx; if (!col[j]) continue;
        if (Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]]; if (e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)]; }
      }
    }
    if (r.outline) for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x; if (out[i]) continue;
      for (const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy; if (nx<0||nx>=W||ny<0||ny>=H) continue;
        const j=ny*W+nx; if (col[j]){ out[i]=KEY; zw[i]=zw[j]; break; }
      }
    }
    if (c.post) c.post({ r, c, out, mt, ff, MATS, RINDEX, at, B, W, H, zbuf });
    const rgba=new Uint8ClampedArray(W*H*4);
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){
      const i=y*W+x, c=flash[i]?FLASH:out[i]; if (!c){ rgba[i*4+3]=0; continue; }
      let [rr,gg,bb]=hex2rgb(c), a=255;
      if (r.spoil>0 && c!==KEY){
        const mixS=r.spoil*(0.40 + (BAYER[x&3][y&3] < r.spoil*0.55 ? 0.28 : 0));
        rr=rr*(1-mixS)+SPRGB[0]*mixS; gg=gg*(1-mixS)+SPRGB[1]*mixS; bb=bb*(1-mixS)+SPRGB[2]*mixS;
      }
      if (r.waterZ!=null && zw[i]<r.waterZ-0.004 && r.anim==='shadow'){ [rr,gg,bb]=KRGB; a=SHADOW_A; }
      else if (r.waterZ!=null && zw[i]<r.waterZ-0.004){
        const dz=r.waterZ-zw[i], mix=Math.min(0.72, 0.30+dz*0.9)*(flash[i]?FLASH_MIX:1);
        rr=rr*(1-mix)+WRGB[0]*mix; gg=gg*(1-mix)+WRGB[1]*mix; bb=bb*(1-mix)+WRGB[2]*mix;
        a = flash[i] ? FLASH_A : dz>0.35 ? 115 : 160;
      }
      rgba[i*4]=Math.round(rr); rgba[i*4+1]=Math.round(gg); rgba[i*4+2]=Math.round(bb); rgba[i*4+3]=a;
    }
    return rgba;
  }

  function render(dir, opts){
    const r=resolve(opts), o=Object.assign({},opts,{dir}), look=opts&&opts.look!=null?opts.look:LOOK_DEFAULT;
    if (look!=='plate') return _paint(r, o);                                                // look:'world', pass E's cell
    r.loft=PLATE_LOFT; return _paint(r, o, Object.assign({}, CELL, { post:lookMarks }));   // A, the default since pass F (below)
  }

  // ---- THE PLATE (catch review pass E): the species for a field guide's page, larger than the world --------
  // Side-on facing left (a flatfish from above, as it lies, head left), at 32 px = 1 m x scale, still and
  // flat (no sweep, curve, roll or wave), centred in a PW x PH cell. The loft is the world's in PLATE_LOFT's
  // finer rings; on it go the marks a field guide shows (MARKS, and PLATE_ALL on every fish), each in the
  // species' own ramps and each following the shading under it. A mark at (u, v) sits at the body's station u
  // (0 the snout's root, 1 the tail's) and v of the way from its axis to its edge (+1 the back, or from above
  // the left side).
  const PW=160, PH=96, PLATE_LOFT={ nseg:36, nr:16 };
  const PLATE_ALL=['the eye, its pupil, iris and light', 'the gill cover', 'the fins rayed'];
  const MARKS={
    cod:      ['the lateral line, pale, arched over the pectoral', 'speckles over the back and the upper flank', 'the chin barbel', 'the mouth, the upper jaw the longer'],
    haddock:  ['the lateral line, black', 'the dark blotch above the pectoral', 'a small chin barbel', 'the small mouth'],
    pollock:  ['the lateral line, pale and nearly straight', 'the mouth, the lower jaw the longer'],
    mackerel: ['wavy dark bars down the back to the mid-flank', 'the flank and the belly plain', 'the mouth'],
    bass:     ['seven dark stripes along the flank', 'the large mouth'],
    flounder: ['both eyes on the upper side', 'dark spots over the back', 'the lateral line, nearly straight'],
    herring:  ['the large eye', 'the keel along the belly', 'the mouth, upturned'],
  };
  const PLATE_EYE={ cod:0.24, haddock:0.27, pollock:0.24, mackerel:0.27, bass:0.22, flounder:0.17, herring:0.34 };
  const PLATE_MOUTH={ cod:[[-0.055,-0.10],[0.075,-0.30]], haddock:[[-0.05,-0.12],[0.055,-0.28]], pollock:[[-0.065,0.05],[0.07,-0.28]],
    mackerel:[[-0.06,-0.02],[0.075,-0.22]], bass:[[-0.065,0.0],[0.09,-0.30]], herring:[[-0.07,0.14],[0.05,-0.12]] };
  const PLATE_LINE={ cod:[[0.20,0.48],[0.28,0.60],[0.40,0.58],[0.55,0.32],[0.70,0.12],[0.86,0.04],[1.00,0.0]],
    haddock:[[0.20,0.52],[0.32,0.58],[0.50,0.48],[0.70,0.28],[0.86,0.12],[1.00,0.04]],
    pollock:[[0.20,0.40],[0.40,0.38],[0.60,0.28],[0.80,0.14],[1.00,0.04]],
    flounder:[[0.18,0.14],[0.26,0.05],[0.36,0.0],[1.00,0.0]] };
  const RAY=3;                                                          // px between a fin's rays at its edge
  const spline=(cp)=>(u)=>{ const n=cp.length; if (u<=cp[0][0]) return cp[0][1]; if (u>=cp[n-1][0]) return cp[n-1][1];
    let k=0; while (u>cp[k+1][0]) k++;
    const m=(j)=>{ const a=cp[Math.max(0,j-1)], b=cp[Math.min(n-1,j+1)]; return (b[1]-a[1])/(b[0]-a[0]); };
    const [u0,v0]=cp[k], [u1,v1]=cp[k+1], h=u1-u0, t=(u-u0)/h, t2=t*t, t3=t2*t;
    return (2*t3-3*t2+1)*v0 + (t3-2*t2+t)*h*m(k) + (-2*t3+3*t2)*v1 + (t3-t2)*h*m(k+1); };
  const hsh=(x,y,s)=>{ let h=(Math.imul(x,374761393)+Math.imul(y,668265263)+Math.imul(s||0,1274126177))|0;
    h=Math.imul(h^(h>>>13),1103515245); return ((h^(h>>>16))>>>0)/4294967296; };
  function plateMarks(q, top){
    const { r, c, out, mt, ff, MATS, RINDEX, at, B, W, H } = q, key=r.spKey;
    const half=(u)=>at.g(u)*(top?at.flat:at.zf)*S;
    const X=(u)=>c.cx-at.yOf(u)*S, Y=(u,v)=>c.cy-v*half(u);
    const U=(x)=>0.5-(c.cx-x)/(at.bodyLen*S);
    const I=(x,y)=>{ x=Math.floor(x); y=Math.floor(y); return (x>=0&&y>=0&&x<W&&y<H)?y*W+x:-1; };
    const body=(i)=>{ const m=mt[i]; return m==='back'||m==='flank'||m==='belly'||m==='gill'||m==='stripe'||m==='spine'; };
    const idx=(i)=>{ const e=RINDEX[out[i]]; return e?e.i:2; };
    const own=(i,d)=>{ const e=RINDEX[out[i]]; return e?e.r[Math.max(0,Math.min(4,e.i+d))]:out[i]; };
    const as=(i,ramp,d)=>ramp[Math.max(0,Math.min(4,idx(i)+(d||0)))];
    const along=(u0,u1,v,paint)=>{ for (let x=Math.ceil(X(u0)); x<=Math.floor(X(u1)); x++){ const u=U(x+0.5), i=I(x, Y(u,v(u))); if (i>=0 && out[i]) paint(i,x,u); } };
    const seg=(ax,ay,bx,by,paint)=>{ const n=Math.max(1,Math.ceil(Math.max(Math.abs(bx-ax),Math.abs(by-ay)))), seen={};
      for (let s=0;s<=n;s++){ const t=s/n, i=I(ax+(bx-ax)*t, ay+(by-ay)*t); if (i>=0 && !seen[i]){ seen[i]=1; paint(i); } } };
    const V=(x,y)=>{ const u=Math.max(-0.1,Math.min(1,U(x))), h=half(u); return h>0?(c.cy-y)/h:9; };
    // the back, the flank and the belly meet in a dithered band (side-on, where the rings' facets meet)
    if (!top){ const vb=Math.sin(Math.PI/8);
      for (let y=0;y<H;y++) for (let x=0;x<W;x++){ const i=y*W+x, m=mt[i]; if (!out[i] || (m!=='back'&&m!=='flank'&&m!=='belly')) continue;
        const hh=half(Math.max(0,Math.min(1,U(x+0.5)))); if (hh<4) continue;
        const v=(c.cy-y-0.5)/hh, d=Math.min(0.2,2.5/hh); let to=null;
        for (const [e,hi,lo] of [[vb,'back','flank'],[-vb,'flank','belly']]){ if (Math.abs(v-e)>=d) continue;
          const want=(v-(e-d))/(2*d)>BAYER[x&3][y&3]?hi:lo; if (want!==m) to=want; }
        const e=to && RINDEX[out[i]]; if (!e) continue;
        out[i]=MATS[to].ramp[Math.max(0,Math.min(4,e.i-MATS[m].off+MATS[to].off))]; mt[i]=to; }
    }
    // the fins' rays: dorsal and anal fins lean back from the body's edge; the tail's and the pectorals'
    // fan from their roots; a flatfish's fringe stands off its outline
    const tail=[X(1), Y(1,0)], RT=at.L*0.24*S, RP=at.girth*S;
    const fan=(x,y,o,R,lo)=>{ const dx=x+0.5-o[0], dy=y+0.5-o[1], d=Math.hypot(dx,dy); if (d<R*lo) return false;
      const da=RAY/R, t=Math.atan2(dy,dx)/da; return Math.abs(t-Math.round(t))*da*d<0.55; };
    for (let y=0;y<H;y++) for (let x=0;x<W;x++){ const i=y*W+x; if (mt[i]!=='fin' || !out[i] || !ff[i]) continue;
      const f=ff[i], tg=f.tag; let on=false;
      if (tg==='tail') on=fan(x,y,tail,RT,0.3);
      else if (tg==='pec'){ const v=projVert(f.root[0],f.root[1],f.root[2],B,c); on=fan(x,y,[v.sx,v.sy],RP,0.25); }
      else if (tg==='dorsal'||tg==='anal'||tg==='fringe'){
        const sd=(tg==='anal' || (tg==='fringe' && y+0.5>c.cy))?-1:1, u=Math.max(0,Math.min(1,U(x+0.5)));
        const e=Y(u,sd), dy=Math.abs(y+0.5-e), sl=(Y(Math.min(1,u+0.01),sd)-Y(Math.max(0,u-0.01),sd))/(X(Math.min(1,u+0.01))-X(Math.max(0,u-0.01)));
        const t=tg==='fringe' ? x+0.5+(y+0.5-e)*sl : x+0.5-dy*0.45;
        on = dy>1 && mod(Math.floor(t),RAY)===0; }
      if (on) out[i]=own(i,-1);
    }
    // the gill cover: its edge an arc bowed back behind the head, from the nape to the throat
    { const h0=half(0.2), pts=[];
      for (let s=0;s<=24;s++){ const v=0.70-1.50*s/24, w=(v+0.05)/0.75; pts.push([X(0.17+0.06*Math.sqrt(Math.max(0,1-w*w))), c.cy-v*h0]); }
      const done={};
      for (let k=0;k+1<pts.length;k++) seg(pts[k][0],pts[k][1],pts[k+1][0],pts[k+1][1],(i)=>{ if (!done[i] && out[i] && body(i)){ done[i]=1; out[i]=own(i,-2); } }); }
    // the species' own marks
    if (key==='cod'){
      for (let y=0;y<H;y++) for (let x=0;x<W;x++){ const i=y*W+x; if (!out[i] || !body(i)) continue;
        const u=U(x+0.5), v=V(x+0.5,y+0.5); if (u<0.03 || u>0.96 || v<-0.3 || hsh(x,y,7)>(v>0.3?0.026:0.018)) continue;
        const big=hsh(x,y,8)<0.4;
        for (const [dx,dy] of big?[[0,0],[1,0],[0,1],[1,1]]:[[0,0]]){ const j=I(x+dx,y+dy); if (j>=0 && out[j] && body(j)) out[j]=own(j,dx||dy?-1:-2); } }
    }
    if (key==='haddock'){
      const u0=0.285, v0=0.12, ru=0.042*at.bodyLen*S, rv=0.24*half(u0), ex=X(u0), ey=Y(u0,v0);
      for (let y=Math.floor(ey-rv-1);y<=ey+rv+1;y++) for (let x=Math.floor(ex-ru-1);x<=ex+ru+1;x++){ const i=I(x,y); if (i<0 || !out[i] || !body(i)) continue;
        const dx=(x+0.5-ex)/ru, dy=(y+0.5-ey)/rv, qq=dx*dx+dy*dy; if (qq>1) continue;
        if (qq<0.6 || BAYER[x&3][y&3]>(qq-0.6)/0.4) out[i]=as(i,MATS.fin.ramp,-1); }
    }
    if (key==='mackerel'){
      for (let y=0;y<H;y++) for (let x=0;x<W;x++){ const i=y*W+x; if (!out[i] || !body(i)) continue;
        const u=U(x+0.5), v=V(x+0.5,y+0.5); if (u<0.22 || u>0.99 || v<0.06) continue;
        const ph=u*24+v*0.6+0.22*Math.sin(v*9+u*14); if (ph-Math.floor(ph)<0.40) out[i]=as(i,MATS.stripe.ramp,-1); }
    }
    if (key==='bass') for (let k=0;k<7;k++){ const vk=0.80-0.185*k; along(0.23,0.99,()=>vk,(i)=>{ if (body(i)) out[i]=as(i,MATS.stripe.ramp,-1); }); }
    if (key==='flounder'){
      const G=Math.max(5,Math.round(half(0.5)*0.32));
      for (let gy=0; gy*G<H; gy++) for (let gx=0; gx*G<W; gx++){ if (hsh(gx,gy,3)>0.55) continue;
        const sx=gx*G+hsh(gx,gy,4)*G, sy=gy*G+hsh(gx,gy,5)*G, rr=0.8+hsh(gx,gy,6)*G*0.22;
        for (let y=Math.floor(sy-rr);y<=sy+rr;y++) for (let x=Math.floor(sx-rr);x<=sx+rr;x++){ const i=I(x,y);
          if (i>=0 && out[i] && mt[i]==='back' && Math.hypot(x+0.5-sx,y+0.5-sy)<=rr) out[i]=as(i,MATS.spot.ramp,0); } }
    }
    if (key==='herring') along(0.12,0.88,()=>-0.86,(i,x)=>{ if (body(i)) out[i]=own(i,x%2?-1:-2); });
    if (PLATE_LINE[key]){ const pale=key==='cod'||key==='pollock', ll=spline(PLATE_LINE[key]);
      along(PLATE_LINE[key][0][0],0.99,ll,(i)=>{ if (body(i)) out[i]=pale?as(i,MATS.belly.ramp,0):key==='haddock'?as(i,MATS.fin.ramp,-1):own(i,-1); }); }
    // the mouth, and the barbel under the chin
    const mo=PLATE_MOUTH[key];
    if (mo && !top) seg(X(mo[0][0]),Y(mo[0][0],mo[0][1]),X(mo[1][0]),Y(mo[1][0],mo[1][1]),(i)=>{ if (out[i] && body(i)) out[i]=own(i,-2); });
    const ue=0.10, re=Math.max(1.2,(PLATE_EYE[key]||0.24)*half(ue));
    if (key==='cod'||key==='haddock'){ const bx=X(0.045), by=Y(0.045,-0.92), n=(key==='cod'?2:1)*re;
      seg(bx,by,bx+n*0.35,by+n,(i)=>{ if (!out[i] || body(i)) out[i]=MATS.belly.ramp[1]; }); }
    // the eye last: the pupil, the iris, a rim a step under the body, and the light
    const eye=(ex,ey)=>{ const rp=re*0.56, R=Math.ceil(re+1);
      for (let y=Math.floor(ey-R); y<=ey+R; y++) for (let x=Math.floor(ex-R); x<=ex+R; x++){ const i=I(x,y); if (i<0 || !out[i]) continue;
        const d=Math.hypot(x+0.5-ex, y+0.5-ey);
        if (d<=rp) out[i]=MATS.fin.ramp[0]; else if (d<=re) out[i]=MATS.belly.ramp[d<re*0.82?3:2]; else if (d<=re+0.7 && body(i)) out[i]=own(i,-2); }
      const h=I(ex-rp*0.4, ey-rp*0.4); if (h>=0 && out[h]) out[h]=MATS.belly.ramp[4]; };
    if (top) for (const sd of [1,-1]) eye(X(ue), Y(ue, sd*0.38)); else eye(X(ue), Y(ue, 0.25));
  }
  function renderPlate(species, scale){
    if (!SPECIES[species]) return null;
    const sp=SPECIES[species], top=!!sp.bottom;
    const r=resolve({ species, anim:'swim', frame:0, sweep:0, curve:0, roll:0, pitch:0, z:0, waterZ:null, scale:scale||1 });
    r.loft=PLATE_LOFT; r.wave=0;
    const o={ dir:6, elev:top?90:0 }, B=camBasis(o), O={ cx:0, cy:0 };
    let x0=Infinity, x1=-Infinity, y0=Infinity, y1=-Infinity;
    for (const f of facesOf(r).F) for (const p of f.v){ const v=projVert(p[0],p[1],p[2],B,O);
      x0=Math.min(x0,v.sx); x1=Math.max(x1,v.sx); y0=Math.min(y0,v.sy); y1=Math.max(y1,v.sy); }
    const c={ W:PW, H:PH, cx:Math.round(PW/2-(x0+x1)/2), cy:Math.round(PH/2-(y0+y1)/2), post:(q)=>plateMarks(q, top) };
    return _paint(r, o, c);
  }

  // ---- A (catch review, 2026-10-28; shown first for question 6, "Preview A"; the default since pass F, question 7):
  // the plate's look in the world's cells ----
  // render(dir) (look 'plate', LOOK_DEFAULT) draws the world's cell on the plate's loft (PLATE_LOFT, with its profile and its snout)
  // and paints on it what of the plate's marks holds at 32 px. Each mark is a point or a line on the body's surface, at
  // the plate's station u and height v, carried through the pose (the bend, the wave, the roll, the pitch, the grip)
  // and the camera, and drawn where the depth test sees it, so it turns with the body. A mark draws only where the
  // body's half-depth at its station (a flatfish's half-width) is LOOK_MIN px or more (the gill cover LOOK_GILL, where
  // its arc has room to bow, not a band across the head); the herring's never is. Not carried: the fins' rays, the
  // mouth, the barbel, the herring's keel, a flatfish's irises (its eyes sit 2 to 3 px apart), and the bands'
  // dithered edges (the plate's own skip under 4 px). render(dir, {look:'world'}) draws pass E's cell, byte for byte:
  // the world's 9 x 8 loft, no mark.
  const LOOK_MIN=2, LOOK_GILL=3;
  const LOOK={
    cod:      ['the gill cover', 'the lateral line, pale', 'speckles over the back and the upper flank', 'the eye, its pupil and iris'],
    haddock:  ['the gill cover', 'the lateral line, black', 'the dark blotch above the pectoral', 'the eye, its pupil and iris'],
    pollock:  ['the gill cover', 'the lateral line, pale', 'the eye, its pupil and iris'],
    mackerel: ['dark bars down the back, 2.5 px apart or more'],
    bass:     ['the gill cover', 'of the seven stripes, those 2 px apart or more', 'the eye, its pupil and iris'],
    flounder: ['the gill cover', 'dark spots over the back', 'the lateral line'],
    herring:  [],
  };
  function lookMarks(q){
    const { r, c, out, mt, MATS, RINDEX, at, B, W, H, zbuf } = q, key=r.spKey, top=!!r.sp.bottom, sides=top?[1]:[1,-1];
    const half=(u)=>at.g(u)*(top?at.flat:at.zf)*S;
    const I=(x,y)=>{ x=Math.floor(x); y=Math.floor(y); return (x>=0&&y>=0&&x<W&&y<H)?y*W+x:-1; };
    const body=(i)=>{ const m=mt[i]; return m==='back'||m==='flank'||m==='belly'||m==='spine'||m==='eye'; };
    const idx=(i)=>{ const e=RINDEX[out[i]]; return e?e.i:2; };
    const own=(i,d)=>{ const e=RINDEX[out[i]]; return e?e.r[Math.max(0,Math.min(4,e.i+d))]:out[i]; };
    const as=(i,ramp,d)=>ramp[Math.max(0,Math.min(4,idx(i)+(d||0)))];
    // the surface at station u and height v (+1 the back), on side sd; a flatfish's v runs across its eyed side
    const surf=(u,v,sd)=>{ v=Math.max(-1,Math.min(1,v)); const ph=top?Math.acos(v):sd>0?Math.asin(v):Math.PI-Math.asin(v), rr=at.g(u);
      return at.T([at.lat(u)+Math.cos(ph)*rr*at.flat, at.yOf(u), at.vert(u)+Math.sin(ph)*rr*at.zf]); };
    // the pixel a surface point falls on, if the depth test sees it there on the body; else -1
    const see=(p,u)=>{ const v=projVert(p[0],p[1],p[2],B,c), i=I(v.sx,v.sy); return i>=0 && out[i] && body(i) && v.d-0.5*at.g(u)<=zbuf[i] ? i : -1; };
    const line=(u0,u1,vf,paint)=>{ const n=Math.max(8,Math.ceil((u1-u0)*at.bodyLen*S*4));
      for (const sd of sides){ const done=new Set();
        for (let k=0;k<=n;k++){ const u=u0+(u1-u0)*k/n; if (half(u)<LOOK_MIN) continue;
          const i=see(surf(u,vf(u),sd),u); if (i>=0 && !done.has(i)){ done.add(i); paint(i); } } } };
    if (!LOOK[key] || !LOOK[key].length) return;
    // the gill cover: its edge an arc bowed back behind the head, from the nape to the throat
    if (half(0.2)>=LOOK_GILL) for (const sd of sides){ const done=new Set();
      for (let s=0;s<=48;s++){ const v=0.70-1.50*s/48, w=(v+0.05)/0.75, u=0.17+0.06*Math.sqrt(Math.max(0,1-w*w)), i=see(surf(u,v,sd),u);
        if (i>=0 && !done.has(i)){ done.add(i); out[i]=own(i,-2); } } }
    // the species' own marks
    if (key==='cod') for (const sd of sides){ const nu=Math.max(1,Math.round(at.bodyLen*S));
      for (let a=0;a<nu;a++){ const u=(a+0.5)/nu; if (u<0.03 || u>0.96 || half(u)<LOOK_MIN) continue; const nv=Math.max(2,Math.round(2*half(u)));
        for (let b=0;b<nv;b++){ const v=-1+(b+0.5)*2/nv; if (v<-0.3 || hsh(a,b+(sd>0?0:64),7)>(v>0.3?0.06:0.04)) continue;
          const i=see(surf(u,v,sd),u); if (i>=0) out[i]=own(i,-2); } } }
    if (key==='haddock' && half(0.285)>=LOOK_MIN) for (const sd of sides){ const done=new Set();
      for (let a=-4;a<=4;a++) for (let b=-4;b<=4;b++){ if (a*a+b*b>16) continue; const u=0.285+a/4*0.042, i=see(surf(u,0.12+b/4*0.24,sd),u);
        if (i>=0 && !done.has(i)){ done.add(i); out[i]=as(i,MATS.fin.ramp,-1); } } }
    if (key==='mackerel'){ const k=Math.min(24,Math.floor(at.bodyLen*S/2.5));                     // bars 2.5 px apart or more
      for (let n=0;n<k;n++) for (const sd of sides){ const done=new Set();
        for (let s=0;s<=32;s++){ const v=0.06+0.94*s/32, u=(n+0.2-v*0.6)/k; if (u<0.22 || u>0.99 || half(u)<LOOK_MIN) continue;
          const i=see(surf(u,v,sd),u); if (i>=0 && !done.has(i)){ done.add(i); out[i]=as(i,MATS.stripe.ramp,-1); } } } }
    if (key==='bass'){ const step=Math.max(1,Math.ceil(2/(0.185*half(0.5))));                    // of the seven, those 2 px apart or more
      for (let k=0;k<7;k+=step){ const vk=0.80-0.185*k; line(0.23,0.99,()=>vk,(i)=>{ out[i]=as(i,MATS.stripe.ramp,-1); }); } }
    if (key==='flounder'){ const nu=Math.max(1,Math.round(at.bodyLen*S/2.5));
      for (let a=0;a<nu;a++){ const u0=a/nu, h=half(u0+0.5/nu), nv=Math.max(1,Math.round(2*h/2.5));
        for (let b=0;b<nv;b++){ if (hsh(a,b,3)>0.45) continue; const u=u0+(0.25+0.5*hsh(a,b,5))/nu, v=-1+(b+0.25+0.5*hsh(a,b,4))*2/nv;
          if (half(u)<LOOK_MIN) continue; const i=see(surf(u,v,1),u); if (i>=0 && mt[i]==='back') out[i]=as(i,MATS.spot.ramp,0); } } }
    if (PLATE_LINE[key]){ const pale=key==='cod'||key==='pollock', ll=spline(PLATE_LINE[key]);
      line(PLATE_LINE[key][0][0],0.99,ll,(i)=>{ out[i]=pale?as(i,MATS.belly.ramp,0):key==='haddock'?as(i,MATS.fin.ramp,-1):own(i,-1); }); }
    // the eye last: the pupil the pixel it falls on, the iris round it, a rim a step under the body; the light where it has
    // room. A flatfish keeps the world's two eye pixels: its eyes sit 2 to 3 px apart, and two irises there run together.
    const ue=0.10; if (top || half(ue)<LOOK_MIN) return;
    const re=Math.max(1.2,(PLATE_EYE[key]||0.24)*half(ue)), rp=re*0.56, R=Math.ceil(re+1);
    for (const sd of sides){ const p=surf(ue,0.25,sd), v=projVert(p[0],p[1],p[2],B,c), e=I(v.sx,v.sy);
      if (e<0 || !out[e] || !(v.d-0.05<zbuf[e])) continue;
      const ex=v.sx, ey=v.sy;
      for (let y=Math.floor(ey-R); y<=ey+R; y++) for (let x=Math.floor(ex-R); x<=ex+R; x++){ const i=I(x,y); if (i<0 || i===e || !out[i] || !body(i)) continue;
        const d=Math.hypot(x+0.5-ex, y+0.5-ey);
        if (d<=rp) out[i]=MATS.fin.ramp[0]; else if (d<=re) out[i]=MATS.belly.ramp[d<re*0.82?3:2]; else if (d<=re+0.7) out[i]=own(i,-2); }
      out[e]=MATS.fin.ramp[0];
      if (re>=2){ const h=I(ex-rp*0.4, ey-rp*0.4); if (h>=0 && h!==e && out[h]) out[h]=MATS.belly.ramp[4]; } }
  }
  function mouth(dir, opts){
    const r=resolve(opts), L=r.sp.len*r.scale, y=L*0.78*r.stretch*0.5 + r.sp.girth*r.scale*0.9;
    const cP=Math.cos(r.pitch), sP=Math.sin(r.pitch);
    const v=projVert(0, y*cP, y*sP + r.zB, camBasis({dir, elev:opts&&opts.elev}));
    return { dx:Math.round(v.sx-cx), dy:Math.round(v.sy-cy) };
  }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir, elev})); return { dx:v.sx-cx, dy:v.sy-cy }; }
  function sheetOrder(){
    const o=[]; for (const a of AORDER) for (let f=0;f<ANIMS[a].n;f++) o.push({anim:a,f});
    for (const rk of RESTS) for (let f=0;f<RPOSE[rk].length;f++) o.push({rest:rk,f});
    return o;
  }
  // size x weight decides the carry — computed, so a new species inherits it
  function hold(spKey, scale){
    const sp=SPECIES[spKey]||SPECIES.mackerel, s=scale||1;
    const mass=sp.len*sp.girth*sp.girth*s*s*s*390*(sp.massK||1);
    return { mass:Math.round(mass*100)/100, hands: mass>=2.2 ? 2 : 1 };
  }
  function sizeOf(spKey, scale){ const sp=SPECIES[spKey]||SPECIES.mackerel, s=scale||1; return { len:+(sp.len*s).toFixed(3), px:+(sp.len*s*S).toFixed(1) }; }
  const dirOf=(h)=>mod(Math.round(h/(Math.PI/4)), 8);
  function mulberry(seed){ let a=seed>>>0; return function(){ a|=0; a=(a+0x6D2B79F5)|0;
    let t=Math.imul(a^(a>>>15),1|a); t=(t+Math.imul(t^(t>>>7),61|t))^t; return ((t^(t>>>14))>>>0)/4294967296; }; }
  /* RUNTIME SHOAL. n singles behind one leader on a lazy figure-eight (radius m, speed m/s).
     t in ms. Returns [{x,y,z (world metres from the shoal anchor), heading (rad, 0=N, CW),
     dir (8-dir bake), frame (swim), scale}] — the game blits FishIso2.render(dir,{species,
     anim:'swim',frame,scale}) at each. Deterministic per seed, so two clients agree. */
  function shoal(species, n, t, opts){
    opts=opts||{};
    const sp=SPECIES[species]||SPECIES.herring, L=sp.len*(opts.scale||1);
    const rng=mulberry((opts.seed||11)*2654435761), R=opts.radius!=null?opts.radius:1.2;
    const w=(opts.speed!=null?opts.speed:0.35)/R, tt=t/1000, a=w*tt;
    const lx=R*Math.sin(a), ly=R*0.55*Math.sin(2*a);
    const vx=R*w*Math.cos(a), vy=R*1.1*w*Math.cos(2*a);
    const head=Math.atan2(vx,vy), ch=Math.cos(head), sh=Math.sin(head);
    const out=[];
    for (let i=0;i<n;i++){
      const back=-(0.3+rng()*1.6)*L*(1+i/n*1.5), side=(rng()-0.5)*L*3.2*(0.35+i/n), lift=(rng()-0.5)*0.08;
      const ph=rng()*6.28, fr=0.6+rng()*0.8, sc=(opts.scale||1)*(0.85+rng()*0.3);
      const fwd=back+Math.cos(tt*fr*0.7+ph)*L*0.15, lat=side+Math.sin(tt*fr+ph)*L*0.25;
      const hd=head+Math.sin(tt*fr*1.3+ph)*0.18;
      out.push({ x:lx+fwd*sh+lat*ch, y:ly+fwd*ch-lat*sh, z:(opts.z!=null?opts.z:-0.15)+lift+Math.sin(tt*fr+ph)*0.01,
        heading:hd, dir:dirOf(hd), frame:mod(Math.floor(t/ANIMS.swim.ms*fr+ph*2), ANIMS.swim.n), scale:sc });
    }
    return out;
  }
  root.FishIso2 = { W, H, pivot:{x:cx,y:cy}, KEY, SPOIL, WATER, KEYLINE_DEFAULT, ORDER, SPECIES, ANIMS, AORDER, MOTION, POSE,
    RESTS, RPOSE, defaultElev:DEFAULT_ELEV, render, mouth, hold, sizeOf, project, shoal, dirOf, sheetOrder,
    PW, PH, PLATE_LOFT, PLATE_ALL, MARKS, renderPlate, LOOK_DEFAULT, LOOK_MIN, LOOK_GILL, LOOK };
})(typeof globalThis!=='undefined'?globalThis:window);

/* Hidden Harbours — ISO HEAD rig (pass 3). The head is now its OWN rig: characterIsoRig2.js
   delegates skull + hair + beard + face to this file, and it also renders standalone portraits.
   ---------------------------------------------------------------------------------------------
   WHY IT IS SEPARATE
   The head carries almost all of the character's expression but occupies ~10x10 px. At that size
   the body's approach (3D quads for eyes, hair assembled from a cap + fringe + locks + clumps)
   fails twice over: eye quads land on sub-pixel boundaries so the eyes drift and smear frame to
   frame, and the separate hair pieces leave GAPS where scalp shows through. Both are fixed here
   by changing technique, not by nudging numbers:

     1. HAIR IS ONE CLOSED SHELL. A 16-segment x 6-ring grid lathed over the skull, with a
        per-angle HAIRLINE (low at the nape, mid at the sideburn, high at the brow) and a
        deterministic clump field displacing the thickness. Because it is a single closed surface
        that converges to one crown ring, there is no seam anywhere and therefore no bald spot.
        The jagged bottom edge that used to need separate fringe wedges now falls out of the
        hairline table.

     2. THE FACE IS STAMPED IN PIXELS, NOT MODELLED. After the mesh is z-buffered, each eye's
        3D anchor is projected, ROUNDED TO AN INTEGER PIXEL, depth-tested against the buffer
        (so hair and profile facings occlude it for free), and a fixed pixel pattern is written
        in. The eye is therefore exactly the same crisp 2x2 mark in every facing and every frame
        -- and gaze/blink become 1px pattern edits instead of geometry, which is what makes
        procedural eye movement possible at all.

   PROCEDURAL LIFE
     look(opts) -> {gaze:[dx,dy], lid:0..1, brow:-1..1}
       · opts.t (seconds)     -> free-running: saccades hold ~1.1 s then dart 1 px; blinks every
                                 ~3.4 s with jitter and an occasional double-blink. Deterministic
                                 in t (hash-based), so two callers at the same t always agree.
       · opts.anim + opts.u   -> LOOP-SAFE schedule for baked sheets (idle blinks once per cycle,
                                 gaze steps through a table that returns to centre at u=1).
       · opts.look            -> explicit override, for portraits and dialogue.

   BUILD AXES: skin, hair, hairStyle 'crop'|'mop'|'bob'|'bun'|'buzz'|'bald', beard 'none'|
   'stubble'|'moustache'|'goatee'|'mutton'|'full', eyes, face 'dash'|'bright'|'soft', weight.

   REQUIRES Art/eyeIsoRig.js for the eye + brow layer (see the note at the eye block below).
   PASS 6 (this file, headIsoRig2.js) — four changes, all of them about READING the lower face:
     A LOWER FACE IS 20% LONGER AND THE JAW 12% WIDER. Pass 3 spent 0.174 m of skull below the eye
       line, which at 32 px/m is 4.3 rows for brow-shadow + nose + mouth + chin + jaw — and a beard
       has to live inside the last two. Every beard therefore came out as the same 4x4 smudge. The
       chin now drops to -0.212 and the jaw corner widens to 0.108, giving 5.4 rows and ~7 px of
       jaw width: enough that a goatee, a chinstrap and mutton chops are three different shapes.
     B BEARDS ARE SILHOUETTE, NOT TEXTURE. Eight styles, and each one BREAKS THE OUTLINE somewhere
       different: mutton flares at the temples, chinstrap traces the jaw edge, goatee spikes below
       the chin, full wraps both, long hangs onto the chest. New per-shell vt[] table tapers a
       beard's thickness along its height, so 'full' can be fat at the chin and thin at the ear
       instead of a uniform rind.
     C HEAD SIZE IS A PARAMETER. skullProf/facesOf/stamp take a size k (and a jaw width), so a
       child can carry a big head on a small body and a woman a narrower jaw, without a second rig.
     D HATS ARE SOLVED, NOT AUTHORED. Pass 6's four hats all deleted the face, and the reason is one
       line of geometry nobody had written down: h = z*cos(e) - y*sin(e). Raising a point lifts it on
       screen AND so does moving it BACKWARD, because the camera looks over the head -- but moving it
       FORWARD drops it, at 0.64 of a row per centimetre. So the above-eye skull is a generous 4.6
       rows tall, and the only two things that can cost you the face are the two things pass 6 built
       its hats out of: a thick front band and a forward brim. A 6 cm ball-cap peak at brow height
       lands TWO ROWS BELOW the eye. A geometrically honest ball cap has no eyes.
       Pass 7 therefore authors the front parts in ROWS ABOVE THE EYE and solves their z with zRows()
       -- one bisection against the real profile, so it holds at any head size, jaw or weight. Band
       thickness and peak reach become free style choices instead of face-deleting risks, and the
       crown's hairline is authored RELATIVE to the solved band, so a hat cannot come apart.
     E SIX HATS, FIVE PARTS, NO TWO USING THE SAME SET. Four rounded caps with a brim ring were four
       identical blobs at 10 px. See the HAT table.

   EXPORTS globalThis.HeadIso2 = { PW,PH,pivot, EYE_Z,EYE_X, HAIRSTYLES,BEARDS,HATS,FACES,
     skullProf, facesOf(hc,b,look,wS), stamp(px,hc,b,look,B,MATS), look(opts),
     render(dir,opts), anchors(dir,opts) }.
   The `B` camera-basis object and the px buffer shape are the SAME contract the body rig's
   camBasis/_paint already use -- that is what lets the body rig hand its buffers straight over. */
(function (root) {
  const DEG = Math.PI/180, DEFAULT_ELEV = 40;
  /* PIXEL SCALE. S is px per metre. The whole rig is metric geometry, so raising S renders the SAME
     character at a higher pixel density -- and q scales the pixel-authored face stamp with it. */
  let S = 32, q = 1;
  function setScale(px){ S = px||32; q = Math.max(1, Math.round(S/32)); return q; }
  let PW = 40, PH = 40, pcx = 20, pcy = 20;        // standalone portrait cell; head centre lands here
  function setPortraitScale(px){ const k=(px||32)/32; PW=Math.round(40*k); PH=Math.round(40*k); pcx=Math.round(20*k); pcy=Math.round(20*k); }

  const HAIRSTYLES = ['crop','mop','bob','long','bun','ponytail','buzz','bald'];
  const BEARDS     = ['none','stubble','moustache','chinstrap','goatee','mutton','full','long'];
  const HATS       = ['none','watchcap','souwester','ballcap','flatcap','kerchief','hood'];
  const FACES      = ['dash'];        // one treatment — see eyeIsoRig pass 5
  const EXPRS      = ['neutral','smile','grin','frown','worry','oh','grit','weary'];
  const EYESHAPES  = ['round','narrow','droop','sharp','wide'];
  const BROWSHAPES = ['natural','flat','raised','angled','worried','thick','none'];

  /* ---------------- THE EYE, AS A CELL ----------------
     An eye is a 2x2 (sometimes 1x2 or 3x2) pixel cell with real internal structure, not a solid
     block. Row 0 is the LID/LASH in ink -- the darkest pixel on the head. Row 1 is the open eye:
     one pixel of iris colour and one of lit sclera. That split is what makes GAZE possible without
     moving the eye: the lid stays put and only the iris pixel slides, which is how hand-pixelled
     characters look around. Moving the whole 2x2 block (what pass 3 did) reads as the head
     twitching, not the eyes moving.

     PERSPECTIVE: in the 3/4 facings the far eye compresses to a single ink pixel. Drawing both
     eyes at full width is the main reason a 3/4 sprite reads flat.

     BROW: an eye-ALIGNED cell -- the same width as the eye it sits over, never wider. A 3 px brow
     over a 2 px eye pushed its inner pixel into the 2 px nose-bridge gap, so the two brows nearly
     met and the whole band read as clutter. Aligned, each brow is a discrete mark over its eye.
     An arch cannot exist in two pixels, so the vocabulary here is what two pixels CAN say: how high
     the brow rides (lift), which end is lower (dy, inner to outer), and how heavy it is (thick).
     Expression tilts the inner end on top of that -- which is where anger and worry actually live
     -- and lk.raise lifts the whole brow. It is drawn by DEFAULT: pass 3 only drew it for
     expressions, which cost every neutral face its main age and character signal. */
  const BROWS = {
    natural:{ dy:[ 0,0], lift:1, thick:false },   // one clear row of forehead beneath it
    flat:   { dy:[ 0,0], lift:0, thick:false },   // rides low on the lid -- heavy-browed
    raised: { dy:[ 0,0], lift:2, thick:false },   // high and open
    angled: { dy:[ 1,0], lift:1, thick:false },   // inner end drops toward the nose -- stern
    worried:{ dy:[-1,0], lift:1, thick:false },   // inner end lifts -- pleading
    thick:  { dy:[ 0,0], lift:1, thick:true  },   // two rows deep
    none:   null,
  };

  /* MOUTH is a 3x2 px stamp, authored as [dx, dy, material] relative to the projected mouth
     anchor. Three pixels is all there is at this scale, so an expression is carried by WHICH of
     them sit a row higher: corners up = smile, corners down = frown. Each expression also carries
     a LID bias (how far the eyes are already closed before any blink) and a BROW raise/tilt --
     between them the eyes do more of the expressing than the mouth does.
     NEUTRAL IS ONE PIXEL. It was two, at dx -1 and 0, which is not centred on the anchor: sitting
     a row under the nose mark it built a three-pixel dark cluster off to one side of an eight-pixel
     face — it read as a smudge on the cheek, not a mouth. Expressions keep their three pixels;
     those are symmetric about the anchor, so they stay centred. */
  /* 3.2 TWO VALUES PER MOUTH. A mouth of lip pixels alone is either one dot (nothing) or a bar (a
     moustache); the second value is skin -1 — the shadow a lip throws — and it goes IN the row, at
     the ends or under the lifted part, never on the row below, which the chin highlight owns on
     half the builds (the two are 1.5 rows apart and round differently per head size). skinL is a
     tooth; the stamp swaps it for shadow on skins lighter than bronze, where it is invisible.
     `cheek` asks the eye layer for the smile squint (only drawable at q >= 2, see eyeIsoRig). */
  const EXPR = {
    neutral:{ m:[[0,0,'lip'],[-1,0,'skinD'],[1,0,'skinD']],                            brow: 0, lid:0.00, raise:0 },
    smile:  { m:[[-1,-1,'lip'],[0,0,'lip'],[1,-1,'lip'],[-1,0,'skinD'],[1,0,'skinD']],  brow: 0, lid:0.16, raise:0, cheek:1 },
    grin:   { m:[[-1,-1,'lip'],[0,-1,'lip'],[1,-1,'lip'],[0,0,'skinL']],               brow: 0, lid:0.28, raise:0, cheek:1 },
    frown:  { m:[[-1,0,'lip'],[0,-1,'lip'],[1,0,'lip'],[0,0,'skinD']],                 brow: 1, lid:0.00, raise:0 },
    worry:  { m:[[-1,-1,'lip'],[0,0,'lip'],[1,0,'lip'],[-1,0,'skinD']],                brow:-1, lid:0.00, raise:1 },
    oh:     { m:[[0,-1,'lip'],[0,0,'lip'],[-1,0,'skinD'],[1,0,'skinD']],               brow:-1, lid:0.00, raise:1 },
    /* 3.1: grit was five pixels — wider than the eye span — and on a stubbled build it read as a
       moustache in every run frame. Three, one row up, plus the lid and brow, is the effort read. */
    grit:   { m:[[-1,-1,'lip'],[0,-1,'lip'],[1,-1,'lip'],[-1,0,'skinD'],[1,0,'skinD']], brow: 1, lid:0.26, raise:0 },
    weary:  { m:[[-1,0,'lip'],[0,0,'lip'],[1,0,'skinD']],                              brow:-1, lid:0.44, raise:0 },
  };

  /* Face geometry constants. Under a 40deg camera the FACE (front surface, chin to hairline)
     projects to only ~6 rows even though the head silhouette is ~11 tall -- points in front of
     the head centre slide DOWN the screen by y*sin(elev). Those six rows are budgeted:
     brow / eye / eye / nose / mouth / chin. EYE_Z is tuned against that, not against the skull. */
  /* PASS-3 FACE BUDGET. EYE_Z was 0.105 on a skull spanning -0.162..0.212 — 65% up the head. Under
     the 40deg camera the hairline had to sit ABOVE that to clear it, which is geometrically almost
     at the crown, so the fringe became a thin cap and five rows of bare forehead opened up between
     it and the eyes. Worse, when the fringe DID reach the eye row the stamp's visible() probe slid
     the whole face down a row to find bare skin, dropping the eyes onto the mouth line. The eyes
     now sit near the skull's mid-height, which is where they belong anatomically and which leaves
     the hairline free to sit at a real hairline height. */
  /* Row budget, solved against the profile above at elev 40 (rows relative, +1 = one row lower):
       hairline -0.2 | brow 0.1 | eye lid 1.1 | iris 2.1 | nose 3.1 | mouth 4.3 | chin 5.4
     Each constant is the z that lands its feature on that row. They are ABSOLUTE, not hung off
     EYE_Z, so moving the eye line does not drag the whole face down with it. */
  const EYE_Z = 0.055, EYE_X = 0.048;
  const NOSE_Z = -0.014, MOUTH_Z = -0.088, CHIN_Z = -0.150;
  // The eye is the darkest thing on the face, always. Taking it from the hair ramp (as the body
  // rig did) gives a mid-gold mark on gold hair and blond brows -- no contrast, no clarity.
  const INK = ['#12181b', '#243036'];

  /* PASS-3 SKULL: FORE-AFT SQUASHED, CROWN FLATTENED.
     Two projection facts drive this table. (1) Under a 40deg camera everything in front of the head
     centre slides DOWN the screen by y*sin(elev), so a geometrically round head spends most of its
     rows on scalp -- 8 of 13 in the first pass-3 build, leaving four for brow/eye/nose/mouth/chin.
     (2) The TOP of the silhouette is set by the OCCIPUT, not the crown, because the camera looks
     over it. So the fore-aft radii are 0.80 of anatomical and the z above the eye line is cut ~30%,
     while the jaw keeps its full drop. Result: ~5 rows of hair over ~5 rows of face, which is the
     reference boards' split, at the same camera. Everything derived -- hair shell, beard shell,
     face-stamp anchors -- reads this profile, so it all follows. */
  /* PASS 6: THE LOWER FACE IS THE EXPENSIVE PART, SO IT GETS THE ROWS.
     size = head scale (a child's head is nearly adult-sized on a much smaller body); jaw = jaw and
     chin width only (a heavier male jaw, a narrower female one) — it deliberately does NOT touch
     the cheekbone or temple, because widening those just makes the whole head fatter.
     Below the eye line: 0.212 m, up from pass 3's 0.178. Above it: 0.174, down from 0.178. Same
     total head, 5.4 face rows instead of 4.3 — which is the difference between one beard and eight. */
  function skullProf(wS, size, jaw){
    const k = 1 + ((wS||1)-1)*0.35, m = size==null?1:size, j = jaw==null?1:jaw;
    return [
      [-0.212*m, 0.040*k*j*m, 0.046*m],   // chin point — the pixel a goatee hangs off
      [-0.186*m, 0.070*k*j*m, 0.062*m],   // chin
      [-0.146*m, 0.108*k*j*m, 0.084*m],   // jaw corner — wide enough for a chinstrap to trace
      [-0.078*m, 0.140*k*m,   0.100*m],   // cheek
      [-0.004*m, 0.157*k*m,   0.110*m],   // eye line (widest fore-aft)
      [ 0.048*m, 0.160*k*m,   0.110*m],   // temple
      [ 0.098*m, 0.146*k*m,   0.099*m],   // upper crown
      [ 0.144*m, 0.098*k*m,   0.068*m],   // crown — sets the silhouette apex
      [ 0.174*m, 0.038*k*m,   0.028*m],   // top
    ];
  }
  // scale a hair/beard/hat shell option block with the head
  function scaleOpt(o, m){
    if(m===1 || m==null) return o;
    const r = {}; for(const key in o) r[key] = o[key];
    r.top = o.top*m; r.base = o.base*m; if(o.bulge!=null) r.bulge = o.bulge*m;
    if(o.bot!=null) r.bot = o.bot*m;
    if(o.hl) r.hl = o.hl.map(v=>v*m);
    return r;
  }
  function profR(prof, dz, which){
    if(dz<=prof[0][0]) return prof[0][which];
    for(let i=0;i+1<prof.length;i++){
      const a=prof[i], b=prof[i+1];
      if(dz<=b[0]){ const t=(dz-a[0])/((b[0]-a[0])||1); return a[which]+(b[which]-a[which])*t; }
    }
    return prof[prof.length-1][which];
  }

  /* ---------------- ROW SPACE ----------------
     h = z*cos(elev) - y*sin(elev) is a point's height on screen. The two terms PULL APART: up lifts,
     and so does BACKWARD (the camera is looking over the head), while FORWARD drops at sin(elev) --
     0.64 of a row per centimetre at the reference camera. Consequences, all of them counter-intuitive
     and all of them measured rather than assumed:
       . the bare skull spends 4.6 rows above the eye line and only 1.4 below it before the brow,
       . a brim reaching 6 cm forward lands 1.2 rows LOWER than its root, which is why every pass-6
         hat ate the eyes and why a real ball cap cannot be modelled honestly at this camera,
       . a brim reaching backward lands HIGHER, so the one place a hat can widen is the back-top of
         the silhouette -- which is exactly what a sou'wester looks like from above and behind.
     Anything that can reach the eye is therefore authored in ROWS ABOVE THE EYE and its z is solved.
     Solved at the REFERENCE camera (40 deg): facesOf never sees the elevation, and a baked sheet has
     to be deterministic. Across the 30-50 deg slider a solved edge moves less than half a row. */
  const PXM = 32, ROW = 1/PXM, RSE = Math.sin(DEFAULT_ELEV*DEG), RCE = Math.cos(DEFAULT_ELEV*DEG);
  const hAt = (y,z)=> z*RCE - y*RSE;
  function hEyeRow(prof, m, wS){
    const p = surfaceAt([0,0,0], prof, EYE_X*wS*m, EYE_Z*m, 0.010);
    return hAt(p[1], p[2]);
  }
  /* The z at which a ring/plate whose front edge stands `out` metres proud of the profile lands
     exactly `rows` rows above the eye row. h is monotonic in z (dh/dz = cos e - r'(z) sin e > 0
     everywhere on this profile), so a bisection is exact and needs no seed. */
  function zRows(prof, rows, out, m, wS){
    const target = hEyeRow(prof, m, wS) + rows*ROW;
    let lo = -0.30*m, hi = 0.40*m;
    for(let i=0;i<32;i++){
      const mid = (lo+hi)/2;
      if(hAt(profR(prof,mid,2)+(out||0), mid) < target) lo = mid; else hi = mid;
    }
    return (lo+hi)/2;
  }
  const rowsAbove = (prof, y, z, m, wS)=> (hAt(y,z) - hEyeRow(prof,m,wS)) * PXM;

  /* ---------------- HAIR: one closed shell ----------------
     hl = hairline z at 12 angles from the FRONT (0deg) round to the back (180deg) and on; the
     shell is mirrored, so only 7 entries are authored. th = thickness multiplier at the same
     angles (front-heavy = a fringe that juts over the brow). */
  /* PASS-3 RETUNE — this table caused the bald patches. thickness = base * th * (1 + clump*lump),
     and lump swings +-1.0, so mop's clump 0.62 with base 0.015 drove thickness to 0.006 m at some
     angles — under the 0.002 floor's practical resolution, where the shell lies ON the skull and
     the brighter skin quads win the depth test. Every style now carries a FATTER base and a
     SMALLER clump: same organic outline, but thickness can no longer collapse. The front hairline
     also drops ~0.03 m so the fringe sits a pixel above the brow instead of three, which is where
     the reference boards put it. */
  const HAIR = {
    crop: { top:0.196, hl:[ 0.124, 0.113, 0.058,-0.009,-0.045,-0.072,-0.086], th:[1.00,0.98,0.92,0.90,0.94,0.98,1.02], base:0.015, clump:0.24 },
    mop:  { top:0.214, hl:[ 0.110, 0.100, 0.037,-0.034,-0.075,-0.105,-0.126], th:[1.32,1.24,1.06,1.00,1.06,1.16,1.26], base:0.019, clump:0.28 },
    bob:  { top:0.206, hl:[ 0.116, 0.100,-0.009,-0.100,-0.143,-0.166,-0.176], th:[1.20,1.16,1.08,1.04,1.08,1.14,1.20], base:0.019, clump:0.20 },
    bun:  { top:0.198, hl:[ 0.132, 0.121, 0.064, 0.003,-0.036,-0.062,-0.077], th:[1.04,1.00,0.94,0.92,0.96,1.00,1.04], base:0.016, clump:0.18 },
    // long / ponytail: the hairline runs WELL below the jaw at the sides and back. At 32 px/m this
    // is the single clearest silhouette signal available for a long-haired character.
    long: { top:0.204, hl:[ 0.118, 0.098,-0.078,-0.246,-0.300,-0.326,-0.336], th:[1.20,1.18,1.14,1.10,1.14,1.20,1.26], base:0.022, clump:0.16 },
    pony: { top:0.202, hl:[ 0.126, 0.114, 0.040,-0.030,-0.062,-0.082,-0.094], th:[1.06,1.04,0.98,0.94,0.98,1.04,1.10], base:0.017, clump:0.16 },
    buzz: { top:0.190, hl:[ 0.140, 0.130, 0.081, 0.022,-0.013,-0.036,-0.050], th:[0.74,0.72,0.68,0.66,0.68,0.72,0.76], base:0.010, clump:0.12 },
  };
  // beard shells: arc half-width in segments, plus top/bottom z. Same shell machinery as the hair.
  /* BEARDS — eight, and no two of them break the silhouette in the same place.
     arc  = how far round the jaw the shell wraps (1.0 = ear to ear across the front)
     top/bot = z span; bot BELOW the chin point (-0.212) hangs past the jaw, which is where a
               goatee, a full beard and a long beard get their outline from
     vt[] = thickness multiplier along the shell's height, BOTTOM first (V = 0, .24, .48, .70, .87, 1).
            This is the pass-6 addition: it lets a beard be fat where it should be fat. A uniform
            rind is what made every style read as the same smudge.
     Anything whose bot goes past the profile's last ring picks up that ring's radius, so vt is
     also what stops a long beard from becoming a 1 px needle. */
  const BEARD = {
    // stubble reads as a DITHERED SKIN SHADOW on the jaw, not as hair -- that is what stubble is
    stubble:   { arc:0.74, top: 0.004, bot:-0.206, base:0.005, clump:0.34, mat:'stub',
                 vt:[0.9,1.0,1.0,1.0,0.9,0.7] },
    /* 3.3 (T5): three of the eight were re-authored as MASKS THAT LEAVE SKIN. arc is HALF-TURNS of
       the shell (0.5 = ear to ear), not the ear-to-ear fraction the old comment claimed, so the
       chinstrap's 0.94 wrapped the nape.
       moustache: one row, BETWEEN the nose mark and the mouth row, so the lip stays skin-coloured
       under it. It was centred on the mouth (-0.088) and the ink lip sat inside a 5 px bar. Still
       wide: at this scale a 3 px moustache is a nose shadow. */
    moustache: { arc:0.30, top:-0.004, bot:-0.070, base:0.022, clump:0.10, mat:'beard',
                 vt:[0.9,1.0,1.05,1.05,1.0,0.9] },
    // one row along the jaw EDGE, ear to ear, chin front bare: the face gains a dark bottom rim.
    // It ran from the nose line to the chin and read as a thin full beard.
    chinstrap: { arc:0.56, top:-0.138, bot:-0.214, base:0.016, clump:0.12, mat:'beard',
                 vt:[1.15,1.10,1.05,1.00,1.00,1.00] },
    // CHIN ONLY, plus the spike below it: the only style that adds height to the silhouette. The
    // top now sits under the mouth row (it started at the nose line and swallowed the lip).
    goatee:    { arc:0.26, top:-0.116, bot:-0.286, base:0.026, clump:0.30, mat:'beard',
                 vt:[1.85,1.55,1.20,1.00,0.95,0.95] },
    // flares at the TEMPLES and stops short of the chin — widens the head, not the jaw
    /* 3.1: the beard shells started ABOVE the mouth (top 0.014–0.058 is the cheek / eye-socket
       height) and the arc reached round to the face front, so full / long closed over the mouth row
       and left a slit for the eyes: a grey helmet. The tops now sit at the nose line and the front
       arc stops short of the mouth (the stamp keeps the mouth for full and long, see below). */
    mutton:    { arc:0.80, top: 0.010, bot:-0.150, base:0.024, clump:0.40, mat:'beard',
                 vt:[1.00,1.15,1.35,1.45,1.30,1.00] },
    full:      { arc:0.66, top:-0.040, bot:-0.264, base:0.024, clump:0.36, mat:'beard',
                 vt:[1.30,1.25,1.15,1.05,0.95,0.85] },
    // hangs onto the chest: unmistakable at any resolution
    long:      { arc:0.58, top:-0.046, bot:-0.408, base:0.026, clump:0.26, mat:'beard',
                 vt:[1.05,1.35,1.35,1.15,1.00,0.85] },
  };

  /* HATS — five parts, and no hat uses all five. Two hats built the same way cannot read as two
     hats at 10 px, which is what pass 6's four rounded caps proved.
       CROWN  closed shell above the hatline (cannot have holes). Buys HEIGHT with z.
       BAND   full ring at the hatline. Its thickness is SOLVED so the front edge lands `rows` rows
              above the eye whatever the head size. Always the darkest part: a hat at this scale is
              a light mass over a dark line.
     DEPTH BIAS IS NOT A PRIORITY DIAL. Pass 6 gave every hat part db 0.078 to force it over the hair.
     db is subtracted from depth, so that is 7.8 cm of cheating -- more than the head is deep -- and
     capLathe closes a ring with a solid disc, so the band painted THROUGH the face from behind. It
     also exceeded the eye stamp's 0.045 depth tolerance, so the eyes were silently dropped in every
     facing of every hat. A hat sits geometrically outside the skull and the hair; it wins the depth
     test on its own radius. Every part here carries db <= 0.030.
       PEAK   front-only sweep. Reaching forward RAISES it, so a peak is a bar over the brow -- there
              is no 40 deg camera where it shades the eyes. Pure silhouette.
       CAPE   rear-only sweep. Reaching backward LOWERS it, so this is the ONLY part that can put
              cloth beside and below the jaw. A sou'wester is mostly cape.
       SHADE  rows of darkened skin under the front edge. Without it the hat floats off the head.
     hairTop is the shell cut: hair shows as fringe and nape UNDER the hat instead of through it. */
  /* HATS — five parts, and no hat uses all five. Two hats built the same way cannot read as two
     hats at 10 px, which is what pass 6's four rounded caps proved.
       CROWN  closed shell over the band (cannot have holes). Its hairline `hl` is authored RELATIVE
              to the solved band top, front-first, so the crown can never detach from the band.
       BAND   full ring at the hatline. Its z is SOLVED so the front edge lands `rows` rows above the
              eye at any head size; `thick` is then a free style choice — a watch cap's roll bulges
              3 cm past the crown, a ball cap's is nearly flush. Always the dark part of the hat.
       PEAK   front sweep. Reaching forward DROPS it 0.64 rows per cm, so its z is solved from its
              reach and it always clears the eye by design. This is the honest cheat: a real peak at
              real brow height covers the eyes at 40 deg, so the peak keeps its LENGTH and gives up
              its height.
       CAPE   rear sweep. Reaching backward RAISES it, so a cape widens the back-top of the
              silhouette. Nothing else in the set does that; it is the sou'wester's whole read.
       SHADE  the row of dark skin the front edge casts. Without it the hat floats off the head.
     DEPTH BIAS IS NOT A PRIORITY DIAL. Pass 6 gave every hat part db 0.078 — 7.8 cm of depth cheat,
     more than the head is deep — so capLathe's closing disc painted THROUGH the face from behind, and
     the eye stamp's own 0.045 tolerance rejected the eye pixel outright. Hats sit outside the skull;
     they win the depth test on radius. Everything here is db 0.028. */
  const HAT = {
    // knit, no brim: a fat dark roll BULGING past a soft tall dome. The only hat whose widest point
    // is the band, and the only one taller than the hair it replaces.
    /* 3.1 FACE RESERVE. Every band or peak that used to sit 0.6–1.1 rows above the eye row put its
       dark edge on the row directly above the lash: hat edge + lash = a two-row bar, and the figure
       was blindfolded at S/SE/SW. Bands and peaks now clear the eye by ~1.6–1.9 rows, which leaves
       ONE row of skin (the brow row) between hat and lash, and the brim shade only exists where it
       cannot reach that row (see hatFaces). The hats ride one row higher; the silhouettes keep. */
    watchcap: { label:'watch cap',
      band:{ rows:1.9, h:0.044, thick:0.030, back:1.10 },
      crown:{ top:0.256, hl:[ 0.004, 0.002,-0.006,-0.016,-0.024,-0.028,-0.030], th:[1.00,1.02,1.06,1.12,1.16,1.18,1.18], base:0.026, clump:0.16 },
      shade:1 },
    // oilskin rain hat: small crown, and a CAPE flaring wide across the back-top — the one shape the
    // projection hands you for free, and the one nothing else in the set has.
    souwester:{ label:"sou'wester",
      band:{ rows:1.9, h:0.030, thick:0.016, back:1.06 },
      crown:{ top:0.222, hl:[ 0.004, 0.002,-0.008,-0.022,-0.032,-0.038,-0.040], th:[1.00,1.00,1.02,1.04,1.06,1.06,1.06], base:0.016, clump:0.03 },
      cape:{ turn0:0.16, turn1:0.84, z:-0.005, h:0.028, reach:0.135, rxK:1.00, xK:0.60 },
      shade:1 },
    // stiff dome + a PEAK. Solved to clear the eye by 0.7 of a row, so it keeps its length and reads
    // as a hard bar over the brow at every facing instead of as a blindfold at the front ones.
    ballcap:  { label:'ball cap',
      band:{ rows:1.7, h:0.026, thick:0.014, back:0.92 },
      crown:{ top:0.234, hl:[ 0.004, 0.002,-0.004,-0.014,-0.022,-0.026,-0.028], th:[1.00,1.00,1.00,1.02,1.04,1.04,1.04], base:0.022, clump:0.03 },
      peak:{ turn:0.150, rows:1.6, reach:0.062, h:0.018, rxK:1.00, xK:0.34 },
      shade:1 },
    // LOW and peaked: nothing else is both. A longer, wider peak than the ball cap's, and a crown
    // that slumps over the occiput, so the silhouette is a wedge rather than a dome.
    flatcap:  { label:'flat cap',
      band:{ rows:1.6, h:0.022, thick:0.014, back:1.06 },
      crown:{ top:0.200, hl:[ 0.002, 0.000,-0.006,-0.014,-0.026,-0.036,-0.044], th:[1.14,1.12,1.06,1.02,1.10,1.22,1.32], base:0.019, clump:0.05 },
      peak:{ turn:0.225, rows:1.6, reach:0.086, h:0.016, rxK:1.02, xK:0.40 },
      shade:1 },
    // the SMALLEST silhouette of the six — smaller than the bare hair it replaces, which IS the read.
    // Cloth down over the ears and nape, and a knot at the back.
    kerchief: { label:'kerchief',
      band:{ rows:1.9, h:0.020, thick:0.010, back:0.86 },
      /* 3.2: back thickness 1.06 -> 1.14. At 1.06 the cloth sat within a millimetre of the bun's hair
         shell at the nape and lost the depth test on scattered pixels: hair speckle through the cloth
         at N/NE/NW on Nan and the Packer. +0.2 px of silhouette. */
      crown:{ top:0.192, hl:[ 0.002,-0.014,-0.120,-0.212,-0.250,-0.266,-0.270], th:[0.90,0.90,0.98,1.08,1.14,1.16,1.16], base:0.013, clump:0.07 },
      knot:{ z:-0.064, r:0.038 }, shade:1 },
    // the BIGGEST: an oilskin hood swallows the head, opens 1.6 rows above the eye and frames the
    // face in two rows of shadow. Cloth all the way down past the jaw at the sides.
    hood:     { label:'oilskin hood',
      band:{ rows:1.7, h:0.046, thick:0.040, back:1.20 },
      crown:{ top:0.266, hl:[ 0.004,-0.014,-0.116,-0.208,-0.254,-0.276,-0.284], th:[1.06,1.10,1.20,1.28,1.34,1.36,1.36], base:0.038, clump:0.06 },
      shade:1 },
  };
  const HATDB = 0.028;
  // the solved hatline: shared by hatFaces (geometry) and facesOf (where to cut the hair)
  function bandZOf(prof, style, m, wS){
    const S2 = HAT[style]; if(!S2 || !S2.band) return null;
    return zRows(prof, S2.band.rows, S2.band.thick*m, m, wS);
  }

  const clampf=(v,a,b)=>v<a?a:(v>b?b:v);
  // deterministic clump field — same (k,j) always gives the same lump, so sheets are reproducible
  function lump(k,j,seg){
    const a = k/seg*Math.PI*2;
    return Math.sin(a*3+j*1.7) * 0.6 + Math.sin(a*5-j*0.9) * 0.4;
  }
  function mirrorTable(tab, k, seg){
    // k runs 0..seg-1 from the front; fold onto the authored 0..180deg half
    const half = seg/2;
    const kk = k<=half ? k : seg-k;
    const t = kk/half*(tab.length-1);
    const i = Math.min(tab.length-2, Math.floor(t)), f = t-i;
    return tab[i] + (tab[i+1]-tab[i])*f;
  }
  /* Shell builder. ang 0 = +y (the face), increasing toward +x. Rings share a single top ring so
     the surface CLOSES — that is the whole point: a closed surface cannot have a bald spot. */
  /* opt.cut = a z CEILING (hair under a hat). Pass 6 flattened the hair shell to top 0.156 when a
     hat was on, which is above every hatline, so the hair pushed through the cap and the two masses
     merged into one blob. A cut instead DELETES the columns whose hairline starts above the ceiling
     -- the crown and forehead, exactly the part the hat covers -- and leaves the rest at full
     thickness, so a bob still hangs to the jaw under a kerchief and a crop still shows at the nape.
     The scalp shell stops being closed when cut, which is safe: the hat's own crown shell is. */
  function shell(hc, prof, opt, seg, mat, bBias, closed, arc){
    const V = [0, 0.24, 0.48, 0.70, 0.87, 1];
    const grid = [], live = [];
    const cut = opt.cut;
    const kMax = closed ? seg : Math.round(arc*seg/2);
    const idx = [];
    for(let n=0; n<(closed?seg:2*kMax+1); n++) idx.push(closed ? n : (n-kMax+seg)%seg);
    for(let n=0; n<idx.length; n++){
      const k = idx[n], ang = k/seg*Math.PI*2;
      const sa = Math.sin(ang), ca = Math.cos(ang);
      const z0 = closed ? mirrorTable(opt.hl, k, seg) : opt.bot;
      const z1 = cut!=null ? Math.min(opt.top, cut) : opt.top;
      live.push(cut==null || z0 < z1-0.014);
      const thK = closed ? mirrorTable(opt.th, k, seg) : 1;
      const col = [];
      for(let j=0;j<V.length;j++){
        const z = z0 + (z1-z0)*V[j];
        const vtK = opt.vt ? opt.vt[j] : 1;
        const t = opt.base * thK * vtK * (1 + opt.clump*lump(k,j,seg)) * (closed ? (1 - 0.24*V[j]*V[j]) : 1);
        /* HARD FLOOR. A hair shell thinner than ~1/3 px is geometrically coincident with the skull
           and the skin quads under it win pixels at random — that IS the bald patch. Beards want
           to stay thin, so the floor only applies to the closed scalp shell. */
        const tmin = closed ? 0.013 : 0.004;
        const rx = profR(prof, z, 1) + Math.max(tmin, t);
        const ry = profR(prof, z, 2) + Math.max(tmin, t);
        col.push([hc[0] + sa*rx, hc[1] + ca*ry, hc[2] + z]);
      }
      grid.push(col);
    }
    const out = [];
    const nCols = grid.length, lastCol = closed ? nCols : nCols-1;
    for(let n=0; n<lastCol; n++){
      if(!live[n] || !live[(n+1)%nCols]) continue;
      const A = grid[n], B2 = grid[(n+1)%nCols];
      for(let j=0;j+1<V.length;j++){
        const kk = idx[n];
        // THE BALD-PATCH BUG. `deff = d - db`, so a NEGATIVE db pushes a surface AWAY from the
        // camera. The hair shell sits only ~0.02 m outside the skull, and it carried db:-0.012 --
        // which cancelled most of that clearance, so wherever the clump field thinned the shell the
        // skull's brighter skin quads won the depth test and punched holes through the hair. It is
        // now PULLED FORWARD, by more than the shell can ever be thin.
        const bb = bBias + (closed ? 0.05*lump(kk,j,seg) : 0.04*lump(kk,j,seg))
        /* FRINGE SHADOW. The lowest band of the scalp shell drops a ramp step. On the boards the
           hair does not simply stop above the eyes — its underside is in shadow, and that dark line
           is what the eyes read against. Without it the dashes sit against the brightest gold on
           the head and disappear at a glance. Doing it in the MESH shades the whole underside of
           the fringe (and the nape, correctly) instead of four eye-aligned pixels. */
                 + ((closed && j===0) ? -0.90 : 0);
        out.push({ v:[A[j], B2[j], B2[j+1], A[j+1]], mat, b:bb, db:(opt.db!=null?opt.db:(closed?0.026:0.010)) });
      }
    }
    if(closed && cut==null){   // crown cap
      const top = grid.map(c=>c[V.length-1]);
      out.push({ v:top, mat, b:bBias+0.10, db:(opt.db!=null?opt.db+0.004:0.026) });
    }
    return out;
  }

  // ---- small solids (kept local so this rig stands alone) ----
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s];
  const v_norm=(a)=>{ const m=Math.hypot(a[0],a[1],a[2])||1; return [a[0]/m,a[1]/m,a[2]/m]; };
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function lathe(c, prof, mat, b, db, seg){
    seg = seg || 10;
    const rings = prof.map(([dz,rx,ry])=>{
      const o=[]; for(let k=0;k<seg;k++){ const a=(k+0.5)/seg*Math.PI*2;
        o.push([c[0]+Math.cos(a)*rx, c[1]+Math.sin(a)*ry, c[2]+dz]); }
      return o;
    });
    const out=[];
    for(let i=0;i+1<rings.length;i++){ const r0=rings[i], r1=rings[i+1];
      for(let k=0;k<seg;k++){ const k2=(k+1)%seg; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:db||0}); } }
    out.push({v:rings[rings.length-1].slice(),mat,b:b||0,db:db||0});
    out.push({v:rings[0].slice().reverse(),mat,b:b||0,db:db||0});
    return out;
  }
  const ball=(c,r,mat,b,db)=>lathe(c,[[-r,r*0.42,r*0.42],[-r*0.62,r*0.80,r*0.78],[0,r,r*0.96],[r*0.62,r*0.80,r*0.78],[r,r*0.42,r*0.42]],mat,b,db,8);
  function tube(A,B2,r0,r1,mat,b,db){
    const ax=v_norm(v_sub(B2,A)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const rr=v_norm(v_cross(ax,up)), uu=v_cross(ax,rr), seg=8;
    const ring=(P,rad)=>{ const o=[]; for(let k=0;k<seg;k++){ const a=(k+0.5)/seg*Math.PI*2;
      o.push(v_add(P, v_add(v_mul(rr,Math.cos(a)*rad), v_mul(uu,Math.sin(a)*rad)))); } return o; };
    const a0=ring(A,r0), a1=ring(B2,r1), out=[];
    for(let k=0;k<seg;k++){ const k2=(k+1)%seg; out.push({v:[a0[k],a0[k2],a1[k2],a1[k]],mat,b:b||0,db:db==null?-0.05:db}); }
    out.push({v:a1.slice(),mat,b:b||0,db:db==null?-0.05:db});
    out.push({v:a0.slice().reverse(),mat,b:b||0,db:db==null?-0.05:db});
    return out;
  }

  /* Ring solid whose FORE-AFT radius differs front from back. A symmetric brim is unusable under a
     40 deg camera: everything in front of the head slides down the screen by y*sin(elev), so a brim
     that clears the forehead by 4 cm lands on the eyes. Front short, back long — which is what a
     sou'wester actually is. rings = [[dz, rx, ryFront, ryBack], ...]. ang 0 = +y = the face. */
  function capLathe(hc, rings, mat, b, db, seg){
    seg = seg || 14;
    const grid = rings.map(R2=>{
      const o=[];
      for(let k=0;k<seg;k++){
        const a=(k+0.5)/seg*Math.PI*2, sa=Math.sin(a), ca=Math.cos(a);
        const ry = ca>=0 ? R2[2] : (R2[3]==null?R2[2]:R2[3]);
        o.push([hc[0]+sa*R2[1], hc[1]+ca*ry, hc[2]+R2[0]]);
      }
      return o;
    });
    const out=[];
    for(let i=0;i+1<grid.length;i++){ const r0=grid[i], r1=grid[i+1];
      for(let k=0;k<seg;k++){ const k2=(k+1)%seg; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:db||0}); } }
    out.push({v:grid[grid.length-1].slice(),mat,b:b||0,db:db||0});
    out.push({v:grid[0].slice().reverse(),mat,b:b||0,db:db||0});
    return out;
  }

  /* Partial ring — a PEAK (front arc) or a CAPE (rear arc). capLathe could only do full rings, which
     is why pass 6 had to fake a peak with a back radius of 0.012 and got a lozenge. The reach tapers
     to zero at both ends with a sine so the part MERGES into the cap instead of ending in a step.
     Three faces per quad: top (lit), underside (the darkest thing on the hat), outer edge. */
  function sweep(hc, prof, ang0, ang1, z0, z1, reach, rxK, xK, mat, bb, db){
    const n = Math.max(4, Math.round(Math.abs(ang1-ang0)/(Math.PI*2)*30));
    const lo=[], lo2=[], hi=[], hi2=[];
    for(let i=0;i<=n;i++){
      const u=i/n, ang=ang0+(ang1-ang0)*u, sa=Math.sin(ang), ca=Math.cos(ang);
      const k=Math.sin(Math.PI*u);
      const ix=profR(prof,z0,1)*rxK, iy=profR(prof,z0,2);
      const ox=ix+reach*k*xK, oy=iy+reach*k;
      lo.push([hc[0]+sa*ix, hc[1]+ca*iy, hc[2]+z0]);
      lo2.push([hc[0]+sa*ox, hc[1]+ca*oy, hc[2]+z0]);
      hi2.push([hc[0]+sa*ox, hc[1]+ca*oy, hc[2]+z1]);
      hi.push([hc[0]+sa*ix, hc[1]+ca*iy, hc[2]+z1]);
    }
    const out=[];
    for(let i=0;i<n;i++){
      // the camera looks DOWN, so the top face is the visible one -- and on a brim it is the dark one
      out.push({v:[hi[i],hi2[i],hi2[i+1],hi[i+1]], mat, b:bb, db});
      out.push({v:[lo2[i],lo[i],lo[i+1],lo2[i+1]], mat, b:bb-0.70, db});
      out.push({v:[lo2[i],hi2[i],hi2[i+1],lo2[i+1]], mat, b:bb-0.35, db});
    }
    return out;
  }

  /* ---------------- HATS ---------------- */
  const T2R = (t)=>t*Math.PI*2;
  function hatFaces(hc, prof, b, m, wS){
    const style = b.hat, S2 = HAT[style];
    if(!style || style==='none' || !S2) return [];
    wS = wS || 1;
    const F=[], add=(fs)=>{ for(const f of fs) F.push(f); };
    let bandTop = 0.10*m;
    if(S2.band){
      const P = S2.band, t = P.thick*m;
      const z0 = bandZOf(prof, style, m, wS), z1 = z0 + P.h*m, bk = P.back==null?1:P.back;
      bandTop = z1;
      add(capLathe(hc, [
        [z0, profR(prof,z0,1)+t*0.90, profR(prof,z0,2)+t,      profR(prof,z0,2)+t*bk],
        [z1, profR(prof,z1,1)+t*0.96, profR(prof,z1,2)+t*1.04, profR(prof,z1,2)+t*bk*1.04]],
        'hatD', -0.85, HATDB, 18));
      /* BRIM SHADOW — the row of skin the front edge darkens. One row is 0.041 m of z here, and the
         z span is what has to be small: h falls at cos(elev) per metre going down, so 0.08 would
         reach the eyes and blind the figure just as surely as a badly placed brim. */
      /* 3.1: the shade may darken the brow row but never the lash row. Its lower edge is clamped to
         ~1.2 rows above the eye row; a band low enough that nothing is left above that gets no shade. */
      if(S2.shade){
        const zLo = Math.max(z0 - 0.038*m*S2.shade, zRows(prof, 1.2, 0.004*m, m, wS));
        if(zLo < z0 - 0.004*m)
          add(sweep(hc, prof, -T2R(0.185), T2R(0.185), zLo, z0, 0.004*m, 1.0, 0.4,
            'skinD', -1.30, 0.012));
      }
    }
    if(S2.crown){
      // scaleOpt returns the SOURCE object when m === 1, so this MUST copy: writing cr.hl in place
      // turned the table's relative offsets into absolutes and the crown climbed on every render.
      const cr = Object.assign({}, scaleOpt(S2.crown, m),
        { hl: S2.crown.hl.map(v => bandTop + v*m), db: HATDB });
      add(shell(hc, prof, cr, 16, 'hat', 0.55, true));
    }
    if(S2.peak){
      const P = S2.peak, reach = P.reach*m;
      const z0 = zRows(prof, P.rows, reach, m, wS), z1 = z0 + P.h*m;
      add(sweep(hc, prof, -T2R(P.turn), T2R(P.turn), z0, z1, reach, P.rxK, P.xK, 'hatD', -1.05, HATDB));
    }
    if(S2.cape){
      const P = S2.cape, z0 = P.z*m, z1 = z0 + P.h*m;
      add(sweep(hc, prof, T2R(P.turn0), T2R(P.turn1), z0, z1, P.reach*m, P.rxK, P.xK, 'hatD', -0.60, HATDB));
    }
    if(S2.knot){
      const P = S2.knot;
      /* 3.2: lower (nape, under the tied edge) and proud of the cloth by ~1 px, so it reads as a node on
         the cloth at E/W/N rather than a dark pixel inside the bun it used to share a centre with. */
      add(ball([hc[0], hc[1]-profR(prof,P.z*m,2)-P.r*m*0.80, hc[2]+P.z*m], P.r*m, 'hatD', -0.15, HATDB));
    }
    return F;
  }

  /* ---------------- PROCEDURAL LOOK ---------------- */
  function hash(n){ const x=Math.sin(n*127.1+311.7)*43758.5453; return x-Math.floor(x); }
  function freeLook(t, expr, talk){
    // saccades: hold a target ~1.1 s (jittered), then snap to the next one
    let acc=0, i=0;
    while(acc<=t && i<4096){ const hold=0.72+1.05*hash(i*3.7); if(acc+hold>t) break; acc+=hold; i++; }
    const gx = [0,0,1,0,-1,0,1,-1,0,0][Math.floor(hash(i*5.3)*10)] || 0;
    const gy = hash(i*8.9)>0.84 ? -1 : 0;
    /* blinks: ~3.4 s apart, ~0.13 s long, sometimes doubled. lid RAMPS 0->1->0 across the window
       rather than snapping, so at any real frame rate the half-lid stage is visible and the blink
       reads as a movement instead of a one-frame pop. */
    let bacc=0, j=0, lid=0;
    const ramp=(u)=>1-Math.abs(u*2-1);
    while(bacc<=t && j<4096){
      const gap=2.4+2.0*hash(j*11.3), dur=0.13+0.06*hash(j*13.1);
      if(t>=bacc+gap && t<bacc+gap+dur){ lid=ramp((t-bacc-gap)/dur); break; }
      const dbl = hash(j*17.7)>0.78, d2=bacc+gap+dur+0.09;
      if(dbl && t>=d2 && t<d2+dur*0.8){ lid=ramp((t-d2)/(dur*0.8)); break; }
      bacc+=gap+dur+(dbl?0.09+dur*0.8:0); j++;
    }
    const base = expr || 'neutral';
    /* talk: flip between the expression and an open mouth, ~4.5 Hz jittered. 3.1: the BROWS, LID and
       RAISE come from the base expression, not from the mouth frame — 'oh' carries brow -1 / raise 1,
       and taking them from the mouth frame lifted the brows on every flap, a 7 Hz face twitch. */
    const mouth = talk ? ((hash(Math.floor(t*4.5))>0.42) ? 'oh' : base) : base;
    const E = EXPR[base] || EXPR.neutral;
    return { gaze:[gx,gy], lid:Math.max(lid, E.lid||0), mouth,
             brow:E.brow + (hash(i*23.1)>0.90 ? 1 : 0), raise:E.raise||0, cheek:E.cheek||0 };
  }
  /* 3.2 BLINK CADENCE. A baked idle loop blinks once per cycle: at 6 f × 170 ms that is a blink every
     1.02 s, twice a resting person's rate, and on a wharf of ten it is a flicker. The blink is gated
     on the LOOP index: blink loops step through a 2-or-3 schedule (hash-picked, `seed` shifts it per
     figure), so a figure blinks every 2nd–3rd loop. No loop given = the old once-per-loop. A sheet
     bakes TWO idle rows — loop 0 (blinks) and loop 1 (still) — and the engine picks the row per loop
     with blinks(k, seed). Free-running callers (opts.t) keep the ~3.4 s clock. */
  function blinks(loop, seed){
    if(loop==null || !isFinite(loop)) return true;
    const k = Math.floor(loop), s = (seed||0)*3.17;
    let acc=0, n=0;
    while(acc<k && n<4096){ acc += hash(n*7.31+s)>0.5 ? 3 : 2; n++; }
    return acc===k;
  }
  function loopLook(anim, u, expr, talk, loop, seed){
    // loop-safe: returns to centre + open at u=1 so a baked sheet cycles seamlessly
    const calm = anim==='idle' || anim==='hold' || anim==='balance' || anim==='bite' || anim==='portrait';
    // three-stage blink inside the window, so even a 6-frame bake catches a half-lid frame
    let lid = 0;
    if(calm && blinks(loop, seed)){ lid = u<0.60||u>=0.80 ? 0 : (u<0.645||u>=0.755 ? 0.45 : 1); }
    let gx = 0;
    if(calm){ gx = u<0.20 ? 0 : u<0.42 ? 1 : u<0.58 ? 0 : u<0.86 ? -1 : 0; }
    // exertion states carry their own expression unless one was asked for explicitly
    const auto = {strike:'grit', reel:'grit', stagger:'oh', run:'grit', land:'grin', dig:'grit'}[anim];
    const base = expr || auto || 'neutral';
    const mouth = talk ? ((u%0.5)<0.25 ? 'oh' : base) : base;
    const E = EXPR[base] || EXPR.neutral;   // 3.1: brows/lid from the expression, never the talk frame
    return { gaze:[gx,0], lid:Math.max(lid, E.lid||0), mouth, brow:E.brow, raise:E.raise||0, cheek:E.cheek||0 };
  }
  function look(opts){
    opts = opts || {};
    const expr = opts.expr, talk = !!opts.talk;
    const base = opts.t!=null ? freeLook(opts.t, expr, talk)
                              : loopLook(opts.anim||'idle', opts.u!=null?opts.u:0, expr, talk, opts.loop, opts.seed);
    return opts.look ? Object.assign(base, opts.look) : base;
  }

  /* ---------------- MESH ---------------- */
  const HAIRKEY = { long:'long', ponytail:'pony' };
  function facesOf(hc, b, lk, wS, m){
    wS = wS || b.weight || 1; m = m || 1;
    const prof = skullProf(wS, m, b.jaw), F = [];
    const add=(fs)=>{ for(const f of fs) F.push(f); };
    add(lathe(hc, prof, 'skin', 0.20, 0, 10));
    /* CHIN BLOCK. Dropped from -0.124 to -0.158 and fattened, because the whole point of pass 6 is
       that the jaw has rows to spend: this solid is what stops the longer lower face from reading as
       a flat blank and gives the beard shells something with form under them. */
    add(ball([hc[0], hc[1]+profR(prof,-0.158*m,2)-0.016*m, hc[2]-0.158*m], 0.046*wS*m, 'skin', 0.34, 0.006));
    const style = b.hairStyle || 'crop';
    const hk = HAIRKEY[style] || style;
    const hatOn = !!(b.hat && b.hat!=='none' && HAT[b.hat]);
    if(style!=='bald' && HAIR[hk]){
      let ho = scaleOpt(HAIR[hk], m);
      /* Cutting AT the hatline leaves 2 rows of hair round the temples, which on a blond head reads
         as gold earmuffs under the hat. Cut below it and thin it: a rim, not a band. */
      if(hatOn) ho = Object.assign({}, ho,
        { cut: bandZOf(prof, b.hat, m, wS) - 0.080*m, base: ho.base*0.74 });   // 3.1: bands ride ~1 row higher, cut follows
      add(shell(hc, prof, ho, 16, 'hair', 0.18, true));
    }
    if(style==='bald')
      add(lathe(hc, [[0.126*m, profR(prof,0.126*m,1)*1.01, profR(prof,0.126*m,2)*1.01],
                     [0.160*m, profR(prof,0.160*m,1)*1.02, profR(prof,0.160*m,2)*1.02],
                     [0.172*m, 0.036*m, 0.026*m]], 'skin', 0.30, 0.005, 10));
    // a bun sits UNDER a hat, so it moves to the nape rather than pushing through the crown
    if(style==='bun'){
      const bz = hatOn ? -0.010*m : 0.136*m;
      // 3.1: 1 cm further back — at 0.038 the lit lobes peeked past the temples at S as two bright
      // pixels at eye height (earrings on Nan)
      /* 3.2: under a KERCHIEF the bun is not drawn — the cloth is tied over it. Drawn in hair it
         z-fought the cloth shell it poked through (white speckle across the nape at N/NE/NW on Nan,
         the knot sitting on hair instead of cloth at E/W); drawn in cloth it speckled dark. The
         kerchief silhouette is the smallest of the set on purpose, and the knot marks the nape. */
      if(!(hatOn && b.hat==='kerchief'))
        add(ball([hc[0], hc[1]-profR(prof,bz,2)-(hatOn?0.030:0.048)*m, hc[2]+bz], (hatOn?0.048:0.056)*wS*m, 'hair', 0.14, 0.014));
    }
    /* PONYTAIL. A tube out of the nape, dropping behind the shoulder. It is drawn even in the front
       facing (it peeks past the neck), which is deliberate: the tail is the whole read. */
    if(style==='ponytail'){
      const y0 = -profR(prof,0.020*m,2);
      add(tube([hc[0], hc[1]+y0-0.010*m, hc[2]+0.030*m], [hc[0], hc[1]+y0-0.052*m, hc[2]-0.180*m],
        0.044*m, 0.026*m, 'hair', -0.06, 0.030));
    }
    const bd = b.beard && b.beard!=='none' ? BEARD[b.beard] : null;
    if(bd) add(shell(hc, prof, scaleOpt(bd, m), 16, bd.mat, -0.10, false, bd.arc));
    add(hatFaces(hc, prof, b, m, wS));
    return F;
  }

  /* ---------------- FACE STAMP (the reason the eyes are stable) ---------------- */
  function projWith(p, B, cx, cy){
    const x=p[0], y=p[1], z=p[2];
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S - (B.heave||0), d:(yr*B.ce-zr*B.se) };
  }
  /* Point on the skull surface at (x, z), pushed `out` metres along the OUTWARD NORMAL. Pushing
     along +y instead (which is what pass 3 did) works head-on but contributes nothing to depth in
     the E/W profile facings, so the depth test buried both eyes inside the skull and the profiles
     came out eyeless. The normal is the ellipse gradient, so the push always moves the anchor
     toward the camera whatever the facing. */
  function surfaceAt(hc, prof, dx, dz, out){
    const rx = profR(prof, dz, 1), ry = profR(prof, dz, 2);
    const s = clampf(Math.abs(dx)/rx, 0, 0.999);
    const y = ry*Math.sqrt(1-s*s);
    let nx = dx/(rx*rx), ny = y/(ry*ry);
    const m = Math.hypot(nx, ny) || 1; nx/=m; ny/=m;
    return [hc[0]+dx+nx*out, hc[1]+y+ny*out, hc[2]+dz];
  }
  function dirOfBasis(B){
    const th = Math.atan2(B.stt, B.ct);
    return (Math.round(-th/(Math.PI/4)) % 8 + 8) % 8;
  }
  /* px = { col, dep, zbuf, W, H } — the very buffers the body rig's _paint already holds.
     Every feature is placed from a PROJECTED, ROUNDED anchor, so it is pixel-exact per facing. */
  function stamp(px, hc, b, lk, B, MATS0, cx, cy, m){
    m = m || 1;
    const wS = b.weight || 1, prof = skullProf(wS, m, b.jaw), W = px.W, H = px.H;
    const face = b.face || 'dash';
    const ex = EYE_X*wS*m;
    /* 3.1 SKIN-RELATIVE FACE PALETTE. The body rig hands over one fixed set of face materials (lip =
       skin step 0, chin = step 5, sclera = step 5) and they were wrong at both ends: step 0 on
       porcelain is a maroon blob bigger than the eyes, and on umber it is the skin. Every mark is now
       chosen against the skin's own value. dark = bronze and below. */
    const skinR = (MATS0.skin && MATS0.skin.ramp) || [];
    const lumOf=(hex)=>{ if(!hex||hex.length<7) return 1; const r=parseInt(hex.slice(1,3),16)/255, g=parseInt(hex.slice(3,5),16)/255, bb=parseInt(hex.slice(5,7),16)/255; return 0.2126*r+0.7152*g+0.0722*bb; };
    const dark = lumOf(skinR[3]) < 0.40;      // sRGB-weighted: umber .37, deep .34, ebony .31 | bronze .45
    const hairR = (MATS0.hair && MATS0.hair.ramp) || [];
    const darkOnDark = dark && Math.abs(lumOf(hairR[2]) - lumOf(skinR[3])) < 0.10;
    /* 3.2: on a YELLOW-hued ramp (olive: hue ~43°; every other skin sits under 32°) the lit steps read
       as bone, and a 3 px bar of it under the mouth paired with the two sclera pixels as teeth or a
       third eye (Cutter). The chin plane stays unlit there — the lathe shading carries the jaw. */
    const hueOf=(hex)=>{ if(!hex||hex.length<7) return 0; const r=parseInt(hex.slice(1,3),16), g=parseInt(hex.slice(3,5),16), bb=parseInt(hex.slice(5,7),16);
      const mx=Math.max(r,g,bb), mn=Math.min(r,g,bb); if(mx===mn) return 0;
      const h = mx===r ? (g-bb)/(mx-mn) : mx===g ? 2+(bb-r)/(mx-mn) : 4+(r-g)/(mx-mn); return (h*60+360)%360; };
    const boneSkin = !dark && hueOf(skinR[4]) > 38;
    const MATS = Object.assign({}, MATS0, {
      lip:    dark ? { ramp:['#243036'], off:0, idx:0 } : { ramp:skinR, off:0, idx:1 },
      chinL:  { ramp:skinR, off:0, idx: dark ? 5 : (boneSkin ? 3 : 4) },
      sclera: { ramp:['#cfd4cc'], off:0, idx:0 },       // fleet shirt ramp step 3: the one eye-white (step 4 read as startled)
      hairline:{ ramp:skinR, off:0, idx:5 } });
    const put=(x,y,mat,depth)=>{
      const M = MATS[mat]; if(!M) return;
      const c = M.ramp[Math.max(0,Math.min(M.ramp.length-1, M.idx==null?2:M.idx))];
      for(let dy=0;dy<q;dy++) for(let dx=0;dx<q;dx++){       // q x q block, so the mark scales
        const X=x+dx, Y=y+dy;
        if(X<0||X>=W||Y<0||Y>=H) continue;
        const i=Y*W+X; if(!px.col[i]) continue;              // never paint outside the silhouette
        px.col[i]=c; if(depth!=null) px.dep[i]=depth;
      }
    };
    // is this pixel bare skin? used to keep the brow OFF the hair on short foreheads
    const skinSet = {}; (MATS.skin.ramp||[]).forEach(c=>{ skinSet[c]=1; });
    const onSkin=(x,y)=>{ if(x<0||x>=W||y<0||y>=H) return false; return !!skinSet[px.col[y*W+x]]; };
    const hairSet = {}; hairR.forEach(c=>{ hairSet[c]=1; });
    const onHair=(x,y)=>{ if(x<0||x>=W||y<0||y>=H) return false; return !!hairSet[px.col[y*W+x]]; };
    /* 3.1 ONE ROUNDING PER HEAD. Every anchor used to round on its own, so as the head slid by a
       fraction of a pixel (walk sway is a half-pixel term) the eyes stepped a column while the
       hair did not: a sideways twitch inside the head on three frames of eight. The head centre is
       rounded once and every feature is an integer offset from it. */
    const hcP = projWith(hc, B, cx, cy);
    const hx0 = Math.round(hcP.sx-0.5), hy0 = Math.round(hcP.sy-0.5);
    /* rz: sub-pixel head yaw in the walk (about half a pixel at S) used to round to a one-column
       step on three frames of eight — a twitch inside a head whose silhouette had not moved. Offsets
       under three quarters of a pixel snap to zero; real offsets (the SE/SW face shift) round. */
    const rz=(v)=>Math.abs(v)<0.75 ? 0 : Math.round(v);
    const snap=(v)=>[hx0 + rz(v.sx-hcP.sx), hy0 + Math.round(v.sy-hcP.sy)];
    /* 3.2 DESPECKLE. An 18-segment lathe at ~10 px across hands one facet a normal that faces the key
       light and nothing around it does — a lone top-step pixel in a brim, a two-steps-darker pit in a
       clumped crown or a fringe. Cloth, hair and skin have no 1-px marks at this scale, so any pixel
       two or more ramp steps away from EVERY same-ramp pixel it touches (4-neighbourhood, three or
       more of them) is pulled to one step off the nearest: texture keeps its 1-step marks, islands
       go. Runs before the face marks are stamped, so the nose, lips and hairline are never touched;
       ink and sclera are not ramp colours and are never candidates. Same-ramp only: a skin pixel is
       judged against skin, never against the hair or hat beside it. */
    {
      const ramps = [MATS.skin && MATS.skin.ramp, hairR, (b.hat && b.hat!=='none' && MATS.hat) ? MATS.hat.ramp : null].filter(r=>r&&r.length);
      const R = 12*q, fixes = [];
      for(const rp of ramps){
        const idx = {}; rp.forEach((c,i)=>{ idx[c]=i; });
        for(let y=Math.max(0,hy0-R); y<Math.min(H,hy0+R); y++) for(let x=Math.max(0,hx0-R); x<Math.min(W,hx0+R); x++){
          const i=y*W+x, me=idx[px.col[i]]; if(me==null) continue;
          let n=0, mx=-1, mn=99;
          for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
            const X=x+dx, Y=y+dy; if(X<0||X>=W||Y<0||Y>=H) continue;
            const j=idx[px.col[Y*W+X]]; if(j==null) continue; n++; if(j>mx) mx=j; if(j<mn) mn=j;
          }
          if(n<3) continue;
          if(me>=mx+2) fixes.push([i, rp[mx+1]]);
          else if(me<=mn-2) fixes.push([i, rp[mn-1]]);
        }
      }
      for(const [i,c] of fixes) px.col[i]=c;
    }
    /* CONTACT SHADOW. A lit (top two steps) skin pixel touching hat on two or more sides is under
       the hat's edge and cannot be lit: the sou'wester's cape and band met around one top-step
       temple pixel at E/W, on the eye row where the brim shade is forbidden, and it read as a glint
       in the brim. It drops to the skin's mid step. Ink, sclera and lips are not skin and are never
       touched. */
    if(b.hat && b.hat!=='none' && MATS.hat && MATS.hat.ramp){
      const hidx = {}; MATS.hat.ramp.forEach((c,i)=>{ hidx[c]=i; });
      const R = 12*q, fixes = [];
      const sr = MATS.skin.ramp||[], sidx = {}; sr.forEach((c,i)=>{ sidx[c]=i; });
      for(let y=Math.max(0,hy0-R); y<Math.min(H,hy0+R); y++) for(let x=Math.max(0,hx0-R); x<Math.min(W,hx0+R); x++){
        const i=y*W+x, me=sidx[px.col[i]]; if(me==null || me<4) continue;
        let n=0; for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const X=x+dx, Y=y+dy; if(X<0||X>=W||Y<0||Y>=H) continue; if(hidx[px.col[Y*W+X]]!=null) n++; }
        if(n>=2) fixes.push([i, sr[3]]);
      }
      for(const [i,c] of fixes) px.col[i]=c;
    }
    /* Snap to the nearest pixel that is actually ON the head and not behind something. 3.1 adds a
       second pass: if nothing within a pixel passes the depth test, take the nearest SKIN pixel
       within two — a feature is never dropped. In the E/W profiles the anchor lands on the
       silhouette edge under a fringe, and the eye used to vanish for five frames of the walk. */
    const visible=(x0,y0,d)=>{
      for(const [dx,dy] of [[0,0],[-1,0],[1,0],[0,1],[0,-1],[-1,1],[1,1]]){
        const x=x0+dx, y=y0+dy;
        if(x<0||x>=W||y<0||y>=H) continue;
        const i=y*W+x; if(!px.col[i]) continue;
        if(px.zbuf[i] < d - 0.045) continue;        // something (hair, far side) is in front
        return [x,y];
      }
      for(const [dx,dy] of [[0,0],[-1,0],[1,0],[0,1],[-1,1],[1,1],[-2,0],[2,0],[0,-1],[-2,1],[2,1],[0,2],[-1,2],[1,2]]){
        if(onSkin(x0+dx, y0+dy)) return [x0+dx, y0+dy];
      }
      return null;
    };
    const gx = (lk.gaze&&lk.gaze[0])|0, gy = (lk.gaze&&lk.gaze[1])|0, lid = lk.lid||0;
    const dir = dirOfBasis(B), frontal = dir>=3 && dir<=5;

    /* 3.1 HAIRLINE. Black hair on a deep skin is one mass at every facing: the fringe edge and the
       forehead are within a few L* of each other. On the front facings the first skin row under the
       hair is lifted to the skin's top step across the face — the lit forehead a real hairline has. */
    if(darkOnDark && frontal){
      const ov0 = snap(projWith(surfaceAt(hc, prof, 0, EYE_Z*m, 0.010), B, cx, cy));
      for(let x=ov0[0]-4*q; x<=ov0[0]+4*q; x++){
        for(let y=ov0[1]-7*q; y<ov0[1]-q; y++){
          if(onSkin(x,y) && onHair(x,y-1)){ put(x, y, 'hairline', null); break; }
        }
      }
    }

    /* 3.3 PROFILE HAIRLINE (T2, the E / W / NE / NW half). Same builds, side facings: the temple and
       sideburn edge is where hair meets face, and on black-over-deep it is one mass, so the head is a
       blob with an eye. The plan said "hair +2 along the silhouette", but black's top step (L .09)
       is still level with deep skin's mid step (L .11): lightening the hair buys nothing. The SKIN
       side of the boundary is lifted instead, as at S — every skin pixel touching hair from beside
       or above, brow row to nose row, goes to the skin's top step (the lit temple). Rows below the
       nose are left alone so a beard edge is never lit, N is skipped (no face), and a lifted pixel
       with no lifted 4-neighbour is dropped so the gate's island rule never sees a lone pixel. */
    if(darkOnDark && !frontal && dir!==0){
      const ov0 = snap(projWith(surfaceAt(hc, prof, 0, EYE_Z*m, 0.010), B, cx, cy));
      const cand = [], set = {};
      for(let y=ov0[1]-4*q; y<=ov0[1]+2*q; y++) for(let x=hx0-8*q; x<=hx0+8*q; x++){
        if(onSkin(x,y) && (onHair(x-1,y) || onHair(x+1,y) || onHair(x,y-1))){ cand.push([x,y]); set[x+','+y]=1; }
      }
      for(const [x,y] of cand){
        if(set[(x-1)+','+y] || set[(x+1)+','+y] || set[x+','+(y-1)] || set[x+','+(y+1)]) put(x, y, 'hairline', null);
      }
    }

    /* --- eyes + brows: DELEGATED to Art/eyeIsoRig.js ---
       Placement used to be pure projection, and at ~10 px across that collapses: at E and W both
       eye anchors land on the same column and stack into one smudge, and at N they project
       face-forward and survive only because the z-buffer buries them. EyeIso replaces that with an
       authored socket per facing, hung off a projected face ORIGIN so the eyes still track head
       bob, lean and body rotation. It also owns the brow, the eye/brow shape vocabularies, and its
       own gaze/blink clock, so the eyes can be animated without involving the head at all. */
    const EI = root.EyeIso;
    const eyes = [];
    let op = null;
    if(EI){
      const ov = projWith(surfaceAt(hc, prof, 0, EYE_Z*m, 0.010), B, cx, cy);
      const os = snap(ov); op = visible(os[0], os[1], ov.d);
      if(op) for(const e of EI.draw(px, {x:op[0], y:op[1], d:ov.d}, dir, b, lk, MATS)) eyes.push(e);
    }

    // --- nose / mouth / chin, hung off the centre-front anchor so they track the facing ---
    const cf = (dz,out)=>projWith(surfaceAt(hc, prof, 0, dz*m, out), B, cx, cy);
    const at = (v)=>{ const s=snap(v); return visible(s[0], s[1], v.d); };
    const nv = cf(NOSE_Z, 0.006), np = at(nv);
    if(np) put(np[0], np[1]+q, 'skinD', nv.d);
    const bd = b.beard && b.beard!=='none' ? b.beard : null;
    let mp=null;
    /* 3.1: the mouth is drawn on EVERY beard. On goatee / full / long it lands on beard pixels and
       the ink lip is the parting in the beard — which is what a bearded mouth is. Only the depth
       test decides. */
    let msh = 0;
    {
      const mv = cf(MOUTH_Z, 0.006); mp = at(mv);
      const E = EXPR[lk.mouth] || EXPR[b.expr] || EXPR.neutral;
      /* 3.2 SE/SW: the pattern is centred on the projected FRONT anchor, and in a three-quarter view
         that anchor sits at the far edge of the visible face — the far pixel of every wide pattern
         fell off the silhouette or onto the far cheek's shade. The whole pattern steps one pixel
         toward the near cheek (the side the head centre is on). Constant per facing, so the 3.1
         frame-to-frame lock holds. */
      msh = (mp && (dir===3||dir===5)) ? (mp[0]<hx0 ? 1 : mp[0]>hx0 ? -1 : 0) : 0;
      // 3.3: only full and long cover the mouth row now, so only they take the ink parting
      const bearded = bd==='full' || bd==='long';
      if(mp) for(const [dx,dy,mat0] of E.m){
        let mat = mat0;
        if(mat==='skinL' && !dark) mat = 'skinD';   // 3.2: a tooth reads only against a dark lip; on light skins the opening is shadow
        const X = mp[0]+(dx+msh)*q, Y = mp[1]+q+dy*q;
        if(mat==='lip'){ put(X, Y, bearded ? 'lash2' : 'lip', mv.d); continue; }
        if(bd && mat0==='skinL') continue;           // no tooth in a beard
        if(onSkin(X, Y)) put(X, Y, mat, mv.d);       // the second value goes on bare skin only — never onto beard or hair
      }
    }
    // chin: a lit plane one row under the mouth — this is what makes the jaw read
    const kv = cf(CHIN_Z, 0.006), kp = at(kv);
    if(kp && !(bd && bd!=='moustache' && bd!=='mutton' && bd!=='chinstrap')){   // 3.3: the chinstrap leaves the chin front bare
      for(let dx=-1;dx<=1;dx++) put(kp[0]+dx*q, kp[1]+q, 'chinL', kv.d);
    }
    if(root.HeadIso && root.HeadIso._dbg) root.HeadIso._dbg.last = { dir, hx0, hy0, op, np, mp, kp, msh, mouth:lk.mouth, dark, darkOnDark };
    // (no cheek catchlight: at 8 px across it lands on the eye row and eats the inner lash pixel.
    //  The lathe shading already lifts the cheek plane, which is enough.)
    return eyes;
  }

  /* ---------------- standalone portrait render ---------------- */
  const GAIN=3.0, BIAS=2.7, EDGE=0.13, KEY='#101a19';
  const LN=(()=>{ const v=[-0.42,0.72,0.52], m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER=[[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  /* NINE SKINS, and the axis they vary on is UNDERTONE as much as value. Three ramps could only ever
     be light / mid / dark; a village needs cool-pale and warm-pale to be different people, and the
     two deepest ramps need different undertones (warm umber, cool ebony) or they read as one
     character in shadow. Every ramp is six steps so the shared SPAN table lands identically. */
  const SKINS={ porcelain:['#7a4a3e','#9a6353','#b8806c','#d29e88','#e6bda9','#f4dccb'],
                fair:     ['#6b4028','#8a5636','#a8724a','#c98d63','#e0a981','#f0c9a2'],
                rose:     ['#75402f','#96573f','#b47254','#d18f6d','#e7ae8d','#f6cfb4'],
                olive:    ['#4d3c22','#68522f','#85703f','#a28c55','#bda872','#d6c497'],
                tan:      ['#59331f','#754628','#936036','#b07d4a','#cc9a63','#e2b57e'],
                bronze:   ['#4a2a17','#63391f','#80502c','#9d6a3d','#b98653','#d1a271'],
                umber:    ['#3a2116','#4f2f1e','#68422a','#835838','#9e7049','#b98c60'],
                deep:     ['#301c12','#45291a','#5e3a24','#7a4f31','#966843','#b08055'],
                ebony:    ['#221a1a','#332727','#473838','#5d4b48','#75625c','#8e7a70'] };
  const HAIRS={ blond:['#7a5a1c','#946218','#b07d1f','#e0b13a','#f2cf6a'],
                sand: ['#6b5636','#877049','#a68d60','#c4ab7c','#ddc79c'],
                black:['#0e1114','#171b21','#232a32','#333c46','#4a545f'],
                brown:['#33271b','#473627','#5e4630','#6b4f35','#8a6a48'],
                auburn:['#3d1a14','#57271c','#732f21','#8f4029','#ab5738'],
                ginger:['#5e2013','#7d301c','#9c4327','#b85835','#d07048'],
                salt: ['#2a2f31','#40474a','#5c656a','#7d878b','#a3adaf'],
                grey: ['#6f7a78','#878b85','#a2a7a0','#bcc2ba','#cfd4cc'],
                white:['#8a908c','#a3a8a3','#bcc0ba','#d3d6cf','#dfe1db'] };
  const HATCOLS={ oil:['#946218','#b07d1f','#cf9d24','#e0b13a','#f2cf6a'],
                  navy:['#0e1526','#172644','#223764','#2f4c88','#4166ac'],
                  rust:['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c'],
                  teal:['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'],
                  char:['#14181b','#1f262a','#2c353b','#3d484f','#505d65'],
                  oat: ['#5c5140','#786a53','#948468','#b09e80','#c9b899'] };
  const EYES={ sea:'#2ba39a', sky:'#4166ac', bark:'#6b4f35', slate:'#8a969b', amber:'#e0b13a' };
  const BOOT=['#101317','#1d2127','#2b323a','#3d454e','#525c63'];
  const SHIRT=['#878b85','#a2a7a0','#bcc2ba','#cfd4cc','#dfe3dc','#eef0ea'];

  /* Materials used by BOTH the mesh and the stamp. `idx` pins the stamp's ramp step so a face
     mark is one fixed colour regardless of how the facet under it happened to shade.
     GAIN IS THE SPAN CONTROL (pass 4, shared with characterIsoRig3's SPAN table): at ~10 px across,
     a 10-segment lathe gives every pixel column its own normal, so a wide gain spreads one material
     over five ramp steps and the head reads as speckle. Narrowed until skin and hair each hold
     about three steps, with bias raised to keep the mean where it was. */
  function makeMats(b){
    const skin = Array.isArray(b.skin) ? b.skin : (SKINS[b.skin]||SKINS.fair);
    const hair = Array.isArray(b.hair) ? b.hair : (HAIRS[b.hair]||HAIRS.blond);
    const iris = EYES[b.eyes] || b.eyes || EYES.sea;
    const MATS = {
      // Dither is a STUBBLE texture, nothing else. On hair it read as scattered bald pixels; the
      // reference boards keep hair as solid colour blocks with clean shading boundaries.
      skin:{ramp:skin,off:0,gain:0.40,bias:3.74}, hair:{ramp:hair,off:0,gain:0.46,bias:2.92},
      beard:{ramp:hair,off:-1,gain:0.46,bias:2.92}, beardD:{ramp:hair,off:-2,gain:0.46,bias:2.92},
      stub:{ramp:skin,off:-1,dith:(S>=48?1:0),gain:0.40,bias:3.74},
      shirt:{ramp:SHIRT,off:0,gain:0.42,bias:3.12},
      hat:{ramp:(Array.isArray(b.hatCol)?b.hatCol:(HATCOLS[b.hatCol]||HATCOLS.char)),off:0,gain:0.46,bias:3.00},
      hatD:{ramp:(Array.isArray(b.hatCol)?b.hatCol:(HATCOLS[b.hatCol]||HATCOLS.char)),off:-1,gain:0.46,bias:3.00},
      // stamp-only materials (idx = fixed ramp step)
      lash:{ramp:INK,off:0,idx:0}, lash2:{ramp:INK,off:0,idx:1},
      // Brow ink: the hair's darkest step pulled a third of the way to eye ink. Straight hair
      // tone vanishes on blond, grey and white heads -- the brow must stay legible on every build,
      // while still reading as hair rather than as a second lash line.
      brow:{ramp:[_mix(hair[0], INK[0], 0.34)],off:0,idx:0}, iris:{ramp:[iris],off:0,idx:0},
      browHair:{ramp:hair,off:0,idx:0},   // fringe shadow: the row of hair directly over the eyes
      lip:{ramp:skin,off:0,idx:0},  skinD:{ramp:skin,off:0,idx:2},
      skinL:{ramp:skin,off:0,idx:5},
    };
    const hatR=(Array.isArray(b.hatCol)?b.hatCol:(HATCOLS[b.hatCol]||HATCOLS.char));
    const RINDEX={}; [skin,hair,SHIRT,BOOT,hatR].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
    return { MATS, RINDEX };
  }
  function camBasis(opts){
    const dir=opts.dir||0, th=-dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) };
  }
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  const _hex=(c)=>[parseInt(c.slice(1,3),16),parseInt(c.slice(3,5),16),parseInt(c.slice(5,7),16)];
  const _mix=(a,b,t)=>{ const A=_hex(a),B2=_hex(b);
    return '#'+[0,1,2].map(i=>Math.round(A[i]+(B2[i]-A[i])*t).toString(16).padStart(2,'0')).join(''); };
  const _kc={}; const keyTint=(c)=>_kc[c]||(_kc[c]=_mix(KEY,c,0.22));

  function paint(faces, opts, MATS, RINDEX, W, H, cx, cy, post){
    const B=camBasis(opts);
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    const pv=(x,y,z)=>{
      const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
      const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
      const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
      return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S - (B.heave||0), d:(yr*B.ce-zr*B.se) };
    };
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>pv(x,y,z));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=n[0]*LN[0]+(n[1]*B.se+n[2]*B.ce)*LN[1]+(-n[1]*B.ce+n[2]*B.se)*LN[2];
      const M=MATS[f.mat]||MATS.skin;
      const fidx=sh*GAIN*(M.gain==null?1:M.gain)+(M.bias==null?BIAS:M.bias)+(f.b||0);
      for(let t=1;t+1<rv.length;t++) tri(rv[0],rv[t],rv[t+1]);
      function tri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) return;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const qx=x+0.5, qy=y+0.5;
          const w0=((b.sx-qx)*(c.sy-qy)-(c.sx-qx)*(b.sy-qy))/area;
          const w1=((c.sx-qx)*(a.sy-qy)-(a.sx-qx)*(c.sy-qy))/area;
          const w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0), i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d;
            const base=Math.floor(fidx), fr=fidx-base;
            const idx=base+(M.dith?((fr>BAYER[x&3][y&3])?1:0):(fr>0.55?1:0))+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){            // depth-edge darkening
      const i=y*W+x; if(!col[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!col[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){
          const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
          if(e && e.i>0) col[far]=e.r[Math.max(0,e.i-1)];   // one step: see characterIsoRig3
        }
      }
    }
    if(post) post({col, dep, zbuf, W, H}, B);
    const out=col.slice();
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){            // tinted 1px keyline
      const i=y*W+x; if(out[i]) continue;
      let src=null;
      for(const [dx,dy] of [[0,-1],[1,0],[-1,0],[0,1]]){
        const nx=x+dx, ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&col[ny*W+nx]){ src=col[ny*W+nx]; break; }
      }
      if(src) out[i]=keyTint(src);
    }
    return out;
  }
  function toRGBA(out,W,H){
    const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){
      const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16);
      rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255;
    }
    return rgba;
  }
  const DEFAULT_BUILD = { skin:'fair', hair:'blond', hairStyle:'mop', beard:'none', eyes:'sea',
                          face:'dash', expr:'neutral', eyeShape:'round', browShape:'natural',
                          hat:'none', hatCol:'char', jaw:1, headSize:1, weight:1 };
  function render(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    const b = Object.assign({}, DEFAULT_BUILD, opts.build||{});
    const o = Object.assign({}, opts, {dir});
    const lk = look(Object.assign({expr:b.expr}, opts));
    const {MATS,RINDEX} = makeMats(b);
    const hc = [0,0,0], wS = b.weight||1, m = opts.headSize || b.headSize || 1;
    const F = [];
    if(opts.neck!==false){    // neck + collar stub, so a portrait is not a floating head
      for(const f of tube([0,-0.008,-0.330*m], [0,-0.006,-0.150*m], 0.052*wS*(b.neck||1), 0.044*wS*(b.neck||1), 'skin', -0.40, -0.05)) F.push(f);
      for(const f of lathe([0,0,-0.360*m], [[-0.060,0.135*wS,0.092],[-0.010,0.150*wS,0.100],[0.020,0.132*wS,0.090]], 'shirt', 0.10, 0, 10)) F.push(f);
    }
    for(const f of facesOf(hc, b, lk, wS, m)) F.push(f);
    const post = (px, B)=>stamp(px, hc, b, lk, B, MATS, pcx, pcy, m);
    return toRGBA(paint(F, o, MATS, RINDEX, PW, PH, pcx, pcy, post), PW, PH);
  }
  function anchors(dir, opts){
    opts = opts||{};
    const b = Object.assign({}, DEFAULT_BUILD, opts.build||{});
    const m = opts.headSize || b.headSize || 1;
    const B = camBasis(Object.assign({},opts,{dir})), prof = skullProf(b.weight||1, m, b.jaw);
    const e=(sgn)=>{ const v=projWith(surfaceAt([0,0,0],prof,sgn*EYE_X*(b.weight||1)*m,EYE_Z*m,0.006),B,pcx,pcy);
      return {x:v.sx, y:v.sy}; };
    return { eyeL:e(-1), eyeR:e(1),
             crown:(()=>{ const v=projWith([0,0,0.174*m],B,pcx,pcy); return {x:v.sx,y:v.sy}; })() };
  }

  const API = { get PW(){return PW;}, get PH(){return PH;}, PX:32,
    get pivot(){ return {x:pcx,y:pcy}; }, get scale(){ return S; }, setScale, setPortraitScale,
    defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    EYE_Z, EYE_X, NOSE_Z, MOUTH_Z, CHIN_Z, HAIRSTYLES, BEARDS, HATS, FACES, EXPRS, EXPR,
    get EYESHAPES(){ return (root.EyeIso||{}).EYESHAPES || EYESHAPES; },
    get BROWSHAPES(){ return (root.EyeIso||{}).BROWSHAPES || BROWSHAPES; },
    get BROWS(){ return (root.EyeIso||{}).BROWS || BROWS; },
    get eyes(){ return root.EyeIso || null; },
    SKINS, HAIRS, HATCOLS, EYES, KEY,
    skullProf, profR, facesOf, stamp, look, freeLook, loopLook, blinks, makeMats, render, anchors,
    hatFaces, capLathe, sweep, shell, scaleOpt, BEARD, HAIR, HAT,
    zRows, hEyeRow, hAt, rowsAbove, bandZOf, PXM, ROW,
    GAIN, BIAS, LN, BAYER, pass:7, rev:'3.3' };
  root.HeadIso3 = API;
  root.HeadIso2 = API;
  root.HeadIso  = API;   // pass 7 is a superset: every pass-3/6 call signature still resolves
})(typeof globalThis!=='undefined'?globalThis:window);

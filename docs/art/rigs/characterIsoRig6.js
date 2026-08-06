/* Hidden Harbours — ISO CHARACTER rig, PASS 6. Pass 5's body, one honest rig set.
   ---------------------------------------------------------------------------------------------
   WHY PASS 6. Pass 5 is unchanged below the neck: every curve, every garment zone, every
   constant is byte-identical, and this file is a drop-in for characterIsoRig5.js. Four things
   were wrong ABOVE the rig — in what the rig SAID about itself — and every one of them made a
   consumer guess:
     1. HATS was the pass-5 list of four. The hat table has lived in the head rig since pass 6 of
        the head (six hats, incl. flat cap and oilskin hood), so the rig was advertising a list it
        no longer owned. HATS now reads through to the head rig and falls back only if none is
        loaded.
     2. Three CAST presets could not wear the two new hats, so the character page re-dressed them
        in the page. A preset that only exists in a page is not a preset. Ginny wears the hood, the
        deck boss the flat cap, the wharf boy the watch cap — in the rig, where a baker can see them.
     3. carry() reported mode 'buckets' for the helm and oar stances, so a consumer could not tell
        a pair of pails from a wheel, and neither piloting stance had the two-hand pin it needs.
        Same anchors, honest mode, plus mid.
     4. Which prop a given animation drives was written down in each consumer page and nowhere
        else. ANIM_MOUNT and CARRIES state it once: dig drives the spade, the eight fishing anims
        drive the rod, the three locomotion anims and the two deck anims are free to carry.
   Nothing else moved. tool(), anchors(), carry(), pose(), render(), the deck-rock transform and
   every table below are pass 5.

   ---------------------------------------------------------------------------------------------
   PASS 5 (all still true). Sex, age, and clothing that reads.
   ---------------------------------------------------------------------------------------------
   WHY PASS 5. Pass 4 had one body and one outfit, and review said the outfit didn't land: "the
   overalls don't read as overalls." It was right, and the reason is worth writing down, because it
   is the same reason a garment fails at any low resolution.

   PASS 4's TEAL WRAPPED THE WHOLE TORSO AT CHEST HEIGHT. A closed ring of overall-colour from the
   armpits down, with a light yoke above it, is not a pair of bib overalls — it is a BODICE. Every
   silhouette cue that says "overalls" was missing:
     · a bib is NARROWER than the chest, so the shirt shows either side of it — that flank of light
       colour is the single load-bearing cue, and pass 4 had none of it;
     · the overall body only wraps the whole torso AT AND BELOW THE WAIST;
     · two straps rise from the bib's top corners and go over the shoulder;
     · there is a hard metal glint where each strap meets the bib.
   Pass 5 rebuilds the torso as ZONES instead of one stack of rings: a full-wrap lathe below the
   waist, a light shirt lathe above it, and a FRONT ARC (new arcLathe primitive) for the bib, which
   shades like cloth on a body instead of like a plate bolted to it. At 32 px/m that buys 3 rows of
   yoke, 1 row of dark bib hem, 2 rows of bib with ~2 px of shirt showing each side, 2 rows of
   waistband, two 1 px strap columns and two 1 px brass buckles. It reads.

   Once the torso is zones, one garment is as cheap as seven, so GARMENTS is a table:
     overalls · oilskins · knit jumper · work shirt · gutting apron · quilted vest · skirt + shawl
   plus hats, which live in the head rig (watchcap, sou'wester, ballcap, kerchief).

   SEX. Not a costume switch — a skeleton one. Shoulder, chest, waist and hip rings each carry their
   own multiplier, so the male silhouette is a wide shelf tapering to narrow hips and the female one
   inverts that; neck and jaw width, limb radius and height follow. At 10 px across, the WAIST-TO-HIP
   difference is what the eye actually reads, so that is where the biggest numbers are.

   AGE. child · youth · adult · elder, and the lever that matters is HEAD-TO-BODY RATIO, not height.
   The skeleton is therefore built from the ground UP — ankle, leg, torso, collar, neck, head — with
   separate scales for limb length, torso length and head size, so a child is 3.1 heads tall and an
   adult 3.8 without either one being a squashed copy of the other. Elders get a stoop: a real pitch
   of the upper body about a point below the ribs, which shortens them on screen more than their
   height scale does.

   FROZEN: toolCurve() is copied verbatim from pass 1 — every animation curve, phase boundary and
   carry/rock override is bit-identical. pose() keeps its output shape (legs/arms/neckB/headC/tool),
   so anchors() / tool() / carry() / projectLocal() still mount the rod, bucket, tray, helm, oar and
   haul layers, and for an adult male every joint resolves to the same number as pass 4 except the
   head, which rises 0.07 m — pass 4's chin (1.028) sat BELOW its own collar (1.037), i.e. there was
   no neck at all. There is now ~1.5 px of one.

   Needs Art/headIsoRig3.js (six hats solved in rows above the eye) and Art/eyeIsoRig.js.
   Exposes globalThis.CharacterIso6 (and CharacterIso5 / CharacterIso if those are free). */
(function (root) {
  const PX = 32;
  let S = 32, W = 64, H = 92, cx = 32, cy = 82;
  function setScale(px){
    px = px || PX; const k = px/PX;
    S = px; W = Math.round(64*k); H = Math.round(92*k); cx = Math.round(32*k); cy = Math.round(82*k);
    return k;
  }
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;

  /* ---- palettes, dark->light (KTC master ramps shared with the fleet) ----
     NINE SKINS. Three could only be light / mid / dark; a village needs cool-pale and warm-pale to
     be different people, and the two deepest need different undertones or they read as one
     character standing in shadow. Undertone varies as much as value. */
  const SKINS = {
    porcelain:['#7a4a3e','#9a6353','#b8806c','#d29e88','#e6bda9','#f4dccb'],
    fair:     ['#6b4028','#8a5636','#a8724a','#c98d63','#e0a981','#f0c9a2'],
    rose:     ['#75402f','#96573f','#b47254','#d18f6d','#e7ae8d','#f6cfb4'],
    olive:    ['#4d3c22','#68522f','#85703f','#a28c55','#bda872','#d6c497'],
    tan:      ['#59331f','#754628','#936036','#b07d4a','#cc9a63','#e2b57e'],
    bronze:   ['#4a2a17','#63391f','#80502c','#9d6a3d','#b98653','#d1a271'],
    umber:    ['#3a2116','#4f2f1e','#68422a','#835838','#9e7049','#b98c60'],
    deep:     ['#301c12','#45291a','#5e3a24','#7a4f31','#966843','#b08055'],
    ebony:    ['#221a1a','#332727','#473838','#5d4b48','#75625c','#8e7a70'],
  };
  const HAIRS = {
    blond: ['#7a5a1c','#946218','#b07d1f','#e0b13a','#f2cf6a'],
    sand:  ['#6b5636','#877049','#a68d60','#c4ab7c','#ddc79c'],
    black: ['#0e1114','#171b21','#232a32','#333c46','#4a545f'],
    brown: ['#33271b','#473627','#5e4630','#6b4f35','#8a6a48'],
    auburn:['#3d1a14','#57271c','#732f21','#8f4029','#ab5738'],
    ginger:['#5e2013','#7d301c','#9c4327','#b85835','#d07048'],
    salt:  ['#2a2f31','#40474a','#5c656a','#7d878b','#a3adaf'],
    grey:  ['#6f7a78','#878b85','#a2a7a0','#bcc2ba','#cfd4cc'],
    white: ['#8a908c','#a3a8a3','#bcc0ba','#d3d6cf','#e8eae3'],
  };
  const OUTFITS = {
    teal: ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'],
    navy: ['#0e1526','#172644','#223764','#2f4c88','#4166ac'],
    oil:  ['#946218','#b07d1f','#cf9d24','#e0b13a','#f2cf6a'],
    rust: ['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c'],
    slate:['#1b2429','#28353c','#374852','#485c68','#5c7280'],
    moss: ['#20291f','#2e3a2a','#3e4d37','#4f6045','#627457'],
  };
  const SHIRTS = {
    white: ['#878b85','#a2a7a0','#bcc2ba','#cfd4cc','#dfe3dc','#eef0ea'],
    cream: ['#8c6a45','#a98352','#c2a06b','#d8c290','#ead9ae','#f4e8c8'],
    navy:  ['#0e1526','#172644','#223764','#2f4c88','#4166ac','#5a80c2'],
    red:   ['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c','#ee7a55'],
    moss:  ['#27312c','#333f37','#414e43','#505e50','#616f60','#748172'],
    slate: ['#20282c','#2d383e','#3d4a52','#4e5e68','#61737e','#7a8b96'],
    ochre: ['#5e4416','#7b5a20','#96712c','#b18b3d','#c8a555','#dcc077'],
  };
  const HATCOLS = {
    oil:  ['#946218','#b07d1f','#cf9d24','#e0b13a','#f2cf6a'],
    navy: ['#0e1526','#172644','#223764','#2f4c88','#4166ac'],
    rust: ['#4a100e','#7c1a15','#a8241b','#cf3626','#e2573c'],
    teal: ['#0d3f3c','#14554e','#1c7367','#2ba39a','#49b8aa'],
    char: ['#14181b','#1f262a','#2c353b','#3d484f','#505d65'],
    oat:  ['#5c5140','#786a53','#948468','#b09e80','#c9b899'],
  };
  const APRONS = {
    canvas:['#4d4436','#665b48','#7f735c','#988b71','#b0a488'],
    rubber:['#2b2f33','#3b4147','#4c545b','#606a72','#78838c'],
    ochre: ['#6b4d16','#8a6520','#a87f2b','#c39a3f','#d9b45c'],
  };
  const SHIRT   = SHIRTS.white;
  const BOOT    = ['#101317','#1d2127','#2b323a','#3d454e','#525c63'];
  const LEATHER = ['#241611','#38221a','#4d2f22','#63402d','#7c553c'];
  const BRASS   = ['#5a4318','#7d6024','#a68333','#c9a548','#e3c46a'];
  const EYES  = { sea:'#2ba39a', sky:'#4166ac', bark:'#6b4f35', slate:'#8a969b', amber:'#e0b13a' };
  const KEY   = '#101a19';
  const INK   = ['#12181b','#243036'];
  const HAIRSTYLES = ['crop','mop','bob','long','bun','ponytail','buzz','bald'];
  const BEARDS     = ['none','stubble','moustache','chinstrap','goatee','mutton','full','long'];
  const HATS_LOCAL = ['none','watchcap','souwester','ballcap','kerchief'];
  // pass 6: the hat table belongs to the head rig — ask it, never re-state it here.
  function hatList(){ const H = root.HeadIso; return (H && H.HATS && H.HATS.length) ? H.HATS : HATS_LOCAL; }
  const FACES = ['dash'];
  const SEXES = ['m','f'];
  const AGE_ORDER = ['child','youth','adult','elder'];

  /* ---------------- SEX: a skeleton, not a costume ----------------
     At ~10 px of torso width the eye reads two things: the SHOULDER-TO-HIP relationship, and
     whether there is a waist between them. Everything else (jaw, neck, limb radius) is a second
     order confirmation, which is why the waist and hip numbers are the biggest in this table. */
  const SEX = {
    m: { shoulder:1.06, chest:1.04, waist:1.01, hip:0.95, neck:1.10, jaw:1.12, limbR:1.05,
         height:1.02, bust:0 },
    f: { shoulder:0.91, chest:0.95, waist:0.84, hip:1.11, neck:0.89, jaw:0.89, limbR:0.94,
         height:0.955, bust:0.021 },
  };
  /* ---------------- AGE: head-to-body ratio does the work ----------------
     head  = skull size (a child's head is nearly adult-sized; that IS the childhood cue)
     leg   = limb LENGTH scale, applied on top of height — children are torso-heavy
     limbR = limb RADIUS scale
     stoop = degrees of upper-body pitch about a point below the ribs (elders only) */
  const AGES = {
    child: { height:0.72,  head:0.90, leg:0.86, torso:1.03, limbR:0.86, shoulder:0.82, neckLen:0.46, stoop:0, weight:0.90 },
    youth: { height:0.875, head:0.97, leg:1.00, torso:0.97, limbR:0.90, shoulder:0.90, neckLen:0.88, stoop:0, weight:0.93 },
    adult: { height:1.00,  head:1.00, leg:1.00, torso:1.00, limbR:1.00, shoulder:1.00, neckLen:1.00, stoop:0, weight:1.00 },
    elder: { height:0.955, head:1.00, leg:0.97, torso:0.98, limbR:0.93, shoulder:0.95, neckLen:0.78, stoop:9, weight:0.99 },
  };
  function propsOf(b){
    const sx = SEX[b.sex] || SEX.m, ag = AGES[b.age] || AGES.adult;
    const kid = b.age==='child', old = b.age==='elder';
    return {
      hS: (b.height||1) * sx.height * ag.height,
      wS: (b.weight||1) * ag.weight,
      headK: (b.headSize||1) * ag.head,
      legK: ag.leg, torsoK: ag.torso, limbR: ag.limbR * sx.limbR,
      shoulderK: sx.shoulder * ag.shoulder,
      chestK: sx.chest, waistK: kid ? 1.0 : sx.waist, hipK: kid ? 0.98 : sx.hip,
      neckK: sx.neck * (kid ? 0.90 : 1),
      neckLen: ag.neckLen,
      jawK: sx.jaw * (kid ? 0.92 : old ? 1.05 : 1),
      // no bust on a child, reduced on a youth; it is ~0.5 px of fore-aft, plus a real
      // silhouette bump in the E and W profiles, which is all this scale can carry
      bust: (b.sex==='f' && !kid) ? sx.bust*(b.age==='youth'?0.62:1) : 0,
      stoop: ag.stoop * (b.stoop==null ? 1 : b.stoop),
    };
  }

  /* ---------------- GARMENTS ----------------
     All z are dz in TORSO-LOCAL space (0 = torso centre, +0.182 = collar, -0.155 = torso bottom),
     unscaled — the builder multiplies them by hS*torsoK so a child's overalls fit a child.
     bibTop  : top of the bib panel; null = no bib
     bibArc  : half-arc of the front bib in radians (0.70 rad -> x = +-0.64 of the chest radius,
               which is what leaves ~2 px of shirt showing either side. THIS IS THE CUE.)
     wrapTop : top of the FULL-WRAP outer garment; null = no outer wrap (shirt to the waist)
     sleeve  : short | longShirt | longOver
     collar  : crew | stand | roll | open
     bootZ   : boot shaft top, as a fraction of hS */
  const GARMENTS = {
    overalls:  { label:'BIB OVERALLS', short:'OVERALLS',   bibTop: 0.076, bibArc:0.50, wrapTop:-0.100, straps:1, buckles:1,
                 sleeve:'short',     collar:'crew',  bootZ:0.150, backTop:-0.010 },
    oilskins:  { label:'OILSKINS', short:'OILSKINS',       bibTop: 0.124, bibArc:0.58, wrapTop:-0.092, straps:1, buckles:0,
                 sleeve:'longOver',  collar:'stand', bootZ:0.215, backTop: 0.030, cuffOver:1 },
    sweater:   { label:'KNIT JUMPER', short:'JUMPER',    bibTop: null,  wrapTop:-0.078, sleeve:'longShirtFull',
                 collar:'roll',      bootZ:0.130, waistBand:1, cuff:1 },
    workshirt: { label:'WORK SHIRT', short:'SHIRT',     bibTop: null,  wrapTop:-0.112, sleeve:'longShirt',
                 collar:'open',      bootZ:0.100, belt:1, cuff:1 },
    apron:     { label:'GUTTING APRON', short:'APRON',  bibTop: null,  wrapTop:-0.112, sleeve:'short',
                 collar:'crew',      bootZ:0.170, belt:1, apron:1 },
    vest:      { label:'QUILTED VEST', short:'VEST',   bibTop: null,  wrapTop:-0.112, sleeve:'longShirt',
                 collar:'stand',     bootZ:0.105, belt:1, vest:1, cuff:1 },
    skirt:     { label:'SKIRT + SHAWL', short:'SKIRT',  bibTop: null,  wrapTop:null,   sleeve:'longShirtFull',
                 collar:'crew',      bootZ:0.075, skirtHem:1, shawl:1, waistBand:1 },
  };
  const GARMENT_ORDER = ['overalls','oilskins','sweater','workshirt','apron','vest','skirt'];

  /* ---------------- CAST ----------------
     Ten builds that exist to prove the axes are separable: read down the list and no two share a
     sex, age and garment triple. These are the presets the sheet-baker names. */
  const BUILDS = {
    fisher:   { label:'Fisher',    sex:'m', age:'adult', garment:'overalls',  skin:'fair',      hair:'blond', hairStyle:'mop',      beard:'stubble',  eyes:'sea',   outfit:'teal',  shirt:'white', hat:'none',      hatCol:'char' },
    skipper:  { label:'Skipper',   sex:'m', age:'elder', garment:'oilskins',  skin:'tan',       hair:'salt',  hairStyle:'crop',     beard:'full',     eyes:'slate', outfit:'oil',   shirt:'navy',  hat:'souwester', hatCol:'oil'  },
    ginny:    { label:'Ginny',     sex:'f', age:'adult', garment:'overalls',  skin:'rose',      hair:'ginger',hairStyle:'bob',      beard:'none',     eyes:'amber', outfit:'rust',  shirt:'moss',  hat:'hood',      hatCol:'rust' },
    nan:      { label:'Nan',       sex:'f', age:'elder', garment:'skirt',     skin:'porcelain', hair:'white', hairStyle:'bun',      beard:'none',     eyes:'sky',   outfit:'slate', shirt:'cream', hat:'kerchief',  hatCol:'rust' },
    deckboss: { label:'Deck boss', sex:'m', age:'adult', garment:'vest',      skin:'deep',      hair:'black', hairStyle:'buzz',     beard:'mutton',   eyes:'bark',  outfit:'navy',  shirt:'slate', hat:'flatcap',   hatCol:'char' },
    packer:   { label:'Packer',    sex:'f', age:'adult', garment:'apron',     skin:'umber',     hair:'black', hairStyle:'bun',      beard:'none',     eyes:'bark',  outfit:'slate', shirt:'white', hat:'kerchief',  hatCol:'teal', apronCol:'canvas' },
    cutter:   { label:'Cutter',    sex:'f', age:'youth', garment:'apron',     skin:'olive',     hair:'auburn',hairStyle:'ponytail', beard:'none',     eyes:'bark',  outfit:'moss',  shirt:'ochre', hat:'none',      hatCol:'oat',  apronCol:'rubber' },
    hand:     { label:'Deckhand',  sex:'m', age:'youth', garment:'workshirt', skin:'bronze',    hair:'brown', hairStyle:'crop',     beard:'none',     eyes:'bark',  outfit:'slate', shirt:'ochre', hat:'ballcap',   hatCol:'navy' },
    boy:      { label:'Wharf boy', sex:'m', age:'child', garment:'sweater',   skin:'fair',      hair:'brown', hairStyle:'mop',      beard:'none',     eyes:'sky',   outfit:'navy',  shirt:'red',   hat:'watchcap',  hatCol:'navy' },
    girl:     { label:'Wharf girl',sex:'f', age:'child', garment:'workshirt', skin:'ebony',     hair:'black', hairStyle:'long',     beard:'none',     eyes:'bark',  outfit:'moss',  shirt:'cream', hat:'none',      hatCol:'oat'  },
  };
  const CAST = ['fisher','ginny','skipper','nan','deckboss','packer','cutter','hand','boy','girl'];

  const ANIMS = { idle:{frames:6, ms:170}, walk:{frames:8, ms:110}, run:{frames:6, ms:80},
                  hold:{frames:6, ms:170}, cast:{frames:10, ms:70}, dig:{frames:10, ms:90},
                  balance:{frames:8, ms:150}, stagger:{frames:10, ms:90},
                  bite:{frames:6, ms:150}, strike:{frames:6, ms:80},
                  reel:{frames:12, ms:90}, land:{frames:12, ms:100},
                  castBack:{frames:6, ms:90}, castRelease:{frames:8, ms:70},
                  /* 6.1 — append-only. board/boardDown read opts.railZ (world metres). */
                  board:{frames:10, ms:90, oneShot:true}, boardDown:{frames:6, ms:95, oneShot:true},
                  haul:{frames:8, ms:120},
                  /* 6.2 — append-only. ladderDown reads opts.rung / opts.ladderW (world metres,
                     defaulting to the wharf rig's own FIT.ladder) and LOOPS. */
                  ladderDown:{frames:10, ms:110} };
  const GROUPS = { base:['idle','walk','run'], balance:['balance','stagger'],
                   fishing:['hold','cast','castBack','castRelease','bite','strike','reel','land'],
                   boarding:['board','boardDown','ladderDown'], work:['dig','haul'] };
  const CAST_W1 = 0.34, CAST_S1 = 0.50;

  /* ---------------- THE MOUNT CONTRACT (pass 6) ----------------
     Which layer an animation drives, and which call returns its pin. Written here so a baker or
     an engine does not have to read a preview page to find out.
       'rod'    -> tool(dir,opts) -> {grip, wrist, pitch, yaw, bend}   pin RodIso.pivot to grip
       'shovel' -> tool(dir,opts) -> same shape                        pin ShovelIso.pivot to grip
       'free'   -> no tool; opts.carry may be set, and carry(dir,opts) returns the hand pins.
     Prop layers draw UNDER the sprite for the away facings — each prop rig states its own
     `behind` list (rod and spade: NW, N, NE). */
  const ANIM_MOUNT = {
    idle:'free', walk:'free', run:'free', balance:'free', stagger:'free',
    hold:'rod', cast:'rod', castBack:'rod', castRelease:'rod',
    bite:'rod', strike:'rod', reel:'rod', land:'rod', dig:'shovel',
    board:'free', boardDown:'free', haul:'free',
    /* 'ladder' is neither free nor a tool: both hands are committed to the rungs, so no carry
       stance may ride this clip and no prop layer mounts on it. */
    ladderDown:'ladder' };
  /* The four carry stances. anims = the animations each one may ride (a tool anim always wins:
     pose() ignores carry when a tool curve is active). */
  const CARRIES = {
    buckets:{ label:'PAILS',  layer:'BucketIso (tier 1/2)', pin:'handL + handR', anims:['idle','walk','run','board','boardDown'] },
    tray:   { label:'TRAY',   layer:'BucketIso (tier 3)',   pin:'mid',           anims:['idle','walk','run','board','boardDown'] },
    helm:   { label:'HELM',   layer:'wheel / tiller',       pin:'mid + handL/handR', anims:['idle','walk'] },
    oars:   { label:'OARS',   layer:'DoryIso / PuntIso oars', pin:'handL + handR', anims:['idle','walk'] },
  };
  const CARRY_ORDER = ['buckets','tray','helm','oars'];
  function rampOf(map, key, fb){ return Array.isArray(key) ? key : (map[key] || map[fb]); }

  /* VALUE SPANS. gain is the SPAN control: at ~10 px across, an 8-segment lathe gives every pixel
     column its own normal, so a wide gain spreads one material over five ramp steps and the garment
     reads as speckle instead of cloth. Each material holds ~3 steps. */
  const SPAN = {
    skin:  [0.40, 3.74],
    hair:  [0.46, 2.92],
    over:  [0.46, 3.00],
    shirt: [0.42, 3.12],
    boot:  [0.52, 2.76],
    hard:  [0.60, 2.60],   // leather, brass, rubber: allowed more span, they are meant to glint
  };
  function makeMats(b){
    const skin = rampOf(SKINS, b.skin, 'fair'), hair = rampOf(HAIRS, b.hair, 'blond'),
          over = rampOf(OUTFITS, b.outfit, 'teal'), shirt = rampOf(SHIRTS, b.shirt, 'white'),
          hatC = rampOf(HATCOLS, b.hatCol, 'char'), apron = rampOf(APRONS, b.apronCol, 'canvas');
    const eye = EYES[b.eyes] || b.eyes || EYES.sea;
    const M=(ramp,key,off,dBias)=>({ramp, off:off||0, gain:SPAN[key][0], bias:SPAN[key][1]+(dBias||0)});
    const MATS = { skin:M(skin,'skin'), hair:M(hair,'hair'), over:M(over,'over'),
                   shirt:M(shirt,'shirt'), boot:M(BOOT,'boot'), eye:{ramp:[eye],off:0},
                   lash:{ramp:INK,off:0,idx:0}, lash2:{ramp:INK,off:0,idx:1},
                   faceLit:M(skin,'skin',1), socket:M(skin,'skin',-2),
                   sleeve:M(shirt,'shirt',0,-0.18), collar:M(shirt,'shirt',1),
                   hairD:M(hair,'hair',-1), overD:M(over,'over',-1), overL:M(over,'over',1),
                   shirtD:M(shirt,'shirt',-1), shirtL:M(shirt,'shirt',1),
                   hat:M(hatC,'over'), hatD:M(hatC,'over',-1),
                   apron:M(apron,'over'), apronD:M(apron,'over',-1),
                   belt:M(LEATHER,'hard'), beltD:M(LEATHER,'hard',-1),
                   brass:M(BRASS,'hard',1), brassD:M(BRASS,'hard'),
                   bootL:M(BOOT,'boot',1),
                   lip:{ramp:skin,off:0,idx:0}, sole:M(BOOT,'boot',-2),
                   brow:{ramp:[_mix(hair[0], INK[0], 0.34)],off:0,idx:0},
                   browHair:{ramp:hair,off:0,idx:0},
                   iris:{ramp:[eye],off:0,idx:0}, skinD:{ramp:skin,off:0,idx:2},
                   skinL:{ramp:skin,off:0,idx:5},
                   beard:M(hair,'hair',-1), beardD:M(hair,'hair',-2),
                   stub:Object.assign(M(skin,'skin',-1),{dith:(S>=48?1:0)}) };
    const RINDEX = {};
    [skin,hair,over,shirt,BOOT,hatC,apron,LEATHER,BRASS].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
    return { MATS, RINDEX };
  }

  // ---- shading constants (fleet recipe) ----
  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.13;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- transforms / vector maths ----
  const ID=(p)=>p;
  const TX=(dx,dy,dz)=>(p)=>[p[0]+dx,p[1]+dy,p[2]+dz];
  const rotZ=(a)=>{ const c=Math.cos(a), s=Math.sin(a); return (p)=>[p[0]*c-p[1]*s, p[0]*s+p[1]*c, p[2]]; };
  const pitchX=(a,z0)=>{ const c=Math.cos(a), s=Math.sin(a); return (p)=>[p[0], p[1]*c+(p[2]-z0)*s, z0-p[1]*s+(p[2]-z0)*c]; };
  const rollY=(a,z0)=>{ const c=Math.cos(a), s=Math.sin(a); return (p)=>[p[0]*c+(p[2]-z0)*s, p[1], z0-p[0]*s+(p[2]-z0)*c]; };
  const chain=(...fs)=>(p)=>{ for(let i=fs.length-1;i>=0;i--) p=fs[i](p); return p; };
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{ const m=Math.hypot(a[0],a[1],a[2])||1; return [a[0]/m,a[1]/m,a[2]/m]; };
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
  function lathe(c, prof, mat, b, db, xf, seg){
    xf = xf || ID; seg = seg || 8;
    const cs=[], sn=[];
    for(let k=0;k<seg;k++){ const a=(k+0.5)/seg*Math.PI*2; cs.push(Math.cos(a)); sn.push(Math.sin(a)); }
    const rings = prof.map(([dz,rx,ry])=>{
      const o=[]; for(let k=0;k<seg;k++) o.push(xf([c[0]+cs[k]*rx, c[1]+sn[k]*ry, c[2]+dz]));
      return o;
    });
    const out=[];
    for(let i=0;i+1<rings.length;i++){
      const r0=rings[i], r1=rings[i+1];
      for(let k=0;k<seg;k++){ const k2=(k+1)%seg; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:db||0}); }
    }
    out.push({v:rings[rings.length-1].slice(),mat,b:b||0,db:db||0});
    out.push({v:rings[0].slice().reverse(),mat,b:b||0,db:db||0});
    return out;
  }
  /* ARC LATHE — the pass-5 primitive, and the reason the bib reads.
     A garment panel laid on the body must be part of the BODY's cylinder, not a flat plate bolted to
     it: a flat plate shades as one value and its silhouette edge is a hard step, while an arc of the
     torso lathe picks up the same round shading the shirt has and its side edges land on clean pixel
     columns. a0..a1 are angles in the lathe's own convention (x = cos a, y = sin a), so the FRONT of
     the body is a = PI/2 and the back is -PI/2. */
  function arcLathe(c, prof, mat, b, db, xf, a0, a1, n){
    xf = xf||ID; n = n||5;
    const rings = prof.map(([dz,rx,ry])=>{
      const o=[];
      for(let k=0;k<=n;k++){ const a = a0 + (a1-a0)*k/n;
        o.push(xf([c[0]+Math.cos(a)*rx, c[1]+Math.sin(a)*ry, c[2]+dz])); }
      return o;
    });
    const out=[];
    for(let i=0;i+1<rings.length;i++){ const r0=rings[i], r1=rings[i+1];
      for(let k=0;k<n;k++) out.push({v:[r0[k],r0[k+1],r1[k+1],r1[k]],mat,b:b||0,db:db||0}); }
    return out;
  }
  function profR(prof, dz, which){
    if(dz<=prof[0][0]) return prof[0][which];
    for(let i=0;i+1<prof.length;i++){
      const a=prof[i], b2=prof[i+1];
      if(dz<=b2[0]){ const t=(dz-a[0])/((b2[0]-a[0])||1); return a[which]+(b2[which]-a[which])*t; }
    }
    return prof[prof.length-1][which];
  }
  function limb(A,B2,r0,r1,mat,b,seg,dbv){
    seg = seg || 6; dbv = (dbv==null? -0.05 : dbv);
    const ax=v_norm(v_sub(B2,A)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const rr=v_norm(v_cross(ax,up)), uu=v_cross(ax,rr);
    const ring=(P,rad)=>{ const o=[];
      for(let k=0;k<seg;k++){ const a=(k+0.5)/seg*Math.PI*2;
        o.push(v_add(P, v_add(v_mul(rr,Math.cos(a)*rad), v_mul(uu,Math.sin(a)*rad)))); }
      return o; };
    const a0=ring(A,r0), a1=ring(B2,r1), out=[];
    for(let k=0;k<seg;k++){ const k2=(k+1)%seg; out.push({v:[a0[k],a0[k2],a1[k2],a1[k]],mat,b:b||0,db:dbv}); }
    out.push({v:a1.slice(),mat,b:b||0,db:dbv});
    out.push({v:a0.slice().reverse(),mat,b:b||0,db:dbv});
    return out;
  }
  const ball=(c,r,mat,b,db,xf)=>lathe(c, [[-r,r*0.42,r*0.42],[-r*0.62,r*0.80,r*0.78],[0,r,r*0.96],[r*0.62,r*0.80,r*0.78],[r,r*0.42,r*0.42]], mat, b, db, xf, 6);

  function ik2(Sy,Sz,Ty,Tz,l1,l2,sign){
    let dy=Ty-Sy, dz=Tz-Sz, d=Math.hypot(dy,dz)||1e-3;
    const dc=Math.min(d, l1+l2-0.005);
    const a=(l1*l1-l2*l2+dc*dc)/(2*dc), h=Math.sqrt(Math.max(0,l1*l1-a*a));
    const uy=dy/d, uz=dz/d, ny=-uz, nz=uy;
    return [Sy+uy*a+ny*h*sign, Sz+uz*a+nz*h*sign];
  }

  /* ============================ ANIMATION (verbatim from pass 1) ============================ */
  const lerp=(a,b2,t)=>a+(b2-a)*t, sm=(t)=>t*t*(3-2*t);
  function toolCurve(anim, u, power){
    const long = power==='long';
    if(anim==='hold') return { pitch:56+3*Math.sin(2*Math.PI*u), yaw:16, wrY:0.150, wrZ:0.60,
      wlY:0.085, wlZ:0.50, lean:0, twist:6, dip:0, bend:0.05+0.02*Math.sin(2*Math.PI*u) };
    if(anim==='dig'){
      const dg=(o)=>{ const cp=Math.cos(o.pitch*DEG), sp=Math.sin(o.pitch*DEG);
        o.wlY=o.wrY-0.26*cp; o.wlZ=o.wrZ-0.26*sp; o.dig=true; return o; };
      const r1=0.28, t1=0.48, p1=0.70;
      if(u<r1){ const t=sm(u/r1);
        return dg({ pitch:lerp(-34,-8,t), yaw:8, wrY:lerp(0.185,0.150,t), wrZ:lerp(0.56,0.74,t),
                    lean:lerp(6,-5,t), twist:lerp(3,-7,t), dip:0.010*(1-t), bend:0 }); }
      if(u<t1){ const t=Math.pow((u-r1)/(t1-r1),1.8);
        return dg({ pitch:lerp(-8,-60,t), yaw:8, wrY:lerp(0.150,0.265,t), wrZ:lerp(0.74,0.470,t),
                    lean:lerp(-5,16,t), twist:lerp(-7,4,t), dip:0.055*t, bend:0 }); }
      if(u<p1){ const t=sm((u-t1)/(p1-t1));
        return dg({ pitch:lerp(-60,-38,t), yaw:8, wrY:lerp(0.265,0.205,t), wrZ:lerp(0.470,0.435,t),
                    lean:lerp(16,4,t), twist:lerp(4,6,t), dip:lerp(0.055,0.065,t), bend:0 }); }
      const t=sm((u-p1)/(1-p1)), toss=Math.sin(Math.PI*Math.min(1,t*1.35));
      return dg({ pitch:lerp(-38,-34,t)+26*toss, yaw:8, wrY:lerp(0.205,0.185,t)-0.03*toss,
                  wrZ:lerp(0.435,0.56,t)+0.10*toss, lean:lerp(4,6,t)-6*toss, twist:lerp(6,3,t)-22*toss,
                  dip:lerp(0.065,0.010,t), bend:0 });
    }
    if(anim==='castBack')    return toolCurve('cast', u*CAST_W1, power);
    if(anim==='castRelease') return toolCurve('cast', CAST_W1 + u*(1-CAST_W1), power);
    if(anim==='bite'){
      const tug=Math.pow(Math.max(0,Math.sin(2*Math.PI*u*2)),6);
      return { pitch:54-5*tug, yaw:16, wrY:0.150+0.010*tug, wrZ:0.60-0.015*tug, wlY:0.085, wlZ:0.50,
               lean:0, twist:6-2*tug, dip:0.006*tug, bend:0.06+0.22*tug };
    }
    if(anim==='strike'){
      if(u<0.28){ const t=sm(u/0.28);
        return { pitch:54-8*t, yaw:16, wrY:0.150+0.020*t, wrZ:0.60-0.05*t, wlY:0.085, wlZ:0.50,
                 lean:3*t, twist:6+4*t, dip:0.020*t, bend:0.10+0.10*t }; }
      if(u<0.55){ const t=Math.pow((u-0.28)/0.27,1.6);
        return { pitch:lerp(46,104,t), yaw:16, wrY:lerp(0.170,0.020,t), wrZ:lerp(0.55,0.92,t),
                 wlY:lerp(0.085,0.020,t), wlZ:lerp(0.50,0.66,t), lean:lerp(3,-9,t), twist:lerp(10,-8,t),
                 dip:0.020*(1-t), bend:lerp(0.20,0.55,t) }; }
      const t=sm((u-0.55)/0.45);
      return { pitch:lerp(104,66,t), yaw:16, wrY:lerp(0.020,0.150,t), wrZ:lerp(0.92,0.62,t),
               wlY:lerp(0.020,0.09,t), wlZ:lerp(0.66,0.52,t), lean:lerp(-9,-4,t), twist:lerp(-8,4,t),
               dip:0, bend:lerp(0.55,0.34,t) };
    }
    if(anim==='reel'){
      const pump=Math.sin(2*Math.PI*u), crank=2*Math.PI*u*2;
      return { pitch:64+16*pump, yaw:16, wrY:0.150-0.03*pump, wrZ:0.60+0.10*Math.max(0,pump),
               wlY:0.085+0.028*Math.cos(crank), wlZ:0.50+0.028*Math.sin(crank),
               lean:-6-3*Math.max(0,pump), twist:6+5*pump, dip:0.012*(0.5-0.5*Math.cos(2*Math.PI*u)),
               bend:0.42+0.16*Math.max(0,pump) };
    }
    if(anim==='land'){
      if(u<0.22){ const t=sm(u/0.22);
        return { pitch:lerp(64,52,t), yaw:16, wrY:lerp(0.150,0.190,t), wrZ:lerp(0.60,0.50,t),
                 wlY:0.085, wlZ:0.50, lean:lerp(-5,4,t), twist:4, dip:0.020*t, bend:lerp(0.42,0.48,t) }; }
      if(u<0.60){ const t=Math.pow((u-0.22)/0.38,0.9);
        return { pitch:lerp(52,112,t), yaw:16, wrY:lerp(0.190,0.020,t), wrZ:lerp(0.50,0.86,t),
                 wlY:lerp(0.085,0.020,t), wlZ:lerp(0.50,0.70,t), lean:lerp(4,-8,t), twist:lerp(4,-6,t),
                 dip:0.020*(1-t), bend:lerp(0.48,0.22,t) }; }
      const t=sm((u-0.60)/0.40);
      return { pitch:lerp(112,86,t), yaw:16, wrY:lerp(0.020,0.120,t), wrZ:lerp(0.86,0.66,t),
               wlY:lerp(0.020,0.270,t), wlZ:lerp(0.70,0.60,t), lean:lerp(-8,-2,t), twist:lerp(-6,2,t),
               dip:0, bend:lerp(0.22,0.12,t) };
    }
    const PB=long?150:128, PF=long?-6:10, LB=long?-8:-5, LF=long?12:7, SB=long?0.78:0.55;
    const w1=CAST_W1, s1=CAST_S1;
    if(u<w1){ const t=sm(u/w1);
      return { pitch:lerp(56,PB,t), yaw:16, wrY:lerp(0.150,-0.075,t), wrZ:lerp(0.60,0.94,t),
               wlY:lerp(0.085,0.02,t), wlZ:lerp(0.50,0.60,t), lean:lerp(0,LB,t), twist:lerp(6,-11,t), dip:0, bend:0.10*t };
    }
    if(u<s1){ const t=(u-w1)/(s1-w1), tt=Math.pow(t,2.1);
      return { pitch:lerp(PB,PF,tt), yaw:16, wrY:lerp(-0.075,0.255,tt), wrZ:lerp(0.94,0.60,tt),
               wlY:lerp(0.02,0.13,tt), wlZ:lerp(0.60,0.50,tt), lean:lerp(LB,LF,tt), twist:lerp(-11,9,tt), dip:0.020*t, bend:lerp(0.10,SB,tt) };
    }
    const t=sm((u-s1)/(1-s1));
    return { pitch:lerp(PF,56,t), yaw:16, wrY:lerp(0.255,0.150,t), wrZ:0.60,
             wlY:lerp(0.13,0.085,t), wlZ:0.50, lean:lerp(LF,0,t), twist:lerp(9,6,t), dip:0.020*(1-t), bend:lerp(SB,0.05,t) };
  }

  /* ==================== BOARDING + HAUL (6.1) ====================
     board / boardDown are the only clips whose GROUND CHANGES under the figure. The pivot stays on
     the dock-side contact for the whole clip and the rise is carried in the pose, so an engine plays
     the clip in place and re-seats the sprite by boardMount().landing on the last frame — no new
     cell, no new pivot, no special case in the baker.

     railZ is a WORLD metre off opts (default 0.55 — a dory sheer) and is deliberately NOT scaled by
     hS: a child steps over the same rail an adult does. It is soft-clamped to 1.55x the figure's own
     hip height, because past that the step stops being a step. A gangway is just railZ near 0 and
     the clip degrades to a long stride; a fixed-wharf face is the wharf rig's own needsLadder case
     (drop > 1.2 m), not this clip. */
  const BOARDING = { board:1, boardDown:1 };
  const RAIL_DEF = 0.55, RAIL_MIN = 0.06, RAIL_MAX = 1.30;
  function railOf(o){
    const r = (o && o.railZ!=null) ? +o.railZ : RAIL_DEF;
    return isFinite(r) ? Math.max(RAIL_MIN, Math.min(RAIL_MAX, r)) : RAIL_DEF;
  }
  function kf(track, u){
    if(u<=track[0][0]) return track[0][1];
    for(let i=0;i+1<track.length;i++){ const a=track[i], c=track[i+1];
      if(u<=c[0]) return a[1]+(c[1]-a[1])*sm((u-a[0])/((c[0]-a[0])||1)); }
    return track[track.length-1][1];
  }
  /* Foot heights are quoted as fractions of R plus a constant, never as fixed metres, so the same
     tracks stay inside the leg's 0.53 m reach at every rail height instead of snapping straight. */
  function boardCurve(anim, u, req, hipBase){
    const cap = hipBase*1.55, R = Math.min(req, cap), clamped = R < req - 1e-6;
    const K = (t)=>kf(t,u), rc = (f,k)=>R*f + (k||0);
    if(anim==='boardDown'){
      /* Stepping down is not the up-clip reversed. The leg has only 0.530 m of reach against a
         0.515 m stance, so a foot can never sit more than ~15 mm below its own hip: the hips must
         come DOWN with the reaching foot and the deep bend goes into the trail leg, still on the
         deck. That is also what a person actually does off a gunwale. */
      return { anim, railZ:R, requested:req, clamped,
        rise:   K([[0,R],[0.18,R*0.92],[0.42,R*0.58],[0.62,R*0.30],[0.78,0],[0.88,-0.045],[1,0]]),
        /* No rail plant going down. Standing on the deck, the rail is at your ANKLES — reaching
           for it is both unreachable from the shoulder and wrong: the hand steadies out to the
           side and never takes load. handOn stays 0 for the whole clip. */
        handOn: 0,
        handOut:K([[0,0.01],[0.25,0.07],[0.60,0.06],[1,0.012]]),
        handZoff:K([[0,0.055],[0.26,0.115],[0.62,0.09],[1,0.055]]),
        handY:  K([[0,0.05],[0.24,0.17],[0.60,0.13],[1,0.018]]),
        phase:  u<0.22?'lift':u<0.55?'reach':u<0.82?'transfer':'settle',
        lead:  { y:K([[0,0.04],[0.22,0.17],[0.50,0.14],[0.70,0.09],[1,0.045]]),
                 z:K([[0,R],[0.14,rc(1,0.055)],[0.34,rc(0.86)],[0.52,rc(0.46)],[0.70,rc(0.16)],[0.82,0],[1,0]]) },
        trail: { y:K([[0,-0.05],[0.52,-0.09],[0.70,-0.02],[0.88,-0.06],[1,-0.05]]),
                 z:K([[0,R],[0.46,R],[0.58,rc(0.62)],[0.72,rc(0.26)],[0.86,0],[1,0]]) },
        handY:  K([[0,0.05],[0.14,0.26],[0.60,0.27],[0.76,0.14],[1,0.018]]),
        freeY:  K([[0,0.02],[0.30,0.16],[0.62,0.10],[1,0.015]]),
        freeOut:K([[0,0.012],[0.34,0.085],[0.70,0.05],[1,0.012]]),
        freeZ:  K([[0,0.055],[0.34,0.115],[0.70,0.085],[1,0.055]]),
        lean:   K([[0,0],[0.24,7],[0.55,11],[0.80,4],[0.90,-2],[1,0]]),
        list:   K([[0,0],[0.24,-4],[0.62,-5],[0.85,0],[1,0]]),
        twist:  K([[0,0],[0.34,6],[0.68,4],[1,0]]) };
    }
    /* load (crouch, hand finds the rail) -> lift (lead foot clears) -> plant (lead sole on deck)
       -> transfer (hips drive up, trail leg swings through) -> settle.
       The trail foot is quoted RELATIVE TO THE RISE, not absolutely: a grounded foot goes out of
       leg reach the instant the hips lift, so it has to leave the dock on the same frame the rise
       starts and then ride it. Quoting the airborne heights as fractions of R keeps the same
       tracks inside reach at every rail height instead of snapping the leg straight. */
    const rise = K([[0,0],[0.16,-0.045],[0.44,-0.030],[0.62,rc(0.46)],[0.84,rc(1.04)],[0.93,rc(0.985)],[1,R]]);
    const trailAir = K([[0,0],[0.44,0],[0.56,0.18],[0.72,0.30],[0.86,0.16],[0.94,0],[1,0]]);
    return { anim, railZ:R, requested:req, clamped, rise,
      /* The hand comes OFF the rail as the rise takes over: the arm has 0.44 m of reach and the
         shoulder climbs by R, so anything still pinned to the rail past the push-off tears loose.
         Releasing on the drive is also just what pushing off a rail looks like. */
      handOn: K([[0,0],[0.14,1],[0.46,1],[0.60,0],[1,0]]),
      handOut: 0, handZoff: 0.055,
      phase:  u<0.16?'load':u<0.44?'lift':u<0.62?'plant':u<0.86?'transfer':'settle',
      lead:  { y:K([[0,0.055],[0.16,0.02],[0.34,0.26],[0.52,0.33],[0.66,0.24],[0.84,0.115],[1,0.045]]),
               z:K([[0,0],[0.14,0.02],[0.34,rc(1,0.085)],[0.50,rc(1,0.085)],[0.62,R],[1,R]]) },
      trail: { y:K([[0,-0.075],[0.42,-0.105],[0.62,-0.16],[0.78,0.055],[0.90,-0.05],[1,-0.045]]),
               z: Math.max(0, rise) + trailAir },
      handY:  K([[0,0.06],[0.14,0.30],[0.62,0.31],[0.78,0.22],[0.90,0.06],[1,0.018]]),
      freeY:  K([[0,0.02],[0.24,-0.14],[0.52,0.06],[0.72,0.20],[0.88,0.06],[1,0.015]]),
      freeOut:K([[0,0.012],[0.30,0.075],[0.62,0.10],[0.84,0.04],[1,0.012]]),
      freeZ:  K([[0,0.055],[0.30,0.10],[0.62,0.16],[0.84,0.08],[1,0.055]]),
      lean:   K([[0,0],[0.16,9],[0.42,15],[0.62,10],[0.80,2],[1,0]]),
      list:   K([[0,0],[0.20,-4],[0.60,-6],[0.82,-1],[1,0]]),
      twist:  K([[0,0],[0.30,7],[0.60,9],[0.84,3],[1,0]]) };
  }
  /* haul — a two-handed heave on a line. Synchronised with the left lagging the right, NOT strict
     hand-over-hand: at 32 px/m alternating hands read as a wobble rather than as work. No tool
     layer — the rope is a runtime line between haulGrip()'s two pins, the way the v1 FisherHaul kit
     drew it. This is the pass-6 answer to tighten/slacken, anchor raise and the windlass read. */
  function haulCurve(u){
    const K=(t)=>kf(t,u);
    const pull = K([[0,0],[0.28,0.06],[0.42,0.55],[0.58,1],[0.74,0.5],[0.88,0.1],[1,0]]);
    return { pull, dip:0.045*pull, lean:-(2+12*pull), twist:-4*pull,
      R:{ y:K([[0,0.29],[0.28,0.31],[0.56,0.02],[0.78,0.15],[1,0.29]]),
          z:K([[0,0.185],[0.28,0.205],[0.56,0.075],[0.78,0.135],[1,0.185]]) },
      L:{ y:K([[0,0.245],[0.34,0.275],[0.62,-0.015],[0.84,0.115],[1,0.245]]),
          z:K([[0,0.135],[0.34,0.155],[0.62,0.055],[0.84,0.10],[1,0.135]]) } };
  }

  /* ==================== LADDER DESCENT (6.2) ====================
     The clip 6.1 wrote down as missing: above the board clamp (1.55x hip) the answer is a ladder,
     and there was no ladder. This is a LOCOMOTION cycle, not a boarding clip — the ground does not
     change under the figure, the cell plays IN PLACE the way walk does, and the engine translates
     the sprite down the ladder as the loop runs.

     The rungs are the wharf rig's own — WharfIso.FIT.ladder = { w:0.45, rung:0.30 }. Both arrive as
     world metres off opts (rung, ladderW) and neither is scaled by build, for the same reason railZ
     is not: a child climbs the same rungs an adult does.

     FEET STEP, HANDS SLIDE. The feet alternate and each one steps TWO rungs, which is the only
     pattern that keeps both soles on real rungs and holds them exactly one rung apart the whole way
     down; the body therefore goes down two rungs — 0.60 m — per loop. The hands do NOT change rungs.
     They ride the stringers at 0.42 of the ladder's own width and slide, which is both what a person
     does on a galvanised wharf ladder and the only thing that reads at 32 px/m: this figure is
     1.51 m tall against a 0.30 m rung, so a hand hopping rungs is a four-pixel flail. Two hands are
     on the ladder in every frame and at least one foot, so contact never drops below three points.

     THE BODY DOES NOT SINK AT A CONSTANT RATE, and that is the part an engine has to respect. A
     climber drops when a LEG extends, so drop(u) is a two-step stair: 0.30 m eased through each foot
     swing, dead flat while both feet are planted. A planted hold is fixed in the WORLD, so its
     height in the cell is (hold - drop(u)) — which is why a planted foot rides 0.30 m up the cell
     and then steps back down. Translate the sprite by ladderMount().descend and the soles sit still
     on real rungs; translate it linearly instead and they creep by up to a third of a rung. */
  const LADDER = { ladderDown:1 };
  const RUNG_DEF = 0.30, LADW_DEF = 0.45, LAD_SW = 0.24, LAD_RF = 0.00, LAD_LF = 0.50;
  function rungOf(o){ const r = (o && o.rung!=null) ? +o.rung : RUNG_DEF;
    return isFinite(r) ? Math.max(0.18, Math.min(0.45, r)) : RUNG_DEF; }
  function ladWOf(o){ const w = (o && o.ladderW!=null) ? +o.ladderW : LADW_DEF;
    return isFinite(w) ? Math.max(0.30, Math.min(0.70, w)) : LADW_DEF; }
  function ladderCurve(u, R, LW, hipBase, ankleZ){
    const seg = (a,f)=> Math.max(0, Math.min(1, (f-a)/LAD_SW));
    /* metres descended by u. Defined past the ends of the loop so a foot planted across the wrap
       reads one continuous stair instead of snapping back at u = 0. */
    const D = (t)=>{ const k = Math.floor(t), f = t - k;
      return 2*R*k + R*sm(seg(LAD_RF,f)) + R*sm(seg(LAD_LF,f)); };
    const foot = (ph)=>{
      let plant = ph + LAD_SW;                 // this foot's most recent plant
      while(plant > u) plant -= 1;
      const go = plant + 1 - LAD_SW;           // and when it leaves the rung again
      if(u <= go) return { z: ankleZ + D(u) - D(plant), air:0 };
      const s = (u - go)/LAD_SW, e = sm(s);
      const z0 = ankleZ + D(go) - D(plant), z1 = ankleZ + D(u) - D(plant) - 2*R;
      return { z: z0 + (z1-z0)*e, air: Math.sin(Math.PI*s) };
    };
    const fR = foot(LAD_RF), fL = foot(LAD_LF);
    const sw = Math.sin(2*Math.PI*(u + 0.25));
    /* contralateral: the hand opposite the stepping foot is the low one, taking the load */
    const hand = (sgn, d)=>({ x: sgn*LW*0.42, y:0.275, dz: 0.11 + 0.15*d, air:0 });
    const step = (t)=>({ y: 0.16 - 0.055*t.air, z:t.z, air:t.air });
    const air = Math.max(fR.air, fL.air);
    return { rung:R, width:LW, stepZ:2*R, drop:D(u),
      hipZ: ankleZ + (hipBase - ankleZ)*0.95,
      F:{ L:step(fL), R:step(fR) }, H:{ L:hand(-1,-sw), R:hand(1,sw) },
      moving: fR.air>0.001 ? 'RF' : fL.air>0.001 ? 'LF' : 'set',
      contact: air>0.001 ? 3 : 4,
      lean:  13 + 2*Math.sin(2*Math.PI*u),
      list:  2.6*Math.sin(2*Math.PI*(u+0.12)),
      twist: 4*sw };
  }

  /* ============================ POSE ============================
     Built from the GROUND UP so age can change proportion without breaking contact with the floor:
       ankle -> leg (length scale) -> hip -> torso (length scale) -> collar -> neck -> head (size
       scale). For an adult male every one of these resolves to pass 4's frozen constant (hip 0.575,
       torso centre 0.855, shoulder 1.060, collar 1.037) — the only deliberate change is the head,
       which pass 4 sat 0.07 m too low: its chin (1.028) was BELOW its own collar (1.037), which is
       why the figure had no neck. */
  function pose(anim, u, b, power){
    const PR = propsOf(b);
    const hS=PR.hS, wS=PR.wS;
    const ankleZ = 0.060*hS;
    const legLen = 0.515*hS*PR.legK;
    /* 6.1: the rise is folded into hipZ0 so every derived height — torso, collar, chin, head,
       shoulders — rides up with it. Everything below resolves to the pass-5 constant when no
       boarding clip is active, so the frozen poses stay byte-identical. */
    const hipBase = ankleZ + legLen;
    const brd = BOARDING[anim] ? boardCurve(anim, u, railOf(arguments[5]), hipBase) : null;
    const hl  = (anim==='haul') ? haulCurve(u) : null;
    const lad = LADDER[anim] ? ladderCurve(u, rungOf(arguments[5]), ladWOf(arguments[5]), hipBase, ankleZ) : null;
    const hipZ0  = lad ? lad.hipZ : hipBase + (brd ? brd.rise : 0);
    const TS     = hS*PR.torsoK;
    const torsoC = hipZ0 + 0.280*TS;
    const collarZ= torsoC + 0.182*TS;
    const shZ0   = torsoC + 0.205*TS;
    const chinZ  = collarZ + 0.062*hS*PR.neckLen;
    const headZ  = chinZ + 0.212*PR.headK;

    let stride=0, lift=0, bob=0, lean=0, arm=0, yaw=0, handF=0.63;
    if(anim==='walk'){ stride=0.16; lift=0.09; bob=0.030; lean=4*DEG;  arm=0.13; yaw=8*DEG;  handF=0.65; }
    if(anim==='run') { stride=0.24; lift=0.16; bob=0.055; lean=11*DEG; arm=0.18; yaw=12*DEG; handF=0.78; }
    stride*=PR.legK; lift*=PR.legK;
    const TOOLS = { hold:1, cast:1, dig:1, bite:1, strike:1, reel:1, land:1, castBack:1, castRelease:1 };
    const tc = TOOLS[anim] ? toolCurve(anim,u,power) : null;
    const carry = (!tc && (arguments.length>4)) ? arguments[4] : null;
    const rock = arguments[5] || null;
    const bal = anim==='balance', stag = anim==='stagger';
    const tw=Math.sin(2*Math.PI*u);
    const idle = anim==='idle', calm = idle || anim==='hold' || bal || anim==='bite';
    if(tc) lean = tc.lean*DEG;
    if(carry==='tray') lean = -5*DEG;
    if(carry==='buckets') lean = lean*0.5;
    if(carry==='helm') lean = 6*DEG;
    if(carry==='oars') lean = 10*DEG;
    if(brd) lean = brd.lean*DEG;
    if(hl)  lean = hl.lean*DEG;
    if(lad) lean = lad.lean*DEG;
    const senv = stag ? Math.exp(-2.4*u) : 0;
    if(stag) lean += (6*DEG)*senv*Math.sin(2*Math.PI*1.2*u);
    let list = 0;
    if(bal)  list = (10*DEG)*Math.sin(2*Math.PI*u);
    if(stag) list = (20*DEG)*senv*Math.sin(2*Math.PI*1.3*u);
    if(brd)  list = brd.list*DEG;
    if(lad)  list = lad.list*DEG;
    if(rock && rock.counter){ const c=counterLean(rock.roll||0, rock.pitch||0, rock.counter);
      list += c.list*DEG; lean += c.lean*DEG; }
    const breathe = calm ? 0.018*Math.sin(2*Math.PI*u)*hS : 0;
    const swayX = tc ? (calm?0.012*tw:0) : ((brd||lad) ? 0 : (idle ? 0.012*tw : 0.010*tw));
    let dip;
    if(tc) dip = tc.dip*hS;
    else if(brd||lad) dip = 0;
    else if(hl)  dip = hl.dip*hS;
    else if(idle) dip = 0;
    else if(bal)  dip = 0.010*hS*(0.5+0.5*Math.cos(4*Math.PI*u));
    else if(stag) dip = 0.060*hS*senv*(u<0.5?1:0.6);
    else dip = bob*hS*(0.5+0.5*Math.cos(4*Math.PI*u));
    const hipZ = hipZ0 - dip;
    const twS = brd||hl||lad;
    const yawS = tc ? tc.twist*DEG : twS ? twS.twist*DEG : yaw*tw,
          yawH = tc ? tc.twist*0.45*DEG : twS ? twS.twist*0.40*DEG : -0.6*yaw*tw;

    /* STOOP is a real pitch of the upper body about a point below the ribs, applied on top of the
       whole-body lean about the hip. Chaining two pivots is what makes an elder read as bent rather
       than as tilted: the legs stay vertical and only the spine folds. */
    const stoopR = (PR.stoop||0)*DEG;
    const spineZ = torsoC - 0.10*TS;
    const leanP  = stoopR ? chain(pitchX(stoopR, spineZ), pitchX(lean, hipZ)) : pitchX(lean, hipZ);
    const listR  = list ? rollY(list, hipZ) : null;
    const headListR = list ? rollY(list*0.35, hipZ) : null;

    const P = { anim,u,hS,wS,PR, hipZ, hipZ0, ankleZ, torsoC, collarZ, shZ0, chinZ, headZ, TS,
                swayX, lean, breathe, yawS, yawH, leanP, list, listR, headListR, stoopR, spineZ };
    P.legs = {};
    const thigh=0.280*hS*PR.legK, shin=0.250*hS*PR.legK;
    for(const [side, ph] of [['L',0],['R',0.5]]){
      const sgn = side==='L' ? -1 : 1;
      const p2=(u+ph)%1;
      const braceX = (carry==='helm'||carry==='oars'||bal||stag||hl) ? 1.55 : 1;
      const hip0 = rotZ(yawH)([sgn*0.082*wS*PR.hipK*braceX, 0, 0]); hip0[0]+=swayX*0.5; hip0[2]=hipZ;
      let yF, zF;
      if(tc){ yF = sgn<0 ? (tc.dig?0.105:0.075) : (tc.dig?-0.085:-0.055); zF = ankleZ; }
      else if(brd){ const LG=(side==='R'?brd.lead:brd.trail); yF = LG.y; zF = ankleZ + LG.z; }
      /* the ladder tracks are already absolute: z is the rung the sole is on, plus the ankle */
      else if(lad){ const LG=(side==='R'?lad.F.R:lad.F.L); yF = LG.y; zF = LG.z; }
      else if(hl){ yF = (side==='R') ? 0.115 : -0.135; zF = ankleZ; }
      else if(idle){ yF = sgn*0.012; zF = ankleZ; }
      else if(bal||stag){ yF = sgn*0.02 + (stag?0.05*senv*Math.sin(2*Math.PI*1.3*u):0); zF = ankleZ; }
      else { yF = stride*Math.cos(2*Math.PI*p2); zF = ankleZ + lift*Math.max(0,-Math.sin(2*Math.PI*p2)); }
      /* Same guard as the arms, for the same reason: a boarding foot is the only target that can
         out-reach its own leg (at a tall rail, or at the top of the drive). Tuned so the default
         rails never need it — it is the floor under the extremes, not the mechanism. */
      if(brd||lad){ const mx=(thigh+shin)*0.995, dy2=yF-hip0[1], dz2=zF-hip0[2], dd=Math.hypot(dy2,dz2);
        if(dd>mx){ const k=mx/dd; yF=hip0[1]+dy2*k; zF=hip0[2]+dz2*k; } }
      const [ky,kz]=ik2(hip0[1],hip0[2], yF, zF, thigh, shin, +1);
      /* a ladder knee goes OUT, not forward — the 2D solver can only bend in the sagittal plane, so
         the splay is added here. It is what stops a bent leg reading as a sit. */
      const kx = lad ? hip0[0]*0.97 + sgn*0.038 : hip0[0]*0.97;
      P.legs[side]={ hip:hip0, knee:[kx,ky,kz], ankle:[hip0[0]*0.92, yF, zF] };
    }
    P.arms = {};
    const upA=0.230*hS*PR.legK, foA=0.210*hS*PR.legK, shZ=shZ0+breathe, sw=0.150*wS*PR.shoulderK;
    for(const [side, ph] of [['L',0.5],['R',0]]){
      const sgn = side==='L' ? -1 : 1;
      const p2=(u+ph)%1;
      let sh = P.leanP(rotZ(yawS)([sgn*sw, 0, shZ])); sh[0]+=swayX*0.5;
      if(listR) sh = listR(sh);
      let ty, tz, tx=sh[0]+sgn*0.012;
      const hz=(f)=>ankleZ + (f-0.060)*hS/1 + breathe*0.5;   // pass-4 hand heights, in body units
      if(tc){ ty = side==='R' ? tc.wrY : tc.wlY; tz = (side==='R' ? tc.wrZ : tc.wlZ)*hS + breathe*0.5; }
      else if(carry==='buckets'){ tx = sh[0]+sgn*0.048; ty = 0.012 + 0.30*arm*Math.cos(2*Math.PI*p2); tz = 0.60*hS + breathe*0.5; }
      else if(carry==='tray'){ tx = sgn*0.24; ty = 0.150 + (idle ? 0.006*Math.sin(2*Math.PI*u) : 0.012*Math.cos(4*Math.PI*u)); tz = 0.615*hS + breathe*0.5; }
      else if(carry==='helm'){ tx = sh[0]+sgn*0.005; ty = 0.150 + (idle?0.006*Math.sin(2*Math.PI*u):0.012*Math.cos(4*Math.PI*u)); tz = 0.72*hS + breathe*0.5; }
      else if(carry==='oars'){ tx = sh[0]+sgn*0.055; ty = 0.120 + (idle?0.014*Math.sin(2*Math.PI*u):0.02*Math.cos(4*Math.PI*u)); tz = 0.575*hS + breathe*0.5; }
      /* Boarding with a load: the carry branches sit ABOVE this one, so a pail or a tray keeps both
         hands and the clip still plays — the legs carry the whole read. Empty-handed, the left hand
         plants on the rail (handOn blends it between a body-relative rest and the rail's own
         absolute height) and the right arm counterbalances. */
      else if(brd){
        if(side==='L'){ const on=brd.handOn;
          tx = sh[0] - 0.020 - 0.030*on - brd.handOut; ty = brd.handY;
          tz = (hipZ + brd.handZoff*hS)*(1-on) + (ankleZ + brd.railZ + 0.022)*on + breathe*0.5; }
        else { tx = sh[0] + brd.freeOut; ty = brd.freeY; tz = hipZ + brd.freeZ*hS; }
      }
      else if(hl){ const HH = side==='R' ? hl.R : hl.L;
        tx = sh[0] + sgn*0.028; ty = HH.y; tz = hipZ + HH.z*hS; }
      /* On the ladder the hands ride the STRINGERS: x sits over the rails at 0.42 of the ladder's
         own width and dz is an offset off the shoulder, so the grip slides instead of hopping. */
      else if(lad){ const HH = side==='R' ? lad.H.R : lad.H.L;
        tx = HH.x; ty = HH.y; tz = shZ0 + HH.dz + breathe*0.5; }
      else if(bal||stag){ const out=0.05+Math.abs(list)*0.9; tx = sh[0]+sgn*out; ty = 0.03 - list*sgn*0.35; tz = (0.585 - (stag?0.03*senv:0))*hS + breathe*0.5; }
      else if(idle){ ty = 0.015 + 0.008*Math.sin(2*Math.PI*u+(sgn>0?0.6:0)); tz = 0.63*hS+breathe*0.5; }
      else { ty = sh[1] + arm*Math.cos(2*Math.PI*p2); tz = handF*hS; }
      /* Only the new clips move a hand far enough to out-reach the arm (the frozen fourteen were
         all authored inside it). ik2 clamps the ELBOW but the wrist is placed raw, so without this
         the hand detaches from the forearm the moment the shoulders climb past the rail. */
      if(brd||hl||lad){ const mx=(upA+foA)*0.985, dy2=ty-sh[1], dz2=tz-sh[2], dd=Math.hypot(dy2,dz2);
        if(dd>mx){ const k=mx/dd; ty=sh[1]+dy2*k; tz=sh[2]+dz2*k; } }
      const [ey,ez]=ik2(sh[1],sh[2], ty, tz, upA, foA, -1);
      P.arms[side]={ sh, elbow:[sh[0]+sgn*0.01,ey,ez], wrist:[tx,ty,tz] };
    }
    P.neckB = (listR||ID)(P.leanP([swayX*0.5, 0, collarZ-0.014]));
    const headLean = stoopR
      ? chain(pitchX(stoopR*1.30, spineZ), pitchX(lean*0.55, hipZ))
      : pitchX(lean*0.55, hipZ);
    P.headC = (headListR||ID)(headLean([swayX*0.5, 0.005, headZ + breathe]));
    P.eyesClosed = calm && u>0.60 && u<0.78;
    P.look = root.HeadIso
      ? root.HeadIso.look({ anim, u, t:(rock&&rock.t), expr:(b.expr||(rock&&rock.expr)), talk:(rock&&rock.talk) })
      : { gaze:[0,0], lid:(P.eyesClosed?1:0), brow:0, mouth:'neutral' };
    P.tool = tc ? { pitch:tc.pitch*DEG, yaw:tc.yaw*DEG, bend:tc.bend } : null;
    P.board = brd; P.haul = hl; P.ladder = lad; P.rise = brd ? brd.rise : 0;
    return P;
  }

  /* ============================ TORSO ============================ */
  const TDEP = 0.78;
  function torsoProf(PR){
    const w=PR.wS, D=TDEP, sh=PR.shoulderK, ch=PR.chestK, wa=PR.waistK, hp=PR.hipK, bu=PR.bust;
    return [
      [-0.155, 0.144*w*hp,  0.092*D*w],              // hip / waistband
      [-0.085, 0.140*w*wa,  0.090*D*w],              // waist — where sex reads hardest
      [ 0.010, 0.152*w*ch, (0.096+bu*0.45)*D*w],     // lower ribs
      [ 0.078, 0.158*w*ch, (0.100+bu)*D*w],          // chest / bust (widest)
      [ 0.112, 0.148*w*sh,  0.094*D*w],              // shoulder shelf
      [ 0.140, 0.116*w*sh,  0.080*D*w],              // trapezius 1
      [ 0.158, 0.072*w*sh,  0.058*D*w],              // trapezius 2
      [ 0.170, 0.046*w*PR.neckK, 0.040*D*w],         // collar
    ];
  }

  function facesOf(P, b){
    const PR=P.PR, wS=PR.wS, hS=P.hS, TS=P.TS, F=[];
    const add=(fs)=>{ for(const f of fs) F.push(f); };
    const listX = P.listR || ID;
    const torsoXf = chain(TX(P.swayX*0.5,0,0), listX, P.leanP, rotZ(P.yawS*0.6));
    const G = GARMENTS[b.garment] || GARMENTS.overalls;
    const TP = torsoProf(PR);
    // dz in torso-local units -> world z, and the radii at that ring
    const at=(dz,grow)=>[dz*TS, profR(TP,dz,1)+(grow||0), profR(TP,dz,2)+(grow||0)];
    const zOf=(dz)=>P.torsoC + dz*TS;
    const TC=[0,0,P.torsoC];
    const FRONT=Math.PI/2, BACK=-Math.PI/2;

    const LEGX = 0.004*wS, ARMX = 0.016*wS*PR.shoulderK, SHDROP = 0.098*hS;
    const ox=(p,dx)=>[p[0]+dx, p[1], p[2]];
    const legMat = G.skirtHem ? 'boot' : 'over';

    // ---------- LEGS ----------
    /* LEG WIDTH IS SET BY THE GAP, NOT BY THE LEG. The hip is 0.288 m across (9 px). Two legs and
       a readable gap between them is 3 px + 2 px + 3 px, so the leg radius has to come DOWN to
       0.056 — pass 4's 0.078 made each leg 5 px, the pair filled the hip edge to edge, and the
       whole lower body read as one skirt. This single number is most of why the overalls failed. */
    const rTh=0.056*wS*PR.limbR, rKn=0.049*wS*PR.limbR, rAn=0.042*wS*PR.limbR;
    for(const side of ['L','R']){
      const g=P.legs[side], sgn = side==='L' ? -1 : 1, dx = sgn*LEGX;
      const hip=ox(g.hip,dx), knee=ox(g.knee,dx), a=ox(g.ankle,dx);
      if(!G.skirtHem){
        add(limb(hip,  knee, rTh, rKn, 'over', -0.04));
        add(limb(knee, a,    rKn*0.94, rAn, 'over', -0.14));
      } else {
        // under a skirt only the lower leg shows
        add(limb([knee[0],knee[1],knee[2]-0.01], a, rKn*0.72, rAn*0.80, 'skin', -0.10));
      }
      /* BOOT SHAFT — pass 4 gave the boot one screen row, so the trouser simply changed colour at
         the floor and the leg had no terminator. A shaft up the calf is both the right garment and
         the thing that stops the teal reading as a hem-less skirt. */
      /* The boot top is cut at a fixed HEIGHT for the frozen fourteen, which is what pass 4 baked
         and what a planted foot wants. A boarding foot tucks high enough that its knee sits BELOW
         its own ankle, and a fixed-height cut then extrapolates backwards down the shin into a
         shaft several metres long. On the boarding clips the shaft is measured as a DISTANCE along
         the shin instead — identical for a vertical shin, correct for an inverted one. */
      if(G.bootZ>0.02){
        let top;
        if(P.board){
          const vx=knee[0]-a[0], vy=knee[1]-a[1], vz=knee[2]-a[2];
          const vl=Math.hypot(vx,vy,vz)||1, tt=Math.min(1,(G.bootZ*hS)/vl);
          top=[a[0]+vx*tt, a[1]+vy*tt, a[2]+vz*tt];
        } else {
          const bz=P.ankleZ+G.bootZ*hS, t=(bz-a[2])/Math.max(0.001,(knee[2]-a[2]));
          top=[a[0]+(knee[0]-a[0])*t, a[1]+(knee[1]-a[1])*t, bz];
        }
        add(limb(top, [a[0],a[1],a[2]+0.006], rAn*1.20, rAn*1.14, 'boot', -0.10));
        add(lathe([top[0],top[1],top[2]], [[-0.020,rAn*1.24,rAn*1.24],[0.018,rAn*1.28,rAn*1.28]],
          'bootL', 0.10, 0.014, null, 6));   // cuff: one row, so the boot top OWNS a pixel row
      }
      add(box([a[0], a[1]-0.002, a[2]+0.002], [0.047*wS, 0.048*wS, 0.028], 'boot', -0.06, -0.02));
      add(box([a[0], a[1]+0.056*hS, a[2]-0.016], [0.044*wS, 0.046*wS, 0.022], 'boot',  0.06, -0.04));
      add(box([a[0], a[1]+0.026*hS, a[2]-0.032], [0.049*wS, 0.090*wS, 0.010], 'sole', -0.40, -0.05));
    }
    /* INSEAM. The legs part by about 1 px at the shin; with nothing behind them the gap shows
       background and the pair reads as one mass with a seam scratched on it. A dark panel set BACK
       (negative db) fills the gap so the two legs are separated by a dark line instead. */
    if(!G.skirtHem){
      const iz0=P.ankleZ+P.rise+0.11*hS, iz1=P.hipZ-0.010*hS;
      if(iz1 > iz0 + 0.01)
        add(box([P.swayX*0.5, 0.004, (iz0+iz1)/2], [0.044*wS, 0.050*wS, (iz1-iz0)/2], 'overD', -0.62, -0.09));
    }

    // ---------- PELVIS ----------
    add(lathe([P.swayX*0.5, 0, P.hipZ+0.020*hS],
      [[-0.052*hS, 0.112*wS*PR.hipK, 0.080*wS],[-0.008*hS, 0.130*wS*PR.hipK, 0.090*wS],[0.048*hS, 0.128*wS*PR.hipK, 0.088*wS]],
      G.skirtHem?'over':'over', -0.12, 0, rotZ(P.yawH), 8));

    // ---------- TORSO ZONES ----------
    /* THE PASS-5 FIX, in three lines: the outer garment wraps the torso only UP TO wrapTop; the
       shirt owns everything above; the bib is a separate FRONT ARC. Pass 4 ran one closed teal ring
       from the armpits down, which is a bodice, not a pair of overalls. */
    const zBot=-0.155, zTop=0.182;
    const wrapTop = G.wrapTop;
    if(wrapTop!=null){
      add(lathe(TC, [at(zBot),at(-0.120),at(wrapTop)], 'over', -0.02, 0, torsoXf, 8));
      add(lathe(TC, [at(wrapTop),at((wrapTop+zTop)/2),at(0.140),at(0.170),at(zTop)], 'shirt', 0.14, 0, torsoXf, 8));
      if(G.waistBand){
        // waistband: a full row of the darker step, so the wrap's top edge owns a pixel row.
        // NOT drawn under a bib: at elev 40 a ring's FLANK projects ~1.5 rows HIGHER than its front,
        // so this ring's side pixels land exactly in the rows where the shirt must show beside the
        // bib — and 2 px of shirt at the flank IS the overalls cue. Measured off a pixel dump.
        add(lathe(TC, [at(wrapTop-0.030,0.005),at(wrapTop+0.012,0.005)], 'overD', -0.22, 0.030, torsoXf, 8));
      }
    } else {
      add(lathe(TC, [at(zBot),at(-0.085),at(0.010),at(0.078),at(0.140),at(0.170),at(zTop)], 'shirt', 0.10, 0, torsoXf, 8));
    }

    // ---------- BIB + STRAPS ----------
    if(G.bibTop!=null){
      const h=G.bibArc, bt=G.bibTop, bb=(wrapTop!=null?wrapTop:-0.085);
      const hemLo=Math.max(bb, bt-0.042/TS);   // one screen row of hem at elev 40
      const rows=(a2,b2,g)=>{ const o=[]; const n=3;
        for(let i=0;i<=n;i++){ const d=a2+(b2-a2)*i/n; o.push(at(d,g)); } return o; };
      add(arcLathe(TC, rows(bb,hemLo,0.007), 'over',  -0.02, 0.020, torsoXf, FRONT-h, FRONT+h, 5));
      add(arcLathe(TC, rows(hemLo,bt,0.009), 'overD', -0.34, 0.030, torsoXf, FRONT-h, FRONT+h, 5));
      // back panel: lower than the bib, so from behind he is teal to the shoulder blades
      if(G.backTop!=null){
        add(arcLathe(TC, rows(bb,G.backTop,0.007), 'over', -0.06, 0.020, torsoXf, BACK-h*0.92, BACK+h*0.92, 5));
        add(arcLathe(TC, rows(G.backTop-0.038,G.backTop,0.009), 'overD', -0.16, 0.030, torsoXf, BACK-h*0.92, BACK+h*0.92, 5));
      }
      if(G.straps){
        const sxx = Math.sin(h)*(profR(TP,0.078,1)+0.007);   // x of the bib's outboard edge
        const sX  = Math.max(0.042*wS, sxx-0.004*wS);         // ON that edge: 1 px column, 4 px of shirt between
        for(const sgn of [-1,1]){
          // FRONT strap: a 1 px column from the bib corner to the collar. Vertical, because a
          // diagonal 1 px tube crosses pixel columns and lands as isolated dots.
          const yF=profR(TP,0.130,2)+0.004;
          add(box([sgn*sX, yF*0.78, (zOf(bt)+zOf(0.184))/2],
            [0.0155*wS, 0.016, (zOf(0.184)-zOf(bt))/2], 'over', -0.40, 0.034, torsoXf));
          // BACK strap, angled inward toward the spine (that is what overall straps do)
          add(box([sgn*sX*0.66, -(profR(TP,0.120,2)+0.002), (zOf(-0.010)+zOf(0.176))/2],
            [0.0150*wS, 0.014, (zOf(0.176)-zOf(-0.010))/2], 'over', -0.30, 0.026, torsoXf));
          // over the shoulder: a short cap so the strap does not vanish at the top
          add(box([sgn*sX, 0.006, zOf(0.176)], [0.0165*wS, profR(TP,0.164,2)+0.006, 0.016*hS],
            'overD', 0.16, 0.030, torsoXf));
          /* BUCKLE. One warm pixel where each strap meets the bib. At 32 px/m two brass pixels are
             the cheapest unmistakable "these are overalls" signal available. */
          if(G.buckles)
            add(box([sgn*sX, yF*0.92, zOf(bt)-0.020*hS], [0.0150*wS, 0.015, 0.019*hS],
              'brass', 0.60, 0.070, torsoXf));
        }
      }
    }

    // ---------- VEST ----------
    if(G.vest){
      add(lathe(TC, [at(-0.100,0.008),at(-0.020,0.008),at(0.060,0.008),at(0.112,0.008),at(0.140,0.008)],
        'over', -0.02, 0.018, torsoXf, 8));
      // the OPENING: a 1 px light column down the centre front. Without it a vest is just a
      // differently coloured torso.
      add(box([0, profR(TP,0.040,2)+0.014, (zOf(-0.096)+zOf(0.126))/2],
        [0.0150*wS, 0.012, (zOf(0.126)-zOf(-0.096))/2], 'shirtL', 0.30, 0.030, torsoXf));
    }

    // ---------- APRON ----------
    if(G.apron){
      const aTop=zOf(0.086), aBot=P.ankleZ+P.rise+0.40*hS;
      const rx=0.150*wS, ry=0.100*wS;
      const rows=[]; const n=5;
      for(let i=0;i<=n;i++) rows.push([aBot+(aTop-aBot)*i/n - P.torsoC, rx*(1+0.06*(1-i/n)), ry]);
      add(arcLathe(TC, rows, 'apron', 0.06, 0.046, torsoXf, FRONT-0.78, FRONT+0.78, 6));
      // hem + waist tie, each a full row so they own their pixels
      add(arcLathe(TC, [[aBot-P.torsoC, rx*1.06, ry],[aBot-P.torsoC+0.040, rx*1.06, ry]],
        'apronD', -0.20, 0.052, torsoXf, FRONT-0.78, FRONT+0.78, 6));
      add(arcLathe(TC, [at(-0.096,0.026),at(-0.056,0.026)], 'apronD', -0.16, 0.052, torsoXf, FRONT-1.5, FRONT+1.5, 8));
      for(const sgn of [-1,1])   // neck loop
        add(box([sgn*0.052*wS, profR(TP,0.130,2)+0.008, (zOf(0.086)+zOf(0.176))/2],
          [0.0140*wS, 0.014, (zOf(0.176)-zOf(0.086))/2], 'apronD', -0.10, 0.040, torsoXf));
    }

    // ---------- SKIRT ----------
    if(G.skirtHem){
      const sTop=-0.100, sBotZ=P.ankleZ+P.rise+0.30*hS;
      const rows=[]; const n=5;
      for(let i=0;i<=n;i++){
        const t=i/n, z=sBotZ+(zOf(sTop)-sBotZ)*t;
        rows.push([z-P.torsoC, (0.238-0.086*t)*wS, (0.150-0.056*t)*wS]);
      }
      add(lathe(TC, rows, 'over', -0.06, 0, torsoXf, 10));
      add(lathe(TC, [[sBotZ-P.torsoC, 0.242*wS, 0.152*wS],[sBotZ-P.torsoC+0.042, 0.240*wS, 0.151*wS]],
        'overD', -0.18, 0.030, torsoXf, 10));
    }
    if(G.shawl){
      add(lathe(TC, [at(0.030,0.012),at(0.090,0.014),at(0.140,0.014),at(0.168,0.012)],
        'overD', 0.08, 0.026, torsoXf, 8));
    }

    // ---------- BELT ----------
    if(G.belt){
      add(lathe(TC, [at(-0.126,0.007),at(-0.086,0.007)], 'belt', -0.02, 0.028, torsoXf, 8));
      add(box([0, profR(TP,-0.106,2)+0.016, zOf(-0.106)], [0.020*wS, 0.012, 0.019*hS],
        'brass', 0.55, 0.046, torsoXf));
    }

    // ---------- SHOULDER CAPS ----------
    for(const sgn of [-1,1]){
      add(lathe([sgn*0.116*wS*PR.shoulderK, 0, P.torsoC+0.076*TS], [
        [-0.040*hS, 0.032*wS, 0.036*wS],
        [-0.012*hS, 0.041*wS, 0.045*wS],
        [ 0.016*hS, 0.038*wS, 0.041*wS],
        [ 0.036*hS, 0.022*wS, 0.026*wS],
      ], (G.sleeve==='longOver'||G.vest)?'over':'shirt', -0.20, 0.014, torsoXf, 8));
    }

    // ---------- COLLAR ----------
    const cMat = G.collar==='stand' ? ((G.sleeve==='longOver')?'overD':'shirtD') : 'collar';
    if(G.collar==='roll'){
      add(lathe(TC, [at(0.156,0.020),at(0.182,0.024),at(0.206,0.020)], 'shirtD', 0.20, 0.022, torsoXf, 8));
    } else if(G.collar==='stand'){
      add(lathe(TC, [at(0.164,0.014),at(0.196,0.014)], cMat, 0.18, 0.020, torsoXf, 8));
    } else if(G.collar==='open'){
      add(lathe(TC, [at(0.160,0.012),at(0.184,0.012)], 'shirtD', 0.16, 0.020, torsoXf, 8));
      add(box([0, profR(TP,0.150,2)+0.010, zOf(0.150)], [0.014*wS, 0.012, 0.020*hS], 'shirtD', -0.55, 0.032, torsoXf));
    } else {
      add(lathe(TC, [at(0.166,0.010),at(0.186,0.010)], 'collar', 0.16, 0.018, torsoXf, 8));
    }

    // ---------- ARMS ----------
    const rSh=0.054*wS*PR.limbR, rHem=0.047*wS*PR.limbR, rEl=0.043*wS*PR.limbR,
          rFo=0.040*wS*PR.limbR, rWr=0.032*wS*PR.limbR;
    const sleeveMat = G.sleeve==='longOver' ? 'over' : 'sleeve';
    for(const side of ['L','R']){
      const a=P.arms[side], sgn = side==='L' ? -1 : 1, dx = sgn*ARMX;
      const sh=[a.sh[0]+dx, a.sh[1], a.sh[2]-SHDROP];
      const el=[a.elbow[0]+dx, a.elbow[1], a.elbow[2]-SHDROP*0.40];
      const wr=ox(a.wrist,dx);
      if(G.sleeve==='short'){
        const hem=v_add(sh, v_mul(v_sub(el,sh), 0.62));
        add(limb(sh,  hem, rSh, rHem, sleeveMat, -0.26));
        add(limb(hem, el,  rEl, rFo,  'skin',    -0.08));
        add(limb(el,  wr,  rFo, rWr,  'skin',    -0.12));
      } else {
        const full = G.sleeve==='longShirtFull' || G.sleeve==='longOver';
        add(limb(sh, el, rSh, rEl+0.003, sleeveMat, -0.26));
        add(limb(el, v_add(el, v_mul(v_sub(wr,el), full?0.90:0.80)), rEl+0.003, rWr+0.004, sleeveMat, -0.12));
        add(limb(v_add(el, v_mul(v_sub(wr,el), full?0.86:0.76)), wr, rWr+0.002, rWr, 'skin', -0.12));
        if(G.cuff||G.cuffOver){
          const cp=v_add(el, v_mul(v_sub(wr,el), full?0.88:0.78));
          add(ball(cp, rWr+0.010, G.cuffOver?'overD':'shirtD', 0.12, 0.014));
        }
      }
      const d=v_norm(v_sub(wr,el));
      add(ball(v_add(wr, v_mul(d,0.024*hS)), 0.036*wS*PR.limbR, 'skin', -0.02, -0.03));
    }

    // ---------- NECK + HEAD ----------
    const HI = root.HeadIso;
    const hc = P.headC;
    const hb = Object.assign({}, b, { jaw:PR.jawK, neck:PR.neckK });
    add(limb(P.neckB, [hc[0],hc[1]-0.010, hc[2]-0.100*PR.headK],
      0.049*wS*PR.neckK, 0.043*wS*PR.neckK, 'skin', -0.24));
    if(HI) add(HI.facesOf(hc, hb, P.look || {gaze:[0,0],lid:0,brow:0,mouth:'neutral'}, wS, PR.headK));
    return F;
  }

  // ---- rasterizer (fleet recipe + tinted keyline) ----
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }
  function camBasis(opts){
    const dir=opts.dir||0, th=-dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) };
  }
  function projVert(x,y,z,B){
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:cx+xr*S, sy:cy-(yr*B.se+zr*B.ce)*S - (B.heave||0), d:(yr*B.ce-zr*B.se) };
  }
  const _hex=(c)=>[parseInt(c.slice(1,3),16),parseInt(c.slice(3,5),16),parseInt(c.slice(5,7),16)];
  const _mix=(a,b2,t)=>{ const A=_hex(a),B2=_hex(b2);
    return '#'+[0,1,2].map(i=>Math.round(A[i]+(B2[i]-A[i])*t).toString(16).padStart(2,'0')).join(''); };
  const _keyCache={};
  function keyTint(c){
    if(_keyCache[c]) return _keyCache[c];
    return (_keyCache[c]=_mix(KEY, c, 0.22));   // tinted outline: dark, but carries the local hue
  }
  function _paint(faces, opts, MATS, RINDEX, post){
    const B=camBasis(opts);
    const zbuf=new Float32Array(W*H).fill(Infinity);
    const col=new Array(W*H).fill(null);
    const dep=new Float32Array(W*H);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const M = MATS[f.mat] || MATS.shirt;
      const fidx = sh*GAIN*(M.gain==null?1:M.gain) + (M.bias==null?BIAS:M.bias) + (f.b||0);
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
          let w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          let w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          let w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d;
            let base=Math.floor(fidx), fr=fidx-base;
            // ordered dither is a HAIR texture, not a global effect: garments/skin snap to a step,
            // so flat panels read as clean colour blocks instead of a 50% transparency checker.
            let idx=base+(M.dith ? ((fr>BAYER[x&3][y&3])?1:0) : (fr>0.55?1:0))+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(!col[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!col[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){
          const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
          // ONE step, not two: with each material now holding only ~3 ramp steps, a 2-step drop at
          // every depth edge was a near-black outline through the middle of the sprite — it is what
          // put the grey checker across the collar and the dark notch beside the profile eye.
          if(e && e.i>0) col[far]=e.r[Math.max(0,e.i-1)];
        }
      }
    }
    if(post) post({col, dep, zbuf, W, H}, B);   // head rig stamps the face here
    const out=col.slice();
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){          // 1px keyline, tinted by what it wraps
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
  function _toRGBA(out){
    const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){
      const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16);
      rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255;
    }
    return rgba;
  }

  function counterLean(rollDeg, pitchDeg, gain){
    const k = (gain==null?1:gain);
    return { list:-(rollDeg||0)*k, lean:-(pitchDeg||0)*k*0.9 };
  }
  const DEFAULT_BUILD = { sex:'m', age:'adult', garment:'overalls', skin:'fair', hair:'blond',
    hairStyle:'mop', beard:'none', eyes:'sea', outfit:'teal', shirt:'white', hat:'none',
    hatCol:'char', apronCol:'canvas', face:'dash', eyeShape:'round', browShape:'natural',
    height:1.00, weight:1.00, headSize:1 };
  function resolveBuild(o){
    const src = o && o.build || {};
    const named = src.preset && BUILDS[src.preset] ? BUILDS[src.preset] : null;
    const b = Object.assign({}, DEFAULT_BUILD, named||{}, src);
    if(!b.browShape) b.browShape = b.sex==='m' ? 'thick' : 'natural';
    if(b.age==='child'){ b.beard='none'; }
    return b;
  }
  function resolveOpts(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    const b = resolveBuild(opts);
    const anim = opts.anim||'idle';
    const A = ANIMS[anim]||ANIMS.idle;
    const u = opts.u!=null ? opts.u : (((opts.frame||0)%A.frames+A.frames)%A.frames)/A.frames;
    const power = opts.power==='long' ? 'long' : 'short';
    const carry = (['buckets','tray','helm','oars'].indexOf(opts.carry)>=0) ? opts.carry : null;
    return { o:Object.assign({},opts,{dir}), b, anim, u, power, carry };
  }
  function render(dir, opts){
    const {o,b,anim,u,power,carry}=resolveOpts(dir,opts);
    const {MATS,RINDEX}=makeMats(b);
    const P=pose(anim,u,b,power,carry,o);
    const HI=root.HeadIso;
    const hb = Object.assign({}, b, { jaw:P.PR.jawK, neck:P.PR.neckK });
    const post = HI ? (px,B)=>HI.stamp(px, P.headC, hb, P.look, B, MATS, cx, cy, P.PR.headK) : null;
    return _toRGBA(_paint(facesOf(P, b), o, MATS, RINDEX, post));
  }
  function renderAt(px, dir, opts){
    const keep=[S,W,H,cx,cy];
    const HI=root.HeadIso, EI=root.EyeIso;
    setScale(px);
    if(HI&&HI.setScale) HI.setScale(px);
    if(EI&&EI.setScale) EI.setScale(px);
    try { return { rgba:render(dir,opts), W, H, cx, cy, px:S }; }
    finally {
      S=keep[0]; W=keep[1]; H=keep[2]; cx=keep[3]; cy=keep[4];
      if(HI&&HI.setScale) HI.setScale(PX);
      if(EI&&EI.setScale) EI.setScale(PX);
    }
  }
  function anchors(dir, opts){
    const {o,b,anim,u,power,carry}=resolveOpts(dir,opts);
    const P=pose(anim,u,b,power,carry,o), B=camBasis(o);
    const pt=(p)=>{ const v=projVert(p[0],p[1],p[2],B); return {x:v.sx, y:v.sy}; };
    return { handL:pt(P.arms.L.wrist), handR:pt(P.arms.R.wrist),
             head:pt([P.headC[0],P.headC[1],P.headC[2]+0.17*P.PR.headK]),
             hip:pt([P.swayX*0.5,0,P.hipZ]) };
  }
  function tool(dir, opts){
    const {o,b,anim,u,power}=resolveOpts(dir,opts);
    const P=pose(anim,u,b,power,null,o);
    if(!P.tool) return null;
    const B=camBasis(o), w=P.arms.R.wrist;
    const v=projVert(w[0]+0.005, w[1]+0.010, w[2]+0.012, B);
    return { grip:{x:v.sx, y:v.sy}, wrist:[w[0],w[1],w[2]], pitch:P.tool.pitch, yaw:P.tool.yaw, bend:P.tool.bend };
  }
  function carry(dir, opts){
    const {o,b,anim,u,power,carry:c}=resolveOpts(dir,Object.assign({carry:'buckets'},opts));
    const P=pose(anim,u,b,power,c,o), B=camBasis(o);
    const pj=(p)=>projVert(p[0],p[1],p[2],B);
    const wl=P.arms.L.wrist, wr=P.arms.R.wrist, vl=pj(wl), vr=pj(wr);
    const A=(anim==='run'?15:anim==='walk'?9:1.6)*DEG, lag=0.9;
    const swingR=A*Math.sin(2*Math.PI*u-lag), swingL=A*Math.sin(2*Math.PI*(u+0.5)-lag);
    if(c==='tray'){
      const mid=pj([(wl[0]+wr[0])/2,(wl[1]+wr[1])/2,(wl[2]+wr[2])/2]);
      return { mode:'tray', handL:{x:vl.sx,y:vl.sy}, handR:{x:vr.sx,y:vr.sy}, mid:{x:mid.sx,y:mid.sy},
               swing:(anim==='idle'?0.8*Math.sin(2*Math.PI*u):2.0*Math.cos(4*Math.PI*u))*DEG,
               behind:B.ct>0.05 };
    }
    const beh=(wx)=>(wx*B.stt + 0.012*B.ct) > 0.001;
    const md=pj([(wl[0]+wr[0])/2,(wl[1]+wr[1])/2,(wl[2]+wr[2])/2]);
    /* pass 6: helm and oars used to answer 'buckets'. The anchors were right and the label was a
       lie, and neither stance had the two-hand pin a wheel or a pair of oars is mounted on. */
    return { mode:(c==='helm'||c==='oars') ? c : 'buckets',
             handL:{x:vl.sx,y:vl.sy}, handR:{x:vr.sx,y:vr.sy}, mid:{x:md.sx,y:md.sy},
             swingL, swingR, behindL:beh(wl[0]), behindR:beh(wr[0]), behind:false };
  }
  /* Boarding contract for the engine. The clip plays IN PLACE (pivot on the dock-side ground the
     whole way); `landing` is the cell-px offset of the deck-side ground contact, so the re-seat on
     the last frame is one vector add and never a guess. `rail` is where the plant hand meets the
     rail — use it to sanity-check a railZ against a hull's actual sheer, or to spark a grip FX. */
  function boardMount(dir, opts){
    const {o,b,anim,u,power,carry:c}=resolveOpts(dir,opts);
    if(!BOARDING[anim]) return null;
    const P=pose(anim,u,b,power,c,o), B=camBasis(o), K=P.board;
    const pt=(p)=>{ const v=projVert(p[0],p[1],p[2],B); return {x:v.sx, y:v.sy}; };
    const wl=P.arms.L.wrist;
    return { railZ:K.railZ, requested:K.requested, clamped:K.clamped, rise:K.rise,
             /* a carried load wins over the rail plant in pose(), so the grip flag has to agree */
             phase:K.phase, handOn:c?0:K.handOn, gripped:!c && K.handOn>0.5, carried:!!c,
             hand:pt(wl), rail:pt([wl[0], K.handY, K.railZ]),
             leadFoot:pt(P.legs.R.ankle), trailFoot:pt(P.legs.L.ankle),
             landing:pt([0,0,K.railZ]) };
  }
  /* Ladder contract for the engine. The clip LOOPS and plays in place: z = 0 in the cell is the top
     of the rung the lower foot last planted on, and `descend` is how far the body has sunk below it
     at this frame (0 -> stepZ across the loop). Seat the sprite at that rung, subtract descend, and
     drop the rung reference by stepZ every time the loop wraps. `standoff` is how far the pivot
     sits off the ladder plane, so a face-mounted ladder needs no eyeballing. */
  function ladderMount(dir, opts){
    const {o,b,anim,u,power}=resolveOpts(dir,opts);
    if(!LADDER[anim]) return null;
    const P=pose(anim,u,b,power,null,o), B=camBasis(o), K=P.ladder, A=ANIMS.ladderDown;
    const pt=(p)=>{ const v=projVert(p[0],p[1],p[2],B); return {x:v.sx, y:v.sy}; };
    const sole=(a)=>pt([a[0], a[1], a[2]-P.ankleZ]);
    return { rung:K.rung, width:K.width, stepZ:K.stepZ, descend:+K.drop.toFixed(4),
             rate:+(K.stepZ/((A.frames*A.ms)/1000)).toFixed(3), standoff:0.275,
             moving:K.moving, contact:K.contact,
             handL:pt(P.arms.L.wrist), handR:pt(P.arms.R.wrist),
             footL:pt(P.legs.L.ankle), footR:pt(P.legs.R.ankle),
             rungL:sole(P.legs.L.ankle), rungR:sole(P.legs.R.ankle),
             airL:K.F.L.air, airR:K.F.R.air, handsOn:true,
             /* the ladder is a +y layer like a carried tray: it goes UNDER the sprite on the away
                facings and over it on the near ones. Same test the carry pins use. */
             ladderBehind: B.ct > 0.05,
             hip:pt([P.swayX*0.5,0,P.hipZ]),
             head:pt([P.headC[0],P.headC[1],P.headC[2]+0.17*P.PR.headK]) };
  }
  /* haul pins. The rope is a runtime line: draw it through handL/handR and out along `out`.
     tension 0..1 is the heave envelope — drive rope sag, a strain SFX or a winch tick off it. */
  function haulGrip(dir, opts){
    const src = (typeof opts==='number') ? {elev:opts} : (opts||{});
    const {o,b,u,power}=resolveOpts(dir,Object.assign({},src,{anim:'haul'}));
    const P=pose('haul',u,b,power,null,o), B=camBasis(o);
    const pt=(p)=>{ const v=projVert(p[0],p[1],p[2],B); return {x:v.sx, y:v.sy}; };
    const wl=P.arms.L.wrist, wr=P.arms.R.wrist;
    const md=[(wl[0]+wr[0])/2,(wl[1]+wr[1])/2,(wl[2]+wr[2])/2];
    const a=pt(md), f=pt([md[0], md[1]+0.50, md[2]+0.12]);
    const dx=f.x-a.x, dy=f.y-a.y, m=Math.hypot(dx,dy)||1;
    return { handL:pt(wl), handR:pt(wr), mid:a, lead:'R',
             tension:P.haul.pull, out:{x:dx/m, y:dy/m} };
  }
  function projectLocal(dir, p, elev){
    const v=projVert(p[0],p[1],p[2],camBasis({dir, elev}));
    return { x:v.sx, y:v.sy };
  }
  // total figure height in metres, for laying out a cast row on one baseline
  function metrics(b){
    const bb=resolveBuild({build:b}), PR=propsOf(bb);
    const P=pose('idle',0,bb,'short',null,{});
    return { top:P.headZ + 0.174*PR.headK, headZ:P.headZ, collarZ:P.collarZ, hipZ:P.hipZ0, PR };
  }

  const API = { get W(){return W;}, get H(){return H;}, PX, DIRS:8,
    get pivot(){ return {x:cx,y:cy}; }, setScale, renderAt, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    ANIMS, BUILDS, CAST, SKINS, HAIRS, OUTFITS, EYES, SHIRT, SHIRTS, HATCOLS, APRONS, BOOT,
    HAIRSTYLES, BEARDS, FACES, SEXES, SEX, AGES, AGE_ORDER, GARMENTS, GARMENT_ORDER, KEY,
    get HATS(){ return hatList(); }, hatList, HATS_LOCAL,
    ANIM_MOUNT, CARRIES, CARRY_ORDER, BOARDING, RAIL_DEF, RAIL_MIN, RAIL_MAX, railOf,
    LADDER, RUNG_DEF, LADW_DEF, rungOf, ladWOf, ladderMount, ladderCurve,
    boardMount, haulGrip, boardCurve, haulCurve,
    GROUPS, CAST_W1, CAST_S1, DEFAULT_BUILD,
    render, anchors, tool, carry, counter:counterLean, projectLocal, metrics, propsOf,
    facesOf, pose, makeMats, lathe, arcLathe, limb, torsoProf, GAIN, BIAS, LN, BAYER, pass:6, revision:'6.2',
    get head(){ return root.HeadIso || null; } };
  root.CharacterIso6 = API;
  if(!root.CharacterIso5) root.CharacterIso5 = API;   // drop-in when pass 5 is not loaded
  if(!root.CharacterIso)  root.CharacterIso  = API;
})(typeof globalThis!=='undefined'?globalThis:window);

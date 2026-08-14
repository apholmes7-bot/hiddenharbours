/* Hidden Harbours — CHARACTER HAND-PROP ANCHORS (pass 6.4, append-only add-on to
   Art/characterIsoRig6.js). Load AFTER characterIsoRig6.js. Exposes globalThis.CharacterHands6.

   WHY THIS FILE EXISTS
   Pass 6 mounts a carried object on the hands honestly — carry() and anchors() project the real
   wrists through the real camera, so the PINS are already per-facing. What was missing is the layer
   above that: for a given object, WHERE on the hand it sits, WHICH hand holds it at which heading,
   HOW it is angled so it reads at that heading, and WHETHER it draws over or under the sprite.
   That was written once per consumer page, in px, by eye, and it is why every held thing looked
   like it hung off one fixed hip offset at all eight facings.

   THE CONTRACT. One table per prop, in BODY-LOCAL METRES, not cell px:
     grip   {out, fwd, up}   offset from the wrist to the object's OWN pivot. out is signed by the
                             carrying hand, so one number serves both hands.
     pitch / yaw / bend      the object's rest attitude in its own local frame (rod convention:
                             +y = character forward, pitch 0 = level, 90 = up, >90 = laid back).
     facing{N..NW}           the eight art corrections. Each row may set yaw/out/fwd/up deltas,
                             swap (carry it in the other hand at this heading), turn (below) and
                             behind (force draw order). Everything defaults to 0/false, so a row
                             that needs no correction is absent and the table stays readable.
     itemScale               Draw scale for a prop borrowed from another use, 1 when it was baked
                             for the hand. Anything other than 1 is an art debt, not a setting.
     turn                    A PROP THAT WAS BAKED ON A TURNTABLE HAS NO YAW — it has eight cells,
                             and its heading is whichever one you draw. turn is that heading, in
                             45deg steps RELATIVE to the body: pin() returns itemDir, and the
                             consumer renders the prop's own rig at itemDir instead of dir. This is
                             what stops a cradled fish from pointing straight at the camera at N and
                             S, where a body-aligned catch foreshortens to a blob.
   Metres project through the rig's own camBasis, so one table is correct at any elev, any scale,
   and any deck-rock roll/pitch — and a 1 px nudge at 32 px/m stays a 1 px nudge at 48.

   WHAT THE TABLE CANNOT FAKE, and is therefore still engine/pose work:
   a true over-the-shoulder carry, or two hands closing on one object that pose() does not already
   close (rodTrail exists because a free arm hangs at the hip; a shoulder carry needs an arm raise
   inside pose(), i.e. a new carry stance in pass 6.5, not an anchor row).

   WHAT NEEDS NO NEW ART. The three "commission this" items on the animation list are already baked:
     rod    RodIso.render      pivot (56,72) = grip centre
     fish   FishIso.render     rest 'gill' / 'tail' / 'cradle' — gripU makes the GRIP the pivot
     clam   Shellfish.renderHandful  hpivot (11,8) = the grip
   Each one pins straight to pin().x/y with no offset. knife, gaff and rope-coil are the real art
   gap: knife and gaff have no rig at all, and RopeProps.render('coil') is a deck prop whose pivot
   is its bottom centre — pivotFix carries it to the grip until a carried coil is baked.

   API
     PROPS, PROP_ORDER, ORDER
     pin(prop, dir, opts)     -> { hand, x, y, dx, dy, yaw, pitch, bend, behind, hands, item, ... }
                                 x/y = cell px in the character cell; dx/dy = px from its pivot.
                                 opts is a CharacterIso6 opts bag (anim, frame/u, elev, build, ...).
     table(prop, opts)        -> the eight resolved rows, for a baker or a sheet.
     item(prop)               -> { rig, pivot, pivotFix } — how to mount the prop's own cell.  */
(function (root) {
  const DEG = Math.PI/180;
  const ORDER = ['N','NE','E','SE','S','SW','W','NW'];
  const C = () => root.CharacterIso6 || root.CharacterIso5 || null;
  /* half the torso's own width in metres — how far out a prop has to sit before the sprite stops
     covering it. Pass 6's shoulder ring is wider than this; the waist is the number that matters. */
  const TORSO_HALF = 0.115;

  /* ---------------------------------------------------------------------------
     THE PROP TABLE. Numbers are metres and degrees; 1 px = 1/32 m at bake scale.
     --------------------------------------------------------------------------- */
  const PROPS = {
    rodTrail: {
      label:'ROD · TRAIL', mount:'hand', hand:'R', hands:1, rig:'RodIso',
      grip:{ out:0.035, fwd:-0.015, up:-0.010 }, pitch:12, yaw:168, bend:0.06,
      /* A trailed rod is a 1.25 m line leaving the hand. At the profile headings it is nearly
         side-on and reads at full length, so it wants no help. At N and S it points into or out of
         the screen and collapses to a stub, so the heading swings outboard — the correction is
         biggest exactly where the foreshortening is. The hand never swaps: a rod is long enough to
         read from the far hand, and a rod that changes hands mid-turn reads as two rods. */
      facing:{ N:{yaw:-34, out:0.02}, NE:{yaw:-21}, E:{yaw:-5}, SE:{yaw:-11},
               S:{yaw:-25, fwd:0.02}, SW:{yaw:-15}, W:{yaw:-4}, NW:{yaw:-24} },
      rest:{ tier:'coast' },
      note:'rides idle / walk / run / board — free arms, no pose change' },

    rodSling: {
      label:'ROD · SLUNG', mount:'back', hands:0, rig:'RodIso',
      /* Not a hand prop: the anchor is the mid-back, so this one rides EVERY clip, including dig,
         haul and the ladder, where both hands are committed. Diagonal across the shoulders, butt
         low on the off side. Draw order is the back's own, and it inverts against the hand props —
         under the sprite on the near facings, over it on the away ones. */
      grip:{ out:-0.055, fwd:-0.105, up:0.055 }, pitch:58, yaw:152, bend:0.03,
      facing:{ N:{yaw:8}, NE:{yaw:4}, E:{yaw:-6}, SE:{yaw:-10}, S:{yaw:-12}, SW:{yaw:-8},
               W:{yaw:-4}, NW:{yaw:4} },
      rest:{ tier:'coast' },
      note:'anchor = hip + 0.52 of the way to the head, pushed 0.105 m aft' },

    fish: {
      label:'FISH · GILL GRIP', mount:'hand', hand:'R', hands:'auto', rig:'FishIso',
      /* hands:'auto' — FishIso.hold(species,scale) returns 1 or 2 by mass. One hand takes the gill
         grip (rest 'gill', head up, tail swinging clear of the thigh); a two-hand fish is cradled
         across the body on the midpoint pin (rest 'cradle'). Unlike the rod, a fish is small enough
         to vanish behind the torso, so it swaps to the near hand on the three headings where the
         right wrist is on the far side of the body. */
      grip:{ out:0.020, fwd:0.030, up:-0.010 },
      /* turn is the whole reason the catch reads: at N and S a body-aligned fish is end-on, so it
         turns 90deg across the view; the diagonals take 45deg, mirrored, which keeps the head
         outboard instead of buried in the chest; the profiles already read at full length. */
      facing:{ N:{fwd:-0.02, out:0.055, turn:2}, NE:{out:0.02, turn:1}, E:{turn:0}, SE:{fwd:0.01, turn:1},
               S:{turn:2}, SW:{swap:true, turn:-1}, W:{swap:true, turn:0}, NW:{swap:true, turn:-1} },
      rest:{ species:'mackerel', rest:'gill', two:'cradle' },
      note:'gill grip 1-hand · cradle 2-hand at mass >= 2.2 kg (cod cradles, mackerel hangs)' },

    clam: {
      label:'CLAM · HANDFUL', mount:'hand', hand:'R', hands:1, rig:'Shellfish',
      /* A handful is three shells, 22x16, and at 32 px/m it is the smallest thing anyone carries —
         which makes it the most sensitive to the offset being wrong by a pixel. Held low and
         slightly forward, cupped: fwd is what sells the cup. hands:2 draws one handful per hand. */
      grip:{ out:0.015, fwd:0.040, up:-0.020 },
      facing:{ N:{fwd:-0.03, out:0.045}, NE:{fwd:-0.01, out:0.02}, S:{fwd:0.01},
               SW:{swap:true}, W:{swap:true}, NW:{swap:true} },
      rest:{ kind:'clam', variant:0 },
      note:'Shellfish.renderHandful — one per hand when hands:2' },

    knife: {
      label:'KNIFE', mount:'hand', hand:'R', hands:1, rig:null, stub:{ len:0.24, pitch:6 },
      /* NO RIG YET. chop already drives a blade through tool(); this row is the CARRIED state —
         blade forward and down, off the leg. The stub length is the brief: 0.24 m overall. */
      grip:{ out:0.020, fwd:0.050, up:0 }, pitch:8, yaw:22,
      facing:{ N:{yaw:-14, fwd:-0.03, out:0.04}, NE:{yaw:-8, out:0.02}, SE:{yaw:6}, S:{yaw:10},
               SW:{swap:true, yaw:-10}, W:{swap:true, yaw:-6}, NW:{swap:true, yaw:-12} },
      note:'needs a knife rig — pitch/yaw and grip are the spec' },

    gaff: {
      label:'GAFF', mount:'hand', hand:'R', hands:1, rig:null, stub:{ len:1.55, pitch:16, hook:true },
      /* NO RIG YET. Carried like the rod but heavier and blunter: less pitch, more sweep, and the
         hook end trails low. Same no-swap rule as the rod. */
      grip:{ out:0.030, fwd:-0.010, up:-0.010 }, pitch:16, yaw:162,
      facing:{ N:{yaw:-30, out:0.02}, NE:{yaw:-19}, E:{yaw:-4}, SE:{yaw:-10},
               S:{yaw:-22, fwd:0.02}, SW:{yaw:-13}, W:{yaw:-3}, NW:{yaw:-21} },
      note:'needs a gaff rig — 1.55 m pole, hook at the far end' },

    rope: {
      label:'ROPE COIL', mount:'hand', hand:'L', hands:1, rig:'RopeProps',
      /* RopeProps.render('coil') is a deck prop: 40x32, pivot (20,30) = bottom centre. pivotFix
         lifts that to the middle of the hank, which is where a hand takes it. Carried on the off
         side so the working hand stays free — hence hand 'L' and no swap. */
      grip:{ out:0.025, fwd:0.020, up:-0.015 }, itemScale:0.5,
      facing:{ N:{fwd:-0.03}, S:{fwd:0.02}, E:{out:0.01}, W:{out:0.01} },
      rest:{ kind:'coil' },
      /* itemScale is an ADMISSION, not a feature: the coil is a deck prop, ~0.75 m across, and
         half-scaling it is what makes it hand-sized at all. A carried hank wants its own bake —
         a 0.35 m flake with a visible bight over the thumb. This is the honest art ask. */
      note:'RopeProps coil at 0.5 scale — a carried hank is still real art' },
  };
  const PROP_ORDER = ['rodTrail','rodSling','fish','clam','knife','gaff','rope'];

  /* Where a prop's own cell pivot sits relative to the grip. Zero for every rig that was baked to
     pin (rod, fish, clam); non-zero only for a prop borrowed from another use. */
  const ITEM = {
    RodIso:    { pivot:'grip centre (56,72)',       pivotFix:{dx:0, dy:0} },
    FishIso:   { pivot:'gripU point (32,38)',       pivotFix:{dx:0, dy:0} },
    Shellfish: { pivot:'handful grip (11,8)',       pivotFix:{dx:0, dy:0} },
    RopeProps: { pivot:'bottom centre (20,30)',     pivotFix:{dx:-3, dy:-13} },
  };

  /* --- projection of a body-local offset to cell px ---------------------------
     The rig's projection is affine in (x,y,z), so a 3D OFFSET maps to a fixed px delta for a given
     camera — independent of the point it is applied to. That is what lets one metre table serve
     every frame of every clip. Kept in sync with characterIsoRig6.js camBasis/projVert, deck-rock
     roll/pitch included, so a prop stays in the hand while the boat moves under her. */
  function basis(o){
    const dir = o.dir||0, th = -dir*Math.PI/4;
    const D = C(), e = (o.elev!=null ? o.elev : (D ? D.defaultElev : 40))*DEG;
    const roll = (o.roll||0)*DEG, pitch = (o.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
             cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch) };
  }
  function scaleOf(){ const D = C(); return D ? D.PX*(D.W/64) : 32; }
  function offsetPx(off, B, S){
    const x1 = off[0]*B.cr + off[2]*B.sr, z1 = -off[0]*B.sr + off[2]*B.cr;
    const y2 = off[1]*B.cq - z1*B.sq,     z2 =  off[1]*B.sq + z1*B.cq;
    const xr = x1*B.ct - y2*B.stt, yr = x1*B.stt + y2*B.ct;
    return { dx:xr*S, dy:-(yr*B.se + z2*B.ce)*S, depth:yr };
  }
  function rowOf(P, dir){ return (P.facing && P.facing[ORDER[dir&7]]) || {}; }

  /* --- the one call an engine or a baker makes ------------------------------- */
  function pin(prop, dir, opts){
    const D = C(), P = PROPS[prop];
    if(!D || !P) return null;
    dir = ((dir|0)%8+8)%8;
    const o = (typeof opts==='number') ? {elev:opts} : Object.assign({}, opts||{});
    const row = rowOf(P, dir), B = basis(Object.assign({}, o, {dir})), S = scaleOf();
    const A = D.anchors(dir, o);

    let hand = P.mount==='hand' ? (row.swap ? (P.hand==='R'?'L':'R') : P.hand) : null;
    if(hand && (o.hand==='L' || o.hand==='R')) hand = o.hand;   // a consumer may force the hand
    let hands = P.hands, mode = P.mount;
    if(hands==='auto' && P.rig==='FishIso' && root.FishIso){
      const r = P.rest||{}, h = root.FishIso.hold(o.species||r.species, o.scale||1);
      hands = h.hands; mode = h.hands===2 ? 'cradle' : 'hand';
    }
    /* base pin, before the prop's own offset */
    let base, sx = 1;
    if(P.mount==='back'){
      const f = 0.52;
      base = { x:A.hip.x + (A.head.x-A.hip.x)*f, y:A.hip.y + (A.head.y-A.hip.y)*f };
    } else if(hands===2 || mode==='cradle'){
      base = { x:(A.handL.x+A.handR.x)/2, y:(A.handL.y+A.handR.y)/2 };
    } else {
      base = hand==='L' ? A.handL : A.handR;
      sx = hand==='L' ? -1 : 1;
    }
    const g = P.grip||{};
    const off = [ sx*((g.out||0) + (row.out||0)),
                  (g.fwd||0) + (row.fwd||0),
                  (g.up||0)  + (row.up||0) ];
    const d = offsetPx(off, B, S);
    /* DRAW ORDER. Depth alone is the wrong test, and it is the bug that made every small held item
       disappear at N: a fish 0.03 m forward of the wrist is behind the body PLANE at N, but the
       wrist is 0.215 m out and the torso is only ~0.115 m half-wide, so the fish sits clear of the
       silhouette in screen x and must draw OVER. So: behind only when the prop is both deeper than
       the body axis AND laterally inside the torso, where the sprite would really cover it.
       A back mount inverts on its own — its offset is aft, so the sign is already the answer. */
    const X = (P.mount==='back' ? 0 : sx*0.215) + off[0]*(P.mount==='back' ? 1 : 0);
    const Y = off[1] + (P.mount==='back' ? 0 : 0.012);
    const screenX = X*B.ct - Y*B.stt;
    const depth = X*B.stt + Y*B.ct;
    const behind = row.behind!=null ? row.behind
                 : (P.mount==='back' ? depth > 0.001
                                     : (depth > 0.001 && Math.abs(screenX) < TORSO_HALF));

    const yawDeg = (P.yaw||0) + (row.yaw||0);
    const turn = (row.turn||0) + (P.turn||0);
    return { prop, label:P.label, mode, hand, hands,
             itemDir:((dir + turn)%8+8)%8, turn,
             x:base.x + d.dx, y:base.y + d.dy, dx:d.dx, dy:d.dy,
             yaw:yawDeg, yawRad:yawDeg*DEG,
             pitch:P.pitch||0, pitchRad:(P.pitch||0)*DEG, bend:P.bend||0,
             behind, depth:+depth.toFixed(4), dir, facing:ORDER[dir], itemScale:P.itemScale||1,
             swapped:!!row.swap, item:item(prop), rig:P.rig, stub:P.stub||null,
             rest:Object.assign({}, P.rest), note:P.note };
  }

  /* the eight rows, rounded — what an art director reads and a baker bakes */
  function table(prop, opts){
    const rows = [];
    for(let d=0; d<8; d++){
      const p = pin(prop, d, opts); if(!p) return rows;
      rows.push({ dir:d, facing:ORDER[d], hand:p.hand||'-', swapped:p.swapped,
                  dx:Math.round(p.dx), dy:Math.round(p.dy),
                  yaw:Math.round(p.yaw), pitch:Math.round(p.pitch),
                  turn:p.turn, itemDir:p.itemDir,
                  order:p.behind ? 'under' : 'over' });
    }
    return rows;
  }
  function item(prop){
    const P = PROPS[prop];
    if(!P || !P.rig) return { rig:null, pivot:'— no rig baked —', pivotFix:{dx:0,dy:0} };
    return Object.assign({ rig:P.rig }, ITEM[P.rig] || { pivot:'?', pivotFix:{dx:0,dy:0} });
  }

  root.CharacterHands6 = { pass:6.4, ORDER, PROPS, PROP_ORDER, ITEM, TORSO_HALF, pin, table, item,
                           offsetPx, basis, scaleOf };
})(typeof globalThis!=='undefined' ? globalThis : window);

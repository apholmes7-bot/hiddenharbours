/* Hidden Harbours — ISO EYE rig (pass 5). Eyes and brows are their OWN layer, separate from the
   head, so they can be placed per facing and animated independently of it.
   ---------------------------------------------------------------------------------------------
   WHY SEPARATE, AND WHY A TABLE

   Pass 3 placed each eye by projecting a 3D anchor on the skull and rounding to a pixel. That is
   correct geometry and it still fails, because at ~10 px across the projection collapses. Measured
   anchor offsets from the face origin, in pixels, at elev 40:

       dir    left eye      right eye
       N      -2, 0         +1, 0        <- back of the head, yet placed as if facing us
       NE     -2,-1         +1,+1
       E       0,-1          0,+1        <- BOTH eyes on the same column: they stack
       SE     +1,-1         -2,+1
       S      +1, 0         -2, 0
       W       0,+1          0,-1        <- same collapse, mirrored

   At E and W the two anchors differ only in y, so the pair reads as one vertical smudge; at N they
   land face-forward and survive only because the z-buffer happens to bury them. No amount of
   tuning fixes that — 3 px of face cannot hold two projected eyes. So placement is AUTHORED: one
   socket per facing, in screen pixels relative to a projected face origin. The origin still comes
   from 3D, so the eyes track head bob, lean and the body rig's rotation; only the arrangement
   within the face is hand-set, which is what guarantees they read at all eight angles.

   THE SOCKET TABLE, read as a picture (o = face origin column, # = eye, . = gap):
       N    (none)                  facing away
       NE   (none)                  three-quarters away: no face to put eyes on
       E    ## . . . o              profile: a single 2 px eye
       SE   . ## . # . o            near eye 2 px, far eye compressed to 1 px
       S    ## . . ## .             two 2 px eyes with a 2 px nose bridge
       SW   mirror of SE
       W    mirror of E
       NW   (none)
   Eyes deliberately stop existing on the away-facing angles rather than being forced to read on
   all eight — a back view with eyes in it is worse than a back view without.

   ---------------------------------------------------------------------------------------------
   PASS 5 — THE DASH IS THE ONLY TREATMENT, AND IT IS ONE ROW.
   Pass 4 offered three treatments (dash / bright / soft) and drew the eye as a 2x2 cell: an ink
   lid row over an ink pupil beside a lit sclera pixel. Measured off a pixel dump of the S-facing
   sprite, that is THREE ink pixels out of four per eye — six ink pixels across a face nine pixels
   wide, in two adjacent rows. It does not read as two eyes; it reads as a mask. And the two colour
   treatments made it worse in a second way: an iris filling half of a 2 px eye is a LIT dot on a
   face, so 'bright' and 'soft' both broke the one rule the reference boards never break — the eye
   is the darkest mark on the head.

   So at 32 px/m an eye is a DASH with a MOVING PUPIL: an ink lash row w px wide, and beneath it one
   ink pixel of pupil beside one lit pixel of sclera. The lash stays put while only the pupil slides,
   which is what makes it read as the eye looking around rather than the head twitching — a
   whole-cell shift reads as a twitch. What pass 4 got wrong was not the pupil, it was the two colour
   treatments: an iris filling half of a 2 px eye is a LIT dot on a face, so 'bright' and 'soft' both
   broke the one rule the reference boards never break — the eye is the darkest mark on the head.
   There is one treatment now, and it is ink.

   Above 32 px/m (q >= 2) there is room for real structure, so the denser sprites get a ONE-pixel
   lash over a lit eye white with a q x q ink pupil. Sizing every feature by q instead keeps the
   ink-to-skin ratio constant and reproduces the same weight problem verbatim on every rung.

   THE BROW IS ONE PIXEL WIDER THAN THE EYE, OUTBOARD. Eye-aligned was too timid: a 2 px brow over a
   2 px eye is the same mark twice and the pupil has nothing to read against. It grows away from the
   nose, never toward it — the original objection to a 3 px brow was that a CENTRED one pushed its
   inner pixel into the 2 px nose-bridge gap and the two brows nearly met. On bare skin it is ink;
   where a fringe covers the brow row it takes the hair's darkest step instead, so it reads as the
   shadow under the fringe rather than as a floating slab, and it is present on every build.
   ---------------------------------------------------------------------------------------------
   ANIMATED SEPARATELY: gaze(t) and blink(t) are free-running generators owned here, so an eye can
   dart or blink without the head rig being involved at all. HeadIso.look() still drives the whole
   face; EyeIso.state() is the eye-only channel for when you want the eyes to live on their own
   clock (portraits, dialogue, a character watching something move past).

   EXPORTS globalThis.EyeIso = { SOCKETS, EYESHAPES, BROWSHAPES, BROWS,
     socketsFor(dir), draw(px, origin, dir, build, st, MATS), state(opts), gaze(t), blink(t) }.
   `origin` is {x, y, d} — the projected, rounded face-origin pixel and its depth.
   `st` is {gaze:[dx,dy], lid:0..1, brow:-1..1, raise:0|1}. */
(function (root) {
  const EYESHAPES  = ['round','narrow','droop','sharp','wide'];
  const BROWSHAPES = ['natural','flat','raised','angled','worried','thick','none'];

  /* BROW: ONE PIXEL WIDER than the eye it sits over, and the extra pixel always goes OUTBOARD.
     Eye-aligned makes the brow the same mark as the lash, so the pupil has nothing to read against;
     a CENTRED 3 px brow pushes its inner pixel into the nose-bridge gap and the pair nearly meet.
     Growing away from the nose gets the width without the unibrow. An arch cannot exist in three
     pixels, so this vocabulary is what three pixels CAN say: how high it rides (lift), which end
     drops (dy, inner to outer), how heavy it is (thick). */
  const BROWS = {
    natural:{ dy:[ 0,0], lift:1, thick:false },
    flat:   { dy:[ 0,0], lift:0, thick:false },
    raised: { dy:[ 0,0], lift:2, thick:false },
    angled: { dy:[ 1,0], lift:1, thick:false },
    worried:{ dy:[-1,0], lift:1, thick:false },
    thick:  { dy:[ 0,0], lift:1, thick:true  },
    none:   null,
  };

  /* One socket list per facing, fleet order N NE E SE S SW W NW.
     x0 = leftmost pixel of the eye cell, relative to the face origin column.
     out = which way is away from the face centre (drives iris rest position and brow tilt).
     far = the compressed eye in a 3/4 view; it gets no pupil, only a mark. */
  const SOCKETS = [
    [],                                                                 // N  — facing away
    [],                                                                 // NE — three-quarters away
    [{x0:-3, w:2, out:-1}],                                             // E  — profile
    [{x0:-2, w:2, out:-1}, {x0: 1, w:1, out: 1, far:true}],             // SE
    [{x0:-3, w:2, out:-1}, {x0: 1, w:2, out: 1}],                       // S
    [{x0:-1, w:1, out:-1, far:true}, {x0: 1, w:2, out: 1}],             // SW — mirror of SE
    [{x0: 2, w:2, out: 1}],                                             // W
    [],                                                                 // NW — three-quarters away
  ];
  const socketsFor = (dir)=> SOCKETS[((dir%8)+8)%8] || [];

  /* PIXEL SCALE. The sockets above are authored in pixels for a 32 px/m sprite. setScale lets the
     same authored eye render on a denser sprite by drawing q x q blocks on a q-scaled grid, so a
     2x sprite gets a 2x eye rather than a 1 px dot lost on a 20 px head. */
  let q = 1;
  function setScale(px){ q = Math.max(1, Math.round((px||32)/32)); return q; }

  // ---- eye-only procedural life, on its own clock ----
  function hash(n){ const x=Math.sin(n*127.1+311.7)*43758.5453; return x-Math.floor(x); }
  function gaze(t){
    let acc=0, i=0;
    while(acc<=t && i<4096){ const hold=0.72+1.05*hash(i*3.7); if(acc+hold>t) break; acc+=hold; i++; }
    const gx=[0,0,1,0,-1,0,1,-1,0,0][Math.floor(hash(i*5.3)*10)]||0;
    const gy=hash(i*8.9)>0.84 ? -1 : 0;
    return [gx,gy];
  }
  function blink(t){
    let acc=0, j=0;
    while(acc<=t && j<4096){
      const gap=2.4+2.0*hash(j*11.3), dur=0.11+0.05*hash(j*13.1);
      if(t>=acc+gap && t<acc+gap+dur) return 1;
      const dbl=hash(j*17.7)>0.78;
      if(dbl && t>=acc+gap+dur+0.09 && t<acc+gap+dur+0.09+dur*0.8) return 1;
      acc+=gap+dur+(dbl?0.09+dur*0.8:0); j++;
    }
    return 0;
  }
  function state(opts){
    opts = opts||{};
    if(opts.look) return Object.assign({gaze:[0,0], lid:0, brow:0, raise:0}, opts.look);
    const t = opts.t||0;
    return { gaze:gaze(t), lid:blink(t), brow:opts.brow||0, raise:opts.raise||0 };
  }

  /* Draw the eye layer into an already-z-buffered pixel buffer.
     px = {col, dep, zbuf, W, H} — the same buffers headIsoRig and characterIsoRig3 hold. */
  function draw(px, origin, dir, b, st, MATS){
    const socks = socketsFor(dir);
    if(!socks.length || !origin) return [];
    const W=px.W, H=px.H;
    const shape = b.eyeShape || 'round';
    const bshape = b.browShape || 'natural';
    const Bw = BROWS[bshape];
    /* put1 writes ONE pixel; put writes the q x q block the socket table is authored in. Features
       that should stay thin as density rises (a lash line, a brow) use put1 and size themselves in
       real pixels — scaling every feature by q keeps the ink-to-skin RATIO constant, which is how
       the 32 px/m mask band reappeared verbatim on the 80 px/m rung. */
    const colOf=(mat)=>{ const M=MATS[mat]; if(!M) return null;
      return M.ramp[Math.max(0,Math.min(M.ramp.length-1, M.idx==null?2:M.idx))]; };
    const put1=(x,y,mat)=>{
      const c=colOf(mat); if(!c) return;
      if(x<0||x>=W||y<0||y>=H) return;
      const i=y*W+x; if(!px.col[i]) return;               // never paint outside the silhouette
      px.col[i]=c; if(origin.d!=null) px.dep[i]=origin.d;
    };
    const put=(x,y,mat)=>{ for(let dy=0;dy<q;dy++) for(let dx=0;dx<q;dx++) put1(x+dx,y+dy,mat); };
    const skinSet={}; (MATS.skin && MATS.skin.ramp || []).forEach(c=>{ skinSet[c]=1; });
    const hairSet={}; (MATS.hair && MATS.hair.ramp || []).forEach(c=>{ hairSet[c]=1; });
    const onSkin=(x,y)=>{ if(x<0||x>=W||y<0||y>=H) return false; return !!skinSet[px.col[y*W+x]]; };
    const onHair=(x,y)=>{ if(x<0||x>=W||y<0||y>=H) return false; return !!hairSet[px.col[y*W+x]]; };

    const gx=(st.gaze&&st.gaze[0])|0, gy=(st.gaze&&st.gaze[1])|0, lid=st.lid||0;
    /* PASS 5.1 — SKIN-RELATIVE EYE. Ink carried the eye at every skin, and from bronze down the ink
       sits within a few L* of the face: deckboss / packer / girl had no eyes at any facing. On a dark
       skin the eye is carried by the SCLERA pixel (a true eye-white, MATS.sclera, supplied by the head
       stamp) with the ink pupil beside it; on a light skin the ink dash carries it and the sclera stays
       the skin's own lit step, exactly as before. */
    const lum=(hex)=>{ if(!hex||hex.length<7) return 1; const r=parseInt(hex.slice(1,3),16)/255, g=parseInt(hex.slice(3,5),16)/255, b2=parseInt(hex.slice(5,7),16)/255; return 0.2126*r+0.7152*g+0.0722*b2; };
    const skinRamp = (MATS.skin && MATS.skin.ramp) || [];
    const dark = lum(skinRamp[3]) < 0.40;     // same cut as the head stamp: umber, deep, ebony
    const SCL = (dark && MATS.sclera) ? 'sclera' : 'skinL';
    const drawn=[];
    for(const s of socks){
      const w = s.w, out = s.out;
      const x0 = origin.x + s.x0*q;
      const yTop = origin.y + (gy<0?q:0), yMid = yTop+q;
      drawn.push([x0, yTop, out]);

      // brow — aligned to the eye cell, asking for its height and settling lower under a fringe
      if(Bw){
        /* ONE ROW OF SKIN BETWEEN BROW AND LASH, WHERE THERE IS SKIN TO SPEND. Sitting the brow
           straight on the lash makes a two-row dark bar three pixels wide per eye — sunglasses, and
           it buries the pupil the row below is there to show. The guard underneath drops the brow
           back onto the lash when the row above is NOT bare skin, which is what happens under a
           fringe: there it becomes the shadow at the fringe's edge instead. 'flat' still rides low
           on purpose, and an expression's own lift still overrides. */
        const idle = !(st.raise|0) && !(st.brow||0);
        const baseLift = bshape==='flat' ? 0 : (bshape==='raised' ? 2 : 1);
        const lift = (idle ? baseLift : Bw.lift + (st.raise|0))*q;
        let by = yTop - q - lift;
        /* 5.1: under a fringe the brow is NOT drawn. Dropping it onto the lash made a two-row bar
           (sunglasses), and painting it in the hair's darkest step made a slab on black hair and
           nothing on white hair. A brow that is covered by hair is covered by hair. */
        if(by < yTop-q && !onSkin(x0, by) && !onSkin(x0+(w-1)*q, by)) by = -1;
        // thickness in REAL pixels, not q-blocks: a 3 px-deep brow over a 6 px eye is a scowl
        const bt = Bw.thick ? Math.max(2, Math.round(q*0.8)) : Math.max(1, Math.round(q*0.45));
        const bw = (w>=2) ? w+1 : w;                       // one pixel wider than the eye, outboard
        /* 5.2 TWO-PIXEL TILT. An expression used to move the inner brow pixel alone: a bar with a
           notch, and frown / worry / grit were told apart by the mouth. On a 3 px brow the inner end
           now goes one way and the OUTER end the other — a true diagonal across the brow (angry:
           inner down, outer up; worried: inner up, outer down) — without adding a pixel next to the
           lash. The 1 px far-eye brow keeps its whole-pixel step. */
        const tilt = st.brow>0 ? 1 : st.brow<0 ? -1 : 0;
        for(let i=0;i<bw && by>=0;i++){                    // i = 0 is the INNER end, toward the nose
          const cxp = out<0 ? x0+(w-1-i)*q : x0+i*q;
          let dy = Bw.dy[Math.min(i, Bw.dy.length-1)];
          if(tilt){ if(i===0) dy += tilt; else if(bw>=3 && i===bw-1) dy -= tilt; }
          const ty = Math.min(by+dy*q, yTop-q);
          /* INK ON SKIN, HAIR'S DARKEST STEP ON HAIR. The body rig used to hand this layer a brow
             material taken from the MIDDLE of the hair ramp and stamp it onto the fringe — a brow
             painted in hair, on hair, invisible on every build in the sheet. Full ink on a fringe is
             the opposite failure: a hard bar floating on the hairline. The hair's own darkest step
             reads as the shadow under the fringe, which is where a brow lives anyway. */
          const mat = onSkin(cxp, ty) ? 'brow' : null;   // 5.1: never on hair (see above)
          if(!mat) continue;
          for(let k=0;k<bt;k++) for(let j=0;j<q;j++) put1(cxp+j, ty+q-1-k, mat);
        }
      }

      /* ---- THE EYE at 32 px/m: a lash row, then a pupil you can watch move ----
         An ink lash w px wide, and beneath it ONE ink pixel of pupil beside one lit pixel of sclera.
         Only the pupil slides with gaze; the lash staying put is what makes it read as the eye
         looking around. At rest the pupil sits inboard so the pair converge on the viewer. */
      if(q===1){
        if(lid>=0.5){
          /* closed: ONE soft ink row where the lash was, and the row above is left as the mesh shaded
             it. Pass 5 put a lit pixel over the ink row, which is the open-eye pattern (sclera over
             pupil) and read as an eye looking down, on every blink and all through the sleep clip. */
          for(let i=0;i<w;i++) put1(x0+i, yMid, 'lash2');
          continue;
        }
        for(let i=0;i<w;i++) put1(x0+i, yTop, 'lash');
        if(s.far || w===1){ put1(x0, yMid, dark ? SCL : 'lash2'); continue; }   // compressed far eye
        /* 5.2: no smile squint at q === 1. The eye is two rows, and the only lower-lid pixel is the
           sclera; lifting it is invisible on a light skin (sclera == skin +2) and blinds a dark one.
           The smile is carried by the mouth's second value at this scale. */
        const inIdx = out<0 ? w-1 : 0;
        let ii = inIdx;
        if(gx<0) ii = 0; else if(gx>0) ii = w-1;
        for(let i=0;i<w;i++) put1(x0+i, yMid, (i===ii) ? 'lash2' : SCL);
        const oI = out<0 ? 0 : w-1;
        if(shape==='narrow' && ii!==oI) put1(x0+oI, yMid, 'lash');
        if(shape==='droop') put1(x0+oI, yMid+1, 'lash2');
        if(shape==='sharp') put1(x0+oI+(out<0?-1:1), yTop, 'lash');
        continue;
      }

      /* ---- q >= 2: room for internal structure — but sized in PIXELS, not in q-blocks ----
         Scaling the whole 2x2 arrangement by q keeps the ink ratio at three quarters of the cell,
         so the mask band reappeared identically on the 48 / 64 / 80 px/m rungs. Here the lash is
         ONE pixel however dense the sprite, the eye white is the rest of the cell, and the pupil is
         a q x q block — the only feature that should grow with resolution. */
      const cw = w*q, ch = 2*q;
      if(lid>=0.62){ for(let i=0;i<cw;i++) put1(x0+i, yTop+q, 'lash2'); continue; }
      if(lid>=0.30){ for(let i=0;i<cw;i++) put1(x0+i, yTop+q, 'lash');  continue; }
      for(let i=0;i<cw;i++) put1(x0+i, yTop, 'lash');                   // 1 px upper lash
      // the OPENING is q rows, not 2q: the lower half of the authored cell stays cheek, so the eye
      // white cannot spread into a flat bright patch the way a full-cell fill does
      for(let y=1;y<=q;y++) for(let i=0;i<cw;i++) put1(x0+i, yTop+y, SCL);
      if(s.far || w===1){ for(let y=1;y<=q;y++) for(let i=0;i<cw;i++) put1(x0+i, yTop+y, dark ? SCL : 'lash2'); continue; }

      /* pupil — only THIS block moves with gaze; the lash staying put is what makes it read as the
         eye looking rather than the head twitching. At rest it sits inboard so the pair converge
         on the viewer. It is INK, never iris colour: a saturated block filling half an eye reads
         as a lit dot on the face, and on the boards the eye is the darkest mark on the head. */
      let pxx = (out<0) ? x0+cw-q : x0;
      if(gx<0) pxx = x0; else if(gx>0) pxx = x0+cw-q;
      for(let y=1;y<=q;y++) for(let i=0;i<q;i++) put1(pxx+i, yTop+y, 'lash2');
      const oX = out<0 ? x0 : x0+cw-1;
      /* 5.2 SMILE SQUINT (q >= 2 only): the outer half of the bottom opening row lifts to skin — the
         lower lid pushed up by the cheek. The pupil block sits inboard, so it is never covered. */
      if(st.cheek) for(let i=0;i<Math.floor(cw/2);i++) put1(oX+(out<0?i:-i), yTop+q, 'skinD');
      if(shape==='narrow') for(let i=0;i<cw;i++) put1(x0+i, yTop+q, 'lash');
      if(shape==='droop')  for(let i=0;i<q;i++) put1(oX+(out<0?i:-i), yTop+q+1, 'lash2');
      if(shape==='sharp')  for(let i=1;i<=q;i++) put1(oX+(out<0?-i:i), yTop, 'lash');
    }
    return drawn;
  }

  root.EyeIso = { SOCKETS, EYESHAPES, BROWSHAPES, BROWS, socketsFor, draw, state, gaze, blink, setScale,
    get scale(){ return q; }, pass:5, revision:'5.2' };
})(typeof globalThis!=='undefined'?globalThis:window);

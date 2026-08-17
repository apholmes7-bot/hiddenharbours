/* Hidden Harbours — TALK CUES for characterIsoRig. The second half of the bubble ask: what the
   SPEAKER's body does through a line. Companion to Art/dialogueBubbleRig.js.

   WHAT THIS IS NOT: a new animation set. Baking talk clips for 8 facings x N frames buys a
   re-bake and a sheet, and the read it buys is a 1 px lift. This file is a CUE LAYER instead —
   integer screen offsets and a marker, applied to the rig's existing idle at draw time.

   WHY THAT IS HONEST AT EIGHT FACINGS (ADR 0034: rows are GROUND bearings, never screen ones):
   the bounce is a VERTICAL SCREEN offset, and screen-vertical is the one direction that does not
   rotate with the ground bearing — so one cue is correct at all eight facings with nothing
   authored per row and no screen-bearing row invented. Offsets are whole pixels: the sprite is
   blitted, never scaled, so no resample and no half-pixel anywhere.

   WHAT THE RIG ALREADY HAS (checked, 2026-08-17 — worth knowing before anyone opens a rig PR):
   the MOUTH cadence exists. characterIsoRig6.pose() forwards its opts to HeadIso.look() as the
   `rock` argument, so CharacterIso6.render(dir, {t, talk:true, expr}) already flips the mouth on
   the head rig's ~7 Hz jittered cadence (headIsoRig3.js freeLook). No rig edit is needed for the
   talking mouth. charOpts() below packs that call so a consumer never has to know it.

   THE THREE EMOTE MARKERS are AC's grammar (surprise / laugh / think), drawn as a small bubble
   from the SAME kit as the speech bubble — same stock, same ink ring, same chamfer — so a marker
   is visibly the speech bubble's small sibling and not a second UI language. 2-frame pop.

   Exposes globalThis.TalkCue. Optional dependency: BubbleKit (for the marker's panel). */
(function (root) {
  'use strict';

  /* ---- the cadence table. ms, not frames: the cue rides a clock, not a baked sheet ---- */
  const CUE = {
    beatMs: 385,        // 2.6 Hz — a spoken phrase beat, measured against the head rig's 7.2 Hz mouth
    liftFrac: 0.45,     // fraction of the beat spent 1 px up
    lift: 1,            // px. One. At 32 px/m a 2 px talk bounce is a nod, not speech.
    emphasisEvery: 4,   // every 4th beat holds the lift a beat-quarter longer
    emphasisFrac: 0.70,
    settleMs: 210,      // one last lift, then still
    startMs: 90,        // the body leads the first character by this much
  };
  const EMOTE = {
    surprise: { label: 'SURPRISE', glyph: 'bang', expr: 'oh',    ms: 620, hop: [[0, -2], [130, -1], [240, 0]],
      note: 'The hop is the read; the marker only confirms it.' },
    laugh:    { label: 'LAUGH',    glyph: 'burst', expr: 'grin', ms: 900, hop: [[0, -1], [110, 0], [220, -1], [330, 0], [440, -1], [550, 0]],
      note: 'Four bobs on the beat — the only cue that repeats.' },
    think:    { label: 'THINK',    glyph: 'dots',  expr: 'worry', ms: 1100, hop: [[0, 0]],
      note: 'Stillness IS the cue. The body stops bouncing and the dots do the work.' },
  };
  const EMOTE_ORDER = ['surprise', 'laugh', 'think'];

  /* ---- marker glyphs, 5 px tall so they sit in the kit's own text cell ---- */
  const GLYPH = {
    bang:  { w: 3, h: 5, mask: ['.#.', '.#.', '.#.', '...', '.#.'] },
    burst: { w: 5, h: 5, mask: ['#...#', '.#.#.', '..#..', '.#.#.', '#...#'] },
    dots:  { w: 7, h: 5, mask: ['.......', '.....##', '..##.##', '##.##..', '##.....'] },
  };

  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;

  /* ============================ the body cue ============================
     body({t, talking, startedAt, stoppedAt, emote, emoteAt}) -> { dy, beat, phase, state }
     t / *At are SECONDS. dy is a whole-pixel screen offset to add to the sprite's blit. */
  function body(o) {
    o = o || {};
    const t = o.t || 0;
    let dy = 0, state = 'still', beat = 0, phase = 0;

    if (o.emote && EMOTE[o.emote]) {
      const E = EMOTE[o.emote], ms = (t - (o.emoteAt || 0)) * 1000;
      if (ms >= 0 && ms < E.ms) {
        let v = 0;
        for (const [at, d] of E.hop) if (ms >= at) v = d;
        return { dy: v, beat: 0, phase: clamp(ms / E.ms, 0, 1), state: 'emote:' + o.emote, expr: E.expr };
      }
    }
    if (o.talking) {
      const ms = (t - (o.startedAt || 0)) * 1000 + CUE.startMs;
      beat = Math.floor(ms / CUE.beatMs);
      phase = (ms % CUE.beatMs) / CUE.beatMs;
      const frac = (beat % CUE.emphasisEvery === 0) ? CUE.emphasisFrac : CUE.liftFrac;
      dy = ms > 0 && phase < frac ? -CUE.lift : 0;
      state = 'talk';
    } else if (o.stoppedAt != null) {
      const ms = (t - o.stoppedAt) * 1000;
      if (ms >= 0 && ms < CUE.settleMs) { dy = ms < CUE.settleMs * 0.5 ? -CUE.lift : 0; state = 'settle'; }
    }
    return { dy, beat, phase, state, expr: o.expr || null };
  }

  /* the opts to hand CharacterIso*.render(dir, ...) — this is where the mouth channel lives */
  function charOpts(o) {
    o = o || {};
    const B = body(o);
    const expr = (B.expr && B.state.indexOf('emote') === 0) ? B.expr : (o.expr || null);
    const out = { t: o.t || 0, talk: !!o.talking, anim: o.anim || 'idle' };
    if (expr) out.expr = expr;
    return out;
  }

  /* ============================ the emote marker ============================
     Anchored on the rig's own head anchor (CharacterIso.anchors(dir).head), so it tracks every
     facing without a table. side: which shoulder it pops over — pass the side away from the tail. */
  function markerBox(kind) {
    const E = EMOTE[kind] || EMOTE.surprise, G = GLYPH[E.glyph];
    const pad = 3;
    return { w: G.w + pad * 2, h: G.h + pad * 2, glyph: G, pad };
  }
  function markerAt(anchor, o) {
    o = o || {};
    const kind = o.emote || 'surprise', B = markerBox(kind);
    const side = o.side === 'left' ? -1 : 1;
    const dx = o.dx == null ? (o.place === 'side' ? 9 : 5) : o.dx;
    const x = Math.round(anchor.x) + side * dx - (side < 0 ? B.w : 0);
    /* 'above' pops it over the head (no bubble up); 'side' sets it beside the head at anchor
       height, which is where it belongs while a speech bubble owns the space above. */
    const y = o.place === 'side'
      ? Math.round(anchor.y) - Math.round(B.h / 2)
      : Math.round(anchor.y) - (o.gap == null ? 4 : o.gap) - B.h;
    return { x, y, w: B.w, h: B.h };
  }
  /* 2-frame pop: frame 0 is the marker inset 1 px all round (it arrives small), frame 1 is full */
  function marker(ctx, x, y, kind, o) {
    o = o || {};
    const E = EMOTE[kind] || EMOTE.surprise, B = markerBox(kind);
    const K = root.BubbleKit;
    const t = o.t == null ? 1 : o.t;                  // 0..1 through the emote
    const pop = t < 0.12 ? 1 : 0;                     // one frame of small
    const w = B.w - pop * 2, h = B.h - pop * 2, X = x + pop, Y = y + pop;
    const stock = (K && K.STOCKS[o.stock || 'cream']) || null;
    if (K) K.panel(ctx, X, Y, w, h, { stock: o.stock || 'cream', chamfer: 1 });
    else { ctx.fillStyle = '#e8ebe5'; ctx.fillRect(X, Y, w, h); }
    const ink = stock ? stock.ink : '#232a32';
    const gx = X + Math.round((w - B.glyph.w) / 2), gy = Y + Math.round((h - B.glyph.h) / 2);
    ctx.fillStyle = ink;
    for (let r = 0; r < B.glyph.mask.length; r++) for (let c = 0; c < B.glyph.mask[r].length; c++)
      if (B.glyph.mask[r][c] === '#') ctx.fillRect(gx + c, gy + r, 1, 1);
    return { x: X, y: Y, w, h, kind, glyph: E.glyph };
  }

  /* ============================ the probe ============================
     House pattern: a MEASURED CLAIM BECOMES A TEST. The claim is "the talking mouth already works —
     render(dir,{t,talk:true}) reaches the head rig's cadence". probeMouth() proves it against the
     loaded rig by rendering the same frame twice at the same t, talk on and talk off: the ONLY
     difference between the two is the talk channel, so any changed pixel is the mouth. It also checks
     WHERE those pixels are (a few rows under the head anchor, i.e. the lower face) so the test cannot
     pass on an unrelated change. The import PR should run this and fail on it. */
  function probeMouth(rig, o) {
    o = o || {};
    if (!rig || !rig.render) return { pass: false, reason: 'no character rig loaded' };
    const dir = o.dir == null ? 4 : o.dir;
    const build = o.build || { preset: 'ginny' };
    const base = { elev: o.elev == null ? 40 : o.elev, anim: 'idle', frame: 0, build };
    const W = rig.W;
    let best = null;
    for (let i = 0; i < (o.samples || 14); i++) {
      const t = i / 7.2 + 0.07;
      const on = rig.render(dir, Object.assign({}, base, { t, talk: true }));
      const off = rig.render(dir, Object.assign({}, base, { t, talk: false }));
      let n = 0, y0 = 1e9, y1 = -1e9, x0 = 1e9, x1 = -1e9;
      for (let p = 0; p < on.length; p += 4) {
        if (on[p] !== off[p] || on[p + 1] !== off[p + 1] || on[p + 2] !== off[p + 2] || on[p + 3] !== off[p + 3]) {
          const px = (p / 4) % W, py = Math.floor((p / 4) / W);
          n++; if (py < y0) y0 = py; if (py > y1) y1 = py; if (px < x0) x0 = px; if (px > x1) x1 = px;
        }
      }
      if (n && (!best || n > best.px)) best = { px: n, t: +t.toFixed(3), bbox: [x0, y0, x1 - x0 + 1, y1 - y0 + 1] };
    }
    if (!best) return { pass: false, px: 0, reason: 'talk:true changed nothing — the channel is NOT reachable through render(); that is a real fix, not a rename' };
    const head = rig.anchors ? rig.anchors(dir, base).head : null;
    const rows = head ? [best.bbox[1] - Math.round(head.y), best.bbox[1] + best.bbox[3] - 1 - Math.round(head.y)] : null;
    const inFace = !rows || (rows[0] >= 0 && rows[1] <= 10);
    const small = best.px <= (o.maxPx || 48);
    return { pass: !!(inFace && small), px: best.px, t: best.t, bbox: best.bbox,
      rowsBelowHeadAnchor: rows, sprite: [rig.W, rig.H],
      note: (inFace ? 'changed pixels sit in the lower face' : 'changed pixels are NOT in the lower face — investigate before trusting the channel') +
        '; ' + (small ? 'and the change is mouth-sized' : 'but the change is too large to be a mouth') };
  }

  /* ============================ contract ============================ */
  function contract() {
    return {
      part: 'talk cues', version: 1, rig: 'talkCueRig.js', global: 'TalkCue',
      appliesTo: 'characterIsoRig6.js (pass 6.3) and any pass that keeps its anchors() contract',
      mechanism: 'draw-time integer screen offset + a marker. No new baked clip, no re-bake, no sheet.',
      facings: {
        count: 8, bearings: 'GROUND (ADR 0034) — untouched by this layer',
        why: 'the cue is a screen-VERTICAL offset, the one axis that does not rotate with the ground bearing, so one cue is correct at all eight rows with nothing authored per row',
        marker: 'anchored to CharacterIso.anchors(dir).head, which the body rig already resolves per facing',
      },
      body: Object.assign({}, CUE, {
        lift: CUE.lift + ' px — at 32 px/m a 2 px talk bounce reads as a nod, not as speech',
        settle: CUE.settleMs + ' ms: one last lift, then still — the line ending has to be visible without the text',
        integer: 'whole pixels only; the sprite is blitted, never scaled',
      }),
      mouth: {
        state: 'ALREADY SHIPPING — no rig edit needed',
        call: 'CharacterIso6.render(dir, {t, talk:true, expr}) — pose() forwards opts to HeadIso.look() as its `rock` argument',
        cadence: '~7.2 Hz jittered mouth flip (headIsoRig3.js freeLook)',
        drive: 'talk = (fill < 1). The mouth stops the instant the bubble finishes populating.',
        probe: 'TalkCue.probeMouth(CharacterIso6) — renders the same frame twice at one t, talk on and off, and asserts the diff is mouth-sized and sits in the lower face. The claim is a test; run it in the import PR and fail on it.',
      },
      emotes: EMOTE_ORDER.map(k => ({ key: k, label: EMOTE[k].label, expr: EMOTE[k].expr, ms: EMOTE[k].ms,
        hop: EMOTE[k].hop, glyph: EMOTE[k].glyph, box: markerBox(k).w + 'x' + markerBox(k).h, note: EMOTE[k].note })),
      exprMap: { surprise: 'oh', laugh: 'grin', think: 'worry (closest of the eight in headIsoRig3.EXPRS; a real "pondering" expression would be a head-rig ask, not a cue-layer one)' },
      markerMount: { anchor: 'anchors(dir).head', place: "'above' with no bubble up, 'side' (beside the head, centred on the anchor) while a speech bubble owns the space above", gap: 4, dx: '5 above / 9 beside', side: 'pass the side away from the bubble tail', pop: '2 frames \u2014 inset 1 px for the first ~12% of the emote, then full' },
      api: {
        body: 'body({t, talking, startedAt, stoppedAt, emote, emoteAt}) -> {dy, beat, phase, state}',
        charOpts: 'charOpts(o) -> the opts object to spread into CharacterIso*.render',
        marker: 'marker(ctx, x, y, kind, {t, stock})  ·  markerAt(headAnchor, {emote, side}) -> box',
        probeMouth: 'probeMouth(CharacterIso6, {dir, samples, maxPx}) -> {pass, px, t, bbox, rowsBelowHeadAnchor}',
      },
      openAsks: [
        'A one-line rig courtesy: characterIsoRig6 reaches the head talk channel through its `rock` argument. It works — probeMouth() proves it — but the name lies. Rename or alias it in the rig PR, KEEPING THE STABLE IDS: `rock` is the sixth positional argument of pose() and is read as arguments[5]; render() / pose() / anchors() signatures and the opts keys (t, talk, expr) must stay exactly as they are, or every consumer page and this kit break at once. Alias first, deprecate later.',
        'Emphasis beats are uniform every 4th beat. Punctuation-driven emphasis (a beat held on a comma) wants the line parsed, which belongs to the dialogue system, not to art.',
      ],
    };
  }

  root.TalkCue = { CUE, EMOTE, EMOTE_ORDER, GLYPH, body, charOpts, marker, markerAt, markerBox, probeMouth, contract, version: 2 };
})(typeof globalThis !== 'undefined' ? globalThis : window);

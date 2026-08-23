#!/usr/bin/env node
// THE ROD IS ONE ROD. This is the check that says so.
//
//   node tools/rig-recipes/rod-continuity.mjs              # the table, and a pass/fail exit code
//   node tools/rig-recipes/rod-continuity.mjs --tier deep  # one tier instead of all three
//   node tools/rig-recipes/rod-continuity.mjs --json       # the same measurements, machine-readable
//
// The owner's law, in his words: *no teleport, no hand change without an animated hand-over, no
// size change, no orientation change across any transition.* A rod that obeys it is a rod the
// player never sees flicker. The defect this file was written for was the opposite: the held rod
// and the cast rod were authored as two animations, and `rest:'ground'` / `rest:'stored'` were
// single cells with their own yaw and their own pivot meaning, so the rod jumped 2.3-3.9 px, swung
// up to 151 deg and changed apparent length by a third the instant it left the hand.
//
// WHAT IS MEASURED, and why each one is here:
//   · pivot   — where the GRIP CENTRE actually lands in the cell. The sprite pins the cell pivot
//               to the character's hand, so a grip drawn anywhere else is a teleport, full stop.
//   · length  — the tier's blank length in metres (the definition) AND the rendered ink's extent
//               in px (what the eye calls "size"). Both, because a rig can keep one and lose the
//               other, and the owner sees the second one.
//   · hand    — which hand holds the rod. A rest lets go; it must let go MID-ANIMATION, so that
//               the seam frame is still held and the release is something you can watch.
//   · angle   — the rod's axis on screen, plus the 3D pitch/yaw behind it.
//
// Every number comes from actually running the rigs and actually rendering the cells (node's V8 is
// the same engine ClearScript gives the in-editor baker — ADR 0021), never from a table beside
// them. The in-editor twin of this check is `RodContinuityTests`.
import { install } from './lib/rigHost.mjs';

// ---- tolerances -----------------------------------------------------------------------------
// A seam is allowed to be an ordinary animation step and nothing more. Pivot and blank length are
// held EXACT because nothing legitimate moves them; the ink and angle carry a small allowance
// because one frame of easing genuinely does move them, and the table prints each state's own
// largest per-frame step beside the seam so you can see the seam is the smaller number.
export const TOL = {
  pivotPx: 0.01,     // the grip is the cell origin in every state — this is exactness, not slack
  lenM: 0,           // the blank cannot get longer because the rod was put down
  inkPx: 2.0,        // rendered extent: one eased frame's worth
  angleDeg: 1.5,     // on-screen axis: likewise
  yawDeg: 0.01,      // one yaw for the whole rod, held or stowed
};

const DEG = 180 / Math.PI;
const FACINGS = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];

/** The five transitions the owner named, each as the pair of frames the player actually sees:
 *  the LAST frame of the state being left and the FIRST frame of the state being entered. */
export const TRANSITIONS = [
  { name: 'hold→cast',   from: ['hold', 1],        to: ['castBack', 0] },
  { name: 'cast→hold',   from: ['castRelease', 1], to: ['hold', 0] },
  { name: 'hold→ground', from: ['hold', 1],        to: ['rest:ground', 0] },
  { name: 'hold→stow-V', from: ['hold', 1],        to: ['rest:stowV', 0] },
  { name: 'hold→stow-H', from: ['hold', 1],        to: ['rest:stowH', 0] },
];

/** The tightest box containing any non-transparent pixel of a rendered cell, and how much ink is
 *  in it. This is the "size" the player sees — measured off the render, not inferred from a pose. */
export function ink(rgba, w, h) {
  let x0 = w, y0 = h, x1 = -1, y1 = -1, n = 0;
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    if (rgba[(y * w + x) * 4 + 3] === 0) continue;
    n++;
    if (x < x0) x0 = x; if (x > x1) x1 = x;
    if (y < y0) y0 = y; if (y > y1) y1 = y;
  }
  return n === 0 ? { n: 0, w: 0, h: 0, diag: 0 }
                 : { n, x0, y0, x1, y1, w: x1 - x0 + 1, h: y1 - y0 + 1,
                     diag: Math.hypot(x1 - x0 + 1, y1 - y0 + 1) };
}

/** One side of a seam, fully measured: the pose it resolves to, where that puts the grip and the
 *  tip, what the rendered cell actually contains, and whose hand it is in. */
export function measure(Rod, Char, state, u, dir, tier) {
  const rest = state.startsWith('rest:') ? state.slice(5) : null;
  if (rest && !Rod.RESTS[Rod.REST_ALIAS[rest] || rest]) return null;   // the state does not exist

  let opts, pose, handPx = null;
  if (rest) {
    opts = { tier, rest, u };
    pose = Rod.poseOf(rest, u, opts);
  } else {
    const t = Char.tool(dir, { anim: state, u });
    opts = { tier, pitch: t.pitch, yaw: t.yaw, bend: t.bend };
    pose = Rod.poseOf('held', null, opts);
    handPx = { x: +t.grip.x.toFixed(2), y: +t.grip.y.toFixed(2) };
  }

  const grip = Rod.gripLocal(opts);
  const off = Rod.project(dir, grip, Rod.defaultElev);
  const gx = Rod.pivot.x + off.dx, gy = Rod.pivot.y + off.dy;
  const tip = Rod.tip(dir, opts), tl = Rod.tipLocal(opts);
  const px = ink(Rod.render(dir, opts), Rod.W, Rod.H);

  return {
    state, u, dir, tier,
    gripX: +gx.toFixed(3), gripY: +gy.toFixed(3),
    offPx: +Math.hypot(gx - Rod.pivot.x, gy - Rod.pivot.y).toFixed(3),
    lenM: Rod.TIERS[tier].len,
    chordM: +Math.hypot(tl[0] - grip[0], tl[1] - grip[1], tl[2] - grip[2]).toFixed(4),
    inkDiag: +px.diag.toFixed(2), inkN: px.n,
    angleDeg: +(Math.atan2(-(tip.y - gy), tip.x - gx) * DEG).toFixed(3),
    pitchDeg: +(pose.pitch * DEG).toFixed(3),
    yawDeg: +(pose.yaw * DEG).toFixed(3),
    bend: +pose.bend.toFixed(4),
    liftM: +pose.lift.toFixed(4),
    hand: pose.hand,
    handPx,
  };
}

const dAngle = (a, b) => Math.abs(((b - a + 180) % 360 + 360) % 360 - 180);

/** The largest step this state takes between two of its OWN drawn frames — the yardstick a seam
 *  is held to. A seam wider than the animation it joins is a cut, however small the number is. */
function selfStep(Rod, Char, state, dir, tier) {
  const rest = state.startsWith('rest:') ? state.slice(5) : null;
  const n = rest ? Rod.REST_FRAMES : Char.ANIMS[state].frames;
  let ink = 0, ang = 0, prev = null;
  for (let f = 0; f < n; f++) {
    const m = measure(Rod, Char, state, rest ? f / (n - 1) : f / n, dir, tier);
    if (prev) {
      ink = Math.max(ink, Math.abs(m.inkDiag - prev.inkDiag));
      ang = Math.max(ang, dAngle(prev.angleDeg, m.angleDeg));
    }
    prev = m;
  }
  return { inkPx: +ink.toFixed(2), angleDeg: +ang.toFixed(2) };
}

export function run({ tiers = null } = {}) {
  install('rod'); install('character');
  const Rod = globalThis.RodIso, Char = globalThis.CharacterIso;
  const rows = [], problems = [];

  // ---- the two cross-rig pins ---------------------------------------------------------------
  // The rod rig states the hold stance it hands over from, and the yaw it holds every state at.
  // Neither is knowable from inside the rod rig alone, so both are pinned to the character rig
  // that actually drives them. This is the drift that produced the defect: the rests were written
  // at yaw 0 while every held frame the character rig drives is at yaw 16 deg.
  for (let dir = 0; dir < 8; dir++) {
    const exit = Char.tool(dir, { anim: 'hold', u: 1 });
    for (const [k, got] of [['pitch', exit.pitch], ['yaw', exit.yaw], ['bend', exit.bend]])
      if (Math.abs(got - Rod.STANCE[k]) > 1e-9)
        problems.push(`RodIso.STANCE.${k} is ${Rod.STANCE[k]} but CharacterIso.tool(${dir},` +
                      `{anim:'hold',u:1}).${k} is ${got} — the rests would leave the hand from a ` +
                      `stance the hand is not in.`);
    for (const anim of ['hold', 'bite', 'strike', 'reel', 'land', 'castBack', 'castRelease']) {
      const n = Char.ANIMS[anim].frames;
      for (let f = 0; f < n; f++) {
        const yaw = Char.tool(dir, { anim, frame: f }).yaw;
        if (Math.abs(yaw * DEG - Rod.HELD_YAW * DEG) > TOL.yawDeg) {
          problems.push(`${anim} dir${dir} f${f} is driven at yaw ${(yaw * DEG).toFixed(2)}deg but ` +
                        `RodIso.HELD_YAW is ${(Rod.HELD_YAW * DEG).toFixed(2)}deg — one rod, one yaw.`);
          f = n; // one report per anim is enough
        }
      }
    }
  }

  for (const tier of (tiers ?? Rod.order)) {
    for (const T of TRANSITIONS) {
      for (let dir = 0; dir < 8; dir++) {
        const a = measure(Rod, Char, T.from[0], T.from[1], dir, tier);
        const b = measure(Rod, Char, T.to[0], T.to[1], dir, tier);
        if (!b) { problems.push(`${T.name}: the state '${T.to[0]}' does not exist in the rig.`);
                  rows.push({ tier, pair: T.name, facing: FACINGS[dir], a, b: null }); continue; }

        const step = selfStep(Rod, Char, T.to[0], dir, tier);
        const d = {
          pivotPx: +Math.hypot(b.gripX - a.gripX, b.gripY - a.gripY).toFixed(3),
          offPx: Math.max(a.offPx, b.offPx),
          lenM: +Math.abs(b.lenM - a.lenM).toFixed(6),
          inkPx: +Math.abs(b.inkDiag - a.inkDiag).toFixed(2),
          angleDeg: +dAngle(a.angleDeg, b.angleDeg).toFixed(3),
          yawDeg: +Math.abs(b.yawDeg - a.yawDeg).toFixed(3),
          hand: a.hand === b.hand,
        };
        rows.push({ tier, pair: T.name, facing: FACINGS[dir], a, b, d, step });

        const at = `${tier} ${T.name} @${FACINGS[dir]}`;
        if (d.pivotPx > TOL.pivotPx) problems.push(`${at}: the grip TELEPORTS ${d.pivotPx} px.`);
        if (d.offPx > TOL.pivotPx) problems.push(`${at}: the grip is drawn ${d.offPx} px off the cell pivot the sprite pins by.`);
        if (d.lenM > TOL.lenM) problems.push(`${at}: the blank changes length by ${d.lenM} m.`);
        if (d.yawDeg > TOL.yawDeg) problems.push(`${at}: the rod is re-pointed ${d.yawDeg}deg in yaw.`);
        if (!d.hand) problems.push(`${at}: the hand changes ${a.hand}→${b.hand} AT the seam — a hand-over has to be animated, not cut.`);
        if (d.inkPx > Math.max(TOL.inkPx, step.inkPx))
          problems.push(`${at}: the rod changes size by ${d.inkPx} px, more than ${T.to[0]}'s own largest frame step (${step.inkPx} px).`);
        if (d.angleDeg > Math.max(TOL.angleDeg, step.angleDeg))
          problems.push(`${at}: the rod swings ${d.angleDeg}deg, more than ${T.to[0]}'s own largest frame step (${step.angleDeg}deg).`);
      }
    }
  }
  return { rows, problems };
}

function table(rows) {
  const h = ['tier', 'transition', 'facing', 'Δpivot px', 'Δlen m', 'Δink px', 'Δangle°', 'Δyaw°', 'hand', 'lift m'];
  const body = rows.map(r => r.b === null
    ? [r.tier, r.pair, r.facing, '—', '—', '—', '—', '—', 'ABSENT', '—']
    : [r.tier, r.pair, r.facing, r.d.pivotPx.toFixed(2), r.d.lenM.toFixed(3), r.d.inkPx.toFixed(2),
       r.d.angleDeg.toFixed(2), r.d.yawDeg.toFixed(2), `${r.a.hand}→${r.b.hand}`, r.b.liftM.toFixed(3)]);
  const w = h.map((c, i) => Math.max(c.length, ...body.map(r => r[i].length)));
  const line = (c) => '| ' + c.map((v, i) => v.padEnd(w[i])).join(' | ') + ' |';
  console.log(line(h));
  console.log('|' + w.map(n => '-'.repeat(n + 2)).join('|') + '|');
  for (const r of body) console.log(line(r));
}

const argv = process.argv.slice(2);
if (import.meta.url === `file://${process.argv[1]}`) {
  const tierAt = argv.indexOf('--tier');
  const { rows, problems } = run({ tiers: tierAt >= 0 ? [argv[tierAt + 1]] : null });
  if (argv.includes('--json')) {
    console.log(JSON.stringify({ rows, problems }, null, 1));
  } else {
    table(rows);
    console.log();
    if (problems.length === 0) {
      console.log(`✓ one rod, ${rows.length} seams: no teleport, no resize, no re-point, no cut hand-over.`);
    } else {
      console.log(`✗ ${problems.length} continuity failure(s):`);
      for (const p of problems) console.log(`  · ${p}`);
    }
  }
  process.exit(problems.length ? 1 : 0);
}

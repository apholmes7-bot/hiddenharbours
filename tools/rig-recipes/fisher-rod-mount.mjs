#!/usr/bin/env node
// Generates docs/art/rigs/gameplay/FisherRodMount.json from the rigs.
//
//   node tools/rig-recipes/fisher-rod-mount.mjs           # rewrite the sidecar
//   node tools/rig-recipes/fisher-rod-mount.mjs --check   # fail if the committed file has drifted
//
// This sidecar is the human-readable half of the rod mount — the one an artist or the owner opens
// to see how the rod pins to the fisher. It is DERIVED, never authored (the gameplay/ README's rule
// for any sidecar whose rig ships a generator), and it is regenerated here because the version it
// replaces was the clearest statement of the defect: it described `hold`, `cast_short` and
// `cast_long` as three overlays with three different cells (71x68, 107x91, 109x95) and three
// different rod pivots ((36,56), (54,56), (55,56)). Three cells and three pivots is three rods. A
// reader following it would pin the cast rod somewhere the held rod never was.
//
// What it says now: ONE cell, ONE pivot, one blank length per tier, and every state — the seven the
// character rig drives, then the three rests — as a pose curve on that one rod.
import fs from 'node:fs';
import path from 'node:path';
import { REPO } from './lib/csharp.mjs';
import { install, rigSha256 } from './lib/rigHost.mjs';

const OUT = 'docs/art/rigs/gameplay/FisherRodMount.json';
const HELD = ['hold', 'bite', 'strike', 'reel', 'land', 'castBack', 'castRelease'];
const r1 = (n) => Math.round(n * 10) / 10;
const r3 = (n) => Math.round(n * 1000) / 1000;

export function build() {
  install('rod'); install('character');
  const Rod = globalThis.RodIso, Char = globalThis.CharacterIso;
  const dirs = Char.DIRS;

  const poses = {};
  for (const state of [...HELD, ...Rod.REST]) {
    const isRest = Rod.REST.includes(state);
    const frames = isRest ? Rod.REST_FRAMES : Char.ANIMS[state].frames;
    const u = (f) => isRest ? f / (frames - 1) : f / frames;

    const grip = [], rodPose = [];
    for (let d = 0; d < dirs; d++) {
      const g = [], p = [];
      for (let f = 0; f < frames; f++) {
        // A rest's grip is the HOLD STANCE's grip, held for the whole hand-over: exact at frame 0
        // (that frame IS the seam) and an honest still after it, because the fisher has no
        // reach-down animation of her own yet. When she gets one, this reads from it instead.
        const t = Char.tool(d, isRest ? { anim: 'hold', u: 1 } : { anim: state, u: u(f) });
        const pose = isRest ? Rod.poseOf(state, u(f), {}) : Rod.poseOf('held', null, t);
        g.push({ x: r1(t.grip.x), y: r1(t.grip.y) });
        p.push({ pitch: r3(pose.pitch), yaw: r3(pose.yaw), bend: r3(pose.bend), hand: pose.hand });
      }
      grip.push(g); rodPose.push(p);
    }

    const entry = { frames };
    if (isRest) {
      entry.heldFrames = Rod.restHeldFrames();
      entry.liftM = {};
      for (const tier of Rod.order) entry.liftM[tier] = r3(Rod.restLift(state, { tier }));
    }
    entry.grip = grip;
    entry.rodPose = rodPose;
    poses[state] = entry;
  }

  return {
    build: 'fisher',
    derivedFromRigSha256: {
      'characterIsoRig.js': rigSha256('docs/art/rigs/characterIsoRig.js'),
      'rodIsoRig.js': rigSha256('docs/art/rigs/rodIsoRig.js'),
    },
    order: Char.order,
    bodyCell: { w: Char.W, h: Char.H },
    // ONE cell and ONE pivot, for every state. This is the whole correction: the pivot is the rod's
    // GRIP CENTRE in the held states, in the cast, on the ground and on the rack alike, so a
    // consumer that pins it to the fisher's hand never has to know which state it is in.
    rodCell: { w: Rod.W, h: Rod.H },
    rodPivot: { x: Rod.pivot.x, y: Rod.pivot.y },
    blankLenM: Object.fromEntries(Rod.order.map((t) => [t, Rod.TIERS[t].len])),
    heldYawRad: r3(Rod.HELD_YAW),
    stance: { pitch: r3(Rod.STANCE.pitch), yaw: r3(Rod.STANCE.yaw), bend: r3(Rod.STANCE.bend) },
    behindDirs: Rod.behind,
    note:
      'ONE ROD, EVERY STATE. Draw the body cell; place the rod overlay so rodPivot lands on ' +
      'grip[dir][frame]. rodCell/rodPivot/blankLenM/heldYawRad do not vary by state — that is the ' +
      'contract, and RodPresenterMath.SameRod plus tools/rig-recipes/rod-continuity.mjs both hold ' +
      'the shipped wiring to it. behindDirs -> rod UNDER body. Runtime line/bobber originate at ' +
      'RodIso.tipLocal(rodPose[dir][frame]) projected from the grip (RodIso.project); the baked ' +
      'blank stays in-cell and the FX sell the distance. The last three poses are the rests: they ' +
      'are ANIMATED hand-overs, not props — frame 0 is the hold stance the rod left the hand from, ' +
      'hand says who is holding it that frame, and liftM is how high the settled rod holds its ' +
      'grip above the ground or rack (a placement datum, never a pixel offset).',
    poses,
  };
}

const json = JSON.stringify(build(), null, 1) + '\n';
if (process.argv.includes('--check')) {
  const on = fs.readFileSync(path.join(REPO, OUT), 'utf8').replace(/\r\n/g, '\n');
  if (on !== json) {
    console.error(`${OUT} has drifted from the rigs. Re-run: node tools/rig-recipes/fisher-rod-mount.mjs`);
    process.exit(1);
  }
  console.log(`✓ ${OUT} still describes the rigs it names.`);
} else {
  fs.writeFileSync(path.join(REPO, OUT), json);
  console.log(`wrote ${OUT}`);
}

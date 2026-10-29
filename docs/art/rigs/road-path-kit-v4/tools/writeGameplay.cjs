#!/usr/bin/env node
/* Road kit v4: write every gameplay sidecar from the kit. Node 18, built-ins only. Run from the kit's root:
     node tools/writeGameplay.cjs          writes gameplay/*.gameplay.json
     node tools/writeGameplay.cjs --dry    returns them without writing (the checker uses this) */
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm');
const ROOT = path.resolve(__dirname, '..'), P = (f) => path.join(ROOT, f);
const LIBS = ['lib/pixelLanguage.js', 'lib/pxKit.js', 'lib/pxKit2.js', 'lib/weatherSky.js', 'lib/terrainLight4.js', 'lib/pxKit8.js', 'lib/terrainLight5.js', 'roadPathRig4.js', 'roadScenes4.js'];
function load() { for (const f of LIBS) vm.runInThisContext(fs.readFileSync(P(f), 'utf8'), { filename: f }); return { RK: globalThis.RoadKit4, RS: globalThis.RoadScenes4 }; }
function build() {
  const { RK, RS } = load(), out = {};
  const classes = {}; for (const k of Object.keys(RK.CLASSES)) { const net = RK.network([{ id: 'sample.' + k, cls: k, pts: [[40, 200], [360, 200], [600, 120]] }], { w: 640, h: 320, seed: 7 }); classes[k] = RK.gameplay(net, { scene: 'class.' + k }).routes[0]; }
  out['gameplay/roadPathRig4.classes.gameplay.json'] = JSON.stringify({ schema: 'hidden-harbours/road-gameplay@1', rig: 'roadPathRig4', what: 'every class, one sample route (20 m straight, then a bend): its section, surface, foot sound, grip, kerb and deck data, water and snow', classes }, null, 1);
  const SC = RS.SCENES || RS; for (const key of ['stpeters', 'route91', 'wharfroad', 'finnigans']) { const sc = SC[key]; const net = RK.network(sc.routes, { w: 520, h: 450, seed: sc.seed || 7 }); out['gameplay/roadScenes4.' + key + '.gameplay.json'] = JSON.stringify(RK.gameplay(net, { scene: key }), null, 1); }
  return { out, RK, RS };
}
if (require.main === module) { const { out } = build(); if (!process.argv.includes('--dry')) { fs.mkdirSync(P('gameplay'), { recursive: true }); for (const [f, t] of Object.entries(out)) fs.writeFileSync(P(f), t); } console.log(Object.keys(out).length + ' sidecars'); }
module.exports = { build, LIBS };

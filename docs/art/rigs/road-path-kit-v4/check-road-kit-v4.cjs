#!/usr/bin/env node
/* Road kit v4: the checker. Node 18, built-ins only. Run from the kit's root:  node check-road-kit-v4.cjs   (last line: RESULT:) */
'use strict';
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const ROOT = __dirname, P = (f) => path.join(ROOT, f), sha = (b) => crypto.createHash('sha256').update(b).digest('hex');
const res = []; const ok = (n, p, d) => { res.push(!!p); console.log((p ? 'PASS ' : 'FAIL ') + n + (d ? ' \u2014 ' + d : '')); };
const W = require(P('tools/writeGameplay.cjs')), B = W.build(), RK = B.RK, RS = B.RS;
ok('roadPathRig4.js installs RoadKit4 with its API', RK && ['network', 'zones', 'paint', 'patchG', 'faces', 'relightFaces', 'composite', 'weather', 'gameplay', 'lift', 'spline'].every(k => typeof RK[k] === 'function') && RK.PPM === 32 && RK.ZS === 24.5, 'PPM ' + RK.PPM + ', ZS ' + RK.ZS);
ok('roadScenes4.js installs RoadScenes4 with the four review scenes', RS && ['stpeters', 'route91', 'wharfroad', 'finnigans'].every(k => (RS.SCENES || RS)[k]));
const want = ['footpath', 'alley', 'walk', 'lane', 'street', 'route', 'town', 'boardwalk', 'paved'];
ok('the nine classes, in their order (paved last, new)', JSON.stringify(Object.keys(RK.CLASSES)) === JSON.stringify(want), Object.keys(RK.CLASSES).join(', '));
const pv = RK.CLASSES.paved; ok('the paved street: 5.6 m asphalt at +0.04, centre and edge lines, group route', pv.surf === 'asphalt' && pv.w === 5.6 && pv.lift === 0.04 && pv.group === 'route' && pv.marks.centre === 'auto' && pv.marks.edge === 1);
ok('the street is gravel and no longer names Wharf Road', RK.CLASSES.street.surf === 'gravel' && !/Wharf Road/.test(RK.CLASSES.street.where), RK.CLASSES.street.where);
const wr = (RS.SCENES || RS).wharfroad.routes.find(r => r.id === 'nmc.wharf_road'); ok('the review scene\u2019s Wharf Road is paved', wr && wr.cls === 'paved');
let off = []; for (const [f, t] of Object.entries(B.out)) { if (!fs.existsSync(P(f)) || fs.readFileSync(P(f), 'utf8') !== t) off.push(f); }
ok('every gameplay sidecar rebuilds from the kit, byte for byte', !off.length, Object.keys(B.out).length + ' sidecars' + (off.length ? '; differ ' + off.join(', ') : ''));
const cl = JSON.parse(fs.readFileSync(P('gameplay/roadPathRig4.classes.gameplay.json'), 'utf8'));
ok('each class\u2019s sidecar has a section, a surface, a foot sound and a grip', want.every(k => cl.classes[k] && cl.classes[k].section.length && cl.classes[k].surface && cl.classes[k].foot && cl.classes[k].grip > 0), want.map(k => k + ' ' + (cl.classes[k] && cl.classes[k].grip)).join(', '));
ok('the boardwalk has a deck, the town street a kerb', cl.classes.boardwalk.deck && cl.classes.boardwalk.deck.z_m === 0.35 && cl.classes.town.kerb && cl.classes.town.kerb.z_m === 0.14);
const sums = fs.readFileSync(P('SHA256SUMS.txt'), 'utf8').trim().split('\n').map(l => l.match(/^([0-9a-f]{64})  (.+)$/)).filter(Boolean), miss = [], bad = [];
for (const [, h, f] of sums) { if (!fs.existsSync(P(f))) { miss.push(f); continue; } if (sha(fs.readFileSync(P(f))) !== h && !/\.png$/.test(f)) bad.push(f); }
ok('SHA256SUMS: every file there, every text file matches', !miss.length && !bad.length, sums.length + ' files' + (miss.length ? '; missing ' + miss.join(', ') : '') + (bad.length ? '; differ ' + bad.join(', ') : ''));
const libs = { 'lib/pixelLanguage.js': 1, 'lib/pxKit.js': 1, 'lib/pxKit2.js': 1, 'lib/weatherSky.js': 1, 'lib/terrainLight4.js': 1, 'lib/pxKit8.js': 1, 'lib/terrainLight5.js': 1 };
ok('every file the kit loads is inside the zip', W.LIBS.every(f => fs.existsSync(P(f))) && Object.keys(libs).every(f => sums.some(s => s[2] === f)));
ok('v3 is not in this zip: its names and globals stay the game\u2019s', !fs.existsSync(P('roadPathRig3.js')) && !globalThis.RoadKit3);
const n = res.filter(Boolean).length; console.log('RESULT: ' + (n === res.length ? 'PASS' : 'FAIL') + ' ' + n + '/' + res.length);

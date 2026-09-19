// check-trailer-return.cjs — Hidden Harbours, trailer kit return check (2026-09-18 barn-leaf fix).
// Usage: node check-trailer-return.cjs [path/to/trailerIsoRig.js]     (exit 0 = every group ok)
// No deps, no build, no network. Loads the rig the way the engine does, bakes with the rig's own
// rasteriser, and re-derives every number this return claims. Nine groups:
//   rigid-leaf     each barn leaf is ONE rigid rotation about the pin the sidecar declares, at TEN
//                  poses, not just the 255deg endpoint. Same fit and the same 1e-6 m bar as the baker.
//   leaf-profile   the leaf's top edge still carries the rolled-header profile it shipped with, and
//                  carries it UNCHANGED into the open pose (that is what makes it one rigid leaf).
//   roll-alive     the roof roll still shapes the FIXED body: the wall top at |x|=hw is pulled the
//                  full 0.065 m under the roof, the crown still reaches roofZ, flatbeds are untouched.
//   face-order     face counts per body, closed == open, and each leaf is one contiguous run of 72.
//   flatbeds       barnL/barnR still clamp to 0 — a flatbed mesh must not move on the door params.
//   handshake      width 2.44, kingpin set 0.90, deck 1.18, 255deg, pins, doorZ, headerZ, roofZ.
//   published      fresh painted_bbox (at rest and open, 4 bodies x 8 facings) vs the contract.
//   sheets         all 20 sheets present, IHDR size == cells x the body's cell.
//   sha            sha256(rig) == contract.rigSha256 == sidecar.derivedFromRigSha256, and every
//                  entry in SHA256SUMS.json hashes to what it says.
const fs = require('fs'), vm = require('vm'), path = require('path'), crypto = require('crypto');
const KIT = path.join(__dirname, 'docs/art/rigs/road-fleet-kit/trailers');
const SIDE = path.join(__dirname, 'docs/art/rigs/gameplay/vehicles/trailerIsoRig.trailers.gameplay.json');
const rigPath = process.argv[2] || path.join(KIT, 'trailerIsoRig.js');
const rigSrc = fs.readFileSync(rigPath, 'utf8');
const ctx = { console }; ctx.globalThis = ctx; ctx.window = ctx; vm.createContext(ctx); vm.runInContext(rigSrc, ctx);
const V = ctx.TrailerIso, G = V.G, ORDER = V.order;
const C = JSON.parse(fs.readFileSync(path.join(KIT, 'trailers.contract.json'), 'utf8'));
const S = JSON.parse(fs.readFileSync(SIDE, 'utf8'));

const groups = [], byName = {};
function A(g, ok, msg) { if (!byName[g]) { byName[g] = { n: 0, bad: [] }; groups.push(g); } byName[g].n++; if (!ok) byName[g].bad.push(msg); }
const near = (a, b, e) => Math.abs(a - b) <= (e == null ? 1e-9 : e);
const REEFERS = [['reefer28', 8.53], ['reefer53', 16.15]], SLOTS = [['barnL', -1], ['barnR', 1]];
const mesh = (o) => V.mesh(o);
const moved = (a, b) => { const idx = []; for (let i = 0; i < a.length; i++) if (a[i].v.length !== b[i].v.length ||
  a[i].v.some((p, k) => p.some((x, j) => Math.abs(x - b[i].v[k][j]) > 1e-9))) idx.push(i); return idx; };

// ---- rigid-leaf: fit ONE rotation about the declared pin, worst vertex residual, ten poses ----
let worstAll = 0, poseCount = 0;
for (const [body, L] of REEFERS) for (const [slot, sx] of SLOTS) {
  const shut = mesh({ body }), a = sx * 1.19, b = -L / 2 + 0.02;
  for (let k = 1; k <= 10; k++) {
    const pose = k / 10, open = mesh({ body, [slot]: pose }), idx = moved(shut, open);
    let s = 0, c = 0;
    for (const i of idx) shut[i].v.forEach((p, m) => { const q = open[i].v[m];
      s += (p[0] - a) * (q[1] - b) - (p[1] - b) * (q[0] - a); c += (p[0] - a) * (q[0] - a) + (p[1] - b) * (q[1] - b); });
    const ang = Math.atan2(s, c), C1 = Math.cos(ang), S1 = Math.sin(ang);
    let w = 0;
    for (const i of idx) shut[i].v.forEach((p, m) => { const q = open[i].v[m], du = p[0] - a, dv = p[1] - b;
      w = Math.max(w, Math.hypot(a + du * C1 - dv * S1 - q[0], b + du * S1 + dv * C1 - q[1], p[2] - q[2])); });
    worstAll = Math.max(worstAll, w); poseCount++;
    A('rigid-leaf', idx.length === 72, body + ' ' + slot + ' pose ' + pose + ': ' + idx.length + ' faces moved, want 72');
    A('rigid-leaf', w <= 1e-6, body + ' ' + slot + ' pose ' + pose + ': worst ' + w.toExponential(3) + ' m > 1e-6');
    const want = ((sx * pose * G.rf.doorDeg) % 360 + 540) % 360 - 180;   // barnL turns -, barnR +
    A('rigid-leaf', near(ang * 180 / Math.PI, want, 1e-9),
      body + ' ' + slot + ' pose ' + pose + ': fitted ' + (ang * 180 / Math.PI).toFixed(6) + ' deg, declared ' + want.toFixed(6));
  }
}

// ---- leaf-profile: the shipped rolled-header top edge, and the same edge when open ----
const wantDrop = 0.065 * Math.pow(1.16 / G.hw, 10) * Math.max(0, Math.min(1, (G.rf.doorZ[1] - G.rf.roofZ + 0.16) / 0.16));
for (const [body, L] of REEFERS) for (const [slot, sx] of SLOTS) {
  const shut = mesh({ body }), open = mesh({ body, [slot]: 1 }), idx = moved(shut, open);
  const tops = []; for (const i of idx) for (const p of shut[i].v) if (p[2] > G.rf.doorZ[1] - 0.02) tops.push(p);
  const inner = tops.reduce((u, v) => Math.abs(v[0]) < Math.abs(u[0]) ? v : u);
  const outer = tops.reduce((u, v) => Math.abs(v[0]) > Math.abs(u[0]) ? v : u);
  A('leaf-profile', near(inner[2], G.rf.doorZ[1]), body + ' ' + slot + ': inner stile top ' + inner[2] + ', want ' + G.rf.doorZ[1]);
  A('leaf-profile', near(outer[2], G.rf.doorZ[1] - wantDrop),
    body + ' ' + slot + ': hinge-stile top ' + outer[2] + ', want ' + (G.rf.doorZ[1] - wantDrop) + ' (the rolled header profile)');
  let zs = 9, zo = 9; for (const i of idx) { for (const p of shut[i].v) zs = Math.min(zs, G.rf.doorZ[1] - p[2]);
    for (const p of open[i].v) zo = Math.min(zo, G.rf.doorZ[1] - p[2]); }
  A('leaf-profile', near(zs, zo), body + ' ' + slot + ': leaf top sits at ' + (G.rf.doorZ[1] - zs) + ' shut but ' + (G.rf.doorZ[1] - zo) + ' open');
}

// ---- roll-alive: the fixed body still gets the roll ----
for (const body of ['reefer28', 'reefer53']) {
  let wall = -9, crown = -9;
  for (const f of mesh({ body })) for (const p of f.v) {
    if (near(Math.abs(p[0]), G.hw, 1e-12)) wall = Math.max(wall, p[2]);
    if (Math.abs(p[0]) < 1e-9) crown = Math.max(crown, p[2]);
  }
  A('roll-alive', near(wall, G.rf.roofZ - 0.065), body + ': wall top at |x|=' + G.hw + ' is ' + wall + ', want ' + (G.rf.roofZ - 0.065));
  A('roll-alive', near(crown, G.rf.roofZ), body + ': roof crown is ' + crown + ', want ' + G.rf.roofZ);
}
for (const body of ['flatbed28', 'flatbed53']) {
  let hi = -9; for (const f of mesh({ body })) for (const p of f.v) hi = Math.max(hi, p[2]);
  A('roll-alive', near(hi, G.fl.headZ), body + ': top of mesh ' + hi + ', want the headboard at ' + G.fl.headZ + ' (no roll below the band)');
}

// ---- face-order: counts, closed == open, one contiguous run per leaf ----
for (const [body, want] of [['flatbed28', 1633], ['flatbed53', 2950], ['reefer28', 1610], ['reefer53', 2433]]) {
  const shut = mesh({ body }), open = mesh({ body, barnL: 1, barnR: 1 });
  A('face-order', shut.length === want, body + ': ' + shut.length + ' faces, want ' + want);
  A('face-order', shut.length === open.length, body + ': face count changes when the doors open');
}
for (const [body] of REEFERS) for (const [slot] of SLOTS) {
  const idx = moved(mesh({ body }), mesh({ body, [slot]: 1 }));
  A('face-order', idx[idx.length - 1] - idx[0] === idx.length - 1, body + ' ' + slot + ': the leaf is not one contiguous face run');
}

// ---- flatbeds: the door params must still clamp to 0 ----
for (const body of ['flatbed28', 'flatbed53']) {
  const idx = moved(mesh({ body }), mesh({ body, barnL: 1, barnR: 1 }));
  A('flatbeds', idx.length === 0, body + ': ' + idx.length + ' faces moved on the barn params');
}

// ---- handshake: the load-bearing numbers, untouched ----
A('handshake', near(G.hw, 1.22) && near(G.kpSet, 0.90) && near(G.deckZ, 1.18), 'width/set/deck drifted');
A('handshake', Math.hypot(G.hw, G.kpSet) < 1.52, 'nose swing no longer clears the tractors 1.52 m gap');
A('handshake', G.rf.doorDeg === 255, 'doorDeg is ' + G.rf.doorDeg);
A('handshake', near(G.rf.doorZ[0], 1.30) && near(G.rf.doorZ[1], 3.92), 'doorZ is ' + JSON.stringify(G.rf.doorZ));
A('handshake', near(G.rf.headerZ, 3.92) && near(G.rf.roofZ, 4.06), 'headerZ/roofZ drifted');
for (const [body, L] of REEFERS) {
  const th = S.bodies[body].THRESHOLD;
  for (const t of th) if (typeof t.hinge[0] === 'number' && typeof t.hinge[1] === 'number')
    A('handshake', near(Math.abs(t.hinge[0]), 1.19) && near(t.hinge[1], -L / 2 + 0.02, 1e-6),
      body + ' ' + t.id + ': sidecar hinge ' + JSON.stringify(t.hinge) + ' is not [+-1.19, -L/2+0.02]');
  for (const t of th) A('handshake', t.swing_deg === G.rf.doorDeg, body + ' ' + t.id + ': sidecar swing_deg vs the rig');
}

// ---- published: fresh bake vs the contract's painted_bbox ----
function bboxOf(rgba, W, H) { let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (rgba[(y * W + x) * 4 + 3]) {
    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; } return [x0, y0, x1, y1]; }
for (const body of V.list()) {
  const cell = V.cellFor(body), rest = { body, paint: 'white', weather: 0.45 };
  const variants = { at_rest: rest, open: body.indexOf('reefer') === 0
    ? Object.assign({}, rest, { barnL: 1, barnR: 1 }) : Object.assign({}, rest, { headboard: false }) };
  for (const vk of Object.keys(variants)) for (let d = 0; d < 8; d++) {
    const fresh = bboxOf(V.render(d, variants[vk]), cell.W, cell.H).join(',');
    const pub = C.bodies[body].painted_bbox[vk][ORDER[d]].join(',');
    A('published', fresh === pub, body + ' ' + vk + ' ' + ORDER[d] + ': contract [' + pub + '] vs fresh [' + fresh + ']');
  }
}

// ---- sheets: present, and the size the cells imply ----
for (const s of C.sheets) {
  const p = path.join(KIT, s.file);
  if (!fs.existsSync(p)) { A('sheets', false, s.file + ': missing'); continue; }
  const b = fs.readFileSync(p), w = b.readUInt32BE(16), h = b.readUInt32BE(20);
  const cols = s.layout ? parseInt(s.layout, 10) : s.cells, rows = Math.ceil(s.cells / cols);
  const cell = V.cellFor(s.frames[0].pose.body);
  A('sheets', w === cell.W * cols && h === cell.H * rows,
    s.file + ': ' + w + 'x' + h + ', want ' + (cell.W * cols) + 'x' + (cell.H * rows));
  if (s.grid) A('sheets', s.grid[0] === w && s.grid[1] === h, s.file + ': contract grid ' + s.grid.join('x') + ' vs the file');
}

// ---- sha: the rig, the contract, the sidecar and the manifest all agree ----
const sha = (buf) => crypto.createHash('sha256').update(buf).digest('hex');
const rigSha = sha(Buffer.from(rigSrc, 'utf8'));
A('sha', rigSha === C.rigSha256, 'rig hashes ' + rigSha.slice(0, 12) + ', contract claims ' + String(C.rigSha256).slice(0, 12));
A('sha', S.derivedFromRigSha256 === C.rigSha256, 'sidecar sha vs contract');
const sums = JSON.parse(fs.readFileSync(path.join(__dirname, 'SHA256SUMS.json'), 'utf8'));
for (const rel of Object.keys(sums)) {
  const p = path.join(__dirname, rel);
  if (!fs.existsSync(p)) { A('sha', false, rel + ': in SHA256SUMS.json but not in the drop'); continue; }
  const got = sha(fs.readFileSync(p));
  A('sha', got === sums[rel], rel + ': ' + got.slice(0, 12) + ' vs manifest ' + String(sums[rel]).slice(0, 12));
}

// ---- report ----
let fails = 0;
for (const g of groups) { const q = byName[g], ok = q.bad.length === 0; if (!ok) fails++;
  console.log((ok ? 'ok   ' : 'FAIL ') + g.padEnd(14) + ' (' + (q.n - q.bad.length) + '/' + q.n + ')');
  q.bad.forEach(m => console.log('       ' + m)); }
console.log('     rigid-leaf worst residual ' + worstAll.toExponential(3) + ' m over ' + poseCount +
  ' leaf-poses, bar 1e-6 — leaf top edge ' + G.rf.doorZ[1] + ' -> ' + +(G.rf.doorZ[1] - wantDrop).toFixed(9) + ' m across the stiles');
// A NOTE, not an assertion: the other post-pose world-space pass in this rig. Out of scope for this
// job, named in the README. build()'s body-group suspension dz(y) runs after the hinge too, so at
// sus != 0 an OPEN leaf shears (it is rigid at sus 0, which is what the bake and the baker use).
let susWorst = 0;
for (const [body, L] of REEFERS) for (const [slot, sx] of SLOTS) {
  const shut = mesh({ body, sus: 1 }), open = mesh({ body, sus: 1, [slot]: 1 }), idx = moved(shut, open);
  const a = sx * 1.19, b = -L / 2 + 0.02; let s = 0, c = 0;
  for (const i of idx) shut[i].v.forEach((p, m) => { const q = open[i].v[m];
    s += (p[0] - a) * (q[1] - b) - (p[1] - b) * (q[0] - a); c += (p[0] - a) * (q[0] - a) + (p[1] - b) * (q[1] - b); });
  const ang = Math.atan2(s, c), C1 = Math.cos(ang), S1 = Math.sin(ang);
  for (const i of idx) shut[i].v.forEach((p, m) => { const q = open[i].v[m], du = p[0] - a, dv = p[1] - b;
    susWorst = Math.max(susWorst, Math.hypot(a + du * C1 - dv * S1 - q[0], b + du * S1 + dv * C1 - q[1], p[2] - q[2])); });
}
console.log('note build() body-group dz(y) is also post-pose: at sus=1 an open leaf shears ' +
  susWorst.toExponential(3) + ' m (0 at sus=0, the bake and baker pose). Named, NOT fixed in this job.');
console.log(fails ? fails + ' group(s) FAILED' : 'all ' + groups.length + ' groups ok');
process.exit(fails ? 1 : 0);

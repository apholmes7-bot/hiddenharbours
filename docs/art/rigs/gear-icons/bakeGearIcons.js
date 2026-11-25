/* GEAR ICONS bake (2026-11-20): catalog icons for the fishing gear that had no art.

   Run from the repository root:  node docs/art/rigs/gear-icons/bakeGearIcons.js

   Every piece is a solid on the shared turntable (deck-loop-kit/Art/isoSolid.js: 45deg steps, elev 40,
   light from the upper left, the 4x4 dither, depth-edge darkening, ringless), shaded down the fleet
   master ramps the tool rigs use (WOOD, STEEL, CORK, RUST, NAVY, GRAPH, TEAL, BRASS, ROPE, NET), so a
   gear icon sits beside the rod, the shovel and the catch plates in the same light.
   Authored in ICON pixels (1 unit = 1 px at the icon), not metres: an icon is a picture of the gear,
   sized to the 64 px card slot like the catch icons (~30-48 px across), not its world size.
   The rod is not modelled here: it is rodIsoRig.js's coaster tier, the same rig the hand mesh was made
   from, laid at a reading angle.

   Output: Assets/_Project/Art/Fishing/Icons/GearIcon_<leaf>.png, trimmed with a 1 px margin. */
const fs = require('fs'), path = require('path'), zlib = require('zlib');
const ROOT = process.cwd(), RIGS = path.join(ROOT, 'docs/art/rigs');
for (const f of ['deck-loop-kit/Art/isoSolid.js', 'rodIsoRig.js']) (0, eval)(fs.readFileSync(path.join(RIGS, f), 'utf8'));
const K = globalThis.IsoSolid, Rod = globalThis.RodIso;
const ICONS = 'Assets/_Project/Art/Fishing/Icons';

// ---- fleet master ramps (copied from the tool and boat rigs, no invented hues) ----------------------
const R = {
  wood:  ['#33271b', '#473627', '#5e4630', '#6b4f35', '#8a6a48'],
  ash:   ['#6f6040', '#8d7b53', '#a99567', '#c3af7f', '#d7c699'],          // doryIsoRig ROPE, light timber
  steel: ['#3a4148', '#565f66', '#7a858c', '#9fabb1', '#c3ced2'],
  graph: ['#101317', '#1d2127', '#2b323a', '#3d454e', '#525c63'],
  rust:  ['#4a100e', '#7c1a15', '#a8241b', '#cf3626', '#e2573c'],
  teal:  ['#0d3f3c', '#14554e', '#1c7367', '#2ba39a', '#49b8aa'],
  navy:  ['#0e1526', '#172644', '#223764', '#2f4c88', '#4166ac'],
  brass: ['#5a4318', '#7d6024', '#a68333', '#c9a548', '#e3c46a'],
  rope:  ['#4a3b22', '#66522f', '#84693d', '#a1844f', '#bd9f66', '#d6bb85'],
  net:   ['#16241d', '#22362b', '#2c4438', '#3f5e4f', '#5c7d6b'],
  plast: ['#33474f', '#476069', '#5f7d8a', '#86a3ad', '#a9c2c9'],
  float: ['#7a2e0c', '#a8461a', '#d0682a', '#e8893f', '#f2a95e'],
  yellow:['#6b5410', '#9c7d2c', '#bd9a3a', '#d4b04e', '#e6c86e'],
  white: ['#6e7672', '#949c97', '#b9c0ba', '#d6dbd5', '#eef0ea'],
  shell: ['#3d3a35', '#57534b', '#757064', '#948e7f', '#b1ab9b'],
};
const MATS = {}; for (const k in R) MATS[k] = { ramp: R[k] };

// ---- shapes ----------------------------------------------------------------------------------------
const U = 1 / 32;                                         // one icon pixel, in turntable metres
const P = (p) => [p[0] * U, p[1] * U, p[2] * U];
const xfOf = (rz) => K.chain((p) => [p[0], p[1], p[2]], K.rotZ(rz || 0));
function Box(c, h, mat, rz, b, db) { return K.box(P(c), P(h), mat, b, db, xfOf(rz)); }
function Bar(a, b2, r, mat, rz, b, db) {
  const xf = xfOf(rz);
  return K.bar(xf(P(a)), xf(P(b2)), Array.isArray(r) ? r.map(v => v * U) : r * U, mat, b, db);
}
function Tube(c, z0, z1, r0, r1, seg, mat, opts) {
  opts = Object.assign({}, opts || {}); const rz = opts.rz || 0, pre = opts.pre || ((p) => p);
  opts.xf = (p) => xfOf(rz)(pre(p));
  return K.tube(c[0] * U, c[1] * U, z0 * U, z1 * U, r0 * U, r1 * U, seg, mat, opts);
}
function render(faces, dir) { const W = 112, H = 112; return { W, H, rgba: K.paint(faces, { W, H, cx: 56, cy: 64, MATS, dir: dir || 0 }) }; }

const crcT = (() => { const t = new Uint32Array(256); for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; } return t; })();
const crc = (b) => { let c = 0xffffffff; for (const x of b) c = crcT[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
const chunk = (t, d) => { const l = Buffer.alloc(4); l.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); };
function writePng(file, w, h, rgba) {
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) { raw[y * (w * 4 + 1)] = 0; for (let x = 0; x < w * 4; x++) raw[y * (w * 4 + 1) + 1 + x] = rgba[y * w * 4 + x]; }
  const ih = Buffer.alloc(13); ih.writeUInt32BE(w, 0); ih.writeUInt32BE(h, 4); ih[8] = 8; ih[9] = 6;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), chunk('IHDR', ih), chunk('IDAT', zlib.deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]));
  return `${path.basename(file)} ${w}x${h}`;
}
function trim(rgba, W, H, m) {
  let x0 = W, x1 = -1, y0 = H, y1 = -1;
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (rgba[(y * W + x) * 4 + 3]) { x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); }
  const w = x1 - x0 + 1 + 2 * m, h = y1 - y0 + 1 + 2 * m, out = new Uint8ClampedArray(w * h * 4);
  for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) for (let k = 0; k < 4; k++) out[((y - y0 + m) * w + (x - x0 + m)) * 4 + k] = rgba[(y * W + x) * 4 + k];
  return { w, h, rgba: out };
}
const save = (leaf, img) => { const t = trim(img.rgba, img.W, img.H, 1); return writePng(path.join(ROOT, ICONS, `GearIcon_${leaf}.png`), t.w, t.h, t.rgba); };

// a treble hook hanging from (x,y,z) straight down the screen (-z), three bends round it
function treble(x, y, z, len, rz) {
  const F = Bar([x, y, z], [x, y, z - len], 0.5, 'steel', rz, 0.6);
  for (const a of [0, 2.1, 4.2]) {
    const dx = Math.cos(a) * 2.2, dy = Math.sin(a) * 2.2;
    F.push(...Bar([x, y, z - len], [x + dx, y + dy, z - len + 0.5], 0.5, 'steel', rz, 0.6));
    F.push(...Bar([x + dx, y + dy, z - len + 0.5], [x + dx * 1.1, y + dy * 1.1, z - len + 2.6], 0.5, 'steel', rz, 0.6));
  }
  return F;
}
function ring(x, y, z, r, rz) {   // a split ring seen side on: four short bars
  return [].concat(Bar([x - r, y, z], [x, y, z + r], 0.5, 'steel', rz, 0.5), Bar([x, y, z + r], [x + r, y, z], 0.5, 'steel', rz, 0.5),
    Bar([x + r, y, z], [x, y, z - r], 0.5, 'steel', rz, 0.5), Bar([x, y, z - r], [x - r, y, z], 0.5, 'steel', rz, 0.5));
}

// ---- the pieces ------------------------------------------------------------------------------------
const PIECES = {
  // two long ash shafts hinged like scissors, a toothed basket at the end of each
  oyster_tongs() {
    const rz = -0.42, F = [];
    F.push(...Bar([-24, -3.2, 1.2], [10, 0.6, 1.2], 1.0, 'ash', rz));
    F.push(...Bar([-24, 3.2, 2.6], [10, -0.6, 2.6], 1.0, 'ash', rz));
    F.push(...Box([10.5, 0, 1.9], [1.2, 1.2, 1.6], 'steel', rz, 0.4));               // the hinge pin
    for (const s of [-1, 1]) {
      const y0 = s * 1.6, z = s < 0 ? 1.2 : 2.6;
      F.push(...Bar([11, y0, z], [23, s * 4.2, z], 0.8, 'steel', rz));               // basket back
      for (let x = 13; x <= 23; x += 2.5)                                             // the teeth, inward
        F.push(...Bar([x, s * (1.6 + (x - 11) * 0.22), z], [x, s * 0.4, z + 0.6], 0.5, 'steel', rz, 0.5));
    }
    return render(F, 0);
  },
  // short handle, a crossbar, five bent tines curling up at the tips
  shellfish_rake() {
    const rz = -0.5, F = [];
    F.push(...Bar([-20, 0, 1.4], [8, 0, 1.4], 1.2, 'wood', rz, 0.3));
    F.push(...Box([8.5, 0, 1.4], [1.6, 1.6, 1.6], 'steel', rz, 0.2));               // ferrule
    F.push(...Bar([10, -7, 1.4], [10, 7, 1.4], 0.9, 'steel', rz));
    for (let y = -6; y <= 6; y += 3) {
      F.push(...Bar([10, y, 1.4], [17, y * 1.1, 1.4], 0.6, 'steel', rz, 0.4));
      F.push(...Bar([17, y * 1.1, 1.4], [19.5, y * 1.1, 4.2], 0.6, 'steel', rz, 0.6));
    }
    return render(F, 0);
  },
  // an orange onion-mesh sack, lumpy with mussels, gathered and tied at the neck
  mussel_sack() {
    const F = [], weave = (i) => (i % 2 ? -0.7 : 0.15);
    const bands = [[0, 3, 7, 9.5], [3, 7, 9.5, 10.5], [7, 11, 10.5, 10], [11, 15, 10, 8.5], [15, 18, 8.5, 5]];
    bands.forEach(([z0, z1, r0, r1], k) => F.push(...Tube([0, 0], z0, z1, r0, r1, 14, 'float', { biasOf: (i) => weave(i + k), caps: k === 0 ? 'bottom' : 'none', phase: k * 0.22 })));
    F.push(...Tube([0, 0], 18, 22, 5, 1.6, 10, 'float', { biasOf: weave }));
    F.push(...Tube([0, 0], 21, 23, 2.2, 2.2, 8, 'rope', { caps: 'top', b: 0.4 }));  // the tie
    F.push(...Bar([1.5, 0, 22], [5, -1, 25], 0.6, 'rope', 0, 0.4));
    for (const [a, z] of [[0.3, 6], [2.0, 9], [3.6, 5], [5.1, 12], [1.1, 14], [4.4, 15]]) {   // shells pressing at the mesh
      const r = 9.6 + (z < 8 ? 0 : 0.3);
      F.push(...Box([Math.cos(a) * r, Math.sin(a) * r, z], [1.0, 1.0, 1.3], 'navy', a, -0.2, 0.2));
    }
    return render(F, 0);
  },
  // a slatted wooden peck basket, wider at the mouth, two cleat handles, oysters showing
  shellfish_basket() {
    const F = [];
    F.push(...Tube([0, 0], 0, 14, 7.5, 11, 16, 'ash', { biasOf: (i) => (i % 2 ? -0.5 : 0.2), caps: 'bottom' }));
    F.push(...Tube([0, 0], 13, 15, 11.2, 11.4, 16, 'wood', { b: 0.3 }));               // the rim hoop
    F.push(...Tube([0, 0], 4, 5.5, 8.6, 8.9, 16, 'wood', { b: 0.1 }));                 // the waist hoop
    for (const s of [-1, 1]) F.push(...Box([s * 11.8, 0, 12.5], [0.9, 3, 1.1], 'wood', 0, 0.3));
    for (const [x, y, z, rz] of [[-4, -2, 13.5, 0.5], [2, -4, 13.8, -0.4], [5, 2, 13.4, 1.1], [-2, 4, 13.6, 2.2], [0, 0, 14.6, 0.9], [-6, 3, 13.2, 1.6]])
      F.push(...Box([x, y, z], [3, 2, 1.2], 'shell', rz, 0, 0.1));
    return render(F, 0);
  },
  // a half-barrel tub with the groundline coiled in it and the hooks hung round the rim
  longline_tub() {
    const F = [];
    F.push(...Tube([0, 0], 0, 12, 9.5, 11, 18, 'plast', { caps: 'bottom', b: 0 }));
    F.push(...Tube([0, 0], 11.5, 13, 11.2, 11.3, 18, 'plast', { b: 0.5 }));
    for (let k = 0; k < 5; k++)                                                          // the coil, ring on ring
      F.push(...Tube([0, 0], 10.4, 11, 9.8 - k * 1.9, 9.8 - k * 1.9, 16, 'rope', { caps: k === 4 ? 'top' : 'none', b: k % 2 ? -0.9 : 0.5, phase: k * 0.3 }));
    F.push(...Tube([0, 0], 10, 10.6, 9.9, 0.5, 16, 'rope', { caps: 'none', b: -0.2 }));
    for (let i = 0; i < 9; i++) {                                                         // hooks over the near rim
      const a = Math.PI * 0.05 + i * Math.PI * 0.11 + Math.PI, x = Math.cos(a) * 11.5, y = Math.sin(a) * 11.5;
      F.push(...Bar([x, y, 13], [x * 1.06, y * 1.06, 10.5], 0.45, 'steel', 0, 0.8));
      F.push(...Bar([x * 1.06, y * 1.06, 10.5], [x * 1.0, y * 1.0, 9.6], 0.45, 'steel', 0, 0.8));
    }
    return render(F, 1);
  },
  // the trawl net laid out: the mouth with its floats on the headline, the long cone, the tied codend
  trawl_net() {
    const F = [], lay = (p) => [p[2] - 18 * U, p[1], p[0] * 0.75 + 7 * U];               // tube axis z -> along x, squashed in height
    const weave = (i) => (i % 2 ? -0.6 : 0.2);
    const seg = [[0, 8, 10, 8.5], [8, 16, 8.5, 6.5], [16, 24, 6.5, 4.5], [24, 32, 4.5, 3.5]];
    seg.forEach(([z0, z1, r0, r1], k) => F.push(...Tube([0, 0], z0, z1, r0, r1, 12, 'net', { pre: lay, biasOf: (i) => weave(i + k), rz: -0.35 })));
    F.push(...Tube([0, 0], 32, 37, 3.5, 3.8, 10, 'net', { pre: lay, caps: 'top', b: -0.3, rz: -0.35 }));
    F.push(...Tube([0, 0], 31.5, 33, 3.9, 3.9, 10, 'rope', { pre: lay, b: 0.3, rz: -0.35 }));   // codend tie
    for (let i = 0; i < 5; i++) {                                                         // floats on the headline
      const a = (i - 2) * 0.55;
      F.push(...Box([-18, Math.sin(a) * 10.4, 7 + Math.cos(a) * 10.4 * 0.75], [1.3, 1.3, 1.3], 'float', -0.35, 0.3, 0.4));
    }
    F.push(...Bar([-18, -10, 7], [-18, 10, 7], 0.7, 'rope', -0.35, 0.1));               // footrope
    return render(F, 0);
  },
  // a hand-length bar of leaded metal, hex in section, a treble on a split ring
  cod_jig() {
    const rz = 0, F = [], tilt = 0.5;
    const at = (s) => [Math.sin(tilt) * s, 0, 14 + Math.cos(tilt) * s];
    F.push(...Bar(at(-12), at(10), [2.4, 1.6], 'steel', rz, 0.3));
    F.push(...Bar(at(-12.5), at(-9), [1.8, 1.2], 'steel', rz, 0.8));
    F.push(...ring(...at(12.4), 1.4, rz));
    const e = at(-14);
    F.push(...ring(e[0], e[1], e[2], 1.4, rz));
    F.push(...treble(e[0], e[1], e[2] - 1.4, 5, rz));
    F.push(...Bar(at(10.5), [Math.sin(tilt) * 16, 0, 14 + Math.cos(tilt) * 16 + 2], 0.35, 'white', rz, 0.6));   // the line
    return render(F, 0);
  },
  // a curved blade of hammered brass, hook ring at the tail
  spoon() {
    const F = [], rz = 0.55;
    const outline = []; for (let i = 0; i < 16; i++) { const a = i / 16 * Math.PI * 2; const w = Math.cos(a) > 0 ? 6.5 : 4.2; outline.push([Math.cos(a) * 13, Math.sin(a) * w]); }
    const bow = (p) => [p[0], p[1], p[2] + 8 + (p[1] * p[1]) * 0.07];
    F.push(...K.prism(outline.map(q => [q[0] * U, q[1] * U]), 0, 0.9 * U, 'brass', { caps: true, capB: 0.4, xf: (p) => xfOf(rz)(P(bow([p[0] / U, p[1] / U, p[2] / U]))) }));
    F.push(...ring(14.5, 0, 9, 1.3, rz));
    F.push(...ring(-14.5, 0, 9, 1.3, rz));
    F.push(...Bar([-16, 0, 9], [-21, 0, 9], 0.5, 'steel', rz, 0.6));
    for (const a of [0.8, -0.8, 2.6]) F.push(...Bar([-21, 0, 9], [-22.5, Math.sin(a) * 2.2, 9 + Math.cos(a) * 2.2], 0.5, 'steel', rz, 0.6));
    return render(F, 0);
  },
  // a wire shaft, a brass blade standing off it on a clevis, red beads, a treble
  spinner() {
    const F = [], rz = 0, z0 = 10;
    const tilt = 0.55, at = (s, dz) => [Math.sin(tilt) * s, 0, z0 + Math.cos(tilt) * s + (dz || 0)];
    F.push(...Bar(at(-12), at(14), 0.45, 'steel', rz, 0.6));
    F.push(...ring(...at(15.2), 1.2, rz));
    for (let k = 0; k < 3; k++) { const c = at(-2 - k * 3); F.push(...Box(c, [1.5, 1.5, 1.5], k === 1 ? 'yellow' : 'rust', 0.6 * k, 0.4)); }
    const bl = []; for (let i = 0; i < 14; i++) { const a = i / 14 * Math.PI * 2; bl.push([Math.cos(a) * 6.5, Math.sin(a) * 3.6]); }
    const c = at(7), bx = (p) => [c[0] + p[1] * 0.55 + 3.4, c[1] - p[2] * 4, c[2] + p[0] * 0.95];
    F.push(...K.prism(bl.map(q => [q[0] * U, q[1] * U]), 0, 0.25 * U, 'brass', { caps: true, capB: 0.6, xf: (p) => xfOf(rz)(P(bx([p[0] / U, p[1] / U, p[2] / U]))) }));
    const e = at(-13);
    F.push(...treble(e[0], e[1], e[2], 4, rz));
    return render(F, 0);
  },
  // a string of four feathered hooks on a clear trace above a lead
  mackerel_feathers() {
    const F = [], rz = 0, cols = ['rust', 'yellow', 'teal', 'white'];
    F.push(...Bar([0, 0, 2], [0, 0, 40], 0.55, 'plast', rz, 0.6));
    cols.forEach((m, k) => {
      const z = 34 - k * 9, s = k % 2 ? 1 : -1;
      F.push(...Bar([0, 0, z], [s * 4, 0, z - 1], 0.55, 'plast', rz, 0.6));            // the snood
      F.push(...Bar([s * 4, 0, z - 1], [s * 6.5, 0, z - 4.5], [1.4, 1.1], m, rz, 0.3));  // the feather
      F.push(...Bar([s * 6.5, 0, z - 4.5], [s * 7.2, 0, z - 6.5], 0.45, 'steel', rz, 0.6));
      F.push(...Bar([s * 7.2, 0, z - 6.5], [s * 6, 0, z - 5.4], 0.45, 'steel', rz, 0.6));
    });
    F.push(...Box([0, 0, 2], [1.8, 1.8, 2.8], 'graph', 0.4, 0.8));                       // the lead
    F.push(...ring(0, 0, 41.2, 1.2, rz));
    return render(F, 0);
  },
  // a curly-tail grub on a lead jig head
  soft_bait() {
    const F = [], rz = 0, z = 10, n = 9;
    F.push(...Box([-13, 0, z], [3, 2.6, 3], 'graph', 0.0, 0.6));                         // the jig head
    F.push(...Box([-15.6, 0, z + 1.4], [0.6, 0.6, 0.6], 'steel', 0, 0.8));
    for (let i = 0; i < n; i++) {                                                         // segmented body
      const x = -9 + i * 2.2, r = 2.4 - Math.max(0, i - 5) * 0.35;
      F.push(...Box([x, 0, z], [1.2, r, r], 'teal', 0, i % 2 ? -0.3 : 0.3));
    }
    for (let i = 0; i < 6; i++) {                                                         // the curled tail
      const a = i / 6 * Math.PI * 1.4, x = 11 + Math.sin(a) * 4, zz = z - 4 + Math.cos(a) * 4;
      F.push(...Box([x, 0, zz], [1.1, 0.9, 1.1], 'teal', 0, 0.4));
    }
    F.push(...Bar([-10, 0, z + 2.6], [-2, 0, z + 3.4], 0.45, 'steel', rz, 0.7));        // the hook point out of the back
    F.push(...Bar([-2, 0, z + 3.4], [-1, 0, z + 5.4], 0.45, 'steel', rz, 0.7));
    return render(F, 0);
  },
  // the coaster rod (gear.rod) from rodIsoRig.js: the same rig the held mesh came from
  rod() {
    const img = Rod.render(ROD_DIR, { tier: 'coast', pitch: ROD_PITCH, yaw: ROD_YAW, bend: 0 });
    return { W: Rod.W, H: Rod.H, rgba: img };
  },
};
const ROD_DIR = 0, ROD_PITCH = 22 * Math.PI / 180, ROD_YAW = 90 * Math.PI / 180;

/* ---- PASS 2 (2026-11-20): the gear the Notebook's KIT page still lettered -------------------------
   The watch, the rod rack, the two pots, the seven baits and the four boat instruments. Same turntable,
   same icon-pixel units, same trim. The pots are not modelled here: they are trapIsoRig.js's builds,
   the rig the world pots (PotWood.png, PotWire.png) come from. The extra ramps below are lifted from
   the rigs that own those things (no invented hues) and are handed ONLY to the pass-2 pieces, so the
   pass-1 icons above re-bake byte for byte:
     resin, bezel, lcd, seg          ui/watch-face/Art/watchRig.js (RESIN, BEZEL, DAY lcd + segments)
     case, sonar, sonar band, fish   ui/console-helm/Art/fishRig.js (CASE, water, band, fish marks)
     chart sea / land                ui/console-helm/Art/fishRig.js (BLUE, TEAK)
     weed                            deck-loop-kit/Art/trapIsoRig.js (WEED)
     meat                            plaza-stores/nmcStores.js (meat)
   A screen is a flat ramp (one colour) so it reads as lit glass, not as a shaded solid. */
(0, eval)(fs.readFileSync(path.join(RIGS, 'deck-loop-kit/Art/trapIsoRig.js'), 'utf8'));
const Trap = globalThis.TrapIso;
const flat = (c) => [c, c, c, c, c];
const R2 = {
  resin: ['#050607', '#0c0f11', '#15191c', '#20262a', '#2c343a'],
  bezel: ['#5b636a', '#767f86', '#8b949b', '#a3acb2', '#c2c9cd'],
  lcd:   flat('#cbd2c4'), seg: flat('#1c2119'),
  case:  ['#12171b', '#1b2228', '#28323a', '#3a4750', '#4f5e68', '#6b7c86'],
  sonar: flat('#1a4b5a'), sonDeep: flat('#081019'),
  bGreen: flat('#4e9a4a'), bYellow: flat('#c9b53c'), bOrange: flat('#e0902f'), bRed: flat('#d0552c'),
  fishA: flat('#e0554a'), fishB: flat('#e8863c'), digit: flat('#ffd08a'), teal2: flat('#7fd6c9'),
  sea:   flat('#5b93b8'), land: flat('#c9a86a'),
  weed:  ['#1a2311', '#26301a', '#39471d', '#5c6e2c', '#8a9a45'],
  meat:  ['#5a1418', '#781c22', '#97272c', '#b03a3a', '#c55650', '#d6766c'],
};
const MATS2 = Object.assign({}, MATS); for (const k in R2) MATS2[k] = { ramp: R2[k] };
// sc grows a small piece to the card's reading size (~30-48 px) about the turntable origin, as an icon is a picture, not world size
function render2(faces, dir, sc) {
  const W = 112, H = 112, k = sc || 1, F = k === 1 ? faces : faces.map(f => Object.assign({}, f, { v: f.v.map(p => [p[0] * k, p[1] * k, p[2] * k]) }));
  return { W, H, rgba: K.paint(F, { W, H, cx: 56, cy: 64, MATS: MATS2, dir: dir || 0 }) };
}

// a face plane: u across, v up the face, w out of it; the face leans back by t from upright, so a screen
// or a watch dial looks at the camera instead of being foreshortened to a sliver
const faceXf = (t, o) => (p) => [o[0] * U + p[0], o[1] * U + p[1] * Math.sin(t) - p[2] * Math.cos(t), o[2] * U + p[1] * Math.cos(t) + p[2] * Math.sin(t)];
function FBox(c, h, mat, t, o, b, db) { return K.box(P(c), P(h), mat, b, db, faceXf(t, o)); }
// a lit rectangle on a face at w (glyph pixels): u0..u1, v0..v1, drawn just proud of the face
function Glyph(u0, u1, v0, v1, w, mat, t, o) { return FBox([(u0 + u1) / 2, (v0 + v1) / 2, w + 0.15], [(u1 - u0) / 2, (v1 - v0) / 2, 0.15], mat, t, o, 0, 0.05); }
// a seven-segment digit on a face, 3 px wide, 5 px tall, lower-left at (u,v)
const SEGS = { 0: 'abcdef', 1: 'bc', 2: 'abged', 3: 'abgcd', 4: 'fgbc', 5: 'afgcd', 7: 'abc', 8: 'abcdefg', 9: 'abcfgd' };
function Digit(n, u, v, w, mat, t, o) {
  const F = [], s = SEGS[n];
  const seg = { a: [u, u + 3, v + 4, v + 5], g: [u, u + 3, v + 2, v + 3], d: [u, u + 3, v, v + 1], f: [u, u + 1, v + 2.5, v + 5], b: [u + 2, u + 3, v + 2.5, v + 5], e: [u, u + 1, v, v + 2.5], c: [u + 2, u + 3, v, v + 2.5] };
  for (const k of s) F.push(...Glyph(...seg[k], w, mat, t, o));
  return F;
}
// a fish lying flat: lens outline in x-y, belly ramp body, a darker back strip, an eye
function FlatFish(len, wid, x, y, z, rz, body, back, opts) {
  opts = opts || {}; const F = [], n = 14, out = [];
  for (let i = 0; i < n; i++) { const a = i / n * Math.PI * 2, c = Math.cos(a), s = Math.sin(a); const taper = c < 0 ? 0.55 + 0.45 * (1 + c) : 1; out.push([c * len / 2, s * wid / 2 * taper]); }
  const xf = (p) => xfOf(rz)([x * U + p[0], y * U + p[1], z * U + p[2]]);
  F.push(...K.prism(out.map(q => [q[0] * U, q[1] * U]), 0, (opts.h || 1.4) * U, body, { caps: true, capB: 0.2, xf }));
  const bk = out.filter(q => q[1] > 0).map(q => [q[0] * U, q[1] * U]); bk.push([len / 2 * 0.9 * U, 0], [-len / 2 * 0.9 * U, 0]);
  const strip = []; for (let i = 0; i <= 8; i++) { const xx = -len / 2 * 0.8 + i * len * 0.8 / 8; strip.push([xx * U, wid * 0.1 * U]); }
  for (let i = 8; i >= 0; i--) { const xx = -len / 2 * 0.8 + i * len * 0.8 / 8; const k = 1 - Math.abs(xx) / (len / 2); strip.push([xx * U, (wid * 0.1 + wid * 0.38 * Math.max(0.2, k)) * U]); }
  F.push(...K.prism(strip, (opts.h || 1.4) * U, ((opts.h || 1.4) + 0.2) * U, back, { caps: true, capB: 0.1, xf, db: 0.02 }));
  // the tail fork
  F.push(...Bar([-len / 2 - 0.2, 0, 0.7], [-len / 2 - 2.6, -1.8, 0.7], [0.9, 0.5], back, 0, 0.1).map(f => (f.v = f.v.map(v => xfOf(rz)([v[0] + x * U, v[1] + y * U, v[2] + z * U])), f)));
  F.push(...Bar([-len / 2 - 0.2, 0, 0.7], [-len / 2 - 2.6, 1.8, 0.7], [0.9, 0.5], back, 0, 0.1).map(f => (f.v = f.v.map(v => xfOf(rz)([v[0] + x * U, v[1] + y * U, v[2] + z * U])), f)));
  if (opts.eye !== false) F.push(...K.box([len / 2 * 0.62 * U, -wid * 0.05 * U, ((opts.h || 1.4) + 0.3) * U], [0.5 * U, 0.5 * U, 0.3 * U], 'graph', 0, 0.06, xf));
  return F;
}
// the U bracket every helm instrument hangs in: two cheeks, a foot, two knobs
function Bracket(hw, hh, t, o, foot) {
  const F = [];
  for (const s of [-1, 1]) {
    F.push(...FBox([s * (hw + 1), 0, -1.5], [0.7, 2.6, 1.6], 'steel', t, o, 0.2));
    F.push(...FBox([s * (hw + 2.0), 0.4, -1.5], [0.6, 1.2, 1.2], 'graph', t, o, 0.4, 0.05));       // the knob
    F.push(...Bar([o[0] + s * (hw + 1), o[1] + 1, o[2] - 2], [o[0] + s * (hw + 1), o[1] + 1, foot], 0.8, 'steel', 0, 0.1));
  }
  F.push(...Box([o[0], o[1] + 1, foot - 0.4], [hw + 2, 3.2, 0.6], 'steel', 0, 0.3));
  return F;
}

Object.assign(PIECES, {
  // a resin digital watch, the strap closed in a loop behind the case, LCD showing the hour
  harbour_watch() {
    const F = [], t = 0.5, o = [0, 0, 16];
    F.push(...FBox([0, 0, 0], [6.5, 7, 1.8], 'resin', t, o, 0.5));                          // the case
    F.push(...FBox([0, 0, 1.9], [5.6, 5.8, 0.25], 'bezel', t, o, 0.2, 0.02));                 // the face ring
    F.push(...Glyph(-4.4, 4.4, -3.2, 3.2, 2.1, 'lcd', t, o));                                  // the LCD
    F.push(...Digit(1, -4.0, -2.5, 2.3, 'seg', t, o), ...Digit(2, 0.8, -2.5, 2.3, 'seg', t, o));
    for (const s of [-1, 1]) for (const v of [-3.5, 3.5]) F.push(...FBox([s * 7.1, v, 0.2], [0.8, 0.9, 0.8], 'steel', t, o, 0.4)); // pushers
    // the strap, up and down out of the case, curling back away from the face; holes in the lower one
    for (const s of [-1, 1]) for (let i = 0; i < 7; i++) {
      const v0 = 6.5 + i * 1.4, v1 = v0 + 1.6, w0 = -0.06 * (v0 - 6.5) ** 2, w1 = -0.06 * (v1 - 6.5) ** 2;
      const a = Math.atan2(w1 - w0, v1 - v0), cv = s * (v0 + v1) / 2, cw = (w0 + w1) / 2;
      F.push(...K.box([0, 0, 0], [4.4 * U, 0.85 * U, 0.8 * U], 'resin', i % 2 ? 0.6 : 0.9, 0,
        (p) => faceXf(t, o)([p[0], cv * U + p[1] * Math.cos(a) * s - p[2] * Math.sin(a), cw * U + p[1] * Math.sin(a) * s + p[2] * Math.cos(a)])));
      if (s < 0 && i >= 2 && i % 2 === 0) F.push(...Glyph(-0.5, 0.5, cv - 0.5, cv + 0.5, cw + 0.65, 'graph', t, o));
    }
    F.push(...FBox([0, 7.2, 0.8], [4.6, 0.5, 0.6], 'steel', t, o, 0.6));                       // the keeper by the case
    return render2(F, 0, 1.25);
  },
  // four rods upright in a wooden rack: a notched top board, a butt cup board, a back plank
  rod_rack() {
    const F = [];
    F.push(...Box([0, 3, 15], [13, 1, 15], 'wood', 0, -0.2));                                // back plank
    F.push(...Box([0, 0, 24], [13, 3, 0.9], 'wood', 0, 0.3));                                // notched top board
    F.push(...Box([0, 0, 3], [13, 3, 1.2], 'wood', 0, 0.1));                                 // butt board
    for (const [k, x] of [[0, -9], [1, -3], [2, 3], [3, 9]]) {
      F.push(...Bar([x, -0.5, 4], [x, -0.5, 12], 1.2, 'ash', 0, 0.2));                        // cork grip
      F.push(...Box([x, -2.2, 14], [1.4, 1.4, 1.5], k % 2 ? 'graph' : 'steel', 0, 0.3));      // the reel
      F.push(...Bar([x, -0.5, 12], [x + (k - 1.5) * 0.3, -0.5, 46 - k % 2 * 3], [0.7, 0.45], k === 2 ? 'navy' : 'graph', 0, 0.4));
      for (const z of [22, 32, 40]) F.push(...Box([x + (k - 1.5) * 0.3 * (z - 12) / 34, -1.3, z], [0.5, 0.6, 0.5], 'steel', 0, 0.6)); // guides
    }
    return render2(F, 0);
  },
  // the wooden bow-top lobster pot (trap.lobster), the world pot's own rig build, three-quarter on
  lobster_pot() { return { W: Trap.W, H: Trap.H, rgba: Trap.render('wood', 1, { colour: 'oak' }) }; },
  // the wire crab pot (trap.crab), the world pot's own rig build
  crab_pot() { return { W: Trap.W, H: Trap.H, rgba: Trap.render('crabWire', 1, { colour: 'black' }) }; },

  // herring for the pots: a net bait bag stuffed with whole herring, tails out of the tied neck
  bait_herring() {
    const F = [], weave = (i) => (i % 2 ? -0.8 : 0.3);
    const bands = [[0, 3, 5, 7.5], [3, 7, 7.5, 8.5], [7, 11, 8.5, 8], [11, 14, 8, 5.5], [14, 16, 5.5, 2.5]];
    bands.forEach(([z0, z1, r0, r1], k) => F.push(...Tube([0, 0], z0, z1, r0, r1, 12, 'net', { biasOf: (i) => weave(i + k), caps: k === 0 ? 'bottom' : 'none', phase: k * 0.26 })));
    for (const [a, z] of [[3.6, 4], [4.3, 9], [5.2, 6], [3.0, 11], [4.9, 12.5], [5.9, 9.5]])   // silver showing through
      F.push(...Box([Math.cos(a) * 8.2, Math.sin(a) * 8.2, z], [1.6, 0.6, 0.7], 'white', a + 1.2, 0.2, 0.25));
    F.push(...Tube([0, 0], 15.5, 17, 2.8, 2.8, 8, 'rope', { caps: 'top', b: 0.4 }));       // the tie
    for (const [dx, a] of [[-1.2, -0.5], [0.6, 0.15], [1.8, 0.7]]) {                            // tails
      F.push(...Bar([dx, 0, 16.5], [dx + Math.sin(a) * 3, 0, 21], [0.9, 0.6], 'steel', 0, 0.3));
      F.push(...Bar([dx + Math.sin(a) * 3, 0, 21], [dx + Math.sin(a) * 3 - 1.6, -0.4, 23.5], [0.8, 0.4], 'navy', 0, 0.3));
      F.push(...Bar([dx + Math.sin(a) * 3, 0, 21], [dx + Math.sin(a) * 3 + 1.6, -0.4, 23.5], [0.8, 0.4], 'navy', 0, 0.3));
    }
    return render2(F, 0, 1.45);
  },
  // mackerel cut for bait: three thick chunks, tiger-barred green back on top, red cut faces forward
  bait_mackerel() {
    const F = [];
    for (const [x, y, rz, w] of [[-8, 2, 0.25, 6], [0, -1, -0.05, 6.5], [8, 2.5, -0.3, 5.5]]) {
      const xf = xfOf(rz), o = (p) => xf([p[0] + x * U, p[1] + y * U, p[2]]);
      const sec = [[-w / 2, -3], [w / 2, -3], [w / 2 * 0.9, 3], [-w / 2 * 0.9, 3]].map(q => [q[0] * U, q[1] * U]);
      F.push(...K.prism(sec, 0, 5 * U, 'meat', { caps: true, capB: 0.6, xf: o }));             // the flesh
      F.push(...K.prism(sec.map(q => [q[0] * 1.02, q[1] * 1.02]), 5 * U, 5.9 * U, 'teal', { caps: true, capB: 0.5, xf: o, db: 0.02 }));  // the skin
      for (const yy of [-1.8, 0.2, 2.2]) F.push(...K.box([0, yy * U, 6.0 * U], [w / 2 * 0.85 * U, 0.45 * U, 0.12 * U], 'navy', 0.2, 0.05, o)); // the bars
      F.push(...K.box([0, -3.05 * U, 2.6 * U], [w / 2 * 0.6 * U, 0.1 * U, 0.6 * U], 'white', 0.4, 0.05, o));   // the backbone
    }
    return render2(F, 0, 1.7);
  },
  // capelin: a fanned bunch of small slim fish, olive backs, tails gathered
  bait_capelin() {
    const F = [];
    [-0.62, -0.31, 0, 0.31, 0.62].forEach((a, k) => {
      const L = 17, cx = Math.cos(a) * (L / 2 + 2) - 8, cy = Math.sin(a) * (L / 2 + 2);
      F.push(...FlatFish(L, 3.6, cx, cy, k % 2 ? 0.3 : 1.0, a, 'white', 'net', { h: 1.2 }));
    });
    return render2(F, 0, 1.5);
  },
  // squid cut in strips: three white tapering ribbons on a cutting board
  bait_squid_strip() {
    const F = [];
    F.push(...Box([0, 0, 1], [14, 8, 1], 'wood', 0, 0.2));
    [[-3.5, 0.18], [0.5, -0.05], [4.5, 0.12]].forEach(([y, rz], k) => {
      const n = 10, pts = [];
      for (let i = 0; i <= n; i++) { const x = -11 + i * 2.2, w = 1.6 - i * 0.08; pts.push([x, y + Math.sin(i * 0.8 + k) * 0.8 + w]); }
      for (let i = n; i >= 0; i--) { const x = -11 + i * 2.2, w = 1.6 - i * 0.08; pts.push([x, y + Math.sin(i * 0.8 + k) * 0.8 - w]); }
      F.push(...K.prism(pts.map(q => [q[0] * U, q[1] * U]), 2 * U, 3 * U, 'white', { caps: true, capB: 1.3, xf: xfOf(rz), db: 0.02 }));
      F.push(...K.box([-11 * U, (y + 0.6) * U, 3.1 * U], [0.8 * U, 0.6 * U, 0.1 * U], 'meat', 0.8, 0.05, xfOf(rz)));   // the skin fleck at the cut end
    });
    return render2(F, 0, 1.4);
  },
  // sea worms: two red-brown segmented worms coiled on a pad of wet rockweed
  bait_sea_worm() {
    const F = [];
    for (const [x, y, s] of [[-6, 2, 4], [2, -2, 5], [7, 3, 3.5], [-2, 4, 4], [0, 1, 4.5]])
      F.push(...Box([x, y, 1.6], [s, s * 0.7, 1.6], 'weed', x * 0.2, (x + y) % 2 ? -0.3 : 0.2));
    const worm = (pts, r) => { for (let i = 0; i + 1 < pts.length; i++) F.push(...Bar(pts[i], pts[i + 1], r * (1 - i / pts.length * 0.4), 'rust', 0, i % 2 ? -0.6 : 0.2, 0.05)); };
    const w1 = []; for (let i = 0; i <= 16; i++) w1.push([-9 + i * 1.2, -1 + Math.sin(i * 0.7) * 3, 3.6 + Math.sin(i * 0.35) * 0.6]);
    worm(w1, 0.95);
    const w2 = []; for (let i = 0; i <= 13; i++) w2.push([-4 + i * 1.25, 4 + Math.sin(i * 0.8 + 2) * 2.4, 3.6]);
    worm(w2, 0.85);
    return render2(F, 0, 1.5);
  },
  // shucked clam: a white tub heaped with the soft meats, dark siphons showing
  bait_shucked_clam() {
    const F = [];
    F.push(...Tube([0, 0], 0, 8, 7.5, 9, 16, 'white', { caps: 'bottom', b: -0.3 }));
    F.push(...Tube([0, 0], 7.5, 8.8, 9.2, 9.3, 16, 'white', { b: 0.3 }));
    F.push(...Tube([0, 0], 7, 7.6, 8.8, 0.5, 16, 'yellow', { caps: 'none', b: -0.4 }));
    for (const [x, y, z, rz] of [[-4, -2, 8.4, 0.5], [2, -4, 8.6, -0.4], [5, 2, 8.2, 1.1], [-2, 4, 8.4, 2.2], [0, 0, 9.6, 0.9], [-5.5, 2.5, 8.0, 1.6], [3, -0.5, 9.2, 2.6]]) {
      F.push(...Box([x, y, z], [2.6, 1.8, 1.2], 'yellow', rz, 0.2, 0.1));
      F.push(...Box([x + Math.cos(rz) * 2.2, y + Math.sin(rz) * 2.2, z + 0.4], [0.9, 0.8, 0.7], 'float', rz, -0.6, 0.15));   // siphon
    }
    return render2(F, 1, 1.5);
  },
  // fish scrap: a wooden fish box of heads, tails and a frame
  bait_fish_scrap() {
    const F = [], hx = 12, hy = 7, h = 6;
    F.push(...Box([0, 0, 0.6], [hx, hy, 0.6], 'wood', 0, -0.2));
    for (const s of [-1, 1]) { F.push(...Box([s * hx, 0, h / 2], [0.8, hy, h / 2], 'wood', 0, 0.1)); F.push(...Box([0, s * hy, h / 2], [hx, 0.8, h / 2], 'wood', 0, s > 0 ? -0.3 : 0.2)); }
    F.push(...Box([0, 0, 3.4], [hx - 0.8, hy - 0.8, 1.6], 'shell', 0, -0.6));                 // the pile under
    for (const [x, y, rz] of [[-6, -1.5, 0.4], [3, 2, -0.6], [7, -2.5, 2.8]]) {               // heads
      F.push(...Box([x, y, 6.2], [2.8, 2.2, 1.8], 'steel', rz, 0.2, 0.1));
      F.push(...Box([x + Math.cos(rz) * 2.6, y + Math.sin(rz) * 2.6, 6.2], [0.4, 2.0, 1.6], 'rust', rz, 0, 0.12));      // the cut gill face
      F.push(...Box([x - Math.cos(rz) * 1.2, y - Math.sin(rz) * 1.2 - 0.5, 8.1], [0.6, 0.6, 0.2], 'graph', 0, 0.4, 0.15)); // eye
    }
    for (const [x, y, a] of [[-1, 3, 0.3], [-9, 3, -0.4]]) {                                 // tails standing up
      F.push(...Bar([x, y, 5], [x + Math.sin(a) * 1.5, y, 9], [0.8, 0.6], 'navy', 0, 0.2));
      F.push(...Bar([x + Math.sin(a) * 1.5, y, 9], [x + Math.sin(a) * 1.5 - 1.8, y, 12], [0.7, 0.4], 'navy', 0, 0.3));
      F.push(...Bar([x + Math.sin(a) * 1.5, y, 9], [x + Math.sin(a) * 1.5 + 1.8, y, 12], [0.7, 0.4], 'navy', 0, 0.3));
    }
    F.push(...Bar([-4, -3.5, 6], [6, 0, 6.5], 0.5, 'white', 0, 0.4));                         // a frame: spine and ribs
    for (let i = 0; i < 5; i++) { const x = -3 + i * 2, y = -3.15 + i * 0.7; F.push(...Bar([x, y, 6.2], [x + 0.6, y - 1.6, 6.6], 0.35, 'white', 0, 0.5)); }
    return render2(F, 0, 1.45);
  },

  // the depth sounder: a small graphite head on its bracket, a grey LCD with the depth in big digits,
  // its transducer puck lying beside it on the cable
  depth_sounder() {
    const F = [], t = 0.3, o = [-2, 0, 13];
    F.push(...FBox([0, 0, -2.5], [7.5, 6.5, 2.5], 'case', t, o, 0.2));
    F.push(...Glyph(-6, 6, -1.5, 5, 0.05, 'lcd', t, o));
    F.push(...Digit(1, -3.6, -0.6, 0.1, 'seg', t, o), ...Digit(8, 0.8, -0.6, 0.1, 'seg', t, o));
    for (const u of [-4, 0, 4]) F.push(...Glyph(u - 1, u + 1, -5, -3.6, 0.1, 'bezel', t, o));
    F.push(...Bracket(7.5, 6.5, t, o, 1));
    // transducer and its cable
    F.push(...Tube([13, -4], 0, 2.5, 3.2, 2.6, 12, 'graph', { caps: 'top', b: 0.2 }));
    const cab = [[13, -4, 2.5], [14, -1, 3], [12, 3, 2], [7, 5, 3], [3, 4, 8]];
    for (let i = 0; i + 1 < cab.length; i++) F.push(...Bar(cab[i], cab[i + 1], 0.55, 'graph', 0, 0.5));
    return render2(F, 0, 1.2);
  },
  // the fish finder: a taller head, a colour sonar screen with a surface line, two fish arches and the
  // bottom in its green-to-red bands
  fish_finder() {
    const F = [], t = 0.3, o = [0, 0, 14];
    F.push(...FBox([0, 0, -2.2], [7.5, 8.5, 2.2], 'case', t, o, 0.2));
    F.push(...Glyph(-6, 6, -6, 7, 0.05, 'sonar', t, o));
    F.push(...Glyph(-6, 6, 6.2, 7, 0.1, 'fishA', t, o));                                        // the surface
    const bottom = [-2.5, -2, -1, -1.5, -0.5, 0, -1, -1.5, -2, -1.5, -2.5, -3];
    for (let i = 0; i < 12; i++) {
      const u = -6 + i, top = bottom[i];
      F.push(...Glyph(u, u + 1, top - 1, top, 0.1, 'bRed', t, o));
      F.push(...Glyph(u, u + 1, top - 2, top - 1, 0.1, 'bOrange', t, o));
      F.push(...Glyph(u, u + 1, top - 3, top - 2, 0.1, 'bYellow', t, o));
      F.push(...Glyph(u, u + 1, -6, top - 3, 0.1, 'bGreen', t, o));
    }
    for (const [u, v] of [[-3.5, 3.5], [2, 2]]) {                                              // fish arches
      F.push(...Glyph(u - 1, u + 1, v, v + 1, 0.12, 'fishB', t, o));
      F.push(...Glyph(u - 2, u - 1, v - 1, v, 0.12, 'fishB', t, o), ...Glyph(u + 1, u + 2, v - 1, v, 0.12, 'fishB', t, o));
    }
    F.push(...Bracket(7.5, 8.5, t, o, 1));
    return render2(F, 0, 1.25);
  },
  // the Loran-C set: a deep box, an amber digit window and a keypad
  loran() {
    const F = [], t = 0.22, o = [0, 0, 8];
    F.push(...FBox([0, 0, -6], [11, 6, 6], 'case', t, o, 0.1));
    F.push(...Glyph(-10, 2, -0.6, 5.6, 0.05, 'sonDeep', t, o));
    for (let k = 0; k < 3; k++) F.push(...Digit([4, 7, 2][k], -9.0 + k * 3.8, 0.0, 0.1, 'digit', t, o));
    F.push(...Glyph(-10, -7, -2.6, -1.6, 0.1, 'teal2', t, o));                                 // the station lamp
    for (let r = 0; r < 3; r++) for (let c = 0; c < 3; c++) F.push(...Glyph(3.5 + c * 2.4, 5.1 + c * 2.4, -4.5 + r * 2.6, -3.1 + r * 2.6, 0.1, 'bezel', t, o));
    F.push(...Glyph(-6, 1, -4.5, -3, 0.1, 'bezel', t, o));                                     // the long key
    F.push(...FBox([0, 0, -12.6], [10, 5, 0.6], 'graph', t, o, -0.2));                          // the back plate
    F.push(...Bar([8, 6, 6], [9, 9, 12], 0.6, 'graph', 0, 0.4), ...Bar([9, 9, 12], [10, 10, 24], 0.35, 'steel', 0, 0.6));   // the antenna lead
    return render2(F, 0, 1.25);
  },
  // the GPS chartplotter: a landscape screen with blue water, a tan coast, the boat and its track
  gps() {
    const F = [], t = 0.3, o = [0, 0, 13];
    F.push(...FBox([0, 0, -2], [10.5, 7.5, 2], 'case', t, o, 0.2));
    F.push(...Glyph(-9, 9, -4.5, 6.5, 0.05, 'sea', t, o));
    const coast = [6.5, 6.5, 5, 4, 4, 3, 1.5, 1, 0, -1.5, -2, -3.5, -4.5, -4.5, -4.5, -4.5, -4.5, -4.5];   // land top-right
    for (let i = 0; i < 18; i++) { const u = -9 + i; const v0 = coast[i]; if (i >= 6 && v0 < 6.5) F.push(...Glyph(u, u + 1, Math.max(v0, -4.5) + (i - 6) * 0.0, 6.5, 0.1, 'land', t, o)); }
    for (const [u, v] of [[-7, -3], [-5.5, -1.5], [-4, 0], [-2.5, 1.5]]) F.push(...Glyph(u, u + 1, v, v + 1, 0.12, 'white', t, o));   // the track
    F.push(...Glyph(-1.5, 0.5, 2.5, 4.5, 0.14, 'fishA', t, o));                                // the boat
    for (const u of [-6, -2, 2, 6]) F.push(...Glyph(u - 1, u + 1, -7, -5.6, 0.1, 'bezel', t, o));
    F.push(...Bracket(10.5, 7.5, t, o, 1));
    return render2(F, 0, 1.2);
  },
});

/* ---- PASS 3 (2026-11-22): the headlamp ------------------------------------------------------------
   He has it from the first morning (gear.headlamp, never sold). Same turntable, units and trim. Its
   colours are the worn lamp's own (Code/Art/Headlamp.cs: StrapColour, BuckleColour, HousingColour,
   HousingTopColour, LensLitColour), each stretched to a five-step ramp about that colour, and handed
   ONLY to this piece, so every icon above re-bakes byte for byte. The lens is a flat ramp: lit glass. */
const R3 = {
  strap:   ['#2e2822', '#3f3730', '#544a40', '#6a5e52', '#827466'],
  buckle:  ['#4e4a42', '#6c665c', '#8c8476', '#a8a092', '#c2bbae'],
  housing: ['#5e2219', '#843327', '#aa4a3a', '#c66852', '#d8876e'],
  lensLit: flat('#fff6dc'),
};
const MATS3 = Object.assign({}, MATS2); for (const k in R3) MATS3[k] = { ramp: R3[k] };
function render3(faces, dir, sc) {
  const W = 112, H = 112, k = sc || 1, F = k === 1 ? faces : faces.map(f => Object.assign({}, f, { v: f.v.map(p => [p[0] * k, p[1] * k, p[2] * k]) }));
  return { W, H, rgba: K.paint(F, { W, H, cx: 56, cy: 64, MATS: MATS3, dir: dir || 0 }) };
}
// a disc facing the camera (-y): a short tube whose axis runs along -y from y0, centred at (x, z)
function FaceDisc(x, y0, z, depth, r, seg, mat, b) {
  return K.tube(0, 0, 0, depth * U, r * U, r * U, seg, mat,
    { caps: 'top', b: b || 0, capB: b || 0, db: 0.02, xf: (p) => [x * U + p[0], y0 * U - p[2], z * U + p[1]] });
}

Object.assign(PIECES, {
  // the headlamp: an elastic strap loop with a top strap over the crown, a grey slide buckle at the
  // side, and the red lamp housing at the front with its lens lit
  headlamp() {
    const F = [], rx = 11, sq = 0.78, z0 = 5, z1 = 8.5;
    F.push(...Tube([0, 0], z0, z1, rx, rx, 20, 'strap', { sq, biasOf: (i) => (i % 2 ? -0.35 : 0.2) }));   // the band, elastic ribs
    for (let i = 0; i < 6; i++) {                                                                        // the top strap, front to back over the crown
      const a0 = i / 6 * Math.PI, a1 = (i + 1) / 6 * Math.PI;
      const p = (a) => [0, -Math.cos(a) * rx * sq, z1 - 0.5 + Math.sin(a) * 9];
      F.push(...Bar(p(a0), p(a1), [1.4, 0.6], 'strap', 0, i % 2 ? -0.2 : 0.1));
    }
    F.push(...Box([rx + 0.4, 0, (z0 + z1) / 2], [0.9, 2.2, 2.4], 'buckle', 0, 0.3, 0.05));               // the slide buckle
    F.push(...Box([rx + 1.0, 0, (z0 + z1) / 2], [0.3, 1.2, 1.0], 'strap', 0, -0.4, 0.08));               // the strap through it
    const hy = -rx * sq - 1.6, hz = (z0 + z1) / 2 + 0.5;
    F.push(...Box([0, -rx * sq + 0.2, hz], [3.8, 0.8, 3.4], 'buckle', 0, -0.2, 0.02));                   // the mount plate on the band
    F.push(...Box([0, hy, hz], [4.6, 1.8, 3.6], 'housing', 0, 0.2, 0.04));                                // the lamp housing
    for (const s of [-1, 1]) F.push(...Box([s * 4.9, hy + 0.6, hz], [0.5, 0.9, 0.9], 'buckle', 0, 0.2, 0.05));   // the tilt hinge knobs
    F.push(...Box([2.6, hy, hz + 3.9], [1.0, 0.8, 0.35], 'buckle', 0, 0.4, 0.06));                        // the switch on top
    F.push(...FaceDisc(-0.6, hy - 1.7, hz, 0.4, 3.0, 14, 'buckle', 0.2));                                 // the lens bezel
    F.push(...FaceDisc(-0.6, hy - 2.0, hz, 0.4, 2.2, 14, 'lensLit', 0));                                  // the lit lens
    const turn = K.rotZ(HEADLAMP_TURN);                                                                   // three-quarter on, so the top strap reads as an arch
    return render3(F.map(f => Object.assign({}, f, { v: f.v.map(turn) })), 0, 1.35);
  },
});

const HEADLAMP_TURN = 0.4;                                  // radians about z: the lamp toward the viewer's right
const only = process.argv.slice(2);
for (const leaf of Object.keys(PIECES)) if (!only.length || only.includes(leaf)) console.log(save(leaf, PIECES[leaf]()));

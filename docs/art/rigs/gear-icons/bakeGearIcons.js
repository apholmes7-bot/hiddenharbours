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

// ---- helpers ---------------------------------------------------------------------------------------
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

const only = process.argv.slice(2);
for (const leaf of Object.keys(PIECES)) if (!only.length || only.includes(leaf)) console.log(save(leaf, PIECES[leaf]()));

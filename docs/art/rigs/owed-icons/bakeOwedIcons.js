/* OWED ICONS bake (2026-11-20): the catch icons the catch pass 2 set still owed
   (docs/art/fish-sprites-owed.md, docs/art/fish-art-pass-2-plan.md section 1.3).

   Run from the repository root:  node docs/art/rigs/owed-icons/bakeOwedIcons.js

   Draws with the catch pass 2 rigs and nothing else; it edits none of them.
   · herring, mackerel: their large rung's deck lay is 14 and 19 px long, so a 64 px icon slot scales a few
     pixels up. The same lay (row 2, frame 0) is rendered at ICON_SCALE, into the icon-cell folder that
     FishIconBuilder.TrimmedIcon reads before the deck sheet. Build Fish Icons then cuts and wires them.
   · lobster, rock crab: Crustacean2.renderPlate, the field-guide drawing from above (the lobster live, dark
     olive), trimmed.
   · soft-shell clam: Shellfish2.renderPlate, the rig's own clam plate with its siphon, trimmed.
   · oyster, blue mussel, sea scallop: the rig has no plate for them, so shellPlate() draws each by the clam
     plate's law (lit from the upper left down the kind's own Shellfish2 PAL ramp, the 4x4 dither, growth
     lines as the margin scaled about the beak, a step darker), on the kind's own outline: the oyster long,
     rough and irregular; the mussel a dark blue-black wedge from a pointed beak with a pale sheen; the
     scallop a ribbed fan with two ears at the hinge. Sizes follow Shellfish2.SIZES at SHELL_SCALE (the mussel larger, so it is not a smudge).
   Output: Assets/_Project/Art/Fishing/Icons/ShellIcon_<id leaf>.png, trimmed with a 1 px margin. */
const fs = require('fs'), path = require('path'), zlib = require('zlib');
const ROOT = process.cwd(), RIGS = path.join(ROOT, 'docs/art/rigs');
for (const f of ['deck-loop-kit/Art/isoSolid.js', 'catch-pass-2-kit/Art/fishIsoRig2.js', 'catch-pass-2-kit/Art/crustaceanRig2.js', 'catch-pass-2-kit/Art/shellfishRig2.js'])
  (0, eval)(fs.readFileSync(path.join(RIGS, f), 'utf8'));
const F = globalThis.FishIso2, C = globalThis.Crustacean2, S = globalThis.Shellfish2;

const ICON_CELLS = 'docs/art/rigs/coast-species-kit/icon-cells';   // FishIconBuilder.IconCell
const ICONS = 'Assets/_Project/Art/Fishing/Icons';
// icon scale: about 38-40 px long, as the cod's icon (38) and the bass's (46)
const ICON_SCALE = { herring: 5.0, mackerel: 2.8 };
// shells: the clam at the quahog's 9.6; the three drawn here a little larger, so each icon is 30-40 px
const CRAB_SCALE = 5.0, LOBSTER_SCALE = 3.4, CLAM_SCALE = 9.6, SHELL_SCALE = { oyster: 11, mussel: 16, scallop: 11 };

// ---- PNG out -------------------------------------------------------------------------------------------
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
const icon = (leaf, img, W, H) => { const t = trim(img, W, H, 1); return writePng(path.join(ROOT, ICONS, `ShellIcon_${leaf}.png`), t.w, t.h, t.rgba); };

// ---- the clam plate's law, on any outline --------------------------------------------------------------
const BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]].map(r => r.map(v => (v + 0.5) / 16));
const hex = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
const hsh = (x, y, s) => { let h = (Math.imul(x, 374761393) + Math.imul(y, 668265263) + Math.imul(s, 1274126177)) | 0; h = Math.imul(h ^ (h >>> 13), 1103515245); return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };
// spec: inside(u, v) on u, v in [-1, 1] (u along the length, v across, +v the hinge side, drawn up);
// beak [u, v]; rings (gauge fractions); tone(u, v, x, y, base) may shift a pixel's ramp step (ribs, sheen)
function shellPlate(kind, spec) {
  const Z = S.SIZES[kind], k = 32 * SHELL_SCALE[kind], rx = Z.len / 2 * k, ry = Z.wid / 2 * k;
  const W = Math.ceil(rx * 2 + 8), H = Math.ceil(ry * 2 + 8), cx = W / 2, cy = H / 2, ramp = S.PAL ? S.PAL[kind] : null;
  const R = (spec.ramp || ramp).map(hex);
  const uv = (x, y) => [(x - cx) / rx, (cy - y) / ry];
  const inS = (x, y) => { const [u, v] = uv(x, y); return spec.inside(u, v); };
  const bx = cx + spec.beak[0] * rx, by = cy - spec.beak[1] * ry;
  const gauge = (x, y) => { let lo = 0, hi = 1; for (let it = 0; it < 22; it++) { const t = (lo + hi) / 2; if (inS(bx + (x - bx) / t, by + (y - by) / t)) hi = t; else lo = t; } return hi; };
  const N = W * H, kk = new Int8Array(N).fill(-1), lo = new Int8Array(N), tg = new Float32Array(N).fill(2);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const px = x + 0.5, py = y + 0.5, i = y * W + x; if (!inS(px, py)) continue;
    tg[i] = gauge(px, py); const [u, v] = uv(px, py);
    let pos = 2.2 - 1.6 * (0.55 * u - 0.75 * v) - 0.6 * (u * u + v * v);
    if (spec.tone) pos += spec.tone(u, v, x, y, tg[i]);
    const b = Math.floor(pos); lo[i] = Math.max(1, Math.min(3, b)); kk[i] = Math.max(0, Math.min(4, b + ((pos - b) > BAYER[x & 3][y & 3] ? 1 : 0)));
  }
  const inside = (x, y) => x >= 0 && y >= 0 && x < W && y < H && kk[y * W + x] >= 0;
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const i = y * W + x; if (kk[i] < 0) continue;
    for (const Rg of spec.rings || []) { if (tg[i] < Rg) continue;
      if ([[-1, 0], [0, -1], [1, 0], [0, 1]].some(([dx, dy]) => inside(x + dx, y + dy) && tg[(y + dy) * W + x + dx] < Rg)) { kk[i] = Math.max(0, lo[i] - 1); break; } }
  }
  // the margin: a pixel on the outline's lower or right edge a step darker, so the shell sits on the slot
  const edge = new Int8Array(N).fill(-1);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const i = y * W + x; if (kk[i] < 0) continue;
    if (!inside(x, y + 1) || !inside(x + 1, y)) edge[i] = Math.max(0, kk[i] - 1); }
  const out = new Uint8ClampedArray(N * 4);
  for (let i = 0; i < N; i++) { const s = edge[i] >= 0 ? edge[i] : kk[i]; if (s < 0) continue; const c = R[s]; out[i * 4] = c[0]; out[i * 4 + 1] = c[1]; out[i * 4 + 2] = c[2]; out[i * 4 + 3] = 255; }
  return { W, H, rgba: out };
}

const SHELLS = {
  // the oyster: long, the beak at the left narrow end, the margin ragged (hashed by angle), thick rough
  // growth lines; Shellfish2 PAL.oyster (grey-white)
  american_oyster: { kind: 'oyster', beak: [-0.92, 0.10], rings: [0.32, 0.44, 0.55, 0.66, 0.76, 0.86],
    inside: (u, v) => { const a = Math.atan2(v, u), r = 1 - 0.10 * hsh(Math.round((a + 4) * 6), 3, 11) - 0.06 * Math.sin(a * 5);
      const w = 0.62 + 0.38 * Math.min(1, (u + 1) / 0.8); return (u * u) + (v / w) * (v / w) <= r * r; },
    tone: (u, v, x, y) => (hsh(x >> 1, y >> 1, 5) < 0.18 ? -1 : 0) },
  // the blue mussel: a wedge, the pointed beak at the left, the back (hinge) edge straight, the far end
  // round; blue-black, a pale sheen along the lit ridge; Shellfish2 PAL.mussel
  blue_mussel: { kind: 'mussel', beak: [-0.98, 0.05], rings: [0.45, 0.62, 0.78, 0.90],
    inside: (u, v) => { const t = (u + 1) / 2; if (t < 0 || t > 1) return false;
      const top = 0.15 + 0.85 * Math.sqrt(Math.min(1, t / 0.75)), bot = -(0.10 + 0.75 * Math.pow(t, 0.8));
      const end = t > 0.80 ? Math.sqrt(1 - Math.pow((t - 0.80) / 0.20, 2)) : 1;
      return v <= top * end && v >= bot * end; },
    tone: (u, v) => { const ridge = Math.abs(v - (0.25 * (u + 1) / 2)); return ridge < 0.12 && u > -0.7 ? 1.2 : -0.4; } },
  // the sea scallop: a round fan, the beak at the top centre with two ears either side of it, ribs out
  // from the beak alternately a step light and dark; Shellfish2 PAL.scallop
  sea_scallop: { kind: 'scallop', beak: [0, 0.98], rings: [0.40, 0.62, 0.82],
    inside: (u, v) => { const fan = u * u + (v + 0.08) * (v + 0.08) / 0.92 <= 0.92 && v < 0.98 - 0.9 * Math.abs(u) + 0.35;
      const ears = v > 0.62 && v <= 0.98 && Math.abs(u) <= 0.42; return fan || ears; },
    tone: (u, v) => { if (v > 0.62 && Math.abs(u) > 0.18) return -0.6;
      const a = Math.atan2(u, 0.98 - v); return (Math.floor((a + 2) * 9) % 2 === 0) ? 0.5 : -0.5; } },
};

const done = [];
for (const sp of Object.keys(ICON_SCALE)) {
  const rgba = F.render(2, { species: sp, rest: 'deck', frame: 0, scale: ICON_SCALE[sp] });
  done.push(writePng(path.join(ROOT, ICON_CELLS, `FishIconCell_${sp}.png`), F.W, F.H, rgba));
}
done.push(icon('lobster', C.renderPlate('lobster', LOBSTER_SCALE), C.PW, C.PH));
done.push(icon('rock_crab', C.renderPlate('crab', CRAB_SCALE), C.PW, C.PH));
done.push(icon('soft_shell_clam', S.renderPlate('clam', CLAM_SCALE), S.PW, S.PH));
for (const [leaf, spec] of Object.entries(SHELLS)) { const p = shellPlate(spec.kind, spec); done.push(icon(leaf, p.rgba, p.W, p.H)); }
console.log(done.join('\n'));

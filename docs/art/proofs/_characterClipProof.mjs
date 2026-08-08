// Proof-render harness for the pass-6.2 character kit.
// Runs the rig files VERBATIM in node's V8 — the same engine ClearScript gives CharacterRigBaker —
// and writes proof PNGs. This is documentation, not game art: the shipped sheets are baked by
// CharacterRigBaker inside the editor.
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';

const RIGS = process.argv[2];
const OUT = process.argv[3];

// ---- load order matters: eye -> head -> body (kit README) --------------------------------
for (const f of ['eyeIsoRig.js', 'headIsoRig3.js', 'characterIsoRig6.js']) {
  const src = fs.readFileSync(path.join(RIGS, f), 'utf8');
  (0, eval)(src);
}
const C = globalThis.CharacterIso6;
if (!C) throw new Error('CharacterIso6 did not register');

// ---- minimal PNG encoder (RGBA8, no filtering) -------------------------------------------
const CRC = (() => {
  const t = new Int32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c;
  }
  return t;
})();
function crc32(buf) {
  let c = -1;
  for (let i = 0; i < buf.length; i++) c = CRC[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ -1) >>> 0;
}
function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length);
  const td = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(td));
  return Buffer.concat([len, td, crc]);
}
function encodePng(rgba, w, h) {
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) {
    raw[y * (w * 4 + 1)] = 0;
    Buffer.from(rgba.buffer ?? rgba, y * w * 4, w * 4).copy(raw, y * (w * 4 + 1) + 1);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0);
  ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

// ---- blit one cell into a sheet buffer ----------------------------------------------------
const W = C.W, H = C.H;
function blit(cell, sheet, sw, col, row) {
  for (let y = 0; y < H; y++) {
    const src = y * W * 4;
    const dst = ((row * H + y) * sw + col * W) * 4;
    for (let i = 0; i < W * 4; i++) sheet[dst + i] = cell[src + i];
  }
}

// ---- CHECKERBOARD so the 1 px tinted keyline and binary alpha are visible in the PR -------
function onChecker(rgba, w, h) {
  const out = Buffer.alloc(w * h * 4);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const i = (y * w + x) * 4;
    const a = rgba[i + 3] / 255;
    const bg = ((x >> 3) + (y >> 3)) & 1 ? 0x2b : 0x38;
    out[i] = Math.round(rgba[i] * a + bg * (1 - a));
    out[i + 1] = Math.round(rgba[i + 1] * a + bg * (1 - a));
    out[i + 2] = Math.round(rgba[i + 2] * a + bg * (1 - a));
    out[i + 3] = 255;
  }
  return out;
}

// ---- scale up (nearest) so 32 px/m art is legible on a PR page ----------------------------
function scale(rgba, w, h, k) {
  const out = Buffer.alloc(w * k * h * k * 4);
  for (let y = 0; y < h * k; y++) for (let x = 0; x < w * k; x++) {
    const s = ((y / k | 0) * w + (x / k | 0)) * 4, d = (y * w * k + x) * 4;
    out[d] = rgba[s]; out[d + 1] = rgba[s + 1]; out[d + 2] = rgba[s + 2]; out[d + 3] = rgba[s + 3];
  }
  return out;
}

// ---- bake one clip to a proof sheet: rows = facings, cols = frames -------------------------
// The proof CROPS each 64×92 cell to the figure's own column (the rig draws a 1.51 m fisher at
// 32 px/m — most of the cell is the headroom a tall build needs) and scales it up, so the PR
// shows the pose rather than a field of transparency. The BAKED sheet is the full cell.
const CX = 14, CY = 10, CW2 = 36, CH2 = 78;   // crop window inside the cell

function proof(anim, opts, dirs, label, k = 4) {
  const frames = C.ANIMS[anim].frames;
  const sw = frames * CW2, sh = dirs.length * CH2;
  const sheet = Buffer.alloc(sw * sh * 4);
  for (let r = 0; r < dirs.length; r++)
    for (let f = 0; f < frames; f++) {
      const cell = C.render(dirs[r], { ...opts, anim, frame: f });
      for (let y = 0; y < CH2; y++) {
        const src = ((y + CY) * W + CX) * 4;
        const dst = ((r * CH2 + y) * sw + f * CW2) * 4;
        for (let i = 0; i < CW2 * 4; i++) sheet[dst + i] = cell[src + i];
      }
    }
  const shown = scale(onChecker(sheet, sw, sh), sw, sh, k);
  fs.writeFileSync(path.join(OUT, `${label}.png`), encodePng(shown, sw * k, sh * k));
  return { anim, frames, ms: C.ANIMS[anim].ms,
           sheetW: frames * W, sheetH: dirs.length * H, dirs: dirs.length };
}

fs.mkdirSync(OUT, { recursive: true });
const build = { preset: 'fisher' };
const ALL8 = [0, 1, 2, 3, 4, 5, 6, 7];
const rows = [];

rows.push(proof('haul', { build }, ALL8, 'character-haul-8dir'));
rows.push(proof('board', { build, railZ: 0.55 }, ALL8, 'character-board-rail055-8dir'));
rows.push(proof('boardDown', { build, railZ: 0.55 }, ALL8, 'character-boardDown-rail055-8dir'));
rows.push(proof('ladderDown', { build, rung: 0.30, ladderW: 0.45 }, ALL8, 'character-ladderDown-8dir'));

// ---- report the CONTRACT the presenter has to honour ---------------------------------------
const report = { cell: { w: C.W, h: C.H }, pivot: C.pivot, sheets: rows, clips: {} };
for (const a of ['board', 'boardDown', 'haul', 'ladderDown']) {
  report.clips[a] = {
    frames: C.ANIMS[a].frames, ms: C.ANIMS[a].ms, oneShot: !!C.ANIMS[a].oneShot,
    mount: C.ANIM_MOUNT[a],
    carries: C.CARRY_ORDER.filter(c => C.CARRIES[c].anims.includes(a)),
  };
}
// the pins each clip family exposes, at dir 4 (South) frame 0
report.pins = {
  boardMount: C.boardMount(4, { anim: 'board', frame: 5, railZ: 0.55, build }),
  haulGrip: C.haulGrip(4, { anim: 'haul', frame: 3, build }),
  ladderMount: C.ladderMount(4, { anim: 'ladderDown', frame: 3, build }),
};
// non-empty check per facing: a "reduced facing set" clip would show empty rows here
report.nonEmptyPerFacing = {};
for (const a of ['board', 'boardDown', 'haul', 'ladderDown']) {
  report.nonEmptyPerFacing[a] = ALL8.map(d => {
    const px = C.render(d, { anim: a, frame: 0, build, railZ: 0.55 });
    let n = 0;
    for (let i = 3; i < px.length; i += 4) if (px[i] > 0) n++;
    return n;
  });
}
console.log(JSON.stringify(report, null, 2));

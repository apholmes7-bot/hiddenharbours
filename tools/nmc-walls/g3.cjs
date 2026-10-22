// NMC walls (H3): G3, the ground, read the way the game reads it. Node built-ins only.
// decode(file): the PNG's samples (16-bit grey, big-endian), row 0 = the top (north), and the pixels' sha256 (the decoded
// samples row by row from the top, 2 bytes a texel, big-endian: the amendment's convention).
// field(codes): PaintedHeightField.ElevationAt over NineMileCreekGround.Load's grid, in float32 step for step.
'use strict';
const fs = require('fs'), zlib = require('zlib'), crypto = require('crypto');
const W = 1520, H = 1120, MIN = -6, MAX = 6;
const f = Math.fround;
const sha = (b) => crypto.createHash('sha256').update(b).digest('hex');

function decode(file) {
  const u8 = fs.readFileSync(file);
  const sig = [137, 80, 78, 71, 13, 10, 26, 10];
  for (let i = 0; i < 8; i++) if (u8[i] !== sig[i]) throw new Error('not a PNG: ' + file);
  let p = 8, hd = null; const idat = [];
  while (p + 12 <= u8.length) {
    const len = u8.readUInt32BE(p), type = u8.toString('latin1', p + 4, p + 8), data = u8.subarray(p + 8, p + 8 + len);
    if (type === 'IHDR') hd = { w: data.readUInt32BE(0), h: data.readUInt32BE(4), bitDepth: data[8], colourType: data[9], interlace: data[12] };
    if (type === 'IDAT') idat.push(data);
    p += 12 + len; if (type === 'IEND') break;
  }
  if (!hd || hd.w !== W || hd.h !== H || hd.bitDepth !== 16 || hd.colourType !== 0 || hd.interlace !== 0)
    throw new Error('not a 1520 x 1120 R16 grey PNG: ' + JSON.stringify(hd));
  const raw = zlib.inflateSync(Buffer.concat(idat)), bpp = 2, rb = W * bpp, px = Buffer.alloc(rb * H);
  for (let r = 0; r < H; r++) {
    const ft = raw[r * (rb + 1)], src = r * (rb + 1) + 1, o = r * rb;
    for (let i = 0; i < rb; i++) {
      const x = raw[src + i], a = i >= bpp ? px[o + i - bpp] : 0, b = r ? px[o - rb + i] : 0, c = r && i >= bpp ? px[o - rb + i - bpp] : 0;
      let v = x;
      if (ft === 1) v += a; else if (ft === 2) v += b; else if (ft === 3) v += (a + b) >> 1;
      else if (ft === 4) { const q = a + b - c, pa = Math.abs(q - a), pb = Math.abs(q - b), pc = Math.abs(q - c); v += (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c); }
      else if (ft !== 0) throw new Error('bad filter ' + ft);
      px[o + i] = v & 255;
    }
  }
  const codes = new Uint16Array(W * H);
  for (let i = 0; i < W * H; i++) codes[i] = (px[2 * i] << 8) | px[2 * i + 1];
  return { header: hd, fileBytes: u8.length, fileSha256: sha(u8), pixelsSha256: sha(px), codes };
}

// Mathf.Lerp(a, b, t) = a + (b - a) * Clamp01(t), in float32.
const lerp = (a, b, t) => f(a + f(f(b - a) * f(t < 0 ? 0 : t > 1 ? 1 : t)));

// NineMileCreekGround.Load: row r of the PNG is grid row (H - 1 - r); elevation = Lerp(-6, 6, code / 65535f).
function field(codes) {
  const elev = new Float32Array(W * H);
  for (let r = 0; r < H; r++)
    for (let c = 0; c < W; c++) elev[(H - 1 - r) * W + c] = lerp(MIN, MAX, f(codes[r * W + c] / 65535));
  const sizeX = f(760), sizeY = f(560), minX = f(-380), minY = f(-280);
  const clamp = (v, lo, hi) => v < lo ? lo : v > hi ? hi : v;
  function at(x, y) {
    x = f(x); y = f(y);
    const u = f(f(x - minX) / sizeX), v = f(f(y - minY) / sizeY);
    const fx = f(f(u * W) - 0.5), fy = f(f(v * H) - 0.5);
    const x0 = Math.floor(fx), y0 = Math.floor(fy), tx = f(fx - x0), ty = f(fy - y0);
    const x0c = clamp(x0, 0, W - 1), x1c = clamp(x0 + 1, 0, W - 1), y0c = clamp(y0, 0, H - 1), y1c = clamp(y0 + 1, 0, H - 1);
    const ex0 = lerp(elev[y0c * W + x0c], elev[y0c * W + x1c], tx), ex1 = lerp(elev[y1c * W + x0c], elev[y1c * W + x1c], tx);
    return lerp(ex0, ex1, ty);
  }
  // a texel's own height by its world centre (col from the west, row from the south)
  const texel = (col, rowFromSouth) => elev[clamp(rowFromSouth, 0, H - 1) * W + clamp(col, 0, W - 1)];
  const centre = (col, rowFromSouth) => [-380 + (col + 0.5) / 2, -280 + (rowFromSouth + 0.5) / 2];
  const colOf = (x) => Math.floor((x + 380) * 2), rowOf = (y) => Math.floor((y + 280) * 2);
  return { at, elev, texel, centre, colOf, rowOf, W, H };
}

module.exports = { decode, field, sha, W, H, lerp, f };

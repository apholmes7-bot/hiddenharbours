// Minimal PNG decode/encode for the recipe verifier.
//
// The verifier compares RGBA PIXEL BYTES, never PNG container bytes. Unity's `EncodeToPNG` is the
// only thing that produced the committed containers, and reproducing its zlib window, filter choice
// and chunk layout outside Unity is neither possible nor the point — what a recipe has to reproduce
// is the IMAGE. So: decode the committed sheet, render the recipe, compare byte for byte.
import zlib from 'node:zlib';

/** Decodes a non-interlaced 8-bit PNG to `{width, height, rgba}`. */
export function decodePng(buf) {
  if (buf.length < 8 || buf.readUInt32BE(0) !== 0x89504e47) throw new Error('not a PNG');
  let off = 8, w = 0, h = 0, bitDepth = 0, colour = 0, interlace = 0;
  const idat = [];
  let pal = null, trns = null;

  while (off + 8 <= buf.length) {
    const len = buf.readUInt32BE(off);
    const type = buf.toString('ascii', off + 4, off + 8);
    const data = buf.subarray(off + 8, off + 8 + len);
    if (type === 'IHDR') {
      w = data.readUInt32BE(0); h = data.readUInt32BE(4);
      bitDepth = data[8]; colour = data[9]; interlace = data[12];
    } else if (type === 'PLTE') pal = Buffer.from(data);
    else if (type === 'tRNS') trns = Buffer.from(data);
    else if (type === 'IDAT') idat.push(Buffer.from(data));
    else if (type === 'IEND') break;
    off += 12 + len;
  }
  if (interlace !== 0) throw new Error('interlaced PNG unsupported');
  if (bitDepth !== 8) throw new Error(`bit depth ${bitDepth} unsupported`);

  const chans = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 }[colour];
  if (chans === undefined) throw new Error(`colour type ${colour} unsupported`);

  const raw = zlib.inflateSync(Buffer.concat(idat));
  const stride = w * chans;
  const flat = Buffer.alloc(h * stride);
  let prev = Buffer.alloc(stride);

  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)];
    const line = raw.subarray(y * (stride + 1) + 1, y * (stride + 1) + 1 + stride);
    const cur = flat.subarray(y * stride, (y + 1) * stride);
    for (let i = 0; i < stride; i++) {
      const a = i >= chans ? cur[i - chans] : 0;
      const b = prev[i];
      const c = i >= chans ? prev[i - chans] : 0;
      let v = line[i];
      if (f === 1) v += a;
      else if (f === 2) v += b;
      else if (f === 3) v += (a + b) >> 1;
      else if (f === 4) {
        const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
        v += (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
      }
      cur[i] = v & 0xff;
    }
    prev = cur;
  }

  const rgba = Buffer.alloc(w * h * 4);
  for (let i = 0; i < w * h; i++) {
    let r, g, b, a = 255;
    if (colour === 6) { r = flat[i * 4]; g = flat[i * 4 + 1]; b = flat[i * 4 + 2]; a = flat[i * 4 + 3]; }
    else if (colour === 2) { r = flat[i * 3]; g = flat[i * 3 + 1]; b = flat[i * 3 + 2]; }
    else if (colour === 0) { r = g = b = flat[i]; }
    else if (colour === 4) { r = g = b = flat[i * 2]; a = flat[i * 2 + 1]; }
    else { const p = flat[i]; r = pal[p * 3]; g = pal[p * 3 + 1]; b = pal[p * 3 + 2]; if (trns && p < trns.length) a = trns[p]; }
    rgba[i * 4] = r; rgba[i * 4 + 1] = g; rgba[i * 4 + 2] = b; rgba[i * 4 + 3] = a;
  }
  return { width: w, height: h, rgba, colourType: colour };
}

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
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
  const td = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(td));
  return Buffer.concat([len, td, crc]);
}

/** RGBA8 → PNG. Used only for the human-readable DIFF images a refusal writes; never for a compare. */
export function encodePng(rgba, w, h) {
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) {
    raw[y * (w * 4 + 1)] = 0;
    Buffer.from(rgba.buffer ?? rgba, rgba.byteOffset ?? 0, rgba.length)
      .copy(raw, y * (w * 4 + 1) + 1, y * w * 4, (y + 1) * w * 4);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; ihdr[9] = 6;
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

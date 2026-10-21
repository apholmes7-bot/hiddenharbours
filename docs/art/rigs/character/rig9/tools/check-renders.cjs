/* tools/check-renders.cjs — the review renders, re-rendered and compared to the pixel.
     node tools/check-renders.cjs      writes reports/renders.{json,txt}
   renders/manifest.json lists every PNG: preset, clip, facing, frames, scale, size, and rgbaSha256, the SHA-256 of its pixels
   as straight RGBA bytes, row-major (alpha 0 or 255; empty pixels 0,0,0,0). This checker renders every strip again with
   CharacterIso9.render({ clip, frame, dir, build }) (keyline, contour and head snap on; 32 px/m, 40 deg), hashes the pixels,
   decodes the PNG (8-bit RGB / RGBA / palette, non-interlaced; zlib from Node) and counts the pixels that differ.
   A strip is the clip's frames left to right, one 64 x 92 cell each (x scale), pivot at (32, 82) in every cell.
   front = S (dir 4, facing the camera), back = N (dir 0). */
'use strict';
(function () {
function run(C, H) {
  const MAN = 'renders/manifest.json', problems = [], rigSha = H.sha256('Art/characterIsoRig9.js'), poseSha = H.sha256('Art/characterIsoRig9.poses.js');
  const man = JSON.parse(H.text(MAN));
  if (man.derivedFromRigSha256 !== rigSha || man.posesDerivedFromRigSha256 !== poseSha) problems.push('manifest stamps do not match the rig / pose library on disk');
  const u32 = (b, o) => ((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]) >>> 0;
  function decodePNG(b) { const sig = [137, 80, 78, 71, 13, 10, 26, 10]; for (let i = 0; i < 8; i++) if (b[i] !== sig[i]) throw new Error('not a PNG');
    let o = 8, w = 0, h = 0, depth = 0, type = 0, inter = 0, plte = null, trns = null; const idat = [];
    while (o < b.length) { const len = u32(b, o), t = String.fromCharCode(b[o + 4], b[o + 5], b[o + 6], b[o + 7]), d = b.subarray(o + 8, o + 8 + len);
      if (t === 'IHDR') { w = u32(d, 0); h = u32(d, 4); depth = d[8]; type = d[9]; inter = d[12]; } else if (t === 'PLTE') plte = d; else if (t === 'tRNS') trns = d; else if (t === 'IDAT') idat.push(d); else if (t === 'IEND') break;
      o += 12 + len; }
    if (depth !== 8 || inter !== 0 || [2, 3, 6].indexOf(type) < 0) throw new Error('unsupported PNG (depth ' + depth + ', type ' + type + ', interlace ' + inter + ')');
    const n = idat.reduce((s, x) => s + x.length, 0), z = new Uint8Array(n); let k = 0; for (const x of idat) { z.set(x, k); k += x.length; }
    const raw = H.inflate(z), bpp = type === 6 ? 4 : type === 2 ? 3 : 1, stride = w * bpp, px = new Uint8Array(h * stride);
    for (let y = 0; y < h; y++) { const f = raw[y * (stride + 1)], src = y * (stride + 1) + 1, dst = y * stride;
      for (let x = 0; x < stride; x++) { const a = x >= bpp ? px[dst + x - bpp] : 0, up = y ? px[dst - stride + x] : 0, c = (x >= bpp && y) ? px[dst - stride + x - bpp] : 0, r = raw[src + x];
        let v; if (f === 0) v = r; else if (f === 1) v = r + a; else if (f === 2) v = r + up; else if (f === 3) v = r + ((a + up) >> 1);
        else if (f === 4) { const p = a + up - c, pa = Math.abs(p - a), pb = Math.abs(p - up), pc = Math.abs(p - c); v = r + (pa <= pb && pa <= pc ? a : pb <= pc ? up : c); } else throw new Error('bad filter ' + f);
        px[dst + x] = v & 255; } }
    const rgba = new Uint8Array(w * h * 4);
    for (let i = 0; i < w * h; i++) { if (type === 6) { rgba.set(px.subarray(i * 4, i * 4 + 4), i * 4); continue; }
      if (type === 2) { rgba[i * 4] = px[i * 3]; rgba[i * 4 + 1] = px[i * 3 + 1]; rgba[i * 4 + 2] = px[i * 3 + 2]; rgba[i * 4 + 3] = 255; continue; }
      const q = px[i]; rgba[i * 4] = plte[q * 3]; rgba[i * 4 + 1] = plte[q * 3 + 1]; rgba[i * 4 + 2] = plte[q * 3 + 2]; rgba[i * 4 + 3] = trns && q < trns.length ? trns[q] : 255; }
    return { w, h, rgba }; }
  function strip(e) { const n = e.frames, s = e.scale, W = C.W * n * s, Hh = C.H * s, out = new Uint8Array(W * Hh * 4);
    for (let k = 0; k < n; k++) { const R = C.render({ clip:e.clip, frame:k, dir:e.dir, build:e.preset });
      for (let y = 0; y < Hh; y++) for (let x = 0; x < C.W * s; x++) { const a = (Math.floor(y / s) * C.W + Math.floor(x / s)) * 4, d = (y * W + k * C.W * s + x) * 4;
        out[d] = R.rgba[a]; out[d + 1] = R.rgba[a + 1]; out[d + 2] = R.rgba[a + 2]; out[d + 3] = R.rgba[a + 3]; } }
    return { W, H:Hh, rgba:out }; }
  const rows = []; let pxBad = 0, hashBad = 0, fileBad = 0;
  for (const e of man.images) { const row = { file:e.file }; let mine, png;
    try { mine = strip(e); png = decodePNG(H.bytes(e.file)); } catch (err) { row.error = String(err.message || err); problems.push(e.file + ': ' + row.error); rows.push(row); continue; }
    row.size = png.w + 'x' + png.h; if (png.w !== mine.W || png.h !== mine.H || png.w !== e.width || png.h !== e.height) problems.push(e.file + ': size ' + row.size + ', expected ' + mine.W + 'x' + mine.H);
    let d = 0; if (png.w === mine.W && png.h === mine.H) for (let i = 0; i < mine.rgba.length; i += 4) { const ea = png.rgba[i + 3] === 0 && mine.rgba[i + 3] === 0;
      if (!ea && (png.rgba[i] !== mine.rgba[i] || png.rgba[i + 1] !== mine.rgba[i + 1] || png.rgba[i + 2] !== mine.rgba[i + 2] || png.rgba[i + 3] !== mine.rgba[i + 3])) d++; }
    row.pixelsDiffer = d; if (d) { pxBad++; problems.push(e.file + ': ' + d + ' px differ from a fresh render'); }
    row.rgbaSha256Ok = H.sha256Bytes(mine.rgba) === e.rgbaSha256; if (!row.rgbaSha256Ok) { hashBad++; problems.push(e.file + ': fresh render hash differs from the manifest'); }
    row.pngSha256Ok = H.sha256(e.file) === e.pngSha256; if (!row.pngSha256Ok) { fileBad++; problems.push(e.file + ': file hash differs from the manifest'); }
    rows.push(row); }
  const listed = {}; for (const e of man.images) listed[e.file] = 1;
  const extra = H.list('renders/').filter((p) => p.endsWith('.png') && !listed[p]); for (const p of extra) problems.push(p + ' is not in the manifest');
  const L = [];
  L.push('check-renders — review renders against fresh renders', 'rig ' + rigSha + '  poses ' + poseSha, '');
  L.push(man.images.length + ' PNGs (' + C.CAST.length + ' presets x ' + man.clips.join(', ') + ' x front, back x 1x, 4x).');
  L.push('pixels identical to a fresh render: ' + (man.images.length - pxBad) + ' / ' + man.images.length + '; manifest pixel hash: ' + (man.images.length - hashBad) + ' / ' + man.images.length + '; file hash: ' + (man.images.length - fileBad) + ' / ' + man.images.length + '; unlisted PNGs: ' + extra.length);
  for (const r of rows) if (r.error || r.pixelsDiffer || !r.rgbaSha256Ok || !r.pngSha256Ok) L.push('  ' + r.file + '  ' + (r.error || (r.pixelsDiffer + ' px, hash ' + r.rgbaSha256Ok + ', file ' + r.pngSha256Ok)));
  L.push('', problems.length ? 'PROBLEMS:\n  ' + problems.join('\n  ') : 'OK — no problems.', '');
  const json = { tool:'check-renders', rig:rigSha, poses:poseSha, ok:problems.length === 0, problems, images:rows };
  return { name:'renders', ok:json.ok, json, text:L.join('\n') };
}
if (typeof module !== 'undefined' && module.exports) { module.exports = run; if (require.main === module) require('./_run.cjs').main(run); }
else (globalThis.HHTools = globalThis.HHTools || {}).renders = run;
})();

'use strict';
const assert = require('node:assert/strict'), fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const folder = process.argv[2] || 'Assets/_Project/Art/Sprites/NineMileCreek/FuelStop';
const raw = fs.readFileSync(path.join(folder, 'fuelStop.today.json'), 'utf8').replaceAll('\r\n', '\n');
const today = JSON.parse(raw), contract = JSON.parse(fs.readFileSync(path.join(folder, 'fuelStop.contract.json')));
for (const p of today.pieces) {
  const sidecar = JSON.parse(fs.readFileSync(path.join(__dirname, '../gameplay', p.key+'.gameplay.json')));
  assert.deepEqual(sidecar.gameplay, p.gameplay);
  assert.equal(sidecar.derivedFromRigSha256, '1fc476b3a5e8d7ed1099c2680a7db931e0bf91d444ade8cb5a64134b74839e25');
}
const {ctx} = require('./write-today.cjs');
const zlib = require('node:zlib');
function decode(png) {
  assert.equal(png[24], 8); assert.equal(png[25], 6);
  const w = png.readUInt32BE(16), h = png.readUInt32BE(20), chunks = [];
  for (let p = 8; p < png.length;) {
    const n = png.readUInt32BE(p), type = png.toString('ascii', p + 4, p + 8);
    if (type === 'IDAT') chunks.push(png.subarray(p + 8, p + 8 + n));
    p += n + 12;
  }
  const scan = zlib.inflateSync(Buffer.concat(chunks)), out = Buffer.alloc(w*h*4), stride = w*4;
  let p = 0;
  for (let y = 0; y < h; y++) {
    const filter = scan[p++]; assert.ok(filter <= 4);
    for (let x = 0; x < stride; x++) {
      const i = y*stride+x, a = x >= 4 ? out[i-4] : 0, b = y ? out[i-stride] : 0,
        c = y && x >= 4 ? out[i-stride-4] : 0;
      const paeth = () => {const q = a+b-c, da = Math.abs(q-a), db = Math.abs(q-b), dc = Math.abs(q-c);
        return da <= db && da <= dc ? a : db <= dc ? b : c;};
      const predictor = [0, a, b, Math.floor((a+b)/2), 0][filter];
      out[i] = (scan[p++] + (filter === 4 ? paeth() : predictor)) & 255;
    }
  }
  return out;
}
assert.equal(contract.todaySha256Lf, crypto.createHash('sha256').update(raw).digest('hex'));
assert.equal(contract.pieces.length, 17);
for (const p of contract.pieces) {
  const t = today.pieces.find(q => q.key === p.key);
  assert.equal(p.id, t.id); assert.equal(p.cells, 8);
  assert.equal(p.sheetW, p.cellW * p.cols); assert.equal(p.sheetH, p.cellH * p.rows);
  assert.ok(p.sheetW <= 4096 && p.sheetH <= 4096);
  const png = fs.readFileSync(path.join(folder, p.sheet + '.png'));
  assert.equal(png.subarray(1, 4).toString(), 'PNG');
  assert.equal(png.readUInt32BE(16), p.sheetW); assert.equal(png.readUInt32BE(20), p.sheetH);
  const pixels = process.argv.includes('--pixels') ? decode(png) : null;
  assert.equal(p.optionsJson, JSON.stringify(t.options));
  for (let i = 0; i < 8; i++) {
    const f = p.frames[i], c = t.gameplay.cells[f.rigDir];
    assert.equal(f.rigDir, (8-i)%8); assert.equal(f.cell, i); assert.equal(f.heading, -1);
    assert.ok(f.opaque > 0); assert.equal(f.nativeW, c.w); assert.equal(f.nativeH, c.h);
    assert.equal(f.pivotX + f.cropX, c.pivot[0]); assert.equal(f.pivotY + f.cropY, c.pivot[1]);
    assert.ok(f.pivotX >= 0 && f.pivotX < p.cellW && f.pivotY >= 0 && f.pivotY < p.cellH);
    if (pixels) {
      const r = ctx.FuelStop.render(p.key, f.rigDir, {sky: ctx.WeatherSky.at(today.bake.sky)});
      for (let y = 0; y < r.h; y++) for (let x = 0; x < r.w; x++) {
        const si = (y*r.w+x)*4, alpha = r.rgba[si+3];
        if (!alpha) continue;
        const dx = (i%p.cols)*p.cellW+x-f.cropX, dy = Math.floor(i/p.cols)*p.cellH+y-f.cropY;
        assert.ok(dx >= (i%p.cols)*p.cellW && dx < (i%p.cols+1)*p.cellW);
        assert.ok(dy >= Math.floor(i/p.cols)*p.cellH && dy < (Math.floor(i/p.cols)+1)*p.cellH);
        const di = (dy*p.sheetW+dx)*4;
        assert.equal(pixels[di+3], alpha, p.key+' alpha');
        if (alpha === 255) for (let k = 0; k < 3; k++) assert.equal(pixels[di+k], r.rgba[si+k], p.key+' RGB');
      }
    }
  }
}
console.log('PASS: 17 PNG sheets, 136 nonempty frames, dimensions, cap, facing order, pivots, options, and today hash');
if (process.argv.includes('--pixels')) console.log('PASS: decoded canvas PNG alpha and opaque RGB match native rig renders at every ink pixel');

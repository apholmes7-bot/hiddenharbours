'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm'), zlib = require('zlib'), crypto = require('crypto');
const source = fs.readFileSync(path.join(__dirname, 'plantIsoRig5.js'));
if (crypto.createHash('sha256').update(source).digest('hex') !== 'a80c485993a1a05930849298d688572864deaa873d6aa62435135bfc3ff0059b') throw Error('Rig hash mismatch');
vm.runInThisContext(source.toString('utf8'));
globalThis.PlantDormant5 = JSON.parse(fs.readFileSync(path.join(__dirname, 'dormant-states.json'), 'utf8'));
vm.runInThisContext(fs.readFileSync(path.join(__dirname, 'bakePlants.js'), 'utf8'));
const catalog = JSON.parse(fs.readFileSync(path.join(__dirname, 'catalog.json'), 'utf8'));
const destination = process.argv[2];
if (!destination) throw Error('Output folder required');
fs.mkdirSync(destination, { recursive: true });
const crcTable = Array.from({ length: 256 }, (_, i) => { let c = i; for (let k = 0; k < 8; k++) c = (c & 1) ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0; });
function chunk(type, data) {
    const body = Buffer.concat([Buffer.from(type), data]), out = Buffer.alloc(body.length + 8);
    out.writeUInt32BE(data.length); body.copy(out, 4); let crc = 0xffffffff;
    for (const b of body) crc = crcTable[(crc ^ b) & 255] ^ (crc >>> 8);
    out.writeUInt32BE((crc ^ 0xffffffff) >>> 0, body.length + 4); return out;
}
function png(w, h, rgba) {
    const header = Buffer.alloc(13); header.writeUInt32BE(w); header.writeUInt32BE(h, 4); header[8] = 8; header[9] = 6;
    const rows = Buffer.alloc((w * 4 + 1) * h);
    for (let y = 0; y < h; y++) Buffer.from(rgba.buffer, rgba.byteOffset + y * w * 4, w * 4).copy(rows, y * (w * 4 + 1) + 1);
    return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]), chunk('IHDR', header), chunk('IDAT', zlib.deflateSync(rows)), chunk('IEND', Buffer.alloc(0))]);
}
const sums = [];
for (const sp of catalog.species) for (let p = 0; p < sp.pages.length; p++) {
    for (const channel of catalog.channels) {
        const bytes = PlantBake5.page(sp, p, channel), name = sp.key + '_' + p + '_' + channel + '.png';
        fs.writeFileSync(path.join(destination, name), png(sp.pages[p].w, sp.pages[p].h, bytes));
        sums.push({ file: name, rgbaSha256: crypto.createHash('sha256').update(bytes).digest('hex') });
    }
    PlantRig5.clearCache();
    console.log(sp.key + ' page ' + p);
}
fs.writeFileSync(path.join(destination, 'bake-hashes.json'), JSON.stringify(sums, null, 2) + '\n');
